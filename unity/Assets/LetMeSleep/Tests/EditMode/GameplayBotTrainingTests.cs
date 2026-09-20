using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;
namespace LetMeSleep.Tests
{
    public sealed class GameplayBotTrainingTests
    {
        private static ActorSnapshot Actor(uint id, PlayerRole role, Float3 position, LifeState life = LifeState.Active, Float3 velocity = default, uint recovery = 0, BiteAttachment? bite = null, string equipped = GameplayTools.Hands)
            => new ActorSnapshot(id,role,life,1,position,velocity,Rotation.Identity,Float3.Forward,0,0,1,1,true,0,0,null,bite,default,recovery,equipped);
        private static BotTarget Target(ActorSnapshot actor) => new BotTarget(actor,actor.Position);
        private static BotObservation Observe(ActorSnapshot self, BotTarget[] seen, BotTrainingContext context = null, ActorPrivateState own = null, string mode = GameModes.Blood)
            => new BotObservation(self,seen,Float3.Forward,false,null,mode,own,null,null,context);
        private static BotCommands Decide(BotController bot, BotObservation observation, uint tick, ulong round = 1) => bot.Decide(observation,new BotTick(1,round,tick));
        [Test] public void ReactionIsBoundedAndNeverImmediate()
        {
            for(uint id=1;id<=20;id++) Assert.That(BotController.ReactionTicks(1,id),Is.InRange(8u,12u));
            var bot=new BotController(); var self=Actor(1,PlayerRole.Human,Float3.Zero);
            var enemy=Target(Actor(2,PlayerRole.Mosquito,new Float3(0,1.53f,.1f),LifeState.Flying)); var o=Observe(self,new[]{enemy});
            var first=Decide(bot,o,10); Assert.That(first.Action.HasValue,Is.False); Assert.That(bot.SelectedActorId,Is.Zero);
            uint ready=10+BotController.ReactionTicks(1,2); Decide(bot,o,ready-1); Assert.That(bot.SelectedActorId,Is.Zero);
            Assert.That(Decide(bot,o,ready).Action.HasValue,Is.True); Assert.That(bot.SelectedActorId,Is.EqualTo(2));
        }
        [Test] public void HiddenMemoryKeepsLastPointButCannotAttackAndExpiresExactly()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var enemy=Target(Actor(2,PlayerRole.Mosquito,new Float3(3,1.53f,0),LifeState.Flying));
            Decide(bot,Observe(self,new[]{enemy}),0); Decide(bot,Observe(self,new[]{enemy}),12);
            var absent=Observe(self,Array.Empty<BotTarget>());
            var hidden=Decide(bot,absent,101); Assert.That(bot.SelectedActorId,Is.EqualTo(2)); Assert.That(hidden.Input.AimForward.X,Is.GreaterThan(.99f)); Assert.That(hidden.Action.HasValue,Is.False);
            var expired=Decide(bot,absent,102);Assert.That(bot.SelectedActorId,Is.Zero);Assert.That(expired.Input.AimForward.Z,Is.GreaterThan(.99f));
        }
        [Test] public void RuntimeThreeTickCadenceProducesThreeHundredToFourHundredMilliseconds()
        {
            for(uint id=2;id<22;id++)
            {
                var bot=new BotController();var o=Observe(Actor(1,PlayerRole.Human,Float3.Zero),new[]{Target(Actor(id,PlayerRole.Mosquito,Float3.Forward,LifeState.Flying))});
                uint reacted=0; for(uint t=0;t<=12;t+=3) { Decide(bot,o,t); if(bot.SelectedActorId!=0) { reacted=t;break; } }
                Assert.That(reacted,Is.InRange(9u,12u));
            }
        }
        [Test] public void NewRoundClearsMemoryAndReaction()
        {
            var bot=new BotController();var o=Observe(Actor(1,PlayerRole.Human,Float3.Zero),new[]{Target(Actor(2,PlayerRole.Mosquito,Float3.Forward,LifeState.Flying))});
            Decide(bot,o,0);Decide(bot,o,12);Assert.That(bot.SelectedActorId,Is.EqualTo(2));Decide(bot,o,13,2);Assert.That(bot.SelectedActorId,Is.Zero);
        }
        [Test] public void RecentlyBittenVictimPenaltyExpiresAfterEightSeconds()
        {
            var bot=new BotController();var bite=new BiteAttachment(2,1,default,Float3.Up,1);
            var attached=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying,bite:bite);
            var targets=new[]{Target(Actor(2,PlayerRole.Human,Float3.Forward)),Target(Actor(3,PlayerRole.Human,Float3.Forward*2))};
            Decide(bot,Observe(attached,targets),0);var free=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying);
            Decide(bot,Observe(free,targets),12);Assert.That(bot.SelectedActorId,Is.EqualTo(3));
            for(uint t=15;t<=237;t+=3) Decide(bot,Observe(free,targets),t);
            Decide(bot,Observe(free,targets),239);Decide(bot,Observe(free,targets),240);Assert.That(bot.SelectedActorId,Is.EqualTo(2));
        }
        [TestCase(44u,false)] [TestCase(45u,true)]
        public void RescueNeedsArrivalPlusOnePointFiveSeconds(uint remaining,bool expected)
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying);
            var target=Target(Actor(2,PlayerRole.Mosquito,Float3.Zero,LifeState.Stunned,recovery:12+remaining));
            var o=Observe(self,new[]{target},new BotTrainingContext(null,1));Decide(bot,o,0);var c=Decide(bot,o,12);Assert.That(c.Input.UseHeld,Is.EqualTo(expected));
        }
        [Test] public void RescueRejectedForVisibleNearbyHumanEvenBeforeItsReactionDelay()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying);
            var friend=Target(Actor(2,PlayerRole.Mosquito,Float3.Zero,LifeState.Stunned,recovery:300));Decide(bot,Observe(self,new[]{friend}),0);
            var c=Decide(bot,Observe(self,new[]{friend,Target(Actor(3,PlayerRole.Human,Float3.Forward*2))}),12);Assert.That(c.Input.UseHeld,Is.False);Assert.That(bot.SelectedActorId,Is.Not.EqualTo(2));
        }
        [Test] public void HiddenFriendCannotBeRescuedFromMemory()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying);var friend=Target(Actor(2,PlayerRole.Mosquito,Float3.Zero,LifeState.Stunned,recovery:300));
            Decide(bot,Observe(self,new[]{friend}),0);Decide(bot,Observe(self,new[]{friend}),12);Assert.That(Decide(bot,Observe(self,Array.Empty<BotTarget>()),13).Input.UseHeld,Is.False);
        }
        [Test] public void FiveSecondsStalledBlocksSixSecondsWithoutRepeatedReports()
        {
            var bot=new BotController();var o=Observe(Actor(1,PlayerRole.Human,Float3.Zero),Array.Empty<BotTarget>());
            Decide(bot,o,0);Decide(bot,o,149);Assert.That(bot.ReplanCount,Is.Zero);Assert.That(Decide(bot,o,150).Input.MovePlanar.Y,Is.Zero);
            Assert.That(bot.ReplanCount,Is.EqualTo(1));Assert.That(bot.BlockedUntilTick,Is.EqualTo(330));
            Assert.That(Decide(bot,o,329).Input.MovePlanar.Y,Is.Zero);Assert.That(bot.ReplanCount,Is.EqualTo(1));Assert.That(Decide(bot,o,330).Input.MovePlanar.Y,Is.GreaterThan(0));
        }
        [Test] public void SteeringReturningZeroStillCountsStalledIntent()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var o=new BotObservation(self,Array.Empty<BotTarget>(),Float3.Forward,false,_=>Float3.Zero);
            Decide(bot,o,0);Decide(bot,o,150);Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void ActualProgressRestartsStallTimer()
        {
            var bot=new BotController();Decide(bot,Observe(Actor(1,PlayerRole.Human,Float3.Zero),Array.Empty<BotTarget>()),0);
            var o=Observe(Actor(1,PlayerRole.Human,Float3.Forward*.2f),Array.Empty<BotTarget>());Decide(bot,o,149);Decide(bot,o,150);Assert.That(bot.ReplanCount,Is.Zero);
        }
        [TestCase(3.99f,true)] [TestCase(4f,false)]
        public void NearbyToolUsesStrictDetourAndOwnFreeSlot(float detour,bool pickup)
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var tool=new ToolPickupSnapshot(10,GameplayTools.Flyswatter,new Float3(0,1.53f,1),Rotation.Identity);
            var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default);
            var o=Observe(self,Array.Empty<BotTarget>(),new BotTrainingContext(new[]{new BotToolOpportunity(tool,detour)},1),own);
            Assert.That(Decide(bot,o,0).Action.HasValue,Is.EqualTo(pickup));
        }
        [Test] public void FullInventoryDoesNotSwapForTool()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default,1,1,0,null,new HumanInventorySnapshot(1,1,2,3,0),30000,false,default,null);
            var tool=new ToolPickupSnapshot(10,GameplayTools.Flyswatter,new Float3(0,1.53f,1),Rotation.Identity);
            Assert.That(Decide(bot,Observe(self,Array.Empty<BotTarget>(),new BotTrainingContext(new[]{new BotToolOpportunity(tool,1)},1),own),0).Action.HasValue,Is.False);
        }
        private static BotObservation TaskObservation(ActorSnapshot self, BotTarget[] seen, BotTrainingContext context = null)
        {
            var objective=new ObjectiveDefinition("test",ObjectiveKind.Clean,"task.test","task.action.hold_clean",Float3.Zero,Float3.Zero,1,30,"room");
            var assignment=new TaskAssignment("test",0,900,30,0,TaskAssignmentStatus.Active,0);
            var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default,1,1,0,assignment);
            return new BotObservation(self,seen,Float3.Forward,false,null,GameModes.Tasks,own,objective,_=>Float3.Zero,context);
        }
        [TestCase(1.99f,0f,false)] [TestCase(2f,0f,true)] [TestCase(4f,-1f,false)] [TestCase(4f,1f,true)]
        public void TaskDefenseMatchesDistanceOrApproachLiteral(float distance,float velocityZ,bool works)
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);
            var target=Target(Actor(2,PlayerRole.Mosquito,Float3.Forward*distance,LifeState.Flying,Float3.Forward*velocityZ));
            var o=TaskObservation(self,new[]{target});Decide(bot,o,0);Assert.That(Decide(bot,o,12).Input.UseHeld,Is.EqualTo(works));
        }
        [Test] public void ToolDoesNotInterruptTaskAlreadyBeingWorked()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var tool=new ToolPickupSnapshot(10,GameplayTools.Flyswatter,new Float3(0,1.53f,1),Rotation.Identity);
            var o=TaskObservation(self,Array.Empty<BotTarget>(),new BotTrainingContext(new[]{new BotToolOpportunity(tool,1)},1));
            var c=Decide(bot,o,0);Assert.That(c.Input.UseHeld,Is.True);Assert.That(c.Action.HasValue,Is.False);
        }
        [Test] public void DepletedToolIsNotPickedUp()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default);
            var tool=new ToolPickupSnapshot(10,GameplayTools.Aerosol,new Float3(0,1.53f,1),Rotation.Identity,0,1,ToolPickupPhase.World,default,0,0,false);
            Assert.That(Decide(bot,Observe(self,Array.Empty<BotTarget>(),new BotTrainingContext(new[]{new BotToolOpportunity(tool,1)},1),own),0).Action.HasValue,Is.False);
        }
        private static BotObservation EquippedObservation(string tool, ThrowChargeSnapshot charge = default, bool visible = true,
            uint owner = 1, uint pickupId = 10, int resource = 120, int stamina = 30000, uint cooldown = 0, uint inventoryRevision = 7, uint pickupRevision = 11)
        {
            var self=Actor(1,PlayerRole.Human,Float3.Zero,equipped:tool);
            var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default,1,1,0,null,new HumanInventorySnapshot(inventoryRevision,10,0,0,0),stamina,false,charge,null);
            var pickup=new ToolPickupSnapshot(pickupId,tool,Float3.Zero,Rotation.Identity,owner,pickupRevision,ToolPickupPhase.Held,default,0,resource,false,cooldown);
            return Observe(self,visible?new[]{Target(Actor(2,PlayerRole.Mosquito,new Float3(0,1.53f,.5f),LifeState.Flying))}:Array.Empty<BotTarget>(),new BotTrainingContext(null,1,pickup),own);
        }
        [Test] public void AerosolHoldsPrimaryOnlyForVisibleTargetAndUsableOwnedResource()
        {
            var bot=new BotController();var o=EquippedObservation(GameplayTools.Aerosol);Decide(bot,o,0);
            var held=Decide(bot,o,12);Assert.That(held.Input.PrimaryHeld,Is.True);Assert.That(held.Action.HasValue,Is.False);
            Assert.That(Decide(bot,EquippedObservation(GameplayTools.Aerosol,visible:false),15).Input.PrimaryHeld,Is.False);
            Assert.That(Decide(bot,EquippedObservation(GameplayTools.Aerosol,resource:0),18).Input.PrimaryHeld,Is.False);
            Assert.That(Decide(bot,EquippedObservation(GameplayTools.Aerosol,owner:3),21).Input.PrimaryHeld,Is.False);
        }
        [Test] public void SlipperBeginAndReleaseUseExactCurrentInventoryAndPickupRevision()
        {
            var bot=new BotController();var o=EquippedObservation(GameplayTools.Slipper);Decide(bot,o,0);
            var begin=Decide(bot,o,12);Assert.That(begin.Input.PrimaryHeld,Is.True);Assert.That(begin.Action.Value.Kind,Is.EqualTo(ActionKind.BeginThrow));
            Assert.That(begin.Action.Value.SlotIndex,Is.EqualTo(0));Assert.That(begin.Action.Value.TargetPickupId,Is.EqualTo(10));Assert.That(begin.Action.Value.ExpectedPickupRevision,Is.EqualTo(11));Assert.That(begin.Action.Value.InventoryRevision,Is.EqualTo(7));
            var hold=Decide(bot,EquippedObservation(GameplayTools.Slipper,new ThrowChargeSnapshot(10,7,26)),38);
            Assert.That(hold.Input.PrimaryHeld,Is.True);Assert.That(hold.Action.HasValue,Is.False);
            var release=Decide(bot,EquippedObservation(GameplayTools.Slipper,new ThrowChargeSnapshot(10,7,27)),39);
            Assert.That(release.Input.PrimaryHeld,Is.False);Assert.That(release.Action.Value.Kind,Is.EqualTo(ActionKind.ReleaseThrow));Assert.That(release.Action.Value.InventoryRevision,Is.EqualTo(7));Assert.That(release.Action.Value.ExpectedPickupRevision,Is.EqualTo(11));
        }
        [Test] public void SlipperLosingSightCancelsWithNeutralInputInsteadOfReleasingAtMemory()
        {
            var bot=new BotController();var o=EquippedObservation(GameplayTools.Slipper);Decide(bot,o,0);Decide(bot,o,12);
            var c=Decide(bot,EquippedObservation(GameplayTools.Slipper,new ThrowChargeSnapshot(10,7,27),visible:false),39);
            Assert.That(c.Input.PrimaryHeld,Is.False);Assert.That(c.Action.Value.Kind,Is.EqualTo(ActionKind.CancelThrow));Assert.That(c.Action.Value.TargetPickupId,Is.Zero);
        }
        [TestCase(3u,10u)] [TestCase(1u,99u)]
        public void SlipperCannotStartWithWrongOwnerOrPickup(uint owner,uint pickup)
        {
            var bot=new BotController();var o=EquippedObservation(GameplayTools.Slipper,owner:owner,pickupId:pickup);Decide(bot,o,0);
            var c=Decide(bot,o,12);Assert.That(c.Input.PrimaryHeld,Is.False);Assert.That(c.Action.HasValue,Is.False);
        }
        [Test] public void SlipperDoesNotStartWithoutFullChargeStaminaOrRenewActiveCharge()
        {
            var bot=new BotController();var low=EquippedObservation(GameplayTools.Slipper,stamina:4499);Decide(bot,low,0);Assert.That(Decide(bot,low,12).Action.HasValue,Is.False);
            var active=EquippedObservation(GameplayTools.Slipper,new ThrowChargeSnapshot(10,7,10));Assert.That(Decide(bot,active,15).Action.HasValue,Is.False);
            var mismatched=EquippedObservation(GameplayTools.Slipper,new ThrowChargeSnapshot(10,6,27));Assert.That(Decide(bot,mismatched,18).Action.Value.Kind,Is.EqualTo(ActionKind.CancelThrow));
        }
        [Test] public void RacketHonorsPublicResourceAndCooldown()
        {
            var bot=new BotController();var cooling=EquippedObservation(GameplayTools.ElectricRacket,cooldown:20);Decide(bot,cooling,0);Assert.That(Decide(bot,cooling,12).Action.HasValue,Is.False);
            Assert.That(Decide(bot,cooling,21).Action.Value.Kind,Is.EqualTo(ActionKind.Primary));
            Assert.That(Decide(bot,EquippedObservation(GameplayTools.ElectricRacket,resource:0),48).Action.Value.Kind,Is.EqualTo(ActionKind.SelectInventorySlot));
        }
        [TestCase(GameplayTools.Aerosol)] [TestCase(GameplayTools.ElectricRacket)]
        public void ExhaustedToolSelectsHandsOncePerObservedRevision(string tool)
        {
            var bot=new BotController();var o=EquippedObservation(tool,resource:0);
            var select=Decide(bot,o,0);Assert.That(select.Action.Value.Kind,Is.EqualTo(ActionKind.SelectInventorySlot));
            Assert.That(select.Action.Value.SlotIndex,Is.EqualTo(-1));Assert.That(select.Action.Value.InventoryRevision,Is.EqualTo(7));
            Assert.That(select.Action.Value.TargetPickupId,Is.Zero);Assert.That(select.Action.Value.ExpectedPickupRevision,Is.Zero);Assert.That(select.Input.PrimaryHeld,Is.False);
            for(uint t=3;t<=60;t+=3) Assert.That(Decide(bot,o,t).Action.HasValue,Is.False);
            var updated=EquippedObservation(tool,resource:0,inventoryRevision:8);
            Assert.That(Decide(bot,updated,63).Action.Value.InventoryRevision,Is.EqualTo(8));
        }
        [Test] public void ToolAimAndReachUseObservedColliderPointInsteadOfOffsetRoot()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Human,Float3.Zero);
            var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default);
            var pickup=new ToolPickupSnapshot(10,GameplayTools.Flyswatter,new Float3(10,0,0),Rotation.Identity);
            var contact=new Float3(0,1.53f,1);
            var observation=Observe(self,Array.Empty<BotTarget>(),new BotTrainingContext(new[]{new BotToolOpportunity(pickup,1,contact)},1),own);
            var command=Decide(bot,observation,0);
            Assert.That(command.Action.Value.Kind,Is.EqualTo(ActionKind.Use));
            Assert.That((command.Action.Value.AimForward-Float3.Forward).Length,Is.LessThan(.00001));
            Assert.That(command.Input.MovePlanar.Y,Is.Zero);
            Assert.That((new BotToolOpportunity(pickup,1).ContactPoint-pickup.Position).Length,Is.Zero,"Old constructor preserves root-point semantics.");
        }
        [Test] public void SurvivalEvadesAfterReactionAndNeverBites()
        {
            var bot=new BotController();var self=Actor(1,PlayerRole.Mosquito,Float3.Zero,LifeState.Flying);var o=Observe(self,new[]{Target(Actor(2,PlayerRole.Human,Float3.Forward))},mode:GameModes.Survival);
            Decide(bot,o,0);var c=Decide(bot,o,12);Assert.That(c.Input.AimForward.Z,Is.LessThan(0));Assert.That(c.Input.BiteHeld,Is.False);
        }
    }
}
