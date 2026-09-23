using System;
using System.IO;
using System.Linq;
using LetMeSleep.Audio;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private AlfaSettingsDraft settings = new AlfaSettingsDraft { MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .85f,
            VoiceVolume = .8f, PushToTalkBinding = "<Keyboard>/v",
            HumanSensitivity = 1f, MosquitoSensitivity = 1f, FullScreen = true, VSync = false, FrameLimit = 0 };
        private BasicCustomizationDraft appearance = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
        // The local draft is durable but never sent to peers. appearance remains the published value.
        private BasicCustomizationDraft localAppearanceDraft = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
        private BasicCustomizationDraft previewAppearance;
        private GameObject previousPreview;
        private Resolution[] resolutions;
        private PreferenceFileStore preferenceStore;
        private int appliedWidth, appliedHeight;
        private bool appliedFullscreen;
        private bool preserveLaunchVideoChoice;
        private AlfaSettingsDraft launchSavedVideoSettings;
        private int launchSavedWidth, launchSavedHeight;
        // Defaults (warm/blue/red) equal the v0.3.0 sketch palette authored in the character audits
        // (Human_Skin #C98B5A, Human_Pajamas #2D4F9A, Mosquito_Shell #9E2228); IDs and wire format unchanged.
        // v0.3.0 UI stage 3 (UI-06 5-6, PER-04): six skin tones, nine clothes colours (the pajama trousers; the
        // shirt stays cream and the nightcap red) and eight mosquito colours. The original ids keep their values;
        // the new ids (fair, brown; sky, orange, pink, gray; forest, sand, ice, toxic) have no legacy modular
        // mapping, so a draft using them simply is not migrated to a modular catalogue (TryMigrateLegacyPreferences).
        private static readonly NamedColorOption[] Skins = {
            new NamedColorOption("fair", "Muy claro", new Color(.941f,.788f,.627f)), new NamedColorOption("light", "Claro", new Color(.91f,.7f,.5f)),
            new NamedColorOption("warm", "Cálido", new Color(.788f,.545f,.353f)), new NamedColorOption("tan", "Bronce", new Color(.54f,.29f,.16f)),
            new NamedColorOption("brown", "Morena", new Color(.4f,.216f,.125f)), new NamedColorOption("dark", "Oscuro", new Color(.27f,.12f,.07f)) };
        private static readonly NamedColorOption[] Pajamas = {
            new NamedColorOption("blue", "Azul", new Color(.176f,.31f,.604f)), new NamedColorOption("sky", "Celeste", new Color(.361f,.561f,.839f)),
            new NamedColorOption("green", "Verde", new Color(.16f,.4f,.27f)), new NamedColorOption("red", "Rojo", new Color(.65f,.17f,.16f)),
            new NamedColorOption("purple", "Violeta", new Color(.4f,.22f,.56f)), new NamedColorOption("yellow", "Mostaza", new Color(.72f,.54f,.18f)),
            new NamedColorOption("orange", "Naranja", new Color(.816f,.4f,.165f)), new NamedColorOption("pink", "Rosa", new Color(.78f,.357f,.541f)),
            new NamedColorOption("gray", "Gris", new Color(.357f,.384f,.439f)) };
        private static readonly NamedColorOption[] MosquitoColors = {
            new NamedColorOption("red", "Rojo", new Color(.62f,.133f,.157f)), new NamedColorOption("blue", "Azul", new Color(.17f,.3f,.52f)),
            new NamedColorOption("green", "Oliva", new Color(.31f,.36f,.18f)), new NamedColorOption("purple", "Violeta", new Color(.37f,.2f,.43f)),
            new NamedColorOption("forest", "Bosque", new Color(.184f,.42f,.227f)), new NamedColorOption("sand", "Desierto", new Color(.722f,.537f,.29f)),
            new NamedColorOption("ice", "Hielo", new Color(.498f,.655f,.788f)), new NamedColorOption("toxic", "Tóxico", new Color(.486f,.702f,.259f)) };
        private void LoadPreferences()
        {
            settings.QualityIndex = Math.Max(0,Array.IndexOf(QualitySettings.names,"PC"));
            resolutions = Screen.resolutions.GroupBy(r => new { r.width, r.height }).Select(g => g.Last()).ToArray();
            // Preserve the actual launch/window size, including command-line test dimensions.
            appliedWidth = Screen.width > 0 ? Screen.width : Screen.currentResolution.width;
            appliedHeight = Screen.height > 0 ? Screen.height : Screen.currentResolution.height;
            appliedFullscreen = Screen.fullScreen;
            int currentIndex = Array.FindIndex(resolutions, r => r.width == appliedWidth && r.height == appliedHeight);
            if (currentIndex < 0)
            {
                currentIndex = resolutions.Length;
                resolutions = resolutions.Concat(new[] { new Resolution { width = appliedWidth, height = appliedHeight } }).ToArray();
            }
            settings.ResolutionIndex = currentIndex;
            settings.FullScreen = appliedFullscreen;
            bool loadedSettings = false, migrateResolution = false, migrateLocalAppearance = false;
            ResolveModularCustomizationRuntime();
            try
            {
                string preferencePath = Path.Combine(DataPath, "preferences.json");
                if (DetectStoredPreferenceSchema(preferencePath) == 2)
                { loadedPreferenceSchema = 2; preferenceWritesBlocked = true; }
                preferenceStore = new PreferenceFileStore(preferencePath, ClassifyPreferences);
                string json = preferenceStore.Load();
                if (preferenceStore.WriteBlocked)
                {
                    preferenceWritesBlocked = true;
                    saveError = "Los ajustes son de otra versión. Se conservaron sin modificar.";
                }
                else if (preferenceStore.RecoveredFromBackup) saveError = "Se recuperaron los ajustes de la copia de respaldo.";
                if (json != null)
                {
                    var header = JsonUtility.FromJson<PreferencesHeader>(json);
                    if (header != null && header.schema == 2 && PreferenceSchemaCodec.TryReadV2(json, out var modularData))
                    {
                        loadedPreferenceSchema = 2;
                        if (modularData.settings != null)
                        {
                            settings = Sanitize(modularData.settings);
                            settings.ResolutionIndex = ResolveResolutionIndex(resolutions, modularData.resolutionWidth,
                                modularData.resolutionHeight, modularData.settings.ResolutionIndex, currentIndex);
                            loadedSettings = true;
                        }
                        if (!string.IsNullOrWhiteSpace(modularData.playerName) && modularData.playerName.Length <= 24)
                            playerName = modularData.playerName;
                        if (!TryActivateModularPreferences(modularData, out string modularError))
                        {
                            preferenceWritesBlocked = true;
                            saveError = "La personalización modular no está disponible. El archivo se conservará sin modificar.";
                            customizationMessage = saveError;
                            if (!string.IsNullOrEmpty(modularError)) Debug.LogWarning("Modular preferences unavailable: " + modularError);
                        }
                    }
                    else
                    {
                        var data = JsonUtility.FromJson<PreferencesV1>(json);
                        if (data.settings != null)
                        {
                            settings = Sanitize(data.settings);
                            settings.ResolutionIndex = ResolveResolutionIndex(resolutions, data.resolutionWidth,
                                data.resolutionHeight, data.settings.ResolutionIndex, currentIndex);
                            loadedSettings = true;
                            migrateResolution = data.resolutionWidth == 0 && data.resolutionHeight == 0;
                        }
                        if (ValidAppearance(data.appearance)) appearance = data.appearance;
                        if (ValidAppearance(data.localAppearanceDraft)) localAppearanceDraft = data.localAppearanceDraft;
                        else { localAppearanceDraft = appearance.Copy(); migrateLocalAppearance = true; }
                        if (!string.IsNullOrWhiteSpace(data.playerName) && data.playerName.Length <= 24) playerName = data.playerName;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { Debug.LogWarning("Preferences could not be loaded; using defaults."); }
            if (loadedSettings && HasVideoLaunchOverride(Environment.GetCommandLineArgs()))
            {
                // Launch overrides are temporary. Audio/cosmetic saves must not replace the user's video choice.
                launchSavedVideoSettings = settings.Copy();
                var savedResolution = resolutions[settings.ResolutionIndex];
                launchSavedWidth = savedResolution.width; launchSavedHeight = savedResolution.height;
                preserveLaunchVideoChoice = true;
                settings.ResolutionIndex = currentIndex; settings.FullScreen = appliedFullscreen;
            }
            bool migratedToModular = loadedPreferenceSchema == 1 && TryMigrateLegacyPreferences();
            // Upgrade schema-1 additive fields once through the existing atomic/backup store.
            if (!migratedToModular && (migrateResolution || migrateLocalAppearance) && !preferenceStore.WriteBlocked)
            {
                string loadNotice = saveError;
                if (SavePreferences()) saveError = loadNotice;
            }
            // A restored private selection must drive the first preview too; otherwise the UI
            // says draft while the 3D view still falls back to the published appearance.
            previewAppearance = localAppearanceDraft.Copy();
            previousPreview = null;
            if (string.IsNullOrEmpty(customizationMessage)) customizationMessage = saveError;
        }

        // Pure resolution policy: exercised externally without Unity/native execution.
        private static int ResolveResolutionIndex(Resolution[] catalog, int width, int height, int legacyIndex, int currentIndex)
        {
            if (width > 0 && height > 0)
            {
                int exact = Array.FindIndex(catalog, r => r.width == width && r.height == height);
                return exact >= 0 ? exact : currentIndex;
            }
            return width == 0 && height == 0 && legacyIndex >= 0 && legacyIndex < catalog.Length
                ? legacyIndex : currentIndex;
        }
        private static bool HasVideoLaunchOverride(string[] args)
            => args.Any(arg => new[] { "-screen-width", "-screen-height", "-screen-fullscreen", "-window-mode", "-monitor" }
                .Any(flag => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)));
        private static bool VideoRequestChanged(int width, int height, bool fullscreen, int previousWidth, int previousHeight, bool previousFullscreen)
            => width != previousWidth || height != previousHeight || fullscreen != previousFullscreen;
        // End pure resolution policy.
        private string saveError = "";
        private string customizationMessage = "";
        private static PreferenceDocumentKind ClassifyPreferences(string json)
            => PreferenceSchemaCodec.Classify(json);
        private bool SavePreferences()
        {
            try {
            if (preferenceStore == null) preferenceStore = new PreferenceFileStore(Path.Combine(DataPath, "preferences.json"), ClassifyPreferences);
            if (preferenceStore.WriteBlocked || preferenceWritesBlocked) { saveError = loadedPreferenceSchema == 2
                ? "La personalización modular no está disponible. El archivo se conservará sin modificar."
                : "Los ajustes son de otra versión. Se conservaron sin modificar."; return false; }
            var storedSettings = settings.Copy();
            var storedResolution = resolutions[settings.ResolutionIndex];
            int storedWidth = storedResolution.width, storedHeight = storedResolution.height;
            if (preserveLaunchVideoChoice)
            {
                storedSettings.ResolutionIndex = launchSavedVideoSettings.ResolutionIndex;
                storedSettings.FullScreen = launchSavedVideoSettings.FullScreen;
                storedWidth = launchSavedWidth; storedHeight = launchSavedHeight;
            }
            if (loadedPreferenceSchema == 2)
            {
                if (!TryWriteModularPreferences(storedSettings, storedWidth, storedHeight)) return false;
            }
            else preferenceStore.Save(JsonUtility.ToJson(new PreferencesV1 { schema = 1, playerName = playerName,
                    settings = storedSettings, appearance = appearance, localAppearanceDraft = localAppearanceDraft,
                    resolutionWidth = storedWidth, resolutionHeight = storedHeight }, true));
            saveError = ""; return true;
            } catch (InvalidDataException) { saveError = "No se guardaron ajustes: el archivo pertenece a otra versión o no es válido."; return false; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { saveError = "No se pudo guardar. Revisá el acceso a la carpeta y volvé a intentar."; return false; }
        }
        private void PresentPreferences()
        {
            ui.PresentSettings(new SettingsUiState(settings, settings, resolutions.Select(r => r.width + " × " + r.height),
                QualitySettings.names, true, true, message: saveError,
                supportsReducedMenuMotion: livingMenu && livingMenu.IsConfigured,
                voiceDevices: VoiceMicrophoneCapture.Devices));
            PresentVoiceBinding();
            if (TryCreateModularUiState(out var modularState)) ui.PresentCustomization(modularState);
            else ui.PresentCustomization(new CustomizationUiState(Skins, Pajamas, MosquitoColors, appearance, localAppearanceDraft,
                isSaving: false, message: customizationMessage, isReadOnly: preferenceWritesBlocked));
        }
        public void ApplySettings(AlfaSettingsDraft draft)
        {
            var previous = settings;
            bool previousPreserve = preserveLaunchVideoChoice;
            settings = Sanitize(draft);
            if (settings.ResolutionIndex != previous.ResolutionIndex || settings.FullScreen != previous.FullScreen)
                preserveLaunchVideoChoice = false;
            if (!SavePreferences()) { settings = previous; preserveLaunchVideoChoice = previousPreserve; }
            ApplySettingsValues(); PresentPreferences();
        }
        private static float FiniteClamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        private AlfaSettingsDraft Sanitize(AlfaSettingsDraft draft)
        {
            var value = draft.Copy(); value.MasterVolume = FiniteClamp(value.MasterVolume,0,1,.8f);
            value.MusicVolume = FiniteClamp(value.MusicVolume,0,1,.5f); value.EffectsVolume = FiniteClamp(value.EffectsVolume,0,1,.85f);
            if (string.IsNullOrWhiteSpace(value.PushToTalkBinding))
            {
                value.PushToTalkBinding = "<Keyboard>/v";
                value.VoiceVolume = .8f; // additive schema-1 migration
            }
            else value.VoiceVolume = FiniteClamp(value.VoiceVolume,0,1,.8f);
            value.VoiceDevice = value.VoiceDevice ?? string.Empty;
            value.HumanSensitivity = FiniteClamp(value.HumanSensitivity,.1f,2f,1f);
            value.MosquitoSensitivity = FiniteClamp(value.MosquitoSensitivity,.1f,2f,1f);
            value.ResolutionIndex = Mathf.Clamp(value.ResolutionIndex,0,Math.Max(0,resolutions.Length-1));
            value.QualityIndex = Mathf.Clamp(value.QualityIndex,0,QualitySettings.names.Length-1);
            if (!new[]{0,30,60,90,120,144,165,240}.Contains(value.FrameLimit)) value.FrameLimit = 0;
            return value;
        }
        private void ApplySettingsValues()
        {
            if (livingMenu) livingMenu.SetReducedMotion(settings.ReduceMenuMotion);
            QualitySettings.SetQualityLevel(settings.QualityIndex, true);
            QualitySettings.vSyncCount = settings.VSync ? 1 : 0; Application.targetFrameRate = settings.FrameLimit == 0 ? -1 : settings.FrameLimit;
            if (resolutions.Length > 0)
            {
                var r = resolutions[settings.ResolutionIndex];
                if (VideoRequestChanged(r.width, r.height, settings.FullScreen, appliedWidth, appliedHeight, appliedFullscreen))
                {
                    Screen.SetResolution(r.width,r.height,settings.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                    appliedWidth = r.width; appliedHeight = r.height; appliedFullscreen = settings.FullScreen;
                }
            }
            SetVolume("Master",settings.MasterVolume); SetVolume("Music",settings.MusicVolume);
            SetVolume("Voice", settings.VoiceVolume); ApplyVoicePreferences();
            foreach (var category in new[]{"Ambience","Character","Critical","Mosquito","World","UI"}) SetVolume(category, settings.EffectsVolume);
            if (game) { game.MouseSensitivity = .002f * (game.LatestSnapshot?.Actors.FirstOrDefault(a=>a.ActorId==game.LocalActorId)?.Role == Core.PlayerRole.Mosquito ? settings.MosquitoSensitivity : settings.HumanSensitivity); game.InvertY = settings.InvertY; }
        }
        private void SetVolume(string category,float volume) { if (Mixer) Mixer.SetFloat(category+"Volume",volume<=.0001f ? -80 : 20*Mathf.Log10(volume)); }
        public void PreviewCustomization(BasicCustomizationDraft draft)
        {
            if (loadedPreferenceSchema != 1 || preferenceWritesBlocked) { customizationMessage = string.IsNullOrEmpty(saveError)
                ? "La edición básica no está disponible para este archivo modular." : saveError; PresentPreferences(); return; }
            if (!ValidAppearance(draft)) return;
            var previous = localAppearanceDraft;
            localAppearanceDraft = draft.Copy();
            if (!SavePreferences())
            {
                localAppearanceDraft = previous;
                previewAppearance = previous.Copy();
                customizationMessage = saveError;
            }
            else
            {
                previewAppearance = localAppearanceDraft.Copy();
                customizationMessage = "Guardado localmente. Aplicá para mostrarlo en la sala.";
            }
            previousPreview = null;
            PresentPreferences();
        }
        public void SaveCustomization(BasicCustomizationDraft draft)
        {
            if (loadedPreferenceSchema != 1 || preferenceWritesBlocked) { customizationMessage = string.IsNullOrEmpty(saveError)
                ? "La edición básica no está disponible para este archivo modular." : saveError; PresentPreferences(); return; }
            if (!ValidAppearance(draft)) return;
            var previousAppearance = appearance;
            var previousLocalDraft = localAppearanceDraft;
            appearance = draft.Copy();
            localAppearanceDraft = draft.Copy();
            if (!SavePreferences())
            {
                appearance = previousAppearance;
                localAppearanceDraft = previousLocalDraft;
                previewAppearance = previousLocalDraft.Copy();
                customizationMessage = saveError;
            }
            else
            {
                previewAppearance = localAppearanceDraft.Copy();
                customizationMessage = "Apariencia aplicada. Ya se mostrará en la sala.";
                appearanceAt = 0;
            }
            previousPreview = null;
            PresentPreferences();
        }
        private static bool ValidAppearance(BasicCustomizationDraft draft)
            => draft != null && (draft.Role == AlfaRole.Human || draft.Role == AlfaRole.Mosquito) && Skins.Any(c=>c.Id==draft.SkinColorId) && Pajamas.Any(c=>c.Id==draft.PajamaColorId) && MosquitoColors.Any(c=>c.Id==draft.MosquitoColorId);
        private void ApplyPreviewColors()
        {
            var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true); var instance=orbit?.CurrentInstance;
            if (!instance || instance == previousPreview) return;
            PreparePreviewInstance(instance);
            if (loadedPreferenceSchema == 2 && modularCustomizationRuntime != null)
            {
                if (TryApplyModularPreview(instance, previewModularAppearance ?? localModularAppearanceDraft,
                    modularEditedRole, out string error)) previousPreview = instance;
                else
                {
                    if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Modular preview application failed: " + error);
                    customizationMessage = "No se pudo actualizar la vista previa modular.";
                }
                return;
            }
            previousPreview=instance;
            ApplyAppearance(instance.GetComponent<CharacterView>(),previewAppearance??appearance);
        }
        /// <summary>The viewer clone renders on the preview layer only, without colliders. A modular preview can be
        /// applied to a clone the same frame the viewer creates it (switching the role tab), before ApplyPreviewColors
        /// ran; its parts copy the clone's layer, so the layer is set before any application.</summary>
        private static void PreparePreviewInstance(GameObject instance)
        {
            foreach(var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
        }
        private static void ApplyAppearance(CharacterView view, BasicCustomizationDraft draft)
        {
            if (!view || !ValidAppearance(draft)) return;
            view.SetSkinColor(Skins.First(c=>c.Id==draft.SkinColorId).Color);
            view.SetPajamaColor(Pajamas.First(c=>c.Id==draft.PajamaColorId).Color);
            view.SetMosquitoColor(MosquitoColors.First(c=>c.Id==draft.MosquitoColorId).Color);
        }
    }
}


