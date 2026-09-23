using System;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Connect auth renewal policy. EOS notifies roughly one minute before the token expires
    /// (ConnectInterface.AddNotifyAuthExpiration), so the current session stays valid while the new Login runs:
    /// P2P traffic must keep flowing, and a failed Login is retried with backoff until the token would expire.
    /// </summary>
    public static class EosSessionRefresh
    {
        public const double WindowSeconds = 55, RetryBaseSeconds = 2, RetryMaximumSeconds = 10;

        public static bool CanUseSession(ConnectionState state, bool refreshing, bool hasLocalUser)
            => hasLocalUser && (state == ConnectionState.Ready || (state == ConnectionState.SigningIn && refreshing));

        /// <summary>Next Login attempt after <paramref name="failures"/> failed renewals, or null when none fits before the deadline.</summary>
        public static double? NextAttempt(double now, double deadline, int failures)
        {
            if (double.IsNaN(now) || double.IsNaN(deadline) || failures < 1) return null;
            double wait = Math.Min(RetryMaximumSeconds, RetryBaseSeconds * Math.Pow(2, Math.Min(16, failures - 1)));
            double at = now + wait;
            return at < deadline ? at : (double?)null;
        }
    }
}
