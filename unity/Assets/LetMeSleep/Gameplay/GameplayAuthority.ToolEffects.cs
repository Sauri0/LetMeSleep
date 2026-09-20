using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public sealed partial class GameplayAuthority
    {
        private sealed class ActiveToolEffect
        {
            internal ToolEffectSnapshot Snapshot;
            internal readonly HashSet<uint> HitActors=new HashSet<uint>();
        }
        private readonly Dictionary<uint,ActiveToolEffect> toolEffects=new Dictionary<uint,ActiveToolEffect>();
        private uint toolEffectId;
        private void CancelRacketEffects(uint actorId)
        {
            foreach(var id in toolEffects.Where(p=>p.Value.Snapshot.SourceActorId==actorId&&p.Value.Snapshot.Kind==ToolEffectKind.RacketPulse).Select(p=>p.Key).ToArray())toolEffects.Remove(id);
        }
        private static float EffectRange(ToolEffectKind kind)=>kind==ToolEffectKind.RacketPulse?HumanEquipmentProfile.RacketRange:HumanEquipmentProfile.AerosolRange;
        private static float EffectAngle(ToolEffectKind kind)=>kind==ToolEffectKind.RacketPulse?HumanEquipmentProfile.RacketHalfAngleDegrees:HumanEquipmentProfile.AerosolHalfAngleDegrees;
        private bool ToolOrigin(Actor actor,ToolPickupSnapshot item,out Float3 origin,out Float3 forward)
        {
            origin=forward=default;
            return world is IGameplayToolEffectWorld effects&&effects.TryToolEffectOrigin(actor.Spawn.ActorId,item.PickupId,actor.Aim,out origin,out forward)&&origin.IsFinite&&origin.Length<10000&&ValidAim(forward);
        }
        private void BeginRacketPulse(Actor actor)
        {
            if(!pickups.TryGetValue(actor.EquippedPickup,out var item)||item.ToolId!=GameplayTools.ElectricRacket||item.ResourceUnits<=0||!actor.HasInput||tick-actor.InputTick>HumanEquipmentProfile.InputFreshTicks)
            {actor.Rejection=CommandReject.InvalidState;return;}
            if(tick<item.CooldownUntilTick){actor.Rejection=CommandReject.Cooldown;return;}
            if(!ToolOrigin(actor,item,out var origin,out var forward)){actor.Rejection=CommandReject.Obstructed;return;}
            ApplyPickup(new ToolPickupSnapshot(item.PickupId,item.ToolId,item.Position,item.Rotation,item.OwnerActorId,item.Revision+1,item.Phase,item.Velocity,item.ThrowerActorId,item.ResourceUnits-1,item.ImpactConsumed,tick+HumanEquipmentProfile.RacketCooldownTicks));
            toolEffects[item.PickupId]=new ActiveToolEffect{Snapshot=new ToolEffectSnapshot(++toolEffectId,item.PickupId,actor.Spawn.ActorId,ToolEffectKind.RacketPulse,origin,forward,tick,2*tick+HumanEquipmentProfile.RacketPulseHalfTicks)};
            actor.TaskInterruptTick=tick;
        }
        private void EmitAerosol(Actor actor)
        {
            if(!actor.Connected||!CanAct(actor)||boundsHeldActors.Contains(actor.Spawn.ActorId)||!actor.Input.PrimaryHeld||!actor.PrimaryArmed||!actor.HasInput||tick-actor.InputTick>HumanEquipmentProfile.InputFreshTicks||actor.Strike.Phase!=StrikePhase.None||actor.ThrowCharge.Capture().Active||
                !pickups.TryGetValue(actor.EquippedPickup,out var item)||item.ToolId!=GameplayTools.Aerosol||item.ResourceUnits<=0)return;
            if(!ToolOrigin(actor,item,out var origin,out var forward)){actor.Rejection=CommandReject.Obstructed;return;}
            ApplyPickup(new ToolPickupSnapshot(item.PickupId,item.ToolId,item.Position,item.Rotation,item.OwnerActorId,item.Revision+1,item.Phase,item.Velocity,item.ThrowerActorId,item.ResourceUnits-1,item.ImpactConsumed,item.CooldownUntilTick));
            if(!toolEffects.TryGetValue(item.PickupId,out var effect)||effect.Snapshot.Kind!=ToolEffectKind.AerosolCloud||effect.Snapshot.SourceActorId!=actor.Spawn.ActorId||2*tick>=effect.Snapshot.EndHalfTick)
                effect=new ActiveToolEffect{Snapshot=new ToolEffectSnapshot(++toolEffectId,item.PickupId,actor.Spawn.ActorId,ToolEffectKind.AerosolCloud,origin,forward,tick,2*tick+HumanEquipmentProfile.AerosolCloudHalfTicks)};
            var previous=effect.Snapshot;
            effect.Snapshot=new ToolEffectSnapshot(previous.EffectId,item.PickupId,actor.Spawn.ActorId,ToolEffectKind.AerosolCloud,origin,forward,previous.StartTick,2*tick+HumanEquipmentProfile.AerosolCloudHalfTicks);
            toolEffects[item.PickupId]=effect;actor.TaskInterruptTick=tick;
        }
        private void UpdateToolEffects()
        {
            if(!(world is IGameplayToolEffectWorld effectWorld))return;
            foreach(var actor in actors.Values.Where(a=>a.Spawn.Role==PlayerRole.Human))EmitAerosol(actor);
            foreach(var entry in toolEffects.ToArray())
            {
                var effect=entry.Value;var state=effect.Snapshot;
                if(2*tick>=state.EndHalfTick){toolEffects.Remove(entry.Key);continue;}
                if(state.Kind==ToolEffectKind.RacketPulse)
                {
                    if(!actors.TryGetValue(state.SourceActorId,out var actor)||!actor.Connected||!CanAct(actor)||boundsHeldActors.Contains(actor.Spawn.ActorId)||actor.EquippedPickup!=state.PickupId||!pickups.TryGetValue(state.PickupId,out var item)||!ToolOrigin(actor,item,out var origin,out var forward))
                    {toolEffects.Remove(entry.Key);continue;}
                    state=new ToolEffectSnapshot(state.EffectId,state.PickupId,state.SourceActorId,state.Kind,origin,forward,state.StartTick,state.EndHalfTick);effect.Snapshot=state;
                }
                var query=new ToolEffectQuery(state.Kind,state.SourceActorId,state.PickupId,state.Origin,state.Forward,EffectRange(state.Kind),EffectAngle(state.Kind));
                var hits=effectWorld.QueryToolEffect(query);
                if(hits==null)continue;
                foreach(var hit in hits.Take(16))
                {
                    if(hit.ActorId==0||effect.HitActors.Contains(hit.ActorId)||!hit.Point.IsFinite||!ValidAim(hit.Normal)||
                        !actors.TryGetValue(hit.ActorId,out var victim)||victim.Spawn.Role!=PlayerRole.Mosquito||!CanAct(victim)||victim.Protection>0)continue;
                    var delta=hit.Point-state.Origin;
                    if(delta.Length>query.Range+.05f||delta.Length>.001f&&Float3.Dot(delta.Normalized,state.Forward)<(float)Math.Cos(query.HalfAngleDegrees*Math.PI/180))continue;
                    effect.HitActors.Add(hit.ActorId);KnockDown(victim,state.Forward*1.2f);
                }
            }
        }
        private ToolEffectSnapshot[] EffectSnapshots()=>toolEffects.Values.Select(e=>e.Snapshot).OrderBy(e=>e.EffectId).ToArray();
    }
}
