using System;
using UnityEngine.InputSystem;

namespace LetMeSleep.Bootstrap
{
    /// <summary>Persisted push-to-talk binding text shared by the menu (no room) and the in-room voice runtime.</summary>
    public static class PushToTalkBindings
    {
        public const string DefaultPath = "<Keyboard>/v";
        public const string WaitingLabel = "PRESIONÁ UNA TECLA";
        public const string WaitingNotice = "Presioná una tecla o un botón del mouse · Esc para cancelar";

        public static string Label(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "V";
            string value;
            try { value = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice); }
            catch (ArgumentException) { return "V"; }
            return string.IsNullOrWhiteSpace(value) ? "V" : value.ToUpperInvariant();
        }
    }
}
