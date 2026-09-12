using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

public sealed class DomainChecks
{
    private sealed class World : IGameplayWorld
    {
        public bool BiteEnabled, Ground, Free = true;
        public uint HitActor;
        public bool WallHit;
        public int Sweeps;
        public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
        public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(q.Position + q.Velocity * q.DeltaSeconds, q.Velocity, Ground, Float3.Up, q.CrouchFraction);
        public MotorResult MoveMosquito(in MotorQuery q) => new MotorResult(q.Position + q.Velocity * q.DeltaSeconds, q.Velocity, Ground, Float3.Up);
        public bool TrySurface(in SurfaceQuery q, out SurfaceContact c) { c = default; return false; }
        public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact c) { c = default; return false; }
        public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = Contact(); return BiteEnabled; }
        public bool ResolveBite(uint id, in BiteAttachment a, int count, out BiteContact c) { c = Contact(); return BiteEnabled; }
        private BiteContact Contact() => new BiteContact(new BiteAttachment(1, 101, default, Float3.Forward, 1), new Float3(0, 1, .1f), Float3.Forward);
        public bool TryPlanStrike(uint a, Float3 f, string t, out StrikePlan p) { p = new StrikePlan(default, Float3.Forward, -Float3.Forward, .075f, 1, t); return true; }
        public StrikeHit SweepStrike(in StrikeSweep q) { Sweeps++; return new StrikeHit(WallHit || HitActor != 0, WallHit ? 0 : HitActor, Float3.Forward, -Float3.Forward); }
        public bool TryFreeRecoveryPoint(uint a, Float3 p, out Float3 r) { r = p; return Free; }
        public bool HasLineOfSight(uint a, Float3 f, uint b, Float3 t) => true;
        public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
        public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
        public void ApplyDoorPose(in DoorPose p) { }
    }
    private static GameplayAuthority Start(World w, float recovery = 35, bool extraMosquito = false, float quota = 20, float extraction = 8)
    {
        var authority = new GameplayAuthority(w);
        var roster = new List<SpawnActor> { new SpawnActor(1, "h", PlayerRole.Human, default), new SpawnActor(2, "m", PlayerRole.Mosquito, new Float3(0, 1, 0)) };
        if (extraMosquito) roster.Add(new SpawnActor(3, "helper", PlayerRole.Mosquito, new Float3(0, 1, -.3f)));
        authority.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "test", 30, quota, new BalanceProfile(recovery, extraction, .1f)), roster); return authority;
    }
    private static ActorSnapshot Actor(GameplayAuthority a, uint id) => a.CaptureSnapshot().Actors.Single(x => x.ActorId == id);
    private static void Input(GameplayAuthority a, uint id, bool bite = false, bool use = false, Float2 move = default, float yaw = 0, float pitch = 0)
    {
        var s = Actor(a, id); var header = new CommandHeader(1, 1, id, a.CurrentTick + 1, a.CurrentTick, s.ViewRevision);
        Assert.That(a.SubmitInput(id == 1 ? "h" : id == 2 ? "m" : "helper", new PlayerInputCommand(header, move, 0, yaw, pitch, MathEx.Aim(yaw, pitch), bite: bite, use: use)), Is.EqualTo(CommandReject.None));
    }
    private static void Tick(GameplayAuthority a) => a.Advance(new HostTick(a.CurrentTick + 1));
    private static void Strike(GameplayAuthority a, uint sequence = 1)
    { Assert.That(a.SubmitAction("h", new PlayerActionCommand(new CommandHeader(1, 1, 1, sequence, a.CurrentTick, Actor(a, 1).ViewRevision), ActionKind.Primary, Float3.Forward)), Is.EqualTo(CommandReject.None)); }
    [Test] public void FlightFollowsPitchAndStopsAfterMissingInput()
    {
        var a = Start(new World()); float yaw = .8f, pitch = .5f; var aim = MathEx.Aim(yaw, pitch);
        for (int i = 0; i < 20; i++) { Input(a, 2, move: new Float2(0, 1), yaw: yaw, pitch: pitch); Tick(a); }
        Assert.That(Float3.Dot(Actor(a, 2).Velocity.Normalized, aim), Is.GreaterThan(.9999f));
        for (int i = 0; i < 13; i++) Tick(a);
        Assert.That(Actor(a, 2).Velocity.Length, Is.LessThan(.0001f));
    }
    [Test] public void SnapshotDoesNotMutateAfterMovementOrRemoval()
    {
        var a = Start(new World()); var before = a.CaptureSnapshot();
        Input(a, 2, move: new Float2(0, 1)); Tick(a); a.RemoveActor(2, ActorRemovalReason.Left);
        Assert.That(before.Actors.Count, Is.EqualTo(2)); Assert.That(before.Actors[1].Position.Z, Is.Zero);
        Assert.That(a.CaptureSnapshot().Winner, Is.EqualTo(PlayerRole.Human));
    }
    [Test] public void RejectsForeignPrincipalRoundNumbersAndDuplicateMutation()
    {
        var a = Start(new World()); var header = new CommandHeader(1, 1, 2, 1, 0, 1);
        var c = new PlayerInputCommand(header, default, 0, 0, 0, Float3.Forward);
        Assert.That(a.SubmitInput("h", c), Is.EqualTo(CommandReject.WrongOwner));
        Assert.That(a.SubmitInput("m", new PlayerInputCommand(header, default, float.NaN, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.InvalidNumber));
        Assert.That(a.SubmitInput("m", c), Is.EqualTo(CommandReject.None));
        Assert.That(a.SubmitInput("m", c), Is.EqualTo(CommandReject.StaleSequence));
        Assert.That(a.SubmitInput("m", new PlayerInputCommand(new CommandHeader(1, 2, 2, 2, 0, 1), default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.WrongRound));
        Assert.That(a.SubmitBotInput(c), Is.EqualTo(CommandReject.WrongOwner));
    }
    [Test] public void WallStopsRestOfStrokeAcrossTicks()
    {
        var w = new World { WallHit = true }; var a = Start(w); Strike(a);
        for (int i = 0; i < 5; i++) Tick(a);
        w.WallHit = false; w.HitActor = 2;
        for (int i = 0; i < 12; i++) Tick(a);
        Assert.That(w.Sweeps, Is.EqualTo(1)); Assert.That(Actor(a, 2).LifeState, Is.EqualTo(LifeState.Flying));
    }
    [Test] public void RecoveryStartsOnLandingAndDoesNotEndRoundWhenAllMosquitoesFall()
    {
        var w = new World { HitActor = 2 }; var a = Start(w); Strike(a);
        for (int i = 0; i < 8; i++) Tick(a);
        Assert.That(Actor(a, 2).LifeState, Is.EqualTo(LifeState.Falling)); Assert.That(a.CapturePrivate(2).RecoverySeconds, Is.Zero);
        w.Ground = true; Tick(a);
        Assert.That(Actor(a, 2).LifeState, Is.EqualTo(LifeState.Stunned)); Assert.That(a.CapturePrivate(2).RecoverySeconds, Is.EqualTo(35 - 1f / 30).Within(.001f));
        Assert.That(a.IsRunning, Is.True);
        var countdown = a.CapturePrivate(2).RecoverySeconds;
        for (int i = 0; i < 12; i++) Tick(a);
        Strike(a, 2); for (int i = 0; i < 8; i++) Tick(a);
        Assert.That(a.CapturePrivate(2).RecoverySeconds, Is.LessThan(countdown));
    }
    [Test] public void ContinuousExtractionFaintsOnceAndCancelsAllAttachments()
    {
        var w = new World { BiteEnabled = true, Ground = true }; var a = Start(w, extraction: 1);
        for (int i = 0; i < 42; i++) { Input(a, 2, bite: true); Tick(a); }
        Assert.That(Actor(a, 1).LifeState, Is.EqualTo(LifeState.Fainted)); Assert.That(Actor(a, 2).BiteAttachment.HasValue, Is.False);
        Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.HumanFainted), Is.EqualTo(1));
        Assert.That(a.CaptureSnapshot().BloodCollected, Is.InRange(.99f, 1.04f));
    }
    [Test] public void HelpAcceleratesOneTimerWithoutStacking()
    {
        var w = new World { HitActor = 2, Ground = true }; var a = Start(w, extraMosquito: true); Strike(a);
        for (int i = 0; i < 6; i++) Tick(a);
        float before = a.CapturePrivate(2).RecoverySeconds;
        var helper = Actor(a, 3); var target = Actor(a, 2); var d = (target.Position - helper.Position).Normalized;
        float yaw = (float)Math.Atan2(d.X, d.Z), pitch = (float)Math.Asin(d.Y);
        Input(a, 3, use: true, yaw: yaw, pitch: pitch); Tick(a);
        Assert.That(a.CapturePrivate(3).HelpTargetId, Is.EqualTo(2));
        Assert.That(before - a.CapturePrivate(2).RecoverySeconds, Is.EqualTo(.1f).Within(.001f));
    }
    [Test] public void RoundResultIsSingleAndRemainingTimeStopsAtZero()
    {
        var a = Start(new World()); for (int i = 0; i < 900; i++) Tick(a);
        Assert.That(a.CaptureSnapshot().Result, Is.EqualTo(RoundEndReason.TimeExpired)); Assert.That(a.CaptureSnapshot().TimeRemainingTicks, Is.Zero);
        Tick(a); a.EndRound(RoundEndReason.Aborted);
        Assert.That(a.DrainEvents().Count(e => e.Kind == GameplayEventKind.RoundEnded), Is.EqualTo(1));
    }
    [Test] public void BalanceIdentityIncludesActualParameters()
    { Assert.That(new BalanceProfile(12).Hash, Is.Not.EqualTo(new BalanceProfile(35).Hash)); }
    [Test] public void ReplicaRejectsWrongContentBalanceAndRegressingSnapshots()
    {
        var a = Start(new World()); var gate = new ReplicaStateGate(); gate.Reset(a.Config); Tick(a);
        Assert.That(gate.AcceptSnapshot(a.CaptureSnapshot()), Is.True);
        var wrongContent = new GameplayRoundConfig(1, 1, "house-patio-v1", "different", 30, 20, new BalanceProfile(35));
        var wrongBalance = new GameplayRoundConfig(1, 1, "house-patio-v1", "test", 30, 20, new BalanceProfile(12));
        Assert.That(gate.AcceptSnapshot(new GameSessionState(wrongContent, 2, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>())), Is.False);
        Assert.That(gate.AcceptSnapshot(new GameSessionState(wrongBalance, 2, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>())), Is.False);
        Assert.That(gate.AcceptSnapshot(new GameSessionState(a.Config, 0, SimulationPhase.Running, 0, RoundEndReason.None, PlayerRole.Unassigned, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>())), Is.False);
    }
    [Test] public void ReplicaRejectsLatePrivateAcrossRoundsAndOldTicks()
    {
        var a = Start(new World()); var gate = new ReplicaStateGate(); gate.Reset(a.Config);
        var old = a.CapturePrivate(2); Tick(a); var fresh = a.CapturePrivate(2);
        Assert.That(gate.AcceptPrivate(fresh, 2), Is.True); Assert.That(gate.AcceptPrivate(old, 2), Is.False);
        gate.Reset(new GameplayRoundConfig(1, 2, "house-patio-v1", "test", 30));
        Assert.That(gate.AcceptPrivate(fresh, 2), Is.False);
        var next = new ActorPrivateState(2, 0, 0, CommandReject.None, InteractionHint.None, 0, 0, 0, 0, true, DoorUseResult.Accepted, 1, 2, 0);
        Assert.That(gate.AcceptPrivate(next, 2), Is.True); Assert.That(gate.AcceptPrivate(next, 1), Is.False);
    }
    [Test] public void ReplicaEventsDeduplicateAllowReorderingAndResetPerRound()
    {
        var a = Start(new World()); var gate = new ReplicaStateGate(); gate.Reset(a.Config);
        GameplayEvent Event(ulong id, ulong round = 1) => new GameplayEvent(1, round, id, 0, GameplayEventKind.StrikeStarted, 1, 0, 1, default, default);
        Assert.That(gate.AcceptEvent(Event(18)), Is.True); Assert.That(gate.AcceptEvent(Event(16)), Is.True); Assert.That(gate.AcceptEvent(Event(18)), Is.False);
        Assert.That(gate.AcceptEvent(Event(2048)), Is.True); Assert.That(gate.AcceptEvent(Event(16)), Is.False);
        gate.Reset(new GameplayRoundConfig(1, 2, "house-patio-v1", "test", 30));
        Assert.That(gate.AcceptEvent(Event(2049)), Is.False); Assert.That(gate.AcceptEvent(Event(1, 2)), Is.True);
    }
    [Test] public void InvalidDoorGeometryIsRejectedBeforeRound()
    {
        var a = Start(new World()); var roster = new[] { new SpawnActor(1, "h", PlayerRole.Human, default), new SpawnActor(2, "m", PlayerRole.Mosquito, new Float3(0, 1, 0)) };
        foreach (var door in new[] {
            new DoorDefinition(1, 1, new Float3(float.NaN, 0, 0), Rotation.Identity, new Float3(1, 2, .04f), default),
            new DoorDefinition(1, 1, default, Rotation.Identity, new Float3(-1, 2, .04f), default),
            new DoorDefinition(1, 1, default, Rotation.Identity, new Float3(1, 2, .04f), default, openSign: 0),
            new DoorDefinition(1, 1, default, new Rotation(0, 0, 0, 0), new Float3(1, 2, .04f), default) })
            Assert.Throws<ArgumentException>(() => a.BeginRound(new GameplayRoundConfig(1, 2, "house-patio-v1", "test", doors: new[] { door }), roster));
    }
}
