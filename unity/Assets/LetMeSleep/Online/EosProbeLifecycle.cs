using System;
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

    public enum EosProbeRoundCheckpoint : byte
    {
        Playing,
        Results,
        ReturnedLobby
    }

    /// <summary>Pure decision state for the development EOS probe. Runtime room authority still lives in RoomSession.</summary>
    public sealed class EosProbeLifecycle
    {
        public const int RequiredRounds = 2;

        private readonly bool host;
        private readonly bool[] localPlaying = new bool[RequiredRounds + 1];
        private readonly bool[] localResults = new bool[RequiredRounds + 1];
        private readonly bool[] localReturned = new bool[RequiredRounds + 1];
        private readonly bool[] remotePlaying = new bool[RequiredRounds + 1];
        private readonly bool[] remoteResults = new bool[RequiredRounds + 1];
        private readonly bool[] remoteReturned = new bool[RequiredRounds + 1];
        private readonly bool[] readyRequested = new bool[RequiredRounds + 1];
        private readonly bool[] startRequested = new bool[RequiredRounds + 1];
        private readonly bool[] finishRequested = new bool[RequiredRounds + 1];
        private readonly bool[] returnRequested = new bool[RequiredRounds + 1];
        private bool closeRequested;

        public int CurrentRound { get; private set; }
        public int CompletedRounds => CountSequential(localReturned);
        public int RemoteConfirmedRounds => CountSequential(remoteReturned);
        public bool SawPlaying => localPlaying[1] || localPlaying[2];
        public bool SawResults => localResults[1] || localResults[2];
        public bool SawReturnedLobby => localReturned[1] || localReturned[2];

        public EosProbeLifecycle(bool host) { this.host = host; }

        public bool HasLocalCheckpoint(EosProbeRoundCheckpoint checkpoint, int round)
        {
            if (!ValidRound(round)) return false;
            return Checkpoint(localPlaying, localResults, localReturned, checkpoint, round);
        }

        public bool HasRemoteCheckpoint(EosProbeRoundCheckpoint checkpoint, int round)
        {
            if (!ValidRound(round)) return false;
            return Checkpoint(remotePlaying, remoteResults, remoteReturned, checkpoint, round);
        }

        /// <summary>
        /// Records one unique host-side acknowledgement for the current round. A packet cannot
        /// acknowledge a stage which this peer has not observed locally, and replay returns false.
        /// </summary>
        public bool RecordRemote(EosProbeRoundCheckpoint checkpoint, int round)
        {
            if (!host || !ValidRound(round) || round != CurrentRound || !HasLocalCheckpoint(checkpoint, round)) return false;
            bool[] target = checkpoint == EosProbeRoundCheckpoint.Playing ? remotePlaying :
                checkpoint == EosProbeRoundCheckpoint.Results ? remoteResults : remoteReturned;
            if (target[round]) return false;
            target[round] = true;
            return true;
        }

        public bool TryGetLatestLocalCheckpoint(out EosProbeRoundCheckpoint checkpoint, out int round)
        {
            round = CurrentRound;
            checkpoint = EosProbeRoundCheckpoint.Playing;
            if (!ValidRound(round)) return false;
            if (localReturned[round]) { checkpoint = EosProbeRoundCheckpoint.ReturnedLobby; return true; }
            if (localResults[round]) { checkpoint = EosProbeRoundCheckpoint.Results; return true; }
            if (localPlaying[round]) return true;
            return false;
        }

        public static string EncodeCheckpoint(EosProbeRoundCheckpoint checkpoint, int round)
        {
            if (!ValidRound(round)) throw new ArgumentOutOfRangeException(nameof(round));
            string prefix;
            switch (checkpoint)
            {
                case EosProbeRoundCheckpoint.Playing: prefix = "ProbePlaying:"; break;
                case EosProbeRoundCheckpoint.Results: prefix = "ProbeResults:"; break;
                case EosProbeRoundCheckpoint.ReturnedLobby: prefix = "ProbeReturned:"; break;
                default: throw new ArgumentOutOfRangeException(nameof(checkpoint));
            }
            return prefix + round;
        }

        public static bool TryParseCheckpoint(string text, out EosProbeRoundCheckpoint checkpoint, out int round)
        {
            checkpoint = EosProbeRoundCheckpoint.Playing; round = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string number;
            if (text.StartsWith("ProbePlaying:", StringComparison.Ordinal))
                number = text.Substring("ProbePlaying:".Length);
            else if (text.StartsWith("ProbeResults:", StringComparison.Ordinal))
            { checkpoint = EosProbeRoundCheckpoint.Results; number = text.Substring("ProbeResults:".Length); }
            else if (text.StartsWith("ProbeReturned:", StringComparison.Ordinal))
            { checkpoint = EosProbeRoundCheckpoint.ReturnedLobby; number = text.Substring("ProbeReturned:".Length); }
            else return false;
            return int.TryParse(number, out round) && ValidRound(round) && text == EncodeCheckpoint(checkpoint, round);
        }

        public EosProbeLifecycleAction Advance(LobbyState lobbyState, RoomPhase? roomPhase, int round,
            bool localReady, bool allReady, bool rolesAssigned, bool transportConfirmed)
        {
            if (!host && lobbyState == LobbyState.Closed)
                return CompletedRounds == RequiredRounds ? EosProbeLifecycleAction.Complete : EosProbeLifecycleAction.None;
            if (closeRequested)
            {
                if ((host && lobbyState == LobbyState.Idle) || (!host && lobbyState == LobbyState.Closed))
                    return EosProbeLifecycleAction.Complete;
                return EosProbeLifecycleAction.None;
            }

            if (!roomPhase.HasValue) return EosProbeLifecycleAction.None;
            if (roomPhase == RoomPhase.Waiting)
                return Waiting(round, localReady, allReady, transportConfirmed);
            if (!ValidRound(round)) return EosProbeLifecycleAction.None;
            if (round != CurrentRound && round != CompletedRounds + 1) return EosProbeLifecycleAction.None;
            if (roomPhase == RoomPhase.Results && round != CurrentRound) return EosProbeLifecycleAction.None;

            CurrentRound = round;
            if (roomPhase == RoomPhase.Playing)
            {
                if (!rolesAssigned) return EosProbeLifecycleAction.None;
                localPlaying[round] = true;
                if (host && transportConfirmed && remotePlaying[round] && !finishRequested[round])
                {
                    finishRequested[round] = true;
                    return EosProbeLifecycleAction.FinishRound;
                }
                return EosProbeLifecycleAction.None;
            }
            if (roomPhase == RoomPhase.Results)
            {
                if (!localPlaying[round]) return EosProbeLifecycleAction.None;
                localResults[round] = true;
                if (host && transportConfirmed && remoteResults[round] && !returnRequested[round])
                {
                    returnRequested[round] = true;
                    return EosProbeLifecycleAction.ReturnToLobby;
                }
            }
            return EosProbeLifecycleAction.None;
        }

        private EosProbeLifecycleAction Waiting(int round, bool localReady, bool allReady, bool transportConfirmed)
        {
            if (round < 0 || round > RequiredRounds) return EosProbeLifecycleAction.None;
            if (round == 0 && (CurrentRound != 0 || CompletedRounds != 0)) return EosProbeLifecycleAction.None;
            if (round > 0)
            {
                if (round != CurrentRound) return EosProbeLifecycleAction.None;
                if (!localPlaying[round] || !localResults[round]) return EosProbeLifecycleAction.None;
                localReturned[round] = true;
                if (round == RequiredRounds)
                {
                    if (host && transportConfirmed && CompletedRounds == RequiredRounds &&
                        RemoteConfirmedRounds == RequiredRounds)
                    {
                        closeRequested = true;
                        return EosProbeLifecycleAction.CloseLobby;
                    }
                    return EosProbeLifecycleAction.None;
                }
                if (host && (!transportConfirmed || !remoteReturned[round])) return EosProbeLifecycleAction.None;
            }

            int nextRound = round + 1;
            if (!localReady)
            {
                if (readyRequested[nextRound]) return EosProbeLifecycleAction.None;
                readyRequested[nextRound] = true;
                return EosProbeLifecycleAction.SetReady;
            }
            if (host && allReady && transportConfirmed && !startRequested[nextRound])
            {
                startRequested[nextRound] = true;
                return EosProbeLifecycleAction.StartRound;
            }
            return EosProbeLifecycleAction.None;
        }

        private static bool ValidRound(int round) => round >= 1 && round <= RequiredRounds;
        private static int CountSequential(bool[] values)
        {
            int count = 0;
            for (int round = 1; round <= RequiredRounds && values[round]; round++) count++;
            return count;
        }
        private static bool Checkpoint(bool[] playing, bool[] results, bool[] returned, EosProbeRoundCheckpoint checkpoint, int round)
        {
            switch (checkpoint)
            {
                case EosProbeRoundCheckpoint.Playing: return playing[round];
                case EosProbeRoundCheckpoint.Results: return results[round];
                case EosProbeRoundCheckpoint.ReturnedLobby: return returned[round];
                default: throw new ArgumentOutOfRangeException(nameof(checkpoint));
            }
        }
    }
}
