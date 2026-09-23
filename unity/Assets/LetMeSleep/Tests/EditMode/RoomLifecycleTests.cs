using LetMeSleep.Bootstrap;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomLifecycleTests
    {
        [Test]
        public void FirstRoundOfANewRoomStartsAfterTheHostClosedThePreviousRoom()
        {
            var tracker = new ActiveRoundTracker();
            tracker.Begin("ROOMAAAAAA", 1);
            // Host of room A leaves: the client never goes through LeaveRoom. Room B numbers from 1 again.
            Assert.That(tracker.IsNewRound("ROOMBBBBBB", 1), Is.True, "Round 1 of room B must prepare the game.");
            tracker.Reset();
            Assert.That(tracker.IsActive, Is.False);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 1), Is.True, "Rejoining after a reset prepares the round again.");
        }

        [Test]
        public void RepeatedViewsOfTheActiveRoundDoNotRestartIt()
        {
            var tracker = new ActiveRoundTracker();
            tracker.Begin("ROOMAAAAAA", 3);
            Assert.That(tracker.IsActive, Is.True);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 3), Is.False);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 4), Is.True);
            Assert.That(tracker.IsNewRound(null, 3), Is.True);
        }

        [Test]
        public void JoinedRoomIsTornDownWhenItClosesOrFailsWithoutLeaving()
        {
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Closed, false, true), Is.True);
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Failed, false, true), Is.True,
                "AuthenticationLost inside the room must not leave a frozen waiting room behind a hidden error.");
        }

        [Test]
        public void CreatingWhileThePreviousRoomIsStillClosingIsReportedInsteadOfIgnored()
        {
            Assert.That(OnlineEntryPolicy.Evaluate(false, LobbyState.Leaving), Is.EqualTo(OnlineEntryDecision.WaitForPreviousRoom),
                "Otherwise the UI stays on 'Creando sala…' until the player presses Cancelar.");
            Assert.That(OnlineEntryPolicy.Evaluate(true, LobbyState.Leaving), Is.EqualTo(OnlineEntryDecision.IgnoreDuplicate));
            Assert.That(OnlineEntryPolicy.Evaluate(false, LobbyState.Connected), Is.EqualTo(OnlineEntryDecision.IgnoreDuplicate));
            Assert.That(OnlineEntryPolicy.Evaluate(true, null), Is.EqualTo(OnlineEntryDecision.IgnoreDuplicate));
            foreach (var state in new LobbyState?[] { null, LobbyState.Idle, LobbyState.Closed, LobbyState.Failed })
                Assert.That(OnlineEntryPolicy.Evaluate(false, state), Is.EqualTo(OnlineEntryDecision.Proceed), state?.ToString() ?? "no lobby");
        }

        [Test]
        public void JoinFailuresAndIntentionalLeavesKeepTheirOwnFlow()
        {
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Failed, false, false), Is.False, "Join errors stay on the join screen.");
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Closed, true, true), Is.False);
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Failed, true, true), Is.False);
            foreach (var state in new[] { LobbyState.Idle, LobbyState.Creating, LobbyState.Joining, LobbyState.Connected, LobbyState.Leaving })
                Assert.That(RoomTeardownPolicy.ShouldTearDown(state, false, true), Is.False, state.ToString());
        }
    }
}
