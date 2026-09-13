using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Coordinator explicitly accepted the recorded .32617534mm excess on 2026-09-13.
// This operation does NOT change the 2mm contract or label the strict suite PASS.
public static class ApplyCampSemanticNavigation
{
    const string Id="hf-campamento-pinar-v2",Root="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-campamento-pinar-v2";
    const string Prefab=Root+"/Prefabs/hf-campamento-pinar-v2.prefab",Scene=Root+"/Scenes/hf-campamento-pinar-v2.unity";
    const string NavPath=Root+"/Data/camp-navigation-schema1.json",MeshPath=Root+"/Data/camp-paths-closed-v1.asset",ProvenancePath=Root+"/Data/camp-technical-revision-v1.json";
    public sealed class Receipt
    {
        public string status="INCOMPLETE",strictValidationStatus="FAIL_48_OF_49",decision="Coordinator accepted one measured .32617534mm excess; 2mm contract unchanged. No claim of visual jitter, rendering or performance validation.";
        public string oldContentHash,newContentHash,navSha256,navigationGuid,collisionGuid,validatedReport,originalSpatialData,originalSpatialDataGuid,originalSpatialDataSha256;
        public string prefabUnchangedScopeBefore,prefabUnchangedScopeAfter,sceneUnchangedScopeBefore,sceneUnchangedScopeAfter;
        public string hashDerivation="SHA256(oldContentHash + newline + nav-schema1 + newline + navSha256 + newline + colliderRevision + newline + candidateMeshSha256 + newline + spawn05 + newline + newPositionF9Invariant)";
        public CampTechnicalCandidate.Receipt collider;
        public CampTechnicalCandidate.SpawnReceipt spawn;
        public bool prefabReadback,sceneReadback;
    }
    public static string Run(string configPath,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)throw new Exception("Idle assigned EditMode slot required.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        Require(config.mapId==Id && config.prefabPath==Prefab && config.action=="apply-camp-recorded-exception","Explicit accepted-exception Camp operation required.");
        var validation=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Report>(File.ReadAllText(config.validationReportPath));
        var json=File.ReadAllText(config.navigationOverridePath);string navHash=Hash(json);
        Require(validation.mapId==Id && validation.status=="FAIL" && validation.errors.Count==0 && validation.pending.Count==0 && validation.cleanup && validation.navigationSha256==navHash,"Require exact scoped candidate validation.");
        var failures=validation.cases.Where(c=>c.status!="PASS").ToArray();
        Require(validation.cases.Count==49 && failures.Length==1 && failures[0].id=="route/tent-cluster-north-circulation" && failures[0].reached==6 && failures[0].requested==6 && failures[0].finalPenetration==0 && Mathf.Abs(failures[0].maxPenetration-.00232617534f)<.00000001f,"Only the measured, explicitly accepted single exception is eligible.");
        Require(validation.passages.Count==25 && validation.passages.All(p=>p.status=="PASS_STATIC_CLEARANCE") && validation.campColliderCandidate!=null && validation.campSpawnCandidate?.appliedToClone==true,"Require all portals and exact technical candidates.");
        var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(json);
        Require(plan.map_id==Id && plan.schema_version==1 && plan.zones.Length==20 && plan.portals.Length==25,"Unexpected semantic plan.");
        var receipt=new Receipt{oldContentHash=validation.contentHash,navSha256=navHash,validatedReport=config.validationReportPath,spawn=validation.campSpawnCandidate};
        receipt.newContentHash=Hash(receipt.oldContentHash+"\nnav-schema1\n"+navHash+"\n"+validation.campColliderCandidate.revision+"\n"+validation.campColliderCandidate.candidateMeshSha256+"\nspawn05\n"+VectorStamp(receipt.spawn.after));
        Directory.CreateDirectory(output);GameObject loaded=null;var owned=new List<Object>();
        try
        {
            loaded=PrefabUtility.LoadPrefabContents(Prefab);var map=loaded.GetComponent<EnvironmentMapDefinition>();
            var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);var sceneMap=FindMap(scene.GetRootGameObjects());
            Require(map.ContentHash==receipt.oldContentHash && sceneMap.ContentHash==receipt.oldContentHash,"Validated content changed; do not overwrite.");
            Require(Vector3.Distance(map.HumanSpawnPoints[4].position,receipt.spawn.before)<.00001f && Vector3.Distance(sceneMap.HumanSpawnPoints[4].position,receipt.spawn.before)<.00001f,"Source Spawn05 changed.");
            var original=Paths(map).sharedMesh;
            Require(CampTechnicalCandidate.Hash(original)==validation.campColliderCandidate.originalMeshSha256 && CampTechnicalCandidate.Hash(Paths(sceneMap).sharedMesh)==validation.campColliderCandidate.originalMeshSha256,"Original collision source changed.");
            receipt.originalSpatialData=AssetDatabase.GetAssetPath(map.SpatialData);receipt.originalSpatialDataGuid=AssetDatabase.AssetPathToGUID(receipt.originalSpatialData);receipt.originalSpatialDataSha256=Hash(map.SpatialData.text);
            receipt.prefabUnchangedScopeBefore=Stamp(map);receipt.sceneUnchangedScopeBefore=Stamp(sceneMap);
            receipt.collider=CampTechnicalCandidate.ApplyPaths(loaded,owned);
            Require(receipt.collider.candidateMeshSha256==validation.campColliderCandidate.candidateMeshSha256,"Rebuilt candidate differs from validated mesh.");
            EnsureDataFolder();var generated=Paths(map).sharedMesh;var persisted=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if(persisted){Require(CampTechnicalCandidate.Hash(persisted)==receipt.collider.candidateMeshSha256,"Mesh destination already differs.");Paths(map).sharedMesh=persisted;}
            else{generated.name="CampPaths_ExactDownExtrusion_v1";AssetDatabase.CreateAsset(generated,MeshPath);owned.Remove(generated);persisted=generated;}
            WriteTextAsset(NavPath,json);var nav=AssetDatabase.LoadAssetAtPath<TextAsset>(NavPath);Require(nav && Hash(nav.text)==navHash,"Navigation import mismatch.");
            receipt.navigationGuid=AssetDatabase.AssetPathToGUID(NavPath);receipt.collisionGuid=AssetDatabase.AssetPathToGUID(MeshPath);
            Assign(map,nav,persisted,receipt);PrefabUtility.SaveAsPrefabAsset(loaded,Prefab);
            Assign(sceneMap,nav,persisted,receipt);
            foreach(var target in new Object[]{sceneMap,sceneMap.HumanSpawnPoints[4],Paths(sceneMap)})
                if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed.");AssetDatabase.SaveAssets();
            PrefabUtility.UnloadPrefabContents(loaded);loaded=null;
            loaded=PrefabUtility.LoadPrefabContents(Prefab);map=loaded.GetComponent<EnvironmentMapDefinition>();
            receipt.prefabReadback=Readback(map,receipt);receipt.prefabUnchangedScopeAfter=Stamp(map);
            PrefabUtility.UnloadPrefabContents(loaded);loaded=null;
            scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);sceneMap=FindMap(scene.GetRootGameObjects());
            receipt.sceneReadback=Readback(sceneMap,receipt);receipt.sceneUnchangedScopeAfter=Stamp(sceneMap);
            Require(receipt.prefabReadback && receipt.sceneReadback && receipt.prefabUnchangedScopeBefore==receipt.prefabUnchangedScopeAfter && receipt.sceneUnchangedScopeBefore==receipt.sceneUnchangedScopeAfter,"Readback or unrelated-content preservation failed.");
            receipt.status="APPLIED_READBACK_PASS_WITH_RECORDED_STRICT_EXCEPTION";
            WriteTextAsset(ProvenancePath,HiggsfieldMapJson.Write(receipt));AssetDatabase.SaveAssets();
        }
        finally
        {
            if(loaded)PrefabUtility.UnloadPrefabContents(loaded);
            foreach(var asset in owned)if(asset && !AssetDatabase.Contains(asset))Object.DestroyImmediate(asset);
            File.WriteAllText(Path.Combine(output,"camp-semantic-apply.json"),HiggsfieldMapJson.Write(receipt));
        }
        return Path.Combine(output,"camp-semantic-apply.json");
    }
    static void Assign(EnvironmentMapDefinition map,TextAsset nav,Mesh mesh,Receipt receipt)
    {map.SpatialData=nav;map.ContentHash=receipt.newContentHash;map.HumanSpawnPoints[4].position=receipt.spawn.after;Paths(map).sharedMesh=mesh;EditorUtility.SetDirty(map);EditorUtility.SetDirty(map.HumanSpawnPoints[4]);EditorUtility.SetDirty(Paths(map));}
    static bool Readback(EnvironmentMapDefinition map,Receipt receipt)=>map.MapId==Id && map.ContentHash==receipt.newContentHash && AssetDatabase.GetAssetPath(map.SpatialData)==NavPath && Hash(map.SpatialData.text)==receipt.navSha256 && AssetDatabase.GetAssetPath(Paths(map).sharedMesh)==MeshPath && CampTechnicalCandidate.Hash(Paths(map).sharedMesh)==receipt.collider.candidateMeshSha256 && Vector3.Distance(map.HumanSpawnPoints[4].position,receipt.spawn.after)<.000001f;
    static EnvironmentMapDefinition FindMap(GameObject[] roots)=>roots.SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==Id);
    static MeshCollider Paths(EnvironmentMapDefinition map)=>map.GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="CAMP_Terrain_Paths");
    static void EnsureDataFolder(){if(!AssetDatabase.IsValidFolder(Root+"/Data"))AssetDatabase.CreateFolder(Root,"Data");}
    static void WriteTextAsset(string path,string text)
    {string absolute=Path.Combine(Application.dataPath,path.Substring(7));if(File.Exists(absolute))Require(Hash(File.ReadAllText(absolute))==Hash(text),"Destination text differs: "+path);File.WriteAllText(absolute,text,new UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
    static string Stamp(EnvironmentMapDefinition map)
    {
        var rows=map.GetComponentsInChildren<Transform>(true).Select(t=>PathOf(t,map.transform)+"|"+(t==map.HumanSpawnPoints[4]?"approved-spawn-position":VectorStamp(t.localPosition))+"|"+t.localRotation.ToString("F9")+"|"+VectorStamp(t.localScale)+"|"+t.gameObject.activeSelf).ToList();
        foreach(var renderer in map.GetComponentsInChildren<Renderer>(true))rows.Add("renderer|"+PathOf(renderer.transform,map.transform)+"|"+renderer.enabled+"|"+string.Join(",",renderer.sharedMaterials.Select(Asset)));
        foreach(var filter in map.GetComponentsInChildren<MeshFilter>(true))rows.Add("visible-mesh|"+PathOf(filter.transform,map.transform)+"|"+Asset(filter.sharedMesh));
        foreach(var collider in map.GetComponentsInChildren<Collider>(true))rows.Add("collider|"+PathOf(collider.transform,map.transform)+"|"+collider.enabled+"|"+collider.isTrigger+"|"+collider.contactOffset+"|"+Asset(collider.sharedMaterial)+(collider is MeshCollider mc?"|"+mc.convex+"|"+mc.cookingOptions+"|"+(mc==Paths(map)?"approved-collision-mesh":Asset(mc.sharedMesh)):collider is BoxCollider bc?"|"+VectorStamp(bc.center)+"|"+VectorStamp(bc.size):""));
        return Hash(string.Join("\n",rows.OrderBy(s=>s,StringComparer.Ordinal)));
    }
    static string Asset(Object value){if(!value)return "null";Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long local),"Persisted asset required.");return guid+":"+local;}
    static string PathOf(Transform value,Transform root){if(value==root)return ".";string result=value.name;while(value.parent && value.parent!=root){value=value.parent;result=value.name+"/"+result;}return result;}
    static string VectorStamp(Vector3 value)=>value.x.ToString("F9",CultureInfo.InvariantCulture)+","+value.y.ToString("F9",CultureInfo.InvariantCulture)+","+value.z.ToString("F9",CultureInfo.InvariantCulture);
    static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
