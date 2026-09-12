// External eval_file body. Director only, in the existing EX2 house instance.
// No Clear, setters, geometry replacement, AssetDatabase save or scene rebuild.
var output = "N:/LetMeSleep/Validation/Alfa-VisualRecovery/exterior-upload-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
System.IO.Directory.CreateDirectory(output);
var roots = new System.Collections.Generic.List<UnityEngine.GameObject>();
foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if (candidate.name == "QualityExterior" && candidate.scene.IsValid() && candidate.activeInHierarchy) roots.Add(candidate);
if (roots.Count != 1) throw new System.InvalidOperationException("Expected one active QualityExterior scene root; found " + roots.Count);
var root = roots[0];
var camera = UnityEngine.Camera.main;
if (!camera) camera = System.Linq.Enumerable.FirstOrDefault(UnityEngine.Camera.allCameras, c => c.targetTexture == null);
if (!camera) throw new System.InvalidOperationException("No resident review camera.");
var originalPosition = camera.transform.position;
var originalRotation = camera.transform.rotation;
var originalFov = camera.fieldOfView;
var meshSet = new System.Collections.Generic.HashSet<UnityEngine.Mesh>();
foreach (var filter in root.GetComponentsInChildren<UnityEngine.MeshFilter>())
    if (filter.sharedMesh && filter.sharedMesh.isReadable) meshSet.Add(filter.sharedMesh);
if (meshSet.Count == 0) throw new System.InvalidOperationException("No readable exterior meshes.");
var meshes = new System.Collections.Generic.List<UnityEngine.Mesh>(meshSet);
meshes.Sort((a,b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
System.Func<byte[],string> shaBytes = bytes => {
    using (var sha = System.Security.Cryptography.SHA256.Create())
        return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
};
System.Func<UnityEngine.Mesh,string> cpuHash = mesh => {
    using (var stream = new System.IO.MemoryStream()) {
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
            writer.Write((int)mesh.indexFormat);
            foreach (var channel in new[] { mesh.vertices, mesh.normals }) {
                writer.Write(channel.Length);
                foreach (var v in channel) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            }
            foreach (var channel in new[] { mesh.uv, mesh.uv2 }) {
                writer.Write(channel.Length);
                foreach (var v in channel) { writer.Write(v.x); writer.Write(v.y); }
            }
            writer.Write(mesh.tangents.Length);
            foreach (var v in mesh.tangents) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); }
            writer.Write(mesh.colors32.Length);
            foreach (var v in mesh.colors32) { writer.Write(v.r); writer.Write(v.g); writer.Write(v.b); writer.Write(v.a); }
            writer.Write(mesh.subMeshCount);
            for (int i=0;i<mesh.subMeshCount;i++) {
                writer.Write((int)mesh.GetTopology(i));
                var indices = mesh.GetIndices(i); writer.Write(indices.Length);
                foreach (var index in indices) writer.Write(index);
            }
            var bounds = mesh.bounds;
            foreach (var v in new[] { bounds.center, bounds.size }) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
        }
        return shaBytes(stream.ToArray());
    }
};
System.Func<UnityEngine.Mesh,string> diskHash = mesh => {
    var assetPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
    if (string.IsNullOrEmpty(assetPath)) return "not-an-asset";
    var absolute = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"..",assetPath));
    return System.IO.File.Exists(absolute) ? shaBytes(System.IO.File.ReadAllBytes(absolute)) : "missing-file";
};
var beforeCpu = new System.Collections.Generic.Dictionary<int,string>();
var beforeDisk = new System.Collections.Generic.Dictionary<int,string>();
var rows = new System.Collections.Generic.List<object>();
var beforePath = output + "/before.png";
var afterPath = output + "/after.png";
int uploaded = 0;
try {
    // Exactly the EX2 patio-forward camera, unchanged between these two captures.
    camera.transform.position = new UnityEngine.Vector3(6.06f,1.53f,12.1f);
    camera.transform.LookAt(new UnityEngine.Vector3(2f,1.2f,16.5f));
    camera.fieldOfView = 65;
    var fixedMatrix = camera.transform.localToWorldMatrix;
    foreach (var mesh in meshes) { beforeCpu[mesh.GetInstanceID()] = cpuHash(mesh); beforeDisk[mesh.GetInstanceID()] = diskHash(mesh); }
    LetMeSleep.Editor.AlfaReviewCapture.Capture(beforePath,1920,1080,false);
    foreach (var mesh in meshes) { mesh.UploadMeshData(false); uploaded++; }
    foreach (var mesh in meshes) {
        var id = mesh.GetInstanceID(); var afterCpu = cpuHash(mesh); var afterDisk = diskHash(mesh);
        rows.Add(new { id, mesh.name, assetPath=UnityEditor.AssetDatabase.GetAssetPath(mesh), mesh.vertexCount, mesh.subMeshCount,
            beforeCpu=beforeCpu[id], afterCpu, cpuEqual=beforeCpu[id]==afterCpu,
            beforeDisk=beforeDisk[id], afterDisk, diskEqual=beforeDisk[id]==afterDisk });
        if (beforeCpu[id]!=afterCpu || beforeDisk[id]!=afterDisk)
            throw new System.InvalidOperationException("Upload changed CPU or disk data: " + mesh.name);
    }
    if (camera.transform.localToWorldMatrix != fixedMatrix || camera.fieldOfView != 65)
        throw new System.InvalidOperationException("Camera changed during upload probe.");
    LetMeSleep.Editor.AlfaReviewCapture.Capture(afterPath,1920,1080,false);
    var beforeSha = shaBytes(System.IO.File.ReadAllBytes(beforePath));
    var afterSha = shaBytes(System.IO.File.ReadAllBytes(afterPath));
    var receipt = new { utc=System.DateTime.UtcNow.ToString("o"), unityVersion=UnityEngine.Application.unityVersion,
        rootId=root.GetInstanceID(), cameraId=camera.GetInstanceID(), scene=root.scene.path,
        cameraPosition=new[]{6.06f,1.53f,12.1f}, cameraTarget=new[]{2f,1.2f,16.5f}, fov=65,
        uploaded, sameInstance=true, cpuAndDiskUnchanged=true, beforePath, afterPath, beforeSha, afterSha,
        pngBytesIdentical=beforeSha==afterSha, meshes=rows,
        scope="Only UploadMeshData(false) on readable meshes of the existing exterior. Same camera and scene instance. No geometry setters, Clear, save, rebuild or light change. Different PNGs alone do not prove the cause; inspect pixels." };
    System.IO.File.WriteAllText(output+"/receipt.json",Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));
    return receipt;
} catch (System.Exception error) {
    System.IO.File.WriteAllText(output+"/error.txt",error.ToString());
    System.IO.File.WriteAllText(output+"/partial.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { uploaded, rows },Newtonsoft.Json.Formatting.Indented));
    throw;
} finally {
    camera.transform.position=originalPosition; camera.transform.rotation=originalRotation; camera.fieldOfView=originalFov;
}
