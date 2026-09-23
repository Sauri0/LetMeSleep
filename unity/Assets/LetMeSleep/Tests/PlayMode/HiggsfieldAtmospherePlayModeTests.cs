#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3.0 atmosphere contract (HiggsfieldAtmosphereCorrection): every installed map binds fog, its own
    /// complete VolumeProfile, a GradientSky whose horizon equals the fog color, a shadow budget that fits the
    /// URP additional-light atlas and GPU water fog; Unbind restores the scene. Role cameras render post.
    /// Visual quality is judged from captures (Validation/V030/Maps), not here.
    /// </summary>
    public sealed class HiggsfieldAtmospherePlayModeTests
    {
        private const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";
        private const string LightingRigPath = "Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_AlfaLightingRoot.prefab";
        private const string SkyShader = "LetMeSleep/Higgsfield/GradientSky";
        private const float PresetFarPlane = 100f;
        private readonly List<Object> created = new List<Object>();

        [UnityTest]
        public IEnumerator EveryMapBindsFogVolumeSkyAndBoundedShadowsThenRestores()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HiggsfieldMapCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            var rigRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingRigPath));
            created.Add(rigRoot);
            var rig = rigRoot.GetComponentInChildren<AlfaLightingRig>();
            Assert.That(rig, Is.Not.Null);
            yield return null;

            Assert.That(catalog.Entries.Count, Is.EqualTo(5));
            foreach (var entry in catalog.Entries)
            {
                var lighting = entry.Lighting;
                bool fogBefore = RenderSettings.fog;
                Material skyBefore = RenderSettings.skybox;
                var map = Object.Instantiate(entry.Prefab);
                created.Add(map.gameObject);
                yield return null;

                rig.BindHiggsfield(map.transform, catalog.ResolveLighting(entry.MapId, map));
                yield return null;

                Assert.That(lighting.FogEnabled, Is.True, entry.MapId + " fog enabled in catalog");
                Assert.That(RenderSettings.fog, Is.True, entry.MapId + " fog bound");
                Assert.That(RenderSettings.fogColor, Is.EqualTo(lighting.FogColor), entry.MapId + " fog color");
                float far = entry.CameraFarPlane > 0 ? entry.CameraFarPlane : PresetFarPlane;
                if (lighting.FogMode == FogMode.Linear)
                    Assert.That(lighting.FogEnd, Is.LessThanOrEqualTo(far), entry.MapId + " fog must hide the far-plane cut");

                Assert.That(RenderSettings.skybox, Is.SameAs(lighting.Skybox));
                Assert.That(lighting.Skybox.shader.name, Is.EqualTo(SkyShader), entry.MapId + " sky shader");
                Color horizon = lighting.Skybox.GetColor("_HorizonColor");
                Assert.That(Mathf.Abs(horizon.r - lighting.FogColor.r) + Mathf.Abs(horizon.g - lighting.FogColor.g) +
                    Mathf.Abs(horizon.b - lighting.FogColor.b), Is.LessThan(0.01f), entry.MapId + " sky horizon equals fog color");

                // Volume lives in the core RP assembly, which this test assembly does not reference.
                var volume = typeof(AlfaLightingRig).GetProperty("GlobalVolume").GetValue(rig) as Behaviour;
                Assert.That(volume, Is.Not.Null);
                object profile = volume.GetType().GetField("sharedProfile").GetValue(volume);
                object expected = typeof(HiggsfieldMapLighting.Configuration).GetField("VolumeProfile").GetValue(lighting);
                Assert.That(expected, Is.Not.Null, entry.MapId + " catalog profile");
                Assert.That(profile, Is.SameAs(expected), entry.MapId + " profile bound");
                Assert.That(volume.enabled, Is.True, entry.MapId + " volume enabled");
                var components = ((IEnumerable)profile.GetType().GetField("components").GetValue(profile)).Cast<Object>().ToArray();
                Assert.That(components.All(c => c != null), Is.True, entry.MapId + " profile has no missing overrides");
                CollectionAssert.IsSubsetOf(new[] { "Tonemapping", "Bloom", "ColorAdjustments", "Vignette" },
                    components.Select(c => c.GetType().Name).ToArray(), entry.MapId + " profile overrides");

                var locals = map.GetComponentsInChildren<Light>(true).Where(l => l.name == "Higgsfield_LocalLight").ToArray();
                Assert.That(locals.Length, Is.EqualTo(entry.LocalLights.Length), entry.MapId + " local lights");
                Assert.That(locals.Count(l => l.shadows != LightShadows.None),
                    Is.EqualTo(entry.LocalLights.Count(l => l.Settings.Shadows != LightShadows.None)));
                Assert.That(locals.Count(l => l.GetComponent<HiggsfieldLightFlicker>() != null),
                    Is.EqualTo(entry.LocalLights.Count(l => l.Settings.Flicker > 0)), entry.MapId + " flicker sources");
                Assert.That(entry.LocalLights.Sum(l => l.Settings.Shadows == LightShadows.None ? 0L
                        : 6L * TierResolution(l.Settings.ShadowResolutionTier) * TierResolution(l.Settings.ShadowResolutionTier)),
                    Is.LessThanOrEqualTo(2048L * 2048L), entry.MapId + " local shadow atlas budget");

                foreach (var water in map.GetComponentsInChildren<HiggsfieldGpuWater>(true).Where(w => w.IsBound))
                    Assert.That(water.CurrentParameters.UseFog, Is.EqualTo(lighting.FogEnabled), entry.MapId + " GPU water fog");

                rig.UnbindHiggsfield();
                Assert.That(RenderSettings.fog, Is.EqualTo(fogBefore), entry.MapId + " fog restored");
                Assert.That(RenderSettings.skybox, Is.SameAs(skyBefore), entry.MapId + " sky restored");
                Object.Destroy(map.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RoleCameraPolicyEnablesPostProcessingSmaaAndDithering()
        {
            var go = new GameObject("AtmosphereTestCamera");
            created.Add(go);
            var camera = go.AddComponent<Camera>();
            CameraPostProcessingPolicy.ApplyGameplay(camera);
            yield return null;
            var data = go.GetComponents<Component>().Single(c => c.GetType().Name == "UniversalAdditionalCameraData");
            Assert.That((bool)data.GetType().GetProperty("renderPostProcessing").GetValue(data), Is.True);
            Assert.That(data.GetType().GetProperty("antialiasing").GetValue(data).ToString(), Is.EqualTo("SubpixelMorphologicalAntiAliasing"));
            Assert.That((bool)data.GetType().GetProperty("dithering").GetValue(data), Is.True);
        }

        private static int TierResolution(int tier) => tier == 0 ? 256 : tier == 1 ? 512 : 1024;

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
