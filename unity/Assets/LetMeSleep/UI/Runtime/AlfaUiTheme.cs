using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    internal static class AlfaUiTheme
    {
        internal static readonly Color Ink900 = Hex("080B12");
        internal static readonly Color Night800 = Hex("121824");
        internal static readonly Color Night700 = Hex("1C2534");
        internal static readonly Color Night600 = Hex("303B4D");
        internal static readonly Color Moon200 = Hex("CAD3DC");
        internal static readonly Color Sheet100 = Hex("FFF1D2");
        internal static readonly Color Lamp400 = Hex("F0B84E");
        internal static readonly Color Pajama500 = Hex("DF6559");
        internal static readonly Color Mint400 = Hex("74C89C");
        internal static readonly Color Sky400 = Hex("70ACD5");
        internal static readonly Color Disabled = Hex("77818E");
        internal static readonly Color Border = Hex("756D62");
        internal static readonly Color Scrim = Hex("070A10", 0.80f);
        internal static readonly Color WarmSurface = Hex("29251F");
        internal static readonly Color ChalkShadow = Hex("D8C7A4", 0.28f);

        internal const float FocusDuration = 0.16f;
        internal const float EntranceDuration = 0.19f;

        internal const float LogoSize = 88f;
        internal const float H1Size = 48f;
        internal const float H2Size = 32f;
        internal const float ButtonSize = 24f;
        internal const float BodySize = 21f;
        internal const float LabelSize = 18f;
        internal const float NoteSize = 16f;

        internal static TMP_FontAsset Display(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont :
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : TMP_Settings.defaultFontAsset;

        internal static TMP_FontAsset Logo(AlfaUiDependencies dependencies) =>
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : Display(dependencies);

        internal static TMP_FontAsset Body(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont : TMP_Settings.defaultFontAsset;

        internal static UnityEngine.UI.ColorBlock ButtonColors(bool primary, bool destructive = false)
        {
            var normal = destructive ? Pajama500 : primary ? Lamp400 : Night600;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = normal,
                highlightedColor = primary ? Hex("FFD277") : Hex("46556B"),
                pressedColor = primary ? Hex("C98A2E") : Hex("252E3D"),
                selectedColor = primary ? Hex("FFD277") : Hex("46556B"),
                disabledColor = new Color(Disabled.r, Disabled.g, Disabled.b, 0.48f),
                colorMultiplier = 1f,
                fadeDuration = FocusDuration
            };
        }

        private static Color Hex(string rgb, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            color.a = alpha;
            return color;
        }
    }
}
