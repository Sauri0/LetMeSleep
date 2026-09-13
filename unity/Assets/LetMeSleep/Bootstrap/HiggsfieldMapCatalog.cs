using System;
using System.Collections.Generic;
using LetMeSleep.Content.Environment;
using LetMeSleep.Presentation;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    /// <summary>
    /// Bootstrap-owned registry. No assets/entries are generated automatically.
    /// Validate -> Resolve(id) -> instantiate entry.Prefab -> ResolveLighting(id, instance)
    /// -> AlfaLightingRig.BindHiggsfield(instance.transform, resolved).
    /// Call UnbindHiggsfield before unloading the map/returning to the lobby.
    /// </summary>
    [CreateAssetMenu(menuName = "Let me sleep/Higgsfield Map Catalog", fileName = "HiggsfieldMapCatalog")]
    public sealed class HiggsfieldMapCatalog : ScriptableObject
    {
        // A known ID, not an installed entry; the other four IDs must come from their verified imports.
        public const string IslandMapId = "hf-isla-del-laguito-v2";

        [Serializable] public sealed class LocalLightBinding
        {
            public string AnchorPath;
            // Anchor must stay null in the asset: it is resolved on the instantiated map.
            public HiggsfieldMapLighting.LocalSource Settings = new HiggsfieldMapLighting.LocalSource();
        }

        [Serializable] public sealed class Entry
        {
            public string MapId;
            public string DisplayName;
            // Zero preserves the role preset. Explicit opt-in, metres.
            public float CameraFarPlane;
            public EnvironmentMapDefinition Prefab;
            // LocalLights and SuppressLights must remain empty; use the relative bindings below.
            public HiggsfieldMapLighting.Configuration Lighting = new HiggsfieldMapLighting.Configuration();
            public LocalLightBinding[] LocalLights = Array.Empty<LocalLightBinding>();
            public string[] SuppressLightPaths = Array.Empty<string>();
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public int Count => entries == null ? 0 : entries.Length;
        public IReadOnlyList<Entry> Entries { get { Validate(); return Array.AsReadOnly(entries); } }

        /// <summary>Reject invalid entries anywhere in the catalog, including duplicate ordinal IDs.</summary>
        public void Validate()
        {
            if (entries == null) throw new InvalidOperationException("Catalog entries are not initialized.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.MapId) || entry.MapId != entry.MapId.Trim() || !ids.Add(entry.MapId))
                    throw new InvalidOperationException("Catalog has an empty, padded or duplicate map ID.");
                if (string.IsNullOrWhiteSpace(entry.DisplayName)) throw new InvalidOperationException("Map display name is required: " + entry.MapId);
                if (!entry.Prefab || entry.Prefab.gameObject.scene.IsValid() ||
                    !string.Equals(entry.MapId, entry.Prefab.MapId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.Prefab.ContentHash))
                    throw new InvalidOperationException("Missing prefab/content identity or mismatched map ID: " + entry.MapId);
                if (entry.MapId == IslandMapId && entry.Lighting != null && entry.Lighting.MapId != HiggsfieldMapLighting.Map.Island)
                    throw new InvalidOperationException("The verified island ID requires the Island lighting category.");
                if (!Finite(entry.CameraFarPlane) || (entry.CameraFarPlane != 0 && (entry.CameraFarPlane < 10 || entry.CameraFarPlane > 1000)))
                    throw new InvalidOperationException("Camera far must be zero or 10..1000 metres: " + entry.MapId);
                ResolveEntryLighting(entry, entry.Prefab.transform); // Validate paths and values without creating lights.
            }
        }

        public Entry Resolve(string mapId)
        {
            Validate();
            foreach (var entry in entries)
                if (string.Equals(entry.MapId, mapId, StringComparison.Ordinal)) return entry;
            throw new KeyNotFoundException("No verified catalog entry for map ID: " + mapId);
        }

        /// <summary>
        /// Fresh configuration/arrays; never assigns instance anchors back into the catalog asset.
        /// Skybox/VolumeProfile remain shared read-only assets. ContentHash matching is an identity
        /// check, not geometry, spawn, gameplay or artistic certification; Root owns those gates.
        /// Relative paths use exact ordinal child names, '/' separators and '.' for the map root.
        /// </summary>
        public HiggsfieldMapLighting.Configuration ResolveLighting(string mapId, EnvironmentMapDefinition instance)
        {
            var entry = Resolve(mapId);
            if (!instance || instance == entry.Prefab || !instance.gameObject.scene.IsValid() ||
                !string.Equals(instance.MapId, entry.MapId, StringComparison.Ordinal) ||
                !string.Equals(instance.ContentHash, entry.Prefab.ContentHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Lighting requires a map instance with the matching catalog identity.");
            return ResolveEntryLighting(entry, instance.transform);
        }

        private static HiggsfieldMapLighting.Configuration ResolveEntryLighting(Entry entry, Transform root)
        {
            var source = entry.Lighting;
            if (source == null || source.LocalLights == null || source.LocalLights.Length != 0 ||
                source.SuppressLights == null || source.SuppressLights.Length != 0 ||
                entry.LocalLights == null || entry.SuppressLightPaths == null)
                throw new InvalidOperationException("Use initialized relative bindings, not direct light/anchor references: " + entry.MapId);
            HiggsfieldMapLighting.PeriodFor(source.MapId);
            Nonnegative(source.SunUnityIntensity); Nonnegative(source.AmbientIntensity); Nonnegative(source.ReflectionIntensity);
            ColorValid(source.SunColor); ColorValid(source.AmbientSky); ColorValid(source.AmbientEquator); ColorValid(source.AmbientGround);
            ColorValid(source.FogColor); Nonnegative(source.FogDensity); Nonnegative(source.FogStart); Nonnegative(source.FogEnd);
            if (source.FogEnd <= source.FogStart || !Finite(source.VolumeWeight) || source.VolumeWeight < 0 || source.VolumeWeight > 1 ||
                !Finite(Quaternion.Dot(source.SunWorldRotation, source.SunWorldRotation)) ||
                Mathf.Abs(Quaternion.Dot(source.SunWorldRotation, source.SunWorldRotation) - 1) > .01f ||
                !Enum.IsDefined(typeof(FogMode), source.FogMode) || !Enum.IsDefined(typeof(LightShadows), source.SunShadows))
                throw new InvalidOperationException("Invalid lighting environment values: " + entry.MapId);
            if (entry.LocalLights.Length > 64) throw new InvalidOperationException("Maximum64 local lights per map.");
            var locals = new HiggsfieldMapLighting.LocalSource[entry.LocalLights.Length];
            var anchors = new HashSet<Transform>();
            for (int i = 0; i < locals.Length; i++)
            {
                var binding = entry.LocalLights[i];
                if (binding == null || binding.Settings == null || binding.Settings.Anchor)
                    throw new InvalidOperationException("Local light requires relative path and no stored Transform.");
                var light = binding.Settings;
                Transform anchor = ResolvePath(root, binding.AnchorPath);
                if (!anchors.Add(anchor) || (light.Type != LightType.Point && light.Type != LightType.Spot))
                    throw new InvalidOperationException("Duplicate local anchor or unsupported light type.");
                Nonnegative(light.UnityIntensity); ColorValid(light.Color);
                if (!Finite(light.Range) || light.Range <= 0 || !Finite(light.SpotAngle) || !Finite(light.InnerSpotAngle) ||
                    light.SpotAngle <= 0 || light.SpotAngle >= 180 || light.InnerSpotAngle < 0 || light.InnerSpotAngle > light.SpotAngle ||
                    !Enum.IsDefined(typeof(LightShadows), light.Shadows))
                    throw new InvalidOperationException("Invalid local light range/angles/shadows.");
                locals[i] = new HiggsfieldMapLighting.LocalSource { Anchor = anchor, Type = light.Type,
                    Color = light.Color, UnityIntensity = light.UnityIntensity, Range = light.Range,
                    SpotAngle = light.SpotAngle, InnerSpotAngle = light.InnerSpotAngle, Shadows = light.Shadows };
            }
            var suppressed = new Light[entry.SuppressLightPaths.Length];
            var lights = new HashSet<Light>();
            for (int i = 0; i < suppressed.Length; i++)
            {
                var found = ResolvePath(root, entry.SuppressLightPaths[i]).GetComponents<Light>();
                if (found.Length != 1 || !lights.Add(found[0])) throw new InvalidOperationException("Suppression path must resolve one distinct Light.");
                suppressed[i] = found[0];
            }
            // Shallow-copy serialized public values/assets without requiring Bootstrap to reference
            // the render-pipeline assembly that owns VolumeProfile. Replace all scene-reference arrays.
            var resolved = new HiggsfieldMapLighting.Configuration();
            foreach (var field in typeof(HiggsfieldMapLighting.Configuration).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                field.SetValue(resolved, field.GetValue(source));
            resolved.LocalLights = locals;
            resolved.SuppressLights = suppressed;
            return resolved;
        }
        private static Transform ResolvePath(Transform root, string path)
        {
            if (path == ".") return root; // Explicit root anchor, never an implicit empty path.
            if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.Contains("\\"))
                throw new InvalidOperationException("A canonical relative Transform path is required.");
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..") throw new InvalidOperationException("Invalid relative path: " + path);
                Transform next = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name != segment) continue;
                    if (next) throw new InvalidOperationException("Ambiguous sibling names in path: " + path);
                    next = child;
                }
                if (!next) throw new InvalidOperationException("Missing anchor path: " + path);
                current = next;
            }
            return current;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Nonnegative(float value) { if (!Finite(value) || value < 0) throw new InvalidOperationException("Explicit finite nonnegative lighting values are required."); }
        private static void ColorValid(Color value) { Nonnegative(value.r); Nonnegative(value.g); Nonnegative(value.b); Nonnegative(value.a); }
    }
}
