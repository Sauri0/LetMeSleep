using System;

namespace LetMeSleep.Online
{
    /// <summary>Independent 20 ms IMA ADPCM frames. No state crosses packet boundaries.</summary>
    public sealed class VoiceImaAdpcmCodec
    {
        private const byte Version = 1;
        private const int HeaderBytes = 6;
        private static readonly int[] IndexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        private static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658,
            724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
            3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        public byte[] Encode(float[] samples)
        {
            if (samples == null || samples.Length != VoiceProtocol.FrameSamples)
                throw new ArgumentException("Voice frames must contain exactly 240 samples.", nameof(samples));
            var pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float value = samples[i];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Voice samples must be finite.", nameof(samples));
                value = Math.Max(-1f, Math.Min(1f, value));
                pcm[i] = (short)Math.Round(value * 32767f);
            }

            int predictor = pcm[0];
            int index = ChooseInitialIndex(pcm);
            var output = new byte[VoiceProtocol.MaximumCodecBytes];
            output[0] = Version;
            output[1] = (byte)VoiceProtocol.FrameSamples;
            output[2] = (byte)(VoiceProtocol.FrameSamples >> 8);
            output[3] = (byte)predictor;
            output[4] = (byte)(predictor >> 8);
            output[5] = (byte)index;
            for (int sample = 1; sample < pcm.Length; sample++)
            {
                byte nibble = EncodeNibble(pcm[sample], ref predictor, ref index);
                int packed = sample - 1;
                int offset = HeaderBytes + packed / 2;
                if ((packed & 1) == 0) output[offset] = nibble;
                else output[offset] |= (byte)(nibble << 4);
            }
            return output;
        }

        public bool TryDecode(ArraySegment<byte> encoded, out float[] samples)
        {
            samples = null;
            if (encoded.Array == null || encoded.Count != VoiceProtocol.MaximumCodecBytes) return false;
            byte[] bytes = encoded.Array;
            int start = encoded.Offset;
            if (bytes[start] != Version) return false;
            int count = bytes[start + 1] | bytes[start + 2] << 8;
            int predictor = (short)(bytes[start + 3] | bytes[start + 4] << 8);
            int index = bytes[start + 5];
            if (count != VoiceProtocol.FrameSamples || index > 88) return false;
            samples = new float[count];
            samples[0] = predictor / 32768f;
            for (int sample = 1; sample < count; sample++)
            {
                int packed = sample - 1;
                byte value = bytes[start + HeaderBytes + packed / 2];
                int nibble = (packed & 1) == 0 ? value & 15 : value >> 4;
                DecodeNibble(nibble, ref predictor, ref index);
                samples[sample] = predictor / 32768f;
            }
            return true;
        }

        private static int ChooseInitialIndex(short[] pcm)
        {
            long total = 0;
            for (int i = 1; i < pcm.Length; i++) total += Math.Abs((int)pcm[i] - pcm[i - 1]);
            int target = Math.Max(7, (int)(total / Math.Max(1, pcm.Length - 1)) / 2);
            int index = 0;
            while (index < 88 && StepTable[index] < target) index++;
            return index;
        }

        private static byte EncodeNibble(int sample, ref int predictor, ref int index)
        {
            int step = StepTable[index];
            int difference = sample - predictor;
            int nibble = 0;
            if (difference < 0) { nibble = 8; difference = -difference; }
            int delta = step >> 3;
            if (difference >= step) { nibble |= 4; difference -= step; delta += step; }
            if (difference >= step >> 1) { nibble |= 2; difference -= step >> 1; delta += step >> 1; }
            if (difference >= step >> 2) { nibble |= 1; delta += step >> 2; }
            predictor += (nibble & 8) != 0 ? -delta : delta;
            predictor = Math.Max(short.MinValue, Math.Min(short.MaxValue, predictor));
            index = Math.Max(0, Math.Min(88, index + IndexTable[nibble]));
            return (byte)nibble;
        }

        private static void DecodeNibble(int nibble, ref int predictor, ref int index)
        {
            int step = StepTable[index];
            int delta = step >> 3;
            if ((nibble & 4) != 0) delta += step;
            if ((nibble & 2) != 0) delta += step >> 1;
            if ((nibble & 1) != 0) delta += step >> 2;
            predictor += (nibble & 8) != 0 ? -delta : delta;
            predictor = Math.Max(short.MinValue, Math.Min(short.MaxValue, predictor));
            index = Math.Max(0, Math.Min(88, index + IndexTable[nibble]));
        }
    }
}
