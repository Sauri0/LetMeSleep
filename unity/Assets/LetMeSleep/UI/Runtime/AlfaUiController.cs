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
        private GameObject onlineRoomDefaultsRow;
        private RectTransform onlineFormPanel;
        private UnityEngine.UI.Button onlineMapPrevious;
        private UnityEngine.UI.Button onlineMapNext;
        private UnityEngine.UI.Image onlineMapThumbnail;
        private AlfaUiIcon onlineMapPlaceholder;
        private TextMeshProUGUI onlineMapLabel;
        private TextMeshProUGUI onlineModeLabel;
        private TextMeshProUGUI onlineHumansLabel;
        private string onlineMapId = HousePatioMapId;
        private string onlineModeId = GameModes.Blood;
        private int? onlineHumans;
        private PendingRoomDefaults pendingRoomDefaults;

        /// <summary>Room rules chosen on the create tab, applied one at a time once the room exists.</summary>
        private sealed class PendingRoomDefaults
        {
            public string MapId;
            public string ModeId;
            public int? Humans;
            public bool MapDone;
            public bool ModeDone;
            public bool HumansDone;
        }

        private RectTransform lobbyNametagRoot;
        private readonly List<RectTransform> lobbyNametags = new List<RectTransform>();
        private RectTransform lobbySafeArea;
        private RectTransform lobbyRosterPanel;
        private RectTransform lobbyMemberScroll;
        private RectTransform lobbyRulesPanel;
        private GameObject lobbyRulesEditor;
        private TextMeshProUGUI lobbyRulesSummary;
        private UnityEngine.UI.Button lobbyRulesToggle;
        private bool lobbyRulesCollapsed;
        private bool lobbyRulesUserChoice;
        private TextMeshProUGUI lobbyVoicePtt;
        private TextMeshProUGUI[] lobbyVoiceLines;
        private TextMeshProUGUI lobbyVoiceHint;
        // Evidence/testing hook: a presence source used instead of the actions (the capture harness sets it).
        private ILobbyPresenceSource lobbyPresenceOverride;

        private CharacterPreviewSetup portraitSetup;
        private UnityEngine.UI.RawImage trainingHumanPortrait;
        private UnityEngine.UI.RawImage trainingMosquitoPortrait;
        private readonly List<RenderTexture> renderedPortraits = new List<RenderTexture>();
        private TextMeshProUGUI hudVoiceChipLabel;
        private GameObject hudVoiceChip;
        private readonly UnityEngine.UI.Image[] hudEquipmentSlots = new UnityEngine.UI.Image[4];
        private readonly TextMeshProUGUI[] hudEquipmentNumbers = new TextMeshProUGUI[4];

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
            pendingRoomDefaults = null;
            lobbyRulesUserChoice = false;
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
            // One title for every busy phase; the phase itself is the message ("Buscando la sala…"), never repeated.
            onlineConnectingTitle.text = "CONECTANDO…";
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
            onlineErrorCard.SetActive(showError);
            // The card carries the message; the inline status would repeat it (and clip) behind the scrim.
            onlineStatus.gameObject.SetActive(!blocking);
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
            lobbyStatus.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "Preparando el código…" : "Compartí el código con tus amigos.";
            lobbyRoleBadge.text = state.IsOwner ? "ANFITRIÓN" : "INVITADO";
            lobbyRoleBadge.color = state.IsOwner ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Moon200;
            lobbyHeaderTitle.text = state.IsWaiting ? "ESPERANDO JUGADORES" : "RONDA EN CURSO";
            lobbyHeaderNote.text = LobbyHeaderNote(state);
            lobbyCount.text = state.Members.Count + "/" + LetMeSleep.Core.RoomRules.Capacity;
            lobbyReadyButton.interactable = !lobbyReadyLatched && !lobbyStartLatched;
            lobbyReadyLabel.text = lobbyReadyLatched ? "GUARDANDO…" : state.LocalReady ? "CANCELAR LISTO" : "LISTO";
            // Ready is the green call to action; once ready, undoing it is a secondary action without the check.
            AlfaUiFactory.ApplyStyle(lobbyReadyButton, state.LocalReady ? AlfaButtonStyle.Secondary : AlfaButtonStyle.Success);
            var readyIcon = lobbyReadyButton.transform.Find("IconPlate/Icon")?.GetComponent<AlfaUiIcon>();
            if (readyIcon != null) readyIcon.Kind = state.LocalReady ? AlfaUiIconKind.Close : AlfaUiIconKind.Ready;
            factory.StrongLabel(lobbyReadyButton, AlfaUiTheme.CtaSize);
            lobbyStartButton.gameObject.SetActive(state.IsOwner);
            lobbyStartButton.interactable = state.IsOwner && state.CanStart && !lobbyReadyLatched && !lobbyStartLatched;
            lobbyStartLabel.text = lobbyStartLatched ? "INICIANDO…" : "INICIAR RONDA";
            lobbyStartReason.text = state.IsOwner && !state.CanStart ? state.StartBlockReason : string.Empty;
            lobbyStartReason.gameObject.SetActive(!string.IsNullOrWhiteSpace(lobbyStartReason.text));
            lobbyReadyButton.gameObject.SetActive(state.IsWaiting);
            lobbyExploreButton.gameObject.SetActive(state.CanExplore);
            lobbyExploreButton.interactable = !lobbyStartLatched;
            lobbyCustomizeButton.gameObject.SetActive(state.IsWaiting);
            lobbyCustomizeButton.interactable = state.IsWaiting && !lobbyStartLatched;
            // Folded by default so the waiting room stays visible; the host unfolds it to edit (chevron).
            if (!lobbyRulesUserChoice) lobbyRulesCollapsed = true;

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
                // Selection is carried by the filled style, the 3-unit frame and the label, not by colour alone.
                AlfaUiFactory.SetSelected(countButton, selected);
                countButton.interactable = state.IsOwner && !lobbyStartLatched;
            }

            if (lobbyStartLatched) lobbyStatus.text = "Iniciando ronda…";
            else if (lobbyReadyLatched) lobbyStatus.text = "Guardando estado…";
            else if (lobbyRulesLatched) lobbyStatus.text = "Guardando reglas…";
            else if (!state.IsWaiting) lobbyStatus.text = "La ronda está en curso. Entrás en la próxima.";
            UpdateRoomMapView();
            UpdateLobbyControls();
            UpdateLobbyVoiceMarkers();
            UpdateLobbyVoicePanel();
            UpdateLobbyRulesView();
            LayoutLobbyRoster();

            bool editingThisLobby = state.IsWaiting && screen == AlfaUiScreen.Customization &&
                customizationReturnScreen == AlfaUiScreen.Lobby && string.Equals(state.RoomCode, customizationLobbyCode, StringComparison.Ordinal);
            if (screen != AlfaUiScreen.Lobby && !editingThisLobby)
                SetScreen(AlfaUiScreen.Lobby, "LobbyReadyButton");
            ApplyPendingRoomDefaults();
        }

        /// <summary>
        /// Second header line: the start countdown when the room publishes one (UI-06 "LA PARTIDA COMIENZA EN
        /// 00:28"), a ready banner once every connected player is ready, or who starts the round.
        /// </summary>
        private static string LobbyHeaderNote(LobbyUiState state)
        {
            if (!state.IsWaiting) return "ENTRÁS EN LA PRÓXIMA RONDA";
            if (state.StartCountdownSeconds.HasValue)
                return "LA PARTIDA COMIENZA EN  <color=#FFC93C><size=130%>" + FormatClock(state.StartCountdownSeconds.Value) + "</size></color>";
            var connected = state.Members.Where(member => member.Connected).ToList();
            if (connected.Count > 1 && connected.All(member => member.Ready))
                return "<color=#57D26B>¡TODOS LISTOS!</color>  " + (state.IsOwner ? "INICIÁ LA RONDA" : "EL ANFITRIÓN INICIA LA RONDA");
            return state.IsOwner ? "INICIÁ LA RONDA CUANDO TODOS ESTÉN LISTOS" : "EL ANFITRIÓN INICIA LA RONDA CUANDO TODOS ESTÁN LISTOS";
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
            EnsureTrainingPortraits();
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
                hudVoice.color = state.Transmitting ? AlfaUiTheme.StatusOk : state.LocalMuted || !state.DeviceAvailable ? AlfaUiTheme.StatusWarn : AlfaUiTheme.Moon200;
                if (hudVoiceChip != null) hudVoiceChip.SetActive(state.InRoom);
            }
            if (pauseVoiceStatus != null)
                pauseVoiceStatus.text = string.IsNullOrWhiteSpace(state.Notice) ? state.ScopeLabel : state.Notice;
            if (pauseVoiceMuteLabel != null)
                pauseVoiceMuteLabel.text = state.LocalMuted ? "ACTIVAR MI MICRÓFONO" : "SILENCIAR MI MICRÓFONO";
            if (pauseVoiceMuteButton != null) pauseVoiceMuteButton.interactable = state.InRoom && actions is IVoiceActions;
            if (pushToTalkBindingLabel != null) pushToTalkBindingLabel.text = "PTT · " + state.BindingLabel;
            UpdateLobbyVoiceMarkers();
            UpdateLobbyVoicePanel();
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
            hudBlood.fontSize = state.ModeId == GameModes.Survival ? AlfaUiTheme.MinTextSize : 22f;
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
                hudEquipmentLabels[0].text = "MANOS\n<color=#A8B8D8>SIN OBJETO</color>";
                hudEquipmentIcons[0].Kind = AlfaUiIconKind.Hands;
                for (int i = 0; i < equipment.Slots.Count; i++)
                {
                    var slot = equipment.Slots[i];
                    hudEquipmentLabels[i + 1].text = slot.Label +
                        (slot.ResourceText.Length == 0 ? "" : "\n<color=#A8B8D8>" + slot.ResourceText + "</color>");
                    hudEquipmentIcons[i + 1].Kind = slot.Icon;
                }
                for (int i = 0; i < hudEquipmentLabels.Length; i++)
                {
                    // Selection is the blue plate with the 3-unit frame (UI-06), not a text marker.
                    bool selected = equipment.SelectedSlot == i - 1;
                    SetEquipmentSlotSelected(i, selected);
                    hudEquipmentLabels[i].color = AlfaUiTheme.Sheet100;
                    hudEquipmentIcons[i].color = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Moon200;
                    if (hudEquipmentNumbers[i] != null) hudEquipmentNumbers[i].color = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Moon200;
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
            BuildTraining(dependencies);
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

            // Colder night: a blue wash pulls the warm bedroom light toward the UI-06 navy.
            var coldWash = AlfaUiFactory.Node("ColdWash", view.transform, typeof(UnityEngine.UI.Image));
            var coldWashImage = coldWash.GetComponent<UnityEngine.UI.Image>();
            coldWashImage.color = AlfaUiTheme.Hex("12357A", 0.24f);
            coldWashImage.raycastTarget = false;
            AlfaUiFactory.Fill(coldWash.GetComponent<RectTransform>());
            // Left-to-right night gradient: #0E1A30 at 85 % on the left edge to 0 % at 45 % of the width.
            var nightWash = AlfaUiFactory.Node("NightWash", view.transform, typeof(UnityEngine.UI.Image));
            var nightWashImage = nightWash.GetComponent<UnityEngine.UI.Image>();
            nightWashImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.85f);
            nightWashImage.sprite = AlfaUiFactory.LinearFadeSprite();
            nightWashImage.raycastTarget = false;
            Anchor(nightWash.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var floorWash = AlfaUiFactory.Node("FloorWash", view.transform, typeof(UnityEngine.UI.Image));
            var floorWashImage = floorWash.GetComponent<UnityEngine.UI.Image>();
            floorWashImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.6f);
            floorWashImage.sprite = AlfaUiFactory.VerticalFadeSprite();
            floorWashImage.raycastTarget = false;
            Anchor(floorWash.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 200f));

            var brand = factory.BrandLockup(view.transform, "Brand", 620f, "HUMANOS CONTRA MOSQUITOS");
            var brandHeight = brand.sizeDelta.y;
            Anchor(brand, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -34f), brand.sizeDelta);

            // Top-anchored under the wordmark so the rail never climbs into the logo on wide or tall aspects. The
            // motto lives in the same column: always 24 units under SALIR, whatever the aspect ratio.
            var menu = factory.Vertical(view.transform, "MenuRail", 12f, TextAnchor.UpperLeft);
            menu.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            Anchor(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -(34f + brandHeight + 30f)), new Vector2(470f, 0f));
            MenuRailButton(menu, "MainPlayButton", "JUGAR", ShowOnlineChoice, AlfaUiIconKind.Play);
            MenuRailButton(menu, "MainTrainingButton", "ENTRENAMIENTO", ShowTraining, AlfaUiIconKind.Training);
            MenuRailButton(menu, "MainCustomizeButton", "PERSONALIZACIÓN", ShowCustomization, AlfaUiIconKind.Customize);
            MenuRailButton(menu, "MainSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.MainMenu), AlfaUiIconKind.Gear);
            MenuRailButton(menu, "MainQuitButton", "SALIR", ConfirmQuit, AlfaUiIconKind.Exit);
            factory.Divider(menu, "MottoGap", Color.clear, 0f); // 12 + 0 + 12 = 24 units under SALIR
            var motto = factory.Text(menu, "Motto", "LA NOCHE NUNCA ES TAN TRANQUILA", 24f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft, true);
            motto.characterSpacing = 9f;
            motto.textWrappingMode = TextWrappingModes.NoWrap;
            motto.overflowMode = TextOverflowModes.Overflow;
            motto.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 30f;
            var version = string.IsNullOrWhiteSpace(Application.version) ? "ALFA" : Application.version.Replace("-", " / ").ToUpperInvariant();
            var versionText = factory.Text(view.transform, "Version", version + "  ·  WINDOWS", AlfaUiTheme.MinTextSize, AlfaUiTheme.Disabled, TextAlignmentOptions.BottomRight);
            versionText.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(versionText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(420f, 30f));
        }

        private UnityEngine.UI.Button MenuRailButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction callback, AlfaUiIconKind icon)
        {
            var button = factory.Button(parent, name, label, callback, AlfaButtonStyle.Menu, 66f, icon);
            factory.StrongLabel(button, AlfaUiTheme.MenuLabelSize);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(text.rectTransform, 80f, 16f, 4f, 2f);
            return button;
        }

        private void BuildOnlineForm()
        {
            var view = factory.View("OnlineFormView", transform, false);
            SetSceneScrim(view, 0.62f);
            screens[AlfaUiScreen.CreateRoom] = view;
            screens[AlfaUiScreen.JoinRoom] = view;
            var panel = CenteredPanel(view.transform, "OnlineFormCard", 1180f, 800f);
            onlineFormPanel = panel;

            var form = AlfaUiFactory.Node("OnlineForm", panel, typeof(CanvasGroup));
            AlfaUiFactory.Fill(form.GetComponent<RectTransform>());
            onlineFormGroup = form.GetComponent<CanvasGroup>();
            onlineFormTitle = factory.Title(form.transform, "Title", "JUGAR ONLINE", AlfaUiTheme.PanelTitleSize);
            Anchor(onlineFormTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -28f), new Vector2(-88f, 44f));
            var intro = factory.Caption(form.transform, "Intro", "CREÁ UNA SALA O UNITE A UNA EXISTENTE");
            intro.color = AlfaUiTheme.Moon200;
            Anchor(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(45f, -74f), new Vector2(-90f, 30f));

            var tabs = factory.Horizontal(form.transform, "OnlineTabs", 12f, TextAnchor.MiddleCenter);
            tabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-88f, 60f));
            onlineCreateTab = factory.Button(tabs, "OnlineCreateTab", "CREAR SALA", () => SwitchOnlineTab(true), AlfaButtonStyle.Tab, 60f, AlfaUiIconKind.House);
            onlineJoinTab = factory.Button(tabs, "OnlineJoinTab", "UNIRSE A SALA", () => SwitchOnlineTab(false), AlfaButtonStyle.Tab, 60f, AlfaUiIconKind.Enter);
            foreach (var tab in new[] { onlineCreateTab, onlineJoinTab }) factory.StrongLabel(tab, 26f);

            // Left column: identity, then either the room defaults (create) or the code (join), and the green CTA.
            var left = factory.Vertical(form.transform, "FormColumn", 8f);
            Anchor(left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -200f), new Vector2(520f, 470f));
            factory.Caption(left, "PlayerNameLabel", "TU NOMBRE");
            playerNameInput = factory.Input(left, "PlayerNameInput", "Cómo te dicen tus amigos", 24);
            playerNameInput.onSubmit.AddListener(_ => SubmitOnline());

            // Create: room defaults as UI-06 (map carousel, mode, humans). They are applied through the room rules
            // as soon as the room exists and stay editable inside the room. Capacity is fixed at RoomRules.Capacity,
            // so the stepper edits the humans per round instead of a maximum player count.
            var defaults = factory.Vertical(left, "RoomDefaults", 8f);
            onlineRoomDefaultsRow = defaults.gameObject;
            factory.Divider(defaults, "DefaultsGap", Color.clear, 4f);
            factory.Caption(defaults, "OnlineMapLabel", "MAPA");
            var carousel = factory.Horizontal(defaults, "OnlineMapCarousel", 8f, TextAnchor.MiddleCenter);
            carousel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 150f;
            onlineMapPrevious = CycleButton(carousel, "OnlineMapPrevious", AlfaUiIconKind.ChevronLeft, () => CycleOnlineMap(-1));
            onlineMapPrevious.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 150f;
            var frame = factory.Inset(carousel, "OnlineMapFrame", 150f, AlfaUiTheme.ButtonRadius);
            var frameLayout = frame.GetComponent<UnityEngine.UI.LayoutElement>();
            frameLayout.preferredWidth = 320f;
            frameLayout.flexibleWidth = 1f;
            onlineMapThumbnail = AlfaUiFactory.Node("OnlineMapThumbnail", frame, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            onlineMapThumbnail.raycastTarget = false;
            Anchor(onlineMapThumbnail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(300f, 110f));
            onlineMapPlaceholder = factory.Icon(frame, "OnlineMapPlaceholderIcon", AlfaUiIconKind.Map, AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, 0.85f));
            Anchor(onlineMapPlaceholder.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -63f), new Vector2(64f, 64f));
            onlineMapLabel = factory.Text(frame, "OnlineMapName", "CASA CON PATIO", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            onlineMapLabel.textWrappingMode = TextWrappingModes.NoWrap;
            onlineMapLabel.richText = false;
            Anchor(onlineMapLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(-16f, 30f));
            onlineMapNext = CycleButton(carousel, "OnlineMapNext", AlfaUiIconKind.ChevronRight, () => CycleOnlineMap(1));
            onlineMapNext.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 150f;
            onlineModeLabel = AddCycleField(defaults, "MODO", "OnlineMode", -1, 1, CycleOnlineMode, 128f, 250f);
            onlineHumansLabel = AddCycleField(defaults, "HUMANOS", "OnlineHumans", -1, 1, CycleOnlineHumans, 128f, 250f);

            // Join: the room code with paste, and a short hint about its format.
            var codeContainer = factory.Vertical(left, "RoomCodeRow", 8f);
            roomCodeRow = codeContainer.gameObject;
            factory.Divider(codeContainer, "CodeGap", Color.clear, 4f);
            factory.Caption(codeContainer, "RoomCodeLabel", "CÓDIGO DE SALA");
            var codeActions = factory.Horizontal(codeContainer, "CodeActions", 10f);
            codeActions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            roomCodeInput = factory.Input(codeActions, "RoomCodeInput", "XXXXX-XXXXX", 32, true);
            roomCodeInput.onValueChanged.AddListener(FormatRoomCodeWhileEditing);
            roomCodeInput.onSubmit.AddListener(_ => SubmitOnline());
            onlinePasteButton = factory.Button(codeActions, "PasteRoomCodeButton", "PEGAR", PasteRoomCode, AlfaButtonStyle.Secondary, 56f, AlfaUiIconKind.Copy);
            var pasteLayout = onlinePasteButton.GetComponent<UnityEngine.UI.LayoutElement>();
            pasteLayout.preferredWidth = 170f;
            pasteLayout.flexibleWidth = 0f;
            var codeHint = factory.Inset(codeContainer, "RoomCodeHint", 74f);
            var hintIcon = factory.Icon(codeHint, "KeyIcon", AlfaUiIconKind.Key, AlfaUiTheme.Lamp400);
            Anchor(hintIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(32f, 32f));
            var hintText = factory.Text(codeHint, "HintText", "El código tiene diez letras o números.\nPegalo con o sin guion.", AlfaUiTheme.MinTextSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            AlfaUiFactory.Fill(hintText.rectTransform, 62f, 14f, 6f, 6f);
            onlineStatus = factory.Text(left, "OnlineStatus", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            onlineStatus.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 30f;

            onlinePrimaryButton = factory.Button(form.transform, "OnlinePrimaryButton", "CREAR SALA", SubmitOnline, AlfaButtonStyle.Success, 76f, AlfaUiIconKind.Play, false);
            factory.StrongLabel(onlinePrimaryButton, AlfaUiTheme.CtaSize);
            onlinePrimaryLabel = onlinePrimaryButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            Anchor((RectTransform)onlinePrimaryButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 40f), new Vector2(520f, 76f));

            // Right column: what the current tab does, as UI-06 list rows. There is no public room browser:
            // rooms are private and joined by code, so this column never pretends to list open matches. The rows
            // are information, so they carry no status bar (a bar reads as a signal meter with no data behind it).
            var right = factory.Vertical(form.transform, "InfoColumn", 10f);
            Anchor(right, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -200f), new Vector2(528f, 420f));
            onlineInfoTitle = factory.Caption(right, "InfoTitle", "TU SALA PRIVADA");
            onlineInfoRows = new RectTransform[4];
            for (var i = 0; i < onlineInfoRows.Length; i++)
                onlineInfoRows[i] = factory.ListRow(right, "InfoRow" + (i + 1), AlfaUiIconKind.Info, string.Empty, string.Empty, Color.clear, 74f, false);

            onlineBackButton = factory.Button(form.transform, "OnlineBackButton", "VOLVER", ShowMainMenu, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Back);
            Anchor((RectTransform)onlineBackButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 48f), new Vector2(250f, 60f));

            BuildOnlineConnectionCards(view.transform);
            UpdateOnlineDefaultsView();
        }

        private void BuildOnlineConnectionCards(Transform view)
        {
            // UI-06 screen 11 over a full-screen #0E1A30 scrim at 80 %: an opaque connecting card with spinner and
            // CANCELAR inside it, and an error card with a neutral frame (red only on the icon and the title).
            onlineOverlay = AlfaUiFactory.Node("OnlineOverlay", view, typeof(UnityEngine.UI.Image));
            var overlayImage = onlineOverlay.GetComponent<UnityEngine.UI.Image>();
            overlayImage.color = AlfaUiTheme.Scrim;
            overlayImage.raycastTarget = true;
            AlfaUiFactory.Fill(onlineOverlay.GetComponent<RectTransform>());

            onlineConnectingCard = factory.Panel(onlineOverlay.transform, "OnlineConnectingCard", Color.white).gameObject;
            AlfaUiFactory.SetSurface(onlineConnectingCard.GetComponent<RectTransform>(), AlfaUiTheme.Hex("1A2E57"), AlfaUiTheme.Night700,
                AlfaUiTheme.Border, AlfaUiTheme.WithAlpha(Color.black, 0.5f));
            Anchor((RectTransform)onlineConnectingCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 262f));
            var wifi = factory.Icon(onlineConnectingCard.transform, "WifiIcon", AlfaUiIconKind.Wifi, AlfaUiTheme.Sky400);
            Anchor(wifi.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(48f, -82f), new Vector2(92f, 92f));
            onlineConnectingTitle = factory.Title(onlineConnectingCard.transform, "ConnectingTitle", "CONECTANDO…", AlfaUiTheme.PanelTitleSize);
            onlineConnectingTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(onlineConnectingTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(172f, -34f), new Vector2(-290f, 46f));
            onlineConnectingMessage = factory.Text(onlineConnectingCard.transform, "ConnectingMessage", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            Anchor(onlineConnectingMessage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(174f, -84f), new Vector2(-292f, 56f));
            var spinner = factory.Spinner(onlineConnectingCard.transform, "ConnectingSpinner", 64f, AlfaUiTheme.Sky400);
            Anchor(spinner, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-84f, -82f), new Vector2(64f, 64f));
            onlineCancelButton = factory.Button(onlineConnectingCard.transform, "OnlineCancelButton", "CANCELAR", RequestOnlineCancel, AlfaButtonStyle.Secondary, 56f, AlfaUiIconKind.Close);
            Anchor((RectTransform)onlineCancelButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(260f, 56f));

            onlineErrorCard = factory.Panel(onlineOverlay.transform, "OnlineErrorCard", Color.white).gameObject;
            AlfaUiFactory.SetSurface(onlineErrorCard.GetComponent<RectTransform>(), AlfaUiTheme.Hex("1A2E57"), AlfaUiTheme.Night700,
                AlfaUiTheme.Border, AlfaUiTheme.WithAlpha(Color.black, 0.5f));
            Anchor((RectTransform)onlineErrorCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 300f));
            var warning = factory.Icon(onlineErrorCard.transform, "WarningIcon", AlfaUiIconKind.Warning, AlfaUiTheme.StatusWarn);
            Anchor(warning.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(48f, -96f), new Vector2(100f, 100f));
            onlineErrorTitle = factory.Title(onlineErrorCard.transform, "ErrorTitle", "NO SE PUDO CONECTAR", AlfaUiTheme.PanelTitleSize, AlfaUiTheme.StatusWarn);
            onlineErrorTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(onlineErrorTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(180f, -36f), new Vector2(-220f, 46f));
            onlineErrorMessage = factory.Text(onlineErrorCard.transform, "ErrorMessage", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            Anchor(onlineErrorMessage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(182f, -88f), new Vector2(-222f, 84f));
            var errorActions = factory.Horizontal(onlineErrorCard.transform, "ErrorActions", 14f, TextAnchor.MiddleCenter);
            Anchor(errorActions, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-96f, 56f));
            onlineRetryButton = factory.Button(errorActions, "OnlineRetryButton", "REINTENTAR", SubmitOnline, AlfaButtonStyle.Primary, 56f, AlfaUiIconKind.Refresh, false);
            onlineRetryButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 260f;
            onlineRetryButton.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            onlineErrorBackButton = factory.Button(errorActions, "OnlineErrorBackButton", "VOLVER", DismissOnlineError, AlfaButtonStyle.Secondary, 56f, AlfaUiIconKind.Back);
            onlineErrorBackButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 220f;
            onlineErrorBackButton.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            onlineOverlay.SetActive(false);
        }

        private void BuildLobby()
        {
            var view = factory.View("LobbyOverlayView", transform, false);
            screens[AlfaUiScreen.Lobby] = view;

            // Floating nametags over each waiting-room character (UI-06 screen 3). Drawn first: panels stay on top.
            lobbyNametagRoot = (RectTransform)AlfaUiFactory.Node("LobbyNametags", view.transform).transform;
            AlfaUiFactory.Fill(lobbyNametagRoot);

            var safe = factory.SafeArea(view.transform, 36f, 36f, 28f, 32f);
            lobbySafeArea = safe;

            // Header: "ESPERANDO JUGADORES" with the X/N counter and map · mode, as UI-06 screen 3. The second line
            // turns into the ready banner (or a start countdown, when the room publishes one).
            var header = factory.Panel(safe, "Header", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.95f));
            Anchor(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1060f, 128f));
            var accent = AlfaUiFactory.Node("Accent", header, typeof(UnityEngine.UI.Image));
            var accentImage = accent.GetComponent<UnityEngine.UI.Image>();
            accentImage.sprite = AlfaUiSkin.Fill(6f);
            accentImage.type = UnityEngine.UI.Image.Type.Sliced;
            accentImage.color = AlfaUiTheme.Sky400;
            accentImage.raycastTarget = false;
            Anchor(accent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(7f, -28f));
            lobbyHeaderTitle = factory.Title(header, "Title", "ESPERANDO JUGADORES", AlfaUiTheme.HeaderTitleSize);
            lobbyHeaderTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(lobbyHeaderTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -18f), new Vector2(680f, 52f));
            lobbyHeaderNote = factory.Caption(header, "Note", "EL ANFITRIÓN INICIA LA RONDA CUANDO TODOS ESTÁN LISTOS");
            lobbyHeaderNote.color = AlfaUiTheme.Moon200;
            lobbyHeaderNote.richText = true;
            lobbyHeaderNote.overflowMode = TextOverflowModes.Overflow;
            Anchor(lobbyHeaderNote.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(37f, 20f), new Vector2(690f, 38f));
            var separator = AlfaUiFactory.Node("Separator", header, typeof(UnityEngine.UI.Image));
            separator.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f);
            separator.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Anchor(separator.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-318f, 0f), new Vector2(2f, -36f));
            lobbyCount = factory.Title(header, "PlayerCount", "1/16", 52f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopRight);
            lobbyCount.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(lobbyCount.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -12f), new Vector2(270f, 64f));
            lobbyMapMode = factory.Text(header, "MapMode", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.BottomRight, true);
            lobbyMapMode.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyMapMode.enableAutoSizing = true;
            lobbyMapMode.fontSizeMin = AlfaUiTheme.MinTextSize;
            lobbyMapMode.fontSizeMax = 22f;
            lobbyMapMode.overflowMode = TextOverflowModes.Ellipsis;
            Anchor(lobbyMapMode.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 18f), new Vector2(280f, 30f));

            // Left: players sized to their content (74-unit rows, at most ~520 units), scrollbar when they overflow.
            lobbyRosterPanel = factory.Panel(safe, "RosterPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.93f));
            Anchor(lobbyRosterPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -146f), new Vector2(470f, 400f));
            var roster = factory.Vertical(lobbyRosterPanel, "Roster", 8f);
            AlfaUiFactory.Fill(roster, 20f, 20f, 16f, 18f);
            factory.SectionHeader(roster, "RosterHeader", "JUGADORES", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            var roleNote = factory.Text(roster, "RoleNote", "Los roles se sortean en cada ronda.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            roleNote.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyMemberScroll = factory.ScrollView(roster, "MemberScrollView", out var memberContent, 400f, true);
            lobbyMemberScroll.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 0f;
            lobbyMemberScroll.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            AlfaUiFactory.SetFrame(lobbyMemberScroll, Color.clear);
            for (var i = 0; i < LetMeSleep.Core.RoomRules.Capacity; i++)
            {
                var rowPanel = factory.ListRow(memberContent, $"MemberPanel{i + 1}", AlfaUiIconKind.Human, string.Empty, string.Empty, AlfaUiTheme.StatusWarn, LobbyRowHeight);
                var title = rowPanel.Find("RowTitle").GetComponent<TextMeshProUGUI>();
                title.name = $"Member{i + 1}";
                rowPanel.gameObject.SetActive(false);
                memberRows.Add(title);
                memberSubtitles.Add(rowPanel.Find("RowSubtitle").GetComponent<TextMeshProUGUI>());
                memberRowRoots.Add(rowPanel.gameObject);
                memberStatusIcons.Add(rowPanel.Find("RowIcon").GetComponent<AlfaUiIcon>());
                memberStatusBars.Add(rowPanel.Find("StatusBar").GetComponent<UnityEngine.UI.Image>());
            }

            // Bottom-left: the voice panel where UI-06 draws the chat. There is no text chat protocol, so it shows
            // who is in the voice room, who is talking or muted, and the push-to-talk key.
            var voicePanel = factory.Panel(safe, "LobbyVoicePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.93f));
            Anchor(voicePanel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(440f, 190f));
            var voiceIcon = factory.Icon(voicePanel, "VoiceIcon", AlfaUiIconKind.Microphone, AlfaUiTheme.Sky400);
            Anchor(voiceIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(28f, 28f));
            var voiceTitle = factory.Text(voicePanel, "VoiceTitle", "VOZ DE LA SALA", 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft, true);
            voiceTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(voiceTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(58f, -12f), new Vector2(-150f, 36f));
            var pttChip = factory.Inset(voicePanel, "VoicePttChip", 34f, AlfaUiTheme.SmallRadius);
            Anchor(pttChip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -13f), new Vector2(92f, 34f));
            lobbyVoicePtt = factory.Text(pttChip, "VoicePttKey", "PTT V", AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center, true);
            lobbyVoicePtt.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(lobbyVoicePtt.rectTransform, 4f, 4f, 2f, 2f);
            lobbyVoiceLines = new TextMeshProUGUI[3];
            for (var i = 0; i < lobbyVoiceLines.Length; i++)
            {
                lobbyVoiceLines[i] = factory.Text(voicePanel, "VoiceLine" + (i + 1), string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
                lobbyVoiceLines[i].textWrappingMode = TextWrappingModes.NoWrap;
                lobbyVoiceLines[i].richText = true;
                Anchor(lobbyVoiceLines[i].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -56f - i * 30f), new Vector2(-44f, 30f));
            }
            lobbyVoiceHint = factory.Text(voicePanel, "VoiceHint", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.BottomLeft);
            lobbyVoiceHint.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(lobbyVoiceHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(22f, 12f), new Vector2(-44f, 28f));

            // Right: host rules for the next round, narrow (460) and collapsible so the scene stays visible.
            lobbyRulesPanel = factory.Panel(safe, "RulesPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.95f));
            Anchor(lobbyRulesPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(460f, 500f));
            var rulesFit = lobbyRulesPanel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rulesFit.padding = new RectOffset(22, 22, 16, 20);
            rulesFit.childControlWidth = rulesFit.childControlHeight = true;
            rulesFit.childForceExpandWidth = true;
            rulesFit.childForceExpandHeight = false;
            lobbyRulesPanel.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var rules = factory.Vertical(lobbyRulesPanel, "Rules", 10f);
            var rulesHeader = factory.Horizontal(rules, "RulesHeader", 10f, TextAnchor.MiddleLeft);
            rulesHeader.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44f;
            var badge = factory.Panel(rulesHeader, "Badge", AlfaUiTheme.WithAlpha(AlfaUiTheme.Lamp400, 0.2f), 40f, 40f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(badge, bottom: Color.white, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Lamp400, 0.55f), shadow: Color.clear);
            badge.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            var badgeIcon = factory.Icon(badge, "Symbol", AlfaUiIconKind.Play, AlfaUiTheme.Lamp400);
            AlfaUiFactory.Fill(badgeIcon.rectTransform, 9f, 9f, 9f, 9f);
            factory.Title(rulesHeader, "Label", "PRÓXIMA RONDA", 28f).textWrappingMode = TextWrappingModes.NoWrap;
            lobbyRoleBadge = factory.Text(rulesHeader, "LocalAuthority", "INVITADO", AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Right, true);
            lobbyRoleBadge.textWrappingMode = TextWrappingModes.NoWrap;
            var authorityLayout = lobbyRoleBadge.GetComponent<UnityEngine.UI.LayoutElement>();
            authorityLayout.preferredWidth = 110f;
            authorityLayout.flexibleWidth = 0f;
            lobbyRulesToggle = CycleButton(rulesHeader, "LobbyRulesToggle", AlfaUiIconKind.ChevronDown, ToggleLobbyRules);
            lobbyRulesToggle.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;
            lobbyRulesToggle.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = lobbyRulesToggle.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 44f;
            factory.Divider(rules, "AuthorityDivider", AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.5f));
            lobbyRulesSummary = factory.Text(rules, "RulesSummary", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Left, true);
            lobbyRulesSummary.richText = true;
            lobbyRulesSummary.lineSpacing = 6f;
            var editor = factory.Vertical(rules, "RulesEditor", 10f);
            lobbyRulesEditor = editor.gameObject;
            roomModeLabel = AddCycleField(editor, "MODO", "RoomMode", -1, 1, CycleRoomMode, 96f, 180f);
            roomModePrevious = roomModeLabel.transform.parent.parent.Find("RoomModePrevious").GetComponent<UnityEngine.UI.Button>();
            roomModeNext = roomModeLabel.transform.parent.parent.Find("RoomModeNext").GetComponent<UnityEngine.UI.Button>();
            roomDurationLabel = AddCycleField(editor, "TIEMPO", "RoomDuration", -1, 1, CycleRoomDuration, 96f, 180f);
            roomDurationPrevious = roomDurationLabel.transform.parent.parent.Find("RoomDurationPrevious").GetComponent<UnityEngine.UI.Button>();
            roomDurationNext = roomDurationLabel.transform.parent.parent.Find("RoomDurationNext").GetComponent<UnityEngine.UI.Button>();
            roomMapLabel = AddCycleField(editor, "MAPA", "RoomMap", -1, 1, CycleRoomMap, 96f, 180f);
            roomMapLabel.richText = false;
            roomMapLabel.enableAutoSizing = true;
            roomMapLabel.fontSizeMin = AlfaUiTheme.MinTextSize;
            roomMapLabel.fontSizeMax = 22f;
            roomMapLabel.overflowMode = TextOverflowModes.Ellipsis;
            roomMapPrevious = roomMapLabel.transform.parent.parent.Find("RoomMapPrevious").GetComponent<UnityEngine.UI.Button>();
            roomMapNext = roomMapLabel.transform.parent.parent.Find("RoomMapNext").GetComponent<UnityEngine.UI.Button>();
            UpdateRoomMapView();
            factory.Caption(editor, "HumanCountLabel", "CANTIDAD DE HUMANOS");
            var counts = factory.Horizontal(editor, "HumanCount", 6f, TextAnchor.MiddleLeft);
            for (var count = 0; count <= 5; count++)
            {
                var captured = count;
                var button = factory.Button(counts, "HumanCount" + count, count == 0 ? "AUTO" : count.ToString(),
                    () => ChangeLobbyHumanCount(captured == 0 ? (int?)null : captured), AlfaButtonStyle.Tab, 48f);
                var countLayout = button.GetComponent<UnityEngine.UI.LayoutElement>();
                countLayout.preferredWidth = count == 0 ? 96f : 52f;
                countLayout.flexibleWidth = 0f;
                humanCountLabels[count] = button.GetComponentInChildren<TextMeshProUGUI>();
                AlfaUiFactory.Fill(humanCountLabels[count].rectTransform, 4f, 4f, 4f, 4f);
            }
            lobbyStartReason = factory.Text(rules, "StartReason", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Left);
            lobbyStartButton = factory.Button(rules, "LobbyStartButton", "INICIAR RONDA", BeginRound, AlfaButtonStyle.Success, 64f, AlfaUiIconKind.Play);
            factory.StrongLabel(lobbyStartButton, AlfaUiTheme.CtaSize);
            lobbyStartLabel = lobbyStartButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            // Bottom-right action stack: room code, invite (copies the code), explore / customise and the big LISTO.
            var actionsStack = factory.Vertical(safe, "LobbyActionsStack", 12f, TextAnchor.LowerRight);
            actionsStack.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            Anchor(actionsStack, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(520f, 0f));
            lobbyStatus = factory.Text(actionsStack, "LobbyStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Right);
            lobbyStatus.textWrappingMode = TextWrappingModes.Normal;
            var codeRow = factory.Horizontal(actionsStack, "CodeRow", 12f, TextAnchor.MiddleRight);
            codeRow.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60f;
            var codeChip = factory.Inset(codeRow, "RoomCodeChip", 60f);
            var chipLayout = codeChip.GetComponent<UnityEngine.UI.LayoutElement>();
            chipLayout.preferredWidth = 226f;
            chipLayout.flexibleWidth = 0f;
            var keyIcon = factory.Icon(codeChip, "KeyIcon", AlfaUiIconKind.Key, AlfaUiTheme.Lamp400);
            Anchor(keyIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(28f, 28f));
            lobbyCode = factory.Text(codeChip, "RoomCode", "PREPARANDO…", 26f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center, true);
            lobbyCode.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyCode.characterSpacing = 3f;
            lobbyCode.enableAutoSizing = true;
            lobbyCode.fontSizeMin = AlfaUiTheme.MinTextSize;
            lobbyCode.fontSizeMax = 26f;
            AlfaUiFactory.Fill(lobbyCode.rectTransform, 46f, 8f, 6f, 6f);
            lobbyCopyButton = factory.Button(codeRow, "LobbyCopyButton", "INVITAR AMIGOS", () =>
            {
                if (lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.RoomCode))
                {
                    actions.CopyRoomCode(lobbyState.RoomCode);
                    lobbyStatus.text = "Código copiado. Pasáselo a tus amigos.";
                }
            }, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Invite);
            lobbyCopyButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 282f;
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
            }, AlfaButtonStyle.Success, 84f, AlfaUiIconKind.Ready);
            factory.StrongLabel(lobbyReadyButton, AlfaUiTheme.CtaSize);
            lobbyReadyLabel = lobbyReadyButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        private const float LobbyRowHeight = 74f;
        private const float LobbyRowSpacing = 8f;
        private const float LobbyListMaxHeight = 520f;

        /// <summary>
        /// Sizes the roster to its rows (74 units each, at most ~520 units of list) and keeps it clear of the
        /// voice panel at any aspect ratio. Longer rosters scroll with a visible bar.
        /// </summary>
        private void LayoutLobbyRoster()
        {
            if (lobbyRosterPanel == null || lobbyMemberScroll == null || lobbyState == null) return;
            var rows = Mathf.Max(1, lobbyState.Members.Count);
            var content = rows * LobbyRowHeight + (rows - 1) * LobbyRowSpacing + 12f;
            const float chrome = 16f + 48f + 8f + 28f + 8f + 18f;
            var available = lobbySafeArea != null ? lobbySafeArea.rect.height - 146f - 190f - 18f - chrome : LobbyListMaxHeight;
            var list = Mathf.Max(LobbyRowHeight + 12f, Mathf.Min(content, Mathf.Min(LobbyListMaxHeight, available)));
            var layout = lobbyMemberScroll.GetComponent<UnityEngine.UI.LayoutElement>();
            if (!Mathf.Approximately(layout.preferredHeight, list))
            {
                layout.preferredHeight = list;
                layout.minHeight = list;
                lobbyRosterPanel.sizeDelta = new Vector2(lobbyRosterPanel.sizeDelta.x, list + chrome);
            }
        }

        private void ToggleLobbyRules()
        {
            lobbyRulesCollapsed = !lobbyRulesCollapsed;
            lobbyRulesUserChoice = true;
            UpdateLobbyRulesView();
        }

        private void UpdateLobbyRulesView()
        {
            if (lobbyRulesEditor == null || lobbyState == null) return;
            var owner = lobbyState.IsOwner;
            // Guests only read the rules: a summary. The host edits them and can fold the panel away.
            var collapsed = !owner || lobbyRulesCollapsed;
            lobbyRulesEditor.SetActive(!collapsed);
            lobbyRulesSummary.gameObject.SetActive(collapsed);
            lobbyRulesToggle.gameObject.SetActive(owner);
            var chevron = lobbyRulesToggle.GetComponentInChildren<AlfaUiIcon>(true);
            if (chevron != null) chevron.transform.localRotation = Quaternion.Euler(0f, 0f, collapsed ? 0f : 180f);
            var humans = lobbyState.HumanCount.HasValue ? lobbyState.HumanCount.Value.ToString() : "AUTO";
            lobbyRulesSummary.text = "MODO  <color=#F2F6FF>" + AlfaModeText.Name(lobbyState.ModeId) + "</color>    TIEMPO  <color=#F2F6FF>" +
                FormatClock(lobbyState.RoundSeconds) + "</color>\nMAPA  <color=#F2F6FF>" + (roomMapLabel != null ? roomMapLabel.text : lobbyState.MapLabel) +
                "</color>    HUMANOS  <color=#F2F6FF>" + humans + "</color>";
        }

        /// <summary>Voice panel lines: speakers first, then muted and connected participants.</summary>
        private void UpdateLobbyVoicePanel()
        {
            if (lobbyVoiceLines == null || voiceState == null) return;
            lobbyVoicePtt.text = "PTT " + voiceState.BindingLabel;
            var participants = voiceState.Participants
                .OrderByDescending(item => item.Speaking && !item.Muted)
                .ThenBy(item => item.Muted)
                .ToList();
            for (var i = 0; i < lobbyVoiceLines.Length; i++)
            {
                var line = lobbyVoiceLines[i];
                if (i == lobbyVoiceLines.Length - 1 && participants.Count > lobbyVoiceLines.Length)
                {
                    line.text = "<color=#A8B8D8>+" + (participants.Count - lobbyVoiceLines.Length + 1) + " más en la sala</color>";
                    continue;
                }
                if (i >= participants.Count) { line.text = string.Empty; continue; }
                var participant = participants[i];
                var state = participant.Muted ? "<color=#A8B8D8>silenciado</color>" : participant.Speaking ? "<color=#57D26B>hablando</color>" : "<color=#A8B8D8>conectado</color>";
                line.text = "<b>" + Escape(participant.DisplayName) + "</b>  ·  " + state;
            }
            lobbyVoiceHint.text = !voiceState.InRoom ? "La voz se activa en la sala." : voiceState.LocalMuted ? "Tu micrófono está silenciado." :
                !voiceState.DeviceAvailable ? "Elegí un micrófono en AJUSTES." : participants.Count == 0 ? "Mantené " + voiceState.BindingLabel + " para hablar. Nadie más conectado." :
                "Mantené " + voiceState.BindingLabel + " para hablar.";
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("<", "‹").Replace(">", "›");

        /// <summary>
        /// Floating nametags over each waiting-room character, when the actions expose the lobby avatars
        /// (<see cref="ILobbyPresenceSource"/>). Name plus a ready dot; hidden behind the camera or off screen.
        /// </summary>
        private void UpdateLobbyNametags()
        {
            if (lobbyNametagRoot == null) return;
            var source = lobbyPresenceOverride ?? actions as ILobbyPresenceSource;
            var visible = screen == AlfaUiScreen.Lobby && !lobbyExploring && lobbyState != null && source != null;
            var used = 0;
            if (visible)
            {
                var canvas = GetComponent<Canvas>();
                foreach (var member in lobbyState.Members)
                {
                    if (!member.Connected || !source.TryGetLobbyAvatar(member.Id, out var avatar, out var worldCamera) || avatar == null || worldCamera == null) continue;
                    var screenPoint = worldCamera.WorldToScreenPoint(NametagAnchor(avatar));
                    if (screenPoint.z <= 0.05f) continue;
                    var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(lobbyNametagRoot, screenPoint, eventCamera, out var local)) continue;
                    if (!lobbyNametagRoot.rect.Contains(local)) continue;
                    var tag = NametagAt(used++);
                    tag.gameObject.SetActive(true);
                    tag.anchoredPosition = local;
                    tag.Find("Name").GetComponent<TextMeshProUGUI>().text = member.Name;
                    tag.Find("Dot").GetComponent<UnityEngine.UI.Image>().color = member.Ready ? AlfaUiTheme.StatusOk : AlfaUiTheme.StatusWarn;
                }
            }
            for (var i = used; i < lobbyNametags.Count; i++) lobbyNametags[i].gameObject.SetActive(false);
        }

        private readonly Dictionary<Transform, Transform> nametagHeads = new Dictionary<Transform, Transform>();

        /// <summary>World point just above the character's head: head bone (humanoid rig or a bone named like a
        /// head, cached per avatar) plus clearance for the nightcap, else the top of its meshes.</summary>
        private Vector3 NametagAnchor(Transform avatar)
        {
            if (!nametagHeads.TryGetValue(avatar, out var head) || (head == null && !ReferenceEquals(head, null)))
            {
                var animator = avatar.GetComponentInChildren<Animator>();
                head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head == null)
                    head = avatar.GetComponentsInChildren<Transform>().FirstOrDefault(item =>
                        item.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 && item.GetComponent<Renderer>() == null);
                if (nametagHeads.Count > 64) nametagHeads.Clear();
                nametagHeads[avatar] = head;
            }
            if (head != null) return head.position + Vector3.up * 0.52f * Mathf.Max(0.01f, avatar.lossyScale.y);
            var renderers = avatar.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return avatar.position + Vector3.up * 2f;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return new Vector3(bounds.center.x, bounds.max.y + 0.22f, bounds.center.z);
        }

        private RectTransform NametagAt(int index)
        {
            while (lobbyNametags.Count <= index)
            {
                var tag = factory.Panel(lobbyNametagRoot, "Nametag" + lobbyNametags.Count, AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.82f), -1f, -1f, AlfaUiTheme.SmallRadius);
                AlfaUiFactory.SetSurface(tag, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f), shadow: AlfaUiTheme.WithAlpha(Color.black, 0.3f));
                tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0.5f);
                tag.pivot = new Vector2(0.5f, 0f);
                var fitter = tag.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                fitter.padding = new RectOffset(12, 14, 4, 4);
                fitter.spacing = 8f;
                fitter.childAlignment = TextAnchor.MiddleCenter;
                fitter.childControlWidth = fitter.childControlHeight = true;
                fitter.childForceExpandWidth = fitter.childForceExpandHeight = false;
                var size = tag.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                size.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                size.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var dot = AlfaUiFactory.Node("Dot", tag, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
                dot.GetComponent<UnityEngine.UI.Image>().sprite = AlfaUiSkin.Circle();
                dot.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                var dotLayout = dot.GetComponent<UnityEngine.UI.LayoutElement>();
                dotLayout.preferredWidth = dotLayout.preferredHeight = dotLayout.minWidth = dotLayout.minHeight = 12f;
                var name = factory.Text(tag, "Name", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.richText = false;
                name.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
                lobbyNametags.Add(tag);
            }
            return lobbyNametags[index];
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            if (screen == AlfaUiScreen.Lobby) LayoutLobbyRoster();
            UpdateLobbyNametags();
        }

        private void BuildTraining(AlfaUiDependencies dependencies)
        {
            var view = factory.View("TrainingView", transform, false);
            SetSceneScrim(view, 0.55f);
            screens[AlfaUiScreen.Training] = view;
            portraitSetup = dependencies?.Preview;
            var panel = CenteredPanel(view.transform, "TrainingCard", 1300f, 900f);
            var title = factory.Title(panel, "Title", "ENTRENAMIENTO", AlfaUiTheme.PanelTitleSize);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -28f), new Vector2(-88f, 44f));
            var intro = factory.Caption(panel, "Intro", "PRACTICÁ, APRENDÉ Y MEJORÁ TUS HABILIDADES");
            intro.color = AlfaUiTheme.Moon200;
            Anchor(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(45f, -74f), new Vector2(-90f, 30f));

            var cards = factory.Horizontal(panel, "RoleCards", 36f, TextAnchor.UpperCenter);
            cards.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            cards.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = true;
            Anchor(cards, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(-88f, 560f));
            trainingHumanButton = BuildTrainingRoleCard(cards, AlfaRole.Human, out trainingHumanLabel);
            trainingMosquitoButton = BuildTrainingRoleCard(cards, AlfaRole.Mosquito, out trainingMosquitoLabel);

            var options = factory.Horizontal(panel, "TrainingOptions", 24f, TextAnchor.MiddleLeft);
            options.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(options, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(-88f, 56f));
            trainingModeLabel = AddCycleField(options, "MODO", "TrainingMode", -1, 1, CycleTrainingMode, 96f, 230f);
            trainingModePrevious = trainingModeLabel.transform.parent.parent.Find("TrainingModePrevious").GetComponent<UnityEngine.UI.Button>();
            trainingModeNext = trainingModeLabel.transform.parent.parent.Find("TrainingModeNext").GetComponent<UnityEngine.UI.Button>();
            trainingMapLabel = AddCycleField(options, "MAPA", "TrainingMap", -1, 1, CycleTrainingMap, 96f, 300f);
            trainingMapPrevious = trainingMapLabel.transform.parent.parent.Find("TrainingMapPrevious").GetComponent<UnityEngine.UI.Button>();
            trainingMapNext = trainingMapLabel.transform.parent.parent.Find("TrainingMapNext").GetComponent<UnityEngine.UI.Button>();

            trainingBackButton = factory.Button(panel, "TrainingBackButton", "VOLVER", BackFromTraining, AlfaButtonStyle.Secondary, 60f, AlfaUiIconKind.Back);
            Anchor((RectTransform)trainingBackButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 36f), new Vector2(250f, 60f));
            trainingBackLabel = trainingBackButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            trainingStatus = factory.Text(panel, "Status", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            Anchor(trainingStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(318f, 32f), new Vector2(-362f, 68f));
            UpdateTrainingMapView();
        }

        private UnityEngine.UI.Button BuildTrainingRoleCard(Transform parent, AlfaRole role, out TextMeshProUGUI startLabel)
        {
            var human = role == AlfaRole.Human;
            var team = human ? AlfaUiTheme.TeamHuman : AlfaUiTheme.TeamMosquito;
            var card = factory.Panel(parent, human ? "HumanCard" : "MosquitoCard", Color.white);
            AlfaUiFactory.SetSurface(card,
                human ? AlfaUiTheme.Hex("1D4B86") : AlfaUiTheme.Hex("5E2231"),
                human ? AlfaUiTheme.Hex("102A52") : AlfaUiTheme.Hex("2E1428"),
                AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.25f), 0.9f));
            var cardTitle = factory.Title(card, "CardTitle", human ? "ENTRENAR COMO HUMANO" : "ENTRENAR COMO MOSQUITO", AlfaUiTheme.PanelTitleSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Top);
            cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-32f, 46f));

            // Image area: 546 x 296 at 1080p, filled edge to edge by the role portrait (see ApplyTrainingPortrait).
            var portraitFrame = factory.Inset(card, "PortraitFrame", -1f, AlfaUiTheme.ButtonRadius);
            AlfaUiFactory.SetSurface(portraitFrame, AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.black, 0.35f), 0.9f),
                AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.black, 0.7f), 0.95f), Color.clear);
            portraitFrame.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            Anchor(portraitFrame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(-42f, 296f));
            var mask = AlfaUiFactory.Node("PortraitMask", portraitFrame, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            var maskImage = mask.GetComponent<UnityEngine.UI.Image>();
            maskImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            maskImage.type = UnityEngine.UI.Image.Type.Sliced;
            maskImage.raycastTarget = false;
            mask.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            AlfaUiFactory.Fill(mask.GetComponent<RectTransform>());
            var glow = AlfaUiFactory.Node("PortraitGlow", mask.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            glow.sprite = AlfaUiSkin.LargeCircle();
            glow.color = AlfaUiTheme.WithAlpha(team, 0.28f);
            glow.raycastTarget = false;
            Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 250f));
            var symbol = factory.Icon(mask.transform, "PortraitIcon", human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito, Color.Lerp(team, Color.white, 0.35f));
            Anchor(symbol.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            var raw = AlfaUiFactory.Node("Portrait", mask.transform, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            raw.raycastTarget = false;
            raw.enabled = false;
            AlfaUiFactory.Fill(raw.rectTransform);
            // Team light over the portrait bottom, as the sketch's blue / red lighting.
            var shade = AlfaUiFactory.Node("PortraitShade", mask.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            shade.sprite = AlfaUiFactory.VerticalFadeSprite();
            shade.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.black, 0.25f), 0.55f);
            shade.raycastTarget = false;
            Anchor(shade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 130f));
            var ring = AlfaUiFactory.Node("PortraitRing", portraitFrame, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            ring.sprite = AlfaUiSkin.Ring(AlfaUiTheme.ButtonRadius);
            ring.type = UnityEngine.UI.Image.Type.Sliced;
            ring.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.3f), 0.6f);
            ring.raycastTarget = false;
            AlfaUiFactory.Fill(ring.rectTransform);
            if (human) trainingHumanPortrait = raw; else trainingMosquitoPortrait = raw;

            var description = factory.Text(card, "Description", human ? "APRENDÉ A DEFENDERTE\nRECORRÉ EL MAPA Y USÁ OBJETOS" :
                "PRACTICÁ EL VUELO\nEXPLORÁ Y MOLESTÁ SIN LÍMITES", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            description.characterSpacing = 2f;
            description.lineSpacing = 6f;
            Anchor(description.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 106f), new Vector2(-40f, 70f));

            var start = factory.Button(card, human ? "TrainingHumanButton" : "TrainingMosquitoButton", "INICIAR",
                () => StartTrainingIntent(role, false), human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger, 72f, AlfaUiIconKind.Play);
            factory.StrongLabel(start, AlfaUiTheme.CtaSize);
            startLabel = start.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            Anchor((RectTransform)start.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(-48f, 72f));
            return start;
        }

        /// <summary>
        /// Fills each training card image area (546 x 296 at 1080p). Order: an illustration in
        /// Resources/AlfaUiPortraits/Human or /Mosquito (another team paints them; PNG as Sprite or Texture, drawn
        /// "cover" so it fills the area), else a one-off snapshot of the in-game model rendered through the
        /// customization preview rig with blue (human) or red (mosquito) lighting, else the large role pictogram.
        /// </summary>
        private void EnsureTrainingPortraits()
        {
            ApplyTrainingPortrait(trainingHumanPortrait, AlfaRole.Human);
            ApplyTrainingPortrait(trainingMosquitoPortrait, AlfaRole.Mosquito);
        }

        private void ApplyTrainingPortrait(UnityEngine.UI.RawImage target, AlfaRole role)
        {
            if (target == null || target.texture != null) return;
            Texture texture = null;
            var illustration = LoadRolePortrait(role);
            if (illustration != null) texture = illustration.texture;
            else if (portraitSetup != null && portraitSetup.IsUsable && (previewOrbit == null || screen != AlfaUiScreen.Customization))
            {
                var rendered = AlfaRolePortrait.Render(portraitSetup, role, 1092, 592);
                if (rendered != null)
                {
                    renderedPortraits.Add(rendered);
                    texture = rendered;
                }
            }
            if (texture == null) return;
            target.texture = texture;
            // The image area is a fixed 546 x 296 frame; its rect may not be laid out yet on first show.
            const float areaAspect = 546f / 296f;
            var textureAspect = texture.height > 0 ? (float)texture.width / texture.height : areaAspect;
            // Cover: crop the longer side instead of letterboxing.
            target.uvRect = textureAspect > areaAspect
                ? new Rect((1f - areaAspect / textureAspect) * 0.5f, 0f, areaAspect / textureAspect, 1f)
                : new Rect(0f, (1f - textureAspect / areaAspect) * 0.5f, 1f, textureAspect / areaAspect);
            target.enabled = true;
            foreach (var name in new[] { "PortraitGlow", "PortraitIcon" })
            {
                var fallback = target.transform.parent.Find(name);
                if (fallback != null) fallback.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Role illustration for the training cards: Resources/AlfaUiPortraits/Human and
        /// Resources/AlfaUiPortraits/Mosquito (PNG imported as Sprite, or as a plain texture).
        /// </summary>
        internal static Sprite LoadRolePortrait(AlfaRole role)
        {
            var path = "AlfaUiPortraits/" + (role == AlfaRole.Human ? "Human" : "Mosquito");
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            foreach (var texture in renderedPortraits)
            {
                if (texture == null) continue;
                texture.Release();
                Destroy(texture);
            }
            renderedPortraits.Clear();
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
            AlfaUiFactory.Fill(previewViewport.GetComponent<RectTransform>(), 20f, 20f, 118f, 104f);
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
            Anchor(stageHeader, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, 80f));
            customizationPreviewIcon = factory.Icon(stageHeader, "StageMark", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            Anchor(customizationPreviewIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(40f, 40f));
            customizationPreviewTitle = factory.Text(stageHeader, "Title", "VISTA EN VIVO · HUMANO", 25f,
                AlfaUiTheme.Sheet100, TextAlignmentOptions.BottomLeft, true);
            AlfaUiFactory.Fill(customizationPreviewTitle.rectTransform, 72f, 18f, 6f, 40f);
            var orbitHint = factory.Text(stageHeader, "OrbitHint", "ARRASTRÁ PARA GIRAR · RUEDA PARA ZOOM", AlfaUiTheme.MinTextSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft, true);
            orbitHint.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(orbitHint.rectTransform, 72f, 18f, 42f, 4f);
            var unavailable = factory.Text(previewPanel, "PreviewUnavailable", "El visor 3D se conecta al personaje del juego.", AlfaUiTheme.BodySize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            Anchor(unavailable.rectTransform, new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            unavailable.gameObject.SetActive(!previewOrbit.IsBound);
            var angles = factory.Horizontal(previewPanel, "PreviewAngles", 8f, TextAnchor.MiddleCenter);
            Anchor(angles, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, 66f));
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewFrontButton", "FRENTE", () => previewOrbit.SetAngle(PreviewAngle.Front), false, false, 66f), 22f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewSideButton", "PERFIL", () => previewOrbit.SetAngle(PreviewAngle.Side), false, false, 66f), 22f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewBackButton", "ESPALDA", () => previewOrbit.SetAngle(PreviewAngle.Back), false, false, 66f), 22f);
            AlfaUiFactory.QuietButton(factory.Button(angles, "PreviewResetButton", "CENTRAR", () => previewOrbit.ResetView(), false, false, 66f), 22f);

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
            AlfaUiFactory.QuietButton(customizationResetButton, 22f);
            AlfaUiFactory.QuietButton(factory.Button(secondary, "CustomizationBackButton", "VOLVER", CloseCustomization, false, false, 66f, AlfaUiIconKind.Back), 22f);
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
            AlfaUiFactory.QuietButton(factory.Button(buttons, "SettingsResetButton", "DESHACER CAMBIOS", ResetSettings, false, false, 66f), 22f);
            AlfaUiFactory.QuietButton(factory.Button(buttons, "SettingsBackButton", "VOLVER", CloseSettings, false, false, 66f, AlfaUiIconKind.Back), 22f);
        }

        private void BuildHud()
        {
            var view = factory.View("GameplayHudView", transform, false);
            screens[AlfaUiScreen.Gameplay] = view;
            var role = factory.Panel(view.transform, "RoleBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.78f));
            Anchor(role, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(188f, 52f));
            hudRoleBackground = role.GetComponent<UnityEngine.UI.Image>();
            hudRoleIcon = factory.Icon(role, "RoleIcon", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            Anchor(hudRoleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(30f, 30f));
            hudRoleLabel = factory.Text(role, "RoleLabel", "HUMANO", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudRoleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudRoleLabel.rectTransform, 46f, 10f, 4f, 4f);

            var clock = factory.Panel(view.transform, "ClockBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -22f), new Vector2(156f, 60f));
            var clockIcon = factory.Icon(clock, "ClockIcon", AlfaUiIconKind.Clock, AlfaUiTheme.Lamp400);
            Anchor(clockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(28f, 28f));
            hudClock = factory.Text(clock, "Clock", "03:00", 32f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudClock.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudClock.rectTransform, 44f, 10f, 4f, 4f);

            var blood = factory.Panel(view.transform, "BloodBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(blood, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(6f, -22f), new Vector2(290f, 60f));
            var bloodIcon = factory.Icon(blood, "BloodIcon", AlfaUiIconKind.Blood, AlfaUiTheme.Pajama500); hudScoreIcon = bloodIcon;
            Anchor(bloodIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(30f, 30f));
            hudBlood = factory.Text(blood, "Blood", "SANGRE  0 / 20", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudBlood.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudBlood.rectTransform, 50f, 12f, 3f, 17f);
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

            hudNetwork = factory.Text(view.transform, "NetworkState", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Right);
            Anchor(hudNetwork.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(460f, 56f));
            // Push-to-talk / voice state on a chip (text.secondary on ink) so it reads over any sky or wall.
            var voiceChip = factory.Panel(view.transform, "VoiceChip", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.82f), -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(voiceChip, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f), shadow: AlfaUiTheme.WithAlpha(Color.black, 0.3f));
            voiceChip.anchorMin = voiceChip.anchorMax = voiceChip.pivot = new Vector2(1f, 1f);
            voiceChip.anchoredPosition = new Vector2(-28f, -84f);
            var chipLayout = voiceChip.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(12, 14, 5, 5);
            chipLayout.spacing = 8f;
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
            var chipFit = voiceChip.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            chipFit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            chipFit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var mic = factory.Icon(voiceChip, "VoiceIcon", AlfaUiIconKind.Microphone, AlfaUiTheme.Moon200);
            var micLayout = mic.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            micLayout.preferredWidth = micLayout.preferredHeight = micLayout.minWidth = micLayout.minHeight = 22f;
            hudVoice = factory.Text(voiceChip, "VoiceState", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center, true);
            hudVoice.textWrappingMode = TextWrappingModes.NoWrap;
            hudVoice.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            hudVoiceChip = voiceChip.gameObject;
            hudVoiceChip.SetActive(false);

            hudPromptPanel = factory.Panel(view.transform, "InteractionPrompt", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f)).gameObject;
            // Keep a deliberate gutter before the right-aligned equipment belt at 720p.
            Anchor(hudPromptPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(440f, 54f));
            hudInteraction = factory.Text(hudPromptPanel.transform, "Interaction", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudInteraction.rectTransform, 16f, 16f, 6f, 6f);

            hudStatePanel = factory.Panel(view.transform, "ActorStatePanel", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.9f)).gameObject;
            Anchor(hudStatePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(440f, 64f));
            hudActorState = factory.Text(hudStatePanel.transform, "ActorState", string.Empty, 22f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudActorState.rectTransform, 16f, 16f, 5f, 20f);
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
            Anchor(hudHintPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(640f, 78f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            hudHint.textWrappingMode = TextWrappingModes.Normal;
            hudHint.enableAutoSizing = false;
            hudHint.overflowMode = TextOverflowModes.Overflow;
            AlfaUiFactory.Fill(hudHint.rectTransform, 16f, 16f, 10f, 10f);
            hudLives = factory.Text(view.transform, "Lives", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            Anchor(hudLives.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -84f), new Vector2(280f, 34f));
            hudTaskPanel = factory.Panel(view.transform, "PrivateTask", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, .9f)).gameObject;
            Anchor(hudTaskPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -128f), new Vector2(440f, 124f));
            hudTask = factory.Text(hudTaskPanel.transform, "PrivateTaskText", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            hudTask.textWrappingMode = TextWrappingModes.Normal;
            AlfaUiFactory.Fill(hudTask.rectTransform, 18f, 18f, 10f, 26f);
            var taskTrack = AlfaUiFactory.Node("TaskProgress", hudTaskPanel.transform, typeof(UnityEngine.UI.Image));
            taskTrack.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            Anchor(taskTrack.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(.5f, 0f), new Vector2(18f, 12f), new Vector2(-36f, 8f));
            hudTaskFill = AlfaUiFactory.Node("Fill", taskTrack.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudTaskFill.color = AlfaUiTheme.Mint400; hudTaskFill.raycastTarget = false; AlfaUiFactory.Fill(hudTaskFill.rectTransform);
            hudTaskPanel.SetActive(false);

            // Private equipment belt: hands plus three slots. The selected slot is the primary blue plate with the
            // 3-unit accent frame (UI-06 slots); the number sits top-left, no text markers.
            hudEquipmentPanel = factory.Panel(view.transform, "PrivateEquipment", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, .80f)).gameObject;
            hudEquipmentRect = hudEquipmentPanel.GetComponent<RectTransform>();
            Anchor(hudEquipmentRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 26f), new Vector2(700f, EquipmentBaseHeight));
            for (int i = 0; i < 4; i++)
            {
                var slot = factory.Panel(hudEquipmentPanel.transform, "EquipmentSlotPlate" + i,
                    new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, .82f));
                Anchor(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f + i * 169f, -10f), new Vector2(160f, EquipmentSlotHeight));
                AlfaUiFactory.SetSurface(slot, shadow: Color.clear);
                hudEquipmentSlots[i] = slot.GetComponent<UnityEngine.UI.Image>();
                var number = factory.Text(slot, "EquipmentNumber" + i, i.ToString(), 22f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft, true);
                number.name = "EquipmentNumber" + i;
                Anchor(number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -3f), new Vector2(26f, 30f));
                hudEquipmentNumbers[i] = number;
                hudEquipmentIcons[i] = factory.Icon(slot, "EquipmentIcon" + i, i == 0 ? AlfaUiIconKind.Hands : AlfaUiIconKind.None, AlfaUiTheme.Moon200);
                Anchor(hudEquipmentIcons[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -6f), new Vector2(28f, 28f));
                hudEquipmentLabels[i] = factory.Text(slot, "EquipmentSlot" + i, i == 0 ? "MANOS" : "VACÍO", AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft, true);
                hudEquipmentLabels[i].textWrappingMode = TextWrappingModes.Normal;
                hudEquipmentLabels[i].overflowMode = TextOverflowModes.Overflow;
                hudEquipmentLabels[i].characterSpacing = 1f;
                hudEquipmentLabels[i].lineSpacing = -8f;
                AlfaUiFactory.Fill(hudEquipmentLabels[i].rectTransform, 10f, 6f, 36f, 2f);
            }
            hudStaminaLabel = factory.Text(hudEquipmentPanel.transform, "StaminaLabel", "ESTAMINA  100%", AlfaUiTheme.MinTextSize, AlfaUiTheme.Mint400, TextAlignmentOptions.Left, true);
            hudStaminaLabelRect = hudStaminaLabel.rectTransform;
            Anchor(hudStaminaLabelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -(EquipmentSlotHeight + 16f)), new Vector2(-28f, 32f));
            var staminaTrack = AlfaUiFactory.Node("StaminaTrack", hudEquipmentPanel.transform, typeof(UnityEngine.UI.Image));
            staminaTrack.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            hudStaminaTrackRect = staminaTrack.GetComponent<RectTransform>();
            Anchor(hudStaminaTrackRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -(EquipmentSlotHeight + 52f)), new Vector2(-28f, 8f));
            hudStaminaFill = AlfaUiFactory.Node("Fill", staminaTrack.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudStaminaFill.color = AlfaUiTheme.Mint400; hudStaminaFill.raycastTarget = false; AlfaUiFactory.Fill(hudStaminaFill.rectTransform);
            hudThrowTrack = AlfaUiFactory.Node("ThrowCharge", hudEquipmentPanel.transform).gameObject;
            hudThrowRect = hudThrowTrack.GetComponent<RectTransform>();
            Anchor(hudThrowRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -EquipmentBaseHeight), new Vector2(-28f, 50f));
            hudThrowLabel = factory.Text(hudThrowTrack.transform, "ThrowLabel", "CARGA PANTUFLA", AlfaUiTheme.MinTextSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            Anchor(hudThrowLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 34f));
            var throwBar = AlfaUiFactory.Node("Track", hudThrowTrack.transform, typeof(UnityEngine.UI.Image));
            throwBar.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            Anchor(throwBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(0f, 8f));
            hudThrowFill = AlfaUiFactory.Node("Fill", throwBar.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudThrowFill.color = AlfaUiTheme.Lamp400; hudThrowFill.raycastTarget = false; AlfaUiFactory.Fill(hudThrowFill.rectTransform);
            hudSwapOffer = factory.Text(hudEquipmentPanel.transform, "SwapOffer", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Left, true);
            hudSwapOffer.textWrappingMode = TextWrappingModes.Normal;
            hudSwapOffer.overflowMode = TextOverflowModes.Overflow;
            hudSwapRect = hudSwapOffer.rectTransform;
            Anchor(hudSwapRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -EquipmentBaseHeight), new Vector2(-28f, 60f));
            hudEquipmentPanel.SetActive(false);
            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100); hudReticle = reticle.gameObject;
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        private const float EquipmentSlotHeight = 122f;
        private const float EquipmentBaseHeight = EquipmentSlotHeight + 76f;

        private void UpdateEquipmentLayout(bool charging, bool hasSwapOffer)
        {
            var nextTop = EquipmentBaseHeight - 6f;
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
            hudEquipmentRect.sizeDelta = new Vector2(700f, charging || hasSwapOffer ? nextTop + 4f : EquipmentBaseHeight);
        }

        /// <summary>Equipment slot plate: selected = primary blue with the 3-unit accent.blue frame.</summary>
        private void SetEquipmentSlotSelected(int index, bool selected)
        {
            var plate = hudEquipmentSlots[index];
            if (plate == null) return;
            var translucent = new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, .82f);
            plate.color = selected ? Color.white : translucent;
            AlfaUiFactory.SetSurface(plate, selected ? AlfaUiTheme.PrimaryHi : Color.white, selected ? AlfaUiTheme.Primary : new Color(0.8f, 0.84f, 0.9f, 1f),
                selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f), Color.clear);
            AlfaUiFactory.MarkSelectedFrame(plate, selected);
            if (selected) AlfaUiFactory.SetSurface(plate, shadow: AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.4f));
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
            factory.Title(content, "Title", "PARTIDA EN PAUSA", AlfaUiTheme.HeaderTitleSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
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
            resultsTitle = factory.Title(content, "Title", "RONDA INTERRUMPIDA", 52f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
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
            confirmTitle = factory.Title(content, "Title", "CONFIRMAR", AlfaUiTheme.HeaderTitleSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
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
            var labelText = factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            var labelLayout = labelText.GetComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.minWidth = 236f;
            labelLayout.preferredWidth = 236f;
            labelLayout.flexibleWidth = 0f;
            var slider = factory.Slider(row, name, min, max, callback);
            slider.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 200f;
            var valueText = factory.Text(row, "Value", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Right, true);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            var valueLayout = valueText.GetComponent<UnityEngine.UI.LayoutElement>();
            valueLayout.minWidth = 66f;
            valueLayout.preferredWidth = 66f;
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
            onlineRoomDefaultsRow.SetActive(createMode);
            // Joining needs no room options: the card shrinks instead of leaving a hole above the CTA.
            onlineFormPanel.sizeDelta = new Vector2(onlineFormPanel.sizeDelta.x, createMode ? 800f : 690f);
            AlfaUiFactory.SetSelected(onlineCreateTab, createMode);
            AlfaUiFactory.SetSelected(onlineJoinTab, !createMode);
            onlineInfoTitle.text = createMode ? "TU SALA PRIVADA" : "CÓMO UNIRTE";
            if (createMode)
            {
                SetOnlineInfoRow(0, AlfaUiIconKind.Lock, "SALA PRIVADA", "Solo entra quien tenga el código");
                SetOnlineInfoRow(1, AlfaUiIconKind.Online, "HASTA " + LetMeSleep.Core.RoomRules.Capacity + " JUGADORES", "Invitá a tus amigos con el código");
                SetOnlineInfoRow(2, AlfaUiIconKind.Map, "REGLAS EDITABLES", "Mapa, modo y tiempo se cambian en la sala");
                SetOnlineInfoRow(3, AlfaUiIconKind.Mosquito, "ROLES AL AZAR", "Humanos o mosquitos en cada ronda");
            }
            else
            {
                SetOnlineInfoRow(0, AlfaUiIconKind.Key, "PEDÍ EL CÓDIGO", "Te lo pasa quien creó la sala");
                SetOnlineInfoRow(1, AlfaUiIconKind.Copy, "PEGALO ACÁ", "Con PEGAR o con Ctrl+V");
                SetOnlineInfoRow(2, AlfaUiIconKind.Enter, "ENTRÁ A LA SALA", "Marcá LISTO y esperá la ronda");
                SetOnlineInfoRow(3, AlfaUiIconKind.Wifi, "CONEXIÓN ONLINE", "Necesitás Internet para jugar");
            }
            UpdateOnlineDefaultsView();
            PresentOnline(new OnlineUiState());
        }

        private IReadOnlyList<TrainingMapOption> OnlineMapOptions()
        {
            // The room starts on its default map; the carousel offers it first, then every catalogued room map.
            var options = new List<TrainingMapOption>();
            if (!roomMaps.Any(map => map.Id == HousePatioMapId)) options.Add(new TrainingMapOption(HousePatioMapId, "CASA CON PATIO"));
            options.AddRange(roomMaps);
            return options;
        }

        private void CycleOnlineMap(int delta)
        {
            if (OnlineBusy) return;
            var options = OnlineMapOptions();
            if (options.Count < 2) return;
            var index = Math.Max(0, options.ToList().FindIndex(map => map.Id == onlineMapId));
            onlineMapId = options[(index + delta + options.Count) % options.Count].Id;
            UpdateOnlineDefaultsView();
        }

        private void CycleOnlineMode(int delta)
        {
            if (OnlineBusy) return;
            var index = Math.Max(0, Array.IndexOf(AlfaModeText.ModeIds, onlineModeId));
            onlineModeId = AlfaModeText.ModeIds[(index + delta + AlfaModeText.ModeIds.Length) % AlfaModeText.ModeIds.Length];
            UpdateOnlineDefaultsView();
        }

        private void CycleOnlineHumans(int delta)
        {
            if (OnlineBusy) return;
            // AUTO, 1..5: the same choices as CANTIDAD DE HUMANOS in the room.
            var current = onlineHumans ?? 0;
            var next = (current + delta + 6) % 6;
            onlineHumans = next == 0 ? (int?)null : next;
            UpdateOnlineDefaultsView();
        }

        private void UpdateOnlineDefaultsView()
        {
            if (onlineMapLabel == null) return;
            var options = OnlineMapOptions();
            if (!options.Any(map => map.Id == onlineMapId)) onlineMapId = options.FirstOrDefault()?.Id ?? HousePatioMapId;
            var selected = options.FirstOrDefault(map => map.Id == onlineMapId);
            onlineMapLabel.text = (selected?.DisplayName ?? "CASA CON PATIO").ToUpperInvariant();
            var thumbnail = LoadMapThumbnail(onlineMapId);
            onlineMapThumbnail.sprite = thumbnail;
            onlineMapThumbnail.preserveAspect = false;
            onlineMapThumbnail.color = thumbnail != null ? Color.white : Color.clear;
            onlineMapPlaceholder.gameObject.SetActive(thumbnail == null);
            onlineMapPrevious.interactable = onlineMapNext.interactable = options.Count > 1;
            onlineModeLabel.text = AlfaModeText.Name(onlineModeId);
            onlineHumansLabel.text = onlineHumans.HasValue ? onlineHumans.Value.ToString() : "AUTO";
        }

        /// <summary>
        /// Map thumbnail for the create carousel: Resources/AlfaUiMapThumbs/(mapId), about 300 x 110, when a map
        /// team provides it; the carousel shows the map pictogram otherwise.
        /// </summary>
        internal static Sprite LoadMapThumbnail(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return null;
            var path = "AlfaUiMapThumbs/" + mapId;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Applies the create-tab choices to the new room through the host rule actions, one per lobby snapshot
        /// (each action publishes the next snapshot). Each rule is tried once; a rejection leaves the room default.
        /// </summary>
        private void ApplyPendingRoomDefaults()
        {
            var pending = pendingRoomDefaults;
            if (pending == null || lobbyState == null) return;
            if (!lobbyState.IsOwner || !lobbyState.IsWaiting)
            {
                pendingRoomDefaults = null;
                return;
            }
            if (LobbyBusy) return;
            if (!pending.MapDone)
            {
                pending.MapDone = true;
                if (pending.MapId != lobbyState.MapId && actions is IRoomMapActions maps && roomMaps.Any(map => map.Id == pending.MapId))
                {
                    LatchRoomDefaultsStep();
                    maps.SetRoomMap(pending.MapId);
                    return;
                }
            }
            if (!pending.ModeDone)
            {
                pending.ModeDone = true;
                if (pending.ModeId != lobbyState.ModeId && actions is IRoomModeActions modes)
                {
                    LatchRoomDefaultsStep();
                    modes.SetRoomMode(pending.ModeId);
                    return;
                }
            }
            if (!pending.HumansDone)
            {
                pending.HumansDone = true;
                if (pending.Humans != lobbyState.HumanCount)
                {
                    LatchRoomDefaultsStep();
                    actions.SetHumanCount(pending.Humans);
                    return;
                }
            }
            pendingRoomDefaults = null;
        }

        private void LatchRoomDefaultsStep()
        {
            lobbyRulesLatched = true;
            UpdateRoomMapView();
            UpdateLobbyControls();
            lobbyStatus.text = "Aplicando las opciones de la sala…";
        }

        private void SetOnlineInfoRow(int index, AlfaUiIconKind icon, string title, string subtitle)
        {
            if (onlineInfoRows == null || index < 0 || index >= onlineInfoRows.Length) return;
            var row = onlineInfoRows[index];
            row.Find("RowIcon").GetComponent<AlfaUiIcon>().Kind = icon;
            row.Find("RowTitle").GetComponent<TextMeshProUGUI>().text = title;
            row.Find("RowSubtitle").GetComponent<TextMeshProUGUI>().text = subtitle;
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
                pendingRoomDefaults = new PendingRoomDefaults { MapId = onlineMapId, ModeId = onlineModeId, Humans = onlineHumans };
                LatchOnlineSubmission("Creando la sala…");
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
            pendingRoomDefaults = null;
            LatchOnlineSubmission("Buscando la sala…");
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
                    slot.Label,
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
                    option.Label,
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
            AlfaUiFactory.SetSelected(button, selected);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
                label.enableAutoSizing = true;
                label.fontSizeMin = AlfaUiTheme.MinTextSize;
                label.fontSizeMax = 23f;
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
                buttonLabel.fontSize = 22f;
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

        /// <summary>Selected options: primary fill and the 3-unit accent frame; the check mark keeps it readable without colour.</summary>
        private static void MarkSelected(Component target, bool selected)
        {
            if (target is UnityEngine.UI.Button button) AlfaUiFactory.SetSelected(button, selected, AlfaButtonStyle.Secondary);
            else if (target != null && target.TryGetComponent(out UnityEngine.UI.Button owner)) AlfaUiFactory.SetSelected(owner, selected, AlfaButtonStyle.Secondary);
            else AlfaUiFactory.MarkSelectedFrame(target, selected);
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
            // UI-06 selection: primary blue fill and a 3-unit accent.blue frame; unselected tabs stay navy.
            AlfaUiFactory.SetSelected(button, selected, AlfaButtonStyle.Secondary);
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
