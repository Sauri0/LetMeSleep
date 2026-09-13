using System;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class RagdollPoseProtocolTests
    {
        private static RagdollPoseFrame Frame(uint tick = 100, uint generation = 1, uint revision = 4,
            RagdollRigProfile profile = RagdollRigProfile.Human17, uint actor = 1, ulong round = 1)
        {
            var bones = new RagdollBonePose[RagdollPoseCodec.BodyCount(profile)];
            for (int i = 0; i < bones.Length; i++) bones[i] = new RagdollBonePose(new Float3(2, i * .04f, 5), Rotation.Identity);
            return new RagdollPoseFrame(1, round, tick, actor, revision, generation, profile, bones);
        }
        private static RagdollPoseGate Gate()
        {
            var gate = new RagdollPoseGate(); gate.Reset(1, 1);
            Assert.True(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 4, 90, 100, true));
            return gate;
        }

        [TestCase(RagdollRigProfile.Human17, 518)]
        [TestCase(RagdollRigProfile.Mosquito18, 546)]
        public void AbsolutePoseFitsOneTransportPacket(RagdollRigProfile profile, int bytes)
        {
            var original = Frame(profile: profile); var encoded = RagdollPoseCodec.Encode(original);
            Assert.AreEqual(bytes, encoded.Length); Assert.Less(encoded.Length + 10, 1170);
            Assert.True(RagdollPoseCodec.TryDecode(encoded, out var decoded));
            Assert.AreEqual(original.Bones.Count, decoded.Bones.Count);
            for (int i = 0; i < original.Bones.Count; i++) Assert.AreEqual(original.Bones[i].Position.Y, decoded.Bones[i].Position.Y);
        }

        [Test]
        public void RejectsEveryTruncationTrailingDataAndWrongProfileCount()
        {
            var bytes = RagdollPoseCodec.Encode(Frame());
            for (int n = 0; n < bytes.Length; n++)
            {
                var truncated = new byte[n]; Array.Copy(bytes, truncated, n);
                Assert.False(RagdollPoseCodec.TryDecode(truncated, out _), "length " + n);
            }
            var trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
            Assert.False(RagdollPoseCodec.TryDecode(trailing, out _));
            bytes[41] = 18; Assert.False(RagdollPoseCodec.TryDecode(bytes, out _));
            bytes[41] = 17; bytes[39] = 255; Assert.False(RagdollPoseCodec.TryDecode(bytes, out _));
        }

        [Test]
        public void RejectsNonfinitePositionAndInvalidQuaternion()
        {
            var source = RagdollPoseCodec.Encode(Frame());
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 10001f })
            {
                var bytes = (byte[])source.Clone(); Array.Copy(BitConverter.GetBytes(value), 0, bytes, 42, 4);
                Assert.False(RagdollPoseCodec.TryDecode(bytes, out _));
            }
            var zero = (byte[])source.Clone(); Array.Clear(zero, 54, 16);
            Assert.False(RagdollPoseCodec.TryDecode(zero, out _));
            var stretched = (byte[])source.Clone(); Array.Copy(BitConverter.GetBytes(20f), 0, stretched, 70, 4);
            Assert.False(RagdollPoseCodec.TryDecode(stretched, out _));
        }

        [Test]
        public void PosesCannotStartFallOrBypassAuthenticatedOwner()
        {
            var gate = new RagdollPoseGate(); gate.Reset(1, 1);
            Assert.False(gate.TryAccept(Frame(), true, 100));
            gate = Gate(); Assert.False(gate.TryAccept(Frame(), false, 100));
            Assert.False(gate.TryAccept(Frame(actor: 2), true, 100));
            Assert.False(gate.TryAccept(Frame(round: 2), true, 100));
            Assert.False(gate.TryAccept(Frame(profile: RagdollRigProfile.Mosquito18), true, 100));
            Assert.True(gate.TryAccept(Frame(), true, 100));
        }

        [Test]
        public void DropsDuplicateReorderedStaleAndFutureSamples()
        {
            var gate = Gate(); Assert.True(gate.TryAccept(Frame(), true, 100));
            Assert.False(gate.TryAccept(Frame(), true, 100));
            Assert.False(gate.TryAccept(Frame(99), true, 100));
            Assert.False(gate.TryAccept(Frame(107), true, 100));
            Assert.False(gate.TryAccept(Frame(101), true, 132));
            Assert.True(gate.TryAccept(Frame(102), true, 103));
        }

        [Test]
        public void RecoveryCannotBeUndoneByLatePoseOrStaleLifecycle()
        {
            var gate = Gate(); Assert.True(gate.TryAccept(Frame(), true, 100));
            Assert.True(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 5, 90, 110, false));
            Assert.False(gate.TryAccept(Frame(111, revision: 5), true, 111));
            Assert.False(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 4, 90, 100, true));
            Assert.False(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 6, 90, 112, true));
            Assert.True(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 2, 6, 120, 121, true));
            Assert.False(gate.TryAccept(Frame(121, generation: 1, revision: 6), true, 121));
            Assert.True(gate.TryAccept(Frame(121, generation: 2, revision: 6), true, 121));
            gate.Reset(1, 2); Assert.False(gate.TryAccept(Frame(121, generation: 2, revision: 6), true, 121));
        }

        [Test]
        public void RevisionChangeKeepsWatermarkAndRejectsPretransitionSamples()
        {
            var gate = Gate(); Assert.True(gate.TryAccept(Frame(105), true, 105));
            Assert.True(gate.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 5, 90, 110, true));
            Assert.False(gate.TryAccept(Frame(101, revision: 5), true, 110));
            Assert.False(gate.TryAccept(Frame(106, revision: 5), true, 110));
            Assert.True(gate.TryAccept(Frame(110, revision: 5), true, 110));
            var ahead = Gate(); Assert.True(ahead.TryAccept(Frame(115), true, 115));
            Assert.True(ahead.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 5, 90, 110, true));
            Assert.False(ahead.TryAccept(Frame(112, revision: 5), true, 116));
            Assert.True(ahead.TryAccept(Frame(116, revision: 5), true, 116));
            Assert.False(RagdollPoseCodec.IsValid(Frame(revision: 0)));
            var empty = new RagdollPoseGate(); empty.Reset(1, 1);
            Assert.False(empty.ObserveLifecycle(1, RagdollRigProfile.Human17, 1, 0, 0, 0, true));
        }

        [Test]
        public void ImmutableFrameDoesNotBorrowCallersArray()
        {
            var bones = new RagdollBonePose[17];
            for (int i = 0; i < bones.Length; i++) bones[i] = new RagdollBonePose(Float3.Zero, Rotation.Identity);
            var frame = new RagdollPoseFrame(1, 1, 1, 1, 1, 1, RagdollRigProfile.Human17, bones);
            bones[0] = new RagdollBonePose(new Float3(float.NaN, 0, 0), default);
            Assert.True(RagdollPoseCodec.IsValid(frame));
        }
    }
}
