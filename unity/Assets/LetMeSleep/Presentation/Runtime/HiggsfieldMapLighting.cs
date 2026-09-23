using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Presentation
{
    /// <summary>Explicit Unity lighting values supplied by Bootstrap; no Content dependency.</summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldMapLighting : MonoBehaviour
    {
        public enum Map { Island, House, Camp, Yacht, Port }
        public enum TimeOfDay { Day, Night, Sunset, Twilight }
        public static TimeOfDay PeriodFor(Map map)
        {
            switch (map)
            {
                case Map.Island: return TimeOfDay.Day;
                case Map.House: case Map.Camp: return TimeOfDay.Night;
                case Map.Yacht: return TimeOfDay.Sunset;
                case Map.Port: return TimeOfDay.Twilight;
                default: throw new ArgumentOutOfRangeException(nameof(map));
            }
        }

        [Serializable] public sealed class LocalSource
        {
            public Transform Anchor;
            public LightType Type = LightType.Point;
            public Color Color = Color.white;
            public float UnityIntensity = float.NaN;
            public float Range = float.NaN;
            public float SpotAngle = 90, InnerSpotAngle = 60;
            public LightShadows Shadows = LightShadows.None;
            // v0.3.0: URP additional-light shadow tier (0 Low, 1 Medium, 2 High = URP default) for shadowed
            // sources, so interiors do not overflow the 2048 shadow atlas and get downscaled.
            public int ShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
            // v0.3.0: soft visual flicker for fire/candle sources, fraction of intensity (0 = steady, max 0.6).
            public float Flicker;
            // v0.3.0 art direction: light position relative to the anchor, in MAP axes (imported anchors can be
            // rotated), e.g. a lantern light moved off its post. Halo and flame offsets use map axes too.
            public Vector3 LocalOffset;
            // v0.3.0: camera-facing halo (metres, 0 = none) at HaloOffset; HaloColor is sRGB with alpha = opacity.
            public float HaloSize;
            public Vector3 HaloOffset;
            public Color HaloColor = new Color(1f, 0.702f, 0.278f, 0.35f);
            public float HaloIntensity = 1f;
            // v0.3.0: three-layer stylized flame (outer height in metres, 0 = none), base at FlameOffset.
            public float FlameHeight;
            public Vector3 FlameOffset;
        }

        /// <summary>Map-wide material replacement on the bound instance (e.g. windows to the night-window shader).</summary>
        [Serializable] public sealed class MaterialSwap
        {
            public Material From;
            public Material To;
        }

        /// <summary>Per-renderer visual override on the bound instance; resolved from relative paths, never stored.</summary>
        [Serializable] public sealed class RendererOverride
        {
            public Renderer Target;
            public bool Hide;
            public bool CastShadowsOff;
            // Lit by the moon and ambient but not by map-local lights (e.g. a lantern post next to its own lantern).
            public bool IgnoreLocalLights;
            public Material SwapFrom;
            public Material SwapTo;
        }

        /// <summary>URP rendering layer that only the moon/sun lights; map-local lights keep the default layer.</summary>
        public const int MoonOnlyRenderingLayer = 6;

        [Serializable] public sealed class Configuration
        {
            public Map MapId;
            public Material Skybox;
            public Quaternion SunWorldRotation = Quaternion.identity;
            public Color SunColor = Color.white;
            public float SunUnityIntensity = float.NaN;
            public LightShadows SunShadows = LightShadows.Soft;
            public Color AmbientSky, AmbientEquator, AmbientGround;
            public float AmbientIntensity = float.NaN, ReflectionIntensity = float.NaN;
            public bool FogEnabled;
            public Color FogColor;
            public FogMode FogMode = FogMode.Linear;
            public float FogStart, FogEnd = 100, FogDensity = 0.01f;
            public VolumeProfile VolumeProfile;
            public float VolumeWeight = 1;
            public int CullingMask = ~(1 << 30);
            public LocalSource[] LocalLights = Array.Empty<LocalSource>();
            // Explicit extra scene lights owned by the integrating Bootstrap, if any.
            public Light[] SuppressLights = Array.Empty<Light>();
            // v0.3.0 atmosphere (visual only; never touches colliders, spawns, navigation or ContentHash).
            public HiggsfieldAtmosphereKit Kit;
            public MaterialSwap[] MaterialSwaps = Array.Empty<MaterialSwap>();
            // Map-local interiors: the night-window shader shows the night sky from inside, warm glass from outside.
            public Bounds[] InteriorVolumes = Array.Empty<Bounds>();
            public RendererOverride[] RendererOverrides = Array.Empty<RendererOverride>();
            // Warm red kicker on characters tagged with the rim rendering layer (night legibility), 0 = off.
            public Color CharacterRimColor = new Color(1f, 0.416f, 0.416f);
            public float CharacterRimIntensity;
        }

        public const int MaximumInteriorVolumes = 8;
        private static readonly int InteriorMinId = Shader.PropertyToID("_LMS_InteriorMin");
        private static readonly int InteriorMaxId = Shader.PropertyToID("_LMS_InteriorMax");
        private static readonly int InteriorCountId = Shader.PropertyToID("_LMS_InteriorCount");

        public bool IsBound { get; private set; }
        public Transform MapRoot { get; private set; }
        public TimeOfDay Period { get; private set; }
        public int LocalLightCount => spawned.Count;
        private readonly List<Light> spawned = new List<Light>();
        private readonly Dictionary<Light, bool> suppressed = new Dictionary<Light, bool>();
        private Light primary;
        private LightType oldType;
        private Color oldColor;
        private float oldIntensity;
        private bool oldEnabled;
        private int oldMask;
        private LightShadows oldShadows;
        private Quaternion oldRotation;
        private Light oldSun;
        private Material oldSkybox;
        private AmbientMode oldAmbientMode;
        private Color oldSky, oldEquator, oldGround, oldFogColor;
        private float oldAmbientIntensity, oldReflectionIntensity, oldFogStart, oldFogEnd, oldFogDensity;
        private bool oldFog;
        private FogMode oldFogMode;
        private SphericalHarmonicsL2 oldProbe;
        private Volume volume;
        private VolumeProfile oldVolumeProfile;
        private float oldVolumeWeight;
        private bool oldVolumeEnabled;
        private readonly List<KeyValuePair<HiggsfieldGpuWater, bool>> waterFog = new List<KeyValuePair<HiggsfieldGpuWater, bool>>();
        private readonly List<GameObject> visuals = new List<GameObject>();
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Renderer, bool> originalEnabled = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, ShadowCastingMode> originalShadows = new Dictionary<Renderer, ShadowCastingMode>();
        private readonly Dictionary<Renderer, uint> originalLayers = new Dictionary<Renderer, uint>();
        private uint oldSunLayers;
        private bool sunLayersChanged;
        private HiggsfieldRimLight rimLight;
        public int AtmosphereVisualCount => visuals.Count;
        public int OverriddenRendererCount => originalMaterials.Count + originalEnabled.Count + originalShadows.Count + originalLayers.Count;
        public HiggsfieldRimLight RimLight => rimLight;

        public static void Validate(Transform root, Light sun, Configuration config)
        {
            if (!root || !sun || config == null) throw new ArgumentException("Map root, existing primary light and configuration are required.");
            PeriodFor(config.MapId);
            Nonnegative(config.SunUnityIntensity); Nonnegative(config.AmbientIntensity); Nonnegative(config.ReflectionIntensity);
            CheckColor(config.SunColor); CheckColor(config.AmbientSky); CheckColor(config.AmbientEquator); CheckColor(config.AmbientGround);
            CheckColor(config.FogColor); Nonnegative(config.FogDensity); Nonnegative(config.FogStart); Nonnegative(config.FogEnd);
            if (config.FogEnd <= config.FogStart || !Finite(config.VolumeWeight) || config.VolumeWeight < 0 || config.VolumeWeight > 1)
                throw new ArgumentException("Invalid fog range or volume weight.");
            Quaternion q = config.SunWorldRotation;
            if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) || Mathf.Abs(Quaternion.Dot(q, q) - 1) > .01f)
                throw new ArgumentException("Sun rotation must be a normalized Unity world rotation.");
            if (config.LocalLights == null || config.LocalLights.Length > 64) throw new ArgumentException("Explicit local light list is required (maximum64).");
            var anchors = new HashSet<Transform>();
            foreach (var source in config.LocalLights)
            {
                if (source == null || !source.Anchor || !source.Anchor.IsChildOf(root) || !anchors.Add(source.Anchor) ||
                    (source.Type != LightType.Point && source.Type != LightType.Spot))
                    throw new ArgumentException("Distinct owned anchors and Point/Spot types are required.");
                Nonnegative(source.UnityIntensity); CheckColor(source.Color);
                if (!Finite(source.Range) || source.Range <= 0 || !Finite(source.SpotAngle) || !Finite(source.InnerSpotAngle) ||
                    source.SpotAngle <= 0 || source.SpotAngle >= 180 || source.InnerSpotAngle < 0 || source.InnerSpotAngle > source.SpotAngle)
                    throw new ArgumentException("Invalid local range or spot angles.");
                if (!ValidShadowTier(source.ShadowResolutionTier) || !Finite(source.Flicker) || source.Flicker < 0 ||
                    source.Flicker > HiggsfieldLightFlicker.MaximumAmplitude)
                    throw new ArgumentException("Invalid local shadow tier (0..2) or flicker (0..0.6).");
                ValidateVisual(source, config.Kit);
            }
            ValidateAtmosphere(root, config);
        }

        public static void ValidateVisual(LocalSource source, HiggsfieldAtmosphereKit kit)
        {
            if (!FiniteVector(source.LocalOffset) || source.LocalOffset.magnitude > 3f || !FiniteVector(source.HaloOffset) ||
                source.HaloOffset.magnitude > 3f || !FiniteVector(source.FlameOffset) || source.FlameOffset.magnitude > 3f)
                throw new ArgumentException("Local light, halo and flame offsets must be finite and within 3 m of the anchor.");
            if (!Finite(source.HaloSize) || source.HaloSize < 0 || source.HaloSize > 6 || !Finite(source.HaloIntensity) ||
                source.HaloIntensity < 0 || source.HaloIntensity > 16 || !Finite(source.FlameHeight) || source.FlameHeight < 0 ||
                source.FlameHeight > 4)
                throw new ArgumentException("Halo size 0..6 m, halo intensity 0..16 and flame height 0..4 m required.");
            CheckColor(source.HaloColor);
            if (source.HaloColor.a > 1) throw new ArgumentException("Halo opacity must be 0..1.");
            if ((source.HaloSize > 0 || source.FlameHeight > 0) && (!kit || !kit.IsComplete))
                throw new ArgumentException("Halos and flames need a complete HiggsfieldAtmosphereKit.");
        }

        public static void ValidateAtmosphere(Transform root, Configuration config)
        {
            if (config.MaterialSwaps == null || config.InteriorVolumes == null || config.RendererOverrides == null)
                throw new ArgumentException("Atmosphere arrays must be initialized.");
            if (config.InteriorVolumes.Length > MaximumInteriorVolumes)
                throw new ArgumentException("At most " + MaximumInteriorVolumes + " interior volumes.");
            foreach (var volume in config.InteriorVolumes)
                if (!FiniteVector(volume.center) || !FiniteVector(volume.size) || volume.size.x <= 0 || volume.size.y <= 0 || volume.size.z <= 0)
                    throw new ArgumentException("Interior volumes need finite centers and positive sizes.");
            var sources = new HashSet<Material>();
            foreach (var swap in config.MaterialSwaps)
                if (swap == null || !swap.From || !swap.To || swap.From == swap.To || !sources.Add(swap.From))
                    throw new ArgumentException("Material swaps need distinct non-null From and a different To.");
            var targets = new HashSet<Renderer>();
            foreach (var item in config.RendererOverrides)
            {
                if (item == null || !item.Target || (root && !item.Target.transform.IsChildOf(root)) || !targets.Add(item.Target))
                    throw new ArgumentException("Renderer overrides need distinct renderers owned by the map.");
                if ((item.SwapFrom == null) != (item.SwapTo == null) || (item.SwapFrom && item.SwapFrom == item.SwapTo))
                    throw new ArgumentException("Renderer material swap needs both From and a different To.");
                if (item.SwapFrom && Array.IndexOf(item.Target.sharedMaterials, item.SwapFrom) < 0)
                    throw new ArgumentException("Renderer override swap source is not on the renderer: " + item.Target.name);
                if (!item.Hide && !item.CastShadowsOff && !item.IgnoreLocalLights && !item.SwapFrom)
                    throw new ArgumentException("Renderer override does nothing: " + item.Target.name);
            }
            CheckColor(config.CharacterRimColor);
            if (!Finite(config.CharacterRimIntensity) || config.CharacterRimIntensity < 0 || config.CharacterRimIntensity > HiggsfieldRimLight.MaximumIntensity)
                throw new ArgumentException("Character rim intensity 0..3 required.");
        }

        /// <param name="lowTierTemplate">Optional disabled Light whose URP data uses the Low shadow tier (tier is editor-only data).</param>
        /// <param name="mediumTierTemplate">Optional disabled Light whose URP data uses the Medium shadow tier.</param>
        public void Bind(Transform root, Light sun, Volume globalVolume, Configuration config, IEnumerable<Light> previousLights,
            Light lowTierTemplate = null, Light mediumTierTemplate = null)
        {
            Validate(root, sun, config); // Failed validation does not disturb the current binding.
            Unbind();
            primary = sun; volume = globalVolume; MapRoot = root; Period = PeriodFor(config.MapId);
            oldType = sun.type; oldColor = sun.color; oldIntensity = sun.intensity; oldEnabled = sun.enabled;
            oldMask = sun.cullingMask; oldShadows = sun.shadows; oldRotation = sun.transform.rotation;
            oldSun = RenderSettings.sun; oldSkybox = RenderSettings.skybox; oldAmbientMode = RenderSettings.ambientMode;
            oldSky = RenderSettings.ambientSkyColor; oldEquator = RenderSettings.ambientEquatorColor; oldGround = RenderSettings.ambientGroundColor;
            oldAmbientIntensity = RenderSettings.ambientIntensity; oldReflectionIntensity = RenderSettings.reflectionIntensity;
            oldProbe = RenderSettings.ambientProbe; oldFog = RenderSettings.fog; oldFogMode = RenderSettings.fogMode;
            oldFogColor = RenderSettings.fogColor; oldFogStart = RenderSettings.fogStartDistance;
            oldFogEnd = RenderSettings.fogEndDistance; oldFogDensity = RenderSettings.fogDensity;
            if (volume) { oldVolumeProfile = volume.sharedProfile; oldVolumeWeight = volume.weight; oldVolumeEnabled = volume.enabled; }
            IsBound = true;
            try
            {
                if (previousLights != null) foreach (var light in previousLights) Suppress(light);
                if (config.SuppressLights != null) foreach (var light in config.SuppressLights) Suppress(light);
                Suppress(oldSun);
                // Imported directional sources in this map must not become a second sun.
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional) Suppress(light);
                sun.type = LightType.Directional; sun.color = config.SunColor; sun.intensity = config.SunUnityIntensity;
                sun.shadows = config.SunShadows; sun.cullingMask = config.CullingMask;
                sun.transform.rotation = config.SunWorldRotation; sun.enabled = true;
                RenderSettings.sun = sun; RenderSettings.skybox = config.Skybox;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = config.AmbientSky; RenderSettings.ambientEquatorColor = config.AmbientEquator;
                RenderSettings.ambientGroundColor = config.AmbientGround; RenderSettings.ambientIntensity = config.AmbientIntensity;
                RenderSettings.reflectionIntensity = config.ReflectionIntensity;
                RenderSettings.fog = config.FogEnabled; RenderSettings.fogMode = config.FogMode; RenderSettings.fogColor = config.FogColor;
                RenderSettings.fogStartDistance = config.FogStart; RenderSettings.fogEndDistance = config.FogEnd; RenderSettings.fogDensity = config.FogDensity;
                if (volume) { volume.sharedProfile = config.VolumeProfile; volume.weight = config.VolumeWeight; volume.enabled = config.VolumeProfile != null; }
                // Unlit GPU ocean opts into scene fog only while this map's fog is on, so it fades into the horizon.
                foreach (var water in root.GetComponentsInChildren<HiggsfieldGpuWater>(true))
                {
                    if (!water.IsBound) continue;
                    var parameters = water.CurrentParameters;
                    waterFog.Add(new KeyValuePair<HiggsfieldGpuWater, bool>(water, parameters.UseFog));
                    if (parameters.UseFog == config.FogEnabled) continue;
                    parameters.UseFog = config.FogEnabled;
                    water.SetParameters(parameters);
                }
                foreach (var source in config.LocalLights)
                {
                    // URP's per-light shadow tier is serialized-only data: lower tiers come from the rig's templates.
                    Light template = source.Shadows == LightShadows.None ? null
                        : source.ShadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow ? lowTierTemplate
                        : source.ShadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium ? mediumTierTemplate
                        : null;
                    GameObject go;
                    Light light;
                    if (template)
                    {
                        light = Instantiate(template, source.Anchor, false);
                        go = light.gameObject;
                        go.SetActive(false); go.name = "Higgsfield_LocalLight";
                        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        go = new GameObject("Higgsfield_LocalLight");
                        go.SetActive(false); go.transform.SetParent(source.Anchor, false);
                        light = go.AddComponent<Light>();
                    }
                    spawned.Add(light);
                    light.type = source.Type; light.color = source.Color; light.intensity = source.UnityIntensity;
                    light.range = source.Range; light.spotAngle = source.SpotAngle; light.innerSpotAngle = source.InnerSpotAngle;
                    light.shadows = source.Shadows; light.cullingMask = config.CullingMask; light.bounceIntensity = 0;
                    go.transform.position = source.Anchor.position + root.rotation * source.LocalOffset;
                    light.enabled = true;
                    go.SetActive(true);
                    if (source.Flicker > 0)
                        go.AddComponent<HiggsfieldLightFlicker>().Configure(source.Flicker, StableSeed(source.Anchor.name));
                    if (source.HaloSize > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateHalo(source.Anchor, root.rotation * source.HaloOffset, source.HaloSize,
                            source.HaloColor, source.HaloIntensity, config.Kit.HaloMaterial));
                    if (source.FlameHeight > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateFlame(source.Anchor, root.rotation * source.FlameOffset, source.FlameHeight,
                            config.Kit, StableSeed(source.Anchor.name)));
                }
                ApplyRendererAtmosphere(root, config, sun);
                if (config.CharacterRimIntensity > 0)
                {
                    var rimObject = new GameObject("Higgsfield_CharacterRim");
                    rimObject.transform.SetParent(transform, false);
                    rimObject.AddComponent<Light>();
                    rimLight = rimObject.AddComponent<HiggsfieldRimLight>();
                    rimLight.Configure(config.CharacterRimColor, config.CharacterRimIntensity);
                }
                DynamicGI.UpdateEnvironment();
            }
            catch { Unbind(); throw; }
        }

        private void ApplyRendererAtmosphere(Transform root, Configuration config, Light sun)
        {
            if (config.MaterialSwaps.Length > 0)
            {
                var map = new Dictionary<Material, Material>();
                foreach (var swap in config.MaterialSwaps) map[swap.From] = swap.To;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] && map.TryGetValue(materials[i], out var replacement)) { materials[i] = replacement; changed = true; }
                    if (!changed) continue;
                    if (!originalMaterials.ContainsKey(renderer)) originalMaterials.Add(renderer, renderer.sharedMaterials);
                    renderer.sharedMaterials = materials;
                }
            }
            foreach (var item in config.RendererOverrides)
            {
                var renderer = item.Target;
                if (item.SwapFrom)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) if (materials[i] == item.SwapFrom) materials[i] = item.SwapTo;
                    if (!originalMaterials.ContainsKey(renderer)) originalMaterials.Add(renderer, renderer.sharedMaterials);
                    renderer.sharedMaterials = materials;
                }
                if (item.Hide)
                {
                    if (!originalEnabled.ContainsKey(renderer)) originalEnabled.Add(renderer, renderer.enabled);
                    renderer.enabled = false;
                }
                if (item.CastShadowsOff)
                {
                    if (!originalShadows.ContainsKey(renderer)) originalShadows.Add(renderer, renderer.shadowCastingMode);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (item.IgnoreLocalLights)
                {
                    if (!originalLayers.ContainsKey(renderer)) originalLayers.Add(renderer, renderer.renderingLayerMask);
                    renderer.renderingLayerMask = (renderer.renderingLayerMask & ~1u) | (1u << MoonOnlyRenderingLayer);
                }
            }
            if (originalLayers.Count > 0 && sun)
            {
                var data = sun.GetUniversalAdditionalLightData();
                oldSunLayers = data.renderingLayers;
                data.renderingLayers = oldSunLayers | (1u << MoonOnlyRenderingLayer);
                sunLayersChanged = true;
            }
            SetInteriorVolumes(root, config.InteriorVolumes);
        }

        /// <summary>Publishes map interiors (world AABBs) for LetMeSleep/Higgsfield/NightWindow; empty clears them.</summary>
        public static void SetInteriorVolumes(Transform root, Bounds[] volumes)
        {
            var min = new Vector4[MaximumInteriorVolumes];
            var max = new Vector4[MaximumInteriorVolumes];
            int count = volumes == null ? 0 : Mathf.Min(volumes.Length, MaximumInteriorVolumes);
            for (int i = 0; i < count; i++)
            {
                Bounds local = volumes[i];
                Vector3 a = root ? root.TransformPoint(local.min) : local.min;
                Vector3 b = root ? root.TransformPoint(local.max) : local.max;
                min[i] = Vector3.Min(a, b);
                max[i] = Vector3.Max(a, b);
            }
            Shader.SetGlobalVectorArray(InteriorMinId, min);
            Shader.SetGlobalVectorArray(InteriorMaxId, max);
            Shader.SetGlobalFloat(InteriorCountId, count);
        }

        private void Suppress(Light light)
        {
            if (!light || light == primary || suppressed.ContainsKey(light)) return;
            suppressed.Add(light, light.enabled); light.enabled = false;
        }

        public void Unbind()
        {
            if (!IsBound) return;
            IsBound = false;
            foreach (var light in spawned)
            {
                if (!light) continue;
                light.enabled = false; light.gameObject.SetActive(false); // No duplicate frame while Destroy is deferred.
                if (Application.isPlaying) Destroy(light.gameObject); else DestroyImmediate(light.gameObject);
            }
            spawned.Clear();
            foreach (var item in visuals)
                if (item) { item.SetActive(false); if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            visuals.Clear();
            foreach (var item in originalMaterials) if (item.Key) item.Key.sharedMaterials = item.Value;
            originalMaterials.Clear();
            foreach (var item in originalEnabled) if (item.Key) item.Key.enabled = item.Value;
            originalEnabled.Clear();
            foreach (var item in originalShadows) if (item.Key) item.Key.shadowCastingMode = item.Value;
            originalShadows.Clear();
            foreach (var item in originalLayers) if (item.Key) item.Key.renderingLayerMask = item.Value;
            originalLayers.Clear();
            if (sunLayersChanged && primary) primary.GetUniversalAdditionalLightData().renderingLayers = oldSunLayers;
            sunLayersChanged = false;
            SetInteriorVolumes(null, null);
            if (rimLight)
            {
                rimLight.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(rimLight.gameObject); else DestroyImmediate(rimLight.gameObject);
            }
            rimLight = null;
            foreach (var item in waterFog)
            {
                if (!item.Key || !item.Key.IsBound) continue;
                var parameters = item.Key.CurrentParameters;
                if (parameters.UseFog == item.Value) continue;
                parameters.UseFog = item.Value;
                item.Key.SetParameters(parameters);
            }
            waterFog.Clear();
            foreach (var item in suppressed) if (item.Key) item.Key.enabled = item.Value;
            suppressed.Clear();
            if (primary)
            {
                primary.type = oldType; primary.color = oldColor; primary.intensity = oldIntensity; primary.enabled = oldEnabled;
                primary.cullingMask = oldMask; primary.shadows = oldShadows; primary.transform.rotation = oldRotation;
            }
            RenderSettings.sun = oldSun; RenderSettings.skybox = oldSkybox; RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.ambientIntensity = oldAmbientIntensity; RenderSettings.reflectionIntensity = oldReflectionIntensity;
            RenderSettings.fog = oldFog; RenderSettings.fogMode = oldFogMode; RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogStartDistance = oldFogStart; RenderSettings.fogEndDistance = oldFogEnd; RenderSettings.fogDensity = oldFogDensity;
            if (volume) { volume.sharedProfile = oldVolumeProfile; volume.weight = oldVolumeWeight; volume.enabled = oldVolumeEnabled; }
            DynamicGI.UpdateEnvironment(); RenderSettings.ambientProbe = oldProbe;
            MapRoot = null; primary = null; volume = null;
        }

        private void LateUpdate() { if (IsBound && !MapRoot) Unbind(); }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteVector(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        public static bool ValidShadowTier(int tier) =>
            tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow ||
            tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium ||
            tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
        private static float StableSeed(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text ?? string.Empty) hash = hash * 31 + c;
                return (hash & 0xFFFF) / 97f;
            }
        }
        private static void Nonnegative(float value) { if (!Finite(value) || value < 0) throw new ArgumentException("Explicit finite nonnegative Unity intensity/environment values required."); }
        private static void CheckColor(Color value) { Nonnegative(value.r); Nonnegative(value.g); Nonnegative(value.b); Nonnegative(value.a); }
    }
}
