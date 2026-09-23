using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Framing and colouring of the painted character art (Resources/AlfaUiPortraits: full-body figures rendered on
    /// transparency). The PNGs are not CPU readable, so their pixels are read once through the GPU: a small copy
    /// (at most 256 wide) gives the opaque bounds and the face crop; a full-size copy is recoloured per player for the
    /// results (UI-06 9: every winner with their own trousers, skin or body colour).
    /// </summary>
    internal static class AlfaUiArt
    {
        private sealed class Analysis
        {
            public int Width, Height;
            public Color32[] Pixels;
            public Rect Opaque;
            public bool? FacesLeft;
            public readonly Dictionary<AlfaRole, Rect> Faces = new Dictionary<AlfaRole, Rect>();
        }

        private static readonly Dictionary<Texture, Analysis> Analyses = new Dictionary<Texture, Analysis>();

        // Colours the painted figures were rendered with (the game defaults): the recolour keeps each pixel's shading
        // relative to these, so a player with the default look gets the art unchanged.
        internal static readonly Color DefaultSkin = new Color(0.788f, 0.545f, 0.353f);     // #C98B5A
        internal static readonly Color DefaultPajama = new Color(0.176f, 0.31f, 0.604f);    // #2D4F9A
        internal static readonly Color DefaultMosquito = new Color(0.62f, 0.133f, 0.157f);  // #9E2228

        /// <summary>Drops the cached analysis of a texture that is about to be destroyed (recoloured copies).</summary>
        internal static void Forget(Texture texture)
        {
            if (texture != null) Analyses.Remove(texture);
        }

        /// <summary>UV rect (0-1, origin bottom-left) of the pixels above ~10 % alpha; the whole texture if unreadable.</summary>
        internal static Rect OpaqueBounds(Texture texture) => Analyze(texture)?.Opaque ?? new Rect(0f, 0f, 1f, 1f);

        /// <summary>
        /// Square crop (UV rect, square in pixels) of the character's face for a round badge (HUD objective): the
        /// human's head with the whole nightcap and its pompom; the mosquito's big eyes and the base of its
        /// proboscis. Falls back to the top of the figure when the art cannot be read.
        /// </summary>
        internal static Rect FaceRect(Texture texture, AlfaRole role)
        {
            var analysis = Analyze(texture);
            if (analysis == null) return new Rect(0f, 0f, 1f, 1f);
            if (analysis.Faces.TryGetValue(role, out var cached)) return cached;
            int w = analysis.Width, h = analysis.Height;
            var pixels = analysis.Pixels;
            var opaque = analysis.Opaque;
            float top = opaque.yMax * h, bottom = opaque.yMin * h, figure = Mathf.Max(1f, top - bottom);
            float cx, cy, side;
            var eyes = role == AlfaRole.Mosquito ? EyeBounds(pixels, w, h) : null;
            if (eyes.HasValue)
            {
                // The eye whites (opaque, near white; the translucent wings are excluded) centre the crop, a little
                // low so the head and the root of the proboscis show under them.
                var e = eyes.Value;
                cx = e.center.x;
                cy = e.center.y - e.width * 0.2f;
                side = e.width * 1.8f;
            }
            else
            {
                // Human: the top 26 % of the figure is the head with the cap; its opaque width takes the pompom in.
                var bandBottom = Mathf.FloorToInt(top - figure * 0.26f);
                int minX = w, maxX = -1;
                for (var y = Mathf.Max(0, bandBottom); y < Mathf.Min(h, Mathf.CeilToInt(top)); y++)
                for (var x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a <= 26) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
                if (maxX < minX) { minX = Mathf.FloorToInt(opaque.xMin * w); maxX = Mathf.CeilToInt(opaque.xMax * w); }
                // The badge's ring covers the outer eighth of the disc: the whole head stays inside it.
                cx = (minX + maxX + 1) * 0.5f;
                side = Mathf.Max(maxX + 1 - minX, figure * 0.26f) * 1.42f;
                cy = top - figure * 0.15f;
            }
            var rect = new Rect((cx - side * 0.5f) / w, (cy - side * 0.5f) / h, side / w, side / h);
            analysis.Faces[role] = rect;
            return rect;
        }

        /// <summary>
        /// True when a side-on figure looks to the left (its eye whites left of its centre), e.g. the painted mosquito,
        /// whose big wing is then on its right. True when unknown.
        /// </summary>
        internal static bool FacesLeft(Texture texture)
        {
            var analysis = Analyze(texture);
            if (analysis == null) return true;
            if (!analysis.FacesLeft.HasValue)
            {
                var eyes = EyeBounds(analysis.Pixels, analysis.Width, analysis.Height);
                analysis.FacesLeft = !eyes.HasValue || eyes.Value.center.x <= analysis.Opaque.center.x * analysis.Width;
            }
            return analysis.FacesLeft.Value;
        }

        /// <summary>Bounds (pixels of the analysis copy) of the opaque near-white pixels: the mosquito's eye whites.</summary>
        private static Rect? EyeBounds(Color32[] pixels, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1, count = 0;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = pixels[y * w + x];
                // Linear copy: 170 is about sRGB 215.
                if (p.a < 250 || p.r < 170 || p.g < 170 || p.b < 170) continue;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            if (count < 12 || maxX - minX < 4) return null;
            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static Analysis Analyze(Texture texture)
        {
            if (texture == null) return null;
            if (Analyses.TryGetValue(texture, out var cached)) return cached;
            Analysis result = null;
            try
            {
                var width = Mathf.Clamp(texture.width, 1, 256);
                var height = Mathf.Clamp(Mathf.RoundToInt(width * (float)texture.height / Mathf.Max(1, texture.width)), 1, 256);
                var pixels = ReadBack(texture, width, height, false);
                if (pixels != null)
                {
                    result = new Analysis { Width = width, Height = height, Pixels = pixels, Opaque = new Rect(0f, 0f, 1f, 1f) };
                    int minX = width, minY = height, maxX = -1, maxY = -1;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        if (pixels[y * width + x].a <= 26) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                    if (maxX >= minX && maxY >= minY)
                        result.Opaque = Rect.MinMaxRect(minX / (float)width, minY / (float)height, (maxX + 1) / (float)width, (maxY + 1) / (float)height);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_ART_BOUNDS_UNAVAILABLE: " + exception.Message);
            }
            Analyses[texture] = result;
            return result;
        }

        /// <summary>Pixels of a texture copied through the GPU at the given size (sRGB-encoded or linear values).</summary>
        private static Color32[] ReadBack(Texture texture, int width, int height, bool srgb)
        {
            RenderTexture target = null;
            Texture2D copy = null;
            var previous = RenderTexture.active;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                    srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                copy.Apply(false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (copy != null)
                {
                    if (Application.isPlaying) Object.Destroy(copy);
                    else Object.DestroyImmediate(copy);
                }
            }
        }

        /// <summary>
        /// A copy of a painted figure with one player's colours (the caller owns the texture): human trousers (the
        /// pajama blue, hue 212-231; the slippers, 231-250, and the cream polka dots keep theirs) and skin (hue
        /// 12-40); mosquito body (the red shell and its dark segments). Hue follows the player's colour; saturation
        /// and value keep each pixel's shading relative to the default the art was rendered with. Null when nothing
        /// changes or the art cannot be read.
        /// </summary>
        internal static Texture2D Recolor(Texture texture, AlfaRole role, Color? skin, Color? pajama, Color? body)
        {
            if (texture == null) return null;
            var human = role == AlfaRole.Human;
            bool Differs(Color? target, Color reference) => target.HasValue &&
                (Mathf.Abs(target.Value.r - reference.r) + Mathf.Abs(target.Value.g - reference.g) + Mathf.Abs(target.Value.b - reference.b)) > 0.02f;
            var tintSkin = human && Differs(skin, DefaultSkin);
            var tintPajama = human && Differs(pajama, DefaultPajama);
            var tintBody = !human && Differs(body, DefaultMosquito);
            if (!tintSkin && !tintPajama && !tintBody) return null;
            try
            {
                var width = Mathf.Min(texture.width, 1024);
                var height = Mathf.Max(1, Mathf.RoundToInt(width * (float)texture.height / Mathf.Max(1, texture.width)));
                var pixels = ReadBack(texture, width, height, true);
                if (pixels == null) return null;
                var skinMap = tintSkin ? new HueMap(DefaultSkin, skin.Value) : null;
                var pajamaMap = tintPajama ? new HueMap(DefaultPajama, pajama.Value) : null;
                var bodyMap = tintBody ? new HueMap(DefaultMosquito, body.Value) : null;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    if (p.a == 0) continue;
                    Color.RGBToHSV(new Color32(p.r, p.g, p.b, 255), out var hue, out var saturation, out var value);
                    var degrees = hue * 360f;
                    HueMap map = null;
                    if (human)
                    {
                        if (pajamaMap != null && degrees >= 212f && degrees < 231f && saturation > 0.3f && value > 0.12f) map = pajamaMap;
                        else if (skinMap != null && degrees >= 12f && degrees <= 40f && saturation > 0.33f && value > 0.18f) map = skinMap;
                    }
                    else if (bodyMap != null && (degrees >= 335f || degrees < 14f) && saturation > 0.45f && value > 0.12f) map = bodyMap;
                    if (map == null) continue;
                    var colour = map.Apply(degrees, saturation, value);
                    pixels[i] = new Color32((byte)Mathf.RoundToInt(colour.r * 255f), (byte)Mathf.RoundToInt(colour.g * 255f), (byte)Mathf.RoundToInt(colour.b * 255f), p.a);
                }
                var result = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    name = texture.name + " (player colours)",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear
                };
                result.SetPixels32(pixels);
                result.Apply(true, true);
                return result;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_ART_RECOLOR_FAILED: " + exception.Message);
                return null;
            }
        }

        /// <summary>Moves a pixel from the default colour's hue to the target's, scaling saturation and value by their ratio.</summary>
        private sealed class HueMap
        {
            private readonly float fromHue, toHue, saturationScale, valueScale;

            public HueMap(Color from, Color to)
            {
                Color.RGBToHSV(from, out var fh, out var fs, out var fv);
                Color.RGBToHSV(to, out var th, out var ts, out var tv);
                fromHue = fh * 360f;
                toHue = th * 360f;
                saturationScale = ts / Mathf.Max(0.01f, fs);
                valueScale = tv / Mathf.Max(0.01f, fv);
            }

            public Color Apply(float hueDegrees, float saturation, float value)
            {
                var offset = Mathf.DeltaAngle(fromHue, hueDegrees);
                var hue = Mathf.Repeat(toHue + offset, 360f) / 360f;
                return Color.HSVToRGB(hue, Mathf.Clamp01(saturation * saturationScale), Mathf.Clamp01(value * valueScale));
            }
        }

        /// <summary>
        /// Rect (centre-anchored, in the frame's units) for the whole texture so that its figure is framed inside a
        /// <paramref name="frame"/>: <paramref name="fromTop"/> = 1 shows the whole figure ("contain", anchored at the
        /// top), less than 1 shows that fraction of the figure from the top of the head down (a bust), never cutting
        /// the head. <paramref name="pad"/> is the margin above the head as a fraction of the figure height.
        /// </summary>
        internal static Rect Place(Texture texture, Vector2 frame, float fromTop, float pad)
        {
            if (texture == null || frame.x <= 1f || frame.y <= 1f) return new Rect(-frame.x * 0.5f, -frame.y * 0.5f, frame.x, frame.y);
            var bounds = OpaqueBounds(texture);
            float w = texture.width, h = texture.height;
            var figureTop = bounds.yMax * h;
            var figureHeight = Mathf.Max(1f, bounds.height * h);
            var figureWidth = Mathf.Max(1f, bounds.width * w);
            var whole = fromTop >= 0.999f;
            var shownHeight = figureHeight * Mathf.Clamp(fromTop, 0.2f, 1f) + figureHeight * pad * (whole ? 2f : 1f);
            var scale = frame.y / shownHeight;
            // The figure (plus a side margin) must also fit the frame's width.
            scale = Mathf.Min(scale, frame.x / (figureWidth * (1f + pad * 2f)));
            var size = new Vector2(w * scale, h * scale);
            // Top of the head (plus the margin) at the top of the frame; figure centred horizontally.
            var imageTop = frame.y * 0.5f + (h - figureTop - figureHeight * pad) * scale;
            if (whole)
            {
                // Contain: when the width limited the scale, keep the whole figure vertically centred instead.
                var used = (figureHeight * (1f + pad * 2f)) * scale;
                if (used < frame.y - 1f) imageTop -= (frame.y - used) * 0.5f;
            }
            var centreX = -(bounds.center.x - 0.5f) * w * scale;
            return new Rect(centreX - size.x * 0.5f, imageTop - size.y, size.x, size.y);
        }
    }

    /// <summary>Keeps a RawImage showing its texture "cover" (cropped to the rect's aspect, centred), e.g. map pictures.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    internal sealed class AlfaUiCover : MonoBehaviour
    {
        private void OnRectTransformDimensionsChange() => Apply();
        private void OnEnable() => Apply();

        internal void Apply()
        {
            var image = GetComponent<UnityEngine.UI.RawImage>();
            var texture = image != null ? image.texture : null;
            var rect = ((RectTransform)transform).rect;
            if (texture == null || texture.height <= 0 || rect.width <= 1f || rect.height <= 1f)
            {
                if (image != null) image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            var textureAspect = (float)texture.width / texture.height;
            var rectAspect = rect.width / rect.height;
            image.uvRect = textureAspect > rectAspect
                ? new Rect((1f - rectAspect / textureAspect) * 0.5f, 0f, rectAspect / textureAspect, 1f)
                : new Rect(0f, (1f - textureAspect / rectAspect) * 0.5f, 1f, textureAspect / rectAspect);
        }
    }

    /// <summary>
    /// Keeps a RawImage child framed on the figure of its painted texture (see <see cref="AlfaUiArt.Place"/>)
    /// whenever the frame is resized. Lives on the masking frame; the image is its child "Portrait".
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AlfaUiArtFit : MonoBehaviour
    {
        internal UnityEngine.UI.RawImage Target;
        internal float FromTop = 1f;
        internal float Pad = 0.06f;

        private void OnRectTransformDimensionsChange() => Apply();
        private void OnEnable() => Apply();

        internal void Apply()
        {
            if (Target == null || Target.texture == null) return;
            var frame = ((RectTransform)transform).rect.size;
            if (frame.x <= 1f || frame.y <= 1f) return;
            var rect = AlfaUiArt.Place(Target.texture, frame, FromTop, Pad);
            var target = Target.rectTransform;
            target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.sizeDelta = rect.size;
            target.anchoredPosition = rect.center;
            Target.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }
}
