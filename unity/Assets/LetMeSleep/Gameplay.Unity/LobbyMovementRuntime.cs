using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LetMeSleep.Gameplay.Unity
{
    public readonly struct LobbySpawn
    {
        public readonly string PlayerId;
        public readonly Vector3 Position;
        public readonly float Yaw;
        public LobbySpawn(string playerId, Vector3 position, float yaw = 0) { PlayerId = playerId; Position = position; Yaw = yaw; }
    }
    public readonly struct LobbyMoveCommand
    {
        public readonly ulong SessionEpoch;
        public readonly uint RosterRevision, Sequence;
        public readonly Vector2 Move;
        public readonly float Yaw;
        public LobbyMoveCommand(ulong epoch, uint revision, uint sequence, Vector2 move, float yaw)
        { SessionEpoch = epoch; RosterRevision = revision; Sequence = sequence; Move = move; Yaw = yaw; }
    }
    public readonly struct LobbyPose
    {
        public readonly string PlayerId;
        public readonly Vector3 Position, Velocity;
        public readonly float Yaw, MotionPhase;
        public readonly bool Grounded;
        public readonly uint LastInputSequence;
        public LobbyPose(string id, Vector3 position, Vector3 velocity, float yaw, bool grounded, uint ack, float motionPhase = 0)
        { PlayerId = id; Position = position; Velocity = velocity; Yaw = yaw; Grounded = grounded; LastInputSequence = ack; MotionPhase = motionPhase; }
    }
    public sealed class LobbySnapshot
    {
        public ulong SessionEpoch { get; }
        public uint RosterRevision { get; }
        public uint HostTick { get; }
        public IReadOnlyList<LobbyPose> Poses { get; }
        public LobbySnapshot(ulong epoch, uint revision, uint tick, IReadOnlyList<LobbyPose> poses)
        {
            SessionEpoch = epoch; RosterRevision = revision; HostTick = tick;
            var copy = new LobbyPose[poses.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = poses[i]; Poses = Array.AsReadOnly(copy);
        }
    }
    // Waiting room locomotion only: no roles, damage, extraction, or gameplay round authority.
    [RequireComponent(typeof(UnityGameplayWorld))]
    public sealed class LobbyMovementRuntime : MonoBehaviour
    {
        public GameObject HumanPrefab;
        public Camera LocalCamera;
        public float MouseSensitivity = .002f;
        public bool InvertY;
        public float CameraDistance = 2.7f;
        public bool CaptureLocalInput = true;
        public bool AutomaticTick = true;
        public bool IsHost { get; private set; }
        public bool IsBound { get; private set; }
        public bool InputBlocked { get; private set; }
        public string LocalPlayerId { get; private set; }
        public ulong SessionEpoch { get; private set; }
        public uint RosterRevision { get; private set; }
        public LobbySnapshot LatestSnapshot { get; private set; }
        public event Action<LobbyMoveCommand> InputReady;
        public event Action<LobbySnapshot> SnapshotReady;
        public event Action<LobbySnapshot> SnapshotApplied;
        public event Action<string, GameObject> VisualCreated;
        private sealed class Member
        {
            internal string Id;
            internal uint ActorId, Sequence, InputTick, RateTick;
            internal int InputCount;
            internal bool HasInput, Grounded;
            internal Vector2 Move;
            internal Vector3 Position, Velocity;
            internal float Yaw, Motion;
            internal Transform Visual;
        }
        private readonly Dictionary<string, Member> members = new Dictionary<string, Member>(StringComparer.Ordinal);
        private readonly Queue<LobbyMoveCommand> prediction = new Queue<LobbyMoveCommand>();
        private readonly List<LobbySnapshot> history = new List<LobbySnapshot>();
        private UnityGameplayWorld world;
        private uint tick, sequence;
        private float accumulator, publication, yaw, pitch = .15f, receivedAt, cameraLength;
        private Vector2 move;
        private bool focus = true;
        private const float Dt = 1f / 30;

        private void Awake() { world = GetComponent<UnityGameplayWorld>(); }
        public void Bind(ulong epoch, string localPlayerId, bool isHost, IReadOnlyList<LobbySpawn> roster, uint rosterRevision = 1)
        {
            if (epoch == 0 || rosterRevision == 0 || !ValidId(localPlayerId)) throw new ArgumentException("Invalid lobby binding.");
            if (!world) Awake();
            Unbind(); SessionEpoch = epoch; LocalPlayerId = localPlayerId; IsHost = isHost; tick = sequence = 0; accumulator = publication = 0; yaw = 0; pitch = .15f; cameraLength = 0; IsBound = true;
            SetRoster(roster, rosterRevision);
            if (members.TryGetValue(LocalPlayerId, out var local)) yaw = local.Yaw;
            SetInputBlocked(false);
        }
        public void CreateRoster(IReadOnlyList<LobbySpawn> roster, uint revision) => SetRoster(roster, revision);
        public void SetRoster(IReadOnlyList<LobbySpawn> roster, uint revision)
        {
            if (!IsBound || revision == 0 || revision < RosterRevision || roster == null || roster.Count < 1 || roster.Count > 16) throw new ArgumentException("Invalid lobby roster.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spawn in roster) if (!ValidId(spawn.PlayerId) || !ids.Add(spawn.PlayerId) || !Finite(spawn.Position) || spawn.Position.magnitude > 10000 || !MathEx.Finite(spawn.Yaw)) throw new ArgumentException("Invalid lobby spawn.");
            if (revision == RosterRevision)
            {
                if (ids.Count == members.Count && ids.All(id => members.ContainsKey(id))) return;
                throw new ArgumentException("Roster changes require a new revision.");
            }
            var previous = new Dictionary<string, Member>(members, StringComparer.Ordinal); members.Clear();
            var physical = new List<SpawnActor>(); uint actorId = 0;
            foreach (var spawn in roster.OrderBy(s => s.PlayerId, StringComparer.Ordinal))
            {
                var member = previous.TryGetValue(spawn.PlayerId, out var existing) ? existing : new Member { Id = spawn.PlayerId, Position = spawn.Position, Yaw = spawn.Yaw };
                member.ActorId = ++actorId; member.Move = default; member.HasInput = false; member.Visual = null; members.Add(member.Id, member);
                physical.Add(new SpawnActor(member.ActorId, member.Id, PlayerRole.Human, member.Position.ToFloat()));
            }
            RosterRevision = revision; prediction.Clear(); history.Clear(); LatestSnapshot = null;
            world.BeginRound(physical, world.GetDoorDefinitions());
            foreach (var member in members.Values)
            {
                if (!HumanPrefab) continue; // A dedicated host may omit presentation entirely.
                var instance = Instantiate(HumanPrefab, world.Actors[member.ActorId].transform); instance.name = "LobbyVisual_" + member.ActorId;
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var animator = instance.GetComponentInChildren<Animator>(); if (animator) animator.applyRootMotion = false;
                member.Visual = instance.transform; VisualCreated?.Invoke(member.Id, instance);
            }
            SyncWorld();
            if (IsHost) Publish();
        }
        public void Unbind()
        {
            SetInputBlocked(true);
            IsBound = false; members.Clear(); prediction.Clear(); history.Clear(); LatestSnapshot = null; RosterRevision = 0; move = default;
            if (world) world.SynchronizeActors(Array.Empty<ActorSnapshot>());
        }
        public CommandReject SubmitMove(string authenticatedId, Vector2 requestedMove, float requestedYaw, uint requestedSequence)
            => SubmitMove(authenticatedId, new LobbyMoveCommand(SessionEpoch, RosterRevision, requestedSequence, requestedMove, requestedYaw));
        public CommandReject SubmitMove(string authenticatedId, in LobbyMoveCommand command)
        {
            if (!IsBound || !IsHost || command.SessionEpoch != SessionEpoch || command.RosterRevision != RosterRevision) return CommandReject.WrongRound;
            if (authenticatedId == null || !members.TryGetValue(authenticatedId, out var member)) return CommandReject.WrongOwner;
            if (!MathEx.Finite(command.Move.x) || !MathEx.Finite(command.Move.y) || !MathEx.Finite(command.Yaw) || Math.Abs(command.Yaw) > 10000) return CommandReject.InvalidNumber;
            if (member.HasInput && !MathEx.Newer(command.Sequence, member.Sequence)) return CommandReject.StaleSequence;
            if (tick - member.RateTick >= 30) { member.RateTick = tick; member.InputCount = 0; }
            if (++member.InputCount > 60) return CommandReject.RateLimited;
            member.Move = Vector2.ClampMagnitude(command.Move, 1); member.Yaw = command.Yaw; member.Sequence = command.Sequence; member.InputTick = tick; member.HasInput = true; return CommandReject.None;
        }
        public void SetInputBlocked(bool blocked)
        {
            InputBlocked = blocked; move = default; prediction.Clear();
            if (CaptureLocalInput) { Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = blocked; }
            if (blocked) SendNeutral();
        }
        private void OnApplicationFocus(bool value) { focus = value; if (!value) { move = default; prediction.Clear(); SendNeutral(); } }
        private void OnDisable() { move = default; prediction.Clear(); SendNeutral(); }
        private void OnDestroy() { if (world) world.SynchronizeActors(Array.Empty<ActorSnapshot>()); }
        private void Update()
        {
            if (!IsBound) return;
            if (CaptureLocalInput) Poll();
            if (AutomaticTick)
            {
                accumulator += Time.unscaledDeltaTime; int steps = 0;
                while (accumulator >= Dt && steps++ < 8) { StepLocalAndHost(); accumulator -= Dt; }
            }
            RenderRemotes();
        }
        private void Poll()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (InputBlocked || !focus || keyboard == null || mouse == null) { move = default; return; }
            var delta = mouse.delta.ReadValue(); yaw = Mathf.Repeat(yaw + delta.x * MouseSensitivity + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            pitch = Mathf.Clamp(pitch + delta.y * MouseSensitivity * (InvertY ? -1 : 1), -65 * Mathf.Deg2Rad, 65 * Mathf.Deg2Rad);
            move = Vector2.ClampMagnitude(new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
            CameraDistance = Mathf.Clamp(CameraDistance - mouse.scroll.ReadValue().y * .002f, .5f, 4);
        }
        public void TickHost()
        {
            if (!IsBound || !IsHost) return;
            tick++;
            foreach (var member in members.Values.OrderBy(m => m.ActorId))
            {
                if (!member.HasInput || tick - member.InputTick > 7) member.Move = default;
                Simulate(member, member.Move, member.Yaw);
            }
            SyncWorld(); publication += Dt;
            if (publication >= .05f) { publication -= .05f; Publish(); }
        }
        private void StepLocalAndHost()
        {
            if (CaptureLocalInput && members.TryGetValue(LocalPlayerId, out var local))
            {
                var command = new LobbyMoveCommand(SessionEpoch, RosterRevision, ++sequence, InputBlocked || !focus ? Vector2.zero : move, yaw);
                if (IsHost) SubmitMove(LocalPlayerId, command);
                else
                {
                    InputReady?.Invoke(command);
                    prediction.Enqueue(command); while (prediction.Count > 64) prediction.Dequeue();
                    Simulate(local, command.Move, command.Yaw); SyncWorld();
                }
            }
            if (IsHost) TickHost();
        }
        private void SendNeutral()
        {
            if (!IsBound || !members.ContainsKey(LocalPlayerId)) return;
            var command = new LobbyMoveCommand(SessionEpoch, RosterRevision, ++sequence, Vector2.zero, yaw);
            if (IsHost) SubmitMove(LocalPlayerId, command); else InputReady?.Invoke(command);
        }
        private void Simulate(Member member, Vector2 input, float facing)
        {
            member.Yaw = facing; var forward = MathEx.Aim(facing, 0).ToUnity(); var right = Vector3.Cross(Vector3.up, forward);
            var flat = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1) * 3.1f;
            float vertical = member.Grounded ? -.5f : member.Velocity.y - 12 * Dt;
            var velocity = new Vector3(flat.x, vertical, flat.z);
            var result = world.MoveHuman(new MotorQuery(member.ActorId, member.Position.ToFloat(), velocity.ToFloat(), Dt, 1.72f, .25f, 0, member.Grounded));
            var position = result.Position.ToUnity(); member.Motion += (position - member.Position).magnitude / 1.2f; member.Position = position; member.Velocity = result.Velocity.ToUnity(); member.Grounded = result.Grounded;
        }
        private void SyncWorld()
        {
            var poses = new List<ActorSnapshot>(members.Count);
            foreach (var member in members.Values)
                poses.Add(new ActorSnapshot(member.ActorId, PlayerRole.Human, LifeState.Active, 1, member.Position.ToFloat(), member.Velocity.ToFloat(), Rotation.Yaw(member.Yaw), MathEx.Aim(member.Yaw, 0), member.Yaw, 0, 1, tick, member.Grounded, 0, member.Motion, null, null, default, 0));
            world.SynchronizeActors(poses);
        }
        public LobbySnapshot CaptureSnapshot()
        {
            if (!IsBound) return null;
            return new LobbySnapshot(SessionEpoch, RosterRevision, tick, members.Values.OrderBy(m => m.ActorId).Select(m => new LobbyPose(m.Id, m.Position, m.Velocity, m.Yaw, m.Grounded, m.Sequence, m.Motion)).ToArray());
        }
        public bool TryGetVisual(string playerId, out GameObject visual)
        {
            visual = null;
            if (playerId == null || !members.TryGetValue(playerId, out var member) || !member.Visual) return false;
            visual = member.Visual.gameObject; return true;
        }
        private void Publish() { LatestSnapshot = CaptureSnapshot(); SnapshotReady?.Invoke(LatestSnapshot); SnapshotApplied?.Invoke(LatestSnapshot); }
        public bool ApplySnapshot(LobbySnapshot snapshot)
        {
            if (!IsBound || IsHost || snapshot == null || snapshot.SessionEpoch != SessionEpoch || snapshot.RosterRevision != RosterRevision || snapshot.Poses.Count != members.Count || snapshot.Poses.Count > 16 || (LatestSnapshot != null && snapshot.HostTick <= LatestSnapshot.HostTick)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pose in snapshot.Poses)
                if (!ValidId(pose.PlayerId) || !ids.Add(pose.PlayerId) || !members.ContainsKey(pose.PlayerId) || !Finite(pose.Position) || !Finite(pose.Velocity) || pose.Position.magnitude > 10000 || pose.Velocity.magnitude > 200 || !MathEx.Finite(pose.Yaw) || Math.Abs(pose.Yaw) > 10000 || !MathEx.Finite(pose.MotionPhase) || pose.MotionPhase < 0) return false;
            LatestSnapshot = snapshot; tick = snapshot.HostTick; receivedAt = Time.unscaledTime;
            history.Add(snapshot); while (history.Count > 8) history.RemoveAt(0);
            foreach (var pose in snapshot.Poses)
            {
                var member = members[pose.PlayerId]; member.Position = pose.Position; member.Velocity = pose.Velocity; member.Yaw = pose.Yaw; member.Grounded = pose.Grounded; member.Sequence = pose.LastInputSequence; member.Motion = pose.MotionPhase;
            }
            SyncWorld();
            if (members.TryGetValue(LocalPlayerId, out var local))
            {
                while (prediction.Count > 0 && !MathEx.Newer(prediction.Peek().Sequence, local.Sequence)) prediction.Dequeue();
                foreach (var command in prediction) Simulate(local, command.Move, command.Yaw);
                SyncWorld();
            }
            SnapshotApplied?.Invoke(snapshot); return true;
        }
        private void RenderRemotes()
        {
            if (IsHost || history.Count == 0) return;
            double renderTime = LatestSnapshot.HostTick / 30.0 + Math.Min(.1f, Time.unscaledTime - receivedAt) - .1;
            var before = history[0]; var after = history[history.Count - 1];
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].HostTick / 30.0 <= renderTime) before = history[i];
                if (history[i].HostTick / 30.0 >= renderTime) { after = history[i]; break; }
            }
            float t = after.HostTick == before.HostTick ? 1 : Mathf.Clamp01((float)((renderTime * 30 - before.HostTick) / (after.HostTick - before.HostTick)));
            foreach (var target in after.Poses)
            {
                if (target.PlayerId == LocalPlayerId || !members.TryGetValue(target.PlayerId, out var member) || !member.Visual) continue;
                var from = before.Poses.First(p => p.PlayerId == target.PlayerId);
                member.Visual.SetPositionAndRotation(Vector3.Lerp(from.Position, target.Position, t), Quaternion.Euler(0, Mathf.LerpAngle(from.Yaw * Mathf.Rad2Deg, target.Yaw * Mathf.Rad2Deg, t), 0));
            }
        }
        private void LateUpdate()
        {
            if (!IsBound || !LocalCamera || !members.TryGetValue(LocalPlayerId, out var local)) return;
            var pivot = local.Position + Vector3.up * 1.42f; var rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0); var direction = rotation * Vector3.back;
            float safe = CameraDistance;
            foreach (var hit in Physics.SphereCastAll(pivot, .12f, direction, CameraDistance, world.GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!world.IsWorldCollider(hit.collider)) continue;
                var actor = hit.collider.GetComponentInParent<GameplayActorProxy>(); if (actor && actor.ActorId == local.ActorId) continue;
                safe = Mathf.Max(0, hit.distance - .01f); break;
            }
            cameraLength = safe < cameraLength ? safe : Mathf.MoveTowards(cameraLength, safe, Time.unscaledDeltaTime * 5);
            LocalCamera.transform.SetPositionAndRotation(pivot + direction * cameraLength, rotation); LocalCamera.nearClipPlane = .04f;
        }
        private static bool Finite(Vector3 p) => MathEx.Finite(p.x) && MathEx.Finite(p.y) && MathEx.Finite(p.z);
        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && !id.Any(char.IsControl);
    }
}
