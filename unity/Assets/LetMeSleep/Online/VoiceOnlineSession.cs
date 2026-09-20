using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LetMeSleep.Online
{
    /// <summary>Authenticated, round-scoped voice media. The room session remains identity authority.</summary>
    public sealed class VoiceOnlineSession : IDisposable
    {
        private sealed class Receiver
        {
            internal readonly VoicePeerRoute Route;
            internal readonly VoiceRateLimiter Limiter = new VoiceRateLimiter();
            internal readonly VoiceJitterBuffer Jitter = new VoiceJitterBuffer();
            internal bool CanHearLocal;
            internal bool CanSpeakToLocal;
            internal Receiver(VoicePeerRoute route)
            {
                Route = route;
                CanHearLocal = route.CanHearLocal;
                CanSpeakToLocal = route.CanSpeakToLocal;
            }
        }

        private readonly IVoiceDatagramTransport transport;
        private readonly VoiceImaAdpcmCodec codec;
        private readonly Func<double> clock;
        private readonly Dictionary<string, Receiver> receiversByMember = new Dictionary<string, Receiver>(StringComparer.Ordinal);
        private readonly Dictionary<uint, Receiver> receiversByActor = new Dictionary<uint, Receiver>();
        private readonly HashSet<uint> mutedActors = new HashSet<uint>();
        private VoiceRoundContext context;
        private VoiceRateLimiter outgoingLimiter = new VoiceRateLimiter();
        private readonly VoiceRateLimiter outgoingControlLimiter = new VoiceRateLimiter(4, 2);
        private uint streamId, sequence;
        private bool transmitting, localMuted, focused = true, paused, disposed;
        private float masterVolume = 1f;
        private readonly Dictionary<uint, float> actorVolumes = new Dictionary<uint, float>();

        public event Action<VoiceDecodedFrame> FrameDecoded;
        public event Action LocalTransmissionStopped;
        public bool IsTransmitting => transmitting;
        public bool LocalMuted => localMuted;

        public VoiceOnlineSession(IVoiceDatagramTransport transport, Func<double> clock = null, VoiceImaAdpcmCodec codec = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            this.codec = codec ?? new VoiceImaAdpcmCodec();
            transport.PacketReceived += OnPacketReceived;
        }

        public void UpdateRound(VoiceRoundContext next)
        {
            ThrowIfDisposed();
            if (next == null) { ClearRound(); return; }
            for (int i = 0; i < next.Peers.Count; i++)
                if (!transport.ContainsMember(next.Peers[i].MemberId))
                    throw new InvalidOperationException("Voice context contains a member outside the authenticated room.");
            StopTransmission(true, clock());
            PurgeReceivers();
            context = next;
            for (int i = 0; i < next.Peers.Count; i++)
            {
                var receiver = new Receiver(next.Peers[i]);
                receiversByMember.Add(next.Peers[i].MemberId, receiver);
                receiversByActor.Add(next.Peers[i].ActorId, receiver);
            }
        }

        public bool BeginPushToTalk(double now)
        {
            ThrowIfDisposed();
            if (transmitting || !CanTransmit() || !ValidTime(now)) return false;
            streamId = streamId == uint.MaxValue ? 1 : streamId + 1;
            sequence = 0; transmitting = true; return true;
        }

        public bool SubmitCapturedFrame(float[] samples, double now)
        {
            ThrowIfDisposed();
            if (!transmitting || !CanTransmit() || !ValidTime(now)) { StopTransmission(true, now); return false; }
            if (!outgoingLimiter.TryConsume(now)) return false;
            byte[] payload = codec.Encode(samples);
            sequence = sequence == uint.MaxValue ? 1 : sequence + 1;
            byte[] packet = VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, context.SessionEpoch,
                context.RoundId, context.LocalActorId, streamId, sequence, payload));
            bool sent = false;
            foreach (Receiver receiver in receiversByMember.Values)
            {
                if (receiver.CanHearLocal && transport.ContainsMember(receiver.Route.MemberId))
                    sent |= transport.Send(receiver.Route.MemberId, new ArraySegment<byte>(packet), false);
            }
            return sent;
        }

        public void EndPushToTalk(double now)
        {
            ThrowIfDisposed();
            StopTransmission(ValidTime(now), now);
        }

        public void Tick(double now)
        {
            ThrowIfDisposed();
            if (!ValidTime(now) || context == null || !context.LocalCanListen || localMuted || !focused || paused) return;
            foreach (Receiver receiver in receiversByMember.Values)
            {
                if (mutedActors.Contains(receiver.Route.ActorId)) { receiver.Jitter.ClearTemporary(); continue; }
                for (int emitted = 0; emitted < 3 && receiver.Jitter.TryDequeue(now, out byte[] payload, out bool concealed); emitted++)
                {
                    if (!codec.TryDecode(new ArraySegment<byte>(payload), out float[] samples)) { receiver.Jitter.ClearTemporary(); break; }
                    float volume = masterVolume * (actorVolumes.TryGetValue(receiver.Route.ActorId, out float actorVolume) ? actorVolume : 1f);
                    if (concealed) volume *= 0.85f;
                    float square = 0;
                    for (int i = 0; i < samples.Length; i++) square += samples[i] * samples[i];
                    float level = (float)Math.Sqrt(square / samples.Length) * volume;
                    FrameDecoded?.Invoke(new VoiceDecodedFrame(receiver.Route.ActorId, receiver.Route.IsMosquito, samples, volume, level, concealed));
                }
            }
        }

        public void SetLocalMuted(bool muted)
        {
            ThrowIfDisposed();
            if (localMuted == muted) return;
            localMuted = muted;
            if (muted) { StopTransmission(true, clock()); ClearReceiverBuffers(); }
        }

        public void SetPeerMuted(uint actorId, bool muted)
        {
            ThrowIfDisposed();
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (muted) { mutedActors.Add(actorId); if (receiversByActor.TryGetValue(actorId, out Receiver receiver)) receiver.Jitter.ClearTemporary(); }
            else mutedActors.Remove(actorId);
        }

        /// <summary>Updates local proximity/occlusion routing without replacing authenticated membership or interrupting PTT.</summary>
        public void SetPeerAudibility(uint actorId, bool peerCanHearLocal, bool localCanHearPeer)
        {
            ThrowIfDisposed();
            if (!receiversByActor.TryGetValue(actorId, out Receiver receiver)) throw new ArgumentOutOfRangeException(nameof(actorId));
            receiver.CanHearLocal = peerCanHearLocal;
            receiver.CanSpeakToLocal = localCanHearPeer;
            if (!localCanHearPeer) receiver.Jitter.ClearTemporary();
        }

        public void SetMasterVolume(float volume) { ThrowIfDisposed(); masterVolume = ValidateVolume(volume); }
        public void SetActorVolume(uint actorId, float volume)
        {
            ThrowIfDisposed(); if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            actorVolumes[actorId] = ValidateVolume(volume);
        }

        public void SetApplicationFocused(bool value)
        {
            ThrowIfDisposed(); focused = value;
            if (!value) { StopTransmission(true, clock()); ClearReceiverBuffers(); }
        }

        public void SetApplicationPaused(bool value)
        {
            ThrowIfDisposed(); paused = value;
            if (value) { StopTransmission(true, clock()); ClearReceiverBuffers(); }
        }

        public void ClearRound()
        {
            if (disposed) return;
            StopTransmission(true, clock()); PurgeReceivers(); context = null;
        }

        private void OnPacketReceived(string memberId, ArraySegment<byte> data)
        {
            if (disposed || context == null || !context.LocalCanListen || localMuted || !focused || paused) return;
            if (!receiversByMember.TryGetValue(memberId, out Receiver receiver) || !receiver.CanSpeakToLocal || mutedActors.Contains(receiver.Route.ActorId)) return;
            double now = clock();
            if (!ValidTime(now) || !receiver.Limiter.TryConsume(now) || !VoiceWireCodec.TryDecode(data, out VoicePacket packet)) return;
            if (packet.SessionEpoch != context.SessionEpoch || packet.RoundId != context.RoundId || packet.ActorId != receiver.Route.ActorId) return;
            if (packet.Kind == VoicePacketKind.Audio) receiver.Jitter.AcceptAudio(packet.StreamId, packet.Sequence, packet.Payload, now);
            else receiver.Jitter.AcceptEnd(packet.StreamId, packet.Sequence, now);
        }

        private bool CanTransmit() => context != null && context.LocalCanSpeak && !localMuted && focused && !paused;

        private void StopTransmission(bool notifyPeers, double now)
        {
            if (!transmitting) return;
            if (notifyPeers && context != null && ValidTime(now) && outgoingControlLimiter.TryConsume(now))
            {
                sequence = sequence == uint.MaxValue ? 1 : sequence + 1;
                byte[] packet = VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.End, context.SessionEpoch,
                    context.RoundId, context.LocalActorId, streamId, sequence, Array.Empty<byte>()));
                foreach (Receiver receiver in receiversByMember.Values)
                {
                    if (receiver.CanHearLocal && transport.ContainsMember(receiver.Route.MemberId))
                        transport.Send(receiver.Route.MemberId, new ArraySegment<byte>(packet), true);
                }
            }
            transmitting = false; LocalTransmissionStopped?.Invoke();
        }

        private void PurgeReceivers()
        {
            ClearReceiverBuffers();
            receiversByMember.Clear(); receiversByActor.Clear();
        }

        private void ClearReceiverBuffers()
        {
            foreach (Receiver receiver in receiversByMember.Values) receiver.Jitter.ClearTemporary();
        }

        private static bool ValidTime(double now) => now >= 0 && !double.IsNaN(now) && !double.IsInfinity(now);
        private static float ValidateVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 2) throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(VoiceOnlineSession)); }

        public void Dispose()
        {
            if (disposed) return;
            ClearRound(); disposed = true; transport.PacketReceived -= OnPacketReceived; transport.Dispose();
            FrameDecoded = null; LocalTransmissionStopped = null;
        }
    }
}
