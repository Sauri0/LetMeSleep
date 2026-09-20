using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Content.Environment;
using LetMeSleep.Gameplay.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Editor
{
    // Explicit five-map transaction. No floor, artistic mesh, global SaveAssets or runtime recovery policy.
    public static class HiggsfieldMapBoundaryInstaller
    {
        const string Node = "_LMS_ArtificialBoundaries_v1";
        const string RecoveryName = "LetMeSleep.Gameplay.Unity.GameplayRecoveryVolume";
        static readonly string[] Ids = { "hf-isla-del-laguito-v2", "hf-casa-del-patio-v1", "hf-campamento-pinar-v2", "hf-yate-a-la-deriva-v3", "hf-puerto-del-faro-v1" };
        static readonly string[] Faces = { "West", "East", "South", "North", "Ceiling" };
        public sealed class Guard { public string path, guid, sha256, metaSha256, dependencyHash; }
        public sealed class BoxInput { public float[] min, max; }
        public sealed class RingInput { public float[][] vertices; }
        public sealed class PolygonInput { public string id; public float[][] outer; public RingInput[] holes; public float minY,maxY,edgeTolerance; }
        public sealed class WaterGuard { public string objectName, mode; public float expectedCrestMaxY, expectedGpuAmplitude, expectedCpuAmplitude; }
        public sealed class RecoveryInput
        {
            public bool regionsApproved;
            public BoxInput[] humanFallZones,mosquitoFallZones;
            public PolygonInput[] humanPolygonFallZones,mosquitoPolygonFallZones;
            public float[][] humanSpawnPoints,mosquitoSpawnPoints;
            public int retryTicks;
            public float interiorMargin,supportProbe;
        }
        public sealed class MapInput
        {
            public string mapId, expectedContentHash;
            public string[] protectedObjectNames;
            public Guard prefab, scene, spatial;
            public float[] expectedPlayMin, expectedPlayMax, innerMin, innerMax, protectedSolidMin, protectedSolidMax;
            public float thickness, clearanceAboveSolid, wallBottomY;
            public int layer;
            public RecoveryInput recovery;
            public WaterGuard[] waterGuards;
        }
        public sealed class Request
        {
            public int schemaVersion;
            public string action, revision, receiptPath, scope,recoveryScriptSha256;
            public bool approvedExtents;
            public Guard catalog;
            public MapInput[] maps;
        }
        public sealed class MapReceipt
        {
            public string mapId, oldContentHash, newContentHash, originalInvariant, finalInvariant;
            public string prefabSha256Before, prefabSha256After, sceneSha256Before, sceneSha256After;
            public string prefabDependencyAfter, spatialSha256;
            public float[] innerMin, innerMax;
            public float[] nativeProtectedMin,nativeProtectedMax;
            public int nativeProtectedColliderCount;
            public bool prefabReadback, sceneReadback;
        }
        public sealed class Receipt
        {
            public string status = "INCOMPLETE", configSha256, unityVersion, error;
            public string scope = "Four static invisible lateral walls and ceiling, updated PlayBounds and explicit recovery opt-in configuration. No floor. Readback does not certify recovery behavior, movement/casts, navigation completion or visual quality.";
            public Request configuration;
            public bool success, rollbackAttempted, rollbackVerified;
            public string[] rollbackErrors;
            public List<MapReceipt> maps = new List<MapReceipt>();
        }
        sealed class Session
        {
            public MapInput input; public MapReceipt receipt;
            public GameObject loaded; public Scene scene;
            public EnvironmentMapDefinition prefabMap, sceneMap;
            public string prefabInvariant, sceneInvariant, prefabCurrentSha, sceneCurrentSha;
            public string[] prefabRows;
            public PropertyModification[] sceneOverrides;
            public bool prefabAttempted, prefabSaved, sceneAttempted, sceneSaved;
        }
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings {
            MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None,
            CheckAdditionalContent = true, MaxDepth = 48, Formatting = Formatting.Indented
        };

        public static void RunFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-higgsfieldBoundsConfig");
            Need(i >= 0 && i + 1 < args.Length && Array.LastIndexOf(args, "-higgsfieldBoundsConfig") == i, "One explicit bounds config required.");
            Run(args[i + 1]);
        }

        public static string Run(string configPath)
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Idle assigned Editor slot required.");
            Need(Path.IsPathRooted(configPath) && File.Exists(configPath) && new FileInfo(configPath).Length <= 1048576, "Absolute config <= 1 MiB required.");
            byte[] raw = File.ReadAllBytes(configPath);
            var token=JObject.Parse(Encoding.UTF8.GetString(raw),new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            Need(token["approvedExtents"]?.Type==JTokenType.Boolean,"Literal approval flag required.");
            Need(token["maps"] is JArray mapTokens && mapTokens.All(m=>m["recovery"]?["regionsApproved"]?.Type==JTokenType.Boolean),"Literal recovery region approval flags required.");
            var request = token.ToObject<Request>(JsonSerializer.Create(Json));
            ValidateRequest(request);
            string configHash = Hash(raw);
            if (File.Exists(request.receiptPath)) return VerifyPrior(request, configHash);
            var receipt = new Receipt { configSha256 = configHash, unityVersion = Application.unityVersion, configuration=request };
            var sessions = new List<Session>(); Scene priorActive = SceneManager.GetActiveScene();
            using (var stream = new FileStream(request.receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                Write(stream, receipt);
                try
                {
                    Verify(request.catalog);
                    VerifyRecoveryScript(request.recoveryScriptSha256);
                    // No asset is mutated until all ten saved assets and five spatial dependencies pass.
                    foreach (var input in request.maps)
                    {
                        Verify(input.prefab); Verify(input.scene); Verify(input.spatial);
                        string actualDependency=AssetDatabase.GetAssetDependencyHash(input.prefab.path).ToString();
                        Need(!string.IsNullOrEmpty(input.prefab.dependencyHash) && actualDependency == input.prefab.dependencyHash,
                            "Prefab dependency guard missing/stale: " + input.mapId+" expected="+input.prefab.dependencyHash+" observed="+actualDependency);
                        Need(!SceneManager.GetSceneByPath(input.scene.path).isLoaded, "Target scene already open; preserve its WIP: " + input.scene.path);
                        Need(!EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(input.prefab.path)), "Unsaved prefab changes.");
                        var s = new Session { input = input, prefabCurrentSha = input.prefab.sha256, sceneCurrentSha = input.scene.sha256 };
                        sessions.Add(s);
                        s.loaded = PrefabUtility.LoadPrefabContents(input.prefab.path);
                        s.prefabMap = s.loaded.GetComponent<EnvironmentMapDefinition>();
                        s.scene = EditorSceneManager.OpenScene(input.scene.path, OpenSceneMode.Additive);
                        s.sceneMap = s.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(o => o.MapId == input.mapId);
                        Need(!s.scene.isDirty && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(s.sceneMap.gameObject) == input.prefab.path, "Scene must contain the exact saved target prefab instance.");
                        Need(PrefabUtility.GetNearestPrefabInstanceRoot(s.sceneMap.gameObject) == s.sceneMap.gameObject, "Map must be the outer prefab instance root.");
                        s.sceneOverrides = PrefabUtility.GetPropertyModifications(s.sceneMap.gameObject);
                        ValidateBase(s.prefabMap, input); ValidateBase(s.sceneMap, input);
                        ValidateRecoveryCandidate(input);
                        s.prefabRows = InvariantRows(s.prefabMap);
                        File.WriteAllLines(request.receiptPath + "." + input.mapId + ".before.txt", s.prefabRows);
                        s.prefabInvariant = RowsHash(s.prefabRows); s.sceneInvariant = Invariant(s.sceneMap);
                        s.receipt = new MapReceipt { mapId = input.mapId, oldContentHash = input.expectedContentHash,
                            newContentHash = ContentHash(input, request.revision), originalInvariant = s.prefabInvariant,
                            innerMin = input.innerMin, innerMax = input.innerMax, spatialSha256 = input.spatial.sha256,
                            prefabSha256Before = input.prefab.sha256, sceneSha256Before = input.scene.sha256 };
                        var measured=MeasureProtected(s.prefabMap,input);
                        s.receipt.nativeProtectedMin=new[]{measured.min.x,measured.min.y,measured.min.z};s.receipt.nativeProtectedMax=new[]{measured.max.x,measured.max.y,measured.max.z};
                        s.receipt.nativeProtectedColliderCount=input.protectedObjectNames.Length;
                        receipt.maps.Add(s.receipt);
                    }
                    VerifyCatalogBindings(request, false);
                    if (request.action == "inspect") { receipt.status = "PASS_PREFLIGHT_ONLY_NO_ASSET_WRITES"; receipt.success = true; }
                    else
                    {
                        foreach (var s in sessions)
                        {
                            Verify(request.catalog); Verify(s.input.spatial);
                            ExpectFile(s.input.prefab, s.prefabCurrentSha); ExpectFile(s.input.scene, s.sceneCurrentSha);
                            Build(s.prefabMap, s.input, s.receipt.newContentHash);
                            var afterRows = InvariantRows(s.prefabMap);
                            Need(RowsHash(afterRows) == s.prefabInvariant, "Unrelated prefab data changed before save: " + s.input.mapId
                                + "\nRemoved: " + string.Join("\n", s.prefabRows.Except(afterRows).Take(12))
                                + "\nAdded: " + string.Join("\n", afterRows.Except(s.prefabRows).Take(12)));
                            s.prefabAttempted = true;
                            PrefabUtility.SaveAsPrefabAsset(s.loaded, s.input.prefab.path, out bool saved);
                            Need(saved, "Prefab save failed."); s.prefabSaved = true; s.prefabCurrentSha = HashFile(s.input.prefab.path);
                            // Updating a prefab refreshes its scene instance. Never create duplicate overrides for inherited colliders.
                            s.sceneMap = s.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(o => o.MapId == s.input.mapId);
                            Need(s.sceneMap.transform.Find(Node), "Scene did not inherit boundary node.");
                            SetDefinition(s.sceneMap, s.input, s.receipt.newContentHash);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(s.sceneMap);
                            ValidateBuilt(s.sceneMap, s.input, s.receipt.newContentHash);
                            Need(Invariant(s.sceneMap) == s.sceneInvariant, "Unrelated scene data changed.");
                            ExpectFile(s.input.scene, s.sceneCurrentSha);
                            s.sceneAttempted = true; EditorSceneManager.MarkSceneDirty(s.scene);
                            Need(EditorSceneManager.SaveScene(s.scene), "Scene save failed.");
                            s.sceneSaved = true; s.sceneCurrentSha = HashFile(s.input.scene.path);
                            Readback(s);
                            Write(stream, receipt);
                        }
                        Verify(request.catalog); VerifyRecoveryScript(request.recoveryScriptSha256); VerifyCatalogBindings(request, true);
                        foreach (var s in sessions) { Verify(s.input.spatial); ExpectFile(s.input.prefab, s.prefabCurrentSha); ExpectFile(s.input.scene, s.sceneCurrentSha); }
                        receipt.status = "APPLIED_READBACK_PASS_BOUNDARY_ENVELOPE_ONLY"; receipt.success = true;
                    }
                }
                catch (Exception error)
                {
                    receipt.error = error.ToString(); receipt.success = false; receipt.status = "FAILED";
                    if (sessions.Any(s => s.prefabAttempted || s.sceneAttempted))
                    {
                        receipt.rollbackAttempted = true; var failures = new List<string>();
                        foreach (var s in sessions.AsEnumerable().Reverse())
                            try { Rollback(s); } catch (Exception rollback) { failures.Add(s.input.mapId + ": " + rollback); }
                        receipt.rollbackErrors = failures.ToArray(); receipt.rollbackVerified = failures.Count == 0;
                        receipt.status = receipt.rollbackVerified ? "FAILED_ROLLBACK_VERIFIED" : "FAILED_ROLLBACK_CONFLICT_REQUIRES_REVIEW";
                    }
                    throw;
                }
                finally
                {
                    try
                    {
                        foreach (var s in sessions.AsEnumerable().Reverse())
                        {
                            if (s.loaded) PrefabUtility.UnloadPrefabContents(s.loaded);
                            if (s.scene.IsValid() && s.scene.isLoaded) Need(EditorSceneManager.CloseScene(s.scene, true), "Cannot close owned scene.");
                        }
                        if (priorActive.IsValid() && priorActive.isLoaded && !EditorSceneManager.IsPreviewScene(priorActive)
                            && SceneManager.GetActiveScene() != priorActive)
                            Need(SceneManager.SetActiveScene(priorActive), "Cannot restore active scene: " + priorActive.path);
                    }
                    catch (Exception cleanup) { receipt.success = false; receipt.status = "FAILED_CLEANUP"; receipt.error += "\n" + cleanup; throw; }
                    finally { Write(stream, receipt); }
                }
            }
            return request.receiptPath;
        }

        static void ValidateRequest(Request r)
        {
            Need(r != null && r.schemaVersion == 1 && (r.action == "inspect" || r.action == "apply") && r.revision == "map-boundaries-v1", "Explicit schema/action/revision required.");
            Need(r.action != "apply" || r.approvedExtents, "Root-approved extents required before application.");
            Need(r.maps != null && r.maps.Length == 5 && r.maps.Select(m => m.mapId).OrderBy(s => s).SequenceEqual(Ids.OrderBy(s => s)), "Exactly five final IDs required.");
            Need(!string.IsNullOrEmpty(r.receiptPath) && Path.IsPathRooted(r.receiptPath) && Directory.Exists(Path.GetDirectoryName(r.receiptPath)), "Existing external receipt directory required.");
            Need(!Path.GetFullPath(r.receiptPath).StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Receipt must stay outside Assets.");
            foreach (var m in r.maps)
            {
                string prefix = "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/" + m.mapId;
                Need(m.prefab.path == prefix + "/Prefabs/" + m.mapId + ".prefab" && m.scene.path == prefix + "/Scenes/" + m.mapId + ".unity", "Canonical final map targets only.");
                var lo = Vector(m.innerMin); var hi = Vector(m.innerMax); var protectedLo = Vector(m.protectedSolidMin); var protectedHi = Vector(m.protectedSolidMax);
                Need(lo.x < hi.x && lo.y < hi.y && lo.z < hi.z && m.thickness >= .5f && m.thickness <= 2 && m.layer == 0, "Invalid envelope, thickness or Default geometry layer.");
                Need(lo.x <= protectedLo.x - .25f && hi.x >= protectedHi.x + .25f && lo.z <= protectedLo.z - .25f && hi.z >= protectedHi.z + .25f,
                    "Boundary would cut configured protected geometry/capsule margin.");
                Need(!float.IsNaN(m.wallBottomY) && !float.IsInfinity(m.wallBottomY) && m.wallBottomY < lo.y && m.wallBottomY < protectedLo.y - 1 &&
                    m.clearanceAboveSolid >= 1.72f + .055f && hi.y >= protectedHi.y + m.clearanceAboveSolid,
                    "Ceiling/side lower extent insufficient for protected geometry.");
                Vector(m.expectedPlayMin); Vector(m.expectedPlayMax);
                Need(Hex(m.expectedContentHash, 64), "Content hash required.");
                Need(m.recovery!=null && (r.action!="apply" || m.recovery.regionsApproved),"Explicit approved recovery configuration required for apply.");
            }
        }
        static void ValidateBase(EnvironmentMapDefinition map, MapInput m)
        {
            Need(map && map.MapId == m.mapId && map.ContentHash == m.expectedContentHash, "Content changed; refresh config after other map work.");
            Need(Near(map.PlayBounds.min, Vector(m.expectedPlayMin)) && Near(map.PlayBounds.max, Vector(m.expectedPlayMax)), "PlayBounds changed.");
            Need(map.SpatialData && AssetDatabase.GetAssetPath(map.SpatialData) == m.spatial.path, "Spatial reference changed.");
            Need(!map.transform.Find(Node) && !map.GetComponentInParent<GameplaySurface>(), "Existing boundary or perchable root/ancestor.");
            Need(!map.GetComponent(RecoveryType()),"Existing recovery component/WIP; installer owns initial opt-in only.");
            Need(Near(map.transform.localScale, Vector3.one), "Unscaled map root required.");
            MeasureProtected(map,m);
            ValidateWaterCrests(map,m);
            var groups=new[]{new{spawns=map.HumanSpawnPoints,radius=.25f,height=1.72f,human=true},new{spawns=map.MosquitoSpawnPoints,radius=.055f,height=.055f,human=false}};
            foreach(var group in groups)
            foreach (var spawn in group.spawns ?? Array.Empty<Transform>())
            {
                Need(spawn && spawn.IsChildOf(map.transform), "Missing/foreign spawn.");
                Vector3 p = map.transform.InverseTransformPoint(spawn.position), lo = Vector(m.innerMin), hi = Vector(m.innerMax);
                Need(p.x >= lo.x + group.radius && p.x <= hi.x - group.radius && p.z >= lo.z + group.radius && p.z <= hi.z - group.radius &&
                    p.y-(group.human?0:group.radius)>lo.y && p.y+group.height<hi.y,"Spawn clearance outside approved envelope.");
            }
        }
        static void Build(EnvironmentMapDefinition map, MapInput m, string content)
        {
            Need(!map.transform.Find(Node), "Refusing existing boundary/WIP.");
            var root = new GameObject(Node); root.transform.SetParent(map.transform, false); root.isStatic = true; root.layer = m.layer;
            var lo = Vector(m.innerMin); lo.y=m.wallBottomY; var hi = Vector(m.innerMax); var mid = (lo + hi) / 2; var size = hi - lo; float t = m.thickness;
            var centers = new[] { new Vector3(lo.x-t/2,mid.y,mid.z),new Vector3(hi.x+t/2,mid.y,mid.z),
                new Vector3(mid.x,mid.y,lo.z-t/2),new Vector3(mid.x,mid.y,hi.z+t/2),new Vector3(mid.x,hi.y+t/2,mid.z) };
            var sizes = new[] { new Vector3(t,size.y+t,size.z+2*t),new Vector3(t,size.y+t,size.z+2*t),
                new Vector3(size.x+2*t,size.y+t,t),new Vector3(size.x+2*t,size.y+t,t),new Vector3(size.x+2*t,t,size.z+2*t) };
            for (int i=0;i<5;i++)
            {
                var wall = new GameObject(Faces[i]); wall.transform.SetParent(root.transform,false); wall.isStatic=true; wall.layer=m.layer;
                var collider=wall.AddComponent<BoxCollider>();collider.center=centers[i];collider.size=sizes[i];collider.isTrigger=false;
            }
            CreateRecovery(map.gameObject,m);SetDefinition(map,m,content); ValidateBuilt(map,m,content);
        }
        static Bounds MeasureProtected(EnvironmentMapDefinition map,MapInput m)
        {
            Need(m.protectedObjectNames!=null && m.protectedObjectNames.Length>0 && m.protectedObjectNames.Distinct().Count()==m.protectedObjectNames.Length,"Explicit unique protected solid names required.");
            var colliders=map.GetComponentsInChildren<MeshCollider>(true).GroupBy(c=>c.name).ToDictionary(g=>g.Key,g=>g.ToArray());
            Vector3 lo=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),hi=-lo;
            foreach(var name in m.protectedObjectNames)
            {
                Need(colliders.TryGetValue(name,out var matches)&&matches.Length==1,"Protected solid collider missing/ambiguous: "+name);
                var c=matches[0];Need(c.enabled&&!c.isTrigger&&c.sharedMesh,"Protected solid disabled/missing mesh: "+name);
                var b=c.sharedMesh.bounds;var matrix=map.transform.worldToLocalMatrix*c.transform.localToWorldMatrix;
                for(int bits=0;bits<8;bits++)
                {
                    var p=matrix.MultiplyPoint3x4(new Vector3((bits&1)==0?b.min.x:b.max.x,(bits&2)==0?b.min.y:b.max.y,(bits&4)==0?b.min.z:b.max.z));
                    lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);
                }
            }
            var expectedLo=Vector(m.protectedSolidMin);var expectedHi=Vector(m.protectedSolidMax);
            Need(lo.x>=expectedLo.x-.005f&&lo.y>=expectedLo.y-.005f&&lo.z>=expectedLo.z-.005f&&hi.x<=expectedHi.x+.005f&&hi.y<=expectedHi.y+.005f&&hi.z<=expectedHi.z+.005f,
                "Native protected mesh envelope exceeds measured source; revise proposal, never trim content.");
            var innerLo=Vector(m.innerMin);var innerHi=Vector(m.innerMax);
            Need(lo.x>=innerLo.x+.25f&&lo.z>=innerLo.z+.25f&&hi.x<=innerHi.x-.25f&&hi.z<=innerHi.z-.25f&&hi.y+1.72f+.055f<innerHi.y,
                "Native protected access/body clearance intersects proposed boundary.");
            var result=new Bounds();result.SetMinMax(lo,hi);return result;
        }
        static void ValidateWaterCrests(EnvironmentMapDefinition map,MapInput m)
        {
            Need(m.waterGuards!=null,"Explicit water crest guard list required (empty for Casa).");
            foreach(var guard in m.waterGuards)
            {
                var transforms=map.GetComponentsInChildren<Transform>(true).Where(t=>t.name==guard.objectName).ToArray();
                Need(transforms.Length==1,"Water object missing/ambiguous: "+guard.objectName);var t=transforms[0];
                Need(t.GetComponents<Collider>().Length==0,"Water must not gain an artificial collider.");
                var matrix=map.transform.worldToLocalMatrix*t.localToWorldMatrix;float max=float.NegativeInfinity;
                if(guard.mode=="cpu_morph_pair")
                {
                    var skin=t.GetComponent<SkinnedMeshRenderer>();var helper=t.GetComponent<LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater>();
                    Need(skin&&helper&&helper.enabled&&skin.sharedMesh&&skin.bones.Length==0,
                        "Expected CPU morph-water binding: " + m.mapId + "/" + guard.objectName
                        + " skinned=" + (bool)skin + " helper=" + (bool)helper
                        + " enabled=" + (helper && helper.enabled) + " bones=" + (skin ? skin.bones.Length : -1));
                    var mesh=skin.sharedMesh;var vertices=mesh.vertices;var delta=new Vector3[vertices.Length];
                    foreach(var wave in new[]{helper.WaveA,helper.WaveB})
                    {
                        int index=LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater.FindShape(mesh,wave);
                        Need(index>=0&&mesh.GetBlendShapeFrameCount(index)==1&&Mathf.Abs(mesh.GetBlendShapeFrameWeight(index,0)-100)<.0001f,"Unexpected CPU morph frame contract.");
                        mesh.GetBlendShapeFrameVertices(index,0,delta,null,null);
                        for(int i=0;i<vertices.Length;i++)max=Mathf.Max(max,matrix.MultiplyPoint3x4(vertices[i]+delta[i]).y);
                    }
                }
                else
                {
                    var filter=t.GetComponent<MeshFilter>();Need(filter&&filter.sharedMesh,"Expected static/GPU water mesh.");
                    foreach(var vertex in filter.sharedMesh.vertices)max=Mathf.Max(max,matrix.MultiplyPoint3x4(vertex).y);
                    if(guard.mode=="cpu_sine_waves")
                    {
                        var helper=t.GetComponent<LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater>();
                        Need(helper && helper.enabled && !t.GetComponent<SkinnedMeshRenderer>()
                            && filter.sharedMesh.isReadable && filter.sharedMesh.vertexCount <= LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater.MaximumAnimatedVertices,
                            "Expected CPU sine-water binding: " + guard.objectName);
                        Need(Mathf.Abs(helper.Amplitude-guard.expectedCpuAmplitude)<1e-6f && helper.Amplitude>=0 && helper.Amplitude<=.15f,
                            "CPU amplitude changed; update crest zone.");
                        max+=Mathf.Abs(map.transform.InverseTransformVector(Vector3.up).y)*helper.Amplitude;
                    }
                    else if(guard.mode=="gpu")
                    {
                        var bindings=map.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c&&c.GetType().FullName=="LetMeSleep.Presentation.HiggsfieldGpuWaterBinding")
                            .Where(c=>Field(c,"Water") is Renderer renderer && renderer.transform==t).ToArray();
                        Need(bindings.Length==1&&bindings[0].enabled,"Expected exact GPU binding.");
                        float amplitude=(float)Field(Field(bindings[0],"Settings"),"Amplitude");
                        Need(Mathf.Abs(amplitude-guard.expectedGpuAmplitude)<1e-6f,"GPU amplitude changed; update crest zone.");max+=amplitude;
                    }
                    else Need(guard.mode=="static"&&!t.GetComponent<LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater>(),"Unknown/static water animation mode.");
                }
                Need(!float.IsNaN(max)&&!float.IsInfinity(max)&&Mathf.Abs(max-guard.expectedCrestMaxY)<.001f,
                    "Imported water maximum differs from measured crest; revise region before applying: "+guard.objectName+" observed="+max.ToString("R",CultureInfo.InvariantCulture));
            }
        }
        static void SetDefinition(EnvironmentMapDefinition map,MapInput m,string hash)
        { map.ContentHash=hash;var bounds=new Bounds();bounds.SetMinMax(Vector(m.innerMin),Vector(m.innerMax));map.PlayBounds=bounds;EditorUtility.SetDirty(map); }
        static void ValidateBuilt(EnvironmentMapDefinition map,MapInput m,string hash)
        {
            Need(map.ContentHash==hash && Near(map.PlayBounds.min,Vector(m.innerMin)) && Near(map.PlayBounds.max,Vector(m.innerMax)),"Definition readback mismatch.");
            ValidateRecovery(map.gameObject,m);
            var root=map.transform.Find(Node);Need(root && root.childCount==5 && root.gameObject.activeSelf,"Exactly five boundaries required.");
            Need(root.GetComponentsInChildren<Renderer>(true).Length==0 && root.GetComponentsInChildren<GameplaySurface>(true).Length==0 && !root.GetComponentInParent<GameplaySurface>(),"Boundaries must be invisible and nonperchable.");
            Need(root.GetComponentsInChildren<Rigidbody>(true).Length==0 && root.GetComponentsInChildren<Collider>(true).Length==5,"Static five-collider envelope only.");
            var lo=Vector(m.innerMin);lo.y=m.wallBottomY;var hi=Vector(m.innerMax);var mid=(lo+hi)/2;var size=hi-lo;float t=m.thickness;
            for(int i=0;i<5;i++)
            {
                var child=root.Find(Faces[i]);Need(child && child.gameObject.isStatic && child.gameObject.layer==m.layer && child.gameObject.activeSelf,"Boundary child missing/static/layer changed.");
                Need(child.GetComponents<Component>().Length==2 && Near(child.localPosition,Vector3.zero) && Near(child.localScale,Vector3.one) && Quaternion.Angle(child.localRotation,Quaternion.identity)<1e-5f,"Unexpected boundary components/transform.");
                var c=child.GetComponent<BoxCollider>();Need(c && c.enabled && !c.isTrigger && !c.attachedRigidbody,"Invalid solid boundary.");
                var expectedCenter=i==0?new Vector3(lo.x-t/2,mid.y,mid.z):i==1?new Vector3(hi.x+t/2,mid.y,mid.z):i==2?new Vector3(mid.x,mid.y,lo.z-t/2):i==3?new Vector3(mid.x,mid.y,hi.z+t/2):new Vector3(mid.x,hi.y+t/2,mid.z);
                var expectedSize=i<2?new Vector3(t,size.y+t,size.z+2*t):i<4?new Vector3(size.x+2*t,size.y+t,t):new Vector3(size.x+2*t,t,size.z+2*t);
                Need(Near(c.center,expectedCenter)&&Near(c.size,expectedSize),"Boundary geometry changed.");
            }
        }
        static void Readback(Session s)
        {
            PrefabUtility.UnloadPrefabContents(s.loaded);s.loaded=PrefabUtility.LoadPrefabContents(s.input.prefab.path);s.prefabMap=s.loaded.GetComponent<EnvironmentMapDefinition>();
            ValidateBuilt(s.prefabMap,s.input,s.receipt.newContentHash);var readbackRows=InvariantRows(s.prefabMap); Need(RowsHash(readbackRows)==s.prefabInvariant,"Prefab invariant readback differs: " + s.input.mapId + "\nRemoved: " + string.Join("\n",s.prefabRows.Except(readbackRows).Take(20)) + "\nAdded: " + string.Join("\n",readbackRows.Except(s.prefabRows).Take(20)));s.receipt.prefabReadback=true;
            Need(EditorSceneManager.CloseScene(s.scene,true),"Cannot close saved scene for readback.");s.scene=EditorSceneManager.OpenScene(s.input.scene.path,OpenSceneMode.Additive);
            s.sceneMap=s.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==s.input.mapId);
            ValidateBuilt(s.sceneMap,s.input,s.receipt.newContentHash);Need(Invariant(s.sceneMap)==s.sceneInvariant,"Scene invariant readback differs.");s.receipt.sceneReadback=true;
            s.receipt.finalInvariant=Invariant(s.prefabMap);s.receipt.prefabSha256After=s.prefabCurrentSha;s.receipt.sceneSha256After=s.sceneCurrentSha;
            s.receipt.prefabDependencyAfter=AssetDatabase.GetAssetDependencyHash(s.input.prefab.path).ToString();
        }
        static void Rollback(Session s)
        {
            if(!s.prefabAttempted && !s.sceneAttempted)return;
            // A failed save or external writer with an unknown hash is never overwritten.
            ExpectFile(s.input.prefab,s.prefabCurrentSha);ExpectFile(s.input.scene,s.sceneCurrentSha);
            if(s.scene.IsValid() && s.scene.isLoaded)Need(EditorSceneManager.CloseScene(s.scene,true),"Cannot discard owned scene changes.");
            if(s.loaded)PrefabUtility.UnloadPrefabContents(s.loaded);
            s.loaded=PrefabUtility.LoadPrefabContents(s.input.prefab.path);s.prefabMap=s.loaded.GetComponent<EnvironmentMapDefinition>();
            if(s.prefabSaved)
            {
                var node=s.prefabMap.transform.Find(Node);Need(node,"Owned boundary missing during rollback.");Object.DestroyImmediate(node.gameObject);
                var recovery=s.prefabMap.GetComponent(RecoveryType());Need(recovery,"Owned recovery component missing during rollback.");Object.DestroyImmediate(recovery);
                s.prefabMap.ContentHash=s.input.expectedContentHash;var old=new Bounds();old.SetMinMax(Vector(s.input.expectedPlayMin),Vector(s.input.expectedPlayMax));s.prefabMap.PlayBounds=old;
                Need(Invariant(s.prefabMap)==s.prefabInvariant,"Rollback would overwrite unrelated prefab change.");
                PrefabUtility.SaveAsPrefabAsset(s.loaded,s.input.prefab.path,out bool saved);Need(saved,"Prefab rollback save failed.");
            }
            s.scene=EditorSceneManager.OpenScene(s.input.scene.path,OpenSceneMode.Additive);
            s.sceneMap=s.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==s.input.mapId);
            if(s.sceneSaved)
            {
                PrefabUtility.SetPropertyModifications(s.sceneMap.gameObject,s.sceneOverrides);
                Need(s.sceneMap.ContentHash==s.input.expectedContentHash,"Original scene override restoration failed.");
                Need(Invariant(s.sceneMap)==s.sceneInvariant,"Rollback would overwrite unrelated scene change.");
                EditorSceneManager.MarkSceneDirty(s.scene);Need(EditorSceneManager.SaveScene(s.scene),"Scene rollback save failed.");
            }
            ValidateBase(s.prefabMap,s.input);ValidateBase(s.sceneMap,s.input);
        }
        static string VerifyPrior(Request request,string configHash)
        {
            var prior=JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(request.receiptPath),Json);
            Need(prior!=null && prior.success && prior.configSha256==configHash && prior.status=="APPLIED_READBACK_PASS_BOUNDARY_ENVELOPE_ONLY","Existing receipt differs/incomplete; never overwrite it.");
            Verify(request.catalog);
            VerifyRecoveryScript(request.recoveryScriptSha256);
            foreach(var m in request.maps)
            {
                var row=prior.maps.Single(x=>x.mapId==m.mapId);ExpectFile(m.prefab,row.prefabSha256After);ExpectFile(m.scene,row.sceneSha256After);Verify(m.spatial);
                Need(AssetDatabase.GetAssetDependencyHash(m.prefab.path).ToString()==row.prefabDependencyAfter,"Dependency changed since prior success.");
                GameObject loaded=PrefabUtility.LoadPrefabContents(m.prefab.path);
                try{ValidateBuilt(loaded.GetComponent<EnvironmentMapDefinition>(),m,row.newContentHash);}finally{PrefabUtility.UnloadPrefabContents(loaded);}
            }
            return request.receiptPath; // Exact idempotent success; zero saves and no hash chaining.
        }
        static void VerifyCatalogBindings(Request r,bool after)
        {
            var catalog=AssetDatabase.LoadMainAssetAtPath(r.catalog.path);Need(catalog && !EditorUtility.IsDirty(catalog),"Saved catalog required.");
            var entries=new SerializedObject(catalog).FindProperty("entries");Need(entries!=null && entries.arraySize==5,"Five catalog bindings required.");
            var seen=new HashSet<string>();
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);var id=entry.FindPropertyRelative("MapId").stringValue;Need(seen.Add(id),"Duplicate catalog ID.");
                var input=r.maps.Single(m=>m.mapId==id);var prefab=entry.FindPropertyRelative("Prefab").objectReferenceValue as EnvironmentMapDefinition;
                Need(prefab && AssetDatabase.GetAssetPath(prefab)==input.prefab.path && prefab.ContentHash==(after?ContentHash(input,r.revision):input.expectedContentHash),"Catalog bound to wrong/stale map.");
            }
        }
        static string Invariant(EnvironmentMapDefinition map)
            => RowsHash(InvariantRows(map));
        static string RowsHash(string[] rows) => Hash(Encoding.UTF8.GetBytes(string.Join("\n", rows)));
        static string[] InvariantRows(EnvironmentMapDefinition map)
        {
            var rows=new List<string>();
            foreach(var transform in map.GetComponentsInChildren<Transform>(true))
            {
                if(transform!=map.transform && (transform.name==Node || transform.GetComponentsInParent<Transform>(true).Any(t=>t!=map.transform && t.name==Node)))continue;
                rows.Add("object|"+PathOf(transform,map.transform)+"|"+transform.gameObject.layer+"|"+transform.gameObject.activeSelf+"|"+transform.gameObject.isStatic);
                foreach(var component in transform.GetComponents<Component>())
                {
                    if(transform==map.transform && component && component.GetType().FullName==RecoveryName)continue;
                    Need(component,"Missing script in protected map.");var serialized=new SerializedObject(component);var p=serialized.GetIterator();
                    bool enterChildren=true;
                    while(p.Next(enterChildren))
                    {
                        // Object references are already normalized by Value; their internal m_FileID is transient.
                        enterChildren=p.propertyType==SerializedPropertyType.Generic;
                        if(p.name=="m_ObjectHideFlags" || p.name=="m_CorrespondingSourceObject" || p.name=="m_PrefabInstance" || p.name=="m_PrefabAsset" || p.propertyPath.StartsWith("m_Children") ||
                            component==map && (p.propertyPath=="ContentHash" || p.propertyPath.StartsWith("ContentHash.",StringComparison.Ordinal) || p.propertyPath.StartsWith("PlayBounds")))continue;
                        if(p.propertyType==SerializedPropertyType.Generic)continue;
                        rows.Add(PathOf(transform,map.transform)+"|"+component.GetType().FullName+"|"+p.propertyPath+"|"+Value(p,map.transform));
                    }
                }
            }
            return rows.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        static string Value(SerializedProperty p,Transform root)
        {
            switch(p.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    var o=p.objectReferenceValue;if(!o)return "null";
                    if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id) && !string.IsNullOrEmpty(guid))return guid+":"+id;
                    var t=o is GameObject g?g.transform:o is Component c?c.transform:null;
                    Need(t && (t==root || t.IsChildOf(root)),"External scene reference unsupported in invariant: "+p.propertyPath);return PathOf(t,root)+":"+o.GetType().FullName;
                case SerializedPropertyType.Integer:case SerializedPropertyType.ArraySize:case SerializedPropertyType.LayerMask:case SerializedPropertyType.Character:return p.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:return p.boolValue.ToString();
                case SerializedPropertyType.Float:return p.doubleValue.ToString("R",CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:return p.stringValue;
                case SerializedPropertyType.Enum:return p.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2:return p.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3:return p.vector3Value.ToString("R");
                case SerializedPropertyType.Vector4:return p.vector4Value.ToString("R");
                case SerializedPropertyType.Quaternion:return p.quaternionValue.ToString("R");
                case SerializedPropertyType.Color:return p.colorValue.ToString("R");
                case SerializedPropertyType.Bounds:return p.boundsValue.ToString("R");
                case SerializedPropertyType.Rect:return p.rectValue.ToString("R");
                default:throw new InvalidOperationException("Unreviewed serialized property type "+p.propertyType+" at "+p.propertyPath);
            }
        }
        static Type RecoveryType()
        {
            var types=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(RecoveryName,false)).Where(t=>t!=null).ToArray();
            Need(types.Length==1 && typeof(MonoBehaviour).IsAssignableFrom(types[0]),"Integrate the approved recovery component before native inspection/application.");return types[0];
        }
        static void VerifyRecoveryScript(string expected)
        {
            Need(Hex(expected,64),"Explicit integrated recovery source SHA required.");var type=RecoveryType();
            var scripts=AssetDatabase.FindAssets("GameplayRecoveryVolume t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>).Where(s=>s && s.GetClass()==type).ToArray();
            Need(scripts.Length==1 && HashFile(AssetDatabase.GetAssetPath(scripts[0]))==expected.ToLowerInvariant(),"Recovery schema/source differs from approved configuration.");
        }
        static void ValidateRecoveryCandidate(MapInput input)
        {
            var temporary=new GameObject("_BoundsRecoveryConfig_Preflight");
            try{CreateRecovery(temporary,input);ValidateRecovery(temporary,input);}finally{Object.DestroyImmediate(temporary);}
        }
        static object Field(object value,string field)=>value.GetType().GetField(field,BindingFlags.Public|BindingFlags.Instance).GetValue(value);
        static void Set(object value,string field,object data)
        {var member=value.GetType().GetField(field,BindingFlags.Public|BindingFlags.Instance);Need(member!=null,"Recovery schema field missing: "+field);member.SetValue(value,data);}
        static Bounds BoundsOf(float[] min,float[] max)
        {var b=new Bounds();b.SetMinMax(Vector(min),Vector(max));Need(b.size.x>0&&b.size.y>0&&b.size.z>0,"Invalid recovery bounds.");return b;}
        static Vector2[] Points2(float[][] values)
        {Need(values!=null,"Explicit polygon vertices required.");return values.Select(a=>{Need(a!=null&&a.Length==2&&a.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),"Finite XZ pair required.");return new Vector2(a[0],a[1]);}).ToArray();}
        static Array Polygons(Type componentType,string field,PolygonInput[] values)
        {
            Need(values!=null&&values.Length<=64,"Explicit <=64 polygon regions required.");
            var type=componentType.GetField(field).FieldType.GetElementType();var array=Array.CreateInstance(type,values.Length);
            for(int i=0;i<values.Length;i++)
            {
                var source=values[i];Need(source!=null&&source.holes!=null,"Explicit polygon holes array required.");var item=Activator.CreateInstance(type);
                Set(item,"Id",source.id);Set(item,"Outer",Points2(source.outer));Set(item,"MinY",source.minY);Set(item,"MaxY",source.maxY);Set(item,"EdgeTolerance",source.edgeTolerance);
                var ringType=type.GetField("Holes").FieldType.GetElementType();var holes=Array.CreateInstance(ringType,source.holes.Length);
                for(int j=0;j<source.holes.Length;j++){var ring=Activator.CreateInstance(ringType);Set(ring,"Vertices",Points2(source.holes[j].vertices));holes.SetValue(ring,j);}
                Set(item,"Holes",holes);array.SetValue(item,i);
            }
            return array;
        }
        static void CreateRecovery(GameObject root,MapInput m)
        {
            var r=m.recovery;var type=RecoveryType();Need(!root.GetComponent(type),"Recovery component already exists.");
            var c=root.AddComponent(type);Set(c,"SafetyBounds",BoundsOf(m.innerMin,m.innerMax));
            Need(r.humanFallZones!=null&&r.mosquitoFallZones!=null&&r.humanSpawnPoints!=null&&r.mosquitoSpawnPoints!=null,"Explicit recovery boxes/spawns required.");
            Set(c,"HumanFallZones",r.humanFallZones.Select(b=>BoundsOf(b.min,b.max)).ToArray());
            Set(c,"MosquitoFallZones",r.mosquitoFallZones.Select(b=>BoundsOf(b.min,b.max)).ToArray());
            Set(c,"HumanSpawnPoints",r.humanSpawnPoints.Select(Vector).ToArray());Set(c,"MosquitoSpawnPoints",r.mosquitoSpawnPoints.Select(Vector).ToArray());
            Set(c,"HumanPolygonFallZones",Polygons(type,"HumanPolygonFallZones",r.humanPolygonFallZones));
            Set(c,"MosquitoPolygonFallZones",Polygons(type,"MosquitoPolygonFallZones",r.mosquitoPolygonFallZones));
            Set(c,"RetryTicks",r.retryTicks);Set(c,"InteriorMargin",r.interiorMargin);Set(c,"SupportProbe",r.supportProbe);EditorUtility.SetDirty(c);
        }
        static PolygonInput[] ReadPolygons(object component,string field)
        {
            return ((Array)Field(component,field)).Cast<object>().Select(o=>new PolygonInput {id=(string)Field(o,"Id"),
                outer=((Vector2[])Field(o,"Outer")).Select(v=>new[]{v.x,v.y}).ToArray(),
                holes=((Array)Field(o,"Holes")).Cast<object>().Select(h=>new RingInput {vertices=((Vector2[])Field(h,"Vertices")).Select(v=>new[]{v.x,v.y}).ToArray()}).ToArray(),
                minY=(float)Field(o,"MinY"),maxY=(float)Field(o,"MaxY"),edgeTolerance=(float)Field(o,"EdgeTolerance")}).ToArray();
        }
        static float[][] ReadPoints(object c,string field)=>((Vector3[])Field(c,field)).Select(v=>new[]{v.x,v.y,v.z}).ToArray();
        static BoxInput[] ReadBoxes(object c,string field)=>((Bounds[])Field(c,field)).Select(b=>new BoxInput {min=new[]{b.min.x,b.min.y,b.min.z},max=new[]{b.max.x,b.max.y,b.max.z}}).ToArray();
        static void ValidateRecovery(GameObject root,MapInput m)
        {
            var type=RecoveryType();var components=root.GetComponents(type);Need(components.Length==1,"Exactly one root recovery component required.");var c=components[0];
            Need(c is Behaviour behaviour && behaviour.enabled,"Recovery opt-in disabled.");var bounds=(Bounds)Field(c,"SafetyBounds");
            Need(Near(bounds.min,Vector(m.innerMin))&&Near(bounds.max,Vector(m.innerMax)),"SafetyBounds differ from PlayBounds.");
            var actual=new RecoveryInput {regionsApproved=m.recovery.regionsApproved,
                humanFallZones=ReadBoxes(c,"HumanFallZones"),mosquitoFallZones=ReadBoxes(c,"MosquitoFallZones"),
                humanSpawnPoints=ReadPoints(c,"HumanSpawnPoints"),mosquitoSpawnPoints=ReadPoints(c,"MosquitoSpawnPoints"),
                humanPolygonFallZones=ReadPolygons(c,"HumanPolygonFallZones"),mosquitoPolygonFallZones=ReadPolygons(c,"MosquitoPolygonFallZones"),
                retryTicks=(int)Field(c,"RetryTicks"),interiorMargin=(float)Field(c,"InteriorMargin"),supportProbe=(float)Field(c,"SupportProbe")};
            Need(JsonConvert.SerializeObject(actual,Json)==JsonConvert.SerializeObject(m.recovery,Json),"Saved recovery region/config readback differs.");
            // Compile the same immutable settings used by BeginRound: this validates topology, holes and all limits.
            var settings=type.GetNestedType("Settings",BindingFlags.NonPublic);Need(settings!=null,"Recovery snapshot schema missing.");
            Activator.CreateInstance(settings,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{c},CultureInfo.InvariantCulture);
        }
        static string PathOf(Transform t,Transform root)
        { if(t==root)return ".";var parts=new List<string>();for(;t && t!=root;t=t.parent)parts.Add(t.name+"["+t.GetSiblingIndex()+"]");parts.Reverse();return string.Join("/",parts); }
        static string ContentHash(MapInput m,string revision)=>Hash(Encoding.UTF8.GetBytes(m.expectedContentHash+"\n"+revision+"\n"+string.Join(",",m.innerMin.Concat(m.innerMax).Concat(new[]{m.thickness,(float)m.layer,m.wallBottomY}).Select(x=>x.ToString("F9",CultureInfo.InvariantCulture)))+"\n"+JsonConvert.SerializeObject(m.recovery,Json)));
        static Vector3 Vector(float[] a){Need(a!=null && a.Length==3 && a.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),"Explicit finite vector required.");return new Vector3(a[0],a[1],a[2]);}
        static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<1e-10f;
        static bool Hex(string s,int length)=>s!=null && s.Length==length && s.All(Uri.IsHexDigit);
        static string Disk(string path){Need(path!=null && path.StartsWith("Assets/",StringComparison.Ordinal)&&!path.Contains("\\")&&!path.Contains(":")&&path.Split('/').All(p=>p!=""&&p!="."&&p!=".."),"Canonical asset path required.");return Path.GetFullPath(Path.Combine(Application.dataPath,"..",path));}
        static void Verify(Guard g){Need(g!=null,"Missing guard.");ExpectFile(g,g.sha256);}
        static void ExpectFile(Guard g,string hash){string file=Disk(g.path);Need(Hex(hash,64)&&Hex(g.guid,32)&&Hex(g.metaSha256,64)&&File.Exists(file)&&Hash(File.ReadAllBytes(file))==hash.ToLowerInvariant()&&AssetDatabase.AssetPathToGUID(g.path)==g.guid&&Hash(File.ReadAllBytes(file+".meta"))==g.metaSha256.ToLowerInvariant(),"Asset guard stale: "+g.path);}
        static string HashFile(string path)=>Hash(File.ReadAllBytes(Disk(path)));
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static void Need(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
        static void Write(FileStream stream,Receipt receipt){byte[] bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(receipt,Json));stream.Position=0;stream.SetLength(0);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
    }
}
