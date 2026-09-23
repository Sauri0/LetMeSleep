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
        private sealed class World : IGameplayWorld, IGameplayModeWorld, IGameplayTaskSelectionWorld
        {
            public bool Available = true, Work = true, Respawn = true, Valid = true, FreeRecovery = true, Bite = false, Surface = false;
            public bool Assignable = true;
            public bool WedgedMosquito;
            public Float3? HumanPosition;
            public uint Hit = 2;
            public int MosquitoMoves, StrikePlans, StrikeSweeps;
            public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
            public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
            public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(HumanPosition ?? q.Position, default, true, Float3.Up, 0);
            public MotorResult MoveMosquito(in MotorQuery q) { MosquitoMoves++; return new MotorResult(q.Position, default, !WedgedMosquito, Float3.Up, 0); }
            public bool TrySurface(in SurfaceQuery q, out SurfaceContact c) { c = new SurfaceContact(new SurfaceAttachment(1, 1, Float3.Zero, -Float3.Forward, Float3.Up), Float3.Zero, -Float3.Forward); return Surface; }
            public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact c) { c = new SurfaceContact(a, Float3.Zero, -Float3.Forward); return Surface; }
            public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = new BiteContact(new BiteAttachment(1, 1, Float3.Zero, Float3.Up, 1), Float3.Forward * .3f, Float3.Up); return Bite; }
            public bool ResolveBite(uint id, in BiteAttachment a, int n, out BiteContact c) { c = new BiteContact(a, Float3.Forward * .3f, Float3.Up); return Bite; }
            public bool TryPlanStrike(uint id, Float3 aim, string tool, out StrikePlan p) { StrikePlans++; p = new StrikePlan(Float3.Zero, Float3.Forward, Float3.Up, .1f, 0, tool); return true; }
            public StrikeHit SweepStrike(in StrikeSweep q) { StrikeSweeps++; return new StrikeHit(Hit != 0, Hit, Float3.Forward * .3f, Float3.Up); }
            public bool TryFreeRecoveryPoint(uint id, Float3 p, out Float3 free) { free = p; return FreeRecovery; }
            public bool HasLineOfSight(uint id, Float3 f, uint target, Float3 to) => true;
            public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
            public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
            public void ApplyDoorPose(in DoorPose p) { }
            public bool ValidateObjective(in SpawnActor h, ObjectiveDefinition o) => Valid;
            public bool IsObjectiveAvailable(uint id, ObjectiveDefinition o) => Available;
            public bool CanAssignObjective(uint id, ObjectiveDefinition o) => Assignable;
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
        [Test] public void SelectionBudgetSkipsDistantTaskBeforeAssignment()
        {
            var w = new World { Assignable = false }; var a = TaskSession(w);
            Assert.That(OwnTask(a), Is.Null); Step(a, 10); Assert.That(OwnTask(a), Is.Null);
            w.Assignable = true; Step(a);
            Assert.That(OwnTask(a), Is.Not.Null);
            Assert.That(OwnTask(a).IssuedTick, Is.EqualTo(a.CurrentTick));
        }

        [Test] public void MovingOutsideSelectionBudgetCannotPauseAnAlreadyAssignedDeadline()
        {
            var w = new World { Work = false }; var a = TaskSession(w);
            var original = OwnTask(a); w.Assignable = false;
            Step(a, (int)original.DeadlineTick);
            Assert.That(OwnTask(a).DeadlineTick, Is.EqualTo(original.DeadlineTick));
            Assert.That(OwnTask(a).Status, Is.EqualTo(TaskAssignmentStatus.Missed));
            Assert.That(OwnTask(a).PersonalFailures, Is.EqualTo(1));
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
            var commands = bot.Decide(new BotObservation(Actor(a, 1), Array.Empty<BotTarget>(), Float3.Forward, false, null, GameModes.Tasks, a.CapturePrivate(1), Objective(), null, null, null, (_,__)=>true), new BotTick(1, 1, 1));
            Assert.That(commands.Input.UseHeld, Is.True);
            Assert.Throws<ArgumentException>(() => new BotObservation(Actor(a, 2), Array.Empty<BotTarget>(), Float3.Forward, false, modeId: GameModes.Tasks, ownPrivate: a.CapturePrivate(1), taskObjective: Objective()));
            var evasive = bot.Decide(new BotObservation(Actor(a, 2), new[] { new BotTarget(Actor(a, 1), Float3.Forward) }, Float3.Forward, false, modeId: GameModes.Survival), new BotTick(1, 1, 2));
            evasive = bot.Decide(new BotObservation(Actor(a, 2), new[] { new BotTarget(Actor(a, 1), Float3.Forward) }, Float3.Forward, false, modeId: GameModes.Survival), new BotTick(1, 1, 14));
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
        private static GameplayAuthority TaskSession(World w, uint work = 100, ModeRuleProfile profile = null, bool secondHuman = false)
        {
            var a = new GameplayAuthority(w);
            var roster = new List<SpawnActor> { new SpawnActor(1, "h", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "m", PlayerRole.Mosquito, Float3.Forward) };
            if (secondHuman) roster.Add(new SpawnActor(3, "h2", PlayerRole.Human, Float3.Zero));
            a.BeginRound(Config(GameModes.Tasks, 600, profile: profile ?? new ModeRuleProfile(GameModes.Tasks), work: work), roster);
            return a;
        }
        private static void WorkFor(GameplayAuthority a, int ticks, uint human = 1)
        { for (int i = 0; i < ticks; i++) { Use(a, human); Step(a); } }
        private static void ReleaseTask(GameplayAuthority a, uint human = 1)
        { a.SubmitInput(human == 1 ? "h" : "h2", new PlayerInputCommand(Header(a, human), default, 0, 0, 0, Float3.Forward)); }
        private static TaskAssignment OwnTask(GameplayAuthority a, uint human = 1) => a.CapturePrivate(human).TaskAssignment;

        [Test] public void UserTaskDefaultsAndEachEffectiveBalanceValueAffectHash()
        {
            var p = new ModeRuleProfile(GameModes.Tasks);
            Assert.That(p.TaskCadenceTicks, Is.EqualTo(1200)); Assert.That(p.TaskDeadlineTicks, Is.EqualTo(900));
            Assert.That(p.TaskMinimumDeadlineTicks, Is.EqualTo(450)); Assert.That(p.TaskFailurePenaltyTicks, Is.EqualTo(150));
            Assert.That(p.TaskSuccessRecoveryTicks, Is.EqualTo(90)); Assert.That(p.TaskInterruptionGraceTicks, Is.EqualTo(30));
            Assert.That(p.TaskDecayBasisPointsPerSecond, Is.EqualTo(1000));
            var baseline = Config(GameModes.Tasks, 120, profile: p).BalanceHash;
            foreach (var changed in new[] { new ModeRuleProfile(GameModes.Tasks, taskSuccessRecoveryTicks: 89),
                new ModeRuleProfile(GameModes.Tasks, taskInterruptionGraceTicks: 29), new ModeRuleProfile(GameModes.Tasks, taskDecayBasisPointsPerSecond: 999) })
                Assert.That(Config(GameModes.Tasks, 120, profile: changed).BalanceHash, Is.Not.EqualTo(baseline));
            Assert.Throws<ArgumentException>(() => new ModeRuleProfile(GameModes.Tasks, taskSuccessRecoveryTicks: 54001));
            Assert.Throws<ArgumentException>(() => new ModeRuleProfile(GameModes.Tasks, taskInterruptionGraceTicks: 54001));
            Assert.Throws<ArgumentException>(() => new ModeRuleProfile(GameModes.Tasks, taskDecayBasisPointsPerSecond: 10001));
        }
        [Test] public void RepeatedFailuresFloorDeadlineAndSuccessesRecoverWithoutErasingHistory()
        {
            var a = TaskSession(new World(), work: 3);
            foreach (uint duration in new uint[] { 750, 600, 450, 450 })
            {
                Step(a, 1200); Assert.That(OwnTask(a).DeadlineTick - OwnTask(a).IssuedTick, Is.EqualTo(duration));
            }
            Assert.That(OwnTask(a).PersonalFailures, Is.EqualTo(4));
            foreach (uint duration in new uint[] { 540, 630, 720, 810, 900, 900 })
            {
                WorkFor(a, 3); Assert.That(OwnTask(a).Status, Is.EqualTo(TaskAssignmentStatus.Completed));
                Step(a, 1197); Assert.That(OwnTask(a).DeadlineTick - OwnTask(a).IssuedTick, Is.EqualTo(duration));
                Assert.That(OwnTask(a).PersonalFailures, Is.EqualTo(4));
            }
            Assert.That(a.CaptureSnapshot().TasksGoal, Is.EqualTo(10));
            Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(15));
        }
        [Test] public void FailureAfterRecoverySubtractsFromRecoveredPersonalDeadline()
        {
            var a = TaskSession(new World(), work: 3, secondHuman: true);
            Step(a, 1200); WorkFor(a, 3); Step(a, 1197);
            Assert.That(OwnTask(a).DeadlineTick - OwnTask(a).IssuedTick, Is.EqualTo(840));
            Step(a, 1200);
            Assert.That(OwnTask(a).DeadlineTick - OwnTask(a).IssuedTick, Is.EqualTo(690));
            Assert.That(OwnTask(a).PersonalFailures, Is.EqualTo(2));
            Assert.That(OwnTask(a, 3).DeadlineTick - OwnTask(a, 3).IssuedTick, Is.EqualTo(450));
            Assert.That(OwnTask(a, 3).PersonalFailures, Is.EqualTo(3));
            Assert.That(a.CapturePrivate(2).TaskAssignment, Is.Null);
        }
        [Test] public void InterruptionUsesOneSecondGraceThenTenPercentOfTotalWorkPerSecond()
        {
            var a = TaskSession(new World()); WorkFor(a, 40); ReleaseTask(a);
            Step(a, 30); Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(40));
            Step(a, 30); Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(30));
            Step(a, 120); Assert.That(OwnTask(a).ProgressTicks, Is.Zero);
            WorkFor(a, 10); Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(10), "Holding work never decays progress.");
            ReleaseTask(a); Step(a, 3); Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(9), "Zero progress does not replenish assignment grace.");
        }
        [Test] public void BriefWorkAndRepeatedInputCannotReplenishGraceOrDiscardFractionalDecay()
        {
            var a = TaskSession(new World()); WorkFor(a, 40); ReleaseTask(a); Step(a, 30);
            for (int i = 0; i < 6; i++)
            {
                WorkFor(a, 1); ReleaseTask(a); Step(a);
                Use(a); Use(a); // Same-tick retries do not work until authority advances.
                ReleaseTask(a);
            }
            Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(44), "Six work ticks minus two ticks of fractional decay.");
            Assert.That(OwnTask(a).IssuedTick, Is.Zero); Assert.That(OwnTask(a).DeadlineTick, Is.EqualTo(900));
        }
        [Test] public void UnavailableRouteFreezesProgressAndGraceWithoutResettingPersonalWindow()
        {
            var w = new World(); var a = TaskSession(w); WorkFor(a, 40); ReleaseTask(a); Step(a, 30);
            w.Available = false; Step(a, 60);
            Assert.That(OwnTask(a).Status, Is.EqualTo(TaskAssignmentStatus.WaitingForRoute));
            Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(40)); Assert.That(OwnTask(a).DeadlineTick, Is.EqualTo(960));
            w.Available = true; Step(a, 3);
            Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(39)); Assert.That(OwnTask(a).PersonalFailures, Is.Zero);
        }
        [Test] public void NewAssignmentRestoresGraceAndCompletedAssignmentCannotRewardTwice()
        {
            var a = TaskSession(new World(), work: 100); WorkFor(a, 40); ReleaseTask(a); Step(a, 60);
            WorkFor(a, 70); Assert.That(OwnTask(a).Status, Is.EqualTo(TaskAssignmentStatus.Completed));
            WorkFor(a, 20); Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
            Step(a, (int)(1200 - a.CurrentTick)); WorkFor(a, 40); ReleaseTask(a); Step(a, 30);
            Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(40));
        }

        [Test] public void DisconnectedActorRejectsCommandsAndCannotAcquireBotControl()
        {
            var a = Start(new World(), GameModes.Tasks);
            a.SetActorConnected(1, false); uint revision = Actor(a, 1).ViewRevision;
            a.SetActorConnected(1, false); a.SetActorConnected(999, false);
            Assert.That(Actor(a, 1).ViewRevision, Is.EqualTo(revision), "Repeated disconnect is idempotent.");
            var input = new PlayerInputCommand(Header(a, 1), default, 0, 0, 0, Float3.Forward, use: true);
            var action = new PlayerActionCommand(Header(a, 1), ActionKind.Primary, Float3.Forward);
            Assert.That(a.SubmitInput("h", input), Is.EqualTo(CommandReject.InvalidState));
            Assert.That(a.SubmitAction("h", action), Is.EqualTo(CommandReject.InvalidState));
            Assert.That(a.SubmitBotInput(input), Is.EqualTo(CommandReject.WrongOwner));
            Assert.That(a.SubmitBotAction(action), Is.EqualTo(CommandReject.WrongOwner));
            Assert.That(a.CapturePrivate(1).CanAct, Is.False);
            Assert.That(Actor(a, 1).LifeState, Is.EqualTo(LifeState.Active));
        }

        [Test] public void DisconnectRejoinInvalidatesQueuedActionAndResetsSequencesOnlyForNewRevision()
        {
            var w = new World(); var a = Start(w, GameModes.Blood);
            uint originalRevision = Actor(a, 1).ViewRevision;
            var oldHeader = new CommandHeader(1, 1, 1, 900, a.CurrentTick, originalRevision);
            Assert.That(a.SubmitInput("h", new PlayerInputCommand(oldHeader, default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.None));
            Assert.That(a.SubmitAction("h", new PlayerActionCommand(oldHeader, ActionKind.Primary, Float3.Forward)), Is.EqualTo(CommandReject.None));
            a.SetActorConnected(1, false); a.SetActorConnected(1, true); Step(a);
            Assert.That(w.StrikePlans, Is.Zero, "Queued old-revision action must not survive reconnect before Advance.");
            Assert.That(a.SubmitInput("h", new PlayerInputCommand(oldHeader, default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.OldViewRevision));
            Assert.That(a.SubmitAction("h", new PlayerActionCommand(oldHeader, ActionKind.Primary, Float3.Forward)), Is.EqualTo(CommandReject.OldViewRevision));
            var fresh = new CommandHeader(1, 1, 1, 1, a.CurrentTick, Actor(a, 1).ViewRevision);
            Assert.That(a.SubmitInput("h", new PlayerInputCommand(fresh, default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.None));
            // Reuse the former action sequence with a different payload: old history must be gone.
            var reused = new CommandHeader(1, 1, 1, 900, a.CurrentTick, Actor(a, 1).ViewRevision);
            Assert.That(a.SubmitAction("h", new PlayerActionCommand(fresh, ActionKind.Use, Float3.Forward)), Is.EqualTo(CommandReject.None));
            Assert.That(a.SubmitAction("h", new PlayerActionCommand(reused, ActionKind.Jump, Float3.Forward)), Is.EqualTo(CommandReject.None));
            Assert.That(a.CapturePrivate(1).CanAct, Is.True);
        }

        [Test] public void DisconnectedMosquitoRemainsVulnerableToElimination()
        {
            var w = new World(); var a = Start(w, GameModes.Survival, secondMosquito: true);
            a.SetActorConnected(2, false); int moves = w.MosquitoMoves; Step(a);
            Assert.That(w.MosquitoMoves - moves, Is.EqualTo(2), "Both connected and disconnected flying actors still use the motor.");
            Strike(a);
            Assert.That(Actor(a).Eliminated, Is.True); Assert.That(Actor(a).LivesRemaining, Is.Zero);
            a.SetActorConnected(2, true);
            Assert.That(Actor(a).Eliminated, Is.True); Assert.That(a.CapturePrivate(2).CanAct, Is.False);
        }

        [Test] public void DisconnectedHumanCanStillBeBittenAndFaint()
        {
            var a = Start(new World { Bite = true }, GameModes.Blood); a.SetActorConnected(1, false);
            bool fainted = false;
            for (int i = 0; i < 290 && !fainted; i++)
            {
                Assert.That(a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward, bite: true)), Is.EqualTo(CommandReject.None));
                Step(a); fainted = Actor(a, 1).LifeState == LifeState.Falling || Actor(a, 1).LifeState == LifeState.Fainted;
            }
            Assert.That(fainted, Is.True); Assert.That(a.CaptureSnapshot().BloodCollected, Is.GreaterThan(0));
            Assert.That(a.CapturePrivate(1).CanAct, Is.False);
        }

        [Test] public void DisconnectCancelsStartedStrikeBiteAndAutomaticSurfaceApproach()
        {
            var w = new World { Hit = 0 }; var a = Start(w, GameModes.Blood);
            a.SubmitAction("h", new PlayerActionCommand(Header(a, 1), ActionKind.Primary, Float3.Forward)); Step(a);
            Assert.That(Actor(a, 1).StrikeState.Phase, Is.Not.EqualTo(StrikePhase.None));
            a.SetActorConnected(1, false); Step(a, 10);
            Assert.That(Actor(a, 1).StrikeState.Phase, Is.EqualTo(StrikePhase.None)); Assert.That(w.StrikeSweeps, Is.Zero);
            w.Bite = true;
            a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward)); Step(a);
            a.SubmitInput("m", new PlayerInputCommand(Header(a, 2), default, 0, 0, 0, Float3.Forward, bite: true)); Step(a);
            Assert.That(Actor(a).BiteAttachment.HasValue, Is.True);
            a.SetActorConnected(2, false); Assert.That(Actor(a).BiteAttachment.HasValue, Is.False);
            float blood = a.CaptureSnapshot().BloodCollected; Step(a, 30); Assert.That(a.CaptureSnapshot().BloodCollected, Is.EqualTo(blood));
            a.SetActorConnected(2, true); w.Bite = false; w.Surface = true;
            a.SubmitAction("m", new PlayerActionCommand(Header(a, 2), ActionKind.PerchToggle, Float3.Forward)); Step(a);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.ApproachingSurface));
            var position = Actor(a).Position;
            a.SetActorConnected(2, false);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Actor(a).SurfaceAttachment.HasValue, Is.False);
            Assert.That(Actor(a).Position, Is.EqualTo(position));
        }

        [Test] public void DisconnectPreservesTaskWindowAndRecoveryContinuesWithoutRestoringLivesOnRejoin()
        {
            var w = new World { Respawn = false }; var a = TaskSession(w); WorkFor(a, 10);
            var task = OwnTask(a); a.SetActorConnected(1, false); Step(a, 10); a.SetActorConnected(1, true);
            Assert.That(OwnTask(a).IssuedTick, Is.EqualTo(task.IssuedTick)); Assert.That(OwnTask(a).DeadlineTick, Is.EqualTo(task.DeadlineTick));
            Assert.That(OwnTask(a).ProgressTicks, Is.EqualTo(task.ProgressTicks)); Assert.That(OwnTask(a).PersonalFailures, Is.EqualTo(task.PersonalFailures));
            // TaskSession uses one second of recovery. Keep the spawn blocked past expiry.
            Strike(a); a.SetActorConnected(2, false); Step(a, 40);
            Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2));
            var before = Actor(a); a.SetActorConnected(2, true);
            Assert.That(Actor(a).Position, Is.EqualTo(before.Position)); Assert.That(Actor(a).LifeState, Is.EqualTo(before.LifeState));
            Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2));
            a.SetActorConnected(2, false); w.Respawn = true; Step(a, 20);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2));
            Assert.That(a.CapturePrivate(2).CanAct, Is.False, "Recovery must not reconnect the player.");
        }

        [Test] public void DisconnectStopsHelpingImmediatelyWithoutGrantingFreeRescue()
        {
            var a = Start(new World(), GameModes.Tasks, secondMosquito: true); Strike(a); Use(a, 4); Step(a);
            Assert.That(a.CapturePrivate(4).HelpTargetId, Is.EqualTo(2));
            a.SetActorConnected(4, false); Assert.That(a.CapturePrivate(4).HelpTargetId, Is.Zero);
            Step(a, 35); Assert.That(Actor(a).LivesRemaining, Is.EqualTo(2));
            Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.HelpEnded && e.SourceActorId == 4), Is.EqualTo(1));
        }
        [Test] public void WedgedFallLandsAndRecoversInsteadOfIncapacitatingForever()
        {
            var w = new World { WedgedMosquito = true }; var a = Start(w, GameModes.Blood); Strike(a);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Falling), "Knocked down, the motor never reports ground.");
            Step(a, 80);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Falling), "Short falls keep waiting for ground.");
            Step(a, 20);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Stunned));
            Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.RecoveryStarted && e.SourceActorId == 2), Is.EqualTo(1));
            Step(a, 60);
            Assert.That(Actor(a).LifeState, Is.EqualTo(LifeState.Flying), "The normal recovery completes.");
        }
        // 30 s, cadence 300, deadline 240: three task slots per human.
        [Test] public void DepartedHumanNoLongerCountsTowardTheCollectiveGoal()
        {
            var a = Start(new World(), GameModes.Tasks, secondHuman: true); Step(a);
            Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(6)); Assert.That(a.CaptureSnapshot().TasksGoal, Is.EqualTo(4));
            a.RemoveActor(3, ActorRemovalReason.Left);
            var state = a.CaptureSnapshot();
            Assert.That(a.IsRunning, Is.True);
            Assert.That(state.ViableTaskOpportunities, Is.EqualTo(3), "Only the remaining human's slots stay reachable.");
            Assert.That(state.TasksGoal, Is.LessThanOrEqualTo(state.TasksCompleted + 3), "The goal must remain reachable by the humans still playing.");
            Assert.That(state.TasksGoal, Is.EqualTo(2));
            // The remaining human completes every slot and wins, instead of a guaranteed TasksMissed.
            for (int slot = 0; slot < 3; slot++)
            {
                for (int guard = 0; guard < 900 && a.IsRunning && (a.CapturePrivate(1).TaskAssignment == null || a.CapturePrivate(1).TaskAssignment.Status != TaskAssignmentStatus.Active); guard++) Step(a);
                for (int i = 0; i < 4; i++) { Use(a); Step(a); }
            }
            Step(a, 900 - (int)a.CurrentTick);
            Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(3));
            Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.TasksMet));
        }
        [Test] public void DepartedHumanKeepsCompletedAndPastOpportunities()
        {
            var a = Start(new World(), GameModes.Tasks, secondHuman: true, goal: 5);
            for (int i = 0; i < 4; i++) { Use(a, 3); Step(a); }
            Assert.That(a.CaptureSnapshot().TasksCompleted, Is.EqualTo(1));
            a.RemoveActor(3, ActorRemovalReason.Disconnected);
            var state = a.CaptureSnapshot();
            Assert.That(state.TasksCompleted, Is.EqualTo(1), "Completed work is never taken away.");
            Assert.That(state.ViableTaskOpportunities, Is.EqualTo(4), "Its finished slot stays counted; its future slots are dropped.");
            Assert.That(state.TasksGoal, Is.EqualTo(4), "A configured goal is capped by the remaining opportunities.");
            a.RemoveActor(2, ActorRemovalReason.Left);
            Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(4), "Mosquito departures do not change task opportunities.");
        }
        // The end snapshot must stay valid: a Tasks snapshot with goal 0 throws and the host could never publish the end.
        [Test] public void LastHumanLeavingDuringTheFirstSlotEndsWithAPublishableSnapshot()
        {
            var a = Start(new World(), GameModes.Tasks); Step(a, 10);
            Assert.That(a.CapturePrivate(1).TaskAssignment.Status, Is.EqualTo(TaskAssignmentStatus.Active), "The first task is still open.");
            a.RemoveActor(1, ActorRemovalReason.Left);
            Assert.That(a.IsRunning, Is.False);
            GameSessionState state = null;
            Assert.DoesNotThrow(() => state = a.CaptureSnapshot());
            Assert.That(state.SimulationPhase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(state.Result, Is.EqualTo(RoundEndReason.OpponentLeft));
            Assert.That(state.Winner, Is.EqualTo(PlayerRole.Mosquito));
            Assert.That(state.TasksGoal, Is.GreaterThanOrEqualTo(1));
            Assert.That(state.ViableTaskOpportunities, Is.GreaterThanOrEqualTo(state.TasksGoal));
        }
        [Test] public void BothHumansLeavingDuringTheFirstSlotEndWithAPublishableSnapshot()
        {
            var a = Start(new World(), GameModes.Tasks, secondHuman: true); Step(a, 5);
            a.RemoveActor(3, ActorRemovalReason.Left);
            Assert.That(a.IsRunning, Is.True);
            Assert.That(a.CaptureSnapshot().ViableTaskOpportunities, Is.EqualTo(3));
            a.RemoveActor(1, ActorRemovalReason.Disconnected);
            GameSessionState state = null;
            Assert.DoesNotThrow(() => state = a.CaptureSnapshot());
            Assert.That(state.Result, Is.EqualTo(RoundEndReason.OpponentLeft));
            Assert.That(state.TasksGoal, Is.EqualTo(2), "The last reachable goal is kept for the results screen.");
            Assert.That(state.ViableTaskOpportunities, Is.EqualTo(3));
        }
        [Test] public void EveryDepartureOrderKeepsTasksSnapshotsValid()
        {
            // Departures across the first two slots (unassigned, active, completed and missed tasks).
            foreach (bool work in new[] { false, true })
            foreach (int leaveAt in new[] { 0, 1, 10, 239, 240, 241, 299, 300, 301, 450, 599 })
            {
                string label = (work ? "after work, " : "idle, ") + "departure at " + leaveAt;
                var a = Start(new World(), GameModes.Tasks, secondHuman: true, secondMosquito: true);
                if (work) for (int i = 0; i < 4; i++) { Use(a, 3); Step(a); }
                Step(a, Math.Max(0, leaveAt - (int)a.CurrentTick));
                a.RemoveActor(3, ActorRemovalReason.Left);
                Assert.DoesNotThrow(() => a.CaptureSnapshot(), "first " + label);
                a.RemoveActor(1, ActorRemovalReason.Left);
                Assert.DoesNotThrow(() => a.CaptureSnapshot(), "last " + label);
                Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.OpponentLeft), label);
            }
        }
    }
}

