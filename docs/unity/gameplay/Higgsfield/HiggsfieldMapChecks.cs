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
        public string mapId, prefabPath, action, navigationOverridePath, colliderCandidate, validationReportPath, caseFilter;
        public int humanPool = 5, mosquitoPool = 16;
        public bool diagnosticRoutesOnly, campSpawnSupportCandidate, yateStorageCandidate, yateBulkheadCandidate, puertoStairCandidate;
        public Route[] routes;
    }
    [Serializable] public sealed class Route
    {
        public string id, role;
        public int spawnIndex, maxTicks = 600;
        public bool crouch, sprint, patrol, runtimePatrol, sphereSupport;
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
        public string status = "INCOMPLETE", utc, unityVersion, mapId, contentHash, configSha256, prefabDependencyHash, navigationSha256, navigationSource;
        public string scope = "Native EditMode physics queries + integrated authority at 30 Hz. No Physics.Simulate, player, rendering, WAN, performance or full-map coverage certification.";
        public List<string> errors = new List<string>(), pending = new List<string>(), strippedBehaviours = new List<string>(), loadedAssemblies = new List<string>();
        public List<Case> cases = new List<Case>();
        public List<Passage> passages = new List<Passage>();
        public int humanPool, mosquitoPool, nativeColliderCount;
        public double seconds;
        public bool cleanup;
        public SouthArrivalColliderCandidate.Receipt colliderCandidate;
        public CampTechnicalCandidate.Receipt campColliderCandidate;
        public CampTechnicalCandidate.SpawnReceipt campSpawnCandidate;
        public YateStorageCandidate.Receipt yateStorageCandidate;
        public YateBulkheadCandidate.Receipt yateBulkheadCandidate;
        public PuertoStairCandidate.Receipt puertoStairCandidate;
    }
    public sealed class Case
    {
        public string id, role, status = "INCOMPLETE", reason;
        public int ticks, reached, requested;
        public float initialPenetration, finalPenetration, maxPenetration;
        public string initialBlocker, finalBlocker;
        public string peakBlocker;
        public Vector3 peakPosition;
        public uint peakTick;
        public string driver;
        public List<string> visitedRegions = new List<string>();
        public float traveled;
        public int longestStillTicks, longestLostNavigationTicks, unzonedTransitTicks;
        public List<Sample> samples = new List<Sample>();
        public List<Hit> failureHits = new List<Hit>();
    }
    public sealed class Hit { public string collider, query; public float distance; public Vector3 point, normal; }
    public sealed class Sample { public uint tick; public Vector3 localPosition; public bool grounded; public float crouch; }
    public sealed class Passage { public string id, status, blocker; public Vector3[] localPoints; }

    public static string Run(string configPath, string outputDirectory)
    {
        var operation = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
        if(operation.action=="prepare-and-validate-remaining-map")
        {
            var preparationPath=HiggsfieldRemainingMapsPreparation.Run(configPath,outputDirectory);
            var preparation=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldRemainingMapsPreparation.Receipt>(File.ReadAllText(preparationPath));
            if(preparation.status!="PREPARED_NATIVE_VALIDATION_PENDING")return preparationPath;
            return Run(preparation.checksPath,outputDirectory);
        }
        if(operation.action=="prepare-camp")return HiggsfieldCampPreparation.Run(configPath,outputDirectory);
        if(operation.action=="prepare-remaining-map")return HiggsfieldRemainingMapsPreparation.Run(configPath,outputDirectory);
        if(operation.action=="apply-remaining-map")return ApplyRemainingSemanticNavigation.Run(configPath,outputDirectory);
        if(operation.action=="apply-camp-recorded-exception")return ApplyCampSemanticNavigation.Run(configPath,outputDirectory);
        if(operation.action=="human-motor-contacts")return HumanMotorContactChecks.Run(configPath,outputDirectory);
        if(operation.action=="apply-casa-semantic")return ApplyCasaSemanticNavigation.Run(configPath,outputDirectory);
        if (operation.action == "prepare-casa" || operation.action == "apply-casa" || operation.action == "fix-casa-spawn") return HiggsfieldCasaPreparation.Run(configPath, outputDirectory);
        if (operation.action == "prepare-isla" || operation.action == "apply-isla") return HiggsfieldIslaPreparation.Run(configPath, outputDirectory);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run only in an idle Editor, outside Play Mode, in the coordinator's native slot.");
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, "map-checks-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        var watch = Stopwatch.StartNew(); GameObject owner = null; TextAsset candidate = null;var ownedGeometry=new List<Object>();
        try
        {
            report.configSha256 = Hash(File.ReadAllBytes(configPath));
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
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
            if(!string.IsNullOrWhiteSpace(config.navigationOverridePath)) { candidate=new TextAsset(File.ReadAllText(config.navigationOverridePath));map.SpatialData=candidate;report.navigationSource="Candidate override on temporary clone: "+config.navigationOverridePath; }
            else report.navigationSource="Prefab SpatialData";
            if(map.SpatialData) report.navigationSha256=Hash(System.Text.Encoding.UTF8.GetBytes(map.SpatialData.text));
            Require(map.MapId == config.mapId, "Config and map IDs differ.");
            Require(!string.IsNullOrWhiteSpace(map.ContentHash), "Missing map content hash.");
            Require(Finite(map.PlayBounds.min) && Finite(map.PlayBounds.max) && map.PlayBounds.size.x > 0 && map.PlayBounds.size.y > 0 && map.PlayBounds.size.z > 0, "Invalid PlayBounds.");
            var world = owner.AddComponent<UnityGameplayWorld>(); world.MapRoot = clone.transform;
            clone.SetActive(true); owner.SetActive(true); Physics.SyncTransforms();
            if(config.colliderCandidate=="camp-paths-exact-down-extrusion")
                report.campColliderCandidate=CampTechnicalCandidate.ApplyPaths(clone,ownedGeometry);
            else if(!string.IsNullOrWhiteSpace(config.colliderCandidate))
            {
                Require(config.colliderCandidate=="south-arrival-local-convex-band" || config.colliderCandidate=="south-arrival-complete-convex-support" || config.colliderCandidate=="south-arrival-one-row-convex-support","Unknown collider candidate.");
                report.colliderCandidate=config.colliderCandidate=="south-arrival-one-row-convex-support" ? CompleteSouthArrivalSupport.Apply(clone,ownedGeometry,1) : SouthArrivalColliderCandidate.Apply(clone,ownedGeometry,config.colliderCandidate=="south-arrival-complete-convex-support");
            }
            if(config.campSpawnSupportCandidate)report.campSpawnCandidate=CampTechnicalCandidate.SurveySpawn(map,true);
            if(config.yateStorageCandidate)report.yateStorageCandidate=YateStorageCandidate.Apply(clone);
            if(config.yateBulkheadCandidate)report.yateBulkheadCandidate=YateBulkheadCandidate.Apply(clone,ownedGeometry);
            if(config.puertoStairCandidate)report.puertoStairCandidate=PuertoStairCandidate.Apply(clone,ownedGeometry);
            world.RegisterGeometry();
            report.nativeColliderCount = clone.GetComponentsInChildren<Collider>(false).Count(c => c.enabled && !c.isTrigger);
            Require(report.nativeColliderCount > 0, "No active physical map colliders.");
            report.humanPool = map.HumanSpawnPoints?.Length ?? 0; report.mosquitoPool = map.MosquitoSpawnPoints?.Length ?? 0;
            if (report.humanPool < config.humanPool || report.mosquitoPool < config.mosquitoPool) report.errors.Add("Spawn pools below contract: " + report.humanPool + " human / " + report.mosquitoPool + " mosquito.");
            Require(report.humanPool <= 32 && report.mosquitoPool <= 32, "More than 32 spawns in a pool exceeds this bounded fixture.");
            if(config.diagnosticRoutesOnly)report.pending.Add("Route-only diagnostic: spawn suite and navigation clearance deliberately not repeated; not a map acceptance run.");
            else
            {
                CheckNavigation(map, world, report, watch);
                CheckSpawns(map.HumanSpawnPoints, PlayerRole.Human, map, world, report, watch);
                CheckSpawns(map.MosquitoSpawnPoints, PlayerRole.Mosquito, map, world, report, watch);
            }
            var routes = config.routes ?? Array.Empty<Route>(); var ids = new HashSet<string>();
            foreach (var route in routes)
            {
                Budget(watch); Require(route != null && !string.IsNullOrWhiteSpace(route.id) && ids.Add(route.id), "Route IDs must be nonempty and unique.");
                Require(route.role == "human" || route.role == "mosquito", "Route role must be human or mosquito.");
                Require(route.maxTicks >= 1 && route.maxTicks <= 600 && route.points != null && (route.points.Length >= 1 || (route.patrol || route.runtimePatrol) && route.role == "mosquito") && route.points.Length <= 64 && route.points.All(Finite) && !(route.patrol && route.runtimePatrol), "Invalid route bounds/points: " + route.id);
                var role = route.role == "human" ? PlayerRole.Human : PlayerRole.Mosquito;
                var pool = role == PlayerRole.Human ? map.HumanSpawnPoints : map.MosquitoSpawnPoints;
                Require(pool != null && route.spawnIndex >= 0 && route.spawnIndex < pool.Length && pool[route.spawnIndex], "Invalid route spawn: " + route.id);
                if(route.runtimePatrol) RunRuntimePatrol(route,pool[route.spawnIndex].position,map,world,report,watch);
                else RunCase("route/" + route.id, role, pool[route.spawnIndex].position, route, map, world, report, watch);
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
            if (candidate) Object.DestroyImmediate(candidate);
            foreach(var geometry in ownedGeometry) if(geometry) Object.DestroyImmediate(geometry);
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
        var c = new Case { id = id, role = role.ToString(), requested = route?.points.Length ?? 0, driver = route?.patrol==true ? "Explore + custom maximum-input adapter at 30Hz; stress only, no BotController/SteerBot" : "Custom waypoint input + actual authority/motor at 30Hz" }; report.cases.Add(c);
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
            if (route != null && route.patrol)
            {
                var type=typeof(UnityGameplayWorld).Assembly.GetType("LetMeSleep.Gameplay.Unity.GameplayBotNavigation",true);
                var nav=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{map.SpatialData,map.MapId,world},null);
                var explore=type.GetMethod("Explore");var regions=(BotRegion[])type.GetField("regions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(nav);
                var visited=new HashSet<string>();float traveled=0;int still=0;
                for(int t=0;t<route.maxTicks;t++)
                {
                    Budget(watch);var previous=proxy.State.Position;var direction=(Float3)explore.Invoke(nav,new object[]{proxy.State,host.CurrentTick});
                    Tick(host,ref sequence,direction.ToUnity(),role,false,false);Track(c,proxy,host,map);
                    var local=map.transform.InverseTransformPoint(proxy.State.Position.ToUnity()).ToFloat();var zone=regions.Where(r=>r.Contains(local)).Select(r=>r.Id).FirstOrDefault();
                    Require(zone!=null,"Patrol left every authored region.");visited.Add(zone);
                    float moved=(proxy.State.Position-previous).Length;traveled+=moved;still=moved<.001f?still+1:0;Require(still<150,"Patrol stalled for 150 ticks.");
                }
                Require(visited.Count>=3 && traveled>2,"Patrol did not traverse at least three regions and 2m.");c.reason="Visited "+visited.Count+" regions; traveled "+traveled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"m.";
            }
            else if (route != null)
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
        catch (Exception e)
        {
            c.status = "FAIL"; c.reason = e.Message;
            if(route!=null && route.points!=null && c.reached<route.points.Length && world.Actors.TryGetValue(1,out var actor))
            {
                var pos=actor.State.Position.ToUnity();var delta=map.transform.TransformPoint(route.points[c.reached])-pos;delta.y=0;
                foreach(var movement in new[]{delta.normalized*Mathf.Min(delta.magnitude,.75f),(delta.normalized*3.1f+Vector3.down*.5f)/30,Vector3.down*.23f})
                {
                    var hits=Physics.CapsuleCastAll(pos+Vector3.up*.25f,pos+Vector3.up*1.47f,.25f,movement.normalized,movement.magnitude+.001f,~0,QueryTriggerInteraction.Ignore);
                    foreach(var hit in hits.Where(h=>h.collider.transform.IsChildOf(map.transform)).OrderBy(h=>h.distance)) c.failureHits.Add(new Hit{collider=PathOf(hit.collider.transform,map.transform),query=movement.ToString("F5"),distance=hit.distance,point=hit.point,normal=hit.normal});
                }
            }
        }
    }
    static void RunRuntimePatrol(Route route, Vector3 position, EnvironmentMapDefinition map, UnityGameplayWorld world, Report report, Stopwatch watch)
    {
        var c=new Case{id="route/"+route.id,role="Mosquito",driver="Actual GameplayRuntime.TickHost at30Hz; actual ObserveBot/BotController/SteerBot at10Hz; no visible enemy"};report.cases.Add(c);
        GameplayRuntime runtime=null;
        try
        {
            Require(route.role=="mosquito" && route.points.Length==0,"Runtime patrol requires mosquito and empty points; targets come only from actual navigation.");
            Require(map.SpatialData,"Runtime patrol requires navigation data.");
            foreach(var actor in world.Actors.Values.ToArray()) Object.DestroyImmediate(actor.gameObject);
            ((IDictionary<uint,GameplayActorProxy>)world.Actors).Clear();
            Require(!world.GetComponent<GameplayRuntime>(),"Unexpected existing runtime on fixture owner.");
            runtime=world.gameObject.AddComponent<GameplayRuntime>();
            runtime.IsHost=true;runtime.AutomaticTick=false;runtime.CaptureLocalInput=false;runtime.UseBuiltInCamera=false;runtime.NavigationData=map.SpatialData;
            runtime.BeginRound(new GameplayRoundConfig(901,1,map.MapId,map.ContentHash,doors:world.GetDoorDefinitions(),tools:world.GetToolDefinitions()),new[]{
                new SpawnActor(1,"probe",PlayerRole.Mosquito,position.ToFloat(),isBot:true),
                new SpawnActor(2,"fixture-opposite-role",PlayerRole.Human,(map.transform.position+new Vector3(2000,2000,2000)).ToFloat())});
            var host=runtime.Authority;var proxy=world.Actors[1];
            c.initialPenetration=Penetration(proxy,map.transform,out c.initialBlocker);c.maxPenetration=c.initialPenetration;SampleState(c,host,map);
            const BindingFlags privateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
            var nav=typeof(GameplayRuntime).GetField("botNavigation",privateInstance).GetValue(runtime);
            Require(nav!=null,"Actual runtime did not instantiate navigation.");
            var regions=(BotRegion[])nav.GetType().GetField("regions",privateInstance).GetValue(nav);
            var patrols=(IDictionary<uint,BotPatrol>)nav.GetType().GetField("patrols",privateInstance).GetValue(nav);
            var routeField=typeof(BotPatrol).GetField("route",privateInstance);
            var visited=new HashSet<string>();int still=0,lost=0;
            for(int t=0;t<route.maxTicks;t++)
            {
                Budget(watch);Require(host.IsRunning,"Round ended before patrol budget.");var previous=proxy.State.Position;uint previousTick=host.CurrentTick;
                runtime.TickHost();Require(host.CurrentTick==previousTick+1,"Runtime failed to advance exactly one host tick.");Track(c,proxy,host,map);
                var local=map.transform.InverseTransformPoint(proxy.State.Position.ToUnity()).ToFloat();string region=regions.Where(r=>r.Contains(local)).Select(r=>r.Id).FirstOrDefault();
                if(region!=null && visited.Add(region))c.visitedRegions.Add(region);
                // Read-only diagnostics: do not call Explore a second time or replace actual steering.
                bool active=patrols.TryGetValue(1,out var patrol) && routeField.GetValue(patrol)!=null;
                if(region==null && active)c.unzonedTransitTicks++;
                lost=region==null && !active?lost+1:0;c.longestLostNavigationTicks=Math.Max(c.longestLostNavigationTicks,lost);
                float moved=(proxy.State.Position-previous).Length;c.traveled+=moved;still=moved<.001f?still+1:0;c.longestStillTicks=Math.Max(c.longestStillTicks,still);
                Require(still<150,"Actual runtime remained still for150ticks.");Require(lost<150,"Actual runtime stayed outside regions with no active passage for150ticks.");
            }
            Require(c.initialPenetration<=.002f && c.maxPenetration<=.002f,"Actual motor penetration exceeded2mm.");
            Require(visited.Count>=2 && c.traveled>2,"Actual runtime did not reach two semantic regions and travel2m within20s.");
            c.status="PASS";c.reason="Scoped20s exploration; visited "+visited.Count+" regions. Does not establish all graph edges, combat or long-running behavior.";
        }
        catch(TimeoutException){c.reason="Global time budget reached.";throw;}
        catch(Exception e){c.status="FAIL";c.reason=(e.InnerException??e).Message;}
        finally{if(runtime){runtime.StopRound();Object.DestroyImmediate(runtime);}}
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
        if(c.finalPenetration>c.maxPenetration){c.peakBlocker=c.finalBlocker;c.peakPosition=map.transform.InverseTransformPoint(proxy.State.Position.ToUnity());c.peakTick=host.CurrentTick;}
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
            var p = Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(map.SpatialData.text);
            Require(p != null && p.schema_version == 1 && p.map_id == map.MapId && p.zones != null && p.zones.Length > 0 && p.portals != null, "SpatialData must contain schema_version:1, exact map_id, zones[] and portals[].");
            Require(p.zones.Length <= 1024 && p.portals.Length <= 3072, "Navigation exceeds bounded fixture limits.");
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
                if (spawn && !p.zones.Any(z => new BotRegion(z.id,Point(z.min).ToFloat(),Point(z.max).ToFloat()).Contains(map.transform.InverseTransformPoint(spawn.position).ToFloat()))) report.errors.Add("Mosquito spawn outside every navigation zone: " + spawn.name);
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
