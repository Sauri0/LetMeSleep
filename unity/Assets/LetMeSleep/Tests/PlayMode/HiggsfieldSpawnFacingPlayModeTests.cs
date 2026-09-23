#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3.0 maps r4 (art director corrections #1 and #9), gameplay data rather than capture-harness tricks:
    /// every human spawn and the first mosquito spawn of each map start looking at least 1.5 m into free space along
    /// the catalog's authored facing (default +Z), the first camp view faces the fire, and a first-person human's head
    /// is hidden only from the camera inside it (free and spectator cameras see it; shadows are kept).
    /// </summary>
    public sealed class HiggsfieldSpawnFacingPlayModeTests
    {
        private const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";
        private readonly List<Object> created = new List<Object>();

        [UnityTest]
        public IEnumerator SpawnFacingsLookIntoFreeSpaceAndCampFacesTheFire()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            foreach (var entry in catalog.Entries)
            {
                var map = Object.Instantiate(entry.Prefab);
                created.Add(map.gameObject);
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                var checks = map.HumanSpawnPoints.Select((t, i) => (t, true, i)).Concat(new[] { (map.MosquitoSpawnPoints[0], false, 0) });
                foreach (var (point, human, index) in checks)
                {
                    entry.TryGetSpawnYaw(human, index, out float yaw);
                    Vector3 eye = point.position + Vector3.up * (human ? 1.55f : 0f);
                    Vector3 forward = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
                    float free = Physics.Raycast(eye, forward, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 30f;
                    Assert.That(free, Is.GreaterThanOrEqualTo(1.5f), entry.MapId + (human ? " human-" : " mosquito-") + index +
                        " faces a wall at " + free.ToString("F2") + " m (yaw " + yaw + ")");
                }
                if (entry.MapId == "hf-campamento-pinar-v2")
                {
                    Assert.That(entry.TryGetSpawnYaw(true, 0, out float yaw), Is.True, "camp first view is authored");
                    var fire = map.transform.Find("Environment/LGT_Fire");
                    Assert.That(fire, Is.Not.Null);
                    Vector3 toFire = fire.position - map.HumanSpawnPoints[0].position;
                    toFire.y = 0f;
                    float angle = Vector3.Angle(Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward, toFire);
                    Assert.That(angle, Is.LessThan(40f), "camp human-0 keeps the fire in view (ENV-04 02)");
                }
                Object.Destroy(map.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FirstPersonHeadIsHiddenOnlyFromTheCameraInsideIt()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(unlit, Is.Not.Null);
            var material = new Material(unlit);
            material.SetColor("_BaseColor", Color.red);
            created.Add(material);
            var body = new GameObject("HeadVisibilityTestCharacter");
            created.Add(body);
            body.transform.position = new Vector3(0f, -500f, 0f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(body.transform, false);
            head.transform.localScale = Vector3.one * 0.25f;
            var renderer = head.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            body.SetActive(false);
            var view = body.AddComponent<CharacterView>();
            view.HeadRenderers = new Renderer[] { renderer };
            body.SetActive(true);
            view.SetFirstPersonVisibility(true);
            yield return null;

            Color Center(Vector3 position)
            {
                var go = new GameObject("HeadVisibilityTestCamera");
                var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
                try
                {
                    var camera = go.AddComponent<Camera>();
                    camera.enabled = true;
                    camera.nearClipPlane = 0.01f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.black;
                    camera.transform.position = position;
                    camera.transform.LookAt(head.transform.position);
                    camera.targetTexture = target;
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                    pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                    pixels.Apply();
                    Color center = pixels.GetPixel(32, 32);
                    Object.DestroyImmediate(pixels);
                    return center;
                }
                finally
                {
                    RenderTexture.active = null;
                    Object.DestroyImmediate(go);
                    target.Release();
                    Object.DestroyImmediate(target);
                }
            }

            Color far = Center(head.transform.position + new Vector3(0f, 0.4f, -3f));
            Assert.That(far.r, Is.GreaterThan(0.5f), "a free/spectator camera sees the first-person head");
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), "back to shadow-only between renders");
            Color eye = Center(head.transform.position + new Vector3(0f, 0.02f, -0.2f));
            Assert.That(eye.r, Is.LessThan(0.1f), "the camera inside the head does not see it");
            view.SetFirstPersonVisibility(false);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in created) if (item) Object.Destroy(item);
            created.Clear();
            yield return null;
        }
    }
}
#endif
