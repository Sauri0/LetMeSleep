using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class EosSessionRefreshTests
    {
        [Test]
        public void RenewalOfAValidSessionKeepsP2PUsable()
        {
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.Ready, false, true), Is.True);
            // The auth-expiration notice arrives about a minute early; the old token still authorizes P2P.
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.SigningIn, true, true), Is.True,
                "Room views, Begin and RoundEnded must not be dropped with AccessDenied during the renewal.");
        }

        [Test]
        public void InitialSignInFailureAndTeardownDoNotAllowTraffic()
        {
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.SigningIn, false, true), Is.False);
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.SigningIn, true, false), Is.False);
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.Failed, true, true), Is.False);
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.Disposed, false, true), Is.False);
            Assert.That(EosSessionRefresh.CanUseSession(ConnectionState.Offline, false, false), Is.False);
        }

        [Test]
        public void FailedRenewalRetriesWithBackoffUntilTheTokenWouldExpire()
        {
            double start = 100, deadline = start + EosSessionRefresh.WindowSeconds;
            Assert.That(EosSessionRefresh.NextAttempt(start, deadline, 1), Is.EqualTo(start + 2));
            Assert.That(EosSessionRefresh.NextAttempt(start, deadline, 2), Is.EqualTo(start + 4));
            Assert.That(EosSessionRefresh.NextAttempt(start, deadline, 3), Is.EqualTo(start + 8));
            Assert.That(EosSessionRefresh.NextAttempt(start, deadline, 9), Is.EqualTo(start + EosSessionRefresh.RetryMaximumSeconds));
            Assert.That(EosSessionRefresh.NextAttempt(deadline - 1, deadline, 1), Is.Null, "No retry fits: fail instead of pretending.");
            Assert.That(EosSessionRefresh.NextAttempt(start, deadline, 0), Is.Null);
        }

        [Test]
        public void ARenewalWindowAllowsSeveralAttemptsAfterAShortNetworkDrop()
        {
            double now = 0, deadline = EosSessionRefresh.WindowSeconds; int attempts = 1;
            for (int failures = 1; ; failures++)
            {
                var next = EosSessionRefresh.NextAttempt(now, deadline, failures);
                if (!next.HasValue) break;
                now = next.Value; attempts++;
            }
            Assert.That(attempts, Is.GreaterThanOrEqualTo(6), "A 30 s outage must not close the room on the first failed Login.");
        }
    }
}
