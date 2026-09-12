using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Editor
{
    /// <summary>Director invokes in the resident Editor. Never starts a bake, render or Play mode.</summary>
    public static class EnvironmentSampleBuilder
    {
        public const string Output = "Assets/LetMeSleep/Content/Environment/RoomSample";
        public const string ScenePath = Output + "/Scenes/RoomSample.unity";
        const string DoorPrefab = Output + "/Prefabs/Door_01.prefab";
        const string RoomPrefab = Output + "/Prefabs/RoomSample.prefab";
        static readonly string[] MaterialNames = { "Plaster_Warm", "Ceiling_Cream", "Wood_Honey", "Wood_Edge", "Floor_Oak", "Textile_Navy", "Textile_Blue", "Linen", "Iron", "Glass_Blue_Opaque" };
        static readonly Color[] Colors = {
            new Color(.68f,.61f,.48f), new Color(.80f,.76f,.65f), new Color(.36f,.19f,.073f),
            new Color(.21f,.095f,.034f), new Color(.29f,.155f,.069f), new Color(.065f,.16f,.29f),
            new Color(.18f,.34f,.48f), new Color(.86f,.80f,.64f), new Color(.048f,.062f,.068f), new Color(.30f,.47f,.53f)
        };

        [Serializable] public class Manifest { public RendererRule[] renderers; public ColliderRule[] collider_children; public Anchor[] anchors; }
        [Serializable] public class RendererRule {
            public string node, mobility, light_probe_usage, reflection_probe_usage;
            public bool contribute_gi, cast_shadows, receive_shadows, static_occluder, static_occludee;
        }
        [Serializable] public class ColliderRule { public string parent_node, child_name, collision_role; public float[] center, size; }
        [Serializable] public class Anchor { public string name, role; public float[] position, size; }
        [Serializable] public class Receipt {
            public string unityVersion, generatedUtc, scene, sourceGeometrySha256, sourceManifestSha256;
            public int meshes, colliders, sharedMaterials, anchors;
            public string[] checks, pending, warnings;
        }
        static readonly List<string> Warnings = new List<string>();

        [MenuItem("Let Me Sleep/Environment/Build Room Sample")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build requires idle Edit mode.");
            // No existing scene is discarded or implicitly saved, including unsaved user work.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == ScenePath)
                    throw new InvalidOperationException("Close the generated RoomSample scene before rebuilding. Existing scene preserved.");
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string source = Path.Combine(repository, "art_source/unity/environments/room_sample");
            foreach (string file in new[] { "room_furnished_without_door.fbx", "door_01.fbx", "presentation_manifest.json", "room_contract.json" })
                if (!File.Exists(Path.Combine(source, file))) throw new FileNotFoundException("Source sample missing", file);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is required. Director must finish package setup.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(source, "presentation_manifest.json")));
            Require(manifest != null && manifest.renderers != null && manifest.renderers.Length == 55, "Manifest must describe 55 renderers.");
            Require(manifest.collider_children != null && manifest.collider_children.Length == 45, "Manifest must describe 45 collider children.");
            Warnings.Clear();
            foreach (string dir in new[] { "Models", "Materials", "Prefabs", "Scenes", "Data" }) EnsureFolder(Output + "/" + dir);
            var materials = EnsureMaterials(shader);
            CopySource(source, "presentation_manifest.json", "Data");
            CopySource(source, "room_contract.json", "Data");
            ImportModel(source, "room_furnished_without_door.fbx", true, materials);
            ImportModel(source, "door_01.fbx", false, materials);

            Scene previous = SceneManager.GetActiveScene();
            Scene review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(review);
            try
            {
                GameObject door = new GameObject("LMS_Door_01");
                try
                {
                    InstantiateModel("door_01.fbx", "DoorVisual", door.transform);
                    ApplyRenderers(door, manifest, materials);
                    AddColliders(door, manifest, true);
                    Transform hinge = Find(door, "Door_01_Hinge");
                    Require(Vector3.Distance(hinge.position, new Vector3(.585f, 0, .045f)) < .001f, "Imported hinge axes/origin disagree with contract.");
                    Require(Vector3.Dot(hinge.up, Vector3.up) > .999f && Vector3.Dot(hinge.right, Vector3.right) > .999f,
                        "Imported hinge basis must match Unity axes before attaching local collider.");
                    Rigidbody body = hinge.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true; body.useGravity = false;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    Require(door.GetComponentsInChildren<MeshRenderer>(true).Length == 11, "Unexpected door mesh count.");
                    Require(PrefabUtility.SaveAsPrefabAsset(door, DoorPrefab) != null, "Door prefab save failed.");
                }
                finally { Object.DestroyImmediate(door); }

                GameObject room = new GameObject("LMS_RoomSample_01_Prefab");
                InstantiateModel("room_furnished_without_door.fbx", "EnvironmentVisual", room.transform);
                ApplyRenderers(room, manifest, materials);
                AddColliders(room, manifest, false);
                var doorInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab), review);
                doorInstance.transform.SetParent(room.transform, false);
                AddAnchors(room, manifest);
                Transform bed = Find(room, "Furniture_Bed");
                var lod = bed.gameObject.AddComponent<LODGroup>();
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(new[] { new LOD(0f, bed.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds(); // LOD0 only. No optimized variants or distance culling claimed.
                ValidateRoom(room, manifest);
                Require(PrefabUtility.SaveAsPrefabAsset(room, RoomPrefab) != null, "Room prefab save failed.");
                Object.DestroyImmediate(room);
                room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab), review);
                // Saved prefab remains closed; scene starts open so the doorway can be reviewed.
                Transform sceneHinge = Find(room, "Door_01_Hinge");
                sceneHinge.localRotation = Quaternion.AngleAxis(-100, Vector3.up);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sceneHinge);
                AddReviewSetup(review);
                Require(EditorSceneManager.SaveScene(review, ScenePath), "Sample scene save failed.");
                AssetDatabase.SaveAssets();
                var receipt = new Receipt {
                    unityVersion = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("o"), scene = ScenePath,
                    sourceGeometrySha256 = Hash(Path.Combine(source,"room_furnished_without_door.fbx")),
                    sourceManifestSha256 = Hash(Path.Combine(source,"presentation_manifest.json")),
                    meshes = room.GetComponentsInChildren<MeshRenderer>(true).Length,
                    colliders = room.GetComponentsInChildren<BoxCollider>(true).Length,
                    sharedMaterials = materials.Count, anchors = manifest.anchors.Length,
                    checks = new[] { "Imported room bounds and hinge origin", "55 renderers; shared URP materials", "45 independent collider children", "Static meshes have UV2", "Root scale and forward", "Separate nested door prefab with moving collider", "Prefab and sample scene serialized through Unity API" },
                    pending = new[] { "UV2 overlap and actual bake padding", "Visual review and light leakage", "Gameplay obstruction/camera traversal", "Performance and WAN outside this check" },
                    warnings = Warnings.ToArray()
                };
                File.WriteAllText(Path.Combine(repository, "docs/unity/environment/UNITY-IMPORT-RECEIPT.json"), JsonUtility.ToJson(receipt, true) + "\n");
                Debug.Log("LMS_ENVIRONMENT_SAMPLE_BUILT " + JsonUtility.ToJson(receipt));
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (review.IsValid() && review.isLoaded) EditorSceneManager.CloseScene(review, true);
            }
        }

        static string Hash(string file)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        static void CopySource(string source, string file, string folder)
        {
            string dest = Output + "/" + folder + "/" + file;
            File.Copy(Path.Combine(source, file), Path.Combine(Application.dataPath, "..", dest), true);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceSynchronousImport);
        }
        static Dictionary<string, Material> EnsureMaterials(Shader shader)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            for (int i = 0; i < MaterialNames.Length; i++)
            {
                string name = MaterialNames[i], path = Output + "/Materials/" + name + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                // Preserve an existing asset so W2 can replace the provisional recipe safely.
                if (mat == null)
                {
                    mat = new Material(shader) { name = name, enableInstancing = true };
                    mat.SetColor("_BaseColor", Colors[i]);
                    mat.SetFloat("_Smoothness", name == "Iron" ? .52f : .22f);
                    mat.SetFloat("_Metallic", name == "Iron" ? .55f : 0f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                Require(mat.shader != null && mat.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal), "Existing material must be URP: " + path);
                result.Add(name, mat);
            }
            return result;
        }
        static void ImportModel(string source, string name, bool lightmapped, Dictionary<string, Material> materials)
        {
            CopySource(source, name, "Models");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Output + "/Models/" + name);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true; // sample import audit reads UV2; revisit after native acceptance
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.generateSecondaryUV = lightmapped;
            importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
            importer.secondaryUVMinLightmapResolution = 16;
            importer.secondaryUVMinObjectScale = 1;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }
        static void InstantiateModel(string name, string instanceName, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Models/" + name);
            Require(source != null, "Model failed to import: " + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
            // Own the hierarchy while preserving Mesh/Material asset references. This avoids
            // losing renderer/physics edits as unrecorded nested Model Prefab overrides.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
        }
        static Transform Find(GameObject root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            Require(matches.Length == 1, "Expected one node " + name + ", found " + matches.Length);
            return matches[0];
        }
        static void ApplyRenderers(GameObject root, Manifest manifest, Dictionary<string, Material> materials)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                RendererRule rule = manifest.renderers.Single(r => r.node == renderer.name);
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    Require(slots[i] != null, "Missing imported material on " + renderer.name);
                    string name = slots[i].name;
                    Require(materials.ContainsKey(name), "Unexpected material " + name + " on " + renderer.name);
                    slots[i] = materials[name];
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = rule.cast_shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = rule.receive_shadows;
                bool dynamic = rule.mobility == "dynamic";
                renderer.receiveGI = dynamic ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps;
                renderer.lightProbeUsage = dynamic ? LightProbeUsage.BlendProbes : LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                StaticEditorFlags flags = 0;
                if (rule.contribute_gi) flags |= StaticEditorFlags.ContributeGI;
                if (rule.static_occluder) flags |= StaticEditorFlags.OccluderStatic;
                if (rule.static_occludee) flags |= StaticEditorFlags.OccludeeStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
            }
        }
        static Vector3 V(float[] values)
        {
            Require(values != null && values.Length == 3, "Expected XYZ vector");
            return new Vector3(values[0], values[1], values[2]);
        }
        static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
            string warning = "Missing layer " + name + "; sample colliders use Default. Director must finalize camera/physics masks.";
            if (!Warnings.Contains(warning)) { Warnings.Add(warning); Debug.LogWarning(warning); }
            return 0;
        }
        static void AddColliders(GameObject root, Manifest manifest, bool door)
        {
            foreach (var rule in manifest.collider_children)
            {
                bool moving = rule.parent_node == "Door_01_Hinge";
                if (moving != door) continue;
                Transform parent = rule.parent_node == "LMS_RoomSample_01" ? root.transform : Find(root, rule.parent_node);
                Require(Quaternion.Angle(parent.rotation, Quaternion.identity) < .01f,
                    "Collider parent axes differ from Unity-local manifest: " + rule.parent_node);
                var child = new GameObject(rule.child_name);
                child.transform.SetParent(parent, false);
                child.layer = Layer(rule.collision_role);
                BoxCollider collider = child.AddComponent<BoxCollider>();
                collider.center = V(rule.center); collider.size = V(rule.size);
            }
        }
        static void AddAnchors(GameObject root, Manifest manifest)
        {
            var parent = new GameObject("PresentationAnchors");
            parent.transform.SetParent(root.transform, false);
            foreach (var anchor in manifest.anchors)
            {
                var child = new GameObject(anchor.name);
                child.transform.SetParent(parent.transform, false);
                child.transform.localPosition = V(anchor.position);
                // Volume extents remain data, never transform scale or hidden trigger geometry.
            }
        }
        static void ValidateRoom(GameObject room, Manifest manifest)
        {
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                Require(Vector3.Distance(t.localScale, Vector3.one) < .001f, "Non-unit scale: " + t.name);
            Require(room.GetComponentsInChildren<MeshRenderer>(true).Length == 55, "Mesh count changed or door duplicated.");
            Require(room.GetComponentsInChildren<BoxCollider>(true).Length == 45, "Collider count mismatch.");
            Require(room.GetComponentsInChildren<Rigidbody>(true).Length == 1, "Expected only the kinematic door body.");
            var hinge = Find(room, "Door_01_Hinge");
            var leaf = Find(room, "Door_01_Leaf").GetComponent<MeshRenderer>();
            Require(Vector3.Distance(leaf.bounds.center, new Vector3(1.12f,1.1025f,.045f)) < .002f,
                "Closed leaf center differs from pivot contract.");
            Quaternion rest = hinge.localRotation;
            try
            {
                hinge.localRotation = Quaternion.AngleAxis(-100, Vector3.up);
                float angle = -100 * Mathf.Deg2Rad;
                Vector3 expected = new Vector3(.585f + .535f * Mathf.Cos(angle), 1.1025f, .045f - .535f * Mathf.Sin(angle));
                Require(Vector3.Distance(leaf.bounds.center, expected) < .002f, "Leaf does not rotate around the documented hinge.");
            }
            finally { hinge.localRotation = rest; }
            var shell = Find(room, "Architecture_Shell").GetComponent<MeshRenderer>();
            Require(Vector3.Distance(shell.bounds.min, new Vector3(-.18f, -.18f, -.18f)) < .002f &&
                    Vector3.Distance(shell.bounds.max, new Vector3(4.98f, 3, 4.58f)) < .002f, "Imported shell metres/axes differ from contract.");
            foreach (var mesh in room.GetComponentsInChildren<MeshFilter>(true))
            {
                Require(mesh.sharedMesh != null, "Missing mesh: " + mesh.name);
                var rule = manifest.renderers.Single(r => r.node == mesh.name);
                if (rule.mobility == "static") Require(mesh.sharedMesh.uv2.Length == mesh.sharedMesh.vertexCount, "UV2 generation failed: " + mesh.name);
            }
            Require(room.GetComponentsInChildren<Light>(true).Length == 0, "Source prefab must not own presentation lights.");
        }
        static void AddReviewSetup(Scene scene)
        {
            var root = new GameObject("ReviewOnly_LightingAndCameras");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .24f, .28f);
            AddLight(root.transform, "Review_WarmCeiling", new Vector3(2.4f,2.55f,2.2f), new Color(1,.80f,.57f), 4f, 7f);
            AddLight(root.transform, "Review_WindowFill", new Vector3(3.6f,2.1f,4.05f), new Color(.65f,.78f,1), 1.5f, 5f);
            AddCamera(root.transform, "Review_HumanEye_1_53m", new Vector3(1.25f,1.53f,1.35f), new Vector3(3.3f,1.1f,3.5f), true);
            AddCamera(root.transform, "Review_Door", new Vector3(2.5f,1.53f,2.1f), new Vector3(.9f,1.1f,0), false);
            AddCamera(root.transform, "Review_Mosquito", new Vector3(2.2f,2.45f,2.4f), new Vector3(3.5f,.7f,3.6f), false);
        }
        static void AddLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color;
            light.intensity = intensity; light.range = range; light.shadows = LightShadows.Soft;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }
        static void AddCamera(Transform parent, string name, Vector3 position, Vector3 target, bool active)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
            go.transform.LookAt(target);
            var camera = go.AddComponent<Camera>(); camera.enabled = active; camera.nearClipPlane = .03f;
            camera.farClipPlane = 30; camera.fieldOfView = 75; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f,.09f,.13f);
            if (active) go.tag = "MainCamera";
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
