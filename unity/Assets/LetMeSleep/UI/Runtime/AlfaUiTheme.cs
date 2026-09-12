using TMPro;
using UnityEngine;

namespace LetMeSleep.UI
{
    internal static class AlfaUiTheme
    {
        internal static readonly Color Ink900 = Hex("101727");
        internal static readonly Color Night800 = Hex("17243A");
        internal static readonly Color Night700 = Hex("223552");
        internal static readonly Color Night600 = Hex("2E4668");
        internal static readonly Color Moon200 = Hex("CBD9EA");
        internal static readonly Color Sheet100 = Hex("F5E9D6");
        internal static readonly Color Lamp400 = Hex("F2B84B");
        internal static readonly Color Pajama500 = Hex("E66050");
        internal static readonly Color Mint400 = Hex("67D19A");
        internal static readonly Color Sky400 = Hex("66A9F5");
        internal static readonly Color Disabled = Hex("718198");
        internal static readonly Color Scrim = Hex("08101F", 0.84f);

        internal const float LogoSize = 88f;
        internal const float H1Size = 48f;
        internal const float H2Size = 32f;
        internal const float ButtonSize = 24f;
        internal const float BodySize = 21f;
        internal const float LabelSize = 18f;
        internal const float NoteSize = 16f;

        internal static TMP_FontAsset Heading(AlfaUiDependencies dependencies) =>
            dependencies?.HeadingFont != null ? dependencies.HeadingFont : TMP_Settings.defaultFontAsset;

        internal static TMP_FontAsset Body(AlfaUiDependencies dependencies) =>
            dependencies?.BodyFont != null ? dependencies.BodyFont : TMP_Settings.defaultFontAsset;

        internal static UnityEngine.UI.ColorBlock ButtonColors(bool primary, bool destructive = false)
        {
            var normal = destructive ? Pajama500 : primary ? Sky400 : Night600;
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = normal,
                highlightedColor = primary ? new Color(0.48f, 0.74f, 1f) : new Color(0.24f, 0.36f, 0.52f),
                pressedColor = Lamp400,
                selectedColor = Lamp400,
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
