using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 atmosphere materials shared by the five maps and the menu/lobby: camera-facing lantern halos,
    /// three-layer stylized flames, lobby string-light bulbs and the menu night window. Referenced by the
    /// catalog (HiggsfieldMapLighting.Configuration.Kit) and by the lighting rig prefab so the shaders ship in
    /// builds. Authored by Editor/ProjectBootstrap/HiggsfieldAtmosphereCorrection; visual only.
    /// </summary>
    [CreateAssetMenu(menuName = "Let me sleep/Higgsfield Atmosphere Kit", fileName = "HiggsfieldAtmosphereKit")]
    public sealed class HiggsfieldAtmosphereKit : ScriptableObject
    {
        [Tooltip("LetMeSleep/Higgsfield/Halo: additive camera-facing glow, color and size set per instance.")]
        public Material HaloMaterial;
        [Tooltip("LetMeSleep/Higgsfield/Flame layers, drawn outer to core (render queue order).")]
        public Material FlameOuter, FlameMiddle, FlameCore;
        [Tooltip("Emissive bulb for the lobby string lights.")]
        public Material BulbMaterial;
        [Tooltip("Dark wire for the lobby string lights.")]
        public Material WireMaterial;
        [Tooltip("LetMeSleep/Higgsfield/NightWindow forced to night (menu/lobby window: night sky, stars and moon).")]
        public Material MenuWindowMaterial;
        [Tooltip("v0.3.0 r3 LetMeSleep/Higgsfield/Beam: additive rotating light shaft (lighthouse). Optional.")]
        public Material BeamMaterial;
        [Tooltip("v0.3.0 r3 LetMeSleep/Higgsfield/Glint: warm reflection streak of a lamp on night water. Optional.")]
        public Material GlintMaterial;
        [Tooltip("v0.3.0 r3 LetMeSleep/Higgsfield/Pool: warm ground pool decal under a lantern. Optional.")]
        public Material PoolMaterial;

        public bool IsComplete => HaloMaterial && FlameOuter && FlameMiddle && FlameCore && BulbMaterial && WireMaterial && MenuWindowMaterial;
    }
}
