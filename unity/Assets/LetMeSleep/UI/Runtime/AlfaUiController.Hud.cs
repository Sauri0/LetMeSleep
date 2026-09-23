using System;
using System.Collections.Generic;
using LetMeSleep.Core;
using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Gameplay HUD (UI-06 screens 7 and 7b, UI-01): objective card with the character's face on a #FFC93C badge, text
    /// and bar at the top left; the clock on a plate at the top centre (tabular figures); the opposing team counter
    /// at the top right; four 80-unit inventory slots centred at the bottom on an opaque tray with the stamina bar
    /// folded under them (human) and a compact four-row key legend at the bottom right under its own status header
    /// (mosquito). Everything the alpha HUD showed is kept: every mode's score, actor states, the interaction
    /// prompt, context hints, lives, private task, network and voice state. Only one warning is shown at a time: the
    /// "being bitten" hint becomes the central banner, and the swap offer is one red chip (its duplicate prompt is
    /// dropped). The layout is recomputed from the canvas width so nothing overlaps at 16:10, 5:4 or 21:9.
    /// Panel names used by the gameplay capture harness are kept: RoleBadge (the objective card), ClockBadge and
    /// ContextHintPanel are direct children of GameplayHudView; PrivateEquipment too.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const float HudMargin = 24f;
        private const float EquipmentSlotSize = 80f;
        private const float EquipmentSlotGap = 10f;
        private const float EquipmentPadding = 12f;
        private const float EquipmentWidth = EquipmentPadding * 2f + EquipmentSlotSize * 4f + EquipmentSlotGap * 3f;
        private const float EquipmentSlotsBottom = 42f;
        private const float EquipmentBaseHeight = EquipmentSlotsBottom + EquipmentSlotSize + 44f;
        private const float LegendWidth = 340f;
        private const float LegendRowHeight = 34f;
        private const float PromptMaxWidth = 620f;
        private const string BittenBanner = "TE ESTÁN PICANDO · MIRÁ Y GOLPEÁ";
        private static readonly Color ObjectiveBadge = AlfaUiTheme.Lamp400;

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
        private UnityEngine.UI.RawImage hudRoleFace;
        private readonly Dictionary<AlfaRole, Texture> hudFaces = new Dictionary<AlfaRole, Texture>();
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
        private RectTransform hudLivesHearts;
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
        private RectTransform hudLegendRect;
        private GameObject hudLegendHeader;
        private RectTransform hudLegendHeaderRect;
        private TextMeshProUGUI hudLegendStatus;
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
        private GameObject hudSwapChip;
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

            hudPromptPanel = factory.Panel(view.transform, "InteractionPrompt", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f)).gameObject;
            hudPromptRect = hudPromptPanel.GetComponent<RectTransform>();
            Anchor(hudPromptRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(560f, 56f));
            hudInteraction = factory.Text(hudPromptPanel.transform, "Interaction", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hudInteraction.textWrappingMode = TextWrappingModes.NoWrap;
            hudInteraction.enableAutoSizing = true;
            hudInteraction.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudInteraction.fontSizeMax = 23f;
            AlfaUiFactory.Fill(hudInteraction.rectTransform, 16f, 16f, 6f, 6f);

            hudStatePanel = factory.Panel(view.transform, "ActorStatePanel", AlfaUiTheme.PanelInset).gameObject;
            AlfaUiFactory.SetSurface(hudStatePanel.GetComponent<RectTransform>(), frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f));
            hudStateRect = hudStatePanel.GetComponent<RectTransform>();
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(460f, 64f));
            hudActorState = factory.Text(hudStatePanel.transform, "ActorState", string.Empty, 23f, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Center, true);
            hudActorState.textWrappingMode = TextWrappingModes.NoWrap;
            hudActorState.enableAutoSizing = true;
            hudActorState.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudActorState.fontSizeMax = 24f;
            AlfaUiFactory.Fill(hudActorState.rectTransform, 16f, 16f, 5f, 20f);
            hudProgress = AlfaUiFactory.ProgressBar(hudStatePanel.transform, "StateProgress", AlfaUiTheme.Lamp400, out var progressTrack);
            Anchor(progressTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-28f, 8f));

            BuildHudHint(view.transform);

            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100);
            hudReticle = reticle.gameObject;
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        /// <summary>
        /// Top-left objective card (UI-06 "OBJETIVO"): the player's character face on a #FFC93C badge, the role and
        /// objective, and the mode bar. Named RoleBadge for the gameplay capture harness.
        /// </summary>
        private void BuildHudObjective(Transform view, Color ink)
        {
            var card = factory.Panel(view, "RoleBadge", ink);
            Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudMargin, -20f), new Vector2(520f, 112f));
            hudRoleBackground = AlfaUiFactory.Node("RolePortrait", card, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)).GetComponent<UnityEngine.UI.Image>();
            hudRoleBackground.sprite = AlfaUiSkin.LargeCircle();
            hudRoleBackground.color = ObjectiveBadge;
            hudRoleBackground.raycastTarget = false;
            Anchor(hudRoleBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(84f, 84f));
            hudRoleFace = AlfaUiFactory.Node("RoleFace", hudRoleBackground.transform, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            hudRoleFace.raycastTarget = false;
            hudRoleFace.enabled = false;
            AlfaUiFactory.Fill(hudRoleFace.rectTransform);
            hudRoleIcon = factory.Icon(hudRoleBackground.transform, "RoleIcon", AlfaUiIconKind.Human, AlfaUiTheme.Ink900);
            AlfaUiFactory.Fill(hudRoleIcon.rectTransform, 18f, 18f, 16f, 20f);
            // The ring sits outside the mask so it is never clipped.
            hudRoleRing = AlfaUiFactory.Node("RoleRing", card, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            hudRoleRing.sprite = AlfaUiSkin.CircleRing();
            hudRoleRing.color = AlfaUiTheme.Hex("E8A21A");
            hudRoleRing.raycastTarget = false;
            Anchor(hudRoleRing.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(88f, 88f));

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
            hudScoreIcon = factory.Icon(bar, "ObjectiveIcon", AlfaUiIconKind.Heart, AlfaUiTheme.TeamMosquito);
            Anchor(hudScoreIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            hudBloodFill = AlfaUiFactory.ProgressBar(bar, "ObjectiveTrack", AlfaUiTheme.TeamMosquito, out var track);
            Anchor(track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(-128f, 16f));
            hudBlood = factory.Text(bar, "ObjectiveValue", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineRight, true);
            hudBlood.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudBlood.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(88f, 30f));

            // Lives (mosquito, tasks/survival) or spectator: "VIDAS" plus one heart per remaining life.
            var livesChip = factory.Panel(view, "LivesChip", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.84f), -1f, -1f, AlfaUiTheme.SmallRadius);
            hudLivesChip = livesChip.gameObject;
            Anchor(livesChip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudMargin, -142f), new Vector2(240f, 44f));
            var livesRow = factory.Horizontal(livesChip, "LivesRow", 8f, TextAnchor.MiddleLeft);
            AlfaUiFactory.Fill(livesRow, 16f, 12f, 4f, 4f);
            hudLives = factory.Text(livesRow, "Lives", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft, true);
            hudLives.textWrappingMode = TextWrappingModes.NoWrap;
            hudLives.characterSpacing = AlfaUiTheme.CaptionTracking;
            hudLives.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            hudLivesHearts = factory.Horizontal(livesRow, "Hearts", 4f, TextAnchor.MiddleLeft);
            for (var i = 0; i < 5; i++)
            {
                var heart = factory.Icon(hudLivesHearts, "Heart" + i, AlfaUiIconKind.Heart, AlfaUiTheme.TeamMosquito);
                var element = heart.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = 24f;
            }
            hudLivesChip.SetActive(false);
        }

        private void BuildHudClock(Transform view, Color ink)
        {
            var clock = factory.Panel(view, "ClockBadge", ink);
            Anchor(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(240f, 98f));
            var clockIcon = factory.Icon(clock, "ClockIcon", AlfaUiIconKind.Clock, AlfaUiTheme.Lamp400);
            Anchor(clockIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(30f, 30f));
            hudClock = factory.Title(clock, "Clock", AlfaUiTheme.Digits("03:00"), 48f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
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
            hudTeamCount = factory.Title(team, "TeamCount", "0", 46f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
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

            hudTaskPanel = factory.Panel(view, "PrivateTask", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.92f)).gameObject;
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
            // UI-06 / UI-01 inventory: four square 80-unit slots on an opaque navy tray, number at the top left
            // (the real keys: 1-3 for the objects, 0 for the hands, which close the row), big pictogram, short
            // resource in the corner; the selected slot keeps the navy fill with a 3-unit #49B2FF frame.
            hudEquipmentPanel = factory.Panel(view, "PrivateEquipment", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.92f)).gameObject;
            hudEquipmentRect = hudEquipmentPanel.GetComponent<RectTransform>();
            AlfaUiFactory.SetSurface(hudEquipmentRect, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f));
            Anchor(hudEquipmentRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(EquipmentWidth, EquipmentBaseHeight));
            for (int i = 0; i < 4; i++)
            {
                var column = i == 0 ? 3 : i - 1;
                var slot = factory.Panel(hudEquipmentPanel.transform, "EquipmentSlotPlate" + i, AlfaUiTheme.PanelInset, -1f, -1f, AlfaUiTheme.SmallRadius);
                Anchor(slot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding + column * (EquipmentSlotSize + EquipmentSlotGap), EquipmentSlotsBottom),
                    new Vector2(EquipmentSlotSize, EquipmentSlotSize));
                AlfaUiFactory.SetSurface(slot, Color.white, Color.white, AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.95f), Color.clear);
                hudEquipmentSlots[i] = slot.GetComponent<UnityEngine.UI.Image>();
                var number = factory.Title(slot, "EquipmentNumber" + i, i.ToString(), 22f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
                number.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -1f), new Vector2(28f, 28f));
                hudEquipmentNumbers[i] = number;
                hudEquipmentIcons[i] = factory.Icon(slot, "EquipmentIcon" + i, i == 0 ? AlfaUiIconKind.Hands : AlfaUiIconKind.None, AlfaUiTheme.Moon200);
                Anchor(hudEquipmentIcons[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 0f), new Vector2(48f, 48f));
                hudEquipmentLabels[i] = factory.Title(slot, "EquipmentSlot" + i, string.Empty, AlfaUiTheme.MinTextSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.BottomRight);
                hudEquipmentLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(hudEquipmentLabels[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-10f, 26f));
            }
            hudEquipmentSelected = factory.Title(hudEquipmentPanel.transform, "EquipmentSelectedLabel", "MANOS", 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            hudEquipmentSelected.textWrappingMode = TextWrappingModes.NoWrap;
            hudEquipmentSelected.enableAutoSizing = true;
            hudEquipmentSelected.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudEquipmentSelected.fontSizeMax = 22f;
            BottomRow(hudEquipmentSelected.rectTransform, EquipmentSlotsBottom + EquipmentSlotSize + 6f, 32f);

            var bolt = factory.Icon(hudEquipmentPanel.transform, "StaminaIcon", AlfaUiIconKind.Bolt, AlfaUiTheme.StatusOk);
            Anchor(bolt.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding, 10f), new Vector2(22f, 22f));
            hudStaminaLabel = factory.Title(hudEquipmentPanel.transform, "StaminaLabel", "100%", AlfaUiTheme.MinTextSize, AlfaUiTheme.StatusOk, TextAlignmentOptions.MidlineLeft);
            hudStaminaLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudStaminaLabel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(EquipmentPadding + 26f, 6f), new Vector2(64f, 30f));
            hudStaminaFill = AlfaUiFactory.ProgressBar(hudEquipmentPanel.transform, "StaminaTrack", AlfaUiTheme.StatusOk, out var staminaTrack);
            Anchor(staminaTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(EquipmentPadding + 94f, 16f),
                new Vector2(-(EquipmentPadding * 2f + 94f), 10f));

            hudThrowTrack = AlfaUiFactory.Node("ThrowCharge", hudEquipmentPanel.transform).gameObject;
            hudThrowRect = hudThrowTrack.GetComponent<RectTransform>();
            BottomRow(hudThrowRect, EquipmentBaseHeight, 50f);
            hudThrowLabel = factory.Title(hudThrowTrack.transform, "ThrowLabel", "CARGA PANTUFLA", AlfaUiTheme.MinTextSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.Left);
            hudThrowLabel.textWrappingMode = TextWrappingModes.NoWrap;
            hudThrowLabel.enableAutoSizing = true;
            hudThrowLabel.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudThrowLabel.fontSizeMax = 22f;
            Anchor(hudThrowLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 32f));
            hudThrowFill = AlfaUiFactory.ProgressBar(hudThrowTrack.transform, "Track", AlfaUiTheme.Lamp400, out var throwTrack);
            Anchor(throwTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 10f));
            hudEquipmentPanel.SetActive(false);

            // One replacement notice only: an opaque chip with a 2-unit #E0393E frame and #FF6B5E text, one line.
            var swap = factory.Panel(view, "SwapOfferChip", AlfaUiTheme.PanelInset, -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(swap, Color.white, Color.white, AlfaUiTheme.TeamMosquito, AlfaUiTheme.WithAlpha(Color.black, 0.4f));
            hudSwapChip = swap.gameObject;
            hudSwapRect = swap;
            hudSwapOffer = factory.Text(swap, "SwapOffer", string.Empty, 22f, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Center, true);
            hudSwapOffer.textWrappingMode = TextWrappingModes.NoWrap;
            hudSwapOffer.enableAutoSizing = true;
            hudSwapOffer.fontSizeMin = AlfaUiTheme.MinTextSize;
            hudSwapOffer.fontSizeMax = 23f;
            AlfaUiFactory.Fill(hudSwapOffer.rectTransform, 18f, 18f, 4f, 4f);
            hudSwapChip.SetActive(false);
        }

        private static void BottomRow(RectTransform rect, float y, float height) =>
            Anchor(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(-EquipmentPadding * 2f, height));

        /// <summary>
        /// Context help. Human: the hint line at the bottom left. Mosquito: the same panel at the bottom right carries
        /// a compact legend of the four flight keys (UI-06 7b) under a header with the current situation in white.
        /// </summary>
        private void BuildHudHint(Transform view)
        {
            hudHintPanel = factory.Panel(view, "ContextHintPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.92f)).gameObject;
            hudHintRect = hudHintPanel.GetComponent<RectTransform>();
            Anchor(hudHintRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(HudMargin, HudMargin), new Vector2(600f, 78f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            hudHint.textWrappingMode = TextWrappingModes.Normal;
            hudHint.enableAutoSizing = false;
            hudHint.overflowMode = TextOverflowModes.Overflow;
            Anchor(hudHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 56f));

            var header = factory.Panel(hudHintPanel.transform, "LegendHeader", AlfaUiTheme.PanelHeader, -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(header, Color.white, Color.white, AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f), Color.clear);
            hudLegendHeader = header.gameObject;
            hudLegendHeaderRect = header;
            hudLegendStatus = factory.Text(header, "LegendStatus", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            hudLegendStatus.textWrappingMode = TextWrappingModes.Normal;
            hudLegendStatus.overflowMode = TextOverflowModes.Overflow;
            AlfaUiFactory.Fill(hudLegendStatus.rectTransform, 14f, 12f, 8f, 8f);
            hudLegendHeader.SetActive(false);

            var legend = factory.Vertical(hudHintPanel.transform, "ControlsLegend", 6f);
            hudLegend = legend.gameObject;
            hudLegendRect = legend;
            Anchor(legend, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-28f, 0f));
            LegendRow(legend, "LegendFly", new[] { "W" }, "Volar");
            LegendRow(legend, "LegendClimb", new[] { "ESPACIO", "CTRL" }, "Subir · bajar");
            LegendRow(legend, "LegendBite", new[] { "E" }, "Picar");
            LegendRow(legend, "LegendPerch", new[] { "F" }, "Posarte");
            hudLegend.SetActive(false);
        }

        private RectTransform LegendRow(Transform parent, string name, string[] keys, string label)
        {
            var row = factory.Horizontal(parent, name, 14f, TextAnchor.MiddleLeft);
            var rowLayout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            rowLayout.minHeight = rowLayout.preferredHeight = LegendRowHeight;
            // Keys right-aligned in their column so each sits next to its label (UI-06 7b legend).
            var keysColumn = factory.Horizontal(row, "Keys", 6f, TextAnchor.MiddleRight);
            var keysLayout = keysColumn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            keysLayout.minWidth = keysLayout.preferredWidth = 154f;
            keysLayout.flexibleWidth = 0f;
            foreach (var key in keys) factory.KeyCap(keysColumn, "Key_" + key, key, 32f);
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
            hudClock.text = AlfaUiTheme.Digits(FormatClock(state.SecondsRemaining));
            hudMode.text = "MODO " + AlfaModeText.Name(state.ModeId);
            PresentHudObjective(state, human);
            PresentHudTeamCounter(state, human);

            hudTaskPanel.SetActive(!state.IsSpectator && !string.IsNullOrWhiteSpace(state.PrivateTaskText));
            hudTask.text = state.PrivateTaskText;
            hudTaskFill.rectTransform.anchorMax = new Vector2(state.TaskProgress01, 1f);
            PresentHudLives(state, human);

            var equipment = state.IsSpectator ? null : state.Equipment;
            hudEquipmentPanel.SetActive(equipment != null);
            var hasSwapOffer = equipment != null && !string.IsNullOrWhiteSpace(equipment.SwapOfferText);
            if (equipment != null) PresentHudEquipment(equipment);
            else hudSwapChip.SetActive(false);
            hudReticle.SetActive(!state.IsSpectator);

            // One replacement notice: while the swap chip is up, its "confirm" prompt would say the same thing.
            var interaction = state.Interaction ?? string.Empty;
            if (hasSwapOffer && interaction.IndexOf("reemplaz", StringComparison.OrdinalIgnoreCase) >= 0) interaction = string.Empty;
            hudInteraction.text = interaction;

            var hint = state.ContextHint ?? string.Empty;
            // Being bitten is shown once, as the central banner; the corner toast never repeats it.
            var bitten = human && (state.ActorState == HudActorState.Bitten || hint.StartsWith("¡Te están picando", StringComparison.Ordinal));
            if (bitten && hint.StartsWith("¡Te están picando", StringComparison.Ordinal)) hint = string.Empty;
            // The mosquito legend already lists the flight keys: only a situational hint is shown, in its header.
            if (!human && hint.StartsWith("W · volar", StringComparison.Ordinal)) hint = string.Empty;
            var legendVisible = !human && !state.IsSpectator;
            hudLegend.SetActive(legendVisible);
            hudLegendStatus.text = legendVisible ? hint : string.Empty;
            hudLegendHeader.SetActive(legendVisible && !string.IsNullOrWhiteSpace(hint));
            hudHint.text = legendVisible ? string.Empty : hint;

            var actorState = state.ActorState == HudActorState.Normal && bitten ? HudActorState.Bitten : state.ActorState;
            hudActorState.text = state.ModeId != GameModes.Blood && actorState == HudActorState.Extracting ? "INTERRUMPIENDO" : ActorStateText(state.Role, actorState);
            hudActorState.color = actorState == HudActorState.Normal ? AlfaUiTheme.Moon200 : actorState == HudActorState.Extracting ? AlfaUiTheme.Lamp400 : AlfaUiTheme.StatusWarn;
            AlfaUiFactory.SetFrame(hudStatePanel.GetComponent<RectTransform>(), actorState == HudActorState.Bitten ? AlfaUiTheme.TeamMosquito : AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f));
            hudProgress.transform.parent.gameObject.SetActive(actorState == HudActorState.Extracting || actorState == HudActorState.Recovering);
            hudProgress.rectTransform.anchorMax = new Vector2(state.StateProgress01, 1f);
            hudNetwork.text = state.NetworkMessage;
            hudPromptPanel.SetActive(!string.IsNullOrWhiteSpace(hudInteraction.text));
            hudStatePanel.SetActive(!string.IsNullOrWhiteSpace(hudActorState.text) ||
                actorState == HudActorState.Extracting || actorState == HudActorState.Recovering);
            hudHintPanel.SetActive(legendVisible || !string.IsNullOrWhiteSpace(hudHint.text));
            LayoutHud();
            if (screen != AlfaUiScreen.Gameplay && screen != AlfaUiScreen.Pause && screen != AlfaUiScreen.Settings)
                ShowGameplay();
        }

        private void PresentHudObjective(BloodHudUiState state, bool human)
        {
            var teamLight = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn;
            hudRoleLabel.text = "<color=#" + ColorUtility.ToHtmlStringRGB(teamLight) + ">" + (human ? "HUMANO" : "MOSQUITO") + "</color>  ·  OBJETIVO";
            PresentHudFace(state.Role);

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
                // Whole units only ("0/18", never "0,2/18"). The human sees the blood still safe as a heart bar that
                // empties as mosquitoes feed (UI-06 "100/100"); the mosquito sees the blood collected filling up.
                var target = Mathf.Max(0, Mathf.RoundToInt(state.BloodTarget));
                var collected = Mathf.Clamp(Mathf.FloorToInt(state.BloodCurrent + 0.0001f), 0, target);
                if (human)
                {
                    text = "Evitá que te piquen";
                    var safe = target - collected;
                    ratio = target > 0 ? Mathf.Clamp01(1f - state.BloodCurrent / state.BloodTarget) : 0f;
                    value = safe + "/" + target;
                    icon = AlfaUiIconKind.Heart;
                }
                else
                {
                    text = "Picá al humano y juntá sangre";
                    ratio = target > 0 ? Mathf.Clamp01(state.BloodCurrent / state.BloodTarget) : 0f;
                    value = collected + "/" + target;
                    icon = AlfaUiIconKind.Blood;
                }
                bar = AlfaUiTheme.TeamMosquito;
            }
            hudObjective.text = text;
            hudBlood.text = AlfaUiTheme.Digits(value);
            hudScoreIcon.Kind = icon;
            hudScoreIcon.color = icon == AlfaUiIconKind.Heart || icon == AlfaUiIconKind.Blood ? AlfaUiTheme.TeamMosquito : Color.Lerp(bar, Color.white, 0.15f);
            var track = hudBloodFill.transform.parent.gameObject;
            track.SetActive(ratio >= 0f);
            AlfaUiFactory.SetBarColor(hudBloodFill, bar);
            hudBloodFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0f, ratio), 1f);
            hudObjectiveBar.SetActive(ratio >= 0f || !string.IsNullOrEmpty(value));
        }

        /// <summary>
        /// The objective badge shows the player's own character face (rendered once per role through the preview
        /// rig) on #FFC93C; without a rig (tests, headless) the role pictogram stands in, in ink on the same disc.
        /// </summary>
        private void PresentHudFace(AlfaRole role)
        {
            if (!hudFaces.TryGetValue(role, out var face))
            {
                face = null;
                var usable = portraitSetup != null && portraitSetup.IsUsable;
                if (usable && screen != AlfaUiScreen.Customization)
                {
                    var rendered = AlfaRolePortrait.RenderHead(portraitSetup, role, 192, ObjectiveBadge);
                    if (rendered != null)
                    {
                        renderedPortraits.Add(rendered);
                        face = rendered;
                    }
                }
                // One attempt per role (a failed render is not retried every HUD refresh).
                if (!usable || screen != AlfaUiScreen.Customization) hudFaces[role] = face;
            }
            hudRoleFace.texture = face;
            hudRoleFace.enabled = face != null;
            hudRoleIcon.gameObject.SetActive(face == null);
            hudRoleIcon.Kind = role == AlfaRole.Human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
        }

        private void PresentHudLives(BloodHudUiState state, bool human)
        {
            var lives = !state.IsSpectator && !human && state.ModeId != GameModes.Blood ? Mathf.Clamp(state.LivesRemaining, 0, 5) : -1;
            hudLives.text = state.IsSpectator ? "ESPECTADOR" : lives >= 0 ? "VIDAS " + lives : string.Empty;
            for (var i = 0; i < hudLivesHearts.childCount; i++) hudLivesHearts.GetChild(i).gameObject.SetActive(i < lives);
            hudLivesHearts.gameObject.SetActive(lives > 0);
            hudLivesChip.SetActive(!string.IsNullOrEmpty(hudLives.text));
            if (!hudLivesChip.activeSelf) return;
            var width = 16f + hudLives.GetPreferredValues(hudLives.text).x + 12f + (lives > 0 ? lives * 28f : 0f) + 8f;
            ((RectTransform)hudLivesChip.transform).sizeDelta = new Vector2(Mathf.Max(160f, width), 44f);
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
            hudTeamCount.text = AlfaUiTheme.Digits(total > 0 ? Mathf.Max(0, count) + "/" + total : count.ToString());
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
                hudEquipmentNumbers[i].color = selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.Moon200;
                hudEquipmentLabels[i].color = AlfaUiTheme.Lamp400;
                if (!selected) continue;
                var name = i == 0 ? "MANOS" : slot.Label;
                var resource = i == 0 ? "SIN OBJETO" : slot.ResourceText;
                hudEquipmentSelected.text = name + (string.IsNullOrEmpty(resource) ? string.Empty : "  <color=#A8B8D8>·  " + resource + "</color>");
            }
            var stamina = Mathf.RoundToInt(equipment.Stamina01 * 100);
            var staminaColor = equipment.Stamina01 < 0.25f ? AlfaUiTheme.StatusWarn : AlfaUiTheme.StatusOk;
            hudStaminaLabel.text = AlfaUiTheme.Digits(stamina + "%");
            hudStaminaLabel.color = staminaColor;
            hudEquipmentPanel.transform.Find("StaminaIcon").GetComponent<AlfaUiIcon>().color = staminaColor;
            AlfaUiFactory.SetBarColor(hudStaminaFill, staminaColor);
            hudStaminaFill.rectTransform.anchorMax = new Vector2(equipment.Stamina01, 1f);
            bool charging = equipment.ThrowCharge01 > 0 || equipment.ThrowAwaitingRelease;
            hudThrowTrack.SetActive(charging);
            hudThrowLabel.text = equipment.ThrowAwaitingRelease ? "LANZAMIENTO PENDIENTE" : "CARGA PANTUFLA  " + Mathf.RoundToInt(equipment.ThrowCharge01 * 100) + "% · SOLTÁ CLIC";
            hudThrowFill.rectTransform.anchorMax = new Vector2(equipment.ThrowCharge01, 1f);
            var hasSwapOffer = !string.IsNullOrWhiteSpace(equipment.SwapOfferText);
            // Bootstrap sends "E · REEMPLAZAR\n<A> POR <B>": one line on the chip.
            hudSwapOffer.text = hasSwapOffer ? equipment.SwapOfferText.Replace("\r", string.Empty).Replace("\n", " ").Trim() : string.Empty;
            hudSwapChip.SetActive(hasSwapOffer);
            UpdateEquipmentLayout(charging);
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

        private void UpdateEquipmentLayout(bool charging)
        {
            var nextTop = EquipmentBaseHeight;
            if (charging)
            {
                BottomRow(hudThrowRect, nextTop - 4f, 50f);
                nextTop += 50f;
            }
            hudEquipmentRect.sizeDelta = new Vector2(EquipmentWidth, nextTop);
        }

        /// <summary>Equipment slot plate: opaque inset navy; selected = #1E3358 with the 3-unit #49B2FF frame.</summary>
        private void SetEquipmentSlotSelected(int index, bool selected)
        {
            var plate = hudEquipmentSlots[index];
            if (plate == null) return;
            plate.color = selected ? AlfaUiTheme.Night600 : AlfaUiTheme.PanelInset;
            AlfaUiFactory.SetSurface(plate, Color.white, Color.white,
                selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.95f), Color.clear);
            AlfaUiFactory.MarkSelectedFrame(plate, selected);
            if (selected) AlfaUiFactory.SetSurface(plate, shadow: AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, 0.35f));
        }

        /// <summary>
        /// Places the bottom widgets from the canvas width: the swap chip, prompt and actor state stack above the
        /// tray (or low in the centre without one), the hint/legend panel takes the corner and never reaches the centre.
        /// </summary>
        private void LayoutHud()
        {
            if (hudView == null) return;
            var width = hudView.rect.width;
            if (width <= 1f) return;
            hudLayoutWidth = width;
            // The network line sits under the clock and must stay clear of the objective card and the right stack.
            hudNetwork.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(width - 2f * (HudMargin + 520f + 16f), 360f, 640f), 34f);
            var belt = hudEquipmentPanel.activeSelf;
            var promptWidth = Mathf.Clamp(width - 2f * (HudMargin + LegendWidth + 16f), 420f, PromptMaxWidth);
            var nextY = belt ? 18f + hudEquipmentRect.sizeDelta.y + 12f : 30f;
            if (hudSwapChip.activeSelf)
            {
                var chipWidth = Mathf.Min(promptWidth, Mathf.Max(EquipmentWidth, hudSwapOffer.GetPreferredValues(hudSwapOffer.text).x + 40f));
                Anchor(hudSwapRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, nextY), new Vector2(chipWidth, 50f));
                nextY += 60f;
            }
            Anchor(hudPromptRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, nextY), new Vector2(promptWidth, 56f));
            var stateY = nextY + (hudPromptPanel.activeSelf ? 66f : 0f);
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, stateY), new Vector2(Mathf.Min(520f, promptWidth), 64f));

            var legend = hudLegend.activeSelf;
            if (legend)
            {
                // Mosquito: header (situation, white) over the four-row legend, bottom right.
                var textWidth = LegendWidth - 26f;
                var headerHeight = 0f;
                if (hudLegendHeader.activeSelf)
                {
                    headerHeight = Mathf.Ceil(hudLegendStatus.GetPreferredValues(hudLegendStatus.text, textWidth, 0f).y) + 16f;
                    Anchor(hudLegendHeaderRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-12f, headerHeight));
                }
                var legendRows = 0;
                foreach (Transform row in hudLegendRect) if (row.gameObject.activeSelf) legendRows++;
                var legendHeight = legendRows * LegendRowHeight + Mathf.Max(0, legendRows - 1) * 6f;
                hudLegendRect.sizeDelta = new Vector2(-28f, legendHeight);
                hudHint.gameObject.SetActive(false);
                var height = 24f + legendHeight + (headerHeight > 0f ? headerHeight + 12f : 0f);
                Anchor(hudHintRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-HudMargin, HudMargin), new Vector2(LegendWidth, height));
                return;
            }
            var hasHint = !string.IsNullOrWhiteSpace(hudHint.text);
            var centreHalf = belt ? EquipmentWidth * 0.5f : promptWidth * 0.5f;
            var panelWidth = Mathf.Clamp(width * 0.5f - centreHalf - HudMargin - 16f, 280f, 600f);
            var hintHeight = hasHint ? Mathf.Ceil(hudHint.GetPreferredValues(hudHint.text, panelWidth - 32f, 0f).y) + 2f : 0f;
            hudHint.gameObject.SetActive(hasHint);
            Anchor(hudHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, hintHeight));
            Anchor(hudHintRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(HudMargin, HudMargin), new Vector2(panelWidth, Mathf.Max(24f + hintHeight, 60f)));
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
                case HudActorState.Bitten: return role == AlfaRole.Human ? BittenBanner : string.Empty;
                case HudActorState.Recovering: return "RECUPERANDO…";
                case HudActorState.Fainted: return "DESMAYADO";
                case HudActorState.Stunned: return "ATURDIDO";
                case HudActorState.Attached: return "[E] DESPRENDERTE";
                default: return string.Empty;
            }
        }
    }
}
