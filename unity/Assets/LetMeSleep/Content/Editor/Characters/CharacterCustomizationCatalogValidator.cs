using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core.Customization;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Content.Characters.Editor
{
    public static class CharacterCustomizationCatalogValidator
    {
        public static bool Validate(CharacterCustomizationCatalog catalog,
            out CustomizationCatalogSnapshot snapshot, out string[] errors)
        {
            snapshot = null;
            if (catalog == null) { errors = new[] { "Catalog is required." }; return false; }
            var issues = new List<string>();
            if (!catalog.TryCreateSnapshot(out snapshot, out var contractErrors)) issues.AddRange(contractErrors);
            foreach (var option in catalog.Options ?? Array.Empty<CharacterCustomizationCatalog.OptionDefinition>())
            {
                if (option == null || option.Kind == CustomizationOptionKind.None || option.Kind == CustomizationOptionKind.Color) continue;
                string path = AssetDatabase.GetAssetPath(option.RuntimeAsset);
                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid))
                    issues.Add("Visual option is not a project asset: " + option.OptionId);
                else if (!string.Equals(option.AssetId, guid, StringComparison.OrdinalIgnoreCase))
                    issues.Add("AssetId must equal the project GUID for option " + option.OptionId + ".");
            }
            errors = issues.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (errors.Length != 0) snapshot = null;
            return errors.Length == 0;
        }
    }

    [CustomEditor(typeof(CharacterCustomizationCatalog))]
    internal sealed class CharacterCustomizationCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var catalog = (CharacterCustomizationCatalog)target;
            if (CharacterCustomizationCatalogValidator.Validate(catalog, out var snapshot, out var errors))
                EditorGUILayout.HelpBox(snapshot.RuntimeReady ?
                    "Catalog valid and runtime-ready. Fingerprint: " + snapshot.Fingerprint :
                    "Catalog valid but inactive: approved human and mosquito base assets are required.",
                    snapshot.RuntimeReady ? MessageType.Info : MessageType.Warning);
            else foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
