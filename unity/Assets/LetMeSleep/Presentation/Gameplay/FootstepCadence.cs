using System;

namespace LetMeSleep.Presentation.Gameplay
{
    // Audio cadence is independent of the visual animation's authoritative phase.
    internal sealed class FootstepCadence
    {
        private double progress;
        private double lastStepTime = double.NegativeInfinity;

        public bool Advance(double planarDistance, double planarSpeed, double elapsed,
            double playbackTime, bool walking)
        {
            if (!walking || !Finite(planarDistance) || !Finite(planarSpeed) ||
                !Finite(elapsed) || !Finite(playbackTime) || elapsed <= 0 || elapsed > 0.25 ||
                planarDistance < 0 || planarSpeed < 0.2 ||
                planarDistance > Math.Max(0.15, planarSpeed * elapsed * 1.5))
            {
                progress = 0;
                return false;
            }

            // Crouch ~0.48 s, walk ~0.39 s, run >=0.28 s between contacts.
            double interval = Math.Max(0.28, Math.Min(0.55, 0.48 - (planarSpeed - 1.55) * 0.058));
            progress += planarDistance / (planarSpeed * interval);
            if (progress < 1)
                return false;

            // Keep one pending contact when snapshot quantization reaches it early.
            // Discard surplus distance; never replay a network backlog as a burst.
            progress = 1;
            if (playbackTime - lastStepTime < 0.28)
                return false;
            progress = 0;
            lastStepTime = playbackTime;
            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
