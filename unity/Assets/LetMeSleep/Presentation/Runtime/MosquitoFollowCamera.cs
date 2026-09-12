using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MosquitoFollowCamera : MonoBehaviour
    {
        private const int HitCapacity = 12;
        private const int OverlapCapacity = 12;
        private const int DepenetrationIterations = 4;

        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Camera controlledCamera = null;
        [SerializeField] private Transform safeAnchor = null;
        [SerializeField] private Transform pivot = null;
        [SerializeField] private Transform cameraTransform = null;
        [SerializeField] private LayerMask collisionMask = -1;
        [SerializeField, Min(0f)] private float collisionPadding = 0.015f;
        [SerializeField, Min(0f)] private float outwardDampingSeconds = 0.08f;
        [SerializeField, Min(0f)] private float rotationDampingSeconds = 0.05f;

        private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];
        private readonly Collider[] overlaps = new Collider[OverlapCapacity];
        private Func<Collider, bool> collisionFilter;
        private SphereCollider collisionProbe;
        private Quaternion desiredRotation = Quaternion.identity;
        private Quaternion smoothedRotation = Quaternion.identity;
        private float desiredDistance = 0.85f;
        private float smoothedDistance;
        private float distanceVelocity;
        private bool initialized;

        public float DesiredDistance => desiredDistance;
        public float ResolvedDistance => smoothedDistance;

        private void Awake()
        {
            Initialize();
            ApplyPreset();
        }

        private void LateUpdate()
        {
            if (!initialized)
                Initialize();
            if (!initialized)
                return;

            float rotationFactor = DampingFactor(rotationDampingSeconds, Time.unscaledDeltaTime);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, desiredRotation, rotationFactor);

            float radius = preset != null ? preset.CameraCollisionRadius : 0.08f;
            Vector3 anchorPosition = ResolveSafePoint(safeAnchor.position, radius);
            Vector3 desiredPivot = pivot.position;

            // Sweep 1: a known safe actor anchor to the visual camera pivot.
            Vector3 resolvedPivot = Sweep(anchorPosition, desiredPivot, radius);
            if (IsBlocked(resolvedPivot, radius))
                resolvedPivot = anchorPosition;

            float maximum = preset != null ? preset.MosquitoMaximumDistance : 2.5f;
            float requested = Mathf.Clamp(desiredDistance, 0f, maximum);
            Vector3 desiredCamera = resolvedPivot - smoothedRotation * Vector3.forward * requested;

            // Sweep 2: resolved pivot to the requested camera position.
            Vector3 resolvedCamera = Sweep(resolvedPivot, desiredCamera, radius);
            if (IsBlocked(resolvedCamera, radius))
                resolvedCamera = resolvedPivot;

            float allowedDistance = Vector3.Distance(resolvedPivot, resolvedCamera);
            if (allowedDistance < smoothedDistance)
            {
                smoothedDistance = allowedDistance;
                distanceVelocity = 0f;
            }
            else
            {
                smoothedDistance = Mathf.SmoothDamp(
                    smoothedDistance, allowedDistance, ref distanceVelocity,
                    outwardDampingSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            cameraTransform.SetPositionAndRotation(
                resolvedPivot - smoothedRotation * Vector3.forward * smoothedDistance,
                smoothedRotation);
        }

        public void SetView(Quaternion authoritativeViewRotation, float requestedDistance)
        {
            if (!IsFinite(authoritativeViewRotation) || float.IsNaN(requestedDistance) || float.IsInfinity(requestedDistance))
                return;
            desiredRotation = authoritativeViewRotation.normalized;
            float maximum = preset != null ? preset.MosquitoMaximumDistance : 2.5f;
            desiredDistance = Mathf.Clamp(requestedDistance, 0f, maximum);
        }

        public void ApplyPreset()
        {
            if (preset == null || controlledCamera == null)
                return;
            controlledCamera.fieldOfView = preset.MosquitoVerticalFov;
            controlledCamera.nearClipPlane = preset.MosquitoNearPlane;
            controlledCamera.farClipPlane = preset.FarPlane;
        }

        public void BindAnchors(Transform anchor, Transform cameraPivot)
        {
            safeAnchor = anchor;
            pivot = cameraPivot;
            initialized = false;
            Initialize();
        }

        public void SetCollisionFilter(Func<Collider, bool> filter) => collisionFilter = filter;

        private void Initialize()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponent<Camera>();
            if (cameraTransform == null && controlledCamera != null)
                cameraTransform = controlledCamera.transform;
            if (safeAnchor == null || pivot == null || cameraTransform == null)
                return;

            desiredRotation = cameraTransform.rotation;
            smoothedRotation = desiredRotation;
            desiredDistance = preset != null ? preset.MosquitoDefaultDistance : desiredDistance;
            smoothedDistance = Vector3.Distance(pivot.position, cameraTransform.position);
            initialized = true;
        }

        private Vector3 Sweep(Vector3 start, Vector3 end, float radius)
        {
            Vector3 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return start;

            Vector3 direction = displacement / distance;
            int count = Physics.SphereCastNonAlloc(
                start, radius, direction, hits, distance, collisionMask,
                QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (!IsCollisionCandidate(collider))
                    continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0f, hits[i].distance - collisionPadding));
            }
            return start + direction * nearest;
        }

        private Vector3 ResolveSafePoint(Vector3 point, float radius)
        {
            if (!IsBlocked(point, radius))
                return point;

            EnsureCollisionProbe(radius);
            collisionProbe.enabled = true;
            try
            {
                for (int iteration = 0; iteration < DepenetrationIterations; iteration++)
                {
                    int count = Physics.OverlapSphereNonAlloc(
                        point, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    {
                        Collider collider = overlaps[i];
                        if (!IsCollisionCandidate(collider))
                            continue;
                        if (!Physics.ComputePenetration(
                                collisionProbe, point, Quaternion.identity,
                                collider, collider.transform.position, collider.transform.rotation,
                                out Vector3 direction, out float distance) || distance <= 0f)
                            continue;

                        point += direction * (distance + collisionPadding);
                        moved = true;
                    }

                    if (!moved || !IsBlocked(point, radius))
                        break;
                }
            }
            finally
            {
                collisionProbe.enabled = false;
            }

            return point;
        }

        private void EnsureCollisionProbe(float radius)
        {
            if (collisionProbe == null)
            {
                var probeObject = new GameObject("CameraCollisionProbe")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = 2
                };
                probeObject.transform.SetParent(transform, false);
                collisionProbe = probeObject.AddComponent<SphereCollider>();
                collisionProbe.isTrigger = true;
                collisionProbe.enabled = false;
            }

            collisionProbe.radius = radius;
        }

        private bool IsBlocked(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (IsCollisionCandidate(collider))
                    return true;
            }
            return false;
        }

        private bool IsCollisionCandidate(Collider collider)
        {
            return collider != null && collider != collisionProbe &&
                (collisionFilter == null || collisionFilter(collider)) &&
                !collider.transform.IsChildOf(safeAnchor);
        }

        private static float DampingFactor(float dampingSeconds, float deltaTime)
        {
            return dampingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / dampingSeconds);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w) &&
                value.x * value.x + value.y * value.y + value.z * value.z +
                value.w * value.w > 0.000001f;
        }
    }
}
