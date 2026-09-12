using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AlfaLightingRig : MonoBehaviour
    {
        private const string LightAnchorPrefix = "LightAnchor_";
        private const int PreviewLayer = 30;
        private const int MaximumShadowedLocalLights = 4;
        private const int MaximumShadowedLocalLightsPerZone = 2;

        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Light moon = null;
        [SerializeField] private Light lobbyFill = null;
        [SerializeField] private Volume globalVolume = null;
        [SerializeField] private Material nightSkybox = null;
        [SerializeField] private Light mapLightLowTemplate = null;
        [SerializeField] private Light mapLightMediumTemplate = null;

        private readonly List<Light> mapLights = new List<Light>();
        private Transform boundAnchors;

        public AlfaPresentationPreset Preset => preset;
        public Light Moon => moon;
        public Volume GlobalVolume => globalVolume;
        public Transform BoundAnchors => boundAnchors;
        public int MapLightCount => mapLights.Count;

        private void Awake()
        {
            ApplyPreset();
        }

        public void ApplyPreset()
        {
            if (preset == null || moon == null)
                return;
            moon.type = LightType.Directional;
            moon.color = preset.MoonColor;
            moon.intensity = preset.MoonIntensityLux;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.72f;
            moon.bounceIntensity = 0f;
            moon.cullingMask &= ~(1 << PreviewLayer);
#if UNITY_EDITOR
            moon.lightmapBakeType = LightmapBakeType.Realtime;
#endif
            RenderSettings.sun = moon;
            if (lobbyFill != null)
            {
                lobbyFill.type = LightType.Directional;
                lobbyFill.color = new Color(0.76f, 0.84f, 1f);
                lobbyFill.intensity = 0.90f;
                lobbyFill.shadows = LightShadows.None;
                lobbyFill.bounceIntensity = 0f;
                lobbyFill.cullingMask &= ~(1 << PreviewLayer);
#if UNITY_EDITOR
                lobbyFill.lightmapBakeType = LightmapBakeType.Realtime;
#endif
            }
            if (nightSkybox != null)
                RenderSettings.skybox = nightSkybox;
        }

        public void BindMap(Transform presentationAnchors, bool house)
        {
            if (presentationAnchors == null)
                throw new ArgumentNullException(nameof(presentationAnchors));
            if (mapLightLowTemplate == null || mapLightMediumTemplate == null)
                throw new InvalidOperationException("The map light templates are not assigned. Rebuild the presentation library.");

            ClearMapLights();
            boundAnchors = presentationAnchors;
            ApplyPreset();
            ApplyAmbientProfile(house);
            moon.shadowStrength = house ? 0.72f : 0.48f;
            if (lobbyFill != null)
                lobbyFill.enabled = !house;

            Transform[] anchors = presentationAnchors.GetComponentsInChildren<Transform>(true);
            var shadowCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int shadowCount = 0;
            int anchorCount = 0;

            for (int i = 0; i < anchors.Length; i++)
            {
                Transform anchor = anchors[i];
                if (!anchor.name.StartsWith(LightAnchorPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                anchorCount++;
                LocalLightProfile profile = ResolveProfile(anchor.name, house);
                string zone = ResolveZone(anchor.name);
                shadowCounts.TryGetValue(zone, out int zoneShadowCount);
                bool castsShadows = profile.ShadowCandidate
                    && shadowCount < MaximumShadowedLocalLights
                    && zoneShadowCount < MaximumShadowedLocalLightsPerZone;

                Light localLight = CreateMapLight(anchor, profile, castsShadows);
                mapLights.Add(localLight);
                if (castsShadows)
                {
                    shadowCount++;
                    shadowCounts[zone] = zoneShadowCount + 1;
                }
            }

            if (anchorCount == 0)
            {
                boundAnchors = null;
                throw new InvalidOperationException(
                    $"{presentationAnchors.name} does not contain an authored {LightAnchorPrefix} source.");
            }
        }

        private void OnDestroy()
        {
            ClearMapLights();
            if (RenderSettings.sun == moon)
                RenderSettings.sun = null;
        }

        private void ClearMapLights()
        {
            for (int i = 0; i < mapLights.Count; i++)
            {
                Light mapLight = mapLights[i];
                if (mapLight == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(mapLight.gameObject);
                else
                    DestroyImmediate(mapLight.gameObject);
            }

            mapLights.Clear();
            boundAnchors = null;
        }

        private Light CreateMapLight(
            Transform anchor, LocalLightProfile profile, bool castsShadows)
        {
            Light template = profile.ShadowTier == LocalShadowTier.Medium
                ? mapLightMediumTemplate
                : mapLightLowTemplate;
            Light localLight = Instantiate(template, anchor, false);
            GameObject lightObject = localLight.gameObject;
            lightObject.name = "LMS_LocalLight_" + ResolveZone(anchor.name);
            localLight.type = profile.Type;
            if (profile.Type == LightType.Spot)
            {
                localLight.spotAngle = 125f;
                localLight.innerSpotAngle = 80f;
            }
            localLight.color = profile.Color;
            localLight.intensity = profile.Intensity;
            localLight.range = profile.Range;
            localLight.bounceIntensity = 0f;
            localLight.renderMode = LightRenderMode.Auto;
#if UNITY_EDITOR
            localLight.lightmapBakeType = LightmapBakeType.Realtime;
#endif
            localLight.shadows = castsShadows ? LightShadows.Soft : LightShadows.None;
            localLight.shadowStrength = 0.68f;
            localLight.shadowBias = 0.075f;
            localLight.shadowNormalBias = 0.35f;
            localLight.shadowNearPlane = 0.1f;
            localLight.enabled = true;
            lightObject.SetActive(true);
            return localLight;
        }

        private static void ApplyAmbientProfile(bool house)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = house
                ? new Color(0.055f, 0.075f, 0.12f)
                : new Color(0.23f, 0.26f, 0.34f);
            RenderSettings.ambientEquatorColor = house
                ? new Color(0.032f, 0.043f, 0.068f)
                : new Color(0.15f, 0.15f, 0.18f);
            RenderSettings.ambientGroundColor = house
                ? new Color(0.014f, 0.017f, 0.027f)
                : new Color(0.075f, 0.07f, 0.085f);
            RenderSettings.ambientIntensity = house ? 0.82f : 1.08f;
            RenderSettings.reflectionIntensity = house ? 0.42f : 0.52f;
            RenderSettings.subtractiveShadowColor = new Color(0.018f, 0.025f, 0.045f);
        }

        private static LocalLightProfile ResolveProfile(string anchorName, bool house)
        {
            if (Contains(anchorName, "Patio"))
                return new LocalLightProfile(new Color(0.42f, 0.58f, 0.92f), 0.42f, 5.5f, false, LocalShadowTier.Low, LightType.Point);
            if (Contains(anchorName, "Lobby"))
                return new LocalLightProfile(new Color(1f, 0.62f, 0.30f), 0.65f, 4.0f, true, LocalShadowTier.Medium);
            if (Contains(anchorName, "Bedroom"))
                return new LocalLightProfile(new Color(1f, 0.58f, 0.32f), 0.90f, 3.7f, true, LocalShadowTier.Low);
            if (Contains(anchorName, "Living"))
                return new LocalLightProfile(new Color(1f, 0.64f, 0.36f), 1.10f, 4.5f, true, LocalShadowTier.Medium);
            if (Contains(anchorName, "Dining") || Contains(anchorName, "Kitchen"))
                return new LocalLightProfile(new Color(1f, 0.68f, 0.40f), 0.90f, 3.8f, false, LocalShadowTier.Low);
            if (Contains(anchorName, "Bathroom") || Contains(anchorName, "Utility"))
                return new LocalLightProfile(new Color(1f, 0.76f, 0.56f), 0.78f, 3.2f, false, LocalShadowTier.Low);
            if (Contains(anchorName, "Hall") || Contains(anchorName, "Landing"))
                return new LocalLightProfile(new Color(1f, 0.70f, 0.44f), 0.60f, 3.0f, false, LocalShadowTier.Low);

            return house
                ? new LocalLightProfile(new Color(1f, 0.68f, 0.40f), 0.85f, 3.8f, false, LocalShadowTier.Low)
                : new LocalLightProfile(new Color(1f, 0.64f, 0.34f), 1.45f, 4.8f, false, LocalShadowTier.Low);
        }

        private static string ResolveZone(string anchorName)
        {
            string zone = anchorName.StartsWith(LightAnchorPrefix, StringComparison.OrdinalIgnoreCase)
                ? anchorName.Substring(LightAnchorPrefix.Length)
                : anchorName;
            int variant = zone.IndexOf('_');
            return variant >= 0 ? zone.Substring(0, variant) : zone;
        }

        private static bool Contains(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct LocalLightProfile
        {
            public LocalLightProfile(
                Color color, float intensity, float range, bool shadowCandidate, LocalShadowTier shadowTier,
                LightType type = LightType.Spot)
            {
                Color = color;
                Intensity = intensity;
                Range = range;
                ShadowCandidate = shadowCandidate;
                ShadowTier = shadowTier;
                Type = type;
            }

            public Color Color { get; }
            public float Intensity { get; }
            public float Range { get; }
            public bool ShadowCandidate { get; }
            public LocalShadowTier ShadowTier { get; }
            public LightType Type { get; }
        }

        private enum LocalShadowTier
        {
            Low,
            Medium
        }
    }
}
