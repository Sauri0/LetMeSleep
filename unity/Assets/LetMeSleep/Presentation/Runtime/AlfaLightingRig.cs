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
        // v0.3.0 menu/lobby ("sala") mood, UI-06 screens 1 and 3: navy night room, cool moon fill and
        // warm lamp pools that the AlfaGlobalVolume bloom turns into halos.
        private const float LobbyMoonScale = 0.3f;
        private static readonly Color LobbyFillColor = new Color(0.5f, 0.62f, 1f);
        private const float LobbyFillIntensity = 0.22f;
        // v0.3.0 art direction (UI-06 #1/#3): warm #FFB347 halo of about 1.5 m around each wall lantern, a night
        // window with moon instead of the framed print, and warm string lights with fill pools over the front
        // half of the room so the lobby's back views are no longer flat navy.
        private static readonly Color LobbyHaloColor = new Color(1f, 0.702f, 0.278f, 0.35f);
        private const float LobbyHaloSize = 1.5f;
        private const string LobbyWindowCanvasPath = "Furnishings/Lobby_Domestic/Menu_FramedNightLake/Canvas";
        private const string LobbyWindowPrintPath = "Furnishings/Lobby_Domestic/Menu_FramedNightLake/NightLake_Print";
        private static readonly Color LobbyStringFillColor = new Color(1f, 0.66f, 0.36f);
        public const string BulbHaloName = "Higgsfield_BulbHalo";
        private const float LobbyBulbHaloSize = 0.25f;
        public static readonly Color LobbyAmbientSky = new Color(0.48f, 0.50f, 0.62f);
        public static readonly Color LobbyAmbientEquator = new Color(0.60f, 0.61f, 0.72f);
        public static readonly Color LobbyAmbientGround = new Color(0.42f, 0.40f, 0.44f);
        private static readonly Color LobbyBulbHaloColor = new Color(1f, 0.78f, 0.45f, 0.3f);

        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Light moon = null;
        [SerializeField] private Light lobbyFill = null;
        [SerializeField] private Volume globalVolume = null;
        [SerializeField] private Material nightSkybox = null;
        [SerializeField] private Light mapLightLowTemplate = null;
        [SerializeField] private Light mapLightMediumTemplate = null;
        [SerializeField] private HiggsfieldAtmosphereKit atmosphereKit = null;

        private readonly List<Light> mapLights = new List<Light>();
        private readonly List<GameObject> lobbyAtmosphere = new List<GameObject>();
        private readonly List<KeyValuePair<Renderer, Material[]>> lobbyMaterials = new List<KeyValuePair<Renderer, Material[]>>();
        private readonly List<Renderer> lobbyHidden = new List<Renderer>();
        private Transform boundAnchors;
        private HiggsfieldMapLighting higgsfieldLighting;

        public bool IsHiggsfieldBound => higgsfieldLighting && higgsfieldLighting.IsBound;

        public void BindHiggsfield(Transform mapRoot, HiggsfieldMapLighting.Configuration configuration)
        {
            HiggsfieldMapLighting.Validate(mapRoot, moon, configuration);
            if (!higgsfieldLighting) higgsfieldLighting = gameObject.AddComponent<HiggsfieldMapLighting>();
            var previousLights = new List<Light>(mapLights);
            if (lobbyFill) previousLights.Add(lobbyFill);
            higgsfieldLighting.Bind(mapRoot, moon, globalVolume, configuration, previousLights,
                mapLightLowTemplate, mapLightMediumTemplate);
        }

        public void UnbindHiggsfield()
        {
            if (higgsfieldLighting) higgsfieldLighting.Unbind();
        }

        public AlfaPresentationPreset Preset => preset;
        public Light Moon => moon;
        public Volume GlobalVolume => globalVolume;
        public Transform BoundAnchors => boundAnchors;
        public int MapLightCount => mapLights.Count;
        public HiggsfieldAtmosphereKit AtmosphereKit => atmosphereKit;
        public int LobbyAtmosphereCount => lobbyAtmosphere.Count;

        private void Awake()
        {
            ApplyPreset();
        }

        public void ApplyPreset()
        {
            if (IsHiggsfieldBound) return;
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
                lobbyFill.color = LobbyFillColor;
                lobbyFill.intensity = LobbyFillIntensity;
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

            UnbindHiggsfield();
            ClearMapLights();
            boundAnchors = presentationAnchors;
            ApplyPreset();
            ApplyAmbientProfile(house);
            moon.shadowStrength = house ? 0.72f : 0.48f;
            if (!house)
                moon.intensity = preset.MoonIntensityLux * LobbyMoonScale;
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

            if (!house)
            {
                AddLobbyCameraFill(presentationAnchors);
                AddLobbyAtmosphere(presentationAnchors);
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
            UnbindHiggsfield();
            ClearMapLights();
            if (RenderSettings.sun == moon)
                RenderSettings.sun = null;
        }

        private void OnDisable() => UnbindHiggsfield();

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
            foreach (var item in lobbyAtmosphere)
                if (item) { item.SetActive(false); if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            lobbyAtmosphere.Clear();
            foreach (var item in lobbyMaterials) if (item.Key) item.Key.sharedMaterials = item.Value;
            lobbyMaterials.Clear();
            foreach (var item in lobbyHidden) if (item) item.enabled = true;
            lobbyHidden.Clear();
            boundAnchors = null;
        }

        private void AddLobbyAtmosphere(Transform presentationAnchors)
        {
            if (atmosphereKit == null || !atmosphereKit.IsComplete)
                return; // Older rigs keep the v0.2.0 lobby; the atmosphere kit is optional here.
            Transform lobby = presentationAnchors.parent != null ? presentationAnchors.parent : presentationAnchors;
            foreach (Transform anchor in presentationAnchors.GetComponentsInChildren<Transform>(true))
            {
                if (!anchor.name.StartsWith(LightAnchorPrefix + "Lobby_Lantern", StringComparison.OrdinalIgnoreCase))
                    continue;
                // Anchors sit 0.19 m in front of the lantern bodies on the back wall.
                lobbyAtmosphere.Add(HiggsfieldAtmosphereVisuals.CreateHalo(anchor, anchor.rotation * new Vector3(0f, 0f, 0.17f), LobbyHaloSize,
                    LobbyHaloColor, 1f, atmosphereKit.HaloMaterial));
            }

            Renderer canvas = FindRenderer(lobby, LobbyWindowCanvasPath);
            if (canvas != null)
            {
                lobbyMaterials.Add(new KeyValuePair<Renderer, Material[]>(canvas, canvas.sharedMaterials));
                var materials = canvas.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = atmosphereKit.MenuWindowMaterial;
                canvas.sharedMaterials = materials;
                Renderer print = FindRenderer(lobby, LobbyWindowPrintPath);
                if (print != null && print.enabled) { print.enabled = false; lobbyHidden.Add(print); }
            }

            // Front half of the room (behind the menu camera): garlands under the ceiling plus warm pools.
            var garlands = new GameObject("Higgsfield_LobbyGarlands");
            garlands.transform.SetParent(lobby, false);
            lobbyAtmosphere.Add(garlands);
            var strands = new[]
            {
                (new Vector3(-6.6f, 2.95f, -5.75f), new Vector3(6.6f, 2.95f, -5.75f), 0.34f, 20),
                (new Vector3(-6.6f, 2.95f, -5.75f), new Vector3(0f, 3.05f, -0.6f), 0.30f, 11),
                (new Vector3(6.6f, 2.95f, -5.75f), new Vector3(0f, 3.05f, -0.6f), 0.30f, 11),
                (new Vector3(-6.75f, 2.95f, 5.4f), new Vector3(-6.75f, 2.95f, -5.4f), 0.32f, 16),
                (new Vector3(6.75f, 2.95f, 5.4f), new Vector3(6.75f, 2.95f, -5.4f), 0.32f, 16)
            };
            foreach (var strand in strands)
            {
                var lights = HiggsfieldAtmosphereVisuals.CreateStringLights(garlands.transform, strand.Item1, strand.Item2, strand.Item3, strand.Item4, atmosphereKit, 0.034f);
                // v0.3.0 scenes.md #5: every garland bulb gets its own small warm halo (0.25 m, alpha 0.3).
                foreach (Transform piece in lights.transform)
                    if (piece.name == "Bulb")
                        HiggsfieldAtmosphereVisuals.CreateHalo(piece, Vector3.zero, LobbyBulbHaloSize, LobbyBulbHaloColor, 1f, atmosphereKit.HaloMaterial).name = BulbHaloName;
            }
            var pools = new[]
            {
                new Vector3(-4.2f, 2.45f, -5.2f), new Vector3(0f, 2.45f, -5.2f), new Vector3(4.2f, 2.45f, -5.2f),
                new Vector3(-6.2f, 2.45f, 0f), new Vector3(6.2f, 2.45f, 0f)
            };
            foreach (var position in pools)
            {
                var item = new GameObject("LMS_LobbyGarlandFill");
                item.transform.SetParent(garlands.transform, false);
                item.transform.localPosition = position;
                var light = item.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LobbyStringFillColor;
                light.intensity = 1.5f;
                light.range = 5.2f;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;
                light.cullingMask &= ~(1 << PreviewLayer);
            }
        }

        private static Renderer FindRenderer(Transform root, string path)
        {
            Transform found = root.Find(path);
            return found != null ? found.GetComponent<Renderer>() : null;
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
            OrientLocalLight(anchor.name, localLight.transform, profile.Type);
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
            if (IsLivingStandingLamp(anchor.name))
            {
                // A low, nearby bulb needs tighter contact than the ceiling fixtures.
                localLight.shadowBias = 0.025f;
                localLight.shadowNormalBias = 0.08f;
                localLight.shadowNearPlane = 0.05f;
            }
            localLight.enabled = true;
            lightObject.SetActive(true);
            return localLight;
        }

        private void AddLobbyCameraFill(Transform presentationAnchors)
        {
            Transform cameraAnchor = presentationAnchors.Find("MainMenuCamera");
            if (cameraAnchor == null)
                return;

            // The character models use broad, low-poly planes. A weak light placed on the
            // menu camera axis preserves their graphic shading without leaving half of a
            // face unlit when the cool directional key hits from the side.
            LocalLightProfile profile = new LocalLightProfile(
                new Color(0.95f, 0.84f, 0.72f),
                0.55f,
                8.5f,
                false,
                LocalShadowTier.Low,
                LightType.Spot);
            Light cameraFill = CreateMapLight(cameraAnchor, profile, false);
            cameraFill.gameObject.name = "LMS_LobbyCameraFill";
            cameraFill.transform.localPosition = Vector3.forward * 2.75f;
            mapLights.Add(cameraFill);
        }

        private static void OrientLocalLight(
            string anchorName, Transform lightTransform, LightType type)
        {
            if (type != LightType.Spot || !Contains(anchorName, "Lobby"))
                return;

            if (Contains(anchorName, "Lantern"))
            {
                // Lobby lantern anchors sit just in front of the back wall. Aim their
                // pools into the room instead of projecting directly onto that wall.
                lightTransform.localRotation = Quaternion.LookRotation(
                    new Vector3(0f, -0.6f, -0.8f), Vector3.up);
                return;
            }

            lightTransform.localRotation = Quaternion.LookRotation(
                Vector3.down, Vector3.forward);
        }

        private static void ApplyAmbientProfile(bool house)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // v0.3.0 scenes r2 (director #1/#2): the menu bedroom and the sala read navy (#2A3560) in shadow, not near black:
            // a brighter cool night ambient under the warm lamps.
            RenderSettings.ambientSkyColor = house
                ? new Color(0.30f, 0.36f, 0.48f)
                : LobbyAmbientSky;
            RenderSettings.ambientEquatorColor = house
                ? new Color(0.22f, 0.23f, 0.30f)
                : LobbyAmbientEquator;
            RenderSettings.ambientGroundColor = house
                ? new Color(0.15f, 0.14f, 0.19f)
                : LobbyAmbientGround;
            RenderSettings.ambientIntensity = house ? 1f : 1.08f;
            RenderSettings.reflectionIntensity = house ? 0.42f : 0.52f;
            RenderSettings.subtractiveShadowColor = new Color(0.018f, 0.025f, 0.045f);

            // URP uploads RenderSettings.ambientProbe to Lit shaders. Runtime changes to
            // Trilight colors do not rebuild that probe until the environment is updated.
            DynamicGI.UpdateEnvironment();
        }

        private static LocalLightProfile ResolveProfile(string anchorName, bool house)
        {
            if (house && IsLivingStandingLamp(anchorName))
                return new LocalLightProfile(new Color(1f, 0.66f, 0.36f), 0.65f, 2.4f, true, LocalShadowTier.Low, LightType.Point);
            if (Contains(anchorName, "Patio"))
                return new LocalLightProfile(new Color(0.42f, 0.58f, 0.92f), 0.42f, 5.5f, false, LocalShadowTier.Low, LightType.Point);
            if (Contains(anchorName, "Lobby"))
                return new LocalLightProfile(new Color(1f, 0.60f, 0.28f), 3.2f, 6.5f, false, LocalShadowTier.Low);
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

        private static bool IsLivingStandingLamp(string anchorName)
        {
            return string.Equals(anchorName, "LightAnchor_Living_StandingLamp", StringComparison.OrdinalIgnoreCase);
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
