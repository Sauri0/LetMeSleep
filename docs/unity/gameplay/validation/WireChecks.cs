using System;
using System.IO;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

public sealed class WireChecks
{
    private static CommandHeader Header => new CommandHeader(71, 82, 13, uint.MaxValue, 120, 3);
    private static PlayerInputCommand Input => new PlayerInputCommand(Header, new Float2(.4f, -.7f), .2f, .8f, -.3f, MathEx.Aim(.8f, -.3f), true, false, true, false);
    private static ActorSnapshot Actor(uint id, bool human, bool attached = false)
    {
        var bite = attached ? new BiteAttachment(1, 101, new Float3(.01f, .02f, .03f), Float3.Forward, 93) : (BiteAttachment?)null;
        var strike = human ? new StrikeState(12, "flyswatter", 1, StrikePhase.Active, 90, new Float3(0, 1, 0), new Float3(0, 1, .7f), -Float3.Forward, .35f) : default;
        return new ActorSnapshot(id, human ? PlayerRole.Human : PlayerRole.Mosquito, human ? LifeState.Active : attached ? LifeState.Biting : LifeState.Flying, 4, new Float3(id, 1, 2), new Float3(.1f, -.2f, .3f), Rotation.Yaw(.4f), MathEx.Aim(.4f, -.2f), .4f, -.2f, 2, 93, human, .3f, 12.5f, null, bite, strike, 120);
    }
    private static DoorSnapshot Door(uint id) => new DoorSnapshot(id, id + 1000, 5, .7f, 1.5f, 2.5f, true, false, 110);
    private static GameSessionState Snapshot(int count = 2, int doors = 1) => new GameSessionState(new GameplayRoundConfig(71, 82, "house-patio-v1", "content-α", 180, 20, new BalanceProfile(35)), 120, SimulationPhase.Running, 2.5f, RoundEndReason.None, PlayerRole.Unassigned, Enumerable.Range(1, count).Select(i => Actor((uint)i, i == 1, i == 2)).ToArray(), Enumerable.Range(1, doors).Select(i => Door((uint)i)).ToArray());
    [Test] public void InputAndActionRoundTripPreserveFieldsAndSequences()
    {
        var bytes = GameplayWireCodec.Encode(Input); Assert.That(GameplayWireCodec.TryDecodeInput(bytes, out var decoded), Is.True);
        Assert.That(decoded.Header.SessionEpoch, Is.EqualTo(71)); Assert.That(decoded.Header.RoundId, Is.EqualTo(82)); Assert.That(decoded.Header.ActorId, Is.EqualTo(13)); Assert.That(decoded.Header.Sequence, Is.EqualTo(uint.MaxValue));
        Assert.That(decoded.MovePlanar.Y, Is.EqualTo(-.7f)); Assert.That(decoded.Vertical, Is.EqualTo(.2f)); Assert.That(decoded.BiteHeld, Is.True); Assert.That(decoded.UseHeld, Is.False); Assert.That(decoded.CrouchHeld, Is.False);
        Assert.That(GameplayWireCodec.Encode(decoded), Is.EqualTo(bytes));
        var action = new PlayerActionCommand(Header, ActionKind.PerchToggle, Float3.Forward); var actionBytes = GameplayWireCodec.Encode(action);
        Assert.That(GameplayWireCodec.TryDecodeAction(actionBytes, out var decodedAction), Is.True); Assert.That(decodedAction.Kind, Is.EqualTo(ActionKind.PerchToggle)); Assert.That(decodedAction.Header.Sequence, Is.EqualTo(uint.MaxValue));
    }
    [Test] public void SnapshotPreservesAnchorsToolsDoorsAndBalance()
    {
        var s = Snapshot(); var bytes = GameplayWireCodec.Encode(s); Assert.That(GameplayWireCodec.TryDecodeSnapshot(bytes, out var decoded), Is.True);
        Assert.That(decoded.BalanceHash, Is.EqualTo(s.BalanceHash)); Assert.That(decoded.TimeRemainingTicks, Is.EqualTo(5280)); Assert.That(decoded.ContentHash, Is.EqualTo("content-α"));
        Assert.That(decoded.Actors[0].StrikeState.ToolId, Is.EqualTo("flyswatter")); Assert.That(decoded.Actors[0].StrikeState.Target.Z, Is.EqualTo(.7f));
        Assert.That(decoded.Actors[1].BiteAttachment.Value.VictimId, Is.EqualTo(1)); Assert.That(decoded.Actors[1].BiteAttachment.Value.LocalPoint.Y, Is.EqualTo(.02f)); Assert.That(decoded.Doors[0].AngleRadians, Is.EqualTo(.7f));
        Assert.That(GameplayWireCodec.Encode(decoded), Is.EqualTo(bytes));
    }
    [Test] public void PrivateAndEventsCarryRoundIdentity()
    {
        var p = new ActorPrivateState(13, 9, 8, CommandReject.Cooldown, InteractionHint.Preparing, .2f, .3f, 34, 2, false, DoorUseResult.Blocked, 71, 82, 120);
        var bytes = GameplayWireCodec.Encode(p); Assert.That(GameplayWireCodec.TryDecodePrivate(bytes, out var decoded), Is.True);
        Assert.That(decoded.SessionEpoch, Is.EqualTo(71)); Assert.That(decoded.RoundId, Is.EqualTo(82)); Assert.That(decoded.HostTick, Is.EqualTo(120)); Assert.That(decoded.LastDoorResult, Is.EqualTo(DoorUseResult.Blocked)); Assert.That(decoded.HelpTargetId, Is.EqualTo(2)); Assert.That(GameplayWireCodec.Encode(decoded), Is.EqualTo(bytes));
        foreach (var e in new[] {
            new GameplayEvent(71, 82, 33, 120, GameplayEventKind.StrikeImpact, 1, 2, 5, new Float3(1, 2, 3), Float3.Up),
            new GameplayEvent(71, 82, 34, 120, GameplayEventKind.DoorChanged, 0, 0, 5, default, Float3.Up, Door(1)),
            new GameplayEvent(71, 82, 35, 120, GameplayEventKind.RoundEnded, 0, 0, 0, default, default, null, RoundEndReason.TimeExpired) })
        {
            var encoded = GameplayWireCodec.Encode(e); Assert.That(GameplayWireCodec.TryDecodeEvent(encoded, out var item), Is.True);
            Assert.That(item.EventId, Is.EqualTo(e.EventId)); Assert.That(item.Kind, Is.EqualTo(e.Kind)); Assert.That(item.TargetActorId, Is.EqualTo(e.TargetActorId)); Assert.That(GameplayWireCodec.Encode(item), Is.EqualTo(encoded));
        }
    }
    [Test] public void MaximumPopulationAndDoorCountStayWithinMessageCap()
    {
        var bytes = GameplayWireCodec.Encode(Snapshot(16, 128)); Assert.That(bytes.Length, Is.LessThanOrEqualTo(16384)); Assert.That(GameplayWireCodec.TryDecodeSnapshot(bytes, out var decoded), Is.True); Assert.That(decoded.Actors.Count, Is.EqualTo(16)); Assert.That(decoded.Doors.Count, Is.EqualTo(128));
        Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Snapshot(17, 128)));
        Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(Snapshot(16, 129)));
    }
    [Test] public void TruncationAndTrailingBytesNeverDecode()
    {
        var frames = new[] {
            GameplayWireCodec.Encode(Input), GameplayWireCodec.Encode(Snapshot()), GameplayWireCodec.Encode(new PlayerActionCommand(Header, ActionKind.Use, Float3.Forward)),
            GameplayWireCodec.Encode(new ActorPrivateState(13, 1, 2, CommandReject.None, InteractionHint.Helping, 0, 0, 0, 2, true, DoorUseResult.Accepted, 71, 82, 120)),
            GameplayWireCodec.Encode(new GameplayEvent(71, 82, 1, 120, GameplayEventKind.DoorChanged, 0, 0, 5, default, Float3.Up, Door(1))) };
        foreach (var frame in frames)
        {
            for (int n = 0; n < frame.Length; n++)
            {
                var cut = frame.Take(n).ToArray(); Assert.That(Any(cut), Is.False, "Truncation " + n);
            }
            Assert.That(Any(frame.Concat(new byte[] { 0 }).ToArray()), Is.False);
        }
        Assert.That(Any(new byte[16385]), Is.False); Assert.That(Any(null), Is.False);
    }
    [Test] public void InvalidVersionsEnumsBooleansNumbersAndUtf8AreRejected()
    {
        var input = GameplayWireCodec.Encode(Input);
        var version = (byte[])input.Clone(); version[4] = 2; Assert.That(Any(version), Is.False);
        var nan = (byte[])input.Clone(); Array.Copy(BitConverter.GetBytes(float.NaN), 0, nan, 39, 4); Assert.That(Any(nan), Is.False);
        var boolean = (byte[])input.Clone(); boolean[71] = 2; Assert.That(Any(boolean), Is.False);
        var action = GameplayWireCodec.Encode(new PlayerActionCommand(Header, ActionKind.Use, Float3.Forward)); action[39] = 255; Assert.That(Any(action), Is.False);
        var utf8 = GameplayWireCodec.Encode(Snapshot()); utf8[29] = 0xC0; utf8[30] = 0xAF; Assert.That(Any(utf8), Is.False);
        var size = GameplayWireCodec.Encode(Snapshot()); size[27] = 255; size[28] = 255; Assert.That(Any(size), Is.False);
        Assert.That(GameplayWireCodec.TryDecodeEvent(input, out _), Is.False);
    }
    [Test] public void InvalidSnapshotCountsAndNullPrivateIdentityAreRejected()
    {
        var bytes = GameplayWireCodec.Encode(Snapshot());
        using (var stream = new MemoryStream(bytes)) using (var reader = new BinaryReader(stream))
        {
            stream.Position = 27; for (int i = 0; i < 3; i++) { int length = reader.ReadUInt16(); stream.Position += length; }
            stream.Position += 15; bytes[stream.Position] = 17;
        }
        Assert.That(Any(bytes), Is.False);
        var invalid = new ActorPrivateState(13, 0, 0, CommandReject.None, InteractionHint.None, 0, 0, 0, 0, true, DoorUseResult.Accepted);
        Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(invalid));
    }
    [Test] public void RandomMalformedMessagesNeverThrowOrReturnPartialValues()
    {
        var random = new Random(139);
        for (int i = 0; i < 1000; i++) { var bytes = new byte[random.Next(0, 800)]; random.NextBytes(bytes); Assert.That(Any(bytes), Is.False); }
        var valid = GameplayWireCodec.Encode(Snapshot());
        for (int i = 0; i < 300; i++)
        {
            var mutated = (byte[])valid.Clone(); for (int j = 0; j < 4; j++) mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
            Assert.DoesNotThrow(() => Any(mutated));
        }
    }
    private static bool Any(byte[] data) => GameplayWireCodec.TryDecodeInput(data, out _) || GameplayWireCodec.TryDecodeAction(data, out _) || GameplayWireCodec.TryDecodeSnapshot(data, out _) || GameplayWireCodec.TryDecodePrivate(data, out _) || GameplayWireCodec.TryDecodeEvent(data, out _);
}
