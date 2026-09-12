using System.Linq;
using LetMeSleep.Gameplay;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.GameplayAuthorityTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class GameplayBiteAuthorityTests
    {
        [Test]
        public void BiteAttachmentExistsOnlyWhileWorldContinuouslyResolvesContact()
        {
            var world = new FakeWorld();
            world.BiteStarts.Enqueue(BiteOn(HumanOne));
            world.BiteResolutions.Enqueue(null);
            var authority = Start(world, secondHuman: true);

            Assert.That(authority.SubmitInput("mosquito", Input(Mosquito, 1, bite: true)), Is.EqualTo(CommandReject.None));
            Advance(authority);
            var attached = Actor(authority.CaptureSnapshot(), Mosquito);
            Assert.That(attached.LifeState, Is.EqualTo(LifeState.PreparingBite));
            Assert.That(attached.BiteAttachment.HasValue, Is.True);
            Assert.That(attached.BiteAttachment.Value.VictimId, Is.EqualTo(HumanOne));
            Assert.That(world.BiteQueries.Single().ActiveHumanCount, Is.EqualTo(2));

            Advance(authority);
            var detached = Actor(authority.CaptureSnapshot(), Mosquito);
            Assert.That(detached.LifeState, Is.EqualTo(LifeState.Flying));
            Assert.That(detached.BiteAttachment.HasValue, Is.False);
            Assert.That(world.ResolveBiteHumanCounts, Is.EqualTo(new[] { 2 }));
            Assert.That(authority.DrainEvents().Any(item => item.Kind == GameplayEventKind.BiteEnded), Is.True);
        }

        [Test]
        public void DetachedBiteCannotBecomeFutureTargetWithoutReleaseAndFreshWorldQuery()
        {
            var world = new FakeWorld();
            world.BiteStarts.Enqueue(BiteOn(HumanOne));
            world.BiteStarts.Enqueue(BiteOn(HumanTwo, poseRevision: 7));
            world.BiteResolutions.Enqueue(null);
            var authority = Start(world, secondHuman: true);

            Assert.That(authority.SubmitInput("mosquito", Input(Mosquito, 1, bite: true)), Is.EqualTo(CommandReject.None));
            Advance(authority);
            Advance(authority);
            Assert.That(Actor(authority.CaptureSnapshot(), Mosquito).BiteAttachment.HasValue, Is.False);

            Assert.That(authority.SubmitInput("mosquito", Input(Mosquito, 2, bite: true, viewRevision: 2)), Is.EqualTo(CommandReject.None));
            Advance(authority);
            Assert.That(world.BiteQueries, Has.Count.EqualTo(1), "Holding after detach must not reuse the former public attachment as a target.");

            authority.RemoveActor(HumanOne, ActorRemovalReason.Left);
            Assert.That(authority.IsRunning, Is.True, "One human and one mosquito still remain.");
            Assert.That(authority.SubmitInput("mosquito", Input(Mosquito, 3, bite: false, viewRevision: 2)), Is.EqualTo(CommandReject.None));
            Advance(authority);
            Assert.That(authority.SubmitInput("mosquito", Input(Mosquito, 4, bite: true, viewRevision: 2)), Is.EqualTo(CommandReject.None));
            Advance(authority);

            var reacquired = Actor(authority.CaptureSnapshot(), Mosquito).BiteAttachment;
            Assert.That(reacquired.HasValue, Is.True);
            Assert.That(reacquired.Value.VictimId, Is.EqualTo(HumanTwo));
            Assert.That(reacquired.Value.PoseRevision, Is.EqualTo(7));
            Assert.That(world.BiteQueries.Select(query => query.ActiveHumanCount), Is.EqualTo(new[] { 2, 1 }));
        }
    }
}
