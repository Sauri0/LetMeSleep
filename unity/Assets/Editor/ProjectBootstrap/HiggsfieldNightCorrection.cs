using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Bootstrap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LetMeSleep.Editor
{
    // Explicit final correction only. Never creates scenes, edits prefabs or saves global Assets.
    public static class HiggsfieldNightCorrection
    {
        [Serializable] public sealed class Request
        {
            public int schema_version;
            public string catalogPath, catalogGuid, catalogSha256, scenePath, sceneGuid, sceneSha256;
            public string receiptPath, catalogReceiptPath, sceneReceiptPath;
            public Item[] maps;
        }
        [Serializable] public sealed class Item
        {
            public string mapId, skyboxPath, skyboxGuid, skyboxSha256;
            public float moonIntensity, exposure;
            public float[] ambientSky, ambientEquator, ambientGround;
        }
        static readonly string[] AllowedIds = { "hf-casa-del-patio-v1", "hf-campamento-pinar-v2" };
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error,
            CheckAdditionalContent = true, MaxDepth = 32
        };
        public static void ApplyFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-higgsfieldNightConfig");
            Need(index >= 0 && index + 1 < args.Length && Array.LastIndexOf(args, "-higgsfieldNightConfig") == index, "One explicit config required.");
            Apply(args[index + 1]);
        }
        public static void Apply(string configPath)
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Idle Edit Mode required.");
            Need(Path.IsPathRooted(configPath) && File.Exists(configPath) && new FileInfo(configPath).Length <= 1024 * 1024, "Absolute config up to 1 MiB required.");
            byte[] input = File.ReadAllBytes(configPath);
            var request = JsonConvert.DeserializeObject<Request>(Encoding.UTF8.GetString(input), Json);
            Need(request != null && request.schema_version == 1 && request.maps != null && request.maps.Length == 2 &&
                request.maps.Select(m => m.mapId).OrderBy(s => s).SequenceEqual(AllowedIds.OrderBy(s => s)), "Only Casa v1 and Camp v2 may change.");
            Need(request.catalogPath == "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset", "Only the installed final catalog is allowed.");
            Verify(request.catalogPath, request.catalogGuid, request.catalogSha256);
            Verify(request.scenePath, request.sceneGuid, request.sceneSha256);
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(request.catalogPath);
            Need(catalog && !EditorUtility.IsDirty(catalog), "Saved catalog required."); catalog.Validate();
            Need(catalog.Entries.Count == 5, "Five-map final catalog required.");
            var materials = new List<Material>();
            foreach (var item in request.maps)
            {
                Need(item.skyboxPath == "Assets/LetMeSleep/Presentation/Generated/Materials/" + item.mapId + "-Skybox.mat", "Only own map skybox materials allowed.");
                Verify(item.skyboxPath, item.skyboxGuid, item.skyboxSha256);
                var material = AssetDatabase.LoadAssetAtPath<Material>(item.skyboxPath);
                Need(material && !EditorUtility.IsDirty(material) && material.shader.name == "Skybox/Procedural" && material.HasProperty("_Exposure"), "Saved procedural skybox required.");
                Need(catalog.Resolve(item.mapId).Lighting.Skybox == material &&
                    catalog.Entries.Count(e => e.Lighting.Skybox == material) == 1, "Skybox must belong exclusively to its target entry.");
                Need(Finite(item.moonIntensity) && item.moonIntensity >= .08f && item.moonIntensity <= .12f &&
                    Finite(item.exposure) && item.exposure >= .08f && item.exposure <= .15f, "Night intensity/exposure outside authorized range.");
                ColorOf(item.ambientSky); ColorOf(item.ambientEquator); ColorOf(item.ambientGround);
                materials.Add(material);
            }
            string[] outputs = { request.receiptPath, request.catalogReceiptPath, request.sceneReceiptPath };
            Need(outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "Three distinct new receipts required.");
            foreach (string path in outputs)
                Need(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && Directory.Exists(Path.GetDirectoryName(path)) &&
                    !Path.GetFullPath(path).StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !File.Exists(path) && !Directory.Exists(path), "Receipts need new absolute files outside Assets.");
            var objects = new List<UnityEngine.Object> { catalog }; objects.AddRange(materials);
            string[] paths = new[] { request.catalogPath }.Concat(request.maps.Select(m => m.skyboxPath)).ToArray();
            string[] snapshots = objects.Select(o => EditorJsonUtility.ToJson(o)).ToArray();
            string[] metaHashes = paths.Select(p => HashFile(Disk(p) + ".meta")).ToArray();
            JObject invariantCatalog = StripCatalog(JObject.Parse(snapshots[0]));
            JObject[] invariantMaterials = snapshots.Skip(1).Select(s => StripMaterial(JObject.Parse(s))).ToArray();
            JArray beforeMaps = MapIdentities(catalog);
            string sceneMetaHash = HashFile(Disk(request.scenePath) + ".meta");
            var reports = outputs.Select((p, i) => new JObject { ["success"] = false, ["configSha256"] = Hash(input),
                ["unityVersion"] = Application.unityVersion, ["scope"] = i == 0 ? "Casa/Camp night parameter correction; visual approval pending. Local lights/emission and other maps preserved." :
                i == 1 ? "Post-adjustment inspection of existing catalog; not creation or visual approval." : "Post-adjustment inspection of existing saved scene binding; no scene creation or save." }).ToArray();
            reports[0]["catalogSha256Before"] = request.catalogSha256;
            var streams = new List<FileStream>();
            Scene previous = SceneManager.GetActiveScene(), inspected = default;
            bool opened = false, mutationStarted = false;
            try
            {
                for (int i = 0; i < outputs.Length; i++)
                {
                    streams.Add(new FileStream(outputs[i], FileMode.CreateNew, FileAccess.Write, FileShare.None)); Write(streams[i], reports[i]);
                }
                inspected = SceneManager.GetSceneByPath(request.scenePath);
                if (!inspected.IsValid() || !inspected.isLoaded)
                {
                    if (string.IsNullOrEmpty(previous.path))
                    {
                        Need(Application.isBatchMode && SceneManager.sceneCount == 1, "Initial Untitled can only be replaced in a dedicated batch.");
                        inspected = EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Single);
                    }
                    else { inspected = EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Additive); opened = true; }
                }
                Need(!inspected.isDirty, "Scene inspection requires saved state.");
                VerifySceneBinding(inspected, catalog);
                // Recheck all file guards immediately before writes.
                Verify(request.catalogPath, request.catalogGuid, request.catalogSha256);
                foreach (var item in request.maps) Verify(item.skyboxPath, item.skyboxGuid, item.skyboxSha256);
                mutationStarted = true;
                for (int i = 0; i < request.maps.Length; i++)
                {
                    var item = request.maps[i]; var lighting = catalog.Resolve(item.mapId).Lighting;
                    lighting.SunUnityIntensity = item.moonIntensity;
                    lighting.AmbientSky = ColorOf(item.ambientSky); lighting.AmbientEquator = ColorOf(item.ambientEquator); lighting.AmbientGround = ColorOf(item.ambientGround);
                    materials[i].SetFloat("_Exposure", item.exposure);
                    materials[i].SetColor("_SkyTint", ColorOf(item.ambientSky)); materials[i].SetColor("_GroundColor", ColorOf(item.ambientGround));
                }
                catalog.Validate();
                Need(JToken.DeepEquals(invariantCatalog, StripCatalog(JObject.Parse(EditorJsonUtility.ToJson(catalog)))), "Unexpected catalog change outside four allowed fields per entry.");
                for (int i = 0; i < materials.Count; i++)
                    Need(JToken.DeepEquals(invariantMaterials[i], StripMaterial(JObject.Parse(EditorJsonUtility.ToJson(materials[i])))), "Unexpected skybox property change.");
                foreach (var asset in objects) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
                var readback = new JArray();
                var serialized = new SerializedObject(catalog); serialized.Update();
                var entries = serialized.FindProperty("entries");
                foreach (var item in request.maps)
                {
                    SerializedProperty entry = null;
                    for (int i = 0; i < entries.arraySize; i++)
                        if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("MapId").stringValue == item.mapId) entry = entries.GetArrayElementAtIndex(i);
                    Need(entry != null, "Serialized entry missing."); var light = entry.FindPropertyRelative("Lighting");
                    Need(Mathf.Abs(light.FindPropertyRelative("SunUnityIntensity").floatValue - item.moonIntensity) < 1e-6f, "Serialized moon mismatch.");
                    Need(light.FindPropertyRelative("AmbientSky").colorValue == ColorOf(item.ambientSky) &&
                        light.FindPropertyRelative("AmbientEquator").colorValue == ColorOf(item.ambientEquator) &&
                        light.FindPropertyRelative("AmbientGround").colorValue == ColorOf(item.ambientGround), "Serialized ambient mismatch.");
                    var material = catalog.Resolve(item.mapId).Lighting.Skybox;
                    var materialSerialized = new SerializedObject(material); materialSerialized.Update();
                    Need(Mathf.Abs(SavedValue(materialSerialized, "m_Floats", "_Exposure").floatValue - item.exposure) < 1e-6f &&
                        SavedValue(materialSerialized, "m_Colors", "_SkyTint").colorValue == ColorOf(item.ambientSky) &&
                        SavedValue(materialSerialized, "m_Colors", "_GroundColor").colorValue == ColorOf(item.ambientGround), "Serialized skybox mismatch.");
                    VerifyGuid(item.skyboxPath, item.skyboxGuid);
                    readback.Add(JObject.FromObject(new { item.mapId, item.moonIntensity, item.exposure, item.ambientSky, item.ambientEquator, item.ambientGround,
                        skyboxGuid = item.skyboxGuid, skyboxSha256Before = item.skyboxSha256, skyboxSha256After = HashFile(Disk(item.skyboxPath)) }));
                }
                for (int i = 0; i < paths.Length; i++) Need(HashFile(Disk(paths[i]) + ".meta") == metaHashes[i], "Asset meta changed.");
                Need(JToken.DeepEquals(beforeMaps, MapIdentities(catalog)), "Prefab/navigation/material dependency identities changed.");
                VerifySceneBinding(inspected, catalog);
                Verify(request.scenePath, request.sceneGuid, request.sceneSha256);
                Need(!inspected.isDirty && HashFile(Disk(request.scenePath) + ".meta") == sceneMetaHash, "Scene changed.");
                VerifyGuid(request.catalogPath, request.catalogGuid);
                string catalogHash = HashFile(Disk(request.catalogPath));
                Need(catalogHash != request.catalogSha256, "Catalog file did not change.");
                reports[0]["readbackSerializedObject"] = readback; reports[0]["allowedChangesOnly"] = true;
                for (int i = 0; i < 3; i++)
                {
                    reports[i]["catalogAssetPath"] = request.catalogPath; reports[i]["catalogGuid"] = request.catalogGuid;
                    reports[i]["catalogSha256"] = catalogHash; reports[i]["maps"] = beforeMaps.DeepClone(); reports[i]["success"] = true;
                }
                reports[1]["fiveMapStructuralCheck"] = true; reports[1]["inspectionOnly"] = true;
                reports[1]["outputAssetPath"] = request.catalogPath; reports[1]["assetGuid"] = request.catalogGuid;
                reports[1]["assetSha256"] = catalogHash; reports[1]["declaredCount"] = 5;
                reports[2]["scenePath"] = request.scenePath; reports[2]["sceneGuid"] = request.sceneGuid;
                reports[2]["sceneSha256"] = request.sceneSha256; reports[2]["sceneMetaSha256"] = sceneMetaHash;
                reports[2]["savedBindingVerified"] = true; reports[2]["sceneUnchanged"] = true; reports[2]["inspectionOnly"] = true;
            }
            catch (Exception error)
            {
                foreach (var report in reports) { report["success"] = false; report["error"] = error.ToString(); }
                if (mutationStarted)
                {
                    try
                    {
                        for (int i = 0; i < objects.Count; i++) { EditorJsonUtility.FromJsonOverwrite(snapshots[i], objects[i]); EditorUtility.SetDirty(objects[i]); AssetDatabase.SaveAssetIfDirty(objects[i]); }
                        reports[0]["rollbackAppliedViaUnityApi"] = true;
                    }
                    catch (Exception rollback) { reports[0]["rollbackError"] = rollback.ToString(); }
                }
                throw;
            }
            finally
            {
                try
                {
                    if (opened && inspected.IsValid() && inspected.isLoaded) Need(EditorSceneManager.CloseScene(inspected, true), "Cannot close inspected scene.");
                    if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous)
                        Need(SceneManager.SetActiveScene(previous), "Cannot restore active scene.");
                }
                catch (Exception cleanup)
                {
                    foreach (var report in reports) { report["success"] = false; report["cleanupError"] = cleanup.ToString(); }
                    throw;
                }
                finally { for (int i = 0; i < streams.Count; i++) { Write(streams[i], reports[i]); streams[i].Dispose(); } }
            }
        }
        static SerializedProperty SavedValue(SerializedObject material, string collection, string name)
        {
            var array = material.FindProperty("m_SavedProperties." + collection);
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name)
                    return array.GetArrayElementAtIndex(i).FindPropertyRelative("second");
            throw new InvalidOperationException("Serialized material property missing: " + name);
        }
        static JObject StripCatalog(JObject root)
        {
            var entries = root["entries"] as JArray; Need(entries != null && entries.Count == 5, "Unexpected catalog serialization.");
            foreach (var entry in entries.OfType<JObject>().Where(e => AllowedIds.Contains((string)e["MapId"])))
            {
                var light = (JObject)entry["Lighting"];
                foreach (string field in new[] { "SunUnityIntensity", "AmbientSky", "AmbientEquator", "AmbientGround" }) Need(light.Remove(field), "Allowed field missing.");
            }
            return root;
        }
        static JObject StripMaterial(JObject root)
        {
            var saved = root["m_SavedProperties"] as JObject; Need(saved != null, "Unexpected material serialization.");
            foreach (string collection in new[] { "m_Floats", "m_Colors" })
            {
                var array = saved[collection] as JArray; Need(array != null, "Missing material property array.");
                foreach (var value in array.OfType<JObject>().Where(v => new[] { "_Exposure", "_SkyTint", "_GroundColor" }.Contains((string)v["first"])).ToArray()) value.Remove();
            }
            return root;
        }
        static JArray MapIdentities(HiggsfieldMapCatalog catalog)
        {
            var result = new JArray();
            foreach (var entry in catalog.Entries)
            {
                Need(entry.Prefab.SpatialData, "SpatialData required.");
                var header = JObject.Parse(entry.Prefab.SpatialData.text);
                Need((int?)header["schema_version"] == 1 && (string)header["map_id"] == entry.MapId, "Spatial header mismatch.");
                string path = AssetDatabase.GetAssetPath(entry.Prefab);
                result.Add(JObject.FromObject(new { mapId = entry.MapId, prefabPath = path, prefabGuid = AssetDatabase.AssetPathToGUID(path),
                    prefabSha256 = HashFile(Disk(path)), prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                    contentHash = entry.Prefab.ContentHash, spatialSha256 = Hash(Encoding.UTF8.GetBytes(entry.Prefab.SpatialData.text)),
                    skyboxGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Lighting.Skybox)),
                    protectedSkyboxSha256 = AllowedIds.Contains(entry.MapId) ? null : HashFile(Disk(AssetDatabase.GetAssetPath(entry.Lighting.Skybox))) }));
            }
            return result;
        }
        static void VerifySceneBinding(Scene scene, HiggsfieldMapCatalog catalog)
        {
            var apps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(c => c && c.GetType().FullName == "LetMeSleep.Bootstrap.AlfaApplication").ToArray();
            Need(apps.Length == 1, "Expected one AlfaApplication in saved scene.");
            var serialized = new SerializedObject(apps[0]); serialized.Update();
            Need(serialized.FindProperty("HiggsfieldMaps")?.objectReferenceValue == catalog, "Saved scene catalog reference mismatch.");
        }
        static Color ColorOf(float[] v) { Need(v != null && v.Length == 3 && v.All(x => Finite(x) && x >= 0 && x <= 1), "Explicit RGB [0,1] required."); return new Color(v[0], v[1], v[2], 1); }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static void Need(bool yes, string why) { if (!yes) throw new InvalidOperationException(why); }
        static string Disk(string path) { Need(path != null && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains("\\"), "Canonical Assets path required."); return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)); }
        static void VerifyGuid(string path, string guid) => Need(!string.IsNullOrEmpty(guid) && guid.Length == 32 && AssetDatabase.AssetPathToGUID(path) == guid, "GUID mismatch: " + path);
        static void Verify(string path, string guid, string hash) { VerifyGuid(path, guid); Need(hash != null && hash.Length == 64 && HashFile(Disk(path)) == hash, "SHA mismatch: " + path); }
        static string HashFile(string p) => Hash(File.ReadAllBytes(p));
        static string Hash(byte[] b) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        static void Write(FileStream stream, JObject report) { byte[] b = Encoding.UTF8.GetBytes(report.ToString(Formatting.Indented)); stream.Position = 0; stream.SetLength(0); stream.Write(b, 0, b.Length); stream.Flush(true); }
    }
}
