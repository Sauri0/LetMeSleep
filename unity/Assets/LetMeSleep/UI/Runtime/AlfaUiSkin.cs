using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Procedural v0.3 skin: one runtime atlas with rounded fills, 2-unit inner rings, soft shadows and
    /// circles at 2x density (200 px per unit against the 100 px reference). Everything shares a single
    /// texture so panels, buttons, frames and shadows batch together, and nothing needs an imported
    /// asset, .meta file or scene reference.
    /// </summary>
    internal static class AlfaUiSkin
    {
        internal const float PixelsPerUnit = 200f;
        private const int AtlasSize = 512;
        private const int Gutter = 2;
        private const int RingStrokePx = 4;     // 2 canvas units.
        private const int ShadowFeatherPx = 14; // 7 canvas units of soft falloff.
        internal static readonly int[] Radii = { 14, 12, 8, 6 };

        internal struct Region
        {
            public Rect Uv;
            public float BorderUnits;
            public float InsetUnits;
            public Vector4 UvBorder;
        }

        private static Texture2D atlas;
        private static readonly Dictionary<string, Region> regions = new Dictionary<string, Region>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        internal static Texture2D Atlas
        {
            get
            {
                Ensure();
                return atlas;
            }
        }

        internal static int RadiusKey(float radius)
        {
            var best = Radii[0];
            foreach (var candidate in Radii)
                if (Mathf.Abs(candidate - radius) < Mathf.Abs(best - radius)) best = candidate;
            return best;
        }

        internal static Sprite Fill(float radius) => Get("fill" + RadiusKey(radius));
        internal static Sprite Ring(float radius) => Get("ring" + RadiusKey(radius));
        internal static Sprite Circle() => Get("circle");
        internal static Sprite CircleRing() => Get("circle-ring");

        internal static bool TryRegion(string key, out Region region)
        {
            Ensure();
            return regions.TryGetValue(key, out region);
        }

        internal static bool OwnsTexture(Texture texture) => texture != null && texture == atlas;

        private static Sprite Get(string key)
        {
            Ensure();
            return sprites.TryGetValue(key, out var sprite) ? sprite : null;
        }

        private static void Ensure()
        {
            if (atlas != null) return;
            regions.Clear();
            sprites.Clear();
            atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false, true)
            {
                name = "LMS UI v0.3 skin atlas",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[AtlasSize * AtlasSize];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            var plan = new List<(string key, int size, int border, int inset, System.Func<float, float, int, float> alpha)>();
            foreach (var r in Radii)
            {
                var radiusPx = r * 2;
                var inset = ShadowFeatherPx + 1;
                var border = inset + radiusPx + 2;
                plan.Add(("shadow" + r, border * 2 + 4, border, inset, (x, y, s) => ShadowAlpha(x, y, s, inset, radiusPx)));
            }
            foreach (var r in Radii)
            {
                var radiusPx = r * 2;
                var border = radiusPx + 2;
                plan.Add(("fill" + r, border * 2 + 4, border, 0, (x, y, s) => Mathf.Clamp01(0.5f - RoundedBox(x, y, s, 0, radiusPx))));
                plan.Add(("ring" + r, border * 2 + 4, border, 0, (x, y, s) =>
                {
                    var d = RoundedBox(x, y, s, 0, radiusPx);
                    return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + RingStrokePx);
                }));
            }
            plan.Add(("circle", 68, 0, 0, (x, y, s) => Mathf.Clamp01(0.5f - (Vector2.Distance(new Vector2(x, y), new Vector2(s * .5f, s * .5f)) - (s * .5f - 2f)))));
            plan.Add(("circle-ring", 68, 0, 0, (x, y, s) =>
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(s * .5f, s * .5f)) - (s * .5f - 2f);
                return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + 9f);
            }));

            int cursorX = Gutter, cursorY = Gutter, rowHeight = 0;
            foreach (var item in plan)
            {
                if (cursorX + item.size + Gutter > AtlasSize)
                {
                    cursorX = Gutter;
                    cursorY += rowHeight + Gutter;
                    rowHeight = 0;
                }
                for (var y = 0; y < item.size; y++)
                for (var x = 0; x < item.size; x++)
                {
                    var a = item.alpha(x + 0.5f, y + 0.5f, item.size);
                    pixels[(cursorY + y) * AtlasSize + cursorX + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a)));
                }
                var pixelRect = new Rect(cursorX, cursorY, item.size, item.size);
                var region = new Region
                {
                    Uv = new Rect(pixelRect.x / AtlasSize, pixelRect.y / AtlasSize, pixelRect.width / AtlasSize, pixelRect.height / AtlasSize),
                    BorderUnits = item.border * 100f / PixelsPerUnit,
                    InsetUnits = item.inset * 100f / PixelsPerUnit,
                    UvBorder = new Vector4(item.border, item.border, item.border, item.border) / AtlasSize
                };
                regions[item.key] = region;
                var sprite = Sprite.Create(atlas, pixelRect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect,
                    new Vector4(item.border, item.border, item.border, item.border));
                sprite.name = "LMS UI " + item.key;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                sprites[item.key] = sprite;
                cursorX += item.size + Gutter;
                rowHeight = Mathf.Max(rowHeight, item.size);
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
        }

        private static float RoundedBox(float x, float y, int size, int inset, int radius)
        {
            var half = size * 0.5f - inset;
            var qx = Mathf.Abs(x - size * 0.5f) - (half - radius);
            var qy = Mathf.Abs(y - size * 0.5f) - (half - radius);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float ShadowAlpha(float x, float y, int size, int inset, int radius)
        {
            var d = RoundedBox(x, y, size, inset, radius);
            var t = Mathf.Clamp01((d + ShadowFeatherPx) / (2f * ShadowFeatherPx));
            var smooth = t * t * (3f - 2f * t);
            return 1f - smooth;
        }

        /// <summary>Appends a sliced quad set (triangle stream) for <paramref name="rect"/> using an atlas region.</summary>
        internal static void AppendSliced(List<UIVertex> stream, Rect rect, Region region, Color32 color)
        {
            var border = region.BorderUnits;
            var bx = Mathf.Min(border, rect.width * 0.5f);
            var by = Mathf.Min(border, rect.height * 0.5f);
            var xs = new[] { rect.xMin, rect.xMin + bx, rect.xMax - bx, rect.xMax };
            var ys = new[] { rect.yMin, rect.yMin + by, rect.yMax - by, rect.yMax };
            var uv = region.Uv;
            var us = new[] { uv.xMin, uv.xMin + region.UvBorder.x, uv.xMax - region.UvBorder.z, uv.xMax };
            var vs = new[] { uv.yMin, uv.yMin + region.UvBorder.y, uv.yMax - region.UvBorder.w, uv.yMax };
            for (var ix = 0; ix < 3; ix++)
            for (var iy = 0; iy < 3; iy++)
            {
                var a = Vertex(xs[ix], ys[iy], us[ix], vs[iy], color);
                var b = Vertex(xs[ix], ys[iy + 1], us[ix], vs[iy + 1], color);
                var c = Vertex(xs[ix + 1], ys[iy + 1], us[ix + 1], vs[iy + 1], color);
                var d = Vertex(xs[ix + 1], ys[iy], us[ix + 1], vs[iy], color);
                stream.Add(a); stream.Add(b); stream.Add(c);
                stream.Add(c); stream.Add(d); stream.Add(a);
            }
        }

        private static UIVertex Vertex(float x, float y, float u, float v, Color32 color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(x, y, 0f);
            vertex.uv0 = new Vector4(u, v, 0f, 0f);
            vertex.color = color;
            return vertex;
        }
    }

    /// <summary>
    /// Mesh effect for an Image that uses an <see cref="AlfaUiSkin"/> fill sprite: vertical gradient on the
    /// fill, a soft drop shadow drawn underneath and a 2-unit frame ring drawn on top, all in the same mesh
    /// and texture. Focus interpolates frame, glow and (optionally) the fill towards the focus colours.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AlfaUiSurface : BaseMeshEffect
    {
        private static readonly List<UIVertex> Source = new List<UIVertex>();
        private static readonly List<UIVertex> Output = new List<UIVertex>();

        internal int RadiusKey = 14;
        internal Color GradientTop = Color.white;
        internal Color GradientBottom = Color.white;
        internal Color FrameColor = Color.clear;
        internal Color ShadowColor = Color.clear;
        internal Vector2 ShadowOffset = new Vector2(0f, -AlfaUiTheme.ShadowOffset);
        internal Color FocusFrameColor = Color.clear;
        internal Color FocusShadowColor = Color.clear;
        internal bool FocusRecolorsFill;
        internal Color FocusGradientTop = Color.white;
        internal Color FocusGradientBottom = Color.white;
        private float focus;

        internal float Focus => focus;

        internal void SetFocus(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, focus)) return;
            focus = value;
            Refresh();
        }

        internal void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var image = graphic as Image;
            var skinned = image != null && image.sprite != null && AlfaUiSkin.OwnsTexture(image.sprite.texture);
            var rect = graphic.rectTransform.rect;
            Source.Clear();
            Output.Clear();
            vh.GetUIVertexStream(Source);

            var top = FocusRecolorsFill ? Color.Lerp(GradientTop, FocusGradientTop, focus) : GradientTop;
            var bottom = FocusRecolorsFill ? Color.Lerp(GradientBottom, FocusGradientBottom, focus) : GradientBottom;
            var height = Mathf.Max(0.001f, rect.height);
            for (var i = 0; i < Source.Count; i++)
            {
                var vertex = Source[i];
                var t = Mathf.Clamp01((vertex.position.y - rect.yMin) / height);
                vertex.color = (Color32)((Color)vertex.color * Color.Lerp(bottom, top, t));
                Source[i] = vertex;
            }

            if (skinned)
            {
                var shadow = Color.Lerp(ShadowColor, FocusShadowColor, focus);
                if (shadow.a > 0.003f && AlfaUiSkin.TryRegion("shadow" + RadiusKey, out var shadowRegion))
                {
                    var spread = shadowRegion.InsetUnits;
                    var focusLift = Vector2.Lerp(ShadowOffset, Vector2.zero, focus);
                    var shadowRect = new Rect(rect.xMin - spread + focusLift.x, rect.yMin - spread + focusLift.y,
                        rect.width + spread * 2f, rect.height + spread * 2f);
                    AlfaUiSkin.AppendSliced(Output, shadowRect, shadowRegion, shadow);
                }
            }
            Output.AddRange(Source);
            if (skinned)
            {
                var frame = Color.Lerp(FrameColor, FocusFrameColor, focus);
                if (frame.a > 0.003f && AlfaUiSkin.TryRegion("ring" + RadiusKey, out var ringRegion))
                    AlfaUiSkin.AppendSliced(Output, rect, ringRegion, frame);
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
            Source.Clear();
            Output.Clear();
        }
    }
}
