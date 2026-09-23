using System;

namespace LetMeSleep.Bootstrap
{
    /// <summary>
    /// Online round this client already prepared. RoomSession numbers rounds from 1 in every room, so the
    /// round number alone cannot tell a new room's first round from the last round played in a previous room.
    /// </summary>
    public sealed class ActiveRoundTracker
    {
        private string roomKey = string.Empty;
        private int round = -1;

        public bool IsActive => round >= 0;
        public int Round => round;

        public bool IsNewRound(string room, int nextRound)
            => nextRound != round || !string.Equals(room ?? string.Empty, roomKey, StringComparison.Ordinal);

        public void Begin(string room, int nextRound) { roomKey = room ?? string.Empty; round = nextRound; }

        public void Reset() { roomKey = string.Empty; round = -1; }
    }
}
