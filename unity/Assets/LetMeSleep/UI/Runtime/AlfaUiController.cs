using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LetMeSleep.UI
{
    [DisallowMultipleComponent]
    public sealed class AlfaUiController : MonoBehaviour
    {
        public const string BloodModeId = "blood";
        public const string HousePatioMapId = "house-patio-v1";
        private static readonly int[] FrameLimitOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };
        private static readonly int[] RoundDurationOptions = { 30, 60, 90, 120, 150, 180, 240, 300, 420, 600, 900, 1200, 1800 };

        private readonly Dictionary<AlfaUiScreen, GameObject> screens = new Dictionary<AlfaUiScreen, GameObject>();
        private readonly Dictionary<int, TextMeshProUGUI> humanCountLabels = new Dictionary<int, TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> memberRows = new List<TextMeshProUGUI>();
        private readonly List<GameObject> memberRowRoots = new List<GameObject>();
        private readonly List<AlfaUiIcon> memberStatusIcons = new List<AlfaUiIcon>();
        private readonly List<TextMeshProUGUI> memberSubtitles = new List<TextMeshProUGUI>();
        private readonly List<UnityEngine.UI.Image> memberStatusBars = new List<UnityEngine.UI.Image>();
        private IMenuActions actions;
        private AlfaUiFactory factory;
        private AlfaUiScreen screen;
        private AlfaUiScreen settingsReturnScreen;
        private OnlineUiState onlineState = new OnlineUiState();
        private LobbyUiState lobbyState;
        private TrainingUiState trainingState = new TrainingUiState();
        private TrainingMapOption[] trainingMaps = { new TrainingMapOption(HousePatioMapId, "CASA CON PATIO") };
        private string selectedTrainingMapId = HousePatioMapId;
        private const string NoTrainingMaps = "No hay mapas de entrenamiento disponibles.";
        private TextMeshProUGUI trainingMapLabel, trainingModeLabel, roomModeLabel, roomDurationLabel;
        private UnityEngine.UI.Button trainingModePrevious, trainingModeNext, roomModePrevious, roomModeNext,
            roomDurationPrevious, roomDurationNext;
        private TextMeshProUGUI hudTask, hudLives, hudStaminaLabel, hudThrowLabel, hudSwapOffer;
        private readonly TextMeshProUGUI[] hudEquipmentLabels = new TextMeshProUGUI[4];
        private readonly AlfaUiIcon[] hudEquipmentIcons = new AlfaUiIcon[4];
        private GameObject hudTaskPanel, hudReticle, hudEquipmentPanel, hudThrowTrack;
        private RectTransform hudEquipmentRect, hudStaminaLabelRect, hudStaminaTrackRect, hudThrowRect, hudSwapRect;
        private bool isSpectator;
        private UnityEngine.UI.Image hudTaskFill, hudStaminaFill, hudThrowFill;
        private AlfaUiIcon hudScoreIcon;
        private UnityEngine.UI.Button trainingMapPrevious, trainingMapNext;
        private CustomizationUiState customizationState;
        private BasicCustomizationDraft customizationDraft;
        private AppearanceSelection modularCustomizationDraft;
        private SettingsUiState settingsState;
        private AlfaSettingsDraft settingsDraft;
        private VoiceUiState voiceState = new VoiceUiState(false, false, false, false, string.Empty, "V", string.Empty, null);
        private bool initialized;
        private bool createMode;
        private bool lobbyExploring;
        private bool onlineSubmissionLatched;
        private bool onlineCancelLatched;
        private bool lobbyReadyLatched;
        private bool lobbyStartLatched;
        private bool lobbyRulesLatched;
        private TrainingMapOption[] roomMaps = Array.Empty<TrainingMapOption>();
        private TextMeshProUGUI roomMapLabel;
        private UnityEngine.UI.Button roomMapPrevious;
        private UnityEngine.UI.Button roomMapNext;
        private bool trainingStartLatched;
        private bool trainingCancelLatched;
        private bool customizationSaveLatched;
        private BasicCustomizationDraft customizationSessionBaseline;
        private AppearanceSelection modularCustomizationSessionBaseline;
        private AlfaRole modularEditedRole;
        private string modularSelectedSlotId = string.Empty;
        private AlfaUiScreen customizationReturnScreen = AlfaUiScreen.MainMenu;
        private string customizationLobbyCode = string.Empty;
        private bool settingsApplyLatched;
        private bool resultsActionLatched;
        private string rememberedPlayerName = string.Empty;
        private bool gameplayIsTraining;
        private GameObject feedbackSelection;
        private string onlineErrorFeedbackKey = string.Empty;

        private TMP_InputField playerNameInput;
        private TMP_InputField roomCodeInput;
        private TextMeshProUGUI onlineFormTitle;
        private TextMeshProUGUI onlineStatus;
        private GameObject roomCodeRow;
        private UnityEngine.UI.Button onlinePrimaryButton;
        private TextMeshProUGUI onlinePrimaryLabel;
        private UnityEngine.UI.Button onlineCancelButton;
        private UnityEngine.UI.Button onlineRetryButton;
        private UnityEngine.UI.Button onlineBackButton;
        private UnityEngine.UI.Button onlinePasteButton;
        private CanvasGroup onlineFormGroup;
        private GameObject onlineCodePreviewRow;
        private UnityEngine.UI.Button onlineCreateTab;
        private UnityEngine.UI.Button onlineJoinTab;
        private TextMeshProUGUI onlineInfoTitle;
        private RectTransform[] onlineInfoRows;
        private GameObject onlineOverlay;
        private GameObject onlineConnectingCard;
        private GameObject onlineErrorCard;
        private TextMeshProUGUI onlineConnectingTitle;
        private TextMeshProUGUI onlineConnectingMessage;
        private TextMeshProUGUI onlineErrorTitle;
        private TextMeshProUGUI onlineErrorMessage;
        private UnityEngine.UI.Button onlineErrorBackButton;
        private bool onlineErrorDismissed;

        private TextMeshProUGUI lobbyCode;
        private TextMeshProUGUI lobbyHeaderTitle;
        private TextMeshProUGUI lobbyHeaderNote;
        private TextMeshProUGUI lobbyCount;
        private TextMeshProUGUI lobbyMapMode;
        private TextMeshProUGUI lobbyStatus;
        private TextMeshProUGUI lobbyStartReason;
        private TextMeshProUGUI lobbyRoleBadge;
        private UnityEngine.UI.Button lobbyCopyButton;
        private UnityEngine.UI.Button lobbyReadyButton;
        private TextMeshProUGUI lobbyReadyLabel;
        private UnityEngine.UI.Button lobbyStartButton;
        private TextMeshProUGUI lobbyStartLabel;
        private UnityEngine.UI.Button lobbyExploreButton;
        private UnityEngine.UI.Button lobbyCustomizeButton;

        private UnityEngine.UI.Button trainingHumanButton;
        private UnityEngine.UI.Button trainingMosquitoButton;
        private TextMeshProUGUI trainingHumanLabel;
        private TextMeshProUGUI trainingMosquitoLabel;
        private AlfaRole trainingPendingRole = AlfaRole.Human;
        private UnityEngine.UI.Button trainingBackButton;
        private TextMeshProUGUI trainingBackLabel;
        private TextMeshProUGUI trainingStatus;

        private CharacterPreviewOrbit previewOrbit;
        private TextMeshProUGUI customizationPreviewTitle;
        private AlfaUiIcon customizationPreviewIcon;
        private TextMeshProUGUI customizationCategoryTitle;
        private TextMeshProUGUI customizationStatus;
        private RectTransform humanPaletteRoot;
        private RectTransform pajamaPaletteRoot;
        private RectTransform mosquitoPaletteRoot;
        private GameObject humanCustomizationFields;
        private GameObject mosquitoCustomizationFields;
        private GameObject modularCustomizationFields;
        private RectTransform modularCategoryRoot;
        private RectTransform modularOptionRoot;
        private UnityEngine.UI.Button customizationHumanButton;
        private UnityEngine.UI.Button customizationMosquitoButton;
        private CanvasGroup customizationControlsGroup;
        private UnityEngine.UI.Button customizationSaveButton;
        private TextMeshProUGUI customizationSaveLabel;
        private UnityEngine.UI.Button customizationResetButton;

        private UnityEngine.UI.Slider masterVolume;
        private UnityEngine.UI.Slider musicVolume;
        private UnityEngine.UI.Slider effectsVolume;
        private UnityEngine.UI.Slider voiceVolume;
        private TMP_Dropdown voiceDeviceDropdown;
        private TextMeshProUGUI pushToTalkBindingLabel;
        private UnityEngine.UI.Slider humanSensitivity;
        private UnityEngine.UI.Slider mosquitoSensitivity;
        private UnityEngine.UI.Toggle fullScreen;
        private UnityEngine.UI.Toggle vSync;
        private UnityEngine.UI.Toggle invertY;
        private UnityEngine.UI.Toggle reduceMenuMotion;
        private TextMeshProUGUI resolutionValue;
        private TextMeshProUGUI qualityValue;
        private TMP_Dropdown frameLimitDropdown;
        private TextMeshProUGUI settingsStatus;
        private GameObject videoSettings;
        private GameObject rebindNote;
        private UnityEngine.UI.Button settingsApplyButton;
        private TextMeshProUGUI settingsApplyLabel;
        private CanvasGroup settingsControlsGroup;

        private TextMeshProUGUI hudClock;
        private TextMeshProUGUI hudBlood;
        private TextMeshProUGUI hudInteraction;
        private TextMeshProUGUI hudHint;
        private TextMeshProUGUI hudActorState;
        private TextMeshProUGUI hudNetwork;
        private TextMeshProUGUI hudVoice;
        private TextMeshProUGUI hudRoleLabel;
        private AlfaUiIcon hudRoleIcon;
        private UnityEngine.UI.Image hudRoleBackground;
        private UnityEngine.UI.Image hudBloodFill;
        private GameObject hudPromptPanel;
        private GameObject hudHintPanel;
        private GameObject hudStatePanel;
        private UnityEngine.UI.Image hudProgress;

        private UnityEngine.UI.Button pauseLeaveButton;
        private TextMeshProUGUI pauseLeaveLabel;
        private UnityEngine.UI.Button pauseVoiceMuteButton;
        private TextMeshProUGUI pauseVoiceMuteLabel;
        private TextMeshProUGUI pauseVoiceStatus;
        private RectTransform pauseVoicePeers;
        private string pauseVoiceKey = string.Empty;

        private TextMeshProUGUI resultsTitle;
        private TextMeshProUGUI resultsStats;
        private TextMeshProUGUI resultsPrimaryLabel;
        private UnityEngine.UI.Button resultsPrimary;
        private UnityEngine.UI.Button resultsLeave;
        private ResultsUiState resultsState;

        private GameObject confirmModal;
        private TextMeshProUGUI confirmTitle;
        private TextMeshProUGUI confirmBody;
        private UnityEngine.UI.Button confirmSafe;
        private TextMeshProUGUI confirmSafeLabel;
        private UnityEngine.UI.Button confirmDanger;
        private TextMeshProUGUI confirmDangerLabel;
        private Action confirmDangerAction;

        public AlfaUiScreen CurrentScreen => screen;
        public bool IsModalOpen => confirmModal != null && confirmModal.activeSelf;
        public event Action<UiFeedbackKind> FeedbackRequested;
        /// <summary>Published after screen/preview activation. Modals retain their parent screen.</summary>
        public event Action<AlfaUiScreen> ScreenChanged;

        public void Initialize(IMenuActions menuActions, AlfaUiDependencies dependencies)
        {
            if (initialized) throw new InvalidOperationException("Alfa UI is already initialized.");
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            AlfaUiMotionPreferences.ReducedMotion = false;
            factory = new AlfaUiFactory(dependencies, RequestFeedback);
            BuildViews(dependencies ?? new AlfaUiDependencies());
            initialized = true;
            ShowMainMenu();
        }

        private void Update()
        {
            if (!initialized) return;
            var escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            var gamepadBack = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (escape || gamepadBack) HandleEscape();
            if (screen == AlfaUiScreen.Gameplay && isSpectator && !IsModalOpen && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && actions is ISpectatorActions spectatorActions) spectatorActions.SpectateNext();
            UpdateSelectionFeedback();
        }

        public void ShowMainMenu()
        {
            if (screen == AlfaUiScreen.Customization)
            {
                customizationSessionBaseline = null;
                modularCustomizationSessionBaseline = null;
                modularSelectedSlotId = string.Empty;
                customizationReturnScreen = AlfaUiScreen.MainMenu;
                customizationLobbyCode = string.Empty;
            }
            lobbyExploring = false;
            gameplayIsTraining = false;
            SetScreen(AlfaUiScreen.MainMenu, "MainPlayButton");
        }

        // UI-06 merges the old create/join choice into one "JUGAR ONLINE" panel with two tabs.
        public void ShowOnlineChoice() => ShowCreateRoom();

        public void SetRememberedPlayerName(string playerName)
        {
            rememberedPlayerName = NormalizePlayerName(playerName);
        }

        public void ShowCreateRoom(string rememberedName = null) => ShowOnlineForm(true, rememberedName, "PlayerNameInput");

        public void ShowJoinRoom(string rememberedName = null) => ShowOnlineForm(false, rememberedName, "PlayerNameInput");

        private void ShowOnlineForm(bool create, string rememberedName, string focusName)
        {
            createMode = create;
            ConfigureOnlineForm(rememberedName == null ? rememberedPlayerName : NormalizePlayerName(rememberedName));
            SetScreen(create ? AlfaUiScreen.CreateRoom : AlfaUiScreen.JoinRoom, focusName);
        }

        private void SwitchOnlineTab(bool create)
        {
            if (OnlineBusy) return;
            var typedName = playerNameInput != null ? playerNameInput.text : null;
            var typedCode = roomCodeInput != null ? roomCodeInput.text : string.Empty;
            ShowOnlineForm(create, typedName ?? string.Empty, create ? "OnlineCreateTab" : "OnlineJoinTab");
            if (!create) roomCodeInput.SetTextWithoutNotify(typedCode);
        }

        public void PresentOnline(OnlineUiState state)
        {
            onlineState = state ?? new OnlineUiState();
            var isError = IsOnlineError(onlineState.Phase);
            var errorKey = isError ? $"{onlineState.Phase}:{onlineState.VisibleMessage}" : string.Empty;
            if (errorKey.Length > 0 && errorKey != onlineErrorFeedbackKey) RequestFeedback(UiFeedbackKind.Error);
            onlineErrorFeedbackKey = errorKey;
            onlineErrorDismissed = false;
            onlineSubmissionLatched = onlineState.IsBusy;
            if (!onlineState.IsBusy) onlineCancelLatched = false;
            var busy = onlineState.IsBusy;
            playerNameInput.interactable = !busy;
            roomCodeInput.interactable = !busy;
            onlinePrimaryButton.interactable = !busy;
            onlinePasteButton.interactable = !busy;
            onlineBackButton.interactable = !busy;
            onlineCreateTab.interactable = onlineJoinTab.interactable = !busy;
            onlineCancelButton.interactable = (onlineState.CanCancel || busy) && !onlineCancelLatched;
            onlineRetryButton.gameObject.SetActive(onlineState.CanRetry);
            onlineStatus.text = onlineState.VisibleMessage;
            onlineStatus.color = isError ? AlfaUiTheme.StatusWarn : AlfaUiTheme.Moon200;
            onlineConnectingTitle.text = ConnectingTitle(onlineState.Phase);
            onlineConnectingMessage.text = onlineState.VisibleMessage;
            onlineErrorTitle.text = ErrorTitle(onlineState.Phase);
            onlineErrorMessage.text = onlineState.VisibleMessage;
            UpdateOnlineOverlay();
            var onlineVisible = screen == AlfaUiScreen.CreateRoom || screen == AlfaUiScreen.JoinRoom;
            if (!onlineVisible) return;
            if (busy) Focus(onlineCancelButton.gameObject);
            else if (isError) Focus(onlineState.CanRetry ? onlineRetryButton.gameObject : onlineErrorBackButton.gameObject);
            else if (onlineState.Phase == OnlineOperationPhase.Cancelled) Focus(onlinePrimaryButton.gameObject);
        }

        private void UpdateOnlineOverlay()
        {
            if (onlineOverlay == null) return;
            var busy = OnlineBusy;
            var showError = !busy && IsOnlineError(onlineState.Phase) && !onlineErrorDismissed;
            var blocking = busy || showError;
            onlineOverlay.SetActive(blocking);
            onlineConnectingCard.SetActive(busy);
            onlineCancelButton.gameObject.SetActive(busy);
            onlineErrorCard.SetActive(showError);
            onlineFormGroup.interactable = !blocking;
            onlineFormGroup.blocksRaycasts = !blocking;
        }

        private void DismissOnlineError()
        {
            if (OnlineBusy) return;
            onlineErrorDismissed = true;
            UpdateOnlineOverlay();
            Focus(createMode ? playerNameInput.gameObject : roomCodeInput.gameObject);
        }

        private static string ConnectingTitle(OnlineOperationPhase phase)
        {
            switch (phase)
            {
                case OnlineOperationPhase.Creating: return "CREANDO SALA…";
                case OnlineOperationPhase.Searching: return "BUSCANDO SALA…";
                case OnlineOperationPhase.Entering: return "ENTRANDO…";
                default: return "CONECTANDO…";
            }
        }

        private static string ErrorTitle(OnlineOperationPhase phase)
        {
            switch (phase)
            {
                case OnlineOperationPhase.IncompatibleVersion: return "VERSIÓN DIFERENTE";
                case OnlineOperationPhase.RoomClosed: return "LA SALA SE CERRÓ";
                default: return "NO SE PUDO CONECTAR";
            }
        }

        // Each presentation is an authoritative snapshot, including all pending flags.
        // Actions must publish another snapshot on completion or rejection to release intent latches.
        public void PresentLobby(LobbyUiState state)
        {
            lobbyState = state ?? throw new ArgumentNullException(nameof(state));
            gameplayIsTraining = false;
            lobbyReadyLatched = state.ReadyPending;
            lobbyStartLatched = state.StartPending;
            lobbyRulesLatched = state.RulesPending;
            lobbyCode.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "PREPARANDO…" : state.RoomCode;
            lobbyCopyButton.interactable = !string.IsNullOrWhiteSpace(state.RoomCode);
            lobbyStatus.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "Preparando el código…" : "Compartí el código para invitar a tus amigos.";
            lobbyRoleBadge.text = state.IsOwner ? "ANFITRIÓN" : "INVITADO";
            lobbyRoleBadge.color = state.IsOwner ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Moon200;
            lobbyHeaderTitle.text = state.IsWaiting ? "ESPERANDO JUGADORES" : "RONDA EN CURSO";
            lobbyHeaderNote.text = !state.IsWaiting ? "ENTRÁS EN LA PRÓXIMA RONDA" : state.IsOwner
                ? "INICIÁ LA RONDA CUANDO TODOS ESTÉN LISTOS" : "EL ANFITRIÓN INICIA LA RONDA CUANDO TODOS ESTÁN LISTOS";
            lobbyCount.text = state.Members.Count + "/" + LetMeSleep.Core.RoomRules.Capacity;
            lobbyReadyButton.interactable = !lobbyReadyLatched && !lobbyStartLatched;
            lobbyReadyLabel.text = lobbyReadyLatched ? "GUARDANDO…" : state.LocalReady ? "CANCELAR LISTO" : "LISTO";
            lobbyStartButton.gameObject.SetActive(state.IsOwner);
            lobbyStartButton.interactable = state.IsOwner && state.CanStart && !lobbyReadyLatched && !lobbyStartLatched;
            lobbyStartLabel.text = lobbyStartLatched ? "INICIANDO…" : "INICIAR RONDA";
            lobbyStartReason.text = state.IsOwner && !state.CanStart ? state.StartBlockReason : string.Empty;
            lobbyReadyButton.gameObject.SetActive(state.IsWaiting);
            lobbyExploreButton.gameObject.SetActive(state.CanExplore);
            lobbyExploreButton.interactable = !lobbyStartLatched;
            lobbyCustomizeButton.gameObject.SetActive(state.IsWaiting);
            lobbyCustomizeButton.interactable = state.IsWaiting && !lobbyStartLatched;

            for (var i = 0; i < memberRows.Count; i++)
            {
                var visible = i < state.Members.Count;
                memberRowRoots[i].SetActive(visible);
                if (!visible) continue;
                var member = state.Members[i];
                var readiness = !member.Connected ? "Sin conexión" : member.Ready ? "Listo" : "No listo";
                memberRows[i].text = member.Name;
                memberRows[i].color = member.Connected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Disabled;
                memberSubtitles[i].text = readiness;
                memberSubtitles[i].color = MemberStatusColor(member, AlfaUiTheme.Moon200);
                memberStatusIcons[i].Kind = MemberStatusIcon(member);
                memberStatusIcons[i].color = MemberStatusColor(member, AlfaUiTheme.Moon200);
                memberStatusBars[i].color = MemberStatusColor(member, AlfaUiTheme.StatusWarn);
            }

            foreach (var entry in humanCountLabels)
            {
                var selected = (entry.Key == 0 && !state.HumanCount.HasValue) || (state.HumanCount.HasValue && entry.Key == state.HumanCount.Value);
                entry.Value.text = entry.Key == 0 ? "AUTO" : entry.Key.ToString();
                var countButton = entry.Value.transform.parent.GetComponent<UnityEngine.UI.Button>();
                // Selection is carried by the filled style and the label, not by colour alone ("AUTO" / "1".."5").
                AlfaUiFactory.ApplyStyle(countButton, selected ? AlfaButtonStyle.Primary : AlfaButtonStyle.Tab);
                countButton.interactable = state.IsOwner && !lobbyStartLatched;
            }

            if (lobbyStartLatched) lobbyStatus.text = "Iniciando ronda…";
            else if (lobbyReadyLatched) lobbyStatus.text = "Guardando estado…";
            else if (lobbyRulesLatched) lobbyStatus.text = "Guardando reglas…";
            else if (!state.IsWaiting) lobbyStatus.text = "La ronda está en curso. Entrás en la próxima.";
            UpdateRoomMapView();
            UpdateLobbyControls();
            UpdateLobbyVoiceMarkers();

            bool editingThisLobby = state.IsWaiting && screen == AlfaUiScreen.Customization &&
                customizationReturnScreen == AlfaUiScreen.Lobby && string.Equals(state.RoomCode, customizationLobbyCode, StringComparison.Ordinal);
            if (screen != AlfaUiScreen.Lobby && !editingThisLobby)
                SetScreen(AlfaUiScreen.Lobby, "LobbyReadyButton");
        }

        private void UpdateLobbyVoiceMarkers()
        {
            if (lobbyState == null || voiceState == null) return;
            for (int i = 0; i < lobbyState.Members.Count && i < memberRows.Count; i++)
            {
                LobbyMemberUiState member = lobbyState.Members[i];
                VoiceParticipantUiState voice = voiceState.Participants.FirstOrDefault(item => item.MemberId == member.Id);
                string readiness = !member.Connected ? "Sin conexión" : member.Ready ? "Listo" : "No listo";
                string voiceLabel = voice == null ? string.Empty : voice.Muted ? "  ·  Silenciado" : voice.Speaking ? "  ·  Hablando" : string.Empty;
                memberSubtitles[i].text = readiness + voiceLabel;
                var speaking = member.Connected && voice != null && voice.Speaking && !voice.Muted;
                memberStatusIcons[i].Kind = speaking ? AlfaUiIconKind.Microphone : MemberStatusIcon(member);
                memberStatusIcons[i].color = speaking ? AlfaUiTheme.Sky400 : MemberStatusColor(member, AlfaUiTheme.Moon200);
            }
        }

        private static AlfaUiIconKind MemberStatusIcon(LobbyMemberUiState member) =>
            !member.Connected ? AlfaUiIconKind.Exit : member.Ready ? AlfaUiIconKind.Ready : AlfaUiIconKind.Human;

        // Status is also spelled out in the row subtitle, so it never depends on colour alone.
        private static Color MemberStatusColor(LobbyMemberUiState member, Color notReady) =>
            !member.Connected ? AlfaUiTheme.Disabled : member.Ready ? AlfaUiTheme.StatusOk : notReady;

        private void RebuildPauseVoicePeers()
        {
            if (pauseVoicePeers == null || voiceState == null) return;
            string key = string.Join("|", voiceState.Participants.Select(item => item.MemberId + ":" + item.Muted + ":" + item.Speaking));
            if (key == pauseVoiceKey) return;
            pauseVoiceKey = key;
            for (int i = pauseVoicePeers.childCount - 1; i >= 0; i--) Destroy(pauseVoicePeers.GetChild(i).gameObject);
            foreach (VoiceParticipantUiState participant in voiceState.Participants)
            {
                string label = participant.Muted ? participant.DisplayName + " · ACTIVAR" : participant.DisplayName + (participant.Speaking ? " · HABLANDO" : " · SILENCIAR");
                string memberId = participant.MemberId; bool nextMuted = !participant.Muted;
                var button = factory.Button(pauseVoicePeers, "VoicePeer-" + memberId.GetHashCode(), label,
                    () => (actions as IVoiceActions)?.SetPeerVoiceMuted(memberId, nextMuted), false, false, 38f, AlfaUiIconKind.Audio);
                button.interactable = actions is IVoiceActions;
            }
        }

        public void SetRoomMaps(IReadOnlyList<TrainingMapOption> maps)
        {
            var copy = maps == null ? Array.Empty<TrainingMapOption>() : maps.ToArray();
            if (copy.Any(map => map == null) || copy.Select(map => map.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Room maps must be non-null with unique IDs.", nameof(maps));
            roomMaps = copy;
            UpdateRoomMapView();
        }

        private bool LobbyBusy => lobbyReadyLatched || lobbyStartLatched || lobbyRulesLatched ||
            (lobbyState != null && (lobbyState.ReadyPending || lobbyState.StartPending || lobbyState.RulesPending));

        private bool CanEditLobbyRules => lobbyState != null && lobbyState.IsOwner && lobbyState.IsWaiting && !LobbyBusy;

        private bool CanCycleRoomMap => CanEditLobbyRules && actions is IRoomMapActions &&
            roomMaps.Any(map => map.Id != lobbyState.MapId);

        private void CycleRoomMap(int delta)
        {
            if (!CanCycleRoomMap || (delta != -1 && delta != 1)) return;
            var index = Array.FindIndex(roomMaps, map => map.Id == lobbyState.MapId);
            var next = index < 0 ? (delta > 0 ? 0 : roomMaps.Length - 1) :
                (index + delta + roomMaps.Length) % roomMaps.Length;
            lobbyRulesLatched = true;
            UpdateRoomMapView();
            UpdateLobbyControls();
            lobbyStatus.text = "Guardando reglas…";
            ((IRoomMapActions)actions).SetRoomMap(roomMaps[next].Id);
        }

        private void UpdateRoomMapView()
        {
            if (roomMapLabel == null) return;
            var id = lobbyState?.MapId ?? HousePatioMapId;
            roomMapLabel.text = id == HousePatioMapId ? "CASA CON PATIO" :
                roomMaps.FirstOrDefault(map => map.Id == id)?.DisplayName ??
                (lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.MapLabel) && lobbyState.MapLabel != "CASA CON PATIO"
                    ? lobbyState.MapLabel : id);
            roomMapPrevious.interactable = roomMapNext.interactable = CanCycleRoomMap;
            if (lobbyMapMode != null)
                lobbyMapMode.text = roomMapLabel.text + "  ·  MODO " + AlfaModeText.Name(lobbyState?.ModeId ?? GameModes.Blood);
            if (roomModeLabel != null)
            {
                roomModeLabel.text = AlfaModeText.Name(lobbyState?.ModeId ?? GameModes.Blood);
                roomModePrevious.interactable = roomModeNext.interactable = CanEditLobbyRules && actions is IRoomModeActions;
            }
            if (roomDurationLabel != null)
            {
                roomDurationLabel.text = FormatClock(lobbyState?.RoundSeconds ?? GameModes.DefaultRoundSeconds(GameModes.Blood));
                roomDurationPrevious.interactable = roomDurationNext.interactable = CanEditLobbyRules && actions is IRoomModeActions;
            }
        }

        private void UpdateLobbyControls()
        {
            if (lobbyState == null) return;
            var available = lobbyState.IsWaiting && !LobbyBusy;
            lobbyReadyButton.interactable = available;
            lobbyStartButton.interactable = available && lobbyState.IsOwner && lobbyState.CanStart;
            lobbyExploreButton.interactable = available && lobbyState.CanExplore;
            foreach (var entry in humanCountLabels)
                entry.Value.transform.parent.GetComponent<UnityEngine.UI.Button>().interactable = CanEditLobbyRules;
        }

        private void ChangeLobbyHumanCount(int? count)
        {
            if (!CanEditLobbyRules || lobbyState.HumanCount == count) return;
            lobbyRulesLatched = true;
            UpdateRoomMapView();
            UpdateLobbyControls();
            lobbyStatus.text = "Guardando reglas…";
            actions.SetHumanCount(count);
        }

        public void SetTrainingMaps(IReadOnlyList<TrainingMapOption> maps)
        {
            var copy = maps == null ? Array.Empty<TrainingMapOption>() : maps.ToArray();
            if (copy.Any(map => map == null) || copy.Select(map => map.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Training maps must be non-null with unique IDs.", nameof(maps));
            trainingMaps = copy;
            if (!HasSelectedTrainingMap) selectedTrainingMapId = trainingMaps.FirstOrDefault()?.Id;
            UpdateTrainingMapView();
        }

        private bool HasSelectedTrainingMap => trainingMaps.Any(map => map.Id == selectedTrainingMapId);
        private bool TrainingModeAvailable => HasSelectedTrainingMap && (trainingState.ModeId != GameModes.Tasks || trainingMaps.First(map => map.Id == selectedTrainingMapId).SupportsTasks);
        private void CycleTrainingMode(int delta)
        {
            if (TrainingBusy) return;
            int index = Array.IndexOf(AlfaModeText.ModeIds, trainingState.ModeId);
            string next = AlfaModeText.ModeIds[(index + delta + AlfaModeText.ModeIds.Length) % AlfaModeText.ModeIds.Length];
            PresentTraining(new TrainingUiState(trainingState.SelectedRole, modeId: next));
        }
        private void CycleRoomMode(int delta)
        {
            if (!CanEditLobbyRules || !(actions is IRoomModeActions modes)) return;
            int index = Array.IndexOf(AlfaModeText.ModeIds, lobbyState.ModeId);
            lobbyRulesLatched = true; UpdateRoomMapView(); UpdateLobbyControls();
            lobbyStatus.text = "Guardando reglas…";
            modes.SetRoomMode(AlfaModeText.ModeIds[(index + delta + AlfaModeText.ModeIds.Length) % AlfaModeText.ModeIds.Length]);
        }
        private void CycleRoomDuration(int delta)
        {
            if (!CanEditLobbyRules || !(actions is IRoomModeActions modes) || (delta != -1 && delta != 1)) return;
            int current = lobbyState.RoundSeconds;
            int next = delta > 0
                ? RoundDurationOptions.FirstOrDefault(value => value > current)
                : RoundDurationOptions.LastOrDefault(value => value < current);
            if (next == 0) next = delta > 0 ? RoundDurationOptions[0] : RoundDurationOptions[RoundDurationOptions.Length - 1];
            lobbyRulesLatched = true; UpdateRoomMapView(); UpdateLobbyControls();
            lobbyStatus.text = "Guardando reglas…";
            modes.SetRoomDurationSeconds(next);
        }

        private void CycleTrainingMap(int delta)
        {
            if (TrainingBusy || trainingMaps.Length == 0) return;
            int index = Array.FindIndex(trainingMaps, map => map.Id == selectedTrainingMapId);
            selectedTrainingMapId = trainingMaps[(index + delta + trainingMaps.Length) % trainingMaps.Length].Id;
            UpdateTrainingMapView();
        }

        private void UpdateTrainingMapView()
        {
            if (trainingMapLabel == null) return;
            trainingMapLabel.text = trainingMaps.FirstOrDefault(map => map.Id == selectedTrainingMapId)?.DisplayName ?? "SIN MAPAS";
            trainingMapPrevious.interactable = trainingMapNext.interactable = !TrainingBusy && trainingMaps.Length > 1;
            trainingHumanButton.interactable = trainingMosquitoButton.interactable = !TrainingBusy && TrainingModeAvailable;
            if (trainingModeLabel != null)
            {
                trainingModeLabel.text = AlfaModeText.Name(trainingState.ModeId);
                trainingModePrevious.interactable = trainingModeNext.interactable = !TrainingBusy;
            }
            if (!TrainingBusy && !HasSelectedTrainingMap) trainingStatus.text = NoTrainingMaps;
            else if (!TrainingBusy && !TrainingModeAvailable) trainingStatus.text = "Este mapa todavía no tiene tareas preparadas. Elegí otro mapa o modo.";
            else if (!TrainingBusy) trainingStatus.text = string.IsNullOrWhiteSpace(trainingState.Message) ? AlfaModeText.Instructions(trainingState.ModeId, trainingState.SelectedRole == AlfaRole.Mosquito) : trainingState.Message;
            if (resultsState != null && resultsState.IsTraining && resultsPrimary != null)
                resultsPrimary.interactable = !TrainingBusy && !resultsActionLatched && TrainingModeAvailable;
        }

        public void PresentTraining(TrainingUiState state)
        {
            trainingState = state ?? new TrainingUiState();
            trainingStartLatched = trainingState.IsLoading;
            if (!trainingState.IsLoading) trainingCancelLatched = false;
            var busy = TrainingBusy;
            if (trainingState.IsLoading) trainingPendingRole = trainingState.SelectedRole;
            trainingHumanButton.interactable = !busy;
            trainingMosquitoButton.interactable = !busy;
            trainingBackButton.interactable = !trainingCancelLatched;
            UpdateTrainingStartLabels(busy);
            trainingBackLabel.text = busy ? trainingCancelLatched ? "CANCELANDO…" : "CANCELAR" : "VOLVER";
            trainingStatus.text = trainingState.IsLoading ? "Preparando entrenamiento…" : trainingState.Message;
            if (screen == AlfaUiScreen.Results && resultsState != null && resultsState.IsTraining)
            {
                resultsActionLatched = busy;
                resultsPrimary.interactable = !busy && TrainingModeAvailable;
                resultsLeave.interactable = !trainingCancelLatched;
                resultsPrimaryLabel.text = busy ? "PREPARANDO…" : "REPETIR ENTRENAMIENTO";
                resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = busy ?
                    trainingCancelLatched ? "CANCELANDO…" : "CANCELAR" : "VOLVER AL MENÚ";
            }
            UpdateTrainingMapView();
        }

        private void UpdateTrainingStartLabels(bool busy)
        {
            trainingHumanLabel.text = busy && trainingPendingRole == AlfaRole.Human ? "PREPARANDO…" : "INICIAR";
            trainingMosquitoLabel.text = busy && trainingPendingRole == AlfaRole.Mosquito ? "PREPARANDO…" : "INICIAR";
        }

        public void ShowTraining()
        {
            PresentTraining(trainingState);
            UpdateTrainingMapView();
            SetScreen(AlfaUiScreen.Training, trainingState.SelectedRole == AlfaRole.Human ? "TrainingHumanButton" : "TrainingMosquitoButton");
        }

        public void PresentCustomization(CustomizationUiState state)
        {
            customizationState = state ?? throw new ArgumentNullException(nameof(state));
            customizationSaveLatched = state.IsSaving;
            customizationControlsGroup.interactable = !customizationSaveLatched;
            customizationControlsGroup.blocksRaycasts = !customizationSaveLatched;
            if (state.Mode == CustomizationUiMode.Modular)
            {
                customizationDraft = null;
                modularCustomizationDraft = state.DraftSelection.Copy();
                modularEditedRole = state.EditedRole;
                EnsureModularSelectedSlot();
                BuildModularCustomization();
                ResetModularScrollPositions();
            }
            else
            {
                modularCustomizationDraft = null;
                modularSelectedSlotId = string.Empty;
                customizationDraft = state.Draft.Copy();
                BuildPalette(humanPaletteRoot, state.SkinColors, customizationDraft.SkinColorId, option => SetCustomizationColor("skin", option));
                BuildPalette(pajamaPaletteRoot, state.PajamaColors, customizationDraft.PajamaColorId, option => SetCustomizationColor("pajama", option));
                BuildPalette(mosquitoPaletteRoot, state.MosquitoColors, customizationDraft.MosquitoColorId, option => SetCustomizationColor("mosquito", option));
            }
            UpdateCustomizationView();
        }

        public void ShowCustomization()
        {
            if (customizationState == null) PresentCustomization(DefaultCustomization());
            if (screen != AlfaUiScreen.Customization)
            {
                customizationReturnScreen = screen == AlfaUiScreen.Lobby && lobbyState?.IsWaiting == true
                    ? AlfaUiScreen.Lobby : AlfaUiScreen.MainMenu;
                customizationLobbyCode = customizationReturnScreen == AlfaUiScreen.Lobby ? lobbyState.RoomCode : string.Empty;
                if (customizationState.Mode == CustomizationUiMode.Modular)
                {
                    customizationSessionBaseline = null;
                    modularCustomizationSessionBaseline = modularCustomizationDraft.Copy();
                }
                else
                {
                    modularCustomizationSessionBaseline = null;
                    customizationSessionBaseline = customizationDraft.Copy();
                }
            }
            AlfaRole role = customizationState.Mode == CustomizationUiMode.Modular ? modularEditedRole : customizationDraft.Role;
            SetScreen(AlfaUiScreen.Customization, role == AlfaRole.Human ? "CustomizationHumanButton" : "CustomizationMosquitoButton");
            previewOrbit?.Show(role);
        }

        public void PresentSettings(SettingsUiState state)
        {
            settingsState = state ?? throw new ArgumentNullException(nameof(state));
            settingsApplyLatched = state.IsApplying;
            settingsDraft = state.Draft.Copy();
            AlfaUiMotionPreferences.ReducedMotion = settingsDraft.ReduceMenuMotion;
            settingsControlsGroup.interactable = !settingsApplyLatched;
            settingsControlsGroup.blocksRaycasts = !settingsApplyLatched;
            masterVolume.SetValueWithoutNotify(settingsDraft.MasterVolume);
            musicVolume.SetValueWithoutNotify(settingsDraft.MusicVolume);
            effectsVolume.SetValueWithoutNotify(settingsDraft.EffectsVolume);
            voiceVolume.SetValueWithoutNotify(settingsDraft.VoiceVolume);
            humanSensitivity.SetValueWithoutNotify(settingsDraft.HumanSensitivity);
            mosquitoSensitivity.SetValueWithoutNotify(settingsDraft.MosquitoSensitivity);
            UpdateSliderValue(masterVolume);
            UpdateSliderValue(musicVolume);
            UpdateSliderValue(effectsVolume);
            UpdateSliderValue(voiceVolume);
            UpdateSliderValue(humanSensitivity);
            UpdateSliderValue(mosquitoSensitivity);
            fullScreen.SetIsOnWithoutNotify(settingsDraft.FullScreen);
            vSync.SetIsOnWithoutNotify(settingsDraft.VSync);
            var frameLimitIndex = Array.IndexOf(FrameLimitOptions, settingsDraft.FrameLimit);
            frameLimitDropdown.SetValueWithoutNotify(Mathf.Max(0, frameLimitIndex));
            frameLimitDropdown.RefreshShownValue();
            invertY.SetIsOnWithoutNotify(settingsDraft.InvertY);
            reduceMenuMotion.SetIsOnWithoutNotify(settingsDraft.ReduceMenuMotion);
            reduceMenuMotion.transform.parent.gameObject.SetActive(state.SupportsReducedMenuMotion);
            videoSettings.SetActive(state.SupportsVideo);
            rebindNote.SetActive(state.SupportsRebinding);
            var deviceOptions = new[] { "ELEGÍ UN MICRÓFONO" }.Concat(state.VoiceDevices).ToList();
            voiceDeviceDropdown.ClearOptions(); voiceDeviceDropdown.AddOptions(deviceOptions);
            int voiceDeviceIndex = state.VoiceDevices.ToList().FindIndex(device => string.Equals(device, settingsDraft.VoiceDevice, StringComparison.Ordinal));
            voiceDeviceDropdown.SetValueWithoutNotify(voiceDeviceIndex + 1); voiceDeviceDropdown.RefreshShownValue();
            pushToTalkBindingLabel.text = "PTT · " + (voiceState?.BindingLabel ?? "V");
            settingsStatus.text = state.IsApplying ? "Aplicando ajustes…" : state.Message;
            settingsApplyButton.interactable = !settingsApplyLatched && !settingsDraft.SameValues(state.Saved);
            settingsApplyLabel.text = settingsApplyLatched ? "APLICANDO…" : "APLICAR";
            UpdateSettingsCycles();
        }

        public void PresentVoice(VoiceUiState state)
        {
            voiceState = state ?? throw new ArgumentNullException(nameof(state));
            if (hudVoice != null)
            {
                string status = state.LocalMuted ? "VOZ SILENCIADA" : state.Transmitting ? "HABLANDO" :
                    !state.DeviceAvailable ? "VOZ · ELEGÍ MICRÓFONO" : "PTT " + state.BindingLabel;
                hudVoice.text = state.InRoom ? status : string.Empty;
                hudVoice.color = state.Transmitting ? AlfaUiTheme.Mint400 : state.LocalMuted || !state.DeviceAvailable ? AlfaUiTheme.Pajama500 : AlfaUiTheme.Moon200;
            }
            if (pauseVoiceStatus != null)
                pauseVoiceStatus.text = string.IsNullOrWhiteSpace(state.Notice) ? state.ScopeLabel : state.Notice;
            if (pauseVoiceMuteLabel != null)
                pauseVoiceMuteLabel.text = state.LocalMuted ? "ACTIVAR MI MICRÓFONO" : "SILENCIAR MI MICRÓFONO";
            if (pauseVoiceMuteButton != null) pauseVoiceMuteButton.interactable = state.InRoom && actions is IVoiceActions;
            if (pushToTalkBindingLabel != null) pushToTalkBindingLabel.text = "PTT · " + state.BindingLabel;
            UpdateLobbyVoiceMarkers();
            RebuildPauseVoicePeers();
        }

        public void OpenSettings(AlfaUiScreen returnTo)
        {
            if (settingsState == null) PresentSettings(DefaultSettings());
            settingsReturnScreen = returnTo;
            if (returnTo == AlfaUiScreen.Gameplay || returnTo == AlfaUiScreen.Pause)
                actions.SetGameplayInputBlocked(true);
            SetScreen(AlfaUiScreen.Settings, "MasterVolumeSlider");
        }

        public void PresentHud(BloodHudUiState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            isSpectator = state.IsSpectator;
            hudClock.text = FormatClock(state.SecondsRemaining);
            hudBlood.text = AlfaModeText.Score(state.ModeId, state.BloodCurrent, state.BloodTarget, state.TasksCompleted, state.TasksGoal, state.MosquitoesAlive);
            hudScoreIcon.Kind = state.ModeId == GameModes.Blood ? AlfaUiIconKind.Blood : state.ModeId == GameModes.Tasks ? AlfaUiIconKind.Ready : AlfaUiIconKind.Mosquito;
            hudBlood.fontSize = state.ModeId == GameModes.Survival ? 17f : 20f;
            var bloodRatio = state.ModeId == GameModes.Tasks ? (state.TasksGoal > 0 ? Mathf.Clamp01((float)state.TasksCompleted / state.TasksGoal) : 0) : state.BloodTarget > 0f ? Mathf.Clamp01(state.BloodCurrent / state.BloodTarget) : 0f;
            hudBloodFill.transform.parent.gameObject.SetActive(state.ModeId != GameModes.Survival);
            hudTaskPanel.SetActive(!state.IsSpectator && !string.IsNullOrWhiteSpace(state.PrivateTaskText));
            hudTask.text = state.PrivateTaskText;
            hudTaskFill.rectTransform.anchorMax = new Vector2(state.TaskProgress01, 1f);
            hudLives.text = state.IsSpectator ? "ESPECTADOR" : state.Role == AlfaRole.Mosquito && state.ModeId != GameModes.Blood ? $"VIDAS  {state.LivesRemaining}" : string.Empty;
            var equipment = state.IsSpectator ? null : state.Equipment;
            hudEquipmentPanel.SetActive(equipment != null);
            if (equipment != null)
            {
                hudEquipmentLabels[0].text = (equipment.SelectedSlot == -1 ? ">  " : "") + "0  MANOS\n<size=85%>SIN OBJETO</size>";
                hudEquipmentIcons[0].Kind = AlfaUiIconKind.Hands;
                for (int i = 0; i < equipment.Slots.Count; i++)
                {
                    var slot = equipment.Slots[i];
                    hudEquipmentLabels[i + 1].text = (equipment.SelectedSlot == i ? ">  " : "") + (i + 1) + "  " + slot.Label +
                        (slot.ResourceText.Length == 0 ? "" : "\n<size=85%>" + slot.ResourceText + "</size>");
                    hudEquipmentIcons[i + 1].Kind = slot.Icon;
                }
                for (int i = 0; i < hudEquipmentLabels.Length; i++)
                {
                    bool selected = equipment.SelectedSlot == i - 1;
                    hudEquipmentLabels[i].color = selected ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Sheet100;
                    hudEquipmentIcons[i].color = selected ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Moon200;
                }
                hudStaminaLabel.text = "ESTAMINA  " + Mathf.RoundToInt(equipment.Stamina01 * 100) + "%";
                hudStaminaFill.rectTransform.anchorMax = new Vector2(equipment.Stamina01, 1f);
                bool charging = equipment.ThrowCharge01 > 0 || equipment.ThrowAwaitingRelease;
                hudThrowTrack.SetActive(charging);
                hudThrowLabel.text = equipment.ThrowAwaitingRelease ? "LANZAMIENTO PENDIENTE" : "CARGA PANTUFLA  " + Mathf.RoundToInt(equipment.ThrowCharge01 * 100) + "% · SOLTÁ CLIC";
                hudThrowFill.rectTransform.anchorMax = new Vector2(equipment.ThrowCharge01, 1f);
                hudSwapOffer.text = equipment.SwapOfferText;
                var hasSwapOffer = !string.IsNullOrWhiteSpace(equipment.SwapOfferText);
                hudSwapOffer.gameObject.SetActive(hasSwapOffer);
                UpdateEquipmentLayout(charging, hasSwapOffer);
            }
            hudReticle.SetActive(!state.IsSpectator);
            hudBloodFill.rectTransform.anchorMax = new Vector2(bloodRatio, 1f);
            hudRoleLabel.text = state.Role == AlfaRole.Human ? "HUMANO" : "MOSQUITO";
            hudRoleIcon.Kind = state.Role == AlfaRole.Human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            hudRoleIcon.color = state.Role == AlfaRole.Human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            var roleColor = state.Role == AlfaRole.Human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            hudRoleBackground.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.22f);
            AlfaUiFactory.SetFrame(hudRoleBackground, AlfaUiTheme.WithAlpha(roleColor, 0.9f));
            hudInteraction.text = state.Interaction;
            hudHint.text = string.IsNullOrWhiteSpace(state.ContextHint) ? DefaultRoleHint(state.Role) : state.ContextHint;
            hudActorState.text = state.ModeId != GameModes.Blood && state.ActorState == HudActorState.Extracting ? "INTERRUMPIENDO" : ActorStateText(state.Role, state.ActorState);
            hudActorState.color = state.ActorState == HudActorState.Normal ? AlfaUiTheme.Moon200 : AlfaUiTheme.Pajama500;
            hudProgress.transform.parent.gameObject.SetActive(state.ActorState == HudActorState.Extracting || state.ActorState == HudActorState.Recovering);
            var rect = hudProgress.rectTransform;
            rect.anchorMax = new Vector2(state.StateProgress01, 1f);
            hudNetwork.text = state.NetworkMessage;
            hudPromptPanel.SetActive(!string.IsNullOrWhiteSpace(hudInteraction.text));
            hudHintPanel.SetActive(!string.IsNullOrWhiteSpace(hudHint.text));
            hudStatePanel.SetActive(!string.IsNullOrWhiteSpace(hudActorState.text) ||
                state.ActorState == HudActorState.Extracting || state.ActorState == HudActorState.Recovering);
            if (screen != AlfaUiScreen.Gameplay && screen != AlfaUiScreen.Pause && screen != AlfaUiScreen.Settings)
                ShowGameplay();
        }

        public void ShowGameplay()
        {
            trainingStartLatched = false;
            trainingCancelLatched = false;
            if (trainingState.IsLoading)
                trainingState = new TrainingUiState(trainingState.SelectedRole, false, trainingState.Message, trainingState.ModeId);
            actions.SetGameplayInputBlocked(false);
            SetScreen(AlfaUiScreen.Gameplay, null);
        }

        public void ShowGameplay(bool isTraining)
        {
            gameplayIsTraining = isTraining;
            ShowGameplay();
        }

        public void PresentResults(ResultsUiState state)
        {
            resultsState = state ?? throw new ArgumentNullException(nameof(state));
            gameplayIsTraining = state.IsTraining;
            resultsActionLatched = false;
            trainingStartLatched = false;
            trainingCancelLatched = false;
            trainingState = new TrainingUiState(state.TrainingRole, false, modeId: state.ModeId);
            selectedTrainingMapId = state.MapId;
            actions.SetGameplayInputBlocked(true);
            resultsTitle.text = state.Outcome == MatchOutcome.Humans ? "GANARON LOS HUMANOS" :
                state.Outcome == MatchOutcome.Mosquitoes ? "GANARON LOS MOSQUITOS" : "RONDA INTERRUMPIDA";
            var reason = string.IsNullOrWhiteSpace(state.Reason) ? string.Empty : "\n" + state.Reason;
            resultsStats.text = AlfaModeText.Name(state.ModeId) + "\n" + AlfaModeText.ResultScore(state.ModeId, state.BloodCurrent, state.BloodTarget, state.TasksCompleted, state.TasksGoal, state.MosquitoesAlive) + $"\nTiempo: {FormatClock(state.ElapsedSeconds)}{reason}";
            resultsPrimary.gameObject.SetActive(state.IsTraining || state.IsOwner);
            resultsPrimary.interactable = !state.IsTraining || TrainingModeAvailable;
            resultsLeave.interactable = true;
            resultsPrimaryLabel.text = state.IsTraining ? "REPETIR ENTRENAMIENTO" : "VOLVER AL LOBBY";
            resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = state.IsTraining ? "VOLVER AL MENÚ" : "SALIR DE LA SALA";
            if (!state.IsTraining && !state.IsOwner) resultsStats.text += "\n\nESPERANDO AL ANFITRIÓN…";
            if (state.IsTraining && !HasSelectedTrainingMap) resultsStats.text += "\n\n" + NoTrainingMaps;
            SetScreen(AlfaUiScreen.Results, resultsPrimary.gameObject.activeSelf ? "ResultsPrimaryButton" : "ResultsLeaveButton");
        }

        public void ShowPause()
        {
            actions.SetGameplayInputBlocked(true);
            pauseLeaveLabel.text = gameplayIsTraining ? "VOLVER AL MENÚ" : "SALIR DE LA SALA";
            SetScreen(AlfaUiScreen.Pause, "PauseContinueButton");
        }

        private void BuildViews(AlfaUiDependencies dependencies)
        {
            BuildMain();
            BuildOnlineForm();
            BuildLobby();
            BuildTraining();
            BuildCustomization(dependencies);
            BuildSettings();
            BuildHud();
            BuildPause();
            BuildResults();
            BuildConfirm();
        }

        private void BuildMain()
        {
            var view = factory.View("MainMenuView", transform, false);
            var backdrop = view.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = Color.clear;
            backdrop.raycastTarget = false;
            screens[AlfaUiScreen.MainMenu] = view;

            // Night wash keeps the live bedroom scene visible on the right while the left column stays legible.
            var nightWash = AlfaUiFactory.Node("NightWash", view.transform, typeof(UnityEngine.UI.Image));
            var nightWashImage = nightWash.GetComponent<UnityEngine.UI.Image>();
            nightWashImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.88f);
            nightWashImage.sprite = AlfaUiFactory.HorizontalFadeSprite();
            nightWashImage.raycastTarget = false;
            Anchor(nightWash.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1040f, 0f));
            var floorWash = AlfaUiFactory.Node("FloorWash", view.transform, typeof(UnityEngine.UI.Image));
            var floorWashImage = floorWash.GetComponent<UnityEngine.UI.Image>();
            floorWashImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.7f);
            floorWashImage.sprite = AlfaUiFactory.VerticalFadeSprite();
            floorWashImage.raycastTarget = false;
            Anchor(floorWash.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 220f));

            var brand = factory.BrandLockup(view.transform, "Brand", 400f, "HUMANOS CONTRA MOSQUITOS");
            Anchor(brand, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -14f), new Vector2(660f, 400f));

            // Top-anchored under the wordmark so the rail never climbs into the logo on wide or tall aspects.
            var menu = factory.Vertical(view.transform, "MenuRail", 12f, TextAnchor.UpperLeft);
            Anchor(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -440f), new Vector2(470f, 400f));
            MenuRailButton(menu, "MainPlayButton", "JUGAR", ShowOnlineChoice, AlfaUiIconKind.Play);
            MenuRailButton(menu, "MainTrainingButton", "ENTRENAMIENTO", ShowTraining, AlfaUiIconKind.Training);
            MenuRailButton(menu, "MainCustomizeButton", "PERSONALIZACIÓN", ShowCustomization, AlfaUiIconKind.Customize);
            MenuRailButton(menu, "MainSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.MainMenu), AlfaUiIconKind.Gear);
            MenuRailButton(menu, "MainQuitButton", "SALIR", ConfirmQuit, AlfaUiIconKind.Exit);

            var motto = factory.Text(view.transform, "Motto", "LA NOCHE NUNCA ES TAN TRANQUILA", 22f, AlfaUiTheme.Moon200, TextAlignmentOptions.BottomLeft, true);
            motto.characterSpacing = 5f;
            motto.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(motto.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 58f), new Vector2(760f, 34f));
            var version = string.IsNullOrWhiteSpace(Application.version) ? "ALFA" : Application.version.Replace("-", " / ").ToUpperInvariant();
            var versionText = factory.Text(view.transform, "Version", version + "  ·  WINDOWS", 15f, AlfaUiTheme.Disabled, TextAlignmentOptions.BottomRight);
            versionText.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(versionText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 26f), new Vector2(420f, 24f));
        }

        private UnityEngine.UI.Button MenuRailButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction callback, AlfaUiIconKind icon)
        {
            var button = factory.Button(parent, name, label, callback, AlfaButtonStyle.Menu, 68f, icon);
            factory.ComicLabel(button, 34f);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(text.rectTransform, 82f, 16f, 4f, 2f);
            return button;
        }

        private void BuildOnlineForm()
        {
            var view = factory.View("OnlineFormView", transform, false);
            SetSceneScrim(view, 0.62f);
            screens[AlfaUiScreen.CreateRoom] = view;
            screens[AlfaUiScreen.JoinRoom] = view;
            var panel = CenteredPanel(view.transform, "OnlineFormCard", 1180f, 780f);

            var form = AlfaUiFactory.Node("OnlineForm", panel, typeof(CanvasGroup));
            AlfaUiFactory.Fill(form.GetComponent<RectTransform>());
            onlineFormGroup = form.GetComponent<CanvasGroup>();
            onlineFormTitle = factory.Title(form.transform, "Title", "JUGAR ONLINE", 50f);
            Anchor(onlineFormTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -28f), new Vector2(-88f, 60f));
            var intro = factory.Caption(form.transform, "Intro", "CREÁ UNA SALA O UNITE A UNA EXISTENTE", 18f);
            intro.color = AlfaUiTheme.Moon200;
            Anchor(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(46f, -90f), new Vector2(-92f, 26f));

            var tabs = factory.Horizontal(form.transform, "OnlineTabs", 12f, TextAnchor.MiddleCenter);
            tabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(-88f, 62f));
            onlineCreateTab = factory.Button(tabs, "OnlineCreateTab", "CREAR SALA", () => SwitchOnlineTab(true), AlfaButtonStyle.Tab, 62f, AlfaUiIconKind.House);
            onlineJoinTab = factory.Button(tabs, "OnlineJoinTab", "UNIRSE A SALA", () => SwitchOnlineTab(false), AlfaButtonStyle.Tab, 62f, AlfaUiIconKind.Enter);

            // Left column: identity, room code, status and the green call to action.
            var left = factory.Vertical(form.transform, "FormColumn", 10f);
            Anchor(left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -222f), new Vector2(520f, 340f));
            factory.Caption(left, "PlayerNameLabel", "TU NOMBRE");
            playerNameInput = factory.Input(left, "PlayerNameInput", "Cómo te dicen tus amigos", 24);
            playerNameInput.onSubmit.AddListener(_ => SubmitOnline());
            var codeContainer = factory.Vertical(left, "RoomCodeRow", 10f);
            roomCodeRow = codeContainer.gameObject;
            factory.Divider(codeContainer, "CodeGap", Color.clear, 6f);
            factory.Caption(codeContainer, "RoomCodeLabel", "CÓDIGO DE SALA");
            var codeActions = factory.Horizontal(codeContainer, "CodeActions", 10f);
            codeActions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            roomCodeInput = factory.Input(codeActions, "RoomCodeInput", "XXXXX-XXXXX", 32, true);
            roomCodeInput.onValueChanged.AddListener(FormatRoomCodeWhileEditing);
            roomCodeInput.onSubmit.AddListener(_ => SubmitOnline());
            onlinePasteButton = factory.Button(codeActions, "PasteRoomCodeButton", "PEGAR", PasteRoomCode, AlfaButtonStyle.Secondary, 56f, AlfaUiIconKind.Copy);
            var pasteLayout = onlinePasteButton.GetComponent<UnityEngine.UI.LayoutElement>();
            pasteLayout.preferredWidth = 184f;
            pasteLayout.flexibleWidth = 0f;
            var codePreview = factory.Vertical(left, "RoomCodePreviewRow", 10f);
            onlineCodePreviewRow = codePreview.gameObject;
            factory.Divider(codePreview, "PreviewGap", Color.clear, 6f);
            factory.Caption(codePreview, "RoomCodePreviewLabel", "CÓDIGO DE SALA");
            var previewChip = factory.Inset(codePreview, "RoomCodePreview", 56f);
            var previewKey = factory.Icon(previewChip, "KeyIcon", AlfaUiIconKind.Key, AlfaUiTheme.Lamp400);
            Anchor(previewKey.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(28f, 28f));
            var previewText = factory.Text(previewChip, "PreviewText", "Se genera al crear la sala", 19f, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            previewText.fontStyle = FontStyles.Italic;
            previewText.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(previewText.rectTransform, 58f, 14f, 4f, 4f);
            onlineStatus = factory.Text(left, "OnlineStatus", string.Empty, 19f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            onlineStatus.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 50f;

            onlinePrimaryButton = factory.Button(form.transform, "OnlinePrimaryButton", "CREAR SALA", SubmitOnline, AlfaButtonStyle.Success, 80f, AlfaUiIconKind.Play, false);
            factory.ComicLabel(onlinePrimaryButton, 40f);
            onlinePrimaryLabel = onlinePrimaryButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            Anchor((RectTransform)onlinePrimaryButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 40f), new Vector2(520f, 80f));

            // Right column: what the current tab does, as UI-06 list rows. There is no public room browser:
            // rooms are private and joined by code, so this column never pretends to list open matches.
            var right = factory.Vertical(form.transform, "InfoColumn", 10f);
            Anchor(right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -222f), new Vector2(528f, 380f));
            onlineInfoTitle = factory.Caption(right, "InfoTitle", "TU SALA PRIVADA");
            onlineInfoRows = new RectTransform[4];
            for (var i = 0; i < onlineInfoRows.Length; i++)
                onlineInfoRows[i] = factory.ListRow(right, "InfoRow" + (i + 1), AlfaUiIconKind.Info, string.Empty, string.Empty, AlfaUiTheme.StatusOk, 68f);

            onlineBackButton = factory.Button(form.transform, "OnlineBackButton", "VOLVER", ShowMainMenu, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Back);
            Anchor((RectTransform)onlineBackButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 50f), new Vector2(250f, 60f));

            BuildOnlineConnectionCards(panel);
        }

        private void BuildOnlineConnectionCards(Transform panel)
        {
            // UI-06 screen 11: a connecting card with spinner and an error card with a red warning and retry.
            onlineOverlay = AlfaUiFactory.Node("OnlineOverlay", panel, typeof(UnityEngine.UI.Image));
            var overlayImage = onlineOverlay.GetComponent<UnityEngine.UI.Image>();
            overlayImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.PanelRadius);
            overlayImage.type = UnityEngine.UI.Image.Type.Sliced;
            overlayImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.9f);
            overlayImage.raycastTarget = true;
            AlfaUiFactory.Fill(onlineOverlay.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);

            onlineConnectingCard = factory.Panel(onlineOverlay.transform, "OnlineConnectingCard", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.98f)).gameObject;
            Anchor((RectTransform)onlineConnectingCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 64f), new Vector2(780f, 190f));
            var wifi = factory.Icon(onlineConnectingCard.transform, "WifiIcon", AlfaUiIconKind.Wifi, AlfaUiTheme.Sky400);
            Anchor(wifi.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(104f, 104f));
            onlineConnectingTitle = factory.Title(onlineConnectingCard.transform, "ConnectingTitle", "CONECTANDO…", 42f);
            onlineConnectingTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(onlineConnectingTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(190f, 2f), new Vector2(-330f, 56f));
            onlineConnectingMessage = factory.Text(onlineConnectingCard.transform, "ConnectingMessage", string.Empty, 20f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            Anchor(onlineConnectingMessage.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(192f, -6f), new Vector2(-332f, 58f));
            var spinner = factory.Spinner(onlineConnectingCard.transform, "ConnectingSpinner", 72f, AlfaUiTheme.Sky400);
            Anchor(spinner, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 0f), new Vector2(72f, 72f));
            onlineCancelButton = factory.Button(onlineOverlay.transform, "OnlineCancelButton", "CANCELAR", RequestOnlineCancel, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Exit);
            Anchor((RectTransform)onlineCancelButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(300f, 60f));

            onlineErrorCard = factory.Panel(onlineOverlay.transform, "OnlineErrorCard", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.98f)).gameObject;
            AlfaUiFactory.SetFrame(onlineErrorCard.GetComponent<RectTransform>(), AlfaUiTheme.WithAlpha(AlfaUiTheme.StatusWarn, 0.55f));
            Anchor((RectTransform)onlineErrorCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(780f, 330f));
            var warning = factory.Icon(onlineErrorCard.transform, "WarningIcon", AlfaUiIconKind.Warning, AlfaUiTheme.StatusWarn);
            Anchor(warning.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -44f), new Vector2(112f, 112f));
            onlineErrorTitle = factory.Title(onlineErrorCard.transform, "ErrorTitle", "NO SE PUDO CONECTAR", 40f, AlfaUiTheme.StatusWarn);
            onlineErrorTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(onlineErrorTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(196f, -40f), new Vector2(-236f, 54f));
            onlineErrorMessage = factory.Text(onlineErrorCard.transform, "ErrorMessage", string.Empty, 21f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            Anchor(onlineErrorMessage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(198f, -100f), new Vector2(-238f, 100f));
            var errorActions = factory.Horizontal(onlineErrorCard.transform, "ErrorActions", 14f, TextAnchor.MiddleLeft);
            Anchor(errorActions, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(196f, 36f), new Vector2(-236f, 62f));
            onlineRetryButton = factory.Button(errorActions, "OnlineRetryButton", "REINTENTAR", SubmitOnline, AlfaButtonStyle.Primary, 62f, AlfaUiIconKind.Refresh, false);
            onlineRetryButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 280f;
            onlineErrorBackButton = factory.Button(errorActions, "OnlineErrorBackButton", "VOLVER", DismissOnlineError, AlfaButtonStyle.Secondary, 62f, AlfaUiIconKind.Back);
            onlineErrorBackButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 220f;
            onlineOverlay.SetActive(false);
        }

        private void BuildLobby()
        {
            var view = factory.View("LobbyOverlayView", transform, false);
            screens[AlfaUiScreen.Lobby] = view;
            var safe = factory.SafeArea(view.transform, 36f, 36f, 28f, 32f);

            // Header: "ESPERANDO JUGADORES" with the X/N counter and map · mode, as UI-06 screen 3.
            var header = factory.Panel(safe, "Header", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f));
            Anchor(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1120f, 132f));
            var accent = AlfaUiFactory.Node("Accent", header, typeof(UnityEngine.UI.Image));
            var accentImage = accent.GetComponent<UnityEngine.UI.Image>();
            accentImage.sprite = AlfaUiSkin.Fill(6f);
            accentImage.type = UnityEngine.UI.Image.Type.Sliced;
            accentImage.color = AlfaUiTheme.Sky400;
            accentImage.raycastTarget = false;
            Anchor(accent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(7f, -28f));
            lobbyHeaderTitle = factory.Title(header, "Title", "ESPERANDO JUGADORES", 50f);
            lobbyHeaderTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(lobbyHeaderTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -16f), new Vector2(700f, 62f));
            lobbyHeaderNote = factory.Caption(header, "Note", "EL ANFITRIÓN INICIA LA RONDA CUANDO TODOS ESTÁN LISTOS", 17f);
            lobbyHeaderNote.color = AlfaUiTheme.Moon200;
            Anchor(lobbyHeaderNote.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(38f, 22f), new Vector2(720f, 26f));
            var separator = AlfaUiFactory.Node("Separator", header, typeof(UnityEngine.UI.Image));
            separator.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f);
            separator.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Anchor(separator.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-352f, 0f), new Vector2(2f, -36f));
            lobbyCount = factory.Title(header, "PlayerCount", "1/16", 56f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopRight);
            lobbyCount.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(lobbyCount.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -12f), new Vector2(300f, 66f));
            lobbyMapMode = factory.Text(header, "MapMode", string.Empty, 19f, AlfaUiTheme.Moon200, TextAlignmentOptions.BottomRight, true);
            lobbyMapMode.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyMapMode.characterSpacing = 0.4f;
            lobbyMapMode.enableAutoSizing = true;
            lobbyMapMode.fontSizeMin = 13f;
            lobbyMapMode.fontSizeMax = 19f;
            Anchor(lobbyMapMode.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(310f, 28f));

            // Left: players with Ready / Not ready status bars (no text chat exists; voice state is shown per row).
            var rosterPanel = factory.Panel(safe, "RosterPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.92f));
            Anchor(rosterPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(470f, -152f));
            var roster = factory.Vertical(rosterPanel, "Roster", 10f);
            AlfaUiFactory.Fill(roster, 22f, 22f, 20f, 20f);
            factory.SectionHeader(roster, "RosterHeader", "JUGADORES", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            var roleNote = factory.Text(roster, "RoleNote", "Los roles se sortean al empezar cada ronda.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            roleNote.textWrappingMode = TextWrappingModes.NoWrap;
            factory.ScrollView(roster, "MemberScrollView", out var memberContent, 520f);
            for (var i = 0; i < LetMeSleep.Core.RoomRules.Capacity; i++)
            {
                var rowPanel = factory.ListRow(memberContent, $"MemberPanel{i + 1}", AlfaUiIconKind.Human, string.Empty, string.Empty, AlfaUiTheme.StatusWarn, 66f);
                var title = rowPanel.Find("RowTitle").GetComponent<TextMeshProUGUI>();
                title.name = $"Member{i + 1}";
                rowPanel.gameObject.SetActive(false);
                memberRows.Add(title);
                memberSubtitles.Add(rowPanel.Find("RowSubtitle").GetComponent<TextMeshProUGUI>());
                memberRowRoots.Add(rowPanel.gameObject);
                memberStatusIcons.Add(rowPanel.Find("RowIcon").GetComponent<AlfaUiIcon>());
                memberStatusBars.Add(rowPanel.Find("StatusBar").GetComponent<UnityEngine.UI.Image>());
            }

            // Right: host rules for the next round (kept from the alfa; UI-06 does not draw them).
            var rulesPanel = factory.Panel(safe, "RulesPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f));
            Anchor(rulesPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(560f, 648f));
            var rulesFit = rulesPanel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rulesFit.padding = new RectOffset(24, 24, 20, 22);
            rulesFit.childControlWidth = rulesFit.childControlHeight = true;
            rulesFit.childForceExpandWidth = true;
            rulesFit.childForceExpandHeight = false;
            rulesPanel.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var rules = factory.Vertical(rulesPanel, "Rules", 10f);
            var rulesHeader = factory.SectionHeader(rules, "RulesHeader", "PRÓXIMA RONDA", AlfaUiIconKind.Play, AlfaUiTheme.Lamp400);
            lobbyRoleBadge = factory.Text(rulesHeader, "LocalAuthority", "INVITADO", 16f, AlfaUiTheme.Moon200, TextAlignmentOptions.Right, true);
            lobbyRoleBadge.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyRoleBadge.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 170f;
            factory.Divider(rules, "AuthorityDivider", AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.5f));
            roomModeLabel = AddCycleField(rules, "MODO", "RoomMode", -1, 1, CycleRoomMode);
            roomModePrevious = roomModeLabel.transform.parent.parent.Find("RoomModePrevious").GetComponent<UnityEngine.UI.Button>();
            roomModeNext = roomModeLabel.transform.parent.parent.Find("RoomModeNext").GetComponent<UnityEngine.UI.Button>();
            roomDurationLabel = AddCycleField(rules, "TIEMPO", "RoomDuration", -1, 1, CycleRoomDuration);
            roomDurationPrevious = roomDurationLabel.transform.parent.parent.Find("RoomDurationPrevious").GetComponent<UnityEngine.UI.Button>();
            roomDurationNext = roomDurationLabel.transform.parent.parent.Find("RoomDurationNext").GetComponent<UnityEngine.UI.Button>();
            roomMapLabel = AddCycleField(rules, "MAPA", "RoomMap", -1, 1, CycleRoomMap);
            roomMapLabel.richText = false;
            roomMapPrevious = roomMapLabel.transform.parent.parent.Find("RoomMapPrevious").GetComponent<UnityEngine.UI.Button>();
            roomMapNext = roomMapLabel.transform.parent.parent.Find("RoomMapNext").GetComponent<UnityEngine.UI.Button>();
            UpdateRoomMapView();
            factory.Caption(rules, "HumanCountLabel", "CANTIDAD DE HUMANOS");
            var counts = factory.Horizontal(rules, "HumanCount", 6f, TextAnchor.MiddleLeft);
            for (var count = 0; count <= 5; count++)
            {
                var captured = count;
                var button = factory.Button(counts, "HumanCount" + count, count == 0 ? "AUTO" : count.ToString(),
                    () => ChangeLobbyHumanCount(captured == 0 ? (int?)null : captured), AlfaButtonStyle.Tab, 48f);
                var countLayout = button.GetComponent<UnityEngine.UI.LayoutElement>();
                countLayout.preferredWidth = count == 0 ? 112f : 60f;
                countLayout.flexibleWidth = 0f;
                humanCountLabels[count] = button.GetComponentInChildren<TextMeshProUGUI>();
                AlfaUiFactory.Fill(humanCountLabels[count].rectTransform, 6f, 6f, 6f, 6f);
            }
            lobbyStartReason = factory.Text(rules, "StartReason", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Left);
            lobbyStartButton = factory.Button(rules, "LobbyStartButton", "INICIAR RONDA", BeginRound, AlfaButtonStyle.Success, 64f, AlfaUiIconKind.Play);
            factory.ComicLabel(lobbyStartButton, 32f);
            lobbyStartLabel = lobbyStartButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            // Bottom-right action stack: room code, invite (copies the code), explore / customise and the big LISTO.
            var actionsStack = factory.Vertical(safe, "LobbyActionsStack", 12f, TextAnchor.LowerRight);
            Anchor(actionsStack, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(560f, 330f));
            lobbyStatus = factory.Text(actionsStack, "LobbyStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Right);
            lobbyStatus.textWrappingMode = TextWrappingModes.NoWrap;
            var codeRow = factory.Horizontal(actionsStack, "CodeRow", 12f, TextAnchor.MiddleRight);
            codeRow.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60f;
            var codeChip = factory.Inset(codeRow, "RoomCodeChip", 60f);
            var chipLayout = codeChip.GetComponent<UnityEngine.UI.LayoutElement>();
            chipLayout.preferredWidth = 226f;
            chipLayout.flexibleWidth = 0f;
            var keyIcon = factory.Icon(codeChip, "KeyIcon", AlfaUiIconKind.Key, AlfaUiTheme.Lamp400);
            Anchor(keyIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(28f, 28f));
            lobbyCode = factory.Text(codeChip, "RoomCode", "PREPARANDO…", 24f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center, true);
            lobbyCode.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyCode.characterSpacing = 1.5f;
            lobbyCode.enableAutoSizing = true;
            lobbyCode.fontSizeMin = 15f;
            lobbyCode.fontSizeMax = 24f;
            AlfaUiFactory.Fill(lobbyCode.rectTransform, 46f, 8f, 6f, 6f);
            lobbyCopyButton = factory.Button(codeRow, "LobbyCopyButton", "INVITAR AMIGOS", () =>
            {
                if (lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.RoomCode))
                {
                    actions.CopyRoomCode(lobbyState.RoomCode);
                    lobbyStatus.text = "Código copiado. Pasáselo a tus amigos.";
                }
            }, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Invite);
            lobbyCopyButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 322f;
            lobbyCopyButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().fontSize = 22f;
            var lobbyActions = factory.Horizontal(actionsStack, "LobbyActions", 12f, TextAnchor.MiddleCenter);
            lobbyActions.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            lobbyActions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 58f;
            lobbyExploreButton = factory.Button(lobbyActions, "LobbyExploreButton", "RECORRER", BeginLobbyExploration, AlfaButtonStyle.Secondary, 58f, AlfaUiIconKind.Explore);
            lobbyCustomizeButton = factory.Button(lobbyActions, "LobbyCustomizeButton", "PERSONALIZAR", ShowCustomization, AlfaButtonStyle.Secondary, 58f, AlfaUiIconKind.Customize);
            foreach (var button in new[] { lobbyExploreButton, lobbyCustomizeButton })
            {
                var layout = button.GetComponent<UnityEngine.UI.LayoutElement>();
                layout.minWidth = 0f;
                layout.flexibleWidth = 1f;
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 20f;
                label.characterSpacing = .2f;
            }
            lobbyReadyButton = factory.Button(actionsStack, "LobbyReadyButton", "LISTO", () =>
            {
                if (lobbyState == null || !lobbyState.IsWaiting || LobbyBusy) return;
                lobbyReadyLatched = true;
                UpdateRoomMapView();
                UpdateLobbyControls();
                lobbyReadyButton.interactable = false;
                lobbyStartButton.interactable = false;
                lobbyReadyLabel.text = "GUARDANDO…";
                lobbyStatus.text = "Guardando estado…";
                Focus(lobbyCopyButton.gameObject);
                actions.SetReady(!lobbyState.LocalReady);
            }, AlfaButtonStyle.Success, 88f, AlfaUiIconKind.Ready);
            factory.ComicLabel(lobbyReadyButton, 48f);
            lobbyReadyLabel = lobbyReadyButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        private void BuildTraining()
        {
            var view = factory.View("TrainingView", transform, false);
            SetSceneScrim(view, 0.5f);
            screens[AlfaUiScreen.Training] = view;
            var panel = CenteredPanel(view.transform, "TrainingCard", 1300f, 930f);
            var title = factory.Title(panel, "Title", "ENTRENAMIENTO", 50f);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -26f), new Vector2(-88f, 60f));
            var intro = factory.Caption(panel, "Intro", "PRACTICÁ, APRENDÉ Y MEJORÁ TUS HABILIDADES", 18f);
            intro.color = AlfaUiTheme.Moon200;
            Anchor(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(46f, -88f), new Vector2(-92f, 26f));

            var cards = factory.Horizontal(panel, "RoleCards", 36f, TextAnchor.UpperCenter);
            cards.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            cards.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = true;
            Anchor(cards, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(-88f, 560f));
            trainingHumanButton = BuildTrainingRoleCard(cards, AlfaRole.Human, out trainingHumanLabel);
            trainingMosquitoButton = BuildTrainingRoleCard(cards, AlfaRole.Mosquito, out trainingMosquitoLabel);

            var options = factory.Horizontal(panel, "TrainingOptions", 24f, TextAnchor.MiddleLeft);
            options.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(options, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(-88f, 60f));
            trainingModeLabel = AddCycleField(options, "MODO", "TrainingMode", -1, 1, CycleTrainingMode, 96f, 230f);
            trainingModePrevious = trainingModeLabel.transform.parent.parent.Find("TrainingModePrevious").GetComponent<UnityEngine.UI.Button>();
            trainingModeNext = trainingModeLabel.transform.parent.parent.Find("TrainingModeNext").GetComponent<UnityEngine.UI.Button>();
            trainingMapLabel = AddCycleField(options, "MAPA", "TrainingMap", -1, 1, CycleTrainingMap, 96f, 300f);
            trainingMapPrevious = trainingMapLabel.transform.parent.parent.Find("TrainingMapPrevious").GetComponent<UnityEngine.UI.Button>();
            trainingMapNext = trainingMapLabel.transform.parent.parent.Find("TrainingMapNext").GetComponent<UnityEngine.UI.Button>();

            trainingBackButton = factory.Button(panel, "TrainingBackButton", "VOLVER", BackFromTraining, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Back);
            Anchor((RectTransform)trainingBackButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 40f), new Vector2(250f, 60f));
            trainingBackLabel = trainingBackButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            trainingStatus = factory.Text(panel, "Status", string.Empty, 19f, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            Anchor(trainingStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(318f, 36f), new Vector2(-362f, 68f));
            UpdateTrainingMapView();
        }

        private UnityEngine.UI.Button BuildTrainingRoleCard(Transform parent, AlfaRole role, out TextMeshProUGUI startLabel)
        {
            var human = role == AlfaRole.Human;
            var team = human ? AlfaUiTheme.TeamHuman : AlfaUiTheme.TeamMosquito;
            var card = factory.Panel(parent, human ? "HumanCard" : "MosquitoCard", Color.white);
            AlfaUiFactory.SetSurface(card,
                human ? AlfaUiTheme.Hex("1D4B86") : AlfaUiTheme.Hex("5E2231"),
                human ? AlfaUiTheme.Hex("0D2445") : AlfaUiTheme.Hex("2A1426"),
                AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.25f), 0.9f));
            var cardTitle = factory.Title(card, "CardTitle", human ? "ENTRENAR COMO HUMANO" : "ENTRENAR COMO MOSQUITO", 34f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Top);
            cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-32f, 46f));

            var portraitFrame = factory.Inset(card, "PortraitFrame", -1f, AlfaUiTheme.ButtonRadius);
            AlfaUiFactory.SetSurface(portraitFrame, AlfaUiTheme.WithAlpha(Color.white, 0.55f), Color.white, AlfaUiTheme.WithAlpha(team, 0.35f));
            Anchor(portraitFrame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(-40f, 300f));
            var portrait = LoadRolePortrait(role);
            if (portrait != null)
            {
                var image = AlfaUiFactory.Node("Portrait", portraitFrame, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                image.sprite = portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
                AlfaUiFactory.Fill(image.rectTransform, 8f, 8f, 8f, 8f);
            }
            else
            {
                var glow = AlfaUiFactory.Node("PortraitGlow", portraitFrame, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                glow.sprite = AlfaUiSkin.Circle();
                glow.color = AlfaUiTheme.WithAlpha(team, 0.22f);
                glow.raycastTarget = false;
                Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 250f));
                var symbol = factory.Icon(portraitFrame, "PortraitIcon", human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito,
                    Color.Lerp(team, Color.white, 0.35f));
                Anchor(symbol.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            }

            var description = factory.Text(card, "Description", human ? "APRENDÉ A DEFENDERTE\nRECORRÉ EL MAPA Y USÁ OBJETOS" :
                "PRACTICÁ EL VUELO\nEXPLORÁ Y MOLESTÁ SIN LÍMITES", 19f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            description.characterSpacing = 0.8f;
            description.lineSpacing = 8f;
            Anchor(description.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 106f), new Vector2(-40f, 70f));

            var start = factory.Button(card, human ? "TrainingHumanButton" : "TrainingMosquitoButton", "INICIAR",
                () => StartTrainingIntent(role, false), human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger, 72f, AlfaUiIconKind.Play);
            factory.ComicLabel(start, 40f);
            startLabel = start.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            Anchor((RectTransform)start.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(-48f, 72f));
            return start;
        }

        /// <summary>
        /// Role portrait for the training cards. Another team renders illustrations to
        /// Resources/AlfaUiPortraits/Human and Resources/AlfaUiPortraits/Mosquito (PNG imported as Sprite, or as
        /// a plain texture). Until they exist the card shows the large role pictogram instead.
        /// </summary>
        internal static Sprite LoadRolePortrait(AlfaRole role)
        {
            var path = "AlfaUiPortraits/" + (role == AlfaRole.Human ? "Human" : "Mosquito");
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildCustomization(AlfaUiDependencies dependencies)
        {
            var view = factory.View("CustomizationView", transform, false);
            SetSceneScrim(view, 0.72f);
            screens[AlfaUiScreen.Customization] = view;
            var safe = factory.SafeArea(view.transform, 48f, 48f, 40f, 40f);
            var columns = factory.Horizontal(safe, "Columns", 28f, TextAnchor.MiddleCenter);
            customizationControlsGroup = columns.gameObject.AddComponent<CanvasGroup>();
            AlfaUiFactory.Fill(columns);
            var previewPanel = factory.Panel(columns, "PreviewPanel", AlfaUiTheme.Night800, 900f, 900f);
            previewPanel.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            var previewViewport = AlfaUiFactory.Node("PreviewViewport", previewPanel);
            AlfaUiFactory.Fill(previewViewport.GetComponent<RectTransform>(), 20f, 20f, 108f, 104f);
            var rawNode = AlfaUiFactory.Node("CharacterPreview", previewViewport.transform, typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.AspectRatioFitter), typeof(CharacterPreviewOrbit));
            AlfaUiFactory.Fill(rawNode.GetComponent<RectTransform>());
            var raw = rawNode.GetComponent<UnityEngine.UI.RawImage>();
            raw.color = Color.white;
            var previewAspect = rawNode.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            previewAspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            previewAspect.aspectRatio = dependencies.Preview?.Texture != null && dependencies.Preview.Texture.height > 0 ?
                (float)dependencies.Preview.Texture.width / dependencies.Preview.Texture.height : 1f;
            previewOrbit = rawNode.GetComponent<CharacterPreviewOrbit>();
            previewOrbit.Initialize(raw, dependencies.Preview);
            var stageHeader = factory.Panel(previewPanel, "StageHeader",
                new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.9f));
            Anchor(stageHeader, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, 68f));
            customizationPreviewIcon = factory.Icon(stageHeader, "StageMark", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            Anchor(customizationPreviewIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(40f, 40f));
            customizationPreviewTitle = factory.Text(stageHeader, "Title", "VISTA EN VIVO · HUMANO", 21f,
                AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            AlfaUiFactory.Fill(customizationPreviewTitle.rectTransform, 72f, 18f, 8f, 30f);
            var orbitHint = factory.Text(stageHeader, "OrbitHint", "ARRASTRÁ PARA GIRAR · RUEDA PARA ZOOM", 16f,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            AlfaUiFactory.Fill(orbitHint.rectTransform, 72f, 18f, 40f, 6f);
            var unavailable = factory.Text(previewPanel, "PreviewUnavailable", "El visor 3D se conecta al personaje del juego.", AlfaUiTheme.BodySize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            Anchor(unavailable.rectTransform, new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            unavailable.gameObject.SetActive(!previewOrbit.IsBound);
            var angles = factory.Horizontal(previewPanel, "PreviewAngles", 8f, TextAnchor.MiddleCenter);
            Anchor(angles, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, 66f));
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewFrontButton", "FRENTE", () => previewOrbit.SetAngle(PreviewAngle.Front), false, false, 66f), 20f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewSideButton", "PERFIL", () => previewOrbit.SetAngle(PreviewAngle.Side), false, false, 66f), 20f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewBackButton", "ESPALDA", () => previewOrbit.SetAngle(PreviewAngle.Back), false, false, 66f), 20f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewResetButton", "CENTRAR", () => previewOrbit.ResetView(), false, false, 66f), 20f);

            var optionsPanel = factory.Panel(columns, "OptionsPanel", AlfaUiTheme.Night700, 720f, 900f);
            var content = factory.Vertical(optionsPanel, "Content", 8f);
            AlfaUiFactory.Fill(content, 20f, 20f, 20f, 192f);
            factory.SectionHeader(content, "Header", "PERSONALIZAR", AlfaUiIconKind.Customize, AlfaUiTheme.Lamp400);
            var roleRow = factory.Horizontal(content, "RoleTabs", 10f, TextAnchor.MiddleCenter);
            customizationHumanButton = factory.FeatureButton(roleRow, "CustomizationHumanButton", "HUMANO", "PIJAMA Y GORRO",
                () => SetCustomizationRole(AlfaRole.Human), AlfaUiIconKind.Human, true, false, 76f);
            customizationMosquitoButton = factory.FeatureButton(roleRow, "CustomizationMosquitoButton", "MOSQUITO", "COLOR DEL CUERPO",
                () => SetCustomizationRole(AlfaRole.Mosquito), AlfaUiIconKind.Mosquito, false, false, 76f);
            customizationCategoryTitle = factory.Text(content, "CategoryTitle", "PALETA DEL HUMANO", AlfaUiTheme.LabelSize,
                AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            factory.Divider(content, "CategoryDivider", new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.52f));
            humanCustomizationFields = factory.Vertical(content, "HumanFields", 8f).gameObject;
            factory.Caption(humanCustomizationFields.transform, "SkinLabel", "TONO DE PIEL");
            humanPaletteRoot = CreatePaletteLayout(humanCustomizationFields.transform, "SkinPalette");
            factory.Caption(humanCustomizationFields.transform, "PajamaLabel", "COLOR DE PIJAMA");
            pajamaPaletteRoot = CreatePaletteLayout(humanCustomizationFields.transform, "PajamaPalette");
            factory.Text(humanCustomizationFields.transform, "DefaultClothes", "Predeterminado: pijama, pantuflas y gorro de noche.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            mosquitoCustomizationFields = factory.Vertical(content, "MosquitoFields", 10f).gameObject;
            factory.Caption(mosquitoCustomizationFields.transform, "MosquitoColorLabel", "COLOR DE CUERPO");
            mosquitoPaletteRoot = CreatePaletteLayout(mosquitoCustomizationFields.transform, "MosquitoPalette");
            modularCustomizationFields = factory.Vertical(content, "ModularFields", 8f).gameObject;
            factory.Caption(modularCustomizationFields.transform, "CategoryLabel", "CATEGORÍAS");
            factory.ScrollView(modularCustomizationFields.transform, "CategoryScroll", out modularCategoryRoot, 134f);
            factory.Caption(modularCustomizationFields.transform, "OptionsLabel", "OPCIONES");
            factory.ScrollView(modularCustomizationFields.transform, "OptionsScroll", out modularOptionRoot, 210f);
            modularCustomizationFields.SetActive(false);
            customizationStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            var footer = factory.Vertical(optionsPanel, "Actions", 10f);
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, 148f));
            customizationSaveButton = factory.Button(footer, "CustomizationSaveButton", "APLICAR", SaveCustomization, true, false, 72f, AlfaUiIconKind.Ready);
            ApplyPositiveStyle(customizationSaveButton);
            customizationSaveLabel = customizationSaveButton.GetComponentInChildren<TextMeshProUGUI>();
            var secondary = factory.Horizontal(footer, "SecondaryActions", 12f);
            customizationResetButton = factory.Button(secondary, "CustomizationResetButton", "DESHACER CAMBIOS", ResetCustomization, false, false, 66f);
            AlfaUiFactory.QuietButton(customizationResetButton, 20f);
            AlfaUiFactory.QuietButton(factory.Button(secondary, "CustomizationBackButton", "VOLVER", CloseCustomization, false, false, 66f, AlfaUiIconKind.Back), 20f);
        }

        private void BuildSettings()
        {
            var view = factory.View("SettingsView", transform, false);
            SetSceneScrim(view, 0.78f);
            screens[AlfaUiScreen.Settings] = view;
            var panel = CenteredPanel(view.transform, "SettingsCard", 1280f, 990f);
            panel.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night800;
            var content = factory.Vertical(panel, "Content", 12f);
            settingsControlsGroup = content.gameObject.AddComponent<CanvasGroup>();
            AlfaUiFactory.Fill(content, 30f, 30f, 26f, 26f);
            factory.SectionHeader(content, "Header", "AJUSTES", AlfaUiIconKind.Settings, AlfaUiTheme.Sky400);
            factory.Caption(content, "Intro", "AJUSTÁ SONIDO, IMAGEN Y CONTROLES");

            var sections = factory.Horizontal(content, "Sections", 18f, TextAnchor.UpperCenter);
            var sectionsLayout = sections.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            sectionsLayout.preferredHeight = 712f;
            sectionsLayout.flexibleHeight = 0f;

            var leftColumn = factory.Vertical(sections, "LeftColumn", 16f);
            var leftLayout = leftColumn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leftLayout.preferredWidth = 570f;
            leftLayout.flexibleWidth = 1f;

            var audioPanel = factory.Panel(leftColumn, "AudioPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), -1f, 384f);
            var audio = factory.Vertical(audioPanel, "AudioContent", 10f);
            AlfaUiFactory.Fill(audio, 22f, 22f, 20f, 20f);
            factory.SectionHeader(audio, "AudioHeader", "AUDIO", AlfaUiIconKind.Audio, AlfaUiTheme.Lamp400);
            masterVolume = AddSliderField(audio, "VOLUMEN GENERAL", "MasterVolumeSlider", value => ChangeSetting(draft => draft.MasterVolume = value));
            musicVolume = AddSliderField(audio, "MÚSICA", "MusicVolumeSlider", value => ChangeSetting(draft => draft.MusicVolume = value));
            effectsVolume = AddSliderField(audio, "EFECTOS", "EffectsVolumeSlider", value => ChangeSetting(draft => draft.EffectsVolume = value));
            voiceVolume = AddSliderField(audio, "VOCES", "VoiceVolumeSlider", value => ChangeSetting(draft => draft.VoiceVolume = value));
            voiceDeviceDropdown = factory.Dropdown(audio, "VoiceDeviceDropdown", new[] { "ELEGÍ UN MICRÓFONO" }, index =>
            {
                string device = index > 0 && settingsState != null && index - 1 < settingsState.VoiceDevices.Count ? settingsState.VoiceDevices[index - 1] : string.Empty;
                ChangeSetting(draft => draft.VoiceDevice = device);
            });

            var controlsPanel = factory.Panel(leftColumn, "ControlsPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), -1f, 312f);
            var controls = factory.Vertical(controlsPanel, "ControlsContent", 10f);
            AlfaUiFactory.Fill(controls, 22f, 22f, 20f, 20f);
            factory.SectionHeader(controls, "ControlsHeader", "CONTROLES", AlfaUiIconKind.Controls, AlfaUiTheme.Mint400);
            humanSensitivity = AddSliderField(controls, "SENSIBILIDAD HUMANO", "HumanSensitivitySlider", value => ChangeSetting(draft => draft.HumanSensitivity = value), 0.1f, 2f);
            mosquitoSensitivity = AddSliderField(controls, "SENSIBILIDAD MOSQUITO", "MosquitoSensitivitySlider", value => ChangeSetting(draft => draft.MosquitoSensitivity = value), 0.1f, 2f);
            invertY = factory.Toggle(controls, "InvertYToggle", "INVERTIR EJE VERTICAL", value => ChangeSetting(draft => draft.InvertY = value));
            var pttButton = factory.Button(controls, "PushToTalkRebindButton", "PTT · V", () =>
                (actions as IVoiceActions)?.BeginPushToTalkRebind((path, label) =>
                {
                    if (settingsDraft != null) settingsDraft.PushToTalkBinding = path;
                    if (pushToTalkBindingLabel != null) pushToTalkBindingLabel.text = "PTT · " + label;
                }), false, false, 44f, AlfaUiIconKind.Audio);
            pushToTalkBindingLabel = pttButton.GetComponentInChildren<TextMeshProUGUI>();
            rebindNote = pttButton.gameObject;

            var videoPanel = factory.Panel(sections, "VideoPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), 570f, 712f);
            videoPanel.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            videoSettings = videoPanel.gameObject;
            var video = factory.Vertical(videoPanel, "VideoContent", 10f);
            AlfaUiFactory.Fill(video, 22f, 22f, 20f, 20f);
            factory.SectionHeader(video, "VideoHeader", "VIDEO", AlfaUiIconKind.Video, AlfaUiTheme.Sky400);
            fullScreen = factory.Toggle(video, "FullScreenToggle", "PANTALLA COMPLETA", value => ChangeSetting(draft => draft.FullScreen = value));
            resolutionValue = AddCycleField(video, "RESOLUCIÓN", "Resolution", -1, 1, delta =>
                ChangeSetting(draft => draft.ResolutionIndex = Cycle(draft.ResolutionIndex, delta, settingsState?.Resolutions.Count ?? 0)), 150f, 200f);
            qualityValue = AddCycleField(video, "CALIDAD", "Quality", -1, 1, delta =>
                ChangeSetting(draft => draft.QualityIndex = Cycle(draft.QualityIndex, delta, settingsState?.Qualities.Count ?? 0)), 150f, 200f);
            vSync = factory.Toggle(video, "VSyncToggle", "SINCRONIZACIÓN VERTICAL", value => ChangeSetting(draft => draft.VSync = value));
            factory.Caption(video, "FrameLimitLabel", "LÍMITE DE FPS");
            frameLimitDropdown = factory.Dropdown(video, "FrameLimitDropdown",
                FrameLimitOptions.Select(FrameLimitLabel).ToArray(), index =>
                    ChangeSetting(draft => draft.FrameLimit = FrameLimitOptions[Mathf.Clamp(index, 0, FrameLimitOptions.Length - 1)]));
            reduceMenuMotion = factory.Toggle(video, "ReduceMenuMotionToggle", "REDUCIR MOVIMIENTO DEL MENÚ",
                value => ChangeSetting(draft => draft.ReduceMenuMotion = value));
            // The backend opts in only after persistence and the real scene effect are wired.
            reduceMenuMotion.transform.parent.gameObject.SetActive(false);
            settingsStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            var buttons = factory.Horizontal(content, "Actions", 12f, TextAnchor.MiddleCenter);
            settingsApplyButton = factory.Button(buttons, "SettingsApplyButton", "APLICAR", ApplySettings, true, false, 66f, AlfaUiIconKind.Ready);
            ApplyPositiveStyle(settingsApplyButton);
            settingsApplyLabel = settingsApplyButton.GetComponentInChildren<TextMeshProUGUI>();
            AlfaUiFactory.QuietButton(factory.Button(buttons, "SettingsResetButton", "DESHACER CAMBIOS", ResetSettings, false, false, 66f), 20f);
            AlfaUiFactory.QuietButton(factory.Button(buttons, "SettingsBackButton", "VOLVER", CloseSettings, false, false, 66f, AlfaUiIconKind.Back), 20f);
        }

        private void BuildHud()
        {
            var view = factory.View("GameplayHudView", transform, false);
            screens[AlfaUiScreen.Gameplay] = view;
            var role = factory.Panel(view.transform, "RoleBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.78f));
            Anchor(role, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(178f, 50f));
            hudRoleBackground = role.GetComponent<UnityEngine.UI.Image>();
            hudRoleIcon = factory.Icon(role, "RoleIcon", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            Anchor(hudRoleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(30f, 30f));
            hudRoleLabel = factory.Text(role, "RoleLabel", "HUMANO", 16f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudRoleLabel.rectTransform, 46f, 10f, 6f, 6f);

            var clock = factory.Panel(view.transform, "ClockBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -22f), new Vector2(150f, 58f));
            var clockIcon = factory.Icon(clock, "ClockIcon", AlfaUiIconKind.Clock, AlfaUiTheme.Lamp400);
            Anchor(clockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(28f, 28f));
            hudClock = factory.Text(clock, "Clock", "03:00", 25f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudClock.rectTransform, 44f, 10f, 6f, 6f);

            var blood = factory.Panel(view.transform, "BloodBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(blood, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(6f, -22f), new Vector2(248f, 58f));
            var bloodIcon = factory.Icon(blood, "BloodIcon", AlfaUiIconKind.Blood, AlfaUiTheme.Pajama500); hudScoreIcon = bloodIcon;
            Anchor(bloodIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(30f, 30f));
            hudBlood = factory.Text(blood, "Blood", "SANGRE  0 / 20", 18f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudBlood.rectTransform, 50f, 14f, 5f, 17f);
            var bloodTrack = AlfaUiFactory.Node("BloodTrack", blood, typeof(UnityEngine.UI.Image));
            var bloodTrackImage = bloodTrack.GetComponent<UnityEngine.UI.Image>();
            bloodTrackImage.color = AlfaUiTheme.Night600;
            bloodTrackImage.raycastTarget = false;
            Anchor(bloodTrack.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(14f, 9f), new Vector2(-28f, 7f));
            var bloodFill = AlfaUiFactory.Node("Fill", bloodTrack.transform, typeof(UnityEngine.UI.Image));
            hudBloodFill = bloodFill.GetComponent<UnityEngine.UI.Image>();
            hudBloodFill.color = AlfaUiTheme.Pajama500;
            hudBloodFill.raycastTarget = false;
            AlfaUiFactory.Fill(hudBloodFill.rectTransform);
            hudBloodFill.rectTransform.anchorMax = new Vector2(0f, 1f);

            hudNetwork = factory.Text(view.transform, "NetworkState", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Pajama500, TextAlignmentOptions.Right);
            Anchor(hudNetwork.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(420f, 56f));
            hudVoice = factory.Text(view.transform, "VoiceState", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Right, true);
            Anchor(hudVoice.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -78f), new Vector2(420f, 42f));

            hudPromptPanel = factory.Panel(view.transform, "InteractionPrompt", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f)).gameObject;
            // Keep a deliberate gutter before the right-aligned equipment belt at 720p.
            Anchor(hudPromptPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(440f, 50f));
            hudInteraction = factory.Text(hudPromptPanel.transform, "Interaction", string.Empty, 20f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudInteraction.rectTransform, 18f, 18f, 8f, 8f);

            hudStatePanel = factory.Panel(view.transform, "ActorStatePanel", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.9f)).gameObject;
            Anchor(hudStatePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 102f), new Vector2(430f, 62f));
            hudActorState = factory.Text(hudStatePanel.transform, "ActorState", string.Empty, 21f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudActorState.rectTransform, 18f, 18f, 6f, 20f);
            var progressRoot = AlfaUiFactory.Node("StateProgress", hudStatePanel.transform, typeof(UnityEngine.UI.Image));
            var progressTrack = progressRoot.GetComponent<UnityEngine.UI.Image>();
            progressTrack.color = AlfaUiTheme.Night600;
            progressTrack.raycastTarget = false;
            Anchor(progressRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(14f, 10f), new Vector2(-28f, 8f));
            var fill = AlfaUiFactory.Node("Fill", progressRoot.transform, typeof(UnityEngine.UI.Image));
            hudProgress = fill.GetComponent<UnityEngine.UI.Image>();
            hudProgress.color = AlfaUiTheme.Lamp400;
            hudProgress.raycastTarget = false;
            AlfaUiFactory.Fill(hudProgress.rectTransform);

            hudHintPanel = factory.Panel(view.transform, "ContextHintPanel", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.72f)).gameObject;
            // Compact two-line context stays left of the central interaction prompt at both target resolutions.
            Anchor(hudHintPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(540f, 64f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, 18f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            hudHint.textWrappingMode = TextWrappingModes.Normal;
            hudHint.enableAutoSizing = false;
            hudHint.overflowMode = TextOverflowModes.Overflow;
            AlfaUiFactory.Fill(hudHint.rectTransform, 16f, 16f, 10f, 10f);
            hudLives = factory.Text(view.transform, "Lives", string.Empty, 20f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            Anchor(hudLives.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -82f), new Vector2(260f, 32f));
            hudTaskPanel = factory.Panel(view.transform, "PrivateTask", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, .9f)).gameObject;
            Anchor(hudTaskPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -104f), new Vector2(400f, 112f));
            hudTask = factory.Text(hudTaskPanel.transform, "PrivateTaskText", string.Empty, 18f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            hudTask.textWrappingMode = TextWrappingModes.Normal;
            AlfaUiFactory.Fill(hudTask.rectTransform, 18f, 18f, 12f, 28f);
            var taskTrack = AlfaUiFactory.Node("TaskProgress", hudTaskPanel.transform, typeof(UnityEngine.UI.Image));
            taskTrack.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            Anchor(taskTrack.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(.5f, 0f), new Vector2(18f, 12f), new Vector2(-36f, 8f));
            hudTaskFill = AlfaUiFactory.Node("Fill", taskTrack.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudTaskFill.color = AlfaUiTheme.Mint400; hudTaskFill.raycastTarget = false; AlfaUiFactory.Fill(hudTaskFill.rectTransform);
            hudTaskPanel.SetActive(false);

            hudEquipmentPanel = factory.Panel(view.transform, "PrivateEquipment", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, .80f)).gameObject;
            hudEquipmentRect = hudEquipmentPanel.GetComponent<RectTransform>();
            Anchor(hudEquipmentRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 26f), new Vector2(700f, 176f));
            for (int i = 0; i < 4; i++)
            {
                var slot = factory.Panel(hudEquipmentPanel.transform, "EquipmentSlotPlate" + i,
                    new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, .78f));
                Anchor(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f + i * 167f, -10f), new Vector2(164f, 100f));
                AlfaUiFactory.SetSurface(slot, shadow: Color.clear);
                hudEquipmentIcons[i] = factory.Icon(slot, "EquipmentIcon" + i, i == 0 ? AlfaUiIconKind.Hands : AlfaUiIconKind.None, AlfaUiTheme.Moon200);
                Anchor(hudEquipmentIcons[i].rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(7f, 0f), new Vector2(22f, 22f));
                hudEquipmentLabels[i] = factory.Text(slot, "EquipmentSlot" + i, i == 0 ? ">  0  MANOS" : i + "  VACÍO", 18f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
                hudEquipmentLabels[i].textWrappingMode = TextWrappingModes.Normal;
                hudEquipmentLabels[i].overflowMode = TextOverflowModes.Overflow;
                AlfaUiFactory.Fill(hudEquipmentLabels[i].rectTransform, 30f, 6f, 6f, 6f);
            }
            hudStaminaLabel = factory.Text(hudEquipmentPanel.transform, "StaminaLabel", "ESTAMINA  100%", 18f, AlfaUiTheme.Mint400, TextAlignmentOptions.Left, true);
            hudStaminaLabelRect = hudStaminaLabel.rectTransform;
            Anchor(hudStaminaLabelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -116f), new Vector2(-28f, 34f));
            var staminaTrack = AlfaUiFactory.Node("StaminaTrack", hudEquipmentPanel.transform, typeof(UnityEngine.UI.Image));
            staminaTrack.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            hudStaminaTrackRect = staminaTrack.GetComponent<RectTransform>();
            Anchor(hudStaminaTrackRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -154f), new Vector2(-28f, 8f));
            hudStaminaFill = AlfaUiFactory.Node("Fill", staminaTrack.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudStaminaFill.color = AlfaUiTheme.Mint400; hudStaminaFill.raycastTarget = false; AlfaUiFactory.Fill(hudStaminaFill.rectTransform);
            hudThrowTrack = AlfaUiFactory.Node("ThrowCharge", hudEquipmentPanel.transform).gameObject;
            hudThrowRect = hudThrowTrack.GetComponent<RectTransform>();
            Anchor(hudThrowRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -168f), new Vector2(-28f, 50f));
            hudThrowLabel = factory.Text(hudThrowTrack.transform, "ThrowLabel", "CARGA PANTUFLA", 18f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            Anchor(hudThrowLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 34f));
            var throwBar = AlfaUiFactory.Node("Track", hudThrowTrack.transform, typeof(UnityEngine.UI.Image));
            throwBar.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            Anchor(throwBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(0f, 8f));
            hudThrowFill = AlfaUiFactory.Node("Fill", throwBar.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudThrowFill.color = AlfaUiTheme.Lamp400; hudThrowFill.raycastTarget = false; AlfaUiFactory.Fill(hudThrowFill.rectTransform);
            hudSwapOffer = factory.Text(hudEquipmentPanel.transform, "SwapOffer", string.Empty, 18f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Left, true);
            hudSwapOffer.textWrappingMode = TextWrappingModes.Normal;
            hudSwapOffer.overflowMode = TextOverflowModes.Overflow;
            hudSwapRect = hudSwapOffer.rectTransform;
            Anchor(hudSwapRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -168f), new Vector2(-28f, 60f));
            hudEquipmentPanel.SetActive(false);
            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100); hudReticle = reticle.gameObject;
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        private void UpdateEquipmentLayout(bool charging, bool hasSwapOffer)
        {
            var nextTop = 168f;
            if (charging)
            {
                Anchor(hudThrowRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f),
                    new Vector2(0f, -nextTop), new Vector2(-28f, 50f));
                nextTop += 56f;
            }
            if (hasSwapOffer)
            {
                Anchor(hudSwapRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f),
                    new Vector2(0f, -nextTop), new Vector2(-28f, 60f));
                nextTop += 66f;
            }
            hudEquipmentRect.sizeDelta = new Vector2(700f, charging || hasSwapOffer ? nextTop + 4f : 176f);
        }

        private void BuildPause()
        {
            var view = factory.View("PauseView", transform, false);
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Pause] = view;
            var panel = CenteredPanel(view.transform, "PauseCard", 720f, 760f);
            var content = factory.Vertical(panel, "Content", 10f);
            AlfaUiFactory.Fill(content, 34f, 34f, 30f, 30f);
            factory.Title(content, "Title", "PAUSA", 58f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            factory.Button(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, true, false, 68f, AlfaUiIconKind.Play);
            factory.Button(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause), false, false, 58f, AlfaUiIconKind.Settings);
            factory.Button(content, "PauseControlsButton", "CONTROLES", () => OpenSettings(AlfaUiScreen.Pause), false, false, 58f, AlfaUiIconKind.Training);
            pauseVoiceStatus = factory.Text(content, "PauseVoiceStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            pauseVoiceMuteButton = factory.Button(content, "PauseVoiceMuteButton", "SILENCIAR MI MICRÓFONO", () =>
                (actions as IVoiceActions)?.SetLocalVoiceMuted(!voiceState.LocalMuted), false, false, 48f, AlfaUiIconKind.Audio);
            pauseVoiceMuteLabel = pauseVoiceMuteButton.GetComponentInChildren<TextMeshProUGUI>();
            pauseVoicePeers = factory.Vertical(content, "PauseVoicePeers", 4f);
            var voicePeersLayout = pauseVoicePeers.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            voicePeersLayout.preferredHeight = 190f; voicePeersLayout.flexibleHeight = 1f;
            pauseLeaveButton = factory.Button(content, "PauseLeaveButton", "SALIR DE LA SALA", LeaveGameplayContext, false, true, 58f, AlfaUiIconKind.Exit);
            pauseLeaveLabel = pauseLeaveButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildResults()
        {
            var view = factory.View("ResultsView", transform, false);
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Results] = view;
            var panel = CenteredPanel(view.transform, "ResultsCard", 780f, 580f);
            var content = factory.Vertical(panel, "Content", 22f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(content, 40f, 40f, 34f, 34f);
            resultsTitle = factory.Title(content, "Title", "RONDA INTERRUMPIDA", 58f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
            resultsStats = factory.Text(content, "Stats", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            resultsPrimary = factory.Button(content, "ResultsPrimaryButton", "VOLVER AL LOBBY", ResultsPrimaryAction, true, false, 68f, AlfaUiIconKind.Play);
            resultsPrimaryLabel = resultsPrimary.GetComponentInChildren<TextMeshProUGUI>();
            resultsLeave = factory.Button(content, "ResultsLeaveButton", "SALIR DE LA SALA", ResultsLeaveAction, false, true, 58f, AlfaUiIconKind.Exit);
        }

        private void BuildConfirm()
        {
            confirmModal = factory.View("ConfirmModal", transform, false);
            confirmModal.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            confirmModal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var panel = CenteredPanel(confirmModal.transform, "ConfirmCard", 640f, 330f);
            var content = factory.Vertical(panel, "Content", 22f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(content, 34f, 34f, 30f, 30f);
            confirmTitle = factory.Title(content, "Title", "CONFIRMAR", 42f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            confirmBody = factory.Text(content, "Body", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            var buttons = factory.Horizontal(content, "Actions", 12f, TextAnchor.MiddleCenter);
            confirmSafe = factory.Button(buttons, "ConfirmSafeButton", "VOLVER", CloseConfirm, true);
            confirmSafeLabel = confirmSafe.GetComponentInChildren<TextMeshProUGUI>();
            confirmDanger = factory.Button(buttons, "ConfirmDangerButton", "CONFIRMAR", ConfirmDanger, false, true);
            confirmDangerLabel = confirmDanger.GetComponentInChildren<TextMeshProUGUI>();
            confirmModal.SetActive(false);
        }

        private RectTransform CenteredPanel(Transform parent, string name, float width, float height)
        {
            var panel = factory.Panel(parent, name, AlfaUiTheme.Night700, width, height);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            return panel;
        }

        private void AddReadOnlyField(Transform parent, string label, string value)
        {
            var panel = factory.Panel(parent, label + "Field", AlfaUiTheme.Ink900, -1f, 62f);
            var row = factory.Horizontal(panel, "Content", 12f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(row, 16f, 16f, 8f, 8f);
            factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Disabled);
            factory.Text(row, "Value", value, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Right);
        }

        private UnityEngine.UI.Slider AddSliderField(Transform parent, string label, string name, UnityEngine.Events.UnityAction<float> callback, float min = 0f, float max = 1f)
        {
            var row = factory.Horizontal(parent, name + "Row", 18f, TextAnchor.MiddleCenter);
            var labelText = factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
            var labelLayout = labelText.GetComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.minWidth = 236f;
            labelLayout.preferredWidth = 236f;
            labelLayout.flexibleWidth = 0f;
            var slider = factory.Slider(row, name, min, max, callback);
            slider.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 200f;
            var valueText = factory.Text(row, "Value", string.Empty, 16f, AlfaUiTheme.Moon200, TextAlignmentOptions.Right);
            var valueLayout = valueText.GetComponent<UnityEngine.UI.LayoutElement>();
            valueLayout.minWidth = 56f;
            valueLayout.preferredWidth = 56f;
            valueLayout.flexibleWidth = 0f;
            slider.onValueChanged.AddListener(_ => UpdateSliderValue(slider));
            UpdateSliderValue(slider);
            return slider;
        }

        private static void UpdateSliderValue(UnityEngine.UI.Slider slider)
        {
            var valueText = slider.transform.parent.Find("Value").GetComponent<TextMeshProUGUI>();
            valueText.text = Mathf.Approximately(slider.maxValue, 1f) ?
                Mathf.RoundToInt(slider.value * 100f) + "%" : slider.value.ToString("0.00") + "×";
        }

        private TextMeshProUGUI AddCycleField(Transform parent, string label, string name, int previous, int next, Action<int> changed,
            float labelWidth = 118f, float valueWidth = 220f)
        {
            // Label, then chevron buttons around an inset value well (UI-06 "‹ valor ›" rows). Chevrons are icons:
            // the shipped fonts have no arrow glyphs.
            var row = factory.Horizontal(parent, name + "Row", 8f, TextAnchor.MiddleLeft);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            var caption = factory.Caption(row, "Label", label);
            var labelLayout = caption.GetComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.minWidth = labelLayout.preferredWidth = labelWidth;
            labelLayout.flexibleWidth = 0f;
            CycleButton(row, name + "Previous", AlfaUiIconKind.ChevronLeft, () => changed(previous));
            var well = factory.Inset(row, name + "Well", 52f);
            var wellLayout = well.GetComponent<UnityEngine.UI.LayoutElement>();
            wellLayout.minWidth = 120f;
            wellLayout.preferredWidth = valueWidth;
            wellLayout.flexibleWidth = 1f;
            var value = factory.Text(well, name + "Value", "—", 20f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            value.characterSpacing = 0.4f;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(value.rectTransform, 10f, 10f, 4f, 4f);
            CycleButton(row, name + "Next", AlfaUiIconKind.ChevronRight, () => changed(next));
            return value;
        }

        private UnityEngine.UI.Button CycleButton(Transform row, string name, AlfaUiIconKind icon, UnityEngine.Events.UnityAction callback)
        {
            var button = factory.Button(row, name, string.Empty, callback, AlfaButtonStyle.Tab, 52f, icon);
            var layout = button.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = layout.preferredWidth = 52f;
            layout.flexibleWidth = 0f;
            var plate = button.transform.Find("IconPlate") as RectTransform;
            if (plate != null)
            {
                plate.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 0.5f);
                plate.anchoredPosition = Vector2.zero;
                plate.sizeDelta = new Vector2(30f, 30f);
            }
            return button;
        }

        private void ConfigureOnlineForm(string rememberedName)
        {
            playerNameInput.SetTextWithoutNotify(rememberedName ?? string.Empty);
            onlineFormTitle.text = "JUGAR ONLINE";
            onlinePrimaryLabel.text = createMode ? "CREAR SALA" : "UNIRME A LA SALA";
            roomCodeRow.SetActive(!createMode);
            onlineCodePreviewRow.SetActive(createMode);
            AlfaUiFactory.ApplyStyle(onlineCreateTab, createMode ? AlfaButtonStyle.Primary : AlfaButtonStyle.Tab);
            AlfaUiFactory.ApplyStyle(onlineJoinTab, createMode ? AlfaButtonStyle.Tab : AlfaButtonStyle.Primary);
            onlineInfoTitle.text = createMode ? "TU SALA PRIVADA" : "CÓMO UNIRTE";
            if (createMode)
            {
                SetOnlineInfoRow(0, AlfaUiIconKind.Lock, "SALA PRIVADA", "Solo entra quien tenga el código", AlfaUiTheme.StatusOk);
                SetOnlineInfoRow(1, AlfaUiIconKind.Online, "HASTA " + LetMeSleep.Core.RoomRules.Capacity + " JUGADORES", "Invitá a tus amigos con el código", AlfaUiTheme.StatusOk);
                SetOnlineInfoRow(2, AlfaUiIconKind.Map, "MAPA Y MODO", "Los elegís adentro de la sala", AlfaUiTheme.StatusOk);
                SetOnlineInfoRow(3, AlfaUiIconKind.Mosquito, "ROLES AL AZAR", "Humanos o mosquitos en cada ronda", AlfaUiTheme.StatusOk);
            }
            else
            {
                SetOnlineInfoRow(0, AlfaUiIconKind.Key, "PEDÍ EL CÓDIGO", "Te lo pasa quien creó la sala", AlfaUiTheme.Sky400);
                SetOnlineInfoRow(1, AlfaUiIconKind.Copy, "PEGALO ACÁ", "Con PEGAR o con Ctrl+V", AlfaUiTheme.Sky400);
                SetOnlineInfoRow(2, AlfaUiIconKind.Enter, "ENTRÁ A LA SALA", "Marcá LISTO y esperá la ronda", AlfaUiTheme.Sky400);
                SetOnlineInfoRow(3, AlfaUiIconKind.Wifi, "CONEXIÓN ONLINE", "Necesitás Internet para jugar", AlfaUiTheme.Sky400);
            }
            PresentOnline(new OnlineUiState());
        }

        private void SetOnlineInfoRow(int index, AlfaUiIconKind icon, string title, string subtitle, Color status)
        {
            if (onlineInfoRows == null || index < 0 || index >= onlineInfoRows.Length) return;
            var row = onlineInfoRows[index];
            row.Find("RowIcon").GetComponent<AlfaUiIcon>().Kind = icon;
            row.Find("RowTitle").GetComponent<TextMeshProUGUI>().text = title;
            row.Find("RowSubtitle").GetComponent<TextMeshProUGUI>().text = subtitle;
            row.Find("StatusBar").GetComponent<UnityEngine.UI.Image>().color = status;
        }

        /// <summary>
        /// Keeps the XXXXX-XXXXX grouping while typing or pasting. The caret is recomputed from the number of
        /// code characters in front of it, so the inserted separator never leaves it behind (ui-presentation-audio-1).
        /// </summary>
        private void FormatRoomCodeWhileEditing(string value)
        {
            var formatted = AlfaRoomCode.FormatForEditing(value, roomCodeInput.stringPosition, out var caret);
            if (formatted == value && caret == roomCodeInput.stringPosition) return;
            roomCodeInput.SetTextWithoutNotify(formatted);
            roomCodeInput.stringPosition = caret;
        }

        private void SubmitOnline()
        {
            if (OnlineBusy) return;
            var playerName = (playerNameInput.text ?? string.Empty).Trim();
            if (playerName.Length < 1 || playerName.Length > 24)
            {
                SetOnlineLocalError("Escribí tu nombre.", playerNameInput.gameObject);
                return;
            }
            if (createMode)
            {
                RequestFeedback(UiFeedbackKind.Confirm);
                rememberedPlayerName = playerName;
                LatchOnlineSubmission("Creando sala…");
                actions.CreateRoom(playerName);
                return;
            }
            var normalized = AlfaRoomCode.Normalize(roomCodeInput.text);
            if (!AlfaRoomCode.IsComplete(normalized))
            {
                SetOnlineLocalError("Ese código no es válido. Copialo completo.", roomCodeInput.gameObject);
                return;
            }
            roomCodeInput.SetTextWithoutNotify(AlfaRoomCode.FormatForDisplay(normalized));
            RequestFeedback(UiFeedbackKind.Confirm);
            rememberedPlayerName = playerName;
            LatchOnlineSubmission("Buscando sala…");
            actions.JoinRoom(playerName, normalized);
        }

        private void PasteRoomCode()
        {
            if (OnlineBusy) return;
            var formatted = AlfaRoomCode.FormatForDisplay(GUIUtility.systemCopyBuffer);
            roomCodeInput.SetTextWithoutNotify(formatted);
            Focus(roomCodeInput.gameObject);
            roomCodeInput.stringPosition = formatted.Length;
        }

        private void LatchOnlineSubmission(string message)
        {
            onlineSubmissionLatched = true;
            onlineCancelLatched = false;
            playerNameInput.interactable = false;
            roomCodeInput.interactable = false;
            onlinePrimaryButton.interactable = false;
            onlinePasteButton.interactable = false;
            onlineBackButton.interactable = false;
            onlineCancelButton.gameObject.SetActive(true);
            onlineCancelButton.interactable = true;
            onlineRetryButton.gameObject.SetActive(false);
            onlineCreateTab.interactable = onlineJoinTab.interactable = false;
            onlineStatus.text = message;
            onlineStatus.color = AlfaUiTheme.Moon200;
            onlineConnectingTitle.text = "CONECTANDO…";
            onlineConnectingMessage.text = message;
            UpdateOnlineOverlay();
            Focus(onlineCancelButton.gameObject);
        }

        private void RequestOnlineCancel()
        {
            if (!OnlineBusy || onlineCancelLatched) return;
            onlineCancelLatched = true;
            onlineCancelButton.interactable = false;
            onlineStatus.text = "Cancelando…";
            onlineStatus.color = AlfaUiTheme.Moon200;
            onlineConnectingMessage.text = "Cancelando…";
            actions.CancelOnline();
        }

        private void SetOnlineLocalError(string message, GameObject focus)
        {
            RequestFeedback(UiFeedbackKind.Error);
            onlineStatus.text = message;
            onlineStatus.color = AlfaUiTheme.StatusWarn;
            Focus(focus);
        }

        private void BackFromTraining()
        {
            if (TrainingBusy) RequestTrainingCancel();
            else ShowMainMenu();
        }

        private void StartTrainingIntent(AlfaRole role, bool fromResults)
        {
            if (TrainingBusy || (fromResults && resultsActionLatched)) return;
            if (!TrainingModeAvailable)
            {
                UpdateTrainingMapView();
                return;
            }
            gameplayIsTraining = true;
            trainingStartLatched = true;
            trainingCancelLatched = false;
            trainingPendingRole = role;
            if (fromResults)
            {
                resultsActionLatched = true;
                resultsPrimary.interactable = false;
                resultsPrimaryLabel.text = "PREPARANDO…";
                resultsLeave.interactable = true;
                resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = "CANCELAR";
            }
            else
            {
                trainingHumanButton.interactable = false;
                trainingMosquitoButton.interactable = false;
                UpdateTrainingStartLabels(true);
                trainingBackLabel.text = "CANCELAR";
            }
            trainingStatus.text = "Preparando entrenamiento…";
            UpdateTrainingMapView();
            actions.StartTraining(role, trainingState.ModeId, selectedTrainingMapId);
        }

        private void RequestTrainingCancel()
        {
            if (!TrainingBusy || trainingCancelLatched) return;
            trainingCancelLatched = true;
            trainingBackButton.interactable = false;
            trainingBackLabel.text = "CANCELANDO…";
            trainingStatus.text = "Cancelando entrenamiento…";
            if (screen == AlfaUiScreen.Results)
            {
                resultsLeave.interactable = false;
                resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = "CANCELANDO…";
            }
            actions.CancelTraining();
        }

        private void SetCustomizationRole(AlfaRole role)
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                modularEditedRole = role;
                modularSelectedSlotId = string.Empty;
                EnsureModularSelectedSlot();
                BuildModularCustomization();
                ResetModularScrollPositions();
                UpdateCustomizationView();
                // The selection is unchanged; this lets the provider update the local role preview only.
                SendModularPreview();
                return;
            }
            if (customizationDraft == null) return;
            customizationDraft.Role = role;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void SetCustomizationColor(string category, NamedColorOption option)
        {
            if (customizationDraft == null || option == null || customizationSaveLatched || customizationState?.IsReadOnly == true) return;
            if (category == "skin") customizationDraft.SkinColorId = option.Id;
            else if (category == "pajama") customizationDraft.PajamaColorId = option.Id;
            else customizationDraft.MosquitoColorId = option.Id;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private static CustomizationRole ToCustomizationRole(AlfaRole role) =>
            role == AlfaRole.Human ? CustomizationRole.Human : CustomizationRole.Mosquito;

        private IEnumerable<CustomizationSlotSnapshot> VisibleModularSlots()
        {
            if (customizationState?.Catalog == null) return Enumerable.Empty<CustomizationSlotSnapshot>();
            CustomizationRole role = ToCustomizationRole(modularEditedRole);
            return customizationState.Catalog.Slots.Where(slot => slot.Role == role &&
                !string.IsNullOrWhiteSpace(slot.Label) &&
                slot.Options.Any(option => !string.IsNullOrWhiteSpace(option.Label)));
        }

        private void EnsureModularSelectedSlot()
        {
            if (VisibleModularSlots().Any(slot => string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal))) return;
            modularSelectedSlotId = VisibleModularSlots().Select(slot => slot.SlotId).FirstOrDefault() ?? string.Empty;
        }

        private void BuildModularCustomization()
        {
            if (modularCategoryRoot == null || modularOptionRoot == null) return;
            AlfaUiFactory.Clear(modularCategoryRoot);
            AlfaUiFactory.Clear(modularOptionRoot);
            foreach (var slot in VisibleModularSlots())
            {
                var capturedSlot = slot;
                bool selected = string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal);
                var button = factory.Button(modularCategoryRoot, "ModularCategory_" + slot.WireSlotId,
                    (selected ? "> " : string.Empty) + slot.Label,
                    () => SelectModularCategory(capturedSlot.SlotId), false, false, 54f);
                ApplyModularButtonStyle(button, selected);
            }

            var current = VisibleModularSlots().FirstOrDefault(slot =>
                string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal));
            if (current == null) return;
            string selectedOption = modularCustomizationDraft.For(ToCustomizationRole(modularEditedRole)).OptionFor(current.SlotId);
            foreach (var option in current.Options.Where(item => !string.IsNullOrWhiteSpace(item.Label)))
            {
                var capturedOption = option;
                bool selected = string.Equals(option.OptionId, selectedOption, StringComparison.Ordinal);
                var button = factory.Button(modularOptionRoot,
                    "ModularOption_" + current.WireSlotId + "_" + option.WireOptionId,
                    (selected ? "> " : string.Empty) + option.Label,
                    () => SetModularCustomizationOption(current.SlotId, capturedOption.OptionId), false, false, 60f);
                ApplyModularButtonStyle(button, selected);
                AddModularOptionVisual(button, current, option);
            }
        }

        private void ResetModularScrollPositions()
        {
            ResetScrollPosition(modularCategoryRoot);
            ResetScrollPosition(modularOptionRoot);
        }

        private static void ResetScrollPosition(RectTransform content)
        {
            var scroll = content?.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null) return;
            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = 0f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void AddModularOptionVisual(UnityEngine.UI.Button button, CustomizationSlotSnapshot slot,
            CustomizationOptionSnapshot option)
        {
            Sprite thumbnail = option.HasSwatch ? null : customizationState.ThumbnailResolver?.Invoke(slot.SlotId, option.OptionId);
            if (!option.HasSwatch && thumbnail == null) return;
            var visual = AlfaUiFactory.Node(option.HasSwatch ? "ColorSwatch" : "Thumbnail", button.transform, typeof(UnityEngine.UI.Image));
            var image = visual.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            if (option.HasSwatch) image.color = ColorFromRgba(option.SwatchRgba);
            else
            {
                image.sprite = thumbnail;
                image.preserveAspect = true;
            }
            Anchor(visual.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(36f, 36f));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) AlfaUiFactory.Fill(label.rectTransform, 58f, 16f, 8f, 8f);
        }

        private static Color ColorFromRgba(uint value) => new Color(
            ((value >> 24) & 255) / 255f,
            ((value >> 16) & 255) / 255f,
            ((value >> 8) & 255) / 255f,
            (value & 255) / 255f);

        private static void ApplyModularButtonStyle(UnityEngine.UI.Button button, bool selected)
        {
            AlfaUiFactory.ApplyStyle(button, selected ? AlfaButtonStyle.Primary : AlfaButtonStyle.Tab);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 20f;
            }
        }

        private void SelectModularCategory(string slotId)
        {
            if (customizationState?.Mode != CustomizationUiMode.Modular || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (!VisibleModularSlots().Any(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))) return;
            modularSelectedSlotId = slotId;
            BuildModularCustomization();
        }

        private void SetModularCustomizationOption(string slotId, string optionId)
        {
            if (customizationState?.Mode != CustomizationUiMode.Modular || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (!customizationState.Catalog.TrySlot(slotId, out var slot) || slot.Role != ToCustomizationRole(modularEditedRole) ||
                !slot.TryOption(optionId, out var option) || string.IsNullOrWhiteSpace(option.Label)) return;
            var next = modularCustomizationDraft.Copy();
            next.For(ToCustomizationRole(modularEditedRole)).SetOption(slotId, optionId);
            if (!customizationState.Catalog.TryNormalize(next, out var normalized, out var error))
            {
                customizationStatus.text = "No se pudo seleccionar esa opción. Probá otra.";
                return;
            }
            modularCustomizationDraft = normalized;
            BuildModularCustomization();
            UpdateCustomizationView();
            SendModularPreview();
        }

        private void SendModularPreview()
        {
            if (actions is IModularCustomizationActions modularActions)
            {
                modularActions.PreviewModularCustomization(modularCustomizationDraft.Copy(), modularEditedRole);
                return;
            }
            customizationStatus.text = "Esta selección todavía no se puede guardar.";
        }

        private static RectTransform CreatePaletteLayout(Transform parent, string name)
        {
            var node = AlfaUiFactory.Node(name, parent, typeof(UnityEngine.UI.GridLayoutGroup));
            var grid = node.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(216f, 66f);
            grid.spacing = new Vector2(8f, 8f);
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            return node.GetComponent<RectTransform>();
        }

        private void BuildPalette(RectTransform parent, IReadOnlyList<NamedColorOption> options, string selectedId, Action<NamedColorOption> selected)
        {
            AlfaUiFactory.Clear(parent);
            foreach (var option in options)
            {
                var captured = option;
                var button = factory.Button(parent, "Color_" + option.Id, option.Label, () => selected(captured), AlfaButtonStyle.Secondary, 66f);
                var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                buttonLabel.alignment = TextAlignmentOptions.Left;
                AlfaUiFactory.Fill(buttonLabel.rectTransform, 52f, 8f, 8f, 8f);
                buttonLabel.enableAutoSizing = false;
                buttonLabel.fontSize = 20f;
                buttonLabel.textWrappingMode = TextWrappingModes.NoWrap;
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 112f;
                var contrast = RelativeLuminance(option.Color) > 0.5f ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100;
                var swatch = factory.Panel(button.transform, "Swatch", option.Color, -1f, -1f, 6f);
                AlfaUiFactory.SetSurface(swatch, bottom: Color.white, frame: AlfaUiTheme.WithAlpha(contrast, 0.55f), shadow: Color.clear);
                Anchor(swatch, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(30f, 30f));
                var selectionMark = factory.Icon(swatch, "SelectionMark", AlfaUiIconKind.Ready, contrast);
                AlfaUiFactory.Fill(selectionMark.rectTransform, 6f, 6f, 6f, 6f);
                selectionMark.gameObject.SetActive(option.Id == selectedId);
                MarkSelected(button, option.Id == selectedId);
            }
        }

        /// <summary>Selected options get the accent frame and glow; the check mark or label keeps it readable without colour.</summary>
        private static void MarkSelected(Component target, bool selected)
        {
            var surface = target != null ? target.GetComponent<AlfaUiSurface>() : null;
            if (surface == null) return;
            surface.FrameColor = selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.Border;
            surface.ShadowColor = selected ? AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.5f) : AlfaUiTheme.WithAlpha(Color.black, 0.42f);
            surface.Refresh();
        }

        private void UpdateCustomizationView()
        {
            if (customizationState == null) return;
            bool modular = customizationState.Mode == CustomizationUiMode.Modular;
            if (modular && modularCustomizationDraft == null) return;
            if (!modular && customizationDraft == null) return;
            var role = modular ? modularEditedRole : customizationDraft.Role;
            var human = role == AlfaRole.Human;
            bool editable = !customizationState.IsReadOnly;
            humanCustomizationFields.SetActive(editable && !modular && human);
            mosquitoCustomizationFields.SetActive(editable && !modular && !human);
            modularCustomizationFields.SetActive(editable && modular);
            customizationHumanButton.interactable = editable && !customizationSaveLatched;
            customizationMosquitoButton.interactable = editable && !customizationSaveLatched;
            customizationResetButton.interactable = editable && !customizationSaveLatched;
            SetRoleButtonSelection(customizationHumanButton, human, AlfaRole.Human);
            SetRoleButtonSelection(customizationMosquitoButton, !human, AlfaRole.Mosquito);
            customizationPreviewTitle.text = human ? "VISTA EN VIVO · HUMANO" : "VISTA EN VIVO · MOSQUITO";
            customizationPreviewIcon.Kind = human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            customizationPreviewIcon.color = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            customizationCategoryTitle.text = !editable ? "PERSONALIZACIÓN NO DISPONIBLE" : modular
                ? (VisibleModularSlots().Any() ? "CATEGORÍAS DEL " + (human ? "HUMANO" : "MOSQUITO") : "SIN OPCIONES DISPONIBLES")
                : human ? "PALETA DEL HUMANO" : "PALETA DEL MOSQUITO";
            previewOrbit?.Show(role);
            if (customizationSaveLatched) customizationStatus.text = "Aplicando apariencia…";
            else if (customizationState.IsReadOnly)
                customizationStatus.text = string.IsNullOrWhiteSpace(customizationState.Message)
                    ? "Esta personalización todavía no está disponible en esta versión."
                    : customizationState.Message;
            else if (modular && !customizationState.ModularPreviewAvailable)
                customizationStatus.text = string.IsNullOrWhiteSpace(customizationState.Message)
                    ? "La vista de estas piezas llegará cuando estén listas en el juego."
                    : customizationState.Message;
            else customizationStatus.text = customizationState.Message;
            bool dirty = modular
                ? !modularCustomizationDraft.CanonicalEquals(customizationState.PublishedSelection)
                : !customizationDraft.SameValues(customizationState.Saved);
            customizationSaveButton.interactable = editable && !customizationSaveLatched && dirty;
            customizationSaveLabel.text = customizationSaveLatched ? "APLICANDO…" : "APLICAR";
            if (!modular)
            {
                MarkPalette(humanPaletteRoot, customizationDraft.SkinColorId);
                MarkPalette(pajamaPaletteRoot, customizationDraft.PajamaColorId);
                MarkPalette(mosquitoPaletteRoot, customizationDraft.MosquitoColorId);
            }
        }

        private static void MarkPalette(Transform parent, string selectedId)
        {
            foreach (Transform child in parent)
            {
                var selected = child.name == "Color_" + selectedId;
                MarkSelected(child, selected);
                var selectionMark = child.Find("Swatch/SelectionMark");
                if (selectionMark != null) selectionMark.gameObject.SetActive(selected);
            }
        }

        private void SaveCustomization()
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsSaving || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                if (modularCustomizationDraft == null) return;
                var modularActions = actions as IModularCustomizationActions;
                if (modularActions == null)
                {
                    customizationStatus.text = "Esta selección todavía no se puede guardar.";
                    return;
                }
                customizationSaveLatched = true;
                customizationSaveButton.interactable = false;
                customizationSaveLabel.text = "APLICANDO…";
                customizationStatus.text = "Aplicando apariencia…";
                modularActions.SaveModularCustomization(modularCustomizationDraft.Copy(), modularEditedRole);
                return;
            }
            if (customizationDraft == null) return;
            customizationSaveLatched = true;
            customizationSaveButton.interactable = false;
            customizationSaveLabel.text = "APLICANDO…";
            customizationStatus.text = "Aplicando apariencia…";
            actions.SaveCustomization(customizationDraft.Copy());
        }

        private void ResetCustomization()
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                modularCustomizationDraft = (modularCustomizationSessionBaseline ?? customizationState.DraftSelection).Copy();
                BuildModularCustomization();
                UpdateCustomizationView();
                SendModularPreview();
                return;
            }
            customizationDraft = (customizationSessionBaseline ?? customizationState.Draft).Copy();
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void CloseCustomization()
        {
            if (customizationSaveLatched) return;
            var returnScreen = customizationReturnScreen;
            var returnLobbyCode = customizationLobbyCode;
            customizationSessionBaseline = null;
            modularCustomizationSessionBaseline = null;
            modularSelectedSlotId = string.Empty;
            customizationReturnScreen = AlfaUiScreen.MainMenu;
            customizationLobbyCode = string.Empty;
            if (returnScreen == AlfaUiScreen.Lobby && lobbyState?.IsWaiting == true && string.Equals(lobbyState.RoomCode, returnLobbyCode, StringComparison.Ordinal))
                SetScreen(AlfaUiScreen.Lobby, "LobbyReadyButton");
            else ShowMainMenu();
        }

        private void ChangeSetting(Action<AlfaSettingsDraft> mutation)
        {
            if (settingsDraft == null || settingsState == null || settingsApplyLatched || settingsState.IsApplying) return;
            mutation(settingsDraft);
            settingsApplyButton.interactable = !settingsDraft.SameValues(settingsState.Saved);
            UpdateSettingsCycles();
        }

        private void UpdateSettingsCycles()
        {
            if (settingsDraft == null || settingsState == null) return;
            resolutionValue.text = ItemAt(settingsState.Resolutions, settingsDraft.ResolutionIndex);
            qualityValue.text = ItemAt(settingsState.Qualities, settingsDraft.QualityIndex);
        }

        private void ApplySettings()
        {
            if (settingsDraft == null || settingsState == null || settingsApplyLatched || settingsState.IsApplying) return;
            settingsApplyLatched = true;
            settingsApplyButton.interactable = false;
            settingsApplyLabel.text = "APLICANDO…";
            settingsStatus.text = "Aplicando ajustes…";
            actions.ApplySettings(settingsDraft.Copy());
        }

        private void ResetSettings()
        {
            if (settingsState == null || settingsApplyLatched) return;
            settingsDraft = settingsState.Saved.Copy();
            PresentSettings(new SettingsUiState(settingsState.Saved, settingsDraft, settingsState.Resolutions,
                settingsState.Qualities, settingsState.SupportsVideo, settingsState.SupportsRebinding,
                supportsReducedMenuMotion: settingsState.SupportsReducedMenuMotion, voiceDevices: settingsState.VoiceDevices));
        }

        private void CloseSettings()
        {
            if (settingsApplyLatched) return;
            if (SettingsDirty())
            {
                ShowConfirm("¿DESCARTAR CAMBIOS?", "Los ajustes no aplicados se perderán.", "SEGUIR EDITANDO", "DESCARTAR", ReturnFromSettings);
                return;
            }
            ReturnFromSettings();
        }

        private void ReturnFromSettings()
        {
            if (settingsReturnScreen == AlfaUiScreen.Gameplay)
                actions.SetGameplayInputBlocked(false);
            SetScreen(settingsReturnScreen, settingsReturnScreen == AlfaUiScreen.Pause ? "PauseContinueButton" : "MainSettingsButton");
        }

        private void ResultsPrimaryAction()
        {
            if (resultsState == null || resultsActionLatched) return;
            if (resultsState.IsTraining)
            {
                StartTrainingIntent(resultsState.TrainingRole, true);
                return;
            }
            resultsActionLatched = true;
            resultsPrimary.interactable = false;
            resultsPrimaryLabel.text = "VOLVIENDO…";
            resultsLeave.interactable = false;
            actions.ReturnToLobby();
        }

        private void BeginRound()
        {
            if (!CanEditLobbyRules || !lobbyState.CanStart) return;
            lobbyStartLatched = true;
            UpdateRoomMapView();
            gameplayIsTraining = false;
            lobbyStartButton.interactable = false;
            lobbyReadyButton.interactable = false;
            lobbyExploreButton.interactable = false;
            foreach (var entry in humanCountLabels)
                entry.Value.transform.parent.GetComponent<UnityEngine.UI.Button>().interactable = false;
            lobbyStartLabel.text = "INICIANDO…";
            lobbyStatus.text = "Iniciando ronda…";
            Focus(lobbyReadyButton.interactable ? lobbyReadyButton.gameObject : lobbyCopyButton.gameObject);
            actions.StartRound();
        }

        private void ResultsLeaveAction()
        {
            if (resultsState != null && resultsState.IsTraining && TrainingBusy)
            {
                RequestTrainingCancel();
                return;
            }
            if (resultsState != null && resultsState.IsTraining)
            {
                resultsActionLatched = true;
                resultsPrimary.interactable = false;
                resultsLeave.interactable = false;
                resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = "SALIENDO…";
                LeaveActiveTraining();
            }
            else ConfirmLeave();
        }

        private void LeaveGameplayContext()
        {
            if (gameplayIsTraining)
                ShowConfirm("¿SALIR DEL ENTRENAMIENTO?", "Volverás al menú principal.", "VOLVER", "SALIR", LeaveActiveTraining);
            else ConfirmLeave();
        }

        private void LeaveActiveTraining()
        {
            actions.CancelTraining();
            ShowMainMenu();
        }

        private void ResumeFromPause()
        {
            actions.ResumeGame();
            ShowGameplay();
        }

        private void BeginLobbyExploration()
        {
            if (lobbyState == null || !lobbyState.IsWaiting || !lobbyState.CanExplore || LobbyBusy) return;
            lobbyExploring = true;
            screens[AlfaUiScreen.Lobby].SetActive(false);
            actions.SetLobbyExploration(true);
            SetCursor(false);
        }

        private void EndLobbyExploration()
        {
            lobbyExploring = false;
            screens[AlfaUiScreen.Lobby].SetActive(true);
            actions.SetLobbyExploration(false);
            SetCursor(true);
            Focus(lobbyReadyButton.gameObject);
        }

        private void ConfirmQuit() => ShowConfirm("¿SALIR DEL JUEGO?", "Vas a cerrar Let me sleep.", "VOLVER", "SALIR", () => actions.QuitGame());
        private void ConfirmLeave() => ShowConfirm("¿SALIR DE LA SALA?", "Volverás al menú principal.", "VOLVER", "SALIR", () => actions.LeaveRoom());
        private void ShowLobbyPause() => ShowConfirm("PAUSA", "La sala sigue abierta.", "VOLVER A SALA", "SALIR", () => actions.LeaveRoom());

        private void ShowConfirm(string title, string body, string safeLabel, string dangerLabel, Action dangerAction)
        {
            confirmTitle.text = title;
            confirmBody.text = body;
            confirmSafeLabel.text = safeLabel;
            confirmDangerLabel.text = dangerLabel;
            confirmDangerAction = dangerAction;
            SetActiveScreenInput(false);
            confirmModal.SetActive(true);
            SetCursor(true);
            Focus(confirmSafe.gameObject);
        }

        private void CloseConfirm()
        {
            confirmDangerAction = null;
            confirmModal.SetActive(false);
            SetActiveScreenInput(true);
            Focus(DefaultFocusName(screen));
        }

        private void ConfirmDanger()
        {
            var action = confirmDangerAction;
            confirmDangerAction = null;
            confirmModal.SetActive(false);
            SetActiveScreenInput(true);
            action?.Invoke();
        }

        private void HandleEscape()
        {
            if (IsModalOpen)
            {
                RequestFeedback(UiFeedbackKind.Confirm);
                CloseConfirm();
                return;
            }
            var handled = true;
            switch (screen)
            {
                case AlfaUiScreen.MainMenu: ConfirmQuit(); break;
                case AlfaUiScreen.OnlineChoice: ShowMainMenu(); break;
                case AlfaUiScreen.CreateRoom:
                case AlfaUiScreen.JoinRoom:
                    if (OnlineBusy && !onlineCancelLatched) RequestOnlineCancel();
                    else if (OnlineBusy) handled = false;
                    else if (onlineErrorCard != null && onlineErrorCard.activeInHierarchy) DismissOnlineError();
                    else ShowMainMenu();
                    break;
                case AlfaUiScreen.Lobby:
                    if (lobbyExploring) EndLobbyExploration(); else ShowLobbyPause();
                    break;
                case AlfaUiScreen.Training:
                    if (TrainingBusy && !trainingCancelLatched) RequestTrainingCancel();
                    else if (!TrainingBusy) ShowMainMenu();
                    else handled = false;
                    break;
                case AlfaUiScreen.Customization:
                    if (!customizationSaveLatched) CloseCustomization(); else handled = false;
                    break;
                case AlfaUiScreen.Settings:
                    if (!settingsApplyLatched) CloseSettings(); else handled = false;
                    break;
                case AlfaUiScreen.Gameplay: ShowPause(); break;
                case AlfaUiScreen.Pause: ResumeFromPause(); break;
                case AlfaUiScreen.Results:
                    if (resultsState != null && resultsState.IsTraining && TrainingBusy && !trainingCancelLatched)
                        RequestTrainingCancel();
                    else handled = false;
                    break;
                default: handled = false; break;
            }
            if (handled) RequestFeedback(UiFeedbackKind.Confirm);
        }

        private void SetScreen(AlfaUiScreen next, string focusName)
        {
            var changed = screen != next;
            screen = next;
            foreach (var pair in screens)
            {
                var active = pair.Key == next;
                if ((next == AlfaUiScreen.CreateRoom || next == AlfaUiScreen.JoinRoom) &&
                    (pair.Key == AlfaUiScreen.CreateRoom || pair.Key == AlfaUiScreen.JoinRoom)) active = true;
                pair.Value.SetActive(active);
                if (active && pair.Value.TryGetComponent(out CanvasGroup group))
                {
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
            }
            confirmModal?.SetActive(false);
            previewOrbit?.SetVisible(next == AlfaUiScreen.Customization);
            var gameplay = next == AlfaUiScreen.Gameplay;
            SetCursor(!gameplay);
            if (focusName == null)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                feedbackSelection = null;
            }
            else Focus(focusName);
            if (changed)
            {
                try { ScreenChanged?.Invoke(next); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void SetActiveScreenInput(bool enabled)
        {
            if (!screens.TryGetValue(screen, out var activeView) ||
                !activeView.TryGetComponent(out CanvasGroup group)) return;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private static void SetCursor(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void Focus(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return;
            var target = FindActive(transform, objectName);
            if (target != null) Focus(target.gameObject);
        }

        private void Focus(GameObject target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
            feedbackSelection = target;
        }

        private void UpdateSelectionFeedback()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == feedbackSelection) return;
            feedbackSelection = selected;
            if (selected == null || !selected.activeInHierarchy || PointerChangedSelectionThisFrame()) return;
            var selectable = selected.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable != null && selectable.IsInteractable()) RequestFeedback(UiFeedbackKind.Select);
        }

        private static bool PointerChangedSelectionThisFrame()
        {
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
                return true;
            return Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
                Touchscreen.current.primaryTouch.press.wasReleasedThisFrame);
        }

        private void RequestFeedback(UiFeedbackKind kind)
        {
            try
            {
                FeedbackRequested?.Invoke(kind);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private static Transform FindActive(Transform root, string objectName)
        {
            foreach (Transform child in root)
            {
                if (child.gameObject.activeInHierarchy && child.name == objectName) return child;
                var found = FindActive(child, objectName);
                if (found != null) return found;
            }
            return null;
        }

        private string DefaultFocusName(AlfaUiScreen value)
        {
            switch (value)
            {
                case AlfaUiScreen.MainMenu: return "MainPlayButton";
                case AlfaUiScreen.OnlineChoice: return "OnlineCreateTab";
                case AlfaUiScreen.CreateRoom:
                case AlfaUiScreen.JoinRoom:
                    if (OnlineBusy) return "OnlineCancelButton";
                    if (onlineErrorCard != null && onlineErrorCard.activeInHierarchy)
                        return onlineRetryButton.gameObject.activeSelf ? "OnlineRetryButton" : "OnlineErrorBackButton";
                    return "PlayerNameInput";
                case AlfaUiScreen.Lobby: return lobbyReadyButton != null && lobbyReadyButton.interactable ? "LobbyReadyButton" : "LobbyCopyButton";
                case AlfaUiScreen.Training: return trainingState.SelectedRole == AlfaRole.Human ? "TrainingHumanButton" : "TrainingMosquitoButton";
                case AlfaUiScreen.Customization:
                    return customizationState?.Mode == CustomizationUiMode.Modular
                        ? modularEditedRole == AlfaRole.Mosquito ? "CustomizationMosquitoButton" : "CustomizationHumanButton"
                        : customizationDraft != null && customizationDraft.Role == AlfaRole.Mosquito ? "CustomizationMosquitoButton" : "CustomizationHumanButton";
                case AlfaUiScreen.Settings: return "MasterVolumeSlider";
                case AlfaUiScreen.Pause: return "PauseContinueButton";
                case AlfaUiScreen.Results: return resultsPrimary != null && resultsPrimary.gameObject.activeSelf ? "ResultsPrimaryButton" : "ResultsLeaveButton";
                default: return null;
            }
        }

        private bool CustomizationDirty() => customizationState != null &&
            (customizationState.Mode == CustomizationUiMode.Modular
                ? modularCustomizationDraft != null && !modularCustomizationDraft.CanonicalEquals(customizationState.PublishedSelection)
                : customizationDraft != null && !customizationDraft.SameValues(customizationState.Saved));
        private bool SettingsDirty() => settingsState != null && settingsDraft != null && !settingsDraft.SameValues(settingsState.Saved);
        private bool OnlineBusy => onlineSubmissionLatched || onlineState.IsBusy;
        private bool TrainingBusy => trainingStartLatched || trainingState.IsLoading;
        private static bool IsOnlineError(OnlineOperationPhase phase) => phase == OnlineOperationPhase.RecoverableError ||
            phase == OnlineOperationPhase.IncompatibleVersion || phase == OnlineOperationPhase.RoomClosed;

        private static void SetRoleButtonSelection(UnityEngine.UI.Button button, bool selected, AlfaRole role)
        {
            // Human selections fill blue, mosquito selections red (team colours); unselected tabs stay navy.
            AlfaUiFactory.ApplyStyle(button, selected ? role == AlfaRole.Human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger : AlfaButtonStyle.Secondary);
            var title = button.GetComponentInChildren<TextMeshProUGUI>();
            var plainTitle = title.text.StartsWith("> ", StringComparison.Ordinal) ? title.text.Substring(2) : title.text;
            title.text = selected ? "> " + plainTitle : plainTitle;
            var rail = button.transform.Find("FocusRail");
            if (rail != null) rail.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(Color.white, selected ? 0.9f : 0f);
        }

        private static void ApplyPositiveStyle(UnityEngine.UI.Button button) => AlfaUiFactory.ApplyStyle(button, AlfaButtonStyle.Success);

        private static void SetSceneScrim(GameObject view, float alpha)
        {
            var image = view.GetComponent<UnityEngine.UI.Image>();
            image.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Scrim, alpha);
            image.raycastTarget = true;
        }

        private static string ActorStateText(AlfaRole role, HudActorState state)
        {
            switch (state)
            {
                case HudActorState.Spectating: return "ELIMINADO · OBSERVANDO";
                case HudActorState.Extracting: return "EXTRAYENDO";
                case HudActorState.Bitten: return role == AlfaRole.Human ? "TE ESTÁN PICANDO · MIRÁ Y GOLPEÁ" : string.Empty;
                case HudActorState.Recovering: return "RECUPERANDO…";
                case HudActorState.Fainted: return "DESMAYADO";
                case HudActorState.Stunned: return "ATURDIDO";
                case HudActorState.Attached: return "[E] DESPRENDERTE";
                default: return string.Empty;
            }
        }

        private static string DefaultRoleHint(AlfaRole role) => role == AlfaRole.Mosquito ?
            "W · volar hacia la mira   ·   Soltá W · frenar" : string.Empty;

        private static string FormatClock(float seconds)
        {
            var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static int Cycle(int current, int delta, int count)
        {
            if (count <= 0) return 0;
            return (current + delta % count + count) % count;
        }

        private static string FrameLimitLabel(int value) => value <= 0 ? "SIN LÍMITE" : value + " FPS";

        private static string ItemAt(IReadOnlyList<string> items, int index) => items != null && index >= 0 && index < items.Count ? items[index] : "—";

        private static float RelativeLuminance(Color color) => 0.2126f * color.linear.r + 0.7152f * color.linear.g + 0.0722f * color.linear.b;

        private static string NormalizePlayerName(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= 24 ? normalized : normalized.Substring(0, 24);
        }

        private static CustomizationUiState DefaultCustomization()
        {
            var skin = new[]
            {
                new NamedColorOption("warm-light", "CLARO", new Color(0.88f, 0.67f, 0.50f)),
                new NamedColorOption("warm-medium", "MEDIO", new Color(0.67f, 0.43f, 0.29f)),
                new NamedColorOption("warm-deep", "OSCURO", new Color(0.35f, 0.20f, 0.16f))
            };
            var pajamas = new[]
            {
                new NamedColorOption("blue", "AZUL", AlfaUiTheme.Sky400),
                new NamedColorOption("coral", "CORAL", AlfaUiTheme.Pajama500),
                new NamedColorOption("green", "VERDE", AlfaUiTheme.Mint400)
            };
            var mosquitoes = new[]
            {
                new NamedColorOption("red", "ROJO", AlfaUiTheme.Pajama500),
                new NamedColorOption("blue", "AZUL", AlfaUiTheme.Sky400),
                new NamedColorOption("green", "VERDE", new Color(0.32f, 0.56f, 0.35f))
            };
            var saved = new BasicCustomizationDraft(AlfaRole.Human, "warm-medium", "blue", "red");
            return new CustomizationUiState(skin, pajamas, mosquitoes, saved);
        }

        private static SettingsUiState DefaultSettings()
        {
            var saved = new AlfaSettingsDraft
            {
                MasterVolume = 1f,
                MusicVolume = 0.8f,
                EffectsVolume = 1f,
                VoiceVolume = .8f,
                PushToTalkBinding = "<Keyboard>/v",
                FullScreen = true,
                VSync = false,
                FrameLimit = 0,
                HumanSensitivity = 1f,
                MosquitoSensitivity = 1f,
                InvertY = false
            };
            return new SettingsUiState(saved, saved, Array.Empty<string>(), Array.Empty<string>(), false, false);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
