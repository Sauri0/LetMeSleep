using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class GameplayReplicaStateGateTests
    {
        [Test]
        public void SecondRoundRejectsAllDelayedFirstRoundChannelsWithoutPoisoningWatermarks()
        {
            var first = Config(41, 1);
            var second = Config(41, 2);
            var gate = new ReplicaStateGate();

            gate.Reset(first);
            Assert.That(gate.AcceptSnapshot(Snapshot(first, 30)), Is.True);
            Assert.That(gate.AcceptPrivate(Private(first, 30), 7), Is.True);
            Assert.That(gate.AcceptEvent(Event(first, 900, 30)), Is.True);

            gate.Reset(second);
            Assert.That(gate.AcceptSnapshot(Snapshot(first, first.RoundDurationTicks)), Is.False);
            Assert.That(gate.AcceptPrivate(Private(first, first.RoundDurationTicks), 7), Is.False);
            Assert.That(gate.AcceptEvent(Event(first, ulong.MaxValue, first.RoundDurationTicks)), Is.False);

            Assert.That(gate.AcceptSnapshot(Snapshot(second, 0)), Is.True);
            Assert.That(gate.AcceptPrivate(Private(second, 0), 7), Is.True);
            Assert.That(gate.AcceptEvent(Event(second, 1, 0)), Is.True);
        }

        [Test]
        public void RejectedHighTickPacketsDoNotAdvanceSnapshotOrPrivateWatermarks()
        {
            var current = Config(72, 8);
            var wrongMap = new GameplayRoundConfig(72, 8, "different-map", "content-a", 30);
            var gate = new ReplicaStateGate();
            gate.Reset(current);

            Assert.That(gate.AcceptSnapshot(Snapshot(wrongMap, 800)), Is.False);
            Assert.That(gate.AcceptSnapshot(Snapshot(current, 4)), Is.True);

            Assert.That(gate.AcceptPrivate(Private(current, 700, actorId: 99), 7), Is.False);
            Assert.That(gate.AcceptPrivate(Private(current, 3), 7), Is.True);
        }

        [Test]
        public void EventReplayWindowHasExactBoundaryAndResetsForNextRound()
        {
            var first = Config(90, 3);
            var second = Config(90, 4);
            var gate = new ReplicaStateGate();
            gate.Reset(first);

            Assert.That(gate.AcceptEvent(Event(first, 1025, 5)), Is.True);
            Assert.That(gate.AcceptEvent(Event(first, 2, 4)), Is.True, "1023 IDs behind remains inside the window.");
            Assert.That(gate.AcceptEvent(Event(first, 2, 4)), Is.False, "An in-window replay must still be rejected.");
            Assert.That(gate.AcceptEvent(Event(first, 1, 3)), Is.False, "1024 IDs behind is the first excluded value.");

            gate.Reset(second);
            Assert.That(gate.AcceptEvent(Event(first, 2049, 6)), Is.False, "A delayed old-round high ID cannot poison the new window.");
            Assert.That(gate.AcceptEvent(Event(second, 1, 0)), Is.True, "Event IDs restart at one each round.");
        }

        [Test]
        public void ClearingGateRejectsPacketsUntilAnotherRoundIsConfigured()
        {
            var round = Config(15, 6);
            var gate = new ReplicaStateGate();
            gate.Reset(round);
            gate.Reset(null);

            Assert.That(gate.AcceptSnapshot(Snapshot(round, 1)), Is.False);
            Assert.That(gate.AcceptPrivate(Private(round, 1), 7), Is.False);
            Assert.That(gate.AcceptEvent(Event(round, 1, 1)), Is.False);
        }

        private static GameplayRoundConfig Config(ulong epoch, ulong round) =>
            new GameplayRoundConfig(epoch, round, RoomRules.AlfaMap, "content-a", 30);

        private static GameSessionState Snapshot(GameplayRoundConfig config, uint tick) =>
            new GameSessionState(config, tick, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>());

        private static ActorPrivateState Private(GameplayRoundConfig config, uint tick, uint actorId = 7) =>
            new ActorPrivateState(actorId, 0, 0, CommandReject.None, InteractionHint.None, 0, 0, 0, 0,
                true, DoorUseResult.Accepted, config.SessionEpoch, config.RoundId, tick);

        private static GameplayEvent Event(GameplayRoundConfig config, ulong eventId, uint tick) =>
            new GameplayEvent(config.SessionEpoch, config.RoundId, eventId, tick, GameplayEventKind.StrikeStarted,
                7, 0, 1, Float3.Zero, Float3.Zero);
    }
}
