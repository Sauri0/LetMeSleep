using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Dictionary<AlfaUiScreen, GameObject> screens = new Dictionary<AlfaUiScreen, GameObject>();
        private readonly Dictionary<int, TextMeshProUGUI> humanCountLabels = new Dictionary<int, TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> memberRows = new List<TextMeshProUGUI>();
        private readonly List<GameObject> memberRowRoots = new List<GameObject>();
        private IMenuActions actions;
        private AlfaUiFactory factory;
        private AlfaUiScreen screen;
        private AlfaUiScreen settingsReturnScreen;
        private OnlineUiState onlineState = new OnlineUiState();
        private LobbyUiState lobbyState;
        private TrainingUiState trainingState = new TrainingUiState();
        private CustomizationUiState customizationState;
        private BasicCustomizationDraft customizationDraft;
        private SettingsUiState settingsState;
        private AlfaSettingsDraft settingsDraft;
        private bool initialized;
        private bool createMode;
        private bool lobbyExploring;
        private bool onlineSubmissionLatched;
        private bool onlineCancelLatched;
        private bool lobbyReadyLatched;
        private bool lobbyStartLatched;
        private bool trainingStartLatched;
        private bool trainingCancelLatched;
        private bool customizationSaveLatched;
        private bool settingsApplyLatched;
        private bool resultsActionLatched;

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

        private TextMeshProUGUI lobbyCode;
        private TextMeshProUGUI lobbyStatus;
        private TextMeshProUGUI lobbyStartReason;
        private UnityEngine.UI.Button lobbyCopyButton;
        private UnityEngine.UI.Button lobbyReadyButton;
        private TextMeshProUGUI lobbyReadyLabel;
        private UnityEngine.UI.Button lobbyStartButton;
        private TextMeshProUGUI lobbyStartLabel;
        private UnityEngine.UI.Button lobbyExploreButton;

        private UnityEngine.UI.Button trainingHumanButton;
        private UnityEngine.UI.Button trainingMosquitoButton;
        private UnityEngine.UI.Button trainingStartButton;
        private TextMeshProUGUI trainingStartLabel;
        private UnityEngine.UI.Button trainingBackButton;
        private TextMeshProUGUI trainingBackLabel;
        private TextMeshProUGUI trainingStatus;

        private CharacterPreviewOrbit previewOrbit;
        private TextMeshProUGUI customizationStatus;
        private RectTransform humanPaletteRoot;
        private RectTransform pajamaPaletteRoot;
        private RectTransform mosquitoPaletteRoot;
        private GameObject humanCustomizationFields;
        private GameObject mosquitoCustomizationFields;
        private UnityEngine.UI.Button customizationSaveButton;
        private TextMeshProUGUI customizationSaveLabel;

        private UnityEngine.UI.Slider masterVolume;
        private UnityEngine.UI.Slider musicVolume;
        private UnityEngine.UI.Slider effectsVolume;
        private UnityEngine.UI.Slider humanSensitivity;
        private UnityEngine.UI.Slider mosquitoSensitivity;
        private UnityEngine.UI.Toggle fullScreen;
        private UnityEngine.UI.Toggle vSync;
        private UnityEngine.UI.Toggle invertY;
        private TextMeshProUGUI resolutionValue;
        private TextMeshProUGUI qualityValue;
        private TMP_Dropdown frameLimitDropdown;
        private TextMeshProUGUI settingsStatus;
        private GameObject videoSettings;
        private GameObject rebindNote;
        private UnityEngine.UI.Button settingsApplyButton;
        private TextMeshProUGUI settingsApplyLabel;

        private TextMeshProUGUI hudClock;
        private TextMeshProUGUI hudBlood;
        private TextMeshProUGUI hudInteraction;
        private TextMeshProUGUI hudHint;
        private TextMeshProUGUI hudActorState;
        private TextMeshProUGUI hudNetwork;
        private UnityEngine.UI.Image hudProgress;

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

        public void Initialize(IMenuActions menuActions, AlfaUiDependencies dependencies)
        {
            if (initialized) throw new InvalidOperationException("Alfa UI is already initialized.");
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            factory = new AlfaUiFactory(dependencies);
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
        }

        public void ShowMainMenu()
        {
            lobbyExploring = false;
            SetScreen(AlfaUiScreen.MainMenu, "MainPlayButton");
        }

        public void ShowOnlineChoice() => SetScreen(AlfaUiScreen.OnlineChoice, "CreateChoiceButton");

        public void ShowCreateRoom(string rememberedName = "")
        {
            createMode = true;
            ConfigureOnlineForm(rememberedName);
            SetScreen(AlfaUiScreen.CreateRoom, "PlayerNameInput");
        }

        public void ShowJoinRoom(string rememberedName = "")
        {
            createMode = false;
            ConfigureOnlineForm(rememberedName);
            SetScreen(AlfaUiScreen.JoinRoom, "PlayerNameInput");
        }

        public void PresentOnline(OnlineUiState state)
        {
            onlineState = state ?? new OnlineUiState();
            onlineSubmissionLatched = onlineState.IsBusy;
            if (!onlineState.IsBusy) onlineCancelLatched = false;
            var busy = onlineState.IsBusy;
            playerNameInput.interactable = !busy;
            roomCodeInput.interactable = !busy;
            onlinePrimaryButton.interactable = !busy;
            onlinePasteButton.interactable = !busy;
            onlineBackButton.interactable = !busy;
            onlineCancelButton.gameObject.SetActive(onlineState.CanCancel || busy);
            onlineCancelButton.interactable = (onlineState.CanCancel || busy) && !onlineCancelLatched;
            onlineRetryButton.gameObject.SetActive(onlineState.CanRetry);
            onlineStatus.text = onlineState.VisibleMessage;
            onlineStatus.color = IsOnlineError(onlineState.Phase) ? AlfaUiTheme.Pajama500 : AlfaUiTheme.Moon200;
            if (!busy && (onlineState.Phase == OnlineOperationPhase.Cancelled || IsOnlineError(onlineState.Phase)))
                Focus(onlineState.CanRetry ? onlineRetryButton.gameObject : onlinePrimaryButton.gameObject);
        }

        public void PresentLobby(LobbyUiState state)
        {
            lobbyState = state ?? throw new ArgumentNullException(nameof(state));
            lobbyReadyLatched = state.ReadyPending;
            lobbyStartLatched = state.StartPending;
            lobbyCode.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "PREPARANDO EL CÓDIGO…" : state.RoomCode;
            lobbyCopyButton.interactable = !string.IsNullOrWhiteSpace(state.RoomCode);
            lobbyStatus.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "Preparando el código…" : "Compartí este código para invitar a tus amigos.";
            lobbyReadyButton.interactable = !lobbyReadyLatched && !lobbyStartLatched;
            lobbyReadyLabel.text = lobbyReadyLatched ? "GUARDANDO…" : state.LocalReady ? "CANCELAR LISTO" : "LISTO";
            lobbyStartButton.gameObject.SetActive(state.IsOwner);
            lobbyStartButton.interactable = state.IsOwner && state.CanStart && !lobbyReadyLatched && !lobbyStartLatched;
            lobbyStartLabel.text = lobbyStartLatched ? "INICIANDO…" : "INICIAR RONDA";
            lobbyStartReason.text = state.IsOwner && !state.CanStart ? state.StartBlockReason : string.Empty;
            lobbyExploreButton.gameObject.SetActive(state.CanExplore);
            lobbyExploreButton.interactable = !lobbyStartLatched;

            for (var i = 0; i < memberRows.Count; i++)
            {
                var visible = i < state.Members.Count;
                memberRowRoots[i].SetActive(visible);
                if (!visible) continue;
                var member = state.Members[i];
                var readiness = member.Connected ? member.Ready ? "LISTO" : "NO LISTO" : "SIN CONEXIÓN";
                memberRows[i].text = $"{member.Name}\n<size=70%>{readiness}  ·  {member.RoleLabel}</size>";
                memberRows[i].color = member.Ready ? AlfaUiTheme.Mint400 : member.Connected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Disabled;
            }

            foreach (var entry in humanCountLabels)
            {
                var selected = (entry.Key == 0 && !state.HumanCount.HasValue) || (state.HumanCount.HasValue && entry.Key == state.HumanCount.Value);
                entry.Value.text = (selected ? "✓ " : string.Empty) + (entry.Key == 0 ? "AUTO" : entry.Key.ToString());
                entry.Value.transform.parent.GetComponent<UnityEngine.UI.Button>().interactable = state.IsOwner && !lobbyStartLatched;
            }

            if (lobbyStartLatched) lobbyStatus.text = "Iniciando ronda…";
            else if (lobbyReadyLatched) lobbyStatus.text = "Guardando estado…";

            if (screen != AlfaUiScreen.Lobby)
                SetScreen(AlfaUiScreen.Lobby, "LobbyReadyButton");
        }

        public void PresentTraining(TrainingUiState state)
        {
            trainingState = state ?? new TrainingUiState();
            trainingStartLatched = trainingState.IsLoading;
            if (!trainingState.IsLoading) trainingCancelLatched = false;
            var busy = TrainingBusy;
            trainingHumanButton.interactable = !busy;
            trainingMosquitoButton.interactable = !busy;
            trainingStartButton.interactable = !busy;
            trainingBackButton.interactable = !trainingCancelLatched;
            trainingStartLabel.text = busy ? "PREPARANDO…" : "EMPEZAR ENTRENAMIENTO";
            trainingBackLabel.text = busy ? trainingCancelLatched ? "CANCELANDO…" : "CANCELAR" : "← VOLVER";
            SetButtonSelection(trainingHumanButton, trainingState.SelectedRole == AlfaRole.Human);
            SetButtonSelection(trainingMosquitoButton, trainingState.SelectedRole == AlfaRole.Mosquito);
            trainingStatus.text = trainingState.IsLoading ? "Preparando entrenamiento…" : trainingState.Message;
            if (screen == AlfaUiScreen.Results && resultsState != null && resultsState.IsTraining)
            {
                resultsActionLatched = busy;
                resultsPrimary.interactable = !busy;
                resultsLeave.interactable = !trainingCancelLatched;
                resultsPrimaryLabel.text = busy ? "PREPARANDO…" : "REPETIR ENTRENAMIENTO";
                resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = busy ?
                    trainingCancelLatched ? "CANCELANDO…" : "CANCELAR" : "VOLVER AL MENÚ";
            }
        }

        public void ShowTraining()
        {
            PresentTraining(trainingState);
            SetScreen(AlfaUiScreen.Training, trainingState.SelectedRole == AlfaRole.Human ? "TrainingHumanButton" : "TrainingMosquitoButton");
        }

        public void PresentCustomization(CustomizationUiState state)
        {
            customizationState = state ?? throw new ArgumentNullException(nameof(state));
            customizationSaveLatched = state.IsSaving;
            customizationDraft = state.Draft.Copy();
            BuildPalette(humanPaletteRoot, state.SkinColors, customizationDraft.SkinColorId, option => SetCustomizationColor("skin", option));
            BuildPalette(pajamaPaletteRoot, state.PajamaColors, customizationDraft.PajamaColorId, option => SetCustomizationColor("pajama", option));
            BuildPalette(mosquitoPaletteRoot, state.MosquitoColors, customizationDraft.MosquitoColorId, option => SetCustomizationColor("mosquito", option));
            UpdateCustomizationView();
        }

        public void ShowCustomization()
        {
            if (customizationState == null) PresentCustomization(DefaultCustomization());
            SetScreen(AlfaUiScreen.Customization, customizationDraft.Role == AlfaRole.Human ? "CustomizationHumanButton" : "CustomizationMosquitoButton");
            previewOrbit?.Show(customizationDraft.Role);
        }

        public void PresentSettings(SettingsUiState state)
        {
            settingsState = state ?? throw new ArgumentNullException(nameof(state));
            settingsApplyLatched = state.IsApplying;
            settingsDraft = state.Draft.Copy();
            masterVolume.SetValueWithoutNotify(settingsDraft.MasterVolume);
            musicVolume.SetValueWithoutNotify(settingsDraft.MusicVolume);
            effectsVolume.SetValueWithoutNotify(settingsDraft.EffectsVolume);
            humanSensitivity.SetValueWithoutNotify(settingsDraft.HumanSensitivity);
            mosquitoSensitivity.SetValueWithoutNotify(settingsDraft.MosquitoSensitivity);
            fullScreen.SetIsOnWithoutNotify(settingsDraft.FullScreen);
            vSync.SetIsOnWithoutNotify(settingsDraft.VSync);
            var frameLimitIndex = Array.IndexOf(FrameLimitOptions, settingsDraft.FrameLimit);
            frameLimitDropdown.SetValueWithoutNotify(Mathf.Max(0, frameLimitIndex));
            frameLimitDropdown.RefreshShownValue();
            invertY.SetIsOnWithoutNotify(settingsDraft.InvertY);
            videoSettings.SetActive(state.SupportsVideo);
            rebindNote.SetActive(state.SupportsRebinding);
            settingsStatus.text = state.IsApplying ? "Aplicando ajustes…" : state.Message;
            settingsApplyButton.interactable = !settingsApplyLatched && !settingsDraft.SameValues(state.Saved);
            settingsApplyLabel.text = settingsApplyLatched ? "APLICANDO…" : "APLICAR";
            UpdateSettingsCycles();
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
            hudClock.text = FormatClock(state.SecondsRemaining);
            hudBlood.text = $"SANGRE  {state.BloodCurrent:0.#} / {state.BloodTarget:0.#}";
            hudInteraction.text = state.Interaction;
            hudHint.text = string.IsNullOrWhiteSpace(state.ContextHint) ? DefaultRoleHint(state.Role) : state.ContextHint;
            hudActorState.text = ActorStateText(state.Role, state.ActorState);
            hudActorState.color = state.ActorState == HudActorState.Normal ? AlfaUiTheme.Moon200 : AlfaUiTheme.Pajama500;
            hudProgress.transform.parent.gameObject.SetActive(state.ActorState == HudActorState.Extracting || state.ActorState == HudActorState.Recovering);
            var rect = hudProgress.rectTransform;
            rect.anchorMax = new Vector2(state.StateProgress01, 1f);
            hudNetwork.text = state.NetworkMessage;
            if (screen != AlfaUiScreen.Gameplay && screen != AlfaUiScreen.Pause && screen != AlfaUiScreen.Settings)
                ShowGameplay();
        }

        public void ShowGameplay()
        {
            trainingStartLatched = false;
            trainingCancelLatched = false;
            if (trainingState.IsLoading)
                trainingState = new TrainingUiState(trainingState.SelectedRole, false, trainingState.Message);
            actions.SetGameplayInputBlocked(false);
            SetScreen(AlfaUiScreen.Gameplay, null);
        }

        public void PresentResults(ResultsUiState state)
        {
            resultsState = state ?? throw new ArgumentNullException(nameof(state));
            resultsActionLatched = false;
            trainingStartLatched = false;
            trainingCancelLatched = false;
            trainingState = new TrainingUiState(state.TrainingRole, false);
            actions.SetGameplayInputBlocked(true);
            resultsTitle.text = state.Outcome == MatchOutcome.Humans ? "GANARON LOS HUMANOS" :
                state.Outcome == MatchOutcome.Mosquitoes ? "GANARON LOS MOSQUITOS" : "RONDA INTERRUMPIDA";
            var reason = string.IsNullOrWhiteSpace(state.Reason) ? string.Empty : "\n" + state.Reason;
            resultsStats.text = $"Sangre compartida: {state.BloodCurrent:0.#} / {state.BloodTarget:0.#}\nTiempo: {FormatClock(state.ElapsedSeconds)}{reason}";
            resultsPrimary.gameObject.SetActive(state.IsTraining || state.IsOwner);
            resultsPrimary.interactable = true;
            resultsLeave.interactable = true;
            resultsPrimaryLabel.text = state.IsTraining ? "REPETIR ENTRENAMIENTO" : "VOLVER AL LOBBY";
            resultsLeave.GetComponentInChildren<TextMeshProUGUI>().text = state.IsTraining ? "VOLVER AL MENÚ" : "SALIR DE LA SALA";
            if (!state.IsTraining && !state.IsOwner) resultsStats.text += "\n\nESPERANDO AL ANFITRIÓN…";
            SetScreen(AlfaUiScreen.Results, resultsPrimary.gameObject.activeSelf ? "ResultsPrimaryButton" : "ResultsLeaveButton");
        }

        public void ShowPause()
        {
            actions.SetGameplayInputBlocked(true);
            SetScreen(AlfaUiScreen.Pause, "PauseContinueButton");
        }

        private void BuildViews(AlfaUiDependencies dependencies)
        {
            BuildMain();
            BuildOnlineChoice();
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
            var view = factory.View("MainMenuView", transform);
            screens[AlfaUiScreen.MainMenu] = view;
            var safe = factory.SafeArea(view.transform, 72f, 72f, 64f, 64f);
            var columns = factory.Horizontal(safe, "Columns", 64f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(columns);
            var brand = factory.Vertical(columns, "Brand", 16f, TextAnchor.MiddleLeft);
            brand.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1.3f;
            factory.Text(brand, "Eyebrow", "LA NOCHE RECIÉN EMPIEZA", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            factory.Text(brand, "Logo", "LET ME\nSLEEP", AlfaUiTheme.LogoSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            factory.Text(brand, "Subtitle", "HUMANOS CONTRA MOSQUITOS", 26f, AlfaUiTheme.Moon200);
            factory.Text(brand, "Version", "0.9.4 / ALFA  ·  WINDOWS", AlfaUiTheme.NoteSize, AlfaUiTheme.Disabled);
            var panel = factory.Panel(columns, "MenuCard", AlfaUiTheme.Night700, 560f);
            var menu = factory.Vertical(panel, "Actions", 14f);
            AlfaUiFactory.Fill(menu, 32f, 32f, 32f, 32f);
            factory.Text(menu, "Question", "¿QUIÉN TE DEJA DORMIR?", AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            factory.Button(menu, "MainPlayButton", "JUGAR ONLINE", ShowOnlineChoice, true, false, 68f);
            factory.Button(menu, "MainTrainingButton", "ENTRENAMIENTO", ShowTraining);
            factory.Button(menu, "MainCustomizeButton", "PERSONALIZAR", ShowCustomization);
            factory.Button(menu, "MainSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.MainMenu));
            factory.Button(menu, "MainQuitButton", "SALIR DEL JUEGO", ConfirmQuit, false, true);
            factory.Text(menu, "NavigationHint", "Tab y flechas · Enter para elegir · Escape para volver", AlfaUiTheme.NoteSize, AlfaUiTheme.Disabled);
        }

        private void BuildOnlineChoice()
        {
            var view = factory.View("OnlineChoiceView", transform);
            screens[AlfaUiScreen.OnlineChoice] = view;
            var panel = CenteredPanel(view.transform, "OnlineChoiceCard", 720f, 570f);
            var content = factory.Vertical(panel, "Content", 18f);
            AlfaUiFactory.Fill(content, 36f, 36f, 34f, 34f);
            factory.Text(content, "Title", "JUGAR ONLINE", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.Button(content, "CreateChoiceButton", "CREAR SALA", () => ShowCreateRoom(), true, false, 72f);
            factory.Text(content, "CreateHelp", "Abrí una sala y compartí el código con tus amigos.", AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            factory.Button(content, "JoinChoiceButton", "UNIRME CON CÓDIGO", () => ShowJoinRoom(), false, false, 72f);
            factory.Text(content, "JoinHelp", "Pegá el código que te mandó el anfitrión.", AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            factory.Button(content, "OnlineChoiceBackButton", "← VOLVER", ShowMainMenu);
        }

        private void BuildOnlineForm()
        {
            var view = factory.View("OnlineFormView", transform);
            screens[AlfaUiScreen.CreateRoom] = view;
            screens[AlfaUiScreen.JoinRoom] = view;
            var panel = CenteredPanel(view.transform, "OnlineFormCard", 780f, 680f);
            var content = factory.Vertical(panel, "Content", 14f);
            AlfaUiFactory.Fill(content, 38f, 38f, 34f, 34f);
            onlineFormTitle = factory.Text(content, "Title", "CREAR SALA", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.Text(content, "PlayerNameLabel", "TU NOMBRE", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            playerNameInput = factory.Input(content, "PlayerNameInput", "Cómo te dicen tus amigos", 24);
            playerNameInput.onSubmit.AddListener(_ => SubmitOnline());
            var codeContainer = factory.Vertical(content, "RoomCodeRow", 8f);
            roomCodeRow = codeContainer.gameObject;
            factory.Text(codeContainer, "RoomCodeLabel", "CÓDIGO DE SALA", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            var codeActions = factory.Horizontal(codeContainer, "CodeActions", 10f);
            codeActions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 58f;
            roomCodeInput = factory.Input(codeActions, "RoomCodeInput", "XXXXX-XXXXX", 32, true);
            roomCodeInput.onValueChanged.AddListener(value =>
            {
                var caret = roomCodeInput.caretPosition;
                roomCodeInput.SetTextWithoutNotify(AlfaRoomCode.FormatForDisplay(value));
                roomCodeInput.caretPosition = Mathf.Min(caret, roomCodeInput.text.Length);
            });
            roomCodeInput.onSubmit.AddListener(_ => SubmitOnline());
            onlinePasteButton = factory.Button(codeActions, "PasteRoomCodeButton", "PEGAR", PasteRoomCode, false, false, 58f);
            onlinePasteButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 150f;
            onlineStatus = factory.Text(content, "OnlineStatus", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            onlinePrimaryButton = factory.Button(content, "OnlinePrimaryButton", "CREAR SALA", SubmitOnline, true, false, 68f);
            onlinePrimaryLabel = onlinePrimaryButton.GetComponentInChildren<TextMeshProUGUI>();
            var actionsRow = factory.Horizontal(content, "Actions", 12f, TextAnchor.MiddleCenter);
            onlineCancelButton = factory.Button(actionsRow, "OnlineCancelButton", "CANCELAR", RequestOnlineCancel);
            onlineRetryButton = factory.Button(actionsRow, "OnlineRetryButton", "INTENTAR OTRA VEZ", SubmitOnline);
            onlineBackButton = factory.Button(actionsRow, "OnlineBackButton", "← VOLVER", ShowOnlineChoice);
            onlineCancelButton.gameObject.SetActive(false);
            onlineRetryButton.gameObject.SetActive(false);
        }

        private void BuildLobby()
        {
            var view = factory.View("LobbyOverlayView", transform, false);
            screens[AlfaUiScreen.Lobby] = view;
            var safe = factory.SafeArea(view.transform, 36f, 36f, 28f, 32f);
            var header = factory.Panel(safe, "Header", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f));
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 92f));
            var headerRow = factory.Horizontal(header, "HeaderRow", 18f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(headerRow, 24f, 24f, 12f, 12f);
            factory.Text(headerRow, "RoomLabel", "SALA ONLINE", AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            lobbyCode = factory.Text(headerRow, "RoomCode", "PREPARANDO EL CÓDIGO…", 28f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
            lobbyCopyButton = factory.Button(headerRow, "LobbyCopyButton", "COPIAR CÓDIGO", () =>
            {
                if (lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.RoomCode))
                {
                    actions.CopyRoomCode(lobbyState.RoomCode);
                    lobbyStatus.text = "Código copiado.";
                }
            }, true);
            lobbyCopyButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 230f;

            var rosterPanel = factory.Panel(safe, "RosterPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.94f));
            Anchor(rosterPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(430f, -116f));
            var roster = factory.Vertical(rosterPanel, "Roster", 12f);
            AlfaUiFactory.Fill(roster, 24f, 24f, 22f, 22f);
            factory.Text(roster, "Title", "JUGADORES", AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            factory.ScrollView(roster, "MemberScrollView", out var memberContent, 520f);
            for (var i = 0; i < LetMeSleep.Core.RoomRules.Capacity; i++)
            {
                var rowPanel = factory.Panel(memberContent, $"MemberPanel{i + 1}", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.46f), -1f, 64f);
                var row = factory.Text(rowPanel, $"Member{i + 1}", string.Empty, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
                AlfaUiFactory.Fill(row.rectTransform, 14f, 14f, 8f, 8f);
                row.margin = new Vector4(14f, 10f, 14f, 10f);
                rowPanel.gameObject.SetActive(false);
                memberRows.Add(row);
                memberRowRoots.Add(rowPanel.gameObject);
            }

            var rulesPanel = factory.Panel(safe, "RulesPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f));
            Anchor(rulesPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(520f, 700f));
            var rules = factory.Vertical(rulesPanel, "Rules", 12f);
            AlfaUiFactory.Fill(rules, 26f, 26f, 24f, 24f);
            factory.Text(rules, "Title", "PRÓXIMA RONDA", AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            AddReadOnlyField(rules, "MODO", "SANGRE");
            AddReadOnlyField(rules, "MAPA", "CASA CON PATIO");
            factory.Text(rules, "HumanCountLabel", "CANTIDAD DE HUMANOS", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            var counts = factory.Horizontal(rules, "HumanCount", 6f, TextAnchor.MiddleCenter);
            for (var count = 0; count <= 5; count++)
            {
                var captured = count;
                var button = factory.Button(counts, "HumanCount" + count, count == 0 ? "AUTO" : count.ToString(), () => actions.SetHumanCount(captured == 0 ? (int?)null : captured), false, false, 48f);
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = count == 0 ? 104f : 52f;
                humanCountLabels[count] = button.GetComponentInChildren<TextMeshProUGUI>();
            }
            factory.Text(rules, "RoleNote", "Los roles se sortean al empezar cada ronda.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            lobbyReadyButton = factory.Button(rules, "LobbyReadyButton", "LISTO", () =>
            {
                if (lobbyState == null || lobbyReadyLatched || lobbyState.ReadyPending) return;
                lobbyReadyLatched = true;
                lobbyReadyButton.interactable = false;
                lobbyStartButton.interactable = false;
                lobbyReadyLabel.text = "GUARDANDO…";
                lobbyStatus.text = "Guardando estado…";
                Focus(lobbyCopyButton.gameObject);
                actions.SetReady(!lobbyState.LocalReady);
            }, true, false, 66f);
            lobbyReadyLabel = lobbyReadyButton.GetComponentInChildren<TextMeshProUGUI>();
            lobbyStartButton = factory.Button(rules, "LobbyStartButton", "INICIAR RONDA", BeginRound, false, false, 66f);
            lobbyStartLabel = lobbyStartButton.GetComponentInChildren<TextMeshProUGUI>();
            lobbyStartReason = factory.Text(rules, "StartReason", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center);
            lobbyExploreButton = factory.Button(rules, "LobbyExploreButton", "RECORRER SALA", BeginLobbyExploration);
            lobbyStatus = factory.Text(rules, "LobbyStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
        }

        private void BuildTraining()
        {
            var view = factory.View("TrainingView", transform);
            screens[AlfaUiScreen.Training] = view;
            var panel = CenteredPanel(view.transform, "TrainingCard", 900f, 760f);
            var content = factory.Vertical(panel, "Content", 16f);
            AlfaUiFactory.Fill(content, 40f, 40f, 34f, 34f);
            factory.Text(content, "Title", "ENTRENAMIENTO", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.Text(content, "Intro", "Practicá con bots antes de entrar a una sala.", AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            factory.Text(content, "RoleLabel", "1. ELEGÍ TU ROL", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            var roles = factory.Horizontal(content, "Roles", 16f, TextAnchor.MiddleCenter);
            trainingHumanButton = factory.Button(roles, "TrainingHumanButton", "HUMANO", () => SelectTrainingRole(AlfaRole.Human), true, false, 74f);
            trainingMosquitoButton = factory.Button(roles, "TrainingMosquitoButton", "MOSQUITO", () => SelectTrainingRole(AlfaRole.Mosquito), false, false, 74f);
            AddReadOnlyField(content, "MODO", "SANGRE");
            AddReadOnlyField(content, "MAPA", "CASA CON PATIO");
            trainingStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            trainingStartButton = factory.Button(content, "TrainingStartButton", "EMPEZAR ENTRENAMIENTO", BeginTraining, true, false, 72f);
            trainingStartLabel = trainingStartButton.GetComponentInChildren<TextMeshProUGUI>();
            trainingBackButton = factory.Button(content, "TrainingBackButton", "← VOLVER", BackFromTraining);
            trainingBackLabel = trainingBackButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildCustomization(AlfaUiDependencies dependencies)
        {
            var view = factory.View("CustomizationView", transform);
            screens[AlfaUiScreen.Customization] = view;
            var safe = factory.SafeArea(view.transform, 48f, 48f, 40f, 40f);
            var columns = factory.Horizontal(safe, "Columns", 36f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(columns);
            var previewPanel = factory.Panel(columns, "PreviewPanel", AlfaUiTheme.Night700, 900f, 900f);
            previewPanel.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            var rawNode = AlfaUiFactory.Node("CharacterPreview", previewPanel, typeof(UnityEngine.UI.RawImage), typeof(CharacterPreviewOrbit));
            AlfaUiFactory.Fill(rawNode.GetComponent<RectTransform>(), 18f, 18f, 92f, 96f);
            var raw = rawNode.GetComponent<UnityEngine.UI.RawImage>();
            raw.color = Color.white;
            previewOrbit = rawNode.GetComponent<CharacterPreviewOrbit>();
            previewOrbit.Initialize(raw, dependencies.Preview);
            var unavailable = factory.Text(previewPanel, "PreviewUnavailable", "El visor 3D se conecta al personaje del juego.", AlfaUiTheme.BodySize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            Anchor(unavailable.rectTransform, new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            unavailable.gameObject.SetActive(!previewOrbit.IsBound);
            var angles = factory.Horizontal(previewPanel, "PreviewAngles", 8f, TextAnchor.MiddleCenter);
            Anchor(angles, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-36f, 64f));
            factory.Button(angles, "PreviewFrontButton", "FRENTE", () => previewOrbit.SetAngle(PreviewAngle.Front), false, false, 48f);
            factory.Button(angles, "PreviewSideButton", "PERFIL", () => previewOrbit.SetAngle(PreviewAngle.Side), false, false, 48f);
            factory.Button(angles, "PreviewBackButton", "ESPALDA", () => previewOrbit.SetAngle(PreviewAngle.Back), false, false, 48f);
            factory.Button(angles, "PreviewResetButton", "RESTABLECER VISTA", () => previewOrbit.ResetView(), false, false, 48f);

            var optionsPanel = factory.Panel(columns, "OptionsPanel", AlfaUiTheme.Night700, 640f, 900f);
            var content = factory.Vertical(optionsPanel, "Content", 12f);
            AlfaUiFactory.Fill(content, 28f, 28f, 28f, 28f);
            factory.Text(content, "Title", "PERSONALIZAR", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            var roleRow = factory.Horizontal(content, "RoleTabs", 10f, TextAnchor.MiddleCenter);
            factory.Button(roleRow, "CustomizationHumanButton", "HUMANO", () => SetCustomizationRole(AlfaRole.Human), true);
            factory.Button(roleRow, "CustomizationMosquitoButton", "MOSQUITO", () => SetCustomizationRole(AlfaRole.Mosquito));
            humanCustomizationFields = factory.Vertical(content, "HumanFields", 10f).gameObject;
            factory.Text(humanCustomizationFields.transform, "SkinLabel", "TONO DE PIEL", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            humanPaletteRoot = factory.Horizontal(humanCustomizationFields.transform, "SkinPalette", 8f);
            factory.Text(humanCustomizationFields.transform, "PajamaLabel", "COLOR DE PIJAMA", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            pajamaPaletteRoot = factory.Horizontal(humanCustomizationFields.transform, "PajamaPalette", 8f);
            factory.Text(humanCustomizationFields.transform, "DefaultClothes", "Predeterminado: pijama, pantuflas y gorro de noche.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            mosquitoCustomizationFields = factory.Vertical(content, "MosquitoFields", 10f).gameObject;
            factory.Text(mosquitoCustomizationFields.transform, "MosquitoColorLabel", "COLOR", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            mosquitoPaletteRoot = factory.Horizontal(mosquitoCustomizationFields.transform, "MosquitoPalette", 8f);
            customizationStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            customizationSaveButton = factory.Button(content, "CustomizationSaveButton", "GUARDAR", SaveCustomization, true, false, 66f);
            customizationSaveLabel = customizationSaveButton.GetComponentInChildren<TextMeshProUGUI>();
            factory.Button(content, "CustomizationResetButton", "DESHACER CAMBIOS", ResetCustomization);
            factory.Button(content, "CustomizationBackButton", "← VOLVER", CloseCustomization);
        }

        private void BuildSettings()
        {
            var view = factory.View("SettingsView", transform);
            screens[AlfaUiScreen.Settings] = view;
            var panel = CenteredPanel(view.transform, "SettingsCard", 980f, 940f);
            var content = factory.Vertical(panel, "Content", 10f);
            AlfaUiFactory.Fill(content, 34f, 34f, 28f, 28f);
            factory.Text(content, "Title", "AJUSTES", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.ScrollView(content, "SettingsScrollView", out var fields, 690f);
            factory.Text(fields, "AudioTitle", "AUDIO", AlfaUiTheme.H2Size, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            masterVolume = AddSliderField(fields, "VOLUMEN GENERAL", "MasterVolumeSlider", value => ChangeSetting(draft => draft.MasterVolume = value));
            musicVolume = AddSliderField(fields, "MÚSICA", "MusicVolumeSlider", value => ChangeSetting(draft => draft.MusicVolume = value));
            effectsVolume = AddSliderField(fields, "EFECTOS", "EffectsVolumeSlider", value => ChangeSetting(draft => draft.EffectsVolume = value));
            videoSettings = factory.Vertical(fields, "VideoFields", 10f).gameObject;
            factory.Text(videoSettings.transform, "VideoTitle", "VIDEO", AlfaUiTheme.H2Size, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            fullScreen = factory.Toggle(videoSettings.transform, "FullScreenToggle", "PANTALLA COMPLETA", value => ChangeSetting(draft => draft.FullScreen = value));
            resolutionValue = AddCycleField(videoSettings.transform, "RESOLUCIÓN", "Resolution", -1, 1, delta =>
                ChangeSetting(draft => draft.ResolutionIndex = Cycle(draft.ResolutionIndex, delta, settingsState?.Resolutions.Count ?? 0)));
            qualityValue = AddCycleField(videoSettings.transform, "CALIDAD", "Quality", -1, 1, delta =>
                ChangeSetting(draft => draft.QualityIndex = Cycle(draft.QualityIndex, delta, settingsState?.Qualities.Count ?? 0)));
            vSync = factory.Toggle(videoSettings.transform, "VSyncToggle", "SINCRONIZACIÓN VERTICAL", value => ChangeSetting(draft => draft.VSync = value));
            factory.Text(videoSettings.transform, "FrameLimitLabel", "LÍMITE DE FPS", AlfaUiTheme.LabelSize, AlfaUiTheme.Moon200);
            frameLimitDropdown = factory.Dropdown(videoSettings.transform, "FrameLimitDropdown",
                FrameLimitOptions.Select(FrameLimitLabel).ToArray(), index =>
                    ChangeSetting(draft => draft.FrameLimit = FrameLimitOptions[Mathf.Clamp(index, 0, FrameLimitOptions.Length - 1)]));
            factory.Text(fields, "ControlsTitle", "CONTROLES", AlfaUiTheme.H2Size, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            humanSensitivity = AddSliderField(fields, "SENSIBILIDAD HUMANO", "HumanSensitivitySlider", value => ChangeSetting(draft => draft.HumanSensitivity = value), 0.1f, 2f);
            mosquitoSensitivity = AddSliderField(fields, "SENSIBILIDAD MOSQUITO", "MosquitoSensitivitySlider", value => ChangeSetting(draft => draft.MosquitoSensitivity = value), 0.1f, 2f);
            invertY = factory.Toggle(fields, "InvertYToggle", "INVERTIR EJE VERTICAL", value => ChangeSetting(draft => draft.InvertY = value));
            rebindNote = factory.Text(fields, "RebindNote", "La reasignación usa el contrato de entrada del juego.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200).gameObject;
            settingsStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            var buttons = factory.Horizontal(content, "Actions", 12f, TextAnchor.MiddleCenter);
            settingsApplyButton = factory.Button(buttons, "SettingsApplyButton", "APLICAR", ApplySettings, true);
            settingsApplyLabel = settingsApplyButton.GetComponentInChildren<TextMeshProUGUI>();
            factory.Button(buttons, "SettingsResetButton", "DESHACER CAMBIOS", ResetSettings);
            factory.Button(buttons, "SettingsBackButton", "← VOLVER", CloseSettings);
        }

        private void BuildHud()
        {
            var view = factory.View("GameplayHudView", transform, false);
            screens[AlfaUiScreen.Gameplay] = view;
            var top = factory.Panel(view.transform, "RoundState", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.9f));
            Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(460f, 88f));
            var topRow = factory.Horizontal(top, "Content", 22f, TextAnchor.MiddleCenter);
            AlfaUiFactory.Fill(topRow, 20f, 20f, 12f, 12f);
            hudClock = factory.Text(topRow, "Clock", "03:00", 26f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            hudBlood = factory.Text(topRow, "Blood", "SANGRE  0 / 20", 24f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center);
            hudNetwork = factory.Text(view.transform, "NetworkState", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Pajama500, TextAlignmentOptions.Right);
            Anchor(hudNetwork.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(420f, 56f));
            hudInteraction = factory.Text(view.transform, "Interaction", string.Empty, 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            Anchor(hudInteraction.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 54f));
            hudActorState = factory.Text(view.transform, "ActorState", string.Empty, 30f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center, true);
            Anchor(hudActorState.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 66f));
            var progressRoot = factory.Panel(view.transform, "StateProgress", AlfaUiTheme.Night600);
            Anchor(progressRoot, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 20f));
            var fill = AlfaUiFactory.Node("Fill", progressRoot, typeof(UnityEngine.UI.Image));
            hudProgress = fill.GetComponent<UnityEngine.UI.Image>();
            hudProgress.color = AlfaUiTheme.Lamp400;
            AlfaUiFactory.Fill(hudProgress.rectTransform);
            hudHint = factory.Text(view.transform, "ContextHint", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            Anchor(hudHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(620f, 70f));
            var reticle = factory.Text(view.transform, "Reticle", "+", 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        }

        private void BuildPause()
        {
            var view = factory.View("PauseView", transform, false);
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Pause] = view;
            var panel = CenteredPanel(view.transform, "PauseCard", 560f, 520f);
            var content = factory.Vertical(panel, "Content", 14f);
            AlfaUiFactory.Fill(content, 34f, 34f, 30f, 30f);
            factory.Text(content, "Title", "PAUSA", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.Button(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, true, false, 68f);
            factory.Button(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause));
            factory.Button(content, "PauseControlsButton", "CONTROLES", () => OpenSettings(AlfaUiScreen.Pause));
            factory.Button(content, "PauseLeaveButton", "SALIR DE LA SALA", ConfirmLeave, false, true);
        }

        private void BuildResults()
        {
            var view = factory.View("ResultsView", transform, false);
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Results] = view;
            var panel = CenteredPanel(view.transform, "ResultsCard", 780f, 580f);
            var content = factory.Vertical(panel, "Content", 18f);
            AlfaUiFactory.Fill(content, 40f, 40f, 34f, 34f);
            resultsTitle = factory.Text(content, "Title", "RONDA INTERRUMPIDA", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            resultsStats = factory.Text(content, "Stats", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            resultsPrimary = factory.Button(content, "ResultsPrimaryButton", "VOLVER AL LOBBY", ResultsPrimaryAction, true, false, 68f);
            resultsPrimaryLabel = resultsPrimary.GetComponentInChildren<TextMeshProUGUI>();
            resultsLeave = factory.Button(content, "ResultsLeaveButton", "SALIR DE LA SALA", ResultsLeaveAction, false, true);
        }

        private void BuildConfirm()
        {
            confirmModal = factory.View("ConfirmModal", transform, false);
            confirmModal.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Scrim;
            confirmModal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var panel = CenteredPanel(confirmModal.transform, "ConfirmCard", 640f, 360f);
            var content = factory.Vertical(panel, "Content", 18f);
            AlfaUiFactory.Fill(content, 34f, 34f, 30f, 30f);
            confirmTitle = factory.Text(content, "Title", "CONFIRMAR", AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
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
            factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
            var slider = factory.Slider(row, name, min, max, callback);
            slider.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 420f;
            return slider;
        }

        private TextMeshProUGUI AddCycleField(Transform parent, string label, string name, int previous, int next, Action<int> changed)
        {
            var row = factory.Horizontal(parent, name + "Row", 10f, TextAnchor.MiddleCenter);
            factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
            var previousButton = factory.Button(row, name + "Previous", "‹", () => changed(previous), false, false, 46f);
            previousButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 54f;
            var value = factory.Text(row, name + "Value", "—", AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            value.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 260f;
            var nextButton = factory.Button(row, name + "Next", "›", () => changed(next), false, false, 46f);
            nextButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 54f;
            return value;
        }

        private void ConfigureOnlineForm(string rememberedName)
        {
            playerNameInput.SetTextWithoutNotify(string.IsNullOrWhiteSpace(rememberedName) ? playerNameInput.text : rememberedName.Trim());
            onlineFormTitle.text = createMode ? "CREAR SALA" : "UNIRME CON CÓDIGO";
            onlinePrimaryLabel.text = createMode ? "CREAR SALA" : "UNIRME";
            roomCodeRow.SetActive(!createMode);
            PresentOnline(new OnlineUiState());
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
            LatchOnlineSubmission("Buscando sala…");
            actions.JoinRoom(playerName, normalized);
        }

        private void PasteRoomCode()
        {
            if (OnlineBusy) return;
            roomCodeInput.SetTextWithoutNotify(AlfaRoomCode.FormatForDisplay(GUIUtility.systemCopyBuffer));
            Focus(roomCodeInput.gameObject);
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
            onlineStatus.text = message;
            onlineStatus.color = AlfaUiTheme.Moon200;
            Focus(onlineCancelButton.gameObject);
        }

        private void RequestOnlineCancel()
        {
            if (!OnlineBusy || onlineCancelLatched) return;
            onlineCancelLatched = true;
            onlineCancelButton.interactable = false;
            onlineStatus.text = "Cancelando…";
            onlineStatus.color = AlfaUiTheme.Moon200;
            actions.CancelOnline();
        }

        private void SetOnlineLocalError(string message, GameObject focus)
        {
            onlineStatus.text = message;
            onlineStatus.color = AlfaUiTheme.Pajama500;
            Focus(focus);
        }

        private void SelectTrainingRole(AlfaRole role)
        {
            if (TrainingBusy) return;
            PresentTraining(new TrainingUiState(role, false, role == AlfaRole.Human ?
                "Defendé tu descanso con mirada y alcance manual." : "Volá hacia la mira y picá por contacto válido."));
        }

        private void BeginTraining()
        {
            StartTrainingIntent(trainingState.SelectedRole, false);
        }

        private void BackFromTraining()
        {
            if (TrainingBusy) RequestTrainingCancel();
            else ShowMainMenu();
        }

        private void StartTrainingIntent(AlfaRole role, bool fromResults)
        {
            if (TrainingBusy || (fromResults && resultsActionLatched)) return;
            trainingStartLatched = true;
            trainingCancelLatched = false;
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
                trainingStartButton.interactable = false;
                trainingStartLabel.text = "PREPARANDO…";
                trainingBackLabel.text = "CANCELAR";
            }
            trainingStatus.text = "Preparando entrenamiento…";
            actions.StartTraining(role, BloodModeId, HousePatioMapId);
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
            if (customizationDraft == null || customizationSaveLatched) return;
            customizationDraft.Role = role;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void SetCustomizationColor(string category, NamedColorOption option)
        {
            if (customizationDraft == null || option == null || customizationSaveLatched) return;
            if (category == "skin") customizationDraft.SkinColorId = option.Id;
            else if (category == "pajama") customizationDraft.PajamaColorId = option.Id;
            else customizationDraft.MosquitoColorId = option.Id;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void BuildPalette(RectTransform parent, IReadOnlyList<NamedColorOption> options, string selectedId, Action<NamedColorOption> selected)
        {
            AlfaUiFactory.Clear(parent);
            foreach (var option in options)
            {
                var captured = option;
                var label = (option.Id == selectedId ? "✓ " : string.Empty) + option.Label;
                var button = factory.Button(parent, "Color_" + option.Id, label, () => selected(captured), false, false, 52f);
                var colors = button.colors;
                colors.normalColor = option.Color;
                colors.highlightedColor = Color.Lerp(option.Color, Color.white, 0.18f);
                colors.selectedColor = AlfaUiTheme.Lamp400;
                button.colors = colors;
                button.GetComponentInChildren<TextMeshProUGUI>().color = RelativeLuminance(option.Color) > 0.5f ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100;
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 112f;
            }
        }

        private void UpdateCustomizationView()
        {
            if (customizationDraft == null || customizationState == null) return;
            var human = customizationDraft.Role == AlfaRole.Human;
            humanCustomizationFields.SetActive(human);
            mosquitoCustomizationFields.SetActive(!human);
            previewOrbit?.Show(customizationDraft.Role);
            customizationStatus.text = customizationSaveLatched ? "Guardando…" : customizationState.Message;
            customizationSaveButton.interactable = !customizationSaveLatched && !customizationDraft.SameValues(customizationState.Saved);
            customizationSaveLabel.text = customizationSaveLatched ? "GUARDANDO…" : "GUARDAR";
            MarkPalette(humanPaletteRoot, customizationDraft.SkinColorId);
            MarkPalette(pajamaPaletteRoot, customizationDraft.PajamaColorId);
            MarkPalette(mosquitoPaletteRoot, customizationDraft.MosquitoColorId);
        }

        private static void MarkPalette(Transform parent, string selectedId)
        {
            foreach (Transform child in parent)
            {
                var label = child.GetComponentInChildren<TextMeshProUGUI>();
                if (label == null) continue;
                var plain = label.text.StartsWith("✓ ", StringComparison.Ordinal) ? label.text.Substring(2) : label.text;
                label.text = child.name == "Color_" + selectedId ? "✓ " + plain : plain;
            }
        }

        private void SaveCustomization()
        {
            if (customizationDraft == null || customizationState == null || customizationSaveLatched || customizationState.IsSaving) return;
            customizationSaveLatched = true;
            customizationSaveButton.interactable = false;
            customizationSaveLabel.text = "GUARDANDO…";
            customizationStatus.text = "Guardando…";
            actions.SaveCustomization(customizationDraft.Copy());
        }

        private void ResetCustomization()
        {
            if (customizationState == null || customizationSaveLatched) return;
            customizationDraft = customizationState.Saved.Copy();
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void CloseCustomization()
        {
            if (customizationSaveLatched) return;
            if (CustomizationDirty())
            {
                ShowConfirm("¿SALIR SIN GUARDAR?", "Los cambios de personalización se perderán.", "SEGUIR EDITANDO", "SALIR", ShowMainMenu);
                return;
            }
            ShowMainMenu();
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
                settingsState.Qualities, settingsState.SupportsVideo, settingsState.SupportsRebinding));
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
            if (lobbyState == null || lobbyStartLatched || lobbyState.StartPending ||
                !lobbyState.IsOwner || !lobbyState.CanStart) return;
            lobbyStartLatched = true;
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
            if (resultsState != null && resultsState.IsTraining) ShowMainMenu();
            else ConfirmLeave();
        }

        private void ResumeFromPause()
        {
            actions.ResumeGame();
            ShowGameplay();
        }

        private void BeginLobbyExploration()
        {
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
                CloseConfirm();
                return;
            }
            switch (screen)
            {
                case AlfaUiScreen.MainMenu: ConfirmQuit(); break;
                case AlfaUiScreen.OnlineChoice: ShowMainMenu(); break;
                case AlfaUiScreen.CreateRoom:
                case AlfaUiScreen.JoinRoom:
                    if (OnlineBusy) RequestOnlineCancel(); else ShowOnlineChoice();
                    break;
                case AlfaUiScreen.Lobby:
                    if (lobbyExploring) EndLobbyExploration(); else ShowLobbyPause();
                    break;
                case AlfaUiScreen.Training:
                    if (TrainingBusy) RequestTrainingCancel(); else ShowMainMenu();
                    break;
                case AlfaUiScreen.Customization: CloseCustomization(); break;
                case AlfaUiScreen.Settings: CloseSettings(); break;
                case AlfaUiScreen.Gameplay: ShowPause(); break;
                case AlfaUiScreen.Pause: ResumeFromPause(); break;
                case AlfaUiScreen.Results:
                    if (resultsState != null && resultsState.IsTraining && TrainingBusy) RequestTrainingCancel();
                    break;
            }
        }

        private void SetScreen(AlfaUiScreen next, string focusName)
        {
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
            }
            else Focus(focusName);
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

        private static void Focus(GameObject target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
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
                case AlfaUiScreen.OnlineChoice: return "CreateChoiceButton";
                case AlfaUiScreen.CreateRoom:
                case AlfaUiScreen.JoinRoom: return "PlayerNameInput";
                case AlfaUiScreen.Lobby: return lobbyReadyButton != null && lobbyReadyButton.interactable ? "LobbyReadyButton" : "LobbyCopyButton";
                case AlfaUiScreen.Training: return trainingState.SelectedRole == AlfaRole.Human ? "TrainingHumanButton" : "TrainingMosquitoButton";
                case AlfaUiScreen.Customization: return customizationDraft != null && customizationDraft.Role == AlfaRole.Mosquito ? "CustomizationMosquitoButton" : "CustomizationHumanButton";
                case AlfaUiScreen.Settings: return "MasterVolumeSlider";
                case AlfaUiScreen.Pause: return "PauseContinueButton";
                case AlfaUiScreen.Results: return resultsPrimary != null && resultsPrimary.gameObject.activeSelf ? "ResultsPrimaryButton" : "ResultsLeaveButton";
                default: return null;
            }
        }

        private bool CustomizationDirty() => customizationState != null && customizationDraft != null && !customizationDraft.SameValues(customizationState.Saved);
        private bool SettingsDirty() => settingsState != null && settingsDraft != null && !settingsDraft.SameValues(settingsState.Saved);
        private bool OnlineBusy => onlineSubmissionLatched || onlineState.IsBusy;
        private bool TrainingBusy => trainingStartLatched || trainingState.IsLoading;
        private static bool IsOnlineError(OnlineOperationPhase phase) => phase == OnlineOperationPhase.RecoverableError ||
            phase == OnlineOperationPhase.IncompatibleVersion || phase == OnlineOperationPhase.RoomClosed;

        private static void SetButtonSelection(UnityEngine.UI.Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Night600;
            button.colors = colors;
        }

        private static string ActorStateText(AlfaRole role, HudActorState state)
        {
            switch (state)
            {
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
