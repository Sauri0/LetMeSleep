using System;
using System.Linq;
using LetMeSleep.Core.Customization;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class CustomizationCatalogContractTests
    {
        [Test]
        public void AppearanceSelectionCopiesDeeplyAndEqualityIsCanonical()
        {
            var first = new AppearanceSelection(
                new AppearanceLoadout(new[] { Pick("human.hair", "none"), Pick("human.base", "human-a") }),
                new AppearanceLoadout(new[] { Pick("mosquito.base", "mosquito-a") }));
            var reordered = new AppearanceSelection(
                new AppearanceLoadout(new[] { Pick("human.base", "human-a"), Pick("human.hair", "none") }),
                new AppearanceLoadout(new[] { Pick("mosquito.base", "mosquito-a") }));

            var copy = first.Copy();
            Assert.That(first.CanonicalEquals(reordered), Is.True);
            Assert.That(first.CanonicalEquals(copy), Is.True);
            copy.Human.SetOption("human.hair", "short");
            Assert.That(first.Human.OptionFor("human.hair"), Is.EqualTo("none"));
            Assert.That(first.CanonicalEquals(copy), Is.False);
        }

        [Test]
        public void SnapshotNormalizesDefaultsAndRoundTripsWireCodes()
        {
            var snapshot = ValidSnapshot();
            var source = new AppearanceSelection(new AppearanceLoadout(), new AppearanceLoadout());

            Assert.That(snapshot.TryNormalize(source, out var normalized, out var error), Is.True, error);
            Assert.That(normalized.Human.OptionFor("human.base"), Is.EqualTo("human-a"));
            Assert.That(normalized.Human.OptionFor("human.hair"), Is.EqualTo("none"));
            Assert.That(normalized.Mosquito.OptionFor("mosquito.base"), Is.EqualTo("mosquito-a"));
            Assert.That(snapshot.TryEncode("human.hair", "none", out byte slot, out ushort option), Is.True);
            Assert.That(option, Is.Zero);
            Assert.That(snapshot.TryDecode(slot, option, out string slotId, out string optionId), Is.True);
            Assert.That(slotId, Is.EqualTo("human.hair"));
            Assert.That(optionId, Is.EqualTo("none"));
        }

        [Test]
        public void FingerprintIsDeterministicAcrossAuthoringOrderAndExcludesLabels()
        {
            var slots = Slots(); var options = Options();
            slots.First(item => item.SlotId == "human.base").Label = "Body";
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, slots, options,
                out var first, out var firstErrors), Is.True, string.Join("; ", firstErrors));
            foreach (var slot in slots) slot.Label = "Translated " + slot.SlotId;
            foreach (var option in options) option.Label = "Translated " + option.OptionId;
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, slots.Reverse(), options.Reverse(),
                out var second, out var secondErrors), Is.True, string.Join("; ", secondErrors));

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.NetworkFingerprint, Is.EqualTo(first.NetworkFingerprint));
            Assert.That(first.TrySlot("human.base", out var firstBase), Is.True);
            Assert.That(second.TrySlot("human.base", out var secondBase), Is.True);
            Assert.That(firstBase.Label, Is.EqualTo("Body"));
            Assert.That(secondBase.Label, Is.EqualTo("Translated human.base"));
            slots.First(item => item.SlotId == "human.base").Label = "Mutated after snapshot";
            Assert.That(secondBase.Label, Is.EqualTo("Translated human.base"));
        }

        [Test]
        public void MissingSlotLabelRemainsEmptyInsteadOfExposingTechnicalId()
        {
            var snapshot = ValidSnapshot();
            Assert.That(snapshot.TrySlot("human.hair", out var slot), Is.True);
            Assert.That(slot.Label, Is.Empty);
        }

        [Test]
        public void CompatibilityUsesBaseTagsAndExplicitIncompatibleSlots()
        {
            var snapshot = ValidSnapshot();
            var incompatibleBase = snapshot.DefaultSelection();
            incompatibleBase.Human.SetOption("human.hair", "long");
            Assert.That(snapshot.TryNormalize(incompatibleBase, out _, out string baseError), Is.False);
            Assert.That(baseError, Does.Contain("base"));

            var incompatibleTags = snapshot.DefaultSelection();
            incompatibleTags.Human.SetOption("human.top", "top-a");
            incompatibleTags.Human.SetOption("human.bottom", "bottom-b");
            Assert.That(snapshot.TryNormalize(incompatibleTags, out _, out string tagError), Is.False);
            Assert.That(tagError, Does.Contain("tags"));

            var incompatibleSlots = snapshot.DefaultSelection();
            incompatibleSlots.Human.SetOption("human.outfit", "full-a");
            incompatibleSlots.Human.SetOption("human.top", "top-a");
            Assert.That(snapshot.TryNormalize(incompatibleSlots, out _, out string slotError), Is.False);
            Assert.That(slotError, Does.Contain("incompatible slots"));
        }

        [Test]
        public void InvalidIdsCodesDefaultsAndMissingAssetsAreRejected()
        {
            var slots = Slots().Concat(new[]
            {
                Slot(CustomizationRole.Human, "Human Base", 7, true, false, false, "invalid-a")
            }).ToArray();
            var options = Options().Concat(new[]
            {
                Visual(CustomizationRole.Human, "Human Base", "invalid-a", 1, "asset-invalid")
            }).ToArray();
            slots[1].WireSlotId = 25;
            options.First(item => item.OptionId == "human-a").HasRuntimeAsset = false;
            options.First(item => item.OptionId == "none").WireOptionId = 4;
            options.First(item => item.OptionId == "human-b").WireOptionId = ushort.MaxValue;

            Assert.That(CustomizationCatalogSnapshot.TryCreate("BAD CATALOG", 0, slots, options,
                out _, out var errors), Is.False);
            string joined = string.Join("\n", errors);
            Assert.That(joined, Does.Contain("CatalogId"));
            Assert.That(joined, Does.Contain("Revision"));
            Assert.That(joined, Does.Contain("Invalid slot ID"));
            Assert.That(joined, Does.Contain("between 1 and 24"));
            Assert.That(joined, Does.Contain("imported runtime asset"));
            Assert.That(joined, Does.Contain("reserved none"));
            Assert.That(joined, Does.Contain("65535 is reserved"));
        }

        [Test]
        public void IdentifiersRejectTrailingNewlinesAndRequiredSlotsRejectExplicitNone()
        {
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.valid\n", 1, Slots(), Options(),
                out _, out var idErrors), Is.False);
            Assert.That(string.Join("\n", idErrors), Does.Contain("CatalogId"));

            var slots = Slots().ToArray(); var options = Options().ToArray();
            var requiredSlot = slots.First(item => item.SlotId == "human.top");
            requiredSlot.Required = true;
            requiredSlot.DefaultOptionId = "top-a";
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, slots, options,
                out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            var selection = snapshot.DefaultSelection();
            selection.Human.SetOption("human.top", "none");
            Assert.That(snapshot.TryNormalize(selection, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("Required slot"));
        }

        [Test]
        public void IncompatibleDefaultsRejectTheCatalogBeforeRuntimeActivation()
        {
            var slots = Slots().ToArray();
            slots.First(item => item.SlotId == "human.top").DefaultOptionId = "top-a";
            slots.First(item => item.SlotId == "human.bottom").DefaultOptionId = "bottom-b";
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, slots, Options(),
                out _, out var errors), Is.False);
            Assert.That(string.Join("\n", errors), Does.Contain("Default selection is invalid"));
        }

        [Test]
        public void UnknownKindsAndNonVisualBaseOptionsAreRejected()
        {
            var unknown = Options().ToArray();
            unknown.First(item => item.OptionId == "short").Kind = (CustomizationOptionKind)99;
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, Slots(), unknown,
                out _, out var unknownErrors), Is.False);
            Assert.That(string.Join("\n", unknownErrors), Does.Contain("Invalid option kind"));

            var colorBase = Options().ToArray();
            var humanBase = colorBase.First(item => item.OptionId == "human-a");
            humanBase.Kind = CustomizationOptionKind.Color;
            humanBase.HasSwatch = true;
            humanBase.HasRuntimeAsset = false;
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, Slots(), colorBase,
                out _, out var baseErrors), Is.False);
            Assert.That(string.Join("\n", baseErrors), Does.Contain("Base slot options"));
        }

        [Test]
        public void UnknownDuplicateAndCrossRoleSelectionsAreRejectedWithoutMutation()
        {
            var snapshot = ValidSnapshot();
            var duplicate = snapshot.DefaultSelection();
            duplicate.Human.Selections = duplicate.Human.Selections.Concat(new[] { Pick("human.base", "human-a") }).ToArray();
            var before = duplicate.Copy();
            Assert.That(snapshot.TryNormalize(duplicate, out _, out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("duplicate"));
            Assert.That(duplicate.CanonicalEquals(before), Is.True);

            var crossRole = snapshot.DefaultSelection();
            crossRole.Human.Selections = new[] { Pick("mosquito.base", "mosquito-a") };
            Assert.That(snapshot.TryNormalize(crossRole, out _, out string crossError), Is.False);
            Assert.That(crossError, Does.Contain("cross-role"));
        }

        [Test]
        public void RuntimeReadyRequiresImportedDefaultBaseForBothRoles()
        {
            var slots = Slots(); var options = Options().ToArray();
            options.First(item => item.OptionId == "mosquito-a").HasRuntimeAsset = false;
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, slots, options,
                out _, out var errors), Is.False);
            Assert.That(string.Join("\n", errors), Does.Contain("imported runtime asset"));

            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.empty", 1,
                Array.Empty<CustomizationSlotRecord>(), Array.Empty<CustomizationOptionRecord>(),
                out var empty, out var emptyErrors), Is.True, string.Join("; ", emptyErrors));
            Assert.That(empty.RuntimeReady, Is.False);
        }

        private static CustomizationCatalogSnapshot ValidSnapshot()
        {
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.v020.characters", 1, Slots(), Options(),
                out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(snapshot.RuntimeReady, Is.True);
            return snapshot;
        }

        private static CustomizationSlotRecord[] Slots() => new[]
        {
            Slot(CustomizationRole.Human, "human.base", 1, true, false, true, "human-a"),
            Slot(CustomizationRole.Human, "human.hair", 2, false, true, false, "none"),
            Slot(CustomizationRole.Human, "human.top", 3, false, true, false, "none", "outfit-pair"),
            Slot(CustomizationRole.Human, "human.bottom", 4, false, true, false, "none", "outfit-pair"),
            Slot(CustomizationRole.Human, "human.outfit", 5, false, true, false, "none"),
            Slot(CustomizationRole.Mosquito, "mosquito.base", 6, true, false, true, "mosquito-a")
        };

        private static CustomizationOptionRecord[] Options() => new[]
        {
            Visual(CustomizationRole.Human, "human.base", "human-a", 1, "asset-human-a"),
            Visual(CustomizationRole.Human, "human.base", "human-b", 2, "asset-human-b"),
            None(CustomizationRole.Human, "human.hair"),
            Visual(CustomizationRole.Human, "human.hair", "short", 1, "asset-short", new[] { "human-a" }),
            Visual(CustomizationRole.Human, "human.hair", "long", 2, "asset-long", new[] { "human-b" }),
            None(CustomizationRole.Human, "human.top"),
            Visual(CustomizationRole.Human, "human.top", "top-a", 1, "asset-top-a", tags: new[] { "set-a" }, incompatible: new[] { "human.outfit" }),
            None(CustomizationRole.Human, "human.bottom"),
            Visual(CustomizationRole.Human, "human.bottom", "bottom-a", 1, "asset-bottom-a", tags: new[] { "set-a" }),
            Visual(CustomizationRole.Human, "human.bottom", "bottom-b", 2, "asset-bottom-b", tags: new[] { "set-b" }),
            None(CustomizationRole.Human, "human.outfit"),
            Visual(CustomizationRole.Human, "human.outfit", "full-a", 1, "asset-full-a", incompatible: new[] { "human.top", "human.bottom" }),
            Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", 1, "asset-mosquito-a")
        };

        private static CustomizationSlotRecord Slot(CustomizationRole role, string id, byte wire, bool required,
            bool none, bool isBase, string defaultOption, string family = "") => new CustomizationSlotRecord
        { Role = role, SlotId = id, WireSlotId = wire, Required = required, AllowsNone = none,
            IsBaseSlot = isBase, DefaultOptionId = defaultOption, CompatibilityFamily = family };

        private static CustomizationOptionRecord None(CustomizationRole role, string slot) => new CustomizationOptionRecord
        { Role = role, SlotId = slot, OptionId = "none", Kind = CustomizationOptionKind.None, WireOptionId = 0 };

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slot, string id, ushort wire,
            string asset, string[] bases = null, string[] tags = null, string[] incompatible = null) => new CustomizationOptionRecord
        { Role = role, SlotId = slot, OptionId = id, Kind = CustomizationOptionKind.SkinnedPart,
            WireOptionId = wire, AssetId = asset, HasRuntimeAsset = true,
            CompatibleBaseOptionIds = bases ?? Array.Empty<string>(), CompatibilityTags = tags ?? Array.Empty<string>(),
            IncompatibleSlotIds = incompatible ?? Array.Empty<string>() };

        private static AppearanceSlotSelection Pick(string slot, string option) => new AppearanceSlotSelection(slot, option);
    }
}
