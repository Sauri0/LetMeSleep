using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests
{
    public sealed class BotReplanTests
    {
        private static ActorSnapshot Actor(Float3 position, LifeState life = LifeState.Active, PlayerRole role = PlayerRole.Human, BiteAttachment? bite = null)
            => new ActorSnapshot(1, role, life, 1, position, default, Rotation.Identity, Float3.Forward,
                0, 0, 1, 1, true, 0, 0, null, bite, default, 0, GameplayTools.Hands);
        private static BotPatrol Patrol(bool alternative = true) => new BotPatrol(new[] {
            new BotRegion("start", new Float3(-5,0,-5),new Float3(5,3,5)),
            new BotRegion("finish",new Float3(6,0,-5),new Float3(15,3,5)),
            new BotRegion("detour",new Float3(-5,0,6),new Float3(15,3,15))
        }, alternative ? new[] {
            new BotPassage("direct", "start", "finish", new[]{new Float3(4,1,0),new Float3(7,1,0)}),
            new BotPassage("alternative", "start", "detour",new[]{new Float3(0,1,4),new Float3(0,1,7)}),
            new BotPassage("onward", "detour", "finish",new[]{new Float3(10,1,7),new Float3(10,1,4)})
        } : new[] { new BotPassage("direct", "start", "finish",new[]{new Float3(4,1,0),new Float3(7,1,0)}) }, 1);
        private static BotObservation Task(BotPatrol patrol, Float3 position, uint tick, LifeState life = LifeState.Active, Float3? objectivePosition = null, bool steerZero = false)
        {
            var destination = objectivePosition ?? new Float3(10,0,0);
            var objective = new ObjectiveDefinition("objective",ObjectiveKind.Clean,"task.test","task.action.hold_clean",destination,destination,1,30,"finish");
            var assignment = new TaskAssignment("objective",0,2000,30,0,TaskAssignmentStatus.Active,0);
            var own = new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default,1,1,0,assignment);
            var context = new BotNavigationContext(()=>patrol.CurrentProgress, patrol.InvalidatePassage);
            return new BotObservation(Actor(position,life),Array.Empty<BotTarget>(),Float3.Forward,false,
                steerZero ? _=>Float3.Zero : (Func<Float3,Float3>)null,GameModes.Tasks,own,objective,
                _=>patrol.DirectionTo(position+Float3.Up,"finish",destination+Float3.Up,tick,_=>true),null,context);
        }
        private static BotCommands Decide(BotController bot,BotObservation observation,uint tick,ulong round=1)
            =>bot.Decide(observation,new BotTick(1,round,tick));

        [Test] public void BlockedPassageIsExcludedForExactlySixSecondsAndReopens()
        {
            var patrol=Patrol(false); var position=Float3.Up;
            Assert.That(patrol.DirectionTo(position,"finish",new Float3(10,1,0),150,_=>true).X,Is.GreaterThan(0));
            patrol.InvalidatePassage("direct",330);
            Assert.That(patrol.DirectionTo(position,"finish",new Float3(10,1,0),329,_=>true).Length,Is.Zero);
            Assert.That(patrol.IsPassageBlocked("direct",329),Is.True);
            Assert.That(patrol.DirectionTo(position,"finish",new Float3(10,1,0),330,_=>true).X,Is.GreaterThan(0));
            Assert.That(patrol.IsPassageBlocked("direct",330),Is.False);
        }
        [Test] public void ControllerInvalidatesActualPassageAndChoosesDifferentRouteNextDecision()
        {
            var patrol=Patrol();var bot=new BotController();
            Decide(bot,Task(patrol,Float3.Zero,0),0);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("direct"));
            Decide(bot,Task(patrol,Float3.Zero,149),149); Assert.That(bot.ReplanCount,Is.Zero);
            Assert.That(Decide(bot,Task(patrol,Float3.Zero,150),150).Input.MovePlanar.Y,Is.Zero);
            Assert.That(patrol.IsPassageBlocked("direct",150),Is.True);
            var alternate=Decide(bot,Task(patrol,Float3.Zero,153),153);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("alternative"));
            Assert.That(alternate.Input.MovePlanar.Y,Is.GreaterThan(0));
            Assert.That(alternate.Input.AimForward.Z,Is.GreaterThan(.9));
            Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void PerpendicularOscillationNeverResetsWaypointProgress()
        {
            var patrol=Patrol();var bot=new BotController();
            Decide(bot,Task(patrol,Float3.Zero,0),0);
            for(uint tick=3;tick<=150;tick+=3)
                Decide(bot,Task(patrol,new Float3(0,0,tick%6==0?.4f:-.4f),tick),tick);
            Assert.That(bot.ReplanCount,Is.EqualTo(1));
            Assert.That(patrol.IsPassageBlocked("direct",150),Is.True);
        }
        [Test] public void ForwardBackwardOscillationOnlyCreditsTheFirstRecord()
        {
            var patrol=Patrol();var bot=new BotController();Decide(bot,Task(patrol,Float3.Zero,0),0);
            for(uint tick=3;tick<=153;tick+=3)
                Decide(bot,Task(patrol,new Float3(tick%6==3?.3f:0,0,0),tick),tick);
            Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void GenuineProgressAndWaypointArrivalRestartTheClock()
        {
            var patrol=Patrol();var bot=new BotController();Decide(bot,Task(patrol,Float3.Zero,0),0);
            Decide(bot,Task(patrol,new Float3(.2f,0,0),149),149);
            Decide(bot,Task(patrol,new Float3(.2f,0,0),150),150);Assert.That(bot.ReplanCount,Is.Zero);
            Decide(bot,Task(patrol,new Float3(4,0,0),298),298);
            Decide(bot,Task(patrol,new Float3(4,0,0),447),447);Assert.That(bot.ReplanCount,Is.Zero);
            Decide(bot,Task(patrol,new Float3(4,0,0),448),448);Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void SteeringZeroStillInvalidatesRoute()
        {
            var patrol=Patrol();var bot=new BotController();Decide(bot,Task(patrol,Float3.Zero,0,steerZero:true),0);
            Decide(bot,Task(patrol,Float3.Zero,150,steerZero:true),150);
            Assert.That(patrol.IsPassageBlocked("direct",150),Is.True);
        }
        [Test] public void WorkingOnTaskForLongerThanFiveSecondsNeverBlocksPassage()
        {
            var patrol=Patrol();var bot=new BotController();
            for(uint tick=0;tick<=600;tick+=3)
                Assert.That(Decide(bot,Task(patrol,Float3.Zero,tick,objectivePosition:Float3.Zero),tick).Input.UseHeld,Is.True);
            Assert.That(bot.ReplanCount,Is.Zero);
        }
        [TestCase(LifeState.Falling)] [TestCase(LifeState.Stunned)] [TestCase(LifeState.Fainted)] [TestCase(LifeState.Recovering)]
        public void IncapacityDiscardsEarlierStallTime(LifeState life)
        {
            var patrol=Patrol();var bot=new BotController();Decide(bot,Task(patrol,Float3.Zero,0),0);
            Decide(bot,Task(patrol,Float3.Zero,149,life),149);
            Decide(bot,Task(patrol,Float3.Zero,900),900);Assert.That(bot.ReplanCount,Is.Zero);
            Decide(bot,Task(patrol,Float3.Zero,1049),1049);Assert.That(bot.ReplanCount,Is.Zero);
            Decide(bot,Task(patrol,Float3.Zero,1050),1050);Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void BiteAttachmentDoesNotAccumulateStallTime()
        {
            var bot=new BotController();var bite=new BiteAttachment(2,1,default,Float3.Up,1);
            for(uint tick=0;tick<=600;tick+=3)
                Decide(bot,new BotObservation(Actor(Float3.Zero,LifeState.Flying,PlayerRole.Mosquito,bite),Array.Empty<BotTarget>(),Float3.Forward,false),tick);
            Assert.That(bot.ReplanCount,Is.Zero);
        }
        [Test] public void PatrolDoesNotBlackListBecauseClockAdvancedDuringAnInterruption()
        {
            var patrol=Patrol(false);patrol.Direction(Float3.Up,0,_=>true);patrol.Direction(Float3.Up,900,_=>true);
            Assert.That(patrol.IsPassageBlocked("direct",900),Is.False);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("direct"));
        }
        [Test] public void ExplorationAndTaskRoutesShareThePassageExclusion()
        {
            var patrol=Patrol(false);patrol.Direction(Float3.Up,0,_=>true);patrol.InvalidatePassage("direct",330);
            patrol.Direction(Float3.Up,329,_=>true);Assert.That(patrol.CurrentProgress.Value.PassageId,Is.Null);
            Assert.That(patrol.DirectionTo(Float3.Up,"finish",new Float3(10,1,0),329,_=>true).Length,Is.Zero);
            patrol.Direction(Float3.Up,330,_=>true);Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("direct"));
        }
        [Test] public void RoundScopedNavigationAndControllerResetTogether()
        {
            var bot=new BotController();var oldRound=Patrol(false);
            Decide(bot,Task(oldRound,Float3.Zero,0),0);Decide(bot,Task(oldRound,Float3.Zero,150),150);
            Assert.That(oldRound.IsPassageBlocked("direct",151),Is.True);
            // GameplayRuntime.ConfigureModeMap creates a fresh GameplayBotNavigation each round.
            var newRound=Patrol(false);var command=Decide(bot,Task(newRound,Float3.Zero,151),151,2);
            Assert.That(bot.ReplanCount,Is.Zero);Assert.That(bot.BlockedUntilTick,Is.Zero);
            Assert.That(command.Input.MovePlanar.Y,Is.GreaterThan(0));Assert.That(newRound.IsPassageBlocked("direct",151),Is.False);
        }
        [Test] public void AlternatePassageCanHaveAlmostTheSameHeading()
        {
            var patrol=new BotPatrol(new[]{
                new BotRegion("start",new Float3(-5,0,-5),new Float3(5,3,5)),
                new BotRegion("finish",new Float3(6,0,-5),new Float3(15,3,5))
            },new[]{
                new BotPassage("a", "start", "finish",new[]{new Float3(4,1,0)}),
                new BotPassage("b", "start", "finish",new[]{new Float3(4,1,.1f)})
            },1);
            var bot=new BotController();Decide(bot,Task(patrol,Float3.Zero,0),0);
            Decide(bot,Task(patrol,Float3.Zero,150),150);
            var alternative=Decide(bot,Task(patrol,Float3.Zero,153),153);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("b"));
            Assert.That(alternative.Input.MovePlanar.Y,Is.GreaterThan(0));
        }
        [Test] public void InteriorWaypointDoesNotChangeJustBecauseFiveSecondsElapsed()
        {
            var patrol=Patrol(false);patrol.Direction(Float3.Up,0,_=>false);
            var initial=patrol.CurrentProgress.Value;
            patrol.Direction(Float3.Up,150,_=>false);
            Assert.That(patrol.CurrentProgress.Value.WaypointKey,Is.EqualTo(initial.WaypointKey));
            Assert.That(patrol.CurrentProgress.Value.RemainingDistance,Is.EqualTo(initial.RemainingDistance));
        }
        [Test] public void MosquitoExplorationUsesTheSameControllerInvalidation()
        {
            var patrol=Patrol(false);var bot=new BotController();
            foreach(uint tick in new[]{0u,150u})
            {
                var direction=patrol.Direction(Float3.Up,tick,_=>true);
                var observation=new BotObservation(Actor(Float3.Up,LifeState.Flying,PlayerRole.Mosquito),
                    Array.Empty<BotTarget>(),direction,false,null,GameModes.Blood,null,null,null,null,
                    new BotNavigationContext(()=>patrol.CurrentProgress,patrol.InvalidatePassage));
                Decide(bot,observation,tick);
            }
            Assert.That(patrol.IsPassageBlocked("direct",150),Is.True);
            Assert.That(bot.ReplanCount,Is.EqualTo(1));
        }
        [Test] public void ToolDiversionDoesNotBlacklistTheAbandonedTaskPassage()
        {
            var patrol=Patrol(false);var bot=new BotController();
            foreach(uint tick in new[]{0u,150u})
            {
                var original=Task(patrol,Float3.Zero,tick);
                var pickup=new ToolPickupSnapshot(10,GameplayTools.Flyswatter,new Float3(0,1.53f,3),Rotation.Identity);
                var context=new BotTrainingContext(new[]{new BotToolOpportunity(pickup,3)},1);
                var observation=new BotObservation(original.Self,original.Visible,original.FreeDirection,false,null,
                    original.ModeId,original.OwnPrivate,original.TaskObjective,original.TaskDirection,context,original.Navigation);
                Decide(bot,observation,tick);
            }
            Assert.That(bot.ReplanCount,Is.EqualTo(1));
            Assert.That(patrol.IsPassageBlocked("direct",150),Is.False);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("direct"));
        }
        [Test] public void WaitingForNoOpenTaskRouteNeverAccumulatesStallTime()
        {
            var patrol=Patrol(false);patrol.InvalidatePassage("direct",1000);var bot=new BotController();
            for(uint tick=0;tick<=600;tick+=3)
                Assert.That(Decide(bot,Task(patrol,Float3.Zero,tick),tick).Input.MovePlanar.Y,Is.Zero);
            Assert.That(bot.ReplanCount,Is.Zero);
        }
        private static readonly Float3 StairSample = new Float3(1.22218943f, 3.73779845f, 1.940733f);
        private static readonly Float3 StairDestination = new Float3(-4, 1.3f, 1);
        private static BotPatrol StairPatrol(bool authoredCorridor = true) => new BotPatrol(new[] {
            new BotRegion("lower",new Float3(-1.25f,.4f,-4.76f),new Float3(2.05f,2.65f,-1.65f)),
            new BotRegion("upper",new Float3(-1.25f,3.3f,3.15f),new Float3(2.05f,5.15f,4.76f)),
            new BotRegion("neighbour",new Float3(-1.25f,3.3f,-1.65f),new Float3(.3f,5.15f,3.15f)),
            new BotRegion("finish",new Float3(-6.76f,.4f,-4.76f),new Float3(-1.55f,2.65f,4.76f))
        },new[] {
            new BotPassage("straight_stair","lower","upper",new[] {
                new Float3(1.225f,1.4f,-2),new Float3(1.225f,2.603f,.63f),
                new Float3(1.225f,2.603f,.63f),new Float3(1.225f,2.603f,.63f),
                new Float3(1.225f,2.603f,.63f),new Float3(1.225f,4.2f,4.25f),new Float3(1.225f,4.2f,3.8f)
            },authoredCorridor?new[] {
                new BotRegion("lower-flight",new Float3(.65f,1.05f,-2),new Float3(1.8f,2.953f,1.08f)),
                new BotRegion("upper-flight",new Float3(.65f,2.253f,.18f),new Float3(1.8f,4.55f,4.70f))
            }:null),
            new BotPassage("upper_neighbour","neighbour","upper",new[] {new Float3(-.55f,4.4f,2.6f),new Float3(-.55f,4.4f,3.7f)}),
            new BotPassage("bottom_exit","lower","finish",new[] {new Float3(-1,1.3f,-2),new Float3(-2,1.3f,-2)})
        },1);
        private static Float3 StairDirection(BotPatrol patrol,Float3 sample,string source,uint tick=44,Func<string,bool> open=null)
            =>patrol.DirectionTo(sample,source,"finish",StairDestination,tick,open??(_=>true));
        private static void EnterStair(BotPatrol patrol)
        {
            StairDirection(patrol,new Float3(-.65f,4.174f,3.95f),"upper",2);
            StairDirection(patrol,new Float3(1.20407653f,4.150159f,3.80167365f),"upper",20);
            StairDirection(patrol,new Float3(1.21852863f,4.150159f,4.11133671f),"upper",23);
        }
        [Test] public void EnteredStairKeepsItsPhysicalWaypointWhenNearestRegionChanges()
        {
            var patrol=StairPatrol();EnterStair(patrol);
            StairDirection(patrol,new Float3(1.22179532f,3.8707273f,2.25073266f),"upper",41);
            string key=patrol.CurrentProgress.Value.WaypointKey;
            var direction=StairDirection(patrol,StairSample,"neighbour");
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("straight_stair"));
            Assert.That(patrol.CurrentProgress.Value.WaypointKey,Is.EqualTo(key));
            Assert.That(direction.X,Is.EqualTo(.00281057f).Within(.00001f));
            Assert.That(direction.Y,Is.EqualTo(-1.13479845f).Within(.00001f));
            Assert.That(direction.Z,Is.EqualTo(-1.310733f).Within(.00001f));
        }
        [Test] public void StairCorridorCannotInitiateAnUnenteredRouteFromTheShaft()
        {
            var patrol=StairPatrol();Assert.That(StairDirection(patrol,StairSample,"neighbour").Length,Is.Zero);
            StairDirection(patrol,new Float3(-.65f,4.174f,3.95f),"upper",2);
            Assert.That(StairDirection(patrol,StairSample,"neighbour").Length,Is.Zero,
                "Selecting a stair is insufficient; its entry waypoint must have been reached.");
        }
        [Test] public void ClosedEnteredStairWaitsWithoutMovingAndResumesOnlyWhenReopened()
        {
            var patrol=StairPatrol();EnterStair(patrol);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",44,p=>p!="straight_stair").Length,Is.Zero);
            Assert.That(patrol.CurrentProgress.HasValue,Is.False);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",45).Z,Is.LessThan(-1));
        }
        [Test] public void BlacklistedEnteredStairPreservesItsExpiryAndResumesAfterSixSeconds()
        {
            var patrol=StairPatrol();EnterStair(patrol);StairDirection(patrol,StairSample,"neighbour");
            patrol.InvalidatePassage("straight_stair",224);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",44).Length,Is.Zero);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",223).Length,Is.Zero);
            Assert.That(patrol.IsPassageBlocked("straight_stair",223),Is.True);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",224).Z,Is.LessThan(-1));
            Assert.That(patrol.IsPassageBlocked("straight_stair",224),Is.False);
        }
        [Test] public void LeavingStairCorridorDiscardsSuspendedLeg()
        {
            var patrol=StairPatrol();EnterStair(patrol);StairDirection(patrol,StairSample,"neighbour");
            patrol.InvalidatePassage("straight_stair",224);
            StairDirection(patrol,new Float3(.2f,StairSample.Y,StairSample.Z),"neighbour",45);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",224).Length,Is.Zero);
        }
        [Test] public void AnotherFloorAtSamePlanarPointCannotKeepTheEnteredStairLeg()
        {
            var patrol=StairPatrol();EnterStair(patrol);
            Assert.That(StairDirection(patrol,new Float3(StairSample.X,1.3f,StairSample.Z),null).Length,Is.Zero);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",45).Length,Is.Zero);
        }
        [Test] public void NewApproachInSameTargetRegionDiscardsEnteredStairLeg()
        {
            var patrol=StairPatrol();EnterStair(patrol);
            Assert.That(patrol.DirectionTo(StairSample,"neighbour","finish",new Float3(-5,1.3f,1),44,_=>true).Length,Is.Zero);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",45).Length,Is.Zero);
        }
        [Test] public void OrdinaryPassageDoesNotAcquireStairCorridorRetention()
        {
            var patrol=StairPatrol(false);EnterStair(patrol);
            StairDirection(patrol,StairSample,"neighbour");
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("upper_neighbour"),
                "Ordinary passages replan under the existing nearest-region policy instead of retaining the old stair leg.");
        }
        [Test] public void StairExitReturnsToOrdinaryGraphRouting()
        {
            var patrol=StairPatrol();EnterStair(patrol);StairDirection(patrol,StairSample,"neighbour");
            StairDirection(patrol,new Float3(1.225f,2.603f,.63f),"neighbour",60);
            StairDirection(patrol,new Float3(1.225f,1.4f,-2),"lower",90);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("bottom_exit"));
        }
        [Test] public void AscendingStairAlsoKeepsTheEnteredUpperFlight()
        {
            var patrol=StairPatrol();var goal=new Float3(-.65f,4.174f,3.95f);
            patrol.DirectionTo(new Float3(0,1.4f,-2.5f),"lower","upper",goal,0,_=>true);
            patrol.DirectionTo(new Float3(1.225f,1.4f,-2),"lower","upper",goal,3,_=>true);
            patrol.DirectionTo(new Float3(1.225f,2.603f,.63f),null,"upper",goal,30,_=>true);
            var direction=patrol.DirectionTo(StairSample,"neighbour","upper",goal,44,_=>true);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("straight_stair"));
            Assert.That(direction.Y,Is.GreaterThan(0));Assert.That(direction.Z,Is.GreaterThan(0));
        }
        [Test] public void GlobalInvalidationNeverSuspendsTheEnteredStairLeg()
        {
            var patrol=StairPatrol();EnterStair(patrol);StairDirection(patrol,StairSample,"neighbour");
            patrol.InvalidatePassage(null,224);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",45).Length,Is.Zero);
            Assert.That(StairDirection(patrol,StairSample,"neighbour",224).Length,Is.Zero);
        }
        [Test] public void OutsideStairCorridorsTheNearestRegionPolicyKeepsThePhysicalOrigin()
        {
            var patrol=Patrol(false);
            var position=new Float3(-5.2f,1,0);
            var direction=patrol.DirectionTo(position,"start","finish",new Float3(10,1,0),0,_=>true);
            Assert.That(patrol.CurrentProgress.Value.PassageId,Is.EqualTo("direct"));
            Assert.That(direction.X,Is.EqualTo(9.2f).Within(.00001f),"Use the physical origin, not its nearest-region projection.");
        }
        [Test] public void ControllerStopsOnSuspendedStairAndDoesNotResetItsReplanCooldown()
        {
            var patrol=StairPatrol();EnterStair(patrol);var bot=new BotController();
            var goal=StairDestination-Float3.Up;
            var objective=new ObjectiveDefinition("objective",ObjectiveKind.Clean,"task.test","task.action.hold_clean",goal,goal,1,30,"finish");
            var own=new ActorPrivateState(1,0,0,default,default,0,0,0,0,true,default,1,1,0,
                new TaskAssignment("objective",0,2000,30,0,TaskAssignmentStatus.Active,0));
            BotObservation Observe(uint tick)=>new BotObservation(Actor(StairSample-Float3.Up),Array.Empty<BotTarget>(),
                Float3.Forward,false,null,GameModes.Tasks,own,objective,
                _=>StairDirection(patrol,StairSample,"neighbour",tick),null,
                new BotNavigationContext(()=>patrol.CurrentProgress,patrol.InvalidatePassage));
            Decide(bot,Observe(44),44);
            Assert.That(Decide(bot,Observe(194),194).Input.MovePlanar.Y,Is.Zero);
            Assert.That(bot.ReplanCount,Is.EqualTo(1));Assert.That(bot.BlockedUntilTick,Is.EqualTo(374));
            Assert.That(Decide(bot,Observe(197),197).Input.MovePlanar.Y,Is.Zero);
            Assert.That(Decide(bot,Observe(373),373).Input.MovePlanar.Y,Is.Zero);
            Assert.That(bot.BlockedUntilTick,Is.EqualTo(374));Assert.That(bot.ReplanCount,Is.EqualTo(1));
            Assert.That(Decide(bot,Observe(374),374).Input.MovePlanar.Y,Is.GreaterThan(0));
        }
    }
}
