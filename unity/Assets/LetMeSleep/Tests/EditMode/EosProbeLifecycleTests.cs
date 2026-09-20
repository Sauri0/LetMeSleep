using LetMeSleep.Core;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class EosProbeLifecycleTests
    {
        [Test]
        public void HostCompletesTwoDistinctRoundsAndClosesAfterSecondRemoteReturn()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 0, ready: false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 0, ready: true, allReady: true), Is.EqualTo(EosProbeLifecycleAction.StartRound));

            PlayHostRound(lifecycle, 1);
            Assert.That(lifecycle.CompletedRounds, Is.EqualTo(1));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 1, ready: false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 1), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 1, ready: false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 1, ready: true, allReady: true), Is.EqualTo(EosProbeLifecycleAction.StartRound));

            PlayHostRound(lifecycle, 2);
            Assert.That(lifecycle.CompletedRounds, Is.EqualTo(2));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 2, ready: false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 2), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 2, ready: false), Is.EqualTo(EosProbeLifecycleAction.CloseLobby));
            Assert.That(lifecycle.Advance(LobbyState.Idle, null, 0, false, false, false, true), Is.EqualTo(EosProbeLifecycleAction.Complete));
            Assert.That(lifecycle.RemoteConfirmedRounds, Is.EqualTo(2));
        }

        [Test]
        public void FirstRoundReplayCannotAcknowledgeSecondRound()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: false);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: true, allReady: true);
            PlayHostRound(lifecycle, 1);
            lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 1);
            Advance(lifecycle, RoomPhase.Waiting, 1, ready: false);
            Advance(lifecycle, RoomPhase.Waiting, 1, ready: true, allReady: true);

            Assert.That(Advance(lifecycle, RoomPhase.Playing, 2, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 1), Is.False, "old round must not advance current round");
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 2), Is.True);
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 2), Is.False, "duplicate must not count twice");
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 2, roles: true), Is.EqualTo(EosProbeLifecycleAction.FinishRound));
        }

        [Test]
        public void FutureOrOutOfOrderAcknowledgementIsRejected()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: false);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: true, allReady: true);
            Advance(lifecycle, RoomPhase.Playing, 1, roles: true);

            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 2), Is.False);
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Results, 1), Is.False);
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 1), Is.False);
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 1), Is.True);
        }

        [Test]
        public void GuestCompletesOnlyAfterTwoRoundsAndOwnerClosure()
        {
            var lifecycle = new EosProbeLifecycle(false);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 0, ready: false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            PlayGuestRound(lifecycle, 1);
            Assert.That(lifecycle.Advance(LobbyState.Closed, RoomPhase.Waiting, 1, false, false, false, true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 1, ready: false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            PlayGuestRound(lifecycle, 2);
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 2, false, false, false, true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Closed, RoomPhase.Waiting, 2, false, false, false, true), Is.EqualTo(EosProbeLifecycleAction.Complete));
        }

        [Test]
        public void HostDoesNotStartWithoutConfirmedTransportOrRepeatActions()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, false, true, false, false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, false, true, false, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, true, true, false, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, true, true, false, true), Is.EqualTo(EosProbeLifecycleAction.StartRound));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, true, true, false, true), Is.EqualTo(EosProbeLifecycleAction.None));
        }

        [Test]
        public void CheckpointWireRequiresCanonicalRoundAndRejectsHistoricalUntaggedMessage()
        {
            Assert.That(EosProbeLifecycle.TryParseCheckpoint("ProbePlaying", out _, out _), Is.False);
            Assert.That(EosProbeLifecycle.TryParseCheckpoint("ProbePlaying:01", out _, out _), Is.False);
            Assert.That(EosProbeLifecycle.TryParseCheckpoint("ProbeResults:3", out _, out _), Is.False);
            Assert.That(EosProbeLifecycle.TryParseCheckpoint("ProbeReturned:2", out EosProbeRoundCheckpoint checkpoint, out int round), Is.True);
            Assert.That(checkpoint, Is.EqualTo(EosProbeRoundCheckpoint.ReturnedLobby));
            Assert.That(round, Is.EqualTo(2));
            Assert.That(EosProbeLifecycle.EncodeCheckpoint(EosProbeRoundCheckpoint.Playing, 1), Is.EqualTo("ProbePlaying:1"));
        }

        [Test]
        public void RapidStateCallbacksExposeEachNextActionWithoutConsumingItEarly()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 0, ready: false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 0, ready: true, allReady: true), Is.EqualTo(EosProbeLifecycleAction.StartRound));
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 1, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 1), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 1, roles: true), Is.EqualTo(EosProbeLifecycleAction.FinishRound));
            Assert.That(Advance(lifecycle, RoomPhase.Results, 1), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Results, 1), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Results, 1), Is.EqualTo(EosProbeLifecycleAction.ReturnToLobby));
        }

        [Test]
        public void RoundTwoCannotSkipRoundOneAndStaleRoundOneCannotReplaceCurrentRound()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: false);
            Advance(lifecycle, RoomPhase.Waiting, 0, ready: true, allReady: true);
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 2, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.CurrentRound, Is.Zero);
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, 2), Is.False);

            PlayHostRound(lifecycle, 1);
            lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 1);
            Advance(lifecycle, RoomPhase.Waiting, 1, ready: false);
            Advance(lifecycle, RoomPhase.Waiting, 1, ready: true, allReady: true);
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 2, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.CurrentRound, Is.EqualTo(2));
            Assert.That(Advance(lifecycle, RoomPhase.Playing, 1, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.CurrentRound, Is.EqualTo(2));
        }

        [Test]
        public void SecondRoundReturnAloneCannotCloseHost()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 2, ready: false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.ReturnedLobby, 2), Is.False);
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, 2, ready: false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.CompletedRounds, Is.Zero);
            Assert.That(lifecycle.RemoteConfirmedRounds, Is.Zero);
        }

        private static void PlayHostRound(EosProbeLifecycle lifecycle, int round)
        {
            Assert.That(Advance(lifecycle, RoomPhase.Playing, round, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Playing, round), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Playing, round, roles: true), Is.EqualTo(EosProbeLifecycleAction.FinishRound));
            Assert.That(Advance(lifecycle, RoomPhase.Results, round), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.RecordRemote(EosProbeRoundCheckpoint.Results, round), Is.True);
            Assert.That(Advance(lifecycle, RoomPhase.Results, round), Is.EqualTo(EosProbeLifecycleAction.ReturnToLobby));
            Assert.That(Advance(lifecycle, RoomPhase.Waiting, round, ready: false), Is.EqualTo(EosProbeLifecycleAction.None));
        }

        private static void PlayGuestRound(EosProbeLifecycle lifecycle, int round)
        {
            Assert.That(Advance(lifecycle, RoomPhase.Playing, round, roles: true), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(Advance(lifecycle, RoomPhase.Results, round), Is.EqualTo(EosProbeLifecycleAction.None));
        }

        private static EosProbeLifecycleAction Advance(EosProbeLifecycle lifecycle, RoomPhase phase, int round,
            bool ready = true, bool allReady = false, bool roles = false)
            => lifecycle.Advance(LobbyState.Connected, phase, round, ready, allReady, roles, true);
    }
}
