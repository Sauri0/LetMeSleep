using UnityEngine;
using UnityEngine.EventSystems;

namespace LetMeSleep.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        private const float MinDistance = 1.15f;
        private const float MaxDistance = 4.2f;
        private CharacterPreviewSetup setup;
        private UnityEngine.UI.RawImage output;
        private GameObject instance;
        private AlfaRole visibleRole;
        private float yaw;
        private float distance = 2.4f;
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
            setup = previewSetup;
            if (output != null)
            {
                output.texture = setup?.Texture;
                output.enabled = setup != null && setup.IsUsable;
            }
            if (setup == null || !setup.IsUsable) return;
            setup.Camera.targetTexture = setup.Texture;
            Show(visibleRole);
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
            instance = Instantiate(prefab, setup.Stage, false);
            instance.name = role + "UiPreview";
            yaw = 0f;
            distance = role == AlfaRole.Human ? 2.4f : 1.5f;
            ApplyOrbit();
        }

        public void SetAngle(PreviewAngle angle)
        {
            yaw = angle == PreviewAngle.Front ? 0f : angle == PreviewAngle.Side ? 90f : 180f;
            ApplyOrbit();
        }

        public void ResetView()
        {
            yaw = 0f;
            distance = visibleRole == AlfaRole.Human ? 2.4f : 1.5f;
            ApplyOrbit();
        }

        public void Zoom(float delta)
        {
            distance = Mathf.Clamp(distance + delta, MinDistance, MaxDistance);
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
            Zoom(-eventData.scrollDelta.y * 0.14f);
        }

        private void ApplyOrbit()
        {
            if (!IsBound) return;
            setup.Stage.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var cameraTransform = setup.Camera.transform;
            cameraTransform.position = setup.Stage.position + new Vector3(0f, visibleRole == AlfaRole.Human ? 0.9f : 0.25f, -distance);
            cameraTransform.LookAt(setup.Stage.position + Vector3.up * (visibleRole == AlfaRole.Human ? 0.85f : 0.2f));
        }

        private void OnDestroy()
        {
            if (instance != null) Destroy(instance);
        }
    }
}
