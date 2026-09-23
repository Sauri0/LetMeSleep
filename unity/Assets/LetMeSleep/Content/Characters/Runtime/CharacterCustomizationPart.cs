using System;
using LetMeSleep.Core.Customization;
using UnityEngine;

namespace LetMeSleep.Content.Characters
{
    public enum CharacterFacialImpact : byte
    {
        None = 0,
        OccludesExistingFace = 1,
        ReplacesTrackedFace = 2
    }

    /// <summary>Declarative bindings carried by a modular visual prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCustomizationPart : MonoBehaviour
    {
        [Serializable]
        public sealed class SkinnedRendererBinding
        {
            public SkinnedMeshRenderer Renderer;
            [Tooltip("Path under CharacterView.Animator.transform. Empty selects the animator transform.")]
            public string RootBonePath;
            [Tooltip("One target-rig path per mesh bind pose, in renderer bone order.")]
            public string[] BonePaths = Array.Empty<string>();
        }

        [Serializable]
        public sealed class SocketBinding
        {
            [Tooltip("Prefab transform reparented below the named CharacterView anchor.")]
            public Transform PartRoot;
            public string AnchorName;
        }

        [Serializable]
        public sealed class ColorChannelBinding
        {
            public Renderer Renderer;
            public int MaterialIndex;
            [Tooltip("Catalog slot whose selected Color option drives this material index.")]
            public string ColorSlotId;
            [Tooltip("0 applies the chosen colour as is; 0.3 applies it 30 % darker (authored shade facets).")]
            [Range(0f, .9f)] public float Shade;
            [Tooltip("0 keeps the chosen colour's alpha; a positive value forces this alpha (translucent membranes).")]
            [Range(0f, 1f)] public float Alpha;
        }

        /// <summary>A renderer of this part that stays hidden while another slot of the same role has a visible
        /// (non-none) selection, e.g. the top of a hairstyle under any hat. Presentation only: the selection, the
        /// catalog fingerprint and the wire format are unchanged.</summary>
        [Serializable]
        public sealed class ConditionalRendererBinding
        {
            public Renderer Renderer;
            [Tooltip("Catalog slot of the same role whose non-none selection hides the renderer.")]
            public string HiddenWhenSlotSelected;
        }

        public CustomizationRole Role;
        public string SlotId;
        public CustomizationOptionKind Kind;
        [Tooltip("Must exactly match CharacterCustomizationHost.RigId.")]
        public string TargetRigId;
        [Tooltip("Must match the host VisualRoot local scale.")]
        public Vector3 TargetVisualScale = Vector3.one;
        public CharacterFacialImpact FacialImpact;
        public SkinnedRendererBinding[] SkinnedRenderers = Array.Empty<SkinnedRendererBinding>();
        public SocketBinding[] SocketParts = Array.Empty<SocketBinding>();
        public ColorChannelBinding[] ColorChannels = Array.Empty<ColorChannelBinding>();
        [Tooltip("Renderers that must follow CharacterView first-person head visibility.")]
        public Renderer[] FirstPersonHeadRenderers = Array.Empty<Renderer>();
        [Tooltip("Renderers hidden while another slot of the role has a non-none selection (hair under hats).")]
        public ConditionalRendererBinding[] ConditionalRenderers = Array.Empty<ConditionalRendererBinding>();
    }
}
