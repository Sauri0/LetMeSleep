using System;

namespace LetMeSleep.Online
{
    public sealed class VoiceRateLimiter
    {
        private readonly double rate, capacity;
        private double tokens, lastTime;
        private bool initialized;

        public VoiceRateLimiter(double packetsPerSecond = VoiceProtocol.MaximumPacketsPerSecond, double burst = VoiceProtocol.RateBurst)
        {
            if (packetsPerSecond <= 0 || burst < 1) throw new ArgumentOutOfRangeException();
            rate = packetsPerSecond; capacity = burst; tokens = burst;
        }

        public bool TryConsume(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) return false;
            if (!initialized) { initialized = true; lastTime = now; }
            if (now < lastTime) return false;
            tokens = Math.Min(capacity, tokens + (now - lastTime) * rate);
            lastTime = now;
            if (tokens < 1) return false;
            tokens -= 1; return true;
        }
    }
}
