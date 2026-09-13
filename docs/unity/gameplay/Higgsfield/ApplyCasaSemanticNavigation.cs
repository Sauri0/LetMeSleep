using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LetMeSleep.Content.Environment;

// Explicitly authorized semantic-data persistence only; no generation, spawn or material repair.
public static class ApplyCasaSemanticNavigation
{
    const string MapId="hf-casa-del-patio-v1",Root="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1";
    const string Prefab=Root+"/Prefabs/hf-casa-del-patio-v1.prefab",Scene=Root+"/Scenes/hf-casa-del-patio-v1.unity",Data=Root+"/Data/casa-navigation-schema1.json";
    public sealed class Receipt
    {
        public string status="INCOMPLETE",oldContentHash,newContentHash,navSha256,navigationGuid,validatedReport,prefabGeometryBefore,prefabGeometryAfter,sceneGeometryBefore,sceneGeometryAfter;
        public string hashDerivation="SHA256(oldContentHash + newline + nav-schema1 + newline + navSha256); local content revision, global catalog not certified";
        public string[] zoneIds,portalIds;
        public string stairId;
        public Vector3 spawnBefore,spawnAfter;
        public bool prefabReadback,sceneReadback;
    }
    public static string Run(string configPath,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)throw new Exception("Idle EditMode only.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        if(config.mapId!=MapId || config.prefabPath!=Prefab)throw new Exception("CASA v1 only.");
        var json=File.ReadAllText(config.navigationOverridePath);string hash=Hash(json);
        var validation=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Report>(File.ReadAllText(config.validationReportPath));
        if(validation.status!="PASS_SCOPED" || validation.mapId!=MapId || validation.navigationSha256!=hash || validation.cases.Count!=44 || validation.cases.Any(c=>c.status!="PASS") || validation.passages.Count!=17 || validation.passages.Any(p=>p.status!="PASS_STATIC_CLEARANCE"))throw new Exception("Require exact44/44validated semantic data and17passages.");
        var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(json);
        if(plan.schema_version!=1 || plan.map_id!=MapId || plan.zones.Length!=16 || plan.portals.Length!=16 || plan.stair?.id!="straight_stair")throw new Exception("Unexpected semantic plan.");
        Directory.CreateDirectory(output);
        var receipt=new Receipt{validatedReport=config.validationReportPath,navSha256=hash,oldContentHash=validation.contentHash,zoneIds=plan.zones.Select(z=>z.id).ToArray(),portalIds=plan.portals.Select(p=>p.id).ToArray(),stairId=plan.stair.id};
        receipt.newContentHash=Hash(receipt.oldContentHash+"\nnav-schema1\n"+hash);
        // Preflight both persisted objects before writing the TextAsset or changing either map.
        var loaded=PrefabUtility.LoadPrefabContents(Prefab);
        var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        var sceneMap=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==MapId);
        try
        {
            var map=loaded.GetComponent<EnvironmentMapDefinition>();
            if(map.ContentHash!=receipt.oldContentHash || sceneMap.ContentHash!=receipt.oldContentHash)throw new Exception("Validated content revision changed; do not overwrite coordinator changes.");
            receipt.spawnBefore=map.HumanSpawnPoints[2].position;
            if(Vector3.Distance(receipt.spawnBefore,new Vector3(-2.5f,.2841115f,2.1f))>.0001f || Vector3.Distance(sceneMap.HumanSpawnPoints[2].position,receipt.spawnBefore)>.0001f)throw new Exception("Corrected spawn must be preserved.");
            receipt.prefabGeometryBefore=GeometryStamp(map);receipt.sceneGeometryBefore=GeometryStamp(sceneMap);
            string absolute=Path.Combine(Application.dataPath,Data.Substring(7));
            if(File.Exists(absolute) && Hash(File.ReadAllText(absolute))!=hash)throw new Exception("New navigation path already contains different data; inspect first.");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));File.WriteAllText(absolute,json,new UTF8Encoding(false));AssetDatabase.ImportAsset(Data,ImportAssetOptions.ForceSynchronousImport);
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Data);if(!asset || Hash(asset.text)!=hash)throw new Exception("Imported TextAsset mismatch.");receipt.navigationGuid=AssetDatabase.AssetPathToGUID(Data);
            map.SpatialData=asset;map.ContentHash=receipt.newContentHash;EditorUtility.SetDirty(map);PrefabUtility.SaveAsPrefabAsset(loaded,Prefab);
            sceneMap.SpatialData=asset;sceneMap.ContentHash=receipt.newContentHash;EditorUtility.SetDirty(sceneMap);
            if(PrefabUtility.IsPartOfPrefabInstance(sceneMap))PrefabUtility.RecordPrefabInstancePropertyModifications(sceneMap);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(loaded);}
        loaded=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var map=loaded.GetComponent<EnvironmentMapDefinition>();receipt.prefabGeometryAfter=GeometryStamp(map);receipt.spawnAfter=map.HumanSpawnPoints[2].position;
            receipt.prefabReadback=map.MapId==MapId && map.ContentHash==receipt.newContentHash && map.SpatialData && Hash(map.SpatialData.text)==hash && AssetDatabase.GetAssetPath(map.SpatialData)==Data;
        }
        finally{PrefabUtility.UnloadPrefabContents(loaded);}
        scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        sceneMap=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==MapId);
        receipt.sceneGeometryAfter=GeometryStamp(sceneMap);
        receipt.sceneReadback=sceneMap.ContentHash==receipt.newContentHash && sceneMap.SpatialData && Hash(sceneMap.SpatialData.text)==hash && AssetDatabase.GetAssetPath(sceneMap.SpatialData)==Data;
        receipt.status=receipt.prefabReadback && receipt.sceneReadback && receipt.prefabGeometryBefore==receipt.prefabGeometryAfter && receipt.sceneGeometryBefore==receipt.sceneGeometryAfter && receipt.spawnBefore==receipt.spawnAfter ? "APPLIED_READBACK_PASS" : "READBACK_FAIL";
        string result=Path.Combine(output,"casa-semantic-apply.json");File.WriteAllText(result,HiggsfieldMapJson.Write(receipt));return result;
    }
    static string GeometryStamp(EnvironmentMapDefinition map)
    {
        var rows=map.GetComponentsInChildren<Transform>(true).Select(t=>PathOf(t,map.transform)+"|"+t.localPosition.ToString("F6")+"|"+t.localRotation.ToString("F6")+"|"+t.localScale.ToString("F6")+"|"+t.gameObject.activeSelf).ToList();
        foreach(var r in map.GetComponentsInChildren<Renderer>(true))rows.Add("renderer|"+PathOf(r.transform,map.transform)+"|"+r.enabled+"|"+string.Join(",",r.sharedMaterials.Select(AssetStamp)));
        foreach(var c in map.GetComponentsInChildren<Collider>(true))rows.Add("collider|"+PathOf(c.transform,map.transform)+"|"+c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+c.contactOffset+"|"+AssetStamp(c.sharedMaterial)+(c is MeshCollider mc?"|"+mc.convex+"|"+mc.cookingOptions+"|"+AssetStamp(mc.sharedMesh):c is BoxCollider bc?"|"+bc.center.ToString("F6")+"|"+bc.size.ToString("F6"):""));
        return Hash(string.Join("\n",rows.OrderBy(s=>s,StringComparer.Ordinal)));
    }
    static string AssetStamp(UnityEngine.Object obj)
    {
        if(!obj)return "null";string path=AssetDatabase.GetAssetPath(obj);
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long local))throw new Exception("Unpersisted geometry/material reference.");
        return guid+":"+local+":"+(string.IsNullOrEmpty(path)?"":AssetDatabase.GetAssetDependencyHash(path).ToString());
    }
    static string PathOf(Transform t,Transform root){if(t==root)return ".";string path=t.name;while(t.parent && t.parent!=root){t=t.parent;path=t.name+"/"+path;}return path;}
    static string Hash(string s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
}
