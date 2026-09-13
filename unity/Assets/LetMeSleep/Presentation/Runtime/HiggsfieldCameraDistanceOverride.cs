using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Explicit Higgsfield camera override; reusable across map switches, no global camera lookup.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1500)] // After HumanViewCamera 1300 / MosquitoFollowCamera 1250.
    public sealed class HiggsfieldCameraDistanceOverride : MonoBehaviour
    {
        static readonly Dictionary<Camera,HiggsfieldCameraDistanceOverride> Owners = new Dictionary<Camera,HiggsfieldCameraDistanceOverride>();
        Camera target;
        Transform mapRoot;
        float originalFar, desiredFar;
        public bool IsBound => target && mapRoot;
        public Camera Target => target;
        public float DesiredFar => desiredFar;

        public void Bind(Camera camera, Transform owningMap, float farPlane)
        {
            if (!isActiveAndEnabled || !camera || !owningMap || !owningMap.gameObject.activeInHierarchy ||
                float.IsNaN(farPlane) || float.IsInfinity(farPlane) || farPlane < 10 || farPlane > 1000 || farPlane <= camera.nearClipPlane)
                throw new ArgumentException("Explicit active camera/map and far plane 10..1000 above near are required.");
            if (Owners.TryGetValue(camera, out var owner) && owner && owner != this)
                throw new InvalidOperationException("Camera already has a distance override owner.");
            Unbind(); // Restores the previous baseline even when rebinding the same camera.
            target = camera; mapRoot = owningMap; originalFar = camera.farClipPlane; desiredFar = farPlane;
            Owners[camera] = this;
            camera.farClipPlane = farPlane;
        }
        void LateUpdate()
        {
            if (!target || !mapRoot || !mapRoot.gameObject.activeInHierarchy) { Unbind(); return; }
            // A role ApplyPreset may reset far earlier in the frame. Do not change near/FOV or the preset asset.
            if (target.farClipPlane != desiredFar) target.farClipPlane = desiredFar;
        }
        public void Unbind()
        {
            if (target) target.farClipPlane = originalFar;
            if (!ReferenceEquals(target, null) && Owners.TryGetValue(target, out var owner) && owner == this) Owners.Remove(target);
            target = null; mapRoot = null; desiredFar = 0;
        }
        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();
    }
}
