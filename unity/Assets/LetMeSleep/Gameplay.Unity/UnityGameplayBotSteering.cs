using System.Collections.Generic;
using UnityEngine;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld
    {
        private readonly Dictionary<uint, Vector3> botDetours = new Dictionary<uint, Vector3>();
        private sealed class HumanObstacleDetour
        {
            internal Collider Obstacle;
            internal Bounds Bounds;
            internal Vector3 Escape, LastPosition;
            internal Vector3[] Corners;
            internal int Face, Corner;
            internal uint Started, ProgressTick;
            internal float BestDistance = float.PositiveInfinity, Travel;
        }
        private sealed class HumanSteeringIntent
        {
            internal string Objective;
            internal uint Tick, LastSteered, RetryAfter;
            internal Vector3 Position;
            internal Collider RetryObstacle;
            internal HumanObstacleDetour Detour;
        }
        private readonly Dictionary<uint, HumanSteeringIntent> humanSteeringIntents = new Dictionary<uint, HumanSteeringIntent>();
        private static readonly float[] BotYawSamples = { 0, 45, -45, 90, -90, 135, -135, 180 };
        private static readonly float[] BotFlightSamples = { 0, .65f, -.65f };
        private static readonly float[] BotGroundSamples = { 0 };
#if UNITY_EDITOR
        private bool captureBotSteeringDiagnostic = false;
        private string lastBotSteeringDiagnostic;
        private string lastBotTraversalDiagnostic;
        public bool DisableBotHumanTraversalPredictionForDiagnostic;
#endif
        internal void ResetBotSteering() { botDetours.Clear(); humanSteeringIntents.Clear(); }
        internal void ObserveBotSteeringIntent(ActorSnapshot actor, uint tick, string objectiveId)
        {
            if (actor.Role != PlayerRole.Human || actor.LifeState != LifeState.Active || string.IsNullOrEmpty(objectiveId))
            { if (humanSteeringIntents.Remove(actor.ActorId)) botDetours.Remove(actor.ActorId); return; }
            if (!humanSteeringIntents.TryGetValue(actor.ActorId, out var intent) || intent.Objective != objectiveId ||
                tick < intent.Tick || (actor.Position.ToUnity() - intent.Position).sqrMagnitude > 4)
            {
                intent = new HumanSteeringIntent { Objective = objectiveId, LastSteered = tick };
                humanSteeringIntents[actor.ActorId] = intent; botDetours.Remove(actor.ActorId);
            }
            else if (intent.Detour != null && (intent.Tick > intent.LastSteered || tick - intent.Detour.Started >= 90))
            { intent.Tick = tick; EndHumanDetour(intent); }
            intent.Tick = tick; intent.Position = actor.Position.ToUnity();
        }
        internal Float3 SteerBot(ActorSnapshot self, Float3 desired)
        {
            Vector3 direction = desired.ToUnity(); direction.y = 0; direction.Normalize();
            bool hasIntent = self.Role == PlayerRole.Human && humanSteeringIntents.TryGetValue(self.ActorId, out _);
            if (hasIntent)
            {
                var intent = humanSteeringIntents[self.ActorId]; intent.LastSteered = intent.Tick;
                if (direction.sqrMagnitude < .1f) EndHumanDetour(intent);
                else if (TryHumanDetour(self, direction, intent, out var detour)) return detour.ToFloat();
            }
            var result = SteerBotReactive(self, desired);
            if (hasIntent && direction.sqrMagnitude > .1f && Vector3.Dot(direction, result.ToUnity()) < -.25f)
                RememberHumanReversal(self, direction, humanSteeringIntents[self.ActorId]);
            return result;
        }
        private Float3 SteerBotReactive(ActorSnapshot self, Float3 desired)
        {
            bool human = self.Role == PlayerRole.Human;
            var direction = desired.ToUnity(); if (human) direction.y = 0;
            direction.Normalize(); if (direction.sqrMagnitude < .1f) return Float3.Zero;
            const float lookAhead = .65f;
            float desiredRawClearance = BotClearanceCore(self, direction, lookAhead,
                human && humanSteeringIntents.ContainsKey(self.ActorId),
                out Collider desiredBlocker, out Vector3 desiredNormal,
                out bool desiredCanAdvance, out float desiredPredictedProgress, out bool canContinueDecision);
#if UNITY_EDITOR
            string desiredTraversalDiagnostic = lastBotTraversalDiagnostic;
#endif
            if (desiredRawClearance >= lookAhead || desiredCanAdvance || canContinueDecision)
            {
#if UNITY_EDITOR
                if (captureBotSteeringDiagnostic)
                    lastBotSteeringDiagnostic = FormatSteeringDiagnostic(desiredRawClearance,
                        desiredCanAdvance, desiredPredictedProgress, desiredBlocker,
                        desiredNormal, 0, lookAhead * 3 + .65f, desiredRawClearance,
                        desiredCanAdvance, desiredPredictedProgress, desiredBlocker, desiredNormal);
                if (captureBotSteeringDiagnostic) lastBotSteeringDiagnostic += " canContinueDecision=" + canContinueDecision;
#endif
                botDetours.Remove(self.ActorId); return direction.ToFloat();
            }
            float bestScore = float.NegativeInfinity; var best = Vector3.zero;
            float bestYaw = float.NaN, bestRawClearance = 0, bestPredictedProgress = 0;
            bool bestCanAdvance = false;
            Collider bestBlocker = null; Vector3 bestNormal = default;
            botDetours.TryGetValue(self.ActorId, out var previous);
            // Sweeps test actual map colliders. The motor remains the final collision authority.
            foreach (float yaw in BotYawSamples)
            foreach (float rise in human ? BotGroundSamples : BotFlightSamples)
            {
                var candidate = (Quaternion.AngleAxis(yaw, Vector3.up) * direction + Vector3.up * rise).normalized;
                float rawClearance = BotClearance(self, candidate, lookAhead,
                    out Collider blocker, out Vector3 normal, out bool canAdvance,
                    out float predictedProgress);
                if (rawClearance < .12f && !canAdvance) continue;
                // Preserve the established steering choice for a motor-certified advance,
                // while retaining its raw sweep distance separately for diagnostics.
                float decisionClearance = canAdvance ? lookAhead : rawClearance;
                float score = decisionClearance * 3 + Vector3.Dot(direction, candidate) * .65f + Vector3.Dot(previous, candidate) * .45f;
                if (score > bestScore)
                {
                    bestScore = score; best = candidate; bestYaw = yaw;
                    bestRawClearance = rawClearance; bestCanAdvance = canAdvance;
                    bestPredictedProgress = predictedProgress; bestBlocker = blocker;
                    bestNormal = normal;
                }
            }
#if UNITY_EDITOR
            if (captureBotSteeringDiagnostic)
            {
                lastBotSteeringDiagnostic = FormatSteeringDiagnostic(desiredRawClearance,
                    desiredCanAdvance, desiredPredictedProgress, desiredBlocker, desiredNormal,
                    bestYaw, bestScore, bestRawClearance, bestCanAdvance,
                    bestPredictedProgress, bestBlocker, bestNormal);
                lastBotTraversalDiagnostic = desiredTraversalDiagnostic;
            }
#endif
            botDetours[self.ActorId] = best; return best.ToFloat();
        }

        private void EndHumanDetour(HumanSteeringIntent intent)
        {
            if (intent.Detour != null)
            { intent.RetryObstacle = intent.Detour.Obstacle; intent.RetryAfter = intent.Tick + 60; }
            intent.Detour = null;
        }
        private void RememberHumanReversal(ActorSnapshot actor, Vector3 desired, HumanSteeringIntent intent)
        {
            if (intent.Detour != null) return;
            var hit = CastMotor(actor.ActorId, actor.Position.ToUnity(), desired * .65f,
                1.72f - .72f * actor.CrouchFraction, .25f, true);
            if (!hit.HasValue || Mathf.Abs(hit.Value.normal.y) >= .55f) return;
            var obstacle = hit.Value.collider;
            if (!obstacle || obstacle.attachedRigidbody || obstacle.GetComponentInParent<GameplayActorProxy>() ||
                intent.RetryObstacle == obstacle && intent.Tick < intent.RetryAfter) return;
            var bounds = obstacle.bounds;
            if (bounds.size.x > 4 || bounds.size.z > 4 || bounds.max.y - actor.Position.Y < .35f) return;
            int face = Mathf.Abs(hit.Value.normal.x) >= Mathf.Abs(hit.Value.normal.z)
                ? (hit.Value.normal.x >= 0 ? 0 : 2) : (hit.Value.normal.z >= 0 ? 1 : 3);
            intent.Detour = new HumanObstacleDetour { Obstacle = obstacle, Bounds = bounds, Face = face,
                LastPosition = actor.Position.ToUnity(), Started = intent.Tick, ProgressTick = intent.Tick };
        }
        // Faces/corners are counterclockwise: east/north/west/south and NE/NW/SW/SE.
        private static Vector3 DetourCorner(Bounds bounds, int corner, float height)
        {
            return new Vector3(corner == 0 || corner == 3 ? bounds.max.x + .45f : bounds.min.x - .45f,
                height, corner < 2 ? bounds.max.z + .45f : bounds.min.z - .45f);
        }
        private static Vector3 FaceOutward(int face) => face == 0 ? Vector3.right : face == 1 ? Vector3.forward : face == 2 ? Vector3.left : Vector3.back;
        private static bool OutsideFace(Vector3 position, Bounds bounds, int face) => face == 0 ? position.x >= bounds.max.x + .40f
            : face == 1 ? position.z >= bounds.max.z + .40f : face == 2 ? position.x <= bounds.min.x - .40f : position.z <= bounds.min.z - .40f;
        private bool TryHumanDetour(ActorSnapshot actor, Vector3 desired, HumanSteeringIntent intent, out Vector3 result)
        {
            result = Vector3.zero; var detour = intent.Detour; if (detour == null) return false;
            Vector3 position = actor.Position.ToUnity();
            detour.Travel += Vector3.Distance(position, detour.LastPosition); detour.LastPosition = position;
            if (!detour.Obstacle || !detour.Obstacle.enabled || !detour.Obstacle.gameObject.activeInHierarchy ||
                (detour.Obstacle.bounds.center - detour.Bounds.center).sqrMagnitude > .0004f ||
                (detour.Obstacle.bounds.size - detour.Bounds.size).sqrMagnitude > .0004f ||
                intent.Tick - detour.Started >= 90 || intent.Tick - detour.ProgressTick >= 30 || detour.Travel > 12)
            { EndHumanDetour(intent); return false; }
            // Query through the known obstacle's far plane, not merely the short horizon
            // which caused the bot to return to the same face after backing away.
            Vector3 absolute = new Vector3(Mathf.Abs(desired.x), 0, Mathf.Abs(desired.z));
            float far = Vector3.Dot(detour.Bounds.center - position, desired) + Vector3.Dot(detour.Bounds.extents, absolute) + .25f + Skin;
            if (far > 6) { EndHumanDetour(intent); return false; }
            var direct = far > 0 ? CastMotor(actor.ActorId, position, desired * Mathf.Max(.65f, far),
                1.72f - .72f * actor.CrouchFraction, .25f, true) : null;
            if (!direct.HasValue)
            { EndHumanDetour(intent); return false; }
            if (direct.Value.collider != detour.Obstacle)
            { EndHumanDetour(intent); return false; }
            if (detour.Escape.sqrMagnitude < .1f)
            {
                Vector3 outward = FaceOutward(detour.Face);
                float bestScore = float.NegativeInfinity;
                foreach (float yaw in BotYawSamples)
                {
                    if (Mathf.Abs(yaw) < 45 || Mathf.Abs(yaw) > 135) continue;
                    var candidate = (Quaternion.AngleAxis(yaw, Vector3.up) * desired).normalized;
                    if (Vector3.Dot(candidate, outward) <= .05f) continue;
                    float clearance = BotClearance(actor, candidate, .65f, out _, out _, out bool canAdvance, out _);
                    if (clearance < .65f && !canAdvance || !BotHumanCanTraverse(actor, candidate, .65f, out _)) continue;
                    float score = Vector3.Dot(candidate, desired) * .65f + Vector3.Dot(candidate, outward) * .45f;
                    if (score > bestScore) { bestScore = score; detour.Escape = candidate; }
                }
                if (detour.Escape.sqrMagnitude < .1f) return false; // Back out reactively until a lateral is certified.
                detour.ProgressTick = intent.Tick; detour.BestDistance = float.PositiveInfinity;
            }
            if (detour.Corners == null && OutsideFace(position, detour.Bounds, detour.Face))
            {
                Vector3 tangent = Vector3.Cross(FaceOutward(detour.Face), Vector3.up);
                int step = Vector3.Dot(detour.Escape, tangent) >= 0 ? 1 : -1;
                int first = step > 0 ? detour.Face : (detour.Face + 3) % 4;
                detour.Corners = new Vector3[3];
                for (int index = 0; index < 3; index++) detour.Corners[index] = DetourCorner(detour.Bounds, (first + step * index + 4) % 4, position.y);
                // Keep the already acquired outward clearance on the first edge.
                var corner = detour.Corners[0];
                if (detour.Face == 0) corner.x = Mathf.Max(corner.x, position.x);
                else if (detour.Face == 2) corner.x = Mathf.Min(corner.x, position.x);
                else if (detour.Face == 1) corner.z = Mathf.Max(corner.z, position.z);
                else corner.z = Mathf.Min(corner.z, position.z);
                detour.Corners[0] = corner; detour.BestDistance = float.PositiveInfinity; detour.ProgressTick = intent.Tick;
                float length = Vector3.Distance(position, corner);
                for (int index = 1; index < 3; index++) length += Vector3.Distance(detour.Corners[index - 1], detour.Corners[index]);
                if (length + detour.Travel > 12) { EndHumanDetour(intent); return false; }
            }
            Vector3 travel = detour.Escape; float remaining;
            if (detour.Corners != null)
            {
                while (detour.Corner < 3)
                {
                    travel = detour.Corners[detour.Corner] - position; travel.y = 0;
                    if (travel.magnitude >= .17f) break;
                    detour.Corner++; detour.ProgressTick = intent.Tick; detour.BestDistance = float.PositiveInfinity;
                }
                if (detour.Corner >= 3) { EndHumanDetour(intent); return false; }
                remaining = travel.magnitude; travel.Normalize();
            }
            else
            {
                Vector3 outward = FaceOutward(detour.Face);
                float edge = Vector3.Dot(detour.Bounds.center - position, outward) +
                    (detour.Face % 2 == 0 ? detour.Bounds.extents.x : detour.Bounds.extents.z) + .45f;
                remaining = Mathf.Max(0, edge);
            }
            if (remaining < detour.BestDistance - .03f)
            { detour.BestDistance = remaining; detour.ProgressTick = intent.Tick; }
            // Certify the next cadence interval with the real motor predictor, including
            // support. A waypoint/AABB is not permission to cross a wall or a drop.
            var chosen = SteerBotReactive(actor, travel.ToFloat()).ToUnity();
            if (Vector3.Dot(chosen, travel) >= .5f && BotHumanCanTraverse(actor, chosen, .35f, out _)) result = chosen;
#if UNITY_EDITOR
            if (captureBotSteeringDiagnostic) lastBotSteeringDiagnostic += " obstacleDetour=active corner=" + detour.Corner;
#endif
            return true;
        }
        private float BotClearance(ActorSnapshot actor, Vector3 direction, float distance,
            out Collider blocker, out Vector3 normal, out bool canAdvance,
            out float predictedProgress)
            => BotClearanceCore(actor, direction, distance, false, out blocker, out normal,
                out canAdvance, out predictedProgress, out _);

        private float BotClearanceCore(ActorSnapshot actor, Vector3 direction, float distance,
            bool allowDecisionContinuation, out Collider blocker, out Vector3 normal,
            out bool canAdvance, out float predictedProgress, out bool canContinueDecision)
        {
            var position = actor.Position.ToUnity(); bool human = actor.Role == PlayerRole.Human;
            float nearest = distance;
            Vector3 contactPoint = default;
            blocker = null; normal = default; canAdvance = false; predictedProgress = 0;
            canContinueDecision = false;
            if (human)
            {
                // Use the physical motor capsule, contact treatment and actor filtering.
                // A larger steering capsule can begin inside furniture when the real body
                // is clear, rejecting every exit. Ignoring other humans also deadlocks
                // two bots whose motor capsules correctly block one another.
                var hit = CastMotor(actor.ActorId, position, direction * distance,
                    1.72f - .72f * actor.CrouchFraction, .25f, true);
                if (hit.HasValue && hit.Value.distance < nearest)
                {
                    nearest = hit.Value.distance; blocker = hit.Value.collider;
                    normal = hit.Value.normal; contactPoint = hit.Value.point;
                }
            }
            else
            {
                foreach (var hit in Physics.SphereCastAll(position, .065f, direction, distance,
                             GeometryMask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsWorldCollider(hit.collider) || hit.collider.GetComponentInParent<GameplayActorProxy>()) continue;
                    if (hit.distance >= nearest) continue;
                    nearest = hit.distance; blocker = hit.collider; normal = hit.normal; contactPoint = hit.point;
                }
            }
            // Steering must not reject ground geometry that the real human motor can
            // traverse as a slope or authored step. Predict with the same CastMotor,
            // projection and TryStep queries, without moving the actor or its collider.
            bool predictionEnabled = true;
#if UNITY_EDITOR
            predictionEnabled = !DisableBotHumanTraversalPredictionForDiagnostic;
#endif
            if (predictionEnabled && human && nearest < distance &&
                BotMayTraverseContact(actor.Position.ToUnity(), blocker, contactPoint, normal, actor.Grounded))
                canAdvance = BotHumanTraversalPrediction(actor, direction, distance, allowDecisionContinuation,
                    out predictedProgress, out canContinueDecision);
            return nearest;
        }

        private static bool BotMayTraverseContact(Vector3 footPosition, Collider blocker, Vector3 contactPoint, Vector3 normal,
            bool canStep)
        {
            if (!blocker) return false;
            bool walkableSupport = normal.y >= .55f;
            bool completeLowObstacle = blocker.bounds.max.y - footPosition.y <= .22f + Skin;
            // A staircase may be one tall mesh. Its first tread still presents a low,
            // upward-facing capsule contact; the full collider AABB cannot describe that
            // local rise. Allow only the low upward contact into the motor prediction.
            // Projected ascent can briefly be airborne, so its next edge is checked by
            // height and normal rather than assigning an artificial grounded state.
            float localRise = contactPoint.y - footPosition.y;
            bool lowUpwardContact = normal.y > .05f && localRise >= -Skin && localRise <= .22f + Skin;
            return walkableSupport || canStep && completeLowObstacle || lowUpwardContact;
        }

        private bool BotHumanCanTraverse(ActorSnapshot actor, Vector3 direction, float distance,
            out float predictedProgress)
            => BotHumanTraversalPrediction(actor, direction, distance, false, out predictedProgress, out _);

        private bool BotHumanTraversalPrediction(ActorSnapshot actor, Vector3 direction, float distance,
            bool allowDecisionContinuation, out float predictedProgress, out bool canContinueDecision)
        {
            Vector3 start = actor.Position.ToUnity(), position = start;
            predictedProgress = 0;
            canContinueDecision = false;
            Vector3 decisionPosition = start;
            float decisionVerticalVelocity = 0, decisionProgress = 0;
            bool decisionGrounded = false, decisionComplete = false;
            float height = 1.72f - .72f * actor.CrouchFraction;
            const float radius = .25f;
            const float dt = 1f / 30f;
            float speed = actor.CrouchFraction > .1f ? 1.55f : 3.1f;
            float verticalVelocity = actor.Velocity.Y;
            bool grounded = actor.Grounded;
            // Model the authority's repeated human ticks. A single .65 m displacement is
            // not equivalent: desired horizontal velocity is reapplied every tick, while
            // vertical velocity and grounded state carry through the real motor result.
            // Cover the requested corridor with actual projected progress. A stair can
            // spend several ticks rising; charging the requested input distance would
            // stop before a later wall while still advertising the full look-ahead.
            // Bound the extra work, and refuse certification if that bound is exhausted.
            int maximumTicks = Mathf.CeilToInt(distance / (speed * dt)) * 3;
            int tick = 0;
            while (predictedProgress < distance && tick < maximumTicks)
            {
                verticalVelocity = grounded ? -.5f : verticalVelocity - 12 * dt;
                // The authority never slows the final tick to fit a query distance.
                Vector3 velocity = direction.normalized * speed +
                                   Vector3.up * verticalVelocity;
                Vector3 before = position;
                if (!BotHumanTick(actor.ActorId, grounded, ref position, ref velocity,
                        height, radius, tick, out grounded))
                {
                    predictedProgress = Vector3.Dot(position - start, direction.normalized);
                    // A later rejected contact still blocks the full horizon. Only the
                    // already completed authority interval may be used before re-querying.
                    // Never count partial motion inside this failed tick as certification.
                    if (allowDecisionContinuation && decisionComplete && decisionProgress >= .12f &&
                        decisionPosition.y >= start.y - .23f &&
                        actors.TryGetValue(actor.ActorId, out var proxy) &&
                        !Overlap(proxy.MotorCollider, start, proxy.transform.rotation, out _) &&
                        !Overlap(proxy.MotorCollider, decisionPosition, proxy.transform.rotation, out _))
                    {
                        var decisionSupport = CastMotor(actor.ActorId, decisionPosition,
                            Vector3.down * .23f, height, radius, true);
                        canContinueDecision = decisionSupport.HasValue && decisionSupport.Value.normal.y > .55f ||
                            BotHumanCanSettle(actor.ActorId, decisionPosition, decisionVerticalVelocity,
                                decisionGrounded, height, radius, (int)GameplayRuntime.BotDecisionIntervalTicks);
                    }
                    return false;
                }
                verticalVelocity = velocity.y;
                if (Vector3.Dot(position - before, direction.normalized) <= Skin)
                {
                    TraceTraversal("tick-stalled", tick, position, null, Vector3.zero);
                    predictedProgress = Vector3.Dot(position - start, direction.normalized);
                    return false;
                }
                predictedProgress = Vector3.Dot(position - start, direction.normalized);
                tick++;
                if (allowDecisionContinuation && tick == GameplayRuntime.BotDecisionIntervalTicks)
                {
                    decisionPosition = position; decisionVerticalVelocity = verticalVelocity;
                    decisionGrounded = grounded; decisionProgress = predictedProgress; decisionComplete = true;
                }
            }
            if (predictedProgress < distance)
            {
                TraceTraversal("horizon-exhausted", tick, position, null, Vector3.zero);
                return false;
            }
            // This is a support query, not a snap or a change to predicted velocity.
            // Short airborne arcs over a tread are valid; ending over a drop with no
            // walkable support within the motor's grounded snap range is not certified.
            RaycastHit? support = CastMotor(actor.ActorId, position, Vector3.down * .23f, height, radius, true);
            if ((!support.HasValue || support.Value.normal.y <= .55f) &&
                !BotHumanCanSettle(actor.ActorId, position, verticalVelocity, grounded, height, radius, tick))
            {
                TraceTraversal("unsupported-endpoint", tick, position, support?.collider, support?.normal ?? Vector3.zero);
                return false;
            }
            TraceTraversal("complete", tick, position, null, Vector3.zero);
            return true;
        }

        private bool BotHumanCanSettle(uint actorId, Vector3 endpoint, float verticalVelocity,
            bool grounded, float height, float radius, int tick)
        {
            // A downward cast can meet the next tread's rounded capsule contact before
            // the supporting tread. Do not call that steep contact walkable. Instead
            // model stopping horizontal input and let the real motor rules resolve it.
            // These are local copies: this probe never moves or grounds the actor.
            Vector3 position = endpoint;
            int supportedTicks = 0;
            for (int settle = 0; settle < 18; settle++)
            {
                verticalVelocity = grounded ? -.5f : verticalVelocity - 12f / 30f;
                Vector3 velocity = Vector3.up * verticalVelocity;
                if (!BotHumanTick(actorId, grounded, ref position, ref velocity,
                        height, radius, tick + settle, out grounded)) return false;
                verticalVelocity = velocity.y;
                Vector3 drift = position - endpoint;
                // A fall or a slide to a different ledge cannot certify this endpoint.
                if (drift.y < -.23f || new Vector2(drift.x, drift.z).sqrMagnitude > radius * radius)
                    return false;
                supportedTicks = grounded ? supportedTicks + 1 : 0;
                if (supportedTicks >= 2) return true;
            }
            return false;
        }

        private bool BotHumanTick(uint actorId, bool wasGrounded, ref Vector3 position,
            ref Vector3 velocity, float height, float radius, int tick, out bool grounded)
        {
            Vector3 remaining = velocity / 30f;
            grounded = false;
            for (int i = 0; i < 5 && remaining.sqrMagnitude > 1e-12f; i++)
            {
                RaycastHit? hit = CastMotor(actorId, position, remaining, height, radius, true);
                if (!hit.HasValue) { position += remaining; break; }
                RaycastHit contact = hit.Value;
                // A low/ground contact may reveal a second obstacle within the same
                // horizon. Certify every actual motor contact so a step cannot license a
                // later tall wall merely because projection finds lateral open space.
                if (!BotMayTraverseContact(position, contact.collider, contact.point, contact.normal, wasGrounded))
                {
                    TraceTraversal("contact-rejected", tick, position,
                        contact.collider, contact.normal);
                    return false;
                }
                float travel = Mathf.Max(0, contact.distance - Skin);
                float advance = Mathf.Min(travel, remaining.magnitude);
                position += remaining.normalized * advance;
                remaining -= remaining.normalized * advance;
                if (wasGrounded && Mathf.Abs(contact.normal.y) < .3f &&
                    TryStep(actorId, position, remaining, height, radius, out Vector3 stepped))
                { position = stepped; remaining = Vector3.zero; grounded = true; break; }
                if (contact.normal.y > .55f) grounded = true;
                remaining = Vector3.ProjectOnPlane(remaining, contact.normal);
                velocity = Vector3.ProjectOnPlane(velocity, contact.normal);
            }
            if (velocity.y <= .1f)
            {
                float snap = wasGrounded ? .23f : .009f;
                RaycastHit? below = CastMotor(actorId, position, Vector3.down * snap, height, radius, true);
                if (below.HasValue && below.Value.normal.y > .55f)
                {
                    grounded = true;
                    position += Vector3.down * Mathf.Max(0, below.Value.distance - Skin);
                    velocity.y = 0;
                }
            }
            return true;
        }
#if UNITY_EDITOR
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceTraversal(string reason, int segment, Vector3 position,
            Collider collider, Vector3 normal)
        {
            if (!captureBotSteeringDiagnostic) return;
            lastBotTraversalDiagnostic = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "reason={0} segment={1} position={2:R},{3:R},{4:R} collider={5} normal={6:R},{7:R},{8:R}",
                reason, segment, position.x, position.y, position.z, BotColliderPath(collider),
                normal.x, normal.y, normal.z);
        }
#else
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceTraversal(string reason, int segment, Vector3 position,
            Collider collider, Vector3 normal) { }
#endif
#if UNITY_EDITOR
        private string FormatSteeringDiagnostic(float desiredRawClearance, bool desiredCanAdvance,
            float desiredPredictedProgress, Collider desiredBlocker, Vector3 desiredNormal,
            float bestYaw, float bestScore, float bestRawClearance, bool bestCanAdvance,
            float bestPredictedProgress, Collider bestBlocker, Vector3 bestNormal) => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "desiredRawClearance={0:R} desiredCanAdvance={1} desiredPredictedProgress={2:R} " +
            "desiredBlocker={3} desiredNormal={4:R},{5:R},{6:R} bestYaw={7:R} bestScore={8:R} " +
            "bestRawClearance={9:R} bestCanAdvance={10} bestPredictedProgress={11:R} " +
            "bestBlocker={12} bestNormal={13:R},{14:R},{15:R}",
            desiredRawClearance, desiredCanAdvance, desiredPredictedProgress,
            BotColliderPath(desiredBlocker), desiredNormal.x, desiredNormal.y, desiredNormal.z,
            bestYaw, bestScore, bestRawClearance, bestCanAdvance, bestPredictedProgress,
            BotColliderPath(bestBlocker), bestNormal.x, bestNormal.y, bestNormal.z);

        private string BotColliderPath(Collider collider)
        {
            if (!collider) return "none";
            var parts = new List<string>();
            Transform current = collider.transform;
            while (current && current != MapRoot)
            { parts.Add(current.name); current = current.parent; }
            parts.Reverse();
            return string.Join("/", parts) + "[" + collider.GetType().Name + "]";
        }
#endif
    }
}
