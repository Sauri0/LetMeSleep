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

// Save only a fully validated final Yate/Puerto schema and explicitly authorized Yate pose delta.
public static class ApplyRemainingSemanticNavigation
{
    public sealed class Receipt
    {
        public string status="INCOMPLETE",mapId,oldContentHash,newContentHash,navigationSha256,navigationGuid,validatedReport;
        public string originalSpatialDataPath,originalSpatialDataGuid,originalSpatialDataSha256;
        public string prefabBefore,prefabAfter,sceneBefore,sceneAfter;
        public string hashDerivation="SHA256(oldContentHash + newline + nav-schema1 + newline + navigationSha256 [+ newline + storageRevision + newline + worldDeltaF9Invariant])";
        public YateStorageCandidate.Receipt storage;
        public YateBulkheadCandidate.Receipt bulkhead;
        public bool prefabReadback,sceneReadback;
    }
    public static string Run(string configPath,string output)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,"Assigned idle EditMode required.");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));
        bool yate=config.mapId=="hf-yate-a-la-deriva-v3";Require(yate || config.mapId=="hf-puerto-del-faro-v1","Final remaining map IDs only.");
        string root="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/"+config.mapId;
        string prefab=root+"/Prefabs/"+config.mapId+".prefab",scenePath=root+"/Scenes/"+config.mapId+".unity",prefix=yate?"yate":"puerto";
        string navPath=root+"/Data/"+prefix+"-navigation-schema1.json",provenancePath=root+"/Data/"+prefix+"-technical-revision-v1.json";
        Require(config.prefabPath==prefab && config.action=="apply-remaining-map","Explicit final semantic operation required.");
        var validation=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Report>(File.ReadAllText(config.validationReportPath));
        string json=File.ReadAllText(config.navigationOverridePath),navHash=Hash(json);
        Require(validation.mapId==config.mapId && validation.status=="PASS_SCOPED" && validation.errors.Count==0 && validation.pending.Count==0 && validation.cleanup && validation.navigationSha256==navHash && validation.cases.Count==49 && validation.cases.All(c=>c.status=="PASS") && validation.passages.All(p=>p.status=="PASS_STATIC_CLEARANCE"),"Require exact complete 49-case PASS and clear portals for this candidate.");
        Require(!yate || validation.yateStorageCandidate!=null && validation.yateStorageCandidate.maximumConservativeBoxPenetration<=.002f && validation.yateStorageCandidate.supports.Count==5,"Yate must include validated chest relocation/support.");
        Require(!yate || validation.yateBulkheadCandidate?.renderAndColliderMatch==true && validation.yateBulkheadCandidate.materialReferencesPreserved,"Yate must include the validated coherent bulkhead repair.");
        var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Plan>(json);
        Require(plan.map_id==config.mapId && plan.schema_version==1 && plan.zones.Length>=2 && plan.portals.Length==validation.passages.Count,"Unexpected schema/portal counts.");
        var receipt=new Receipt{mapId=config.mapId,oldContentHash=validation.contentHash,navigationSha256=navHash,validatedReport=config.validationReportPath,storage=validation.yateStorageCandidate,bulkhead=validation.yateBulkheadCandidate};
        receipt.newContentHash=Hash(receipt.oldContentHash+"\nnav-schema1\n"+navHash+(receipt.storage==null?"":"\n"+receipt.storage.revision+"\n"+VectorStamp(receipt.storage.worldDelta)));
        if(receipt.bulkhead!=null){receipt.newContentHash=Hash(receipt.newContentHash+"\n"+receipt.bulkhead.revision+"\n"+receipt.bulkhead.candidateMeshSha256);receipt.hashDerivation+="; then SHA256(previous + newline + bulkheadRevision + newline + bulkheadMeshSha256)";}
        Directory.CreateDirectory(output);GameObject loaded=null;var owned=new List<Object>();Mesh wallAsset=null;
        try
        {
            loaded=PrefabUtility.LoadPrefabContents(prefab);var map=loaded.GetComponent<EnvironmentMapDefinition>();
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);var sceneMap=FindMap(scene.GetRootGameObjects(),config.mapId);
            Require(map.ContentHash==receipt.oldContentHash && sceneMap.ContentHash==receipt.oldContentHash,"Validated content changed; do not overwrite coordinator edits.");
            VerifyParts(map,receipt.storage,false);VerifyParts(sceneMap,receipt.storage,false);
            VerifyWall(map,receipt.bulkhead,false);VerifyWall(sceneMap,receipt.bulkhead,false);
            receipt.originalSpatialDataPath=AssetDatabase.GetAssetPath(map.SpatialData);receipt.originalSpatialDataGuid=AssetDatabase.AssetPathToGUID(receipt.originalSpatialDataPath);receipt.originalSpatialDataSha256=Hash(map.SpatialData.text);
            receipt.prefabBefore=Stamp(map,receipt.storage);receipt.sceneBefore=Stamp(sceneMap,receipt.storage);
            if(!AssetDatabase.IsValidFolder(root+"/Data"))AssetDatabase.CreateFolder(root,"Data");
            if(receipt.bulkhead!=null)
            {
                var rebuilt=YateBulkheadCandidate.Apply(loaded,owned);Require(rebuilt.candidateMeshSha256==receipt.bulkhead.candidateMeshSha256,"Rebuilt bulkhead differs from validation.");
                string wallPath=root+"/Data/yate-swim-bulkhead-v1.asset";var generated=FindPart(map,YateBulkheadCandidate.Name).GetComponent<MeshFilter>().sharedMesh;wallAsset=AssetDatabase.LoadAssetAtPath<Mesh>(wallPath);
                if(wallAsset)Require(CampTechnicalCandidate.Hash(wallAsset)==rebuilt.candidateMeshSha256,"Existing bulkhead asset differs.");
                else{generated.name="YateSwimBulkheadNotch_v1";AssetDatabase.CreateAsset(generated,wallPath);owned.Remove(generated);wallAsset=generated;}
            }
            Write(navPath,json);
            var nav=AssetDatabase.LoadAssetAtPath<TextAsset>(navPath);Require(nav && Hash(nav.text)==navHash,"Navigation import mismatch.");receipt.navigationGuid=AssetDatabase.AssetPathToGUID(navPath);
            Assign(map,nav,receipt,wallAsset);PrefabUtility.SaveAsPrefabAsset(loaded,prefab);Assign(sceneMap,nav,receipt,wallAsset);
            if(PrefabUtility.IsPartOfPrefabInstance(sceneMap))PrefabUtility.RecordPrefabInstancePropertyModifications(sceneMap);
            if(receipt.storage!=null)foreach(var part in receipt.storage.parts){var transform=FindPart(sceneMap,part.name);if(PrefabUtility.IsPartOfPrefabInstance(transform))PrefabUtility.RecordPrefabInstancePropertyModifications(transform);}
            if(wallAsset){var wall=FindPart(sceneMap,YateBulkheadCandidate.Name);foreach(var component in new Object[]{wall.GetComponent<MeshFilter>(),wall.GetComponent<MeshCollider>()})if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed.");AssetDatabase.SaveAssets();
            PrefabUtility.UnloadPrefabContents(loaded);loaded=null;loaded=PrefabUtility.LoadPrefabContents(prefab);map=loaded.GetComponent<EnvironmentMapDefinition>();
            receipt.prefabReadback=Readback(map,receipt,navPath);receipt.prefabAfter=Stamp(map,receipt.storage);VerifyParts(map,receipt.storage,true);
            VerifyWall(map,receipt.bulkhead,true);
            PrefabUtility.UnloadPrefabContents(loaded);loaded=null;scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);sceneMap=FindMap(scene.GetRootGameObjects(),config.mapId);
            receipt.sceneReadback=Readback(sceneMap,receipt,navPath);receipt.sceneAfter=Stamp(sceneMap,receipt.storage);VerifyParts(sceneMap,receipt.storage,true);
            VerifyWall(sceneMap,receipt.bulkhead,true);
            Require(receipt.prefabReadback && receipt.sceneReadback && receipt.prefabBefore==receipt.prefabAfter && receipt.sceneBefore==receipt.sceneAfter,"Readback or unrelated-content preservation failed.");
            receipt.status="APPLIED_READBACK_PASS";Write(provenancePath,HiggsfieldMapJson.Write(receipt));AssetDatabase.SaveAssets();
        }
        finally{if(loaded)PrefabUtility.UnloadPrefabContents(loaded);foreach(var asset in owned)if(asset && !AssetDatabase.Contains(asset))Object.DestroyImmediate(asset);File.WriteAllText(Path.Combine(output,prefix+"-semantic-apply.json"),HiggsfieldMapJson.Write(receipt));}
        return Path.Combine(output,prefix+"-semantic-apply.json");
    }
    static void VerifyParts(EnvironmentMapDefinition map,YateStorageCandidate.Receipt storage,bool after)
    {
        if(storage==null)return;
        Require(map.MapId=="hf-yate-a-la-deriva-v3" && storage.parts.Count==2 && storage.worldDelta==new Vector3(-1.4f,0,2),"Only the measured rigid relocation is authorized here.");
        foreach(var part in storage.parts)
        {
            Require(part.name=="YATE_DeckStorage_01" || part.name=="YATE_DeckStorage_Lid_01","Unexpected movable part.");var transform=FindPart(map,part.name);
            Require(Vector3.Distance(transform.position,after?part.after:part.before)<.000001f && Quaternion.Angle(transform.rotation,part.rotation)<.001f && transform.localScale==part.scale,"Storage pose changed.");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(transform.GetComponent<MeshFilter>().sharedMesh,out string guid,out long local) && guid==part.meshGuid && local==part.meshLocalId,"Storage mesh reference changed.");
            Require(transform.GetComponent<Renderer>().sharedMaterials.Select(m=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).SequenceEqual(part.materialGuids),"Storage material references changed.");
        }
    }
    static void Assign(EnvironmentMapDefinition map,TextAsset nav,Receipt receipt,Mesh wallAsset)
    {if(receipt.storage!=null)foreach(var part in receipt.storage.parts){var transform=FindPart(map,part.name);transform.position=part.after;EditorUtility.SetDirty(transform);}if(wallAsset){var wall=FindPart(map,YateBulkheadCandidate.Name);wall.GetComponent<MeshFilter>().sharedMesh=wallAsset;wall.GetComponent<MeshCollider>().sharedMesh=wallAsset;EditorUtility.SetDirty(wall.GetComponent<MeshFilter>());EditorUtility.SetDirty(wall.GetComponent<MeshCollider>());}map.SpatialData=nav;map.ContentHash=receipt.newContentHash;EditorUtility.SetDirty(map);}
    static void VerifyWall(EnvironmentMapDefinition map,YateBulkheadCandidate.Receipt wall,bool after)
    {if(wall==null)return;var transform=FindPart(map,YateBulkheadCandidate.Name);var mesh=transform.GetComponent<MeshFilter>().sharedMesh;Require(mesh==transform.GetComponent<MeshCollider>().sharedMesh && CampTechnicalCandidate.Hash(mesh)==(after?wall.candidateMeshSha256:wall.originalMeshSha256),"Bulkhead visible/collision geometry differs from validated revision.");}
    static bool Readback(EnvironmentMapDefinition map,Receipt receipt,string navPath)=>map.MapId==receipt.mapId && map.ContentHash==receipt.newContentHash && AssetDatabase.GetAssetPath(map.SpatialData)==navPath && Hash(map.SpatialData.text)==receipt.navigationSha256;
    static EnvironmentMapDefinition FindMap(GameObject[] roots,string id)=>roots.SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(m=>m.MapId==id);
    static Transform FindPart(EnvironmentMapDefinition map,string name)=>map.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static string Stamp(EnvironmentMapDefinition map,YateStorageCandidate.Receipt storage)
    {
        var movable=storage?.parts.Select(p=>p.name).ToArray()??Array.Empty<string>();
        var rows=map.GetComponentsInChildren<Transform>(true).Select(t=>PathOf(t,map.transform)+"|"+(movable.Contains(t.name)?"approved-storage-position":VectorStamp(t.localPosition))+"|"+t.localRotation.ToString("F9")+"|"+VectorStamp(t.localScale)+"|"+t.gameObject.activeSelf).ToList();
        foreach(var renderer in map.GetComponentsInChildren<Renderer>(true))rows.Add("renderer|"+PathOf(renderer.transform,map.transform)+"|"+renderer.enabled+"|"+string.Join(",",renderer.sharedMaterials.Select(Asset)));
        foreach(var filter in map.GetComponentsInChildren<MeshFilter>(true))rows.Add("mesh|"+PathOf(filter.transform,map.transform)+"|"+(storage!=null && filter.name==YateBulkheadCandidate.Name?"approved-bulkhead-mesh":Asset(filter.sharedMesh)));
        foreach(var collider in map.GetComponentsInChildren<Collider>(true))rows.Add("collider|"+PathOf(collider.transform,map.transform)+"|"+collider.enabled+"|"+collider.isTrigger+"|"+collider.contactOffset+"|"+Asset(collider.sharedMaterial)+(collider is MeshCollider mc?"|"+mc.convex+"|"+mc.cookingOptions+"|"+(storage!=null && mc.name==YateBulkheadCandidate.Name?"approved-bulkhead-mesh":Asset(mc.sharedMesh)):collider is BoxCollider bc?"|"+VectorStamp(bc.center)+"|"+VectorStamp(bc.size):""));
        return Hash(string.Join("\n",rows.OrderBy(s=>s,StringComparer.Ordinal)));
    }
    static string Asset(Object value){if(!value)return "null";Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long local),"Persisted reference required.");return guid+":"+local+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(value));}
    static string PathOf(Transform value,Transform root){if(value==root)return ".";string path=value.name;while(value.parent && value.parent!=root){value=value.parent;path=value.name+"/"+path;}return path;}
    static string VectorStamp(Vector3 value)=>value.x.ToString("F9",CultureInfo.InvariantCulture)+","+value.y.ToString("F9",CultureInfo.InvariantCulture)+","+value.z.ToString("F9",CultureInfo.InvariantCulture);
    static void Write(string path,string text){string absolute=Path.Combine(Application.dataPath,path.Substring(7));if(File.Exists(absolute))Require(Hash(File.ReadAllText(absolute))==Hash(text),"Destination already differs: "+path);File.WriteAllText(absolute,text,new UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
    static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
}
