#if UNITY_EDITOR
using System;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEngine;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class ModularPersistenceContractPlayModeTests
    {
        [Serializable]
        private sealed class V2Fixture
        {
            public int schema = 2;
            public string playerName = "Prueba";
            public AlfaSettingsDraft settings = Settings();
            public AppearanceSelection publishedAppearance = EmptySelection();
            public AppearanceSelection localAppearanceDraft = EmptySelection();
            public int resolutionWidth = 1280;
            public int resolutionHeight = 720;
        }

        [Test]
        public void SchemaClassifierAcceptsOnlyExactKnownV2Shape()
        {
            string exact = JsonUtility.ToJson(new V2Fixture(), true);
            Assert.That(PreferenceSchemaCodec.Classify("{\"schema\":1}"), Is.EqualTo(PreferenceDocumentKind.Current));
            Assert.That(PreferenceSchemaCodec.IsExactV2RoundTrip(exact), Is.True);
            Assert.That(PreferenceSchemaCodec.Classify(exact), Is.EqualTo(PreferenceDocumentKind.Current));

            string unknown = exact.TrimEnd().TrimEnd('}') + ",\"futureField\":true}";
            Assert.That(PreferenceSchemaCodec.IsExactV2RoundTrip(unknown), Is.False);
            Assert.That(PreferenceSchemaCodec.Classify(unknown), Is.EqualTo(PreferenceDocumentKind.UnsupportedVersion));
            Assert.That(PreferenceSchemaCodec.Classify("{\"schema\":3}"), Is.EqualTo(PreferenceDocumentKind.UnsupportedVersion));
        }

        [Test]
        public void CompleteLegacyMapMigratesPublishedAndPrivateSelectionsIndependently()
        {
            var snapshot = Snapshot();
            var mappings = Mappings();
            Assert.That(LegacyAppearanceMapper.TryCreate(mappings, snapshot, out var mapper, out var error), Is.True, error);

            Assert.That(mapper.TryMap(new BasicCustomizationDraft(AlfaRole.Human, "light", "red", "green"),
                snapshot, out var published, out error), Is.True, error);
            Assert.That(mapper.TryMap(new BasicCustomizationDraft(AlfaRole.Mosquito, "dark", "yellow", "purple"),
                snapshot, out var draft, out error), Is.True, error);
            Assert.That(published.Human.OptionFor("human.skin"), Is.EqualTo("skin-light"));
            Assert.That(published.Human.OptionFor("human.pajama"), Is.EqualTo("pajama-red"));
            Assert.That(published.Mosquito.OptionFor("mosquito.color"), Is.EqualTo("mosquito-green"));
            Assert.That(draft.Human.OptionFor("human.skin"), Is.EqualTo("skin-dark"));
            Assert.That(draft.Human.OptionFor("human.pajama"), Is.EqualTo("pajama-yellow"));
            Assert.That(draft.Mosquito.OptionFor("mosquito.color"), Is.EqualTo("mosquito-purple"));

            mappings[0].OptionId = "skin-dark";
            Assert.That(mapper.TryMap(new BasicCustomizationDraft(AlfaRole.Human, "light", "blue", "red"),
                snapshot, out var afterMutation, out error), Is.True, error);
            Assert.That(afterMutation.Human.OptionFor("human.skin"), Is.EqualTo("skin-light"), "Mapper must own immutable copies.");
        }

        [Test]
        public void IncompleteOrOverlappingLegacyMapCannotEnableMigration()
        {
            var snapshot = Snapshot();
            var incomplete = Mappings().Take(12).ToArray();
            Assert.That(LegacyAppearanceMapper.TryCreate(incomplete, snapshot, out _, out var incompleteError), Is.False);
            Assert.That(incompleteError, Does.Contain("13"));

            var overlapping = Mappings();
            foreach (var item in overlapping.Where(item => item.Kind == LegacyCustomizationKind.Pajama))
            {
                item.SlotId = "human.skin";
                item.OptionId = "skin-" + item.LegacyId;
            }
            Assert.That(LegacyAppearanceMapper.TryCreate(overlapping, snapshot, out _, out var overlapError), Is.False);
            Assert.That(overlapError, Does.Contain("distinct"));

            var collapsing = Mappings();
            collapsing.Single(item => item.Kind == LegacyCustomizationKind.Skin && item.LegacyId == "light").OptionId = "skin-warm";
            Assert.That(LegacyAppearanceMapper.TryCreate(collapsing, snapshot, out _, out var collapseError), Is.False);
            Assert.That(collapseError, Does.Contain("distinct destination option"));
        }

        [Test]
        public void RuntimeProviderRequiresTheApplicationPrefabsItWillActuallyCustomize()
        {
            var owner = new GameObject("Synthetic modular provider");
            try
            {
                var provider = owner.AddComponent<ModularCustomizationRuntimeProvider>();
                Assert.That(provider.TryResolve(null, null, out _, out var reason), Is.False);
                Assert.That(reason, Does.Contain("application human and mosquito prefabs"));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static CustomizationCatalogSnapshot Snapshot()
        {
            var slots = new[]
            {
                Slot(CustomizationRole.Human, "human.base", 1, "human-a", true),
                Slot(CustomizationRole.Mosquito, "mosquito.base", 2, "mosquito-a", true),
                Slot(CustomizationRole.Human, "human.skin", 3, "skin-warm"),
                Slot(CustomizationRole.Human, "human.pajama", 4, "pajama-blue"),
                Slot(CustomizationRole.Mosquito, "mosquito.color", 5, "mosquito-red")
            };
            var options = new[]
            {
                Visual(CustomizationRole.Human, "human.base", "human-a", 1),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", 1)
            }.Concat(new[] { "light", "warm", "tan", "dark", "blue", "red", "green", "purple", "yellow" }
                .Distinct(StringComparer.Ordinal).Select((id, index) => Color(CustomizationRole.Human, "human.skin", "skin-" + id, (ushort)(index + 1))))
            .Concat(new[] { "blue", "red", "green", "purple", "yellow" }
                .Select((id, index) => Color(CustomizationRole.Human, "human.pajama", "pajama-" + id, (ushort)(index + 1))))
            .Concat(new[] { "red", "blue", "green", "purple" }
                .Select((id, index) => Color(CustomizationRole.Mosquito, "mosquito.color", "mosquito-" + id, (ushort)(index + 1))))
            .ToArray();
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.persistence.fixture", 1, slots, options,
                out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            return snapshot;
        }

        private static LegacyCustomizationMapping[] Mappings()
        {
            return new[] { "light", "warm", "tan", "dark" }.Select(id => Mapping(LegacyCustomizationKind.Skin, id, "human.skin", "skin-" + id))
                .Concat(new[] { "blue", "red", "green", "purple", "yellow" }.Select(id => Mapping(LegacyCustomizationKind.Pajama, id, "human.pajama", "pajama-" + id)))
                .Concat(new[] { "red", "blue", "green", "purple" }.Select(id => Mapping(LegacyCustomizationKind.Mosquito, id, "mosquito.color", "mosquito-" + id)))
                .ToArray();
        }

        private static CustomizationSlotRecord Slot(CustomizationRole role, string id, byte wire, string defaultOption, bool isBase = false)
            => new CustomizationSlotRecord { Role = role, SlotId = id, WireSlotId = wire, Required = true,
                IsBaseSlot = isBase, DefaultOptionId = defaultOption };

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slot, string id, ushort wire)
            => new CustomizationOptionRecord { Role = role, SlotId = slot, OptionId = id, WireOptionId = wire,
                Kind = CustomizationOptionKind.SkinnedPart, AssetId = "asset-" + id, HasRuntimeAsset = true };

        private static CustomizationOptionRecord Color(CustomizationRole role, string slot, string id, ushort wire)
            => new CustomizationOptionRecord { Role = role, SlotId = slot, OptionId = id, WireOptionId = wire,
                Kind = CustomizationOptionKind.Color, HasSwatch = true, SwatchRgba = 0xffffffff };

        private static LegacyCustomizationMapping Mapping(LegacyCustomizationKind kind, string legacy, string slot, string option)
            => new LegacyCustomizationMapping { Kind = kind, LegacyId = legacy, SlotId = slot, OptionId = option };

        private static AppearanceSelection EmptySelection()
            => new AppearanceSelection(new AppearanceLoadout(), new AppearanceLoadout());

        private static AlfaSettingsDraft Settings()
            => new AlfaSettingsDraft { MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .8f,
                VoiceVolume = .8f, PushToTalkBinding = "<Keyboard>/v", HumanSensitivity = 1f, MosquitoSensitivity = 1f };
    }
}
#endif
