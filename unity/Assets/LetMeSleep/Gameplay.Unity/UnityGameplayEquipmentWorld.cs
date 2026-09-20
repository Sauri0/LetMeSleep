using System.Linq;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld : IGameplayEquipmentWorld
    {
        private static string EquipmentColliderKey(Collider collider)
        {
            string key = "";
            for (var t = collider.transform; t; t = t.parent) key = t.GetSiblingIndex().ToString("D6") + "/" + key;
            return key + collider.GetType().Name;
        }
        private bool BlocksEquipment(Collider collider, uint actorId, uint pickupId)
        {
            if (!IsWorldCollider(collider)) return false;
            var actor = Actor(collider);
            if (actor && actor.ActorId == actorId) return false;
            var pickup = collider.GetComponentInParent<GameplayToolPickup>();
            if (pickup) return pickup.PickupId != pickupId && pickup.OwnerActorId == 0;
            return !collider.isTrigger || collider.GetComponent<GameplayBodySurface>();
        }

        private bool EquipmentOverlap(Vector3 root, Quaternion rotation, Vector3 center, Vector3 half,
            uint actorId, uint pickupId, out Collider obstruction)
        {
            obstruction = Physics.OverlapBox(root + rotation * center, half, rotation, GeometryMask,
                QueryTriggerInteraction.Collide).Where(c => BlocksEquipment(c, actorId, pickupId))
                .OrderBy(c => Actor(c)?.ActorId ?? 0).ThenBy(EquipmentColliderKey).FirstOrDefault();
            return obstruction;
        }

        private RaycastHit? EquipmentCast(Vector3 root, Quaternion rotation, Vector3 center, Vector3 half,
            Vector3 delta, uint actorId, uint pickupId)
        {
            if (delta.sqrMagnitude < .00000001f) return null;
            foreach (var hit in Physics.BoxCastAll(root + rotation * center, half, delta.normalized,
                rotation, delta.magnitude, GeometryMask, QueryTriggerInteraction.Collide).OrderBy(h => h.distance)
                .ThenBy(h => Actor(h.collider)?.ActorId ?? 0).ThenBy(h => EquipmentColliderKey(h.collider)))
                if (BlocksEquipment(hit.collider, actorId, pickupId)) return hit;
            return null;
        }

        public bool TryDropTool(uint actorId, uint pickupId, out Float3 position, out Rotation rotation)
        {
            position = default; rotation = Rotation.Identity;
            if (!actors.TryGetValue(actorId, out var actor) || !toolPickups.TryGetValue(pickupId, out var pickup)) return false;
            pickup.GetPlacementVolume(out var center, out var half);
            var q = Quaternion.Euler(0, actor.transform.eulerAngles.y, 0);
            var forward = q * Vector3.forward;
            var origin = actor.transform.position + Vector3.up * 1.1f;
            // Try three local deposits, in nearest-first order. Every ray stops at
            // its first obstruction; another support below it is never considered.
            foreach (float distance in new[] { HumanEquipmentProfile.DepositMinimumDistance, .8f, HumanEquipmentProfile.DepositMaximumDistance })
            {
                var top = origin + forward * distance;
                var pathRoot = origin - q * center;
                if (EquipmentOverlap(pathRoot, q, center, half, actorId, pickupId, out _) ||
                    EquipmentCast(pathRoot, q, center, half, forward * distance, actorId, pickupId).HasValue) continue;
                RaycastHit? support = null;
                foreach (var hit in Physics.RaycastAll(top, Vector3.down, 2.5f, GeometryMask,
                    QueryTriggerInteraction.Collide).OrderBy(h => h.distance)
                    .ThenBy(h => Actor(h.collider)?.ActorId ?? 0).ThenBy(h => EquipmentColliderKey(h.collider)))
                    if (BlocksEquipment(hit.collider, actorId, pickupId)) { support = hit; break; }
                if (!support.HasValue || support.Value.normal.y < .65f || Actor(support.Value.collider) ||
                    support.Value.collider.GetComponentInParent<GameplayToolPickup>()) continue;
                var root = support.Value.point - q * center + Vector3.up * (half.y + .003f);
                if (recoveryVolume && recoverySettings != null &&
                    (InFallZone(LetMeSleep.Core.PlayerRole.Human, root + q * center) ||
                     !recoverySettings.SafetyBounds.Contains(recoveryVolume.transform.InverseTransformPoint(root + q * center)))) continue;
                if (EquipmentOverlap(root, q, center, half, actorId, pickupId, out _)) continue;
                // A thin shelf can catch the center ray while the item's edges
                // cross a wall on the way down. Sweep the actual item volume.
                var from = top - q * center;
                var descent = root - from;
                var block = EquipmentCast(from, q, center, half, descent, actorId, pickupId);
                if (block.HasValue && block.Value.distance + .002f < descent.magnitude) continue;
                position = root.ToFloat(); rotation = q.ToRotation(); return true;
            }
            return false;
        }

        public bool TryPrepareThrow(uint actorId, uint pickupId, Float3 aim, float power,
            out Float3 position, out Rotation rotation, out Float3 velocity)
        {
            position = velocity = default; rotation = Rotation.Identity;
            if (!aim.IsFinite || Mathf.Abs(aim.LengthSquared - 1) > .02f || !MathEx.Finite(power) ||
                power < .35f || power > 1 || !actors.TryGetValue(actorId, out var actor) ||
                !toolPickups.TryGetValue(pickupId, out var pickup) || pickup.ToolId != GameplayTools.Slipper) return false;
            pickup.GetPlacementVolume(out var center, out var half);
            var q = Quaternion.Euler(0, actor.transform.eulerAngles.y, 0);
            var eye = actor.transform.position + Vector3.up * (1.53f - .64f * (actor.State?.CrouchFraction ?? 0));
            var start = eye - q * center;
            var delta = aim.ToUnity() * .5f;
            if (EquipmentOverlap(start, q, center, half, actorId, pickupId, out _) ||
                EquipmentCast(start, q, center, half, delta, actorId, pickupId).HasValue ||
                EquipmentOverlap(start + delta, q, center, half, actorId, pickupId, out _)) return false;
            position = (start + delta).ToFloat(); rotation = q.ToRotation();
            velocity = aim * Mathf.Lerp(HumanEquipmentProfile.LaunchMinimumSpeed, HumanEquipmentProfile.LaunchMaximumSpeed,
                (power - .35f) / .65f) + Float3.Up * HumanEquipmentProfile.LaunchLiftSpeed;
            return true;
        }

        public bool SweepProjectile(in ToolProjectileQuery query, out ToolProjectileHit hit)
        {
            hit = default;
            if (!toolPickups.TryGetValue(query.PickupId, out var pickup)) return false;
            pickup.GetPlacementVolume(out var center, out var half);
            var root = query.Position.ToUnity();
            var q = query.Rotation.ToUnity();
            var delta = query.Displacement.ToUnity();
            if (EquipmentOverlap(root, q, center, half, query.ThrowerActorId, query.PickupId, out var overlap))
            {
                var normal = root + q * center - overlap.ClosestPoint(root + q * center);
                if (normal.sqrMagnitude < .000001f) normal = delta.sqrMagnitude > .000001f ? -delta : Vector3.up;
                hit = new ToolProjectileHit(Actor(overlap)?.ActorId ?? 0, query.Position, normal.normalized.ToFloat(), 0, true);
                return true;
            }
            var cast = EquipmentCast(root, q, center, half, delta, query.ThrowerActorId, query.PickupId);
            if (!cast.HasValue) return false;
            var contact = cast.Value;
            var safeDistance = Mathf.Max(0, contact.distance - .002f);
            hit = new ToolProjectileHit(Actor(contact.collider)?.ActorId ?? 0,
                (root + delta.normalized * safeDistance).ToFloat(), contact.normal.ToFloat(),
                delta.magnitude > 0 ? Mathf.Clamp01(safeDistance / delta.magnitude) : 0);
            return true;
        }
    }
}
