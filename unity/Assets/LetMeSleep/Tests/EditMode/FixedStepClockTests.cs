using System;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class FixedStepClockTests
    {
        [Test]
        public void SteadyFramesProduceOneTickPerStepWithoutDroppingTime()
        {
            var clock = new FixedStepClock();
            int steps = 0;
            for (int frame = 0; frame < 180; frame++) { steps += clock.Advance(1f / 60, out int dropped); Assert.That(dropped, Is.Zero); }
            Assert.That(steps, Is.InRange(89, 90), "3 s at 60 FPS is 90 ticks at 30 Hz.");
            Assert.That(clock.DroppedTicks, Is.Zero);
        }

        [Test]
        public void ShortHitchIsCaughtUpInFull()
        {
            var clock = new FixedStepClock();
            Assert.That(clock.Advance(.21f, out int dropped), Is.EqualTo(6));
            Assert.That(dropped, Is.Zero, "A 210 ms hitch stays inside the debt bound and is simulated.");
        }

        [Test]
        public void LongStallIsDroppedInsteadOfFastForwardedForSeveralFrames()
        {
            var clock = new FixedStepClock();
            int burst = clock.Advance(3f, out int dropped);
            Assert.That(burst, Is.LessThanOrEqualTo(8));
            Assert.That(burst + dropped, Is.InRange(89, 91), "Every stalled tick is either simulated or reported as dropped.");
            Assert.That(clock.DroppedTicks, Is.EqualTo(dropped));
            Assert.That(clock.Accumulated, Is.LessThan(FixedStepClock.DefaultStepSeconds));
            // Previously the 90-tick debt replayed as 8 ticks per frame for ~11 frames (8x speed for all clients).
            for (int frame = 0; frame < 12; frame++)
                Assert.That(clock.Advance(1f / 60, out _), Is.LessThanOrEqualTo(1), "No catch-up burst after the stall.");
        }

        [Test]
        public void OverloadedSimulationKeepsBoundedStepsPerFrame()
        {
            var clock = new FixedStepClock();
            for (int frame = 0; frame < 50; frame++)
            {
                // Each frame takes as long as eight ticks of real time: debt must not grow without bound.
                int steps = clock.Advance(.4f, out _);
                Assert.That(steps, Is.LessThanOrEqualTo(8));
                Assert.That(clock.Accumulated, Is.LessThanOrEqualTo(clock.MaximumDebtSeconds));
            }
        }

        [TestCase(.040f, 1)] [TestCase(.025f, 2)] [TestCase(.010f, 6)] [TestCase(.002f, 7)]
        public void ExpensiveStepsOnlyRunWhatFitsInTheFrameBudget(float stepCost, int expected)
        {
            var clock = new FixedStepClock();
            clock.ReportStepCost(stepCost);
            Assert.That(clock.Advance(.25f, out int dropped), Is.EqualTo(expected));
            Assert.That(dropped, Is.Zero, "Unrun debt stays pending inside the bound; nothing is dropped yet.");
        }

        [Test]
        public void SustainedOverloadKeepsFramesShortAndShedsSimulatedTime()
        {
            // Each tick costs 40 ms (the host cannot keep up with 30 Hz). Frame time = 10 ms of rendering + ticks run.
            var adaptive = new FixedStepClock();
            float worstAdaptive = 0; long adaptiveTicks = 0; double adaptiveSeconds = 0;
            float delta = 1f / 60;
            for (int frame = 0; frame < 300; frame++)
            {
                int steps = adaptive.Advance(delta, out _);
                for (int i = 0; i < steps; i++) adaptive.ReportStepCost(.04f);
                delta = .01f + steps * .04f; worstAdaptive = Math.Max(worstAdaptive, delta);
                adaptiveTicks += steps; adaptiveSeconds += delta;
                Assert.That(adaptive.Accumulated, Is.LessThanOrEqualTo(adaptive.MaximumDebtSeconds));
            }
            Assert.That(worstAdaptive, Is.LessThanOrEqualTo(.05f + 1e-4f), "One 40 ms tick per frame: about 20 FPS instead of multi-tick frames.");
            Assert.That(adaptive.DroppedTicks, Is.GreaterThan(0), "Time the host cannot simulate is shed and reported.");
            Assert.That(adaptiveTicks + adaptive.DroppedTicks + adaptive.Accumulated * 30, Is.EqualTo(adaptiveSeconds * 30).Within(1),
                "Every elapsed tick is simulated, dropped or still pending.");

            // Without cost feedback the same host stretches frames to the step cap (the spiral the bound only limits).
            var blind = new FixedStepClock();
            float worstBlind = 0; delta = 1f / 60;
            for (int frame = 0; frame < 300; frame++)
            {
                int steps = blind.Advance(delta, out _);
                delta = .01f + steps * .04f; worstBlind = Math.Max(worstBlind, delta);
            }
            Assert.That(worstBlind, Is.GreaterThan(.25f));
        }

        [Test]
        public void CheapStepsKeepTheFullCatchUpAndInvalidCostsAreIgnored()
        {
            var clock = new FixedStepClock();
            foreach (float cost in new[] { float.NaN, -1f, float.PositiveInfinity }) clock.ReportStepCost(cost);
            Assert.That(clock.AverageStepCostSeconds, Is.Zero);
            clock.ReportStepCost(.001f);
            Assert.That(clock.StepLimit, Is.EqualTo(FixedStepClock.DefaultMaximumStepsPerFrame));
            Assert.That(clock.Advance(.21f, out _), Is.EqualTo(6));
            // A single slow tick does not throttle at once: the cost is smoothed.
            clock.ReportStepCost(.05f);
            Assert.That(clock.StepLimit, Is.GreaterThan(1));
            clock.Reset();
            Assert.That(clock.AverageStepCostSeconds, Is.Zero);
            Assert.That(clock.StepLimit, Is.EqualTo(FixedStepClock.DefaultMaximumStepsPerFrame));
        }

        [TestCase(float.NaN)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidOrEmptyDeltaNeverSteps(float delta)
        {
            var clock = new FixedStepClock();
            Assert.That(clock.Advance(delta, out int dropped), Is.Zero);
            Assert.That(dropped, Is.Zero);
            Assert.That(clock.Accumulated, Is.Zero);
        }

        [Test]
        public void InfiniteDeltaIsTreatedAsAStall()
        {
            var clock = new FixedStepClock();
            Assert.That(clock.Advance(float.PositiveInfinity, out int dropped), Is.LessThanOrEqualTo(8));
            Assert.That(dropped, Is.GreaterThan(0));
            Assert.That(float.IsNaN(clock.Accumulated) || float.IsInfinity(clock.Accumulated), Is.False);
        }

        [Test]
        public void ResetDiscardsPendingTime()
        {
            var clock = new FixedStepClock();
            clock.Advance(.02f, out _);
            clock.Reset();
            Assert.That(clock.Advance(.02f, out _), Is.Zero);
        }

        [Test]
        public void RejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(1f / 30, 1f / 60));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(1f / 30, .25f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(1f / 30, .25f, 8, 0));
        }
    }
}
