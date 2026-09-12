using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class MessageFramingTests
    {
        [Test]
        public void EncodeRejectsNullEmptyAndOversizedPayloads()
        {
            var framing = new MessageFraming();

            Assert.Throws<ArgumentOutOfRangeException>(() => framing.Encode(1, null).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => framing.Encode(1, Array.Empty<byte>()).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => framing.Encode(1, new byte[MessageFraming.MaximumMessageBytes + 1]).ToArray());
        }

        [TestCase(1)]
        [TestCase(MessageFraming.ChunkBytes)]
        [TestCase(MessageFraming.ChunkBytes + 1)]
        [TestCase(MessageFraming.MaximumMessageBytes)]
        public void BoundaryPayloadsRoundTripWithinPacketLimit(int length)
        {
            var sender = new MessageFraming();
            var receiver = new MessageFraming();
            var payload = Payload(length, 17);
            var packets = sender.Encode(9, payload).ToArray();
            var delivered = new List<byte[]>();
            receiver.MessageReceived += (member, kind, message) =>
            {
                Assert.That(member, Is.EqualTo("member-a"));
                Assert.That(kind, Is.EqualTo(9));
                delivered.Add(message);
            };

            Assert.That(packets, Has.Length.EqualTo((length + MessageFraming.ChunkBytes - 1) / MessageFraming.ChunkBytes));
            Assert.That(packets.All(packet => packet.Length <= EosPeerTransport.MaximumPacketBytes), Is.True);
            foreach (var packet in packets.Reverse())
                Assert.That(receiver.Accept("member-a", new ArraySegment<byte>(packet), 10), Is.True);

            Assert.That(delivered, Has.Count.EqualTo(1));
            Assert.That(delivered[0], Is.EqualTo(payload));
        }

        [Test]
        public void NonZeroArraySegmentOffsetRoundTrips()
        {
            var sender = new MessageFraming();
            var receiver = new MessageFraming();
            var payload = Payload(73, 29);
            var packet = sender.Encode(4, payload).Single();
            var envelope = new byte[packet.Length + 11];
            Buffer.BlockCopy(packet, 0, envelope, 7, packet.Length);
            byte[] delivered = null;
            receiver.MessageReceived += (_, __, message) => delivered = message;

            Assert.That(receiver.Accept("member", new ArraySegment<byte>(envelope, 7, packet.Length), 0), Is.True);
            Assert.That(delivered, Is.EqualTo(payload));
        }

        [Test]
        public void DuplicateFragmentIsRejectedWithoutPreventingCompletion()
        {
            var sender = new MessageFraming();
            var receiver = new MessageFraming();
            var payload = Payload(MessageFraming.ChunkBytes + 7, 41);
            var packets = sender.Encode(3, payload).ToArray();
            var delivered = new List<byte[]>();
            receiver.MessageReceived += (_, __, message) => delivered.Add(message);

            Assert.That(receiver.Accept("member", Segment(packets[0]), 0), Is.True);
            Assert.That(receiver.Accept("member", Segment(packets[0]), 0), Is.False);
            Assert.That(receiver.Accept("member", Segment(packets[1]), 0), Is.True);

            Assert.That(delivered, Has.Count.EqualTo(1));
            Assert.That(delivered[0], Is.EqualTo(payload));
        }

        [Test]
        public void ConflictingFragmentMetadataIsRejectedWithoutPoisoningAssembly()
        {
            var sender = new MessageFraming();
            var receiver = new MessageFraming();
            var payload = Payload(MessageFraming.ChunkBytes + 3, 53);
            var packets = sender.Encode(2, payload).ToArray();
            var conflicting = (byte[])packets[1].Clone();
            conflicting[1] = 7;
            byte[] delivered = null;
            receiver.MessageReceived += (_, __, message) => delivered = message;

            Assert.That(receiver.Accept("member", Segment(packets[0]), 0), Is.True);
            Assert.That(receiver.Accept("member", Segment(conflicting), 0), Is.False);
            Assert.That(receiver.Accept("member", Segment(packets[1]), 0), Is.True);
            Assert.That(delivered, Is.EqualTo(payload));
        }

        [Test]
        public void SameMessageIdFromDifferentMembersRemainsIsolated()
        {
            var senderA = new MessageFraming();
            var senderB = new MessageFraming();
            var receiver = new MessageFraming();
            var payloadA = Payload(MessageFraming.ChunkBytes + 1, 61);
            var payloadB = Payload(MessageFraming.ChunkBytes + 2, 67);
            var packetsA = senderA.Encode(5, payloadA).ToArray();
            var packetsB = senderB.Encode(6, payloadB).ToArray();
            var delivered = new Dictionary<string, byte[]>();
            receiver.MessageReceived += (member, _, message) => delivered.Add(member, message);

            Assert.That(receiver.Accept("member-a", Segment(packetsA[0]), 0), Is.True);
            Assert.That(receiver.Accept("member-b", Segment(packetsB[0]), 0), Is.True);
            Assert.That(receiver.Accept("member-b", Segment(packetsB[1]), 0), Is.True);
            Assert.That(receiver.Accept("member-a", Segment(packetsA[1]), 0), Is.True);

            Assert.That(delivered["member-a"], Is.EqualTo(payloadA));
            Assert.That(delivered["member-b"], Is.EqualTo(payloadB));
        }

        [Test]
        public void MalformedHeadersAndLengthsAreRejectedBeforeDelivery()
        {
            var sender = new MessageFraming();
            var source = sender.Encode(8, Payload(MessageFraming.ChunkBytes + 1, 71)).First();
            var malformed = new List<byte[]>
            {
                source.Take(MessageFraming.HeaderBytes).ToArray(),
                Mutate(source, 0, 0),
                Mutate(source, 7, 1),
                Mutate(source, 6, 2),
                Mutate(Mutate(source, 8, 0), 9, 0),
                source.Take(source.Length - 1).ToArray()
            };

            foreach (var packet in malformed)
            {
                var receiver = new MessageFraming();
                var delivered = false;
                receiver.MessageReceived += (_, __, ___) => delivered = true;
                Assert.That(receiver.Accept("member", Segment(packet), 0), Is.False);
                Assert.That(delivered, Is.False);
            }
        }

        [Test]
        public void CompletedAssembliesDoNotConsumePendingMemberBudget()
        {
            var receiver = new MessageFraming();
            for (var member = 0; member < 16; member++)
                DeliverFragmented(receiver, "member-" + member, now: 0);

            var next = new MessageFraming().Encode(1, Payload(MessageFraming.ChunkBytes + 1, 83)).First();
            Assert.That(receiver.Accept("member-16", Segment(next), 0), Is.True,
                "Completed members must not occupy the global pending-member budget.");
        }

        [Test]
        public void ExpiredAssembliesReleaseGlobalMemberBudget()
        {
            var receiver = new MessageFraming();
            for (var member = 0; member < 16; member++)
            {
                var first = new MessageFraming().Encode(1, Payload(MessageFraming.ChunkBytes + 1, 89 + member)).First();
                Assert.That(receiver.Accept("member-" + member, Segment(first), 0), Is.True);
            }

            var next = new MessageFraming().Encode(1, Payload(MessageFraming.ChunkBytes + 1, 109)).First();
            Assert.That(receiver.Accept("member-16", Segment(next), 3), Is.True,
                "Expired assemblies from inactive members must release the global budget.");
        }

        private static void DeliverFragmented(MessageFraming receiver, string member, double now)
        {
            var packets = new MessageFraming().Encode(1, Payload(MessageFraming.ChunkBytes + 1, member.Length)).ToArray();
            foreach (var packet in packets)
                Assert.That(receiver.Accept(member, Segment(packet), now), Is.True);
        }

        private static byte[] Payload(int length, int seed)
        {
            var data = new byte[length];
            for (var index = 0; index < data.Length; index++) data[index] = (byte)((index * 31 + seed) & 0xff);
            return data;
        }

        private static ArraySegment<byte> Segment(byte[] packet) => new ArraySegment<byte>(packet);

        private static byte[] Mutate(byte[] source, int index, byte value)
        {
            var copy = (byte[])source.Clone();
            copy[index] = value;
            return copy;
        }
    }
}
