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
    public sealed class AlfaUiIcon : MaskableGraphic
    {
        [SerializeField] private AlfaUiIconKind kind;

        public AlfaUiIconKind Kind
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var scale = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            switch (kind)
            {
                case AlfaUiIconKind.Play:
                    Triangle(vh, center + new Vector2(-scale * 0.13f, 0f), scale * 0.58f);
                    break;
                case AlfaUiIconKind.Online:
                    Person(vh, center + new Vector2(-scale * 0.16f, 0f), scale * 0.72f);
                    Person(vh, center + new Vector2(scale * 0.18f, scale * 0.02f), scale * 0.58f);
                    break;
                case AlfaUiIconKind.Training:
                    Ring(vh, center, scale * 0.38f, scale * 0.065f, 24);
                    Ring(vh, center, scale * 0.18f, scale * 0.055f, 18);
                    Quad(vh, center, new Vector2(scale * 0.56f, scale * 0.055f));
                    Quad(vh, center, new Vector2(scale * 0.055f, scale * 0.56f));
                    break;
                case AlfaUiIconKind.Customize:
                    Ring(vh, center, scale * 0.33f, scale * 0.11f, 24);
                    Circle(vh, center + new Vector2(-scale * 0.18f, scale * 0.12f), scale * 0.055f, 10);
                    Circle(vh, center + new Vector2(scale * 0.05f, scale * 0.2f), scale * 0.055f, 10);
                    Circle(vh, center + new Vector2(scale * 0.2f, scale * 0.02f), scale * 0.055f, 10);
                    break;
                case AlfaUiIconKind.Settings:
                    Slider(vh, center, scale, 0.2f, -0.22f);
                    Slider(vh, center, scale, -0.12f, 0f);
                    Slider(vh, center, scale, 0.1f, 0.22f);
                    break;
                case AlfaUiIconKind.Exit:
                    RectOutline(vh, center + new Vector2(-scale * 0.13f, 0f), new Vector2(scale * 0.42f, scale * 0.66f), scale * 0.065f);
                    Line(vh, center + new Vector2(-scale * 0.02f, 0f), center + new Vector2(scale * 0.37f, 0f), scale * 0.075f);
                    Line(vh, center + new Vector2(scale * 0.2f, scale * 0.16f), center + new Vector2(scale * 0.37f, 0f), scale * 0.075f);
                    Line(vh, center + new Vector2(scale * 0.2f, -scale * 0.16f), center + new Vector2(scale * 0.37f, 0f), scale * 0.075f);
                    break;
                case AlfaUiIconKind.Human:
                    Person(vh, center, scale * 0.92f);
                    break;
                case AlfaUiIconKind.Mosquito:
                    Mosquito(vh, center, scale);
                    break;
                case AlfaUiIconKind.Clock:
                    Ring(vh, center, scale * 0.36f, scale * 0.075f, 28);
                    Line(vh, center, center + new Vector2(0f, scale * 0.21f), scale * 0.07f);
                    Line(vh, center, center + new Vector2(scale * 0.17f, -scale * 0.1f), scale * 0.07f);
                    break;
                case AlfaUiIconKind.Blood:
                    Drop(vh, center, scale);
                    break;
                case AlfaUiIconKind.Ready:
                    Line(vh, center + new Vector2(-scale * 0.3f, 0f), center + new Vector2(-scale * 0.08f, -scale * 0.22f), scale * 0.105f);
                    Line(vh, center + new Vector2(-scale * 0.08f, -scale * 0.22f), center + new Vector2(scale * 0.32f, scale * 0.25f), scale * 0.105f);
                    break;
                case AlfaUiIconKind.Copy:
                    RectOutline(vh, center + new Vector2(-scale * 0.09f, scale * 0.09f), new Vector2(scale * 0.48f, scale * 0.56f), scale * 0.06f);
                    RectOutline(vh, center + new Vector2(scale * 0.1f, -scale * 0.09f), new Vector2(scale * 0.48f, scale * 0.56f), scale * 0.06f);
                    break;
                case AlfaUiIconKind.Explore:
                    Line(vh, center + new Vector2(-scale * 0.38f, 0f), center, scale * 0.065f);
                    Line(vh, center, center + new Vector2(scale * 0.38f, 0f), scale * 0.065f);
                    Line(vh, center + new Vector2(-scale * 0.38f, 0f), center + new Vector2(0f, scale * 0.24f), scale * 0.065f);
                    Line(vh, center + new Vector2(scale * 0.38f, 0f), center + new Vector2(0f, scale * 0.24f), scale * 0.065f);
                    Line(vh, center + new Vector2(-scale * 0.38f, 0f), center + new Vector2(0f, -scale * 0.24f), scale * 0.065f);
                    Line(vh, center + new Vector2(scale * 0.38f, 0f), center + new Vector2(0f, -scale * 0.24f), scale * 0.065f);
                    Circle(vh, center, scale * 0.1f, 14);
                    break;
                case AlfaUiIconKind.Back:
                    Line(vh, center + new Vector2(-scale * 0.3f, 0f), center + new Vector2(scale * 0.32f, 0f), scale * 0.08f);
                    Line(vh, center + new Vector2(-scale * 0.3f, 0f), center + new Vector2(-scale * 0.04f, scale * 0.24f), scale * 0.08f);
                    Line(vh, center + new Vector2(-scale * 0.3f, 0f), center + new Vector2(-scale * 0.04f, -scale * 0.24f), scale * 0.08f);
                    break;
                case AlfaUiIconKind.Crosshair:
                    Ring(vh, center, scale * 0.16f, scale * 0.045f, 18);
                    Line(vh, center + new Vector2(-scale * 0.43f, 0f), center + new Vector2(-scale * 0.22f, 0f), scale * 0.045f);
                    Line(vh, center + new Vector2(scale * 0.22f, 0f), center + new Vector2(scale * 0.43f, 0f), scale * 0.045f);
                    Line(vh, center + new Vector2(0f, -scale * 0.43f), center + new Vector2(0f, -scale * 0.22f), scale * 0.045f);
                    Line(vh, center + new Vector2(0f, scale * 0.22f), center + new Vector2(0f, scale * 0.43f), scale * 0.045f);
                    break;
            }
        }

        private void Slider(VertexHelper vh, Vector2 center, float scale, float knobX, float y)
        {
            var a = center + new Vector2(-scale * 0.35f, scale * y);
            var b = center + new Vector2(scale * 0.35f, scale * y);
            Line(vh, a, b, scale * 0.065f);
            Circle(vh, center + new Vector2(scale * knobX, scale * y), scale * 0.105f, 12);
        }

        private void Person(VertexHelper vh, Vector2 center, float scale)
        {
            Circle(vh, center + new Vector2(0f, scale * 0.25f), scale * 0.13f, 16);
            Line(vh, center + new Vector2(0f, scale * 0.1f), center + new Vector2(0f, -scale * 0.18f), scale * 0.1f);
            Line(vh, center + new Vector2(-scale * 0.24f, 0f), center + new Vector2(scale * 0.24f, 0f), scale * 0.08f);
            Line(vh, center + new Vector2(0f, -scale * 0.16f), center + new Vector2(-scale * 0.2f, -scale * 0.36f), scale * 0.08f);
            Line(vh, center + new Vector2(0f, -scale * 0.16f), center + new Vector2(scale * 0.2f, -scale * 0.36f), scale * 0.08f);
        }

        private void Mosquito(VertexHelper vh, Vector2 center, float scale)
        {
            Line(vh, center + new Vector2(-scale * 0.08f, scale * 0.18f), center + new Vector2(scale * 0.16f, -scale * 0.24f), scale * 0.1f);
            Circle(vh, center + new Vector2(-scale * 0.13f, scale * 0.25f), scale * 0.12f, 14);
            Line(vh, center + new Vector2(-scale * 0.05f, scale * 0.12f), center + new Vector2(-scale * 0.34f, -scale * 0.25f), scale * 0.055f);
            Line(vh, center + new Vector2(scale * 0.06f, 0f), center + new Vector2(scale * 0.36f, -scale * 0.26f), scale * 0.055f);
            Triangle(vh, center + new Vector2(-scale * 0.28f, scale * 0.12f), scale * 0.32f);
            Triangle(vh, center + new Vector2(scale * 0.15f, scale * 0.2f), scale * 0.32f);
            Line(vh, center + new Vector2(-scale * 0.2f, scale * 0.31f), center + new Vector2(-scale * 0.38f, scale * 0.42f), scale * 0.035f);
            Line(vh, center + new Vector2(-scale * 0.08f, scale * 0.34f), center + new Vector2(scale * 0.02f, scale * 0.48f), scale * 0.035f);
        }

        private void Drop(VertexHelper vh, Vector2 center, float scale)
        {
            var tip = center + new Vector2(0f, scale * 0.38f);
            var left = center + new Vector2(-scale * 0.28f, -scale * 0.05f);
            var bottom = center + new Vector2(0f, -scale * 0.34f);
            var right = center + new Vector2(scale * 0.28f, -scale * 0.05f);
            Triangle(vh, tip, left, right);
            Triangle(vh, left, bottom, right);
        }

        private void Triangle(VertexHelper vh, Vector2 center, float size)
        {
            Triangle(vh, center + new Vector2(-size * 0.35f, size * 0.5f), center + new Vector2(size * 0.48f, 0f), center + new Vector2(-size * 0.35f, -size * 0.5f));
        }

        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            var index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }

        private void Quad(VertexHelper vh, Vector2 center, Vector2 size)
        {
            var half = size * 0.5f;
            var index = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-half.x, -half.y), color, Vector2.zero);
            vh.AddVert(center + new Vector2(-half.x, half.y), color, Vector2.zero);
            vh.AddVert(center + new Vector2(half.x, half.y), color, Vector2.zero);
            vh.AddVert(center + new Vector2(half.x, -half.y), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            var index = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private void Circle(VertexHelper vh, Vector2 center, float radius, int segments)
        {
            var centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
            }
        }

        private void Ring(VertexHelper vh, Vector2 center, float radius, float width, int segments)
        {
            var inner = Mathf.Max(0f, radius - width);
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var index = vh.currentVertCount;
                vh.AddVert(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * inner, color, Vector2.zero);
                vh.AddVert(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, color, Vector2.zero);
                vh.AddVert(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, color, Vector2.zero);
                vh.AddVert(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * inner, color, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
        }

        private void RectOutline(VertexHelper vh, Vector2 center, Vector2 size, float width)
        {
            var half = size * 0.5f;
            Line(vh, center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y), width);
            Line(vh, center + new Vector2(half.x, -half.y), center + new Vector2(half.x, half.y), width);
            Line(vh, center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y), width);
            Line(vh, center + new Vector2(-half.x, half.y), center + new Vector2(-half.x, -half.y), width);
        }
    }
}
