using System;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Frozen-pose crossfade for owner switches that the Animator cannot blend itself (gait graph to
    /// controller and back, immediate Play after a cut). Every frame the evaluated base pose is captured;
    /// Begin() blends from the last captured pose to the newly evaluated one over a short time.
    /// Rotations and positions of the rig bones only; scale belongs to secondary motion. An optional
    /// "fast" bone set (the legs) can use a shorter blend, so a body that already moves steps into its
    /// stride at once instead of skating on legs frozen by the fade.
    /// </summary>
    public sealed class PoseCrossfade
    {
        private readonly Transform[] bones;
        private readonly bool[] fast;
        private readonly Vector3[] positions, fromPositions;
        private readonly Quaternion[] rotations, fromRotations;
        private bool captured;
        private float elapsed, duration, fastDuration;

        public bool Active => elapsed < Mathf.Max(duration, fastDuration) && Mathf.Max(duration, fastDuration) > 0;
        public float Weight => duration > 0 && elapsed < duration ? Smooth(elapsed / duration) : 1;
        public float FastWeight => fastDuration > 0 && elapsed < fastDuration ? Smooth(elapsed / fastDuration) : 1;
        public int BoneCount => bones.Length;

        public PoseCrossfade(Transform[] rigBones, Func<Transform, bool> fastBone = null)
        {
            bones = rigBones ?? Array.Empty<Transform>();
            fast = new bool[bones.Length];
            if (fastBone != null)
                for (int i = 0; i < bones.Length; i++) fast[i] = bones[i] && fastBone(bones[i]);
            positions = new Vector3[bones.Length]; fromPositions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length]; fromRotations = new Quaternion[bones.Length];
        }

        public static Transform[] CollectRig(Transform rigRoot)
        {
            if (!rigRoot) return Array.Empty<Transform>();
            return rigRoot.GetComponentsInChildren<Transform>(true);
        }

        /// <summary>Human leg chain (thigh, shin, foot, toe and their sockets), not the hips that carry the torso.</summary>
        public static bool IsLegBone(Transform bone)
        {
            for (var item = bone; item; item = item.parent)
            {
                string name = item.name;
                if (name.StartsWith("UpperLeg.", StringComparison.Ordinal)) return true;
                if (name == "Hips" || name == "Root") return false;
            }
            return false;
        }

        /// <summary>Start blending from the last captured pose. Ignored before the first capture.</summary>
        public void Begin(float seconds) => Begin(seconds, seconds);

        /// <summary>Start blending; the fast bone set uses fastSeconds.</summary>
        public void Begin(float seconds, float fastSeconds)
        {
            if (!captured || !(seconds > 0) || float.IsInfinity(seconds)) return;
            if (!(fastSeconds > 0) || float.IsInfinity(fastSeconds)) fastSeconds = seconds;
            Array.Copy(positions, fromPositions, bones.Length);
            Array.Copy(rotations, fromRotations, bones.Length);
            duration = seconds; fastDuration = fastSeconds; elapsed = 0;
        }

        public void Cancel() { duration = fastDuration = 0; elapsed = 0; }

        /// <summary>Call after the base pose was evaluated this frame and before procedural writers.</summary>
        public void Apply(float deltaSeconds)
        {
            if (Active)
            {
                if (deltaSeconds > 0 && !float.IsInfinity(deltaSeconds)) elapsed += deltaSeconds;
                float weight = Weight, fastWeight = FastWeight;
                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];
                    if (!bone) continue;
                    float w = fast[i] ? fastWeight : weight;
                    if (w >= 1) continue;
                    bone.localPosition = Vector3.LerpUnclamped(fromPositions[i], bone.localPosition, w);
                    bone.localRotation = Quaternion.SlerpUnclamped(fromRotations[i], bone.localRotation, w);
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
