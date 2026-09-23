using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Small ordered playout queue with loss concealment requests. The playout ring downstream owns timing
    /// (adaptive prebuffer and drift control against the local audio clock), so after the initial
    /// <see cref="PrebufferSeconds"/> this queue releases frames in order as soon as they are contiguous instead of
    /// pacing them at the receiver's nominal 50 fps (pacing hid a faster sender's surplus from the playout and let
    /// latency creep). It reorders, rejects replays and decides when a frame is lost: a missing frame is concealed
    /// only after a later frame has waited <see cref="ReorderGraceSeconds"/>, so a merely reordered packet still plays.
    /// </summary>
    public sealed class VoiceJitterBuffer
    {
        public const int MaximumQueuedFrames = 12;
        public const uint MaximumFutureFrames = 24;
        public const double PrebufferSeconds = 0.040;
        public const double FrameSeconds = 0.020;
        public const double IdleSeconds = 0.300;
        public const double ReorderGraceSeconds = 0.030;

        private readonly SortedDictionary<uint, byte[]> frames = new SortedDictionary<uint, byte[]>();
        private readonly Dictionary<uint, double> arrivals = new Dictionary<uint, double>();
        private uint streamId, expected, endSequence, closedStreamId, replayWatermark;
        private double nextPlayout, lastArrival;
        private bool active, ended, temporarilyCleared, primed;
        private byte[] lastPayload;

        public bool IsActive => active;
        public int QueuedFrames => frames.Count;

        public bool AcceptAudio(uint incomingStream, uint sequence, byte[] payload, double now)
        {
            if (incomingStream == 0 || sequence == 0 || payload == null || payload.Length != VoiceProtocol.MaximumCodecBytes || !FiniteTime(now)) return false;
            if (closedStreamId != 0 && !IsNewer(incomingStream, closedStreamId)) return false;
            if (!active || IsNewer(incomingStream, streamId)) Start(incomingStream, sequence, now);
            else if (incomingStream != streamId || ended) return false;
            else if (temporarilyCleared)
            {
                if (!IsNewer(sequence, replayWatermark)) return false;
                Resume(sequence, now);
            }
            if (sequence < expected || sequence - expected > MaximumFutureFrames || frames.ContainsKey(sequence) || frames.Count >= MaximumQueuedFrames) return false;
            var copy = new byte[payload.Length]; Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
            frames.Add(sequence, copy); arrivals[sequence] = now;
            if (replayWatermark == 0 || IsNewer(sequence, replayWatermark)) replayWatermark = sequence;
            lastArrival = now; return true;
        }

        public bool AcceptEnd(uint incomingStream, uint sequence, double now)
        {
            if (incomingStream == 0 || sequence == 0 || !FiniteTime(now)) return false;
            if (!active)
            {
                if (closedStreamId != 0 && !IsNewer(incomingStream, closedStreamId)) return false;
                closedStreamId = incomingStream; return true;
            }
            if (incomingStream != streamId)
            {
                if (!IsNewer(incomingStream, streamId)) return false;
                Clear(); closedStreamId = incomingStream; return true;
            }
            if (temporarilyCleared)
            {
                if (!IsNewer(sequence, replayWatermark)) return false;
                closedStreamId = incomingStream; ResetActive(); return true;
            }
            if (sequence < expected || sequence - expected > MaximumFutureFrames + 1) return false;
            ended = true; endSequence = sequence; closedStreamId = incomingStream; lastArrival = now;
            var afterEnd = new List<uint>();
            foreach (uint queued in frames.Keys) if (queued >= endSequence) afterEnd.Add(queued);
            for (int i = 0; i < afterEnd.Count; i++) { frames.Remove(afterEnd[i]); arrivals.Remove(afterEnd[i]); }
            return true;
        }

        public bool TryDequeue(double now, out byte[] payload, out bool concealed)
        {
            payload = null; concealed = false;
            if (!active || temporarilyCleared || !FiniteTime(now)) return false;
            if (ended && expected >= endSequence) { Clear(); return false; }
            // Silence and delayed delivery are not an authenticated stream close. Keep the
            // replay watermark so a newer frame from the same held PTT can resume safely.
            if (now - lastArrival >= IdleSeconds) { ClearTemporary(); return false; }
            if (!primed && now + 0.000001 < nextPlayout) return false;
            if (frames.TryGetValue(expected, out payload))
            {
                frames.Remove(expected); arrivals.Remove(expected); lastPayload = payload;
            }
            else
            {
                if (frames.Count == 0 && !ended) return false;
                // A later frame is queued: give the missing one a short reorder window before declaring it lost.
                if (!ended && now - EarliestQueuedArrival() < ReorderGraceSeconds) return false;
                payload = lastPayload;
                concealed = true;
            }
            expected++; nextPlayout += FrameSeconds;
            if (payload != null) primed = true;
            return payload != null;
        }

        public void Clear()
        {
            if (active && (closedStreamId == 0 || IsNewer(streamId, closedStreamId))) closedStreamId = streamId;
            ResetActive();
        }

        /// <summary>Drops queued audio while preserving the open stream and its replay watermark.</summary>
        public void ClearTemporary()
        {
            if (!active) return;
            frames.Clear(); arrivals.Clear(); lastPayload = null; nextPlayout = lastArrival = 0; primed = false;
            if (ended) { ResetActive(); return; }
            temporarilyCleared = true;
        }

        private void Start(uint incomingStream, uint sequence, double now)
        {
            ResetActive();
            active = true; streamId = incomingStream; expected = sequence;
            nextPlayout = now + PrebufferSeconds; lastArrival = now;
        }

        private void Resume(uint sequence, double now)
        {
            frames.Clear(); arrivals.Clear(); expected = sequence; endSequence = 0; lastPayload = null;
            nextPlayout = now + PrebufferSeconds; lastArrival = now; primed = false;
            temporarilyCleared = false;
        }

        private void ResetActive()
        {
            frames.Clear(); arrivals.Clear(); streamId = expected = endSequence = replayWatermark = 0; nextPlayout = lastArrival = 0;
            active = ended = temporarilyCleared = primed = false; lastPayload = null;
        }

        private double EarliestQueuedArrival()
        {
            double earliest = double.MaxValue;
            foreach (double arrival in arrivals.Values) if (arrival < earliest) earliest = arrival;
            return earliest;
        }

        private static bool IsNewer(uint candidate, uint current) => current == 0 || unchecked((int)(candidate - current)) > 0;
        private static bool FiniteTime(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
