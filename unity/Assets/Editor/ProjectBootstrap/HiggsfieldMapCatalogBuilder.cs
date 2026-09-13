using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
using LetMeSleep.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// Explicit invocation: -executeMethod LetMeSleep.Editor.HiggsfieldMapCatalogBuilder.BuildFromCommandLine
    /// -higgsfieldCatalogConfig N:/absolute/config.json . Caller controls -quit; this helper never exits a resident editor.
    /// JSON fields match Request/MapInput/LocalInput below. Lighting fields use Configuration's exact public names.
    /// Creates only a NEW catalog asset and NEW external receipt; no scene installation or automatic map discovery.
    /// </summary>
    public static class HiggsfieldMapCatalogBuilder
    {
#pragma warning disable CS0649
        [Serializable] public sealed class Request
        {
            public int schema_version;
            public string outputAssetPath;
            public string receiptPath;
            public int declaredCount;
            public bool requireFiveMaps;
            public bool allowMissingSpatialDataForAuthoring;
            public MapInput[] entries;
        }
        [Serializable] public sealed class MapInput
        {
            public string mapId, displayName, prefabPath;
            public HiggsfieldMapLighting.Configuration lighting;
            public string skyboxAssetPath, volumeProfileAssetPath;
            public float[] sunEulerDegrees;
            public LocalInput[] localLights;
            public string[] suppressLightPaths;
        }
        [Serializable] public sealed class LocalInput
        {
            public string anchorPath;
            public string type;
            public Color color;
            public float unityIntensity, range;
            public float spotAngle = 90, innerSpotAngle = 60;
            public string shadows = "None";
            // Optional assertion of the existing anchor's MAP-LOCAL Unity position; never moves it.
            public float[] expectedMapLocalPosition;
        }
        [Serializable] private sealed class SpatialHeader { public int schema_version; public string map_id; }
        [Serializable] private sealed class MapReceipt
        {
            public string mapId, prefabPath, contentHash, spatialSha256;
            public bool spatialHeaderValidated;
            public int localLightCount;
        }
        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string configSha256, unityVersion, outputAssetPath, assetGuid, assetSha256;
            public int declaredCount;
            public bool fiveMapStructuralCheck;
            public bool sceneInstalled = false;
            public string scope = "Authoring catalog validation only; no scene installation, visual approval, gameplay, FPS or WAN evidence.";
            public MapReceipt[] maps;
        }
#pragma warning restore CS0649

        public static void BuildFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string path = null;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-higgsfieldCatalogConfig")
                {
                    if (path != null || i + 1 >= args.Length) throw new ArgumentException("Supply exactly one config path.");
                    path = args[++i];
                }
            Build(path);
        }

        public static void Build(string configPath)
        {
            Require(!string.IsNullOrWhiteSpace(configPath) && Path.IsPathRooted(configPath), "An absolute external JSON config is required.");
            configPath = Path.GetFullPath(configPath);
            Require(File.Exists(configPath) && new FileInfo(configPath).Length <= 1024 * 1024, "Config missing or larger than1MiB.");
            string json = File.ReadAllText(configPath);
            var request = JsonUtility.FromJson<Request>(json);
            Require(request != null && request.schema_version == 1 && request.entries != null &&
                request.declaredCount == request.entries.Length && request.declaredCount >= 1 && request.declaredCount <= 5,
                "Schema1 and an exact declaredCount of1..5 are required.");
            Require(!request.requireFiveMaps || (request.declaredCount == 5 && !request.allowMissingSpatialDataForAuthoring),
                "Final structural gate requires5 maps and mandatory SpatialData.");
            string output = AssetPath(request.outputAssetPath, ".asset");
            Require(AssetDatabase.IsValidFolder(Path.GetDirectoryName(output).Replace('\\', '/')), "Output asset folder must already exist.");
            string outputFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
            Require(!string.IsNullOrWhiteSpace(request.receiptPath) && Path.IsPathRooted(request.receiptPath), "Receipt must be an absolute external path.");
            string receiptPath = Path.GetFullPath(request.receiptPath);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(!receiptPath.StartsWith(assets, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.GetDirectoryName(receiptPath)), "Receipt needs an existing folder outside Assets.");
            RequireNew(outputFile); RequireNew(outputFile + ".meta"); RequireNew(receiptPath);
            Require(AssetDatabase.LoadMainAssetAtPath(output) == null, "Output asset already exists.");

            var entries = new HiggsfieldMapCatalog.Entry[request.declaredCount];
            var receipts = new MapReceipt[entries.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                var input = request.entries[i];
                Require(input != null && !string.IsNullOrWhiteSpace(input.mapId) && ids.Add(input.mapId), "Missing/duplicate map ID.");
                string prefabPath = AssetPath(input.prefabPath, ".prefab");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var definition = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
                Require(definition && definition.MapId == input.mapId, "Prefab MapId does not match: " + input.mapId);
                var receipt = new MapReceipt { mapId = input.mapId, prefabPath = prefabPath, contentHash = definition.ContentHash };
                if (definition.SpatialData)
                {
                    var header = JsonUtility.FromJson<SpatialHeader>(definition.SpatialData.text);
                    Require(header != null && header.schema_version == 1 && header.map_id == input.mapId,
                        "SpatialData schema_version/map_id mismatch: " + input.mapId);
                    receipt.spatialHeaderValidated = true;
                    receipt.spatialSha256 = Hash(Encoding.UTF8.GetBytes(definition.SpatialData.text));
                }
                else Require(request.allowMissingSpatialDataForAuthoring && !request.requireFiveMaps, "SpatialData is required: " + input.mapId);
                Require(input.lighting != null && input.localLights != null && input.suppressLightPaths != null,
                    "Explicit lighting and relative light lists are required.");
                Require(!input.lighting.Skybox && !input.lighting.VolumeProfile &&
                    input.lighting.LocalLights != null && input.lighting.LocalLights.Length == 0 &&
                    input.lighting.SuppressLights != null && input.lighting.SuppressLights.Length == 0,
                    "No JSON object/instance references: supply asset paths and relative bindings.");
                input.lighting.Skybox = LoadOptional<Material>(input.skyboxAssetPath);
                input.lighting.VolumeProfile = LoadOptional<VolumeProfile>(input.volumeProfileAssetPath);
                input.lighting.SunWorldRotation = Quaternion.Euler(Vector(input.sunEulerDegrees));
                var bindings = new HiggsfieldMapCatalog.LocalLightBinding[input.localLights.Length];
                for (int j = 0; j < bindings.Length; j++)
                {
                    var light = input.localLights[j];
                    Require(light != null, "Null local light.");
                    Require(Enum.TryParse(light.type, out LightType type) && (type == LightType.Point || type == LightType.Spot), "Local type must be Point or Spot.");
                    Require(Enum.TryParse(light.shadows, out LightShadows shadows) && Enum.IsDefined(typeof(LightShadows), shadows), "Invalid local shadow mode.");
                    bindings[j] = new HiggsfieldMapCatalog.LocalLightBinding { AnchorPath = light.anchorPath,
                        Settings = new HiggsfieldMapLighting.LocalSource { Type = type, Color = light.color,
                            UnityIntensity = light.unityIntensity, Range = light.range, SpotAngle = light.spotAngle,
                            InnerSpotAngle = light.innerSpotAngle, Shadows = shadows } };
                }
                entries[i] = new HiggsfieldMapCatalog.Entry { MapId = input.mapId, DisplayName = input.displayName,
                    Prefab = definition, Lighting = input.lighting, LocalLights = bindings, SuppressLightPaths = input.suppressLightPaths };
                receipt.localLightCount = bindings.Length; receipts[i] = receipt;
            }

            var catalog = ScriptableObject.CreateInstance<HiggsfieldMapCatalog>();
            bool created = false;
            try
            {
                typeof(HiggsfieldMapCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, entries);
                catalog.Validate(); // Includes canonical/unique relative paths on the prefab, before any asset mutation.
                for (int i = 0; i < entries.Length; i++)
                    foreach (var light in request.entries[i].localLights)
                        if (light.expectedMapLocalPosition != null && light.expectedMapLocalPosition.Length != 0)
                        {
                            var root = entries[i].Prefab.transform;
                            var anchor = light.anchorPath == "." ? root : root.Find(light.anchorPath);
                            Require(anchor && Vector3.Distance(root.InverseTransformPoint(anchor.position), Vector(light.expectedMapLocalPosition)) <= .01f,
                                "Existing anchor position differs by more than1cm: " + light.anchorPath);
                        }
                // Reserve receipt without overwriting. Asset existence is checked again immediately before Unity CreateAsset.
                using (var stream = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var receipt = new Receipt { configSha256 = Hash(Encoding.UTF8.GetBytes(json)), unityVersion = Application.unityVersion,
                        outputAssetPath = output, declaredCount = entries.Length, maps = receipts };
                    byte[] pending = Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt, true));
                    stream.Write(pending, 0, pending.Length); stream.Flush(); // Failure leaves a non-success receipt, not an empty success marker.
                    RequireNew(outputFile); RequireNew(outputFile + ".meta");
                    AssetDatabase.CreateAsset(catalog, output); created = AssetDatabase.Contains(catalog);
                    Require(created && AssetDatabase.GetAssetPath(catalog) == output, "Unity did not create the requested catalog asset.");
                    EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                    var saved = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(output); saved.Validate();
                    receipt.assetGuid = AssetDatabase.AssetPathToGUID(output);
                    receipt.assetSha256 = Hash(File.ReadAllBytes(outputFile));
                    receipt.fiveMapStructuralCheck = request.requireFiveMaps;
                    receipt.success = true;
                    stream.Position = 0; stream.SetLength(0);
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(JsonUtility.ToJson(receipt, true));
                }
                Debug.Log("LMS_HIGGSFIELD_CATALOG_CREATED " + output);
            }
            catch
            {
                // Preserve any newly-created artifact for inspection; never delete project files or retry by overwriting.
                Debug.LogError("LMS_HIGGSFIELD_CATALOG_FAILED inspect new output/receipt; no scene was installed.");
                throw;
            }
            finally { if (!created) UnityEngine.Object.DestroyImmediate(catalog); }
        }

        private static T LoadOptional<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = AssetPath(path, null);
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(value, "Missing or wrong asset type: " + path); return value;
        }
        private static string AssetPath(string path, string extension)
        {
            Require(!string.IsNullOrWhiteSpace(path) && path == path.Trim() && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                !path.Contains("\\") && !path.Contains(":"), "Use a canonical Assets/ path.");
            foreach (var part in path.Split('/')) Require(part.Length != 0 && part != "." && part != "..", "Noncanonical asset path.");
            Require(extension == null || path.EndsWith(extension, StringComparison.OrdinalIgnoreCase), "Wrong asset extension.");
            return path;
        }
        private static Vector3 Vector(float[] values)
        {
            Require(values != null && values.Length == 3, "Explicit3-element Unity vector required.");
            foreach (float value in values) Require(!float.IsNaN(value) && !float.IsInfinity(value), "Nonfinite vector.");
            return new Vector3(values[0], values[1], values[2]);
        }
        private static void RequireNew(string path) => Require(!File.Exists(path) && !Directory.Exists(path), "Output already exists: " + path);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
