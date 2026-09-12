using UnityEngine;
using UnityEngine.EventSystems;

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
            yaw = 0f;
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
        }

        public void SetAngle(PreviewAngle angle)
        {
            yaw = angle == PreviewAngle.Front ? 0f : angle == PreviewAngle.Side ? 90f : 180f;
            ApplyOrbit();
        }

        public void ResetView()
        {
            yaw = 0f;
            distance = Mathf.Clamp(fitDistance, minDistance, maxDistance);
            ApplyOrbit();
        }

        public void Zoom(float delta)
        {
            distance = Mathf.Clamp(distance + delta, minDistance, maxDistance);
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
            Zoom(-eventData.scrollDelta.y * Mathf.Max(0.04f, fitDistance * 0.08f));
        }

        private void ApplyOrbit()
        {
            if (!IsBound) return;
            setup.Stage.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var focusWorld = setup.Stage.TransformPoint(focusLocal);
            var cameraTransform = setup.Camera.transform;
            cameraTransform.position = focusWorld + Vector3.forward * distance;
            cameraTransform.LookAt(focusWorld, Vector3.up);
        }

        private void RecalculateFraming()
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                focusLocal = new Vector3(0f, visibleRole == AlfaRole.Human ? 0.9f : 0.1f, 0f);
                fitDistance = visibleRole == AlfaRole.Human ? 2.4f : 0.35f;
                minDistance = fitDistance * 0.55f;
                maxDistance = fitDistance * 2.2f;
                distance = fitDistance;
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            focusLocal = setup.Stage.InverseTransformPoint(bounds.center);

            var camera = setup.Camera;
            var aspect = setup.Texture.height > 0 ? (float)setup.Texture.width / setup.Texture.height : Mathf.Max(0.1f, camera.aspect);
            var verticalHalfFov = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            var horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * Mathf.Max(0.1f, aspect));
            var verticalDistance = bounds.extents.y / Mathf.Max(0.01f, Mathf.Tan(verticalHalfFov));
            var horizontalDistance = bounds.extents.x / Mathf.Max(0.01f, Mathf.Tan(horizontalHalfFov));
            fitDistance = Mathf.Max(0.08f, (Mathf.Max(verticalDistance, horizontalDistance) + bounds.extents.z) * 1.18f);
            var radius = Mathf.Max(0.04f, bounds.extents.magnitude);
            minDistance = Mathf.Max(fitDistance * 0.55f, radius + camera.nearClipPlane + 0.02f);
            maxDistance = Mathf.Max(minDistance + 0.1f, fitDistance * 2.2f);
            distance = Mathf.Clamp(fitDistance, minDistance, maxDistance);
        }

        private void OnDestroy()
        {
            if (setup?.Camera != null) setup.Camera.enabled = false;
            if (instance != null) Destroy(instance);
        }
    }
}
