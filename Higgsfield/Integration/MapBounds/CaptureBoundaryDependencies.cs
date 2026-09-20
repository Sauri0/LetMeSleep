using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;

// External read-only driver. Compile offline, execute only in an assigned native lease.
// Never updates config guards: coordinator must review evidence and explain dependency-only drift.
public static class CaptureBoundaryDependencies
{
    public static string Run(string configPath,string baselinePath,string outputPath)
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating,"Assigned idle native lease required.");
        Need(Path.IsPathRooted(outputPath)&&Directory.Exists(Path.GetDirectoryName(outputPath))&&!File.Exists(outputPath),"New external receipt required.");
        Need(!Path.GetFullPath(outputPath).StartsWith(Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"External receipt only.");
        var config=JObject.Parse(File.ReadAllText(configPath));var baseline=JObject.Parse(File.ReadAllText(baselinePath));
        var report=new JObject { ["success"]=false,["scope"]="Read-only native dependency and mesh inventory. No guard updates, scenes opened, imports, saves or collider mutations.",
            ["unityVersion"]=Application.unityVersion,["configSha256"]=HashFile(configPath),["baselineSha256"]=HashFile(baselinePath) };
        using(var output=new FileStream(outputPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        {
            try
            {
                var files=(JArray)baseline["files"];Need(files!=null&&files.Count>0,"Explicit protected asset baseline required.");
                foreach(var row in files)Need(HashFile(Disk((string)row["path"]))==(string)row["sha256"],"Protected asset drift: "+row["path"]);
                var baselinePaths=new HashSet<string>(files.Select(r=>(string)r["path"]),StringComparer.Ordinal);
                var rows=(JArray)config["maps"];Need(rows!=null&&rows.Count==5&&rows.Select(r=>(string)r["mapId"]).Distinct().Count()==5,"Five unique map records required.");
                CheckGuard(config["catalog"]);var maps=new JArray();report["maps"]=maps;
                foreach(var row in rows)
                {
                    foreach(var kind in new[]{"prefab","scene","spatial"})CheckGuard(row[kind]);
                    string path=(string)row["prefab"]["path"];var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Need(root&&!EditorUtility.IsDirty(root),"Saved prefab required.");var map=root.GetComponent<EnvironmentMapDefinition>();
                    Need(map&&map.MapId==(string)row["mapId"]&&map.ContentHash==(string)row["expectedContentHash"]&&!EditorUtility.IsDirty(map),"Map definition drift.");
                    Need(map.SpatialData&&AssetDatabase.GetAssetPath(map.SpatialData)==(string)row["spatial"]["path"],"Spatial reference drift.");
                    var dependencies=new JArray();
                    foreach(var dependency in AssetDatabase.GetDependencies(path,true).OrderBy(x=>x,StringComparer.Ordinal))
                    {
                        string disk=dependency.StartsWith("Assets/",StringComparison.Ordinal)?Disk(dependency):null;
                        dependencies.Add(new JObject {["path"]=dependency,["guid"]=AssetDatabase.AssetPathToGUID(dependency),
                            ["sha256"]=disk!=null&&File.Exists(disk)?HashFile(disk):null,
                            ["metaSha256"]=disk!=null&&File.Exists(disk+".meta")?HashFile(disk+".meta"):null,
                            ["protectedByBaseline"]=baselinePaths.Contains(dependency),
                            ["type"]=AssetDatabase.GetMainAssetTypeAtPath(dependency)?.FullName});
                    }
                    var meshes=new JArray();var meshCache=new Dictionary<Mesh,string>();
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))AddMesh(meshes,meshCache,baselinePaths,map.transform,filter.transform,filter.sharedMesh,"visible");
                    foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))AddMesh(meshes,meshCache,baselinePaths,map.transform,renderer.transform,renderer.sharedMesh,"skinned");
                    foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))AddMesh(meshes,meshCache,baselinePaths,map.transform,collider.transform,collider.sharedMesh,"collision");
                    maps.Add(new JObject {["mapId"]=map.MapId,["contentHash"]=map.ContentHash,["prefabPath"]=path,
                        ["prefabSha256"]=HashFile(Disk(path)),["prefabGuid"]=AssetDatabase.AssetPathToGUID(path),
                        ["expectedDependencyHash"]=(string)row["prefab"]["dependencyHash"],
                        ["observedDependencyHash"]=AssetDatabase.GetAssetDependencyHash(path).ToString(),
                        ["dependencies"]=dependencies,["meshes"]=meshes});
                }
                foreach(var row in files)Need(HashFile(Disk((string)row["path"]))==(string)row["sha256"],"Protected asset changed during inventory.");
                report["success"]=true;report["status"]="PASS_READ_ONLY_NATIVE_DEPENDENCY_MESH_INVENTORY";
            }
            catch(Exception error){report["status"]="FAILED_NO_GUARD_UPDATES";report["error"]=error.ToString();throw;}
            finally{byte[] bytes=System.Text.Encoding.UTF8.GetBytes(report.ToString());output.Write(bytes,0,bytes.Length);output.Flush(true);}
        }
        return outputPath;
    }
    static void AddMesh(JArray rows,Dictionary<Mesh,string> cache,HashSet<string> protectedPaths,Transform root,Transform owner,Mesh mesh,string kind)
    {
        Need(mesh,"Missing referenced mesh.");string path=AssetDatabase.GetAssetPath(mesh);
        Need(protectedPaths.Contains(path)&&protectedPaths.Contains(path+".meta"),"Mesh file/meta absent from protected baseline: "+path);
        Need(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long local),"Persisted mesh identity required.");
        if(!cache.TryGetValue(mesh,out string hash))
        {
            using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
            {foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(int index in mesh.triangles)writer.Write(index);writer.Flush();hash=Hash(memory.ToArray());cache.Add(mesh,hash);}
        }
        string relative=owner==root?".":AnimationUtility.CalculateTransformPath(owner,root);
        rows.Add(new JObject {["objectPath"]=relative,["kind"]=kind,["meshPath"]=path,["meshGuid"]=guid,
            ["meshLocalId"]=local.ToString(CultureInfo.InvariantCulture),["geometrySha256"]=hash,["vertices"]=mesh.vertexCount,
            ["triangles"]=mesh.triangles.Length/3,["subMeshes"]=mesh.subMeshCount,["blendShapes"]=mesh.blendShapeCount});
    }
    static void CheckGuard(JToken guard)
    {
        Need(guard!=null,"Missing file guard.");string path=(string)guard["path"],disk=Disk(path);
        Need(HashFile(disk)==(string)guard["sha256"]&&HashFile(disk+".meta")== (string)guard["metaSha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)guard["guid"],"SHA/meta/GUID drift: "+path);
    }
    static string Disk(string path)
    {Need(path!=null&&path.StartsWith("Assets/",StringComparison.Ordinal)&&!path.Contains("\\")&&!path.Contains(":")&&path.Split('/').All(p=>p!=""&&p!="."&&p!=".."),"Canonical Assets path required.");return Path.GetFullPath(Path.Combine(Application.dataPath,"..",path));}
    static string HashFile(string path)=>Hash(File.ReadAllBytes(path));
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Need(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
