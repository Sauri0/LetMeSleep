using System;
using System.Collections.Generic;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class PacketHandlerListTests
    {
        [Test]
        public void ThrowingConsumerDoesNotStarveTheOthers()
        {
            var list = new PacketHandlerList(); var seen = new List<string>(); var errors = new List<Exception>();
            list.Add((member, channel, _) => seen.Add("room:" + member));
            // e.g. voice context rejecting a view that raced ahead of the EOS lobby notification.
            list.Add((member, channel, _) => throw new InvalidOperationException("voice"));
            list.Add((member, channel, _) => seen.Add("gameplay:" + channel));
            list.Dispatch("peer", 1, new ArraySegment<byte>(new byte[] { 1 }), errors.Add);
            list.Dispatch("peer", 2, new ArraySegment<byte>(new byte[] { 2 }), errors.Add);
            Assert.That(seen, Is.EqualTo(new[] { "room:peer", "gameplay:1", "room:peer", "gameplay:2" }));
            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void RemovalMatchesMulticastSemantics()
        {
            var list = new PacketHandlerList(); int a = 0, b = 0;
            Action<string, byte, ArraySegment<byte>> first = (_, __, ___) => a++, second = (_, __, ___) => b++;
            list.Add(first); list.Add(second); list.Add(first);
            list.Remove(first);
            list.Dispatch("peer", 0, default, null);
            Assert.That((a, b), Is.EqualTo((1, 1)));
            list.Remove(first); list.Remove(first); list.Remove(null); list.Add(null);
            list.Dispatch("peer", 0, default, null);
            Assert.That((a, b), Is.EqualTo((1, 2)));
            list.Clear();
            Assert.That(list.Count, Is.Zero);
        }

        [Test]
        public void HandlersAddedDuringDispatchRunFromTheNextPacket()
        {
            var list = new PacketHandlerList(); int late = 0;
            list.Add((_, __, ___) => list.Add((x, y, z) => late++));
            list.Dispatch("peer", 0, default, null);
            Assert.That(late, Is.Zero);
            list.Dispatch("peer", 0, default, null);
            Assert.That(late, Is.EqualTo(1));
        }
    }
}
