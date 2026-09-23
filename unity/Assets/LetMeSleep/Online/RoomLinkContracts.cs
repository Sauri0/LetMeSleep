using System;

namespace LetMeSleep.Online
{
    /// <summary>Authenticated lobby membership as seen by the room coordinator (EosLobbySession in the game).</summary>
    public interface IRoomLobby
    {
        LobbyState State { get; }
        bool IsOwner { get; }
        string OwnerId { get; }
        /// <summary>Authenticated local product user id, or null before sign-in.</summary>
        string LocalMemberId { get; }
        bool Contains(string memberId);
        event Action Changed;
    }

    /// <summary>Packet link used by the room coordinator (EosPeerTransport in the game).</summary>
    public interface IRoomLink
    {
        /// <summary>Best effort: a delivery failure is reported by the transport, never thrown.</summary>
        void SendPacket(string memberId, byte channel, ArraySegment<byte> data, bool reliable);
        event Action<string, byte, ArraySegment<byte>> PacketReceived;
        /// <summary>(member, state): a network type when a link is established, "Closed:reason" when it closes.</summary>
        event Action<string, string> PeerStateChanged;
    }
}
