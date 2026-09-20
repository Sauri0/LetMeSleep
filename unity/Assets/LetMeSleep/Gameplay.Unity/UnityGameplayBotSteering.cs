using System.Collections.Generic;
using UnityEngine;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld
    {
        private readonly Dictionary<uint, Vector3> botDetours = new Dictionary<uint, Vector3>();
        private static readonly float[] BotYawSamples = { 0, 45, -45, 90, -90, 135, -135, 180 };
        private static readonly float[] BotFlightSamples = { 0, .65f, -.65f };
        private static readonly float[] BotGroundSamples = { 0 };
#if UNITY_EDITOR
        private bool captureBotSteeringDiagnostic = false;
        private string lastBotSteeringDiagnostic;
        private string lastBotTraversalDiagnostic;
        public bool DisableBotHumanTraversalPredictionForDiagnostic;
#endif
        internal void ResetBotSteering() => botDetours.Clear();
        internal Float3 SteerBot(ActorSnapshot self, Float3 desired)
        {
            bool human = self.Role == PlayerRole.Human;
            var direction = desired.ToUnity(); if (human) direction.y = 0;
            direction.Normalize(); if (direction.sqrMagnitude < .1f) return Float3.Zero;
            const float lookAhead = .65f;
            float desiredRawClearance = BotClearance(self, direction, lookAhead,
                out Collider desiredBlocker, out Vector3 desiredNormal,
                out bool desiredCanAdvance, out float desiredPredictedProgress);
#if UNITY_EDITOR
            string desiredTraversalDiagnostic = lastBotTraversalDiagnostic;
#endif
            if (desiredRawClearance >= lookAhead || desiredCanAdvance)
            {
#if UNITY_EDITOR
                if (captureBotSteeringDiagnostic)
                    lastBotSteeringDiagnostic = FormatSteeringDiagnostic(desiredRawClearance,
                        desiredCanAdvance, desiredPredictedProgress, desiredBlocker,
                        desiredNormal, 0, lookAhead * 3 + .65f, desiredRawClearance,
                        desiredCanAdvance, desiredPredictedProgress, desiredBlocker, desiredNormal);
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
        private float BotClearance(ActorSnapshot actor, Vector3 direction, float distance,
            out Collider blocker, out Vector3 normal, out bool canAdvance,
            out float predictedProgress)
        {
            var position = actor.Position.ToUnity(); bool human = actor.Role == PlayerRole.Human;
            float radius = human ? .26f : .065f;
            var hits = human
                ? Physics.CapsuleCastAll(position + Vector3.up * .29f, position + Vector3.up * (1.46f - .64f * actor.CrouchFraction), radius, direction, distance, GeometryMask, QueryTriggerInteraction.Ignore)
                : Physics.SphereCastAll(position, radius, direction, distance, GeometryMask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            blocker = null; normal = default; canAdvance = false; predictedProgress = 0;
            foreach (var hit in hits)
            {
                if (!IsWorldCollider(hit.collider) || hit.collider.GetComponentInParent<GameplayActorProxy>()) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance; blocker = hit.collider; normal = hit.normal;
            }
            // Steering must not reject ground geometry that the real human motor can
            // traverse as a slope or authored step. Predict with the same CastMotor,
            // projection and TryStep queries, without moving the actor or its collider.
            bool predictionEnabled = true;
#if UNITY_EDITOR
            predictionEnabled = !DisableBotHumanTraversalPredictionForDiagnostic;
#endif
            if (predictionEnabled && human && nearest < distance &&
                BotMayTraverseContact(actor.Position.ToUnity(), blocker, normal, actor.Grounded) &&
                BotHumanCanTraverse(actor, direction, distance, out predictedProgress))
                canAdvance = true;
            return nearest;
        }

        private static bool BotMayTraverseContact(Vector3 footPosition, Collider blocker, Vector3 normal,
            bool canStep)
        {
            if (!blocker) return false;
            bool walkableSupport = normal.y >= .55f;
            bool completeLowObstacle = blocker.bounds.max.y - footPosition.y <= .22f + Skin;
            return walkableSupport || canStep && completeLowObstacle;
        }

        private bool BotHumanCanTraverse(ActorSnapshot actor, Vector3 direction, float distance,
            out float predictedProgress)
        {
            Vector3 start = actor.Position.ToUnity(), position = start;
            predictedProgress = 0;
            float height = 1.72f - .72f * actor.CrouchFraction;
            const float radius = .25f;
            const float dt = 1f / 30f;
            float speed = actor.CrouchFraction > .1f ? 1.55f : 3.1f;
            float verticalVelocity = actor.Velocity.Y;
            bool grounded = actor.Grounded;
            // Model the authority's repeated human ticks. A single .65 m displacement is
            // not equivalent: desired horizontal velocity is reapplied every tick, while
            // vertical velocity and grounded state carry through the real motor result.
            float remainingDistance = distance;
            int tick = 0;
            while (remainingDistance > .0001f)
            {
                float horizontalDistance = Mathf.Min(speed * dt, remainingDistance);
                verticalVelocity = grounded ? -.5f : verticalVelocity - 12 * dt;
                Vector3 velocity = direction.normalized * (horizontalDistance / dt) +
                                   Vector3.up * verticalVelocity;
                Vector3 before = position;
                if (!BotHumanTick(actor.ActorId, grounded, ref position, ref velocity,
                        height, radius, tick, out grounded))
                {
                    predictedProgress = Vector3.Dot(position - start, direction.normalized);
                    return false;
                }
                verticalVelocity = velocity.y;
                if (Vector3.Dot(position - before, direction.normalized) <= Skin)
                {
                    TraceTraversal("tick-stalled", tick, position, null, Vector3.zero);
                    predictedProgress = Vector3.Dot(position - start, direction.normalized);
                    return false;
                }
                remainingDistance -= horizontalDistance;
                tick++;
            }
            predictedProgress = Vector3.Dot(position - start, direction.normalized);
            TraceTraversal("complete", tick, position, null, Vector3.zero);
            return predictedProgress >= .12f;
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
                if (!BotMayTraverseContact(position, contact.collider, contact.normal, wasGrounded))
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
