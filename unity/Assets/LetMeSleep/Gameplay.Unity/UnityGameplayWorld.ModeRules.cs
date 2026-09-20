using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Environment;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed partial class UnityGameplayWorld : IGameplayModeWorld, IGameplayTaskSelectionWorld
    {
        private sealed class ObjectiveBinding
        {
            internal ObjectiveDefinition Definition;
            internal GameplayObjectiveCatalog.Entry Authored;
            internal Collider Target;
        }

        private readonly Dictionary<string, ObjectiveBinding> modeObjectives =
            new Dictionary<string, ObjectiveBinding>(StringComparer.Ordinal);
        private GameplayObjectiveCatalog objectiveCatalog;
        private EnvironmentMapDefinition objectiveMap;
        private GameplayBotNavigation modeNavigation;

        public IReadOnlyList<ObjectiveDefinition> GetObjectiveDefinitions()
        {
            BindObjectiveCatalog();
            return modeObjectives.Values.Select(binding => binding.Definition)
                .OrderBy(objective => objective.ObjectiveId, StringComparer.Ordinal).ToArray();
        }

        internal GameplayBotNavigation ConfigureModeMap(TextAsset navigationData, string mapId, bool requireObjectives)
        {
            if (requireObjectives)
            {
                BindObjectiveCatalog();
                if (!string.Equals(objectiveCatalog.MapId, mapId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Objective catalog does not match the active map.");
            }
            modeNavigation = navigationData ? new GameplayBotNavigation(navigationData, mapId, this) : null;
            return modeNavigation;
        }

        internal void ResetModeMap()
        {
            modeNavigation = null;
            modeObjectives.Clear();
            objectiveCatalog = null;
            objectiveMap = null;
        }

        public bool ValidateObjective(in SpawnActor human, ObjectiveDefinition objective)
        {
            if (human.Role != PlayerRole.Human || objective == null || modeNavigation == null ||
                !TryBinding(objective, out var binding) || !TargetWitness(binding) ||
                !ApproachFree(0, objective.ApproachPoint.ToUnity()) ||
                !modeNavigation.KnowsRegion(objective.RouteRegionId) ||
                !modeNavigation.ContainsFootPoint(objective.RouteRegionId, objective.ApproachPoint)) return false;

            var authored = modeObjectives.Values.Select(source => source.Definition).ToArray();
            int spawnChoices = Math.Min(2, authored.Length);
            int onwardChoices = Math.Min(2, Math.Max(0, authored.Length - 1));
            Float3 humanPosition = human.Position;
            bool targetHasSpawn = (objectiveMap.HumanSpawnPoints ?? Array.Empty<Transform>()).Any(spawn =>
                spawn && modeNavigation.RouteWithin(spawn.position.ToFloat(), objective.RouteRegionId,
                    objective.ApproachPoint, objective.RouteBudgetTicks));
            int choicesForHuman = authored.Count(candidate => modeNavigation.RouteWithin(humanPosition,
                candidate.RouteRegionId, candidate.ApproachPoint, candidate.RouteBudgetTicks));
            int choicesAfterTarget = authored.Count(candidate => candidate.ObjectiveId != objective.ObjectiveId &&
                modeNavigation.RouteWithin(objective.ApproachPoint, candidate.RouteRegionId,
                    candidate.ApproachPoint, candidate.RouteBudgetTicks));
            return targetHasSpawn && choicesForHuman >= spawnChoices && choicesAfterTarget >= onwardChoices;
        }

        public bool IsObjectiveAvailable(uint actorId, ObjectiveDefinition objective)
        {
            if (objective == null || modeNavigation == null || !TryBinding(objective, out var binding) ||
                !TargetWitness(binding) || !actors.TryGetValue(actorId, out var actor) || actor.State == null ||
                actor.State.Eliminated || !ApproachFree(actorId, objective.ApproachPoint.ToUnity())) return false;
            return modeNavigation.HasOpenRoute(actor.State.Position, objective.RouteRegionId,
                objective.ApproachPoint);
        }

        public bool CanAssignObjective(uint actorId, ObjectiveDefinition objective)
        {
            if (objective == null || modeNavigation == null || !TryBinding(objective, out var binding) ||
                !TargetWitness(binding) || !actors.TryGetValue(actorId, out var actor) || actor.State == null ||
                actor.State.Eliminated || !ApproachFree(actorId, objective.ApproachPoint.ToUnity())) return false;
            return modeNavigation.OpenRouteWithin(actor.State.Position, objective.RouteRegionId,
                objective.ApproachPoint, objective.RouteBudgetTicks);
        }

        public bool CanWorkObjective(uint actorId, ObjectiveDefinition objective, Float3 position, Float3 aim)
        {
            if (objective == null || !position.IsFinite || !aim.IsFinite || aim.LengthSquared < .9f ||
                !TryBinding(objective, out var binding) || !TargetWitness(binding) ||
                !actors.TryGetValue(actorId, out var actor) || actor.State == null || actor.State.Eliminated) return false;
            Vector3 eye = position.ToUnity() + Vector3.up * (1.53f - .64f * actor.State.CrouchFraction);
            Vector3 direction = aim.ToUnity().normalized;
            Vector3 toObjective = objective.Position.ToUnity() - eye;
            if (toObjective.sqrMagnitude < .0001f || Vector3.Dot(direction, toObjective.normalized) < .94f) return false;
            var hit = FirstRay(eye, direction, Mathf.Min(4.6f, toObjective.magnitude + .3f), actorId, false);
            return hit.HasValue && hit.Value.collider == binding.Target &&
                Vector3.Distance(hit.Value.point, objective.Position.ToUnity()) <= .35f;
        }

        public bool TryMosquitoRespawn(uint actorId, out Float3 position)
        {
            position = default;
            if (recoveryVolume == null || recoverySettings == null || !boundsMemory.TryGetValue(actorId, out var memory) ||
                memory.Role != PlayerRole.Mosquito) return false;
            Vector3[] authored = recoverySettings.MosquitoSpawnPoints;
            Vector3[] candidates = authored.Length > 0
                ? authored.Select(point => recoveryVolume.transform.TransformPoint(point)).ToArray()
                : memory.Spawns;
            foreach (var candidate in candidates)
            {
                var query = new BoundsRecoveryQuery(actorId, 0, PlayerRole.Mosquito, candidate.ToFloat(),
                    .114f, .057f, false, LifeState.Recovering);
                if (!TrySafe(query, candidate, out var safe, out _)) continue;
                position = safe.ToFloat();
                return true;
            }
            return false;
        }

        internal Float3 TaskDirection(ActorSnapshot actor, ObjectiveDefinition objective) =>
            modeNavigation == null ? Float3.Zero : modeNavigation.DirectionTo(actor, objective);

        private void BindObjectiveCatalog()
        {
            if (!MapRoot || !MapRoot.gameObject.activeInHierarchy)
                throw new InvalidOperationException("An active map is required for objectives.");
            var map = MapRoot.GetComponent<EnvironmentMapDefinition>();
            var catalog = MapRoot.GetComponent<GameplayObjectiveCatalog>();
            if (!map || !catalog) throw new InvalidOperationException("The active map has no authored objective catalog.");
            catalog.ValidateAuthoring(map.MapId);
            if (objectiveCatalog == catalog && objectiveMap == map && modeObjectives.Count == catalog.Entries.Count) return;
            objectiveCatalog = catalog;
            objectiveMap = map;
            modeObjectives.Clear();
            foreach (var authored in catalog.Entries)
            {
                Transform target = ResolvePath(MapRoot, authored.TargetPath);
                var colliders = target.GetComponents<Collider>().Where(c => c && !c.isTrigger).ToArray();
                if (colliders.Length != 1) throw new InvalidOperationException("Objective target must resolve exactly one solid collider: " + authored.ObjectiveId);
                var definition = new ObjectiveDefinition(authored.ObjectiveId, (ObjectiveKind)authored.Kind,
                    authored.DisplayKey, authored.ActionKey, MapRoot.TransformPoint(authored.LocalPosition).ToFloat(),
                    MapRoot.TransformPoint(authored.LocalApproachPoint).ToFloat(), authored.UseRadius,
                    authored.WorkTicks, authored.RouteRegionId, authored.RouteBudgetTicks);
                modeObjectives.Add(definition.ObjectiveId, new ObjectiveBinding
                { Definition = definition, Authored = authored, Target = colliders[0] });
            }
        }

        private bool TryBinding(ObjectiveDefinition objective, out ObjectiveBinding binding)
        {
            binding = null;
            try { BindObjectiveCatalog(); }
            catch (InvalidOperationException) { return false; }
            return modeObjectives.TryGetValue(objective.ObjectiveId, out binding) && Same(binding.Definition, objective);
        }

        private bool TargetWitness(ObjectiveBinding binding)
        {
            if (binding.Target == null || !binding.Target.enabled || !binding.Target.gameObject.activeInHierarchy ||
                !binding.Target.transform.IsChildOf(MapRoot)) return false;
            Vector3 point = binding.Definition.Position.ToUnity();
            foreach (var direction in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                if (binding.Target.Raycast(new Ray(point + direction * .06f, -direction), out var hit, .12f) &&
                    Vector3.Distance(hit.point, point) <= .012f) return true;
            return false;
        }

        private bool ApproachFree(uint actorId, Vector3 point)
        {
            var support = Physics.RaycastAll(point + Vector3.up * .25f, Vector3.down, .55f,
                    GeometryMask, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance).FirstOrDefault(hit => IsWorldCollider(hit.collider) && !Actor(hit.collider));
            if (!support.collider || support.normal.y < .55f || Mathf.Abs(support.point.y - point.y) > .08f) return false;
            return !Physics.OverlapCapsule(point + Vector3.up * .251f, point + Vector3.up * 1.469f, .249f,
                    GeometryMask, QueryTriggerInteraction.Collide)
                .Any(collider => BlocksMotor(collider, actorId));
        }

        private static Transform ResolvePath(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform next = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name != segment) continue;
                    if (next) throw new InvalidOperationException("Ambiguous objective target path: " + path);
                    next = child;
                }
                if (!next) throw new InvalidOperationException("Missing objective target path: " + path);
                current = next;
            }
            return current;
        }

        private static bool Same(ObjectiveDefinition expected, ObjectiveDefinition actual) =>
            expected.Kind == actual.Kind && expected.DisplayKey == actual.DisplayKey && expected.ActionKey == actual.ActionKey &&
            expected.Position.Equals(actual.Position) && expected.ApproachPoint.Equals(actual.ApproachPoint) &&
            expected.UseRadius.Equals(actual.UseRadius) && expected.WorkTicks == actual.WorkTicks &&
            expected.RouteRegionId == actual.RouteRegionId && expected.RouteBudgetTicks == actual.RouteBudgetTicks;
    }
}
