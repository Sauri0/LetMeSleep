using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class GameplayModeAuthorityTests
    {
        private sealed class World : IGameplayWorld, IGameplayModeWorld
        {
            public bool Available = true, Work = true, Respawn = true, Valid = true, FreeRecovery = true, Bite = false;
            public Float3? HumanPosition;
            public uint Hit = 2;
            public int MosquitoMoves;
            public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
            public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
            public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(HumanPosition ?? q.Position, default, true, Float3.Up, 0);
            public MotorResult MoveMosquito(in MotorQuery q) { MosquitoMoves++; return new MotorResult(q.Position, default, true, Float3.Up, 0); }
            public bool TrySurface(in SurfaceQuery q, out SurfaceContact c) { c = default; return false; }
            public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact c) { c = default; return false; }
            public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = new BiteContact(new BiteAttachment(1, 1, Float3.Zero, Float3.Up, 1), Float3.Forward * .3f, Float3.Up); return Bite; }
            public bool ResolveBite(uint id, in BiteAttachment a, int n, out BiteContact c) { c = new BiteContact(a, Float3.Forward * .3f, Float3.Up); return Bite; }
            public bool TryPlanStrike(uint id, Float3 aim, string tool, out StrikePlan p) { p = new StrikePlan(Float3.Zero, Float3.Forward, Float3.Up, .1f, 0, tool); return true; }
            public StrikeHit SweepStrike(in StrikeSweep q) => new StrikeHit(Hit != 0, Hit, Float3.Forward * .3f, Float3.Up);
            public bool TryFreeRecoveryPoint(uint id, Float3 p, out Float3 free) { free = p; return FreeRecovery; }
            public bool HasLineOfSight(uint id, Float3 f, uint target, Float3 to) => true;
            public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
            public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
            public void ApplyDoorPose(in DoorPose p) { }
            public bool ValidateObjective(in SpawnActor h, ObjectiveDefinition o) => Valid;
            public bool IsObjectiveAvailable(uint id, ObjectiveDefinition o) => Available;
            public bool CanWorkObjective(uint id, ObjectiveDefinition o, Float3 p, Float3 aim) => Work;
            public bool TryMosquitoRespawn(uint id, out Float3 p) { p = Float3.Forward * .3f; return Respawn; }
        }
        private static ObjectiveDefinition Objective(uint work = 3, string id = "lamp") => new ObjectiveDefinition(id, ObjectiveKind.Switch, "task.lamp", "action.switch", Float3.Zero, Float3.Zero, 1, work, "room", 30);
        private static GameplayRoundConfig Config(string mode, int seconds = 30, int goal = 0, ModeRuleProfile profile = null, uint work = 3) => new GameplayRoundConfig(1, 1, "house-patio-v1", "test", seconds, balance: new BalanceProfile(recoveryBaseSeconds: 1), modeId: mode, modeRules: profile ?? new ModeRuleProfile(mode, 300, 240, 120, 30), objectives: mode == GameModes.Tasks ? new[] { Objective(work) } : null, tasksGoal: goal);
        private static GameplayAuthority Start(World w, string mode, bool secondHuman = false, bool secondMosquito = false, int goal = 0)
        {
            var a = new GameplayAuthority(w);
            var roster = new List<SpawnActor> { new SpawnActor(1, "h", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "m", PlayerRole.Mosquito, Float3.Forward * .3f) };
            if (secondHuman) roster.Add(new SpawnActor(3, "h2", PlayerRole.Human, Float3.Zero));
            if (secondMosquito) roster.Add(new SpawnActor(4, "m2", PlayerRole.Mosquito, Float3.Zero));
            a.BeginRound(Config(mode, goal: goal), roster); return a;
        }
        private static ActorSnapshot Actor(GameplayAuthority a, uint id = 2) => a.CaptureSnapshot().Actors.Single(x => x.ActorId == id);
        private static void Step(GameplayAuthority a, int ticks = 1) { for (int i = 0; i < ticks; i++) a.Advance(new HostTick(a.CurrentTick + 1)); }
        private static CommandHeader Header(GameplayAuthority a, uint id) => new CommandHeader(1, 1, id, a.CurrentTick + 100, a.CurrentTick, Actor(a, id).ViewRevision);
        private static void Use(GameplayAuthority a, uint id = 1) => a.SubmitInput(id == 1 ? "h" : id == 3 ? "h2" : "m2", new PlayerInputCommand(Header(a, id), default, 0, 0, 0, Float3.Forward, use: true));
        private static void Strike(GameplayAuthority a) { a.SubmitAction("h", new PlayerActionCommand(Header(a, 1), ActionKind.Primary, Float3.Forward)); Step(a, 5); }

        [Test] public void SurvivalHitEliminatesOnceAndRejectsCommands()
        {
            var a = Start(new World(), GameModes.Survival, secondMosquito: true); Strike(a);
            Assert.That(Actor(a).Eliminated, Is.True); Assert.That(Actor(a).LivesRemaining, Is.Zero);
            Assert.That(a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.InvalidState));
            Step(a, 60); Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.ActorEliminated), Is.EqualTo(1));
            Assert.That(a.CapturePrivate(2).CanAct, Is.False);
        }
        [Test] public void SurvivalLastMosquitoWinsHumansImmediately()
        {
            var a = Start(new World(), GameModes.Survival); Strike(a);
            Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.AllOpponentsEliminated)); Assert.That(a.CaptureSnapshot().Winner, Is.EqualTo(PlayerRole.Human));
        }
        [Test] public void SurvivalTimeoutWinsMosquitoAndBloodStaysZero()
        {
            var a = Start(new World(), GameModes.Survival); Step(a, 900);
            Assert.That(a.CaptureSnapshot().Winner, Is.EqualTo(PlayerRole.Mosquito)); Assert.That(a.CaptureSnapshot().BloodGoal, Is.Zero);
        }
        [Test] public void BloodTimeoutAndKnockdownRemainUnchanged()
        {
            var a = Start(new World(), GameModes.Blood); Strike(a); Assert.That(Actor(a).Eliminated, Is.False);
            Step(a, 895); Assert.That(a.CaptureSnapshot().Winner, Is.EqualTo(PlayerRole.Human)); Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.TimeExpired));
        }
        [Test] public void TaskAssignmentPrivateAndGoalDoesNotFinishEarly()
        {
            var a = Start(new World(), GameModes.Tasks, goal: 1);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ObjectiveId, Is.EqualTo("lamp")); Assert.That(a.CapturePrivate(2).TaskAssignment, Is.Null);
            Use(a); Step(a, 3); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1)); Assert.That(a.IsRunning, Is.True);
            var e = a.DrainEvents().Single(x => x.Kind == GameplayEventKind.TaskCompleted); Assert.That(e.Position.LengthSquared, Is.Zero); Assert.That(e.TargetActorId, Is.Zero);
            Step(a, 897); Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.TasksMet));
        }
        [Test] public void TasksPauseForObstructionAndStrikePreservingProgress()
        {
            var w = new World(); var a = Start(w, GameModes.Tasks); Use(a); Step(a);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1)); w.Work = false; Step(a, 2);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1)); w.Work = true; Strike(a);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1)); Step(a, 20); Use(a); Step(a, 2);
            Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
        }
        [Test] public void MissDeadlinePenalizesOnlyFuturePersonalWindow()
        {
            var a = Start(new World(), GameModes.Tasks, secondHuman: true); Use(a, 3); Step(a, 239);
            Assert.That(a.CapturePrivate(1).TaskAssignment.Status, Is.EqualTo(TaskAssignmentStatus.Active)); Step(a);
            Assert.That(a.CapturePrivate(1).TaskAssignment.PersonalFailures, Is.EqualTo(1)); Step(a, 60);
            Assert.That(a.CapturePrivate(1).TaskAssignment.DeadlineTick, Is.EqualTo(510)); Assert.That(a.CapturePrivate(3).TaskAssignment.DeadlineTick, Is.EqualTo(540));
            Assert.That(a.CaptureSnapshot().TasksGoal, Is.EqualTo(4)); Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(6));
        }
        [Test] public void BlockedRouteWaitsWithoutPenaltyOrDiscount()
        {
            var w = new World { Available = false }; var a = Start(w, GameModes.Tasks); Step(a, 500);
            Assert.That(a.CapturePrivate(1).TaskAssignment, Is.Null); Assert.That(a.CaptureSnapshot().TasksGoal, Is.EqualTo(2));
            w.Available = true; Step(a, 100); Assert.That(a.CapturePrivate(1).TaskAssignment.PersonalFailures, Is.Zero);
            Step(a, 300); Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.TasksMissed));
        }
        [Test] public void TaskLifeConsumedOnceWhileSpawnUnavailable()
        {
            var w = new World { Respawn = false }; var a = Start(w, GameModes.Tasks); Strike(a); Step(a, 100);
            Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2)); Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.LifeConsumed), Is.EqualTo(1));
            w.Respawn = true; Step(a, 20); Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying)); Strike(a); Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2));
        }
        [Test] public void ThreeTaskDeathsEliminateAndWinEarly()
        {
            var a = Start(new World(), GameModes.Tasks);
            for (int i = 0; i < 3; i++) { Strike(a); Step(a, 110); }
            Assert.That(Actor(a).Eliminated, Is.True); Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.AllOpponentsEliminated));
            Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.LifeConsumed), Is.EqualTo(3));
        }
        [Test] public void FullRescueConservesPersonalLife()
        {
            var a = Start(new World(), GameModes.Tasks, secondMosquito: true); Strike(a);
            for (int i = 0; i < 25; i++) { Use(a, 4); Step(a); }
            Assert.That(Actor(a).LivesRemaining, Is.EqualTo(3)); Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying));
        }
        [Test] public void Default120SecondsHasThreeOpportunitiesPerHuman()
        {
            var a = new GameplayAuthority(new World());
            a.BeginRound(Config(GameModes.Tasks, 120, profile: new ModeRuleProfile(GameModes.Tasks)), new[] { new SpawnActor(1, "h", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "m", PlayerRole.Mosquito, Float3.Zero) });
            Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(3)); Assert.That(a.CaptureSnapshot().TasksGoal, Is.EqualTo(2));
        }
        [Test] public void TasksRequireCatalogAndValidatedWorld()
        {
            Assert.Throws<ArgumentException>(() => new GameplayRoundConfig(1, 1, "map", "hash", modeId: GameModes.Tasks));
            Assert.Throws<ArgumentException>(() => Start(new World { Valid = false }, GameModes.Tasks));
            Assert.Throws<ArgumentException>(() => new GameplayRoundConfig(1, 1, "map", "hash", modeId: GameModes.Tasks, objectives: new[] { Objective(id: "same"), Objective(id: "same") }));
        }
        [Test] public void TaskBotOnlyUsesOwnAssignmentAndSurvivalEvades()
        {
            var a = Start(new World(), GameModes.Tasks); var bot = new BotController();
            var commands = bot.Decide(new BotObservation(Actor(a, 1), Array.Empty<BotTarget>(), Float3.Forward, false, modeId: GameModes.Tasks, ownPrivate: a.CapturePrivate(1), taskObjective: Objective()), new BotTick(1, 1, 1));
            Assert.That(commands.Input.UseHeld, Is.True);
            Assert.Throws<ArgumentException>(() => new BotObservation(Actor(a, 2), Array.Empty<BotTarget>(), Float3.Forward, false, modeId: GameModes.Tasks, ownPrivate: a.CapturePrivate(1), taskObjective: Objective()));
            var evasive = bot.Decide(new BotObservation(Actor(a, 2), new[] { new BotTarget(Actor(a, 1), Float3.Forward) }, Float3.Forward, false, modeId: GameModes.Survival), new BotTick(1, 1, 2));
            Assert.That(evasive.Input.BiteHeld, Is.False); Assert.That(evasive.Input.AimForward.Z, Is.LessThan(0));
        }
        [Test] public void DisconnectAndRestartClearModeState()
        {
            var w = new World(); var a = Start(w, GameModes.Tasks); Use(a); Step(a, 3); a.RemoveActor(2, ActorRemovalReason.Disconnected);
            Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.OpponentLeft));
            a.BeginRound(Config(GameModes.Survival), new[] { new SpawnActor(1, "h", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "m", PlayerRole.Mosquito, Float3.Zero) });
            Assert.That(a.CapturePrivate(1).TaskAssignment, Is.Null); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.Zero); Assert.That(Actor(a).LivesRemaining, Is.EqualTo(1));
        }
        [Test] public void BitePausesHumanTaskAndResumeDoesNotDuplicateCompletion()
        {
            var w = new World { Bite = true }; var a = Start(w, GameModes.Tasks); Use(a);
            a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward)); Step(a);
            a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward, bite: true)); Step(a, 2);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1));
            w.Bite = false; Use(a); Step(a, 3); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
            Use(a); Step(a, 10); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
        }
        [Test] public void CompletedRescueWaitsSafelyWithoutConsumingLife()
        {
            var w = new World { FreeRecovery = false }; var a = Start(w, GameModes.Tasks, secondMosquito: true); Strike(a);
            for (int i = 0; i < 40; i++) { Use(a, 4); Step(a); }
            Assert.That(Actor(a).LivesRemaining, Is.EqualTo(3)); Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Stunned));
            w.FreeRecovery = true; Step(a, 20); Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Actor(a).LivesRemaining, Is.EqualTo(3));
        }
        [Test] public void LeavingObjectiveRadiusAndDoorUsePauseProgress()
        {
            var w = new World(); var a = Start(w, GameModes.Tasks); Use(a); Step(a);
            w.HumanPosition = new Float3(10, 0, 0); Step(a, 2); Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1));
            w.HumanPosition = Float3.Zero; a.SubmitAction("h", new PlayerActionCommand(Header(a, 1), ActionKind.Use, Float3.Forward)); Step(a);
            Assert.That(a.CapturePrivate(1).TaskAssignment.ProgressTicks, Is.EqualTo(1)); Step(a, 2); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
        }
        [Test] public void DeadlineOnCadenceBoundaryPenalizesOnce()
        {
            var a = new GameplayAuthority(new World());
            a.BeginRound(Config(GameModes.Tasks, profile: new ModeRuleProfile(GameModes.Tasks, 300, 300, 120, 30)), new[] { new SpawnActor(1, "h", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "m", PlayerRole.Mosquito, Float3.Zero) });
            Step(a, 300); Assert.That(a.CapturePrivate(1).TaskAssignment.PersonalFailures, Is.EqualTo(1)); Assert.That(a.CapturePrivate(1).TaskAssignment.DeadlineTick, Is.EqualTo(570));
            Step(a, 300); Assert.That(a.CapturePrivate(1).TaskAssignment.PersonalFailures, Is.EqualTo(2));
        }
        [Test] public void DirectedBotPathUsesOpenAuthoredPassage()
        {
            var patrol = new BotPatrol(new[] { new BotRegion("a", new Float3(-2,-1,-2), new Float3(2,2,2)), new BotRegion("b", new Float3(3,-1,-2), new Float3(6,2,2)) }, new[] { new BotPassage("door", "a", "b", new[] { new Float3(1,0,1), new Float3(4,0,1) }) }, 1);
            Assert.That(patrol.DirectionTo(Float3.Zero, "b", new Float3(5,0,0), _ => false).LengthSquared, Is.Zero);
            var direction = patrol.DirectionTo(Float3.Zero, "b", new Float3(5,0,0), _ => true);
            Assert.That(direction.X, Is.EqualTo(1)); Assert.That(direction.Z, Is.EqualTo(1));
            Assert.That(patrol.DirectionTo(Float3.Zero, "unknown", new Float3(5,0,0), _ => true).LengthSquared, Is.Zero);
        }
        [Test] public void ModeProfileAndCatalogArePartOfIdentity()
        {
            var blood = Config(GameModes.Blood); var survival = Config(GameModes.Survival); var tasks = Config(GameModes.Tasks);
            Assert.That(blood.BalanceHash, Is.Not.EqualTo(survival.BalanceHash)); Assert.That(tasks.BalanceHash, Is.Not.EqualTo(Config(GameModes.Tasks, work: 4).BalanceHash));
            var rules = new ModeRuleProfile(GameModes.Tasks); Assert.That(rules.DeadlineFor(1800), Is.EqualTo(rules.TaskMinimumDeadlineTicks));
            Assert.Throws<ArgumentException>(() => Objective(work: uint.MaxValue));
        }
        [Test] public void RoomModeChangeResetsReadyAndRejectsUnknownProfile()
        {
            var room = new RoomSession("h", "Host", new SeededRandom(1)); room.Join("m", "Guest", RoomSession.Protocol); room.SetReady("h", true); room.SetReady("m", true);
            Assert.That(room.ChangeRules("h", new RoomRules(modeId: GameModes.Tasks)), Is.EqualTo(RoomError.None));
            Assert.That(room.Snapshot().Members.All(m => !m.Ready), Is.True); Assert.That(room.Snapshot().Rules.BloodQuota, Is.Zero);
            Assert.That(new RoomRules(modeId: "sleep").IsValid, Is.False); Assert.That(new RoomRules(modeId: GameModes.Tasks, modeRuleProfileId: "bad").IsValid, Is.False);
        }
    }
}

