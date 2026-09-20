using System;
using System.Collections.Generic;
using System.Linq;

namespace LetMeSleep.Gameplay
{
    public readonly struct BotRegion
    {
        public readonly string Id;
        public readonly Float3 Min, Max;
        public BotRegion(string id, Float3 min, Float3 max) { Id = id; Min = min; Max = max; }
        public bool Contains(Float3 p) => p.X >= Min.X - .04f && p.X <= Max.X + .04f && p.Y >= Min.Y - .04f && p.Y <= Max.Y + .04f && p.Z >= Min.Z - .04f && p.Z <= Max.Z + .04f;
    }
    public sealed class BotPassage
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public IReadOnlyList<Float3> Points { get; }
        public IReadOnlyList<BotRegion> TraversalRegions { get; }
        public BotPassage(string id, string from, string to, IReadOnlyList<Float3> points)
            : this(id, from, to, points, null) { }
        public BotPassage(string id, string from, string to, IReadOnlyList<Float3> points, IReadOnlyList<BotRegion> traversalRegions)
        { Id = id; From = from; To = to; Points = Array.AsReadOnly(GameplayRoundConfig.Copy(points));
            TraversalRegions = Array.AsReadOnly(GameplayRoundConfig.Copy(traversalRegions)); }
    }
    // Patrol uses authored public topology and passage availability, never hidden actor positions.
    // One instance per bot and round. All waypoints remain subject to the ordinary motor.
    public sealed class BotPatrol
    {
        private readonly BotRegion[] regions;
        private readonly BotPassage[] passages;
        private readonly uint seed;
        private readonly Dictionary<string, int> visits = new Dictionary<string, int>();
        private readonly Dictionary<string, uint> retryAfter = new Dictionary<string, uint>();
        private BotPassage route;
        private Float3[] points;
        private int pointIndex;
        private string lastRegion;
        private Float3? interiorPoint;
        private string interiorRegion;
        private uint interiorVersion;
        public BotNavigationProgress? CurrentProgress { get; private set; }
        public bool IsPassageBlocked(string passageId, uint tick) => passageId != null &&
            retryAfter.TryGetValue(passageId, out var until) && tick < until;
        public void InvalidatePassage(string passageId, uint untilTick)
        {
            if (!string.IsNullOrEmpty(passageId)) retryAfter[passageId] = untilTick;
            if (passageId == null || route?.Id == passageId) Clear();
            if (passageId == null || (objectivePassage?.Id == passageId && !CanSuspendDirectedPassage(lastDirectedPosition)))
            { objectivePassage = null; objectivePoints = null; objectivePointIndex = 0; }
            CurrentProgress = null; interiorPoint = null;
        }
        private Float3 Travel(Float3 direction, string key, string passageId)
        {
            CurrentProgress = new BotNavigationProgress(key, passageId, direction.Length);
            return direction;
        }
        public BotPatrol(IReadOnlyList<BotRegion> regions, IReadOnlyList<BotPassage> passages, uint seed)
        { this.regions = GameplayRoundConfig.Copy(regions); this.passages = GameplayRoundConfig.Copy(passages); this.seed = seed; }
        public Float3 Direction(Float3 position, uint tick, Func<string, bool> passageOpen)
        {
            CurrentProgress = null;
            string region = regions.Where(r => r.Contains(position)).Select(r => r.Id).FirstOrDefault();
            if (region != null && region != lastRegion)
            { visits[region] = VisitCount(region) + 1; lastRegion = region; }
            if (route != null)
            {
                if (!passageOpen(route.Id) || IsPassageBlocked(route.Id, tick) || (region != null && region != route.From && region != route.To)) Clear();
                else
                {
                    AdvanceReachedPatrolWaypoints(position);
                    if (pointIndex == points.Length) Clear();
                    else
                    {
                        return Travel(points[pointIndex] - position,
                            "explore:" + route.Id + ":" + pointIndex, route.Id);
                    }
                }
            }
            if (region == null) return Float3.Zero;
            var choices = passages.Where(p => p.Points.Count > 0 && (p.From == region || p.To == region) && passageOpen(p.Id)
                && (!retryAfter.TryGetValue(p.Id, out var retry) || tick >= retry)).ToArray();
            if (choices.Length > 0)
            {
                // Least visited adjacent region first; actor seed only breaks ties.
                int start = (int)(seed % (uint)choices.Length);
                route = Enumerable.Range(0, choices.Length).Select(i => choices[(start + i) % choices.Length])
                    .OrderBy(p => VisitCount(p.From == region ? p.To : p.From)).First();
                points = route.From == region ? route.Points.ToArray() : route.Points.Reverse().ToArray();
                pointIndex = 0;
                AdvanceReachedPatrolWaypoints(position);
                if (pointIndex < points.Length)
                    return Travel(points[pointIndex] - position,
                        "explore:" + route.Id + ":" + pointIndex, route.Id);
                Clear();
                return Float3.Zero;
            }
            // Closed rooms remain closed. Inspect reachable interior instead of pushing the wall.
            var current = regions.First(r => r.Id == region);
            if (!interiorPoint.HasValue || interiorRegion != region || (interiorPoint.Value - position).Length < .24f)
            {
                float phase = tick / 150 + seed * 2.399963f;
                var center = (current.Min + current.Max) * .5f;
                var extent = (current.Max - current.Min) * .25f;
                interiorPoint = new Float3(center.X + (float)Math.Sin(phase) * extent.X,
                    Math.Min(current.Max.Y - .3f, current.Min.Y + 1.1f), center.Z + (float)Math.Cos(phase) * extent.Z);
                interiorRegion = region; interiorVersion++;
            }
            return Travel(interiorPoint.Value - position, "interior:" + region + ":" + interiorVersion, null);
        }
        private void AdvanceReachedPatrolWaypoints(Float3 position)
        {
            while (pointIndex < points.Length && (points[pointIndex] - position).Length < .24f)
                pointIndex++;
        }
        private BotPassage objectivePassage;
        private Float3[] objectivePoints;
        private int objectivePointIndex;
        private string objectiveRegion;
        private Float3 objectiveApproach;
        private Float3 lastDirectedPosition;
#if UNITY_EDITOR
        // Opt-in editor diagnostics read this only after the real DirectionTo call.
        // It never advances or recomputes the route.
        public string DirectedRouteDiagnostic
        {
            get
            {
                string waypoint = objectivePoints != null && objectivePointIndex >= 0 &&
                                  objectivePointIndex < objectivePoints.Length
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}",
                        objectivePoints[objectivePointIndex].X, objectivePoints[objectivePointIndex].Y,
                        objectivePoints[objectivePointIndex].Z)
                    : "none";
                return "passage=" + (objectivePassage?.Id ?? "none") + " targetRegion=" +
                       (objectiveRegion ?? "none") + " pointIndex=" + objectivePointIndex + " waypoint=" + waypoint;
            }
        }
#endif
        // Route only toward the owning bot's resolved assignment, through authored passages.
        // Returns zero while no open route exists; callers never substitute a straight wall-crossing vector.
        public Float3 DirectionTo(Float3 position, string targetRegion, Float3 approachPoint, Func<string, bool> passageOpen) =>
            DirectionTo(position, targetRegion, approachPoint, 0, passageOpen);
        public Float3 DirectionTo(Float3 position, string targetRegion, Float3 approachPoint, uint tick, Func<string, bool> passageOpen)
            => DirectionTo(position, regions.Where(r => r.Contains(position)).Select(r => r.Id).FirstOrDefault(),
                targetRegion, approachPoint, tick, passageOpen);
        // Classification selects graph nodes; steering and waypoint arrival use the real
        // physical sample. Never move the sample to an unrelated nearby room's boundary.
        public Float3 DirectionTo(Float3 position, string current, string targetRegion, Float3 approachPoint, uint tick, Func<string, bool> passageOpen)
        {
            CurrentProgress = null;
            if (passageOpen == null || !position.IsFinite || !approachPoint.IsFinite) return Float3.Zero;
            lastDirectedPosition = position;
            var target = regions.FirstOrDefault(r => r.Id == targetRegion);
            if (target.Id == null || !target.Contains(approachPoint)) return Float3.Zero;
            if (objectiveRegion != targetRegion || (objectiveApproach - approachPoint).LengthSquared > .000001f ||
                (objectivePassage != null && !CanFollowDirectedPassage(position, current)))
            { objectivePassage = null; objectivePoints = null; objectiveRegion = targetRegion; objectiveApproach = approachPoint; }
            if (objectivePassage != null && (!passageOpen(objectivePassage.Id) || IsPassageBlocked(objectivePassage.Id, tick)))
            {
                // A bot already inside an unclassified stair shaft cannot acquire another
                // route from a fictitious room. Keep only this entered leg, suspended until
                // it reopens; target/position/corridor guards above still discard stale legs.
                if (CanSuspendDirectedPassage(position)) return Float3.Zero;
                objectivePassage = null; objectivePoints = null;
            }
            if (objectivePassage != null)
            {
                // Directed task travel is consumed by a ground human. Portal points are
                // authored at torso height while the navigation sample is foot + 1m, so
                // a 3D arrival radius can become narrower than one bot decision stride.
                // Advance on horizontal crossing; the ordinary motor remains responsible
                // for floors, stairs and every physical obstruction.
                AdvanceReachedDirectedWaypoints(position);
                if (objectivePointIndex < objectivePoints.Length) return Travel(objectivePoints[objectivePointIndex] - position,
                    "task:" + targetRegion + ":" + objectivePassage.Id + ":" + objectivePointIndex, objectivePassage.Id);
                objectivePassage = null; objectivePoints = null;
            }
            // An unclassified stair shaft is only reachable through an already entered
            // authored passage. Outside stairs, retain the existing nearest-region policy;
            // physical collision queries remain responsible for reaching its first point.
            var source = regions.FirstOrDefault(r => r.Id == current);
            bool insideAuthoredCorridor = passages.Any(p => p.TraversalRegions.Any(r => r.Contains(position)));
            if (source.Id == null || (!source.Contains(position) && insideAuthoredCorridor &&
                !CanAcquireFromConnectedEndpoint(position, current, tick, passageOpen)))
                return Float3.Zero;
            if (current == targetRegion) return Travel(approachPoint - position,
                "approach:" + targetRegion + ":" + approachPoint.X + ":" + approachPoint.Y + ":" + approachPoint.Z, null);
            if (!BotRoutePlanner.TryPlan(regions, passages, current, position, targetRegion, approachPoint,
                    id => passageOpen(id) && !IsPassageBlocked(id, tick), out var plan) ||
                plan.FirstPassage == null) return Float3.Zero;
            objectivePassage = plan.FirstPassage;
            objectiveRegion = targetRegion;
            objectivePoints = objectivePassage.From == current
                ? objectivePassage.Points.ToArray()
                : objectivePassage.Points.Reverse().ToArray();
            objectivePointIndex = 0;
            AdvanceReachedDirectedWaypoints(position);
            if (objectivePointIndex < objectivePoints.Length)
                return Travel(objectivePoints[objectivePointIndex] - position,
                    "task:" + targetRegion + ":" + objectivePassage.Id + ":" + objectivePointIndex, objectivePassage.Id);

            objectivePassage = null; objectivePoints = null; objectivePointIndex = 0;
            return target.Contains(position)
                ? Travel(approachPoint - position,
                    "approach:" + targetRegion + ":" + approachPoint.X + ":" + approachPoint.Y + ":" + approachPoint.Z, null)
                : Float3.Zero;
        }
        private void AdvanceReachedDirectedWaypoints(Float3 position)
        {
            while (objectivePointIndex < objectivePoints.Length &&
                   ReachedDirectedWaypoint(objectivePoints[objectivePointIndex], position))
                objectivePointIndex++;
        }
        private bool CanFollowDirectedPassage(Float3 position, string current)
        {
            if (objectivePassage.TraversalRegions.Count == 0)
                return current == objectivePassage.From || current == objectivePassage.To;
            if (objectivePointIndex == 0)
                return regions.Any(r => (r.Id == objectivePassage.From || r.Id == objectivePassage.To) && r.Contains(position));
            if (objectivePoints == null || objectivePointIndex >= objectivePoints.Length ||
                !objectivePassage.TraversalRegions.Any(r => r.Contains(position))) return false;
            var from = objectivePoints[objectivePointIndex - 1];
            var to = objectivePoints[objectivePointIndex];
            // Follow the active leg, not any part of the staircase on another floor.
            if (position.Y < Math.Min(from.Y, to.Y) - .35f || position.Y > Math.Max(from.Y, to.Y) + .35f) return false;
            var planar = new Float3(to.X - from.X, 0, to.Z - from.Z);
            float length = planar.Length;
            if (length < .0001f) return PlanarDistance(position, to) < .24f;
            float along = Float3.Dot(position - from, planar / length);
            return along >= -.24f && along <= length + .24f;
        }
        private bool CanSuspendDirectedPassage(Float3 position) => objectivePassage != null &&
            objectivePassage.TraversalRegions.Count > 0 && objectivePointIndex > 0 &&
            !regions.Any(r => r.Contains(position)) && CanFollowDirectedPassage(position, null);
        private bool CanAcquireFromConnectedEndpoint(Float3 position, string current, uint tick,
            Func<string, bool> passageOpen)
        {
            foreach (var passage in passages)
            {
                if (passage.TraversalRegions.Count == 0 ||
                    (!string.Equals(passage.From, current, StringComparison.Ordinal) &&
                     !string.Equals(passage.To, current, StringComparison.Ordinal)) ||
                    !passageOpen(passage.Id) || IsPassageBlocked(passage.Id, tick)) continue;
                int endpoint = string.Equals(passage.From, current, StringComparison.Ordinal)
                    ? 0 : passage.TraversalRegions.Count - 1;
                if (passage.TraversalRegions[endpoint].Contains(position)) return true;
            }
            return false;
        }
        private bool ReachedDirectedWaypoint(Float3 waypoint, Float3 position)
        {
            if (PlanarDistance(waypoint, position) >= .24f) return false;
            if (objectivePassage.TraversalRegions.Count > 0)
                return Math.Abs(waypoint.Y - position.Y) <= .35f;
            // Ordinary portal centers are authored near torso height, while ground-human
            // navigation samples use foot + 1 m. Accept that offset only on the physical
            // floor band of either connected region, never at the same XZ on another floor.
            return regions.Any(region =>
                (region.Id == objectivePassage.From || region.Id == objectivePassage.To) &&
                position.Y >= region.Min.Y - .04f && position.Y <= region.Max.Y + .04f);
        }
        private int VisitCount(string id) => visits.TryGetValue(id, out int count) ? count : 0;
        private static float PlanarDistance(Float3 a, Float3 b)
        {
            float x = a.X - b.X, z = a.Z - b.Z;
            return (float)Math.Sqrt(x * x + z * z);
        }
        private void Clear() { route = null; points = null; pointIndex = 0; }
    }
}
