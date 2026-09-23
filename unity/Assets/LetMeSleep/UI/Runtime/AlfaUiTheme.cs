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
    /// v0.3 palette derived from the UI-06 sketch (docs/v030/GUIA-ESTILO-BOCETOS.md) and calibrated
    /// against colours sampled from the sketch itself. Token names from the alpha are kept so that
    /// every existing use recolours without touching the controller.
    /// </summary>
    internal static class AlfaUiTheme
    {
        // Legacy token names, v0.3 values.
        internal static readonly Color Ink900 = Hex("0B1426");      // outlines, dark ink, drop shadows
        internal static readonly Color Night800 = Hex("0A1A33");    // bg.deep
        internal static readonly Color Night700 = Hex("0E2545");    // panel
        internal static readonly Color Night600 = Hex("133259");    // btn.secondary
        internal static readonly Color Moon200 = Hex("A8B8D8");     // text.secondary
        internal static readonly Color Sheet100 = Hex("F2F6FF");    // text.primary
        internal static readonly Color Lamp400 = Hex("FFC93C");     // accent.yellow
        internal static readonly Color Pajama500 = Hex("E0393E");   // team.mosquito / danger text
        internal static readonly Color Mint400 = Hex("46C45F");     // success
        internal static readonly Color Sky400 = Hex("49B2FF");      // accent.blue, selection
        internal static readonly Color Disabled = Hex("6E8299");
        internal static readonly Color Border = Hex("2F5A96");      // panel.border
        internal static readonly Color Scrim = Hex("06111F", 0.82f);

        // v0.3 tokens.
        internal static readonly Color PanelHeader = Hex("16325C");
        internal static readonly Color PanelInset = Hex("0A1C36");
        internal static readonly Color InsetBorder = Hex("2A4B7C");
        internal static readonly Color SecondaryHover = Hex("1C4478");
        internal static readonly Color Primary = Hex("0C5FC9");
        internal static readonly Color PrimaryHi = Hex("2F86F0");
        internal static readonly Color PrimaryBorder = Hex("7CC0FF");
        internal static readonly Color Success = Hex("219A3E");
        internal static readonly Color SuccessHi = Hex("3CC45A");
        internal static readonly Color SuccessBorder = Hex("8FF0A2");
        internal static readonly Color Danger = Hex("A92A34");
        internal static readonly Color DangerHi = Hex("E04550");
        internal static readonly Color DangerBorder = Hex("FF9197");
        internal static readonly Color TeamHuman = Hex("2F7BFF");
        internal static readonly Color TeamMosquito = Hex("E0393E");
        internal static readonly Color StatusOk = Hex("57D26B");
        internal static readonly Color StatusWarn = Hex("FF6B5E");
        internal static readonly Color LabelInk = Hex("8BA2C3");    // small uppercase field labels
        internal static readonly Color LogoYellowTop = Hex("FFE680");
        internal static readonly Color LogoYellowBottom = Hex("FFB21C");
        internal static readonly Color LogoBlueTop = Hex("A6DEFF");
        internal static readonly Color LogoBlueBottom = Hex("2F8BFF");

        internal const float FocusDuration = 0.16f;
        internal const float EntranceDuration = 0.19f;

        // Shape metrics in canvas units at the 1920x1080 reference.
        internal const float PanelRadius = 14f;
        internal const float ButtonRadius = 12f;
        internal const float SmallRadius = 8f;
        internal const float BorderWidth = 2f;
        internal const float ShadowOffset = 4f;

        internal const float LogoSize = 88f;
        internal const float H1Size = 48f;
        internal const float H2Size = 32f;
        internal const float ButtonSize = 24f;
        internal const float BodySize = 21f;
        internal const float LabelSize = 18f;
        internal const float NoteSize = 16f;

        /// <summary>Readable UI face (Atkinson Hyperlegible) used with bold for buttons and labels.</summary>
        internal static TMP_FontAsset Display(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont :
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : TMP_Settings.defaultFontAsset;

        /// <summary>Comic display face (Bangers) for the logo, screen titles and large calls to action.</summary>
        internal static TMP_FontAsset Logo(AlfaUiDependencies dependencies) =>
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : Display(dependencies);

        internal static TMP_FontAsset Body(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont : TMP_Settings.defaultFontAsset;

        internal static bool HasComicFont(AlfaUiDependencies dependencies) => dependencies?.HeadingFont != null;

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
                    top = Hex("0F2A4E"); bottom = Hex("0B2140"); frame = new Color(Border.r, Border.g, Border.b, 0.75f); content = Moon200; break;
                case AlfaButtonStyle.Tab:
                    top = Hex("123056"); bottom = Hex("0E2748"); frame = new Color(Border.r, Border.g, Border.b, 0.9f); content = Moon200; break;
                default:
                    top = Hex("17396A"); bottom = Night600; frame = Border; break;
            }
        }

        /// <summary>
        /// Tint block over an already coloured (vertex gradient) surface. Normal is slightly dimmed so that
        /// hover and keyboard focus can brighten without exceeding the authored colour.
        /// </summary>
        internal static UnityEngine.UI.ColorBlock TintColors(AlfaButtonStyle style)
        {
            var dim = style == AlfaButtonStyle.Secondary || style == AlfaButtonStyle.Quiet || style == AlfaButtonStyle.Tab || style == AlfaButtonStyle.Menu ? 0.86f : 0.92f;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = new Color(dim, dim, dim, 1f),
                highlightedColor = Color.white,
                pressedColor = new Color(0.74f, 0.74f, 0.78f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.46f, 0.5f, 0.58f, 0.62f),
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
                disabledColor = new Color(Disabled.r, Disabled.g, Disabled.b, 0.48f),
                colorMultiplier = 1f,
                fadeDuration = FocusDuration
            };
        }

        internal static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        internal static Color Hex(string rgb, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            color.a = alpha;
            return color;
        }
    }
}
