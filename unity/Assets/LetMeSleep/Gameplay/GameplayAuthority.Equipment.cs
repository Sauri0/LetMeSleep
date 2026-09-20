using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public sealed partial class GameplayAuthority
    {
        private readonly Dictionary<uint,int> projectileTicks=new Dictionary<uint,int>();
        private static bool ValidEquipmentPayload(in PlayerActionCommand c)
        {
            if(c.Kind==ActionKind.SelectInventorySlot)
                return c.SlotIndex>=-1&&c.SlotIndex<3&&c.InventoryRevision!=0&&c.TargetPickupId==0&&c.ExpectedPickupRevision==0;
            if(c.Kind==ActionKind.BeginThrow||c.Kind==ActionKind.ReleaseThrow||c.Kind==ActionKind.ConfirmPickup)
                return c.SlotIndex>=0&&c.SlotIndex<3&&c.InventoryRevision!=0&&c.TargetPickupId!=0&&c.ExpectedPickupRevision!=0;
            return c.SlotIndex==-1&&c.TargetPickupId==0&&c.ExpectedPickupRevision==0&&c.InventoryRevision==0;
        }
        private static void CancelEquipmentActions(Actor a)
        {a.ThrowCharge.Cancel();a.SwapOffer=null;a.Strike=default;a.Plan=default;a.HitActors.Clear();a.StrikeBlocked=false;}
        private void RefreshEquipment(Actor a)
        {
            a.EquippedPickup=a.Inventory.ActivePickup;
            a.EquippedTool=a.EquippedPickup!=0&&pickups.TryGetValue(a.EquippedPickup,out var item)?item.ToolId:GameplayTools.Hands;
            a.Revision++;
        }
        private void SelectInventorySlot(Actor a,in PlayerActionCommand c)
        {
            if(a.Spawn.Role!=PlayerRole.Human){a.Rejection=CommandReject.WrongRole;return;}
            if(c.InventoryRevision!=a.Inventory.Revision){a.Rejection=CommandReject.InvalidState;return;}
            if(a.Inventory.Select(c.SlotIndex)){CancelEquipmentActions(a);a.TaskInterruptTick=tick;RefreshEquipment(a);}
        }
        private bool QueryPickup(Actor a,Float3 aim,out ToolPickupSnapshot pickup)
        {
            pickup=default;
            if(!(world is IGameplayToolWorld tools))return false;
            var eye=a.Position+Float3.Up*(1.53f-.64f*a.Crouch);
            if(!tools.TryToolInteraction(new ToolInteractionQuery(a.Spawn.ActorId,eye,aim,1.5f),out var candidate))return false;
            if(!pickups.TryGetValue(candidate.PickupId,out pickup)||pickup.Revision!=candidate.Revision||pickup.OwnerActorId!=0||pickup.Phase!=ToolPickupPhase.World)
            {a.Rejection=CommandReject.InvalidState;pickup=default;return true;}
            if(!MathEx.Finite(candidate.Distance)||candidate.Distance<0||candidate.Distance>1.5f)
            {a.Rejection=CommandReject.OutOfReach;pickup=default;return true;}
            return true;
        }
        private bool TryInventoryPickup(Actor a,Float3 aim)
        {
            if(a.Spawn.Role!=PlayerRole.Human)return false;
            if(!QueryPickup(a,aim,out var pickup))return false;
            if(pickup.PickupId==0)return true;
            var decision=a.Inventory.PlanPickup(pickup.PickupId,false,a.Inventory.Revision,out var plan);
            if(decision==InventoryPlanResult.ConfirmationRequired)
            {
                a.SwapOffer=new PickupSwapOffer(pickup.PickupId,pickup.Revision,a.Inventory.Revision,a.Inventory.SelectedSlot,tick+60);
                a.Hint=InteractionHint.Tool;return true;
            }
            if(decision!=InventoryPlanResult.Ready){a.Rejection=CommandReject.InvalidState;return true;}
            CommitPickup(a,pickup,plan);return true;
        }
        private void ConfirmPickup(Actor a,in PlayerActionCommand c)
        {
            if(a.Spawn.Role!=PlayerRole.Human){a.Rejection=CommandReject.WrongRole;return;}
            var offer=a.SwapOffer;
            if(!offer.HasValue||tick>offer.Value.ExpiresAtTick||offer.Value.PickupId!=c.TargetPickupId||offer.Value.PickupRevision!=c.ExpectedPickupRevision||offer.Value.InventoryRevision!=c.InventoryRevision||offer.Value.SlotIndex!=c.SlotIndex)
            {a.Rejection=CommandReject.InvalidState;return;}
            if(!QueryPickup(a,c.AimForward,out var pickup)||pickup.PickupId!=c.TargetPickupId||pickup.Revision!=c.ExpectedPickupRevision)
            {a.Rejection=CommandReject.OutOfReach;return;}
            if(a.Inventory.PlanPickup(pickup.PickupId,true,c.InventoryRevision,out var plan)!=InventoryPlanResult.Ready)
            {a.Rejection=CommandReject.InvalidState;return;}
            CommitPickup(a,pickup,plan);
        }
        private bool TryDepositPose(Actor a,uint pickupId,out Float3 position,out Rotation rotation)
        {
            bool found;
            if(world is IGameplayEquipmentWorld equipment)found=equipment.TryDropTool(a.Spawn.ActorId,pickupId,out position,out rotation);
            else if(pickupId==a.EquippedPickup&&world is IGameplayToolWorld tools)found=tools.TryDropTool(a.Spawn.ActorId,out position,out rotation);
            else{position=default;rotation=default;return false;}
            return found&&position.IsFinite&&position.Length<=10000&&ValidRotation(rotation);
        }
        private void CommitPickup(Actor a,in ToolPickupSnapshot pickup,in InventoryPickupPlan plan)
        {
            Float3 position=default;Rotation rotation=default;ToolPickupSnapshot previous=default;
            if(plan.DisplacedPickup!=0)
            {
                if(!pickups.TryGetValue(plan.DisplacedPickup,out previous)||previous.OwnerActorId!=a.Spawn.ActorId||previous.Phase!=ToolPickupPhase.Held||!TryDepositPose(a,previous.PickupId,out position,out rotation))
                {a.Rejection=CommandReject.Obstructed;return;}
            }
            if(!a.Inventory.CommitPickup(plan,true)){a.Rejection=CommandReject.InvalidState;return;}
            CancelEquipmentActions(a);a.TaskInterruptTick=tick;
            if(previous.PickupId!=0)ApplyPickup(new ToolPickupSnapshot(previous.PickupId,previous.ToolId,position,rotation,0,previous.Revision+1,ToolPickupPhase.World,default,0,previous.ResourceUnits,false));
            ApplyPickup(new ToolPickupSnapshot(pickup.PickupId,pickup.ToolId,pickup.Position,pickup.Rotation,a.Spawn.ActorId,pickup.Revision+1,ToolPickupPhase.Held,default,0,pickup.ResourceUnits,false));
            RefreshEquipment(a);a.Hint=InteractionHint.Tool;
        }
        private void ApplyPickup(in ToolPickupSnapshot pickup)
        {pickups[pickup.PickupId]=pickup;((IGameplayToolWorld)world).ApplyToolState(pickup);}
        private void DepositActive(Actor a,bool departing)
        {
            a.ThrowCharge.Cancel();
            uint id=a.Inventory.ActivePickup;
            if(id==0||!pickups.TryGetValue(id,out var pickup)||!(world is IGameplayToolWorld))return;
            if(!TryDepositPose(a,id,out var position,out var rotation))
            {
                if(!departing){a.Rejection=CommandReject.Obstructed;return;}
                var original=config.ToolDefinitions.First(d=>d.PickupId==id);position=original.Position;rotation=original.Rotation;
            }
            if(!a.Inventory.RemoveActive(a.Inventory.Revision,id,true)){a.Rejection=CommandReject.InvalidState;return;}
            CancelEquipmentActions(a);a.TaskInterruptTick=tick;
            ApplyPickup(new ToolPickupSnapshot(id,pickup.ToolId,position,rotation,0,pickup.Revision+1,ToolPickupPhase.World,default,0,pickup.ResourceUnits,false));
            RefreshEquipment(a);a.Hint=InteractionHint.Tool;
        }
        private void DropAllTools(Actor a)
        {
            for(int slot=0;slot<3;slot++){a.Inventory.Select(slot);RefreshEquipment(a);DepositActive(a,true);}
            foreach(var item in pickups.Values.Where(p=>p.Phase==ToolPickupPhase.Projectile&&p.ThrowerActorId==a.Spawn.ActorId).ToArray())RecoverProjectile(item);
        }
        private bool MatchesActivePickup(Actor a,in PlayerActionCommand c,out ToolPickupSnapshot item)
        {
            item=default;
            return a.Spawn.Role==PlayerRole.Human&&c.InventoryRevision==a.Inventory.Revision&&c.SlotIndex==a.Inventory.SelectedSlot&&c.TargetPickupId==a.Inventory.ActivePickup&&
                pickups.TryGetValue(c.TargetPickupId,out item)&&item.OwnerActorId==a.Spawn.ActorId&&item.Phase==ToolPickupPhase.Held&&item.Revision==c.ExpectedPickupRevision;
        }
        private void BeginThrow(Actor a,in PlayerActionCommand c)
        {
            if(!(world is IGameplayEquipmentWorld)||!MatchesActivePickup(a,c,out var item)||item.ToolId!=GameplayTools.Slipper||a.Strike.Phase!=StrikePhase.None||!a.HasInput||tick-a.InputTick>HumanEquipmentProfile.InputFreshTicks)
            {a.Rejection=CommandReject.InvalidState;return;}
            if(!a.ThrowCharge.Begin(item.ToolId,item.PickupId,a.Inventory.Revision)){a.Rejection=CommandReject.InvalidState;return;}
            if(!a.Input.PrimaryHeld)a.ThrowCharge.ObserveNeutral();
            a.ChargeStartTick=tick;a.SwapOffer=null;a.TaskInterruptTick=tick;
        }
        private void AdvanceCharges()
        {
            foreach(var a in actors.Values.Where(a=>a.Spawn.Role==PlayerRole.Human))
            {
                if(a.SwapOffer.HasValue&&tick>a.SwapOffer.Value.ExpiresAtTick)a.SwapOffer=null;
                bool controls=a.Connected&&CanAct(a)&&!boundsHeldActors.Contains(a.Spawn.ActorId);
                if(tick!=a.ChargeStartTick)a.ThrowCharge.Step(a.Inventory.ActivePickup,a.Inventory.Revision,controls,a.Input.PrimaryHeld,a.HasInput&&tick-a.InputTick<=HumanEquipmentProfile.InputFreshTicks);
            }
        }
        private void AutoReleaseCharges()
        {
            foreach(var a in actors.Values.Where(a=>a.Spawn.Role==PlayerRole.Human))
                if(a.ThrowCharge.Capture().AutoReleaseDue&&a.Input.PrimaryHeld&&a.HasInput&&tick-a.InputTick<=HumanEquipmentProfile.InputFreshTicks)CommitThrow(a,a.Aim);
        }
        private void ReleaseThrow(Actor a,in PlayerActionCommand c)
        {
            if(!MatchesActivePickup(a,c,out _)){a.Rejection=CommandReject.InvalidState;return;}
            CommitThrow(a,c.AimForward);
        }
        private void CommitThrow(Actor a,Float3 aim)
        {
            if(!(world is IGameplayEquipmentWorld equipment)||!a.ThrowCharge.TryPreviewRelease(a.Inventory.ActivePickup,a.Inventory.Revision,out var charge)||!pickups.TryGetValue(charge.PickupId,out var item))
            {a.ThrowCharge.Cancel();a.Rejection=CommandReject.InvalidState;return;}
            if(!a.Stamina.CanSpend(charge.StaminaCost)){a.ThrowCharge.Cancel();a.Rejection=CommandReject.InvalidState;return;}
            bool prepared=equipment.TryPrepareThrow(a.Spawn.ActorId,item.PickupId,aim,charge.Power,out var position,out var rotation,out var velocity)
                &&position.IsFinite&&position.Length<=10000&&ValidRotation(rotation)&&velocity.IsFinite&&velocity.Length<=100;
            if(!a.ThrowCharge.CommitPreparedRelease(a.Inventory,a.Stamina,prepared,out _)){a.Rejection=prepared?CommandReject.InvalidState:CommandReject.Obstructed;return;}
            a.StaminaSpendTick=tick;a.TaskInterruptTick=tick;a.SwapOffer=null;
            ApplyPickup(new ToolPickupSnapshot(item.PickupId,item.ToolId,position,rotation,0,item.Revision+1,ToolPickupPhase.Projectile,velocity,a.Spawn.ActorId,item.ResourceUnits,false));
            projectileTicks[item.PickupId]=0;RefreshEquipment(a);
        }
        private void UpdateHumanStamina()
        {
            foreach(var a in actors.Values.Where(a=>a.Spawn.Role==PlayerRole.Human))
            {
                bool controls=a.Connected&&CanAct(a)&&!boundsHeldActors.Contains(a.Spawn.ActorId);
                bool moving=controls&&(a.Input.MovePlanar.X*a.Input.MovePlanar.X+a.Input.MovePlanar.Y*a.Input.MovePlanar.Y>.0001f);
                a.SprintFraction=a.Stamina.Step(a.Input.SprintHeld,moving,a.Input.CrouchHeld||a.Crouch>.1f,controls,a.StaminaSpendTick!=tick);
            }
        }
        private void RecoverProjectile(ToolPickupSnapshot item)
        {
            var original=config.ToolDefinitions.First(d=>d.PickupId==item.PickupId);
            ApplyPickup(new ToolPickupSnapshot(item.PickupId,item.ToolId,original.Position,original.Rotation,0,item.Revision+1,ToolPickupPhase.World,default,0,item.ResourceUnits,false));
            projectileTicks.Remove(item.PickupId);
        }
        private void UpdateProjectiles(float dt)
        {
            if(!(world is IGameplayEquipmentWorld equipment))return;
            foreach(var item in pickups.Values.Where(p=>p.Phase==ToolPickupPhase.Projectile).ToArray())
            {
                int age=projectileTicks.TryGetValue(item.PickupId,out var previousAge)?previousAge+1:1;
                if(age>300){RecoverProjectile(item);continue;}
                projectileTicks[item.PickupId]=age;
                var velocity=item.Velocity-new Float3(0,12*dt,0);var displacement=velocity*dt;
                var position=item.Position+displacement;bool consumed=item.ImpactConsumed;var phase=ToolPickupPhase.Projectile;
                if(equipment.SweepProjectile(new ToolProjectileQuery(item.PickupId,item.ThrowerActorId,item.Position,item.Rotation,displacement),out var hit))
                {
                    if(hit.StartedOverlapping){RecoverProjectile(item);continue;}
                    if(!hit.Point.IsFinite||!ValidAim(hit.Normal)||!MathEx.Finite(hit.Fraction)||hit.Fraction<0||hit.Fraction>1){RecoverProjectile(item);continue;}
                    position=hit.Point;
                    if(!consumed&&hit.ActorId!=0&&actors.TryGetValue(hit.ActorId,out var victim)&&victim.Spawn.Role==PlayerRole.Mosquito&&CanAct(victim)&&victim.Protection<=0)
                        KnockDown(victim,velocity.Normalized*1.2f);
                    consumed=true;
                    if(hit.ActorId==0&&hit.Normal.Y>=.55f){phase=ToolPickupPhase.World;velocity=default;projectileTicks.Remove(item.PickupId);}
                    else velocity=Float3.ProjectPlane(velocity,hit.Normal)*.35f;
                }
                if(!position.IsFinite||position.Length>10000){RecoverProjectile(item);continue;}
                ApplyPickup(new ToolPickupSnapshot(item.PickupId,item.ToolId,position,item.Rotation,0,item.Revision+1,phase,velocity,phase==ToolPickupPhase.Projectile?item.ThrowerActorId:0,item.ResourceUnits,phase==ToolPickupPhase.Projectile&&consumed));
            }
        }
    }
}
