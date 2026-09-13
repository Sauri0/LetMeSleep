using System;
using System.Linq;
using UnityEngine;
using LetMeSleep.Content.Environment;
using Object = UnityEngine.Object;

// Explicit, reversible visual correction on an instantiated final Puerto map.
// No assets are saved here. The caller owns disposal and native render evidence.
public static class PuertoOceanExtentCandidate
{
    public sealed class Change : IDisposable
    {
        public MeshFilter Filter;
        public Mesh Original, Candidate;
        public int ChangedVertices, PreservedNearVertices;
        public Bounds Before, After;
        public void Dispose()
        {
            if (Filter && Filter.sharedMesh == Candidate) Filter.sharedMesh = Original;
            if (Candidate) Object.DestroyImmediate(Candidate);
        }
    }

    public static Change Apply(EnvironmentMapDefinition map)
    {
        Need(map && map.MapId == "hf-puerto-del-faro-v1", "Final Puerto instance required.");
        var filter = map.GetComponentsInChildren<MeshFilter>(true)
            .Single(f => f.name == "Water_Ocean_Pueblo");
        var original = filter.sharedMesh;
        Need(original && original.isReadable, "Readable authored ocean required.");
        Need(!map.GetComponentsInChildren<MeshCollider>(true).Any(c => c.sharedMesh == original),
            "Visual-only change: ocean mesh must not supply collision.");
        Need(filter.GetComponentsInChildren<Collider>(true).Length == 0,
            "Ocean renderer must not own colliders.");
        var vertices = original.vertices;
        var toMap = map.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        var fromMap = toMap.inverse;
        float radius = vertices.Max(v => {
            var p = toMap.MultiplyPoint3x4(v);
            return Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
        });
        Need(Mathf.Abs(radius - 150f) < .01f, "Expected original 150m ocean boundary; do not apply twice.");
        var change = new Change { Filter = filter, Original = original, Before = original.bounds };
        try
        {
            change.Candidate = Object.Instantiate(original);
            change.Candidate.name = "Puerto_Ocean_Horizon400_VisualOnly";
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = toMap.MultiplyPoint3x4(vertices[i]);
                float r = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
                if (r <= 80f + .0001f) { change.PreservedNearVertices++; continue; }
                // Continuous square rings: no split vertices or seam, unchanged shoreline.
                float expanded = 80f + (r - 80f) * (400f - 80f) / (150f - 80f);
                float scale = expanded / r;
                p.x *= scale; p.z *= scale;
                vertices[i] = fromMap.MultiplyPoint3x4(p);
                change.ChangedVertices++;
            }
            Need(change.ChangedVertices > 0 && change.PreservedNearVertices > 0, "Both regions required.");
            change.Candidate.vertices = vertices;
            change.Candidate.RecalculateBounds();
            change.After = change.Candidate.bounds;
            Need(change.Candidate.vertexCount == original.vertexCount &&
                 change.Candidate.subMeshCount == original.subMeshCount &&
                 change.Candidate.blendShapeCount == original.blendShapeCount,
                 "Mesh topology and authored shape channels must be retained.");
            for (int sub = 0; sub < original.subMeshCount; sub++)
                Need(original.GetIndices(sub).SequenceEqual(change.Candidate.GetIndices(sub)), "Indices changed.");
            filter.sharedMesh = change.Candidate;
            return change;
        }
        catch { change.Dispose(); throw; }
    }
    static void Need(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
