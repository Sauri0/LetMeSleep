using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomMessageLimiterTests
    {
        [Test]
        public void FloodingMemberIsCappedAtBurstThenSteadyRate()
        {
            var limits = new RoomMessageLimiter();
            int accepted = 0;
            // A modified client sending 128 Hello in one frame used to trigger 128 room-wide reliable broadcasts.
            for (int i = 0; i < 128; i++) if (limits.TryAccept("flooder", 10)) accepted++;
            Assert.That(accepted, Is.EqualTo((int)RoomMessageLimiter.Burst));
            Assert.That(limits.TryAccept("flooder", 10.1), Is.False);
            Assert.That(limits.TryAccept("flooder", 10.1 + 1 / RoomMessageLimiter.MessagesPerSecond), Is.True);
            accepted = 0;
            for (double t = 11; t < 21; t += .01) if (limits.TryAccept("flooder", t)) accepted++;
            Assert.That(accepted, Is.InRange(41, 44), "Three refilled tokens plus about four messages per second sustained.");
        }

        [Test]
        public void LegitimateJoinAndReadyTrafficIsNeverThrottled()
        {
            var limits = new RoomMessageLimiter();
            for (double t = 0; t < 25; t += 1) Assert.That(limits.TryAccept("guest", t), Is.True, "Hello once per second.");
            for (int click = 0; click < 6; click++) Assert.That(limits.TryAccept("guest", 25 + click * .3), Is.True, "Quick Ready toggles.");
        }

        [Test]
        public void MembersHaveIndependentBudgetsAndDepartureResetsIt()
        {
            var limits = new RoomMessageLimiter();
            for (int i = 0; i < 20; i++) limits.TryAccept("flooder", 0);
            Assert.That(limits.TryAccept("flooder", 0), Is.False);
            Assert.That(limits.TryAccept("friend", 0), Is.True, "One member cannot exhaust another's budget.");
            limits.Forget("flooder");
            Assert.That(limits.TryAccept("flooder", 0), Is.True);
            Assert.That(limits.TryAccept(null, 0), Is.False);
            Assert.That(limits.TryAccept("", 0), Is.False);
        }
    }
}
