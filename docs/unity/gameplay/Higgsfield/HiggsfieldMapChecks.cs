using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// External editor diagnostic assembly. Never install under Assets or attach to a scene.
public static class HiggsfieldMapChecks
{
#pragma warning disable 0649
    [Serializable] public sealed class Config
    {
        public string mapId, prefabPath;
        public int humanPool = 5, mosquitoPool = 16;
        public Route[] routes;
    }
    [Serializable] public sealed class Route
    {
        public string id, role;
        public int spawnIndex, maxTicks = 600;
        public bool crouch, sprint;
        public Vector3[] points;
    }
    [Serializable] public sealed class Plan { public int schema_version; public string map_id; public Zone[] zones; public Portal[] portals; public Stair stair; }
    [Serializable] public sealed class Zone { public string id; public float[] min, max; }
    [Serializable] public sealed class Portal { public string id, from, to; public float[] center, normal; public float width, height; public bool door; }
    [Serializable] public sealed class Stair { public string id, from, to; public Flight lower_flight, upper_flight; public Zone mid_landing; }
    [Serializable] public sealed class Flight { public float[] clear_x; public float start_y, end_y, start_z, end_z; }
#pragma warning restore 0649
    public sealed class Report
    {
        public string status = "INCOMPLETE", utc, unityVersion, mapId, contentHash, configSha256, prefabDependencyHash;
        public string scope = "Native EditMode physics queries + integrated authority at 30 Hz. No Physics.Simulate, player, rendering, WAN, performance or full-map coverage certification.";
        public List<string> errors = new List<string>(), pending = new List<string>(), strippedBehaviours = new List<string>(), loadedAssemblies = new List<string>();
        public List<Case> cases = new List<Case>();
        public List<Passage> passages = new List<Passage>();
        public int humanPool, mosquitoPool, nativeColliderCount;
        public double seconds;
        public bool cleanup;
    }
    public sealed class Case
    {
        public string id, role, status = "INCOMPLETE", reason;
        public int ticks, reached, requested;
        public float initialPenetration, finalPenetration, maxPenetration;
        public string initialBlocker, finalBlocker;
        public List<Sample> samples = new List<Sample>();
    }
    public sealed class Sample { public uint tick; public Vector3 localPosition; public bool grounded; public float crouch; }
    public sealed class Passage { public string id, status, blocker; public Vector3[] localPoints; }

    public static string Run(string configPath, string outputDirectory)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run only in an idle Editor, outside Play Mode, in the coordinator's native slot.");
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, "map-checks-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        var watch = Stopwatch.StartNew(); GameObject owner = null;
        try
        {
            report.configSha256 = Hash(File.ReadAllBytes(configPath));
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(configPath));
            Require(config != null && !string.IsNullOrWhiteSpace(config.mapId), "Missing config mapId.");
            Require(config.humanPool >= 5 && config.humanPool <= 32 && config.mosquitoPool >= 16 && config.mosquitoPool <= 32, "Pool contract must cover at least 5 human / 16 mosquito spawns; max 32 per pool.");
            Require(config.routes == null || config.routes.Length <= 32, "At most 32 routes per bounded run.");
            Require(!string.IsNullOrEmpty(config.prefabPath) && config.prefabPath.StartsWith("Assets/", StringComparison.Ordinal), "prefabPath must be an AssetDatabase path.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);
            Require(prefab, "Prefab not found: " + config.prefabPath);
            report.prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(config.prefabPath).ToString();
            foreach (var type in new[] { typeof(HiggsfieldMapChecks), typeof(GameplayAuthority), typeof(UnityGameplayWorld), typeof(EnvironmentMapDefinition), typeof(PlayerRole) })
                report.loadedAssemblies.Add(type.Assembly.GetName().Name + " | MVID=" + type.Module.ModuleVersionId + " | " + type.Assembly.Location);
            owner = new GameObject("HiggsfieldMapChecks_TEMP") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var clone = Object.Instantiate(prefab, owner.transform, false);
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour) { report.errors.Add("Missing script on cloned prefab."); continue; }
                if (behaviour is EnvironmentMapDefinition || behaviour is GameplaySurface || behaviour is GameplayDoor || behaviour is GameplayToolPickup) continue;
                report.strippedBehaviours.Add(behaviour.GetType().FullName);
                Object.DestroyImmediate(behaviour);
            }
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var a in clone.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            foreach (var a in clone.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            Require(clone.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Dynamic/rigidbody map requires a separate fixture; this tool validates static geometry.");
            Require(clone.transform.localScale == Vector3.one && Quaternion.Angle(clone.transform.localRotation, Quaternion.identity) < .001f && clone.transform.localPosition.sqrMagnitude < .000001f, "Prefab root must have identity transform for this contract.");
            var definitions = clone.GetComponentsInChildren<EnvironmentMapDefinition>(true);
            Require(definitions.Length == 1 && definitions[0].transform == clone.transform, "Exactly one map definition must be on prefab root.");
            var map = definitions[0]; report.mapId = map.MapId; report.contentHash = map.ContentHash;
            Require(map.MapId == config.mapId, "Config and map IDs differ.");
            Require(!string.IsNullOrWhiteSpace(map.ContentHash), "Missing map content hash.");
            Require(Finite(map.PlayBounds.min) && Finite(map.PlayBounds.max) && map.PlayBounds.size.x > 0 && map.PlayBounds.size.y > 0 && map.PlayBounds.size.z > 0, "Invalid PlayBounds.");
            var world = owner.AddComponent<UnityGameplayWorld>(); world.MapRoot = clone.transform;
            clone.SetActive(true); owner.SetActive(true); Physics.SyncTransforms();
            world.RegisterGeometry();
            report.nativeColliderCount = clone.GetComponentsInChildren<Collider>(false).Count(c => c.enabled && !c.isTrigger);
            Require(report.nativeColliderCount > 0, "No active physical map colliders.");
            report.humanPool = map.HumanSpawnPoints?.Length ?? 0; report.mosquitoPool = map.MosquitoSpawnPoints?.Length ?? 0;
            if (report.humanPool < config.humanPool || report.mosquitoPool < config.mosquitoPool) report.errors.Add("Spawn pools below contract: " + report.humanPool + " human / " + report.mosquitoPool + " mosquito.");
            Require(report.humanPool <= 32 && report.mosquitoPool <= 32, "More than 32 spawns in a pool exceeds this bounded fixture.");
            CheckNavigation(map, world, report, watch);
            CheckSpawns(map.HumanSpawnPoints, PlayerRole.Human, map, world, report, watch);
            CheckSpawns(map.MosquitoSpawnPoints, PlayerRole.Mosquito, map, world, report, watch);
            var routes = config.routes ?? Array.Empty<Route>(); var ids = new HashSet<string>();
            foreach (var route in routes)
            {
                Budget(watch); Require(route != null && !string.IsNullOrWhiteSpace(route.id) && ids.Add(route.id), "Route IDs must be nonempty and unique.");
                Require(route.role == "human" || route.role == "mosquito", "Route role must be human or mosquito.");
                Require(route.maxTicks >= 1 && route.maxTicks <= 600 && route.points != null && route.points.Length >= 1 && route.points.Length <= 64 && route.points.All(Finite), "Invalid route bounds/points: " + route.id);
                var role = route.role == "human" ? PlayerRole.Human : PlayerRole.Mosquito;
                var pool = role == PlayerRole.Human ? map.HumanSpawnPoints : map.MosquitoSpawnPoints;
                Require(pool != null && route.spawnIndex >= 0 && route.spawnIndex < pool.Length && pool[route.spawnIndex], "Invalid route spawn: " + route.id);
                RunCase("route/" + route.id, role, pool[route.spawnIndex].position, route, map, world, report, watch);
            }
            if (routes.Count(r => r.role == "human") < 2 || routes.Count(r => r.role == "mosquito") < 2)
                report.pending.Add("Author and execute at least two human and two mosquito routes through actual bottlenecks; pool checks do not establish traversal.");
            report.status = report.errors.Count > 0 || report.cases.Any(c => c.status == "FAIL") || report.passages.Any(p => p.status == "FAIL") ? "FAIL" : report.pending.Count > 0 ? "INCOMPLETE" : "PASS_SCOPED";
        }
        catch (TimeoutException e) { report.pending.Add(e.Message); report.status = report.errors.Count > 0 || report.cases.Any(c => c.status == "FAIL") ? "FAIL" : "INCOMPLETE"; }
        catch (Exception e) { report.errors.Add(e.ToString()); report.status = "FAIL"; }
        finally
        {
            if (owner) Object.DestroyImmediate(owner);
            Physics.SyncTransforms(); report.cleanup = !owner; report.seconds = watch.Elapsed.TotalSeconds;
            File.WriteAllText(reportPath, HiggsfieldMapJson.Write(report));
        }
        return reportPath;
    }

    static void CheckSpawns(Transform[] pool, PlayerRole role, EnvironmentMapDefinition map, UnityGameplayWorld world, Report report, Stopwatch watch)
    {
        if (pool == null) return;
        var seen = new HashSet<Transform>();
        for (int i = 0; i < pool.Length; i++)
        {
            Budget(watch); var spawn = pool[i];
            if (!spawn || !seen.Add(spawn) || !spawn.IsChildOf(map.transform) || !spawn.gameObject.activeInHierarchy)
            { report.errors.Add("Missing, duplicate, inactive or foreign " + role + " spawn " + i); continue; }
            for (int j = 0; j < i; j++) if (pool[j] && Vector3.Distance(spawn.position, pool[j].position) < (role == PlayerRole.Human ? .5f : .11f))
                report.errors.Add(role + " pool positions overlap: " + j + "/" + i);
            RunCase("spawn/" + role + "/" + i, role, spawn.position, null, map, world, report, watch);
        }
    }

    static void RunCase(string id, PlayerRole role, Vector3 position, Route route, EnvironmentMapDefinition map, UnityGameplayWorld world, Report report, Stopwatch watch)
    {
        var c = new Case { id = id, role = role.ToString(), requested = route?.points.Length ?? 0 }; report.cases.Add(c);
        try
        {
            foreach (var actor in world.Actors.Values.ToArray()) Object.DestroyImmediate(actor.gameObject);
            ((IDictionary<uint, GameplayActorProxy>)world.Actors).Clear();
            var host = new GameplayAuthority(world);
            var other = role == PlayerRole.Human ? PlayerRole.Mosquito : PlayerRole.Human;
            host.BeginRound(new GameplayRoundConfig(901, 1, map.MapId, map.ContentHash, doors: world.GetDoorDefinitions(), tools: world.GetToolDefinitions()), new[] {
                new SpawnActor(1, "probe", role, position.ToFloat()),
                new SpawnActor(2, "fixture-opposite-role", other, (map.transform.position + new Vector3(2000,2000,2000)).ToFloat()) });
            var proxy = world.Actors[1];
            c.initialPenetration = Penetration(proxy, map.transform, out c.initialBlocker);
            c.maxPenetration = c.initialPenetration;
            SampleState(c, host, map); uint sequence = 0;
            for (int t = 0; t < 30; t++) { Budget(watch); Tick(host, ref sequence, Vector3.zero, role, false, false); Track(c, proxy, host, map); }
            Require(role != PlayerRole.Human || proxy.State.Grounded, "Human did not settle grounded after 30 ticks.");
            Require(Vector3.Distance(position, proxy.State.Position.ToUnity()) <= .5f, "Spawn moved more than 0.5m during neutral settling.");
            if (route != null)
            {
                for (int t = 0; t <= route.maxTicks && c.reached < route.points.Length;)
                {
                    Budget(watch); var delta = map.transform.TransformPoint(route.points[c.reached]) - proxy.State.Position.ToUnity();
                    bool arrived = role == PlayerRole.Human ? new Vector2(delta.x, delta.z).magnitude <= .18f && Mathf.Abs(delta.y) <= .15f : delta.magnitude <= .12f;
                    if (arrived) { c.reached++; continue; }
                    if (t == route.maxTicks) break;
                    Tick(host, ref sequence, delta, role, route.crouch, route.sprint); Track(c, proxy, host, map); t++;
                }
                Require(c.reached == route.points.Length, "Route did not reach all points within its tick budget; inspect samples and geometry (no pathfinder in this driver).");
            }
            c.finalPenetration = Penetration(proxy, map.transform, out c.finalBlocker);
            Require(c.initialPenetration <= .002f, "Authored spawn penetrates geometry by more than 2mm, even if the motor later depenetrates it.");
            Require(c.finalPenetration <= .002f, "Final motor shape penetrates geometry by more than 2mm.");
            Require(c.maxPenetration <= .002f, "Motor shape penetration exceeded 2mm during traversal.");
            c.status = "PASS";
        }
        catch (TimeoutException) { c.reason = "Global time budget reached."; throw; }
        catch (Exception e) { c.status = "FAIL"; c.reason = e.Message; }
    }
    static void Tick(GameplayAuthority host, ref uint sequence, Vector3 delta, PlayerRole role, bool crouch, bool sprint)
    {
        // Forward input follows aim. Vertical-only flight uses the existing Vertical channel.
        float yaw = Mathf.Atan2(delta.x, delta.z), pitch = 0, vertical = 0, move = delta.sqrMagnitude > .000001f ? 1 : 0;
        if (role == PlayerRole.Mosquito && move > 0)
        {
            if (new Vector2(delta.x, delta.z).magnitude < .01f) { vertical = Mathf.Sign(delta.y); move = 0; }
            else pitch = Mathf.Clamp(Mathf.Asin(delta.normalized.y), -1.5533f, 1.5533f);
        }
        // Slow only the input magnitude near a target; motor speed/rules remain untouched.
        float distance = role == PlayerRole.Human ? new Vector2(delta.x, delta.z).magnitude : delta.magnitude;
        float amount = Mathf.Clamp01(distance / .3f);
        var state = host.CaptureSnapshot().Actors.First(a => a.ActorId == 1);
        var header = new CommandHeader(901, 1, 1, ++sequence, host.CurrentTick, state.ViewRevision);
        Require(host.SubmitInput("probe", new PlayerInputCommand(header, new Float2(0, move * amount), vertical * amount, yaw, pitch, MathEx.Aim(yaw, pitch), sprint: sprint, crouch: crouch)) == CommandReject.None, "Input rejected.");
        host.Advance(new HostTick(host.CurrentTick + 1));
    }
    static void Track(Case c, GameplayActorProxy proxy, GameplayAuthority host, EnvironmentMapDefinition map)
    {
        c.ticks++; Require(proxy.State.Position.IsFinite && proxy.State.Velocity.IsFinite, "Nonfinite motor state.");
        SampleState(c, host, map);
        Require(map.PlayBounds.Contains(map.transform.InverseTransformPoint(proxy.State.Position.ToUnity())), "Actor left authored PlayBounds. Bounds are not an enforced motor boundary.");
        c.finalPenetration = Penetration(proxy, map.transform, out c.finalBlocker);
        c.maxPenetration = Mathf.Max(c.maxPenetration, c.finalPenetration);
    }
    static void SampleState(Case c, GameplayAuthority host, EnvironmentMapDefinition map)
    {
        var state = host.CaptureSnapshot().Actors.First(a => a.ActorId == 1);
        c.samples.Add(new Sample { tick = host.CurrentTick, localPosition = map.transform.InverseTransformPoint(state.Position.ToUnity()), grounded = state.Grounded, crouch = state.CrouchFraction });
    }
    static float Penetration(GameplayActorProxy proxy, Transform root, out string blocker)
    {
        blocker = null; float depth = 0; var own = proxy.MotorCollider; var bounds = own.bounds;
        foreach (var other in Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * .005f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (other.transform.IsChildOf(root) && Physics.ComputePenetration(own, own.transform.position, own.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out float d) && d > depth)
            { depth = d; blocker = PathOf(other.transform, root); }
        return depth;
    }

    static void CheckNavigation(EnvironmentMapDefinition map, UnityGameplayWorld world, Report report, Stopwatch watch)
    {
        try
        {
            Require(map.SpatialData, "SpatialData absent; import recipe is not navigation data.");
            var p = JsonUtility.FromJson<Plan>(map.SpatialData.text);
            Require(p != null && p.schema_version == 1 && p.map_id == map.MapId && p.zones != null && p.zones.Length > 0 && p.portals != null, "SpatialData must contain schema_version:1, exact map_id, zones[] and portals[].");
            Require(p.zones.Length <= 256 && p.portals.Length <= 512, "Navigation exceeds bounded fixture limits.");
            var zones = new HashSet<string>(); var links = new HashSet<string>();
            foreach (var z in p.zones) { Require(z != null && !string.IsNullOrWhiteSpace(z.id) && zones.Add(z.id), "Invalid/duplicate zone ID."); var min = Point(z.min); var max = Point(z.max); Require(min.x < max.x && min.y < max.y && min.z < max.z, "Invalid zone bounds: " + z.id); }
            foreach (var link in p.portals)
            {
                Require(link != null && !string.IsNullOrWhiteSpace(link.id) && links.Add(link.id) && zones.Contains(link.from) && zones.Contains(link.to) && link.from != link.to, "Invalid/duplicate portal ID or endpoint.");
                Point(link.center); Require(Point(link.normal).sqrMagnitude > .000001f && MathEx.Finite(link.width) && MathEx.Finite(link.height) && link.width >= .3f && link.height >= .5f, "Portal would be ignored or malformed: " + link.id);
                if (link.door) Require(world.Doors.Values.Count(d => d.name == link.id) == 1, "Door portal must bind to exactly one GameplayDoor.name: " + link.id);
            }
            if (p.stair != null)
            {
                Require(!string.IsNullOrWhiteSpace(p.stair.id) && links.Add(p.stair.id) && zones.Contains(p.stair.from) && zones.Contains(p.stair.to) && p.stair.from != p.stair.to && p.stair.lower_flight != null && p.stair.upper_flight != null && p.stair.mid_landing != null, "Incomplete stair would be ignored by runtime.");
                foreach (var f in new[] { p.stair.lower_flight, p.stair.upper_flight })
                    Require(f.clear_x != null && f.clear_x.Length == 2 && f.clear_x.All(MathEx.Finite) && f.clear_x[1] > f.clear_x[0] && new[] { f.start_y, f.end_y, f.start_z, f.end_z }.All(MathEx.Finite), "Invalid stair flight.");
                var a = Point(p.stair.mid_landing.min); var b = Point(p.stair.mid_landing.max); Require(a.x < b.x && a.y < b.y && a.z < b.z, "Invalid stair landing.");
            }
            var type = typeof(UnityGameplayWorld).Assembly.GetType("LetMeSleep.Gameplay.Unity.GameplayBotNavigation", true);
            var nav = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { map.SpatialData, map.MapId, world }, null);
            var passages = (BotPassage[])type.GetField("passages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(nav);
            var reached = new HashSet<string> { p.zones[0].id };
            for (int n = 0; n < p.zones.Length; n++) foreach (var passage in passages) { if (reached.Contains(passage.From)) reached.Add(passage.To); if (reached.Contains(passage.To)) reached.Add(passage.From); }
            if (reached.Count != zones.Count) report.errors.Add("Navigation graph contains disconnected zones (door state ignored for connectivity).");
            foreach (var spawn in map.MosquitoSpawnPoints ?? Array.Empty<Transform>())
                if (spawn && !p.zones.Any(z => new Bounds((Point(z.min) + Point(z.max)) * .5f, Point(z.max) - Point(z.min)).Contains(map.transform.InverseTransformPoint(spawn.position)))) report.errors.Add("Mosquito spawn outside every navigation zone: " + spawn.name);
            foreach (var passage in passages)
            {
                Budget(watch); var row = new Passage { id = passage.Id, localPoints = passage.Points.Select(v => v.ToUnity()).ToArray(), status = "PASS_STATIC_CLEARANCE" }; report.passages.Add(row);
                var door = p.portals.FirstOrDefault(x => x.id == passage.Id && x.door);
                if (door != null && Mathf.Abs(world.Doors.Values.Single(d => d.name == door.id).AngleRadians) < 60 * Mathf.Deg2Rad)
                { row.status = "PENDING_CLOSED_DOOR"; report.pending.Add("Exercise opening and motor traversal for door " + door.id + " in a separate case; this fixture does not open doors."); continue; }
                for (int i = 0; i < row.localPoints.Length; i++)
                {
                    var start = map.transform.TransformPoint(row.localPoints[i]);
                    var blocker = Physics.OverlapSphere(start, .055f, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(c => c.transform.IsChildOf(map.transform));
                    if (!blocker && i + 1 < row.localPoints.Length)
                    {
                        var delta = map.transform.TransformPoint(row.localPoints[i + 1]) - start;
                        if (delta.sqrMagnitude > .000001f) blocker = Physics.SphereCastAll(start, .055f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Select(h => h.collider).FirstOrDefault(c => c.transform.IsChildOf(map.transform));
                    }
                    if (blocker) { row.status = "FAIL"; row.blocker = PathOf(blocker.transform, map.transform); break; }
                }
            }
        }
        catch (TimeoutException) { throw; }
        catch (Exception e) { report.errors.Add("Navigation: " + (e.InnerException ?? e).Message); }
    }
    static string PathOf(Transform t, Transform root) { string path = t.name; while (t.parent && t.parent != root) { t = t.parent; path = t.name + "/" + path; } return path; }
    static Vector3 Point(float[] v) { Require(v != null && v.Length == 3 && v.All(MathEx.Finite), "Invalid finite XYZ navigation point."); return new Vector3(v[0], v[1], v[2]); }
    static bool Finite(Vector3 v) => MathEx.Finite(v.x) && MathEx.Finite(v.y) && MathEx.Finite(v.z);
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static void Budget(Stopwatch watch) { if (watch.Elapsed.TotalSeconds > 45) throw new TimeoutException("45-second cooperative budget reached; split route configs and rerun. A single native call cannot be preempted."); }
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
}
