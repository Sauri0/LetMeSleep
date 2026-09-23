using System;
using LetMeSleep.Online;

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

    public enum OnlineEntryDecision { Proceed, IgnoreDuplicate, WaitForPreviousRoom }

    public static class OnlineEntryPolicy
    {
        /// <summary>
        /// Create/Join requests while an attempt is pending or a room is open are duplicates. While the previous
        /// room is still being left the request cannot start yet, and the player must be told so: the UI already
        /// locked itself on "Creando sala…" and nothing else would ever release it.
        /// </summary>
        public static OnlineEntryDecision Evaluate(bool pendingOnline, LobbyState? lobby)
            => pendingOnline || lobby == LobbyState.Connected ? OnlineEntryDecision.IgnoreDuplicate
                : lobby == LobbyState.Leaving ? OnlineEntryDecision.WaitForPreviousRoom
                : OnlineEntryDecision.Proceed;
    }

    public static class RoomTeardownPolicy
    {
        /// <summary>
        /// True when the room this client had joined is gone without the player leaving it: the lobby closed
        /// (host left) or failed after membership (e.g. authentication lost). Failures while joining keep the
        /// join screen and its error instead.
        /// </summary>
        public static bool ShouldTearDown(LobbyState state, bool intentionalLeave, bool joinedRoom)
            => !intentionalLeave && (state == LobbyState.Closed || (state == LobbyState.Failed && joinedRoom));

        /// <summary>
        /// Error shown after a teardown, or null for the plain "room closed" notice. A reason the client already
        /// gave for leaving (a rejected handshake: "No se pudo entrar: …") wins over the failure of that same
        /// Leave (Leave_*, LobbyTimedOut), which would otherwise replace it with a misleading lost-connection error.
        /// </summary>
        public static string TeardownError(LobbyState state, string lobbyErrorCode, string closingError)
            => !string.IsNullOrEmpty(closingError) ? closingError
                : state == LobbyState.Failed ? "Se perdió la conexión con la sala (" + lobbyErrorCode + "). Volvé a intentarlo."
                : null;
    }
}
