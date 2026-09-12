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
        private readonly Dictionary<uint, uint> doorSurfaces = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, string> toolTypes = new Dictionary<uint, string>();
        public void Reset(GameplayRoundConfig next)
        {
            config = next; snapshotTick = privateTick = 0; highestEvent = 0; events.Clear(); doorSurfaces.Clear(); toolTypes.Clear();
            if (next == null) return;
            foreach (var door in next.DoorDefinitions) doorSurfaces.Add(door.DoorId, door.SurfaceId);
            foreach (var tool in next.ToolDefinitions) toolTypes.Add(tool.PickupId, tool.ToolId);
        }
        public bool AcceptSnapshot(GameSessionState state)
        {
            if (state == null || config == null || state.SessionEpoch != config.SessionEpoch || state.RoundId != config.RoundId || state.MapId != config.MapId || state.ContentHash != config.ContentHash || state.BalanceHash != config.BalanceHash || state.HostTick < snapshotTick || state.HostTick > config.RoundDurationTicks) return false;
            if (state.Doors.Count != doorSurfaces.Count || state.ToolPickups.Count != toolTypes.Count) return false;
            foreach (var door in state.Doors) if (!doorSurfaces.TryGetValue(door.DoorId, out var surface) || surface != door.SurfaceId) return false;
            foreach (var tool in state.ToolPickups) if (!toolTypes.TryGetValue(tool.PickupId, out var type) || type != tool.ToolId) return false;
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
