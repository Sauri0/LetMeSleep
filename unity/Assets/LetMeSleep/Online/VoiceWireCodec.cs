using System;

namespace LetMeSleep.Online
{
    public enum VoicePacketKind : byte { Audio = 1, End = 2 }

    public sealed class VoicePacket
    {
        public VoicePacketKind Kind { get; }
        public ulong SessionEpoch { get; }
        public ulong RoundId { get; }
        public uint ActorId { get; }
        public uint StreamId { get; }
        public uint Sequence { get; }
        public byte[] Payload { get; }

        public VoicePacket(VoicePacketKind kind, ulong sessionEpoch, ulong roundId, uint actorId,
            uint streamId, uint sequence, byte[] payload)
        {
            Kind = kind; SessionEpoch = sessionEpoch; RoundId = roundId; ActorId = actorId;
            StreamId = streamId; Sequence = sequence; Payload = payload ?? Array.Empty<byte>();
        }
    }

    public static class VoiceWireCodec
    {
        public const int HeaderBytes = 39;
        public const int AudioPacketBytes = HeaderBytes + VoiceProtocol.MaximumCodecBytes;
        private const byte Version = 1;

        public static byte[] Encode(VoicePacket packet)
        {
            if (!IsValid(packet)) throw new ArgumentException("Invalid voice packet.", nameof(packet));
            var bytes = new byte[HeaderBytes + packet.Payload.Length];
            bytes[0] = 0x4c; bytes[1] = 0x56; bytes[2] = Version;
            bytes[3] = (byte)packet.Kind; bytes[4] = 0;
            WriteUInt64(bytes, 5, packet.SessionEpoch);
            WriteUInt64(bytes, 13, packet.RoundId);
            WriteUInt32(bytes, 21, packet.ActorId);
            WriteUInt32(bytes, 25, packet.StreamId);
            WriteUInt32(bytes, 29, packet.Sequence);
            ushort rate = packet.Kind == VoicePacketKind.Audio ? (ushort)VoiceProtocol.SampleRate : (ushort)0;
            ushort count = packet.Kind == VoicePacketKind.Audio ? (ushort)VoiceProtocol.FrameSamples : (ushort)0;
            WriteUInt16(bytes, 33, rate); WriteUInt16(bytes, 35, count); WriteUInt16(bytes, 37, (ushort)packet.Payload.Length);
            if (packet.Payload.Length > 0) Buffer.BlockCopy(packet.Payload, 0, bytes, HeaderBytes, packet.Payload.Length);
            return bytes;
        }

        public static bool TryDecode(ArraySegment<byte> data, out VoicePacket packet)
        {
            packet = null;
            if (data.Array == null || data.Count < HeaderBytes || data.Count > AudioPacketBytes) return false;
            byte[] bytes = data.Array; int start = data.Offset;
            if (bytes[start] != 0x4c || bytes[start + 1] != 0x56 || bytes[start + 2] != Version || bytes[start + 4] != 0) return false;
            var kind = (VoicePacketKind)bytes[start + 3];
            ulong epoch = ReadUInt64(bytes, start + 5), round = ReadUInt64(bytes, start + 13);
            uint actor = ReadUInt32(bytes, start + 21), stream = ReadUInt32(bytes, start + 25), sequence = ReadUInt32(bytes, start + 29);
            int rate = ReadUInt16(bytes, start + 33), count = ReadUInt16(bytes, start + 35), length = ReadUInt16(bytes, start + 37);
            if (epoch == 0 || round == 0 || actor == 0 || stream == 0 || sequence == 0 || data.Count != HeaderBytes + length) return false;
            if (kind == VoicePacketKind.Audio)
            {
                if (rate != VoiceProtocol.SampleRate || count != VoiceProtocol.FrameSamples || length != VoiceProtocol.MaximumCodecBytes) return false;
            }
            else if (kind == VoicePacketKind.End)
            {
                if (rate != 0 || count != 0 || length != 0) return false;
            }
            else return false;
            var payload = new byte[length];
            if (length > 0) Buffer.BlockCopy(bytes, start + HeaderBytes, payload, 0, length);
            packet = new VoicePacket(kind, epoch, round, actor, stream, sequence, payload);
            return true;
        }

        private static bool IsValid(VoicePacket packet)
        {
            if (packet == null || packet.SessionEpoch == 0 || packet.RoundId == 0 || packet.ActorId == 0 || packet.StreamId == 0 || packet.Sequence == 0) return false;
            return packet.Kind == VoicePacketKind.Audio ? packet.Payload.Length == VoiceProtocol.MaximumCodecBytes :
                packet.Kind == VoicePacketKind.End && packet.Payload.Length == 0;
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value) { bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8); }
        private static int ReadUInt16(byte[] bytes, int offset) => bytes[offset] | bytes[offset + 1] << 8;
        private static void WriteUInt32(byte[] bytes, int offset, uint value) { for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8)); }
        private static uint ReadUInt32(byte[] bytes, int offset) { uint value = 0; for (int i = 0; i < 4; i++) value |= (uint)bytes[offset + i] << (i * 8); return value; }
        private static void WriteUInt64(byte[] bytes, int offset, ulong value) { for (int i = 0; i < 8; i++) bytes[offset + i] = (byte)(value >> (i * 8)); }
        private static ulong ReadUInt64(byte[] bytes, int offset) { ulong value = 0; for (int i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << (i * 8); return value; }
    }
}
