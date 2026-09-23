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
            [Tooltip("0 tints with the chosen colour. A positive value keeps the material's own colour and only dims " +
                     "it with the chosen colour's brightness, down to this factor (eye whites follow the skin tone).")]
            [Range(0f, 1f)] public float LuminanceFloor;
            [Tooltip("Host channels only: while this catalog slot has a non-none selection the material index is " +
                     "alpha-clipped away (its material must use alpha clipping), e.g. the authored hair under any hat.")]
            public string HiddenWhileSlotSelected;
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
            [Tooltip("Inverts the rule: the renderer shows ONLY while that slot has a non-none selection (the flat " +
                     "'under a hat' variant of a hairstyle).")]
            public bool ShowOnlyWhileSelected;
        }

        /// <summary>A host anchor that follows a point of this part instead of its source bone head while the part is
        /// worn, e.g. ProboscisTip on the real tip of a shorter or longer proboscis, so the bite contact (and anything
        /// else reading the anchor) touches with the visible tip. Presentation only.</summary>
        [Serializable]
        public sealed class AnchorOffsetBinding
        {
            [Tooltip("Name of an existing CharacterView anchor.")]
            public string AnchorName;
            [Tooltip("Point in the anchor's source bone local space (target rig units).")]
            public Vector3 LocalPosition;
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
        [Tooltip("Renderers hidden (or, inverted, shown only) while another slot of the role has a non-none selection " +
                 "(hair under hats).")]
        public ConditionalRendererBinding[] ConditionalRenderers = Array.Empty<ConditionalRendererBinding>();
        [Tooltip("Host anchors moved onto points of this part while it is worn (one part per anchor).")]
        public AnchorOffsetBinding[] AnchorOffsets = Array.Empty<AnchorOffsetBinding>();
    }
}
