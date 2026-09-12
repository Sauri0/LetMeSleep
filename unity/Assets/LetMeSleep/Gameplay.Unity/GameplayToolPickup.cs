using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed class GameplayToolPickup : MonoBehaviour
    {
        public uint PickupId;
        public string ToolId = GameplayTools.Flyswatter;
        public GameObject VisualRoot;
        public Collider InteractionCollider;
        public uint Revision { get; private set; } = 1;
        public uint OwnerActorId { get; private set; }
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialized;
        public void Initialize()
        {
            if (initialized) return;
            initialPosition = transform.position; initialRotation = transform.rotation; initialized = true;
            if (!InteractionCollider)
            {
                var box = gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, .025f, .18f); box.size = new Vector3(.19f, .05f, .40f); InteractionCollider = box;
            }
            InteractionCollider.isTrigger = true;
        }
        public ToolPickupDefinition Definition { get { Initialize(); return new ToolPickupDefinition(PickupId, ToolId, initialPosition.ToFloat(), initialRotation.ToRotation()); } }
        public void Apply(in ToolPickupSnapshot state)
        {
            Initialize(); Revision = state.Revision; OwnerActorId = state.OwnerActorId;
            transform.SetPositionAndRotation(state.Position.ToUnity(), state.Rotation.ToUnity());
            if (VisualRoot && VisualRoot != gameObject) VisualRoot.SetActive(state.OwnerActorId == 0);
            else foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = state.OwnerActorId == 0;
            InteractionCollider.enabled = state.OwnerActorId == 0;
        }
    }
}
