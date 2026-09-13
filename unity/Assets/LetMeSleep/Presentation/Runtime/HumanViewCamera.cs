using UnityEngine;

namespace LetMeSleep.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1300)]
    public sealed class HumanViewCamera : MonoBehaviour
    {
        private const float MinimumPitchRadians = -1.91986218f; // -110 degrees
        private const float MaximumPitchRadians = 1.30899694f;  // +75 degrees

        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Camera controlledCamera = null;
        [SerializeField] private Transform eyeAnchor = null;
        [SerializeField] private Transform pitchPivot = null;
        [SerializeField] private bool followEyePosition = true;

        private float yawRadians;
        private float pitchRadians;
        private uint viewRevision;
        private bool initialized;
        private bool hasView;
        private System.Action refreshEyeAnchor;

        public uint ViewRevision => viewRevision;
        public float PitchRadians => pitchRadians;

        private void Awake()
        {
            Initialize();
            ApplyPreset();
        }

        private void LateUpdate()
        {
            if (!initialized)
                Initialize();
            if (pitchPivot == null)
                return;

            refreshEyeAnchor?.Invoke(); // After final body/facial writers, before reading the copied anchor.
            if (followEyePosition && eyeAnchor != null)
                pitchPivot.position = eyeAnchor.position;
            pitchPivot.rotation = Quaternion.Euler(
                -pitchRadians * Mathf.Rad2Deg, yawRadians * Mathf.Rad2Deg, 0f);
        }

        public void SetPitch(float authoritativePitchRadians, uint authoritativeViewRevision, bool force = false)
        {
            SetView(yawRadians, authoritativePitchRadians, authoritativeViewRevision, force);
        }

        public void SetView(
            float authoritativeYawRadians, float authoritativePitchRadians,
            uint authoritativeViewRevision, bool force = false)
        {
            if (!IsFinite(authoritativeYawRadians) || !IsFinite(authoritativePitchRadians))
                return;
            if (!force && hasView && !IsNewer(authoritativeViewRevision, viewRevision))
                return;

            yawRadians = authoritativeYawRadians;
            pitchRadians = Mathf.Clamp(authoritativePitchRadians, MinimumPitchRadians, MaximumPitchRadians);
            viewRevision = authoritativeViewRevision;
            hasView = true;
        }

        public void ApplyPreset()
        {
            if (preset == null || controlledCamera == null)
                return;
            controlledCamera.fieldOfView = preset.HumanVerticalFov;
            controlledCamera.nearClipPlane = preset.HumanNearPlane;
            controlledCamera.farClipPlane = preset.FarPlane;
        }

        public void BindEye(Transform anchor, System.Action refreshAnchor = null)
        {
            eyeAnchor = anchor;
            refreshEyeAnchor = refreshAnchor;
            initialized = false;
            Initialize();
        }

        private void Initialize()
        {
            if (pitchPivot == null && controlledCamera != null)
                pitchPivot = controlledCamera.transform;
            if (pitchPivot == null)
                return;
            initialized = true;
        }

        private static bool IsNewer(uint candidate, uint current)
        {
            return unchecked((int)(candidate - current)) > 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
