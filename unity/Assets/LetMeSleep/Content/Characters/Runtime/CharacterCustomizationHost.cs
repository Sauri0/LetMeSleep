using System;
using LetMeSleep.Core.Customization;
using UnityEngine;

namespace LetMeSleep.Content.Characters
{
    /// <summary>Explicit host ownership and rig contract for modular visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCustomizationHost : MonoBehaviour
    {
        public CustomizationRole Role;
        public CharacterView View;
        public string RigId;
        public Vector3 ExpectedVisualScale = Vector3.one;
        [Tooltip("Dedicated child under CharacterView.VisualRoot. The assembler owns its children only.")]
        public Transform PartsRoot;
        [Tooltip("Only these authored renderers may be hidden by a modular base swap.")]
        public Renderer[] OwnedBaseRenderers = Array.Empty<Renderer>();
        [Tooltip("Color channels on authored host renderers, addressed by Color catalog slot.")]
        public CharacterCustomizationPart.ColorChannelBinding[] ColorChannels =
            Array.Empty<CharacterCustomizationPart.ColorChannelBinding>();
    }
}
