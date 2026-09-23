using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;

namespace LetMeSleep.Online
{
    public readonly struct VoiceSpatialResult
    {
        public bool Audible { get; }
        public float Gain { get; }
        public float LowPassHertz { get; }
        public float Distance { get; }

        public VoiceSpatialResult(bool audible, float gain, float lowPassHertz, float distance)
        {
            Audible = audible;
            Gain = gain;
            LowPassHertz = lowPassHertz;
            Distance = distance;
        }
    }

    /// <summary>
    /// Pure local presentation policy. Room membership and packet identity remain authoritative elsewhere.
    ///
    /// Distance: natural inverse-distance law softened to −4.5 dB per doubling around a 2 m reference (0 dB),
    /// +8 dB cap up close (reached at 0.5 m: a mosquito at your ear sounds at your ear), and a raised-cosine fade over the last 42 %
    /// of the role range so the voice melts away with zero slope at the cut. Beyond the cut the gain is exactly 0.
    /// Occlusion (fraction of blocked mouth→ear rays, number of walls): −7 dB for one wall, −4 dB per extra wall,
    /// low-pass from 5.5 kHz down to 1.5 kHz (0.9 kHz behind two walls), proportional to the blocked fraction.
    /// </summary>
    public static class VoiceSpatialPolicy
    {
        public const float ReferenceDistance = 2f;
        public const float RolloffDbPerDoubling = 4.5f;
        public const float NearBoostDb = 8f;
        public const float FadeStartFraction = .58f;
        /// <summary>Kept for compatibility: the curve is at 0 dB at the reference distance, not flat up to here.</summary>
        public const float ClearDistance = ReferenceDistance;
        public const float HumanCutoffDistance = 12f;
        public const float MosquitoToHumanCutoffDistance = 8f;
        public const float MosquitoToMosquitoCutoffDistance = 16f;
        /// <summary>Routing keeps a peer connected this far past the cut so audibility does not flap (gain stays 0).</summary>
        public const float RoutingHysteresisDistance = 1f;
        public const float OpenLowPassHertz = 22000f;
        public const float OneWallDb = -7f;
        public const float ExtraWallDb = -4f;
        public const float OccludedLowPassHertz = 1500f;
        public const float TwoWallLowPassHertz = 900f;
        public const float OccludedGain = 0.4466836f; // one wall, fully blocked: -7 dB

        public static VoiceSpatialResult Evaluate(PlayerRole speakerRole, bool speakerEliminated, Float3 speakerPosition,
            PlayerRole listenerRole, bool listenerEliminated, Float3 listenerPosition, bool occluded)
            => Evaluate(speakerRole, speakerEliminated, speakerPosition, listenerRole, listenerEliminated, listenerPosition,
                occluded ? 1f : 0f, occluded ? 1 : 0);

        /// <summary>Distance, cohort and graded occlusion. <paramref name="occlusion"/> is the blocked fraction 0…1.</summary>
        public static VoiceSpatialResult Evaluate(PlayerRole speakerRole, bool speakerEliminated, Float3 speakerPosition,
            PlayerRole listenerRole, bool listenerEliminated, Float3 listenerPosition, float occlusion, int walls)
        {
            if (!speakerPosition.IsFinite || !listenerPosition.IsFinite ||
                (speakerRole != PlayerRole.Human && speakerRole != PlayerRole.Mosquito) ||
                (listenerRole != PlayerRole.Human && listenerRole != PlayerRole.Mosquito))
                return default;
            if (speakerEliminated != listenerEliminated) return default;

            float cutoff = CutoffDistance(speakerRole, listenerRole);
            float distance = (speakerPosition - listenerPosition).Length;
            if (distance >= cutoff) return new VoiceSpatialResult(false, 0, OpenLowPassHertz, distance);
            float gain = DistanceGain(distance, cutoff) * OcclusionGain(occlusion, walls);
            float lowPass = occlusion > 0f ? OcclusionLowPassHertz(occlusion, walls) : OpenLowPassHertz;
            return new VoiceSpatialResult(gain > .0001f, gain, lowPass, distance);
        }

        public static float CutoffDistance(PlayerRole speakerRole, PlayerRole listenerRole)
            => speakerRole == PlayerRole.Human ? HumanCutoffDistance :
               listenerRole == PlayerRole.Human ? MosquitoToHumanCutoffDistance : MosquitoToMosquitoCutoffDistance;

        /// <summary>Linear gain for a distance inside a role range (0 at and beyond <paramref name="cutoff"/>).</summary>
        public static float DistanceGain(float distance, float cutoff)
        {
            if (float.IsNaN(distance) || distance < 0f || !(cutoff > 0f) || distance >= cutoff) return 0f;
            double d = Math.Max(distance, .1f);
            double db = Math.Min(NearBoostDb, -RolloffDbPerDoubling * Math.Log(d / ReferenceDistance, 2.0));
            double fadeStart = cutoff * FadeStartFraction;
            double window = distance <= fadeStart ? 1.0 : .5 + .5 * Math.Cos(Math.PI * (distance - fadeStart) / (cutoff - fadeStart));
            return (float)(Math.Pow(10.0, db / 20.0) * window);
        }

        /// <summary>Linear gain for a blocked fraction (0…1) and the number of walls on the direct path.</summary>
        public static float OcclusionGain(float occlusion, int walls)
        {
            float amount = Clamp01(occlusion);
            if (amount <= 0f) return 1f;
            float db = (OneWallDb + ExtraWallDb * Math.Max(0, Math.Min(2, walls - 1))) * amount;
            return (float)Math.Pow(10.0, db / 20.0);
        }

        /// <summary>Low-pass corner for a blocked fraction; interpolated in log-frequency from the open 5.5 kHz.</summary>
        public static float OcclusionLowPassHertz(float occlusion, int walls)
        {
            float amount = Clamp01(occlusion);
            float closed = walls >= 2 ? TwoWallLowPassHertz : OccludedLowPassHertz;
            const float open = 5500f;
            return (float)Math.Exp(Math.Log(open) + (Math.Log(closed) - Math.Log(open)) * amount);
        }

        /// <summary>
        /// Gentle distance darkening (legibility cue, exaggerated air absorption): open up to 4 m, then down to
        /// 3.8 kHz at the cut, log-interpolated.
        /// </summary>
        public static float AirLowPassHertz(float distance, float cutoff)
        {
            const float open = 5500f, far = 3800f, start = 4f;
            if (float.IsNaN(distance) || distance <= start || !(cutoff > start)) return open;
            float t = Math.Min(1f, (distance - start) / (cutoff - start));
            return (float)Math.Exp(Math.Log(open) + (Math.Log(far) - Math.Log(open)) * t);
        }

        /// <summary>Network routing with hysteresis: a route opens inside the cut and closes 1 m past it.</summary>
        public static bool RouteAudible(float distance, float cutoff, bool wasAudible)
        {
            if (float.IsNaN(distance) || distance < 0f) return false;
            return wasAudible ? distance < cutoff + RoutingHysteresisDistance : distance < cutoff;
        }

        public static VoiceSpatialResult NonSpatialWaitingRoom()
            => new VoiceSpatialResult(true, 1f, OpenLowPassHertz, 0);

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
