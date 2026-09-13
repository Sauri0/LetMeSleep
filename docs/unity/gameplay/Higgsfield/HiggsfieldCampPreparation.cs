using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using LetMeSleep.Content.Environment;
using Object=UnityEngine.Object;

// Measure human route support on an owned clone of the repaired v2 import.
// Generates external diagnostics only; no save/prefab/scene/marker modifications.
public static class HiggsfieldCampPreparation
{
    const string Id="hf-campamento-pinar-v2";
    const string SourceSha="3fd036eb8ede448aa48eb72764ae9f143dbf6ecff375e1ae10d144dfb44badc3";
    const string Prefab="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-campamento-pinar-v2/Prefabs/hf-campamento-pinar-v2.prefab";
    public sealed class Receipt
    {
        public string status="INCOMPLETE",mapId,contentHash,prefabDependencyHash,navigationSha256,sourceSha256=SourceSha,checksPath;
        public string scope="V2 clone-only floor survey; physical traversal and semantic runtime validation pending.";
        public int humanPool,mosquitoPool,colliders;
        public List<Floor> floors=new List<Floor>();
        public List<Anchor> lightAnchors=new List<Anchor>();
        public CampTechnicalCandidate.SpawnReceipt spawn05;
        public List<string> errors=new List<string>();
        public bool cleanup;
    }
    public sealed class Floor{public string route,collider;public int pointIndex;public Vector3 probe,support,normal;public bool found;}
    public sealed class Anchor{public string name;public Vector3 position,forward;}
    public static string Run(string configPath,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)throw new Exception("Idle assigned Unity slot required.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        if(config.mapId!=Id || config.prefabPath!=Prefab || config.routes==null || config.routes.Length>32)throw new Exception("Bounded Campamento v2 config required.");
        Directory.CreateDirectory(output);var receipt=new Receipt();GameObject owner=null;var watch=Stopwatch.StartNew();
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(!prefab)throw new Exception("V2 prefab not imported yet.");
            receipt.prefabDependencyHash=AssetDatabase.GetAssetDependencyHash(Prefab).ToString();
            owner=new GameObject("CampFloorSurvey_TEMP"){hideFlags=HideFlags.HideAndDontSave};owner.SetActive(false);
            var clone=Object.Instantiate(prefab,owner.transform,false);var map=clone.GetComponent<EnvironmentMapDefinition>();
            if(!map || map.MapId!=Id || !map.SpatialData)throw new Exception("Expected v2 definition with source recipe.");
            var recipe=Newtonsoft.Json.Linq.JObject.Parse(map.SpatialData.text);
            if((string)recipe["sourceSha256"]!=SourceSha)throw new Exception("Imported source differs from repaired v2 recipe; re-audit before surveying.");
            if(clone.transform.localPosition!=Vector3.zero || clone.transform.localScale!=Vector3.one || Quaternion.Angle(clone.transform.localRotation,Quaternion.identity)>.001f)throw new Exception("Identity map root required.");
            foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))if(script && !(script is EnvironmentMapDefinition))Object.DestroyImmediate(script);
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            if(clone.GetComponentsInChildren<Rigidbody>(true).Length!=0)throw new Exception("Unexpected dynamic geometry.");
            owner.SetActive(true);clone.SetActive(true);Physics.SyncTransforms();
            receipt.mapId=map.MapId;receipt.contentHash=map.ContentHash;receipt.humanPool=map.HumanSpawnPoints.Length;receipt.mosquitoPool=map.MosquitoSpawnPoints.Length;
            receipt.colliders=clone.GetComponentsInChildren<Collider>(false).Count(c=>c.enabled && !c.isTrigger);
            if(receipt.humanPool!=5 || receipt.mosquitoPool!=16)throw new Exception("Expected exact5/16authored pools.");
            foreach(var anchor in clone.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("LGT_")))
                receipt.lightAnchors.Add(new Anchor{name=anchor.name,position=anchor.position,forward=anchor.forward});
            receipt.spawn05=CampTechnicalCandidate.SurveySpawn(map,false);
            foreach(var route in config.routes.Where(r=>r.role=="human"))
            {
                if(route.points==null || route.points.Length==0 || route.points.Length>64)throw new Exception("Human route requires bounded probe points.");
                for(int i=0;i<route.points.Length;i++)
                {
                    if(watch.Elapsed.TotalSeconds>45)throw new TimeoutException("45s floor-survey budget exceeded.");
                    var probe=map.transform.TransformPoint(route.points[i]);var row=new Floor{route=route.id,pointIndex=i,probe=probe};receipt.floors.Add(row);
                    var hits=Physics.RaycastAll(probe,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.collider.transform.IsChildOf(clone.transform) && h.normal.y>.55f && IsSupport(h.collider.name)).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length==0){receipt.errors.Add("No eligible upward support at "+route.id+"/"+i+" "+probe);continue;}
                    var hit=hits[0];row.found=true;row.collider=hit.collider.name;row.support=hit.point;row.normal=hit.normal;
                    route.points[i]=map.transform.InverseTransformPoint(hit.point+Vector3.up*.003f);
                }
            }
            var nav=File.ReadAllText(config.navigationOverridePath);receipt.navigationSha256=Hash(nav);
            var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(nav);
            if(plan.schema_version!=1 || plan.map_id!=Id)throw new Exception("Semantic candidate must matchv2.");
            string navPath=Path.Combine(output,"camp-v2.semantic-navigation.json");File.WriteAllText(navPath,nav);
            config.navigationOverridePath=navPath;config.action=null;
            if(receipt.errors.Count==0){receipt.checksPath=Path.Combine(output,"camp-v2.measured-checks.json");File.WriteAllText(receipt.checksPath,HiggsfieldMapJson.Write(config));receipt.status="PREPARED_NATIVE_VALIDATION_PENDING";}
            else receipt.status="FLOOR_SURVEY_FAILED";
        }
        catch(Exception e){receipt.status="FAIL";receipt.errors.Add(e.ToString());}
        finally{if(owner)Object.DestroyImmediate(owner);Physics.SyncTransforms();receipt.cleanup=!owner;watch.Stop();}
        string result=Path.Combine(output,"camp-v2-preparation.json");File.WriteAllText(result,HiggsfieldMapJson.Write(receipt));return result;
    }
    public static bool IsSupport(string name)=>name.StartsWith("CAMP_Terrain_") || name.StartsWith("CAMP_Hill_") || name=="CAMP_Washroom_Floor" || name=="CAMP_Washroom_ThresholdRamp" || name.StartsWith("CAMP_Bridge_") && (name.EndsWith("_DeckSupport") || name.EndsWith("_Ramp1") || name.EndsWith("_Ramp2"));
    static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
}
