using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
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
        public void JitterBuffer_ReordersConcealsOneGapRejectsReplayAndTemporarilyPurgesIdle()
        {
            byte[] one = FilledPayload(1), three = FilledPayload(3);
            var jitter = new VoiceJitterBuffer();
            Assert.That(jitter.AcceptAudio(5, 1, one, 0), Is.True);
            Assert.That(jitter.AcceptAudio(5, 3, three, 0.01), Is.True);
            Assert.That(jitter.AcceptAudio(5, 1, one, 0.02), Is.False);
            double p = VoiceJitterBuffer.PrebufferSeconds;
            Assert.That(jitter.TryDequeue(p - 0.001, out _, out _), Is.False);
            Assert.That(jitter.TryDequeue(p, out byte[] first, out bool firstConcealed), Is.True);
            Assert.That(first, Is.EqualTo(one)); Assert.That(firstConcealed, Is.False);
            Assert.That(jitter.TryDequeue(p + 0.020, out byte[] concealed, out bool wasConcealed), Is.True);
            Assert.That(concealed, Is.EqualTo(one)); Assert.That(wasConcealed, Is.True);
            Assert.That(jitter.TryDequeue(p + 0.040, out byte[] third, out bool thirdConcealed), Is.True);
            Assert.That(third, Is.EqualTo(three)); Assert.That(thirdConcealed, Is.False);
            Assert.That(jitter.TryDequeue(0.320, out _, out _), Is.False);
            Assert.That(jitter.IsActive, Is.True, "An idle network interval is not an authenticated End packet.");
            Assert.That(jitter.AcceptAudio(5, 3, three, 0.33), Is.False, "Idle purge retains the replay watermark.");
            Assert.That(jitter.AcceptAudio(5, 5, three, 0.34), Is.True, "A delayed newer frame can resume the same held PTT stream.");
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
        public void JitterBuffer_TemporaryClearResumesSameStreamWithoutReplayOrCrossingEnd()
        {
            byte[] one = FilledPayload(1), three = FilledPayload(3);
            var jitter = new VoiceJitterBuffer();
            Assert.That(jitter.AcceptAudio(5, 1, one, 0), Is.True);
            jitter.ClearTemporary();
            Assert.That(jitter.TryDequeue(1, out _, out _), Is.False, "A temporary gate must not idle-close the open stream.");
            Assert.That(jitter.AcceptAudio(5, 1, one, 1.01), Is.False, "Temporary purge must retain the replay watermark.");
            Assert.That(jitter.AcceptAudio(5, 3, three, 1.02), Is.True, "A newer sequence from the same PTT stream must resume playout.");
            Assert.That(jitter.TryDequeue(1.08, out byte[] resumed, out bool concealed), Is.True);
            Assert.That(resumed, Is.EqualTo(three));
            Assert.That(concealed, Is.False);
            Assert.That(jitter.AcceptEnd(5, 4, 1.09), Is.True);
            jitter.ClearTemporary();
            Assert.That(jitter.AcceptAudio(5, 4, three, 1.10), Is.False);
            Assert.That(jitter.AcceptAudio(5, 5, three, 1.11), Is.False);
            Assert.That(jitter.AcceptAudio(6, 1, one, 1.12), Is.True);
            jitter.ClearTemporary();
            Assert.That(jitter.AcceptEnd(6, 100, 1.13), Is.True, "End after an intentionally ignored interval must still close the stream.");
            Assert.That(jitter.AcceptAudio(6, 101, three, 1.14), Is.False);
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
        public void Session_PeerMuteFollowsAuthenticatedMemberAcrossRounds()
        {
            using var transport = new FakeTransport("peer-a", "peer-b");
            using var session = new VoiceOnlineSession(transport, () => 0);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            session.SetPeerMuted(4, true);
            Assert.That(session.IsMemberMuted("peer-a"), Is.True);

            session.UpdateRound(new VoiceRoundContext(1, 3, 8, true, true, new[]
            {
                new VoicePeerRoute("peer-a", 9, true, true, false),
                new VoicePeerRoute("peer-b", 4, true, true, false)
            }));
            Assert.That(session.IsMemberMuted("peer-a"), Is.True, "Mute belongs to the authenticated room identity, not a per-round actor id.");
            Assert.That(session.IsMemberMuted("peer-b"), Is.False, "Actor-id reuse must not transfer mute to another member.");
        }

        [Test]
        public void Session_LocalMicrophoneMuteStopsSendingButKeepsIncomingVoice()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            session.SetLocalMuted(true);
            Assert.That(session.BeginPushToTalk(now), Is.False);
            int heard = 0; session.FrameDecoded += _ => heard++;
            byte[] payload = new VoiceImaAdpcmCodec().Encode(Sine(300, .2f));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 1, 1, payload)));
            now = .06; session.Tick(now);
            Assert.That(heard, Is.EqualTo(1), "Silencing the local microphone must not deafen incoming room voice.");
        }

        [Test]
        public void VoiceUsesDedicatedChannelFourWithoutCollidingWithLobbyMovement()
        {
            Assert.That(VoiceProtocol.Channel, Is.EqualTo(4));
            Assert.That(EosPeerTransport.MaximumChannel, Is.EqualTo(4));
            Assert.That(EosPeerTransport.IsSupportedChannel(0), Is.True);
            Assert.That(EosPeerTransport.IsSupportedChannel(3), Is.True, "Lobby movement remains on channel 3.");
            Assert.That(EosPeerTransport.IsSupportedChannel(4), Is.True, "Voice owns channel 4.");
            Assert.That(EosPeerTransport.IsSupportedChannel(5), Is.False);
        }

        [Test]
        public void JitterBuffer_ReorderedFrameInsideTheGraceWindowIsPlayedNotConcealed()
        {
            byte[] one = FilledPayload(1), two = FilledPayload(2), three = FilledPayload(3);
            var jitter = new VoiceJitterBuffer();
            double p = VoiceJitterBuffer.PrebufferSeconds;
            Assert.That(jitter.AcceptAudio(5, 1, one, 0), Is.True);
            Assert.That(jitter.TryDequeue(p, out byte[] first, out _), Is.True);
            Assert.That(first, Is.EqualTo(one));
            Assert.That(jitter.AcceptAudio(5, 3, three, p + 0.001), Is.True);
            Assert.That(jitter.TryDequeue(p + 0.010, out _, out _), Is.False, "Frame 2 may still arrive: wait inside the reorder grace.");
            Assert.That(jitter.AcceptAudio(5, 2, two, p + 0.015), Is.True);
            Assert.That(jitter.TryDequeue(p + 0.016, out byte[] second, out bool concealed), Is.True);
            Assert.That(second, Is.EqualTo(two)); Assert.That(concealed, Is.False);
            Assert.That(jitter.TryDequeue(p + 0.017, out byte[] third, out _), Is.True, "After the prebuffer frames flow as they arrive (the playout owns timing).");
            Assert.That(third, Is.EqualTo(three));
        }

        [Test]
        public void Session_RaisesStreamClosedAfterTheLastFrameOfAnEndedStream()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true, new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            var closed = new List<uint>();
            int frames = 0;
            session.StreamClosed += closed.Add;
            session.FrameDecoded += _ => frames++;
            byte[] payload = new VoiceImaAdpcmCodec().Encode(Sine(300, 0.2f));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 1, 1, payload)));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 1, 2, payload)));
            transport.Receive("peer-a", VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.End, 1, 2, 4, 1, 3, Array.Empty<byte>())));
            now = VoiceJitterBuffer.PrebufferSeconds + 0.001; session.Tick(now);
            Assert.That(frames, Is.EqualTo(2));
            Assert.That(closed, Is.EqualTo(new[] { 4u }), "The playout drains and fades on StreamClosed.");
            now += 0.02; session.Tick(now);
            Assert.That(closed.Count, Is.EqualTo(1), "Raised once per stream.");
        }

        [Test]
        public void SpatialPolicy_UsesApprovedRoleDistancesOcclusionAndEliminatedCohorts()
        {
            var origin = Float3.Zero;
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Human, false, origin, PlayerRole.Mosquito, false,
                new Float3(2f, 0, 0), false).Gain, Is.EqualTo(1).Within(.0001), "0 dB at the 2 m reference.");
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Human, false, origin, PlayerRole.Mosquito, false,
                new Float3(3.9f, 0, 0), false).Gain, Is.InRange(.55f, .7f), "Natural fall-off, no flat plateau.");
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Human, false, origin, PlayerRole.Human, false,
                new Float3(12f, 0, 0), false).Audible, Is.False);
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Mosquito, false, origin, PlayerRole.Human, false,
                new Float3(8f, 0, 0), false).Audible, Is.False);
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Mosquito, false, origin, PlayerRole.Mosquito, false,
                new Float3(12f, 0, 0), false).Audible, Is.True);
            var blocked = VoiceSpatialPolicy.Evaluate(PlayerRole.Human, true, origin, PlayerRole.Human, true,
                new Float3(4f, 0, 0), true);
            Assert.That(blocked.Gain, Is.EqualTo(VoiceSpatialPolicy.DistanceGain(4f, 12f) * VoiceSpatialPolicy.OccludedGain).Within(.0001));
            Assert.That(blocked.LowPassHertz, Is.EqualTo(VoiceSpatialPolicy.OccludedLowPassHertz).Within(1f));
            Assert.That(VoiceSpatialPolicy.Evaluate(PlayerRole.Human, true, origin, PlayerRole.Human, false,
                new Float3(1f, 0, 0), false).Audible, Is.False);
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
        public void Session_TemporaryListeningGatesResumeNewSequencesFromSameStream()
        {
            double now = 0;
            using var transport = new FakeTransport("peer-a");
            using var session = new VoiceOnlineSession(transport, () => now);
            session.UpdateRound(new VoiceRoundContext(1, 2, 3, true, true,
                new[] { new VoicePeerRoute("peer-a", 4, true, true, false) }));
            int heard = 0;
            session.FrameDecoded += _ => heard++;
            byte[] frame = new VoiceImaAdpcmCodec().Encode(Sine(300, 0.2f));

            Receive(1); session.SetPeerAudibility(4, true, false); session.SetPeerAudibility(4, true, true);
            now = 0.5; session.Tick(now);
            Receive(1); Receive(3); now = 0.56; session.Tick(now);
            Assert.That(heard, Is.EqualTo(1), "Audibility recovery must play only the new sequence from the open stream.");

            now = 0.57; Receive(4); session.SetPeerMuted(4, true); session.SetPeerMuted(4, false);
            Receive(4); Receive(6); now = 0.63; session.Tick(now);
            Assert.That(heard, Is.EqualTo(2), "Unmute must resume the same stream without accepting replay.");

            now = 0.64; Receive(7); session.SetApplicationFocused(false); session.SetApplicationFocused(true);
            Receive(7); Receive(9); now = 0.70; session.Tick(now);
            Assert.That(heard, Is.EqualTo(3), "Focus recovery must resume the same stream without accepting replay.");

            now = 0.71; Receive(10); session.SetApplicationPaused(true); session.SetApplicationPaused(false);
            Receive(10); Receive(12); now = 0.77; session.Tick(now);
            Assert.That(heard, Is.EqualTo(4), "Pause recovery must resume the same stream without accepting replay.");

            void Receive(uint packetSequence) => transport.Receive("peer-a", VoiceWireCodec.Encode(
                new VoicePacket(VoicePacketKind.Audio, 1, 2, 4, 5, packetSequence, frame)));
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
