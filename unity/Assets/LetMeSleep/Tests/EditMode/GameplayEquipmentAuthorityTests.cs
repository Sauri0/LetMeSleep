using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class GameplayEquipmentAuthorityTests
    {
        sealed class World : IGameplayWorld, IGameplayEquipmentWorld, IGameplayToolEffectWorld
        {
            public readonly Dictionary<uint,ToolPickupSnapshot> Items=new Dictionary<uint,ToolPickupSnapshot>();
            public readonly Queue<ToolProjectileHit> Hits=new Queue<ToolProjectileHit>();
            public uint Candidate=1;
            public bool Deposit=true,Prepare=true,Grounded=true;
            public bool FlatFloor;
            public int Prepared,Sweeps;
            public MotorQuery LastHuman;
            public bool EffectOriginValid=true,PlanStrike;
            public bool BugGrounded,RecoverySafe;
            public readonly List<ToolEffectHit> EffectHits=new List<ToolEffectHit>();
            public readonly List<ToolEffectQuery> EffectQueries=new List<ToolEffectQuery>();
            public void BeginRound(IReadOnlyList<SpawnActor> a,IReadOnlyList<DoorDefinition> d){}
            public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a){}
            public MotorResult MoveHuman(in MotorQuery q){if(q.ActorId==1)LastHuman=q;var position=q.Position+q.Velocity/30;var velocity=q.Velocity;if(FlatFloor&&Grounded){position=new Float3(position.X,0,position.Z);velocity=new Float3(velocity.X,0,velocity.Z);}return new MotorResult(position,velocity,Grounded,Float3.Up,q.CrouchFraction);}
            public MotorResult MoveMosquito(in MotorQuery q)=>new MotorResult(q.Position,q.Velocity,BugGrounded,Float3.Up,0);
            public bool TrySurface(in SurfaceQuery q,out SurfaceContact c){c=default;return false;}
            public bool ResolveSurface(in SurfaceAttachment a,out SurfaceContact c){c=default;return false;}
            public bool TryBiteContact(in BiteQuery q,out BiteContact c){c=default;return false;}
            public bool ResolveBite(uint id,in BiteAttachment b,int humans,out BiteContact c){c=default;return false;}
            public bool TryPlanStrike(uint id,Float3 aim,string tool,out StrikePlan p){p=new StrikePlan(Float3.Zero,Float3.Forward,Float3.Up,.075f,1,tool);return PlanStrike;}
            public StrikeHit SweepStrike(in StrikeSweep q)=>default;
            public bool TryFreeRecoveryPoint(uint id,Float3 p,out Float3 point){point=p;return RecoverySafe;}
            public bool HasLineOfSight(uint id,Float3 p,uint other,Float3 end)=>true;
            public bool TryDoorInteraction(in DoorInteractionQuery q,out DoorInteractionCandidate c){c=default;return false;}
            public DoorSweepResult SweepDoor(in DoorMotionQuery q)=>new DoorSweepResult(q.ToAngleRadians,false);
            public void ApplyDoorPose(in DoorPose p){}
            public void BeginTools(IReadOnlyList<ToolPickupDefinition> d){Items.Clear();}
            public bool TryToolInteraction(in ToolInteractionQuery q,out ToolInteractionCandidate c)
            {if(Items.TryGetValue(Candidate,out var item)){c=new ToolInteractionCandidate(Candidate,item.Revision,1);return true;}c=default;return false;}
            public bool TryDropTool(uint actor,out Float3 p,out Rotation r)=>TryDropTool(actor,0,out p,out r);
            public bool TryDropTool(uint actor,uint pickup,out Float3 p,out Rotation r){p=new Float3(.8f,0,0);r=Rotation.Identity;return Deposit;}
            public void ApplyToolState(in ToolPickupSnapshot s)=>Items[s.PickupId]=s;
            public bool TryPrepareThrow(uint actor,uint pickup,Float3 aim,float power,out Float3 p,out Rotation r,out Float3 v)
            {Prepared++;p=new Float3(0,1.5f,.5f);r=Rotation.Identity;v=aim*(6+8*power)+Float3.Up*1.5f;return Prepare;}
            public bool SweepProjectile(in ToolProjectileQuery q,out ToolProjectileHit h){Sweeps++;if(Hits.Count>0){h=Hits.Dequeue();return true;}h=default;return false;}
            public bool TryToolEffectOrigin(uint actor,uint pickup,Float3 aim,out Float3 origin,out Float3 forward)
            {origin=new Float3(0,1.5f,0);forward=aim;return EffectOriginValid;}
            public IReadOnlyList<ToolEffectHit> QueryToolEffect(in ToolEffectQuery query){EffectQueries.Add(query);return EffectHits.ToArray();}
        }
        sealed class Session
        {
            public readonly World World=new World();public readonly GameplayAuthority Host;
            private uint input,action;
            public Session(string firstTool=GameplayTools.Slipper,string mode=GameModes.Blood)
            {
                Host=new GameplayAuthority(World);
                var definitions=new[]{new ToolPickupDefinition(1,firstTool,Float3.Zero,Rotation.Identity),new ToolPickupDefinition(2,GameplayTools.Flyswatter,Float3.Forward,Rotation.Identity),new ToolPickupDefinition(3,GameplayTools.ElectricRacket,new Float3(2,0,0),Rotation.Identity),new ToolPickupDefinition(4,GameplayTools.Aerosol,new Float3(3,0,0),Rotation.Identity)};
                Host.BeginRound(new GameplayRoundConfig(1,1,"map","hash",tools:definitions,modeId:mode),new[]{new SpawnActor(1,"human",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"bug",PlayerRole.Mosquito,new Float3(0,1,2)),new SpawnActor(3,"other",PlayerRole.Human,new Float3(3,0,0))});
            }
            public ActorPrivateState Private=>Host.CapturePrivate(1);
            CommandHeader Header(uint seq)=>new CommandHeader(1,1,1,seq,Host.CurrentTick,Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).ViewRevision);
            public void Input(bool primary=false,bool sprint=false,bool move=false)
            {Assert.That(Host.SubmitInput("human",new PlayerInputCommand(Header(++input),move?new Float2(0,1):default,0,0,0,Float3.Forward,sprint,false,false,false,primary)),Is.EqualTo(CommandReject.None));}
            public PlayerActionCommand Act(ActionKind kind,bool payload=false,int? slot=null)
            {
                var state=Private;uint item=state.Inventory.ActivePickup;
                uint revision=item==0?0:World.Items[item].Revision;
                var command=payload?new PlayerActionCommand(Header(++action),kind,Float3.Forward,slot??state.Inventory.SelectedSlot,kind==ActionKind.SelectInventorySlot?0:item,kind==ActionKind.SelectInventorySlot?0:revision,state.Inventory.Revision):new PlayerActionCommand(Header(++action),kind,Float3.Forward);
                Assert.That(Host.SubmitAction("human",command),Is.EqualTo(CommandReject.None));return command;
            }
            public void Tick(bool primary=false,bool sprint=false,bool move=false){Input(primary,sprint,move);Host.Advance(new HostTick(Host.CurrentTick+1));}
            public void Pickup(uint id){World.Candidate=id;Act(ActionKind.Use);Tick();}
            public void BeginCharge(){Input(true);Act(ActionKind.BeginThrow,true);Tick(true);Assert.That(Private.ThrowCharge.Active,Is.True);Assert.That(Private.ThrowCharge.ElapsedTicks,Is.Zero);}
            public void Confirm()
            {
                var o=Private.SwapOffer.Value;
                var c=new PlayerActionCommand(Header(++action),ActionKind.ConfirmPickup,Float3.Forward,o.SlotIndex,o.PickupId,o.PickupRevision,o.InventoryRevision);
                Assert.That(Host.SubmitAction("human",c),Is.EqualTo(CommandReject.None));Tick();
            }
        }
        [TestCase(GameplayTools.Aerosol)] [TestCase(GameplayTools.ElectricRacket)]
        public void TrainingBotCollectsUsesExhaustsAndSelectsHandsThroughAuthority(string tool)
        {
            var world=new World { FlatFloor=true };var authority=new GameplayAuthority(world);var bot=new BotController();
            authority.BeginRound(new GameplayRoundConfig(1,1,"map","hash",tools:new[]{new ToolPickupDefinition(1,tool,new Float3(0,.5f,0),Rotation.Identity)}),
                new[]{new SpawnActor(1,"human",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"bug",PlayerRole.Mosquito,new Float3(0,1.53f,.5f))});
            bool picked=false,used=false,selectedHands=false;uint selectionSequence=0;
            for(int step=0;step<240&&!selectedHands;step++)
            {
                if(authority.CurrentTick%3==0)
                {
                    var snapshot=authority.CaptureSnapshot();var self=snapshot.Actors.Single(a=>a.ActorId==1);var bug=snapshot.Actors.Single(a=>a.ActorId==2);
                    var own=authority.CapturePrivate(1);var item=world.Items[1];
                    var tools=item.Phase==ToolPickupPhase.World?new[]{new BotToolOpportunity(item,1)}:Array.Empty<BotToolOpportunity>();
                    var context=new BotTrainingContext(tools,1,item.Phase==ToolPickupPhase.Held?(ToolPickupSnapshot?)item:null);
                    var visible=picked?new[]{new BotTarget(bug,bug.Position)}:Array.Empty<BotTarget>();
                    var observation=new BotObservation(self,visible,Float3.Forward,false,null,GameModes.Blood,own,null,null,context);
                    var commands=bot.Decide(observation,new BotTick(1,1,authority.CurrentTick));
                    Assert.That(authority.SubmitInput("human",commands.Input),Is.EqualTo(CommandReject.None));
                    if(commands.Action.HasValue)
                    {
                        Assert.That(authority.SubmitAction("human",commands.Action.Value),Is.EqualTo(CommandReject.None));
                        if(commands.Action.Value.Kind==ActionKind.SelectInventorySlot) selectionSequence=commands.Action.Value.Header.Sequence;
                    }
                }
                authority.Advance(new HostTick(authority.CurrentTick+1));
                picked|=world.Items[1].OwnerActorId==1;
                used|=world.Items[1].ResourceUnits<GameplayTools.InitialResourceUnits(tool);
                var state=authority.CapturePrivate(1);
                selectedHands=picked&&used&&world.Items[1].ResourceUnits==0&&state.Inventory.SelectedSlot==-1;
            }
            Assert.That(picked,Is.True,"pickup");Assert.That(used,Is.True,"use: "+authority.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).EquippedToolId+" resource="+world.Items[1].ResourceUnits);Assert.That(selectedHands,Is.True,"hands: resource="+world.Items[1].ResourceUnits+" selected="+authority.CapturePrivate(1).Inventory.SelectedSlot);
            Assert.That(authority.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).EquippedToolId,Is.EqualTo(GameplayTools.Hands));
            Assert.That(authority.CapturePrivate(1).LastAcceptedActionSequence,Is.EqualTo(selectionSequence));
            Assert.That(authority.CapturePrivate(1).StaminaUnits,Is.EqualTo(HumanEquipmentProfile.Maximum));
            Assert.That(authority.CapturePrivate(1).ThrowCharge.Active,Is.False);
            Assert.That(world.Items[1].OwnerActorId,Is.EqualTo(1));Assert.That(world.Items[1].ResourceUnits,Is.Zero);
            Assert.That(authority.CapturePrivate(1).Inventory.Slot0,Is.EqualTo(1),"The empty object remains owned; selection does not refill or drop it.");
        }
        [Test] public void HumanSprintConsumesButMosquitoPrivateStateHasNoHumanInventory()
        {
            var s=new Session();for(int i=0;i<30;i++)s.Tick(sprint:true,move:true);
            Assert.That(s.Private.StaminaUnits,Is.EqualTo(25200));Assert.That(s.World.LastHuman.Velocity.Z,Is.EqualTo(5).Within(.0001));
            var bug=s.Host.CapturePrivate(2);Assert.That(bug.StaminaUnits,Is.Zero);Assert.That(bug.Inventory.SelectedSlot,Is.EqualTo(-1));
        }
        [Test] public void GroundedJumpCostsOnceAndAirJumpIsFreeRejection()
        {
            var s=new Session();s.Tick();s.Act(ActionKind.Jump);s.Act(ActionKind.Jump);s.Tick();
            Assert.That(s.Private.StaminaUnits,Is.EqualTo(27000));
            s.World.Grounded=false;s.Tick();int before=s.Private.StaminaUnits;s.Act(ActionKind.Jump);s.Tick();
            Assert.That(s.Private.StaminaUnits,Is.GreaterThanOrEqualTo(before));Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.InvalidState));
        }
        [Test] public void ThreeSlotsSwapOnlyAfterConfirmationAndSafeDeposit()
        {
            var s=new Session();s.Pickup(1);s.Pickup(2);s.Pickup(3);s.Pickup(4);
            Assert.That(s.Private.SwapOffer.HasValue,Is.True);Assert.That(s.Private.Inventory.Slot2,Is.EqualTo(3));
            s.World.Deposit=false;s.Confirm();Assert.That(s.Private.Inventory.Slot2,Is.EqualTo(3));Assert.That(s.World.Items[4].OwnerActorId,Is.Zero);
            s.World.Deposit=true;s.Confirm();Assert.That(s.Private.Inventory.Slot2,Is.EqualTo(4));Assert.That(s.World.Items[3].OwnerActorId,Is.Zero);Assert.That(s.World.Items[3].ResourceUnits,Is.EqualTo(5));
        }
        [Test] public void AnotherHumanCannotDuplicateAnOwnedPickup()
        {
            var s=new Session();s.Pickup(1);
            var c=new PlayerActionCommand(new CommandHeader(1,1,3,1,s.Host.CurrentTick,1),ActionKind.Use,Float3.Forward);
            Assert.That(s.Host.SubmitAction("other",c),Is.EqualTo(CommandReject.None));s.Tick();
            Assert.That(s.World.Items[1].OwnerActorId,Is.EqualTo(1));Assert.That(s.Host.CapturePrivate(3).Inventory.ActivePickup,Is.Zero);
        }
        [Test] public void ExplicitReleaseConsumesChargeOnceAndReusesPickupIdentity()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();for(int i=0;i<27;i++)s.Tick(true);
            var command=s.Act(ActionKind.ReleaseThrow,true);s.Tick(true);
            Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.Projectile));Assert.That(s.Private.StaminaUnits,Is.EqualTo(25500));Assert.That(s.Private.Inventory.ActivePickup,Is.Zero);
            Assert.That(s.Host.SubmitAction("human",command),Is.EqualTo(CommandReject.None));s.Tick();
            Assert.That(s.World.Prepared,Is.EqualTo(1));Assert.That(s.World.Items.Count,Is.EqualTo(4));
        }
        [Test] public void AutoReleaseOccursAtFortyFiveHostTicksNotBeginTick()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();for(int i=0;i<44;i++)s.Tick(true);
            Assert.That(s.Private.ThrowCharge.ElapsedTicks,Is.EqualTo(44));Assert.That(s.World.Prepared,Is.Zero);
            s.Tick(true);Assert.That(s.World.Prepared,Is.EqualTo(1));Assert.That(s.Private.StaminaUnits,Is.EqualTo(25500));
        }
        [Test] public void QuickBeginAndReleaseSameBatchWithNeutralThrowsAtMinimumPower()
        {
            var s=new Session();s.Pickup(1);s.Input(false);s.Act(ActionKind.BeginThrow,true);s.Act(ActionKind.ReleaseThrow,true);s.Tick(false);
            Assert.That(s.World.Prepared,Is.EqualTo(1));Assert.That(s.Private.StaminaUnits,Is.EqualTo(27600));
            Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.Projectile));
        }
        [Test] public void ReliableBeginOvertakingHeldInputStaysFrozenWithoutAutoRelease()
        {
            var s=new Session();s.Pickup(1);s.Input(false);s.Act(ActionKind.BeginThrow,true);s.Tick(false);
            Assert.That(s.Private.ThrowCharge.Active,Is.True);Assert.That(s.Private.ThrowCharge.AwaitingRelease,Is.True);
            for(int i=0;i<29;i++)s.Tick(true);
            Assert.That(s.Private.ThrowCharge.ElapsedTicks,Is.Zero);Assert.That(s.World.Prepared,Is.Zero);
            s.Tick(true);Assert.That(s.Private.ThrowCharge.Active,Is.False);
        }
        [Test] public void NeutralBeforeFocusCancelCannotAutoThrow()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();for(int i=0;i<44;i++)s.Tick(true);
            s.Input(false);s.Act(ActionKind.CancelThrow);s.Tick(false);
            Assert.That(s.World.Prepared,Is.Zero);Assert.That(s.Private.Inventory.ActivePickup,Is.EqualTo(1));Assert.That(s.Private.StaminaUnits,Is.EqualTo(30000));
        }
        [Test] public void NeutralFreezesPowerUntilExplicitReleaseAndOrphanTimesOut()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();for(int i=0;i<10;i++)s.Tick(true);
            s.Tick(false);for(int i=0;i<10;i++)s.Tick(false);
            Assert.That(s.Private.ThrowCharge.ElapsedTicks,Is.EqualTo(10));s.Act(ActionKind.ReleaseThrow,true);s.Tick(false);
            Assert.That(s.Private.StaminaUnits,Is.EqualTo(30000-HumanEquipmentProfile.ThrowCost(10)));
            var orphan=new Session();orphan.Pickup(1);orphan.BeginCharge();for(int i=0;i<30;i++)orphan.Tick(false);
            Assert.That(orphan.Private.ThrowCharge.Active,Is.False);Assert.That(orphan.World.Prepared,Is.Zero);
        }
        [Test] public void StaleInputAndDisconnectCancelChargeWithoutLosingInventory()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();for(int i=0;i<7;i++)s.Host.Advance(new HostTick(s.Host.CurrentTick+1));
            Assert.That(s.Private.ThrowCharge.Active,Is.False);Assert.That(s.World.Prepared,Is.Zero);
            s.BeginCharge();s.Host.SetActorConnected(1,false);Assert.That(s.Private.ThrowCharge.Active,Is.False);Assert.That(s.Private.Inventory.ActivePickup,Is.EqualTo(1));
            s.Host.SetActorConnected(1,true);Assert.That(s.Private.Inventory.ActivePickup,Is.EqualTo(1));Assert.That(s.Private.StaminaUnits,Is.EqualTo(30000));
        }
        [Test] public void InsufficientReleaseKeepsItemAndDoesNotPrepareProjectile()
        {
            var s=new Session();s.Pickup(1);for(int i=0;i<188;i++)s.Tick(sprint:true,move:true);
            s.BeginCharge();int before=s.Private.StaminaUnits;s.Act(ActionKind.ReleaseThrow,true);s.Tick();
            Assert.That(s.Private.Inventory.ActivePickup,Is.EqualTo(1));Assert.That(s.World.Prepared,Is.Zero);Assert.That(s.Private.StaminaUnits,Is.GreaterThanOrEqualTo(before));
        }
        [Test] public void FirstWallHitConsumesDamageAndKeepsFallingUntilSupport()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();s.World.Hits.Enqueue(new ToolProjectileHit(0,new Float3(0,1,.6f),new Float3(1,0,0),.5f));
            s.Act(ActionKind.ReleaseThrow,true);s.Tick();Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.Projectile));Assert.That(s.World.Items[1].ImpactConsumed,Is.True);
            s.World.Hits.Enqueue(new ToolProjectileHit(2,new Float3(0,0,.6f),Float3.Up,.5f));s.Tick();
            Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.Projectile));Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Flying));
            s.World.Hits.Enqueue(new ToolProjectileHit(0,new Float3(0,0,.6f),Float3.Up,.5f));s.Tick();
            Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.World));
        }
        [Test] public void ActorHeadIsNotAStaticRestingPlaceForPickup()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();s.World.Hits.Enqueue(new ToolProjectileHit(3,new Float3(0,1.8f,.6f),Float3.Up,.5f));
            s.Act(ActionKind.ReleaseThrow,true);s.Tick();
            Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.Projectile));Assert.That(s.World.Items[1].ImpactConsumed,Is.True);
            s.Tick();Assert.That(s.World.Items[1].Velocity.Y,Is.LessThan(0));
        }
        [Test] public void InitialOverlapRecoversWithoutNewDamage()
        {
            var s=new Session();s.Pickup(1);s.BeginCharge();s.World.Hits.Enqueue(new ToolProjectileHit(2,new Float3(9,9,9),Float3.Up,0,true));
            s.Act(ActionKind.ReleaseThrow,true);s.Tick();Assert.That(s.World.Items[1].Phase,Is.EqualTo(ToolPickupPhase.World));Assert.That(s.World.Items[1].Position.Length,Is.Zero);
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Flying));
        }
        [Test] public void RemovingHumanDepositsEverySlotWithoutRechargingResources()
        {
            var s=new Session();s.Pickup(1);s.Pickup(2);s.Pickup(3);s.World.Deposit=false;s.Host.RemoveActor(1,ActorRemovalReason.Left);
            Assert.That(s.World.Items.Values.All(i=>i.OwnerActorId==0),Is.True);Assert.That(s.World.Items[3].ResourceUnits,Is.EqualTo(5));
        }
        [Test] public void RacketUsesExactHalfTickLifetimeAndThirtySixTickCooldown()
        {
            var s=new Session(GameplayTools.ElectricRacket);s.Pickup(1);s.Act(ActionKind.Primary);s.Tick();
            uint start=s.Host.CurrentTick;var effect=s.Host.CaptureSnapshot().ToolEffects.Single();
            Assert.That(effect.EndHalfTick,Is.EqualTo(2*start+21));Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(4));
            Assert.That(s.World.Items[1].CooldownUntilTick,Is.EqualTo(start+36));Assert.That(s.Private.StaminaUnits,Is.EqualTo(30000));
            for(int i=0;i<10;i++)s.Tick();Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
            s.Tick();Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
            s.Act(ActionKind.Primary);s.Tick();Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.Cooldown));Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(4));
            while(s.Host.CurrentTick<start+35)s.Tick();s.Act(ActionKind.Primary);s.Tick();Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(3));
        }
        [Test] public void RacketDropAndPickupPreserveCooldownAndRemainingCharges()
        {
            var s=new Session(GameplayTools.ElectricRacket);s.Pickup(1);s.Act(ActionKind.Primary);s.Tick();uint cooldown=s.World.Items[1].CooldownUntilTick;
            s.Act(ActionKind.DropTool);s.Tick();Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
            s.Pickup(1);Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(4));Assert.That(s.World.Items[1].CooldownUntilTick,Is.EqualTo(cooldown));
            s.Act(ActionKind.Primary);s.Tick();Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(4));
        }
        [Test] public void ObstructedEffectOriginDoesNotConsumeBatteryOrFuel()
        {
            var racket=new Session(GameplayTools.ElectricRacket);racket.Pickup(1);racket.World.EffectOriginValid=false;racket.Act(ActionKind.Primary);racket.Tick();
            Assert.That(racket.World.Items[1].ResourceUnits,Is.EqualTo(5));Assert.That(racket.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
            var spray=new Session(GameplayTools.Aerosol);spray.Pickup(1);spray.World.EffectOriginValid=false;spray.Tick(true);
            Assert.That(spray.World.Items[1].ResourceUnits,Is.EqualTo(120));Assert.That(spray.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
        }
        [Test] public void AerosolConsumesFourSecondsAndCloudPersistsThirtySixTicks()
        {
            var s=new Session(GameplayTools.Aerosol);s.Pickup(1);
            for(int i=0;i<120;i++)s.Tick(true);
            Assert.That(s.World.Items[1].ResourceUnits,Is.Zero);Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
            uint last=s.Host.CurrentTick;Assert.That(s.Host.CaptureSnapshot().ToolEffects.Single().EndHalfTick,Is.EqualTo(2*last+72));
            for(int i=0;i<35;i++)s.Tick(false);Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
            s.Tick(false);Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);Assert.That(s.World.Items[1].ResourceUnits,Is.Zero);
        }
        [Test] public void AerosolOneCloudPerPickupAndDepositDoesNotRefill()
        {
            var s=new Session(GameplayTools.Aerosol);s.Pickup(1);s.Tick(true);uint effect=s.Host.CaptureSnapshot().ToolEffects.Single().EffectId;
            for(int i=0;i<9;i++)s.Tick(true);
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Single().EffectId,Is.EqualTo(effect));Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(110));
            s.Act(ActionKind.DropTool);s.Tick(false);Assert.That(s.Host.CaptureSnapshot().ToolEffects.Single().EffectId,Is.EqualTo(effect));
            s.Pickup(1);Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(110));
        }
        [Test] public void CancelStopsEmissionEvenWhenNeutralPacketHasNotArrived()
        {
            var s=new Session(GameplayTools.Aerosol);s.Pickup(1);s.Tick(true);int fuel=s.World.Items[1].ResourceUnits;
            s.Act(ActionKind.CancelThrow);s.Host.Advance(new HostTick(s.Host.CurrentTick+1));
            Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(fuel));Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
        }
        [Test] public void WeaponEffectKnocksDownMosquitoOnlyOnceAndNeverHuman()
        {
            var s=new Session(GameplayTools.ElectricRacket);s.Pickup(1);
            var hit=new ToolEffectHit(2,new Float3(0,1.5f,.5f),Float3.Up);s.World.EffectHits.Add(hit);s.World.EffectHits.Add(hit);
            s.World.EffectHits.Add(new ToolEffectHit(3,new Float3(0,1.5f,.5f),Float3.Up));s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Falling));
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==3).LifeState,Is.EqualTo(LifeState.Active));
            Assert.That(s.Host.DrainEvents().Count(e=>e.Kind==GameplayEventKind.MosquitoKnockedDown),Is.EqualTo(1));
        }
        [Test] public void SwatterReachAndStrikeWindowScaleFromHandsExactlyOnce()
        {
            Assert.That(GameplayTools.FlyswatterShoulderReach/HumanEquipmentProfile.HandsReach,Is.EqualTo(1.35f).Within(.00001));
            var hands=new Session();hands.World.PlanStrike=true;hands.Act(ActionKind.Primary);hands.Tick();uint handStart=hands.Host.CurrentTick;
            while(hands.Host.CurrentTick<handStart+18)hands.Tick();Assert.That(hands.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).StrikeState.Phase,Is.EqualTo(StrikePhase.None));
            var swatter=new Session(GameplayTools.Flyswatter);swatter.Pickup(1);swatter.World.PlanStrike=true;swatter.Act(ActionKind.Primary);swatter.Tick();uint toolStart=swatter.Host.CurrentTick;
            while(swatter.Host.CurrentTick<toolStart+22)swatter.Tick();Assert.That(swatter.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).StrikeState.Phase,Is.EqualTo(StrikePhase.Recovery));
            swatter.Tick();Assert.That(swatter.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).StrikeState.Phase,Is.EqualTo(StrikePhase.None));
        }
        [Test] public void RacketHasFivePulsesAndCannotFireWhenEmpty()
        {
            var s=new Session(GameplayTools.ElectricRacket);s.Pickup(1);
            for(int shot=0;shot<5;shot++)
            {
                s.Act(ActionKind.Primary);s.Tick();Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(4-shot));
                for(int i=0;i<35;i++)s.Tick();
            }
            s.Act(ActionKind.Primary);s.Tick();Assert.That(s.World.Items[1].ResourceUnits,Is.Zero);Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.InvalidState));
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
        }
        [Test] public void RecoveredMosquitoProtectionRejectsFreshWeaponPulse()
        {
            var s=new Session(GameplayTools.ElectricRacket);s.Pickup(1);s.World.BugGrounded=true;s.World.RecoverySafe=true;
            s.World.EffectHits.Add(new ToolEffectHit(2,new Float3(0,1.5f,.5f),Float3.Up));s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Falling));
            int guard=0;while(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState!=LifeState.Flying&&guard++<1000)s.Tick();
            Assert.That(guard,Is.LessThan(1000));s.Host.DrainEvents();s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Flying));
            Assert.That(s.Host.DrainEvents().Any(e=>e.Kind==GameplayEventKind.MosquitoKnockedDown),Is.False);
        }
        [Test] public void WeaponEffectUsesSurvivalEliminationAndClosesRoundNormally()
        {
            var s=new Session(GameplayTools.ElectricRacket,GameModes.Survival);s.Pickup(1);
            s.World.EffectHits.Add(new ToolEffectHit(2,new Float3(0,1.5f,.5f),Float3.Up));s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==2).LifeState,Is.EqualTo(LifeState.Eliminated));
            Assert.That(s.Host.CaptureSnapshot().Result,Is.EqualTo(RoundEndReason.AllOpponentsEliminated));
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
        }
        [Test] public void RemovalClearsAerosolWithoutRelyingOnRoundEnd()
        {
            var s=new Session(GameplayTools.Aerosol);s.Pickup(1);s.Tick(true);
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
            s.Host.RemoveActor(1,ActorRemovalReason.Left);
            Assert.That(s.Host.IsRunning,Is.True);Assert.That(s.Host.CaptureSnapshot().Actors.Any(a=>a.ActorId==3),Is.True);
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
        }
        [Test] public void DisconnectStopsNewSprayButExistingCloudExpiresNaturally()
        {
            var s=new Session(GameplayTools.Aerosol);s.Pickup(1);s.Tick(true);int remaining=s.World.Items[1].ResourceUnits;
            s.Host.SetActorConnected(1,false);s.Host.Advance(new HostTick(s.Host.CurrentTick+1));
            Assert.That(s.World.Items[1].ResourceUnits,Is.EqualTo(remaining));Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.EqualTo(1));
            for(int i=0;i<35;i++)s.Host.Advance(new HostTick(s.Host.CurrentTick+1));
            Assert.That(s.Host.CaptureSnapshot().ToolEffects.Count,Is.Zero);
        }
        [TestCase(ActionKind.CancelThrow)] [TestCase(ActionKind.SelectInventorySlot)]
        public void CancellingAStrikeCannotSkipItsCooldown(ActionKind cancel)
        {
            var s=new Session();s.World.PlanStrike=true;s.Tick();
            s.Act(ActionKind.Primary);s.Tick();uint start=s.Host.CurrentTick;
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).StrikeState.StartTick,Is.EqualTo(start));
            while(s.Host.CurrentTick<start+9)s.Tick();
            if(cancel==ActionKind.SelectInventorySlot)s.Act(cancel,true,0);else s.Act(cancel);
            s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.Cooldown),"A mouse-wheel/CancelThrow pair must not restart the swing.");
            Assert.That(s.Host.DrainEvents().Count(e=>e.Kind==GameplayEventKind.StrikeStarted),Is.EqualTo(1));
            while(s.Host.CurrentTick<start+18)s.Tick();
            s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.None),"The natural 19-tick hands cycle is unchanged.");
            Assert.That(s.Host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1).StrikeState.StartTick,Is.EqualTo(start+19));
        }
        [Test] public void PickingUpDuringASwatterStrikeCannotSkipItsCooldown()
        {
            var s=new Session(GameplayTools.Flyswatter);s.Pickup(1);s.World.PlanStrike=true;
            s.Act(ActionKind.Primary);s.Tick();uint start=s.Host.CurrentTick;
            while(s.Host.CurrentTick<start+9)s.Tick();
            s.World.Candidate=2;s.Act(ActionKind.Use);s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.World.Items[2].OwnerActorId,Is.EqualTo(1),"The pickup itself still succeeds.");
            Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.Cooldown));
            while(s.Host.CurrentTick<start+23)s.Tick();
            s.Act(ActionKind.Primary);s.Tick();
            Assert.That(s.Private.Rejection,Is.EqualTo(CommandReject.None),"The swatter's scaled 24-tick cycle is unchanged.");
        }
    }
}
