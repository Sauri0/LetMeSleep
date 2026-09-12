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
        public BotObservation(ActorSnapshot self, IReadOnlyList<BotTarget> visible, Float3 freeDirection, bool doorAhead)
        { Self = self; Visible = Array.AsReadOnly(GameplayRoundConfig.Copy(visible)); FreeDirection = freeDirection; DoorAhead = doorAhead; }
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
                bool help = self.Role == PlayerRole.Mosquito && visible.Actor.Role == PlayerRole.Mosquito && visible.Actor.LifeState == LifeState.Stunned;
                if (!help && self.Role == visible.Actor.Role) continue;
                if (visible.Actor.LifeState == LifeState.Falling || visible.Actor.LifeState == LifeState.Fainted || visible.Actor.LifeState == LifeState.Recovering || (!help && visible.Actor.LifeState == LifeState.Stunned)) continue;
                float distance = (visible.ContactPoint - self.Position).LengthSquared * (help ? .5f : 1);
                if (distance < nearest) { selected = visible; nearest = distance; }
            }
            var origin = self.Position + (self.Role == PlayerRole.Human ? Float3.Up * (1.53f - .64f * self.CrouchFraction) : Float3.Zero);
            var direction = selected.HasValue ? (selected.Value.ContactPoint - origin).Normalized : observation.FreeDirection.Normalized;
            if (direction.LengthSquared < .5f) direction = Float3.Forward;
            float yaw = (float)Math.Atan2(direction.X, direction.Z), pitch = (float)Math.Asin(MathEx.Clamp(direction.Y, -1, 1));
            pitch = MathEx.Clamp(pitch, -1.919862f, self.Role == PlayerRole.Human ? 1.308996f : 1.553343f); direction = MathEx.Aim(yaw, pitch);
            bool bite = false, helpHeld = false; float forward = selected.HasValue ? 1 : .5f;
            ActionKind? action = null;
            if (self.Role == PlayerRole.Mosquito)
            {
                if (selected.HasValue)
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
            else if (selected.HasValue)
            {
                float distance = (selected.Value.ContactPoint - origin).Length;
                if (distance < .95f) forward = 0;
                if (distance < .8f && tick.Tick >= nextStrike) { action = ActionKind.Primary; nextStrike = tick.Tick + 24; }
            }
            if (observation.DoorAhead && self.Role == PlayerRole.Human && !action.HasValue) action = ActionKind.Use;
            if (self.LifeState == LifeState.Falling || self.LifeState == LifeState.Stunned || self.LifeState == LifeState.Fainted || self.LifeState == LifeState.Recovering) { forward = 0; bite = false; helpHeld = false; action = null; }
            var h = new CommandHeader(tick.Epoch, tick.Round, self.ActorId, ++inputSequence, tick.Tick, self.ViewRevision);
            var input = new PlayerInputCommand(h, new Float2(0, forward), 0, yaw, pitch, direction, false, false, bite, helpHeld);
            PlayerActionCommand? command = action.HasValue ? new PlayerActionCommand(new CommandHeader(tick.Epoch, tick.Round, self.ActorId, ++actionSequence, tick.Tick, self.ViewRevision), action.Value, direction) : (PlayerActionCommand?)null;
            return new BotCommands(input, command);
        }
    }
}
