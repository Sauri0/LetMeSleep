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
        }
    }
}
