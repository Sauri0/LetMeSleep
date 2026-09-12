using System.Linq;
using LetMeSleep.Gameplay;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.GameplayAuthorityTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class GameplayDoorAuthorityTests
    {
        [Test]
        public void HumanUsePublishesDoorStateAndDuplicateSequenceDoesNotRetoggle()
        {
            var world = new FakeWorld();
            world.DoorInteractions.Enqueue(DoorCandidate());
            var authority = Start(world);
            var initial = authority.CaptureSnapshot();

            Assert.That(authority.SubmitAction("human-one", Use(HumanOne, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            var changed = authority.CaptureSnapshot().Doors.Single();
            Assert.That(changed.Revision, Is.EqualTo(2));
            Assert.That(changed.TargetAngleRadians, Is.EqualTo(DoorOpenAngle));
            Assert.That(changed.AngleRadians, Is.GreaterThan(0));
            Assert.That(authority.CapturePrivate(HumanOne).LastDoorResult, Is.EqualTo(DoorUseResult.Accepted));
            var events = authority.DrainEvents().Where(item => item.Kind == GameplayEventKind.DoorChanged).ToArray();
            Assert.That(events, Has.Length.EqualTo(1));
            Assert.That(events[0].Door.HasValue, Is.True);
            Assert.That(events[0].Door.Value.Revision, Is.EqualTo(2));

            Assert.That(authority.SubmitAction("human-one", Use(HumanOne, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            Assert.That(world.DoorQueries, Has.Count.EqualTo(1), "An idempotent retry must not perform a second interaction query.");
            Assert.That(authority.CaptureSnapshot().Doors.Single().TargetAngleRadians, Is.EqualTo(DoorOpenAngle));
            Assert.That(authority.DrainEvents().Any(item => item.Kind == GameplayEventKind.DoorChanged), Is.False);
            Assert.That(initial.Doors.Single().Revision, Is.EqualTo(1), "A previously published snapshot must remain immutable.");
            Assert.That(initial.Doors.Single().AngleRadians, Is.Zero);
        }

        [Test]
        public void HeldUseInputNeverOperatesDoor()
        {
            var world = new FakeWorld();
            var authority = Start(world);

            Assert.That(authority.SubmitInput("human-one", Input(HumanOne, 1, use: true)), Is.EqualTo(CommandReject.None));
            for (var index = 0; index < 4; index++) Advance(authority);

            var door = authority.CaptureSnapshot().Doors.Single();
            Assert.That(world.DoorQueries, Is.Empty);
            Assert.That(door.Revision, Is.EqualTo(1));
            Assert.That(door.AngleRadians, Is.Zero);
            Assert.That(door.TargetAngleRadians, Is.Zero);
        }

        [Test]
        public void MosquitoUseIsRejectedBeforeWorldQuery()
        {
            var world = new FakeWorld();
            var authority = Start(world);

            Assert.That(authority.SubmitAction("mosquito", Use(Mosquito, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            Assert.That(authority.CapturePrivate(Mosquito).LastDoorResult, Is.EqualTo(DoorUseResult.WrongRole));
            Assert.That(world.DoorQueries, Is.Empty);
            Assert.That(authority.CaptureSnapshot().Doors.Single().Revision, Is.EqualTo(1));
        }

        [TestCase(0u, 1f, DoorUseResult.NoDoor, TestName = "DoorUseRejectsMissingCandidate")]
        [TestCase(1u, 2.01f, DoorUseResult.OutOfReach, TestName = "DoorUseRejectsOutOfReachCandidate")]
        [TestCase(99u, 1f, DoorUseResult.StaleRevision, TestName = "DoorUseRejectsStaleCandidate")]
        public void InvalidDoorCandidateDoesNotMutateDoor(uint revision, float distance, DoorUseResult expected)
        {
            var world = new FakeWorld();
            if (revision != 0) world.DoorInteractions.Enqueue(DoorCandidate(revision, distance));
            var authority = Start(world);

            Assert.That(authority.SubmitAction("human-one", Use(HumanOne, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            var door = authority.CaptureSnapshot().Doors.Single();
            Assert.That(authority.CapturePrivate(HumanOne).LastDoorResult, Is.EqualTo(expected));
            Assert.That(door.Revision, Is.EqualTo(1));
            Assert.That(door.AngleRadians, Is.Zero);
            Assert.That(door.TargetAngleRadians, Is.Zero);
            Assert.That(authority.DrainEvents().Any(item => item.Kind == GameplayEventKind.DoorChanged), Is.False);
        }

        [Test]
        public void SameTickUsesAcceptFirstAndCooldownSecond()
        {
            var world = new FakeWorld();
            world.DoorInteractions.Enqueue(DoorCandidate(1));
            world.DoorInteractions.Enqueue(DoorCandidate(2));
            var authority = Start(world, secondHuman: true);

            Assert.That(authority.SubmitAction("human-two", Use(HumanTwo, 1)), Is.EqualTo(CommandReject.None));
            Assert.That(authority.SubmitAction("human-one", Use(HumanOne, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            Assert.That(authority.CapturePrivate(HumanTwo).LastDoorResult, Is.EqualTo(DoorUseResult.Accepted));
            Assert.That(authority.CapturePrivate(HumanOne).LastDoorResult, Is.EqualTo(DoorUseResult.Cooldown));
            Assert.That(authority.CaptureSnapshot().Doors.Single().TargetAngleRadians, Is.EqualTo(DoorOpenAngle));
            Assert.That(authority.DrainEvents().Count(item => item.Kind == GameplayEventKind.DoorChanged), Is.EqualTo(1));
        }

        [Test]
        public void BlockedDoorStopsAtSafeAngleThenResumesWithoutMovingActors()
        {
            var world = new FakeWorld();
            world.DoorInteractions.Enqueue(DoorCandidate());
            world.DoorSweeps.Enqueue(new DoorSweepResult(0, true, Mosquito));
            world.DoorSweeps.Enqueue(new DoorSweepResult(2.5f / 30, false));
            var authority = Start(world);
            var originalActors = authority.CaptureSnapshot().Actors.ToDictionary(actor => actor.ActorId, actor => actor.Position);

            Assert.That(authority.SubmitAction("human-one", Use(HumanOne, 1)), Is.EqualTo(CommandReject.None));
            Advance(authority);
            var blocked = authority.CaptureSnapshot().Doors.Single();
            Assert.That(blocked.Blocked, Is.True);
            Assert.That(blocked.AngleRadians, Is.Zero);
            Assert.That(blocked.TargetAngleRadians, Is.EqualTo(DoorOpenAngle));

            Advance(authority);
            var released = authority.CaptureSnapshot().Doors.Single();
            Assert.That(released.Blocked, Is.False);
            Assert.That(released.AngleRadians, Is.EqualTo(2.5f / 30).Within(.00001f));
            Assert.That(released.TargetAngleRadians, Is.EqualTo(DoorOpenAngle));
            Assert.That(released.Revision, Is.GreaterThan(blocked.Revision));
            foreach (var actor in authority.CaptureSnapshot().Actors)
                AssertPosition(actor.Position, originalActors[actor.ActorId]);

            var events = authority.DrainEvents().Where(item => item.Kind == GameplayEventKind.DoorChanged).ToArray();
            Assert.That(events, Has.Length.EqualTo(3), "Target, blocked and unblocked are distinct reliable changes.");
            Assert.That(events.Last().Door.Value.Blocked, Is.False);
            Assert.That(world.DoorPoses.Last().AngleRadians, Is.EqualTo(released.AngleRadians).Within(.00001f));
        }
    }
}
