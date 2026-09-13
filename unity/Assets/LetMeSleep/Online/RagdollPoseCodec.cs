using System;
using System.Collections.Generic;
using System.IO;
using LetMeSleep.Gameplay;

namespace LetMeSleep.Online
{
    // Separate opt-in protocol. Not sent by OnlineGameplaySession until rig/lifecycle negotiation exists.
    public enum RagdollRigProfile : ushort { Human17 = 1, Mosquito18 = 2 }

    public readonly struct RagdollBonePose
    {
        public readonly Float3 Position;
        public readonly Rotation Rotation;
        public RagdollBonePose(Float3 position, Rotation rotation) { Position = position; Rotation = rotation; }
    }

    public sealed class RagdollPoseFrame
    {
        public ulong Epoch { get; }
        public ulong Round { get; }
        public uint HostTick { get; }
        public uint ActorId { get; }
        public uint StateRevision { get; }
        public uint Generation { get; }
        public RagdollRigProfile Profile { get; }
        public IReadOnlyList<RagdollBonePose> Bones { get; }
        public RagdollPoseFrame(ulong epoch, ulong round, uint tick, uint actorId, uint stateRevision,
            uint generation, RagdollRigProfile profile, IReadOnlyList<RagdollBonePose> bones)
        {
            Epoch = epoch; Round = round; HostTick = tick; ActorId = actorId;
            StateRevision = stateRevision; Generation = generation; Profile = profile;
            if (bones == null || bones.Count > 18) throw new ArgumentException("Invalid bone count.", nameof(bones));
            var copy = new RagdollBonePose[bones.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = bones[i];
            Bones = Array.AsReadOnly(copy);
        }
    }

    /// <summary>Absolute world poses with fixed profile order. Transport must authenticate the owner;
    /// decoding alone never grants simulation authority or starts a fall.</summary>
    public static class RagdollPoseCodec
    {
        public const int HeaderBytes = 42, MaximumBytes = 546;
        public const ushort Version = 1;
        private const uint Magic = 0x524D534C;
        private const byte PoseKind = 1;
        public static int BodyCount(RagdollRigProfile profile) =>
            profile == RagdollRigProfile.Human17 ? 17 : profile == RagdollRigProfile.Mosquito18 ? 18 : 0;

        public static byte[] Encode(RagdollPoseFrame frame)
        {
            if (!IsValid(frame)) throw new ArgumentException("Invalid ragdoll frame.", nameof(frame));
            using var stream = new MemoryStream(MaximumBytes);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic); writer.Write(Version); writer.Write(PoseKind);
            writer.Write(frame.Epoch); writer.Write(frame.Round); writer.Write(frame.HostTick);
            writer.Write(frame.ActorId); writer.Write(frame.StateRevision); writer.Write(frame.Generation);
            writer.Write((ushort)frame.Profile); writer.Write((byte)frame.Bones.Count);
            foreach (var bone in frame.Bones)
            {
                writer.Write(bone.Position.X); writer.Write(bone.Position.Y); writer.Write(bone.Position.Z);
                writer.Write(bone.Rotation.X); writer.Write(bone.Rotation.Y);
                writer.Write(bone.Rotation.Z); writer.Write(bone.Rotation.W);
            }
            writer.Flush(); return stream.ToArray();
        }

        public static bool TryDecode(byte[] data, out RagdollPoseFrame frame)
        {
            frame = null;
            if (data == null || (data.Length != HeaderBytes + 17 * 28 && data.Length != MaximumBytes)) return false;
            try
            {
                using var stream = new MemoryStream(data, false);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version || reader.ReadByte() != PoseKind) return false;
                ulong epoch = reader.ReadUInt64(), round = reader.ReadUInt64();
                uint tick = reader.ReadUInt32(), actor = reader.ReadUInt32(), revision = reader.ReadUInt32(), generation = reader.ReadUInt32();
                var profile = (RagdollRigProfile)reader.ReadUInt16();
                int count = reader.ReadByte();
                if (count != BodyCount(profile) || count == 0 || data.Length != HeaderBytes + count * 28) return false;
                var bones = new RagdollBonePose[count];
                for (int i = 0; i < count; i++)
                    bones[i] = new RagdollBonePose(new Float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        new Rotation(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                var parsed = new RagdollPoseFrame(epoch, round, tick, actor, revision, generation, profile, bones);
                if (stream.Position != stream.Length || !IsValid(parsed)) return false;
                frame = parsed; return true;
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
        }

        public static bool IsValid(RagdollPoseFrame frame)
        {
            if (frame == null || frame.Epoch == 0 || frame.Round == 0 || frame.ActorId == 0 ||
                frame.Generation == 0 || frame.StateRevision == 0 || frame.HostTick > 54000 || BodyCount(frame.Profile) == 0 ||
                frame.Bones.Count != BodyCount(frame.Profile)) return false;
            // Coarse packet boundary, not a claim that a distorted rig is physically acceptable.
            // Exact parent lengths and orientation limits remain the negotiated rig's responsibility.
            Float3 origin = frame.Bones[0].Position;
            float maximumExtent = frame.Profile == RagdollRigProfile.Human17 ? 3f : 2f;
            foreach (var bone in frame.Bones)
            {
                var p = bone.Position; var q = bone.Rotation;
                if (!p.IsFinite || Math.Abs(p.X) > 10000 || Math.Abs(p.Y) > 10000 || Math.Abs(p.Z) > 10000 ||
                    (p - origin).LengthSquared > maximumExtent * maximumExtent) return false;
                if (!MathEx.Finite(q.X) || !MathEx.Finite(q.Y) || !MathEx.Finite(q.Z) || !MathEx.Finite(q.W) ||
                    Math.Abs(q.X) > 1 || Math.Abs(q.Y) > 1 || Math.Abs(q.Z) > 1 || Math.Abs(q.W) > 1 ||
                    Math.Abs(q.X*q.X + q.Y*q.Y + q.Z*q.Z + q.W*q.W - 1) > .002f) return false;
            }
            return true;
        }
    }
}
