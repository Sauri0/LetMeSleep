using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LetMeSleep.Editor
{
    /// <summary>Explicitly installs a verified five-map catalog into a NEW copy of a saved bootstrap scene.</summary>
    public static class HiggsfieldBootstrapSceneInstaller
    {
#pragma warning disable CS0649
        [Serializable] public sealed class Request
        {
            public int schema_version;
            public string sourceScenePath, expectedSourceGuid, expectedSourceSha256;
            public string catalogAssetPath, expectedCatalogGuid, expectedCatalogSha256;
            public string[] approvedMapIds;
            public string newScenePath, receiptPath;
        }
        [Serializable] private sealed class SpatialHeader { public int schema_version; public string map_id; }
        [Serializable] private sealed class MapReceipt
        {
            public string mapId, prefabGuid, contentHash, spatialSha256;
        }
        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string error, unityVersion, configSha256;
            public string sourceScenePath, sourceGuid, sourceSha256, sourceMetaSha256;
            public string newScenePath, newSceneGuid, newSceneSha256;
            public string catalogAssetPath, catalogGuid, catalogSha256;
            public bool originalPreserved, catalogPreserved, savedBindingVerified;
            public MapReceipt[] maps;
            public string scope = "Scene copy and serialized catalog binding only. Spatial headers checked; no navigation, visual, FPS, gameplay or WAN approval. Build Settings unchanged.";
        }
#pragma warning restore CS0649

        public static void InstallFromCommandLine()
        {
            string path = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-higgsfieldSceneConfig")
                {
                    Require(path == null && i + 1 < args.Length, "Supply exactly one scene config path.");
                    path = args[++i];
                }
            Install(path); // Caller controls -quit. Never exits an existing editor.
        }

        public static void Install(string configPath)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Run in idle Edit Mode.");
            Require(!string.IsNullOrWhiteSpace(configPath) && Path.IsPathRooted(configPath), "Absolute config required.");
            Require(File.Exists(configPath) && new FileInfo(configPath).Length <= 1024 * 1024, "Config missing or exceeds 1 MiB.");
            byte[] configBytes = File.ReadAllBytes(configPath);
            var request = JsonUtility.FromJson<Request>(Encoding.UTF8.GetString(configBytes));
            Require(request != null && request.schema_version == 1, "Request schema_version must be 1.");
            string source = AssetPath(request.sourceScenePath, ".unity");
            string target = AssetPath(request.newScenePath, ".unity");
            string catalogPath = AssetPath(request.catalogAssetPath, ".asset");
            Require(!string.Equals(source, target, StringComparison.OrdinalIgnoreCase), "New scene must differ from source.");
            Require(AssetDatabase.IsValidFolder(Path.GetDirectoryName(target).Replace('\\', '/')), "Target folder must exist.");
            string sourceFile = DiskPath(source), targetFile = DiskPath(target), catalogFile = DiskPath(catalogPath);
            Verify(source, sourceFile, request.expectedSourceGuid, request.expectedSourceSha256);
            Verify(catalogPath, catalogFile, request.expectedCatalogGuid, request.expectedCatalogSha256);
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(source), "Source must be an imported scene.");
            RequireNew(targetFile); RequireNew(targetFile + ".meta");
            Require(AssetDatabase.LoadMainAssetAtPath(target) == null, "Target already registered.");
            Require(!string.IsNullOrWhiteSpace(request.receiptPath) && Path.IsPathRooted(request.receiptPath), "Absolute receipt path required.");
            string receiptPath = Path.GetFullPath(request.receiptPath);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Require(!receiptPath.StartsWith(assets, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.GetDirectoryName(receiptPath)), "Receipt requires an existing folder outside Assets.");
            RequireNew(receiptPath);
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(catalogPath);
            Require(catalog && !EditorUtility.IsDirty(catalog), "Catalog must be imported and saved.");
            catalog.Validate();
            Require(catalog.Entries.Count == 5 && request.approvedMapIds != null && request.approvedMapIds.Length == 5,
                "Exactly five approved map IDs and five catalog entries required.");
            var approved = new HashSet<string>(request.approvedMapIds, StringComparer.Ordinal);
            Require(approved.Count == 5 && !approved.Contains(null) && !approved.Contains(""), "Approved IDs must be distinct and nonempty.");
            var categories = new HashSet<int>();
            var maps = new List<MapReceipt>();
            foreach (var entry in catalog.Entries)
            {
                Require(approved.Remove(entry.MapId), "Unapproved map: " + entry.MapId);
                Require(categories.Add((int)entry.Lighting.MapId) && (int)entry.Lighting.MapId >= 0 &&
                    (int)entry.Lighting.MapId <= 4, "Require one map of each of the five categories.");
                Require(entry.Prefab.SpatialData, "SpatialData required: " + entry.MapId);
                var spatial = JsonUtility.FromJson<SpatialHeader>(entry.Prefab.SpatialData.text);
                Require(spatial != null && spatial.schema_version == 1 && spatial.map_id == entry.MapId,
                    "SpatialData schema_version/map_id mismatch: " + entry.MapId);
                Require(!EditorUtility.IsDirty(entry.Prefab) && !EditorUtility.IsDirty(entry.Prefab.SpatialData),
                    "Save prefab and spatial data before installation.");
                maps.Add(new MapReceipt { mapId = entry.MapId,
                    prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Prefab)),
                    contentHash = entry.Prefab.ContentHash,
                    spatialSha256 = Hash(Encoding.UTF8.GetBytes(entry.Prefab.SpatialData.text)) });
            }
            string sourceMetaHash = HashFile(sourceFile + ".meta"), catalogMetaHash = HashFile(catalogFile + ".meta");
            var receipt = new Receipt { unityVersion = Application.unityVersion, configSha256 = Hash(configBytes),
                sourceScenePath = source, sourceGuid = request.expectedSourceGuid, sourceSha256 = request.expectedSourceSha256,
                sourceMetaSha256 = sourceMetaHash, newScenePath = target, catalogAssetPath = catalogPath,
                catalogGuid = request.expectedCatalogGuid, catalogSha256 = request.expectedCatalogSha256, maps = maps.ToArray() };
            Scene previousActive = SceneManager.GetActiveScene(), copy = default;
            using (var stream = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                WriteReceipt(stream, receipt);
                try
                {
                    Verify(source, sourceFile, request.expectedSourceGuid, request.expectedSourceSha256);
                    Verify(catalogPath, catalogFile, request.expectedCatalogGuid, request.expectedCatalogSha256);
                    RequireNew(targetFile); RequireNew(targetFile + ".meta");
                    Require(AssetDatabase.CopyAsset(source, target), "Unity could not copy scene.");
                    receipt.newSceneGuid = AssetDatabase.AssetPathToGUID(target);
                    Require(!string.IsNullOrEmpty(receipt.newSceneGuid) && receipt.newSceneGuid != request.expectedSourceGuid,
                        "Copy requires a new scene GUID.");
                    copy = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive);
                    MonoBehaviour application = FindApplication(copy);
                    var serialized = new SerializedObject(application);
                    var property = serialized.FindProperty("HiggsfieldMaps");
                    Require(property != null && property.propertyType == SerializedPropertyType.ObjectReference,
                        "Source lacks the current AlfaApplication.HiggsfieldMaps serialized field.");
                    property.objectReferenceValue = catalog;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.IsPartOfPrefabInstance(application))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(application);
                    EditorSceneManager.MarkSceneDirty(copy);
                    Require(EditorSceneManager.SaveScene(copy), "Failed to save new scene.");
                    Require(EditorSceneManager.CloseScene(copy, true), "Failed to close copied scene for verification.");
                    copy = default;
                    copy = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive);
                    var saved = new SerializedObject(FindApplication(copy)).FindProperty("HiggsfieldMaps");
                    Require(saved != null && saved.objectReferenceValue == catalog, "Saved catalog binding differs.");
                    receipt.savedBindingVerified = true;
                    Verify(source, sourceFile, request.expectedSourceGuid, request.expectedSourceSha256);
                    Require(HashFile(sourceFile + ".meta") == sourceMetaHash, "Source meta changed.");
                    receipt.originalPreserved = true;
                    Verify(catalogPath, catalogFile, request.expectedCatalogGuid, request.expectedCatalogSha256);
                    Require(HashFile(catalogFile + ".meta") == catalogMetaHash, "Catalog meta changed.");
                    receipt.catalogPreserved = true;
                    receipt.newSceneSha256 = HashFile(targetFile);
                    receipt.success = true;
                }
                catch (Exception error) { receipt.error = error.ToString(); throw; }
                finally
                {
                    try
                    {
                        if (copy.IsValid() && copy.isLoaded)
                            Require(EditorSceneManager.CloseScene(copy, true), "Failed to close copied scene.");
                        if (previousActive.IsValid() && previousActive.isLoaded && SceneManager.GetActiveScene() != previousActive)
                            Require(SceneManager.SetActiveScene(previousActive), "Failed to restore active scene.");
                    }
                    catch (Exception error) { receipt.success = false; receipt.error += "\n" + error; throw; }
                    finally { WriteReceipt(stream, receipt); }
                }
            }
        }

        private static MonoBehaviour FindApplication(Scene scene)
        {
            MonoBehaviour found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (candidate && candidate.GetType().FullName == "LetMeSleep.Bootstrap.AlfaApplication")
                    {
                        Require(!found, "Copy must contain exactly one AlfaApplication.");
                        found = candidate;
                    }
            Require(found, "Copy has no AlfaApplication.");
            return found;
        }
        private static string AssetPath(string value, string extension)
        {
            Require(!string.IsNullOrWhiteSpace(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.EndsWith(extension, StringComparison.Ordinal) && value.IndexOf('\\') < 0, "Canonical Assets path required.");
            foreach (string segment in value.Split('/'))
                Require(segment.Length > 0 && segment != "." && segment != ".." && segment == segment.Trim() &&
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, "Invalid path segment.");
            return value;
        }
        private static string DiskPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        private static void Verify(string assetPath, string file, string guid, string hash)
        {
            Require(!string.IsNullOrEmpty(guid) && guid.Length == 32 && AssetDatabase.AssetPathToGUID(assetPath) == guid,
                "Verified GUID mismatch: " + assetPath);
            Require(!string.IsNullOrEmpty(hash) && hash.Length == 64 && File.Exists(file) &&
                string.Equals(HashFile(file), hash, StringComparison.OrdinalIgnoreCase), "Verified SHA256 mismatch: " + assetPath);
        }
        private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void RequireNew(string path) => Require(!File.Exists(path) && !Directory.Exists(path), "Refusing existing output: " + path);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void WriteReceipt(FileStream stream, Receipt receipt)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt, true));
            stream.Position = 0; stream.SetLength(0); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
    }
}
