using System;
using LetMeSleep.Core;
using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Gameplay HUD (UI-06 screens 7 and 7b, UI-01): objective card with icon, text and bar at the top left, the
    /// clock on a plate at the top centre, the opposing team counter at the top right, the four square inventory
    /// slots centred at the bottom with the stamina bar folded under them (human) and the real key legend at the
    /// bottom right (mosquito). Everything the alpha HUD showed is kept: every mode's score, actor states, the
    /// interaction prompt, context hints, lives, private task, network and voice state. The layout is recomputed
    /// from the canvas width so nothing overlaps at 16:10, 5:4 or 21:9 (ui-presentation-audio-7).
    /// Panel names used by the gameplay capture harness are kept: RoleBadge (now the objective card),
    /// ClockBadge and ContextHintPanel are direct children of GameplayHudView; PrivateEquipment too.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const float HudMargin = 24f;
        private const float EquipmentSlotSize = 104f;
        private const float EquipmentSlotGap = 12f;
        private const float EquipmentPadding = 14f;
        private const float EquipmentWidth = EquipmentPadding * 2f + EquipmentSlotSize * 4f + EquipmentSlotGap * 3f;
        private const float EquipmentBaseHeight = 194f;
        private const float LegendWidth = 360f;
        private const float PromptMaxWidth = 620f;

        private bool isSpectator;
        private AlfaRole hudRole = AlfaRole.Human;
        private RectTransform hudView;
        private float hudLayoutWidth = -1f;
        private TextMeshProUGUI hudClock;
        private TextMeshProUGUI hudMode;
        private TextMeshProUGUI hudBlood;
        private TextMeshProUGUI hudObjective;
        private TextMeshProUGUI hudRoleLabel;
        private AlfaUiIcon hudRoleIcon;
        private UnityEngine.UI.Image hudRoleBackground;
        private UnityEngine.UI.Image hudRoleRing;
        private AlfaUiIcon hudScoreIcon;
        private UnityEngine.UI.Image hudBloodFill;
        private GameObject hudObjectiveBar;
        private GameObject hudTeamPanel;
        private AlfaUiIcon hudTeamIcon;
        private TextMeshProUGUI hudTeamCaption;
        private TextMeshProUGUI hudTeamCount;
        private TextMeshProUGUI hudNetwork;
        private TextMeshProUGUI hudVoice;
        private GameObject hudVoiceChip;
        private TextMeshProUGUI hudTask;
        private GameObject hudTaskPanel;
        private UnityEngine.UI.Image hudTaskFill;
        private TextMeshProUGUI hudLives;
        private GameObject hudLivesChip;
        private TextMeshProUGUI hudInteraction;
        private TextMeshProUGUI hudHint;
        private TextMeshProUGUI hudActorState;
        private GameObject hudPromptPanel;
        private GameObject hudHintPanel;
        private GameObject hudStatePanel;
        private RectTransform hudPromptRect;
        private RectTransform hudStateRect;
        private RectTransform hudHintRect;
        private UnityEngine.UI.Image hudProgress;
        private GameObject hudLegend;
        private GameObject hudLegendRescueRow;
        private RectTransform hudLegendRect;
        private GameObject hudReticle;
        private GameObject hudEquipmentPanel;
        private RectTransform hudEquipmentRect;
        private readonly TextMeshProUGUI[] hudEquipmentLabels = new TextMeshProUGUI[4];
        private readonly AlfaUiIcon[] hudEquipmentIcons = new AlfaUiIcon[4];
        private readonly UnityEngine.UI.Image[] hudEquipmentSlots = new UnityEngine.UI.Image[4];
        private readonly TextMeshProUGUI[] hudEquipmentNumbers = new TextMeshProUGUI[4];
        private TextMeshProUGUI hudEquipmentSelected;
        private TextMeshProUGUI hudStaminaLabel;
        private UnityEngine.UI.Image hudStaminaFill;
        private GameObject hudThrowTrack;
        private RectTransform hudThrowRect;
        private TextMeshProUGUI hudThrowLabel;
        private UnityEngine.UI.Image hudThrowFill;
        private TextMeshProUGUI hudSwapOffer;
        private RectTransform hudSwapRect;

        private void BuildHud()
        {
            var view = factory.View("GameplayHudView", transform, false);
            screens[AlfaUiScreen.Gameplay] = view;
            hudView = (RectTransform)view.transform;
            var ink = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.84f);

            BuildHudObjective(view.transform, ink);
            BuildHudClock(view.transform, ink);
            BuildHudTeamCounter(view.transform, ink);
            BuildHudSideStatus(view.transform);
            BuildHudEquipment(view.transform);

            hudPromptPanel = factory.Panel(view.transform, "InteractionPrompt", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.88f)).gameObject;
            hudPromptRect = hudPromptPanel.GetComponent<RectTransform>();
            Anchor(hudPromptRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(560f, 56f));
            hudInteraction = factory.Text(hudPromptPanel.transform, "Interaction", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudInteraction.textWrappingMode = TextWrappingModes.NoWrap;
            hudInteraction.enableAutoSizing = true;
            hudInteraction.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudInteraction.fontSizeMax = 23f;
            AlfaUiFactory.Fill(hudInteraction.rectTransform, 16f, 16f, 6f, 6f);

            hudStatePanel = factory.Panel(view.transform, "ActorStatePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.9f)).gameObject;
            hudStateRect = hudStatePanel.GetComponent<RectTransform>();
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(460f, 64f));
            hudActorState = factory.Text(hudStatePanel.transform, "ActorState", string.Empty, 22f, AlfaUiTheme.Pajama500, TextAlignmentOptions.Center, true);
            hudActorState.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudActorState.rectTransform, 16f, 16f, 5f, 20f);
            hudProgress = AlfaUiFactory.ProgressBar(hudStatePanel.transform, "StateProgress", AlfaUiTheme.Lamp400, out var progressTrack);
            Anchor(progressTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-28f, 8f));

            BuildHudHint(view.transform);

            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100);
            hudReticle = reticle.gameObject;
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        /// <summary>Top-left objective card (UI-06 "OBJETIVO"). Named RoleBadge for the gameplay capture harness.</summary>
        private void BuildHudObjective(Transform view, Color ink)
        {
            var card = factory.Panel(view, "RoleBadge", ink);
            Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudMargin, -20f), new Vector2(520f, 112f));
            hudRoleBackground = AlfaUiFactory.Node("RolePortrait", card, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudRoleBackground.sprite = AlfaUiSkin.LargeCircle();
            hudRoleBackground.raycastTarget = false;
            Anchor(hudRoleBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(84f, 84f));
            hudRoleRing = AlfaUiFactory.Node("RoleRing", hudRoleBackground.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudRoleRing.sprite = AlfaUiSkin.CircleRing();
            hudRoleRing.raycastTarget = false;
            AlfaUiFactory.Fill(hudRoleRing.rectTransform);
            hudRoleIcon = factory.Icon(hudRoleBackground.transform, "RoleIcon", AlfaUiIconKind.Human, AlfaUiTheme.Sheet100);
            AlfaUiFactory.Fill(hudRoleIcon.rectTransform, 18f, 18f, 16f, 20f);

            hudRoleLabel = factory.Text(card, "RoleLabel", "HUMANO", AlfaUiTheme.MinTextSize, AlfaUiTheme.LabelInk, TextAlignmentOptions.TopLeft, true);
            hudRoleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            hudRoleLabel.characterSpacing = AlfaUiTheme.CaptionTracking;
            Anchor(hudRoleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(112f, -10f), new Vector2(-126f, 28f));
            hudObjective = factory.Text(card, "ObjectiveText", string.Empty, 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            hudObjective.fontStyle = FontStyles.Bold;
            hudObjective.textWrappingMode = TextWrappingModes.NoWrap;
            hudObjective.enableAutoSizing = true;
            hudObjective.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudObjective.fontSizeMax = 24f;
            Anchor(hudObjective.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(112f, -38f), new Vector2(-126f, 32f));

            hudObjectiveBar = AlfaUiFactory.Node("ObjectiveBar", card).gameObject;
            var bar = hudObjectiveBar.GetComponent<RectTransform>();
            Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(112f, 10f), new Vector2(-126f, 30f));
            hudScoreIcon = factory.Icon(bar, "ObjectiveIcon", AlfaUiIconKind.Blood, AlfaUiTheme.Pajama500);
            Anchor(hudScoreIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            hudBloodFill = AlfaUiFactory.ProgressBar(bar, "ObjectiveTrack", AlfaUiTheme.TeamMosquito, out var track);
            Anchor(track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(-126f, 14f));
            hudBlood = factory.Text(bar, "ObjectiveValue", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineRight, true);
            hudBlood.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudBlood.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(88f, 30f));

            hudLivesChip = factory.Panel(view, "LivesChip", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.82f), -1f, -1f, AlfaUiTheme.SmallRadius).gameObject;
            Anchor(hudLivesChip.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudMargin, -142f), new Vector2(240f, 42f));
            hudLives = factory.Text(hudLivesChip.transform, "Lives", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudLives.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudLives.rectTransform, 12f, 12f, 2f, 2f);
            hudLivesChip.SetActive(false);
        }

        private void BuildHudClock(Transform view, Color ink)
        {
            var clock = factory.Panel(view, "ClockBadge", ink);
            Anchor(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(240f, 98f));
            var clockIcon = factory.Icon(clock, "ClockIcon", AlfaUiIconKind.Clock, AlfaUiTheme.Lamp400);
            Anchor(clockIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(30f, 30f));
            hudClock = factory.Title(clock, "Clock", "03:00", 46f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            hudClock.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(hudClock.rectTransform, 44f, 14f, 4f, 36f);
            hudMode = factory.Caption(clock, "ModeLabel", "MODO SANGRE");
            hudMode.alignment = TextAlignmentOptions.Center;
            AlfaUiFactory.Fill(hudMode.rectTransform, 10f, 10f, 64f, 6f);

            hudNetwork = factory.Title(view, "NetworkState", string.Empty, 22f, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Center);
            hudNetwork.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudNetwork.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(640f, 34f));
            hudNetwork.enableAutoSizing = true;
            hudNetwork.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudNetwork.fontSizeMax = 22f;
        }

        private void BuildHudTeamCounter(Transform view, Color ink)
        {
            var team = factory.Panel(view, "TeamCounter", ink);
            hudTeamPanel = team.gameObject;
            Anchor(team, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HudMargin, -20f), new Vector2(270f, 98f));
            hudTeamIcon = factory.Icon(team, "TeamIcon", AlfaUiIconKind.Mosquito, AlfaUiTheme.Pajama500);
            Anchor(hudTeamIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(68f, 68f));
            hudTeamCaption = factory.Caption(team, "TeamCaption", "MOSQUITOS");
            Anchor(hudTeamCaption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(-110f, 26f));
            hudTeamCount = factory.Title(team, "TeamCount", "0", 44f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            hudTeamCount.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudTeamCount.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -36f), new Vector2(-110f, 56f));
            hudTeamPanel.SetActive(false);
        }

        private void BuildHudSideStatus(Transform view)
        {
            // Push-to-talk / voice state on a chip (text.secondary on ink) so it reads over any sky or wall.
            var voiceChip = factory.Panel(view, "VoiceChip", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.82f), -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(voiceChip, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f), shadow: AlfaUiTheme.WithAlpha(Color.black, 0.3f));
            voiceChip.anchorMin = voiceChip.anchorMax = voiceChip.pivot = new Vector2(1f, 1f);
            voiceChip.anchoredPosition = new Vector2(-HudMargin, -128f);
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

            hudTaskPanel = factory.Panel(view, "PrivateTask", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.9f)).gameObject;
            Anchor(hudTaskPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HudMargin, -178f), new Vector2(440f, 124f));
            hudTask = factory.Text(hudTaskPanel.transform, "PrivateTaskText", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            hudTask.textWrappingMode = TextWrappingModes.Normal;
            AlfaUiFactory.Fill(hudTask.rectTransform, 18f, 18f, 10f, 30f);
            hudTaskFill = AlfaUiFactory.ProgressBar(hudTaskPanel.transform, "TaskProgress", AlfaUiTheme.StatusOk, out var taskTrack);
            Anchor(taskTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-36f, 10f));
            hudTaskPanel.SetActive(false);
        }

        private void BuildHudEquipment(Transform view)
        {
            // Four square slots centred at the bottom (UI-06): hands plus three objects, number at the top left,
            // big pictogram, short resource in the corner and the selected slot in blue with the 3-unit frame.
            hudEquipmentPanel = factory.Panel(view, "PrivateEquipment", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.55f)).gameObject;
            hudEquipmentRect = hudEquipmentPanel.GetComponent<RectTransform>();
            AlfaUiFactory.SetSurface(hudEquipmentRect, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.55f), shadow: Color.clear);
            Anchor(hudEquipmentRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(EquipmentWidth, EquipmentBaseHeight));
            for (int i = 0; i < 4; i++)
            {
                var slot = factory.Panel(hudEquipmentPanel.transform, "EquipmentSlotPlate" + i, AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.9f));
                Anchor(slot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding + i * (EquipmentSlotSize + EquipmentSlotGap), 44f),
                    new Vector2(EquipmentSlotSize, EquipmentSlotSize));
                AlfaUiFactory.SetSurface(slot, shadow: Color.clear);
                hudEquipmentSlots[i] = slot.GetComponent<UnityEngine.UI.Image>();
                var number = factory.Title(slot, "EquipmentNumber" + i, i.ToString(), 22f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
                number.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(9f, -3f), new Vector2(30f, 30f));
                hudEquipmentNumbers[i] = number;
                hudEquipmentIcons[i] = factory.Icon(slot, "EquipmentIcon" + i, i == 0 ? AlfaUiIconKind.Hands : AlfaUiIconKind.None, AlfaUiTheme.Moon200);
                Anchor(hudEquipmentIcons[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(60f, 60f));
                hudEquipmentLabels[i] = factory.Title(slot, "EquipmentSlot" + i, string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.BottomRight);
                hudEquipmentLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(hudEquipmentLabels[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(-14f, 28f));
            }
            hudEquipmentSelected = factory.Title(hudEquipmentPanel.transform, "EquipmentSelectedLabel", "MANOS", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            hudEquipmentSelected.textWrappingMode = TextWrappingModes.NoWrap;
            BottomRow(hudEquipmentSelected.rectTransform, 154f, 32f);

            hudStaminaLabel = factory.Title(hudEquipmentPanel.transform, "StaminaLabel", "ESTAMINA 100%", AlfaUiTheme.MinTextSize, AlfaUiTheme.StatusOk, TextAlignmentOptions.MidlineLeft);
            hudStaminaLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudStaminaLabel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding + 28f, 8f), new Vector2(150f, 30f));
            var bolt = factory.Icon(hudEquipmentPanel.transform, "StaminaIcon", AlfaUiIconKind.Bolt, AlfaUiTheme.StatusOk);
            Anchor(bolt.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding, 11f), new Vector2(22f, 22f));
            hudStaminaFill = AlfaUiFactory.ProgressBar(hudEquipmentPanel.transform, "StaminaTrack", AlfaUiTheme.StatusOk, out var staminaTrack);
            Anchor(staminaTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(EquipmentPadding + 186f, 18f),
                new Vector2(-(EquipmentPadding * 2f + 186f), 10f));

            hudThrowTrack = AlfaUiFactory.Node("ThrowCharge", hudEquipmentPanel.transform).gameObject;
            hudThrowRect = hudThrowTrack.GetComponent<RectTransform>();
            BottomRow(hudThrowRect, EquipmentBaseHeight, 50f);
            hudThrowLabel = factory.Title(hudThrowTrack.transform, "ThrowLabel", "CARGA PANTUFLA", AlfaUiTheme.MinTextSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left);
            hudThrowLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudThrowLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 32f));
            hudThrowFill = AlfaUiFactory.ProgressBar(hudThrowTrack.transform, "Track", AlfaUiTheme.Lamp400, out var throwTrack);
            Anchor(throwTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 10f));
            hudSwapOffer = factory.Title(hudEquipmentPanel.transform, "SwapOffer", string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Left);
            hudSwapOffer.textWrappingMode = TextWrappingModes.Normal;
            hudSwapOffer.overflowMode = TextOverflowModes.Overflow;
            hudSwapRect = hudSwapOffer.rectTransform;
            BottomRow(hudSwapRect, EquipmentBaseHeight, 60f);
            hudEquipmentPanel.SetActive(false);
        }

        private static void BottomRow(RectTransform rect, float y, float height) =>
            Anchor(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(-EquipmentPadding * 2f, height));

        /// <summary>
        /// Context help. Human: the hint line at the bottom left. Mosquito: the same panel at the bottom right
        /// carries the real key legend (UI-06 7b) and, above it, the hint when it adds something to the legend.
        /// </summary>
        private void BuildHudHint(Transform view)
        {
            hudHintPanel = factory.Panel(view, "ContextHintPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.78f)).gameObject;
            hudHintRect = hudHintPanel.GetComponent<RectTransform>();
            Anchor(hudHintRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(HudMargin, HudMargin), new Vector2(600f, 78f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            hudHint.textWrappingMode = TextWrappingModes.Normal;
            hudHint.enableAutoSizing = false;
            hudHint.overflowMode = TextOverflowModes.Overflow;
            Anchor(hudHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 56f));

            var legend = factory.Vertical(hudHintPanel.transform, "ControlsLegend", 6f);
            hudLegend = legend.gameObject;
            hudLegendRect = legend;
            Anchor(legend, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-32f, 0f));
            LegendRow(legend, "LegendAim", null, AlfaUiIconKind.Mouse, "Apuntar");
            LegendRow(legend, "LegendFly", new[] { "W" }, AlfaUiIconKind.None, "Volar");
            LegendRow(legend, "LegendClimb", new[] { "ESPACIO", "CTRL" }, AlfaUiIconKind.None, "Subir · bajar");
            LegendRow(legend, "LegendPerch", new[] { "F" }, AlfaUiIconKind.None, "Posarte");
            LegendRow(legend, "LegendBite", new[] { "E" }, AlfaUiIconKind.None, "Picar");
            hudLegendRescueRow = LegendRow(legend, "LegendRescue", new[] { "R" }, AlfaUiIconKind.None, "Ayudar").gameObject;
            hudLegend.SetActive(false);
        }

        private RectTransform LegendRow(Transform parent, string name, string[] keys, AlfaUiIconKind icon, string label)
        {
            var row = factory.Horizontal(parent, name, 12f, TextAnchor.MiddleLeft);
            var rowLayout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            rowLayout.minHeight = rowLayout.preferredHeight = 40f;
            // Keys right-aligned in their column so each sits next to its label (UI-06 7b legend).
            var keysColumn = factory.Horizontal(row, "Keys", 8f, TextAnchor.MiddleRight);
            var keysLayout = keysColumn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            keysLayout.minWidth = keysLayout.preferredWidth = 164f;
            keysLayout.flexibleWidth = 0f;
            if (icon != AlfaUiIconKind.None)
            {
                var symbol = factory.Icon(keysColumn, "Icon", icon, AlfaUiTheme.Sheet100);
                var symbolLayout = symbol.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                symbolLayout.minWidth = symbolLayout.preferredWidth = 36f;
                symbolLayout.minHeight = symbolLayout.preferredHeight = 36f;
            }
            if (keys != null) foreach (var key in keys) factory.KeyCap(keysColumn, "Key_" + key, key, 36f);
            var text = factory.Text(row, "Label", label, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return row;
        }

        public void PresentHud(BloodHudUiState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            isSpectator = state.IsSpectator;
            hudRole = state.Role;
            var human = state.Role == AlfaRole.Human;
            hudClock.text = FormatClock(state.SecondsRemaining);
            hudMode.text = "MODO " + AlfaModeText.Name(state.ModeId);
            PresentHudObjective(state, human);
            PresentHudTeamCounter(state, human);

            hudTaskPanel.SetActive(!state.IsSpectator && !string.IsNullOrWhiteSpace(state.PrivateTaskText));
            hudTask.text = state.PrivateTaskText;
            hudTaskFill.rectTransform.anchorMax = new Vector2(state.TaskProgress01, 1f);
            hudLives.text = state.IsSpectator ? "ESPECTADOR" : !human && state.ModeId != GameModes.Blood ? $"VIDAS  {state.LivesRemaining}" : string.Empty;
            hudLivesChip.SetActive(!string.IsNullOrEmpty(hudLives.text));

            var equipment = state.IsSpectator ? null : state.Equipment;
            hudEquipmentPanel.SetActive(equipment != null);
            if (equipment != null) PresentHudEquipment(equipment);
            hudReticle.SetActive(!state.IsSpectator);

            hudInteraction.text = state.Interaction;
            var hint = state.ContextHint ?? string.Empty;
            // The mosquito legend already lists the flight keys: only a situational hint is repeated above it.
            if (!human && hint.StartsWith("W · volar", StringComparison.Ordinal)) hint = string.Empty;
            hudHint.text = hint;
            hudActorState.text = state.ModeId != GameModes.Blood && state.ActorState == HudActorState.Extracting ? "INTERRUMPIENDO" : ActorStateText(state.Role, state.ActorState);
            hudActorState.color = state.ActorState == HudActorState.Normal ? AlfaUiTheme.Moon200 : AlfaUiTheme.StatusWarn;
            hudProgress.transform.parent.gameObject.SetActive(state.ActorState == HudActorState.Extracting || state.ActorState == HudActorState.Recovering);
            hudProgress.rectTransform.anchorMax = new Vector2(state.StateProgress01, 1f);
            hudNetwork.text = state.NetworkMessage;
            hudPromptPanel.SetActive(!string.IsNullOrWhiteSpace(hudInteraction.text));
            hudStatePanel.SetActive(!string.IsNullOrWhiteSpace(hudActorState.text) ||
                state.ActorState == HudActorState.Extracting || state.ActorState == HudActorState.Recovering);
            hudLegend.SetActive(!human && !state.IsSpectator);
            hudLegendRescueRow.SetActive(state.ModeId == GameModes.Tasks);
            hudHintPanel.SetActive(hudLegend.activeSelf || !string.IsNullOrWhiteSpace(hudHint.text));
            LayoutHud();
            if (screen != AlfaUiScreen.Gameplay && screen != AlfaUiScreen.Pause && screen != AlfaUiScreen.Settings)
                ShowGameplay();
        }

        private void PresentHudObjective(BloodHudUiState state, bool human)
        {
            var team = human ? AlfaUiTheme.TeamHuman : AlfaUiTheme.TeamMosquito;
            var teamLight = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn;
            hudRoleLabel.text = "<color=#" + ColorUtility.ToHtmlStringRGB(teamLight) + ">" + (human ? "HUMANO" : "MOSQUITO") + "</color>  ·  OBJETIVO";
            hudRoleIcon.Kind = human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            hudRoleBackground.color = Color.Lerp(team, AlfaUiTheme.Ink900, 0.35f);
            hudRoleRing.color = AlfaUiTheme.WithAlpha(Color.Lerp(team, Color.white, 0.35f), 0.95f);

            string text, value = string.Empty;
            float ratio = -1f;
            Color bar;
            AlfaUiIconKind icon;
            if (state.ModeId == GameModes.Tasks)
            {
                text = human ? "Completá las tareas del equipo" : "Frená las tareas humanas";
                ratio = state.TasksGoal > 0 ? Mathf.Clamp01((float)state.TasksCompleted / state.TasksGoal) : 0f;
                value = state.TasksCompleted + "/" + state.TasksGoal;
                bar = human ? AlfaUiTheme.StatusOk : AlfaUiTheme.Lamp400;
                icon = AlfaUiIconKind.Ready;
            }
            else if (state.ModeId == GameModes.Survival)
            {
                text = human ? "Eliminá a todos los mosquitos" : "Sobreviví hasta el final";
                if (state.MosquitoesTotal > 0)
                {
                    ratio = Mathf.Clamp01((float)state.MosquitoesAlive / state.MosquitoesTotal);
                    value = state.MosquitoesAlive + "/" + state.MosquitoesTotal;
                }
                else value = state.MosquitoesAlive + (state.MosquitoesAlive == 1 ? " VIVO" : " VIVOS");
                bar = AlfaUiTheme.TeamMosquito;
                icon = AlfaUiIconKind.Mosquito;
            }
            else
            {
                text = human ? "Evitá que te piquen" : "Picá al humano y juntá sangre";
                ratio = state.BloodTarget > 0f ? Mathf.Clamp01(state.BloodCurrent / state.BloodTarget) : 0f;
                value = state.BloodCurrent.ToString("0.#") + "/" + state.BloodTarget.ToString("0.#");
                bar = AlfaUiTheme.TeamMosquito;
                icon = AlfaUiIconKind.Blood;
            }
            hudObjective.text = text;
            hudBlood.text = value;
            hudScoreIcon.Kind = icon;
            hudScoreIcon.color = Color.Lerp(bar, Color.white, 0.15f);
            var track = hudBloodFill.transform.parent.gameObject;
            track.SetActive(ratio >= 0f);
            AlfaUiFactory.SetBarColor(hudBloodFill, bar);
            hudBloodFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0f, ratio), 1f);
            hudObjectiveBar.SetActive(ratio >= 0f || !string.IsNullOrEmpty(value));
        }

        /// <summary>Opposing team at the top right: MOSQUITOS alive/total for humans, HUMANOS awake/total for mosquitoes.</summary>
        private void PresentHudTeamCounter(BloodHudUiState state, bool human)
        {
            int count, total;
            if (human) { count = state.MosquitoesAlive; total = state.MosquitoesTotal; }
            else { count = state.HumansActive; total = state.HumansTotal; }
            var known = total > 0 || (human && count > 0);
            hudTeamPanel.SetActive(known);
            if (!known) return;
            hudTeamCaption.text = human ? "MOSQUITOS" : "HUMANOS";
            hudTeamCount.text = total > 0 ? Mathf.Max(0, count) + "/" + total : count.ToString();
            hudTeamIcon.Kind = human ? AlfaUiIconKind.Mosquito : AlfaUiIconKind.Online;
            hudTeamIcon.color = human ? AlfaUiTheme.StatusWarn : AlfaUiTheme.Sky400;
        }

        private void PresentHudEquipment(EquipmentHudUiState equipment)
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = i == 0 ? null : equipment.Slots[i - 1];
                var kind = i == 0 ? AlfaUiIconKind.Hands : slot.Icon;
                bool selected = equipment.SelectedSlot == i - 1;
                hudEquipmentIcons[i].Kind = kind;
                hudEquipmentIcons[i].gameObject.SetActive(kind != AlfaUiIconKind.None);
                hudEquipmentLabels[i].text = i == 0 ? string.Empty : ShortResource(slot.ResourceText);
                SetEquipmentSlotSelected(i, selected);
                hudEquipmentIcons[i].color = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Moon200;
                hudEquipmentNumbers[i].color = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Moon200;
                hudEquipmentLabels[i].color = selected ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Lamp400;
                if (!selected) continue;
                var name = i == 0 ? "MANOS" : slot.Label;
                var resource = i == 0 ? "SIN OBJETO" : slot.ResourceText;
                hudEquipmentSelected.text = name + (string.IsNullOrEmpty(resource) ? string.Empty : "  <color=#A8B8D8>·  " + resource + "</color>");
            }
            var stamina = Mathf.RoundToInt(equipment.Stamina01 * 100);
            var staminaColor = equipment.Stamina01 < 0.25f ? AlfaUiTheme.StatusWarn : AlfaUiTheme.StatusOk;
            hudStaminaLabel.text = "ESTAMINA " + stamina + "%";
            hudStaminaLabel.color = staminaColor;
            AlfaUiFactory.SetBarColor(hudStaminaFill, staminaColor);
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

        /// <summary>"3 CARGAS" → "3", "2,9 s" stays, "REUTILIZABLE" is the default and is not repeated in the slot.</summary>
        private static string ShortResource(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource) || resource == "REUTILIZABLE") return string.Empty;
            resource = resource.Trim();
            if (resource.Length <= 6) return resource;
            var first = resource.Split(' ')[0];
            return first.Length > 0 && char.IsDigit(first[0]) ? first : string.Empty;
        }

        private void UpdateEquipmentLayout(bool charging, bool hasSwapOffer)
        {
            var nextTop = EquipmentBaseHeight;
            if (charging)
            {
                BottomRow(hudThrowRect, nextTop, 50f);
                nextTop += 56f;
            }
            if (hasSwapOffer)
            {
                BottomRow(hudSwapRect, nextTop, 60f);
                nextTop += 66f;
            }
            hudEquipmentRect.sizeDelta = new Vector2(EquipmentWidth, nextTop);
        }

        /// <summary>Equipment slot plate: selected = primary blue with the 3-unit accent.blue frame.</summary>
        private void SetEquipmentSlotSelected(int index, bool selected)
        {
            var plate = hudEquipmentSlots[index];
            if (plate == null) return;
            plate.color = selected ? Color.white : AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.9f);
            AlfaUiFactory.SetSurface(plate, selected ? AlfaUiTheme.PrimaryHi : Color.white, selected ? AlfaUiTheme.Primary : new Color(0.8f, 0.84f, 0.9f, 1f),
                selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f), Color.clear);
            AlfaUiFactory.MarkSelectedFrame(plate, selected);
            if (selected) AlfaUiFactory.SetSurface(plate, shadow: AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.4f));
        }

        /// <summary>
        /// Places the bottom widgets from the canvas width: the prompt and actor state stack above the belt (or
        /// low in the centre without one), the hint/legend panel takes the corner and never reaches the centre.
        /// </summary>
        private void LayoutHud()
        {
            if (hudView == null) return;
            var width = hudView.rect.width;
            if (width <= 1f) return;
            hudLayoutWidth = width;
            // The network line sits under the clock and must stay clear of the objective card and the right stack.
            hudNetwork.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(width - 2f * (HudMargin + 520f + 16f), 360f, 640f), 34f);
            var human = hudRole == AlfaRole.Human;
            var belt = hudEquipmentPanel.activeSelf;
            var promptWidth = Mathf.Clamp(width - 2f * (HudMargin + LegendWidth + 16f), 420f, PromptMaxWidth);
            var promptY = belt ? 18f + hudEquipmentRect.sizeDelta.y + 12f : 30f;
            Anchor(hudPromptRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, promptY), new Vector2(promptWidth, 56f));
            var stateY = promptY + (hudPromptPanel.activeSelf ? 66f : 0f);
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, stateY), new Vector2(Mathf.Min(460f, promptWidth), 64f));

            var legend = hudLegend.activeSelf;
            var hasHint = !string.IsNullOrWhiteSpace(hudHint.text);
            float panelWidth;
            if (legend) panelWidth = LegendWidth;
            else
            {
                var centreHalf = belt ? EquipmentWidth * 0.5f : promptWidth * 0.5f;
                panelWidth = Mathf.Clamp(width * 0.5f - centreHalf - HudMargin - 16f, 280f, 600f);
            }
            var textWidth = panelWidth - 32f;
            var hintHeight = hasHint ? Mathf.Ceil(hudHint.GetPreferredValues(hudHint.text, textWidth, 0f).y) + 2f : 0f;
            hudHint.gameObject.SetActive(hasHint);
            Anchor(hudHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, hintHeight));
            var legendRows = 0;
            foreach (Transform row in hudLegendRect) if (row.gameObject.activeSelf) legendRows++;
            var legendHeight = legend ? legendRows * 40f + Mathf.Max(0, legendRows - 1) * 6f : 0f;
            hudLegendRect.sizeDelta = new Vector2(-32f, legendHeight);
            var height = 24f + hintHeight + (hasHint && legend ? 12f : 0f) + legendHeight;
            if (legend)
                Anchor(hudHintRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-HudMargin, HudMargin), new Vector2(panelWidth, height));
            else
                Anchor(hudHintRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(HudMargin, HudMargin), new Vector2(panelWidth, Mathf.Max(height, 60f)));
        }

        private void UpdateHudLayoutIfResized()
        {
            if (screen != AlfaUiScreen.Gameplay || hudView == null) return;
            if (Mathf.Abs(hudView.rect.width - hudLayoutWidth) > 0.5f) LayoutHud();
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
    }
}
