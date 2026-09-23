using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Content.Characters.Editor
{
    /// <summary>
    /// v0.3.0 modular review r1 #2 (and the navy "beard" of review #10): the flat skin facets under the human's jaw and
    /// chin face the floor, so in Unity they only receive the scene's ground ambient. In the customization viewer
    /// (ground ambient ~0.05) the facet at the jaw angle rendered pure black from behind; in the blue lobby the jaw
    /// underside rendered navy. This import step bends the normal of each downward-facing (more than ~7 deg) skin facet
    /// of HumanHead between
    /// 1.20 m and 1.50 m halfway toward "out of the head axis" (about 45 deg down and out), so it takes the side
    /// (equator) ambient and the key light like the jaw planes above it. Each facet keeps one flat normal (it only
    /// changes direction); positions, UVs, bones and the blink shapes are untouched. Applies to the combat model and to
    /// the living-menu model (same head).
    /// </summary>
    public sealed class HumanJawNormalsPostprocessor : AssetPostprocessor
    {
        private const string Models = "Assets/LetMeSleep/Content/Characters/Models/";
        internal const float MinimumHeight = 1.20f, MaximumHeight = 1.50f, DownwardLimit = -.12f, Bend = .55f, Lift = 0f;

        public override uint GetVersion() => 4;
        // After HumanEyelidNormalsPostprocessor (1000), which rebuilds the blink normal deltas.
        public override int GetPostprocessOrder() => 1010;

        private void OnPostprocessModel(GameObject root)
        {
            if (!string.Equals(assetPath, Models + "LMS_Human_alpha.fbx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assetPath, Models + "LMS_HumanMenu.fbx", StringComparison.OrdinalIgnoreCase))
                return;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "HumanHead" || renderer.sharedMesh == null) continue;
                int bent = BendJawUnderside(renderer, root.transform);
                if (bent == 0) throw new InvalidOperationException("Human jaw normals: no downward skin facet under the jaw on " + assetPath);
            }
        }

        /// <summary>Returns the number of bent triangles.</summary>
        internal static int BendJawUnderside(SkinnedMeshRenderer renderer, Transform root)
        {
            var mesh = renderer.sharedMesh;
            int skin = Array.FindIndex(renderer.sharedMaterials, material => material != null && material.name == "Human_Skin");
            if (skin < 0 || skin >= mesh.subMeshCount) return 0;
            var toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var fromRoot = toRoot.inverse;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length) return 0;
            var triangles = mesh.GetTriangles(skin);
            var target = new Dictionary<int, Vector3>();
            int bent = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = toRoot.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = toRoot.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                var centre = (a + b + c) / 3f;
                if (centre.y <= MinimumHeight || centre.y >= MaximumHeight) continue;
                // The imported (authored, flat) normal of the facet decides; its winding is not needed.
                var normal = toRoot.MultiplyVector(normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]).normalized;
                if (normal.y >= DownwardLimit) continue;
                var outward = new Vector3(centre.x, 0f, centre.z);
                if (outward.sqrMagnitude < 1e-8f) continue;
                var aim = (outward.normalized + Vector3.up * Lift).normalized;
                var bentNormal = (normal * (1f - Bend) + aim * Bend).normalized;
                var local = fromRoot.MultiplyVector(bentNormal).normalized;
                for (int k = 0; k < 3; k++) target[triangles[i + k]] = local;
                bent++;
            }
            foreach (var pair in target) normals[pair.Key] = pair.Value;
            if (target.Count > 0) mesh.normals = normals;
            return bent;
        }
    }
}
