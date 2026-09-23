using UnityEngine;
using Trajectory = LetMeSleep.Gameplay.StrikeVisualTrajectory;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// v0.3.0 round 3 (anim-r3): the visual path of a comic swat, presentation only. The authority sweeps its
    /// contact in a straight line from the shoulder to the target; the arm IK instead follows a slap:
    /// anticipation with the hand (or the flyswatter head) cocked above and behind the ear during the
    /// authority's windup, a diagonal arc that bulges up and out and accelerates into the authoritative
    /// target, reached exactly when the sweep ends and held through the closure tick, then a follow-through
    /// down and across the body while the pose weight hands the arm back to the clip. Times are authority
    /// seconds since the strike started.
    /// </summary>
    public static class StrikeSwingPath
    {
        public const float ImpactSeconds = Trajectory.SweepStart + Trajectory.SweepDuration;
        public const float FollowThroughSeconds = .15f;
        /// <summary>Wrist lag of a held tool while cocked (negative trails behind the sweep), in degrees.</summary>
        public const float CockedWristDegrees = -35f;
        /// <summary>Wrist lead of a held tool at the end of the follow-through, in degrees.</summary>
        public const float FollowWristDegrees = 30f;

        /// <summary>IK weight from elapsed seconds (the authority's PoseWeight, but continuous in time).</summary>
        public static float Weight(float elapsed)
        {
            if (float.IsNaN(elapsed) || elapsed < 0f || elapsed >= Trajectory.Duration) return 0f;
            if (elapsed < Trajectory.SweepStart) return Smooth(elapsed / Trajectory.ReadyTickSeconds);
            if (elapsed <= Trajectory.ClosureTickSeconds) return 1f;
            return 1f - Smooth((elapsed - Trajectory.ClosureTickSeconds) / (Trajectory.Duration - Trajectory.ClosureTickSeconds));
        }

        /// <summary>Sweep fraction eased to accelerate into the impact (a whip, not a push).</summary>
        public static float Sweep(float elapsed)
        {
            float f = Mathf.Clamp01((elapsed - Trajectory.SweepStart) / Trajectory.SweepDuration);
            return Mathf.Pow(f, 1.6f);
        }

        /// <summary>Follow-through fraction (eased out). The effector holds the target until the authority's
        /// closure tick (it still sweeps .233 -> .25 there), then carries on down and across the body.</summary>
        public static float Follow(float elapsed)
        {
            float g = Mathf.Clamp01((elapsed - Trajectory.ClosureTickSeconds) / FollowThroughSeconds);
            return 1f - (1f - g) * (1f - g);
        }

        /// <summary>
        /// Effector position (hand palm, or the tool's impact point) before blending with the clip's rest by
        /// Weight: cocked until the sweep starts, then a quadratic arc through a control point lifted by
        /// <paramref name="bulge"/> (scaled by the arc length) into the target, then the follow-through.
        /// </summary>
        public static Vector3 Evaluate(float elapsed, Vector3 cocked, Vector3 target, Vector3 followThrough, Vector3 bulge)
        {
            if (elapsed < Trajectory.SweepStart) return cocked;
            if (elapsed <= ImpactSeconds)
            {
                float f = Sweep(elapsed);
                Vector3 control = (cocked + target) * .5f + bulge * Vector3.Distance(cocked, target);
                float u = 1f - f;
                return u * u * cocked + 2f * u * f * control + f * f * target;
            }
            return Vector3.LerpUnclamped(target, followThrough, Follow(elapsed));
        }

        /// <summary>Wrist snap of a held tool about the sweep axis: trailing while cocked and during the whip,
        /// aligned at the impact, leading through the follow-through.</summary>
        public static float WristDegrees(float elapsed)
        {
            if (elapsed < Trajectory.SweepStart) return CockedWristDegrees;
            if (elapsed <= ImpactSeconds) return CockedWristDegrees * (1f - Sweep(elapsed));
            return FollowWristDegrees * Follow(elapsed);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
