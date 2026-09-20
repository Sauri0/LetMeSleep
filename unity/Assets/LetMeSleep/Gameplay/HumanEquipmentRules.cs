using System;
using System.Security.Cryptography;
using System.Text;

namespace LetMeSleep.Gameplay
{
    // Pure host rules; world/authority integration is separate. Fixed host ticks: 30 Hz. One point = 300 units.
    public static class HumanEquipmentProfile
    {
        public const int UnitsPerPoint=300, TickRate=30, Maximum=30000;
        public const int SprintPerTick=160, WalkRecoveryPerTick=120, RestRecoveryPerTick=280;
        public const int JumpCost=3000, ThrowMinimumCost=2400, ThrowMaximumCost=4500;
        public const int ChargeFullTicks=27, ChargeLimitTicks=45, ReleaseWaitTicks=30, InputFreshTicks=6;
        // Includes effective interpolation, exhaustion policy and tick precision, not only labels.
        public const string CanonicalBalance="human-equipment-1:30:300:30000:160:120:280:3000:2400:4500:27:45:30:6:smoothstep35:auto-release45:explicit-release:false-freezes:stale-cancels:insufficient-cancel:release-sprint-latch:atomic-deposit";
        public static readonly string Hash=ComputeHash();
        private static string ComputeHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalBalance))).Replace("-","").ToLowerInvariant();}
        public static float ChargeFraction(int ticks)
        {
            float t=Math.Max(0,Math.Min(ChargeFullTicks,ticks))/(float)ChargeFullTicks;
            return t*t*(3-2*t);
        }
        public static float ThrowPower(int ticks)=>.35f+.65f*ChargeFraction(ticks);
        public static int ThrowCost(int ticks)=>ThrowMinimumCost+(int)Math.Ceiling((ThrowMaximumCost-ThrowMinimumCost)*ChargeFraction(ticks));
    }

    public sealed class HumanStamina
    {
        public int Units {get;private set;}=HumanEquipmentProfile.Maximum;
        public bool SprintExhausted {get;private set;}
        public float Points=>Units/(float)HumanEquipmentProfile.UnitsPerPoint;
        public bool CanSpend(int cost)=>cost>=0&&cost<=Units;
        public bool TrySpend(int cost)
        {
            if(cost<0)throw new ArgumentOutOfRangeException(nameof(cost));
            if(!CanSpend(cost))return false;
            Units-=cost;return true;
        }
        // Returns fraction of this tick at sprint speed; the final partial tick is charged exactly.
        // Caller supplies intent after movement/role/state validation. Inactive actors do not sprint.
        public float Step(bool sprintHeld,bool moving,bool crouching,bool controlsAvailable)
            =>Step(sprintHeld,moving,crouching,controlsAvailable,true);
        public float Step(bool sprintHeld,bool moving,bool crouching,bool controlsAvailable,bool allowRecovery)
        {
            if(!sprintHeld)SprintExhausted=false;
            bool sprint=controlsAvailable&&sprintHeld&&moving&&!crouching&&!SprintExhausted;
            if(sprint&&Units>0)
            {
                int used=Math.Min(Units,HumanEquipmentProfile.SprintPerTick);Units-=used;
                if(Units==0)SprintExhausted=true;
                return used/(float)HumanEquipmentProfile.SprintPerTick;
            }
            if(sprint&&Units==0)SprintExhausted=true;
            if(!allowRecovery)return 0;
            int recovery=!moving||crouching?HumanEquipmentProfile.RestRecoveryPerTick:HumanEquipmentProfile.WalkRecoveryPerTick;
            Units=Math.Min(HumanEquipmentProfile.Maximum,Units+recovery);return 0;
        }
    }

    public readonly struct HumanInventorySnapshot
    {
        public readonly uint Revision,Slot0,Slot1,Slot2;
        public readonly int SelectedSlot;
        public HumanInventorySnapshot(uint revision,uint slot0,uint slot1,uint slot2,int selected)
        {Revision=revision;Slot0=slot0;Slot1=slot1;Slot2=slot2;SelectedSlot=selected;}
        public uint ActivePickup=>SelectedSlot==0?Slot0:SelectedSlot==1?Slot1:SelectedSlot==2?Slot2:0;
    }
    public enum InventoryPlanResult:byte { Ready,InvalidPickup,AlreadyOwned,SelectSlot,ConfirmationRequired,StaleRevision }
    public readonly struct InventoryPickupPlan
    {
        public readonly uint InventoryRevision,IncomingPickup,DisplacedPickup;
        public readonly int Slot;
        internal InventoryPickupPlan(uint revision,uint incoming,uint displaced,int slot)
        {InventoryRevision=revision;IncomingPickup=incoming;DisplacedPickup=displaced;Slot=slot;}
    }
    public sealed class HumanInventory
    {
        private readonly uint[] slots=new uint[3];
        public uint Revision {get;private set;}=1;
        public int SelectedSlot {get;private set;}=-1;
        public uint ActivePickup=>SelectedSlot<0?0:slots[SelectedSlot];
        public HumanInventorySnapshot Capture()=>new HumanInventorySnapshot(Revision,slots[0],slots[1],slots[2],SelectedSlot);
        public bool Select(int slot)
        {
            if(slot< -1||slot>2)throw new ArgumentOutOfRangeException(nameof(slot));
            if(slot==SelectedSlot)return false;
            SelectedSlot=slot;Revision++;return true;
        }
        public InventoryPlanResult PlanPickup(uint pickup,bool confirmedReplacement,uint expectedRevision,out InventoryPickupPlan plan)
        {
            plan=default;
            if(pickup==0)return InventoryPlanResult.InvalidPickup;
            if(Array.IndexOf(slots,pickup)>=0)return InventoryPlanResult.AlreadyOwned;
            if(expectedRevision!=Revision)return InventoryPlanResult.StaleRevision;
            int slot=SelectedSlot>=0&&slots[SelectedSlot]==0?SelectedSlot:Array.IndexOf(slots,0u);
            if(slot<0)
            {
                if(SelectedSlot<0)return InventoryPlanResult.SelectSlot;
                if(!confirmedReplacement)return InventoryPlanResult.ConfirmationRequired;
                slot=SelectedSlot;
            }
            plan=new InventoryPickupPlan(Revision,pickup,slots[slot],slot);return InventoryPlanResult.Ready;
        }
        // The host must validate incoming pickup ownership/revision plus deposit pose BEFORE commit.
        // A caller cannot use this pure collection as proof that a physical drop is safe.
        public bool CommitPickup(in InventoryPickupPlan plan,bool displacementSafe)
        {
            if(plan.InventoryRevision!=Revision||plan.IncomingPickup==0||plan.Slot<0||plan.Slot>2||
               slots[plan.Slot]!=plan.DisplacedPickup||Array.IndexOf(slots,plan.IncomingPickup)>=0||
               (plan.DisplacedPickup!=0&&!displacementSafe))return false;
            slots[plan.Slot]=plan.IncomingPickup;SelectedSlot=plan.Slot;Revision++;return true;
        }
        public bool RemoveActive(uint expectedRevision,uint expectedPickup,bool worldAccepted)
        {
            if(!worldAccepted||Revision!=expectedRevision||SelectedSlot<0||expectedPickup==0||ActivePickup!=expectedPickup)return false;
            slots[SelectedSlot]=0;SelectedSlot=-1;Revision++;return true;
        }
    }

    public readonly struct ThrowChargeSnapshot
    {
        public readonly uint PickupId,InventoryRevision;
        public readonly int ElapsedTicks;
        public readonly bool AwaitingRelease;
        public readonly int ReleaseWaitTicks;
        public bool Active=>PickupId!=0;
        public bool LimitReached=>Active&&ElapsedTicks>=HumanEquipmentProfile.ChargeLimitTicks;
        public bool AutoReleaseDue=>LimitReached&&!AwaitingRelease;
        public float Power=>Active?HumanEquipmentProfile.ThrowPower(ElapsedTicks):0;
        public int StaminaCost=>Active?HumanEquipmentProfile.ThrowCost(ElapsedTicks):0;
        public ThrowChargeSnapshot(uint pickup,uint revision,int ticks,bool awaitingRelease=false,int releaseWaitTicks=0){PickupId=pickup;InventoryRevision=revision;ElapsedTicks=ticks;AwaitingRelease=awaitingRelease;ReleaseWaitTicks=releaseWaitTicks;}
    }
    public sealed class HumanThrowCharge
    {
        public const string Slipper="slipper"; // Stable proposed catalog ID; only approved throwable J21=A.
        private uint pickup,revision;
        private int elapsed;
        private bool awaitingRelease;
        private int releaseWait;
        public ThrowChargeSnapshot Capture()=>new ThrowChargeSnapshot(pickup,revision,elapsed,awaitingRelease,releaseWait);
        public bool Begin(string toolId,uint pickupId,uint inventoryRevision)
        {
            if(pickup!=0||toolId!=Slipper||pickupId==0||inventoryRevision==0)return false;
            pickup=pickupId;revision=inventoryRevision;elapsed=releaseWait=0;awaitingRelease=false;return true;
        }
        public void Step(uint activePickup,uint inventoryRevision,bool controlsAvailable)
            =>Step(activePickup,inventoryRevision,controlsAvailable,true,true);
        public void Step(uint activePickup,uint inventoryRevision,bool controlsAvailable,bool primaryHeld,bool inputFresh)
        {
            if(pickup==0)return;
            if(!controlsAvailable||!inputFresh||pickup!=activePickup||revision!=inventoryRevision){Cancel();return;}
            // Neutral input can overtake a reliable CancelThrow. Never infer release from it.
            if(!primaryHeld)awaitingRelease=true;
            if(awaitingRelease)
            {
                if(++releaseWait>=HumanEquipmentProfile.ReleaseWaitTicks)Cancel();
                return;
            }
            elapsed=Math.Min(HumanEquipmentProfile.ChargeLimitTicks,elapsed+1);
        }
        public void Cancel(){pickup=revision=0;elapsed=releaseWait=0;awaitingRelease=false;}
        public void ObserveNeutral(){if(pickup!=0)awaitingRelease=true;}
        // No resource mutation here: authority performs release as one transaction after world validation.
        public bool TryPreviewRelease(uint activePickup,uint inventoryRevision,out ThrowChargeSnapshot charge)
        {
            charge=Capture();
            if(!charge.Active||activePickup!=pickup||inventoryRevision!=revision){charge=default;return false;}
            return true;
        }
        // Host calls on release edge or LimitReached while input remains valid. The world has
        // already prepared a non-failing projectile insertion; no raycast/physics stub is hidden here.
        public bool CommitPreparedRelease(HumanInventory inventory,HumanStamina stamina,bool worldPrepared,out ThrowChargeSnapshot released)
        {
            if(inventory==null||stamina==null)throw new ArgumentNullException();
            released=default;
            if(!worldPrepared||!TryPreviewRelease(inventory.ActivePickup,inventory.Revision,out var charge)||!stamina.CanSpend(charge.StaminaCost))
            {Cancel();return false;}
            if(!inventory.RemoveActive(charge.InventoryRevision,charge.PickupId,true)){Cancel();return false;}
            if(!stamina.TrySpend(charge.StaminaCost))throw new InvalidOperationException("Single-thread host transaction invariant violated.");
            released=charge;Cancel();return true;
        }
    }
}
