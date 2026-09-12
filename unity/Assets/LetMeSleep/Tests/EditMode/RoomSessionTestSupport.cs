using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    internal static class RoomSessionTestSupport
    {
        internal sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<int> values;
            private readonly int fallback;

            internal SequenceRandom(int fallback, params int[] values)
            {
                this.fallback = fallback;
                this.values = new Queue<int>(values ?? Array.Empty<int>());
            }

            public int Next(int exclusiveMax)
            {
                Assert.That(exclusiveMax, Is.GreaterThan(0), "RoomSession requested an invalid random range.");
                var raw = values.Count > 0 ? values.Dequeue() : fallback;
                return ((raw % exclusiveMax) + exclusiveMax) % exclusiveMax;
            }
        }

        internal static RoomSession TwoPlayerSession(IRandomSource random = null)
        {
            var session = new RoomSession("owner-puid", "Owner", random ?? new SequenceRandom(0));
            Assert.That(session.Join("guest-puid", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            return session;
        }

        internal static void ReadyEveryone(RoomSession session)
        {
            foreach (var member in session.Snapshot().Members)
                Assert.That(session.SetReady(member.Id, true), Is.EqualTo(RoomError.None));
        }

        internal static MemberView Member(RoomView view, string id)
        {
            return view.Members.Single(member => member.Id == id);
        }

        internal static int CountRole(RoomView view, PlayerRole role)
        {
            return view.Members.Count(member => member.Role == role);
        }
    }
}
