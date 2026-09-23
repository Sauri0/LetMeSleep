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
        /// <summary>Pupil size multiplier across the look axis (1 = authored); mosquito discs and human decals.</summary>
        public float Pupil;
        /// <summary>Human brow lift in metres along the head up axis.</summary>
        public float BrowLift;
        /// <summary>Human brow tilt in degrees, positive = inner ends down (angry).</summary>
        public float BrowTilt;
        /// <summary>Human jaw opening in degrees, added to the animated jaw.</summary>
        public float Jaw;
        /// <summary>0..1 weight of the circling "dizzy" pupils.</summary>
        public float Dizzy;
        /// <summary>Human pupil vertical flattening 0..1: the squint of anger, focus and joy.</summary>
        public float Squint;
        /// <summary>Head pitch in degrees added after look-at, positive = chin down.</summary>
        public float HeadPitch;
        /// <summary>Head roll in degrees added after look-at (a tilted, sleepy or happy head).</summary>
        public float HeadRoll;

        /// <summary>
        /// v0.3.0 review: every mood must read at a 64 px thumbnail, so each one differs from the others in
        /// at least two strong channels (lids, lid/brow tilt, pupil size, squint, jaw, head pose). The human cap
        /// hides lifted brows, so raised-brow moods use small pupils and head pose instead; lowered brows
        /// drag the upper lid skin, so angry/focused keep them within ~6 mm and rely on the V tilt.
        /// </summary>
        public static FacialMoodShape For(FacialMood mood)
        {
            switch (mood)
            {
                case FacialMood.Happy: return new FacialMoodShape { Upper = .15f, Lower = .78f, Tilt = -12, Pupil = 1.12f, BrowLift = .002f, BrowTilt = -10, Jaw = 12, Squint = .58f, HeadPitch = -3, HeadRoll = 9 };
                case FacialMood.Angry: return new FacialMoodShape { Upper = .30f, Lower = .20f, Tilt = 32, Pupil = .78f, BrowLift = -.006f, BrowTilt = 34, Jaw = 0, Squint = .15f, HeadPitch = 9, HeadRoll = 0 };
                case FacialMood.Alert: return new FacialMoodShape { Upper = .10f, Lower = .12f, Tilt = 8, Pupil = .60f, BrowLift = .002f, BrowTilt = -8, Jaw = 0, Squint = 0, HeadPitch = -6, HeadRoll = 0 };
                case FacialMood.Sleepy: return new FacialMoodShape { Upper = .66f, Lower = .22f, Tilt = -14, Pupil = 1f, BrowLift = -.004f, BrowTilt = -10, Jaw = 2, Squint = .08f, HeadPitch = 10, HeadRoll = 9 };
                case FacialMood.Surprised: return new FacialMoodShape { Upper = 0, Lower = 0, Tilt = -12, Pupil = .45f, BrowLift = .004f, BrowTilt = -12, Jaw = 13, Squint = 0, HeadPitch = -13, HeadRoll = 0 };
                case FacialMood.Focused: return new FacialMoodShape { Upper = .25f, Lower = .44f, Tilt = 12, Pupil = .74f, BrowLift = -.004f, BrowTilt = 14, Jaw = 0, Squint = .30f, HeadPitch = 5, HeadRoll = 0 };
                case FacialMood.Dizzy: return new FacialMoodShape { Upper = .34f, Lower = .12f, Tilt = -6, Pupil = .95f, BrowLift = .002f, BrowTilt = -6, Jaw = 5, Dizzy = 1, Squint = .1f, HeadPitch = 0, HeadRoll = 0 };
                case FacialMood.Excited: return new FacialMoodShape { Upper = 0, Lower = .34f, Tilt = -8, Pupil = 1.34f, BrowLift = .004f, BrowTilt = -6, Jaw = 16, Squint = .18f, HeadPitch = -5, HeadRoll = -8 };
                case FacialMood.Yawning: return new FacialMoodShape { Upper = .88f, Lower = .30f, Tilt = -6, Pupil = 1f, BrowLift = .003f, BrowTilt = -4, Jaw = 0, Squint = 0, HeadPitch = -6, HeadRoll = 0 };
                case FacialMood.Unconscious: return new FacialMoodShape { Upper = 1f, Lower = .4f, Tilt = 0, Pupil = 1f, BrowLift = 0, BrowTilt = 0, Jaw = 0, Squint = 0, HeadPitch = 0, HeadRoll = 0 };
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
                Jaw = Mathf.Lerp(a.Jaw, b.Jaw, t), Dizzy = Mathf.Lerp(a.Dizzy, b.Dizzy, t),
                Squint = Mathf.Lerp(a.Squint, b.Squint, t),
                HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t), HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t)
            };
        }

        /// <summary>
        /// Readability distance between two shapes, each channel scaled to its visible range (a full lid,
        /// 30 deg of tilt, a 50% pupil change, a full squint, 15 deg of jaw or head pose).
        /// </summary>
        public static float Distance(FacialMoodShape a, FacialMoodShape b, bool human)
        {
            float Sq(float x) => x * x;
            float sum = Sq(a.Upper - b.Upper) + Sq((a.HeadPitch - b.HeadPitch) / 15f) + Sq((a.HeadRoll - b.HeadRoll) / 15f) +
                Sq(a.Dizzy - b.Dizzy) + Sq((a.Pupil - b.Pupil) / .5f);
            if (human)
                sum += Sq((a.BrowTilt - b.BrowTilt) / 30f) + Sq((a.BrowLift - b.BrowLift) / .012f) +
                    Sq(a.Squint - b.Squint) + Sq((a.Jaw - b.Jaw) / 15f);
            else
                sum += Sq(a.Lower - b.Lower) + Sq((a.Tilt - b.Tilt) / 30f);
            return Mathf.Sqrt(sum);
        }

        public bool IsFinite =>
            Finite(Upper) && Finite(Lower) && Finite(Tilt) && Finite(Pupil) &&
            Finite(BrowLift) && Finite(BrowTilt) && Finite(Jaw) && Finite(Dizzy) &&
            Finite(Squint) && Finite(HeadPitch) && Finite(HeadRoll);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
