using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 night legibility for characters, visual only (URP rendering layers; the map, the moon and every other
    /// light are unaffected):
    /// - a shadowless directional "kicker" that only lights renderers tagged with the rim layer (the mosquito),
    ///   re-aimed for every camera so it always comes from behind the subject. Spread 0 keeps one top-back kicker;
    ///   spread 1 splits it into two side-back kickers that outline both silhouette edges from behind and from the side;
    /// - an optional camera-side fill that lights every character tagged with the fill layer (humans and mosquitoes),
    ///   so pajamas, shells and eyes keep their sketch colors against dark pines and navy skies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HiggsfieldRimLight : MonoBehaviour
    {
        /// <summary>URP rendering layer 7 ("Light Layer 7" in the tag manager) is reserved for character rim.</summary>
        public const int RenderingLayer = 7;
        public const uint RenderingLayerMask = 1u << RenderingLayer;
        /// <summary>URP rendering layer 5 is reserved for the camera-side character fill (every character).</summary>
        public const int FillRenderingLayer = 5;
        public const uint FillRenderingLayerMask = 1u << FillRenderingLayer;
        public const float MaximumIntensity = 3f;

        private Light rim, rimSecond, fill;
        private float spread;

        /// <summary>Adds the rim and fill layers to every renderer of a mosquito visual (keeps its other layers).</summary>
        public static void MarkCharacter(GameObject character) => Mark(character, RenderingLayerMask | FillRenderingLayerMask);

        /// <summary>Adds only the fill layer (humans: legible pajamas at night without the mosquito's red kicker).</summary>
        public static void MarkFillOnly(GameObject character) => Mark(character, FillRenderingLayerMask);

        private static void Mark(GameObject character, uint mask)
        {
            if (!character) return;
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask |= mask;
        }

        public Light Light => rim;
        public Light SecondRim => rimSecond;
        public Light Fill => fill;

        public void Configure(Color color, float intensity) => Configure(color, intensity, 0f, Color.white, 0f);

        public void Configure(Color rimColor, float rimIntensity, float rimSpread, Color fillColor, float fillIntensity)
        {
            spread = Mathf.Clamp01(rimSpread);
            rim = GetComponent<Light>();
            Setup(rim, rimColor, rimIntensity, RenderingLayerMask);
            rim.enabled = rimIntensity > 0;
            if (spread > 0)
            {
                rimSecond = Child("Higgsfield_CharacterRim_B");
                Setup(rimSecond, rimColor, rimIntensity, RenderingLayerMask);
                rimSecond.enabled = rimIntensity > 0;
            }
            if (fillIntensity > 0)
            {
                fill = Child("Higgsfield_CharacterFill");
                Setup(fill, fillColor, fillIntensity, FillRenderingLayerMask);
            }
        }

        private Light Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<Light>();
        }

        private static void Setup(Light light, Color color, float intensity, uint layers)
        {
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = Mathf.Clamp(intensity, 0f, MaximumIntensity);
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;
            light.cullingMask = ~0;
            light.GetUniversalAdditionalLightData().renderingLayers = layers;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Orient;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Orient;

        private void Orient(ScriptableRenderContext context, Camera camera)
        {
            if (!camera || !rim) return;
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = camera.transform.up;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (spread <= 0f)
            {
                // Source sits beyond the subject (away from the camera) and above it.
                Aim(rim.transform, forward * 0.6f + Vector3.up * 0.8f);
            }
            else
            {
                // Two kickers behind the subject, left and right: both silhouette edges catch a warm line.
                Vector3 back = forward * Mathf.Lerp(0.6f, 0.45f, spread);
                Vector3 side = right * Mathf.Lerp(0f, 0.85f, spread);
                Vector3 up = Vector3.up * Mathf.Lerp(0.8f, 0.35f, spread);
                Aim(rim.transform, back + side + up);
                if (rimSecond) Aim(rimSecond.transform, back - side + up);
            }
            // Fill from the camera side, a little right and above: gives shape without flattening the faces.
            if (fill) Aim(fill.transform, -forward * 0.75f + right * 0.4f + Vector3.up * 0.55f);
        }

        private static void Aim(Transform light, Vector3 toLight) =>
            light.rotation = Quaternion.LookRotation(-toLight.normalized, Vector3.up);
    }
}
