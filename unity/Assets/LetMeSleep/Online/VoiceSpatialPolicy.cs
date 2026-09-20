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

    /// <summary>Pure local presentation policy. Room membership and packet identity remain authoritative elsewhere.</summary>
    public static class VoiceSpatialPolicy
    {
        public const float ClearDistance = 4f;
        public const float HumanCutoffDistance = 12f;
        public const float MosquitoToHumanCutoffDistance = 8f;
        public const float MosquitoToMosquitoCutoffDistance = 16f;
        public const float OccludedGain = 0.5011872f; // -6 dB
        public const float OpenLowPassHertz = 22000f;
        public const float OccludedLowPassHertz = 3500f;

        public static VoiceSpatialResult Evaluate(PlayerRole speakerRole, bool speakerEliminated, Float3 speakerPosition,
            PlayerRole listenerRole, bool listenerEliminated, Float3 listenerPosition, bool occluded)
        {
            if (!speakerPosition.IsFinite || !listenerPosition.IsFinite ||
                (speakerRole != PlayerRole.Human && speakerRole != PlayerRole.Mosquito) ||
                (listenerRole != PlayerRole.Human && listenerRole != PlayerRole.Mosquito))
                return default;
            if (speakerEliminated != listenerEliminated) return default;

            float cutoff = speakerRole == PlayerRole.Human ? HumanCutoffDistance :
                listenerRole == PlayerRole.Human ? MosquitoToHumanCutoffDistance : MosquitoToMosquitoCutoffDistance;
            float distance = (speakerPosition - listenerPosition).Length;
            if (distance >= cutoff) return new VoiceSpatialResult(false, 0, OpenLowPassHertz, distance);
            float t = Math.Max(0, Math.Min(1, (distance - ClearDistance) / Math.Max(.001f, cutoff - ClearDistance)));
            float gain = 1f - t * t * (3f - 2f * t);
            if (occluded) gain *= OccludedGain;
            return new VoiceSpatialResult(gain > .0001f, gain,
                occluded ? OccludedLowPassHertz : OpenLowPassHertz, distance);
        }

        public static VoiceSpatialResult NonSpatialWaitingRoom()
            => new VoiceSpatialResult(true, 1f, OpenLowPassHertz, 0);
    }
}
