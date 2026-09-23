using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Presentation;
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
    /// v0.3.0 menu bedroom (UI-06 screen 1, scenes.md #6) and warm "sala" (scenes.md #5, ENV-05), reproducible:
    ///   -executeMethod LetMeSleep.Editor.V030SceneTools.Run -v030Steps props,characters,menu
    /// - LMS_MenuBedroom.prefab: a closed, collider-free bedroom set placed 40 m in front of the private lobby (so the
    ///   sala never sees it) with Prop_BedSleeper against the back wall (headboard on the screen's right), nightstand,
    ///   warm table lamp (own rendering layer, excluded from its light) with halo, alarm clock reading 03:27 (emissive
    ///   seven-segment mesh), night window (sky #1A2A6A, hills, mullions, moon), navy walls over a #8B5A2B wainscot and a
    ///   #5A3A24 plank floor. <see cref="MenuBedroomSet"/> carries the sleeper anchor, the ear, the camera framing solved
    ///   so the head/lamp/mosquito land where UI-06 puts them, and a mosquito flight loop kept at >= 180 px on screen.
    /// - PrivateLobby-Decor.prefab: warm restyle of the lobby (floor, plaster, wainscot), a red/blue striped rug instead of
    ///   the flat blue inlay, a real night window in place of the framed print, and props for the "-back" views (table with
    ///   a lit lantern, bookshelf, crates, barrel, plants, painting) with two warm wall pools. Visual only.
    /// - Binds AlfaApplication (LetMeSleepHiggsfield.unity): MenuBedroomPrefab, LobbyDecorPrefab, MenuSleep, MenuSleepSwat.
    /// </summary>
    public static class V030MenuSceneBuilder
    {
        public const string Root = "Assets/LetMeSleep/Content/Environment/V030Scenes";
        public const string BedroomPath = Root + "/LMS_MenuBedroom.prefab";
        public const string LobbyDecorPath = Root + "/PrivateLobby-Decor.prefab";
        public const string ScenePath = "Assets/Scenes/LetMeSleepHiggsfield.unity";
        public const string KitPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldAtmosphereKit.asset";
        public const string MenuModel = "Assets/LetMeSleep/Content/Characters/Models/LMS_HumanMenu.fbx";
        public const string MosquitoPrefabPath = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Mosquito.prefab";
        public static readonly Vector3 BedroomOrigin = new Vector3(0f, 0f, -40f);
        public const int LampSelfRenderingLayer = 1;
        public const float MinimumMosquitoPixels = 180f;
        // UI-06 composition at 16:9 (viewport, origin bottom-left): the sleeper's head right of centre, the lamp in the
        // right third, the mosquito high on the right, the navigation rail over the left third.
        static readonly Vector2 HeadViewport = new Vector2(0.60f, 0.40f);
        static readonly Vector2 MosquitoLoopViewport = new Vector2(0.80f, 0.72f);
        const float CameraFov = 40f;

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);

        public static void Build(string output)
        {
            foreach (var folder in new[] { Root, Root + "/Materials", Root + "/Meshes" }) EnsureFolder(folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            materials.Clear();
            Flat("Scene_WallNavy", "#2A3560"); Flat("Scene_Wainscot", "#8B5A2B"); Flat("Scene_Trim", "#A86F3A");
            Flat("Scene_Baseboard", "#6E4424"); Flat("Scene_FloorA", "#5A3A24"); Flat("Scene_FloorB", "#654230");
            Flat("Scene_Ceiling", "#1B2444"); Flat("Scene_TableTop", "#A86F3A"); Flat("Scene_TableLeg", "#6E4424");
            Flat("Lobby_V030_Floor", "#B07442"); Flat("Lobby_V030_Plaster", "#8C8272"); Flat("Lobby_V030_Ceiling", "#3A3552");
            Flat("Scene_ClockDigits", "#FF3B30", emission: 2.5f);
            var kit = AssetDatabase.LoadAssetAtPath<HiggsfieldAtmosphereKit>(KitPath);
            Need(kit && kit.IsComplete, "Atmosphere kit missing or incomplete: " + KitPath);
            var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["unity"] = Application.unityVersion };
            receipt["bedroom"] = BuildBedroom(scene, kit);
            receipt["lobbyDecor"] = BuildLobbyDecor(scene, kit);
            receipt["scene"] = BindScene();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(output, "menu-scenes-receipt.json"), receipt.ToString(Formatting.Indented) + "\n");
            Debug.Log("LMS_V030_MENU_SCENES_BUILT " + BedroomPath + " " + LobbyDecorPath);
        }

        // ---------------------------------------------------------------- bedroom
        static JObject BuildBedroom(Scene scene, HiggsfieldAtmosphereKit kit)
        {
            var root = new GameObject("LMS_MenuBedroom");
            SceneManager.MoveGameObjectToScene(root, scene);
            // Authored at the origin (world == set space); moved to BedroomOrigin just before saving.
            var room = Child(root.transform, "Room");
            // Shell: every face points into the room; no colliders anywhere.
            const float x0 = -3.4f, x1 = 3.2f, z0 = -3.4f, z1 = 1.3f, h = 2.7f;
            float w = x1 - x0, d = z1 - z0, cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f;
            for (int i = 0; i < 14; i++)
            {
                float pz = z0 + (i + .5f) * d / 14f;
                Box(room, "Floor_Plank_" + i.ToString("00"), new Vector3(cx, -.03f, pz), new Vector3(w, .06f, d / 14f - .006f), i % 2 == 0 ? "Scene_FloorA" : "Scene_FloorB");
            }
            Box(room, "Floor_Gaps", new Vector3(cx, -.065f, cz), new Vector3(w, .01f, d), "Scene_Baseboard");
            Box(room, "Wall_Back", new Vector3(cx, h * .5f, z1 + .05f), new Vector3(w + .2f, h, .1f), "Scene_WallNavy");
            Box(room, "Wall_Right", new Vector3(x1 + .05f, h * .5f, cz), new Vector3(.1f, h, d + .2f), "Scene_WallNavy");
            Box(room, "Wall_Left", new Vector3(x0 - .05f, h * .5f, cz), new Vector3(.1f, h, d + .2f), "Scene_WallNavy");
            Box(room, "Wall_Front", new Vector3(cx, h * .5f, z0 - .05f), new Vector3(w + .2f, h, .1f), "Scene_WallNavy");
            Box(room, "Ceiling", new Vector3(cx, h + .05f, cz), new Vector3(w + .2f, .1f, d + .2f), "Scene_Ceiling");
            Wainscot(room, "Back", new Vector3(cx, 0, z1 - .02f), new Vector3(w, 0, 0));
            Wainscot(room, "Right", new Vector3(x1 - .02f, 0, cz), new Vector3(0, 0, d));
            Wainscot(room, "Left", new Vector3(x0 + .02f, 0, cz), new Vector3(0, 0, d));
            Box(room, "Ceiling_Beam", new Vector3(cx, h - .08f, .2f), new Vector3(w, .16f, .18f), "Scene_Baseboard");

            var furniture = Child(root.transform, "Furniture");
            var bed = Prop(furniture, "BedSleeper", new Vector3(.75f, 0, .70f), -90f);
            var headAnchor = Find(bed, "Anchor_sleeper_head");
            var sleeperRoot = Find(bed, "Anchor_sleeper_root");
            var nightstand = Prop(furniture, "Nightstand", new Vector3(2.42f, 0, .92f), 180f);
            var lamp = Prop(furniture, "TableLamp", Find(nightstand, "Anchor_lamp").position, 200f);
            foreach (var renderer in lamp.GetComponentsInChildren<Renderer>(true)) renderer.renderingLayerMask = 1u << LampSelfRenderingLayer;
            var window = Prop(furniture, "Window", new Vector3(.80f, 1.02f, z1 - .05f), 180f);
            Prop(furniture, "RugBlue", new Vector3(.35f, .001f, -.55f), 4f);
            Prop(furniture, "Chest", new Vector3(-.62f, 0, .92f), 180f);
            Prop(furniture, "Bookshelf", new Vector3(-2.65f, 0, 1.08f), 180f);
            Prop(furniture, "PottedPlant", new Vector3(-1.62f, 0, 1.0f), 30f);
            var painting = Prop(furniture, "PaintingLandscape", new Vector3(-1.75f, 1.42f, z1 - .03f), 180f);

            // Camera: solve the rotation that puts the head where UI-06 has it, from a slightly high, frontal position.
            Vector3 head = headAnchor.position;
            var cameraAnchor = Child(root.transform, "MenuCameraAnchor");
            cameraAnchor.position = head + new Vector3(-.42f, .78f, -2.25f);
            cameraAnchor.rotation = Aim(cameraAnchor.position, head, HeadViewport, CameraFov, 16f / 9f);

            var clock = Prop(furniture, "AlarmClock", Find(nightstand, "Anchor_clock").position, 0f);
            Vector3 toCamera = cameraAnchor.position - clock.transform.position; toCamera.y = 0;
            clock.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            var screen = Find(clock, "Anchor_screen");
            var digits = new GameObject("Clock_Digits_0327");
            digits.transform.SetParent(screen, false);
            digits.transform.localPosition = new Vector3(0, 0, .0012f);
            digits.AddComponent<MeshFilter>().sharedMesh = ClockMesh();
            var digitRenderer = digits.AddComponent<MeshRenderer>();
            digitRenderer.sharedMaterial = materials["Scene_ClockDigits"];
            digitRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Lights: warm lamp (never lighting its own shade), a soft fill on the sleeper's face, moonlight through the
            // window and a small key where the mosquito flies. All shadowless except the lamp.
            var lights = Child(root.transform, "Lights");
            var lampLight = PointLight(lights, "Lamp_Light", Find(lamp, "Anchor_light").position + (cameraAnchor.position - lamp.transform.position).normalized * .06f,
                new Color(1f, .72f, .42f), 3.2f, 3.8f);
            lampLight.shadows = LightShadows.Soft; lampLight.shadowStrength = .75f; lampLight.shadowBias = .03f; lampLight.shadowNormalBias = .1f;
            lampLight.renderingLayerMask = ~(1 << LampSelfRenderingLayer);
            PointLight(lights, "Sleeper_Fill", head + (cameraAnchor.position - head).normalized * .55f + Vector3.up * .15f, new Color(1f, .78f, .55f), .9f, 1.9f)
                .renderingLayerMask = -1;
            var moon = new GameObject("Moon_Window_Spot").AddComponent<Light>();
            moon.transform.SetParent(lights, false);
            Vector3 windowView = Find(window, "Anchor_view").position;
            moon.transform.position = windowView + new Vector3(0, .35f, .25f);
            moon.transform.rotation = Quaternion.LookRotation((head + new Vector3(-.6f, -.3f, 0) - moon.transform.position).normalized, Vector3.up);
            moon.type = LightType.Spot; moon.spotAngle = 75f; moon.innerSpotAngle = 35f; moon.range = 5.5f; moon.intensity = 2.2f;
            moon.color = new Color(.42f, .52f, 1f); moon.shadows = LightShadows.None; moon.bounceIntensity = 0; moon.renderingLayerMask = -1;

            // Mosquito loop in the top right, at a depth where the menu mosquito stays >= 180 px tall/wide on a 1080p frame.
            var mosquitoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MosquitoPrefabPath);
            Need(mosquitoPrefab, "Mosquito prefab missing");
            float mosquitoSize = MosquitoSize(mosquitoPrefab);
            const float loopDepth = 2.0f;
            float pixels = mosquitoSize / (2f * loopDepth * Mathf.Tan(CameraFov * .5f * Mathf.Deg2Rad)) * 1080f;
            float mosquitoScale = Mathf.Max(1f, 1.35f * MinimumMosquitoPixels / Mathf.Max(1f, pixels));
            var ear = Child(root.transform, "Sleeper_Ear");
            ear.position = head + Vector3.up * .15f + (cameraAnchor.position - head).normalized * .05f;
            var path = Child(root.transform, "MosquitoPath");
            var points = new Transform[8];
            var basis = cameraAnchor.rotation;
            float tanV = Mathf.Tan(CameraFov * .5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
            Vector3 FromViewport(Vector2 v, float depth) =>
                cameraAnchor.position + basis * new Vector3((v.x * 2f - 1f) * tanH * depth, (v.y * 2f - 1f) * tanV * depth, depth);
            Vector2[] loop =
            {
                MosquitoLoopViewport + new Vector2(.00f, .10f), MosquitoLoopViewport + new Vector2(.07f, .06f),
                MosquitoLoopViewport + new Vector2(.08f, -.04f), MosquitoLoopViewport + new Vector2(.02f, -.10f)
            };
            for (int i = 0; i < 4; i++) { points[i] = Child(path, "MenuMosquitoPath_" + i.ToString("00")); points[i].position = FromViewport(loop[i], loopDepth + (i % 2) * .25f); }
            // Dive to buzz at the sleeper's ear (the clumsy swat reacts there), then climb back past the lamp.
            points[4] = Child(path, "MenuMosquitoPath_04"); points[4].position = ear.position + new Vector3(.25f, .10f, -.30f);
            points[5] = Child(path, "MenuMosquitoPath_05"); points[5].position = ear.position + new Vector3(-.12f, .08f, -.22f);
            points[6] = Child(path, "MenuMosquitoPath_06"); points[6].position = ear.position + new Vector3(-.05f, .45f, -.45f);
            points[7] = Child(path, "MenuMosquitoPath_07"); points[7].position = FromViewport(MosquitoLoopViewport + new Vector2(-.07f, .06f), loopDepth);

            // The mosquito turns toward a point between the sleeper and the camera (eyes toward the room, UI-06) and gets a
            // soft key light so its red body and white eyes read against the navy wall.
            var faceTarget = Child(root.transform, "MosquitoFaceTarget");
            faceTarget.position = Vector3.Lerp(ear.position, cameraAnchor.position, .65f);
            Vector3 loopCentre = points.Take(4).Aggregate(Vector3.zero, (a, p) => a + p.position) / 4f;
            PointLight(lights, "Mosquito_Key", Vector3.Lerp(loopCentre, cameraAnchor.position, .35f) + Vector3.up * .15f, new Color(1f, .86f, .72f), 1.1f, 2.2f)
                .renderingLayerMask = -1;
            var set = root.AddComponent<MenuBedroomSet>();
            set.SleeperRoot = sleeperRoot; set.EarAnchor = ear; set.CameraAnchor = cameraAnchor; set.CameraFieldOfView = CameraFov;
            set.MosquitoPath = points; set.MosquitoScale = mosquitoScale; set.MosquitoFaceTarget = faceTarget;
            var dressing = root.AddComponent<HiggsfieldDecorDressing>();
            dressing.Kit = kit;
            dressing.Halos = new[]
            {
                new HiggsfieldDecorDressing.Halo { Anchor = Find(lamp, "Anchor_light"), Size = 1.25f, Color = new Color(1f, .70f, .28f, .42f), Intensity = 1f },
                new HiggsfieldDecorDressing.Halo { Anchor = Find(window, "Anchor_view"), Offset = new Vector3(.12f, .3f, -.06f), Size = .35f, Color = new Color(1f, .95f, .75f, .25f), Intensity = 1f }
            };
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            // Composition receipt: where the key elements land on a 16:9 frame.
            var report = new JObject
            {
                ["prefab"] = BedroomPath, ["origin"] = Vec(BedroomOrigin), ["camera"] = Vec(cameraAnchor.position), ["fov"] = CameraFov,
                ["mosquitoSizeM"] = Math.Round(mosquitoSize, 3), ["mosquitoScale"] = Math.Round(mosquitoScale, 3),
                ["mosquitoPixelsAtLoop"] = Math.Round(pixels * mosquitoScale, 1)
            };
            JArray View(Vector3 world)
            {
                Vector3 local = Quaternion.Inverse(basis) * (world - cameraAnchor.position);
                return new JArray(Math.Round(.5f + local.x / (local.z * tanH) * .5f, 3), Math.Round(.5f + local.y / (local.z * tanV) * .5f, 3), Math.Round(local.z, 2));
            }
            report["viewport"] = new JObject
            {
                ["head"] = View(head), ["lampLight"] = View(Find(lamp, "Anchor_light").position), ["clock"] = View(screen.position),
                ["window"] = View(windowView), ["painting"] = View(painting.transform.position), ["ear"] = View(ear.position),
                ["mosquitoPath"] = new JArray(points.Select(p => View(p.position)))
            };
            report["closestPassToEarM"] = Math.Round(ClosestPass(points, ear.position), 3);
            root.transform.position = BedroomOrigin;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, BedroomPath, out bool success);
            Need(success && saved, "Could not save " + BedroomPath);
            report["renderers"] = saved.GetComponentsInChildren<Renderer>(true).Length;
            report["lights"] = saved.GetComponentsInChildren<Light>(true).Length;
            Object.DestroyImmediate(root);
            return report;
        }

        static void Wainscot(Transform parent, string name, Vector3 center, Vector3 span)
        {
            bool alongX = Mathf.Abs(span.x) > 0;
            float length = alongX ? span.x : span.z;
            Vector3 Size(float along, float height, float depth) => alongX ? new Vector3(along, height, depth) : new Vector3(depth, height, along);
            Box(parent, "Wainscot_" + name, center + Vector3.up * .5f, Size(length, 1.0f, .04f), "Scene_Wainscot");
            Box(parent, "Wainscot_Rail_" + name, center + Vector3.up * 1.0f, Size(length, .06f, .07f), "Scene_Trim");
            Box(parent, "Baseboard_" + name, center + Vector3.up * .06f, Size(length, .12f, .06f), "Scene_Baseboard");
            int boards = Mathf.Max(2, Mathf.RoundToInt(length / .55f));
            for (int i = 1; i < boards; i++)
            {
                float t = -length * .5f + i * length / boards;
                Box(parent, "Wainscot_Stile_" + name + "_" + i.ToString("00"), center + Vector3.up * .5f + (alongX ? new Vector3(t, 0, 0) : new Vector3(0, 0, t)),
                    Size(.035f, .9f, .055f), "Scene_Trim");
            }
        }

        // ---------------------------------------------------------------- lobby decor
        static JObject BuildLobbyDecor(Scene scene, HiggsfieldAtmosphereKit kit)
        {
            var root = new GameObject("PrivateLobby-Decor");
            SceneManager.MoveGameObjectToScene(root, scene);
            var overrides = root.AddComponent<LobbyMaterialOverrides>();
            const string shell = "lobby_alfa_static/LobbyAlfaSource/LobbyShell";
            overrides.Swaps = new[]
            {
                new LobbyMaterialOverrides.Swap { Path = shell, FromMaterial = "Floor_Oak", To = materials["Lobby_V030_Floor"] },
                new LobbyMaterialOverrides.Swap { Path = shell, FromMaterial = "Plaster_Warm", To = materials["Lobby_V030_Plaster"] },
                new LobbyMaterialOverrides.Swap { Path = shell, FromMaterial = "Ceiling_Cream", To = materials["Lobby_V030_Ceiling"] }
            };
            overrides.Hide = new[] { "ArchitecturalTrim/Central_Textile_Inlay", "Furnishings/Lobby_Domestic/Menu_FramedNightLake" };
            var dressing = Child(root.transform, "Dressing");
            var rug = Prop(dressing, "RugStripedRedBlue", new Vector3(0, .004f, 0), 0f);
            rug.transform.localScale = new Vector3(2f, 1f, 2f);
            var window = Prop(dressing, "Window", new Vector3(3.10f, 1.46f, 5.90f), 180f);
            // Front wall (seen in the sala "-back" views): wainscot, table with a lit lantern, shelf, crates, plants.
            Wainscot(dressing, "Front", new Vector3(0, 0, -5.91f), new Vector3(13.7f, 0, 0));
            Wainscot(dressing, "West", new Vector3(-6.91f, 0, 0), new Vector3(0, 0, 11.7f));
            Wainscot(dressing, "East", new Vector3(6.91f, 0, 0), new Vector3(0, 0, 11.7f));
            var table = Child(dressing, "Lobby_Table");
            table.localPosition = new Vector3(-2.3f, 0, -5.25f);
            Box(table, "Top", new Vector3(0, .74f, 0), new Vector3(1.4f, .06f, .78f), "Scene_TableTop");
            foreach (float x in new[] { -.62f, .62f }) foreach (float z in new[] { -.32f, .32f })
                Box(table, "Leg", new Vector3(x, .355f, z), new Vector3(.08f, .71f, .08f), "Scene_TableLeg");
            Box(table, "Apron", new Vector3(0, .66f, 0), new Vector3(1.3f, .1f, .68f), "Scene_TableLeg");
            var lantern = Prop(dressing, "HandLantern", new Vector3(-2.72f, .77f, -5.28f), 20f);
            Prop(dressing, "Mug", new Vector3(-1.95f, .77f, -5.12f), 140f);
            Prop(dressing, "RolledMap", new Vector3(-2.2f, .77f, -5.42f), 12f);
            // Seen from the sala spawns facing -Z the image left is +X: shelf and crates left of the table, plant right.
            Prop(dressing, "Bookshelf", new Vector3(1.30f, 0, -5.70f), 0f);
            Prop(dressing, "Crate", new Vector3(2.85f, 0, -5.42f), 8f);
            Prop(dressing, "Crate", new Vector3(2.88f, .70f, -5.44f), 23f);
            Prop(dressing, "Barrel", new Vector3(3.80f, 0, -5.40f), 0f);
            Prop(dressing, "Chest", new Vector3(-4.35f, 0, -5.45f), 0f);
            Prop(dressing, "PottedPlant", new Vector3(-3.45f, 0, -5.55f), 60f);
            Prop(dressing, "PottedPlant", new Vector3(.25f, 0, -5.55f), 10f);
            Prop(dressing, "PaintingLandscape", new Vector3(-.95f, 1.40f, -5.87f), 0f);
            Prop(dressing, "HangingPlant", new Vector3(5.2f, 3.0f - 1.03f, -4.6f), 0f);
            // West-front corner (the "sala-0-back" view): barrel, plant and a painting over the bench.
            Prop(dressing, "Barrel", new Vector3(-6.35f, 0, -3.75f), 20f);
            Prop(dressing, "PottedPlant", new Vector3(-5.2f, 0, -5.5f), 110f);
            Prop(dressing, "PaintingLandscape", new Vector3(-6.94f, 1.45f, -2.3f), 90f);
            // Warm hanging lanterns under the two ceiling beams (x = +-4.7): the room reads lit by lamps, not by the moon.
            var hanging = new List<GameObject>();
            foreach (float x in new[] { -4.7f, 4.7f })
                foreach (float z in new[] { -2.4f, 2.4f })
                {
                    var hung = Prop(dressing, "HandLantern", new Vector3(x, 2.30f, z), 15f);
                    hung.name = "Prop_HandLantern_Hanging";
                    Box(dressing, "Lantern_Chain", new Vector3(x, 2.30f + .392f + .16f, z), new Vector3(.018f, .32f, .018f), "Scene_TableLeg");
                    hanging.Add(hung);
                }
            var lights = Child(root.transform, "Lights");
            var lanternLight = PointLight(lights, "Table_Lantern_Light", Find(lantern, "Anchor_light").position + new Vector3(0, .02f, .12f), new Color(1f, .68f, .36f), 2.6f, 4.2f);
            var corner = PointLight(lights, "Corner_Warm_Pool", new Vector3(3.3f, 1.85f, -5.35f), new Color(1f, .66f, .35f), 2.2f, 3.4f);
            var hangingLights = hanging.Select((h, i) => PointLight(lights, "Hanging_Lantern_Light_" + i, Find(h, "Anchor_light").position + Vector3.down * .05f,
                new Color(1f, .70f, .42f), 2.4f, 6.0f)).ToArray();
            var dressingComponent = root.AddComponent<HiggsfieldDecorDressing>();
            dressingComponent.Kit = kit;
            dressingComponent.Halos = new[] { lantern }.Concat(hanging).Select(l => new HiggsfieldDecorDressing.Halo
                { Anchor = Find(l, "Anchor_light"), Size = .9f, Color = new Color(1f, .70f, .28f, .38f), Intensity = 1f }).ToArray();
            dressingComponent.Flickers = new[] { lanternLight }.Concat(hangingLights).Select((l, i) => new HiggsfieldDecorDressing.Flicker
                { Light = l, Amplitude = .07f, Seed = 3.1f + i * 1.7f }).ToArray();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, LobbyDecorPath, out bool success);
            Need(success && saved, "Could not save " + LobbyDecorPath);
            var report = new JObject
            {
                ["prefab"] = LobbyDecorPath, ["renderers"] = saved.GetComponentsInChildren<Renderer>(true).Length,
                ["lights"] = saved.GetComponentsInChildren<Light>(true).Length, ["colliders"] = saved.GetComponentsInChildren<Collider>(true).Length
            };
            Object.DestroyImmediate(root);
            return report;
        }

        // ---------------------------------------------------------------- scene binding
        static JObject BindScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var apps = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AlfaApplication>(true)).ToArray();
            Need(apps.Length == 1, "Expected one AlfaApplication in " + ScenePath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(MenuModel).OfType<AnimationClip>().ToArray();
            var sleep = clips.SingleOrDefault(c => c.name == "MenuSleep");
            var swat = clips.SingleOrDefault(c => c.name == "MenuSleepSwat");
            Need(sleep && swat, "MenuSleep/MenuSleepSwat missing in " + MenuModel + " (run the characters step first)");
            var bedroom = AssetDatabase.LoadAssetAtPath<GameObject>(BedroomPath);
            var lobbyDecor = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyDecorPath);
            var serialized = new SerializedObject(apps[0]);
            bool changed = false;
            void Set(string field, Object value)
            {
                var property = serialized.FindProperty(field);
                Need(property != null, "AlfaApplication field missing: " + field);
                if (property.objectReferenceValue == value) return;
                property.objectReferenceValue = value; changed = true;
            }
            Set("MenuBedroomPrefab", bedroom); Set("LobbyDecorPrefab", lobbyDecor); Set("MenuSleep", sleep); Set("MenuSleepSwat", swat);
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Need(EditorSceneManager.SaveScene(scene), "Could not save " + ScenePath);
            }
            return new JObject { ["scene"] = ScenePath, ["changed"] = changed, ["menuSleep"] = sleep.length, ["menuSleepSwat"] = swat.length };
        }

        // ---------------------------------------------------------------- helpers
        static Quaternion Aim(Vector3 camera, Vector3 target, Vector2 viewport, float fov, float aspect)
        {
            // Rotate so the target projects at the requested viewport point (exact for yaw/pitch without roll).
            float tanV = Mathf.Tan(fov * .5f * Mathf.Deg2Rad), tanH = tanV * aspect;
            Vector3 dir = (target - camera).normalized;
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            for (int i = 0; i < 20; i++)
            {
                Vector3 local = Quaternion.Inverse(look) * (target - camera);
                float u = .5f + local.x / (local.z * tanH) * .5f, v = .5f + local.y / (local.z * tanV) * .5f;
                float yaw = Mathf.Atan((u - viewport.x) * 2f * tanH) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan((v - viewport.y) * 2f * tanV) * Mathf.Rad2Deg;
                if (Mathf.Abs(yaw) < .01f && Mathf.Abs(pitch) < .01f) break;
                Vector3 e = look.eulerAngles;
                look = Quaternion.Euler(e.x + pitch, e.y + yaw, 0f);
            }
            return look;
        }

        static float ClosestPass(Transform[] points, Vector3 target)
        {
            int count = points.Length; float best = float.MaxValue;
            for (int k = 0; k < 800; k++)
            {
                float step = k / 800f * count; int index = Mathf.FloorToInt(step) % count; float t = step - Mathf.Floor(step);
                Vector3 center = points[index].position;
                Vector3 start = (points[(index + count - 1) % count].position + center) * .5f, end = (center + points[(index + 1) % count].position) * .5f;
                Vector3 p = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * center + t * t * end;
                best = Mathf.Min(best, Vector3.Distance(p, target));
            }
            return best;
        }

        static float MosquitoSize(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                return Mathf.Max(bounds.size.x, bounds.size.y);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static Mesh ClockMesh()
        {
            string path = Root + "/Meshes/Clock_Digits_0327.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = "Clock_Digits_0327" };
            mesh.Clear();
            const float height = .046f, width = .026f, stroke = .0068f, gap = .0085f, colon = .012f;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Quad(float xMin, float yMin, float xMax, float yMax)
            {
                int b = vertices.Count;
                // Seen from the front (+Z of the screen anchor) the anchor's +X is the viewer's left: lay digits out along -X.
                vertices.Add(new Vector3(-xMin, yMin, 0)); vertices.Add(new Vector3(-xMin, yMax, 0));
                vertices.Add(new Vector3(-xMax, yMax, 0)); vertices.Add(new Vector3(-xMax, yMin, 0));
                // Both windings: the screen face and its anchor are authored in Blender, never trust one side.
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            string[] segments = { "abcdef", "abcdg", "abged", "abc" }; // 0, 3, 2, 7
            float total = 4 * width + 3 * gap + colon;
            float x = -total * .5f, y0 = -height * .5f, y1 = height * .5f, ym = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == 2)
                {
                    float cxm = x - gap * .5f + colon * .5f - gap * .5f + gap * .5f;
                    Quad(cxm - stroke * .5f, ym + height * .15f, cxm + stroke * .5f, ym + height * .15f + stroke);
                    Quad(cxm - stroke * .5f, ym - height * .15f - stroke, cxm + stroke * .5f, ym - height * .15f);
                    x += colon;
                }
                string s = segments[i];
                float l = x, r = x + width;
                if (s.Contains('a')) Quad(l + stroke * .6f, y1 - stroke, r - stroke * .6f, y1);
                if (s.Contains('d')) Quad(l + stroke * .6f, y0, r - stroke * .6f, y0 + stroke);
                if (s.Contains('g')) Quad(l + stroke * .6f, ym - stroke * .5f, r - stroke * .6f, ym + stroke * .5f);
                if (s.Contains('f')) Quad(l, ym + stroke * .3f, l + stroke, y1 - stroke * .3f);
                if (s.Contains('b')) Quad(r - stroke, ym + stroke * .3f, r, y1 - stroke * .3f);
                if (s.Contains('e')) Quad(l, y0 + stroke * .3f, l + stroke, ym - stroke * .3f);
                if (s.Contains('c')) Quad(r - stroke, y0 + stroke * .3f, r, ym - stroke * .3f);
                x += width + gap;
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(vertices.Select(_ => Vector3.forward).ToList());
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static GameObject Prop(Transform parent, string prop, Vector3 localPosition, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(V030PropLibraryImporter.PrefabsRoot + "/Prop_" + prop + ".prefab");
            Need(prefab, "Library prop missing: " + prop);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static Transform Find(GameObject go, string name) => Find(go.transform, name);
        static Transform Find(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Need(found, "Missing " + name + " under " + root.name);
            return found;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Box(Transform parent, string name, Vector3 localCenter, Vector3 size, string material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[material];
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        static Light PointLight(Transform parent, string name, Vector3 worldPosition, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.bounceIntensity = 0f; light.renderMode = LightRenderMode.Auto;
            light.cullingMask &= ~(1 << 30);
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        static void Flat(string name, string hex, float emission = 0f)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !material;
            if (created) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            Need(ColorUtility.TryParseHtmlString(hex, out var color), "Bad colour " + hex);
            material.SetColor("_BaseColor", color); material.SetColor("_Color", color);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .12f); material.SetFloat("_EnvironmentReflections", 0);
            if (emission > 0)
            {
                Color linear = color.linear * emission;
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
            if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            materials[name] = material;
        }

        static JArray Vec(Vector3 v) => new JArray(Math.Round(v.x, 3), Math.Round(v.y, 3), Math.Round(v.z, 3));

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
