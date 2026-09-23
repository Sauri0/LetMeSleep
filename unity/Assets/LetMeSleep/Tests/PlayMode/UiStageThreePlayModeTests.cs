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
    /// v0.3 stage-3 UI contracts (docs: Validation/V030/deltas/ui-stage3.md): the results group of winners on one
    /// baseline with contact shadows, the pill score line and the green call to action on the right; the online
    /// pause menu of the sketch (VOLVER A LA SALA for the host, SALIR DE LA PARTIDA) with a voice list of three whole
    /// rows at 720p; one banner over loose inventory slots, bitten chip and vignette, the interaction chip beside the
    /// crosshair; one mosquito legend panel with a fixed key column; GENERAL settings as the sketch with one control
    /// column; inactive APLICAR keeping its green; the 3x3 palettes, the preview row inside the content margin and
    /// ALEATORIO right under it; modular cards clear of the scrollbar; training portraits that never cut the head.
    /// Director pass: winners 210 units apart with their own colours over one shadow no wider than the chip,
    /// mosquitoes 230 apart facing each other; the guest's locked VOLVER A LA SALA and no voice panel without a
    /// session; the 30 % edge vignette and the neutral E chip; the mosquito header without dangling dots; settings
    /// cards fitted to their rows; the mosquito's CUERPO column with locked cards; VISTA PREVIA 16 units under the
    /// options; the map picture in the create carousel; the painted face in the HUD badge; nametag chips; the modal scrim.
    /// </summary>
    public sealed class UiStageThreePlayModeTests
    {
        private sealed class Actions : IMenuActions, IModularCustomizationActions, IVoiceActions
        {
            public readonly List<string> Calls = new List<string>();
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
            public void PreviewCustomization(BasicCustomizationDraft draft) => Calls.Add("preview");
            public void SaveCustomization(BasicCustomizationDraft draft) => Calls.Add("save");
            public void ApplySettings(AlfaSettingsDraft draft) { }
            public void SetGameplayInputBlocked(bool blocked) { }
            public void ResumeGame() => Calls.Add("resume");
            public void ReturnToLobby() => Calls.Add("lobby");
            public void SetLobbyExploration(bool exploring) { }
            public void QuitGame() { }
            public void PreviewModularCustomization(AppearanceSelection draft, AlfaRole editedRole) => Calls.Add("modularPreview");
            public void SaveModularCustomization(AppearanceSelection draft, AlfaRole editedRole) => Calls.Add("modularSave");
            public void SetLocalVoiceMuted(bool muted) => Calls.Add("mute:" + muted);
            public void SetPeerVoiceMuted(string memberId, bool muted) => Calls.Add("peer:" + memberId + ":" + muted);
            public void BeginPushToTalkRebind(Action<string, string> completed) { }
        }

        private Actions actions;
        private AlfaUiController ui;
        private Camera camera;
        private RenderTexture target;

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
            if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
        }

        [UnityTest]
        public IEnumerator ResultsGroupTheWinnersOnOneBaseline()
        {
            yield return UseCanvas(1920, 1080);
            var blue = new Color(.176f, .31f, .604f);
            var red = new Color(.65f, .17f, .16f);
            var looks = new[]
            {
                new ResultsFigureUiState(AlfaRole.Human, new Color(.788f, .545f, .353f), blue, null, true),
                new ResultsFigureUiState(AlfaRole.Human, new Color(.941f, .788f, .627f), red),
                new ResultsFigureUiState(AlfaRole.Human, new Color(.4f, .216f, .125f), new Color(.16f, .4f, .27f))
            };
            ui.PresentResults(new ResultsUiState(MatchOutcome.Humans, false, true, 5, 20, 175, humansCount: 4, mosquitoesCount: 2, figures: looks));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var group = Find("HumanFigure");
            var figures = group.GetComponentsInChildren<UnityEngine.UI.RawImage>(false).Where(image => image.enabled).ToList();
            Assert.That(figures.Count, Is.EqualTo(3), "min(players, 3) winners stand together.");
            var byX = figures.OrderBy(image => CanvasRect(image.rectTransform).center.x).ToList();
            var centre = byX[1];
            var centres = byX.Select(image => CanvasRect(image.rectTransform).center.x).ToList();
            Assert.That(centres[1] - centres[0], Is.EqualTo(210f).Within(2f), "Winners 210 units apart (no clones overlapping).");
            Assert.That(centres[2] - centres[1], Is.EqualTo(210f).Within(2f));
            Assert.That(Mathf.Abs(centres[1]), Is.LessThan(2f), "The group is centred on the screen.");
            Assert.That(CanvasRect(centre.rectTransform).yMin, Is.EqualTo(-540f + 260f).Within(2f), "Feet at y 820 of 1080.");
            foreach (var side in new[] { byX[0], byX[2] })
            {
                Assert.That(CanvasRect(side.rectTransform).yMin - CanvasRect(centre.rectTransform).yMin, Is.EqualTo(12f).Within(1f), "Side figures stand 12 units higher.");
                Assert.That(CanvasRect(side.rectTransform).height / CanvasRect(centre.rectTransform).height, Is.EqualTo(0.92f).Within(0.01f), "Side figures at 0.92.");
                Assert.That(side.transform.GetSiblingIndex(), Is.LessThan(centre.transform.GetSiblingIndex()), "Side figures stand behind the centre one.");
            }
            Assert.That(figures.Select(image => image.texture).Distinct().Count(), Is.EqualTo(3), "Each winner wears their own colours.");
            var shadows = group.GetComponentsInChildren<UnityEngine.UI.Image>(false).Where(image => image.name.StartsWith("ContactShadow")).ToList();
            Assert.That(shadows.Count, Is.EqualTo(1), "One contact shadow under the group.");
            var shadow = CanvasRect(shadows[0].rectTransform);
            var chip = CanvasRect((RectTransform)Find("ResultsHumansChip"));
            Assert.That(shadow.xMin, Is.GreaterThanOrEqualTo(chip.xMin - 1f), "The shadow never shows past the chip.");
            Assert.That(shadow.xMax, Is.LessThanOrEqualTo(chip.xMax + 1f));
            Assert.That(shadows[0].color.a, Is.EqualTo(0.35f).Within(0.01f), "35 % in the centre (radial, 0 % at the edge).");
            Assert.That(shadows[0].transform.GetSiblingIndex(), Is.EqualTo(0), "Behind every figure.");
            Assert.That(chip.yMax, Is.GreaterThan(CanvasRect(centre.rectTransform).yMin), "The winner chip covers the legs.");
            Assert.That(Find("ResultsHumansChip").GetSiblingIndex(), Is.GreaterThan(group.GetSiblingIndex()), "Chips are drawn in front of the figures.");
            Assert.That(Find("WinnerGlow").GetComponent<UnityEngine.UI.Image>().color.a, Is.LessThanOrEqualTo(0.26f), "Only a soft 25 % light.");
            var title = Find("ResultsBanner/Title").GetComponent<TextMeshProUGUI>();
            Assert.That(title.fontSize, Is.LessThanOrEqualTo(116f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(title.rectTransform.localEulerAngles.z, 3f)), Is.LessThan(0.1f), "The title is tilted like the sketch.");
            // Host: green VOLVER A LA SALA on the right, navy SALIR DE LA SALA on the left.
            var primary = (RectTransform)Find("ResultsPrimaryButton");
            var leave = (RectTransform)Find("ResultsLeaveButton");
            Assert.That(CanvasRect(primary).center.x, Is.GreaterThan(CanvasRect(leave).center.x));
            Assert.That(SurfaceColor(primary, "GradientTop"), Is.EqualTo(new Color(0.275f, 0.769f, 0.373f, 1f)).Using(ColorComparer), "The call to action is the green #46C45F.");
            var pill = Find("StatsPill").GetComponent<UnityEngine.UI.Image>();
            Assert.That(pill.color.a, Is.EqualTo(0.7f).Within(0.01f), "The score line sits on a #0E1A30 pill at 70 %.");
            Assert.That(Find("Stats").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(new Color(0.949f, 0.965f, 1f, 1f)).Using(ColorComparer));

            ui.PresentResults(new ResultsUiState(MatchOutcome.Mosquitoes, false, true, 20, 20, 142, humansCount: 3, mosquitoesCount: 2));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var mosquitoes = Find("MosquitoFigure").GetComponentsInChildren<UnityEngine.UI.RawImage>(false).Where(image => image.enabled)
                .OrderBy(image => CanvasRect(image.rectTransform).center.x).ToList();
            Assert.That(mosquitoes.Count, Is.EqualTo(2));
            Assert.That(CanvasRect(mosquitoes[0].rectTransform).center.x, Is.EqualTo(-230f).Within(2f), "Mosquitoes 230 units each side.");
            Assert.That(CanvasRect(mosquitoes[1].rectTransform).center.x, Is.EqualTo(230f).Within(2f));
            Assert.That(Mathf.Sign(mosquitoes[0].uvRect.width), Is.Not.EqualTo(Mathf.Sign(mosquitoes[1].uvRect.width)),
                "The two face each other: each big wing on the outer side, no crossed wings in the middle.");
            Assert.That(Find("HumanFigure").GetComponentsInChildren<UnityEngine.UI.RawImage>(false).Count(image => image.enabled), Is.EqualTo(1), "The loser is one figure.");
            Assert.That(Find("HumanFigure").localScale.x, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(Find("HumanFigure").GetSiblingIndex(), Is.LessThan(Find("ResultsHumansChip").GetSiblingIndex()), "The loser stands behind its chip.");
        }

        [UnityTest]
        public IEnumerator OnlinePauseOffersTheSketchMenu()
        {
            ui.PresentLobby(new LobbyUiState(true, "ABCDE12345", new[] { new LobbyMemberUiState("m0", "Branko", true), new LobbyMemberUiState("m1", "Luna", true) },
                true, false, null, true, string.Empty));
            ui.ShowGameplay(false);
            ui.ShowPause();
            yield return UseCanvas(1920, 1080);
            Assert.That(Find("PauseLobbyButton").gameObject.activeInHierarchy, Is.True, "VOLVER A LA SALA in an online round.");
            Assert.That(Find("PauseLobbyButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.True, "The host can end the round.");
            Assert.That(Label("PauseLeaveButton/Label"), Is.EqualTo("SALIR DE LA PARTIDA"));
            Assert.That(Find("PauseHostNote").gameObject.activeSelf, Is.False);
            Find("PauseLobbyButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(ui.IsModalOpen, Is.True, "Ending the round for everyone asks first.");
            Find("ConfirmDangerButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(actions.Calls, Does.Contain("lobby"));

            ui.PresentLobby(new LobbyUiState(false, "ABCDE12345", new[] { new LobbyMemberUiState("m0", "Branko", true), new LobbyMemberUiState("m1", "Luna", true) },
                true, false, null, false, string.Empty));
            ui.ShowGameplay(false);
            ui.ShowPause();
            yield return null;
            Assert.That(Find("PauseLobbyButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False, "A guest cannot end the round.");
            Assert.That(Find("PauseHostNote").gameObject.activeInHierarchy, Is.True, "... and is told why.");
            Assert.That(Find("PauseLobbyButton/LockIcon").gameObject.activeSelf, Is.True, "Locked: a padlock on the right.");
            Assert.That(CanvasRect((RectTransform)Find("PauseLobbyButton/LockIcon")).width, Is.EqualTo(20f).Within(0.5f));
            var locked = ContentAlpha(0.45f, new Color(0.949f, 0.965f, 1f), new Color(0.118f, 0.2f, 0.345f));
            Assert.That(Find("PauseLobbyButton/Label").GetComponent<TextMeshProUGUI>().color.a, Is.EqualTo(locked).Within(0.02f), "Label reads at 45 %.");
            Assert.That(Find("PauseLobbyButton/IconPlate/Icon").GetComponent<AlfaUiIcon>().color.a, Is.EqualTo(locked).Within(0.02f), "Icon reads at 45 %.");
            Assert.That(SurfaceColor(Find("PauseLobbyButton"), "DisabledFrameColor"), Is.EqualTo(new Color(0.231f, 0.369f, 0.612f, 0.4f)).Using(ColorComparer),
                "Border #3B5E9C at 40 %.");
            Assert.That(Find("PauseVoicePanel").gameObject.activeSelf, Is.False, "No voice session: no empty voice panel.");
            AssertInside(Find("PauseLeaveButton"), "PauseLeaveButton");

            // Training never shows the voice panel, even with a voice state in a room.
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "PARTIDA", "V", string.Empty, null));
            ui.ShowGameplay(true);
            ui.ShowPause();
            yield return null;
            Assert.That(Find("PauseLobbyButton").gameObject.activeInHierarchy, Is.False, "Training has no room to return to.");
            Assert.That(Label("PauseLeaveButton/Label"), Is.EqualTo("SALIR DEL ENTRENAMIENTO"));
            Assert.That(Find("PauseVoicePanel").gameObject.activeSelf, Is.False, "No CHAT DE VOZ panel in training.");
        }

        [UnityTest]
        public IEnumerator PauseVoiceListShowsThreeWholeRowsAt720()
        {
            var peers = Enumerable.Range(1, 12).Select(i => new VoiceParticipantUiState("m" + i, "Jugador " + i, true, i == 3, i == 1)).ToArray();
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "RONDA · VOZ DE PROXIMIDAD", "V", string.Empty, peers));
            ui.ShowGameplay(false);
            ui.ShowPause();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
            {
                yield return UseCanvas(size.x, size.y);
                Canvas.ForceUpdateCanvases();
                var viewport = Find("PauseVoiceScroll").GetComponent<UnityEngine.UI.ScrollRect>().viewport;
                var view = CanvasRect(viewport);
                var third = CanvasRect((RectTransform)Find("VoicePeer_2"));
                Assert.That(third.yMin, Is.GreaterThanOrEqualTo(view.yMin - 0.5f), "The third row is whole at " + size);
                AssertInside(Find("PauseVoicePanel"), "PauseVoicePanel at " + size);
            }
            Assert.That(Find("PauseVoicePanel/Header/Title").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("CHAT DE VOZ (12)"));
        }

        [UnityTest]
        public IEnumerator HumanHudKeepsOneBannerAndItsChips()
        {
            ui.PresentHud(Human(selected: 0, charge: .7f, bitten: true, interaction: "E · Recoger objeto · G · soltar equipado"));
            yield return UseCanvas(1920, 1080);
            ui.PresentHud(Human(selected: 0, charge: .7f, bitten: true, interaction: "E · Recoger objeto · G · soltar equipado"));
            Canvas.ForceUpdateCanvases();
            Assert.That(Label("ActorState"), Does.Not.Contain("CARGA PANTUFLA"), "No slipper charge with the flyswatter in hand.");
            var vignette = Find("BittenVignette").GetComponent<UnityEngine.UI.Image>();
            Assert.That(vignette.color.a, Is.EqualTo(0.3f).Within(0.01f), "#E0393E at 30 % on the edge.");
            var edge = vignette.sprite.texture;
            Assert.That(AlphaAt(edge, 0.5f, 0.5f), Is.LessThan(0.01f), "Clear in the centre.");
            Assert.That(AlphaAt(edge, 0.5f + 0.35f * 0.5f * 0.95f, 0.5f), Is.LessThan(0.02f), "0 % up to 35 % of the radius.");
            Assert.That(AlphaAt(edge, 0.995f, 0.5f), Is.GreaterThan(0.95f), "Full strength at the screen edge.");
            Assert.That(SurfaceColor(Find("InteractionPrompt"), "FrameColor"), Is.EqualTo(new Color(0.231f, 0.369f, 0.612f, 1f)).Using(ColorComparer),
                "The E chip has the panel border; red is only for ¡TE ESTÁN PICANDO!");
            var reticle = CanvasRect((RectTransform)Find("Reticle"));
            var prompt = CanvasRect((RectTransform)Find("InteractionPrompt"));
            Assert.That(prompt.xMin, Is.GreaterThan(reticle.xMax), "The interaction chip is right of the crosshair.");
            Assert.That(Mathf.Abs(prompt.center.y - reticle.center.y), Is.LessThan(2f));
            Assert.That(Label("PromptKey0/Key"), Is.EqualTo("E"));
            Assert.That(Label("PromptKey1/Key"), Is.EqualTo("G"));
            Assert.That(Label("Interaction1"), Is.EqualTo("Soltar equipado"));
            var chip = CanvasRect((RectTransform)Find("BittenChip"));
            Assert.That(chip.yMax, Is.LessThan(reticle.yMin), "The bitten chip is under the crosshair.");
            Assert.That(prompt.height, Is.EqualTo(CanvasRect((RectTransform)Find("ActorStatePanel")).height).Within(1f), "Chip and banner share a height.");
            Assert.That(Find("ActorState").GetComponent<TextMeshProUGUI>().fontSize, Is.EqualTo(Find("Interaction").GetComponent<TextMeshProUGUI>().fontSize), "... and their type.");

            ui.PresentHud(Human(selected: 1, charge: .7f, bitten: false, interaction: string.Empty));
            Canvas.ForceUpdateCanvases();
            Assert.That(Label("ActorState"), Does.Contain("CARGA PANTUFLA"), "The charge banner while the slipper is in hand.");
            Assert.That(Find("BittenChip").gameObject.activeSelf, Is.False);
            var banners = new[] { "ActorStatePanel", "SwapOfferChip" }.Select(name => Find(name)).Count(item => item != null && item.gameObject.activeInHierarchy);
            Assert.That(banners, Is.EqualTo(1), "One banner over the inventory.");
            var banner = CanvasRect((RectTransform)Find("ActorStatePanel"));
            var slots = CanvasRect((RectTransform)Find("EquipmentSlotPlate1"));
            Assert.That(banner.yMin, Is.GreaterThan(slots.yMax), "The banner sits over the slots.");
        }

        [UnityTest]
        public IEnumerator MosquitoLegendIsOnePanelWithAFixedKeyColumn()
        {
            ui.PresentHud(new BloodHudUiState(AlfaRole.Mosquito, 504, 0, 18, interaction: "Mantené E · Picar",
                contextHint: "Interrumpiendo al humano · Mantené E · Soltá E para despegar", actorState: HudActorState.Extracting,
                stateProgress01: .55f, modeId: GameModes.Tasks, tasksCompleted: 3, tasksGoal: 6, livesRemaining: 2, humansActive: 1, humansTotal: 4));
            yield return UseCanvas(1920, 1080);
            Canvas.ForceUpdateCanvases();
            var panel = CanvasRect((RectTransform)Find("ContextHintPanel"));
            var header = CanvasRect((RectTransform)Find("LegendHeader"));
            Assert.That(panel.width, Is.EqualTo(340f).Within(1f));
            Assert.That(header.yMax, Is.LessThanOrEqualTo(panel.yMax) , "The situation is the panel's own header.");
            Assert.That(Find("LegendHeader").GetComponent<UnityEngine.UI.Image>(), Is.Null, "No box of its own inside the panel.");
            var labels = Find("ControlsLegend").Cast<Transform>().Select(row => CanvasRect((RectTransform)row.Find("Label")).xMin).ToList();
            Assert.That(labels.Max() - labels.Min(), Is.LessThan(1f), "Every action label starts at the same x (ESPACIO + CTRL included).");
            foreach (Transform row in Find("ControlsLegend"))
                Assert.That(CanvasRect((RectTransform)row.Find("Keys")).width, Is.EqualTo(150f).Within(1f));
            Assert.That(Find("InteractionPrompt").gameObject.activeSelf, Is.False, "No second 'Mantené E · Picar' pill.");
            Assert.That(Label("LegendStatus"), Is.EqualTo("Interrumpiendo al humano\nSoltá E para despegar"), "The header breaks at the dots, no dangling '·'.");
            Assert.That(Find("RoleBadge").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(new Color(0.082f, 0.149f, 0.29f, 1f)).Using(ColorComparer),
                "The role badge is opaque #15264A.");
            Assert.That(Find("RoleLabel").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(new Color(0.949f, 0.965f, 1f, 1f)).Using(ColorComparer));
        }

        [UnityTest]
        public IEnumerator SettingsGeneralMatchesTheSketchWithOneControlColumn()
        {
            var saved = new AlfaSettingsDraft { MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .85f, VoiceVolume = .8f, PushToTalkBinding = "<Keyboard>/v", HumanSensitivity = 1f, MosquitoSensitivity = 1f };
            ui.PresentSettings(new SettingsUiState(saved, saved, new[] { "1920 × 1080" }, new[] { "PC" }, true, true, supportsReducedMenuMotion: true,
                voiceDevices: new[] { "Micrófono (USB)" }));
            ui.PresentVoice(new VoiceUiState(true, false, false, true, "SALA", "V", string.Empty, null));
            ui.OpenSettings(AlfaUiScreen.MainMenu);
            yield return UseCanvas(1920, 1080);
            var starts = new List<float>();
            foreach (var page in new[] { "General", "Audio", "Video", "Controls", "Accessibility" })
            {
                Find("SettingsTab" + page).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                foreach (var row in ui.GetComponentsInChildren<UnityEngine.UI.HorizontalLayoutGroup>(false).Where(group => group.name.EndsWith("Row") && group.transform.parent.name.EndsWith("Panel")))
                {
                    var control = row.transform.Cast<Transform>().FirstOrDefault(child => child.name != "Label" && child.name != "RowSeparator");
                    if (control != null) starts.Add(CanvasRect((RectTransform)control).xMin);
                    Assert.That(row.transform.Find("RowSeparator"), Is.Not.Null, row.name + " has its separator line.");
                }
            }
            Assert.That(starts.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(starts.Max() - starts.Min(), Is.LessThan(1.5f), "Every control starts in the same column (the microphone used to start 100 units earlier).");
            Find("SettingsTabControls").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            var keyLabels = Find("HumanKeys").Cast<Transform>().Where(line => line.name.StartsWith("Key_")).Select(line => CanvasRect((RectTransform)line.Find("Label")).xMin).ToList();
            Assert.That(keyLabels.Max() - keyLabels.Min(), Is.LessThan(1f), "Fixed 84-unit key column.");
            Find("SettingsTabAccessibility").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(Find("StatusLegend").gameObject.activeInHierarchy, Is.True, "ACCESIBILIDAD also explains the status icons.");

            // The content panel is its rows + 24 on every tab and the card follows it with its top fixed.
            var tops = new List<float>();
            foreach (var page in new[] { "General", "Audio", "Video", "Controls", "Accessibility" })
            {
                Find("SettingsTab" + page).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var panel = (RectTransform)Find(page + "Panel");
                var rows = UnityEngine.UI.LayoutUtility.GetPreferredHeight(panel);
                // Never shorter than the tab column with VOLVER (5 x 64 + 4 x 10 + 20 + 66, minus the 96-unit footer).
                Assert.That(CanvasRect((RectTransform)Find("Pages")).height, Is.EqualTo(Mathf.Max(rows + 24f, 350f)).Within(1.5f),
                    page + ": the content panel is its rows + 24 (no empty band).");
                tops.Add(CanvasRect((RectTransform)Find("SettingsCard")).yMax);
            }
            Assert.That(tops.Max() - tops.Min(), Is.LessThan(1f), "The card's top stays put between tabs.");
            Find("SettingsTabAudio").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(Label("MusicVolumeSliderRow/Label"), Is.EqualTo("Volumen música"), "Same labels in GENERAL and AUDIO.");
            Assert.That(Label("EffectsVolumeSliderRow/Label"), Is.EqualTo("Volumen efectos"));
            Find("SettingsTabGeneral").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(Label("LanguageValue"), Is.EqualTo("Español"));
            Assert.That(Find("LanguagePrevious").GetComponent<UnityEngine.UI.Button>().interactable, Is.False, "A locked ‹ Español › selector, not a mute field.");
            Assert.That(Find("LanguageNext").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);

            // Inactive APLICAR: the full green, content at 55 %; while a key is awaited GENERAL stays highlighted.
            Find("SettingsTabGeneral").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var apply = Find("SettingsApplyButton");
            Assert.That(apply.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(SurfaceColor(apply, "GradientTop"), Is.EqualTo(new Color(0.275f, 0.769f, 0.373f, 1f)).Using(ColorComparer));
            Assert.That(apply.Find("Label").GetComponent<TextMeshProUGUI>().color.a, Is.EqualTo(0.55f).Within(0.02f));
            Find("PushToTalkRebindButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var tab = Find("SettingsTabGeneral");
            Assert.That(tab.GetComponent<UnityEngine.UI.Button>().IsInteractable(), Is.False, "The card is locked while a key is awaited.");
            Assert.That(SurfaceField(tab, "SelectedKeepsLook"), Is.EqualTo(true));
            Assert.That(tab.Find("Label").GetComponent<TextMeshProUGUI>().color.a, Is.GreaterThan(0.95f), "GENERAL stays highlighted.");
        }

        [UnityTest]
        public IEnumerator CustomizationPalettesAndPreviewFollowTheSketch()
        {
            ui.PresentCustomization(State(9, 6, 8));
            ui.ShowCustomization();
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) })
            {
                yield return UseCanvas(size.x, size.y);
                Invoke("SelectBasicCategory", "pajama");
                Canvas.ForceUpdateCanvases();
                var grid = Find("PajamaPalette").GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Assert.That(grid.constraintCount, Is.EqualTo(3), "3 x 3 like the sketch.");
                Assert.That(grid.cellSize.x, Is.EqualTo(76f).Within(0.1f));
                Assert.That(Find("PajamaPalette").Cast<Transform>().Count(), Is.EqualTo(9));
                Assert.That(CanvasRect((RectTransform)Find("Color_blue/Swatch")).width, Is.EqualTo(64f).Within(0.5f), "64-unit swatches.");
                var content = CanvasRect((RectTransform)Find("OptionsPanel/Content"));
                var front = CanvasRect((RectTransform)Find("PreviewFrontButton"));
                var side = CanvasRect((RectTransform)Find("PreviewSideButton"));
                Assert.That(front.xMin, Is.GreaterThanOrEqualTo(content.xMin - 0.5f), "VISTA PREVIA starts at the content margin at " + size);
                Assert.That(side.xMax, Is.LessThanOrEqualTo(content.xMax + 0.5f), "... and never reaches the panel edge at " + size);
                Assert.That(front.height, Is.GreaterThan(200f), "About 165 x 265 at " + size);
                var random = CanvasRect((RectTransform)Find("CustomizationRandomButton"));
                var row = CanvasRect((RectTransform)Find("PreviewRow"));
                Assert.That(row.yMin - random.yMax, Is.EqualTo(12f).Within(1f), "ALEATORIO 12 units under VISTA PREVIA at " + size);
                var options = CanvasRect((RectTransform)Find("PajamaGroup"));
                var caption = CanvasRect((RectTransform)Find("PreviewCaption"));
                Assert.That(options.yMin - caption.yMax, Is.EqualTo(16f).Within(1.5f), "VISTA PREVIA 16 units under the palette at " + size);
                Assert.That(Find("RoleSummary").gameObject.activeInHierarchy, Is.True, "The rail is filled by the role card at " + size);
                var rail = CanvasRect((RectTransform)Find("BasicCategories"));
                Assert.That(rail.height, Is.EqualTo(3 * 74f + 2 * 10f).Within(1f), "The categories take their buttons' height.");
            }
            Invoke("SelectBasicCategory", "skin");
            Assert.That(Find("SkinPalette").Cast<Transform>().Count(), Is.EqualTo(6), "Six skin tones (PER-04).");
            Invoke("SetCustomizationRole", AlfaRole.Mosquito);
            yield return null;
            Assert.That(Find("MosquitoPalette").Cast<Transform>().Count(), Is.EqualTo(8), "Eight body colours.");
            // CUERPO (UI-06 6): COLOR DE CUERPO (8, 4 x 2), ESTILO DE ALAS and OJOS in one column; the rail has only
            // CUERPO and COLORES, both live.
            foreach (var name in new[] { "CustomizationCategory_mosquito-body", "CustomizationCategory_mosquito" })
            {
                Assert.That(Find(name).gameObject.activeInHierarchy, Is.True, name);
                Assert.That(Find(name).GetComponent<UnityEngine.UI.Button>().interactable, Is.True, name + " is live");
            }
            foreach (var gone in new[] { "CustomizationCategory_mosquito-wings", "CustomizationCategory_mosquito-eyes", "CustomizationCategory_mosquito-proboscis" })
                Assert.That(Find(gone), Is.Null, gone + " is not a locked rail entry any more.");
            Invoke("SelectBasicCategory", "mosquito-body");
            yield return null;
            Assert.That(Find("MosquitoBodyPalette").Cast<Transform>().Count(), Is.EqualTo(8));
            Assert.That(Find("MosquitoBodyPalette").GetComponent<UnityEngine.UI.GridLayoutGroup>().constraintCount, Is.EqualTo(4));
            foreach (var section in new[] { "Wings", "Eyes" })
            {
                var cards = Find(section + "Cards").Cast<Transform>().ToList();
                Assert.That(cards.Count, Is.EqualTo(3), section + ": three cards.");
                Assert.That(cards.Count(card => card.Find("Lock") != null), Is.EqualTo(2), section + ": the two this build lacks are locked.");
                foreach (var card in cards.Where(card => card.Find("Lock") != null))
                {
                    Assert.That(card.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                    Assert.That(card.Find("Label").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Próximamente"));
                    Assert.That(card.Find("Label").GetComponent<TextMeshProUGUI>().color.a,
                        Is.EqualTo(ContentAlpha(0.55f, new Color(0.949f, 0.965f, 1f), new Color(0.118f, 0.2f, 0.345f))).Within(0.02f), "Locked cards read at 55 %.");
                }
                Assert.That(cards[0].Find("SelectionMark").gameObject.activeSelf, Is.True, section + ": what the mosquito wears is marked.");
            }
            var column = CanvasRect((RectTransform)Find("MosquitoBodyGroup"));
            var content2 = CanvasRect((RectTransform)Find("OptionsPanel/Content"));
            Assert.That(column.xMax, Is.LessThanOrEqualTo(content2.xMax + 0.5f), "The column fits the panel.");
            Assert.That(Label("HumanFields/PajamaGroup/DefaultClothes"), Does.Contain("gorro rojo"), "The clothes colour never tints the nightcap.");
            Assert.That(Label("CustomizationHumanButton/Subtitle"), Is.EqualTo("PIEL, ROPA Y ACCESORIOS"));
        }

        [UnityTest]
        public IEnumerator ModularCardsLeaveTheScrollbarChannel()
        {
            var snapshot = MosquitoSnapshot();
            var selection = snapshot.DefaultSelection();
            ui.PresentCustomization(new CustomizationUiState(snapshot, selection, selection, AlfaRole.Mosquito));
            ui.ShowCustomization();
            yield return UseCanvas(1920, 1080);
            Invoke("SelectModularCategory", "mosquito.wings");
            Canvas.ForceUpdateCanvases();
            var scroll = Find("OptionsScroll").GetComponent<UnityEngine.UI.ScrollRect>();
            var viewport = CanvasRect(scroll.viewport);
            var root = CanvasRect((RectTransform)scroll.transform);
            Assert.That(root.xMax - viewport.xMax, Is.GreaterThanOrEqualTo(6f + 12f - 0.5f), "A fixed 12-unit channel for the scrollbar.");
            var last = CanvasRect((RectTransform)Find("ModularOption_3_4"));
            Assert.That(last.xMax, Is.LessThanOrEqualTo(viewport.xMax + 0.5f), "CORTAS is never cut by the scrollbar.");
            Assert.That(CanvasRect((RectTransform)Find("PreviewFrontButton")).height, Is.GreaterThan(200f), "The modular VISTA PREVIA keeps the human size.");
            Assert.That(Find("OptionsFade"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TrainingPortraitsNeverCutTheHead()
        {
            ui.ShowTraining();
            yield return UseCanvas(1920, 1080);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var art = typeof(AlfaUiController).Assembly.GetType("LetMeSleep.UI.AlfaUiArt");
            var bounds = art.GetMethod("OpaqueBounds", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var card in new[] { "HumanCard", "MosquitoCard" })
            {
                var frame = CanvasRect((RectTransform)Find(card + "/PortraitFrame"));
                var image = Find(card + "/PortraitFrame/PortraitMask/Portrait").GetComponent<UnityEngine.UI.RawImage>();
                Assert.That(image.texture, Is.Not.Null, card + " shows the painted figure.");
                var rect = CanvasRect(image.rectTransform);
                var opaque = (Rect)bounds.Invoke(null, new object[] { image.texture });
                var headTop = rect.yMin + opaque.yMax * rect.height;
                Assert.That(headTop, Is.LessThanOrEqualTo(frame.yMax + 0.5f), card + ": the head is inside the frame.");
                Assert.That(headTop, Is.GreaterThan(frame.yMax - frame.height * 0.2f), card + ": the figure starts near the top.");
                var left = rect.xMin + opaque.xMin * rect.width;
                var right = rect.xMin + opaque.xMax * rect.width;
                Assert.That(left, Is.GreaterThanOrEqualTo(frame.xMin - 0.5f));
                Assert.That(right, Is.LessThanOrEqualTo(frame.xMax + 0.5f));
            }
        }

        private static BloodHudUiState Human(int selected, float charge, bool bitten, string interaction) => new BloodHudUiState(AlfaRole.Human, 147, 5.4f, 18,
            interaction: interaction, contextHint: bitten ? "¡Te están picando! Buscá al mosquito y golpeá hacia él." : "Clic · golpear hacia la mira",
            actorState: bitten ? HudActorState.Bitten : HudActorState.Normal, modeId: GameModes.Blood, mosquitoesAlive: 2,
            equipment: new EquipmentHudUiState(new[]
            {
                new EquipmentSlotUiState("MATAMOSCAS", "REUTILIZABLE", AlfaUiIconKind.Flyswatter),
                new EquipmentSlotUiState("PANTUFLA", "REUTILIZABLE", AlfaUiIconKind.Slipper),
                new EquipmentSlotUiState("AEROSOL", "2,9 s", AlfaUiIconKind.Aerosol)
            }, selected, .82f, charge), mosquitoesTotal: 2);

        private static CustomizationUiState State(int clothes, int skins, int mosquitoes)
        {
            NamedColorOption[] Options(string prefix, int count) => Enumerable.Range(0, count)
                .Select(i => new NamedColorOption(i == 0 ? (prefix == "p" ? "blue" : prefix == "s" ? "warm" : "red") : prefix + i, prefix.ToUpperInvariant() + i,
                    Color.HSVToRGB(i / (float)count, .6f, .8f))).ToArray();
            return new CustomizationUiState(Options("s", skins), Options("p", clothes), Options("m", mosquitoes),
                new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red"));
        }

        [UnityTest]
        public IEnumerator DirectorPassDetails()
        {
            // JUGAR ONLINE: the default map (CASA CON PATIO) has a real picture about 400 x 140 with its name on a strip.
            ui.ShowCreateRoom("Branko");
            yield return UseCanvas(1920, 1080);
            Canvas.ForceUpdateCanvases();
            var picture = Find("OnlineMapThumbnail").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(picture.texture, Is.Not.Null, "A picture of the map, not the generic icon.");
            Assert.That(Find("OnlineMapPlaceholderIcon").gameObject.activeSelf, Is.False);
            var area = CanvasRect(picture.rectTransform);
            Assert.That(area.width, Is.EqualTo(400f).Within(20f));
            Assert.That(area.height, Is.EqualTo(140f).Within(8f));
            var strip = Find("OnlineMapNameStrip").GetComponent<UnityEngine.UI.Image>();
            Assert.That((Vector4)strip.color, Is.EqualTo(new Vector4(0.055f, 0.102f, 0.188f, OverlayAlpha(0.7f))).Using(Vector4Comparer), "#0E1A30 strip reading at 70 %.");
            Assert.That(CanvasRect((RectTransform)Find("OnlineMapName")).yMin, Is.GreaterThanOrEqualTo(area.yMin - 0.5f), "The name sits on the picture.");

            // Modal cards: #0E1A30 reading at 80 % behind them.
            ui.PresentOnline(new OnlineUiState(OnlineOperationPhase.Searching, canCancel: true));
            yield return null;
            Assert.That(Find("OnlineOverlay").GetComponent<UnityEngine.UI.Image>().color.a, Is.GreaterThanOrEqualTo(0.9f), "Nothing cut behind the modal reads.");
            ui.PresentOnline(new OnlineUiState());

            // HUD: the painted face on the 84-unit #FFC93C disc, cropped to the head (with the whole nightcap).
            ui.PresentHud(Human(selected: 0, charge: 0f, bitten: false, interaction: string.Empty));
            yield return null;
            var face = Find("RoleFace").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(face.texture, Is.Not.Null, "The v0.3 character's face.");
            Assert.That(face.uvRect.width, Is.LessThan(0.6f), "A head crop, not the whole figure.");
            Assert.That(face.uvRect.y, Is.GreaterThan(0.3f), "From the top of the figure.");
            var disc = Find("RolePortrait").GetComponent<UnityEngine.UI.Image>();
            Assert.That(CanvasRect(disc.rectTransform).width, Is.EqualTo(84f).Within(0.5f));
            Assert.That(disc.color, Is.EqualTo(new Color(1f, 0.788f, 0.235f, 1f)).Using(ColorComparer));
            Assert.That(Find("RoleBadge").GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(1f).Within(0.001f), "The role badge is opaque.");

            // Waiting room nametags: #15264A chips reading at 80 % with a status dot, never stacked.
            var cameraObject = new GameObject("NametagTestCamera", typeof(Camera));
            var worldCamera = cameraObject.GetComponent<Camera>();
            worldCamera.transform.position = new Vector3(0f, 1.5f, -6f);
            var avatars = new[] { new GameObject("AvatarA"), new GameObject("AvatarB") };
            avatars[0].transform.position = new Vector3(0f, 0f, 0f);
            avatars[1].transform.position = new Vector3(0.05f, 0f, 1f); // right behind the first one
            typeof(AlfaUiController).GetField("lobbyPresenceOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(ui, new Presence(new Dictionary<string, Transform> { { "m0", avatars[0].transform }, { "m1", avatars[1].transform } }, worldCamera));
            ui.PresentLobby(new LobbyUiState(true, "ABCDE12345", new[] { new LobbyMemberUiState("m0", "Branko", true), new LobbyMemberUiState("m1", "Luna", false) },
                true, false, null, true, string.Empty));
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var tags = ui.GetComponentsInChildren<RectTransform>(false).Where(item => item.name.StartsWith("Nametag")).ToList();
            try
            {
                Assert.That(tags.Count, Is.EqualTo(2));
                var chip = tags[0].GetComponent<UnityEngine.UI.Image>();
                Assert.That((Vector4)chip.color, Is.EqualTo(new Vector4(0.082f, 0.149f, 0.29f, OverlayAlpha(0.8f))).Using(Vector4Comparer), "#15264A chip reading at 80 %.");
                Assert.That(CanvasRect(tags[0]).Overlaps(CanvasRect(tags[1])), Is.False, "Tags of players behind one another do not stack.");
                var dots = tags.Select(tag => tag.Find("Dot").GetComponent<UnityEngine.UI.Image>().color).ToList();
                Assert.That(dots, Does.Contain(new Color(0.341f, 0.824f, 0.42f, 1f)).Using(ColorComparer), "Green dot: ready.");
                Assert.That(dots, Does.Contain(new Color(1f, 0.42f, 0.369f, 1f)).Using(ColorComparer), "Red dot: not ready.");
            }
            finally
            {
                foreach (var avatar in avatars) Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class Presence : ILobbyPresenceSource
        {
            private readonly Dictionary<string, Transform> avatars;
            private readonly Camera camera;
            public Presence(Dictionary<string, Transform> avatars, Camera camera) { this.avatars = avatars; this.camera = camera; }
            public bool TryGetLobbyAvatar(string memberId, out Transform avatar, out Camera view)
            {
                view = camera;
                return avatars.TryGetValue(memberId, out avatar);
            }
        }

        private static readonly IEqualityComparer<Vector4> Vector4Comparer = new Vector4Equality();

        private sealed class Vector4Equality : IEqualityComparer<Vector4>
        {
            public bool Equals(Vector4 a, Vector4 b) => (a - b).magnitude < 0.02f;
            public int GetHashCode(Vector4 value) => 0;
        }

        private static readonly System.Type Theme = typeof(AlfaUiController).Assembly.GetType("LetMeSleep.UI.AlfaUiTheme");

        private static float ContentAlpha(float coverage, Color content, Color panel) =>
            (float)Theme.GetMethod("ContentAlpha", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { coverage, content, panel });

        private static float OverlayAlpha(float coverage) =>
            (float)Theme.GetMethod("OverlayAlpha", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { coverage });

        private static float AlphaAt(Texture2D texture, float u, float v) => texture.GetPixelBilinear(u, v).a;

        private static CustomizationCatalogSnapshot MosquitoSnapshot()
        {
            var slots = new List<CustomizationSlotRecord>
            {
                new CustomizationSlotRecord { Role = CustomizationRole.Human, SlotId = "human.base", Label = "PERSONAJE", WireSlotId = 1, Required = true, IsBaseSlot = true, DefaultOptionId = "base-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.base", Label = "CUERPO", WireSlotId = 2, Required = true, IsBaseSlot = true, DefaultOptionId = "body-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.wings", Label = "ALAS", WireSlotId = 3, DefaultOptionId = "wings-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.eyes", Label = "OJOS", WireSlotId = 4, DefaultOptionId = "eyes-a" },
                new CustomizationSlotRecord { Role = CustomizationRole.Mosquito, SlotId = "mosquito.proboscis", Label = "PROBÓSCIDE", WireSlotId = 5, DefaultOptionId = "probe-a" }
            };
            var options = new List<CustomizationOptionRecord>
            {
                Visual(CustomizationRole.Human, "human.base", "base-a", "BASE", 1),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "body-a", "ESTÁNDAR", 1), Visual(CustomizationRole.Mosquito, "mosquito.base", "body-b", "ROBUSTO", 2),
                Visual(CustomizationRole.Mosquito, "mosquito.wings", "wings-a", "FACETADAS", 1), Visual(CustomizationRole.Mosquito, "mosquito.wings", "wings-b", "REDONDAS", 2),
                Visual(CustomizationRole.Mosquito, "mosquito.wings", "wings-c", "LARGAS Y FINAS", 3), Visual(CustomizationRole.Mosquito, "mosquito.wings", "wings-d", "CORTAS", 4),
                Visual(CustomizationRole.Mosquito, "mosquito.eyes", "eyes-a", "GRANDES", 1), Visual(CustomizationRole.Mosquito, "mosquito.eyes", "eyes-b", "ENOJADOS", 2),
                Visual(CustomizationRole.Mosquito, "mosquito.proboscis", "probe-a", "ESTÁNDAR", 1), Visual(CustomizationRole.Mosquito, "mosquito.proboscis", "probe-b", "CURVA", 2)
            };
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.ui.stage3", 1, slots, options, out var snapshot, out var errors), Is.True, string.Join("\n", errors));
            return snapshot;
        }

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slotId, string optionId, string label, ushort wire) =>
            new CustomizationOptionRecord { Role = role, SlotId = slotId, OptionId = optionId, WireOptionId = wire,
                Kind = CustomizationOptionKind.SkinnedPart, Label = label, AssetId = "synthetic-" + optionId, HasRuntimeAsset = true };

        private static readonly IEqualityComparer<Color> ColorComparer = new ColorEquality();

        private sealed class ColorEquality : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
            public int GetHashCode(Color color) => 0;
        }

        private static object SurfaceField(Transform item, string field)
        {
            var surface = item.GetComponent("AlfaUiSurface");
            return surface.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
        }

        private static Color SurfaceColor(Transform item, string field) => (Color)SurfaceField(item, field);

        private IEnumerator UseCanvas(int width, int height)
        {
            if (camera == null)
            {
                camera = new GameObject("UiStageThreeCamera", typeof(Camera)).GetComponent<Camera>();
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
        }

        private void AssertInside(Transform item, string label)
        {
            Assert.That(item, Is.Not.Null, label);
            var rect = CanvasRect((RectTransform)item);
            var root = ((RectTransform)ui.transform).rect;
            Assert.That(rect.xMin >= root.xMin - 1f && rect.yMin >= root.yMin - 1f && rect.xMax <= root.xMax + 1f && rect.yMax <= root.yMax + 1f, Is.True,
                label + " leaves the canvas: " + rect + " in " + root);
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

        private void Invoke(string method, params object[] arguments)
        {
            var info = typeof(AlfaUiController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(item => item.Name == method && item.GetParameters().Length == arguments.Length);
            info.Invoke(ui, arguments);
        }
    }
}
