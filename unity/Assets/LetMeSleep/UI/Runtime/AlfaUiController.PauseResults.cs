using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Pause (UI-06 screen 8) and results (screen 9).
    /// Pause: left column "PARTIDA EN PAUSA" with CONTINUAR (blue), AJUSTES and SALIR over the still visible scene,
    /// and the voice panel under it with a scrolling, pooled list of players (ui-presentation-audio-5 and -6): rows
    /// are rebuilt only when the set of players changes, never while the pause is hidden, and focus survives.
    /// There is no "VOLVER A LA SALA" mid-round: only the host can end a round, so it is not offered.
    /// Results: big "¡HUMANOS GANAN!" / "¡MOSQUITOS GANAN!" banner in the team colour with the role portraits
    /// (painted ones from Resources/AlfaUiPortraits, otherwise the in-game models), team chips and the actions that
    /// exist: JUGAR DE NUEVO (training) or VOLVER A LA SALA (host), plus leaving.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private UnityEngine.UI.Button pauseLeaveButton;
        private TextMeshProUGUI pauseLeaveLabel;
        private UnityEngine.UI.Button pauseVoiceMuteButton;
        private TextMeshProUGUI pauseVoiceMuteLabel;
        private TextMeshProUGUI pauseVoiceStatus;
        private GameObject pauseVoicePanel;
        private RectTransform pauseVoicePeers;
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
        private UnityEngine.UI.Image resultsBannerTint;
        private UnityEngine.UI.RawImage resultsHumanPortrait;
        private UnityEngine.UI.RawImage resultsMosquitoPortrait;
        private readonly Dictionary<AlfaRole, ResultsChip> resultsChips = new Dictionary<AlfaRole, ResultsChip>();

        private sealed class ResultsChip
        {
            public RectTransform Root;
            public TextMeshProUGUI Count;
            public GameObject Crown;
        }

        private void BuildPause()
        {
            var view = factory.View("PauseView", transform, false);
            var backdrop = view.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.34f);
            backdrop.raycastTarget = true;
            screens[AlfaUiScreen.Pause] = view;
            var wash = AlfaUiFactory.Node("NightWash", view.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            wash.sprite = AlfaUiFactory.LinearFadeSprite();
            wash.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.9f);
            wash.raycastTarget = false;
            Anchor(wash.rectTransform, Vector2.zero, new Vector2(0.55f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var column = AlfaUiFactory.Node("PauseColumn", view.transform).GetComponent<RectTransform>();
            AlfaUiFactory.Place(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(64f, 40f), new Vector2(64f + 540f, -84f));
            const float cardHeight = 96f + 3f * 80f + 2f * 14f + 40f;
            var card = factory.Panel(column, "PauseCard", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.96f));
            Anchor(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, cardHeight));
            var content = factory.Vertical(card, "Content", 14f);
            AlfaUiFactory.Fill(content, 26f, 26f, 20f, 26f);
            var title = factory.Title(content, "Title", "PARTIDA EN PAUSA", AlfaUiTheme.HeaderTitleSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            title.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 60f;
            PauseButton(content, "PauseContinueButton", "CONTINUAR", ResumeFromPause, AlfaButtonStyle.Primary, AlfaUiIconKind.Play);
            PauseButton(content, "PauseSettingsButton", "AJUSTES", () => OpenSettings(AlfaUiScreen.Pause), AlfaButtonStyle.Secondary, AlfaUiIconKind.Gear);
            pauseLeaveButton = PauseButton(content, "PauseLeaveButton", "SALIR DE LA SALA", LeaveGameplayContext, AlfaButtonStyle.Danger, AlfaUiIconKind.Exit);
            pauseLeaveLabel = pauseLeaveButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            var voice = factory.Panel(column, "PauseVoicePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f));
            pauseVoicePanel = voice.gameObject;
            AlfaUiFactory.Place(voice, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -(cardHeight + 18f)));
            var header = factory.Horizontal(voice, "Header", 10f, TextAnchor.MiddleLeft);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-40f, 36f));
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
                (actions as IVoiceActions)?.SetLocalVoiceMuted(!voiceState.LocalMuted), AlfaButtonStyle.Secondary, 58f, AlfaUiIconKind.Microphone);
            Anchor((RectTransform)pauseVoiceMuteButton.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(-40f, 58f));
            pauseVoiceMuteLabel = pauseVoiceMuteButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var hint = factory.Text(voice, "PeersHint", "Tocá a un jugador para silenciarlo o volver a escucharlo.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            hint.overflowMode = TextOverflowModes.Overflow;
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(-40f, 54f));
            var scroll = factory.ScrollView(voice, "PauseVoiceScroll", out pauseVoicePeers, 200f, true);
            AlfaUiFactory.Place(scroll, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -190f));
            pauseVoicePeers.name = "PauseVoicePeers";
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
                        AlfaButtonStyle.Secondary, 52f, AlfaUiIconKind.Audio);
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
            view.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.62f);
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            screens[AlfaUiScreen.Results] = view;
            var card = CenteredPanel(view.transform, "ResultsCard", 1240f, 800f);
            card.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night800;

            var banner = AlfaUiFactory.Node("ResultsBanner", card, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)).GetComponent<RectTransform>();
            AlfaUiFactory.Fill(banner, 20f, 20f, 20f, 322f);
            var bannerImage = banner.GetComponent<UnityEngine.UI.Image>();
            bannerImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            bannerImage.type = UnityEngine.UI.Image.Type.Sliced;
            bannerImage.color = AlfaUiTheme.PanelInset;
            bannerImage.raycastTarget = false;
            resultsBannerTint = AlfaUiFactory.Node("BannerTint", banner, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            resultsBannerTint.sprite = AlfaUiFactory.VerticalFadeSprite();
            resultsBannerTint.raycastTarget = false;
            AlfaUiFactory.Fill(resultsBannerTint.rectTransform);
            resultsHumanPortrait = ResultsPortrait(banner, "HumanPortrait");
            resultsMosquitoPortrait = ResultsPortrait(banner, "MosquitoPortrait");
            // Ink at the top so the title reads over any portrait, and at the bottom for the team chips.
            var topShade = AlfaUiFactory.Node("TopShade", banner, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            topShade.sprite = AlfaUiFactory.VerticalFadeSprite();
            topShade.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.75f);
            topShade.raycastTarget = false;
            // Flipped in place (centre pivot) so the opaque end of the fade sits at the banner top.
            Anchor(topShade.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(0f, 180f));
            topShade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var bottomShade = AlfaUiFactory.Node("BottomShade", banner, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            bottomShade.sprite = AlfaUiFactory.VerticalFadeSprite();
            bottomShade.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.6f);
            bottomShade.raycastTarget = false;
            Anchor(bottomShade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 120f));
            resultsTitle = factory.Title(banner, "Title", "RONDA INTERRUMPIDA", 88f, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
            resultsTitle.textWrappingMode = TextWrappingModes.NoWrap;
            resultsTitle.enableAutoSizing = true;
            resultsTitle.fontSizeMin = 48f;
            resultsTitle.fontSizeMax = 88f;
            Anchor(resultsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-60f, 108f));

            var chips = factory.Horizontal(card, "Scoreboard", 48f, TextAnchor.MiddleCenter);
            Anchor(chips, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 276f), new Vector2(820f, 88f));
            resultsChips[AlfaRole.Human] = ResultsTeamChip(chips, "ResultsHumansChip", "HUMANOS", AlfaUiIconKind.Online);
            resultsChips[AlfaRole.Mosquito] = ResultsTeamChip(chips, "ResultsMosquitoesChip", "MOSQUITOS", AlfaUiIconKind.Mosquito);

            resultsStats = factory.Text(card, "Stats", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            resultsStats.overflowMode = TextOverflowModes.Overflow;
            Anchor(resultsStats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(-80f, 132f));

            var buttons = factory.Horizontal(card, "ResultsActions", 24f, TextAnchor.MiddleCenter);
            Anchor(buttons, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(-80f, 84f));
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

        /// <summary>Portrait in a rounded, team-framed window (like the training cards) so it reads as intentional.</summary>
        private UnityEngine.UI.RawImage ResultsPortrait(Transform banner, string name)
        {
            var frame = AlfaUiFactory.Node(name + "Frame", banner, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)).GetComponent<UnityEngine.UI.Image>();
            frame.sprite = AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.color = AlfaUiTheme.PanelInset;
            frame.raycastTarget = false;
            var raw = AlfaUiFactory.Node(name, frame.transform, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            raw.raycastTarget = false;
            raw.enabled = false;
            AlfaUiFactory.Fill(raw.rectTransform);
            var ring = AlfaUiFactory.Node("Ring", frame.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            ring.sprite = AlfaUiSkin.Ring(AlfaUiTheme.ButtonRadius);
            ring.type = UnityEngine.UI.Image.Type.Sliced;
            ring.raycastTarget = false;
            AlfaUiFactory.Fill(ring.rectTransform);
            var fallback = factory.Icon(raw.transform, "Fallback", name.StartsWith("Human", StringComparison.Ordinal) ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito,
                AlfaUiTheme.WithAlpha(AlfaUiTheme.Sheet100, 0.85f));
            Anchor(fallback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(180f, 180f));
            return raw;
        }

        private ResultsChip ResultsTeamChip(Transform parent, string name, string label, AlfaUiIconKind icon)
        {
            var chip = factory.Panel(parent, name, Color.white, 386f, 88f);
            var layout = chip.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = 386f;
            layout.minHeight = 88f;
            var symbol = factory.Icon(chip, "Icon", icon, AlfaUiTheme.Sheet100);
            Anchor(symbol.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(54f, 54f));
            var name2 = factory.Title(chip, "Team", label, 30f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            name2.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(name2.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(-190f, 0f));
            var count = factory.Title(chip, "Count", string.Empty, 50f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineRight);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(90f, 0f));
            var crown = factory.Icon(chip, "Crown", AlfaUiIconKind.Trophy, AlfaUiTheme.Lamp400);
            Anchor(crown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(40f, 40f));
            return new ResultsChip { Root = chip, Count = count, Crown = crown.gameObject };
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
            var team = humans ? AlfaUiTheme.TeamHuman : mosquitoes ? AlfaUiTheme.TeamMosquito : AlfaUiTheme.Border;
            resultsBannerTint.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, AlfaUiTheme.Night800, 0.25f), 0.85f);
            PresentResultsPortraits(state.Outcome);
            PresentResultsChip(AlfaRole.Human, state.HumansCount, humans, mosquitoes);
            PresentResultsChip(AlfaRole.Mosquito, state.MosquitoesCount, mosquitoes, humans);

            var reason = string.IsNullOrWhiteSpace(state.Reason) ? string.Empty : "\n" + state.Reason;
            resultsStats.text = "MODO " + AlfaModeText.Name(state.ModeId) + "  ·  " +
                AlfaModeText.ResultScore(state.ModeId, state.BloodCurrent, state.BloodTarget, state.TasksCompleted, state.TasksGoal, state.MosquitoesAlive) +
                "  ·  Tiempo " + FormatClock(state.ElapsedSeconds) + reason;
            if (!state.IsTraining && !state.IsOwner) resultsStats.text += "\n<color=#FFC93C>ESPERANDO AL ANFITRIÓN…</color>";
            if (state.IsTraining && !HasSelectedTrainingMap) resultsStats.text += "\n" + NoTrainingMaps;

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

        private static void SetButtonIcon(UnityEngine.UI.Button button, AlfaUiIconKind kind)
        {
            var icon = button.transform.Find("IconPlate/Icon")?.GetComponent<AlfaUiIcon>();
            if (icon != null) icon.Kind = kind;
        }

        /// <summary>Winner large on the left, loser smaller and dimmed on the right; side by side when interrupted.</summary>
        private void PresentResultsPortraits(MatchOutcome outcome)
        {
            var humanWon = outcome == MatchOutcome.Humans;
            var mosquitoWon = outcome == MatchOutcome.Mosquitoes;
            var winnerMin = new Vector2(0.05f, 0.05f); var winnerMax = new Vector2(0.57f, 0.71f);
            var loserMin = new Vector2(0.6f, 0.05f); var loserMax = new Vector2(0.95f, 0.6f);
            var evenLeftMin = new Vector2(0.05f, 0.05f); var evenLeftMax = new Vector2(0.49f, 0.66f);
            var evenRightMin = new Vector2(0.51f, 0.05f); var evenRightMax = new Vector2(0.95f, 0.66f);
            PlacePortrait(resultsHumanPortrait, AlfaRole.Human, humanWon ? winnerMin : mosquitoWon ? loserMin : evenLeftMin,
                humanWon ? winnerMax : mosquitoWon ? loserMax : evenLeftMax, mosquitoWon);
            PlacePortrait(resultsMosquitoPortrait, AlfaRole.Mosquito, mosquitoWon ? winnerMin : humanWon ? loserMin : evenRightMin,
                mosquitoWon ? winnerMax : humanWon ? loserMax : evenRightMax, humanWon);
        }

        private void PlacePortrait(UnityEngine.UI.RawImage target, AlfaRole role, Vector2 min, Vector2 max, bool dim)
        {
            var rect = (RectTransform)target.transform.parent;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var team = role == AlfaRole.Human ? AlfaUiTheme.TeamHuman : AlfaUiTheme.TeamMosquito;
            var ring = rect.Find("Ring")?.GetComponent<UnityEngine.UI.Image>();
            if (ring != null) ring.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, dim ? 0.1f : 0.35f), dim ? 0.55f : 0.95f);
            EnsureTrainingPortraits();
            var source = role == AlfaRole.Human ? trainingHumanPortrait : trainingMosquitoPortrait;
            var texture = source != null ? source.texture : null;
            target.texture = texture;
            target.enabled = true;
            target.color = texture == null ? Color.clear : dim ? new Color(0.55f, 0.58f, 0.68f, 1f) : Color.white;
            var fallback = target.transform.Find("Fallback");
            if (fallback != null) fallback.gameObject.SetActive(texture == null);
            if (texture == null) return;
            // Cover the area at 1240 x 800 (banner 1200 x 458) without stretching: crop the longer side.
            var areaAspect = (max.x - min.x) * 1200f / Mathf.Max(1f, (max.y - min.y) * 458f);
            var textureAspect = texture.height > 0 ? (float)texture.width / texture.height : areaAspect;
            target.uvRect = textureAspect > areaAspect
                ? new Rect((1f - areaAspect / textureAspect) * 0.5f, 0f, areaAspect / textureAspect, 1f)
                : new Rect(0f, 1f - textureAspect / areaAspect, 1f, textureAspect / areaAspect);
        }

        private void PresentResultsChip(AlfaRole role, int count, bool winner, bool loser)
        {
            var chip = resultsChips[role];
            var human = role == AlfaRole.Human;
            var style = winner ? human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger : AlfaButtonStyle.Secondary;
            AlfaUiTheme.StyleColors(style, out var top, out var bottom, out var frame, out _);
            AlfaUiFactory.SetSurface(chip.Root, top, bottom, winner ? AlfaUiTheme.Lamp400 : AlfaUiTheme.WithAlpha(human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn, 0.7f),
                winner ? AlfaUiTheme.WithAlpha(AlfaUiTheme.Lamp400, 0.35f) : AlfaUiTheme.WithAlpha(Color.black, 0.4f));
            AlfaUiFactory.MarkSelectedFrame(chip.Root, winner);
            if (winner) AlfaUiFactory.SetFrame(chip.Root, AlfaUiTheme.Lamp400);
            var icon = chip.Root.Find("Icon")?.GetComponent<AlfaUiIcon>();
            if (icon != null) icon.color = winner ? AlfaUiTheme.Sheet100 : human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn;
            chip.Count.text = count >= 0 ? count.ToString() : string.Empty;
            chip.Count.color = loser ? AlfaUiTheme.Moon200 : AlfaUiTheme.Sheet100;
            chip.Crown.SetActive(winner);
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
    }
}
