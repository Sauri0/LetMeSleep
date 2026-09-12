using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LetMeSleep.Core
{
    public static class RoomWireCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static byte[] Encode(RoomView view)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write((byte)1); writer.Write(view.Revision); writer.Write(view.Round); WriteText(writer, view.OwnerId, 128);
            writer.Write((byte)view.Phase); writer.Write((byte)(view.Rules.HumanCount ?? 0));
            writer.Write(view.Rules.RoundSeconds); writer.Write(view.Rules.BloodQuota); WriteText(writer, view.Rules.MapId, 64);
            writer.Write((byte)view.Members.Count);
            foreach (var member in view.Members)
            {
                WriteText(writer, member.Id, 128); WriteText(writer, member.Name, 96);
                writer.Write(member.Ready); writer.Write((byte)member.Role);
            }
            return stream.ToArray();
        }

        public static bool TryDecode(byte[] packet, string expectedOwner, out RoomView view)
        {
            view = null;
            if (packet == null || packet.Length < 20 || packet.Length > 8192) return false;
            try
            {
                using var stream = new MemoryStream(packet, false); using var reader = new BinaryReader(stream, Utf8);
                if (reader.ReadByte() != 1) return false;
                long revision = reader.ReadInt64(); int round = reader.ReadInt32(); string owner = ReadText(reader, 128);
                var phase = (RoomPhase)reader.ReadByte(); int humanCount = reader.ReadByte();
                int seconds = reader.ReadInt32(); float quota = reader.ReadSingle(); string map = ReadText(reader, 64);
                var rules = new RoomRules(humanCount == 0 ? (int?)null : humanCount, seconds, quota, map);
                if (owner != expectedOwner || revision < 0 || round < 0 || phase > RoomPhase.Closed || !rules.IsValid) return false;
                int count = reader.ReadByte(); if (count > RoomRules.Capacity || (count == 0 && phase != RoomPhase.Closed)) return false;
                var members = new MemberView[count]; var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    string id = ReadText(reader, 128), name = ReadText(reader, 96);
                    byte ready = reader.ReadByte(); var role = (PlayerRole)reader.ReadByte();
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || name.Length > 24 || ready > 1 || role > PlayerRole.Mosquito || !ids.Add(id)) return false;
                    foreach (char value in id + name) if (char.IsControl(value)) return false;
                    members[i] = new MemberView(id, name, ready == 1, role);
                }
                if (stream.Position != stream.Length || (phase != RoomPhase.Closed && !ids.Contains(owner))) return false;
                view = new RoomView(revision, round, owner, phase, rules, members); return true;
            }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (DecoderFallbackException) { return false; }
        }
        public static void WriteText(BinaryWriter writer, string text, int maximumBytes)
        {
            byte[] data = Utf8.GetBytes(text ?? "");
            if (data.Length > maximumBytes) throw new InvalidDataException("Text exceeds network limit.");
            writer.Write((ushort)data.Length); writer.Write(data);
        }
        public static string ReadText(BinaryReader reader, int maximumBytes)
        {
            int length = reader.ReadUInt16();
            if (length > maximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid network text length.");
            return Utf8.GetString(reader.ReadBytes(length));
        }
    }
}
