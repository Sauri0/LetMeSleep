using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class VoiceNetworkTests
    {
        [Test]
        public void ImaAdpcm_HasFixedPayloadDurationAndBoundedSyntheticError()
        {
            float[] input = Sine(440, 0.4f);
            var codec = new VoiceImaAdpcmCodec();
            byte[] payload = codec.Encode(input);
            Assert.That(payload.Length, Is.EqualTo(126));
            Assert.That(codec.TryDecode(new ArraySegment<byte>(payload), out float[] output), Is.True);
            Assert.That(output.Length, Is.EqualTo(input.Length));
            double square = 0;
            for (int i = 0; i < input.Length; i++) square += Math.Pow(input[i] - output[i], 2);
            Assert.That(Math.Sqrt(square / input.Length), Is.LessThan(0.02));
            payload[5] = 89;
            Assert.That(codec.TryDecode(new ArraySegment<byte>(payload), out _), Is.False);
        }

        [Test]
        public void WireCodec_AuthenticatesAllRoundFieldsAndRejectsTrailingOrMalformedData()
        {
            byte[] codecPayload = new VoiceImaAdpcmCodec().Encode(Sine(220, 0.25f));
            var original = new VoicePacket(VoicePacketKind.Audio, 7, 9, 11, 13, 17, codecPayload);
            byte[] encoded = VoiceWireCodec.Encode(original);
            Assert.That(encoded.Length, Is.EqualTo(165));
            Assert.That(VoiceWireCodec.TryDecode(new ArraySegment<byte>(encoded), out VoicePacket decoded), Is.True);
            Assert.That((decoded.SessionEpoch, decoded.RoundId, decoded.ActorId, decoded.StreamId, decoded.Sequence), Is.EqualTo((7UL, 9UL, 11U, 13U, 17U)));
            Assert.That(decoded.Payload, Is.EqualTo(codecPayload));
            Assert.That(VoiceWireCodec.TryDecode(new ArraySegment<byte>(encoded.Concat(new byte[] { 0 }).ToArray()), out _), Is.False);
            encoded[4] = 1;
            Assert.That(VoiceWireCodec.TryDecode(new ArraySegment<byte>(encoded), out _), Is.False);
        }

        [Test]
        public void JitterBuffer_ReordersConcealsOneGapRejectsReplayAndPurgesIdle()
        {
            byte[] one = FilledPayload(1), three = FilledPayload(3);
            var jitter = new VoiceJitterBuffer();
            Assert.That(jitter.AcceptAudio(5, 1, one, 0), Is.True);
            Assert.That(jitter.AcceptAudio(5, 3, three, 0.01), Is.True);
            Assert.That(jitter.AcceptAudio(5, 1, one, 0.02), Is.False);
            Assert.That(jitter.TryDequeue(0.059, out _, out _), Is.False);
            Assert.That(jitter.TryDequeue(0.060, out byte[] first, out bool firstConcealed), Is.True);
            Assert.That(first, Is.EqualTo(one)); Assert.That(firstConcealed, Is.False);
            Assert.That(jitter.TryDequeue(0.080, out byte[] concealed, out bool wasConcealed), Is.True);
            Assert.That(concealed, Is.EqualTo(one)); Assert.That(wasConcealed, Is.True);
            Assert.That(jitter.TryDequeue(0.100, out byte[] third, out bool thirdConcealed), Is.True);
            Assert.That(third, Is.EqualTo(three)); Assert.That(thirdConcealed, Is.False);
            Assert.That(jitter.TryDequeue(0.320, out _, out _), Is.False);
            Assert.That(jitter.IsActive, Is.False);
        }

        [Test]
        public void JitterBuffer_EndBoundsFramesAndClosedStreamCannotResurrect()
        {
            byte[] one = FilledPayload(1), afterEnd = FilledPayload(3);
            var jitter = new VoiceJitterBuffer();
            Assert.That(jitter.AcceptAudio(5, 1, one, 0), Is.True);
            Assert.That(jitter.AcceptAudio(5, 3, afterEnd, 0.01), Is.True);
            Assert.That(jitter.AcceptEnd(5, 3, 0.02), Is.True);
            Assert.That(jitter.QueuedFrames, Is.EqualTo(1));
            Assert.That(jitter.TryDequeue(0.06, out byte[] first, out bool concealed), Is.True);
            Assert.That(first, Is.EqualTo(one)); Assert.That(concealed, Is.False);
            Assert.That(jitter.TryDequeue(0.08, out byte[] lost, out concealed), Is.True);
            Assert.That(lost, Is.EqualTo(one)); Assert.That(concealed, Is.True);
            Assert.That(jitter.TryDequeue(0.10, out _, out _), Is.False);
            Assert.That(jitter.AcceptAudio(5, 4, afterEnd, 0.11), Is.False);
            Assert.That(jitter.AcceptAudio(6, 1, one, 0.12), Is.True);
        }

        [Test]
        public void RateLimiter_IsBoundedAndRejectsTimeRollback()
        {
            var limiter = new VoiceRateLimiter();
            for (int i = 0; i < VoiceProtocol.RateBurst; i++) Assert.That(limiter.TryConsume(1), Is.True);
            Assert.That(limiter.TryConsume(1), Is.False);
            Assert.That(limiter.TryConsume(0.5), Is.False);
            Assert.That(limiter.TryConsume(1.020), Is.True);
        }

        [Test]
        public void Session_RequiresRoomMembershipAndAuthenticatedActor()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            var route = new VoicePeerRoute("peer-a", 22, true, true, true);
            session.UpdateRound(new VoiceRoundContext(3, 4, 11, true, true, new[] { route }));
            int decoded = 0; session.FrameDecoded += _ => decoded++;
            byte[] payload = new VoiceImaAdpcmCodec().Encode(Sine(330, 0.2f));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 3, 4, 99, 1, 1, payload)));
            now = 0.1; session.Tick(now); Assert.That(decoded, Is.Zero);
            now = 1;
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 3, 4, 22, 1, 1, payload)));
            now = 1.06; session.Tick(now); Assert.That(decoded, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => session.UpdateRound(new VoiceRoundContext(3, 4, 11, true, true,
                new[] { new VoicePeerRoute("outsider", 33, true, true, false) })));
        }

        [Test]
        public void Session_PttMuteFocusAndRoundChangesStopAndPurge()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(8, 9, 10, true, true,
                new[] { new VoicePeerRoute("peer-a", 20, true, true, false) }));
            Assert.That(session.SubmitCapturedFrame(Sine(200, 0.1f), now), Is.False);
            Assert.That(session.BeginPushToTalk(now), Is.True);
            Assert.That(session.SubmitCapturedFrame(Sine(200, 0.1f), now), Is.True);
            Assert.That(transport.Sent.Count, Is.EqualTo(1));
            Assert.That(transport.Sent[0].Reliable, Is.False);
            session.SetApplicationFocused(false);
            Assert.That(session.IsTransmitting, Is.False);
            Assert.That(transport.Sent.Last().Reliable, Is.True);
            session.SetApplicationFocused(true); session.SetLocalMuted(true);
            Assert.That(session.BeginPushToTalk(1), Is.False);
            session.SetLocalMuted(false);
            Assert.That(session.BeginPushToTalk(1), Is.True, "Focus/mute purge must preserve authenticated routes.");
            Assert.That(session.SubmitCapturedFrame(Sine(200, 0.1f), 1), Is.True);
            session.EndPushToTalk(1);
            session.UpdateRound(null);
            Assert.That(session.BeginPushToTalk(2), Is.False);
        }

        [Test]
        public void Session_BoundsBurstAndPeerMuteDropsBufferedAudio()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            Assert.That(session.BeginPushToTalk(now), Is.True);
            for (int i = 0; i < 30; i++) session.SubmitCapturedFrame(Sine(100 + i, 0.1f), now);
            Assert.That(transport.Sent.Count, Is.EqualTo(VoiceProtocol.RateBurst));
            int heard = 0; session.FrameDecoded += _ => heard++;
            byte[] payload = new VoiceImaAdpcmCodec().Encode(Sine(300, 0.2f));
            now = 2; transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 1, 1, payload)));
            session.SetPeerMuted(4, true); now = 2.1; session.Tick(now);
            Assert.That(heard, Is.Zero);
        }

        [Test]
        public void Session_AudibilityChangesDoNotRestartPttAndPurgeHiddenPeer()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            Assert.That(session.BeginPushToTalk(now), Is.True);
            session.SetPeerAudibility(4, false, false);
            Assert.That(session.IsTransmitting, Is.True);
            Assert.That(session.SubmitCapturedFrame(Sine(220, 0.1f), now), Is.False);
            byte[] payload = new VoiceImaAdpcmCodec().Encode(Sine(300, 0.2f));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 1, 1, payload)));
            int heard = 0; session.FrameDecoded += _ => heard++;
            now = 0.1; session.Tick(now); Assert.That(heard, Is.Zero);
            session.SetPeerAudibility(4, true, true);
            now = 0.2; Assert.That(session.SubmitCapturedFrame(Sine(220, 0.1f), now), Is.True);
        }

        [Test]
        public void Session_PttRestartCannotRefillMediaOrControlRateLimiters()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            for (int i = 0; i < 30; i++)
            {
                Assert.That(session.BeginPushToTalk(now), Is.True);
                session.SubmitCapturedFrame(Sine(200, 0.1f), now);
                session.EndPushToTalk(now);
            }
            int audio = 0, end = 0;
            foreach (FakeTransport.SendRecord sent in transport.Sent)
            {
                Assert.That(VoiceWireCodec.TryDecode(new ArraySegment<byte>(sent.Packet), out VoicePacket packet), Is.True);
                if (packet.Kind == VoicePacketKind.Audio) audio++; else end++;
            }
            Assert.That(audio, Is.EqualTo(VoiceProtocol.RateBurst));
            Assert.That(end, Is.LessThanOrEqualTo(2));
        }

        private static float[] Sine(double hertz, float amplitude)
        {
            var samples = new float[VoiceProtocol.FrameSamples];
            for (int i = 0; i < samples.Length; i++) samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * hertz * i / VoiceProtocol.SampleRate);
            return samples;
        }
        private static byte[] FilledPayload(byte value) => Enumerable.Repeat(value, VoiceProtocol.MaximumCodecBytes).ToArray();

        private sealed class FakeTransport : IVoiceDatagramTransport
        {
            internal sealed class SendRecord { internal string Member; internal byte[] Packet; internal bool Reliable; }
            private readonly HashSet<string> members;
            internal readonly List<SendRecord> Sent = new List<SendRecord>();
            public event Action<string, ArraySegment<byte>> PacketReceived;
            internal FakeTransport(params string[] members) { this.members = new HashSet<string>(members, StringComparer.Ordinal); }
            public bool ContainsMember(string memberId) => members.Contains(memberId);
            public bool Send(string memberId, ArraySegment<byte> packet, bool reliable)
            {
                if (!ContainsMember(memberId)) return false;
                var copy = new byte[packet.Count]; Buffer.BlockCopy(packet.Array, packet.Offset, copy, 0, packet.Count);
                Sent.Add(new SendRecord { Member = memberId, Packet = copy, Reliable = reliable }); return true;
            }
            internal void Receive(string member, byte[] packet) => PacketReceived?.Invoke(member, new ArraySegment<byte>(packet));
            public void Dispose() { PacketReceived = null; }
        }
    }
}
