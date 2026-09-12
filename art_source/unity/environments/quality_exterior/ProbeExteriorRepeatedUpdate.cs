// Director eval_file after integrating the public UpdateMeshData fix.
// Historical/current topology alternates on the same existing Pine_00 mesh.
var sourceDir = "N:/LetMeSleep/Worktrees/maps/art_source/unity/environments/quality_exterior/";
var output = "N:/LetMeSleep/Validation/Alfa-VisualRecovery/exterior-repeat-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
System.IO.Directory.CreateDirectory(output);
var recipe = UnityEngine.JsonUtility.FromJson<LetMeSleep.Content.Editor.AlfaQualityExterior.Recipe>(System.IO.File.ReadAllText(sourceDir+"generated_exterior.json"));
var current = System.Linq.Enumerable.Single(recipe.meshes, m=>m.name=="Pine_00");
var historical = UnityEngine.JsonUtility.FromJson<LetMeSleep.Content.Editor.AlfaQualityExterior.MeshSpec>(System.IO.File.ReadAllText(sourceDir+"Pine_EX1_Diagnostic.json"));
var roots = new System.Collections.Generic.List<UnityEngine.GameObject>();
foreach(var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if(go.name=="QualityExterior" && go.scene.IsValid() && go.activeInHierarchy) roots.Add(go);
if(roots.Count!=1) throw new System.InvalidOperationException("Expected one active exterior instance.");
var root=roots[0];
var filter=System.Linq.Enumerable.Single(root.GetComponentsInChildren<UnityEngine.MeshFilter>(),f=>f.name=="Patio_Pine_W_Replacement");
var mesh=filter.sharedMesh;
if(!mesh || !mesh.isReadable) throw new System.InvalidOperationException("Pine_00 must remain readable.");
var renderer=filter.GetComponent<UnityEngine.MeshRenderer>();
var materials=renderer.sharedMaterials;
if(!System.Linq.Enumerable.SequenceEqual(mesh.vertices,current.vertices)) throw new System.InvalidOperationException("Resident vertex data is not current EX2; inspect before mutating.");
for(int i=0;i<current.submeshes.Length;i++)
    if(!System.Linq.Enumerable.SequenceEqual(mesh.GetTriangles(i),current.submeshes[i].triangles)) throw new System.InvalidOperationException("Resident indices differ from EX2.");
var camera=UnityEngine.Camera.main;
if(!camera) camera=System.Linq.Enumerable.FirstOrDefault(UnityEngine.Camera.allCameras,c=>c.targetTexture==null);
if(!camera) throw new System.InvalidOperationException("No resident camera.");
var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldFov=camera.fieldOfView;
var meshId=mesh.GetInstanceID();var rootId=root.GetInstanceID();var cameraId=camera.GetInstanceID();
var assetPath=UnityEditor.AssetDatabase.GetAssetPath(mesh);var guid=UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
var assetFile=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"..",assetPath));
System.Func<byte[],string> hash=bytes=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();};
System.Func<UnityEngine.Mesh,string> cpuHash=m=>{
    using(var stream=new System.IO.MemoryStream()){
        using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true)){
            foreach(var channel in new[]{m.vertices,m.normals}){writer.Write(channel.Length);foreach(var v in channel){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}}
            foreach(var channel in new[]{m.uv,m.uv2}){writer.Write(channel.Length);foreach(var v in channel){writer.Write(v.x);writer.Write(v.y);}}
            writer.Write(m.subMeshCount);for(int i=0;i<m.subMeshCount;i++){var ids=m.GetTriangles(i);writer.Write(ids.Length);foreach(var id in ids)writer.Write(id);}
        }
        return hash(stream.ToArray());
    }
};
var beforeCpu=cpuHash(mesh);var beforeDisk=hash(System.IO.File.ReadAllBytes(assetFile));
var records=new System.Collections.Generic.List<object>();
var imageHashes=new System.Collections.Generic.List<string>();
bool restored=false;
try{
    camera.transform.position=new UnityEngine.Vector3(6.06f,1.53f,12.1f);
    camera.transform.LookAt(new UnityEngine.Vector3(2f,1.2f,16.5f));camera.fieldOfView=65;
    var matrix=camera.transform.localToWorldMatrix;
    LetMeSleep.Editor.AlfaReviewCapture.Capture(output+"/baseline.png",1920,1080,false);
    var sequence=new[]{historical,current,historical,current};
    var labels=new[]{"old-1","current-1","old-2","current-2"};
    for(int i=0;i<sequence.Length;i++){
        LetMeSleep.Content.Editor.AlfaQualityExterior.UpdateMeshData(mesh,sequence[i]);
        if(mesh.GetInstanceID()!=meshId || filter.sharedMesh!=mesh || UnityEditor.AssetDatabase.AssetPathToGUID(assetPath)!=guid)
            throw new System.InvalidOperationException("Mesh identity/GUID changed.");
        if(!System.Linq.Enumerable.SequenceEqual(renderer.sharedMaterials,materials) || camera.transform.localToWorldMatrix!=matrix || camera.fieldOfView!=65)
            throw new System.InvalidOperationException("Materials/camera changed.");
        var path=output+"/"+labels[i]+".png";
        LetMeSleep.Editor.AlfaReviewCapture.Capture(path,1920,1080,false);
        var pngSha=hash(System.IO.File.ReadAllBytes(path));imageHashes.Add(pngSha);
        records.Add(new{label=labels[i],meshId,vertexCount=mesh.vertexCount,subMeshCount=mesh.subMeshCount,cpuSha=cpuHash(mesh),pngSha,path});
    }
    var finalCpu=cpuHash(mesh);var finalDisk=hash(System.IO.File.ReadAllBytes(assetFile));
    if(finalCpu!=beforeCpu || finalDisk!=beforeDisk) throw new System.InvalidOperationException("Current CPU state/disk did not roundtrip.");
    restored=true;
    var receipt=new{utc=System.DateTime.UtcNow.ToString("o"),unityVersion=UnityEngine.Application.unityVersion,rootId,cameraId,meshId,guid,assetPath,
        beforeCpu,finalCpu,beforeDisk,finalDisk,currentRestored=true,records,
        oldRepeatsEqual=imageHashes[0]==imageHashes[2],currentRepeatsEqual=imageHashes[1]==imageHashes[3],
        oldAndCurrentDiffer=imageHashes[0]!=imageHashes[1],
        scope="Same existing mesh instance and GUID; historical/current topology alternates twice through production UpdateMeshData. Material slots remain fixed. No asset/scene save, JSON modification or geometry reauthoring. Inspect images; hash equality is not visual approval."};
    System.IO.File.WriteAllText(output+"/receipt.json",Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));
    return receipt;
}catch(System.Exception e){System.IO.File.WriteAllText(output+"/error.txt",e.ToString());throw;}
finally{
    if(!restored) LetMeSleep.Content.Editor.AlfaQualityExterior.UpdateMeshData(mesh,current);
    camera.transform.position=oldPosition;camera.transform.rotation=oldRotation;camera.fieldOfView=oldFov;
}
