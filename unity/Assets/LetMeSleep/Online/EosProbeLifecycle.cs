using LetMeSleep.Core;

namespace LetMeSleep.Online
{
    public enum EosProbeLifecycleAction
    {
        None,
        SetReady,
        StartRound,
        FinishRound,
        ReturnToLobby,
        CloseLobby,
        Complete
    }

    /// <summary>Pure decision state for the development EOS probe. Runtime room authority still lives in RoomSession.</summary>
    public sealed class EosProbeLifecycle
    {
        private readonly bool host;
        private bool sawPlaying, sawResults, sawReturnedLobby, closeRequested;

        public bool SawPlaying => sawPlaying;
        public bool SawResults => sawResults;
        public bool SawReturnedLobby => sawReturnedLobby;

        public EosProbeLifecycle(bool host) { this.host = host; }

        public EosProbeLifecycleAction Advance(LobbyState lobbyState, RoomPhase? roomPhase, int round,
            bool localReady, bool allReady, bool rolesAssigned, bool remoteSawPlaying,
            bool remoteSawResults, bool remoteSawReturnedLobby)
        {
            if (!host && lobbyState == LobbyState.Closed && sawPlaying && sawResults && sawReturnedLobby)
                return EosProbeLifecycleAction.Complete;
            if (closeRequested)
            {
                if ((host && lobbyState == LobbyState.Idle) || (!host && lobbyState == LobbyState.Closed))
                    return EosProbeLifecycleAction.Complete;
                return EosProbeLifecycleAction.None;
            }

            if (!roomPhase.HasValue) return EosProbeLifecycleAction.None;
            if (roomPhase == RoomPhase.Waiting && round == 0)
            {
                if (!localReady) return EosProbeLifecycleAction.SetReady;
                if (host && allReady) return EosProbeLifecycleAction.StartRound;
                return EosProbeLifecycleAction.None;
            }
            if (roomPhase == RoomPhase.Playing)
            {
                if (!rolesAssigned) return EosProbeLifecycleAction.None;
                sawPlaying = true;
                if (host && remoteSawPlaying) return EosProbeLifecycleAction.FinishRound;
                return EosProbeLifecycleAction.None;
            }
            if (roomPhase == RoomPhase.Results)
            {
                sawResults = true;
                if (host && remoteSawResults) return EosProbeLifecycleAction.ReturnToLobby;
                return EosProbeLifecycleAction.None;
            }
            if (roomPhase == RoomPhase.Waiting && round > 0 && sawPlaying && sawResults)
            {
                sawReturnedLobby = true;
                if (host && remoteSawReturnedLobby)
                {
                    closeRequested = true;
                    return EosProbeLifecycleAction.CloseLobby;
                }
            }
            return EosProbeLifecycleAction.None;
        }
    }
}
