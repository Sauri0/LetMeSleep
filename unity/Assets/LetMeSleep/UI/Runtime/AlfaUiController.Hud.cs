using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LetMeSleep.Core;
using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Gameplay HUD (UI-06 screens 7 and 7b, UI-01). Top: the objective card (the character's face on a #FFC93C badge,
    /// role, objective and bar) on an opaque #15264A plate with #F2F6FF text so it reads over light skies; the clock
    /// on a plate at the top centre (tabular figures); the opposing team counter at the top right. Bottom centre (stage
    /// 3): only the four loose 80-unit slots (number at the top left, the selected one framed #49B2FF 3 units) with a
    /// slim stamina line under them, and at most ONE banner above them (actor state, else the slipper charge while the
    /// slipper is in hand, else the name of a freshly selected object for a moment). Being bitten is a red vignette on
    /// the screen edges (#E0393E at 25 %) plus a small chip under the crosshair; the interaction prompt is a chip with
    /// its key caps to the right of the crosshair (UI-01 "E Pick Up"), in the banner's type and height, and the swap
    /// offer uses that same chip. Mosquito: one 340-unit panel at the bottom right with the situation as its white
    /// header and the four-row key legend (keys right-aligned in a fixed 150-unit column); no repeated "Mantené E"
    /// pill. Everything else the alpha HUD showed is kept (scores, lives, private task, network and voice state). The
    /// layout is recomputed from the canvas width so nothing overlaps at 16:10, 5:4 or 21:9. Names used by the capture
    /// harness are kept: RoleBadge, ClockBadge, ContextHintPanel and PrivateEquipment under GameplayHudView.
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const float HudMargin = 24f;
        private const float EquipmentSlotSize = 80f;
        private const float EquipmentSlotGap = 10f;
        private const float EquipmentWidth = EquipmentSlotSize * 4f + EquipmentSlotGap * 3f;
        private const float EquipmentStaminaHeight = 22f;
        private const float EquipmentSlotsBottom = EquipmentStaminaHeight + 8f;
        private const float EquipmentHeight = EquipmentSlotsBottom + EquipmentSlotSize;
        private const float EquipmentBottom = 16f;
        private const float LegendWidth = 340f;
        private const float LegendRowHeight = 34f;
        private const float LegendKeyColumn = 150f;
        private const float BannerHeight = 50f;
        private const float BannerProgressHeight = 62f;
        private const float BannerMaxWidth = 620f;
        private const float HudChipTextSize = 22f;
        private const float SelectionToastSeconds = 2.5f;
        private const string BittenChipText = "¡TE ESTÁN PICANDO!";
        private const float BittenVignetteAlpha = 0.3f;
        private static readonly Color ObjectiveBadge = AlfaUiTheme.Lamp400;
        private static readonly Regex KeyToken = new Regex(@"^(?:(Mantené|Mantener)\s+)?([A-ZÑ0-9]{1,3}|Tab|TAB|Clic|CLIC|Espacio|ESPACIO)$");

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
        // Look each cached face was made for; empty = the default character (the painted portrait serves).
        private readonly Dictionary<AlfaRole, string> hudFaceLooks = new Dictionary<AlfaRole, string>();
        private readonly Dictionary<AlfaRole, string> localLooks = new Dictionary<AlfaRole, string>();

        /// <summary>
        /// The local player's published look per role, as an opaque key (empty for the default character). A custom
        /// look makes the objective badge render the player's own head instead of the painted default portrait.
        /// </summary>
        public void SetLocalLook(string humanLook, string mosquitoLook)
        {
            localLooks[AlfaRole.Human] = humanLook ?? string.Empty;
            localLooks[AlfaRole.Mosquito] = mosquitoLook ?? string.Empty;
        }
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
        private TextMeshProUGUI hudStaminaLabel;
        private UnityEngine.UI.Image hudStaminaFill;
        private GameObject hudBittenVignette;
        private GameObject hudBittenChip;
        private readonly RectTransform[] hudPromptKeys = new RectTransform[2];
        private readonly TextMeshProUGUI[] hudPromptKeyLabels = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] hudPromptLabels = new TextMeshProUGUI[2];
        private int hudSelectedSlot = int.MinValue;
        private float hudSelectedSince = -100f;

        private void BuildHud()
        {
            var view = factory.View("GameplayHudView", transform, false);
            screens[AlfaUiScreen.Gameplay] = view;
            hudView = (RectTransform)view.transform;

            // Being bitten: red edges (#E0393E at 30 % on the screen edge, down to 0 % at 35 % of the radius), behind
            // everything else of the HUD.
            var vignette = AlfaUiFactory.Node("BittenVignette", view.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            vignette.sprite = AlfaUiFactory.EdgeVignetteSprite();
            vignette.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.TeamMosquito, BittenVignetteAlpha);
            vignette.raycastTarget = false;
            AlfaUiFactory.Fill(vignette.rectTransform);
            hudBittenVignette = vignette.gameObject;
            hudBittenVignette.SetActive(false);

            // Top plates: opaque #15264A with #F2F6FF text (they sit over bright skies).
            var plate = AlfaUiTheme.Night700;
            BuildHudObjective(view.transform, plate);
            BuildHudClock(view.transform, plate);
            BuildHudTeamCounter(view.transform, plate);
            BuildHudSideStatus(view.transform);
            BuildHudEquipment(view.transform);
            BuildHudPrompt(view.transform);
            BuildHudBanner(view.transform);

            var bitten = factory.Panel(view.transform, "BittenChip", AlfaUiTheme.PanelInset, -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(bitten, Color.white, Color.white, AlfaUiTheme.TeamMosquito, AlfaUiTheme.WithAlpha(Color.black, 0.4f));
            Anchor(bitten, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(300f, 44f));
            var bittenText = factory.Title(bitten, "BittenLabel", BittenChipText, HudChipTextSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Center);
            bittenText.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(bittenText.rectTransform, 14f, 14f, 2f, 2f);
            bitten.sizeDelta = new Vector2(bittenText.GetPreferredValues(BittenChipText).x + 40f, 44f);
            hudBittenChip = bitten.gameObject;
            hudBittenChip.SetActive(false);

            BuildHudHint(view.transform);

            var reticle = factory.Icon(view.transform, "Reticle", AlfaUiIconKind.Crosshair, AlfaUiTheme.Sheet100);
            hudReticle = reticle.gameObject;
            Anchor(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        /// <summary>
        /// Top-left objective card (UI-06 "OBJETIVO"): the player's character face on a #FFC93C badge, the role and
        /// objective, and the mode bar. Named RoleBadge for the gameplay capture harness.
        /// </summary>
        private void BuildHudObjective(Transform view, Color plate)
        {
            var card = factory.Panel(view, "RoleBadge", plate);
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

            hudRoleLabel = factory.Text(card, "RoleLabel", "HUMANO", AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft, true);
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
            var livesChip = factory.Panel(view, "LivesChip", AlfaUiTheme.Night700, -1f, -1f, AlfaUiTheme.SmallRadius);
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

        private void BuildHudClock(Transform view, Color plate)
        {
            var clock = factory.Panel(view, "ClockBadge", plate);
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

        private void BuildHudTeamCounter(Transform view, Color plate)
        {
            var team = factory.Panel(view, "TeamCounter", plate);
            hudTeamPanel = team.gameObject;
            Anchor(team, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HudMargin, -20f), new Vector2(270f, 98f));
            hudTeamIcon = factory.Icon(team, "TeamIcon", AlfaUiIconKind.Mosquito, AlfaUiTheme.Pajama500);
            Anchor(hudTeamIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(68f, 68f));
            hudTeamCaption = factory.Caption(team, "TeamCaption", "MOSQUITOS");
            hudTeamCaption.color = AlfaUiTheme.Sheet100;
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
            // UI-06 / UI-01 inventory: four loose, square 80-unit slots (no tray), number at the top left (the real
            // keys: 1-3 for the objects, 0 for the hands, which close the row), big pictogram, short resource in the
            // corner; the selected slot keeps the navy fill with a 3-unit #49B2FF frame. A slim stamina line under them.
            hudEquipmentPanel = factory.Panel(view, "PrivateEquipment", Color.clear).gameObject;
            hudEquipmentRect = hudEquipmentPanel.GetComponent<RectTransform>();
            AlfaUiFactory.SetSurface(hudEquipmentRect, frame: Color.clear, shadow: Color.clear);
            Anchor(hudEquipmentRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, EquipmentBottom), new Vector2(EquipmentWidth, EquipmentHeight));
            for (int i = 0; i < 4; i++)
            {
                var column = i == 0 ? 3 : i - 1;
                var slot = factory.Panel(hudEquipmentPanel.transform, "EquipmentSlotPlate" + i, AlfaUiTheme.PanelInset, -1f, -1f, AlfaUiTheme.SmallRadius);
                Anchor(slot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(column * (EquipmentSlotSize + EquipmentSlotGap), EquipmentSlotsBottom),
                    new Vector2(EquipmentSlotSize, EquipmentSlotSize));
                AlfaUiFactory.SetSurface(slot, Color.white, Color.white, AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.95f), AlfaUiTheme.WithAlpha(Color.black, 0.4f));
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

            // The stamina line gets its own small opaque strip: without a tray it would sit on the bare scene.
            var staminaPlate = factory.Panel(hudEquipmentPanel.transform, "StaminaPlate", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.92f), -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(staminaPlate, Color.white, Color.white, AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.7f), AlfaUiTheme.WithAlpha(Color.black, 0.3f));
            Anchor(staminaPlate, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -2f), new Vector2(8f, EquipmentStaminaHeight + 4f));
            var bolt = factory.Icon(hudEquipmentPanel.transform, "StaminaIcon", AlfaUiIconKind.Bolt, AlfaUiTheme.StatusOk);
            Anchor(bolt.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(6f, 0f), new Vector2(22f, 22f));
            hudStaminaFill = AlfaUiFactory.ProgressBar(hudEquipmentPanel.transform, "StaminaTrack", AlfaUiTheme.StatusOk, out var staminaTrack);
            Anchor(staminaTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(34f, 7f), new Vector2(-(34f + 70f), 8f));
            hudStaminaLabel = factory.Title(hudEquipmentPanel.transform, "StaminaLabel", "100%", AlfaUiTheme.MinTextSize, AlfaUiTheme.StatusOk, TextAlignmentOptions.MidlineRight);
            hudStaminaLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(hudStaminaLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6f, -4f), new Vector2(64f, 30f));
            hudEquipmentPanel.SetActive(false);
        }

        /// <summary>
        /// Interaction chip to the right of the crosshair (UI-01 "E Pick Up"): up to two key caps with their actions,
        /// same type and height as the banner. Named InteractionPrompt.
        /// </summary>
        private void BuildHudPrompt(Transform view)
        {
            var prompt = factory.Panel(view, "InteractionPrompt", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f), -1f, -1f, AlfaUiTheme.SmallRadius);
            hudPromptPanel = prompt.gameObject;
            hudPromptRect = prompt;
            Anchor(prompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(300f, BannerHeight));
            var row = prompt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.padding = new RectOffset(9, 18, 9, 9);
            row.spacing = 12f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = prompt.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            for (var i = 0; i < 2; i++)
            {
                var cap = factory.KeyCap(prompt, "PromptKey" + i, "E", 32f, 14f);
                hudPromptKeys[i] = cap;
                hudPromptKeyLabels[i] = cap.Find("Key").GetComponent<TextMeshProUGUI>();
                var label = factory.Title(prompt, i == 0 ? "Interaction" : "Interaction" + i, string.Empty, HudChipTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
                hudPromptLabels[i] = label;
            }
            hudPromptPanel.SetActive(false);
        }

        /// <summary>The one banner above the slots (or low in the centre): actor state, slipper charge or object name.</summary>
        private void BuildHudBanner(Transform view)
        {
            hudStatePanel = factory.Panel(view, "ActorStatePanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f), -1f, -1f, AlfaUiTheme.SmallRadius).gameObject;
            hudStateRect = hudStatePanel.GetComponent<RectTransform>();
            AlfaUiFactory.SetSurface(hudStateRect, frame: AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f));
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(460f, BannerHeight));
            hudActorState = factory.Title(hudStatePanel.transform, "ActorState", string.Empty, HudChipTextSize, AlfaUiTheme.StatusWarn, TextAlignmentOptions.Center);
            hudActorState.textWrappingMode = TextWrappingModes.NoWrap;
            hudActorState.richText = true;
            Anchor(hudActorState.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-32f, 42f));
            hudProgress = AlfaUiFactory.ProgressBar(hudStatePanel.transform, "StateProgress", AlfaUiTheme.Lamp400, out var progressTrack);
            Anchor(progressTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-32f, 8f));
            hudStatePanel.SetActive(false);
        }

        private static void BottomRow(RectTransform rect, float y, float height) =>
            Anchor(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(0f, height));

        /// <summary>
        /// Context help. Human: the hint line at the bottom left. Mosquito: one panel at the bottom right with the
        /// current situation as its white header (flush, over a thin divider) and the compact legend of the four
        /// flight keys (UI-06 7b).
        /// </summary>
        private void BuildHudHint(Transform view)
        {
            hudHintPanel = factory.Panel(view, "ContextHintPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.94f)).gameObject;
            hudHintRect = hudHintPanel.GetComponent<RectTransform>();
            Anchor(hudHintRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(HudMargin, HudMargin), new Vector2(600f, 78f));
            hudHint = factory.Text(hudHintPanel.transform, "ContextHint", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            hudHint.textWrappingMode = TextWrappingModes.Normal;
            hudHint.enableAutoSizing = false;
            hudHint.overflowMode = TextOverflowModes.Overflow;
            Anchor(hudHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 56f));

            // Header of the same panel (no inset box of its own): white situation text over a thin divider.
            var header = AlfaUiFactory.Node("LegendHeader", hudHintPanel.transform).GetComponent<RectTransform>();
            hudLegendHeader = header.gameObject;
            hudLegendHeaderRect = header;
            hudLegendStatus = factory.Text(header, "LegendStatus", string.Empty, 22f, AlfaUiTheme.Sheet100, TextAlignmentOptions.TopLeft);
            hudLegendStatus.textWrappingMode = TextWrappingModes.Normal;
            hudLegendStatus.overflowMode = TextOverflowModes.Overflow;
            AlfaUiFactory.Fill(hudLegendStatus.rectTransform, 0f, 0f, 0f, 10f);
            var divider = AlfaUiFactory.Node("LegendDivider", header, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            divider.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.8f);
            divider.raycastTarget = false;
            Anchor(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1f));
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
            // Keys right-aligned in a fixed 150-unit column so every label starts at the same x (UI-06 7b legend).
            var keysColumn = factory.Horizontal(row, "Keys", 6f, TextAnchor.MiddleRight);
            var keysLayout = keysColumn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            keysLayout.minWidth = keysLayout.preferredWidth = LegendKeyColumn;
            keysLayout.flexibleWidth = 0f;
            foreach (var key in keys) factory.KeyCap(keysColumn, "Key_" + key, key, 32f, keys.Length > 1 ? 12f : 20f);
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
            if (equipment != null) PresentHudEquipment(equipment);
            else hudSelectedSlot = int.MinValue;
            hudReticle.SetActive(!state.IsSpectator);

            var hint = state.ContextHint ?? string.Empty;
            // Being bitten: red vignette + a chip under the crosshair; the corner toast never repeats it.
            var bitten = human && !state.IsSpectator && (state.ActorState == HudActorState.Bitten || hint.StartsWith("¡Te están picando", StringComparison.Ordinal));
            if (bitten && hint.StartsWith("¡Te están picando", StringComparison.Ordinal)) hint = string.Empty;
            hudBittenVignette.SetActive(bitten);
            hudBittenChip.SetActive(bitten);
            // The mosquito legend already lists the flight keys: only a situational hint is shown, in its header,
            // without the "Mantené E" the legend already says (E · Picar).
            if (!human && hint.StartsWith("W · volar", StringComparison.Ordinal)) hint = string.Empty;
            if (!human && !state.IsSpectator && hint.Length > 0)
                hint = string.Join(" · ", hint.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries).Where(part => part.Trim() != "Mantené E"));
            var legendVisible = !human && !state.IsSpectator;
            hudLegend.SetActive(legendVisible);
            // The header breaks at each " · " so no line ends with a dangling dot ("Interrumpiendo al humano" /
            // "Soltá E para despegar").
            hudLegendStatus.text = legendVisible ? string.Join("\n", hint.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim())) : string.Empty;
            hudLegendHeader.SetActive(legendVisible && !string.IsNullOrWhiteSpace(hint));
            hudHint.text = legendVisible ? string.Empty : hint;

            PresentHudPrompt(state, equipment, legendVisible);
            PresentHudBanner(state, equipment, human);
            hudNetwork.text = state.NetworkMessage;
            hudHintPanel.SetActive(legendVisible || !string.IsNullOrWhiteSpace(hudHint.text));
            LayoutHud();
            if (screen != AlfaUiScreen.Gameplay && screen != AlfaUiScreen.Pause && screen != AlfaUiScreen.Settings)
                ShowGameplay();
        }

        /// <summary>
        /// The interaction chip: the swap offer when there is one ("E Reemplazar matamoscas por pantufla"), else the
        /// game's prompt split into key caps and actions. The mosquito's E prompts are dropped while its legend lists E.
        /// </summary>
        private void PresentHudPrompt(BloodHudUiState state, EquipmentHudUiState equipment, bool legendVisible)
        {
            var text = state.Interaction ?? string.Empty;
            var swap = equipment != null && !string.IsNullOrWhiteSpace(equipment.SwapOfferText)
                ? equipment.SwapOfferText.Replace("\r", string.Empty).Replace("\n", " ").Trim() : string.Empty;
            if (swap.Length > 0) text = swap;
            var parts = ParsePrompt(text);
            if (legendVisible && parts.Exists(part => part.key == "E")) parts.Clear();
            // The chip always has the panel border (#3B5E9C); red is only for "¡TE ESTÁN PICANDO!".
            for (var i = 0; i < 2; i++)
            {
                var used = i < parts.Count;
                var keyed = used && !string.IsNullOrEmpty(parts[i].key);
                hudPromptKeys[i].gameObject.SetActive(keyed);
                if (keyed)
                {
                    hudPromptKeyLabels[i].text = parts[i].key;
                    var capLayout = hudPromptKeys[i].GetComponent<UnityEngine.UI.LayoutElement>();
                    capLayout.minWidth = capLayout.preferredWidth = Mathf.Max(32f, hudPromptKeyLabels[i].GetPreferredValues(parts[i].key).x + 14f);
                }
                hudPromptLabels[i].gameObject.SetActive(used);
                hudPromptLabels[i].text = used ? parts[i].label : string.Empty;
            }
            AlfaUiFactory.SetFrame(hudPromptRect, AlfaUiTheme.Border);
            hudPromptPanel.SetActive(parts.Count > 0);
        }

        /// <summary>
        /// "E · Abrir / cerrar puerta" -> [E] Abrir / cerrar puerta; "E · Recoger objeto · G · soltar equipado" ->
        /// [E] Recoger objeto [G] Soltar equipado; "Mantené E · Trabajar" -> [E] Mantené: trabajar; "E · REEMPLAZAR
        /// MATAMOSCAS POR PANTUFLA" -> [E] Reemplazar matamoscas por pantufla; text without keys stays one label.
        /// </summary>
        internal static List<(string key, string label)> ParsePrompt(string text)
        {
            var result = new List<(string key, string label)>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var tokens = text.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length && result.Count < 2; i++)
            {
                var token = tokens[i].Trim();
                var match = KeyToken.Match(token);
                if (match.Success && i + 1 < tokens.Length)
                {
                    var label = Sentence(tokens[i + 1].Trim());
                    if (match.Groups[1].Success) label = "Mantené: " + char.ToLowerInvariant(label[0]) + label.Substring(1);
                    result.Add((match.Groups[2].Value.ToUpperInvariant(), label));
                    i++;
                    continue;
                }
                // Anything else is plain text; the rest of the line joins it.
                result.Add((string.Empty, Sentence(string.Join(" · ", tokens, i, tokens.Length - i))));
                break;
            }
            return result;
        }

        /// <summary>"REEMPLAZAR MATAMOSCAS" -> "Reemplazar matamoscas"; mixed-case text is kept.</summary>
        private static string Sentence(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value != value.ToUpperInvariant()) return char.ToUpperInvariant(value[0]) + value.Substring(1);
            var lower = value.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        /// <summary>
        /// The single banner above the inventory: the actor state first, then the slipper charge (only while the
        /// slipper is the object in hand), then the name of a newly selected object for a moment.
        /// </summary>
        private void PresentHudBanner(BloodHudUiState state, EquipmentHudUiState equipment, bool human)
        {
            var actorState = state.ActorState == HudActorState.Bitten ? HudActorState.Normal : state.ActorState;
            var text = state.ModeId != GameModes.Blood && actorState == HudActorState.Extracting ? "INTERRUMPIENDO" : ActorStateText(state.Role, actorState);
            var color = actorState == HudActorState.Extracting ? AlfaUiTheme.Lamp400 : AlfaUiTheme.StatusWarn;
            var progress = actorState == HudActorState.Extracting || actorState == HudActorState.Recovering ? state.StateProgress01 : -1f;
            var frame = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f);
            if (string.IsNullOrEmpty(text) && human && equipment != null)
            {
                var selected = equipment.SelectedSlot >= 0 && equipment.SelectedSlot < equipment.Slots.Count ? equipment.Slots[equipment.SelectedSlot] : null;
                var charging = equipment.ThrowCharge01 > 0f || equipment.ThrowAwaitingRelease;
                if (charging && selected != null && selected.Icon == AlfaUiIconKind.Slipper)
                {
                    text = equipment.ThrowAwaitingRelease ? "LANZAMIENTO PENDIENTE" : "CARGA PANTUFLA  " + Mathf.RoundToInt(equipment.ThrowCharge01 * 100f) + " %  ·  SOLTÁ CLIC";
                    color = AlfaUiTheme.Lamp400;
                    progress = equipment.ThrowCharge01;
                }
                else if (Time.unscaledTime - hudSelectedSince < SelectionToastSeconds)
                {
                    var name = selected != null ? selected.Label : "MANOS";
                    var resource = selected != null ? selected.ResourceText : "SIN OBJETO";
                    text = name + (string.IsNullOrEmpty(resource) ? string.Empty : "  <color=#A8B8D8>·  " + resource + "</color>");
                    color = AlfaUiTheme.Sheet100;
                }
            }
            hudActorState.text = AlfaUiTheme.Digits(text);
            hudActorState.color = color;
            AlfaUiFactory.SetFrame(hudStateRect, frame);
            var hasBar = progress >= 0f;
            hudProgress.transform.parent.gameObject.SetActive(hasBar);
            hudProgress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            AlfaUiFactory.SetBarColor(hudProgress, color == AlfaUiTheme.Sheet100 ? AlfaUiTheme.Lamp400 : color);
            hudStatePanel.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private void PresentHudObjective(BloodHudUiState state, bool human)
        {
            // Opaque #15264A plate: the role reads in #F2F6FF over any sky.
            hudRoleLabel.text = (human ? "HUMANO" : "MOSQUITO") + "  ·  OBJETIVO";
            hudRoleLabel.color = AlfaUiTheme.Sheet100;
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
        /// The objective badge shows the character's face on the 84-unit #FFC93C disc: a crop of the rendered v0.3
        /// character (Resources/AlfaUiPortraits: the human's head with the red nightcap and its pompom and the big
        /// eyes; the mosquito's eyes and head), else a head shot rendered once through the preview rig; without either
        /// (tests, headless) the role pictogram stands in, in ink on the same disc.
        /// </summary>
        private void PresentHudFace(AlfaRole role)
        {
            var look = localLooks.TryGetValue(role, out var chosenLook) ? chosenLook ?? string.Empty : string.Empty;
            if (hudFaces.ContainsKey(role) && (!hudFaceLooks.TryGetValue(role, out var builtLook) || builtLook != look))
                hudFaces.Remove(role);
            if (!hudFaces.TryGetValue(role, out var face))
            {
                // A custom look is always rendered (dressed like the player); the painted art is the default one.
                face = look.Length == 0 ? LoadRoleArt(role, string.Empty) : null;
                var remember = true;
                if (face == null)
                {
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
                    // The rig is busy while customizing: try again later. Otherwise one attempt per role.
                    remember = !usable || screen != AlfaUiScreen.Customization;
                }
                if (remember) { hudFaces[role] = face; hudFaceLooks[role] = look; }
            }
            var painted = face != null && face == LoadRoleArt(role, string.Empty);
            hudRoleFace.texture = face;
            hudRoleFace.uvRect = painted ? AlfaUiArt.FaceRect(face, role) : new Rect(0f, 0f, 1f, 1f);
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
            if (equipment.SelectedSlot != hudSelectedSlot)
            {
                hudSelectedSlot = equipment.SelectedSlot;
                hudSelectedSince = Time.unscaledTime;
            }
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
            }
            var stamina = Mathf.RoundToInt(equipment.Stamina01 * 100);
            var staminaColor = equipment.Stamina01 < 0.25f ? AlfaUiTheme.StatusWarn : AlfaUiTheme.StatusOk;
            hudStaminaLabel.text = AlfaUiTheme.Digits(stamina + "%");
            hudStaminaLabel.color = staminaColor;
            hudEquipmentPanel.transform.Find("StaminaIcon").GetComponent<AlfaUiIcon>().color = staminaColor;
            AlfaUiFactory.SetBarColor(hudStaminaFill, staminaColor);
            hudStaminaFill.rectTransform.anchorMax = new Vector2(equipment.Stamina01, 1f);
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

        /// <summary>Equipment slot plate: opaque inset navy; selected = #1E3358 with the 3-unit #49B2FF frame.</summary>
        private void SetEquipmentSlotSelected(int index, bool selected)
        {
            var plate = hudEquipmentSlots[index];
            if (plate == null) return;
            plate.color = selected ? AlfaUiTheme.Night600 : AlfaUiTheme.PanelInset;
            AlfaUiFactory.SetSurface(plate, Color.white, Color.white,
                selected ? AlfaUiTheme.Sky400 : AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.95f), AlfaUiTheme.WithAlpha(Color.black, 0.4f));
            AlfaUiFactory.MarkSelectedFrame(plate, selected);
            if (selected) AlfaUiFactory.SetSurface(plate, shadow: AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, 0.35f));
        }

        /// <summary>
        /// Places the bottom widgets from the canvas width: the one banner sits over the slots (or low in the centre
        /// without them), the interaction chip right of the crosshair never passes the right margin, and the
        /// hint/legend panel takes a corner and never reaches the centre.
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
            var bannerMax = Mathf.Clamp(width - 2f * (HudMargin + LegendWidth + 16f), 360f, BannerMaxWidth);
            var hasBar = hudProgress.transform.parent.gameObject.activeSelf;
            var bannerHeight = hasBar ? BannerProgressHeight : BannerHeight;
            var bannerWidth = Mathf.Clamp(hudActorState.GetPreferredValues(hudActorState.text).x + 48f, belt ? EquipmentWidth : 300f, bannerMax);
            var bannerY = belt ? EquipmentBottom + EquipmentHeight + 12f : 30f;
            Anchor(hudStateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bannerY), new Vector2(bannerWidth, bannerHeight));
            // Text centred in the upper 42 units; the progress line under it when there is one.
            Anchor(hudActorState.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, hasBar ? -4f : -4f), new Vector2(-32f, 42f));

            // Interaction chip: left edge 40 units right of the crosshair, clamped to the right margin.
            var maxPrompt = width * 0.5f - 40f - HudMargin;
            for (var i = 0; i < 2; i++)
            {
                var label = hudPromptLabels[i];
                var element = label.GetComponent<UnityEngine.UI.LayoutElement>();
                element.preferredWidth = label.gameObject.activeSelf ? Mathf.Min(label.GetPreferredValues(label.text).x + 2f, maxPrompt - 80f) : -1f;
            }

            var legend = hudLegend.activeSelf;
            if (legend)
            {
                // Mosquito: one panel, the situation as its white header over a divider, then the four-row legend.
                var textWidth = LegendWidth - 28f;
                var headerHeight = 0f;
                if (hudLegendHeader.activeSelf)
                {
                    headerHeight = Mathf.Ceil(hudLegendStatus.GetPreferredValues(hudLegendStatus.text, textWidth, 0f).y) + 12f;
                    Anchor(hudLegendHeaderRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-28f, headerHeight));
                }
                var legendRows = 0;
                foreach (Transform row in hudLegendRect) if (row.gameObject.activeSelf) legendRows++;
                var legendHeight = legendRows * LegendRowHeight + Mathf.Max(0, legendRows - 1) * 6f;
                hudLegendRect.sizeDelta = new Vector2(-28f, legendHeight);
                hudHint.gameObject.SetActive(false);
                var height = 24f + legendHeight + (headerHeight > 0f ? headerHeight + 10f : 0f);
                Anchor(hudHintRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-HudMargin, HudMargin), new Vector2(LegendWidth, height));
                return;
            }
            var hasHint = !string.IsNullOrWhiteSpace(hudHint.text);
            var bannerHalf = hudStatePanel.activeSelf ? bannerWidth * 0.5f : 0f;
            var centreHalf = Mathf.Max(belt ? EquipmentWidth * 0.5f : 0f, bannerHalf, 180f);
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
                case HudActorState.Recovering: return "RECUPERANDO…";
                case HudActorState.Fainted: return "DESMAYADO";
                case HudActorState.Stunned: return "ATURDIDO";
                case HudActorState.Attached: return "[E] DESPRENDERTE";
                default: return string.Empty;
            }
        }
    }
}
