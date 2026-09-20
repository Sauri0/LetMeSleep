using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;

namespace LetMeSleep.Bootstrap
{
    public static class AppearanceWireCodec
    {
        public const byte Version = 2;
        public const int MaximumPacketBytes = 128;

        public static bool TryEncode(CustomizationCatalogSnapshot snapshot, string roomCode,
            AppearanceSelection selection, out byte[] packet)
        {
            packet = null;
            if (snapshot == null || !snapshot.RuntimeReady || selection == null) return false;
            if (!snapshot.TryNormalize(selection, out var normalized, out _)) return false;

            try
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                writer.Write(Version);
                RoomWireCodec.WriteText(writer, roomCode, 16);
                writer.Write(snapshot.NetworkFingerprint);

                var slots = snapshot.Slots.OrderBy(item => item.WireSlotId).ToArray();
                if (slots.Length > CustomizationCatalogSnapshot.MaximumSelectedSlots) return false;
                writer.Write((byte)slots.Length);
                foreach (var slot in slots)
                {
                    string optionId = normalized.For(slot.Role).OptionFor(slot.SlotId);
                    if (!snapshot.TryEncode(slot.SlotId, optionId, out byte slotCode, out ushort optionCode))
                        return false;
                    writer.Write(slotCode);
                    writer.Write(optionCode);
                }

                if (stream.Length > MaximumPacketBytes) return false;
                packet = stream.ToArray();
                return true;
            }
            catch (EncoderFallbackException) { return false; }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        public static bool TryDecode(byte[] packet, string expectedRoomCode,
            CustomizationCatalogSnapshot snapshot, out AppearanceSelection selection) =>
            TryDecode(packet == null ? default : new ArraySegment<byte>(packet), expectedRoomCode, snapshot, out selection);

        public static bool TryDecode(ArraySegment<byte> packet, string expectedRoomCode,
            CustomizationCatalogSnapshot snapshot, out AppearanceSelection selection)
        {
            selection = null;
            if (snapshot == null || !snapshot.RuntimeReady || !ValidSegment(packet)
                || packet.Count > MaximumPacketBytes) return false;

            try
            {
                using var stream = new MemoryStream(packet.Array, packet.Offset, packet.Count, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadByte() != Version) return false;
                if (!string.Equals(RoomWireCodec.ReadText(reader, 16), expectedRoomCode, StringComparison.Ordinal)) return false;
                if (reader.ReadUInt64() != snapshot.NetworkFingerprint) return false;

                int count = reader.ReadByte();
                if (count > CustomizationCatalogSnapshot.MaximumSelectedSlots || count != snapshot.Slots.Count) return false;

                var seen = new HashSet<byte>();
                var human = new List<AppearanceSlotSelection>();
                var mosquito = new List<AppearanceSlotSelection>();
                for (int index = 0; index < count; index++)
                {
                    byte slotCode = reader.ReadByte();
                    ushort optionCode = reader.ReadUInt16();
                    if (!seen.Add(slotCode)
                        || !snapshot.TryDecode(slotCode, optionCode, out string slotId, out string optionId)
                        || !snapshot.TrySlot(slotId, out var slot)) return false;

                    var item = new AppearanceSlotSelection(slotId, optionId);
                    if (slot.Role == CustomizationRole.Human) human.Add(item);
                    else if (slot.Role == CustomizationRole.Mosquito) mosquito.Add(item);
                    else return false;
                }

                if (stream.Position != stream.Length) return false;
                var decoded = new AppearanceSelection(new AppearanceLoadout(human), new AppearanceLoadout(mosquito));
                if (!snapshot.TryNormalize(decoded, out var normalized, out _) || !decoded.CanonicalEquals(normalized)) return false;
                selection = normalized;
                return true;
            }
            catch (DecoderFallbackException) { return false; }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static bool ValidSegment(ArraySegment<byte> packet)
        {
            if (packet.Array == null || packet.Offset < 0 || packet.Count < 0) return false;
            return packet.Offset <= packet.Array.Length && packet.Count <= packet.Array.Length - packet.Offset;
        }
    }
}
