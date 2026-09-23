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
            // v0.3.0 r3: URP rendering layers lit by this light (bit mask, 0 = URP default "layer 0" only). A lantern
            // can light only the ground pool and characters, never the pines or rails around it.
            public int LightLayers;
            // v0.3.0 r3: depth tolerance of the halo occlusion test in metres (0 = automatic). Lamps inside glass
            // lanterns behind mullions/railings need a larger value so the bars in front never cut the glow.
            public float HaloDepthTolerance;
            // v0.3.0 r3: rotating light shafts (lighthouse). Length in metres, 0 = none; alpha of BeamColor = opacity.
            public float BeamLength, BeamRadius, BeamSpeed, BeamTilt;
            public int BeamCount = 2;
            public Vector3 BeamOffset;
            public Color BeamColor = new Color(1f, 0.824f, 0.478f, 0.15f);
            // v0.3.0 r3: warm vertical reflection streak on the water below the lamp (length 0 = none). WaterY is
            // the water surface height in map space; alpha of ReflectionColor = opacity.
            public float ReflectionLength, ReflectionWidth, ReflectionWaterY;
            public Color ReflectionColor = new Color(1f, 0.702f, 0.278f, 0.5f);
            // v0.3.0 r3: warm pool decal on the ground below the light (radius in metres, 0 = none). Alpha of PoolColor
            // is the opacity at the center; the ground is found with a downward ray from 0.6 m below the light.
            public float PoolRadius;
            public Color PoolColor = new Color(0.45f, 0.31f, 0.18f, 0.55f);
            // v0.3.0 r4: bright round core of the halo (0 = the halo material's default), e.g. the lighthouse lens.
            public float HaloCore;
            // v0.3.0 r4: shadow normal bias of a shadowed source (0 = Unity default 0.4); 1.0 removes the striped acne a
            // soft point shadow leaves on thin baseboards and foundations next to it.
            public float ShadowNormalBias;
            // v0.3.0 r4: visual-only small hanging lantern around the light (edge size in metres, 0 = none).
            public float LanternSize;

            /// <summary>Copy of every serialized value bound to an instance anchor (catalog assets never keep one).</summary>
            public LocalSource CloneFor(Transform anchor)
            {
                var copy = (LocalSource)MemberwiseClone();
                copy.Anchor = anchor;
                return copy;
            }
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
            // v0.3.0 r3: extra URP rendering layers (bit mask) for this renderer, e.g. the ground that receives a
            // lantern pool (LanternPoolRenderingLayer) while pines and rails do not.
            public int AddLightLayers;
            // v0.3.0 r4: further material slots swapped on the same renderer (e.g. both stone swatches of a stairway).
            public MaterialSwap[] ExtraSwaps = Array.Empty<MaterialSwap>();
        }

        /// <summary>URP rendering layer that only the moon/sun lights; map-local lights keep the default layer.</summary>
        public const int MoonOnlyRenderingLayer = 6;
        /// <summary>Receivers of lantern pools (ground, rocks, paths) for lights restricted with LightLayers.</summary>
        public const int LanternPoolRenderingLayer = 4;
        /// <summary>
        /// Everything that is not fully inside an interior volume (ExteriorLightsSkipInteriors): exterior lamps restricted to
        /// this layer never light rooms through the (shadowless) walls.
        /// </summary>
        public const int ExteriorRenderingLayer = 3;
        /// <summary>
        /// Renderers that do not touch any interior volume (ExteriorLightsSkipInteriors): a shadowless directional sky fill
        /// restricted to this layer cannot reach the inner faces of exterior walls and roofs (which only get
        /// ExteriorRenderingLayer, lit by point lamps whose falloff and N.L keep them outside).
        /// </summary>
        public const int OpenAirRenderingLayer = 2;
        /// <summary>Rendering layers a config may use: 0 default, 4 lantern pool, 5 character fill, 7 mosquito rim.</summary>
        public const int AllowedLightLayers = (1 << 0) | (1 << OpenAirRenderingLayer) | (1 << ExteriorRenderingLayer) | (1 << LanternPoolRenderingLayer) |
            (1 << HiggsfieldRimLight.FillRenderingLayer) | (1 << HiggsfieldRimLight.RenderingLayer);

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
            // v0.3.0 r3: 0 = one top-back kicker, 1 = two side-back kickers that outline both silhouette edges.
            public float CharacterRimSpread;
            // v0.3.0 r3: camera-side fill on every character (fill rendering layer) so bodies, shirts and eyes read at night.
            public Color CharacterFillColor = new Color(1f, 0.94f, 0.86f);
            public float CharacterFillIntensity;
            // v0.3.0 r3: warm indoor bounce for renderers inside InteriorVolumes (per-renderer ambient probe). Inner
            // faces of exterior walls/roofs get it only on the side facing the room; 0 = off.
            public Color InteriorAmbientColor = new Color(0.55f, 0.36f, 0.2f);
            public float InteriorAmbientIntensity;
            // Fraction of the night ambient kept indoors, and the smallest renderer (metres) that gets a probe.
            public float InteriorAmbientKeep = 0.3f;
            public float InteriorAmbientMinSize = 0.5f;
            // v0.3.0 r3: tag every renderer that is not fully inside an interior volume with ExteriorRenderingLayer, so
            // porch/wall lamps restricted to that layer stop burning partitions and furniture through the walls.
            public bool ExteriorLightsSkipInteriors;
            // v0.3.0 r3: explicit shadowless sky fill owned by the map binding. Until r3 every map was also lit by the UI
            // customization key (a 1.5 directional light meant for the preview layer; Forward+ ignores light culling
            // masks), which burned interiors. Bind now suppresses stray directional lights and this fill replaces the
            // key's contribution on purpose, optionally restricted to rendering layers (e.g. exterior + characters).
            public Color SkyFillColor = Color.white;
            public float SkyFillIntensity;
            public Quaternion SkyFillRotation = Quaternion.Euler(30f, 150f, 0f);
            public int SkyFillLightLayers;
        }

        public const int MaximumInteriorVolumes = 8;
        private static readonly int PaneMinId = Shader.PropertyToID("_PaneMin");
        private static readonly int PaneMaxId = Shader.PropertyToID("_PaneMax");
        private readonly Dictionary<Renderer, MaterialPropertyBlock> originalBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        private readonly Dictionary<Renderer, LightProbeUsage> originalProbeUsage = new Dictionary<Renderer, LightProbeUsage>();
        public int InteriorAmbientRendererCount => originalProbeUsage.Count;
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
        private Light skyFill;
        public Light SkyFill => skyFill;
        public int AtmosphereVisualCount => visuals.Count;
        public int OverriddenRendererCount => originalMaterials.Count + originalEnabled.Count + originalShadows.Count + originalLayers.Count +
            originalBlocks.Count;
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
            if (!Finite(source.HaloSize) || source.HaloSize < 0 || source.HaloSize > 12 || !Finite(source.HaloIntensity) ||
                source.HaloIntensity < 0 || source.HaloIntensity > 16 || !Finite(source.FlameHeight) || source.FlameHeight < 0 ||
                source.FlameHeight > 4)
                throw new ArgumentException("Halo size 0..12 m, halo intensity 0..16 and flame height 0..4 m required.");
            CheckColor(source.HaloColor);
            if (source.HaloColor.a > 1) throw new ArgumentException("Halo opacity must be 0..1.");
            if ((source.HaloSize > 0 || source.FlameHeight > 0) && (!kit || !kit.IsComplete))
                throw new ArgumentException("Halos and flames need a complete HiggsfieldAtmosphereKit.");
            if ((source.LightLayers & ~AllowedLightLayers) != 0 || !Finite(source.HaloDepthTolerance) || source.HaloDepthTolerance < 0 ||
                source.HaloDepthTolerance > 8)
                throw new ArgumentException("Light layers must use the allowed rendering layers; halo depth tolerance 0..8 m.");
            if (!Finite(source.BeamLength) || source.BeamLength < 0 || source.BeamLength > 80 || !Finite(source.BeamRadius) ||
                source.BeamRadius < 0 || source.BeamRadius > 20 || !Finite(source.BeamSpeed) || Mathf.Abs(source.BeamSpeed) > 360 ||
                !Finite(source.BeamTilt) || Mathf.Abs(source.BeamTilt) > 45 || source.BeamCount < 1 || source.BeamCount > 4 ||
                !FiniteVector(source.BeamOffset) || source.BeamOffset.magnitude > 3f)
                throw new ArgumentException("Beam: length 0..80 m, radius 0..20 m, speed |deg/s| <= 360, tilt |deg| <= 45, 1..4 shafts.");
            CheckColor(source.BeamColor); CheckColor(source.ReflectionColor);
            if (source.BeamColor.a > 1 || source.ReflectionColor.a > 1) throw new ArgumentException("Beam/reflection opacity must be 0..1.");
            if (!Finite(source.ReflectionLength) || source.ReflectionLength < 0 || source.ReflectionLength > 30 ||
                !Finite(source.ReflectionWidth) || source.ReflectionWidth < 0 || source.ReflectionWidth > 5 || !Finite(source.ReflectionWaterY))
                throw new ArgumentException("Reflection length 0..30 m and width 0..5 m required.");
            if ((source.BeamLength > 0 && (!kit || !kit.BeamMaterial)) || (source.ReflectionLength > 0 && (!kit || !kit.GlintMaterial)))
                throw new ArgumentException("Beams and water reflections need the kit's beam/glint materials.");
            CheckColor(source.PoolColor);
            if (!Finite(source.PoolRadius) || source.PoolRadius < 0 || source.PoolRadius > 8 || source.PoolColor.a > 1)
                throw new ArgumentException("Pool radius 0..8 m and opacity 0..1 required.");
            if (source.PoolRadius > 0 && (!kit || !kit.PoolMaterial))
                throw new ArgumentException("Ground pools need the kit's pool material.");
            if (!Finite(source.HaloCore) || source.HaloCore < 0 || source.HaloCore > 8 || !Finite(source.ShadowNormalBias) ||
                source.ShadowNormalBias < 0 || source.ShadowNormalBias > 3 || !Finite(source.LanternSize) || source.LanternSize < 0 ||
                source.LanternSize > 1)
                throw new ArgumentException("Halo core 0..8, shadow normal bias 0..3 and lantern size 0..1 m required.");
            if (source.LanternSize > 0 && (!kit || !kit.IsComplete))
                throw new ArgumentException("Lantern props need a complete HiggsfieldAtmosphereKit.");
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
                var extraSources = new HashSet<Material>();
                if (item.SwapFrom) extraSources.Add(item.SwapFrom);
                foreach (var extra in item.ExtraSwaps ?? Array.Empty<MaterialSwap>())
                    if (!item.SwapFrom || extra == null || !extra.From || !extra.To || extra.From == extra.To || !extraSources.Add(extra.From) ||
                        Array.IndexOf(item.Target.sharedMaterials, extra.From) < 0)
                        throw new ArgumentException("Renderer override extra swaps need a first swap and distinct sources on the renderer: " + item.Target.name);
                if ((item.AddLightLayers & ~AllowedLightLayers) != 0)
                    throw new ArgumentException("Renderer override adds a rendering layer that is not allowed: " + item.Target.name);
                if (!item.Hide && !item.CastShadowsOff && !item.IgnoreLocalLights && !item.SwapFrom && item.AddLightLayers == 0)
                    throw new ArgumentException("Renderer override does nothing: " + item.Target.name);
            }
            CheckColor(config.CharacterRimColor); CheckColor(config.CharacterFillColor); CheckColor(config.InteriorAmbientColor);
            if (!Finite(config.CharacterRimIntensity) || config.CharacterRimIntensity < 0 || config.CharacterRimIntensity > HiggsfieldRimLight.MaximumIntensity ||
                !Finite(config.CharacterFillIntensity) || config.CharacterFillIntensity < 0 || config.CharacterFillIntensity > HiggsfieldRimLight.MaximumIntensity ||
                !Finite(config.CharacterRimSpread) || config.CharacterRimSpread < 0 || config.CharacterRimSpread > 1)
                throw new ArgumentException("Character rim/fill intensity 0..3 and rim spread 0..1 required.");
            CheckColor(config.SkyFillColor);
            Quaternion fill = config.SkyFillRotation;
            if (!Finite(config.SkyFillIntensity) || config.SkyFillIntensity < 0 || config.SkyFillIntensity > 3 ||
                (config.SkyFillLightLayers & ~AllowedLightLayers) != 0 || !Finite(fill.x) || !Finite(fill.y) || !Finite(fill.z) ||
                !Finite(fill.w) || Mathf.Abs(Quaternion.Dot(fill, fill) - 1) > .01f)
                throw new ArgumentException("Sky fill intensity 0..3, allowed layers and a normalized rotation required.");
            if (!Finite(config.InteriorAmbientIntensity) || config.InteriorAmbientIntensity < 0 || config.InteriorAmbientIntensity > 4 ||
                !Finite(config.InteriorAmbientKeep) || config.InteriorAmbientKeep < 0 || config.InteriorAmbientKeep > 1 ||
                !Finite(config.InteriorAmbientMinSize) || config.InteriorAmbientMinSize < 0 || config.InteriorAmbientMinSize > 10)
                throw new ArgumentException("Interior ambient intensity 0..4, keep 0..1 and minimum size 0..10 m required.");
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
                // Forward+ ignores light culling masks: any other enabled directional light in the scene (e.g. the UI
                // customization key meant for the preview layer) would light the whole map. Only the moon/sun and this
                // binding's own sky fill and character lights may light a map.
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && light != sun && light.enabled) Suppress(light);
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
                    if (source.ShadowNormalBias > 0) light.shadowNormalBias = source.ShadowNormalBias;
                    if (source.LightLayers != 0) light.GetUniversalAdditionalLightData().renderingLayers = (uint)source.LightLayers;
                    go.transform.position = source.Anchor.position + root.rotation * source.LocalOffset;
                    light.enabled = true;
                    go.SetActive(true);
                    if (source.Flicker > 0)
                        go.AddComponent<HiggsfieldLightFlicker>().Configure(source.Flicker, StableSeed(source.Anchor.name));
                    if (source.HaloSize > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateHalo(source.Anchor, root.rotation * source.HaloOffset, source.HaloSize,
                            source.HaloColor, source.HaloIntensity, config.Kit.HaloMaterial, source.HaloDepthTolerance, source.HaloCore));
                    if (source.BeamLength > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateBeam(source.Anchor, root.rotation * source.BeamOffset, source.BeamLength,
                            source.BeamRadius, source.BeamCount, source.BeamTilt, source.BeamSpeed, source.BeamColor, config.Kit.BeamMaterial,
                            StableSeed(source.Anchor.name)));
                    if (source.PoolRadius > 0)
                    {
                        Vector3 from = source.Anchor.position + root.rotation * source.LocalOffset + Vector3.down * 0.6f;
                        if (Physics.Raycast(from, Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore))
                            visuals.Add(HiggsfieldAtmosphereVisuals.CreatePool(source.Anchor, ground.point, source.PoolRadius,
                                source.PoolColor, config.Kit.PoolMaterial));
                    }
                    if (source.ReflectionLength > 0)
                    {
                        Vector3 anchorLocal = root.InverseTransformPoint(source.Anchor.position + root.rotation * source.LocalOffset);
                        Vector3 surface = root.TransformPoint(new Vector3(anchorLocal.x, source.ReflectionWaterY, anchorLocal.z));
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateGlint(source.Anchor, surface, source.ReflectionLength,
                            source.ReflectionWidth, source.ReflectionColor, config.Kit.GlintMaterial, StableSeed(source.Anchor.name)));
                    }
                    if (source.LanternSize > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateLantern(source.Anchor, root.rotation * source.LocalOffset, source.LanternSize,
                            config.Kit));
                    if (source.FlameHeight > 0)
                        visuals.Add(HiggsfieldAtmosphereVisuals.CreateFlame(source.Anchor, root.rotation * source.FlameOffset, source.FlameHeight,
                            config.Kit, StableSeed(source.Anchor.name)));
                }
                ApplyRendererAtmosphere(root, config, sun);
                if (config.SkyFillIntensity > 0)
                {
                    var fillObject = new GameObject("Higgsfield_SkyFill");
                    fillObject.transform.SetParent(transform, false);
                    fillObject.transform.rotation = config.SkyFillRotation;
                    skyFill = fillObject.AddComponent<Light>();
                    skyFill.type = LightType.Directional;
                    skyFill.color = config.SkyFillColor;
                    skyFill.intensity = config.SkyFillIntensity;
                    skyFill.shadows = LightShadows.None;
                    skyFill.bounceIntensity = 0f;
                    skyFill.cullingMask = config.CullingMask;
                    if (config.SkyFillLightLayers != 0) skyFill.GetUniversalAdditionalLightData().renderingLayers = (uint)config.SkyFillLightLayers;
                }
                if (config.CharacterRimIntensity > 0 || config.CharacterFillIntensity > 0)
                {
                    var rimObject = new GameObject("Higgsfield_CharacterRim");
                    rimObject.transform.SetParent(transform, false);
                    rimObject.AddComponent<Light>();
                    rimLight = rimObject.AddComponent<HiggsfieldRimLight>();
                    rimLight.Configure(config.CharacterRimColor, config.CharacterRimIntensity, config.CharacterRimSpread,
                        config.CharacterFillColor, config.CharacterFillIntensity);
                }
                if (config.InteriorAmbientIntensity > 0 && config.InteriorVolumes.Length > 0) ApplyInteriorAmbient(root, config);
                if (config.ExteriorLightsSkipInteriors && config.InteriorVolumes.Length > 0) TagExteriorRenderers(root, config);
                DynamicGI.UpdateEnvironment();
            }
            catch { Unbind(); throw; }
        }

        private void ApplyRendererAtmosphere(Transform root, Configuration config, Light sun)
        {
            // v0.3.0 r4: per-renderer swaps win over map-wide swaps of the same swatch (they run first, so the map-wide swap
            // no longer finds that slot), e.g. lantern glass keeps its own material where every window pane is swapped.
            foreach (var item in config.RendererOverrides)
            {
                if (!item.SwapFrom) continue;
                var renderer = item.Target;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == item.SwapFrom) { materials[i] = item.SwapTo; continue; }
                    foreach (var extra in item.ExtraSwaps ?? Array.Empty<MaterialSwap>())
                        if (materials[i] == extra.From) { materials[i] = extra.To; break; }
                }
                if (!originalMaterials.ContainsKey(renderer)) originalMaterials.Add(renderer, renderer.sharedMaterials);
                renderer.sharedMaterials = materials;
            }
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
                if (item.AddLightLayers != 0)
                {
                    if (!originalLayers.ContainsKey(renderer)) originalLayers.Add(renderer, renderer.renderingLayerMask);
                    renderer.renderingLayerMask |= (uint)item.AddLightLayers;
                }
            }
            // Night-window panes need their own world box (the imported meshes have no UVs) for the gradient, the
            // curtains and the fake room behind the glass.
            foreach (var renderer in new List<Renderer>(originalMaterials.Keys))
            {
                if (!renderer) continue;
                bool pane = false;
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.HasProperty(PaneMinId)) { pane = true; break; }
                if (!pane) continue;
                var block = BlockFor(renderer);
                Bounds b = renderer.bounds;
                block.SetVector(PaneMinId, b.min);
                block.SetVector(PaneMaxId, b.max);
                renderer.SetPropertyBlock(block);
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

        /// <summary>Current block of a renderer, remembering the original once so Unbind restores it exactly.</summary>
        private MaterialPropertyBlock BlockFor(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (!originalBlocks.ContainsKey(renderer))
            {
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                originalBlocks.Add(renderer, original);
            }
            return block;
        }

        /// <summary>
        /// Indoor bounce (v0.3.0 r3): renderers inside an interior volume get a custom ambient probe mixing the map
        /// night ambient with a warm indoor color, so walls far from the practicals read cream/wood instead of the
        /// blue-violet night ambient. Renderers that cross the volume boundary (exterior walls, roofs) only get the warm
        /// part on the side facing the room: the outer face keeps the night ambient. Visual only; restored on Unbind.
        /// </summary>
        private void ApplyInteriorAmbient(Transform root, Configuration config)
        {
            var volumes = WorldVolumes(root, config);
            const float margin = 0.35f;
            Color warm = config.InteriorAmbientColor * config.InteriorAmbientIntensity;
            float keep = config.InteriorAmbientKeep;
            float ambient = config.AmbientIntensity;
            Color Night(Vector3 d) => (d.y >= 0 ? Color.Lerp(config.AmbientEquator, config.AmbientSky, d.y)
                : Color.Lerp(config.AmbientEquator, config.AmbientGround, -d.y)) * ambient;
            var directions = SphereDirections;
            // Unity's SH directional lobes integrate to ~1.88 over the Fibonacci set: normalize so a uniform radiance of
            // 1 evaluates to 1, like RenderSettings ambient.
            float weight = 4f / directions.Length / SphereGain;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) || renderer.lightProbeUsage == LightProbeUsage.Off) continue;
                Bounds b = renderer.bounds;
                if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < config.InteriorAmbientMinSize) continue;
                int hit = -1;
                for (int i = 0; i < volumes.Length && hit < 0; i++)
                {
                    Bounds grown = volumes[i]; grown.Expand(2f * margin);
                    Vector3 slack = grown.size - b.size;
                    if (grown.Contains(b.center) && slack.x >= 0 && slack.y >= 0 && slack.z >= 0) hit = i;
                }
                if (hit < 0) continue;
                Bounds v = volumes[hit];
                // Axes where the renderer pokes out of the room: its room side points back toward the room center.
                Vector3 inward = Vector3.zero;
                if (b.min.x < v.min.x - 0.02f || b.max.x > v.max.x + 0.02f) inward.x = v.center.x - b.center.x;
                if (b.min.y < v.min.y - 0.02f || b.max.y > v.max.y + 0.02f) inward.y = v.center.y - b.center.y;
                if (b.min.z < v.min.z - 0.02f || b.max.z > v.max.z + 0.02f) inward.z = v.center.z - b.center.z;
                bool boundary = inward.sqrMagnitude > 1e-4f;
                if (boundary) inward.Normalize();
                var sh = new SphericalHarmonicsL2();
                foreach (var d in directions)
                {
                    // Radiance arriving from direction d: warm from the room, night ambient from outside.
                    Color night = Night(d);
                    Color radiance = boundary && Vector3.Dot(d, inward) <= 0 ? night : night * keep + warm;
                    sh.AddDirectionalLight(d, radiance, weight);
                }
                var block = BlockFor(renderer);
                block.CopySHCoefficientArraysFrom(new[] { sh });
                renderer.SetPropertyBlock(block);
                if (!originalProbeUsage.ContainsKey(renderer)) originalProbeUsage.Add(renderer, renderer.lightProbeUsage);
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
            }
        }

        /// <summary>Adds ExteriorRenderingLayer to every lit renderer that is not fully inside an interior volume.</summary>
        private void TagExteriorRenderers(Transform root, Configuration config)
        {
            var volumes = new List<Bounds>(WorldVolumes(root, config));
            // Stacked floors of one building count as one shell (the slab between them is interior too).
            int count = volumes.Count;
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                {
                    Bounds a = volumes[i], c = volumes[j];
                    bool overlapXZ = a.min.x < c.max.x && c.min.x < a.max.x && a.min.z < c.max.z && c.min.z < a.max.z;
                    float gap = Mathf.Max(a.min.y - c.max.y, c.min.y - a.max.y);
                    if (!overlapXZ || gap > 0.4f) continue;
                    Bounds union = a; union.Encapsulate(c);
                    volumes.Add(union);
                }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if ((renderer.renderingLayerMask & 1u) == 0) continue; // Moon-only renderers stay unlit by local lamps.
                Bounds b = renderer.bounds;
                bool inside = false, touches = false;
                foreach (var v in volumes)
                {
                    Bounds grown = v; grown.Expand(0.1f);
                    if (grown.Contains(b.min) && grown.Contains(b.max)) { inside = true; break; }
                    // The shell itself (walls, window frames, sills, eaves within 0.7 m): big terrain and roofs stay open air.
                    Bounds shell = v; shell.Expand(1.4f);
                    if (shell.Contains(b.min) && shell.Contains(b.max)) touches = true;
                }
                if (inside) continue;
                if (!originalLayers.ContainsKey(renderer)) originalLayers.Add(renderer, renderer.renderingLayerMask);
                renderer.renderingLayerMask |= 1u << ExteriorRenderingLayer;
                if (!touches) renderer.renderingLayerMask |= 1u << OpenAirRenderingLayer;
            }
        }

        private static Bounds[] WorldVolumes(Transform root, Configuration config)
        {
            var volumes = new Bounds[Mathf.Min(config.InteriorVolumes.Length, MaximumInteriorVolumes)];
            for (int i = 0; i < volumes.Length; i++)
            {
                Bounds local = config.InteriorVolumes[i];
                var box = new Bounds(root.TransformPoint(local.center), Vector3.zero);
                foreach (var corner in Corners(local)) box.Encapsulate(root.TransformPoint(corner));
                volumes[i] = box;
            }
            return volumes;
        }

        private static float sphereGain;
        private static float SphereGain
        {
            get
            {
                if (sphereGain > 0f) return sphereGain;
                var uniform = new SphericalHarmonicsL2();
                foreach (var d in SphereDirections) uniform.AddDirectionalLight(d, Color.white, 4f / SphereDirections.Length);
                var result = new Color[1];
                uniform.Evaluate(new[] { Vector3.up }, result);
                return sphereGain = Mathf.Max(0.1f, result[0].r);
            }
        }

        private static Vector3[] sphereDirections;
        private static Vector3[] SphereDirections
        {
            get
            {
                if (sphereDirections != null) return sphereDirections;
                const int count = 96; // Fibonacci sphere: even coverage, deterministic.
                var result = new Vector3[count];
                float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
                for (int i = 0; i < count; i++)
                {
                    float y = 1f - (i + 0.5f) * 2f / count;
                    float r = Mathf.Sqrt(1f - y * y);
                    result[i] = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
                }
                return sphereDirections = result;
            }
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
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
            foreach (var item in originalBlocks) if (item.Key) item.Key.SetPropertyBlock(item.Value.isEmpty ? null : item.Value);
            originalBlocks.Clear();
            foreach (var item in originalProbeUsage) if (item.Key) item.Key.lightProbeUsage = item.Value;
            originalProbeUsage.Clear();
            if (sunLayersChanged && primary) primary.GetUniversalAdditionalLightData().renderingLayers = oldSunLayers;
            sunLayersChanged = false;
            SetInteriorVolumes(null, null);
            if (rimLight)
            {
                rimLight.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(rimLight.gameObject); else DestroyImmediate(rimLight.gameObject);
            }
            rimLight = null;
            if (skyFill)
            {
                skyFill.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(skyFill.gameObject); else DestroyImmediate(skyFill.gameObject);
            }
            skyFill = null;
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
