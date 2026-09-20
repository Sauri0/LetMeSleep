using System;
using Epic.OnlineServices;

namespace LetMeSleep.Online
{
    /// <summary>Dedicated voice view over EOS P2P channel 3. Does not own the shared peer transport.</summary>
    public sealed class VoiceEosChannelTransport : IVoiceDatagramTransport
    {
        private readonly EosPeerTransport transport;
        private readonly EosLobbySession lobby;
        private bool disposed;
        public event Action<string, ArraySegment<byte>> PacketReceived;

        public VoiceEosChannelTransport(EosPeerTransport transport, EosLobbySession lobby)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            transport.PacketReceived += OnPacket;
        }

        public bool ContainsMember(string memberId) => !disposed && !string.IsNullOrEmpty(memberId) && lobby.Contains(memberId);

        public bool Send(string memberId, ArraySegment<byte> packet, bool reliable)
        {
            if (disposed || !ContainsMember(memberId) || packet.Array == null || packet.Count < VoiceWireCodec.HeaderBytes || packet.Count > VoiceWireCodec.AudioPacketBytes) return false;
            return transport.Send(memberId, VoiceProtocol.Channel, packet, reliable) == Result.Success;
        }

        private void OnPacket(string memberId, byte channel, ArraySegment<byte> packet)
        {
            if (!disposed && channel == VoiceProtocol.Channel && lobby.Contains(memberId)) PacketReceived?.Invoke(memberId, packet);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; transport.PacketReceived -= OnPacket; PacketReceived = null;
        }
    }
}
