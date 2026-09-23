using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Per-sender receive budget for <see cref="EosPeerTransport.Poll"/>. EOS hands out packets from a single
    /// arrival-ordered queue, so one member sending far above any legitimate rate (a modified client) could use
    /// the whole per-frame read budget and hold back everyone else's traffic. Poll reads more packets per frame
    /// than before and cheaply discards a sender's packets once its token bucket is empty, so a flood is drained
    /// instead of queuing up in front of the other members. Legitimate peers send about 100-250 packets per second
    /// (snapshot fragments, private state, events, voice); the bucket allows four times that sustained plus a
    /// burst covering a multi-second receive stall, so their traffic is never discarded.
    /// </summary>
    public sealed class PeerPacketBudget
    {
        public const int ReadsPerFrame = 512;
        public const double PacketsPerSecond = 1000, Burst = 3000;
        private readonly Dictionary<string, VoiceRateLimiter> buckets = new Dictionary<string, VoiceRateLimiter>(StringComparer.Ordinal);
        private readonly double rate, burst;

        public PeerPacketBudget(double packetsPerSecond = PacketsPerSecond, double burstPackets = Burst)
        {
            if (!(packetsPerSecond > 0) || !(burstPackets >= 1)) throw new ArgumentOutOfRangeException(nameof(packetsPerSecond));
            rate = packetsPerSecond; burst = burstPackets;
        }

        public int TrackedCount => buckets.Count;

        /// <summary>False when <paramref name="member"/> exceeded its budget: discard the packet without dispatching it.</summary>
        public bool TryAccept(string member, double now)
        {
            if (string.IsNullOrEmpty(member)) return false;
            if (!buckets.TryGetValue(member, out var bucket))
                buckets.Add(member, bucket = new VoiceRateLimiter(rate, burst));
            return bucket.TryConsume(now);
        }

        public void Forget(string member) { if (member != null) buckets.Remove(member); }
        public void Clear() => buckets.Clear();
    }
}
