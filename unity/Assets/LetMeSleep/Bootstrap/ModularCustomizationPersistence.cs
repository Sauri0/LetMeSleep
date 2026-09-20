using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    [Serializable]
    internal sealed class PreferencesHeader
    {
        public int schema;
    }

    [Serializable]
    internal sealed class PreferencesV1
    {
        public int schema;
        public string playerName;
        public AlfaSettingsDraft settings;
        public BasicCustomizationDraft appearance;
        public BasicCustomizationDraft localAppearanceDraft;
        public int resolutionWidth;
        public int resolutionHeight;
    }

    [Serializable]
    internal sealed class PreferencesV2
    {
        public int schema;
        public string playerName;
        public AlfaSettingsDraft settings;
        public AppearanceSelection publishedAppearance;
        public AppearanceSelection localAppearanceDraft;
        public int resolutionWidth;
        public int resolutionHeight;
    }

    public static class PreferenceSchemaCodec
    {
        public static PreferenceDocumentKind Classify(string json)
        {
            try
            {
                var header = JsonUtility.FromJson<PreferencesHeader>(json);
                if (header == null || header.schema < 1) return PreferenceDocumentKind.Invalid;
                if (header.schema == 1) return JsonUtility.FromJson<PreferencesV1>(json) != null
                    ? PreferenceDocumentKind.Current : PreferenceDocumentKind.Invalid;
                if (header.schema > 2) return PreferenceDocumentKind.UnsupportedVersion;
                return TryReadV2(json, out _) ? PreferenceDocumentKind.Current : PreferenceDocumentKind.UnsupportedVersion;
            }
            catch (ArgumentException) { return PreferenceDocumentKind.Invalid; }
        }

        public static bool IsExactV2RoundTrip(string json)
            => TryReadV2(json, out _);

        internal static bool TryReadV2(string json, out PreferencesV2 document)
        {
            document = null;
            try
            {
                var parsed = JsonUtility.FromJson<PreferencesV2>(json);
                if (!StructurallyComplete(parsed)) return false;
                string serialized = JsonUtility.ToJson(parsed, false);
                if (!string.Equals(Compact(json), Compact(serialized), StringComparison.Ordinal)) return false;
                document = parsed;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        internal static string WriteV2(PreferencesV2 document, bool pretty = true)
        {
            if (!StructurallyComplete(document)) throw new ArgumentException("Schema 2 preferences are incomplete.", nameof(document));
            return JsonUtility.ToJson(document, pretty);
        }

        private static bool StructurallyComplete(PreferencesV2 value)
        {
            return value != null && value.schema == 2 && value.settings != null
                && Complete(value.publishedAppearance) && Complete(value.localAppearanceDraft);
        }

        private static bool Complete(AppearanceSelection value)
        {
            return value != null && value.Schema == AppearanceSelection.CurrentSchema
                && value.Human != null && value.Human.Selections != null
                && value.Mosquito != null && value.Mosquito.Selections != null;
        }

        private static string Compact(string json)
        {
            if (json == null) return string.Empty;
            var result = new StringBuilder(json.Length);
            bool quoted = false, escaped = false;
            foreach (char value in json)
            {
                if (quoted)
                {
                    result.Append(value);
                    if (escaped) escaped = false;
                    else if (value == '\\') escaped = true;
                    else if (value == '"') quoted = false;
                }
                else if (value == '"') { quoted = true; result.Append(value); }
                else if (!char.IsWhiteSpace(value)) result.Append(value);
            }
            return result.ToString();
        }
    }

    public enum LegacyCustomizationKind : byte { Skin, Pajama, Mosquito }

    [Serializable]
    public sealed class LegacyCustomizationMapping
    {
        public LegacyCustomizationKind Kind;
        public string LegacyId;
        public string SlotId;
        public string OptionId;
    }

    public sealed class LegacyAppearanceMapper
    {
        private static readonly string[] SkinIds = { "light", "warm", "tan", "dark" };
        private static readonly string[] PajamaIds = { "blue", "red", "green", "purple", "yellow" };
        private static readonly string[] MosquitoIds = { "red", "blue", "green", "purple" };
        private readonly Dictionary<string, LegacyCustomizationMapping> mappings;

        private LegacyAppearanceMapper(IEnumerable<LegacyCustomizationMapping> source)
        {
            mappings = source.Select(item => new LegacyCustomizationMapping
            {
                Kind = item.Kind,
                LegacyId = item.LegacyId,
                SlotId = item.SlotId,
                OptionId = item.OptionId
            }).ToDictionary(Key, item => item, StringComparer.Ordinal);
        }

        public static bool TryCreate(IEnumerable<LegacyCustomizationMapping> source,
            CustomizationCatalogSnapshot snapshot, out LegacyAppearanceMapper mapper, out string error)
        {
            mapper = null; error = string.Empty;
            if (snapshot == null || !snapshot.RuntimeReady)
            { error = "The modular catalog is unavailable."; return false; }
            var items = (source ?? Enumerable.Empty<LegacyCustomizationMapping>()).ToArray();
            if (items.Any(item => item == null))
            { error = "Legacy mappings contain a null entry."; return false; }
            if (items.GroupBy(Key, StringComparer.Ordinal).Any(group => group.Count() != 1))
            { error = "Legacy mappings contain duplicate entries."; return false; }

            var expected = ExpectedKeys().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var actual = items.Select(Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            { error = "Legacy mappings must cover exactly the 13 supported colour IDs."; return false; }

            var categorySlots = items.GroupBy(item => item.Kind).ToDictionary(group => group.Key,
                group => group.Select(item => item.SlotId ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray());
            if (categorySlots.Any(item => item.Value.Length != 1)
                || string.Equals(categorySlots[LegacyCustomizationKind.Skin][0],
                    categorySlots[LegacyCustomizationKind.Pajama][0], StringComparison.Ordinal))
            { error = "Each legacy colour category needs one distinct destination slot."; return false; }
            if (items.GroupBy(item => item.Kind).Any(group => group.Select(item => item.OptionId ?? string.Empty)
                .Distinct(StringComparer.Ordinal).Count() != group.Count()))
            { error = "Each legacy colour needs a distinct destination option within its category."; return false; }

            foreach (var item in items)
            {
                CustomizationRole role = item.Kind == LegacyCustomizationKind.Mosquito
                    ? CustomizationRole.Mosquito : CustomizationRole.Human;
                if (!snapshot.TrySlot(item.SlotId, out var slot) || slot.Role != role
                    || !slot.TryOption(item.OptionId, out var option) || option.Kind != CustomizationOptionKind.Color
                    || !snapshot.TryEncode(item.SlotId, item.OptionId, out _, out _))
                { error = "Legacy mapping references an unknown or cross-role option: " + Key(item); return false; }
            }

            mapper = new LegacyAppearanceMapper(items);
            foreach (var sample in AllLegacySamples())
                if (!mapper.TryMap(sample, snapshot, out _, out error)) { mapper = null; return false; }
            return true;
        }

        public bool TryMap(BasicCustomizationDraft legacy, CustomizationCatalogSnapshot snapshot,
            out AppearanceSelection selection, out string error)
        {
            selection = null; error = string.Empty;
            if (legacy == null || snapshot == null || !snapshot.RuntimeReady)
            { error = "Legacy appearance or modular catalog is unavailable."; return false; }
            var candidate = snapshot.DefaultSelection();
            if (!Set(candidate, LegacyCustomizationKind.Skin, legacy.SkinColorId, CustomizationRole.Human, out error)
                || !Set(candidate, LegacyCustomizationKind.Pajama, legacy.PajamaColorId, CustomizationRole.Human, out error)
                || !Set(candidate, LegacyCustomizationKind.Mosquito, legacy.MosquitoColorId, CustomizationRole.Mosquito, out error)) return false;
            return snapshot.TryNormalize(candidate, out selection, out error);
        }

        private bool Set(AppearanceSelection selection, LegacyCustomizationKind kind, string legacyId,
            CustomizationRole role, out string error)
        {
            error = string.Empty;
            if (!mappings.TryGetValue(Key(kind, legacyId), out var mapping))
            { error = "Legacy colour has no explicit replacement: " + legacyId; return false; }
            selection.For(role).SetOption(mapping.SlotId, mapping.OptionId);
            return true;
        }

        private static IEnumerable<string> ExpectedKeys()
        {
            return SkinIds.Select(id => Key(LegacyCustomizationKind.Skin, id))
                .Concat(PajamaIds.Select(id => Key(LegacyCustomizationKind.Pajama, id)))
                .Concat(MosquitoIds.Select(id => Key(LegacyCustomizationKind.Mosquito, id)));
        }

        private static IEnumerable<BasicCustomizationDraft> AllLegacySamples()
        {
            foreach (string skin in SkinIds)
                foreach (string pajama in PajamaIds)
                    foreach (string mosquito in MosquitoIds)
                        yield return new BasicCustomizationDraft(AlfaRole.Human, skin, pajama, mosquito);
        }

        private static string Key(LegacyCustomizationMapping item) => Key(item.Kind, item.LegacyId);
        private static string Key(LegacyCustomizationKind kind, string id) => ((byte)kind) + ":" + (id ?? string.Empty);
    }
}
