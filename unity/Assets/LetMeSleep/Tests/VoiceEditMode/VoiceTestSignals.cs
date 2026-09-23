using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using LetMeSleep.Online;

namespace LetMeSleep.Tests.VoiceEditMode
{
    /// <summary>Deterministic speech-like signal at 12 kHz (same recipe as the audio evidence harness).</summary>
    internal sealed class SpeechLikeSignal
    {
        private const int Rate = 12000;
        private readonly Random random;
        private readonly float baseF0;
        private double phase;
        private long sample;
        private float noiseState;

        public SpeechLikeSignal(float f0 = 130f, int seed = 11) { baseF0 = f0; random = new Random(seed); }

        public float[] NextFrame(float gain = .5f)
        {
            var output = new float[240];
            for (int i = 0; i < output.Length; i++, sample++)
            {
                double t = sample / (double)Rate;
                double syllable = (t * 4.5) % 1.0;
                double envelope = .3 + .7 * Math.Pow(Math.Sin(Math.PI * syllable), 2);
                double f0 = baseF0 * (1 + .08 * Math.Sin(2 * Math.PI * .7 * t) + .03 * Math.Sin(2 * Math.PI * 5.5 * t));
                phase += f0 / Rate; phase -= Math.Floor(phase);
                double vowel = .5 + .5 * Math.Sin(2 * Math.PI * .9 * t);
                double f1 = 450 + 350 * vowel, f2 = 1100 + 900 * (1 - vowel), f3 = 2600;
                double voiced = 0;
                for (int k = 1; k * f0 < Rate / 2 - 200; k++)
                {
                    double frequency = k * f0;
                    double formant = Formant(frequency, f1, 90) + .7 * Formant(frequency, f2, 120) + .35 * Formant(frequency, f3, 180);
                    voiced += Math.Sin(2 * Math.PI * k * phase) / Math.Sqrt(k) * (.05 + formant);
                }
                float white = (float)(random.NextDouble() * 2 - 1);
                float hiss = white - noiseState; noiseState = white;
                double fricative = syllable < .12 ? .08 * hiss * (.5 - .5 * Math.Cos(2 * Math.PI * syllable / .12)) : 0;
                output[i] = (float)Math.Max(-1, Math.Min(1, gain * (envelope * voiced * .22 + fricative)));
            }
            return output;
        }

        private static double Formant(double frequency, double centre, double bandwidth)
        {
            double x = (frequency - centre) / bandwidth;
            return Math.Exp(-.5 * x * x);
        }
    }

    /// <summary>
    /// Real-time simulation of the receive path with the production classes: packets (optionally lossy,
    /// jittered, reordered) → VoiceJitterBuffer → IMA ADPCM decode → VoiceConcealer → VoicePlayoutEngine,
    /// main thread ticking at a game frame rate, audio thread rendering 1024-frame blocks at 48 kHz with the
    /// centre-pan gains Unity would pass to OnAudioFilterRead.
    /// </summary>
    internal sealed class VoiceLinkSimulation
    {
        public const int OutputRate = 48000, Block = 1024;
        public double Loss, Jitter, BaseLatency = .03, FrameSeconds = 1 / 60.0, FrameJitter, SenderDrift, ReorderShare;
        public double HitchAt = -1, HitchSeconds;
        public int Seed = 5;
        public readonly VoicePlayoutEngine Engine = new VoicePlayoutEngine { OutputRate = OutputRate };
        public readonly List<float> Left = new List<float>();
        public int Sent, Lost, Concealed, StreamsClosed;

        public void Run(double talkStart, double talkSeconds, double totalSeconds, SpeechLikeSignal voice = null)
        {
            voice = voice ?? new SpeechLikeSignal();
            var random = new Random(Seed);
            var codec = new VoiceImaAdpcmCodec();
            var jitter = new VoiceJitterBuffer();
            var concealer = new VoiceConcealer();
            var inFlight = new List<(double arrival, uint sequence, byte[] payload, bool end)>();
            Engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            double t = 0, nextSend = talkStart, nextTick = 0, nextAudio = 0;
            uint sequence = 0;
            bool talking = true, wasActive = false, hitchDone = false;
            while (t < totalSeconds)
            {
                if (talking && t >= nextSend)
                {
                    if (t >= talkStart + talkSeconds)
                    {
                        talking = false;
                        inFlight.Add((t + BaseLatency + Jitter, ++sequence, null, true));
                    }
                    else
                    {
                        byte[] payload = codec.Encode(voice.NextFrame());
                        sequence++; Sent++;
                        double arrival = t + BaseLatency + random.NextDouble() * Jitter + (random.NextDouble() < ReorderShare ? .025 : 0);
                        if (random.NextDouble() < Loss) Lost++; else inFlight.Add((arrival, sequence, payload, false));
                        nextSend += .02 / (1 + SenderDrift);
                    }
                }
                if (t >= nextTick)
                {
                    inFlight.Sort((a, b) => a.arrival.CompareTo(b.arrival));
                    while (inFlight.Count > 0 && inFlight[0].arrival <= t)
                    {
                        var packet = inFlight[0]; inFlight.RemoveAt(0);
                        if (packet.end) jitter.AcceptEnd(1, packet.sequence, t); else jitter.AcceptAudio(1, packet.sequence, packet.payload, t);
                    }
                    for (int emitted = 0; emitted < 3 && jitter.TryDequeue(t, out byte[] payload, out bool concealed); emitted++)
                    {
                        codec.TryDecode(new ArraySegment<byte>(payload), out float[] decoded);
                        float[] frame = concealed ? concealer.Conceal() : concealer.Accept(decoded);
                        if (concealed) Concealed++;
                        Engine.Enqueue(frame, 0, frame.Length);
                    }
                    if (wasActive && !jitter.IsActive) { Engine.MarkStreamEnd(); concealer.Reset(); StreamsClosed++; }
                    wasActive = jitter.IsActive;
                    double frame2 = FrameSeconds + (random.NextDouble() * 2 - 1) * FrameJitter;
                    if (!hitchDone && HitchAt >= 0 && t >= HitchAt) { frame2 += HitchSeconds; hitchDone = true; }
                    nextTick = t + Math.Max(.001, frame2);
                }
                if (t >= nextAudio)
                {
                    var data = new float[Block * 2];
                    for (int i = 0; i < data.Length; i++) data[i] = .7071f;
                    Engine.Render(data, 2);
                    for (int i = 0; i < Block; i++) Left.Add(data[i * 2]);
                    nextAudio += Block / (double)OutputRate;
                }
                t += .0005;
            }
        }

        /// <summary>Longest run of digital silence (|x| &lt; 1e-5) in seconds within [from, to).</summary>
        public double LongestSilence(double from, double to)
        {
            int a = Math.Max(0, (int)(from * OutputRate)), b = Math.Min(Left.Count, (int)(to * OutputRate));
            int run = 0, longest = 0;
            for (int i = a; i < b; i++) { if (Math.Abs(Left[i]) < 1e-5f) { run++; if (run > longest) longest = run; } else run = 0; }
            return longest / (double)OutputRate;
        }

        /// <summary>First time (s) with output above −80 dBFS.</summary>
        public double FirstSound()
        {
            for (int i = 0; i < Left.Count; i++) if (Math.Abs(Left[i]) > 1e-4f) return i / (double)OutputRate;
            return double.PositiveInfinity;
        }

        public double LastSound()
        {
            for (int i = Left.Count - 1; i >= 0; i--) if (Math.Abs(Left[i]) > 1e-4f) return i / (double)OutputRate;
            return double.NegativeInfinity;
        }

        /// <summary>Largest sample-to-sample step relative to the local RMS of steps (click detector, like the harness).</summary>
        public double WorstStepRatio(double from, double to)
        {
            int a = Math.Max(1, (int)(from * OutputRate)), b = Math.Min(Left.Count, (int)(to * OutputRate));
            const int window = 480;
            double worst = 0;
            var steps = new double[Math.Max(0, b - a)];
            for (int i = a; i < b; i++) steps[i - a] = Math.Abs(Left[i] - Left[i - 1]);
            double sum = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                sum += steps[i] * steps[i];
                if (i >= window) sum -= steps[i - window] * steps[i - window];
                if (i < window) continue;
                double local = Math.Sqrt(Math.Max(1e-18, (sum - steps[i] * steps[i]) / (window - 1)));
                if (steps[i] > 3e-3) worst = Math.Max(worst, steps[i] / local);
            }
            return worst;
        }
    }
}
