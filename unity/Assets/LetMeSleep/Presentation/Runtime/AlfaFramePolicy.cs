using UnityEngine;

namespace LetMeSleep.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AlfaFramePolicy : MonoBehaviour
    {
        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private bool applyOnAwake = true;

        private void Awake()
        {
            if (applyOnAwake)
                ApplyDefault();
        }

        public void ApplyDefault()
        {
            int vSync = preset != null ? preset.DefaultVSyncCount : 0;
            int target = preset != null ? preset.DefaultTargetFrameRate : -1;
            QualitySettings.vSyncCount = Mathf.Clamp(vSync, 0, 4);
            Application.targetFrameRate = target;
        }

        public void ApplyUserChoice(bool vSync, int frameLimit)
        {
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = vSync || frameLimit <= 0 ? -1 : frameLimit;
        }
    }
}
