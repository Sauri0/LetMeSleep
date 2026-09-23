using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// v0.3.0 decoration planning aid (read-only for assets): for every catalog map it writes a JSON dump (renderers with
    /// world bounds, colliders, spawns, objectives, keep-outs) and top-down orthographic plan images with a labelled
    /// 5/10 m grid, routes (yellow), portals (cyan), spawns (blue humans, red mosquitoes), objectives (magenta) and,
    /// optionally, an existing decor instance. Floor slices cut the roofs so interiors can be planned.
    ///   -executeMethod LetMeSleep.Editor.HiggsfieldDecorPlanner.DumpFromCommandLine -v030PlanOutput &lt;dir&gt; [-v030PlanWithDecor]
    /// </summary>
    public static class HiggsfieldDecorPlanner
    {
        public const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";

        public static void DumpFromCommandLine()
        {
            try
            {
                string output = V030PropLibraryImporter.Argument("-v030PlanOutput");
                if (string.IsNullOrEmpty(output)) throw new ArgumentException("-v030PlanOutput <dir> required");
                bool withDecor = Environment.GetCommandLineArgs().Contains("-v030PlanWithDecor");
                Dump(output, withDecor);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Dump(string output, bool withDecor)
        {
            Directory.CreateDirectory(output);
            // New scene first: opening a Single scene unloads unreferenced assets loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(CatalogPath);
            if (!catalog) throw new InvalidOperationException("Catalog missing");
            foreach (var entry in catalog.Entries)
            {
                var map = (EnvironmentMapDefinition)PrefabUtility.InstantiatePrefab(entry.Prefab, scene);
                GameObject decor = null;
                var decorField = entry.GetType().GetField("Decor");
                if (withDecor && decorField != null && decorField.GetValue(entry) is GameObject decorPrefab && decorPrefab)
                {
                    decor = (GameObject)PrefabUtility.InstantiatePrefab(decorPrefab, scene);
                    decor.transform.SetParent(map.transform, false);
                }
                Physics.SyncTransforms();
                var keepouts = HiggsfieldDecorClearance.Build(map);
                var json = DumpMap(map, keepouts, decor);
                File.WriteAllText(Path.Combine(output, entry.MapId + "-data.json"), json.ToString(Formatting.Indented));
                foreach (var slice in Slices(map))
                    RenderPlan(map, keepouts, decor, slice.top, Path.Combine(output, entry.MapId + "-" + slice.name + ".png"));
                Object.DestroyImmediate(map.gameObject);
            }
            Debug.Log("LMS_DECOR_PLANS_WRITTEN " + output);
        }

        static IEnumerable<(string name, float top)> Slices(EnvironmentMapDefinition map)
        {
            yield return ("top", map.PlayBounds.max.y + 40f);
            // Interior floors: one slice 2.2 m above every distinct floor level with enclosed zones.
            if (!map.SpatialData) yield break;
            var data = JObject.Parse(map.SpatialData.text);
            var zones = (data["human_routes"] is JArray routes && routes.Count > 0 ? data["human_zones"] : data["zones"]) as JArray;
            if (zones == null) yield break;
            var levels = new SortedSet<float>();
            foreach (var zone in zones)
            {
                float minY = zone["min"][1].Value<float>(), maxY = zone["max"][1].Value<float>();
                if (maxY - minY < 4.2f) levels.Add(Mathf.Round(minY * 2f) / 2f);
            }
            int n = 0;
            foreach (float level in levels)
            {
                if (n++ >= 4) break;
                yield return ("slice-" + level.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', 'p').Replace('-', 'm'), level + 2.2f);
            }
        }

        static JObject DumpMap(EnvironmentMapDefinition map, List<HiggsfieldDecorClearance.Keepout> keepouts, GameObject decor)
        {
            JArray Vec(Vector3 v) => new JArray(Math.Round(v.x, 3), Math.Round(v.y, 3), Math.Round(v.z, 3));
            var renderers = new JArray();
            foreach (var renderer in map.GetComponentsInChildren<Renderer>(true))
            {
                if (decor && renderer.transform.IsChildOf(decor.transform)) continue;
                var b = renderer.bounds;
                renderers.Add(new JObject
                {
                    ["path"] = AnimationUtility.CalculateTransformPath(renderer.transform, map.transform),
                    ["center"] = Vec(b.center), ["size"] = Vec(b.size),
                    ["collider"] = renderer.GetComponent<Collider>() ? renderer.GetComponent<Collider>().GetType().Name : null,
                    ["material"] = renderer.sharedMaterial ? renderer.sharedMaterial.name : null,
                    ["enabled"] = renderer.enabled
                });
            }
            var objectives = new JArray();
            var catalog = map.GetComponent<GameplayObjectiveCatalog>();
            if (catalog)
                foreach (var e in catalog.Entries)
                    objectives.Add(new JObject { ["id"] = e.ObjectiveId, ["position"] = Vec(map.transform.TransformPoint(e.LocalPosition)),
                        ["approach"] = Vec(map.transform.TransformPoint(e.LocalApproachPoint)), ["radius"] = e.UseRadius, ["target"] = e.TargetPath, ["region"] = e.RouteRegionId });
            var result = new JObject
            {
                ["mapId"] = map.MapId, ["playBounds"] = new JObject { ["center"] = Vec(map.PlayBounds.center), ["size"] = Vec(map.PlayBounds.size) },
                ["humanSpawns"] = new JArray((map.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(t => t).Select(t => Vec(t.position))),
                ["mosquitoSpawns"] = new JArray((map.MosquitoSpawnPoints ?? Array.Empty<Transform>()).Where(t => t).Select(t => Vec(t.position))),
                ["objectives"] = objectives,
                ["keepouts"] = new JArray(keepouts.Select(k => new JObject
                {
                    ["kind"] = k.Kind.ToString(), ["id"] = k.Id,
                    ["a"] = k.IsCapsule ? Vec(k.A) : null, ["b"] = k.IsCapsule ? Vec(k.B) : null,
                    ["center"] = k.IsCapsule ? null : Vec(k.Box.center), ["size"] = k.IsCapsule || k.IsSphere ? null : Vec(k.Box.size), ["radius"] = k.Radius
                })),
                ["renderers"] = renderers
            };
            if (decor)
                result["decor"] = new JArray(decor.GetComponentsInChildren<Renderer>(true).Select(r => new JObject
                {
                    ["path"] = AnimationUtility.CalculateTransformPath(r.transform, decor.transform), ["center"] = Vec(r.bounds.center), ["size"] = Vec(r.bounds.size)
                }));
            return result;
        }

        const int Resolution = 2048;

        static void RenderPlan(EnvironmentMapDefinition map, List<HiggsfieldDecorClearance.Keepout> keepouts, GameObject decor, float top, string path)
        {
            Bounds b = map.PlayBounds;
            float half = Mathf.Max(b.extents.x, b.extents.z) + 4f;
            var cameraObject = new GameObject("PlanCamera");
            var lightObject = new GameObject("PlanLight");
            var rt = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var oldAmbient = RenderSettings.ambientLight; var oldMode = RenderSettings.ambientMode; var oldFog = RenderSettings.fog;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = half;
                camera.aspect = 1f;
                camera.transform.SetPositionAndRotation(new Vector3(b.center.x, top, b.center.z), Quaternion.Euler(90f, 0f, 0f));
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = top - (b.min.y - 30f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .09f, .12f);
                camera.targetTexture = rt;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
                RenderSettings.fog = false;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                RenderTexture.active = previous;
                var pixels = new Canvas(texture, new Vector2(b.center.x - half, b.center.z - half), half * 2f / Resolution);
                pixels.Grid();
                foreach (var k in keepouts)
                {
                    switch (k.Kind)
                    {
                        case HiggsfieldDecorClearance.Kind.Route: pixels.Line(k.A, k.B, new Color(1f, .85f, .1f), 2); break;
                        case HiggsfieldDecorClearance.Kind.Portal: pixels.Rect(k.Box, new Color(.2f, 1f, 1f)); break;
                        case HiggsfieldDecorClearance.Kind.Stair: pixels.Rect(k.Box, new Color(.6f, 1f, .4f)); break;
                        case HiggsfieldDecorClearance.Kind.Spawn:
                            pixels.Circle(k.Box.center, k.Radius, k.Id.Contains("mosquito") ? new Color(1f, .25f, .25f) : new Color(.3f, .55f, 1f)); break;
                        case HiggsfieldDecorClearance.Kind.Objective: pixels.Circle(k.Box.center, k.Radius, new Color(1f, .3f, 1f)); break;
                        case HiggsfieldDecorClearance.Kind.ObjectiveTarget: pixels.Rect(k.Box, new Color(1f, .5f, 1f)); break;
                    }
                }
                if (decor)
                    foreach (var r in decor.GetComponentsInChildren<Renderer>(true))
                        pixels.Rect(r.bounds, new Color(.1f, 1f, .3f));
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldMode; RenderSettings.fog = oldFog;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
                rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        /// <summary>World-to-pixel painter for plan images (x → right, z → up).</summary>
        sealed class Canvas
        {
            readonly Texture2D texture; readonly Vector2 origin; readonly float metersPerPixel;
            public Canvas(Texture2D texture, Vector2 origin, float metersPerPixel) { this.texture = texture; this.origin = origin; this.metersPerPixel = metersPerPixel; }
            Vector2Int P(Vector3 world) => new Vector2Int(Mathf.RoundToInt((world.x - origin.x) / metersPerPixel), Mathf.RoundToInt((world.z - origin.y) / metersPerPixel));
            void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < texture.width && y < texture.height) texture.SetPixel(x, y, c); }
            void Blend(int x, int y, Color c, float a) { if (x >= 0 && y >= 0 && x < texture.width && y < texture.height) texture.SetPixel(x, y, Color.Lerp(texture.GetPixel(x, y), c, a)); }
            public void Line(Vector3 a, Vector3 b, Color c, int width)
            {
                var p = P(a); var q = P(b);
                int steps = Mathf.Max(1, Mathf.Max(Mathf.Abs(q.x - p.x), Mathf.Abs(q.y - p.y)));
                for (int i = 0; i <= steps; i++)
                {
                    int x = Mathf.RoundToInt(Mathf.Lerp(p.x, q.x, i / (float)steps)), y = Mathf.RoundToInt(Mathf.Lerp(p.y, q.y, i / (float)steps));
                    for (int dx = -width / 2; dx <= width / 2; dx++) for (int dy = -width / 2; dy <= width / 2; dy++) Set(x + dx, y + dy, c);
                }
            }
            public void Rect(Bounds box, Color c)
            {
                var min = P(box.min); var max = P(box.max);
                for (int x = min.x; x <= max.x; x++) { Set(x, min.y, c); Set(x, max.y, c); }
                for (int y = min.y; y <= max.y; y++) { Set(min.x, y, c); Set(max.x, y, c); }
            }
            public void Circle(Vector3 center, float radius, Color c)
            {
                var p = P(center); int r = Mathf.Max(2, Mathf.RoundToInt(radius / metersPerPixel));
                for (int i = 0; i < 180; i++)
                {
                    float angle = i * Mathf.PI * 2f / 180f;
                    Set(p.x + Mathf.RoundToInt(Mathf.Cos(angle) * r), p.y + Mathf.RoundToInt(Mathf.Sin(angle) * r), c);
                }
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) Set(p.x + dx, p.y + dy, c);
            }
            public void Grid()
            {
                float width = texture.width * metersPerPixel;
                int first = Mathf.CeilToInt(origin.x / 5f) * 5, last = Mathf.FloorToInt((origin.x + width) / 5f) * 5;
                for (int v = first; v <= last; v += 5)
                {
                    bool major = v % 10 == 0;
                    Color c = v == 0 ? new Color(1f, .3f, .3f) : Color.white;
                    int x = Mathf.RoundToInt((v - origin.x) / metersPerPixel);
                    for (int y = 0; y < texture.height; y++) Blend(x, y, c, major ? .45f : .2f);
                    if (major) Label(v.ToString(), x + 4, texture.height - 30);
                }
                first = Mathf.CeilToInt(origin.y / 5f) * 5; last = Mathf.FloorToInt((origin.y + width) / 5f) * 5;
                for (int v = first; v <= last; v += 5)
                {
                    bool major = v % 10 == 0;
                    Color c = v == 0 ? new Color(.3f, .5f, 1f) : Color.white;
                    int y = Mathf.RoundToInt((v - origin.y) / metersPerPixel);
                    for (int x = 0; x < texture.width; x++) Blend(x, y, c, major ? .45f : .2f);
                    if (major) Label(v.ToString(), 6, y + 4);
                }
            }
            // 3x5 digits and minus sign, scaled 4x, drawn with a dark outline.
            static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
            {
                ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111", ['3'] = "111001111001111", ['4'] = "101101111001001",
                ['5'] = "111100111001111", ['6'] = "111100111101111", ['7'] = "111001001001001", ['8'] = "111101111101111", ['9'] = "111101111001111",
                ['-'] = "000000111000000"
            };
            void Label(string text, int x, int y)
            {
                const int scale = 4;
                foreach (char ch in text)
                {
                    if (!Glyphs.TryGetValue(ch, out var glyph)) continue;
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            if (glyph[row * 3 + col] != '1') continue;
                            for (int dx = -1; dx <= scale; dx++) for (int dy = -1; dy <= scale; dy++)
                                Set(x + col * scale + dx, y + (4 - row) * scale + dy, Color.black);
                        }
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            if (glyph[row * 3 + col] != '1') continue;
                            for (int dx = 0; dx < scale; dx++) for (int dy = 0; dy < scale; dy++)
                                Set(x + col * scale + dx, y + (4 - row) * scale + dy, new Color(1f, 1f, .6f));
                        }
                    x += 4 * scale + 2;
                }
            }
        }
    }
}
