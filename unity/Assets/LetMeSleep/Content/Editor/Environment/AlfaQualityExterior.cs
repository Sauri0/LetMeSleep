using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Environment;
using LetMeSleep.Gameplay.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Editor
{
    /// <summary>Own exterior hook. Director calls after house dressing, before gameplay binding/save.</summary>
    public static class AlfaQualityExterior
    {
        const string Output = "Assets/LetMeSleep/Content/Environment/AlfaMaps/QualityExterior";
        const string Source = "art_source/unity/environments/quality_exterior/generated_exterior.json";
        const string RootName = "QualityExterior";

        [Serializable] public class Recipe
        {
            public int schemaVersion;
            public string mapId;
            public MaterialSpec[] materials;
            public MeshSpec[] meshes;
            public InstanceSpec[] instances;
            public string[] replaceRendererRoots;
        }
        [Serializable] public class MaterialSpec { public string name; public Color color; }
        [Serializable] public class MeshSpec { public string name; public Vector3[] vertices; public SubmeshSpec[] submeshes; }
        [Serializable] public class SubmeshSpec { public string material; public int[] triangles; }
        [Serializable] public class InstanceSpec
        {
            public string name, mesh, zone;
            public Vector3 position, scale;
            public float yaw;
        }
        [Serializable] public class Receipt
        {
            public string unityVersion, utc, sourceSha256, meshUpdateStrategy;
            public int meshes, instances, collidersBefore, collidersAfter, lightsBefore, lightsAfter;
            public string[] verified, pending;
        }

        public static void Build(GameObject house)
        {
            Require(house != null, "Exterior requires the authored house root.");
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exterior authoring requires Edit mode.");
            Require(house.transform.Find(RootName) == null, "Build exterior once on a fresh house; rebuild via AlfaMapBuilder.");
            var definition = house.GetComponent<EnvironmentMapDefinition>();
            Require(definition != null && definition.MapId == "house-patio-v1", "Exterior belongs to house-patio-v1 only.");
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            byte[] sourceBytes = File.ReadAllBytes(Path.Combine(repository, Source));
            var recipe = JsonUtility.FromJson<Recipe>(System.Text.Encoding.UTF8.GetString(sourceBytes));
            ValidateRecipe(recipe);
            var colliderSignature = ColliderSignature(house);
            int collidersBefore = house.GetComponentsInChildren<Collider>(true).Length;
            Bounds playBounds = definition.PlayBounds;
            Transform[] humans = definition.HumanSpawnPoints.ToArray();
            Transform[] mosquitoes = definition.MosquitoSpawnPoints.ToArray();
            Transform[] pickups = definition.ToolPickupPoints.ToArray();
            var anchors = humans.Concat(mosquitoes).Concat(pickups).Distinct().ToDictionary(t => t, t => t.localToWorldMatrix);
            int lightsBefore = house.GetComponentsInChildren<Light>(true).Length;
            // Resolve replacements before mutation. Keep exact trunk colliders, transforms and IDs.
            var replaceRoots = recipe.replaceRendererRoots.Select(name => {
                var matches = house.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
                Require(matches.Length == 1, "Expected one legacy tree: " + name);
                return matches[0];
            }).ToArray();

            EnvironmentSampleBuilder.EnsureFolder(Output);
            EnvironmentSampleBuilder.EnsureFolder(Output + "/Materials");
            EnvironmentSampleBuilder.EnsureFolder(Output + "/Meshes");
            var materials = recipe.materials.ToDictionary(m => m.name, MakeMaterial);
            var meshes = recipe.meshes.ToDictionary(m => m.name, MakeMesh);
            var meshSpecs = recipe.meshes.ToDictionary(m => m.name);
            var root = new GameObject(RootName);
            root.transform.SetParent(house.transform, false);
            var zones = new Dictionary<string, Transform>();
            foreach (var instance in recipe.instances)
            {
                if (!zones.TryGetValue(instance.zone, out var zone))
                {
                    zone = new GameObject(instance.zone).transform;
                    zone.SetParent(root.transform, false);
                    zones.Add(instance.zone, zone);
                }
                var piece = new GameObject(instance.name);
                piece.transform.SetParent(zone, false);
                piece.transform.localPosition = instance.position;
                piece.transform.localRotation = Quaternion.Euler(0, instance.yaw, 0);
                piece.transform.localScale = instance.scale;
                piece.AddComponent<MeshFilter>().sharedMesh = meshes[instance.mesh];
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = meshSpecs[instance.mesh].submeshes.Select(s => materials[s.material]).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // Exterior depth must not increase the baked map's lightmap footprint.
                renderer.receiveGI = ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            foreach (var legacy in replaceRoots)
            {
                foreach (var lod in legacy.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                foreach (var renderer in legacy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            }
            Require(root.GetComponentsInChildren<Collider>(true).Length == 0, "Exterior art must not add colliders.");
            Require(root.GetComponentsInChildren<Light>(true).Length == 0, "Lighting belongs to Presentation.");
            Require(colliderSignature == ColliderSignature(house), "Exterior changed a collider or surface ID.");
            Require(definition.PlayBounds == playBounds, "Exterior changed play bounds.");
            Require(humans.SequenceEqual(definition.HumanSpawnPoints) && mosquitoes.SequenceEqual(definition.MosquitoSpawnPoints)
                && pickups.SequenceEqual(definition.ToolPickupPoints), "Exterior changed map anchor arrays.");
            Require(anchors.All(a => a.Key.localToWorldMatrix == a.Value), "Exterior moved spawn or pickup anchors.");
            AssetDatabase.SaveAssets();
            string sha;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                sha = BitConverter.ToString(hash.ComputeHash(sourceBytes)).Replace("-", "").ToLowerInvariant();
            var receipt = new Receipt {
                unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("o"), sourceSha256 = sha,
                meshUpdateStrategy = "Public Mesh API replacement with explicit upload and CPU readback; existing asset GUID retained",
                meshes = meshes.Count, instances = recipe.instances.Length,
                collidersBefore = collidersBefore, collidersAfter = house.GetComponentsInChildren<Collider>(true).Length,
                lightsBefore = lightsBefore, lightsAfter = house.GetComponentsInChildren<Light>(true).Length,
                verified = new[] { "Finite indexed meshes imported", "Existing collider objects, geometry, transforms and surface IDs unchanged",
                    "Spawn and pickup arrays and world poses unchanged", "PlayBounds unchanged", "No added lights or colliders" },
                pending = new[] { "Native living and patio visual review", "Human and mosquito traversal", "Night lighting/readability approval", "Hardware performance" }
            };
            File.WriteAllText(Path.Combine(repository, "art_source/unity/environments/quality_exterior/unity_import_receipt.json"), JsonUtility.ToJson(receipt, true) + "\n");
            Debug.Log("LMS_QUALITY_EXTERIOR_BUILT " + JsonUtility.ToJson(receipt));
        }

        static Material MakeMaterial(MaterialSpec spec)
        {
            string path = Output + "/Materials/Exterior_" + spec.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Require(shader != null, "URP Lit shader required.");
                material = new Material(shader) { name = "Exterior_" + spec.name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            material.SetColor("_BaseColor", spec.color);
            material.SetFloat("_Smoothness", .13f);
            material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh MakeMesh(MeshSpec spec)
        {
            string name = "Exterior_" + spec.name;
            string path = Output + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh { name = name };
            UpdateMeshData(mesh, spec);
            if (create) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Also used by the Director's controlled same-session topology regression.
        // No AssetDatabase save here: callers decide asset lifecycle separately.
        public static void UpdateMeshData(Mesh mesh, MeshSpec spec)
        {
            Require(mesh != null && spec != null, "Exterior mesh update requires target and recipe.");
            // Fresh-session evidence showed the serialized data was correct while
            // resident rendering was stale. Notify Unity through its native setters;
            // Upload alone was separately tested and did not correct that state.
            mesh.Clear(false);
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = spec.vertices;
            mesh.subMeshCount = spec.submeshes.Length;
            for (int i = 0; i < spec.submeshes.Length; i++) mesh.SetTriangles(spec.submeshes[i].triangles, i);
            mesh.uv = spec.vertices.Select(v => new Vector2(v.x, v.z)).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.MarkModified();
            mesh.UploadMeshData(false);
            Require(mesh.vertices.SequenceEqual(spec.vertices), "Exterior vertex readback differs: " + spec.name);
            Require(mesh.subMeshCount == spec.submeshes.Length, "Exterior submesh count differs: " + spec.name);
            for (int i = 0; i < spec.submeshes.Length; i++)
                Require(mesh.GetTriangles(i).SequenceEqual(spec.submeshes[i].triangles), "Exterior index readback differs: " + spec.name + "/" + i);
        }

        static void ValidateRecipe(Recipe recipe)
        {
            Require(recipe != null && recipe.schemaVersion == 1 && recipe.mapId == "house-patio-v1", "Unknown exterior recipe.");
            Require(recipe.materials != null && recipe.meshes != null && recipe.instances != null && recipe.replaceRendererRoots != null, "Incomplete exterior recipe.");
            var names = recipe.meshes.Select(m => m.name).ToArray();
            Require(names.Distinct().Count() == names.Length, "Duplicate mesh names.");
            Require(recipe.instances.Select(i => i.name).Distinct().Count() == recipe.instances.Length, "Duplicate instance names.");
            var materials = recipe.materials.Select(m => m.name).ToArray();
            foreach (var mesh in recipe.meshes)
            {
                Require(mesh.vertices.Length >= 3 && mesh.vertices.All(Finite), "Nonfinite/empty exterior mesh.");
                foreach (var submesh in mesh.submeshes)
                {
                    Require(materials.Contains(submesh.material), "Unknown material.");
                    Require(submesh.triangles.Length % 3 == 0 && submesh.triangles.All(i => i >= 0 && i < mesh.vertices.Length), "Invalid triangle indices.");
                    for (int i = 0; i < submesh.triangles.Length; i += 3)
                    {
                        var a = mesh.vertices[submesh.triangles[i]]; var b = mesh.vertices[submesh.triangles[i + 1]]; var c = mesh.vertices[submesh.triangles[i + 2]];
                        Require(Vector3.Cross(b - a, c - a).sqrMagnitude > 1e-16f, "Degenerate exterior triangle.");
                    }
                }
            }
            Require(recipe.instances.All(i => names.Contains(i.mesh) && Finite(i.position) && Finite(i.scale)
                && i.scale.x > 0 && i.scale.y > 0 && i.scale.z > 0 && !float.IsNaN(i.yaw) && !float.IsInfinity(i.yaw)), "Invalid exterior instance.");
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        static string ColliderSignature(GameObject root)
        {
            return string.Join("\n", root.GetComponentsInChildren<Collider>(true).OrderBy(c => c.GetInstanceID()).Select(c =>
            {
                var surface = c.GetComponent<GameplaySurface>();
                return c.GetInstanceID() + "|" + EditorJsonUtility.ToJson(c) + "|" + c.transform.localToWorldMatrix.ToString("R")
                    + "|" + c.gameObject.activeInHierarchy + "|" + c.gameObject.layer + "|" + (surface == null ? "none" : EditorJsonUtility.ToJson(surface));
            }));
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
