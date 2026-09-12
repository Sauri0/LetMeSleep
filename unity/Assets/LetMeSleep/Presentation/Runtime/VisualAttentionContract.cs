using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Builder-authored evidence marker on the exact new visual prefab; defaults deliberately reject installation.</summary>
    [DisallowMultipleComponent]
    public sealed class VisualAttentionContract : MonoBehaviour
    {
        public const string SupportedSchema="lms.visual-attention.v1";
        public string Schema;
        public string RigRevision;
        public string SourceSha256;
        public bool UnityAxesVerified;
        public bool LegacyScaleBlinkVerified;
        public VisualAttentionRig.Bindings Rig;
        [NonSerialized] internal VisualAttentionRig Installed;
    }
}
