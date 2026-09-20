using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class HumanEquipmentRulesTests
    {
        private static void Store(HumanInventory inventory,uint id)
        {
            Assert.That(inventory.PlanPickup(id,false,inventory.Revision,out var plan),Is.EqualTo(InventoryPlanResult.Ready));
            Assert.That(inventory.CommitPickup(plan,true),Is.True);
        }
        [Test] public void SprintWalkRestAndCrouchUseChosenRates()
        {
            var stamina=new HumanStamina();
            for(int i=0;i<30;i++)stamina.Step(true,true,false,true);
            Assert.That(stamina.Points,Is.EqualTo(84));
            for(int i=0;i<30;i++)stamina.Step(false,true,false,true);
            Assert.That(stamina.Points,Is.EqualTo(96));
            stamina.TrySpend(18000);
            for(int i=0;i<30;i++)stamina.Step(false,false,false,true);
            Assert.That(stamina.Points,Is.EqualTo(64));
            stamina.TrySpend(stamina.Units);
            for(int i=0;i<30;i++)stamina.Step(true,true,true,true);
            Assert.That(stamina.Points,Is.EqualTo(28));
        }
        [Test] public void ExhaustionUsesLastHalfTickThenRequiresSprintRelease()
        {
            var stamina=new HumanStamina();
            for(int i=0;i<187;i++)stamina.Step(true,true,false,true);
            Assert.That(stamina.Units,Is.EqualTo(80));
            Assert.That(stamina.Step(true,true,false,true),Is.EqualTo(.5f));
            Assert.That(stamina.SprintExhausted,Is.True);
            Assert.That(stamina.Step(true,true,false,true),Is.Zero);
            Assert.That(stamina.Units,Is.EqualTo(120));
            Assert.That(stamina.Step(true,true,false,true),Is.Zero);
            stamina.Step(false,true,false,true);
            Assert.That(stamina.Step(true,true,false,true),Is.GreaterThan(0));
        }
        [Test] public void SpendIsAtomicAndRecoveryCannotExceedMaximum()
        {
            var stamina=new HumanStamina();
            Assert.That(stamina.TrySpend(HumanEquipmentProfile.JumpCost),Is.True);
            Assert.That(stamina.Points,Is.EqualTo(90));
            Assert.That(stamina.TrySpend(stamina.Units+1),Is.False);
            Assert.That(stamina.Points,Is.EqualTo(90));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>stamina.TrySpend(-1));
            for(int i=0;i<1000;i++)stamina.Step(true,false,false,false);
            Assert.That(stamina.Points,Is.EqualTo(100));
        }
        [Test] public void FullInventoryRequiresConfirmationAndAtomicSafeDeposit()
        {
            var inventory=new HumanInventory();Store(inventory,11);Store(inventory,12);Store(inventory,13);
            Assert.That(inventory.PlanPickup(14,false,inventory.Revision,out _),Is.EqualTo(InventoryPlanResult.ConfirmationRequired));
            Assert.That(inventory.PlanPickup(14,true,inventory.Revision,out var plan),Is.EqualTo(InventoryPlanResult.Ready));
            Assert.That(inventory.CommitPickup(plan,false),Is.False);
            Assert.That(inventory.ActivePickup,Is.EqualTo(13));
            Assert.That(inventory.CommitPickup(plan,true),Is.True);
            Assert.That(inventory.ActivePickup,Is.EqualTo(14));
            Assert.That(inventory.CommitPickup(plan,true),Is.False);
        }
        [Test] public void HandsNeverChooseAnOccupiedSlotForReplacement()
        {
            var inventory=new HumanInventory();Store(inventory,11);Store(inventory,12);Store(inventory,13);
            inventory.Select(-1);
            Assert.That(inventory.PlanPickup(14,true,inventory.Revision,out _),Is.EqualTo(InventoryPlanResult.SelectSlot));
            Assert.That(inventory.Capture().Slot0,Is.EqualTo(11));
            Assert.That(inventory.Capture().Slot1,Is.EqualTo(12));
            Assert.That(inventory.Capture().Slot2,Is.EqualTo(13));
        }
        [Test] public void OldRevisionAndDuplicatePickupCannotMutateInventory()
        {
            var inventory=new HumanInventory();Store(inventory,11);
            Assert.That(inventory.PlanPickup(11,true,inventory.Revision,out _),Is.EqualTo(InventoryPlanResult.AlreadyOwned));
            Assert.That(inventory.PlanPickup(12,false,inventory.Revision,out var plan),Is.EqualTo(InventoryPlanResult.Ready));
            inventory.Select(-1);
            Assert.That(inventory.CommitPickup(plan,true),Is.False);
            Assert.That(inventory.PlanPickup(12,false,plan.InventoryRevision,out _),Is.EqualTo(InventoryPlanResult.StaleRevision));
            inventory.Select(0);
            Assert.That(inventory.RemoveActive(inventory.Revision,11,false),Is.False);
            Assert.That(inventory.ActivePickup,Is.EqualTo(11));
        }
        [Test] public void OnlyPantuflaChargesAndPowerCostAreBounded()
        {
            var charge=new HumanThrowCharge();Assert.That(charge.Begin("flyswatter",11,1),Is.False);
            Assert.That(charge.Begin("slipper",11,1),Is.True);
            Assert.That(charge.Capture().Power,Is.EqualTo(.35f));
            Assert.That(charge.Capture().StaminaCost,Is.EqualTo(2400));
            int previous=2400;
            for(int i=0;i<27;i++){charge.Step(11,1,true);Assert.That(charge.Capture().StaminaCost,Is.InRange(previous,4500));previous=charge.Capture().StaminaCost;}
            Assert.That(charge.Capture().Power,Is.EqualTo(1));
            Assert.That(charge.Capture().StaminaCost,Is.EqualTo(4500));
            for(int i=0;i<100;i++)charge.Step(11,1,true);
            Assert.That(charge.Capture().ElapsedTicks,Is.EqualTo(45));
            Assert.That(charge.Capture().LimitReached,Is.True);
        }
        [Test] public void SelectionRevisionAndLostControlCancelCharge()
        {
            var charge=new HumanThrowCharge();charge.Begin("slipper",11,2);charge.Step(11,3,true);
            Assert.That(charge.Capture().Active,Is.False);
            charge.Begin("slipper",11,3);charge.Step(11,3,false);
            Assert.That(charge.Capture().Active,Is.False);
            charge.Begin("slipper",11,3);charge.Cancel();
            Assert.That(charge.TryPreviewRelease(11,3,out _),Is.False);
        }
        [Test] public void InsufficientStaminaCancelsWithoutDowngradingPowerOrLosingPickup()
        {
            var inventory=new HumanInventory();Store(inventory,11);var stamina=new HumanStamina();
            stamina.TrySpend(stamina.Units-4499);var charge=new HumanThrowCharge();charge.Begin("slipper",11,inventory.Revision);
            for(int i=0;i<45;i++)charge.Step(11,inventory.Revision,true);
            Assert.That(charge.CommitPreparedRelease(inventory,stamina,true,out _),Is.False);
            Assert.That(stamina.Units,Is.EqualTo(4499));Assert.That(inventory.ActivePickup,Is.EqualTo(11));
            Assert.That(charge.Capture().Active,Is.False);
        }
        [Test] public void PreparedReleaseConsumesOnceAndBlockedPreparationIsFree()
        {
            var inventory=new HumanInventory();Store(inventory,11);var stamina=new HumanStamina();var charge=new HumanThrowCharge();
            charge.Begin("slipper",11,inventory.Revision);
            Assert.That(charge.CommitPreparedRelease(inventory,stamina,false,out _),Is.False);
            Assert.That(stamina.Points,Is.EqualTo(100));Assert.That(inventory.ActivePickup,Is.EqualTo(11));
            charge.Begin("slipper",11,inventory.Revision);for(int i=0;i<45;i++)charge.Step(11,inventory.Revision,true);
            Assert.That(charge.CommitPreparedRelease(inventory,stamina,true,out var released),Is.True);
            Assert.That(released.Power,Is.EqualTo(1));Assert.That(stamina.Points,Is.EqualTo(85));
            Assert.That(inventory.ActivePickup,Is.Zero);Assert.That(inventory.SelectedSlot,Is.EqualTo(-1));
            Assert.That(charge.CommitPreparedRelease(inventory,stamina,true,out _),Is.False);
            Assert.That(stamina.Points,Is.EqualTo(85));
        }
        [Test] public void ProfileHashIsCanonicalHexForRoundCompatibility()
        {
            Assert.That(HumanEquipmentProfile.Hash,Does.Match("^[0-9a-f]{64}$"));
            Assert.That(HumanEquipmentProfile.CanonicalBalance,Does.Contain("auto-release45:explicit-release:false-freezes:stale-cancels:insufficient-cancel"));
        }
        [Test] public void NeutralPacketBeforeCancelNeverTriggersThrowOrContinuesPower()
        {
            var charge=new HumanThrowCharge();charge.Begin("slipper",11,1);
            for(int i=0;i<44;i++)charge.Step(11,1,true,true,true);
            charge.Step(11,1,true,false,true);
            Assert.That(charge.Capture().AwaitingRelease,Is.True);
            Assert.That(charge.Capture().AutoReleaseDue,Is.False);
            Assert.That(charge.Capture().ElapsedTicks,Is.EqualTo(44));
            charge.Step(11,1,true,true,true);
            Assert.That(charge.Capture().ElapsedTicks,Is.EqualTo(44));
            Assert.That(charge.Capture().AutoReleaseDue,Is.False);
            charge.Cancel();Assert.That(charge.TryPreviewRelease(11,1,out _),Is.False);
        }
        [Test] public void ExplicitReleaseUsesFrozenPowerOrCurrentActionTick()
        {
            var charge=new HumanThrowCharge();charge.Begin("slipper",11,1);
            for(int i=0;i<10;i++)charge.Step(11,1,true,true,true);
            Assert.That(charge.TryPreviewRelease(11,1,out var held),Is.True);
            Assert.That(held.ElapsedTicks,Is.EqualTo(10));
            charge.Step(11,1,true,false,true);
            for(int i=0;i<10;i++)charge.Step(11,1,true,false,true);
            Assert.That(charge.TryPreviewRelease(11,1,out var released),Is.True);
            Assert.That(released.ElapsedTicks,Is.EqualTo(10));
            Assert.That(released.Power,Is.EqualTo(held.Power));
        }
        [Test] public void OrphanNeutralChargeCancelsAtThirtyTicks()
        {
            var charge=new HumanThrowCharge();charge.Begin("slipper",11,1);
            for(int i=0;i<29;i++)charge.Step(11,1,true,false,true);
            Assert.That(charge.Capture().Active,Is.True);
            Assert.That(charge.Capture().ReleaseWaitTicks,Is.EqualTo(29));
            charge.Step(11,1,true,false,true);
            Assert.That(charge.Capture().Active,Is.False);
            Assert.That(charge.TryPreviewRelease(11,1,out _),Is.False);
        }
        [Test] public void StaleInputCancelsAndBeginCannotRenewActiveCharge()
        {
            var charge=new HumanThrowCharge();charge.Begin("slipper",11,1);
            for(int i=0;i<44;i++)charge.Step(11,1,true,true,true);
            Assert.That(charge.Begin("slipper",11,1),Is.False);
            Assert.That(charge.Capture().ElapsedTicks,Is.EqualTo(44));
            charge.Step(11,1,true,true,false);
            Assert.That(charge.Capture().Active,Is.False);
            Assert.That(charge.Capture().AutoReleaseDue,Is.False);
        }
    }
}
