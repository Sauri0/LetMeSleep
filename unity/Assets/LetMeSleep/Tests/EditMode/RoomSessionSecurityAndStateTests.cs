using System;
using System.Linq;
using LetMeSleep.Core;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomSessionSecurityAndStateTests
    {
        [Test]
        public void ConstructorRejectsInvalidIdentityAndMissingRandomSource()
        {
            Assert.Throws<ArgumentException>(() => new RoomSession("", "Owner", new SequenceRandom(0)));
            Assert.Throws<ArgumentException>(() => new RoomSession("owner", " ", new SequenceRandom(0)));
            Assert.Throws<ArgumentException>(() => new RoomSession("owner\n", "Owner", new SequenceRandom(0)));
            Assert.Throws<ArgumentException>(() => new RoomSession("owner", new string('N', 25), new SequenceRandom(0)));
            Assert.Throws<ArgumentNullException>(() => new RoomSession("owner", "Owner", null));
        }

        [Test]
        public void JoinRequiresExactProtocolAndValidUniqueIdentity()
        {
            var session = new RoomSession("owner", "  Owner  ", new SequenceRandom(0));
            var before = session.Snapshot();
            var invalidId = new string('i', 129);

            Assert.That(session.Join("guest", "Guest", "lms-unity-094-alfa-0"), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(session.Join("guest", "Guest", "LMS-UNITY-094-ALFA-1"), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(session.Join("guest", "Guest", null), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(session.Join(" ", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.InvalidMember));
            Assert.That(session.Join(invalidId, "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.InvalidMember));
            Assert.That(session.Join("guest", "Bad\nName", RoomSession.Protocol), Is.EqualTo(RoomError.InvalidMember));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(before.Revision));

            Assert.That(session.Join("guest", "  Guest  ", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(session.Join("guest", "Another", RoomSession.Protocol), Is.EqualTo(RoomError.DuplicateMember));
            var joined = session.Snapshot();
            {
                Assert.That(Member(joined, "owner").Name, Is.EqualTo("Owner"));
                Assert.That(Member(joined, "guest").Name, Is.EqualTo("Guest"));
                Assert.That(joined.Members, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public void StartRequiresTwoReadyMembersAndOwnerAuthority()
        {
            var solo = new RoomSession("owner", "Owner", new SequenceRandom(0));
            Assert.That(solo.SetReady("owner", true), Is.EqualTo(RoomError.None));
            Assert.That(solo.StartRound("owner"), Is.EqualTo(RoomError.NotEnoughPlayers));

            var session = TwoPlayerSession();
            Assert.That(session.StartRound("guest-puid"), Is.EqualTo(RoomError.NotOwner));
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.NotReady));
            Assert.That(session.SetReady("unknown", true), Is.EqualTo(RoomError.UnknownMember));
            Assert.That(session.SetReady("owner-puid", true), Is.EqualTo(RoomError.None));
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.NotReady));
            Assert.That(session.SetReady("guest-puid", true), Is.EqualTo(RoomError.None));
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(session.Snapshot().Phase, Is.EqualTo(RoomPhase.Playing));
        }

        [Test]
        public void LifecycleRejectsTransitionsFromWrongPhaseOrCaller()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));

            {
                Assert.That(session.Join("late", "Late", RoomSession.Protocol), Is.EqualTo(RoomError.None));
                Assert.That(session.SetReady("owner-puid", false), Is.EqualTo(RoomError.WrongPhase));
                Assert.That(session.ChangeRules("owner-puid", new RoomRules(1)), Is.EqualTo(RoomError.WrongPhase));
                Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.WrongPhase));
                Assert.That(session.FinishRound("guest-puid"), Is.EqualTo(RoomError.NotOwner));
                Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.WrongPhase));
            }

            Assert.That(session.FinishRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.WrongPhase));
            Assert.That(session.ReturnToWaiting("guest-puid"), Is.EqualTo(RoomError.NotOwner));
            Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.None));

            var waiting = session.Snapshot();
            {
                Assert.That(waiting.Phase, Is.EqualTo(RoomPhase.Waiting));
                Assert.That(waiting.Round, Is.EqualTo(1));
                Assert.That(waiting.Members.All(member => !member.Ready), Is.True);
                Assert.That(waiting.Members.All(member => member.Role == PlayerRole.Unassigned), Is.True);
            }
        }

        [Test]
        public void OwnerDepartureClosesRoomAndNoOperationCanReopenIt()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var playing = session.Snapshot();

            Assert.That(session.Leave("owner-puid"), Is.EqualTo(RoomError.None));
            var closed = session.Snapshot();
            {
                Assert.That(closed.Phase, Is.EqualTo(RoomPhase.Closed));
                Assert.That(closed.Members.Select(member => member.Id), Is.EquivalentTo(new[] { "guest-puid" }));
                Assert.That(session.Join("late", "Late", RoomSession.Protocol), Is.EqualTo(RoomError.Closed));
                Assert.That(session.SetReady("guest-puid", true), Is.EqualTo(RoomError.Closed));
                Assert.That(session.ChangeRules("owner-puid", new RoomRules(1)), Is.EqualTo(RoomError.Closed));
                Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.Closed));
                Assert.That(session.FinishRound("owner-puid"), Is.EqualTo(RoomError.Closed));
                Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.Closed));
                Assert.That(session.Leave("guest-puid"), Is.EqualTo(RoomError.Closed));
            }
            Assert.That(playing.Phase, Is.EqualTo(RoomPhase.Playing), "A past view changed after host closure.");
        }

        [Test]
        public void GuestDepartureDuringRoundRemovesOnlyThatMember()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var before = session.Snapshot();

            Assert.That(session.Leave("guest-puid"), Is.EqualTo(RoomError.None));
            var after = session.Snapshot();
            {
                Assert.That(after.Phase, Is.EqualTo(RoomPhase.Playing));
                Assert.That(after.Members.Select(member => member.Id), Is.EquivalentTo(new[] { "owner-puid" }));
                Assert.That(session.Leave("guest-puid"), Is.EqualTo(RoomError.UnknownMember));
                Assert.That(before.Members, Has.Count.EqualTo(2), "A past roster changed after guest departure.");
            }
        }

        [Test]
        public void DisconnectReservesSameRoleForThirtySecondsWithoutExtendingDuplicateEvents()
        {
            var session = TwoPlayerSession(); ReadyEveryone(session); session.StartRound("owner-puid");
            var before = session.Snapshot();
            var role = Member(before, "guest-puid").Role;
            Assert.That(session.Disconnect("guest-puid", 100), Is.EqualTo(RoomError.None));
            var reserved = session.Snapshot();
            Assert.That(reserved.Members.Count, Is.EqualTo(2));
            Assert.That(Member(reserved, "guest-puid").Connected, Is.False);
            Assert.That(Member(before, "guest-puid").Connected, Is.True, "Old snapshots are immutable.");
            Assert.That(Member(reserved, "guest-puid").Role, Is.EqualTo(role));
            session.Disconnect("guest-puid", 125);
            Assert.That(session.Snapshot().Revision, Is.EqualTo(reserved.Revision));
            Assert.That(session.ExpireReservations(129.999), Is.False);
            Assert.That(session.ExpireReservations(130), Is.True);
            Assert.That(session.Snapshot().Members.Count, Is.EqualTo(1));
            Assert.That(session.Reconnect("guest-puid", "Guest", RoomSession.Protocol, 130), Is.EqualTo(RoomError.UnknownMember));
        }

        [Test]
        public void AuthenticatedReconnectKeepsIdentityRoleRoundAndCannotBeClaimedByName()
        {
            var session = TwoPlayerSession(); ReadyEveryone(session); session.StartRound("owner-puid");
            var before = session.Snapshot(); session.Disconnect("guest-puid", 10);
            Assert.That(session.Reconnect("impostor", "Guest", RoomSession.Protocol, 11), Is.EqualTo(RoomError.UnknownMember));
            Assert.That(session.Reconnect("guest-puid", "Guest", "old", 12), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(Member(session.Snapshot(), "guest-puid").Connected, Is.False);
            Assert.That(session.Reconnect("guest-puid", "Different name", RoomSession.Protocol, 39.999), Is.EqualTo(RoomError.None));
            var after = session.Snapshot();
            Assert.That(Member(after, "guest-puid").Connected, Is.True);
            Assert.That(Member(after, "guest-puid").Role, Is.EqualTo(Member(before, "guest-puid").Role));
            Assert.That(Member(after, "guest-puid").Name, Is.EqualTo("Guest"));
            Assert.That(after.Round, Is.EqualTo(before.Round));
            Assert.That(session.ExpireReservations(100), Is.False);
        }

        [Test]
        public void ExactDeadlineAndBackwardClockCannotRenewAnExpiredReservation()
        {
            var session = TwoPlayerSession(); ReadyEveryone(session); session.StartRound("owner-puid");
            session.Disconnect("guest-puid", 10); session.ExpireReservations(40);
            Assert.That(session.Reconnect("guest-puid", "Guest", RoomSession.Protocol, 20), Is.EqualTo(RoomError.UnknownMember));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExpireReservations(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExpireReservations(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Disconnect("owner-puid", -1));
        }

        [Test]
        public void WaitingDepartureIsImmediateOwnerLossClosesAndNewLobbyClearsReservations()
        {
            var waiting = TwoPlayerSession(); waiting.Disconnect("guest-puid", 1);
            Assert.That(waiting.Snapshot().Members.Count, Is.EqualTo(1));
            waiting.Disconnect("owner-puid", 2);
            Assert.That(waiting.Snapshot().Phase, Is.EqualTo(RoomPhase.Closed));
            var playing = TwoPlayerSession(); ReadyEveryone(playing); playing.StartRound("owner-puid");
            playing.Disconnect("guest-puid", 1); playing.FinishRound("owner-puid"); playing.ReturnToWaiting("owner-puid");
            Assert.That(playing.Snapshot().Members.Count, Is.EqualTo(1));
            Assert.That(playing.Join("guest-puid", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(Member(playing.Snapshot(), "guest-puid").Role, Is.EqualTo(PlayerRole.Unassigned));
        }

        [Test]
        public void ReservedRoomMemberRoundTripsAndPreviousRoomSchemaIsRejected()
        {
            var session = TwoPlayerSession(); ReadyEveryone(session); session.StartRound("owner-puid");
            session.Disconnect("guest-puid", 5);
            var packet = RoomWireCodec.Encode(session.Snapshot());
            Assert.That(RoomWireCodec.TryDecode(packet, "owner-puid", out var view), Is.True);
            Assert.That(Member(view, "guest-puid").Connected, Is.False);
            packet[0] = 2;
            Assert.That(RoomWireCodec.TryDecode(packet, "owner-puid", out _), Is.False);
        }

        [Test]
        public void NoOpAndRejectedCommandsDoNotAdvanceRevision()
        {
            var session = new RoomSession("owner", "Owner", new SequenceRandom(0));
            var initial = session.Snapshot().Revision;

            Assert.That(session.SetReady("owner", false), Is.EqualTo(RoomError.None));
            Assert.That(session.SetReady("missing", true), Is.EqualTo(RoomError.UnknownMember));
            Assert.That(session.ChangeRules("owner", new RoomRules(0)), Is.EqualTo(RoomError.InvalidRules));
            Assert.That(session.Join("bad", "Bad", "wrong"), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(session.StartRound("owner"), Is.EqualTo(RoomError.NotEnoughPlayers));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(initial));

            Assert.That(session.Join("guest", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            var joined = session.Snapshot().Revision;
            Assert.That(joined, Is.GreaterThan(initial));
            Assert.That(session.SetReady("guest", true), Is.EqualTo(RoomError.None));
            var ready = session.Snapshot().Revision;
            Assert.That(ready, Is.GreaterThan(joined));
            Assert.That(session.SetReady("guest", true), Is.EqualTo(RoomError.None));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(ready));
        }
    }
}
