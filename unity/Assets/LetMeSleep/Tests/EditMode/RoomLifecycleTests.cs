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
        public void RejectedHandshakeKeepsItsReasonWhenTheFollowingLeaveFails()
        {
            // Update calls lobby.Leave() after "No se pudo entrar: X"; that Leave can end Failed (Leave_*, LobbyTimedOut).
            Assert.That(RoomTeardownPolicy.ShouldTearDown(LobbyState.Failed, false, true), Is.True);
            Assert.That(RoomTeardownPolicy.TeardownError(LobbyState.Failed, "LobbyTimedOut", "No se pudo entrar: Full"),
                Is.EqualTo("No se pudo entrar: Full"));
            Assert.That(RoomTeardownPolicy.TeardownError(LobbyState.Closed, "", "No se pudo entrar: Full"),
                Is.EqualTo("No se pudo entrar: Full"));
            Assert.That(RoomTeardownPolicy.TeardownError(LobbyState.Failed, "AuthenticationLost", ""),
                Is.EqualTo("Se perdió la conexión con la sala (AuthenticationLost). Volvé a intentarlo."));
            Assert.That(RoomTeardownPolicy.TeardownError(LobbyState.Closed, "", ""), Is.Null, "A host closing the room shows the plain notice.");
        }

        [Test]
        public void OnlyChangedSettingsAskToMarkReadyAgainInsideTheRoom()
        {
            Assert.That(RoomRejectionPolicy.Evaluate("InvalidRules", true, out string message), Is.EqualTo(RoomRejectionResponse.ShowInRoom));
            Assert.That(message, Is.EqualTo("Los ajustes cambiaron. Volvé a marcar Listo."));
            foreach (var code in new[] { "UnknownMember", "WrongPhase", "DuplicateMember" })
            {
                // A reconnect/phase race the next Hello or view corrects: nothing misleading for the player.
                Assert.That(RoomRejectionPolicy.Evaluate(code, true, out message), Is.EqualTo(RoomRejectionResponse.Ignore), code);
                Assert.That(message, Is.Empty, code);
            }
            Assert.That(RoomRejectionPolicy.Evaluate("", true, out message), Is.EqualTo(RoomRejectionResponse.Ignore));
            Assert.That(RoomRejectionPolicy.Evaluate(null, false, out message), Is.EqualTo(RoomRejectionResponse.Ignore));
        }

        [Test]
        public void LosingTheSeatOrTheRoomLeavesWithItsOwnReason()
        {
            Assert.That(RoomRejectionPolicy.Evaluate("Full", true, out string message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Is.EqualTo("Perdiste tu lugar en la sala: se llenó mientras te reconectabas."));
            Assert.That(RoomRejectionPolicy.Evaluate("Closed", true, out message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Is.EqualTo("La sala se cerró."));
            Assert.That(RoomRejectionPolicy.Evaluate("IncompatibleVersion", true, out message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Does.Contain("IncompatibleVersion"), "Keeps the version screen selection of ShowOnlineError.");
            Assert.That(RoomRejectionPolicy.Evaluate("NotEnoughPlayers", true, out message), Is.EqualTo(RoomRejectionResponse.ShowInRoom));
            Assert.That(message, Does.Not.Contain("ajustes"));
        }

        [Test]
        public void HandshakeRejectionsLeaveBeforeEnteringWithTheReason()
        {
            Assert.That(RoomRejectionPolicy.Evaluate("Full", false, out string message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Is.EqualTo("No se pudo entrar: la sala está llena (Full)."));
            Assert.That(RoomRejectionPolicy.Evaluate("IncompatibleVersion", false, out message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Is.EqualTo("No se pudo entrar: IncompatibleVersion"));
            Assert.That(RoomRejectionPolicy.Evaluate("RoomHandshakeTimedOut", false, out message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Is.EqualTo("No se pudo entrar: el anfitrión no respondió (RoomHandshakeTimedOut)."));
            Assert.That(RoomRejectionPolicy.Evaluate("InvalidRules", false, out message), Is.EqualTo(RoomRejectionResponse.LeaveRoom));
            Assert.That(message, Does.StartWith("No se pudo entrar: "));
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
