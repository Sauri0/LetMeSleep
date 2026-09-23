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
        /// <summary>Human "Smile" mouth morph 0..1: a wide open grin with the corners up.</summary>
        public float Smile;
        /// <summary>Human "MouthO" mouth morph 0..1: a round open shout or yawn (the Jaw adds the drop).</summary>
        public float MouthOpen;
        /// <summary>Human "Frown" mouth morph 0..1: corners pulled down, lips pressed.</summary>
        public float Frown;

        /// <summary>
        /// Human preset (blend-shape lids, decal pupils, brows, jaw and mouth morphs). v0.3.0 round 3: every
        /// mood reads at a 64 px thumbnail by its mouth as well (a real open smile for Happy/Excited, an open
        /// yawn, an O of surprise, an angry pinch); human pupils stay within 0.82-1.3 of their size (smaller
        /// decals sank into the faceted globes) and the squint is at most 0.3.
        /// </summary>
        public static FacialMoodShape For(FacialMood mood) => For(mood, false);

        /// <summary>Preset for a rig with blend-shape lids (human) or with lid bones (mosquito, boneLids).</summary>
        public static FacialMoodShape For(FacialMood mood, bool boneLids)
        {
            if (boneLids) return Mosquito(mood);
            switch (mood)
            {
                case FacialMood.Happy: return new FacialMoodShape { Upper = .12f, Lower = .78f, Tilt = -12, Pupil = 1.12f, BrowLift = .002f, BrowTilt = -10, Jaw = 7, Squint = .28f, HeadPitch = -3, HeadRoll = 9, Smile = 1 };
                case FacialMood.Angry: return new FacialMoodShape { Upper = .30f, Lower = .20f, Tilt = 32, Pupil = .84f, BrowLift = -.006f, BrowTilt = 34, Jaw = 0, Squint = .15f, HeadPitch = 9, HeadRoll = 0, Frown = 1 };
                case FacialMood.Alert: return new FacialMoodShape { Upper = .02f, Lower = .12f, Tilt = 8, Pupil = .82f, BrowLift = .003f, BrowTilt = -8, Jaw = 3, Squint = 0, HeadPitch = -6, HeadRoll = 0, MouthOpen = .35f };
                case FacialMood.Sleepy: return new FacialMoodShape { Upper = .66f, Lower = .22f, Tilt = -14, Pupil = 1f, BrowLift = -.004f, BrowTilt = -10, Jaw = 2, Squint = .08f, HeadPitch = 10, HeadRoll = 9, MouthOpen = .15f };
                case FacialMood.Surprised: return new FacialMoodShape { Upper = 0, Lower = 0, Tilt = -12, Pupil = .82f, BrowLift = .004f, BrowTilt = -12, Jaw = 13, Squint = 0, HeadPitch = -13, HeadRoll = 0, MouthOpen = .9f };
                case FacialMood.Focused: return new FacialMoodShape { Upper = .25f, Lower = .44f, Tilt = 12, Pupil = .88f, BrowLift = -.004f, BrowTilt = 14, Jaw = 0, Squint = .30f, HeadPitch = 5, HeadRoll = 0, Frown = .45f };
                case FacialMood.Dizzy: return new FacialMoodShape { Upper = .34f, Lower = .12f, Tilt = -6, Pupil = .95f, BrowLift = .002f, BrowTilt = -6, Jaw = 5, Dizzy = 1, Squint = .1f, HeadPitch = 0, HeadRoll = 0, MouthOpen = .35f };
                case FacialMood.Excited: return new FacialMoodShape { Upper = 0, Lower = .34f, Tilt = -8, Pupil = 1.3f, BrowLift = .004f, BrowTilt = -6, Jaw = 8, Squint = .1f, HeadPitch = -5, HeadRoll = -8, Smile = 1f };
                case FacialMood.Yawning: return new FacialMoodShape { Upper = .88f, Lower = .30f, Tilt = -6, Pupil = 1f, BrowLift = .003f, BrowTilt = -4, Jaw = 24, Squint = 0, HeadPitch = -6, HeadRoll = 0, MouthOpen = 1 };
                case FacialMood.Unconscious: return new FacialMoodShape { Upper = 1f, Lower = .4f, Tilt = 0, Pupil = 1f, BrowLift = 0, BrowTilt = 0, Jaw = 4, Squint = 0, HeadPitch = 0, HeadRoll = 0, MouthOpen = .25f };
                default: return Neutral;
            }
        }

        /// <summary>
        /// Mosquito preset (lid bones with a tilt, pupil discs, no brows or mouth). v0.3.0 round 3 (PER-03/PER-07):
        /// angry V lids clearly closed and tilted, focused narrowed eyes, alert small pupils under level lids,
        /// excited huge pupils with smiling lower lids, happy crescent eyes.
        /// </summary>
        private static FacialMoodShape Mosquito(FacialMood mood)
        {
            switch (mood)
            {
                case FacialMood.Happy: return new FacialMoodShape { Upper = .22f, Lower = .72f, Tilt = -18, Pupil = 1.15f, HeadPitch = -3, HeadRoll = 9 };
                case FacialMood.Angry: return new FacialMoodShape { Upper = .56f, Lower = .16f, Tilt = 40, Pupil = .74f, HeadPitch = 9 };
                case FacialMood.Alert: return new FacialMoodShape { Upper = .16f, Lower = .16f, Tilt = 0, Pupil = .62f, HeadPitch = 6 };
                case FacialMood.Sleepy: return new FacialMoodShape { Upper = .66f, Lower = .22f, Tilt = -14, Pupil = 1f, HeadPitch = 10, HeadRoll = 9 };
                case FacialMood.Surprised: return new FacialMoodShape { Upper = 0, Lower = 0, Tilt = -22, Pupil = .45f, HeadPitch = -13 };
                case FacialMood.Focused: return new FacialMoodShape { Upper = .42f, Lower = .42f, Tilt = 16, Pupil = .86f, HeadPitch = 5 };
                case FacialMood.Dizzy: return new FacialMoodShape { Upper = .34f, Lower = .12f, Tilt = -6, Pupil = .95f, Dizzy = 1 };
                case FacialMood.Excited: return new FacialMoodShape { Upper = 0, Lower = .45f, Tilt = -24, Pupil = 1.38f, HeadPitch = -6, HeadRoll = -8 };
                case FacialMood.Yawning: return new FacialMoodShape { Upper = .88f, Lower = .30f, Tilt = -6, Pupil = 1f, HeadPitch = -6 };
                case FacialMood.Unconscious: return new FacialMoodShape { Upper = 1f, Lower = .4f, Pupil = 1f };
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
                HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t), HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t),
                Smile = Mathf.Lerp(a.Smile, b.Smile, t), MouthOpen = Mathf.Lerp(a.MouthOpen, b.MouthOpen, t),
                Frown = Mathf.Lerp(a.Frown, b.Frown, t)
            };
        }

        /// <summary>
        /// Readability distance between two shapes, each channel scaled to its visible range (a full lid,
        /// 30 deg of tilt, a 50% pupil change, a full squint, 15 deg of jaw or head pose, a full mouth morph).
        /// </summary>
        public static float Distance(FacialMoodShape a, FacialMoodShape b, bool human)
        {
            float Sq(float x) => x * x;
            float sum = Sq(a.Upper - b.Upper) + Sq((a.HeadPitch - b.HeadPitch) / 15f) + Sq((a.HeadRoll - b.HeadRoll) / 15f) +
                Sq(a.Dizzy - b.Dizzy) + Sq((a.Pupil - b.Pupil) / .5f);
            if (human)
                sum += Sq((a.BrowTilt - b.BrowTilt) / 30f) + Sq((a.BrowLift - b.BrowLift) / .012f) +
                    Sq(a.Squint - b.Squint) + Sq((a.Jaw - b.Jaw) / 15f) +
                    Sq(a.Smile - b.Smile) + Sq(a.MouthOpen - b.MouthOpen) + Sq(a.Frown - b.Frown);
            else
                sum += Sq(a.Lower - b.Lower) + Sq((a.Tilt - b.Tilt) / 30f);
            return Mathf.Sqrt(sum);
        }

        public bool IsFinite =>
            Finite(Upper) && Finite(Lower) && Finite(Tilt) && Finite(Pupil) &&
            Finite(BrowLift) && Finite(BrowTilt) && Finite(Jaw) && Finite(Dizzy) &&
            Finite(Squint) && Finite(HeadPitch) && Finite(HeadRoll) &&
            Finite(Smile) && Finite(MouthOpen) && Finite(Frown);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
