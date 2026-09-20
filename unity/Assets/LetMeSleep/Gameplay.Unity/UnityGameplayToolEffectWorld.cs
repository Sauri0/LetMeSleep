using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld : IGameplayToolEffectWorld
    {
        public bool TryToolEffectOrigin(uint actorId, uint pickupId, Float3 aim, out Float3 origin, out Float3 forward)
        {
            origin = forward = default;
            if (!aim.IsFinite || Mathf.Abs(aim.LengthSquared - 1) > .02f ||
                !actors.TryGetValue(actorId, out var actor) || actor.Role != PlayerRole.Human ||
                !toolPickups.TryGetValue(pickupId, out var pickup) || pickup.OwnerActorId != actorId ||
                (pickup.ToolId != GameplayTools.ElectricRacket && pickup.ToolId != GameplayTools.Aerosol)) return false;
            var eye = actor.transform.position + Vector3.up * (1.53f - .64f * (actor.State?.CrouchFraction ?? 0));
            var direction = aim.ToUnity().normalized;
            var muzzle = eye + direction * .35f;
            // A clear end point alone would allow emission through a thin wall.
            foreach (var hit in Physics.SphereCastAll(eye, .025f, direction, .35f, GeometryMask, QueryTriggerInteraction.Collide)
                .OrderBy(h => h.distance).ThenBy(h => Actor(h.collider)?.ActorId ?? 0))
            {
                if (!BlocksEquipment(hit.collider, actorId, pickupId)) continue;
                if (!Actor(hit.collider)) return false;
                // A mosquito touching the muzzle is a valid target. Keep the
                // emission in front of that actor instead of jumping past it.
                muzzle = eye + direction * Mathf.Max(0, hit.distance - .026f); break;
            }
            foreach (var point in new[] { eye, muzzle })
                foreach (var collision in Physics.OverlapSphere(point, .025f, GeometryMask, QueryTriggerInteraction.Collide))
                {
                    if (!BlocksEquipment(collision, actorId, pickupId)) continue;
                    if (!Actor(collision)) return false;
                    muzzle = eye;
                }
            origin = muzzle.ToFloat(); forward = direction.ToFloat(); return true;
        }

        public IReadOnlyList<ToolEffectHit> QueryToolEffect(in ToolEffectQuery query)
        {
            if (!query.Origin.IsFinite || !query.Forward.IsFinite || Mathf.Abs(query.Forward.LengthSquared - 1) > .02f ||
                !MathEx.Finite(query.Range) || query.Range <= 0 || query.Range > 2 ||
                !MathEx.Finite(query.HalfAngleDegrees) || query.HalfAngleDegrees <= 0 || query.HalfAngleDegrees > 35 ||
                (query.Kind != ToolEffectKind.RacketPulse && query.Kind != ToolEffectKind.AerosolCloud)) return Array.Empty<ToolEffectHit>();
            var hits = new List<ToolEffectHit>();
            var source = query.Origin.ToUnity(); var direction = query.Forward.ToUnity();
            uint sourceActor = query.SourceActorId, pickupId = query.PickupId;
            // A door may move over an already emitted cloud. Raycasts from inside
            // that solid do not report its exit face, so test the origin as well.
            var initialBlockers = Physics.OverlapSphere(source, .001f, GeometryMask, QueryTriggerInteraction.Collide)
                .Where(c => BlocksEquipment(c, sourceActor, pickupId)).ToArray();
            float cosine = Mathf.Cos(query.HalfAngleDegrees * Mathf.Deg2Rad);
            foreach (var actor in actors.Values.OrderBy(a => a.ActorId))
            {
                if (actor.ActorId == query.SourceActorId || !actor.MotorCollider || !actor.MotorCollider.enabled) continue;
                if (initialBlockers.Any(c => Actor(c)?.ActorId != actor.ActorId)) continue;
                var point = actor.MotorCollider.ClosestPoint(source);
                var delta = point - source; float distance = delta.magnitude;
                if (distance > query.Range || distance > .0001f && Vector3.Dot(delta / distance, direction) < cosine) continue;
                if (distance > .0001f)
                {
                    var blocker = FirstRay(source, delta / distance, distance + .002f, query.SourceActorId);
                    if (blocker.HasValue && Actor(blocker.Value.collider)?.ActorId != actor.ActorId) continue;
                }
                var normal = distance > .0001f ? -delta / distance : -direction;
                hits.Add(new ToolEffectHit(actor.ActorId, point.ToFloat(), normal.ToFloat()));
            }
            return hits;
        }
    }
}
