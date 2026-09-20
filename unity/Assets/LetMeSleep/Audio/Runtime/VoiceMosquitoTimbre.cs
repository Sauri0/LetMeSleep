using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Streaming dual-delay pitch shifter. It emits exactly one sample for every input sample,
    /// so mosquito pitch changes without changing speech duration or AudioSource playback speed.
    /// </summary>
    public sealed class VoiceMosquitoTimbre
    {
        private const int BufferSize = 1024;
        private const float MinimumDelay = 96f;
        private const float DelaySweep = 384f;
        private readonly float[] delay = new float[BufferSize];
        private int writePosition;
        private double phase = 0.5;
        private bool initialized;

        public float[] Process(float[] input, float semitones = 4f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (float.IsNaN(semitones) || float.IsInfinity(semitones) || semitones < 0 || semitones > 7)
                throw new ArgumentOutOfRangeException(nameof(semitones));
            var output = new float[input.Length];
            if (input.Length == 0) return output;
            if (!initialized)
            {
                float initial = FiniteClamp(input[0]);
                for (int i = 0; i < delay.Length; i++) delay[i] = initial;
                initialized = true;
            }
            double factor = Math.Pow(2.0, semitones / 12.0);
            double phaseStep = (factor - 1.0) / DelaySweep;
            for (int i = 0; i < input.Length; i++)
            {
                delay[writePosition] = FiniteClamp(input[i]);
                double secondPhase = phase + 0.5;
                if (secondPhase >= 1) secondPhase -= 1;
                double firstWeight = 0.5 - 0.5 * Math.Cos(2 * Math.PI * phase);
                double shifted = ReadHead(phase) * firstWeight + ReadHead(secondPhase) * (1.0 - firstWeight);
                output[i] = (float)Math.Max(-1.0, Math.Min(1.0, shifted));
                writePosition = (writePosition + 1) & (BufferSize - 1);
                phase += phaseStep;
                phase -= Math.Floor(phase);
            }
            return output;
        }

        public void Reset()
        {
            Array.Clear(delay, 0, delay.Length);
            writePosition = 0; phase = 0.5; initialized = false;
        }

        private double ReadHead(double headPhase)
        {
            double samplesBack = MinimumDelay + (1.0 - headPhase) * DelaySweep;
            double position = writePosition - samplesBack;
            while (position < 0) position += BufferSize;
            int left = (int)position & (BufferSize - 1);
            int right = (left + 1) & (BufferSize - 1);
            double fraction = position - Math.Floor(position);
            return delay[left] + (delay[right] - delay[left]) * fraction;
        }

        private static float FiniteClamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
            return Math.Max(-1f, Math.Min(1f, value));
        }
    }
}
