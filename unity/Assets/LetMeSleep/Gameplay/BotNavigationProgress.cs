using System;

namespace LetMeSleep.Gameplay
{
    // Public topology only. Read after the final task/exploration direction was requested.
    public readonly struct BotNavigationProgress
    {
        public readonly string WaypointKey, PassageId;
        public readonly float RemainingDistance;
        public BotNavigationProgress(string waypointKey, string passageId, float remainingDistance)
        {
            if (string.IsNullOrEmpty(waypointKey) || !MathEx.Finite(remainingDistance) || remainingDistance < 0)
                throw new ArgumentException("Navigation progress requires a stable waypoint and finite distance.");
            WaypointKey = waypointKey; PassageId = passageId; RemainingDistance = remainingDistance;
        }
    }
    public sealed class BotNavigationContext
    {
        public Func<BotNavigationProgress?> ReadProgress { get; }
        public Action<string, uint> InvalidatePassage { get; }
        public BotNavigationContext(Func<BotNavigationProgress?> readProgress, Action<string, uint> invalidatePassage)
        {
            ReadProgress = readProgress ?? throw new ArgumentNullException(nameof(readProgress));
            InvalidatePassage = invalidatePassage ?? throw new ArgumentNullException(nameof(invalidatePassage));
        }
    }
}
