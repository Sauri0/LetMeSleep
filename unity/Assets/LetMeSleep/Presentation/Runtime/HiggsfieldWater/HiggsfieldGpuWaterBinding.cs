using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Explicit serialized map binding; references survive prefab/build loading.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HiggsfieldGpuWater))]
    public sealed class HiggsfieldGpuWaterBinding : MonoBehaviour
    {
        public MeshRenderer Water;
        public Shader WaterShader;
        public HiggsfieldGpuWater.Parameters Settings = HiggsfieldGpuWater.Parameters.Default;
        HiggsfieldGpuWater helper;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            helper = GetComponent<HiggsfieldGpuWater>();
            helper.Configure(Water, WaterShader, Settings);
        }
        void OnDisable() { if (helper) helper.Release(); }
        void OnDestroy() { if (helper) helper.Release(); }
    }
}
