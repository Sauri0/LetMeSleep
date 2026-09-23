using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>v0.3.0 r3: turns a lighthouse beam around the world vertical axis (visual only, no physics).</summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldBeamSweep : MonoBehaviour
    {
        public float DegreesPerSecond = 24f;
        public float Phase;

        private void LateUpdate()
        {
            float angle = Mathf.Repeat(Phase + Time.time * DegreesPerSecond, 360f);
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
        }
    }
}
