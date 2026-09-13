using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LetMeSleep.Content.Environment;
using Object=UnityEngine.Object;
public static class ApplyCampEdge
{
    public sealed class Receipt
    {
        public string status="INCOMPLETE",mapId="hf-campamento-pinar-v2",oldContentHash,newContentHash,validatedReport,reportSha256,navigationSha256,priorRevisionPath;
        public string prefabBefore,prefabAfter,sceneBefore,sceneAfter;public bool prefabReadback,sceneReadback;public Vector3 preservedSpawn05;public CampEdgeCandidate.Receipt edge;
        public string hashDerivation="SHA256(oldContentHash + newline + revision + newline + visibleMeshHash + newline + collisionMeshHash)";
    }
    public static string Run(string configPath,string output)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating,"Assigned idle editor required");
        var config=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Config>(File.ReadAllText(configPath));var report=Newtonsoft.Json.JsonConvert.DeserializeObject<HiggsfieldMapChecks.Report>(File.ReadAllText(config.validationReportPath));
        Require(config.action=="apply-camp-edge"&&config.mapId=="hf-campamento-pinar-v2"&&report.mapId==config.mapId&&report.status=="PASS_SCOPED"&&report.cases.Count==49&&report.cases.All(x=>x.status=="PASS")&&report.passages.Count==25&&report.passages.All(x=>x.status=="PASS_STATIC_CLEARANCE")&&report.errors.Count==0&&report.pending.Count==0&&report.cleanup,"Exact final49/49 and25/25 required");
        var edge=report.campEdgeCandidate;Require(edge!=null&&edge.deltaY==-.003f&&edge.meshes.Count==2&&edge.meshes.All(m=>m.changedVertices==2&&m.topologyPreserved&&m.minimumUpwardNormalY>.999f)&&edge.materialReferencesPreserved,"Exact validated local geometry required");
        var r=new Receipt{oldContentHash=report.contentHash,validatedReport=config.validationReportPath,reportSha256=Hash(File.ReadAllText(config.validationReportPath)),navigationSha256=report.navigationSha256,edge=edge};
        r.newContentHash=Hash(r.oldContentHash+"\n"+edge.revision+"\n"+edge.meshes.Single(x=>x.kind=="visible").candidateHash+"\n"+edge.meshes.Single(x=>x.kind=="collision").candidateHash);
        string root="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/"+r.mapId,prefab=root+"/Prefabs/"+r.mapId+".prefab",scenePath=root+"/Scenes/"+r.mapId+".unity";r.priorRevisionPath=root+"/Data/camp-technical-revision-v1.json";
        GameObject loaded=null;var owned=new List<Object>();Directory.CreateDirectory(output);
        try
        {
            loaded=PrefabUtility.LoadPrefabContents(prefab);var map=loaded.GetComponent<EnvironmentMapDefinition>();var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);var sceneMap=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(x=>x.MapId==r.mapId);
            Require(map.ContentHash==r.oldContentHash&&sceneMap.ContentHash==r.oldContentHash,"Content changed after validation");Require(Hash(map.SpatialData.text)==r.navigationSha256&&Hash(sceneMap.SpatialData.text)==r.navigationSha256,"Navigation changed");
            VerifyMeshes(map,edge,false);VerifyMeshes(sceneMap,edge,false);r.preservedSpawn05=map.HumanSpawnPoints[4].position;Require(Vector3.Distance(sceneMap.HumanSpawnPoints[4].position,r.preservedSpawn05)<.000001f,"Spawn differs between prefab and scene");r.prefabBefore=Stamp(map);r.sceneBefore=Stamp(sceneMap);
            var rebuilt=CampEdgeCandidate.Apply(loaded,owned);foreach(var part in edge.meshes)Require(rebuilt.meshes.Single(x=>x.kind==part.kind).candidateHash==part.candidateHash,"Rebuild differs from tested geometry");
            var path=Find(map);var visible=Persist(path.GetComponent<MeshFilter>().sharedMesh,root+"/Data/camp-path-visible-edge-v2.asset",owned);var collision=Persist(path.GetComponent<MeshCollider>().sharedMesh,root+"/Data/camp-paths-closed-edge-v2.asset",owned);
            Assign(map,visible,collision,r.newContentHash);PrefabUtility.SaveAsPrefabAsset(loaded,prefab);Assign(sceneMap,visible,collision,r.newContentHash);
            foreach(var component in new Object[]{sceneMap,Find(sceneMap).GetComponent<MeshFilter>(),Find(sceneMap).GetComponent<MeshCollider>()})if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed");AssetDatabase.SaveAssets();PrefabUtility.UnloadPrefabContents(loaded);loaded=null;
            loaded=PrefabUtility.LoadPrefabContents(prefab);map=loaded.GetComponent<EnvironmentMapDefinition>();VerifyMeshes(map,edge,true);r.prefabAfter=Stamp(map);r.prefabReadback=map.ContentHash==r.newContentHash&&Hash(map.SpatialData.text)==r.navigationSha256&&map.HumanSpawnPoints[4].position==r.preservedSpawn05;
            PrefabUtility.UnloadPrefabContents(loaded);loaded=null;scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);sceneMap=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnvironmentMapDefinition>(true)).Single(x=>x.MapId==r.mapId);VerifyMeshes(sceneMap,edge,true);r.sceneAfter=Stamp(sceneMap);r.sceneReadback=sceneMap.ContentHash==r.newContentHash&&Hash(sceneMap.SpatialData.text)==r.navigationSha256&&sceneMap.HumanSpawnPoints[4].position==r.preservedSpawn05;
            Require(r.prefabReadback&&r.sceneReadback&&r.prefabBefore==r.prefabAfter&&r.sceneBefore==r.sceneAfter,"Persistence or unrelated content preservation failed");r.status="APPLIED_READBACK_PASS";
            string provenance=root+"/Data/camp-technical-revision-v2.json",absolute=Path.Combine(Application.dataPath,provenance.Substring(7));Require(!File.Exists(absolute),"Do not overwrite prior v2 provenance");File.WriteAllText(absolute,HiggsfieldMapJson.Write(r),new UTF8Encoding(false));AssetDatabase.ImportAsset(provenance,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
        }
        finally{if(loaded)PrefabUtility.UnloadPrefabContents(loaded);foreach(var asset in owned)if(asset&&!AssetDatabase.Contains(asset))Object.DestroyImmediate(asset);File.WriteAllText(Path.Combine(output,"camp-edge-apply.json"),HiggsfieldMapJson.Write(r));}
        return Path.Combine(output,"camp-edge-apply.json");
    }
    static Transform Find(EnvironmentMapDefinition m)=>m.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="CAMP_Terrain_Paths");
    static void Assign(EnvironmentMapDefinition m,Mesh visible,Mesh collision,string hash){var t=Find(m);t.GetComponent<MeshFilter>().sharedMesh=visible;t.GetComponent<MeshCollider>().sharedMesh=collision;m.ContentHash=hash;foreach(var c in new Object[]{m,t.GetComponent<MeshFilter>(),t.GetComponent<MeshCollider>()})EditorUtility.SetDirty(c);}
    static Mesh Persist(Mesh m,string path,List<Object> owned){Require(!AssetDatabase.LoadAssetAtPath<Mesh>(path),"Destination mesh already exists");m.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(m,path);owned.Remove(m);return m;}
    static void VerifyMeshes(EnvironmentMapDefinition m,CampEdgeCandidate.Receipt r,bool after){var t=Find(m);foreach(var part in r.meshes){var mesh=part.kind=="visible"?t.GetComponent<MeshFilter>().sharedMesh:t.GetComponent<MeshCollider>().sharedMesh;Require(CampTechnicalCandidate.Hash(mesh)==(after?part.candidateHash:part.originalHash),"Map mesh differs from validation");}}
    static string Asset(Object o){if(!o)return "null";Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long local),"Persisted reference required");return guid+":"+local;}
    static string PathOf(Transform t,Transform root){if(t==root)return ".";string s=t.name;while(t.parent&&t.parent!=root){t=t.parent;s=t.name+"/"+s;}return s;}
    static string Stamp(EnvironmentMapDefinition m)
    {
        var rows=m.GetComponentsInChildren<Transform>(true).Select(t=>PathOf(t,m.transform)+"|"+t.localPosition.ToString("F9")+"|"+t.localRotation.ToString("F9")+"|"+t.localScale.ToString("F9")+"|"+t.gameObject.activeSelf).ToList();
        foreach(var r in m.GetComponentsInChildren<Renderer>(true))rows.Add("renderer|"+PathOf(r.transform,m.transform)+"|"+r.enabled+"|"+string.Join(",",r.sharedMaterials.Select(Asset)));
        foreach(var f in m.GetComponentsInChildren<MeshFilter>(true))rows.Add("mesh|"+PathOf(f.transform,m.transform)+"|"+(f.name=="CAMP_Terrain_Paths"?"authorized-edge":Asset(f.sharedMesh)));
        foreach(var c in m.GetComponentsInChildren<Collider>(true))rows.Add("collider|"+PathOf(c.transform,m.transform)+"|"+c.enabled+"|"+c.isTrigger+"|"+c.contactOffset+"|"+Asset(c.sharedMaterial)+(c is MeshCollider mc?"|"+mc.convex+"|"+mc.cookingOptions+"|"+(c.name=="CAMP_Terrain_Paths"?"authorized-edge":Asset(mc.sharedMesh)):""));
        return Hash(string.Join("\n",rows.OrderBy(x=>x,StringComparer.Ordinal)));
    }
    static string Hash(string s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
    static void Require(bool v,string s){if(!v)throw new Exception(s);}
}
