using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 runtime atmosphere of a decoration prefab (map Decor, lobby decor, menu bedroom): camera-facing halos on
    /// lamps and lanterns, string lights (optionally one small halo per bulb), warm ground pools and soft flicker on the
    /// prefab's own Point/Spot lights. The procedural meshes of <see cref="HiggsfieldAtmosphereVisuals"/> are never saved,
    /// so they are generated here when the decor is enabled and destroyed with it. Visual only: no Collider, no Rigidbody,
    /// no shadows; every generated object lives under one child named <see cref="GeneratedName"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HiggsfieldDecorDressing : MonoBehaviour
    {
        public const string GeneratedName = "_LMS_DecorAtmosphere";
        public const int MaximumHalos = 96;
        public const int MaximumGarlands = 12;
        public const int MaximumPools = 16;

        [Serializable]
        public sealed class Halo
        {
            public Transform Anchor;
            [Tooltip("World-axis offset from the anchor (anchors may be rotated).")]
            public Vector3 Offset;
            public float Size = 0.8f;
            [Tooltip("sRGB; alpha is the halo opacity.")]
            public Color Color = new Color(1f, 0.702f, 0.278f, 0.35f);
            public float Intensity = 1f;
        }

        [Serializable]
        public sealed class Flicker
        {
            public Light Light;
            [Range(0f, HiggsfieldLightFlicker.MaximumAmplitude)] public float Amplitude = 0.12f;
            public float Seed;
        }

        [Serializable]
        public sealed class Garland
        {
            [Tooltip("Local space of this component's transform.")]
            public Vector3 From, To;
            public float Sag = 0.3f;
            public int Bulbs = 12;
            public float BulbRadius = 0.034f;
            [Tooltip("0 = no per-bulb halo.")]
            public float BulbHaloSize = 0f;
            public Color BulbHaloColor = new Color(1f, 0.78f, 0.45f, 0.3f);
        }

        [Serializable]
        public sealed class Pool
        {
            [Tooltip("Ground point, local space of this component's transform.")]
            public Vector3 Ground;
            public float Radius = 1.5f;
            public Color Color = new Color(1f, 0.6f, 0.3f, 0.35f);
        }

        public HiggsfieldAtmosphereKit Kit;
        public Halo[] Halos = Array.Empty<Halo>();
        public Flicker[] Flickers = Array.Empty<Flicker>();
        public Garland[] Garlands = Array.Empty<Garland>();
        public Pool[] Pools = Array.Empty<Pool>();

        private GameObject generated;
        private readonly List<HiggsfieldLightFlicker> flickers = new List<HiggsfieldLightFlicker>();

        public GameObject Generated => generated;

        private void OnEnable() => Build();

        private void OnDestroy() => Clear();

        /// <summary>Creates the atmosphere once; a second call is a no-op while the generated root exists.</summary>
        public void Build()
        {
            if (generated) return;
            if (!Kit || !Kit.HaloMaterial) return; // Decor without a kit keeps only its meshes and lights.
            generated = new GameObject(GeneratedName);
            generated.transform.SetParent(transform, false);
            int halos = 0;
            foreach (var halo in Halos ?? Array.Empty<Halo>())
            {
                if (halo == null || !halo.Anchor || halos >= MaximumHalos) continue;
                // Decor is static once instantiated: the halo is placed in world space under the generated root.
                HiggsfieldAtmosphereVisuals.CreateHalo(generated.transform, halo.Anchor.position - generated.transform.position + halo.Offset,
                    halo.Size, halo.Color, halo.Intensity, Kit.HaloMaterial);
                halos++;
            }
            int garlands = 0;
            foreach (var garland in Garlands ?? Array.Empty<Garland>())
            {
                if (garland == null || garlands >= MaximumGarlands || !Kit.BulbMaterial || !Kit.WireMaterial) continue;
                var strand = HiggsfieldAtmosphereVisuals.CreateStringLights(generated.transform, garland.From, garland.To, garland.Sag,
                    garland.Bulbs, Kit, garland.BulbRadius);
                garlands++;
                if (garland.BulbHaloSize <= 0f) continue;
                foreach (Transform piece in strand.transform)
                {
                    if (piece.name != "Bulb" || halos >= MaximumHalos) continue;
                    HiggsfieldAtmosphereVisuals.CreateHalo(piece, Vector3.zero, garland.BulbHaloSize, garland.BulbHaloColor, 1f, Kit.HaloMaterial).name =
                        AlfaLightingRig.BulbHaloName;
                    halos++;
                }
            }
            int pools = 0;
            if (Kit.PoolMaterial)
                foreach (var pool in Pools ?? Array.Empty<Pool>())
                {
                    if (pool == null || pools >= MaximumPools) continue;
                    HiggsfieldAtmosphereVisuals.CreatePool(generated.transform, transform.TransformPoint(pool.Ground), pool.Radius, pool.Color, Kit.PoolMaterial);
                    pools++;
                }
            foreach (var flicker in Flickers ?? Array.Empty<Flicker>())
            {
                if (flicker == null || !flicker.Light || !flicker.Light.transform.IsChildOf(transform)) continue;
                var component = flicker.Light.GetComponent<HiggsfieldLightFlicker>();
                if (!component) component = flicker.Light.gameObject.AddComponent<HiggsfieldLightFlicker>();
                component.Configure(flicker.Amplitude, flicker.Seed);
                flickers.Add(component);
            }
        }

        public void Clear()
        {
            foreach (var flicker in flickers) if (flicker) Destroy(flicker);
            flickers.Clear();
            if (generated) { generated.SetActive(false); Destroy(generated); }
            generated = null;
        }
    }
}
