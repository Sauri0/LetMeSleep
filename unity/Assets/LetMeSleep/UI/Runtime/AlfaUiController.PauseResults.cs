using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Pause (UI-06 screen 8) and results (screen 9).
    /// Pause: the scene is dimmed with #0E1A30 at 56 % so the menu stands out; left column "PARTIDA EN PAUSA" (ink
    /// contour and shadow) with CONTINUAR (blue), AJUSTES and SALIR; under it a compact voice panel whose list shows
    /// whole rows only, fades at the bottom edge when it scrolls, and is pooled (ui-presentation-audio-5 and -6).
    /// There is no "VOLVER A LA SALA" mid-round: only the host can end a round, so it is not offered.
    /// Results: #0E1A30 at 58 % over the scene, a 116-unit "¡HUMANOS GANAN!" / "¡MOSQUITOS GANAN!" title with ink
    /// contour and shadow, the two teams full body and unframed in a fixed order (human left, mosquito right) with
    /// the winner celebrating at 1.15 (trophy and #FFC93C frame on its chip) and the loser behind at 0.8, labelled
    /// player counts, and the actions that exist: JUGAR DE NUEVO (training) or VOLVER A LA SALA (host), plus leaving.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const float PauseVoiceRowHeight = 52f;
        private const float PauseVoiceRowSpacing = 8f;
        private const int PauseVoiceVisibleRows = 3;
        private const float ResultsTitleHeight = 150f;
        private const float ResultsChipHeight = 88f;
        private const float ResultsWinnerScale = 1.15f;
        private const float ResultsLoserScale = 0.8f;
        private RectTransform resultsCard;
        private float resultsLaidOutHeight = -1f;
        private MatchOutcome resultsOutcome = MatchOutcome.Interrupted;

        private UnityEngine.UI.Button pauseLeaveButton;
        private TextMeshProUGUI pauseLeaveLabel;
        private UnityEngine.UI.Button pauseVoiceMuteButton;
        private TextMeshProUGUI pauseVoiceMuteLabel;
        private TextMeshProUGUI pauseVoiceStatus;
        private GameObject pauseVoicePanel;
        private RectTransform pauseVoicePeers;
        private UnityEngine.UI.ScrollRect pauseVoiceScroll;
        private GameObject pauseVoiceFade;
        private readonly List<string> pauseVoiceIds = new List<string>();
        private readonly Dictionary<string, UnityEngine.UI.Button> pauseVoiceRows = new Dictionary<string, UnityEngine.UI.Button>();
        private bool pauseVoiceDirty;
        private GameObject pauseVoiceEmpty;

        private TextMeshProUGUI resultsTitle;
        private TextMeshProUGUI resultsStats;
        private TextMeshProUGUI resultsPrimaryLabel;
        private TextMeshProUGUI resultsLeaveLabel;
        private UnityEngine.UI.Button resultsPrimary;
        private UnityEngine.UI.Button resultsLeave;
        private ResultsUiState resultsState;
        private UnityEngine.UI.Image resultsGlow;
        private readonly Dictionary<AlfaRole, ResultsFigure> resultsFigures = new Dictionary<AlfaRole, ResultsFigure>();
        private readonly Dictionary<string, Texture2D> resultsFigureTextures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<AlfaRole, ResultsChip> resultsChips = new Dictionary<AlfaRole, ResultsChip>();

        private sealed class ResultsChip
        {
            public RectTransform Root;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Unit;
            public GameObject Crown;
        }

        private sealed class ResultsFigure
        {
            public RectTransform Root;
            public UnityEngine.UI.RawImage Image;
            public AlfaUiIcon Fallback;
        }

        private void BuildPause()
        {
            var view = factory.View("PauseView", transform, false);
            var backdrop = view.GetComponent<UnityEngine.UI.Image>();
            // #0E1A30 over the scene; 0.7 in the UI's linear blending reads as the sketch's 50-60 % dim.
            backdrop.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.7f);
            backdrop.raycastTarget = true;
            screens[AlfaUiScreen.Pause] = view;
            var wash = AlfaUiFactory.Node("NightWash", view.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            wash.sprite = AlfaUiFactory.LinearFadeSprite();
            wash.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.5f);
            wash.raycastTarget = false;
            Anchor(wash.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var column = AlfaUiFactory.Node("PauseColumn", view.transform).GetComponent<RectTransform>();
            AlfaUiFactory.Place(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(64f, 40f), new Vector2(64f + 540f, -84f));
            const float cardHeight = 96f + 3f * 80f + 2f * 14f + 40f;
            var card = factory.Panel(column, "PauseCard", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.97f));
            Anchor(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, cardHeight));
            var content = factory.Vertical(card, "Content", 14f);
            AlfaUiFactory.Fill(content, 26f, 26f, 20f, 26f);
            var title = factory.Title(content, "Title", "PARTIDA EN PAUSA", 46f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            factory.OutlineTitle(title, 46f);
            title.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 60f;
            PauseButton(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, AlfaButtonStyle.Primary, AlfaUiIconKind.Play);
            PauseButton(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause), AlfaButtonStyle.Secondary, AlfaUiIconKind.Gear);
            pauseLeaveButton = PauseButton(content, "PauseLeaveButton", "SALIR DE LA SALA", LeaveGameplayContext, AlfaButtonStyle.Danger, AlfaUiIconKind.Exit);
            pauseLeaveLabel = pauseLeaveButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            // Compact voice panel: always shorter than the pause card; the list shows whole rows only.
            var listHeight = PauseVoiceVisibleRows * PauseVoiceRowHeight + (PauseVoiceVisibleRows - 1) * PauseVoiceRowSpacing + 12f;
            var voiceHeight = 150f + listHeight + 18f;
            var voice = factory.Panel(column, "PauseVoicePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.95f));
            pauseVoicePanel = voice.gameObject;
            Anchor(voice, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(cardHeight + 18f)), new Vector2(0f, voiceHeight));
            var header = factory.Horizontal(voice, "Header", 10f, TextAnchor.MiddleLeft);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-40f, 34f));
            var mic = factory.Icon(header, "VoiceIcon", AlfaUiIconKind.Microphone, AlfaUiTheme.Sky400);
            var micLayout = mic.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            micLayout.minWidth = micLayout.preferredWidth = 26f;
            micLayout.minHeight = micLayout.preferredHeight = 26f;
            var voiceTitle = factory.Caption(header, "Title", "CHAT DE VOZ");
            voiceTitle.color = AlfaUiTheme.Sheet100;
            voiceTitle.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            pauseVoiceStatus = factory.Text(header, "PauseVoiceStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineRight);
            pauseVoiceStatus.textWrappingMode = TextWrappingModes.NoWrap;
            pauseVoiceMuteButton = factory.Button(voice, "PauseVoiceMuteButton", "SILENCIAR MI MICRÓFONO", () =>
                (actions as IVoiceActions)?.SetLocalVoiceMuted(!voiceState.LocalMuted), AlfaButtonStyle.Secondary, 56f, AlfaUiIconKind.Microphone);
            Anchor((RectTransform)pauseVoiceMuteButton.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(-40f, 56f));
            pauseVoiceMuteLabel = pauseVoiceMuteButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var hint = factory.Text(voice, "PeersHint", "Tocá a un jugador para silenciarlo.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-40f, 28f));
            var scroll = factory.ScrollView(voice, "PauseVoiceScroll", out pauseVoicePeers, listHeight, true);
            Anchor(scroll, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(-40f, listHeight));
            pauseVoicePeers.name = "PauseVoicePeers";
            pauseVoicePeers.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = PauseVoiceRowSpacing;
            pauseVoiceScroll = scroll.GetComponent<UnityEngine.UI.ScrollRect>();
            // The viewport keeps its inset (whole rows only) and leaves room for the bar instead of being resized.
            pauseVoiceScroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            pauseVoiceScroll.viewport.offsetMax = new Vector2(-18f, -6f);
            pauseVoiceScroll.scrollSensitivity = PauseVoiceRowHeight + PauseVoiceRowSpacing;
            pauseVoiceScroll.onValueChanged.AddListener(_ => UpdatePauseVoiceFade());
            // 24-unit fade at the bottom edge while more rows are below (no half-cut row reads as the last one).
            var fade = AlfaUiFactory.Node("PeersFade", scroll, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            fade.sprite = AlfaUiFactory.VerticalFadeSprite();
            fade.color = AlfaUiTheme.Night700;
            fade.raycastTarget = false;
            Anchor(fade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 24f));
            pauseVoiceFade = fade.gameObject;
            pauseVoiceFade.SetActive(false);
            pauseVoiceEmpty = factory.Text(scroll, "PeersEmpty", "Todavía no hay otros jugadores en el chat de voz.", AlfaUiTheme.NoteSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center).gameObject;
            AlfaUiFactory.Fill((RectTransform)pauseVoiceEmpty.transform, 20f, 20f, 20f, 20f);
            pauseVoicePanel.SetActive(false);
        }

        private UnityEngine.UI.Button PauseButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction callback,
            AlfaButtonStyle style, AlfaUiIconKind icon)
        {
            var button = factory.Button(parent, name, label, callback, style, 80f, icon);
            factory.StrongLabel(button, AlfaUiTheme.MenuLabelSize);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            AlfaUiFactory.Fill(text.rectTransform, 84f, 16f, 4f, 2f);
            return button;
        }

        public void ShowPause()
        {
            actions.SetGameplayInputBlocked(true);
            pauseLeaveLabel.text = gameplayIsTraining ? "SALIR DEL ENTRENAMIENTO" : "SALIR DE LA SALA";
            SetScreen(AlfaUiScreen.Pause, "PauseContinueButton");
            if (pauseVoiceDirty) RebuildPauseVoicePeers();
            UpdatePauseVoiceFade();
        }

        private void ResumeFromPause()
        {
            actions.ResumeGame();
            ShowGameplay();
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

        /// <summary>
        /// Voice rows are pooled per member: labels update in place; the list is rebuilt only when the set of
        /// members changes and only while the pause is on screen (otherwise it is marked dirty for ShowPause).
        /// </summary>
        private void RebuildPauseVoicePeers()
        {
            if (pauseVoicePeers == null || voiceState == null) return;
            pauseVoicePanel.SetActive(voiceState.InRoom);
            var ids = voiceState.Participants.Select(item => item.MemberId).ToList();
            if (!ids.SequenceEqual(pauseVoiceIds))
            {
                if (screen != AlfaUiScreen.Pause && pauseVoiceIds.Count > 0)
                {
                    pauseVoiceDirty = true;
                    return;
                }
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var focusedId = pauseVoiceRows.FirstOrDefault(pair => pair.Value != null && pair.Value.gameObject == selected).Key;
                AlfaUiFactory.Clear(pauseVoicePeers);
                pauseVoiceRows.Clear();
                pauseVoiceIds.Clear();
                pauseVoiceIds.AddRange(ids);
                foreach (var id in ids)
                {
                    var memberId = id;
                    var row = factory.Button(pauseVoicePeers, "VoicePeer_" + pauseVoiceRows.Count, string.Empty, () => ToggleVoicePeer(memberId),
                        AlfaButtonStyle.Secondary, PauseVoiceRowHeight, AlfaUiIconKind.Audio);
                    var rowLayout = row.GetComponent<UnityEngine.UI.LayoutElement>();
                    rowLayout.minHeight = rowLayout.preferredHeight = PauseVoiceRowHeight;
                    var label = row.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.richText = true;
                    pauseVoiceRows[id] = row;
                }
                if (focusedId != null && screen == AlfaUiScreen.Pause)
                    Focus(pauseVoiceRows.TryGetValue(focusedId, out var kept) ? kept.gameObject : pauseVoiceMuteButton.gameObject);
            }
            pauseVoiceDirty = false;
            pauseVoiceEmpty.SetActive(voiceState.Participants.Count == 0);
            foreach (var participant in voiceState.Participants)
            {
                if (!pauseVoiceRows.TryGetValue(participant.MemberId, out var row) || row == null) continue;
                var label = row.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                label.text = Escape(participant.DisplayName) + (participant.Muted ? "  <color=#FF6B5E>· SILENCIADO</color>" :
                    participant.Speaking ? "  <color=#57D26B>· HABLANDO</color>" : string.Empty);
                var icon = row.transform.Find("IconPlate/Icon")?.GetComponent<AlfaUiIcon>();
                if (icon != null)
                {
                    icon.Kind = participant.Muted ? AlfaUiIconKind.Close : participant.Speaking ? AlfaUiIconKind.Microphone : AlfaUiIconKind.Audio;
                    icon.color = participant.Muted ? AlfaUiTheme.StatusWarn : participant.Speaking ? AlfaUiTheme.StatusOk : AlfaUiTheme.Sheet100;
                }
                row.interactable = actions is IVoiceActions;
            }
            UpdatePauseVoiceFade();
        }

        private void UpdatePauseVoiceFade() => UpdateScrollFade(pauseVoiceScroll, pauseVoiceFade);

        /// <summary>24-unit fade at a list's bottom edge while more content lies below it.</summary>
        private static void UpdateScrollFade(UnityEngine.UI.ScrollRect scroll, GameObject fade)
        {
            if (fade == null || scroll == null || scroll.content == null || scroll.viewport == null) return;
            var overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            var remaining = overflow - scroll.content.anchoredPosition.y;
            var show = overflow > 1f && remaining > 2f;
            if (fade.activeSelf != show) fade.SetActive(show);
        }

        private void ToggleVoicePeer(string memberId)
        {
            var participant = voiceState?.Participants.FirstOrDefault(item => item.MemberId == memberId);
            if (participant == null) return;
            (actions as IVoiceActions)?.SetPeerVoiceMuted(memberId, !participant.Muted);
        }

        private void BuildResults()
        {
            var view = factory.View("ResultsView", transform, false);
            // #0E1A30 over the scene. The UI blends in linear space, so 0.7 here reads as the sketch's ~58 % dim.
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.7f);
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Results] = view;
            // No card: the teams stand over the dimmed scene (UI-06 9). ResultsCard is the layout frame only, as tall
            // as the canvas allows (21:9 is short) so the figures take whatever height is left.
            var card = AlfaUiFactory.Node("ResultsCard", view.transform).GetComponent<RectTransform>();
            Anchor(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, -40f));
            resultsCard = card;

            resultsGlow = AlfaUiFactory.Node("WinnerGlow", card, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            resultsGlow.sprite = AlfaUiFactory.RadialGlowSprite();
            resultsGlow.raycastTarget = false;

            // Loser first so the winner is drawn in front when the two overlap.
            resultsFigures[AlfaRole.Human] = ResultsFigureNode(card, "HumanFigure", AlfaUiIconKind.Human);
            resultsFigures[AlfaRole.Mosquito] = ResultsFigureNode(card, "MosquitoFigure", AlfaUiIconKind.Mosquito);

            var banner = AlfaUiFactory.Node("ResultsBanner", card).GetComponent<RectTransform>();
            Anchor(banner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, ResultsTitleHeight));
            resultsTitle = factory.Title(banner, "Title", "RONDA INTERRUMPIDA", 116f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
            factory.OutlineTitle(resultsTitle, 116f);
            resultsTitle.textWrappingMode = TextWrappingModes.NoWrap;
            resultsTitle.enableAutoSizing = true;
            resultsTitle.fontSizeMin = 64f;
            resultsTitle.fontSizeMax = 116f;
            resultsTitle.characterSpacing = 3f;
            AlfaUiFactory.Fill(resultsTitle.rectTransform, 20f, 20f, 4f, 4f);

            resultsChips[AlfaRole.Human] = ResultsTeamChip(card, "ResultsHumansChip", "HUMANOS", AlfaUiIconKind.Online);
            resultsChips[AlfaRole.Mosquito] = ResultsTeamChip(card, "ResultsMosquitoesChip", "MOSQUITOS", AlfaUiIconKind.Mosquito);

            resultsStats = factory.Text(card, "Stats", string.Empty, 24f, AlfaUiTheme.Moon200, TextAlignmentOptions.Center, true);
            factory.MakeDisplay(resultsStats, 24f);
            resultsStats.characterSpacing = 2f;
            resultsStats.overflowMode = TextOverflowModes.Overflow;
            resultsStats.alignment = TextAlignmentOptions.Top;
            Anchor(resultsStats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(-80f, 92f));

            var buttons = factory.Horizontal(card, "ResultsActions", 24f, TextAnchor.MiddleCenter);
            Anchor(buttons, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-80f, 84f));
            resultsLeave = factory.Button(buttons, "ResultsLeaveButton", "SALIR DE LA SALA", ResultsLeaveAction, AlfaButtonStyle.Secondary, 84f, AlfaUiIconKind.Exit);
            resultsPrimary = factory.Button(buttons, "ResultsPrimaryButton", "VOLVER A LA SALA", ResultsPrimaryAction, AlfaButtonStyle.Primary, 84f, AlfaUiIconKind.Enter);
            foreach (var button in new[] { resultsLeave, resultsPrimary })
            {
                factory.StrongLabel(button, 30f);
                var layout = button.GetComponent<UnityEngine.UI.LayoutElement>();
                layout.preferredWidth = layout.minWidth = 460f;
                layout.flexibleWidth = 0f;
            }
            resultsPrimaryLabel = resultsPrimary.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            resultsLeaveLabel = resultsLeave.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        private ResultsFigure ResultsFigureNode(Transform card, string name, AlfaUiIconKind fallbackKind)
        {
            var root = AlfaUiFactory.Node(name, card).GetComponent<RectTransform>();
            var image = AlfaUiFactory.Node(name + "Image", root, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            AlfaUiFactory.Fill(image.rectTransform);
            var fallback = factory.Icon(root, "Fallback", fallbackKind, AlfaUiTheme.WithAlpha(AlfaUiTheme.Sheet100, 0.85f));
            Anchor(fallback.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(260f, 260f));
            return new ResultsFigure { Root = root, Image = image, Fallback = fallback };
        }

        private ResultsChip ResultsTeamChip(Transform parent, string name, string label, AlfaUiIconKind icon)
        {
            var chip = factory.Panel(parent, name, Color.white, 400f, 88f);
            var symbol = factory.Icon(chip, "Icon", icon, AlfaUiTheme.Sheet100);
            Anchor(symbol.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(52f, 52f));
            var name2 = factory.Title(chip, "Team", label, 32f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            name2.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(name2.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(-246f, 0f));
            // Labelled count: the number with what it counts under it ("3" / "JUGADORES").
            var count = factory.Title(chip, "Count", string.Empty, 40f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.overflowMode = TextOverflowModes.Overflow;
            Anchor(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -2f), new Vector2(136f, 52f));
            var unit = factory.Caption(chip, "CountUnit", "JUGADORES");
            unit.alignment = TextAlignmentOptions.Center;
            unit.overflowMode = TextOverflowModes.Overflow;
            Anchor(unit.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 6f), new Vector2(136f, 28f));
            var crown = factory.Icon(chip, "Crown", AlfaUiIconKind.Trophy, AlfaUiTheme.Lamp400);
            Anchor(crown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(46f, 46f));
            return new ResultsChip { Root = chip, Count = count, Unit = unit, Crown = crown.gameObject };
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

            var humans = state.Outcome == MatchOutcome.Humans;
            var mosquitoes = state.Outcome == MatchOutcome.Mosquitoes;
            resultsTitle.text = humans ? "¡HUMANOS GANAN!" : mosquitoes ? "¡MOSQUITOS GANAN!" : "RONDA INTERRUMPIDA";
            resultsTitle.color = humans || mosquitoes ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Sheet100;
            PresentResultsTeam(AlfaRole.Human, state.HumansCount, humans, mosquitoes);
            PresentResultsTeam(AlfaRole.Mosquito, state.MosquitoesCount, mosquitoes, humans);
            var team = humans ? AlfaUiTheme.TeamHuman : AlfaUiTheme.TeamMosquito;
            resultsGlow.gameObject.SetActive(humans || mosquitoes);
            resultsGlow.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.1f), 0.38f);
            resultsOutcome = state.Outcome;
            LayoutResults();

            // The score line only when it carries information: an interrupted round has no score or time.
            var lines = new List<string>();
            if (state.Outcome != MatchOutcome.Interrupted)
                lines.Add("MODO " + AlfaModeText.Name(state.ModeId) + "   ·   " + ResultsScoreLine(state) + "   ·   TIEMPO " + FormatClock(state.ElapsedSeconds));
            if (!string.IsNullOrWhiteSpace(state.Reason)) lines.Add("<color=#F2F6FF>" + Escape(state.Reason) + "</color>");
            if (!state.IsTraining && !state.IsOwner) lines.Add("<color=#FFC93C>ESPERANDO AL ANFITRIÓN…</color>");
            if (state.IsTraining && !HasSelectedTrainingMap) lines.Add(NoTrainingMaps);
            resultsStats.text = AlfaUiTheme.Digits(string.Join("\n", lines));

            resultsPrimary.gameObject.SetActive(state.IsTraining || state.IsOwner);
            resultsPrimary.interactable = !state.IsTraining || TrainingModeAvailable;
            resultsLeave.interactable = true;
            AlfaUiFactory.ApplyStyle(resultsPrimary, state.IsTraining ? AlfaButtonStyle.Success : AlfaButtonStyle.Primary);
            SetButtonIcon(resultsPrimary, state.IsTraining ? AlfaUiIconKind.Refresh : AlfaUiIconKind.Enter);
            SetButtonIcon(resultsLeave, state.IsTraining ? AlfaUiIconKind.House : AlfaUiIconKind.Exit);
            resultsPrimaryLabel.text = state.IsTraining ? "JUGAR DE NUEVO" : "VOLVER A LA SALA";
            resultsLeaveLabel.text = state.IsTraining ? "VOLVER AL MENÚ" : "SALIR DE LA SALA";
            SetScreen(AlfaUiScreen.Results, resultsPrimary.gameObject.activeSelf ? "ResultsPrimaryButton" : "ResultsLeaveButton");
        }

        /// <summary>Whole units, uppercase, display face: "SANGRE 20 / 20", "TAREAS 4 / 6", "MOSQUITOS VIVOS 2".</summary>
        private static string ResultsScoreLine(ResultsUiState state)
        {
            if (state.ModeId == GameModes.Tasks) return "TAREAS " + state.TasksCompleted + " / " + state.TasksGoal;
            if (state.ModeId == GameModes.Survival) return "MOSQUITOS VIVOS " + state.MosquitoesAlive;
            var goal = Mathf.Max(0, Mathf.RoundToInt(state.BloodTarget));
            return "SANGRE " + Mathf.Clamp(Mathf.FloorToInt(state.BloodCurrent + 0.0001f), 0, goal) + " / " + goal;
        }

        private static void SetButtonIcon(UnityEngine.UI.Button button, AlfaUiIconKind kind)
        {
            var icon = button.transform.Find("IconPlate/Icon")?.GetComponent<AlfaUiIcon>();
            if (icon != null) icon.Kind = kind;
        }

        /// <summary>
        /// One team: its figure (human always on the left, mosquito on the right) and its chip under it. The winner
        /// celebrates at 1.15 with a trophy and a 3-unit #FFC93C frame; the loser stands behind at 0.8, dimmed.
        /// </summary>
        /// <summary>
        /// Vertical plan of the results frame (from its top): title, figures standing on a floor line, the team
        /// chips under it, then the score lines and the buttons at the bottom. Returns the floor (distance from the
        /// top) and the unscaled figure box height that lets the 1.15 winner clear the title.
        /// </summary>
        private float ResultsFloor(out float boxHeight)
        {
            var height = resultsCard != null && resultsCard.rect.height > 1f ? resultsCard.rect.height : 1040f;
            var chipTop = height - (100f + 92f + 10f) - ResultsChipHeight * ResultsWinnerScale;
            var floor = chipTop - 10f;
            boxHeight = Mathf.Clamp((floor - ResultsTitleHeight - 6f) / ResultsWinnerScale, 220f, 520f);
            return floor;
        }

        private void PresentResultsTeam(AlfaRole role, int count, bool winner, bool loser)
        {
            var human = role == AlfaRole.Human;
            var figure = resultsFigures[role];
            var texture = ResultsFigureTexture(role, winner);
            figure.Image.texture = texture;
            figure.Image.enabled = texture != null;
            figure.Fallback.gameObject.SetActive(texture == null);
            figure.Image.color = loser ? new Color(0.55f, 0.6f, 0.72f, 0.92f) : Color.white;
            figure.Fallback.color = AlfaUiTheme.WithAlpha(loser ? AlfaUiTheme.Moon200 : AlfaUiTheme.Sheet100, loser ? 0.6f : 0.9f);
            if (winner) figure.Root.SetAsLastSibling();

            var chip = resultsChips[role];
            var style = winner ? human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger : AlfaButtonStyle.Secondary;
            AlfaUiTheme.StyleColors(style, out var top, out var bottom, out _, out _);
            AlfaUiFactory.SetSurface(chip.Root, top, bottom, winner ? AlfaUiTheme.Lamp400 : AlfaUiTheme.WithAlpha(human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn, 0.7f),
                winner ? AlfaUiTheme.WithAlpha(AlfaUiTheme.Lamp400, 0.35f) : AlfaUiTheme.WithAlpha(Color.black, 0.45f));
            AlfaUiFactory.MarkSelectedFrame(chip.Root, winner);
            if (winner) AlfaUiFactory.SetFrame(chip.Root, AlfaUiTheme.Lamp400);
            var icon = chip.Root.Find("Icon")?.GetComponent<AlfaUiIcon>();
            if (icon != null) icon.color = winner ? AlfaUiTheme.Sheet100 : human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn;
            chip.Count.text = count >= 0 ? count.ToString() : string.Empty;
            chip.Unit.text = count >= 0 ? count == 1 ? "JUGADOR" : "JUGADORES" : string.Empty;
            chip.Count.color = loser ? AlfaUiTheme.Moon200 : AlfaUiTheme.Sheet100;
            chip.Unit.color = winner ? AlfaUiTheme.WithAlpha(AlfaUiTheme.Sheet100, 0.85f) : AlfaUiTheme.LabelInk;
            chip.Crown.SetActive(winner);
            if (winner) chip.Root.SetAsLastSibling();
        }

        /// <summary>
        /// Places figures, chips and the winner glow from the frame's current height. Runs on present and again
        /// whenever the canvas size changes (the size can still be stale when results are presented).
        /// </summary>
        private void LayoutResults()
        {
            if (resultsCard == null) return;
            resultsLaidOutHeight = resultsCard.rect.height;
            var floor = ResultsFloor(out var boxHeight);
            foreach (var role in new[] { AlfaRole.Human, AlfaRole.Mosquito })
            {
                var human = role == AlfaRole.Human;
                var winner = human ? resultsOutcome == MatchOutcome.Humans : resultsOutcome == MatchOutcome.Mosquitoes;
                var loser = human ? resultsOutcome == MatchOutcome.Mosquitoes : resultsOutcome == MatchOutcome.Humans;
                var x = human ? -300f : 300f;
                var figure = resultsFigures[role];
                var texture = figure.Image.texture;
                var box = new Vector2(Mathf.Min(560f, boxHeight * 1.15f), boxHeight);
                if (texture != null && texture.height > 0)
                {
                    var aspect = (float)texture.width / texture.height;
                    box = aspect >= box.x / box.y ? new Vector2(box.x, box.x / aspect) : new Vector2(box.y * aspect, box.y);
                }
                // Loser a little further back: raised towards the horizon.
                Anchor(figure.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                    new Vector2(x * (loser ? 0.92f : 1f), -floor + (loser ? 26f : 0f)), box);
                figure.Root.localScale = Vector3.one * (winner ? ResultsWinnerScale : loser ? ResultsLoserScale : 1f);
                var chip = resultsChips[role];
                Anchor(chip.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -floor - 10f), new Vector2(400f, ResultsChipHeight));
                chip.Root.localScale = Vector3.one * (winner ? ResultsWinnerScale : 1f);
            }
            Anchor(resultsGlow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(resultsOutcome == MatchOutcome.Humans ? -300f : 300f, -floor + boxHeight * 0.55f), new Vector2(760f, boxHeight * 1.45f));
        }

        /// <summary>Per-frame layout upkeep of the stage-2 screens (called from LateUpdate).</summary>
        private void UpdateStageTwoLayout()
        {
            if (screen == AlfaUiScreen.Results && resultsCard != null && Mathf.Abs(resultsCard.rect.height - resultsLaidOutHeight) > 0.5f) LayoutResults();
            if (screen == AlfaUiScreen.Pause) UpdatePauseVoiceFade();
            if (screen == AlfaUiScreen.Customization) UpdateOptionsFade();
        }

        /// <summary>
        /// Full-body figure for a team: painted art in Resources/AlfaUiPortraits/&lt;Role&gt;Winner or &lt;Role&gt;
        /// when it exists, otherwise the in-game model rendered once on transparency (arms up when it won).
        /// </summary>
        private Texture ResultsFigureTexture(AlfaRole role, bool celebrate)
        {
            var key = role + (celebrate ? ":win" : ":stand");
            if (resultsFigureTextures.TryGetValue(key, out var cached)) return cached;
            var painted = LoadRoleArt(role, celebrate ? "Winner" : string.Empty);
            if (painted != null) return painted.texture;
            Texture2D rendered = null;
            if (portraitSetup != null && portraitSetup.IsUsable && screen != AlfaUiScreen.Customization)
                rendered = role == AlfaRole.Human
                    ? AlfaRolePortrait.RenderFigure(portraitSetup, role, 420, 700, celebrate)
                    : AlfaRolePortrait.RenderFigure(portraitSetup, role, 720, 560, celebrate);
            resultsFigureTextures[key] = rendered;
            return rendered;
        }

        private static Sprite LoadRoleArt(AlfaRole role, string suffix)
        {
            var path = "AlfaUiPortraits/" + (role == AlfaRole.Human ? "Human" : "Mosquito") + suffix;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
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
                resultsLeaveLabel.text = "SALIENDO…";
                LeaveActiveTraining();
            }
            else ConfirmLeave();
        }

        private void DestroyResultsFigures()
        {
            foreach (var texture in resultsFigureTextures.Values)
                if (texture != null) Destroy(texture);
            resultsFigureTextures.Clear();
        }
    }
}
