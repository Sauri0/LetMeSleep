using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        }

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
        }

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
            }
        }

        public void Bind(Transform root, Light sun, Volume globalVolume, Configuration config, IEnumerable<Light> previousLights)
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
                foreach (var source in config.LocalLights)
                {
                    var go = new GameObject("Higgsfield_LocalLight");
                    go.SetActive(false); go.transform.SetParent(source.Anchor, false);
                    var light = go.AddComponent<Light>(); spawned.Add(light);
                    light.type = source.Type; light.color = source.Color; light.intensity = source.UnityIntensity;
                    light.range = source.Range; light.spotAngle = source.SpotAngle; light.innerSpotAngle = source.InnerSpotAngle;
                    light.shadows = source.Shadows; light.cullingMask = config.CullingMask; light.bounceIntensity = 0;
                    go.SetActive(true);
                }
                DynamicGI.UpdateEnvironment();
            }
            catch { Unbind(); throw; }
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
        private static void Nonnegative(float value) { if (!Finite(value) || value < 0) throw new ArgumentException("Explicit finite nonnegative Unity intensity/environment values required."); }
        private static void CheckColor(Color value) { Nonnegative(value.r); Nonnegative(value.g); Nonnegative(value.b); Nonnegative(value.a); }
    }
}
