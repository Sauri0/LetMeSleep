using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed class GameplayBodySurface : MonoBehaviour
    {
        public GameplayActorProxy Actor;
        public uint SurfaceId, PartId;
        public CapsuleCollider Collider;
        public Vector3 RestPosition;
    }
}
