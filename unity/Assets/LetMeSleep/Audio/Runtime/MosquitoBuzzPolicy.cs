using System.Collections.Generic;

namespace LetMeSleep.Audio
{
    public readonly struct MosquitoBuzzCandidate
    {
        public MosquitoBuzzCandidate(uint actorId, float squaredDistance)
        {
            ActorId = actorId;
            SquaredDistance = squaredDistance;
        }

        public uint ActorId { get; }
        public float SquaredDistance { get; }
    }

    // A swarm must not turn into an unbounded wall of identical loops. The old
    // runtime used the nearest six voices inside eight metres; retain that
    // listener-side rule independently from gameplay and replication.
    public static class MosquitoBuzzPolicy
    {
        public const int MaximumVoices = 6;
        public const float MaximumDistance = 8f;
        private const float MaximumSquaredDistance = MaximumDistance * MaximumDistance;

        public static void SelectNearest(
            List<MosquitoBuzzCandidate> candidates, HashSet<uint> selected)
        {
            selected.Clear();
            candidates.Sort(Compare);
            for (int i = 0; i < candidates.Count && selected.Count < MaximumVoices; i++)
            {
                MosquitoBuzzCandidate candidate = candidates[i];
                if (float.IsNaN(candidate.SquaredDistance) || float.IsInfinity(candidate.SquaredDistance) ||
                    candidate.SquaredDistance < 0f || candidate.SquaredDistance > MaximumSquaredDistance)
                    continue;
                selected.Add(candidate.ActorId);
            }
        }

        private static int Compare(MosquitoBuzzCandidate left, MosquitoBuzzCandidate right)
        {
            int distance = left.SquaredDistance.CompareTo(right.SquaredDistance);
            return distance != 0 ? distance : left.ActorId.CompareTo(right.ActorId);
        }
    }
}
