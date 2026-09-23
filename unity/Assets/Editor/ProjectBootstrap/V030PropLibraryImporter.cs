using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// v0.3.0 prop library import (reproducible, idempotent):
    ///   -executeMethod LetMeSleep.Editor.V030PropLibraryImporter.ImportFromCommandLine [-v030PropsReceipt &lt;abs.json&gt;]
    /// Source: art_source/unity/environments/v030_props (one FBX per prop + manifest.json, Blender 5.2).
    /// Output: Assets/LetMeSleep/Content/Environment/V030Props/{Models,Materials,Prefabs}.
    /// - FBX copied only when the SHA-256 in the manifest matches (never edited in place).
    /// - ModelImporter: metres, axis conversion baked, no colliders, lights, cameras, blend shapes or animation;
    ///   authored flat normals kept; FBX materials remapped by name to the flat URP/Lit materials below.
    /// - Materials: URP/Lit, _BaseColor = color_srgb, no textures; *_Emissive get _EmissionColor =
    ///   linear(color_srgb) x emission_strength with _EMISSION.
    /// - Prefab per prop (Prop_X.prefab): identity root (base-centred pivot), one mesh child "Prop_X_Mesh" and
    ///   the manifest anchors as empty children "Anchor_&lt;name&gt;". The mesh child keeps the FBX node transform (Blender
    ///   exports the Z-up turn as a node rotation), so the root is identity. Visual only: no Collider, Rigidbody or Light.
    /// </summary>
    public static class V030PropLibraryImporter
    {
        public const string Root = "Assets/LetMeSleep/Content/Environment/V030Props";
        public const string ModelsRoot = Root + "/Models";
        public const string MaterialsRoot = Root + "/Materials";
        public const string PrefabsRoot = Root + "/Prefabs";
        public const string AnchorPrefix = "Anchor_";
        const string SourceFolder = "art_source/unity/environments/v030_props";
        // Flat props on the floor or on water: no shadow casting (avoids acne and floating dark rims).
        static readonly HashSet<string> NoShadowProps = new HashSet<string>(StringComparer.Ordinal) { "RugRedStriped", "RugBlue", "RugStripedRedBlue", "LilyPads" };

        sealed class Manifest { public string schema; public PropEntry[] props; }
        sealed class PropEntry
        {
            public string name, asset, fbx, fbx_sha256, category, mount;
            public Dictionary<string, float> unity_size_m;
            public int triangles;
            public MaterialEntry[] materials;
            public Dictionary<string, AnchorEntry> anchors;
        }
        sealed class MaterialEntry { public int slot; public string name, part, color_srgb; public bool emissive; public float emission_strength, roughness, metallic; }
        sealed class AnchorEntry { public float[] unity_local_m, normal_unity, up_unity; }

        [MenuItem("Let Me Sleep/Content/Import v0.3.0 Prop Library")]
        public static void ImportMenu() => Import(null);

        public static void ImportFromCommandLine()
        {
            try
            {
                Import(Argument("-v030PropsReceipt"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static JObject Import(string receiptPath)
        {
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string source = Path.Combine(repository, SourceFolder);
            var manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(Path.Combine(source, "manifest.json")));
            Need(manifest != null && manifest.schema == "lms.props.v030/1" && manifest.props != null && manifest.props.Length > 0, "Unexpected prop manifest schema.");
            foreach (var folder in new[] { Root, ModelsRoot, MaterialsRoot, PrefabsRoot }) EnsureFolder(folder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Need(lit != null, "URP/Lit shader missing.");
            var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["unity"] = Application.unityVersion, ["source"] = SourceFolder };
            var rows = new JArray();
            var names = new HashSet<string>(StringComparer.Ordinal);
            frontFlip = null;
            // The Bed goes first: its import decides the axis correction for every prop (TurnFrontToPlusZ).
            foreach (var prop in manifest.props.OrderBy(p => p.name == "Bed" ? 0 : 1))
            {
                Need(!string.IsNullOrEmpty(prop.name) && names.Add(prop.name) && prop.asset == "Prop_" + prop.name && prop.fbx == prop.asset + ".fbx", "Bad prop identity: " + prop.name);
                string fbxSource = Path.Combine(source, prop.fbx);
                byte[] bytes = File.ReadAllBytes(fbxSource);
                Need(Sha256(bytes) == prop.fbx_sha256, "FBX hash differs from the manifest: " + prop.fbx);
                string modelPath = ModelsRoot + "/" + prop.fbx;
                string modelDisk = Disk(modelPath);
                bool copied = !File.Exists(modelDisk) || Sha256(File.ReadAllBytes(modelDisk)) != prop.fbx_sha256;
                if (copied) File.WriteAllBytes(modelDisk, bytes);
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);

                // Materials first, so the FBX remap can point at them.
                var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var entry in prop.materials.OrderBy(m => m.slot))
                    materials.Add(entry.name, BuildMaterial(lit, entry));

                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                Need(importer != null, "Model importer missing: " + modelPath);
                bool changed = Configure(importer, materials);
                if (changed || copied) importer.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Need(model != null, "Imported model missing: " + modelPath);
                Need(model.GetComponentsInChildren<Collider>(true).Length == 0 && model.GetComponentsInChildren<Light>(true).Length == 0 &&
                     model.GetComponentsInChildren<Camera>(true).Length == 0, "Prop FBX must not import colliders, lights or cameras: " + prop.fbx);
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                Need(filters.Length == 1, "One mesh per prop FBX: " + prop.fbx);
                var sourceRenderer = filters[0].GetComponent<MeshRenderer>();
                Mesh mesh = filters[0].sharedMesh;
                Need(mesh != null && sourceRenderer != null, "Prop mesh/renderer missing: " + prop.fbx);
                // Blender writes the Z-up to Y-up turn as the node rotation (the mesh keeps Blender axes): keep that
                // transform on a child so the prefab root stays an identity, base-centred pivot.
                Matrix4x4 nodeMatrix = TurnFrontToPlusZ(filters[0].transform.localToWorldMatrix, mesh, prop.name);
                var slotMaterials = sourceRenderer.sharedMaterials;
                Need(slotMaterials.Length == mesh.subMeshCount && slotMaterials.All(m => m != null && materials.ContainsValue(m)),
                    "Every submesh must map to a library material: " + prop.fbx);
                int triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3);
                Need(triangles == prop.triangles, $"Triangle count {triangles} differs from the manifest {prop.triangles}: {prop.fbx}");
                Bounds bounds = Transform(nodeMatrix, mesh.bounds);
                var size = new Vector3(prop.unity_size_m["x"], prop.unity_size_m["y"], prop.unity_size_m["z"]);
                Need((bounds.size - size).magnitude < .02f && Mathf.Abs(bounds.min.y) < .01f && new Vector2(bounds.center.x, bounds.center.z).magnitude < .02f,
                    $"Imported bounds {bounds.size} / min.y {bounds.min.y} / node {nodeMatrix.rotation.eulerAngles} {nodeMatrix.lossyScale} do not match the base-centred manifest size {size}: {prop.fbx}");

                string prefabPath = PrefabsRoot + "/" + prop.asset + ".prefab";
                var root = new GameObject(prop.asset);
                try
                {
                    var meshObject = new GameObject(prop.asset + "_Mesh");
                    meshObject.transform.SetParent(root.transform, false);
                    meshObject.transform.localPosition = nodeMatrix.GetColumn(3);
                    meshObject.transform.localRotation = nodeMatrix.rotation;
                    meshObject.transform.localScale = nodeMatrix.lossyScale;
                    meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = meshObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = slotMaterials;
                    renderer.shadowCastingMode = NoShadowProps.Contains(prop.name) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    if (prop.anchors != null)
                        foreach (var pair in prop.anchors.OrderBy(p => p.Key, StringComparer.Ordinal))
                        {
                            var anchor = new GameObject(AnchorPrefix + pair.Key).transform;
                            anchor.SetParent(root.transform, false);
                            anchor.localPosition = V(pair.Value.unity_local_m);
                            Vector3 normal = pair.Value.normal_unity != null ? V(pair.Value.normal_unity) : Vector3.up;
                            // Anchors with an explicit "up" carry a full frame (e.g. the sleeping actor's root);
                            // others face along their normal, upright, or keep identity for floor/top anchors.
                            if (pair.Value.up_unity != null)
                                anchor.localRotation = Quaternion.LookRotation(normal.normalized, V(pair.Value.up_unity).normalized);
                            else
                                anchor.localRotation = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) > .999f ? Quaternion.identity
                                    : Quaternion.LookRotation(normal.normalized, Vector3.up);
                        }
                    SaveIfChanged(root, prefabPath);
                }
                finally { Object.DestroyImmediate(root); }
                rows.Add(new JObject
                {
                    ["prop"] = prop.name, ["fbx"] = modelPath, ["sha256"] = prop.fbx_sha256, ["copied"] = copied, ["prefab"] = prefabPath,
                    ["triangles"] = triangles, ["materials"] = new JArray(slotMaterials.Select(m => m.name)),
                    ["boundsSize"] = new JArray(bounds.size.x, bounds.size.y, bounds.size.z),
                    ["anchors"] = new JArray((prop.anchors ?? new Dictionary<string, AnchorEntry>()).Keys)
                });
            }
            AssetDatabase.SaveAssets();
            receipt["props"] = rows;
            receipt["count"] = rows.Count;
            Debug.Log("LMS_V030_PROPS_IMPORTED " + rows.Count + " props into " + Root);
            if (!string.IsNullOrEmpty(receiptPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
                File.WriteAllText(receiptPath, receipt.ToString(Formatting.Indented) + "\n");
            }
            return receipt;
        }

        static bool Configure(ModelImporter importer, Dictionary<string, Material> materials)
        {
            bool changed = false;
            void Set<T>(Func<T> get, Action<T> set, T value) { if (!EqualityComparer<T>.Default.Equals(get(), value)) { set(value); changed = true; } }
            Set(() => importer.globalScale, v => importer.globalScale = v, 1f);
            Set(() => importer.useFileScale, v => importer.useFileScale = v, true);
            Set(() => importer.bakeAxisConversion, v => importer.bakeAxisConversion = v, true);
            Set(() => importer.addCollider, v => importer.addCollider = v, false);
            Set(() => importer.importCameras, v => importer.importCameras = v, false);
            Set(() => importer.importLights, v => importer.importLights = v, false);
            Set(() => importer.importVisibility, v => importer.importVisibility = v, false);
            Set(() => importer.importBlendShapes, v => importer.importBlendShapes = v, false);
            Set(() => importer.importAnimation, v => importer.importAnimation = v, false);
            Set(() => importer.animationType, v => importer.animationType = v, ModelImporterAnimationType.None);
            Set(() => importer.importNormals, v => importer.importNormals = v, ModelImporterNormals.Import);
            Set(() => importer.importTangents, v => importer.importTangents = v, ModelImporterTangents.None);
            Set(() => importer.meshCompression, v => importer.meshCompression = v, ModelImporterMeshCompression.Off);
            Set(() => importer.isReadable, v => importer.isReadable = v, false);
            Set(() => importer.preserveHierarchy, v => importer.preserveHierarchy = v, false);
            Set(() => importer.materialImportMode, v => importer.materialImportMode = v, ModelImporterMaterialImportMode.ImportStandard);
            Set(() => importer.materialLocation, v => importer.materialLocation = v, ModelImporterMaterialLocation.InPrefab);
            var existing = importer.GetExternalObjectMap();
            foreach (var pair in materials)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (existing.TryGetValue(id, out var current) && current == pair.Value) continue;
                importer.AddRemap(id, pair.Value);
                changed = true;
            }
            return changed;
        }

        static Material BuildMaterial(Shader lit, MaterialEntry entry)
        {
            Need(!string.IsNullOrEmpty(entry.name) && entry.name.StartsWith("Prop_", StringComparison.Ordinal), "Bad material name " + entry.name);
            Need(entry.emissive == entry.name.EndsWith("_Emissive", StringComparison.Ordinal), "Emissive suffix mismatch: " + entry.name);
            Need(ColorUtility.TryParseHtmlString(entry.color_srgb, out Color srgb), "Bad color " + entry.color_srgb);
            string path = MaterialsRoot + "/" + entry.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(lit) { name = entry.name };
            if (material.shader != lit) material.shader = lit;
            material.SetColor("_BaseColor", srgb);
            material.SetColor("_Color", srgb);
            material.SetFloat("_Metallic", Mathf.Clamp01(entry.metallic) * .5f);
            // Flat stylized look: matte even for the "polished" parts, never mirror-like.
            material.SetFloat("_Smoothness", Mathf.Clamp01(1f - entry.roughness) * .45f);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.SetFloat("_SpecularHighlights", entry.emissive ? 0 : 1);
            if (entry.emissive)
            {
                Color linear = srgb.linear * Mathf.Max(0, entry.emission_strength);
                material.SetVector("_EmissionColor", new Vector4(linear.r, linear.g, linear.b, 1));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.SetVector("_EmissionColor", Vector4.zero);
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            material.enableInstancing = true;
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        // Blender -> FBX -> Unity lands each prop with its front (Blender -Y) toward Unity -Z, while the manifest anchors
        // (and the decor tools) use the documented convention "front -> +Z, Unity = (-x, z, -y)". Measured on the Bed, whose
        // headboard (Blender +Y, the only part above 1 m) must end at -Z: when it does not, every prop gets the same
        // half turn about Y so meshes and anchors agree.
        static bool? frontFlip;
        static Matrix4x4 TurnFrontToPlusZ(Matrix4x4 node, Mesh mesh, string prop)
        {
            if (frontFlip == null)
            {
                var bed = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsRoot + "/Prop_Bed.fbx");
                Need(bed != null, "Prop_Bed.fbx is required to verify the prop axes.");
                var filter = bed.GetComponentInChildren<MeshFilter>(true);
                var matrix = filter.transform.localToWorldMatrix;
                var bedMesh = filter.sharedMesh;
                var vertices = new List<Vector3>(); bedMesh.GetVertices(vertices);
                var top = vertices.Select(v => matrix.MultiplyPoint3x4(v)).Where(p => p.y > 1.0f).ToArray();
                Need(top.Length > 0, "Bed headboard vertices not found.");
                frontFlip = top.Average(p => p.z) > 0f;
                Debug.Log("LMS_V030_PROPS_AXES headboard mean z " + top.Average(p => p.z).ToString("F3") + (frontFlip.Value ? " -> half turn applied" : " -> as imported"));
            }
            return frontFlip.Value ? Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * node : node;
        }

        static Bounds Transform(Matrix4x4 matrix, Bounds local)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            var e = local.extents;
            for (int i = 0; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return result;
        }

        static void SaveIfChanged(GameObject root, string path)
        {
            // Prefab saves are deterministic; SaveAsPrefabAsset keeps the GUID of an existing prefab.
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Need(success, "Could not save " + path);
        }

        public static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static string Disk(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);
        static string Sha256(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
