using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    // v0.3 UI (UI-06 screen 3): exposes each waiting-room character so the lobby UI can float the player's name
    // over it. Read-only; no gameplay or network state changes.
    public sealed partial class AlfaApplication : ILobbyPresenceSource
    {
        public bool TryGetLobbyAvatar(string memberId, out Transform avatar, out Camera camera)
        {
            avatar = null;
            camera = MenuCamera;
            if (!lobbyMovement || string.IsNullOrEmpty(memberId) || !lobbyMovement.TryGetVisual(memberId, out var visual) || !visual) return false;
            avatar = visual.transform;
            return camera && camera.isActiveAndEnabled;
        }
    }
}
