using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 main-menu bedroom (UI-06 screen 1): the authored anchors of the decorative set that AlfaApplication
    /// instantiates with the private lobby. The set is visual only (no colliders) and sits outside the lobby room, so the
    /// "sala" never sees it. The living menu lays the decorative human at <see cref="SleeperRoot"/> (the sleeper anchor of
    /// Prop_BedSleeper), flies the decorative mosquito through <see cref="MosquitoPath"/> and frames the fixed menu camera
    /// with <see cref="CameraAnchor"/> / <see cref="CameraFieldOfView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBedroomSet : MonoBehaviour
    {
        public Transform SleeperRoot;
        [Tooltip("The sleeper's upper ear: the mosquito buzzes here and the clumsy swat reacts to it.")]
        public Transform EarAnchor;
        public Transform CameraAnchor;
        public float CameraFieldOfView = 40f;
        [Tooltip("Closed loop of flight control points (the living scene flies a quadratic spline through their midpoints).")]
        public Transform[] MosquitoPath = Array.Empty<Transform>();
        [Tooltip("Optional point the hovering mosquito turns toward (between the sleeper and the camera).")]
        public Transform MosquitoFaceTarget;
        [Tooltip("Uniform scale of the decorative menu mosquito so it reads large in the top right of the frame.")]
        public float MosquitoScale = 1f;
        [Tooltip("Optional warm key on the sleeper / cool fill anchors for the living scene (null: the set's own lights only).")]
        public Transform WarmLightAnchor, CoolLightAnchor;
        [Tooltip("v0.3.0 r2 (director #2): flattened sleeping lids, closed-eye lines, small smile and head turn toward the camera.")]
        public SleepingFaceRig.Settings SleepFace = new SleepingFaceRig.Settings();
        [Tooltip("How much the hovering mosquito turns toward MosquitoFaceTarget instead of its flight direction (0..1).")]
        [Range(0f, 1f)] public float MosquitoFacing = .85f;
        [Tooltip("Pupil size of the menu mosquito across the look axis (1 = authored ~25 % of the eye; director #3 asks >= 30 %).")]
        [Range(1f, 1.6f)] public float MosquitoPupilScale = 1.32f;
        [Tooltip("Director #3: pupil smoothness in the menu (a glossy 12-20 px pupil reflects the lamp and reads grey); " +
                 "negative keeps the material's own value.")]
        [Range(-1f, 1f)] public float MosquitoPupilSmoothness = .12f;
        [Tooltip("Director #3: the menu mosquito hovers with its wings near their widest spread instead of full wingbeats. " +
                 "Flight clip time (s) of the widest projected span; negative plays the full flap.")]
        public float FlightHoverTime = -1f;
        [Tooltip("Half amplitude (s of the flight clip) of the flutter around FlightHoverTime, and its rate (Hz).")]
        public float FlightHoverWobble = .02f, FlightHoverRate = 9f;

        public bool IsComplete
        {
            get
            {
                if (!SleeperRoot || !EarAnchor || !CameraAnchor || MosquitoPath == null || MosquitoPath.Length < 4) return false;
                foreach (var point in MosquitoPath) if (!point) return false;
                return CameraFieldOfView > 1f && CameraFieldOfView < 120f && MosquitoScale > 0f;
            }
        }
    }
}
