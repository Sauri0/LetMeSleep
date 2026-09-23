using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class PeerConnectionPlannerTests
    {
        private readonly List<string> accept = new List<string>(), close = new List<string>();

        [Test]
        public void EveryRemoteMemberIsAcceptedProactivelyButNeverTheLocalUser()
        {
            var planner = new PeerConnectionPlanner();
            planner.Reconcile(new[] { "local", "host", "guest-b", "" }, "local", 0, accept, close);
            Assert.That(accept, Is.EqualTo(new[] { "guest-b", "host" }));
            Assert.That(close, Is.Empty);
            Assert.That(planner.IsTracked("local"), Is.False);
        }

        [Test]
        public void AcceptedMembersAreNotRequestedAgainUntilTheLinkCloses()
        {
            var planner = new PeerConnectionPlanner();
            planner.Reconcile(new[] { "local", "host" }, "local", 0, accept, close);
            planner.AcceptIssued("host", true, 0);
            planner.Reconcile(new[] { "local", "host" }, "local", 5, accept, close);
            Assert.That(accept, Is.Empty);
            Assert.That(planner.ShouldAccept("host", 5), Is.False);
            Assert.That(planner.HasDueRetry(100), Is.False);
        }

        [Test]
        public void RemoteCloseIsRecoveredWithBoundedBackoffWhileThePeerRemainsInTheLobby()
        {
            var planner = new PeerConnectionPlanner();
            planner.Reconcile(new[] { "local", "host" }, "local", 0, accept, close);
            planner.AcceptIssued("host", true, 0);
            planner.Established("host");

            planner.Closed("host", 10);
            Assert.That(planner.IsAccepted("host"), Is.False);
            Assert.That(planner.HasDueRetry(10.5), Is.False);
            Assert.That(planner.HasDueRetry(10 + PeerConnectionPlanner.RetryBaseSeconds), Is.True);
            planner.Reconcile(new[] { "local", "host" }, "local", 11, accept, close);
            Assert.That(accept, Is.EqualTo(new[] { "host" }), "A closed link must be requested again, not abandoned.");

            // Repeated failures back off exponentially, capped; an established link resets the backoff.
            double at = 11, expected = PeerConnectionPlanner.RetryBaseSeconds * 2;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                planner.AcceptIssued("host", true, at);
                planner.Closed("host", at);
                Assert.That(planner.ShouldAccept("host", at + expected - .01), Is.False);
                Assert.That(planner.ShouldAccept("host", at + expected), Is.True);
                at += expected;
                expected = Math.Min(PeerConnectionPlanner.RetryMaximumSeconds, expected * 2);
            }
            planner.AcceptIssued("host", true, at);
            planner.Established("host");
            planner.Closed("host", at);
            Assert.That(planner.ShouldAccept("host", at + PeerConnectionPlanner.RetryBaseSeconds), Is.True);
        }

        [Test]
        public void MembersThatLeaveAreClosedAndForgotten()
        {
            var planner = new PeerConnectionPlanner();
            planner.Reconcile(new[] { "local", "a", "b" }, "local", 0, accept, close);
            planner.AcceptIssued("a", true, 0); planner.AcceptIssued("b", true, 0);
            planner.Reconcile(new[] { "local", "a" }, "local", 1, accept, close);
            Assert.That(close, Is.EqualTo(new[] { "b" }));
            Assert.That(accept, Is.Empty);
            Assert.That(planner.IsTracked("b"), Is.False);
            planner.Closed("b", 1);
            Assert.That(planner.HasDueRetry(50), Is.False, "A departed member must never be requested again.");

            planner.Reconcile(Array.Empty<string>(), "local", 2, accept, close);
            Assert.That(close, Is.EqualTo(new[] { "a" }), "A closed room releases every remaining link.");
            Assert.That(planner.TrackedCount, Is.Zero);
        }

        [Test]
        public void NoConnectionOnSendRequestsTheLinkAgainImmediately()
        {
            var planner = new PeerConnectionPlanner();
            Assert.That(planner.ShouldAccept("host", 0), Is.True, "An authenticated member not reconciled yet is accepted on first send.");
            planner.AcceptIssued("host", true, 0);
            Assert.That(planner.ShouldAccept("host", 0), Is.False);
            planner.ConnectionLost("host");
            Assert.That(planner.ShouldAccept("host", 0), Is.True);
        }

        [Test]
        public void FailedAcceptBacksOffInsteadOfSpinning()
        {
            var planner = new PeerConnectionPlanner();
            planner.AcceptIssued("host", false, 3);
            Assert.That(planner.ShouldAccept("host", 3.5), Is.False);
            Assert.That(planner.ShouldAccept("host", 3 + PeerConnectionPlanner.RetryBaseSeconds), Is.True);
        }

        [Test]
        public void DeliveryDiagnosticsAreThrottledPerPeerAndKind()
        {
            var planner = new PeerConnectionPlanner();
            planner.ShouldAccept("host", 0);
            Assert.That(planner.ShouldReportFailure("host", "Send0:LimitExceeded", 0), Is.True);
            Assert.That(planner.ShouldReportFailure("host", "Send0:LimitExceeded", 1), Is.False);
            Assert.That(planner.ShouldReportFailure("host", "Send1:NoConnection", 1), Is.True);
            Assert.That(planner.ShouldReportFailure("host", "Send1:NoConnection", 1 + PeerConnectionPlanner.FailureReportSeconds), Is.True);
            Assert.That(planner.ShouldReportFailure("stranger", "Send0:NoConnection", 0), Is.False);
        }

        // Models the documented EOS semantics (SendPacketOptions.DisableAutoAcceptConnection=true):
        // SendPacket returns NoConnection unless the sender accepted the peer, a connection exists only
        // after both sides accepted, and a close drops both acceptances and every queued packet.
        private sealed class Endpoint
        {
            internal readonly string Id;
            internal readonly PeerConnectionPlanner Planner = new PeerConnectionPlanner();
            internal readonly HashSet<string> Accepted = new HashSet<string>(StringComparer.Ordinal);
            internal readonly List<string> Inbox = new List<string>();
            internal Endpoint(string id) { Id = id; }
        }

        private static void Accept(Endpoint local, string remote, double now)
        {
            local.Accepted.Add(remote);
            local.Planner.AcceptIssued(remote, true, now);
        }

        private static bool Connected(Endpoint a, Endpoint b) => a.Accepted.Contains(b.Id) && b.Accepted.Contains(a.Id);

        private static bool Send(Endpoint from, Endpoint to, string message, double now, Queue<(Endpoint, string)> queue)
        {
            if (from.Planner.ShouldAccept(to.Id, now)) Accept(from, to.Id, now);
            if (!from.Accepted.Contains(to.Id)) { from.Planner.ConnectionLost(to.Id); return false; }
            queue.Enqueue((to, message)); // AllowDelayedDelivery: queued until both sides accepted.
            return true;
        }

        private static void Deliver(Endpoint a, Endpoint b, Queue<(Endpoint, string)> queue)
        {
            if (!Connected(a, b)) return;
            a.Planner.Established(b.Id); b.Planner.Established(a.Id);
            while (queue.Count > 0) { var (to, message) = queue.Dequeue(); to.Inbox.Add(message); }
        }

        private void Reconcile(Endpoint endpoint, string[] lobby, double now)
        {
            endpoint.Planner.Reconcile(lobby, endpoint.Id, now, accept, close);
            foreach (var member in close) endpoint.Accepted.Remove(member);
            foreach (var member in accept) Accept(endpoint, member, now);
        }

        [Test]
        public void GuestHelloReachesHostAndRecoversAfterTheLinkCloses()
        {
            var host = new Endpoint("host"); var guest = new Endpoint("guest"); var queue = new Queue<(Endpoint, string)>();
            string[] lobby = { "guest", "host" };
            // The guest joins; both lobby notifications reconcile membership before any room packet.
            Reconcile(guest, lobby, 0);
            Reconcile(host, lobby, 0);
            Assert.That(Send(guest, host, "Hello", 0, queue), Is.True);
            Deliver(guest, host, queue);
            Assert.That(host.Inbox, Is.EqualTo(new[] { "Hello" }), "Explicit acceptance on both sides must open the link.");

            // Wi-Fi drop: EOS closes and flushes. Both sides re-request after the backoff, without new lobby events.
            host.Accepted.Remove("guest"); guest.Accepted.Remove("host"); queue.Clear();
            host.Planner.Closed("guest", 20); guest.Planner.Closed("host", 20);
            Assert.That(Send(host, guest, "View", 20.2, queue), Is.False, "Within the backoff the send reports NoConnection.");
            Assert.That(host.Planner.HasDueRetry(21), Is.True);
            Reconcile(host, lobby, 21);
            Reconcile(guest, lobby, 21);
            Assert.That(Send(host, guest, "View", 21, queue), Is.True);
            Deliver(host, guest, queue);
            Assert.That(guest.Inbox, Is.EqualTo(new[] { "View" }));
        }

        [Test]
        public void ConnectionRequestThatRacesTheLobbyNotificationIsCompletedByReconcile()
        {
            var host = new Endpoint("host"); var guest = new Endpoint("guest"); var queue = new Queue<(Endpoint, string)>();
            Reconcile(guest, new[] { "guest", "host" }, 0);
            Assert.That(Send(guest, host, "Hello", 0, queue), Is.True);
            // The host has not seen the guest in the lobby yet, so it ignores the request.
            Reconcile(host, new[] { "host" }, 0);
            Deliver(guest, host, queue);
            Assert.That(host.Inbox, Is.Empty);
            // The lobby notification arrives: the host accepts proactively and the queued Hello is delivered.
            Reconcile(host, new[] { "guest", "host" }, .4);
            Deliver(guest, host, queue);
            Assert.That(host.Inbox.Single(), Is.EqualTo("Hello"));
        }
    }
}
