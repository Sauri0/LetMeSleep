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
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// v0.3.0 production modular customization (PER-04..07, UI-06 screens 5-6): the catalog asset, its pinned network
    /// identity, the four host prefabs, the build scene's provider, the V1 to V2 preference migration with the real
    /// catalog, and the visual contract of the parts on the production rigs.
    /// </summary>
    public sealed class ProductionCustomizationCatalogPlayModeTests
    {
        private const string CatalogPath = "Assets/LetMeSleep/Content/Characters/Customization/LMS_CustomizationCatalog.asset";
        private const string Prefabs = "Assets/LetMeSleep/Content/Characters/Prefabs/";
        private const string Materials = "Assets/LetMeSleep/Content/Characters/Materials/";
        private const string SceneName = "LetMeSleepHiggsfield";

        /// <summary>
        /// Online identity of this catalog (SHA-256 of ids, wire codes, kinds, swatches, asset GUIDs and rules; labels
        /// and thumbnails excluded). Every client of a build computes this value; peers with another value fall back
        /// to default looks. Changing a swatch, code, option or part GUID is a network change: bump the catalog
        /// revision and update this constant on purpose.
        /// </summary>
        private const string PinnedFingerprint = "f1b7401a48ea6672608b0573a7285ff2c2af934e7579bcf0507e081235f630ff";

        private static readonly (string Slot, int Wire, string Default)[] ExpectedSlots =
        {
            ("human.base", 1, "base"), ("human.skin", 2, "calido"), ("human.hair", 3, "corto"),
            ("human.hair_color", 4, "castano"), ("human.headwear", 6, "gorro-dormir"), ("human.glasses", 7, "none"),
            ("human.top", 8, "remera"), ("human.top_color", 9, "crema"), ("human.bottom", 10, "pijama"),
            ("human.pajama", 11, "azul"), ("human.footwear", 12, "pantuflas"), ("human.back", 14, "none"),
            ("mosquito.base", 15, "base"), ("mosquito.color", 16, "natural"), ("mosquito.wings", 17, "clasicas"),
            ("mosquito.wing_color", 18, "lavanda"), ("mosquito.proboscis", 20, "estandar"), ("mosquito.markings", 22, "none"),
            ("mosquito.accent_color", 23, "crema"), ("mosquito.accessory", 24, "none"),
        };

        private readonly List<Object> owned = new List<Object>();
        private string dataPath;

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        // ------------------------------------------------------------------ catalog contract
        [Test]
        public void CatalogIsCompleteRuntimeReadyAndHasThePinnedNetworkIdentity()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out var errors), Is.True, string.Join("; ", errors ?? new string[0]));
            Assert.That(snapshot.RuntimeReady, Is.True);
            Assert.That(snapshot.CatalogId, Is.EqualTo("lms.v030.characters"));
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            Assert.That(snapshot.Slots.Count, Is.EqualTo(ExpectedSlots.Length).And.LessThanOrEqualTo(CustomizationCatalogSnapshot.MaximumSelectedSlots));
            foreach (var (slotId, wire, defaultId) in ExpectedSlots)
            {
                Assert.That(snapshot.TrySlot(slotId, out var slot), Is.True, slotId);
                Assert.That(slot.WireSlotId, Is.EqualTo(wire), slotId + " wire code (never renumber)");
                Assert.That(slot.DefaultOptionId, Is.EqualTo(defaultId), slotId + " default");
                bool baseSlot = slotId.EndsWith(".base", StringComparison.Ordinal);
                Assert.That(slot.IsBaseSlot, Is.EqualTo(baseSlot), slotId);
                Assert.That(string.IsNullOrEmpty(slot.Label), Is.EqualTo(baseSlot), slotId + ": only the single base is hidden");
                if (!baseSlot) Assert.That(slot.Label, Is.EqualTo(slot.Label.ToUpperInvariant()), slotId + " label style");
                Assert.That(slot.Options.All(option => !string.IsNullOrWhiteSpace(option.Label)), Is.True, slotId + " option labels");
            }
            Assert.That(snapshot.Fingerprint, Is.EqualTo(PinnedFingerprint), "The online catalog identity changed.");
            Assert.That(catalog.TryCreateSnapshot(out var again, out _) && again.Fingerprint == snapshot.Fingerprint, Is.True,
                "The fingerprint must be deterministic.");
            foreach (var option in catalog.Options)
            {
                StringAssert.DoesNotContain("Bite", option.Label ?? string.Empty);
                if (option.Kind == CustomizationOptionKind.SkinnedPart)
                {
                    Assert.That(option.RuntimeAsset, Is.Not.Null, option.OptionId);
                    Assert.That(option.AssetId, Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(option.RuntimeAsset))),
                        option.SlotId + "/" + option.OptionId + " AssetId must be the prefab GUID");
                }
                bool hiddenSlot = option.SlotId.EndsWith(".base", StringComparison.Ordinal);
                if (option.Kind != CustomizationOptionKind.Color && !hiddenSlot)
                    Assert.That(option.Thumbnail, Is.Not.Null, option.SlotId + "/" + option.OptionId + " needs a baked thumbnail");
            }
        }

        [Test]
        public void LabelsAndThumbnailsStayOutOfTheFingerprintButSwatchesDoNot()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var reference, out _), Is.True);
            var copy = Own(Object.Instantiate(catalog));
            foreach (var slot in copy.Slots) if (!string.IsNullOrEmpty(slot.Label)) slot.Label += " X";
            foreach (var option in copy.Options) { option.Label += " X"; option.Thumbnail = null; }
            Assert.That(copy.TryCreateSnapshot(out var relabelled, out _), Is.True);
            Assert.That(relabelled.Fingerprint, Is.EqualTo(reference.Fingerprint));
            copy.Options.First(option => option.Kind == CustomizationOptionKind.Color).Swatch = new Color32(1, 2, 3, 255);
            Assert.That(copy.TryCreateSnapshot(out var recoloured, out _), Is.True);
            Assert.That(recoloured.Fingerprint, Is.Not.EqualTo(reference.Fingerprint));
        }

        [Test]
        public void DefaultsAreTheAuthoredPajamaLookAndItsExactPalette()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            var defaults = snapshot.DefaultSelection();
            Assert.That(defaults.Human.OptionFor("human.headwear"), Is.EqualTo("gorro-dormir"));
            Assert.That(defaults.Human.OptionFor("human.bottom"), Is.EqualTo("pijama"));
            Assert.That(defaults.Human.OptionFor("human.footwear"), Is.EqualTo("pantuflas"));
            Assert.That(defaults.Human.OptionFor("human.top"), Is.EqualTo("remera"));
            foreach (var (slot, option, material) in new[]
                     {
                         ("human.skin", "calido", "Human_Skin"), ("human.top_color", "crema", "Human_Shirt"),
                         ("human.pajama", "azul", "Human_Pajamas"), ("human.hair_color", "castano", "Human_Hair"),
                         ("mosquito.color", "natural", "Mosquito_Shell"), ("mosquito.wing_color", "lavanda", "Mosquito_Wing")
                     })
            {
                CustomizationOptionSnapshot o = null;
                Assert.That(snapshot.TrySlot(slot, out var s) && s.TryOption(option, out o) && o.HasSwatch, Is.True, slot);
                Color swatch = Unpack(o.SwatchRgba);
                var authored = AssetDatabase.LoadAssetAtPath<Material>(Materials + material + ".mat").GetColor("_BaseColor");
                Assert.That(swatch.r, Is.EqualTo(authored.r).Within(1.5f / 255), slot + " red");
                Assert.That(swatch.g, Is.EqualTo(authored.g).Within(1.5f / 255), slot + " green");
                Assert.That(swatch.b, Is.EqualTo(authored.b).Within(1.5f / 255), slot + " blue");
            }
        }

        [Test]
        public void ANonDefaultOutfitRoundTripsOnTheWireUnder128Bytes()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            var outfit = snapshot.DefaultSelection();
            outfit.Human.SetOption("human.hair", "rulos");
            outfit.Human.SetOption("human.headwear", "gorra");
            outfit.Human.SetOption("human.glasses", "sol");
            outfit.Human.SetOption("human.top", "buzo");
            outfit.Human.SetOption("human.top_color", "verde");
            outfit.Human.SetOption("human.bottom", "jean");
            outfit.Human.SetOption("human.footwear", "zapatillas");
            outfit.Human.SetOption("human.back", "mochila");
            outfit.Mosquito.SetOption("mosquito.wings", "largas");
            outfit.Mosquito.SetOption("mosquito.markings", "lunares");
            outfit.Mosquito.SetOption("mosquito.accessory", "flor");
            outfit.Mosquito.SetOption("mosquito.color", "toxico");
            Assert.That(AppearanceWireCodec.TryEncode(snapshot, "ABCD1234", outfit, out var packet), Is.True);
            Assert.That(packet.Length, Is.LessThanOrEqualTo(AppearanceWireCodec.MaximumPacketBytes));
            Assert.That(AppearanceWireCodec.TryDecode(packet, "ABCD1234", snapshot, out var decoded), Is.True);
            Assert.That(snapshot.TryNormalize(outfit, out var normalized, out _), Is.True);
            Assert.That(decoded.CanonicalEquals(normalized), Is.True);
        }

        // ------------------------------------------------------------------ hosts and parts
        [UnityTest]
        public IEnumerator EveryOptionAppliesOnEveryProductionHost()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            foreach (string name in new[] { "LMS_Human", "LMS_Human_FirstPerson", "LMS_HumanMenu", "LMS_Mosquito" })
            {
                var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab")));
                yield return null;
                var view = actor.GetComponent<CharacterView>();
                var host = actor.GetComponent<CharacterCustomizationHost>();
                var assembler = actor.GetComponent<CharacterModularVisualAssembler>();
                Assert.That(view && host && assembler, Is.True, name + " must expose a modular host");
                Assert.That(host.RigId, Is.EqualTo(host.Role == CustomizationRole.Human ? "lms.human.v030" : "lms.mosquito.v030"));
                foreach (var slot in snapshot.Slots.Where(item => item.Role == host.Role))
                    foreach (var option in slot.Options)
                    {
                        var selection = snapshot.DefaultSelection();
                        selection.For(host.Role).SetOption(slot.SlotId, option.OptionId);
                        Assert.That(assembler.TryApply(view, catalog, selection, host.Role, out var error), Is.True,
                            name + " " + slot.SlotId + "/" + option.OptionId + ": " + error);
                        Assert.That(host.OwnedBaseRenderers.All(renderer => !renderer.enabled), Is.True, name + " authored body hidden");
                        Assert.That(host.PartsRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Count(item => item.enabled), Is.GreaterThan(0));
                    }
                if (host.Role == CustomizationRole.Human)
                {
                    var head = view.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(item => item.name == "HumanHead");
                    Assert.That(head.enabled, Is.True, "The certified face (blink shape keys) always stays on the host.");
                }
                assembler.ClearAppliedParts();
                Assert.That(host.OwnedBaseRenderers.All(renderer => renderer.enabled), Is.True, name + " restores the authored body");
            }
        }

        [UnityTest]
        public IEnumerator HeadPartsFollowFirstPersonHairHidesUnderHatsAndColoursTint()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Human_FirstPerson.prefab")));
            yield return null;
            var view = actor.GetComponent<CharacterView>();
            var assembler = actor.GetComponent<CharacterModularVisualAssembler>();
            Assert.That(view.IsFirstPerson, Is.True);

            var outfit = snapshot.DefaultSelection();
            outfit.Human.SetOption("human.hair", "rulos");
            outfit.Human.SetOption("human.headwear", "gorra");
            outfit.Human.SetOption("human.glasses", "sol");
            outfit.Human.SetOption("human.top_color", "rojo");
            outfit.Human.SetOption("human.bottom", "jean");
            outfit.Human.SetOption("human.pajama", "verde"); // worn by nothing with jeans: must not block the outfit
            Assert.That(assembler.TryApply(view, catalog, outfit, CustomizationRole.Human, out var error), Is.True, error);
            var parts = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(item => item.name.StartsWith("HumanPart", StringComparison.Ordinal)).ToArray();
            foreach (string head in new[] { "HumanPartHeadwear", "HumanPartHair", "HumanPartHairTop", "HumanPartGlasses" })
            {
                var renderer = parts.Single(item => item.name == head);
                Assert.That(view.HeadRenderers, Does.Contain(renderer), head + " must follow first-person head visibility");
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), head + " hidden from the local camera");
            }
            Assert.That(parts.Single(item => item.name == "HumanPartHairTop").enabled, Is.False, "the crown of the hair hides under a hat");
            Assert.That(parts.Single(item => item.name == "HumanPartHair").enabled, Is.True, "the curls under the cap stay");
            var top = parts.Single(item => item.name == "HumanPartTop");
            AssertTint(top, "Human_Shirt", new Color32(0xC8, 0x32, 0x2E, 255), 0);
            int shadeIndex = Array.FindIndex(top.sharedMaterials, m => m.name == "Human_ShirtShade");
            var block = new MaterialPropertyBlock();
            top.GetPropertyBlock(block, shadeIndex);
            Assert.That(block.GetColor("_BaseColor").r, Is.LessThan(0xC8 / 255f - .02f), "the shirt shade facets stay darker");

            outfit.Human.SetOption("human.headwear", "none");
            Assert.That(assembler.TryApply(view, catalog, outfit, CustomizationRole.Human, out error), Is.True, error);
            // The replaced parts are destroyed at the end of the frame (deactivated now): look at active objects only.
            var hairTop = actor.GetComponentsInChildren<SkinnedMeshRenderer>(false).Single(item => item.name == "HumanPartHairTop");
            Assert.That(hairTop.enabled, Is.True, "without a hat the whole hairstyle shows");

            var mosquito = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Mosquito.prefab")));
            yield return null;
            var mosquitoView = mosquito.GetComponent<CharacterView>();
            var wingsOutfit = snapshot.DefaultSelection();
            wingsOutfit.Mosquito.SetOption("mosquito.wing_color", "rosa");
            Assert.That(mosquito.GetComponent<CharacterModularVisualAssembler>().TryApply(mosquitoView, catalog, wingsOutfit,
                CustomizationRole.Mosquito, out error), Is.True, error);
            var membranes = mosquito.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(item => item.name == "MosquitoPartWings");
            membranes.GetPropertyBlock(block, 0);
            Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(.5f).Within(.01f), "membranes keep their translucency");
            Assert.That(block.GetColor("_BaseColor").r, Is.EqualTo(0xF5 / 255f).Within(.01f));

            // A camera that hides the authored actor (MosquitoFollowCamera inside the body) hides the parts too.
            var authored = mosquito.GetComponent<CharacterCustomizationHost>().OwnedBaseRenderers;
            foreach (var renderer in authored) renderer.forceRenderingOff = true;
            yield return null;
            Assert.That(mosquito.GetComponent<CharacterCustomizationHost>().PartsRoot.GetComponentsInChildren<Renderer>()
                .All(item => item.forceRenderingOff), Is.True);
            foreach (var renderer in authored) renderer.forceRenderingOff = false;
            yield return null;
            Assert.That(mosquito.GetComponent<CharacterCustomizationHost>().PartsRoot.GetComponentsInChildren<Renderer>()
                .Any(item => item.forceRenderingOff), Is.False);
        }

        /// <summary>The default parts are the authored body split into pieces: in the idle pose their skinned
        /// vertices cover the same volume as the authored renderers they replace (rig and bind pose agree).</summary>
        [UnityTest]
        public IEnumerator DefaultPartsReproduceTheAuthoredBodyInThePose()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            foreach (string name in new[] { "LMS_Human", "LMS_Mosquito" })
            {
                var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab")));
                yield return null;
                var view = actor.GetComponent<CharacterView>();
                var host = actor.GetComponent<CharacterCustomizationHost>();
                var idle = view.Motions.First(motion => motion.StateName.EndsWith(".Idle", StringComparison.Ordinal));
                var clip = view.Animator.runtimeAnimatorController.animationClips.First(item => item.name == idle.ClipName);
                view.Animator.enabled = false;
                clip.SampleAnimation(view.Animator.gameObject, clip.length * .3f);
                var authored = Bounds(host.OwnedBaseRenderers.OfType<SkinnedMeshRenderer>());
                Assert.That(actor.GetComponent<CharacterModularVisualAssembler>().TryApply(view, catalog, snapshot.DefaultSelection(),
                    host.Role, out var error), Is.True, error);
                clip.SampleAnimation(view.Animator.gameObject, clip.length * .3f);
                var parts = Bounds(host.PartsRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(item => item.enabled));
                float tolerance = host.Role == CustomizationRole.Human ? .004f : .002f;
                Assert.That(Vector3.Distance(parts.min, authored.min), Is.LessThan(tolerance), name + " min " + parts.min + " vs " + authored.min);
                Assert.That(Vector3.Distance(parts.max, authored.max), Is.LessThan(tolerance), name + " max " + parts.max + " vs " + authored.max);
            }
        }

        // ------------------------------------------------------------------ build scene and preferences
        [UnityTest]
        public IEnumerator BuildSceneActivatesTheCatalogAndMigratesLegacyPreferences()
        {
            dataPath = RequireIsolatedDataPath();
            string preferences = Path.Combine(dataPath, "preferences.json");
            Directory.CreateDirectory(dataPath);
            foreach (string stale in new[] { preferences, preferences + ".backup" }) if (File.Exists(stale)) File.Delete(stale);
            File.WriteAllText(preferences, "{\"schema\":1,\"playerName\":\"Prueba\",\"appearance\":{\"Role\":0,\"SkinColorId\":\"tan\"," +
                "\"PajamaColorId\":\"green\",\"MosquitoColorId\":\"purple\"},\"localAppearanceDraft\":{\"Role\":1,\"SkinColorId\":\"light\"," +
                "\"PajamaColorId\":\"yellow\",\"MosquitoColorId\":\"blue\"}}");
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            var application = Object.FindFirstObjectByType<AlfaApplication>();
            try
            {
                Assert.That(application.ModularCustomizationProvider, Is.Not.Null, "The build scene must install the production provider.");
                Assert.That(application.ModularCustomizationProvider.Catalog, Is.SameAs(LoadCatalog()));
                Assert.That(Field<ModularCustomizationRuntime>(application, "modularCustomizationRuntime"), Is.Not.Null,
                    "The production catalog and the application prefabs must open the modular gate.");
                Assert.That(Field<int>(application, "loadedPreferenceSchema"), Is.EqualTo(2));
                var published = Field<AppearanceSelection>(application, "publishedModularAppearance");
                var local = Field<AppearanceSelection>(application, "localModularAppearanceDraft");
                Assert.That(published.Human.OptionFor("human.skin"), Is.EqualTo("bronce"));
                Assert.That(published.Human.OptionFor("human.pajama"), Is.EqualTo("verde"));
                Assert.That(published.Mosquito.OptionFor("mosquito.color"), Is.EqualTo("fantasia"));
                Assert.That(published.Human.OptionFor("human.headwear"), Is.EqualTo("gorro-dormir"), "clothes keep the default pajama look");
                Assert.That(local.Human.OptionFor("human.skin"), Is.EqualTo("claro"));
                Assert.That(local.Human.OptionFor("human.pajama"), Is.EqualTo("mostaza"));
                Assert.That(local.Mosquito.OptionFor("mosquito.color"), Is.EqualTo("hielo"));
                string json = File.ReadAllText(preferences);
                Assert.That(PreferenceSchemaCodec.IsExactV2RoundTrip(json), Is.True, "the file is rewritten as schema 2");
                var ui = Field<AlfaUiController>(application, "ui");
                var state = Field<CustomizationUiState>(ui, "customizationState");
                Assert.That(state.Mode, Is.EqualTo(CustomizationUiMode.Modular));
                Assert.That(state.ModularPreviewAvailable, Is.True);
                Assert.That(state.ThumbnailResolver("human.headwear", "gorra"), Is.Not.Null);

                // The viewer: switching to the mosquito tab previews the modular mosquito on the viewer layer (the
                // clone is dressed the frame the viewer creates it, before its first colour pass).
                ui.ShowCustomization();
                yield return null;
                Call(ui, "SetCustomizationRole", AlfaRole.Mosquito);
                yield return null;
                yield return null;
                var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true);
                var clone = orbit.CurrentInstance;
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone.GetComponent<CharacterCustomizationHost>().Role, Is.EqualTo(CustomizationRole.Mosquito));
                Assert.That(clone.GetComponent<CharacterModularVisualAssembler>().HasAppliedParts, Is.True);
                var visible = clone.GetComponentsInChildren<Renderer>(false).Where(item => item.enabled).ToArray();
                Assert.That(visible.Length, Is.GreaterThan(0));
                Assert.That(visible.All(item => item.gameObject.layer == 30), Is.True,
                    "every visible viewer renderer must be on the preview layer: " +
                    string.Join(",", visible.Where(item => item.gameObject.layer != 30).Select(item => item.name)));
                ui.ShowMainMenu();
                yield return null;
                Assert.That(Invoke<bool>(application, "TryBuildAppearancePacket", "ABCD1234", out byte[] packet), Is.True);
                Assert.That(AppearanceWireCodec.TryDecode(packet, "ABCD1234", LoadSnapshot(), out var sent), Is.True);
                Assert.That(sent.CanonicalEquals(published), Is.True, "only the published look travels");
            }
            finally
            {
                foreach (string stale in new[] { preferences, preferences + ".backup" }) if (File.Exists(stale)) File.Delete(stale);
            }
        }

        // ------------------------------------------------------------------ helpers
        private static CharacterCustomizationCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCustomizationCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Production catalog missing: " + CatalogPath);
            return catalog;
        }

        private static CustomizationCatalogSnapshot LoadSnapshot()
        {
            Assert.That(LoadCatalog().TryCreateSnapshot(out var snapshot, out _), Is.True);
            return snapshot;
        }

        private static void AssertTint(SkinnedMeshRenderer renderer, string material, Color32 expected, float shade)
        {
            int index = Array.FindIndex(renderer.sharedMaterials, m => m && m.name == material);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), material);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, index);
            var colour = block.GetColor("_BaseColor");
            Assert.That(colour.r, Is.EqualTo(expected.r / 255f * (1 - shade)).Within(.01f), material);
            Assert.That(colour.g, Is.EqualTo(expected.g / 255f * (1 - shade)).Within(.01f), material);
            Assert.That(colour.b, Is.EqualTo(expected.b / 255f * (1 - shade)).Within(.01f), material);
        }

        private static Bounds Bounds(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            bool first = true;
            var result = new Bounds();
            foreach (var renderer in renderers)
            {
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        var world = renderer.transform.TransformPoint(vertex);
                        if (first) { result = new Bounds(world, Vector3.zero); first = false; }
                        else result.Encapsulate(world);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            Assert.That(first, Is.False, "no skinned vertices");
            return result;
        }

        private static Color Unpack(uint value) => new Color32((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static T Invoke<T>(object target, string name, string argument, out byte[] packet)
        {
            object[] arguments = { argument, null };
            var result = (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
            packet = (byte[])arguments[1];
            return result;
        }

        private static void Call(object target, string name, params object[] arguments) =>
            target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .First(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(target, arguments);

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
    }
}
#endif
