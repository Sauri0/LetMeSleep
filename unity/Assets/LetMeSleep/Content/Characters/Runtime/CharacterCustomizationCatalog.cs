using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core.Customization;
using UnityEngine;

namespace LetMeSleep.Content.Characters
{
    [CreateAssetMenu(menuName = "Let Me Sleep/Characters/Customization Catalog", fileName = "CharacterCustomizationCatalog")]
    public sealed class CharacterCustomizationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class SlotDefinition
        {
            public CustomizationRole Role;
            public string SlotId;
            [Range(1, CustomizationCatalogSnapshot.MaximumSelectedSlots)] public int WireSlotId;
            public bool Required;
            public bool AllowsNone;
            public bool IsBaseSlot;
            public string DefaultOptionId;
            public string CompatibilityFamily;
        }

        [Serializable]
        public sealed class OptionDefinition
        {
            public CustomizationRole Role;
            public string SlotId;
            public string OptionId;
            [Range(0, 65534)] public int WireOptionId;
            public CustomizationOptionKind Kind;
            public string Label;
            public bool HasSwatch;
            public Color32 Swatch = new Color32(255, 255, 255, 255);
            [Tooltip("Stable GUID of RuntimeAsset. Required for visual options.")]
            public string AssetId;
            public UnityEngine.Object RuntimeAsset;
            public Sprite Thumbnail;
            public string[] CompatibleBaseOptionIds = Array.Empty<string>();
            public string[] CompatibilityTags = Array.Empty<string>();
            public string[] IncompatibleSlotIds = Array.Empty<string>();
        }

        public string CatalogId;
        [Min(1)] public int Revision = 1;
        public SlotDefinition[] Slots = Array.Empty<SlotDefinition>();
        public OptionDefinition[] Options = Array.Empty<OptionDefinition>();

        public bool TryCreateSnapshot(out CustomizationCatalogSnapshot snapshot, out string[] errors)
        {
            var authoringErrors = new List<string>();
            var slotRecords = (Slots ?? Array.Empty<SlotDefinition>()).Where(item => item != null).Select(item =>
                new CustomizationSlotRecord
                {
                    Role = item.Role,
                    SlotId = item.SlotId,
                    WireSlotId = item.WireSlotId >= byte.MinValue && item.WireSlotId <= byte.MaxValue ? (byte)item.WireSlotId : (byte)0,
                    Required = item.Required,
                    AllowsNone = item.AllowsNone,
                    IsBaseSlot = item.IsBaseSlot,
                    DefaultOptionId = item.DefaultOptionId,
                    CompatibilityFamily = item.CompatibilityFamily
                });
            var optionRecords = (Options ?? Array.Empty<OptionDefinition>()).Where(item => item != null).Select(item =>
            {
                bool wireValid = item.WireOptionId >= 0 && item.WireOptionId <= 65534;
                if (!wireValid) authoringErrors.Add("Wire option ID must be between 0 and 65534: " + item.OptionId);
                bool visual = item.Kind == CustomizationOptionKind.SkinnedPart ||
                              item.Kind == CustomizationOptionKind.SocketPart || item.Kind == CustomizationOptionKind.Composite;
                if (visual && item.RuntimeAsset != null && !(item.RuntimeAsset is GameObject))
                    authoringErrors.Add("Visual option RuntimeAsset must be a GameObject: " + item.OptionId);
                if (!visual && item.RuntimeAsset != null)
                    authoringErrors.Add("None and Color options cannot use RuntimeAsset: " + item.OptionId);
                return new CustomizationOptionRecord
                {
                    Role = item.Role,
                    SlotId = item.SlotId,
                    OptionId = item.OptionId,
                    // Preserve invalid inspector/programmatic values as the reserved sentinel.
                    // Mapping them to zero would turn an invalid `none` into a valid one.
                    WireOptionId = wireValid ? (ushort)item.WireOptionId : ushort.MaxValue,
                    Kind = item.Kind,
                    Label = item.Label,
                    HasSwatch = item.HasSwatch,
                    SwatchRgba = Pack(item.Swatch),
                    AssetId = item.AssetId,
                    HasRuntimeAsset = item.RuntimeAsset != null,
                    CompatibleBaseOptionIds = item.CompatibleBaseOptionIds,
                    CompatibilityTags = item.CompatibilityTags,
                    IncompatibleSlotIds = item.IncompatibleSlotIds
                };
            }).ToArray();
            bool coreValid = CustomizationCatalogSnapshot.TryCreate(CatalogId, Revision, slotRecords, optionRecords,
                out snapshot, out var coreErrors);
            errors = authoringErrors.Concat(coreErrors).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!coreValid || errors.Length != 0) snapshot = null;
            return snapshot != null;
        }

        public bool TryGetAssets(string slotId, string optionId, out UnityEngine.Object runtimeAsset, out Sprite thumbnail)
        {
            var result = (Options ?? Array.Empty<OptionDefinition>()).FirstOrDefault(item => item != null &&
                string.Equals(item.SlotId, slotId, StringComparison.Ordinal) &&
                string.Equals(item.OptionId, optionId, StringComparison.Ordinal));
            runtimeAsset = result?.RuntimeAsset; thumbnail = result?.Thumbnail; return result != null;
        }

        private static uint Pack(Color32 value) =>
            ((uint)value.r << 24) | ((uint)value.g << 16) | ((uint)value.b << 8) | value.a;
    }
}
