using System;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;

namespace LetMeSleep.Online
{
    /// <summary>Bounded authenticated datagrams. Consumers must copy the receive segment before returning.</summary>
    public sealed class EosPeerTransport : IDisposable
    {
        public const int MaximumPacketBytes = 1170;
        private readonly EosConnection connection;
        private readonly EosLobbySession room;
        private readonly P2PInterface p2p;
        private readonly SocketId socket = new SocketId { SocketName = "LMSUnityAlfa1" };
        private readonly byte[] receiveBuffer = new byte[MaximumPacketBytes];
        private readonly ulong incomingNotification, establishedNotification, closedNotification;
        private bool disposed;
        public event Action<string, byte, ArraySegment<byte>> PacketReceived;
        public event Action<string, string> PeerStateChanged;

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
        }

        public Result Send(string memberId, byte channel, ArraySegment<byte> data, bool reliable)
        {
            if (disposed || connection.State != ConnectionState.Ready || room.State != LobbyState.Connected || !room.Contains(memberId)) return Result.AccessDenied;
            if (data.Array == null || data.Count < 1 || data.Count > MaximumPacketBytes || channel > 3) return Result.InvalidParameters;
            var options = new SendPacketOptions
            {
                LocalUserId = connection.LocalUserId, RemoteUserId = ProductUserId.FromString(memberId), SocketId = socket,
                Channel = channel, Data = data, AllowDelayedDelivery = true,
                Reliability = reliable ? PacketReliability.ReliableOrdered : PacketReliability.UnreliableUnordered,
                DisableAutoAcceptConnection = true
            };
            return p2p.SendPacket(ref options);
        }

        public void Poll()
        {
            if (disposed || connection.State != ConnectionState.Ready) return;
            var options = new ReceivePacketOptions { LocalUserId = connection.LocalUserId, MaxDataSizeBytes = MaximumPacketBytes };
            // Bound work even if a member floods the room. Payload validation is the next layer.
            for (int packet = 0; packet < 128; packet++)
            {
                ProductUserId peer = null;
                var sourceSocket = new SocketId();
                var result = p2p.ReceivePacket(ref options, ref peer, ref sourceSocket, out byte channel,
                    new ArraySegment<byte>(receiveBuffer), out uint length);
                if (result == Result.NotFound) break;
                if (result != Result.Success) break;
                if (room.State != LobbyState.Connected || peer == null || sourceSocket.SocketName != socket.SocketName || channel > 3) continue;
                string member = peer.ToString();
                if (room.Contains(member)) PacketReceived?.Invoke(member, channel, new ArraySegment<byte>(receiveBuffer, 0, (int)length));
            }
        }

        private void OnIncoming(ref OnIncomingConnectionRequestInfo info)
        {
            if (disposed || connection.State != ConnectionState.Ready || room.State != LobbyState.Connected || !room.Contains(info.RemoteUserId.ToString())) return;
            var options = new AcceptConnectionOptions { LocalUserId = connection.LocalUserId, RemoteUserId = info.RemoteUserId, SocketId = socket };
            p2p.AcceptConnection(ref options);
        }
        private void OnEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            if (!disposed && connection.State == ConnectionState.Ready && room.State == LobbyState.Connected && room.Contains(info.RemoteUserId.ToString()))
                PeerStateChanged?.Invoke(info.RemoteUserId.ToString(), info.NetworkType.ToString());
        }
        private void OnClosed(ref OnRemoteConnectionClosedInfo info)
        {
            if (!disposed && connection.State == ConnectionState.Ready && room.State == LobbyState.Connected && room.Contains(info.RemoteUserId.ToString()))
                PeerStateChanged?.Invoke(info.RemoteUserId.ToString(), "Closed:" + info.Reason);
        }
        private void OnConnectionChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disposed) Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            connection.StateChanged -= OnConnectionChanged;
            p2p.RemoveNotifyPeerConnectionRequest(incomingNotification);
            p2p.RemoveNotifyPeerConnectionEstablished(establishedNotification);
            p2p.RemoveNotifyPeerConnectionClosed(closedNotification);
            var close = new CloseConnectionsOptions { LocalUserId = connection.LocalUserId, SocketId = socket };
            p2p.CloseConnections(ref close);
            PacketReceived = null; PeerStateChanged = null;
        }
    }
}
