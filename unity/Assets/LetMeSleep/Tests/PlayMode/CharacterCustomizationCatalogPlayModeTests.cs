using System;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class CharacterCustomizationCatalogPlayModeTests
    {
        private CharacterCustomizationCatalog catalog;
        private GameObject humanAsset;
        private GameObject mosquitoAsset;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<CharacterCustomizationCatalog>();
            humanAsset = new GameObject("Synthetic human catalog asset");
            mosquitoAsset = new GameObject("Synthetic mosquito catalog asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (catalog) Object.DestroyImmediate(catalog);
            if (humanAsset) Object.DestroyImmediate(humanAsset);
            if (mosquitoAsset) Object.DestroyImmediate(mosquitoAsset);
        }

        [Test]
        public void EmptyAssetIsValidButInactiveAndCreatesNoProductionOptions()
        {
            catalog.CatalogId = "lms.v020.empty";
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(snapshot.RuntimeReady, Is.False);
            Assert.That(snapshot.Slots, Is.Empty);
        }

        [Test]
        public void ImportedBaseAssetsActivateSnapshotAndExposeAssetLookup()
        {
            ConfigureBases();
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(snapshot.RuntimeReady, Is.True);
            Assert.That(catalog.TryGetAssets("human.base", "human-a", out var asset, out var thumbnail), Is.True);
            Assert.That(asset, Is.SameAs(humanAsset));
            Assert.That(thumbnail, Is.Null);
        }

        [Test]
        public void MissingVisualAssetRejectsAuthoredOptionRatherThanShowingPlaceholder()
        {
            ConfigureBases();
            catalog.Options[0].RuntimeAsset = null;
            Assert.That(catalog.TryCreateSnapshot(out _, out var errors), Is.False);
            Assert.That(string.Join("\n", errors), Does.Contain("imported runtime asset"));
        }

        [Test]
        public void InvalidNoneWireValueCannotCollapseToTheZeroSentinel()
        {
            ConfigureBases();
            catalog.Slots = new[]
            {
                catalog.Slots[0], catalog.Slots[1],
                new CharacterCustomizationCatalog.SlotDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.hair", WireSlotId = 3, AllowsNone = true, DefaultOptionId = "none" }
            };
            catalog.Options = new[]
            {
                catalog.Options[0], catalog.Options[1],
                new CharacterCustomizationCatalog.OptionDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.hair", OptionId = "none", WireOptionId = -1, Kind = CustomizationOptionKind.None }
            };
            Assert.That(catalog.TryCreateSnapshot(out _, out var errors), Is.False);
            Assert.That(string.Join("\n", errors), Does.Contain("between 0 and 65534"));
            Assert.That(string.Join("\n", errors), Does.Contain("65535 is reserved"));
        }

        [Test]
        public void TextureCannotActivateAVisualBaseOption()
        {
            ConfigureBases();
            var texture = new Texture2D(1, 1);
            try
            {
                catalog.Options[0].RuntimeAsset = texture;
                Assert.That(catalog.TryCreateSnapshot(out _, out var errors), Is.False);
                Assert.That(string.Join("\n", errors), Does.Contain("must be a GameObject"));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private void ConfigureBases()
        {
            catalog.CatalogId = "lms.v020.synthetic"; catalog.Revision = 1;
            catalog.Slots = new[]
            {
                new CharacterCustomizationCatalog.SlotDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.base", WireSlotId = 1, Required = true, IsBaseSlot = true, DefaultOptionId = "human-a" },
                new CharacterCustomizationCatalog.SlotDefinition { Role = CustomizationRole.Mosquito,
                    SlotId = "mosquito.base", WireSlotId = 2, Required = true, IsBaseSlot = true, DefaultOptionId = "mosquito-a" }
            };
            catalog.Options = new[]
            {
                new CharacterCustomizationCatalog.OptionDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.base", OptionId = "human-a", WireOptionId = 1,
                    Kind = CustomizationOptionKind.SkinnedPart, AssetId = "synthetic-human", RuntimeAsset = humanAsset },
                new CharacterCustomizationCatalog.OptionDefinition { Role = CustomizationRole.Mosquito,
                    SlotId = "mosquito.base", OptionId = "mosquito-a", WireOptionId = 1,
                    Kind = CustomizationOptionKind.SkinnedPart, AssetId = "synthetic-mosquito", RuntimeAsset = mosquitoAsset }
            };
        }
    }
}
