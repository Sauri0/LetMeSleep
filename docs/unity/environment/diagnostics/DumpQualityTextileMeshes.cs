// READ ONLY eval_file for the Director's active Editor. No rebuild/reload or mutation.
System.Func<UnityEngine.Mesh, object> describe = mesh => {
    if (!mesh) return null;
    if (!mesh.isReadable) return new { name = mesh.name, id = mesh.GetInstanceID(), readable = false };
    var vertices = mesh.vertices;
    var normals = mesh.normals;
    var triangles = mesh.subMeshCount > 0 ? mesh.GetTriangles(0) : new int[0];
    int flatTriangles = 0;
    float maximumCornerAngle = 0;
    if (normals.Length == vertices.Length) for (int i = 0; i < triangles.Length; i += 3) {
        var a = normals[triangles[i]];
        float angle = UnityEngine.Mathf.Max(UnityEngine.Vector3.Angle(a, normals[triangles[i + 1]]), UnityEngine.Vector3.Angle(a, normals[triangles[i + 2]]));
        if (angle < .1f) flatTriangles++;
        maximumCornerAngle = UnityEngine.Mathf.Max(maximumCornerAngle, angle);
    }
    string fingerprint;
    using (var stream = new System.IO.MemoryStream()) {
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
            foreach (var value in vertices) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
            foreach (var value in normals) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
            foreach (var value in triangles) writer.Write(value);
        }
        using (var hash = System.Security.Cryptography.SHA256.Create())
            fingerprint = System.BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
    }
    return new { name = mesh.name, id = mesh.GetInstanceID(), readable = true,
        assetPath = UnityEditor.AssetDatabase.GetAssetPath(mesh), vertexCount = vertices.Length,
        normalCount = normals.Length, bodyTriangles = triangles.Length / 3, flatTriangles,
        maximumCornerAngle, fingerprint, bounds = mesh.bounds.ToString("F5") };
};
var result = new System.Collections.Generic.List<object>();
foreach (var filter in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) {
    if (!(filter.name.StartsWith("Cushion_") || filter.name.StartsWith("Back_Pad_") || filter.name.StartsWith("Seat_Pad_") || filter.name == "Seat_Throw")) continue;
    var mesh = filter.sharedMesh;
    var renderer = filter.GetComponent<UnityEngine.MeshRenderer>();
    string path = filter.name;
    for (var parent = filter.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
    string assetPath = mesh ? UnityEditor.AssetDatabase.GetAssetPath(mesh) : "";
    // Loading the asset does not reimport it. Also look up a named asset when a
    // runtime mesh has become detached or has been replaced by a batching copy.
    var asset = string.IsNullOrEmpty(assetPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(assetPath);
    var candidates = new System.Collections.Generic.List<object>();
    if (mesh) foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Mesh " + mesh.name)) {
        string candidatePath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (!candidatePath.EndsWith("/" + mesh.name + ".asset")) continue;
        candidates.Add(describe(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(candidatePath)));
    }
    var materials = new System.Collections.Generic.List<object>();
    if (renderer) foreach (var material in renderer.sharedMaterials) if (material)
        materials.Add(new { name = material.name, shader = material.shader ? material.shader.name : "null", keywords = material.shaderKeywords });
    result.Add(new { path, scene = filter.gameObject.scene.path, active = filter.gameObject.activeInHierarchy,
        rendererEnabled = renderer && renderer.enabled, staticBatch = renderer && renderer.isPartOfStaticBatch,
        runtimeMesh = describe(mesh), assetMesh = describe(asset), sameInstance = mesh && asset && mesh == asset,
        candidates, materials });
}
return result;
