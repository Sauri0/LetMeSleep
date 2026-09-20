using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Linq;
#endif
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public readonly struct BotTarget
    {
        public readonly ActorSnapshot Actor;
        public readonly Float3 ContactPoint;
        public BotTarget(ActorSnapshot actor, Float3 contactPoint) { Actor = actor; ContactPoint = contactPoint; }
    }
    public readonly struct BotToolOpportunity
    {
        public readonly ToolPickupSnapshot Pickup;
        public readonly float DetourMeters;
        public readonly Float3 ContactPoint;
        public BotToolOpportunity(ToolPickupSnapshot pickup, float detourMeters) : this(pickup, detourMeters, pickup.Position) { }
        public BotToolOpportunity(ToolPickupSnapshot pickup, float detourMeters, Float3 contactPoint)
        {
            if (!contactPoint.IsFinite) throw new ArgumentException("Tool contact point must be finite.", nameof(contactPoint));
            Pickup = pickup; DetourMeters = detourMeters; ContactPoint = contactPoint;
        }
    }
    public sealed class BotTrainingContext
    {
        public IReadOnlyList<BotToolOpportunity> VisibleTools { get; }
        public float RescueTravelSpeed { get; }
        public ToolPickupSnapshot? EquippedPickup { get; }
        public BotTrainingContext(IReadOnlyList<BotToolOpportunity> visibleTools, float rescueTravelSpeed)
            : this(visibleTools, rescueTravelSpeed, null) { }
        public BotTrainingContext(IReadOnlyList<BotToolOpportunity> visibleTools, float rescueTravelSpeed, ToolPickupSnapshot? equippedPickup)
        {
            EquippedPickup = equippedPickup;
            if (!MathEx.Finite(rescueTravelSpeed) || rescueTravelSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(rescueTravelSpeed));
            VisibleTools = Array.AsReadOnly(GameplayRoundConfig.Copy(visibleTools)); RescueTravelSpeed = rescueTravelSpeed;
        }
    }
    public sealed class BotObservation
    {
        public ActorSnapshot Self { get; }
        public IReadOnlyList<BotTarget> Visible { get; }
        public Float3 FreeDirection { get; }
        public bool DoorAhead { get; }
        public Func<Float3, Float3> Steer { get; }
        public string ModeId { get; }
        public TaskAssignment OwnAssignment { get; }
        public ObjectiveDefinition TaskObjective { get; }
        public Func<ObjectiveDefinition, Float3> TaskDirection { get; }
        public ActorPrivateState OwnPrivate { get; }
        public BotTrainingContext Training { get; }
        public BotObservation(ActorSnapshot self, IReadOnlyList<BotTarget> visible, Float3 freeDirection, bool doorAhead, Func<Float3, Float3> steer = null, string modeId = GameModes.Blood, ActorPrivateState ownPrivate = null, ObjectiveDefinition taskObjective = null, Func<ObjectiveDefinition, Float3> taskDirection = null)
            : this(self, visible, freeDirection, doorAhead, steer, modeId, ownPrivate, taskObjective, taskDirection, null) { }
        public BotObservation(ActorSnapshot self, IReadOnlyList<BotTarget> visible, Float3 freeDirection, bool doorAhead, Func<Float3, Float3> steer, string modeId, ActorPrivateState ownPrivate, ObjectiveDefinition taskObjective, Func<ObjectiveDefinition, Float3> taskDirection, BotTrainingContext training)
        { OwnPrivate = ownPrivate; Training = training; Self = self; Visible = Array.AsReadOnly(GameplayRoundConfig.Copy(visible)); FreeDirection = freeDirection; DoorAhead = doorAhead; Steer = steer; ModeId = modeId;
            if (!GameModes.IsValid(modeId) || (ownPrivate != null && ownPrivate.ActorId != self.ActorId)) throw new ArgumentException("Bot observation identity mismatch.");
            if (modeId == GameModes.Tasks && self.Role == PlayerRole.Human && ownPrivate?.TaskAssignment != null && taskObjective?.ObjectiveId == ownPrivate.TaskAssignment.ObjectiveId)
            { OwnAssignment = ownPrivate.TaskAssignment; TaskObjective = taskObjective; TaskDirection = taskDirection; }
        }
    }
    public readonly struct BotTick
    {
        public readonly ulong Epoch, Round;
        public readonly uint Tick;
        public BotTick(ulong epoch, ulong round, uint tick) { Epoch = epoch; Round = round; Tick = tick; }
    }
    public readonly struct BotCommands
    {
        public readonly PlayerInputCommand Input;
        public readonly PlayerActionCommand? Action;
        public BotCommands(PlayerInputCommand input, PlayerActionCommand? action) { Input = input; Action = action; }
    }
    public interface IBotController { BotCommands Decide(BotObservation observation, in BotTick tick); }
    public sealed class BotController : IBotController
    {
        private uint inputSequence, actionSequence, nextStrike, nextThrowAttempt;
        private uint handsRequestInventoryRevision, handsRequestPickup, handsRequestViewRevision;
        private bool releaseBite;
        public const uint MemoryTicks = 90, RepeatVictimTicks = 240, ReplanTicks = 150, BlockTicks = 180;
        public uint SelectedActorId { get; private set; }
        public uint ReplanCount { get; private set; }
        public uint BlockedUntilTick { get; private set; }
        public Float3 BlockedDirection => blockedDirection;
        private sealed class Memory { public BotTarget Target; public uint LastSeen, ReadyTick; public bool Visible; }
        private readonly Dictionary<uint, Memory> memory = new Dictionary<uint, Memory>();
        private readonly Dictionary<uint, uint> recentlyBitten = new Dictionary<uint, uint>();
        private ulong epoch, round; private uint actorId, progressTick; private bool tracking;
        private Float3 progressPosition, blockedDirection;
#if UNITY_EDITOR
        private bool captureDecisionDiagnostic = false;
        private string lastDecisionDiagnostic;
#endif
        public static uint ReactionTicks(uint observer, uint target) => 8 + (observer * 17u + target * 31u) % 5;
        private void ResetIdentity(ActorSnapshot self, in BotTick tick)
        {
            if (actorId == self.ActorId && epoch == tick.Epoch && round == tick.Round) return;
            actorId = self.ActorId; epoch = tick.Epoch; round = tick.Round;
            memory.Clear(); recentlyBitten.Clear(); tracking = false; BlockedUntilTick = 0; ReplanCount = 0;
            inputSequence = actionSequence = nextStrike = nextThrowAttempt = 0;
            handsRequestInventoryRevision = handsRequestPickup = handsRequestViewRevision = 0; releaseBite = false;
        }
        private void Remember(BotObservation observation, uint tick)
        {
            var expired = new List<uint>();
            foreach (var pair in memory) { pair.Value.Visible = false; if (tick - pair.Value.LastSeen >= MemoryTicks) expired.Add(pair.Key); }
            foreach (var id in expired) memory.Remove(id);
            foreach (var visible in observation.Visible)
            {
                if (visible.Actor == null || visible.Actor.ActorId == observation.Self.ActorId) continue;
                if (!memory.TryGetValue(visible.Actor.ActorId, out var item))
                    memory.Add(visible.Actor.ActorId, item = new Memory { ReadyTick = tick + ReactionTicks(observation.Self.ActorId, visible.Actor.ActorId) });
                // Snapshot/contact are immutable observations. Never query a hidden actor to refresh these.
                item.Target = visible; item.LastSeen = tick; item.Visible = true;
            }
        }
        private BotTarget? SelectTarget(BotObservation o, uint tick, out bool visibleNow)
        {
            BotTarget? result = null; visibleNow = false; float best = float.MaxValue;
            foreach (var pair in memory)
            {
                var item = pair.Value; var target = item.Target; var actor = target.Actor;
                if (tick < item.ReadyTick || actor.Eliminated) continue;
                bool rescue = o.Self.Role == PlayerRole.Mosquito && actor.Role == PlayerRole.Mosquito && actor.LifeState == LifeState.Stunned;
                if (rescue)
                {
                    // Rescue requires current visible risk and a known expiry, never a remembered body.
                    if (!item.Visible || actor.RecoveryEndTick <= tick) continue;
                    float speed = o.Training?.RescueTravelSpeed ?? 1;
                    float arrivalTicks = (target.ContactPoint - o.Self.Position).Length / speed * 30;
                    if (actor.RecoveryEndTick - tick < arrivalTicks + 45) continue;
                    bool danger = false;
                    foreach (var enemy in o.Visible)
                        if (enemy.Actor.Role == PlayerRole.Human && !enemy.Actor.Eliminated && (enemy.Actor.Position - actor.Position).Length < 2.5f) danger = true;
                    if (danger) continue;
                }
                else if (actor.Role == o.Self.Role || actor.LifeState == LifeState.Falling || actor.LifeState == LifeState.Fainted || actor.LifeState == LifeState.Recovering || actor.LifeState == LifeState.Stunned) continue;
                float score = (target.ContactPoint - o.Self.Position).Length;
                if (rescue) score *= .5f;
                if (o.Self.Role == PlayerRole.Human && item.Visible && IsThreat(o.Self, target)) score -= 100;
                if (o.Self.Role == PlayerRole.Mosquito && actor.Role == PlayerRole.Human)
                {
                    if (recentlyBitten.TryGetValue(actor.ActorId, out var last) && tick - last < RepeatVictimTicks) score += 8;
                    if (actor.StrikeState.Phase != StrikePhase.None) score += 3;
                    // Public stationary behaviour is observable; private task assignments are never inspected.
                    if (actor.Velocity.LengthSquared < .01f) score -= .25f;
                }
                if (!item.Visible) score += 2;
                if (score < best || (score == best && actor.ActorId < (result?.Actor.ActorId ?? uint.MaxValue)))
                { best = score; result = target; visibleNow = item.Visible; }
            }
            return result;
        }
        private static bool IsThreat(ActorSnapshot self, BotTarget target)
        {
            var toward = self.Position - target.Actor.Position;
            return toward.Length < 2 || Float3.Dot(target.Actor.Velocity, toward.Normalized) > .1f
                || (target.Actor.BiteAttachment.HasValue && target.Actor.BiteAttachment.Value.VictimId == self.ActorId);
        }
        private static bool TryOwnEquipment(BotObservation observation, out ToolPickupSnapshot pickup)
        {
            pickup = observation.Training?.EquippedPickup ?? default;
            return observation.OwnPrivate != null && pickup.PickupId != 0 && pickup.OwnerActorId == observation.Self.ActorId
                && pickup.Phase == ToolPickupPhase.Held && pickup.ToolId == observation.Self.EquippedToolId
                && pickup.PickupId == observation.OwnPrivate.Inventory.ActivePickup && observation.OwnPrivate.Inventory.Revision != 0;
        }
        private static bool HasFreeSlot(ActorPrivateState state) => state != null
            && (state.Inventory.Slot0 == 0 || state.Inventory.Slot1 == 0 || state.Inventory.Slot2 == 0);
        private void TrackProgress(BotObservation observation, uint tick, Float3 desired, ref float forward)
        {
            if (tick < BlockedUntilTick && Float3.Dot(desired, blockedDirection) > .8f) { forward = 0; tracking = false; return; }
            if (forward <= 0) { tracking = false; return; }
            if (!tracking || (observation.Self.Position - progressPosition).Length >= .15f)
            { tracking = true; progressTick = tick; progressPosition = observation.Self.Position; return; }
            if (tick - progressTick < ReplanTicks) return;
            BlockedUntilTick = tick + BlockTicks; blockedDirection = desired; ReplanCount++; tracking = false; forward = 0;
        }
        public BotCommands Decide(BotObservation observation, in BotTick tick)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            var self = observation.Self;
            ResetIdentity(self, tick);
            Remember(observation, tick.Tick);
            if (self.BiteAttachment.HasValue) recentlyBitten[self.BiteAttachment.Value.VictimId] = tick.Tick;
            BotTarget? selected = SelectTarget(observation, tick.Tick, out bool selectedVisible);
            SelectedActorId = selected?.Actor.ActorId ?? 0;
            bool threat = selectedVisible && selected.HasValue && IsThreat(self, selected.Value);
            var origin = self.Position + (self.Role == PlayerRole.Human ? Float3.Up * (1.53f - .64f * self.CrouchFraction) : Float3.Zero);
            var direction = selected.HasValue ? (selected.Value.ContactPoint - origin).Normalized : observation.FreeDirection.Normalized;
            bool task = observation.TaskObjective != null && observation.OwnAssignment.Status == TaskAssignmentStatus.Active
                && self.Role == PlayerRole.Human && !threat;
            var taskTravel = task && observation.TaskDirection != null ? observation.TaskDirection(observation.TaskObjective) : Float3.Zero;
#if UNITY_EDITOR
            uint diagnosticTool = 0;
            Float3 diagnosticBeforeSteer = default, diagnosticSteer = default;
            bool diagnosticSteerCalled = false;
#endif
            if (task) direction = taskTravel.Normalized;
            if (observation.ModeId == GameModes.Survival && self.Role == PlayerRole.Mosquito && selected.HasValue && selected.Value.Actor.Role == PlayerRole.Human) direction = -direction;
            if (direction.LengthSquared < .5f) direction = Float3.Forward;
            float yaw = (float)Math.Atan2(direction.X, direction.Z), pitch = (float)Math.Asin(MathEx.Clamp(direction.Y, -1, 1));
            pitch = MathEx.Clamp(pitch, -1.919862f, self.Role == PlayerRole.Human ? 1.308996f : 1.553343f); direction = MathEx.Aim(yaw, pitch);
            bool primaryHeld = false, equipmentPayload = false;
            ToolPickupSnapshot equipped = default;
            bool validEquipment = TryOwnEquipment(observation, out equipped);
            bool bite = false, helpHeld = false; float forward = selected.HasValue ? 1 : .5f;
            ActionKind? action = null;
            if (self.Role == PlayerRole.Mosquito)
            {
                if (selectedVisible && selected.HasValue && (observation.ModeId != GameModes.Survival || selected.Value.Actor.Role == PlayerRole.Mosquito))
                {
                    var target = selected.Value; float distance = (target.ContactPoint - origin).Length;
                    helpHeld = target.Actor.Role == PlayerRole.Mosquito && distance < .55f;
                    bite = target.Actor.Role == PlayerRole.Human && distance < .15f;
                    if (helpHeld || bite) forward = 0;
                }
                if (self.BiteAttachment.HasValue) { bite = true; forward = 0; }
                // Periodic release permits a fresh attempt after a host-forced detach.
                if (self.LifeState == LifeState.Flying && releaseBite) bite = false;
                releaseBite = bite && !self.BiteAttachment.HasValue;
            }
            else if (selectedVisible && selected.HasValue && !task)
            {
                float distance = (selected.Value.ContactPoint - origin).Length;
                float reach = self.EquippedToolId == GameplayTools.Aerosol ? HumanEquipmentProfile.AerosolRange
                    : self.EquippedToolId == GameplayTools.ElectricRacket ? HumanEquipmentProfile.RacketRange
                    : self.EquippedToolId == GameplayTools.Slipper ? 4 : .8f;
                if (distance < reach) forward = 0;
                if (self.EquippedToolId == GameplayTools.Aerosol)
                    primaryHeld = distance <= reach && validEquipment && equipped.ResourceUnits > 0;
                else if (self.EquippedToolId == GameplayTools.Slipper)
                {
                    bool charge = observation.OwnPrivate?.ThrowCharge.Active == true;
                    if (distance <= reach && validEquipment)
                    {
                        if (charge)
                        {
                            var current = observation.OwnPrivate.ThrowCharge;
                            if (current.PickupId != equipped.PickupId || current.InventoryRevision != observation.OwnPrivate.Inventory.Revision)
                                action = ActionKind.CancelThrow;
                            else if (current.ElapsedTicks >= HumanEquipmentProfile.ChargeFullTicks || current.AwaitingRelease)
                            { action = ActionKind.ReleaseThrow; equipmentPayload = true; }
                            else primaryHeld = true;
                        }
                        else if (tick.Tick >= nextThrowAttempt && observation.OwnPrivate.StaminaUnits >= HumanEquipmentProfile.ThrowMaximumCost)
                        { action = ActionKind.BeginThrow; primaryHeld = true; equipmentPayload = true; nextThrowAttempt = tick.Tick + 30; }
                    }
                }
                else if (distance <= reach && tick.Tick >= nextStrike
                    && (self.EquippedToolId != GameplayTools.ElectricRacket || (validEquipment && equipped.ResourceUnits > 0 && tick.Tick >= equipped.CooldownUntilTick)))
                { action = ActionKind.Primary; nextStrike = tick.Tick + 24; }
            }
            if (task)
            {
                forward = taskTravel.LengthSquared < .0001f ? 0 : 1;
                if ((observation.TaskObjective.Position - self.Position).Length <= observation.TaskObjective.UseRadius * .9f)
                {
                    forward = 0; helpHeld = true;
                    var aim = (observation.TaskObjective.Position - origin).Normalized;
                    if (aim.LengthSquared > .5f) { yaw = (float)Math.Atan2(aim.X, aim.Z); pitch = MathEx.Clamp((float)Math.Asin(MathEx.Clamp(aim.Y, -1, 1)), -1.919862f, 1.308996f); direction = MathEx.Aim(yaw, pitch); }
                }
            }
            if (!threat && !helpHeld && self.EquippedToolId == GameplayTools.Hands && self.Role == PlayerRole.Human && observation.Training != null && HasFreeSlot(observation.OwnPrivate))
            {
                BotToolOpportunity? tool = null;
                foreach (var candidate in observation.Training.VisibleTools)
                    if (candidate.Pickup.Phase == ToolPickupPhase.World && candidate.DetourMeters >= 0 && candidate.DetourMeters < 4
                        && GameplayTools.IsPickup(candidate.Pickup.ToolId)
                        && ((candidate.Pickup.ToolId != GameplayTools.Aerosol && candidate.Pickup.ToolId != GameplayTools.ElectricRacket) || candidate.Pickup.ResourceUnits > 0) && (!tool.HasValue || candidate.DetourMeters < tool.Value.DetourMeters)) tool = candidate;
                if (tool.HasValue)
                {
#if UNITY_EDITOR
                    diagnosticTool = tool.Value.Pickup.PickupId;
#endif
                    var delta = tool.Value.ContactPoint - origin; direction = delta.Normalized;
                    yaw = (float)Math.Atan2(direction.X, direction.Z); pitch = MathEx.Clamp((float)Math.Asin(MathEx.Clamp(direction.Y, -1, 1)), -1.919862f, 1.308996f);
                    direction = MathEx.Aim(yaw, pitch); forward = 1; helpHeld = false;
                    if (delta.Length < 1.4f) { action = ActionKind.Use; forward = 0; }
                }
            }
            if (observation.OwnPrivate?.ThrowCharge.Active == true && !primaryHeld && action != ActionKind.ReleaseThrow)
            { action = ActionKind.CancelThrow; equipmentPayload = false; }
            if (observation.DoorAhead && self.Role == PlayerRole.Human && !primaryHeld && !action.HasValue) action = ActionKind.Use;
            // Selection is an ordinary authority transaction and cancels held actions for free.
            // Do not repeat the same revision/owner request while its resulting state is pending.
            if (validEquipment && equipped.ResourceUnits == 0
                && (equipped.ToolId == GameplayTools.Aerosol || equipped.ToolId == GameplayTools.ElectricRacket)
                && (handsRequestInventoryRevision != observation.OwnPrivate.Inventory.Revision
                    || handsRequestPickup != equipped.PickupId || handsRequestViewRevision != self.ViewRevision))
            { action = ActionKind.SelectInventorySlot; primaryHeld = false; helpHeld = false; equipmentPayload = false; }
            if (self.Eliminated || self.LifeState == LifeState.Falling || self.LifeState == LifeState.Stunned || self.LifeState == LifeState.Fainted || self.LifeState == LifeState.Recovering) { forward = 0; bite = false; helpHeld = false; primaryHeld = false; action = null; }
            TrackProgress(observation, tick.Tick, direction, ref forward);
#if UNITY_EDITOR
            diagnosticBeforeSteer = direction;
#endif
            if (forward > 0 && observation.Steer != null && !action.HasValue)
            {
                var travel = observation.Steer(direction);
#if UNITY_EDITOR
                diagnosticSteerCalled = true; diagnosticSteer = travel;
#endif
                if (travel.LengthSquared < .01f) forward = 0;
                else
                {
                    travel = travel.Normalized;
                    yaw = (float)Math.Atan2(travel.X, travel.Z);
                    pitch = MathEx.Clamp((float)Math.Asin(MathEx.Clamp(travel.Y, -1, 1)), -1.919862f, self.Role == PlayerRole.Human ? 1.308996f : 1.553343f);
                    direction = MathEx.Aim(yaw, pitch);
                }
            }
            var h = new CommandHeader(tick.Epoch, tick.Round, self.ActorId, ++inputSequence, tick.Tick, self.ViewRevision);
            var input = new PlayerInputCommand(h, new Float2(0, forward), 0, yaw, pitch, direction, false, false, bite, helpHeld, primaryHeld);
            PlayerActionCommand? command = action.HasValue ? new PlayerActionCommand(new CommandHeader(tick.Epoch, tick.Round, self.ActorId, ++actionSequence, tick.Tick, self.ViewRevision), action.Value, direction) : (PlayerActionCommand?)null;
            if (equipmentPayload && action.HasValue)
                command = new PlayerActionCommand(new CommandHeader(tick.Epoch, tick.Round, self.ActorId, actionSequence, tick.Tick, self.ViewRevision), action.Value, direction,
                    observation.OwnPrivate.Inventory.SelectedSlot, equipped.PickupId, equipped.Revision, observation.OwnPrivate.Inventory.Revision);
            if (action == ActionKind.SelectInventorySlot)
            {
                handsRequestInventoryRevision = observation.OwnPrivate.Inventory.Revision;
                handsRequestPickup = equipped.PickupId; handsRequestViewRevision = self.ViewRevision;
                command = new PlayerActionCommand(new CommandHeader(tick.Epoch, tick.Round, self.ActorId, actionSequence, tick.Tick, self.ViewRevision), action.Value, direction,
                    -1, 0, 0, observation.OwnPrivate.Inventory.Revision);
            }
#if UNITY_EDITOR
            if (captureDecisionDiagnostic)
            {
                string tools = observation.Training == null ? "none" : string.Join(";", observation.Training.VisibleTools.Select(item =>
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:detour={1:R}",
                        item.Pickup.PickupId, item.DetourMeters)));
                lastDecisionDiagnostic = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "task={0} taskTravel={1:R},{2:R},{3:R} tools=[{4}] selectedTool={5} " +
                    "preSteer={6:R},{7:R},{8:R} steerCalled={9} steer={10:R},{11:R},{12:R} " +
                    "finalAim={13:R},{14:R},{15:R} forward={16:R} action={17}",
                    task ? 1 : 0, taskTravel.X, taskTravel.Y, taskTravel.Z, tools, diagnosticTool,
                    diagnosticBeforeSteer.X, diagnosticBeforeSteer.Y, diagnosticBeforeSteer.Z,
                    diagnosticSteerCalled ? 1 : 0, diagnosticSteer.X, diagnosticSteer.Y, diagnosticSteer.Z,
                    direction.X, direction.Y, direction.Z, forward,
                    action.HasValue ? action.Value.ToString() : "none");
            }
#endif
            return new BotCommands(input, command);
        }
    }
}
