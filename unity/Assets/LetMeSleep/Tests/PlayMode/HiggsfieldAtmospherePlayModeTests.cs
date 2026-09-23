#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
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
    /// Round 3 adds lighthouse beams, water glints, lantern-pool rendering layers, character fill/split rim and the
    /// per-renderer indoor ambient probe, all restored on Unbind.
    /// Visual quality is judged from captures (Validation/V030/Maps), not here.
    /// </summary>
    public sealed class HiggsfieldAtmospherePlayModeTests
    {
        private const string CatalogPath = "Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset";
        private const string LightingRigPath = "Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_AlfaLightingRoot.prefab";
        private const string SkyShader = "LetMeSleep/Higgsfield/GradientSky";
        private const float PresetFarPlane = 100f;
        private const string LobbyPrefabPath = "Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab";
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

                // Round 2 art direction: halos, 3-layer flames, renderer overrides, night windows, rim light.
                var halos = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == HiggsfieldAtmosphereVisuals.HaloName).ToArray();
                var flames = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == HiggsfieldAtmosphereVisuals.FlameName).ToArray();
                Assert.That(halos.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.HaloSize > 0)), entry.MapId + " halos");
                Assert.That(flames.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.FlameHeight > 0)), entry.MapId + " flames");
                foreach (var flame in flames)
                {
                    Assert.That(flame.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(3), entry.MapId + " flame layers");
                    Assert.That(Vector3.Angle(flame.up, Vector3.up), Is.LessThan(0.5f), entry.MapId + " flames stand upright");
                }
                foreach (var visual in halos.Concat(flames))
                    Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, entry.MapId + " atmosphere visuals are not geometry");
                var overrides = entry.RendererOverrides.Select(o => new { Binding = o, Renderer = Find(map.transform, o.Path).GetComponent<Renderer>() }).ToArray();
                var colliderEnabled = overrides.Where(o => o.Renderer.GetComponent<Collider>()).ToDictionary(o => o.Renderer, o => o.Renderer.GetComponent<Collider>().enabled);
                foreach (var item in overrides)
                {
                    if (item.Binding.Hide) Assert.That(item.Renderer.enabled, Is.False, item.Binding.Path + " hidden");
                    if (item.Binding.CastShadowsOff) Assert.That(item.Renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), item.Binding.Path);
                    if (item.Binding.SwapTo) CollectionAssert.Contains(item.Renderer.sharedMaterials, item.Binding.SwapTo, item.Binding.Path + " swapped");
                    if (item.Binding.IgnoreLocalLights) Assert.That(item.Renderer.renderingLayerMask & 1u, Is.EqualTo(0u), item.Binding.Path + " moon-only layer");
                }
                foreach (var pair in colliderEnabled) Assert.That(pair.Key.GetComponent<Collider>().enabled, Is.EqualTo(pair.Value), "colliders untouched");
                // Round 4 (director corrections): several swaps per renderer, small shelter lanterns, shadow normal bias.
                foreach (var item in overrides)
                    foreach (var extra in item.Binding.ExtraSwaps ?? Array.Empty<HiggsfieldMapLighting.MaterialSwap>())
                    {
                        CollectionAssert.Contains(item.Renderer.sharedMaterials, extra.To, item.Binding.Path + " extra swap");
                        CollectionAssert.DoesNotContain(item.Renderer.sharedMaterials, extra.From, item.Binding.Path + " extra swap source gone");
                    }
                var lanternProps = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Higgsfield_Lantern").ToArray();
                Assert.That(lanternProps.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.LanternSize > 0)), entry.MapId + " lantern props");
                foreach (var prop in lanternProps)
                    Assert.That(prop.GetComponentsInChildren<Collider>(true), Is.Empty, entry.MapId + " lantern props are not geometry");
                Assert.That(locals.Count(l => Mathf.Approximately(l.shadowNormalBias, 1f)),
                    Is.EqualTo(entry.LocalLights.Count(l => Mathf.Approximately(l.Settings.ShadowNormalBias, 1f))), entry.MapId + " shadow normal bias");
                foreach (var swap in lighting.MaterialSwaps)
                    Assert.That(map.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Contains(swap.From)), Is.False, entry.MapId + " swapped " + swap.From.name);
                Assert.That(Shader.GetGlobalFloat("_LMS_InteriorCount"), Is.EqualTo(lighting.InteriorVolumes.Length), entry.MapId + " interiors");
                var rim = rig.GetComponentInChildren<HiggsfieldRimLight>();
                Assert.That(rim != null, Is.EqualTo(lighting.CharacterRimIntensity > 0 || lighting.CharacterFillIntensity > 0),
                    entry.MapId + " rim/fill light");
                if (rim)
                {
                    Assert.That(rim.Fill != null, Is.EqualTo(lighting.CharacterFillIntensity > 0), entry.MapId + " character fill");
                    Assert.That(rim.SecondRim != null, Is.EqualTo(lighting.CharacterRimSpread > 0), entry.MapId + " split rim");
                }

                // Round 3: explicit sky fill replaces stray scene directional lights (Forward+ ignores culling masks).
                var binding = rig.GetComponent<HiggsfieldMapLighting>();
                Assert.That(binding.SkyFill != null, Is.EqualTo(lighting.SkyFillIntensity > 0), entry.MapId + " sky fill");
                Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.enabled && l.type == LightType.Directional &&
                    l != binding.SkyFill && !l.GetComponentInParent<HiggsfieldRimLight>()), Is.EqualTo(1), entry.MapId + " only the moon/sun");

                // Round 3: lighthouse beams, water glints, lantern-pool layers and the indoor ambient probe.
                var beams = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == HiggsfieldAtmosphereVisuals.BeamName).ToArray();
                var glints = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == HiggsfieldAtmosphereVisuals.GlintName).ToArray();
                Assert.That(beams.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.BeamLength > 0)), entry.MapId + " beams");
                Assert.That(glints.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.ReflectionLength > 0)), entry.MapId + " glints");
                var pools = map.GetComponentsInChildren<Transform>(true).Where(x => x.name == HiggsfieldAtmosphereVisuals.PoolName).ToArray();
                Assert.That(pools.Length, Is.EqualTo(entry.LocalLights.Count(l => l.Settings.PoolRadius > 0)), entry.MapId + " ground pools");
                foreach (var visual in beams.Concat(glints).Concat(pools))
                    Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, entry.MapId + " beams/glints are not geometry");
                foreach (var item in overrides.Where(o => o.Binding.AddLightLayers != 0))
                    Assert.That(item.Renderer.renderingLayerMask & (uint)item.Binding.AddLightLayers, Is.EqualTo((uint)item.Binding.AddLightLayers),
                        item.Binding.Path + " extra light layers");
                int probes = map.GetComponentsInChildren<Renderer>(true).Count(r => r.lightProbeUsage == UnityEngine.Rendering.LightProbeUsage.CustomProvided);
                if (lighting.InteriorAmbientIntensity > 0) Assert.That(probes, Is.GreaterThan(0), entry.MapId + " indoor ambient probes");
                else Assert.That(probes, Is.Zero, entry.MapId + " no indoor ambient");

                rig.UnbindHiggsfield();
                yield return null;
                Assert.That(map.GetComponentsInChildren<Transform>(true).Count(x => x.name == HiggsfieldAtmosphereVisuals.HaloName ||
                    x.name == HiggsfieldAtmosphereVisuals.FlameName || x.name == HiggsfieldAtmosphereVisuals.BeamName ||
                    x.name == HiggsfieldAtmosphereVisuals.GlintName || x.name == HiggsfieldAtmosphereVisuals.PoolName ||
                    x.name == "Higgsfield_Lantern"), Is.Zero,
                    entry.MapId + " visuals removed");
                Assert.That(map.GetComponentsInChildren<Renderer>(true).Count(r => r.lightProbeUsage == UnityEngine.Rendering.LightProbeUsage.CustomProvided ||
                    r.HasPropertyBlock()), Is.Zero, entry.MapId + " probes and property blocks restored");
                var pristine = entry.Prefab.transform;
                foreach (var item in overrides)
                {
                    var source = Find(pristine, item.Binding.Path).GetComponent<Renderer>();
                    Assert.That(item.Renderer.enabled, Is.EqualTo(source.enabled), item.Binding.Path + " restored");
                    Assert.That(item.Renderer.shadowCastingMode, Is.EqualTo(source.shadowCastingMode), item.Binding.Path + " shadows restored");
                    CollectionAssert.AreEqual(source.sharedMaterials, item.Renderer.sharedMaterials, item.Binding.Path + " materials restored");
                    Assert.That(item.Renderer.renderingLayerMask, Is.EqualTo(source.renderingLayerMask), item.Binding.Path + " layers restored");
                }
                Assert.That(Shader.GetGlobalFloat("_LMS_InteriorCount"), Is.Zero, entry.MapId + " interiors cleared");
                Assert.That(rig.GetComponentInChildren<HiggsfieldRimLight>(), Is.Null, entry.MapId + " rim removed");
                Assert.That(RenderSettings.fog, Is.EqualTo(fogBefore), entry.MapId + " fog restored");
                Assert.That(RenderSettings.skybox, Is.SameAs(skyBefore), entry.MapId + " sky restored");
                Object.Destroy(map.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LobbyGetsHalosNightWindowAndGarlandsWithoutColliders()
        {
            var rigRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingRigPath));
            created.Add(rigRoot);
            var rig = rigRoot.GetComponentInChildren<AlfaLightingRig>();
            Assert.That(rig.AtmosphereKit, Is.Not.Null, "lighting rig prefab references the atmosphere kit");
            Assert.That(rig.AtmosphereKit.IsComplete, Is.True);
            var lobby = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath)).GetComponent<EnvironmentMapDefinition>();
            created.Add(lobby.gameObject);
            int collidersBefore = lobby.GetComponentsInChildren<Collider>(true).Length;
            yield return null;

            rig.BindMap(lobby.PresentationAnchors, false);
            yield return null;
            int lanterns = lobby.PresentationAnchors.GetComponentsInChildren<Transform>(true).Count(x => x.name.StartsWith("LightAnchor_Lobby_Lantern"));
            Assert.That(lanterns, Is.GreaterThan(0));
            Assert.That(lobby.GetComponentsInChildren<Transform>(true).Count(x => x.name == HiggsfieldAtmosphereVisuals.HaloName), Is.EqualTo(lanterns), "sconce halos");
            Assert.That(lobby.GetComponentsInChildren<Transform>(true).Count(x => x.name == HiggsfieldAtmosphereVisuals.StringLightsName), Is.GreaterThan(0), "garlands");
            var canvas = lobby.transform.Find("Furnishings/Lobby_Domestic/Menu_FramedNightLake/Canvas").GetComponent<Renderer>();
            Assert.That(canvas.sharedMaterial, Is.SameAs(rig.AtmosphereKit.MenuWindowMaterial), "night window with moon");
            Assert.That(lobby.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(collidersBefore), "no colliders added to the lobby");

            rig.BindMap(lobby.PresentationAnchors, false); // Rebinding replaces, never duplicates.
            yield return null;
            Assert.That(lobby.GetComponentsInChildren<Transform>(true).Count(x => x.name == HiggsfieldAtmosphereVisuals.HaloName), Is.EqualTo(lanterns), "halos not duplicated");
        }

        private static Transform Find(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform next = null;
                for (int i = 0; i < current.childCount; i++) if (current.GetChild(i).name == segment) next = current.GetChild(i);
                Assert.That(next, Is.Not.Null, "Missing path " + path);
                current = next;
            }
            return current;
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
