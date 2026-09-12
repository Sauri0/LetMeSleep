using System;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;

namespace LetMeSleep.Online
{
    /// <summary>Connects authenticated EOS members with host-owned room rules.</summary>
    public sealed class OnlineRoomCoordinator : IDisposable
    {
        private const byte Hello = 1, View = 2, Ready = 3, Rejected = 4;
        private readonly EosConnection connection;
        private readonly EosLobbySession lobby;
        private readonly EosPeerTransport transport;
        private readonly MessageFraming frames = new MessageFraming();
        private readonly string playerName;
        private RoomSession hostRoom;
        private double clock, lastHello = -10;
        private bool disposed;
        public RoomView Current { get; private set; }
        public RoomSession HostAuthority => hostRoom;
        public string Error { get; private set; } = "";
        public event Action<RoomView> RoomChanged;

        public OnlineRoomCoordinator(EosConnection connection, EosLobbySession lobby, EosPeerTransport transport, string playerName)
        {
            this.connection = connection; this.lobby = lobby; this.transport = transport; this.playerName = playerName;
            transport.PacketReceived += ReceivePacket;
            frames.MessageReceived += ReceiveMessage;
            lobby.Changed += MembershipChanged;
            MembershipChanged();
        }
        public void Tick(double monotonicSeconds)
        {
            clock = monotonicSeconds;
            if (disposed || lobby.State != LobbyState.Connected || lobby.IsOwner || Current != null) return;
            if (clock - lastHello < 1) return;
            lastHello = clock;
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            RoomWireCodec.WriteText(writer, RoomSession.Protocol, 64); RoomWireCodec.WriteText(writer, playerName, 96);
            Send(lobby.OwnerId, Hello, stream.ToArray());
        }
        public RoomError SetReady(bool ready)
        {
            if (Current == null) return RoomError.WrongPhase;
            Error = "";
            if (lobby.IsOwner)
            {
                var result = hostRoom.SetReady(connection.LocalUserId.ToString(), ready); Publish(); return result;
            }
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(ready); writer.Write(Current.Revision); writer.Write(Current.Rules.HumanCount ?? 0); writer.Write(Current.Rules.RoundSeconds); writer.Write(Current.Rules.BloodQuota);
            Send(lobby.OwnerId, Ready, stream.ToArray()); return RoomError.None;
        }
        public RoomError SetRules(RoomRules rules)
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.ChangeRules(connection.LocalUserId.ToString(), rules); Publish(); return error;
        }
        public RoomError StartRound()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.StartRound(connection.LocalUserId.ToString()); Publish(); return error;
        }
        public RoomError FinishRound()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.FinishRound(connection.LocalUserId.ToString()); Publish(); return error;
        }
        public RoomError ReturnToLobby()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.ReturnToWaiting(connection.LocalUserId.ToString()); Publish(); return error;
        }
        private void MembershipChanged()
        {
            if (disposed) return;
            if (lobby.State != LobbyState.Connected)
            {
                bool hadView = Current != null;
                Current = null; hostRoom = null; Error = ""; frames.Clear(); lastHello = -10;
                if (hadView) RoomChanged?.Invoke(null);
                return;
            }
            if (lobby.IsOwner && hostRoom == null)
                hostRoom = new RoomSession(connection.LocalUserId.ToString(), playerName, new SeededRandom(Guid.NewGuid().GetHashCode()));
            if (hostRoom != null)
            {
                foreach (var member in hostRoom.Snapshot().Members)
                    if (!lobby.Contains(member.Id)) { hostRoom.Leave(member.Id); frames.Forget(member.Id); }
                Publish();
            }
        }
        private void ReceivePacket(string peer, byte channel, ArraySegment<byte> packet)
        {
            if (!disposed && channel == 0) frames.Accept(peer, packet, clock);
        }
        private void ReceiveMessage(string peer, byte kind, byte[] payload)
        {
            if (disposed || !lobby.Contains(peer)) return;
            if (kind == View && !lobby.IsOwner && peer == lobby.OwnerId)
            {
                if (RoomWireCodec.TryDecode(payload, peer, out var view) && (Current == null || view.Revision > Current.Revision) && view.Members.Any(m => m.Id == connection.LocalUserId.ToString()))
                { Current = view; Error = ""; RoomChanged?.Invoke(view); }
                return;
            }
            if (kind == Rejected && peer == lobby.OwnerId && payload.Length == 1 && payload[0] <= (byte)RoomError.NotReady) { Error = ((RoomError)payload[0]).ToString(); return; }
            if (hostRoom == null) return;
            try
            {
                using var stream = new MemoryStream(payload, false); using var reader = new BinaryReader(stream, Encoding.UTF8);
                RoomError result;
                if (kind == Hello)
                {
                    string protocol = RoomWireCodec.ReadText(reader, 64), name = RoomWireCodec.ReadText(reader, 96);
                    if (stream.Position != stream.Length) return;
                    result = hostRoom.Join(peer, name, protocol);
                    if (result == RoomError.DuplicateMember) result = RoomError.None;
                }
                else if (kind == Ready)
                {
                    byte ready = reader.ReadByte(); long revision = reader.ReadInt64(); int count = reader.ReadInt32(), seconds = reader.ReadInt32(); float quota = reader.ReadSingle();
                    if (stream.Position != stream.Length || ready > 1) return;
                    var snapshot = hostRoom.Snapshot(); var rules = snapshot.Rules;
                    result = revision != snapshot.Revision || count != (rules.HumanCount ?? 0) || seconds != rules.RoundSeconds || quota != rules.BloodQuota
                        ? RoomError.InvalidRules : hostRoom.SetReady(peer, ready == 1);
                }
                else return;
                if (result != RoomError.None) Send(peer, Rejected, new[] { (byte)result });
                Publish();
            }
            catch (IOException) { }
            catch (InvalidDataException) { }
            catch (DecoderFallbackException) { }
        }
        private void Publish()
        {
            if (hostRoom == null || lobby.State != LobbyState.Connected) return;
            Current = hostRoom.Snapshot();
            byte[] data = RoomWireCodec.Encode(Current);
            foreach (var member in Current.Members)
                if (member.Id != connection.LocalUserId.ToString() && lobby.Contains(member.Id)) Send(member.Id, View, data);
            RoomChanged?.Invoke(Current);
        }
        private void Send(string peer, byte kind, byte[] data)
        {
            foreach (var packet in frames.Encode(kind, data)) transport.Send(peer, 0, new ArraySegment<byte>(packet), true);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; transport.PacketReceived -= ReceivePacket; lobby.Changed -= MembershipChanged;
            frames.MessageReceived -= ReceiveMessage; frames.Clear(); RoomChanged = null;
        }
    }
}
