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
            if (local.PickupId == 0 || local.PickupId != received.PickupId || local.ToolId != GameplayTools.Flyswatter || local.ToolId != received.ToolId || !local.Position.IsFinite || !received.Position.IsFinite || (local.Position - received.Position).LengthSquared > PositionToleranceMetres * PositionToleranceMetres) return false;
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
    public readonly struct ToolPickupSnapshot
    {
        public readonly uint PickupId, OwnerActorId, Revision;
        public readonly string ToolId;
        public readonly Float3 Position;
        public readonly Rotation Rotation;
        public ToolPickupSnapshot(uint id, string toolId, Float3 position, Rotation rotation, uint ownerActorId = 0, uint revision = 1)
        { PickupId = id; ToolId = toolId; Position = position; Rotation = rotation; OwnerActorId = ownerActorId; Revision = revision; }
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
}
