using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LetMeSleep.UI
{
    public enum AlfaUiIconKind
    {
        None,
        Play,
        Online,
        Training,
        Customize,
        Settings,
        Exit,
        Human,
        Mosquito,
        Clock,
        Blood,
        Ready,
        Copy,
        Explore,
        Back,
        Crosshair
    }

    [DisallowMultipleComponent]
    public sealed class AlfaUiIcon : MonoBehaviour
    {
        [SerializeField] private AlfaUiIconKind kind;
        [SerializeField] private Color tint = Color.white;
        private readonly List<Image> strokes = new List<Image>();
        private bool receivesRaycasts;

        public RectTransform rectTransform => (RectTransform)transform;

        public AlfaUiIconKind Kind
        {
            get => kind;
            set
            {
                if (kind == value && strokes.Count > 0) return;
                kind = value;
                Rebuild();
            }
        }

        public Color color
        {
            get => tint;
            set
            {
                tint = value;
                for (var i = 0; i < strokes.Count; i++) strokes[i].color = tint;
            }
        }

        public bool raycastTarget
        {
            get => receivesRaycasts;
            set
            {
                receivesRaycasts = value;
                for (var i = 0; i < strokes.Count; i++) strokes[i].raycastTarget = value;
            }
        }

        private void Rebuild()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            strokes.Clear();

            switch (kind)
            {
                case AlfaUiIconKind.Play:
                    Bar(new Vector2(-0.04f, 0.11f), new Vector2(0.14f, 0.56f), -38f);
                    Bar(new Vector2(-0.04f, -0.11f), new Vector2(0.14f, 0.56f), 38f);
                    break;
                case AlfaUiIconKind.Online:
                    Person(new Vector2(-0.14f, 0f), 0.72f);
                    Person(new Vector2(0.2f, -0.04f), 0.54f);
                    break;
                case AlfaUiIconKind.Training:
                    Crosshair(0.74f);
                    Outline(Vector2.zero, new Vector2(0.28f, 0.28f), 0.075f);
                    break;
                case AlfaUiIconKind.Customize:
                    Spark(Vector2.zero, 0.78f);
                    Bar(new Vector2(0.29f, 0.27f), new Vector2(0.09f, 0.24f));
                    Bar(new Vector2(0.29f, 0.27f), new Vector2(0.24f, 0.09f));
                    break;
                case AlfaUiIconKind.Settings:
                    Slider(0.2f, 0.24f);
                    Slider(-0.17f, 0f);
                    Slider(0.08f, -0.24f);
                    break;
                case AlfaUiIconKind.Exit:
                    Outline(new Vector2(-0.15f, 0f), new Vector2(0.42f, 0.7f), 0.075f);
                    Arrow(new Vector2(0.18f, 0f), false);
                    break;
                case AlfaUiIconKind.Human:
                    Person(Vector2.zero, 0.92f);
                    break;
                case AlfaUiIconKind.Mosquito:
                    Bar(Vector2.zero, new Vector2(0.11f, 0.76f), -34f);
                    Bar(new Vector2(-0.18f, 0.12f), new Vector2(0.12f, 0.5f), -58f);
                    Bar(new Vector2(0.18f, 0.11f), new Vector2(0.12f, 0.5f), 58f);
                    Bar(new Vector2(-0.16f, -0.12f), new Vector2(0.08f, 0.48f), 48f);
                    Bar(new Vector2(0.16f, -0.12f), new Vector2(0.08f, 0.48f), -48f);
                    break;
                case AlfaUiIconKind.Clock:
                    Outline(Vector2.zero, new Vector2(0.68f, 0.68f), 0.08f);
                    Bar(new Vector2(0f, 0.1f), new Vector2(0.075f, 0.28f));
                    Bar(new Vector2(0.1f, -0.02f), new Vector2(0.075f, 0.26f), -58f);
                    break;
                case AlfaUiIconKind.Blood:
                    Bar(new Vector2(0f, -0.08f), new Vector2(0.48f, 0.48f), 45f);
                    Bar(new Vector2(0f, 0.25f), new Vector2(0.13f, 0.3f));
                    break;
                case AlfaUiIconKind.Ready:
                    Bar(new Vector2(-0.17f, -0.1f), new Vector2(0.12f, 0.38f), 43f);
                    Bar(new Vector2(0.12f, 0.04f), new Vector2(0.12f, 0.68f), -42f);
                    break;
                case AlfaUiIconKind.Copy:
                    Outline(new Vector2(-0.1f, 0.1f), new Vector2(0.5f, 0.56f), 0.07f);
                    Outline(new Vector2(0.1f, -0.1f), new Vector2(0.5f, 0.56f), 0.07f);
                    break;
                case AlfaUiIconKind.Explore:
                    Diamond(Vector2.zero, 0.74f, 0.08f);
                    Bar(Vector2.zero, new Vector2(0.16f, 0.16f), 45f);
                    break;
                case AlfaUiIconKind.Back:
                    Arrow(Vector2.zero, true);
                    break;
                case AlfaUiIconKind.Crosshair:
                    Crosshair(0.86f);
                    break;
            }
        }

        private void Person(Vector2 center, float scale)
        {
            Bar(center + new Vector2(0f, 0.27f) * scale, new Vector2(0.22f, 0.22f) * scale, 45f);
            Bar(center + new Vector2(0f, -0.03f) * scale, new Vector2(0.12f, 0.36f) * scale);
            Bar(center, new Vector2(0.5f, 0.1f) * scale);
            Bar(center + new Vector2(-0.11f, -0.27f) * scale, new Vector2(0.1f, 0.34f) * scale, 34f);
            Bar(center + new Vector2(0.11f, -0.27f) * scale, new Vector2(0.1f, 0.34f) * scale, -34f);
        }

        private void Slider(float knobX, float y)
        {
            Bar(new Vector2(0f, y), new Vector2(0.72f, 0.075f));
            Bar(new Vector2(knobX, y), new Vector2(0.17f, 0.17f), 45f);
        }

        private void Spark(Vector2 center, float scale)
        {
            Bar(center, new Vector2(0.1f, 0.72f) * scale);
            Bar(center, new Vector2(0.72f, 0.1f) * scale);
            Bar(center, new Vector2(0.09f, 0.54f) * scale, 45f);
            Bar(center, new Vector2(0.09f, 0.54f) * scale, -45f);
        }

        private void Crosshair(float scale)
        {
            Bar(new Vector2(-0.29f, 0f) * scale, new Vector2(0.3f, 0.07f) * scale);
            Bar(new Vector2(0.29f, 0f) * scale, new Vector2(0.3f, 0.07f) * scale);
            Bar(new Vector2(0f, -0.29f) * scale, new Vector2(0.07f, 0.3f) * scale);
            Bar(new Vector2(0f, 0.29f) * scale, new Vector2(0.07f, 0.3f) * scale);
            Bar(Vector2.zero, new Vector2(0.12f, 0.12f) * scale, 45f);
        }

        private void Arrow(Vector2 center, bool left)
        {
            var direction = left ? -1f : 1f;
            Bar(center, new Vector2(0.68f, 0.09f));
            Bar(center + new Vector2(0.22f * direction, 0.12f), new Vector2(0.09f, 0.36f), -42f * direction);
            Bar(center + new Vector2(0.22f * direction, -0.12f), new Vector2(0.09f, 0.36f), 42f * direction);
        }

        private void Diamond(Vector2 center, float size, float width)
        {
            var half = size * 0.25f;
            var edge = size * 0.7f;
            Bar(center + new Vector2(-half, half), new Vector2(width, edge), -45f);
            Bar(center + new Vector2(half, half), new Vector2(width, edge), 45f);
            Bar(center + new Vector2(-half, -half), new Vector2(width, edge), 45f);
            Bar(center + new Vector2(half, -half), new Vector2(width, edge), -45f);
        }

        private void Outline(Vector2 center, Vector2 size, float width)
        {
            Bar(center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, width));
            Bar(center + new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, width));
            Bar(center + new Vector2(-size.x * 0.5f, 0f), new Vector2(width, size.y));
            Bar(center + new Vector2(size.x * 0.5f, 0f), new Vector2(width, size.y));
        }

        private void Bar(Vector2 center, Vector2 size, float angle = 0f)
        {
            var node = new GameObject("Stroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(transform, false);
            var image = node.GetComponent<Image>();
            image.color = tint;
            image.raycastTarget = receivesRaycasts;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f + center.x - size.x * 0.5f, 0.5f + center.y - size.y * 0.5f);
            rect.anchorMax = new Vector2(0.5f + center.x + size.x * 0.5f, 0.5f + center.y + size.y * 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            strokes.Add(image);
        }
    }
}
