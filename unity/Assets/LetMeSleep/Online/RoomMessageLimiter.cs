using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Per-member budget for room control messages (channel 0). Every accepted Hello/Ready can make the host
    /// broadcast a reliable view to the whole room, so an unbounded sender would amplify its traffic by the
    /// room size and starve everyone else's reliable queue. Legitimate clients send about one Hello per second
    /// while joining and one Ready per click.
    /// </summary>
    public sealed class RoomMessageLimiter
    {
        public const double MessagesPerSecond = 4, Burst = 8;
        private readonly Dictionary<string, VoiceRateLimiter> buckets = new Dictionary<string, VoiceRateLimiter>(StringComparer.Ordinal);

        public bool TryAccept(string member, double now)
        {
            if (string.IsNullOrEmpty(member)) return false;
            if (!buckets.TryGetValue(member, out var bucket))
                buckets.Add(member, bucket = new VoiceRateLimiter(MessagesPerSecond, Burst));
            return bucket.TryConsume(now);
        }

        public void Forget(string member) { if (member != null) buckets.Remove(member); }
        public void Clear() => buckets.Clear();
    }
}
