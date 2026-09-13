#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LetMeSleep.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public class HiggsfieldGpuWaterPlayModeTests
    {
        [Serializable] class Config { public string prefab, output; }
        GameObject instance, cameraObject;
        RenderTexture target;
        Texture2D frame;
        [UnityTest] public IEnumerator ExplicitGpuWaterRendersMotionAndRestoresItsSource()
        {
            var args = System.Environment.GetCommandLineArgs(); int arg = Array.IndexOf(args, "-higgsfieldReview");
            if (arg < 0) Assert.Ignore("Requires explicit Higgsfield map review configuration.");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Run GPU proof with a graphics device.");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(args[arg + 1]));
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(config.prefab));
            yield return null;
            var bindings = instance.GetComponentsInChildren<HiggsfieldGpuWaterBinding>();
            Assert.That(bindings.Length, Is.GreaterThan(0));
            Directory.CreateDirectory(config.output);
            cameraObject = new GameObject("GpuWaterProofCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31; camera.orthographic = true; camera.orthographicSize = 2.5f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 1000;
            target = new RenderTexture(512, 512, 24); target.Create(); camera.targetTexture = target;
            frame = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var report = new System.Text.StringBuilder();
            foreach (var binding in bindings)
            {
                var helper = binding.GetComponent<HiggsfieldGpuWater>();
                var water = binding.Water; var filter = water.GetComponent<MeshFilter>();
                var source = filter.sharedMesh; var originalVertices = source.vertices;
                Assert.That(AssetDatabase.Contains(source), Is.True);
                Assert.That(helper.IsBound, Is.True);
                Assert.That(ShaderUtil.ShaderHasError(binding.WaterShader), Is.False);
                Assert.That(water.GetComponent<Collider>(), Is.Null);
                Assert.That(water.sharedMaterials.All(m => !AssetDatabase.Contains(m) && m.shader == binding.WaterShader), Is.True);
                int oldLayer = water.gameObject.layer; water.gameObject.layer = 31;
                var focus = water.transform.TransformPoint(originalVertices[originalVertices.Length / 3]);
                camera.transform.position = focus + new Vector3(4, 2, -4); camera.transform.LookAt(focus);
                helper.SetTimeOverride(0); yield return null;
                var before = Capture(camera, Path.Combine(config.output, water.name + "-gpu-t0.png"));
                helper.SetTimeOverride(2.5f); yield return null;
                var after = Capture(camera, Path.Combine(config.output, water.name + "-gpu-t2_5.png"));
                int changed = before.Zip(after, (a, b) => a.Equals(b) ? 0 : 1).Sum();
                Assert.That(changed, Is.GreaterThan(10), "Fixed camera must observe shader-driven pixel motion.");
                Assert.That(after.Count(p => p.r > 245 && p.g < 5 && p.b > 245), Is.LessThan(10), "Shader must not render error magenta.");
                Assert.That(filter.sharedMesh, Is.SameAs(source));
                CollectionAssert.AreEqual(originalVertices, source.vertices, "GPU water must not mutate source vertices.");
                binding.enabled = false;
                Assert.That(helper.IsBound, Is.False);
                Assert.That(water.sharedMaterials.All(AssetDatabase.Contains), Is.True, "Disable must restore source materials.");
                var restored = water.sharedMaterials;
                binding.enabled = true; yield return null;
                Assert.That(helper.IsBound, Is.True, "Explicit binding must support prefab reactivation.");
                binding.enabled = false; CollectionAssert.AreEqual(restored, water.sharedMaterials);
                water.gameObject.layer = oldLayer;
                report.AppendLine(water.name + ": " + source.vertexCount + " source vertices unchanged; " + changed + " changed pixels at t0/t2.5; material restoration and reactivation PASS.");
            }
            File.WriteAllText(Path.Combine(config.output, "gpu-water-playmode.txt"), "PASS Unity " + Application.unityVersion + " / " + SystemInfo.graphicsDeviceType + "\n" + report + "Not a performance benchmark or full depth-pass certification.");
        }
        Color32[] Capture(Camera camera, string path)
        {
            camera.Render(); var previous = RenderTexture.active;
            try { RenderTexture.active = target; frame.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); frame.Apply(); File.WriteAllBytes(path, frame.EncodeToPNG()); return frame.GetPixels32(); }
            finally { RenderTexture.active = previous; }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (cameraObject) Object.Destroy(cameraObject); if (instance) Object.Destroy(instance);
            if (target) { target.Release(); Object.Destroy(target); } if (frame) Object.Destroy(frame);
            yield return null;
        }
    }
}
#endif
