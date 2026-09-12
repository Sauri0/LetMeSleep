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
        private readonly Queue<ActionKind> queuedActions = new Queue<ActionKind>();
        private PlayerInputCommand held;
        private GameplayRoundConfig roundConfig;
        private float cameraDistance;

        private void Awake() { World = GetComponent<UnityGameplayWorld>(); Authority = new GameplayAuthority(World); }
        public void BeginRound(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster)
        {
            if (!World) Awake();
            roundConfig = config; accumulator = sendAccumulator = snapshotAccumulator = 0; inputSequence = actionSequence = knownViewRevision = 0; yaw = pitch = 0; cameraDistance = 0; finishedSent = false;
            LatestSnapshot = null; LocalPrivate = null; held = default;
            replicaGate.Reset(config);
            queuedActions.Clear(); bots.Clear(); biteNeedsRelease = true; wasAttached = false;
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
            var delta = mouse.delta.ReadValue(); yaw = Mathf.Repeat(yaw + delta.x * MouseSensitivity + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            pitch = Mathf.Clamp(pitch + delta.y * MouseSensitivity * (InvertY ? -1 : 1), -110 * Mathf.Deg2Rad, (self.Role == PlayerRole.Human ? 75 : 89) * Mathf.Deg2Rad);
            bool e = keyboard.eKey.isPressed;
            if (!e) biteNeedsRelease = false;
            bool attached = self.BiteAttachment.HasValue;
            if (keyboard.eKey.wasPressedThisFrame && attached && wasAttached) { Enqueue(ActionKind.Detach); biteNeedsRelease = true; }
            wasAttached = attached;
            if (keyboard.spaceKey.wasPressedThisFrame && self.Role == PlayerRole.Human) Enqueue(ActionKind.Jump);
            if (keyboard.fKey.wasPressedThisFrame) Enqueue(self.Role == PlayerRole.Human ? ActionKind.Use : ActionKind.PerchToggle);
            if (keyboard.gKey.wasPressedThisFrame && self.Role == PlayerRole.Human) Enqueue(ActionKind.DropTool);
            if (mouse.leftButton.wasPressedThisFrame && self.Role == PlayerRole.Human) Enqueue(ActionKind.Primary);
            if (self.Role == PlayerRole.Mosquito) MosquitoCameraDistance = Mathf.Clamp(MosquitoCameraDistance - mouse.scroll.ReadValue().y * .0015f, 0, 2.5f);
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            float vertical = self.Role == PlayerRole.Mosquito ? (keyboard.spaceKey.isPressed ? 1 : 0) - (keyboard.leftCtrlKey.isPressed ? 1 : 0) : 0;
            held = new PlayerInputCommand(default, new Float2(x, y), vertical, yaw, pitch, LocalViewForward, keyboard.leftShiftKey.isPressed, keyboard.leftCtrlKey.isPressed, e && !biteNeedsRelease, keyboard.rKey.isPressed);
        }
        private void Enqueue(ActionKind kind) { if (queuedActions.Count < 8) queuedActions.Enqueue(kind); }
        private void SendNeutral()
        {
            if (roundConfig == null || LocalActorId == 0) return;
            held = new PlayerInputCommand(default, default, 0, yaw, pitch, LocalViewForward); SendLocal(true);
        }
        private void SendLocal(bool forceNeutral = false)
        {
            var self = LocalActor(); if (self == null) return;
            bool neutral = forceNeutral || InputBlocked || !focus;
            var h = new CommandHeader(roundConfig.SessionEpoch, roundConfig.RoundId, LocalActorId, ++inputSequence, LatestSnapshot.HostTick, self.ViewRevision);
            var input = new PlayerInputCommand(h, neutral ? default : held.MovePlanar, neutral ? 0 : held.Vertical, yaw, pitch, LocalViewForward, !neutral && held.SprintHeld, !neutral && held.CrouchHeld, !neutral && held.BiteHeld, !neutral && held.UseHeld);
            if (IsHost) Authority.SubmitInput(LocalPrincipal, input); else InputReady?.Invoke(input);
            while (queuedActions.Count > 0)
            {
                var action = queuedActions.Dequeue(); if (neutral) continue;
                var command = new PlayerActionCommand(new CommandHeader(roundConfig.SessionEpoch, roundConfig.RoundId, LocalActorId, ++actionSequence, LatestSnapshot.HostTick, self.ViewRevision), action, LocalViewForward);
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
            var doorQuery = new DoorInteractionQuery(self.ActorId, state.HostTick, origin, self.ViewForward, 1.6f);
            bool doorAhead = World.TryDoorInteraction(doorQuery, out _);
            return new BotObservation(self, visible, free, doorAhead);
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
            if (self != null && self.ViewRevision != knownViewRevision)
            {
                knownViewRevision = self.ViewRevision; queuedActions.Clear(); biteNeedsRelease = true;
                // Preserve local world look; only discard movement/action prediction from the old frame.
            }
            SnapshotApplied?.Invoke(snapshot);
            if (!IsHost) SnapshotReady?.Invoke(snapshot);
        }
        public void ApplySnapshot(GameSessionState snapshot, double renderHostTime) => ApplySnapshot(snapshot);
        public void ApplyPrivate(ActorPrivateState state)
        { if (replicaGate.AcceptPrivate(state, LocalActorId)) { LocalPrivate = state; PrivateReady?.Invoke(state); } }
        public void ApplyEvent(in GameplayEvent item)
        { if (replicaGate.AcceptEvent(item)) EventReady?.Invoke(item); }
        private void LateUpdate()
        {
            if (!UseBuiltInCamera || !LocalCamera) return;
            var self = LocalActor(); if (self == null) return;
            var forward = LocalViewForward.ToUnity(); var center = self.Position.ToUnity();
            if (self.Role == PlayerRole.Human)
            {
                LocalCamera.transform.SetPositionAndRotation(center + Vector3.up * (1.53f - .64f * self.CrouchFraction), Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-pitch * Mathf.Rad2Deg, Vector3.right));
                LocalCamera.nearClipPlane = .025f;
            }
            else
            {
                var pivot = CameraSweep(center, center + Vector3.up * .12f, self.ActorId);
                var desired = CameraSweep(pivot, pivot - forward * MosquitoCameraDistance, self.ActorId);
                float available = Vector3.Distance(pivot, desired);
                cameraDistance = available < cameraDistance ? available : Mathf.MoveTowards(cameraDistance, available, Time.unscaledDeltaTime * 3);
                LocalCamera.transform.SetPositionAndRotation(pivot - forward * cameraDistance, Quaternion.LookRotation(forward, Vector3.up)); LocalCamera.nearClipPlane = .01f;
            }
        }
        private Vector3 CameraSweep(Vector3 from, Vector3 to, uint actorId)
        {
            var delta = to - from;
            foreach (var hit in Physics.SphereCastAll(from, .025f, delta.normalized, delta.magnitude, World.GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                var own = hit.collider.GetComponentInParent<GameplayActorProxy>(); if (own && own.ActorId == actorId) continue;
                return from + delta.normalized * Mathf.Max(0, hit.distance - .002f);
            }
            return to;
        }
    }
}
