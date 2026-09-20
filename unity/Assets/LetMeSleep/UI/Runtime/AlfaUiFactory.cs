using System;
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
        private static Sprite roundedSprite;
        private static Sprite horizontalFadeSprite;

        internal AlfaUiFactory(AlfaUiDependencies dependencies, Action<UiFeedbackKind> feedback = null)
        {
            this.dependencies = dependencies ?? new AlfaUiDependencies();
            this.feedback = feedback;
        }

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

        internal static UnityEngine.UI.Shadow PlainShadow(GameObject node)
        {
            foreach (var effect in node.GetComponents<UnityEngine.UI.Shadow>())
                if (effect.GetType() == typeof(UnityEngine.UI.Shadow)) return effect;
            return null;
        }

        internal RectTransform Panel(Transform parent, string name, Color? color = null, float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline), typeof(UnityEngine.UI.Shadow), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = color ?? AlfaUiTheme.Night700;
            image.raycastTarget = false;
            image.sprite = dependencies.PanelSprite != null ? dependencies.PanelSprite : RoundedSprite();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var outline = node.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.68f);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            var shadow = PlainShadow(node);
            shadow.effectColor = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.55f);
            shadow.effectDistance = new Vector2(0f, -5f);
            var topEdge = Node("TopEdge", node.transform, typeof(UnityEngine.UI.Image));
            var topEdgeImage = topEdge.GetComponent<UnityEngine.UI.Image>();
            topEdgeImage.color = new Color(AlfaUiTheme.Sheet100.r, AlfaUiTheme.Sheet100.g, AlfaUiTheme.Sheet100.b, 0.18f);
            topEdgeImage.raycastTarget = false;
            var topEdgeRect = topEdge.GetComponent<RectTransform>();
            topEdgeRect.anchorMin = new Vector2(0f, 1f);
            topEdgeRect.anchorMax = Vector2.one;
            topEdgeRect.pivot = new Vector2(0.5f, 1f);
            topEdgeRect.offsetMin = new Vector2(3f, -3f);
            topEdgeRect.offsetMax = new Vector2(-3f, 0f);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            if (preferredWidth > 0f) layout.preferredWidth = preferredWidth;
            if (preferredHeight > 0f) layout.preferredHeight = preferredHeight;
            return node.GetComponent<RectTransform>();
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

        internal TextMeshProUGUI LogoText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = Text(parent, name, value, size, color, alignment, true);
            text.font = AlfaUiTheme.Logo(dependencies);
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            return text;
        }

        internal RectTransform BrandLockup(Transform parent, string name, float height = 210f)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.LayoutElement));
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            var first = LogoText(root.transform, "LogoFirstLine", "LET ME", 84f, AlfaUiTheme.Sheet100);
            Fill(first.rectTransform, 0f, 0f, 0f, 112f);
            var second = LogoText(root.transform, "LogoSecondLine", "SLEEP", 112f, AlfaUiTheme.Sheet100);
            Fill(second.rectTransform, 0f, 98f, 86f, 0f);
            // Vertex colours keep the editable wordmark crisp at both target resolutions.
            first.color = second.color = Color.white;
            first.enableVertexGradient = second.enableVertexGradient = true;
            first.colorGradient = new VertexGradient(AlfaUiTheme.Sheet100, AlfaUiTheme.Sheet100,
                AlfaUiTheme.Lamp400, AlfaUiTheme.Lamp400);
            second.colorGradient = new VertexGradient(AlfaUiTheme.Sheet100, AlfaUiTheme.Sheet100,
                AlfaUiTheme.Sky400, AlfaUiTheme.Sky400);
            first.outlineColor = second.outlineColor = AlfaUiTheme.Ink900;
            first.outlineWidth = second.outlineWidth = 0.14f;

            var mosquito = Icon(root.transform, "MosquitoMark", AlfaUiIconKind.Mosquito, AlfaUiTheme.Pajama500);
            var rect = mosquito.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(244f, 17f);
            rect.sizeDelta = new Vector2(76f, 76f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -14f);
            return root.GetComponent<RectTransform>();
        }

        internal static void NightPrimaryButton(UnityEngine.UI.Button button)
        {
            // Local menu treatment: readable light labels on saturated blue, including focus.
            var colors = button.colors;
            colors.normalColor = new Color32(17, 79, 139, 255);
            colors.highlightedColor = new Color32(22, 96, 163, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color32(12, 57, 104, 255);
            button.colors = colors;
            foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>())
                label.color = AlfaUiTheme.Sheet100;
            foreach (var symbol in button.GetComponentsInChildren<AlfaUiIcon>())
                symbol.color = AlfaUiTheme.Sheet100;
            button.GetComponent<UnityEngine.UI.Outline>().effectColor = AlfaUiTheme.Sky400;
        }

        internal static void QuietButton(UnityEngine.UI.Button button, float labelSize = 22f)
        {
            var colors = button.colors;
            colors.normalColor = AlfaUiTheme.Night800;
            colors.highlightedColor = AlfaUiTheme.Night600;
            colors.selectedColor = Color.Lerp(AlfaUiTheme.Night600, AlfaUiTheme.Sky400, 0.22f);
            button.colors = colors;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = labelSize;
            label.color = AlfaUiTheme.Moon200;
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.4f);
            outline.effectDistance = new Vector2(1f, -1f);
            var symbol = button.GetComponentInChildren<AlfaUiIcon>();
            if (symbol != null) symbol.color = AlfaUiTheme.Moon200;
            PlainShadow(button.gameObject).enabled = false;
        }

        internal UnityEngine.UI.Button Button(Transform parent, string name, string label, UnityAction callback,
            bool primary = false, bool destructive = false, float height = 58f, AlfaUiIconKind icon = AlfaUiIconKind.None,
            bool emitConfirm = true)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement), typeof(UnityEngine.UI.Outline), typeof(UnityEngine.UI.Shadow));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : RoundedSprite();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var button = node.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.colors = AlfaUiTheme.ButtonColors(primary, destructive);
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
            var outline = node.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = primary ? AlfaUiTheme.Sheet100 : new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = PlainShadow(node);
            shadow.effectColor = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.68f);
            shadow.effectDistance = new Vector2(0f, -5f);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = Mathf.Max(44f, height);
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;
            var text = Text(node.transform, "Label", label, AlfaUiTheme.ButtonSize,
                destructive ? AlfaUiTheme.Sheet100 : primary ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100,
                TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 0.8f;
            var iconPlateSize = Mathf.Clamp(height - 16f, 34f, 50f);
            Fill(text.rectTransform, icon == AlfaUiIconKind.None ? 16f : iconPlateSize + 34f, 18f, 8f, 8f);
            RectTransform iconPlate = null;
            if (icon != AlfaUiIconKind.None)
            {
                var plate = Node("IconPlate", node.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
                var plateImage = plate.GetComponent<UnityEngine.UI.Image>();
                plateImage.color = primary ? new Color(AlfaUiTheme.Sheet100.r, AlfaUiTheme.Sheet100.g, AlfaUiTheme.Sheet100.b, 0.08f) :
                    new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.12f);
                plateImage.raycastTarget = false;
                var plateOutline = plate.GetComponent<UnityEngine.UI.Outline>();
                plateOutline.effectColor = primary ? new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.38f) :
                    new Color(AlfaUiTheme.Moon200.r, AlfaUiTheme.Moon200.g, AlfaUiTheme.Moon200.b, 0.22f);
                plateOutline.enabled = false;
                var plateRect = plate.GetComponent<RectTransform>();
                iconPlate = plateRect;
                plateRect.anchorMin = new Vector2(0f, 0.5f);
                plateRect.anchorMax = new Vector2(0f, 0.5f);
                plateRect.pivot = new Vector2(0f, 0.5f);
                plateRect.anchoredPosition = new Vector2(12f, 0f);
                plateRect.sizeDelta = new Vector2(iconPlateSize, iconPlateSize);

                var iconGraphic = Icon(plate.transform, "Icon", icon,
                    destructive ? AlfaUiTheme.Sheet100 : primary ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100);
                Fill(iconGraphic.rectTransform, 3f, 3f, 3f, 3f);
            }
            var shine = Node("Shine", node.transform, typeof(UnityEngine.UI.Image));
            var shineImage = shine.GetComponent<UnityEngine.UI.Image>();
            shineImage.color = new Color(AlfaUiTheme.Sheet100.r, AlfaUiTheme.Sheet100.g, AlfaUiTheme.Sheet100.b, primary ? 0.28f : 0.12f);
            shineImage.raycastTarget = false;
            var shineRect = shine.GetComponent<RectTransform>();
            shineRect.anchorMin = new Vector2(0f, 1f);
            shineRect.anchorMax = Vector2.one;
            shineRect.pivot = new Vector2(0.5f, 1f);
            shineRect.offsetMin = new Vector2(3f, -2f);
            shineRect.offsetMax = new Vector2(-3f, 0f);
            var lowerBevel = Node("LowerBevel", node.transform, typeof(UnityEngine.UI.Image));
            var lowerBevelImage = lowerBevel.GetComponent<UnityEngine.UI.Image>();
            lowerBevelImage.color = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.34f);
            lowerBevelImage.raycastTarget = false;
            var lowerBevelRect = lowerBevel.GetComponent<RectTransform>();
            lowerBevelRect.anchorMin = Vector2.zero;
            lowerBevelRect.anchorMax = new Vector2(1f, 0f);
            lowerBevelRect.pivot = new Vector2(0.5f, 0f);
            lowerBevelRect.offsetMin = new Vector2(6f, 2f);
            lowerBevelRect.offsetMax = new Vector2(-6f, 5f);
            var motion = node.AddComponent<AlfaUiFocusMotion>();
            motion.Bind(shineImage, iconPlate, shadow);
            return button;
        }

        internal UnityEngine.UI.Button FeatureButton(Transform parent, string name, string title, string subtitle,
            UnityAction callback, AlfaUiIconKind icon, bool primary = false, bool destructive = false, float height = 76f)
        {
            var button = Button(parent, name, title, callback, primary, destructive, height, icon);
            var titleText = button.GetComponentInChildren<TextMeshProUGUI>();
            titleText.fontSize = 22f;
            titleText.alignment = TextAlignmentOptions.Left;
            Fill(titleText.rectTransform, 82f, 16f, 7f, 32f);
            var subtitleText = Text(button.transform, "Subtitle", subtitle, 16f,
                destructive ? AlfaUiTheme.Sheet100 : primary ? AlfaUiTheme.Ink900 : AlfaUiTheme.Moon200,
                TextAlignmentOptions.Left, true);
            subtitleText.characterSpacing = 0.6f;
            Fill(subtitleText.rectTransform, 82f, 16f, 40f, 5f);
            var rail = Node("FocusRail", button.transform, typeof(UnityEngine.UI.Image));
            var railImage = rail.GetComponent<UnityEngine.UI.Image>();
            railImage.color = new Color(AlfaUiTheme.Lamp400.r, AlfaUiTheme.Lamp400.g, AlfaUiTheme.Lamp400.b, primary ? 0.9f : 0.22f);
            railImage.raycastTarget = false;
            var railRect = rail.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(0f, 0.18f);
            railRect.anchorMax = new Vector2(0f, 0.82f);
            railRect.pivot = new Vector2(0f, 0.5f);
            railRect.anchoredPosition = new Vector2(4f, 0f);
            railRect.sizeDelta = new Vector2(5f, 0f);
            button.GetComponent<AlfaUiFocusMotion>().BindAccent(railImage);
            return button;
        }

        private static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 48;
            const float radius = 12f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI rounded surface",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nearestX = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var nearestY = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(nearestX, nearestY));
                var alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(radius + 0.5f - distance));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
            roundedSprite.name = "LMS UI rounded surface";
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }

        internal static Sprite HorizontalFadeSprite()
        {
            if (horizontalFadeSprite != null) return horizontalFadeSprite;
            const int width = 256;
            const int height = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI horizontal fade",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var x = 0; x < width; x++)
            {
                var t = Mathf.InverseLerp(0.30f, 1f, x / (width - 1f));
                var alpha = (byte)Mathf.RoundToInt(255f * (1f - t * t * (3f - 2f * t)));
                for (var y = 0; y < height; y++) pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            horizontalFadeSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            horizontalFadeSprite.name = "LMS UI horizontal fade";
            horizontalFadeSprite.hideFlags = HideFlags.HideAndDontSave;
            return horizontalFadeSprite;
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
            var badge = Panel(row, "Badge", new Color(color.r, color.g, color.b, 0.22f), 44f, 44f);
            PlainShadow(badge.gameObject).enabled = false;
            var symbol = Icon(badge, "Symbol", icon, color);
            Fill(symbol.rectTransform, 7f, 7f, 7f, 7f);
            Text(row, "Label", label, AlfaUiTheme.H2Size, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left, true);
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
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(TMP_InputField), typeof(UnityEngine.UI.LayoutElement), typeof(UnityEngine.UI.Outline));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = AlfaUiTheme.Night800;
            image.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : RoundedSprite();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var outline = node.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = AlfaUiTheme.Border;
            outline.effectDistance = new Vector2(2f, -2f);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
            layout.flexibleWidth = 1f;

            var viewport = Node("Text Area", node.transform, typeof(UnityEngine.UI.RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 18f, 18f, 8f, 8f);
            var placeholderText = Text(viewport.transform, "Placeholder", placeholder, AlfaUiTheme.BodySize,
                new Color(AlfaUiTheme.Moon200.r, AlfaUiTheme.Moon200.g, AlfaUiTheme.Moon200.b, 0.68f), TextAlignmentOptions.Left);
            Fill(placeholderText.rectTransform);
            placeholderText.fontStyle = FontStyles.Italic;
            var valueText = Text(viewport.transform, "Text", string.Empty, code ? 24f : AlfaUiTheme.BodySize,
                code ? AlfaUiTheme.Lamp400 : AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            Fill(valueText.rectTransform);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Masking;

            var input = node.GetComponent<TMP_InputField>();
            input.textViewport = viewportRect;
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = maxLength;
            input.richText = false;
            input.caretColor = AlfaUiTheme.Lamp400;
            input.selectionColor = new Color(AlfaUiTheme.Sky400.r, AlfaUiTheme.Sky400.g, AlfaUiTheme.Sky400.b, 0.5f);
            input.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
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
            Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 17f, 17f);
            background.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            background.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            background.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var fillArea = Node("Fill Area", root.transform);
            Stretch(fillArea.GetComponent<RectTransform>(), 8f, 8f, 17f, 17f);
            var fill = Node("Fill", fillArea.transform, typeof(UnityEngine.UI.Image));
            Fill(fill.GetComponent<RectTransform>());
            fill.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Lamp400;
            fill.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            fill.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var handleArea = Node("Handle Slide Area", root.transform);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRect.sizeDelta = new Vector2(-18f, 28f);
            var handle = Node("Handle", handleArea.transform, typeof(UnityEngine.UI.Image));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(22f, 22f);
            handle.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Sheet100;
            handle.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            handle.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var slider = root.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            if (callback != null) slider.onValueChanged.AddListener(callback);
            return slider;
        }

        internal UnityEngine.UI.Toggle Toggle(Transform parent, string name, string label, UnityAction<bool> callback)
        {
            var row = Horizontal(parent, name, 12f);
            var box = Node("Box", row, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.LayoutElement));
            box.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 44f;
            box.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44f;
            box.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night600;
            box.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            box.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var check = Node("Check", box.transform, typeof(UnityEngine.UI.Image));
            Fill(check.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
            check.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Mint400;
            check.GetComponent<UnityEngine.UI.Image>().sprite = AlfaUiIcon.GetSprite(AlfaUiIconKind.Ready);
            check.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
            check.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var toggle = box.GetComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = box.GetComponent<UnityEngine.UI.Image>();
            toggle.graphic = check.GetComponent<UnityEngine.UI.Image>();
            toggle.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            if (callback != null) toggle.onValueChanged.AddListener(callback);
            Text(row, "Label", label, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.Left);
            return toggle;
        }

        internal TMP_Dropdown Dropdown(Transform parent, string name, System.Collections.Generic.IReadOnlyList<string> options,
            UnityAction<int> callback)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(TMP_Dropdown), typeof(UnityEngine.UI.LayoutElement), typeof(UnityEngine.UI.Outline));
            var background = root.GetComponent<UnityEngine.UI.Image>();
            background.color = AlfaUiTheme.Night600;
            background.sprite = dependencies.ButtonSprite != null ? dependencies.ButtonSprite : RoundedSprite();
            background.type = UnityEngine.UI.Image.Type.Sliced;
            root.GetComponent<UnityEngine.UI.Outline>().effectColor = AlfaUiTheme.Border;
            root.GetComponent<UnityEngine.UI.Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
            layout.flexibleWidth = 1f;

            var caption = Text(root.transform, "Label", string.Empty, AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            Fill(caption.rectTransform, 18f, 54f, 8f, 8f);
            var arrow = Text(root.transform, "Arrow", "▼", AlfaUiTheme.NoteSize, AlfaUiTheme.Lamp400, TextAlignmentOptions.Center);
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.offsetMin = new Vector2(-48f, 0f);
            arrowRect.offsetMax = Vector2.zero;

            var template = Node("Template", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = Vector2.zero;
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0f, 270f);
            template.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night700;
            template.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            template.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;

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
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            var item = Node("Item", content.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.LayoutElement));
            item.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44f;
            var itemBackground = item.GetComponent<UnityEngine.UI.Image>();
            itemBackground.color = AlfaUiTheme.Night600;
            itemBackground.sprite = RoundedSprite();
            itemBackground.type = UnityEngine.UI.Image.Type.Sliced;
            var check = Text(item.transform, "Item Checkmark", "✓", AlfaUiTheme.BodySize, AlfaUiTheme.Mint400, TextAlignmentOptions.Center);
            var checkRect = check.rectTransform;
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = new Vector2(0f, 1f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.offsetMin = new Vector2(8f, 0f);
            checkRect.offsetMax = new Vector2(42f, 0f);
            var itemLabel = Text(item.transform, "Item Label", "OPCIÓN", AlfaUiTheme.BodySize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            Fill(itemLabel.rectTransform, 50f, 12f, 4f, 4f);
            var itemToggle = item.GetComponent<UnityEngine.UI.Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = check;

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
            dropdown.colors = AlfaUiTheme.ButtonColors(false);
            dropdown.options.Clear();
            if (options != null)
                for (var i = 0; i < options.Count; i++) dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
            if (callback != null) dropdown.onValueChanged.AddListener(callback);
            template.SetActive(false);
            dropdown.RefreshShownValue();
            return dropdown;
        }

        internal RectTransform ScrollView(Transform parent, string name, out RectTransform content, float preferredHeight)
        {
            var root = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect), typeof(UnityEngine.UI.LayoutElement));
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.35f);
            root.GetComponent<UnityEngine.UI.Image>().sprite = RoundedSprite();
            root.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var rootLayout = root.GetComponent<UnityEngine.UI.LayoutElement>();
            rootLayout.preferredHeight = preferredHeight;
            rootLayout.flexibleHeight = 1f;
            rootLayout.flexibleWidth = 1f;
            var viewport = Node("Viewport", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            Fill(viewport.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
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

    [DisallowMultipleComponent]
    internal sealed class AlfaUiFocusMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private UnityEngine.UI.Image shine;
        private UnityEngine.UI.Image accent;
        private RectTransform iconPlate;
        private UnityEngine.UI.Shadow shadow;
        private Color shineBase;
        private Color accentBase;
        private bool pointerInside;
        private bool selected;
        private bool pressed;
        private float amount;
        private UnityEngine.UI.Selectable selectable;

        private void Awake() => selectable = GetComponent<UnityEngine.UI.Selectable>();

        internal void Bind(UnityEngine.UI.Image shineGraphic, RectTransform icon, UnityEngine.UI.Shadow buttonShadow)
        {
            shine = shineGraphic;
            iconPlate = icon;
            shadow = buttonShadow;
            shineBase = shine != null ? shine.color : Color.clear;
        }

        internal void BindAccent(UnityEngine.UI.Image graphic)
        {
            accent = graphic;
            accentBase = graphic != null ? graphic.color : Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
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
            if (AlfaUiMotionPreferences.ReducedMotion || selectable == null || !selectable.IsInteractable())
            {
                pressed = false;
                if (amount <= 0f) return;
                amount = 0f;
                Apply(0f);
                return;
            }
            var target = pointerInside || selected ? (pressed ? 0.55f : 1f) : 0f;
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
                color.a = Mathf.Clamp01(shineBase.a + value * 0.18f);
                shine.color = color;
            }
            if (accent != null)
            {
                var color = accentBase;
                color.a = Mathf.Clamp01(accentBase.a + value * 0.65f);
                accent.color = color;
            }
            if (iconPlate != null) iconPlate.localScale = Vector3.one * (1f + value * 0.07f);
            if (shadow != null) shadow.effectDistance = new Vector2(0f, -5f - value * 2f);
        }
    }

    internal static class AlfaUiMotionPreferences
    {
        internal static bool ReducedMotion { get; set; }
    }
}
