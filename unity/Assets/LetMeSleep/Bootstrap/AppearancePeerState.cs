using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;

namespace LetMeSleep.Bootstrap
{
    // Transport supplies the authenticated peer ID; room membership is checked again here.
    public sealed class AppearancePeerState
    {
        private sealed class Entry
        {
            public BasicCustomizationDraft Legacy;
            public AppearanceSelection Modular;
            public ulong Fingerprint;
            public double AcceptedAt;
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Func<BasicCustomizationDraft, bool> validLegacy;
        private string currentRoomCode;

        public AppearancePeerState(Func<BasicCustomizationDraft, bool> validLegacy)
            => this.validLegacy = validLegacy ?? throw new ArgumentNullException(nameof(validLegacy));

        public void Clear()
        {
            entries.Clear();
            currentRoomCode = null;
        }

        public void Synchronize(RoomView room, string roomCode)
        {
            if (room == null || room.Phase == RoomPhase.Closed || string.IsNullOrEmpty(roomCode))
            { Clear(); return; }
            if (!string.Equals(currentRoomCode, roomCode, StringComparison.Ordinal))
            { entries.Clear(); currentRoomCode = roomCode; }
            var members = new HashSet<string>(room.Members.Select(member => member.Id), StringComparer.Ordinal);
            foreach (string peer in entries.Keys.Where(peer => !members.Contains(peer)).ToArray()) entries.Remove(peer);
        }

        public bool TryReceive(string peer, byte channel, ArraySegment<byte> packet, double now,
            RoomView room, string roomCode, CustomizationCatalogSnapshot catalog,
            Func<AppearanceSelection, bool> canApplyModular)
        {
            Synchronize(room, roomCode);
            if (channel != 2 || currentRoomCode == null || string.IsNullOrEmpty(peer)
                || !room.Members.Any(member => member.Id == peer && member.Connected)
                || packet.Array == null || packet.Count < 1 || packet.Count > AppearanceWireCodec.MaximumPacketBytes
                || double.IsNaN(now) || double.IsInfinity(now) || now < 0) return false;
            entries.TryGetValue(peer, out var previous);
            if (previous != null && now - previous.AcceptedAt < .25) return false;

            byte version = packet.Array[packet.Offset];
            if (version == AppearanceWireCodec.Version)
            {
                if (catalog == null || canApplyModular == null
                    || !AppearanceWireCodec.TryDecode(packet, roomCode, catalog, out var selection)
                    || !canApplyModular(selection)) return false;
                entries[peer] = new Entry { Modular = selection.Copy(), Fingerprint = catalog.NetworkFingerprint, AcceptedAt = now };
                return true;
            }
            // A delayed legacy message must not replace an already accepted modular appearance.
            if (version != 1 || previous?.Modular != null || !TryDecodeLegacy(packet, roomCode, out var legacy)
                || !validLegacy(legacy)) return false;
            entries[peer] = new Entry { Legacy = legacy.Copy(), AcceptedAt = now };
            return true;
        }

        public bool TryGetLegacy(string peer, out BasicCustomizationDraft appearance)
        {
            appearance = null;
            if (peer == null || !entries.TryGetValue(peer, out var value) || value.Legacy == null) return false;
            appearance = value.Legacy.Copy();
            return true;
        }

        public bool TryGetModular(string peer, ulong fingerprint, out AppearanceSelection appearance)
        {
            appearance = null;
            if (peer == null || !entries.TryGetValue(peer, out var value) || value.Modular == null
                || value.Fingerprint != fingerprint) return false;
            appearance = value.Modular.Copy();
            return true;
        }

        public static bool TryEncodeLegacy(string roomCode, BasicCustomizationDraft appearance, out byte[] packet)
        {
            packet = null;
            if (string.IsNullOrEmpty(roomCode) || appearance == null) return false;
            try
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                writer.Write((byte)1);
                RoomWireCodec.WriteText(writer, roomCode, 16);
                RoomWireCodec.WriteText(writer, appearance.SkinColorId, 16);
                RoomWireCodec.WriteText(writer, appearance.PajamaColorId, 16);
                RoomWireCodec.WriteText(writer, appearance.MosquitoColorId, 16);
                if (stream.Length > AppearanceWireCodec.MaximumPacketBytes) return false;
                packet = stream.ToArray();
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
        }

        private static bool TryDecodeLegacy(ArraySegment<byte> packet, string roomCode, out BasicCustomizationDraft appearance)
        {
            appearance = null;
            try
            {
                using var stream = new MemoryStream(packet.Array, packet.Offset, packet.Count, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadByte() != 1 || !string.Equals(RoomWireCodec.ReadText(reader, 16), roomCode, StringComparison.Ordinal)) return false;
                var value = new BasicCustomizationDraft(AlfaRole.Human, RoomWireCodec.ReadText(reader, 16),
                    RoomWireCodec.ReadText(reader, 16), RoomWireCodec.ReadText(reader, 16));
                if (stream.Position != stream.Length) return false;
                appearance = value;
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
        }
    }
}
