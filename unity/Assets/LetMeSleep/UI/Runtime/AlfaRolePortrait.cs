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
