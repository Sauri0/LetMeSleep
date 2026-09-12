using System.Collections.Generic;

namespace LetMeSleep.Gameplay
{
    // Correlation and replay protection shared by host presentation and remote replicas.
    public sealed class ReplicaStateGate
    {
        private GameplayRoundConfig config;
        private uint snapshotTick, privateTick;
        private ulong highestEvent;
        private readonly HashSet<ulong> events = new HashSet<ulong>();
        public void Reset(GameplayRoundConfig next)
        { config = next; snapshotTick = privateTick = 0; highestEvent = 0; events.Clear(); }
        public bool AcceptSnapshot(GameSessionState state)
        {
            if (state == null || config == null || state.SessionEpoch != config.SessionEpoch || state.RoundId != config.RoundId || state.MapId != config.MapId || state.ContentHash != config.ContentHash || state.BalanceHash != config.BalanceHash || state.HostTick < snapshotTick || state.HostTick > config.RoundDurationTicks) return false;
            snapshotTick = state.HostTick; return true;
        }
        public bool AcceptPrivate(ActorPrivateState state, uint localActor)
        {
            if (state == null || config == null || state.ActorId != localActor || state.SessionEpoch != config.SessionEpoch || state.RoundId != config.RoundId || state.HostTick < privateTick || state.HostTick > config.RoundDurationTicks) return false;
            privateTick = state.HostTick; return true;
        }
        public bool AcceptEvent(in GameplayEvent item)
        {
            if (config == null || item.SessionEpoch != config.SessionEpoch || item.RoundId != config.RoundId || item.HostTick > config.RoundDurationTicks || item.EventId == 0 || (highestEvent > item.EventId && highestEvent - item.EventId >= 1024) || !events.Add(item.EventId)) return false;
            if (item.EventId > highestEvent)
            {
                highestEvent = item.EventId;
                if (events.Count > 1024) events.RemoveWhere(id => highestEvent > id && highestEvent - id >= 1024);
            }
            return true;
        }
    }
}
