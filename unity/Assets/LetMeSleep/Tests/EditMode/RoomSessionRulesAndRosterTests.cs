using System;
using System.Linq;
using LetMeSleep.Core;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RoomSessionRulesAndRosterTests
    {
        [Test]
        public void RulesAcceptOnlyFiniteBoundedAlfaValues()
        {
            var valid = new[]
            {
                new RoomRules(),
                new RoomRules(null, 30, 0.01f),
                new RoomRules(1, 1800, 1000),
                new RoomRules(5, 180, 20, RoomRules.AlfaMap)
            };
            var invalid = new[]
            {
                new RoomRules(0), new RoomRules(6),
                new RoomRules(null, 29), new RoomRules(null, 1801),
                new RoomRules(null, 180, 0), new RoomRules(null, 180, -1),
                new RoomRules(null, 180, 1000.01f),
                new RoomRules(null, 180, float.NaN),
                new RoomRules(null, 180, float.PositiveInfinity),
                new RoomRules(null, 180, 20, null),
                new RoomRules(null, 180, 20, "lobby"),
                new RoomRules(null, 180, 20, "future-map")
            };

            Assert.That(valid.All(rule => rule.IsValid), Is.True, "A supported boundary/default was rejected.");
            Assert.That(invalid.All(rule => !rule.IsValid), Is.True, "An invalid rule or non-alfa map was accepted.");
        }

        [Test]
        public void InvalidRuleChangePreservesAcknowledgedState()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            var before = session.Snapshot();

            Assert.That(session.ChangeRules("owner-puid", new RoomRules(0)), Is.EqualTo(RoomError.InvalidRules));
            var after = session.Snapshot();

            {
                Assert.That(after.Revision, Is.EqualTo(before.Revision));
                Assert.That(after.Rules.HumanCount, Is.EqualTo(before.Rules.HumanCount));
                Assert.That(after.Rules.RoundSeconds, Is.EqualTo(before.Rules.RoundSeconds));
                Assert.That(after.Rules.BloodQuota, Is.EqualTo(before.Rules.BloodQuota));
                Assert.That(after.Members.All(member => member.Ready), Is.True);
            }
        }

        [Test]
        public void SuccessfulRuleChangeIsOwnerOnlyAndInvalidatesEveryReadyVote()
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            var before = session.Snapshot();
            var next = new RoomRules(1, 240, 31.5f);

            Assert.That(session.ChangeRules("guest-puid", next), Is.EqualTo(RoomError.NotOwner));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(before.Revision));
            Assert.That(session.ChangeRules("owner-puid", next), Is.EqualTo(RoomError.None));
            var after = session.Snapshot();

            {
                Assert.That(after.Revision, Is.GreaterThan(before.Revision));
                Assert.That(after.Rules.HumanCount, Is.EqualTo(1));
                Assert.That(after.Rules.RoundSeconds, Is.EqualTo(240));
                Assert.That(after.Rules.BloodQuota, Is.EqualTo(31.5f));
                Assert.That(after.Rules.MapId, Is.EqualTo(RoomRules.AlfaMap));
                Assert.That(after.Members.All(member => !member.Ready), Is.True);
                Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.NotReady));
            }
        }

        [Test]
        public void CapacityIsSixteenAndRejectedJoinsDoNotConsumePlaces()
        {
            var session = new RoomSession("owner-puid", "Owner", new SequenceRandom(0));
            Assert.That(session.Join("guest-1", "Guest", "wrong-protocol"), Is.EqualTo(RoomError.IncompatibleVersion));
            Assert.That(session.Join(" ", "Guest", RoomSession.Protocol), Is.EqualTo(RoomError.InvalidMember));

            for (var index = 1; index < RoomRules.Capacity; index++)
                Assert.That(session.Join("guest-" + index, "Guest " + index, RoomSession.Protocol), Is.EqualTo(RoomError.None));

            var full = session.Snapshot();
            Assert.That(full.Members, Has.Count.EqualTo(RoomRules.Capacity));
            Assert.That(session.Join("overflow", "Overflow", RoomSession.Protocol), Is.EqualTo(RoomError.Full));
            Assert.That(session.Snapshot().Members, Has.Count.EqualTo(RoomRules.Capacity));
        }

        [Test]
        public void FixedHumanCountRequiresBothTeamsAndDoesNotClampToRoster()
        {
            var fivePlayers = new RoomSession("owner", "Owner", new SequenceRandom(0));
            for (var index = 1; index < 5; index++)
                Assert.That(fivePlayers.Join("p" + index, "P" + index, RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(fivePlayers.ChangeRules("owner", new RoomRules(5)), Is.EqualTo(RoomError.None));
            ReadyEveryone(fivePlayers);
            Assert.That(fivePlayers.StartRound("owner"), Is.EqualTo(RoomError.InvalidRules));
            Assert.That(fivePlayers.Snapshot().Phase, Is.EqualTo(RoomPhase.Waiting));

            Assert.That(fivePlayers.ChangeRules("owner", new RoomRules(4)), Is.EqualTo(RoomError.None));
            ReadyEveryone(fivePlayers);
            Assert.That(fivePlayers.StartRound("owner"), Is.EqualTo(RoomError.None));
            Assert.That(CountRole(fivePlayers.Snapshot(), PlayerRole.Human), Is.EqualTo(4));
            Assert.That(CountRole(fivePlayers.Snapshot(), PlayerRole.Mosquito), Is.EqualTo(1));
        }

        [Test]
        public void AutomaticRosterStaysWithinOneToFiveHumansAndKeepsBothTeams()
        {
            foreach (var randomFallback in new[] { 0, int.MaxValue })
            {
                var session = new RoomSession("owner", "Owner", new SequenceRandom(randomFallback));
                for (var index = 1; index < RoomRules.Capacity; index++)
                    Assert.That(session.Join("p" + index, "P" + index, RoomSession.Protocol), Is.EqualTo(RoomError.None));
                ReadyEveryone(session);
                Assert.That(session.StartRound("owner"), Is.EqualTo(RoomError.None));

                var playing = session.Snapshot();
                var humans = CountRole(playing, PlayerRole.Human);
                var mosquitoes = CountRole(playing, PlayerRole.Mosquito);
                {
                    Assert.That(humans, Is.InRange(1, 5));
                    Assert.That(mosquitoes, Is.GreaterThanOrEqualTo(1));
                    Assert.That(humans + mosquitoes, Is.EqualTo(RoomRules.Capacity));
                    Assert.That(playing.Members.All(member => member.Role != PlayerRole.Unassigned), Is.True);
                }
            }
        }

        [Test]
        public void TwoPlayerLotteryCanAssignEitherParticipantAsHuman()
        {
            var ownerHuman = TwoPlayerSession(new SequenceRandom(0, 1, 0));
            ReadyEveryone(ownerHuman);
            Assert.That(ownerHuman.StartRound("owner-puid"), Is.EqualTo(RoomError.None));

            var guestHuman = TwoPlayerSession(new SequenceRandom(0, 0, 0));
            ReadyEveryone(guestHuman);
            Assert.That(guestHuman.StartRound("owner-puid"), Is.EqualTo(RoomError.None));

            {
                Assert.That(Member(ownerHuman.Snapshot(), "owner-puid").Role, Is.EqualTo(PlayerRole.Human));
                Assert.That(Member(ownerHuman.Snapshot(), "guest-puid").Role, Is.EqualTo(PlayerRole.Mosquito));
                Assert.That(Member(guestHuman.Snapshot(), "owner-puid").Role, Is.EqualTo(PlayerRole.Mosquito));
                Assert.That(Member(guestHuman.Snapshot(), "guest-puid").Role, Is.EqualTo(PlayerRole.Human));
            }
        }

        [Test]
        public void NewRoundDrawDoesNotPreservePreviousRoles()
        {
            var session = TwoPlayerSession(new SequenceRandom(0, 1, 0, 0, 0));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var firstOwnerRole = Member(session.Snapshot(), "owner-puid").Role;

            Assert.That(session.FinishRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.None));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var second = session.Snapshot();

            {
                Assert.That(second.Round, Is.EqualTo(2));
                Assert.That(Member(second, "owner-puid").Role, Is.Not.EqualTo(firstOwnerRole));
                Assert.That(CountRole(second, PlayerRole.Human), Is.EqualTo(1));
                Assert.That(CountRole(second, PlayerRole.Mosquito), Is.EqualTo(1));
            }
        }

        [Test]
        public void NewRoundMayRepeatRolesWhenFreshDrawRepeats()
        {
            var session = TwoPlayerSession(new SequenceRandom(0, 1, 0, 1, 0));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var first = session.Snapshot();

            Assert.That(session.FinishRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(session.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.None));
            ReadyEveryone(session);
            Assert.That(session.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var second = session.Snapshot();

            Assert.That(Member(second, "owner-puid").Role, Is.EqualTo(Member(first, "owner-puid").Role),
                "A fresh lottery was replaced by forced role alternation.");
            Assert.That(CountRole(second, PlayerRole.Human), Is.EqualTo(1));
            Assert.That(CountRole(second, PlayerRole.Mosquito), Is.EqualTo(1));
        }
    }
}
