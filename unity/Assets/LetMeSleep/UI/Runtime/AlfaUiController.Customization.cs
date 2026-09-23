using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core.Customization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Customization (UI-06 screens 5-6, PER-08): role tabs HUMANO (blue) / MOSQUITO (red) on top; a category rail
    /// with icons on the left (human PERSONAJE / COLORES / ACCESORIOS; mosquito CUERPO / COLORES, both live) sized to
    /// its buttons, with a summary card of the role (the character rendered by the viewer itself, refreshed with every
    /// change, and what is chosen) filling the rest of the column above VOLVER; the 3D viewer in the middle, in the
    /// painted warm bedroom on a wooden pedestal (drag to rotate, CENTRAR; the mosquito seen from the front with both
    /// wings in a V); on the right the options (colours as a grid of 64-unit swatches with the chosen one shown big
    /// beside it; nine clothes colours, six skin tones, eight mosquito colours; the mosquito's CUERPO shows the
    /// sketch's column: COLOR DE CUERPO, ESTILO DE ALAS and OJOS, three cards each, the ones this build does not have
    /// yet with a padlock and "Próximamente" at 55 %), then the "VISTA PREVIA" row (FRENTE, ESPALDA, LADO, framed by
    /// the same rule from the model's bounds, 16 units under the options) with ALEATORIO / DESHACER 12 units under it
    /// and one call to action, always APLICAR (the full green; with nothing to apply only its label dims). Spare
    /// height goes to the preview views, then under ALEATORIO. With a modular catalogue every category of the role is a
    /// section of one scrolling list (the rail jumps to it) whose cards are sized to leave a fixed 12-unit scrollbar
    /// channel, with a 24-unit fade at the bottom edge; each option has its own picture and, while the game cannot
    /// assemble the parts yet, the viewer shows an approximation (bone scale, rounded wings, angry brows and lids,
    /// body tint). Presentations never rebuild controls that did not change, so scroll position and keyboard focus
    /// survive each selection (ui-presentation-audio-8).
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const string SkinCategory = "skin";
        private const string PajamaCategory = "pajama";
        private const string AccessoriesCategory = "accessories";
        private const string MosquitoCategory = "mosquito";
        private const string MosquitoBodyCategory = "mosquito-body";
        private const float LockedCardAlpha = 0.55f;
        private const float PreviewTopGap = 4f; // + the 12-unit content spacing: VISTA PREVIA 16 units under the options
        private const float CustomizationHeaderHeight = 84f;
        private const float CustomizationColumnsTop = CustomizationHeaderHeight + 22f;
        private const float CustomizationRailWidth = 300f;
        private const float CustomizationOptionsWidth = 560f;
        private const float CustomizationGap = 22f;
        private const float CustomizationFooterHeight = 82f;
        private const float CustomizationContentPadding = 24f;
        private const float CustomizationContentWidth = CustomizationOptionsWidth - 2f * CustomizationContentPadding;
        private const float CategoryButtonHeight = 74f;
        private const float CategorySpacing = 10f;
        private const float RailInset = 14f;
        private const float RailFooter = RailInset + 64f + RailInset;
        private const float SummaryMinHeight = 250f;
        private const int SwatchColumns = 3;
        private const int BodySwatchColumns = 4;
        private const int CardColumns = 4;
        private const int ModularSwatchColumns = 5;
        private const float ScrollChannel = 12f;
        private const float PreviewThumbAspect = 0.62f;
        private const float PreviewRowPreferred = 318f;
        private const float PreviewRowMinimum = 150f;
        // Basic palettes (UI-06 5): 3 columns of 64-unit swatches (76-unit buttons).
        private static readonly Vector2 SwatchCell = new Vector2(76f, 76f);
        private const float SwatchInset = 6f;
        private static readonly Vector2 ModularSwatchCell = new Vector2(80f, 80f);
        // Modular picture cards: four per row with a 12-unit scrollbar channel (panel - 24 inset - 12 channel - 3 gaps).
        // 126 tall: two complete sections (caption + one row each) fit the list at 1080p, so no row is cut there.
        private static readonly Vector2 CardCell = new Vector2(Mathf.Floor((CustomizationContentWidth - 24f - ScrollChannel - 3f * 12f) / CardColumns), 126f);
        private const float PreviewExtraMaximum = 200f;
        // Three cards per row inside the grid padding (2 + 2): (content - 4 - 2 gaps) / 3, 74 tall (picture + one line).
        private static readonly Vector2 StyleCardCell = new Vector2(Mathf.Floor((CustomizationContentWidth - 4f - 2f * 12f) / 3f), 74f);
        private static readonly Vector2 GridSpacing = new Vector2(12f, 12f);
        private static readonly Color NightcapRed = AlfaUiTheme.Hex("C8322E");

        private CharacterPreviewOrbit previewOrbit;
        private GameObject customizationPreviewUnavailable;
        private GameObject customizationApproximateChip;
        private TextMeshProUGUI customizationCategoryTitle;
        private TextMeshProUGUI customizationSelectionLabel;
        private TextMeshProUGUI customizationStatus;
        private RectTransform humanPaletteRoot;
        private RectTransform pajamaPaletteRoot;
        private RectTransform mosquitoPaletteRoot;
        private RectTransform mosquitoBodyPaletteRoot;
        private GameObject mosquitoBodyGroup;
        private GameObject mosquitoColorsGroup;
        private GameObject skinPaletteGroup;
        private GameObject pajamaPaletteGroup;
        private GameObject accessoriesGroup;
        private AlfaUiIcon accessoryHatIcon;
        private GameObject humanCustomizationFields;
        private GameObject mosquitoCustomizationFields;
        private GameObject modularCustomizationFields;
        private GameObject basicCategoryRail;
        private GameObject modularCategoryRail;
        private RectTransform modularCategoryRoot;
        private RectTransform modularOptionRoot;
        private UnityEngine.UI.ScrollRect modularOptionScroll;
        private GameObject modularOptionFade;
        private UnityEngine.UI.LayoutElement previewRowLayout;
        private UnityEngine.UI.LayoutElement modularFieldsLayout;
        private GameObject optionsSpacer;
        private RectTransform customizationRail;
        private RectTransform basicCategoryRect;
        private RectTransform modularCategoryRect;
        private RectTransform roleSummary;
        private TextMeshProUGUI roleSummaryTitle;
        private AlfaUiIcon roleSummaryIcon;
        private UnityEngine.UI.RawImage roleSummaryPortrait;
        private AlfaUiArtFit roleSummaryFit;
        private RectTransform roleSummaryPortraitFrame;
        private TextMeshProUGUI roleSummaryNote;
        private readonly List<(RectTransform row, UnityEngine.UI.Image dot, TextMeshProUGUI label, TextMeshProUGUI value)> roleSummaryRows =
            new List<(RectTransform, UnityEngine.UI.Image, TextMeshProUGUI, TextMeshProUGUI)>();
        private float customizationLaidOutRailHeight = -1f;
        private float customizationLaidOutOptionsHeight = -1f;
        private readonly Dictionary<RectTransform, (UnityEngine.UI.Image swatch, TextMeshProUGUI name)> paletteChoices =
            new Dictionary<RectTransform, (UnityEngine.UI.Image, TextMeshProUGUI)>();
        private UnityEngine.UI.Button customizationHumanButton;
        private UnityEngine.UI.Button customizationMosquitoButton;
        private CanvasGroup customizationControlsGroup;
        private UnityEngine.UI.Button customizationSaveButton;
        private TextMeshProUGUI customizationSaveLabel;
        private UnityEngine.UI.Button customizationResetButton;
        private UnityEngine.UI.Button customizationRandomButton;
        private readonly Dictionary<string, UnityEngine.UI.Button> basicCategoryButtons = new Dictionary<string, UnityEngine.UI.Button>();
        private readonly Dictionary<RectTransform, string> builtPaletteSignatures = new Dictionary<RectTransform, string>();
        private readonly Dictionary<string, UnityEngine.UI.Button> modularOptionButtons = new Dictionary<string, UnityEngine.UI.Button>();
        private readonly Dictionary<string, RectTransform> modularSections = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, TextMeshProUGUI> modularSectionCaptions = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Sprite> optionArt = new Dictionary<string, Sprite>();
        private readonly UnityEngine.UI.Button[] previewAngleButtons = new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.RawImage[] previewAngleViews = new UnityEngine.UI.RawImage[3];
        private static readonly PreviewAngle[] PreviewAngleOrder = { PreviewAngle.Front, PreviewAngle.Back, PreviewAngle.Side };
        private string basicHumanCategory = SkinCategory;
        private string basicMosquitoCategory = MosquitoBodyCategory;
        private string builtModularCategoriesKey = string.Empty;
        private string builtModularOptionsKey = string.Empty;
        private int selectedPreviewAngle = -1;
        private readonly System.Random customizationRandom = new System.Random();

        private void BuildCustomization(AlfaUiDependencies dependencies)
        {
            var view = factory.View("CustomizationView", transform, false);
            SetSceneScrim(view, 0.8f);
            screens[AlfaUiScreen.Customization] = view;
            var safe = factory.SafeArea(view.transform, 40f, 40f, 30f, 32f);
            var layout = AlfaUiFactory.Node("Layout", safe, typeof(CanvasGroup)).GetComponent<RectTransform>();
            AlfaUiFactory.Fill(layout);
            customizationControlsGroup = layout.GetComponent<CanvasGroup>();

            // Header: title on the left, role tabs centred (PER-08: HUMAN / MOSQUITO).
            var header = factory.SectionHeader(layout, "Header", "PERSONALIZACIÓN", AlfaUiIconKind.Customize, AlfaUiTheme.Lamp400);
            Anchor(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -16f), new Vector2(460f, 52f));
            header.Find("Label").GetComponent<TextMeshProUGUI>().fontSize = AlfaUiTheme.HeaderTitleSize;
            var roleTabs = factory.Horizontal(layout, "RoleTabs", 14f, TextAnchor.MiddleCenter);
            roleTabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            roleTabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = true;
            // Right of the title (never over it, down to 5:4), roughly centred over the viewer at 16:9.
            Anchor(roleTabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, 0f), new Vector2(760f, CustomizationHeaderHeight));
            customizationHumanButton = factory.FeatureButton(roleTabs, "CustomizationHumanButton", "HUMANO", "PIEL, ROPA Y ACCESORIOS",
                () => SetCustomizationRole(AlfaRole.Human), AlfaUiIconKind.Human, false, false, CustomizationHeaderHeight);
            customizationMosquitoButton = factory.FeatureButton(roleTabs, "CustomizationMosquitoButton", "MOSQUITO", "CUERPO Y COLORES",
                () => SetCustomizationRole(AlfaRole.Mosquito), AlfaUiIconKind.Mosquito, false, false, CustomizationHeaderHeight);
            foreach (var tab in new[] { customizationHumanButton, customizationMosquitoButton })
            {
                var title = tab.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                factory.MakeDisplay(title, 30f);
                title.alignment = TextAlignmentOptions.BottomLeft;
                AlfaUiFactory.Fill(title.rectTransform, 76f, 16f, 4f, 38f);
                var subtitle = tab.transform.Find("Subtitle").GetComponent<RectTransform>();
                AlfaUiFactory.Fill(subtitle, 76f, 16f, 48f, 6f);
            }

            BuildCustomizationRail(layout);
            BuildCustomizationViewer(layout, dependencies);
            BuildCustomizationOptions(layout);
        }

        private void BuildCustomizationRail(RectTransform layout)
        {
            var rail = factory.Panel(layout, "CategoryRail", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.96f));
            AlfaUiFactory.Place(rail, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(CustomizationRailWidth, -CustomizationColumnsTop));
            customizationRail = rail;

            // Categories at the top, sized to their buttons (LayoutCustomizationRail); the role summary fills the rest.
            var basic = factory.Vertical(rail, "BasicCategories", CategorySpacing);
            Anchor(basic, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -RailInset), new Vector2(-2f * RailInset, 240f));
            basicCategoryRect = basic;
            basicCategoryRail = basic.gameObject;
            // Human (UI-06 5): PERSONAJE, COLORES, ACCESORIOS.
            basicCategoryButtons[SkinCategory] = CategoryButton(basic, "CustomizationCategory_" + SkinCategory, "PERSONAJE",
                AlfaUiIconKind.Face, () => SelectBasicCategory(SkinCategory));
            basicCategoryButtons[PajamaCategory] = CategoryButton(basic, "CustomizationCategory_" + PajamaCategory, "COLORES",
                AlfaUiIconKind.Palette, () => SelectBasicCategory(PajamaCategory));
            basicCategoryButtons[AccessoriesCategory] = CategoryButton(basic, "CustomizationCategory_" + AccessoriesCategory, "ACCESORIOS",
                AlfaUiIconKind.Hat, () => SelectBasicCategory(AccessoriesCategory));
            // Mosquito (UI-06 6): CUERPO (colour, wings and eyes in one column) and COLORES, both live. The parts this
            // build cannot change yet are cards with a padlock inside CUERPO, never locked rail entries.
            basicCategoryButtons[MosquitoBodyCategory] = CategoryButton(basic, "CustomizationCategory_" + MosquitoBodyCategory, "CUERPO",
                AlfaUiIconKind.Mosquito, () => SelectBasicCategory(MosquitoBodyCategory));
            basicCategoryButtons[MosquitoCategory] = CategoryButton(basic, "CustomizationCategory_" + MosquitoCategory, "COLORES",
                AlfaUiIconKind.Palette, () => SelectBasicCategory(MosquitoCategory));

            var modular = AlfaUiFactory.Node("ModularCategories", rail).GetComponent<RectTransform>();
            Anchor(modular, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-24f, 400f));
            modularCategoryRect = modular;
            modularCategoryRail = modular.gameObject;
            var categoryScroll = factory.ScrollView(modular, "CategoryScroll", out modularCategoryRoot, 400f, true);
            AlfaUiFactory.Fill(categoryScroll);
            modularCategoryRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 10f;
            modularCategoryRail.SetActive(false);

            BuildRoleSummary(rail);

            var back = factory.Button(rail, "CustomizationBackButton", "VOLVER", CloseCustomization, AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Back);
            Anchor((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-28f, 64f));
        }

        /// <summary>
        /// Role card under the categories (fills the rail down to VOLVER): the role's picture, what is chosen for
        /// each part (with its colour) and one honest note about what is fixed in this build.
        /// </summary>
        private void BuildRoleSummary(RectTransform rail)
        {
            roleSummary = factory.Inset(rail, "RoleSummary", -1f, AlfaUiTheme.ButtonRadius);
            var header = factory.Horizontal(roleSummary, "Header", 10f, TextAnchor.MiddleLeft);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-28f, 32f));
            roleSummaryIcon = factory.Icon(header, "Icon", AlfaUiIconKind.Human, AlfaUiTheme.Sky400);
            var iconLayout = roleSummaryIcon.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            iconLayout.minWidth = iconLayout.preferredWidth = iconLayout.minHeight = iconLayout.preferredHeight = 28f;
            roleSummaryTitle = factory.Title(header, "Title", "TU HUMANO", 26f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            roleSummaryTitle.textWrappingMode = TextWrappingModes.NoWrap;

            var frame = AlfaUiFactory.Node("SummaryPortrait", roleSummary, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask), typeof(AlfaUiArtFit))
                .GetComponent<RectTransform>();
            var frameImage = frame.GetComponent<UnityEngine.UI.Image>();
            frameImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            frameImage.type = UnityEngine.UI.Image.Type.Sliced;
            frameImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night600, 0.6f);
            frameImage.raycastTarget = false;
            frame.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            roleSummaryPortraitFrame = frame;
            roleSummaryPortrait = AlfaUiFactory.Node("Portrait", frame, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            roleSummaryPortrait.raycastTarget = false;
            roleSummaryFit = frame.GetComponent<AlfaUiArtFit>();
            roleSummaryFit.Target = roleSummaryPortrait;

            var rows = factory.Vertical(roleSummary, "SummaryRows", 2f);
            Anchor(rows, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-28f, 4f * 32f + 3f * 2f + 58f));
            for (var i = 0; i < 4; i++)
            {
                var row = factory.Horizontal(rows, "SummaryRow" + i, 8f, TextAnchor.MiddleLeft);
                var rowLayout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                rowLayout.minHeight = rowLayout.preferredHeight = 32f;
                var dot = AlfaUiFactory.Node("Dot", row, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement)).GetComponent<UnityEngine.UI.Image>();
                dot.sprite = AlfaUiSkin.Circle();
                dot.raycastTarget = false;
                var dotLayout = dot.GetComponent<UnityEngine.UI.LayoutElement>();
                dotLayout.minWidth = dotLayout.preferredWidth = dotLayout.minHeight = dotLayout.preferredHeight = 18f;
                var label = factory.Caption(row, "Label", string.Empty);
                label.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
                var value = factory.Text(row, "Value", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineRight, true);
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
                roleSummaryRows.Add((row, dot, label, value));
            }
            roleSummaryNote = factory.Text(rows, "Note", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            roleSummaryNote.textWrappingMode = TextWrappingModes.Normal;
            roleSummaryNote.overflowMode = TextOverflowModes.Overflow;
            roleSummaryNote.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 54f;
        }

        /// <summary>
        /// Rail heights from its content: the category buttons (or the modular list, scrolling only when it has to),
        /// then the summary card down to VOLVER, hidden when less than 250 units would be left for it.
        /// </summary>
        private void LayoutCustomizationRail()
        {
            if (customizationRail == null) return;
            var height = customizationRail.rect.height;
            if (height <= 1f) return;
            customizationLaidOutRailHeight = height;
            var modular = modularCategoryRail.activeSelf;
            var buttons = 0;
            var root = modular ? modularCategoryRoot : (Transform)basicCategoryRect;
            foreach (Transform child in root) if (child.gameObject.activeSelf) buttons++;
            var needed = buttons * CategoryButtonHeight + Mathf.Max(0, buttons - 1) * CategorySpacing + (modular ? 12f : 0f);
            var available = height - RailInset - RailFooter;
            var summary = needed + RailInset + SummaryMinHeight <= available;
            var categories = summary ? needed : available;
            if (modular) modularCategoryRect.sizeDelta = new Vector2(-24f, categories);
            else basicCategoryRect.sizeDelta = new Vector2(-2f * RailInset, categories);
            roleSummary.gameObject.SetActive(summary && customizationState != null && !customizationState.IsReadOnly);
            if (!roleSummary.gameObject.activeSelf) return;
            var top = RailInset + categories + RailInset;
            var summaryHeight = height - top - RailFooter;
            Anchor(roleSummary, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(-2f * RailInset, summaryHeight));
            // Picture between the header and the rows when there is room for it.
            var rowsHeight = ((RectTransform)roleSummaryRows[0].row.parent).sizeDelta.y;
            var pictureHeight = summaryHeight - 12f - 32f - 10f - 10f - rowsHeight - 12f;
            roleSummaryPortraitFrame.gameObject.SetActive(pictureHeight >= 90f);
            if (pictureHeight >= 90f)
            {
                Anchor(roleSummaryPortraitFrame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(-28f, pictureHeight));
                roleSummaryFit.Apply();
            }
        }

        /// <summary>Summary rows: what is chosen per part, with its colour; the note says what is fixed.</summary>
        private void UpdateRoleSummary(bool human, bool modular)
        {
            if (roleSummary == null) return;
            roleSummaryTitle.text = human ? "TU HUMANO" : "TU MOSQUITO";
            roleSummaryIcon.Kind = human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            roleSummaryIcon.color = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.StatusWarn;
            if (previewOrbit != null && previewOrbit.IsBound)
            {
                // The picture comes from the viewer itself (same clone, same look), re-rendered with every change of
                // colour, wings, eyes or proboscis; never the stale painted art.
                if (roleSummaryFit.Target != null)
                {
                    roleSummaryFit.Target = null;
                    AlfaUiFactory.Fill(roleSummaryPortrait.rectTransform);
                    roleSummaryPortrait.texture = null;
                    previewOrbit.BindSummary(roleSummaryPortrait);
                }
                roleSummaryPortrait.enabled = roleSummaryPortrait.texture != null;
            }
            else
            {
                var art = LoadRolePortrait(human ? AlfaRole.Human : AlfaRole.Mosquito);
                roleSummaryFit.Target = roleSummaryPortrait;
                roleSummaryPortrait.texture = art != null ? art.texture : null;
                roleSummaryPortrait.enabled = art != null;
                roleSummaryFit.FromTop = human ? 0.62f : 1f;
                roleSummaryFit.Pad = human ? 0.05f : 0.04f;
                roleSummaryFit.Apply();
            }
            var rows = new List<(string label, string value, Color? color)>();
            if (modular)
            {
                var loadout = modularCustomizationDraft?.For(ToCustomizationRole(modularEditedRole));
                foreach (var slot in VisibleModularSlots())
                {
                    if (rows.Count == 4) break;
                    if (loadout == null || !slot.TryOption(loadout.OptionFor(slot.SlotId) ?? string.Empty, out var option)) continue;
                    rows.Add((slot.Label.ToUpperInvariant(), option.Label, option.HasSwatch ? ColorFromRgba(option.SwatchRgba) : (Color?)null));
                }
                roleSummaryNote.text = "Se ve en el visor y en VISTA PREVIA.";
            }
            else if (human && customizationDraft != null)
            {
                var skin = customizationState.SkinColors.FirstOrDefault(option => option.Id == customizationDraft.SkinColorId);
                var clothes = customizationState.PajamaColors.FirstOrDefault(option => option.Id == customizationDraft.PajamaColorId);
                rows.Add(("PIEL", skin?.Label ?? "—", skin?.Color));
                rows.Add(("PANTALÓN", clothes?.Label ?? "—", clothes?.Color));
                rows.Add(("REMERA", "Crema", AlfaUiTheme.Hex("E8DCC5")));
                rows.Add(("GORRO", "Rojo", NightcapRed));
                roleSummaryNote.text = "Pantuflas y gorro de dormir rojo.";
            }
            else if (customizationDraft != null)
            {
                var body = customizationState.MosquitoColors.FirstOrDefault(option => option.Id == customizationDraft.MosquitoColorId);
                rows.Add(("CUERPO", body?.Label ?? "—", body?.Color));
                rows.Add(("ALAS", "Facetadas", (Color?)null));
                rows.Add(("OJOS", "Grandes", (Color?)null));
                rows.Add(("PROBÓSCIDE", "Estándar", (Color?)null));
                roleSummaryNote.text = "Alas, ojos y probóscide: próximamente.";
            }
            for (var i = 0; i < roleSummaryRows.Count; i++)
            {
                var entry = roleSummaryRows[i];
                var used = i < rows.Count;
                entry.row.gameObject.SetActive(used);
                if (!used) continue;
                entry.label.text = rows[i].label;
                entry.value.text = rows[i].value;
                entry.dot.color = rows[i].color ?? Color.clear;
            }
        }

        private UnityEngine.UI.Button CategoryButton(Transform parent, string name, string label, AlfaUiIconKind icon, UnityEngine.Events.UnityAction callback)
        {
            var button = factory.Button(parent, name, label, callback, AlfaButtonStyle.Tab, 74f, icon);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = AlfaUiTheme.MinTextSize;
            text.fontSizeMax = 25f;
            text.lineSpacing = -10f;
            AlfaUiFactory.Fill(text.rectTransform, 76f, 12f, 4f, 4f);
            return button;
        }

        private void BuildCustomizationViewer(RectTransform layout, AlfaUiDependencies dependencies)
        {
            var previewPanel = factory.Panel(layout, "PreviewPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.98f));
            AlfaUiFactory.Place(previewPanel, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(CustomizationRailWidth + CustomizationGap, 0f),
                new Vector2(-(CustomizationOptionsWidth + CustomizationGap), -CustomizationColumnsTop));

            var viewport = AlfaUiFactory.Node("PreviewViewport", previewPanel, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            var viewportRect = viewport.GetComponent<RectTransform>();
            AlfaUiFactory.Fill(viewportRect, 12f, 12f, 12f, 12f);
            var viewportImage = viewport.GetComponent<UnityEngine.UI.Image>();
            viewportImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            viewportImage.type = UnityEngine.UI.Image.Type.Sliced;
            viewportImage.color = CharacterPreviewOrbit.StageBackground;
            viewportImage.raycastTarget = false;
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;

            var rawNode = AlfaUiFactory.Node("CharacterPreview", viewport.transform, typeof(UnityEngine.UI.RawImage), typeof(CharacterPreviewOrbit));
            AlfaUiFactory.Fill(rawNode.GetComponent<RectTransform>());
            var raw = rawNode.GetComponent<UnityEngine.UI.RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = true;
            previewOrbit = rawNode.GetComponent<CharacterPreviewOrbit>();
            previewOrbit.BindingChanged += RefreshPreviewAvailability;
            previewOrbit.Initialize(raw, dependencies.Preview);

            // Soft falloff towards the panel edges (UI-06 viewer); never intercepts the drag.
            var vignette = AlfaUiFactory.Node("PreviewVignette", viewport.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            vignette.sprite = AlfaUiFactory.RadialVignetteSprite();
            vignette.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.55f);
            vignette.raycastTarget = false;
            AlfaUiFactory.Fill(vignette.rectTransform, -40f, -40f, -60f, -60f);

            // "ARRASTRÁ PARA GIRAR" with the mouse and side arrows (PER-08 "drag to rotate").
            var hint = factory.Horizontal(viewport.transform, "RotateHint", 10f, TextAnchor.MiddleCenter);
            var hintImage = hint.gameObject.AddComponent<UnityEngine.UI.Image>();
            hintImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            hintImage.type = UnityEngine.UI.Image.Type.Sliced;
            hintImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.72f);
            hintImage.raycastTarget = false;
            var hintLayout = hint.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hintLayout.padding = new RectOffset(16, 16, 6, 6);
            hintLayout.childControlWidth = true;
            var hintFit = hint.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            hintFit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.anchoredPosition = new Vector2(0f, 18f);
            hint.sizeDelta = new Vector2(0f, 44f);
            HintIcon(hint, "HintLeft", AlfaUiIconKind.ChevronLeft, 20f);
            HintIcon(hint, "HintMouse", AlfaUiIconKind.Mouse, 28f);
            var hintText = factory.Text(hint, "OrbitHint", "ARRASTRÁ PARA GIRAR", AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.characterSpacing = AlfaUiTheme.CaptionTracking;
            hintText.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            HintIcon(hint, "HintRight", AlfaUiIconKind.ChevronRight, 20f);

            // CENTRAR: a small icon button in the viewer corner (the angle views are in VISTA PREVIA).
            var reset = factory.Button(viewport.transform, "PreviewResetButton", string.Empty, () =>
            {
                previewOrbit.ResetView();
                MarkPreviewAngle(-1);
            }, AlfaButtonStyle.Quiet, 52f, AlfaUiIconKind.Refresh);
            reset.transform.Find("Label").gameObject.SetActive(false);
            Anchor((RectTransform)reset.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(64f, 52f));
            var resetPlate = reset.transform.Find("IconPlate") as RectTransform;
            if (resetPlate != null) Anchor(resetPlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, resetPlate.sizeDelta);

            // Honest, discreet: while the viewer approximates modular parts it says so in a corner chip.
            var chip = factory.Panel(viewport.transform, "PreviewApproximate", AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.72f), -1f, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(chip, frame: Color.clear, shadow: Color.clear);
            Anchor(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(236f, 40f));
            var chipText = factory.Text(chip, "Label", "VISTA APROXIMADA", AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.Center, true);
            chipText.textWrappingMode = TextWrappingModes.NoWrap;
            chipText.characterSpacing = AlfaUiTheme.CaptionTracking;
            AlfaUiFactory.Fill(chipText.rectTransform, 10f, 10f, 2f, 2f);
            customizationApproximateChip = chip.gameObject;
            customizationApproximateChip.SetActive(false);

            var unavailable = factory.Text(previewPanel, "PreviewUnavailable", "El visor 3D se conecta al personaje del juego.", AlfaUiTheme.BodySize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            Anchor(unavailable.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            customizationPreviewUnavailable = unavailable.gameObject;
            RefreshPreviewAvailability();
        }

        private void HintIcon(Transform parent, string name, AlfaUiIconKind kind, float size)
        {
            var icon = factory.Icon(parent, name, kind, AlfaUiTheme.Sheet100);
            var element = icon.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minWidth = element.preferredWidth = size;
            element.minHeight = element.preferredHeight = size;
        }

        private void MarkPreviewAngle(int index)
        {
            selectedPreviewAngle = index;
            for (var i = 0; i < previewAngleButtons.Length; i++)
                if (previewAngleButtons[i] != null) MarkOption(previewAngleButtons[i], i == index);
        }

        private void RefreshPreviewAvailability()
        {
            if (customizationPreviewUnavailable != null && previewOrbit != null)
                customizationPreviewUnavailable.SetActive(!previewOrbit.IsBound);
        }

        private void BuildCustomizationOptions(RectTransform layout)
        {
            var optionsPanel = factory.Panel(layout, "OptionsPanel", AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.97f));
            AlfaUiFactory.Place(optionsPanel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-CustomizationOptionsWidth, 0f),
                new Vector2(0f, -CustomizationColumnsTop));

            var content = factory.Vertical(optionsPanel, "Content", 12f);
            AlfaUiFactory.Fill(content, CustomizationContentPadding, CustomizationContentPadding, 20f, 20f + CustomizationFooterHeight + 14f);
            content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childForceExpandHeight = false;
            customizationCategoryTitle = factory.Caption(content, "CategoryTitle", "OPCIONES DEL HUMANO");

            humanCustomizationFields = factory.Vertical(content, "HumanFields", 12f).gameObject;
            skinPaletteGroup = factory.Vertical(humanCustomizationFields.transform, "SkinGroup", 10f).gameObject;
            SectionCaption(skinPaletteGroup.transform, "SkinCaption", "TONO DE PIEL");
            humanPaletteRoot = CreatePaletteLayout(skinPaletteGroup.transform, "SkinPalette");
            pajamaPaletteGroup = factory.Vertical(humanCustomizationFields.transform, "PajamaGroup", 10f).gameObject;
            SectionCaption(pajamaPaletteGroup.transform, "PajamaCaption", "COLORES DE ROPA");
            pajamaPaletteRoot = CreatePaletteLayout(pajamaPaletteGroup.transform, "PajamaPalette");
            factory.Text(pajamaPaletteGroup.transform, "DefaultClothes", "Tiñe el pantalón. Remera crema y gorro rojo.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            BuildAccessories(humanCustomizationFields.transform);
            mosquitoCustomizationFields = factory.Vertical(content, "MosquitoFields", 10f).gameObject;
            // CUERPO (UI-06 6): COLOR DE CUERPO (8 swatches, 4 x 2), ESTILO DE ALAS and OJOS (three cards each).
            mosquitoBodyGroup = factory.Vertical(mosquitoCustomizationFields.transform, "MosquitoBodyGroup", 8f).gameObject;
            SectionCaption(mosquitoBodyGroup.transform, "MosquitoBodyCaption", "COLOR DE CUERPO");
            mosquitoBodyPaletteRoot = CreatePaletteLayout(mosquitoBodyGroup.transform, "MosquitoBodyPalette", BodySwatchColumns);
            BuildStyleCards(mosquitoBodyGroup.transform, "Wings", "ESTILO DE ALAS", new[]
            {
                ("Faceted", "WingsFaceted", "FACETADAS", true), ("Round", "WingsRound", "REDONDAS", false), ("Long", "WingsLong", "LARGAS", false)
            });
            BuildStyleCards(mosquitoBodyGroup.transform, "Eyes", "OJOS", new[]
            {
                ("Big", "EyesBig", "GRANDES", true), ("Angry", "EyesAngry", "ENOJADOS", false), ("Sleepy", "EyesSleepy", "DORMIDOS", false)
            });
            // COLORES: the body colours alone, with the chosen one big beside them.
            mosquitoColorsGroup = factory.Vertical(mosquitoCustomizationFields.transform, "MosquitoColorsGroup", 10f).gameObject;
            SectionCaption(mosquitoColorsGroup.transform, "MosquitoCaption", "COLOR DE CUERPO");
            mosquitoPaletteRoot = CreatePaletteLayout(mosquitoColorsGroup.transform, "MosquitoPalette");

            modularCustomizationFields = factory.Vertical(content, "ModularFields", 8f).gameObject;
            modularFieldsLayout = modularCustomizationFields.AddComponent<UnityEngine.UI.LayoutElement>();
            modularFieldsLayout.flexibleHeight = 1f;
            var optionsScroll = factory.ScrollView(modularCustomizationFields.transform, "OptionsScroll", out modularOptionRoot, 300f, true);
            optionsScroll.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 1f;
            var optionsLayout = modularOptionRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            optionsLayout.spacing = 16f;
            optionsLayout.padding = new RectOffset(6, 6, 6, 10);
            modularOptionScroll = optionsScroll.GetComponent<UnityEngine.UI.ScrollRect>();
            // A fixed 12-unit channel for the scrollbar: the cards are sized to end before it (CORTAS is never cut).
            modularOptionScroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            modularOptionScroll.viewport.offsetMax = new Vector2(-(6f + ScrollChannel), -6f);
            // 24-unit fade (#15264A to 0 %) at the bottom edge while more options are below, so a half-cut row never
            // reads as the last one.
            var optionsFade = AlfaUiFactory.Node("OptionsFade", optionsScroll, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            optionsFade.sprite = AlfaUiFactory.VerticalFadeSprite();
            optionsFade.color = Color.Lerp(AlfaUiTheme.Night700, AlfaUiTheme.PanelInset, 0.55f);
            optionsFade.raycastTarget = false;
            Anchor(optionsFade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-(ScrollChannel * 0.5f), 6f), new Vector2(-(12f + ScrollChannel), 32f));
            modularOptionFade = optionsFade.gameObject;
            modularOptionFade.SetActive(false);
            modularCustomizationFields.SetActive(false);

            var selection = factory.Horizontal(content, "Selection", 10f, TextAnchor.MiddleLeft);
            selection.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 34f;
            var check = factory.Icon(selection, "SelectionCheck", AlfaUiIconKind.Ready, AlfaUiTheme.StatusOk);
            var checkLayout = check.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            checkLayout.minWidth = checkLayout.preferredWidth = 24f;
            checkLayout.minHeight = checkLayout.preferredHeight = 24f;
            customizationSelectionLabel = factory.Text(selection, "SelectionLabel", string.Empty, 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft, true);
            customizationSelectionLabel.textWrappingMode = TextWrappingModes.NoWrap;
            customizationSelectionLabel.enableAutoSizing = true;
            customizationSelectionLabel.fontSizeMin = AlfaUiTheme.MinTextSize;
            customizationSelectionLabel.fontSizeMax = 24f;

            customizationStatus = factory.Text(content, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            customizationStatus.overflowMode = TextOverflowModes.Overflow;

            // Basic mode: VISTA PREVIA 16 units under the options and ALEATORIO / DESHACER 12 units under it; spare
            // height grows the preview views first, the rest sits under ALEATORIO (never a band above VISTA PREVIA).
            BuildPreviewRow(content);

            // ALEATORIO / DESHACER right under VISTA PREVIA (12 units, the content spacing).
            var secondary = factory.Horizontal(content, "SecondaryActions", 12f, TextAnchor.MiddleCenter);
            secondary.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            var secondaryLayout = secondary.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            secondaryLayout.minHeight = secondaryLayout.preferredHeight = 64f;

            optionsSpacer = AlfaUiFactory.Node("OptionsSpacer", content, typeof(UnityEngine.UI.LayoutElement)).gameObject;
            var spacerLayout = optionsSpacer.GetComponent<UnityEngine.UI.LayoutElement>();
            spacerLayout.minHeight = 0f;
            spacerLayout.flexibleHeight = 1f;

            var footer = AlfaUiFactory.Node("Actions", optionsPanel).GetComponent<RectTransform>();
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-2f * CustomizationContentPadding, CustomizationFooterHeight));
            customizationRandomButton = factory.Button(secondary, "CustomizationRandomButton", "ALEATORIO", RandomizeCustomization,
                AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Dice);
            customizationResetButton = factory.Button(secondary, "CustomizationResetButton", "DESHACER", ResetCustomization,
                AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Undo);
            // One call to action, always APLICAR (Ajustes uses the same): green, dimmed to 50 % with nothing to apply.
            customizationSaveButton = factory.Button(footer, "CustomizationSaveButton", "APLICAR", CustomizationPrimaryAction,
                AlfaButtonStyle.Success, 82f, AlfaUiIconKind.Ready);
            factory.StrongLabel(customizationSaveButton, AlfaUiTheme.CtaSize);
            AlfaUiFactory.KeepIntentWhenDisabled(customizationSaveButton);
            Anchor((RectTransform)customizationSaveButton.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 82f));
            customizationSaveLabel = customizationSaveButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        /// <summary>Uppercase section caption that wraps instead of truncating (long catalogue labels).</summary>
        private TextMeshProUGUI SectionCaption(Transform parent, string name, string text)
        {
            var caption = factory.Caption(parent, name, text);
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.overflowMode = TextOverflowModes.Overflow;
            caption.richText = true;
            return caption;
        }

        /// <summary>
        /// ACCESORIOS (UI-06 5): what the human always wears in this build, the red nightcap and the slippers.
        /// They are shown as worn, not as choices, because there are no alternatives yet.
        /// </summary>
        private void BuildAccessories(Transform parent)
        {
            accessoriesGroup = factory.Vertical(parent, "AccessoriesGroup", 10f).gameObject;
            SectionCaption(accessoriesGroup.transform, "AccessoriesCaption", "ACCESORIOS PUESTOS");
            var grid = AlfaUiFactory.Node("AccessoriesGrid", accessoriesGroup.transform, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            AlfaUiFactory.ConfigureGrid(grid.GetComponent<UnityEngine.UI.GridLayoutGroup>(), new Vector2(170f, 170f), GridSpacing, 2);
            var gridLayout = grid.GetComponent<UnityEngine.UI.LayoutElement>();
            gridLayout.minHeight = gridLayout.preferredHeight = 174f;
            accessoryHatIcon = AccessoryCard(grid.transform, "AccessoryHat", "GORRO DE DORMIR", AlfaUiIconKind.Hat);
            AccessoryCard(grid.transform, "AccessorySlippers", "PANTUFLAS", AlfaUiIconKind.Slipper);
            factory.Text(accessoriesGroup.transform, "AccessoriesNote", "Siempre puestos. El gorro de dormir es rojo.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
        }

        /// <summary>
        /// A section of three picture cards (UI-06 6 "ESTILO DE ALAS" / "OJOS"): the style the mosquito wears is
        /// marked chosen; the ones this build does not have yet are locked cards: padlock, "Próximamente", all at 55 %.
        /// Cards are named "MosquitoStyle_&lt;section&gt;_&lt;variant&gt;".
        /// </summary>
        private void BuildStyleCards(Transform parent, string section, string caption, (string variant, string art, string label, bool available)[] cards)
        {
            SectionCaption(parent, section + "Caption", caption);
            var grid = AlfaUiFactory.Node(section + "Cards", parent, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            AlfaUiFactory.ConfigureGrid(grid.GetComponent<UnityEngine.UI.GridLayoutGroup>(), StyleCardCell, GridSpacing, 3);
            var layout = grid.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = StyleCardCell.y + 4f;
            foreach (var card in cards)
            {
                var button = factory.Button(grid.transform, "MosquitoStyle_" + section + "_" + card.variant, card.available ? card.label : "Próximamente",
                    null, AlfaButtonStyle.Secondary, StyleCardCell.y);
                var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Bottom;
                label.fontSize = AlfaUiTheme.MinTextSize;
                label.enableAutoSizing = true;
                label.fontSizeMin = AlfaUiTheme.MinTextSize;
                label.fontSizeMax = 22f;
                AlfaUiFactory.Fill(label.rectTransform, 6f, 6f, 44f, 3f);
                var picture = OptionArt(card.art);
                if (picture != null)
                {
                    var image = AlfaUiFactory.Node("OptionArt", button.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                    image.sprite = picture;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    Anchor(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(StyleCardCell.x - 24f, 38f));
                }
                if (card.available)
                {
                    // What the mosquito wears in this build: chosen (primary fill, 3-unit frame, check), nothing to change.
                    var mark = factory.Icon(button.transform, "SelectionMark", AlfaUiIconKind.Ready, AlfaUiTheme.Sheet100);
                    Anchor(mark.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(22f, 22f));
                    button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
                    MarkOption(button, true);
                    button.interactable = false;
                    continue;
                }
                var padlock = factory.Icon(button.transform, "Lock", AlfaUiIconKind.Lock, AlfaUiTheme.Sheet100);
                Anchor(padlock.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(20f, 20f));
                button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
                var dim = AlfaUiTheme.ContentAlpha(LockedCardAlpha, AlfaUiTheme.Sheet100, AlfaUiTheme.DisabledFill);
                AlfaUiFactory.LockedWhenDisabled(button, dim, AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.5f));
                button.interactable = false;
                // The picture dims with the card.
                var art = button.transform.Find("OptionArt")?.GetComponent<UnityEngine.UI.Image>();
                if (art != null) art.color = AlfaUiTheme.WithAlpha(Color.white, dim);
            }
        }

        private AlfaUiIcon AccessoryCard(Transform parent, string name, string label, AlfaUiIconKind kind)
        {
            var card = factory.Inset(parent, name, -1f, AlfaUiTheme.SmallRadius);
            AlfaUiFactory.SetSurface(card, frame: AlfaUiTheme.Sky400);
            AlfaUiFactory.MarkSelectedFrame(card, true);
            var icon = factory.Icon(card, "Icon", kind, AlfaUiTheme.Sheet100);
            Anchor(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(84f, 84f));
            var text = factory.Text(card, "Label", label, AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Bottom, true);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -12f;
            AlfaUiFactory.Fill(text.rectTransform, 8f, 8f, 104f, 10f);
            var mark = factory.Icon(card, "SelectionMark", AlfaUiIconKind.Ready, AlfaUiTheme.StatusOk);
            Anchor(mark.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(26f, 26f));
            return icon;
        }

        /// <summary>
        /// "VISTA PREVIA": the character from the front, the back and the side (UI-06 5), rendered by the viewer.
        /// Each picture is also the button that turns the big viewer to that angle.
        /// </summary>
        private void BuildPreviewRow(Transform content)
        {
            var row = factory.Vertical(content, "PreviewRow", 8f);
            row.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding = new RectOffset(0, 0, (int)PreviewTopGap, 0);
            previewRowLayout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            // About 165 x 265 per view at 1080p in both modes; shrinks (never below 150) on short canvases.
            previewRowLayout.minHeight = PreviewRowMinimum;
            previewRowLayout.preferredHeight = PreviewRowPreferred;
            previewRowLayout.flexibleHeight = 0f;
            factory.Caption(row, "PreviewCaption", "VISTA PREVIA");
            var thumbs = AlfaUiFactory.Node("PreviewThumbs", row, typeof(UnityEngine.UI.LayoutElement), typeof(AlfaUiAspectRow)).GetComponent<RectTransform>();
            var thumbsLayout = thumbs.GetComponent<UnityEngine.UI.LayoutElement>();
            thumbsLayout.minHeight = PreviewRowMinimum - 34f;
            thumbsLayout.flexibleHeight = 1f;
            var fit = thumbs.GetComponent<AlfaUiAspectRow>();
            fit.ItemAspect = PreviewThumbAspect;
            fit.LabelHeight = 32f;
            fit.Spacing = 14f;
            fit.MaxImageHeight = 400f;
            fit.MinItemAspect = 0.42f;
            var labels = new[] { "FRENTE", "ESPALDA", "LADO" };
            var names = new[] { "PreviewFrontButton", "PreviewBackButton", "PreviewSideButton" };
            var yaws = new float[3];
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var angle = PreviewAngleOrder[i];
                yaws[i] = CharacterPreviewOrbit.AngleYaw(angle);
                var button = factory.Button(thumbs, names[i], labels[i], () =>
                {
                    previewOrbit.SetAngle(angle);
                    MarkPreviewAngle(index);
                }, AlfaButtonStyle.Secondary, 180f);
                var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                label.fontSize = AlfaUiTheme.MinTextSize;
                label.alignment = TextAlignmentOptions.Bottom;
                AlfaUiFactory.Fill(label.rectTransform, 4f, 4f, 0f, 6f);
                var frame = AlfaUiFactory.Node("ViewFrame", button.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)).GetComponent<UnityEngine.UI.Image>();
                frame.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
                frame.type = UnityEngine.UI.Image.Type.Sliced;
                frame.color = new Color(0.663f, 0.722f, 0.808f, 1f);
                frame.raycastTarget = false;
                AlfaUiFactory.Fill(frame.rectTransform, 6f, 6f, 6f, 34f);
                var placeholder = factory.Icon(frame.transform, "ViewPlaceholder", AlfaUiIconKind.Human, AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.55f));
                AlfaUiFactory.Fill(placeholder.rectTransform, 18f, 18f, 24f, 24f);
                var view = AlfaUiFactory.Node("View", frame.transform, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
                view.raycastTarget = false;
                view.enabled = false;
                AlfaUiFactory.Fill(view.rectTransform);
                previewAngleViews[i] = view;
                previewAngleButtons[i] = button;
                button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
            }
            previewOrbit?.BindViews(previewAngleViews, yaws);
        }

        public void PresentCustomization(CustomizationUiState state)
        {
            customizationState = state ?? throw new ArgumentNullException(nameof(state));
            customizationSaveLatched = state.IsSaving;
            customizationControlsGroup.interactable = !customizationSaveLatched;
            customizationControlsGroup.blocksRaycasts = !customizationSaveLatched;
            if (state.Mode == CustomizationUiMode.Modular)
            {
                customizationDraft = null;
                builtPaletteSignatures.Clear();
                var roleChanged = modularEditedRole != state.EditedRole;
                modularCustomizationDraft = state.DraftSelection.Copy();
                modularEditedRole = state.EditedRole;
                EnsureModularSelectedSlot();
                RefreshModularLists(roleChanged);
            }
            else
            {
                modularCustomizationDraft = null;
                modularSelectedSlotId = string.Empty;
                builtModularCategoriesKey = builtModularOptionsKey = string.Empty;
                customizationDraft = state.Draft.Copy();
                SyncPalette(humanPaletteRoot, state.SkinColors, option => SetCustomizationColor(SkinCategory, option));
                SyncPalette(pajamaPaletteRoot, state.PajamaColors, option => SetCustomizationColor(PajamaCategory, option));
                SyncPalette(mosquitoPaletteRoot, state.MosquitoColors, option => SetCustomizationColor(MosquitoCategory, option));
                SyncPalette(mosquitoBodyPaletteRoot, state.MosquitoColors, option => SetCustomizationColor(MosquitoCategory, option));
            }
            UpdateCustomizationView();
        }

        public void ShowCustomization()
        {
            if (customizationState == null) PresentCustomization(DefaultCustomization());
            if (screen != AlfaUiScreen.Customization)
            {
                customizationReturnScreen = screen == AlfaUiScreen.Lobby && lobbyState?.IsWaiting == true
                    ? AlfaUiScreen.Lobby : AlfaUiScreen.MainMenu;
                customizationLobbyCode = customizationReturnScreen == AlfaUiScreen.Lobby ? lobbyState.RoomCode : string.Empty;
                if (customizationState.Mode == CustomizationUiMode.Modular)
                {
                    customizationSessionBaseline = null;
                    modularCustomizationSessionBaseline = modularCustomizationDraft.Copy();
                }
                else
                {
                    modularCustomizationSessionBaseline = null;
                    customizationSessionBaseline = customizationDraft.Copy();
                }
                MarkPreviewAngle(-1);
            }
            AlfaRole role = EditedCustomizationRole();
            SetScreen(AlfaUiScreen.Customization, role == AlfaRole.Human ? "CustomizationHumanButton" : "CustomizationMosquitoButton");
            previewOrbit?.Show(role);
            RefreshPreviewAvailability();
            UpdateCustomizationPreview();
        }

        private AlfaRole EditedCustomizationRole() =>
            customizationState?.Mode == CustomizationUiMode.Modular ? modularEditedRole : customizationDraft?.Role ?? AlfaRole.Human;

        private void SetCustomizationRole(AlfaRole role)
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsReadOnly) return;
            MarkPreviewAngle(-1);
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                var changed = modularEditedRole != role;
                modularEditedRole = role;
                modularSelectedSlotId = string.Empty;
                EnsureModularSelectedSlot();
                RefreshModularLists(changed);
                UpdateCustomizationView();
                // The selection is unchanged; this lets the provider update the local role preview only.
                SendModularPreview();
                return;
            }
            if (customizationDraft == null) return;
            customizationDraft.Role = role;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void SelectBasicCategory(string category)
        {
            if (customizationDraft == null) return;
            if (category == SkinCategory || category == PajamaCategory || category == AccessoriesCategory) basicHumanCategory = category;
            else if (category == MosquitoBodyCategory || category == MosquitoCategory) basicMosquitoCategory = category;
            UpdateCustomizationView();
        }

        private void SetCustomizationColor(string category, NamedColorOption option)
        {
            if (customizationDraft == null || option == null || customizationSaveLatched || customizationState?.IsReadOnly == true) return;
            if (category == SkinCategory) customizationDraft.SkinColorId = option.Id;
            else if (category == PajamaCategory) customizationDraft.PajamaColorId = option.Id;
            else customizationDraft.MosquitoColorId = option.Id;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private static CustomizationRole ToCustomizationRole(AlfaRole role) =>
            role == AlfaRole.Human ? CustomizationRole.Human : CustomizationRole.Mosquito;

        private IEnumerable<CustomizationSlotSnapshot> VisibleModularSlots()
        {
            if (customizationState?.Catalog == null) return Enumerable.Empty<CustomizationSlotSnapshot>();
            CustomizationRole role = ToCustomizationRole(modularEditedRole);
            return customizationState.Catalog.Slots.Where(slot => slot.Role == role &&
                !string.IsNullOrWhiteSpace(slot.Label) &&
                slot.Options.Any(option => !string.IsNullOrWhiteSpace(option.Label)));
        }

        private void EnsureModularSelectedSlot()
        {
            if (VisibleModularSlots().Any(slot => string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal))) return;
            modularSelectedSlotId = VisibleModularSlots().Select(slot => slot.SlotId).FirstOrDefault() ?? string.Empty;
        }

        private CustomizationSlotSnapshot CurrentModularSlot() => VisibleModularSlots().FirstOrDefault(slot =>
            string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal));

        /// <summary>
        /// Rebuilds the category rail and the option sections only when the visible catalogue or role changed;
        /// otherwise it just moves the selection marks. Every category of the role is one section of the option
        /// list (UI-06 shows wing styles and eyes together); scroll positions reset only with a structural change and
        /// keyboard focus is restored by name after a rebuild.
        /// </summary>
        private void RefreshModularLists(bool resetScroll = false)
        {
            if (modularCategoryRoot == null || modularOptionRoot == null || modularCustomizationDraft == null) return;
            var focused = FocusedNameUnder(modularCategoryRoot, modularOptionRoot);
            var slots = VisibleModularSlots().ToList();
            var categoriesKey = modularEditedRole + "|" + string.Join("|", slots.Select(slot => slot.SlotId + ":" + slot.Label + ":" + slot.WireSlotId));
            var rebuiltCategories = categoriesKey != builtModularCategoriesKey;
            if (rebuiltCategories)
            {
                AlfaUiFactory.Clear(modularCategoryRoot);
                foreach (var slot in slots)
                {
                    var capturedSlot = slot.SlotId;
                    CategoryButton(modularCategoryRoot, "ModularCategory_" + slot.WireSlotId, slot.Label, CategoryIcon(slot),
                        () => SelectModularCategory(capturedSlot));
                }
                builtModularCategoriesKey = categoriesKey;
            }
            MarkModularCategories(slots);

            var optionsKey = categoriesKey + "#" + string.Join("#", slots.Select(slot => slot.SlotId + ":" +
                string.Join("|", slot.Options.Select(option => option.OptionId + ":" + option.Label + ":" + option.HasSwatch + ":" + option.SwatchRgba))));
            var rebuiltOptions = optionsKey != builtModularOptionsKey;
            if (rebuiltOptions)
            {
                AlfaUiFactory.Clear(modularOptionRoot);
                modularOptionButtons.Clear();
                modularSections.Clear();
                modularSectionCaptions.Clear();
                foreach (var slot in slots) BuildModularSection(slot);
                builtModularOptionsKey = optionsKey;
            }
            foreach (var slot in slots) MarkModularOptions(slot);
            if (resetScroll || rebuiltCategories) ResetScrollPosition(modularCategoryRoot);
            if (resetScroll || rebuiltOptions) ResetScrollPosition(modularOptionRoot);
            if ((rebuiltCategories || rebuiltOptions) && focused != null && screen == AlfaUiScreen.Customization) Focus(focused);
        }

        private void MarkModularCategories(IEnumerable<CustomizationSlotSnapshot> slots)
        {
            foreach (var slot in slots)
            {
                var button = modularCategoryRoot.Find("ModularCategory_" + slot.WireSlotId)?.GetComponent<UnityEngine.UI.Button>();
                if (button != null) AlfaUiFactory.SetSelected(button, string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal));
            }
        }

        private string FocusedNameUnder(params Transform[] roots)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return null;
            foreach (var root in roots)
                if (root != null && selected.transform.IsChildOf(root)) return selected.name;
            return null;
        }

        /// <summary>One category: caption (with the chosen option) and its grid of swatches or picture cards.</summary>
        private void BuildModularSection(CustomizationSlotSnapshot slot)
        {
            var section = factory.Vertical(modularOptionRoot, "Section_" + slot.WireSlotId, 8f);
            modularSections[slot.SlotId] = section;
            modularSectionCaptions[slot.SlotId] = SectionCaption(section, "SectionCaption", SectionTitle(slot));
            var labelled = slot.Options.Where(item => !string.IsNullOrWhiteSpace(item.Label)).ToList();
            var swatches = labelled.Count > 0 && labelled.All(item => item.HasSwatch);
            var cell = swatches ? ModularSwatchCell : CardCell;
            var columns = swatches ? ModularSwatchColumns : CardColumns;
            var gridNode = AlfaUiFactory.Node("Grid_" + slot.WireSlotId, section, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            AlfaUiFactory.ConfigureGrid(gridNode.GetComponent<UnityEngine.UI.GridLayoutGroup>(), cell, GridSpacing, columns);
            var rows = Mathf.CeilToInt(labelled.Count / (float)columns);
            var gridLayout = gridNode.GetComponent<UnityEngine.UI.LayoutElement>();
            gridLayout.minHeight = gridLayout.preferredHeight = rows * cell.y + Mathf.Max(0, rows - 1) * GridSpacing.y + 4f;
            foreach (var option in labelled) BuildModularOption(gridNode.transform, slot, option, swatches);
        }

        /// <summary>UI-06 captions: "ESTILO DE ALAS", "OJOS"…; the chosen option follows in secondary ink.</summary>
        private string SectionTitle(CustomizationSlotSnapshot slot)
        {
            var title = CategoryIcon(slot) == AlfaUiIconKind.Wings ? "ESTILO DE ALAS" : slot.Label.ToUpperInvariant();
            var selectedId = modularCustomizationDraft?.For(ToCustomizationRole(modularEditedRole)).OptionFor(slot.SlotId);
            var chosen = slot.TryOption(selectedId ?? string.Empty, out var option) ? option.Label : string.Empty;
            return string.IsNullOrWhiteSpace(chosen) ? Escape(title) : Escape(title) + "  <color=#A8B8D8>·  " + Escape(chosen.ToUpperInvariant()) + "</color>";
        }

        private void BuildModularOption(Transform grid, CustomizationSlotSnapshot slot, CustomizationOptionSnapshot option, bool swatchGrid)
        {
            var slotId = slot.SlotId;
            var optionId = option.OptionId;
            var name = "ModularOption_" + slot.WireSlotId + "_" + option.WireOptionId;
            var button = factory.Button(grid, name, swatchGrid ? string.Empty : option.Label, () => SetModularCustomizationOption(slotId, optionId),
                AlfaButtonStyle.Secondary, swatchGrid ? ModularSwatchCell.y : CardCell.y);
            modularOptionButtons[name] = button;
            var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            if (swatchGrid)
            {
                label.gameObject.SetActive(false);
                AddOptionSwatch(button, "ColorSwatch", ColorFromRgba(option.SwatchRgba), 9f);
                return;
            }
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = AlfaUiTheme.MinTextSize;
            label.fontSizeMax = 22f;
            label.lineSpacing = -14f;
            AlfaUiFactory.Fill(label.rectTransform, 5f, 5f, 70f, 4f);
            Sprite thumbnail = option.HasSwatch ? null : customizationState.ThumbnailResolver?.Invoke(slot.SlotId, option.OptionId);
            var art = option.HasSwatch || thumbnail != null ? null : OptionArt(ClassifyOption(slot, option).ArtName);
            if (option.HasSwatch)
            {
                var swatch = AddOptionSwatch(button, "ColorSwatch", ColorFromRgba(option.SwatchRgba), 0f);
                Anchor(swatch, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(58f, 58f));
            }
            else if (thumbnail != null || art != null)
            {
                // A catalogue thumbnail ("Thumbnail") or this option's own picture ("OptionArt"), never a shared one.
                var image = AlfaUiFactory.Node(thumbnail != null ? "Thumbnail" : "OptionArt", button.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                image.sprite = thumbnail != null ? thumbnail : art;
                image.preserveAspect = true;
                image.raycastTarget = false;
                Anchor(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(CardCell.x - 18f, 64f));
            }
            else
            {
                // Nothing specific: the category pictogram keeps the card readable (the name is below).
                var icon = factory.Icon(button.transform, "Placeholder", CategoryIcon(slot), AlfaUiTheme.WithAlpha(AlfaUiTheme.Moon200, 0.55f));
                Anchor(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(60f, 60f));
            }
            var mark = factory.Icon(button.transform, "SelectionMark", AlfaUiIconKind.Ready, AlfaUiTheme.Sheet100);
            Anchor(mark.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(24f, 24f));
            button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
        }

        private Sprite OptionArt(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (optionArt.TryGetValue(name, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("AlfaUiOptionArt/" + name);
            var sprite = texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            optionArt[name] = sprite;
            return sprite;
        }

        /// <summary>
        /// Which part an option restyles and how, read from its id and label (the catalogue has no such field):
        /// wing styles (facetadas, redondas, largas, cortas), eyes (grandes, chicos, enojados, dormidos), proboscis
        /// (curva, corta, larga) and body builds (robusto, delgado). Colour options carry their swatch.
        /// </summary>
        internal static PreviewPartStyle ClassifyOption(CustomizationSlotSnapshot slot, CustomizationOptionSnapshot option)
        {
            var key = ((option.OptionId ?? string.Empty) + " " + (option.Label ?? string.Empty)).ToLowerInvariant();
            bool Has(params string[] words) => words.Any(word => key.Contains(word));
            if (option.HasSwatch) return new PreviewPartStyle(PreviewPart.Color, string.Empty, ColorFromRgba(option.SwatchRgba));
            switch (CategoryIcon(slot))
            {
                case AlfaUiIconKind.Wings:
                    return new PreviewPartStyle(PreviewPart.Wings, Has("redond", "round") ? "Round" : Has("larg", "long", "fina") ? "Long"
                        : Has("cort", "short") ? "Short" : "Faceted");
                case AlfaUiIconKind.Eye:
                    return new PreviewPartStyle(PreviewPart.Eyes, Has("enoj", "angry", "furi") ? "Angry" : Has("dorm", "sleep", "entorn", "sueñ") ? "Sleepy"
                        : Has("chic", "peque", "small") ? "Small" : "Big");
                case AlfaUiIconKind.Proboscis:
                    return new PreviewPartStyle(PreviewPart.Proboscis, Has("curv") ? "Curved" : Has("cort", "short") ? "Short"
                        : Has("larg", "long") ? "Long" : "Standard");
                case AlfaUiIconKind.Mosquito:
                    return new PreviewPartStyle(PreviewPart.Body, Has("robust", "gord", "bulky", "fuert") ? "Robust"
                        : Has("delg", "slim", "flac") ? "Slim" : "Standard");
                default:
                    return new PreviewPartStyle(PreviewPart.None, string.Empty);
            }
        }

        /// <summary>Square colour chip inside an option button, with a centred check mark when selected.</summary>
        private RectTransform AddOptionSwatch(UnityEngine.UI.Button button, string name, Color color, float inset)
        {
            var swatch = factory.Panel(button.transform, name, color, -1f, -1f, AlfaUiTheme.SmallRadius);
            var contrast = RelativeLuminance(color) > 0.45f ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100;
            AlfaUiFactory.SetSurface(swatch, Color.white, new Color(0.86f, 0.86f, 0.86f, 1f), AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.45f), Color.clear);
            AlfaUiFactory.Fill(swatch, inset, inset, inset, inset);
            var mark = factory.Icon(swatch, "SelectionMark", AlfaUiIconKind.Ready, contrast);
            Anchor(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            mark.gameObject.SetActive(false);
            button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
            return swatch;
        }

        private void MarkModularOptions(CustomizationSlotSnapshot slot)
        {
            if (slot == null || modularCustomizationDraft == null) return;
            string selectedOption = modularCustomizationDraft.For(ToCustomizationRole(modularEditedRole)).OptionFor(slot.SlotId);
            foreach (var option in slot.Options)
            {
                if (!modularOptionButtons.TryGetValue("ModularOption_" + slot.WireSlotId + "_" + option.WireOptionId, out var button) || button == null) continue;
                MarkOption(button, string.Equals(option.OptionId, selectedOption, StringComparison.Ordinal));
            }
            if (modularSectionCaptions.TryGetValue(slot.SlotId, out var caption) && caption != null) caption.text = SectionTitle(slot);
        }

        /// <summary>Selected option: primary fill, 3-unit accent frame and a check mark (never colour alone).</summary>
        private static void MarkOption(UnityEngine.UI.Button button, bool selected)
        {
            if (button == null) return;
            AlfaUiFactory.SetSelected(button, selected, AlfaButtonStyle.Secondary);
            foreach (var mark in button.GetComponentsInChildren<AlfaUiIcon>(true))
                if (mark.name == "SelectionMark") mark.gameObject.SetActive(selected);
        }

        private static AlfaUiIconKind CategoryIcon(CustomizationSlotSnapshot slot)
        {
            var key = ((slot.SlotId ?? string.Empty) + " " + (slot.Label ?? string.Empty)).ToLowerInvariant();
            bool Has(params string[] words) => words.Any(word => key.Contains(word));
            if (Has("wing", "ala")) return AlfaUiIconKind.Wings;
            if (Has("eye", "ojo")) return AlfaUiIconKind.Eye;
            if (Has("probosc", "trompa", "aguij", "sting")) return AlfaUiIconKind.Proboscis;
            if (Has("hat", "gorro", "gorra", "sombrero", "cap", "casco")) return AlfaUiIconKind.Hat;
            if (Has("skin", "piel", "tono", "cara", "face")) return AlfaUiIconKind.Face;
            if (Has("shoe", "slipper", "pantufla", "calzado")) return AlfaUiIconKind.Slipper;
            if (Has("pajama", "pijama", "outfit", "ropa", "shirt", "remera", "camis")) return AlfaUiIconKind.Customize;
            if (Has("color", "tint", "pintura", "marca", "mark")) return AlfaUiIconKind.Palette;
            if (Has("base", "body", "cuerpo")) return slot.Role == CustomizationRole.Mosquito ? AlfaUiIconKind.Mosquito : AlfaUiIconKind.Human;
            return AlfaUiIconKind.Palette;
        }

        private static void ResetScrollPosition(RectTransform content)
        {
            var scroll = content != null ? content.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll == null) return;
            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = 0f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private static Color ColorFromRgba(uint value) => new Color(
            ((value >> 24) & 255) / 255f,
            ((value >> 16) & 255) / 255f,
            ((value >> 8) & 255) / 255f,
            (value & 255) / 255f);

        /// <summary>The rail jumps to the category's section (its top at the top of the list) and marks it.</summary>
        private void SelectModularCategory(string slotId)
        {
            if (customizationState?.Mode != CustomizationUiMode.Modular || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (!VisibleModularSlots().Any(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))) return;
            modularSelectedSlotId = slotId;
            RefreshModularLists();
            ScrollToModularSection(slotId);
            UpdateCustomizationView();
        }

        private void ScrollToModularSection(string slotId)
        {
            if (!modularSections.TryGetValue(slotId, out var section) || section == null) return;
            var scroll = modularOptionRoot.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null || scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(modularOptionRoot);
            var overflow = modularOptionRoot.rect.height - scroll.viewport.rect.height;
            scroll.StopMovement();
            if (overflow <= 0.5f) { scroll.verticalNormalizedPosition = 1f; return; }
            var top = Mathf.Clamp(-section.anchoredPosition.y - section.rect.height * (1f - section.pivot.y) - 6f, 0f, overflow);
            scroll.verticalNormalizedPosition = 1f - top / overflow;
        }

        private void SetModularCustomizationOption(string slotId, string optionId)
        {
            if (customizationState?.Mode != CustomizationUiMode.Modular || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (!customizationState.Catalog.TrySlot(slotId, out var slot) || slot.Role != ToCustomizationRole(modularEditedRole) ||
                !slot.TryOption(optionId, out var option) || string.IsNullOrWhiteSpace(option.Label)) return;
            var next = modularCustomizationDraft.Copy();
            next.For(ToCustomizationRole(modularEditedRole)).SetOption(slotId, optionId);
            if (!customizationState.Catalog.TryNormalize(next, out var normalized, out var error))
            {
                customizationStatus.text = "No se pudo seleccionar esa opción. Probá otra.";
                return;
            }
            modularCustomizationDraft = normalized;
            modularSelectedSlotId = slotId;
            RefreshModularLists();
            UpdateCustomizationView();
            SendModularPreview();
        }

        private void SendModularPreview()
        {
            if (actions is IModularCustomizationActions modularActions)
            {
                modularActions.PreviewModularCustomization(modularCustomizationDraft.Copy(), modularEditedRole);
                return;
            }
            customizationStatus.text = "Esta selección todavía no se puede guardar.";
        }

        /// <summary>ALEATORIO: a random option per visible category of the role being edited.</summary>
        private void RandomizeCustomization()
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                if (modularCustomizationDraft == null) return;
                var role = ToCustomizationRole(modularEditedRole);
                var next = modularCustomizationDraft.Copy();
                foreach (var slot in VisibleModularSlots().ToList())
                {
                    var choices = slot.Options.Where(item => !string.IsNullOrWhiteSpace(item.Label)).Select(item => item.OptionId).ToList();
                    var pick = RandomOther(choices, next.For(role).OptionFor(slot.SlotId));
                    if (pick == null) continue;
                    var attempt = next.Copy();
                    attempt.For(role).SetOption(slot.SlotId, pick);
                    if (customizationState.Catalog.TryNormalize(attempt, out var normalized, out _)) next = normalized;
                }
                modularCustomizationDraft = next;
                RefreshModularLists();
                UpdateCustomizationView();
                SendModularPreview();
                return;
            }
            if (customizationDraft == null) return;
            if (customizationDraft.Role == AlfaRole.Human)
            {
                customizationDraft.SkinColorId = RandomOther(customizationState.SkinColors.Select(item => item.Id).ToList(), customizationDraft.SkinColorId) ?? customizationDraft.SkinColorId;
                customizationDraft.PajamaColorId = RandomOther(customizationState.PajamaColors.Select(item => item.Id).ToList(), customizationDraft.PajamaColorId) ?? customizationDraft.PajamaColorId;
            }
            else
                customizationDraft.MosquitoColorId = RandomOther(customizationState.MosquitoColors.Select(item => item.Id).ToList(), customizationDraft.MosquitoColorId) ?? customizationDraft.MosquitoColorId;
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private string RandomOther(IReadOnlyList<string> choices, string current)
        {
            if (choices == null || choices.Count == 0) return null;
            var others = choices.Where(choice => !string.Equals(choice, current, StringComparison.Ordinal)).ToList();
            var pool = others.Count > 0 ? others : choices.ToList();
            return pool[customizationRandom.Next(pool.Count)];
        }

        /// <summary>
        /// Palette row (UI-06 5): a 3-column grid of 64-unit swatches on the left and, beside it, the chosen colour
        /// shown big with its name. Returns the grid (its children are the "Color_&lt;id&gt;" buttons).
        /// </summary>
        private RectTransform CreatePaletteLayout(Transform parent, string name, int columns = SwatchColumns)
        {
            var row = factory.Horizontal(parent, name + "Row", 24f, TextAnchor.UpperLeft);
            var node = AlfaUiFactory.Node(name, row, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            var grid = node.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            AlfaUiFactory.ConfigureGrid(grid, SwatchCell, GridSpacing, columns);
            var gridLayout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            gridLayout.minWidth = gridLayout.preferredWidth = columns * SwatchCell.x + (columns - 1) * GridSpacing.x + 4f;
            gridLayout.flexibleWidth = 0f;

            var choice = factory.Inset(row, name.Replace("Palette", "Choice"), -1f, AlfaUiTheme.ButtonRadius);
            var choiceLayout = choice.GetComponent<UnityEngine.UI.LayoutElement>();
            choiceLayout.flexibleWidth = 1f;
            choiceLayout.minHeight = choiceLayout.preferredHeight = 2f * SwatchCell.y + GridSpacing.y;
            var caption = factory.Caption(choice, "Caption", "ELEGIDO");
            caption.alignment = TextAlignmentOptions.Center;
            Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-16f, 26f));
            var swatch = factory.Panel(choice, "ChosenSwatch", Color.white, -1f, -1f, AlfaUiTheme.ButtonRadius);
            AlfaUiFactory.SetSurface(swatch, Color.white, new Color(0.86f, 0.86f, 0.86f, 1f), AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.45f), AlfaUiTheme.WithAlpha(Color.black, 0.35f));
            Anchor(swatch, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(84f, 84f));
            var chosenName = factory.Text(choice, "ChosenName", string.Empty, 26f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Top, true);
            chosenName.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(chosenName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(-16f, 32f));
            var rect = node.GetComponent<RectTransform>();
            paletteChoices[rect] = (swatch.GetComponent<UnityEngine.UI.Image>(), chosenName);
            return rect;
        }

        /// <summary>Builds a palette grid only when its options changed (ids, names or colours); otherwise keeps it.</summary>
        private void SyncPalette(RectTransform parent, IReadOnlyList<NamedColorOption> options, Action<NamedColorOption> selected)
        {
            var signature = string.Join("|", options.Select(option => option.Id + ":" + option.Label + ":" + ColorUtility.ToHtmlStringRGBA(option.Color)));
            if (builtPaletteSignatures.TryGetValue(parent, out var built) && built == signature) return;
            var focused = FocusedNameUnder(parent);
            AlfaUiFactory.Clear(parent);
            foreach (var option in options)
            {
                var captured = option;
                var button = factory.Button(parent, "Color_" + option.Id, string.Empty, () => selected(captured), AlfaButtonStyle.Secondary, SwatchCell.y);
                button.transform.Find("Label").gameObject.SetActive(false);
                AddOptionSwatch(button, "Swatch", option.Color, SwatchInset);
            }
            var columns = Mathf.Max(1, parent.GetComponent<UnityEngine.UI.GridLayoutGroup>().constraintCount);
            var rows = Mathf.CeilToInt(options.Count / (float)columns);
            var layout = parent.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = rows * SwatchCell.y + Mathf.Max(0, rows - 1) * GridSpacing.y + 4f;
            builtPaletteSignatures[parent] = signature;
            if (focused != null && screen == AlfaUiScreen.Customization) Focus(focused);
        }

        private void MarkPalette(RectTransform parent, string selectedId, IReadOnlyList<NamedColorOption> options)
        {
            foreach (Transform child in parent)
                MarkOption(child.GetComponent<UnityEngine.UI.Button>(), child.name == "Color_" + selectedId);
            if (!paletteChoices.TryGetValue(parent, out var choice)) return;
            var chosen = options?.FirstOrDefault(option => option.Id == selectedId);
            choice.swatch.color = chosen != null ? chosen.Color : AlfaUiTheme.Night600;
            choice.name.text = chosen != null ? chosen.Label : "—";
        }

        private void UpdateCustomizationView()
        {
            if (customizationState == null) return;
            bool modular = customizationState.Mode == CustomizationUiMode.Modular;
            if (modular && modularCustomizationDraft == null) return;
            if (!modular && customizationDraft == null) return;
            var role = modular ? modularEditedRole : customizationDraft.Role;
            var human = role == AlfaRole.Human;
            bool editable = !customizationState.IsReadOnly;
            humanCustomizationFields.SetActive(editable && !modular && human);
            mosquitoCustomizationFields.SetActive(editable && !modular && !human);
            modularCustomizationFields.SetActive(editable && modular);
            basicCategoryRail.SetActive(editable && !modular);
            modularCategoryRail.SetActive(editable && modular);
            skinPaletteGroup.SetActive(basicHumanCategory == SkinCategory);
            pajamaPaletteGroup.SetActive(basicHumanCategory == PajamaCategory);
            accessoriesGroup.SetActive(basicHumanCategory == AccessoriesCategory);
            foreach (var key in new[] { SkinCategory, PajamaCategory, AccessoriesCategory }) basicCategoryButtons[key].gameObject.SetActive(human);
            foreach (var key in new[] { MosquitoBodyCategory, MosquitoCategory }) basicCategoryButtons[key].gameObject.SetActive(!human);
            mosquitoBodyGroup.SetActive(basicMosquitoCategory == MosquitoBodyCategory);
            mosquitoColorsGroup.SetActive(basicMosquitoCategory == MosquitoCategory);
            var basicCategory = human ? basicHumanCategory : basicMosquitoCategory;
            foreach (var pair in basicCategoryButtons) AlfaUiFactory.SetSelected(pair.Value, pair.Key == basicCategory);
            // Basic mode: spare height under ALEATORIO; modular: the list takes it.
            modularFieldsLayout.flexibleHeight = modular ? 1f : 0f;
            optionsSpacer.SetActive(!modular);
            // The views keep the human's tall cards (about 165 x 265), except under the mosquito's full CUERPO column,
            // where the room left is short: there they are wide, as the insect seen from the side.
            var aspectRow = previewRowLayout != null ? previewRowLayout.GetComponentInChildren<AlfaUiAspectRow>(true) : null;
            if (aspectRow != null)
            {
                var wide = !modular && !human && basicMosquitoCategory == MosquitoBodyCategory;
                aspectRow.ItemAspect = wide ? 1.25f : PreviewThumbAspect;
                aspectRow.MinItemAspect = wide ? 0.95f : 0.36f;
                aspectRow.Arrange();
            }

            customizationHumanButton.interactable = editable && !customizationSaveLatched;
            customizationMosquitoButton.interactable = editable && !customizationSaveLatched;
            SetRoleTabSelection(customizationHumanButton, human, AlfaRole.Human);
            SetRoleTabSelection(customizationMosquitoButton, !human, AlfaRole.Mosquito);

            string selectionName;
            if (!editable || modular) selectionName = string.Empty;
            else if (human && basicHumanCategory == AccessoriesCategory) selectionName = string.Empty;
            else
            {
                var palette = !human ? customizationState.MosquitoColors : basicHumanCategory == SkinCategory ? customizationState.SkinColors : customizationState.PajamaColors;
                var selectedId = !human ? customizationDraft.MosquitoColorId : basicHumanCategory == SkinCategory ? customizationDraft.SkinColorId : customizationDraft.PajamaColorId;
                selectionName = palette.FirstOrDefault(option => option.Id == selectedId)?.Label ?? string.Empty;
            }
            customizationCategoryTitle.text = !editable ? "PERSONALIZACIÓN NO DISPONIBLE" : modular && !VisibleModularSlots().Any()
                ? "SIN OPCIONES DISPONIBLES" : "OPCIONES DEL " + (human ? "HUMANO" : "MOSQUITO");
            // In the modular list every section has its own caption: the panel title would only push rows off.
            // The mosquito's CUERPO column has its own section captions (UI-06 6), like the modular list.
            customizationCategoryTitle.gameObject.SetActive(!editable || (!modular && !(!human && basicMosquitoCategory == MosquitoBodyCategory)) ||
                (modular && !VisibleModularSlots().Any()));
            customizationSelectionLabel.text = selectionName;
            // Basic palettes show the chosen colour big beside the grid; the line is for other selections only.
            customizationSelectionLabel.transform.parent.gameObject.SetActive(modular && !string.IsNullOrEmpty(selectionName));
            // The nightcap is always red (it is not tinted by the clothes colour).
            if (accessoryHatIcon != null) accessoryHatIcon.color = NightcapRed;
            foreach (var view in previewAngleViews)
            {
                var placeholder = view != null ? view.transform.parent.Find("ViewPlaceholder")?.GetComponent<AlfaUiIcon>() : null;
                if (placeholder != null) placeholder.Kind = human ? AlfaUiIconKind.Human : AlfaUiIconKind.Mosquito;
            }

            previewOrbit?.Show(role);
            RefreshPreviewAvailability();
            UpdateCustomizationPreview();
            if (customizationSaveLatched) customizationStatus.text = "Aplicando apariencia…";
            else if (customizationState.IsReadOnly)
                customizationStatus.text = string.IsNullOrWhiteSpace(customizationState.Message)
                    ? "Esta personalización todavía no está disponible en esta versión."
                    : customizationState.Message;
            else customizationStatus.text = customizationState.Message;
            customizationStatus.gameObject.SetActive(!string.IsNullOrWhiteSpace(customizationStatus.text));

            bool dirty = CustomizationDirty();
            customizationSaveButton.interactable = editable && !customizationSaveLatched && dirty;
            customizationSaveLabel.text = customizationSaveLatched ? "APLICANDO…" : "APLICAR";
            customizationResetButton.interactable = editable && !customizationSaveLatched;
            customizationRandomButton.interactable = editable && !customizationSaveLatched && (!modular || VisibleModularSlots().Any());
            if (!modular)
            {
                MarkPalette(humanPaletteRoot, customizationDraft.SkinColorId, customizationState.SkinColors);
                MarkPalette(pajamaPaletteRoot, customizationDraft.PajamaColorId, customizationState.PajamaColors);
                MarkPalette(mosquitoPaletteRoot, customizationDraft.MosquitoColorId, customizationState.MosquitoColors);
                MarkPalette(mosquitoBodyPaletteRoot, customizationDraft.MosquitoColorId, customizationState.MosquitoColors);
            }
            if (editable) UpdateRoleSummary(human, modular);
            LayoutCustomizationRail();
            LayoutCustomizationOptions();
        }

        /// <summary>
        /// Basic mode: VISTA PREVIA sits 16 units under the options and takes the height they leave, from its
        /// minimum up to 200 units over its preferred size (its views grow taller); whatever is left goes under
        /// ALEATORIO. When even the minimum does not fit (the mosquito's full CUERPO column on a short 21:9 canvas)
        /// the row is hidden rather than squeezed: the big viewer already shows the character.
        /// </summary>
        private void LayoutCustomizationOptions()
        {
            if (previewRowLayout == null) return;
            var content = (RectTransform)previewRowLayout.transform.parent;
            var height = content.rect.height;
            if (height <= 1f) return;
            customizationLaidOutOptionsHeight = height;
            var modular = customizationState?.Mode == CustomizationUiMode.Modular;
            var row = previewRowLayout.gameObject;
            if (modular)
            {
                if (!row.activeSelf) row.SetActive(true);
                previewRowLayout.preferredHeight = PreviewRowPreferred;
                return;
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var group = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            var used = 0f;
            var count = 0;
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf && child.gameObject != row) continue;
                var element = child.GetComponent<UnityEngine.UI.LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                count++;
                if (child.gameObject == optionsSpacer || child.gameObject == row) continue;
                used += UnityEngine.UI.LayoutUtility.GetPreferredHeight((RectTransform)child);
            }
            used += group.spacing * Mathf.Max(0, count - 1) + group.padding.vertical;
            var available = height - used;
            var fits = available >= PreviewRowMinimum;
            if (row.activeSelf != fits) row.SetActive(fits);
            // Never taller than its views can use (their width caps their height): the rest goes under ALEATORIO.
            var thumbs = row.GetComponentInChildren<AlfaUiAspectRow>(true);
            var useful = thumbs != null
                ? row.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding.vertical + 8f +
                  UnityEngine.UI.LayoutUtility.GetPreferredHeight((RectTransform)row.transform.Find("PreviewCaption")) + thumbs.MaxUsefulHeight(content.rect.width)
                : PreviewRowPreferred + PreviewExtraMaximum;
            previewRowLayout.preferredHeight = fits ? Mathf.Clamp(Mathf.Min(available, useful), PreviewRowMinimum, PreviewRowPreferred + PreviewExtraMaximum) : PreviewRowMinimum;
        }

        /// <summary>
        /// Keeps the viewer in step with the draft: the angle views refresh and, in modular mode while the game
        /// cannot assemble the real parts (no certified applicator), the mosquito shows an approximation of the
        /// chosen shapes and colour, flagged by the "VISTA APROXIMADA" chip.
        /// </summary>
        private void UpdateCustomizationPreview()
        {
            if (previewOrbit == null || customizationState == null) return;
            List<PreviewPartStyle> styles = null;
            if (customizationState.Mode == CustomizationUiMode.Modular && !customizationState.ModularPreviewAvailable && modularCustomizationDraft != null &&
                modularEditedRole == AlfaRole.Mosquito)
            {
                styles = new List<PreviewPartStyle>();
                var loadout = modularCustomizationDraft.For(CustomizationRole.Mosquito);
                foreach (var slot in VisibleModularSlots())
                {
                    if (!slot.TryOption(loadout.OptionFor(slot.SlotId) ?? string.Empty, out var option)) continue;
                    var style = ClassifyOption(slot, option);
                    if (style.Part != PreviewPart.None) styles.Add(style);
                }
            }
            previewOrbit.SetApproximation(styles);
            if (customizationApproximateChip != null)
                customizationApproximateChip.SetActive(styles != null && styles.Count > 0 && previewOrbit.IsBound);
            previewOrbit.RequestViews();
        }

        private void UpdateOptionsFade() => UpdateScrollFade(modularOptionScroll, modularOptionFade);

        /// <summary>Per-frame upkeep while customizing: the options fade and the rail layout after a resize.</summary>
        private void UpdateCustomizationLayout()
        {
            UpdateOptionsFade();
            if (customizationRail != null && Mathf.Abs(customizationRail.rect.height - customizationLaidOutRailHeight) > 0.5f) LayoutCustomizationRail();
            var content = previewRowLayout != null ? (RectTransform)previewRowLayout.transform.parent : null;
            if (content != null && Mathf.Abs(content.rect.height - customizationLaidOutOptionsHeight) > 0.5f) LayoutCustomizationOptions();
        }

        /// <summary>Role tab: HUMANO selected is primary blue, MOSQUITO selected is danger red; both with the 3-unit frame.</summary>
        private static void SetRoleTabSelection(UnityEngine.UI.Button button, bool selected, AlfaRole role)
        {
            var human = role == AlfaRole.Human;
            AlfaUiFactory.ApplyStyle(button, selected ? human ? AlfaButtonStyle.Primary : AlfaButtonStyle.Danger : AlfaButtonStyle.Secondary);
            AlfaUiFactory.MarkSelectedFrame(button, selected);
            var surface = button.GetComponent<AlfaUiSurface>();
            if (selected && !human && surface != null)
            {
                surface.FrameColor = AlfaUiTheme.DangerBorder;
                surface.FocusFrameColor = Color.Lerp(AlfaUiTheme.DangerBorder, Color.white, 0.5f);
                surface.ShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.DangerHi, 0.45f);
                surface.Refresh();
            }
            var icon = button.transform.Find("IconPlate/Icon")?.GetComponent<AlfaUiIcon>();
            if (icon != null && !selected) icon.color = human ? AlfaUiTheme.Sky400 : AlfaUiTheme.Pajama500;
            var rail = button.transform.Find("FocusRail");
            if (rail != null) rail.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.WithAlpha(Color.white, 0f);
            button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
        }

        /// <summary>APLICAR publishes a changed look; with nothing changed it is dimmed and does nothing.</summary>
        private void CustomizationPrimaryAction()
        {
            if (customizationState == null || customizationSaveLatched) return;
            if (!customizationState.IsReadOnly && CustomizationDirty()) SaveCustomization();
        }

        private void SaveCustomization()
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsSaving || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                if (modularCustomizationDraft == null) return;
                var modularActions = actions as IModularCustomizationActions;
                if (modularActions == null)
                {
                    customizationStatus.text = "Esta selección todavía no se puede guardar.";
                    return;
                }
                customizationSaveLatched = true;
                customizationSaveButton.interactable = false;
                customizationSaveLabel.text = "APLICANDO…";
                customizationStatus.text = "Aplicando apariencia…";
                modularActions.SaveModularCustomization(modularCustomizationDraft.Copy(), modularEditedRole);
                return;
            }
            if (customizationDraft == null) return;
            customizationSaveLatched = true;
            customizationSaveButton.interactable = false;
            customizationSaveLabel.text = "APLICANDO…";
            customizationStatus.text = "Aplicando apariencia…";
            actions.SaveCustomization(customizationDraft.Copy());
        }

        private void ResetCustomization()
        {
            if (customizationState == null || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (customizationState.Mode == CustomizationUiMode.Modular)
            {
                modularCustomizationDraft = (modularCustomizationSessionBaseline ?? customizationState.DraftSelection).Copy();
                RefreshModularLists();
                UpdateCustomizationView();
                SendModularPreview();
                return;
            }
            customizationDraft = (customizationSessionBaseline ?? customizationState.Draft).Copy();
            UpdateCustomizationView();
            actions.PreviewCustomization(customizationDraft.Copy());
        }

        private void CloseCustomization()
        {
            if (customizationSaveLatched) return;
            var returnScreen = customizationReturnScreen;
            var returnLobbyCode = customizationLobbyCode;
            customizationSessionBaseline = null;
            modularCustomizationSessionBaseline = null;
            modularSelectedSlotId = string.Empty;
            customizationReturnScreen = AlfaUiScreen.MainMenu;
            customizationLobbyCode = string.Empty;
            if (returnScreen == AlfaUiScreen.Lobby && lobbyState?.IsWaiting == true && string.Equals(lobbyState.RoomCode, returnLobbyCode, StringComparison.Ordinal))
                SetScreen(AlfaUiScreen.Lobby, "LobbyReadyButton");
            else ShowMainMenu();
        }

        private bool CustomizationDirty() => customizationState != null &&
            (customizationState.Mode == CustomizationUiMode.Modular
                ? modularCustomizationDraft != null && !modularCustomizationDraft.CanonicalEquals(customizationState.PublishedSelection)
                : customizationDraft != null && !customizationDraft.SameValues(customizationState.Saved));

        private static CustomizationUiState DefaultCustomization()
        {
            var skin = new[]
            {
                new NamedColorOption("light", "Claro", AlfaUiTheme.Hex("E8B380")),
                new NamedColorOption("warm", "Cálido", AlfaUiTheme.Hex("C98B5A")),
                new NamedColorOption("dark", "Oscuro", AlfaUiTheme.Hex("451F12"))
            };
            var pajamas = new[]
            {
                new NamedColorOption("blue", "Azul", AlfaUiTheme.Hex("2D4F9A")),
                new NamedColorOption("red", "Rojo", AlfaUiTheme.Hex("A62B29")),
                new NamedColorOption("green", "Verde", AlfaUiTheme.Hex("2E6B45"))
            };
            var mosquitoes = new[]
            {
                new NamedColorOption("red", "Rojo", AlfaUiTheme.Hex("9E2228")),
                new NamedColorOption("blue", "Azul", AlfaUiTheme.Hex("2B4C85")),
                new NamedColorOption("green", "Oliva", AlfaUiTheme.Hex("4F5C2E"))
            };
            var saved = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
            return new CustomizationUiState(skin, pajamas, mosquitoes, saved);
        }
    }

    /// <summary>
    /// Lays out a row of equal picture buttons (image above, label below) at a fixed image aspect, as large as the
    /// row allows and centred, without a layout group (the "VISTA PREVIA" angle views).
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AlfaUiAspectRow : MonoBehaviour
    {
        internal float ItemAspect = 0.73f;
        /// <summary>Narrowest image aspect when the row is taller than its width needs (the views grow taller).</summary>
        internal float MinItemAspect = 0.73f;
        internal float LabelHeight = 32f;
        internal float Spacing = 12f;
        internal float MaxImageHeight = 230f;

        private void OnRectTransformDimensionsChange() => Arrange();
        private void OnEnable() => Arrange();
        private void OnTransformChildrenChanged() => Arrange();

        /// <summary>Tallest the row can use at a given width: the views' width caps their height (MinItemAspect).</summary>
        internal float MaxUsefulHeight(float width)
        {
            var count = 0;
            foreach (Transform child in transform) if (child.gameObject.activeSelf) count++;
            if (count == 0 || width <= 1f) return LabelHeight + 12f + MaxImageHeight;
            var imageWidth = (width - Spacing * (count - 1)) / count - 12f;
            return LabelHeight + 12f + Mathf.Min(MaxImageHeight, imageWidth / Mathf.Max(0.05f, MinItemAspect));
        }

        internal void Arrange()
        {
            var rect = (RectTransform)transform;
            var count = 0;
            foreach (Transform child in transform) if (child.gameObject.activeSelf) count++;
            if (count == 0 || rect.rect.width <= 1f || rect.rect.height <= 1f) return;
            var maxWidth = (rect.rect.width - Spacing * (count - 1)) / count;
            // The item is the image plus a 12-unit frame: never wider than its share of the row (it used to start
            // 18 units left of the content margin and reach the panel edge at 720p and 5:4). A taller row makes the
            // images taller (down to MinItemAspect) instead of leaving empty height.
            var imageHeight = Mathf.Max(40f, Mathf.Min(MaxImageHeight, rect.rect.height - LabelHeight - 12f));
            var imageWidth = Mathf.Min(maxWidth - 12f, imageHeight * ItemAspect);
            if (imageWidth < imageHeight * MinItemAspect) imageHeight = Mathf.Max(40f, imageWidth / Mathf.Max(0.05f, MinItemAspect));
            var itemWidth = imageWidth + 12f;
            var itemHeight = imageHeight + LabelHeight + 12f;
            var total = itemWidth * count + Spacing * (count - 1);
            var x = -total * 0.5f + itemWidth * 0.5f;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var item = (RectTransform)child;
                item.anchorMin = item.anchorMax = new Vector2(0.5f, 1f);
                item.pivot = new Vector2(0.5f, 1f);
                item.anchoredPosition = new Vector2(x, 0f);
                item.sizeDelta = new Vector2(itemWidth, itemHeight);
                x += itemWidth + Spacing;
            }
        }
    }
}
