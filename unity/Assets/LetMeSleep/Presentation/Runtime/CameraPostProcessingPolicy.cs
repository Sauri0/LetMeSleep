using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 atmosphere: the role cameras must render URP post-processing so each map's VolumeProfile
    /// (Neutral tonemapping, bloom halos on lamps/windows/fire, grading, vignette) and SMAA reach the
    /// player. A camera without UniversalAdditionalCameraData renders with renderPostProcessing=false.
    /// Idempotent and local to the given camera; never touches volumes, other cameras or presets.
    /// </summary>
    public static class CameraPostProcessingPolicy
    {
        public static void ApplyGameplay(Camera camera)
        {
            if (camera == null)
                return;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
        }
    }
}
