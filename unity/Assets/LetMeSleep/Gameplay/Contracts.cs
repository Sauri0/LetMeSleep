using System;
using System.Collections.Generic;
using System.Globalization;
using LetMeSleep.Core;

namespace LetMeSleep.Gameplay
{
    public static class GameplayTools
    {
        public const string Hands = "hands";
        public const string Flyswatter = "flyswatter";
        public const float FlyswatterGripToImpact = .365f;
        public const float FlyswatterHeadRadius = .085f;
        public const float FlyswatterShoulderReach = 1.05f;
        public static bool IsFlyswatter(string id) => id == Flyswatter || id == "swatter";
    }
    public readonly struct Float2
    {
        public readonly float X, Y;
        public Float2(float x, float y) { X = x; Y = y; }
        public bool IsFinite => MathEx.Finite(X) && MathEx.Finite(Y);
    }
    public readonly struct Float3
    {
        public readonly float X, Y, Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Float3 Zero => default;
        public static Float3 Up => new Float3(0, 1, 0);
        public static Float3 Forward => new Float3(0, 0, 1);
        public float LengthSquared => X * X + Y * Y + Z * Z;
        public float Length => (float)Math.Sqrt(LengthSquared);
        public bool IsFinite => MathEx.Finite(X) && MathEx.Finite(Y) && MathEx.Finite(Z);
        public Float3 Normalized => Length > .00001f ? this / Length : Zero;
        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator -(Float3 a) => a * -1;
        public static Float3 operator *(Float3 a, float b) => new Float3(a.X * b, a.Y * b, a.Z * b);
        public static Float3 operator /(Float3 a, float b) => a * (1 / b);
        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Float3 Cross(Float3 a, Float3 b) => new Float3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static Float3 ClampLength(Float3 v, float max = 1) => v.LengthSquared > max * max ? v.Normalized * max : v;
        public static Float3 MoveTowards(Float3 from, Float3 to, float delta) => from + ClampLength(to - from, delta);
        public static Float3 ProjectPlane(Float3 v, Float3 normal) => v - normal * Dot(v, normal);
    }
    public readonly struct Rotation
    {
        public readonly float X, Y, Z, W;
        public Rotation(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public static Rotation Identity => new Rotation(0, 0, 0, 1);
        public static Rotation Yaw(float radians) => new Rotation(0, (float)Math.Sin(radians / 2), 0, (float)Math.Cos(radians / 2));
    }
    public static class MathEx
    {
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        public static bool Newer(uint a, uint b) => unchecked((int)(a - b)) > 0;
        public static Float3 Aim(float yaw, float pitch) => new Float3((float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)Math.Sin(pitch), (float)(Math.Cos(yaw) * Math.Cos(pitch)));
    }
    public enum LifeState : byte { Active, Flying, ApproachingSurface, Surface, PreparingBite, Biting, Falling, Fainted, Stunned, Recovering }
    public enum SimulationPhase : byte { Running, Ended }
    public enum StrikePhase : byte { None, Windup, Active, Recovery }
    public enum ActionKind : byte { Jump, Primary, PerchToggle, Detach, Use }
    public enum CommandReject : byte { None, UnknownActor, WrongOwner, WrongRound, StaleSequence, InvalidNumber, InvalidDirection, WrongRole, InvalidState, Cooldown, OutOfReach, Obstructed, OldViewRevision, RateLimited }
    public enum RoundEndReason : byte { None, BloodGoal, TimeExpired, OpponentLeft, Aborted }
    public enum ActorRemovalReason : byte { Left, Disconnected }
    public enum GameplayEventKind : byte { StrikeStarted, StrikeImpact, BiteStarted, BiteEnded, MosquitoKnockedDown, RecoveryStarted, HelpStarted, HelpEnded, Recovered, HumanFainted, DoorChanged, RoundEnded }
    public enum InteractionHint : byte { None, ContactRequired, Preparing, Biting, Helping, Stunned, Recovering, Door, Blocked }
    public enum DoorUseResult : byte { Accepted, WrongRole, InvalidState, NoDoor, OutOfReach, Occluded, StaleRevision, Cooldown, Blocked }
    public readonly struct CommandHeader
    {
        public readonly ulong SessionEpoch, RoundId;
        public readonly uint ActorId, Sequence, ClientTick, ViewRevision;
        public CommandHeader(ulong epoch, ulong round, uint actor, uint sequence, uint clientTick, uint viewRevision)
        { SessionEpoch = epoch; RoundId = round; ActorId = actor; Sequence = sequence; ClientTick = clientTick; ViewRevision = viewRevision; }
    }
    public readonly struct PlayerInputCommand
    {
        public readonly CommandHeader Header;
        public readonly Float2 MovePlanar;
        public readonly float Vertical, ViewYawRadians, ViewPitchRadians;
        public readonly Float3 AimForward;
        public readonly bool SprintHeld, CrouchHeld, BiteHeld, UseHeld;
        public PlayerInputCommand(CommandHeader header, Float2 move, float vertical, float yaw, float pitch, Float3 aim, bool sprint = false, bool crouch = false, bool bite = false, bool use = false)
        { Header = header; MovePlanar = move; Vertical = vertical; ViewYawRadians = yaw; ViewPitchRadians = pitch; AimForward = aim; SprintHeld = sprint; CrouchHeld = crouch; BiteHeld = bite; UseHeld = use; }
    }
    public readonly struct PlayerActionCommand
    {
        public readonly CommandHeader Header;
        public readonly ActionKind Kind;
        public readonly Float3 AimForward;
        public PlayerActionCommand(CommandHeader header, ActionKind kind, Float3 aim) { Header = header; Kind = kind; AimForward = aim; }
    }
    public readonly struct HostTick
    {
        public readonly uint Index;
        public readonly float DeltaSeconds;
        public HostTick(uint index, float deltaSeconds = 1f / 30) { Index = index; DeltaSeconds = deltaSeconds; }
    }
    public sealed class BalanceProfile
    {
        public const string Id = "alfa-blood-initial-1";
        public float RecoveryBaseSeconds { get; }
        public float FullExtractionSeconds { get; }
        public float PreparationSeconds { get; }
        public float HelpMultiplier { get; }
        public float ProtectionSeconds { get; }
        public string Hash => Id + ":" + RecoveryBaseSeconds.ToString("R", CultureInfo.InvariantCulture) + ":" + FullExtractionSeconds.ToString("R", CultureInfo.InvariantCulture) + ":" + PreparationSeconds.ToString("R", CultureInfo.InvariantCulture) + ":" + HelpMultiplier.ToString("R", CultureInfo.InvariantCulture) + ":" + ProtectionSeconds.ToString("R", CultureInfo.InvariantCulture);
        public BalanceProfile(float recoveryBaseSeconds = 12, float fullExtractionSeconds = 8, float preparationSeconds = .6f, float helpMultiplier = 3, float protectionSeconds = 1.5f)
        {
            if (!MathEx.Finite(recoveryBaseSeconds) || recoveryBaseSeconds < 1 || recoveryBaseSeconds > 120 || !MathEx.Finite(fullExtractionSeconds) || fullExtractionSeconds < 1 || fullExtractionSeconds > 60 || !MathEx.Finite(preparationSeconds) || preparationSeconds < .1f || preparationSeconds > 5 || !MathEx.Finite(helpMultiplier) || helpMultiplier < 1 || helpMultiplier > 5 || !MathEx.Finite(protectionSeconds) || protectionSeconds < 0 || protectionSeconds > 10) throw new ArgumentOutOfRangeException(nameof(recoveryBaseSeconds));
            RecoveryBaseSeconds = recoveryBaseSeconds; FullExtractionSeconds = fullExtractionSeconds; PreparationSeconds = preparationSeconds; HelpMultiplier = helpMultiplier; ProtectionSeconds = protectionSeconds;
        }
    }
    public sealed class GameplayRoundConfig
    {
        public ulong SessionEpoch { get; }
        public ulong RoundId { get; }
        public string MapId { get; }
        public string ContentHash { get; }
        public string BalanceHash => Balance.Hash;
        public int HostTickRate => 30;
        public uint RoundDurationTicks { get; }
        public float BloodGoal { get; }
        public BalanceProfile Balance { get; }
        public IReadOnlyList<DoorDefinition> DoorDefinitions { get; }
        public GameplayRoundConfig(ulong epoch, ulong round, string mapId, string contentHash, int roundSeconds = 180, float bloodGoal = 20, BalanceProfile balance = null, IReadOnlyList<DoorDefinition> doors = null)
        {
            if (epoch == 0 || round == 0 || string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(contentHash) || roundSeconds < 30 || roundSeconds > 1800 || !MathEx.Finite(bloodGoal) || bloodGoal <= 0 || bloodGoal > 1000) throw new ArgumentException("Invalid gameplay round.");
            SessionEpoch = epoch; RoundId = round; MapId = mapId; ContentHash = contentHash; RoundDurationTicks = (uint)(roundSeconds * 30); BloodGoal = bloodGoal; Balance = balance ?? new BalanceProfile();
            DoorDefinitions = Array.AsReadOnly(Copy(doors));
        }
        internal static T[] Copy<T>(IReadOnlyList<T> list) { var copy = new T[list?.Count ?? 0]; for (int i = 0; i < copy.Length; i++) copy[i] = list[i]; return copy; }
    }
    public readonly struct SpawnActor
    {
        public readonly uint ActorId;
        public readonly string OwnerPuid, SpawnId, CosmeticProfileId;
        public readonly PlayerRole Role;
        public readonly Float3 Position;
        public readonly bool IsBot;
        public SpawnActor(uint actorId, string ownerPuid, PlayerRole role, Float3 position, string spawnId = "default", string cosmeticProfileId = "default", bool isBot = false)
        { ActorId = actorId; OwnerPuid = ownerPuid; Role = role; Position = position; SpawnId = spawnId; CosmeticProfileId = cosmeticProfileId; IsBot = isBot; }
    }
    public readonly struct SurfaceAttachment
    {
        public readonly uint SurfaceId, Revision;
        public readonly Float3 LocalPoint, LocalNormal, TangentForward;
        public SurfaceAttachment(uint surfaceId, uint revision, Float3 point, Float3 normal, Float3 tangent) { SurfaceId = surfaceId; Revision = revision; LocalPoint = point; LocalNormal = normal; TangentForward = tangent; }
    }
    public readonly struct BiteAttachment
    {
        public readonly uint VictimId, SurfaceId, PoseRevision;
        public readonly Float3 LocalPoint, LocalNormal;
        public BiteAttachment(uint victim, uint surface, Float3 point, Float3 normal, uint revision) { VictimId = victim; SurfaceId = surface; LocalPoint = point; LocalNormal = normal; PoseRevision = revision; }
    }
    public readonly struct StrikeState
    {
        public readonly ulong StrikeId;
        public readonly string ToolId;
        public readonly int Hand;
        public readonly StrikePhase Phase;
        public readonly uint StartTick;
        public readonly Float3 Origin, Target, Normal;
        public readonly float Progress;
        public StrikeState(ulong id, string tool, int hand, StrikePhase phase, uint start, Float3 origin, Float3 target, Float3 normal, float progress)
        { StrikeId = id; ToolId = tool; Hand = hand; Phase = phase; StartTick = start; Origin = origin; Target = target; Normal = normal; Progress = progress; }
    }
    public sealed class ActorSnapshot
    {
        public uint ActorId { get; }
        public PlayerRole Role { get; }
        public LifeState LifeState { get; }
        public uint StateRevision { get; }
        public Float3 Position { get; }
        public Float3 Velocity { get; }
        public Rotation BodyRotation { get; }
        public Float3 ViewForward { get; }
        public float ViewYawRadians { get; }
        public float ViewPitchRadians { get; }
        public uint ViewRevision { get; }
        public uint PoseRevision { get; }
        public bool Grounded { get; }
        public float CrouchFraction { get; }
        public float MotionPhase { get; }
        public SurfaceAttachment? SurfaceAttachment { get; }
        public BiteAttachment? BiteAttachment { get; }
        public StrikeState StrikeState { get; }
        public uint RecoveryEndTick { get; }
        public ActorSnapshot(uint id, PlayerRole role, LifeState state, uint revision, Float3 position, Float3 velocity, Rotation body, Float3 forward, float yaw, float pitch, uint viewRevision, uint poseRevision, bool grounded, float crouch, float motion, SurfaceAttachment? surface, BiteAttachment? bite, StrikeState strike, uint recoveryEnd)
        { ActorId = id; Role = role; LifeState = state; StateRevision = revision; Position = position; Velocity = velocity; BodyRotation = body; ViewForward = forward; ViewYawRadians = yaw; ViewPitchRadians = pitch; ViewRevision = viewRevision; PoseRevision = poseRevision; Grounded = grounded; CrouchFraction = crouch; MotionPhase = motion; SurfaceAttachment = surface; BiteAttachment = bite; StrikeState = strike; RecoveryEndTick = recoveryEnd; }
    }
    public sealed class ActorPrivateState
    {
        public ulong SessionEpoch { get; }
        public ulong RoundId { get; }
        public uint HostTick { get; }
        public uint ActorId { get; }
        public uint LastAcceptedInputSequence { get; }
        public uint LastAcceptedActionSequence { get; }
        public CommandReject Rejection { get; }
        public InteractionHint InteractionHint { get; }
        public float PreparationProgress { get; }
        public float ExtractionProgress { get; }
        public float RecoverySeconds { get; }
        public uint HelpTargetId { get; }
        public bool CanAct { get; }
        public DoorUseResult LastDoorResult { get; }
        public ActorPrivateState(uint actor, uint input, uint action, CommandReject rejection, InteractionHint hint, float preparation, float extraction, float recovery, uint help, bool canAct, DoorUseResult door, ulong sessionEpoch = 0, ulong roundId = 0, uint hostTick = 0)
        { ActorId = actor; LastAcceptedInputSequence = input; LastAcceptedActionSequence = action; Rejection = rejection; InteractionHint = hint; PreparationProgress = preparation; ExtractionProgress = extraction; RecoverySeconds = recovery; HelpTargetId = help; CanAct = canAct; LastDoorResult = door; SessionEpoch = sessionEpoch; RoundId = roundId; HostTick = hostTick; }
    }
    public readonly struct DoorDefinition
    {
        public readonly uint DoorId, SurfaceId;
        public readonly Float3 HingePosition, LeafSize, LeafCenterLocal, HandleLocalPoint;
        public readonly Rotation ClosedRotation, LeafRotationLocal;
        public readonly float OpenSign, OpenAngleRadians, InitialAngleRadians;
        public DoorDefinition(uint id, uint surface, Float3 hinge, Rotation closed, Float3 size, Float3 handle, float openSign = 1, float openAngleRadians = 1.5707963f, float initialAngleRadians = 0, Float3 leafCenterLocal = default, Rotation leafRotationLocal = default)
        { DoorId = id; SurfaceId = surface; HingePosition = hinge; ClosedRotation = closed; LeafSize = size; HandleLocalPoint = handle; OpenSign = openSign; OpenAngleRadians = openAngleRadians; InitialAngleRadians = initialAngleRadians; LeafCenterLocal = leafCenterLocal; LeafRotationLocal = leafRotationLocal.X == 0 && leafRotationLocal.Y == 0 && leafRotationLocal.Z == 0 && leafRotationLocal.W == 0 ? Rotation.Identity : leafRotationLocal; }
    }
    public readonly struct DoorSnapshot
    {
        public readonly uint DoorId, SurfaceId, Revision, LastChangedTick;
        public readonly float AngleRadians, TargetAngleRadians, AngularVelocity;
        public readonly bool Moving, Blocked;
        public DoorSnapshot(uint id, uint surface, uint revision, float angle, float target, float velocity, bool moving, bool blocked, uint tick)
        { DoorId = id; SurfaceId = surface; Revision = revision; AngleRadians = angle; TargetAngleRadians = target; AngularVelocity = velocity; Moving = moving; Blocked = blocked; LastChangedTick = tick; }
    }
    public readonly struct GameplayEvent
    {
        public readonly ulong SessionEpoch, RoundId, EventId;
        public readonly uint HostTick, SourceActorId, TargetActorId, StateRevision;
        public readonly GameplayEventKind Kind;
        public readonly Float3 Position, Normal;
        public readonly DoorSnapshot? Door;
        public readonly RoundEndReason Reason;
        public GameplayEvent(ulong epoch, ulong round, ulong id, uint tick, GameplayEventKind kind, uint source, uint target, uint revision, Float3 position, Float3 normal, DoorSnapshot? door = null, RoundEndReason reason = RoundEndReason.None)
        { SessionEpoch = epoch; RoundId = round; EventId = id; HostTick = tick; Kind = kind; SourceActorId = source; TargetActorId = target; StateRevision = revision; Position = position; Normal = normal; Door = door; Reason = reason; }
    }
    public sealed class GameSessionState
    {
        public ulong SessionEpoch { get; }
        public ulong RoundId { get; }
        public uint HostTick { get; }
        public double HostTime => HostTick / 30.0;
        public string MapId { get; }
        public string ContentHash { get; }
        public string BalanceHash { get; }
        public SimulationPhase SimulationPhase { get; }
        public uint TimeRemainingTicks { get; }
        public float BloodCollected { get; }
        public float BloodGoal { get; }
        public RoundEndReason Result { get; }
        public PlayerRole Winner { get; }
        public IReadOnlyList<ActorSnapshot> Actors { get; }
        public IReadOnlyList<DoorSnapshot> Doors { get; }
        public GameSessionState(GameplayRoundConfig config, uint tick, SimulationPhase phase, float blood, RoundEndReason result, PlayerRole winner, IReadOnlyList<ActorSnapshot> actors, IReadOnlyList<DoorSnapshot> doors)
        { SessionEpoch = config.SessionEpoch; RoundId = config.RoundId; HostTick = tick; MapId = config.MapId; ContentHash = config.ContentHash; BalanceHash = config.BalanceHash; SimulationPhase = phase; TimeRemainingTicks = tick >= config.RoundDurationTicks ? 0 : config.RoundDurationTicks - tick; BloodCollected = blood; BloodGoal = config.BloodGoal; Result = result; Winner = winner; Actors = Array.AsReadOnly(GameplayRoundConfig.Copy(actors)); Doors = Array.AsReadOnly(GameplayRoundConfig.Copy(doors)); }
    }
    public interface IGameplayCommandSink
    {
        CommandReject SubmitInput(string authenticatedPuid, in PlayerInputCommand command);
        CommandReject SubmitAction(string authenticatedPuid, in PlayerActionCommand command);
    }
    public interface IGameplayAuthority : IGameplayCommandSink
    {
        void BeginRound(GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster);
        void Advance(in HostTick tick);
        void RemoveActor(uint actorId, ActorRemovalReason reason);
        GameSessionState CaptureSnapshot();
        ActorPrivateState CapturePrivate(uint actorId);
        IReadOnlyList<GameplayEvent> DrainEvents();
        void EndRound(RoundEndReason reason);
    }
    public interface IGameplayPresentationSink
    {
        void ApplySnapshot(GameSessionState snapshot, double renderHostTime);
        void ApplyPrivate(ActorPrivateState state);
        void ApplyEvent(in GameplayEvent item);
    }
}
