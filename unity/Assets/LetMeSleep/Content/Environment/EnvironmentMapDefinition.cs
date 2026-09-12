using UnityEngine;

namespace LetMeSleep.Content.Environment
{
    // Authored scene data only. Core/Online owns map loading, role choice and spawning.
    public sealed class EnvironmentMapDefinition : MonoBehaviour
    {
        public string MapId;
        public string ContentHash;
        public TextAsset SpatialData;
        public Transform[] HumanSpawnPoints;
        public Transform[] MosquitoSpawnPoints;
        public Transform[] LobbySpawnPoints;
        public Transform[] ToolPickupPoints;
        public Transform PresentationAnchors;
        public Bounds PlayBounds;
        public string GeometryContract = "lms-environment-alfa-1";
    }
}
