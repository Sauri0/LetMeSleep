using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    internal static class AlfaUiTheme
    {
        internal static readonly Color Ink900 = Hex("081526");
        internal static readonly Color Night800 = Hex("10233D");
        internal static readonly Color Night700 = Hex("18365A");
        internal static readonly Color Night600 = Hex("244B78");
        internal static readonly Color Moon200 = Hex("BED4EA");
        internal static readonly Color Sheet100 = Hex("FFF1D6");
        internal static readonly Color Lamp400 = Hex("F6C453");
        internal static readonly Color Pajama500 = Hex("EF6258");
        internal static readonly Color Mint400 = Hex("65D49B");
        internal static readonly Color Sky400 = Hex("4FA9F5");
        internal static readonly Color Disabled = Hex("6E8299");
        internal static readonly Color Border = Hex("4D83BD");
        internal static readonly Color Scrim = Hex("06111F", 0.82f);

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
            var normal = destructive ? Pajama500 : primary ? Sky400 : Night600;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = normal,
                highlightedColor = primary ? Hex("72C2FF") : Hex("35699D"),
                pressedColor = primary ? Hex("2E82C9") : Sky400,
                selectedColor = Sky400,
                disabledColor = new Color(Disabled.r, Disabled.g, Disabled.b, 0.48f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
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
