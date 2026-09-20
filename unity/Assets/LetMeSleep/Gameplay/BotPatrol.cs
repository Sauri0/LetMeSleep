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
        public BotPassage(string id, string from, string to, IReadOnlyList<Float3> points)
        { Id = id; From = from; To = to; Points = Array.AsReadOnly(GameplayRoundConfig.Copy(points)); }
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
            if (passageId == null || objectivePassage?.Id == passageId)
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
                    while (pointIndex < points.Length && (points[pointIndex] - position).Length < .24f)
                    { pointIndex++; }
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
                return Travel(points[0] - position, "explore:" + route.Id + ":0", route.Id);
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
        private BotPassage objectivePassage;
        private Float3[] objectivePoints;
        private int objectivePointIndex;
        private string objectiveRegion;
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
        {
            CurrentProgress = null;
            if (passageOpen == null || !position.IsFinite || !approachPoint.IsFinite) return Float3.Zero;
            string current = regions.Where(r => r.Contains(position)).Select(r => r.Id).FirstOrDefault();
            var target = regions.FirstOrDefault(r => r.Id == targetRegion);
            if (current == null || target.Id == null || !target.Contains(approachPoint)) return Float3.Zero;
            if (objectiveRegion != targetRegion || (objectivePassage != null && (!passageOpen(objectivePassage.Id) || IsPassageBlocked(objectivePassage.Id, tick) || (current != objectivePassage.From && current != objectivePassage.To))))
            { objectivePassage = null; objectivePoints = null; objectiveRegion = targetRegion; }
            if (objectivePassage != null)
            {
                // Directed task travel is consumed by a ground human. Portal points are
                // authored at torso height while the navigation sample is foot + 1m, so
                // a 3D arrival radius can become narrower than one bot decision stride.
                // Advance on horizontal crossing; the ordinary motor remains responsible
                // for floors, stairs and every physical obstruction.
                while (objectivePointIndex < objectivePoints.Length &&
                       PlanarDistance(objectivePoints[objectivePointIndex], position) < .24f &&
                       Math.Abs(objectivePoints[objectivePointIndex].Y - position.Y) <= .35f)
                    objectivePointIndex++;
                if (objectivePointIndex < objectivePoints.Length) return Travel(objectivePoints[objectivePointIndex] - position,
                    "task:" + targetRegion + ":" + objectivePassage.Id + ":" + objectivePointIndex, objectivePassage.Id);
                objectivePassage = null; objectivePoints = null;
            }
            if (current == targetRegion) return Travel(approachPoint - position,
                "approach:" + targetRegion + ":" + approachPoint.X + ":" + approachPoint.Y + ":" + approachPoint.Z, null);
            var queue = new Queue<string>(); queue.Enqueue(current);
            var visited = new HashSet<string> { current };
            var first = new Dictionary<string, BotPassage>();
            while (queue.Count > 0)
            {
                string node = queue.Dequeue();
                foreach (var passage in passages.Where(p => p.Points.Count > 0 && (p.From == node || p.To == node) && passageOpen(p.Id) && !IsPassageBlocked(p.Id, tick)).OrderBy(p => p.Id, StringComparer.Ordinal))
                {
                    string next = passage.From == node ? passage.To : passage.From;
                    if (!visited.Add(next)) continue;
                    first[next] = node == current ? passage : first[node];
                    if (next == targetRegion)
                    {
                        objectivePassage = first[next]; objectiveRegion = targetRegion;
                        objectivePoints = objectivePassage.From == current ? objectivePassage.Points.ToArray() : objectivePassage.Points.Reverse().ToArray();
                        objectivePointIndex = 0; return Travel(objectivePoints[0] - position,
                            "task:" + targetRegion + ":" + objectivePassage.Id + ":0", objectivePassage.Id);
                    }
                    queue.Enqueue(next);
                }
            }
            return Float3.Zero;
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
