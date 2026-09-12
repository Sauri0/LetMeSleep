using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld
    {
        private readonly Dictionary<uint, GameplayToolPickup> toolPickups = new Dictionary<uint, GameplayToolPickup>();
        private void RegisterTools()
        {
            toolPickups.Clear();
            foreach (var pickup in MapComponents<GameplayToolPickup>())
            {
                pickup.Initialize();
                if (pickup.PickupId == 0 || pickup.ToolId != GameplayTools.Flyswatter || toolPickups.ContainsKey(pickup.PickupId) || !pickup.InteractionCollider || !pickup.InteractionCollider.transform.IsChildOf(MapRoot)) throw new InvalidOperationException("Invalid, duplicate or foreign map pickup.");
                toolPickups.Add(pickup.PickupId, pickup);
            }
            if (toolPickups.Count > 32) throw new InvalidOperationException("Too many map pickups.");
        }
        public IReadOnlyList<ToolPickupDefinition> GetToolDefinitions() { RegisterTools(); return toolPickups.Values.OrderBy(p => p.PickupId).Select(p => p.Definition).ToArray(); }
        public void BeginTools(IReadOnlyList<ToolPickupDefinition> definitions)
        {
            RegisterTools();
            if (definitions == null || definitions.Count != toolPickups.Count) throw new InvalidOperationException("Round must include all map pickups.");
            var seen = new HashSet<uint>();
            foreach (var definition in definitions)
            {
                if (!seen.Add(definition.PickupId) || !toolPickups.TryGetValue(definition.PickupId, out var pickup) || !ToolDefinitionValidation.Matches(pickup.Definition, definition)) throw new InvalidOperationException("Pickup definition does not match authored map pose (1 mm / 0.1 degree).");
            }
            // Validate the entire batch before applying any remote configuration.
            foreach (var definition in definitions)
                toolPickups[definition.PickupId].Apply(new ToolPickupSnapshot(definition.PickupId, definition.ToolId, definition.Position, definition.Rotation));
            Physics.SyncTransforms();
        }
        public bool TryToolInteraction(in ToolInteractionQuery query, out ToolInteractionCandidate candidate)
        {
            candidate = default;
            foreach (var hit in Physics.RaycastAll(query.EyeOrigin.ToUnity(), query.AimForward.ToUnity(), query.Reach, GeometryMask, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
            {
                if (!IsWorldCollider(hit.collider)) continue;
                var actor = Actor(hit.collider); if (actor && actor.ActorId == query.ActorId) continue;
                var pickup = hit.collider.GetComponentInParent<GameplayToolPickup>();
                if (hit.collider.isTrigger && !pickup) continue;
                if (pickup && pickup.OwnerActorId == 0 && toolPickups.ContainsKey(pickup.PickupId))
                { candidate = new ToolInteractionCandidate(pickup.PickupId, pickup.Revision, hit.distance); return true; }
                return false; // First physical obstruction wins; never pick through doors or furniture.
            }
            return false;
        }
        public bool TryDropTool(uint actorId, out Float3 position, out Rotation rotation)
        {
            position = default; rotation = Rotation.Identity;
            if (!actors.TryGetValue(actorId, out var actor)) return false;
            var origin = actor.transform.position + Vector3.up * 1.1f; var forward = actor.transform.forward;
            var ahead = FirstRay(origin, forward, .65f, actorId, false);
            float reach = ahead.HasValue ? Mathf.Max(0, ahead.Value.distance - .25f) : .65f;
            if (reach < .3f) return false;
            var target = origin + forward * reach;
            var floor = FirstRay(target, Vector3.down, 2.5f, actorId, false);
            if (!floor.HasValue || floor.Value.normal.y < .65f || Actor(floor.Value.collider)) return false;
            var p = floor.Value.point + Vector3.up * .014f; var q = Quaternion.Euler(0, actor.transform.eulerAngles.y, 0);
            var center = p + q * new Vector3(0, .006f, .18f);
            foreach (var collision in Physics.OverlapBox(center, new Vector3(.09f, .012f, .20f), q, GeometryMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsWorldCollider(collision)) continue;
                var own = Actor(collision); if (own && own.ActorId == actorId) continue;
                return false;
            }
            position = p.ToFloat(); rotation = q.ToRotation(); return true;
        }
        public void ApplyToolState(in ToolPickupSnapshot state)
        {
            if (!toolPickups.TryGetValue(state.PickupId, out var pickup)) throw new InvalidOperationException("Unknown map pickup.");
            pickup.Apply(state); Physics.SyncTransforms();
        }
    }
}
