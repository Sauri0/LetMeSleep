using LetMeSleep.Bootstrap;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomLifecycleTests
    {
        [Test]
        public void FirstRoundOfANewRoomStartsAfterTheHostClosedThePreviousRoom()
        {
            var tracker = new ActiveRoundTracker();
            tracker.Begin("ROOMAAAAAA", 1);
            // Host of room A leaves: the client never goes through LeaveRoom. Room B numbers from 1 again.
            Assert.That(tracker.IsNewRound("ROOMBBBBBB", 1), Is.True, "Round 1 of room B must prepare the game.");
            tracker.Reset();
            Assert.That(tracker.IsActive, Is.False);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 1), Is.True, "Rejoining after a reset prepares the round again.");
        }

        [Test]
        public void RepeatedViewsOfTheActiveRoundDoNotRestartIt()
        {
            var tracker = new ActiveRoundTracker();
            tracker.Begin("ROOMAAAAAA", 3);
            Assert.That(tracker.IsActive, Is.True);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 3), Is.False);
            Assert.That(tracker.IsNewRound("ROOMAAAAAA", 4), Is.True);
            Assert.That(tracker.IsNewRound(null, 3), Is.True);
        }
    }
}
