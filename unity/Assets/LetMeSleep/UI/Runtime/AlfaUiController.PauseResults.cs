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
    /// Pause: the scene is dimmed with #0E1A30 so the menu stands out; left column "PARTIDA EN PAUSA" (ink contour
    /// and shadow) with the sketch's menu: CONTINUAR (blue), AJUSTES, VOLVER A LA SALA (online only: the host ends
    /// the round for everyone after a confirmation; a guest sees it disabled with the reason) and SALIR DE LA PARTIDA
    /// (red; SALIR DEL ENTRENAMIENTO in training). Under it, at 70 % of the menu width so it never competes with it,
    /// a compact voice panel ("CHAT DE VOZ (12)") whose list shows three whole rows (3 rows + 2 gaps + padding), fades
    /// at the bottom edge when it scrolls and is pooled (ui-presentation-audio-5 and -6). When the column is too short
    /// (21:9) the voice panel moves beside the menu.
    /// Results: #0E1A30 over the scene; a comic "¡HUMANOS GANAN!" / "¡MOSQUITOS GANAN!" title (the logo's face,
    /// #FFC93C, #0B1426 contour, tilted 3 degrees up to the right as the sketch); the winning team as a group of
    /// min(players, 3) figures at 1.15, centred with 170 units between them, standing on one baseline with an
    /// elliptical contact shadow each (220 x 40, black at 35 %) and a 25 % radial team light behind; the team chips in
    /// front of their legs (winner centred, loser to its side with its own figure behind it at 0.8); the score line on
    /// a #0E1A30 pill; and the actions that exist: the green JUGAR DE NUEVO (training) or VOLVER A LA SALA (host) on
    /// the right and leaving, navy, on the left.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const float PauseVoiceRowHeight = 52f;
        private const float PauseVoiceRowSpacing = 8f;
        private const int PauseVoiceVisibleRows = 3;
        private const float PauseVoiceListTopPadding = 2f;
        private const float PauseVoiceListBottomPadding = 4f;
        private const float PauseCardWidth = 540f;
        private const float PauseButtonHeight = 80f;
        private const float PauseButtonSpacing = 14f;
        private const float PauseVoiceWidthRatio = 0.7f;
        private const float ResultsTitleTop = 36f;
        private const float ResultsTitleHeight = 156f;
        private const float ResultsChipHeight = 88f;
        private const float ResultsChipWidth = 400f;
        private const float ResultsWinnerScale = 1.15f;
        private const float ResultsLoserScale = 0.8f;
        private const float ResultsButtonsBottom = 36f;
        private const float ResultsButtonsHeight = 84f;
        private const float ResultsStatsBottom = 144f;
        private const float ResultsChipCentre = 275f;   // from the bottom: chips span y 760-850 at 1080p
        private const float ResultsBaseline = 260f;     // feet at y 820 at 1080p
        private const float ResultsGroupSpacing = 170f;
        private const float ResultsMosquitoSpacing = 210f;
        private const float ResultsLoserLift = 26f;
        private static readonly Vector2 ResultsShadowSize = new Vector2(220f, 40f);
        private RectTransform resultsCard;
        private float resultsLaidOutHeight = -1f;
        private MatchOutcome resultsOutcome = MatchOutcome.Interrupted;

        private RectTransform pauseColumn;
        private RectTransform pauseCard;
        private UnityEngine.UI.Button pauseLobbyButton;
        private TextMeshProUGUI pauseHostNote;
        private UnityEngine.UI.Button pauseLeaveButton;
        private TextMeshProUGUI pauseLeaveLabel;
        private UnityEngine.UI.Button pauseVoiceMuteButton;
        private TextMeshProUGUI pauseVoiceMuteLabel;
        private TextMeshProUGUI pauseVoiceStatus;
        private TextMeshProUGUI pauseVoiceTitle;
        private GameObject pauseVoicePanel;
        private RectTransform pauseVoiceRect;
        private float pauseVoiceHeight;
        private RectTransform pauseVoicePeers;
        private UnityEngine.UI.ScrollRect pauseVoiceScroll;
        private GameObject pauseVoiceFade;
        private readonly List<string> pauseVoiceIds = new List<string>();
        private readonly Dictionary<string, UnityEngine.UI.Button> pauseVoiceRows = new Dictionary<string, UnityEngine.UI.Button>();
        private bool pauseVoiceDirty;
        private GameObject pauseVoiceEmpty;
        private float pauseLaidOutHeight = -1f;

        private TextMeshProUGUI resultsTitle;
        private TextMeshProUGUI resultsStats;
        private RectTransform resultsStatsPill;
        private TextMeshProUGUI resultsPrimaryLabel;
        private TextMeshProUGUI resultsLeaveLabel;
        private UnityEngine.UI.Button resultsPrimary;
        private UnityEngine.UI.Button resultsLeave;
        private ResultsUiState resultsState;
        private UnityEngine.UI.Image resultsGlow;
        private readonly Dictionary<AlfaRole, ResultsTeam> resultsTeams = new Dictionary<AlfaRole, ResultsTeam>();
        private readonly Dictionary<string, Texture2D> resultsFigureTextures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<AlfaRole, ResultsChip> resultsChips = new Dictionary<AlfaRole, ResultsChip>();

        private sealed class ResultsChip
        {
            public RectTransform Root;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Unit;
            public GameObject Crown;
        }

        /// <summary>One team on the results stage: a group root (its x is the team's place) with up to three figures.</summary>
        private sealed class ResultsTeam
        {
            public RectTransform Root;
            public readonly UnityEngine.UI.RawImage[] Figures = new UnityEngine.UI.RawImage[3];
            public readonly UnityEngine.UI.Image[] Shadows = new UnityEngine.UI.Image[3];
            public AlfaUiIcon Fallback;
            public int Count = 1;
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

            pauseColumn = AlfaUiFactory.Node("PauseColumn", view.transform).GetComponent<RectTransform>();
            AlfaUiFactory.Place(pauseColumn, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(64f, 40f), new Vector2(64f + PauseCardWidth, -84f));
            pauseCard = factory.Panel(pauseColumn, "PauseCard", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.97f));
            Anchor(pauseCard, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, PauseCardHeight(4, false)));
            var content = factory.Vertical(pauseCard, "Content", PauseButtonSpacing);
            AlfaUiFactory.Fill(content, 26f, 26f, 20f, 26f);
            var title = factory.Title(content, "Title", "PARTIDA EN PAUSA", 46f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            factory.OutlineTitle(title, 46f);
            title.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 60f;
            PauseButton(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, AlfaButtonStyle.Primary, AlfaUiIconKind.Play);
            PauseButton(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause), AlfaButtonStyle.Secondary, AlfaUiIconKind.Gear);
            pauseLobbyButton = PauseButton(content, "PauseLobbyButton", "VOLVER A LA SALA", ReturnToRoomFromPause, AlfaButtonStyle.Secondary, AlfaUiIconKind.Invite);
            pauseHostNote = factory.Text(content, "PauseHostNote", "Solo el anfitrión puede terminar la ronda y volver a la sala.", AlfaUiTheme.NoteSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            pauseHostNote.textWrappingMode = TextWrappingModes.Normal;
            pauseHostNote.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 48f;
            pauseLeaveButton = PauseButton(content, "PauseLeaveButton", "SALIR DE LA PARTIDA", LeaveGameplayContext, AlfaButtonStyle.Danger, AlfaUiIconKind.Exit);
            pauseLeaveLabel = pauseLeaveButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            BuildPauseVoicePanel();
        }

        private static float PauseCardHeight(int buttons, bool hostNote) =>
            96f + buttons * PauseButtonHeight + (buttons - 1) * PauseButtonSpacing + (hostNote ? 48f + PauseButtonSpacing : 0f) + 40f;

        private static float PauseVoiceViewportHeight => PauseVoiceVisibleRows * PauseVoiceRowHeight + (PauseVoiceVisibleRows - 1) * PauseVoiceRowSpacing +
            PauseVoiceListTopPadding + PauseVoiceListBottomPadding;

        /// <summary>
        /// Voice panel at 70 % of the menu width: "CHAT DE VOZ (n)", the scope line, mute and the list. The viewport is
        /// exactly three rows, two gaps and the list padding, so the third row always shows its bottom edge.
        /// </summary>
        private void BuildPauseVoicePanel()
        {
            var listHeight = PauseVoiceViewportHeight + 12f;
            pauseVoiceHeight = 14f + 30f + 2f + 26f + 10f + 52f + 8f + 28f + 8f + listHeight + 14f;
            var voice = factory.Panel(pauseColumn, "PauseVoicePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.95f));
            pauseVoicePanel = voice.gameObject;
            pauseVoiceRect = voice;
            Anchor(voice, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(PauseCardHeight(4, false) + 18f)),
                new Vector2(PauseCardWidth * PauseVoiceWidthRatio, pauseVoiceHeight));
            var header = factory.Horizontal(voice, "Header", 10f, TextAnchor.MiddleLeft);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-36f, 30f));
            var mic = factory.Icon(header, "VoiceIcon", AlfaUiIconKind.Microphone, AlfaUiTheme.Sky400);
            var micLayout = mic.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            micLayout.minWidth = micLayout.preferredWidth = 26f;
            micLayout.minHeight = micLayout.preferredHeight = 26f;
            pauseVoiceTitle = factory.Caption(header, "Title", "CHAT DE VOZ");
            pauseVoiceTitle.color = AlfaUiTheme.Sheet100;
            pauseVoiceStatus = factory.Text(voice, "PauseVoiceStatus", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            pauseVoiceStatus.textWrappingMode = TextWrappingModes.NoWrap;
            pauseVoiceStatus.overflowMode = TextOverflowModes.Overflow;
            Anchor(pauseVoiceStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-36f, 30f));
            pauseVoiceMuteButton = factory.Button(voice, "PauseVoiceMuteButton", "SILENCIARME", () =>
                (actions as IVoiceActions)?.SetLocalVoiceMuted(!voiceState.LocalMuted), AlfaButtonStyle.Secondary, 52f, AlfaUiIconKind.Microphone);
            Anchor((RectTransform)pauseVoiceMuteButton.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(-36f, 52f));
            pauseVoiceMuteLabel = pauseVoiceMuteButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var hint = factory.Text(voice, "PeersHint", "Tocá un nombre para silenciarlo.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(-36f, 28f));
            var scroll = factory.ScrollView(voice, "PauseVoiceScroll", out pauseVoicePeers, listHeight, true);
            Anchor(scroll, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(-36f, listHeight));
            pauseVoicePeers.name = "PauseVoicePeers";
            var peersLayout = pauseVoicePeers.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            peersLayout.spacing = PauseVoiceRowSpacing;
            peersLayout.padding = new RectOffset(0, 0, (int)PauseVoiceListTopPadding, (int)PauseVoiceListBottomPadding);
            pauseVoiceScroll = scroll.GetComponent<UnityEngine.UI.ScrollRect>();
            // The viewport keeps its inset (whole rows only) and a fixed 12-unit channel for the bar.
            pauseVoiceScroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            pauseVoiceScroll.viewport.offsetMax = new Vector2(-18f, -6f);
            pauseVoiceScroll.scrollSensitivity = PauseVoiceRowHeight + PauseVoiceRowSpacing;
            pauseVoiceScroll.onValueChanged.AddListener(_ => UpdatePauseVoiceFade());
            // 24-unit fade at the bottom edge while more rows are below (no half-cut row reads as the last one).
            var fade = AlfaUiFactory.Node("PeersFade", scroll, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            fade.sprite = AlfaUiFactory.VerticalFadeSprite();
            fade.color = AlfaUiTheme.Night700;
            fade.raycastTarget = false;
            Anchor(fade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-6f, 4f), new Vector2(-20f, 24f));
            pauseVoiceFade = fade.gameObject;
            pauseVoiceFade.SetActive(false);
            pauseVoiceEmpty = factory.Text(scroll, "PeersEmpty", "Todavía no hay otros jugadores en el chat de voz.", AlfaUiTheme.NoteSize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center).gameObject;
            AlfaUiFactory.Fill((RectTransform)pauseVoiceEmpty.transform, 16f, 16f, 16f, 16f);
            pauseVoicePanel.SetActive(false);
        }

        private UnityEngine.UI.Button PauseButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction callback,
            AlfaButtonStyle style, AlfaUiIconKind icon)
        {
            var button = factory.Button(parent, name, label, callback, style, PauseButtonHeight, icon);
            factory.StrongLabel(button, AlfaUiTheme.MenuLabelSize);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            AlfaUiFactory.Fill(text.rectTransform, 84f, 16f, 4f, 2f);
            return button;
        }

        public void ShowPause()
        {
            actions.SetGameplayInputBlocked(true);
            // Online rounds get the sketch's menu (VOLVER A LA SALA + SALIR DE LA PARTIDA); training keeps its exit.
            var online = !gameplayIsTraining;
            var host = online && lobbyState != null && lobbyState.IsOwner;
            pauseLobbyButton.gameObject.SetActive(online);
            pauseLobbyButton.interactable = host;
            pauseHostNote.gameObject.SetActive(online && !host);
            pauseLeaveLabel.text = online ? "SALIR DE LA PARTIDA" : "SALIR DEL ENTRENAMIENTO";
            SetScreen(AlfaUiScreen.Pause, "PauseContinueButton");
            LayoutPause();
            if (pauseVoiceDirty) RebuildPauseVoicePeers();
            UpdatePauseVoiceFade();
        }

        /// <summary>
        /// Card height from its visible buttons; the voice panel goes under the card, or beside it when the column is
        /// too short for both (21:9).
        /// </summary>
        private void LayoutPause()
        {
            if (pauseCard == null) return;
            var buttons = pauseLobbyButton.gameObject.activeSelf ? 4 : 3;
            var cardHeight = PauseCardHeight(buttons, pauseHostNote.gameObject.activeSelf);
            pauseCard.sizeDelta = new Vector2(0f, cardHeight);
            var columnHeight = pauseColumn.rect.height;
            pauseLaidOutHeight = columnHeight;
            var below = columnHeight <= 1f || cardHeight + 18f + pauseVoiceHeight <= columnHeight;
            pauseVoiceRect.anchoredPosition = below ? new Vector2(0f, -(cardHeight + 18f)) : new Vector2(PauseCardWidth + 18f, 0f);
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
            else ShowConfirm("¿SALIR DE LA PARTIDA?", "Vas a dejar la sala y volver al menú principal.", "SEGUIR JUGANDO", "SALIR", () => actions.LeaveRoom());
        }

        /// <summary>
        /// VOLVER A LA SALA (UI-06 8): only the host can end the round; after a confirmation everyone returns to the
        /// waiting room (the lobby presentation that follows replaces this screen).
        /// </summary>
        private void ReturnToRoomFromPause()
        {
            if (gameplayIsTraining || lobbyState == null || !lobbyState.IsOwner) return;
            ShowConfirm("¿VOLVER A LA SALA?", "La ronda termina para todos y vuelven a la sala de espera.", "SEGUIR JUGANDO", "VOLVER A LA SALA",
                () => actions.ReturnToLobby());
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
            if (pauseVoiceTitle != null)
                pauseVoiceTitle.text = voiceState.Participants.Count > 0 ? "CHAT DE VOZ (" + voiceState.Participants.Count + ")" : "CHAT DE VOZ";
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
                    label.enableAutoSizing = true;
                    label.fontSizeMin = AlfaUiTheme.MinTextSize;
                    label.fontSizeMax = AlfaUiTheme.ButtonSize;
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
            Anchor(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 0f));
            resultsCard = card;

            resultsGlow = AlfaUiFactory.Node("WinnerGlow", card, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            resultsGlow.sprite = AlfaUiFactory.RadialGlowSprite();
            resultsGlow.raycastTarget = false;

            resultsTeams[AlfaRole.Human] = ResultsTeamNode(card, "HumanFigure", AlfaUiIconKind.Human);
            resultsTeams[AlfaRole.Mosquito] = ResultsTeamNode(card, "MosquitoFigure", AlfaUiIconKind.Mosquito);

            var banner = AlfaUiFactory.Node("ResultsBanner", card).GetComponent<RectTransform>();
            Anchor(banner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -ResultsTitleTop), new Vector2(0f, ResultsTitleHeight));
            // The logo's comic face at 116, #FFC93C with the #0B1426 contour, rising 3 degrees to the right (UI-06).
            resultsTitle = factory.ComicTitle(banner, "Title", "RONDA INTERRUMPIDA", 116f, AlfaUiTheme.Lamp400);
            resultsTitle.textWrappingMode = TextWrappingModes.NoWrap;
            resultsTitle.enableAutoSizing = true;
            resultsTitle.fontSizeMin = 64f;
            resultsTitle.fontSizeMax = 116f;
            AlfaUiFactory.Fill(resultsTitle.rectTransform, 40f, 40f, 4f, 4f);
            resultsTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);

            resultsChips[AlfaRole.Human] = ResultsTeamChip(card, "ResultsHumansChip", "HUMANOS", AlfaUiIconKind.Online);
            resultsChips[AlfaRole.Mosquito] = ResultsTeamChip(card, "ResultsMosquitoesChip", "MOSQUITOS", AlfaUiIconKind.Mosquito);

            // Score line on a #0E1A30 pill at 70 % (the bare #A8B8D8 line did not read over the deck).
            resultsStatsPill = factory.Panel(card, "StatsPill", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.7f), -1f, -1f, AlfaUiTheme.PanelRadius);
            AlfaUiFactory.SetSurface(resultsStatsPill, Color.white, Color.white, AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.45f), Color.clear);
            var pillLayout = resultsStatsPill.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            pillLayout.padding = new RectOffset(26, 26, 8, 8);
            pillLayout.childAlignment = TextAnchor.MiddleCenter;
            pillLayout.childControlWidth = pillLayout.childControlHeight = true;
            pillLayout.childForceExpandWidth = pillLayout.childForceExpandHeight = false;
            var pillFit = resultsStatsPill.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            pillFit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            pillFit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            Anchor(resultsStatsPill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ResultsStatsBottom), new Vector2(600f, 44f));
            resultsStats = factory.Text(resultsStatsPill, "Stats", string.Empty, 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            factory.MakeDisplay(resultsStats, 24f);
            resultsStats.characterSpacing = 2f;
            resultsStats.textWrappingMode = TextWrappingModes.NoWrap;
            resultsStats.overflowMode = TextOverflowModes.Overflow;
            var statsLayout = resultsStats.GetComponent<UnityEngine.UI.LayoutElement>();
            statsLayout.flexibleWidth = 0f;
            statsLayout.minHeight = 28f;

            var buttons = factory.Horizontal(card, "ResultsActions", 24f, TextAnchor.MiddleCenter);
            Anchor(buttons, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ResultsButtonsBottom), new Vector2(-80f, ResultsButtonsHeight));
            // Leaving on the left in navy, the green call to action on the right (UI-06 9).
            resultsLeave = factory.Button(buttons, "ResultsLeaveButton", "SALIR DE LA SALA", ResultsLeaveAction, AlfaButtonStyle.Secondary, ResultsButtonsHeight, AlfaUiIconKind.Exit);
            resultsPrimary = factory.Button(buttons, "ResultsPrimaryButton", "VOLVER A LA SALA", ResultsPrimaryAction, AlfaButtonStyle.Success, ResultsButtonsHeight, AlfaUiIconKind.Enter);
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

        private ResultsTeam ResultsTeamNode(Transform card, string name, AlfaUiIconKind fallbackKind)
        {
            var root = AlfaUiFactory.Node(name, card).GetComponent<RectTransform>();
            var team = new ResultsTeam { Root = root };
            // Shadows first: every figure of the group stands in front of every shadow.
            for (var i = 0; i < 3; i++)
            {
                var shadow = AlfaUiFactory.Node("ContactShadow" + i, root, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                shadow.sprite = AlfaUiFactory.RadialGlowSprite();
                shadow.color = AlfaUiTheme.WithAlpha(Color.black, 0.35f);
                shadow.raycastTarget = false;
                team.Shadows[i] = shadow;
            }
            for (var i = 0; i < 3; i++)
            {
                var image = AlfaUiFactory.Node(i == 0 ? name + "Image" : name + "Image" + i, root, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
                image.raycastTarget = false;
                image.enabled = false;
                team.Figures[i] = image;
            }
            team.Fallback = factory.Icon(root, "Fallback", fallbackKind, AlfaUiTheme.WithAlpha(AlfaUiTheme.Sheet100, 0.85f));
            Anchor(team.Fallback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(240f, 240f));
            return team;
        }

        private ResultsChip ResultsTeamChip(Transform parent, string name, string label, AlfaUiIconKind icon)
        {
            var chip = factory.Panel(parent, name, Color.white, ResultsChipWidth, ResultsChipHeight);
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
            // A soft radial team light at 25 % behind the group (no flat halo).
            resultsGlow.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.15f), 0.25f);
            resultsOutcome = state.Outcome;
            LayoutResults();

            // The score line only when it carries information: an interrupted round has no score or time.
            var lines = new List<string>();
            if (state.Outcome != MatchOutcome.Interrupted)
                lines.Add("MODO " + AlfaModeText.Name(state.ModeId) + "   ·   " + ResultsScoreLine(state) + "   ·   TIEMPO " + FormatClock(state.ElapsedSeconds));
            if (!string.IsNullOrWhiteSpace(state.Reason)) lines.Add(Escape(state.Reason));
            if (!state.IsTraining && !state.IsOwner) lines.Add("<color=#FFC93C>ESPERANDO AL ANFITRIÓN…</color>");
            if (state.IsTraining && !HasSelectedTrainingMap) lines.Add(NoTrainingMaps);
            resultsStats.text = AlfaUiTheme.Digits(string.Join("\n", lines));
            resultsStatsPill.gameObject.SetActive(lines.Count > 0);

            resultsPrimary.gameObject.SetActive(state.IsTraining || state.IsOwner);
            resultsPrimary.interactable = !state.IsTraining || TrainingModeAvailable;
            resultsLeave.interactable = true;
            // JUGAR DE NUEVO and the host's VOLVER A LA SALA are both the green call to action (UI-06 9).
            AlfaUiFactory.ApplyStyle(resultsPrimary, AlfaButtonStyle.Success);
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
        /// One team: its figures (the winner's group celebrating, the loser's single figure dimmed) and its chip.
        /// The winner's chip gets the trophy and the 3-unit #FFC93C frame.
        /// </summary>
        private void PresentResultsTeam(AlfaRole role, int count, bool winner, bool loser)
        {
            var human = role == AlfaRole.Human;
            var team = resultsTeams[role];
            var texture = ResultsFigureTexture(role, winner);
            team.Count = winner ? Mathf.Clamp(count, 1, 3) : 1;
            for (var i = 0; i < team.Figures.Length; i++)
            {
                var figure = team.Figures[i];
                var used = i < team.Count && texture != null;
                figure.texture = texture;
                figure.enabled = used;
                figure.gameObject.SetActive(used);
                figure.color = loser ? new Color(0.55f, 0.6f, 0.72f, 0.94f) : Color.white;
                team.Shadows[i].gameObject.SetActive(i < team.Count);
                team.Shadows[i].color = AlfaUiTheme.WithAlpha(Color.black, loser ? 0.25f : 0.35f);
            }
            team.Fallback.gameObject.SetActive(texture == null);
            team.Fallback.color = AlfaUiTheme.WithAlpha(loser ? AlfaUiTheme.Moon200 : AlfaUiTheme.Sheet100, loser ? 0.6f : 0.9f);
            if (winner) team.Root.SetAsLastSibling();

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
        }

        /// <summary>
        /// Places the teams, chips and light from the frame's current height (re-run whenever the canvas changes).
        /// Everything is measured from the bottom at the 1080p reference: buttons, the score pill, the chips (y 760-850)
        /// and the baseline (feet at y 820), so on a short 21:9 canvas only the figures shrink.
        /// </summary>
        private void LayoutResults()
        {
            if (resultsCard == null) return;
            var height = resultsCard.rect.height > 1f ? resultsCard.rect.height : 1080f;
            resultsLaidOutHeight = resultsCard.rect.height;
            // Unscaled human height so the 1.15 winner stands between the baseline and the title.
            var humanHeight = Mathf.Clamp((height - ResultsTitleTop - ResultsTitleHeight - 8f - ResultsBaseline) / ResultsWinnerScale, 240f, 520f);
            var decided = resultsOutcome == MatchOutcome.Humans || resultsOutcome == MatchOutcome.Mosquitoes;
            var loserOffset = ResultsChipWidth * ResultsWinnerScale * 0.5f + 24f + ResultsChipWidth * 0.5f;
            foreach (var role in new[] { AlfaRole.Human, AlfaRole.Mosquito })
            {
                var human = role == AlfaRole.Human;
                var winner = human ? resultsOutcome == MatchOutcome.Humans : resultsOutcome == MatchOutcome.Mosquitoes;
                var loser = decided && !winner;
                // Winner centred; loser to its side (humans always left of mosquitoes); undecided side by side.
                var x = !decided ? (human ? -300f : 300f) : winner ? 0f : human ? -loserOffset : loserOffset;
                var scale = winner ? ResultsWinnerScale : loser ? ResultsLoserScale : 1f;
                var team = resultsTeams[role];
                Anchor(team.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(x, ResultsBaseline + (loser ? ResultsLoserLift : 0f)), new Vector2(10f, 10f));
                team.Root.localScale = Vector3.one * scale;
                LayoutResultsFigures(team, human ? humanHeight : humanHeight * 0.72f, scale, human ? ResultsGroupSpacing : ResultsMosquitoSpacing);

                var chip = resultsChips[role];
                Anchor(chip.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, ResultsChipCentre),
                    new Vector2(ResultsChipWidth, ResultsChipHeight));
                chip.Root.localScale = Vector3.one * (winner ? ResultsWinnerScale : 1f);
            }
            // Chips in front of every figure (they cover the legs), the winner's chip in front of the loser's.
            foreach (var role in new[] { AlfaRole.Human, AlfaRole.Mosquito })
                if (!(role == AlfaRole.Human ? resultsOutcome == MatchOutcome.Humans : resultsOutcome == MatchOutcome.Mosquitoes))
                    resultsChips[role].Root.SetAsLastSibling();
            foreach (var role in new[] { AlfaRole.Human, AlfaRole.Mosquito })
                if (role == AlfaRole.Human ? resultsOutcome == MatchOutcome.Humans : resultsOutcome == MatchOutcome.Mosquitoes)
                    resultsChips[role].Root.SetAsLastSibling();
            resultsStatsPill.SetAsLastSibling();
            // The title is always in front (a tall figure or a wing never covers it).
            resultsTitle.transform.parent.SetAsLastSibling();
            Anchor(resultsGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, ResultsBaseline + humanHeight * ResultsWinnerScale * 0.5f), new Vector2(980f, humanHeight * ResultsWinnerScale * 1.5f));
            resultsGlow.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// A team's figures on the baseline: each painted figure is cropped to its opaque bounds, its feet on the line,
        /// spaced around the team's x (the right one of three mirrored so the group does not read as copies), each
        /// over its own contact shadow. Values are unscaled: the team root carries the 1.15 / 0.8 scale.
        /// </summary>
        private static void LayoutResultsFigures(ResultsTeam team, float figureHeight, float scale, float spacing)
        {
            var texture = team.Figures[0].texture;
            var bounds = AlfaUiArt.OpaqueBounds(texture);
            var aspect = texture != null && texture.height > 0 ? bounds.width * texture.width / Mathf.Max(1f, bounds.height * texture.height) : 0.5f;
            var width = figureHeight * aspect;
            var step = spacing / Mathf.Max(0.01f, scale);
            var shadow = ResultsShadowSize / Mathf.Max(0.01f, scale);
            // Draw order: sides first, the centre figure in front.
            var order = team.Count == 3 ? new[] { 0, 2, 1 } : team.Count == 2 ? new[] { 0, 1 } : new[] { 0 };
            var slot = 0;
            foreach (var place in order)
            {
                var offset = (place - (team.Count - 1) * 0.5f) * step;
                var figure = team.Figures[slot];
                var rect = figure.rectTransform;
                // The side figures of a group of three stand a little behind (7 % smaller) on the same baseline.
                var depth = team.Count == 3 && place != 1 ? 0.93f : 1f;
                Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(offset, 0f), new Vector2(width * depth, figureHeight * depth));
                var mirrored = team.Count > 1 && place == team.Count - 1;
                figure.uvRect = mirrored ? new Rect(bounds.xMax, bounds.yMin, -bounds.width, bounds.height) : bounds;
                figure.transform.SetAsLastSibling();
                var contact = team.Shadows[slot].rectTransform;
                Anchor(contact, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(offset, 2f / Mathf.Max(0.01f, scale)), shadow);
                slot++;
            }
            team.Fallback.transform.SetAsLastSibling();
        }

        /// <summary>Per-frame layout upkeep of the stage-2/3 screens (called from LateUpdate).</summary>
        private void UpdateStageTwoLayout()
        {
            if (screen == AlfaUiScreen.Results && resultsCard != null && Mathf.Abs(resultsCard.rect.height - resultsLaidOutHeight) > 0.5f) LayoutResults();
            if (screen == AlfaUiScreen.Pause)
            {
                if (pauseColumn != null && Mathf.Abs(pauseColumn.rect.height - pauseLaidOutHeight) > 0.5f) LayoutPause();
                UpdatePauseVoiceFade();
            }
            if (screen == AlfaUiScreen.Customization) UpdateCustomizationLayout();
        }

        /// <summary>
        /// Full-body figure for a team: painted art in Resources/AlfaUiPortraits/&lt;Role&gt;Winner or &lt;Role&gt;
        /// when it exists, otherwise the in-game model rendered once on transparency (arms up when it won).
        /// </summary>
        private Texture ResultsFigureTexture(AlfaRole role, bool celebrate)
        {
            var key = role + (celebrate ? ":win" : ":stand");
            if (resultsFigureTextures.TryGetValue(key, out var cached)) return cached;
            var painted = LoadRoleArt(role, celebrate ? "Winner" : string.Empty) ?? (celebrate ? LoadRoleArt(role, string.Empty) : null);
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
