using System;

namespace LetMeSleep.Gameplay
{
    // Presentation basis: +Y points away from support, -Y feet point toward it.
    public static class SurfaceVisualFrame
    {
        public static bool TryResolve(Float3 normal, Float3 desiredForward, Float3 previousForward,
            float maximumTurnRadians, out Float3 up, out Float3 forward)
        {
            up = forward = Float3.Zero;
            if (!normal.IsFinite || !MathEx.Finite(normal.LengthSquared) || normal.LengthSquared < .000001f) return false;
            up = normal.Normalized;
            var previous = Tangent(previousForward, up);
            var desired = Tangent(desiredForward, up);
            // A near-normal view has no stable heading: retain the previous tangent.
            if (desired.LengthSquared < .0025f) desired = previous;
            if (desired.LengthSquared < .000001f)
                desired = Tangent(Math.Abs(up.Y) < .9f ? Float3.Up : Float3.Forward, up);
            desired = desired.Normalized;
            if (previous.LengthSquared < .000001f) { forward = desired; return true; }
            previous = previous.Normalized;
            float sine = Float3.Dot(up, Float3.Cross(previous, desired));
            float cosine = MathEx.Clamp(Float3.Dot(previous, desired), -1, 1);
            // Deterministic turn direction for exactly opposite headings.
            float angle = cosine < 0 && Math.Abs(sine) < .000001f
                ? (float)Math.PI : (float)Math.Atan2(sine, cosine);
            float limit = MathEx.Finite(maximumTurnRadians) ? Math.Max(0, maximumTurnRadians) : 0;
            angle = MathEx.Clamp(angle, -limit, limit);
            forward = (previous * (float)Math.Cos(angle)
                + Float3.Cross(up, previous) * (float)Math.Sin(angle)).Normalized;
            return true;
        }

        private static Float3 Tangent(Float3 direction, Float3 up) => direction.IsFinite
            ? Float3.ProjectPlane(direction, up) : Float3.Zero;
    }
}
