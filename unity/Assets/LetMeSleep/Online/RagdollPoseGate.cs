using System;
using System.Collections.Generic;
using LetMeSleep.Gameplay;

namespace LetMeSleep.Online
{
    /// <summary>Bounded per-actor replay gate. ObserveLifecycle is called ONLY after an authenticated,
    /// validated snapshot has been accepted. A pose packet cannot create or renew a lifecycle.</summary>
    public sealed class RagdollPoseGate
    {
        private sealed class Entry
        {
            public uint Generation, Revision, StartTick, SnapshotTick, LastPoseTick, RevisionTick;
            public RagdollRigProfile Profile;
            public bool Active, HasPose;
        }
        private readonly Dictionary<uint, Entry> actors = new Dictionary<uint, Entry>();
        private ulong epoch, round;

        public void Reset(ulong nextEpoch, ulong nextRound)
        {
            if (nextEpoch == 0 || nextRound == 0) throw new ArgumentException("Invalid round identity.");
            actors.Clear(); epoch = nextEpoch; round = nextRound;
        }

        public bool ObserveLifecycle(uint actorId, RagdollRigProfile profile, uint generation,
            uint revision, uint startTick, uint snapshotTick, bool physicalPoseActive)
        {
            if (epoch == 0 || actorId == 0 || generation == 0 || revision == 0 || RagdollPoseCodec.BodyCount(profile) == 0 ||
                startTick > snapshotTick || snapshotTick > 54000) return false;
            if (!actors.TryGetValue(actorId, out var entry))
            {
                if (actors.Count >= 16) return false;
                entry = new Entry(); actors.Add(actorId, entry);
            }
            else
            {
                if (snapshotTick < entry.SnapshotTick || generation < entry.Generation || revision < entry.Revision) return false;
                if (generation == entry.Generation)
                {
                    if (profile != entry.Profile || startTick != entry.StartTick || (!entry.Active && physicalPoseActive)) return false;
                    if (snapshotTick == entry.SnapshotTick && (revision != entry.Revision || physicalPoseActive != entry.Active)) return false;
                }
                else if (snapshotTick <= entry.SnapshotTick || revision <= entry.Revision || startTick < entry.SnapshotTick)
                    return false;
            }
            bool newGeneration = entry.Generation != generation;
            if (newGeneration || entry.Revision != revision) entry.RevisionTick = snapshotTick;
            entry.Generation = generation; entry.Revision = revision; entry.StartTick = startTick;
            entry.SnapshotTick = snapshotTick; entry.Profile = profile; entry.Active = physicalPoseActive;
            if (newGeneration) entry.HasPose = false;
            return true;
        }

        public bool TryAccept(RagdollPoseFrame frame, bool senderIsAuthenticatedOwner, uint estimatedHostTick)
        {
            if (!senderIsAuthenticatedOwner || !RagdollPoseCodec.IsValid(frame) || estimatedHostTick > 54000 ||
                frame.Epoch != epoch || frame.Round != round || !actors.TryGetValue(frame.ActorId, out var entry) ||
                !entry.Active || frame.Profile != entry.Profile || frame.Generation != entry.Generation ||
                frame.StateRevision != entry.Revision || frame.HostTick < entry.StartTick || frame.HostTick < entry.RevisionTick ||
                frame.HostTick + 30 < estimatedHostTick || frame.HostTick > estimatedHostTick + 6 ||
                (entry.HasPose && frame.HostTick <= entry.LastPoseTick)) return false;
            entry.LastPoseTick = frame.HostTick; entry.HasPose = true; return true;
        }

        public void Clear() { actors.Clear(); epoch = 0; round = 0; }
    }
}
