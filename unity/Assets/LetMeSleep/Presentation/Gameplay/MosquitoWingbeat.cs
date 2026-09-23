using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>
    /// Presentation-only wingbeat clock for the Fly/Hover loops (3 wingbeats per authored loop).
    /// The rate follows flight speed (8 Hz hovering, 12 Hz at the 3.8 m/s maximum), never the distance
    /// travelled, and each actor starts at its own phase so a swarm does not flap in lockstep.
    /// </summary>
    public static class MosquitoWingbeat
    {
        public const float WingbeatsPerLoop = 3f;
        public const float HoverHz = 8f, MaximumHz = 12f, MaximumSpeed = 3.8f;

        public static float FrequencyHz(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0;
            return Mathf.Lerp(HoverHz, MaximumHz, Mathf.Clamp01(Mathf.Abs(speed) / MaximumSpeed));
        }

        /// <summary>Animator speed for a loop of the given length that holds WingbeatsPerLoop wingbeats.</summary>
        public static float Playback(float speed, float loopSeconds)
        {
            if (!(loopSeconds > 0) || float.IsInfinity(loopSeconds)) return 1;
            return FrequencyHz(speed) * loopSeconds / WingbeatsPerLoop;
        }

        /// <summary>Deterministic per-actor phase in [0, 1).</summary>
        public static float InitialPhase(uint actorId)
        {
            unchecked
            {
                uint hash = actorId * 2654435761u;
                hash ^= hash >> 15; hash *= 2246822519u; hash ^= hash >> 13;
                return (hash & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
