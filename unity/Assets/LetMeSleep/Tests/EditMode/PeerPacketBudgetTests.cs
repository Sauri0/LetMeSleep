using System;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class PeerPacketBudgetTests
    {
        [Test]
        public void LegitimateTrafficIsNeverDiscardedEvenAfterAMultiSecondStall()
        {
            var budget = new PeerPacketBudget();
            double now = 100;
            // A host at 250 packets/s (snapshot fragments, private state, events, voice) for 60 s ...
            for (int second = 0; second < 60; second++)
            for (int frame = 0; frame < 60; frame++, now += 1.0 / 60)
            for (int packet = 0; packet < (frame % 4 == 0 ? 5 : 4); packet++)
                Assert.That(budget.TryAccept("host", now), Is.True);
            // ... then the receiver stalls 8 s and Poll finds 2000 of them queued at once.
            now += 8;
            for (int packet = 0; packet < 2000; packet++)
                Assert.That(budget.TryAccept("host", now), Is.True, "packet " + packet);
        }

        [Test]
        public void FloodingMemberIsCutToItsBudgetWithoutAffectingOthers()
        {
            var budget = new PeerPacketBudget();
            double now = 5;
            int floodAccepted = 0, friendAccepted = 0, friendSent = 0;
            // 512 reads per frame: the flooder fills them for 2 s while a friend sends 4 packets per frame.
            for (int frame = 0; frame < 120; frame++, now += 1.0 / 60)
            {
                for (int read = 0; read < PeerPacketBudget.ReadsPerFrame - 4; read++) if (budget.TryAccept("flooder", now)) floodAccepted++;
                for (int read = 0; read < 4; read++, friendSent++) if (budget.TryAccept("friend", now)) friendAccepted++;
            }
            Assert.That(friendAccepted, Is.EqualTo(friendSent));
            Assert.That(floodAccepted, Is.LessThanOrEqualTo((int)(PeerPacketBudget.Burst + 2 * PeerPacketBudget.PacketsPerSecond) + 1));
            Assert.That(floodAccepted, Is.LessThan(120 * (PeerPacketBudget.ReadsPerFrame - 4) / 10), "Over 90% of the flood is discarded before dispatch.");
            Assert.That(PeerPacketBudget.ReadsPerFrame, Is.GreaterThanOrEqualTo(4 * 128), "Discarding is cheap, so Poll drains a flood faster than before.");
        }

        [Test]
        public void DepartedMembersAreForgottenAndInvalidInputIsRejected()
        {
            var budget = new PeerPacketBudget(10, 2);
            Assert.That(budget.TryAccept("a", 1), Is.True);
            Assert.That(budget.TryAccept("a", 1), Is.True);
            Assert.That(budget.TryAccept("a", 1), Is.False);
            budget.Forget("a");
            Assert.That(budget.TrackedCount, Is.Zero);
            Assert.That(budget.TryAccept("a", 1), Is.True, "A member that rejoins starts with a full bucket.");
            Assert.That(budget.TryAccept(null, 1), Is.False);
            Assert.That(budget.TryAccept("", 1), Is.False);
            Assert.That(budget.TryAccept("b", double.NaN), Is.False);
            budget.Clear();
            Assert.That(budget.TrackedCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PeerPacketBudget(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PeerPacketBudget(1, 0));
        }
    }
}
