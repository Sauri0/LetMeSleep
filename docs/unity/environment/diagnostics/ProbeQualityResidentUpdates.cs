// Manual Director eval_file in Play: original -> 55% -> 80% -> original.
// Calls the production PersistQualityMesh helper. Never saves/reimports assets.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Requires the Director's active Humantraining session.");
System.Func<UnityEngine.Camera> selectCamera = () => {
    var candidates = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(UnityEngine.Camera.allCameras,
        c => c && c.isActiveAndEnabled && c.gameObject.activeInHierarchy && c.cameraType == UnityEngine.CameraType.Game
            && c.targetTexture == null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
            && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(c.gameObject.scene)));
    if (candidates.Length != 1) throw new System.InvalidOperationException("Require exactly one active game camera without a render target, excluding preview scenes.");
    return candidates[0];
};
var camera = selectCamera();
var filters = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(
    UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsSortMode.None),
    f => f.name == "Cushion_m0p57" && f.gameObject.activeInHierarchy));
if (filters.Length != 1) throw new System.InvalidOperationException("Require exactly one active blue living cushion.");
var filter = filters[0]; var mesh = filter.sharedMesh; var renderer = filter.GetComponent<UnityEngine.MeshRenderer>();
if (!mesh || !mesh.isReadable || !renderer || !renderer.enabled) throw new System.InvalidOperationException("Cushion must be visible and readable.");
var assetPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
if (!assetPath.StartsWith("Assets/LetMeSleep/Content/Environment/AlfaMaps/Meshes/Quality_") || !assetPath.EndsWith("Cushion_m0p57.asset"))
    throw new System.InvalidOperationException("Unexpected asset; no changes allowed.");
System.Func<string, System.Type> findType = name => {
    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) { var type = assembly.GetType(name, false); if (type != null) return type; }
    throw new System.InvalidOperationException("Missing loaded type: " + name);
};
var persist = findType("LetMeSleep.Content.Editor.AlfaMapBuilder").GetMethod("PersistQualityMesh", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var capture = findType("LetMeSleep.Editor.AlfaReviewCapture").GetMethod("Capture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
if (persist == null || capture == null) throw new System.InvalidOperationException("Integrate 11dd53b and load the existing capture helper first.");
System.Func<byte[], string> sha = bytes => {
    using (var hash = System.Security.Cryptography.SHA256.Create()) return System.BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
};
System.Func<object, string> json = value => Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);
System.Func<UnityEngine.Mesh, string> meshHash = value => {
    using (var stream = new System.IO.MemoryStream()) {
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
            writer.Write(value.name); writer.Write((int)value.indexFormat); writer.Write(value.vertexCount);
            foreach (var v in value.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            foreach (var n in value.normals) { writer.Write(n.x); writer.Write(n.y); writer.Write(n.z); }
            foreach (var t in value.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
            foreach (var c in value.colors) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            foreach (var uv in value.uv) { writer.Write(uv.x); writer.Write(uv.y); }
            foreach (var uv in value.uv2) { writer.Write(uv.x); writer.Write(uv.y); }
            writer.Write(value.subMeshCount);
            for (int slot = 0; slot < value.subMeshCount; slot++) {
                writer.Write((int)value.GetTopology(slot)); var indices = value.GetIndices(slot); writer.Write(indices.Length); foreach (int index in indices) writer.Write(index);
            }
            var b = value.bounds; writer.Write(b.center.x); writer.Write(b.center.y); writer.Write(b.center.z); writer.Write(b.size.x); writer.Write(b.size.y); writer.Write(b.size.z);
        }
        return sha(stream.ToArray());
    }
};
int cameraId = camera.GetInstanceID(), filterId = filter.GetInstanceID(), meshId = mesh.GetInstanceID(), frame = UnityEngine.Time.frameCount;
string guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath), originalHash = meshHash(mesh);
string diskPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", assetPath));
string diskHash = sha(System.IO.File.ReadAllBytes(diskPath)), metaHash = sha(System.IO.File.ReadAllBytes(diskPath + ".meta"));
bool wasDirty = UnityEditor.EditorUtility.IsDirty(mesh), sceneWasDirty = filter.gameObject.scene.isDirty;
var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
var target = camera.targetTexture; var activeTarget = UnityEngine.RenderTexture.active;
string output = "N:/LetMeSleep/Validation/Alfa-VisualRecovery/quality-resident-updates-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
System.IO.Directory.CreateDirectory(output);
UnityEngine.Mesh snapshot = null;
var stages = new System.Collections.Generic.List<object>(); var images = new System.Collections.Generic.List<string>(); var hashes = new System.Collections.Generic.List<string>();
bool attemptedUpdate = false, restored = false, controlsStable = true;
System.Exception failure = null;
System.Func<string> controls = () => {
    var materials = new System.Collections.Generic.List<string>(); var block = new UnityEngine.MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
    if (!block.isEmpty) throw new System.InvalidOperationException("Unknown renderer property overrides.");
    for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++) {
        renderer.GetPropertyBlock(block, slot); if (!block.isEmpty) throw new System.InvalidOperationException("Unknown material slot overrides.");
        var material = renderer.sharedMaterials[slot]; materials.Add(material.GetInstanceID() + ":" + UnityEditor.EditorJsonUtility.ToJson(material));
    }
    var lights = new System.Collections.Generic.List<string>();
    foreach (var light in System.Linq.Enumerable.OrderBy(UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None), l => l.GetInstanceID()))
        lights.Add(light.GetInstanceID() + ":" + light.transform.localToWorldMatrix.ToString("F6") + ":" + UnityEditor.EditorJsonUtility.ToJson(light));
    return json(new { cameraId = camera.GetInstanceID(), selectedCameraId = selectCamera().GetInstanceID(),
        position = camera.transform.position.ToString("F6"), rotation = camera.transform.rotation.ToString("F6"), fov = camera.fieldOfView,
        view = camera.worldToCameraMatrix.ToString("F6"), projection = camera.projectionMatrix.ToString("F6"),
        filterId = filter.GetInstanceID(), meshId = filter.sharedMesh.GetInstanceID(), transform = filter.transform.localToWorldMatrix.ToString("F6"),
        visible = filter.gameObject.activeInHierarchy && renderer.enabled, frame = UnityEngine.Time.frameCount, materials, lights });
};
string lockedControls = null;
System.Action<string, float> captureStage = (label, scale) => {
    if (filter.sharedMesh != mesh || mesh.GetInstanceID() != meshId || UnityEditor.AssetDatabase.AssetPathToGUID(assetPath) != guid)
        throw new System.InvalidOperationException("Mesh binding/identity/GUID changed.");
    if (selectCamera() != camera) throw new System.InvalidOperationException("Selected game camera changed.");
    // The existing capture helper selects Camera.main, otherwise the first camera
    // without a target. Verify its choice matches our strict preflight selection.
    var captureCamera = UnityEngine.Camera.main;
    if (!captureCamera) captureCamera = System.Linq.Enumerable.FirstOrDefault(UnityEngine.Camera.allCameras, c => c.targetTexture == null);
    if (captureCamera != camera) throw new System.InvalidOperationException("Capture helper would use a different camera; nothing captured.");
    string path = output + "/" + label + ".png";
    capture.Invoke(null, new object[] { path, 1920, 1080, false });
    string current = controls(); controlsStable &= current == lockedControls;
    string dataHash = meshHash(mesh), imageHash = sha(System.IO.File.ReadAllBytes(path)); images.Add(imageHash); hashes.Add(dataHash);
    var stage = new { label, scale, path, frame = UnityEngine.Time.frameCount, cameraId, filterId, meshId, guid,
        vertexCount = mesh.vertexCount, bodyTriangles = mesh.GetTriangles(0).Length / 3,
        boundsCenter = mesh.bounds.center.ToString("F6"), boundsSize = mesh.bounds.size.ToString("F6"), dataHash, imageHash,
        controlsUnchanged = current == lockedControls };
    stages.Add(stage); System.IO.File.WriteAllText(output + "/" + label + ".json", json(stage));
    if (!controlsStable) throw new System.InvalidOperationException("Camera/material/light/frame control changed; comparison invalid.");
};
System.Action<float> update = scale => {
    var generated = UnityEngine.Object.Instantiate(snapshot); generated.name = snapshot.name;
    try {
        if (scale != 1) {
            var vertices = generated.vertices; var anchor = generated.bounds.center; anchor.y = generated.bounds.min.y;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = anchor + (vertices[i] - anchor) * scale;
            generated.SetVertices(vertices); generated.RecalculateBounds();
        }
        attemptedUpdate = true;
        var result = persist.Invoke(null, new object[] { generated, assetPath });
        if (!object.ReferenceEquals(result, mesh) && (UnityEngine.Mesh)result != mesh) throw new System.InvalidOperationException("Production helper returned a different mesh.");
    } finally { if (generated) UnityEngine.Object.DestroyImmediate(generated); }
};
try {
    snapshot = UnityEngine.Object.Instantiate(mesh); snapshot.name = mesh.name;
    if (meshHash(snapshot) != originalHash) throw new System.InvalidOperationException("Snapshot is not identical to original CPU data.");
    camera.transform.position = new UnityEngine.Vector3(2.05f, 1, 1.25f);
    camera.transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(.85f, .7f, 2.35f) - camera.transform.position, UnityEngine.Vector3.up);
    camera.fieldOfView = 60; lockedControls = controls();
    captureStage("00-original", 1);
    update(.55f); captureStage("01-update-55pct", .55f);
    update(.80f); captureStage("02-update-80pct", .80f);
    update(1); restored = meshHash(mesh) == originalHash;
    captureStage("03-restored", 1);
} catch (System.Exception error) { failure = error; }
finally {
    try {
        if (attemptedUpdate && !restored && snapshot) { update(1); restored = meshHash(mesh) == originalHash; }
        if (!wasDirty && (!attemptedUpdate || restored)) UnityEditor.EditorUtility.ClearDirty(mesh);
    } catch (System.Exception cleanupError) { failure = failure == null ? cleanupError : new System.AggregateException(failure, cleanupError); }
    finally {
        camera.targetTexture = target; camera.transform.position = position; camera.transform.rotation = rotation; camera.fieldOfView = fov;
        UnityEngine.RenderTexture.active = activeTarget;
        if (snapshot) UnityEngine.Object.DestroyImmediate(snapshot);
    }
}
bool diskUnchanged = diskHash == sha(System.IO.File.ReadAllBytes(diskPath)) && metaHash == sha(System.IO.File.ReadAllBytes(diskPath + ".meta"));
bool identityPreserved = filter.sharedMesh == mesh && mesh.GetInstanceID() == meshId && UnityEditor.AssetDatabase.AssetPathToGUID(assetPath) == guid;
bool cameraRestored = camera.transform.position == position && camera.transform.rotation == rotation && camera.fieldOfView == fov && camera.targetTexture == target && UnityEngine.RenderTexture.active == activeTarget;
bool twoDistinctUpdates = hashes.Count == 4 && hashes[1] != hashes[0] && hashes[2] != hashes[1] && hashes[2] != hashes[0] && hashes[3] == hashes[0];
bool twoVisibleUpdates = images.Count == 4 && images[1] != images[0] && images[2] != images[1] && images[2] != images[0];
bool restoredPixelsExact = images.Count == 4 && images[3] == images[0];
bool dirtyStateRestored = UnityEditor.EditorUtility.IsDirty(mesh) == wasDirty && filter.gameObject.scene.isDirty == sceneWasDirty;
var receipt = new { scope = "Manual same-session production API test: two temporary uniform scales and full restoration; no asset saves or artistic approval",
    output, assetPath, guid, meshId, filterId, cameraId, frame, stages, twoDistinctUpdates, twoVisibleUpdates, restoredPixelsExact,
    restoredCpuData = restored, identityPreserved, cameraRestored, dirtyStateRestored, diskUnchanged, controlsStable,
    error = failure == null ? null : failure.ToString(), pending = "Review all four images; pixel changes alone are not artistic approval." };
System.IO.File.WriteAllText(output + "/receipt.json", json(receipt));
if (failure != null || !twoDistinctUpdates || !twoVisibleUpdates || !restoredPixelsExact || !restored || !identityPreserved || !cameraRestored || !dirtyStateRestored || !diskUnchanged || !controlsStable)
    throw new System.InvalidOperationException("Resident update proof incomplete or a restoration/control failed: " + output + "/receipt.json", failure);
return receipt;
