using System;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.UI;
using NUnit.Framework;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class EquipmentHudPresentationTests
    {
        [Test]
        public void PrivateInventoryResolvesAllToolNamesIconsAndResourcesFromSnapshotLedger()
        {
            var state = State();
            AssertSlot(state, 11, "MATAMOSCAS", "REUTILIZABLE", AlfaUiIconKind.Flyswatter);
            AssertSlot(state, 12, "PANTUFLA", "REUTILIZABLE", AlfaUiIconKind.Slipper);
            AssertSlot(state, 13, "RAQUETA ELÉCTRICA", "3 CARGAS", AlfaUiIconKind.ElectricRacket);
            AssertSlot(state, 14, "AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol);

            var personal = Private(new HumanInventorySnapshot(9, 11, 12, 13, 1));
            var equipment = new EquipmentHudUiState(ModeHudText.EquipmentSlots(state, personal), personal.Inventory.SelectedSlot,
                personal.StaminaUnits / (float)HumanEquipmentProfile.Maximum, .5f, false);
            Assert.That(equipment.Slots, Has.Count.EqualTo(3));
            Assert.That(equipment.SelectedSlot, Is.EqualTo(1));
            Assert.That(equipment.Stamina01, Is.EqualTo(.5f));
        }

        [Test]
        public void FullInventoryWithoutSelectionPromptsForSlotAndTaskTextUsesCurrentEControl()
        {
            var personal = Private(new HumanInventorySnapshot(4, 11, 12, 13, -1));
            Assert.That(ModeHudText.InventoryFullWithoutSelection(personal), Is.True);
            Assert.That(ModeHudText.ActionName("task.action.hold_repair"), Does.Contain("Mantené E"));
            Assert.That(ModeHudText.ActionName("task.action.hold_repair"), Does.Not.Contain("Mantené R"));
        }

        private static void AssertSlot(GameSessionState state, uint id, string name, string resource, AlfaUiIconKind icon)
        {
            var slot = ModeHudText.EquipmentSlot(state, id);
            Assert.That(slot.Label, Is.EqualTo(name));
            Assert.That(slot.ResourceText, Is.EqualTo(resource));
            Assert.That(slot.Icon, Is.EqualTo(icon));
        }

        private static ActorPrivateState Private(HumanInventorySnapshot inventory) => new ActorPrivateState(
            1, 0, 0, CommandReject.None, InteractionHint.Tool, 0, 0, 0, 0, true, DoorUseResult.Accepted,
            1, 2, 10, null, inventory, HumanEquipmentProfile.Maximum / 2, false, default, null);

        private static GameSessionState State()
        {
            var config = new GameplayRoundConfig(1, 2, RoomRules.AlfaMap, "content", 180);
            var actors = new[]
            {
                new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1, Float3.Zero, Float3.Zero,
                    Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0),
                new ActorSnapshot(2, PlayerRole.Mosquito, LifeState.Flying, 1, new Float3(0, 1, 1), Float3.Zero,
                    Rotation.Identity, Float3.Forward, 0, 0, 1, 1, false, 0, 0, null, null, default, 0)
            };
            var pickups = new[]
            {
                new ToolPickupSnapshot(11, GameplayTools.Flyswatter, Float3.Zero, Rotation.Identity, 1),
                new ToolPickupSnapshot(12, GameplayTools.Slipper, Float3.Zero, Rotation.Identity, 1),
                new ToolPickupSnapshot(13, GameplayTools.ElectricRacket, Float3.Zero, Rotation.Identity, 1, 1,
                    ToolPickupPhase.Held, Float3.Zero, 0, 3, false),
                new ToolPickupSnapshot(14, GameplayTools.Aerosol, Float3.Zero, Rotation.Identity, 0, 1,
                    ToolPickupPhase.World, Float3.Zero, 0, 87, false)
            };
            return new GameSessionState(config, 10, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, actors, Array.Empty<DoorSnapshot>(), pickups);
        }
    }
}
