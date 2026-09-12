using System.Collections.Generic;

namespace LetMeSleep.Gameplay
{
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
