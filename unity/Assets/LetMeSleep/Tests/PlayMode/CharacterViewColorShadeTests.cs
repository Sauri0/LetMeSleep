using LetMeSleep.Content.Characters;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class CharacterViewColorShadeTests
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Test]
        public void ShadedFacetsKeepTheirContrastAndOtherChannelsStayUntouched()
        {
            var root = new GameObject("Colour shade fixture");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material, material, material };
                var view = root.AddComponent<CharacterView>();
                view.Colors = new[]
                {
                    new CharacterView.ColorBinding { Renderer = renderer, MaterialIndex = 0, Category = "Mosquito" },
                    new CharacterView.ColorBinding { Renderer = renderer, MaterialIndex = 1, Category = "Mosquito", Shade = .25f },
                    new CharacterView.ColorBinding { Renderer = renderer, MaterialIndex = 2, Category = "Pajamas", Shade = .5f }
                };
                var blue = new Color(.2f, .4f, .8f, 1f);
                view.SetMosquitoColor(blue);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                AssertColor(block.GetColor(BaseColor), blue);
                renderer.GetPropertyBlock(block, 1);
                AssertColor(block.GetColor(BaseColor), new Color(.15f, .3f, .6f, 1f));
                renderer.GetPropertyBlock(block, 2);
                Assert.That(block.isEmpty, Is.True, "A different customization channel must not be tinted.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-5f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-5f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-5f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-5f));
        }
    }
}
