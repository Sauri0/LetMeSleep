using System;
using System.IO;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class AppearanceWireCodecTests
    {
        private const string Room = "SLEEP-24";

        [Test]
        public void RoundTripCarriesAllTwentyFourSlotsUnderPacketLimitInCanonicalOrder()
        {
            var snapshot = Snapshot();
            var source = snapshot.DefaultSelection();
            source.Human.Selections = source.Human.Selections.Reverse().ToArray();

            Assert.That(AppearanceWireCodec.TryEncode(snapshot, Room, source, out var packet), Is.True);
            Assert.That(packet.Length, Is.LessThanOrEqualTo(AppearanceWireCodec.MaximumPacketBytes));
            Assert.That(ReadSlotCodes(packet), Is.EqualTo(Enumerable.Range(1, 24).Select(value => (byte)value)));
            Assert.That(AppearanceWireCodec.TryDecode(packet, Room, snapshot, out var decoded), Is.True);
            Assert.That(decoded.CanonicalEquals(snapshot.DefaultSelection()), Is.True);
            Assert.That(decoded.Human.Selections.Length, Is.EqualTo(23));
            Assert.That(decoded.Mosquito.Selections.Length, Is.EqualTo(1));
        }

        [Test]
        public void DecodeAcceptsAValidNonZeroArraySegment()
        {
            var snapshot = Snapshot();
            Assert.That(AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet), Is.True);
            var envelope = Enumerable.Repeat((byte)0xCC, packet.Length + 11).ToArray();
            Array.Copy(packet, 0, envelope, 7, packet.Length);

            Assert.That(AppearanceWireCodec.TryDecode(new ArraySegment<byte>(envelope, 7, packet.Length), Room, snapshot, out var decoded), Is.True);
            Assert.That(decoded.CanonicalEquals(snapshot.DefaultSelection()), Is.True);
            Assert.That(AppearanceWireCodec.TryDecode(default(ArraySegment<byte>), Room, snapshot, out var absent), Is.False);
            Assert.That(absent, Is.Null);
        }

        [Test]
        public void TruncatedAndTrailingPacketsAreRejectedWithoutAnOutput()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);
            for (int length = 0; length < packet.Length; length++)
            {
                var truncated = packet.Take(length).ToArray();
                Assert.That(AppearanceWireCodec.TryDecode(truncated, Room, snapshot, out var decoded), Is.False, "length " + length);
                Assert.That(decoded, Is.Null, "length " + length);
            }

            Assert.That(AppearanceWireCodec.TryDecode(packet.Concat(new byte[] { 0 }).ToArray(), Room, snapshot, out var trailing), Is.False);
            Assert.That(trailing, Is.Null);
        }

        [Test]
        public void VersionRoomAndFingerprintMustMatchExactly()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);

            AssertRejected(Change(packet, 0, 1), Room, snapshot);
            AssertRejected(packet, Room.ToLowerInvariant(), snapshot);
            int fingerprintOffset = 3 + Room.Length;
            AssertRejected(Change(packet, fingerprintOffset, (byte)(packet[fingerprintOffset] ^ 0x80)), Room, snapshot);
        }

        [Test]
        public void UnknownAndDuplicateWireCodesAreRejected()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);
            int pairs = PairOffset(packet);

            AssertRejected(Change(packet, pairs, 25), Room, snapshot);
            AssertRejected(ChangeUInt16(packet, pairs + 1, 60000), Room, snapshot);
            AssertRejected(Change(packet, pairs + 3, packet[pairs]), Room, snapshot);
        }

        [Test]
        public void CountMustDescribeTheCompleteCatalog()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);
            int countOffset = PairOffset(packet) - 1;

            AssertRejected(Change(packet, countOffset, 23), Room, snapshot);
            AssertRejected(Change(packet, countOffset, 25), Room, snapshot);
        }

        [Test]
        public void RequiredNoneAndIncompatibleSelectionsAreRejected()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);
            int pairs = PairOffset(packet);

            AssertRejected(ChangeUInt16(packet, pairs + (2 * 3) + 1, 0), Room, snapshot);
            AssertRejected(ChangeUInt16(packet, pairs + (2 * 3) + 1, 1), Room, snapshot);
        }

        [Test]
        public void CatalogRolesControlDecodedLoadoutsAndCrossRoleInputCannotEncode()
        {
            var snapshot = Snapshot();
            AppearanceWireCodec.TryEncode(snapshot, Room, snapshot.DefaultSelection(), out var packet);
            Assert.That(AppearanceWireCodec.TryDecode(packet, Room, snapshot, out var decoded), Is.True);
            Assert.That(decoded.Human.OptionFor("human.base"), Is.EqualTo("human-a"));
            Assert.That(decoded.Mosquito.OptionFor("mosquito.base"), Is.EqualTo("mosquito-a"));
            Assert.That(decoded.Human.OptionFor("mosquito.base"), Is.Null);

            var crossRole = snapshot.DefaultSelection();
            crossRole.Human.Selections = crossRole.Human.Selections
                .Concat(new[] { new AppearanceSlotSelection("mosquito.base", "mosquito-a") }).ToArray();
            Assert.That(AppearanceWireCodec.TryEncode(snapshot, Room, crossRole, out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
        }

        [Test]
        public void EncodeNormalizesDefaultsButRejectsInvalidOrInactiveInputs()
        {
            var snapshot = Snapshot();
            var partial = new AppearanceSelection(new AppearanceLoadout(), new AppearanceLoadout());
            Assert.That(AppearanceWireCodec.TryEncode(snapshot, Room, partial, out var packet), Is.True);
            Assert.That(AppearanceWireCodec.TryDecode(packet, Room, snapshot, out var decoded), Is.True);
            Assert.That(decoded.CanonicalEquals(snapshot.DefaultSelection()), Is.True);

            Assert.That(AppearanceWireCodec.TryEncode(snapshot, new string('x', 17), partial, out var longRoom), Is.False);
            Assert.That(longRoom, Is.Null);
            Assert.That(AppearanceWireCodec.TryEncode(null, Room, partial, out var noCatalog), Is.False);
            Assert.That(noCatalog, Is.Null);

            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.empty", 1,
                Array.Empty<CustomizationSlotRecord>(), Array.Empty<CustomizationOptionRecord>(),
                out var inactive, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(inactive.RuntimeReady, Is.False);
            Assert.That(AppearanceWireCodec.TryEncode(inactive, Room, partial, out var inactivePacket), Is.False);
            Assert.That(inactivePacket, Is.Null);
            Assert.That(AppearanceWireCodec.TryDecode(packet, Room, inactive, out var inactiveSelection), Is.False);
            Assert.That(inactiveSelection, Is.Null);
        }

        [Test]
        public void FailedDecodeLeavesCallersPreviousSelectionUntouched()
        {
            var snapshot = Snapshot();
            var previous = snapshot.DefaultSelection();
            var before = previous.Copy();
            AppearanceWireCodec.TryEncode(snapshot, Room, previous, out var packet);

            Assert.That(AppearanceWireCodec.TryDecode(Change(packet, 0, 99), Room, snapshot, out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(previous.CanonicalEquals(before), Is.True);
        }

        private static void AssertRejected(byte[] packet, string room, CustomizationCatalogSnapshot snapshot)
        {
            Assert.That(AppearanceWireCodec.TryDecode(packet, room, snapshot, out var decoded), Is.False);
            Assert.That(decoded, Is.Null);
        }

        private static byte[] ReadSlotCodes(byte[] packet)
        {
            int start = PairOffset(packet);
            int count = packet[start - 1];
            return Enumerable.Range(0, count).Select(index => packet[start + index * 3]).ToArray();
        }

        private static int PairOffset(byte[] packet)
        {
            int roomLength = packet[1] | packet[2] << 8;
            return 1 + 2 + roomLength + sizeof(ulong) + 1;
        }

        private static byte[] Change(byte[] source, int index, byte value)
        {
            var result = (byte[])source.Clone();
            result[index] = value;
            return result;
        }

        private static byte[] ChangeUInt16(byte[] source, int index, ushort value)
        {
            var result = (byte[])source.Clone();
            result[index] = (byte)value;
            result[index + 1] = (byte)(value >> 8);
            return result;
        }

        private static CustomizationCatalogSnapshot Snapshot()
        {
            var slots = new CustomizationSlotRecord[24];
            slots[0] = Slot(CustomizationRole.Human, "human.base", 1, true, false, true, "human-a");
            slots[1] = Slot(CustomizationRole.Mosquito, "mosquito.base", 2, true, false, true, "mosquito-a");
            slots[2] = Slot(CustomizationRole.Human, "human.compat", 3, true, true, false, "compat-default");
            for (int index = 3; index < slots.Length; index++)
                slots[index] = Slot(CustomizationRole.Human, "human.slot" + index, (byte)(index + 1), false, true, false, "none");

            var options = new[]
            {
                Visual(CustomizationRole.Human, "human.base", "human-a", 1),
                Visual(CustomizationRole.Human, "human.base", "human-b", 2),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", 1),
                None(CustomizationRole.Human, "human.compat"),
                Visual(CustomizationRole.Human, "human.compat", "compat-b", 1, new[] { "human-b" }),
                Visual(CustomizationRole.Human, "human.compat", "compat-default", 2)
            }.Concat(slots.Skip(3).SelectMany(slot => new[]
            {
                None(CustomizationRole.Human, slot.SlotId),
                Visual(CustomizationRole.Human, slot.SlotId, "item-" + slot.WireSlotId, 1)
            })).ToArray();

            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.codec.fixture", 1, slots, options,
                out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(snapshot.RuntimeReady, Is.True);
            return snapshot;
        }

        private static CustomizationSlotRecord Slot(CustomizationRole role, string id, byte wire,
            bool required, bool allowsNone, bool isBase, string defaultOption) => new CustomizationSlotRecord
        {
            Role = role,
            SlotId = id,
            WireSlotId = wire,
            Required = required,
            AllowsNone = allowsNone,
            IsBaseSlot = isBase,
            DefaultOptionId = defaultOption
        };

        private static CustomizationOptionRecord None(CustomizationRole role, string slotId) => new CustomizationOptionRecord
        {
            Role = role,
            SlotId = slotId,
            OptionId = "none",
            WireOptionId = 0,
            Kind = CustomizationOptionKind.None
        };

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slotId, string optionId,
            ushort wireOptionId, string[] compatibleBases = null) => new CustomizationOptionRecord
        {
            Role = role,
            SlotId = slotId,
            OptionId = optionId,
            WireOptionId = wireOptionId,
            Kind = CustomizationOptionKind.SkinnedPart,
            AssetId = "asset-" + slotId + "-" + optionId,
            HasRuntimeAsset = true,
            CompatibleBaseOptionIds = compatibleBases ?? Array.Empty<string>()
        };
    }
}
