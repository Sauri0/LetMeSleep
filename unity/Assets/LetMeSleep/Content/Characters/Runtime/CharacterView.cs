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
        private static readonly int Motion = UnityEngine.Animator.StringToHash("Motion");

        private sealed class RuntimeBindings
        {
            public Renderer[] Heads;
            public ColorBinding[] Colors;
        }

        public bool IsFirstPerson => firstPerson;
        private bool headShownToCamera;
        private void OnEnable()
        {
            SetFirstPersonVisibility(firstPerson); RefreshAnchors();
            RenderPipelineManager.beginCameraRendering += ShowHeadToOtherCameras;
            RenderPipelineManager.endCameraRendering += HideHeadAfterCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ShowHeadToOtherCameras;
            RenderPipelineManager.endCameraRendering -= HideHeadAfterCamera;
            if (headShownToCamera) { headShownToCamera = false; SetFirstPersonVisibility(firstPerson); }
        }
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

        // v0.3.0 (maps director #9): first person hides the head only from the camera that sits inside it. Free, spectator
        // and capture cameras render the local human with its head; the head keeps casting shadows for every camera, and
        // between renders the renderers stay in the first-person (shadow-only) state.
        private void ShowHeadToOtherCameras(ScriptableRenderContext context, Camera camera)
        {
            if (!firstPerson || camera == null || HeadRenderers == null || HeadRenderers.Length == 0) return;
            bool any = false;
            var head = new Bounds();
            foreach (var renderer in HeadRenderers)
            {
                if (renderer == null) continue;
                if (!any) { head = renderer.bounds; any = true; } else head.Encapsulate(renderer.bounds);
            }
            if (!any) return;
            head.Expand(0.3f);
            if (head.Contains(camera.transform.position)) return; // The first-person eye.
            foreach (var renderer in HeadRenderers)
                if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.On;
            headShownToCamera = true;
        }

        private void HideHeadAfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!headShownToCamera) return;
            headShownToCamera = false;
            SetFirstPersonVisibility(firstPerson);
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
                binding.Anchor.SetPositionAndRotation(binding.SourceBone.position,
                    binding.SourceBone.rotation * binding.RotationOffset);
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

        internal void SetCustomizationBindings(Component owner, Renderer[] heads, ColorBinding[] colors)
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
                Colors = (colors ?? Array.Empty<ColorBinding>()).Where(item => item != null).ToArray()
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
                SetFirstPersonVisibility(firstPerson);
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
            SetFirstPersonVisibility(firstPerson);
        }

        private void SetColor(string category, Color color)
        {
            if (colorBlock == null) colorBlock = new MaterialPropertyBlock();
            foreach (var binding in Colors)
            {
                if (binding.Category != category || binding.Renderer == null) continue;
                binding.Renderer.GetPropertyBlock(colorBlock, binding.MaterialIndex);
                float keep = 1 - Mathf.Clamp01(binding.Shade);
                colorBlock.SetColor(BaseColor, new Color(color.r * keep, color.g * keep, color.b * keep, color.a));
                binding.Renderer.SetPropertyBlock(colorBlock, binding.MaterialIndex);
                colorBlock.Clear();
            }
        }
    }
}
