using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LetMeSleep.Gameplay.Unity
{
    // Room/Online owns lifecycle. This adapter never creates or sorts a RoomSession.
    [RequireComponent(typeof(UnityGameplayWorld))]
    public sealed class GameplayRuntime : MonoBehaviour, IGameplayPresentationSink
    {
        public bool IsHost = true;
        public bool AutomaticTick = true;
        public bool CaptureLocalInput = true;
        public bool UseBuiltInCamera;
        public Camera LocalCamera;
        [Tooltip("Active map's schema-1 SpatialData; assign before BeginRound. Host bot patrol only.")]
        public TextAsset NavigationData;
        public uint LocalActorId;
        public string LocalPrincipal;
        public float MouseSensitivity = .002f;
        public bool InvertY;
        public float MosquitoCameraDistance = .85f;
        public GameplayAuthority Authority { get; private set; }
        public UnityGameplayWorld World { get; private set; }
        public GameSessionState LatestSnapshot { get; private set; }
        public ActorPrivateState LocalPrivate { get; private set; }
        public bool InputBlocked { get; private set; }
        public Float3 LocalViewForward => MathEx.Aim(yaw, pitch);
        public float LocalViewYaw => yaw;
        public float LocalViewPitch => pitch;
        public Quaternion LocalCameraRotation
        {
            get
            {
                var self = LocalActor();
                if (self != null && self.Role == PlayerRole.Mosquito)
                { UpdateMosquitoLook(self, 0, 0); return mosquitoLook.View; }
                return Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);
            }
        }
        private void UpdateMosquitoLook(ActorSnapshot self, float yawDelta, float pitchDelta)
        {
            if (!mosquitoLookReady) { mosquitoLook.Reset(self.BodyRotation, LocalViewForward); mosquitoLookReady = true; }
            mosquitoLook.Step(self.BodyRotation, yawDelta, pitchDelta);
            var aim = mosquitoLook.Forward;
            // asin is the canonical world pitch; the orbit limit is relative to support,
            // so a wall can legitimately look straight up in world space.
            if (aim.X * aim.X + aim.Z * aim.Z > .00000001f) yaw = Mathf.Atan2(aim.X, aim.Z);
            pitch = Mathf.Asin(Mathf.Clamp(aim.Y, -1, 1));
        }
        public event Action<PlayerInputCommand> InputReady;
        public event Action<PlayerActionCommand> ActionReady;
        public event Action<GameSessionState> SnapshotReady;
        // Applied local/remote state for UI/presentation; SnapshotReady remains host transport output.
        public event Action<GameSessionState> SnapshotApplied;
        public event Action<ActorPrivateState> PrivateReady;
        public event Action<GameplayEvent> EventReady;
        public event Action<RoundEndReason, PlayerRole> RoundFinished;
        private readonly Dictionary<uint, BotController> bots = new Dictionary<uint, BotController>();
        private readonly ReplicaStateGate replicaGate = new ReplicaStateGate();
        private float accumulator, yaw, pitch, sendAccumulator, snapshotAccumulator;
        private uint inputSequence, actionSequence, knownViewRevision;
        private bool biteNeedsRelease, wasAttached, focus = true, finishedSent;
        private bool controlsNeedRelease;
        private readonly MosquitoLookFrame mosquitoLook = new MosquitoLookFrame();
        private bool mosquitoLookReady;
        private readonly Queue<PlayerActionCommand> queuedActions = new Queue<PlayerActionCommand>();
        private PlayerActionCommand? localThrow;
        private PlayerInputCommand held;
        private GameplayRoundConfig roundConfig;
        private float cameraDistance;
        private GameplayBotNavigation botNavigation;

        private void Awake() { World = GetComponent<UnityGameplayWorld>(); Authority = new GameplayAuthority(World); }
        public void BeginRound(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster)
        {
            if (!World) Awake();
            roundConfig = config; accumulator = sendAccumulator = snapshotAccumulator = 0; inputSequence = actionSequence = knownViewRevision = 0; yaw = pitch = 0; cameraDistance = 0; finishedSent = false;
            LatestSnapshot = null; LocalPrivate = null; held = default;
            replicaGate.Reset(config);
            queuedActions.Clear(); bots.Clear(); localThrow = null; biteNeedsRelease = true; wasAttached = false; controlsNeedRelease = false; mosquitoLookReady = false;
            botNavigation = World.ConfigureModeMap(NavigationData, config.MapId, config.ModeId == GameModes.Tasks);
            World.ResetBotSteering();
            if (IsHost)
            {
                Authority.BeginRound(config, roster);
                foreach (var spawn in roster.Where(a => a.IsBot)) bots.Add(spawn.ActorId, new BotController());
                ApplySnapshot(Authority.CaptureSnapshot());
            }
            else { World.BeginRound(roster, config.DoorDefinitions); World.BeginTools(config.ToolDefinitions); }
            SetInputBlocked(false);
        }
        public void StopRound()
        {
            if (IsHost) Authority?.EndRound(RoundEndReason.Aborted);
            roundConfig = null; LatestSnapshot = null; LocalPrivate = null; queuedActions.Clear(); bots.Clear(); SetInputBlocked(true);
            botNavigation = null; World?.ResetBotSteering(); World?.ResetModeMap();
            replicaGate.Reset(null);
        }
        public void SetInputBlocked(bool blocked)
        {
            InputBlocked = blocked; queuedActions.Clear(); biteNeedsRelease = true;
            if (CaptureLocalInput) { Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = blocked; }
            if (blocked && roundConfig != null) SendNeutral();
        }
        private void OnApplicationFocus(bool value) { focus = value; if (!value) { queuedActions.Clear(); biteNeedsRelease = true; SendNeutral(); } }
        private void OnDisable() { SendNeutral(); }
        private ActorSnapshot LocalActor() => LatestSnapshot?.Actors.FirstOrDefault(a => a.ActorId == LocalActorId);
        private void Update()
        {
            if (roundConfig == null) return;
            if (CaptureLocalInput) PollInput();
            if (IsHost && AutomaticTick)
            {
                accumulator += Time.unscaledDeltaTime;
                int steps = 0;
                while (accumulator >= 1f / 30 && steps++ < 8) { TickHost(); accumulator -= 1f / 30; }
                // Retain time debt; do not silently shorten the round on a slow frame.
            }
            else if (!IsHost && CaptureLocalInput)
            {
                sendAccumulator += Time.unscaledDeltaTime;
                if (sendAccumulator >= 1f / 30) { sendAccumulator %= 1f / 30; SendLocal(); }
            }
        }
        private void PollInput()
        {
            var self = LocalActor(); if (self == null) return;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (InputBlocked || !focus || keyboard == null || mouse == null)
            { held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward); queuedActions.Clear(); return; }
            if (!BodyControlsAvailable(self.LifeState))
            { ClearIncapacitatedInput(self); return; }
            if (controlsNeedRelease)
            {
                held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward);
                queuedActions.Clear();
                // Pause/UI keys are not part of this gate. Resume requires a fresh
                // gameplay press instead of replaying a button held through recovery.
                controlsNeedRelease = keyboard.wKey.isPressed || keyboard.aKey.isPressed ||
                    keyboard.sKey.isPressed || keyboard.dKey.isPressed || keyboard.spaceKey.isPressed ||
                    keyboard.leftCtrlKey.isPressed || keyboard.leftShiftKey.isPressed ||
                    keyboard.eKey.isPressed || keyboard.rKey.isPressed || keyboard.fKey.isPressed ||
                    keyboard.gKey.isPressed || mouse.leftButton.isPressed;
                return;
            }
            var delta = mouse.delta.ReadValue();
            if (self.Role == PlayerRole.Mosquito)
            {
                // Orbit relative to the body's support horizon. The authority keeps the
                // body heading still while movement is neutral, but accepts this aim.
                UpdateMosquitoLook(self, delta.x * MouseSensitivity, delta.y * MouseSensitivity * (InvertY ? -1 : 1));
            }
            else
            {
                yaw = Mathf.Repeat(yaw + delta.x * MouseSensitivity + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
                pitch = Mathf.Clamp(pitch + delta.y * MouseSensitivity * (InvertY ? -1 : 1), -110 * Mathf.Deg2Rad, 75 * Mathf.Deg2Rad);
            }
            bool e = keyboard.eKey.isPressed;
            if (!e) biteNeedsRelease = false;
            bool attached = self.BiteAttachment.HasValue;
            if (keyboard.eKey.wasPressedThisFrame && attached && wasAttached) { Enqueue(ActionKind.Detach); biteNeedsRelease = true; }
            wasAttached = attached;
            if (keyboard.spaceKey.wasPressedThisFrame && self.Role == PlayerRole.Human) Enqueue(ActionKind.Jump);
            if (self.Role == PlayerRole.Human && keyboard.eKey.wasPressedThisFrame)
            {
                if (LocalPrivate?.SwapOffer is PickupSwapOffer offer)
                    Enqueue(new PlayerActionCommand(default, ActionKind.ConfirmPickup, default, offer.SlotIndex, offer.PickupId, offer.PickupRevision, offer.InventoryRevision));
                else Enqueue(ActionKind.Use);
            }
            if (self.Role == PlayerRole.Mosquito && keyboard.fKey.wasPressedThisFrame) Enqueue(ActionKind.PerchToggle);
            if (keyboard.gKey.wasPressedThisFrame && self.Role == PlayerRole.Human) Enqueue(ActionKind.DropTool);
            if (self.Role == PlayerRole.Human)
            {
                PollEquipment(keyboard, mouse, self);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (self.EquippedToolId == GameplayTools.Slipper && LocalPrivate != null)
                    {
                        var inventory = LocalPrivate.Inventory;
                        var pickup = LatestSnapshot.ToolPickups.FirstOrDefault(p => p.PickupId == inventory.ActivePickup && p.OwnerActorId == self.ActorId);
                        if (pickup.PickupId != 0)
                        {
                            localThrow = new PlayerActionCommand(default, ActionKind.BeginThrow, default, inventory.SelectedSlot, pickup.PickupId, pickup.Revision, inventory.Revision);
                            Enqueue(localThrow.Value);
                        }
                    }
                    else Enqueue(ActionKind.Primary);
                }
                if (mouse.leftButton.wasReleasedThisFrame && localThrow.HasValue)
                {
                    var charge = localThrow.Value;
                    Enqueue(new PlayerActionCommand(default, ActionKind.ReleaseThrow, default, charge.SlotIndex, charge.TargetPickupId, charge.ExpectedPickupRevision, charge.InventoryRevision));
                    localThrow = null;
                }
            }
            if (self.Role == PlayerRole.Mosquito) MosquitoCameraDistance = Mathf.Clamp(MosquitoCameraDistance - mouse.scroll.ReadValue().y * .0015f, 0, 2.5f);
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            float vertical = self.Role == PlayerRole.Mosquito ? (keyboard.spaceKey.isPressed ? 1 : 0) - (keyboard.leftCtrlKey.isPressed ? 1 : 0) : 0;
            held = new PlayerInputCommand(default, new Float2(x, y), vertical, yaw, pitch, LocalViewForward, keyboard.leftShiftKey.isPressed, keyboard.leftCtrlKey.isPressed, e && !biteNeedsRelease, self.Role == PlayerRole.Human ? e : keyboard.rKey.isPressed, mouse.leftButton.isPressed);
        }
        private void Enqueue(ActionKind kind) => Enqueue(new PlayerActionCommand(default, kind, default));
        private void Enqueue(PlayerActionCommand command) { if (queuedActions.Count < 8) queuedActions.Enqueue(command); }
        private void PollEquipment(Keyboard keyboard, Mouse mouse, ActorSnapshot self)
        {
            if (LocalPrivate == null || LocalPrivate.Inventory.Revision == 0) return;
            int selected = LocalPrivate.Inventory.SelectedSlot, next = selected;
            if (keyboard.digit1Key.wasPressedThisFrame) next = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame) next = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame) next = 2;
            else if (keyboard.digit0Key.wasPressedThisFrame) next = -1;
            else if (mouse.scroll.ReadValue().y != 0) next = ((selected + 1 + (mouse.scroll.ReadValue().y > 0 ? 1 : 3)) % 4) - 1;
            if (next == selected) return;
            localThrow = null;
            Enqueue(ActionKind.CancelThrow);
            Enqueue(new PlayerActionCommand(default, ActionKind.SelectInventorySlot, default, next, 0, 0, LocalPrivate.Inventory.Revision));
        }
        private void SendNeutral()
        {
            if (roundConfig == null || LocalActorId == 0) return;
            localThrow = null;
            var self = LocalActor();
            if (self != null && self.Role == PlayerRole.Human && !self.Eliminated)
            {
                var cancel = new PlayerActionCommand(new CommandHeader(roundConfig.SessionEpoch, roundConfig.RoundId, LocalActorId, ++actionSequence, LatestSnapshot.HostTick, self.ViewRevision), ActionKind.CancelThrow, LocalViewForward);
                if (IsHost) Authority.SubmitAction(LocalPrincipal, cancel); else ActionReady?.Invoke(cancel);
            }
            held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward); SendLocal(true);
        }
        private void SendLocal(bool forceNeutral = false)
        {
            var self = LocalActor(); if (self == null) return;
            if (self.Eliminated)
            { held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward); queuedActions.Clear(); return; }
            bool neutral = forceNeutral || InputBlocked || !focus || controlsNeedRelease || !BodyControlsAvailable(self.LifeState);
            var h = new CommandHeader(roundConfig.SessionEpoch, roundConfig.RoundId, LocalActorId, ++inputSequence, LatestSnapshot.HostTick, self.ViewRevision);
            var input = new PlayerInputCommand(h, neutral ? default : held.MovePlanar, neutral ? 0 : held.Vertical, yaw, pitch, LocalViewForward, !neutral && held.SprintHeld, !neutral && held.CrouchHeld, !neutral && held.BiteHeld, !neutral && held.UseHeld, !neutral && held.PrimaryHeld);
            if (IsHost) Authority.SubmitInput(LocalPrincipal, input); else InputReady?.Invoke(input);
            while (queuedActions.Count > 0)
            {
                var action = queuedActions.Dequeue(); if (neutral) continue;
                var command = new PlayerActionCommand(new CommandHeader(roundConfig.SessionEpoch, roundConfig.RoundId, LocalActorId, ++actionSequence, LatestSnapshot.HostTick, self.ViewRevision), action.Kind, LocalViewForward, action.SlotIndex, action.TargetPickupId, action.ExpectedPickupRevision, action.InventoryRevision);
                if (IsHost) Authority.SubmitAction(LocalPrincipal, command); else ActionReady?.Invoke(command);
            }
        }
        public void TickHost()
        {
            if (!IsHost || Authority == null || !Authority.IsRunning) return;
            if (CaptureLocalInput) SendLocal();
            uint next = Authority.CurrentTick + 1;
            var snapshot = Authority.CaptureSnapshot();
            foreach (var bot in bots)
            {
                if ((next + bot.Key) % 3 != 0) continue;
                var self = snapshot.Actors.FirstOrDefault(a => a.ActorId == bot.Key); if (self == null) continue;
                if (self.Eliminated) continue;
                var observation = ObserveBot(self, snapshot);
                var commands = bot.Value.Decide(observation, new BotTick(snapshot.SessionEpoch, snapshot.RoundId, next));
                Authority.SubmitBotInput(commands.Input); if (commands.Action.HasValue) Authority.SubmitBotAction(commands.Action.Value);
            }
            Authority.Advance(new HostTick(next));
            var latest = Authority.CaptureSnapshot(); ApplySnapshot(latest);
            LocalPrivate = Authority.CapturePrivate(LocalActorId); if (LocalPrivate != null) PrivateReady?.Invoke(LocalPrivate);
            foreach (var item in Authority.DrainEvents()) ApplyEvent(item);
            snapshotAccumulator += 1f / 30;
            if (snapshotAccumulator >= .05f || latest.SimulationPhase == SimulationPhase.Ended) { snapshotAccumulator -= .05f; SnapshotReady?.Invoke(latest); }
            if (latest.SimulationPhase == SimulationPhase.Ended && !finishedSent) { finishedSent = true; RoundFinished?.Invoke(latest.Result, latest.Winner); }
        }
        private BotObservation ObserveBot(ActorSnapshot self, GameSessionState state)
        {
            uint navigationTick = state.HostTick + 1;
            var origin = self.Position + (self.Role == PlayerRole.Human ? Float3.Up * (1.53f - .64f * self.CrouchFraction) : Float3.Zero);
            var visible = new List<BotTarget>();
            foreach (var target in state.Actors)
            {
                if (target.ActorId == self.ActorId || (target.Position - self.Position).Length > 12) continue;
                Float3 point = target.Position;
                if (target.Role == PlayerRole.Human && World.Actors.TryGetValue(target.ActorId, out var proxy))
                {
                    // Perception samples the closest visible body surface; no assigned zone is stored.
                    var candidate = proxy.BodySurfaces.Values.Select(s => s.Collider.ClosestPoint(origin.ToUnity())).OrderBy(p => (p - origin.ToUnity()).sqrMagnitude).FirstOrDefault(); point = candidate.ToFloat();
                }
                if (World.HasLineOfSight(self.ActorId, origin, target.ActorId, point)) visible.Add(new BotTarget(target, point));
            }
            float angle = (state.HostTick / 120 + self.ActorId * 2.399963f);
            var free = new Float3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
            if (botNavigation != null && self.Role == PlayerRole.Mosquito)
            {
                var explore = botNavigation.Explore(self, navigationTick);
                if (explore.LengthSquared > .01f) free = explore;
            }
            var doorQuery = new DoorInteractionQuery(self.ActorId, state.HostTick, origin, self.ViewForward, 1.6f);
            bool doorAhead = World.TryDoorInteraction(doorQuery, out var door) && World.Doors.TryGetValue(door.DoorId, out var physicalDoor) && Mathf.Abs(physicalDoor.AngleRadians) < .1f;
            ActorPrivateState privateState = Authority.CapturePrivate(self.ActorId);
            ObjectiveDefinition objective = privateState?.TaskAssignment == null ? null : roundConfig.Objectives
                .FirstOrDefault(item => item.ObjectiveId == privateState.TaskAssignment.ObjectiveId);
            var opportunities = new List<BotToolOpportunity>();
            ToolPickupSnapshot? equipped = null;
            foreach (var pickup in state.ToolPickups)
            {
                if (privateState != null && pickup.PickupId == privateState.Inventory.ActivePickup &&
                    pickup.OwnerActorId == self.ActorId && pickup.Phase == ToolPickupPhase.Held)
                    equipped = pickup;
                if (self.Role != PlayerRole.Human || botNavigation == null ||
                    !World.TryObserveTool(self.ActorId, pickup, origin, out var visiblePoint) ||
                    !botNavigation.TryToolDetour(self, pickup.Position,
                        privateState?.TaskAssignment?.Status == TaskAssignmentStatus.Active ? objective : null, out var detour)) continue;
                if (detour < 4) opportunities.Add(new BotToolOpportunity(pickup, detour, visiblePoint));
            }
            float rescueSpeed = self.SurfaceAttachment.HasValue ? .65f : Mathf.Clamp(self.Velocity.Length, 1f, 3.8f);
            return new BotObservation(self, visible, free, doorAhead, direction => World.SteerBot(self, direction),
                roundConfig.ModeId, privateState, objective,
                item => botNavigation == null ? Float3.Zero : World.TaskDirection(self, item, navigationTick),
                new BotTrainingContext(opportunities, rescueSpeed, equipped), botNavigation?.ContextFor(self.ActorId));
        }
        public void ApplySnapshot(GameSessionState snapshot)
        {
            if (!replicaGate.AcceptSnapshot(snapshot)) return;
            LatestSnapshot = snapshot;
            if (!IsHost)
            {
                World.SynchronizeActors(snapshot.Actors);
                foreach (var door in snapshot.Doors) World.ApplyDoorPose(new DoorPose(door.DoorId, door.Revision, snapshot.HostTick, door.AngleRadians));
                foreach (var pickup in snapshot.ToolPickups) World.ApplyToolState(pickup);
            }
            var self = LocalActor();
            if (self != null && !BodyControlsAvailable(self.LifeState)) ClearIncapacitatedInput(self);
            if (self != null && self.ViewRevision != knownViewRevision)
            {
                knownViewRevision = self.ViewRevision; queuedActions.Clear(); biteNeedsRelease = true;
                // Preserve local world look; only discard movement/action prediction from the old frame.
            }
            SnapshotApplied?.Invoke(snapshot);
            if (!IsHost) SnapshotReady?.Invoke(snapshot);
        }
        public void ApplySnapshot(GameSessionState snapshot, double renderHostTime) => ApplySnapshot(snapshot);
        private static bool BodyControlsAvailable(LifeState state) =>
            state != LifeState.Falling && state != LifeState.Fainted && state != LifeState.Stunned &&
            state != LifeState.Recovering && state != LifeState.Eliminated;
        private void ClearIncapacitatedInput(ActorSnapshot self)
        {
            yaw = self.ViewYawRadians; pitch = self.ViewPitchRadians;
            mosquitoLookReady = false;
            held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward);
            queuedActions.Clear(); localThrow = null; biteNeedsRelease = true; controlsNeedRelease = true;
        }
        public void ApplyPrivate(ActorPrivateState state)
        { if (replicaGate.AcceptPrivate(state, LocalActorId)) { LocalPrivate = state; PrivateReady?.Invoke(state); } }
        public void ApplyEvent(in GameplayEvent item)
        { if (replicaGate.AcceptEvent(item)) EventReady?.Invoke(item); }
        private void LateUpdate()
        {
            if (!UseBuiltInCamera || !LocalCamera) return;
            var self = LocalActor(); if (self == null) return;
            var viewRotation = LocalCameraRotation;
            var forward = viewRotation * Vector3.forward; var center = self.Position.ToUnity();
            if (self.Role == PlayerRole.Human)
            {
                LocalCamera.transform.SetPositionAndRotation(center + Vector3.up * (1.53f - .64f * self.CrouchFraction), Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-pitch * Mathf.Rad2Deg, Vector3.right));
                LocalCamera.nearClipPlane = .025f;
            }
            else
            {
                var pivot = CameraSweep(center, center + self.BodyRotation.Up.ToUnity() * .12f, self.ActorId);
                var desired = CameraSweep(pivot, pivot - forward * MosquitoCameraDistance, self.ActorId);
                float available = Vector3.Distance(pivot, desired);
                cameraDistance = available < cameraDistance ? available : Mathf.MoveTowards(cameraDistance, available, Time.unscaledDeltaTime * 3);
                LocalCamera.transform.SetPositionAndRotation(pivot - forward * cameraDistance, viewRotation); LocalCamera.nearClipPlane = .01f;
            }
        }
        private Vector3 CameraSweep(Vector3 from, Vector3 to, uint actorId)
        {
            var delta = to - from;
            foreach (var hit in Physics.SphereCastAll(from, .025f, delta.normalized, delta.magnitude, World.GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!World.IsWorldCollider(hit.collider)) continue;
                var own = hit.collider.GetComponentInParent<GameplayActorProxy>(); if (own && own.ActorId == actorId) continue;
                return from + delta.normalized * Mathf.Max(0, hit.distance - .002f);
            }
            return to;
        }
    }
}
