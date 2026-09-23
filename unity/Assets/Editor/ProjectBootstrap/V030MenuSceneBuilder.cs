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
        const float CameraFov = 40f;
        public const string DefaultTuning = "docs/v030/maps/scenes-v030.json";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);

        /// <summary>
        /// v0.3.0 scenes r2 (art director's corrections): measured values live in docs/v030/maps/scenes-v030.json so the
        /// menu bedroom and the sala can be tuned against captures without touching code. Missing fields keep these defaults.
        /// </summary>
        public sealed class Tuning
        {
            public string schema;
            public Dictionary<string, string> colors = new Dictionary<string, string>();
            public LightTune[] bedroomLights = Array.Empty<LightTune>();
            public LightTune[] lobbyLights = Array.Empty<LightTune>();
            public float mosquitoScale = 1.75f, mosquitoLoopDepth = 2f, mosquitoFacing = .85f, mosquitoKeyIntensity = .8f;
            public float[] mosquitoLoopViewport = { .65f, .75f }, mosquitoLoopRadius = { .06f, .055f };
            public int mosquitoLoopPoints = 11, mosquitoDiveIndex = 4;
            public float lidProtrusion = .35f, headTurnDegrees = 15f, lineWidth = .0062f, smileWidth = .05f, lineAngle = 62f;
            public float lobbyRugScale = 2f, mosquitoPupilScale = 1.32f;
            public bool mosquitoHover = true;
            public float mosquitoHoverWobbleBeats = .12f, mosquitoHoverRate = 9f;
        }
        public sealed class LightTune
        {
            public string name, color = "#FFB347", type = "Point";
            public float[] position, target;
            public float intensity = 1f, range = 5f, spotAngle = 90f, innerSpotAngle = 50f;
        }
        static Tuning tuning = new Tuning();

        static string Hex(string material, string fallback) => tuning.colors != null && tuning.colors.TryGetValue(material, out var hex) ? hex : fallback;

        public static void Build(string output)
        {
            foreach (var folder in new[] { Root, Root + "/Materials", Root + "/Meshes" }) EnsureFolder(folder);
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string tuningPath = V030PropLibraryImporter.Argument("-v030ScenesTuning") ?? Path.Combine(repository, DefaultTuning);
            tuning = File.Exists(tuningPath) ? JsonConvert.DeserializeObject<Tuning>(File.ReadAllText(tuningPath)) : new Tuning();
            Need(tuning != null && (tuning.schema == null || tuning.schema == "lms.scenes.v030/1"), "Unexpected scenes tuning schema: " + tuningPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            materials.Clear();
            foreach (var (name, hex) in new[]
            {
                ("Scene_WallNavy", "#2A3560"), ("Scene_Wainscot", "#8B5A2B"), ("Scene_Trim", "#A86F3A"), ("Scene_Baseboard", "#6E4424"),
                ("Scene_FloorA", "#5A3A24"), ("Scene_FloorB", "#654230"), ("Scene_Ceiling", "#1B2444"), ("Scene_TableTop", "#A86F3A"),
                ("Scene_TableLeg", "#6E4424"), ("Lobby_V030_Floor", "#B07442"), ("Lobby_V030_Plaster", "#8C8272"), ("Lobby_V030_Ceiling", "#3A3552"),
                ("Lobby_V030_Wainscot", "#8B5A2B")
            }) Flat(name, Hex(name, hex));
            Flat("Scene_ClockDigits", "#FF3B30", emission: 2.5f);
            var kit = AssetDatabase.LoadAssetAtPath<HiggsfieldAtmosphereKit>(KitPath);
            Need(kit && kit.IsComplete, "Atmosphere kit missing or incomplete: " + KitPath);
            var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["unity"] = Application.unityVersion, ["tuning"] = tuningPath };
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
            // r2 (director #2): cool moonlight spilling from the window onto the left and back-left walls (tuned lights).
            foreach (var spec in tuning.bedroomLights ?? Array.Empty<LightTune>()) TunedLight(lights, spec);

            // r2 (director #3): the mosquito is a protagonist: >= 380 px wingspan and >= 220 px tall on a 1080p frame,
            // hovering right of the logo and above the head (viewport x .55-.75, y .65-.85) for >= 70 % of the loop, with one
            // quick dive to buzz the sleeper's ear. The loop is a ring of control points in that window at a fixed depth; the
            // dive sits where MainMenuLivingScene's time warp moves fastest (phase ~.35).
            var mosquitoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MosquitoPrefabPath);
            Need(mosquitoPrefab, "Mosquito prefab missing");
            float mosquitoSize = MosquitoSize(mosquitoPrefab);
            float loopDepth = tuning.mosquitoLoopDepth;
            float pixels = mosquitoSize / (2f * loopDepth * Mathf.Tan(CameraFov * .5f * Mathf.Deg2Rad)) * 1080f;
            float mosquitoScale = Mathf.Max(tuning.mosquitoScale, 1.35f * MinimumMosquitoPixels / Mathf.Max(1f, pixels));
            var ear = Child(root.transform, "Sleeper_Ear");
            ear.position = head + Vector3.up * .15f + (cameraAnchor.position - head).normalized * .05f;
            var path = Child(root.transform, "MosquitoPath");
            var basis = cameraAnchor.rotation;
            float tanV = Mathf.Tan(CameraFov * .5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
            Vector3 FromViewport(Vector2 v, float depth) =>
                cameraAnchor.position + basis * new Vector3((v.x * 2f - 1f) * tanH * depth, (v.y * 2f - 1f) * tanV * depth, depth);
            var loopCenter = new Vector2(tuning.mosquitoLoopViewport[0], tuning.mosquitoLoopViewport[1]);
            var loopRadius = new Vector2(tuning.mosquitoLoopRadius[0], tuning.mosquitoLoopRadius[1]);
            int ring = Mathf.Max(6, tuning.mosquitoLoopPoints);
            int dive = Mathf.Clamp(tuning.mosquitoDiveIndex, 1, ring - 1);
            var positions = new List<Vector3>();
            for (int k = 0; k < ring; k++)
            {
                // Clockwise from the top of the ring; a gentle figure-of-eight wobble in depth keeps it alive.
                float a = Mathf.PI * .5f - k * 2f * Mathf.PI / ring;
                var v = loopCenter + new Vector2(Mathf.Cos(a) * loopRadius.x, Mathf.Sin(a) * loopRadius.y);
                positions.Add(FromViewport(v, loopDepth + .12f * Mathf.Sin(2f * a)));
                if (k == dive - 1)
                {
                    // Dive to buzz at the sleeper's ear (the clumsy swat reacts there), then climb straight back.
                    positions.Add(ear.position + new Vector3(.22f, .16f, -.28f));
                    positions.Add(ear.position + new Vector3(-.10f, .08f, -.22f));
                    positions.Add(ear.position + new Vector3(.05f, .42f, -.42f));
                }
            }
            var points = new Transform[positions.Count];
            for (int i = 0; i < points.Length; i++) { points[i] = Child(path, "MenuMosquitoPath_" + i.ToString("00")); points[i].position = positions[i]; }

            // The mosquito turns toward a point between the sleeper and the camera (eyes and pupils toward the viewer, UI-06)
            // and gets a soft key light so its red body reads against the navy wall; the key stays below the bloom threshold
            // on the eye whites so the black pupils survive (director #3).
            var faceTarget = Child(root.transform, "MosquitoFaceTarget");
            faceTarget.position = Vector3.Lerp(ear.position, cameraAnchor.position, .8f);
            Vector3 loopCentre = FromViewport(loopCenter, loopDepth);
            PointLight(lights, "Mosquito_Key", Vector3.Lerp(loopCentre, cameraAnchor.position, .35f) + Vector3.up * .15f, new Color(1f, .86f, .72f),
                tuning.mosquitoKeyIntensity, 2.2f).renderingLayerMask = -1;
            var set = root.AddComponent<MenuBedroomSet>();
            set.SleeperRoot = sleeperRoot; set.EarAnchor = ear; set.CameraAnchor = cameraAnchor; set.CameraFieldOfView = CameraFov;
            set.MosquitoPath = points; set.MosquitoScale = mosquitoScale; set.MosquitoFaceTarget = faceTarget;
            set.MosquitoFacing = tuning.mosquitoFacing;
            set.MosquitoPupilScale = tuning.mosquitoPupilScale;
            var hover = HoverPhase(mosquitoPrefab, mosquitoScale, basis);
            report_hover = hover;
            if (tuning.mosquitoHover && hover.time >= 0f)
            {
                set.FlightHoverTime = hover.time;
                set.FlightHoverWobble = hover.beat * tuning.mosquitoHoverWobbleBeats;
                set.FlightHoverRate = tuning.mosquitoHoverRate;
            }
            set.SleepFace = new SleepingFaceRig.Settings
            {
                LidProtrusion = tuning.lidProtrusion, HeadTurnDegrees = tuning.headTurnDegrees, LineWidth = tuning.lineWidth, SmileWidth = tuning.smileWidth,
                LineAngleFromFront = tuning.lineAngle
            };
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
            report["flightHover"] = new JObject
            {
                ["time"] = Math.Round(report_hover.time, 4), ["beatSeconds"] = Math.Round(report_hover.beat, 4),
                ["spanPxAtLoop"] = Math.Round(report_hover.widest / (2f * tuning.mosquitoLoopDepth * tanH) * 1920f, 1),
                ["narrowestSpanPxAtLoop"] = Math.Round(report_hover.narrowest / (2f * tuning.mosquitoLoopDepth * tanH) * 1920f, 1),
                ["heightPxAtLoop"] = Math.Round(report_hover.height / (2f * tuning.mosquitoLoopDepth * tanV) * 1080f, 1)
            };
            // Share of the loop time with the mosquito's centre in the UI-06 window, with MainMenuLivingScene's time warp.
            int inWindow = 0; const int steps = 2000;
            for (int k = 0; k < steps; k++)
            {
                float r = k / (float)steps;
                float warped = r + .4f / (2f * Mathf.PI) * (1f - Mathf.Cos(2f * Mathf.PI * r));
                float phase = Mathf.Repeat(warped + .5f / points.Length, 1f);
                var local = View(PathPoint(points, phase));
                double x = (double)local[0], y = (double)local[1];
                if (x >= .55 && x <= .75 && y >= .65 && y <= .85) inWindow++;
            }
            report["loopTimeInWindow"] = Math.Round(inWindow / (double)steps, 3);
            root.transform.position = BedroomOrigin;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, BedroomPath, out bool success);
            Need(success && saved, "Could not save " + BedroomPath);
            report["renderers"] = saved.GetComponentsInChildren<Renderer>(true).Length;
            report["lights"] = saved.GetComponentsInChildren<Light>(true).Length;
            Object.DestroyImmediate(root);
            return report;
        }

        static void Wainscot(Transform parent, string name, Vector3 center, Vector3 span, string material = "Scene_Wainscot")
        {
            bool alongX = Mathf.Abs(span.x) > 0;
            float length = alongX ? span.x : span.z;
            Vector3 Size(float along, float height, float depth) => alongX ? new Vector3(along, height, depth) : new Vector3(depth, height, along);
            Box(parent, "Wainscot_" + name, center + Vector3.up * .5f, Size(length, 1.0f, .04f), material);
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
            // r2 (director #1): the four indoor pines read as black cones: hidden (their trunk colliders stay, the potted
            // plants below stand where they were) and replaced by lit potted plants.
            overrides.Hide = new[]
            {
                "ArchitecturalTrim/Central_Textile_Inlay", "Furnishings/Lobby_Domestic/Menu_FramedNightLake",
                "Furnishings/Lobby_Pine_5p85_4p75", "Furnishings/Lobby_Pine_5p85_m4p75",
                "Furnishings/Lobby_Pine_m5p85_4p75", "Furnishings/Lobby_Pine_m5p85_m4p75"
            };
            var dressing = Child(root.transform, "Dressing");
            var rug = Prop(dressing, "RugStripedRedBlue", new Vector3(0, .004f, 0), 0f);
            rug.transform.localScale = new Vector3(tuning.lobbyRugScale, 1f, tuning.lobbyRugScale);
            var window = Prop(dressing, "Window", new Vector3(3.10f, 1.46f, 5.90f), 180f);
            // Wainscot on the three walls around the room (own material, tuned against the sala captures).
            Wainscot(dressing, "Front", new Vector3(0, 0, -5.91f), new Vector3(13.7f, 0, 0), "Lobby_V030_Wainscot");
            Wainscot(dressing, "West", new Vector3(-6.91f, 0, 0), new Vector3(0, 0, 11.7f), "Lobby_V030_Wainscot");
            Wainscot(dressing, "East", new Vector3(6.91f, 0, 0), new Vector3(0, 0, 11.7f), "Lobby_V030_Wainscot");
            // UI-06 screen 3: the table with the lit lantern stands in the middle of the room, right behind the rug (between
            // the spawn grid and the back sofas), with six wooden chairs around it. Visual only: players walk through.
            var tableCenter = new Vector3(0f, 0f, 3.35f);
            var table = Prop(dressing, "WoodTable", tableCenter, 0f);
            Vector3 top = Find(table, "Anchor_top").position;
            var lantern = Prop(dressing, "HandLantern", top + new Vector3(-.25f, 0f, .05f), 20f);
            Prop(dressing, "Mug", top + new Vector3(.3f, 0f, -.18f), 140f);
            Prop(dressing, "Mug", top + new Vector3(.52f, 0f, .16f), 60f);
            Prop(dressing, "RolledMap", top + new Vector3(.12f, 0f, .2f), 12f);
            foreach (var (offset, yaw) in new[]
            {
                (new Vector3(-.45f, 0, -.78f), 4f), (new Vector3(.47f, 0, -.8f), -7f),      // rug side, facing the table (+Z)
                (new Vector3(-.42f, 0, .8f), 186f), (new Vector3(.48f, 0, .76f), 173f),     // sofa side
                (new Vector3(-1.2f, 0, .02f), 92f), (new Vector3(1.22f, 0, -.04f), -88f)    // the two ends
            }) Prop(dressing, "WoodChair", tableCenter + offset, yaw);
            // Front wall (seen in the sala "-back" views): shelf, crates, barrel, chest with a lantern, plants, paintings.
            Prop(dressing, "Bookshelf", new Vector3(1.30f, 0, -5.70f), 0f);
            Prop(dressing, "Crate", new Vector3(2.85f, 0, -5.42f), 8f);
            Prop(dressing, "Crate", new Vector3(2.88f, .70f, -5.44f), 23f);
            Prop(dressing, "Barrel", new Vector3(3.80f, 0, -5.40f), 0f);
            Prop(dressing, "Chest", new Vector3(-2.35f, 0, -5.45f), 0f);
            var chestLantern = Prop(dressing, "HandLantern", new Vector3(-2.05f, .6f, -5.45f), -15f);
            Prop(dressing, "WallShelf", new Vector3(-2.35f, 1.35f, -5.81f), 0f);
            Prop(dressing, "PottedPlant", new Vector3(-3.45f, 0, -5.55f), 60f);
            Prop(dressing, "PottedPlant", new Vector3(.25f, 0, -5.55f), 10f);
            Prop(dressing, "PaintingLandscape", new Vector3(-.95f, 1.40f, -5.87f), 0f);
            Prop(dressing, "HangingPlant", new Vector3(5.2f, 3.0f - 1.03f, -4.6f), 0f);
            // West-front corner (the "sala-0-back" view): barrel and a painting over the bench.
            Prop(dressing, "Barrel", new Vector3(-6.35f, 0, -3.75f), 20f);
            Prop(dressing, "PaintingLandscape", new Vector3(-6.94f, 1.45f, -2.3f), 90f);
            // Lit potted plants where the pines stood (corner lights below).
            var plants = new List<GameObject>();
            foreach (var (x, z, yaw) in new[] { (5.85f, 4.75f, 20f), (5.85f, -4.75f, 75f), (-5.85f, 4.75f, 140f), (-5.85f, -4.75f, 250f) })
            {
                var plant = Prop(dressing, "PottedPlant", new Vector3(x, 0, z), yaw);
                plant.transform.localScale = Vector3.one * 1.45f;
                plants.Add(plant);
            }
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
            var lanternLight = PointLight(lights, "Table_Lantern_Light", Find(lantern, "Anchor_light").position + new Vector3(0, .02f, 0), new Color(1f, .68f, .36f), 2.6f, 4.5f);
            var corner = PointLight(lights, "Corner_Warm_Pool", new Vector3(3.3f, 1.85f, -5.35f), new Color(1f, .66f, .35f), 2.2f, 3.4f);
            var hangingLights = hanging.Select((h, i) => PointLight(lights, "Hanging_Lantern_Light_" + i, Find(h, "Anchor_light").position + Vector3.down * .05f,
                new Color(1f, .70f, .42f), 2.4f, 6.0f)).ToArray();
            // r2 (director #1): warm #FFB347 fills (range 8 m) and wall pools until the floor, wainscot, walls and rug read
            // their UI-06 colours (values in docs/v030/maps/scenes-v030.json, measured by measure_scenes.py).
            var fills = (tuning.lobbyLights ?? Array.Empty<LightTune>()).Select(spec => TunedLight(lights, spec)).ToArray();
            var dressingComponent = root.AddComponent<HiggsfieldDecorDressing>();
            dressingComponent.Kit = kit;
            dressingComponent.Halos = new[] { lantern, chestLantern }.Concat(hanging).Select(l => new HiggsfieldDecorDressing.Halo
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

        static (float time, float beat, float widest, float narrowest, float height) report_hover;

        /// <summary>
        /// Director #3: the flight clip time where the menu mosquito's wings spread widest across the menu camera (the
        /// projected span of its baked skinned meshes, the mosquito turned toward the camera as the living scene turns it).
        /// Returns the time, one wingbeat (s) and the widest / narrowest spans and the height at that time (m, at scale).
        /// </summary>
        static (float time, float beat, float widest, float narrowest, float height) HoverPhase(GameObject prefab, float scale, Quaternion camera)
        {
            const long FlightFileId = -1491716763371280848; // AlfaApplication.MenuMosquitoFlight (Mosquito_Fly)
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/LetMeSleep/Content/Characters/Models/LMS_Mosquito_alpha.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id) && id == FlightFileId);
            if (!clip) return (-1f, 0f, 0f, 0f, 0f);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.localScale *= scale;
                // Facing the camera: the body's forward points back at the lens.
                instance.transform.rotation = Quaternion.LookRotation(-(camera * Vector3.forward), Vector3.up);
                var animator = instance.GetComponentInChildren<Animator>(true);
                var target = animator ? animator.gameObject : instance;
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Vector3 right = camera * Vector3.right, up = camera * Vector3.up;
                float best = -1f, bestTime = -1f, narrowest = float.MaxValue, bestHeight = 0f;
                var baked = new Mesh();
                const int samples = 64;
                for (int k = 0; k < samples; k++)
                {
                    float t = clip.length * k / samples;
                    clip.SampleAnimation(target, t);
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    foreach (var skin in skins)
                    {
                        if (!skin.sharedMesh) continue;
                        skin.BakeMesh(baked, true);
                        var matrix = skin.transform.localToWorldMatrix;
                        foreach (var v in baked.vertices)
                        {
                            Vector3 w = matrix.MultiplyPoint3x4(v);
                            float x = Vector3.Dot(w, right), y = Vector3.Dot(w, up);
                            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                        }
                    }
                    float span = maxX - minX;
                    narrowest = Mathf.Min(narrowest, span);
                    if (span > best) { best = span; bestTime = t; bestHeight = maxY - minY; }
                }
                Object.DestroyImmediate(baked);
                // Mosquito_Fly: three wingbeats per loop (author_mosquito_motion.FLIGHT_WINGBEATS_PER_LOOP).
                return (bestTime, clip.length / 3f, best, narrowest, bestHeight);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>MainMenuLivingScene.Path: quadratic B-spline through the midpoints of consecutive control points.</summary>
        static Vector3 PathPoint(Transform[] points, float phase)
        {
            int count = points.Length;
            float step = phase * count; int index = Mathf.FloorToInt(step) % count; float t = step - Mathf.Floor(step);
            Vector3 center = points[index].position;
            Vector3 start = (points[(index + count - 1) % count].position + center) * .5f, end = (center + points[(index + 1) % count].position) * .5f;
            return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * center + t * t * end;
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

        static Light TunedLight(Transform parent, LightTune spec)
        {
            Need(spec != null && !string.IsNullOrEmpty(spec.name) && spec.position != null && spec.position.Length == 3, "Tuned light needs a name and a position");
            Need(ColorUtility.TryParseHtmlString(spec.color, out var color), "Bad light colour " + spec.color);
            var light = PointLight(parent, spec.name, new Vector3(spec.position[0], spec.position[1], spec.position[2]), color, spec.intensity, spec.range);
            light.renderingLayerMask = -1;
            if (spec.type == "Spot")
            {
                Need(spec.target != null && spec.target.Length == 3, "Spot light needs a target: " + spec.name);
                light.type = LightType.Spot; light.spotAngle = spec.spotAngle; light.innerSpotAngle = Mathf.Min(spec.innerSpotAngle, spec.spotAngle);
                light.transform.rotation = Quaternion.LookRotation(new Vector3(spec.target[0], spec.target[1], spec.target[2]) - light.transform.position, Vector3.up);
            }
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
