#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using LetMeSleep.Content.Environment;
using LetMeSleep.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3.0 scenes/decoration contract (HiggsfieldDecorBuilder, V030MenuSceneBuilder):
    /// - every map has a visual-only Decor (no collider, shadowless Point/Spot lights) instantiated as a child of the map by
    ///   the same code path as AlfaApplication.LoadMap, leaving the map's colliders and gameplay surfaces untouched;
    /// - no decor prop sits in a human route, portal, stair, spawn or objective keep-out, and every floor prop rests within
    ///   2 cm of a surface;
    /// - the menu bedroom frames the sleeper, the lamp and a >= 180 px mosquito as UI-06 does, and the boot scene runs the
    ///   sleeping living menu with closed eyes; the warm lobby decor restyles the sala without colliders and restores it.
    /// Visual quality is judged from captures (Validation/V030/Scenes), not here.
    /// </summary>
    public sealed class V030ScenesDecorPlayModeTests
    {
        private const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";
        private const string BedroomPath = "Assets/LetMeSleep/Content/Environment/V030Scenes/LMS_MenuBedroom.prefab";
        private const string LobbyDecorPath = "Assets/LetMeSleep/Content/Environment/V030Scenes/PrivateLobby-Decor.prefab";
        private const string LobbyPrefabPath = "Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab";
        private const string MosquitoPrefabPath = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Mosquito.prefab";
        private const string BootScene = "Assets/Scenes/LetMeSleepHiggsfield.unity";
        private const string Separator = "__";
        private readonly List<Object> created = new List<Object>();

        private static HiggsfieldMapCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private (EnvironmentMapDefinition map, GameObject decor) LoadWithDecor(HiggsfieldMapCatalog.Entry entry)
        {
            var map = Object.Instantiate(entry.Prefab);
            created.Add(map.gameObject);
            var decor = Object.Instantiate(entry.Decor, map.transform, false); // AlfaApplication.LoadMap
            decor.name = "_LMS_Decor";
            Physics.SyncTransforms();
            return (map, decor);
        }

        [Serializable] private sealed class DecorConfigFile { public DecorMap[] maps; }
        [Serializable] private sealed class DecorMap { public string mapId, openingPattern; }

        private static DecorConfigFile DecorConfig()
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../docs/v030/maps/decor-v030.json"));
            Assert.That(System.IO.File.Exists(path), Is.True, "decor config " + path);
            return JsonUtility.FromJson<DecorConfigFile>(System.IO.File.ReadAllText(path));
        }

        private static bool IsBoundary(Transform t)
        {
            for (; t; t = t.parent) if (t.name == "_LMS_ArtificialBoundaries_v1") return true;
            return false;
        }

        [UnityTest]
        public IEnumerator EveryMapHasVisualOnlyDecorThatLeavesGameplayGeometryUntouched()
        {
            var catalog = Catalog();
            Assert.That(catalog.Entries.Count, Is.EqualTo(5));
            foreach (var entry in catalog.Entries)
            {
                Assert.That(entry.Decor, Is.Not.Null, entry.MapId + " has decoration");
                Assert.DoesNotThrow(() => HiggsfieldMapCatalog.ValidateDecor(entry.Decor, entry.MapId));
                var bare = Object.Instantiate(entry.Prefab);
                created.Add(bare.gameObject);
                int colliders = bare.GetComponentsInChildren<Collider>(true).Length;
                int surfaces = bare.GetComponentsInChildren<LetMeSleep.Gameplay.Unity.GameplaySurface>(true).Length;
                var (map, decor) = LoadWithDecor(entry);
                yield return null;
                Assert.That(decor.GetComponentsInChildren<Collider>(true), Is.Empty, entry.MapId + " decor colliders");
                Assert.That(decor.GetComponentsInChildren<Rigidbody>(true), Is.Empty, entry.MapId + " decor rigidbodies");
                Assert.That(map.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders), entry.MapId + " map collider count");
                Assert.That(map.GetComponentsInChildren<LetMeSleep.Gameplay.Unity.GameplaySurface>(true).Length, Is.EqualTo(surfaces), entry.MapId + " surfaces");
                var props = decor.GetComponentsInChildren<Transform>(true).Where(t => t.name.Split(new[] { Separator }, StringSplitOptions.None).Length == 3).ToArray();
                Assert.That(props.Length, Is.GreaterThanOrEqualTo(8), entry.MapId + " decoration density");
                foreach (var light in decor.GetComponentsInChildren<Light>(true))
                {
                    Assert.That(light.type == LightType.Point || light.type == LightType.Spot, entry.MapId + " light type " + light.name);
                    Assert.That(light.shadows, Is.EqualTo(LightShadows.None), entry.MapId + " light shadows " + light.name);
                    Assert.That(light.range, Is.LessThanOrEqualTo(HiggsfieldMapCatalog.MaximumDecorLightRange));
                }
                Assert.That(decor.GetComponentsInChildren<Light>(true).Length, Is.LessThanOrEqualTo(HiggsfieldMapCatalog.MaximumDecorLights));
                var dressing = decor.GetComponent<HiggsfieldDecorDressing>();
                Assert.That(dressing, Is.Not.Null, entry.MapId + " dressing");
                if (dressing.Halos.Length > 0) Assert.That(dressing.Generated, Is.Not.Null, entry.MapId + " halos generated");
                if (dressing.Generated) Assert.That(dressing.Generated.GetComponentsInChildren<Collider>(true), Is.Empty);
                Object.Destroy(map.gameObject);
                Object.Destroy(bare.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DecorStaysOutOfRoutesPortalsSpawnsAndObjectivesAndRestsOnSurfaces()
        {
            var catalog = Catalog();
            foreach (var entry in catalog.Entries)
            {
                var (map, decor) = LoadWithDecor(entry);
                yield return null;
                var solid = new HashSet<Collider>(map.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && !IsBoundary(c.transform) && !c.transform.IsChildOf(decor.transform)));
                bool Separated(Vector3 a, Vector3 b)
                {
                    Vector3 d = b - a; float length = d.magnitude;
                    if (length < 1e-3f) return false;
                    return Physics.RaycastAll(a, d / length, length).Any(h => solid.Contains(h.collider) && Mathf.Abs(h.normal.y) < .5f) ||
                           Physics.RaycastAll(b, -d / length, length).Any(h => solid.Contains(h.collider) && Mathf.Abs(h.normal.y) < .5f);
                }
                var keepouts = HiggsfieldDecorClearance.Build(map);
                Assert.That(keepouts.Count, Is.GreaterThan(0), entry.MapId + " keep-outs");
                // Support: map colliders plus temporary colliders on the visual floors and on the decor itself (stacking).
                var temporary = new List<Collider>();
                foreach (var filter in map.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.GetComponent<Collider>() || !filter.sharedMesh || filter.GetComponent<MeshRenderer>() == null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    temporary.Add(collider);
                }
                Physics.SyncTransforms();
                var problems = new List<string>();
                foreach (var prop in decor.GetComponentsInChildren<Transform>(true))
                {
                    var parts = prop.name.Split(new[] { Separator }, StringSplitOptions.None);
                    if (parts.Length != 3) continue;
                    var renderers = prop.GetComponentsInChildren<Renderer>(true);
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var violation = HiggsfieldDecorClearance.FirstViolation(bounds, keepouts, Separated);
                    if (violation != null) problems.Add(prop.name + " in keep-out " + violation.Kind + " " + violation.Id);
                    if (parts[2] == "floor" || parts[2] == "on")
                    {
                        float gap = float.MaxValue;
                        foreach (var hit in Physics.RaycastAll(prop.position + Vector3.up * .3f, Vector3.down, .7f))
                        {
                            if (hit.collider.transform.IsChildOf(prop) || IsBoundary(hit.collider.transform) || hit.collider.isTrigger) continue;
                            gap = Mathf.Min(gap, Mathf.Abs(hit.point.y - prop.position.y));
                        }
                        if (gap > .02f) problems.Add(prop.name + " not resting on a surface (gap " + (gap == float.MaxValue ? "none" : gap.ToString("F3")) + ")");
                    }
                }
                // r2 (director #5): wall props keep 0.15 m from every window and door frame of the map.
                var config = DecorConfig().maps.FirstOrDefault(m => m.mapId == entry.MapId);
                if (config != null && !string.IsNullOrEmpty(config.openingPattern))
                {
                    var opening = new System.Text.RegularExpressions.Regex(config.openingPattern);
                    var openings = map.GetComponentsInChildren<Renderer>(true).Where(r => opening.IsMatch(r.name) && !r.transform.IsChildOf(decor.transform))
                        .Select(r => { var b = r.bounds; b.Expand(.3f); return (r.name, b); }).ToArray();
                    foreach (var prop in decor.GetComponentsInChildren<Transform>(true))
                    {
                        var parts = prop.name.Split(new[] { Separator }, StringSplitOptions.None);
                        if (parts.Length != 3 || parts[2] != "wall") continue;
                        var renderers = prop.GetComponentsInChildren<Renderer>(true);
                        Bounds bounds = renderers[0].bounds;
                        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                        foreach (var (name, box) in openings)
                            if (box.Intersects(bounds)) problems.Add(prop.name + " within 0.15 m of the opening " + name);
                    }
                }
                foreach (var collider in temporary) Object.Destroy(collider);
                Assert.That(problems, Is.Empty, entry.MapId + ":\n" + string.Join("\n", problems));
                Object.Destroy(map.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MenuBedroomFramesSleeperLampClockAndALargeMosquito()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BedroomPath);
            Assert.That(prefab, Is.Not.Null);
            var bedroom = Object.Instantiate(prefab);
            created.Add(bedroom);
            yield return null;
            var set = bedroom.GetComponent<MenuBedroomSet>();
            Assert.That(set && set.IsComplete, Is.True, "bedroom anchors");
            Assert.That(bedroom.GetComponentsInChildren<Collider>(true), Is.Empty, "bedroom colliders");
            var cameraObject = new GameObject("BedroomTestCamera");
            created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(set.CameraAnchor.position, set.CameraAnchor.rotation);
            camera.fieldOfView = set.CameraFieldOfView;
            camera.aspect = 16f / 9f;
            Transform Find(string name) => bedroom.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            Vector3 head = camera.WorldToViewportPoint(Find("Anchor_sleeper_head").position);
            Assert.That(head.x, Is.InRange(.45f, .75f), "sleeper head right of centre");
            Assert.That(head.y, Is.InRange(.25f, .6f), "sleeper head low in the frame");
            Assert.That(head.z, Is.GreaterThan(0));
            var lamp = bedroom.GetComponentsInChildren<Transform>(true).First(t => t.name == "Anchor_light" && t.parent.name.StartsWith("Prop_TableLamp"));
            Assert.That(camera.WorldToViewportPoint(lamp.position).x, Is.InRange(.72f, .98f), "lamp in the right third");
            var digits = bedroom.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Clock_Digits_0327");
            Assert.That(digits, Is.Not.Null, "03:27 digits");
            Assert.That(digits.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True, "digits glow");
            Assert.That(bedroom.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Prop_Window")), Is.True, "night window");
            var dressing = bedroom.GetComponent<HiggsfieldDecorDressing>();
            Assert.That(dressing && dressing.Generated, Is.True, "lamp halo generated");
            // Mosquito size along the flight loop (menu mosquito scale applied).
            var mosquito = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MosquitoPrefabPath));
            created.Add(mosquito);
            mosquito.transform.localScale *= set.MosquitoScale;
            float smallest = float.MaxValue; int inWindow = 0;
            foreach (var point in set.MosquitoPath)
            {
                mosquito.transform.position = point.position;
                yield return null;
                var renderers = mosquito.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                Bounds b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 v = camera.WorldToViewportPoint(corner);
                    min = Vector2.Min(min, v); max = Vector2.Max(max, v);
                }
                smallest = Mathf.Min(smallest, Mathf.Max((max.x - min.x) * 1920f, (max.y - min.y) * 1080f));
                Vector3 center = camera.WorldToViewportPoint(point.position);
                if (center.x >= .55f && center.x <= .75f && center.y >= .65f && center.y <= .85f) inWindow++;
            }
            Assert.That(smallest, Is.GreaterThanOrEqualTo(180f), "mosquito >= 180 px on a 1080p frame everywhere on its loop");
            // r2 (director #3): right of the logo and above the head (x .55-.75, y .65-.85) for >= 70 % of the loop; the
            // silhouette size itself is measured on render masks by the capture harness (menu-mosquito.json).
            Assert.That(inWindow / (float)set.MosquitoPath.Length, Is.GreaterThanOrEqualTo(.7f), "the loop hovers in the UI-06 window");
            Assert.That(set.MosquitoPath.Min(p => Vector3.Distance(p.position, set.EarAnchor.position)), Is.LessThan(.5f), "one dive buzzes the ear");
            Assert.That(set.SleepFace, Is.Not.Null);
            Assert.That(set.SleepFace.LidProtrusion, Is.InRange(.25f, .45f), "flattened sleeping lids (director #2)");
            Assert.That(set.SleepFace.HeadTurnDegrees, Is.InRange(10f, 20f), "head turned ~15 degrees toward the camera");
            var lights = bedroom.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Any(l => l.name.StartsWith("Moon_Fill")), Is.True, "cool moon fill from the window (director #2)");
        }

        [UnityTest]
        public IEnumerator LobbyDecorWarmsTheSalaWithoutCollidersAndRestores()
        {
            var lobby = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath));
            created.Add(lobby);
            int colliders = lobby.GetComponentsInChildren<Collider>(true).Length;
            var shell = lobby.transform.Find("lobby_alfa_static/LobbyAlfaSource/LobbyShell").GetComponent<Renderer>();
            var before = shell.sharedMaterials;
            var inlay = lobby.transform.Find("ArchitecturalTrim/Central_Textile_Inlay").GetComponent<Renderer>();
            var decor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LobbyDecorPath), lobby.transform, false);
            yield return null;
            Assert.That(lobby.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders), "no colliders added to the lobby");
            Assert.That(shell.sharedMaterials.Select(m => m.name), Does.Contain("Lobby_V030_Floor"));
            Assert.That(inlay.enabled, Is.False, "flat blue inlay hidden");
            Assert.That(decor.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Prop_RugStripedRedBlue")), Is.True, "striped rug");
            Assert.That(decor.GetComponentsInChildren<Light>(true).Length, Is.GreaterThanOrEqualTo(3), "warm lamps and wall pools");
            Assert.That(decor.GetComponentsInChildren<Light>(true).All(l => l.shadows == LightShadows.None), Is.True);
            // r2 (director #1): warm #FFB347 fills of 8 m, the table with its lantern in the middle of the room with chairs
            // around it (off the spawn grid), and no black pine cones indoors.
            Assert.That(decor.GetComponentsInChildren<Light>(true).Count(l => l.name.StartsWith("Sala_Fill") && l.range >= 7.9f), Is.GreaterThanOrEqualTo(1));
            var table = decor.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Prop_WoodTable"));
            Assert.That(new Vector2(table.localPosition.x, table.localPosition.z).magnitude, Is.LessThan(4f), "table in the middle of the room");
            // Prop roots only (each prop instance also has a model child of the same name).
            var chairs = decor.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Prop_WoodChair") && !(t.parent && t.parent.name.StartsWith("Prop_WoodChair"))).ToArray();
            Assert.That(chairs.Length, Is.InRange(4, 6), "4-6 chairs around the table");
            Assert.That(chairs.All(c => Vector3.Distance(c.position, table.position) < 1.6f), Is.True, "chairs around the table");
            var lobbyData = lobby.GetComponent<EnvironmentMapDefinition>();
            foreach (var spawn in lobbyData.LobbySpawnPoints)
                Assert.That(chairs.Concat(new[] { table }).All(p => Vector2.Distance(new Vector2(p.position.x, p.position.z), new Vector2(spawn.position.x, spawn.position.z)) > .6f),
                    Is.True, "no furniture on a lobby spawn " + spawn.name);
            var pines = lobby.transform.Find("Furnishings").Cast<Transform>().Where(t => t.name.StartsWith("Lobby_Pine")).ToArray();
            Assert.That(pines.Length, Is.EqualTo(4));
            Assert.That(pines.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).All(r => !r.enabled), Is.True, "indoor pines hidden");
            Object.Destroy(decor);
            yield return null;
            CollectionAssert.AreEqual(before, shell.sharedMaterials, "materials restored");
            Assert.That(inlay.enabled, Is.True, "inlay restored");
            Assert.That(pines.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).Any(r => r.enabled), Is.True, "pines restored");
        }

        [UnityTest]
        public IEnumerator MosquitoesStartAtLeastSixMetresFromTheHumansAndSpawnsLookIntoFreeSpace()
        {
            // r2 (director #9): nobody is bitten in the first second; human first views are free of decor too.
            var catalog = Catalog();
            foreach (var entry in catalog.Entries)
            {
                var (map, decor) = LoadWithDecor(entry);
                yield return null;
                for (int humans = 1; humans <= Mathf.Min(3, map.HumanSpawnPoints.Length); humans++)
                {
                    var order = AlfaApplication.MosquitoSpawnOrder(map, humans);
                    Assert.That(order.Length, Is.EqualTo(map.MosquitoSpawnPoints.Length));
                    for (int j = 0; j < Mathf.Min(2, order.Length); j++)
                    {
                        float nearest = Enumerable.Range(0, humans).Min(h => Vector3.Distance(map.MosquitoSpawnPoints[order[j]].position, map.HumanSpawnPoints[h].position));
                        Assert.That(nearest, Is.GreaterThanOrEqualTo(AlfaApplication.MinimumSpawnSeparation),
                            entry.MapId + " mosquito " + j + " with " + humans + " humans starts " + nearest.ToString("F1") + " m away");
                    }
                }
                var temporary = new List<Collider>();
                var solid = new HashSet<Collider>(map.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && !IsBoundary(c.transform)));
                foreach (var filter in map.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || filter.GetComponent<Collider>() || !filter.sharedMesh || IsBoundary(filter.transform)) continue;
                    if (renderer.sharedMaterials.Any(m => m && m.renderQueue >= 2500) || filter.name.StartsWith("Water") || filter.name.StartsWith("Foam")) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    temporary.Add(collider); solid.Add(collider);
                }
                Physics.SyncTransforms();
                for (int i = 0; i < map.HumanSpawnPoints.Length; i++)
                {
                    entry.TryGetSpawnYaw(true, i, out float yaw);
                    float free = 20f;
                    for (float d = -16f; d <= 16f; d += 4f)
                        foreach (float height in new[] { 1.55f, 1f, .5f })
                        {
                            Vector3 direction = Quaternion.AngleAxis(yaw + d, Vector3.up) * Vector3.forward;
                            foreach (var hit in Physics.RaycastAll(map.HumanSpawnPoints[i].position + Vector3.up * height, direction, 20f, ~0, QueryTriggerInteraction.Ignore))
                                if (solid.Contains(hit.collider)) free = Mathf.Min(free, hit.distance);
                        }
                    Assert.That(free, Is.GreaterThanOrEqualTo(1f), entry.MapId + " human-" + i + " faces an object at " + free.ToString("F2") + " m (yaw " + yaw + ")");
                }
                foreach (var collider in temporary) Object.Destroy(collider);
                Object.Destroy(map.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BootSceneRunsTheSleepingMenuInTheBedroomWithClosedEyes()
        {
            ValidationDataGuard.RequireDedicatedDataPath();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BootScene, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return null;
            var application = Object.FindFirstObjectByType<AlfaApplication>();
            Assert.That(application, Is.Not.Null);
            Assert.That(application.MenuBedroomPrefab, Is.Not.Null, "bedroom bound");
            Assert.That(application.LobbyDecorPrefab, Is.Not.Null, "lobby decor bound");
            Assert.That(application.MenuSleep && application.MenuSleepSwat, Is.True, "sleeping clips bound");
            for (int i = 0; i < 30; i++) yield return null;
            var living = Object.FindFirstObjectByType<MainMenuLivingScene>();
            Assert.That(living, Is.Not.Null);
            Assert.That(living.IsConfigured && living.Sleeping && living.IsRunning, Is.True, "sleeping living menu running");
            var bedroom = Object.FindFirstObjectByType<MenuBedroomSet>();
            Assert.That(bedroom, Is.Not.Null);
            var human = living.GetComponentsInChildren<CharacterView>(true).FirstOrDefault(v => v.GetComponentInChildren<VisualAttentionContract>(true) != null && !v.name.Contains("Mosquito"));
            Assert.That(human, Is.Not.Null, "decorative human");
            Assert.That(Vector3.Distance(human.transform.position, bedroom.SleeperRoot.position), Is.LessThan(.01f), "human lies at the sleeper anchor");
            Assert.That(Vector3.Angle(human.transform.up, bedroom.SleeperRoot.up), Is.LessThan(1f), "lying down along the bed");
            var contract = human.GetComponentInChildren<VisualAttentionContract>(true);
            var eyelids = contract.Rig.Eyelids;
            int blink = eyelids.sharedMesh.GetBlendShapeIndex(contract.Rig.LeftBlinkShapes[3]);
            Assert.That(eyelids.GetBlendShapeWeight(blink), Is.GreaterThan(99f), "eyes closed");
            // r2 (director #2): flattened lids on a private copy of the head mesh, closed-eye lines and a small smile.
            Assert.That(living.SleepFace, Is.Not.Null, "sleeping face built");
            Assert.That(living.SleepFace.IsBuilt && eyelids.sharedMesh == living.SleepFace.FlattenedMesh, Is.True, "flattened lids in use");
            Assert.That(living.SleepFace.LineCount, Is.EqualTo(3), "two closed-eye lines and a smile");
            Assert.That(living.SleepFace.LastTurnDegrees, Is.InRange(5f, 20f), "head turned toward the camera");
            Assert.That(Vector3.Distance(application.MenuCamera.transform.position, bedroom.CameraAnchor.position), Is.LessThan(.01f), "menu camera framed on the bedroom");
            Assert.That(human.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True, "decorative colliders off");
        }

        [UnityTest]
        public IEnumerator TrainingRoundsStartEveryMosquitoAtLeastSixMetresFromTheHuman()
        {
            // r2 (director #9) through the real runtime path: the first snapshot of a training round on every map, both roles.
            ValidationDataGuard.RequireDedicatedDataPath();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BootScene, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return null;
            var application = Object.FindFirstObjectByType<AlfaApplication>();
            Assert.That(application, Is.Not.Null);
            T Field<T>(string name) => (T)typeof(AlfaApplication).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(application);
            foreach (var entry in application.HiggsfieldMaps.Entries)
                foreach (var role in new[] { LetMeSleep.UI.AlfaRole.Human, LetMeSleep.UI.AlfaRole.Mosquito })
                {
                    application.StartTraining(role, LetMeSleep.UI.AlfaUiController.BloodModeId, entry.MapId);
                    var runtime = Field<LetMeSleep.Gameplay.Unity.GameplayRuntime>("game");
                    Assert.That(runtime, Is.Not.Null, entry.MapId + " " + role);
                    runtime.CaptureLocalInput = false;
                    for (int i = 0; i < 30 && runtime.LatestSnapshot == null; i++) yield return null;
                    var actors = runtime.LatestSnapshot.Actors;
                    var humans = actors.Where(a => a.Role == LetMeSleep.Core.PlayerRole.Human).Select(a => new Vector3(a.Position.X, a.Position.Y, a.Position.Z)).ToArray();
                    var mosquitoes = actors.Where(a => a.Role == LetMeSleep.Core.PlayerRole.Mosquito).Select(a => new Vector3(a.Position.X, a.Position.Y, a.Position.Z)).ToArray();
                    Assert.That(humans.Length, Is.EqualTo(1)); Assert.That(mosquitoes.Length, Is.EqualTo(2));
                    float nearest = mosquitoes.Min(m => humans.Min(h => Vector3.Distance(m, h)));
                    // A tick or two of motion at most: 6 m authored separation, 5.5 m after the first snapshot.
                    Assert.That(nearest, Is.GreaterThanOrEqualTo(5.5f), entry.MapId + " " + role + ": a mosquito starts " + nearest.ToString("F2") + " m from the human");
                    application.CancelTraining();
                    yield return null; yield return null;
                }
        }

        [UnityTest]
        public IEnumerator MosquitoThirdPersonKeepsTheBodyBelowTheCrosshair()
        {
            // r2 (director #9, UI-06 7b): the body at image x ~.6, y ~.7 (viewport y .3), never under the crosshair; first
            // person and a collapsed orbit bring it back to the centre.
            var cameraObject = new GameObject("FramingCamera", typeof(Camera));
            created.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.nearClipPlane = .02f; camera.aspect = 16f / 9f; camera.fieldOfView = 60f;
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(actor);
            actor.transform.position = new Vector3(0f, 50f, 0f);
            actor.transform.localScale = Vector3.one * .2f;
            Object.Destroy(actor.GetComponent<Collider>());
            var follow = cameraObject.AddComponent<MosquitoFollowCamera>();
            follow.SetCollisionFilter(c => false);
            follow.BindAnchors(actor.transform, actor.transform);
            follow.BindLocalVisual(actor.transform, new[] { actor.GetComponent<Renderer>() }, Array.Empty<Transform>());
            follow.SetView(Quaternion.Euler(10f, 30f, 0f), 1.6f);
            yield return new WaitForSecondsRealtime(.6f);
            Vector3 body = camera.WorldToViewportPoint(actor.transform.position);
            Assert.That(body.z, Is.GreaterThan(1f));
            // The anim framing lifts the camera over the body and converges the reticle on the flight line.
            Assert.That(body.y, Is.LessThan(.5f), "body below the crosshair");
            var bounds = actor.GetComponent<Renderer>().bounds;
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 v = camera.WorldToViewportPoint(new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                min = Vector2.Min(min, v); max = Vector2.Max(max, v);
            }
            Assert.That(min.x > .5f || max.y < .5f, Is.True, "the crosshair is not covered by the body");
            follow.SetView(Quaternion.Euler(10f, 30f, 0f), 0f);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Vector3.Distance(camera.transform.position, actor.transform.position), Is.LessThan(.05f), "first person keeps the centred eye");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in created) if (item) Object.Destroy(item);
            created.Clear();
            yield return null;
        }
    }
}
#endif
