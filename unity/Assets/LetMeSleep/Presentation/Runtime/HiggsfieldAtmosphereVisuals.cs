using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// Procedural, visual-only atmosphere props for v0.3.0 (sketch look of ENV-03/ENV-04/UI-06):
    /// camera-facing halos around lanterns and sconces, three-layer low-poly flames (outer red-orange,
    /// middle orange, yellow core) and lobby string lights. Every object is renderer-only: no Collider,
    /// no Rigidbody, no shadows, no probes, so it never becomes gameplay geometry (IsWorldCollider) and
    /// costs one draw per piece. Meshes are generated once and shared (never saved).
    /// </summary>
    public static class HiggsfieldAtmosphereVisuals
    {
        public const string HaloName = "Higgsfield_Halo";
        public const string FlameName = "Higgsfield_Flame";
        public const string StringLightsName = "Higgsfield_StringLights";

        private static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        private static readonly int HaloIntensityId = Shader.PropertyToID("_HaloIntensity");
        private static readonly int FlameSeedId = Shader.PropertyToID("_FlameSeed");
        private static Mesh haloQuad, bulb, box;
        private static readonly Mesh[] flameLayers = new Mesh[3];

        /// <param name="offset">World-axis offset from the parent position (imported anchors may be rotated).</param>
        /// <param name="color">sRGB color; alpha is the halo opacity (0..1).</param>
        public static GameObject CreateHalo(Transform parent, Vector3 offset, float size, Color color, float intensity, Material material)
        {
            var go = NewUpright(HaloName, parent, offset);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * Mathf.Max(0.01f, size));
            var renderer = Render(go, HaloQuad, material);
            var block = new MaterialPropertyBlock();
            block.SetColor(HaloColorId, color);
            block.SetFloat(HaloIntensityId, Mathf.Max(0f, intensity));
            renderer.SetPropertyBlock(block);
            return go;
        }

        /// <summary>Three nested flame layers; <paramref name="height"/> is the outer flame height in metres.</summary>
        public static GameObject CreateFlame(Transform parent, Vector3 offset, float height, HiggsfieldAtmosphereKit kit, float seed)
        {
            var root = NewUpright(FlameName, parent, offset);
            root.transform.localScale = Vector3.Scale(root.transform.localScale, Vector3.one * Mathf.Max(0.05f, height));
            Material[] materials = { kit.FlameOuter, kit.FlameMiddle, kit.FlameCore };
            for (int layer = 0; layer < 3; layer++)
            {
                var piece = NewPiece(FlameName + "_" + layer, root.transform, Vector3.zero);
                var renderer = Render(piece, FlameLayer(layer), materials[layer]);
                var block = new MaterialPropertyBlock();
                block.SetFloat(FlameSeedId, seed + layer * 1.37f);
                renderer.SetPropertyBlock(block);
            }
            return root;
        }

        /// <summary>A sagging strand of warm bulbs between two points (parent space).</summary>
        public static GameObject CreateStringLights(Transform parent, Vector3 from, Vector3 to, float sag, int bulbs,
            HiggsfieldAtmosphereKit kit, float bulbRadius = 0.045f)
        {
            var root = NewPiece(StringLightsName, parent, Vector3.zero);
            bulbs = Mathf.Clamp(bulbs, 2, 64);
            int segments = bulbs * 3;
            Vector3 previous = Catenary(from, to, sag, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = Catenary(from, to, sag, i / (float)segments);
                Vector3 delta = next - previous;
                if (delta.sqrMagnitude > 1e-6f)
                {
                    var wire = NewPiece("Wire", root.transform, (previous + next) * 0.5f);
                    wire.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    wire.transform.localScale = new Vector3(0.012f, 0.012f, delta.magnitude + 0.004f);
                    Render(wire, Box, kit.WireMaterial);
                }
                previous = next;
            }
            for (int i = 0; i < bulbs; i++)
            {
                float t = (i + 0.5f) / bulbs;
                var piece = NewPiece("Bulb", root.transform, Catenary(from, to, sag, t) + Vector3.down * (bulbRadius * 1.4f));
                piece.transform.localScale = new Vector3(bulbRadius, bulbRadius * 1.3f, bulbRadius);
                Render(piece, Bulb, kit.BulbMaterial);
            }
            return root;
        }

        public static Vector3 Catenary(Vector3 from, Vector3 to, float sag, float t) =>
            Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));

        /// <summary>Child of <paramref name="parent"/> at parent position + world offset, world-upright, unit world scale.</summary>
        private static GameObject NewUpright(string name, Transform parent, Vector3 worldOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(parent.position + worldOffset, Quaternion.identity);
            Vector3 lossy = parent.lossyScale;
            go.transform.localScale = new Vector3(SafeInverse(lossy.x), SafeInverse(lossy.y), SafeInverse(lossy.z));
            return go;
        }

        private static float SafeInverse(float value) => Mathf.Abs(value) > 1e-4f ? 1f / Mathf.Abs(value) : 1f;

        private static GameObject NewPiece(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static MeshRenderer Render(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        // ---------- Meshes ----------
        public static Mesh HaloQuad
        {
            get
            {
                if (haloQuad) return haloQuad;
                haloQuad = new Mesh { name = "Higgsfield_HaloQuad", hideFlags = HideFlags.DontSave };
                haloQuad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
                haloQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                haloQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                // The shader turns the quad toward the camera: cull against a cube, not the flat quad.
                haloQuad.bounds = new Bounds(Vector3.zero, Vector3.one * 1.2f);
                return haloQuad;
            }
        }

        public static Mesh FlameLayer(int layer)
        {
            layer = Mathf.Clamp(layer, 0, 2);
            if (flameLayers[layer]) return flameLayers[layer];
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // (angle degrees, distance, height, radius, lean) in units of the outer flame height.
            float[][] tongues = layer == 0
                ? new[] { new[] { 0f, 0f, 1f, .30f, .04f }, new[] { 25f, .17f, .64f, .17f, .10f }, new[] { 115f, .18f, .72f, .18f, .10f },
                          new[] { 205f, .16f, .58f, .16f, .10f }, new[] { 295f, .18f, .68f, .17f, .10f } }
                : layer == 1
                    ? new[] { new[] { 20f, .015f, .74f, .22f, .03f }, new[] { 70f, .10f, .48f, .13f, .07f }, new[] { 190f, .10f, .52f, .13f, .07f },
                              new[] { 310f, .10f, .46f, .12f, .07f } }
                    : new[] { new[] { 40f, .01f, .44f, .14f, .02f }, new[] { 110f, .06f, .30f, .09f, .05f }, new[] { 280f, .06f, .28f, .09f, .05f } };
            foreach (var t in tongues)
                AddTongue(vertices, triangles, t[0] * Mathf.Deg2Rad, t[1], t[2], t[3], t[4], 5);
            var mesh = new Mesh { name = "Higgsfield_FlameLayer" + layer, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            // Room for the shader's sway and flicker.
            mesh.bounds = new Bounds(new Vector3(0, 0.6f, 0), new Vector3(1.1f, 1.4f, 1.1f));
            return flameLayers[layer] = mesh;
        }

        private static void AddTongue(List<Vector3> v, List<int> tris, float angle, float distance, float height, float radius, float lean, int sides)
        {
            var center = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
            var outward = distance > 1e-4f ? center.normalized : new Vector3(1, 0, 0);
            float[] ringY = { 0f, .28f, .6f };
            float[] ringR = { .78f, 1f, .55f };
            int start = v.Count;
            for (int ring = 0; ring < ringY.Length; ring++)
            {
                float y = ringY[ring] * height;
                Vector3 offset = center + outward * (lean * ringY[ring] * height);
                for (int s = 0; s < sides; s++)
                {
                    float a = angle + s * Mathf.PI * 2f / sides + ring * 0.35f;
                    v.Add(offset + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius * ringR[ring]) + Vector3.up * y);
                }
            }
            int tip = v.Count;
            v.Add(center + outward * (lean * height * 1.6f) + Vector3.up * height);
            for (int ring = 0; ring < ringY.Length - 1; ring++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + ring * sides + s, b = start + ring * sides + (s + 1) % sides;
                    int c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            int top = start + (ringY.Length - 1) * sides;
            for (int s = 0; s < sides; s++)
            {
                tris.Add(top + s); tris.Add(tip); tris.Add(top + (s + 1) % sides);
            }
        }

        public static Mesh Bulb
        {
            get
            {
                if (bulb) return bulb;
                // Low-poly octahedron.
                bulb = new Mesh { name = "Higgsfield_Bulb", hideFlags = HideFlags.DontSave };
                bulb.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
                bulb.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
                bulb.RecalculateNormals();
                bulb.RecalculateBounds();
                return bulb;
            }
        }

        public static Mesh Box
        {
            get
            {
                if (box) return box;
                box = new Mesh { name = "Higgsfield_Box", hideFlags = HideFlags.DontSave };
                var p = new List<Vector3>();
                var t = new List<int>();
                Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var normal in n)
                {
                    Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up);
                    Vector3 w = Vector3.Cross(normal, u);
                    int s = p.Count;
                    p.Add((normal - u - w) * .5f); p.Add((normal + u - w) * .5f); p.Add((normal + u + w) * .5f); p.Add((normal - u + w) * .5f);
                    t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
                }
                box.SetVertices(p);
                box.SetTriangles(t, 0);
                box.RecalculateNormals();
                box.RecalculateBounds();
                return box;
            }
        }
    }
}
