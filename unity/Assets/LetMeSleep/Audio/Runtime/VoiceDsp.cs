using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Small allocation-free DSP building blocks for the voice path. Pure C#: no UnityEngine calls, so the
    /// same code runs on the audio thread, in EditMode tests and in offline measurement tools.
    /// </summary>
    public static class VoiceDsp
    {
        public const float Pi = (float)Math.PI;

        public static float DbToGain(float db) => db <= -120f ? 0f : (float)Math.Pow(10.0, db / 20.0);

        public static float GainToDb(float gain) => gain <= 1e-6f ? -120f : 20f * (float)Math.Log10(gain);

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Finite value or zero (NaN/Infinity never propagate into filters).</summary>
        public static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        /// <summary>
        /// Soft limiter curve: linear below <paramref name="threshold"/>, tanh knee above it, asymptotic to
        /// <paramref name="ceiling"/>. First derivative is continuous at the knee, so it never clicks.
        /// </summary>
        public static float SoftLimit(float x, float threshold = .6f, float ceiling = .891f)
        {
            float magnitude = x < 0 ? -x : x;
            if (magnitude <= threshold) return x;
            float range = ceiling - threshold;
            float limited = threshold + range * (float)Math.Tanh((magnitude - threshold) / range);
            return x < 0 ? -limited : limited;
        }

        /// <summary>Raised-cosine ramp 0→1 for t in [0,1].</summary>
        public static float Ramp(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return .5f - .5f * (float)Math.Cos(Math.PI * t);
        }

        public static float Rms(float[] samples, int offset, int count)
        {
            if (samples == null || count <= 0) return 0f;
            double sum = 0;
            for (int i = 0; i < count; i++) { float v = samples[offset + i]; sum += v * v; }
            return (float)Math.Sqrt(sum / count);
        }

        /// <summary>Zeroes denormals and tiny values in feedback paths (x64 SSE denormals are slow).</summary>
        public static float Flush(float value) => value > -1e-20f && value < 1e-20f ? 0f : value;
    }

    /// <summary>One-pole exponential smoother, evaluated per sample.</summary>
    public struct VoiceSmoother
    {
        private float coefficient;
        public float Value;
        public float Target;

        public void Configure(float seconds, float sampleRate)
        {
            coefficient = seconds <= 0f || sampleRate <= 0f ? 1f : 1f - (float)Math.Exp(-1.0 / (seconds * sampleRate));
        }

        public void Reset(float value) { Value = value; Target = value; }

        public float Next()
        {
            Value += (Target - Value) * coefficient;
            return Value;
        }

        /// <summary>Advances <paramref name="samples"/> steps at once (block-rate smoothing).</summary>
        public float Advance(int samples)
        {
            if (samples <= 0) return Value;
            float remaining = (float)Math.Pow(1f - coefficient, samples);
            Value = Target + (Value - Target) * remaining;
            return Value;
        }
    }

    /// <summary>
    /// Topology-preserving-transform state variable filter (Zavalishin / Simper). Stable under fast cutoff
    /// modulation, which is what occlusion and distance filtering need.
    /// </summary>
    public sealed class VoiceSvf
    {
        private float a1, a2, a3, k;
        private float ic1, ic2;
        public float Low, Band, High;

        public VoiceSvf() { SetCoefficients(1000f, 12000f, .7071f); }

        public void SetCoefficients(float cutoffHz, float sampleRate, float q)
        {
            float nyquistSafe = sampleRate * .49f;
            float cutoff = VoiceDsp.Clamp(cutoffHz, 10f, nyquistSafe);
            float g = (float)Math.Tan(Math.PI * cutoff / sampleRate);
            k = 1f / Math.Max(.05f, q);
            a1 = 1f / (1f + g * (g + k));
            a2 = g * a1;
            a3 = g * a2;
        }

        public void Reset() { ic1 = ic2 = Low = Band = High = 0f; }

        public void Process(float input)
        {
            float v3 = input - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = VoiceDsp.Flush(2f * v1 - ic1);
            ic2 = VoiceDsp.Flush(2f * v2 - ic2);
            Low = v2;
            Band = v1;
            High = input - k * v1 - v2;
        }
    }

    /// <summary>
    /// Kaiser-windowed sinc kernel sampled at a fine grid. <see cref="Evaluate"/> returns the kernel at a
    /// continuous offset (in units of the lower of the two sample rates) with linear table interpolation.
    /// </summary>
    public sealed class VoiceSincKernel
    {
        private const int Resolution = 512;
        private readonly float[] table;
        public int HalfWidth { get; }
        public float Cutoff { get; }

        /// <param name="halfWidth">Zero crossings on each side (taps = 2 × halfWidth at the lower rate).</param>
        /// <param name="cutoff">Passband edge as a fraction of the lower rate's Nyquist (0.9 = 90 %).</param>
        /// <param name="beta">Kaiser window beta (≈6: −60 dB, ≈8: −80 dB stop band).</param>
        public VoiceSincKernel(int halfWidth, float cutoff, float beta)
        {
            if (halfWidth < 2 || halfWidth > 64) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (!(cutoff > .1f && cutoff <= 1f)) throw new ArgumentOutOfRangeException(nameof(cutoff));
            HalfWidth = halfWidth;
            Cutoff = cutoff;
            table = new float[halfWidth * Resolution + 2];
            double denominator = BesselI0(beta);
            for (int i = 0; i < table.Length; i++)
            {
                double x = i / (double)Resolution;
                if (x >= halfWidth) { table[i] = 0f; continue; }
                double sinc = x < 1e-9 ? 1.0 : Math.Sin(Math.PI * cutoff * x) / (Math.PI * cutoff * x);
                double ratio = x / halfWidth;
                double window = BesselI0(beta * Math.Sqrt(Math.Max(0, 1 - ratio * ratio))) / denominator;
                table[i] = (float)(cutoff * sinc * window);
            }
        }

        public float Evaluate(double offset)
        {
            double x = offset < 0 ? -offset : offset;
            if (x >= HalfWidth) return 0f;
            double position = x * Resolution;
            int index = (int)position;
            float fraction = (float)(position - index);
            return table[index] + (table[index + 1] - table[index]) * fraction;
        }

        private static double BesselI0(double x)
        {
            double sum = 1, term = 1, half = x / 2;
            for (int k = 1; k < 40; k++)
            {
                term *= half / k;
                double squared = term * term;
                sum += squared;
                if (squared < sum * 1e-12) break;
            }
            return sum;
        }
    }
}
