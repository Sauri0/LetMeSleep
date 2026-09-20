using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    public enum AlfaUiScreen
    {
        MainMenu,
        OnlineChoice,
        CreateRoom,
        JoinRoom,
        Lobby,
        Training,
        Customization,
        Settings,
        Gameplay,
        Pause,
        Results
    }

    public enum OnlineOperationPhase
    {
        Idle,
        Connecting,
        Creating,
        Searching,
        Entering,
        Cancelled,
        RecoverableError,
        IncompatibleVersion,
        RoomClosed
    }

    public enum UiFeedbackKind
    {
        Select,
        Confirm,
        Error
    }

    public enum AlfaRole { Human, Mosquito }
    public enum MatchOutcome { Interrupted, Humans, Mosquitoes }
    public enum PreviewAngle { Front, Side, Back }
    public enum HudActorState { Normal, Extracting, Bitten, Recovering, Fainted, Stunned, Attached, Spectating }

    public sealed class AlfaUiDependencies
    {
        public TMP_FontAsset HeadingFont { get; }
        public TMP_FontAsset BodyFont { get; }
        public Sprite PanelSprite { get; }
        public Sprite ButtonSprite { get; }
        public bool PersistentAcrossScenes { get; }
        public CharacterPreviewSetup Preview { get; }

        public AlfaUiDependencies(
            TMP_FontAsset headingFont = null,
            TMP_FontAsset bodyFont = null,
            Sprite panelSprite = null,
            Sprite buttonSprite = null,
            CharacterPreviewSetup preview = null,
            bool persistentAcrossScenes = true)
        {
            HeadingFont = headingFont;
            BodyFont = bodyFont;
            PanelSprite = panelSprite;
            ButtonSprite = buttonSprite;
            Preview = preview;
            PersistentAcrossScenes = persistentAcrossScenes;
        }
    }

    public sealed class CharacterPreviewSetup
    {
        public Camera Camera { get; }
        public Transform Stage { get; }
        public RenderTexture Texture { get; }
        public GameObject HumanPrefab { get; }
        public GameObject MosquitoPrefab { get; }
        public Action<GameObject, Camera> OnPreviewCreated { get; }

        public CharacterPreviewSetup(Camera camera, Transform stage, RenderTexture texture, GameObject humanPrefab, GameObject mosquitoPrefab)
            : this(camera, stage, texture, humanPrefab, mosquitoPrefab, null)
        {
        }

        public CharacterPreviewSetup(Camera camera, Transform stage, RenderTexture texture, GameObject humanPrefab,
            GameObject mosquitoPrefab, Action<GameObject, Camera> onPreviewCreated)
        {
            Camera = camera;
            Stage = stage;
            Texture = texture;
            HumanPrefab = humanPrefab;
            MosquitoPrefab = mosquitoPrefab;
            OnPreviewCreated = onPreviewCreated;
        }

        public bool IsUsable => Camera != null && Stage != null && Texture != null && HumanPrefab != null && MosquitoPrefab != null;
    }

    public interface IMenuActions
    {
        void CreateRoom(string playerName);
        void JoinRoom(string playerName, string normalizedRoomCode);
        void CancelOnline();
        void CancelTraining();
        void CopyRoomCode(string groupedRoomCode);
        void SetReady(bool ready);
        void SetHumanCount(int? humanCount);
        void StartRound();
        void LeaveRoom();
        void StartTraining(AlfaRole role, string modeId, string mapId);
        void PreviewCustomization(BasicCustomizationDraft draft);
        void SaveCustomization(BasicCustomizationDraft draft);
        void ApplySettings(AlfaSettingsDraft draft);
        void SetGameplayInputBlocked(bool blocked);
        void ResumeGame();
        void ReturnToLobby();
        void SetLobbyExploration(bool exploring);
        void QuitGame();
    }

    public interface IRoomMapActions
    {
        void SetRoomMap(string mapId);
    }

    public interface ISpectatorActions { void SpectateNext(); }

    public interface IRoomModeActions
    {
        void SetRoomMode(string modeId);
    }

    public sealed class OnlineUiState
    {
        public OnlineOperationPhase Phase { get; }
        public string Message { get; }
        public bool CanCancel { get; }
        public bool CanRetry { get; }
        public bool IsBusy => Phase == OnlineOperationPhase.Connecting || Phase == OnlineOperationPhase.Creating ||
                              Phase == OnlineOperationPhase.Searching || Phase == OnlineOperationPhase.Entering;

        public OnlineUiState(OnlineOperationPhase phase = OnlineOperationPhase.Idle, string message = "", bool canCancel = false, bool canRetry = false)
        {
            Phase = phase;
            Message = message ?? string.Empty;
            CanCancel = canCancel;
            CanRetry = canRetry;
        }

        public string VisibleMessage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Message)) return Message;
                switch (Phase)
                {
                    case OnlineOperationPhase.Connecting: return "Conectando con el servicio…";
                    case OnlineOperationPhase.Creating: return "Creando la sala…";
                    case OnlineOperationPhase.Searching: return "Buscando la sala…";
                    case OnlineOperationPhase.Entering: return "Sala encontrada. Entrando…";
                    case OnlineOperationPhase.Cancelled: return "Conexión cancelada.";
                    case OnlineOperationPhase.IncompatibleVersion: return "La sala usa otra versión del juego.";
                    case OnlineOperationPhase.RoomClosed: return "La sala ya no está disponible.";
                    case OnlineOperationPhase.RecoverableError: return "No pudimos conectar. Revisá Internet e intentá otra vez.";
                    default: return string.Empty;
                }
            }
        }
    }

    public sealed class LobbyMemberUiState
    {
        public string Id { get; }
        public string Name { get; }
        public bool Ready { get; }
        public bool Connected { get; }
        public string RoleLabel { get; }

        public LobbyMemberUiState(string id, string name, bool ready, bool connected = true, string roleLabel = "SE SORTEA AL EMPEZAR")
        {
            Id = id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? "Jugador" : name.Trim();
            Ready = ready;
            Connected = connected;
            RoleLabel = string.IsNullOrWhiteSpace(roleLabel) ? "SE SORTEA AL EMPEZAR" : roleLabel;
        }
    }

    public sealed class LobbyUiState
    {
        public bool IsOwner { get; }
        public string RoomCode { get; }
        public IReadOnlyList<LobbyMemberUiState> Members { get; }
        public bool LocalReady { get; }
        public bool ReadyPending { get; }
        public int? HumanCount { get; }
        public bool CanStart { get; }
        public string StartBlockReason { get; }
        public string MapId { get; }
        public string MapLabel { get; }
        public bool CanExplore { get; }
        public bool StartPending { get; }
        public bool IsWaiting { get; }
        public bool RulesPending { get; }
        public string ModeId { get; }

        public LobbyUiState(
            bool isOwner,
            string roomCode,
            IEnumerable<LobbyMemberUiState> members,
            bool localReady,
            bool readyPending,
            int? humanCount,
            bool canStart,
            string startBlockReason,
            string mapId = RoomRules.AlfaMap,
            string mapLabel = "CASA CON PATIO",
            bool canExplore = false,
            bool startPending = false,
            bool isWaiting = true,
            bool rulesPending = false, string modeId = GameModes.Blood)
        {
            ModeId = GameModes.IsValid(modeId) ? modeId : throw new ArgumentException("Unknown game mode.");
            IsOwner = isOwner;
            RoomCode = AlfaRoomCode.FormatForDisplay(roomCode);
            Members = Array.AsReadOnly((members ?? Enumerable.Empty<LobbyMemberUiState>()).ToArray());
            LocalReady = localReady;
            ReadyPending = readyPending;
            HumanCount = humanCount;
            CanStart = canStart;
            StartBlockReason = startBlockReason ?? string.Empty;
            MapId = mapId ?? RoomRules.AlfaMap;
            MapLabel = string.IsNullOrWhiteSpace(mapLabel) ? "CASA CON PATIO" : mapLabel;
            CanExplore = canExplore;
            StartPending = startPending;
            IsWaiting = isWaiting;
            RulesPending = rulesPending;
        }

        public static LobbyUiState FromRoomView(RoomView room, string localMemberId, string roomCode, bool canStart,
            string startBlockReason, bool readyPending = false, bool startPending = false, bool rulesPending = false)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var members = room.Members.Select(member => new LobbyMemberUiState(member.Id, member.Name, member.Ready));
            var local = room.Members.FirstOrDefault(member => member.Id == localMemberId);
            return new LobbyUiState(room.OwnerId == localMemberId, roomCode, members, local != null && local.Ready,
                readyPending, room.Rules.HumanCount, canStart, startBlockReason, room.Rules.MapId, startPending: startPending,
                isWaiting: room.Phase == RoomPhase.Waiting, rulesPending: rulesPending, modeId: room.Rules.ModeId);
        }
    }

    public sealed class TrainingMapOption
    {
        public string Id { get; }
        public string DisplayName { get; }
        public bool SupportsTasks { get; }

        public TrainingMapOption(string id, string displayName, bool supportsTasks = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Map ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Map display name is required.", nameof(displayName));
            Id = id;
            DisplayName = displayName; SupportsTasks = supportsTasks;
        }
    }

    public sealed class TrainingUiState
    {
        public string ModeId { get; }
        public AlfaRole SelectedRole { get; }
        public bool IsLoading { get; }
        public string Message { get; }

        public TrainingUiState(AlfaRole selectedRole = AlfaRole.Human, bool isLoading = false, string message = "", string modeId = GameModes.Blood)
        {
            SelectedRole = selectedRole; ModeId = GameModes.IsValid(modeId) ? modeId : throw new ArgumentException("Unknown game mode.");
            IsLoading = isLoading;
            Message = message ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class NamedColorOption
    {
        public string Id;
        public string Label;
        public Color Color;

        public NamedColorOption(string id, string label, Color color)
        {
            Id = id ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            Color = color;
        }
    }

    [Serializable]
    public sealed class BasicCustomizationDraft
    {
        public AlfaRole Role;
        public string SkinColorId;
        public string PajamaColorId;
        public string MosquitoColorId;

        public BasicCustomizationDraft(AlfaRole role, string skinColorId, string pajamaColorId, string mosquitoColorId)
        {
            Role = role;
            SkinColorId = skinColorId ?? string.Empty;
            PajamaColorId = pajamaColorId ?? string.Empty;
            MosquitoColorId = mosquitoColorId ?? string.Empty;
        }

        public BasicCustomizationDraft Copy() => new BasicCustomizationDraft(Role, SkinColorId, PajamaColorId, MosquitoColorId);

        public bool SameValues(BasicCustomizationDraft other) => other != null && Role == other.Role &&
            SkinColorId == other.SkinColorId && PajamaColorId == other.PajamaColorId && MosquitoColorId == other.MosquitoColorId;
    }

    public sealed class CustomizationUiState
    {
        public IReadOnlyList<NamedColorOption> SkinColors { get; }
        public IReadOnlyList<NamedColorOption> PajamaColors { get; }
        public IReadOnlyList<NamedColorOption> MosquitoColors { get; }
        public BasicCustomizationDraft Saved { get; }
        public BasicCustomizationDraft Draft { get; }
        public bool IsSaving { get; }
        public string Message { get; }

        public CustomizationUiState(
            IEnumerable<NamedColorOption> skinColors,
            IEnumerable<NamedColorOption> pajamaColors,
            IEnumerable<NamedColorOption> mosquitoColors,
            BasicCustomizationDraft saved,
            BasicCustomizationDraft draft = null,
            bool isSaving = false,
            string message = "")
        {
            SkinColors = Array.AsReadOnly((skinColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            PajamaColors = Array.AsReadOnly((pajamaColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            MosquitoColors = Array.AsReadOnly((mosquitoColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            Saved = saved?.Copy() ?? throw new ArgumentNullException(nameof(saved));
            Draft = draft?.Copy() ?? Saved.Copy();
            IsSaving = isSaving;
            Message = message ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class AlfaSettingsDraft
    {
        public float MasterVolume;
        public float MusicVolume;
        public float EffectsVolume;
        public bool FullScreen;
        public int ResolutionIndex;
        public int QualityIndex;
        public bool VSync;
        public int FrameLimit;
        public float HumanSensitivity;
        public float MosquitoSensitivity;
        public bool InvertY;
        public bool ReduceMenuMotion;

        public AlfaSettingsDraft Copy() => (AlfaSettingsDraft)MemberwiseClone();

        public bool SameValues(AlfaSettingsDraft other)
        {
            return other != null && Mathf.Approximately(MasterVolume, other.MasterVolume) &&
                   Mathf.Approximately(MusicVolume, other.MusicVolume) && Mathf.Approximately(EffectsVolume, other.EffectsVolume) &&
                   FullScreen == other.FullScreen && ResolutionIndex == other.ResolutionIndex && QualityIndex == other.QualityIndex &&
                   VSync == other.VSync && FrameLimit == other.FrameLimit &&
                   Mathf.Approximately(HumanSensitivity, other.HumanSensitivity) &&
                   Mathf.Approximately(MosquitoSensitivity, other.MosquitoSensitivity) && InvertY == other.InvertY &&
                   ReduceMenuMotion == other.ReduceMenuMotion;
        }
    }

    public sealed class SettingsUiState
    {
        public AlfaSettingsDraft Saved { get; }
        public AlfaSettingsDraft Draft { get; }
        public IReadOnlyList<string> Resolutions { get; }
        public IReadOnlyList<string> Qualities { get; }
        public bool SupportsVideo { get; }
        public bool SupportsRebinding { get; }
        public bool SupportsReducedMenuMotion { get; }
        public bool IsApplying { get; }
        public string Message { get; }

        public SettingsUiState(AlfaSettingsDraft saved, AlfaSettingsDraft draft, IEnumerable<string> resolutions,
            IEnumerable<string> qualities, bool supportsVideo, bool supportsRebinding, bool isApplying = false, string message = "",
            bool supportsReducedMenuMotion = false)
        {
            Saved = saved?.Copy() ?? throw new ArgumentNullException(nameof(saved));
            Draft = draft?.Copy() ?? Saved.Copy();
            Resolutions = Array.AsReadOnly((resolutions ?? Enumerable.Empty<string>()).ToArray());
            Qualities = Array.AsReadOnly((qualities ?? Enumerable.Empty<string>()).ToArray());
            SupportsVideo = supportsVideo;
            SupportsRebinding = supportsRebinding;
            SupportsReducedMenuMotion = supportsReducedMenuMotion;
            IsApplying = isApplying;
            Message = message ?? string.Empty;
        }
    }

    public sealed class BloodHudUiState
    {
        public string ModeId { get; }
        public int TasksCompleted { get; }
        public int TasksGoal { get; }
        public int MosquitoesAlive { get; }
        public int LivesRemaining { get; }
        public string PrivateTaskText { get; }
        public float TaskProgress01 { get; }
        public bool IsSpectator => ActorState == HudActorState.Spectating;
        public AlfaRole Role { get; }
        public float SecondsRemaining { get; }
        public float BloodCurrent { get; }
        public float BloodTarget { get; }
        public string Interaction { get; }
        public string ContextHint { get; }
        public HudActorState ActorState { get; }
        public float StateProgress01 { get; }
        public string NetworkMessage { get; }

        public BloodHudUiState(AlfaRole role, float secondsRemaining, float bloodCurrent, float bloodTarget,
            string interaction = "", string contextHint = "", HudActorState actorState = HudActorState.Normal,
            float stateProgress01 = 0f, string networkMessage = "", string modeId = GameModes.Blood, int tasksCompleted = 0, int tasksGoal = 0, int mosquitoesAlive = 0, int livesRemaining = 0, string privateTaskText = "", float taskProgress01 = 0)
        {
            Role = role; ModeId = GameModes.IsValid(modeId) ? modeId : throw new ArgumentException("Unknown game mode.");
            TasksCompleted = Math.Max(0, tasksCompleted); TasksGoal = Math.Max(0, tasksGoal); MosquitoesAlive = Math.Max(0, mosquitoesAlive); LivesRemaining = Math.Max(0, livesRemaining);
            PrivateTaskText = role == AlfaRole.Human && modeId == GameModes.Tasks ? privateTaskText ?? string.Empty : string.Empty;
            TaskProgress01 = Mathf.Clamp01(taskProgress01);
            SecondsRemaining = Mathf.Max(0f, secondsRemaining);
            BloodCurrent = Mathf.Max(0f, bloodCurrent);
            BloodTarget = Mathf.Max(0.01f, bloodTarget);
            Interaction = interaction ?? string.Empty;
            ContextHint = contextHint ?? string.Empty;
            ActorState = actorState;
            StateProgress01 = Mathf.Clamp01(stateProgress01);
            NetworkMessage = networkMessage ?? string.Empty;
        }
    }

    public sealed class ResultsUiState
    {
        public string ModeId { get; }
        public string MapId { get; }
        public int TasksCompleted { get; }
        public int TasksGoal { get; }
        public int MosquitoesAlive { get; }
        public MatchOutcome Outcome { get; }
        public bool IsTraining { get; }
        public bool IsOwner { get; }
        public float BloodCurrent { get; }
        public float BloodTarget { get; }
        public float ElapsedSeconds { get; }
        public string Reason { get; }
        public AlfaRole TrainingRole { get; }

        public ResultsUiState(MatchOutcome outcome, bool isTraining, bool isOwner, float bloodCurrent,
            float bloodTarget, float elapsedSeconds, string reason = "", AlfaRole trainingRole = AlfaRole.Human, string modeId = GameModes.Blood, string mapId = RoomRules.AlfaMap, int tasksCompleted = 0, int tasksGoal = 0, int mosquitoesAlive = 0)
        {
            ModeId = GameModes.IsValid(modeId) ? modeId : throw new ArgumentException("Unknown game mode."); MapId = mapId; TasksCompleted = tasksCompleted; TasksGoal = tasksGoal; MosquitoesAlive = mosquitoesAlive;
            Outcome = outcome;
            IsTraining = isTraining;
            IsOwner = isOwner;
            BloodCurrent = bloodCurrent;
            BloodTarget = bloodTarget;
            ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            Reason = reason ?? string.Empty;
            TrainingRole = trainingRole;
        }
    }

    public static class AlfaRoomCode
    {
        public const int CharacterCount = 10;

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Where(c => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray();
            return new string(chars);
        }

        public static bool IsComplete(string value) => Normalize(value).Length == CharacterCount;

        public static string FormatForDisplay(string value)
        {
            var normalized = Normalize(value);
            return normalized.Length <= 5 ? normalized : normalized.Substring(0, 5) + "-" + normalized.Substring(5);
        }
    }
}
