using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;

namespace LetMeSleep.Online
{
    /// <summary>Round barrier and authenticated gameplay packets over EOS. Owns no simulation.</summary>
    public sealed class OnlineGameplaySession : IDisposable
    {
        private const byte Begin = 20, Ack = 21, Input = 22, Action = 23, Snapshot = 24, Private = 25, Event = 26;
        private readonly EosLobbySession lobby;
        private readonly OnlineRoomCoordinator room;
        private readonly EosPeerTransport transport;
        private readonly IGameplayCommandSink authority;
        private readonly IGameplayPresentationSink replica;
        private readonly Func<IReadOnlyList<DoorDefinition>> doors;
        private readonly Func<IReadOnlyList<ToolPickupDefinition>> tools;
        private readonly MessageFraming framing = new MessageFraming();
        private readonly HashSet<string> waiting = new HashSet<string>(StringComparer.Ordinal);
        private readonly string localId, contentHash;
        private SpawnActor[] roster = Array.Empty<SpawnActor>();
        private GameplayRoundConfig config;
        private byte[] beginPacket;
        private double now, barrierStart, retryAt, lastOwnerPacket;
        private bool disposed, failed, clockStarted, receivedSnapshot;
        public bool Ready => config != null && !failed && room.Current?.Phase == RoomPhase.Playing
            && config.RoundId == (ulong)room.Current.Round && (!lobby.IsOwner || waiting.Count == 0);
        public event Action<GameplayRoundConfig, IReadOnlyList<SpawnActor>> BeginReceived;
        public event Action<string> Failed;

        public OnlineGameplaySession(EosLobbySession lobby, OnlineRoomCoordinator room, EosPeerTransport transport,
            string localId, string contentHash, IGameplayCommandSink authority, IGameplayPresentationSink replica,
            Func<IReadOnlyList<DoorDefinition>> doors, Func<IReadOnlyList<ToolPickupDefinition>> tools)
        {
            this.lobby = lobby; this.room = room; this.transport = transport; this.localId = localId;
            this.contentHash = contentHash; this.authority = authority; this.replica = replica; this.doors = doors; this.tools = tools;
            transport.PacketReceived += ReceivePacket; framing.MessageReceived += ReceiveMessage;
        }
        public void StartHost(GameplayRoundConfig round, IReadOnlyList<SpawnActor> actors)
        {
            if (round == null || !lobby.IsOwner || room.Current?.Phase != RoomPhase.Playing || round.RoundId != (ulong)room.Current.Round)
                throw new InvalidOperationException("Only the active room owner can start gameplay.");
            if (actors == null || actors.Count != room.Current.Members.Count || actors.Count < 2 || actors.Count > RoomRules.Capacity
                || actors.Select(a => a.ActorId).Distinct().Count() != actors.Count || actors.Select(a => a.OwnerPuid).Distinct().Count() != actors.Count
                || actors.Any(a => a.ActorId == 0 || a.IsBot || !a.Position.IsFinite || !room.Current.Members.Any(m => m.Id == a.OwnerPuid && m.Role == a.Role)))
                throw new ArgumentException("Round roster does not match authenticated room members.");
            if (round.MapId != room.Current.Rules.MapId || round.ContentHash != contentHash
                || round.RoundDurationTicks != room.Current.Rules.RoundSeconds * 30 || round.BloodGoal != room.Current.Rules.BloodQuota)
                throw new ArgumentException("Round settings do not match room settings.");
            config = round; roster = actors.ToArray(); failed = false; waiting.Clear(); framing.Clear();
            foreach (var member in room.Current.Members) if (member.Id != localId) waiting.Add(member.Id);
            beginPacket = EncodeBegin(round, roster); barrierStart = now; retryAt = now + 1;
            foreach (var member in waiting) Send(member, Begin, beginPacket, true);
        }
        public void Tick(double time)
        {
            now = time;
            if (!clockStarted) { clockStarted = true; barrierStart = now; retryAt = now + 1; lastOwnerPacket = now; }
            if (disposed || failed) return;
            if (lobby.State != LobbyState.Connected) { Fail("RoomClosed"); return; }
            if (config == null)
            {
                if (!lobby.IsOwner && room.Current?.Phase == RoomPhase.Playing && now - barrierStart > 35)
                    Fail("HostBeginTimedOut");
                return;
            }
            if (room.Current?.Phase != RoomPhase.Playing || config.RoundId != (ulong)room.Current.Round) return;
            if (lobby.IsOwner && waiting.Count > 0)
            {
                waiting.RemoveWhere(id => !lobby.Contains(id));
                if (now - barrierStart > 25) { Fail("PlayerLoadingTimedOut"); return; }
                if (now >= retryAt) { retryAt = now + 1; foreach (var id in waiting) Send(id, Begin, beginPacket, true); }
            }
            if (!lobby.IsOwner && now - lastOwnerPacket > (receivedSnapshot ? 15 : 35)) Fail("HostConnectionTimedOut");
        }
        public void SendInput(PlayerInputCommand input) { if (Ready && !lobby.IsOwner) Send(lobby.OwnerId, Input, GameplayWireCodec.Encode(input), false); }
        public void SendAction(PlayerActionCommand action) { if (Ready && !lobby.IsOwner) Send(lobby.OwnerId, Action, GameplayWireCodec.Encode(action), true); }
        public void SendSnapshot(GameSessionState snapshot)
        {
            if (Ready && lobby.IsOwner) Broadcast(Snapshot, GameplayWireCodec.Encode(snapshot), snapshot.SimulationPhase == SimulationPhase.Ended);
        }
        public void SendPrivate(string owner, ActorPrivateState state)
        {
            if (Ready && lobby.IsOwner && owner != localId && roster.Any(a => a.OwnerPuid == owner && a.ActorId == state.ActorId))
                Send(owner, Private, GameplayWireCodec.Encode(state), false);
        }
        public void SendEvent(GameplayEvent item) { if (Ready && lobby.IsOwner) Broadcast(Event, GameplayWireCodec.Encode(item), true); }
        private void Broadcast(byte kind, byte[] data, bool reliable)
        {
            foreach (var member in room.Current.Members) if (member.Id != localId && lobby.Contains(member.Id)) Send(member.Id, kind, data, reliable);
        }
        private void Send(string peer, byte kind, byte[] data, bool reliable)
        {
            foreach (var packet in framing.Encode(kind, data)) transport.Send(peer, 1, new ArraySegment<byte>(packet), reliable);
        }
        private void ReceivePacket(string peer, byte channel, ArraySegment<byte> packet)
        {
            if (!disposed && channel == 1 && room.Current != null && room.Current.Members.Any(m => m.Id == peer)) framing.Accept(peer, packet, now);
        }
        private void ReceiveMessage(string peer, byte kind, byte[] packet)
        {
            if (disposed || lobby.State != LobbyState.Connected || !lobby.Contains(peer)) return;
            if (kind == Begin && !lobby.IsOwner && peer == lobby.OwnerId)
            {
                if (!TryBegin(packet, out var next, out var nextRoster)) return;
                if (config == null || config.RoundId < next.RoundId)
                {
                    config = next; roster = nextRoster; lastOwnerPacket = now; failed = false;
                    BeginReceived?.Invoke(config, roster);
                }
                if (failed || config.RoundId != next.RoundId || config.SessionEpoch != next.SessionEpoch) return;
                Send(peer, Ack, RoundIdentity(config), true); return;
            }
            if (config == null || failed) return;
            if (lobby.IsOwner)
            {
                if (kind == Ack && packet.SequenceEqual(RoundIdentity(config))) { waiting.Remove(peer); return; }
                if (!Ready || !roster.Any(a => a.OwnerPuid == peer)) return;
                if (kind == Input && GameplayWireCodec.TryDecode(packet, out PlayerInputCommand input)) authority.SubmitInput(peer, input);
                else if (kind == Action && GameplayWireCodec.TryDecode(packet, out PlayerActionCommand action)) authority.SubmitAction(peer, action);
                return;
            }
            if (peer != lobby.OwnerId) return;
            if (kind == Snapshot && GameplayWireCodec.TryDecode(packet, out GameSessionState snapshot))
            {
                if (snapshot.SessionEpoch != config.SessionEpoch || snapshot.RoundId != config.RoundId) return;
                lastOwnerPacket = now; receivedSnapshot = true;
                replica.ApplySnapshot(snapshot, snapshot.HostTime);
            }
            else if (kind == Private && GameplayWireCodec.TryDecode(packet, out ActorPrivateState state)) replica.ApplyPrivate(state);
            else if (kind == Event && GameplayWireCodec.TryDecode(packet, out GameplayEvent item)) replica.ApplyEvent(item);
        }
        private static byte[] RoundIdentity(GameplayRoundConfig round)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(round.SessionEpoch); writer.Write(round.RoundId); return stream.ToArray();
        }
        private static byte[] EncodeBegin(GameplayRoundConfig round, SpawnActor[] actors)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((byte)2); writer.Write(round.SessionEpoch); writer.Write(round.RoundId);
            RoomWireCodec.WriteText(writer, round.MapId, 64); RoomWireCodec.WriteText(writer, round.ContentHash, 128);
            writer.Write((int)(round.RoundDurationTicks / 30)); writer.Write(round.BloodGoal);
            writer.Write(round.Balance.RecoveryBaseSeconds); writer.Write(round.Balance.FullExtractionSeconds);
            writer.Write(round.Balance.PreparationSeconds); writer.Write(round.Balance.HelpMultiplier); writer.Write(round.Balance.ProtectionSeconds);
            writer.Write((byte)round.ToolDefinitions.Count);
            foreach (var tool in round.ToolDefinitions)
            {
                writer.Write(tool.PickupId); RoomWireCodec.WriteText(writer, tool.ToolId, 32);
                writer.Write(tool.Position.X); writer.Write(tool.Position.Y); writer.Write(tool.Position.Z);
                writer.Write(tool.Rotation.X); writer.Write(tool.Rotation.Y); writer.Write(tool.Rotation.Z); writer.Write(tool.Rotation.W);
            }
            writer.Write((byte)actors.Length);
            foreach (var actor in actors)
            {
                writer.Write(actor.ActorId); RoomWireCodec.WriteText(writer, actor.OwnerPuid, 128); writer.Write((byte)actor.Role);
                writer.Write(actor.Position.X); writer.Write(actor.Position.Y); writer.Write(actor.Position.Z);
                RoomWireCodec.WriteText(writer, actor.SpawnId, 64); RoomWireCodec.WriteText(writer, actor.CosmeticProfileId, 128);
            }
            return stream.ToArray();
        }
        private bool TryBegin(byte[] packet, out GameplayRoundConfig result, out SpawnActor[] actors)
        {
            result = null; actors = null;
            if (room.Current?.Phase != RoomPhase.Playing || packet.Length > 8192) return false;
            try
            {
                using var stream = new MemoryStream(packet, false); using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadByte() != 2) return false;
                ulong epoch = reader.ReadUInt64(), round = reader.ReadUInt64();
                string map = RoomWireCodec.ReadText(reader, 64), hash = RoomWireCodec.ReadText(reader, 128);
                int seconds = reader.ReadInt32(); float goal = reader.ReadSingle();
                var balance = new BalanceProfile(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (epoch == 0 || round != (ulong)room.Current.Round || map != room.Current.Rules.MapId || hash != contentHash
                    || seconds != room.Current.Rules.RoundSeconds || goal != room.Current.Rules.BloodQuota) return false;
                var localTools = tools(); int toolCount = reader.ReadByte();
                if (toolCount > 32 || toolCount != localTools.Count) return false;
                var toolIds = new HashSet<uint>();
                for (int t = 0; t < toolCount; t++)
                {
                    uint id = reader.ReadUInt32(); string type = RoomWireCodec.ReadText(reader, 32);
                    var position = new Float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var rotation = new Rotation(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (!toolIds.Add(id) || !localTools.Any(d => d.PickupId == id && d.ToolId == type && SamePose(d, position, rotation))) return false;
                }
                int count = reader.ReadByte(); if (count < 2 || count > RoomRules.Capacity || count != room.Current.Members.Count) return false;
                var nextActors = new SpawnActor[count]; var ids = new HashSet<uint>(); var owners = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    uint id = reader.ReadUInt32(); string owner = RoomWireCodec.ReadText(reader, 128); var role = (PlayerRole)reader.ReadByte();
                    var position = new Float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    string spawn = RoomWireCodec.ReadText(reader, 64), cosmetic = RoomWireCodec.ReadText(reader, 128);
                    var member = room.Current.Members.FirstOrDefault(m => m.Id == owner);
                    if (id == 0 || !ids.Add(id) || !owners.Add(owner) || member == null || member.Role != role || !position.IsFinite || position.LengthSquared > 250000) return false;
                    nextActors[i] = new SpawnActor(id, owner, role, position, spawn, cosmetic);
                }
                if (stream.Position != stream.Length || !owners.Contains(localId)) return false;
                result = new GameplayRoundConfig(epoch, round, map, hash, seconds, goal, balance, doors(), tools: localTools); actors = nextActors; return true;
            }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (ArgumentException) { return false; }
        }
        private static bool SamePose(ToolPickupDefinition definition, Float3 position, Rotation rotation)
            => ToolDefinitionValidation.Matches(definition,new ToolPickupDefinition(definition.PickupId,definition.ToolId,position,rotation));
        private void Fail(string reason) { if (failed) return; failed = true; Failed?.Invoke(reason); }
        public void Dispose()
        {
            if (disposed) return; disposed = true; transport.PacketReceived -= ReceivePacket;
            framing.MessageReceived -= ReceiveMessage; framing.Clear(); waiting.Clear(); BeginReceived = null; Failed = null;
        }
    }
}




