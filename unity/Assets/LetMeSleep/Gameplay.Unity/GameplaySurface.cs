using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    // Stable authored ID. Cosmetic objects never supply network identity.
    public sealed class GameplaySurface : MonoBehaviour
    {
        public uint SurfaceId;
        public bool CanPerch = true;
        public uint Revision = 1;
    }
}
