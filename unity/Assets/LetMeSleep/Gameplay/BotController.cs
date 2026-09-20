using System;
using System.Collections.Generic;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public readonly struct BotTarget
    {
        public readonly ActorSnapshot Actor;
        public readonly Float3 ContactPoint;
        public BotTarget(ActorSnapshot actor, Float3 contactPoint) { Actor = actor; ContactPoint = contactPoint; }
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
        public BotObservation(ActorSnapshot self, IReadOnlyList<BotTarget> visible, Float3 freeDirection, bool doorAhead, Func<Float3, Float3> steer = null, string modeId = GameModes.Blood, ActorPrivateState ownPrivate = null, ObjectiveDefinition taskObjective = null, Func<ObjectiveDefinition, Float3> taskDirection = null)
        { Self = self; Visible = Array.AsReadOnly(GameplayRoundConfig.Copy(visible)); FreeDirection = freeDirection; DoorAhead = doorAhead; Steer = steer; ModeId = modeId;
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
        private uint inputSequence, actionSequence, nextStrike;
        private bool releaseBite;
        public BotCommands Decide(BotObservation observation, in BotTick tick)
        {
            var self = observation.Self; BotTarget? selected = null;
            float nearest = float.MaxValue;
            foreach (var visible in observation.Visible)
            {
                if (visible.Actor.Eliminated) continue;
                bool help = self.Role == PlayerRole.Mosquito && visible.Actor.Role == PlayerRole.Mosquito && visible.Actor.LifeState == LifeState.Stunned;
                if (!help && self.Role == visible.Actor.Role) continue;
                if (visible.Actor.LifeState == LifeState.Falling || visible.Actor.LifeState == LifeState.Fainted || visible.Actor.LifeState == LifeState.Recovering || (!help && visible.Actor.LifeState == LifeState.Stunned)) continue;
                float distance = (visible.ContactPoint - self.Position).LengthSquared * (help ? .5f : 1);
                if (distance < nearest) { selected = visible; nearest = distance; }
            }
            var origin = self.Position + (self.Role == PlayerRole.Human ? Float3.Up * (1.53f - .64f * self.CrouchFraction) : Float3.Zero);
            var direction = selected.HasValue ? (selected.Value.ContactPoint - origin).Normalized : observation.FreeDirection.Normalized;
            bool task = observation.TaskObjective != null && observation.OwnAssignment.Status == TaskAssignmentStatus.Active
                && self.Role == PlayerRole.Human && (!selected.HasValue || nearest > 1);
            var taskTravel = task && observation.TaskDirection != null ? observation.TaskDirection(observation.TaskObjective) : Float3.Zero;
            if (task) direction = taskTravel.Normalized;
            if (observation.ModeId == GameModes.Survival && self.Role == PlayerRole.Mosquito && selected.HasValue) direction = -direction;
            if (direction.LengthSquared < .5f) direction = Float3.Forward;
            float yaw = (float)Math.Atan2(direction.X, direction.Z), pitch = (float)Math.Asin(MathEx.Clamp(direction.Y, -1, 1));
            pitch = MathEx.Clamp(pitch, -1.919862f, self.Role == PlayerRole.Human ? 1.308996f : 1.553343f); direction = MathEx.Aim(yaw, pitch);
            bool bite = false, helpHeld = false; float forward = selected.HasValue ? 1 : .5f;
            ActionKind? action = null;
            if (self.Role == PlayerRole.Mosquito)
            {
                if (selected.HasValue && observation.ModeId != GameModes.Survival)
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
            else if (selected.HasValue && !task)
            {
                float distance = (selected.Value.ContactPoint - origin).Length;
                if (distance < .95f) forward = 0;
                if (distance < .8f && tick.Tick >= nextStrike) { action = ActionKind.Primary; nextStrike = tick.Tick + 24; }
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
            if (observation.DoorAhead && self.Role == PlayerRole.Human && !action.HasValue) action = ActionKind.Use;
            if (self.Eliminated || self.LifeState == LifeState.Falling || self.LifeState == LifeState.Stunned || self.LifeState == LifeState.Fainted || self.LifeState == LifeState.Recovering) { forward = 0; bite = false; helpHeld = false; action = null; }
            if (forward > 0 && observation.Steer != null && !action.HasValue)
            {
                var travel = observation.Steer(direction);
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
            var input = new PlayerInputCommand(h, new Float2(0, forward), 0, yaw, pitch, direction, false, false, bite, helpHeld);
            PlayerActionCommand? command = action.HasValue ? new PlayerActionCommand(new CommandHeader(tick.Epoch, tick.Round, self.ActorId, ++actionSequence, tick.Tick, self.ViewRevision), action.Value, direction) : (PlayerActionCommand?)null;
            return new BotCommands(input, command);
        }
    }
}
