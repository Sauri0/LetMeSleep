using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Presentation
{
    /// <summary>Explicit runtime opt-in for a static water MeshRenderer. No map discovery or mesh mutation.</summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldGpuWater : MonoBehaviour
    {
        public const string ShaderName = "LetMeSleep/Higgsfield/FlatGpuWater";
        const string CpuType = "LetMeSleep.Content.Environment.Higgsfield.HiggsfieldLowPolyWater";
        [Serializable] public struct Parameters
        {
            public float Amplitude, Wavelength, Speed, Phase;
            public bool UseTimeOverride, UseVertexColors;
            public bool UseFog; // Explicit opt-in; default false preserves existing maps/materials.
            public float TimeSeconds;
            public static Parameters Default => new Parameters { Amplitude = .025f, Wavelength = 4, Speed = .65f };
        }

        static readonly Dictionary<Renderer, HiggsfieldGpuWater> Owners = new Dictionary<Renderer, HiggsfieldGpuWater>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), LegacyColor = Shader.PropertyToID("_Color");
        MeshRenderer target;
        Material[] originals, owned;
        MaterialPropertyBlock originalBlock;
        MaterialPropertyBlock[] originalSlots;
        Bounds originalBounds;
        ShadowCastingMode originalShadows;
        bool originalReceiveShadows;
        Parameters parameters;
        readonly List<MonoBehaviour> componentScratch = new List<MonoBehaviour>();
        public bool IsBound => target != null;
        /// <summary>Parameters currently applied (meaningful only while bound).</summary>
        public Parameters CurrentParameters => parameters;

        /// <param name="water">Only the renderer of explicitly selected, non-solid water.</param>
        /// <param name="shader">Supply a referenced shader asset; no Shader.Find or automatic inclusion.</param>
        /// <param name="flatColors">Optional explicit palette, one color per material slot. Otherwise captures effective BaseColor/Color including MPBs.</param>
        public void Configure(MeshRenderer water, Shader shader, Parameters settings, Color[] flatColors = null)
        {
            Require(Application.isPlaying && isActiveAndEnabled, "Configure an enabled helper explicitly in Play Mode.");
            Require(water && water.gameObject.activeInHierarchy, "An active water MeshRenderer is required.");
            Require(shader && shader.name == ShaderName, "Supply the FlatGpuWater shader asset explicitly.");
            Validate(settings);
            Require(!Owners.TryGetValue(water, out var owner) || !owner || owner == this, "Renderer already has a GPU water owner.");
            Require(water.GetComponent<Collider>() == null && water.GetComponentsInChildren<Collider>(true).Length == 0,
                "GPU water must be visual-only, with no collider on its object or descendants.");
            Require(!HasActiveCpu(water), "Disable the CPU water component first and let OnDisable restore its rest mesh.");
            var filter = water.GetComponent<MeshFilter>();
            Require(filter && filter.sharedMesh, "A static MeshFilter is required; skinned water is not supported.");
            Require(!settings.UseVertexColors || filter.sharedMesh.HasVertexAttribute(VertexAttribute.Color), "Mesh has no authored vertex colors.");
            if (target) Release();

            var materials = water.sharedMaterials;
            Require(materials.Length > 0 && (flatColors == null || flatColors.Length == materials.Length), "Palette must match material slots.");
            var global = new MaterialPropertyBlock(); water.GetPropertyBlock(global);
            var slots = new MaterialPropertyBlock[materials.Length];
            var copies = new Material[materials.Length];
            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Require(materials[i], "Missing source material.");
                    slots[i] = new MaterialPropertyBlock(); water.GetPropertyBlock(slots[i], i);
                    // Unity's per-material block takes precedence over the whole renderer block.
                    var effectiveBlock = slots[i].isEmpty ? global : slots[i];
                    Color color = flatColors != null ? flatColors[i] : ReadColor(materials[i], effectiveBlock);
                    Require(Finite(color.r) && Finite(color.g) && Finite(color.b) && color.r >= 0 && color.g >= 0 && color.b >= 0,
                        "Palette colors must be finite and nonnegative.");
                    copies[i] = new Material(shader) { name = materials[i].name + "_PrivateGpuWater", hideFlags = HideFlags.DontSave };
                    copies[i].SetColor(BaseColor, color);
                    Apply(copies[i], settings);
                }
            }
            catch { DestroyMaterials(copies); throw; }

            target = water; originals = materials; owned = copies; originalBlock = global; originalSlots = slots;
            originalBounds = water.localBounds; originalShadows = water.shadowCastingMode; originalReceiveShadows = water.receiveShadows;
            parameters = settings; Owners[water] = this;
            try
            {
                water.sharedMaterials = copies;
                water.SetPropertyBlock(null);
                for (int i = 0; i < copies.Length; i++) water.SetPropertyBlock(null, i);
                water.shadowCastingMode = ShadowCastingMode.Off; water.receiveShadows = false;
                UpdateBounds();
            }
            catch { Release(); throw; }
        }

        public void SetParameters(Parameters settings)
        {
            Require(target, "Configure the renderer first."); Validate(settings);
            Require(!settings.UseVertexColors || target.GetComponent<MeshFilter>().sharedMesh.HasVertexAttribute(VertexAttribute.Color),
                "Mesh has no authored vertex colors.");
            parameters = settings;
            foreach (var material in owned) Apply(material, settings);
            UpdateBounds();
        }
        public void SetTimeOverride(float seconds)
        {
            var next = parameters; next.UseTimeOverride = true; next.TimeSeconds = seconds; SetParameters(next);
        }
        public void UseGameTime()
        {
            var next = parameters; next.UseTimeOverride = false; SetParameters(next);
        }
        void LateUpdate()
        {
            if (!target) { if (owned != null) Release(); return; }
            if (HasActiveCpu(target))
            {
                Debug.LogError("CPU water was enabled during GPU ownership; releasing GPU water to avoid double deformation.", this);
                Release(); return;
            }
            // Constant work, independent of vertex count; handles changed rotation/scale for world-Y bounds.
            UpdateBounds();
        }
        void UpdateBounds()
        {
            var padding = target.transform.InverseTransformVector(Vector3.up * parameters.Amplitude);
            var bounds = originalBounds;
            bounds.Expand(new Vector3(Mathf.Abs(padding.x), Mathf.Abs(padding.y), Mathf.Abs(padding.z)) * 2);
            target.localBounds = bounds; // Renderer override only; sharedMesh.bounds is never changed.
        }
        public void Release()
        {
            if (target)
            {
                target.sharedMaterials = originals;
                target.SetPropertyBlock(originalBlock.isEmpty ? null : originalBlock);
                for (int i = 0; i < originalSlots.Length; i++)
                    target.SetPropertyBlock(originalSlots[i].isEmpty ? null : originalSlots[i], i);
                target.shadowCastingMode = originalShadows; target.receiveShadows = originalReceiveShadows;
                target.localBounds = originalBounds;
                Owners.Remove(target);
            }
            else
            {
                // Remove a destroyed Unity object key without looking up any other renderer in the scene.
                Renderer stale = null;
                foreach (var pair in Owners) if (pair.Value == this) { stale = pair.Key; break; }
                if (!ReferenceEquals(stale, null)) Owners.Remove(stale);
            }
            DestroyMaterials(owned);
            target = null; owned = originals = null; originalBlock = null; originalSlots = null;
        }
        void OnDisable() => Release();
        void OnDestroy() => Release();

        bool HasActiveCpu(Renderer renderer)
        {
            renderer.GetComponents(componentScratch);
            foreach (var component in componentScratch)
                if (component && component.isActiveAndEnabled && component.GetType().FullName == CpuType) return true;
            return false;
        }
        static Color ReadColor(Material source, MaterialPropertyBlock block)
        {
            int id = source.HasColor(BaseColor) ? BaseColor : LegacyColor;
            Require(source.HasColor(id), "Source lacks BaseColor/Color; supply an explicit flat palette.");
            return block.HasColor(id) ? block.GetColor(id) : source.GetColor(id);
        }
        static void Apply(Material material, Parameters p)
        {
            material.SetFloat("_WaterAmplitude", p.Amplitude); material.SetFloat("_WaterWavelength", p.Wavelength);
            material.SetFloat("_WaterSpeed", p.Speed); material.SetFloat("_WaterPhase", p.Phase);
            material.SetFloat("_WaterTimeOverride", p.TimeSeconds); material.SetFloat("_WaterUseTimeOverride", p.UseTimeOverride ? 1 : 0);
            material.SetFloat("_WaterUseVertexColors", p.UseVertexColors ? 1 : 0);
            material.SetFloat("_WaterUseFog", p.UseFog ? 1 : 0);
        }
        static void Validate(Parameters p)
        {
            Require(Finite(p.Amplitude) && p.Amplitude >= 0 && p.Amplitude <= .15f && Finite(p.Wavelength) && p.Wavelength > 0 &&
                Finite(p.Speed) && Finite(p.Phase) && Finite(p.TimeSeconds), "Invalid wave parameters; amplitude must be 0..0.15 m.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        static void DestroyMaterials(Material[] materials)
        {
            if (materials == null) return;
            foreach (var material in materials) if (material) UnityEngine.Object.Destroy(material);
        }
    }
}
