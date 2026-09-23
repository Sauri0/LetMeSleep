using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Content.Characters
{
    /// <summary>Visual rig bindings only. Gameplay owns motion, hits and authoritative state.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CharacterView : MonoBehaviour
    {
        [Serializable]
        public sealed class AnchorBinding
        {
            public string Name;
            public Transform Anchor;
            public Transform SourceBone;
            public Quaternion RotationOffset = Quaternion.identity;
        }

        [Serializable]
        public sealed class ColorBinding
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public string Category;
            /// <summary>0 applies the chosen colour as is; 0.3 applies it 30 % darker. Authored shade
            /// facets (e.g. Mosquito_ShellShade/Dark/Deep) keep their contrast when the colour changes.</summary>
            public float Shade;
            /// <summary>0 keeps the alpha of the chosen colour; a positive value forces it (a wing membrane keeps its
            /// authored translucency whatever the colour swatch).</summary>
            public float Alpha;
            /// <summary>0 tints with the chosen colour. A positive value keeps the material's own colour and dims it
            /// with the chosen colour's brightness (relative to the authored skin), never below this factor: eye
            /// whites stay white on light skin and are a little darker on dark skin, so they never glow at night.</summary>
            public float LuminanceFloor;
            /// <summary>Alpha 0 on this material index (clipped away by an alpha-clip material), e.g. the authored
            /// hair under a hat, which a hairstyle part replaces.</summary>
            public bool Hidden;
        }

        [Serializable]
        public sealed class MotionBinding
        {
            public int Id;
            public string StateName;
            public string ClipName;
            public bool Loop;
        }

        public Animator Animator;
        public Transform VisualRoot;
        public Collider HitVolume;
        public Renderer[] HeadRenderers = Array.Empty<Renderer>();
        public AnchorBinding[] Anchors = Array.Empty<AnchorBinding>();
        public ColorBinding[] Colors = Array.Empty<ColorBinding>();
        public MotionBinding[] Motions = Array.Empty<MotionBinding>();
        [SerializeField] private bool firstPerson;
        private MaterialPropertyBlock colorBlock;
        private Renderer[] authoredHeadRenderers;
        private ColorBinding[] authoredColors;
        private Dictionary<Component, RuntimeBindings> customizationBindings;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // Linear luminance of the authored skin (#C98B5A): eye whites dimmed by LuminanceFloor bindings keep their full
        // white at this skin tone and lighter ones.
        private const float ReferenceSkinLuminance = .316f;
        private static readonly int Motion = UnityEngine.Animator.StringToHash("Motion");

        private sealed class RuntimeBindings
        {
            public Renderer[] Heads;
            public ColorBinding[] Colors;
            public KeyValuePair<string, Vector3>[] AnchorOffsets;
        }

        // Anchor name -> point in its source bone's local space, supplied by worn customization parts.
        private Dictionary<string, Vector3> anchorOffsets;

        public bool IsFirstPerson => firstPerson;
        private void OnEnable() { SetFirstPersonVisibility(firstPerson); RefreshAnchors(); }
        private void LateUpdate() => RefreshAnchors();

        public void SetFirstPersonVisibility(bool enabled)
        {
            firstPerson = enabled;
            foreach (var renderer in HeadRenderers)
            {
                if (renderer == null) continue;
                renderer.shadowCastingMode = enabled ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        public Transform GetAnchor(string name)
        {
            foreach (var binding in Anchors) if (binding.Name == name) return binding.Anchor;
            return null;
        }

        public void RefreshAnchors()
        {
            foreach (var binding in Anchors)
            {
                if (binding.Anchor == null || binding.SourceBone == null) continue;
                var position = anchorOffsets != null && anchorOffsets.TryGetValue(binding.Name, out var local)
                    ? binding.SourceBone.TransformPoint(local) : binding.SourceBone.position;
                binding.Anchor.SetPositionAndRotation(position, binding.SourceBone.rotation * binding.RotationOffset);
            }
        }

        // StateName values and stable IDs are serialized into the prefab and build receipt.
        public bool PlayMotion(int id, float crossFadeSeconds = 0.1f)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return false;
            foreach (var motion in Motions)
            {
                if (motion.Id != id) continue;
                Animator.SetInteger(Motion, id);
                Animator.CrossFadeInFixedTime(motion.StateName, Mathf.Max(0, crossFadeSeconds), 0);
                return true;
            }
            return false;
        }

        public void SetSkinColor(Color color) => SetColor("Skin", color);
        public void SetPajamaColor(Color color) => SetColor("Pajamas", color);
        public void SetMosquitoColor(Color color) => SetColor("Mosquito", color);
        public void ApplyColor(string channel, Color color) => SetColor(channel, color);

        internal void SetCustomizationBindings(Component owner, Renderer[] heads, ColorBinding[] colors) =>
            SetCustomizationBindings(owner, heads, colors, null);

        internal void SetCustomizationBindings(Component owner, Renderer[] heads, ColorBinding[] colors,
            KeyValuePair<string, Vector3>[] anchorOffsetsByName)
        {
            if (ReferenceEquals(owner, null)) throw new ArgumentNullException(nameof(owner));
            if (customizationBindings == null)
            {
                authoredHeadRenderers = (HeadRenderers ?? Array.Empty<Renderer>()).ToArray();
                authoredColors = (Colors ?? Array.Empty<ColorBinding>()).ToArray();
                customizationBindings = new Dictionary<Component, RuntimeBindings>();
            }
            customizationBindings[owner] = new RuntimeBindings
            {
                Heads = (heads ?? Array.Empty<Renderer>()).Where(item => item != null).Distinct().ToArray(),
                Colors = (colors ?? Array.Empty<ColorBinding>()).Where(item => item != null).ToArray(),
                AnchorOffsets = (anchorOffsetsByName ?? Array.Empty<KeyValuePair<string, Vector3>>())
                    .Where(item => !string.IsNullOrEmpty(item.Key)).ToArray()
            };
            RebuildCustomizationBindings();
        }

        internal void ClearCustomizationBindings(Component owner)
        {
            if (ReferenceEquals(owner, null) || customizationBindings == null || !customizationBindings.Remove(owner)) return;
            if (customizationBindings.Count == 0)
            {
                HeadRenderers = authoredHeadRenderers ?? Array.Empty<Renderer>();
                Colors = authoredColors ?? Array.Empty<ColorBinding>();
                authoredHeadRenderers = null;
                authoredColors = null;
                customizationBindings = null;
                anchorOffsets = null;
                SetFirstPersonVisibility(firstPerson);
                RefreshAnchors();
                return;
            }
            RebuildCustomizationBindings();
        }

        private void RebuildCustomizationBindings()
        {
            HeadRenderers = (authoredHeadRenderers ?? Array.Empty<Renderer>())
                .Concat(customizationBindings.Values.SelectMany(item => item.Heads)).Where(item => item != null)
                .Distinct().ToArray();
            Colors = (authoredColors ?? Array.Empty<ColorBinding>())
                .Concat(customizationBindings.Values.SelectMany(item => item.Colors)).Where(item => item != null).ToArray();
            anchorOffsets = null;
            foreach (var pair in customizationBindings.Values.SelectMany(item => item.AnchorOffsets ?? Array.Empty<KeyValuePair<string, Vector3>>()))
            {
                if (anchorOffsets == null) anchorOffsets = new Dictionary<string, Vector3>(StringComparer.Ordinal);
                anchorOffsets[pair.Key] = pair.Value;
            }
            SetFirstPersonVisibility(firstPerson);
            RefreshAnchors();
        }

        /// <summary>True while a worn customization part moves this anchor off its source bone head.</summary>
        public bool TryGetAnchorOffset(string name, out Vector3 localPosition)
        {
            localPosition = Vector3.zero;
            return anchorOffsets != null && anchorOffsets.TryGetValue(name, out localPosition);
        }

        private void SetColor(string category, Color color)
        {
            if (colorBlock == null) colorBlock = new MaterialPropertyBlock();
            foreach (var binding in Colors)
            {
                if (binding.Category != category || binding.Renderer == null) continue;
                binding.Renderer.GetPropertyBlock(colorBlock, binding.MaterialIndex);
                float keep = 1 - Mathf.Clamp01(binding.Shade);
                float alpha = binding.Alpha > 0 ? Mathf.Clamp01(binding.Alpha) : color.a;
                var tint = color;
                if (binding.LuminanceFloor > 0)
                {
                    var materials = binding.Renderer.sharedMaterials;
                    var own = binding.MaterialIndex < materials.Length && materials[binding.MaterialIndex] != null &&
                              materials[binding.MaterialIndex].HasProperty(BaseColor)
                        ? materials[binding.MaterialIndex].GetColor(BaseColor) : Color.white;
                    var linear = color.linear;
                    float luminance = .2126f * linear.r + .7152f * linear.g + .0722f * linear.b;
                    float factor = Mathf.Clamp(Mathf.Pow(luminance / ReferenceSkinLuminance, .8f), Mathf.Clamp01(binding.LuminanceFloor), 1f);
                    tint = (own.linear * factor).gamma;
                    alpha = own.a;
                }
                if (binding.Hidden) alpha = 0f;
                colorBlock.SetColor(BaseColor, new Color(tint.r * keep, tint.g * keep, tint.b * keep, alpha));
                binding.Renderer.SetPropertyBlock(colorBlock, binding.MaterialIndex);
                colorBlock.Clear();
            }
        }
    }
}
