using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.UI
{
    /// <summary>
    /// One-off role portraits for the training cards, rendered from the in-game character prefabs through the
    /// customization preview rig (its camera only sees layer 30 and has its own key light). The subject is
    /// placed away from the orbit stage, given a team-coloured key and rim light (blue human, red mosquito),
    /// rendered once into a texture and destroyed. Used only until painted portraits exist in
    /// Resources/AlfaUiPortraits.
    /// </summary>
    internal static class AlfaRolePortrait
    {
        private const int PreviewLayer = 30;
        private static readonly Vector3 StudioOffset = new Vector3(40f, 0f, 0f);

        internal static RenderTexture Render(CharacterPreviewSetup setup, AlfaRole role, int width, int height)
        {
            if (setup == null || !setup.IsUsable) return null;
            var camera = setup.Camera;
            var cameraTransform = camera.transform;
            var saved = new CameraState(camera);
            var human = role == AlfaRole.Human;
            var studio = new GameObject("TrainingPortraitStudio");
            RenderTexture target = null;
            try
            {
                studio.transform.position = setup.Stage.position + StudioOffset;
                var subject = Object.Instantiate(human ? setup.HumanPrefab : setup.MosquitoPrefab, studio.transform, false);
                subject.name = role + "TrainingPortrait";
                subject.transform.localRotation = Quaternion.Euler(0f, human ? 18f : 38f, 0f);
                foreach (var child in subject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
                foreach (var collider in subject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var animator in subject.GetComponentsInChildren<Animator>(true))
                {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Update(0f);
                }

                if (!TryBounds(subject, out var bounds)) return null;
                // Human: from the hips up, like the sketch card; mosquito: the whole insect.
                var aspect = (float)width / height;
                var frameBottom = human ? Mathf.Lerp(bounds.min.y, bounds.max.y, 0.5f) : bounds.min.y - bounds.size.y * 0.02f;
                var frameTop = bounds.max.y + bounds.size.y * (human ? 0.03f : 0.06f);
                var frameHeight = frameTop - frameBottom;
                var frameWidth = human ? frameHeight * aspect : Mathf.Max(bounds.size.x, bounds.size.z) * 1.08f;
                camera.fieldOfView = 28f;
                camera.aspect = aspect;
                var tanHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var distance = Mathf.Max(frameHeight * 0.5f / tanHalf, frameWidth * 0.5f / (tanHalf * aspect));
                var focus = new Vector3(bounds.center.x, (frameBottom + frameTop) * 0.5f, bounds.center.z);
                cameraTransform.position = focus + new Vector3(0f, frameHeight * 0.06f, 1f) * (distance + bounds.extents.z);
                cameraTransform.LookAt(focus, Vector3.up);
                camera.nearClipPlane = Mathf.Max(0.005f, distance * 0.02f);
                camera.farClipPlane = distance * 4f + bounds.size.magnitude;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = human ? new Color(0.07f, 0.16f, 0.36f, 1f) : new Color(0.26f, 0.07f, 0.12f, 1f);
                camera.cullingMask = 1 << PreviewLayer;
                setup.OnPreviewCreated?.Invoke(subject, camera);

                var team = human ? new Color(0.35f, 0.6f, 1f) : new Color(1f, 0.3f, 0.28f);
                var size = bounds.size.magnitude;
                AddLight(studio.transform, "Key", focus + new Vector3(-0.6f, 0.5f, 1f) * size, team, 2.6f, size * 3f);
                AddLight(studio.transform, "Rim", focus + new Vector3(0.9f, 0.8f, -0.8f) * size, Color.Lerp(team, Color.white, 0.35f), 3.2f, size * 3f);

                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "LMS training portrait " + role,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false
                };
                target.Create();
                camera.targetTexture = target;
                camera.aspect = aspect;
                // Explicit projection: the preview camera keeps the square customization aspect otherwise.
                camera.projectionMatrix = Matrix4x4.Perspective(camera.fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                return target;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_TRAINING_PORTRAIT_FAILED " + role + ": " + exception.Message);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                return null;
            }
            finally
            {
                saved.Restore(camera);
                studio.SetActive(false);
                Object.Destroy(studio);
            }
        }

        /// <summary>
        /// Head shot for the HUD objective badge (UI-06 7: the character's face on a #FFC93C disc). The camera frames
        /// the head bone (human: head and nightcap; mosquito: head, eyes and proboscis) from the front, over the
        /// given solid background so the circle reads as one badge.
        /// </summary>
        internal static RenderTexture RenderHead(CharacterPreviewSetup setup, AlfaRole role, int size, Color background)
        {
            if (setup == null || !setup.IsUsable) return null;
            var camera = setup.Camera;
            var saved = new CameraState(camera);
            var human = role == AlfaRole.Human;
            var studio = new GameObject("HudFaceStudio");
            RenderTexture target = null;
            try
            {
                studio.transform.position = setup.Stage.position + StudioOffset + new Vector3(0f, 0f, 12f);
                var subject = Spawn(setup, role, studio.transform, human ? 8f : 22f);
                // The badge shows the player's own character: the published look (colours and modular parts).
                setup.DressLocalLook?.Invoke(subject, role);
                BakeSkinnedMeshes(subject);
                if (!TryBounds(subject, out var bounds)) return null;
                var head = FindBone(subject.transform, "Head");
                Vector3 focus;
                float radius;
                if (human)
                {
                    // Head + nightcap: the top of the body, centred a little under the crown.
                    radius = bounds.size.y * 0.13f;
                    focus = new Vector3(head != null ? head.position.x : bounds.center.x, bounds.max.y - radius * 1.08f,
                        head != null ? head.position.z : bounds.center.z);
                }
                else
                {
                    // The face: both big eyes (their pupil pivots are the globe centres) and the proboscis root. The
                    // head bone sits at the neck, so framing on it showed the abdomen and cut the eyes.
                    var left = FindBone(subject.transform, "Pupil.L");
                    var right = FindBone(subject.transform, "Pupil.R");
                    if (left != null && right != null)
                    {
                        var span = Vector3.Distance(left.position, right.position);
                        radius = span * 1.35f;
                        focus = (left.position + right.position) * 0.5f + Vector3.down * span * 0.25f;
                    }
                    else
                    {
                        var size3 = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                        radius = size3 * 0.24f;
                        focus = head != null ? head.position : bounds.center;
                        focus.y -= radius * 0.12f;
                    }
                }
                target = NewTarget("LMS HUD face " + role, size, size);
                Frame(camera, focus, radius * 2.1f, radius * 2.1f, 1f, 26f, radius * 3f);
                camera.backgroundColor = background;
                camera.cullingMask = 1 << PreviewLayer;
                setup.OnPreviewCreated?.Invoke(subject, camera);
                var warm = new Color(1f, 0.93f, 0.82f);
                AddLight(studio.transform, "Key", focus + new Vector3(-0.5f, 0.6f, 1.2f) * radius * 4f, warm, 2.4f, radius * 14f);
                AddLight(studio.transform, "Fill", focus + new Vector3(0.9f, 0.1f, 0.8f) * radius * 4f, new Color(0.8f, 0.86f, 1f), 1.1f, radius * 14f);
                Submit(camera, target);
                return target;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_HUD_FACE_FAILED " + role + ": " + exception.Message);
                Release(target);
                return null;
            }
            finally
            {
                saved.Restore(camera);
                studio.SetActive(false);
                Object.Destroy(studio);
            }
        }

        /// <summary>
        /// Full-body figure on transparency for the results screen (UI-06 9: winners celebrating over the scene,
        /// no frame). The human raises both arms (the arm bones are posed and the skinned meshes baked, so the pose
        /// is exact even though the clone never animates); the mosquito rears up. The alpha is recovered from two
        /// renders over black and white, which works whatever the pipeline does with the alpha channel.
        /// </summary>
        internal static Texture2D RenderFigure(CharacterPreviewSetup setup, AlfaRole role, int width, int height, bool celebrate)
        {
            if (setup == null || !setup.IsUsable) return null;
            var camera = setup.Camera;
            var saved = new CameraState(camera);
            var human = role == AlfaRole.Human;
            var studio = new GameObject("ResultsFigureStudio");
            RenderTexture target = null;
            Texture2D black = null, white = null;
            try
            {
                studio.transform.position = setup.Stage.position + StudioOffset + new Vector3(0f, 0f, -12f);
                var subject = Spawn(setup, role, studio.transform, human ? (celebrate ? 10f : 24f) : (celebrate ? 32f : 48f));
                if (human && celebrate) RaiseArms(subject.transform);
                if (!human && celebrate) subject.transform.localRotation *= Quaternion.Euler(-14f, 0f, 0f);
                BakeSkinnedMeshes(subject);
                if (!TryBounds(subject, out var bounds)) return null;
                var aspect = (float)width / height;
                var frameHeight = bounds.size.y * 1.04f;
                var frameWidth = Mathf.Max(bounds.size.x, bounds.size.z) * 1.06f;
                var focus = bounds.center;
                target = NewTarget("LMS results figure " + role, width, height, 1);
                Frame(camera, focus, frameWidth, frameHeight, aspect, 24f, bounds.extents.magnitude);
                camera.cullingMask = 1 << PreviewLayer;
                setup.OnPreviewCreated?.Invoke(subject, camera);
                var size = bounds.size.magnitude;
                var warm = new Color(1f, 0.9f, 0.76f);
                AddLight(studio.transform, "Key", focus + new Vector3(-0.6f, 0.7f, 1f) * size, warm, 2.6f, size * 4f);
                AddLight(studio.transform, "Rim", focus + new Vector3(0.9f, 0.9f, -0.7f) * size,
                    human ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.5f, 0.45f), 2.8f, size * 4f);
                AddLight(studio.transform, "Fill", focus + new Vector3(0.8f, 0.1f, 1f) * size, new Color(0.75f, 0.82f, 1f), 1.1f, size * 4f);
                camera.backgroundColor = Color.black;
                Submit(camera, target);
                black = ReadBack(target);
                camera.backgroundColor = Color.white;
                Submit(camera, target);
                white = ReadBack(target);
                return Unmix(black, white, "LMS results figure " + role);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("LMS_UI_RESULTS_FIGURE_FAILED " + role + ": " + exception.Message);
                return null;
            }
            finally
            {
                saved.Restore(camera);
                Release(target);
                if (black != null) Object.Destroy(black);
                if (white != null) Object.Destroy(white);
                studio.SetActive(false);
                Object.Destroy(studio);
            }
        }

        private static GameObject Spawn(CharacterPreviewSetup setup, AlfaRole role, Transform parent, float yaw)
        {
            var subject = Object.Instantiate(role == AlfaRole.Human ? setup.HumanPrefab : setup.MosquitoPrefab, parent, false);
            subject.name = role + "UiStudioSubject";
            subject.SetActive(true);
            subject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var child in subject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            foreach (var collider in subject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var animator in subject.GetComponentsInChildren<Animator>(true))
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);
                animator.enabled = false;
            }
            return subject;
        }

        /// <summary>Both arms up and a little out, forearms nearly straight: the sketch's celebration pose.</summary>
        private static void RaiseArms(Transform root)
        {
            foreach (var side in new[] { "L", "R" })
            {
                var upper = FindBone(root, "UpperArm." + side);
                var lower = FindBone(root, "LowerArm." + side);
                if (upper == null || lower == null) continue;
                var outward = Vector3.Dot(lower.position - upper.position, root.right) >= 0f ? root.right : -root.right;
                Aim(upper, lower.position - upper.position, (Vector3.up * 0.93f + outward * 0.34f + root.forward * 0.08f).normalized);
                var hand = HandOf(lower);
                if (hand != null) Aim(lower, hand.position - lower.position, (Vector3.up * 0.97f + outward * 0.18f).normalized);
            }
        }

        private static void Aim(Transform bone, Vector3 current, Vector3 target)
        {
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current.normalized, target) * bone.rotation;
        }

        private static Transform HandOf(Transform bone)
        {
            for (var i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (child.name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0) return child;
            }
            return bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        internal static Transform FindBone(Transform root, string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item;
            return null;
        }

        /// <summary>Replaces each skinned mesh with a static snapshot of its current pose (exact and synchronous).</summary>
        private static void BakeSkinnedMeshes(GameObject subject)
        {
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                var mesh = new Mesh { name = skin.sharedMesh.name + " (posed)" };
                skin.BakeMesh(mesh, true);
                var node = new GameObject(skin.name + "Baked", typeof(MeshFilter), typeof(MeshRenderer));
                node.layer = PreviewLayer;
                node.transform.SetParent(skin.transform, false);
                // BakeMesh(useScale: true) returns the posed mesh in the renderer's local space (the orbit's
                // CollectCharacterPoints relies on the same), so the snapshot takes the renderer's transform as is. The
                // old inverse scale drew the mosquito (VisualRoot 0.5) twice its size, far from its bones: the HUD
                // head shot framed on the bones showed its legs and abdomen.
                node.transform.localScale = Vector3.one;
                node.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = skin.sharedMaterials;
                var block = new MaterialPropertyBlock();
                skin.GetPropertyBlock(block);
                renderer.SetPropertyBlock(block);
                // Per-material colours (skin tone, clothes, body colour) are blocks on each material index.
                for (var index = 0; index < skin.sharedMaterials.Length; index++)
                {
                    if (!skin.HasPropertyBlock()) break;
                    block.Clear();
                    skin.GetPropertyBlock(block, index);
                    if (!block.isEmpty) renderer.SetPropertyBlock(block, index);
                }
                node.AddComponent<BakedMeshOwner>().Mesh = mesh;
                skin.enabled = false;
            }
        }

        private sealed class BakedMeshOwner : MonoBehaviour
        {
            internal Mesh Mesh;
            private void OnDestroy() { if (Mesh != null) Destroy(Mesh); }
        }

        private static RenderTexture NewTarget(string name, int width, int height, int antiAliasing = 4)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                antiAliasing = antiAliasing
            };
            target.Create();
            return target;
        }

        private static void Frame(Camera camera, Vector3 focus, float frameWidth, float frameHeight, float aspect, float fov, float depth)
        {
            camera.fieldOfView = fov;
            camera.aspect = aspect;
            var tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var distance = Mathf.Max(frameHeight * 0.5f / tanHalf, frameWidth * 0.5f / (tanHalf * aspect));
            camera.transform.position = focus + Vector3.forward * (distance + depth * 0.2f);
            camera.transform.LookAt(focus, Vector3.up);
            camera.nearClipPlane = Mathf.Max(0.002f, distance * 0.02f);
            camera.farClipPlane = distance * 4f + depth * 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private static void Submit(Camera camera, RenderTexture target)
        {
            camera.targetTexture = target;
            camera.projectionMatrix = Matrix4x4.Perspective(camera.fieldOfView, (float)target.width / target.height, camera.nearClipPlane, camera.farClipPlane);
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
        }

        private static Texture2D ReadBack(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply(false);
            RenderTexture.active = previous;
            return image;
        }

        /// <summary>
        /// Coverage from the black/white pair: alpha = 1 - (white - black) / background difference (measured in a
        /// corner, so tone mapping of the clear colour cancels out); colour = black / alpha.
        /// </summary>
        private static Texture2D Unmix(Texture2D black, Texture2D white, string name)
        {
            var b = black.GetPixels32();
            var w = white.GetPixels32();
            var backgroundSpan = Mathf.Max(1f, ((w[0].r - b[0].r) + (w[0].g - b[0].g) + (w[0].b - b[0].b)) / 3f);
            var result = new Color32[b.Length];
            for (var i = 0; i < b.Length; i++)
            {
                var span = ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f;
                var alpha = Mathf.Clamp01(1f - span / backgroundSpan);
                if (alpha < 0.004f) { result[i] = new Color32(0, 0, 0, 0); continue; }
                result[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b[i].r / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b[i].g / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b[i].b / alpha), 0, 255),
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
            var texture = new Texture2D(black.width, black.height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(result);
            texture.Apply(false, true);
            return texture;
        }

        private static void Release(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            Object.Destroy(target);
        }

        /// <summary>
        /// World bounds of the visible meshes. Skinned meshes are baked first: their culling bounds are not
        /// reliable in renderer space (same approach as CharacterPreviewOrbit).
        /// </summary>
        private static bool TryBounds(GameObject subject, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            var baked = new Mesh();
            try
            {
                foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                    Bounds local;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        if (skin.sharedMesh == null) continue;
                        skin.BakeMesh(baked, true);
                        if (baked.vertexCount == 0) continue;
                        baked.RecalculateBounds();
                        local = baked.bounds;
                    }
                    else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                        local = filter.sharedMesh.bounds;
                    else continue;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        var world = renderer.transform.TransformPoint(local.center + offset);
                        if (!found) { bounds = new Bounds(world, Vector3.zero); found = true; }
                        else bounds.Encapsulate(world);
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            return found && bounds.size.sqrMagnitude > 0.0001f;
        }

        private static void AddLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var node = new GameObject("Portrait" + name + "Light");
            node.transform.SetParent(parent, false);
            node.transform.position = position;
            var light = node.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        private readonly struct CameraState
        {
            private readonly RenderTexture targetTexture;
            private readonly bool enabled;
            private readonly CameraClearFlags clearFlags;
            private readonly Color backgroundColor;
            private readonly float fieldOfView, nearClipPlane, farClipPlane;
            private readonly int cullingMask;
            private readonly Vector3 position;
            private readonly Quaternion rotation;

            internal CameraState(Camera camera)
            {
                targetTexture = camera.targetTexture;
                enabled = camera.enabled;
                clearFlags = camera.clearFlags;
                backgroundColor = camera.backgroundColor;
                fieldOfView = camera.fieldOfView;
                nearClipPlane = camera.nearClipPlane;
                farClipPlane = camera.farClipPlane;
                cullingMask = camera.cullingMask;
                position = camera.transform.position;
                rotation = camera.transform.rotation;
            }

            internal void Restore(Camera camera)
            {
                camera.targetTexture = targetTexture;
                camera.clearFlags = clearFlags;
                camera.backgroundColor = backgroundColor;
                camera.fieldOfView = fieldOfView;
                camera.nearClipPlane = nearClipPlane;
                camera.farClipPlane = farClipPlane;
                camera.cullingMask = cullingMask;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.ResetProjectionMatrix();
                camera.ResetAspect();
                camera.enabled = enabled;
            }
        }
    }
}
