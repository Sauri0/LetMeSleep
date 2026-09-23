#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class ModularCustomizationPersistenceIntegrationTests
    {
        [Serializable]
        private sealed class PreferencesV1Fixture
        {
            public int schema = 1;
            public string playerName = "Prueba";
            public AlfaSettingsDraft settings = Settings();
            public BasicCustomizationDraft appearance;
            public BasicCustomizationDraft localAppearanceDraft;
            public int resolutionWidth = 1280;
            public int resolutionHeight = 720;
        }

        [Serializable]
        private sealed class PreferencesV2Fixture
        {
            public int schema;
            public AppearanceSelection publishedAppearance;
            public AppearanceSelection localAppearanceDraft;
        }

        private const string SceneName = "LetMeSleepHiggsfield";
        private readonly List<Object> owned = new List<Object>();
        private readonly Dictionary<string, Color32> colors = new Dictionary<string, Color32>(StringComparer.Ordinal);
        private string dataPath;
        private AlfaApplication application;
        private AlfaUiController ui;
        private CharacterPreviewOrbit orbit;
        private ModularCustomizationRuntimeProvider provider;
        private ModularCustomizationRuntime runtime;
        private GameObject humanPrefab;
        private GameObject mosquitoPrefab;
        private Material material;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dataPath = RequireIsolatedDataPath();
            ResetFixtureDirectory();
            WriteV1(PublishedLegacy(), PrivateLegacy());

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, SceneName + " must be enabled in Build Settings.");
            while (!load.isDone) yield return null;
            yield return null;

            application = Object.FindFirstObjectByType<AlfaApplication>();
            Assert.That(application, Is.Not.Null);
            ui = Field<AlfaUiController>(application, "ui");
            Assert.That(ui, Is.Not.Null);

            BuildRuntimeFixture();
            application.HumanPrefab = humanPrefab;
            application.MosquitoPrefab = mosquitoPrefab;
            application.ModularCustomizationProvider = provider;
            Invoke(application, "LoadPreferences");
            runtime = Field<ModularCustomizationRuntime>(application, "modularCustomizationRuntime");
            Assert.That(runtime, Is.Not.Null, "A complete synthetic catalog and both real application prefabs must open the gate.");

            orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true);
            Assert.That(orbit, Is.Not.Null);
            orbit.Bind(new CharacterPreviewSetup(application.PreviewCamera, application.PreviewStage,
                application.PreviewTexture, humanPrefab, mosquitoPrefab));
            Invoke(application, "PresentPreferences");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (application != null) application.CancelTraining();
            if (orbit != null) orbit.Bind(null);
            yield return null;
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null) Object.DestroyImmediate(owned[index]);
            owned.Clear();
            if (!string.IsNullOrEmpty(dataPath)) ResetFixtureDirectory();
        }

        [UnityTest]
        public IEnumerator MigrationPreviewAndApplyKeepPublishedAndPrivateSelectionsIndependent()
        {
            Assert.That(Field<int>(application, "loadedPreferenceSchema"), Is.EqualTo(2));
            Assert.That(PreferenceSchemaCodec.IsExactV2RoundTrip(File.ReadAllText(PreferencePath)), Is.True);
            Assert.That(runtime.TryMigrate(PublishedLegacy(), out var migratedPublished, out var publishedError), Is.True, publishedError);
            Assert.That(runtime.TryMigrate(PrivateLegacy(), out var migratedPrivate, out var privateError), Is.True, privateError);
            AssertSelection(Field<AppearanceSelection>(application, "publishedModularAppearance"), migratedPublished, "published migration");
            AssertSelection(Field<AppearanceSelection>(application, "localModularAppearanceDraft"), migratedPrivate, "private migration");

            var diskAfterMigration = ReadV2();
            Assert.That(diskAfterMigration.schema, Is.EqualTo(2));
            AssertSelection(diskAfterMigration.publishedAppearance, migratedPublished, "disk published migration");
            AssertSelection(diskAfterMigration.localAppearanceDraft, migratedPrivate, "disk private migration");
            Assert.That(TryBuildAppearancePacket("ABCD1234", out var packetBeforePreview), Is.True);
            Assert.That(AppearanceWireCodec.TryDecode(packetBeforePreview, "ABCD1234", runtime.Snapshot,
                out var networkBeforePreview), Is.True);
            AssertSelection(networkBeforePreview, migratedPublished, "network published migration");

            ui.ShowCustomization();
            orbit.Show(AlfaRole.Human);
            var previewDraft = migratedPrivate.Copy();
            previewDraft.Human.SetOption("human.skin", "skin-light");
            SetField(application, "appearanceAt", 41d);
            application.PreviewModularCustomization(previewDraft, AlfaRole.Human);

            Assert.That(Field<double>(application, "appearanceAt"), Is.EqualTo(41d), "Preview must not schedule publication.");
            AssertSelection(Field<AppearanceSelection>(application, "publishedModularAppearance"), migratedPublished, "preview published memory");
            AssertSelection(Field<AppearanceSelection>(application, "localModularAppearanceDraft"), previewDraft, "preview private memory");
            var diskAfterPreview = ReadV2();
            AssertSelection(diskAfterPreview.publishedAppearance, migratedPublished, "preview published disk");
            AssertSelection(diskAfterPreview.localAppearanceDraft, previewDraft, "preview private disk");
            AssertPreviewColor(orbit.CurrentInstance, "human.skin", colors["skin-light"]);
            Assert.That(TryBuildAppearancePacket("ABCD1234", out var packetAfterPreview), Is.True);
            Assert.That(packetAfterPreview, Is.EqualTo(packetBeforePreview), "Private preview must not change the wire payload.");

            var applied = previewDraft.Copy();
            applied.Human.SetOption("human.pajama", "pajama-red");
            SetField(application, "appearanceAt", 73d);
            application.SaveModularCustomization(applied, AlfaRole.Human);

            Assert.That(Field<double>(application, "appearanceAt"), Is.Zero, "Only successful Apply schedules publication.");
            AssertSelection(Field<AppearanceSelection>(application, "publishedModularAppearance"), applied, "apply published memory");
            AssertSelection(Field<AppearanceSelection>(application, "localModularAppearanceDraft"), applied, "apply private memory");
            var diskAfterApply = ReadV2();
            AssertSelection(diskAfterApply.publishedAppearance, applied, "apply published disk");
            AssertSelection(diskAfterApply.localAppearanceDraft, applied, "apply private disk");
            AssertPreviewColor(orbit.CurrentInstance, "human.pajama", colors["pajama-red"]);
            Assert.That(TryBuildAppearancePacket("ABCD1234", out var packetAfterApply), Is.True);
            Assert.That(AppearanceWireCodec.TryDecode(packetAfterApply, "ABCD1234", runtime.Snapshot,
                out var networkAfterApply), Is.True);
            AssertSelection(networkAfterApply, applied, "network published apply");
            Assert.That(packetAfterApply, Is.Not.EqualTo(packetBeforePreview), "Successful Apply must change the wire payload.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealWriteFailuresRestoreDiskMemoryAndCrossRolePreview()
        {
            var published = Field<AppearanceSelection>(application, "publishedModularAppearance").Copy();
            var privateDraft = Field<AppearanceSelection>(application, "localModularAppearanceDraft").Copy();
            ui.ShowCustomization();

            orbit.Show(AlfaRole.Mosquito);
            GameObject mosquitoPreview = orbit.CurrentInstance;
            SetField(application, "modularEditedRole", AlfaRole.Human);
            var mosquitoCandidate = privateDraft.Copy();
            mosquitoCandidate.Mosquito.SetOption("mosquito.color", "mosquito-blue");
            byte[] beforeMosquito = File.ReadAllBytes(PreferencePath);
            ForceWriteFailure();
            SetField(application, "appearanceAt", 91d);
            application.PreviewModularCustomization(mosquitoCandidate, AlfaRole.Mosquito);

            Assert.That(File.ReadAllBytes(PreferencePath), Is.EqualTo(beforeMosquito));
            Assert.That(Field<double>(application, "appearanceAt"), Is.EqualTo(91d));
            AssertSelection(Field<AppearanceSelection>(application, "publishedModularAppearance"), published, "Human to Mosquito published rollback");
            AssertSelection(Field<AppearanceSelection>(application, "localModularAppearanceDraft"), privateDraft, "Human to Mosquito private rollback");
            AssertSelection(Field<AppearanceSelection>(application, "previewModularAppearance"), privateDraft, "Human to Mosquito preview rollback");
            AssertPreviewColor(mosquitoPreview, "mosquito.color", colors["mosquito-green"]);
            AssertNoTemporaryFiles();

            Directory.Delete(BackupPath, true);
            orbit.Show(AlfaRole.Human);
            GameObject humanPreview = orbit.CurrentInstance;
            SetField(application, "modularEditedRole", AlfaRole.Mosquito);
            var humanCandidate = privateDraft.Copy();
            humanCandidate.Human.SetOption("human.skin", "skin-light");
            byte[] beforeHuman = File.ReadAllBytes(PreferencePath);
            ForceWriteFailure();
            SetField(application, "appearanceAt", 117d);
            application.SaveModularCustomization(humanCandidate, AlfaRole.Human);

            Assert.That(File.ReadAllBytes(PreferencePath), Is.EqualTo(beforeHuman));
            Assert.That(Field<double>(application, "appearanceAt"), Is.EqualTo(117d), "Failed Apply must not schedule publication.");
            AssertSelection(Field<AppearanceSelection>(application, "publishedModularAppearance"), published, "Mosquito to Human published rollback");
            AssertSelection(Field<AppearanceSelection>(application, "localModularAppearanceDraft"), privateDraft, "Mosquito to Human private rollback");
            AssertSelection(Field<AppearanceSelection>(application, "previewModularAppearance"), privateDraft, "Mosquito to Human preview rollback");
            AssertPreviewColor(humanPreview, "human.skin", colors["skin-dark"]);
            AssertPreviewColor(humanPreview, "human.pajama", colors["pajama-purple"]);
            AssertNoTemporaryFiles();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingOrDifferentApplicationPrefabsKeepRuntimeUnavailable()
        {
            Assert.That(provider.TryResolve(null, mosquitoPrefab, out _, out var missingReason), Is.False);
            Assert.That(missingReason, Does.Contain("application human and mosquito prefabs"));

            var unrelated = Own(new GameObject("Unrelated legacy human prefab"));
            application.HumanPrefab = unrelated;
            Invoke(application, "ResolveModularCustomizationRuntime");
            Assert.That(Field<ModularCustomizationRuntime>(application, "modularCustomizationRuntime"), Is.Null);
            Assert.That(TryBuildAppearancePacket("ABCD1234", out var blockedPacket), Is.False,
                "Schema 2 without its validated runtime must not send basic defaults.");
            Assert.That(blockedPacket, Is.Null);
            yield return null;
        }

        private void BuildRuntimeFixture()
        {
            material = Own(new Material(FindShader()));
            material.name = "Synthetic persistence material";
            humanPrefab = HostPrefab(CustomizationRole.Human, "synthetic.human.rig",
                new[] { "human.skin", "human.pajama" });
            mosquitoPrefab = HostPrefab(CustomizationRole.Mosquito, "synthetic.mosquito.rig",
                new[] { "mosquito.color" });
            var humanBase = BasePart(CustomizationRole.Human, "human.base", "synthetic.human.rig");
            var mosquitoBase = BasePart(CustomizationRole.Mosquito, "mosquito.base", "synthetic.mosquito.rig");
            var catalog = Catalog(humanBase, mosquitoBase);
            var providerObject = Own(new GameObject("Synthetic modular runtime provider"));
            provider = providerObject.AddComponent<ModularCustomizationRuntimeProvider>();
            provider.Catalog = catalog;
            provider.LegacyMappings = LegacyMappings();
            Assert.That(provider.TryResolve(humanPrefab, mosquitoPrefab, out _, out var reason), Is.True, reason);
        }

        private GameObject HostPrefab(CustomizationRole role, string rigId, string[] colorSlots)
        {
            var actor = Own(new GameObject("Synthetic " + role + " application prefab"));
            var view = actor.AddComponent<CharacterView>();
            var host = actor.AddComponent<CharacterCustomizationHost>();
            actor.AddComponent<CharacterModularVisualAssembler>();
            view.HitVolume = actor.AddComponent<BoxCollider>();

            var visual = new GameObject("VisualRoot").transform;
            visual.SetParent(actor.transform, false);
            view.VisualRoot = visual;
            var rig = new GameObject("Rig");
            rig.transform.SetParent(visual, false);
            view.Animator = rig.AddComponent<Animator>();
            var root = new GameObject("Root").transform;
            root.SetParent(rig.transform, false);
            new GameObject("Bone").transform.SetParent(root, false);
            var parts = new GameObject("CustomizationParts").transform;
            parts.SetParent(visual, false);

            var body = new GameObject("AuthoredBody");
            body.transform.SetParent(visual, false);
            body.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(material, colorSlots.Length).ToArray();

            host.Role = role;
            host.View = view;
            host.RigId = rigId;
            host.ExpectedVisualScale = Vector3.one;
            host.PartsRoot = parts;
            host.OwnedBaseRenderers = new Renderer[] { renderer };
            host.ColorChannels = colorSlots.Select((slot, index) => new CharacterCustomizationPart.ColorChannelBinding
                { Renderer = renderer, MaterialIndex = index, ColorSlotId = slot }).ToArray();
            actor.SetActive(false);
            return actor;
        }

        private GameObject BasePart(CustomizationRole role, string slotId, string rigId)
        {
            var root = Own(new GameObject("Synthetic " + role + " base part"));
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = Mesh();
            renderer.sharedMaterials = new[] { material };
            var metadata = root.AddComponent<CharacterCustomizationPart>();
            metadata.Role = role;
            metadata.SlotId = slotId;
            metadata.Kind = CustomizationOptionKind.SkinnedPart;
            metadata.TargetRigId = rigId;
            metadata.TargetVisualScale = Vector3.one;
            metadata.SkinnedRenderers = new[] { new CharacterCustomizationPart.SkinnedRendererBinding
                { Renderer = renderer, RootBonePath = "Root", BonePaths = new[] { "Root/Bone" } } };
            root.SetActive(false);
            return root;
        }

        private CharacterCustomizationCatalog Catalog(GameObject humanBase, GameObject mosquitoBase)
        {
            var catalog = Own(ScriptableObject.CreateInstance<CharacterCustomizationCatalog>());
            catalog.CatalogId = "lms.v020.synthetic.persistence";
            catalog.Revision = 1;
            catalog.Slots = new[]
            {
                Slot(CustomizationRole.Human, "human.base", 1, "human-a", true),
                Slot(CustomizationRole.Human, "human.skin", 2, "skin-warm"),
                Slot(CustomizationRole.Human, "human.pajama", 3, "pajama-blue"),
                Slot(CustomizationRole.Mosquito, "mosquito.base", 4, "mosquito-a", true),
                Slot(CustomizationRole.Mosquito, "mosquito.color", 5, "mosquito-red")
            };
            var options = new List<CharacterCustomizationCatalog.OptionDefinition>
            {
                Visual(CustomizationRole.Human, "human.base", "human-a", humanBase),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", mosquitoBase)
            };
            AddColors(options, CustomizationRole.Human, "human.skin", "skin-",
                new[] { "light", "warm", "tan", "dark" }, new[]
                {
                    new Color32(240, 210, 180, 255), new Color32(210, 160, 120, 255),
                    new Color32(170, 115, 75, 255), new Color32(95, 60, 40, 255)
                });
            AddColors(options, CustomizationRole.Human, "human.pajama", "pajama-",
                new[] { "blue", "red", "green", "purple", "yellow" }, new[]
                {
                    new Color32(30, 100, 220, 255), new Color32(210, 45, 45, 255),
                    new Color32(45, 170, 80, 255), new Color32(130, 70, 190, 255),
                    new Color32(230, 190, 35, 255)
                });
            AddColors(options, CustomizationRole.Mosquito, "mosquito.color", "mosquito-",
                new[] { "red", "blue", "green", "purple" }, new[]
                {
                    new Color32(220, 35, 35, 255), new Color32(40, 100, 220, 255),
                    new Color32(35, 170, 75, 255), new Color32(135, 65, 185, 255)
                });
            catalog.Options = options.ToArray();
            return catalog;
        }

        private void AddColors(List<CharacterCustomizationCatalog.OptionDefinition> target, CustomizationRole role,
            string slotId, string prefix, string[] ids, Color32[] swatches)
        {
            for (int index = 0; index < ids.Length; index++)
            {
                string optionId = prefix + ids[index];
                colors[optionId] = swatches[index];
                target.Add(new CharacterCustomizationCatalog.OptionDefinition
                {
                    Role = role, SlotId = slotId, OptionId = optionId, Label = ids[index],
                    WireOptionId = index + 1, Kind = CustomizationOptionKind.Color,
                    HasSwatch = true, Swatch = swatches[index]
                });
            }
        }

        private static LegacyCustomizationMapping[] LegacyMappings()
        {
            return new[] { "light", "warm", "tan", "dark" }
                .Select(id => Mapping(LegacyCustomizationKind.Skin, id, "human.skin", "skin-" + id))
                .Concat(new[] { "blue", "red", "green", "purple", "yellow" }
                    .Select(id => Mapping(LegacyCustomizationKind.Pajama, id, "human.pajama", "pajama-" + id)))
                .Concat(new[] { "red", "blue", "green", "purple" }
                    .Select(id => Mapping(LegacyCustomizationKind.Mosquito, id, "mosquito.color", "mosquito-" + id)))
                .ToArray();
        }

        private Mesh Mesh()
        {
            var mesh = Own(new Mesh { name = "Synthetic persistence mesh" });
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bindposes = new[] { Matrix4x4.identity };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 }
            };
            return mesh;
        }

        private void AssertPreviewColor(GameObject instance, string slotId, Color32 expected)
        {
            Assert.That(instance, Is.Not.Null, "Preview instance for " + slotId);
            var host = instance.GetComponent<CharacterCustomizationHost>();
            Assert.That(host, Is.Not.Null);
            var binding = host.ColorChannels.Single(item => item.ColorSlotId == slotId);
            var block = new MaterialPropertyBlock();
            binding.Renderer.GetPropertyBlock(block, binding.MaterialIndex);
            Color actual = block.GetColor(Shader.PropertyToID("_BaseColor"));
            Assert.That(actual.r, Is.EqualTo(expected.r / 255f).Within(.01f), slotId + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g / 255f).Within(.01f), slotId + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b / 255f).Within(.01f), slotId + " blue");
            Assert.That(actual.a, Is.EqualTo(expected.a / 255f).Within(.01f), slotId + " alpha");
        }

        private void WriteV1(BasicCustomizationDraft published, BasicCustomizationDraft privateDraft)
        {
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(PreferencePath, JsonUtility.ToJson(new PreferencesV1Fixture
                { appearance = published, localAppearanceDraft = privateDraft }, true));
        }

        private PreferencesV2Fixture ReadV2() => JsonUtility.FromJson<PreferencesV2Fixture>(File.ReadAllText(PreferencePath));

        private void ForceWriteFailure()
        {
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            if (Directory.Exists(BackupPath)) Directory.Delete(BackupPath, true);
            Directory.CreateDirectory(BackupPath);
        }

        private void AssertNoTemporaryFiles()
            => Assert.That(Directory.GetFiles(dataPath, "preferences.json.tmp-*"), Is.Empty);

        // Deletes only what this fixture creates: the directory can be a shared run folder holding other evidence.
        private void ResetFixtureDirectory()
        {
            Directory.CreateDirectory(dataPath);
            if (File.Exists(PreferencePath)) File.Delete(PreferencePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            if (Directory.Exists(BackupPath)) Directory.Delete(BackupPath, true); // ForceWriteFailure's blocker
            foreach (string temporary in Directory.GetFiles(dataPath, "preferences.json.tmp-*")) File.Delete(temporary);
        }

        private string PreferencePath => Path.Combine(dataPath, "preferences.json");
        private string BackupPath => PreferencePath + ".backup";

        private static BasicCustomizationDraft PublishedLegacy()
            => new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");

        private static BasicCustomizationDraft PrivateLegacy()
            => new BasicCustomizationDraft(AlfaRole.Mosquito, "dark", "purple", "green");

        private static AlfaSettingsDraft Settings()
            => new AlfaSettingsDraft { MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .85f,
                VoiceVolume = .8f, PushToTalkBinding = "<Keyboard>/v", HumanSensitivity = 1f,
                MosquitoSensitivity = 1f, FullScreen = false };

        private static CharacterCustomizationCatalog.SlotDefinition Slot(CustomizationRole role, string id,
            int wire, string defaultOption, bool isBase = false) => new CharacterCustomizationCatalog.SlotDefinition
            { Role = role, SlotId = id, Label = id, WireSlotId = wire, Required = true,
                IsBaseSlot = isBase, DefaultOptionId = defaultOption };

        private static CharacterCustomizationCatalog.OptionDefinition Visual(CustomizationRole role, string slot,
            string id, GameObject asset) => new CharacterCustomizationCatalog.OptionDefinition
            { Role = role, SlotId = slot, OptionId = id, Label = id, WireOptionId = 1,
                Kind = CustomizationOptionKind.SkinnedPart, AssetId = "synthetic-" + id, RuntimeAsset = asset };

        private static LegacyCustomizationMapping Mapping(LegacyCustomizationKind kind, string legacy,
            string slot, string option) => new LegacyCustomizationMapping
            { Kind = kind, LegacyId = legacy, SlotId = slot, OptionId = option };

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Invoke(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private bool TryBuildAppearancePacket(string roomCode, out byte[] packet)
        {
            object[] arguments = { roomCode, null };
            bool result = (bool)application.GetType().GetMethod("TryBuildAppearancePacket",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(application, arguments);
            packet = (byte[])arguments[1];
            return result;
        }

        private static void AssertSelection(AppearanceSelection actual, AppearanceSelection expected, string label)
        {
            Assert.That(actual, Is.Not.Null, label);
            Assert.That(actual.CanonicalEquals(expected), Is.True, label);
        }

        private static string RequireIsolatedDataPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--lms-validation-data");
            if (option < 0 || option + 1 >= args.Length)
                Assert.Ignore("Requires --lms-validation-data below a validation root.");
            string path = ValidationDataGuard.Normalize(args[option + 1]);
            Assert.That(ValidationDataGuard.IsDedicatedRunDirectory(path), Is.True,
                "Use a dedicated directory below " + string.Join(" or ", ValidationDataGuard.Roots) + ", never a root itself.");
            return path;
        }

        private static Shader FindShader() => Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader");
    }
}
#endif
