using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AlfaLightingRig : MonoBehaviour
    {
        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Light moon = null;
        [SerializeField] private Volume globalVolume = null;

        public AlfaPresentationPreset Preset => preset;
        public Light Moon => moon;
        public Volume GlobalVolume => globalVolume;

        public void ApplyPreset()
        {
            if (preset == null || moon == null)
                return;
            moon.type = LightType.Directional;
            moon.color = preset.MoonColor;
            moon.intensity = preset.MoonIntensityLux;
            moon.lightmapBakeType = LightmapBakeType.Mixed;
            moon.shadows = LightShadows.Soft;
        }
    }
}
