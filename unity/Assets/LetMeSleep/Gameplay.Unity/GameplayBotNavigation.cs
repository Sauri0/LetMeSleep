using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    internal sealed class GameplayBotNavigation
    {
        // DTO for the authored map schema. No dependency from Gameplay to Content.
#pragma warning disable 0649 // Populated by JsonUtility from the authored SpatialData.
        [Serializable] private sealed class Plan { public int schema_version; public string map_id; public Zone[] zones; public Portal[] portals; public Stair stair; }
        [Serializable] private sealed class Zone { public string id; public float[] min, max; }
        [Serializable] private sealed class Portal { public string id, from, to; public float[] center, normal; public float width, height; public bool door; }
        [Serializable] private sealed class Flight { public float[] clear_x; public float start_y, end_y, start_z, end_z; }
        [Serializable] private sealed class Stair { public string id, from, to; public Flight lower_flight, upper_flight; public Zone mid_landing; }
#pragma warning restore 0649
        private readonly UnityGameplayWorld world;
        private readonly BotRegion[] regions;
        private readonly BotPassage[] passages;
        private readonly Dictionary<string, GameplayDoor> doors = new Dictionary<string, GameplayDoor>();
        private readonly Dictionary<uint, BotPatrol> patrols = new Dictionary<uint, BotPatrol>();
#if UNITY_EDITOR
        private bool captureDirectionDiagnostic = false;
        private string lastDirectionDiagnostic;
#endif
        public GameplayBotNavigation(TextAsset data, string mapId, UnityGameplayWorld world)
        {
            this.world = world;
            var plan = JsonUtility.FromJson<Plan>(data.text);
            if (plan == null || plan.schema_version != 1 || plan.map_id != mapId || plan.zones == null || plan.portals == null)
                throw new ArgumentException("Bot navigation data must match the active map and schema 1.");
            regions = plan.zones.Select(z => new BotRegion(z.id, Point(z.min), Point(z.max))).ToArray();
            if (regions.Length == 0 || regions.Select(r => r.Id).Distinct().Count() != regions.Length)
                throw new ArgumentException("Bot navigation regions must have unique IDs.");
            MapId = mapId;
            var links = new List<BotPassage>();
            var known = new HashSet<string>(regions.Select(r => r.Id));
            foreach (var portal in plan.portals)
            {
                // Exterior/service voids without authored walkable regions are not routes.
                if (!known.Contains(portal.from) || !known.Contains(portal.to)) continue;
                if (portal.width < .3f || portal.height < .5f) continue;
                var center = Point(portal.center); var normal = Point(portal.normal).Normalized;
                if (normal.LengthSquared < .9f) throw new ArgumentException("Invalid portal normal.");
                links.Add(new BotPassage(portal.id, portal.from, portal.to,
                    new[] { center - normal * .55f, center, center + normal * .55f }));
                if (portal.door)
                {
                    // Missing/ambiguous authored door binding is closed, never an invisible shortcut.
                    var matches = world.Doors.Values.Where(d => d.name == portal.id).ToArray();
                    doors.Add(portal.id, matches.Length == 1 ? matches[0] : null);
                }
            }
            var stair = plan.stair;
            if (stair?.lower_flight != null && stair.upper_flight != null && stair.mid_landing != null && known.Contains(stair.from) && known.Contains(stair.to))
            {
                var lower = stair.lower_flight; var upper = stair.upper_flight;
                float lx = Mid(lower.clear_x), ux = Mid(upper.clear_x);
                float turnZ = (Point(stair.mid_landing.min).Z + Point(stair.mid_landing.max).Z) * .5f;
                links.Add(new BotPassage(stair.id, stair.from, stair.to, new[] {
                    new Float3(lx, lower.start_y + .85f, lower.start_z - .45f),
                    new Float3(lx, lower.end_y + .85f, lower.end_z),
                    new Float3(lx, lower.end_y + .85f, turnZ),
                    new Float3(ux, upper.start_y + .85f, turnZ),
                    new Float3(ux, upper.start_y + .85f, upper.start_z),
                    new Float3(ux, upper.end_y + .85f, upper.end_z),
                    new Float3(ux, upper.end_y + .85f, upper.end_z - .45f) },
                    new[] { StairFlightRegion(stair.id + ":lower", lower), StairFlightRegion(stair.id + ":upper", upper),
                        new BotRegion(stair.id + ":landing", Point(stair.mid_landing.min) + new Float3(0, .85f - .35f, -.24f),
                            Point(stair.mid_landing.max) + new Float3(0, .85f + .35f, .24f)) }));
            }
            passages = links.ToArray();
        }
        public string MapId { get; }
        public BotNavigationContext ContextFor(uint actorId) => new BotNavigationContext(
            () => patrols.TryGetValue(actorId, out var patrol) ? patrol.CurrentProgress : null,
            (passageId, untilTick) =>
            {
                if (patrols.TryGetValue(actorId, out var patrol)) patrol.InvalidatePassage(passageId, untilTick);
            });
        public Float3 Explore(ActorSnapshot actor, uint tick)
        {
            if (actor.Role != PlayerRole.Mosquito) return Float3.Zero;
            if (!patrols.TryGetValue(actor.ActorId, out var patrol))
                patrols.Add(actor.ActorId, patrol = new BotPatrol(regions, passages, actor.ActorId));
            var local = world.MapRoot.InverseTransformPoint(actor.Position.ToUnity()).ToFloat();
            var direction = patrol.Direction(local, tick, IsOpen);
            return world.MapRoot.TransformVector(direction.ToUnity()).ToFloat();
        }
        public Float3 DirectionTo(ActorSnapshot actor, ObjectiveDefinition objective) => DirectionTo(actor, objective, 0);
        public Float3 DirectionTo(ActorSnapshot actor, ObjectiveDefinition objective, uint tick)
        {
            if (actor.Role != PlayerRole.Human || objective == null) return Float3.Zero;
            var local = world.MapRoot.InverseTransformPoint(actor.Position.ToUnity()).ToFloat();
            var approach = world.MapRoot.InverseTransformPoint(objective.ApproachPoint.ToUnity()).ToFloat();
            bool sourceKnown = TryHumanRegion(local, out var sourceRegion, out _);
            var navigationPoint = local + Float3.Up;
            if (!TryTargetPoint(objective.RouteRegionId, approach, out var navigationApproach, out _)) return Float3.Zero;
            if (!patrols.TryGetValue(actor.ActorId, out var patrol))
                patrols.Add(actor.ActorId, patrol = new BotPatrol(regions, passages, actor.ActorId));
            var direction = patrol.DirectionTo(navigationPoint, sourceKnown ? sourceRegion.Id : null,
                objective.RouteRegionId, navigationApproach, tick, IsOpen);
            var worldDirection = world.MapRoot.TransformVector(direction.ToUnity()).ToFloat();
#if UNITY_EDITOR
            if (captureDirectionDiagnostic)
                lastDirectionDiagnostic = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "sourceRegion={0} navigationPoint={1:R},{2:R},{3:R} rawLocal={4:R},{5:R},{6:R} " +
                    "taskTravel={7:R},{8:R},{9:R} {10}", sourceRegion.Id,
                    navigationPoint.X, navigationPoint.Y, navigationPoint.Z,
                    direction.X, direction.Y, direction.Z, worldDirection.X, worldDirection.Y,
                    worldDirection.Z, patrol.DirectedRouteDiagnostic);
#endif
            return worldDirection;
        }
        public bool HasOpenRoute(Float3 worldPosition, string targetRegion, Float3 worldApproach)
        {
            var local = world.MapRoot.InverseTransformPoint(worldPosition.ToUnity()).ToFloat();
            var approach = world.MapRoot.InverseTransformPoint(worldApproach.ToUnity()).ToFloat();
            if (!TryHumanRegion(local, out var from, out _) ||
                !TryTargetPoint(targetRegion, approach, out _, out _)) return false;
            return Reachable(from.Id, targetRegion, IsOpen);
        }
        public bool RouteWithin(Float3 worldPosition, string targetRegion, Float3 worldApproach, uint budgetTicks)
            => RouteWithin(worldPosition, targetRegion, worldApproach, budgetTicks, null);
        internal bool TryToolDetour(ActorSnapshot actor, Float3 pickupPosition,
            ObjectiveDefinition ownObjective, out float detourMeters)
        {
            detourMeters = 0;
            if (actor == null || actor.Role != PlayerRole.Human || !pickupPosition.IsFinite) return false;
            var actorLocal = world.MapRoot.InverseTransformPoint(actor.Position.ToUnity()).ToFloat();
            var pickupLocal = world.MapRoot.InverseTransformPoint(pickupPosition.ToUnity()).ToFloat();
            if (!TryHumanRegion(actorLocal, out var actorRegion, out var actorPoint) ||
                !TryHumanRegion(pickupLocal, out var pickupRegion, out var pickupPoint)) return false;
            float outbound = ShortestAuthoredDistance(actorRegion.Id, actorPoint, pickupRegion.Id,
                pickupPoint, IsOpen);
            if (!MathEx.Finite(outbound)) return false;
            if (ownObjective == null) { detourMeters = outbound; return true; }

            var objectiveLocal = world.MapRoot.InverseTransformPoint(ownObjective.ApproachPoint.ToUnity()).ToFloat();
            if (!TryTargetPoint(ownObjective.RouteRegionId, objectiveLocal, out var objectivePoint,
                    out var objectiveRegion)) return false;
            float direct = ShortestAuthoredDistance(actorRegion.Id, actorPoint, objectiveRegion.Id,
                objectivePoint, IsOpen);
            float onward = ShortestAuthoredDistance(pickupRegion.Id, pickupPoint, objectiveRegion.Id,
                objectivePoint, IsOpen);
            if (!MathEx.Finite(direct) || !MathEx.Finite(onward)) return false;
            detourMeters = Mathf.Max(0, outbound + onward - direct);
            return true;
        }
        internal bool OpenRouteWithin(Float3 worldPosition, string targetRegion, Float3 worldApproach,
            uint budgetTicks) => RouteWithin(worldPosition, targetRegion, worldApproach, budgetTicks, IsOpen);
        private bool RouteWithin(Float3 worldPosition, string targetRegion, Float3 worldApproach,
            uint budgetTicks, Func<string, bool> passageAllowed)
        {
            var local = world.MapRoot.InverseTransformPoint(worldPosition.ToUnity()).ToFloat();
            var approach = world.MapRoot.InverseTransformPoint(worldApproach.ToUnity()).ToFloat();
            if (!TryHumanRegion(local, out var from, out var navigationPoint) ||
                !TryTargetPoint(targetRegion, approach, out var navigationApproach, out _)) return false;
            // Measure between authored passage endpoints. Region centers can be far from a
            // perfectly valid doorway and would reject large but traversable rooms.
            float distance = ShortestAuthoredDistance(from.Id, navigationPoint, targetRegion,
                navigationApproach, passageAllowed);
            return MathEx.Finite(distance) && Mathf.CeilToInt(distance / (3.1f / 30f)) <= budgetTicks;
        }
        internal string DiagnoseRoute(Float3 worldPosition, string targetRegion, Float3 worldApproach, uint budgetTicks)
        {
            var local = world.MapRoot.InverseTransformPoint(worldPosition.ToUnity()).ToFloat();
            var approach = world.MapRoot.InverseTransformPoint(worldApproach.ToUnity()).ToFloat();
            bool sourceWithin = TryHumanRegion(local, out var from, out var navigationPoint);
            bool targetWithin = TryTargetPoint(targetRegion, approach, out var navigationApproach, out var target);
            float sourceOffset = from.Id == null ? float.PositiveInfinity : Distance(local + Float3.Up, navigationPoint);
            float targetOffset = target.Id == null ? float.PositiveInfinity : Distance(approach + Float3.Up, navigationApproach);
            bool authoredConnected = from.Id != null && target.Id != null && Reachable(from.Id, target.Id, AlwaysOpen);
            bool openConnected = from.Id != null && target.Id != null && Reachable(from.Id, target.Id, IsOpen);
            float distance = sourceWithin && targetWithin && authoredConnected
                ? ShortestAuthoredDistance(from.Id, navigationPoint, target.Id, navigationApproach, null)
                : float.PositiveInfinity;
            int requiredTicks = MathEx.Finite(distance) ? Mathf.CeilToInt(distance / (3.1f / 30f)) : -1;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "sourceRegion={0} sourceOffset={1:R} sourceWithin={2} targetRegion={3} targetOffset={4:R} " +
                "targetWithin={5} authoredConnected={6} openConnected={7} distance={8:R} requiredTicks={9} budget={10} within={11}",
                from.Id ?? "none", sourceOffset, sourceWithin ? 1 : 0, target.Id ?? "none", targetOffset,
                targetWithin ? 1 : 0, authoredConnected ? 1 : 0, openConnected ? 1 : 0, distance,
                requiredTicks, budgetTicks,
                sourceWithin && targetWithin && authoredConnected && requiredTicks <= budgetTicks ? 1 : 0);
        }
        public bool KnowsRegion(string regionId) => regions.Any(r => r.Id == regionId);
        public bool ContainsFootPoint(string regionId, Float3 worldFoot)
        {
            var region = regions.FirstOrDefault(item => item.Id == regionId);
            if (region.Id == null) return false;
            var localFoot = world.MapRoot.InverseTransformPoint(worldFoot.ToUnity()).ToFloat();
            return region.Contains(localFoot + Float3.Up);
        }

        private bool TryHumanRegion(Float3 foot, out BotRegion region, out Float3 navigationPoint)
        {
            var sample = foot + Float3.Up;
            region = regions.OrderBy(r => Distance(sample, Clamp(sample, r)))
                .ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            navigationPoint = region.Id == null ? default : Clamp(sample, region);
            return region.Id != null && Distance(sample, navigationPoint) <= 3f;
        }
        // Width comes from the authored flight. Vertical slack matches waypoint arrival;
        // longitudinal extension includes the existing .45m stair entry/exit points.
        private static BotRegion StairFlightRegion(string id, Flight flight) => new BotRegion(id,
            new Float3(Mathf.Min(flight.clear_x[0], flight.clear_x[1]), Mathf.Min(flight.start_y, flight.end_y) + .85f - .35f,
                Mathf.Min(flight.start_z, flight.end_z) - .45f),
            new Float3(Mathf.Max(flight.clear_x[0], flight.clear_x[1]), Mathf.Max(flight.start_y, flight.end_y) + .85f + .35f,
                Mathf.Max(flight.start_z, flight.end_z) + .45f));
        private bool TryTargetPoint(string regionId, Float3 foot, out Float3 navigationPoint, out BotRegion region)
        {
            region = regions.FirstOrDefault(r => r.Id == regionId);
            if (region.Id == null) { navigationPoint = default; return false; }
            var sample = foot + Float3.Up;
            navigationPoint = Clamp(sample, region);
            return Distance(sample, navigationPoint) <= 3f;
        }
        private bool Reachable(string from, string target, Func<string, bool> open)
        {
            var queue = new Queue<string>(); queue.Enqueue(from);
            var visited = new HashSet<string>(StringComparer.Ordinal) { from };
            while (queue.Count > 0)
            {
                string node = queue.Dequeue(); if (node == target) return true;
                foreach (var passage in passages.Where(p => p.Points.Count > 0 && open(p.Id) && (p.From == node || p.To == node)))
                {
                    string next = passage.From == node ? passage.To : passage.From;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }
        private float ShortestAuthoredDistance(string from, Float3 start, string target, Float3 finish,
            Func<string, bool> passageAllowed)
        {
            return BotRoutePlanner.TryPlan(regions, passages, from, start, target, finish,
                passageAllowed, out var plan) ? plan.Distance : float.PositiveInfinity;
        }
        private static Float3 Clamp(Float3 point, BotRegion region) => new Float3(
            Mathf.Clamp(point.X, region.Min.X, region.Max.X), Mathf.Clamp(point.Y, region.Min.Y, region.Max.Y),
            Mathf.Clamp(point.Z, region.Min.Z, region.Max.Z));
        private static float Distance(Float3 a, Float3 b) => (a - b).Length;
        private static bool AlwaysOpen(string passage) => true;
        private bool IsOpen(string passage) => !doors.TryGetValue(passage, out var door) || (door && Mathf.Abs(door.AngleRadians) >= 60 * Mathf.Deg2Rad);
        private static float Mid(float[] v)
        { if (v == null || v.Length != 2 || !MathEx.Finite(v[0]) || !MathEx.Finite(v[1])) throw new ArgumentException("Invalid stair width."); return (v[0] + v[1]) * .5f; }
        private static Float3 Point(float[] v)
        {
            if (v == null || v.Length != 3) throw new ArgumentException("Invalid navigation point.");
            var point = new Float3(v[0], v[1], v[2]); if (!point.IsFinite) throw new ArgumentException("Nonfinite navigation point."); return point;
        }
    }
}
