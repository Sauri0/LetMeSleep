using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Clone-only floor support measurement for the coordinator's final two imports.
public static class HiggsfieldRemainingMapsPreparation
{
    public sealed class Receipt
    {
        public string status="INCOMPLETE",mapId,contentHash,sourceSha256,checksPath;
        public int colliders,humanPool,mosquitoPool;
        public List<Floor> floors=new List<Floor>();
        public List<string> errors=new List<string>();
        public bool cleanup;
    }
    public sealed class Floor{public string route,collider;public int point;public Vector3 probe,support,normal,feetTarget;public bool sphereSupport;}
    public static string Run(string configPath,string output)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,"Assigned idle Unity required.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        string expected=config.mapId=="hf-yate-a-la-deriva-v3"?"217774581132711b6197a6115954e6e63acd3fdbf42b2705c0c115e6bc146870":config.mapId=="hf-puerto-del-faro-v1"?"aa4bb192353bfbebe5f3d48e4dd6950245b854f397b95a7fb7d7ac85a2a834a4":null;
        Require(expected!=null && config.prefabPath=="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/"+config.mapId+"/Prefabs/"+config.mapId+".prefab","Final Yate v3 / Puerto v1 only.");
        Require(config.routes!=null && config.routes.Length<=32,"Bounded route list required.");
        Directory.CreateDirectory(output);var receipt=new Receipt{mapId=config.mapId,sourceSha256=expected};GameObject owner=null;var watch=Stopwatch.StartNew();
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);Require(prefab,"Final prefab missing.");
            owner=new GameObject("RemainingMapFloorSurvey_TEMP"){hideFlags=HideFlags.HideAndDontSave};owner.SetActive(false);
            var clone=Object.Instantiate(prefab,owner.transform,false);var map=clone.GetComponent<EnvironmentMapDefinition>();
            Require(map && map.MapId==config.mapId && map.SpatialData,"Definition with source recipe required.");
            Require((string)Newtonsoft.Json.Linq.JObject.Parse(map.SpatialData.text)["sourceSha256"]==expected,"Source recipe changed; re-audit.");
            Require(clone.transform.localPosition==Vector3.zero && clone.transform.localScale==Vector3.one && Quaternion.Angle(clone.transform.localRotation,Quaternion.identity)<.001f,"Identity root required.");
            foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))if(script && !(script is EnvironmentMapDefinition))Object.DestroyImmediate(script);
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            Require(clone.GetComponentsInChildren<Rigidbody>(true).Length==0,"Static fixture only.");
            owner.SetActive(true);clone.SetActive(true);Physics.SyncTransforms();
            receipt.contentHash=map.ContentHash;receipt.humanPool=map.HumanSpawnPoints.Length;receipt.mosquitoPool=map.MosquitoSpawnPoints.Length;receipt.colliders=clone.GetComponentsInChildren<Collider>(false).Count(c=>c.enabled && !c.isTrigger);
            Require(receipt.humanPool==5 && receipt.mosquitoPool==16,"Expected authored 5/16 pools.");
            foreach(var route in config.routes.Where(r=>r.role=="human"))
            {
                Require(route.points!=null && route.points.Length>0 && route.points.Length<=64,"Bounded human probes required.");
                for(int i=0;i<route.points.Length;i++)
                {
                    Require(watch.Elapsed.TotalSeconds<45,"Floor survey exceeded 45s.");
                    var probe=map.transform.TransformPoint(route.points[i]);
                    var raw=route.sphereSupport?Physics.SphereCastAll(probe+Vector3.up*.25f,.25f,Vector3.down,12,~0,QueryTriggerInteraction.Ignore):Physics.RaycastAll(probe,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
                    var hits=raw.Where(h=>h.collider.transform.IsChildOf(clone.transform) && h.normal.y>.55f && IsSupport(config.mapId,h.collider.name)).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length==0){receipt.errors.Add("No eligible floor: "+route.id+"/"+i+" "+probe);continue;}
                    var hit=hits[0];var feet=route.sphereSupport?probe+Vector3.down*hit.distance+Vector3.up*.003f:hit.point+Vector3.up*.003f;
                    receipt.floors.Add(new Floor{route=route.id,point=i,probe=probe,support=hit.point,normal=hit.normal,collider=hit.collider.name,feetTarget=feet,sphereSupport=route.sphereSupport});
                    route.points[i]=map.transform.InverseTransformPoint(feet);
                }
            }
            var navigation=File.ReadAllText(config.navigationOverridePath);var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(navigation);
            Require(plan.schema_version==1 && plan.map_id==config.mapId,"Schema1 candidate identity mismatch.");
            string navPath=Path.Combine(output,"navigation-candidate.json");File.WriteAllText(navPath,navigation);config.navigationOverridePath=navPath;config.action=null;
            if(receipt.errors.Count==0){receipt.checksPath=Path.Combine(output,"measured-checks.json");File.WriteAllText(receipt.checksPath,HiggsfieldMapJson.Write(config));receipt.status="PREPARED_NATIVE_VALIDATION_PENDING";}
            else receipt.status="FLOOR_SURVEY_FAILED";
        }
        catch(Exception error){receipt.status="FAIL";receipt.errors.Add(error.ToString());}
        finally{if(owner)Object.DestroyImmediate(owner);Physics.SyncTransforms();receipt.cleanup=!owner;}
        string result=Path.Combine(output,"native-preparation.json");File.WriteAllText(result,HiggsfieldMapJson.Write(receipt));return result;
    }
    static bool IsSupport(string map,string name)
    {
        if(map=="hf-yate-a-la-deriva-v3")return name=="YATE_MainDeck_Continuous" || name=="YATE_LowerCabinFloor" || name=="YATE_Roof_Removable_FlybridgeDeck" || name=="YATE_SwimPlatform" || name.StartsWith("YATE_Stair_") || name=="YATE_Lower_EndBulkhead_-11.6";
        return name=="Terrain_Playable_110x85" || name=="Plaza_ContinuousPaving" || name.Contains("Floor") || name.Contains("Foundation") || name.Contains("Stair") || name.Contains("LandConnector") || name.Contains("Deck") || name.Contains("Threshold") || name=="Workshop_QuaysidePorch_Support";
    }
    static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
}
