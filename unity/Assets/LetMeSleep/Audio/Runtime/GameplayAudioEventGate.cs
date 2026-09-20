using System.Collections.Generic;

namespace LetMeSleep.Audio
{
    // Presentation receives snapshots and events independently. Keep sounds tied to
    // the round that made them, rather than replaying a delayed packet after results.
    public sealed class GameplayAudioEventGate
    {
        private readonly HashSet<EventKey> accepted = new HashSet<EventKey>();
        private ulong epoch;
        private ulong round;
        private bool active;

        public void BeginRound(ulong nextEpoch, ulong nextRound)
        {
            epoch = nextEpoch;
            round = nextRound;
            active = true;
            accepted.Clear();
        }

        public void EndRound()
        {
            active = false;
            accepted.Clear();
        }

        // A presenter can be disabled while its round continues. Resume without
        // discarding accepted IDs, otherwise rebind would replay old packets.
        public void ResumeRound(ulong nextEpoch, ulong nextRound)
        {
            if (epoch != nextEpoch || round != nextRound)
            {
                BeginRound(nextEpoch, nextRound);
                return;
            }
            active = true;
        }

        public void Suspend() => active = false;

        public bool TryAccept(ulong eventEpoch, ulong eventRound, ulong eventId)
        {
            return active && eventEpoch == epoch && eventRound == round &&
                accepted.Add(new EventKey(eventEpoch, eventRound, eventId));
        }

        private readonly struct EventKey : System.IEquatable<EventKey>
        {
            private readonly ulong epoch;
            private readonly ulong round;
            private readonly ulong id;

            internal EventKey(ulong epoch, ulong round, ulong id)
            {
                this.epoch = epoch;
                this.round = round;
                this.id = id;
            }

            public bool Equals(EventKey other) =>
                epoch == other.epoch && round == other.round && id == other.id;

            public override bool Equals(object obj) => obj is EventKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = epoch.GetHashCode();
                    hash = (hash * 397) ^ round.GetHashCode();
                    return (hash * 397) ^ id.GetHashCode();
                }
            }
        }
    }
}
