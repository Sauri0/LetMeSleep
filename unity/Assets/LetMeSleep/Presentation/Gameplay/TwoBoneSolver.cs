using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Analytic two-bone IK on real transforms: rotates the two segments about their joints and never
    /// stretches local offsets. The bend plane keeps the current middle joint (or the pole hint).
    /// </summary>
    public static class TwoBoneSolver
    {
        /// <summary>Maximum reach so the inner middle-joint angle stays at or below maxInnerDegrees.</summary>
        public static float MaximumReach(float upper, float lower, float maxInnerDegrees)
        {
            float inner = Mathf.Clamp(maxInnerDegrees, 1f, 180f) * Mathf.Deg2Rad;
            return Mathf.Sqrt(Mathf.Max(0f, upper * upper + lower * lower - 2f * upper * lower * Mathf.Cos(inner)));
        }

        /// <summary>Inner angle at the middle joint, in degrees (180 = straight).</summary>
        public static float InnerAngle(Vector3 root, Vector3 middle, Vector3 end) =>
            Vector3.Angle(root - middle, end - middle);

        /// <summary>Returns the residual distance between the end and the target after solving.</summary>
        public static float Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 poleHint,
            float maxInnerDegrees = 180f)
        {
            if (!upper || !lower || !end || !Finite(target)) return float.PositiveInfinity;
            Vector3 root = upper.position, middle = lower.position, tip = end.position;
            float a = Vector3.Distance(root, middle), b = Vector3.Distance(middle, tip);
            if (a < 1e-4f || b < 1e-4f || !Finite(root) || !Finite(middle) || !Finite(tip)) return float.PositiveInfinity;
            Vector3 delta = target - root;
            Vector3 axis = delta.sqrMagnitude > 1e-10f ? delta.normalized : (tip - root).normalized;
            if (axis.sqrMagnitude < .5f) return float.PositiveInfinity;
            float maximum = Mathf.Min(a + b - 1e-4f, MaximumReach(a, b, maxInnerDegrees));
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 1e-4f, Mathf.Max(Mathf.Abs(a - b) + 1e-4f, maximum));
            Vector3 bend = Vector3.ProjectOnPlane(middle - root, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(poleHint, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.forward, axis);
            float along = (a * a - b * b + length * length) / (2f * length);
            Vector3 solvedMiddle = root + axis * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 solvedTip = root + axis * length;
            upper.rotation = Quaternion.FromToRotation(middle - root, solvedMiddle - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, solvedTip - lower.position) * lower.rotation;
            return Vector3.Distance(end.position, target);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
