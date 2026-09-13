using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LetMeSleep.Content.Environment;
using Object = UnityEngine.Object;

// Isla v2 only. Coordinator-authorized plan authoring; no geometry or runtime edits.
public static class HiggsfieldIslaPreparation
{
    const string Root = "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-isla-del-laguito-v2";
    const string Prefab = Root + "/Prefabs/hf-isla-del-laguito-v2.prefab";
    const string PlanPath = Root + "/Data/isla-navigation-schema1.json";
    const float Cell = 1f;
    static readonly Vector3Int[] Steps = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };
    public sealed class Survey
    {
        public string mapId, status, prefabHash, navigationSha256, oldContentHash, newContentHash;
        public List<string> errors = new List<string>();
        public List<Probe> probes = new List<Probe>();
        public int queriedCells, selectedCells, portals;
    }
    public sealed class Probe { public string id, floor; public Vector3 position; public bool ground, capsuleFree; public string[] blockers; }
    public sealed class MeshSurvey { public string name; public Vector3[] vertices; public int[] triangles; }
    static Transform mapRoot;
    static Dictionary<Vector3Int, bool> free;
    static readonly System.Diagnostics.Stopwatch Watch = new System.Diagnostics.Stopwatch();
    public static string Run(string configPath, string output)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("EditMode only.");
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        if(config.prefabPath != Prefab || config.mapId != "hf-isla-del-laguito-v2") throw new Exception("Isla v2 only.");
        Directory.CreateDirectory(output);
        if(config.action == "apply-isla") return Apply(output);
        GameObject holder = null; var report = new Survey { mapId=config.mapId, prefabHash=AssetDatabase.GetAssetDependencyHash(Prefab).ToString() };
        Watch.Restart();
        try
        {
            holder = new GameObject("IslaSurvey_TEMP") { hideFlags=HideFlags.HideAndDontSave }; holder.SetActive(false);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), holder.transform, false);
            foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true)) if(script && !(script is EnvironmentMapDefinition)) Object.DestroyImmediate(script);
            foreach(var r in instance.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
            mapRoot=instance.transform; holder.SetActive(true); instance.SetActive(true); Physics.SyncTransforms();
            var map=instance.GetComponent<EnvironmentMapDefinition>(); report.oldContentHash=map.ContentHash;
            var meshes=instance.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.Contains("Path_Lookout")||m.name.Contains("Entry_Steps")||m.name.Contains("Wall_Front")||m.name.Contains("Lookout_Terrace")).Select(m=>new MeshSurvey{name=m.name,vertices=m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v)).ToArray(),triangles=m.sharedMesh.triangles}).ToArray();
            File.WriteAllText(Path.Combine(output,"measured-path-meshes.json"),HiggsfieldMapJson.Write(meshes));
            free=new Dictionary<Vector3Int,bool>(); var selected=new HashSet<Vector3Int>();
            var hub=At(new Vector3(0,6,0));
            if(!Free(hub)) throw new Exception("Air hub occupied.");
            foreach(var spawn in map.MosquitoSpawnPoints)
            {
                var start=At(spawn.position);
                if(!Free(start)) throw new Exception("Spawn cannot fit a free 1m cell: "+spawn.name+" at "+spawn.position);
                foreach(var cell in Find(start,hub)) selected.Add(cell);
            }
            // Extra measured air destinations connect walking landmarks and cabin interior.
            foreach(var point in new[]{new Vector3(0,3,-40),new Vector3(0,4,16),new Vector3(29.5f,5.5f,4.5f)})
            {
                var start=At(point);
                if(!Free(start)) { report.errors.Add("Optional landmark air cell occupied: "+point); continue; }
                try { foreach(var cell in Find(start,hub)) selected.Add(cell); }
                catch(Exception e) { report.errors.Add("Optional landmark "+point+": "+e.Message); }
            }
            var cells=selected.OrderBy(c=>c.x).ThenBy(c=>c.y).ThenBy(c=>c.z).ToArray();
            var plan=new HiggsfieldMapChecks.Plan {schema_version=1,map_id=config.mapId};
            plan.zones=cells.Select(c=>new HiggsfieldMapChecks.Zone{id=Id(c),min=Arr((Vector3)c*Cell),max=Arr((Vector3)(c+Vector3Int.one)*Cell)}).ToArray();
            var portals=new List<HiggsfieldMapChecks.Portal>();
            foreach(var c in cells) foreach(var step in new[]{Vector3Int.right,Vector3Int.up,new Vector3Int(0,0,1)})
            {
                var next=c+step; if(!selected.Contains(next)) continue;
                var center=((Vector3)c+Vector3.one*.5f+(Vector3)step*.5f)*Cell;
                portals.Add(new HiggsfieldMapChecks.Portal{id=Id(c)+"__"+Id(next),from=Id(c),to=Id(next),center=Arr(center),normal=Arr(step),width=Cell,height=Cell,door=false});
            }
            plan.portals=portals.ToArray(); report.queriedCells=free.Count; report.selectedCells=cells.Length; report.portals=portals.Count;
            if(cells.Length>1024) throw new Exception("Selected cell budget exceeded.");
            var json=HiggsfieldMapJson.Write(plan); report.navigationSha256=Hash(json); File.WriteAllText(Path.Combine(output,"isla-navigation-schema1.json"),json);
            var routes=new List<HiggsfieldMapChecks.Route>();
            routes.Add(Human("arrival-dock-return",0,Enumerable.Range(0,11).Select(i=>new Vector2(0,-19-i*2)).Concat(Enumerable.Range(0,10).Select(i=>new Vector2(0,-37+i*2))),report));
            routes.Add(Human("bridge-both-approaches",3,new[]{new Vector2(-6,16.6f),new Vector2(6,16.6f),new Vector2(.15f,16.6f)},report));
            routes.Add(Human("west-lake-to-bridge",2,new[]{new Vector2(-20.5f,0),new Vector2(-20,4),new Vector2(-18,8),new Vector2(-15,12),new Vector2(-10,15),new Vector2(-5.2f,16.6f),new Vector2(0,16.6f),new Vector2(5.2f,16.6f)},report));
            // Survey cabin doorway from source-derived local coordinates, preserving geometry.
            for(float x=-3; x<=3; x+=.25f) for(float z=-6;z<=-1;z+=.5f) ProbeFloor("cabin/"+x+"/"+z,Cabin(x,z),report);
            var cabinXZ=new[]{new Vector2(24,-3),Cabin(0,-6.5f),Cabin(0,-5.8f),Cabin(0,-5.1f),Cabin(0,-4.4f),Cabin(0,-3.7f),Cabin(-1,-3.6f),Cabin(-1,-2.4f),Cabin(-1,-1.3f)};
            routes.Add(Human("cabin-entry",1,cabinXZ,report));
            routes.Add(Human("lookout-trail",4,new[]{new Vector2(19,26),new Vector2(22,28),new Vector2(26,30),new Vector2(30,29),new Vector2(32,25),new Vector2(31,21),new Vector2(28,18),new Vector2(23,17),new Vector2(19,16)},report));
            foreach(int i in new[]{0,9,10,11,12,14,15})
            {
                var path=Find(At(map.MosquitoSpawnPoints[i].position),hub);
                // Grid cells are fully empty expanded by .056m. Use adjacent cell centers.
                routes.Add(new HiggsfieldMapChecks.Route{id="flight-spawn-"+(i+1)+"-to-lake",role="mosquito",spawnIndex=i,maxTicks=600,points=path.Where((c,j)=>j==0||j==path.Count-1||path[j]-path[j-1]!=path[j+1]-path[j]).Select(c=>((Vector3)c+Vector3.one*.5f)*Cell).ToArray()});
            }
            config.action=null; config.routes=routes.ToArray();
            File.WriteAllText(Path.Combine(output,"isla-v2.routes.json"),HiggsfieldMapJson.Write(config));
            report.status=report.errors.Count==0?"PREPARED_NOT_APPLIED":"PREPARED_WITH_NOTES";
        }
        catch(Exception e){report.status="FAIL";report.errors.Add(e.ToString());}
        finally{ if(holder) Object.DestroyImmediate(holder); Physics.SyncTransforms(); mapRoot=null; free=null; Watch.Stop(); }
        var pathOut=Path.Combine(output,"isla-preparation.json"); File.WriteAllText(pathOut,HiggsfieldMapJson.Write(report)); return pathOut;
    }
    static bool Free(Vector3Int c)
    {
        var center=((Vector3)c+Vector3.one*.5f)*Cell;
        if(center.x < -44 || center.x>44 || center.z < -44 || center.z>36 || center.y<2 || center.y>12) return false;
        if(free.TryGetValue(c,out var value)) return value;
        if(Watch.Elapsed.TotalSeconds>90)throw new TimeoutException("Preparation exceeded 90s cooperative budget.");
        value=!Physics.OverlapBox(center,Vector3.one*(Cell*.5f+.056f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(x=>x.transform.IsChildOf(mapRoot)); free[c]=value; return value;
    }
    static List<Vector3Int> Find(Vector3Int start,Vector3Int goal)
    {
        var open=new List<Vector3Int>{start}; var cost=new Dictionary<Vector3Int,int>{{start,0}}; var previous=new Dictionary<Vector3Int,Vector3Int>(); var closed=new HashSet<Vector3Int>();
        int H(Vector3Int c)=>Math.Abs(c.x-goal.x)+Math.Abs(c.y-goal.y)+Math.Abs(c.z-goal.z);
        while(open.Count>0)
        {
            var current=open.OrderBy(c=>cost[c]+H(c)).ThenBy(H).First();open.Remove(current);
            if(current==goal){var path=new List<Vector3Int>{current};while(previous.TryGetValue(current,out var p)){path.Add(p);current=p;}path.Reverse();return path;}
            closed.Add(current); if(closed.Count>30000)throw new Exception("Path search budget exceeded.");
            foreach(var step in Steps){var next=current+step;if(closed.Contains(next)||!Free(next))continue;int g=cost[current]+1;if(cost.TryGetValue(next,out int old)&&old<=g)continue;cost[next]=g;previous[next]=current;if(!open.Contains(next))open.Add(next);}
        }
        throw new Exception("No free cell path: "+start+" -> "+goal);
    }
    static Vector2 Cabin(float x,float z)=>new Vector2(29+.8020958f*x+.5971954f*z,4-.5971954f*x+.8020958f*z);
    static Probe ProbeFloor(string id,Vector2 xz,Survey report)
    {
        var row=new Probe{id=id,position=new Vector3(xz.x,0,xz.y)};
        var hits=Physics.RaycastAll(new Vector3(xz.x,14,xz.y),Vector3.down,20,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(mapRoot)&&h.normal.y>.55f && (h.collider.name.Contains("Path_")||h.collider.name.Contains("Terrain")||h.collider.name.Contains("Deck")||h.collider.name.Contains("Approach")||h.collider.name.Contains("Entry_Steps")||h.collider.name.Contains("Floor_and_Porch")||h.collider.name.Contains("Terrace"))).OrderByDescending(h=>h.point.y).ToArray();
        if(hits.Length>0){var h=hits[0];row.ground=true;row.floor=h.collider.name;row.position=h.point+Vector3.up*.003f;row.blockers=Physics.OverlapCapsule(row.position+Vector3.up*.251f,row.position+Vector3.up*1.469f,.249f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(mapRoot)).Select(c=>c.name).ToArray();row.capsuleFree=row.blockers.Length==0;}
        report.probes.Add(row);return row;
    }
    static HiggsfieldMapChecks.Route Human(string id,int spawn,IEnumerable<Vector2> xz,Survey report)=>new HiggsfieldMapChecks.Route{id=id,role="human",spawnIndex=spawn,maxTicks=600,points=xz.Select((p,i)=>ProbeFloor(id+"/"+i,p,report).position).ToArray()};
    static string Id(Vector3Int c)=>"air_"+c.x+"_"+c.y+"_"+c.z;
    static Vector3Int At(Vector3 p)=>Vector3Int.FloorToInt(p/Cell);
    static float[] Arr(Vector3 v)=>new[]{v.x,v.y,v.z};
    static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    static string Apply(string output)
    {
        string json=File.ReadAllText(Path.Combine(output,"isla-navigation-schema1.json"));
        var surveyPath=Path.Combine(output,"isla-preparation.json");var survey=Newtonsoft.Json.JsonConvert.DeserializeObject<Survey>(File.ReadAllText(surveyPath));
        var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(json);
        if(plan.map_id!="hf-isla-del-laguito-v2"||plan.schema_version!=1||Hash(json)!=survey.navigationSha256)throw new Exception("Plan identity/hash mismatch.");
        if(AssetDatabase.GetAssetDependencyHash(Prefab).ToString()!=survey.prefabHash)throw new Exception("Prefab changed since native preparation; regenerate instead of applying stale navigation.");
        File.WriteAllText(Path.Combine(Application.dataPath,PlanPath.Substring(7)),json);AssetDatabase.ImportAsset(PlanPath,ImportAssetOptions.ForceSynchronousImport);
        var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(PlanPath);var loaded=PrefabUtility.LoadPrefabContents(Prefab);
        try{var map=loaded.GetComponent<EnvironmentMapDefinition>();survey.oldContentHash=map.ContentHash;survey.newContentHash=Hash(map.ContentHash+"\nnav-schema1\n"+survey.navigationSha256);map.SpatialData=asset;map.ContentHash=survey.newContentHash;EditorUtility.SetDirty(map);PrefabUtility.SaveAsPrefabAsset(loaded,Prefab);}finally{PrefabUtility.UnloadPrefabContents(loaded);}
        var scene=EditorSceneManager.OpenScene(Root+"/Scenes/hf-isla-del-laguito-v2.unity",OpenSceneMode.Single);
        foreach(var root in scene.GetRootGameObjects())foreach(var map in root.GetComponentsInChildren<EnvironmentMapDefinition>(true))if(map.MapId==plan.map_id){map.SpatialData=asset;map.ContentHash=survey.newContentHash;EditorUtility.SetDirty(map);if(PrefabUtility.IsPartOfPrefabInstance(map))PrefabUtility.RecordPrefabInstancePropertyModifications(map);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        survey.status="APPLIED_ISLA_ONLY_GLOBAL_HASH_NOT_CERTIFIED";File.WriteAllText(Path.Combine(output,"isla-navigation-apply.json"),HiggsfieldMapJson.Write(survey));return Path.Combine(output,"isla-navigation-apply.json");
    }
}
