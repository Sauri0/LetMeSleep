using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>Small bounded messages over EOS MTU-sized packets. No allocation from unvalidated lengths.</summary>
    public sealed class MessageFraming
    {
        public const int HeaderBytes = 10, ChunkBytes = EosPeerTransport.MaximumPacketBytes - HeaderBytes, MaximumMessageBytes = 16384;
        private sealed class Pending
        {
            internal uint Id;
            internal byte Kind;
            internal byte[][] Parts;
            internal int Total, Received;
            internal double Expires;
        }
        private readonly Dictionary<string, List<Pending>> pending = new Dictionary<string, List<Pending>>(StringComparer.Ordinal);
        private uint nextId;
        public event Action<string, byte, byte[]> MessageReceived;

        public IEnumerable<byte[]> Encode(byte kind, byte[] payload)
        {
            if (payload == null || payload.Length < 1 || payload.Length > MaximumMessageBytes) throw new ArgumentOutOfRangeException(nameof(payload));
            uint id = ++nextId;
            int count = (payload.Length + ChunkBytes - 1) / ChunkBytes;
            for (int i = 0; i < count; i++)
            {
                int offset = i * ChunkBytes, size = Math.Min(ChunkBytes, payload.Length - offset);
                var packet = new byte[HeaderBytes + size];
                packet[0] = 0x4c; packet[1] = kind;
                for (int b = 0; b < 4; b++) packet[2 + b] = (byte)(id >> (b * 8));
                packet[6] = (byte)i; packet[7] = (byte)count;
                packet[8] = (byte)payload.Length; packet[9] = (byte)(payload.Length >> 8);
                Buffer.BlockCopy(payload, offset, packet, HeaderBytes, size);
                yield return packet;
            }
        }

        // Only pass authenticated, current room members to this method.
        public bool Accept(string member, ArraySegment<byte> packet, double now)
        {
            if (string.IsNullOrEmpty(member) || packet.Array == null || packet.Count <= HeaderBytes || packet.Count > EosPeerTransport.MaximumPacketBytes) return false;
            byte[] bytes = packet.Array; int start = packet.Offset;
            if (bytes[start] != 0x4c) return false;
            byte kind = bytes[start + 1];
            uint id = 0;
            for (int b = 0; b < 4; b++) id |= (uint)bytes[start + 2 + b] << (8 * b);
            int index = bytes[start + 6], count = bytes[start + 7], total = bytes[start + 8] | bytes[start + 9] << 8;
            if (total < 1 || total > MaximumMessageBytes || count != (total + ChunkBytes - 1) / ChunkBytes || index >= count) return false;
            int size = Math.Min(ChunkBytes, total - index * ChunkBytes);
            if (packet.Count != HeaderBytes + size) return false;
            if (count == 1)
            {
                var data = new byte[total]; Buffer.BlockCopy(bytes, start + HeaderBytes, data, 0, total);
                MessageReceived?.Invoke(member, kind, data); return true;
            }
            if (!pending.TryGetValue(member, out var slots))
            {
                var expired = new List<string>();
                foreach (var entry in pending)
                {
                    entry.Value.RemoveAll(item => item.Expires <= now);
                    if (entry.Value.Count == 0) expired.Add(entry.Key);
                }
                foreach (var key in expired) pending.Remove(key);
                if (pending.Count >= 16) return false;
                slots = new List<Pending>(2); pending.Add(member, slots);
            }
            slots.RemoveAll(item => item.Expires <= now);
            var assembly = slots.Find(item => item.Id == id);
            if (assembly == null)
            {
                if (slots.Count == 2) slots.RemoveAt(0);
                assembly = new Pending { Id = id, Kind = kind, Total = total, Parts = new byte[count][], Expires = now + 2 };
                slots.Add(assembly);
            }
            if (assembly.Kind != kind || assembly.Total != total || assembly.Parts.Length != count) return false;
            if (assembly.Parts[index] != null) return false;
            var part = new byte[size]; Buffer.BlockCopy(bytes, start + HeaderBytes, part, 0, size);
            assembly.Parts[index] = part; assembly.Received++;
            if (assembly.Received == count)
            {
                var data = new byte[total];
                for (int i = 0; i < count; i++) Buffer.BlockCopy(assembly.Parts[i], 0, data, i * ChunkBytes, assembly.Parts[i].Length);
                slots.Remove(assembly);
                if (slots.Count == 0) pending.Remove(member);
                MessageReceived?.Invoke(member, kind, data);
            }
            return true;
        }
        public void Forget(string member) => pending.Remove(member);
        public void Clear() => pending.Clear();
    }
}
