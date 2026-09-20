using System.Collections.Generic;

namespace LetMeSleep.Gameplay
{
    public enum ToolEffectKind : byte { RacketPulse=1, AerosolCloud=2 }
    public readonly struct ToolEffectQuery
    {
        public readonly ToolEffectKind Kind;
        public readonly uint SourceActorId, PickupId;
        public readonly Float3 Origin, Forward;
        public readonly float Range, HalfAngleDegrees;
        public ToolEffectQuery(ToolEffectKind kind,uint sourceActorId,uint pickupId,Float3 origin,Float3 forward,float range,float halfAngleDegrees)
        {Kind=kind;SourceActorId=sourceActorId;PickupId=pickupId;Origin=origin;Forward=forward;Range=range;HalfAngleDegrees=halfAngleDegrees;}
    }
    public readonly struct ToolEffectHit
    {
        public readonly uint ActorId;
        public readonly Float3 Point, Normal;
        public ToolEffectHit(uint actorId,Float3 point,Float3 normal){ActorId=actorId;Point=point;Normal=normal;}
    }
    public readonly struct ToolEffectSnapshot
    {
        public readonly uint EffectId, PickupId, SourceActorId, StartTick, EndHalfTick;
        public readonly ToolEffectKind Kind;
        public readonly Float3 Origin, Forward;
        public ToolEffectSnapshot(uint effectId,uint pickupId,uint sourceActorId,ToolEffectKind kind,Float3 origin,Float3 forward,uint startTick,uint endHalfTick)
        {EffectId=effectId;PickupId=pickupId;SourceActorId=sourceActorId;Kind=kind;Origin=origin;Forward=forward;StartTick=startTick;EndHalfTick=endHalfTick;}
    }
    // Native geometry/first-blocker/line-of-sight only. Authority owns damage and resource consumption.
    public interface IGameplayToolEffectWorld
    {
        bool TryToolEffectOrigin(uint actorId,uint pickupId,Float3 aim,out Float3 origin,out Float3 forward);
        IReadOnlyList<ToolEffectHit> QueryToolEffect(in ToolEffectQuery query);
    }
}
