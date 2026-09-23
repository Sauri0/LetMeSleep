using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using NUnit.Framework;

namespace LetMeSleep.Tests.VoiceEditMode
{
    /// <summary>Sender conditioning: PTT fades, AGC, gate, limiter, echo guard.</summary>
    public sealed class VoiceCaptureProcessorTests
    {
        [Test]
        public void PushToTalk_FadesInOver20msAndOutOver40ms()
        {
            var processor = new VoiceCaptureProcessor();
            var tone = Tone(300, .25f, 60);
            processor.BeginTransmission();
            float[] first = processor.Process(tone[0]);
            Assert.That(Math.Abs(first[0]), Is.LessThan(.002f), "The first sample of a transmission is (almost) silent.");
            float early = Rms(first, 0, 24), late = Rms(first, 216, 24);
            Assert.That(late, Is.GreaterThan(early * 5), "Level rises across the first 20 ms.");
            for (int i = 1; i < 30; i++) processor.Process(tone[i]);
            float steady = Rms(processor.Process(tone[30]), 0, 240);

            processor.BeginRelease();
            Assert.That(processor.Released, Is.False);
            var tail = new List<float>();
            int frames = 0;
            while (!processor.Released && frames < 5) { tail.AddRange(processor.Process(tone[31 + frames])); frames++; }
            Assert.That(processor.Released, Is.True);
            Assert.That(frames, Is.EqualTo(2), "The release tail is 40 ms (two frames).");
            Assert.That(Math.Abs(tail[tail.Count - 1]), Is.LessThan(1e-4f), "The stream ends at zero, so End never cuts audio.");
            Assert.That(Rms(tail.ToArray(), 0, 60), Is.GreaterThan(steady * .8f), "The fade starts from full level.");
            float maxStep = 0; for (int i = 1; i < tail.Count; i++) maxStep = Math.Max(maxStep, Math.Abs(tail[i] - tail[i - 1]));
            Assert.That(maxStep, Is.LessThan(.12f), "No step inside the fade.");
        }

        [Test]
        public void Agc_BringsQuietAndLoudMicrophonesWithin3Db()
        {
            float quiet = SteadyOutputDb(-40f), loud = SteadyOutputDb(-10f);
            Assert.That(Math.Abs(quiet - loud), Is.LessThanOrEqualTo(3.0f), $"-40 dBFS in → {quiet:0.0} dBFS, -10 dBFS in → {loud:0.0} dBFS");
            Assert.That(loud, Is.InRange(-24f, -14f), "Speech lands near the −18 dBFS target.");
        }

        [Test]
        public void Gate_AttenuatesBackgroundNoiseButNotSpeech()
        {
            var processor = new VoiceCaptureProcessor();
            processor.BeginTransmission();
            var voice = new SpeechLikeSignal();
            var random = new Random(4);
            float speechOut = 0;
            for (int f = 0; f < 150; f++) { float[] o = processor.Process(Scale(voice.NextFrame(), -24f)); if (f >= 100) speechOut += Rms(o, 0, 240); }
            speechOut /= 50;
            float noiseOut = 0;
            for (int f = 0; f < 100; f++)
            {
                var noise = new float[240];
                for (int i = 0; i < 240; i++) noise[i] = (float)(random.NextDouble() * 2 - 1) * .0017f; // ≈ −60 dBFS
                float[] o = processor.Process(noise);
                if (f >= 50) noiseOut += Rms(o, 0, 240);
            }
            noiseOut /= 50;
            // Noise alone is at least 18 dB below where the AGC would put it (−60 dBFS + AGC gain).
            float agc = VoiceDsp.DbToGain(processor.AgcGainDb);
            Assert.That(VoiceDsp.GainToDb(noiseOut) - (-60f + processor.AgcGainDb), Is.LessThan(-18f), "Gate floor applies to noise.");
            Assert.That(speechOut, Is.GreaterThan(noiseOut * 30f), "Speech stays ≥ 30 dB above the gated noise.");
            Assert.That(agc, Is.GreaterThan(0f));
        }

        [Test]
        public void Limiter_KeepsFullScaleInputBelowMinusOneDbfs()
        {
            var processor = new VoiceCaptureProcessor();
            processor.BeginTransmission();
            float peak = 0;
            foreach (float[] frame in Tone(200, 1f, 50)) foreach (float v in processor.Process(frame)) peak = Math.Max(peak, Math.Abs(v));
            Assert.That(peak, Is.LessThanOrEqualTo(.8913f), "≤ −1 dBFS before the codec.");
        }

        [Test]
        public void EchoGuard_GatesSpeakerLeakWhileRemoteVoicesPlay()
        {
            float Leak(float farEnd)
            {
                var processor = new VoiceCaptureProcessor();
                processor.BeginTransmission();
                processor.SetFarEndLevel(farEnd);
                var voice = new SpeechLikeSignal(210f, 3);
                float sum = 0;
                for (int f = 0; f < 120; f++) { float[] o = processor.Process(Scale(voice.NextFrame(), -45f)); if (f >= 60) sum += Rms(o, 0, 240); }
                return sum / 60;
            }
            float quietRoom = Leak(0f), whileRemoteTalks = Leak(VoiceDsp.DbToGain(-20f));
            Assert.That(VoiceDsp.GainToDb(whileRemoteTalks) - VoiceDsp.GainToDb(quietRoom), Is.LessThan(-15f),
                "A −45 dBFS pickup of a −20 dBFS remote voice must not be retransmitted (echo).");
        }

        private static float SteadyOutputDb(float inputDb)
        {
            var processor = new VoiceCaptureProcessor();
            var voice = new SpeechLikeSignal();
            float sum = 0; int n = 0;
            processor.BeginTransmission();
            for (int f = 0; f < 400; f++)
            {
                float[] o = processor.Process(Scale(voice.NextFrame(), inputDb));
                if (f >= 250) { sum += Rms(o, 0, 240) * Rms(o, 0, 240); n++; }
            }
            return VoiceDsp.GainToDb((float)Math.Sqrt(sum / n));
        }

        /// <summary>Scales a speech-like frame so the signal's long-term RMS is <paramref name="db"/> dBFS.</summary>
        private static float[] Scale(float[] frame, float db)
        {
            const float syntheticRmsDb = -28.9f; // SpeechLikeSignal at gain .5
            float g = VoiceDsp.DbToGain(db - syntheticRmsDb);
            for (int i = 0; i < frame.Length; i++) frame[i] = Math.Max(-1f, Math.Min(1f, frame[i] * g));
            return frame;
        }

        private static List<float[]> Tone(double hertz, float amplitude, int frames)
        {
            var list = new List<float[]>();
            for (int f = 0; f < frames; f++)
            {
                var frame = new float[240];
                for (int i = 0; i < 240; i++) frame[i] = amplitude * (float)Math.Sin(2 * Math.PI * hertz * (f * 240 + i) / 12000.0);
                list.Add(frame);
            }
            return list;
        }

        private static float Rms(float[] x, int from, int count) => VoiceDsp.Rms(x, from, Math.Min(count, x.Length - from));
    }
}
