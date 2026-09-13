using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

// Authority/capability composition with prescribed contacts; not a PhysX adjacency test.
public sealed class SurfaceTraversalChecks
{
    private sealed class World : IGameplayWorld, ISurfaceTraversalWorld
    {
        public readonly Dictionary<uint, Float3> Normals = new Dictionary<uint, Float3> { [10] = Float3.Up };
        public Float3 Shift;
        public SurfaceContact? Next;
        public bool Exists = true, Follow = true, BlockMotor, Hit;
        public int FollowCalls;
        public readonly List<MotorQuery> Moves = new List<MotorQuery>();
        public SurfaceContact Contact(uint id, Float3 point, Float3 normal) =>
            new SurfaceContact(new SurfaceAttachment(id, 1, point - Shift, normal, Float3.Forward), point, normal);
        public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact c)
        { c = Contact(a.SurfaceId, a.LocalPoint + Shift, Normals.TryGetValue(a.SurfaceId, out var n) ? n : a.LocalNormal); return Exists && Normals.ContainsKey(a.SurfaceId); }
        public bool TrySurface(in SurfaceQuery q, out SurfaceContact c)
        { c = Contact(10, Shift, Float3.Up); return Exists; }
        public bool TryFollowSurface(uint actor, in SurfaceAttachment previous, Float3 position, Float3 direction, out SurfaceContact c)
        {
            FollowCalls++; c = default;
            if (!Exists || !Follow || !ResolveSurface(previous, out var old)) return false;
            if (Next.HasValue) { c = Next.Value; Next = null; return true; }
            c = Contact(previous.SurfaceId, position - old.WorldNormal * Float3.Dot(position - old.WorldPoint, old.WorldNormal), old.WorldNormal);
            return true;
        }
        public MotorResult MoveMosquito(in MotorQuery q)
        { Moves.Add(q); return new MotorResult(BlockMotor ? q.Position : q.Position + q.Velocity * q.DeltaSeconds, q.Velocity, false, Float3.Up); }
        public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(q.Position, Float3.Zero, true, Float3.Up, q.CrouchFraction);
        public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
        public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = default; return false; }
        public bool ResolveBite(uint id, in BiteAttachment a, int count, out BiteContact c) { c = default; return false; }
        public bool TryPlanStrike(uint id, Float3 aim, string tool, out StrikePlan p) { p = new StrikePlan(Float3.Zero, Float3.Forward, -Float3.Forward, .075f, 1, tool); return true; }
        public StrikeHit SweepStrike(in StrikeSweep q) => new StrikeHit(Hit, Hit ? 2u : 0u, Float3.Forward, -Float3.Forward);
        public bool TryFreeRecoveryPoint(uint id, Float3 position, out Float3 point) { point = position; return false; }
        public bool HasLineOfSight(uint actor, Float3 from, uint target, Float3 to) => true;
        public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
        public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
        public void ApplyDoorPose(in DoorPose p) { }
    }
    private static GameplayRoundConfig Config => new GameplayRoundConfig(1, 1, "house-patio-v1", "traversal-cpu", 300, 100);
    private static GameplayAuthority Begin(World w)
    {
        var a = new GameplayAuthority(w);
        a.BeginRound(Config, new[] { new SpawnActor(1, "h", PlayerRole.Human, new Float3(5, 0, 5)), new SpawnActor(2, "m", PlayerRole.Mosquito, new Float3(0, .057f, 0)) });
        Input(a, 0, -(float)Math.PI / 2);
        Act(a, ActionKind.PerchToggle, 1); Tick(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Surface)); return a;
    }
    private static ActorSnapshot Self(GameplayAuthority a) => a.CaptureSnapshot().Actors.Single(x => x.ActorId == 2);
    private static void Tick(GameplayAuthority a) => a.Advance(new HostTick(a.CurrentTick + 1));
    private static void Input(GameplayAuthority a, float yaw, float pitch, Float2 move = default)
    { var s = Self(a); Assert.That(a.SubmitInput("m", new PlayerInputCommand(new CommandHeader(1, 1, 2, a.CurrentTick + 1, a.CurrentTick, s.ViewRevision), move, 0, yaw, pitch, MathEx.Aim(yaw, pitch))), Is.EqualTo(CommandReject.None)); }
    private static void Act(GameplayAuthority a, ActionKind kind, uint sequence)
    { var s = Self(a); Assert.That(a.SubmitAction("m", new PlayerActionCommand(new CommandHeader(1, 1, 2, sequence, a.CurrentTick, s.ViewRevision), kind, s.ViewForward)), Is.EqualTo(CommandReject.None)); }
    private static void Queue(World w, GameplayAuthority a, uint id, Float3 normal, float distance = .05f)
    {
        w.Normals[id] = normal;
        Float3 center = Self(a).Position + new Float3(distance, 0, 0);
        w.Next = w.Contact(id, center - normal * .057f, normal);
    }
    [Test] public void FloorWallCeilingAndReverseUseMotorAndRoundTripExistingSnapshot()
    {
        var w = new World(); var a = Begin(w); var replica = new ReplicaStateGate(); replica.Reset(Config);
        uint id = 11;
        foreach (var normal in new[] { -Float3.Forward, -Float3.Up, -Float3.Forward, Float3.Up })
        {
            var before = Self(a); Queue(w, a, id, normal); Tick(a);
            var motor = w.Moves.Last();
            Assert.That((Self(a).Position - (motor.Position + motor.Velocity * motor.DeltaSeconds)).Length,
                Is.LessThan(.0001f), "Selecting another contact must preserve the motor result");
            Assert.That((Self(a).Position - before.Position).Length, Is.LessThanOrEqualTo(.65f / 30 + .0001f));
            Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.ApproachingSurface));
            Assert.That(Self(a).SurfaceAttachment.Value.SurfaceId, Is.EqualTo(id));
            int frames = 0;
            while (Self(a).LifeState == LifeState.ApproachingSurface && frames++ < 20)
            {
                var from = Self(a).Position; Tick(a);
                Assert.That((Self(a).Position - from).Length, Is.LessThanOrEqualTo(.65f / 30 + .0001f));
                Assert.That(GameplayWireCodec.TryDecodeSnapshot(GameplayWireCodec.Encode(a.CaptureSnapshot()), out var decoded), Is.True);
                Assert.That(replica.AcceptSnapshot(decoded), Is.True);
                var remote = decoded.Actors.Single(x => x.ActorId == 2);
                Assert.That(remote.SurfaceAttachment.Value.SurfaceId, Is.EqualTo(id));
                Assert.That((remote.SurfaceAttachment.Value.LocalNormal - normal).Length, Is.LessThan(.0001f));
                Assert.That((remote.Position - Self(a).Position).Length, Is.LessThan(.0001f));
            }
            Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Surface));
            Console.WriteLine("SURFACE_TRAVERSAL_CPU old=" + before.SurfaceAttachment.Value.SurfaceId + " new=" + id + " steps=" + frames + " remote=codec_only");
            id++;
        }
    }
    [Test] public void FDuringEdgeApproachCancelsAndDoesNotReattachOnItsOwn()
    {
        var w = new World(); var a = Begin(w); Queue(w, a, 11, -Float3.Forward); Tick(a);
        Act(a, ActionKind.PerchToggle, 2); Tick(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
        for (int i = 0; i < 5; i++) Tick(a);
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
    }
    [Test] public void StrikeInterruptsEdgeApproachAndClearsContact()
    {
        var w = new World(); var a = Begin(w); Queue(w, a, 11, -Float3.Forward, .14f); Tick(a);
        w.Hit = true;
        var h = a.CaptureSnapshot().Actors.Single(x => x.ActorId == 1);
        Assert.That(a.SubmitAction("h", new PlayerActionCommand(new CommandHeader(1, 1, 1, 1, a.CurrentTick, h.ViewRevision), ActionKind.Primary, h.ViewForward)), Is.EqualTo(CommandReject.None));
        for (int i = 0; i < 5; i++) Tick(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Falling)); Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
    }
    [Test] public void StalledOrRemovedNewSupportCancelsInsteadOfHanging()
    {
        foreach (bool removed in new[] { false, true })
        {
            var w = new World(); var a = Begin(w); Queue(w, a, 11, -Float3.Forward); Tick(a);
            if (removed) w.Exists = false; else w.BlockMotor = true;
            for (int i = 0; i < 22; i++) Tick(a);
            Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
        }
    }
    [Test] public void MovingSupportFollowsThroughMotorButLargeJumpDetaches()
    {
        var w = new World(); var a = Begin(w);
        w.Shift = new Float3(.02f, 0, 0); var before = Self(a).Position; Tick(a);
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.True);
        Assert.That(Self(a).Position.X, Is.GreaterThan(before.X));
        var motor = w.Moves.Last();
        Assert.That((Self(a).Position - (motor.Position + motor.Velocity * motor.DeltaSeconds)).Length,
            Is.LessThan(.0001f), "Moving support must preserve the motor result");
        Assert.That(Self(a).Position.X, Is.EqualTo(.02f).Within(.0001f), "Carry must not slip on a translated support");
        w.Shift = new Float3(2, 0, 0); Tick(a);
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
    }
    [Test] public void MissingOrDistantNeighborCannotBecomeAnAttachment()
    {
        foreach (bool distant in new[] { false, true })
        {
            var w = new World(); var a = Begin(w);
            if (distant) Queue(w, a, 11, -Float3.Forward, 1); else w.Follow = false;
            var position = Self(a).Position; Tick(a);
            Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
            Assert.That((Self(a).Position - position).Length, Is.LessThan(.001f));
        }
    }
}
