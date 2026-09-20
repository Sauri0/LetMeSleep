using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LetMeSleep.Core.Customization
{
    public enum CustomizationOptionKind : byte { None = 0, Color = 1, SkinnedPart = 2, SocketPart = 3, Composite = 4 }

    public sealed class CustomizationSlotRecord
    {
        public CustomizationRole Role;
        public string SlotId;
        public string Label;
        public byte WireSlotId;
        public bool Required;
        public bool AllowsNone;
        public bool IsBaseSlot;
        public string DefaultOptionId;
        public string CompatibilityFamily;
    }

    public sealed class CustomizationOptionRecord
    {
        public CustomizationRole Role;
        public string SlotId;
        public string OptionId;
        public ushort WireOptionId;
        public CustomizationOptionKind Kind;
        public string Label;
        public bool HasSwatch;
        public uint SwatchRgba;
        public string AssetId;
        public bool HasRuntimeAsset;
        public string[] CompatibleBaseOptionIds = Array.Empty<string>();
        public string[] CompatibilityTags = Array.Empty<string>();
        public string[] IncompatibleSlotIds = Array.Empty<string>();
    }

    public sealed class CustomizationSlotSnapshot
    {
        private readonly Dictionary<string, CustomizationOptionSnapshot> options;
        internal CustomizationSlotSnapshot(CustomizationSlotRecord source,
            IEnumerable<CustomizationOptionSnapshot> optionItems)
        {
            Role = source.Role; SlotId = source.SlotId; Label = source.Label ?? string.Empty;
            WireSlotId = source.WireSlotId;
            Required = source.Required; AllowsNone = source.AllowsNone; IsBaseSlot = source.IsBaseSlot;
            DefaultOptionId = source.DefaultOptionId; CompatibilityFamily = source.CompatibilityFamily ?? string.Empty;
            options = optionItems.ToDictionary(item => item.OptionId, StringComparer.Ordinal);
            Options = Array.AsReadOnly(options.Values.OrderBy(item => item.WireOptionId).ToArray());
        }
        public CustomizationRole Role { get; }
        public string SlotId { get; }
        // Presentation metadata only. Empty means the UI must not expose this category yet;
        // SlotId remains a technical identifier and is never used as a display fallback.
        public string Label { get; }
        public byte WireSlotId { get; }
        public bool Required { get; }
        public bool AllowsNone { get; }
        public bool IsBaseSlot { get; }
        public string DefaultOptionId { get; }
        public string CompatibilityFamily { get; }
        public IReadOnlyList<CustomizationOptionSnapshot> Options { get; }
        public bool TryOption(string optionId, out CustomizationOptionSnapshot option) =>
            options.TryGetValue(optionId ?? string.Empty, out option);
    }

    public sealed class CustomizationOptionSnapshot
    {
        internal CustomizationOptionSnapshot(CustomizationOptionRecord source)
        {
            Role = source.Role; SlotId = source.SlotId; OptionId = source.OptionId;
            WireOptionId = source.WireOptionId; Kind = source.Kind; Label = source.Label ?? source.OptionId;
            HasSwatch = source.HasSwatch; SwatchRgba = source.SwatchRgba;
            AssetId = source.AssetId ?? string.Empty; HasRuntimeAsset = source.HasRuntimeAsset;
            CompatibleBaseOptionIds = Array.AsReadOnly(Clean(source.CompatibleBaseOptionIds));
            CompatibilityTags = Array.AsReadOnly(Clean(source.CompatibilityTags));
            IncompatibleSlotIds = Array.AsReadOnly(Clean(source.IncompatibleSlotIds));
        }
        public CustomizationRole Role { get; }
        public string SlotId { get; }
        public string OptionId { get; }
        public ushort WireOptionId { get; }
        public CustomizationOptionKind Kind { get; }
        public string Label { get; }
        public bool HasSwatch { get; }
        public uint SwatchRgba { get; }
        public string AssetId { get; }
        public bool HasRuntimeAsset { get; }
        public IReadOnlyList<string> CompatibleBaseOptionIds { get; }
        public IReadOnlyList<string> CompatibilityTags { get; }
        public IReadOnlyList<string> IncompatibleSlotIds { get; }
        private static string[] Clean(IEnumerable<string> source) =>
            (source ?? Enumerable.Empty<string>()).Where(value => value != null)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class CustomizationCatalogSnapshot
    {
        public const int MaximumSelectedSlots = 24;
        private static readonly Regex CatalogIdPattern = new Regex("\\A[a-z][a-z0-9_.-]{0,31}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex SlotIdPattern = new Regex("\\A[a-z][a-z0-9_.]{0,31}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex OptionIdPattern = new Regex("\\A[a-z][a-z0-9_.-]{0,47}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex TagPattern = new Regex("\\A[a-z][a-z0-9_.-]{0,31}\\z", RegexOptions.CultureInvariant);
        private readonly Dictionary<string, CustomizationSlotSnapshot> slots;
        private readonly Dictionary<byte, CustomizationSlotSnapshot> slotsByWire;

        private CustomizationCatalogSnapshot(string catalogId, int revision,
            IEnumerable<CustomizationSlotSnapshot> slotItems, bool runtimeReady, string fingerprint)
        {
            CatalogId = catalogId; Revision = revision; RuntimeReady = runtimeReady; Fingerprint = fingerprint;
            NetworkFingerprint = ulong.Parse(fingerprint.Substring(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            slots = slotItems.ToDictionary(item => item.SlotId, StringComparer.Ordinal);
            slotsByWire = slots.Values.ToDictionary(item => item.WireSlotId);
            Slots = Array.AsReadOnly(slots.Values.OrderBy(item => item.WireSlotId).ToArray());
        }

        public string CatalogId { get; }
        public int Revision { get; }
        // Data-contract gate only. It proves that both roles have referenced base assets;
        // it does not certify rig binding, first person, animation or artistic approval.
        public bool RuntimeReady { get; }
        public string Fingerprint { get; }
        public ulong NetworkFingerprint { get; }
        public IReadOnlyList<CustomizationSlotSnapshot> Slots { get; }

        public static bool TryCreate(string catalogId, int revision,
            IEnumerable<CustomizationSlotRecord> slotRecords,
            IEnumerable<CustomizationOptionRecord> optionRecords,
            out CustomizationCatalogSnapshot snapshot, out string[] errors)
        {
            snapshot = null; var issues = new List<string>();
            var rawSlots = (slotRecords ?? Enumerable.Empty<CustomizationSlotRecord>()).ToArray();
            var rawOptions = (optionRecords ?? Enumerable.Empty<CustomizationOptionRecord>()).ToArray();
            if (rawSlots.Any(item => item == null)) issues.Add("Catalog contains a null slot definition.");
            if (rawOptions.Any(item => item == null)) issues.Add("Catalog contains a null option definition.");
            var sourceSlots = rawSlots.Where(item => item != null).ToArray();
            var sourceOptions = rawOptions.Where(item => item != null).ToArray();
            if (!Valid(CatalogIdPattern, catalogId)) issues.Add("CatalogId is invalid.");
            if (revision < 1) issues.Add("Revision must be at least 1.");
            if (sourceSlots.Length > MaximumSelectedSlots) issues.Add("Catalog exceeds the 24-slot wire limit.");
            foreach (var group in sourceSlots.GroupBy(item => item.SlotId ?? string.Empty, StringComparer.Ordinal))
                if (group.Count() > 1) issues.Add("Duplicate slot ID: " + group.Key);
            foreach (var group in sourceSlots.GroupBy(item => item.WireSlotId))
                if (group.Key == 0 || group.Key > MaximumSelectedSlots || group.Count() > 1)
                    issues.Add("Wire slot ID must be unique and between 1 and 24: " + group.Key);

            var byId = sourceSlots.Where(item => !string.IsNullOrEmpty(item.SlotId))
                .GroupBy(item => item.SlotId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var slot in sourceSlots)
            {
                if (!ValidRole(slot.Role)) issues.Add("Invalid role for slot " + slot.SlotId + ".");
                if (!Valid(SlotIdPattern, slot.SlotId)) issues.Add("Invalid slot ID: " + slot.SlotId);
                if (!Valid(OptionIdPattern, slot.DefaultOptionId)) issues.Add("Invalid default option for " + slot.SlotId + ".");
                if (!string.IsNullOrEmpty(slot.CompatibilityFamily) && !Valid(TagPattern, slot.CompatibilityFamily))
                    issues.Add("Invalid compatibility family for " + slot.SlotId + ".");
            }
            foreach (var group in sourceSlots.Where(item => item.IsBaseSlot).GroupBy(item => item.Role))
                if (group.Count() > 1) issues.Add("A role may declare at most one base slot: " + group.Key);

            foreach (var option in sourceOptions)
            {
                if (!ValidRole(option.Role) || !byId.TryGetValue(option.SlotId ?? string.Empty, out var slot) || slot.Role != option.Role)
                { issues.Add("Option references an unknown or cross-role slot: " + option.SlotId + "/" + option.OptionId); continue; }
                if (!Valid(OptionIdPattern, option.OptionId)) issues.Add("Invalid option ID: " + option.OptionId);
                if (option.Kind < CustomizationOptionKind.None || option.Kind > CustomizationOptionKind.Composite)
                    issues.Add("Invalid option kind: " + option.OptionId);
                bool none = option.OptionId == "none";
                if (none != (option.Kind == CustomizationOptionKind.None) || (none && (!slot.AllowsNone || option.WireOptionId != 0)))
                    issues.Add("The reserved none option is inconsistent in slot " + slot.SlotId + ".");
                if (!none && option.WireOptionId == 0) issues.Add("Non-none option needs a wire ID: " + option.OptionId);
                if (option.WireOptionId == ushort.MaxValue) issues.Add("Wire option ID 65535 is reserved: " + option.OptionId);
                if (option.Kind == CustomizationOptionKind.Color && !option.HasSwatch)
                    issues.Add("Color option needs a swatch: " + option.OptionId);
                if (option.Kind == CustomizationOptionKind.None && (option.HasRuntimeAsset || !string.IsNullOrEmpty(option.AssetId)))
                    issues.Add("None option cannot reference an asset: " + option.OptionId);
                if (option.Kind != CustomizationOptionKind.None && option.Kind != CustomizationOptionKind.Color && string.IsNullOrEmpty(option.AssetId))
                    issues.Add("Visual option needs a stable AssetId: " + option.OptionId);
                if (option.Kind != CustomizationOptionKind.None && option.Kind != CustomizationOptionKind.Color && !option.HasRuntimeAsset)
                    issues.Add("Visual option needs an imported runtime asset: " + option.OptionId);
                ValidateList(option.CompatibleBaseOptionIds, OptionIdPattern, "compatible base", option.OptionId, issues);
                ValidateList(option.CompatibilityTags, TagPattern, "compatibility tag", option.OptionId, issues);
                ValidateList(option.IncompatibleSlotIds, SlotIdPattern, "incompatible slot", option.OptionId, issues);
            }
            foreach (var group in sourceOptions.GroupBy(item => (item.SlotId ?? string.Empty) + "\u001f" + (item.OptionId ?? string.Empty), StringComparer.Ordinal))
                if (group.Count() > 1) issues.Add("Duplicate option: " + group.Key.Replace("\u001f", "/"));
            foreach (var group in sourceOptions.GroupBy(item => (item.SlotId ?? string.Empty) + "\u001f" + item.WireOptionId, StringComparer.Ordinal))
                if (group.Count() > 1) issues.Add("Duplicate wire option ID in slot: " + group.Key.Replace("\u001f", "/"));

            foreach (var slot in sourceSlots)
            {
                var options = sourceOptions.Where(item => item.SlotId == slot.SlotId).ToArray();
                if (options.Length == 0) issues.Add("Slot has no options: " + slot.SlotId);
                var defaultOption = options.FirstOrDefault(item => item.OptionId == slot.DefaultOptionId);
                if (defaultOption == null) issues.Add("Default option is absent from slot: " + slot.SlotId);
                else if (slot.Required && defaultOption.Kind == CustomizationOptionKind.None)
                    issues.Add("Required slot cannot default to none: " + slot.SlotId);
                if (slot.IsBaseSlot)
                    foreach (var option in options)
                        if (option.Kind != CustomizationOptionKind.SkinnedPart && option.Kind != CustomizationOptionKind.Composite)
                            issues.Add("Base slot options must be SkinnedPart or Composite: " + option.OptionId);
                var baseSlot = sourceSlots.FirstOrDefault(item => item.Role == slot.Role && item.IsBaseSlot);
                var baseIds = baseSlot == null ? new HashSet<string>() : new HashSet<string>(sourceOptions
                    .Where(item => item.SlotId == baseSlot.SlotId).Select(item => item.OptionId), StringComparer.Ordinal);
                foreach (var option in options)
                {
                    foreach (var baseId in option.CompatibleBaseOptionIds ?? Array.Empty<string>())
                        if (!baseIds.Contains(baseId)) issues.Add("Unknown compatible base " + baseId + " for " + option.OptionId + ".");
                    foreach (var incompatible in option.IncompatibleSlotIds ?? Array.Empty<string>())
                        if (!byId.TryGetValue(incompatible, out var other) || other.Role != option.Role || incompatible == slot.SlotId)
                            issues.Add("Invalid incompatible slot " + incompatible + " for " + option.OptionId + ".");
                }
            }

            errors = issues.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (errors.Length != 0) return false;
            var snapshots = sourceSlots.Select(slot => new CustomizationSlotSnapshot(slot, sourceOptions
                .Where(option => option.SlotId == slot.SlotId).Select(option => new CustomizationOptionSnapshot(option)))).ToArray();
            string fingerprint = ComputeFingerprint(catalogId, revision, snapshots);
            var structural = new CustomizationCatalogSnapshot(catalogId, revision, snapshots, false, fingerprint);
            if (!structural.TryNormalize(structural.DefaultSelection(), out _, out string defaultError))
            {
                errors = new[] { "Default selection is invalid: " + defaultError };
                return false;
            }
            bool runtimeReady = new[] { CustomizationRole.Human, CustomizationRole.Mosquito }.All(role =>
            {
                var baseSlot = snapshots.SingleOrDefault(item => item.Role == role && item.IsBaseSlot);
                return baseSlot != null && baseSlot.Required && baseSlot.TryOption(baseSlot.DefaultOptionId, out var value) && value.HasRuntimeAsset;
            });
            snapshot = new CustomizationCatalogSnapshot(catalogId, revision, snapshots, runtimeReady, fingerprint);
            return true;
        }

        public bool TrySlot(string slotId, out CustomizationSlotSnapshot slot) =>
            slots.TryGetValue(slotId ?? string.Empty, out slot);

        public bool TryEncode(string slotId, string optionId, out byte slotCode, out ushort optionCode)
        {
            slotCode = 0; optionCode = 0;
            if (!TrySlot(slotId, out var slot) || !slot.TryOption(optionId, out var option)) return false;
            slotCode = slot.WireSlotId; optionCode = option.WireOptionId; return true;
        }

        public bool TryDecode(byte slotCode, ushort optionCode, out string slotId, out string optionId)
        {
            slotId = null; optionId = null;
            if (!slotsByWire.TryGetValue(slotCode, out var slot)) return false;
            var option = slot.Options.FirstOrDefault(item => item.WireOptionId == optionCode);
            if (option == null) return false;
            slotId = slot.SlotId; optionId = option.OptionId; return true;
        }

        public bool TryNormalize(AppearanceSelection source, out AppearanceSelection normalized, out string error)
        {
            normalized = null; error = string.Empty;
            if (source == null || source.Schema != AppearanceSelection.CurrentSchema)
            { error = "Appearance schema is unsupported."; return false; }
            var human = Normalize(CustomizationRole.Human, source.Human, out error);
            if (human == null) return false;
            var mosquito = Normalize(CustomizationRole.Mosquito, source.Mosquito, out error);
            if (mosquito == null) return false;
            normalized = new AppearanceSelection(human, mosquito); return true;
        }

        public AppearanceSelection DefaultSelection()
        {
            var human = new AppearanceLoadout(Slots.Where(item => item.Role == CustomizationRole.Human)
                .Select(item => new AppearanceSlotSelection(item.SlotId, item.DefaultOptionId)));
            var mosquito = new AppearanceLoadout(Slots.Where(item => item.Role == CustomizationRole.Mosquito)
                .Select(item => new AppearanceSlotSelection(item.SlotId, item.DefaultOptionId)));
            return new AppearanceSelection(human, mosquito);
        }

        private AppearanceLoadout Normalize(CustomizationRole role, AppearanceLoadout source, out string error)
        {
            error = string.Empty; var selected = new Dictionary<string, CustomizationOptionSnapshot>(StringComparer.Ordinal);
            foreach (var item in source?.Selections ?? Array.Empty<AppearanceSlotSelection>())
            {
                if (item == null || !slots.TryGetValue(item.SlotId ?? string.Empty, out var slot) || slot.Role != role)
                { error = "Selection contains an unknown or cross-role slot."; return null; }
                if (selected.ContainsKey(slot.SlotId)) { error = "Selection contains a duplicate slot."; return null; }
                if (!slot.TryOption(item.OptionId, out var option)) { error = "Selection contains an unknown option."; return null; }
                if (slot.Required && option.Kind == CustomizationOptionKind.None)
                { error = "Required slot cannot select none."; return null; }
                selected.Add(slot.SlotId, option);
            }
            foreach (var slot in Slots.Where(item => item.Role == role))
                if (!selected.ContainsKey(slot.SlotId)) selected.Add(slot.SlotId, slot.Options.First(item => item.OptionId == slot.DefaultOptionId));
            var baseOption = selected.Values.FirstOrDefault(item => slots[item.SlotId].IsBaseSlot);
            foreach (var option in selected.Values)
            {
                if (option.CompatibleBaseOptionIds.Count > 0 && (baseOption == null || !option.CompatibleBaseOptionIds.Contains(baseOption.OptionId)))
                { error = "Selection is incompatible with its base."; return null; }
                foreach (var incompatible in option.IncompatibleSlotIds)
                    if (selected.TryGetValue(incompatible, out var other) && other.Kind != CustomizationOptionKind.None)
                    { error = "Selection contains incompatible slots."; return null; }
            }
            foreach (var family in selected.Values.Where(item => item.Kind != CustomizationOptionKind.None)
                         .GroupBy(item => slots[item.SlotId].CompatibilityFamily).Where(group => !string.IsNullOrEmpty(group.Key) && group.Count() > 1))
            {
                HashSet<string> common = null;
                foreach (var option in family)
                {
                    var tags = new HashSet<string>(option.CompatibilityTags, StringComparer.Ordinal);
                    if (tags.Count == 0) { error = "Compatibility family option has no tags."; return null; }
                    if (common == null) common = tags; else common.IntersectWith(tags);
                }
                if (common == null || common.Count == 0) { error = "Selection compatibility tags do not intersect."; return null; }
            }
            return new AppearanceLoadout(selected.OrderBy(pair => slots[pair.Key].WireSlotId)
                .Select(pair => new AppearanceSlotSelection(pair.Key, pair.Value.OptionId)));
        }

        private static string ComputeFingerprint(string catalogId, int revision, IEnumerable<CustomizationSlotSnapshot> source)
        {
            var text = new StringBuilder().Append(catalogId).Append('|').Append(revision).Append('\n');
            foreach (var slot in source.OrderBy(item => item.WireSlotId))
            {
                text.Append((byte)slot.Role).Append('|').Append(slot.WireSlotId).Append('|').Append(slot.SlotId).Append('|')
                    .Append(slot.Required ? 1 : 0).Append('|').Append(slot.AllowsNone ? 1 : 0).Append('|')
                    .Append(slot.IsBaseSlot ? 1 : 0).Append('|').Append(slot.DefaultOptionId).Append('|').Append(slot.CompatibilityFamily).Append('\n');
                foreach (var option in slot.Options.OrderBy(item => item.WireOptionId))
                    text.Append(option.WireOptionId).Append('|').Append(option.OptionId).Append('|').Append((byte)option.Kind).Append('|')
                        .Append(option.HasSwatch ? option.SwatchRgba.ToString("x8", CultureInfo.InvariantCulture) : "-").Append('|')
                        .Append(option.AssetId).Append('|').Append(option.HasRuntimeAsset ? 1 : 0).Append('|')
                        .Append(string.Join(",", option.CompatibleBaseOptionIds)).Append('|')
                        .Append(string.Join(",", option.CompatibilityTags)).Append('|')
                        .Append(string.Join(",", option.IncompatibleSlotIds)).Append('\n');
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Valid(Regex pattern, string value) => value != null && Encoding.UTF8.GetByteCount(value) <= 64 && pattern.IsMatch(value);
        private static bool ValidRole(CustomizationRole role) => role == CustomizationRole.Human || role == CustomizationRole.Mosquito;
        private static void ValidateList(IEnumerable<string> source, Regex pattern, string label, string option, ICollection<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in source ?? Enumerable.Empty<string>())
                if (!Valid(pattern, value) || !seen.Add(value)) errors.Add("Invalid or duplicate " + label + " in " + option + ".");
        }
    }
}
