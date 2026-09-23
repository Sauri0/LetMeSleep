using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Compact stereo room reverb for one voice at 12 kHz: pre-delay, two input diffusers and a four-line
    /// feedback delay network (Hadamard mix, per-line HF damping). Delay lengths are fixed so decay and
    /// damping can change continuously without pitch artifacts; the decay (RT60) is re-evaluated per block.
    /// Output is diffuse (decorrelated L/R) and independent of the dry signal's pan.
    /// </summary>
    public sealed class VoiceRoomReverb
    {
        public const int SampleRate = 12000;
        private const int BlockSize = 16;
        private static readonly int[] LineDelays = { 331, 419, 509, 613 };   // 27.6 … 51.1 ms, mutually prime
        private static readonly int[] DiffuserDelays = { 71, 113 };           // 5.9 and 9.4 ms
        private const float DiffuserGain = .6f;
        private const int PreDelaySamples = 90;                                // 7.5 ms

        private readonly float[][] lines = new float[4][];
        private readonly int[] linePositions = new int[4];
        private readonly float[] damping = new float[4];
        private readonly float[] feedback = new float[4];
        private readonly float[][] diffusers = new float[2][];
        private readonly int[] diffuserPositions = new int[2];
        private readonly float[] preDelay = new float[PreDelaySamples];
        private int preDelayPosition, blockCounter;
        private float dampingCoefficient = .5f;
        private VoiceSmoother decay, dampingHz;
        private float energy;

        public VoiceRoomReverb()
        {
            for (int i = 0; i < lines.Length; i++) lines[i] = new float[LineDelays[i]];
            for (int i = 0; i < diffusers.Length; i++) diffusers[i] = new float[DiffuserDelays[i]];
            decay.Configure(.25f, SampleRate / (float)BlockSize);
            dampingHz.Configure(.25f, SampleRate / (float)BlockSize);
            decay.Reset(.4f);
            dampingHz.Reset(3500f);
            UpdateCoefficients();
        }

        /// <summary>Decay time to −60 dB in seconds (clamped 0.1–3 s) and HF damping corner in Hz.</summary>
        public void SetTargets(float rt60Seconds, float dampingCornerHz)
        {
            decay.Target = VoiceDsp.Clamp(VoiceDsp.Finite(rt60Seconds), .1f, 3f);
            dampingHz.Target = VoiceDsp.Clamp(VoiceDsp.Finite(dampingCornerHz), 800f, 5500f);
        }

        /// <summary>Jumps straight to the targets (first use of a new voice, no glide).</summary>
        public void SnapToTargets()
        {
            decay.Reset(decay.Target);
            dampingHz.Reset(dampingHz.Target);
            UpdateCoefficients();
        }

        /// <summary>True while the tail still carries audible energy (about −90 dBFS and up).</summary>
        public bool IsRinging => energy > 1e-9f;

        public void Reset()
        {
            for (int i = 0; i < lines.Length; i++) { Array.Clear(lines[i], 0, lines[i].Length); linePositions[i] = 0; damping[i] = 0; }
            for (int i = 0; i < diffusers.Length; i++) { Array.Clear(diffusers[i], 0, diffusers[i].Length); diffuserPositions[i] = 0; }
            Array.Clear(preDelay, 0, preDelay.Length);
            preDelayPosition = 0; energy = 0;
        }

        public void Process(float input, out float left, out float right)
        {
            if (++blockCounter >= BlockSize)
            {
                blockCounter = 0;
                decay.Next(); dampingHz.Next();
                UpdateCoefficients();
            }

            float delayed = preDelay[preDelayPosition];
            preDelay[preDelayPosition] = VoiceDsp.Finite(input);
            if (++preDelayPosition == PreDelaySamples) preDelayPosition = 0;

            // Schroeder allpass diffusers raise echo density before the network.
            float x = delayed;
            for (int d = 0; d < diffusers.Length; d++)
            {
                float[] buffer = diffusers[d];
                int position = diffuserPositions[d];
                float stored = buffer[position];
                float v = x + DiffuserGain * stored;
                buffer[position] = VoiceDsp.Flush(v);
                x = stored - DiffuserGain * v;
                if (++diffuserPositions[d] == buffer.Length) diffuserPositions[d] = 0;
            }

            float y0 = lines[0][linePositions[0]], y1 = lines[1][linePositions[1]];
            float y2 = lines[2][linePositions[2]], y3 = lines[3][linePositions[3]];

            // Per-line HF damping (one-pole low-pass) then decay gain.
            damping[0] += (y0 - damping[0]) * dampingCoefficient; float d0 = damping[0] * feedback[0];
            damping[1] += (y1 - damping[1]) * dampingCoefficient; float d1 = damping[1] * feedback[1];
            damping[2] += (y2 - damping[2]) * dampingCoefficient; float d2 = damping[2] * feedback[2];
            damping[3] += (y3 - damping[3]) * dampingCoefficient; float d3 = damping[3] * feedback[3];

            // Orthonormal Hadamard mix.
            float m0 = .5f * (d0 + d1 + d2 + d3);
            float m1 = .5f * (d0 - d1 + d2 - d3);
            float m2 = .5f * (d0 + d1 - d2 - d3);
            float m3 = .5f * (d0 - d1 - d2 + d3);

            float injected = x * .5f;
            Write(0, m0 + injected); Write(1, m1 + injected); Write(2, m2 - injected); Write(3, m3 - injected);

            left = .7071f * (y0 + y2);
            right = .7071f * (y1 - y3);
            energy = energy * .999f + (left * left + right * right) * .001f;
            if (energy < 1e-12f) energy = 0f;
        }

        private void Write(int line, float value)
        {
            float[] buffer = lines[line];
            buffer[linePositions[line]] = VoiceDsp.Flush(value);
            if (++linePositions[line] == buffer.Length) linePositions[line] = 0;
        }

        private void UpdateCoefficients()
        {
            float rt60 = Math.Max(.1f, decay.Value);
            for (int i = 0; i < feedback.Length; i++)
                feedback[i] = (float)Math.Pow(10.0, -3.0 * LineDelays[i] / (rt60 * SampleRate));
            dampingCoefficient = 1f - (float)Math.Exp(-2.0 * Math.PI * dampingHz.Value / SampleRate);
        }
    }
}
