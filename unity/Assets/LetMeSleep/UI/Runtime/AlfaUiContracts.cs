using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
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
    public enum CustomizationUiMode { Basic, Modular }
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

    // Deliberately separate from IMenuActions while the basic three-colour flow remains live.
    // Bootstrap opts in only after persistence and the visual feature gate are ready.
    public interface IModularCustomizationActions
    {
        void PreviewModularCustomization(AppearanceSelection draft, AlfaRole editedRole);
        void SaveModularCustomization(AppearanceSelection draft, AlfaRole editedRole);
    }

    public interface IRoomMapActions
    {
        void SetRoomMap(string mapId);
    }

    public interface ISpectatorActions { void SpectateNext(); }

    /// <summary>
    /// Optional, implemented next to <see cref="IMenuActions"/>: the waiting-room character of a member, so the
    /// UI can float that player's name over it (UI-06 screen 3). Return false when the member has no avatar.
    /// </summary>
    public interface ILobbyPresenceSource
    {
        bool TryGetLobbyAvatar(string memberId, out Transform avatar, out Camera camera);
    }

    public interface IRoomModeActions
    {
        void SetRoomMode(string modeId);
        void SetRoomDurationSeconds(int seconds);
    }

    public interface IVoiceActions
    {
        void SetLocalVoiceMuted(bool muted);
        void SetPeerVoiceMuted(string memberId, bool muted);
        void BeginPushToTalkRebind(Action<string, string> completed);
    }

    public sealed class VoiceParticipantUiState
    {
        public string MemberId { get; }
        public string DisplayName { get; }
        public bool Audible { get; }
        public bool Muted { get; }
        public bool Speaking { get; }

        public VoiceParticipantUiState(string memberId, string displayName, bool audible, bool muted, bool speaking)
        {
            MemberId = memberId ?? string.Empty;
            DisplayName = displayName ?? "Jugador";
            Audible = audible;
            Muted = muted;
            Speaking = speaking;
        }
    }

    public sealed class VoiceUiState
    {
        public bool InRoom { get; }
        public bool LocalMuted { get; }
        public bool Transmitting { get; }
        public bool DeviceAvailable { get; }
        public string ScopeLabel { get; }
        public string BindingLabel { get; }
        public string Notice { get; }
        public IReadOnlyList<VoiceParticipantUiState> Participants { get; }

        public VoiceUiState(bool inRoom, bool localMuted, bool transmitting, bool deviceAvailable,
            string scopeLabel, string bindingLabel, string notice, IEnumerable<VoiceParticipantUiState> participants)
        {
            InRoom = inRoom;
            LocalMuted = localMuted;
            Transmitting = transmitting;
            DeviceAvailable = deviceAvailable;
            ScopeLabel = scopeLabel ?? string.Empty;
            BindingLabel = bindingLabel ?? "V";
            Notice = notice ?? string.Empty;
            Participants = Array.AsReadOnly((participants ?? Enumerable.Empty<VoiceParticipantUiState>()).ToArray());
        }
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
        public int RoundSeconds { get; }
        /// <summary>Seconds until the round starts on its own, when the room runs a start countdown; null otherwise.</summary>
        public int? StartCountdownSeconds { get; }

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
            bool rulesPending = false, string modeId = GameModes.Blood, int roundSeconds = 180, int? startCountdownSeconds = null)
        {
            if (startCountdownSeconds.HasValue && startCountdownSeconds.Value < 0) throw new ArgumentOutOfRangeException(nameof(startCountdownSeconds));
            StartCountdownSeconds = startCountdownSeconds;
            ModeId = GameModes.IsValid(modeId) ? modeId : throw new ArgumentException("Unknown game mode.");
            if (roundSeconds < 30 || roundSeconds > 1800) throw new ArgumentOutOfRangeException(nameof(roundSeconds));
            RoundSeconds = roundSeconds;
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
            var members = room.Members.Select(member => new LobbyMemberUiState(member.Id, member.Name, member.Ready, member.Connected));
            var local = room.Members.FirstOrDefault(member => member.Id == localMemberId);
            return new LobbyUiState(room.OwnerId == localMemberId, roomCode, members, local != null && local.Ready,
                readyPending, room.Rules.HumanCount, canStart, startBlockReason, room.Rules.MapId, startPending: startPending,
                isWaiting: room.Phase == RoomPhase.Waiting, rulesPending: rulesPending, modeId: room.Rules.ModeId,
                roundSeconds: room.Rules.RoundSeconds);
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
        public CustomizationUiMode Mode { get; }
        public IReadOnlyList<NamedColorOption> SkinColors { get; }
        public IReadOnlyList<NamedColorOption> PajamaColors { get; }
        public IReadOnlyList<NamedColorOption> MosquitoColors { get; }
        public BasicCustomizationDraft Saved { get; }
        public BasicCustomizationDraft Draft { get; }
        public CustomizationCatalogSnapshot Catalog { get; }
        public AppearanceSelection PublishedSelection { get; }
        public AppearanceSelection DraftSelection { get; }
        // This is a screen tab only. It is not part of AppearanceSelection or a network payload.
        public AlfaRole EditedRole { get; }
        // A caller must explicitly certify the visual applicator before promising a changed 3D preview.
        public bool ModularPreviewAvailable { get; }
        // UI-only lookup. A null result means that the option is rendered by its visible name alone.
        public Func<string, string, Sprite> ThumbnailResolver { get; }
        public bool IsSaving { get; }
        // A retained profile can be visible but cannot safely be edited by this build.
        public bool IsReadOnly { get; }
        public string Message { get; }

        public CustomizationUiState(
            IEnumerable<NamedColorOption> skinColors,
            IEnumerable<NamedColorOption> pajamaColors,
            IEnumerable<NamedColorOption> mosquitoColors,
            BasicCustomizationDraft saved,
            BasicCustomizationDraft draft = null,
            bool isSaving = false,
            string message = "",
            bool isReadOnly = false)
        {
            Mode = CustomizationUiMode.Basic;
            SkinColors = Array.AsReadOnly((skinColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            PajamaColors = Array.AsReadOnly((pajamaColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            MosquitoColors = Array.AsReadOnly((mosquitoColors ?? Enumerable.Empty<NamedColorOption>()).ToArray());
            Saved = saved?.Copy() ?? throw new ArgumentNullException(nameof(saved));
            Draft = draft?.Copy() ?? Saved.Copy();
            IsSaving = isSaving;
            IsReadOnly = isReadOnly;
            Message = message ?? string.Empty;
        }

        public CustomizationUiState(
            CustomizationCatalogSnapshot catalog,
            AppearanceSelection publishedSelection,
            AppearanceSelection draftSelection,
            AlfaRole editedRole,
            bool isSaving = false,
            string message = "",
            bool modularPreviewAvailable = false,
            Func<string, string, Sprite> thumbnailResolver = null,
            bool isReadOnly = false)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (editedRole != AlfaRole.Human && editedRole != AlfaRole.Mosquito)
                throw new ArgumentOutOfRangeException(nameof(editedRole));
            if (!catalog.TryNormalize(publishedSelection, out var normalizedPublished, out var publishedError))
                throw new ArgumentException("Published modular selection is invalid: " + publishedError, nameof(publishedSelection));
            if (!catalog.TryNormalize(draftSelection ?? publishedSelection, out var normalizedDraft, out var draftError))
                throw new ArgumentException("Draft modular selection is invalid: " + draftError, nameof(draftSelection));

            Mode = CustomizationUiMode.Modular;
            Catalog = catalog;
            PublishedSelection = normalizedPublished;
            DraftSelection = normalizedDraft;
            EditedRole = editedRole;
            ModularPreviewAvailable = modularPreviewAvailable;
            ThumbnailResolver = thumbnailResolver;
            IsSaving = isSaving;
            IsReadOnly = isReadOnly;
            Message = message ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class AlfaSettingsDraft
    {
        public float MasterVolume;
        public float MusicVolume;
        public float EffectsVolume;
        public float VoiceVolume;
        public string VoiceDevice;
        public string PushToTalkBinding;
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
                   Mathf.Approximately(VoiceVolume, other.VoiceVolume) && string.Equals(VoiceDevice, other.VoiceDevice, StringComparison.Ordinal) &&
                   string.Equals(PushToTalkBinding, other.PushToTalkBinding, StringComparison.Ordinal) &&
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
        public IReadOnlyList<string> VoiceDevices { get; }
        public bool SupportsVideo { get; }
        public bool SupportsRebinding { get; }
        public bool SupportsReducedMenuMotion { get; }
        public bool IsApplying { get; }
        public string Message { get; }

        public SettingsUiState(AlfaSettingsDraft saved, AlfaSettingsDraft draft, IEnumerable<string> resolutions,
            IEnumerable<string> qualities, bool supportsVideo, bool supportsRebinding, bool isApplying = false, string message = "",
            bool supportsReducedMenuMotion = false, IEnumerable<string> voiceDevices = null)
        {
            Saved = saved?.Copy() ?? throw new ArgumentNullException(nameof(saved));
            Draft = draft?.Copy() ?? Saved.Copy();
            Resolutions = Array.AsReadOnly((resolutions ?? Enumerable.Empty<string>()).ToArray());
            Qualities = Array.AsReadOnly((qualities ?? Enumerable.Empty<string>()).ToArray());
            VoiceDevices = Array.AsReadOnly((voiceDevices ?? Enumerable.Empty<string>()).ToArray());
            SupportsVideo = supportsVideo;
            SupportsRebinding = supportsRebinding;
            SupportsReducedMenuMotion = supportsReducedMenuMotion;
            IsApplying = isApplying;
            Message = message ?? string.Empty;
        }
    }

    public sealed class EquipmentSlotUiState
    {
        public string Label { get; }
        public string ResourceText { get; }
        public AlfaUiIconKind Icon { get; }

        public EquipmentSlotUiState(string label, string resourceText, AlfaUiIconKind icon)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "VACÍO" : label.Trim();
            ResourceText = resourceText?.Trim() ?? string.Empty;
            Icon = icon;
        }
    }

    public sealed class EquipmentHudUiState
    {
        public IReadOnlyList<EquipmentSlotUiState> Slots { get; }
        public int SelectedSlot { get; }
        public float Stamina01 { get; }
        public float ThrowCharge01 { get; }
        public bool ThrowAwaitingRelease { get; }
        public string SwapOfferText { get; }

        public EquipmentHudUiState(IEnumerable<EquipmentSlotUiState> slots, int selectedSlot, float stamina01,
            float throwCharge01 = 0f, bool throwAwaitingRelease = false, string swapOfferText = "")
        {
            var copy = (slots ?? Enumerable.Empty<EquipmentSlotUiState>()).ToArray();
            if (copy.Length != 3 || copy.Any(slot => slot == null)) throw new ArgumentException("Equipment HUD requires exactly three slots.", nameof(slots));
            if (selectedSlot < -1 || selectedSlot > 2) throw new ArgumentOutOfRangeException(nameof(selectedSlot));
            Slots = Array.AsReadOnly(copy);
            SelectedSlot = selectedSlot;
            Stamina01 = Mathf.Clamp01(stamina01);
            ThrowCharge01 = Mathf.Clamp01(throwCharge01);
            ThrowAwaitingRelease = throwAwaitingRelease;
            SwapOfferText = swapOfferText ?? string.Empty;
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
        public EquipmentHudUiState Equipment { get; }
        /// <summary>Mosquitoes in the round (alive or not); -1 when the caller does not know it.</summary>
        public int MosquitoesTotal { get; }
        /// <summary>Humans still able to act (not eliminated nor fainted); -1 when unknown.</summary>
        public int HumansActive { get; }
        /// <summary>Humans in the round; -1 when unknown.</summary>
        public int HumansTotal { get; }

        public BloodHudUiState(AlfaRole role, float secondsRemaining, float bloodCurrent, float bloodTarget,
            string interaction = "", string contextHint = "", HudActorState actorState = HudActorState.Normal,
            float stateProgress01 = 0f, string networkMessage = "", string modeId = GameModes.Blood, int tasksCompleted = 0, int tasksGoal = 0, int mosquitoesAlive = 0, int livesRemaining = 0, string privateTaskText = "", float taskProgress01 = 0,
            EquipmentHudUiState equipment = null, int mosquitoesTotal = -1, int humansActive = -1, int humansTotal = -1)
        {
            MosquitoesTotal = mosquitoesTotal < 0 ? -1 : mosquitoesTotal;
            HumansTotal = humansTotal < 0 ? -1 : humansTotal;
            HumansActive = humansActive < 0 ? -1 : humansActive;
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
            Equipment = role == AlfaRole.Human ? equipment : null;
        }
    }

    /// <summary>
    /// How one player of the round looks, for the results figures (UI-06 9: each winner with their own colours).
    /// Human: skin and pajama trousers; mosquito: body colour. Colours the caller does not know stay null.
    /// </summary>
    public sealed class ResultsFigureUiState
    {
        public AlfaRole Role { get; }
        public Color? SkinColor { get; }
        public Color? PajamaColor { get; }
        public Color? MosquitoColor { get; }
        /// <summary>True for the local player (drawn in the middle of the winning group).</summary>
        public bool IsLocal { get; }

        public ResultsFigureUiState(AlfaRole role, Color? skinColor = null, Color? pajamaColor = null, Color? mosquitoColor = null, bool isLocal = false)
        {
            Role = role;
            SkinColor = skinColor;
            PajamaColor = pajamaColor;
            MosquitoColor = mosquitoColor;
            IsLocal = isLocal;
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
        /// <summary>Players on each team this round (the results scoreboard); -1 when unknown.</summary>
        public int HumansCount { get; }
        public int MosquitoesCount { get; }
        /// <summary>The look of the round's players (any order; the UI draws up to three per team); may be empty.</summary>
        public IReadOnlyList<ResultsFigureUiState> Figures { get; }

        public ResultsUiState(MatchOutcome outcome, bool isTraining, bool isOwner, float bloodCurrent,
            float bloodTarget, float elapsedSeconds, string reason = "", AlfaRole trainingRole = AlfaRole.Human, string modeId = GameModes.Blood, string mapId = RoomRules.AlfaMap, int tasksCompleted = 0, int tasksGoal = 0, int mosquitoesAlive = 0,
            int humansCount = -1, int mosquitoesCount = -1, IEnumerable<ResultsFigureUiState> figures = null)
        {
            Figures = Array.AsReadOnly((figures ?? Enumerable.Empty<ResultsFigureUiState>()).Where(item => item != null).ToArray());
            HumansCount = humansCount < 0 ? -1 : humansCount;
            MosquitoesCount = mosquitoesCount < 0 ? -1 : mosquitoesCount;
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

        /// <summary>
        /// Formats a code that is being edited and maps the caret to the same logical place: after the same
        /// number of code characters, past the separator once the first group is complete. Typing or pasting
        /// "ABCDE-FGHIJ" one character at a time therefore yields "ABCDE-FGHIJ", never "ABCDE-GHIJF".
        /// </summary>
        public static string FormatForEditing(string raw, int caret, out int formattedCaret)
        {
            raw = raw ?? string.Empty;
            caret = Math.Max(0, Math.Min(caret, raw.Length));
            var codeCharactersBeforeCaret = 0;
            for (var i = 0; i < caret; i++)
                if (IsCodeCharacter(raw[i])) codeCharactersBeforeCaret++;
            var formatted = FormatForDisplay(raw);
            formattedCaret = codeCharactersBeforeCaret + (codeCharactersBeforeCaret > 5 ? 1 : 0);
            formattedCaret = Math.Min(formattedCaret, formatted.Length);
            return formatted;
        }

        private static bool IsCodeCharacter(char c) => c != '-' && !char.IsWhiteSpace(c);
    }
}
