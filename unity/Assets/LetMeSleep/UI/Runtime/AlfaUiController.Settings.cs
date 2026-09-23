using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>
    /// Settings (UI-06 screen 10, UI-05): tabs GENERAL / AUDIO / VIDEO / CONTROLES / ACCESIBILIDAD with icons on the
    /// left, one row per real option (label on the left, control and value in one right-hand column of 480 units),
    /// rows separated by a 1-unit #3B5E9C line at 40 %, RESTAURAR and APLICAR (always the full green; with nothing to
    /// apply only its label and check dim to 55 %). Content follows the sketch (stage 3): GENERAL has the language,
    /// the mouse sensitivity, the general / music / effects volumes and the voice chat key; AUDIO is the full audio
    /// page (the same volumes plus voices and microphone); the sensitivity is not repeated in CONTROLES, whose key
    /// reference uses a fixed 84-unit key column; ACCESIBILIDAD has the options this build has plus the legend of
    /// the status icons. Controls of the same option edit the same draft and are refreshed together. Push-to-talk
    /// rebinding keeps unapplied edits, shows one waiting text (on its row), keeps the current tab highlighted and
    /// its Esc no longer closes the screen (ui-presentation-audio-4). Options the game does not have (subtitles,
    /// colour blindness, text size) are not shown: adding them needs a new preferences schema (see MAPA-SISTEMAS, ui).
    /// </summary>
    public sealed partial class AlfaUiController
    {
        private enum SettingsTab { General, Audio, Video, Controls, Accessibility }
        private const float SettingsControlWidth = 480f;
        private const float SettingsRowHeight = 58f;
        private const float SettingsKeyColumn = 84f;

        private static readonly int[] FrameLimitOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };
        private static readonly CultureInfo SettingsCulture = CultureInfo.GetCultureInfo("es-AR");

        private AlfaUiScreen settingsReturnScreen;
        private SettingsUiState settingsState;
        private AlfaSettingsDraft settingsDraft;
        private bool settingsApplyLatched;
        private SettingsTab settingsTab = SettingsTab.General;
        private readonly Dictionary<SettingsTab, GameObject> settingsPages = new Dictionary<SettingsTab, GameObject>();
        private readonly Dictionary<SettingsTab, UnityEngine.UI.Button> settingsTabButtons = new Dictionary<SettingsTab, UnityEngine.UI.Button>();
        private readonly Dictionary<SettingsTab, string> settingsFirstControl = new Dictionary<SettingsTab, string>();
        private readonly List<Action> settingsRefreshers = new List<Action>();
        private TMP_Dropdown voiceDeviceDropdown;
        private string voiceDeviceOptionsKey = string.Empty;
        private TextMeshProUGUI pushToTalkBindingLabel;
        private UnityEngine.UI.Button pushToTalkButton;
        private TextMeshProUGUI pushToTalkButtonLabel;
        private TextMeshProUGUI pushToTalkNote;
        private bool pttRebinding;
        private AlfaSettingsDraft pttRebindSnapshot;
        // Label of a binding just completed, shown until the voice runtime publishes it (PresentVoice).
        private string pttCompletedLabel;
        private GameObject reduceMotionRow;
        private GameObject accessibilityEmptyNote;
        private TextMeshProUGUI settingsStatus;
        private UnityEngine.UI.Button settingsApplyButton;
        private TextMeshProUGUI settingsApplyLabel;
        private UnityEngine.UI.Button settingsResetButton;
        private CanvasGroup settingsControlsGroup;

        private void BuildSettings()
        {
            var view = factory.View("SettingsView", transform, false);
            SetSceneScrim(view, 0.8f);
            screens[AlfaUiScreen.Settings] = view;
            var panel = CenteredPanel(view.transform, "SettingsCard", 1440f, 860f);
            panel.GetComponent<UnityEngine.UI.Image>().color = AlfaUiTheme.Night800;
            var header = factory.SectionHeader(panel, "Header", "AJUSTES", AlfaUiIconKind.Gear, AlfaUiTheme.Sky400);
            Anchor(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(600f, 52f));
            header.Find("Label").GetComponent<TextMeshProUGUI>().fontSize = AlfaUiTheme.HeaderTitleSize;

            var content = AlfaUiFactory.Node("Content", panel, typeof(CanvasGroup)).GetComponent<RectTransform>();
            AlfaUiFactory.Fill(content, 28f, 28f, 100f, 28f);
            settingsControlsGroup = content.GetComponent<CanvasGroup>();

            var tabs = factory.Vertical(content, "Tabs", 10f);
            Anchor(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 5 * 76f + 4 * 10f));
            SettingsTabButton(tabs, SettingsTab.General, "GENERAL", AlfaUiIconKind.Gear);
            SettingsTabButton(tabs, SettingsTab.Audio, "AUDIO", AlfaUiIconKind.Audio);
            SettingsTabButton(tabs, SettingsTab.Video, "VIDEO", AlfaUiIconKind.Video);
            SettingsTabButton(tabs, SettingsTab.Controls, "CONTROLES", AlfaUiIconKind.Controls);
            SettingsTabButton(tabs, SettingsTab.Accessibility, "ACCESIBILIDAD", AlfaUiIconKind.Accessibility);
            var back = factory.Button(content, "SettingsBackButton", "VOLVER", CloseSettings, AlfaButtonStyle.Quiet, 66f, AlfaUiIconKind.Back);
            Anchor((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(300f, 66f));

            var pages = factory.Inset(content, "Pages", -1f, AlfaUiTheme.PanelRadius);
            AlfaUiFactory.Place(pages, Vector2.zero, Vector2.one, new Vector2(324f, 96f), Vector2.zero);

            // GENERAL as UI-06: language, sensitivity, the three volumes and the voice chat key.
            var general = SettingsPage(pages, SettingsTab.General, "GeneralPanel");
            ValueRow(general, "Idioma", "Language", "Español");
            SliderRow(general, "Sensibilidad del mouse · humano", "HumanSensitivitySlider", draft => draft.HumanSensitivity, (draft, value) => draft.HumanSensitivity = value, 0.1f, 2f);
            SliderRow(general, "Sensibilidad del mouse · mosquito", "MosquitoSensitivitySlider", draft => draft.MosquitoSensitivity, (draft, value) => draft.MosquitoSensitivity = value, 0.1f, 2f);
            SliderRow(general, "Volumen general", "GeneralMasterVolumeSlider", draft => draft.MasterVolume, (draft, value) => draft.MasterVolume = value);
            SliderRow(general, "Volumen música", "GeneralMusicVolumeSlider", draft => draft.MusicVolume, (draft, value) => draft.MusicVolume = value);
            SliderRow(general, "Volumen efectos", "GeneralEffectsVolumeSlider", draft => draft.EffectsVolume, (draft, value) => draft.EffectsVolume = value);
            BuildPushToTalkRow(general);

            var audio = SettingsPage(pages, SettingsTab.Audio, "AudioPanel");
            SliderRow(audio, "Volumen general", "MasterVolumeSlider", draft => draft.MasterVolume, (draft, value) => draft.MasterVolume = value);
            SliderRow(audio, "Música", "MusicVolumeSlider", draft => draft.MusicVolume, (draft, value) => draft.MusicVolume = value);
            SliderRow(audio, "Efectos", "EffectsVolumeSlider", draft => draft.EffectsVolume, (draft, value) => draft.EffectsVolume = value);
            SliderRow(audio, "Voces del chat", "VoiceVolumeSlider", draft => draft.VoiceVolume, (draft, value) => draft.VoiceVolume = value);
            var micRow = SettingsRow(audio, "Micrófono", "VoiceDeviceRow");
            voiceDeviceDropdown = factory.Dropdown(micRow, "VoiceDeviceDropdown", new[] { "ELEGÍ UN MICRÓFONO" }, index =>
            {
                string device = index > 0 && settingsState != null && index - 1 < settingsState.VoiceDevices.Count ? settingsState.VoiceDevices[index - 1] : string.Empty;
                ChangeSetting(draft => draft.VoiceDevice = device);
            });
            // Same right-hand column as every other control (it used to stretch and start 100 units earlier).
            var dropdownLayout = voiceDeviceDropdown.GetComponent<UnityEngine.UI.LayoutElement>();
            dropdownLayout.minWidth = dropdownLayout.preferredWidth = SettingsControlWidth;
            dropdownLayout.flexibleWidth = 0f;

            var video = SettingsPage(pages, SettingsTab.Video, "VideoPanel");
            ToggleRow(video, "Pantalla completa", "FullScreen", draft => draft.FullScreen, (draft, value) => draft.FullScreen = value);
            CycleRow(video, "Resolución", "Resolution", draft => ItemAt(settingsState?.Resolutions, draft.ResolutionIndex),
                (draft, delta) => draft.ResolutionIndex = Cycle(draft.ResolutionIndex, delta, settingsState?.Resolutions.Count ?? 0));
            CycleRow(video, "Calidad", "Quality", draft => ItemAt(settingsState?.Qualities, draft.QualityIndex),
                (draft, delta) => draft.QualityIndex = Cycle(draft.QualityIndex, delta, settingsState?.Qualities.Count ?? 0));
            ToggleRow(video, "Sincronización vertical", "VSync", draft => draft.VSync, (draft, value) => draft.VSync = value);
            CycleRow(video, "Límite de FPS", "FrameLimit", draft => FrameLimitLabel(draft.FrameLimit), (draft, delta) =>
            {
                var index = Mathf.Max(0, Array.IndexOf(FrameLimitOptions, draft.FrameLimit));
                draft.FrameLimit = FrameLimitOptions[Cycle(index, delta, FrameLimitOptions.Length)];
            });

            // The mouse sensitivity lives in GENERAL only (it used to be repeated here).
            var controls = SettingsPage(pages, SettingsTab.Controls, "ControlsPanel");
            ToggleRow(controls, "Invertir eje vertical", "InvertY", draft => draft.InvertY, (draft, value) => draft.InvertY = value);
            BuildKeyReference(controls);

            var access = SettingsPage(pages, SettingsTab.Accessibility, "AccessibilityPanel");
            reduceMotionRow = ToggleRow(access, "Reducir movimiento", "ReduceMenuMotion", draft => draft.ReduceMenuMotion,
                (draft, value) => draft.ReduceMenuMotion = value).gameObject;
            accessibilityEmptyNote = factory.Text(access, "AccessibilityNote",
                "Esta versión todavía no tiene opciones de accesibilidad para este equipo.", AlfaUiTheme.BodySize, AlfaUiTheme.Moon200).gameObject;
            var accessInfo = factory.Text(access, "AccessibilityInfo",
                "Los estados del juego nunca dependen sólo del color: cada uno lleva un icono o un texto.", AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200);
            accessInfo.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 40f;
            BuildStatusLegend(access);
            reduceMotionRow.SetActive(false);

            var footer = factory.Horizontal(content, "Actions", 14f, TextAnchor.MiddleRight);
            AlfaUiFactory.Place(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(324f, 0f), new Vector2(0f, 72f));
            settingsStatus = factory.Text(footer, "Status", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
            settingsStatus.textWrappingMode = TextWrappingModes.Normal;
            settingsStatus.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            settingsResetButton = factory.Button(footer, "SettingsResetButton", "RESTAURAR", ResetSettings, AlfaButtonStyle.Secondary, 72f, AlfaUiIconKind.Refresh);
            var resetLayout = settingsResetButton.GetComponent<UnityEngine.UI.LayoutElement>();
            resetLayout.preferredWidth = resetLayout.minWidth = 260f;
            resetLayout.flexibleWidth = 0f;
            settingsApplyButton = factory.Button(footer, "SettingsApplyButton", "APLICAR", ApplySettings, AlfaButtonStyle.Success, 72f, AlfaUiIconKind.Ready);
            factory.StrongLabel(settingsApplyButton, AlfaUiTheme.CtaSize);
            // Always green; with nothing to apply it stays green at half opacity (never the flat navy).
            AlfaUiFactory.KeepIntentWhenDisabled(settingsApplyButton);
            var applyLayout = settingsApplyButton.GetComponent<UnityEngine.UI.LayoutElement>();
            applyLayout.preferredWidth = applyLayout.minWidth = 300f;
            applyLayout.flexibleWidth = 0f;
            settingsApplyLabel = settingsApplyButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            SelectSettingsTab(SettingsTab.General, false);
        }

        private void SettingsTabButton(Transform parent, SettingsTab tab, string label, AlfaUiIconKind icon)
        {
            var button = factory.Button(parent, "SettingsTab" + tab, label, () => SelectSettingsTab(tab, false), AlfaButtonStyle.Tab, 76f, icon);
            var text = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            factory.MakeDisplay(text, 26f);
            settingsTabButtons[tab] = button;
        }

        private RectTransform SettingsPage(Transform pages, SettingsTab tab, string name)
        {
            var page = factory.Vertical(pages, name, 6f);
            AlfaUiFactory.Fill(page, 30f, 30f, 22f, 22f);
            settingsPages[tab] = page.gameObject;
            return page;
        }

        /// <summary>
        /// UI-06 settings row: sentence-case label on the left, the control (and its value) in the fixed right-hand
        /// column, and a 1-unit #3B5E9C line at 40 % under the row.
        /// </summary>
        private RectTransform SettingsRow(Transform page, string label, string name)
        {
            var row = factory.Horizontal(page, name, 18f, TextAnchor.MiddleLeft);
            var element = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = element.preferredHeight = SettingsRowHeight;
            var text = factory.Text(row, "Label", label, 23f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var layout = text.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = 300f;
            layout.flexibleWidth = 1f;
            var separator = AlfaUiFactory.Node("RowSeparator", row, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            separator.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var line = separator.GetComponent<UnityEngine.UI.Image>();
            line.color = AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.4f);
            line.raycastTarget = false;
            Anchor(line.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -3f), new Vector2(0f, 1f));
            return row;
        }

        private RectTransform SliderRow(Transform page, string label, string name, Func<AlfaSettingsDraft, float> read,
            Action<AlfaSettingsDraft, float> write, float min = 0f, float max = 1f)
        {
            var row = SettingsRow(page, label, name + "Row");
            UnityEngine.UI.Slider slider = null;
            slider = factory.Slider(row, name, min, max, value => ChangeSetting(draft => write(draft, value)));
            var sliderLayout = slider.GetComponent<UnityEngine.UI.LayoutElement>();
            // Slider + gap + value = the 480-unit control column.
            sliderLayout.preferredWidth = sliderLayout.minWidth = SettingsControlWidth - 18f - 84f;
            sliderLayout.flexibleWidth = 0f;
            var valueText = factory.Text(row, "Value", string.Empty, 23f, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineRight, true);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            var valueLayout = valueText.GetComponent<UnityEngine.UI.LayoutElement>();
            valueLayout.minWidth = valueLayout.preferredWidth = 84f;
            valueLayout.flexibleWidth = 0f;
            slider.onValueChanged.AddListener(value => valueText.text = FormatSettingValue(value, max));
            settingsRefreshers.Add(() =>
            {
                if (settingsDraft == null) return;
                var value = read(settingsDraft);
                slider.SetValueWithoutNotify(value);
                valueText.text = FormatSettingValue(value, max);
            });
            RememberFirstControl(page, name);
            return row;
        }

        private RectTransform CycleRow(Transform page, string label, string name, Func<AlfaSettingsDraft, string> read, Action<AlfaSettingsDraft, int> step)
        {
            var row = SettingsRow(page, label, name + "Row");
            var cycle = factory.Horizontal(row, name + "Cycle", 8f, TextAnchor.MiddleLeft);
            var cycleLayout = cycle.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            cycleLayout.minWidth = cycleLayout.preferredWidth = SettingsControlWidth;
            cycleLayout.flexibleWidth = 0f;
            CycleButton(cycle, name + "Previous", AlfaUiIconKind.ChevronLeft, () => ChangeSetting(draft => step(draft, -1)));
            var well = factory.Inset(cycle, name + "Well", 52f);
            well.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            // Values in the display face: its zero has no slash ("1920 × 1080", "60 FPS").
            var value = factory.Text(well, name + "Value", "—", 25f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(value.rectTransform, 10f, 10f, 4f, 4f);
            CycleButton(cycle, name + "Next", AlfaUiIconKind.ChevronRight, () => ChangeSetting(draft => step(draft, 1)));
            settingsRefreshers.Add(() => { if (settingsDraft != null) value.text = read(settingsDraft); });
            RememberFirstControl(page, name + "Previous");
            return row;
        }

        /// <summary>
        /// Read-only value row (the game has a single language): the value in a well, no arrows, so it never
        /// invites a change that does not exist.
        /// </summary>
        private RectTransform ValueRow(Transform page, string label, string name, string value)
        {
            var row = SettingsRow(page, label, name + "Row");
            var well = factory.Inset(row, name + "Well", 52f);
            var layout = well.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = layout.preferredWidth = SettingsControlWidth;
            layout.flexibleWidth = 0f;
            var text = factory.Text(well, name + "Value", value, 25f, AlfaUiTheme.Sheet100, TextAlignmentOptions.Center, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AlfaUiFactory.Fill(text.rectTransform, 10f, 10f, 4f, 4f);
            return row;
        }

        /// <summary>On/off option as the UI-06 "‹ Activado ›" cycle.</summary>
        private RectTransform ToggleRow(Transform page, string label, string name, Func<AlfaSettingsDraft, bool> read, Action<AlfaSettingsDraft, bool> write) =>
            CycleRow(page, label, name, draft => read(draft) ? "Activado" : "Desactivado", (draft, _) => write(draft, !read(draft)));

        private void RememberFirstControl(Transform page, string controlName)
        {
            foreach (var pair in settingsPages)
                if (pair.Value.transform == page && !settingsFirstControl.ContainsKey(pair.Key)) settingsFirstControl[pair.Key] = controlName;
        }

        private void BuildPushToTalkRow(Transform page)
        {
            var row = SettingsRow(page, "Chat de voz · tecla para hablar", "PushToTalkRow");
            var group = factory.Horizontal(row, "PushToTalk", 12f, TextAnchor.MiddleLeft);
            var groupLayout = group.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            groupLayout.minWidth = groupLayout.preferredWidth = SettingsControlWidth;
            groupLayout.flexibleWidth = 0f;
            var cap = factory.KeyCap(group, "PushToTalkKey", "V", 46f);
            pushToTalkBindingLabel = cap.Find("Key").GetComponent<TextMeshProUGUI>();
            var capLayout = cap.GetComponent<UnityEngine.UI.LayoutElement>();
            capLayout.minWidth = capLayout.preferredWidth = 110f;
            pushToTalkButton = factory.Button(group, "PushToTalkRebindButton", "CAMBIAR", BeginPushToTalkRebind, AlfaButtonStyle.Secondary, 52f, AlfaUiIconKind.Microphone);
            pushToTalkButtonLabel = pushToTalkButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            pushToTalkNote = factory.Text(page, "PushToTalkNote", string.Empty, AlfaUiTheme.NoteSize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineRight);
        }

        /// <summary>Read-only reference of the real gameplay keys (GameplayRuntime), per role.</summary>
        private void BuildKeyReference(Transform page)
        {
            factory.Divider(page, "KeysDivider", AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.5f));
            var columns = factory.Horizontal(page, "KeyReference", 28f, TextAnchor.UpperLeft);
            columns.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
            KeyColumn(columns, "HumanKeys", "TECLAS DEL HUMANO", new[]
            {
                ("CLIC", "Golpear"), ("E", "Usar · recoger"), ("1 2 3 · 0", "Objetos · manos"), ("G", "Soltar"), ("SHIFT", "Correr"), ("CTRL", "Agacharte")
            });
            KeyColumn(columns, "MosquitoKeys", "TECLAS DEL MOSQUITO", new[]
            {
                ("W", "Volar hacia la mira"), ("ESPACIO", "Subir"), ("CTRL", "Bajar"), ("F", "Posarte · despegar"), ("E", "Picar (mantené)"), ("R", "Ayudar a un aliado")
            });
        }

        private void KeyColumn(Transform parent, string name, string title, (string key, string label)[] keys)
        {
            var column = factory.Vertical(parent, name, 4f);
            column.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            factory.Caption(column, "Title", title);
            foreach (var (key, label) in keys)
            {
                var line = factory.Horizontal(column, "Key_" + key, 12f, TextAnchor.MiddleLeft);
                line.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 34f;
                // Fixed 84-unit key column: every label starts at the same x whatever the key's width.
                var slot = factory.Horizontal(line, "KeySlot", 0f, TextAnchor.MiddleLeft);
                var slotLayout = slot.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                slotLayout.minWidth = slotLayout.preferredWidth = SettingsKeyColumn;
                slotLayout.flexibleWidth = 0f;
                var cap = factory.KeyCap(slot, "Cap", key, 32f, 12f);
                var capLayout = cap.GetComponent<UnityEngine.UI.LayoutElement>();
                capLayout.minWidth = capLayout.preferredWidth = Mathf.Min(SettingsKeyColumn, Mathf.Max(capLayout.preferredWidth, 44f));
                var text = factory.Text(line, "Label", label, AlfaUiTheme.MinTextSize, AlfaUiTheme.Moon200, TextAlignmentOptions.MidlineLeft);
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>ACCESIBILIDAD: what each status icon means (the game never shows a state by colour alone).</summary>
        private void BuildStatusLegend(Transform page)
        {
            factory.Divider(page, "LegendDivider", AlfaUiTheme.WithAlpha(AlfaUiTheme.Border, 0.4f), 1f);
            factory.Caption(page, "StatusLegendTitle", "LEYENDA DE ICONOS DE ESTADO");
            var grid = AlfaUiFactory.Node("StatusLegend", page, typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            AlfaUiFactory.ConfigureGrid(grid.GetComponent<UnityEngine.UI.GridLayoutGroup>(), new Vector2(470f, 44f), new Vector2(18f, 6f), 2);
            var entries = new (AlfaUiIconKind icon, Color color, string label)[]
            {
                (AlfaUiIconKind.Ready, AlfaUiTheme.StatusOk, "Listo para jugar"),
                (AlfaUiIconKind.Close, AlfaUiTheme.StatusWarn, "No listo o silenciado"),
                (AlfaUiIconKind.Microphone, AlfaUiTheme.StatusOk, "Hablando por el chat de voz"),
                (AlfaUiIconKind.Audio, AlfaUiTheme.Sheet100, "Se escucha en el chat de voz"),
                (AlfaUiIconKind.Warning, AlfaUiTheme.Lamp400, "Aviso o problema de conexión"),
                (AlfaUiIconKind.Lock, AlfaUiTheme.Moon200, "Todavía no disponible"),
                (AlfaUiIconKind.Crown, AlfaUiTheme.Lamp400, "Anfitrión de la sala"),
                (AlfaUiIconKind.Heart, AlfaUiTheme.TeamMosquito, "Vidas o sangre a salvo")
            };
            foreach (var (icon, color, label) in entries)
            {
                var row = factory.Horizontal(grid.transform, "Legend_" + icon, 12f, TextAnchor.MiddleLeft);
                var symbol = factory.Icon(row, "Icon", icon, color);
                var symbolLayout = symbol.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                symbolLayout.minWidth = symbolLayout.preferredWidth = symbolLayout.minHeight = symbolLayout.preferredHeight = 28f;
                var text = factory.Text(row, "Label", label, AlfaUiTheme.NoteSize, AlfaUiTheme.Sheet100, TextAlignmentOptions.MidlineLeft);
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var rows = Mathf.CeilToInt(entries.Length / 2f);
            var layout = grid.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = rows * 44f + (rows - 1) * 6f + 4f;
        }

        private void SelectSettingsTab(SettingsTab tab, bool focusControl)
        {
            if (!settingsPages.ContainsKey(tab) || (settingsTabButtons.TryGetValue(tab, out var tabButton) && !tabButton.gameObject.activeSelf)) tab = SettingsTab.General;
            settingsTab = tab;
            foreach (var pair in settingsPages) pair.Value.SetActive(pair.Key == tab);
            foreach (var pair in settingsTabButtons) AlfaUiFactory.SetSelected(pair.Value, pair.Key == tab);
            if (focusControl) Focus(SettingsDefaultFocus());
        }

        private string SettingsDefaultFocus() => settingsFirstControl.TryGetValue(settingsTab, out var name) ? name : "SettingsTab" + settingsTab;

        public void PresentSettings(SettingsUiState state)
        {
            settingsState = state ?? throw new ArgumentNullException(nameof(state));
            settingsApplyLatched = state.IsApplying;
            var draft = state.Draft.Copy();
            // A completed push-to-talk rebind persists the binding and re-presents the saved settings: keep the
            // unapplied edits made before the rebind instead of discarding them (ui-presentation-audio-4).
            if (pttRebinding && pttRebindSnapshot != null && !state.IsApplying)
            {
                draft = pttRebindSnapshot.Copy();
                draft.PushToTalkBinding = state.Saved.PushToTalkBinding;
            }
            settingsDraft = draft;
            AlfaUiMotionPreferences.ReducedMotion = settingsDraft.ReduceMenuMotion;
            UpdateSettingsInteractivity();
            reduceMotionRow.SetActive(state.SupportsReducedMenuMotion);
            accessibilityEmptyNote.SetActive(!state.SupportsReducedMenuMotion);
            settingsTabButtons[SettingsTab.Video].gameObject.SetActive(state.SupportsVideo);
            if (!state.SupportsVideo && settingsTab == SettingsTab.Video) SelectSettingsTab(SettingsTab.General, false);
            var devicesKey = string.Join("\n", state.VoiceDevices);
            if (devicesKey != voiceDeviceOptionsKey)
            {
                voiceDeviceOptionsKey = devicesKey;
                voiceDeviceDropdown.ClearOptions();
                voiceDeviceDropdown.AddOptions(new[] { "ELEGÍ UN MICRÓFONO" }.Concat(state.VoiceDevices).ToList());
            }
            if (!pttRebinding) settingsStatus.text = state.IsApplying ? "Aplicando ajustes…" : state.Message;
            RefreshSettingsControls();
            UpdatePushToTalkRow();
        }

        private void RefreshSettingsControls()
        {
            if (settingsDraft == null) return;
            foreach (var refresh in settingsRefreshers) refresh();
            if (settingsState != null)
            {
                int voiceDeviceIndex = settingsState.VoiceDevices.ToList().FindIndex(device => string.Equals(device, settingsDraft.VoiceDevice, StringComparison.Ordinal));
                voiceDeviceDropdown.SetValueWithoutNotify(voiceDeviceIndex + 1);
                voiceDeviceDropdown.RefreshShownValue();
            }
            RefreshSettingsApplyState();
        }

        private void RefreshSettingsApplyState()
        {
            if (settingsState == null || settingsDraft == null) return;
            var dirty = !settingsDraft.SameValues(settingsState.Saved);
            settingsApplyButton.interactable = !settingsApplyLatched && !pttRebinding && dirty;
            settingsResetButton.interactable = !settingsApplyLatched && !pttRebinding && dirty;
            settingsApplyLabel.text = settingsApplyLatched ? "APLICANDO…" : "APLICAR";
        }

        private void UpdateSettingsInteractivity()
        {
            var editable = !settingsApplyLatched && !pttRebinding;
            settingsControlsGroup.interactable = editable;
            // While waiting for a key the whole card still eats clicks: the next press becomes the binding.
            settingsControlsGroup.blocksRaycasts = !settingsApplyLatched;
        }

        public void OpenSettings(AlfaUiScreen returnTo)
        {
            if (settingsState == null) PresentSettings(DefaultSettings());
            settingsReturnScreen = returnTo;
            if (returnTo == AlfaUiScreen.Gameplay || returnTo == AlfaUiScreen.Pause)
                actions.SetGameplayInputBlocked(true);
            SelectSettingsTab(SettingsTab.General, false);
            SetScreen(AlfaUiScreen.Settings, SettingsDefaultFocus());
        }

        private void ChangeSetting(Action<AlfaSettingsDraft> mutation)
        {
            if (settingsDraft == null || settingsState == null || settingsApplyLatched || settingsState.IsApplying || pttRebinding) return;
            mutation(settingsDraft);
            RefreshSettingsControls();
        }

        private void ApplySettings()
        {
            if (settingsDraft == null || settingsState == null || settingsApplyLatched || settingsState.IsApplying || pttRebinding) return;
            settingsApplyLatched = true;
            RefreshSettingsApplyState();
            UpdateSettingsInteractivity();
            settingsStatus.text = "Aplicando ajustes…";
            actions.ApplySettings(settingsDraft.Copy());
        }

        /// <summary>RESTAURAR: back to the last applied values (the unapplied edits are dropped).</summary>
        private void ResetSettings()
        {
            if (settingsState == null || settingsApplyLatched || pttRebinding) return;
            settingsDraft = settingsState.Saved.Copy();
            AlfaUiMotionPreferences.ReducedMotion = settingsDraft.ReduceMenuMotion;
            settingsStatus.text = "Se restauraron los ajustes guardados.";
            RefreshSettingsControls();
        }

        private void CloseSettings()
        {
            if (settingsApplyLatched || pttRebinding) return;
            if (SettingsDirty())
            {
                ShowConfirm("¿DESCARTAR CAMBIOS?", "Los ajustes no aplicados se perderán.", "SEGUIR EDITANDO", "DESCARTAR", ReturnFromSettings);
                return;
            }
            ReturnFromSettings();
        }

        private void ReturnFromSettings()
        {
            if (settingsState != null) settingsDraft = settingsState.Saved.Copy();
            if (settingsReturnScreen == AlfaUiScreen.Gameplay)
                actions.SetGameplayInputBlocked(false);
            SetScreen(settingsReturnScreen, settingsReturnScreen == AlfaUiScreen.Pause ? "PauseSettingsButton" : "MainSettingsButton");
        }

        private bool SettingsDirty() => settingsState != null && settingsDraft != null && !settingsDraft.SameValues(settingsState.Saved);

        /// <summary>
        /// Push to talk: only possible inside a room (the voice runtime exists there). While waiting, the row says so,
        /// the rest of the card is locked and Esc cancels only the rebind.
        /// </summary>
        private void BeginPushToTalkRebind()
        {
            if (pttRebinding || settingsApplyLatched || settingsDraft == null) return;
            if (!(actions is IVoiceActions voice) || voiceState == null || !voiceState.InRoom)
            {
                UpdatePushToTalkRow();
                return;
            }
            pttRebinding = true;
            pttRebindSnapshot = settingsDraft.Copy();
            // The row says it is waiting; the footer stays quiet so there is a single waiting text.
            settingsStatus.text = string.Empty;
            UpdatePushToTalkRow();
            UpdateSettingsInteractivity();
            RefreshSettingsApplyState();
            voice.BeginPushToTalkRebind((path, label) =>
            {
                var wasRebinding = pttRebinding;
                pttRebinding = false;
                pttRebindSnapshot = null;
                if (settingsDraft != null) settingsDraft.PushToTalkBinding = path;
                if (wasRebinding) settingsStatus.text = "Tecla para hablar: " + label + ". Ya quedó guardada.";
                pttCompletedLabel = label;
                UpdateSettingsInteractivity();
                UpdatePushToTalkRow();
                RefreshSettingsApplyState();
            });
        }

        private void CancelPushToTalkRebindUi()
        {
            if (!pttRebinding) return;
            pttRebinding = false;
            pttRebindSnapshot = null;
            settingsStatus.text = "Cambio de tecla cancelado.";
            UpdateSettingsInteractivity();
            UpdatePushToTalkRow();
            RefreshSettingsApplyState();
        }

        private void UpdatePushToTalkRow()
        {
            if (pushToTalkButton == null) return;
            var inRoom = voiceState != null && voiceState.InRoom && actions is IVoiceActions;
            var supported = settingsState == null || settingsState.SupportsRebinding;
            if (pttCompletedLabel != null && voiceState != null && voiceState.BindingLabel == pttCompletedLabel) pttCompletedLabel = null;
            if (!pttRebinding)
                pushToTalkBindingLabel.text = pttCompletedLabel ?? (inRoom && !string.IsNullOrWhiteSpace(voiceState.BindingLabel) ? voiceState.BindingLabel
                    : BindingKeyLabel(settingsDraft?.PushToTalkBinding));
            pushToTalkButton.interactable = inRoom && supported && !pttRebinding && !settingsApplyLatched;
            pushToTalkButtonLabel.text = pttRebinding ? "PULSÁ UNA TECLA…" : "CAMBIAR";
            pushToTalkNote.text = pttRebinding ? "Esc cancela" :
                !supported ? "Esta versión no permite cambiarla." :
                !inRoom ? "Se cambia dentro de una sala, con el chat de voz activo." : string.Empty;
            pushToTalkNote.gameObject.SetActive(!string.IsNullOrEmpty(pushToTalkNote.text));
        }

        private static string BindingKeyLabel(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "V";
            var slash = path.LastIndexOf('/');
            var key = slash >= 0 ? path.Substring(slash + 1) : path;
            return string.IsNullOrWhiteSpace(key) ? "V" : key.ToUpperInvariant();
        }

        private static string FormatSettingValue(float value, float max) => Mathf.Approximately(max, 1f)
            ? Mathf.RoundToInt(value * 100f) + "%"
            : value.ToString("0.00", SettingsCulture) + "×";

        private static int Cycle(int current, int delta, int count)
        {
            if (count <= 0) return 0;
            return ((current + delta) % count + count) % count;
        }

        private static string FrameLimitLabel(int value) => value <= 0 ? "Sin límite" : value + " FPS";

        private static string ItemAt(IReadOnlyList<string> items, int index) => items != null && index >= 0 && index < items.Count ? items[index] : "—";

        private static SettingsUiState DefaultSettings()
        {
            var saved = new AlfaSettingsDraft
            {
                MasterVolume = .8f,
                MusicVolume = .5f,
                EffectsVolume = .85f,
                VoiceVolume = .8f,
                PushToTalkBinding = "<Keyboard>/v",
                FullScreen = true,
                VSync = false,
                FrameLimit = 0,
                HumanSensitivity = 1f,
                MosquitoSensitivity = 1f,
                InvertY = false
            };
            return new SettingsUiState(saved, saved, Array.Empty<string>(), Array.Empty<string>(), false, false);
        }
    }
}
