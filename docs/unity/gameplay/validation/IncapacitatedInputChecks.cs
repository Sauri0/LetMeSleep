using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.GameplayAuthorityTestSupport;

// CPU authority checks. State setup invokes the real transition helper; FakeWorld
// does not prove collision, local mouse polling, Animator, or ragdoll behavior.
public sealed class IncapacitatedInputChecks
{
    [TestCase(LifeState.Falling, false)]
    [TestCase(LifeState.Fainted, false)]
    [TestCase(LifeState.Stunned, false)]
    [TestCase(LifeState.Recovering, false)]
    [TestCase(LifeState.Falling, true)]
    [TestCase(LifeState.Fainted, true)]
    [TestCase(LifeState.Stunned, true)]
    [TestCase(LifeState.Recovering, true)]
    public void RemoteAndBotInputsAcknowledgeWithoutTurningOrReplayingHeldControls(LifeState state, bool bot)
    {
        foreach (var role in new[] { PlayerRole.Human, PlayerRole.Mosquito })
        {
            var world = new FakeWorld(); var authority = Start(world, role, bot);
            Submit(authority, bot, 1, .3f, .2f);
            var before = Snapshot(authority);
            Transition(authority, state);
            Assert.That(authority.CapturePrivate(1).CanAct, Is.False);
            Assert.That(Submit(authority, bot, 2, 2.4f, -.7f), Is.EqualTo(CommandReject.None));
            Assert.That(authority.CapturePrivate(1).LastAcceptedInputSequence, Is.EqualTo(2u));
            Assert.That(Submit(authority, bot, 2, 1.4f, .1f), Is.EqualTo(CommandReject.StaleSequence));
            AssertFrozen(before, Snapshot(authority));
            Advance(authority);
            AssertFrozen(before, Snapshot(authority));
            if (state == LifeState.Falling)
            {
                var query = role == PlayerRole.Human ? world.HumanMoves.Last() : world.MosquitoMoves.Last();
                Assert.That(query.Velocity.Y, Is.LessThan(0), "Control gate must not stop falling physics");
            }
            // Resume without a new packet: the incapacitated command must not replay.
            Transition(authority, role == PlayerRole.Human ? LifeState.Active : LifeState.Flying);
            Advance(authority);
            var resumed = Snapshot(authority);
            Assert.That(resumed.Velocity.X, Is.EqualTo(0).Within(.00001f));
            Assert.That(resumed.Velocity.Z, Is.EqualTo(0).Within(.00001f));
            Assert.That(resumed.BiteAttachment.HasValue, Is.False);
            AssertFrozen(before, resumed);
            Assert.That(Submit(authority, bot, 3, 1.2f, .1f), Is.EqualTo(CommandReject.None));
            Advance(authority);
            Assert.That(Snapshot(authority).ViewYawRadians, Is.EqualTo(1.2f));
            Assert.That(Snapshot(authority).Velocity.Length, Is.GreaterThan(.01f));
        }
    }

    [Test] public void IncapacitatedStreamStillEnforcesOwnerNumbersViewAndRate()
    {
        var a = Start(new FakeWorld(), PlayerRole.Mosquito, false);
        Transition(a, LifeState.Stunned);
        var valid = Command(a, 1, .1f, .1f);
        Assert.That(a.SubmitInput("intruder", valid), Is.EqualTo(CommandReject.WrongOwner));
        Assert.That(a.SubmitBotInput(valid), Is.EqualTo(CommandReject.WrongOwner));
        Assert.That(a.SubmitInput("owner", new PlayerInputCommand(valid.Header, default, 0, float.NaN, 0, Float3.Forward)), Is.EqualTo(CommandReject.InvalidNumber));
        var oldView = new CommandHeader(Epoch, Round, 1, 1, 0, 100);
        Assert.That(a.SubmitInput("owner", new PlayerInputCommand(oldView, default, 0, 0, 0, Float3.Forward)), Is.EqualTo(CommandReject.OldViewRevision));
        for (uint i = 1; i <= 60; i++) Assert.That(Submit(a, false, i, .1f, .1f), Is.EqualTo(CommandReject.None));
        Assert.That(Submit(a, false, 61, .2f, .1f), Is.EqualTo(CommandReject.RateLimited));
        Assert.That(a.CapturePrivate(1).LastAcceptedInputSequence, Is.EqualTo(60u));
        Assert.That(Snapshot(a).ViewYawRadians, Is.Zero);
    }

    [Test] public void EnteringIncapacityClearsInputEvenBeforeNextPacket()
    {
        var a = Start(new FakeWorld(), PlayerRole.Human, false);
        Submit(a, false, 1, .8f, .2f);
        Transition(a, LifeState.Fainted);
        Transition(a, LifeState.Active);
        Advance(a);
        Assert.That(Snapshot(a).Velocity.X, Is.EqualTo(0).Within(.00001f));
        Assert.That(Snapshot(a).Velocity.Z, Is.EqualTo(0).Within(.00001f));
    }

    private static GameplayAuthority Start(FakeWorld world, PlayerRole role, bool bot)
    {
        var a = new GameplayAuthority(world);
        a.BeginRound(new GameplayRoundConfig(Epoch, Round, "house-patio-v1", "cpu-input", 300, 100),
            new[] { new SpawnActor(1, "owner", role, Float3.Zero, isBot: bot),
                new SpawnActor(2, "other", role == PlayerRole.Human ? PlayerRole.Mosquito : PlayerRole.Human, Float3.Forward) });
        return a;
    }
    private static ActorSnapshot Snapshot(GameplayAuthority a) => a.CaptureSnapshot().Actors.Single(x => x.ActorId == 1);
    private static PlayerInputCommand Command(GameplayAuthority a, uint sequence, float yaw, float pitch) =>
        new PlayerInputCommand(new CommandHeader(Epoch, Round, 1, sequence, a.CurrentTick, Snapshot(a).ViewRevision), new Float2(1, 1), 1, yaw, pitch, MathEx.Aim(yaw, pitch), true, true, true, true);
    private static CommandReject Submit(GameplayAuthority a, bool bot, uint sequence, float yaw, float pitch)
    { var c = Command(a, sequence, yaw, pitch); return bot ? a.SubmitBotInput(c) : a.SubmitInput("owner", c); }
    private static void Transition(GameplayAuthority a, LifeState state)
    {
        var actors = (IDictionary)typeof(GameplayAuthority).GetField("actors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(a);
        typeof(GameplayAuthority).GetMethod("SetState", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { actors[1u], (object)state });
    }
    private static void AssertFrozen(ActorSnapshot before, ActorSnapshot after)
    {
        Assert.That(after.ViewYawRadians, Is.EqualTo(before.ViewYawRadians));
        Assert.That(after.ViewPitchRadians, Is.EqualTo(before.ViewPitchRadians));
        Assert.That((after.ViewForward - before.ViewForward).Length, Is.LessThan(.00001f));
        Assert.That(after.BodyRotation.Y, Is.EqualTo(before.BodyRotation.Y));
        Assert.That(after.BodyRotation.W, Is.EqualTo(before.BodyRotation.W));
    }
}
