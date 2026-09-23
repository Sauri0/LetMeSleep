using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    internal sealed class AlfaUiFactory
    {
        private readonly AlfaUiDependencies dependencies;
        private readonly Action<UiFeedbackKind> feedback;
        private static Sprite horizontalFadeSprite;
        private static Sprite verticalFadeSprite;
        private static readonly Dictionary<TMP_FontAsset, Material> ShadowedMaterials = new Dictionary<TMP_FontAsset, Material>();

        internal AlfaUiFactory(AlfaUiDependencies dependencies, Action<UiFeedbackKind> feedback = null)
        {
            this.dependencies = dependencies ?? new AlfaUiDependencies();
            this.feedback = feedback;
        }

        internal bool HasComicFont => AlfaUiTheme.HasComicFont(dependencies);

        internal GameObject View(string name, Transform parent, bool opaque = true)
        {
            var view = Node(name, parent, typeof(UnityEngine.UI.Image));
            var group = view.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
            Fill(view.GetComponent<RectTransform>());
            var image = view.GetComponent<UnityEngine.UI.Image>();
            image.color = opaque ? AlfaUiTheme.Night800 : Color.clear;
            image.raycastTarget = opaque;
            view.AddComponent<AlfaUiEntranceMotion>();
            return view;
        }

        internal RectTransform SafeArea(Transform parent, float left = 56f, float right = 56f, float top = 42f, float bottom = 42f)
        {
            var node = Node("SafeArea", parent);
            var rect = node.GetComponent<RectTransform>();
            Stretch(rect, left, right, top, bottom);
            return rect;
        }

        internal RectTransform Vertical(Transform parent, string name, float spacing = 16f, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.VerticalLayoutGroup));
            var group = node.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return node.GetComponent<RectTransform>();
        }

        internal RectTransform Horizontal(Transform parent, string name, float spacing = 16f, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.HorizontalLayoutGroup));
            var group = node.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return node.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Rounded navy surface with a vertical shade, a 2-unit frame and a soft drop shadow (UI-06 panel).
        /// </summary>
        internal RectTransform Panel(Transform parent, string name, Color? color = null, float preferredWidth = -1f, float preferredHeight = -1f,
            float radius = AlfaUiTheme.PanelRadius)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = color ?? AlfaUiTheme.WithAlpha(AlfaUiTheme.Night700, 0.95f);
            image.raycastTarget = false;
            image.sprite = dependencies.PanelSprite != null ? dependencies.PanelSprite : AlfaUiSkin.Fill(radius);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var surface = node.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = AlfaUiSkin.RadiusKey(radius);
            surface.GradientTop = Color.white;
            surface.GradientBottom = new Color(0.8f, 0.84f, 0.9f, 1f);
            surface.FrameColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.95f);
            surface.ShadowColor = AlfaUiTheme.WithAlpha(Color.black, 0.38f);
            surface.ShadowOffset = new Vector2(0f, -AlfaUiTheme.ShadowOffset);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            if (preferredWidth > 0f) layout.preferredWidth = preferredWidth;
            if (preferredHeight > 0f) layout.preferredHeight = preferredHeight;
            return node.GetComponent<RectTransform>();
        }

        /// <summary>Inset well for fields, list rows and slots (panel.inset).</summary>
        internal RectTransform Inset(Transform parent, string name, float preferredHeight = -1f, float radius = AlfaUiTheme.SmallRadius)
        {
            var inset = Panel(parent, name, AlfaUiTheme.WithAlpha(AlfaUiTheme.PanelInset, 0.9f), -1f, preferredHeight, radius);
            var surface = inset.GetComponent<AlfaUiSurface>();
            surface.GradientBottom = Color.white;
            surface.FrameColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.85f);
            surface.ShadowColor = Color.clear;
            return inset;
        }

        internal static void SetSurface(Component target, Color? top = null, Color? bottom = null, Color? frame = null, Color? shadow = null)
        {
            var surface = target != null ? target.GetComponent<AlfaUiSurface>() : null;
            if (surface == null) return;
            if (top.HasValue) surface.GradientTop = top.Value;
            if (bottom.HasValue) surface.GradientBottom = bottom.Value;
            if (frame.HasValue) surface.FrameColor = frame.Value;
            if (shadow.HasValue) surface.ShadowColor = shadow.Value;
            surface.Refresh();
        }

        /// <summary>Frame (border) colour of a skinned surface; replaces the alpha's Outline component.</summary>
        internal static void SetFrame(Component target, Color color)
        {
            var surface = target != null ? target.GetComponent<AlfaUiSurface>() : null;
            if (surface == null) return;
            surface.FrameColor = color;
            surface.Refresh();
        }

        internal TextMeshProUGUI Text(Transform parent, string name, string value, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool heading = false)
        {
            var node = Node(name, parent, typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            var text = node.GetComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.font = heading ? AlfaUiTheme.Display(dependencies) : AlfaUiTheme.Body(dependencies);
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (heading)
            {
                text.fontStyle = FontStyles.Bold;
                text.characterSpacing = 1.5f;
            }
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = Mathf.Max(size * 1.25f, 24f);
            layout.flexibleWidth = 1f;
            return text;
        }

        /// <summary>Small uppercase field caption ("NOMBRE DE LA SALA").</summary>
        internal TextMeshProUGUI Caption(Transform parent, string name, string value, float size = 17f)
        {
            var text = Text(parent, name, value, size, AlfaUiTheme.LabelInk, TextAlignmentOptions.Left, true);
            text.characterSpacing = 1.2f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        internal TextMeshProUGUI LogoText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = Text(parent, name, value, size, color, alignment, true);
            text.font = AlfaUiTheme.Logo(dependencies);
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            return text;
        }

        /// <summary>
        /// Comic screen title (Bangers when available, bold Atkinson otherwise) with a soft ink drop shadow.
        /// </summary>
        internal TextMeshProUGUI Title(Transform parent, string name, string value, float size, Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = Text(parent, name, value, size, color ?? AlfaUiTheme.Sheet100, alignment, true);
            MakeComic(text, size);
            return text;
        }

        /// <summary>Switches a label to the comic display face with a shared shadowed material.</summary>
        internal void MakeComic(TextMeshProUGUI text, float size = -1f)
        {
            if (text == null) return;
            if (!HasComicFont)
            {
                text.fontStyle = FontStyles.Bold;
                return;
            }
            text.font = AlfaUiTheme.Logo(dependencies);
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 2.2f;
            if (size > 0f) text.fontSize = size;
            var material = ShadowedMaterial(text.font);
            if (material != null) text.fontSharedMaterial = material;
        }

        private static Material ShadowedMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (ShadowedMaterials.TryGetValue(font, out var cached) && cached != null) return cached;
            var material = new Material(font.material) { name = font.name + " UI shadow", hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.72f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.15f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            }
            ShadowedMaterials[font] = material;
            return material;
        }

        /// <summary>
        /// UI-06 wordmark: "LET ME" yellow and "SLEEP" blue in the comic face, thick ink outline and an
        /// offset ink extrusion, followed by the yellow subtitle line.
        /// </summary>
        internal RectTransform BrandLockup(Transform parent, string name, float height = 400f, string subtitle = "HUMANOS CONTRA MOSQUITOS")
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.LayoutElement));
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            // Rects are taller than the glyphs on purpose: the lines overlap like the sketch without TMP reporting overflow.
            var first = LogoText(root.transform, "LogoFirstLine", "LET ME", 150f, Color.white);
            Anchor(first.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, 0f), new Vector2(-44f, 210f));
            var second = LogoText(root.transform, "LogoSecondLine", "SLEEP", 236f, Color.white);
            Anchor(second.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -112f), new Vector2(0f, 320f));
            foreach (var line in new[] { first, second })
            {
                line.textWrappingMode = TextWrappingModes.NoWrap;
                line.overflowMode = TextOverflowModes.Overflow;
                line.enableVertexGradient = true;
                line.characterSpacing = 1f;
                line.alignment = TextAlignmentOptions.TopLeft;
            }
            first.colorGradient = new VertexGradient(AlfaUiTheme.LogoYellowTop, AlfaUiTheme.LogoYellowTop,
                AlfaUiTheme.LogoYellowBottom, AlfaUiTheme.LogoYellowBottom);
            second.colorGradient = new VertexGradient(AlfaUiTheme.LogoBlueTop, AlfaUiTheme.LogoBlueTop,
                AlfaUiTheme.LogoBlueBottom, AlfaUiTheme.LogoBlueBottom);
            foreach (var line in new[] { first, second })
            {
                // Per-text material instances: two wordmark lines only; they carry the thick outline and extrusion.
                var material = line.fontMaterial;
                line.outlineColor = AlfaUiTheme.Ink900;
                line.outlineWidth = 0.42f;
                if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
                {
                    material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    material.SetColor(ShaderUtilities.ID_UnderlayColor, AlfaUiTheme.WithAlpha(Color.black, 0.8f));
                    material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.3f);
                    material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
                    material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.75f);
                    material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
                }
            }

            var mosquito = Icon(root.transform, "MosquitoMark", AlfaUiIconKind.Mosquito, AlfaUiTheme.TeamMosquito);
            var rect = mosquito.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(470f, -74f);
            rect.sizeDelta = new Vector2(110f, 110f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -16f);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var caption = Title(root.transform, "Subtitle", subtitle, 36f, AlfaUiTheme.Lamp400);
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.characterSpacing = 3f;
                Anchor(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(-40f, 52f));
            }
            return root.GetComponent<RectTransform>();
        }

        internal static void QuietButton(UnityEngine.UI.Button button, float labelSize = 22f)
        {
            ApplyStyle(button, AlfaButtonStyle.Quiet);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.fontSize = labelSize;
        }

        internal UnityEngine.UI.Button Button(Transform parent, string name, string label, UnityAction callback,
            bool primary = false, bool destructive = false, float height = 58f, AlfaUiIconKind icon = AlfaUiIconKind.None,
            bool emitConfirm = true)
        {
            var style = destructive ? AlfaButtonStyle.Danger : primary ? AlfaButtonStyle.Primary : AlfaButtonStyle.Secondary;
            return Button(parent, name, label, callback, style, height, icon, emitConfirm);
        }

        internal UnityEngine.UI.Button Button(Transform parent, string name, string label, UnityAction callback,
            AlfaButtonStyle style, float height = 58f, AlfaUiIconKind icon = AlfaUiIconKind.None, bool emitConfirm = true)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : AlfaUiSkin.Fill(AlfaUiTheme.ButtonRadius);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            node.GetComponent<AlfaUiSurface>().RadiusKey = AlfaUiSkin.RadiusKey(AlfaUiTheme.ButtonRadius);
            var button = node.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic, wrapAround = false };
            if (callback != null)
            {
                button.onClick.AddListener(() =>
                {
                    if (emitConfirm) feedback?.Invoke(UiFeedbackKind.Confirm);
                    callback();
                });
            }
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = Mathf.Max(44f, height);
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;
            var text = Text(node.transform, "Label", label, AlfaUiTheme.ButtonSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 0.8f;
            var iconPlateSize = Mathf.Clamp(height - 20f, 30f, 46f);
            Fill(text.rectTransform, icon == AlfaUiIconKind.None ? 16f : iconPlateSize + 30f, 18f, 6f, 6f);
            RectTransform iconPlate = null;
            if (icon != AlfaUiIconKind.None)
            {
                var plate = Node("IconPlate", node.transform, typeof(UnityEngine.UI.Image));
                var plateImage = plate.GetComponent<UnityEngine.UI.Image>();
                plateImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
                plateImage.type = UnityEngine.UI.Image.Type.Sliced;
                plateImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.18f);
                plateImage.raycastTarget = false;
                var plateRect = plate.GetComponent<RectTransform>();
                iconPlate = plateRect;
                plateRect.anchorMin = new Vector2(0f, 0.5f);
                plateRect.anchorMax = new Vector2(0f, 0.5f);
                plateRect.pivot = new Vector2(0f, 0.5f);
                plateRect.anchoredPosition = new Vector2(12f, 0f);
                plateRect.sizeDelta = new Vector2(iconPlateSize, iconPlateSize);
                var iconGraphic = Icon(plate.transform, "Icon", icon, AlfaUiTheme.Sheet100);
                Fill(iconGraphic.rectTransform, 5f, 5f, 5f, 5f);
            }
            var shine = Node("Shine", node.transform, typeof(UnityEngine.UI.Image));
            var shineImage = shine.GetComponent<UnityEngine.UI.Image>();
            shineImage.color = new Color(1f, 1f, 1f, 0.2f);
            shineImage.raycastTarget = false;
            var shineRect = shine.GetComponent<RectTransform>();
            shineRect.anchorMin = new Vector2(0f, 1f);
            shineRect.anchorMax = Vector2.one;
            shineRect.pivot = new Vector2(0.5f, 1f);
            shineRect.offsetMin = new Vector2(12f, -3.5f);
            shineRect.offsetMax = new Vector2(-12f, -2f);
            var lowerBevel = Node("LowerBevel", node.transform, typeof(UnityEngine.UI.Image));
            var lowerBevelImage = lowerBevel.GetComponent<UnityEngine.UI.Image>();
            lowerBevelImage.color = new Color(0f, 0f, 0f, 0.16f);
            lowerBevelImage.raycastTarget = false;
            var lowerBevelRect = lowerBevel.GetComponent<RectTransform>();
            lowerBevelRect.anchorMin = Vector2.zero;
            lowerBevelRect.anchorMax = new Vector2(1f, 0f);
            lowerBevelRect.pivot = new Vector2(0.5f, 0f);
            lowerBevelRect.offsetMin = new Vector2(10f, 2f);
            lowerBevelRect.offsetMax = new Vector2(-10f, 4f);
            var motion = node.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(shineImage, iconPlate, node.GetComponent<AlfaUiSurface>());
            ApplyStyle(button, style);
            return button;
        }

        /// <summary>Recolours a factory button (surface, tint block, label and icon) for an intent.</summary>
        internal static void ApplyStyle(UnityEngine.UI.Button button, AlfaButtonStyle style)
        {
            if (button == null) return;
            AlfaUiTheme.StyleColors(style == AlfaButtonStyle.Menu ? AlfaButtonStyle.Secondary : style, out var top, out var bottom, out var frame, out var content);
            var surface = button.GetComponent<AlfaUiSurface>();
            if (surface != null)
            {
                surface.GradientTop = top;
                surface.GradientBottom = bottom;
                surface.FrameColor = frame;
                surface.ShadowColor = style == AlfaButtonStyle.Quiet || style == AlfaButtonStyle.Tab
                    ? AlfaUiTheme.WithAlpha(Color.black, 0.22f) : AlfaUiTheme.WithAlpha(Color.black, 0.42f);
                surface.FocusRecolorsFill = style == AlfaButtonStyle.Menu || style == AlfaButtonStyle.Tab || style == AlfaButtonStyle.Secondary;
                switch (style)
                {
                    case AlfaButtonStyle.Success:
                        surface.FocusFrameColor = Color.Lerp(AlfaUiTheme.SuccessBorder, Color.white, 0.6f);
                        surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.SuccessHi, 0.55f);
                        break;
                    case AlfaButtonStyle.Danger:
                        surface.FocusFrameColor = Color.Lerp(AlfaUiTheme.DangerBorder, Color.white, 0.6f);
                        surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.DangerHi, 0.55f);
                        break;
                    case AlfaButtonStyle.Primary:
                        surface.FocusFrameColor = Color.Lerp(AlfaUiTheme.PrimaryBorder, Color.white, 0.6f);
                        surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.6f);
                        break;
                    case AlfaButtonStyle.Menu:
                        surface.FocusGradientTop = AlfaUiTheme.PrimaryHi;
                        surface.FocusGradientBottom = AlfaUiTheme.Primary;
                        surface.FocusFrameColor = AlfaUiTheme.PrimaryBorder;
                        surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.55f);
                        break;
                    default:
                        surface.FocusGradientTop = AlfaUiTheme.SecondaryHover;
                        surface.FocusGradientBottom = Color.Lerp(AlfaUiTheme.SecondaryHover, AlfaUiTheme.Night600, 0.5f);
                        surface.FocusFrameColor = AlfaUiTheme.Sky400;
                        surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.4f);
                        break;
                }
                surface.Refresh();
            }
            button.colors = AlfaUiTheme.TintColors(style);
            var labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
                label.color = label.name == "Subtitle" ? AlfaUiTheme.WithAlpha(content, 0.78f) : content;
            foreach (var symbol in button.GetComponentsInChildren<AlfaUiIcon>(true)) symbol.color = content;
            var motion = button.GetComponent<AlfaUiFocusMotion>();
            if (motion != null) motion.SelectOnHover = style == AlfaButtonStyle.Menu;
        }

        /// <summary>Puts a factory button label in the comic face (large CTAs and the main menu rail).</summary>
        internal void ComicLabel(UnityEngine.UI.Button button, float size)
        {
            var label = button != null ? button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>() : null;
            if (label == null) return;
            MakeComic(label, size);
            if (!HasComicFont) label.fontSize = Mathf.Min(size, AlfaUiTheme.ButtonSize + 2f);
        }

        internal UnityEngine.UI.Button FeatureButton(Transform parent, string name, string title, string subtitle,
            UnityAction callback, AlfaUiIconKind icon, bool primary = false, bool destructive = false, float height = 76f)
        {
            var button = Button(parent, name, title, callback, primary, destructive, height, icon);
            var titleText = button.GetComponentInChildren<TextMeshProUGUI>();
            titleText.fontSize = 22f;
            titleText.alignment = TextAlignmentOptions.Left;
            var textLeft = Mathf.Clamp(height - 20f, 30f, 46f) + 30f;
            Fill(titleText.rectTransform, textLeft, 16f, 7f, 32f);
            var subtitleText = Text(button.transform, "Subtitle", subtitle, 16f, AlfaUiTheme.Moon200, TextAlignmentOptions.Left, true);
            subtitleText.characterSpacing = 0.6f;
            Fill(subtitleText.rectTransform, textLeft, 16f, 40f, 5f);
            var rail = Node("FocusRail", button.transform, typeof(UnityEngine.UI.Image));
            var railImage = rail.GetComponent<UnityEngine.UI.Image>();
            railImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, primary ? 0.9f : 0.0f);
            railImage.raycastTarget = false;
            var railRect = rail.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(0f, 0.2f);
            railRect.anchorMax = new Vector2(0f, 0.8f);
            railRect.pivot = new Vector2(0f, 0.5f);
            railRect.anchoredPosition = new Vector2(3f, 0f);
            railRect.sizeDelta = new Vector2(4f, 0f);
            button.GetComponent<AlfaUiFocusMotion>().BindAccent(railImage);
            ApplyStyle(button, destructive ? AlfaButtonStyle.Danger : primary ? AlfaButtonStyle.Primary : AlfaButtonStyle.Secondary);
            return button;
        }

        internal static Sprite HorizontalFadeSprite()
        {
            if (horizontalFadeSprite != null) return horizontalFadeSprite;
            horizontalFadeSprite = FadeSprite("LMS UI horizontal fade", true);
            return horizontalFadeSprite;
        }

        internal static Sprite VerticalFadeSprite()
        {
            if (verticalFadeSprite != null) return verticalFadeSprite;
            verticalFadeSprite = FadeSprite("LMS UI vertical fade", false);
            return verticalFadeSprite;
        }

        private static Sprite FadeSprite(string name, bool horizontal)
        {
            const int length = 256;
            const int thickness = 4;
            var width = horizontal ? length : thickness;
            var height = horizontal ? thickness : length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var i = 0; i < length; i++)
            {
                // Horizontal: opaque at the left edge. Vertical: opaque at the bottom edge.
                var t = Mathf.InverseLerp(0.30f, 1f, i / (length - 1f));
                var alpha = (byte)Mathf.RoundToInt(255f * (1f - t * t * (3f - 2f * t)));
                for (var j = 0; j < thickness; j++)
                {
                    var index = horizontal ? j * width + i : i * width + j;
                    pixels[index] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        internal AlfaUiIcon Icon(Transform parent, string name, AlfaUiIconKind kind, Color color)
        {
            var node = Node(name, parent, typeof(AlfaUiIcon));
            var icon = node.GetComponent<AlfaUiIcon>();
            icon.Kind = kind;
            icon.color = color;
            icon.raycastTarget = false;
            return icon;
        }

        internal RectTransform SectionHeader(Transform parent, string name, string label, AlfaUiIconKind icon, Color color)
        {
            var row = Horizontal(parent, name, 12f, TextAnchor.MiddleLeft);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48f;
            var badge = Panel(row, "Badge", AlfaUiTheme.WithAlpha(color, 0.2f), 44f, 44f, AlfaUiTheme.SmallRadius);
            SetSurface(badge, bottom: Color.white, frame: AlfaUiTheme.WithAlpha(color, 0.55f), shadow: Color.clear);
            badge.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0f;
            var symbol = Icon(badge, "Symbol", icon, color);
            Fill(symbol.rectTransform, 8f, 8f, 8f, 8f);
            Title(row, "Label", label, 36f, AlfaUiTheme.Sheet100);
            return row;
        }

        internal RectTransform Divider(Transform parent, string name, Color color, float height = 2f)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            node.GetComponent<UnityEngine.UI.Image>().color = color;
            node.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;
            return node.GetComponent<RectTransform>();
        }

        internal TMP_InputField Input(Transform parent, string name, string placeholder, int maxLength, bool code = false)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(TMP_InputField), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = AlfaUiTheme.PanelInset;
            image.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var surface = node.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = AlfaUiSkin.RadiusKey(AlfaUiTheme.SmallRadius);
            surface.FrameColor = AlfaUiTheme.InsetBorder;
            surface.FocusFrameColor = AlfaUiTheme.Sky400;
            surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.35f);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 56f;
            layout.flexibleWidth = 1f;

            var viewport = Node("Text Area", node.transform, typeof(UnityEngine.UI.RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 18f, 18f, 8f, 8f);
            var placeholderText = Text(viewport.transform, "Placeholder", placeholder, AlfaUiTheme.BodySize,
                AlfaUiTheme.WithAlpha(AlfaUiTheme.Moon200, 0.62f), TextAlignmentOptions.Left);
            Fill(placeholderText.rectTransform);
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
            var valueText = Text(viewport.transform, "Text", string.Empty, code ? 26f : AlfaUiTheme.BodySize,
                code ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            Fill(valueText.rectTransform);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Masking;
            if (code)
            {
                valueText.fontStyle = FontStyles.Bold;
                valueText.characterSpacing = 4f;
                placeholderText.characterSpacing = 4f;
            }

            var input = node.GetComponent<TMP_InputField>();
            input.textViewport = viewportRect;
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = maxLength;
            input.richText = false;
            input.caretColor = AlfaUiTheme.Sky400;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.selectionColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, 0.45f);
            input.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            input.targetGraphic = image;
            input.colors = AlfaUiTheme.TintColors(AlfaButtonStyle.Secondary);
            var motion = node.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, null, surface);
            return input;
        }

        internal UnityEngine.UI.Slider Slider(Transform parent, string name, float min, float max, UnityAction<float> callback)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Slider), typeof(UnityEngine.UI.LayoutElement));
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 44f;
            layout.preferredHeight = 44f;
            layout.flexibleWidth = 1f;
            var background = Node("Background", root.transform, typeof(UnityEngine.UI.Image));
            Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 16f, 16f);
            var backgroundImage = background.GetComponent<UnityEngine.UI.Image>();
            backgroundImage.color = AlfaUiTheme.PanelInset;
            backgroundImage.sprite = AlfaUiSkin.Fill(6f);
            backgroundImage.type = UnityEngine.UI.Image.Type.Sliced;
            var backgroundSurface = background.AddComponent<AlfaUiSurface>();
            backgroundSurface.RadiusKey = 6;
            backgroundSurface.FrameColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.9f);
            var fillArea = Node("Fill Area", root.transform);
            Stretch(fillArea.GetComponent<RectTransform>(), 6f, 6f, 16f, 16f);
            var fill = Node("Fill", fillArea.transform, typeof(UnityEngine.UI.Image));
            Fill(fill.GetComponent<RectTransform>());
            var fillImage = fill.GetComponent<UnityEngine.UI.Image>();
            fillImage.color = Color.white;
            fillImage.sprite = AlfaUiSkin.Fill(6f);
            fillImage.type = UnityEngine.UI.Image.Type.Sliced;
            var fillSurface = fill.AddComponent<AlfaUiSurface>();
            fillSurface.RadiusKey = 6;
            fillSurface.GradientTop = AlfaUiTheme.Hex("5BB4FF");
            fillSurface.GradientBottom = AlfaUiTheme.Hex("2A86E8");
            var handleArea = Node("Handle Slide Area", root.transform);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRect.sizeDelta = new Vector2(-24f, 30f);
            var handle = Node("Handle", handleArea.transform, typeof(UnityEngine.UI.Image));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(24f, 24f);
            var handleImage = handle.GetComponent<UnityEngine.UI.Image>();
            handleImage.color = AlfaUiTheme.Sheet100;
            handleImage.sprite = AlfaUiSkin.Circle();
            handleImage.preserveAspect = true;
            var slider = root.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.colors = new UnityEngine.UI.ColorBlock
            {
                normalColor = AlfaUiTheme.Hex("DDE6F5"),
                highlightedColor = Color.white,
                pressedColor = AlfaUiTheme.Hex("B9C7DD"),
                selectedColor = Color.white,
                disabledColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.Disabled, 0.6f),
                colorMultiplier = 1f,
                fadeDuration = AlfaUiTheme.FocusDuration
            };
            slider.minValue = min;
            slider.maxValue = max;
            slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            if (callback != null) slider.onValueChanged.AddListener(callback);
            var motion = root.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, handleRect, backgroundSurface);
            backgroundSurface.FocusFrameColor = AlfaUiTheme.Sky400;
            return slider;
        }

        internal UnityEngine.UI.Toggle Toggle(Transform parent, string name, string label, UnityAction<bool> callback)
        {
            var row = Horizontal(parent, name, 14f);
            var box = Node("Box", row, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.LayoutElement));
            box.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 40f;
            box.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;
            var boxImage = box.GetComponent<UnityEngine.UI.Image>();
            boxImage.color = AlfaUiTheme.PanelInset;
            boxImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            boxImage.type = UnityEngine.UI.Image.Type.Sliced;
            var surface = box.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = 8;
            surface.FrameColor = AlfaUiTheme.InsetBorder;
            surface.FocusFrameColor = AlfaUiTheme.Sky400;
            surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.4f);
            var check = Node("Check", box.transform, typeof(UnityEngine.UI.Image));
            Fill(check.GetComponent<RectTransform>(), 7f, 7f, 7f, 7f);
            var checkImage = check.GetComponent<UnityEngine.UI.Image>();
            checkImage.color = AlfaUiTheme.StatusOk;
            checkImage.sprite = AlfaUiIcon.GetSprite(AlfaUiIconKind.Ready);
            checkImage.preserveAspect = true;
            checkImage.raycastTarget = false;
            var toggle = box.GetComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.colors = AlfaUiTheme.TintColors(AlfaButtonStyle.Secondary);
            toggle.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            if (callback != null) toggle.onValueChanged.AddListener(callback);
            var motion = box.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, null, surface);
            Text(row, "Label", label, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            return toggle;
        }

        internal TMP_Dropdown Dropdown(Transform parent, string name, IReadOnlyList<string> options, UnityAction<int> callback)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(TMP_Dropdown), typeof(UnityEngine.UI.LayoutElement));
            var background = root.GetComponent<UnityEngine.UI.Image>();
            background.color = Color.white;
            background.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            background.type = UnityEngine.UI.Image.Type.Sliced;
            var surface = root.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = 8;
            surface.GradientTop = AlfaUiTheme.Hex("123056");
            surface.GradientBottom = AlfaUiTheme.Hex("0E2748");
            surface.FrameColor = AlfaUiTheme.InsetBorder;
            surface.FocusFrameColor = AlfaUiTheme.Sky400;
            surface.FocusShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.35f);
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 56f;
            layout.flexibleWidth = 1f;

            var caption = Text(root.transform, "Label", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            Fill(caption.rectTransform, 18f, 54f, 8f, 8f);
            // U+25BC is missing from the shipped fonts; the chevron is an owned pictogram.
            var arrow = Icon(root.transform, "Arrow", AlfaUiIconKind.ChevronDown, AlfaUiTheme.Sky400);
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-16f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 24f);

            var template = Node("Template", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = Vector2.zero;
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 270f);
            var templateImage = template.GetComponent<UnityEngine.UI.Image>();
            templateImage.color = AlfaUiTheme.Night700;
            templateImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            templateImage.type = UnityEngine.UI.Image.Type.Sliced;
            var templateSurface = template.AddComponent<AlfaUiSurface>();
            templateSurface.RadiusKey = 8;
            templateSurface.FrameColor = AlfaUiTheme.Border;
            templateSurface.ShadowColor = AlfaUiTheme.WithAlpha(Color.black, 0.5f);

            var viewport = Node("Viewport", template.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            Fill(viewport.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            viewport.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Node("Content", viewport.transform, typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var contentLayout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 2f;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            var item = Node("Item", content.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.LayoutElement));
            item.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44f;
            var itemBackground = item.GetComponent<UnityEngine.UI.Image>();
            itemBackground.color = Color.white;
            itemBackground.sprite = AlfaUiSkin.Fill(6f);
            itemBackground.type = UnityEngine.UI.Image.Type.Sliced;
            // U+2713 is missing from the shipped fonts; the check is the Ready pictogram.
            var check = Node("Item Checkmark", item.transform, typeof(UnityEngine.UI.Image));
            var checkImage = check.GetComponent<UnityEngine.UI.Image>();
            checkImage.sprite = AlfaUiIcon.GetSprite(AlfaUiIconKind.Ready);
            checkImage.color = AlfaUiTheme.StatusOk;
            checkImage.preserveAspect = true;
            checkImage.raycastTarget = false;
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.anchoredPosition = new Vector2(12f, 0f);
            checkRect.sizeDelta = new Vector2(22f, 22f);
            var itemLabel = Text(item.transform, "Item Label", "OPCIÓN", AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Fill(itemLabel.rectTransform, 46f, 12f, 4f, 4f);
            var itemToggle = item.GetComponent<UnityEngine.UI.Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = checkImage;
            itemToggle.colors = new UnityEngine.UI.ColorBlock
            {
                normalColor = AlfaUiTheme.Night600,
                highlightedColor = AlfaUiTheme.SecondaryHover,
                pressedColor = AlfaUiTheme.Primary,
                selectedColor = AlfaUiTheme.SecondaryHover,
                disabledColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.Disabled, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = AlfaUiTheme.FocusDuration
            };

            var scroll = template.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            var dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            dropdown.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            dropdown.colors = AlfaUiTheme.TintColors(AlfaButtonStyle.Secondary);
            dropdown.options.Clear();
            if (options != null)
                for (var i = 0; i < options.Count; i++) dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
            if (callback != null) dropdown.onValueChanged.AddListener(callback);
            template.SetActive(false);
            dropdown.RefreshShownValue();
            var motion = root.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, null, surface);
            return dropdown;
        }

        internal RectTransform ScrollView(Transform parent, string name, out RectTransform content, float preferredHeight)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface), typeof(UnityEngine.UI.ScrollRect), typeof(UnityEngine.UI.LayoutElement));
            var rootImage = root.GetComponent<UnityEngine.UI.Image>();
            rootImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.PanelInset, 0.55f);
            rootImage.sprite = AlfaUiSkin.Fill(AlfaUiTheme.SmallRadius);
            rootImage.type = UnityEngine.UI.Image.Type.Sliced;
            var surface = root.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = 8;
            surface.FrameColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.InsetBorder, 0.55f);
            var rootLayout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            rootLayout.preferredHeight = preferredHeight;
            rootLayout.flexibleHeight = 1f;
            rootLayout.flexibleWidth = 1f;
            var viewport = Node("Viewport", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            Fill(viewport.GetComponent<RectTransform>(), 6f, 6f, 6f, 6f);
            viewport.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var contentNode = Node("Content", viewport.transform, typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            content = contentNode.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var group = contentNode.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.spacing = 8f;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            contentNode.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            return root.GetComponent<RectTransform>();
        }

        /// <summary>
        /// UI-06 list row: inset well, white title, secondary subtitle and a coloured status bar on the right.
        /// </summary>
        internal RectTransform ListRow(Transform parent, string name, AlfaUiIconKind icon, string title, string subtitle,
            Color status, float height = 64f)
        {
            var row = Inset(parent, name, height);
            row.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = height;
            if (icon != AlfaUiIconKind.None)
            {
                var symbol = Icon(row, "RowIcon", icon, AlfaUiTheme.Sky400);
                Anchor(symbol.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(30f, 30f));
            }
            var left = icon == AlfaUiIconKind.None ? 16f : 56f;
            var titleText = Text(row, "RowTitle", title, 20f, AlfaUiTheme.Sheet100, TextAlignmentOptions.BottomLeft, true);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.characterSpacing = 0.4f;
            Anchor(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(left, 0f), new Vector2(-left - 22f, -6f));
            var subtitleText = Text(row, "RowSubtitle", subtitle, 16f, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(left, 0f), new Vector2(-left - 22f, -6f));
            var bar = Node("StatusBar", row, typeof(UnityEngine.UI.Image));
            var barImage = bar.GetComponent<UnityEngine.UI.Image>();
            barImage.sprite = AlfaUiSkin.Fill(6f);
            barImage.type = UnityEngine.UI.Image.Type.Sliced;
            barImage.color = status;
            barImage.raycastTarget = false;
            Anchor(bar.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(6f, -18f));
            return row;
        }

        /// <summary>Indeterminate spinner: a three-quarter ring rotated in unscaled time (static with reduced motion).</summary>
        internal RectTransform Spinner(Transform parent, string name, float size, Color color)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(AlfaUiSpinner), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AlfaUiSkin.CircleRing();
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            image.fillAmount = 0.72f;
            image.color = color;
            image.raycastTarget = false;
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = layout.minWidth = size;
            layout.preferredHeight = layout.minHeight = size;
            layout.flexibleWidth = 0f;
            var rect = node.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        internal static GameObject Node(string name, Transform parent, params Type[] components)
        {
            var all = new Type[components.Length + 1];
            all[0] = typeof(RectTransform);
            Array.Copy(components, 0, all, 1, components.Length);
            var node = new GameObject(name, all);
            node.transform.SetParent(parent, false);
            return node;
        }

        internal static void Fill(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void Stretch(RectTransform rect, float left, float right, float top, float bottom) => Fill(rect, left, right, top, bottom);

        internal static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                parent.GetChild(i).gameObject.SetActive(false);
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class AlfaUiEntranceMotion : MonoBehaviour
    {
        private CanvasGroup group;
        private float progress;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            progress = 0f;
            if (group != null) group.alpha = 0.55f;
        }

        private void Update()
        {
            if (group == null || progress >= 1f) return;
            if (AlfaUiMotionPreferences.ReducedMotion)
            {
                progress = 1f;
                group.alpha = 1f;
                return;
            }
            progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / AlfaUiTheme.EntranceDuration);
            group.alpha = Mathf.Lerp(0.55f, 1f, 1f - Mathf.Pow(1f - progress, 3f));
        }
    }

    /// <summary>Rotates an indeterminate progress ring; respects reduced motion and runs in unscaled time.</summary>
    [DisallowMultipleComponent]
    internal sealed class AlfaUiSpinner : MonoBehaviour
    {
        private const float DegreesPerSecond = 300f;

        private void Update()
        {
            if (AlfaUiMotionPreferences.ReducedMotion) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, transform.localEulerAngles.z - DegreesPerSecond * Time.unscaledDeltaTime);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class AlfaUiFocusMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private UnityEngine.UI.Image shine;
        private UnityEngine.UI.Image accent;
        private RectTransform iconPlate;
        private AlfaUiSurface surface;
        private Color shineBase;
        private Color accentBase;
        private bool pointerInside;
        private bool selected;
        private bool pressed;
        private float amount;
        private UnityEngine.UI.Selectable selectable;

        /// <summary>Menu rails move keyboard selection with the pointer so exactly one item reads as selected.</summary>
        internal bool SelectOnHover { get; set; }

        private void Awake() => selectable = GetComponent<UnityEngine.UI.Selectable>();

        internal void Bind(UnityEngine.UI.Image shineGraphic, RectTransform icon, AlfaUiSurface buttonSurface)
        {
            shine = shineGraphic;
            iconPlate = icon;
            surface = buttonSurface;
            shineBase = shine != null ? shine.color : Color.clear;
        }

        internal void BindAccent(UnityEngine.UI.Image graphic)
        {
            accent = graphic;
            accentBase = graphic != null ? graphic.color : Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (!SelectOnHover || EventSystem.current == null || selectable == null || !selectable.IsInteractable()) return;
            if (EventSystem.current.currentSelectedGameObject != gameObject) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData) { pointerInside = false; pressed = false; }
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void OnDisable()
        {
            pointerInside = selected = pressed = false;
            amount = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (selectable == null) selectable = GetComponent<UnityEngine.UI.Selectable>();
            var interactable = selectable != null && selectable.IsInteractable();
            var focused = SelectOnHover ? selected : pointerInside || selected;
            var target = interactable && focused ? (pressed ? 0.7f : 1f) : 0f;
            if (AlfaUiMotionPreferences.ReducedMotion)
            {
                pressed = false;
                if (Mathf.Approximately(amount, target)) return;
                amount = target;
                Apply(amount);
                return;
            }
            var next = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / AlfaUiTheme.FocusDuration);
            if (Mathf.Approximately(next, amount)) return;
            amount = next;
            Apply(amount);
        }

        private void Apply(float value)
        {
            if (shine != null)
            {
                var color = shineBase;
                color.a = Mathf.Clamp01(shineBase.a + value * 0.22f);
                shine.color = color;
            }
            if (accent != null)
            {
                var color = accentBase;
                color.a = Mathf.Clamp01(accentBase.a + value * 0.9f);
                accent.color = color;
            }
            if (iconPlate != null && !AlfaUiMotionPreferences.ReducedMotion) iconPlate.localScale = Vector3.one * (1f + value * 0.07f);
            else if (iconPlate != null) iconPlate.localScale = Vector3.one;
            if (surface != null) surface.SetFocus(value);
        }
    }

    internal static class AlfaUiMotionPreferences
    {
        internal static bool ReducedMotion { get; set; }
    }
}
