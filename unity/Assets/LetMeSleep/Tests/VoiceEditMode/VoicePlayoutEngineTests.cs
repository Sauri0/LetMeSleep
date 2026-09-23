using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using NUnit.Framework;

namespace LetMeSleep.Tests.VoiceEditMode
{
    /// <summary>Stability of the real-time playout: start latency, continuity, loss, hitches, drift, fades.</summary>
    public sealed class VoicePlayoutEngineTests
    {
        [Test]
        public void CleanLink_StartsWithin200msAndNeverDropsOut()
        {
            var link = new VoiceLinkSimulation();
            link.Run(.2, 3.0, 3.8);
            double first = link.FirstSound();
            Assert.That(first - .2, Is.LessThanOrEqualTo(.200), "Continuous voice must start ≤ 200 ms after the first frame is sent.");
            Assert.That(link.LongestSilence(first + .03, 3.1), Is.LessThan(.005), "No ≥ 5 ms dropout while the peer talks.");
            Assert.That(link.Engine.Starvations, Is.Zero);
            Assert.That(link.WorstStepRatio(first, link.LastSound() + .05), Is.LessThan(12), "No click (harness ratio 12).");
        }

        [TestCase(.05, .040)]
        [TestCase(.10, .060)]
        public void PacketLoss_IsConcealedWithoutSilenceOrClicks(double loss, double jitter)
        {
            var link = new VoiceLinkSimulation { Loss = loss, Jitter = jitter, Seed = 21 };
            link.Run(.2, 5.0, 5.9);
            double first = link.FirstSound();
            Assert.That(link.Lost, Is.GreaterThan(0), "The scenario must actually lose packets.");
            Assert.That(link.Concealed, Is.GreaterThanOrEqualTo(link.Lost - 2), "Every lost frame inside the stream is concealed.");
            Assert.That(link.LongestSilence(first + .03, 5.1), Is.LessThan(.005), "Loss must be concealed, never a hole.");
            Assert.That(link.WorstStepRatio(first, link.LastSound() + .05), Is.LessThan(12));
            Assert.That(first - .2, Is.LessThanOrEqualTo(.230));
        }

        [Test]
        public void MainThreadHitch_IsBridgedAndTheTargetGrows()
        {
            var link = new VoiceLinkSimulation { HitchAt = 1.5, HitchSeconds = .07 };
            link.Run(.2, 3.0, 3.8);
            double first = link.FirstSound();
            Assert.That(link.Engine.Starvations, Is.GreaterThanOrEqualTo(1), "A 70 ms freeze empties a 47 ms buffer.");
            Assert.That(link.LongestSilence(first + .03, 3.1), Is.LessThan(.005), "The underrun is concealed, not silent.");
            Assert.That(link.Engine.TargetSeconds, Is.GreaterThanOrEqualTo(.06f), "The prebuffer adapts after an underrun (starts at ≈ 47 ms).");
            Assert.That(link.WorstStepRatio(first, link.LastSound() + .05), Is.LessThan(12));
        }

        [TestCase(.004)]
        [TestCase(-.004)]
        public void ClockDrift_IsAbsorbedWithoutLatencyCreep(double drift)
        {
            var link = new VoiceLinkSimulation { SenderDrift = drift };
            link.Run(.2, 20.0, 20.8);
            double first = link.FirstSound();
            Assert.That(link.LongestSilence(first + .03, 20.1), Is.LessThan(.005));
            Assert.That(link.Engine.Starvations, Is.LessThanOrEqualTo(1));
            // Latency at the end of 20 s: the stream stops within target + 60 ms of the talk end (+ network).
            double tail = link.LastSound() - 20.2;
            Assert.That(tail, Is.LessThan(link.Engine.TargetSeconds + .03 + .04 + .08), "Drift correction keeps latency bounded.");
        }

        [Test]
        public void EndOfStream_FadesInsteadOfCutting()
        {
            var engine = new VoicePlayoutEngine { OutputRate = 48000 };
            engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            var loud = new float[4800];
            for (int i = 0; i < loud.Length; i++) loud[i] = .5f * (float)Math.Sin(2 * Math.PI * 300 * i / 12000.0);
            engine.Enqueue(loud, 0, loud.Length);
            engine.MarkStreamEnd();
            var output = Render(engine, 48000 / 1024 + 3);
            int last = LastAbove(output, 1e-4f);
            Assert.That(last, Is.GreaterThan(0));
            // Peak of the final 2 ms is well under the steady level: the stream ends on a ramp, not a step.
            float tailPeak = PeakOf(output, last - 96, last + 1);
            Assert.That(tailPeak, Is.LessThan(.1f), "The last 2 ms before silence must be faded.");
            Assert.That(engine.Starvations, Is.Zero, "A marked end is not an underrun.");
        }

        [Test]
        public void FadeClear_ReachesSilenceWithin6msWithoutStep()
        {
            var engine = new VoicePlayoutEngine { OutputRate = 48000 };
            engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            var feed = new Feed(i => .5f * (float)Math.Sin(2 * Math.PI * 200 * i / 12000.0));
            feed.Render(engine, 12);
            engine.RequestFadeClear();
            float[] block = Render(engine, 1);
            int silentFrom = -1;
            for (int i = 0; i < block.Length; i++) if (Math.Abs(block[i]) > 1e-5f) silentFrom = i + 1;
            Assert.That(silentFrom, Is.LessThan(48 * 7), "Voice is gone ≈ 5 ms (plus the resampler's half width) after the clear.");
            float maxStep = 0;
            for (int i = 1; i < block.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(block[i] - block[i - 1]));
            Assert.That(maxStep, Is.LessThan(.05f), "A 200 Hz tone at −6 dBFS moves ≤ 0.013 per sample at 48 kHz; a step would be ≥ 0.1.");
            Assert.That(engine.BufferedSamples, Is.Zero, "Queued audio is dropped once faded.");
        }

        [Test]
        public void OcclusionChange_GlidesAndCutsHighFrequencies()
        {
            var engine = new VoicePlayoutEngine { OutputRate = 48000 };
            engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            var random = new Random(3);
            var feed = new Feed(i => (float)(random.NextDouble() * 2 - 1) * .2f);
            float[] open = feed.Render(engine, 40);
            var occluded = VoicePlayoutParameters.Open(Online.VoiceSpatialPolicy.OcclusionGain(1f, 1));
            occluded.LowPassHz = Online.VoiceSpatialPolicy.OcclusionLowPassHertz(1f, 1);
            engine.SetParameters(occluded);
            float[] transition = feed.Render(engine, 1);
            float[] closed = feed.Render(engine, 30);
            float openHf = HighBandRms(open, 3500), closedHf = HighBandRms(closed, 3500);
            float openRms = Rms(open, 0, open.Length), closedRms = Rms(closed, 0, closed.Length);
            Assert.That(20 * Math.Log10(openRms / closedRms), Is.GreaterThanOrEqualTo(6.0), "Wall: ≥ 6 dB quieter.");
            Assert.That(20 * Math.Log10(openHf / closedHf) - 20 * Math.Log10(openRms / closedRms), Is.GreaterThanOrEqualTo(6.0), "Wall: ≥ 6 dB extra above 3.5 kHz.");
            // The first 5 ms after the change are still close to the open level (12 ms gain glide, 40 ms filter glide).
            float firstMs = Rms(transition, 0, 240), steadyOpen = Rms(open, open.Length - 4800, 4800);
            Assert.That(firstMs, Is.GreaterThan(steadyOpen * .55f), "No instantaneous jump when occlusion changes.");
        }

        [Test]
        public void RoomReverb_InteriorTailIsTenDecibelsAboveExterior()
        {
            double interior = TailRatioDb(new VoicePlayoutParameters { DirectGain = 1, ReverbSend = .3f, LowPassHz = 5500, ReverbDecaySeconds = .6f, ReverbDampingHz = 3500, Crossfeed = .15f });
            double exterior = TailRatioDb(new VoicePlayoutParameters { DirectGain = 1, ReverbSend = .035f, LowPassHz = 5500, ReverbDecaySeconds = .25f, ReverbDampingHz = 4500, Crossfeed = .15f });
            Assert.That(interior - exterior, Is.GreaterThanOrEqualTo(10.0), $"interior {interior:0.0} dB vs exterior {exterior:0.0} dB (tail 50–600 ms re burst)");
        }

        [Test]
        public void SpatialGains_ArePreservedWithCrossfeedInsteadOfHardPan()
        {
            var engine = new VoicePlayoutEngine { OutputRate = 48000 };
            engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            var feed = new Feed(i => .3f * (float)Math.Sin(2 * Math.PI * 500 * i / 12000.0));
            feed.Prefill(engine);
            double left = 0, right = 0;
            for (int b = 0; b < 30; b++)
            {
                feed.Push(engine);
                var data = new float[2048];
                for (int i = 0; i < 1024; i++) { data[i * 2] = 1f; data[i * 2 + 1] = 0f; } // source fully to the left
                engine.Render(data, 2);
                if (b < 5) continue;
                for (int i = 0; i < 1024; i++) { left += data[i * 2] * data[i * 2]; right += data[i * 2 + 1] * data[i * 2 + 1]; }
            }
            double ild = 10 * Math.Log10(left / right);
            Assert.That(ild, Is.InRange(9.0, 26.0), "Clear side, but the far ear is not digitally silent.");
        }

        /// <summary>Feeds a generator at the real-time rate (256 input samples per 1024-frame block at 48 kHz).</summary>
        private sealed class Feed
        {
            private readonly Func<int, float> generator;
            private int position;
            public Feed(Func<int, float> generator) { this.generator = generator; }

            public void Prefill(VoicePlayoutEngine engine) => Push(engine, 720);

            public void Push(VoicePlayoutEngine engine, int count = 256)
            {
                var chunk = new float[count];
                for (int i = 0; i < count; i++) chunk[i] = generator(position++);
                engine.Enqueue(chunk, 0, count);
            }

            public float[] Render(VoicePlayoutEngine engine, int blocks)
            {
                if (position == 0) Prefill(engine);
                var mono = new List<float>();
                for (int b = 0; b < blocks; b++)
                {
                    Push(engine);
                    mono.AddRange(VoicePlayoutEngineTests.Render(engine, 1));
                }
                return mono.ToArray();
            }
        }

        private static double TailRatioDb(VoicePlayoutParameters parameters)
        {
            var engine = new VoicePlayoutEngine { OutputRate = 48000 };
            engine.SetParameters(parameters, true);
            var burst = new float[1200];
            var random = new Random(9);
            for (int i = 0; i < burst.Length; i++) burst[i] = (float)(random.NextDouble() * 2 - 1) * .4f;
            engine.Enqueue(burst, 0, burst.Length);
            engine.MarkStreamEnd();
            float[] output = Render(engine, 60);
            int end = LastAbove(output, .05f);
            double burstEnergy = Energy(output, Math.Max(0, end - 4800), end);
            double tail = Energy(output, end + 2400, end + 28800);
            return 10 * Math.Log10(tail / burstEnergy);
        }

        private static float[] Render(VoicePlayoutEngine engine, int blocks)
        {
            var mono = new List<float>();
            for (int b = 0; b < blocks; b++)
            {
                var data = new float[2048];
                for (int i = 0; i < data.Length; i++) data[i] = .7071f;
                engine.Render(data, 2);
                for (int i = 0; i < 1024; i++) mono.Add(data[i * 2]);
            }
            return mono.ToArray();
        }

        private static int LastAbove(float[] x, float threshold)
        {
            for (int i = x.Length - 1; i >= 0; i--) if (Math.Abs(x[i]) > threshold) return i;
            return -1;
        }

        private static float PeakOf(float[] x, int from, int to)
        {
            float peak = 0; for (int i = Math.Max(0, from); i < Math.Min(x.Length, to); i++) peak = Math.Max(peak, Math.Abs(x[i])); return peak;
        }

        private static double Energy(float[] x, int from, int to)
        {
            double sum = 1e-20; for (int i = Math.Max(0, from); i < Math.Min(x.Length, to); i++) sum += x[i] * x[i]; return sum;
        }

        private static float Rms(float[] x, int from, int count)
        {
            double sum = 0; int n = 0;
            for (int i = Math.Max(0, from); i < Math.Min(x.Length, from + count); i++) { sum += x[i] * x[i]; n++; }
            return (float)Math.Sqrt(sum / Math.Max(1, n));
        }

        /// <summary>RMS above <paramref name="cutoff"/> Hz (4th-order TPT high-pass at 48 kHz).</summary>
        private static float HighBandRms(float[] x, float cutoff)
        {
            var a = new VoiceSvf(); var b = new VoiceSvf();
            a.SetCoefficients(cutoff, 48000f, .5412f); b.SetCoefficients(cutoff, 48000f, 1.3066f);
            double sum = 0;
            for (int i = 0; i < x.Length; i++) { a.Process(x[i]); b.Process(a.High); sum += b.High * b.High; }
            return (float)Math.Sqrt(sum / Math.Max(1, x.Length));
        }
    }
}
