using System;

namespace LetMeSleep.Gameplay
{
    /// <summary>
    /// Fixed-rate stepping with bounded time debt. A long stall (window drag, GC, shader compile) or a
    /// simulation that cannot keep up must not replay seconds of backlog in bursts of fast-forwarded ticks:
    /// debt beyond <see cref="MaximumDebtSeconds"/> is dropped and reported, so simulated time pauses instead.
    /// Callers report the measured cost of each step: when steps are expensive, a frame runs only as many as fit
    /// in <see cref="FrameBudgetSeconds"/> (at least one), so an overloaded host keeps a responsive frame rate and
    /// sheds simulated time instead of stretching every frame to the step cap.
    /// </summary>
    public sealed class FixedStepClock
    {
        public const float DefaultStepSeconds = 1f / 30, DefaultMaximumDebtSeconds = .25f;
        public const int DefaultMaximumStepsPerFrame = 8;
        public const float DefaultFrameBudgetSeconds = 1f / 15;
        private const float CostSmoothing = .25f;
        private readonly float step, maximumDebt, frameBudget;
        private readonly int maximumSteps;
        private float accumulator, averageStepCost;

        public float StepSeconds => step;
        public float MaximumDebtSeconds => maximumDebt;
        public float Accumulated => accumulator;
        public float FrameBudgetSeconds => frameBudget;
        public float AverageStepCostSeconds => averageStepCost;
        /// <summary>Steps a frame may run: the per-frame cap, reduced to what the measured step cost fits in the budget.</summary>
        public int StepLimit => averageStepCost <= 0 ? maximumSteps
            : (int)Math.Max(1, Math.Min(maximumSteps, Math.Floor(frameBudget / (double)averageStepCost)));
        public long DroppedTicks { get; private set; }

        public FixedStepClock(float stepSeconds = DefaultStepSeconds, float maximumDebtSeconds = DefaultMaximumDebtSeconds,
            int maximumStepsPerFrame = DefaultMaximumStepsPerFrame, float frameBudgetSeconds = DefaultFrameBudgetSeconds)
        {
            if (!(stepSeconds > 0) || float.IsInfinity(stepSeconds) || !(maximumDebtSeconds >= stepSeconds) || float.IsInfinity(maximumDebtSeconds) || maximumStepsPerFrame < 1
                || !(frameBudgetSeconds > 0) || float.IsInfinity(frameBudgetSeconds))
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            step = stepSeconds; maximumDebt = maximumDebtSeconds; maximumSteps = maximumStepsPerFrame; frameBudget = frameBudgetSeconds;
        }

        public void Reset() { accumulator = 0; averageStepCost = 0; }

        /// <summary>Measured wall time of one executed step (smoothed). Invalid measurements are ignored.</summary>
        public void ReportStepCost(float seconds)
        {
            if (!(seconds >= 0) || float.IsInfinity(seconds)) return;
            averageStepCost = averageStepCost <= 0 ? seconds : averageStepCost + (seconds - averageStepCost) * CostSmoothing;
        }

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
            int steps = 0, limit = StepLimit;
            while (accumulator >= step && steps < limit) { accumulator -= step; steps++; }
            return steps;
        }
    }
}
