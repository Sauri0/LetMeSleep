using System;
using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    // Persistent support-relative camera frame. It consumes body up, not body yaw:
    // turning the body toward the aim must never feed that turn back into the camera.
    public sealed class MosquitoLookFrame
    {
        private const float PitchLimit = 89f * (float)Math.PI / 180f;
        private Float3 supportUp = Float3.Up, tangent = Float3.Forward;
        private float pitch;
        private bool initialized;
        public Quaternion View { get; private set; } = new Quaternion(0, 0, 0, 1);
        public Float3 Forward { get; private set; } = Float3.Forward;

        public void Reset(Rotation body, Float3 worldAim)
        {
            supportUp = SafeUnit(body.Up, Float3.Up);
            var aim = SafeUnit(worldAim, SafeUnit(body.Forward, Float3.Forward));
            float vertical = MathEx.Clamp(Float3.Dot(aim, supportUp), -1, 1);
            pitch = MathEx.Clamp((float)Math.Asin(vertical), -PitchLimit, PitchLimit);
            tangent = Tangent(aim, supportUp);
            if (tangent.LengthSquared < .0000000001f) tangent = Tangent(body.Forward, supportUp);
            tangent = SafeTangent(tangent, supportUp);
            initialized = true;
            Publish();
        }

        // Positive yaw turns right about support up; positive pitch looks toward up.
        // The caller owns any world-space pitch restriction required by its wire format.
        public void Step(Rotation body, float yawDeltaRadians, float pitchDeltaRadians)
        {
            if (!initialized) Reset(body, body.Forward);
            var nextUp = SafeUnit(body.Up, supportUp);
            var cross = Float3.Cross(supportUp, nextUp);
            float sine = cross.Length, cosine = MathEx.Clamp(Float3.Dot(supportUp, nextUp), -1, 1);
            if (sine > .000001f)
                tangent = Rotate(tangent, cross / sine, (float)Math.Atan2(sine, cosine));
            else if (cosine < 0)
                // At opposite normals, the prior tangent is the deterministic half-turn axis.
                tangent = Rotate(tangent, tangent, (float)Math.PI);
            supportUp = nextUp;
            tangent = SafeTangent(Tangent(tangent, supportUp), supportUp);
            if (MathEx.Finite(yawDeltaRadians))
                tangent = Rotate(tangent, supportUp, (float)Math.IEEERemainder(yawDeltaRadians, 2 * Math.PI));
            if (MathEx.Finite(pitchDeltaRadians))
                pitch = (float)Math.Max(-PitchLimit, Math.Min(PitchLimit, (double)pitch + pitchDeltaRadians));
            Publish();
        }

        private void Publish()
        {
            tangent = SafeTangent(Tangent(tangent, supportUp), supportUp);
            float sine = (float)Math.Sin(pitch), cosine = (float)Math.Cos(pitch);
            Forward = (tangent * cosine + supportUp * sine).Normalized;
            var up = (supportUp * cosine - tangent * sine).Normalized;
            var rotation = Rotation.Look(Forward, up);
            View = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
        }
        private static Float3 Rotate(Float3 value, Float3 axis, float radians)
        {
            float sine = (float)Math.Sin(radians), cosine = (float)Math.Cos(radians);
            return value * cosine + Float3.Cross(axis, value) * sine + axis * (Float3.Dot(axis, value) * (1 - cosine));
        }
        private static Float3 Tangent(Float3 value, Float3 up) => value.IsFinite ? Float3.ProjectPlane(value, up) : Float3.Zero;
        private static Float3 SafeUnit(Float3 value, Float3 fallback)
            => value.IsFinite && MathEx.Finite(value.LengthSquared) && value.LengthSquared > .0000000001f ? value.Normalized : fallback;
        private static Float3 SafeTangent(Float3 value, Float3 up)
        {
            if (value.LengthSquared < .0000000001f || !value.IsFinite)
                value = Tangent(Math.Abs(up.Y) < .9f ? Float3.Up : Float3.Forward, up);
            return value.Normalized;
        }
    }
}
