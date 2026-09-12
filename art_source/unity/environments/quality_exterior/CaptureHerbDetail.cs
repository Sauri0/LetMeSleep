// Director eval_file: two diagnostic cameras, no object/light/mesh changes.
var output="N:/LetMeSleep/Validation/Alfa-VisualRecovery/herb-detail-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
System.IO.Directory.CreateDirectory(output);
var candidates=new System.Collections.Generic.List<UnityEngine.MeshRenderer>();
foreach(var r in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MeshRenderer>())
    if(r.name=="PatioWestNear_Plant_01" && r.gameObject.scene.IsValid() && r.gameObject.activeInHierarchy) candidates.Add(r);
if(candidates.Count!=1) throw new System.InvalidOperationException("Expected one active PatioWestNear_Plant_01; found "+candidates.Count);
var target=candidates[0];
var filter=target.GetComponent<UnityEngine.MeshFilter>();
var objectMatrix=target.transform.localToWorldMatrix;
var bounds=target.bounds;
var center=bounds.center;
var camera=UnityEngine.Camera.main;
if(!camera) camera=System.Linq.Enumerable.FirstOrDefault(UnityEngine.Camera.allCameras,c=>c.targetTexture==null);
if(!camera) throw new System.InvalidOperationException("No resident camera.");
var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldFov=camera.fieldOfView;
// Based on actual renderer bounds, including runtime map transform and authored yaw.
var positions=new[]{center+new UnityEngine.Vector3(1f,.15f,-.3f).normalized*1.10f,
                    center+new UnityEngine.Vector3(0,1.10f,.08f)};
var names=new[]{"near-side","near-top"};
var rows=new System.Collections.Generic.List<object>();
try {
    for(int i=0;i<positions.Length;i++){
        camera.transform.position=positions[i];camera.transform.LookAt(center);camera.fieldOfView=45;
        var path=output+"/"+names[i]+".png";
        LetMeSleep.Editor.AlfaReviewCapture.Capture(path,1920,1080,false);
        string sha;
        using(var hash=System.Security.Cryptography.SHA256.Create())
            sha=System.BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        rows.Add(new{label=names[i],path,sha,position=new[]{positions[i].x,positions[i].y,positions[i].z},target=new[]{center.x,center.y,center.z},fov=45});
    }
    if(target.transform.localToWorldMatrix!=objectMatrix || target.bounds!=bounds)
        throw new System.InvalidOperationException("Plant moved during diagnostic captures.");
    var receipt=new{utc=System.DateTime.UtcNow.ToString("o"),unityVersion=UnityEngine.Application.unityVersion,
        objectName=target.name,objectId=target.gameObject.GetInstanceID(),rendererId=target.GetInstanceID(),meshId=filter.sharedMesh.GetInstanceID(),
        meshName=filter.sharedMesh.name,meshPath=UnityEditor.AssetDatabase.GetAssetPath(filter.sharedMesh),
        boundsCenter=new[]{center.x,center.y,center.z},boundsSize=new[]{bounds.size.x,bounds.size.y,bounds.size.z},
        materials=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(target.sharedMaterials,m=>m.name)),
        cameraId=camera.GetInstanceID(),objectUnchanged=true,views=rows,
        scope="Close plant shape/winding/coverage inspection. Camera only; no object, mesh, material or light edits. Side view is deliberately low and is not evidence of player eye height or traversal."};
    System.IO.File.WriteAllText(output+"/receipt.json",Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));
    return receipt;
} finally {camera.transform.position=oldPosition;camera.transform.rotation=oldRotation;camera.fieldOfView=oldFov;}
