using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;

namespace LetMeSleep.Online
{
    /// <summary>Bounded binary DTO codec. Transport authenticates the sender; no owner/PUID is accepted in payloads.</summary>
    public static class GameplayWireCodec
    {
        public const int MaxMessageBytes = 16384, MaxActors = 16, MaxDoors = 128, MaxToolPickups = 32;
        public const ushort Version = 2;
        private const uint Magic = 0x314D534C;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private enum Kind : byte { Input = 1, Action, Snapshot, Private, Event }

        public static byte[] Encode(PlayerInputCommand value) => Pack(Kind.Input, w => WriteInput(w, value));
        public static byte[] Encode(PlayerActionCommand value) => Pack(Kind.Action, w => WriteAction(w, value));
        public static byte[] Encode(GameSessionState value) => Pack(Kind.Snapshot, w => WriteSnapshot(w, value));
        public static byte[] Encode(ActorPrivateState value) => Pack(Kind.Private, w => WritePrivate(w, value));
        public static byte[] Encode(GameplayEvent value) => Pack(Kind.Event, w => WriteEvent(w, value));
        public static bool TryDecode(byte[] data, out PlayerInputCommand value) => Unpack(data, Kind.Input, ReadInput, out value);
        public static bool TryDecode(byte[] data, out PlayerActionCommand value) => Unpack(data, Kind.Action, ReadAction, out value);
        public static bool TryDecode(byte[] data, out GameSessionState value) => Unpack(data, Kind.Snapshot, ReadSnapshot, out value);
        public static bool TryDecode(byte[] data, out ActorPrivateState value) => Unpack(data, Kind.Private, ReadPrivate, out value);
        public static bool TryDecode(byte[] data, out GameplayEvent value) => Unpack(data, Kind.Event, ReadEvent, out value);
        public static bool TryDecodeInput(byte[] data, out PlayerInputCommand value) => TryDecode(data, out value);
        public static bool TryDecodeAction(byte[] data, out PlayerActionCommand value) => TryDecode(data, out value);
        public static bool TryDecodeSnapshot(byte[] data, out GameSessionState value) => TryDecode(data, out value);
        public static bool TryDecodePrivate(byte[] data, out ActorPrivateState value) => TryDecode(data, out value);
        public static bool TryDecodeEvent(byte[] data, out GameplayEvent value) => TryDecode(data, out value);

        private static byte[] Pack(Kind kind, Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream(1024))
            using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                writer.Write(Magic); writer.Write(Version); writer.Write((byte)kind); write(writer); writer.Flush();
                Require(stream.Length <= MaxMessageBytes, "Message too large.");
                var bytes = stream.ToArray();
                // Run the same structural/semantic validation used at ingress. Invalid local DTOs never go on wire.
                bool valid;
                switch (kind)
                {
                    case Kind.Input: valid = TryDecodeInput(bytes, out _); break;
                    case Kind.Action: valid = TryDecodeAction(bytes, out _); break;
                    case Kind.Snapshot: valid = TryDecodeSnapshot(bytes, out _); break;
                    case Kind.Private: valid = TryDecodePrivate(bytes, out _); break;
                    default: valid = TryDecodeEvent(bytes, out _); break;
                }
                Require(valid, "Invalid gameplay DTO."); return bytes;
            }
        }
        private static bool Unpack<T>(byte[] data, Kind kind, Func<BinaryReader, T> read, out T value)
        {
            value = default;
            if (data == null || data.Length < 7 || data.Length > MaxMessageBytes) return false;
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Utf8, true))
                {
                    if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version || reader.ReadByte() != (byte)kind) return false;
                    T parsed = read(reader); if (stream.Position != stream.Length) return false;
                    value = parsed; return true;
                }
            }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            catch (OverflowException) { return false; }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static float F(BinaryReader r, float min = -10000, float max = 10000)
        { float v = r.ReadSingle(); Require(MathEx.Finite(v) && v >= min && v <= max, "Invalid float."); return v; }
        private static uint Id(BinaryReader r) { uint v = r.ReadUInt32(); Require(v != 0, "Zero ID."); return v; }
        private static ulong Epoch(BinaryReader r) { ulong v = r.ReadUInt64(); Require(v != 0, "Zero epoch/round/event."); return v; }
        private static uint Tick(BinaryReader r) { uint v = r.ReadUInt32(); Require(v <= 54000, "Tick out of range."); return v; }
        private static bool B(BinaryReader r) { byte v = r.ReadByte(); Require(v <= 1, "Invalid boolean."); return v == 1; }
        private static T E<T>(BinaryReader r, int max) where T : struct
        { byte v = r.ReadByte(); Require(v <= max, "Invalid enum."); return (T)Enum.ToObject(typeof(T), v); }
        private static void V(BinaryWriter w, Float3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        private static Float3 V(BinaryReader r) => new Float3(F(r), F(r), F(r));
        private static Float3 Unit(BinaryReader r)
        { var v = V(r); Require(Math.Abs(v.LengthSquared - 1) <= .02f, "Non-unit direction."); return v; }
        private static void Q(BinaryWriter w, Rotation q) { w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W); }
        private static Rotation Q(BinaryReader r)
        { var q = new Rotation(F(r, -1, 1), F(r, -1, 1), F(r, -1, 1), F(r, -1, 1)); Require(Math.Abs(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W - 1) <= .002f, "Invalid rotation."); return q; }
        private static void S(BinaryWriter w, string value, int maxBytes)
        {
            Require(!string.IsNullOrWhiteSpace(value) && value.Length <= maxBytes && !value.Any(char.IsControl), "Invalid string.");
            byte[] bytes = Utf8.GetBytes(value); Require(bytes.Length <= maxBytes, "String too long."); w.Write((ushort)bytes.Length); w.Write(bytes);
        }
        private static string S(BinaryReader r, int maxBytes)
        {
            int length = r.ReadUInt16(); Require(length > 0 && length <= maxBytes && r.BaseStream.Length - r.BaseStream.Position >= length, "Invalid string length.");
            string value = Utf8.GetString(r.ReadBytes(length)); Require(!string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl), "Invalid string."); return value;
        }
        private static void Header(BinaryWriter w, CommandHeader h)
        { w.Write(h.SessionEpoch); w.Write(h.RoundId); w.Write(h.ActorId); w.Write(h.Sequence); w.Write(h.ClientTick); w.Write(h.ViewRevision); }
        private static CommandHeader Header(BinaryReader r) => new CommandHeader(Epoch(r), Epoch(r), Id(r), r.ReadUInt32(), Tick(r), Id(r));
        private static void WriteInput(BinaryWriter w, PlayerInputCommand c)
        {
            Header(w, c.Header); w.Write(c.MovePlanar.X); w.Write(c.MovePlanar.Y); w.Write(c.Vertical); w.Write(c.ViewYawRadians); w.Write(c.ViewPitchRadians); V(w, c.AimForward);
            w.Write(c.SprintHeld); w.Write(c.CrouchHeld); w.Write(c.BiteHeld); w.Write(c.UseHeld);
        }
        private static PlayerInputCommand ReadInput(BinaryReader r)
        {
            var h = Header(r); var move = new Float2(F(r, -1, 1), F(r, -1, 1)); float vertical = F(r, -1, 1), yaw = F(r), pitch = F(r, -1.919863f, 1.553344f); var aim = Unit(r);
            Require(Float3.Dot(MathEx.Aim(yaw, pitch), aim.Normalized) >= .99984f, "Aim mismatch.");
            return new PlayerInputCommand(h, move, vertical, yaw, pitch, aim, B(r), B(r), B(r), B(r));
        }
        private static void WriteAction(BinaryWriter w, PlayerActionCommand c) { Header(w, c.Header); w.Write((byte)c.Kind); V(w, c.AimForward); }
        private static PlayerActionCommand ReadAction(BinaryReader r) => new PlayerActionCommand(Header(r), E<ActionKind>(r, 5), Unit(r));

        private static void WriteSnapshot(BinaryWriter w, GameSessionState s)
        {
            Require(s != null && s.Actors.Count <= MaxActors && s.Doors.Count <= MaxDoors && s.ToolPickups.Count <= MaxToolPickups, "Invalid snapshot counts.");
            w.Write(s.SessionEpoch); w.Write(s.RoundId); w.Write(s.HostTick); S(w, s.MapId, 128); S(w, s.ContentHash, 128); S(w, s.BalanceHash, 256);
            w.Write((byte)s.SimulationPhase); w.Write(s.TimeRemainingTicks); w.Write(s.BloodCollected); w.Write(s.BloodGoal); w.Write((byte)s.Result); w.Write((byte)s.Winner);
            w.Write((byte)s.Actors.Count); foreach (var actor in s.Actors) WriteActor(w, actor);
            w.Write((ushort)s.Doors.Count); foreach (var door in s.Doors) WriteDoor(w, door);
            w.Write((byte)s.ToolPickups.Count); foreach (var pickup in s.ToolPickups) WritePickup(w, pickup);
        }
        private static GameSessionState ReadSnapshot(BinaryReader r)
        {
            ulong epoch = Epoch(r), round = Epoch(r); uint tick = Tick(r); string map = S(r, 128), content = S(r, 128), balanceHash = S(r, 256);
            var phase = E<SimulationPhase>(r, 1); uint remaining = Tick(r); float blood = F(r, 0, 1000), goal = F(r, .000001f, 1000); var result = E<RoundEndReason>(r, 4); var winner = E<PlayerRole>(r, 2);
            uint total = tick + remaining;
            Require(total >= 900 && total <= 54000 && total % 30 == 0 && blood <= goal, "Invalid round progress.");
            Require(phase == SimulationPhase.Running ? result == RoundEndReason.None && winner == PlayerRole.Unassigned : result != RoundEndReason.None, "Inconsistent result.");
            var balance = ParseBalance(balanceHash);
            int count = r.ReadByte(); Require(count <= MaxActors, "Too many actors.");
            var actors = new ActorSnapshot[count]; var ids = new HashSet<uint>();
            for (int i = 0; i < count; i++) { actors[i] = ReadActor(r, tick); Require(ids.Add(actors[i].ActorId), "Duplicate actor."); }
            if (phase == SimulationPhase.Running) Require(actors.Count(a => a.Role == PlayerRole.Human) >= 1 && actors.Count(a => a.Role == PlayerRole.Human) <= 5 && actors.Any(a => a.Role == PlayerRole.Mosquito), "Missing team.");
            foreach (var actor in actors)
                if (actor.BiteAttachment.HasValue) Require(actor.BiteAttachment.Value.VictimId != actor.ActorId && actors.Any(a => a.ActorId == actor.BiteAttachment.Value.VictimId && a.Role == PlayerRole.Human), "Invalid bite victim.");
            int doorCount = r.ReadUInt16(); Require(doorCount <= MaxDoors, "Too many doors."); var doors = new DoorSnapshot[doorCount]; ids.Clear(); var surfaceIds = new HashSet<uint>();
            for (int i = 0; i < doorCount; i++) { doors[i] = ReadDoor(r, tick); Require(ids.Add(doors[i].DoorId) && surfaceIds.Add(doors[i].SurfaceId), "Duplicate door/surface."); }
            int pickupCount = r.ReadByte(); Require(pickupCount <= MaxToolPickups, "Too many pickups."); var pickups = new ToolPickupSnapshot[pickupCount]; ids.Clear(); var owners = new HashSet<uint>();
            for (int i = 0; i < pickupCount; i++)
            {
                pickups[i] = ReadPickup(r); Require(ids.Add(pickups[i].PickupId), "Duplicate pickup.");
                if (pickups[i].OwnerActorId != 0) Require(owners.Add(pickups[i].OwnerActorId) && actors.Any(a => a.ActorId == pickups[i].OwnerActorId && a.Role == PlayerRole.Human && a.EquippedToolId == pickups[i].ToolId), "Invalid pickup owner.");
            }
            foreach (var actor in actors) Require(actor.EquippedToolId == GameplayTools.Hands ? !owners.Contains(actor.ActorId) : owners.Contains(actor.ActorId), "Equipment ownership mismatch.");
            var config = new GameplayRoundConfig(epoch, round, map, content, (int)(total / 30), goal, balance);
            Require(config.BalanceHash == balanceHash, "Noncanonical balance.");
            return new GameSessionState(config, tick, phase, blood, result, winner, actors, doors, pickups);
        }
        private static BalanceProfile ParseBalance(string hash)
        {
            var fields = hash.Split(':'); Require(fields.Length == 6 && fields[0] == BalanceProfile.Id, "Unsupported balance version.");
            var values = new float[5];
            for (int i = 0; i < 5; i++) Require(float.TryParse(fields[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) && MathEx.Finite(values[i]), "Invalid balance value.");
            return new BalanceProfile(values[0], values[1], values[2], values[3], values[4]);
        }
        private static void WriteActor(BinaryWriter w, ActorSnapshot a)
        {
            Require(a != null, "Null actor.");
            w.Write(a.ActorId); w.Write((byte)a.Role); w.Write((byte)a.LifeState); w.Write(a.StateRevision); V(w, a.Position); V(w, a.Velocity); Q(w, a.BodyRotation); V(w, a.ViewForward); w.Write(a.ViewYawRadians); w.Write(a.ViewPitchRadians);
            w.Write(a.ViewRevision); w.Write(a.PoseRevision); w.Write(a.Grounded); w.Write(a.CrouchFraction); w.Write(a.MotionPhase); w.Write(a.RecoveryEndTick);
            w.Write(a.SurfaceAttachment.HasValue); if (a.SurfaceAttachment.HasValue) WriteSurface(w, a.SurfaceAttachment.Value);
            w.Write(a.BiteAttachment.HasValue); if (a.BiteAttachment.HasValue) WriteBite(w, a.BiteAttachment.Value);
            WriteStrike(w, a.StrikeState);
            S(w, a.EquippedToolId, 24);
        }
        private static ActorSnapshot ReadActor(BinaryReader r, uint hostTick)
        {
            uint id = Id(r); var role = E<PlayerRole>(r, 2); Require(role != PlayerRole.Unassigned, "Unassigned actor."); var life = E<LifeState>(r, 9); uint revision = Id(r);
            var position = V(r); var velocity = V(r); Require(velocity.Length <= 200, "Invalid velocity."); var body = Q(r); var view = Unit(r); float yaw = F(r), pitch = F(r, -1.919863f, 1.553344f);
            Require(Float3.Dot(MathEx.Aim(yaw, pitch), view.Normalized) >= .99984f && (role != PlayerRole.Human || pitch <= 1.308997f), "View mismatch.");
            uint viewRevision = Id(r), poseRevision = r.ReadUInt32(); bool grounded = B(r); float crouch = F(r, 0, 1), motion = F(r, 0, 1000000); uint recovery = r.ReadUInt32(); Require(recovery <= hostTick + 4000, "Invalid recovery deadline.");
            SurfaceAttachment? surface = B(r) ? ReadSurface(r) : (SurfaceAttachment?)null;
            BiteAttachment? bite = B(r) ? ReadBite(r) : (BiteAttachment?)null;
            var strike = ReadStrike(r, hostTick);
            string equipped = S(r, 24); Require(equipped == GameplayTools.Hands || (role == PlayerRole.Human && equipped == GameplayTools.Flyswatter), "Invalid equipment.");
            if (strike.Phase != StrikePhase.None) Require(strike.ToolId == equipped || (GameplayTools.IsFlyswatter(strike.ToolId) && equipped == GameplayTools.Flyswatter), "Strike/equipment mismatch.");
            if (role == PlayerRole.Human) Require((life == LifeState.Active || life == LifeState.Falling || life == LifeState.Fainted || life == LifeState.Recovering) && !surface.HasValue && !bite.HasValue, "Human state mismatch.");
            else Require(life != LifeState.Active && life != LifeState.Fainted && strike.Phase == StrikePhase.None, "Mosquito state mismatch.");
            if (surface.HasValue) Require(life == LifeState.Surface || life == LifeState.ApproachingSurface, "Surface state mismatch.");
            if (bite.HasValue) Require(life == LifeState.Biting || life == LifeState.PreparingBite, "Bite state mismatch.");
            return new ActorSnapshot(id, role, life, revision, position, velocity, body, view, yaw, pitch, viewRevision, poseRevision, grounded, crouch, motion, surface, bite, strike, recovery, equipped);
        }
        private static void WritePickup(BinaryWriter w, ToolPickupSnapshot p)
        { w.Write(p.PickupId); S(w, p.ToolId, 24); V(w, p.Position); Q(w, p.Rotation); w.Write(p.OwnerActorId); w.Write(p.Revision); }
        private static ToolPickupSnapshot ReadPickup(BinaryReader r)
        {
            uint id = Id(r); string tool = S(r, 24); Require(tool == GameplayTools.Flyswatter, "Unknown pickup tool.");
            var position = V(r); var rotation = Q(r); uint owner = r.ReadUInt32(), revision = Id(r);
            return new ToolPickupSnapshot(id, tool, position, rotation, owner, revision);
        }
        private static void WriteSurface(BinaryWriter w, SurfaceAttachment a) { w.Write(a.SurfaceId); w.Write(a.Revision); V(w, a.LocalPoint); V(w, a.LocalNormal); V(w, a.TangentForward); }
        private static SurfaceAttachment ReadSurface(BinaryReader r) => new SurfaceAttachment(Id(r), Id(r), V(r), Unit(r), Unit(r));
        private static void WriteBite(BinaryWriter w, BiteAttachment a) { w.Write(a.VictimId); w.Write(a.SurfaceId); V(w, a.LocalPoint); V(w, a.LocalNormal); w.Write(a.PoseRevision); }
        private static BiteAttachment ReadBite(BinaryReader r) => new BiteAttachment(Id(r), Id(r), V(r), Unit(r), r.ReadUInt32());
        private static void WriteStrike(BinaryWriter w, StrikeState s)
        {
            w.Write((byte)s.Phase); if (s.Phase == StrikePhase.None) return;
            w.Write(s.StrikeId); S(w, s.ToolId, 24); w.Write((sbyte)s.Hand); w.Write(s.StartTick); V(w, s.Origin); V(w, s.Target); V(w, s.Normal); w.Write(s.Progress);
        }
        private static StrikeState ReadStrike(BinaryReader r, uint tick)
        {
            var phase = E<StrikePhase>(r, 3); if (phase == StrikePhase.None) return default;
            ulong id = Epoch(r); string tool = S(r, 24); Require(tool == GameplayTools.Hands || GameplayTools.IsFlyswatter(tool), "Unknown tool.");
            int hand = r.ReadSByte(); Require(hand == -1 || hand == 1, "Invalid hand."); uint start = Tick(r); Require(start <= tick, "Future strike.");
            return new StrikeState(id, tool, hand, phase, start, V(r), V(r), Unit(r), F(r, 0, 1));
        }
        private static void WriteDoor(BinaryWriter w, DoorSnapshot d)
        { w.Write(d.DoorId); w.Write(d.SurfaceId); w.Write(d.Revision); w.Write(d.AngleRadians); w.Write(d.TargetAngleRadians); w.Write(d.AngularVelocity); w.Write(d.Moving); w.Write(d.Blocked); w.Write(d.LastChangedTick); }
        private static DoorSnapshot ReadDoor(BinaryReader r, uint hostTick)
        {
            uint id = Id(r), surface = Id(r), revision = Id(r); float angle = F(r, 0, (float)Math.PI), target = F(r, 0, (float)Math.PI), velocity = F(r, -2.501f, 2.501f); bool moving = B(r), blocked = B(r); uint changed = Tick(r); Require(changed <= hostTick, "Future door revision.");
            return new DoorSnapshot(id, surface, revision, angle, target, velocity, moving, blocked, changed);
        }
        private static void WritePrivate(BinaryWriter w, ActorPrivateState p)
        {
            Require(p != null, "Null private state."); w.Write(p.SessionEpoch); w.Write(p.RoundId); w.Write(p.HostTick); w.Write(p.ActorId); w.Write(p.LastAcceptedInputSequence); w.Write(p.LastAcceptedActionSequence); w.Write((byte)p.Rejection); w.Write((byte)p.InteractionHint); w.Write(p.PreparationProgress); w.Write(p.ExtractionProgress); w.Write(p.RecoverySeconds); w.Write(p.HelpTargetId); w.Write(p.CanAct); w.Write((byte)p.LastDoorResult);
        }
        private static ActorPrivateState ReadPrivate(BinaryReader r)
        {
            ulong epoch = Epoch(r), round = Epoch(r); uint tick = Tick(r), actor = Id(r), input = r.ReadUInt32(), action = r.ReadUInt32(); var reject = E<CommandReject>(r, 13); var hint = E<InteractionHint>(r, 9);
            float preparation = F(r, 0, 1), extraction = F(r, 0, 1.001f), recovery = F(r, 0, 120); uint help = r.ReadUInt32(); Require(help != actor, "Self help."); bool canAct = B(r); var door = E<DoorUseResult>(r, 8);
            return new ActorPrivateState(actor, input, action, reject, hint, preparation, extraction, recovery, help, canAct, door, epoch, round, tick);
        }
        private static void WriteEvent(BinaryWriter w, GameplayEvent e)
        {
            w.Write(e.SessionEpoch); w.Write(e.RoundId); w.Write(e.EventId); w.Write(e.HostTick); w.Write((byte)e.Kind); w.Write(e.SourceActorId); w.Write(e.TargetActorId); w.Write(e.StateRevision); V(w, e.Position); V(w, e.Normal); w.Write((byte)e.Reason); w.Write(e.Door.HasValue); if (e.Door.HasValue) WriteDoor(w, e.Door.Value);
        }
        private static GameplayEvent ReadEvent(BinaryReader r)
        {
            ulong epoch = Epoch(r), round = Epoch(r), id = Epoch(r); uint tick = Tick(r); var kind = E<GameplayEventKind>(r, 11); uint source = r.ReadUInt32(), target = r.ReadUInt32(), revision = r.ReadUInt32(); var position = V(r); var normal = V(r); Require(normal.LengthSquared <= 1.02f, "Invalid event normal."); var reason = E<RoundEndReason>(r, 4);
            DoorSnapshot? door = B(r) ? ReadDoor(r, tick) : (DoorSnapshot?)null;
            Require((kind == GameplayEventKind.DoorChanged) == door.HasValue, "Door payload mismatch.");
            Require(kind == GameplayEventKind.RoundEnded ? reason != RoundEndReason.None : reason == RoundEndReason.None, "Event result mismatch.");
            if (kind != GameplayEventKind.DoorChanged && kind != GameplayEventKind.RoundEnded) Require(source != 0, "Missing event source.");
            return new GameplayEvent(epoch, round, id, tick, kind, source, target, revision, position, normal, door, reason);
        }
    }
}
