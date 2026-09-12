using LetMeSleep.UI;
using LetMeSleep.Audio;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private int menuAudioContext = -1;

        private void OnUiFeedback(UiFeedbackKind kind)
        {
            AlfaAudioDirector audio = presentation
                ? presentation.GetComponentInChildren<AlfaAudioDirector>() : menuAudio;
            if (!audio || !audio.isActiveAndEnabled) return;
            switch (kind)
            {
                case UiFeedbackKind.Select: audio.PlayUiSelect(); break;
                case UiFeedbackKind.Confirm: audio.PlayUiConfirm(); break;
                case UiFeedbackKind.Error: audio.PlayUiError(); break;
            }
        }

        private void SyncMenuAudioContext()
        {
            if (!menuAudio || ui == null || game != null) { menuAudioContext = -1; return; }
            // Returning after an unexpected room closure also restores the menu bed.
            if (!menuAudio.gameObject.activeSelf) { menuAudio.gameObject.SetActive(true); menuAudioContext = -1; }
            int next = ui.CurrentScreen == AlfaUiScreen.Customization || ui.CurrentScreen == AlfaUiScreen.Settings ? 1 : 0;
            if (menuAudioContext == next) return;
            menuAudioContext = next;
            if (next == 1)
                menuAudio.EnterQuietMenu();
            else
                menuAudio.EnterMenu();
        }
    }
}
