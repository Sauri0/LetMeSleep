using System.Collections.Generic;
using System;

namespace LetMeSleep.Gameplay
{
    public static class ToolDefinitionValidation
    {
        public const float PositionToleranceMetres = .001f;
        public const double RotationToleranceDegrees = .1;
        public static bool Matches(in ToolPickupDefinition local, in ToolPickupDefinition received)
        {
            if (local.PickupId == 0 || local.PickupId != received.PickupId || !GameplayTools.IsPickup(local.ToolId) || local.ToolId != received.ToolId || !local.Position.IsFinite || !received.Position.IsFinite || (local.Position - received.Position).LengthSquared > PositionToleranceMetres * PositionToleranceMetres) return false;
            var a = local.Rotation; var b = received.Rotation;
            if (!Valid(a) || !Valid(b)) return false;
            double dot = Math.Abs((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z + (double)a.W * b.W) / Math.Sqrt(Norm(a) * Norm(b));
            return dot >= Math.Cos(RotationToleranceDegrees * Math.PI / 360);
        }
        private static double Norm(Rotation q) => (double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W;
        private static bool Valid(Rotation q) => MathEx.Finite(q.X) && MathEx.Finite(q.Y) && MathEx.Finite(q.Z) && MathEx.Finite(q.W) && Math.Abs(Norm(q) - 1) < .002;
    }
    public readonly struct ToolPickupDefinition
    {
        public readonly uint PickupId;
        public readonly string ToolId;
        public readonly Float3 Position;
        public readonly Rotation Rotation;
        public ToolPickupDefinition(uint id, string toolId, Float3 position, Rotation rotation) { PickupId = id; ToolId = toolId; Position = position; Rotation = rotation; }
    }
    public enum ToolPickupPhase : byte { World, Held, Projectile }
    public readonly struct ToolPickupSnapshot
    {
        public readonly uint PickupId, OwnerActorId, Revision;
        public readonly string ToolId;
        public readonly Float3 Position;
        public readonly Rotation Rotation;
        public readonly ToolPickupPhase Phase;
        public readonly Float3 Velocity;
        public readonly uint ThrowerActorId;
        public readonly int ResourceUnits;
        public readonly bool ImpactConsumed;
        public ToolPickupSnapshot(uint id, string toolId, Float3 position, Rotation rotation, uint ownerActorId = 0, uint revision = 1)
            : this(id, toolId, position, rotation, ownerActorId, revision, ownerActorId == 0 ? ToolPickupPhase.World : ToolPickupPhase.Held, default, 0, GameplayTools.InitialResourceUnits(toolId), false) { }
        public ToolPickupSnapshot(uint id, string toolId, Float3 position, Rotation rotation, uint ownerActorId, uint revision, ToolPickupPhase phase, Float3 velocity, uint throwerActorId, int resourceUnits, bool impactConsumed)
        { PickupId = id; ToolId = toolId; Position = position; Rotation = rotation; OwnerActorId = ownerActorId; Revision = revision; Phase = phase; Velocity = velocity; ThrowerActorId = throwerActorId; ResourceUnits = resourceUnits; ImpactConsumed = impactConsumed; }
    }
    public readonly struct ToolInteractionQuery
    {
        public readonly uint ActorId;
        public readonly Float3 EyeOrigin, AimForward;
        public readonly float Reach;
        public ToolInteractionQuery(uint actor, Float3 eye, Float3 aim, float reach) { ActorId = actor; EyeOrigin = eye; AimForward = aim; Reach = reach; }
    }
    public readonly struct ToolInteractionCandidate
    {
        public readonly uint PickupId, Revision;
        public readonly float Distance;
        public ToolInteractionCandidate(uint id, uint revision, float distance) { PickupId = id; Revision = revision; Distance = distance; }
    }
    // Optional capability: existing world fakes without map pickups remain compatible.
    public interface IGameplayToolWorld
    {
        void BeginTools(IReadOnlyList<ToolPickupDefinition> definitions);
        bool TryToolInteraction(in ToolInteractionQuery query, out ToolInteractionCandidate candidate);
        bool TryDropTool(uint actorId, out Float3 position, out Rotation rotation);
        void ApplyToolState(in ToolPickupSnapshot state);
    }
    public readonly struct ToolProjectileQuery
    {
        public readonly uint PickupId, ThrowerActorId;
        public readonly Float3 Position, Displacement;
        public readonly Rotation Rotation;
        public ToolProjectileQuery(uint pickupId, uint throwerActorId, Float3 position, Rotation rotation, Float3 displacement)
        { PickupId=pickupId;ThrowerActorId=throwerActorId;Position=position;Rotation=rotation;Displacement=displacement; }
    }
    public readonly struct ToolProjectileHit
    {
        public readonly uint ActorId;
        // Safe pickup-root origin only when !StartedOverlapping; never a collider surface point.
        public readonly Float3 Point, Normal;
        public readonly float Fraction;
        public readonly bool StartedOverlapping;
        public ToolProjectileHit(uint actorId, Float3 point, Float3 normal, float fraction)
            : this(actorId, point, normal, fraction, false) { }
        public ToolProjectileHit(uint actorId, Float3 point, Float3 normal, float fraction, bool startedOverlapping)
        { ActorId=actorId;Point=point;Normal=normal;Fraction=fraction;StartedOverlapping=startedOverlapping; }
    }
    // Queries only. The authority owns projectile integration, pickup ledger and impact consumption.
    public interface IGameplayEquipmentWorld : IGameplayToolWorld
    {
        bool TryDropTool(uint actorId, uint pickupId, out Float3 position, out Rotation rotation);
        bool TryPrepareThrow(uint actorId, uint pickupId, Float3 aim, float power, out Float3 position, out Rotation rotation, out Float3 velocity);
        bool SweepProjectile(in ToolProjectileQuery query, out ToolProjectileHit hit);
    }
}
