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
