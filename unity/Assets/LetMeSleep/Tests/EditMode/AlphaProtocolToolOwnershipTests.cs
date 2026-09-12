using System;
using System.IO;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class AlphaProtocolToolOwnershipTests
    {
        [Test]
        public void RoomAndGameplayWireVersionsStayOnAlphaTwo()
        {
            Assert.That(RoomSession.Protocol, Is.EqualTo("lms-unity-094-alfa-2"));
            Assert.That(GameplayWireCodec.Version, Is.EqualTo(2));
            Assert.That(RoomSession.Protocol, Does.EndWith("-" + GameplayWireCodec.Version));

            var room = new RoomSession("owner", "Owner", new RoomSessionTestSupport.SequenceRandom(0));
            var before = room.Snapshot();

            Assert.That(room.Join("old-client", "Old", "lms-unity-094-alfa-1"),
                Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(room.Snapshot().Revision, Is.EqualTo(before.Revision));
            Assert.That(room.Snapshot().Members.Count, Is.EqualTo(before.Members.Count));
            Assert.That(room.Join("current-client", "Current", RoomSession.Protocol), Is.EqualTo(RoomError.None));
        }

        [Test]
        public void SnapshotRoundTripPreservesEquippedFlyswatterOwnership()
        {
            GameSessionState original = State(pickupOwner: 1, humanTool: GameplayTools.Flyswatter);

            byte[] packet = GameplayWireCodec.Encode(original);

            Assert.That(BitConverter.ToUInt16(packet, 4), Is.EqualTo(GameplayWireCodec.Version));
            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState decoded), Is.True);
            Assert.That(decoded.SessionEpoch, Is.EqualTo(original.SessionEpoch));
            Assert.That(decoded.RoundId, Is.EqualTo(original.RoundId));
            Assert.That(decoded.HostTick, Is.EqualTo(original.HostTick));
            Assert.That(decoded.ToolPickups.Count, Is.EqualTo(1));
            Assert.That(decoded.ToolPickups[0].PickupId, Is.EqualTo(1001));
            Assert.That(decoded.ToolPickups[0].OwnerActorId, Is.EqualTo(1));
            Assert.That(decoded.ToolPickups[0].ToolId, Is.EqualTo(GameplayTools.Flyswatter));
            Assert.That(decoded.Actors.Single(actor => actor.ActorId == 1).EquippedToolId,
                Is.EqualTo(GameplayTools.Flyswatter));
            Assert.That(decoded.Actors.Single(actor => actor.ActorId == 2).EquippedToolId,
                Is.EqualTo(GameplayTools.Hands));
        }

        [TestCase(99u, GameplayTools.Hands, TestName = "SnapshotRejectsOrphanPickupOwner")]
        [TestCase(2u, GameplayTools.Hands, TestName = "SnapshotRejectsMosquitoPickupOwner")]
        [TestCase(0u, GameplayTools.Flyswatter, TestName = "SnapshotRejectsEquippedToolWithoutOwnedPickup")]
        public void InvalidToolOwnershipCannotBeEncoded(uint pickupOwner, string humanTool)
        {
            GameSessionState invalid = State(pickupOwner, humanTool);

            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(invalid));
        }

        [Test]
        public void PreviousGameplayWireVersionIsRejectedWithoutState()
        {
            byte[] packet = GameplayWireCodec.Encode(State(1, GameplayTools.Flyswatter));
            byte[] version = BitConverter.GetBytes((ushort)1);
            packet[4] = version[0];
            packet[5] = version[1];

            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState decoded), Is.False);
            Assert.That(decoded, Is.Null);
        }

        private static GameSessionState State(uint pickupOwner, string humanTool)
        {
            var pickupDefinition = new ToolPickupDefinition(
                1001, GameplayTools.Flyswatter, new Float3(1f, 0.8f, 2f), Rotation.Identity);
            var config = new GameplayRoundConfig(
                73, 4, RoomRules.AlfaMap, "sha256-test-content", 30, 20,
                tools: new[] { pickupDefinition });
            var actors = new[]
            {
                new ActorSnapshot(
                    1, PlayerRole.Human, LifeState.Active, 1,
                    new Float3(0f, 0f, 0f), Float3.Zero, Rotation.Identity, Float3.Forward,
                    0f, 0f, 1, 0, true, 0f, 0f, null, null, default, 0, humanTool),
                new ActorSnapshot(
                    2, PlayerRole.Mosquito, LifeState.Flying, 1,
                    new Float3(2f, 1f, 2f), Float3.Zero, Rotation.Identity, Float3.Forward,
                    0f, 0f, 1, 0, false, 0f, 0f, null, null, default, 0, GameplayTools.Hands)
            };
            var pickups = new[]
            {
                new ToolPickupSnapshot(
                    pickupDefinition.PickupId, pickupDefinition.ToolId,
                    pickupDefinition.Position, pickupDefinition.Rotation, pickupOwner, 2)
            };
            return new GameSessionState(
                config, 12, SimulationPhase.Running, 0f, RoundEndReason.None,
                PlayerRole.Unassigned, actors, Array.Empty<DoorSnapshot>(), pickups);
        }
    }
}
