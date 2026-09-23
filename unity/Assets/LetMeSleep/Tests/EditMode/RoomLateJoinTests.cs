using System.Linq;
using LetMeSleep.Core;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomLateJoinTests
    {
        [Test]
        public void PlayingRoomAcceptsAuthenticatedMemberOnlyAsNextRoundWaiting()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var before = session.Snapshot();

            Assert.That(session.Join("late-puid", "Late", RoomSession.Protocol), Is.EqualTo(RoomError.None));

            var playing = session.Snapshot();
            var late = Member(playing, "late-puid");
            Assert.That(playing.Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(playing.Round, Is.EqualTo(before.Round));
            Assert.That(late.Connected, Is.True);
            Assert.That(late.Ready, Is.False);
            Assert.That(late.Role, Is.EqualTo(PlayerRole.Unassigned));
            foreach (var active in before.Members)
                Assert.That(Member(playing, active.Id).Role, Is.EqualTo(active.Role), active.Id);
            Assert.That(session.SetReady("late-puid", true), Is.EqualTo(RoomError.WrongPhase));
        }

        [Test]
        public void ReservedAndLateWaitingMembersBothConsumeRoomCapacity()
        {
            var session = new RoomSession("owner", "Owner", new SequenceRandom(0));
            for (int i = 1; i < 15; i++)
                Assert.That(session.Join("guest-" + i, "Guest " + i, RoomSession.Protocol), Is.EqualTo(RoomError.None));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner"), Is.EqualTo(RoomError.None));
            Assert.That(session.Disconnect("guest-1", 10), Is.EqualTo(RoomError.None));

            Assert.That(session.Join("late-15", "Late 15", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(session.Snapshot().Members, Has.Count.EqualTo(RoomRules.Capacity));
            Assert.That(Member(session.Snapshot(), "guest-1").Connected, Is.False);
            Assert.That(session.Join("overflow", "Overflow", RoomSession.Protocol), Is.EqualTo(RoomError.Full));
        }

        [Test]
        public void LateWaitingMemberReceivesRoleOnlyWhenFollowingRoundStarts()
        {
            var session = TwoPlayerSession(); ReadyEveryone(session); session.StartRound("owner-puid");
            session.Join("late-puid", "Late", RoomSession.Protocol);
            Assert.That(Member(session.Snapshot(), "late-puid").Role, Is.EqualTo(PlayerRole.Unassigned));

            Assert.That(session.FinishRound("owner-puid"), Is.EqualTo(RoomError.None));
            // A friend entering the code while Results is shown waits for the next round instead of being rejected.
            Assert.That(session.Join("results-puid", "Results", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(session.Snapshot().Phase, Is.EqualTo(RoomPhase.Results));
            Assert.That(Member(session.Snapshot(), "results-puid").Role, Is.EqualTo(PlayerRole.Unassigned));
            Assert.That(session.SetReady("results-puid", true), Is.EqualTo(RoomError.WrongPhase));
            Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(session.Snapshot().Members.All(member => member.Role == PlayerRole.Unassigned), Is.True);
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(Member(session.Snapshot(), "late-puid").Role, Is.Not.EqualTo(PlayerRole.Unassigned));
            Assert.That(Member(session.Snapshot(), "results-puid").Role, Is.Not.EqualTo(PlayerRole.Unassigned));
        }
    }
}
