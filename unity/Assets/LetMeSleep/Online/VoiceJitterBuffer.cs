using System;
using System.Collections.Generic;

namespace LetMeSleep.Online
{
    /// <summary>Small ordered playout queue with one-frame loss concealment requests.</summary>
    public sealed class VoiceJitterBuffer
    {
        public const int MaximumQueuedFrames = 12;
        public const uint MaximumFutureFrames = 24;
        public const double PrebufferSeconds = 0.060;
        public const double FrameSeconds = 0.020;
        public const double IdleSeconds = 0.300;

        private readonly SortedDictionary<uint, byte[]> frames = new SortedDictionary<uint, byte[]>();
        private uint streamId, expected, endSequence, closedStreamId;
        private double nextPlayout, lastArrival;
        private bool active, ended;
        private byte[] lastPayload;

        public bool IsActive => active;
        public int QueuedFrames => frames.Count;

        public bool AcceptAudio(uint incomingStream, uint sequence, byte[] payload, double now)
        {
            if (incomingStream == 0 || sequence == 0 || payload == null || payload.Length != VoiceProtocol.MaximumCodecBytes || !FiniteTime(now)) return false;
            if (closedStreamId != 0 && !IsNewer(incomingStream, closedStreamId)) return false;
            if (!active || IsNewer(incomingStream, streamId)) Start(incomingStream, sequence, now);
            else if (incomingStream != streamId || ended) return false;
            if (sequence < expected || sequence - expected > MaximumFutureFrames || frames.ContainsKey(sequence) || frames.Count >= MaximumQueuedFrames) return false;
            var copy = new byte[payload.Length]; Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
            frames.Add(sequence, copy); lastArrival = now; return true;
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
            if (sequence < expected || sequence - expected > MaximumFutureFrames + 1) return false;
            ended = true; endSequence = sequence; closedStreamId = incomingStream; lastArrival = now;
            var afterEnd = new List<uint>();
            foreach (uint queued in frames.Keys) if (queued >= endSequence) afterEnd.Add(queued);
            for (int i = 0; i < afterEnd.Count; i++) frames.Remove(afterEnd[i]);
            return true;
        }

        public bool TryDequeue(double now, out byte[] payload, out bool concealed)
        {
            payload = null; concealed = false;
            if (!active || !FiniteTime(now)) return false;
            if (now - lastArrival >= IdleSeconds || (ended && expected >= endSequence)) { Clear(); return false; }
            if (now + 0.000001 < nextPlayout) return false;
            if (frames.TryGetValue(expected, out payload))
            {
                frames.Remove(expected); lastPayload = payload;
            }
            else
            {
                if (frames.Count == 0 && !ended) return false;
                payload = lastPayload;
                concealed = true;
            }
            expected++; nextPlayout += FrameSeconds;
            return payload != null;
        }

        public void Clear()
        {
            if (active && (closedStreamId == 0 || IsNewer(streamId, closedStreamId))) closedStreamId = streamId;
            frames.Clear(); streamId = expected = endSequence = 0; nextPlayout = lastArrival = 0;
            active = ended = false; lastPayload = null;
        }

        private void Start(uint incomingStream, uint sequence, double now)
        {
            frames.Clear(); streamId = expected = endSequence = 0; nextPlayout = lastArrival = 0;
            active = ended = false; lastPayload = null;
            active = true; streamId = incomingStream; expected = sequence;
            nextPlayout = now + PrebufferSeconds; lastArrival = now;
        }

        private static bool IsNewer(uint candidate, uint current) => current == 0 || unchecked((int)(candidate - current)) > 0;
        private static bool FiniteTime(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
