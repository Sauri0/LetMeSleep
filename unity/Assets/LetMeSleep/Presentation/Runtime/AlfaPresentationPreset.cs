using UnityEngine;

namespace LetMeSleep.Presentation
{
    [CreateAssetMenu(menuName = "Let me sleep/Presentation/Alfa Preset", fileName = "AlfaPresentationPreset")]
    public sealed class AlfaPresentationPreset : ScriptableObject
    {
        [Header("Frame policy")]
        [SerializeField] private int defaultTargetFrameRate = -1;
        [SerializeField, Range(0, 4)] private int defaultVSyncCount = 0;

        [Header("Reference camera")]
        [SerializeField, Range(30f, 110f)] private float humanVerticalFov = 75f;
        [SerializeField, Range(30f, 110f)] private float mosquitoVerticalFov = 68f;
        [SerializeField, Min(0.01f)] private float humanNearPlane = 0.03f;
        [SerializeField, Min(0.01f)] private float mosquitoNearPlane = 0.02f;
        [SerializeField, Min(1f)] private float farPlane = 100f;

        [Header("Mosquito follow camera")]
        [SerializeField, Min(0f)] private float mosquitoDefaultDistance = 0.85f;
        [SerializeField, Min(0f)] private float mosquitoMaximumDistance = 2.5f;
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.08f;

        [Header("Reference light")]
        [SerializeField] private Color moonColor = new Color(0.663f, 0.749f, 0.902f, 1f);
        [SerializeField, Min(0f)] private float moonIntensityLux = 1f;
        [SerializeField, Min(0f)] private float shadowDistanceMeters = 28f;

        public int DefaultTargetFrameRate => defaultTargetFrameRate;
        public int DefaultVSyncCount => defaultVSyncCount;
        public float HumanVerticalFov => humanVerticalFov;
        public float MosquitoVerticalFov => mosquitoVerticalFov;
        public float HumanNearPlane => humanNearPlane;
        public float MosquitoNearPlane => mosquitoNearPlane;
        public float FarPlane => farPlane;
        public float MosquitoDefaultDistance => mosquitoDefaultDistance;
        public float MosquitoMaximumDistance => mosquitoMaximumDistance;
        public float CameraCollisionRadius => cameraCollisionRadius;
        public Color MoonColor => moonColor;
        public float MoonIntensityLux => moonIntensityLux;
        public float ShadowDistanceMeters => shadowDistanceMeters;
    }
}
