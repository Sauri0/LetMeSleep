#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class CustomizationPersistencePlayModeTests
    {
        [Serializable]
        private sealed class PreferenceFixture
        {
            public int schema = 1;
            public BasicCustomizationDraft appearance;
            public BasicCustomizationDraft localAppearanceDraft;
        }

        [Serializable]
        private sealed class PreferenceV2Fixture
        {
            public int schema = 2;
            public string playerName = "Prueba";
            public AlfaSettingsDraft settings;
            public AppearanceSelection publishedAppearance;
            public AppearanceSelection localAppearanceDraft;
            public int resolutionWidth;
            public int resolutionHeight;
        }

        private const string BootScene = "LetMeSleepHiggsfield";
        private string dataPath;
        private AlfaApplication application;
        private AlfaUiController ui;

        [UnitySetUp]
        public IEnumerator LoadIsolatedBootScene()
        {
            dataPath = RequireIsolatedDataPath();
            ResetFixtureDirectory();
            WritePreferences(Published(), LocalDraft());
            yield return ReloadBootScene();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (application != null) application.CancelTraining();
            yield return null;
            if (!string.IsNullOrEmpty(dataPath)) ResetFixtureDirectory();
        }

        [UnityTest]
        public IEnumerator PrivateDraftRestoresPreviewsAndPublishesOnlyOnApply()
        {
            BasicCustomizationDraft published = Published();
            BasicCustomizationDraft local = LocalDraft();
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), published, "published after boot");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), local, "local draft after boot");
            AssertDraft(Field<BasicCustomizationDraft>(application, "previewAppearance"), local, "preview after boot");
            AssertDraft(Field<CustomizationUiState>(ui, "customizationState").Saved, published, "UI published state after boot");
            AssertDraft(Field<CustomizationUiState>(ui, "customizationState").Draft, local, "UI local state after boot");

            ui.ShowCustomization(); // Captures the original local draft as this session's undo baseline.
            BasicCustomizationDraft edited = new BasicCustomizationDraft(AlfaRole.Mosquito, "light", "yellow", "purple");
            application.PreviewCustomization(edited);
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), published, "preview must not publish");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), edited, "preview must save locally");
            AssertDraft(Field<BasicCustomizationDraft>(application, "previewAppearance"), edited, "preview must match local save");
            PreferenceFixture afterPreview = ReadPreferences();
            AssertDraft(afterPreview.appearance, published, "file published after preview");
            AssertDraft(afterPreview.localAppearanceDraft, edited, "file local after preview");

            application.SaveCustomization(edited);
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), edited, "apply publishes the local draft");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), edited, "apply retains the local draft");

            Invoke(ui, "ResetCustomization");
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), edited, "undo after apply stays private");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), local, "undo restores entry baseline");

            ForceWriteFailure();
            BasicCustomizationDraft rejected = new BasicCustomizationDraft(AlfaRole.Human, "tan", "green", "blue");
            application.PreviewCustomization(rejected);
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), edited, "write failure must not publish");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), local, "write failure must roll back local draft");
            Assert.That(Field<CustomizationUiState>(ui, "customizationState").Message, Does.Contain("No se pudo guardar"));
            Assert.That(Field<CustomizationUiState>(ui, "customizationState").Message, Does.Not.Contain("Guardado localmente"));
            AssertNoTemporaryFiles();
            Directory.Delete(BackupPath, true);

            ForceWriteFailure();
            application.SaveCustomization(rejected);
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), edited, "apply write failure must keep published appearance");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), local, "apply write failure must keep local draft");
            AssertDraft(Field<BasicCustomizationDraft>(application, "previewAppearance"), local, "apply write failure must keep preview");
            Assert.That(Field<CustomizationUiState>(ui, "customizationState").Message, Does.Contain("No se pudo guardar"));
            Assert.That(Field<CustomizationUiState>(ui, "customizationState").Message, Does.Not.Contain("Apariencia aplicada"));
            AssertNoTemporaryFiles();
            Directory.Delete(BackupPath, true);

            // Simulate a fresh application process on the same isolated preference directory.
            yield return ReloadBootScene();
            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), edited, "published after restart");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), local, "local draft after restart");
            AssertDraft(Field<BasicCustomizationDraft>(application, "previewAppearance"), local, "restored preview after restart");

            VerifyCosmeticSavePreservesLaunchVideoValues(local);

            AssertLobbyCustomizationStaysOpenForSnapshotsAndReturnsToLobby();
            ui.ShowCustomization();
            ui.ShowGameplay(false); // A round transition remains authoritative over a customization screen.
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Gameplay));
            ui.ShowCustomization();
            application.LeaveRoom(); // A closed room still returns to a non-orphaned main menu.
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.MainMenu));
        }

        [UnityTest]
        public IEnumerator UnsupportedSchemaBlocksCustomizationWriteWithoutChangingTheFile()
        {
            ResetFixtureDirectory();
            string future = "{\"schema\":3,\"appearance\":{\"Role\":0,\"SkinColorId\":\"warm\",\"PajamaColorId\":\"blue\",\"MosquitoColorId\":\"red\"}}";
            File.WriteAllText(PreferencePath, future);
            byte[] before = File.ReadAllBytes(PreferencePath);
            yield return ReloadBootScene();

            application.PreviewCustomization(LocalDraft());

            Assert.That(File.ReadAllBytes(PreferencePath), Is.EqualTo(before));
            var state = Field<CustomizationUiState>(ui, "customizationState");
            Assert.That(state.Message, Does.Contain("otra versión"));
            Assert.That(state.IsReadOnly, Is.True);
            Assert.That(state.IsSaving, Is.False);
            AssertNoTemporaryFiles();
        }

        [UnityTest]
        public IEnumerator SchemaTwoWithoutRuntimePreservesTheExactFileAndDisablesBasicEditing()
        {
            ResetFixtureDirectory();
            Directory.CreateDirectory(dataPath);
            var published = new AppearanceSelection(new AppearanceLoadout(new[]
            {
                new AppearanceSlotSelection("human.future", "published")
            }), new AppearanceLoadout());
            var local = new AppearanceSelection(new AppearanceLoadout(new[]
            {
                new AppearanceSlotSelection("human.future", "private")
            }), new AppearanceLoadout());
            var fixture = new PreferenceV2Fixture
            {
                settings = new AlfaSettingsDraft { MasterVolume = .7f, MusicVolume = .4f, EffectsVolume = .8f,
                    VoiceVolume = .6f, PushToTalkBinding = "<Keyboard>/v", FullScreen = false,
                    HumanSensitivity = 1f, MosquitoSensitivity = 1f },
                publishedAppearance = published,
                localAppearanceDraft = local,
                resolutionWidth = 1280,
                resolutionHeight = 720
            };
            string json = JsonUtility.ToJson(fixture, true);
            Assert.That(PreferenceSchemaCodec.IsExactV2RoundTrip(json), Is.True, "The fixture must be a known schema-2 document.");
            File.WriteAllText(PreferencePath, json);
            byte[] before = File.ReadAllBytes(PreferencePath);
            yield return ReloadBootScene();

            application.PreviewCustomization(LocalDraft());
            var changedSettings = Field<AlfaSettingsDraft>(application, "settings").Copy();
            changedSettings.MasterVolume = .2f;
            application.ApplySettings(changedSettings);

            Assert.That(File.ReadAllBytes(PreferencePath), Is.EqualTo(before));
            var state = Field<CustomizationUiState>(ui, "customizationState");
            Assert.That(state.Mode, Is.EqualTo(CustomizationUiMode.Basic));
            Assert.That(state.IsReadOnly, Is.True, "The fallback controls must remain disabled for retained V2 data.");
            Assert.That(state.IsSaving, Is.False, "A permanent data gate is not an operation in progress.");
            Assert.That(state.Message, Does.Contain("modular"));
            ui.ShowCustomization();
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Customization));
            Invoke(ui, "CloseCustomization");
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.MainMenu), "Read-only customization must allow returning to the menu.");
            AssertNoTemporaryFiles();
        }

        [UnityTest]
        public IEnumerator LegacySchemaOneMigratesMissingPrivateDraftFromPublishedAppearance()
        {
            ResetFixtureDirectory();
            BasicCustomizationDraft published = Published();
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(PreferencePath, JsonUtility.ToJson(new PreferenceFixture { appearance = published }, true));
            yield return ReloadBootScene();

            AssertDraft(Field<BasicCustomizationDraft>(application, "appearance"), published, "legacy published appearance");
            AssertDraft(Field<BasicCustomizationDraft>(application, "localAppearanceDraft"), published, "legacy local migration");
            AssertDraft(Field<BasicCustomizationDraft>(application, "previewAppearance"), published, "legacy preview migration");
            PreferenceFixture migrated = ReadPreferences();
            Assert.That(migrated.schema, Is.EqualTo(1));
            AssertDraft(migrated.appearance, published, "migrated published appearance");
            AssertDraft(migrated.localAppearanceDraft, published, "migrated private draft");
        }

        private void AssertLobbyCustomizationStaysOpenForSnapshotsAndReturnsToLobby()
        {
            var initial = new LobbyUiState(true, "ABCD123456", new[] { new LobbyMemberUiState("local", "Prueba", false, true) },
                false, false, null, false, "Falta alguien.", canExplore: true, isWaiting: true);
            ui.PresentLobby(initial);
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Lobby));
            Canvas.ForceUpdateCanvases();
            AssertLobbyActionLabelsFitCurrentCanvas();
            CaptureLobbyActionsIfRequested();
            ui.ShowCustomization();
            ui.ShowCustomization(); // A redundant request must keep the existing lobby origin.
            ui.PresentLobby(new LobbyUiState(true, "ABCD123456", new[] { new LobbyMemberUiState("local", "Prueba", true, true) },
                true, false, null, false, "Falta alguien.", isWaiting: true));
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Customization), "A waiting-room snapshot must not eject local editing.");
            Invoke(ui, "CloseCustomization");
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Lobby));

            ui.ShowCustomization();
            ui.PresentLobby(new LobbyUiState(true, "WXYZ987654", new[] { new LobbyMemberUiState("local", "Prueba", true, true) },
                true, false, null, false, "Falta alguien.", isWaiting: true));
            Assert.That(ui.CurrentScreen, Is.EqualTo(AlfaUiScreen.Lobby), "A different room snapshot must never retain the prior room's customization context.");
        }

        private void AssertLobbyActionLabelsFitCurrentCanvas()
        {
            foreach (string name in new[] { "LobbyExploreButton", "LobbyCustomizeButton" })
            {
                var button = Array.Find(ui.GetComponentsInChildren<UnityEngine.UI.Button>(true), item => item.name == name);
                Assert.That(button, Is.Not.Null, name);
                Assert.That(button.gameObject.activeInHierarchy, Is.True, name + " must be visible before its layout is checked.");
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                Assert.That(label, Is.Not.Null, name + " label");
                label.ForceMeshUpdate(false, false);
                Assert.That(label.isTextOverflowing, Is.False, name + " label overflows in the active canvas.");
            }
        }

        private void CaptureLobbyActionsIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-customizationSaveEvidence");
            if (option < 0) return;
            Assert.That(option + 1, Is.LessThan(args.Length), "-customizationSaveEvidence requires an output directory.");
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(UnderValidationRoot(output), Is.True, "Evidence must stay under Validation/V020 or Validation/V030.");
            Directory.CreateDirectory(output);

            var camera = new GameObject("CustomizationSaveEvidenceCamera", typeof(Camera)).GetComponent<Camera>();
            var canvas = ui.GetComponent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            var receipt = new StringBuilder("PASS Unity " + Application.unityVersion + "\n");
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.015f, .025f, .05f, 1f);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                CaptureLobbyCanvas(output, camera, 1280, 720, receipt);
                CaptureLobbyCanvas(output, camera, 1920, 1080, receipt);
                receipt.Append("Lobby action row only; local Canvas evidence, not a room, peer or WAN test.\n");
                File.WriteAllText(Path.Combine(output, "customization-save-lobby-layout.txt"), receipt.ToString());
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private void CaptureLobbyCanvas(string output, Camera camera, int width, int height, StringBuilder receipt)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                AssertLobbyActionLabelsFitCurrentCanvas();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, "customization-save-lobby-" + width + "x" + height + ".png"), pixels.EncodeToPNG());
                int visiblePixels = 0;
                foreach (Color32 color in pixels.GetPixels32()) if (color.r > 80 || color.g > 80 || color.b > 80) visiblePixels++;
                Assert.That(visiblePixels, Is.GreaterThan(width * height / 250), "Lobby action row did not render enough visible pixels.");
                receipt.AppendLine(width + "x" + height + ": visible=" + visiblePixels + " canvas=" + ui.GetComponent<Canvas>().pixelRect);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// v0.3.0: the build scene carries the production modular provider, so a schema-1 file migrates to schema 2 on
        /// boot. These tests cover the basic mode that stays the fallback when no modular runtime resolves; the scene's
        /// provider is detached after Awake/OnEnable and before AlfaApplication.Start (sceneLoaded runs in between).
        /// </summary>
        private static void DetachSceneModularProvider(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var app in root.GetComponentsInChildren<AlfaApplication>(true)) app.ModularCustomizationProvider = null;
        }

        private IEnumerator ReloadBootScene()
        {
            SceneManager.sceneLoaded += DetachSceneModularProvider;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, $"{BootScene} must be present and enabled in Build Settings.");
                while (!load.isDone) yield return null;
            }
            finally { SceneManager.sceneLoaded -= DetachSceneModularProvider; }
            yield return null;
            application = Object.FindFirstObjectByType<AlfaApplication>();
            Assert.That(application, Is.Not.Null, "The boot scene must start AlfaApplication.");
            ui = Field<AlfaUiController>(application, "ui");
            Assert.That(ui, Is.Not.Null, "AlfaApplication must create the menu UI.");
        }

        private void WritePreferences(BasicCustomizationDraft appearance, BasicCustomizationDraft localDraft)
        {
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(PreferencePath, JsonUtility.ToJson(new PreferenceFixture
            {
                appearance = appearance,
                localAppearanceDraft = localDraft
            }, true));
        }

        private PreferenceFixture ReadPreferences() => JsonUtility.FromJson<PreferenceFixture>(File.ReadAllText(PreferencePath));

        private void ForceWriteFailure()
        {
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            Directory.CreateDirectory(BackupPath);
        }

        private void VerifyCosmeticSavePreservesLaunchVideoValues(BasicCustomizationDraft draft)
        {
            var storedVideo = Field<AlfaSettingsDraft>(application, "settings").Copy();
            SetField(application, "preserveLaunchVideoChoice", true);
            SetField(application, "launchSavedVideoSettings", storedVideo);
            SetField(application, "launchSavedWidth", 1234);
            SetField(application, "launchSavedHeight", 567);
            application.PreviewCustomization(draft);
            string json = File.ReadAllText(PreferencePath);
            Assert.That(json, Does.Contain("\"resolutionWidth\": 1234"));
            Assert.That(json, Does.Contain("\"resolutionHeight\": 567"));
        }

        private void ResetFixtureDirectory()
        {
            if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true);
            Directory.CreateDirectory(dataPath);
        }

        private void AssertNoTemporaryFiles()
            => Assert.That(Directory.GetFiles(dataPath, "preferences.json.tmp-*"), Is.Empty);

        private string PreferencePath => Path.Combine(dataPath, "preferences.json");
        private string BackupPath => PreferencePath + ".backup";

        private static BasicCustomizationDraft Published() => new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
        private static BasicCustomizationDraft LocalDraft() => new BasicCustomizationDraft(AlfaRole.Mosquito, "dark", "purple", "green");

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Invoke(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void AssertDraft(BasicCustomizationDraft actual, BasicCustomizationDraft expected, string label)
        {
            Assert.That(actual, Is.Not.Null, label);
            Assert.That(actual.SameValues(expected), Is.True, label);
        }

        private static bool UnderValidationRoot(string path) => new[] { "N:/LetMeSleep/Validation/V020", "N:/LetMeSleep/Validation/V030" }
            .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));

        private static string RequireIsolatedDataPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--lms-validation-data");
            if (option < 0 || option + 1 >= args.Length)
                Assert.Ignore("Requires --lms-validation-data under N:/LetMeSleep/Validation/V020 or V030.");
            string path = Path.GetFullPath(args[option + 1]);
            Assert.That(UnderValidationRoot(path), Is.True, "Use an isolated V020 or V030 validation directory.");
            Assert.That(Path.GetFileName(path).StartsWith("CustomizationPersistencePlayModeTests-", StringComparison.Ordinal), Is.True,
                "The isolated directory name must make cleanup explicit.");
            return path;
        }
    }
}
#endif
