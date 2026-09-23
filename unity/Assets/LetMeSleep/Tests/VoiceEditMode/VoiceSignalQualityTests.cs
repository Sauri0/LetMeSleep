using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.VoiceEditMode
{
    /// <summary>Concealment smoothness, anti-aliasing, mosquito timbre level and the codec encoder.</summary>
    public sealed class VoiceSignalQualityTests
    {
        [Test]
        public void Concealer_BridgesSingleLossesWithoutStepsOrSilence()
        {
            var voice = new SpeechLikeSignal();
            var concealer = new VoiceConcealer();
            var random = new Random(8);
            var output = new List<float>();
            int lost = 0;
            for (int f = 0; f < 250; f++)
            {
                float[] frame = voice.NextFrame();
                bool drop = f > 10 && random.NextDouble() < .08; // 8 % random loss
                if (drop) lost++;
                output.AddRange(drop ? concealer.Conceal() : concealer.Accept(frame));
            }
            Assert.That(lost, Is.GreaterThan(10));
            float[] x = output.ToArray();
            int longestSilence = 0, run = 0;
            for (int i = 240 * 11; i < x.Length; i++) { if (Math.Abs(x[i]) < 1e-5f) { run++; longestSilence = Math.Max(longestSilence, run); } else run = 0; }
            Assert.That(longestSilence, Is.LessThan(60), "No 5 ms hole where a single packet was lost.");
            Assert.That(WorstStepRatio(x, 240 * 11), Is.LessThan(12.0), "No click at the loss or recovery boundaries.");
        }

        [Test]
        public void Concealer_LongLossFadesToSilenceWithin60ms()
        {
            var voice = new SpeechLikeSignal();
            var concealer = new VoiceConcealer();
            for (int f = 0; f < 20; f++) concealer.Accept(voice.NextFrame());
            var lost = new List<float>();
            for (int f = 0; f < 5; f++) lost.AddRange(concealer.Conceal());
            float early = VoiceDsp.Rms(lost.ToArray(), 0, 120), late = VoiceDsp.Rms(lost.ToArray(), 720, 240);
            Assert.That(early, Is.GreaterThan(.01f), "The first 10 ms continue the voice.");
            Assert.That(late, Is.LessThan(1e-4f), "After 60 ms of loss the continuation is silent (no endless buzz).");
        }

        [Test]
        public void Framer_RejectsAliasingFrom48kHzDevices()
        {
            const int rate = 48000;
            double Measure(double hertz, double probe)
            {
                var framer = new VoiceSampleFramer(rate);
                var output = new List<float>();
                framer.FrameReady += output.AddRange;
                var input = new float[rate];
                for (int i = 0; i < input.Length; i++) input[i] = .5f * (float)Math.Sin(2 * Math.PI * hertz * i / rate);
                framer.Push(input, 0, input.Length);
                // Correlate against the probe frequency at 12 kHz (the alias of 9 kHz lands on 3 kHz).
                double re = 0, im = 0; int n = 0;
                for (int i = 1200; i < output.Count; i++, n++)
                {
                    re += output[i] * Math.Cos(2 * Math.PI * probe * i / 12000.0);
                    im += output[i] * Math.Sin(2 * Math.PI * probe * i / 12000.0);
                }
                return 2 * Math.Sqrt(re * re + im * im) / n;
            }
            double passband = Measure(1000, 1000), alias = Measure(9000, 3000);
            Assert.That(20 * Math.Log10(passband / .5), Is.InRange(-.5, .5), "1 kHz passes untouched.");
            Assert.That(20 * Math.Log10(alias / .5 + 1e-12), Is.LessThan(-40.0), "A 9 kHz tone must not fold to 3 kHz.");
        }

        [Test]
        public void MosquitoTimbre_KeepsLevelWithin1DbAndFlushesItsTail()
        {
            var voice = new SpeechLikeSignal(140f);
            var timbre = new VoiceMosquitoTimbre();
            double inPower = 0, outPower = 0;
            for (int f = 0; f < 300; f++)
            {
                float[] frame = voice.NextFrame();
                float[] shifted = timbre.Process(frame);
                Assert.That(shifted.Length, Is.EqualTo(frame.Length));
                if (f < 50) continue;
                foreach (float v in frame) inPower += v * v;
                foreach (float v in shifted) outPower += v * v;
            }
            Assert.That(10 * Math.Log10(outPower / inPower), Is.InRange(-1.0, 1.0));
            float[] tail = timbre.Flush();
            Assert.That(VoiceDsp.Rms(tail, 0, timbre.LatencyInSamples - 40), Is.GreaterThan(.01f), "The last word is still inside the shifter and comes out on flush.");
        }

        [Test]
        public void ImaEncoder_PicksTheBestInitialStepWithoutChangingTheWireFormat()
        {
            var voice = new SpeechLikeSignal(120f);
            var codec = new VoiceImaAdpcmCodec();
            double signal = 0, error = 0;
            for (int f = 0; f < 300; f++)
            {
                float[] frame = voice.NextFrame();
                byte[] payload = codec.Encode(frame);
                Assert.That(payload.Length, Is.EqualTo(VoiceProtocol.MaximumCodecBytes));
                Assert.That(payload[5], Is.LessThanOrEqualTo(88), "Step index stays inside the decoder's table.");
                Assert.That(codec.TryDecode(new ArraySegment<byte>(payload), out float[] decoded), Is.True);
                for (int i = 0; i < frame.Length; i++) { signal += frame[i] * frame[i]; error += (frame[i] - decoded[i]) * (frame[i] - decoded[i]); }
            }
            Assert.That(10 * Math.Log10(signal / error), Is.GreaterThan(16.0), "Legacy heuristic measured 15.3–15.6 dB on this signal.");
        }

        private static double WorstStepRatio(float[] x, int from)
        {
            const int window = 120;
            double worst = 0, sum = 0;
            var steps = new double[x.Length];
            for (int i = from + 1; i < x.Length; i++) steps[i] = Math.Abs(x[i] - x[i - 1]);
            for (int i = from + 1; i < x.Length; i++)
            {
                sum += steps[i] * steps[i];
                if (i - window > from) sum -= steps[i - window] * steps[i - window];
                if (i < from + window + 1) continue;
                double local = Math.Sqrt(Math.Max(1e-18, (sum - steps[i] * steps[i]) / (window - 1)));
                if (steps[i] > 3e-3) worst = Math.Max(worst, steps[i] / local);
            }
            return worst;
        }
    }
}
