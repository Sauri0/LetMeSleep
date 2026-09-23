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

        internal bool HasDisplayFont => AlfaUiTheme.HasDisplayFont(dependencies);

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
            size = Mathf.Max(size, AlfaUiTheme.MinTextSize);
            text.font = heading ? AlfaUiTheme.Display(dependencies) : AlfaUiTheme.Body(dependencies);
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (heading)
            {
                // The display face is already bold; synthetic bold only when it is missing.
                text.fontStyle = HasDisplayFont ? FontStyles.Normal : FontStyles.Bold;
                text.characterSpacing = AlfaUiTheme.DisplayTracking;
            }
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = Mathf.Max(size * 1.25f, 24f);
            layout.flexibleWidth = 1f;
            return text;
        }

        /// <summary>Uppercase field caption ("NOMBRE DE LA SALA") in the display face.</summary>
        internal TextMeshProUGUI Caption(Transform parent, string name, string value, float size = AlfaUiTheme.LabelSize)
        {
            var text = Text(parent, name, value, size, AlfaUiTheme.LabelInk, TextAlignmentOptions.Left, true);
            text.characterSpacing = AlfaUiTheme.CaptionTracking;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>
        /// Panel / screen title in the upright condensed display face (UI-06 draws no comic face outside the
        /// logo) with a soft ink drop shadow so it holds over the live scene.
        /// </summary>
        internal TextMeshProUGUI Title(Transform parent, string name, string value, float size = AlfaUiTheme.PanelTitleSize, Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = Text(parent, name, value, size, color ?? AlfaUiTheme.Sheet100, alignment, true);
            MakeDisplay(text, size);
            return text;
        }

        /// <summary>Switches a label to the display face with the shared soft-shadow material.</summary>
        internal void MakeDisplay(TextMeshProUGUI text, float size = -1f)
        {
            if (text == null) return;
            text.font = AlfaUiTheme.Display(dependencies);
            text.fontStyle = HasDisplayFont ? FontStyles.Normal : FontStyles.Bold;
            text.characterSpacing = AlfaUiTheme.DisplayTracking;
            if (size > 0f) text.fontSize = Mathf.Max(size, AlfaUiTheme.MinTextSize);
            var material = ShadowedMaterial(text.font);
            if (material != null) text.fontSharedMaterial = material;
        }

        private static readonly Dictionary<TMP_FontAsset, Material> OutlinedMaterials = new Dictionary<TMP_FontAsset, Material>();

        /// <summary>
        /// Big screen titles (pause, results): the display face with the sketch's comic treatment, a #0B1426 ink
        /// contour and a hard 4-unit drop shadow underneath (UI-06 "¡HUMANOS GANAN!", "PARTIDA EN PAUSA").
        /// </summary>
        internal void OutlineTitle(TextMeshProUGUI text, float size = -1f)
        {
            if (text == null) return;
            MakeDisplay(text, size);
            var material = OutlinedMaterial(text.font);
            if (material != null) text.fontSharedMaterial = material;
        }

        private static Material OutlinedMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (OutlinedMaterials.TryGetValue(font, out var cached) && cached != null) return cached;
            var material = new Material(font.material) { name = font.name + " UI outlined title", hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                // The SDF spread (padding 9 at 72 pt) limits the contour; dilating the face keeps the letters full
                // while the ink ring grows outwards (about 6-7 % of the em at title sizes).
                material.SetColor(ShaderUtilities.ID_OutlineColor, AlfaUiTheme.Ink900);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.42f);
                material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.36f);
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.85f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.62f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.36f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
            }
            OutlinedMaterials[font] = material;
            return material;
        }

        /// <summary>Disabled keeps the button's intent at half opacity (APLICAR stays green, dimmed).</summary>
        internal static void KeepIntentWhenDisabled(UnityEngine.UI.Button button)
        {
            var surface = button != null ? button.GetComponent<AlfaUiSurface>() : null;
            if (surface == null) return;
            surface.DisabledKeepsIntent = true;
            surface.Refresh();
        }

        private static Material ShadowedMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (ShadowedMaterials.TryGetValue(font, out var cached) && cached != null) return cached;
            var material = new Material(font.material) { name = font.name + " UI shadow", hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.6f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.1f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
            }
            ShadowedMaterials[font] = material;
            return material;
        }

        /// <summary>
        /// UI-06 wordmark: the "LET ME / SLEEP" artwork (Resources/AlfaUiBrand/LogoWordmark, rendered by
        /// docs/unity/ui/tools/build_ui_brand.py: rounded heavy letters, cream and sky gradients, 9 px ink outline,
        /// 6 px extrusion), the cartoon mosquito next to "SLEEP" and the cream subtitle. The artwork is 2x the
        /// 1080p size. Without it, the words fall back to the display face in the same colours.
        /// </summary>
        internal RectTransform BrandLockup(Transform parent, string name, float width = 660f, string subtitle = "HUMANOS CONTRA MOSQUITOS")
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.LayoutElement));
            var wordmarkSprite = BrandSprite("LogoWordmark");
            var aspect = wordmarkSprite != null ? wordmarkSprite.rect.height / wordmarkSprite.rect.width : 0.47f;
            var markHeight = width * aspect;
            if (wordmarkSprite != null)
            {
                var mark = Node("Wordmark", root.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                mark.sprite = wordmarkSprite;
                mark.raycastTarget = false;
                mark.preserveAspect = true;
                Anchor(mark.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, markHeight));
            }
            else
            {
                var first = Text(root.transform, "LogoFirstLine", "LET <size=75%>ME</size>", 132f, AlfaUiTheme.Hex("FFE4A8"), TextAlignmentOptions.TopLeft, true);
                first.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(first.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, 0f), new Vector2(width, 150f));
                var second = Text(root.transform, "LogoSecondLine", "SLEEP", 176f, AlfaUiTheme.Hex("8ED2FA"), TextAlignmentOptions.TopLeft, true);
                second.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(second.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -120f), new Vector2(width, 200f));
            }

            var mosquitoSprite = BrandSprite("LogoMosquito");
            if (mosquitoSprite != null)
            {
                var mosquito = Node("LogoMosquito", root.transform, typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                mosquito.sprite = mosquitoSprite;
                mosquito.preserveAspect = true;
                mosquito.raycastTarget = false;
                // Flying just right of "SLEEP", level with the top of its "P".
                Anchor(mosquito.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(width + 44f, -markHeight * 0.34f), new Vector2(124f, 124f));
                mosquito.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            }

            var subtitleHeight = 0f;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var caption = Text(root.transform, "Subtitle", subtitle, 30f, AlfaUiTheme.LogoCream, TextAlignmentOptions.TopLeft, true);
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.overflowMode = TextOverflowModes.Overflow;
                caption.characterSpacing = 6f;
                MakeDisplay(caption, 30f);
                caption.characterSpacing = 6f;
                subtitleHeight = 42f;
                Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(width * 0.05f, -markHeight - 4f), new Vector2(width, subtitleHeight));
            }
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = markHeight + subtitleHeight + 4f;
            layout.preferredWidth = width;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, layout.preferredHeight);
            return rect;
        }

        private static readonly Dictionary<string, Sprite> BrandSprites = new Dictionary<string, Sprite>();

        internal static Sprite BrandSprite(string name)
        {
            if (BrandSprites.TryGetValue(name, out var cached) && cached != null) return cached;
            var sprite = Resources.Load<Sprite>("AlfaUiBrand/" + name);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>("AlfaUiBrand/" + name);
                if (texture != null) sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            BrandSprites[name] = sprite;
            return sprite;
        }

        internal static void QuietButton(UnityEngine.UI.Button button, float labelSize = 22f)
        {
            ApplyStyle(button, AlfaButtonStyle.Quiet);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.fontSize = Mathf.Max(labelSize, AlfaUiTheme.MinTextSize);
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
            var text = Text(node.transform, "Label", label, AlfaUiTheme.ButtonSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
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
                label.color = label.name == "Subtitle" ? AlfaUiTheme.WithAlpha(content, 0.8f) : content;
            // Swatch check marks keep their contrast colour against the swatch.
            foreach (var symbol in button.GetComponentsInChildren<AlfaUiIcon>(true)) if (symbol.name != "SelectionMark") symbol.color = content;
            if (surface != null) surface.ThickFrame = false;
            var motion = button.GetComponent<AlfaUiFocusMotion>();
            if (motion != null)
            {
                motion.SelectOnHover = style == AlfaButtonStyle.Menu;
                motion.CaptureContent();
            }
        }

        /// <summary>
        /// Selection state for tabs, options and slots (UI-06): primary blue fill plus a 3-unit accent.blue
        /// (#49B2FF) frame. Unselected controls return to <paramref name="unselected"/>. No text prefix.
        /// </summary>
        internal static void SetSelected(UnityEngine.UI.Button button, bool selected, AlfaButtonStyle unselected = AlfaButtonStyle.Tab)
        {
            if (button == null) return;
            ApplyStyle(button, selected ? AlfaButtonStyle.Primary : unselected);
            MarkSelectedFrame(button, selected);
        }

        /// <summary>3-unit accent.blue frame on any skinned surface (buttons, slot plates).</summary>
        internal static void MarkSelectedFrame(Component target, bool selected)
        {
            var surface = target != null ? target.GetComponent<AlfaUiSurface>() : null;
            if (surface == null) return;
            surface.ThickFrame = selected;
            if (selected)
            {
                surface.FrameColor = AlfaUiTheme.Sky400;
                surface.FocusFrameColor = Color.Lerp(AlfaUiTheme.Sky400, Color.white, 0.45f);
                surface.ShadowColor = AlfaUiTheme.WithAlpha(AlfaUiTheme.PrimaryHi, 0.45f);
            }
            surface.Refresh();
        }

        /// <summary>Puts a factory button label in the display face at a given size (menu rail 30, CTAs 34).</summary>
        internal void StrongLabel(UnityEngine.UI.Button button, float size)
        {
            var label = button != null ? button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>() : null;
            if (label == null) return;
            MakeDisplay(label, size);
        }

        internal UnityEngine.UI.Button FeatureButton(Transform parent, string name, string title, string subtitle,
            UnityAction callback, AlfaUiIconKind icon, bool primary = false, bool destructive = false, float height = 76f)
        {
            var button = Button(parent, name, title, callback, primary, destructive, height, icon);
            var titleText = button.GetComponentInChildren<TextMeshProUGUI>();
            titleText.fontSize = 25f;
            titleText.alignment = TextAlignmentOptions.BottomLeft;
            var textLeft = Mathf.Clamp(height - 20f, 30f, 46f) + 30f;
            Fill(titleText.rectTransform, textLeft, 16f, 4f, height * 0.5f);
            var subtitleText = Text(button.transform, "Subtitle", subtitle, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft, true);
            subtitleText.characterSpacing = 1f;
            subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            Fill(subtitleText.rectTransform, textLeft, 16f, height * 0.5f, 2f);
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

        private static Sprite linearFadeSprite;

        /// <summary>Opaque at the left edge, fading linearly to transparent at the right edge.</summary>
        internal static Sprite LinearFadeSprite()
        {
            if (linearFadeSprite != null) return linearFadeSprite;
            const int length = 256;
            var texture = new Texture2D(length, 4, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI linear fade",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[length * 4];
            for (var i = 0; i < length; i++)
            {
                var alpha = (byte)Mathf.RoundToInt(255f * (1f - i / (length - 1f)));
                for (var j = 0; j < 4; j++) pixels[j * length + i] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            linearFadeSprite = Sprite.Create(texture, new Rect(0f, 0f, length, 4), new Vector2(0.5f, 0.5f), 100f);
            linearFadeSprite.name = texture.name;
            linearFadeSprite.hideFlags = HideFlags.HideAndDontSave;
            return linearFadeSprite;
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
            Title(row, "Label", label, AlfaUiTheme.PanelTitleSize, AlfaUiTheme.Sheet100);
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
            var valueText = Text(viewport.transform, "Text", string.Empty, code ? 28f : AlfaUiTheme.BodySize,
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
            input.colors = FieldColors();
            var motion = node.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, null, surface);
            return input;
        }

        /// <summary>Tint block for fields (inputs, toggles, dropdowns): these keep a dimmed look when disabled.</summary>
        private static UnityEngine.UI.ColorBlock FieldColors()
        {
            var colors = AlfaUiTheme.TintColors(AlfaButtonStyle.Secondary);
            colors.disabledColor = new Color(0.62f, 0.66f, 0.74f, 0.6f);
            return colors;
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
            toggle.colors = FieldColors();
            toggle.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            if (callback != null) toggle.onValueChanged.AddListener(callback);
            var motion = box.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(null, null, surface);
            Text(row, "Label", label, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
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
            dropdown.colors = FieldColors();
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

        internal RectTransform ScrollView(Transform parent, string name, out RectTransform content, float preferredHeight, bool scrollbar = false)
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
            if (scrollbar)
            {
                // Visible only when the content overflows; the viewport gives it room then (UI-06 lists).
                var bar = Node("Scrollbar", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Scrollbar));
                var barRect = bar.GetComponent<RectTransform>();
                Anchor(barRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(8f, -12f));
                var track = bar.GetComponent<UnityEngine.UI.Image>();
                track.sprite = AlfaUiSkin.Fill(6f);
                track.type = UnityEngine.UI.Image.Type.Sliced;
                track.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.7f);
                var slide = Node("Sliding Area", bar.transform);
                Fill(slide.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
                var handle = Node("Handle", slide.transform, typeof(UnityEngine.UI.Image));
                var handleImage = handle.GetComponent<UnityEngine.UI.Image>();
                handleImage.sprite = AlfaUiSkin.Fill(6f);
                handleImage.type = UnityEngine.UI.Image.Type.Sliced;
                handleImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Sky400, 0.85f);
                Fill(handle.GetComponent<RectTransform>());
                var scrollbarComponent = bar.GetComponent<UnityEngine.UI.Scrollbar>();
                scrollbarComponent.handleRect = handle.GetComponent<RectTransform>();
                scrollbarComponent.targetGraphic = handleImage;
                scrollbarComponent.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
                scrollbarComponent.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                scroll.verticalScrollbar = scrollbarComponent;
                scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scroll.verticalScrollbarSpacing = 6f;
            }
            return root.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Scroll view whose content is a fixed-column grid (UI-06 swatch and thumbnail grids). Same frame, mask and
        /// optional auto-hiding scrollbar as <see cref="ScrollView"/>.
        /// </summary>
        internal RectTransform GridScrollView(Transform parent, string name, out RectTransform content, float preferredHeight,
            Vector2 cellSize, Vector2 spacing, int columns, bool scrollbar = true)
        {
            var root = ScrollView(parent, name, out content, preferredHeight, scrollbar);
            var vertical = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vertical != null) UnityEngine.Object.DestroyImmediate(vertical);
            var grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            ConfigureGrid(grid, cellSize, spacing, columns);
            return root;
        }

        internal static void ConfigureGrid(UnityEngine.UI.GridLayoutGroup grid, Vector2 cellSize, Vector2 spacing, int columns)
        {
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset(2, 2, 2, 2);
        }

        /// <summary>Keyboard key cap (UI-06 control legend): light rounded cap with the key in the display face.</summary>
        internal RectTransform KeyCap(Transform parent, string name, string key, float height = 40f)
        {
            var cap = Panel(parent, name, AlfaUiTheme.Hex("DDE6F5"), -1f, height, AlfaUiTheme.SmallRadius);
            SetSurface(cap, Color.white, AlfaUiTheme.Hex("B9C7DD"), AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.55f), AlfaUiTheme.WithAlpha(Color.black, 0.35f));
            var label = Text(cap, "Key", key, AlfaUiTheme.MinTextSize, AlfaUiTheme.Ink900, TextAlignmentOptions.Center, true);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.characterSpacing = 1f;
            Fill(label.rectTransform, 8f, 8f, 2f, 2f);
            var width = Mathf.Max(height, label.GetPreferredValues(key, 1000f, height).x + 20f);
            var layout = cap.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = height;
            layout.flexibleWidth = 0f;
            cap.sizeDelta = new Vector2(width, height);
            return cap;
        }

        /// <summary>Rounded progress track with a gradient fill; set the fill's anchorMax.x to the ratio.</summary>
        internal static UnityEngine.UI.Image ProgressBar(Transform parent, string name, Color fill, out RectTransform track)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Image));
            var trackImage = root.GetComponent<UnityEngine.UI.Image>();
            trackImage.sprite = AlfaUiSkin.Fill(6f);
            trackImage.type = UnityEngine.UI.Image.Type.Sliced;
            trackImage.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Ink900, 0.85f);
            trackImage.raycastTarget = false;
            track = root.GetComponent<RectTransform>();
            var fillNode = Node("Fill", root.transform, typeof(UnityEngine.UI.Image), typeof(AlfaUiSurface));
            var fillImage = fillNode.GetComponent<UnityEngine.UI.Image>();
            fillImage.sprite = AlfaUiSkin.Fill(6f);
            fillImage.type = UnityEngine.UI.Image.Type.Sliced;
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;
            var surface = fillNode.GetComponent<AlfaUiSurface>();
            surface.RadiusKey = 6;
            SetBarColor(fillImage, fill);
            Fill(fillImage.rectTransform);
            fillImage.rectTransform.anchorMax = new Vector2(0f, 1f);
            return fillImage;
        }

        internal static void SetBarColor(UnityEngine.UI.Image fill, Color color)
        {
            SetSurface(fill, Color.Lerp(color, Color.white, 0.22f), Color.Lerp(color, Color.black, 0.12f), Color.clear, Color.clear);
        }

        private static Sprite radialVignetteSprite;

        /// <summary>Transparent centre fading to opaque corners: soft studio vignette over the 3D viewer.</summary>
        internal static Sprite RadialVignetteSprite()
        {
            if (radialVignetteSprite != null) return radialVignetteSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI radial vignette",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var t = Mathf.InverseLerp(0.45f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy));
                var alpha = t * t * (3f - 2f * t);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            radialVignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            radialVignetteSprite.name = texture.name;
            radialVignetteSprite.hideFlags = HideFlags.HideAndDontSave;
            return radialVignetteSprite;
        }

        private static Sprite radialGlowSprite;

        /// <summary>Opaque centre fading to transparent edges: soft team-coloured light behind the results winner.</summary>
        internal static Sprite RadialGlowSprite()
        {
            if (radialGlowSprite != null) return radialGlowSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI radial glow",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                var alpha = 1f - t * t * (3f - 2f * t);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha * alpha));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            radialGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            radialGlowSprite.name = texture.name;
            radialGlowSprite.hideFlags = HideFlags.HideAndDontSave;
            return radialGlowSprite;
        }

        /// <summary>Anchors a rect to a parent region with explicit edge offsets (stretched columns).</summary>
        internal static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>
        /// UI-06 list row: inset well, white title in the display face, secondary subtitle and, when it carries
        /// real state (ready / not ready), a coloured status bar on the right.
        /// </summary>
        internal RectTransform ListRow(Transform parent, string name, AlfaUiIconKind icon, string title, string subtitle,
            Color status, float height = 74f, bool statusBar = true)
        {
            var row = Inset(parent, name, height);
            row.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = height;
            if (icon != AlfaUiIconKind.None)
            {
                var symbol = Icon(row, "RowIcon", icon, AlfaUiTheme.Sky400);
                Anchor(symbol.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(32f, 32f));
            }
            var left = icon == AlfaUiIconKind.None ? 18f : 62f;
            var right = statusBar ? 26f : 14f;
            var titleText = Text(row, "RowTitle", title, 24f, AlfaUiTheme.Sheet100, TextAlignmentOptions.BottomLeft, true);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(left, -1f), new Vector2(-left - right, -4f));
            var subtitleText = Text(row, "RowSubtitle", subtitle, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.TopLeft);
            subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(left, 1f), new Vector2(-left - right, -4f));
            var bar = Node("StatusBar", row, typeof(UnityEngine.UI.Image));
            var barImage = bar.GetComponent<UnityEngine.UI.Image>();
            barImage.sprite = AlfaUiSkin.Fill(6f);
            barImage.type = UnityEngine.UI.Image.Type.Sliced;
            barImage.color = status;
            barImage.raycastTarget = false;
            Anchor(bar.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-9f, 0f), new Vector2(6f, -20f));
            bar.SetActive(statusBar);
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
        private bool? shownInteractable;
        private readonly List<KeyValuePair<UnityEngine.UI.Graphic, float>> content = new List<KeyValuePair<UnityEngine.UI.Graphic, float>>();
        private readonly List<KeyValuePair<AlfaUiIcon, float>> icons = new List<KeyValuePair<AlfaUiIcon, float>>();

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

        /// <summary>
        /// Records label/icon opacity after a style change so that the disabled look (flat navy, content at
        /// 50 %) can be applied and removed without losing authored alpha.
        /// </summary>
        internal void CaptureContent()
        {
            content.Clear();
            icons.Clear();
            foreach (var label in GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) content.Add(new KeyValuePair<UnityEngine.UI.Graphic, float>(label, label.color.a));
            foreach (var symbol in GetComponentsInChildren<AlfaUiIcon>(true)) icons.Add(new KeyValuePair<AlfaUiIcon, float>(symbol, symbol.color.a));
            shownInteractable = null;
            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            if (selectable == null) selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (!(selectable is UnityEngine.UI.Button)) return;
            var interactable = selectable.IsInteractable();
            if (shownInteractable == interactable) return;
            shownInteractable = interactable;
            if (surface != null)
            {
                surface.Disabled = !interactable;
                surface.Refresh();
            }
            AlfaUiTheme.DisabledColors(out _, out _, out _, out var disabledAlpha);
            var factor = interactable ? 1f : disabledAlpha;
            foreach (var pair in content)
            {
                if (pair.Key == null) continue;
                var color = pair.Key.color;
                color.a = pair.Value * factor;
                pair.Key.color = color;
            }
            foreach (var pair in icons)
            {
                if (pair.Key == null) continue;
                var color = pair.Key.color;
                color.a = pair.Value * factor;
                pair.Key.color = color;
            }
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

        private void OnEnable()
        {
            shownInteractable = null;
            RefreshInteractable();
        }

        private void Update()
        {
            if (selectable == null) selectable = GetComponent<UnityEngine.UI.Selectable>();
            RefreshInteractable();
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
