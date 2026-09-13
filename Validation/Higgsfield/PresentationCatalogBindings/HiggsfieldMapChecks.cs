using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
using LetMeSleep.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exact entrypoint required by the existing HiggsfieldExternalFixture adapter.
public static class HiggsfieldMapChecks
{
    static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        MissingMemberHandling = MissingMemberHandling.Error,
        CheckAdditionalContent = true,
        MaxDepth = 32,
        Culture = CultureInfo.InvariantCulture
    };
#pragma warning disable CS0649
    [Serializable] public sealed class Request
    {
        public int schema_version;
        public string catalogAssetPath, expectedCatalogGuid, expectedCatalogSha256;
        public string rigPrefabPath;
        public string[] mapIds;
    }
    [Serializable] sealed class MapResult
    {
        public string mapId;
        public int localLights;
        public bool bind, rebind, unbind, destroyedWithoutResidue;
    }
    [Serializable] sealed class Report
    {
        public bool success, assetsUnchanged, originalSceneStateRestored;
        public string error, unityVersion, configSha256, fixtureSha256, catalogGuid, catalogSha256;
        public List<MapResult> maps = new List<MapResult>();
        public string scope = "Native Edit Mode binding/rebinding/restoration using the real rig prefab; no Play Mode, deferred destruction, render, navigation, UI, FPS or WAN evidence.";
    }
#pragma warning restore CS0649
    static readonly string[] EnvironmentFields = {
        "sun", "skybox", "ambientMode", "ambientSkyColor", "ambientEquatorColor", "ambientGroundColor",
        "ambientIntensity", "reflectionIntensity", "fog", "fogMode", "fogColor", "fogStartDistance", "fogEndDistance", "fogDensity", "ambientProbe"
    };

    public static string Run(string configPath, string outputPath)
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Idle Edit Mode required.");
        Check(Path.IsPathRooted(configPath) && File.Exists(configPath) && new FileInfo(configPath).Length <= 1024 * 1024, "Absolute config up to 1 MiB required.");
        Check(Path.IsPathRooted(outputPath) && Directory.Exists(Path.GetDirectoryName(outputPath)), "Existing external output directory required.");
        outputPath = Path.GetFullPath(outputPath);
        Check(!outputPath.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Report must be outside Assets.");
        Check(!File.Exists(outputPath) && !Directory.Exists(outputPath), "Report must be new.");
        byte[] config = File.ReadAllBytes(configPath);
        var request = JsonConvert.DeserializeObject<Request>(Encoding.UTF8.GetString(config), JsonSettings);
        Check(request != null && request.schema_version == 1 && request.mapIds != null && request.mapIds.Length == 5 &&
            new HashSet<string>(request.mapIds).Count == 5, "Five distinct explicit IDs required.");
        var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(request.catalogAssetPath);
        Check(catalog && !EditorUtility.IsDirty(catalog), "Saved catalog asset required.");
        catalog.Validate(); Check(catalog.Entries.Count == 5, "Final catalog must have five entries.");
        Check(catalog.Entries.Select(entry => entry.Lighting.MapId).Distinct().Count() == 5, "Five map categories required.");
        Check(AssetDatabase.AssetPathToGUID(request.catalogAssetPath) == request.expectedCatalogGuid &&
            HashFile(AssetFile(request.catalogAssetPath)) == request.expectedCatalogSha256, "Catalog identity mismatch.");
        foreach (string id in request.mapIds) Check(catalog.Resolve(id) != null, "Missing map: " + id);
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.rigPrefabPath);
        Check(rigPrefab && rigPrefab.GetComponentsInChildren<AlfaLightingRig>(true).Length == 1, "Explicit real rig prefab required.");
        var assets = new Dictionary<string, string>();
        TrackAsset(assets, request.catalogAssetPath); TrackAsset(assets, request.rigPrefabPath);
        foreach (var entry in catalog.Entries)
        {
            Check(entry.Lighting.Skybox, "Final map skybox must be assigned: " + entry.MapId);
            TrackAsset(assets, AssetDatabase.GetAssetPath(entry.Prefab));
            if (entry.Lighting.Skybox) TrackAsset(assets, AssetDatabase.GetAssetPath(entry.Lighting.Skybox));
            if (entry.Lighting.VolumeProfile) TrackAsset(assets, AssetDatabase.GetAssetPath(entry.Lighting.VolumeProfile));
        }
        var report = new Report { unityVersion = Application.unityVersion, configSha256 = Hash(config),
            fixtureSha256 = HashFile(typeof(HiggsfieldMapChecks).Assembly.Location),
            catalogGuid = request.expectedCatalogGuid, catalogSha256 = request.expectedCatalogSha256 };
        Scene originalScene = SceneManager.GetActiveScene(), testScene = default;
        Check(originalScene.IsValid() && originalScene.isLoaded, "Loaded original active scene required.");
        bool originalDirty = originalScene.isDirty;
        var originalEnvironment = CaptureEnvironment();
        GameObject rigObject = null, mapObject = null;
        AlfaLightingRig rig = null;
        using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            Write(stream, report);
            try
            {
                testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Check(SceneManager.SetActiveScene(testScene), "Cannot activate test scene.");
                rigObject = Object.Instantiate(rigPrefab);
                SceneManager.MoveGameObjectToScene(rigObject, testScene);
                rig = rigObject.GetComponentInChildren<AlfaLightingRig>(true);
                Check(rig && rig.Moon && rig.GlobalVolume, "Real rig needs its primary light and Volume references.");
                rig.ApplyPreset(); // Explicit in Edit Mode, where Awake is not relied on.
                foreach (string id in request.mapIds)
                {
                    var result = new MapResult { mapId = id }; report.maps.Add(result);
                    var entry = catalog.Resolve(id);
                    mapObject = Object.Instantiate(entry.Prefab.gameObject);
                    SceneManager.MoveGameObjectToScene(mapObject, testScene);
                    var definition = mapObject.GetComponent<EnvironmentMapDefinition>();
                    var configLighting = catalog.ResolveLighting(id, definition);
                    var baseline = CaptureEnvironment();
                    var lightBaseline = rigObject.GetComponentsInChildren<Light>(true).Concat(mapObject.GetComponentsInChildren<Light>(true))
                        .Distinct().ToDictionary(light => light, light => new LightState(light));
                    var volume = rig.GlobalVolume;
                    var profile = volume.sharedProfile; float weight = volume.weight; bool volumeEnabled = volume.enabled;
                    var originalMapLights = mapObject.GetComponentsInChildren<Light>(true);
                    rig.BindHiggsfield(mapObject.transform, configLighting);
                    Light[] first = VerifyBound(rig, mapObject, configLighting, originalMapLights);
                    result.bind = true; result.localLights = first.Length;
                    rig.BindHiggsfield(mapObject.transform, configLighting);
                    Check(first.All(light => !light), "Rebind retained first light objects: " + id);
                    Light[] second = VerifyBound(rig, mapObject, configLighting, originalMapLights);
                    result.rebind = true;
                    rig.UnbindHiggsfield();
                    Check(!rig.IsHiggsfieldBound && second.All(light => !light), "Unbind retained local lights: " + id);
                    var binding = rig.GetComponent<HiggsfieldMapLighting>();
                    Check(binding && !binding.MapRoot && binding.LocalLightCount == 0, "Binding retained map or lights.");
                    EqualEnvironment(baseline);
                    foreach (var state in lightBaseline) state.Value.Verify(state.Key);
                    Check(volume.sharedProfile == profile && volume.enabled == volumeEnabled, "Volume profile/enabled not restored.");
                    Equal(volume.weight, weight, "Volume weight");
                    Check(mapObject.GetComponentsInChildren<Light>(true).Length == originalMapLights.Length, "Residual map lights.");
                    result.unbind = true;
                    var descendants = mapObject.GetComponentsInChildren<Transform>(true);
                    Object.DestroyImmediate(mapObject); mapObject = null;
                    Check(descendants.All(item => !item), "Map destruction left descendants.");
                    Check(testScene.GetRootGameObjects().Length == 1 && testScene.GetRootGameObjects()[0] == rigObject, "Residual scene roots.");
                    result.destroyedWithoutResidue = true;
                }
                foreach (var asset in assets) Check(HashFile(asset.Key) == asset.Value, "Asset modified: " + asset.Key);
                report.assetsUnchanged = true;
                report.success = report.maps.Count == 5 && report.maps.All(item => item.destroyedWithoutResidue);
            }
            catch (Exception error) { report.success = false; report.error = error.ToString(); throw; }
            finally
            {
                try
                {
                    if (rig) rig.UnbindHiggsfield();
                    if (mapObject) Object.DestroyImmediate(mapObject);
                    if (rigObject) Object.DestroyImmediate(rigObject);
                }
                catch (Exception error) { report.success = false; report.error += "\nCleanup: " + error; }
                finally
                {
                    try
                    {
                        if (testScene.IsValid() && testScene.isLoaded)
                            Check(EditorSceneManager.CloseScene(testScene, true), "Cannot close test scene.");
                        Check(SceneManager.SetActiveScene(originalScene), "Cannot restore original active scene.");
                        try { EqualEnvironment(originalEnvironment); }
                        catch
                        {
                            // Emergency restoration is not counted as a successful automatic restoration check.
                            foreach (var value in originalEnvironment) typeof(RenderSettings).GetProperty(value.Key).SetValue(null, value.Value);
                            throw;
                        }
                        Check(originalScene.isDirty == originalDirty, "Original scene dirty state changed.");
                        report.originalSceneStateRestored = true;
                    }
                    catch (Exception error) { report.success = false; report.error += "\nScene restoration: " + error; }
                    finally { Write(stream, report); }
                }
            }
        }
        Check(report.success, "Fixture failed; inspect " + outputPath);
        return outputPath;
    }

    static Light[] VerifyBound(AlfaLightingRig rig, GameObject map, HiggsfieldMapLighting.Configuration config, Light[] originals)
    {
        Check(rig.IsHiggsfieldBound && rig.GetComponent<HiggsfieldMapLighting>().MapRoot == map.transform, "Rig not bound to current instance.");
        Check(RenderSettings.sun == rig.Moon && RenderSettings.skybox == config.Skybox, "Sun/skybox reference mismatch.");
        Check(rig.Moon.enabled && rig.Moon.type == LightType.Directional && rig.Moon.shadows == config.SunShadows &&
            rig.Moon.cullingMask == config.CullingMask, "Primary light state mismatch.");
        Equal(rig.Moon.color, config.SunColor, "Sun color"); Equal(rig.Moon.intensity, config.SunUnityIntensity, "Sun intensity");
        Check(Quaternion.Angle(rig.Moon.transform.rotation, config.SunWorldRotation) < .01f, "Sun rotation mismatch.");
        Equal(RenderSettings.ambientSkyColor, config.AmbientSky, "Ambient sky");
        Equal(RenderSettings.ambientEquatorColor, config.AmbientEquator, "Ambient equator");
        Equal(RenderSettings.ambientGroundColor, config.AmbientGround, "Ambient ground");
        Equal(RenderSettings.ambientIntensity, config.AmbientIntensity, "Ambient intensity");
        Equal(RenderSettings.reflectionIntensity, config.ReflectionIntensity, "Reflection intensity");
        Check(RenderSettings.ambientMode == AmbientMode.Trilight && RenderSettings.fog == config.FogEnabled && RenderSettings.fogMode == config.FogMode, "Environment modes mismatch.");
        Equal(RenderSettings.fogColor, config.FogColor, "Fog color"); Equal(RenderSettings.fogDensity, config.FogDensity, "Fog density");
        Equal(RenderSettings.fogStartDistance, config.FogStart, "Fog start"); Equal(RenderSettings.fogEndDistance, config.FogEnd, "Fog end");
        Check(rig.GlobalVolume.sharedProfile == config.VolumeProfile && rig.GlobalVolume.enabled == (config.VolumeProfile != null), "Bound Volume mismatch.");
        Equal(rig.GlobalVolume.weight, config.VolumeWeight, "Bound Volume weight");
        var extra = map.GetComponentsInChildren<Light>(true).Where(light => !originals.Contains(light)).ToArray();
        Check(extra.Length == config.LocalLights.Length, "Local light count mismatch.");
        foreach (var source in config.LocalLights)
        {
            var matched = extra.Where(light => light.transform.parent == source.Anchor).ToArray();
            Check(matched.Length == 1 && source.Anchor.IsChildOf(map.transform), "Local anchor ownership mismatch.");
            var light = matched[0];
            Check(light.enabled && light.gameObject.activeInHierarchy && light.type == source.Type && light.shadows == source.Shadows &&
                light.cullingMask == config.CullingMask, "Local light mode mismatch.");
            Check(light.transform.localPosition.sqrMagnitude < 1e-8f, "Local light offset differs from anchor.");
            Equal(light.color, source.Color, "Local color"); Equal(light.range, source.Range, "Local range");
            Equal(light.intensity, source.UnityIntensity, "Local intensity"); Equal(light.bounceIntensity, 0, "Local bounce");
            Equal(light.spotAngle, source.SpotAngle, "Spot angle"); Equal(light.innerSpotAngle, source.InnerSpotAngle, "Inner spot angle");
        }
        foreach (var light in config.SuppressLights) Check(!light.enabled, "Explicit light not suppressed.");
        foreach (var light in originals.Where(light => light.type == LightType.Directional)) Check(!light.enabled, "Imported sun not suppressed.");
        var lobby = typeof(AlfaLightingRig).GetField("lobbyFill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig) as Light;
        if (lobby) Check(!lobby.enabled, "Lobby fill not suppressed.");
        return extra;
    }
    sealed class LightState
    {
        readonly bool enabled; readonly LightType type; readonly Color color; readonly float intensity;
        readonly int mask; readonly LightShadows shadows; readonly Quaternion rotation;
        public LightState(Light light) { enabled = light.enabled; type = light.type; color = light.color; intensity = light.intensity;
            mask = light.cullingMask; shadows = light.shadows; rotation = light.transform.rotation; }
        public void Verify(Light light)
        {
            Check(light && light.enabled == enabled && light.type == type && light.cullingMask == mask && light.shadows == shadows, "Light not restored.");
            Equal(light.color, color, "Restored color"); Equal(light.intensity, intensity, "Restored intensity");
            Check(Quaternion.Angle(light.transform.rotation, rotation) < .01f, "Light rotation not restored.");
        }
    }
    static Dictionary<string, object> CaptureEnvironment() => EnvironmentFields.ToDictionary(name => name, name => typeof(RenderSettings).GetProperty(name).GetValue(null));
    static void EqualEnvironment(Dictionary<string, object> snapshot)
    {
        foreach (var pair in snapshot)
        {
            object value = typeof(RenderSettings).GetProperty(pair.Key).GetValue(null);
            if (value is float f) Equal(f, (float)pair.Value, pair.Key);
            else if (value is Color c) Equal(c, (Color)pair.Value, pair.Key);
            else if (value is SphericalHarmonicsL2 sh)
            {
                var expected = (SphericalHarmonicsL2)pair.Value;
                for (int channel = 0; channel < 3; channel++) for (int coefficient = 0; coefficient < 9; coefficient++)
                    Equal(sh[channel, coefficient], expected[channel, coefficient], "Ambient probe");
            }
            else Check(Equals(value, pair.Value), "Environment not restored: " + pair.Key);
        }
    }
    static void Equal(Color a, Color b, string label) { Equal(a.r,b.r,label); Equal(a.g,b.g,label); Equal(a.b,b.b,label); Equal(a.a,b.a,label); }
    static void Equal(float a, float b, string label) => Check(!float.IsNaN(a) && Mathf.Abs(a-b) <= 1e-4f * Mathf.Max(1, Mathf.Abs(b)), label + " mismatch.");
    static string AssetFile(string path)
    {
        Check(path != null && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains("\\"), "Assets path required.");
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
    static void TrackAsset(Dictionary<string,string> files, string path)
    {
        string file = AssetFile(path); files[file] = HashFile(file); files[file + ".meta"] = HashFile(file + ".meta");
    }
    static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Write(FileStream stream, Report report)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report, Formatting.Indented, JsonSettings)); stream.Position = 0; stream.SetLength(0);
        stream.Write(bytes,0,bytes.Length); stream.Flush(true);
    }
}
