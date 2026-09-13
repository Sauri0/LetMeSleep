#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Environment;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public class HiggsfieldMapLoadingPlayModeTests
    {
        [Serializable] class Config { public string scene, output; public string[] expectedMapIds; }
        AlfaApplication app;
        [UnityTest] public IEnumerator FiveCatalogMapsLoadForBothRolesAndReturnToMenu()
        {
            var args = System.Environment.GetCommandLineArgs(); int arg = Array.IndexOf(args, "-higgsfieldGameReview");
            if (arg < 0) Assert.Ignore("Requires explicitly installed five-map bootstrap scene.");
            Assert.That(Array.IndexOf(args, "--lms-validation-data"), Is.GreaterThanOrEqualTo(0), "Use isolated validation preferences.");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(args[arg + 1]));
            Assert.That(config.expectedMapIds.Distinct().Count(), Is.EqualTo(5));
            EditorSceneManager.LoadSceneInPlayMode(config.scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<AlfaApplication>(); Assert.That(app, Is.Not.Null);
            Assert.That(app.HiggsfieldMaps, Is.Not.Null);
            CollectionAssert.AreEquivalent(config.expectedMapIds, app.HiggsfieldMaps.Entries.Select(e => e.MapId).ToArray());
            var ui = Field<AlfaUiController>("ui"); Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.MainMenu));
            var report = new System.Text.StringBuilder();
            foreach (string id in config.expectedMapIds)
            foreach (var role in new[] { AlfaRole.Human, AlfaRole.Mosquito })
            {
                app.StartTraining(role, AlfaUiController.BloodModeId, id);
                var runtime = Field<GameplayRuntime>("game"); Assert.That(runtime, Is.Not.Null, id);
                runtime.CaptureLocalInput = false;
                ui.ShowGameplay(true);
                yield return new WaitForSecondsRealtime(1.2f);
                var map = Field<EnvironmentMapDefinition>("map");
                Assert.That(map.MapId, Is.EqualTo(id));
                Assert.That(map.ContentHash, Is.EqualTo(app.HiggsfieldMaps.Resolve(id).Prefab.ContentHash));
                Assert.That(runtime.World.MapRoot, Is.EqualTo(map.transform));
                Assert.That(runtime.NavigationData, Is.EqualTo(map.SpatialData));
                Assert.That(runtime.LatestSnapshot, Is.Not.Null);
                Assert.That(runtime.LatestSnapshot.Actors.Count, Is.EqualTo(3));
                Assert.That(runtime.LatestSnapshot.Actors.All(a => a.Position.IsFinite), Is.True);
                Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Gameplay));
                Directory.CreateDirectory(config.output);
                CaptureGameplayCamera(Path.Combine(config.output, id + "-" + role + ".png"));
                app.CancelTraining(); yield return null; yield return null;
                Assert.That(Field<GameplayRuntime>("game"), Is.Null);
                Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.MainMenu));
                Assert.That(Object.FindObjectsByType<GameplayRuntime>(FindObjectsSortMode.None).Length, Is.EqualTo(0));
                report.AppendLine(id + " / " + role + ": correct identity, runtime, navigation, three finite actors and return to menu PASS.");
            }
            Directory.CreateDirectory(config.output);
            File.WriteAllText(Path.Combine(config.output, "five-map-game-loading.txt"), "PASS Unity " + Application.unityVersion + "\n" + report + "Ten local training sessions via application API; no WAN, full playthrough, role-capacity or performance claim.");
        }
        T Field<T>(string name) => (T)typeof(AlfaApplication).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        void CaptureGameplayCamera(string path)
        {
            var cameras = Field<GameObject>("presentation").GetComponentsInChildren<Camera>()
                .Where(camera => camera.enabled && camera.gameObject.activeInHierarchy).ToArray();
            Assert.That(cameras.Length, Is.EqualTo(1), "Exactly one active gameplay camera expected.");
            var camera = cameras[0]; var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active; float previousAspect = camera.aspect;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 1280f / 720f; camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.aspect = previousAspect;
                RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target); Object.Destroy(pixels);
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (app) app.CancelTraining();
            yield return null;
        }
    }
}
#endif
