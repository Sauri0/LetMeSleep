using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

public sealed class SurfaceApproachChecks
{
    // Analytical plane with the same ray reach + mosquito radius as UnityGameplayWorld.
    private sealed class PlaneWorld : IGameplayWorld
    {
        public readonly Float3 Normal;
        public bool Exists = true, Obstructed;
        public PlaneWorld(Float3 normal) { Normal = normal; }
        private SurfaceContact Contact(Float3 point, uint id = 10190) => new SurfaceContact(new SurfaceAttachment(id, 1, point, Normal, Float3.Cross(Normal, Math.Abs(Normal.Y) < .9f ? Float3.Up : Float3.Forward).Normalized), point, Normal);
        public bool TrySurface(in SurfaceQuery q, out SurfaceContact contact)
        {
            contact = default; float denominator = Float3.Dot(q.Direction, Normal);
            if (!Exists || denominator >= -.0001f) return false;
            float distance = -Float3.Dot(q.Position, Normal) / denominator;
            if (distance < 0 || distance > q.Reach + .055f) return false;
            contact = Contact(q.Position + q.Direction * distance, Obstructed ? 10191u : 10190u); return true;
        }
        public bool ResolveSurface(in SurfaceAttachment a, out SurfaceContact contact) { contact = Contact(a.LocalPoint); return Exists && a.SurfaceId == 10190; }
        public MotorResult MoveMosquito(in MotorQuery q)
        {
            var p = q.Position + q.Velocity * q.DeltaSeconds;
            float height = Float3.Dot(p, Normal);
            if (height < .055f) p += Normal * (.055f - height);
            return new MotorResult(p, q.Velocity, Normal.Y > .5f && height <= .057f, Normal);
        }
        public void BeginRound(IReadOnlyList<SpawnActor> a, IReadOnlyList<DoorDefinition> d) { }
        public void SynchronizeActors(IReadOnlyList<ActorSnapshot> a) { }
        public MotorResult MoveHuman(in MotorQuery q) => new MotorResult(q.Position, Float3.Zero, true, Float3.Up);
        public bool TryBiteContact(in BiteQuery q, out BiteContact c) { c = default; return false; }
        public bool ResolveBite(uint id, in BiteAttachment a, int count, out BiteContact c) { c = default; return false; }
        public bool TryPlanStrike(uint id, Float3 aim, string tool, out StrikePlan p) { p = default; return false; }
        public StrikeHit SweepStrike(in StrikeSweep q) => default;
        public bool TryFreeRecoveryPoint(uint id, Float3 position, out Float3 p) { p = position; return true; }
        public bool HasLineOfSight(uint id, Float3 from, uint target, Float3 to) => true;
        public bool TryDoorInteraction(in DoorInteractionQuery q, out DoorInteractionCandidate c) { c = default; return false; }
        public DoorSweepResult SweepDoor(in DoorMotionQuery q) => new DoorSweepResult(q.ToAngleRadians, false);
        public void ApplyDoorPose(in DoorPose p) { }
    }
    private static ActorSnapshot Self(GameplayAuthority authority) => authority.CaptureSnapshot().Actors.Single(a => a.ActorId == 2);
    private static GameplayAuthority Begin(PlaneWorld world)
    {
        var authority = new GameplayAuthority(world);
        authority.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "surface-regression", 30), new[] {
            new SpawnActor(1, "human", PlayerRole.Human, new Float3(8, 0, 8)),
            new SpawnActor(2, "mosquito", PlayerRole.Mosquito, world.Normal * .23f) });
        var toward = -world.Normal;
        float yaw = (float)Math.Atan2(toward.X, toward.Z), pitch = Math.Min(1.553343f, (float)Math.Asin(toward.Y));
        var aim = MathEx.Aim(yaw, pitch);
        Assert.That(authority.SubmitInput("mosquito", new PlayerInputCommand(new CommandHeader(1, 1, 2, 1, 0, 1), default, 0, yaw, pitch, aim)), Is.EqualTo(CommandReject.None));
        Assert.That(authority.SubmitAction("mosquito", new PlayerActionCommand(new CommandHeader(1, 1, 2, 1, 0, 1), ActionKind.PerchToggle, aim)), Is.EqualTo(CommandReject.None));
        return authority;
    }
    private static void Advance(GameplayAuthority authority, int count = 1)
    { for (int i = 0; i < count; i++) authority.Advance(new HostTick(authority.CurrentTick + 1)); }
    [TestCase(0, 1, 0)] [TestCase(0, -1, 0)] [TestCase(1, 0, 0)]
    [TestCase(-1, 0, 0)] [TestCase(0, 0, 1)] [TestCase(0, 0, -1)]
    public void ValidPerchFrom23CentimetersApproachesThenStaysAttached(float x, float y, float z)
    {
        var world = new PlaneWorld(new Float3(x, y, z)); var a = Begin(world);
        Advance(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.ApproachingSurface), "A valid approach must survive the first tick.");
        Assert.That(Float3.Dot(Self(a).Position, world.Normal), Is.LessThan(.23f));
        Advance(a, 20);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Surface));
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.True);
        Assert.That(Float3.Dot(Self(a).Position, world.Normal), Is.EqualTo(.057f).Within(.002f));
    }
    [TestCase(false)] [TestCase(true)]
    public void LostSurfaceCancelsApproachAndExistingPerch(bool alreadyPerched)
    {
        var world = new PlaneWorld(Float3.Up); var a = Begin(world); Advance(a, alreadyPerched ? 12 : 1);
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.True);
        world.Exists = false; Advance(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
    }
    [Test] public void InterveningSurfaceCancelsApproachInsteadOfReplacingTarget()
    {
        var world = new PlaneWorld(Float3.Up); var a = Begin(world); Advance(a);
        Assert.That(Self(a).SurfaceAttachment.HasValue, Is.True);
        world.Obstructed = true; Advance(a);
        Assert.That(Self(a).LifeState, Is.EqualTo(LifeState.Flying)); Assert.That(Self(a).SurfaceAttachment.HasValue, Is.False);
    }
}
