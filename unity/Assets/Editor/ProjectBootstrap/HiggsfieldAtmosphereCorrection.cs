using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Bootstrap;
using LetMeSleep.Presentation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// v0.3.0 atmosphere pass for the five Higgsfield maps, the menu and the lobby ("sala").
    /// Reproducible and idempotent: every value comes from one JSON config (docs/v030/maps/atmosphere-v030.json).
    ///   -executeMethod LetMeSleep.Editor.HiggsfieldAtmosphereCorrection.ApplyFromCommandLine -higgsfieldAtmosphereConfig &lt;abs.json&gt;
    /// Writes: per-map VolumeProfiles (real sub-assets), GradientSky materials, catalog Lighting values
    /// (sky, sun, ambient, fog, volume, far plane, local lights), Color_NNN palette/emission derived from each
    /// map's import recipe, the repaired menu/lobby profile (AlfaGlobalVolume), GraphicsSettings fog stripping
    /// and the MenuCamera URP data. Never touches geometry, colliders, prefabs, navigation or ContentHash.
    /// Supersedes HiggsfieldNightCorrection (its 0.08-0.12 moon / Procedural-sky guard encoded the v0.2.0
    /// "almost black" night; the v0.3.0 style guide asks for navy nights with warm contrasted lights).
    /// Validates the catalog before and after and leaves a JSON receipt outside Assets.
    /// </summary>
    public static class HiggsfieldAtmosphereCorrection
    {
        public const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";
        public const string CatalogGuid = "5a2287ce1168159439a0d2aa8486f5d8";
        public const string ScenePath = "Assets/Scenes/LetMeSleepHiggsfield.unity";
        public const string SkyShaderPath = "Assets/LetMeSleep/Presentation/Runtime/HiggsfieldGradientSky.shader";
        public const string ProfilesRoot = "Assets/LetMeSleep/Presentation/Generated/Profiles/";
        public const string MaterialsRoot = "Assets/LetMeSleep/Presentation/Generated/Materials/";
        public const string MapsRoot = "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/";
        public const string MenuProfilePath = ProfilesRoot + "AlfaGlobalVolume.asset";
        const float PresetFarPlane = 100f; // AlfaPresentationPreset.farPlane when CameraFarPlane == 0.
        static readonly string[] MapIds =
            { "hf-isla-del-laguito-v2", "hf-casa-del-patio-v1", "hf-campamento-pinar-v2", "hf-yate-a-la-deriva-v3", "hf-puerto-del-faro-v1" };

        public sealed class Config
        {
            public int schema_version;
            public string receiptDirectory;
            public GraphicsConfig graphics;
            public CameraConfig menuCamera;
            public ProfileConfig[] profiles;
            public MapConfig[] maps;
            public EmissionEdit[] menuMaterials = Array.Empty<EmissionEdit>();
        }
        // Menu/lobby ("sala") practicals of the private lobby: raise emission above the bloom threshold.
        public sealed class EmissionEdit { public string path; public float[] emission; }
        public const string MenuMaterialsRoot = "Assets/LetMeSleep/Content/Environment/AlfaMaps/Materials/";
        public sealed class GraphicsConfig { public bool customFogStripping; public bool keepLinear; public bool keepExp; public bool keepExp2; }
        public sealed class CameraConfig { public bool renderPostProcessing; public string antialiasing; public string antialiasingQuality; public bool dithering; }
        public sealed class ProfileConfig
        {
            public string path;
            public string tonemapping;
            public BloomConfig bloom;
            public ColorConfig color;
            public float[] shadows, midtones, highlights;
            public float whiteBalanceTemperature, whiteBalanceTint;
            public VignetteConfig vignette;
        }
        public sealed class BloomConfig { public float threshold, intensity, scatter; public float[] tint; public bool highQuality; }
        public sealed class ColorConfig { public float postExposure, contrast, saturation; public float[] colorFilter; }
        public sealed class VignetteConfig { public float intensity, smoothness; public float[] color; }
        public sealed class MapConfig
        {
            public string mapId;
            public string note;
            public string profile;
            public float volumeWeight = 1f;
            public float cameraFarPlane;
            public SkyConfig sky;
            public SunConfig sun;
            public AmbientConfig ambient;
            public FogConfig fog;
            public LightRule[] lightRules = Array.Empty<LightRule>();
            public MaterialEdit[] materials = Array.Empty<MaterialEdit>();
        }
        public sealed class SkyConfig
        {
            public float[] horizon, zenith, ground, glow, disc, star;
            public float[] celestialDirection; // Optional world direction toward the drawn sun/moon.
            public float groundBlend, exponent = 1f, glowTightness = 8f, discSize = 2.5f, starDensity;
        }
        public sealed class SunConfig { public float[] color; public float intensity; public float[] eulerDegrees; }
        public sealed class AmbientConfig { public float[] sky, equator, ground; public float intensity = 1f, reflection = 1f; }
        public sealed class FogConfig { public bool enabled; public string mode; public float start, end, density; }
        public sealed class LightRule
        {
            // Exact AnchorPath, or a prefix ending in '*'. Rules apply in order; each must match at least one anchor.
            public string match;
            public float[] color;
            public float? intensity, range, flicker;
            public string shadows;
            public int? shadowTier;
        }
        public sealed class MaterialEdit
        {
            public int index;
            public string name; // Must equal the recipe sourceName: guards against index drift.
            public float saturation = 1f, value = 1f, hueShift;
            public float[] emission; // Linear HDR, written with SetVector like the importer.
        }

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32
        };

        public static void ApplyFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-higgsfieldAtmosphereConfig");
            Need(index >= 0 && index + 1 < args.Length, "-higgsfieldAtmosphereConfig <absolute json> is required.");
            try { Apply(args[index + 1]); }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void Apply(string configPath)
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Idle Edit Mode required.");
            Need(Path.IsPathRooted(configPath) && File.Exists(configPath), "Absolute existing config required.");
            byte[] input = File.ReadAllBytes(configPath);
            var config = JsonConvert.DeserializeObject<Config>(Encoding.UTF8.GetString(input), Json);
            Need(config != null && config.schema_version == 1, "schema_version 1 required.");
            Need(config.maps != null && config.maps.Select(m => m.mapId).OrderBy(s => s, StringComparer.Ordinal)
                .SequenceEqual(MapIds.OrderBy(s => s, StringComparer.Ordinal)), "Config must list exactly the five canonical maps.");
            Need(!string.IsNullOrWhiteSpace(config.receiptDirectory) && Path.IsPathRooted(config.receiptDirectory) &&
                !Path.GetFullPath(config.receiptDirectory).StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase),
                "Receipts need an absolute directory outside Assets.");
            Directory.CreateDirectory(config.receiptDirectory);

            Need(AssetDatabase.AssetPathToGUID(CatalogPath) == CatalogGuid, "Installed catalog GUID mismatch.");
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(CatalogPath);
            Need(catalog, "Catalog missing.");
            catalog.Validate();
            Need(catalog.Count == 5 && catalog.Entries.Select(e => e.MapId).OrderBy(s => s, StringComparer.Ordinal)
                .SequenceEqual(MapIds.OrderBy(s => s, StringComparer.Ordinal)), "Five-map catalog required.");
            var skyShader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
            Need(skyShader && skyShader.name == "LetMeSleep/Higgsfield/GradientSky" && skyShader.isSupported, "GradientSky shader missing or unsupported.");

            var receipt = new JObject
            {
                ["tool"] = "HiggsfieldAtmosphereCorrection", ["utc"] = DateTime.UtcNow.ToString("o"), ["unityVersion"] = Application.unityVersion,
                ["configPath"] = configPath, ["configSha256"] = Hash(input), ["success"] = false
            };
            string receiptPath = Path.Combine(config.receiptDirectory, "atmosphere-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            JArray invariantBefore = Invariants(catalog);
            receipt["catalogBefore"] = Summaries(catalog);
            receipt["catalogSha256Before"] = HashFile(Disk(CatalogPath));
            try
            {
                // 1) Volume profiles (menu/lobby + one per map), rebuilt with real sub-assets.
                var profiles = new Dictionary<string, VolumeProfile>(StringComparer.Ordinal);
                var profileReport = new JArray();
                Need(config.profiles != null && config.profiles.Any(p => p.path == MenuProfilePath), "Menu/lobby profile (AlfaGlobalVolume) must be configured.");
                foreach (var profileConfig in config.profiles)
                {
                    Need(profileConfig.path != null && profileConfig.path.StartsWith(ProfilesRoot, StringComparison.Ordinal) &&
                        profileConfig.path.EndsWith(".asset", StringComparison.Ordinal) && !profiles.ContainsKey(profileConfig.path),
                        "Profiles must be distinct assets under " + ProfilesRoot);
                    var profile = BuildProfile(profileConfig);
                    profiles.Add(profileConfig.path, profile);
                    profileReport.Add(DescribeProfile(profileConfig.path, profile));
                }
                receipt["profiles"] = profileReport;

                // 2) Maps: sky, lighting, fog, volume, local lights, palette.
                var mapReport = new JArray();
                foreach (var mapConfig in config.maps)
                {
                    var entry = catalog.Resolve(mapConfig.mapId);
                    var lighting = entry.Lighting;
                    VolumeProfile mapProfile = null;
                    Need(mapConfig.profile != null && mapConfig.profile != MenuProfilePath && profiles.TryGetValue(mapConfig.profile, out mapProfile),
                        "Map needs its own configured profile: " + mapConfig.mapId);
                    Need(Finite(mapConfig.volumeWeight) && mapConfig.volumeWeight > 0 && mapConfig.volumeWeight <= 1, "Volume weight 0..1.");
                    Need(mapConfig.cameraFarPlane == 0 || (mapConfig.cameraFarPlane >= 10 && mapConfig.cameraFarPlane <= 1000), "Far plane 0 or 10..1000.");
                    Need(mapConfig.sky != null && mapConfig.sun != null && mapConfig.ambient != null && mapConfig.fog != null, "Sky, sun, ambient and fog are required.");

                    var sky = BuildSky(mapConfig.mapId, mapConfig.sky, skyShader);
                    Color horizon = Rgb(mapConfig.sky.horizon);
                    lighting.Skybox = sky;
                    lighting.SunColor = Rgb(mapConfig.sun.color);
                    lighting.SunUnityIntensity = NonNegative(mapConfig.sun.intensity);
                    if (mapConfig.sun.eulerDegrees != null)
                    {
                        Need(mapConfig.sun.eulerDegrees.Length == 3 && mapConfig.sun.eulerDegrees.All(Finite), "Sun euler x,y,z required.");
                        lighting.SunWorldRotation = Quaternion.Euler(mapConfig.sun.eulerDegrees[0], mapConfig.sun.eulerDegrees[1], mapConfig.sun.eulerDegrees[2]);
                    }
                    lighting.AmbientSky = Rgb(mapConfig.ambient.sky);
                    lighting.AmbientEquator = Rgb(mapConfig.ambient.equator);
                    lighting.AmbientGround = Rgb(mapConfig.ambient.ground);
                    lighting.AmbientIntensity = NonNegative(mapConfig.ambient.intensity);
                    lighting.ReflectionIntensity = NonNegative(mapConfig.ambient.reflection);
                    lighting.FogEnabled = mapConfig.fog.enabled;
                    lighting.FogMode = (FogMode)Enum.Parse(typeof(FogMode), mapConfig.fog.mode, false);
                    lighting.FogColor = horizon; // Horizon of the sky and fog are identical by contract.
                    lighting.FogStart = NonNegative(mapConfig.fog.start);
                    lighting.FogEnd = NonNegative(mapConfig.fog.end);
                    lighting.FogDensity = NonNegative(mapConfig.fog.density);
                    lighting.VolumeProfile = mapProfile;
                    lighting.VolumeWeight = mapConfig.volumeWeight;
                    entry.CameraFarPlane = mapConfig.cameraFarPlane;
                    float effectiveFar = mapConfig.cameraFarPlane > 0 ? mapConfig.cameraFarPlane : PresetFarPlane;
                    if (lighting.FogEnabled && lighting.FogMode == FogMode.Linear)
                        Need(lighting.FogEnd <= effectiveFar && lighting.FogEnd > lighting.FogStart,
                            "Linear fog must close before the effective far plane: " + mapConfig.mapId);

                    ApplyLightRules(entry, mapConfig.lightRules ?? Array.Empty<LightRule>());
                    var materialReport = ApplyPalette(mapConfig.mapId, mapConfig.materials ?? Array.Empty<MaterialEdit>());
                    CheckShadowBudget(entry);
                    mapReport.Add(new JObject
                    {
                        ["mapId"] = mapConfig.mapId, ["note"] = mapConfig.note, ["skyPath"] = AssetDatabase.GetAssetPath(sky),
                        ["profilePath"] = mapConfig.profile, ["effectiveFarPlane"] = effectiveFar, ["materials"] = materialReport,
                        ["shadowedLocalLights"] = entry.LocalLights.Count(l => l.Settings.Shadows != LightShadows.None),
                        ["flickeringLocalLights"] = entry.LocalLights.Count(l => l.Settings.Flicker > 0)
                    });
                }
                receipt["maps"] = mapReport;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                catalog.Validate();
                Need(JToken.DeepEquals(invariantBefore, Invariants(catalog)), "Catalog identity fields (ids, prefabs, anchors, suppression) changed.");

                // 2b) Menu/lobby practical emission.
                var menuMaterialReport = new JArray();
                foreach (var edit in config.menuMaterials ?? Array.Empty<EmissionEdit>())
                {
                    Need(edit.path != null && edit.path.StartsWith(MenuMaterialsRoot, StringComparison.Ordinal) && edit.path.EndsWith(".mat", StringComparison.Ordinal),
                        "Menu materials must live under " + MenuMaterialsRoot);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(edit.path);
                    Need(material && material.HasProperty("_EmissionColor"), "Emissive menu material missing: " + edit.path);
                    Need(edit.emission != null && edit.emission.Length == 3 && edit.emission.All(x => Finite(x) && x >= 0 && x <= 16), "Emission linear RGB 0..16.");
                    Vector4 before = material.GetVector("_EmissionColor");
                    material.SetVector("_EmissionColor", new Vector4(edit.emission[0], edit.emission[1], edit.emission[2], 1));
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    menuMaterialReport.Add(new JObject { ["path"] = edit.path, ["emissionBefore"] = VectorText(before), ["emissionAfter"] = VectorText(material.GetVector("_EmissionColor")) });
                }
                receipt["menuMaterials"] = menuMaterialReport;

                // 3) Build-time fog variants: Automatic stripping drops FOG_* because the saved scene has fog off.
                receipt["graphics"] = ApplyGraphics(config.graphics);
                // 4) Menu/lobby camera (the lobby "sala" reuses MenuCamera as its local camera).
                receipt["menuCamera"] = ApplyMenuCamera(config.menuCamera, catalog);

                AssetDatabase.SaveAssets();
                receipt["catalogAfter"] = Summaries(catalog);
                receipt["catalogSha256After"] = HashFile(Disk(CatalogPath));
                receipt["files"] = FileHashes(profiles.Keys.Concat(config.maps.Select(m => MaterialsRoot + m.mapId + "-GradientSky.mat"))
                    .Concat(new[] { CatalogPath, ScenePath, "ProjectSettings/GraphicsSettings.asset" }));
                receipt["success"] = true;
                Debug.Log("[HiggsfieldAtmosphereCorrection] Applied " + configPath + " -> " + receiptPath);
            }
            catch (Exception error)
            {
                receipt["error"] = error.ToString();
                throw;
            }
            finally
            {
                File.WriteAllText(receiptPath, receipt.ToString(Formatting.Indented));
            }
        }

        // ---------- Volume profiles ----------
        static VolumeProfile BuildProfile(ProfileConfig c)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(c.path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = Path.GetFileNameWithoutExtension(c.path);
                AssetDatabase.CreateAsset(profile, c.path);
            }
            // Reuse existing override sub-assets by type (stable local IDs, so reruns do not churn the YAML),
            // reset every override flag, and drop dangling {fileID: 0} entries left by the old GetOrAdd bug.
            var existing = AssetDatabase.LoadAllAssetsAtPath(c.path).OfType<VolumeComponent>().ToList();
            var keep = new List<VolumeComponent>();
            T Add<T>(VolumeProfile owner) where T : VolumeComponent
            {
                var component = existing.OfType<T>().FirstOrDefault();
                if (!component)
                {
                    component = ScriptableObject.CreateInstance<T>();
                    component.name = typeof(T).Name;
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, owner);
                }
                foreach (var parameter in component.parameters) parameter.overrideState = false;
                component.active = true;
                keep.Add(component);
                return component;
            }

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override((TonemappingMode)Enum.Parse(typeof(TonemappingMode), c.tonemapping, false));

            Need(c.bloom != null, "Bloom required: " + c.path);
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(Range(c.bloom.threshold, 0, 4));
            bloom.intensity.Override(Range(c.bloom.intensity, 0, 3));
            bloom.scatter.Override(Range(c.bloom.scatter, 0, 1));
            bloom.tint.Override(Rgb(c.bloom.tint ?? new[] { 1f, 1f, 1f }));
            bloom.highQualityFiltering.Override(c.bloom.highQuality);

            Need(c.color != null, "Color adjustments required: " + c.path);
            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(Range(c.color.postExposure, -2, 2));
            color.contrast.Override(Range(c.color.contrast, -30, 40));
            color.saturation.Override(Range(c.color.saturation, -30, 40));
            color.colorFilter.Override(Rgb(c.color.colorFilter ?? new[] { 1f, 1f, 1f }));

            if (c.shadows != null || c.midtones != null || c.highlights != null)
            {
                var smh = Add<ShadowsMidtonesHighlights>(profile);
                smh.shadows.Override(Vector4Of(c.shadows));
                smh.midtones.Override(Vector4Of(c.midtones));
                smh.highlights.Override(Vector4Of(c.highlights));
            }
            if (c.whiteBalanceTemperature != 0 || c.whiteBalanceTint != 0)
            {
                var white = Add<WhiteBalance>(profile);
                white.temperature.Override(Range(c.whiteBalanceTemperature, -30, 30));
                white.tint.Override(Range(c.whiteBalanceTint, -30, 30));
            }
            Need(c.vignette != null, "Vignette required: " + c.path);
            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(Range(c.vignette.intensity, 0, 0.45f));
            vignette.smoothness.Override(Range(c.vignette.smoothness, 0.01f, 1));
            vignette.color.Override(Rgb(c.vignette.color ?? new[] { 0f, 0f, 0f }));

            foreach (var stale in existing.Where(x => !keep.Contains(x)))
                UnityEngine.Object.DestroyImmediate(stale, true);
            profile.components.Clear();
            profile.components.AddRange(keep);
            foreach (var component in keep) EditorUtility.SetDirty(component);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            var saved = AssetDatabase.LoadAllAssetsAtPath(c.path).OfType<VolumeComponent>().ToArray();
            Need(profile.components.Count > 0 && profile.components.All(x => x && AssetDatabase.Contains(x) &&
                    !AssetDatabase.IsMainAsset(x) && AssetDatabase.GetAssetPath(x) == c.path) && saved.Length == profile.components.Count,
                "Every volume component must be a saved sub-asset: " + c.path);
            return profile;
        }

        static JObject DescribeProfile(string path, VolumeProfile profile)
        {
            var components = new JArray();
            foreach (var component in profile.components)
            {
                var parameters = new JObject();
                for (int i = 0; i < component.parameters.Count; i++)
                    if (component.parameters[i].overrideState)
                        parameters[component.parameters[i].GetType().Name + "#" + i] = component.parameters[i].ToString();
                components.Add(new JObject { ["type"] = component.GetType().Name, ["overrides"] = parameters });
            }
            return new JObject { ["path"] = path, ["guid"] = AssetDatabase.AssetPathToGUID(path), ["components"] = components };
        }

        // ---------- Sky ----------
        static Material BuildSky(string mapId, SkyConfig c, Shader shader)
        {
            string path = MaterialsRoot + mapId + "-GradientSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = mapId + "-GradientSky" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_HorizonColor", Rgb(c.horizon));
            material.SetColor("_ZenithColor", Rgb(c.zenith));
            material.SetColor("_GroundColor", c.ground != null ? Rgb(c.ground) : Rgb(c.horizon));
            material.SetFloat("_GroundBlend", Range(c.groundBlend, 0, 1));
            material.SetFloat("_HorizonExponent", Range(c.exponent, .25f, 4));
            material.SetColor("_GlowColor", c.glow != null ? Hdr(c.glow) : Color.black);
            material.SetFloat("_GlowTightness", Range(c.glowTightness, 1, 128));
            material.SetColor("_DiscColor", c.disc != null ? Hdr(c.disc) : Color.black);
            material.SetFloat("_DiscSize", Range(c.discSize, .1f, 12));
            material.SetFloat("_StarDensity", Range(c.starDensity, 0, 1));
            material.SetColor("_StarColor", c.star != null ? Hdr(c.star) : Color.white);
            if (c.celestialDirection != null)
            {
                Need(c.celestialDirection.Length == 3 && c.celestialDirection.All(Finite), "celestialDirection x,y,z required.");
                var direction = new Vector3(c.celestialDirection[0], c.celestialDirection[1], c.celestialDirection[2]);
                Need(direction.sqrMagnitude > 1e-4f && direction.normalized.y > 0.05f, "celestialDirection must point above the horizon.");
                direction.Normalize();
                material.SetVector("_CelestialDirection", new Vector4(direction.x, direction.y, direction.z, 1));
            }
            else material.SetVector("_CelestialDirection", new Vector4(0, 1, 0, 0));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // ---------- Local lights ----------
        static void ApplyLightRules(HiggsfieldMapCatalog.Entry entry, LightRule[] rules)
        {
            foreach (var rule in rules)
            {
                Need(!string.IsNullOrWhiteSpace(rule.match), "Light rule needs match.");
                bool prefix = rule.match.EndsWith("*", StringComparison.Ordinal);
                string key = prefix ? rule.match.Substring(0, rule.match.Length - 1) : rule.match;
                var matched = entry.LocalLights.Where(l => prefix
                    ? l.AnchorPath.StartsWith(key, StringComparison.Ordinal)
                    : string.Equals(l.AnchorPath, key, StringComparison.Ordinal)).ToArray();
                Need(matched.Length > 0, "Light rule matched no anchor: " + entry.MapId + " " + rule.match);
                foreach (var binding in matched)
                {
                    var s = binding.Settings;
                    if (rule.color != null) s.Color = Rgb(rule.color);
                    if (rule.intensity.HasValue) s.UnityIntensity = Range(rule.intensity.Value, 0, 20);
                    if (rule.range.HasValue) s.Range = Range(rule.range.Value, 0.5f, 30);
                    if (rule.flicker.HasValue) s.Flicker = Range(rule.flicker.Value, 0, HiggsfieldLightFlicker.MaximumAmplitude);
                    if (rule.shadows != null) s.Shadows = (LightShadows)Enum.Parse(typeof(LightShadows), rule.shadows, false);
                    if (rule.shadowTier.HasValue)
                    {
                        Need(HiggsfieldMapLighting.ValidShadowTier(rule.shadowTier.Value), "Shadow tier 0..2.");
                        s.ShadowResolutionTier = rule.shadowTier.Value;
                    }
                }
            }
        }

        // Point lights use 6 atlas slices, spots 1. Assume every shadowed light of the map can be visible at once.
        static void CheckShadowBudget(HiggsfieldMapCatalog.Entry entry)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Need(asset, "URP asset required.");
            long atlas = (long)asset.additionalLightsShadowmapResolution * asset.additionalLightsShadowmapResolution;
            long used = 0;
            foreach (var light in entry.LocalLights.Select(l => l.Settings).Where(s => s.Shadows != LightShadows.None))
            {
                int resolution = light.ShadowResolutionTier == 0 ? asset.additionalLightsShadowResolutionTierLow
                    : light.ShadowResolutionTier == 1 ? asset.additionalLightsShadowResolutionTierMedium
                    : asset.additionalLightsShadowResolutionTierHigh;
                used += (light.Type == LightType.Point ? 6L : 1L) * resolution * resolution;
            }
            Need(used <= atlas, "Local shadow requests exceed the additional-light atlas (URP would downscale): " + entry.MapId +
                " " + used + " > " + atlas);
        }

        // ---------- Palette ----------
        static JArray ApplyPalette(string mapId, MaterialEdit[] edits)
        {
            string recipePath = MapsRoot + mapId + "/Data/import-recipe.json";
            var recipe = JObject.Parse(File.ReadAllText(Disk(recipePath)));
            var swatches = (JArray)recipe["materials"];
            Need(swatches != null && swatches.Count > 0, "Recipe materials missing: " + mapId);
            Need(edits.Select(e => e.index).Distinct().Count() == edits.Length, "Duplicate material edits: " + mapId);
            var report = new JArray();
            for (int i = 0; i < swatches.Count; i++)
            {
                string path = MapsRoot + mapId + "/Materials/Color_" + i.ToString("000") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Need(material && material.shader && material.shader.name == "Universal Render Pipeline/Lit", "Flat URP/Lit swatch required: " + path);
                var swatch = (JObject)swatches[i];
                var rgb = swatch["rgb"].ToObject<float[]>();
                var baseline = new Color(rgb[0], rgb[1], rgb[2], 1);
                if ((string)swatch["colorSpace"] == "linear") baseline = baseline.gamma;
                Color before = material.GetColor("_BaseColor");
                Vector4 emissionBefore = material.GetVector("_EmissionColor");
                var edit = edits.FirstOrDefault(e => e.index == i);
                Color after = baseline;
                if (edit != null)
                {
                    Need(edit.name == (string)swatch["sourceName"], "Material edit name/index mismatch: " + mapId + " " + i + " " + edit.name);
                    Color.RGBToHSV(baseline, out float h, out float s, out float v);
                    h = Mathf.Repeat(h + Range(edit.hueShift, -60, 60) / 360f, 1f);
                    s = Mathf.Clamp01(s * Range(edit.saturation, 0.5f, 2.5f));
                    v = Mathf.Clamp01(v * Range(edit.value, 0.5f, 1.5f));
                    after = Color.HSVToRGB(h, s, v);
                }
                after.a = before.a; // Opacity (YATE_Glass BLEND) is not a palette decision.
                material.SetColor("_BaseColor", after);
                if (material.HasProperty("_Color")) material.SetColor("_Color", after);
                if (edit?.emission != null)
                {
                    Need(edit.emission.Length == 3 && edit.emission.All(x => Finite(x) && x >= 0 && x <= 16), "Emission linear RGB 0..16.");
                    material.SetVector("_EmissionColor", new Vector4(edit.emission[0], edit.emission[1], edit.emission[2], 1));
                    bool on = edit.emission.Any(x => x > 0);
                    if (on)
                    {
                        material.EnableKeyword("_EMISSION");
                        var flags = material.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.globalIlluminationFlags = (flags & MaterialGlobalIlluminationFlags.AnyEmissive) == 0 ? flags | MaterialGlobalIlluminationFlags.BakedEmissive : flags;
                    }
                    else
                    {
                        material.DisableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                }
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                bool changed = !Approximately(before, after) || !Approximately(emissionBefore, material.GetVector("_EmissionColor"));
                bool unconfiguredDrift = edit == null && !Approximately(before, baseline);
                if (edit != null || changed)
                    report.Add(new JObject
                    {
                        ["index"] = i, ["name"] = (string)swatch["sourceName"], ["baseline"] = ColorText(baseline),
                        ["before"] = ColorText(before), ["after"] = ColorText(after),
                        ["emissionBefore"] = VectorText(emissionBefore), ["emissionAfter"] = VectorText(material.GetVector("_EmissionColor")),
                        ["restoredToRecipe"] = unconfiguredDrift
                    });
            }
            return report;
        }

        // ---------- Graphics settings ----------
        static JObject ApplyGraphics(GraphicsConfig c)
        {
            Need(c != null && c.customFogStripping && c.keepLinear && c.keepExp && c.keepExp2, "Keep all fog variants in custom mode.");
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            Need(settings, "GraphicsSettings asset missing.");
            var serialized = new SerializedObject(settings);
            var report = new JObject { ["before"] = FogStripping(serialized) };
            serialized.FindProperty("m_FogStripping").intValue = 1; // Custom
            serialized.FindProperty("m_FogKeepLinear").boolValue = true;
            serialized.FindProperty("m_FogKeepExp").boolValue = true;
            serialized.FindProperty("m_FogKeepExp2").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            serialized.Update();
            report["after"] = FogStripping(serialized);
            Need(serialized.FindProperty("m_FogStripping").intValue == 1, "Fog stripping did not change.");
            return report;
        }

        static JObject FogStripping(SerializedObject s) => new JObject
        {
            ["m_FogStripping"] = s.FindProperty("m_FogStripping").intValue,
            ["m_FogKeepLinear"] = s.FindProperty("m_FogKeepLinear").boolValue,
            ["m_FogKeepExp"] = s.FindProperty("m_FogKeepExp").boolValue,
            ["m_FogKeepExp2"] = s.FindProperty("m_FogKeepExp2").boolValue
        };

        // ---------- Menu camera ----------
        static JObject ApplyMenuCamera(CameraConfig c, HiggsfieldMapCatalog catalog)
        {
            Need(c != null, "menuCamera config required.");
            Scene previous = SceneManager.GetActiveScene();
            bool opened = false;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, string.IsNullOrEmpty(previous.path) ? OpenSceneMode.Single : OpenSceneMode.Additive);
                opened = !string.IsNullOrEmpty(previous.path);
            }
            try
            {
                Need(!scene.isDirty, "Scene must be saved before the camera change.");
                var apps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AlfaApplication>(true)).ToArray();
                Need(apps.Length == 1 && apps[0].HiggsfieldMaps == catalog, "Expected one AlfaApplication bound to the installed catalog.");
                var camera = apps[0].MenuCamera;
                Need(camera && camera.gameObject.scene == scene, "MenuCamera missing.");
                var data = camera.GetUniversalAdditionalCameraData();
                var report = new JObject { ["before"] = CameraText(data) };
                data.renderPostProcessing = c.renderPostProcessing;
                data.antialiasing = (AntialiasingMode)Enum.Parse(typeof(AntialiasingMode), c.antialiasing, false);
                data.antialiasingQuality = (AntialiasingQuality)Enum.Parse(typeof(AntialiasingQuality), c.antialiasingQuality, false);
                data.dithering = c.dithering;
                EditorUtility.SetDirty(data);
                EditorSceneManager.MarkSceneDirty(scene);
                Need(EditorSceneManager.SaveScene(scene), "Scene save failed.");
                report["after"] = CameraText(data);
                return report;
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static string CameraText(UniversalAdditionalCameraData d) =>
            "renderPostProcessing=" + d.renderPostProcessing + ";antialiasing=" + d.antialiasing + ";quality=" + d.antialiasingQuality + ";dithering=" + d.dithering;

        // ---------- Receipts and invariants ----------
        static JArray Invariants(HiggsfieldMapCatalog catalog)
        {
            var result = new JArray();
            foreach (var entry in catalog.Entries)
                result.Add(new JObject
                {
                    ["mapId"] = entry.MapId, ["displayName"] = entry.DisplayName,
                    ["prefabGuid"] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Prefab)),
                    ["contentHash"] = entry.Prefab.ContentHash, ["lightingMap"] = entry.Lighting.MapId.ToString(),
                    ["cullingMask"] = entry.Lighting.CullingMask, ["sunShadows"] = entry.Lighting.SunShadows.ToString(),
                    ["anchors"] = new JArray(entry.LocalLights.Select(l => l.AnchorPath + "|" + l.Settings.Type)),
                    ["suppress"] = new JArray(entry.SuppressLightPaths)
                });
            return result;
        }

        static JArray Summaries(HiggsfieldMapCatalog catalog)
        {
            var result = new JArray();
            foreach (var entry in catalog.Entries)
            {
                var l = entry.Lighting;
                result.Add(new JObject
                {
                    ["mapId"] = entry.MapId, ["cameraFarPlane"] = entry.CameraFarPlane,
                    ["skybox"] = l.Skybox ? AssetDatabase.GetAssetPath(l.Skybox) : null,
                    ["sun"] = ColorText(l.SunColor) + " x" + F(l.SunUnityIntensity), ["sunEuler"] = VectorText(l.SunWorldRotation.eulerAngles),
                    ["ambient"] = ColorText(l.AmbientSky) + " / " + ColorText(l.AmbientEquator) + " / " + ColorText(l.AmbientGround),
                    ["fog"] = l.FogEnabled + " " + l.FogMode + " " + F(l.FogStart) + ".." + F(l.FogEnd) + " d" + F(l.FogDensity) + " " + ColorText(l.FogColor),
                    ["volume"] = l.VolumeProfile ? AssetDatabase.GetAssetPath(l.VolumeProfile) : null, ["volumeWeight"] = l.VolumeWeight,
                    ["localLights"] = new JArray(entry.LocalLights.Select(b => b.AnchorPath + " " + ColorText(b.Settings.Color) + " i" + F(b.Settings.UnityIntensity) +
                        " r" + F(b.Settings.Range) + " " + b.Settings.Shadows + " t" + b.Settings.ShadowResolutionTier + " f" + F(b.Settings.Flicker)))
                });
            }
            return result;
        }

        static JObject FileHashes(IEnumerable<string> paths)
        {
            var result = new JObject();
            foreach (var path in paths.Distinct())
            {
                string disk = Disk(path);
                result[path] = File.Exists(disk) ? HashFile(disk) : "missing";
                if (File.Exists(disk + ".meta")) result[path + ".meta"] = HashFile(disk + ".meta");
            }
            return result;
        }

        // ---------- Helpers ----------
        static string Disk(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static string HashFile(string path) => Hash(File.ReadAllBytes(path));
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static float NonNegative(float f) { Need(Finite(f) && f >= 0, "Finite non-negative value required."); return f; }
        static float Range(float f, float min, float max) { Need(Finite(f) && f >= min && f <= max, "Value " + f + " outside " + min + ".." + max); return f; }
        static Color Rgb(float[] v) { Need(v != null && v.Length == 3 && v.All(x => Finite(x) && x >= 0 && x <= 1), "RGB [0,1] required."); return new Color(v[0], v[1], v[2], 1); }
        static Color Hdr(float[] v) { Need(v != null && v.Length == 3 && v.All(x => Finite(x) && x >= 0 && x <= 16), "HDR RGB 0..16 required."); return new Color(v[0], v[1], v[2], 1); }
        static Vector4 Vector4Of(float[] v)
        {
            if (v == null) return new Vector4(1, 1, 1, 0);
            Need(v.Length == 4 && v.All(Finite) && v.Take(3).All(x => x >= 0 && x <= 2) && v[3] >= -1 && v[3] <= 1, "SMH vector r,g,b,offset required.");
            return new Vector4(v[0], v[1], v[2], v[3]);
        }
        static bool Approximately(Color a, Color b) => Mathf.Abs(a.r - b.r) < 1e-4f && Mathf.Abs(a.g - b.g) < 1e-4f && Mathf.Abs(a.b - b.b) < 1e-4f;
        static bool Approximately(Vector4 a, Vector4 b) => (a - b).sqrMagnitude < 1e-8f;
        static string ColorText(Color c) => F(c.r) + "," + F(c.g) + "," + F(c.b);
        static string VectorText(Vector4 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        static string F(float value) => value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
