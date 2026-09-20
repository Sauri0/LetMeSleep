using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using NUnit.Framework;

namespace LetMeSleep.Tests.VoiceEditMode
{
    public sealed class VoiceAudioSignalTests
    {
        [Test]
        public void Framer_ResamplesSynthetic48KhzToExact20MillisecondFrames()
        {
            const int sourceRate = 48000;
            var input = new float[sourceRate / 10];
            for (int i = 0; i < input.Length; i++) input[i] = 0.4f * (float)Math.Sin(2 * Math.PI * 300 * i / sourceRate);
            var frames = new List<float[]>();
            var framer = new VoiceSampleFramer(sourceRate); framer.FrameReady += frames.Add;
            framer.Push(input, 0, input.Length);
            Assert.That(frames.Count, Is.EqualTo(5));
            Assert.That(frames.TrueForAll(frame => frame.Length == 240), Is.True);
            Assert.That(frames.Count * 240.0 / VoiceSampleFramer.TargetRate, Is.EqualTo(0.10).Within(0.000001));
        }

        [Test]
        public void MosquitoTimbre_RaisesPitchWhilePreservingExactDuration()
        {
            const int sampleRate = 12000;
            var input = new float[sampleRate];
            for (int i = 0; i < input.Length; i++)
                input[i] = 0.4f * (float)Math.Sin(2 * Math.PI * 220 * i / sampleRate);
            var filter = new VoiceMosquitoTimbre();
            var output = new float[input.Length];
            for (int offset = 0; offset < input.Length; offset += 240)
            {
                var frame = new float[240]; Array.Copy(input, offset, frame, 0, frame.Length);
                float[] shifted = filter.Process(frame); Array.Copy(shifted, 0, output, offset, shifted.Length);
            }
            Assert.That(output.Length, Is.EqualTo(input.Length));
            double inputFrequency = EstimateFrequency(input, sampleRate, 2400);
            double outputFrequency = EstimateFrequency(output, sampleRate, 2400);
            Assert.That(outputFrequency / inputFrequency, Is.InRange(1.15, 1.40));
        }

        private static double EstimateFrequency(float[] samples, int sampleRate, int offset)
        {
            int first = -1, last = -1, crossings = 0;
            for (int i = Math.Max(1, offset); i < samples.Length; i++)
            {
                if (samples[i - 1] <= 0 && samples[i] > 0)
                {
                    if (first < 0) first = i;
                    last = i; crossings++;
                }
            }
            Assert.That(crossings, Is.GreaterThan(2));
            return (crossings - 1) * sampleRate / (double)(last - first);
        }
    }
}
