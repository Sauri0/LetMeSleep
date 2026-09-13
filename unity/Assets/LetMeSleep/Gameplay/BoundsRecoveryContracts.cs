using System.Collections.Generic;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    // Optional host-only capability. No fields added to network messages.
    public interface IGameplayBoundsWorld
    {
        bool BoundsRecoveryEnabled { get; }
        void BeginBoundsRecovery(IReadOnlyList<SpawnActor> roster);
        void EndBoundsRecovery();
        BoundsRecoveryResult CheckBounds(in BoundsRecoveryQuery query);
    }

    public enum BoundsRecoveryStatus : byte
    { Disabled, NotRequired, Recovered, NoSafeDestination, RetryDeferred, InvalidConfiguration, UnstableRecovery }

    public readonly struct BoundsRecoveryQuery
    {
        public readonly uint ActorId, Tick;
        public readonly PlayerRole Role;
        public readonly Float3 Position;
        public readonly float Height, Radius;
        public readonly bool Grounded;
        public readonly LifeState LifeState;
        public BoundsRecoveryQuery(uint actorId, uint tick, PlayerRole role, Float3 position,
            float height, float radius, bool grounded, LifeState lifeState = LifeState.Active)
        { ActorId = actorId; Tick = tick; Role = role; Position = position;
          Height = height; Radius = radius; Grounded = grounded; LifeState = lifeState; }
    }

    public readonly struct BoundsRecoveryResult
    {
        public readonly BoundsRecoveryStatus Status;
        public readonly Float3 Position;
        public readonly bool Grounded;
        public BoundsRecoveryResult(BoundsRecoveryStatus status, Float3 position = default, bool grounded = false)
        { Status = status; Position = position; Grounded = grounded; }
    }
}
