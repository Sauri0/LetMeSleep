using System;
using System.Collections.Generic;

namespace LetMeSleep.Audio
{
    /// <summary>Streaming mono resampler and exact-size frame builder used by microphone capture.</summary>
    public sealed class VoiceSampleFramer
    {
        public const int TargetRate = 12000;
        public const int FrameSamples = 240;
        private readonly List<float> source = new List<float>(2048);
        private readonly float[] frame = new float[FrameSamples];
        private int frameCount, sourceRate;
        private double sourcePosition;
        public event Action<float[]> FrameReady;

        public VoiceSampleFramer(int sourceRate)
        {
            SetSourceRate(sourceRate);
        }

        public void SetSourceRate(int value)
        {
            if (value < 8000 || value > 192000) throw new ArgumentOutOfRangeException(nameof(value));
            if (sourceRate != value) { Clear(); sourceRate = value; }
        }

        public void Push(float[] samples, int offset, int count)
        {
            if (samples == null || offset < 0 || count < 0 || offset + count > samples.Length) throw new ArgumentOutOfRangeException();
            for (int i = 0; i < count; i++)
            {
                float value = samples[offset + i];
                source.Add(float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(-1f, Math.Min(1f, value)));
            }
            double step = (double)sourceRate / TargetRate;
            while (sourcePosition + 1 < source.Count)
            {
                int left = (int)sourcePosition;
                float fraction = (float)(sourcePosition - left);
                frame[frameCount++] = source[left] + (source[left + 1] - source[left]) * fraction;
                sourcePosition += step;
                if (frameCount == FrameSamples)
                {
                    var output = new float[FrameSamples]; Array.Copy(frame, output, FrameSamples);
                    frameCount = 0; FrameReady?.Invoke(output);
                }
            }
            int consumed = Math.Max(0, (int)sourcePosition - 1);
            if (consumed > 0) { source.RemoveRange(0, consumed); sourcePosition -= consumed; }
        }

        public void Clear()
        {
            source.Clear(); sourcePosition = 0; frameCount = 0; Array.Clear(frame, 0, frame.Length);
        }
    }
}
