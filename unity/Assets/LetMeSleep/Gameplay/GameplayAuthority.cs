using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public sealed class GameplayAuthority : IGameplayAuthority
    {
        private sealed class Actor
        {
            internal SpawnActor Spawn;
            internal Float3 Position, Velocity, Aim = Float3.Forward;
            internal float Yaw, Pitch, Crouch, Motion, Preparation, Extraction, Recovery, Protection;
            internal LifeState State;
            internal uint Revision = 1, ViewRevision = 1, PoseRevision, InputSequence, ActionSequence, InputTick, RateTick, HelpTarget;
            internal int InputCount, ActionCount;
            internal bool HasInput, HasAction, Grounded, BiteArmed = true, Jump, StrikeBlocked;
            internal PlayerInputCommand Input;
            internal CommandReject Rejection;
            internal InteractionHint Hint;
            internal DoorUseResult DoorResult;
            internal uint EquippedPickup;
            internal string EquippedTool = GameplayTools.Hands;
            internal SurfaceAttachment? Surface;
            internal BiteAttachment? Bite;
            internal StrikePlan Plan;
            internal StrikeState Strike;
            internal readonly HashSet<uint> HitActors = new HashSet<uint>();
            internal readonly Dictionary<uint, PlayerActionCommand> ActionHistory = new Dictionary<uint, PlayerActionCommand>();
            internal readonly Queue<uint> ActionHistoryOrder = new Queue<uint>();
        }
        private sealed class Door
        {
            internal DoorDefinition Definition;
            internal float Angle, Target, Velocity;
            internal uint Revision = 1, LastChanged, NextUse;
            internal bool Blocked;
            internal DoorSnapshot Snapshot => new DoorSnapshot(Definition.DoorId, Definition.SurfaceId, Revision, Angle, Target, Velocity, Math.Abs(Target - Angle) > .0001f, Blocked, LastChanged);
        }
        private readonly IGameplayWorld world;
        private readonly Dictionary<uint, Actor> actors = new Dictionary<uint, Actor>();
        private readonly Dictionary<uint, Door> doors = new Dictionary<uint, Door>();
        private readonly Dictionary<uint, ToolPickupSnapshot> pickups = new Dictionary<uint, ToolPickupSnapshot>();
        private readonly Queue<PlayerActionCommand> pending = new Queue<PlayerActionCommand>();
        private readonly List<GameplayEvent> events = new List<GameplayEvent>();
        private GameplayRoundConfig config;
        private uint tick;
        private float blood;
        private ulong eventId, strikeId;
        private SimulationPhase phase = SimulationPhase.Ended;
        private RoundEndReason result;
        private PlayerRole winner;
        public GameplayRoundConfig Config => config;
        public uint CurrentTick => tick;
        public bool IsRunning => config != null && phase == SimulationPhase.Running;
        public GameplayAuthority(IGameplayWorld world) { this.world = world ?? throw new ArgumentNullException(nameof(world)); }

        public void BeginRound(GameplayRoundConfig next, IReadOnlyList<SpawnActor> roster)
        {
            if (next == null || roster == null || roster.Count < 2 || roster.Count > 16) throw new ArgumentException("Invalid round roster.");
            var ids = new HashSet<uint>(); var owners = new HashSet<string>(); int humans = 0, insects = 0;
            foreach (var spawn in roster)
            {
                if (spawn.ActorId == 0 || !ids.Add(spawn.ActorId) || !spawn.Position.IsFinite || spawn.Position.Length > 10000 || (!spawn.IsBot && (string.IsNullOrWhiteSpace(spawn.OwnerPuid) || !owners.Add(spawn.OwnerPuid)))) throw new ArgumentException("Invalid actor identity or spawn.");
                if (spawn.Role == PlayerRole.Human) humans++; else if (spawn.Role == PlayerRole.Mosquito) insects++; else throw new ArgumentException("Role must come from Core.");
            }
            if (humans < 1 || humans > 5 || insects < 1) throw new ArgumentException("Both teams required.");
            ids.Clear();
            if (next.DoorDefinitions.Count > 128) throw new ArgumentException("Too many doors.");
            var surfaceIds = new HashSet<uint>();
            foreach (var door in next.DoorDefinitions)
                if (door.DoorId == 0 || door.SurfaceId == 0 || !ids.Add(door.DoorId) || !surfaceIds.Add(door.SurfaceId) || !MathEx.Finite(door.OpenAngleRadians) || door.OpenAngleRadians <= 0 || door.OpenAngleRadians > (float)Math.PI || !MathEx.Finite(door.InitialAngleRadians) || door.InitialAngleRadians < 0 || door.InitialAngleRadians > door.OpenAngleRadians || !door.HingePosition.IsFinite || door.HingePosition.Length > 10000 || !door.LeafSize.IsFinite || door.LeafSize.X <= 0 || door.LeafSize.Y <= 0 || door.LeafSize.Z <= 0 || door.LeafSize.Length > 100 || !door.HandleLocalPoint.IsFinite || !door.LeafCenterLocal.IsFinite || !ValidRotation(door.ClosedRotation) || !ValidRotation(door.LeafRotationLocal) || (door.OpenSign != -1 && door.OpenSign != 1)) throw new ArgumentException("Invalid door definition.");
            ids.Clear();
            if (next.ToolDefinitions.Count > 32 || (next.ToolDefinitions.Count > 0 && !(world is IGameplayToolWorld))) throw new ArgumentException("Tool world unavailable or pickup limit exceeded.");
            foreach (var tool in next.ToolDefinitions)
                if (tool.PickupId == 0 || !ids.Add(tool.PickupId) || tool.ToolId != GameplayTools.Flyswatter || !tool.Position.IsFinite || tool.Position.Length > 10000 || !ValidRotation(tool.Rotation)) throw new ArgumentException("Invalid tool definition.");
            world.BeginRound(roster, next.DoorDefinitions);
            if (world is IGameplayToolWorld toolWorld) toolWorld.BeginTools(next.ToolDefinitions);
            config = next; tick = 0; blood = 0; eventId = 0; strikeId = 0; result = RoundEndReason.None; winner = PlayerRole.Unassigned; phase = SimulationPhase.Running;
            actors.Clear(); doors.Clear(); pickups.Clear(); pending.Clear(); events.Clear();
            foreach (var spawn in roster) actors.Add(spawn.ActorId, new Actor { Spawn = spawn, Position = spawn.Position, State = spawn.Role == PlayerRole.Human ? LifeState.Active : LifeState.Flying });
            foreach (var definition in next.DoorDefinitions)
            {
                var door = new Door { Definition = definition, Angle = definition.InitialAngleRadians, Target = definition.InitialAngleRadians }; doors.Add(definition.DoorId, door);
                world.ApplyDoorPose(new DoorPose(definition.DoorId, 1, 0, door.Angle));
            }
            foreach (var definition in next.ToolDefinitions)
            {
                var state = new ToolPickupSnapshot(definition.PickupId, definition.ToolId, definition.Position, definition.Rotation);
                pickups.Add(state.PickupId, state); ((IGameplayToolWorld)world).ApplyToolState(state);
            }
            Synchronize();
        }

        public CommandReject SubmitInput(string authenticatedPuid, in PlayerInputCommand command) => Input(authenticatedPuid, command, false);
        public CommandReject SubmitAction(string authenticatedPuid, in PlayerActionCommand command) => Action(authenticatedPuid, command, false);
        // Host-side capability. Online must expose only IGameplayCommandSink, never these bot methods.
        public CommandReject SubmitBotInput(in PlayerInputCommand command) => Input(null, command, true);
        public CommandReject SubmitBotAction(in PlayerActionCommand command) => Action(null, command, true);

        private CommandReject Validate(string principal, CommandHeader h, bool bot, out Actor actor)
        {
            actor = null;
            if (!IsRunning || h.SessionEpoch != config.SessionEpoch || h.RoundId != config.RoundId) return CommandReject.WrongRound;
            if (!actors.TryGetValue(h.ActorId, out actor)) return CommandReject.UnknownActor;
            if (bot ? !actor.Spawn.IsBot : actor.Spawn.IsBot || string.IsNullOrEmpty(principal) || actor.Spawn.OwnerPuid != principal) return CommandReject.WrongOwner;
            if (h.ViewRevision != actor.ViewRevision) return CommandReject.OldViewRevision;
            return CommandReject.None;
        }
        private static bool ValidAim(Float3 aim) => aim.IsFinite && Math.Abs(aim.LengthSquared - 1) <= .02f;
        private static bool ValidRotation(Rotation r) => MathEx.Finite(r.X) && MathEx.Finite(r.Y) && MathEx.Finite(r.Z) && MathEx.Finite(r.W) && Math.Abs(r.X * r.X + r.Y * r.Y + r.Z * r.Z + r.W * r.W - 1) < .002f;
        private void ResetRate(Actor a) { if (tick - a.RateTick >= 30) { a.RateTick = tick; a.InputCount = 0; a.ActionCount = 0; } }
        private CommandReject Input(string principal, in PlayerInputCommand c, bool bot)
        {
            var reject = Validate(principal, c.Header, bot, out var a); if (reject != CommandReject.None) return reject;
            if (!c.MovePlanar.IsFinite || !MathEx.Finite(c.Vertical) || !MathEx.Finite(c.ViewYawRadians) || !MathEx.Finite(c.ViewPitchRadians)) return a.Rejection = CommandReject.InvalidNumber;
            if (!ValidAim(c.AimForward) || Math.Abs(c.ViewYawRadians) > 10000 || c.ViewPitchRadians < -1.919863f || c.ViewPitchRadians > 1.553344f || Float3.Dot(MathEx.Aim(c.ViewYawRadians, c.ViewPitchRadians), c.AimForward.Normalized) < .99984f) return a.Rejection = CommandReject.InvalidDirection;
            if (a.Spawn.Role == PlayerRole.Human && c.ViewPitchRadians > 1.308997f) return a.Rejection = CommandReject.InvalidDirection;
            if (a.HasInput && !MathEx.Newer(c.Header.Sequence, a.InputSequence)) return CommandReject.StaleSequence;
            ResetRate(a); if (++a.InputCount > 60) return a.Rejection = CommandReject.RateLimited;
            a.InputTick = tick; a.InputSequence = c.Header.Sequence; a.HasInput = true;
            // Accept and acknowledge the validated stream while incapacitated, but do
            // not turn mouse/control intent into body orientation or deferred movement.
            if (!CanAct(a)) { ClearHeld(a); return a.Rejection = CommandReject.None; }
            a.Input = c; a.Yaw = c.ViewYawRadians; a.Pitch = c.ViewPitchRadians; a.Aim = c.AimForward.Normalized;
            if (!c.BiteHeld) a.BiteArmed = true;
            return a.Rejection = CommandReject.None;
        }
        private CommandReject Action(string principal, in PlayerActionCommand c, bool bot)
        {
            var reject = Validate(principal, c.Header, bot, out var a); if (reject != CommandReject.None) return reject;
            if (!ValidAim(c.AimForward) || (byte)c.Kind > (byte)ActionKind.DropTool) return a.Rejection = CommandReject.InvalidDirection;
            if (a.ActionHistory.TryGetValue(c.Header.Sequence, out var prior))
                return prior.Kind == c.Kind && (prior.AimForward - c.AimForward).LengthSquared < .0000001f ? CommandReject.None : CommandReject.StaleSequence;
            if (a.HasAction && !MathEx.Newer(c.Header.Sequence, a.ActionSequence)) return CommandReject.StaleSequence;
            if (Float3.Dot(a.Aim, c.AimForward.Normalized) < .9848f) return a.Rejection = CommandReject.InvalidDirection;
            ResetRate(a); if (++a.ActionCount > 20 || pending.Count >= 32) return a.Rejection = CommandReject.RateLimited;
            a.HasAction = true; a.ActionSequence = c.Header.Sequence; a.ActionHistory.Add(c.Header.Sequence, c); a.ActionHistoryOrder.Enqueue(c.Header.Sequence);
            while (a.ActionHistoryOrder.Count > 64) a.ActionHistory.Remove(a.ActionHistoryOrder.Dequeue());
            pending.Enqueue(c); return a.Rejection = CommandReject.None;
        }

        public void Advance(in HostTick hostTick)
        {
            if (!IsRunning) return;
            if (hostTick.Index != tick + 1 || Math.Abs(hostTick.DeltaSeconds - 1f / 30) > .00001f) throw new ArgumentException("Advance requires consecutive fixed 30 Hz ticks.");
            tick = hostTick.Index; const float dt = 1f / 30;
            foreach (var a in actors.Values)
            {
                a.Hint = InteractionHint.None; a.Protection = Math.Max(0, a.Protection - dt);
                if (!a.HasInput || tick - a.InputTick > 7) { a.Input = default; a.BiteArmed = false; }
            }
            ProcessActions(); UpdateDoors(dt);
            foreach (var a in actors.Values.OrderBy(a => a.Spawn.ActorId)) Move(a, dt);
            Synchronize();
            foreach (var a in actors.Values.OrderBy(a => a.Spawn.ActorId)) UpdateStrike(a);
            Synchronize(); // Contact queries use this tick's strike/limb pose as well as locomotion.
            int activeHumans = actors.Values.Count(a => a.Spawn.Role == PlayerRole.Human && a.State == LifeState.Active);
            foreach (var a in actors.Values.Where(a => a.Spawn.Role == PlayerRole.Mosquito).OrderBy(a => a.Spawn.ActorId)) UpdateContact(a, activeHumans, dt);
            Extract(dt);
            Recover(dt);
            Synchronize();
            if (blood >= config.BloodGoal) Finish(RoundEndReason.BloodGoal, PlayerRole.Mosquito);
            else if (tick >= config.RoundDurationTicks) Finish(RoundEndReason.TimeExpired, PlayerRole.Human);
        }
        private void ProcessActions()
        {
            while (pending.Count > 0)
            {
                var c = pending.Dequeue(); if (!actors.TryGetValue(c.Header.ActorId, out var a) || c.Header.ViewRevision != a.ViewRevision) continue;
                if (!CanAct(a)) { a.Rejection = CommandReject.InvalidState; continue; }
                switch (c.Kind)
                {
                    case ActionKind.Jump:
                        if (a.Spawn.Role == PlayerRole.Human) a.Jump = a.Grounded;
                        else Detach(a);
                        break;
                    case ActionKind.Detach: if (a.Spawn.Role == PlayerRole.Mosquito) Detach(a); else a.Rejection = CommandReject.WrongRole; break;
                    case ActionKind.PerchToggle:
                        if (a.Spawn.Role != PlayerRole.Mosquito) { a.Rejection = CommandReject.WrongRole; break; }
                        if (a.Surface.HasValue || a.Bite.HasValue) { Detach(a); break; }
                        if (world.TrySurface(new SurfaceQuery(a.Spawn.ActorId, a.Position, c.AimForward, .25f), out var contact)) { a.Surface = contact.Attachment; SetState(a, LifeState.ApproachingSurface); }
                        else a.Rejection = CommandReject.OutOfReach;
                        break;
                    case ActionKind.Primary:
                        if (a.Spawn.Role != PlayerRole.Human) { a.Rejection = CommandReject.WrongRole; break; }
                        if (a.Strike.Phase != StrikePhase.None) { a.Rejection = CommandReject.Cooldown; break; }
                        if (world.TryPlanStrike(a.Spawn.ActorId, c.AimForward, a.EquippedTool, out a.Plan))
                        {
                            a.HitActors.Clear(); a.StrikeBlocked = false; a.Strike = new StrikeState(++strikeId, a.Plan.ToolId, a.Plan.Hand, StrikePhase.Windup, tick, a.Plan.Origin, a.Plan.Target, a.Plan.Normal, 0);
                            Emit(GameplayEventKind.StrikeStarted, a, 0, a.Plan.Target, a.Plan.Normal);
                        }
                        else a.Rejection = CommandReject.OutOfReach;
                        break;
                    case ActionKind.Use: if (!TryPickTool(a, c.AimForward)) UseDoor(a, c.AimForward); break;
                    case ActionKind.DropTool:
                        if (a.Spawn.Role != PlayerRole.Human) a.Rejection = CommandReject.WrongRole;
                        else if (a.Strike.Phase != StrikePhase.None) a.Rejection = CommandReject.Cooldown;
                        else DropTool(a, false);
                        break;
                }
            }
        }
        private bool TryPickTool(Actor a, Float3 aim)
        {
            if (a.Spawn.Role != PlayerRole.Human || !(world is IGameplayToolWorld toolWorld)) return false;
            var eye = a.Position + Float3.Up * (1.53f - .64f * a.Crouch);
            if (!toolWorld.TryToolInteraction(new ToolInteractionQuery(a.Spawn.ActorId, eye, aim, 1.5f), out var candidate)) return false;
            if (!pickups.TryGetValue(candidate.PickupId, out var pickup) || pickup.Revision != candidate.Revision || pickup.OwnerActorId != 0) { a.Rejection = CommandReject.InvalidState; return true; }
            if (candidate.Distance > 1.5f || candidate.Distance < 0 || !MathEx.Finite(candidate.Distance)) { a.Rejection = CommandReject.OutOfReach; return true; }
            if (a.EquippedPickup != 0 || a.Strike.Phase != StrikePhase.None) { a.Rejection = CommandReject.InvalidState; return true; }
            var equipped = new ToolPickupSnapshot(pickup.PickupId, pickup.ToolId, pickup.Position, pickup.Rotation, a.Spawn.ActorId, pickup.Revision + 1);
            pickups[pickup.PickupId] = equipped; a.EquippedPickup = pickup.PickupId; a.EquippedTool = pickup.ToolId; a.Revision++; a.Hint = InteractionHint.Tool;
            toolWorld.ApplyToolState(equipped); return true;
        }
        private void DropTool(Actor a, bool departing)
        {
            if (a.EquippedPickup == 0 || !(world is IGameplayToolWorld toolWorld) || !pickups.TryGetValue(a.EquippedPickup, out var pickup)) return;
            if (!toolWorld.TryDropTool(a.Spawn.ActorId, out var position, out var rotation))
            {
                if (!departing) { a.Rejection = CommandReject.Obstructed; return; }
                var original = config.ToolDefinitions.First(t => t.PickupId == pickup.PickupId); position = original.Position; rotation = original.Rotation;
            }
            var dropped = new ToolPickupSnapshot(pickup.PickupId, pickup.ToolId, position, rotation, 0, pickup.Revision + 1);
            pickups[pickup.PickupId] = dropped; a.EquippedPickup = 0; a.EquippedTool = GameplayTools.Hands; a.Revision++; a.Hint = InteractionHint.Tool;
            toolWorld.ApplyToolState(dropped);
        }
        private void Move(Actor a, float dt)
        {
            if (a.State == LifeState.Stunned || a.State == LifeState.Fainted || a.State == LifeState.Recovering) { a.Velocity = default; return; }
            if (a.Bite.HasValue) { a.Velocity = default; return; }
            var input = a.Input;
            if (a.State == LifeState.Falling) a.Velocity += new Float3(0, -12 * dt, 0);
            else if (a.Spawn.Role == PlayerRole.Human)
            {
                float desiredCrouch = input.CrouchHeld ? 1 : 0;
                a.Crouch += MathEx.Clamp(desiredCrouch - a.Crouch, -dt * 5, dt * 5);
                var forward = MathEx.Aim(a.Yaw, 0); var right = Float3.Cross(Float3.Up, forward);
                float speed = a.Crouch > .1f ? 1.55f : input.SprintHeld ? 5 : 3.1f;
                var horizontal = Float3.ClampLength(forward * input.MovePlanar.Y + right * input.MovePlanar.X) * speed;
                float vertical = a.Jump && a.Grounded ? 4.6f : a.Grounded ? -.5f : a.Velocity.Y - 12 * dt;
                a.Velocity = new Float3(horizontal.X, vertical, horizontal.Z); a.Jump = false;
            }
            else if (a.Surface.HasValue)
            {
                if (!world.ResolveSurface(a.Surface.Value, out var contact)) { Detach(a); return; }
                var right = new Float3((float)Math.Cos(a.Yaw), 0, -(float)Math.Sin(a.Yaw));
                var desired = Float3.ProjectPlane(a.Aim * input.MovePlanar.Y + right * input.MovePlanar.X, contact.WorldNormal);
                var target = contact.WorldPoint + contact.WorldNormal * .057f;
                if (a.State == LifeState.ApproachingSurface) a.Velocity = Float3.ClampLength((target - a.Position) / dt, .65f);
                else a.Velocity = Float3.ClampLength(desired) * .65f + (target - a.Position) * 8;
            }
            else
            {
                var right = new Float3((float)Math.Cos(a.Yaw), 0, -(float)Math.Sin(a.Yaw));
                var desired = Float3.ClampLength(a.Aim * input.MovePlanar.Y + right * input.MovePlanar.X + Float3.Up * MathEx.Clamp(input.Vertical, -1, 1)) * 3.8f;
                a.Velocity = Float3.MoveTowards(a.Velocity, desired, (desired.LengthSquared < .0001f ? 28 : 13) * dt);
            }
            var old = a.Position;
            var query = new MotorQuery(a.Spawn.ActorId, a.Position, a.Velocity, dt, 1.72f - .72f * a.Crouch, a.Spawn.Role == PlayerRole.Human ? .25f : .055f, a.Crouch, a.Grounded, a.State != LifeState.Falling);
            var moved = a.Spawn.Role == PlayerRole.Human ? world.MoveHuman(query) : world.MoveMosquito(query);
            a.Position = moved.Position; a.Velocity = moved.Velocity; a.Grounded = moved.Grounded; a.Crouch = moved.CrouchFraction;
            a.Motion += (a.Position - old).Length / (a.Spawn.Role == PlayerRole.Human ? 1.2f : .3f); a.PoseRevision++;
            if (a.State == LifeState.Falling && a.Grounded)
            {
                a.Recovery = config.Balance.RecoveryBaseSeconds; a.Velocity = default;
                SetState(a, a.Spawn.Role == PlayerRole.Human ? LifeState.Fainted : LifeState.Stunned); Emit(GameplayEventKind.RecoveryStarted, a);
            }
            else if (a.Surface.HasValue && world.ResolveSurface(a.Surface.Value, out var support))
            {
                // Keep the acquisition reach while travelling toward the selected support.
                // A newly interposed surface cancels approach; an attached insect retains the tight follow probe.
                bool approaching = a.State == LifeState.ApproachingSurface;
                float reach = approaching ? .25f : .12f;
                if (world.TrySurface(new SurfaceQuery(a.Spawn.ActorId, a.Position, -support.WorldNormal, reach), out var updated)
                    && (!approaching || updated.Attachment.SurfaceId == a.Surface.Value.SurfaceId))
                { a.Surface = updated.Attachment; if ((a.Position - updated.WorldPoint).Length < .065f) SetState(a, LifeState.Surface); }
                else Detach(a);
            }
        }
        private void UpdateStrike(Actor a)
        {
            if (a.Strike.Phase == StrikePhase.None || a.State != LifeState.Active) return;
            var s = a.Strike; float elapsed = (tick - s.StartTick) / 30f;
            if (elapsed >= .6f) { a.Strike = default; return; }
            var nextPhase = elapsed < .08f ? StrikePhase.Windup : elapsed < .25f ? StrikePhase.Active : StrikePhase.Recovery;
            float now = MathEx.Clamp((elapsed - .08f) / .17f, 0, 1), prior = MathEx.Clamp((elapsed - 1f / 30 - .08f) / .17f, 0, 1);
            a.Strike = new StrikeState(s.StrikeId, s.ToolId, s.Hand, nextPhase, s.StartTick, s.Origin, s.Target, s.Normal, MathEx.Clamp(elapsed / .6f, 0, 1));
            if (now <= prior || a.StrikeBlocked) return;
            var hit = world.SweepStrike(new StrikeSweep(a.Spawn.ActorId, s.StrikeId, s.Origin + (s.Target - s.Origin) * prior, s.Origin + (s.Target - s.Origin) * now, a.Plan.Radius));
            if (!hit.Hit) return;
            if (hit.ActorId == 0 || (actors.TryGetValue(hit.ActorId, out var blocking) && blocking.Spawn.Role == PlayerRole.Human)) a.StrikeBlocked = true;
            if (!a.HitActors.Add(hit.ActorId)) return;
            Emit(GameplayEventKind.StrikeImpact, a, hit.ActorId, hit.Point, hit.Normal);
            if (actors.TryGetValue(hit.ActorId, out var victim) && victim.Spawn.Role == PlayerRole.Mosquito && CanAct(victim) && victim.Protection <= 0) KnockDown(victim, (hit.Point - s.Origin).Normalized * 1.2f);
        }
        private void UpdateContact(Actor a, int humans, float dt)
        {
            if (!CanAct(a)) return;
            if (a.Bite.HasValue)
            {
                if (!a.Input.BiteHeld || !actors.TryGetValue(a.Bite.Value.VictimId, out var victim) || victim.State != LifeState.Active || victim.Protection > 0 || !world.ResolveBite(a.Spawn.ActorId, a.Bite.Value, humans, out var contact)) { Detach(a); return; }
                a.Position = contact.MosquitoPosition; a.Bite = contact.Attachment; a.PoseRevision++;
                if (a.State == LifeState.PreparingBite)
                {
                    a.Preparation += dt; a.Hint = InteractionHint.Preparing;
                    if (a.Preparation >= config.Balance.PreparationSeconds) { SetState(a, LifeState.Biting); Emit(GameplayEventKind.BiteStarted, a, victim.Spawn.ActorId); }
                }
                else a.Hint = InteractionHint.Biting;
            }
            else if (a.Input.BiteHeld && a.BiteArmed && a.Protection <= 0)
            {
                a.Hint = InteractionHint.ContactRequired;
                if (world.TryBiteContact(new BiteQuery(a.Spawn.ActorId, a.Position, a.Aim, .025f, humans), out var contact) && actors.TryGetValue(contact.Attachment.VictimId, out var victim) && victim.State == LifeState.Active && victim.Protection <= 0)
                {
                    a.Surface = null; a.Bite = contact.Attachment; a.Position = contact.MosquitoPosition; a.Velocity = default; a.Preparation = 0; a.Extraction = 0; SetState(a, LifeState.PreparingBite);
                }
            }
        }
        private void Extract(float dt)
        {
            foreach (var group in actors.Values.Where(a => a.State == LifeState.Biting && a.Bite.HasValue).GroupBy(a => a.Bite.Value.VictimId).ToArray())
            {
                if (!actors.TryGetValue(group.Key, out var victim) || victim.State != LifeState.Active) continue;
                var attached = group.ToArray(); float share = dt / attached.Length;
                foreach (var a in attached) { a.Extraction += share / config.Balance.FullExtractionSeconds; blood = Math.Min(config.BloodGoal, blood + share); }
                if (attached.Any(a => a.Extraction >= 1 - .00001f))
                {
                    victim.Strike = default; victim.Velocity = new Float3(0, -.5f, 0); SetState(victim, LifeState.Falling); Emit(GameplayEventKind.HumanFainted, victim);
                    foreach (var a in actors.Values.Where(a => a.Bite.HasValue && a.Bite.Value.VictimId == victim.Spawn.ActorId).ToArray()) Detach(a);
                }
            }
        }
        private void Recover(float dt)
        {
            var helped = new HashSet<uint>();
            foreach (var helper in actors.Values.Where(a => a.Spawn.Role == PlayerRole.Mosquito))
            {
                uint target = 0;
                if (CanAct(helper) && !helper.Bite.HasValue && helper.Input.UseHeld)
                {
                    var candidate = actors.Values.Where(a => a.Spawn.Role == PlayerRole.Mosquito && a.State == LifeState.Stunned && (a.Position - helper.Position).Length <= .6f && Float3.Dot((a.Position - helper.Position).Normalized, helper.Aim) >= .7f && world.HasLineOfSight(helper.Spawn.ActorId, helper.Position, a.Spawn.ActorId, a.Position)).OrderBy(a => (a.Position - helper.Position).LengthSquared).FirstOrDefault();
                    if (candidate != null) { target = candidate.Spawn.ActorId; helped.Add(target); helper.Hint = InteractionHint.Helping; }
                }
                if (target != helper.HelpTarget)
                {
                    if (helper.HelpTarget != 0) Emit(GameplayEventKind.HelpEnded, helper, helper.HelpTarget);
                    helper.HelpTarget = target; if (target != 0) Emit(GameplayEventKind.HelpStarted, helper, target);
                }
            }
            foreach (var a in actors.Values)
            {
                if (a.State == LifeState.Stunned || a.State == LifeState.Fainted)
                {
                    a.Hint = InteractionHint.Stunned; a.Recovery = Math.Max(0, a.Recovery - dt * (helped.Contains(a.Spawn.ActorId) ? config.Balance.HelpMultiplier : 1));
                    if (a.Recovery <= 0 && world.TryFreeRecoveryPoint(a.Spawn.ActorId, a.Position, out var point))
                    { a.Position = point; a.Recovery = .4f; SetState(a, LifeState.Recovering); a.ViewRevision++; ClearHeld(a); }
                }
                else if (a.State == LifeState.Recovering)
                {
                    a.Hint = InteractionHint.Recovering; a.Recovery = Math.Max(0, a.Recovery - dt);
                    if (a.Recovery <= 0) { SetState(a, a.Spawn.Role == PlayerRole.Human ? LifeState.Active : LifeState.Flying); a.Protection = config.Balance.ProtectionSeconds; Emit(GameplayEventKind.Recovered, a); }
                }
            }
        }
        private void UseDoor(Actor a, Float3 aim)
        {
            if (a.Spawn.Role != PlayerRole.Human) { a.DoorResult = DoorUseResult.WrongRole; return; }
            if (a.State != LifeState.Active) { a.DoorResult = DoorUseResult.InvalidState; return; }
            var eye = a.Position + Float3.Up * (1.53f - .64f * a.Crouch);
            if (!world.TryDoorInteraction(new DoorInteractionQuery(a.Spawn.ActorId, tick, eye, aim, 2), out var candidate) || !doors.TryGetValue(candidate.DoorId, out var door)) { a.DoorResult = DoorUseResult.NoDoor; return; }
            if (candidate.Distance > 2) { a.DoorResult = DoorUseResult.OutOfReach; return; }
            if (candidate.DoorRevision != door.Revision) { a.DoorResult = DoorUseResult.StaleRevision; return; }
            if (tick < door.NextUse) { a.DoorResult = DoorUseResult.Cooldown; return; }
            door.NextUse = tick + 11; door.Target = door.Target > .001f ? 0 : door.Definition.OpenAngleRadians; door.Revision++; door.LastChanged = tick; a.DoorResult = DoorUseResult.Accepted; a.Hint = InteractionHint.Door;
            EmitDoor(door, a.Spawn.ActorId);
        }
        private void UpdateDoors(float dt)
        {
            foreach (var door in doors.Values)
            {
                bool wasMoving = Math.Abs(door.Target - door.Angle) > .0001f;
                float desired = door.Angle + MathEx.Clamp(door.Target - door.Angle, -2.5f * dt, 2.5f * dt);
                var sweep = world.SweepDoor(new DoorMotionQuery(door.Definition.DoorId, door.Revision, tick, door.Angle, desired));
                float safe = MathEx.Clamp(sweep.SafeAngleRadians, Math.Min(door.Angle, desired), Math.Max(door.Angle, desired));
                door.Velocity = (safe - door.Angle) / dt; door.Angle = safe;
                bool changed = sweep.Blocked != door.Blocked || (wasMoving && Math.Abs(door.Target - door.Angle) <= .0001f); door.Blocked = sweep.Blocked;
                if (changed) { door.Revision++; door.LastChanged = tick; }
                world.ApplyDoorPose(new DoorPose(door.Definition.DoorId, door.Revision, tick, door.Angle));
                if (changed) EmitDoor(door, 0);
            }
        }
        private void EmitDoor(Door door, uint source) => events.Add(new GameplayEvent(config.SessionEpoch, config.RoundId, ++eventId, tick, GameplayEventKind.DoorChanged, source, 0, door.Revision, door.Definition.HingePosition, Float3.Up, door.Snapshot));
        private static bool CanAct(Actor a) => a.State != LifeState.Falling && a.State != LifeState.Stunned && a.State != LifeState.Fainted && a.State != LifeState.Recovering;
        private static void ClearHeld(Actor a) { a.Input = default; a.Jump = false; a.BiteArmed = false; }
        private static void SetState(Actor a, LifeState state)
        {
            if (a.State == state) return;
            bool wasControllable = CanAct(a);
            a.State = state; a.Revision++;
            if (!CanAct(a) || !wasControllable) ClearHeld(a);
        }
        private void Detach(Actor a)
        {
            if (a.Bite.HasValue) Emit(GameplayEventKind.BiteEnded, a, a.Bite.Value.VictimId);
            bool attached = a.Bite.HasValue || a.Surface.HasValue;
            a.Bite = null; a.Surface = null; a.Preparation = 0; a.Extraction = 0; a.BiteArmed = false;
            if (attached) { a.ViewRevision++; ClearHeld(a); }
            if (CanAct(a)) SetState(a, LifeState.Flying);
        }
        private void KnockDown(Actor a, Float3 impulse) { Detach(a); a.Velocity = impulse + new Float3(0, -.6f, 0); a.Grounded = false; SetState(a, LifeState.Falling); Emit(GameplayEventKind.MosquitoKnockedDown, a); }
        private void Emit(GameplayEventKind kind, Actor a, uint target = 0, Float3 position = default, Float3 normal = default) => events.Add(new GameplayEvent(config.SessionEpoch, config.RoundId, ++eventId, tick, kind, a.Spawn.ActorId, target, a.Revision, position.LengthSquared == 0 ? a.Position : position, normal));
        private ActorSnapshot Snapshot(Actor a) => new ActorSnapshot(a.Spawn.ActorId, a.Spawn.Role, a.State, a.Revision, a.Position, a.Velocity, Rotation.Yaw(a.Yaw), a.Aim, a.Yaw, a.Pitch, a.ViewRevision, a.PoseRevision, a.Grounded, a.Crouch, a.Motion, a.Surface, a.Bite, a.Strike, tick + (uint)Math.Ceiling(a.Recovery * 30), a.EquippedTool);
        private ActorSnapshot[] ActorSnapshots() => actors.Values.OrderBy(a => a.Spawn.ActorId).Select(Snapshot).ToArray();
        private void Synchronize() => world.SynchronizeActors(ActorSnapshots());
        public GameSessionState CaptureSnapshot()
        { if (config == null) throw new InvalidOperationException("BeginRound first."); return new GameSessionState(config, tick, phase, blood, result, winner, ActorSnapshots(), doors.Values.OrderBy(d => d.Definition.DoorId).Select(d => d.Snapshot).ToArray(), pickups.Values.OrderBy(p => p.PickupId).ToArray()); }
        public ActorPrivateState CapturePrivate(uint actorId)
        {
            if (!actors.TryGetValue(actorId, out var a)) return null;
            return new ActorPrivateState(actorId, a.InputSequence, a.ActionSequence, a.Rejection, a.Hint, MathEx.Clamp(a.Preparation / config.Balance.PreparationSeconds, 0, 1), a.Extraction, a.Recovery, a.HelpTarget, CanAct(a), a.DoorResult, config.SessionEpoch, config.RoundId, tick);
        }
        public IReadOnlyList<GameplayEvent> DrainEvents() { var copy = Array.AsReadOnly(events.ToArray()); events.Clear(); return copy; }
        public void RemoveActor(uint actorId, ActorRemovalReason reason)
        {
            if (!actors.TryGetValue(actorId, out var leaving)) return;
            DropTool(leaving, true); actors.Remove(actorId);
            foreach (var a in actors.Values.Where(a => a.Bite.HasValue && a.Bite.Value.VictimId == actorId)) Detach(a);
            Synchronize();
            if (!IsRunning) return;
            bool human = actors.Values.Any(a => a.Spawn.Role == PlayerRole.Human), mosquito = actors.Values.Any(a => a.Spawn.Role == PlayerRole.Mosquito);
            if (!human || !mosquito) Finish(!human && !mosquito ? RoundEndReason.Aborted : RoundEndReason.OpponentLeft, human ? PlayerRole.Human : mosquito ? PlayerRole.Mosquito : PlayerRole.Unassigned);
        }
        public void EndRound(RoundEndReason reason) => Finish(reason == RoundEndReason.None ? RoundEndReason.Aborted : reason, PlayerRole.Unassigned);
        private void Finish(RoundEndReason reason, PlayerRole team)
        {
            if (!IsRunning) return;
            phase = SimulationPhase.Ended; result = reason; winner = team; pending.Clear();
            foreach (var a in actors.Values) { a.Bite = null; a.Surface = null; a.Strike = default; a.Velocity = default; ClearHeld(a); }
            events.Add(new GameplayEvent(config.SessionEpoch, config.RoundId, ++eventId, tick, GameplayEventKind.RoundEnded, 0, 0, 0, default, default, null, reason));
        }
    }
}
