using System;

namespace LetMeSleep.Gameplay
{
    /// <summary>
    /// Fixed-rate stepping with bounded time debt. A long stall (window drag, GC, shader compile) or a
    /// simulation that cannot keep up must not replay seconds of backlog in bursts of fast-forwarded ticks:
    /// debt beyond <see cref="MaximumDebtSeconds"/> is dropped and reported, so simulated time pauses instead.
    /// </summary>
    public sealed class FixedStepClock
    {
        public const float DefaultStepSeconds = 1f / 30, DefaultMaximumDebtSeconds = .25f;
        public const int DefaultMaximumStepsPerFrame = 8;
        private readonly float step, maximumDebt;
        private readonly int maximumSteps;
        private float accumulator;

        public float StepSeconds => step;
        public float MaximumDebtSeconds => maximumDebt;
        public float Accumulated => accumulator;
        public long DroppedTicks { get; private set; }

        public FixedStepClock(float stepSeconds = DefaultStepSeconds, float maximumDebtSeconds = DefaultMaximumDebtSeconds,
            int maximumStepsPerFrame = DefaultMaximumStepsPerFrame)
        {
            if (!(stepSeconds > 0) || float.IsInfinity(stepSeconds) || !(maximumDebtSeconds >= stepSeconds) || float.IsInfinity(maximumDebtSeconds) || maximumStepsPerFrame < 1)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            step = stepSeconds; maximumDebt = maximumDebtSeconds; maximumSteps = maximumStepsPerFrame;
        }

        public void Reset() { accumulator = 0; }

        /// <summary>Adds elapsed frame time and returns how many fixed steps to run now.</summary>
        /// <param name="droppedTicks">Whole ticks of debt discarded this frame.</param>
        public int Advance(float deltaSeconds, out int droppedTicks)
        {
            droppedTicks = 0;
            if (float.IsNaN(deltaSeconds) || deltaSeconds <= 0) return 0;
            if (float.IsInfinity(deltaSeconds)) deltaSeconds = maximumDebt + step;
            accumulator += deltaSeconds;
            if (accumulator > maximumDebt)
            {
                double excess = accumulator - maximumDebt;
                droppedTicks = (int)Math.Min(int.MaxValue, Math.Ceiling(excess / step - 1e-6));
                accumulator = (float)Math.Max(0, accumulator - droppedTicks * (double)step);
                DroppedTicks += droppedTicks;
            }
            int steps = 0;
            while (accumulator >= step && steps < maximumSteps) { accumulator -= step; steps++; }
            return steps;
        }
    }
}
