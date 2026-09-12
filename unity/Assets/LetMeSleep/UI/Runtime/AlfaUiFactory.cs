using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace LetMeSleep.UI
{
    internal sealed class AlfaUiFactory
    {
        private readonly AlfaUiDependencies dependencies;

        internal AlfaUiFactory(AlfaUiDependencies dependencies)
        {
            this.dependencies = dependencies ?? new AlfaUiDependencies();
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

        internal RectTransform Panel(Transform parent, string name, Color? color = null, float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Shadow), typeof(UnityEngine.UI.Outline), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = color ?? AlfaUiTheme.Night700;
            image.raycastTarget = false;
            image.sprite = dependencies.PanelSprite;
            image.type = dependencies.PanelSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            var outline = node.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AlfaUiTheme.Border.r, AlfaUiTheme.Border.g, AlfaUiTheme.Border.b, 0.82f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = node.GetComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.78f);
            shadow.effectDistance = new Vector2(7f, -8f);
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

        internal UnityEngine.UI.Button Button(Transform parent, string name, string label, UnityAction callback,
            bool primary = false, bool destructive = false, float height = 58f, AlfaUiIconKind icon = AlfaUiIconKind.None)
        {
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement), typeof(UnityEngine.UI.Shadow), typeof(UnityEngine.UI.Outline));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.sprite = dependencies.ButtonSprite;
            image.type = dependencies.ButtonSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            var button = node.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.colors = AlfaUiTheme.ButtonColors(primary, destructive);
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic, wrapAround = false };
            if (callback != null) button.onClick.AddListener(callback);
            var outline = node.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = primary ? AlfaUiTheme.Sheet100 : AlfaUiTheme.Border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = node.GetComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(AlfaUiTheme.Ink900.r, AlfaUiTheme.Ink900.g, AlfaUiTheme.Ink900.b, 0.92f);
            shadow.effectDistance = new Vector2(4f, -5f);
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = Mathf.Max(44f, height);
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;
            var text = Text(node.transform, "Label", label, AlfaUiTheme.ButtonSize,
                destructive ? AlfaUiTheme.Sheet100 : primary ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100,
                TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 0.8f;
            Fill(text.rectTransform, icon == AlfaUiIconKind.None ? 12f : 60f, 16f, 8f, 8f);
            if (icon != AlfaUiIconKind.None)
            {
                var iconGraphic = Icon(node.transform, "Icon", icon,
                    destructive ? AlfaUiTheme.Sheet100 : primary ? AlfaUiTheme.Ink900 : AlfaUiTheme.Sheet100);
                var iconRect = iconGraphic.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(18f, 0f);
                iconRect.sizeDelta = new Vector2(30f, 30f);
            }
            return button;
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
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 42f;
            var badge = Panel(row, "Badge", new Color(color.r, color.g, color.b, 0.16f), 38f, 38f);
            badge.GetComponent<UnityEngine.UI.Shadow>().enabled = false;
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
            var node = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(TMP_InputField), typeof(UnityEngine.UI.LayoutElement));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = AlfaUiTheme.Sheet100;
            image.sprite = dependencies.ButtonSprite;
            image.type = dependencies.ButtonSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            var layout = node.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
            layout.flexibleWidth = 1f;

            var viewport = Node("Text Area", node.transform, typeof(UnityEngine.UI.RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 18f, 18f, 8f, 8f);
            var placeholderText = Text(viewport.transform, "Placeholder", placeholder, AlfaUiTheme.BodySize,
                AlfaUiTheme.Disabled, TextAlignmentOptions.Left);
            Fill(placeholderText.rectTransform);
            placeholderText.fontStyle = FontStyles.Italic;
            var valueText = Text(viewport.transform, "Text", string.Empty, code ? 24f : AlfaUiTheme.BodySize,
                AlfaUiTheme.Ink900, TextAlignmentOptions.Left);
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
            var fillArea = Node("Fill Area", root.transform);
            Stretch(fillArea.GetComponent<RectTransform>(), 8f, 8f, 17f, 17f);
            var fill = Node("Fill", fillArea.transform, typeof(UnityEngine.UI.Image));
            Fill(fill.GetComponent<RectTransform>());
            fill.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Sky400;
            var handleArea = Node("Handle Slide Area", root.transform);
            Stretch(handleArea.GetComponent<RectTransform>(), 8f, 8f, 6f, 6f);
            var handle = Node("Handle", handleArea.transform, typeof(UnityEngine.UI.Image));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28f, 32f);
            handle.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Sheet100;
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
            var check = Node("Check", box.transform, typeof(UnityEngine.UI.Image));
            Fill(check.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
            check.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Mint400;
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
            var root = Node(name, parent, typeof(UnityEngine.UI.Image), typeof(TMP_Dropdown), typeof(UnityEngine.UI.LayoutElement));
            var background = root.GetComponent<UnityEngine.UI.Image>();
            background.color = AlfaUiTheme.Night600;
            background.sprite = dependencies.ButtonSprite;
            background.type = dependencies.ButtonSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
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
}
