using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// Read-only inventory for the v0.3.0 atmosphere pass: which renderers use each Color_NNN swatch,
    /// what sits around every catalog light anchor (lantern heads, flames, lamp shades), spawn transforms
    /// and the private lobby's practicals. Writes one JSON file; never saves or dirties any asset.
    ///   -executeMethod LetMeSleep.Editor.HiggsfieldAtmosphereDiagnostics.DumpFromCommandLine -higgsfieldAtmosphereDump &lt;abs.json&gt;
    /// </summary>
    public static class HiggsfieldAtmosphereDiagnostics
    {
        const string LobbyPrefabPath = "Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab";
        static readonly string[] Keywords =
            { "lamp", "lantern", "shade", "window", "glass", "flame", "fire", "lens", "ceiling", "ember", "bulb", "sconce", "candle", "light" };

        public static void DumpFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-higgsfieldAtmosphereDump");
            try
            {
                if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                    throw new ArgumentException("-higgsfieldAtmosphereDump <absolute json> is required.");
                Dump(args[index + 1]);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void Dump(string outputPath)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(HiggsfieldAtmosphereCorrection.CatalogPath);
            if (!catalog) throw new InvalidOperationException("Catalog missing.");
            var result = new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["unityVersion"] = Application.unityVersion };
            var maps = new JArray();
            foreach (var entry in catalog.Entries)
            {
                var map = DumpMap(entry);
                map["allRenderers"] = AllRenderers(entry);
                map["spawnClearance"] = SpawnClearance(entry);
                maps.Add(map);
            }
            result["maps"] = maps;
            result["lobby"] = DumpPrefab(LobbyPrefabPath, null);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, result.ToString(Formatting.Indented));
            Debug.Log("[HiggsfieldAtmosphereDiagnostics] Wrote " + outputPath);
        }

        static JObject DumpMap(HiggsfieldMapCatalog.Entry entry)
        {
            string path = AssetDatabase.GetAssetPath(entry.Prefab);
            return DumpPrefab(path, entry);
        }

        static JObject DumpPrefab(string prefabPath, HiggsfieldMapCatalog.Entry entry)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var report = new JObject { ["prefab"] = prefabPath, ["mapId"] = entry?.MapId };
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                report["rendererCount"] = renderers.Length;

                var byMaterial = new Dictionary<string, List<Renderer>>(StringComparer.Ordinal);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials.Where(m => m).Distinct())
                    {
                        string key = material.name;
                        if (!byMaterial.TryGetValue(key, out var list)) byMaterial[key] = list = new List<Renderer>();
                        list.Add(renderer);
                    }
                var materials = new JArray();
                foreach (var pair in byMaterial.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var sample = pair.Value.First().sharedMaterials.First(m => m && m.name == pair.Key);
                    materials.Add(new JObject
                    {
                        ["material"] = pair.Key, ["assetPath"] = AssetDatabase.GetAssetPath(sample),
                        ["shader"] = sample.shader ? sample.shader.name : null,
                        ["baseColor"] = sample.HasProperty("_BaseColor") ? Text(sample.GetColor("_BaseColor")) : null,
                        ["emission"] = sample.HasProperty("_EmissionColor") ? Text(sample.GetColor("_EmissionColor")) : null,
                        ["cull"] = sample.HasProperty("_Cull") ? sample.GetFloat("_Cull") : -1,
                        ["renderers"] = pair.Value.Count,
                        ["sample"] = new JArray(pair.Value.Take(80).Select(r => Describe(root.transform, r)))
                    });
                }
                report["materials"] = materials;

                report["keywordRenderers"] = new JArray(renderers
                    .Where(r => Keywords.Any(k => r.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Take(400).Select(r => Describe(root.transform, r)));

                if (entry != null)
                {
                    var anchors = new JArray();
                    foreach (var binding in entry.LocalLights)
                    {
                        Transform anchor = binding.AnchorPath == "." ? root.transform : Find(root.transform, binding.AnchorPath);
                        if (!anchor) { anchors.Add(new JObject { ["anchor"] = binding.AnchorPath, ["missing"] = true }); continue; }
                        Vector3 p = anchor.position;
                        var near = renderers.Where(r => r.bounds.SqrDistance(p) < 1.5f * 1.5f)
                            .OrderBy(r => r.bounds.SqrDistance(p)).Take(14).Select(r => Describe(root.transform, r));
                        anchors.Add(new JObject
                        {
                            ["anchor"] = binding.AnchorPath, ["position"] = Text(p), ["euler"] = Text(anchor.eulerAngles),
                            ["light"] = Text(binding.Settings.Color) + " i" + binding.Settings.UnityIntensity + " r" + binding.Settings.Range +
                                " " + binding.Settings.Shadows,
                            ["near"] = new JArray(near)
                        });
                    }
                    report["anchors"] = anchors;
                }
                var definition = root.GetComponent<EnvironmentMapDefinition>();
                if (definition)
                {
                    report["playBounds"] = Text(definition.PlayBounds.center) + " / " + Text(definition.PlayBounds.size);
                    report["humanSpawns"] = Spawns(root.transform, definition.HumanSpawnPoints, renderers);
                    report["lobbySpawns"] = Spawns(root.transform, definition.LobbySpawnPoints, renderers);
                    if (definition.PresentationAnchors)
                        report["presentationAnchors"] = new JArray(definition.PresentationAnchors.GetComponentsInChildren<Transform>(true)
                            .Select(t => new JObject { ["path"] = PathOf(root.transform, t), ["position"] = Text(t.position), ["euler"] = Text(t.eulerAngles) }));
                }
                report["lights"] = new JArray(root.GetComponentsInChildren<Light>(true)
                    .Select(l => PathOf(root.transform, l.transform) + " " + l.type + " " + Text(l.color) + " i" + l.intensity));
                return report;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Every renderer of the map prefab: "path|materials|center|size" (invariant culture), for choosing overrides.</summary>
        static JArray AllRenderers(HiggsfieldMapCatalog.Entry entry)
        {
            string path = AssetDatabase.GetAssetPath(entry.Prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                return new JArray(root.GetComponentsInChildren<Renderer>(true).Select(r =>
                    PathOf(root.transform, r.transform) + "|" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")) + "|" +
                    Invariant(r.bounds.center) + "|" + Invariant(r.bounds.size) + "|" + (r.GetComponent<Collider>() ? "collider" : "")));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// v0.3.0 maps-r3 director #1/#9: free distance (metres, 30 = open) from each spawn's eye along 16 yaws
        /// (0 = +Z, clockwise seen from above, the GameplayRuntime convention) against the map colliders, so spawn
        /// facings can be chosen away from walls. Instantiates the prefab in the open (unsaved) scene and destroys it.
        /// </summary>
        static JArray SpawnClearance(HiggsfieldMapCatalog.Entry entry)
        {
            var result = new JArray();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab.gameObject);
            try
            {
                Physics.SyncTransforms();
                var definition = instance.GetComponent<EnvironmentMapDefinition>();
                var samples = new List<(Transform point, bool human, int index, JArray free)>();
                foreach (var (points, human) in new[] { (definition.HumanSpawnPoints, true), (definition.MosquitoSpawnPoints, false) })
                    for (int i = 0; i < (points?.Length ?? 0); i++)
                        if (points[i]) samples.Add((points[i], human, i, Rays(points[i].position + Vector3.up * (human ? 1.55f : 0f))));
                // Second pass against every render mesh (pine canopies, grass and props have no collider but still fill the view).
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh && !filter.GetComponent<Collider>() && filter.GetComponent<MeshRenderer>())
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                Physics.SyncTransforms();
                foreach (var s in samples)
                    result.Add(new JObject
                    {
                        ["role"] = s.human ? "human" : "mosquito", ["index"] = s.index, ["name"] = s.point.name,
                        ["position"] = Invariant(s.point.position), ["free16"] = s.free,
                        ["visual16"] = Rays(s.point.position + Vector3.up * (s.human ? 1.55f : 0f))
                    });
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            return result;

            JArray Rays(Vector3 eye)
            {
                var distances = new JArray();
                for (int k = 0; k < 16; k++)
                {
                    Vector3 direction = Quaternion.AngleAxis(k * 22.5f, Vector3.up) * Vector3.forward;
                    distances.Add(Physics.Raycast(eye, direction, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)
                        ? (float)Math.Round(hit.distance, 2) : 30f);
                }
                return distances;
            }
        }

        static string Invariant(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3}", v.x, v.y, v.z);

        static JArray Spawns(Transform root, Transform[] points, Renderer[] renderers)
        {
            var result = new JArray();
            if (points == null) return result;
            foreach (var t in points.Where(x => x))
            {
                Vector3 eye = t.position + Vector3.up * 1.6f;
                var near = renderers.Where(r => r.bounds.SqrDistance(eye) < 0.8f * 0.8f)
                    .OrderBy(r => r.bounds.SqrDistance(eye)).Take(6).Select(r => PathOf(root, r.transform) + " d=" +
                        Mathf.Sqrt(r.bounds.SqrDistance(eye)).ToString("F2") + " [" + string.Join(",", r.sharedMaterials.Where(m => m).Select(m => m.name)) + "]");
                result.Add(new JObject
                {
                    ["path"] = PathOf(root, t), ["position"] = Text(t.position), ["euler"] = Text(t.eulerAngles),
                    ["nearEye"] = new JArray(near)
                });
            }
            return result;
        }

        static JObject Describe(Transform root, Renderer r) => new JObject
        {
            ["path"] = PathOf(root, r.transform),
            ["materials"] = new JArray(r.sharedMaterials.Select(m => m ? m.name : "null")),
            ["center"] = Text(r.bounds.center), ["size"] = Text(r.bounds.size),
            ["shadows"] = r.shadowCastingMode.ToString(),
            ["collider"] = r.GetComponent<Collider>() ? r.GetComponent<Collider>().GetType().Name : null
        };

        static Transform Find(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = current.Cast<Transform>().FirstOrDefault(c => c.name == segment);
                if (!current) return null;
            }
            return current;
        }

        static string PathOf(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return parts.Count == 0 ? "." : string.Join("/", parts);
        }

        static string Text(Vector3 v) => v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3");
        static string Text(Color c) => c.r.ToString("F3") + "," + c.g.ToString("F3") + "," + c.b.ToString("F3");
    }
}
