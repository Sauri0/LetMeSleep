using System;
using System.IO;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication : IModularCustomizationActions
    {
        public ModularCustomizationRuntimeProvider ModularCustomizationProvider;

        private int loadedPreferenceSchema = 1;
        private bool preferenceWritesBlocked;
        private ModularCustomizationRuntime modularCustomizationRuntime;
        private AppearanceSelection publishedModularAppearance;
        private AppearanceSelection localModularAppearanceDraft;
        private AppearanceSelection previewModularAppearance;
        private AlfaRole modularEditedRole = AlfaRole.Human;

        private bool ModularCustomizationAvailable => loadedPreferenceSchema == 2
            && !preferenceWritesBlocked && modularCustomizationRuntime != null
            && publishedModularAppearance != null && localModularAppearanceDraft != null;

        private bool TryGetPublishedModularAppearance(out CharacterCustomizationCatalog catalog,
            out CustomizationCatalogSnapshot snapshot, out AppearanceSelection selection)
        {
            catalog = modularCustomizationRuntime?.Catalog;
            snapshot = modularCustomizationRuntime?.Snapshot;
            selection = ModularCustomizationAvailable ? publishedModularAppearance.Copy() : null;
            return catalog && snapshot != null && selection != null;
        }

        private bool TryApplyModularAppearance(CharacterView view, AppearanceSelection selection,
            CustomizationRole role, out string error)
        {
            error = string.Empty;
            return ModularCustomizationAvailable && modularCustomizationRuntime.TryApply(view, selection, role, out error);
        }

        private static void ClearModularAppearance(CharacterView view)
        {
            if (!view) return;
            var assembler = view.GetComponent<CharacterModularVisualAssembler>();
            if (assembler) assembler.ClearAppliedParts();
        }

        private void ResolveModularCustomizationRuntime()
        {
            modularCustomizationRuntime = null;
            if (ModularCustomizationProvider)
                ModularCustomizationProvider.TryResolve(HumanPrefab, MosquitoPrefab,
                    out modularCustomizationRuntime, out _);
        }

        private static int DetectStoredPreferenceSchema(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 16384) return 0;
                return JsonUtility.FromJson<PreferencesHeader>(File.ReadAllText(path))?.schema ?? 0;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { return 0; }
        }

        private bool TryActivateModularPreferences(PreferencesV2 document, out string error)
        {
            error = string.Empty;
            if (modularCustomizationRuntime == null)
            { error = "Falta un catálogo o host visual modular válido."; return false; }
            var snapshot = modularCustomizationRuntime.Snapshot;
            if (!snapshot.TryNormalize(document.publishedAppearance, out var published, out error)
                || !snapshot.TryNormalize(document.localAppearanceDraft, out var local, out error)
                || !modularCustomizationRuntime.CanApply(published, out error)
                || !modularCustomizationRuntime.CanApply(local, out error)) return false;

            publishedModularAppearance = published;
            localModularAppearanceDraft = local;
            previewModularAppearance = local.Copy();
            preferenceWritesBlocked = false;
            return true;
        }

        private bool TryMigrateLegacyPreferences()
        {
            if (preferenceWritesBlocked || preferenceStore == null || preferenceStore.WriteBlocked
                || modularCustomizationRuntime == null
                || !modularCustomizationRuntime.TryMigrate(appearance, out var published, out _)
                || !modularCustomizationRuntime.TryMigrate(localAppearanceDraft, out var local, out _)
                || !modularCustomizationRuntime.CanApply(published, out _)
                || !modularCustomizationRuntime.CanApply(local, out _)) return false;

            int previousSchema = loadedPreferenceSchema;
            var previousPublished = publishedModularAppearance;
            var previousLocal = localModularAppearanceDraft;
            var previousPreviewSelection = previewModularAppearance;
            loadedPreferenceSchema = 2;
            preferenceWritesBlocked = false;
            publishedModularAppearance = published;
            localModularAppearanceDraft = local;
            previewModularAppearance = local.Copy();
            if (SavePreferences())
            {
                customizationMessage = "La personalización se migró al catálogo modular.";
                return true;
            }

            loadedPreferenceSchema = previousSchema;
            publishedModularAppearance = previousPublished;
            localModularAppearanceDraft = previousLocal;
            previewModularAppearance = previousPreviewSelection;
            return false;
        }

        private bool TryWriteModularPreferences(AlfaSettingsDraft storedSettings, int storedWidth, int storedHeight)
        {
            if (modularCustomizationRuntime == null || publishedModularAppearance == null || localModularAppearanceDraft == null)
            { saveError = "La personalización modular no está disponible. El archivo se conservará sin modificar."; return false; }
            var snapshot = modularCustomizationRuntime.Snapshot;
            if (!snapshot.TryNormalize(publishedModularAppearance, out var published, out string error)
                || !snapshot.TryNormalize(localModularAppearanceDraft, out var local, out error)
                || !modularCustomizationRuntime.CanApply(published, out error)
                || !modularCustomizationRuntime.CanApply(local, out error))
            {
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Modular preference validation failed: " + error);
                saveError = "No se guardó la personalización modular. La selección anterior se conservó.";
                return false;
            }

            var candidate = new PreferencesV2
            {
                schema = 2,
                playerName = playerName,
                settings = storedSettings,
                publishedAppearance = published,
                localAppearanceDraft = local,
                resolutionWidth = storedWidth,
                resolutionHeight = storedHeight
            };
            string json = PreferenceSchemaCodec.WriteV2(candidate, true);
            if (!PreferenceSchemaCodec.IsExactV2RoundTrip(json))
            { saveError = "No se pudo verificar el formato modular; el archivo anterior se conservó."; return false; }
            preferenceStore.Save(json);
            publishedModularAppearance = published;
            localModularAppearanceDraft = local;
            return true;
        }

        private bool TryCreateModularUiState(out CustomizationUiState state)
        {
            state = null;
            if (loadedPreferenceSchema != 2 || preferenceWritesBlocked || modularCustomizationRuntime == null
                || publishedModularAppearance == null || localModularAppearanceDraft == null) return false;
            state = new CustomizationUiState(modularCustomizationRuntime.Snapshot, publishedModularAppearance,
                localModularAppearanceDraft, modularEditedRole, message: customizationMessage,
                modularPreviewAvailable: true,
                thumbnailResolver: (slotId, optionId) => modularCustomizationRuntime.Catalog.TryGetAssets(
                    slotId, optionId, out _, out var thumbnail) ? thumbnail : null);
            return true;
        }

        public void PreviewModularCustomization(AppearanceSelection draft, AlfaRole editedRole)
        {
            if (!TryPrepareModularChange(draft, editedRole, out var normalized, out var instance, out string error))
            {
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Modular preview validation failed: " + error);
                customizationMessage = "No se pudo previsualizar esta selección. La apariencia anterior se conservó.";
                PresentPreferences(); return;
            }
            if (localModularAppearanceDraft != null && normalized.CanonicalEquals(localModularAppearanceDraft))
            {
                modularEditedRole = editedRole;
                previewModularAppearance = normalized.Copy();
                previousPreview = instance ? instance : null;
                PresentPreferences();
                return;
            }
            var previousLocal = localModularAppearanceDraft;
            var previousPreviewSelection = previewModularAppearance;
            var previousRole = modularEditedRole;
            bool previewRestored = true;
            string restoreError = string.Empty;
            localModularAppearanceDraft = normalized;
            previewModularAppearance = normalized.Copy();
            modularEditedRole = editedRole;
            if (!SavePreferences())
            {
                localModularAppearanceDraft = previousLocal;
                previewModularAppearance = previousPreviewSelection;
                modularEditedRole = previousRole;
                if (instance && previousPreviewSelection != null)
                    previewRestored = TryApplyModularPreview(instance, previousPreviewSelection, editedRole, out restoreError);
                if (!previewRestored) Debug.LogWarning("Modular preview rollback failed: " + restoreError);
                customizationMessage = saveError;
            }
            else customizationMessage = "Guardado localmente. Aplicá para mostrarlo en la sala.";
            previousPreview = previewRestored && instance ? instance : null;
            PresentPreferences();
        }

        public void SaveModularCustomization(AppearanceSelection draft, AlfaRole editedRole)
        {
            if (!TryPrepareModularChange(draft, editedRole, out var normalized, out var instance, out string error))
            {
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Modular apply validation failed: " + error);
                customizationMessage = "No se pudo aplicar esta selección. La apariencia anterior se conservó.";
                PresentPreferences(); return;
            }
            var previousPublished = publishedModularAppearance;
            var previousLocal = localModularAppearanceDraft;
            var previousPreviewSelection = previewModularAppearance;
            var previousRole = modularEditedRole;
            bool previewRestored = true;
            string restoreError = string.Empty;
            publishedModularAppearance = normalized;
            localModularAppearanceDraft = normalized.Copy();
            previewModularAppearance = normalized.Copy();
            modularEditedRole = editedRole;
            if (!SavePreferences())
            {
                publishedModularAppearance = previousPublished;
                localModularAppearanceDraft = previousLocal;
                previewModularAppearance = previousPreviewSelection;
                modularEditedRole = previousRole;
                if (instance && previousPreviewSelection != null)
                    previewRestored = TryApplyModularPreview(instance, previousPreviewSelection, editedRole, out restoreError);
                if (!previewRestored) Debug.LogWarning("Modular preview rollback failed: " + restoreError);
                customizationMessage = saveError;
            }
            else
            {
                customizationMessage = "Apariencia aplicada. Ya se mostrará en la sala.";
                appearanceAt = 0;
            }
            previousPreview = previewRestored && instance ? instance : null;
            PresentPreferences();
        }

        private bool TryPrepareModularChange(AppearanceSelection draft, AlfaRole editedRole,
            out AppearanceSelection normalized, out GameObject instance, out string error)
        {
            normalized = null; instance = CurrentPreviewInstance(); error = string.Empty;
            if (loadedPreferenceSchema != 2 || preferenceWritesBlocked || modularCustomizationRuntime == null)
            { error = "La personalización modular no está disponible."; return false; }
            if (editedRole != AlfaRole.Human && editedRole != AlfaRole.Mosquito)
            { error = "La pestaña de personalización no es válida."; return false; }
            if (!modularCustomizationRuntime.Snapshot.TryNormalize(draft, out normalized, out error)
                || !modularCustomizationRuntime.CanApply(normalized, out error)) return false;
            return !instance || TryApplyModularPreview(instance, normalized, editedRole, out error);
        }

        private GameObject CurrentPreviewInstance()
        {
            if (!ui) return null;
            var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true);
            return orbit ? orbit.CurrentInstance : null;
        }

        private bool TryApplyModularPreview(GameObject instance, AppearanceSelection selection,
            AlfaRole editedRole, out string error)
        {
            error = string.Empty;
            if (!instance || selection == null || modularCustomizationRuntime == null) return false;
            var view = UnityComponents.OnSelfOrChildren<CharacterView>(instance);
            var role = editedRole == AlfaRole.Mosquito ? CustomizationRole.Mosquito : CustomizationRole.Human;
            return view && modularCustomizationRuntime.TryApply(view, selection, role, out error);
        }
    }
}
