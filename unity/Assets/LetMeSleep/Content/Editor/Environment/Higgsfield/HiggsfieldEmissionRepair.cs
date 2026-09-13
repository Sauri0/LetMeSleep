using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Editor.Higgsfield
{
    // Explicit in-place material repair. No ModelImporter, scene opening, prefab instantiation or geometry building.
    public static class HiggsfieldEmissionRepair
    {
        [Serializable] sealed class CommandConfiguration
        {
            public string mapId, sourceFbxSha256, newRecipePath, expectedContentHash, receiptPath;
        }
        public static void RunFromCommandLine()
        {
            try
            {
                var args=System.Environment.GetCommandLineArgs();
                int index=Array.IndexOf(args,"-higgsfieldEmissionRepair");
                if(index<0 || index+1>=args.Length)throw new ArgumentException("Explicit emission repair configuration required.");
                var config=JsonUtility.FromJson<CommandConfiguration>(File.ReadAllText(args[index+1]));
                string receipt=Repair(config.mapId,config.sourceFbxSha256,config.newRecipePath,config.expectedContentHash,config.receiptPath);
                Debug.Log("HIGGSFIELD_EMISSION_REPAIR_DONE "+receipt);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        const string Strategy = "higgsfield-emission-repair-1";

        public static string Repair(string mapId, string sourceFbxSha256, string newRecipePath, string expectedContentHash, string receiptPath)
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Idle Edit mode required.");
            string nextText = File.ReadAllText(newRecipePath);
            var next = JsonUtility.FromJson<HiggsfieldImportContract>(nextText);
            Need(next != null, "New recipe missing."); next.Validate();
            Need(next.mapId == mapId && SameHash(next.sourceSha256, sourceFbxSha256), "Requested map/source does not match new recipe.");
            string folder = HiggsfieldEnvironmentImporter.Output + "/" + mapId;
            string prefabPath = folder + "/Prefabs/" + mapId + ".prefab";
            string modelPath = folder + "/Models/Environment.fbx";
            string oldRecipePath = folder + "/Data/import-recipe.json";
            Need(!File.Exists(folder + "/Data/import-incomplete.txt"), "Incomplete map cannot be repaired.");
            Need(File.Exists(modelPath) && SameHash(HashFile(modelPath), sourceFbxSha256), "Imported FBX hash mismatch.");
            string externalFbx = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(newRecipePath)), next.sourceFbx));
            Need(File.Exists(externalFbx) && SameHash(HashFile(externalFbx), sourceFbxSha256), "New recipe source FBX hash mismatch.");
            var before = JsonUtility.FromJson<HiggsfieldImportContract>(File.ReadAllText(oldRecipePath));
            HiggsfieldImportContract.ValidateEmissionOnlyChange(before, next);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Need(prefab != null && !EditorUtility.IsDirty(prefab), "Existing clean prefab asset required.");
            var definition = prefab.GetComponent<EnvironmentMapDefinition>();
            Need(definition != null && definition.MapId == mapId && definition.ContentHash == expectedContentHash && !string.IsNullOrEmpty(expectedContentHash), "Current ContentHash/map differs from caller expectation.");
            Need(!EditorUtility.IsDirty(definition), "Map definition has unsaved changes.");
            // A saved scene inherits the prefab metadata. Never silently leave a loaded override stale.
            foreach (var loaded in Resources.FindObjectsOfTypeAll<EnvironmentMapDefinition>())
            {
                if (EditorUtility.IsPersistent(loaded) || !loaded.gameObject.scene.IsValid() || loaded.MapId != mapId) continue;
                Need(PrefabUtility.GetCorrespondingObjectFromSource(loaded) == definition && loaded.ContentHash == expectedContentHash,
                    "Loaded map is detached or has a different content hash; close/reconcile it before repair.");
                Need(!new SerializedObject(loaded).FindProperty("ContentHash").prefabOverride, "Loaded scene overrides ContentHash; resolve it before repair.");
            }

            var palette = new Dictionary<string, Material>(StringComparer.Ordinal);
            var backups = new Dictionary<Material, string>();
            var changes = new List<MaterialChange>();
            for (int i = 0; i < next.materials.Length; i++)
            {
                var swatch = next.materials[i];
                string path = folder + "/Materials/Color_" + i.ToString("000") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                // CreateAsset may adopt Color_NNN (the asset filename) as the material's native name.
                // Identity still comes from immutable recipe order/path and exact renderer references below.
                Need(material != null && (material.name == swatch.sourceName || material.name == Path.GetFileNameWithoutExtension(path)) && material.shader.name == "Universal Render Pipeline/Lit", "Material palette/path mismatch: " + path);
                Need(!EditorUtility.IsDirty(material), "Material has unsaved changes: " + path);
                Need(material.GetTexture("_BaseMap") == null && material.GetTexture("_EmissionMap") == null, "Flat untextured material required: " + path);
                var reference = new Material(material.shader);
                try
                {
                    var c = new Color(swatch.rgb[0], swatch.rgb[1], swatch.rgb[2], 1);
                    reference.SetColor("_BaseColor", swatch.colorSpace == "linear" ? c.gamma : c);
                    Need(Close(material.GetVector("_BaseColor"), reference.GetVector("_BaseColor")), "Current base RGB differs from recipe: " + path);
                }
                finally { Object.DestroyImmediate(reference); }
                palette.Add(swatch.sourceName, material);
                var desired = new Material(material);
                try
                {
                    HiggsfieldEnvironmentImporter.ApplyEmission(desired, swatch);
                    if (!Close(material.GetVector("_EmissionColor"), desired.GetVector("_EmissionColor")) ||
                        material.IsKeywordEnabled("_EMISSION") != desired.IsKeywordEnabled("_EMISSION") ||
                        material.globalIlluminationFlags != desired.globalIlluminationFlags)
                    {
                        backups.Add(material, EditorJsonUtility.ToJson(material));
                        changes.Add(new MaterialChange { path = path, guid = AssetDatabase.AssetPathToGUID(path), name = swatch.sourceName,
                            beforeFileSha256 = HashFile(path), baseRgb = A(material.GetVector("_BaseColor")),
                            beforeEmission = A(material.GetVector("_EmissionColor")), afterEmission = A(desired.GetVector("_EmissionColor")),
                            beforeKeyword = material.IsKeywordEnabled("_EMISSION"), afterKeyword = desired.IsKeywordEnabled("_EMISSION"),
                            beforeGiFlags = (int)material.globalIlluminationFlags, afterGiFlags = (int)desired.globalIlluminationFlags });
                    }
                }
                finally { Object.DestroyImmediate(desired); }
            }
            Need(prefab.transform.childCount == 1, "Expected one authored model root in prefab.");
            Transform visual = prefab.transform.GetChild(0);
            var importedSlots=JsonUtility.FromJson<ImportedPalette>(File.ReadAllText(folder+"/Data/import-receipt.json"));
            Need(importedSlots?.meshes!=null,"Native import material-slot receipt required.");
            var expectedSlots=importedSlots.meshes.ToDictionary(m=>m.path,m=>m.materialSlots,StringComparer.Ordinal);
            var usedRules = new HashSet<string>(StringComparer.Ordinal);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                string path = Relative(renderer.transform, visual);
                usedRules.Add(next.Resolve(path).path);
                Need(expectedSlots.TryGetValue(path,out var slots) && slots!=null && slots.Length==renderer.sharedMaterials.Length &&
                    slots.Select((name,index)=>palette.TryGetValue(name,out var expected) && expected==renderer.sharedMaterials[index]).All(match=>match),
                    "Renderer material paths differ from native import receipt: " + path);
            }
            Need(usedRules.Count == next.nodes.Length, "Authored mesh paths differ from recipe.");
            string geometryBefore = GeometryFingerprint(prefab, definition);
            string spatialPath = AssetDatabase.GetAssetPath(definition.SpatialData);
            // Protect every existing file except the explicit changed .mat files and prefab metadata file.
            var mutable = new HashSet<string>(changes.Select(c => Path.GetFullPath(c.path)), StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(prefabPath) };
            var protectedFiles = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath).Where(p => !mutable.Contains(p)).ToDictionary(p => p, HashFile, StringComparer.OrdinalIgnoreCase);
            string nextRecipeHash = Hash(Encoding.UTF8.GetBytes(nextText));
            string nextHash = changes.Count == 0 ? expectedContentHash : Hash(Encoding.UTF8.GetBytes(
                Strategy + "\n" + expectedContentHash + "\n" + sourceFbxSha256.ToLowerInvariant() + "\n" + nextRecipeHash));
            receiptPath = Path.GetFullPath(receiptPath);
            Need(string.Equals(Path.GetExtension(receiptPath), ".json", StringComparison.OrdinalIgnoreCase), "Receipt must be a JSON file.");
            Need(!File.Exists(receiptPath) && !protectedFiles.ContainsKey(receiptPath), "Receipt must be a new file.");
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            var receipt = new Receipt { status = "PREPARED", strategy = Strategy, mapId = mapId,
                sourceFbxSha256 = sourceFbxSha256.ToLowerInvariant(), previousContentHash = expectedContentHash, contentHash = nextHash,
                newRecipePath = Path.GetFullPath(newRecipePath), newRecipeSha256 = nextRecipeHash,
                prefab = prefabPath, prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath), spatialDataPreserved = spatialPath,
                geometryFingerprint = geometryBefore, changes = changes.ToArray(),
                pending = new[] { "Native material readback and render", "Saved scene hash inheritance and central registry refresh", "Lighting/bloom/Gameplay unchanged by this repair" } };
            // Reserve the receipt and check write permissions before mutating assets.
            using (var stream = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(receipt, true));
            if (changes.Count == 0)
            {
                receipt.status = "NO_CHANGE"; WriteReceipt(receiptPath, receipt); return receiptPath;
            }
            try
            {
                foreach (var change in changes)
                {
                    Material material = palette[change.name];
                    HiggsfieldEnvironmentImporter.ApplyEmission(material, next.materials.Single(m => m.sourceName == change.name));
                    EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
                    Need(Close(material.GetVector("_BaseColor"), V(change.baseRgb)) && Close(material.GetVector("_EmissionColor"), V(change.afterEmission)), "Material readback differs: " + change.path);
                    Need(material.IsKeywordEnabled("_EMISSION") == change.afterKeyword && (int)material.globalIlluminationFlags == change.afterGiFlags, "Emission keyword/flags readback differs.");
                    Need(AssetDatabase.AssetPathToGUID(change.path) == change.guid, "Material GUID changed.");
                    change.afterFileSha256 = HashFile(change.path);
                }
                definition.ContentHash = nextHash;
                EditorUtility.SetDirty(definition);
                PrefabUtility.SavePrefabAsset(prefab, out bool saved);
                Need(saved, "Could not save ContentHash metadata on existing prefab.");
                Need(GeometryFingerprint(prefab, definition) == geometryBefore && AssetDatabase.GetAssetPath(definition.SpatialData) == spatialPath, "Prefab geometry/SpatialData changed.");
                foreach (var file in protectedFiles) Need(HashFile(file.Key) == file.Value, "Protected file changed: " + file.Key);
                Need(AssetDatabase.AssetPathToGUID(prefabPath) == receipt.prefabGuid, "Prefab GUID changed.");
                receipt.status = "APPLIED_MATERIALS_AND_CONTENT_HASH_ONLY";
                WriteReceipt(receiptPath, receipt);
                return receiptPath;
            }
            catch (Exception error)
            {
                var rollbackErrors = new List<string>();
                foreach (var backup in backups)
                    try { EditorJsonUtility.FromJsonOverwrite(backup.Value, backup.Key); EditorUtility.SetDirty(backup.Key); AssetDatabase.SaveAssetIfDirty(backup.Key); }
                    catch (Exception e) { rollbackErrors.Add(e.Message); }
                try { definition.ContentHash = expectedContentHash; EditorUtility.SetDirty(definition); PrefabUtility.SavePrefabAsset(prefab, out bool restored); if (!restored) rollbackErrors.Add("Prefab hash rollback save failed"); }
                catch (Exception e) { rollbackErrors.Add(e.Message); }
                receipt.status = rollbackErrors.Count == 0 ? "ROLLED_BACK" : "FAILED_ROLLBACK_REQUIRES_REVIEW";
                receipt.error = error.Message; receipt.rollbackErrors = rollbackErrors.ToArray(); WriteReceipt(receiptPath, receipt);
                throw new InvalidOperationException("Emission repair failed; inspect " + receiptPath, error);
            }
        }

        static string GeometryFingerprint(GameObject prefab, EnvironmentMapDefinition definition)
        {
            var lines = new List<string>();
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Need(component != null, "Missing script in prefab.");
                if (component == definition) continue; // Only ContentHash on this component is deliberately changed.
                lines.Add(Relative(component.transform, prefab.transform) + "|" + component.GetType().FullName + "|" + EditorJsonUtility.ToJson(component));
            }
            var copy = new DefinitionSnapshot { MapId = definition.MapId, GeometryContract = definition.GeometryContract,
                SpatialData = definition.SpatialData, HumanSpawnPoints = definition.HumanSpawnPoints,
                MosquitoSpawnPoints = definition.MosquitoSpawnPoints, LobbySpawnPoints = definition.LobbySpawnPoints,
                ToolPickupPoints = definition.ToolPickupPoints, PresentationAnchors = definition.PresentationAnchors, PlayBounds = definition.PlayBounds };
            lines.Add(JsonUtility.ToJson(copy)); // All current definition fields except ContentHash.
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }
        static string Relative(Transform node, Transform root)
        {
            if (node == root) return "$root";
            var parts = new List<string>();
            for (; node != root; node = node.parent) { Need(node != null, "Transform outside expected root."); parts.Add(node.name); }
            parts.Reverse(); return string.Join("/", parts);
        }
        static bool Close(Vector4 a, Vector4 b) => Enumerable.Range(0, 4).All(i => Mathf.Abs(a[i] - b[i]) <= 1e-6f * Mathf.Max(1, Mathf.Abs(a[i]), Mathf.Abs(b[i])));
        static float[] A(Vector4 v) => new[] { v.x, v.y, v.z, v.w };
        static Vector4 V(float[] a) => new Vector4(a[0], a[1], a[2], a[3]);
        static bool SameHash(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        static string HashFile(string path) => Hash(File.ReadAllBytes(path));
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Need(bool value, string message) => HiggsfieldImportContract.Need(value, message);
        static void WriteReceipt(string path, Receipt receipt) => File.WriteAllText(path, JsonUtility.ToJson(receipt, true));
        [Serializable] sealed class ImportedPalette { public ImportedSlots[] meshes; }
        [Serializable] sealed class ImportedSlots { public string path; public string[] materialSlots; }
        [Serializable] sealed class DefinitionSnapshot
        {
            public string MapId, GeometryContract;
            public TextAsset SpatialData;
            public Transform[] HumanSpawnPoints, MosquitoSpawnPoints, LobbySpawnPoints, ToolPickupPoints;
            public Transform PresentationAnchors;
            public Bounds PlayBounds;
        }
        [Serializable] sealed class MaterialChange
        {
            public string path, guid, name, beforeFileSha256, afterFileSha256;
            public float[] baseRgb, beforeEmission, afterEmission;
            public bool beforeKeyword, afterKeyword;
            public int beforeGiFlags, afterGiFlags;
        }
        [Serializable] sealed class Receipt
        {
            public string status, strategy, mapId, sourceFbxSha256, previousContentHash, contentHash, newRecipePath, newRecipeSha256;
            public string prefab, prefabGuid, spatialDataPreserved, geometryFingerprint, error;
            public MaterialChange[] changes;
            public string[] pending, rollbackErrors;
        }
    }
}
