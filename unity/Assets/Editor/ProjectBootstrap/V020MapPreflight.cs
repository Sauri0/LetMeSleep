using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    // Re-capture imported dependency hashes after code integration, while requiring
    // unchanged authored files, metadata, GUIDs and recovery volume schema.
    public static class V020MapPreflight
    {
        public static void CaptureAndInspect()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-v020Evidence");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("An absolute -v020Evidence directory is required.");
            string output = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(output)) throw new IOException("Evidence directory already exists.");
            string root = Directory.GetParent(Application.dataPath).Parent.FullName;
            string source = Path.Combine(root, "Higgsfield/Integration/MapBounds/bounds-approved-preflight.json");
            var request = JsonConvert.DeserializeObject<HiggsfieldMapBoundaryInstaller.Request>(File.ReadAllText(source));
            if (request == null || request.action != "inspect") throw new InvalidDataException("Inspect request required.");
            var guards = new List<HiggsfieldMapBoundaryInstaller.Guard> { request.catalog };
            foreach (var map in request.maps) { guards.Add(map.prefab); guards.Add(map.scene); guards.Add(map.spatial); }
            foreach (var guard in guards)
            {
                string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, guard.path);
                if (Hash(file) != guard.sha256 || Hash(file + ".meta") != guard.metaSha256 ||
                    AssetDatabase.AssetPathToGUID(guard.path) != guard.guid)
                    throw new InvalidDataException("Authored input changed; review before refreshing dependencies: " + guard.path);
            }
            var dependencies = new JArray();
            var waterBindings = new JArray();
            foreach (var map in request.maps)
            {
                string current = AssetDatabase.GetAssetDependencyHash(map.prefab.path).ToString();
                dependencies.Add(new JObject { ["mapId"] = map.mapId, ["previous"] = map.prefab.dependencyHash, ["current"] = current });
                map.prefab.dependencyHash = current;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(map.prefab.path);
                foreach (var water in map.waterGuards.Where(w => w.mode == "cpu_morph_pair"))
                {
                    var transform = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == water.objectName);
                    if (transform.GetComponent<SkinnedMeshRenderer>()) continue;
                    var helper = transform.GetComponent<LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater>();
                    var filter = transform.GetComponent<MeshFilter>();
                    if (!helper || !helper.enabled || !filter || !filter.sharedMesh || !filter.sharedMesh.isReadable ||
                        filter.sharedMesh.vertexCount > LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater.MaximumAnimatedVertices ||
                        helper.Amplitude < 0 || helper.Amplitude > .15f || float.IsNaN(helper.Amplitude))
                        throw new InvalidDataException("Cannot measure runtime water: " + map.mapId + "/" + water.objectName);
                    var matrix = prefab.transform.worldToLocalMatrix * transform.localToWorldMatrix;
                    float crest = filter.sharedMesh.vertices.Max(v => matrix.MultiplyPoint3x4(v).y)
                        + Mathf.Abs(prefab.transform.InverseTransformVector(Vector3.up).y) * helper.Amplitude;
                    // Match the actual runtime weighted sine envelope, not GLB morphs
                    // that were not imported. Keep authored XZ rings and human heights.
                    var zones = map.recovery.mosquitoPolygonFallZones.Where(z => z.id.StartsWith(water.objectName + "-", StringComparison.Ordinal)).ToArray();
                    if (zones.Length == 0) throw new InvalidDataException("Missing water recovery polygon.");
                    waterBindings.Add(new JObject { ["mapId"] = map.mapId, ["object"] = water.objectName,
                        ["previousMode"] = water.mode, ["previousCrest"] = water.expectedCrestMaxY,
                        ["measuredCrest"] = crest, ["amplitude"] = helper.Amplitude });
                    water.mode = "cpu_sine_waves"; water.expectedCpuAmplitude = helper.Amplitude; water.expectedCrestMaxY = crest;
                    foreach (var zone in zones) zone.maxY = crest + .055f;
                }
            }
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "dependency-recapture.json"), new JObject {
                ["sourceSha256"] = Hash(source), ["unity"] = Application.unityVersion,
                ["authoredFilesUnchanged"] = true, ["maps"] = dependencies, ["runtimeWaterBindings"] = waterBindings
            }.ToString());
            request.receiptPath = Path.Combine(output, "inspect-receipt.json");
            string config = Path.Combine(output, "bounds-inspect.json");
            File.WriteAllText(config, JsonConvert.SerializeObject(request, Formatting.Indented));
            HiggsfieldMapBoundaryInstaller.Run(config);
            var receipt = JObject.Parse(File.ReadAllText(request.receiptPath));
            if (receipt.Value<bool>("success") != true) throw new InvalidOperationException("Map preflight failed: " + receipt.Value<string>("error"));
            Debug.Log("LMS_V020_MAP_PREFLIGHT " + request.receiptPath);
        }
        private static string Hash(string file)
        {
            using var stream = File.OpenRead(file);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
