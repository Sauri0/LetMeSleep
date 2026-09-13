using System;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Characters;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        [Serializable] private sealed class Preferences
        {
            public int schema; public string playerName; public AlfaSettingsDraft settings; public BasicCustomizationDraft appearance;
            // Additive schema-1 fields. Zero/zero identifies a legacy positional selection.
            public int resolutionWidth; public int resolutionHeight;
        }
        private AlfaSettingsDraft settings = new AlfaSettingsDraft { MasterVolume = .8f, MusicVolume = .5f, EffectsVolume = .85f,
            HumanSensitivity = 1f, MosquitoSensitivity = 1f, FullScreen = true, VSync = false, FrameLimit = 0 };
        private BasicCustomizationDraft appearance = new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
        private BasicCustomizationDraft previewAppearance;
        private GameObject previousPreview;
        private Resolution[] resolutions;
        private PreferenceFileStore preferenceStore;
        private int appliedWidth, appliedHeight;
        private bool appliedFullscreen;
        private bool preserveLaunchVideoChoice;
        private AlfaSettingsDraft launchSavedVideoSettings;
        private int launchSavedWidth, launchSavedHeight;
        private static readonly NamedColorOption[] Skins = {
            new NamedColorOption("light", "Claro", new Color(.91f,.7f,.5f)), new NamedColorOption("warm", "Cálido", new Color(.72f,.4f,.25f)),
            new NamedColorOption("tan", "Bronce", new Color(.54f,.29f,.16f)), new NamedColorOption("dark", "Oscuro", new Color(.27f,.12f,.07f)) };
        private static readonly NamedColorOption[] Pajamas = {
            new NamedColorOption("blue", "Azul", new Color(.12f,.32f,.51f)), new NamedColorOption("red", "Rojo", new Color(.65f,.17f,.16f)),
            new NamedColorOption("green", "Verde", new Color(.16f,.4f,.27f)), new NamedColorOption("purple", "Violeta", new Color(.4f,.22f,.56f)),
            new NamedColorOption("yellow", "Mostaza", new Color(.72f,.54f,.18f)) };
        private static readonly NamedColorOption[] MosquitoColors = {
            new NamedColorOption("red", "Rojo", new Color(.55f,.14f,.11f)), new NamedColorOption("blue", "Azul", new Color(.17f,.3f,.52f)),
            new NamedColorOption("green", "Oliva", new Color(.31f,.36f,.18f)), new NamedColorOption("purple", "Violeta", new Color(.37f,.2f,.43f)) };
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
            bool loadedSettings = false, migrateResolution = false;
            try
            {
                preferenceStore = new PreferenceFileStore(Path.Combine(DataPath, "preferences.json"), ClassifyPreferences);
                string json = preferenceStore.Load();
                if (preferenceStore.WriteBlocked) saveError = "Los ajustes son de otra versión. Se conservaron sin modificar.";
                else if (preferenceStore.RecoveredFromBackup) saveError = "Se recuperaron los ajustes de la copia de respaldo.";
                if (json != null)
                {
                    var data = JsonUtility.FromJson<Preferences>(json);
                    if (data.settings != null)
                    {
                        settings = Sanitize(data.settings);
                        settings.ResolutionIndex = ResolveResolutionIndex(resolutions, data.resolutionWidth,
                            data.resolutionHeight, data.settings.ResolutionIndex, currentIndex);
                        loadedSettings = true;
                        migrateResolution = data.schema == 1 && data.resolutionWidth == 0 && data.resolutionHeight == 0;
                    }
                    if (ValidAppearance(data.appearance)) appearance = data.appearance;
                    if (!string.IsNullOrWhiteSpace(data.playerName) && data.playerName.Length <= 24) playerName = data.playerName;
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
            // Upgrade the legacy index once through the existing atomic/backup store, without changing schema.
            if (migrateResolution && !preferenceStore.WriteBlocked)
            {
                string loadNotice = saveError;
                if (SavePreferences()) saveError = loadNotice;
            }
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
        private static PreferenceDocumentKind ClassifyPreferences(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<Preferences>(json);
                if (data == null || data.schema < 1) return PreferenceDocumentKind.Invalid;
                return data.schema == 1 ? PreferenceDocumentKind.Current : PreferenceDocumentKind.UnsupportedVersion;
            }
            catch (ArgumentException) { return PreferenceDocumentKind.Invalid; }
        }
        private bool SavePreferences()
        {
            try {
            if (preferenceStore == null) preferenceStore = new PreferenceFileStore(Path.Combine(DataPath, "preferences.json"), ClassifyPreferences);
            if (preferenceStore.WriteBlocked) { saveError = "Los ajustes son de otra versión. Se conservaron sin modificar."; return false; }
            var storedSettings = settings.Copy();
            var storedResolution = resolutions[settings.ResolutionIndex];
            int storedWidth = storedResolution.width, storedHeight = storedResolution.height;
            if (preserveLaunchVideoChoice)
            {
                storedSettings.ResolutionIndex = launchSavedVideoSettings.ResolutionIndex;
                storedSettings.FullScreen = launchSavedVideoSettings.FullScreen;
                storedWidth = launchSavedWidth; storedHeight = launchSavedHeight;
            }
            preferenceStore.Save(JsonUtility.ToJson(new Preferences { schema = 1, playerName = playerName,
                settings = storedSettings, appearance = appearance, resolutionWidth = storedWidth, resolutionHeight = storedHeight }, true));
            saveError = ""; return true;
            } catch (InvalidDataException) { saveError = "No se guardaron ajustes: el archivo pertenece a otra versión o no es válido."; return false; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { saveError = "No se pudo guardar. Revisá el acceso a la carpeta y volvé a intentar."; return false; }
        }
        private void PresentPreferences()
        {
            ui.PresentSettings(new SettingsUiState(settings, settings, resolutions.Select(r => r.width + " × " + r.height),
                QualitySettings.names, true, false, message: saveError,
                supportsReducedMenuMotion: livingMenu && livingMenu.IsConfigured));
            ui.PresentCustomization(new CustomizationUiState(Skins, Pajamas, MosquitoColors, appearance, message: saveError));
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
            foreach (var category in new[]{"Ambience","Character","Critical","Mosquito","World","UI"}) SetVolume(category, settings.EffectsVolume);
            if (game) { game.MouseSensitivity = .002f * (game.LatestSnapshot?.Actors.FirstOrDefault(a=>a.ActorId==game.LocalActorId)?.Role == Core.PlayerRole.Mosquito ? settings.MosquitoSensitivity : settings.HumanSensitivity); game.InvertY = settings.InvertY; }
        }
        private void SetVolume(string category,float volume) { if (Mixer) Mixer.SetFloat(category+"Volume",volume<=.0001f ? -80 : 20*Mathf.Log10(volume)); }
        public void PreviewCustomization(BasicCustomizationDraft draft)
        { if (ValidAppearance(draft)) { previewAppearance = draft.Copy(); previousPreview = null; } }
        public void SaveCustomization(BasicCustomizationDraft draft)
        { if (!ValidAppearance(draft)) return; var previous = appearance; appearance=draft.Copy(); if(!SavePreferences()) appearance=previous; previewAppearance=appearance.Copy(); previousPreview=null; appearanceAt=0; PresentPreferences(); }
        private static bool ValidAppearance(BasicCustomizationDraft draft)
            => draft != null && (draft.Role == AlfaRole.Human || draft.Role == AlfaRole.Mosquito) && Skins.Any(c=>c.Id==draft.SkinColorId) && Pajamas.Any(c=>c.Id==draft.PajamaColorId) && MosquitoColors.Any(c=>c.Id==draft.MosquitoColorId);
        private void ApplyPreviewColors()
        {
            var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true); var instance=orbit?.CurrentInstance;
            if (!instance || instance == previousPreview) return; previousPreview=instance;
            foreach(var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            ApplyAppearance(instance.GetComponent<CharacterView>(),previewAppearance??appearance);
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


