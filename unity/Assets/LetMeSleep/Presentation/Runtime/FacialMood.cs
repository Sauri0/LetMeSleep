using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Expression presets of the v0.3.0 sketches (PER-02, PER-03 heads, PER-07 "Mood expressions").</summary>
    public enum FacialMood : byte { Neutral, Happy, Angry, Alert, Sleepy, Surprised, Focused, Dizzy, Excited, Yawning, Unconscious }

    /// <summary>Resolved, blendable expression channels. Purely visual; no gameplay meaning.</summary>
    public struct FacialMoodShape
    {
        /// <summary>Upper lid closure 0..1 (blink still closes fully on top).</summary>
        public float Upper;
        /// <summary>Lower lid closure 0..1 (mosquito lid bones only).</summary>
        public float Lower;
        /// <summary>Upper lid tilt in degrees, positive = inner corners down (angry), negative = worried/droopy.</summary>
        public float Tilt;
        /// <summary>Mosquito pupil size multiplier (1 = authored).</summary>
        public float Pupil;
        /// <summary>Human brow lift in metres along the head up axis.</summary>
        public float BrowLift;
        /// <summary>Human brow tilt in degrees, positive = inner ends down (angry).</summary>
        public float BrowTilt;
        /// <summary>Human jaw opening in degrees, added to the animated jaw.</summary>
        public float Jaw;
        /// <summary>0..1 weight of the circling "dizzy" pupils.</summary>
        public float Dizzy;

        public static FacialMoodShape For(FacialMood mood)
        {
            switch (mood)
            {
                case FacialMood.Happy: return new FacialMoodShape { Upper = .30f, Lower = .40f, Tilt = -6, Pupil = 1.06f, BrowLift = .005f, BrowTilt = -5, Jaw = 9 };
                case FacialMood.Angry: return new FacialMoodShape { Upper = .45f, Lower = .12f, Tilt = 20, Pupil = .88f, BrowLift = -.003f, BrowTilt = 16, Jaw = 0 };
                case FacialMood.Alert: return new FacialMoodShape { Upper = 0, Lower = 0, Tilt = 0, Pupil = .82f, BrowLift = .004f, BrowTilt = -2, Jaw = 0 };
                case FacialMood.Sleepy: return new FacialMoodShape { Upper = .58f, Lower = .16f, Tilt = -8, Pupil = 1f, BrowLift = -.002f, BrowTilt = -5, Jaw = 1 };
                case FacialMood.Surprised: return new FacialMoodShape { Upper = 0, Lower = 0, Tilt = -4, Pupil = .72f, BrowLift = .010f, BrowTilt = -3, Jaw = 12 };
                case FacialMood.Focused: return new FacialMoodShape { Upper = .38f, Lower = .24f, Tilt = 10, Pupil = .9f, BrowLift = -.002f, BrowTilt = 9, Jaw = 0 };
                case FacialMood.Dizzy: return new FacialMoodShape { Upper = .34f, Lower = .12f, Tilt = -6, Pupil = .95f, BrowLift = .002f, BrowTilt = -6, Jaw = 5, Dizzy = 1 };
                case FacialMood.Excited: return new FacialMoodShape { Upper = 0, Lower = .28f, Tilt = -3, Pupil = 1.12f, BrowLift = .007f, BrowTilt = -3, Jaw = 11 };
                case FacialMood.Yawning: return new FacialMoodShape { Upper = .86f, Lower = .30f, Tilt = -6, Pupil = 1f, BrowLift = .004f, BrowTilt = -4, Jaw = 0 };
                case FacialMood.Unconscious: return new FacialMoodShape { Upper = 1f, Lower = .4f, Tilt = 0, Pupil = 1f, BrowLift = 0, BrowTilt = 0, Jaw = 0 };
                default: return Neutral;
            }
        }

        public static FacialMoodShape Neutral => new FacialMoodShape { Pupil = 1 };

        public static FacialMoodShape Lerp(FacialMoodShape a, FacialMoodShape b, float t)
        {
            t = Mathf.Clamp01(t);
            return new FacialMoodShape
            {
                Upper = Mathf.Lerp(a.Upper, b.Upper, t), Lower = Mathf.Lerp(a.Lower, b.Lower, t),
                Tilt = Mathf.Lerp(a.Tilt, b.Tilt, t), Pupil = Mathf.Lerp(a.Pupil, b.Pupil, t),
                BrowLift = Mathf.Lerp(a.BrowLift, b.BrowLift, t), BrowTilt = Mathf.Lerp(a.BrowTilt, b.BrowTilt, t),
                Jaw = Mathf.Lerp(a.Jaw, b.Jaw, t), Dizzy = Mathf.Lerp(a.Dizzy, b.Dizzy, t)
            };
        }

        public bool IsFinite =>
            Finite(Upper) && Finite(Lower) && Finite(Tilt) && Finite(Pupil) &&
            Finite(BrowLift) && Finite(BrowTilt) && Finite(Jaw) && Finite(Dizzy);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
