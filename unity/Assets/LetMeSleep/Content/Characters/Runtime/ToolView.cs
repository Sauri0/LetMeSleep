using UnityEngine;

namespace LetMeSleep.Content.Characters
{
    /// <summary>Authored tool contact locations. Does not apply damage or confirm strike events.</summary>
    [DisallowMultipleComponent]
    public sealed class ToolView : MonoBehaviour
    {
        public string ToolId = "flyswatter";
        public Transform Grip;
        public Transform Impact;
        public float HeadRadius = 0.085f;
        public float GripToImpact = 0.365f;
    }
}
