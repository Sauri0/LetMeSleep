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
        private readonly List<AlfaUiIcon> memberStatusIcons = new List<AlfaUiIcon>();
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

        private TextMeshProUGUI lobbyCode;
        private TextMeshProUGUI lobbyStatus;
        private TextMeshProUGUI lobbyStartReason;
        private TextMeshProUGUI lobbyRoleBadge;
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
        private TextMeshProUGUI customizationPreviewTitle;
        private AlfaUiIcon customizationPreviewIcon;
        private TextMeshProUGUI customizationCategoryTitle;
        private TextMeshProUGUI customizationStatus;
        private RectTransform humanPaletteRoot;
        private RectTransform pajamaPaletteRoot;
        private RectTransform mosquitoPaletteRoot;
        private GameObject humanCustomizationFields;
        private GameObject mosquitoCustomizationFields;
        private UnityEngine.UI.Button customizationHumanButton;
        private UnityEngine.UI.Button customizationMosquitoButton;
        private CanvasGroup customizationControlsGroup;
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
        private CanvasGroup settingsControlsGroup;

        private TextMeshProUGUI hudClock;
        private TextMeshProUGUI hudBlood;
        private TextMeshProUGUI hudInteraction;
        private TextMeshProUGUI hudHint;
        private TextMeshProUGUI hudActorState;
        private TextMeshProUGUI hudNetwork;
        private TextMeshProUGUI hudRoleLabel;
        private AlfaUiIcon hudRoleIcon;
        private UnityEngine.UI.Image hudRoleBackground;
        private UnityEngine.UI.Outline hudRoleOutline;
        private UnityEngine.UI.Image hudBloodFill;
        private GameObject hudPromptPanel;
        private GameObject hudHintPanel;
        private GameObject hudStatePanel;
        private UnityEngine.UI.Image hudProgress;

        private UnityEngine.UI.Button pauseLeaveButton;
        private TextMeshProUGUI pauseLeaveLabel;

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
            UpdateSelectionFeedback();
        }

        public void ShowMainMenu()
        {
            lobbyExploring = false;
            gameplayIsTraining = false;
            SetScreen(AlfaUiScreen.MainMenu, "MainPlayButton");
        }

        public void ShowOnlineChoice() => SetScreen(AlfaUiScreen.OnlineChoice, "CreateChoiceButton");

        public void SetRememberedPlayerName(string playerName)
        {
            rememberedPlayerName = NormalizePlayerName(playerName);
        }

        public void ShowCreateRoom(string rememberedName = null)
        {
            createMode = true;
            ConfigureOnlineForm(rememberedName == null ? rememberedPlayerName : NormalizePlayerName(rememberedName));
            SetScreen(AlfaUiScreen.CreateRoom, "PlayerNameInput");
        }

        public void ShowJoinRoom(string rememberedName = null)
        {
            createMode = false;
            ConfigureOnlineForm(rememberedName == null ? rememberedPlayerName : NormalizePlayerName(rememberedName));
            SetScreen(AlfaUiScreen.JoinRoom, "PlayerNameInput");
        }

        public void PresentOnline(OnlineUiState state)
        {
            onlineState = state ?? new OnlineUiState();
            var errorKey = IsOnlineError(onlineState.Phase) ? $"{onlineState.Phase}:{onlineState.VisibleMessage}" : string.Empty;
            if (errorKey.Length > 0 && errorKey != onlineErrorFeedbackKey) RequestFeedback(UiFeedbackKind.Error);
            onlineErrorFeedbackKey = errorKey;
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
            gameplayIsTraining = false;
            lobbyReadyLatched = state.ReadyPending;
            lobbyStartLatched = state.StartPending;
            lobbyCode.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "PREPARANDO EL CÓDIGO…" : state.RoomCode;
            lobbyCopyButton.interactable = !string.IsNullOrWhiteSpace(state.RoomCode);
            lobbyStatus.text = string.IsNullOrWhiteSpace(state.RoomCode) ? "Preparando el código…" : "Compartí este código para invitar a tus amigos.";
            lobbyRoleBadge.text = state.IsOwner ? "SOS ADMINISTRADOR DE LA SALA" : "SOS INVITADO";
            lobbyRoleBadge.color = state.IsOwner ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Moon200;
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
                memberStatusIcons[i].Kind = member.Ready ? AlfaUiIconKind.Ready : member.Connected ? AlfaUiIconKind.Human : AlfaUiIconKind.Exit;
                memberStatusIcons[i].color = member.Ready ? AlfaUiTheme.Mint400 : member.Connected ? AlfaUiTheme.Moon200 : AlfaUiTheme.Pajama500;
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
            trainingBackLabel.text = busy ? trainingCancelLatched ? "CANCELANDO…" : "CANCELAR" : "VOLVER";
            SetRoleButtonSelection(trainingHumanButton, trainingState.SelectedRole == AlfaRole.Human, AlfaRole.Human);
            SetRoleButtonSelection(trainingMosquitoButton, trainingState.SelectedRole == AlfaRole.Mosquito, AlfaRole.Mosquito);
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
            customizationControlsGroup.interactable = !customizationSaveLatched;
            customizationControlsGroup.blocksRaycasts = !customizationSaveLatched;
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
            settingsControlsGroup.interactable = !settingsApplyLatched;
            settingsControlsGroup.blocksRaycasts = !settingsApplyLatched;
            masterVolume.SetValueWithoutNotify(settingsDraft.MasterVolume);
            musicVolume.SetValueWithoutNotify(settingsDraft.MusicVolume);
            effectsVolume.SetValueWithoutNotify(settingsDraft.EffectsVolume);
            humanSensitivity.SetValueWithoutNotify(settingsDraft.HumanSensitivity);
            mosquitoSensitivity.SetValueWithoutNotify(settingsDraft.MosquitoSensitivity);
            UpdateSliderValue(masterVolume);
            UpdateSliderValue(musicVolume);
            UpdateSliderValue(effectsVolume);
            UpdateSliderValue(humanSensitivity);
            UpdateSliderValue(mosquitoSensitivity);
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
            var bloodRatio = state.BloodTarget > 0f ? Mathf.Clamp01(state.BloodCurrent / state.BloodTarget) : 0f;
            hudBloodFill.rectTransform.anchorMax = new Vector2(bloodRatio, 1f);
            hudRoleLabel.text = state.Role == AlfaRole.Human ? "HUMANO" : "MOSQUITO";
            hudRoleIcon.Kind = state.Role == AlfaRole.Human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            hudRoleIcon.color = state.Role == AlfaRole.Human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            var roleColor = state.Role == AlfaRole.Human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            hudRoleBackground.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.22f);
            hudRoleOutline.effectColor = new Color(roleColor.r, roleColor.g, roleColor.b, 0.9f);
            hudInteraction.text = state.Interaction;
            hudHint.text = string.IsNullOrWhiteSpace(state.ContextHint) ? DefaultRoleHint(state.Role) : state.ContextHint;
            hudActorState.text = ActorStateText(state.Role, state.ActorState);
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
                trainingState = new TrainingUiState(trainingState.SelectedRole, false, trainingState.Message);
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
            pauseLeaveLabel.text = gameplayIsTraining ? "VOLVER AL MENÚ" : "SALIR DE LA SALA";
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
            var view = factory.View("MainMenuView", transform, false);
            var backdrop = view.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.18f);
            backdrop.raycastTarget = false;
            screens[AlfaUiScreen.MainMenu] = view;

            var panel = factory.Panel(view.transform, "MenuRail",
                new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.92f), 500f, 880f);
            Anchor(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(500f, 880f));
            panel.GetComponent<UnityEngine.UI.Outline>().effectColor = new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.72f);
            var menu = factory.Vertical(panel, "Content", 9f, TextAnchor.MiddleLeft);
            AlfaUiFactory.Fill(menu, 32f, 32f, 28f, 24f);
            factory.BrandLockup(menu, "Brand");
            factory.Text(menu, "Subtitle", "HUMANOS CONTRA MOSQUITOS", 22f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left, true);
            factory.Divider(menu, "BrandDivider", new Color(AlfaUiTheme.Lamp400.r, AlfaUiTheme.Lamp400.g, AlfaUiTheme.Lamp400.b, 0.9f), 3f);
            factory.Text(menu, "Question", "ELEGÍ CÓMO JUGAR", 20f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left, true);
            factory.FeatureButton(menu, "MainPlayButton", "JUGAR ONLINE", "CREÁ O UNITE A UNA SALA", ShowOnlineChoice, AlfaUiIconKind.Online, true, false, 88f);
            factory.FeatureButton(menu, "MainTrainingButton", "ENTRENAMIENTO", "PRACTICÁ CON BOTS", ShowTraining, AlfaUiIconKind.Training, false, false, 72f);
            factory.FeatureButton(menu, "MainCustomizeButton", "PERSONALIZAR", "HUMANO Y MOSQUITO", ShowCustomization, AlfaUiIconKind.Customize, false, false, 72f);
            factory.FeatureButton(menu, "MainSettingsButton", "AJUSTES", "AUDIO · VIDEO · CONTROLES", () => OpenSettings(AlfaUiScreen.MainMenu), AlfaUiIconKind.Settings, false, false, 72f);
            var quit = factory.FeatureButton(menu, "MainQuitButton", "SALIR", "CERRAR EL JUEGO", ConfirmQuit, AlfaUiIconKind.Exit, false, false, 72f);
            AlfaUiFactory.QuietButton(quit);
            factory.Text(menu, "NavigationHint", "FLECHAS / TAB  ·  ENTER  ·  ESC", 16f, AlfaUiTheme.Moon200);
            var version = string.IsNullOrWhiteSpace(Application.version) ? "ALFA" : Application.version.Replace("-", " / ").ToUpperInvariant();
            factory.Text(menu, "Version", version + "  ·  WINDOWS", 16f, AlfaUiTheme.Disabled);

            var sceneLabel = factory.Text(view.transform, "SceneLabel", "CASA CON PATIO  ·  NOCHE", 14f,
                new Color(AlfaUiTheme.Moon200.r, AlfaUiTheme.Moon200.g, AlfaUiTheme.Moon200.b, 0.82f), TextAlignmentOptions.Right, true);
            Anchor(sceneLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-42f, 28f), new Vector2(420f, 34f));
        }

        private void BuildOnlineChoice()
        {
            var view = factory.View("OnlineChoiceView", transform, false);
            SetSceneScrim(view, 0.76f);
            screens[AlfaUiScreen.OnlineChoice] = view;
            var panel = CenteredPanel(view.transform, "OnlineChoiceCard", 860f, 620f);
            var content = factory.Vertical(panel, "Content", 16f);
            AlfaUiFactory.Fill(content, 42f, 42f, 38f, 34f);
            factory.SectionHeader(content, "Header", "JUGAR ONLINE", AlfaUiIconKind.Online, AlfaUiTheme.Sky400);
            factory.Text(content, "Intro", "CREÁ UNA SALA O ENTRÁ CON EL CÓDIGO DE TUS AMIGOS.", 17f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            factory.Divider(content, "Divider", new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.55f));
            factory.FeatureButton(content, "CreateChoiceButton", "CREAR SALA", "GENERÁ UN CÓDIGO PARA INVITAR",
                () => ShowCreateRoom(), AlfaUiIconKind.Online, true, false, 92f);
            factory.FeatureButton(content, "JoinChoiceButton", "UNIRME CON CÓDIGO", "USÁ EL CÓDIGO DE DIEZ CARACTERES",
                () => ShowJoinRoom(), AlfaUiIconKind.Play, false, false, 92f);
            factory.Button(content, "OnlineChoiceBackButton", "VOLVER", ShowMainMenu, false, false, 56f, AlfaUiIconKind.Back);
        }

        private void BuildOnlineForm()
        {
            var view = factory.View("OnlineFormView", transform, false);
            SetSceneScrim(view, 0.78f);
            screens[AlfaUiScreen.CreateRoom] = view;
            screens[AlfaUiScreen.JoinRoom] = view;
            var panel = CenteredPanel(view.transform, "OnlineFormCard", 800f, 690f);
            var content = factory.Vertical(panel, "Content", 14f);
            AlfaUiFactory.Fill(content, 38f, 38f, 34f, 34f);
            var header = factory.SectionHeader(content, "Header", "SALA ONLINE", AlfaUiIconKind.Online, AlfaUiTheme.Sky400);
            onlineFormTitle = header.GetComponentInChildren<TextMeshProUGUI>();
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
            onlinePasteButton = factory.Button(codeActions, "PasteRoomCodeButton", "PEGAR", PasteRoomCode, false, false, 58f, AlfaUiIconKind.Copy);
            onlinePasteButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 150f;
            onlineStatus = factory.Text(content, "OnlineStatus", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            onlinePrimaryButton = factory.Button(content, "OnlinePrimaryButton", "CREAR SALA", SubmitOnline, true, false, 70f, AlfaUiIconKind.Play, false);
            onlinePrimaryLabel = onlinePrimaryButton.GetComponentInChildren<TextMeshProUGUI>();
            var actionsRow = factory.Horizontal(content, "Actions", 12f, TextAnchor.MiddleCenter);
            onlineCancelButton = factory.Button(actionsRow, "OnlineCancelButton", "CANCELAR", RequestOnlineCancel, false, true, 56f, AlfaUiIconKind.Exit);
            onlineRetryButton = factory.Button(actionsRow, "OnlineRetryButton", "REINTENTAR", SubmitOnline, false, false, 56f, AlfaUiIconKind.Play, false);
            onlineBackButton = factory.Button(actionsRow, "OnlineBackButton", "VOLVER", ShowOnlineChoice, false, false, 56f, AlfaUiIconKind.Back);
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
            var roomMark = factory.Icon(headerRow, "RoomIcon", AlfaUiIconKind.Online, AlfaUiTheme.Sky400);
            roomMark.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 42f;
            factory.Text(headerRow, "RoomLabel", "SALA ONLINE", 26f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
            lobbyCode = factory.Text(headerRow, "RoomCode", "PREPARANDO EL CÓDIGO…", 32f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center, true);
            lobbyCopyButton = factory.Button(headerRow, "LobbyCopyButton", "COPIAR PARA INVITAR", () =>
            {
                if (lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.RoomCode))
                {
                    actions.CopyRoomCode(lobbyState.RoomCode);
                    lobbyStatus.text = "Código copiado.";
                }
            }, true, false, 58f, AlfaUiIconKind.Copy);
            lobbyCopyButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 270f;

            var rosterPanel = factory.Panel(safe, "RosterPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.94f));
            Anchor(rosterPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(430f, -116f));
            var roster = factory.Vertical(rosterPanel, "Roster", 12f);
            AlfaUiFactory.Fill(roster, 24f, 24f, 22f, 22f);
            factory.SectionHeader(roster, "RosterHeader", "JUGADORES", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            factory.ScrollView(roster, "MemberScrollView", out var memberContent, 520f);
            for (var i = 0; i < LetMeSleep.Core.RoomRules.Capacity; i++)
            {
                var rowPanel = factory.Panel(memberContent, $"MemberPanel{i + 1}", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.46f), -1f, 64f);
                AlfaUiFactory.PlainShadow(rowPanel.gameObject).effectDistance = new Vector2(2f, -2f);
                var stateIcon = factory.Icon(rowPanel, "StatusIcon", AlfaUiIconKind.Human, AlfaUiTheme.Moon200);
                Anchor(stateIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(30f, 30f));
                var row = factory.Text(rowPanel, $"Member{i + 1}", string.Empty, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
                AlfaUiFactory.Fill(row.rectTransform, 52f, 14f, 8f, 8f);
                row.margin = new Vector4(4f, 10f, 14f, 10f);
                rowPanel.gameObject.SetActive(false);
                memberRows.Add(row);
                memberRowRoots.Add(rowPanel.gameObject);
                memberStatusIcons.Add(stateIcon);
            }

            var rulesPanel = factory.Panel(safe, "RulesPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f));
            Anchor(rulesPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(520f, 700f));
            var rules = factory.Vertical(rulesPanel, "Rules", 12f);
            AlfaUiFactory.Fill(rules, 26f, 26f, 24f, 24f);
            factory.SectionHeader(rules, "RulesHeader", "PRÓXIMA RONDA", AlfaUiIconKind.Play, AlfaUiTheme.Lamp400);
            lobbyRoleBadge = factory.Text(rules, "LocalAuthority", "SOS INVITADO", AlfaUiTheme.NoteSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center, true);
            factory.Divider(rules, "AuthorityDivider", new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.5f));
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
            }, true, false, 68f, AlfaUiIconKind.Ready);
            ApplyPositiveStyle(lobbyReadyButton);
            lobbyReadyLabel = lobbyReadyButton.GetComponentInChildren<TextMeshProUGUI>();
            lobbyStartButton = factory.Button(rules, "LobbyStartButton", "INICIAR RONDA", BeginRound, false, false, 66f, AlfaUiIconKind.Play);
            ApplyPositiveStyle(lobbyStartButton);
            lobbyStartLabel = lobbyStartButton.GetComponentInChildren<TextMeshProUGUI>();
            lobbyStartReason = factory.Text(rules, "StartReason", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center);
            lobbyExploreButton = factory.Button(rules, "LobbyExploreButton", "RECORRER SALA", BeginLobbyExploration, false, false, 58f, AlfaUiIconKind.Explore);
            lobbyStatus = factory.Text(rules, "LobbyStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
        }

        private void BuildTraining()
        {
            var view = factory.View("TrainingView", transform, false);
            view.GetComponent<UnityEngine.UI.Image>().color = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.24f);
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            screens[AlfaUiScreen.Training] = view;
            var panel = factory.Panel(view.transform, "TrainingCard", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.94f), 760f, 800f);
            Anchor(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(760f, 800f));
            var content = factory.Vertical(panel, "Content", 16f);
            AlfaUiFactory.Fill(content, 40f, 40f, 34f, 34f);
            factory.SectionHeader(content, "Header", "ENTRENAMIENTO", AlfaUiIconKind.Training, AlfaUiTheme.Lamp400);
            factory.Text(content, "Intro", "PRACTICÁ ANTES DE ENTRAR A UNA SALA.", 17f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            factory.Text(content, "RoleLabel", "ELEGÍ TU ROL", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left, true);
            var roles = factory.Horizontal(content, "Roles", 16f, TextAnchor.MiddleCenter);
            trainingHumanButton = factory.FeatureButton(roles, "TrainingHumanButton", "HUMANO", "DEFENDÉ TU DESCANSO",
                () => SelectTrainingRole(AlfaRole.Human), AlfaUiIconKind.Human, true, false, 86f);
            trainingMosquitoButton = factory.FeatureButton(roles, "TrainingMosquitoButton", "MOSQUITO", "VOLÁ Y EXTRAÉ SANGRE",
                () => SelectTrainingRole(AlfaRole.Mosquito), AlfaUiIconKind.Mosquito, false, false, 86f);
            AddReadOnlyField(content, "MODO", "SANGRE");
            AddReadOnlyField(content, "MAPA", "CASA CON PATIO");
            trainingStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            trainingStartButton = factory.Button(content, "TrainingStartButton", "EMPEZAR", BeginTraining, true, false, 74f, AlfaUiIconKind.Play);
            trainingStartLabel = trainingStartButton.GetComponentInChildren<TextMeshProUGUI>();
            trainingBackButton = factory.Button(content, "TrainingBackButton", "VOLVER", BackFromTraining, false, false, 58f, AlfaUiIconKind.Back);
            trainingBackLabel = trainingBackButton.GetComponentInChildren<TextMeshProUGUI>();
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
            factory.Text(humanCustomizationFields.transform, "SkinLabel", "TONO DE PIEL", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            humanPaletteRoot = CreatePaletteLayout(humanCustomizationFields.transform, "SkinPalette");
            factory.Text(humanCustomizationFields.transform, "PajamaLabel", "COLOR DE PIJAMA", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            pajamaPaletteRoot = CreatePaletteLayout(humanCustomizationFields.transform, "PajamaPalette");
            factory.Text(humanCustomizationFields.transform, "DefaultClothes", "Predeterminado: pijama, pantuflas y gorro de noche.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            mosquitoCustomizationFields = factory.Vertical(content, "MosquitoFields", 10f).gameObject;
            factory.Text(mosquitoCustomizationFields.transform, "MosquitoColorLabel", "COLOR", AlfaUiTheme.LabelSize, AlfaUiTheme.Lamp400);
            mosquitoPaletteRoot = CreatePaletteLayout(mosquitoCustomizationFields.transform, "MosquitoPalette");
            customizationStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            var footer = factory.Vertical(optionsPanel, "Actions", 10f);
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, 148f));
            customizationSaveButton = factory.Button(footer, "CustomizationSaveButton", "GUARDAR", SaveCustomization, true, false, 72f, AlfaUiIconKind.Ready);
            ApplyPositiveStyle(customizationSaveButton);
            customizationSaveLabel = customizationSaveButton.GetComponentInChildren<TextMeshProUGUI>();
            var secondary = factory.Horizontal(footer, "SecondaryActions", 12f);
            AlfaUiFactory.QuietButton(factory.Button(secondary, "CustomizationResetButton", "DESHACER CAMBIOS", ResetCustomization, false, false, 66f), 20f);
            AlfaUiFactory.QuietButton(factory.Button(secondary, "CustomizationBackButton", "VOLVER", CloseCustomization, false, false, 66f, AlfaUiIconKind.Back), 20f);
        }

        private void BuildSettings()
        {
            var view = factory.View("SettingsView", transform, false);
            SetSceneScrim(view, 0.78f);
            screens[AlfaUiScreen.Settings] = view;
            var panel = CenteredPanel(view.transform, "SettingsCard", 1280f, 840f);
            panel.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night800;
            var content = factory.Vertical(panel, "Content", 12f);
            settingsControlsGroup = content.gameObject.AddComponent<CanvasGroup>();
            AlfaUiFactory.Fill(content, 30f, 30f, 26f, 26f);
            factory.SectionHeader(content, "Header", "AJUSTES", AlfaUiIconKind.Settings, AlfaUiTheme.Sky400);
            factory.Text(content, "Intro", "CONFIGURÁ EL ALFA SIN SALIR DE LA NOCHE", AlfaUiTheme.NoteSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Left, true);

            var sections = factory.Horizontal(content, "Sections", 18f, TextAnchor.UpperCenter);
            var sectionsLayout = sections.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            sectionsLayout.preferredHeight = 558f;
            sectionsLayout.flexibleHeight = 0f;

            var leftColumn = factory.Vertical(sections, "LeftColumn", 16f);
            var leftLayout = leftColumn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leftLayout.preferredWidth = 570f;
            leftLayout.flexibleWidth = 1f;

            var audioPanel = factory.Panel(leftColumn, "AudioPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), -1f, 250f);
            var audio = factory.Vertical(audioPanel, "AudioContent", 10f);
            AlfaUiFactory.Fill(audio, 22f, 22f, 20f, 20f);
            factory.SectionHeader(audio, "AudioHeader", "AUDIO", AlfaUiIconKind.Audio, AlfaUiTheme.Lamp400);
            masterVolume = AddSliderField(audio, "VOLUMEN GENERAL", "MasterVolumeSlider", value => ChangeSetting(draft => draft.MasterVolume = value));
            musicVolume = AddSliderField(audio, "MÚSICA", "MusicVolumeSlider", value => ChangeSetting(draft => draft.MusicVolume = value));
            effectsVolume = AddSliderField(audio, "EFECTOS", "EffectsVolumeSlider", value => ChangeSetting(draft => draft.EffectsVolume = value));

            var controlsPanel = factory.Panel(leftColumn, "ControlsPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), -1f, 292f);
            var controls = factory.Vertical(controlsPanel, "ControlsContent", 10f);
            AlfaUiFactory.Fill(controls, 22f, 22f, 20f, 20f);
            factory.SectionHeader(controls, "ControlsHeader", "CONTROLES", AlfaUiIconKind.Controls, AlfaUiTheme.Mint400);
            humanSensitivity = AddSliderField(controls, "SENSIBILIDAD HUMANO", "HumanSensitivitySlider", value => ChangeSetting(draft => draft.HumanSensitivity = value), 0.1f, 2f);
            mosquitoSensitivity = AddSliderField(controls, "SENSIBILIDAD MOSQUITO", "MosquitoSensitivitySlider", value => ChangeSetting(draft => draft.MosquitoSensitivity = value), 0.1f, 2f);
            invertY = factory.Toggle(controls, "InvertYToggle", "INVERTIR EJE VERTICAL", value => ChangeSetting(draft => draft.InvertY = value));
            rebindNote = factory.Text(controls, "RebindNote", "La reasignación usa el contrato de entrada del juego.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200).gameObject;

            var videoPanel = factory.Panel(sections, "VideoPanel", new Color(AlfaUiTheme.Night700.r, AlfaUiTheme.Night700.g, AlfaUiTheme.Night700.b, 0.96f), 570f, 558f);
            videoPanel.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            videoSettings = videoPanel.gameObject;
            var video = factory.Vertical(videoPanel, "VideoContent", 10f);
            AlfaUiFactory.Fill(video, 22f, 22f, 20f, 20f);
            factory.SectionHeader(video, "VideoHeader", "VIDEO", AlfaUiIconKind.Video, AlfaUiTheme.Sky400);
            fullScreen = factory.Toggle(video, "FullScreenToggle", "PANTALLA COMPLETA", value => ChangeSetting(draft => draft.FullScreen = value));
            resolutionValue = AddCycleField(video, "RESOLUCIÓN", "Resolution", -1, 1, delta =>
                ChangeSetting(draft => draft.ResolutionIndex = Cycle(draft.ResolutionIndex, delta, settingsState?.Resolutions.Count ?? 0)));
            qualityValue = AddCycleField(video, "CALIDAD", "Quality", -1, 1, delta =>
                ChangeSetting(draft => draft.QualityIndex = Cycle(draft.QualityIndex, delta, settingsState?.Qualities.Count ?? 0)));
            vSync = factory.Toggle(video, "VSyncToggle", "SINCRONIZACIÓN VERTICAL", value => ChangeSetting(draft => draft.VSync = value));
            factory.Text(video, "FrameLimitLabel", "LÍMITE DE FPS", AlfaUiTheme.LabelSize, AlfaUiTheme.Moon200);
            frameLimitDropdown = factory.Dropdown(video, "FrameLimitDropdown",
                FrameLimitOptions.Select(FrameLimitLabel).ToArray(), index =>
                    ChangeSetting(draft => draft.FrameLimit = FrameLimitOptions[Mathf.Clamp(index, 0, FrameLimitOptions.Length - 1)]));
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
            var role = factory.Panel(view.transform, "RoleBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.86f));
            Anchor(role, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(210f, 62f));
            hudRoleBackground = role.GetComponent<UnityEngine.UI.Image>();
            hudRoleOutline = role.GetComponent<UnityEngine.UI.Outline>();
            hudRoleIcon = factory.Icon(role, "RoleIcon", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            Anchor(hudRoleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(36f, 36f));
            hudRoleLabel = factory.Text(role, "RoleLabel", "HUMANO", 18f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudRoleLabel.rectTransform, 54f, 12f, 8f, 8f);

            var clock = factory.Panel(view.transform, "ClockBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -24f), new Vector2(170f, 66f));
            var clockIcon = factory.Icon(clock, "ClockIcon", AlfaUiIconKind.Clock, AlfaUiTheme.Lamp400);
            Anchor(clockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(32f, 32f));
            hudClock = factory.Text(clock, "Clock", "03:00", 28f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudClock.rectTransform, 52f, 12f, 8f, 8f);

            var blood = factory.Panel(view.transform, "BloodBadge", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f));
            Anchor(blood, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(8f, -24f), new Vector2(260f, 66f));
            var bloodIcon = factory.Icon(blood, "BloodIcon", AlfaUiIconKind.Blood, AlfaUiTheme.Pajama500);
            Anchor(bloodIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(30f, 30f));
            hudBlood = factory.Text(blood, "Blood", "SANGRE  0 / 20", 20f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
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

            hudPromptPanel = factory.Panel(view.transform, "InteractionPrompt", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.88f)).gameObject;
            Anchor(hudPromptPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(560f, 58f));
            hudInteraction = factory.Text(hudPromptPanel.transform, "Interaction", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            AlfaUiFactory.Fill(hudInteraction.rectTransform, 18f, 18f, 8f, 8f);

            hudStatePanel = factory.Panel(view.transform, "ActorStatePanel", new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.9f)).gameObject;
            Anchor(hudStatePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(500f, 76f));
            hudActorState = factory.Text(hudStatePanel.transform, "ActorState", string.Empty, 24f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center, true);
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
            Anchor(hudHintPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(490f, 48f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, 15f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left);
            AlfaUiFactory.Fill(hudHint.rectTransform, 16f, 16f, 7f, 7f);
            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100);
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
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
            factory.Button(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, true, false, 68f, AlfaUiIconKind.Play);
            factory.Button(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause), false, false, 58f, AlfaUiIconKind.Settings);
            factory.Button(content, "PauseControlsButton", "CONTROLES", () => OpenSettings(AlfaUiScreen.Pause), false, false, 58f, AlfaUiIconKind.Training);
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
            var content = factory.Vertical(panel, "Content", 18f);
            AlfaUiFactory.Fill(content, 40f, 40f, 34f, 34f);
            resultsTitle = factory.Text(content, "Title", "RONDA INTERRUMPIDA", AlfaUiTheme.H1Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
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
            var labelText = factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100);
            var labelLayout = labelText.GetComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.minWidth = 190f;
            labelLayout.preferredWidth = 190f;
            labelLayout.flexibleWidth = 0f;
            var slider = factory.Slider(row, name, min, max, callback);
            slider.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 230f;
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

        private TextMeshProUGUI AddCycleField(Transform parent, string label, string name, int previous, int next, Action<int> changed)
        {
            var row = factory.Horizontal(parent, name + "Row", 10f, TextAnchor.MiddleCenter);
            var labelLayout = factory.Text(row, "Label", label, AlfaUiTheme.LabelSize, AlfaUiTheme.Sheet100).GetComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.minWidth = labelLayout.preferredWidth = 140f;
            labelLayout.flexibleWidth = 0f;
            var previousButton = factory.Button(row, name + "Previous", "‹", () => changed(previous), false, false, 66f);
            previousButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 66f;
            var value = factory.Text(row, name + "Value", "—", AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            value.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 220f;
            var nextButton = factory.Button(row, name + "Next", "›", () => changed(next), false, false, 66f);
            nextButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 66f;
            return value;
        }

        private void ConfigureOnlineForm(string rememberedName)
        {
            playerNameInput.SetTextWithoutNotify(rememberedName ?? string.Empty);
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
            RequestFeedback(UiFeedbackKind.Error);
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
            gameplayIsTraining = true;
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
                var label = (option.Id == selectedId ? "> " : string.Empty) + option.Label;
                var button = factory.Button(parent, "Color_" + option.Id, label, () => selected(captured), false, false, 66f);
                var colors = button.colors;
                colors.normalColor = AlfaUiTheme.Night600;
                colors.highlightedColor = Color.Lerp(AlfaUiTheme.Night600, AlfaUiTheme.Sky400, 0.3f);
                colors.selectedColor = AlfaUiTheme.Sky400;
                button.colors = colors;
                var outline = button.GetComponent<UnityEngine.UI.Outline>();
                outline.effectColor = option.Id == selectedId ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Ink900;
                outline.effectDistance = option.Id == selectedId ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
                var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                buttonLabel.color = AlfaUiTheme.Sheet100;
                buttonLabel.alignment = TextAlignmentOptions.Left;
                AlfaUiFactory.Fill(buttonLabel.rectTransform, 48f, 8f, 8f, 8f);
                buttonLabel.enableAutoSizing = false;
                buttonLabel.fontSize = 20f;
                buttonLabel.textWrappingMode = TextWrappingModes.NoWrap;
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 112f;
                var swatch = AlfaUiFactory.Node("Swatch", button.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
                swatch.GetComponent<UnityEngine.UI.Image>().color = option.Color;
                swatch.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                swatch.GetComponent<UnityEngine.UI.Outline>().effectColor = RelativeLuminance(option.Color) > 0.5f ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100;
                swatch.GetComponent<UnityEngine.UI.Outline>().effectDistance = new Vector2(1f, -1f);
                Anchor(swatch.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(28f, 28f));
                var selectionMark = factory.Icon(swatch.transform, "SelectionMark", AlfaUiIconKind.Ready,
                    RelativeLuminance(option.Color) > 0.5f ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100);
                AlfaUiFactory.Fill(selectionMark.rectTransform, 6f, 6f, 6f, 6f);
                selectionMark.gameObject.SetActive(option.Id == selectedId);
            }
        }

        private void UpdateCustomizationView()
        {
            if (customizationDraft == null || customizationState == null) return;
            var human = customizationDraft.Role == AlfaRole.Human;
            humanCustomizationFields.SetActive(human);
            mosquitoCustomizationFields.SetActive(!human);
            SetRoleButtonSelection(customizationHumanButton, human, AlfaRole.Human);
            SetRoleButtonSelection(customizationMosquitoButton, !human, AlfaRole.Mosquito);
            customizationPreviewTitle.text = human ? "VISTA EN VIVO · HUMANO" : "VISTA EN VIVO · MOSQUITO";
            customizationPreviewIcon.Kind = human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            customizationPreviewIcon.color = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            customizationCategoryTitle.text = human ? "PALETA DEL HUMANO" : "PALETA DEL MOSQUITO";
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
                var plain = label.text.StartsWith("> ", StringComparison.Ordinal) ? label.text.Substring(2) : label.text;
                var selected = child.name == "Color_" + selectedId;
                label.text = selected ? "> " + plain : plain;
                var outline = child.GetComponent<UnityEngine.UI.Outline>();
                if (outline != null)
                {
                    outline.effectColor = selected ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Ink900;
                    outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
                }
                var selectionMark = child.Find("Swatch/SelectionMark");
                if (selectionMark != null) selectionMark.gameObject.SetActive(selected);
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
                    else if (!OnlineBusy) ShowOnlineChoice();
                    else handled = false;
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

        private static void SetRoleButtonSelection(UnityEngine.UI.Button button, bool selected, AlfaRole role)
        {
            var roleColor = role == AlfaRole.Human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            var selectedContent = role == AlfaRole.Human ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100;
            var colors = button.colors;
            colors.normalColor = selected ? roleColor : AlfaUiTheme.Night600;
            colors.highlightedColor = selected ? Color.Lerp(roleColor, Color.white, 0.16f) : Color.Lerp(AlfaUiTheme.Night600, Color.white, 0.12f);
            colors.selectedColor = selected ? roleColor : AlfaUiTheme.Night600;
            button.colors = colors;
            var title = button.GetComponentInChildren<TextMeshProUGUI>();
            var plainTitle = title.text.StartsWith("> ", StringComparison.Ordinal) ? title.text.Substring(2) : title.text;
            title.text = selected ? "> " + plainTitle : plainTitle;
            var labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (var i = 0; i < labels.Length; i++)
                labels[i].color = selected ? selectedContent : labels[i].name == "Subtitle" ? AlfaUiTheme.Moon200 : AlfaUiTheme.Sheet100;
            var icon = button.GetComponentInChildren<AlfaUiIcon>();
            if (icon != null) icon.color = selected ? selectedContent : AlfaUiTheme.Sheet100;
            var plate = button.transform.Find("IconPlate");
            if (plate != null)
                plate.GetComponent<UnityEngine.UI.Image>().color = selected ?
                    new Color(AlfaUiTheme.Sheet100.r, AlfaUiTheme.Sheet100.g, AlfaUiTheme.Sheet100.b, 0.28f) :
                    new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.36f);
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null)
            {
                outline.effectColor = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Border;
                outline.effectDistance = selected ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);
            }
        }

        private static void ApplyPositiveStyle(UnityEngine.UI.Button button)
        {
            var colors = button.colors;
            colors.normalColor = AlfaUiTheme.Mint400;
            colors.highlightedColor = Color.Lerp(AlfaUiTheme.Mint400, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(AlfaUiTheme.Mint400, AlfaUiTheme.Ink900, 0.2f);
            colors.selectedColor = AlfaUiTheme.Mint400;
            button.colors = colors;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = AlfaUiTheme.Ink900;
            var icon = button.GetComponentInChildren<AlfaUiIcon>();
            if (icon != null) icon.color = AlfaUiTheme.Ink900;
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) outline.effectColor = AlfaUiTheme.Sheet100;
        }

        private static void SetSceneScrim(GameObject view, float alpha)
        {
            var image = view.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, alpha);
            image.raycastTarget = true;
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
