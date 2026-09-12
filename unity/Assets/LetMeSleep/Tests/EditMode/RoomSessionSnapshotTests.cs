using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomSessionSnapshotTests
    {
        [Test]
        public void InitialViewContainsOnlyTrimmedOwnerAndAlfaDefaults()
        {
            var session = new RoomSession("owner-puid", "  Owner  ", new SequenceRandom(0));
            var view = session.Snapshot();

            {
                Assert.That(view.Revision, Is.EqualTo(0));
                Assert.That(view.Round, Is.EqualTo(0));
                Assert.That(view.OwnerId, Is.EqualTo("owner-puid"));
                Assert.That(view.Phase, Is.EqualTo(RoomPhase.Waiting));
                Assert.That(view.Rules.MapId, Is.EqualTo(RoomRules.AlfaMap));
                Assert.That(view.Rules.HumanCount, Is.Null);
                Assert.That(view.Rules.RoundSeconds, Is.EqualTo(180));
                Assert.That(view.Rules.BloodQuota, Is.EqualTo(20));
                Assert.That(view.Members, Has.Count.EqualTo(1));
                Assert.That(view.Members[0].Name, Is.EqualTo("Owner"));
                Assert.That(view.Members[0].Ready, Is.False);
                Assert.That(view.Members[0].Role, Is.EqualTo(PlayerRole.Unassigned));
            }
        }

        [Test]
        public void PublishedRosterIsReadOnlyAndPastViewsRemainUnchanged()
        {
            var session = new RoomSession("owner", "Owner", new SequenceRandom(0));
            var initial = session.Snapshot();
            var writableView = (IList<MemberView>)initial.Members;

            Assert.Throws<NotSupportedException>(() => writableView.Add(new MemberView("intruder", "Intruder", true, PlayerRole.Human)));
            Assert.Throws<NotSupportedException>(() => writableView[0] = new MemberView("intruder", "Intruder", true, PlayerRole.Human));

            Assert.That(session.Join("guest", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            var joined = session.Snapshot();
            ReadyEveryone(session);
            Assert.That(session.ChangeRules("owner", new RoomRules(1, 240, 25)), Is.EqualTo(RoomError.None));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner"), Is.EqualTo(RoomError.None));
            var playing = session.Snapshot();

            {
                Assert.That(initial.Members, Has.Count.EqualTo(1));
                Assert.That(initial.Members[0].Ready, Is.False);
                Assert.That(initial.Members[0].Role, Is.EqualTo(PlayerRole.Unassigned));
                Assert.That(initial.Rules.RoundSeconds, Is.EqualTo(180));
                Assert.That(joined.Members, Has.Count.EqualTo(2));
                Assert.That(joined.Members.All(member => !member.Ready), Is.True);
                Assert.That(joined.Rules.RoundSeconds, Is.EqualTo(180));
                Assert.That(playing.Members, Has.Count.EqualTo(2));
                Assert.That(playing.Members.All(member => member.Ready), Is.True);
                Assert.That(playing.Members.All(member => member.Role != PlayerRole.Unassigned), Is.True);
                Assert.That(playing.Rules.RoundSeconds, Is.EqualTo(240));
                Assert.That(playing.Rules.BloodQuota, Is.EqualTo(25));
            }
        }

        [Test]
        public void EverySuccessfulLifecycleMutationProducesAFreshView()
        {
            var session = new RoomSession("owner", "Owner", new SequenceRandom(0));
            var views = new List<RoomView> { session.Snapshot() };

            Assert.That(session.Join("guest", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());
            Assert.That(session.SetReady("owner", true), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());
            Assert.That(session.SetReady("guest", true), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());
            Assert.That(session.StartRound("owner"), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());
            Assert.That(session.FinishRound("owner"), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());
            Assert.That(session.ReturnToWaiting("owner"), Is.EqualTo(RoomError.None));
            views.Add(session.Snapshot());

            for (var index = 1; index < views.Count; index++)
            {
                Assert.That(views[index], Is.Not.SameAs(views[index - 1]));
                Assert.That(views[index].Revision, Is.GreaterThan(views[index - 1].Revision));
            }
            Assert.That(views[0].Phase, Is.EqualTo(RoomPhase.Waiting));
            Assert.That(views[4].Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(views[5].Phase, Is.EqualTo(RoomPhase.Results));
            Assert.That(views[6].Phase, Is.EqualTo(RoomPhase.Waiting));
        }
    }
}
