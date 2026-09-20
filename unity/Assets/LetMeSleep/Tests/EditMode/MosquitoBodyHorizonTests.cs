using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class MosquitoBodyHorizonTests
    {
        private sealed class SurfaceWorld : IGameplayWorld, IGameplayModeWorld
        {
            internal Float3 Normal = Float3.Up, SupportPoint, RespawnPoint = new Float3(3, 1, 0);
            internal bool Floor, StrikeEnabled;
            internal int Strikes, Respawns;
            private SurfaceContact Contact => new SurfaceContact(new SurfaceAttachment(1, 1, Float3.Zero, Normal, Float3.Forward), SupportPoint, Normal);
            public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
            public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
            public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(q.Position, Float3.Zero, true, Float3.Up, 0);
            public MotorResult MoveMosquito(in MotorQuery q)
            {
                var point=q.Position+q.Velocity*q.DeltaSeconds;
                if(Floor&&point.Y<=.055f)return new MotorResult(new Float3(point.X,.055f,point.Z),Float3.Zero,true,Float3.Up,0);
                return new MotorResult(point,q.Velocity,false,Normal,0);
            }
            public bool TrySurface(in SurfaceQuery q, out SurfaceContact c) { c = Contact; return true; }
            public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact c) { c = Contact; return true; }
            public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = default; return false; }
            public bool ResolveBite(uint id, in BiteAttachment a, int n, out BiteContact c) { c = default; return false; }
            public bool TryPlanStrike(uint id, Float3 aim, string tool, out StrikePlan p) { p = new StrikePlan(new Float3(0,1.53f,0),SupportPoint+Normal*.057f,Normal,.1f,1,tool); return StrikeEnabled; }
            public StrikeHit SweepStrike(in StrikeSweep q)
            { if(!StrikeEnabled||Strikes!=0)return default;Strikes++;return new StrikeHit(true,2,SupportPoint+Normal*.057f,Normal); }
            public bool ValidateObjective(in SpawnActor actor,ObjectiveDefinition objective)=>true;
            public bool IsObjectiveAvailable(uint actor,ObjectiveDefinition objective)=>true;
            public bool CanWorkObjective(uint actor,ObjectiveDefinition objective,Float3 position,Float3 aim)=>false;
            public bool TryMosquitoRespawn(uint actor,out Float3 position){Respawns++;position=RespawnPoint;return true;}
            public bool TryFreeRecoveryPoint(uint id, Float3 p, out Float3 free) { free = p; return true; }
            public bool HasLineOfSight(uint id, Float3 f, uint target, Float3 to) => true;
            public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
            public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
            public void ApplyDoorPose(in DoorPose p) { }
        }
        private static ActorSnapshot Mosquito(GameplayAuthority authority) => authority.CaptureSnapshot().Actors.Single(a => a.ActorId == 2);
        private static void Input(GameplayAuthority authority, uint sequence, Float3 aim, float forward)
        {
            float yaw = (float)Math.Atan2(aim.X, aim.Z), pitch = (float)Math.Asin(aim.Y);
            var state = Mosquito(authority);
            var header = new CommandHeader(1, 1, 2, sequence, authority.CurrentTick, state.ViewRevision);
            Assert.That(authority.SubmitInput("m", new PlayerInputCommand(header, new Float2(0, forward), 0, yaw, pitch, aim)), Is.EqualTo(CommandReject.None));
        }
        private static void Step(GameplayAuthority authority) => authority.Advance(new HostTick(authority.CurrentTick + 1));

        [Test]
        public void FlyingLookRemainsIndependentUntilMovementResumes()
        {
            var authority = new GameplayAuthority(new GameplayAuthorityTestSupport.FakeWorld());
            authority.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "test"), new[] {
                new SpawnActor(1,"h",PlayerRole.Human,Float3.Zero), new SpawnActor(2,"m",PlayerRole.Mosquito,new Float3(0,2,0)) });
            var aim = new Float3(1, .4f, 0).Normalized;
            Input(authority, 1, aim, 0); Step(authority);
            Assert.That((Mosquito(authority).ViewForward - aim).Length, Is.LessThan(.0001f));
            Assert.That((Mosquito(authority).BodyRotation.Forward - Float3.Forward).Length, Is.LessThan(.0001f));
            Input(authority, 2, aim, 1); Step(authority);
            var bodyHeading = Float3.ProjectPlane(aim, Float3.Up).Normalized;
            Assert.That((Mosquito(authority).BodyRotation.Forward - bodyHeading).Length, Is.LessThan(.0001f));
            Input(authority, 3, -Float3.Forward, 0); Step(authority);
            Assert.That((Mosquito(authority).BodyRotation.Forward - bodyHeading).Length, Is.LessThan(.0001f));
        }

        [TestCase(0, 1, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(0, -1, 0)]
        public void SupportedLookKeepsBodyAndHorizonWhileIdleAndTurnsOnMovement(int x, int y, int z)
        {
            var normal = new Float3(x, y, z); var world = new SurfaceWorld { Normal = normal };
            var authority = new GameplayAuthority(world);
            authority.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "test"), new[] {
                new SpawnActor(1,"h",PlayerRole.Human,new Float3(10,0,10)), new SpawnActor(2,"m",PlayerRole.Mosquito,normal*.057f) });
            Input(authority, 1, Float3.Forward, 0);
            Assert.That(authority.SubmitAction("m",new PlayerActionCommand(new CommandHeader(1,1,2,1,0,1),ActionKind.PerchToggle,Float3.Forward)),Is.EqualTo(CommandReject.None));
            Step(authority); Step(authority);
            Assert.That(Mosquito(authority).SurfaceAttachment.HasValue,Is.True);
            var before = Mosquito(authority).BodyRotation;
            Assert.That((before.Up-normal).Length, Is.LessThan(.0001f));
            var aim = (Float3.Cross(normal,Float3.Forward) + Float3.Forward*.2f).Normalized;
            Input(authority, 2, aim, 0); Step(authority);
            Assert.That((Mosquito(authority).BodyRotation.Forward-before.Forward).Length, Is.LessThan(.0001f));
            Assert.That((Mosquito(authority).ViewForward-aim).Length, Is.LessThan(.0001f));
            Input(authority, 3, aim, 1); Step(authority);
            Assert.That(Float3.Dot(Mosquito(authority).BodyRotation.Forward,aim), Is.GreaterThan(Float3.Dot(before.Forward,aim)+.05f));
            Assert.That((Mosquito(authority).BodyRotation.Up-normal).Length, Is.LessThan(.0001f));
        }

        [Test]
        public void CeilingDetachRestoresFlightHorizonBeforeAnyNewMovement()
        {
            var authority = new GameplayAuthority(new SurfaceWorld { Normal = -Float3.Up });
            authority.BeginRound(new GameplayRoundConfig(1,1,"house-patio-v1","test"),new[] {
                new SpawnActor(1,"h",PlayerRole.Human,new Float3(10,0,10)),new SpawnActor(2,"m",PlayerRole.Mosquito,-Float3.Up*.057f) });
            Input(authority,1,Float3.Forward,0);
            authority.SubmitAction("m",new PlayerActionCommand(new CommandHeader(1,1,2,1,0,1),ActionKind.PerchToggle,Float3.Forward));
            Step(authority); Step(authority);
            Assert.That(Float3.Dot(Mosquito(authority).BodyRotation.Up,Float3.Up),Is.LessThan(-.99f));
            var self=Mosquito(authority);
            Assert.That(authority.SubmitAction("m",new PlayerActionCommand(new CommandHeader(1,1,2,2,authority.CurrentTick,self.ViewRevision),ActionKind.Detach,Float3.Forward)),Is.EqualTo(CommandReject.None));
            Step(authority);
            Assert.That(Mosquito(authority).SurfaceAttachment,Is.Null);
            Assert.That((Mosquito(authority).BodyRotation.Up-Float3.Up).Length,Is.LessThan(.0001f));
        }

        [Test]
        public void BiteBodyFollowsChangingContactAndCarriesItsHorizon()
        {
            var world=new GameplayAuthorityTestSupport.FakeWorld();
            var firstNormal=new Float3(-1,0,0);
            var nextNormal=new Float3(-1,-1,0).Normalized;
            var attachment=new BiteAttachment(1,701,Float3.Zero,firstNormal,1);
            world.BiteStarts.Enqueue(new BiteContact(attachment,new Float3(0,1,1),firstNormal));
            world.BiteResolutions.Enqueue(new BiteContact(attachment,new Float3(0,1,1),nextNormal));
            var authority=GameplayAuthorityTestSupport.Start(world);
            Assert.That(authority.SubmitInput("mosquito",GameplayAuthorityTestSupport.Input(2,1,bite:true)),Is.EqualTo(CommandReject.None));
            Step(authority);
            Assert.That((Mosquito(authority).BodyRotation.Forward+firstNormal).Length,Is.LessThan(.0001f));
            Step(authority);
            var body=Mosquito(authority).BodyRotation;
            Assert.That((body.Forward+nextNormal).Length,Is.LessThan(.0001f));
            Assert.That(Math.Abs(Float3.Dot(body.Up,nextNormal)),Is.LessThan(.0001f));
            Assert.That(Float3.Dot(body.Up,Float3.Up),Is.GreaterThan(.6f));
        }

        [Test]
        public void CeilingKnockdownTasksRespawnKeepsUprightHorizonWithoutNewMovement()
        {
            var world=new SurfaceWorld { Normal=-Float3.Up,SupportPoint=Float3.Up*2,Floor=true,StrikeEnabled=true };
            var authority=new GameplayAuthority(world);
            var objective=new ObjectiveDefinition("test",ObjectiveKind.Clean,"task.test","task.action.hold_clean",Float3.Zero,Float3.Zero,1,30,"room");
            authority.BeginRound(new GameplayRoundConfig(1,1,"map","hash",balance:new BalanceProfile(recoveryBaseSeconds:1),modeId:GameModes.Tasks,objectives:new[]{objective}),new[]{
                new SpawnActor(1,"h",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"m",PlayerRole.Mosquito,world.SupportPoint+world.Normal*.057f)});
            Input(authority,1,Float3.Forward,0);
            Assert.That(authority.SubmitAction("m",new PlayerActionCommand(new CommandHeader(1,1,2,1,0,1),ActionKind.PerchToggle,Float3.Forward)),Is.EqualTo(CommandReject.None));
            Step(authority);Step(authority);var ceiling=Mosquito(authority);
            Assert.That(ceiling.LifeState,Is.EqualTo(LifeState.Surface));Assert.That(ceiling.LivesRemaining,Is.EqualTo(3));
            Assert.That(Float3.Dot(ceiling.BodyRotation.Up,Float3.Up),Is.LessThan(-.99f));
            var strikeAim=(Float3.Up+Float3.Forward*.5f).Normalized;
            Assert.That(authority.SubmitInput("h",new PlayerInputCommand(new CommandHeader(1,1,1,1,authority.CurrentTick,1),default,0,0,(float)Math.Asin(strikeAim.Y),strikeAim)),Is.EqualTo(CommandReject.None));
            Assert.That(authority.SubmitAction("h",new PlayerActionCommand(new CommandHeader(1,1,1,1,authority.CurrentTick,1),ActionKind.Primary,strikeAim)),Is.EqualTo(CommandReject.None));
            bool fell=false,stunned=false,recovering=false;
            for(int i=0;i<150&&!recovering;i++)
            {
                Step(authority);var current=Mosquito(authority);
                fell|=current.LifeState==LifeState.Falling;stunned|=current.LifeState==LifeState.Stunned;
                recovering=current.LifeState==LifeState.Recovering;
            }
            var respawn=Mosquito(authority);
            Assert.That(fell&&stunned&&recovering,Is.True,"Natural strike, motor landing and recovery timers must all run.");
            Assert.That(world.Strikes,Is.EqualTo(1));Assert.That(world.Respawns,Is.EqualTo(1));Assert.That(respawn.LivesRemaining,Is.EqualTo(2));
            Assert.That((respawn.Position-world.RespawnPoint).Length,Is.LessThan(.0001));Assert.That(respawn.SurfaceAttachment,Is.Null);Assert.That(respawn.BiteAttachment,Is.Null);
            Assert.That(respawn.ViewRevision,Is.GreaterThan(ceiling.ViewRevision));Assert.That((respawn.ViewForward-ceiling.ViewForward).Length,Is.LessThan(.0001));
            Assert.That((respawn.BodyRotation.Up-Float3.Up).Length,Is.LessThan(.0001));
            for(int i=0;i<15;i++)Step(authority);
            var flying=Mosquito(authority);Assert.That(flying.LifeState,Is.EqualTo(LifeState.Flying));Assert.That(flying.LivesRemaining,Is.EqualTo(2));
            Assert.That((flying.BodyRotation.Up-Float3.Up).Length,Is.LessThan(.0001));Assert.That((flying.Position-world.RespawnPoint).Length,Is.LessThan(.0001));
            Assert.That(world.Respawns,Is.EqualTo(1));
        }

        [Test]
        public void MosquitoRejectsNegativePitchPastCanonicalNinetyWhileHumanRetainsLookDownRange()
        {
            var authority=new GameplayAuthority(new GameplayAuthorityTestSupport.FakeWorld());
            authority.BeginRound(new GameplayRoundConfig(1,1,"map","hash"),new[]{new SpawnActor(1,"h",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"m",PlayerRole.Mosquito,Float3.Up)});
            Input(authority,1,-Float3.Up,0);
            float pitch=-90.01f*(float)Math.PI/180;
            Assert.That(authority.SubmitInput("m",new PlayerInputCommand(new CommandHeader(1,1,2,2,0,1),default,0,0,pitch,MathEx.Aim(0,pitch))),Is.EqualTo(CommandReject.InvalidDirection));
            Assert.That(authority.SubmitInput("h",new PlayerInputCommand(new CommandHeader(1,1,1,1,0,1),default,0,0,pitch,MathEx.Aim(0,pitch))),Is.EqualTo(CommandReject.None));
        }

        [Test]
        public void WorldVerticalAimIsValidForMosquitoButNotPastNinetyDegrees()
        {
            var authority=new GameplayAuthority(new GameplayAuthorityTestSupport.FakeWorld());
            authority.BeginRound(new GameplayRoundConfig(1,1,"house-patio-v1","test"),new[] {
                new SpawnActor(1,"h",PlayerRole.Human,Float3.Zero),new SpawnActor(2,"m",PlayerRole.Mosquito,new Float3(0,2,0)) });
            Input(authority,1,Float3.Up,0);
            float pitch=(float)Math.PI/2+.001f;
            Assert.That(authority.SubmitInput("m",new PlayerInputCommand(new CommandHeader(1,1,2,2,0,1),default,0,0,pitch,MathEx.Aim(0,pitch))),Is.EqualTo(CommandReject.InvalidDirection));
            Assert.That(authority.SubmitInput("h",new PlayerInputCommand(new CommandHeader(1,1,1,1,0,1),default,0,0,(float)Math.PI/2,Float3.Up)),Is.EqualTo(CommandReject.InvalidDirection));
        }
    }
}
