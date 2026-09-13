using UnityEngine;

namespace LetMeSleep.Content.Environment.Higgsfield
{
    /// <summary>Visual-only waves on a private copy of the authored mesh. No new faces or collider changes.</summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldLowPolyWater : MonoBehaviour
    {
        public const int MaximumAnimatedVertices = 20000;
        [Range(0, .15f)] public float Amplitude = .025f;
        [Min(.1f)] public float Wavelength = 4f;
        public float Speed = .65f;
        public string WaveA = "Wave_A", WaveB = "Wave_B";
        MeshFilter filter;
        SkinnedMeshRenderer skinned;
        int shapeA = -1, shapeB = -1;
        float originalA, originalB;
        bool weightsSaved;
        Mesh original, animated;
        Vector3[] rest, displaced;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            skinned = GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                shapeA = FindShape(skinned.sharedMesh, WaveA);
                shapeB = FindShape(skinned.sharedMesh, WaveB);
                if (shapeA < 0 || shapeB < 0 || shapeA == shapeB || skinned.bones.Length != 0 || GetComponent<Collider>() != null)
                {
                    Debug.LogError("Higgsfield skinned water requires two authored wave blendshapes, no bones and no collider.", this);
                    enabled = false; return;
                }
                originalA = skinned.GetBlendShapeWeight(shapeA); originalB = skinned.GetBlendShapeWeight(shapeB);
                weightsSaved = true;
                return;
            }
            filter = GetComponent<MeshFilter>();
            original = filter == null ? null : filter.sharedMesh;
            if (original == null || !original.isReadable || original.vertexCount > MaximumAnimatedVertices || GetComponent<Collider>() != null)
            {
                Debug.LogError("Higgsfield water requires a readable visual mesh, <=20000 vertices and no collider on the same object.", this);
                enabled = false;
                return;
            }
            animated = Instantiate(original);
            animated.name = original.name + "_RuntimeWater";
            animated.MarkDynamic();
            rest = original.vertices;
            displaced = new Vector3[rest.Length];
            filter.sharedMesh = animated;
        }
        void Update()
        {
            if (skinned != null)
            {
                float weight = (Mathf.Sin(Time.time * Speed) + 1f) * 50f;
                skinned.SetBlendShapeWeight(shapeA, weight);
                skinned.SetBlendShapeWeight(shapeB, 100f - weight);
                return;
            }
            if (animated == null) return;
            float k = 2f * Mathf.PI / Mathf.Max(.1f, Wavelength);
            float phase = Time.time * Speed;
            Vector3 localUp = transform.InverseTransformVector(Vector3.up);
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 world = transform.TransformPoint(rest[i]);
                float height = Mathf.Clamp(Amplitude, 0, .15f) *
                    (.65f * Mathf.Sin((world.x + world.z * .37f) * k + phase) +
                     .35f * Mathf.Sin((world.z - world.x * .21f) * k * .71f + phase * .83f));
                displaced[i] = rest[i] + localUp * height;
            }
            animated.vertices = displaced;
            animated.RecalculateNormals();
            animated.RecalculateBounds();
        }
        void OnDisable()
        {
            if (weightsSaved && skinned != null && shapeA >= 0 && shapeB >= 0 && shapeA != shapeB)
            {
                skinned.SetBlendShapeWeight(shapeA, originalA); skinned.SetBlendShapeWeight(shapeB, originalB);
            }
            skinned = null; shapeA = shapeB = -1; weightsSaved = false;
            if (filter != null && animated != null && filter.sharedMesh == animated) filter.sharedMesh = original;
            if (animated != null) Destroy(animated);
            animated = null;
            rest = displaced = null;
        }
        public static int FindShape(Mesh mesh, string name)
        {
            if (mesh == null || string.IsNullOrEmpty(name)) return -1;
            int match = -1;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (mesh.GetBlendShapeName(i) == name || mesh.GetBlendShapeName(i).EndsWith("." + name, System.StringComparison.Ordinal))
                { if (match >= 0) return -1; match = i; }
            return match;
        }
    }
}
