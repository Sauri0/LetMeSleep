using System;
using System.Collections.Generic;
using System.Linq;

namespace LetMeSleep.Gameplay
{
    public readonly struct BotRoutePlan
    {
        public float Distance { get; }
        public BotPassage FirstPassage { get; }

        internal BotRoutePlan(float distance, BotPassage firstPassage)
        {
            Distance = distance;
            FirstPassage = firstPassage;
        }
    }

    // Shared authored-graph contract for task admission and actual bot travel.
    // In-region legs use endpoint distance; passage legs use their authored polyline.
    public static class BotRoutePlanner
    {
        public static bool TryPlan(IReadOnlyList<BotRegion> regions, IReadOnlyList<BotPassage> passages,
            string from, Float3 start, string target, Float3 finish, Func<string, bool> passageAllowed,
            out BotRoutePlan plan)
        {
            plan = default;
            if (regions == null || passages == null || from == null || target == null ||
                !start.IsFinite || !finish.IsFinite) return false;
            if (from == target)
            {
                plan = new BotRoutePlan(Distance(start, finish), null);
                return true;
            }

            var orderedPassages = passages.Where(item => item != null && item.Points.Count > 0 &&
                    (passageAllowed == null || passageAllowed(item.Id)))
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            var nodes = new List<RoutePoint> { new RoutePoint(from, start), new RoutePoint(target, finish) };
            var passageLengths = new float[orderedPassages.Length];
            for (int passageIndex = 0; passageIndex < orderedPassages.Length; passageIndex++)
            {
                var passage = orderedPassages[passageIndex];
                float length = 0;
                for (int point = 1; point < passage.Points.Count; point++)
                    length += Distance(passage.Points[point - 1], passage.Points[point]);
                passageLengths[passageIndex] = length;
                nodes.Add(new RoutePoint(passage.From, passage.Points[0], passageIndex));
                nodes.Add(new RoutePoint(passage.To, passage.Points[passage.Points.Count - 1], passageIndex));
            }

            var byRegion = nodes.Select((node, index) => new { node.Region, index })
                .GroupBy(item => item.Region, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.index).ToArray(),
                    StringComparer.Ordinal);
            var distances = Enumerable.Repeat(float.PositiveInfinity, nodes.Count).ToArray();
            var firstPassages = new BotPassage[nodes.Count];
            var pending = new SortedSet<RouteCost>(RouteCostComparer.Instance)
                { new RouteCost(0, 0, null) };
            distances[0] = 0;
            while (pending.Count > 0)
            {
                var current = pending.Min;
                pending.Remove(current);
                if (current.Distance != distances[current.Node] ||
                    ComparePassages(current.FirstPassage, firstPassages[current.Node]) != 0) continue;
                if (current.Node == 1)
                {
                    plan = new BotRoutePlan(current.Distance, current.FirstPassage);
                    return true;
                }

                foreach (int next in byRegion[nodes[current.Node].Region])
                {
                    if (next == current.Node) continue;
                    Relax(next, current.Distance + Distance(nodes[current.Node].Point, nodes[next].Point),
                        current.FirstPassage, distances, firstPassages, pending);
                }
                int passageIndex = nodes[current.Node].Passage;
                if (passageIndex < 0) continue;
                int firstEndpoint = 2 + passageIndex * 2;
                int oppositeEndpoint = current.Node == firstEndpoint ? firstEndpoint + 1 : firstEndpoint;
                Relax(oppositeEndpoint, current.Distance + passageLengths[passageIndex],
                    current.FirstPassage ?? orderedPassages[passageIndex], distances, firstPassages, pending);
            }
            return false;
        }

        private static void Relax(int node, float candidate, BotPassage firstPassage, float[] distances,
            BotPassage[] firstPassages, SortedSet<RouteCost> pending)
        {
            int distanceOrder = candidate.CompareTo(distances[node]);
            if (distanceOrder > 0 ||
                (distanceOrder == 0 && ComparePassages(firstPassage, firstPassages[node]) >= 0)) return;
            distances[node] = candidate;
            firstPassages[node] = firstPassage;
            pending.Add(new RouteCost(candidate, node, firstPassage));
        }

        private static int ComparePassages(BotPassage left, BotPassage right) =>
            string.Compare(left?.Id, right?.Id, StringComparison.Ordinal);
        private static float Distance(Float3 left, Float3 right) => (left - right).Length;

        private readonly struct RoutePoint
        {
            internal readonly string Region;
            internal readonly Float3 Point;
            internal readonly int Passage;
            internal RoutePoint(string region, Float3 point, int passage = -1)
            {
                Region = region;
                Point = point;
                Passage = passage;
            }
        }

        private readonly struct RouteCost
        {
            internal readonly float Distance;
            internal readonly int Node;
            internal readonly BotPassage FirstPassage;
            internal RouteCost(float distance, int node, BotPassage firstPassage)
            {
                Distance = distance;
                Node = node;
                FirstPassage = firstPassage;
            }
        }

        private sealed class RouteCostComparer : IComparer<RouteCost>
        {
            internal static readonly RouteCostComparer Instance = new RouteCostComparer();
            public int Compare(RouteCost left, RouteCost right)
            {
                int distance = left.Distance.CompareTo(right.Distance);
                if (distance != 0) return distance;
                int passage = ComparePassages(left.FirstPassage, right.FirstPassage);
                return passage != 0 ? passage : left.Node.CompareTo(right.Node);
            }
        }
    }
}
