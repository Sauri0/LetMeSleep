// MANUAL eval_file only, in the Director's Play-mode Editor. Never an autorun.
// Captures one live instance/camera before and after UploadMeshData(false).
// No scene/prefab/material/mesh asset is saved or reimported. No geometry setter.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Run only in the Director's live Humantraining instance.");
var camera = UnityEngine.Camera.main;
if (!camera) camera = System.Linq.Enumerable.FirstOrDefault(UnityEngine.Camera.allCameras, c => c.targetTexture == null);
if (!camera) throw new System.InvalidOperationException("No active game camera.");
var names = new System.Collections.Generic.HashSet<string> {
    "Cushion_m0p57", "Cushion_0p57", "Back_Pad_m0p46", "Back_Pad_0p46",
    "Seat_Pad_m0p46", "Seat_Pad_0p46", "Seat_Throw"
};
var filters = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(
    System.Linq.Enumerable.Where(UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsSortMode.None),
        f => f.gameObject.activeInHierarchy && names.Contains(f.name)), f => f.name));
if (filters.Length != 7 || System.Linq.Enumerable.Count(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(filters, f => f.name))) != 7
    || System.Linq.Enumerable.Count(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(filters, f => f.transform.root.GetInstanceID()))) != 1)
    throw new System.InvalidOperationException("Require exactly seven textile parts in one active house instance; nothing uploaded.");
var meshes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(filters, f => f.sharedMesh)));
if (System.Linq.Enumerable.Any(meshes, m => !m || !m.isReadable)) throw new System.InvalidOperationException("All textile meshes must remain readable.");
var originalBindings = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(filters, f => f.sharedMesh));
var originalPosition = camera.transform.position;
var originalRotation = camera.transform.rotation;
var originalFov = camera.fieldOfView;
var originalTarget = camera.targetTexture;
var originalActiveTarget = UnityEngine.RenderTexture.active;
var output = "N:/LetMeSleep/Validation/Alfa-VisualRecovery/textile-gpu-probe-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
System.IO.Directory.CreateDirectory(output);
System.Func<object, string> json = value => Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);
System.Func<byte[], string> sha = bytes => {
    using (var hash = System.Security.Cryptography.SHA256.Create())
        return System.BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
};
System.Func<UnityEngine.Vector3, float[]> vector = v => new[] { v.x, v.y, v.z };
System.Func<UnityEngine.Quaternion, float[]> quaternion = q => new[] { q.x, q.y, q.z, q.w };
System.Func<UnityEngine.Matrix4x4, float[]> matrix = m => {
    var values = new float[16]; for (int i = 0; i < 16; i++) values[i] = m[i]; return values;
};
System.Func<UnityEngine.Transform, string> hierarchy = t => {
    string path = t.name; for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path; return path;
};
System.Func<UnityEngine.Mesh, object> meshState = mesh => {
    var vertices = mesh.vertices; var normals = mesh.normals; var indices = mesh.GetTriangles(0);
    int flat = 0; float maxAngle = 0;
    if (normals.Length != vertices.Length) throw new System.InvalidOperationException("Incomplete normals: " + mesh.name);
    for (int i = 0; i < indices.Length; i += 3) {
        float angle = UnityEngine.Mathf.Max(UnityEngine.Vector3.Angle(normals[indices[i]], normals[indices[i + 1]]), UnityEngine.Vector3.Angle(normals[indices[i]], normals[indices[i + 2]]));
        if (angle < .1f) flat++; maxAngle = UnityEngine.Mathf.Max(maxAngle, angle);
    }
    string fingerprint;
    using (var stream = new System.IO.MemoryStream()) {
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
            writer.Write(vertices.Length);
            foreach (var v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            foreach (var n in normals) { writer.Write(n.x); writer.Write(n.y); writer.Write(n.z); }
            foreach (var uv in mesh.uv) { writer.Write(uv.x); writer.Write(uv.y); }
            foreach (var uv in mesh.uv2) { writer.Write(uv.x); writer.Write(uv.y); }
            writer.Write(mesh.subMeshCount);
            for (int slot = 0; slot < mesh.subMeshCount; slot++) {
                var tris = mesh.GetTriangles(slot); writer.Write(tris.Length); foreach (int index in tris) writer.Write(index);
            }
        }
        fingerprint = sha(stream.ToArray());
    }
    return new { id = mesh.GetInstanceID(), name = mesh.name, path = UnityEditor.AssetDatabase.GetAssetPath(mesh),
        vertexCount = vertices.Length, normalCount = normals.Length, bodyTriangles = indices.Length / 3,
        flatTriangles = flat, maximumCornerAngle = maxAngle, fingerprint,
        readable = mesh.isReadable, dirty = UnityEditor.EditorUtility.IsDirty(mesh),
        boundsCenter = vector(mesh.bounds.center), boundsSize = vector(mesh.bounds.size) };
};
System.Func<object> cameraState = () => new {
    id = camera.GetInstanceID(), position = vector(camera.transform.position), rotation = quaternion(camera.transform.rotation),
    fov = camera.fieldOfView, aspect = camera.aspect, projection = matrix(camera.projectionMatrix), view = matrix(camera.worldToCameraMatrix),
    near = camera.nearClipPlane, far = camera.farClipPlane, mask = camera.cullingMask,
    target = camera.targetTexture ? camera.targetTexture.GetInstanceID() : 0, orthographic = camera.orthographic,
    clearFlags = camera.clearFlags.ToString(), rect = new[] { camera.rect.x, camera.rect.y, camera.rect.width, camera.rect.height }
};
System.Func<object> lightingState = () => {
    var lights = new System.Collections.Generic.List<object>();
    foreach (var light in System.Linq.Enumerable.OrderBy(UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None), l => l.GetInstanceID()))
        lights.Add(new { id = light.GetInstanceID(), active = light.gameObject.activeInHierarchy, enabled = light.enabled,
            position = vector(light.transform.position), rotation = quaternion(light.transform.rotation), serialized = UnityEditor.EditorJsonUtility.ToJson(light) });
    return new { lights, ambientMode = UnityEngine.RenderSettings.ambientMode.ToString(),
        ambientIntensity = UnityEngine.RenderSettings.ambientIntensity,
        ambientLight = UnityEngine.RenderSettings.ambientLight.ToString("F6"),
        ambientSky = UnityEngine.RenderSettings.ambientSkyColor.ToString("F6"),
        ambientEquator = UnityEngine.RenderSettings.ambientEquatorColor.ToString("F6"),
        ambientGround = UnityEngine.RenderSettings.ambientGroundColor.ToString("F6"),
        skybox = UnityEngine.RenderSettings.skybox ? UnityEditor.EditorJsonUtility.ToJson(UnityEngine.RenderSettings.skybox) : "null" };
};
System.Func<object> bindingState = () => {
    var parts = new System.Collections.Generic.List<object>();
    foreach (var filter in filters) {
        var renderer = filter.GetComponent<UnityEngine.MeshRenderer>();
        if (!renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy) throw new System.InvalidOperationException("Textile renderer became inactive.");
        var block = new UnityEngine.MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
        var materials = new System.Collections.Generic.List<object>();
        for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++) {
            var material = renderer.sharedMaterials[slot]; var slotBlock = new UnityEngine.MaterialPropertyBlock(); renderer.GetPropertyBlock(slotBlock, slot);
            // These authored textiles have no overrides. Abort if an unknown MPB
            // would make the before/after controls unverifiable.
            if (!block.isEmpty || !slotBlock.isEmpty) throw new System.InvalidOperationException("Unexpected textile material property block.");
            materials.Add(new { id = material.GetInstanceID(), name = material.name, shader = material.shader.name,
                serialized = UnityEditor.EditorJsonUtility.ToJson(material), dirty = UnityEditor.EditorUtility.IsDirty(material) });
        }
        parts.Add(new { filterId = filter.GetInstanceID(), path = hierarchy(filter.transform), rootId = filter.transform.root.GetInstanceID(),
            scene = filter.gameObject.scene.path, sceneDirty = filter.gameObject.scene.isDirty, meshId = filter.sharedMesh.GetInstanceID(),
            transform = matrix(filter.transform.localToWorldMatrix), rendererId = renderer.GetInstanceID(), staticBatch = renderer.isPartOfStaticBatch, materials });
    }
    return parts;
};
UnityEngine.RenderTexture target = null;
UnityEngine.Color32[] beforePixels = null, afterPixels = null;
object before = null, after = null;
string beforeMeshes = null, beforeBindings = null, beforeCamera = null, beforeLights = null;
bool controlsUnchanged = false;
int beforeFrame = -1, afterFrame = -1;
var originalCamera = json(cameraState());
System.Func<string, UnityEngine.Color32[]> capture = path => {
    UnityEngine.Texture2D pixels = null;
    try {
        UnityEngine.RenderTexture.active = target;
        UnityEngine.GL.Clear(true, true, UnityEngine.Color.clear);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
        UnityEngine.RenderTexture.active = target;
        pixels = new UnityEngine.Texture2D(1920, 1080, UnityEngine.TextureFormat.RGB24, false);
        pixels.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
        System.IO.File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(pixels));
        return pixels.GetPixels32();
    } finally { if (pixels) UnityEngine.Object.DestroyImmediate(pixels); }
};
try {
    target = new UnityEngine.RenderTexture(1920, 1080, 24);
    if (!target.Create()) throw new System.InvalidOperationException("Could not allocate the probe render target.");
    camera.transform.position = new UnityEngine.Vector3(2.05f, 1.0f, 1.25f);
    camera.transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(.85f, .7f, 2.35f) - camera.transform.position, UnityEngine.Vector3.up);
    camera.fieldOfView = 60; camera.targetTexture = target;
    beforeMeshes = json(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(meshes, meshState)));
    beforeBindings = json(bindingState()); beforeCamera = json(cameraState()); beforeLights = json(lightingState());
    beforeFrame = UnityEngine.Time.frameCount;
    beforePixels = capture(output + "/before.png");
    // Dump AFTER the before render, still BEFORE the upload. Also reject a render
    // callback changing any control or CPU mesh during the before capture.
    before = new { frame = beforeFrame, camera = cameraState(), lights = lightingState(), parts = bindingState(), meshes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(meshes, meshState)) };
    System.IO.File.WriteAllText(output + "/before.json", json(before));
    if (beforeMeshes != json(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(meshes, meshState))) || beforeBindings != json(bindingState()) || beforeCamera != json(cameraState()) || beforeLights != json(lightingState()))
        throw new System.InvalidOperationException("A control changed during BEFORE render; no upload performed.");
    foreach (var mesh in meshes) mesh.UploadMeshData(false);
    afterPixels = capture(output + "/after.png");
    afterFrame = UnityEngine.Time.frameCount;
    after = new { frame = afterFrame, camera = cameraState(), lights = lightingState(), parts = bindingState(), meshes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(meshes, meshState)) };
    System.IO.File.WriteAllText(output + "/after.json", json(after));
    controlsUnchanged = beforeFrame == afterFrame && beforeMeshes == json(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(meshes, meshState)))
        && beforeBindings == json(bindingState()) && beforeCamera == json(cameraState()) && beforeLights == json(lightingState());
} catch (System.Exception error) {
    System.IO.File.WriteAllText(output + "/failure.json", json(new { error = error.ToString(), scope = "Incomplete probe; do not infer a graphics cause" }));
    throw;
} finally {
    camera.targetTexture = originalTarget;
    camera.transform.position = originalPosition; camera.transform.rotation = originalRotation; camera.fieldOfView = originalFov;
    UnityEngine.RenderTexture.active = originalActiveTarget;
    if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
}
bool cameraRestored = originalCamera == json(cameraState());
bool bindingsPreserved = true;
for (int i = 0; i < filters.Length; i++) bindingsPreserved &= filters[i] && filters[i].sharedMesh == originalBindings[i];
long difference = 0; int changedPixels = 0;
for (int i = 0; i < beforePixels.Length; i++) {
    int delta = System.Math.Abs(beforePixels[i].r - afterPixels[i].r) + System.Math.Abs(beforePixels[i].g - afterPixels[i].g) + System.Math.Abs(beforePixels[i].b - afterPixels[i].b);
    difference += delta; if (delta != 0) changedPixels++;
}
var receipt = new { scope = "Same live house/camera before and after textile UploadMeshData(false); no asset save, no rebuild, no artistic approval",
    output, cameraId = camera.GetInstanceID(), rootId = filters[0].transform.root.GetInstanceID(),
    uniqueMeshesUploaded = meshes.Length, beforeFrame, afterFrame, controlsUnchanged, cameraRestored, bindingsPreserved,
    changedPixels, totalPixels = beforePixels.Length, meanAbsoluteRgbByteDifference = difference / (beforePixels.Length * 3.0),
    beforePngSha256 = sha(System.IO.File.ReadAllBytes(output + "/before.png")), afterPngSha256 = sha(System.IO.File.ReadAllBytes(output + "/after.png")),
    pending = "Review both PNGs; unchanged CPU data or changed pixels alone do not approve the art or establish the original cause." };
System.IO.File.WriteAllText(output + "/receipt.json", json(receipt));
if (!controlsUnchanged || !cameraRestored || !bindingsPreserved) throw new System.InvalidOperationException("Probe controls/restoration failed; see receipt.json and do not treat this as a controlled comparison.");
return receipt;
