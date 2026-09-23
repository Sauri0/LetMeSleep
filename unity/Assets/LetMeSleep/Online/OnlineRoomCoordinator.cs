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
        /// <summary>A joined guest repeats its Hello this often; the host answers a known member with its current view.</summary>
        public const double ViewRefreshSeconds = 5;
        private readonly IRoomLobby lobby;
        private readonly IRoomLink transport;
        private readonly MessageFraming frames = new MessageFraming();
        private readonly RoomMessageLimiter limits = new RoomMessageLimiter();
        private readonly string playerName;
        private RoomSession hostRoom;
        private double clock, lastHello = -10, awaitingSince = -1;
        private bool disposed;
        public RoomView Current { get; private set; }
        public RoomSession HostAuthority => hostRoom;
        public string Error { get; private set; } = "";
        public event Action<RoomView> RoomChanged;
        public event Action<string> MemberReconnected;

        public OnlineRoomCoordinator(EosConnection connection, EosLobbySession lobby, EosPeerTransport transport, string playerName)
            : this((IRoomLobby)lobby, transport, playerName) { }
        public OnlineRoomCoordinator(IRoomLobby lobby, IRoomLink transport, string playerName)
        {
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.playerName = playerName;
            transport.PacketReceived += ReceivePacket;
            // EOS flushes queued reliable packets when a link closes: a re-established link resynchronizes the view.
            transport.PeerStateChanged += PeerRouteChanged;
            frames.MessageReceived += ReceiveMessage;
            lobby.Changed += MembershipChanged;
            MembershipChanged();
        }
        private string LocalId => lobby.LocalMemberId;
        public void Tick(double monotonicSeconds)
        {
            clock = monotonicSeconds;
            if (!disposed && hostRoom != null && lobby.State == LobbyState.Connected && lobby.IsOwner
                && hostRoom.ExpireReservations(clock)) Publish();
            if (disposed || lobby.State != LobbyState.Connected || lobby.IsOwner) return;
            if (Current != null)
            {
                // Slow resync: a view dropped without a link event (full send queue) is recovered within seconds.
                if (clock - lastHello >= ViewRefreshSeconds) SendHello();
                return;
            }
            if (awaitingSince < 0) awaitingSince = clock;
            if (clock - awaitingSince > 25) { Error = "RoomHandshakeTimedOut"; return; }
            if (clock - lastHello < 1) return;
            SendHello();
        }
        private void SendHello()
        {
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
                var result = hostRoom.SetReady(LocalId, ready); Publish(); return result;
            }
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(ready); writer.Write(Current.Revision); writer.Write(Current.Rules.HumanCount ?? 0); writer.Write(Current.Rules.RoundSeconds); writer.Write(Current.Rules.BloodQuota);
            Send(lobby.OwnerId, Ready, stream.ToArray()); return RoomError.None;
        }
        public RoomError SetRules(RoomRules rules)
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.ChangeRules(LocalId, rules); Publish(); return error;
        }
        public RoomError StartRound()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.StartRound(LocalId); Publish(); return error;
        }
        public RoomError FinishRound()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.FinishRound(LocalId); Publish(); return error;
        }
        public RoomError ReturnToLobby()
        {
            if (hostRoom == null || !lobby.IsOwner) return RoomError.NotOwner;
            var error = hostRoom.ReturnToWaiting(LocalId); Publish(); return error;
        }
        private void MembershipChanged()
        {
            if (disposed) return;
            if (lobby.State != LobbyState.Connected)
            {
                bool hadView = Current != null;
                Current = null; hostRoom = null; Error = ""; frames.Clear(); limits.Clear(); lastHello = -10; awaitingSince = -1;
                if (hadView) RoomChanged?.Invoke(null);
                return;
            }
            if (lobby.IsOwner && hostRoom == null)
                hostRoom = new RoomSession(LocalId, playerName, new SeededRandom(Guid.NewGuid().GetHashCode()));
            if (hostRoom != null)
            {
                foreach (var member in hostRoom.Snapshot().Members)
                    if (!lobby.Contains(member.Id)) { hostRoom.Disconnect(member.Id, clock); frames.Forget(member.Id); limits.Forget(member.Id); }
                Publish();
            }
        }
        // A (re)established P2P link has no queued room traffic: EOS flushes reliable packets on close, so a view
        // published while the link was down (for example the switch to Playing) is lost. Both sides resynchronize:
        // the host sends its current view, and a guest asks for it with a Hello (a known member just gets the view back).
        private void PeerRouteChanged(string peer, string state)
        {
            if (disposed || lobby.State != LobbyState.Connected || string.IsNullOrEmpty(peer) || state == null
                || state.StartsWith("Closed:", StringComparison.Ordinal) || !lobby.Contains(peer)) return;
            if (lobby.IsOwner)
            {
                if (hostRoom != null && Current != null && peer != LocalId && Current.Members.Any(member => member.Id == peer))
                    Send(peer, View, RoomWireCodec.Encode(Current));
            }
            else if (peer == lobby.OwnerId)
            {
                if (Current == null) lastHello = -10; // The waiting handshake retries on the next Tick.
                else SendHello();
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
                if (RoomWireCodec.TryDecode(payload, peer, out var view) && (Current == null || view.Revision > Current.Revision) && view.Members.Any(m => m.Id == LocalId))
                { Current = view; Error = ""; RoomChanged?.Invoke(view); }
                return;
            }
            if (kind == Rejected && peer == lobby.OwnerId && payload.Length == 1 && payload[0] <= (byte)RoomError.NotReady) { Reject((RoomError)payload[0]); return; }
            if (hostRoom == null || (kind != Hello && kind != Ready)) return;
            // Bounded per member: each accepted message may cost a reliable view to every member.
            if (!limits.TryAccept(peer, clock)) return;
            try
            {
                long before = hostRoom.Snapshot().Revision;
                using var stream = new MemoryStream(payload, false); using var reader = new BinaryReader(stream, Encoding.UTF8);
                RoomError result;
                bool reconnected = false;
                if (kind == Hello)
                {
                    string protocol = RoomWireCodec.ReadText(reader, 64), name = RoomWireCodec.ReadText(reader, 96);
                    if (stream.Position != stream.Length) return;
                    var existing = hostRoom.Snapshot().Members.FirstOrDefault(m => m.Id == peer);
                    result = existing != null && !existing.Connected
                        ? hostRoom.Reconnect(peer, name, protocol, clock) : hostRoom.Join(peer, name, protocol);
                    reconnected = result == RoomError.None && existing != null && !existing.Connected;
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
                // Broadcast only real changes; a duplicate Hello or stale Ready gets the current view back alone.
                if (hostRoom.Snapshot().Revision != before) Publish();
                else if (lobby.State == LobbyState.Connected && Current != null && Current.Members.Any(m => m.Id == peer)) Send(peer, View, RoomWireCodec.Encode(Current));
                if (reconnected) MemberReconnected?.Invoke(peer);
            }
            catch (IOException) { }
            catch (InvalidDataException) { }
            catch (DecoderFallbackException) { }
        }
        // A joined guest the host no longer knows (its reconnect reservation expired while the link was down, so a
        // periodic Hello or a Ready raced the expiry) is not an error for the player: the next Hello joins the room
        // again, or earns a real answer such as Full. Ask again soon instead of waiting for the slow resync, but
        // never faster than once per second whatever the host sends.
        private void Reject(RoomError error)
        {
            if (Current != null && error == RoomError.UnknownMember)
            {
                lastHello = Math.Min(lastHello, clock - ViewRefreshSeconds + 1);
                return;
            }
            Error = error.ToString();
        }
        private void Publish()
        {
            if (hostRoom == null || lobby.State != LobbyState.Connected) return;
            Current = hostRoom.Snapshot();
            byte[] data = RoomWireCodec.Encode(Current);
            foreach (var member in Current.Members)
                if (member.Id != LocalId && lobby.Contains(member.Id)) Send(member.Id, View, data);
            RoomChanged?.Invoke(Current);
        }
        private void Send(string peer, byte kind, byte[] data)
        {
            foreach (var packet in frames.Encode(kind, data)) transport.SendPacket(peer, 0, new ArraySegment<byte>(packet), true);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; transport.PacketReceived -= ReceivePacket; transport.PeerStateChanged -= PeerRouteChanged; lobby.Changed -= MembershipChanged;
            frames.MessageReceived -= ReceiveMessage; frames.Clear(); RoomChanged = null; MemberReconnected = null;
        }
    }
}


