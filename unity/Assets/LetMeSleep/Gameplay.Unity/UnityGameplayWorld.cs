using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed class UnityGameplayWorld : MonoBehaviour, IGameplayWorld
    {
        public LayerMask GeometryMask = ~0;
        public event Action<GameplayActorProxy> ActorCreated;
        public IReadOnlyDictionary<uint, GameplayActorProxy> Actors => actors;
        public IReadOnlyDictionary<uint, GameplayDoor> Doors => doors;
        private readonly Dictionary<uint, GameplayActorProxy> actors = new Dictionary<uint, GameplayActorProxy>();
        private readonly Dictionary<uint, GameplayDoor> doors = new Dictionary<uint, GameplayDoor>();
        private readonly Dictionary<uint, GameplaySurface> surfaces = new Dictionary<uint, GameplaySurface>();
        private const float Skin = .001f;

        public void RegisterGeometry()
        {
            surfaces.Clear(); doors.Clear();
            foreach (var door in FindObjectsByType<GameplayDoor>(FindObjectsSortMode.None))
            {
                door.Initialize();
                if (door.DoorId == 0 || !door.Leaf || doors.ContainsKey(door.DoorId)) throw new InvalidOperationException("Missing/duplicate door geometry.");
                doors.Add(door.DoorId, door);
            }
            foreach (var surface in FindObjectsByType<GameplaySurface>(FindObjectsSortMode.None))
            {
                if (surface.SurfaceId == 0 || surfaces.ContainsKey(surface.SurfaceId)) throw new InvalidOperationException("Duplicate/zero stable surface ID.");
                surfaces.Add(surface.SurfaceId, surface);
            }
        }
        public IReadOnlyList<DoorDefinition> GetDoorDefinitions() { RegisterGeometry(); return doors.Values.OrderBy(d => d.DoorId).Select(d => d.Definition).ToArray(); }
        public void BeginRound(IReadOnlyList<SpawnActor> roster, IReadOnlyList<DoorDefinition> definitions)
        {
            RegisterGeometry();
            foreach (var definition in definitions) if (!doors.ContainsKey(definition.DoorId) || doors[definition.DoorId].SurfaceId != definition.SurfaceId) throw new InvalidOperationException("Round door does not match map geometry.");
            if (definitions.Count != doors.Count) throw new InvalidOperationException("Round must include all map doors.");
            foreach (var actor in actors.Values) { actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            actors.Clear();
            foreach (var spawn in roster)
            {
                var root = new GameObject("Actor_" + spawn.ActorId); root.transform.SetParent(transform, false);
                var actor = root.AddComponent<GameplayActorProxy>(); actor.Initialize(spawn); actors.Add(spawn.ActorId, actor); ActorCreated?.Invoke(actor);
            }
            Physics.SyncTransforms();
        }
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot> states)
        {
            var alive = new HashSet<uint>();
            foreach (var state in states) { alive.Add(state.ActorId); if (actors.TryGetValue(state.ActorId, out var actor)) actor.Apply(state); }
            foreach (var id in actors.Keys.Where(id => !alive.Contains(id)).ToArray()) { var actor = actors[id]; actor.gameObject.SetActive(false); Destroy(actor.gameObject); actors.Remove(id); }
            Physics.SyncTransforms();
        }
        private static GameplayActorProxy Actor(Collider collider) => collider.GetComponentInParent<GameplayActorProxy>();
        private bool BlocksMotor(Collider collider, uint own)
        {
            if (!collider) return false;
            var actor = Actor(collider); if (actor && actor.ActorId == own) return false;
            bool mosquito = actors.TryGetValue(own, out var self) && self.Role == PlayerRole.Mosquito;
            var anatomy = collider.GetComponent<GameplayBodySurface>();
            if (collider.isTrigger) return mosquito && anatomy;
            if (mosquito && actor && actor.Role == PlayerRole.Human) return false;
            return true;
        }
        private bool Overlap(Collider own, Vector3 position, Quaternion rotation, out Vector3 correction)
        {
            correction = Vector3.zero;
            var bounds = own.bounds;
            foreach (var other in Physics.OverlapBox(position + (bounds.center - own.transform.position), bounds.extents + Vector3.one * .01f, Quaternion.identity, GeometryMask, QueryTriggerInteraction.Collide))
            {
                if (other == own || !BlocksMotor(other, Actor(own).ActorId)) continue;
                if (Physics.ComputePenetration(own, position + correction, rotation, other, other.transform.position, other.transform.rotation, out var direction, out float distance)) correction += direction * (distance + Skin);
            }
            return correction.sqrMagnitude > 0;
        }
        private RaycastHit? CastMotor(uint actorId, Vector3 position, Vector3 delta, float height, float radius, bool human)
        {
            if (delta.sqrMagnitude < 1e-12f) return null;
            RaycastHit[] hits = human
                ? Physics.CapsuleCastAll(position + Vector3.up * radius, position + Vector3.up * (height - radius), radius, delta.normalized, delta.magnitude + Skin, GeometryMask, QueryTriggerInteraction.Collide)
                : Physics.SphereCastAll(position, radius, delta.normalized, delta.magnitude + Skin, GeometryMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits.OrderBy(h => h.distance).ThenBy(h => Actor(h.collider)?.ActorId ?? 0)) if (BlocksMotor(hit.collider, actorId)) return hit;
            return null;
        }
        public MotorResult MoveHuman(in MotorQuery query) => Move(query, true);
        public MotorResult MoveMosquito(in MotorQuery query) => Move(query, false);
        private MotorResult Move(MotorQuery q, bool human)
        {
            var actor = actors[q.ActorId]; var position = q.Position.ToUnity(); var velocity = q.Velocity.ToUnity(); float height = q.Height, crouch = q.CrouchFraction;
            if (actor.MotorCollider is CapsuleCollider capsule)
            {
                float oldHeight = capsule.height;
                if (height > oldHeight)
                {
                    // Test the full standing volume before expanding under a ceiling.
                    bool occupied = Physics.OverlapCapsule(position + Vector3.up * q.Radius, position + Vector3.up * (height - q.Radius), q.Radius - Skin, GeometryMask, QueryTriggerInteraction.Ignore).Any(c => BlocksMotor(c, q.ActorId));
                    if (occupied) { height = oldHeight; crouch = (1.72f - height) / .72f; }
                }
                capsule.height = height; capsule.center = Vector3.up * (height / 2);
            }
            actor.transform.position = position; Physics.SyncTransforms();
            for (int i = 0; i < 4 && Overlap(actor.MotorCollider, position, actor.transform.rotation, out var correction); i++) position += Vector3.ClampMagnitude(correction, .3f);
            var remaining = velocity * q.DeltaSeconds;
            bool grounded = false; var groundNormal = Vector3.up;
            for (int i = 0; i < 5 && remaining.sqrMagnitude > 1e-12f; i++)
            {
                var hit = CastMotor(q.ActorId, position, remaining, height, q.Radius, human);
                if (!hit.HasValue) { position += remaining; break; }
                var h = hit.Value; float travel = Mathf.Max(0, h.distance - Skin); position += remaining.normalized * Mathf.Min(travel, remaining.magnitude);
                remaining -= remaining.normalized * Mathf.Min(travel, remaining.magnitude);
                if (human && q.AllowStep && q.WasGrounded && Mathf.Abs(h.normal.y) < .3f && TryStep(q.ActorId, position, remaining, height, q.Radius, out var stepped)) { position = stepped; remaining = Vector3.zero; grounded = true; break; }
                if (h.normal.y > .55f) { grounded = true; groundNormal = h.normal; }
                remaining = Vector3.ProjectOnPlane(remaining, h.normal); velocity = Vector3.ProjectOnPlane(velocity, h.normal);
            }
            if (velocity.y <= .1f)
            {
                float snap = human && q.WasGrounded ? .23f : .009f;
                var below = CastMotor(q.ActorId, position, Vector3.down * snap, height, q.Radius, human);
                if (below.HasValue && below.Value.normal.y > .55f) { grounded = true; groundNormal = below.Value.normal; position += Vector3.down * Mathf.Max(0, below.Value.distance - Skin); velocity.y = 0; }
            }
            actor.transform.position = position; Physics.SyncTransforms();
            return new MotorResult(position.ToFloat(), velocity.ToFloat(), grounded, groundNormal.ToFloat(), crouch);
        }
        private bool TryStep(uint actor, Vector3 position, Vector3 remaining, float height, float radius, out Vector3 result)
        {
            result = position; var flat = Vector3.ProjectOnPlane(remaining, Vector3.up); if (flat.sqrMagnitude < .000001f) return false;
            var up = Vector3.up * .22f;
            if (CastMotor(actor, position, up, height, radius, true).HasValue || CastMotor(actor, position + up, flat, height, radius, true).HasValue) return false;
            var hit = CastMotor(actor, position + up + flat, Vector3.down * .23f, height, radius, true);
            if (!hit.HasValue || hit.Value.normal.y < .55f) return false;
            result = position + up + flat + Vector3.down * Mathf.Max(0, hit.Value.distance - Skin); return true;
        }
        private RaycastHit? FirstRay(Vector3 origin, Vector3 direction, float reach, uint own, bool includeAnatomy = true, bool skipHumanMotor = false)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction, reach, GeometryMask, QueryTriggerInteraction.Collide).OrderBy(h => h.distance).ThenBy(h => Actor(h.collider)?.ActorId ?? 0))
            {
                var actor = Actor(hit.collider); if (actor && actor.ActorId == own) continue;
                var body = hit.collider.GetComponent<GameplayBodySurface>();
                if (hit.collider.isTrigger && (!body || !includeAnatomy)) continue;
                if (skipHumanMotor && actor && actor.Role == PlayerRole.Human && !body) continue;
                return hit;
            }
            return null;
        }
        public bool TrySurface(in SurfaceQuery query, out SurfaceContact contact)
        {
            contact = default;
            var hit = FirstRay(query.Position.ToUnity(), query.Direction.ToUnity(), query.Reach + .055f, query.ActorId, false);
            if (!hit.HasValue || Actor(hit.Value.collider)) return false;
            var surface = hit.Value.collider.GetComponentInParent<GameplaySurface>();
            if (!surface || !surface.CanPerch || !surfaces.ContainsKey(surface.SurfaceId)) return false;
            var point = hit.Value.point; var normal = hit.Value.normal.normalized;
            var localPoint = surface.transform.InverseTransformPoint(point).ToFloat(); var localNormal = surface.transform.InverseTransformDirection(normal).ToFloat();
            var tangent = Vector3.ProjectOnPlane(query.Direction.ToUnity(), normal);
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.ProjectOnPlane(Vector3.up, normal);
            var attachment = new SurfaceAttachment(surface.SurfaceId, surface.Revision, localPoint, localNormal, surface.transform.InverseTransformDirection(tangent.normalized).ToFloat());
            contact = new SurfaceContact(attachment, point.ToFloat(), normal.ToFloat()); return true;
        }
        public bool ResolveSurface(in SurfaceAttachment attachment, out SurfaceContact contact)
        {
            contact = default;
            if (!surfaces.TryGetValue(attachment.SurfaceId, out var surface) || !surface || !surface.CanPerch) return false;
            var point = surface.transform.TransformPoint(attachment.LocalPoint.ToUnity()); var normal = surface.transform.TransformDirection(attachment.LocalNormal.ToUnity()).normalized;
            contact = new SurfaceContact(new SurfaceAttachment(attachment.SurfaceId, surface.Revision, attachment.LocalPoint, attachment.LocalNormal, attachment.TangentForward), point.ToFloat(), normal.ToFloat()); return true;
        }
        private bool FreeMosquito(uint actor, Vector3 position, uint victim = 0)
        {
            foreach (var collider in Physics.OverlapSphere(position, .054f, GeometryMask, QueryTriggerInteraction.Ignore))
            {
                var other = Actor(collider); if (other && (other.ActorId == actor || other.ActorId == victim)) continue;
                if (BlocksMotor(collider, actor)) return false;
            }
            return true;
        }
        private bool Defendible(GameplayBodySurface body, Vector3 point, int humanCount)
        {
            var victim = body.Actor;
            if (victim.State == null || victim.State.LifeState != LifeState.Active) return false;
            // Eligible continuous subset is bounded by actual shoulder reach and view domain.
            // Back contacts require another active human with a clear reachable strike.
            var candidates = humanCount <= 1 ? new[] { victim } : actors.Values.Where(a => a.Role == PlayerRole.Human && a.State != null && a.State.LifeState == LifeState.Active);
            foreach (var human in candidates)
            {
                float crouch = human.State.CrouchFraction;
                var local = human.transform.InverseTransformPoint(point);
                if (human == victim && local.z < -.02f) continue;
                var shoulderY = 1.39f - .57f * crouch;
                foreach (int side in new[] { -1, 1 })
                {
                    if (human == victim && ((body.PartId == 5 && side == -1) || (body.PartId == 6 && side == 1))) continue;
                    var shoulder = human.transform.TransformPoint(new Vector3(.21f * side, shoulderY, 0));
                    if (Vector3.Distance(shoulder, point) > .72f) continue;
                    var eye = human.transform.position + Vector3.up * (1.53f - .64f * crouch);
                    var direction = (point - eye).normalized;
                    float pitch = Mathf.Asin(direction.y);
                    if (pitch > 75 * Mathf.Deg2Rad) continue;
                    var block = FirstRay(eye, direction, Vector3.Distance(eye, point) - .005f, human.ActorId, false, true);
                    if (!block.HasValue) return true;
                }
            }
            return false;
        }
        public bool TryBiteContact(in BiteQuery query, out BiteContact contact)
        {
            contact = default;
            // Proboscis tip is 95 mm forward from the locomotion centre.
            var origin = query.Position.ToUnity(); var aim = query.AimForward.ToUnity();
            var hit = FirstRay(origin, aim, .095f + query.Reach, query.ActorId, true, true);
            if (!hit.HasValue) return false;
            var body = hit.Value.collider.GetComponent<GameplayBodySurface>();
            if (!body || !Defendible(body, hit.Value.point, query.ActiveHumanCount)) return false;
            var normal = hit.Value.normal.normalized; var center = hit.Value.point + normal * .096f;
            if (Vector3.Dot(aim, -normal) < .65f || Vector3.Distance(center, origin) > .045f || !FreeMosquito(query.ActorId, center, body.Actor.ActorId)) return false;
            var attachment = new BiteAttachment(body.Actor.ActorId, body.SurfaceId, body.transform.InverseTransformPoint(hit.Value.point).ToFloat(), body.transform.InverseTransformDirection(normal).ToFloat(), body.Actor.State.PoseRevision);
            contact = new BiteContact(attachment, center.ToFloat(), normal.ToFloat()); return true;
        }
        public bool ResolveBite(uint mosquitoId, in BiteAttachment attachment, int humanCount, out BiteContact contact)
        {
            contact = default;
            if (!actors.TryGetValue(attachment.VictimId, out var victim) || !victim.BodySurfaces.TryGetValue(attachment.SurfaceId, out var body)) return false;
            var point = body.transform.TransformPoint(attachment.LocalPoint.ToUnity()); var normal = body.transform.TransformDirection(attachment.LocalNormal.ToUnity()).normalized; var center = point + normal * .096f;
            if (!Defendible(body, point, humanCount) || !FreeMosquito(mosquitoId, center, victim.ActorId)) return false;
            var collision = FirstRay(center, -normal, .10f, mosquitoId, true, true);
            if (!collision.HasValue || collision.Value.collider != body.Collider) return false;
            contact = new BiteContact(new BiteAttachment(attachment.VictimId, attachment.SurfaceId, attachment.LocalPoint, attachment.LocalNormal, victim.State.PoseRevision), center.ToFloat(), normal.ToFloat()); return true;
        }
        public bool TryPlanStrike(uint actorId, Float3 aim, string toolId, out StrikePlan plan)
        {
            plan = default; if (!actors.TryGetValue(actorId, out var actor) || actor.Role != PlayerRole.Human || actor.State == null) return false;
            var state = actor.State; var eye = actor.transform.position + Vector3.up * (1.53f - .64f * state.CrouchFraction);
            bool tool = toolId == "swatter"; float reach = tool ? 1.05f : .72f;
            // The ray is manual. No nearest-mosquito query changes this target.
            var hit = FirstRay(eye, aim.ToUnity(), reach + .3f, actorId, true, true);
            var target = hit.HasValue ? hit.Value.point : eye + aim.ToUnity() * .62f;
            int hand = actor.transform.InverseTransformPoint(target).x < 0 ? 1 : -1;
            var shoulder = actor.transform.TransformPoint(new Vector3(.21f * hand, 1.39f - .57f * state.CrouchFraction, 0));
            if (Vector3.Distance(shoulder, target) > reach) target = shoulder + (target - shoulder).normalized * reach;
            var origin = shoulder + actor.transform.forward * .08f;
            plan = new StrikePlan(origin.ToFloat(), target.ToFloat(), hit.HasValue ? hit.Value.normal.ToFloat() : -aim, tool ? .13f : .075f, hand, tool ? "swatter" : "hands"); return true;
        }
        public StrikeHit SweepStrike(in StrikeSweep query)
        {
            var from = query.From.ToUnity(); var to = query.To.ToUnity(); var delta = to - from;
            // Resolve overlap at the start as casts do not report initial overlaps.
            foreach (var c in Physics.OverlapSphere(from, query.Radius, GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(c => Vector3.Distance(c.ClosestPoint(from), from)))
            {
                var actor = Actor(c); if (actor && actor.ActorId == query.ActorId) continue;
                return new StrikeHit(true, actor ? actor.ActorId : 0, c.ClosestPoint(from).ToFloat(), (-delta.normalized).ToFloat());
            }
            foreach (var hit in Physics.SphereCastAll(from, query.Radius, delta.sqrMagnitude > 0 ? delta.normalized : Vector3.forward, delta.magnitude, GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ThenBy(h => Actor(h.collider)?.ActorId ?? 0))
            {
                var actor = Actor(hit.collider); if (actor && actor.ActorId == query.ActorId) continue;
                return new StrikeHit(true, actor ? actor.ActorId : 0, hit.point.ToFloat(), hit.normal.ToFloat());
            }
            return default;
        }
        public bool TryFreeRecoveryPoint(uint actorId, Float3 position, out Float3 point)
        {
            var actor = actors[actorId]; var origin = position.ToUnity();
            foreach (var offset in new[] { Vector3.zero, Vector3.up * .12f, Vector3.right * .2f, Vector3.left * .2f, Vector3.forward * .2f, Vector3.back * .2f })
            {
                var candidate = origin + offset;
                bool free = actor.Role == PlayerRole.Mosquito ? FreeMosquito(actorId, candidate) : !Physics.OverlapCapsule(candidate + Vector3.up * .251f, candidate + Vector3.up * 1.469f, .249f, GeometryMask, QueryTriggerInteraction.Ignore).Any(c => BlocksMotor(c, actorId));
                if (free) { point = candidate.ToFloat(); return true; }
            }
            point = position; return false;
        }
        public bool HasLineOfSight(uint actorId, Float3 from, uint targetActorId, Float3 to)
        {
            var delta = to - from; var hit = FirstRay(from.ToUnity(), delta.Normalized.ToUnity(), delta.Length, actorId);
            return !hit.HasValue || Actor(hit.Value.collider)?.ActorId == targetActorId;
        }
        public bool TryDoorInteraction(in DoorInteractionQuery query, out DoorInteractionCandidate candidate)
        {
            candidate = default; var hit = FirstRay(query.EyeOrigin.ToUnity(), query.AimForward.ToUnity(), query.Reach, query.ActorId);
            if (!hit.HasValue) return false;
            var door = hit.Value.collider.GetComponentInParent<GameplayDoor>(); if (!door || !doors.ContainsKey(door.DoorId)) return false;
            candidate = new DoorInteractionCandidate(door.DoorId, door.Revision, hit.Value.point.ToFloat(), hit.Value.distance); return true;
        }
        public DoorSweepResult SweepDoor(in DoorMotionQuery query)
        {
            if (!doors.TryGetValue(query.DoorId, out var door)) return new DoorSweepResult(query.FromAngleRadians, true);
            if (Mathf.Abs(query.ToAngleRadians - query.FromAngleRadians) < .000001f) return new DoorSweepResult(query.FromAngleRadians, false);
            var leaf = door.Leaf; var original = door.AngleRadians; float safe = query.FromAngleRadians;
            float delta = query.ToAngleRadians - query.FromAngleRadians;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / (.5f * Mathf.Deg2Rad)));
            // Each midpoint OBB is enlarged by maximum arc travel, covering the entire interval.
            // This is conservative for thin obstacles, not a final-angle-only overlap check.
            float radius = Vector3.Distance(door.Hinge.position, leaf.bounds.center) + leaf.bounds.extents.magnitude;
            float padding = radius * Mathf.Abs(delta / steps) * .5f + .0005f;
            for (int i = 0; i < steps; i++)
            {
                float midpoint = query.FromAngleRadians + delta * ((i + .5f) / steps); door.ApplyAngle(midpoint); Physics.SyncTransforms();
                var center = leaf.transform.TransformPoint(leaf.center); var half = Vector3.Scale(leaf.size * .5f, leaf.transform.lossyScale) + Vector3.one * padding;
                foreach (var other in Physics.OverlapBox(center, half, leaf.transform.rotation, GeometryMask, QueryTriggerInteraction.Ignore))
                {
                    if (other == leaf || other.transform.IsChildOf(door.Hinge)) continue;
                    var actor = Actor(other);
                    door.ApplyAngle(original); Physics.SyncTransforms(); return new DoorSweepResult(safe, true, actor ? actor.ActorId : 0);
                }
                safe = query.FromAngleRadians + delta * ((i + 1f) / steps);
            }
            door.ApplyAngle(original); Physics.SyncTransforms(); return new DoorSweepResult(safe, false);
        }
        public void ApplyDoorPose(in DoorPose pose)
        {
            if (!doors.TryGetValue(pose.DoorId, out var door)) throw new InvalidOperationException("Unknown map door.");
            door.Revision = pose.Revision; door.ApplyAngle(pose.AngleRadians);
            if (surfaces.TryGetValue(door.SurfaceId, out var surface)) surface.Revision = pose.Revision;
            Physics.SyncTransforms();
        }
    }
}
