using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Streaming mono resampler and exact-size frame builder used by microphone capture. Devices that do not
    /// deliver 12 kHz go through a Kaiser-windowed sinc resampler whose pass band ends at 5.4 kHz, so content
    /// above the 6 kHz voice Nyquist is removed before decimation instead of folding back as aliasing (the
    /// previous linear interpolator let a 9 kHz tone alias to 3 kHz). 12 kHz input passes through untouched.
    /// </summary>
    public sealed class VoiceSampleFramer
    {
        public const int TargetRate = 12000;
        public const int FrameSamples = 240;
        private static readonly VoiceSincKernel Kernel = new VoiceSincKernel(8, .9f, 7.5f);
        private readonly float[] frame = new float[FrameSamples];
        private float[] input = new float[4096];
        private int inputCount, frameCount, sourceRate, padding;
        private double position, ratio, scale, halfSpan;
        public event Action<float[]> FrameReady;

        public VoiceSampleFramer(int sourceRate)
        {
            SetSourceRate(sourceRate);
        }

        public int SourceRate => sourceRate;

        /// <summary>Input samples the resampler waits for before it can emit (0 at 12 kHz).</summary>
        public int LatencyInputSamples => padding;

        public void SetSourceRate(int value)
        {
            if (value < 8000 || value > 192000) throw new ArgumentOutOfRangeException(nameof(value));
            if (sourceRate == value) return;
            sourceRate = value;
            ratio = value / (double)TargetRate;
            scale = Math.Max(1.0, ratio);
            halfSpan = Kernel.HalfWidth * scale;
            padding = value == TargetRate ? 0 : (int)Math.Ceiling(halfSpan);
            Clear();
        }

        public void Push(float[] samples, int offset, int count)
        {
            if (samples == null || offset < 0 || count < 0 || offset + count > samples.Length) throw new ArgumentOutOfRangeException();
            if (sourceRate == TargetRate)
            {
                for (int i = 0; i < count; i++) Emit(Sanitize(samples[offset + i]));
                return;
            }
            EnsureCapacity(inputCount + count);
            for (int i = 0; i < count; i++) input[inputCount++] = Sanitize(samples[offset + i]);

            while (position + halfSpan < inputCount - 1)
            {
                int first = (int)Math.Ceiling(position - halfSpan);
                int last = (int)Math.Floor(position + halfSpan);
                if (first < 0) first = 0;
                double sum = 0, weightSum = 0;
                for (int n = first; n <= last; n++)
                {
                    float w = Kernel.Evaluate((n - position) / scale);
                    sum += input[n] * w;
                    weightSum += w;
                }
                Emit(weightSum > 1e-9 ? (float)(sum / weightSum) : 0f);
                position += ratio;
            }

            int keep = (int)Math.Floor(position - halfSpan) - 1;
            if (keep > 0)
            {
                Array.Copy(input, keep, input, 0, inputCount - keep);
                inputCount -= keep;
                position -= keep;
            }
        }

        public void Clear()
        {
            frameCount = 0;
            Array.Clear(frame, 0, frame.Length);
            inputCount = padding;
            if (input.Length < padding + 16) input = new float[padding + 4096];
            Array.Clear(input, 0, padding);
            position = padding;
        }

        private void Emit(float value)
        {
            frame[frameCount++] = value;
            if (frameCount < FrameSamples) return;
            var output = new float[FrameSamples];
            Array.Copy(frame, output, FrameSamples);
            frameCount = 0;
            FrameReady?.Invoke(output);
        }

        private void EnsureCapacity(int needed)
        {
            if (needed <= input.Length) return;
            int size = input.Length;
            while (size < needed) size *= 2;
            Array.Resize(ref input, size);
        }

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(-1f, Math.Min(1f, value));
    }
}
