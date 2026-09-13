using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

public sealed class StrikeTrajectoryChecks
{
    [TestCase(-.785398f)] [TestCase(0f)] [TestCase(.785398f)]
    public void VisualEndpointsMatchEveryActualAuthoritySweepIncludingRecoveryClosure(float pitch)
    {
        var world=new World{Aim=MathEx.Aim(0,pitch)};var host=new GameplayAuthority(world);
        host.BeginRound(new GameplayRoundConfig(1,1,"test","test"),new[]{new SpawnActor(1,"h",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"m",PlayerRole.Mosquito,new Float3(3,2,3))});
        host.SubmitInput("h",new PlayerInputCommand(new CommandHeader(1,1,1,1,0,1),default,0,0,pitch,world.Aim));
        Assert.That(host.SubmitAction("h",new PlayerActionCommand(new CommandHeader(1,1,1,1,0,1),ActionKind.Primary,world.Aim)),Is.EqualTo(CommandReject.None));
        Float3 previous=default;int sweeps=0;bool recoverySweep=false;
        for(uint tick=1;tick<=19;tick++)
        {
            world.Last=null;host.Advance(new HostTick(tick));var state=host.CaptureSnapshot().Actors.Single(a=>a.ActorId==1);
            var actual=StrikeVisualTrajectory.Contact(state.StrikeState,new Float3(.2f,.9f,.05f));
            if(world.Last.HasValue)
            {
                var q=world.Last.Value;sweeps++;
                Assert.That((actual-q.To).Length,Is.LessThan(.000002f));
                Assert.That((previous-q.From).Length,Is.LessThan(.000002f));
                recoverySweep|=state.StrikeState.Phase==StrikePhase.Recovery;
            }
            previous=actual;
        }
        Assert.That(sweeps,Is.EqualTo(6));Assert.That(recoverySweep,Is.True);
    }
    [Test] public void WindupAndRecoveryJoinContinuouslyWithoutEarlyRetraction()
    {
        var origin=new Float3(0,1.4f,.08f);var target=new Float3(0,1.5f,.6f);var rest=new Float3(.2f,.9f,.05f);
        foreach(float time in new[]{0f,2f/30,.08f,.25f,8f/30,.6f})
        {
            var s=State(time,origin,target);var point=StrikeVisualTrajectory.Contact(s,rest);
            var expected=time==0||time==.6f?rest:time<=.08f?origin:target;
            Assert.That((point-expected).Length,Is.LessThan(.000002f));
            foreach(float delta in new[]{-.000001f,.000001f})
                Assert.That((StrikeVisualTrajectory.Contact(State(time+delta,origin,target),rest)-point).Length,Is.LessThan(.00002f));
        }
    }
    [Test] public void InterruptedStrikeReturnsAuthoredContactAndZeroOrientationWeight()
    {
        var rest=new Float3(3,2,1);
        Assert.That((StrikeVisualTrajectory.Contact(default,rest)-rest).Length,Is.Zero);
        Assert.That(StrikeVisualTrajectory.PoseWeight(default),Is.Zero);
    }
    static StrikeState State(float elapsed,Float3 origin,Float3 target)=>new StrikeState(1,GameplayTools.Hands,1,elapsed<.08f?StrikePhase.Windup:elapsed<.25f?StrikePhase.Active:StrikePhase.Recovery,0,origin,target,-Float3.Forward,elapsed/.6f);
    sealed class World:IGameplayWorld
    {
        public Float3 Aim;public StrikeSweep? Last;
        public void BeginRound(IReadOnlyList<SpawnActor>a,IReadOnlyList<DoorDefinition>d){}
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot>a){}
        public MotorResult MoveHuman(in MotorQuery q)=>new MotorResult(q.Position,Float3.Zero,true,Float3.Up,q.CrouchFraction);
        public MotorResult MoveMosquito(in MotorQuery q)=>new MotorResult(q.Position,Float3.Zero,false,Float3.Up);
        public bool TrySurface(in SurfaceQuery q,out SurfaceContact c){c=default;return false;}
        public bool ResolveSurface(in SurfaceAttachment a,out SurfaceContact c){c=default;return false;}
        public bool TryBiteContact(in BiteQuery q,out BiteContact c){c=default;return false;}
        public bool ResolveBite(uint id,in BiteAttachment a,int n,out BiteContact c){c=default;return false;}
        public bool TryPlanStrike(uint id,Float3 aim,string tool,out StrikePlan p){var origin=new Float3(.21f,1.39f,.08f);p=new StrikePlan(origin,origin+Aim*.62f,-Aim,.075f,1,tool);return true;}
        public StrikeHit SweepStrike(in StrikeSweep q){Last=q;return default;}
        public bool TryFreeRecoveryPoint(uint id,Float3 p,out Float3 r){r=p;return true;}
        public bool HasLineOfSight(uint id,Float3 f,uint target,Float3 t)=>true;
        public bool TryDoorInteraction(in DoorInteractionQuery q,out DoorInteractionCandidate c){c=default;return false;}
        public DoorSweepResult SweepDoor(in DoorMotionQuery q)=>new DoorSweepResult(q.ToAngleRadians,false);
        public void ApplyDoorPose(in DoorPose p){}
    }
}
