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
    }
}
