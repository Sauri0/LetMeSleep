using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Packet-loss / underrun concealment for 12 kHz voice (waveform substitution in the spirit of ITU-T G.711
    /// Appendix I). A gap continues the last pitch period (period from normalized autocorrelation, wrap made
    /// seamless with a quarter-period blend, first sample aligned with the last real one), holds full level for
    /// 10 ms and then fades to silence over 50 ms. Real audio returning after a gap crossfades in over 5 ms, so
    /// neither the loss nor the recovery produces a step.
    ///
    /// Two front ends share the algorithm: the frame API (<see cref="Accept"/>, <see cref="Conceal"/>) used by the
    /// main thread for packets the jitter buffer declared lost, and the sample API (<see cref="Observe"/>,
    /// <see cref="BeginLoss"/>, <see cref="NextSynthetic"/>) used by the playout engine on the audio thread when
    /// the network or a frame hitch starves the ring.
    /// </summary>
    public sealed class VoiceConcealer
    {
        public const int FrameSamples = 240;
        public const int RecoveryCrossfade = 60;   // 5 ms
        public const int FullLevelSamples = 120;   // 10 ms
        public const int DecaySamples = 600;       // 50 ms
        private const int HistoryLength = 512;
        private const int HistoryMask = HistoryLength - 1;
        private const int MinimumPeriod = 30;      // 400 Hz
        private const int MaximumPeriod = 200;     // 60 Hz
        private const int CorrelationLength = 120; // 10 ms
        private const int CorrectionLength = 24;   // 2 ms

        private readonly float[] history = new float[HistoryLength];
        private readonly float[] linear = new float[HistoryLength];
        private readonly float[] pitchBuffer = new float[MaximumPeriod];
        private int historyWrite, historyFilled, period, periodPosition, lostSamples, correctionRemaining;
        private float correction;
        private bool concealing;

        public bool Concealing => concealing;
        /// <summary>The synthetic continuation has faded to silence.</summary>
        public bool Exhausted => concealing && lostSamples >= FullLevelSamples + DecaySamples;
        public int TotalConcealedFrames { get; private set; }
        public int LastPeriod => period;

        public void Reset()
        {
            Array.Clear(history, 0, history.Length);
            historyWrite = 0; historyFilled = 0; period = 0; periodPosition = 0; lostSamples = 0; correctionRemaining = 0;
            concealing = false;
        }

        // ------------------------------------------------------------------ frame API (main thread)

        /// <summary>Returns the frame to play for a received frame (crossfaded from the concealment after a loss).</summary>
        public float[] Accept(float[] frame)
        {
            if (frame == null || frame.Length != FrameSamples) throw new ArgumentException("Voice frames must contain 240 samples.", nameof(frame));
            var output = new float[FrameSamples];
            for (int i = 0; i < FrameSamples; i++) output[i] = VoiceDsp.Finite(frame[i]);
            if (concealing)
            {
                for (int i = 0; i < RecoveryCrossfade; i++)
                {
                    float synthetic = NextSynthetic();
                    float w = VoiceDsp.Ramp((i + 1) / (float)(RecoveryCrossfade + 1));
                    output[i] = synthetic * (1f - w) + output[i] * w;
                }
                concealing = false;
            }
            lostSamples = 0;
            for (int i = 0; i < FrameSamples; i++) Observe(output[i]);
            return output;
        }

        /// <summary>Synthesizes one frame for a missing packet.</summary>
        public float[] Conceal()
        {
            var output = new float[FrameSamples];
            if (!concealing) BeginLoss();
            for (int i = 0; i < FrameSamples; i++) output[i] = NextSynthetic();
            TotalConcealedFrames++;
            return output;
        }

        // ------------------------------------------------------------------ sample API (audio thread)

        /// <summary>Records a real sample that was just played.</summary>
        public void Observe(float sample)
        {
            history[historyWrite] = sample;
            historyWrite = (historyWrite + 1) & HistoryMask;
            if (historyFilled < HistoryLength) historyFilled++;
        }

        /// <summary>Real audio is back: the caller crossfades from <see cref="NextSynthetic"/> into it.</summary>
        public void EndLoss() { concealing = false; lostSamples = 0; }

        public void BeginLoss()
        {
            concealing = true;
            lostSamples = 0;
            if (historyFilled < CorrelationLength + MinimumPeriod) { period = 0; return; }
            for (int i = 0; i < HistoryLength; i++) linear[i] = history[(historyWrite + i) & HistoryMask];
            int end = HistoryLength;
            period = EstimatePeriod();
            for (int i = 0; i < period; i++) pitchBuffer[i] = linear[end - period + i];
            // Seamless wrap: blend the tail of the period toward the samples that precede its start.
            int blend = Math.Max(4, period / 4);
            for (int i = 0; i < blend; i++)
            {
                float w = (i + 1) / (float)(blend + 1);
                int tail = period - blend + i;
                pitchBuffer[tail] = pitchBuffer[tail] * (1f - w) + linear[end - period - blend + i] * w;
            }
            periodPosition = 0;
            // Remove the step between the last real sample and the first synthetic one.
            float last = linear[end - 1], previous = linear[end - 2];
            correction = last + (last - previous) * .5f - pitchBuffer[0];
            correctionRemaining = CorrectionLength;
        }

        public float NextSynthetic()
        {
            if (period <= 0) { lostSamples++; return 0f; }
            float sample = pitchBuffer[periodPosition];
            if (++periodPosition >= period) periodPosition = 0;
            if (correctionRemaining > 0)
            {
                sample += correction * (correctionRemaining / (float)CorrectionLength);
                correctionRemaining--;
            }
            float gain = lostSamples < FullLevelSamples ? 1f : Math.Max(0f, 1f - (lostSamples - FullLevelSamples) / (float)DecaySamples);
            lostSamples++;
            return sample * VoiceDsp.Ramp(gain);
        }

        private int EstimatePeriod()
        {
            int end = HistoryLength;
            int start = end - CorrelationLength;
            double energy0 = 0;
            for (int i = start; i < end; i++) energy0 += linear[i] * linear[i];
            if (energy0 < 1e-9) return MaximumPeriod;
            int maxLag = Math.Min(MaximumPeriod, historyFilled - CorrelationLength - MaximumPeriod / 4);
            if (maxLag < MinimumPeriod) maxLag = MinimumPeriod;
            int best = maxLag;
            double bestScore = -1;
            for (int lag = MinimumPeriod; lag <= maxLag; lag++)
            {
                double score = Correlation(start, end, lag, energy0);
                if (score > bestScore) { bestScore = score; best = lag; }
            }
            // Prefer the shortest lag that is nearly as periodic (avoids octave-down loops).
            if (bestScore > .3)
            {
                for (int divisor = 4; divisor >= 2; divisor--)
                {
                    int candidate = best / divisor;
                    if (candidate < MinimumPeriod) continue;
                    if (Correlation(start, end, candidate, energy0) > bestScore * .92) { best = candidate; break; }
                }
            }
            return best;
        }

        private double Correlation(int start, int end, int lag, double energy0)
        {
            double cross = 0, energy = 0;
            for (int i = start; i < end; i++)
            {
                float delayed = linear[i - lag];
                cross += linear[i] * delayed;
                energy += delayed * delayed;
            }
            return energy < 1e-12 ? -1 : cross / Math.Sqrt(energy * energy0);
        }
    }
}
