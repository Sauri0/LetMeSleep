using System;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;
using static LetMeSleep.Tests.EditMode.GameplayAuthorityTestSupport;

// CPU composition only. FakeWorld supplies contact, and this test explicitly bridges the room
// lifecycle; it does not exercise Bootstrap callbacks, PhysX, EOS, UI or a packaged player.
public sealed class TwoRoundFlowChecks
{
    [TestCase(false)]
    [TestCase(true)]
    public void BloodThenTimeoutResetStateWithEitherRepeatedOrChangedLottery(bool repeatRoles)
    {
        var room = TwoPlayerSession(new SequenceRandom(0, 1, 0, repeatRoles ? 1 : 0, 0));
        // Legal short fixture settings only; the native build recipe keeps the normal room settings.
        Assert.That(room.ChangeRules("owner-puid", new RoomRules(roundSeconds: 30, bloodQuota: 1)), Is.EqualTo(RoomError.None));
        var world = new FakeWorld();
        var authority = new GameplayAuthority(world);
        GameSessionState completed = null;
        PlayerRole firstOwnerRole = PlayerRole.Unassigned;

        for (int round = 1; round <= 2; round++)
        {
            ReadyEveryone(room);
            Assert.That(room.StartRound("owner-puid"), Is.EqualTo(RoomError.None));
            var view = room.Snapshot();
            Assert.That(view.Round, Is.EqualTo(round));
            var ownerRole = Member(view, "owner-puid").Role;
            if (round == 1) firstOwnerRole = ownerRole;
            else Assert.That(ownerRole == firstOwnerRole, Is.EqualTo(repeatRoles), "Fresh lottery may repeat; production must not force alternation.");
            var roster = view.Members.Select((m, i) => new SpawnActor((uint)i + 1, m.Id, m.Role, new Float3(i, 1, 0))).ToArray();
            var config = new GameplayRoundConfig((ulong)(100 + round), (ulong)view.Round,
                view.Rules.MapId, "cpu-two-round-fixture", view.Rules.RoundSeconds, view.Rules.BloodQuota);
            authority.BeginRound(config, roster);
            var initial = authority.CaptureSnapshot();
            Assert.That(initial.HostTick, Is.Zero);
            Assert.That(initial.BloodCollected, Is.Zero);
            Assert.That(initial.Result, Is.EqualTo(RoundEndReason.None));
            Assert.That(initial.Winner, Is.EqualTo(PlayerRole.Unassigned));
            Assert.That(initial.TimeRemainingTicks, Is.EqualTo(900));
            Assert.That(authority.DrainEvents(), Is.Empty);
            foreach (var actor in initial.Actors)
            {
                Assert.That(actor.LifeState, Is.EqualTo(actor.Role == PlayerRole.Human ? LifeState.Active : LifeState.Flying));
                Assert.That(actor.BiteAttachment.HasValue || actor.SurfaceAttachment.HasValue, Is.False);
                Assert.That(actor.Velocity.Length, Is.Zero);
                Assert.That(actor.MotionPhase, Is.Zero);
                Assert.That(actor.StrikeState.Phase, Is.EqualTo(StrikePhase.None));
                Assert.That(actor.EquippedToolId, Is.EqualTo(GameplayTools.Hands));
                Assert.That(authority.CapturePrivate(actor.ActorId).RecoverySeconds, Is.Zero);
            }

            var mosquito = roster.Single(a => a.Role == PlayerRole.Mosquito);
            if (round == 2)
            {
                // Reject old epoch and old round independently without consuming sequence 1.
                var state = initial.Actors.Single(a => a.ActorId == mosquito.ActorId);
                foreach (var stale in new[] {
                    new CommandHeader(101, config.RoundId, mosquito.ActorId, 1, 0, state.ViewRevision),
                    new CommandHeader(config.SessionEpoch, 1, mosquito.ActorId, 1, 0, state.ViewRevision) })
                    Assert.That(authority.SubmitInput(mosquito.OwnerPuid, new PlayerInputCommand(stale, default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.WrongRound));
                Assert.That(completed.BloodCollected, Is.EqualTo(1));
                Assert.That(completed.Result, Is.EqualTo(RoundEndReason.BloodGoal));
            }
            world.BiteStarts.Clear(); world.BiteResolutions.Clear();
            if (round == 1)
            {
                var contact = BiteOn(roster.Single(a => a.Role == PlayerRole.Human).ActorId);
                world.BiteStarts.Enqueue(contact);
                for (int i = 0; i < 900; i++) world.BiteResolutions.Enqueue(contact);
            }
            uint sequence = 0;
            while (authority.IsRunning && authority.CurrentTick < 900)
            {
                var actor = authority.CaptureSnapshot().Actors.Single(a => a.ActorId == mosquito.ActorId);
                var header = new CommandHeader(config.SessionEpoch, config.RoundId, mosquito.ActorId, ++sequence, authority.CurrentTick, actor.ViewRevision);
                Assert.That(authority.SubmitInput(mosquito.OwnerPuid,
                    new PlayerInputCommand(header, default, 0, 0, 0, Float3.Forward, bite: round == 1)), Is.EqualTo(CommandReject.None));
                authority.Advance(new HostTick(authority.CurrentTick + 1));
            }
            var result = authority.CaptureSnapshot();
            Assert.That(result.SimulationPhase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(result.Result, Is.EqualTo(round == 1 ? RoundEndReason.BloodGoal : RoundEndReason.TimeExpired));
            Assert.That(result.Winner, Is.EqualTo(round == 1 ? PlayerRole.Mosquito : PlayerRole.Human));
            Assert.That(result.BloodCollected, Is.EqualTo(round == 1 ? 1 : 0));
            Assert.That(authority.DrainEvents().Count(e => e.Kind == GameplayEventKind.RoundEnded), Is.EqualTo(1));
            authority.Advance(new HostTick(authority.CurrentTick + 1));
            Assert.That(authority.DrainEvents(), Is.Empty);
            Assert.That(room.FinishRound("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(room.ReturnToWaiting("owner-puid"), Is.EqualTo(RoomError.None));
            Assert.That(room.Snapshot().Phase, Is.EqualTo(RoomPhase.Waiting));
            Assert.That(room.Snapshot().Members.All(m => !m.Ready && m.Role == PlayerRole.Unassigned), Is.True);
            if (round == 1) completed = result;
        }
    }
}
