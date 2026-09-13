using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Content.Characters.Editor
{
    // Unity's calculated blink normals can modify the chin/cheeks even though
    // the FBX contains only eyelid position/normal indices. Restrict each
    // sample's normal/tangent deltas to triangles actually deformed by it.
    public sealed class HumanEyelidNormalsPostprocessor : AssetPostprocessor
    {
        private const string Models = "Assets/LetMeSleep/Content/Characters/Models/";
        private static readonly HashSet<string> BlinkNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Blink25.L", "Blink50.L", "Blink75.L", "Blink.L",
            "Blink25.R", "Blink50.R", "Blink75.R", "Blink.R"
        };

        private sealed class Frame
        {
            internal string Name;
            internal float Weight;
            internal Vector3[] Position, Normal, Tangent;
        }

        public override uint GetVersion() => 1;
        public override int GetPostprocessOrder() => 1000;

        private void OnPostprocessModel(GameObject root)
        {
            if (!string.Equals(assetPath, Models + "LMS_Human_alpha.fbx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assetPath, Models + "LMS_HumanMenu.fbx", StringComparison.OrdinalIgnoreCase))
                return;

            SkinnedMeshRenderer head = null;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "HumanHead") continue;
                if (head != null) throw new InvalidOperationException("Duplicate HumanHead on " + assetPath);
                head = renderer;
            }
            if (head == null || head.sharedMesh == null)
                throw new InvalidOperationException("Human eyelid import requires HumanHead on " + assetPath);
            Repair(head);
        }

        private static void Repair(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh;
            int vertexCount = mesh.vertexCount;
            int[] triangles = mesh.triangles;
            var frames = new List<Frame>();
            var found = new HashSet<string>(StringComparer.Ordinal);
            var weights = new float[mesh.blendShapeCount];
            Bounds originalBounds = mesh.bounds;
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                string name = mesh.GetBlendShapeName(shape);
                bool blink = BlinkNames.Contains(name);
                if (blink) found.Add(name);
                weights[shape] = renderer.GetBlendShapeWeight(shape);
                int frameCount = mesh.GetBlendShapeFrameCount(shape);
                if (frameCount == 0) throw new InvalidOperationException("Empty blendshape " + name);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frame = new Frame
                    {
                        Name = name, Weight = mesh.GetBlendShapeFrameWeight(shape, frameIndex),
                        Position = new Vector3[vertexCount], Normal = new Vector3[vertexCount],
                        Tangent = new Vector3[vertexCount]
                    };
                    mesh.GetBlendShapeFrameVertices(shape, frameIndex, frame.Position, frame.Normal, frame.Tangent);
                    if (blink)
                    {
                        var moving = new bool[vertexCount];
                        for (int i = 0; i < vertexCount; i++) moving[i] = frame.Position[i].sqrMagnitude > 1e-14f;
                        bool[] allowed = EyelidMorphSupport.Build(moving, triangles);
                        EyelidMorphSupport.Filter(frame.Normal, allowed);
                        EyelidMorphSupport.Filter(frame.Tangent, allowed);
                    }
                    frames.Add(frame);
                }
            }
            if (!found.SetEquals(BlinkNames))
                throw new InvalidOperationException("HumanHead must contain all eight authored blink shapes.");

            // Preserve names, ordering, frames, frame weights and ALL position
            // deltas. Base normals, mesh vertices, skin and materials are untouched.
            mesh.ClearBlendShapes();
            foreach (var frame in frames)
                mesh.AddBlendShapeFrame(frame.Name, frame.Weight, frame.Position, frame.Normal, frame.Tangent);
            mesh.bounds = originalBounds;
            for (int shape = 0; shape < weights.Length; shape++)
                renderer.SetBlendShapeWeight(shape, weights[shape]);
        }
    }
}
