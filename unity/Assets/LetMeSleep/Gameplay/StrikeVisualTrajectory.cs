using System;

namespace LetMeSleep.Gameplay
{
    // Presentation of the existing Authority sweep, not a new damage/reach contract.
    // Keep the closure tick: Authority still sweeps .2333 -> .25 at elapsed 8/30,
    // although that snapshot already has Phase Recovery.
    public static class StrikeVisualTrajectory
    {
        public const float Duration = .6f;
        public const float SweepStart = .08f;
        public const float SweepDuration = .17f;
        public const float ReadyTickSeconds = 2f / 30f;
        public const float ClosureTickSeconds = 8f / 30f;

        public static float Elapsed(in StrikeState strike) => MathEx.Clamp(strike.Progress, 0, 1) * Duration;
        public static float SweepFraction(float elapsed) => MathEx.Clamp((elapsed - SweepStart) / SweepDuration, 0, 1);
        public static float PoseWeight(in StrikeState strike)
        {
            if (strike.Phase == StrikePhase.None) return 0;
            float elapsed = Elapsed(strike);
            if (elapsed < SweepStart) return Smooth(elapsed / ReadyTickSeconds);
            if (elapsed <= ClosureTickSeconds) return 1;
            return 1 - Smooth((elapsed - ClosureTickSeconds) / (Duration - ClosureTickSeconds));
        }
        public static Float3 Contact(in StrikeState strike, Float3 restContact)
        {
            if (strike.Phase == StrikePhase.None) return restContact;
            float elapsed = Elapsed(strike);
            if (elapsed < SweepStart) return restContact + (strike.Origin - restContact) * PoseWeight(strike);
            if (elapsed <= ClosureTickSeconds)
                return strike.Origin + (strike.Target - strike.Origin) * SweepFraction(elapsed);
            return restContact + (strike.Target - restContact) * PoseWeight(strike);
        }
        // Radial alignment minimizes outer reach but can put the wrist inside
        // |upper-lower|. Tilt only enough to reach the inner arm sphere, preserving
        // the intended contact and the rigid hand/contact offset length.
        public static Float3 ToolOffset(Float3 shoulderToContact, float offsetLength,
            float minimumArmReach, Float3 pole)
        {
            if (!shoulderToContact.IsFinite || !MathEx.Finite(offsetLength) || offsetLength <= .00001f) return Float3.Zero;
            float distance = shoulderToContact.Length;
            var axis = distance > .00001f ? shoulderToContact / distance : Float3.Forward;
            float cosine = 1;
            if (distance > .00001f && MathEx.Finite(minimumArmReach) && minimumArmReach > 0 &&
                Math.Abs(distance - offsetLength) < minimumArmReach)
                cosine = MathEx.Clamp((distance * distance + offsetLength * offsetLength - minimumArmReach * minimumArmReach) /
                    (2 * distance * offsetLength), -1, 1);
            if (cosine >= 1) return axis * offsetLength;
            var tangent = Float3.ProjectPlane(pole, axis);
            if (!tangent.IsFinite || tangent.LengthSquared < .000001f) tangent = Float3.ProjectPlane(Float3.Up, axis);
            if (tangent.LengthSquared < .000001f) tangent = Float3.ProjectPlane(Float3.Forward, axis);
            float sine = (float)Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
            return (axis * cosine + tangent.Normalized * sine) * offsetLength;
        }
        private static float Smooth(float t) { t = MathEx.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    }
}
