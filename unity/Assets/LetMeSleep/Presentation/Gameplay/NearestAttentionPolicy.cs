using System;
using System.Collections.Generic;

namespace LetMeSleep.Presentation.Gameplay
{
    // Pure cosmetic policy; callers supply only current-session visual instances.
    public sealed class NearestAttentionPolicy<T> where T : class
    {
        public const float RadiusSquared = 4f;
        public const float MinimumForwardDot = .5f; // 120 degrees total: +/-60.
        public const double SwitchSeconds = 2;
        public readonly struct Candidate
        {
            public readonly T Instance;
            public readonly float SquaredDistance, ForwardDot;
            public Candidate(T instance, float squaredDistance, float forwardDot = 1)
            { Instance = instance; SquaredDistance = squaredDistance; ForwardDot = forwardDot; }
        }
        public T Current { get; private set; }
        private T challenger;
        private double challengerSince, lastTime = double.NaN;
        public void Reset() { Current = challenger = null; lastTime = double.NaN; }
        public static bool IsEligible(float squaredDistance, float forwardDot) =>
            !float.IsNaN(squaredDistance) && !float.IsInfinity(squaredDistance) &&
            squaredDistance >= 0 && squaredDistance <= RadiusSquared &&
            !float.IsNaN(forwardDot) && !float.IsInfinity(forwardDot) && forwardDot >= MinimumForwardDot;

        public T Select(IReadOnlyList<Candidate> candidates, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) { Reset(); return null; }
            if (!double.IsNaN(lastTime) && now < lastTime) challenger = null;
            lastTime = now;
            T nearest = null;
            float nearestDistance = float.PositiveInfinity, currentDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                float d = item.SquaredDistance;
                if (item.Instance == null || !IsEligible(d, item.ForwardDot)) continue;
                if (ReferenceEquals(item.Instance, Current)) currentDistance = d;
                bool preferredTie = d == nearestDistance &&
                    (ReferenceEquals(item.Instance, Current) ||
                     (!ReferenceEquals(nearest, Current) && ReferenceEquals(item.Instance, challenger)));
                if (d < nearestDistance || preferredTie) { nearest = item.Instance; nearestDistance = d; }
            }
            if (float.IsPositiveInfinity(currentDistance))
            { Current = nearest; challenger = null; return Current; }
            if (nearest == null || ReferenceEquals(nearest, Current) || nearestDistance >= currentDistance)
            { challenger = null; return Current; }
            if (!ReferenceEquals(challenger, nearest)) { challenger = nearest; challengerSince = now; }
            else if (now - challengerSince >= SwitchSeconds) { Current = nearest; challenger = null; }
            return Current;
        }
    }
}

