using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Pure bookkeeping for explicit EOS P2P acceptance. EosPeerTransport sends with
    /// DisableAutoAcceptConnection=true: per the bundled SDK (SendPacketOptions, P2PInterface.SendPacket)
    /// SendPacket then never opens a connection by itself and returns NoConnection until AcceptConnection
    /// ("Accept or Request") was called for that peer, and again after every close. Therefore every
    /// authenticated room member is accepted proactively from both sides, re-accepted with a bounded backoff
    /// after a close while it is still a member, and closed when it leaves the lobby.
    /// </summary>
    public sealed class PeerConnectionPlanner
    {
        public const double RetryBaseSeconds = 1, RetryMaximumSeconds = 8, FailureReportSeconds = 5;

        private sealed class Link
        {
            internal bool Accepted, Established;
            internal double RetryAt = double.NegativeInfinity, Backoff = RetryBaseSeconds;
            internal string LastFailure;
            internal double FailureReportedAt = double.NegativeInfinity;
        }

        private readonly Dictionary<string, Link> links = new Dictionary<string, Link>(StringComparer.Ordinal);
        private readonly List<string> departed = new List<string>();
        private readonly HashSet<string> current = new HashSet<string>(StringComparer.Ordinal);

        public int TrackedCount => links.Count;
        public bool IsTracked(string member) => member != null && links.ContainsKey(member);
        public bool IsAccepted(string member) => member != null && links.TryGetValue(member, out var link) && link.Accepted;
        public bool IsEstablished(string member) => member != null && links.TryGetValue(member, out var link) && link.Established;

        /// <summary>
        /// Reconciles against the authoritative lobby member list. <paramref name="accept"/> receives remote members
        /// that need AcceptConnection now; <paramref name="close"/> receives tracked peers that left the lobby.
        /// </summary>
        public void Reconcile(IEnumerable<string> members, string localId, double now, List<string> accept, List<string> close)
        {
            if (accept == null) throw new ArgumentNullException(nameof(accept));
            if (close == null) throw new ArgumentNullException(nameof(close));
            accept.Clear(); close.Clear(); current.Clear();
            if (members != null)
                foreach (var member in members)
                    if (!string.IsNullOrEmpty(member) && !string.Equals(member, localId, StringComparison.Ordinal)) current.Add(member);
            departed.Clear();
            foreach (var tracked in links.Keys) if (!current.Contains(tracked)) departed.Add(tracked);
            foreach (var member in departed) { links.Remove(member); close.Add(member); }
            foreach (var member in current)
            {
                if (!links.TryGetValue(member, out var link)) links.Add(member, link = new Link());
                if (!link.Accepted && now >= link.RetryAt) accept.Add(member);
            }
            accept.Sort(StringComparer.Ordinal); close.Sort(StringComparer.Ordinal);
        }

        /// <summary>True when a send to this (already authenticated) member must first (re)issue AcceptConnection.</summary>
        public bool ShouldAccept(string member, double now)
        {
            if (string.IsNullOrEmpty(member)) return false;
            if (!links.TryGetValue(member, out var link)) { links.Add(member, new Link()); return true; }
            return !link.Accepted && now >= link.RetryAt;
        }

        public void AcceptIssued(string member, bool success, double now)
        {
            if (string.IsNullOrEmpty(member)) return;
            if (!links.TryGetValue(member, out var link)) links.Add(member, link = new Link());
            if (success) { link.Accepted = true; return; }
            link.Accepted = false; ScheduleRetry(link, now);
        }

        public void Established(string member)
        {
            if (member == null || !links.TryGetValue(member, out var link)) return;
            link.Accepted = true; link.Established = true; link.Backoff = RetryBaseSeconds; link.LastFailure = null;
        }

        /// <summary>EOS dropped the local acceptance (and flushed queued packets). Retry after a bounded backoff.</summary>
        public void Closed(string member, double now)
        {
            if (member == null || !links.TryGetValue(member, out var link)) return;
            link.Accepted = false; link.Established = false; ScheduleRetry(link, now);
        }

        /// <summary>SendPacket reported NoConnection: the acceptance is gone even if no close was observed yet.</summary>
        public void ConnectionLost(string member)
        {
            if (member == null || !links.TryGetValue(member, out var link)) return;
            link.Accepted = false; link.Established = false;
        }

        public bool HasDueRetry(double now)
        {
            foreach (var link in links.Values) if (!link.Accepted && now >= link.RetryAt) return true;
            return false;
        }

        /// <summary>Throttles delivery diagnostics to one record per peer and failure kind every few seconds.</summary>
        public bool ShouldReportFailure(string member, string failure, double now)
        {
            if (member == null || !links.TryGetValue(member, out var link)) return false;
            if (string.Equals(link.LastFailure, failure, StringComparison.Ordinal) && now - link.FailureReportedAt < FailureReportSeconds) return false;
            link.LastFailure = failure; link.FailureReportedAt = now; return true;
        }

        public void Clear() { links.Clear(); departed.Clear(); current.Clear(); }

        private static void ScheduleRetry(Link link, double now)
        {
            link.RetryAt = now + link.Backoff;
            link.Backoff = Math.Min(RetryMaximumSeconds, link.Backoff * 2);
        }
    }
}
