using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    public static class VoiceProtocol
    {
        public const byte Channel = 3;
        public const int SampleRate = 12000;
        public const int FrameSamples = 240;
        public const int FrameMilliseconds = 20;
        public const int MaximumCodecBytes = 126;
        public const int MaximumPeers = 15;
        public const int MaximumPacketsPerSecond = 55;
        public const int RateBurst = 12;
    }

    public sealed class VoicePeerRoute
    {
        public string MemberId { get; }
        public uint ActorId { get; }
        public bool CanHearLocal { get; }
        public bool CanSpeakToLocal { get; }
        public bool IsMosquito { get; }

        public VoicePeerRoute(string memberId, uint actorId, bool canHearLocal, bool canSpeakToLocal, bool isMosquito)
        {
            if (string.IsNullOrWhiteSpace(memberId) || memberId.Length > 128) throw new ArgumentOutOfRangeException(nameof(memberId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            MemberId = memberId;
            ActorId = actorId;
            CanHearLocal = canHearLocal;
            CanSpeakToLocal = canSpeakToLocal;
            IsMosquito = isMosquito;
        }
    }

    /// <summary>Immutable identity/routing snapshot derived from the authenticated room session.</summary>
    public sealed class VoiceRoundContext
    {
        private readonly VoicePeerRoute[] peers;
        public ulong SessionEpoch { get; }
        public ulong RoundId { get; }
        public uint LocalActorId { get; }
        public bool LocalCanSpeak { get; }
        public bool LocalCanListen { get; }
        public IReadOnlyList<VoicePeerRoute> Peers => peers;

        public VoiceRoundContext(ulong sessionEpoch, ulong roundId, uint localActorId, bool localCanSpeak,
            bool localCanListen, IReadOnlyList<VoicePeerRoute> peers)
        {
            if (sessionEpoch == 0) throw new ArgumentOutOfRangeException(nameof(sessionEpoch));
            if (roundId == 0) throw new ArgumentOutOfRangeException(nameof(roundId));
            if (localActorId == 0) throw new ArgumentOutOfRangeException(nameof(localActorId));
            if (peers == null || peers.Count > VoiceProtocol.MaximumPeers) throw new ArgumentOutOfRangeException(nameof(peers));
            var members = new HashSet<string>(StringComparer.Ordinal);
            var actors = new HashSet<uint> { localActorId };
            this.peers = new VoicePeerRoute[peers.Count];
            for (int i = 0; i < peers.Count; i++)
            {
                VoicePeerRoute peer = peers[i] ?? throw new ArgumentException("A voice route cannot be null.", nameof(peers));
                if (!members.Add(peer.MemberId)) throw new ArgumentException("Voice member IDs must be unique.", nameof(peers));
                if (!actors.Add(peer.ActorId)) throw new ArgumentException("Voice actor IDs must be unique and exclude the local actor.", nameof(peers));
                this.peers[i] = peer;
            }
            SessionEpoch = sessionEpoch;
            RoundId = roundId;
            LocalActorId = localActorId;
            LocalCanSpeak = localCanSpeak;
            LocalCanListen = localCanListen;
        }
    }

    public sealed class VoiceDecodedFrame
    {
        public uint ActorId { get; }
        public bool IsMosquito { get; }
        public float[] Samples { get; }
        public float Volume { get; }
        public float Level { get; }
        public bool Concealed { get; }

        public VoiceDecodedFrame(uint actorId, bool isMosquito, float[] samples, float volume, float level, bool concealed)
        {
            ActorId = actorId;
            IsMosquito = isMosquito;
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            Volume = volume;
            Level = level;
            Concealed = concealed;
        }
    }

    public interface IVoiceDatagramTransport : IDisposable
    {
        event Action<string, ArraySegment<byte>> PacketReceived;
        bool ContainsMember(string memberId);
        bool Send(string memberId, ArraySegment<byte> packet, bool reliable);
    }
}
