using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LetMeSleep.Content.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LetMeSleep.Editor
{
    public static class CharacterRenderReview
    {
        public static void Capture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics device required.");
            const string output = "N:/LetMeSleep/Artifacts/review/alfa-characters";
            Directory.CreateDirectory(output);
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.35f, .38f, .46f);
                var light = new GameObject("ReviewKey").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 2.2f; light.color = new Color(1, .9f, .78f); light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(35, 150, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.07f, 0); floor.transform.localScale = new Vector3(8, .1f, 8);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(.18f, .22f, .29f); floor.GetComponent<Renderer>().sharedMaterial = material;
                var camera = new GameObject("ReviewCamera").AddComponent<Camera>(); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .14f, .21f); camera.nearClipPlane = .01f; camera.farClipPlane = 30;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (string species in new[] { "Human", "Mosquito" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/LetMeSleep/Content/Characters/Prefabs/LMS_{species}.prefab");
                    if (prefab == null) throw new InvalidOperationException($"Character prefab not loaded: {species}");
                    var actor = UnityEngine.Object.Instantiate(prefab); var view = actor.GetComponent<CharacterView>();
                    if (view == null || view.Animator == null || view.Animator.runtimeAnimatorController == null)
                        throw new InvalidOperationException($"Character view/controller missing: {species}");
                    view.Animator.enabled = true; view.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        skin.updateWhenOffscreen = true;
                        skin.forceMatrixRecalculationPerRender = true;
                    }
                    var clips = view.Animator.runtimeAnimatorController.animationClips;
                    var actionHashes = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    foreach (var pose in species == "Human" ? new[] { "Idle", "Crouch", "Clap", "Swat" } : new[] { "Idle", "Fly", "BiteLoop" })
                    {
                        var motion = view.Motions.First(m => m.StateName == pose || m.StateName.EndsWith("." + pose));
                        var clip = clips.First(c => c.name == motion.ClipName);
                        view.Animator.Rebind(); view.Animator.Update(0);
                        view.Animator.Play(motion.StateName, 0, .45f); view.Animator.Update(1f / 60f);
                        if (!view.Animator.GetCurrentAnimatorStateInfo(0).IsName(motion.StateName))
                            throw new InvalidOperationException($"Animator state not entered: {species}/{pose}");
                        // Direct sampling invalidates editor render skin caches deterministically;
                        // the controller gate above still proves the runtime state is wired.
                        clip.SampleAnimation(view.Animator.gameObject, clip.length * .45f); view.RefreshAnchors();
                        ValidateSkinnedPose(actor, species, pose);
                        foreach (var angle in pose == "Idle" ? new[] { 0f, 90f, 180f } : new[] { 35f })
                        {
                            bool human = species == "Human"; var target = new Vector3(0, human ? .96f : .015f, 0);
                            camera.fieldOfView = 36;
                            camera.transform.position = target + Quaternion.Euler(0, angle, 0) * new Vector3(0, human ? .18f : .12f, human ? 3.3f : .68f);
                            camera.transform.LookAt(target);
                            string hash = Render(camera, output + "/" + species + "_" + pose + "_" + angle + ".png");
                            if (pose != "Idle" && !actionHashes.Add(hash))
                                throw new InvalidOperationException($"Rendered action pose duplicated: {species}/{pose}");
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                UnityEngine.Object.DestroyImmediate(material);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); }
            Debug.Log("LMS_CHARACTER_RENDER_REVIEW_COMPLETE");
        }
        private static void ValidateSkinnedPose(GameObject actor, string species, string pose)
        {
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);
                    if (baked.vertexCount == 0 || !baked.vertices.All(v =>
                        !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                        !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                        !float.IsNaN(v.z) && !float.IsInfinity(v.z)))
                        throw new InvalidOperationException($"Invalid skinned pose: {species}/{pose}/{skin.name}");
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
            }
        }

        private static string Render(Camera camera, string path)
        {
            var target = new RenderTexture(960, 960, 24, RenderTextureFormat.ARGB32); target.Create();
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image = new Texture2D(960, 960, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); image.Apply();
                byte[] png = image.EncodeToPNG(); File.WriteAllBytes(path, png);
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
            }
            finally { RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}

