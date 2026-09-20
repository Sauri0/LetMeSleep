using LetMeSleep.Core;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class EosProbeLifecycleTests
    {
        [Test]
        public void HostRequiresRemoteObservationBeforeAdvancingAndClosesAfterReturn()
        {
            var lifecycle = new EosProbeLifecycle(true);
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, false, false, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, true, true, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.StartRound));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Playing, 1, true, true, true, false, false, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Playing, 1, true, true, true, true, false, false), Is.EqualTo(EosProbeLifecycleAction.FinishRound));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Results, 1, true, true, true, true, false, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Results, 1, true, true, true, true, true, false), Is.EqualTo(EosProbeLifecycleAction.ReturnToLobby));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 1, false, false, false, true, true, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 1, false, false, false, true, true, true), Is.EqualTo(EosProbeLifecycleAction.CloseLobby));
            Assert.That(lifecycle.Advance(LobbyState.Idle, null, 0, false, false, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.Complete));
        }

        [Test]
        public void GuestCompletesOnlyAfterOwnerClosure()
        {
            var lifecycle = new EosProbeLifecycle(false);
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 0, false, false, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.SetReady));
            lifecycle.Advance(LobbyState.Connected, RoomPhase.Playing, 1, true, true, true, false, false, false);
            lifecycle.Advance(LobbyState.Connected, RoomPhase.Results, 1, true, true, true, false, false, false);
            lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 1, false, false, false, false, false, false);
            Assert.That(lifecycle.Advance(LobbyState.Connected, RoomPhase.Waiting, 1, false, false, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.None));
            Assert.That(lifecycle.Advance(LobbyState.Closed, RoomPhase.Waiting, 1, false, false, false, false, false, false), Is.EqualTo(EosProbeLifecycleAction.Complete));
        }
    }
}
