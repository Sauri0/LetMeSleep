using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomWireCodecTests
    {
        private readonly struct RawMember
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly byte Ready;
            internal readonly byte Role;

            internal RawMember(string id, string name, byte ready = 0, byte role = 0)
            {
                Id = id;
                Name = name;
                Ready = ready;
                Role = role;
            }
        }

        [Test]
        public void WaitingSnapshotRoundTripsRulesMembersAndUnicodeName()
        {
            var session = new RoomSession("owner", "Dueño Ñ", new RoomSessionTestSupport.SequenceRandom(0));
            Assert.That(session.Join("guest", "Invité", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(session.ChangeRules("owner", new RoomRules(1, 417, 37.25f)), Is.EqualTo(RoomError.None));
            Assert.That(session.SetReady("guest", true), Is.EqualTo(RoomError.None));
            var original = session.Snapshot();

            var packet = RoomWireCodec.Encode(original);
            Assert.That(RoomWireCodec.TryDecode(packet, "owner", out var decoded), Is.True);

            AssertEquivalent(decoded, original);
            Assert.That(decoded.Members, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<MemberView>>());
        }

        [Test]
        public void PlayingSnapshotRoundTripsFreshRolesAndRound()
        {
            var session = RoomSessionTestSupport.TwoPlayerSession(new RoomSessionTestSupport.SequenceRandom(0, 1, 0));
            RoomSessionTestSupport.ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var original = session.Snapshot();

            Assert.That(RoomWireCodec.TryDecode(RoomWireCodec.Encode(original), "owner-puid", out var decoded), Is.True);

            AssertEquivalent(decoded, original);
            Assert.That(decoded.Round, Is.EqualTo(1));
            Assert.That(decoded.Members.Count(member => member.Role == PlayerRole.Human), Is.EqualTo(1));
            Assert.That(decoded.Members.Count(member => member.Role == PlayerRole.Mosquito), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosedSnapshotRoundTripsAfterOwnerIsRemoved(bool keepGuest)
        {
            var session = new RoomSession("owner", "Owner", new RoomSessionTestSupport.SequenceRandom(0));
            if (keepGuest)
                Assert.That(session.Join("guest", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(session.Leave("owner"), Is.EqualTo(RoomError.None));
            var original = session.Snapshot();

            Assert.That(RoomWireCodec.TryDecode(RoomWireCodec.Encode(original), "owner", out var decoded), Is.True);
            AssertEquivalent(decoded, original);
            Assert.That(decoded.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(decoded.Members.Any(member => member.Id == "owner"), Is.False);
        }

        [Test]
        public void MaximumRosterRemainsBelowWireLimitAndRoundTrips()
        {
            var ownerId = new string('o', 128);
            var session = new RoomSession(ownerId, new string('A', 24), new RoomSessionTestSupport.SequenceRandom(0));
            for (var index = 1; index < RoomRules.Capacity; index++)
            {
                var id = index.ToString("D2") + new string((char)('a' + index), 126);
                Assert.That(session.Join(id, new string((char)('A' + index), 24), RoomSession.Protocol), Is.EqualTo(RoomError.None));
            }

            var packet = RoomWireCodec.Encode(session.Snapshot());

            Assert.That(packet.Length, Is.LessThanOrEqualTo(8192));
            Assert.That(RoomWireCodec.TryDecode(packet, ownerId, out var decoded), Is.True);
            Assert.That(decoded.Members, Has.Count.EqualTo(RoomRules.Capacity));
        }

        [Test]
        public void WrongOwnerVersionTrailingDataAndEveryTruncationAreRejected()
        {
            var packet = RoomWireCodec.Encode(RoomSessionTestSupport.TwoPlayerSession().Snapshot());
            Assert.That(RoomWireCodec.TryDecode(packet, "different-owner", out _), Is.False);

            var wrongVersion = (byte[])packet.Clone();
            wrongVersion[0] = 2;
            Assert.That(RoomWireCodec.TryDecode(wrongVersion, "owner-puid", out _), Is.False);

            var trailing = new byte[packet.Length + 1];
            Buffer.BlockCopy(packet, 0, trailing, 0, packet.Length);
            trailing[trailing.Length - 1] = 99;
            Assert.That(RoomWireCodec.TryDecode(trailing, "owner-puid", out _), Is.False);

            for (var length = 0; length < packet.Length; length++)
                Assert.That(RoomWireCodec.TryDecode(packet.Take(length).ToArray(), "owner-puid", out _), Is.False, "truncated length " + length);
        }

        [Test]
        public void InvalidScalarRosterAndRuleFieldsAreRejected()
        {
            var owner = new RawMember("owner", "Owner");
            var guest = new RawMember("guest", "Guest");
            var invalid = new Dictionary<string, byte[]>
            {
                ["negative revision"] = Raw(revision: -1, members: new[] { owner }),
                ["negative round"] = Raw(round: -1, members: new[] { owner }),
                ["invalid phase"] = Raw(phase: 4, members: new[] { owner }),
                ["human count above five"] = Raw(humanCount: 6, members: new[] { owner }),
                ["round too short"] = Raw(seconds: 29, members: new[] { owner }),
                ["nonfinite quota"] = Raw(quota: float.NaN, members: new[] { owner }),
                ["unknown map"] = Raw(map: "future-map", members: new[] { owner }),
                ["capacity above sixteen"] = Raw(declaredCount: 17, members: Array.Empty<RawMember>()),
                ["duplicate member"] = Raw(members: new[] { owner, owner }),
                ["invalid ready byte"] = Raw(members: new[] { new RawMember("owner", "Owner", 2) }),
                ["invalid role"] = Raw(members: new[] { new RawMember("owner", "Owner", role: 3) }),
                ["owner absent while open"] = Raw(members: new[] { guest }),
                ["blank name"] = Raw(members: new[] { new RawMember("owner", " ") }),
                ["control in name"] = Raw(members: new[] { new RawMember("owner", "Bad\nName") })
            };

            foreach (var item in invalid)
            {
                Assert.That(RoomWireCodec.TryDecode(item.Value, "owner", out var view), Is.False, item.Key);
                Assert.That(view, Is.Null, item.Key);
            }
        }

        [Test]
        public void InvalidUtf8IsRejectedWithoutReplacementCharacters()
        {
            var packet = Raw(members: new[] { new RawMember("owner", "Owner") });
            packet[15] = 0xff;

            Assert.That(RoomWireCodec.TryDecode(packet, "owner", out var view), Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TextHelpersEnforceUtf8ByteLimitBeforeAllocation()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            var exact = new string('é', 64);
            RoomWireCodec.WriteText(writer, exact, 128);
            Assert.Throws<InvalidDataException>(() => RoomWireCodec.WriteText(writer, new string('é', 65), 128));
            writer.Flush();
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            Assert.That(RoomWireCodec.ReadText(reader, 128), Is.EqualTo(exact));
        }

        [Test]
        public void BoundedArbitraryPacketsNeverEscapeParserExceptions()
        {
            var random = new Random(9404);
            for (var sample = 0; sample < 1000; sample++)
            {
                var packet = new byte[random.Next(0, 257)];
                random.NextBytes(packet);
                Assert.DoesNotThrow(() => RoomWireCodec.TryDecode(packet, "owner", out _), "sample " + sample);
            }
        }

        private static byte[] Raw(
            byte version = 1,
            long revision = 0,
            int round = 0,
            string owner = "owner",
            byte phase = 0,
            byte humanCount = 0,
            int seconds = 180,
            float quota = 20,
            string map = RoomRules.AlfaMap,
            RawMember[] members = null,
            byte? declaredCount = null)
        {
            members = members ?? new[] { new RawMember(owner, "Owner") };
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(version);
            writer.Write(revision);
            writer.Write(round);
            RoomWireCodec.WriteText(writer, owner, 128);
            writer.Write(phase);
            writer.Write(humanCount);
            writer.Write(seconds);
            writer.Write(quota);
            RoomWireCodec.WriteText(writer, map, 64);
            writer.Write(declaredCount ?? (byte)members.Length);
            foreach (var member in members)
            {
                RoomWireCodec.WriteText(writer, member.Id, 128);
                RoomWireCodec.WriteText(writer, member.Name, 96);
                writer.Write(member.Ready);
                writer.Write(member.Role);
            }
            writer.Flush();
            return stream.ToArray();
        }

        private static void AssertEquivalent(RoomView actual, RoomView expected)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.Round, Is.EqualTo(expected.Round));
            Assert.That(actual.OwnerId, Is.EqualTo(expected.OwnerId));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Rules.HumanCount, Is.EqualTo(expected.Rules.HumanCount));
            Assert.That(actual.Rules.RoundSeconds, Is.EqualTo(expected.Rules.RoundSeconds));
            Assert.That(actual.Rules.BloodQuota, Is.EqualTo(expected.Rules.BloodQuota));
            Assert.That(actual.Rules.MapId, Is.EqualTo(expected.Rules.MapId));
            Assert.That(actual.Members.Select(member => member.Id), Is.EqualTo(expected.Members.Select(member => member.Id)));
            Assert.That(actual.Members.Select(member => member.Name), Is.EqualTo(expected.Members.Select(member => member.Name)));
            Assert.That(actual.Members.Select(member => member.Ready), Is.EqualTo(expected.Members.Select(member => member.Ready)));
            Assert.That(actual.Members.Select(member => member.Role), Is.EqualTo(expected.Members.Select(member => member.Role)));
        }
    }
}
