using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Content.Environment;
using LetMeSleep.Content.Environment.Higgsfield;
using LetMeSleep.Gameplay.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Editor.Higgsfield
{
    /// <summary>Explicit manual import of a final export. Never an AssetPostprocessor or auto-build hook.</summary>
    public static class HiggsfieldEnvironmentImporter
    {
        public const string Output = "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps";
        const string GeometryContract = "lms-higgsfield-environment-1";

        [MenuItem("Let Me Sleep/Higgsfield/Import Final Environment Recipe")]
        public static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanel("Final Higgsfield FBX recipe", "", "json");
            if (!string.IsNullOrEmpty(path)) Debug.Log(Import(path));
        }

        // -executeMethod LetMeSleep.Content.Editor.Higgsfield.HiggsfieldEnvironmentImporter.ImportFromCommandLine
        // -higgsfieldRecipe "N:/.../final.recipe.json"
        public static void ImportFromCommandLine()
        {
            try
            {
                string[] args = System.Environment.GetCommandLineArgs();
                int[] indices = Enumerable.Range(0, args.Length).Where(i => args[i] == "-higgsfieldRecipe").ToArray();
                Need(indices.Length == 1 && indices[0] + 1 < args.Length && !args[indices[0] + 1].StartsWith("-", StringComparison.Ordinal), "Exactly one -higgsfieldRecipe <path> argument required.");
                Debug.Log(Import(args[indices[0] + 1]));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static string Import(string recipePath)
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Idle Edit mode required.");
            bool singleEmptyBatchScene = ValidateSceneSetup(); // Fail before importing/writing any assets.
            recipePath = Path.GetFullPath(recipePath);
            string recipeJson = File.ReadAllText(recipePath);
            var recipe = JsonUtility.FromJson<HiggsfieldImportContract>(recipeJson);
            Need(recipe != null, "Recipe missing.");
            recipe.Validate();
            string source = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(recipePath), recipe.sourceFbx));
            Need(File.Exists(source) && string.Equals(Path.GetExtension(source), ".fbx", StringComparison.OrdinalIgnoreCase), "Final FBX missing.");
            Need(Hash(File.ReadAllBytes(source)).Equals(recipe.sourceSha256, StringComparison.OrdinalIgnoreCase), "Final FBX SHA256 mismatch.");
            int layer = LayerMask.NameToLayer(recipe.collisionLayer);
            Need(layer >= 0, "Required collision layer does not exist: " + recipe.collisionLayer);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Need(shader != null, "URP/Lit unavailable.");
            string destination = Output + "/" + recipe.mapId;
            Need(!Directory.Exists(destination) && !AssetDatabase.IsValidFolder(destination), "Output exists; use a new map revision ID. Existing assets are never overwritten.");
            // Every mutation below is confined to this new destination. Failure deliberately leaves
            // import-incomplete.txt for inspection; it must not be registered as playable content.
            foreach (string dir in new[] { "Models", "Materials", "Prefabs", "Scenes", "Data" }) Folder(destination + "/" + dir);
            File.WriteAllText(destination + "/Data/import-incomplete.txt", "Import has not completed. Do not register this map.");
            string modelPath = destination + "/Models/Environment.fbx";
            File.Copy(source, modelPath, false);
            Need(Hash(File.ReadAllBytes(modelPath)).Equals(recipe.sourceSha256, StringComparison.OrdinalIgnoreCase), "Copied source changed during import.");
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            Need(importer != null, "FBX importer unavailable.");
            importer.globalScale = recipe.importScale;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Need(model != null, "No FBX root imported.");
            string configPath = destination + "/Data/import-recipe.json";
            File.WriteAllText(configPath, recipeJson);
            AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceSynchronousImport);
            string contentHash = Hash(Encoding.UTF8.GetBytes(GeometryContract + "\n" + recipe.sourceSha256.ToLowerInvariant() + "\n" + recipeJson));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, singleEmptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject(recipe.mapId);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                visual.transform.SetParent(root.transform, false);
                Need(visual.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Static environment FBX required.");
                Need(visual.GetComponentsInChildren<Collider>(true).Length == 0, "Unexpected imported colliders.");
                var paths = NodePaths(visual.transform);
                var renderers = visual.GetComponentsInChildren<Renderer>(true).OrderBy(r => Relative(r.transform, visual.transform), StringComparer.Ordinal).ToArray();
                Need(renderers.Length > 0, "FBX has no static mesh renderers.");
                foreach (var rule in recipe.nodes) Need(paths.ContainsKey(rule.path), "Rule does not match an imported node: " + rule.path);
                var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                for (int i = 0; i < recipe.materials.Length; i++)
                {
                    var swatch = recipe.materials[i];
                    var material = new Material(shader) { name = swatch.sourceName };
                    var color = new Color(swatch.rgb[0], swatch.rgb[1], swatch.rgb[2], 1);
                    material.SetColor("_BaseColor", swatch.colorSpace == "linear" ? color.gamma : color);
                    material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0);
                    material.SetFloat("_Surface", 0); // Flat colour, opaque; no realistic texture or transparency dependency.
                    ApplyEmission(material, swatch);
                    AssetDatabase.CreateAsset(material, destination + "/Materials/Color_" + i.ToString("000") + ".mat");
                    materials.Add(swatch.sourceName, material);
                }
                var surfaceEntries = new List<SurfaceEntry>();
                var meshEntries = new List<MeshEntry>();
                var usedRules = new HashSet<string>(StringComparer.Ordinal);
                int waterCount = 0;
                foreach (var renderer in renderers)
                {
                    string path = Relative(renderer.transform, visual.transform);
                    var rule = recipe.Resolve(path);
                    usedRules.Add(rule.path);
                    var skinned = renderer as SkinnedMeshRenderer;
                    var filter = renderer.GetComponent<MeshFilter>();
                    Need(renderer is MeshRenderer || skinned != null, "Unsupported renderer: " + path);
                    Mesh mesh = skinned != null ? skinned.sharedMesh : (filter == null ? null : filter.sharedMesh);
                    Need(mesh != null, "Missing mesh: " + path);
                    if (skinned != null)
                    {
                        Need((rule.kind == "water" || rule.kind == "foam") && skinned.bones.Length == 0, "Only boneless water blendshapes supported as SkinnedMeshRenderer: " + path);
                        Need(HiggsfieldLowPolyWater.FindShape(mesh, "Wave_A") >= 0 && HiggsfieldLowPolyWater.FindShape(mesh, "Wave_B") >= 0, "Missing/ambiguous Wave_A or Wave_B on " + path);
                    }
                    Need(mesh.vertexCount > 0 && mesh.normals.Length == mesh.vertexCount, "Authored normals missing: " + path);
                    Need(mesh.vertices.All(Finite), "Non-finite mesh coordinates: " + path);
                    Need(Finite(renderer.transform.lossyScale) && renderer.transform.lossyScale.x > 0 && renderer.transform.lossyScale.y > 0 && renderer.transform.lossyScale.z > 0, "Apply negative/zero scales before export: " + path);
                    var oldMaterials = renderer.sharedMaterials;
                    Need(oldMaterials.Length == mesh.subMeshCount, "Material/submesh count mismatch: " + path);
                    renderer.sharedMaterials = oldMaterials.Select(m => {
                        Need(m != null && materials.ContainsKey(m.name), "Missing explicit material swatch on " + path + ": " + (m == null ? "<null>" : m.name));
                        return materials[m.name];
                    }).ToArray();
                    meshEntries.Add(new MeshEntry { path = path, kind = rule.kind, vertices = mesh.vertexCount, submeshes = mesh.subMeshCount,
                        materialSlots = oldMaterials.Select(m => m.name).ToArray(), waterMode = rule.kind == "water" || rule.kind == "foam" ? (skinned != null ? "authored-blendshapes" : "private-mesh-cpu") : "none" });
                    renderer.shadowCastingMode = rule.kind == "water" || rule.kind == "foam" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true; // Includes foliage/branches; collider exclusion does not disable shadows.
                    if (rule.kind == "solid")
                    {
                        renderer.gameObject.layer = layer;
                        var collider = renderer.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = mesh; collider.convex = false;
                        var surface = renderer.gameObject.AddComponent<GameplaySurface>();
                        surface.SurfaceId = checked(recipe.firstSurfaceId + (uint)surfaceEntries.Count);
                        surface.CanPerch = rule.canPerch; surface.Revision = 1;
                        surfaceEntries.Add(new SurfaceEntry { path = path, id = surface.SurfaceId });
                    }
                    if (rule.kind == "water" || rule.kind == "foam")
                    {
                        Need(skinned != null || mesh.vertexCount <= HiggsfieldLowPolyWater.MaximumAnimatedVertices, "Water exceeds CPU animation vertex budget: " + path);
                        var water = renderer.gameObject.AddComponent<HiggsfieldLowPolyWater>();
                        water.Amplitude = rule.waveAmplitude; water.Wavelength = rule.waveLength; water.Speed = rule.waveSpeed;
                        waterCount++;
                    }
                    // Do not static-batch water; keep authored hierarchy and mesh topology for every object.
                }
                Need(usedRules.Count == recipe.nodes.Length, "Unused/shadowed node rules; remove stale classification.");
                Need(surfaceEntries.Count > 0, "No solid playable geometry classified.");
                var definition = root.AddComponent<EnvironmentMapDefinition>();
                definition.MapId = recipe.mapId; definition.ContentHash = contentHash;
                definition.GeometryContract = GeometryContract;
                definition.SpatialData = AssetDatabase.LoadAssetAtPath<TextAsset>(configPath);
                definition.HumanSpawnPoints = Markers(paths, recipe.humanSpawns);
                definition.MosquitoSpawnPoints = Markers(paths, recipe.mosquitoSpawns);
                definition.LobbySpawnPoints = Markers(paths, recipe.lobbySpawns);
                definition.ToolPickupPoints = Markers(paths, recipe.toolPickups);
                definition.PresentationAnchors = string.IsNullOrEmpty(recipe.presentationRoot) ? null : Find(paths, recipe.presentationRoot);
                Vector3 min = string.IsNullOrEmpty(recipe.boundsMinEmpty) ? V(recipe.playBoundsMin) : Empty(paths, recipe.boundsMinEmpty).position;
                Vector3 max = string.IsNullOrEmpty(recipe.boundsMaxEmpty) ? V(recipe.playBoundsMax) : Empty(paths, recipe.boundsMaxEmpty).position;
                Need(Finite(min) && Finite(max) && max.x > min.x && max.y > min.y && max.z > min.z, "Invalid imported bounds; check FBX axes/metres.");
                definition.PlayBounds = new Bounds((min + max) * .5f, max - min);
                var spawns = definition.HumanSpawnPoints.Concat(definition.MosquitoSpawnPoints).Concat(definition.LobbySpawnPoints).ToArray();
                foreach (var spawn in spawns) Need(definition.PlayBounds.Contains(spawn.position), "Spawn outside playable bounds: " + spawn.name);
                for (int i = 0; i < spawns.Length; i++)
                    for (int j = i + 1; j < spawns.Length; j++)
                        Need(Vector3.Distance(spawns[i].position, spawns[j].position) > .1f, "Overlapping spawn marker origins: " + spawns[i].name + "/" + spawns[j].name);
                Need(root.GetComponentsInChildren<Light>(true).Length == 0 && root.GetComponentsInChildren<Camera>(true).Length == 0, "Unexpected camera/light import.");
                string prefabPath = destination + "/Prefabs/" + recipe.mapId + ".prefab";
                string scenePath = destination + "/Scenes/" + recipe.mapId + ".unity";
                Need(PrefabUtility.SaveAsPrefabAsset(root, prefabPath) != null, "Prefab save failed.");
                Object.DestroyImmediate(root);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
                var saved = instance.GetComponent<EnvironmentMapDefinition>();
                Need(saved != null && saved.ContentHash == contentHash && saved.HumanSpawnPoints.All(t => t != null) && saved.MosquitoSpawnPoints.All(t => t != null), "Prefab references failed to round trip.");
                Need(instance.GetComponentsInChildren<Collider>(true).Length == surfaceEntries.Count, "Collider count changed during prefab serialization.");
                Need(EditorSceneManager.SaveScene(scene, scenePath), "Scene save failed.");
                var receipt = new Receipt {
                    status = "IMPORTED_REQUIRES_NATIVE_REVIEW", mapId = recipe.mapId, unityVersion = Application.unityVersion,
                    sourceSha256 = recipe.sourceSha256.ToLowerInvariant(), contentHash = contentHash,
                    prefab = prefabPath, scene = scenePath, meshRenderers = renderers.Length, staticColliders = surfaceEntries.Count,
                    animatedWaterAndFoam = waterCount, humanSpawns = saved.HumanSpawnPoints.Length, mosquitoSpawns = saved.MosquitoSpawnPoints.Length,
                    playBoundsMin = A(saved.PlayBounds.min), playBoundsMax = A(saved.PlayBounds.max),
                    markers = saved.HumanSpawnPoints.Concat(saved.MosquitoSpawnPoints).Select(t => new MarkerEntry { name = t.name, worldPosition = A(t.position) }).ToArray(),
                    has16MarkersPerRole = saved.HumanSpawnPoints.Length >= 16 && saved.MosquitoSpawnPoints.Length >= 16,
                    surfaces = surfaceEntries.ToArray(),
                    meshes = meshEntries.ToArray(),
                    pending = new[] { "FBX axes/metres against authored dimensions", "16-player role capacity and spawn capsule/flight clearance", "Routes, stairs, doors and bounds enforcement", "Water motion/foam seams and native performance", "Lighting/cameras and visual approval", "RoomRules, UI/Online registry and authoritative content hash integration" }
                };
                string receiptPath = destination + "/Data/import-receipt.json";
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
                AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.DeleteAsset(destination + "/Data/import-incomplete.txt");
                if (File.Exists(destination + "/Data/import-incomplete.txt")) File.Delete(destination + "/Data/import-incomplete.txt");
                return receiptPath;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (!singleEmptyBatchScene && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool ValidateSceneSetup()
        {
            if (Application.isBatchMode && SceneManager.sceneCount == 1)
            {
                Scene initial = SceneManager.GetSceneAt(0);
                if (initial.isLoaded && string.IsNullOrEmpty(initial.path) && initial.rootCount == 0 && !initial.isDirty)
                    return true; // Only the empty, clean initial batch scene may be replaced.
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Need(!string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path), "Cannot add a generated scene alongside an Untitled scene. Save or close it manually first; this importer never saves user scenes. In batch, use an empty clean initial scene or a saved scene.");
            return false;
        }

        public static void ApplyEmission(Material material, HiggsfieldSwatch swatch)
        {
            Need(material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit", "Emission requires URP/Lit.");
            Need(material.HasProperty("_EmissionColor") && material.GetTexture("_EmissionMap") == null, "Only flat, untextured emission is supported.");
            float[] rgb = swatch.EmissionLinear();
            // SetVector writes linear shader data directly, avoiding SetColor's HDR gamma conversion.
            material.SetVector("_EmissionColor", new Vector4(rgb[0], rgb[1], rgb[2], 1));
            bool on = rgb.Any(v => v > 0);
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

        static Dictionary<string, Transform> NodePaths(Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = Relative(t, root);
                Need(!result.ContainsKey(path), "Ambiguous exported sibling names: " + path);
                result.Add(path, t);
            }
            return result;
        }
        static string Relative(Transform t, Transform root)
        {
            if (t == root) return "$root";
            var parts = new List<string>();
            for (; t != root; t = t.parent) { Need(t != null, "Node outside model root."); parts.Add(t.name); }
            parts.Reverse(); return string.Join("/", parts);
        }
        static Transform Find(Dictionary<string, Transform> nodes, string path) { Need(nodes.ContainsKey(path), "Missing exported node: " + path); return nodes[path]; }
        static Transform Empty(Dictionary<string, Transform> nodes, string path)
        {
            var t = Find(nodes, path);
            Need(t.GetComponentsInChildren<Renderer>(true).Length == 0, "Marker must be an EMPTY, not mesh geometry: " + path);
            Need(Finite(t.position), "Non-finite marker: " + path); return t;
        }
        static Transform[] Markers(Dictionary<string, Transform> nodes, string[] paths) => (paths ?? Array.Empty<string>()).Select(p => Empty(nodes, p)).ToArray();
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        static float[] A(Vector3 v) => new[] { v.x, v.y, v.z };
        static bool Finite(Vector3 v) => HiggsfieldImportContract.Finite(v.x) && HiggsfieldImportContract.Finite(v.y) && HiggsfieldImportContract.Finite(v.z);
        static void Need(bool value, string message) => HiggsfieldImportContract.Need(value, message);
        [Serializable] sealed class SurfaceEntry { public string path; public uint id; }
        [Serializable] sealed class MarkerEntry { public string name; public float[] worldPosition; }
        [Serializable] sealed class MeshEntry { public string path, kind, waterMode; public int vertices, submeshes; public string[] materialSlots; }
        [Serializable] sealed class Receipt
        {
            public string status, mapId, unityVersion, sourceSha256, contentHash, prefab, scene;
            public int meshRenderers, staticColliders, animatedWaterAndFoam, humanSpawns, mosquitoSpawns;
            public bool has16MarkersPerRole;
            public float[] playBoundsMin, playBoundsMax;
            public MarkerEntry[] markers;
            public MeshEntry[] meshes;
            public SurfaceEntry[] surfaces;
            public string[] pending;
        }
    }
}
