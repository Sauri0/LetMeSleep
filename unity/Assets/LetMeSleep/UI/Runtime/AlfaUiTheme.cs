using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    /// <summary>Intent of a filled control. Each screen declares intent; the palette lives here.</summary>
    internal enum AlfaButtonStyle
    {
        Primary,
        Success,
        Danger,
        Secondary,
        Quiet,
        Tab,
        Menu
    }

    /// <summary>
    /// v0.3 palette from docs/v030/GUIA-ESTILO-BOCETOS.md (UI-06). Token names from the alpha are kept so that
    /// every existing use recolours without touching the controller; the values are the guide's.
    /// </summary>
    internal static class AlfaUiTheme
    {
        // Legacy token names, guide values.
        internal static readonly Color Ink900 = Hex("0B1426");      // outlines, dark ink, drop shadows
        internal static readonly Color Night800 = Hex("0E1A30");    // bg.deep
        internal static readonly Color Night700 = Hex("15264A");    // panel
        internal static readonly Color Night600 = Hex("1E3358");    // btn.secondary
        internal static readonly Color Moon200 = Hex("A8B8D8");     // text.secondary
        internal static readonly Color Sheet100 = Hex("F2F6FF");    // text.primary
        internal static readonly Color Lamp400 = Hex("FFC93C");     // accent.yellow
        internal static readonly Color Pajama500 = Hex("E0393E");   // team.mosquito / danger text
        internal static readonly Color Mint400 = Hex("46C45F");     // success
        internal static readonly Color Sky400 = Hex("49B2FF");      // accent.blue, selection
        internal static readonly Color Disabled = Hex("6E8299");
        internal static readonly Color Border = Hex("3B5E9C");      // panel.border
        internal static readonly Color Scrim = Hex("0E1A30", 0.8f); // full-screen scrim behind modal cards

        // v0.3 tokens.
        internal static readonly Color PanelHeader = Hex("1C3160");
        internal static readonly Color PanelInset = Hex("0F1D38");
        internal static readonly Color InsetBorder = Hex("2D4A7E");
        internal static readonly Color SecondaryHover = Hex("274473");
        internal static readonly Color Primary = Hex("1F6FE0");
        internal static readonly Color PrimaryHi = Hex("3A8DFF");
        internal static readonly Color PrimaryBorder = Hex("7CC0FF");
        internal static readonly Color Success = Hex("2E9E48");
        internal static readonly Color SuccessHi = Hex("46C45F");
        internal static readonly Color SuccessBorder = Hex("8FF0A2");
        internal static readonly Color Danger = Hex("C62E36");
        internal static readonly Color DangerHi = Hex("E5484F");
        internal static readonly Color DangerBorder = Hex("FF9197");
        internal static readonly Color DisabledFill = Hex("1E3358");
        internal static readonly Color TeamHuman = Hex("2F7BFF");
        internal static readonly Color TeamMosquito = Hex("E0393E");
        internal static readonly Color StatusOk = Hex("57D26B");
        internal static readonly Color StatusWarn = Hex("FF6B5E");
        internal static readonly Color LabelInk = Hex("8FA6CC");    // small uppercase field labels
        internal static readonly Color LogoCream = Hex("EACEAB");   // wordmark subtitle

        /// <summary>Label/icon opacity of a disabled control that keeps its intent colours (inactive APLICAR).</summary>
        internal const float KeptIntentContentAlpha = 0.55f;
        internal const float FocusDuration = 0.16f;
        internal const float EntranceDuration = 0.19f;

        // Shape metrics in canvas units at the 1920x1080 reference.
        internal const float PanelRadius = 14f;
        internal const float ButtonRadius = 12f;
        internal const float SmallRadius = 8f;
        internal const float BorderWidth = 2f;
        internal const float SelectionBorderWidth = 3f;
        internal const float ShadowOffset = 4f;

        // Type scale at 1080p. The canvas scales by 2/3 at 1280x720, so 21 units is the 14 px floor there.
        internal const float MinTextSize = 21f;
        internal const float MenuLabelSize = 30f;
        internal const float PanelTitleSize = 34f;
        internal const float CtaSize = 34f;
        internal const float HeaderTitleSize = 40f;
        internal const float ButtonSize = 24f;
        internal const float BodySize = 22f;
        internal const float LabelSize = 21f;
        internal const float NoteSize = 21f;
        internal const float DisplayTracking = 2f;   // ~2 % of the em, as UI-06
        internal const float CaptionTracking = 3f;

        private const string DisplayFontResource = "AlfaUiFonts/LMSBarlowNarrow-Bold SDF";
        private static TMP_FontAsset displayFont;
        private static bool displayFontResolved;

        /// <summary>
        /// Upright bold condensed display face (LMS Barlow Narrow Bold, derived from Barlow, OFL) for tabs, buttons,
        /// calls to action, captions and titles. Loaded from Resources so it needs no scene reference; falls back
        /// to the body face in bold.
        /// </summary>
        internal static TMP_FontAsset Display(AlfaUiDependencies dependencies)
        {
            if (!displayFontResolved)
            {
                displayFont = Resources.Load<TMP_FontAsset>(DisplayFontResource);
                displayFontResolved = true;
            }
            return displayFont != null ? displayFont : Body(dependencies);
        }

        internal static bool HasDisplayFont(AlfaUiDependencies dependencies) => Display(dependencies) != Body(dependencies);

        /// <summary>
        /// Comic face of the logo (Bangers, the scene's heading font) for the big results title (UI-06 9); the
        /// display face when no distinct heading font is injected.
        /// </summary>
        internal static TMP_FontAsset Comic(AlfaUiDependencies dependencies) =>
            dependencies?.HeadingFont != null && dependencies.HeadingFont != dependencies.BodyFont ? dependencies.HeadingFont : Display(dependencies);

        internal static TMP_FontAsset Body(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont :
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : TMP_Settings.defaultFontAsset;

        /// <summary>Vertical gradient (top, bottom) and frame colour for a filled control.</summary>
        internal static void StyleColors(AlfaButtonStyle style, out Color top, out Color bottom, out Color frame, out Color content)
        {
            content = Sheet100;
            switch (style)
            {
                case AlfaButtonStyle.Primary:
                    top = PrimaryHi; bottom = Primary; frame = PrimaryBorder; break;
                case AlfaButtonStyle.Success:
                    top = SuccessHi; bottom = Success; frame = SuccessBorder; break;
                case AlfaButtonStyle.Danger:
                    top = DangerHi; bottom = Danger; frame = DangerBorder; break;
                case AlfaButtonStyle.Quiet:
                    top = Hex("182C52"); bottom = Hex("132546"); frame = WithAlpha(Border, 0.75f); content = Moon200; break;
                case AlfaButtonStyle.Tab:
                    top = Hex("1B3160"); bottom = Hex("162A52"); frame = WithAlpha(Border, 0.9f); content = Moon200; break;
                default:
                    top = Hex("233B66"); bottom = Night600; frame = Border; break;
            }
        }

        /// <summary>Disabled controls: flat secondary navy with content at half opacity, whatever their intent.</summary>
        internal static void DisabledColors(out Color top, out Color bottom, out Color frame, out float contentAlpha)
        {
            top = DisabledFill;
            bottom = DisabledFill;
            frame = WithAlpha(Border, 0.55f);
            contentAlpha = 0.5f;
        }

        /// <summary>
        /// Tint block over an already coloured (vertex gradient) surface. Normal is slightly dimmed so that
        /// hover and keyboard focus can brighten without exceeding the authored colour. Disabled is untinted:
        /// the surface itself switches to <see cref="DisabledColors"/>.
        /// </summary>
        internal static UnityEngine.UI.ColorBlock TintColors(AlfaButtonStyle style)
        {
            var dim = style == AlfaButtonStyle.Secondary || style == AlfaButtonStyle.Quiet || style == AlfaButtonStyle.Tab || style == AlfaButtonStyle.Menu ? 0.88f : 0.93f;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = new Color(dim, dim, dim, 1f),
                highlightedColor = Color.white,
                pressedColor = new Color(0.74f, 0.74f, 0.78f, 1f),
                selectedColor = Color.white,
                disabledColor = Color.white,
                colorMultiplier = 1f,
                fadeDuration = FocusDuration
            };
        }

        /// <summary>Legacy flat colour block for selectables that are not gradient surfaces (dropdowns, sliders).</summary>
        internal static UnityEngine.UI.ColorBlock ButtonColors(bool primary, bool destructive = false)
        {
            var normal = destructive ? Danger : primary ? Primary : Night600;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = normal,
                highlightedColor = destructive ? DangerHi : primary ? PrimaryHi : SecondaryHover,
                pressedColor = Color.Lerp(normal, Ink900, 0.25f),
                selectedColor = destructive ? DangerHi : primary ? PrimaryHi : SecondaryHover,
                disabledColor = DisabledFill,
                colorMultiplier = 1f,
                fadeDuration = FocusDuration
            };
        }

        internal static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>
        /// Tabular figures for times and counters ("02:55", "20 / 20"): every run of digits is monospaced so clocks
        /// do not jitter. Use on labels in the display face (LMS Barlow Narrow, whose zero has no slash).
        /// </summary>
        internal static string Digits(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            var builder = new System.Text.StringBuilder(value.Length + 24);
            var inRun = false;
            var inTag = false;
            foreach (var character in value)
            {
                // Rich-text tags ("<color=#F2F6FF>") are copied untouched.
                if (character == '<') inTag = true;
                var digit = !inTag && character >= '0' && character <= '9';
                if (character == '>') inTag = false;
                if (digit && !inRun) builder.Append("<mspace=0.52em>");
                if (!digit && inRun) builder.Append("</mspace>");
                inRun = digit;
                builder.Append(character);
            }
            if (inRun) builder.Append("</mspace>");
            return builder.ToString();
        }

        internal static Color Hex(string rgb, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            color.a = alpha;
            return color;
        }
    }
}
