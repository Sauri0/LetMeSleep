using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3 stage-2 UI contracts: customization keeps its controls, scroll and focus across selections
    /// (ui-presentation-audio-8) and refreshes the viewer notice after a late bind; push-to-talk rebinding keeps
    /// unapplied settings and its Esc does not leave Ajustes (ui-presentation-audio-4); the pause voice list
    /// scrolls and is pooled (ui-presentation-audio-5/-6); HUD, menu and cards never overlap at 16:9, 16:10, 5:4
    /// and 21:9 (ui-presentation-audio-7); results and settings expose the UI-06 structure.
    /// </summary>
    public sealed class UiStageTwoPlayModeTests
    {
        private sealed class Actions : IMenuActions, IModularCustomizationActions, IVoiceActions
        {
            public readonly List<string> Calls = new List<string>();
            public Action<string, string> PendingRebind;
            public AlfaSettingsDraft Applied;
            public void CreateRoom(string playerName) { }
            public void JoinRoom(string playerName, string normalizedRoomCode) { }
            public void CancelOnline() { }
            public void CancelTraining() => Calls.Add("cancelTraining");
            public void CopyRoomCode(string groupedRoomCode) { }
            public void SetReady(bool ready) { }
            public void SetHumanCount(int? humanCount) { }
            public void StartRound() { }
            public void LeaveRoom() => Calls.Add("leave");
            public void StartTraining(AlfaRole role, string modeId, string mapId) => Calls.Add("training:" + role);
            public void PreviewCustomization(BasicCustomizationDraft draft) => Calls.Add("preview:" + draft.SkinColorId + ":" + draft.PajamaColorId + ":" + draft.MosquitoColorId);
            public void SaveCustomization(BasicCustomizationDraft draft) => Calls.Add("save");
            public void ApplySettings(AlfaSettingsDraft draft) => Applied = draft.Copy();
            public void SetGameplayInputBlocked(bool blocked) { }
            public void ResumeGame() { }
            public void ReturnToLobby() => Calls.Add("lobby");
            public void SetLobbyExploration(bool exploring) { }
            public void QuitGame() { }
            public void PreviewModularCustomization(AppearanceSelection draft, AlfaRole editedRole) => Calls.Add("modularPreview");
            public void SaveModularCustomization(AppearanceSelection draft, AlfaRole editedRole) => Calls.Add("modularSave");
            public void SetLocalVoiceMuted(bool muted) => Calls.Add("mute:" + muted);
            public void SetPeerVoiceMuted(string memberId, bool muted) => Calls.Add("peer:" + memberId + ":" + muted);
            public void BeginPushToTalkRebind(Action<string, string> completed) => PendingRebind = completed;
        }

        private Actions actions;
        private AlfaUiController ui;
        private Camera camera;
        private RenderTexture target;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            actions = new Actions();
            ui = AlfaUiRuntime.Create(actions, new AlfaUiDependencies(persistentAcrossScenes: false));
        }

        [TearDown]
        public void TearDown()
        {
            if (ui != null) Object.DestroyImmediate(ui.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
        }

        [UnityTest]
        public IEnumerator BasicSelectionKeepsSwatchesAndFocus()
        {
            var state = BasicState("warm", "blue", "red");
            ui.PresentCustomization(state);
            ui.ShowCustomization();
            yield return null;
            Invoke("SelectBasicCategory", "pajama");
            var red = Find("Color_red").GetComponent<UnityEngine.UI.Button>();
            EventSystem.current.SetSelectedGameObject(red.gameObject);
            red.onClick.Invoke();
            Assert.That(actions.Calls.Last(), Is.EqualTo("preview:warm:red:red"));
            // Bootstrap answers every preview with a fresh presentation (PresentPreferences).
            ui.PresentCustomization(BasicState("warm", "blue", "red", draft: new BasicCustomizationDraft(AlfaRole.Human, "warm", "red", "red")));
            yield return null;
            Assert.That(red == null, Is.False, "The palette was rebuilt although its options did not change.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(red.gameObject), "Keyboard focus was lost.");
            Assert.That(red.transform.Find("Swatch/SelectionMark").gameObject.activeSelf, Is.True, "The chosen swatch shows its check mark.");
            Assert.That(Label("SelectionLabel"), Is.EqualTo("Rojo"));
            Assert.That(Label("CustomizationSaveButton/Label"), Is.EqualTo("APLICAR"), "A changed look offers APLICAR.");
        }

        [UnityTest]
        public IEnumerator ModularSelectionKeepsButtonsScrollAndFocus()
        {
            var snapshot = ManyOptionsSnapshot();
            var selection = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, selection, AlfaRole.Human));
            ui.ShowCustomization();
            Invoke("SelectModularCategory", "human.hat");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var scroll = Find("OptionsScroll").GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            var last = Find("ModularOption_2_18").GetComponent<UnityEngine.UI.Button>();
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            last.onClick.Invoke();
            var draft = selection.Copy();
            draft.Human.SetOption("human.hat", "hat-18");
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, draft, AlfaRole.Human));
            yield return null;
            Assert.That(last == null, Is.False, "Selecting an option rebuilt the option grid.");
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(0.05f), "Selecting an option reset the scroll position.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(last.gameObject), "Keyboard focus was lost.");
            Assert.That(actions.Calls.Count(call => call == "modularPreview"), Is.EqualTo(1));

            Invoke("SetCustomizationRole", AlfaRole.Mosquito);
            yield return null;
            Assert.That(Find("ModularCategory_9"), Is.Not.Null, "Changing the role rebuilds the rail for that role.");
        }

        [UnityTest]
        public IEnumerator PreviewNoticeFollowsALateBind()
        {
            ui.PresentCustomization(BasicState("warm", "blue", "red"));
            ui.ShowCustomization();
            yield return null;
            var notice = Find("PreviewUnavailable").gameObject;
            Assert.That(notice.activeSelf, Is.True);
            var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true);
            orbit.Bind(PreviewRig());
            yield return null;
            Assert.That(notice.activeSelf, Is.False, "The 'viewer unavailable' notice stays after the viewer is bound.");
            Assert.That(orbit.CurrentInstance, Is.Not.Null);
            Assert.That(created.OfType<GameObject>().First(item => item.name == "PreviewStage").transform.Find("PreviewPedestal"), Is.Not.Null,
                "The viewer puts the character on a pedestal.");
        }

        [UnityTest]
        public IEnumerator PushToTalkRebindKeepsUnappliedSettingsAndEscOnlyCancels()
        {
            var saved = Settings();
            ui.PresentSettings(new SettingsUiState(saved, saved, new[] { "1920 × 1080" }, new[] { "PC" }, true, true));
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "SALA", "V", string.Empty, null));
            ui.OpenSettings(AlfaUiScreen.MainMenu);
            yield return null;
            Find("MasterVolumeSlider").GetComponent<UnityEngine.UI.Slider>().value = 0.35f;
            Find("PushToTalkRebindButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(actions.PendingRebind, Is.Not.Null);
            Assert.That(Label("PushToTalkRebindButton/Label"), Does.Contain("PULSÁ"), "The row says it is waiting for a key.");

            // Esc during the rebind cancels only the rebind.
            Invoke("HandleEscape");
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Settings));
            Assert.That(ui.IsModalOpen, Is.False, "Esc during a rebind asked to discard the settings.");

            Find("PushToTalkRebindButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            // Bootstrap persists the binding and re-presents the saved settings, then completes.
            var persisted = saved.Copy();
            persisted.PushToTalkBinding = "<Keyboard>/b";
            ui.PresentSettings(new SettingsUiState(persisted, persisted, new[] { "1920 × 1080" }, new[] { "PC" }, true, true));
            actions.PendingRebind("<Keyboard>/b", "B");
            var draft = (AlfaSettingsDraft)Field("settingsDraft");
            Assert.That(draft.MasterVolume, Is.EqualTo(0.35f).Within(0.001f), "The unapplied volume was discarded by the rebind.");
            Assert.That(draft.PushToTalkBinding, Is.EqualTo("<Keyboard>/b"));
            Assert.That(Label("PushToTalkKey/Key"), Is.EqualTo("B"));
            Find("SettingsApplyButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(actions.Applied.MasterVolume, Is.EqualTo(0.35f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PushToTalkNeedsARoom()
        {
            var saved = Settings();
            ui.PresentSettings(new SettingsUiState(saved, saved, new[] { "1920 × 1080" }, new[] { "PC" }, true, true));
            ui.PresentVoice(new VoiceUiState(false, false, false, false, string.Empty, "V", string.Empty, null));
            ui.OpenSettings(AlfaUiScreen.MainMenu);
            Invoke("SelectSettingsTab", SettingsTab("General"), false);
            yield return null;
            Assert.That(Find("PushToTalkRebindButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Label("PushToTalkNote"), Does.Contain("sala"));
        }

        [UnityTest]
        public IEnumerator SettingsTabsShowOnePageAtATime()
        {
            var saved = Settings();
            ui.PresentSettings(new SettingsUiState(saved, saved, new[] { "1920 × 1080" }, new[] { "PC" }, true, true, supportsReducedMenuMotion: true));
            ui.OpenSettings(AlfaUiScreen.MainMenu);
            yield return null;
            foreach (var (tab, page) in new[] { ("Audio", "AudioPanel"), ("Video", "VideoPanel"), ("Controls", "ControlsPanel"), ("Accessibility", "AccessibilityPanel"), ("General", "GeneralPanel") })
            {
                Find("SettingsTab" + tab).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                foreach (var other in new[] { "GeneralPanel", "AudioPanel", "VideoPanel", "ControlsPanel", "AccessibilityPanel" })
                    Assert.That(Find(other).gameObject.activeSelf, Is.EqualTo(other == page), tab + " shows " + other);
            }
            // Twin controls of the same option stay in sync (general volume: GENERAL and AUDIO, stage 3).
            Find("GeneralMasterVolumeSlider").GetComponent<UnityEngine.UI.Slider>().value = 0.35f;
            Assert.That(Find("MasterVolumeSlider").GetComponent<UnityEngine.UI.Slider>().value, Is.EqualTo(0.35f).Within(0.001f));
            Find("HumanSensitivitySlider").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            Find("SettingsResetButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(Find("MasterVolumeSlider").GetComponent<UnityEngine.UI.Slider>().value, Is.EqualTo(saved.MasterVolume).Within(0.001f));
            Assert.That(Find("GeneralMasterVolumeSlider").GetComponent<UnityEngine.UI.Slider>().value, Is.EqualTo(saved.MasterVolume).Within(0.001f));
            Assert.That(Find("HumanSensitivitySlider").GetComponent<UnityEngine.UI.Slider>().value, Is.EqualTo(saved.HumanSensitivity).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PauseVoiceListScrollsAndKeepsRows()
        {
            var participants = Enumerable.Range(1, 15).Select(i => new VoiceParticipantUiState("m" + i, "Jugador " + i, true, false, false)).ToArray();
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "RONDA", "V", string.Empty, participants));
            ui.ShowGameplay(false);
            ui.ShowPause();
            yield return UseCanvas(1920, 1080);
            var first = Find("VoicePeer_0");
            Assert.That(first.GetComponentInParent<UnityEngine.UI.ScrollRect>(), Is.Not.Null, "Voice rows are not in a scroll view.");
            AssertInside(Find("PauseLeaveButton"), "PauseLeaveButton");
            AssertInside(Find("PauseVoiceScroll"), "PauseVoiceScroll");
            participants[3] = new VoiceParticipantUiState("m4", "Jugador 4", true, false, true);
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "RONDA", "V", string.Empty, participants));
            yield return null;
            Assert.That(first == null, Is.False, "A speaking change recreated the voice rows.");
            Assert.That(Label("VoicePeer_3/Label"), Does.Contain("HABLANDO"));
            Find("VoicePeer_3").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(actions.Calls.Last(), Is.EqualTo("peer:m4:True"));
        }

        [UnityTest]
        public IEnumerator ResultsShowTheWinningTeamAndRealActions()
        {
            ui.PresentResults(new ResultsUiState(MatchOutcome.Humans, true, true, 12, 20, 175, humansCount: 1, mosquitoesCount: 3));
            yield return null;
            Assert.That(Label("ResultsBanner/Title"), Is.EqualTo("¡HUMANOS GANAN!"));
            Assert.That(Label("ResultsPrimaryButton/Label"), Is.EqualTo("JUGAR DE NUEVO"));
            Assert.That(Label("ResultsLeaveButton/Label"), Is.EqualTo("VOLVER AL MENÚ"));
            Assert.That(Label("ResultsHumansChip/Count"), Is.EqualTo("1"));
            Assert.That(Find("ResultsHumansChip/Crown").gameObject.activeSelf, Is.True);
            Assert.That(Find("ResultsMosquitoesChip/Crown").gameObject.activeSelf, Is.False);

            ui.PresentResults(new ResultsUiState(MatchOutcome.Mosquitoes, false, false, 20, 20, 90, humansCount: 3, mosquitoesCount: 2));
            yield return null;
            Assert.That(Label("ResultsBanner/Title"), Is.EqualTo("¡MOSQUITOS GANAN!"));
            Assert.That(Find("ResultsPrimaryButton").gameObject.activeSelf, Is.False, "Only the host returns the room to the lobby.");
            Assert.That(Label("Stats"), Does.Contain("ESPERANDO AL ANFITRIÓN"));
            // Fixed order whoever wins: humans (figure and chip) on the left, mosquitoes on the right.
            Assert.That(Find("HumanFigure").localPosition.x, Is.LessThan(Find("MosquitoFigure").localPosition.x));
            Assert.That(Find("ResultsHumansChip").localPosition.x, Is.LessThan(Find("ResultsMosquitoesChip").localPosition.x));
            Assert.That(Mathf.Abs(Find("HumanFigure").localPosition.x - Find("ResultsHumansChip").localPosition.x), Is.LessThan(40f),
                "Each chip sits under its own team's figure.");
            Assert.That(Find("MosquitoFigure").localScale.x, Is.EqualTo(1.15f).Within(0.001f), "The winner is shown at 1.15.");
            Assert.That(Find("HumanFigure").localScale.x, Is.EqualTo(0.8f).Within(0.001f), "The loser stands behind at 0.8.");
            Assert.That(Find("ResultsMosquitoesChip/Crown").gameObject.activeSelf, Is.True);
            Assert.That(Label("ResultsHumansChip/CountUnit"), Is.EqualTo("JUGADORES"), "Team counts are labelled.");

            ui.PresentResults(new ResultsUiState(MatchOutcome.Interrupted, false, false, 0, 20, 0, "Se perdió la conexión con la partida.",
                modeId: GameModes.Tasks));
            yield return null;
            Assert.That(Label("Stats"), Does.Not.Contain("TAREAS").And.Not.Contain("TIEMPO"), "An interrupted round shows no empty score.");
            Assert.That(Label("Stats"), Does.Contain("Se perdió la conexión"));
        }

        [UnityTest]
        public IEnumerator HumanHudShowsOneWarningPerSituationAndWholeBlood()
        {
            ui.PresentHud(new BloodHudUiState(AlfaRole.Human, 175, 0.2f, 18, interaction: "E · Confirmar reemplazo",
                contextHint: "¡Te están picando! Buscá al mosquito y golpeá hacia él.", modeId: GameModes.Blood, mosquitoesAlive: 2,
                equipment: new EquipmentHudUiState(new[]
                {
                    new EquipmentSlotUiState("MATAMOSCAS", "REUTILIZABLE", AlfaUiIconKind.Flyswatter),
                    new EquipmentSlotUiState("RAQUETA ELÉCTRICA", "3 CARGAS", AlfaUiIconKind.ElectricRacket),
                    new EquipmentSlotUiState("AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol)
                }, 0, .6f, swapOfferText: "E · REEMPLAZAR\nMATAMOSCAS POR PANTUFLA"), mosquitoesTotal: 4));
            yield return UseCanvas(1920, 1080);
            // Stage 3: bitten = edge vignette + chip under the crosshair; the swap offer is the "E" chip beside it.
            Assert.That(Label("ObjectiveValue"), Does.Contain("18").And.Not.Contain(","), "Blood is shown in whole units.");
            Assert.That(Find("SwapOfferChip"), Is.Null, "The swap offer is the interaction chip, not a second notice.");
            Assert.That(Find("InteractionPrompt").gameObject.activeSelf, Is.True);
            Assert.That(Label("PromptKey0/Key"), Is.EqualTo("E"));
            Assert.That(Label("Interaction"), Is.EqualTo("Reemplazar matamoscas por pantufla"), "One line, one replacement notice.");
            Assert.That(Find("BittenChip").gameObject.activeSelf, Is.True, "Being bitten is a chip under the crosshair.");
            Assert.That(Find("BittenVignette").gameObject.activeSelf, Is.True, "... and a red vignette on the edges.");
            Assert.That(Label("ActorState"), Does.Not.Contain("PICANDO"), "The bottom banner no longer repeats it.");
            Assert.That(Find("ContextHintPanel").gameObject.activeSelf, Is.False, "The corner toast never repeats the chip.");
            var slot = (RectTransform)Find("EquipmentSlotPlate1");
            Assert.That(slot.rect.width, Is.EqualTo(80f).Within(0.5f), "Inventory slots are 80 units.");
            Assert.That(Label("EquipmentNumber1"), Is.EqualTo("1"));
            Assert.That(Find("EquipmentSlotPlate0").localPosition.x, Is.GreaterThan(Find("EquipmentSlotPlate3").localPosition.x),
                "Objects 1-3 first, the hands (key 0) close the row.");
            Assert.That(Find("EquipmentSlotPlate1").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(new Color(0.118f, 0.2f, 0.345f, 1f)).Using(ColorComparer),
                "The selected slot keeps the navy #1E3358 fill (the frame marks it).");
            Assert.That(Find("PrivateEquipment").GetComponent<UnityEngine.UI.Image>().color.a, Is.LessThan(0.01f), "Loose slots: there is no tray behind them.");
            Assert.That(Find("EquipmentSlotPlate2").GetComponent<UnityEngine.UI.Image>().color.a, Is.GreaterThanOrEqualTo(0.95f), "Each slot is opaque on its own.");
        }

        [UnityTest]
        public IEnumerator MosquitoHudHasACompactLegendAndHearts()
        {
            ui.PresentHud(new BloodHudUiState(AlfaRole.Mosquito, 504, 0, 18, interaction: "Mantené E · Picar",
                contextHint: "Interrumpiendo al humano · Mantené E · Soltá E para despegar", actorState: HudActorState.Extracting,
                stateProgress01: .5f, modeId: GameModes.Tasks, tasksCompleted: 3, tasksGoal: 6, livesRemaining: 2, humansActive: 1, humansTotal: 4));
            yield return UseCanvas(1920, 1080);
            var legend = Find("ControlsLegend");
            Assert.That(legend.Cast<Transform>().Count(row => row.gameObject.activeSelf), Is.LessThanOrEqualTo(4), "At most four legend rows.");
            Assert.That(Label("LegendStatus"), Does.Contain("Interrumpiendo"), "The situation is in the legend header, in white.");
            Assert.That(Find("LegendStatus").GetComponent<TMPro.TextMeshProUGUI>().color, Is.EqualTo(new Color(0.949f, 0.965f, 1f, 1f)).Using(ColorComparer));
            var panel = (RectTransform)Find("ContextHintPanel");
            Assert.That(panel.rect.width, Is.LessThanOrEqualTo(340f), "Compact legend.");
            Assert.That(Label("Lives"), Does.Contain("VIDAS 2"));
            Assert.That(Find("InteractionPrompt").gameObject.activeSelf, Is.False, "The legend already says E · Picar.");
            Assert.That(Find("Hearts").Cast<Transform>().Count(heart => heart.gameObject.activeSelf), Is.EqualTo(2), "One heart per life.");
        }

        [UnityTest]
        public IEnumerator CustomizationRailsViewsAndOneCallToAction()
        {
            ui.PresentCustomization(BasicState("warm", "blue", "red"));
            ui.ShowCustomization();
            yield return UseCanvas(1920, 1080);
            foreach (var name in new[] { "CustomizationCategory_skin", "CustomizationCategory_pajama", "CustomizationCategory_accessories" })
                Assert.That(Find(name).gameObject.activeInHierarchy, Is.True, name);
            Assert.That(Label("CustomizationSaveButton/Label"), Is.EqualTo("APLICAR"));
            Assert.That(Find("CustomizationSaveButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False, "Nothing to apply yet.");
            foreach (var view in new[] { "PreviewFrontButton", "PreviewBackButton", "PreviewSideButton" })
                Assert.That(Find(view).gameObject.activeInHierarchy, Is.True, view + " lives in VISTA PREVIA");
            var front = (RectTransform)Find("PreviewFrontButton");
            Assert.That(front.rect.height, Is.GreaterThanOrEqualTo(150f), "Angle views fill the free height.");
            Assert.That(front.position.y, Is.GreaterThan(((RectTransform)Find("Actions")).position.y));

            Invoke("SetCustomizationRole", AlfaRole.Mosquito);
            yield return null;
            Assert.That(Label("CustomizationCategory_mosquito/Label"), Is.EqualTo("COLORES"), "The colours category fits in one line.");
            // Director pass (UI-06 6): the rail keeps CUERPO and COLORES, both live; what this build lacks is a locked
            // card inside CUERPO, never a locked rail entry.
            Assert.That(Find("CustomizationCategory_mosquito-body").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find("CustomizationCategory_mosquito-body").GetComponent<UnityEngine.UI.Button>().interactable, Is.True, "CUERPO is live.");
            Assert.That(Label("CustomizationCategory_mosquito-body/Label"), Is.EqualTo("CUERPO"));
            foreach (var gone in new[] { "CustomizationCategory_mosquito-wings", "CustomizationCategory_mosquito-eyes", "CustomizationCategory_mosquito-proboscis" })
                Assert.That(Find(gone), Is.Null, gone);
            Assert.That(Find("MosquitoStyle_Wings_Round/Lock"), Is.Not.Null, "REDONDAS is a locked card.");
            Assert.That(Label("CustomizationSaveButton/Label"), Is.EqualTo("APLICAR"), "Changing the look keeps the same call to action.");
            Assert.That(Find("CustomizationSaveButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator ModularWingsAndEyesShareTheListWithTheirOwnPictures()
        {
            var snapshot = MosquitoSnapshot();
            var selection = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, selection, AlfaRole.Mosquito));
            ui.ShowCustomization();
            yield return UseCanvas(1920, 1080);
            Invoke("SelectModularCategory", "mosquito.wings");
            Canvas.ForceUpdateCanvases();
            var viewport = Find("OptionsScroll").GetComponent<UnityEngine.UI.ScrollRect>().viewport;
            Assert.That(Visible(viewport, Find("ModularOption_3_1")), Is.True, "Wings visible after jumping to ALAS.");
            Assert.That(Visible(viewport, Find("ModularOption_4_1")), Is.True, "Eyes visible together with the wings (UI-06).");
            var art = new[] { 1, 2, 3, 4 }.Select(i => Find("ModularOption_3_" + i + "/OptionArt")).ToArray();
            Assert.That(art.All(item => item != null), Is.True, "Every wing style has its own picture.");
            Assert.That(art.Select(item => item.GetComponent<UnityEngine.UI.Image>().sprite.texture.name).Distinct().Count(), Is.EqualTo(4),
                "Four wing styles, four different silhouettes.");
            Assert.That(Label("Status"), Does.Not.Contain("llegará"));
        }

        private static bool Visible(RectTransform viewport, Transform item)
        {
            if (item == null) return false;
            var a = new Vector3[4]; var b = new Vector3[4];
            viewport.GetWorldCorners(a); ((RectTransform)item).GetWorldCorners(b);
            return b[1].y <= a[1].y + 1f && b[0].y >= a[0].y - 1f;
        }

        private static readonly IEqualityComparer<Color> ColorComparer = new ColorEquality();

        private sealed class ColorEquality : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
            public int GetHashCode(Color color) => 0;
        }

        [UnityTest]
        public IEnumerator SettingsFollowTheSketchAndApplyStaysGreen()
        {
            var saved = Settings();
            ui.PresentSettings(new SettingsUiState(saved, saved, new[] { "1920 × 1080" }, new[] { "PC" }, true, true, supportsReducedMenuMotion: true));
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "SALA", "V", string.Empty, null));
            ui.OpenSettings(AlfaUiScreen.MainMenu);
            yield return null;
            var general = Find("GeneralPanel");
            Assert.That(general.Find("LanguageRow"), Is.Not.Null, "GENERAL shows the language.");
            Assert.That(general.Find("PushToTalkRow"), Is.Not.Null, "GENERAL has the voice chat key.");
            foreach (var slider in new[] { "HumanSensitivitySlider", "MosquitoSensitivitySlider", "GeneralMasterVolumeSlider", "GeneralMusicVolumeSlider", "GeneralEffectsVolumeSlider" })
                Assert.That(general.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Any(item => item.name == slider), Is.True, "GENERAL has " + slider + " (UI-06).");
            Assert.That(Find("ControlsPanel").GetComponentsInChildren<UnityEngine.UI.Slider>(true), Is.Empty, "The sensitivity is not repeated in CONTROLES.");
            Assert.That(general.Find("GeneralFullScreenRow"), Is.Null, "Full screen lives only in VIDEO.");
            Assert.That(Find("AudioPanel").GetComponentsInChildren<UnityEngine.UI.Slider>(true).Any(slider => slider.name == "MasterVolumeSlider"), Is.True);
            var apply = Find("SettingsApplyButton").GetComponent<UnityEngine.UI.Button>();
            Assert.That(apply.interactable, Is.False);
            var surface = Find("SettingsApplyButton").GetComponent("AlfaUiSurface");
            var keepsIntent = surface.GetType().GetField("DisabledKeepsIntent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
            Assert.That(keepsIntent, Is.EqualTo(true), "APLICAR stays green (content at 55 %) with nothing to apply.");
            Find("PushToTalkRebindButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(Label("Actions/Status"), Is.Empty, "A single waiting text: the row's.");
            Assert.That(Label("PushToTalkNote"), Is.EqualTo("Esc cancela"));
        }

        [UnityTest]
        public IEnumerator PauseVoicePanelShowsWholeRowsAndStaysCompact()
        {
            var participants = Enumerable.Range(1, 9).Select(i => new VoiceParticipantUiState("m" + i, "Jugador " + i, true, false, false)).ToArray();
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "RONDA", "V", string.Empty, participants));
            ui.ShowGameplay(false);
            ui.ShowPause();
            yield return UseCanvas(1920, 1080);
            var card = (RectTransform)Find("PauseCard");
            var voice = (RectTransform)Find("PauseVoicePanel");
            Assert.That(voice.rect.height, Is.LessThan(card.rect.height), "The voice panel is smaller than the pause menu.");
            Assert.That(voice.rect.width, Is.LessThanOrEqualTo(card.rect.width * 0.7f + 1f), "The voice panel is at most 70 % of the menu width.");
            var viewport = Find("PauseVoiceScroll").GetComponent<UnityEngine.UI.ScrollRect>().viewport;
            var rows = viewport.rect.height / (52f + 8f);
            Assert.That(Mathf.Abs(viewport.rect.height - (3 * 52f + 2 * 8f + 6f)), Is.LessThan(1f), "Three whole rows, two gaps and the list padding: " + rows);
            Assert.That(Find("PeersFade").gameObject.activeSelf, Is.True, "More rows below: the bottom edge fades.");
        }

        private static CustomizationCatalogSnapshot MosquitoSnapshot()
        {
            var slots = new List<CustomizationSlotRecord>
            {
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.base", Label = "PERSONAJE", WireSlotId = 1, Required = true, IsBaseSlot = true, DefaultOptionId = "base-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.base", Label = "CUERPO", WireSlotId = 2, Required = true, IsBaseSlot = true, DefaultOptionId = "body-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.wings", Label = "ALAS", WireSlotId = 3, DefaultOptionId = "wings-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.eyes", Label = "OJOS", WireSlotId = 4, DefaultOptionId = "eyes-a" }
            };
            var options = new List<CustomizationOptionRecord>
            {
                Visual(CustomizationRole.Human, "human.base", "base-a", 1),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "body-a", 1),
                Named("mosquito.wings", "wings-a", "FACETADAS", 1), Named("mosquito.wings", "wings-b", "REDONDAS", 2),
                Named("mosquito.wings", "wings-c", "LARGAS Y FINAS", 3), Named("mosquito.wings", "wings-d", "CORTAS", 4),
                Named("mosquito.eyes", "eyes-a", "GRANDES", 1), Named("mosquito.eyes", "eyes-b", "ENOJADOS", 2)
            };
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.ui.stage2.art", 1, slots, options, out var snapshot, out var errors), Is.True, string.Join("\n", errors));
            return snapshot;
        }

        private static CustomizationOptionRecord Named(string slotId, string optionId, string label, ushort wire) =>
            new CustomizationOptionRecord { Role = CustomizationRole.Mosquito, SlotId = slotId, OptionId = optionId, WireOptionId = wire,
                Kind = CustomizationOptionKind.SkinnedPart, Label = label, AssetId = "synthetic-" + optionId, HasRuntimeAsset = true };

        private static readonly Vector2Int[] Aspects =
        {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(1280, 1024),
            new Vector2Int(1600, 1200), new Vector2Int(2560, 1080), new Vector2Int(3440, 1440)
        };

        [UnityTest]
        public IEnumerator HudDoesNotOverlapAtAnyAspect()
        {
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "RONDA", "V", string.Empty, null));
            foreach (var size in Aspects)
            {
                ui.PresentHud(HumanHud());
                yield return UseCanvas(size.x, size.y);
                ui.PresentHud(HumanHud());
                Canvas.ForceUpdateCanvases();
                AssertNoOverlap(size, "RoleBadge", "ClockBadge", "NetworkState", "TeamCounter", "VoiceChip", "PrivateTask",
                    "PrivateEquipment", "BittenChip", "InteractionPrompt", "ActorStatePanel", "ContextHintPanel");
                ui.PresentHud(MosquitoHud());
                Canvas.ForceUpdateCanvases();
                AssertNoOverlap(size, "RoleBadge", "LivesChip", "ClockBadge", "TeamCounter", "VoiceChip", "InteractionPrompt",
                    "ActorStatePanel", "ContextHintPanel");
            }
        }

        [UnityTest]
        public IEnumerator ScreensFitAtAnyAspect()
        {
            foreach (var size in Aspects)
            {
                yield return UseCanvas(size.x, size.y);
                ui.ShowMainMenu();
                Canvas.ForceUpdateCanvases();
                AssertNoOverlap(size, "Brand", "MenuRail");
                ui.PresentCustomization(BasicState("warm", "blue", "red"));
                ui.ShowCustomization();
                Canvas.ForceUpdateCanvases();
                AssertNoOverlap(size, "Header", "RoleTabs");
                AssertNoOverlap(size, "CategoryRail", "PreviewPanel", "OptionsPanel");
                Assert.That(((RectTransform)Find("PreviewPanel")).rect.width, Is.GreaterThan(480f), "Viewer too narrow at " + size);
                ui.OpenSettings(AlfaUiScreen.MainMenu);
                Canvas.ForceUpdateCanvases();
                AssertInside(Find("SettingsCard"), "SettingsCard at " + size);
                ui.PresentResults(new ResultsUiState(MatchOutcome.Humans, true, true, 12, 20, 175));
                Canvas.ForceUpdateCanvases();
                AssertInside(Find("ResultsCard"), "ResultsCard at " + size);
                ui.ShowGameplay(true);
                ui.ShowPause();
                Canvas.ForceUpdateCanvases();
                AssertInside(Find("PauseLeaveButton"), "PauseLeaveButton at " + size);
            }
        }

        private static BloodHudUiState HumanHud() => new BloodHudUiState(AlfaRole.Human, 93, 8, 18,
            interaction: "E · Recoger objeto · G · soltar equipado",
            contextHint: "¡Te están picando! Buscá al mosquito y golpeá hacia él.",
            actorState: HudActorState.Bitten, networkMessage: "Esperando a los jugadores…", modeId: GameModes.Tasks,
            tasksCompleted: 2, tasksGoal: 6, mosquitoesAlive: 2, privateTaskText: "LIMPIÁ LA MESA DE POPA\nMantené E junto al objeto", taskProgress01: .4f,
            equipment: new EquipmentHudUiState(new[]
            {
                new EquipmentSlotUiState("PANTUFLA", "REUTILIZABLE", AlfaUiIconKind.Slipper),
                new EquipmentSlotUiState("RAQUETA ELÉCTRICA", "3 CARGAS", AlfaUiIconKind.ElectricRacket),
                new EquipmentSlotUiState("AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol)
            }, 1, .42f, .78f, swapOfferText: "E · REEMPLAZAR\nRAQUETA ELÉCTRICA POR MATAMOSCAS"),
            mosquitoesTotal: 4, humansActive: 2, humansTotal: 3);

        private static BloodHudUiState MosquitoHud() => new BloodHudUiState(AlfaRole.Mosquito, 93, 8, 18,
            interaction: "Mantené E · Picar", contextHint: "Extrayendo sangre · Mantené E · Soltá E para despegar",
            actorState: HudActorState.Extracting, stateProgress01: .6f, modeId: GameModes.Tasks, tasksCompleted: 2, tasksGoal: 6,
            mosquitoesAlive: 3, livesRemaining: 2, mosquitoesTotal: 4, humansActive: 1, humansTotal: 3);

        private IEnumerator UseCanvas(int width, int height)
        {
            if (camera == null)
            {
                camera = new GameObject("UiStageTwoCamera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.enabled = false;
                var canvas = ui.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            if (target != null) { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
            target = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)ui.transform;
            var expected = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1920f, 2f), Mathf.Log(height / 1080f, 2f), 0.5f));
            Assert.That(root.rect.width, Is.EqualTo(width / expected).Within(2f), "Canvas did not rescale to " + width + "x" + height);
        }

        private void AssertNoOverlap(Vector2Int size, params string[] names)
        {
            var rects = names.Select(name => (name, rect: ActiveRect(name))).Where(item => item.rect.HasValue).ToList();
            var root = ((RectTransform)ui.transform).rect;
            foreach (var item in rects)
                Assert.That(Inside(root, item.rect.Value), Is.True, item.name + " leaves the canvas at " + size + ": " + item.rect.Value + " in " + root);
            for (var i = 0; i < rects.Count; i++)
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = Shrink(rects[i].rect.Value); var b = Shrink(rects[j].rect.Value);
                Assert.That(a.Overlaps(b), Is.False, rects[i].name + " overlaps " + rects[j].name + " at " + size + ": " + a + " / " + b);
            }
        }

        private void AssertInside(Transform item, string label)
        {
            Assert.That(item, Is.Not.Null, label);
            var rect = CanvasRect((RectTransform)item);
            var root = ((RectTransform)ui.transform).rect;
            Assert.That(Inside(root, rect), Is.True, label + " leaves the canvas: " + rect + " in " + root);
        }

        private Rect? ActiveRect(string name)
        {
            var item = ui.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(rect => rect.name == name);
            if (item == null) return null;
            var text = item.GetComponent<TextMeshProUGUI>();
            if (text != null && string.IsNullOrWhiteSpace(text.text)) return null;
            return CanvasRect(item);
        }

        private Rect CanvasRect(RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            var root = (RectTransform)ui.transform;
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1f && inner.yMin >= outer.yMin - 1f &&
            inner.xMax <= outer.xMax + 1f && inner.yMax <= outer.yMax + 1f;

        private static Rect Shrink(Rect rect) => Rect.MinMaxRect(rect.xMin + 1f, rect.yMin + 1f, rect.xMax - 1f, rect.yMax - 1f);

        private CharacterPreviewSetup PreviewRig()
        {
            var stage = new GameObject("PreviewStage");
            stage.transform.position = new Vector3(500f, 0f, 0f);
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            var previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.cullingMask = 1 << 30;
            previewCamera.enabled = false;
            var texture = new RenderTexture(256, 256, 16);
            var human = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            human.name = "HumanPrefabStub";
            human.SetActive(false);
            var mosquito = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mosquito.name = "MosquitoPrefabStub";
            mosquito.SetActive(false);
            created.AddRange(new Object[] { stage, cameraObject, texture, human, mosquito });
            return new CharacterPreviewSetup(previewCamera, stage.transform, texture, human, mosquito);
        }

        private static CustomizationUiState BasicState(string skin, string pajama, string mosquito, BasicCustomizationDraft draft = null)
        {
            var skins = new[] { new NamedColorOption("light", "Claro", new Color(.91f, .7f, .5f)), new NamedColorOption("warm", "Cálido", new Color(.72f, .4f, .25f)) };
            var pajamas = new[] { new NamedColorOption("blue", "Azul", new Color(.12f, .32f, .51f)), new NamedColorOption("red", "Rojo", new Color(.65f, .17f, .16f)) };
            var mosquitoes = new[] { new NamedColorOption("red", "Rojo", new Color(.55f, .14f, .11f)), new NamedColorOption("blue", "Azul", new Color(.17f, .3f, .52f)) };
            var saved = new BasicCustomizationDraft(AlfaRole.Human, skin, pajama, mosquito);
            return new CustomizationUiState(skins, pajamas, mosquitoes, saved, draft);
        }

        private static CustomizationCatalogSnapshot ManyOptionsSnapshot()
        {
            var slots = new List<CustomizationSlotRecord>
            {
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.base", Label = "BASE", WireSlotId = 1, Required = true, IsBaseSlot = true, DefaultOptionId = "base-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.hat", Label = "GORROS", WireSlotId = 2, DefaultOptionId = "hat-1" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.base", Label = "CUERPO", WireSlotId = 9, Required = true, IsBaseSlot = true, DefaultOptionId = "body-a" }
            };
            var options = new List<CustomizationOptionRecord>
            {
                Visual(CustomizationRole.Human, "human.base", "base-a", 1),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "body-a", 1)
            };
            for (ushort i = 1; i <= 18; i++)
                options.Add(new CustomizationOptionRecord { Role = CustomizationRole.Human, SlotId = "human.hat", OptionId = "hat-" + i, WireOptionId = i,
                    Kind = CustomizationOptionKind.Color, Label = "GORRO " + i, HasSwatch = true, SwatchRgba = 0x2D4F9AFF + (uint)(i << 8) });
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.ui.stage2", 1, slots, options, out var snapshot, out var errors), Is.True, string.Join("\n", errors));
            return snapshot;
        }

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slotId, string optionId, ushort wire) =>
            new CustomizationOptionRecord { Role = role, SlotId = slotId, OptionId = optionId, WireOptionId = wire,
                Kind = CustomizationOptionKind.SkinnedPart, Label = optionId.ToUpperInvariant(), AssetId = "synthetic-" + optionId, HasRuntimeAsset = true };

        private static AlfaSettingsDraft Settings() => new AlfaSettingsDraft
        {
            MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .85f, VoiceVolume = .8f, PushToTalkBinding = "<Keyboard>/v",
            FullScreen = true, HumanSensitivity = 1f, MosquitoSensitivity = 1f
        };

        private object SettingsTab(string name)
        {
            var type = typeof(AlfaUiController).GetNestedType("SettingsTab", BindingFlags.NonPublic);
            return Enum.Parse(type, name);
        }

        private Transform Find(string path)
        {
            var parts = path.Split('/');
            var node = ui.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == parts[0] && item.gameObject.activeInHierarchy)
                ?? ui.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == parts[0]);
            for (var i = 1; node != null && i < parts.Length; i++) node = node.Find(parts[i]);
            return node;
        }

        private string Label(string path)
        {
            var node = Find(path);
            Assert.That(node, Is.Not.Null, path);
            var text = node.GetComponent<TextMeshProUGUI>();
            return (text != null ? text : node.GetComponentInChildren<TextMeshProUGUI>(true)).text;
        }

        private object Field(string name) => typeof(AlfaUiController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);

        private void Invoke(string method, params object[] arguments)
        {
            var info = typeof(AlfaUiController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(item => item.Name == method && item.GetParameters().Length == arguments.Length);
            info.Invoke(ui, arguments);
        }
    }
}
