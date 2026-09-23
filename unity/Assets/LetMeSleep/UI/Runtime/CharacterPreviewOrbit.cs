using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace LetMeSleep.UI
{
    [DisallowMultipleComponent]
    // Late: the approximate bone edits are applied after the animator and the face attention have written the pose.
    [DefaultExecutionOrder(10000)]
    public sealed class CharacterPreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        // v0.3 stage 2 (UI-06 5-6 / PER-08 viewer), art-direction pass: the character stands on a light wooden
        // pedestal (#A86F3A top, #8B5A2B edge) in front of the painted warm bedroom of the sketch
        // (Resources/AlfaUiBackdrops/PreviewBedroom: cream wall, wardrobe, #FFB347 lamp with halo, wooden floor),
        // with a warm rim light. The camera renders into a target of the viewport's own aspect, so the painting is
        // never stretched and the whole panel accepts the drag. Without the painting (or its shader) the camera
        // clears to the navy studio colour below.
        internal static readonly Color StageBackground = new Color(0.106f, 0.188f, 0.345f, 1f); // #1B3058
        private static readonly Color PedestalTop = new Color(0.659f, 0.435f, 0.227f);            // #A86F3A
        private static readonly Color PedestalEdge = new Color(0.545f, 0.353f, 0.169f);           // #8B5A2B
        private static readonly Color RimColor = new Color(1f, 0.702f, 0.278f);                   // #FFB347
        private static readonly Color ViewBackground = new Color(0.663f, 0.722f, 0.808f, 1f);     // #A9B8CE, UI-06 thumbnails
        private static readonly Color SummaryBackground = new Color(0.165f, 0.275f, 0.467f, 1f);  // #2A4677, rail summary card
        // VISTA PREVIA rule (stage-3 director pass): the figure fills 85 % of the view's height (92 % of its width at
        // most), centred, at one scale shared by FRENTE, ESPALDA and LADO, from the model's own bounds.
        private const float ViewHeightFill = 0.85f;
        private const float ViewWidthFill = 0.92f;
        private const string BackdropResource = "AlfaUiBackdrops/PreviewBedroom";
        private const int FallbackPreviewLayer = 30;
        private const int ViewWidth = 248;
        private const int ViewHeight = 400;
        private const int ViewTargetHeight = 440;
        private GameObject pedestal;
        private Material pedestalTopMaterial;
        private Material pedestalSideMaterial;
        private float pedestalRadius;
        private GameObject backdrop;
        private Material backdropMaterial;
        private Mesh backdropMesh;
        private float backdropAspect = 1.6f;
        private Light rimLight;
        private Light fillLight;
        private int characterPointCount;
        private Vector3 characterFocusLocal;
        private RenderTexture ownTarget;
        private bool targetDirty = true;
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
        private float characterRadius = 1f;
        private readonly List<Vector3> framingPoints = new List<Vector3>();
        private readonly List<Vector3> viewPoints = new List<Vector3>();
        // The clone is spawned in its bind pose (arms out): the framing is measured again once it has been animated.
        private bool framingStale;
        private UnityEngine.UI.RawImage summaryImage;
        private RenderTexture summaryTarget;
        private Vector3 focusLocal = new Vector3(0f, 0.9f, 0f);
        private Vector2 dragStart;
        private float yawStart;

        private UnityEngine.UI.RawImage[] viewImages = new UnityEngine.UI.RawImage[0];
        private float[] viewYaws = new float[0];
        private RenderTexture[] viewTargets = new RenderTexture[0];
        // Frame at which the angle views re-render; one frame after a change so skinning has caught up.
        private int viewsDueFrame = -1;

        private IReadOnlyList<PreviewPartStyle> approximation;
        private GameObject approximatedInstance;
        private readonly List<BoneEdit> boneEdits = new List<BoneEdit>();
        // Approximation extras: the angry brows (with their meshes and materials), and any renderer hidden while shown.
        private readonly List<Object> approximationObjects = new List<Object>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private static readonly Color BrowColor = new Color(0.165f, 0.102f, 0.102f, 1f); // #2A1A1A

        public bool IsBound => setup != null && setup.IsUsable;
        public GameObject CurrentInstance => instance;
        /// <summary>True while an approximate modular preview (bone scale/shape) is applied to the clone.</summary>
        public bool ShowsApproximation => approximation != null && approximation.Count > 0 && instance != null;
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
            DestroyStudio();
            setup = previewSetup;
            ReleaseTargets();
            if (output != null)
            {
                output.texture = setup?.Texture;
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
            EnsureStudio();
            EnsureTarget();
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
            EnsureStudio();
            if (instance != null) Destroy(instance);
            var prefab = role == AlfaRole.Human ? setup.HumanPrefab : setup.MosquitoPrefab;
            setup.Stage.localRotation = Quaternion.identity;
            instance = Instantiate(prefab, setup.Stage, false);
            instance.name = role + "UiPreview";
            boneEdits.Clear();
            approximatedInstance = null;
            yaw = DefaultYaw;
            zoomFactor = 1f;
            RecalculateFraming();
            framingStale = true;
            ApplyOrbit();
            // Install optional presentation once per clone, after its camera is positioned.
            // Reusing a role or hiding/showing the preview must not reinstall components.
            setup.OnPreviewCreated?.Invoke(instance, setup.Camera);
            instance.SetActive(previewVisible);
            SetStudioActive(previewVisible);
            RequestViews();
            BindingChanged?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            previewVisible = visible;
            var render = visible && IsBound;
            if (output != null) output.enabled = render;
            if (setup?.Camera != null) setup.Camera.enabled = render;
            if (instance != null) instance.SetActive(render);
            SetStudioActive(render);
            if (render)
            {
                EnsureTarget();
                UpdateOutputUv();
            }
            if (render && instance != null)
            {
                RecalculateFraming();
                ApplyOrbit();
                RequestViews();
            }
        }

        public void SetAngle(PreviewAngle angle)
        {
            yaw = AngleYaw(angle);
            ApplyOrbit();
        }

        internal static float AngleYaw(PreviewAngle angle) => angle == PreviewAngle.Front ? 0f : angle == PreviewAngle.Side ? 90f : 180f;

        public void ResetView()
        {
            yaw = DefaultYaw;
            zoomFactor = 1f;
            if (IsBound && instance != null) RecalculateFraming();
            ApplyOrbit();
        }

        /// <summary>
        /// Re-fits the camera and the pedestal to the clone as it looks now, keeping the angle and zoom: modular parts
        /// (long wings, a beanie, a backpack) can reach beyond the authored body the framing was computed from.
        /// </summary>
        public void Reframe()
        {
            if (!IsBound || instance == null) return;
            RecalculateFraming();
            ApplyOrbit();
            RequestViews();
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

        /// <summary>
        /// UI-06 "VISTA PREVIA": small renders of the character from fixed angles (front, back, side) on the light
        /// thumbnail colour, refreshed when the look changes. One render target per image, owned by the viewer.
        /// </summary>
        internal void BindViews(UnityEngine.UI.RawImage[] images, float[] yaws)
        {
            ReleaseViewTargets();
            viewImages = images ?? new UnityEngine.UI.RawImage[0];
            viewYaws = yaws ?? new float[0];
            viewTargets = new RenderTexture[viewImages.Length];
            RequestViews();
        }

        /// <summary>
        /// The rail's "TU HUMANO / TU MOSQUITO" picture: rendered by this viewer from the same clone as the big view
        /// and VISTA PREVIA (colours, wings, eyes, proboscis included), re-rendered with every change of the look.
        /// Null unbinds it.
        /// </summary>
        internal void BindSummary(UnityEngine.UI.RawImage image)
        {
            if (summaryImage != null && summaryImage != image && summaryImage.texture == summaryTarget) summaryImage.texture = null;
            summaryImage = image;
            RequestViews();
        }

        /// <summary>True when the summary picture is rendered by the viewer (a usable rig is bound).</summary>
        internal bool RendersSummary => summaryImage != null && IsBound;

        /// <summary>Marks the angle views for a refresh at the end of the frame (after the look was applied).</summary>
        public void RequestViews()
        {
            var due = Time.frameCount + 1;
            if (viewsDueFrame < 0 || viewsDueFrame > due) viewsDueFrame = due;
        }

        /// <summary>
        /// Approximate modular preview while the game cannot assemble the real parts yet: wing, eye, proboscis and
        /// body styles become bone scales/rotations on the clone and the body colour a tint, so choosing an option
        /// visibly changes the viewer. Null or empty restores the clone.
        /// </summary>
        internal void SetApproximation(IReadOnlyList<PreviewPartStyle> styles)
        {
            approximation = styles;
            RestoreBoneEdits();
            ClearApproximationObjects();
            approximatedInstance = null;
            RequestViews();
        }

        // The mosquito faces the camera with both wings open in a V (UI-06 6), turned just enough to read its body.
        private float DefaultYaw => visibleRole == AlfaRole.Mosquito ? 15f : 0f;

        /// <summary>
        /// Before this frame's animation: undo last frame's approximate edits where nothing else wrote the bone, so
        /// the LateUpdate edit always starts from the fresh pose and never compounds (animator, blink or none).
        /// </summary>
        private void Update()
        {
            for (var i = 0; i < boneEdits.Count; i++)
            {
                var edit = boneEdits[i];
                if (edit.Bone == null || !edit.HasBase) continue;
                if (edit.Bone.localScale == edit.WrittenScale) edit.Bone.localScale = edit.BaseScale;
                if (edit.Bone.localRotation == edit.WrittenRotation) edit.Bone.localRotation = edit.BaseRotation;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            targetDirty = true;
            UpdateOutputUv();
        }

        private void LateUpdate()
        {
            if (!IsBound || !previewVisible) return;
            if (targetDirty) EnsureTarget();
            ApplyApproximationEdits();
            if (framingStale && instance != null && instance.activeInHierarchy)
            {
                // First animated frame of a new clone: frame the posed character, not its bind pose.
                framingStale = false;
                RecalculateFraming();
                ApplyOrbit();
                RequestViews();
            }
            // The angle views follow their frames' aspect (VISTA PREVIA grows taller when the panel has room).
            for (var i = 0; i < viewImages.Length && i < viewTargets.Length; i++)
                if (viewTargets[i] != null && viewImages[i] != null && Mathf.Abs(ViewAspect(viewImages[i]) - (float)viewTargets[i].width / viewTargets[i].height) > 0.03f)
                    RequestViews();
            if (summaryTarget != null && summaryImage != null && Mathf.Abs(ViewAspect(summaryImage) - (float)summaryTarget.width / summaryTarget.height) > 0.03f)
                RequestViews();
            if (viewsDueFrame >= 0 && Time.frameCount >= viewsDueFrame && instance != null) RenderViews();
        }

        /// <summary>Render target at the viewport's pixel size: the camera sees exactly the panel, nothing stretched.</summary>
        private void EnsureTarget()
        {
            targetDirty = false;
            if (!IsBound || output == null) return;
            var rect = output.rectTransform.rect;
            var canvas = output.canvas != null ? output.canvas.rootCanvas : null;
            var scale = canvas != null ? Mathf.Max(0.25f, canvas.scaleFactor) : 1f;
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 0, 2048);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 0, 2048);
            if (width < 64 || height < 64)
            {
                UpdateOutputUv();
                return;
            }
            if (ownTarget != null && Mathf.Abs(ownTarget.width - width) <= 2 && Mathf.Abs(ownTarget.height - height) <= 2) return;
            var previous = ownTarget;
            ownTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "LMS customization viewer",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            ownTarget.Create();
            setup.Camera.targetTexture = ownTarget;
            output.texture = ownTarget;
            output.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (previous != null)
            {
                previous.Release();
                Destroy(previous);
            }
            if (instance != null) ApplyOrbit();
        }

        /// <summary>
        /// Fallback for the shared square target (before the viewport is laid out): shown "contain" across the
        /// panel, the clamped edge texels repeat the studio colour.
        /// </summary>
        private void UpdateOutputUv()
        {
            if (output == null || (ownTarget != null && output.texture == ownTarget)) return;
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

        private float TargetAspect()
        {
            var texture = ownTarget != null ? (Texture)ownTarget : setup?.Texture;
            return texture != null && texture.height > 0 ? (float)texture.width / texture.height : Mathf.Max(0.1f, setup?.Camera != null ? setup.Camera.aspect : 1f);
        }

        /// <summary>Pedestal (child of the orbit stage), painted bedroom (fixed to the camera) and warm rim light.</summary>
        private void EnsureStudio()
        {
            if (!IsBound) return;
            EnsurePedestal();
            if (backdrop == null)
            {
                var texture = Resources.Load<Texture2D>(BackdropResource);
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (texture != null && shader != null)
                {
                    backdropAspect = texture.height > 0 ? (float)texture.width / texture.height : 1.6f;
                    backdropMaterial = new Material(shader) { name = "LMS preview bedroom", hideFlags = HideFlags.HideAndDontSave };
                    if (backdropMaterial.HasProperty("_BaseMap")) backdropMaterial.SetTexture("_BaseMap", texture);
                    if (backdropMaterial.HasProperty("_BaseColor")) backdropMaterial.SetColor("_BaseColor", Color.white);
                    backdropMesh = QuadMesh();
                    backdrop = new GameObject("PreviewBackdrop", typeof(MeshFilter), typeof(MeshRenderer));
                    backdrop.layer = PreviewLayer();
                    backdrop.transform.SetParent(setup.Stage.parent, false);
                    backdrop.GetComponent<MeshFilter>().sharedMesh = backdropMesh;
                    var renderer = backdrop.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = backdropMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    backdrop.SetActive(false);
                    setup.Camera.backgroundColor = new Color(0.16f, 0.11f, 0.09f, 1f);
                }
            }
            // Point lights on the preview layer only (never a directional that could become the scene's main light).
            if (rimLight == null) rimLight = StudioLight("PreviewRimLight", RimColor);
            if (fillLight == null) fillLight = StudioLight("PreviewFillLight", new Color(1f, 0.9f, 0.78f));
        }

        private Light StudioLight(string name, Color color)
        {
            var node = new GameObject(name);
            node.transform.SetParent(setup.Stage.parent, false);
            var light = node.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << PreviewLayer();
            node.SetActive(false);
            return light;
        }

        private void SetStudioActive(bool active)
        {
            if (pedestal != null) pedestal.SetActive(active);
            if (backdrop != null) backdrop.SetActive(active);
            if (rimLight != null) rimLight.gameObject.SetActive(active);
            if (fillLight != null) fillLight.gameObject.SetActive(active);
        }

        private void DestroyStudio()
        {
            if (pedestal != null) { Destroy(pedestal); pedestal = null; }
            if (backdrop != null) { Destroy(backdrop); backdrop = null; }
            if (rimLight != null) { Destroy(rimLight.gameObject); rimLight = null; }
            if (fillLight != null) { Destroy(fillLight.gameObject); fillLight = null; }
        }

        private static Mesh QuadMesh()
        {
            var mesh = new Mesh { name = "LMS preview backdrop quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
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
            if (pedestalTopMaterial == null) pedestalTopMaterial = PedestalMaterial(shader, PedestalTop, "LMS preview pedestal top");
            if (pedestalSideMaterial == null) pedestalSideMaterial = PedestalMaterial(shader, PedestalEdge, "LMS preview pedestal side");
            var renderer = pedestal.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { pedestalTopMaterial, pedestalSideMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
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
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
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
            distance = FitDistance(TargetAspect(), true);
            cameraTransform.position = focusWorld + Vector3.forward * distance;
            cameraTransform.LookAt(focusWorld, Vector3.up);
            PlaceStudio(focusWorld);
        }

        /// <summary>The painting fills the frustum behind the character ("cover"); the rim light sits behind it.</summary>
        private void PlaceStudio(Vector3 focusWorld)
        {
            var camera = setup.Camera;
            var cameraTransform = camera.transform;
            var forward = cameraTransform.forward;
            var worldRadius = Mathf.Max(0.001f, characterRadius);
            if (backdrop != null)
            {
                var depth = distance + Mathf.Max(worldRadius * 1.6f, pedestalRadius * 2.2f * setup.Stage.lossyScale.x);
                var height = 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                var aspect = TargetAspect();
                var width = height * aspect;
                var size = width / height > backdropAspect ? new Vector2(width, width / backdropAspect) : new Vector2(height * backdropAspect, height);
                backdrop.transform.SetPositionAndRotation(cameraTransform.position + forward * depth, Quaternion.LookRotation(forward, Vector3.up));
                backdrop.transform.localScale = new Vector3(size.x, size.y, 1f);
                if (camera.farClipPlane < depth * 1.2f) camera.farClipPlane = depth * 1.2f;
            }
            // Warm rim from behind, low and to the right (edges glow, top faces barely); soft fill from the front left.
            // Intensity grows with the square of the light distance, so a 1.8 m human and a 12 cm mosquito receive
            // the same light.
            var reach = worldRadius * 2.5f;
            if (rimLight != null)
            {
                rimLight.transform.position = focusWorld + (forward * 0.8f + cameraTransform.right * 0.45f + Vector3.up * 0.35f).normalized * reach;
                rimLight.range = reach * 3f;
                rimLight.intensity = 1.8f * reach * reach;
            }
            if (fillLight != null)
            {
                fillLight.transform.position = focusWorld + (-forward * 0.85f - cameraTransform.right * 0.45f + Vector3.up * 0.25f).normalized * reach;
                fillLight.range = reach * 3f;
                fillLight.intensity = 0.8f * reach * reach;
            }
        }

        /// <summary>
        /// Corners of every visible mesh of the clone in stage space: renderers that are enabled and active under the
        /// clone (an inactive held prop never widens the frame), skinned meshes baked in their current pose.
        /// </summary>
        private void CollectCharacterPoints(List<Vector3> into)
        {
            into.Clear();
            if (instance == null) return;
            var root = instance.transform;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                if (!ActiveUnder(renderer.transform, root)) continue;
                // Imported skin culling bounds are not necessarily in renderer-transform space.
                // BakeMesh(true) compensates renderer scale before TransformPoint applies it.
                // False would count a scaled visual root twice (see CharacterContentBuilder).
                var bounds = renderer.localBounds;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    if (skin.sharedMesh == null) continue;
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
                    into.Add(setup.Stage.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center + offset)));
                }
            }
        }

        /// <summary>activeSelf of the transform and every parent up to the clone root (the clone itself may be hidden).</summary>
        private static bool ActiveUnder(Transform item, Transform root)
        {
            for (var node = item; node != null && node != root; node = node.parent)
                if (!node.gameObject.activeSelf) return false;
            return true;
        }

        private void RecalculateFraming()
        {
            CollectCharacterPoints(framingPoints);

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
            characterPointCount = framingPoints.Count;
            characterFocusLocal = character.center;
            PlacePedestal(character);
            var combined = new Bounds(framingPoints[0], Vector3.zero);
            foreach (var point in framingPoints) combined.Encapsulate(point);
            focusLocal = combined.center;
            var radius = setup.Stage.TransformVector(combined.extents).magnitude;
            characterRadius = radius;
            // Only the isolated preview camera changes: never enlarge the prefab or game actor.
            setup.Camera.nearClipPlane = Mathf.Clamp(radius * 0.02f, 0.0001f, 0.01f);
        }

        /// <summary>Distance that fits every framing point at the current stage rotation for an aspect.</summary>
        private float FitDistance(float aspect, bool applyZoom, bool characterOnly = false)
        {
            if (framingPoints.Count == 0) return distance;
            var focus = characterOnly ? characterFocusLocal : focusLocal;
            var count = characterOnly && characterPointCount > 0 ? characterPointCount : framingPoints.Count;
            var camera = setup.Camera;
            camera.aspect = aspect;
            var verticalHalfFov = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            var tanVertical = Mathf.Max(0.01f, Mathf.Tan(verticalHalfFov));
            var tanHorizontal = tanVertical * Mathf.Max(0.1f, aspect);
            var closestDepth = 0f;
            var fit = 0.0001f;
            for (var index = 0; index < count; index++)
            {
                var offset = setup.Stage.TransformVector(framingPoints[index] - focus);
                var projectedDistance = Mathf.Max(Mathf.Abs(offset.x) / tanHorizontal,
                    Mathf.Abs(offset.y) / tanVertical);
                // Positive Z points toward the camera; include depth per corner, not globally.
                fit = Mathf.Max(fit, offset.z + projectedDistance * 1.1f);
                closestDepth = Mathf.Max(closestDepth, offset.z);
            }
            var nearest = Mathf.Max(fit * 0.55f, closestDepth + camera.nearClipPlane * 2f);
            fit = Mathf.Max(fit, nearest);
            if (!applyZoom) return fit;
            fitDistance = fit;
            minDistance = nearest;
            maxDistance = fitDistance * 2.2f;
            return Mathf.Clamp(fitDistance * zoomFactor, minDistance, maxDistance);
        }

        /// <summary>
        /// Renders the angle views and the rail summary: no painting and no pedestal, the light thumbnail colour
        /// behind. The character is measured again in its current pose; FRENTE, ESPALDA and LADO share one camera
        /// distance (the largest any of them needs to show the figure at 85 % of its height and within 92 % of its
        /// width), each centred on the figure, so the three read at the same size.
        /// </summary>
        private void RenderViews()
        {
            viewsDueFrame = -1;
            if (viewImages.Length == 0 && summaryImage == null) return;
            var camera = setup.Camera;
            var cameraTransform = camera.transform;
            var savedRotation = setup.Stage.localRotation;
            var savedPosition = cameraTransform.position;
            var savedCameraRotation = cameraTransform.rotation;
            var savedTarget = camera.targetTexture;
            var savedBackground = camera.backgroundColor;
            var savedDistance = distance;
            var backdropWasActive = backdrop != null && backdrop.activeSelf;
            var pedestalWasActive = pedestal != null && pedestal.activeSelf;
            try
            {
                if (backdrop != null) backdrop.SetActive(false);
                if (pedestal != null) pedestal.SetActive(false);
                CollectCharacterPoints(viewPoints);
                if (viewPoints.Count == 0) return;
                camera.backgroundColor = ViewBackground;
                // One distance for the three angles (same aspect: the views share their frame size).
                var aspect = viewImages.Length > 0 && viewImages[0] != null ? ViewAspect(viewImages[0]) : (float)ViewWidth / ViewHeight;
                var width = Mathf.Clamp(Mathf.RoundToInt(ViewTargetHeight * aspect), 64, 1024);
                aspect = (float)width / ViewTargetHeight;
                var shared = 0f;
                for (var i = 0; i < viewImages.Length; i++)
                    shared = Mathf.Max(shared, ViewDistance(i < viewYaws.Length ? viewYaws[i] : 0f, aspect, 1f, ViewHeightFill, out _));
                for (var i = 0; i < viewImages.Length; i++)
                {
                    var image = viewImages[i];
                    if (image == null) continue;
                    var viewAspect = ViewAspect(image);
                    var viewWidth = Mathf.Clamp(Mathf.RoundToInt(ViewTargetHeight * viewAspect), 64, 1024);
                    EnsureViewTarget(ref viewTargets[i], image, viewWidth, "LMS customization angle view " + i);
                    var yawDegrees = i < viewYaws.Length ? viewYaws[i] : 0f;
                    ViewDistance(yawDegrees, aspect, 1f, ViewHeightFill, out var centre);
                    RenderView(viewTargets[i], image, yawDegrees, centre, shared, aspect);
                }
                if (summaryImage != null)
                {
                    // TU HUMANO / TU MOSQUITO: three-quarter view; the human from the nightcap to the hips.
                    camera.backgroundColor = SummaryBackground;
                    var summaryAspect = ViewAspect(summaryImage);
                    var summaryWidth = Mathf.Clamp(Mathf.RoundToInt(ViewTargetHeight * summaryAspect), 64, 1024);
                    EnsureViewTarget(ref summaryTarget, summaryImage, summaryWidth, "LMS customization summary");
                    summaryAspect = (float)summaryWidth / ViewTargetHeight;
                    var summaryYaw = visibleRole == AlfaRole.Human ? 22f : 30f;
                    var fromTop = visibleRole == AlfaRole.Human ? 0.6f : 1f;
                    var summaryDistance = ViewDistance(summaryYaw, summaryAspect, fromTop, 0.9f, out var summaryCentre);
                    RenderView(summaryTarget, summaryImage, summaryYaw, summaryCentre, summaryDistance, summaryAspect);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_PREVIEW_VIEWS_FAILED: " + exception.Message);
            }
            finally
            {
                setup.Stage.localRotation = savedRotation;
                cameraTransform.SetPositionAndRotation(savedPosition, savedCameraRotation);
                camera.targetTexture = savedTarget;
                camera.backgroundColor = savedBackground;
                camera.aspect = TargetAspect();
                distance = savedDistance;
                if (backdrop != null) backdrop.SetActive(backdropWasActive);
                if (pedestal != null) pedestal.SetActive(pedestalWasActive);
            }
        }

        private void EnsureViewTarget(ref RenderTexture target, UnityEngine.UI.RawImage image, int width, string name)
        {
            if (target != null && (target.width != width || target.height != ViewTargetHeight))
            {
                if (image.texture == target) image.texture = null;
                target.Release();
                Destroy(target);
                target = null;
            }
            if (target != null) return;
            target = new RenderTexture(width, ViewTargetHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear
            };
            target.Create();
        }

        private void RenderView(RenderTexture target, UnityEngine.UI.RawImage image, float yawDegrees, Vector3 centre, float cameraDistance, float aspect)
        {
            var camera = setup.Camera;
            setup.Stage.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            camera.transform.position = centre + Vector3.forward * cameraDistance;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            camera.targetTexture = target;
            camera.aspect = aspect;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            image.texture = target;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.enabled = true;
        }

        /// <summary>
        /// Camera distance (looking along -Z at the stage turned by <paramref name="yawDegrees"/>) that shows the top
        /// <paramref name="fromTop"/> of the figure at <paramref name="heightFill"/> of the view's height, within 92 %
        /// of its width, and the world point the view is centred on.
        /// </summary>
        private float ViewDistance(float yawDegrees, float aspect, float fromTop, float heightFill, out Vector3 centre)
        {
            var camera = setup.Camera;
            setup.Stage.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var tanVertical = Mathf.Max(0.01f, Mathf.Tan(Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad));
            var tanHorizontal = tanVertical * Mathf.Max(0.1f, aspect);
            var world = new Bounds(setup.Stage.TransformPoint(viewPoints[0]), Vector3.zero);
            foreach (var point in viewPoints) world.Encapsulate(setup.Stage.TransformPoint(point));
            var bottom = world.max.y - world.size.y * Mathf.Clamp01(fromTop);
            var shown = new Bounds(new Vector3(world.center.x, (world.max.y + bottom) * 0.5f, world.center.z), Vector3.zero);
            foreach (var point in viewPoints)
            {
                var p = setup.Stage.TransformPoint(point);
                if (p.y < bottom - 0.0001f) continue;
                shown.Encapsulate(p);
            }
            shown.Encapsulate(new Vector3(shown.center.x, bottom, shown.center.z));
            centre = shown.center;
            var needed = 0.0001f;
            foreach (var point in viewPoints)
            {
                var p = setup.Stage.TransformPoint(point);
                if (p.y < bottom - 0.0001f) continue;
                var offset = p - centre;
                needed = Mathf.Max(needed, offset.z + Mathf.Abs(offset.y) / (heightFill * tanVertical));
                needed = Mathf.Max(needed, offset.z + Mathf.Abs(offset.x) / (ViewWidthFill * tanHorizontal));
            }
            return Mathf.Max(needed, camera.nearClipPlane * 4f);
        }

        /// <summary>Displayed aspect of an angle view (its frame), the default 248 x 400 before layout.</summary>
        private static float ViewAspect(UnityEngine.UI.RawImage image)
        {
            var rect = image != null ? image.rectTransform.rect : Rect.zero;
            return rect.width > 1f && rect.height > 1f ? Mathf.Clamp(rect.width / rect.height, 0.3f, 1.5f) : (float)ViewWidth / ViewHeight;
        }

        private struct BoneEdit
        {
            public Transform Bone;
            public Vector3 Scale;
            public Quaternion Rotation;
            public bool HasBase;
            public Vector3 BaseScale;
            public Quaternion BaseRotation;
            public Vector3 WrittenScale;
            public Quaternion WrittenRotation;
        }

        /// <summary>
        /// Re-applies the approximate edits after animation every frame, on top of the pose written this frame
        /// (Update restored last frame's edit, so nothing compounds).
        /// </summary>
        private void ApplyApproximationEdits()
        {
            if (instance == null) return;
            if (approximatedInstance != instance)
            {
                RestoreBoneEdits();
                ClearApproximationObjects();
                approximatedInstance = instance;
                ResolveApproximation();
            }
            for (var i = 0; i < boneEdits.Count; i++)
            {
                var edit = boneEdits[i];
                if (edit.Bone == null) continue;
                edit.BaseScale = edit.Bone.localScale;
                edit.BaseRotation = edit.Bone.localRotation;
                edit.HasBase = true;
                edit.Bone.localScale = Vector3.Scale(edit.BaseScale, edit.Scale);
                edit.Bone.localRotation = edit.BaseRotation * edit.Rotation;
                edit.WrittenScale = edit.Bone.localScale;
                edit.WrittenRotation = edit.Bone.localRotation;
                boneEdits[i] = edit;
            }
        }

        private void RestoreBoneEdits()
        {
            foreach (var edit in boneEdits)
            {
                if (edit.Bone == null || !edit.HasBase) continue;
                if (edit.Bone.localScale == edit.WrittenScale) edit.Bone.localScale = edit.BaseScale;
                if (edit.Bone.localRotation == edit.WrittenRotation) edit.Bone.localRotation = edit.BaseRotation;
            }
            boneEdits.Clear();
        }

        private void ClearApproximationObjects()
        {
            foreach (var item in approximationObjects)
                if (item != null) Destroy(item);
            approximationObjects.Clear();
            foreach (var renderer in hiddenRenderers)
                if (renderer != null) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        private void ResolveApproximation()
        {
            if (instance == null || approximation == null) return;
            foreach (var style in approximation)
            {
                switch (style.Part)
                {
                    case PreviewPart.Wings:
                        // Bone Y runs along the wing (the rig's wing vertices span local Y 0..0.36 from the bone origin, the
                        // wing root); X/Z are its breadth. REDONDAS: the wing's own mesh and material (lavender #DCDDF5 at
                        // 0.45 with its veins) scaled X 1.4 / Y 0.7 about the root: shorter and wider, still translucent,
                        // clear of the head.
                        var wing = style.Variant == "Round" ? new Vector3(1.4f, 0.7f, 1.4f) : style.Variant == "Long" ? new Vector3(0.58f, 1.5f, 0.58f)
                            : style.Variant == "Short" ? new Vector3(1f, 0.55f, 1f) : Vector3.one;
                        Edit("Wing.L", wing, Quaternion.identity);
                        Edit("Wing.R", wing, Quaternion.identity);
                        break;
                    case PreviewPart.Eyes:
                        var eye = style.Variant == "Small" ? 0.78f : style.Variant == "Big" ? 1.18f : 1f;
                        Edit("Pupil.L", Vector3.one * eye, Quaternion.identity);
                        Edit("Pupil.R", Vector3.one * eye, Quaternion.identity);
                        if (style.Variant == "Angry")
                        {
                            // ENOJADOS: dark wedge brows over each eye and the upper lids closed 30 degrees.
                            Edit("LidUpper.L", Vector3.one, LidClosure("LidUpper.L", 30f));
                            Edit("LidUpper.R", Vector3.one, LidClosure("LidUpper.R", 30f));
                            AngryBrows();
                        }
                        else if (style.Variant == "Sleepy")
                        {
                            Edit("LidUpper.L", Vector3.one, LidClosure("LidUpper.L", 48f));
                            Edit("LidUpper.R", Vector3.one, LidClosure("LidUpper.R", 48f));
                        }
                        if (style.Variant == "Big" || style.Variant == "Small") Edit("Head", Vector3.one * (style.Variant == "Big" ? 1.12f : 0.9f), Quaternion.identity);
                        break;
                    case PreviewPart.Proboscis:
                        if (style.Variant == "Curved") Edit("Proboscis", Vector3.one, Quaternion.Euler(24f, 0f, 0f));
                        else if (style.Variant == "Long") Edit("Proboscis", new Vector3(1f, 1.4f, 1f), Quaternion.identity);
                        else if (style.Variant == "Short") Edit("Proboscis", new Vector3(1f, 0.58f, 1f), Quaternion.identity);
                        break;
                    case PreviewPart.Body:
                        if (style.Variant == "Robust") { Edit("Abdomen01", new Vector3(1.3f, 1f, 1.3f), Quaternion.identity); Edit("Thorax", new Vector3(1.15f, 1f, 1.15f), Quaternion.identity); }
                        else if (style.Variant == "Slim") Edit("Abdomen01", new Vector3(0.75f, 1.05f, 0.75f), Quaternion.identity);
                        break;
                    case PreviewPart.Color:
                        Tint(style.Color);
                        break;
                }
            }
        }

        private void Edit(string boneName, Vector3 scale, Quaternion rotation)
        {
            if (scale == Vector3.one && rotation == Quaternion.identity) return;
            foreach (var bone in instance.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name != boneName) continue;
                boneEdits.Add(new BoneEdit { Bone = bone, Scale = scale, Rotation = rotation });
                return;
            }
        }

        /// <summary>
        /// Partial closure of a lid bone about the rig's own closing axis (read by name from the character's facial
        /// contract: the UI has no reference to Presentation); -Y by 90 degrees is the imported mosquito rig's value.
        /// </summary>
        private Quaternion LidClosure(string boneName, float degrees)
        {
            var axis = Vector3.down;
            var sign = 1f;
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "VisualAttentionContract") continue;
                var rig = component.GetType().GetField("Rig")?.GetValue(component);
                if (rig == null) break;
                foreach (var side in new[] { "LeftLids", "RightLids" })
                {
                    if (!(rig.GetType().GetField(side)?.GetValue(rig) is System.Array lids)) continue;
                    foreach (var lid in lids)
                    {
                        if (lid == null) continue;
                        var type = lid.GetType();
                        var bone = type.GetField("Bone")?.GetValue(lid) as Transform;
                        if (bone == null || bone.name != boneName) continue;
                        if (type.GetField("LocalAxis")?.GetValue(lid) is Vector3 localAxis && localAxis.sqrMagnitude > 0.0001f) axis = localAxis.normalized;
                        if (type.GetField("ClosedAngleDegrees")?.GetValue(lid) is float closed && Mathf.Abs(closed) > 0.001f) sign = Mathf.Sign(closed);
                    }
                }
                break;
            }
            return Quaternion.AngleAxis(sign * degrees, axis);
        }

        /// <summary>
        /// ENOJADOS: a dark #2A1A1A wedge over each eye, lower towards the middle of the face (about 26 degrees),
        /// parented to the head. The eyes are found from the eye-white surface of the character's skin mesh.
        /// </summary>
        private void AngryBrows()
        {
            Transform head = null;
            foreach (var bone in instance.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Head") { head = bone; break; }
            if (head == null) return;
            var root = instance.transform;
            var eyes = new List<Bounds>();
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = skin.sharedMaterials;
                var sub = System.Array.FindIndex(materials, material => material != null && material.name.Contains("EyeWhite"));
                if (sub < 0 || skin.sharedMesh == null) continue;
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);
                    if (sub >= baked.subMeshCount) continue;
                    var vertices = baked.vertices;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var found = false;
                        var bounds = new Bounds();
                        foreach (var index in baked.GetTriangles(sub))
                        {
                            var point = root.InverseTransformPoint(skin.transform.TransformPoint(vertices[index]));
                            if (Mathf.Sign(point.x) != side) continue;
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                        if (found) eyes.Add(bounds);
                    }
                }
                finally
                {
                    Destroy(baked);
                }
                break;
            }
            if (eyes.Count == 0) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) return;
            var material = new Material(shader) { name = "LMS preview angry brow", hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", BrowColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", BrowColor);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            approximationObjects.Add(material);
            foreach (var eye in eyes)
            {
                var inward = eye.center.x < 0f ? 1f : -1f;
                var length = eye.size.x * 0.92f;
                var thickness = eye.size.y * 0.2f;
                var depth = eye.size.z * 0.3f;
                var mesh = WedgeMesh(length, thickness, depth, inward);
                var node = new GameObject("AngryBrow" + (eye.center.x < 0f ? ".L" : ".R"), typeof(MeshFilter), typeof(MeshRenderer));
                node.layer = instance.layer;
                node.transform.SetParent(root, false);
                // On the top of the eye, a little forward, so it sits on the eye instead of sticking out like a horn.
                node.transform.localPosition = new Vector3(eye.center.x + inward * eye.size.x * 0.05f, eye.max.y - thickness * 0.15f, eye.center.z + eye.size.z * 0.3f);
                node.transform.localRotation = Quaternion.Euler(0f, 0f, -inward * 26f);
                node.transform.SetParent(head, true);
                node.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                approximationObjects.Add(node);
                approximationObjects.Add(mesh);
            }
        }

        /// <summary>Flat-shaded wedge along X: full thickness at the outer end, 55 % at the inner end (+X when inward is 1).</summary>
        private static Mesh WedgeMesh(float length, float thickness, float depth, float inward)
        {
            var half = length * 0.5f;
            float outerX = -half * inward, innerX = half * inward;
            float outerH = thickness * 0.5f, innerH = thickness * 0.5f * 0.55f;
            float front = depth * 0.5f, back = -depth * 0.5f;
            var corners = new[]
            {
                new Vector3(outerX, -outerH, front), new Vector3(outerX, outerH, front), new Vector3(innerX, innerH, front), new Vector3(innerX, -innerH, front),
                new Vector3(outerX, -outerH, back), new Vector3(outerX, outerH, back), new Vector3(innerX, innerH, back), new Vector3(innerX, -innerH, back)
            };
            var faces = new[] { new[] { 0, 1, 2, 3 }, new[] { 7, 6, 5, 4 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }, new[] { 4, 5, 1, 0 }, new[] { 3, 2, 6, 7 } };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var face in faces)
            {
                var start = vertices.Count;
                foreach (var index in face) vertices.Add(corners[index]);
                // Both windings: the brow reads from any angle whatever the handedness of the face order.
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = "LMS preview brow", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Body colour through the character view on the clone (looked up by name: UI has no reference to it).</summary>
        private void Tint(Color color)
        {
            foreach (var component in instance.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "CharacterView") continue;
                var method = component.GetType().GetMethod(visibleRole == AlfaRole.Mosquito ? "SetMosquitoColor" : "SetPajamaColor", new[] { typeof(Color) });
                method?.Invoke(component, new object[] { color });
                return;
            }
        }

        private void ReleaseTargets()
        {
            if (ownTarget != null)
            {
                if (setup?.Camera != null && setup.Camera.targetTexture == ownTarget) setup.Camera.targetTexture = setup.Texture;
                ownTarget.Release();
                Destroy(ownTarget);
                ownTarget = null;
            }
            targetDirty = true;
        }

        private void ReleaseViewTargets()
        {
            if (summaryTarget != null)
            {
                if (summaryImage != null && summaryImage.texture == summaryTarget) summaryImage.texture = null;
                summaryTarget.Release();
                Destroy(summaryTarget);
                summaryTarget = null;
            }
            for (var i = 0; i < viewTargets.Length; i++)
            {
                if (viewTargets[i] == null) continue;
                if (i < viewImages.Length && viewImages[i] != null && viewImages[i].texture == viewTargets[i]) viewImages[i].texture = null;
                viewTargets[i].Release();
                Destroy(viewTargets[i]);
                viewTargets[i] = null;
            }
        }

        private void OnDestroy()
        {
            ClearApproximationObjects();
            if (setup?.Camera != null) setup.Camera.enabled = false;
            if (instance != null) Destroy(instance);
            if (pedestal != null)
            {
                var mesh = pedestal.GetComponent<MeshFilter>().sharedMesh;
                Destroy(pedestal);
                if (mesh != null) Destroy(mesh);
            }
            if (backdrop != null) Destroy(backdrop);
            if (rimLight != null) Destroy(rimLight.gameObject);
            if (fillLight != null) Destroy(fillLight.gameObject);
            if (backdropMesh != null) Destroy(backdropMesh);
            if (backdropMaterial != null) Destroy(backdropMaterial);
            if (pedestalTopMaterial != null) Destroy(pedestalTopMaterial);
            if (pedestalSideMaterial != null) Destroy(pedestalSideMaterial);
            ReleaseTargets();
            ReleaseViewTargets();
        }
    }

    /// <summary>Part of the mosquito an option restyles, for the approximate preview and its thumbnail art.</summary>
    internal enum PreviewPart
    {
        None,
        Wings,
        Eyes,
        Proboscis,
        Body,
        Color
    }

    internal readonly struct PreviewPartStyle
    {
        public readonly PreviewPart Part;
        public readonly string Variant;
        public readonly Color Color;

        public PreviewPartStyle(PreviewPart part, string variant, Color color = default)
        {
            Part = part;
            Variant = variant ?? string.Empty;
            Color = color;
        }

        /// <summary>Art in Resources/AlfaUiOptionArt for this style (e.g. "WingsRound"), or null.</summary>
        public string ArtName => Part == PreviewPart.Wings || Part == PreviewPart.Eyes || Part == PreviewPart.Proboscis || Part == PreviewPart.Body
            ? Part + Variant : null;
    }
}
