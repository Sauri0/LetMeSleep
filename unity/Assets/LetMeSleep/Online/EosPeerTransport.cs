using System;
using System.Collections.Generic;
using System.Diagnostics;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;

namespace LetMeSleep.Online
{
    /// <summary>Bounded authenticated datagrams. Consumers must copy the receive segment before returning.</summary>
    public sealed class EosPeerTransport : IDisposable, IRoomLink
    {
        public const int MaximumPacketBytes = 1170;
        public const byte MaximumChannel = 4;
        public static bool IsSupportedChannel(byte channel) => channel <= MaximumChannel;
        private readonly EosConnection connection;
        private readonly EosLobbySession room;
        private readonly P2PInterface p2p;
        private readonly SocketId socket = new SocketId { SocketName = "LMSUnityAlfa1" };
        private readonly byte[] receiveBuffer = new byte[MaximumPacketBytes];
        private readonly ulong incomingNotification, establishedNotification, closedNotification;
        // SendPacket runs with DisableAutoAcceptConnection=true, so nothing opens a connection unless this
        // transport calls AcceptConnection ("Accept or Request") for every member, and again after each close.
        private readonly PeerConnectionPlanner links = new PeerConnectionPlanner();
        private readonly List<string> acceptNow = new List<string>(), closeNow = new List<string>();
        private bool membershipDirty = true;
        private bool disposed;
        private readonly PacketHandlerList packetHandlers = new PacketHandlerList();
        private readonly PeerPacketBudget receiveBudget = new PeerPacketBudget();
        private static readonly Action<Exception> LogHandlerFailure = error => UnityEngine.Debug.LogException(error);
        // Each consumer runs isolated: one failing handler must not cut the drain for everyone else.
        public event Action<string, byte, ArraySegment<byte>> PacketReceived
        {
            add => packetHandlers.Add(value);
            remove => packetHandlers.Remove(value);
        }
        public event Action<string, string> PeerStateChanged;
        /// <summary>Throttled diagnostics for SendPacket/AcceptConnection results other than Success.</summary>
        public event Action<string, string> DeliveryIssue;

        public EosPeerTransport(EosConnection connection, EosLobbySession room, bool forceRelay = false)
        {
            this.connection = connection; this.room = room;
            p2p = connection.Platform.GetP2PInterface();
            var relay = new SetRelayControlOptions { RelayControl = forceRelay ? RelayControl.ForceRelays : RelayControl.AllowRelays };
            var result = p2p.SetRelayControl(ref relay);
            if (result != Result.Success) throw new InvalidOperationException("Relay configuration failed: " + result);
            var incoming = new AddNotifyPeerConnectionRequestOptions { LocalUserId = connection.LocalUserId, SocketId = socket };
            incomingNotification = p2p.AddNotifyPeerConnectionRequest(ref incoming, null, OnIncoming);
            var established = new AddNotifyPeerConnectionEstablishedOptions { LocalUserId = connection.LocalUserId, SocketId = socket };
            establishedNotification = p2p.AddNotifyPeerConnectionEstablished(ref established, null, OnEstablished);
            var closed = new AddNotifyPeerConnectionClosedOptions { LocalUserId = connection.LocalUserId, SocketId = socket };
            closedNotification = p2p.AddNotifyPeerConnectionClosed(ref closed, null, OnClosed);
            connection.StateChanged += OnConnectionChanged;
            // Subscribed before any room coordinator: membership changes accept peers before views are published.
            room.Changed += OnMembershipChanged;
        }

        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        private bool SessionUsable => !disposed && connection.CanUseSession;

        public Result Send(string memberId, byte channel, ArraySegment<byte> data, bool reliable)
        {
            if (!SessionUsable || room.State != LobbyState.Connected || !room.Contains(memberId)) return Result.AccessDenied;
            if (data.Array == null || data.Count < 1 || data.Count > MaximumPacketBytes || !IsSupportedChannel(channel)) return Result.InvalidParameters;
            double now = Now;
            if (links.ShouldAccept(memberId, now)) Accept(memberId, now);
            var options = new SendPacketOptions
            {
                LocalUserId = connection.LocalUserId, RemoteUserId = ProductUserId.FromString(memberId), SocketId = socket,
                Channel = channel, Data = data, AllowDelayedDelivery = true,
                Reliability = reliable ? PacketReliability.ReliableOrdered : PacketReliability.UnreliableUnordered,
                DisableAutoAcceptConnection = true
            };
            var result = p2p.SendPacket(ref options);
            if (result == Result.NoConnection)
            {
                // The acceptance was dropped (closed before we observed it). Request it again and retry once.
                links.ConnectionLost(memberId);
                if (links.ShouldAccept(memberId, now) && Accept(memberId, now)) result = p2p.SendPacket(ref options);
            }
            if (result != Result.Success) ReportIssue(memberId, "Send" + channel + ":" + result, now);
            return result;
        }

        void IRoomLink.SendPacket(string memberId, byte channel, ArraySegment<byte> data, bool reliable) => Send(memberId, channel, data, reliable);

        public void Poll()
        {
            if (!SessionUsable) return;
            if (membershipDirty || links.HasDueRetry(Now)) ReconcileMembers();
            var options = new ReceivePacketOptions { LocalUserId = connection.LocalUserId, MaxDataSizeBytes = MaximumPacketBytes };
            double now = Now;
            // Bound work even if a member floods the room. Payload validation is the next layer. A sender above its
            // budget is discarded here, cheaply, so it cannot hold back the packets queued behind it by other members.
            for (int packet = 0; packet < PeerPacketBudget.ReadsPerFrame; packet++)
            {
                ProductUserId peer = null;
                var sourceSocket = new SocketId();
                var result = p2p.ReceivePacket(ref options, ref peer, ref sourceSocket, out byte channel,
                    new ArraySegment<byte>(receiveBuffer), out uint length);
                if (result == Result.NotFound) break;
                if (result != Result.Success) break;
                if (room.State != LobbyState.Connected || peer == null || sourceSocket.SocketName != socket.SocketName || !IsSupportedChannel(channel)) continue;
                string member = peer.ToString();
                if (!room.Contains(member)) continue;
                if (!receiveBudget.TryAccept(member, now)) { ReportIssue(member, "ReceiveBudgetExceeded", now); continue; }
                packetHandlers.Dispatch(member, channel, new ArraySegment<byte>(receiveBuffer, 0, (int)length), LogHandlerFailure);
            }
        }

        private void OnMembershipChanged()
        {
            membershipDirty = true;
            ReconcileMembers();
        }

        private void ReconcileMembers()
        {
            // Keep existing links while the session is temporarily unusable (auth refresh); retry on the next Poll.
            if (!SessionUsable) return;
            membershipDirty = false;
            double now = Now;
            string local = connection.LocalUserId?.ToString();
            links.Reconcile(room.State == LobbyState.Connected ? room.CaptureMembers() : Array.Empty<string>(), local, now, acceptNow, closeNow);
            foreach (var member in closeNow) { Close(member); receiveBudget.Forget(member); }
            foreach (var member in acceptNow) Accept(member, now);
        }

        private bool Accept(string member, double now)
        {
            var options = new AcceptConnectionOptions { LocalUserId = connection.LocalUserId, RemoteUserId = ProductUserId.FromString(member), SocketId = socket };
            var result = p2p.AcceptConnection(ref options);
            links.AcceptIssued(member, result == Result.Success, now);
            if (result != Result.Success) ReportIssue(member, "Accept:" + result, now);
            return result == Result.Success;
        }

        private void Close(string member)
        {
            var options = new CloseConnectionOptions { LocalUserId = connection.LocalUserId, RemoteUserId = ProductUserId.FromString(member), SocketId = socket };
            p2p.CloseConnection(ref options);
        }

        private void ReportIssue(string member, string issue, double now)
        {
            if (links.ShouldReportFailure(member, issue, now)) DeliveryIssue?.Invoke(member, issue);
        }

        private void OnIncoming(ref OnIncomingConnectionRequestInfo info)
        {
            if (!SessionUsable || room.State != LobbyState.Connected || info.RemoteUserId == null) return;
            string member = info.RemoteUserId.ToString();
            // A request that races ahead of our lobby notification is accepted by the next reconcile instead.
            if (room.Contains(member)) Accept(member, Now);
        }
        private void OnEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            if (disposed || info.RemoteUserId == null) return;
            string member = info.RemoteUserId.ToString();
            links.Established(member);
            if (SessionUsable && room.State == LobbyState.Connected && room.Contains(member))
                PeerStateChanged?.Invoke(member, info.NetworkType.ToString());
        }
        private void OnClosed(ref OnRemoteConnectionClosedInfo info)
        {
            if (disposed || info.RemoteUserId == null) return;
            string member = info.RemoteUserId.ToString();
            // Closing flushes queued packets, reliable ones included. Re-request while the peer is still a member.
            links.Closed(member, Now);
            if (SessionUsable && room.State == LobbyState.Connected && room.Contains(member))
                PeerStateChanged?.Invoke(member, "Closed:" + info.Reason);
        }
        private void OnConnectionChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disposed) { Dispose(); return; }
            if (connection.CanUseSession) membershipDirty = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            connection.StateChanged -= OnConnectionChanged;
            room.Changed -= OnMembershipChanged;
            p2p.RemoveNotifyPeerConnectionRequest(incomingNotification);
            p2p.RemoveNotifyPeerConnectionEstablished(establishedNotification);
            p2p.RemoveNotifyPeerConnectionClosed(closedNotification);
            var close = new CloseConnectionsOptions { LocalUserId = connection.LocalUserId, SocketId = socket };
            p2p.CloseConnections(ref close);
            links.Clear(); receiveBudget.Clear();
            packetHandlers.Clear(); PeerStateChanged = null; DeliveryIssue = null;
        }
    }
}
