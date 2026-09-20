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
        public ToolPickupPhase Phase { get; private set; }
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialized;
        private Vector3 placementCenter, placementHalf;
        // Stored before the pickup collider is hidden in an inventory. Bounds on a
        // disabled collider are empty, so querying them when dropping loses volume.
        public void GetPlacementVolume(out Vector3 center, out Vector3 half)
        { Initialize(); center = placementCenter; half = placementHalf; }
        public void Initialize()
        {
            if (initialized) return;
            initialPosition = transform.position; initialRotation = transform.rotation; initialized = true;
            if (!InteractionCollider)
            {
                var box = gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, .025f, .18f); box.size = new Vector3(.19f, .05f, .40f); InteractionCollider = box;
            }
            InteractionCollider.isTrigger = true;
            var boxCollider = InteractionCollider as BoxCollider;
            var bounds = boxCollider ? new Bounds(boxCollider.center, boxCollider.size) : InteractionCollider.bounds;
            var inverseRotation = Quaternion.Inverse(transform.rotation);
            var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int mask = 0; mask < 8; mask++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((mask & 1) == 0 ? -1 : 1, (mask & 2) == 0 ? -1 : 1, (mask & 4) == 0 ? -1 : 1));
                var world = boxCollider ? boxCollider.transform.TransformPoint(corner) : corner;
                var local = inverseRotation * (world - transform.position);
                minimum = Vector3.Min(minimum, local); maximum = Vector3.Max(maximum, local);
            }
            placementCenter = (minimum + maximum) * .5f;
            placementHalf = Vector3.Max((maximum - minimum) * .5f, Vector3.one * .005f);
        }
        public ToolPickupDefinition Definition { get { Initialize(); return new ToolPickupDefinition(PickupId, ToolId, initialPosition.ToFloat(), initialRotation.ToRotation()); } }
        public void Apply(in ToolPickupSnapshot state)
        {
            Initialize(); Revision = state.Revision; OwnerActorId = state.OwnerActorId; Phase = state.Phase;
            transform.SetPositionAndRotation(state.Position.ToUnity(), state.Rotation.ToUnity());
            if (VisualRoot && VisualRoot != gameObject) VisualRoot.SetActive(state.OwnerActorId == 0);
            else foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = state.OwnerActorId == 0;
            InteractionCollider.enabled = state.Phase == ToolPickupPhase.World;
        }
    }
}
