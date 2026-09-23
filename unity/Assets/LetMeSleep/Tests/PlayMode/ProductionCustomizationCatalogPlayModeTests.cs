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
        private const string PinnedFingerprint = "534a131f6f14022ec3fa60a5cf2fe71d3ee0ede4fa52836733bafe911375e6d7";

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
            Assert.That(snapshot.Revision, Is.EqualTo(2));
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
            foreach (string head in new[] { "HumanPartHeadwear", "HumanPartHairUnderHat", "HumanPartHairTop", "HumanPartGlasses" })
            {
                var renderer = parts.Single(item => item.name == head);
                Assert.That(view.HeadRenderers, Does.Contain(renderer), head + " must follow first-person head visibility");
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), head + " hidden from the local camera");
            }
            Assert.That(parts.Single(item => item.name == "HumanPartHairTop").enabled, Is.False, "the full hairstyle hides under a hat");
            Assert.That(parts.Single(item => item.name == "HumanPartHairUnderHat").enabled, Is.True, "the flat under-hat curls show");
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
            var underHat = actor.GetComponentsInChildren<SkinnedMeshRenderer>(false).Single(item => item.name == "HumanPartHairUnderHat");
            Assert.That(underHat.enabled, Is.False, "the under-hat variant shows only under a hat");

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

        /// <summary>
        /// Review r1 #1: under every hat each hairstyle shows only its flat variant, which (like the host's authored
        /// hair) stays within 5 mm of the skull in the idle pose; without a hat only the full style shows.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryHairUnderEveryHatHugsTheSkull()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Human.prefab")));
            yield return null;
            var view = actor.GetComponent<CharacterView>();
            var assembler = actor.GetComponent<CharacterModularVisualAssembler>();
            view.Animator.enabled = false;
            var idle = view.Motions.First(motion => motion.StateName.EndsWith(".Idle", StringComparison.Ordinal));
            var clip = view.Animator.runtimeAnimatorController.animationClips.First(item => item.name == idle.ClipName);
            var head = view.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(item => item.name == "HumanHead");
            snapshot.TrySlot("human.hair", out var hairSlot);
            snapshot.TrySlot("human.headwear", out var hatSlot);
            foreach (var hair in hairSlot.Options)
                foreach (var hat in hatSlot.Options)
                {
                    var outfit = snapshot.DefaultSelection();
                    outfit.Human.SetOption("human.hair", hair.OptionId);
                    outfit.Human.SetOption("human.headwear", hat.OptionId);
                    Assert.That(assembler.TryApply(view, catalog, outfit, CustomizationRole.Human, out var error), Is.True, error);
                    clip.SampleAnimation(view.Animator.gameObject, clip.length * .3f);
                    yield return null;
                    var worn = actor.GetComponent<CharacterCustomizationHost>().PartsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false)
                        .Where(item => item.name.StartsWith("HumanPartHair", StringComparison.Ordinal)).ToArray();
                    bool hatOn = hat.Kind != CustomizationOptionKind.None;
                    Assert.That(worn.Where(item => item.name == "HumanPartHairTop").All(item => item.enabled != hatOn), Is.True,
                        hair.OptionId + "/" + hat.OptionId + ": the full style shows only without a hat");
                    foreach (var under in worn.Where(item => item.name == "HumanPartHairUnderHat"))
                    {
                        Assert.That(under.enabled, Is.EqualTo(hatOn), hair.OptionId + "/" + hat.OptionId + ": the flat variant shows only under a hat");
                        if (!hatOn) continue;
                        float farthest = FarthestFromSkin(under, head);
                        Assert.That(farthest, Is.LessThanOrEqualTo(.0055f), hair.OptionId + "/" + hat.OptionId + " under-hat hair must stay within 5 mm of the skull");
                    }
                }
        }

        /// <summary>Review r1 #6: the bite anchor (ProboscisTip) is the real tip of every proboscis option.</summary>
        [UnityTest]
        public IEnumerator EveryProboscisTipIsTheBiteAnchor()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out _), Is.True);
            var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Mosquito.prefab")));
            yield return null;
            var view = actor.GetComponent<CharacterView>();
            var assembler = actor.GetComponent<CharacterModularVisualAssembler>();
            view.Animator.enabled = false;
            var mouth = view.Animator.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Socket.Mouth");
            var probe = view.Animator.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Proboscis");
            snapshot.TrySlot("mosquito.proboscis", out var slot);
            foreach (var option in slot.Options)
            {
                var outfit = snapshot.DefaultSelection();
                outfit.Mosquito.SetOption("mosquito.proboscis", option.OptionId);
                Assert.That(assembler.TryApply(view, catalog, outfit, CustomizationRole.Mosquito, out var error), Is.True, error);
                yield return null;
                view.RefreshAnchors();
                var renderer = actor.GetComponent<CharacterCustomizationHost>().PartsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false)
                    .Single(item => item.name == "MosquitoPartProboscis");
                var mesh = new Mesh();
                Vector3 tip;
                try
                {
                    renderer.BakeMesh(mesh, true);
                    var axis = (mouth.position - probe.position).normalized;
                    tip = mesh.vertices.Select(v => renderer.transform.TransformPoint(v)).OrderByDescending(p => Vector3.Dot(p - probe.position, axis)).First();
                }
                finally { Object.DestroyImmediate(mesh); }
                var anchor = view.GetAnchor("ProboscisTip").position;
                // The proboscis at 0.5 visual scale: 3 mm in world is 6 mm of source length.
                Assert.That(Vector3.Distance(anchor, tip), Is.LessThan(.003f), option.OptionId + ": ProboscisTip " + anchor + " vs tip " + tip);
                bool offset = view.TryGetAnchorOffset("ProboscisTip", out _);
                Assert.That(offset, Is.EqualTo(option.OptionId == "corta" || option.OptionId == "larga"), option.OptionId + " anchor offset");
            }
            assembler.ClearAppliedParts();
            view.RefreshAnchors();
            Assert.That(Vector3.Distance(view.GetAnchor("ProboscisTip").position, mouth.position), Is.LessThan(1e-5f),
                "without parts the anchor is back on Socket.Mouth");
        }

        /// <summary>Review r1 #14: colours with nothing to tint are reported as not applying (the UI dims them).</summary>
        [Test]
        public void ColoursWithoutAWornConsumerDoNotApply()
        {
            var provider = new GameObject("provider").AddComponent<ModularCustomizationRuntimeProvider>();
            Own(provider.gameObject);
            provider.Catalog = LoadCatalog();
            provider.LegacyMappings = new[]
            {
                (LegacyCustomizationKind.Skin, "light", "human.skin", "claro"), (LegacyCustomizationKind.Skin, "warm", "human.skin", "calido"),
                (LegacyCustomizationKind.Skin, "tan", "human.skin", "bronce"), (LegacyCustomizationKind.Skin, "dark", "human.skin", "oscuro"),
                (LegacyCustomizationKind.Pajama, "blue", "human.pajama", "azul"), (LegacyCustomizationKind.Pajama, "red", "human.pajama", "rojo"),
                (LegacyCustomizationKind.Pajama, "green", "human.pajama", "verde"), (LegacyCustomizationKind.Pajama, "purple", "human.pajama", "violeta"),
                (LegacyCustomizationKind.Pajama, "yellow", "human.pajama", "mostaza"), (LegacyCustomizationKind.Mosquito, "red", "mosquito.color", "natural"),
                (LegacyCustomizationKind.Mosquito, "blue", "mosquito.color", "hielo"), (LegacyCustomizationKind.Mosquito, "green", "mosquito.color", "bosque"),
                (LegacyCustomizationKind.Mosquito, "purple", "mosquito.color", "fantasia")
            }.Select(item => new LegacyCustomizationMapping { Kind = item.Item1, LegacyId = item.Item2, SlotId = item.Item3, OptionId = item.Item4 }).ToArray();
            Assert.That(provider.TryResolve(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Human.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "LMS_Mosquito.prefab"), out var runtime, out var reason), Is.True, reason);
            var outfit = runtime.Snapshot.DefaultSelection();
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Human, "human.pajama"), Is.True, "pajama colour with the pajama");
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Mosquito, "mosquito.accent_color"), Is.False, "no markings");
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Human, "human.hair_color"), Is.True, "the host hair always shows");
            outfit.Human.SetOption("human.bottom", "jean");
            outfit.Human.SetOption("human.top", "buzo");
            outfit.Mosquito.SetOption("mosquito.markings", "anillos");
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Human, "human.pajama"), Is.False, "pajama colour with jeans");
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Human, "human.top_color"), Is.True, "the hoodie takes the top colour");
            Assert.That(runtime.ColorSlotApplies(outfit, CustomizationRole.Mosquito, "mosquito.accent_color"), Is.True, "rings");
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

        /// <summary>Largest distance from the vertices of a baked part to the host head's skin triangles.</summary>
        private static float FarthestFromSkin(SkinnedMeshRenderer part, SkinnedMeshRenderer head)
        {
            int skin = Array.FindIndex(head.sharedMaterials, m => m && m.name == "Human_Skin");
            Assert.That(skin, Is.GreaterThanOrEqualTo(0));
            var headMesh = new Mesh();
            var partMesh = new Mesh();
            try
            {
                head.BakeMesh(headMesh, true);
                part.BakeMesh(partMesh, true);
                var headVertices = headMesh.vertices.Select(v => head.transform.TransformPoint(v)).ToArray();
                var triangles = headMesh.GetTriangles(skin);
                float farthest = 0f;
                foreach (var vertex in partMesh.vertices)
                {
                    var p = part.transform.TransformPoint(vertex);
                    float nearest = float.MaxValue;
                    for (int i = 0; i < triangles.Length; i += 3)
                        nearest = Mathf.Min(nearest, (ClosestOnTriangle(p, headVertices[triangles[i]], headVertices[triangles[i + 1]],
                            headVertices[triangles[i + 2]]) - p).magnitude);
                    farthest = Mathf.Max(farthest, nearest);
                }
                return farthest;
            }
            finally { Object.DestroyImmediate(headMesh); Object.DestroyImmediate(partMesh); }
        }

        private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denominator = 1f / (va + vb + vc);
            return a + ab * (vb * denominator) + ac * (vc * denominator);
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
