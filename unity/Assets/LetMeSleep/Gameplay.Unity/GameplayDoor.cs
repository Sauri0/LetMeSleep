using UnityEngine;

namespace LetMeSleep.Gameplay.Unity
{
    public sealed class GameplayDoor : MonoBehaviour
    {
        public uint DoorId = 1;
        public uint SurfaceId = 100;
        public Transform Hinge;
        public BoxCollider Leaf;
        public Transform Handle;
        public float OpenSign = 1;
        public float OpenDegrees = 90;
        public float InitialDegrees;
        public uint Revision { get; internal set; } = 1;
        public float AngleRadians { get; internal set; }
        private Quaternion closedRotation;
        private bool initialized;
        public void Initialize()
        {
            if (initialized) return;
            if (!Hinge) Hinge = transform;
            if (!Leaf) Leaf = GetComponentInChildren<BoxCollider>();
            closedRotation = Hinge.localRotation; initialized = true;
            if (Leaf)
            {
                var surface = Leaf.GetComponent<GameplaySurface>(); if (!surface) surface = Leaf.gameObject.AddComponent<GameplaySurface>();
                surface.SurfaceId = SurfaceId;
            }
        }
        public DoorDefinition Definition
        {
            get
            {
                Initialize();
                var worldClosed = Hinge.parent ? Hinge.parent.rotation * closedRotation : closedRotation;
                if (!Leaf) throw new System.InvalidOperationException("Door leaf collider is required.");
                var center = Hinge.InverseTransformPoint(Leaf.transform.TransformPoint(Leaf.center));
                var size = Vector3.Scale(Leaf.size, Leaf.transform.lossyScale);
                var localRotation = Quaternion.Inverse(Hinge.rotation) * Leaf.transform.rotation;
                var leafRight = Hinge.InverseTransformDirection(Leaf.transform.right);
                var handle = Handle ? Hinge.InverseTransformPoint(Handle.position) : center + leafRight * Mathf.Max(0, size.x * .5f - .09f);
                return new DoorDefinition(DoorId, SurfaceId, Hinge.position.ToFloat(), worldClosed.ToRotation(), size.ToFloat(), handle.ToFloat(), OpenSign, OpenDegrees * Mathf.Deg2Rad, InitialDegrees * Mathf.Deg2Rad, center.ToFloat(), localRotation.ToRotation());
            }
        }
        public void ApplyAngle(float radians)
        {
            Initialize(); AngleRadians = radians;
            Hinge.localRotation = closedRotation * Quaternion.AngleAxis(radians * Mathf.Rad2Deg * OpenSign, Vector3.up);
        }
    }
    public static class GameplayConversions
    {
        public static Vector3 ToUnity(this Float3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Float3 ToFloat(this Vector3 v) => new Float3(v.x, v.y, v.z);
        public static Quaternion ToUnity(this Rotation r) => new Quaternion(r.X, r.Y, r.Z, r.W);
        public static Rotation ToRotation(this Quaternion q) => new Rotation(q.x, q.y, q.z, q.w);
    }
}
