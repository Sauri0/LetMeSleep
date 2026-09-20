using System;
using System.IO;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class EquipmentNetworkCodecTests
    {
        private static CommandHeader Header => new CommandHeader(1, 2, 1, 4, 10, 1);
        [Test] public void PrimaryHeldRoundTripsAndRejectsNoncanonicalBoolean()
        {
            var input = new PlayerInputCommand(Header, default, 0, 0, 0, Float3.Forward, false, false, false, false, true);
            var packet = GameplayWireCodec.Encode(input);
            Assert.That(GameplayWireCodec.TryDecode(packet, out PlayerInputCommand decoded), Is.True);
            Assert.That(decoded.PrimaryHeld, Is.True);
            packet[packet.Length - 1] = 2;
            Assert.That(GameplayWireCodec.TryDecode(packet, out PlayerInputCommand _), Is.False);
        }
        [Test] public void ReleaseIdentityRoundTripsAndRejectsTruncationAndExtraData()
        {
            var command = new PlayerActionCommand(Header, ActionKind.ReleaseThrow, Float3.Forward, 1, 45, 7, 9);
            var packet = GameplayWireCodec.Encode(command);
            Assert.That(GameplayWireCodec.TryDecode(packet, out PlayerActionCommand decoded), Is.True);
            Assert.That(decoded.TargetPickupId, Is.EqualTo(45)); Assert.That(decoded.InventoryRevision, Is.EqualTo(9));
            for (int i = 0; i < packet.Length; i++)
                Assert.That(GameplayWireCodec.TryDecode(packet.Take(i).ToArray(), out PlayerActionCommand _), Is.False);
            Assert.That(GameplayWireCodec.TryDecode(packet.Concat(new byte[] { 0 }).ToArray(), out PlayerActionCommand _), Is.False);
        }
        [Test] public void CancelCannotSmuggleInventoryPayload()
        {
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(new PlayerActionCommand(Header,
                ActionKind.CancelThrow, Float3.Forward, 0, 45, 7, 9)));
        }
        private static ActorPrivateState Private(HumanInventorySnapshot inventory, ThrowChargeSnapshot charge = default, PickupSwapOffer? offer = null)
            => new ActorPrivateState(1, 1, 1, CommandReject.None, InteractionHint.None, 0, 0, 0, 0, true,
                default, 1, 2, 10, null, inventory, 25000, false, charge, offer);

        [Test] public void PrivateInventoryChargeAndReplacementOfferRoundTrip()
        {
            var state = Private(new HumanInventorySnapshot(9, 41, 45, 49, 1),
                new ThrowChargeSnapshot(45, 9, 20, true, 4), new PickupSwapOffer(51, 3, 9, 1, 60));
            Assert.That(GameplayWireCodec.TryDecode(GameplayWireCodec.Encode(state), out ActorPrivateState decoded), Is.True);
            Assert.That(decoded.Inventory.Slot2, Is.EqualTo(49)); Assert.That(decoded.StaminaUnits, Is.EqualTo(25000));
            Assert.That(decoded.ThrowCharge.AwaitingRelease, Is.True); Assert.That(decoded.ThrowCharge.ReleaseWaitTicks, Is.EqualTo(4));
            Assert.That(decoded.SwapOffer.Value.PickupId, Is.EqualTo(51));
        }
        [Test] public void PrivateRejectsDuplicateSlotsAndChargeFromStaleInventory()
        {
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Private(new HumanInventorySnapshot(9, 41, 41, 0, 0))));
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Private(new HumanInventorySnapshot(9, 41, 45, 0, 1), new ThrowChargeSnapshot(45, 8, 20))));
        }
        private static GameSessionState Snapshot(params ToolPickupSnapshot[] pickups)
        {
            var config = new GameplayRoundConfig(1, 2, RoomRules.AlfaMap, "content", 180);
            var actors = new[] {
                new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1, Float3.Zero, Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 0, true, 0, 0, null, null, default, 0),
                new ActorSnapshot(2, PlayerRole.Mosquito, LifeState.Flying, 1, new Float3(0,1,1), Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 0, false, 0, 0, null, null, default, 0) };
            return new GameSessionState(config, 10, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, actors, Array.Empty<DoorSnapshot>(), pickups);
        }
        private static ToolPickupSnapshot Held(uint id) => new ToolPickupSnapshot(id, GameplayTools.Slipper, Float3.Zero, Rotation.Identity, 1);
        [Test] public void ThreeStoredObjectsAreValidWithHandsSelectedButFourthIsRejected()
        {
            Assert.That(GameplayWireCodec.TryDecode(GameplayWireCodec.Encode(Snapshot(Held(1), Held(2), Held(3))), out GameSessionState _), Is.True);
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Snapshot(Held(1), Held(2), Held(3), Held(4))));
        }
        [Test] public void ProjectilePreservesSamePickupAndConsumedImpact()
        {
            var item = new ToolPickupSnapshot(8, GameplayTools.Slipper, new Float3(0,1,1), Rotation.Identity, 0, 7,
                ToolPickupPhase.Projectile, new Float3(0,-1,8), 1, 0, true);
            Assert.That(GameplayWireCodec.TryDecode(GameplayWireCodec.Encode(Snapshot(item)), out GameSessionState state), Is.True);
            Assert.That(state.ToolPickups[0].PickupId, Is.EqualTo(8)); Assert.That(state.ToolPickups[0].ImpactConsumed, Is.True);
        }
        [Test] public void PreviousWireVersionIsRejected()
        {
            var packet = GameplayWireCodec.Encode(new PlayerActionCommand(Header, ActionKind.CancelThrow, Float3.Forward));
            packet[4] = 4; packet[5] = 0;
            Assert.That(GameplayWireCodec.TryDecode(packet, out PlayerActionCommand _), Is.False);
        }
        [TestCase(99u)] [TestCase(2u)]
        public void ProjectileRejectsMissingOrMosquitoThrower(uint thrower)
        {
            var item = new ToolPickupSnapshot(8, GameplayTools.Slipper, new Float3(0,1,1), Rotation.Identity, 0, 7,
                ToolPickupPhase.Projectile, new Float3(0,-1,8), thrower, 0, false);
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Snapshot(item)));
        }
    }
}
