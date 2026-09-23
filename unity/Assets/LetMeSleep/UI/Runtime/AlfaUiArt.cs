using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Framing of the painted character art (Resources/AlfaUiPortraits: full-body figures on transparency). The
    /// opaque bounds of each texture are read once through the GPU (the PNGs are not CPU readable), so the training
    /// cards, the customization summary and the results can frame the figure itself instead of the canvas around it.
    /// </summary>
    internal static class AlfaUiArt
    {
        private static readonly Dictionary<Texture, Rect> Bounds = new Dictionary<Texture, Rect>();

        /// <summary>UV rect (0-1, origin bottom-left) of the pixels above ~10 % alpha; the whole texture if unreadable.</summary>
        internal static Rect OpaqueBounds(Texture texture)
        {
            if (texture == null) return new Rect(0f, 0f, 1f, 1f);
            if (Bounds.TryGetValue(texture, out var cached)) return cached;
            var result = new Rect(0f, 0f, 1f, 1f);
            RenderTexture target = null;
            Texture2D copy = null;
            var previous = RenderTexture.active;
            try
            {
                var width = Mathf.Clamp(texture.width, 1, 256);
                var height = Mathf.Clamp(Mathf.RoundToInt(width * (float)texture.height / Mathf.Max(1, texture.width)), 1, 256);
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                copy.Apply(false);
                var pixels = copy.GetPixels32();
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
                    result = Rect.MinMaxRect(minX / (float)width, minY / (float)height, (maxX + 1) / (float)width, (maxY + 1) / (float)height);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_ART_BOUNDS_UNAVAILABLE: " + exception.Message);
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
            Bounds[texture] = result;
            return result;
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
