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
        private float bestDistance;
        private uint progressTick;
        private string lastRegion;
        public BotPatrol(IReadOnlyList<BotRegion> regions, IReadOnlyList<BotPassage> passages, uint seed)
        { this.regions = GameplayRoundConfig.Copy(regions); this.passages = GameplayRoundConfig.Copy(passages); this.seed = seed; }
        public Float3 Direction(Float3 position, uint tick, Func<string, bool> passageOpen)
        {
            string region = regions.Where(r => r.Contains(position)).Select(r => r.Id).FirstOrDefault();
            if (region != null && region != lastRegion)
            { visits[region] = VisitCount(region) + 1; lastRegion = region; }
            if (route != null)
            {
                if (!passageOpen(route.Id) || (region != null && region != route.From && region != route.To)) Clear();
                else
                {
                    while (pointIndex < points.Length && (points[pointIndex] - position).Length < .24f)
                    { pointIndex++; bestDistance = float.MaxValue; progressTick = tick; }
                    if (pointIndex == points.Length) Clear();
                    else
                    {
                        float distance = (points[pointIndex] - position).Length;
                        if (distance < bestDistance - .08f) { bestDistance = distance; progressTick = tick; }
                        if (tick - progressTick > 150)
                        { retryAfter[route.Id] = tick + 180; Clear(); }
                        else return points[pointIndex] - position;
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
                pointIndex = 0; bestDistance = float.MaxValue; progressTick = tick;
                return points[0] - position;
            }
            // Closed rooms remain closed. Inspect reachable interior instead of pushing the wall.
            var current = regions.First(r => r.Id == region);
            float phase = tick / 150 + seed * 2.399963f;
            var center = (current.Min + current.Max) * .5f;
            var extent = (current.Max - current.Min) * .25f;
            var interior = new Float3(center.X + (float)Math.Sin(phase) * extent.X,
                Math.Min(current.Max.Y - .3f, current.Min.Y + 1.1f), center.Z + (float)Math.Cos(phase) * extent.Z);
            return interior - position;
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
        public Float3 DirectionTo(Float3 position, string targetRegion, Float3 approachPoint, Func<string, bool> passageOpen)
        {
            if (passageOpen == null || !position.IsFinite || !approachPoint.IsFinite) return Float3.Zero;
            string current = regions.Where(r => r.Contains(position)).Select(r => r.Id).FirstOrDefault();
            var target = regions.FirstOrDefault(r => r.Id == targetRegion);
            if (current == null || target.Id == null || !target.Contains(approachPoint)) return Float3.Zero;
            if (objectiveRegion != targetRegion || (objectivePassage != null && (!passageOpen(objectivePassage.Id) || (current != objectivePassage.From && current != objectivePassage.To))))
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
                if (objectivePointIndex < objectivePoints.Length) return objectivePoints[objectivePointIndex] - position;
                objectivePassage = null; objectivePoints = null;
            }
            if (current == targetRegion) return approachPoint - position;
            var queue = new Queue<string>(); queue.Enqueue(current);
            var visited = new HashSet<string> { current };
            var first = new Dictionary<string, BotPassage>();
            while (queue.Count > 0)
            {
                string node = queue.Dequeue();
                foreach (var passage in passages.Where(p => p.Points.Count > 0 && (p.From == node || p.To == node) && passageOpen(p.Id)).OrderBy(p => p.Id, StringComparer.Ordinal))
                {
                    string next = passage.From == node ? passage.To : passage.From;
                    if (!visited.Add(next)) continue;
                    first[next] = node == current ? passage : first[node];
                    if (next == targetRegion)
                    {
                        objectivePassage = first[next]; objectiveRegion = targetRegion;
                        objectivePoints = objectivePassage.From == current ? objectivePassage.Points.ToArray() : objectivePassage.Points.Reverse().ToArray();
                        objectivePointIndex = 0; return objectivePoints[0] - position;
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
