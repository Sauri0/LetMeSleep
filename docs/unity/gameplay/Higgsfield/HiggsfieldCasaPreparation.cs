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
using Object=UnityEngine.Object;

public static class HiggsfieldCasaPreparation
{
    const string Root="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1";
    const string Prefab=Root+"/Prefabs/hf-casa-del-patio-v1.prefab",PlanPath=Root+"/Data/casa-navigation-schema1.json";
    const float Cell=.6f;
    static readonly Vector3 Origin=new Vector3(-.7f,.05f,-.4f);
    static readonly Vector3Int[] Steps={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
    public sealed class Receipt
    {
        public string status,prefabHash,navSha256,oldContentHash,newContentHash;
        public List<string> pending=new List<string>();
        public List<Floor> floors=new List<Floor>();
        public int queriedCells,zones,portals;
        public Vector3 spawnBefore,spawnAfter;
        public string spawnSupportCollider;
    }
    public sealed class Floor { public string route,collider;public Vector3 point;public bool found; }
    static Transform root;
    static Dictionary<Vector3Int,bool> free;
    static System.Diagnostics.Stopwatch watch;
    public static string Run(string configPath,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("EditMode only.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        if(config.prefabPath!=Prefab||config.mapId!="hf-casa-del-patio-v1")throw new Exception("Casa v1 only.");
        Directory.CreateDirectory(output);if(config.action=="apply-casa" || config.action=="fix-casa-spawn")return Apply(output,config.action=="apply-casa");
        GameObject owner=null;var report=new Receipt{prefabHash=AssetDatabase.GetAssetDependencyHash(Prefab).ToString()};watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            owner=new GameObject("CasaSurvey_TEMP"){hideFlags=HideFlags.HideAndDontSave};owner.SetActive(false);
            var clone=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),owner.transform,false);
            foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))if(script && !(script is EnvironmentMapDefinition))Object.DestroyImmediate(script);
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            root=clone.transform;owner.SetActive(true);clone.SetActive(true);Physics.SyncTransforms();var map=clone.GetComponent<EnvironmentMapDefinition>();report.oldContentHash=map.ContentHash;
            var routes=new List<HiggsfieldMapChecks.Route>();
            routes.Add(Human("patio-entry-living",0,new[]{new Vector3(-.4f,2,-11.5f),new Vector3(-.4f,2,-8.5f),new Vector3(-.4f,2,-7.9f),new Vector3(-.4f,2,-7.3f),new Vector3(-.4f,2,-5.4f),new Vector3(-.4f,2,-4.5f),new Vector3(-.4f,2,-.25f),new Vector3(-2,2,-.25f),new Vector3(-2.5f,2,1.5f)},report));
            routes.Add(Human("hall-kitchen",1,new[]{new Vector3(.5f,2,-2.65f),new Vector3(.5f,2,-3),new Vector3(1.6f,2,-3),new Vector3(2.2f,2,-3),new Vector3(3.2f,2,-3)},report));
            var stepColliders=clone.GetComponentsInChildren<Collider>().Where(c=>System.Text.RegularExpressions.Regex.IsMatch(c.name,@"^CASA_Stair_Step_\d\d$")).OrderBy(c=>c.name).ToArray();
            if(stepColliders.Length!=16)throw new Exception("Expected exactly the 16 audited stair steps.");
            var steps=stepColliders.Select(c=>new Vector3(c.bounds.center.x,c.bounds.max.y+.003f,c.bounds.center.z)).ToArray();
            var top=FloorAt("stair-top",new Vector3(1.225f,5,3.7f),report);
            routes.Add(new HiggsfieldMapChecks.Route{id="only-stair-up",role="human",spawnIndex=1,maxTicks=600,points=new[]{FloorAt("stair-start",new Vector3(1.225f,2,-2),report)}.Concat(steps).Concat(new[]{top,FloorAt("upper-hall",new Vector3(-.65f,5,3.95f),report)}).ToArray()});
            routes.Add(new HiggsfieldMapChecks.Route{id="only-stair-down",role="human",spawnIndex=3,maxTicks=600,points=new[]{top}.Concat(steps.Reverse()).Concat(new[]{FloorAt("stair-bottom",new Vector3(1.225f,2,-2),report),FloorAt("hall-return",new Vector3(-.6f,2,-2.65f),report)}).ToArray()});
            routes.Add(Human("rear-patio-entry",4,new[]{new Vector3(3.5f,2,11.5f),new Vector3(-.4f,2,10),new Vector3(-.4f,2,8.5f),new Vector3(-.4f,2,7.8f),new Vector3(-.4f,2,7.2f),new Vector3(-.4f,2,5.5f),new Vector3(-.4f,2,4.2f)},report));
            routes.Add(new HiggsfieldMapChecks.Route{id="flight-stair-up",role="mosquito",spawnIndex=5,maxTicks=600,points=new[]{new Vector3(1.225f,1.4f,-2)}.Concat(steps.Select(p=>p+Vector3.up*.9f)).Concat(new[]{new Vector3(1.225f,4.2f,3.8f),map.MosquitoSpawnPoints[11].position}).ToArray()});
            routes.Add(new HiggsfieldMapChecks.Route{id="flight-stair-down",role="mosquito",spawnIndex=11,maxTicks=600,points=new[]{new Vector3(1.225f,4.2f,3.8f)}.Concat(steps.Reverse().Select(p=>p+Vector3.up*.9f)).Concat(new[]{new Vector3(1.225f,1.4f,-2),map.MosquitoSpawnPoints[5].position}).ToArray()});
            // Save measured routes even if conservative volume authoring cannot connect rooms.
            config.action=null;config.routes=routes.ToArray();File.WriteAllText(Path.Combine(output,"casa-v1.routes.json"),HiggsfieldMapJson.Write(config));
            free=new Dictionary<Vector3Int,bool>();var selected=new HashSet<Vector3Int>();var hub=At(new Vector3(-.4f,1.6f,-10));
            if(!Free(hub))throw new Exception("Patio hub occupied.");
            foreach(var spawn in map.MosquitoSpawnPoints)
            {
                var start=At(spawn.position);
                if(!Free(start))
                {
                    var alternatives=new List<Vector3Int>();
                    for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    {
                        var c=start+new Vector3Int(x,y,z);var min=Corner(c)-Vector3.one*.00001f;var max=Corner(c)+Vector3.one*(Cell+.00001f);var p=spawn.position;
                        if(p.x>=min.x&&p.x<=max.x&&p.y>=min.y&&p.y<=max.y&&p.z>=min.z&&p.z<=max.z&&Free(c))alternatives.Add(c);
                    }
                    if(alternatives.Count==0){report.pending.Add("Conservative containing cells occupied: "+spawn.name+" "+spawn.position);continue;}
                    start=alternatives[0];
                }
                try{foreach(var c in Find(start,hub))selected.Add(c);}catch(Exception e){report.pending.Add(spawn.name+": "+e.Message);}
            }
            report.queriedCells=free.Count;
            if(report.pending.Count>0){report.status="ROUTES_READY_NAV_INCOMPLETE";}
            else
            {
                var cells=selected.OrderBy(c=>c.x).ThenBy(c=>c.y).ThenBy(c=>c.z).ToArray();if(cells.Length>1024)throw new Exception("Navigation cell budget exceeded.");
                var plan=new HiggsfieldMapChecks.Plan{schema_version=1,map_id=config.mapId,zones=cells.Select(GrownZone).ToArray()};
                var portals=new List<HiggsfieldMapChecks.Portal>();
                foreach(var c in cells)foreach(var d in new[]{Vector3Int.right,Vector3Int.up,new Vector3Int(0,0,1)})if(selected.Contains(c+d))portals.Add(new HiggsfieldMapChecks.Portal{id=Id(c)+"__"+Id(c+d),from=Id(c),to=Id(c+d),center=Arr(Corner(c)+Vector3.one*(Cell*.5f)+(Vector3)d*(Cell*.5f)),normal=Arr(d),width=Cell,height=Cell});
                plan.portals=portals.ToArray();string json=HiggsfieldMapJson.Write(plan);report.navSha256=Hash(json);report.zones=cells.Length;report.portals=portals.Count;File.WriteAllText(Path.Combine(output,"casa-navigation-schema1.json"),json);report.status="PREPARED_NOT_APPLIED";
            }
        }
        catch(Exception e){report.pending.Add(e.ToString());report.status="INCOMPLETE";}
        finally{if(owner)Object.DestroyImmediate(owner);Physics.SyncTransforms();root=null;free=null;watch.Stop();}
        string path=Path.Combine(output,"casa-preparation.json");File.WriteAllText(path,HiggsfieldMapJson.Write(report));return path;
    }
    static Vector3 FloorAt(string id,Vector3 probe,Receipt report)
    {
        var hits=Physics.RaycastAll(probe,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(root)&&h.normal.y>.55f&&(h.collider.name.StartsWith("CASA_Floor")||h.collider.name.StartsWith("CASA_Porch")||h.collider.name.StartsWith("CASA_Path")||h.collider.name.StartsWith("CASA_Terrain")||h.collider.name.Contains("TileFloor"))).OrderBy(h=>h.distance).ToArray();
        var row=new Floor{route=id,found=hits.Length>0,point=probe};if(row.found){row.collider=hits[0].collider.name;row.point=hits[0].point+Vector3.up*.003f;}report.floors.Add(row);if(!row.found)throw new Exception("No floor at measured route "+id+" "+probe);return row.point;
    }
    static HiggsfieldMapChecks.Route Human(string id,int spawn,IEnumerable<Vector3> probes,Receipt report)=>new HiggsfieldMapChecks.Route{id=id,role="human",spawnIndex=spawn,maxTicks=600,points=probes.Select((p,i)=>FloorAt(id+"/"+i,p,report)).ToArray()};
    static Vector3Int At(Vector3 p)=>Vector3Int.FloorToInt((p-Origin)/Cell);
    static HiggsfieldMapChecks.Zone GrownZone(Vector3Int c)
    {
        var min=Corner(c);var max=min+Vector3.one*Cell;
        // Extra stopping margin for the existing .55m passage points; only grow
        // a face when the entire enlarged box plus mosquito radius remains free.
        foreach(var d in Steps)
        {
            var a=min;var b=max;
            for(int k=0;k<3;k++){if(d[k]<0)a[k]-=.15f;if(d[k]>0)b[k]+=.15f;}
            if(!Physics.OverlapBox((a+b)*.5f,(b-a)*.5f+Vector3.one*.056f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(x=>x.transform.IsChildOf(root))){min=a;max=b;}
        }
        return new HiggsfieldMapChecks.Zone{id=Id(c),min=Arr(min),max=Arr(max)};
    }
    static Vector3 Corner(Vector3Int c)=>Origin+(Vector3)c*Cell;
    static string Id(Vector3Int c)=>"casa_air_"+c.x+"_"+c.y+"_"+c.z;
    static float[] Arr(Vector3 v)=>new[]{v.x,v.y,v.z};
    static bool Free(Vector3Int c)
    {
        var p=Corner(c)+Vector3.one*(Cell*.5f);if(p.x< -22||p.x>22||p.z< -19||p.z>19||p.y<.3f||p.y>7)return false;
        if(free.TryGetValue(c,out var b))return b;if(watch.Elapsed.TotalSeconds>90)throw new TimeoutException("90s preparation budget reached.");
        b=!Physics.OverlapBox(p,Vector3.one*(Cell*.5f+.056f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(x=>x.transform.IsChildOf(root));free[c]=b;return b;
    }
    static List<Vector3Int> Find(Vector3Int start,Vector3Int goal)
    {
        var open=new List<Vector3Int>{start};var g=new Dictionary<Vector3Int,int>{{start,0}};var prev=new Dictionary<Vector3Int,Vector3Int>();var closed=new HashSet<Vector3Int>();int H(Vector3Int c)=>Math.Abs(c.x-goal.x)+Math.Abs(c.y-goal.y)+Math.Abs(c.z-goal.z);
        while(open.Count>0){var c=open.OrderBy(x=>g[x]+H(x)).ThenBy(H).First();open.Remove(c);if(c==goal){var path=new List<Vector3Int>{c};while(prev.TryGetValue(c,out var p)){path.Add(p);c=p;}path.Reverse();return path;}closed.Add(c);if(closed.Count>15000)throw new Exception("Bounded search did not connect room.");foreach(var d in Steps){var n=c+d;if(closed.Contains(n)||!Free(n))continue;int v=g[c]+1;if(g.TryGetValue(n,out int old)&&old<=v)continue;g[n]=v;prev[n]=c;if(!open.Contains(n))open.Add(n);}}throw new Exception("No free volume route to patio hub.");
    }
    static string Hash(string s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
    static string Apply(string output,bool applyNavigation)
    {
        var report=Newtonsoft.Json.JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(Path.Combine(output,"casa-preparation.json")));var json=File.ReadAllText(Path.Combine(output,"casa-navigation-schema1.json"));
        if(report.status!="PREPARED_NOT_APPLIED"||report.pending.Count>0||Hash(json)!=report.navSha256||AssetDatabase.GetAssetDependencyHash(Prefab).ToString()!=report.prefabHash)throw new Exception("Preparation stale or incomplete.");
        TextAsset asset=null;
        if(applyNavigation){File.WriteAllText(Path.Combine(Application.dataPath,PlanPath.Substring(7)),json);AssetDatabase.ImportAsset(PlanPath,ImportAssetOptions.ForceSynchronousImport);asset=AssetDatabase.LoadAssetAtPath<TextAsset>(PlanPath);}
        var loaded=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var map=loaded.GetComponent<EnvironmentMapDefinition>();var spawn=map.HumanSpawnPoints[2];
            if(spawn.name!="Spawn_Human_03.001" || Mathf.Abs(spawn.position.y-.25f)>.0001f)throw new Exception("Unexpected spawn revision; do not apply twice.");
            report.spawnBefore=spawn.position;report.spawnAfter=new Vector3(spawn.position.x,.2841115f,spawn.position.z);report.spawnSupportCollider="Environment/CASA_Floorboard_GF_10_03";
            spawn.position=report.spawnAfter;EditorUtility.SetDirty(spawn);report.oldContentHash=map.ContentHash;
            var basis=applyNavigation?Hash(map.ContentHash+"\nnav-schema1\n"+report.navSha256):map.ContentHash;
            report.newContentHash=Hash(basis+"\nspawn-human03-support-plus-0.01\n0.2841115");
            if(applyNavigation)map.SpatialData=asset;map.ContentHash=report.newContentHash;EditorUtility.SetDirty(map);PrefabUtility.SaveAsPrefabAsset(loaded,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(loaded);}
        var scene=EditorSceneManager.OpenScene(Root+"/Scenes/hf-casa-del-patio-v1.unity",OpenSceneMode.Single);
        foreach(var go in scene.GetRootGameObjects())foreach(var map in go.GetComponentsInChildren<EnvironmentMapDefinition>(true))if(map.MapId=="hf-casa-del-patio-v1")
        {
            if(applyNavigation)map.SpatialData=asset;map.ContentHash=report.newContentHash;map.HumanSpawnPoints[2].position=report.spawnAfter;
            EditorUtility.SetDirty(map);EditorUtility.SetDirty(map.HumanSpawnPoints[2]);
            if(PrefabUtility.IsPartOfPrefabInstance(map)){PrefabUtility.RecordPrefabInstancePropertyModifications(map);PrefabUtility.RecordPrefabInstancePropertyModifications(map.HumanSpawnPoints[2]);}
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();report.status=(applyNavigation?"NAV_AND_SPAWN":"SPAWN_ONLY")+"_APPLIED_GLOBAL_HASH_NOT_CERTIFIED";string path=Path.Combine(output,"casa-apply.json");File.WriteAllText(path,HiggsfieldMapJson.Write(report));return path;
    }
}
