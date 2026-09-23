using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 night legibility: a shadowless directional "kicker" that only lights renderers tagged with the
    /// character rim rendering layer (the mosquito), re-aimed for every camera so it always comes from behind
    /// and above the subject and outlines its top and far edges in warm red against dark skies and pines.
    /// Visual only; the map, the moon and every other light are unaffected (URP rendering layers).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HiggsfieldRimLight : MonoBehaviour
    {
        /// <summary>URP rendering layer 7 ("Light Layer 7" in the tag manager) is reserved for character rim.</summary>
        public const int RenderingLayer = 7;
        public const uint RenderingLayerMask = 1u << RenderingLayer;
        public const float MaximumIntensity = 3f;

        private Light rim;

        /// <summary>Adds the rim layer to every renderer of a character visual (keeps its other layers).</summary>
        public static void MarkCharacter(GameObject character)
        {
            if (!character) return;
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask |= RenderingLayerMask;
        }

        public Light Light => rim;

        public void Configure(Color color, float intensity)
        {
            rim = GetComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = color;
            rim.intensity = Mathf.Clamp(intensity, 0f, MaximumIntensity);
            rim.shadows = LightShadows.None;
            rim.bounceIntensity = 0f;
            rim.cullingMask = ~0;
            rim.GetUniversalAdditionalLightData().renderingLayers = RenderingLayerMask;
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
            // Source sits beyond the subject (away from the camera) and above it.
            Vector3 toLight = (forward.normalized * 0.6f + Vector3.up * 0.8f).normalized;
            transform.rotation = Quaternion.LookRotation(-toLight, Vector3.up);
        }
    }
}
