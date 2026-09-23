#if UNITY_EDITOR
using System;
using LetMeSleep.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>v0.3.0 map Decor contract (HiggsfieldMapCatalog.ValidateDecor): visual-only prefab assets. Plain [Test]s in the
    /// PlayMode assembly because the EditMode test assemblies have no engine references; temporary probe prefabs are deleted.</summary>
    public sealed class HiggsfieldDecorValidationTests
    {
        private const string Folder = "Assets/LetMeSleep/Tests/PlayMode";
        private string path;

        private GameObject Save(Action<GameObject> build)
        {
            var root = new GameObject("DecorValidationProbe");
            try
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(prop.GetComponent<Collider>());
                prop.transform.SetParent(root.transform, false);
                build(root);
                path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/DecorValidationProbe.prefab");
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TearDown]
        public void Delete()
        {
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            path = null;
        }

        [Test]
        public void AcceptsMeshesAndShadowlessPointLights()
        {
            var decor = Save(root =>
            {
                var light = new GameObject("Lamp").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Point; light.shadows = LightShadows.None; light.range = 4f;
                light.cullingMask &= ~(1 << 30);
            });
            Assert.DoesNotThrow(() => HiggsfieldMapCatalog.ValidateDecor(decor, "probe"));
            Assert.DoesNotThrow(() => HiggsfieldMapCatalog.ValidateDecor(null, "probe"), "decor is optional");
        }

        [Test]
        public void RejectsCollidersRigidbodiesAndSceneObjects()
        {
            var withCollider = Save(root => root.transform.GetChild(0).gameObject.AddComponent<BoxCollider>());
            Assert.Throws<InvalidOperationException>(() => HiggsfieldMapCatalog.ValidateDecor(withCollider, "probe"));
            Delete();
            var withBody = Save(root => root.AddComponent<Rigidbody>());
            Assert.Throws<InvalidOperationException>(() => HiggsfieldMapCatalog.ValidateDecor(withBody, "probe"));
            var sceneObject = new GameObject("SceneDecor");
            try { Assert.Throws<InvalidOperationException>(() => HiggsfieldMapCatalog.ValidateDecor(sceneObject, "probe")); }
            finally { Object.DestroyImmediate(sceneObject); }
        }

        [Test]
        public void RejectsDirectionalShadowedOrLongRangeLights()
        {
            foreach (var configure in new Action<Light>[]
            {
                l => l.type = LightType.Directional,
                l => { l.type = LightType.Point; l.shadows = LightShadows.Soft; },
                l => { l.type = LightType.Point; l.range = 40f; }
            })
            {
                var decor = Save(root =>
                {
                    var light = new GameObject("Lamp").AddComponent<Light>();
                    light.transform.SetParent(root.transform, false);
                    light.type = LightType.Point; light.shadows = LightShadows.None; light.range = 4f; light.cullingMask &= ~(1 << 30);
                    configure(light);
                });
                Assert.Throws<InvalidOperationException>(() => HiggsfieldMapCatalog.ValidateDecor(decor, "probe"));
                Delete();
            }
        }

        [Test]
        public void RejectsANonIdentityRoot()
        {
            var decor = Save(root => root.transform.position = new Vector3(0, 1, 0));
            Assert.Throws<InvalidOperationException>(() => HiggsfieldMapCatalog.ValidateDecor(decor, "probe"));
        }
    }
}
#endif
