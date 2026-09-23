using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Mosquito voice: raises pitch and formants (+4 semitones by default) without changing duration, then
    /// adds a light wing flutter and a small presence lift so it reads as "insect" while staying intelligible.
    ///
    /// Pitch shift = WSOLA time stretch by α (waveform-similarity overlap-add: each 20 ms Hann grain is placed
    /// where it best continues the previous one, ±5 ms search) followed by band-limited resampling by α. The old
    /// two-head delay-line shifter crossfaded uncorrelated heads at ≈8 Hz, which left an audible level/phase
    /// warble. Output level tracks the input level (±1 dB). Exactly one output sample per input sample, with a
    /// constant 26 ms latency; <see cref="Flush"/> drains it at the end of a stream.
    /// </summary>
    public sealed class VoiceMosquitoTimbre
    {
        public const int SampleRate = 12000;
        private const int Window = 240;               // 20 ms grains
        private const int Hop = Window / 2;           // synthesis hop, 50 % overlap (Hann sums to one)
        private const int Search = 60;                // ±5 ms: covers half a period down to 100 Hz
        private const int LatencySamples = Window + Search + 12;
        private const float FlutterHz = 38f;
        private const float FlutterDepth = .12f;
        private static readonly float[] Hann = BuildHann();

        private float[] input = new float[4096];
        private long inputBase;                       // absolute index of input[0]
        private int inputCount;
        private double analysisPosition;              // absolute nominal start of the next grain
        private long previousGrain = -1;
        private readonly float[] overlap = new float[Window];
        private float[] stretched = new float[4096];
        private int stretchedCount;
        private double readPosition;
        private float[] output = new float[4096];
        private int outputCount;
        private readonly VoiceSvf antiAlias1 = new VoiceSvf(), antiAlias2 = new VoiceSvf(), presence = new VoiceSvf();
        private double flutterPhase;
        private float inputPower = 1e-6f, outputPower = 1e-6f, levelGain = 1f;
        private bool primed;

        public VoiceMosquitoTimbre() { Reset(); }

        public int LatencyInSamples => LatencySamples;

        public float[] Process(float[] inputFrame, float semitones = 4f)
        {
            if (inputFrame == null) throw new ArgumentNullException(nameof(inputFrame));
            if (float.IsNaN(semitones) || float.IsInfinity(semitones) || semitones < 0 || semitones > 7)
                throw new ArgumentOutOfRangeException(nameof(semitones));
            var result = new float[inputFrame.Length];
            if (inputFrame.Length == 0) return result;
            if (!primed) { EnsureOutput(LatencySamples); outputCount = LatencySamples; primed = true; }

            double alpha = Math.Pow(2.0, semitones / 12.0);
            double inPower = 0;
            EnsureInput(inputCount + inputFrame.Length);
            for (int i = 0; i < inputFrame.Length; i++)
            {
                float v = inputFrame[i];
                v = float.IsNaN(v) || float.IsInfinity(v) ? 0f : Math.Max(-1f, Math.Min(1f, v));
                input[inputCount++] = v;
                inPower += v * v;
            }
            Stretch(alpha);
            Resample(alpha);

            int count = Math.Min(result.Length, outputCount);
            double outPower = 0;
            float flutterNorm = 1f / (1f - FlutterDepth * .5f);
            for (int i = 0; i < count; i++)
            {
                flutterPhase += FlutterHz / SampleRate;
                if (flutterPhase >= 1) flutterPhase -= 1;
                float flutter = 1f - FlutterDepth * (.5f + .5f * (float)Math.Sin(2 * Math.PI * flutterPhase));
                presence.Process(output[i]);
                float shaped = (output[i] + .35f * presence.Band) * flutter * flutterNorm;
                result[i] = shaped;
                outPower += shaped * shaped;
            }
            // Level compensation from slow power averages (≈ 300 ms), ramped across the frame.
            float blend = 1f - (float)Math.Exp(-inputFrame.Length / (.3 * SampleRate));
            inputPower += ((float)(inPower / inputFrame.Length) - inputPower) * blend;
            outputPower += ((float)(outPower / Math.Max(1, count)) - outputPower) * blend;
            float targetGain = levelGain;
            if (outputPower > 1e-7f && inputPower > 1e-7f)
                targetGain = VoiceDsp.Clamp((float)Math.Sqrt(inputPower / outputPower), .7f, 1.6f);
            for (int i = 0; i < count; i++)
            {
                float g = levelGain + (targetGain - levelGain) * ((i + 1) / (float)count);
                result[i] = VoiceDsp.SoftLimit(result[i] * g, .7f, .95f);
            }
            levelGain = targetGain;
            outputCount -= count;
            Array.Copy(output, count, output, 0, outputCount);
            return result;
        }

        /// <summary>Returns the samples still held by the shifter's latency (the end of the last word).</summary>
        public float[] Flush(float semitones = 4f)
        {
            var silence = new float[240];
            var first = Process(silence, semitones);
            var second = Process(silence, semitones);
            var tail = new float[first.Length + second.Length];
            Array.Copy(first, tail, first.Length);
            Array.Copy(second, 0, tail, first.Length, second.Length);
            return tail;
        }

        public void Reset()
        {
            Array.Clear(input, 0, input.Length); inputCount = 0; inputBase = 0;
            analysisPosition = 0; previousGrain = -1;
            Array.Clear(overlap, 0, overlap.Length);
            stretchedCount = 0; readPosition = 0; outputCount = 0; primed = false;
            antiAlias1.SetCoefficients(4300f, SampleRate, .5412f); antiAlias1.Reset();
            antiAlias2.SetCoefficients(4300f, SampleRate, 1.3066f); antiAlias2.Reset();
            presence.SetCoefficients(2700f, SampleRate, 1.1f); presence.Reset();
            flutterPhase = 0; inputPower = outputPower = 1e-6f; levelGain = 1f;
        }

        private void Stretch(double alpha)
        {
            double analysisHop = Hop / alpha;
            while (true)
            {
                long nominal = (long)Math.Round(analysisPosition);
                if (nominal + Search + Window > inputBase + inputCount) break;
                long chosen = nominal;
                if (previousGrain >= 0)
                {
                    long natural = previousGrain + Hop;
                    int bestOffset = 0;
                    double bestScore = double.NegativeInfinity;
                    for (int offset = -Search; offset <= Search; offset++)
                    {
                        long candidate = nominal + offset;
                        if (candidate < inputBase) continue;
                        double cross = 0, energy = 1e-9;
                        for (int n = 0; n < Hop; n += 2)
                        {
                            float a = input[candidate - inputBase + n];
                            cross += a * input[natural - inputBase + n];
                            energy += a * a;
                        }
                        double score = cross / Math.Sqrt(energy);
                        if (score > bestScore) { bestScore = score; bestOffset = offset; }
                    }
                    chosen = nominal + bestOffset;
                    if (chosen < inputBase) chosen = inputBase;
                }
                int start = (int)(chosen - inputBase);
                for (int n = 0; n < Window; n++) overlap[n] += Hann[n] * input[start + n];
                EnsureStretched(stretchedCount + Hop);
                for (int n = 0; n < Hop; n++)
                {
                    antiAlias1.Process(overlap[n]);
                    antiAlias2.Process(antiAlias1.Low);
                    stretched[stretchedCount++] = antiAlias2.Low;
                }
                Array.Copy(overlap, Hop, overlap, 0, Window - Hop);
                Array.Clear(overlap, Window - Hop, Hop);
                previousGrain = chosen;
                analysisPosition += analysisHop;
            }
            // Keep what the next search can still reach (natural continuation and the search window).
            long oldestNeeded = Math.Min((long)Math.Round(analysisPosition) - Search, previousGrain >= 0 ? previousGrain + Hop : long.MaxValue) - 4;
            int drop = (int)Math.Max(0, oldestNeeded - inputBase);
            if (drop > 0 && drop <= inputCount)
            {
                Array.Copy(input, drop, input, 0, inputCount - drop);
                inputCount -= drop; inputBase += drop;
            }
        }

        private void Resample(double alpha)
        {
            while (readPosition + 2 < stretchedCount)
            {
                int i = (int)readPosition;
                float t = (float)(readPosition - i);
                float y0 = i > 0 ? stretched[i - 1] : stretched[i];
                float y1 = stretched[i], y2 = stretched[i + 1], y3 = stretched[i + 2];
                float c1 = .5f * (y2 - y0), c2 = y0 - 2.5f * y1 + 2f * y2 - .5f * y3, c3 = .5f * (y3 - y0) + 1.5f * (y1 - y2);
                EnsureOutput(outputCount + 1);
                output[outputCount++] = ((c3 * t + c2) * t + c1) * t + y1;
                readPosition += alpha;
            }
            int drop = (int)readPosition - 1;
            if (drop > 0)
            {
                Array.Copy(stretched, drop, stretched, 0, stretchedCount - drop);
                stretchedCount -= drop; readPosition -= drop;
            }
        }

        private void EnsureInput(int needed) { if (needed > input.Length) Array.Resize(ref input, Math.Max(needed, input.Length * 2)); }
        private void EnsureStretched(int needed) { if (needed > stretched.Length) Array.Resize(ref stretched, Math.Max(needed, stretched.Length * 2)); }
        private void EnsureOutput(int needed) { if (needed > output.Length) Array.Resize(ref output, Math.Max(needed, output.Length * 2)); }

        private static float[] BuildHann()
        {
            var window = new float[Window];
            for (int n = 0; n < Window; n++) window[n] = .5f - .5f * (float)Math.Cos(2 * Math.PI * n / Window);
            return window;
        }
    }
}
