using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace LetMeSleep.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        private CharacterPreviewSetup setup;
        private UnityEngine.UI.RawImage output;
        private GameObject instance;
        private AlfaRole visibleRole;
        private bool previewVisible;
        private float yaw;
        private float distance;
        private float fitDistance = 2.4f;
        private float minDistance = 1.15f;
        private float maxDistance = 4.2f;
        private float zoomFactor = 1f;
        private readonly List<Vector3> framingPoints = new List<Vector3>();
        private Vector3 focusLocal = new Vector3(0f, 0.9f, 0f);
        private Vector2 dragStart;
        private float yawStart;

        public bool IsBound => setup != null && setup.IsUsable;
        public GameObject CurrentInstance => instance;

        internal void Initialize(UnityEngine.UI.RawImage rawImage, CharacterPreviewSetup previewSetup)
        {
            output = rawImage;
            Bind(previewSetup);
        }

        public void Bind(CharacterPreviewSetup previewSetup)
        {
            if (setup?.Camera != null && setup.Camera != previewSetup?.Camera) setup.Camera.enabled = false;
            if (instance != null)
            {
                Destroy(instance);
                instance = null;
            }
            setup = previewSetup;
            if (output != null)
            {
                output.texture = setup?.Texture;
                var aspect = output.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (aspect != null && setup?.Texture != null && setup.Texture.height > 0)
                    aspect.aspectRatio = (float)setup.Texture.width / setup.Texture.height;
            }
            if (setup == null || !setup.IsUsable)
            {
                SetVisible(false);
                return;
            }
            setup.Camera.targetTexture = setup.Texture;
            Show(visibleRole);
            SetVisible(previewVisible);
        }

        public void Show(AlfaRole role)
        {
            if (instance != null && visibleRole == role)
            {
                ApplyOrbit();
                return;
            }

            visibleRole = role;
            if (!IsBound) return;
            if (instance != null) Destroy(instance);
            var prefab = role == AlfaRole.Human ? setup.HumanPrefab : setup.MosquitoPrefab;
            setup.Stage.localRotation = Quaternion.identity;
            instance = Instantiate(prefab, setup.Stage, false);
            instance.name = role + "UiPreview";
            yaw = DefaultYaw;
            zoomFactor = 1f;
            RecalculateFraming();
            ApplyOrbit();
            instance.SetActive(previewVisible);
        }

        public void SetVisible(bool visible)
        {
            previewVisible = visible;
            var render = visible && IsBound;
            if (output != null) output.enabled = render;
            if (setup?.Camera != null) setup.Camera.enabled = render;
            if (instance != null) instance.SetActive(render);
            if (render && instance != null)
            {
                RecalculateFraming();
                ApplyOrbit();
            }
        }

        public void SetAngle(PreviewAngle angle)
        {
            yaw = angle == PreviewAngle.Front ? 0f : angle == PreviewAngle.Side ? 90f : 180f;
            ApplyOrbit();
        }

        public void ResetView()
        {
            yaw = DefaultYaw;
            zoomFactor = 1f;
            if (IsBound && instance != null) RecalculateFraming();
            ApplyOrbit();
        }

        public void Zoom(float delta)
        {
            zoomFactor = Mathf.Clamp((distance + delta) / Mathf.Max(0.0001f, fitDistance),
                minDistance / Mathf.Max(0.0001f, fitDistance), 2.2f);
            ApplyOrbit();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragStart = eventData.position;
            yawStart = yaw;
        }

        public void OnDrag(PointerEventData eventData)
        {
            yaw = yawStart - (eventData.position.x - dragStart.x) * 0.25f;
            ApplyOrbit();
        }

        public void OnScroll(PointerEventData eventData)
        {
            Zoom(-eventData.scrollDelta.y * fitDistance * 0.08f);
        }

        private float DefaultYaw => visibleRole == AlfaRole.Mosquito ? 35f : 0f;

        private void ApplyOrbit()
        {
            if (!IsBound) return;
            setup.Stage.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var focusWorld = setup.Stage.TransformPoint(focusLocal);
            var cameraTransform = setup.Camera.transform;
            // Fit the current angle, preserving relative zoom when turning a long mosquito.
            // The camera stays level so Frente/Perfil/Espalda remain exact views.
            RecalculateDistance();
            cameraTransform.position = focusWorld + Vector3.forward * distance;
            cameraTransform.LookAt(focusWorld, Vector3.up);
        }

        private void RecalculateFraming()
        {
            framingPoints.Clear();
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                // Imported skin culling bounds are not necessarily in renderer-transform space.
                // Bake the posed geometry explicitly; BakeMesh(false) returns vertices in that
                // space without baking the Transform scale, which TransformPoint applies once.
                var bounds = renderer.localBounds;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    var snapshot = new Mesh();
                    try
                    {
                        skin.BakeMesh(snapshot, false);
                        if (snapshot.vertexCount == 0) continue;
                        snapshot.RecalculateBounds();
                        bounds = snapshot.bounds;
                    }
                    finally
                    {
                        Destroy(snapshot);
                    }
                }
                for (var corner = 0; corner < 8; corner++)
                {
                    var offset = Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    framingPoints.Add(setup.Stage.InverseTransformPoint(
                        renderer.transform.TransformPoint(bounds.center + offset)));
                }
            }

            if (framingPoints.Count == 0)
            {
                var height = visibleRole == AlfaRole.Human ? 1.8f : 0.12f;
                for (var corner = 0; corner < 8; corner++)
                    framingPoints.Add(new Vector3(
                        (corner & 1) == 0 ? -height * 0.3f : height * 0.3f,
                        (corner & 2) == 0 ? 0f : height,
                        (corner & 4) == 0 ? -height * 0.3f : height * 0.3f));
            }

            var combined = new Bounds(framingPoints[0], Vector3.zero);
            foreach (var point in framingPoints) combined.Encapsulate(point);
            focusLocal = combined.center;
            var radius = setup.Stage.TransformVector(combined.extents).magnitude;
            // Only the isolated preview camera changes: never enlarge the prefab or game actor.
            setup.Camera.nearClipPlane = Mathf.Clamp(radius * 0.02f, 0.0001f, 0.01f);
        }

        private void RecalculateDistance()
        {
            if (framingPoints.Count == 0) return;
            var camera = setup.Camera;
            var aspect = setup.Texture.height > 0 ? (float)setup.Texture.width / setup.Texture.height : Mathf.Max(0.1f, camera.aspect);
            camera.aspect = aspect;
            var verticalHalfFov = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            var tanVertical = Mathf.Max(0.01f, Mathf.Tan(verticalHalfFov));
            var tanHorizontal = tanVertical * Mathf.Max(0.1f, aspect);
            var closestDepth = 0f;
            fitDistance = 0.0001f;
            foreach (var point in framingPoints)
            {
                var offset = setup.Stage.TransformVector(point - focusLocal);
                var projectedDistance = Mathf.Max(Mathf.Abs(offset.x) / tanHorizontal,
                    Mathf.Abs(offset.y) / tanVertical);
                // Positive Z points toward the camera; include depth per corner, not globally.
                fitDistance = Mathf.Max(fitDistance, offset.z + projectedDistance * 1.1f);
                closestDepth = Mathf.Max(closestDepth, offset.z);
            }
            minDistance = Mathf.Max(fitDistance * 0.55f, closestDepth + camera.nearClipPlane * 2f);
            fitDistance = Mathf.Max(fitDistance, minDistance);
            maxDistance = fitDistance * 2.2f;
            distance = Mathf.Clamp(fitDistance * zoomFactor, minDistance, maxDistance);
        }

        private void OnDestroy()
        {
            if (setup?.Camera != null) setup.Camera.enabled = false;
            if (instance != null) Destroy(instance);
        }
    }
}
