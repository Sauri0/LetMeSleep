using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Frozen-pose crossfade for owner switches that the Animator cannot blend itself (gait graph to
    /// controller and back, immediate Play after a cut). Every frame the evaluated base pose is captured;
    /// Begin() blends from the last captured pose to the newly evaluated one over a short time.
    /// Rotations and positions of the rig bones only; scale belongs to secondary motion.
    /// </summary>
    public sealed class PoseCrossfade
    {
        private readonly Transform[] bones;
        private readonly Vector3[] positions, fromPositions;
        private readonly Quaternion[] rotations, fromRotations;
        private bool captured;
        private float elapsed, duration;

        public bool Active => duration > 0 && elapsed < duration;
        public float Weight => Active ? Smooth(elapsed / duration) : 1;
        public int BoneCount => bones.Length;

        public PoseCrossfade(Transform[] rigBones)
        {
            bones = rigBones ?? System.Array.Empty<Transform>();
            positions = new Vector3[bones.Length]; fromPositions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length]; fromRotations = new Quaternion[bones.Length];
        }

        public static Transform[] CollectRig(Transform rigRoot)
        {
            if (!rigRoot) return System.Array.Empty<Transform>();
            return rigRoot.GetComponentsInChildren<Transform>(true);
        }

        /// <summary>Start blending from the last captured pose. Ignored before the first capture.</summary>
        public void Begin(float seconds)
        {
            if (!captured || !(seconds > 0) || float.IsInfinity(seconds)) return;
            System.Array.Copy(positions, fromPositions, bones.Length);
            System.Array.Copy(rotations, fromRotations, bones.Length);
            duration = seconds; elapsed = 0;
        }

        public void Cancel() { duration = 0; elapsed = 0; }

        /// <summary>Call after the base pose was evaluated this frame and before procedural writers.</summary>
        public void Apply(float deltaSeconds)
        {
            if (Active)
            {
                if (deltaSeconds > 0 && !float.IsInfinity(deltaSeconds)) elapsed += deltaSeconds;
                float weight = Weight;
                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];
                    if (!bone) continue;
                    bone.localPosition = Vector3.LerpUnclamped(fromPositions[i], bone.localPosition, weight);
                    bone.localRotation = Quaternion.SlerpUnclamped(fromRotations[i], bone.localRotation, weight);
                }
            }
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                if (!bone) continue;
                positions[i] = bone.localPosition; rotations[i] = bone.localRotation;
            }
            captured = true;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
