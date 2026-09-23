using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// Soft fire/candle flicker for one map-local Light created by HiggsfieldMapLighting. Visual only:
    /// no gameplay, network or physics effect. Two octaves of Perlin noise around the configured
    /// intensity (never strobing) plus a small range breath, so warm pools of light feel alive.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HiggsfieldLightFlicker : MonoBehaviour
    {
        public const float MaximumAmplitude = 0.6f;

        private Light target;
        private float baseIntensity;
        private float baseRange;
        private float amplitude;
        private float seed;

        public float Amplitude => amplitude;
        public float BaseIntensity => baseIntensity;

        /// <param name="flickerAmplitude">Fraction of the base intensity, 0..0.6.</param>
        /// <param name="noiseSeed">Stable per-light offset so neighbouring fires do not pulse together.</param>
        public void Configure(float flickerAmplitude, float noiseSeed)
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            baseRange = target.range;
            amplitude = Mathf.Clamp(flickerAmplitude, 0f, MaximumAmplitude);
            seed = noiseSeed;
            Apply(Time.time);
        }

        private void LateUpdate()
        {
            if (target)
                Apply(Time.time);
        }

        private void Apply(float time)
        {
            float t = time + seed;
            float noise = Mathf.PerlinNoise(t * 2.3f, seed * 0.37f) * 0.65f
                + Mathf.PerlinNoise(t * 7.9f, seed * 0.37f + 11.1f) * 0.35f;
            float centered = Mathf.Clamp(noise, 0f, 1f) * 2f - 1f;
            target.intensity = baseIntensity * (1f + amplitude * centered);
            target.range = baseRange * (1f + amplitude * 0.2f * centered);
        }

        private void OnDisable()
        {
            if (!target)
                return;
            target.intensity = baseIntensity;
            target.range = baseRange;
        }
    }
}
