using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LetMeSleep.Content.Environment.Higgsfield
{
    /// <summary>Explicit runtime opt-in. Has no Awake/Start hook and never discovers an approved set.</summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldStaticBatching : MonoBehaviour
    {
        public bool Attempted { get; private set; }
        public int EligibleCount { get; private set; }
        public int BatchedCount { get; private set; }
        public bool StructurePreserved { get; private set; }

        /// <summary>
        /// Call after final map placement. Caller declares each supplied renderer immutable for the
        /// instance lifetime and supplies all moving/excluded roots (empty only after explicit audit).
        /// No child transform, mesh or materials of a combined object may subsequently be changed.
        /// </summary>
        public int CombineOnce(MeshRenderer[] approvedImmutableRenderers, Transform[] excludedRoots)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Runtime instance required.");
            if (Attempted) throw new InvalidOperationException("Batching already attempted; recreate the instance to retry.");
            if (approvedImmutableRenderers == null || excludedRoots == null)
                throw new ArgumentNullException("Explicit approved renderers and excluded roots are required.");
            if (excludedRoots.Any(t => t == null || !t.IsChildOf(transform)))
                throw new ArgumentException("Excluded roots must exist within this map.");
            if (approvedImmutableRenderers.Any(r => r == null || !r.transform.IsChildOf(transform)))
                throw new ArgumentException("Approved renderers must exist within this map.");

            var candidates = approvedImmutableRenderers.Distinct().Where(r => Eligible(r, excludedRoots)).ToArray();
            EligibleCount = candidates.Length;
            Attempted = true;
            if (candidates.Length < 2) { StructurePreserved = true; return 0; }

            // Keep component identities, collider meshes and every transform, including EMPTY anchors.
            // Eligible MeshFilter mesh references intentionally change as Unity builds combined buffers.
            var poses = GetComponentsInChildren<Transform>(true).Select(t => new Pose(t)).ToArray();
            var renderers = GetComponentsInChildren<Renderer>(true);
            var materials = renderers.Select(r => r.sharedMaterials).ToArray();
            var colliders = GetComponentsInChildren<Collider>(true);
            var colliderMeshes = colliders.OfType<MeshCollider>().ToDictionary(c => c, c => c.sharedMesh);
            var candidateObjects = new HashSet<GameObject>(candidates.Select(r => r.gameObject));
            var excludedMeshes = GetComponentsInChildren<MeshFilter>(true)
                .Where(f => !candidateObjects.Contains(f.gameObject)).ToDictionary(f => f, f => f.sharedMesh);
            var skinnedMeshes = renderers.OfType<SkinnedMeshRenderer>().ToDictionary(r => r, r => r.sharedMesh);

            StaticBatchingUtility.Combine(candidates.Select(r => r.gameObject).ToArray(), gameObject);
            BatchedCount = candidates.Count(r => r.isPartOfStaticBatch);
            StructurePreserved = poses.All(p => p.Unchanged()) &&
                renderers.SequenceEqual(GetComponentsInChildren<Renderer>(true)) &&
                colliders.SequenceEqual(GetComponentsInChildren<Collider>(true)) &&
                renderers.Select((r, i) => r.sharedMaterials.SequenceEqual(materials[i])).All(v => v) &&
                colliderMeshes.All(p => p.Key != null && p.Key.sharedMesh == p.Value) &&
                excludedMeshes.All(p => p.Key != null && p.Key.sharedMesh == p.Value) &&
                skinnedMeshes.All(p => p.Key != null && p.Key.sharedMesh == p.Value);
            if (!StructurePreserved)
                throw new InvalidOperationException("Batching changed map structure or excluded mesh references. Discard this instance.");
            return BatchedCount;
        }

        bool Eligible(MeshRenderer renderer, Transform[] excludedRoots)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.isPartOfStaticBatch ||
                excludedRoots.Any(t => renderer.transform.IsChildOf(t))) return false;
            for (var t = renderer.transform; t != null; t = t.parent)
                if (t.GetComponent<HiggsfieldLowPolyWater>() != null || t.GetComponent<Animator>() != null ||
                    t.GetComponent<Animation>() != null || t.GetComponent<Rigidbody>() != null ||
                    t.GetComponent<SkinnedMeshRenderer>() != null) return false;
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0 || mesh.blendShapeCount != 0) return false;
            var materials = renderer.sharedMaterials;
            return materials.Length == mesh.subMeshCount && materials.Length > 0 && materials.All(m =>
                m != null && m.shader != null &&
                string.Equals(m.GetTag("DisableBatching", false, "False"), "False", StringComparison.OrdinalIgnoreCase));
        }

        sealed class Pose
        {
            readonly Transform target, parent;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            public Pose(Transform t)
            { target = t; parent = t.parent; position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
            public bool Unchanged() => target != null && target.parent == parent &&
                target.localPosition == position && target.localRotation == rotation && target.localScale == scale;
        }
    }
}
