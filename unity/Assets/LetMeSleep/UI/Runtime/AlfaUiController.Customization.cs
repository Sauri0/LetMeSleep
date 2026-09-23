using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LetMeSleep.Core.Customization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Customization (UI-06 screens 5-6, PER-08): role tabs HUMANO (blue) / MOSQUITO (red) on top, a category rail
    /// with icons on the left, the 3D viewer on a pedestal in the middle (drag to rotate, FRENTE / PERFIL / ESPALDA)
    /// and the options on the right as a grid of square swatches or thumbnail cards, with ALEATORIO, DESHACER and
    /// the green LISTO / APLICAR call to action. Works with the basic palettes and with a modular catalogue.
    /// Presentations never rebuild controls that did not change, so scroll position and keyboard focus survive
    /// each selection (ui-presentation-audio-8).
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private const string SkinCategory = "skin";
        private const string PajamaCategory = "pajama";
        private const string MosquitoCategory = "mosquito";
        private const float CustomizationHeaderHeight = 84f;
        private const float CustomizationColumnsTop = CustomizationHeaderHeight + 22f;
        private const float CustomizationRailWidth = 300f;
        private const float CustomizationOptionsWidth = 560f;
        private const float CustomizationGap = 22f;
        private static readonly Vector2 SwatchCell = new Vector2(80f, 80f);
        private static readonly Vector2 CardCell = new Vector2(150f, 150f);
        private static readonly Vector2 GridSpacing = new Vector2(12f, 12f);

        private CharacterPreviewOrbit previewOrbit;
        private GameObject customizationPreviewUnavailable;
        private TextMeshProUGUI customizationCategoryTitle;
        private TextMeshProUGUI customizationOptionsTitle;
        private TextMeshProUGUI customizationSelectionLabel;
        private TextMeshProUGUI customizationStatus;
        private RectTransform humanPaletteRoot;
        private RectTransform pajamaPaletteRoot;
        private RectTransform mosquitoPaletteRoot;
        private GameObject skinPaletteGroup;
        private GameObject pajamaPaletteGroup;
        private GameObject humanCustomizationFields;
        private GameObject mosquitoCustomizationFields;
        private GameObject modularCustomizationFields;
        private GameObject basicCategoryRail;
        private GameObject modularCategoryRail;
        private RectTransform modularCategoryRoot;
        private RectTransform modularOptionRoot;
        private UnityEngine.UI.GridLayoutGroup modularOptionGrid;
        private UnityEngine.UI.Button customizationHumanButton;
        private UnityEngine.UI.Button customizationMosquitoButton;
        private CanvasGroup customizationControlsGroup;
        private UnityEngine.UI.Button customizationSaveButton;
        private TextMeshProUGUI customizationSaveLabel;
        private UnityEngine.UI.Button customizationResetButton;
        private UnityEngine.UI.Button customizationRandomButton;
        private readonly Dictionary<string, UnityEngine.UI.Button> basicCategoryButtons = new Dictionary<string, UnityEngine.UI.Button>();
        private readonly Dictionary<RectTransform, string> builtPaletteSignatures = new Dictionary<RectTransform, string>();
        private readonly UnityEngine.UI.Button[] previewAngleButtons = new UnityEngine.UI.Button[3];
        private string basicHumanCategory = SkinCategory;
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
            customizationHumanButton = factory.FeatureButton(roleTabs, "CustomizationHumanButton", "HUMANO", "PIJAMA, PANTUFLAS Y GORRO",
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

            var basic = factory.Vertical(rail, "BasicCategories", 10f);
            AlfaUiFactory.Fill(basic, 14f, 14f, 14f, 96f);
            basicCategoryRail = basic.gameObject;
            basicCategoryButtons[SkinCategory] = CategoryButton(basic, "CustomizationCategory_" + SkinCategory, "TONO DE PIEL",
                AlfaUiIconKind.Face, () => SelectBasicCategory(SkinCategory));
            basicCategoryButtons[PajamaCategory] = CategoryButton(basic, "CustomizationCategory_" + PajamaCategory, "PIJAMA Y GORRO",
                AlfaUiIconKind.Customize, () => SelectBasicCategory(PajamaCategory));
            basicCategoryButtons[MosquitoCategory] = CategoryButton(basic, "CustomizationCategory_" + MosquitoCategory, "COLOR DEL CUERPO",
                AlfaUiIconKind.Palette, () => SelectBasicCategory(MosquitoCategory));

            var modular = AlfaUiFactory.Node("ModularCategories", rail).GetComponent<RectTransform>();
            AlfaUiFactory.Fill(modular, 12f, 12f, 12f, 92f);
            modularCategoryRail = modular.gameObject;
            var categoryScroll = factory.ScrollView(modular, "CategoryScroll", out modularCategoryRoot, 400f, true);
            AlfaUiFactory.Fill(categoryScroll);
            modularCategoryRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 10f;
            modularCategoryRail.SetActive(false);

            var back = factory.Button(rail, "CustomizationBackButton", "VOLVER", CloseCustomization, AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Back);
            Anchor((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-28f, 64f));
        }

        private UnityEngine.UI.Button CategoryButton(Transform parent, string name, string label, AlfaUiIconKind icon, UnityEngine.Events.UnityAction callback)
        {
            var button = factory.Button(parent, name, label, callback, AlfaButtonStyle.Tab, 78f, icon);
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
            AlfaUiFactory.Fill(viewportRect, 12f, 12f, 12f, 88f);
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

            // Soft studio falloff towards the panel edges (UI-06 viewer); never intercepts the drag.
            var vignette = AlfaUiFactory.Node("PreviewVignette", viewport.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            vignette.sprite = AlfaUiFactory.RadialVignetteSprite();
            vignette.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Night800, 0.9f);
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
            hint.anchoredPosition = new Vector2(0f, 16f);
            hint.sizeDelta = new Vector2(0f, 44f);
            HintIcon(hint, "HintLeft", AlfaUiIconKind.ChevronLeft, 20f);
            HintIcon(hint, "HintMouse", AlfaUiIconKind.Mouse, 28f);
            var hintText = factory.Text(hint, "OrbitHint", "ARRASTRÁ PARA GIRAR", AlfaUiTheme.MinTextSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.characterSpacing = AlfaUiTheme.CaptionTracking;
            hintText.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            HintIcon(hint, "HintRight", AlfaUiIconKind.ChevronRight, 20f);

            var unavailable = factory.Text(previewPanel, "PreviewUnavailable", "El visor 3D se conecta al personaje del juego.", AlfaUiTheme.BodySize,
                AlfaUiTheme.Moon200, TextAlignmentOptions.Center);
            Anchor(unavailable.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            customizationPreviewUnavailable = unavailable.gameObject;
            RefreshPreviewAvailability();

            var angles = factory.Horizontal(previewPanel, "PreviewAngles", 10f, TextAnchor.MiddleCenter);
            angles.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(angles, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-24f, 60f));
            previewAngleButtons[0] = AngleButton(angles, "PreviewFrontButton", "FRENTE", 0, PreviewAngle.Front);
            previewAngleButtons[1] = AngleButton(angles, "PreviewSideButton", "PERFIL", 1, PreviewAngle.Side);
            previewAngleButtons[2] = AngleButton(angles, "PreviewBackButton", "ESPALDA", 2, PreviewAngle.Back);
            var reset = factory.Button(angles, "PreviewResetButton", "CENTRAR", () =>
            {
                previewOrbit.ResetView();
                MarkPreviewAngle(-1);
            }, AlfaButtonStyle.Tab, 60f, AlfaUiIconKind.Refresh);
            reset.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
        }

        private void HintIcon(Transform parent, string name, AlfaUiIconKind kind, float size)
        {
            var icon = factory.Icon(parent, name, kind, AlfaUiTheme.Sheet100);
            var element = icon.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minWidth = element.preferredWidth = size;
            element.minHeight = element.preferredHeight = size;
        }

        private UnityEngine.UI.Button AngleButton(Transform parent, string name, string label, int index, PreviewAngle angle)
        {
            return factory.Button(parent, name, label, () =>
            {
                previewOrbit.SetAngle(angle);
                MarkPreviewAngle(index);
            }, AlfaButtonStyle.Tab, 60f);
        }

        private void MarkPreviewAngle(int index)
        {
            selectedPreviewAngle = index;
            for (var i = 0; i < previewAngleButtons.Length; i++) AlfaUiFactory.SetSelected(previewAngleButtons[i], i == index);
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

            var content = factory.Vertical(optionsPanel, "Content", 10f);
            AlfaUiFactory.Fill(content, 24f, 24f, 20f, 196f);
            customizationCategoryTitle = factory.Caption(content, "CategoryTitle", "OPCIONES DEL HUMANO");
            customizationOptionsTitle = factory.Title(content, "OptionsTitle", "TONO DE PIEL", 30f);
            customizationOptionsTitle.textWrappingMode = TextWrappingModes.NoWrap;
            customizationOptionsTitle.enableAutoSizing = true;
            customizationOptionsTitle.fontSizeMin = AlfaUiTheme.MinTextSize;
            customizationOptionsTitle.fontSizeMax = 30f;
            factory.Divider(content, "CategoryDivider", AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.52f));

            humanCustomizationFields = factory.Vertical(content, "HumanFields", 10f).gameObject;
            skinPaletteGroup = factory.Vertical(humanCustomizationFields.transform, "SkinGroup", 10f).gameObject;
            humanPaletteRoot = CreatePaletteLayout(skinPaletteGroup.transform, "SkinPalette");
            pajamaPaletteGroup = factory.Vertical(humanCustomizationFields.transform, "PajamaGroup", 10f).gameObject;
            pajamaPaletteRoot = CreatePaletteLayout(pajamaPaletteGroup.transform, "PajamaPalette");
            factory.Text(pajamaPaletteGroup.transform, "DefaultClothes", "El color tiñe el pijama y el gorro. Pantuflas de fábrica.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            mosquitoCustomizationFields = factory.Vertical(content, "MosquitoFields", 10f).gameObject;
            mosquitoPaletteRoot = CreatePaletteLayout(mosquitoCustomizationFields.transform, "MosquitoPalette");

            modularCustomizationFields = factory.Vertical(content, "ModularFields", 8f).gameObject;
            var modularLayout = modularCustomizationFields.AddComponent<UnityEngine.UI.LayoutElement>();
            modularLayout.flexibleHeight = 1f;
            var optionsScroll = factory.GridScrollView(modularCustomizationFields.transform, "OptionsScroll", out modularOptionRoot, 300f,
                CardCell, GridSpacing, 3, true);
            optionsScroll.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 1f;
            modularOptionGrid = modularOptionRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
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

            var footer = AlfaUiFactory.Node("Actions", optionsPanel).GetComponent<RectTransform>();
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-48f, 158f));
            var secondary = factory.Horizontal(footer, "SecondaryActions", 12f, TextAnchor.MiddleCenter);
            secondary.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            Anchor(secondary, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 64f));
            customizationRandomButton = factory.Button(secondary, "CustomizationRandomButton", "ALEATORIO", RandomizeCustomization,
                AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Dice);
            customizationResetButton = factory.Button(secondary, "CustomizationResetButton", "DESHACER", ResetCustomization,
                AlfaButtonStyle.Secondary, 64f, AlfaUiIconKind.Undo);
            customizationSaveButton = factory.Button(footer, "CustomizationSaveButton", "LISTO", CustomizationPrimaryAction,
                AlfaButtonStyle.Success, 82f, AlfaUiIconKind.Ready);
            factory.StrongLabel(customizationSaveButton, AlfaUiTheme.CtaSize);
            Anchor((RectTransform)customizationSaveButton.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 82f));
            customizationSaveLabel = customizationSaveButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
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
            if (category == SkinCategory || category == PajamaCategory) basicHumanCategory = category;
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
        /// Rebuilds the category list only when the visible catalogue or role changed, and the option grid only
        /// when the category changed too; otherwise it just moves the selection marks. Scroll positions reset only
        /// with a structural change; keyboard focus is restored by name after a rebuild.
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
            foreach (var slot in slots)
            {
                var button = modularCategoryRoot.Find("ModularCategory_" + slot.WireSlotId)?.GetComponent<UnityEngine.UI.Button>();
                if (button != null) AlfaUiFactory.SetSelected(button, string.Equals(slot.SlotId, modularSelectedSlotId, StringComparison.Ordinal));
            }

            var current = CurrentModularSlot();
            var optionsKey = categoriesKey + "#" + (current == null ? string.Empty : current.SlotId + ":" +
                string.Join("|", current.Options.Select(option => option.OptionId + ":" + option.Label + ":" + option.HasSwatch + ":" + option.SwatchRgba)));
            var rebuiltOptions = optionsKey != builtModularOptionsKey;
            if (rebuiltOptions)
            {
                AlfaUiFactory.Clear(modularOptionRoot);
                if (current != null)
                {
                    var labelled = current.Options.Where(item => !string.IsNullOrWhiteSpace(item.Label)).ToList();
                    var swatches = labelled.Count > 0 && labelled.All(item => item.HasSwatch);
                    modularOptionGrid.cellSize = swatches ? SwatchCell : CardCell;
                    modularOptionGrid.constraintCount = swatches ? 5 : 3;
                    foreach (var option in labelled) BuildModularOption(current, option, swatches);
                }
                builtModularOptionsKey = optionsKey;
            }
            MarkModularOptions(current);
            if (resetScroll || rebuiltCategories) ResetScrollPosition(modularCategoryRoot);
            if (resetScroll || rebuiltOptions) ResetScrollPosition(modularOptionRoot);
            if ((rebuiltCategories || rebuiltOptions) && focused != null && screen == AlfaUiScreen.Customization) Focus(focused);
        }

        private string FocusedNameUnder(params Transform[] roots)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return null;
            foreach (var root in roots)
                if (root != null && selected.transform.IsChildOf(root)) return selected.name;
            return null;
        }

        private void BuildModularOption(CustomizationSlotSnapshot slot, CustomizationOptionSnapshot option, bool swatchGrid)
        {
            var slotId = slot.SlotId;
            var optionId = option.OptionId;
            var button = factory.Button(modularOptionRoot, "ModularOption_" + slot.WireSlotId + "_" + option.WireOptionId,
                swatchGrid ? string.Empty : option.Label, () => SetModularCustomizationOption(slotId, optionId), AlfaButtonStyle.Secondary, 80f);
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
            label.lineSpacing = -12f;
            AlfaUiFactory.Fill(label.rectTransform, 8f, 8f, 94f, 6f);
            Sprite thumbnail = option.HasSwatch ? null : customizationState.ThumbnailResolver?.Invoke(slot.SlotId, option.OptionId);
            if (option.HasSwatch)
            {
                var swatch = AddOptionSwatch(button, "ColorSwatch", ColorFromRgba(option.SwatchRgba), 0f);
                Anchor(swatch, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(74f, 74f));
            }
            else if (thumbnail != null)
            {
                var image = AlfaUiFactory.Node("Thumbnail", button.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                image.sprite = thumbnail;
                image.preserveAspect = true;
                image.raycastTarget = false;
                Anchor(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(112f, 78f));
            }
            else
            {
                // No thumbnail yet: the category pictogram keeps the card readable (the name is below).
                var icon = factory.Icon(button.transform, "Placeholder", CategoryIcon(slot), AlfaUiTheme.WithAlpha(AlfaUiTheme.Moon200, 0.55f));
                Anchor(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(66f, 66f));
            }
            var mark = factory.Icon(button.transform, "SelectionMark", AlfaUiIconKind.Ready, AlfaUiTheme.Sheet100);
            Anchor(mark.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(26f, 26f));
            button.GetComponent<AlfaUiFocusMotion>()?.CaptureContent();
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

        private void MarkModularOptions(CustomizationSlotSnapshot current)
        {
            if (current == null || modularCustomizationDraft == null) return;
            string selectedOption = modularCustomizationDraft.For(ToCustomizationRole(modularEditedRole)).OptionFor(current.SlotId);
            foreach (var option in current.Options)
            {
                var node = modularOptionRoot.Find("ModularOption_" + current.WireSlotId + "_" + option.WireOptionId);
                if (node == null) continue;
                var selected = string.Equals(option.OptionId, selectedOption, StringComparison.Ordinal);
                MarkOption(node.GetComponent<UnityEngine.UI.Button>(), selected);
            }
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

        private void SelectModularCategory(string slotId)
        {
            if (customizationState?.Mode != CustomizationUiMode.Modular || customizationSaveLatched || customizationState.IsReadOnly) return;
            if (!VisibleModularSlots().Any(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))) return;
            modularSelectedSlotId = slotId;
            RefreshModularLists();
            UpdateCustomizationView();
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

        private static RectTransform CreatePaletteLayout(Transform parent, string name)
        {
            var node = AlfaUiFactory.Node(name, parent, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            var grid = node.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            AlfaUiFactory.ConfigureGrid(grid, SwatchCell, GridSpacing, 5);
            return node.GetComponent<RectTransform>();
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
                AddOptionSwatch(button, "Swatch", option.Color, 9f);
            }
            var rows = Mathf.CeilToInt(options.Count / 5f);
            var layout = parent.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = rows * SwatchCell.y + Mathf.Max(0, rows - 1) * GridSpacing.y + 4f;
            builtPaletteSignatures[parent] = signature;
            if (focused != null && screen == AlfaUiScreen.Customization) Focus(focused);
        }

        private static void MarkPalette(Transform parent, string selectedId)
        {
            foreach (Transform child in parent)
                MarkOption(child.GetComponent<UnityEngine.UI.Button>(), child.name == "Color_" + selectedId);
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
            basicCategoryButtons[SkinCategory].gameObject.SetActive(human);
            basicCategoryButtons[PajamaCategory].gameObject.SetActive(human);
            basicCategoryButtons[MosquitoCategory].gameObject.SetActive(!human);
            var basicCategory = human ? basicHumanCategory : MosquitoCategory;
            foreach (var pair in basicCategoryButtons) AlfaUiFactory.SetSelected(pair.Value, pair.Key == basicCategory);

            customizationHumanButton.interactable = editable && !customizationSaveLatched;
            customizationMosquitoButton.interactable = editable && !customizationSaveLatched;
            SetRoleTabSelection(customizationHumanButton, human, AlfaRole.Human);
            SetRoleTabSelection(customizationMosquitoButton, !human, AlfaRole.Mosquito);

            string optionsTitle, selectionName;
            if (!editable) { optionsTitle = "SÓLO LECTURA"; selectionName = string.Empty; }
            else if (modular)
            {
                var slot = CurrentModularSlot();
                optionsTitle = slot?.Label ?? "SIN OPCIONES";
                var selectedId = slot == null ? null : modularCustomizationDraft.For(ToCustomizationRole(role)).OptionFor(slot.SlotId);
                selectionName = slot != null && slot.TryOption(selectedId ?? string.Empty, out var chosen) ? chosen.Label : string.Empty;
            }
            else
            {
                var palette = !human ? customizationState.MosquitoColors : basicHumanCategory == SkinCategory ? customizationState.SkinColors : customizationState.PajamaColors;
                var selectedId = !human ? customizationDraft.MosquitoColorId : basicHumanCategory == SkinCategory ? customizationDraft.SkinColorId : customizationDraft.PajamaColorId;
                optionsTitle = !human ? "COLOR DEL CUERPO" : basicHumanCategory == SkinCategory ? "TONO DE PIEL" : "COLOR DEL PIJAMA";
                selectionName = palette.FirstOrDefault(option => option.Id == selectedId)?.Label ?? string.Empty;
            }
            customizationCategoryTitle.text = !editable ? "PERSONALIZACIÓN NO DISPONIBLE" : modular && !VisibleModularSlots().Any()
                ? "SIN OPCIONES DISPONIBLES" : "OPCIONES DEL " + (human ? "HUMANO" : "MOSQUITO");
            customizationOptionsTitle.text = optionsTitle;
            customizationSelectionLabel.text = selectionName;
            customizationSelectionLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(selectionName));

            previewOrbit?.Show(role);
            RefreshPreviewAvailability();
            if (customizationSaveLatched) customizationStatus.text = "Aplicando apariencia…";
            else if (customizationState.IsReadOnly)
                customizationStatus.text = string.IsNullOrWhiteSpace(customizationState.Message)
                    ? "Esta personalización todavía no está disponible en esta versión."
                    : customizationState.Message;
            else if (modular && !customizationState.ModularPreviewAvailable)
                customizationStatus.text = string.IsNullOrWhiteSpace(customizationState.Message)
                    ? "La vista de estas piezas llegará cuando estén listas en el juego."
                    : customizationState.Message;
            else customizationStatus.text = customizationState.Message;

            bool dirty = CustomizationDirty();
            customizationSaveButton.interactable = editable && !customizationSaveLatched;
            customizationSaveLabel.text = customizationSaveLatched ? "APLICANDO…" : dirty ? "APLICAR" : "LISTO";
            customizationResetButton.interactable = editable && !customizationSaveLatched;
            customizationRandomButton.interactable = editable && !customizationSaveLatched && (!modular || VisibleModularSlots().Any());
            if (!modular)
            {
                MarkPalette(humanPaletteRoot, customizationDraft.SkinColorId);
                MarkPalette(pajamaPaletteRoot, customizationDraft.PajamaColorId);
                MarkPalette(mosquitoPaletteRoot, customizationDraft.MosquitoColorId);
            }
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

        /// <summary>The green call to action applies a changed look (APLICAR) or, when nothing changed, closes (LISTO).</summary>
        private void CustomizationPrimaryAction()
        {
            if (customizationState == null || customizationSaveLatched) return;
            if (!customizationState.IsReadOnly && CustomizationDirty()) SaveCustomization();
            else CloseCustomization();
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
                new NamedColorOption("warm-light", "Claro", new Color(0.88f, 0.67f, 0.50f)),
                new NamedColorOption("warm-medium", "Medio", new Color(0.67f, 0.43f, 0.29f)),
                new NamedColorOption("warm-deep", "Oscuro", new Color(0.35f, 0.20f, 0.16f))
            };
            var pajamas = new[]
            {
                new NamedColorOption("blue", "Azul", AlfaUiTheme.Sky400),
                new NamedColorOption("coral", "Coral", AlfaUiTheme.Pajama500),
                new NamedColorOption("green", "Verde", AlfaUiTheme.Mint400)
            };
            var mosquitoes = new[]
            {
                new NamedColorOption("red", "Rojo", AlfaUiTheme.Pajama500),
                new NamedColorOption("blue", "Azul", AlfaUiTheme.Sky400),
                new NamedColorOption("green", "Verde", new Color(0.32f, 0.56f, 0.35f))
            };
            var saved = new BasicCustomizationDraft(AlfaRole.Human, "warm-medium", "blue", "red");
            return new CustomizationUiState(skin, pajamas, mosquitoes, saved);
        }
    }
}
