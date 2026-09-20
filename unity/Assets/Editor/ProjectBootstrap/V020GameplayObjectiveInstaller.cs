using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Content.Environment;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class V020GameplayObjectiveInstaller
{
    private sealed class Recipe
    {
        internal string MapId, PrefabPath, ObjectiveId, DisplayKey, TargetName, Region;
        internal Vector3 Point;
    }

    private sealed class CatalogSpec
    {
        internal string ObjectiveId, DisplayKey, ActionKey, TargetName, Region;
        internal GameplayObjectiveKind Kind;
        internal Vector3? ContactPoint, ApproachPoint;
    }

    private sealed class AuthoredApproach
    {
        internal Vector3 Point, Approach;
        internal Collider Support;
    }

#pragma warning disable 0649 // External authoring manifest, read by JsonUtility.
    [Serializable] private sealed class ExternalManifest { public ExternalMap[] maps; }
    [Serializable] private sealed class ExternalMap
    {
        public string mapId, prefabPath;
        public string navigationPath, navigationAssetPath;
        public ExternalObjective[] objectives;
    }
    [Serializable] private sealed class ExternalObjective
    {
        public string objectiveId, kind, displayKey, actionKey, targetName, routeRegionId;
        public float[] contactPoint, approachPoint;
    }
#pragma warning restore 0649

    private sealed class NavigationOverride
    {
        internal TextAsset Data;
        internal string AssetPath;
    }
    private static readonly Dictionary<string, NavigationOverride> NavigationOverrides =
        new Dictionary<string, NavigationOverride>(StringComparer.Ordinal);

    private static void ClearNavigationOverrides()
    {
        EditorApplication.delayCall -= ClearNavigationOverrides;
        foreach (var candidate in NavigationOverrides.Values)
            if (candidate.Data) Object.DestroyImmediate(candidate.Data);
        NavigationOverrides.Clear();
    }

    private static TextAsset NavigationFor(EnvironmentMapDefinition map)
        => NavigationOverrides.TryGetValue(map.MapId, out var candidate) ? candidate.Data : map.SpatialData;

    private static string AbsoluteAssetPath(string assetPath)
        => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", assetPath));

    private static void RegisterNavigationOverride(ExternalMap candidate)
    {
        if (string.IsNullOrEmpty(candidate.navigationPath) && string.IsNullOrEmpty(candidate.navigationAssetPath)) return;
        string expectedAsset = "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/" + candidate.mapId +
                               "/Data/isla-navigation-human-v020.json";
        // The first separate graph is deliberately scoped to Isla. Other maps need their own reviewed recipe.
        if (candidate.mapId != "hf-isla-del-laguito-v2" || candidate.navigationAssetPath != expectedAsset ||
            string.IsNullOrWhiteSpace(candidate.navigationPath) || !System.IO.Path.IsPathRooted(candidate.navigationPath))
            throw new ArgumentException("Separate human navigation requires the reviewed Isla source and asset paths.");
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.prefabPath)?.GetComponent<EnvironmentMapDefinition>();
        if (!map || !map.SpatialData) throw new InvalidOperationException("Original navigation missing.");
        string text = File.ReadAllText(candidate.navigationPath);
        JObject proposed = JObject.Parse(text), original = JObject.Parse(map.SpatialData.text);
        string[] humanFields = { "human_zones", "human_portals", "human_routes" };
        if (humanFields.Any(field => proposed[field] == null || proposed[field].Type != JTokenType.Array))
            throw new ArgumentException("All separate human navigation arrays are required.");
        foreach (string field in humanFields) { proposed.Remove(field); original.Remove(field); }
        if (!JToken.DeepEquals(original, proposed))
            throw new ArgumentException("Separate human navigation must preserve all legacy navigation data.");
        if (File.Exists(AbsoluteAssetPath(expectedAsset)) && File.ReadAllText(AbsoluteAssetPath(expectedAsset)) != text)
            throw new InvalidOperationException("Refusing to overwrite a different installed navigation version.");
        if (NavigationOverrides.TryGetValue(candidate.mapId, out var cached))
        {
            if (cached.Data.text != text) throw new InvalidOperationException("Navigation source changed during validation.");
            return;
        }
        NavigationOverrides.Add(candidate.mapId, new NavigationOverride
        { Data = new TextAsset(text) { name = candidate.mapId + " external human navigation", hideFlags = HideFlags.HideAndDontSave }, AssetPath = expectedAsset });
        EditorApplication.delayCall -= ClearNavigationOverrides;
        EditorApplication.delayCall += ClearNavigationOverrides;
        Debug.Log("LMS_OBJECTIVE_NAVIGATION map=" + candidate.mapId + " sha256=" + Hash(text) + " saved=0 legacyUnchanged=1");
    }

    // Explicit batch input; never saves a prefab or treats source metadata as proof.
    public static void ProbeExternalGeometryOnly()
    {
        var manifest = ReadExternalManifest(Environment.GetCommandLineArgs());
        var failures = new List<string>();
        foreach (var candidate in manifest.maps)
        {
            try
            {
                var specs = ExternalSpecs(candidate, false);
                BuildAndValidateCatalog(candidate.mapId, candidate.prefabPath, specs, false, true, true);
                Debug.Log("LMS_OBJECTIVE_GEOMETRY_PROBE map=" + candidate.mapId + " candidates=" +
                          specs.Length + " saved=0 status=PASS scope=contact-support-clearance-region-los-authored-budget-only");
            }
            catch (Exception error)
            {
                failures.Add(candidate.mapId + ": " + error.GetBaseException().Message);
                Debug.LogError("LMS_OBJECTIVE_GEOMETRY_PROBE map=" + candidate.mapId +
                               " saved=0 status=FAIL error=" + error.GetBaseException().Message);
            }
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(" | ", failures));
    }

    public static void ValidateExternalCatalogsOnly()
    {
        string[] args = Environment.GetCommandLineArgs();
        var manifest = ReadExternalManifest(args);
        bool motor = !args.Contains("-objectiveStaticOnly");
        var failures = new List<string>();
        foreach (ExternalMap candidate in manifest.maps)
        {
            try
            {
                CatalogSpec[] specs = ExternalSpecs(candidate);
                BuildAndValidateCatalog(candidate.mapId, candidate.prefabPath, specs, motor, true);
                Debug.Log("LMS_OBJECTIVE_CATALOG map=" + candidate.mapId +
                          " objectives=" + specs.Length + " distinctTargets=" + specs.Length +
                          " saved=0 status=PASS scope=" + (motor ? "motor" : "static-only"));
            }
            catch (Exception error)
            {
                failures.Add(candidate.mapId + ": " + error.GetBaseException().Message);
                Debug.LogError("LMS_OBJECTIVE_CATALOG map=" + candidate.mapId + " saved=0 status=FAIL error=" +
                               error.GetBaseException().Message);
            }
        }
        if (failures.Count > 0)
            throw new InvalidOperationException("External catalog validation failed: " + string.Join(" | ", failures));
    }

    public static void DiagnoseExternalRouteOnly()
    {
        string[] args = Environment.GetCommandLineArgs();
        var candidate = ReadExternalManifest(args).maps.Single();
        var specs = ExternalSpecs(candidate);
        if (new[] { "-objectiveSpawn", "-objectiveSourceObjective", "-objectiveStart" }.Count(args.Contains) != 1)
            throw new ArgumentException("Specify one spawn, source objective or recorded start position.");
        string target = CommandArgument(args, "-objectiveTarget");
        if (!specs.Any(item => item.ObjectiveId == target))
            throw new ArgumentException("Requested diagnostic target is not in the manifest.");
        var entries = BuildAndValidateCatalog(candidate.mapId, candidate.prefabPath, specs, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.prefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(item => item).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(item => item);
        if (!mosquito) throw new InvalidOperationException("Diagnostic requires an authored mosquito spawn.");
        Float3 source;
        string sourceId;
        if (args.Contains("-objectiveSpawn"))
        {
            if (!int.TryParse(CommandArgument(args, "-objectiveSpawn"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int spawn) || spawn < 0 || spawn >= humans.Length)
                throw new ArgumentException("Existing nonnegative -objectiveSpawn index required.");
            source = humans[spawn].position.ToFloat(); sourceId = "external:spawn:" + spawn;
        }
        else if (args.Contains("-objectiveSourceObjective"))
        {
            string id = CommandArgument(args, "-objectiveSourceObjective");
            var entry = entries.SingleOrDefault(item => item.ObjectiveId == id);
            if (entry == null || id == target) throw new ArgumentException("Distinct catalog source objective required.");
            source = prefab.transform.TransformPoint(entry.LocalApproachPoint).ToFloat();
            sourceId = "external:objective:" + id;
        }
        else
        {
            string[] coordinates = CommandArgument(args, "-objectiveStart").Split(',');
            var values = new float[3];
            if (coordinates.Length != 3) throw new ArgumentException("Start position requires x,y,z.");
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(coordinates[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                    !MathEx.Finite(values[i]) || Math.Abs(values[i]) > 10000)
                    throw new ArgumentException("Start position must be finite and bounded.");
            source = new Float3(values[0], values[1], values[2]); sourceId = "external:recorded-position";
        }
        ProveHumanRoute(entries, source, sourceId, target,
            mosquito.position.ToFloat(), 9800, trace: true, decisionTrace: true,
            mapId: candidate.mapId, prefabPath: candidate.prefabPath);
        Debug.Log("LMS_OBJECTIVE_ROUTE_DIAGNOSTIC map=" + candidate.mapId + " cases=1 saved=0");
    }

    private static string CommandArgument(string[] args, string flag)
    {
        int index = Array.IndexOf(args, flag);
        if (index < 0 || index + 1 >= args.Length || Array.LastIndexOf(args, flag) != index ||
            string.IsNullOrWhiteSpace(args[index + 1]))
            throw new ArgumentException("One " + flag + " argument required.");
        return args[index + 1];
    }

    private static ExternalManifest ReadExternalManifest(string[] args)
    {
        ClearNavigationOverrides();
        string path = CommandArgument(args, "-objectiveManifest");
        if (!System.IO.Path.IsPathRooted(path)) throw new ArgumentException("Absolute manifest path required.");
        var manifest = JsonUtility.FromJson<ExternalManifest>(File.ReadAllText(path));
        if (manifest?.maps == null || manifest.maps.Length == 0 || manifest.maps.Any(item => item == null) ||
            manifest.maps.Select(item => item.mapId).Distinct(StringComparer.Ordinal).Count() != manifest.maps.Length)
            throw new ArgumentException("Manifest requires distinct maps.");
        return manifest;
    }

    private static CatalogSpec[] ExternalSpecs(ExternalMap candidate, bool requireCatalogCount = true)
    {
        Recipe recipe = Recipes.SingleOrDefault(item => item.MapId == candidate.mapId);
        if (recipe == null || candidate.prefabPath != recipe.PrefabPath)
            throw new ArgumentException("Manifest must identify an existing final map prefab.");
        RegisterNavigationOverride(candidate);
        if (candidate.objectives == null || candidate.objectives.Length == 0 || candidate.objectives.Length > 128 ||
            requireCatalogCount && (candidate.objectives.Length < 10 ||
                candidate.objectives.Length > LetMeSleep.Online.GameplayWireCodec.MaxObjectives) || candidate.objectives.Any(item => item == null) ||
            candidate.objectives.Select(item => item.objectiveId).Distinct(StringComparer.Ordinal).Count() != candidate.objectives.Length ||
            candidate.objectives.Select(item => item.targetName).Distinct(StringComparer.Ordinal).Count() != candidate.objectives.Length)
            throw new ArgumentException(requireCatalogCount ? "Each catalog requires 10 to " +
                LetMeSleep.Online.GameplayWireCodec.MaxObjectives + " distinct objective IDs and targets." :
                "Geometry probe requires 1 to 128 distinct objective IDs and targets.");
        return candidate.objectives.Select(item =>
        {
            if (new[] { item.objectiveId, item.displayKey, item.actionKey, item.targetName, item.routeRegionId }
                .Any(string.IsNullOrWhiteSpace) || !Enum.TryParse(item.kind, false, out GameplayObjectiveKind kind) ||
                (kind != GameplayObjectiveKind.Clean && kind != GameplayObjectiveKind.Repair && kind != GameplayObjectiveKind.Switch))
                throw new ArgumentException("Invalid objective metadata: " + item.objectiveId);
            var spec = Spec(item.objectiveId, kind, item.displayKey, item.actionKey, item.targetName, item.routeRegionId);
            if (item.contactPoint != null || item.approachPoint != null)
            {
                if (item.contactPoint?.Length != 3 || item.approachPoint?.Length != 3 ||
                    item.contactPoint.Concat(item.approachPoint).Any(value => !MathEx.Finite(value) || Math.Abs(value) > 10000))
                    throw new ArgumentException("Explicit contact and approach require two finite local xyz points.");
                spec.ContactPoint = new Vector3(item.contactPoint[0], item.contactPoint[1], item.contactPoint[2]);
                spec.ApproachPoint = new Vector3(item.approachPoint[0], item.approachPoint[1], item.approachPoint[2]);
            }
            return spec;
        }).ToArray();
    }

    private static readonly Recipe[] Recipes =
    {
        RecipeFor("hf-isla-del-laguito-v2", "isla", "task.isla.cabin_access",
            "Path_Cabin_Access_COLLIDABLE", "air_20_6_-4", new Vector3(20.791523f, 2.69664073f, -4.86497545f)),
        RecipeFor("hf-casa-del-patio-v1", "casa", "task.casa.bathroom_tile",
            "CASA_Bathroom_Ground_TileFloor", "gf_bathroom", new Vector3(3.77500033f, .305999964f, 3.86166668f)),
        RecipeFor("hf-campamento-pinar-v2", "camp", "task.camp.washroom",
            "CAMP_Washroom_Floor", "washroom_interior", new Vector3(-28.14282f, .08000004f, 19.6380711f)),
        RecipeFor("hf-yate-a-la-deriva-v3", "yacht", "task.yacht.main_deck",
            "YATE_MainDeck_Continuous", "aft_center", new Vector3(-.825f, 3.40000081f, -9.133331f)),
        RecipeFor("hf-puerto-del-faro-v1", "port", "task.port.lighthouse_floor",
            "Lighthouse_GroundFloor", "lighthouse_entry", new Vector3(29.1690769f, 7.19999552f, 26.8500118f))
    };

    private const string CasaMapId = "hf-casa-del-patio-v1";
    private const string CasaPrefabPath =
        "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1/Prefabs/hf-casa-del-patio-v1.prefab";
    private static readonly CatalogSpec[] CasaSpecs =
    {
        Spec("casa.clean.ground_basin", GameplayObjectiveKind.Clean, "task.casa.ground_basin",
            "task.action.hold_clean", "CASA_Bathroom_Ground_Basin", "gf_bathroom"),
        Spec("casa.switch.ground_toilet", GameplayObjectiveKind.Switch, "task.casa.ground_toilet",
            "task.action.hold_switch", "CASA_Bathroom_Ground_WC_FlushButton", "gf_bathroom"),
        Spec("casa.clean.kitchen_sink", GameplayObjectiveKind.Clean, "task.casa.kitchen_sink",
            "task.action.hold_clean", "CASA_Kitchen_Sink", "gf_east_room"),
        Spec("casa.repair.oven", GameplayObjectiveKind.Repair, "task.casa.oven",
            "task.action.hold_repair", "CASA_Oven_Handle", "gf_east_room"),
        Spec("casa.repair.fridge", GameplayObjectiveKind.Repair, "task.casa.fridge",
            "task.action.hold_repair", "CASA_Fridge_Handle", "gf_east_room"),
        Spec("casa.clean.coffee_table", GameplayObjectiveKind.Clean, "task.casa.coffee_table",
            "task.action.hold_clean", "CASA_Living_CoffeeTable_Top_Plank_0", "gf_west_room"),
        Spec("casa.switch.bedroom_one_lamp", GameplayObjectiveKind.Switch, "task.casa.bedroom_one_lamp",
            "task.action.hold_switch", "CASA_Bedroom_One_BedsideLamp_Base", "uf_west_room"),
        Spec("casa.switch.bedroom_two_lamp", GameplayObjectiveKind.Switch, "task.casa.bedroom_two_lamp",
            "task.action.hold_switch", "CASA_Bedroom_Two_BedsideLamp_Base", "uf_east_room"),
        Spec("casa.clean.upper_basin", GameplayObjectiveKind.Clean, "task.casa.upper_basin",
            "task.action.hold_clean", "CASA_Bathroom_Upper_Basin", "uf_bathroom"),
        Spec("casa.switch.upper_toilet", GameplayObjectiveKind.Switch, "task.casa.upper_toilet",
            "task.action.hold_switch", "CASA_Bathroom_Upper_WC_FlushButton", "uf_bathroom")
    };

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Validate objective candidates (no save)")]
    public static void ValidateCandidatesOnly()
    {
        ClearNavigationOverrides();
        var failures = new List<string>();
        foreach (var recipe in Recipes)
        {
            try { ValidateCandidate(recipe); }
            catch (Exception error)
            {
                failures.Add(recipe.MapId + ": " + error.GetBaseException().Message);
                Debug.LogError($"LMS_OBJECTIVE_CANDIDATE map={recipe.MapId} route=FAIL saved=0 error={error.GetBaseException().Message}");
            }
        }
        if (failures.Count > 0)
            throw new InvalidOperationException("Objective candidate diagnostics failed: " + string.Join(" | ", failures));
        Debug.Log("LMS_OBJECTIVE_CANDIDATES validated=5 saved=0 scope=one-route-candidate-per-map-not-final-catalog");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Validate Casa objective catalog (no save)")]
    public static void ValidateCasaCatalogOnly()
    {
        BuildAndValidateCasaCatalog();
        Debug.Log("LMS_OBJECTIVE_CATALOG map=" + CasaMapId + " objectives=10 distinctTargets=10 saved=0 status=PASS");
    }

    public static void VerifyInstalledCasaCatalogOnly()
    {
        var entries = BuildAndValidateCasaCatalog(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        if (!prefab || !CatalogEquals(prefab.GetComponent<GameplayObjectiveCatalog>(), CasaMapId, entries))
            throw new InvalidOperationException("Authoring no longer reproduces the installed Casa catalog exactly.");
        Debug.Log("LMS_OBJECTIVE_INSTALLED_MATCH map=" + CasaMapId + " objectives=10 saved=0 status=PASS");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Verify two Casa human routes (no save)")]
    public static void VerifyCasaHumanRoutesOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 3 || !mosquito)
            throw new InvalidOperationException("Casa directed route diagnostic requires spawn 2 and a mosquito spawn.");
        bool control = ProveHumanRoute(entries, humans[2].position.ToFloat(), "spawn:2-control",
            "casa.clean.coffee_table", mosquito.position.ToFloat(), 9001, true);
        bool crossRoom = ProveHumanRoute(entries, humans[2].position.ToFloat(), "spawn:2-cross-room",
            "casa.clean.ground_basin", mosquito.position.ToFloat(), 9002, true);
        Debug.Log("LMS_OBJECTIVE_MOTOR_VERIFICATION control=" + (control ? "PASS" : "FAIL") +
                  " crossRoom=" + (crossRoom ? "PASS" : "FAIL") + " saved=0");
        if (!control || !crossRoom)
            throw new InvalidOperationException("Casa directed route verification requires both routes to pass.");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Diagnose Casa spawn threat priority (no save)")]
    public static void DiagnoseCasaSpawnThreatPriorityOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 5 || !mosquito)
            throw new InvalidOperationException("Casa threat diagnostic requires human spawns 0/4 and one mosquito spawn.");
        bool first = ProveHumanRoute(entries, humans[0].position.ToFloat(), "spawn:0-threat",
            "casa.clean.coffee_table", mosquito.position.ToFloat(), 9100, decisionTrace: true);
        bool last = ProveHumanRoute(entries, humans[4].position.ToFloat(), "spawn:4-threat",
            "casa.clean.coffee_table", mosquito.position.ToFloat(), 9104, decisionTrace: true);
        Debug.Log("LMS_OBJECTIVE_THREAT_DIAGNOSTIC spawn0=" + (first ? "PASS" : "FAIL") +
                  " spawn4=" + (last ? "PASS" : "FAIL") + " saved=0");
        if (first || last)
            throw new InvalidOperationException("Casa threat diagnostic no longer reproduces both catalog failures.");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Verify Casa boundary spawns (no save)")]
    public static void VerifyCasaBoundarySpawnsOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 5 || !mosquito)
            throw new InvalidOperationException("Casa boundary verification requires human spawns 0/4 and one mosquito spawn.");
        bool first = ProveHumanRoute(entries, humans[0].position.ToFloat(), "spawn:0-boundary",
            "casa.clean.coffee_table", mosquito.position.ToFloat(), 9200);
        bool last = ProveHumanRoute(entries, humans[4].position.ToFloat(), "spawn:4-boundary",
            "casa.clean.coffee_table", mosquito.position.ToFloat(), 9204);
        Debug.Log("LMS_OBJECTIVE_BOUNDARY_VERIFICATION spawn0=" + (first ? "PASS" : "FAIL") +
                  " spawn4=" + (last ? "PASS" : "FAIL") + " saved=0");
        if (!first || !last)
            throw new InvalidOperationException("Casa boundary spawn verification requires both routes to pass.");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Diagnose Casa spawn 3 coffee steering A-B (no save)")]
    public static void DiagnoseCasaSpawn3CoffeeSteeringOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 4 || !mosquito)
            throw new InvalidOperationException("Casa spawn 3 steering diagnostic requires spawn 3 and one mosquito spawn.");
        bool rawControl = ProveHumanRoute(entries, humans[3].position.ToFloat(),
            "spawn:3-coffee-raw", "casa.clean.coffee_table", mosquito.position.ToFloat(),
            9303, trace: true, decisionTrace: true, disableTraversalPrediction: true);
        bool predictor = ProveHumanRoute(entries, humans[3].position.ToFloat(),
            "spawn:3-coffee-predictor", "casa.clean.coffee_table", mosquito.position.ToFloat(),
            9403, trace: true, decisionTrace: true);
        Debug.Log("LMS_OBJECTIVE_SPAWN3_COFFEE_AB rawControl=" + (rawControl ? "PASS" : "FAIL") +
                  " predictor=" + (predictor ? "PASS" : "FAIL") + " saved=0");
        if (!rawControl)
            throw new InvalidOperationException("Casa spawn 3 A-B control did not reproduce the previously passing raw-clearance route.");
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Diagnose remaining Casa spawn routes (no save)")]
    public static void DiagnoseCasaRemainingSpawnRoutesOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 5 || !mosquito)
            throw new InvalidOperationException("Casa remaining-route diagnostic requires spawns 0/4 and one mosquito spawn.");
        bool frontBathroom = ProveHumanRoute(entries, humans[0].position.ToFloat(),
            "spawn:0-ground-basin-diagnostic", "casa.clean.ground_basin",
            mosquito.position.ToFloat(), 9500, trace: true, decisionTrace: true);
        bool rearKitchen = ProveHumanRoute(entries, humans[4].position.ToFloat(),
            "spawn:4-kitchen-sink-diagnostic", "casa.clean.kitchen_sink",
            mosquito.position.ToFloat(), 9504, trace: true, decisionTrace: true);
        Debug.Log("LMS_OBJECTIVE_REMAINING_ROUTE_DIAGNOSTIC spawn0GroundBasin=" +
                  (frontBathroom ? "PASS" : "FAIL") + " spawn4KitchenSink=" +
                  (rearKitchen ? "PASS" : "FAIL") + " saved=0");
    }

    public static void DiagnoseCasaUpperLampRoutesOnly()
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCasaCatalog(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasaPrefabPath);
        var map = prefab ? prefab.GetComponent<EnvironmentMapDefinition>() : null;
        Transform[] humans = (map?.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map?.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (humans.Length < 5 || !mosquito)
            throw new InvalidOperationException("Casa lamp diagnostic requires spawns 0/4 and a mosquito spawn.");
        var cases = new[]
        {
            new { Spawn = 0, Target = "casa.switch.bedroom_two_lamp" },
            new { Spawn = 4, Target = "casa.switch.bedroom_one_lamp" },
            new { Spawn = 4, Target = "casa.switch.bedroom_two_lamp" }
        };
        ulong run = 9600;
        bool sourceZeroOnly = Environment.GetCommandLineArgs().Contains("-objectiveSourceZeroOnly");
        foreach (var route in cases.Where(item => !sourceZeroOnly || item.Spawn == 0))
            ProveHumanRoute(entries, humans[route.Spawn].position.ToFloat(), "spawn:" + route.Spawn,
                route.Target, mosquito.position.ToFloat(), run++, trace: true, decisionTrace: true);
        Debug.Log("LMS_OBJECTIVE_LAMP_DIAGNOSTIC cases=" + (sourceZeroOnly ? 1 : 3) +
                  " saved=0 scope=observed-routes-not-catalog-approval");
    }

    public static void ValidateCasaTaskRoundOnly()
        => ValidateTaskRound(CasaMapId, CasaPrefabPath, CasaSpecs);

    public static void ValidateExternalTaskRoundOnly()
    {
        var candidate = ReadExternalManifest(Environment.GetCommandLineArgs()).maps.Single();
        ValidateTaskRound(candidate.mapId, candidate.prefabPath, ExternalSpecs(candidate));
    }

    private static void ValidateTaskRound(string mapId, string prefabPath, IReadOnlyList<CatalogSpec> specs)
    {
        GameplayObjectiveCatalog.Entry[] entries = BuildAndValidateCatalog(mapId, prefabPath, specs, false);
        var fixture = new GameObject(mapId + " task round validation");
        GameplayRuntime runtime = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            root.transform.SetParent(fixture.transform, false);
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var map = root.GetComponent<EnvironmentMapDefinition>();
            var catalog = root.GetComponent<GameplayObjectiveCatalog>() ?? root.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor(mapId, entries);
            var world = fixture.AddComponent<UnityGameplayWorld>();
            world.MapRoot = root.transform;
            var doors = world.GetDoorDefinitions();
            var tools = world.GetToolDefinitions();
            runtime = fixture.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            runtime.NavigationData = NavigationFor(map); runtime.LocalActorId = 1;
            Transform[] humans = map.HumanSpawnPoints.Where(item => item).ToArray();
            Transform mosquito = map.MosquitoSpawnPoints.FirstOrDefault(item => item);
            if (humans.Length != 5 || !mosquito)
                throw new InvalidOperationException("Task round proof requires exactly five humans and one mosquito spawn.");
            var roster = humans.Select((spawn, index) => new SpawnActor((uint)index + 1,
                "task-round-human-" + index, PlayerRole.Human, spawn.position.ToFloat(),
                "task-round-spawn-" + index, "default", true)).ToList();
            roster.Add(new SpawnActor((uint)humans.Length + 1, "task-round-static-mosquito", PlayerRole.Mosquito,
                mosquito.position.ToFloat()));
            var config = new GameplayRoundConfig(9700, 9700, map.MapId, map.ContentHash, 150, 0,
                doors: doors, tools: tools, modeId: GameModes.Tasks,
                modeRules: new ModeRuleProfile(GameModes.Tasks), objectives: world.GetObjectiveDefinitions());
            runtime.BeginRound(config, roster);
            var previous = new Dictionary<uint, string>();
            for (uint tick = 1; tick <= config.RoundDurationTicks && runtime.Authority.IsRunning; tick++)
            {
                runtime.TickHost();
                if (runtime.Authority.CurrentTick != tick)
                    throw new InvalidOperationException("Task round did not advance its real host tick.");
                foreach (var human in roster.Where(item => item.Role == PlayerRole.Human))
                {
                    TaskAssignment assignment = runtime.Authority.CapturePrivate(human.ActorId)?.TaskAssignment;
                    string state = assignment == null ? "none" : assignment.IssuedTick + ":" + assignment.ObjectiveId +
                        ":" + assignment.Status + ":" + (assignment.ProgressTicks > 0 ? "working" : "not-working");
                    if (previous.TryGetValue(human.ActorId, out string last) && last == state) continue;
                    previous[human.ActorId] = state;
                    var actor = runtime.LatestSnapshot?.Actors.FirstOrDefault(item => item.ActorId == human.ActorId);
                    if (actor == null) throw new InvalidOperationException("Task round actor snapshot missing.");
                    Debug.Log("LMS_TASK_ROUND actor=" + human.ActorId + " tick=" + tick + " state=" + state +
                              " progress=" + (assignment?.ProgressTicks ?? 0) + " deadline=" + (assignment?.DeadlineTick ?? 0) +
                              string.Format(CultureInfo.InvariantCulture, " position={0:R},{1:R},{2:R} grounded={3}",
                                  actor.Position.X, actor.Position.Y, actor.Position.Z, actor.Grounded ? 1 : 0));
                }
            }
            GameSessionState result = runtime.LatestSnapshot;
            if (result == null) throw new InvalidOperationException("Task round produced no final state.");
            Debug.Log("LMS_TASK_ROUND_RESULT map=" + map.MapId + " humans=" + humans.Length +
                      " completed=" + result.TasksCompleted + " goal=" + result.TasksGoal +
                      " opportunities=" + result.ViableTaskOpportunities + " tick=" + result.HostTick +
                      " result=" + result.Result + " winner=" + result.Winner +
                      " saved=0 scope=human-training-bots-uncontrolled-mosquito-no-network");
            if (runtime.Authority.IsRunning || result.TasksGoal != 14 || result.ViableTaskOpportunities != 20 ||
                result.TasksCompleted < result.TasksGoal || result.Result != RoundEndReason.TasksMet ||
                result.Winner != PlayerRole.Human || result.HostTick != config.RoundDurationTicks)
                throw new InvalidOperationException("Human task bots did not complete the configured round goal against a static mosquito.");
        }
        finally
        {
            if (runtime) runtime.StopRound();
            Object.DestroyImmediate(fixture);
            Physics.SyncTransforms();
        }
    }

    [MenuItem("Tools/Let Me Sleep/v0.2.0/Install validated Casa objective catalog")]
    public static void InstallCasaCatalog()
        => InstallCatalog(CasaMapId, CasaPrefabPath, BuildAndValidateCasaCatalog());

    public static void InstallExternalCatalog()
    {
        var candidate = ReadExternalManifest(Environment.GetCommandLineArgs()).maps.Single();
        var specs = ExternalSpecs(candidate);
        ValidateTaskRound(candidate.mapId, candidate.prefabPath, specs);
        var entries = BuildAndValidateCatalog(candidate.mapId, candidate.prefabPath, specs, true);
        InstallCatalog(candidate.mapId, candidate.prefabPath, entries);
    }

    private static void InstallCatalog(string mapId, string prefabPath, GameplayObjectiveCatalog.Entry[] entries)
    {
        string expectedContentHash = null;
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var map = root.GetComponent<EnvironmentMapDefinition>();
            if (!map || map.MapId != mapId || string.IsNullOrWhiteSpace(map.ContentHash))
                throw new InvalidOperationException(mapId + " prefab identity/content hash missing.");
            var catalog = root.GetComponent<GameplayObjectiveCatalog>() ?? root.AddComponent<GameplayObjectiveCatalog>();
            NavigationOverrides.TryGetValue(mapId, out var navigation);
            bool navigationMatches = navigation == null || map.SpatialData &&
                AssetDatabase.GetAssetPath(map.SpatialData) == navigation.AssetPath && map.SpatialData.text == navigation.Data.text;
            if (CatalogEquals(catalog, mapId, entries) && navigationMatches)
            {
                Debug.Log("LMS_OBJECTIVE_INSTALL map=" + mapId + " objectives=" + entries.Length + " changed=0");
                return;
            }
            string previousHash = map.ContentHash;
            catalog.ConfigureForEditor(mapId, entries);
            string catalogHash = ObjectiveDefinition.CatalogHash(Definitions(root.transform, entries));
            string navigationHash = navigation == null ? "" : Hash(navigation.Data.text);
            if (navigation != null)
            {
                string absoluteNavigationPath = AbsoluteAssetPath(navigation.AssetPath);
                if (!File.Exists(absoluteNavigationPath)) File.WriteAllText(absoluteNavigationPath, navigation.Data.text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(navigation.AssetPath, ImportAssetOptions.ForceSynchronousImport);
                var savedNavigation = AssetDatabase.LoadAssetAtPath<TextAsset>(navigation.AssetPath);
                if (!savedNavigation || savedNavigation.text != navigation.Data.text)
                    throw new InvalidOperationException("Navigation asset readback failed before prefab save.");
                map.SpatialData = savedNavigation;
            }
            map.ContentHash = Hash("objectives-v020\n" + previousHash + "\n" + catalogHash +
                (navigation == null ? "" : "\nhuman-navigation\n" + navigationHash));
            expectedContentHash = map.ContentHash;
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(map);
            if (!PrefabUtility.SaveAsPrefabAsset(root, prefabPath))
                throw new InvalidOperationException("Could not save " + mapId + " objective catalog.");
            Debug.Log("LMS_OBJECTIVE_INSTALL map=" + mapId + " objectives=" + entries.Length + " changed=1 previousHash=" +
                      previousHash + " contentHash=" + map.ContentHash + " catalogHash=" + catalogHash +
                      " navigationHash=" + navigationHash);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var savedMap = saved ? saved.GetComponent<EnvironmentMapDefinition>() : null;
        var savedCatalog = saved ? saved.GetComponent<GameplayObjectiveCatalog>() : null;
        if (!savedMap || !savedCatalog || !CatalogEquals(savedCatalog, mapId, entries) ||
            savedMap.ContentHash != expectedContentHash)
            throw new InvalidOperationException(mapId + " objective catalog readback failed.");
        if (NavigationOverrides.TryGetValue(mapId, out var expectedNavigation) &&
            (!savedMap.SpatialData || AssetDatabase.GetAssetPath(savedMap.SpatialData) != expectedNavigation.AssetPath ||
             savedMap.SpatialData.text != expectedNavigation.Data.text))
            throw new InvalidOperationException(mapId + " navigation reference readback failed.");
    }

    private static GameplayObjectiveCatalog.Entry[] BuildAndValidateCasaCatalog(bool validateMotor = true)
        => BuildAndValidateCatalog(CasaMapId, CasaPrefabPath, CasaSpecs, validateMotor);

    private static GameplayObjectiveCatalog.Entry[] BuildAndValidateCatalog(string mapId, string prefabPath,
        IReadOnlyList<CatalogSpec> specs, bool validateMotor, bool collectGeometryFailures = false, bool geometryOnly = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!prefab) throw new InvalidOperationException("Missing map prefab: " + prefabPath);
        var fixture = new GameObject(mapId + " objective catalog validation");
        GameplayRuntime runtime = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.transform.SetParent(fixture.transform, false);
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var map = root.GetComponent<EnvironmentMapDefinition>();
            if (!map || map.MapId != mapId || string.IsNullOrWhiteSpace(map.ContentHash) || !map.SpatialData)
                throw new InvalidOperationException(mapId + " identity/navigation missing.");
            var world = fixture.AddComponent<UnityGameplayWorld>();
            world.MapRoot = root.transform;
            var doorDefinitions = world.GetDoorDefinitions();
            var toolDefinitions = world.GetToolDefinitions();
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            object navigation = typeof(UnityGameplayWorld).GetMethod("ConfigureModeMap", hidden)
                ?.Invoke(world, new object[] { NavigationFor(map), mapId, false });
            MethodInfo approachFree = typeof(UnityGameplayWorld).GetMethod("ApproachFree", hidden);
            MethodInfo routeWithin = navigation?.GetType().GetMethod("RouteWithin");
            if (navigation == null || approachFree == null || routeWithin == null)
                throw new InvalidOperationException(mapId + " authoring probe contract missing.");
            Physics.SyncTransforms();
            var authored = new List<GameplayObjectiveCatalog.Entry>();
            var geometryFailures = new List<string>();
            foreach (CatalogSpec spec in specs)
            {
                try { authored.Add(FindCatalogEntry(spec, root.transform, world,
                    approachFree, navigation, routeWithin, map.HumanSpawnPoints)); }
                catch (Exception error) when (collectGeometryFailures)
                {
                    geometryFailures.Add(spec.ObjectiveId + ": " + error.GetBaseException().Message);
                    Debug.LogError("LMS_OBJECTIVE_AUTHORED map=" + mapId + " objective=" + spec.ObjectiveId +
                                   " status=FAIL saved=0 error=" + error.GetBaseException().Message);
                }
            }
            if (geometryFailures.Count > 0)
                throw new InvalidOperationException(string.Join(" | ", geometryFailures));
            var entries = authored.ToArray();
            if (geometryOnly) return entries.Select(Copy).ToArray();
            if (entries.Length < 10 || entries.Length > LetMeSleep.Online.GameplayWireCodec.MaxObjectives ||
                entries.Select(entry => entry.TargetPath).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException(mapId + " requires at least ten distinct authored objective targets within the wire limit.");
            var catalog = root.GetComponent<GameplayObjectiveCatalog>() ?? root.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor(mapId, entries);

            runtime = fixture.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            runtime.NavigationData = NavigationFor(map);
            var objectives = world.GetObjectiveDefinitions();
            var roster = Roster(map);
            var config = new GameplayRoundConfig(1, 1, map.MapId, map.ContentHash, 180, 0,
                doors: doorDefinitions, tools: toolDefinitions,
                modeId: GameModes.Tasks, modeRules: new ModeRuleProfile(GameModes.Tasks), objectives: objectives);
            try { runtime.BeginRound(config, roster); }
            catch (ArgumentException)
            {
                LogValidationBreakdown(world, objectives, map.HumanSpawnPoints);
                throw;
            }
            LogCatalogCoverage(navigation, routeWithin, objectives, map.HumanSpawnPoints);
            runtime.StopRound();
            if (validateMotor) ValidateHumanRoutes(entries, map, objectives, prefabPath);
            return entries.Select(Copy).ToArray();
        }
        finally
        {
            if (runtime) runtime.StopRound();
            Object.DestroyImmediate(fixture);
            Physics.SyncTransforms();
        }
    }

    private static GameplayObjectiveCatalog.Entry FindCatalogEntry(CatalogSpec spec, Transform root,
        UnityGameplayWorld world, MethodInfo approachFree, object navigation, MethodInfo routeWithin,
        IReadOnlyList<Transform> spawns)
    {
        Transform target = Unique(root, spec.TargetName);
        Collider[] solids = target.GetComponents<Collider>().Where(c => c && !c.isTrigger).ToArray();
        if (solids.Length != 1)
            throw new InvalidOperationException(spec.ObjectiveId + " must own exactly one solid collider.");
        Collider collider = solids[0];
        MethodInfo containsFootPoint = navigation.GetType().GetMethod("ContainsFootPoint");
        if (containsFootPoint == null)
            throw new InvalidOperationException("Objective navigation foot-region contract missing.");
        var candidates = new List<AuthoredApproach>();
        if (spec.ContactPoint.HasValue)
            candidates.Add(ValidateExplicitApproach(spec, root, collider, world, approachFree,
                navigation, containsFootPoint, routeWithin, spawns));
        int probes = 0, supported = 0, near = 0, clear = 0, inRegion = 0, visible = 0, routed = 0;
        var regionWitnesses = new Dictionary<string, int>(StringComparer.Ordinal);
        var authoredRegions = navigation.GetType().GetField("regions", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(navigation) as IReadOnlyList<BotRegion>;
        Vector3? firstClearFoot = null;
        foreach (IEnumerable<Vector3> witnesses in new[] { AxisWitnesses(collider), HorizontalEdgeWitnesses(collider) })
        {
            if (spec.ContactPoint.HasValue) break;
            foreach (Vector3 point in witnesses)
            foreach (float radius in new[] { .55f, .75f, 1f })
            foreach (Vector3 direction in HorizontalDirections())
            {
                probes++;
                Vector3 probe = point + direction * radius + Vector3.up * .5f;
                RaycastHit floor = Physics.RaycastAll(probe, Vector3.down, 2.5f, world.GeometryMask,
                        QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance)
                    .FirstOrDefault(hit => world.IsWorldCollider(hit.collider) &&
                                           !hit.collider.GetComponentInParent<GameplayActorProxy>());
                // Standing on the target's own tabletop is not a ground approach to interact with it.
                if (!floor.collider || floor.collider == collider || floor.normal.y < .55f) continue;
                supported++;
                Vector3 approach = floor.point;
                if (Vector3.Distance(point, approach) > 1.25f) continue;
                near++;
                if (!(bool)approachFree.Invoke(world, new object[] { 0u, approach })) continue;
                clear++;
                if (!firstClearFoot.HasValue) firstClearFoot = approach;
                if (authoredRegions != null)
                {
                    Float3 sample = root.InverseTransformPoint(approach).ToFloat() + Float3.Up;
                    foreach (BotRegion region in authoredRegions.Where(item => item.Contains(sample)))
                        regionWitnesses[region.Id] = regionWitnesses.TryGetValue(region.Id, out int count) ? count + 1 : 1;
                }
                if (!(bool)containsFootPoint.Invoke(navigation, new object[] { spec.Region, approach.ToFloat() })) continue;
                inRegion++;
                Vector3 eye = approach + Vector3.up * 1.53f;
                Vector3 aim = point - eye;
                if (aim.sqrMagnitude < .0001f) continue;
                RaycastHit first = Physics.RaycastAll(eye, aim.normalized, Mathf.Min(4.6f, aim.magnitude + .3f),
                        world.GeometryMask, QueryTriggerInteraction.Collide)
                    .OrderBy(hit => hit.distance)
                    .FirstOrDefault(hit => world.IsWorldCollider(hit.collider) && !hit.collider.isTrigger);
                if (!first.collider || first.collider != collider || Vector3.Distance(first.point, point) > .35f) continue;
                visible++;
                bool hasSpawnRoute = (spawns ?? Array.Empty<Transform>()).Any(spawn => spawn &&
                    (bool)routeWithin.Invoke(navigation, new object[]
                        { spawn.position.ToFloat(), spec.Region, approach.ToFloat(), 330u }));
                if (!hasSpawnRoute) continue;
                routed++;
                candidates.Add(new AuthoredApproach { Point = point, Approach = approach, Support = floor.collider });
            }
            // Preserve established central witnesses when valid. Edge sampling only fills a search gap.
            if (candidates.Count > 0) break;
        }
        var selected = candidates.OrderBy(candidate => candidate.Approach.y)
            .ThenBy(candidate => Vector3.Distance(candidate.Point, candidate.Approach))
            .ThenBy(candidate => candidate.Approach.x).ThenBy(candidate => candidate.Approach.z)
            .FirstOrDefault();
        if (selected != null)
        {
            var entry = new GameplayObjectiveCatalog.Entry
            {
                ObjectiveId = spec.ObjectiveId, Kind = spec.Kind, DisplayKey = spec.DisplayKey,
                ActionKey = spec.ActionKey, LocalPosition = root.InverseTransformPoint(selected.Point),
                LocalApproachPoint = root.InverseTransformPoint(selected.Approach), UseRadius = 1.25f,
                WorkTicks = 90, RouteRegionId = spec.Region, RouteBudgetTicks = 330,
                TargetPath = Path(root, target)
            };
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "LMS_OBJECTIVE_AUTHORED map={0} objective={1} target={2} region={3} " +
                "point={4:R},{5:R},{6:R} approach={7:R},{8:R},{9:R} support={10} saved=0",
                root.GetComponent<EnvironmentMapDefinition>().MapId, entry.ObjectiveId, spec.TargetName, entry.RouteRegionId,
                entry.LocalPosition.x, entry.LocalPosition.y, entry.LocalPosition.z,
                entry.LocalApproachPoint.x, entry.LocalApproachPoint.y, entry.LocalApproachPoint.z,
                HierarchyPath(root, selected.Support.transform)));
            return entry;
        }
        throw new InvalidOperationException(spec.ObjectiveId +
            " has no exact contact/support/LOS/routed approach under current values; probes=" + probes +
            " supported=" + supported + " near=" + near + " clear=" + clear +
            " inRegion=" + inRegion + " visible=" + visible + " routed=" + routed +
            " firstClearFoot=" + (firstClearFoot.HasValue ? firstClearFoot.Value.ToString("R") : "none") +
            " otherRegions=" + string.Join(",", regionWitnesses.OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + ":" + item.Value)) + ".");
    }

    private static AuthoredApproach ValidateExplicitApproach(CatalogSpec spec, Transform root, Collider target,
        UnityGameplayWorld world, MethodInfo approachFree, object navigation, MethodInfo containsFootPoint,
        MethodInfo routeWithin, IReadOnlyList<Transform> spawns)
    {
        Vector3 point = root.TransformPoint(spec.ContactPoint.Value);
        Vector3 requestedFoot = root.TransformPoint(spec.ApproachPoint.Value);
        RaycastHit support = Physics.RaycastAll(requestedFoot + Vector3.up * .1f, Vector3.down, .2f,
                world.GeometryMask, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance)
            .FirstOrDefault(hit => world.IsWorldCollider(hit.collider) && !hit.collider.GetComponentInParent<GameplayActorProxy>());
        if (!support.collider || support.collider == target || support.normal.y < .55f ||
            Vector3.Distance(support.point, requestedFoot) > .01f)
            throw new InvalidOperationException(spec.ObjectiveId + " explicit approach lacks matching independent walkable support.");
        Vector3 foot = support.point;
        if (Vector3.Distance(point, foot) > 1.25f || !(bool)approachFree.Invoke(world, new object[] { 0u, foot }) ||
            !(bool)containsFootPoint.Invoke(navigation, new object[] { spec.Region, foot.ToFloat() }))
            throw new InvalidOperationException(spec.ObjectiveId + " explicit approach fails range, clearance or authored region.");
        Vector3 eye = foot + Vector3.up * 1.53f, aim = point - eye;
        if (aim.sqrMagnitude < .0001f || !target.Raycast(new Ray(eye, aim.normalized), out RaycastHit contact, aim.magnitude + .02f) ||
            Vector3.Distance(contact.point, point) > .02f)
            throw new InvalidOperationException(spec.ObjectiveId + " explicit contact is not on the target surface.");
        RaycastHit first = Physics.RaycastAll(eye, aim.normalized, Mathf.Min(4.6f, aim.magnitude + .3f),
                world.GeometryMask, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance)
            .FirstOrDefault(hit => world.IsWorldCollider(hit.collider) && !hit.collider.isTrigger);
        if (first.collider != target || Vector3.Distance(first.point, point) > .35f)
            throw new InvalidOperationException(spec.ObjectiveId + " explicit contact is occluded.");
        if (!(spawns ?? Array.Empty<Transform>()).Any(spawn => spawn && (bool)routeWithin.Invoke(navigation,
                new object[] { spawn.position.ToFloat(), spec.Region, foot.ToFloat(), 330u })))
            throw new InvalidOperationException(spec.ObjectiveId + " explicit approach has no spawn route within budget.");
        return new AuthoredApproach { Point = point, Approach = foot, Support = support.collider };
    }

    private static Recipe RecipeFor(string mapId, string shortId, string displayKey,
        string targetName, string region, Vector3 point) => new Recipe
    {
        MapId = mapId,
        PrefabPath = $"Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/{mapId}/Prefabs/{mapId}.prefab",
        ObjectiveId = shortId + ".clean.01",
        DisplayKey = displayKey,
        TargetName = targetName,
        Region = region,
        Point = point
    };

    private static void ValidateCandidate(Recipe recipe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.PrefabPath);
        if (!prefab) throw new InvalidOperationException("Missing map prefab: " + recipe.PrefabPath);
        var fixture = new GameObject("Objective candidate validation " + recipe.MapId);
        GameplayRuntime runtime = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.transform.SetParent(fixture.transform, false);
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var map = root.GetComponent<EnvironmentMapDefinition>();
            if (!map || map.MapId != recipe.MapId || string.IsNullOrWhiteSpace(map.ContentHash) || !map.SpatialData)
                throw new InvalidOperationException("Map identity/navigation missing: " + recipe.MapId);
            Transform target = Unique(root.transform, recipe.TargetName);
            var solids = target.GetComponents<Collider>().Where(c => c && !c.isTrigger).ToArray();
            if (solids.Length != 1) throw new InvalidOperationException("Candidate target must own one solid collider: " + recipe.TargetName);
            Physics.SyncTransforms();
            if (!RayWitness(solids[0], recipe.Point))
                throw new InvalidOperationException("Recorded native contact has no exact ray witness: " + recipe.TargetName);

            Vector3 localPoint = root.transform.InverseTransformPoint(recipe.Point);
            var entry = new GameplayObjectiveCatalog.Entry
            {
                ObjectiveId = recipe.ObjectiveId,
                Kind = GameplayObjectiveKind.Clean,
                DisplayKey = recipe.DisplayKey,
                ActionKey = "task.action.hold_clean",
                LocalPosition = localPoint,
                LocalApproachPoint = localPoint + Vector3.up * .002f,
                UseRadius = 1.25f,
                WorkTicks = 90,
                RouteRegionId = recipe.Region,
                RouteBudgetTicks = 330,
                TargetPath = Path(root.transform, target)
            };
            var catalog = root.GetComponent<GameplayObjectiveCatalog>() ?? root.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor(recipe.MapId, new[] { entry });

            var world = fixture.AddComponent<UnityGameplayWorld>();
            runtime = fixture.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            world.MapRoot = root.transform; runtime.NavigationData = map.SpatialData;
            var objectives = world.GetObjectiveDefinitions();
            var roster = Roster(map);
            var config = new GameplayRoundConfig(1, 1, map.MapId, map.ContentHash, 180, 0,
                doors: world.GetDoorDefinitions(), tools: world.GetToolDefinitions(),
                modeId: GameModes.Tasks, modeRules: new ModeRuleProfile(GameModes.Tasks), objectives: objectives);
            try { runtime.BeginRound(config, roster); }
            catch (ArgumentException)
            {
                LogValidationBreakdown(world, objectives, map.HumanSpawnPoints);
                throw;
            }
            Debug.Log($"LMS_OBJECTIVE_CANDIDATE map={recipe.MapId} objective={recipe.ObjectiveId} humans={roster.Count - 1} target={recipe.TargetName} route=PASS saved=0");
        }
        finally
        {
            if (runtime) runtime.StopRound();
            Object.DestroyImmediate(fixture);
            Physics.SyncTransforms();
        }
    }

    private static void LogValidationBreakdown(UnityGameplayWorld world,
        IReadOnlyList<ObjectiveDefinition> objectives, IReadOnlyList<Transform> spawns)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        object navigation = typeof(UnityGameplayWorld).GetField("modeNavigation", privateInstance)?.GetValue(world);
        MethodInfo approachFree = typeof(UnityGameplayWorld).GetMethod("ApproachFree", privateInstance);
        MethodInfo blocksMotor = typeof(UnityGameplayWorld).GetMethod("BlocksMotor", privateInstance);
        MethodInfo knowsRegion = navigation?.GetType().GetMethod("KnowsRegion");
        MethodInfo routeWithin = navigation?.GetType().GetMethod("RouteWithin");
        MethodInfo diagnoseRoute = navigation?.GetType().GetMethod("DiagnoseRoute", privateInstance);
        if (navigation == null || approachFree == null || blocksMotor == null || knowsRegion == null ||
            routeWithin == null || diagnoseRoute == null)
        {
            Debug.LogError("LMS_OBJECTIVE_DIAGNOSTIC setup=FAIL reason=reflection-contract-missing");
            return;
        }

        foreach (var objective in objectives)
        {
            bool support = (bool)approachFree.Invoke(world,
                new object[] { 0u, objective.ApproachPoint.ToUnity() });
            LogApproachBreakdown(world, blocksMotor, objective);
            bool region = (bool)knowsRegion.Invoke(navigation,
                new object[] { objective.RouteRegionId });
            bool allRoutes = true;
            int reachableSpawns = 0;
            int spawnIndex = 0;
            foreach (Transform spawn in spawns ?? Array.Empty<Transform>())
            {
                bool route = spawn && (bool)routeWithin.Invoke(navigation, new object[]
                {
                    spawn.position.ToFloat(), objective.RouteRegionId,
                    objective.ApproachPoint, objective.RouteBudgetTicks
                });
                allRoutes &= route;
                if (route) reachableSpawns++;
                string breakdown = spawn ? (string)diagnoseRoute.Invoke(navigation, new object[]
                {
                    spawn.position.ToFloat(), objective.RouteRegionId,
                    objective.ApproachPoint, objective.RouteBudgetTicks
                }) : "spawn=missing";
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "LMS_OBJECTIVE_ROUTE objective={0} spawn={1} position={2:R},{3:R},{4:R} region={5} budget={6} route={7} {8}",
                    objective.ObjectiveId, spawnIndex++, spawn ? spawn.position.x : float.NaN,
                    spawn ? spawn.position.y : float.NaN, spawn ? spawn.position.z : float.NaN,
                    objective.RouteRegionId, objective.RouteBudgetTicks, route ? "PASS" : "FAIL", breakdown));
            }
            var probe = new SpawnActor(1, "candidate-diagnostic", PlayerRole.Human,
                spawns != null && spawns.Count > 0 && spawns[0] ? spawns[0].position.ToFloat() : Float3.Zero);
            bool aggregate = world.ValidateObjective(probe, objective);
            int onwardChoices = 0;
            var onwardDiagnostics = new List<string>();
            foreach (var next in objectives.Where(item => item.ObjectiveId != objective.ObjectiveId))
            {
                bool onward = (bool)routeWithin.Invoke(navigation, new object[] {
                    objective.ApproachPoint, next.RouteRegionId, next.ApproachPoint, next.RouteBudgetTicks });
                if (onward) onwardChoices++;
                onwardDiagnostics.Add("LMS_OBJECTIVE_ONWARD source=" + objective.ObjectiveId + " target=" +
                    next.ObjectiveId + " route=" + (onward ? "PASS " : "FAIL ") +
                    (string)diagnoseRoute.Invoke(navigation, new object[] {
                        objective.ApproachPoint, next.RouteRegionId, next.ApproachPoint, next.RouteBudgetTicks }));
            }
            if (onwardChoices < Math.Min(2, Math.Max(0, objectives.Count - 1)))
                foreach (string diagnostic in onwardDiagnostics) Debug.Log(diagnostic);
            Debug.Log($"LMS_OBJECTIVE_DIAGNOSTIC objective={objective.ObjectiveId} witness=not-isolated approach={(support ? "PASS" : "FAIL")} region={(region ? "PASS" : "FAIL")} allSpawnRoutes={(allRoutes ? "PASS" : "FAIL")} reachableSpawns={reachableSpawns} onwardChoices={onwardChoices} aggregate={(aggregate ? "PASS" : "FAIL")}");
        }
    }

    private static void LogApproachBreakdown(UnityGameplayWorld world, MethodInfo blocksMotor,
        ObjectiveDefinition objective)
    {
        Vector3 point = objective.ApproachPoint.ToUnity();
        var support = Physics.RaycastAll(point + Vector3.up * .25f, Vector3.down, .55f,
                world.GeometryMask, QueryTriggerInteraction.Ignore)
            .OrderBy(hit => hit.distance)
            .FirstOrDefault(hit => world.IsWorldCollider(hit.collider) &&
                                   !hit.collider.GetComponentInParent<GameplayActorProxy>());
        bool supportOk = support.collider && support.normal.y >= .55f &&
                         Mathf.Abs(support.point.y - point.y) <= .08f;
        var blockers = Physics.OverlapCapsule(point + Vector3.up * .251f,
                point + Vector3.up * 1.469f, .249f, world.GeometryMask,
                QueryTriggerInteraction.Collide)
            .Where(collider => (bool)blocksMotor.Invoke(world, new object[] { collider, 0u }))
            .OrderBy(collider => HierarchyPath(world.MapRoot, collider.transform), StringComparer.Ordinal)
            .ToArray();
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "LMS_OBJECTIVE_APPROACH objective={0} point={1:R},{2:R},{3:R} support={4} supportPath={5} " +
            "supportPoint={6:R},{7:R},{8:R} normalY={9:R} verticalDelta={10:R} blockers={11}",
            objective.ObjectiveId, point.x, point.y, point.z, supportOk ? "PASS" : "FAIL",
            support.collider ? HierarchyPath(world.MapRoot, support.collider.transform) : "none",
            support.collider ? support.point.x : float.NaN, support.collider ? support.point.y : float.NaN,
            support.collider ? support.point.z : float.NaN, support.collider ? support.normal.y : float.NaN,
            support.collider ? Mathf.Abs(support.point.y - point.y) : float.NaN, blockers.Length));
        foreach (Collider blocker in blockers)
        {
            Bounds bounds = blocker.bounds;
            var actor = blocker.GetComponentInParent<GameplayActorProxy>();
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "LMS_OBJECTIVE_APPROACH_BLOCKER objective={0} path={1} type={2} trigger={3} actor={4} " +
                "center={5:R},{6:R},{7:R} size={8:R},{9:R},{10:R}",
                objective.ObjectiveId, HierarchyPath(world.MapRoot, blocker.transform),
                blocker.GetType().Name, blocker.isTrigger ? 1 : 0, actor ? actor.ActorId.ToString(CultureInfo.InvariantCulture) : "none",
                bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z));
        }
    }

    private static void LogCatalogCoverage(object navigation, MethodInfo routeWithin,
        IReadOnlyList<ObjectiveDefinition> objectives, IReadOnlyList<Transform> spawns)
    {
        int spawnIndex = 0;
        foreach (Transform spawn in spawns ?? Array.Empty<Transform>())
        {
            var reachable = spawn ? objectives.Where(objective => (bool)routeWithin.Invoke(navigation,
                new object[] { spawn.position.ToFloat(), objective.RouteRegionId,
                    objective.ApproachPoint, objective.RouteBudgetTicks })).Select(objective => objective.ObjectiveId).ToArray()
                : Array.Empty<string>();
            Debug.Log("LMS_OBJECTIVE_COVERAGE source=spawn:" + spawnIndex++ + " count=" + reachable.Length +
                      " required=2 destinations=" + string.Join(",", reachable));
        }
        foreach (ObjectiveDefinition source in objectives)
        {
            var reachable = objectives.Where(target => target.ObjectiveId != source.ObjectiveId &&
                (bool)routeWithin.Invoke(navigation, new object[] { source.ApproachPoint,
                    target.RouteRegionId, target.ApproachPoint, target.RouteBudgetTicks }))
                .Select(target => target.ObjectiveId).ToArray();
            Debug.Log("LMS_OBJECTIVE_COVERAGE source=objective:" + source.ObjectiveId + " count=" +
                      reachable.Length + " required=2 destinations=" + string.Join(",", reachable));
        }
    }

    private static void ValidateHumanRoutes(IReadOnlyList<GameplayObjectiveCatalog.Entry> entries,
        EnvironmentMapDefinition map, IReadOnlyList<ObjectiveDefinition> objectives, string prefabPath)
    {
        Transform[] spawns = (map.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        Transform mosquito = (map.MosquitoSpawnPoints ?? Array.Empty<Transform>()).FirstOrDefault(spawn => spawn);
        if (spawns.Length == 0 || !mosquito)
            throw new InvalidOperationException(map.MapId + " motor proof requires authored human and mosquito spawns.");
        var spawnPasses = new bool[spawns.Length, objectives.Count];
        ulong run = 10;
        for (int spawn = 0; spawn < spawns.Length; spawn++)
        for (int target = 0; target < objectives.Count; target++)
            spawnPasses[spawn, target] = ProveHumanRoute(entries, spawns[spawn].position.ToFloat(),
                "spawn:" + spawn, objectives[target].ObjectiveId, mosquito.position.ToFloat(), run++,
                mapId: map.MapId, prefabPath: prefabPath);

        var failures = new List<string>();
        for (int spawn = 0; spawn < spawns.Length; spawn++)
        {
            int count = Enumerable.Range(0, objectives.Count).Count(target => spawnPasses[spawn, target]);
            Debug.Log("LMS_OBJECTIVE_MOTOR_COVERAGE source=spawn:" + spawn + " count=" + count + " required=2");
            if (count < Math.Min(2, objectives.Count)) failures.Add("spawn:" + spawn + " has " + count + " motor routes");
        }
        for (int target = 0; target < objectives.Count; target++)
        {
            int count = Enumerable.Range(0, spawns.Length).Count(spawn => spawnPasses[spawn, target]);
            Debug.Log("LMS_OBJECTIVE_MOTOR_COVERAGE target=" + objectives[target].ObjectiveId +
                      " spawns=" + count + " required=1");
            if (count == 0) failures.Add(objectives[target].ObjectiveId + " has no motor route from a spawn");
        }

        for (int source = 0; source < objectives.Count; source++)
        {
            int onward = 0;
            foreach (ObjectiveDefinition target in objectives.Where((_, index) => index != source))
            {
                if (ProveHumanRoute(entries, objectives[source].ApproachPoint,
                        "objective:" + objectives[source].ObjectiveId, target.ObjectiveId,
                        mosquito.position.ToFloat(), run++, mapId: map.MapId, prefabPath: prefabPath))
                    onward++;
                if (onward >= Math.Min(2, objectives.Count - 1)) break;
            }
            Debug.Log("LMS_OBJECTIVE_MOTOR_COVERAGE source=objective:" + objectives[source].ObjectiveId +
                      " count=" + onward + " required=2");
            if (onward < Math.Min(2, objectives.Count - 1))
                failures.Add(objectives[source].ObjectiveId + " has " + onward + " onward motor routes");
        }
        if (failures.Count > 0)
            throw new InvalidOperationException(map.MapId + " human motor route proof failed: " + string.Join(" | ", failures));
        Debug.Log("LMS_OBJECTIVE_MOTOR_CATALOG map=" + map.MapId + " objectives=" + objectives.Count +
                  " spawns=" + spawns.Length + " routeBudget=330 status=PASS");
    }

    private static bool ProveHumanRoute(IReadOnlyList<GameplayObjectiveCatalog.Entry> entries,
        Float3 source, string sourceId, string targetId, Float3 mosquitoSpawn, ulong run,
        bool trace = false, bool decisionTrace = false,
        bool disableTraversalPrediction = false, string mapId = CasaMapId, string prefabPath = CasaPrefabPath)
    {
        var fixture = new GameObject(mapId + " independent human route " + run);
        GameplayRuntime runtime = null;
        float bestHorizontal = float.PositiveInfinity, bestVertical = float.PositiveInfinity;
        uint reachedTick = 0;
        uint simulatedTicks = 0;
        bool targetAssigned = false;
        uint maximumWorkProgress = 0;
        TaskAssignmentStatus? lastTargetStatus = null;
        string reason = "budget";
        LifeState finalState = LifeState.Active;
        bool finalGrounded = false;
        uint budget = 330;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var root = prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : null;
            if (!root) throw new InvalidOperationException("Missing " + mapId + " prefab for independent route proof.");
            root.transform.SetParent(fixture.transform, false);
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var map = root.GetComponent<EnvironmentMapDefinition>();
            if (!map || map.MapId != mapId || !map.SpatialData)
                throw new InvalidOperationException(mapId + " route fixture identity/navigation missing.");
            var catalog = root.GetComponent<GameplayObjectiveCatalog>() ?? root.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor(mapId, entries.Select(Copy).ToArray());
            var world = fixture.AddComponent<UnityGameplayWorld>();
            world.MapRoot = root.transform;
#if UNITY_EDITOR
            world.DisableBotHumanTraversalPredictionForDiagnostic = disableTraversalPrediction;
#endif
            var doors = world.GetDoorDefinitions();
            var tools = world.GetToolDefinitions();
            runtime = fixture.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            runtime.NavigationData = NavigationFor(map); runtime.LocalActorId = 1;
            ObjectiveDefinition target = world.GetObjectiveDefinitions().Single(item => item.ObjectiveId == targetId);
            budget = target.RouteBudgetTicks;
            var config = new GameplayRoundConfig(run, run, map.MapId, map.ContentHash, 180, 0,
                doors: doors, tools: tools, modeId: GameModes.Tasks,
                modeRules: new ModeRuleProfile(GameModes.Tasks), objectives: new[] { target });
            var roster = new[]
            {
                new SpawnActor(1, "catalog-route-bot", PlayerRole.Human, source,
                    "catalog-route-source", "default", true),
                new SpawnActor(2, "catalog-route-mosquito", PlayerRole.Mosquito, mosquitoSpawn)
            };
            runtime.BeginRound(config, roster);
            if (decisionTrace) EnableDecisionTrace(runtime);
            for (uint tick = 1; tick <= target.RouteBudgetTicks; tick++)
            {
                DecisionTraceSample decision = decisionTrace && (tick + 1) % 3 == 0
                    ? CaptureDecisionTrace(runtime, tick)
                    : default;
                if (decisionTrace && decision.HasValue) ClearDecisionTrace(runtime);
                runtime.TickHost();
                simulatedTicks = runtime.Authority.CurrentTick;
                if (decisionTrace && simulatedTicks != tick)
                    throw new InvalidOperationException("Casa threat diagnostic did not advance the expected host tick.");
                if (decisionTrace && decision.HasValue)
                    LogDecisionTrace(runtime, decision, sourceId);
                ActorSnapshot actor = runtime.LatestSnapshot?.Actors.FirstOrDefault(item => item.ActorId == 1);
                if (actor == null) { reason = "missing-actor"; break; }
                TaskAssignment assignment = runtime.Authority.CapturePrivate(1)?.TaskAssignment;
                targetAssigned |= assignment?.ObjectiveId == targetId && assignment.Status == TaskAssignmentStatus.Active;
                bool workAdvanced = assignment?.ObjectiveId == targetId && assignment.ProgressTicks > maximumWorkProgress;
                if (assignment?.ObjectiveId == targetId)
                {
                    maximumWorkProgress = Math.Max(maximumWorkProgress, assignment.ProgressTicks);
                    lastTargetStatus = assignment.Status;
                }
                if (trace && (tick == 1 || tick % 30 == 0))
                    LogHumanRouteTrace(runtime, actor, target, sourceId, tick);
                finalState = actor.LifeState; finalGrounded = actor.Grounded;
                Vector3 delta = actor.Position.ToUnity() - target.ApproachPoint.ToUnity();
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                float vertical = Mathf.Abs(delta.y);
                bestHorizontal = Mathf.Min(bestHorizontal, horizontal);
                bestVertical = Mathf.Min(bestVertical, vertical);
                if (actor.LifeState == LifeState.Falling || actor.LifeState == LifeState.Fainted ||
                    actor.LifeState == LifeState.Recovering || actor.LifeState == LifeState.Eliminated)
                { reason = "unsafe-state:" + actor.LifeState; break; }
                // The authored approach is one verified access point, not the only legal
                // side of an object. Actual new authority-accepted work proves that the
                // grounded actor reached a usable contact with valid range and LOS.
                // Retained/decaying progress alone is never a fresh arrival witness.
                if (targetAssigned && actor.Grounded &&
                    (horizontal <= .70f && vertical <= .08f || workAdvanced))
                {
                    reachedTick = tick;
                    reason = workAdvanced ? "reached-supported-interaction" : "reached-supported-approach";
                    break;
                }
                if (!runtime.Authority.IsRunning)
                { reason = "round-ended-before-approach"; break; }
            }
        }
        catch (Exception error)
        {
            // Infrastructure failures cannot be hidden by the catalog's minimum route coverage.
            throw new InvalidOperationException(mapId + " route fixture is invalid for " + sourceId +
                                                " -> " + targetId + ".", error);
        }
        finally
        {
            if (runtime) runtime.StopRound();
            Object.DestroyImmediate(fixture);
            Physics.SyncTransforms();
        }
        bool pass = reachedTick > 0;
        if (decisionTrace && !pass && (reason != "budget" || simulatedTicks != budget))
            throw new InvalidOperationException("Casa threat diagnostic did not produce an observed full-budget route failure for " +
                                                sourceId + ": ticks=" + simulatedTicks + " reason=" + reason);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "LMS_OBJECTIVE_MOTOR source={0} target={1} result={2} reachedTick={3} budget={4} " +
            "bestHorizontal={5:R} bestVertical={6:R} finalState={7} grounded={8} reason={9} assigned={10} maxWorkProgress={11} targetStatus={12}",
            sourceId, targetId, pass ? "PASS" : "FAIL", reachedTick, budget,
            bestHorizontal, bestVertical, finalState, finalGrounded ? 1 : 0, reason, targetAssigned ? 1 : 0,
            maximumWorkProgress, lastTargetStatus.HasValue ? lastTargetStatus.Value.ToString() : "none"));
        return pass;
    }

    private readonly struct DecisionTraceSample
    {
        internal readonly uint Tick;
        internal readonly ActorSnapshot Self, Opponent;
        internal readonly TaskAssignment Assignment;
        internal readonly bool Visible, Threat;
        internal bool HasValue => Self != null && Opponent != null;
        internal DecisionTraceSample(uint tick, ActorSnapshot self, ActorSnapshot opponent,
            TaskAssignment assignment, bool visible, bool threat)
        { Tick = tick; Self = self; Opponent = opponent; Assignment = assignment; Visible = visible; Threat = threat; }
    }

    private static void EnableDecisionTrace(GameplayRuntime runtime)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var bots = typeof(GameplayRuntime).GetField("bots", hidden)?.GetValue(runtime)
            as IDictionary<uint, BotController>;
        if (bots == null || !bots.TryGetValue(1, out var controller))
            throw new InvalidOperationException("Casa threat diagnostic could not enable the human bot trace.");
        FieldInfo decisionCapture = typeof(BotController).GetField("captureDecisionDiagnostic", hidden);
        object navigation = typeof(GameplayRuntime).GetField("botNavigation", hidden)?.GetValue(runtime);
        FieldInfo directionCapture = navigation?.GetType().GetField("captureDirectionDiagnostic", hidden);
        FieldInfo steeringCapture = typeof(UnityGameplayWorld).GetField("captureBotSteeringDiagnostic", hidden);
        if (decisionCapture == null || directionCapture == null || steeringCapture == null)
            throw new InvalidOperationException("Casa threat diagnostic editor probes are unavailable.");
        decisionCapture.SetValue(controller, true);
        directionCapture.SetValue(navigation, true);
        steeringCapture.SetValue(runtime.World, true);
    }

    private static DecisionTraceSample CaptureDecisionTrace(GameplayRuntime runtime, uint tick)
    {
        GameSessionState state = runtime.Authority.CaptureSnapshot();
        ActorSnapshot self = state.Actors.FirstOrDefault(item => item.ActorId == 1);
        ActorSnapshot opponent = state.Actors.FirstOrDefault(item => item.Role == PlayerRole.Mosquito);
        if (self == null || opponent == null)
            throw new InvalidOperationException("Casa threat diagnostic is missing its human or mosquito at tick " + tick + ".");
        Float3 origin = self.Position + Float3.Up * (1.53f - .64f * self.CrouchFraction);
        bool visible = (opponent.Position - self.Position).Length <= 12 &&
                       runtime.World.HasLineOfSight(self.ActorId, origin, opponent.ActorId, opponent.Position);
        var target = new BotTarget(opponent, opponent.Position);
        bool threat = visible && IsDiagnosticThreat(self, target);
        TaskAssignment assignment = runtime.Authority.CapturePrivate(self.ActorId)?.TaskAssignment;
        return new DecisionTraceSample(tick, self, opponent, assignment, visible, threat);
    }

    private static void ClearDecisionTrace(GameplayRuntime runtime)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var bots = typeof(GameplayRuntime).GetField("bots", hidden)?.GetValue(runtime)
            as IDictionary<uint, BotController>;
        if (bots == null || !bots.TryGetValue(1, out var controller))
            throw new InvalidOperationException("Route diagnostic human bot missing.");
        typeof(BotController).GetField("lastDecisionDiagnostic", hidden)?.SetValue(controller, null);
        object navigation = typeof(GameplayRuntime).GetField("botNavigation", hidden)?.GetValue(runtime);
        navigation?.GetType().GetField("lastDirectionDiagnostic", hidden)?.SetValue(navigation, null);
        typeof(UnityGameplayWorld).GetField("lastBotSteeringDiagnostic", hidden)?.SetValue(runtime.World, null);
    }

    private static void LogDecisionTrace(GameplayRuntime runtime, DecisionTraceSample sample, string sourceId)
    {
        bool wanted = sourceId == "spawn:0-threat" && (sample.Tick == 14 || sample.Tick == 17) ||
                      sourceId == "spawn:4-threat" &&
                      (sample.Tick == 317 || sample.Tick == 320 || sample.Tick == 323 || sample.Tick == 326) ||
                      sourceId == "spawn:3-coffee-predictor" ||
                      sourceId == "spawn:3-coffee-raw" && (sample.Tick == 2 || (sample.Tick - 2) % 15 == 0) ||
                      (sourceId == "spawn:0-ground-basin-diagnostic" ||
                       sourceId == "spawn:4-kitchen-sink-diagnostic") &&
                      (sample.Tick == 2 || (sample.Tick - 2) % 15 == 0) ||
                      sourceId == "spawn:0" && sample.Assignment?.ObjectiveId == "casa.switch.bedroom_two_lamp" &&
                      (sample.Tick >= 62 && sample.Tick <= 95 || sample.Tick >= 305 && sample.Tick <= 330) ||
                      sourceId.StartsWith("external:", StringComparison.Ordinal) &&
                      sample.Assignment?.Status == TaskAssignmentStatus.Active;
        if (!wanted) return;
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var bots = typeof(GameplayRuntime).GetField("bots", hidden)?.GetValue(runtime)
            as IDictionary<uint, BotController>;
        if (bots == null || !bots.TryGetValue(sample.Self.ActorId, out var controller))
            throw new InvalidOperationException("Casa threat diagnostic could not inspect the human bot controller.");
        string decision = typeof(BotController).GetField("lastDecisionDiagnostic", hidden)?.GetValue(controller) as string;
        object navigation = typeof(GameplayRuntime).GetField("botNavigation", hidden)?.GetValue(runtime);
        string route = navigation?.GetType().GetField("lastDirectionDiagnostic", hidden)?.GetValue(navigation) as string;
        string steering = typeof(UnityGameplayWorld).GetField("lastBotSteeringDiagnostic", hidden)?
            .GetValue(runtime.World) as string;
        if (string.IsNullOrEmpty(decision))
            throw new InvalidOperationException("Route diagnostic did not capture the current decision.");
        bool taskCalled = decision.StartsWith("task=1 ", StringComparison.Ordinal);
        bool steerCalled = decision.Contains(" steerCalled=1 ");
        if (taskCalled && string.IsNullOrEmpty(route) || steerCalled && string.IsNullOrEmpty(steering))
            throw new InvalidOperationException("Route diagnostic did not capture a called navigation or steering method.");
        if (!taskCalled) route = "not-called";
        if (!steerCalled) steering = "not-called";
        uint selected = controller.SelectedActorId;
        bool blocksTask = sample.Visible && sample.Threat && selected == sample.Opponent.ActorId;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "LMS_OBJECTIVE_BOT_DECISION source={0} tick={1} assignment={2} status={3} progress={22} " +
            "opponent={4} distance={5:R} visible={6} threat={7} selected={8} blocksTask={9} " +
            "position={10:R},{11:R},{12:R} velocity={13:R},{14:R},{15:R} view={16:R},{17:R},{18:R} " +
            "decision=({19}) route=({20}) steering=({21})",
            sourceId, sample.Tick, sample.Assignment?.ObjectiveId ?? "none",
            sample.Assignment == null ? "none" : sample.Assignment.Status.ToString(),
            sample.Opponent.ActorId, (sample.Opponent.Position - sample.Self.Position).Length,
            sample.Visible ? 1 : 0, sample.Threat ? 1 : 0, selected, blocksTask ? 1 : 0,
            sample.Self.Position.X, sample.Self.Position.Y, sample.Self.Position.Z,
            sample.Self.Velocity.X, sample.Self.Velocity.Y, sample.Self.Velocity.Z,
            sample.Self.ViewForward.X, sample.Self.ViewForward.Y, sample.Self.ViewForward.Z,
            decision, route, steering, sample.Assignment?.ProgressTicks ?? 0));
    }

    private static bool IsDiagnosticThreat(ActorSnapshot self, BotTarget target)
    {
        Float3 toward = self.Position - target.Actor.Position;
        return toward.Length < 2 || Float3.Dot(target.Actor.Velocity, toward.Normalized) > .1f ||
               (target.Actor.BiteAttachment.HasValue && target.Actor.BiteAttachment.Value.VictimId == self.ActorId);
    }

    private static void LogHumanRouteTrace(GameplayRuntime runtime, ActorSnapshot actor,
        ObjectiveDefinition target, string sourceId, uint tick)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        object navigation = typeof(UnityGameplayWorld).GetField("modeNavigation", hidden)?.GetValue(runtime.World);
        MethodInfo diagnose = navigation?.GetType().GetMethod("DiagnoseRoute", hidden);
        string route = diagnose == null ? "route-diagnostic-unavailable" : (string)diagnose.Invoke(navigation,
            new object[] { actor.Position, target.RouteRegionId, target.ApproachPoint, target.RouteBudgetTicks });
        Vector3 direction = actor.ViewForward.ToUnity(); direction.y = 0;
        string blocker = "none"; float blockerDistance = float.NaN;
        if (direction.sqrMagnitude > .0001f)
        {
            direction.Normalize();
            RaycastHit hit = Physics.CapsuleCastAll(actor.Position.ToUnity() + Vector3.up * .29f,
                    actor.Position.ToUnity() + Vector3.up * (1.46f - .64f * actor.CrouchFraction),
                    .26f, direction, .65f, runtime.World.GeometryMask, QueryTriggerInteraction.Ignore)
                .Where(item => runtime.World.IsWorldCollider(item.collider) &&
                               !item.collider.GetComponentInParent<GameplayActorProxy>())
                .OrderBy(item => item.distance).FirstOrDefault();
            if (hit.collider)
            {
                blocker = HierarchyPath(runtime.World.MapRoot, hit.collider.transform);
                blockerDistance = hit.distance;
            }
        }
        TaskAssignment assignment = runtime.LocalPrivate?.TaskAssignment;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "LMS_OBJECTIVE_MOTOR_TRACE source={0} target={1} tick={2} position={3:R},{4:R},{5:R} " +
            "velocity={6:R},{7:R},{8:R} view={9:R},{10:R},{11:R} grounded={12} state={13} " +
            "taskStatus={14} taskProgress={15} ahead={16} aheadDistance={17:R} route=({18})",
            sourceId, target.ObjectiveId, tick, actor.Position.X, actor.Position.Y, actor.Position.Z,
            actor.Velocity.X, actor.Velocity.Y, actor.Velocity.Z, actor.ViewForward.X,
            actor.ViewForward.Y, actor.ViewForward.Z, actor.Grounded ? 1 : 0, actor.LifeState,
            assignment == null ? "none" : assignment.Status.ToString(),
            assignment == null ? 0 : assignment.ProgressTicks, blocker, blockerDistance, route));
    }

    private static IEnumerable<Vector3> AxisWitnesses(Collider collider)
    {
        float reach = collider.bounds.extents.magnitude + .25f;
        foreach (Vector3 axis in new[]
                 { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            if (collider.Raycast(new Ray(collider.bounds.center + axis * reach, -axis), out RaycastHit hit,
                    reach * 2) && Vector3.Distance(hit.point, collider.bounds.center) <= reach)
                yield return hit.point;
    }

    private static IEnumerable<Vector3> HorizontalEdgeWitnesses(Collider collider)
    {
        Bounds bounds = collider.bounds;
        var seen = new HashSet<Vector3>();
        foreach (float height in new[] { .1f, .5f, .9f, .98f })
        foreach (float lateral in new[] { -.75f, 0f, .75f })
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Vector3 center = bounds.center;
            center.y = Mathf.Lerp(bounds.min.y, bounds.max.y, height);
            Vector3 tangent = Vector3.Cross(Vector3.up, direction);
            center += Vector3.Scale(tangent, bounds.extents) * lateral;
            float distance = bounds.extents.magnitude + 1f;
            if (collider.Raycast(new Ray(center + direction * distance, -direction), out var hit, distance * 2f) &&
                seen.Add(hit.point)) yield return hit.point;
        }
    }

    private static IEnumerable<Vector3> HorizontalDirections()
    {
        for (int i = 0; i < 16; i++)
        {
            float angle = Mathf.PI * 2 * i / 16f;
            yield return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        }
    }

    private static CatalogSpec Spec(string id, GameplayObjectiveKind kind, string display, string action,
        string target, string region) => new CatalogSpec
    {
        ObjectiveId = id, Kind = kind, DisplayKey = display, ActionKey = action,
        TargetName = target, Region = region
    };

    private static GameplayObjectiveCatalog.Entry Copy(GameplayObjectiveCatalog.Entry source) =>
        new GameplayObjectiveCatalog.Entry
        {
            ObjectiveId = source.ObjectiveId, Kind = source.Kind, DisplayKey = source.DisplayKey,
            ActionKey = source.ActionKey, LocalPosition = source.LocalPosition,
            LocalApproachPoint = source.LocalApproachPoint, UseRadius = source.UseRadius,
            WorkTicks = source.WorkTicks, RouteRegionId = source.RouteRegionId,
            RouteBudgetTicks = source.RouteBudgetTicks, TargetPath = source.TargetPath
        };

    private static IReadOnlyList<ObjectiveDefinition> Definitions(Transform root,
        IReadOnlyList<GameplayObjectiveCatalog.Entry> entries) => entries.Select(entry =>
        new ObjectiveDefinition(entry.ObjectiveId, (ObjectiveKind)entry.Kind, entry.DisplayKey,
            entry.ActionKey, root.TransformPoint(entry.LocalPosition).ToFloat(),
            root.TransformPoint(entry.LocalApproachPoint).ToFloat(), entry.UseRadius,
            entry.WorkTicks, entry.RouteRegionId, entry.RouteBudgetTicks)).ToArray();

    private static bool CatalogEquals(GameplayObjectiveCatalog catalog, string mapId,
        IReadOnlyList<GameplayObjectiveCatalog.Entry> expected)
    {
        if (!catalog || catalog.MapId != mapId || catalog.Entries.Count != expected.Count) return false;
        try { catalog.ValidateAuthoring(mapId); }
        catch (InvalidOperationException) { return false; }
        return catalog.Entries.Zip(expected, (actual, wanted) => actual.ObjectiveId == wanted.ObjectiveId &&
            actual.Kind == wanted.Kind && actual.DisplayKey == wanted.DisplayKey &&
            actual.ActionKey == wanted.ActionKey && actual.LocalPosition == wanted.LocalPosition &&
            actual.LocalApproachPoint == wanted.LocalApproachPoint && actual.UseRadius == wanted.UseRadius &&
            actual.WorkTicks == wanted.WorkTicks && actual.RouteRegionId == wanted.RouteRegionId &&
            actual.RouteBudgetTicks == wanted.RouteBudgetTicks && actual.TargetPath == wanted.TargetPath).All(equal => equal);
    }

    private static string Hash(string value)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }

    private static string HierarchyPath(Transform root, Transform target)
    {
        if (!target) return "missing";
        var parts = new Stack<string>();
        for (Transform current = target; current; current = current.parent)
        {
            parts.Push(current.name);
            if (current == root) return string.Join("/", parts);
        }
        return "outside-map/" + string.Join("/", parts);
    }

    private static IReadOnlyList<SpawnActor> Roster(EnvironmentMapDefinition map)
    {
        var humans = (map.HumanSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        var mosquitoes = (map.MosquitoSpawnPoints ?? Array.Empty<Transform>()).Where(spawn => spawn).ToArray();
        if (humans.Length == 0 || humans.Length > 5 || mosquitoes.Length == 0)
            throw new InvalidOperationException("Candidate validation requires 1-5 human spawns and one mosquito spawn: " + map.MapId);
        var roster = new List<SpawnActor>();
        for (int i = 0; i < humans.Length; i++)
            roster.Add(new SpawnActor((uint)(i + 1), "candidate-human-" + i, PlayerRole.Human, humans[i].position.ToFloat()));
        roster.Add(new SpawnActor((uint)(humans.Length + 1), "candidate-mosquito", PlayerRole.Mosquito, mosquitoes[0].position.ToFloat()));
        return roster;
    }

    private static bool RayWitness(Collider collider, Vector3 point)
    {
        if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        foreach (var direction in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            if (collider.Raycast(new Ray(point + direction * .06f, -direction), out var hit, .12f) &&
                Vector3.Distance(hit.point, point) <= .012f) return true;
        return false;
    }

    private static Transform Unique(Transform root, string name)
    {
        var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"Expected one target {name}, found {matches.Length}.");
        return matches[0];
    }

    private static string Path(Transform root, Transform target)
    {
        var parts = new Stack<string>();
        for (var current = target; current && current != root; current = current.parent) parts.Push(current.name);
        if (parts.Count == 0) throw new InvalidOperationException("Objective target cannot be the map root.");
        return string.Join("/", parts);
    }
}
