using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace LetMeSleep.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        // v0.3 stage 2 (UI-06 / PER-08 viewer): a soft navy studio background, a low-poly wooden pedestal under the
        // character and a texture shown "contain" across the whole viewport (clamped edge pixels extend the
        // background, so the drag area covers the full panel without stretching the render).
        internal static readonly Color StageBackground = new Color(0.106f, 0.188f, 0.345f, 1f); // #1B3058
        private const int FallbackPreviewLayer = 30;
        private GameObject pedestal;
        private Material pedestalTopMaterial;
        private Material pedestalSideMaterial;
        private float pedestalRadius;
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
        /// <summary>Raised after Bind/Show changes what the viewer can display.</summary>
        public event System.Action BindingChanged;

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
            if (pedestal != null)
            {
                Destroy(pedestal);
                pedestal = null;
            }
            setup = previewSetup;
            if (output != null)
            {
                output.texture = setup?.Texture;
                var aspect = output.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (aspect != null && setup?.Texture != null && setup.Texture.height > 0)
                    aspect.aspectRatio = (float)setup.Texture.width / setup.Texture.height;
                UpdateOutputUv();
            }
            if (setup == null || !setup.IsUsable)
            {
                SetVisible(false);
                BindingChanged?.Invoke();
                return;
            }
            setup.Camera.targetTexture = setup.Texture;
            setup.Camera.clearFlags = CameraClearFlags.SolidColor;
            setup.Camera.backgroundColor = StageBackground;
            EnsurePedestal();
            Show(visibleRole);
            SetVisible(previewVisible);
            BindingChanged?.Invoke();
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
            EnsurePedestal();
            if (instance != null) Destroy(instance);
            var prefab = role == AlfaRole.Human ? setup.HumanPrefab : setup.MosquitoPrefab;
            setup.Stage.localRotation = Quaternion.identity;
            instance = Instantiate(prefab, setup.Stage, false);
            instance.name = role + "UiPreview";
            yaw = DefaultYaw;
            zoomFactor = 1f;
            RecalculateFraming();
            ApplyOrbit();
            // Install optional presentation once per clone, after its camera is positioned.
            // Reusing a role or hiding/showing the preview must not reinstall components.
            setup.OnPreviewCreated?.Invoke(instance, setup.Camera);
            instance.SetActive(previewVisible);
            if (pedestal != null) pedestal.SetActive(previewVisible);
            BindingChanged?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            previewVisible = visible;
            var render = visible && IsBound;
            if (output != null) output.enabled = render;
            if (setup?.Camera != null) setup.Camera.enabled = render;
            if (instance != null) instance.SetActive(render);
            if (pedestal != null) pedestal.SetActive(render);
            if (render) UpdateOutputUv();
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

        private void OnRectTransformDimensionsChange() => UpdateOutputUv();

        /// <summary>
        /// Shows the square render "contain" across the whole (usually wider) viewport: the UV rect grows past
        /// 0..1 on the long axis and the clamped edge texels repeat the studio background, so nothing is stretched,
        /// the character stays fully visible and every point of the panel accepts the drag.
        /// </summary>
        private void UpdateOutputUv()
        {
            if (output == null) return;
            var texture = output.texture;
            var rect = output.rectTransform.rect;
            if (texture == null || texture.height <= 0 || rect.width <= 1f || rect.height <= 1f)
            {
                output.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            var textureAspect = (float)texture.width / texture.height;
            var viewAspect = rect.width / rect.height;
            if (viewAspect >= textureAspect)
            {
                var width = viewAspect / textureAspect;
                output.uvRect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                var height = textureAspect / viewAspect;
                output.uvRect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }

        /// <summary>Low-poly two-tier wooden pedestal (PER-08) on the preview layer, child of the orbit stage.</summary>
        private void EnsurePedestal()
        {
            if (pedestal != null || !IsBound) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) return;
            pedestal = new GameObject("PreviewPedestal", typeof(MeshFilter), typeof(MeshRenderer));
            pedestal.layer = PreviewLayer();
            pedestal.transform.SetParent(setup.Stage, false);
            pedestal.GetComponent<MeshFilter>().sharedMesh = BuildPedestalMesh();
            if (pedestalTopMaterial == null) pedestalTopMaterial = PedestalMaterial(shader, new Color(0.58f, 0.37f, 0.21f), "LMS preview pedestal top");
            if (pedestalSideMaterial == null) pedestalSideMaterial = PedestalMaterial(shader, new Color(0.30f, 0.19f, 0.12f), "LMS preview pedestal side");
            var renderer = pedestal.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { pedestalTopMaterial, pedestalSideMaterial };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            pedestal.SetActive(false);
        }

        private int PreviewLayer()
        {
            var mask = setup?.Camera != null ? setup.Camera.cullingMask : 0;
            for (var layer = 0; layer < 32; layer++)
                if (mask == 1 << layer) return layer;
            return FallbackPreviewLayer;
        }

        private static Material PedestalMaterial(Shader shader, Color color, string name)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            return material;
        }

        // Unit radius, top at y = 0. Flat-shaded facets (separate vertices per face) as the low-poly art style.
        private const float PedestalTopHeight = 0.12f;
        private const float PedestalBaseHeight = 0.1f;
        private const float PedestalBaseRadius = 1.1f;

        private static Mesh BuildPedestalMesh()
        {
            const int sides = 18;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var top = new List<int>();
            var side = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> into)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (var i = 0; i < 4; i++) normals.Add(normal);
                into.Add(start); into.Add(start + 1); into.Add(start + 2);
                into.Add(start); into.Add(start + 2); into.Add(start + 3);
            }
            Vector3 Ring(int i, float radius, float y)
            {
                var angle = Mathf.PI * 2f * i / sides;
                return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }
            var centre = vertices.Count;
            vertices.Add(Vector3.zero); normals.Add(Vector3.up);
            for (var i = 0; i < sides; i++)
            {
                var a = vertices.Count;
                vertices.Add(Ring(i, 1f, 0f)); vertices.Add(Ring(i + 1, 1f, 0f));
                normals.Add(Vector3.up); normals.Add(Vector3.up);
                top.Add(centre); top.Add(a + 1); top.Add(a);
            }
            var lower = -PedestalTopHeight;
            var bottom = lower - PedestalBaseHeight;
            for (var i = 0; i < sides; i++)
            {
                // Clockwise seen from outside/above (Unity front faces): top tier wall, base ledge and base wall.
                Quad(Ring(i, 1f, 0f), Ring(i + 1, 1f, 0f), Ring(i + 1, 1f, lower), Ring(i, 1f, lower), side);
                Quad(Ring(i, 1f, lower), Ring(i + 1, 1f, lower), Ring(i + 1, PedestalBaseRadius, lower), Ring(i, PedestalBaseRadius, lower), top);
                Quad(Ring(i, PedestalBaseRadius, lower), Ring(i + 1, PedestalBaseRadius, lower),
                    Ring(i + 1, PedestalBaseRadius, bottom), Ring(i, PedestalBaseRadius, bottom), side);
            }
            var mesh = new Mesh { name = "LMS preview pedestal", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(side, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Places the pedestal under the character bounds and adds its rim to the framing points.</summary>
        private void PlacePedestal(Bounds character)
        {
            if (pedestal == null) return;
            pedestalRadius = Mathf.Max(Mathf.Max(character.extents.x, character.extents.z) * 0.78f, character.size.y * 0.2f);
            pedestal.transform.localPosition = new Vector3(character.center.x, character.min.y, character.center.z);
            pedestal.transform.localRotation = Quaternion.identity;
            pedestal.transform.localScale = Vector3.one * pedestalRadius;
            var rimY = character.min.y - (PedestalTopHeight + PedestalBaseHeight * 0.6f) * pedestalRadius;
            for (var i = 0; i < 8; i++)
            {
                var angle = Mathf.PI * 2f * i / 8f;
                framingPoints.Add(new Vector3(character.center.x + Mathf.Cos(angle) * pedestalRadius,
                    rimY, character.center.z + Mathf.Sin(angle) * pedestalRadius));
            }
        }

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
                // BakeMesh(true) compensates renderer scale before TransformPoint applies it.
                // False would count a scaled visual root twice (see CharacterContentBuilder).
                var bounds = renderer.localBounds;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    var snapshot = new Mesh();
                    try
                    {
                        skin.BakeMesh(snapshot, true);
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

            var character = new Bounds(framingPoints[0], Vector3.zero);
            foreach (var point in framingPoints) character.Encapsulate(point);
            PlacePedestal(character);
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
            if (pedestal != null)
            {
                var mesh = pedestal.GetComponent<MeshFilter>().sharedMesh;
                Destroy(pedestal);
                if (mesh != null) Destroy(mesh);
            }
            if (pedestalTopMaterial != null) Destroy(pedestalTopMaterial);
            if (pedestalSideMaterial != null) Destroy(pedestalSideMaterial);
        }
    }
}
