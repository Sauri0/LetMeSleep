using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LetMeSleep.Content.Editor.Higgsfield
{
    // Kept free of Unity APIs so contract failures can be tested without an Editor.
    [Serializable] public sealed class HiggsfieldImportContract
    {
        public string mapId, sourceFbx, sourceSha256;
        public bool sourceFinal;
        public float importScale = 1;
        public string collisionLayer = "WorldStatic";
        public string[] humanSpawns, mosquitoSpawns, lobbySpawns, toolPickups;
        public string presentationRoot;
        public float[] playBoundsMin, playBoundsMax; // Unity world XYZ metres, not Blender XYZ.
        public string boundsMinEmpty, boundsMaxEmpty; // Optional pair, overrides numeric bounds.
        public int requiredHumanSpawns = 16, requiredMosquitoSpawns = 16;
        public uint firstSurfaceId = 1000000;
        public HiggsfieldNodeRule[] nodes;
        public HiggsfieldSwatch[] materials;

        public void Validate()
        {
            Need(sourceFinal, "Source is not declared final. Never import a Scene Builder checkpoint.");
            Need(Regex.IsMatch(mapId ?? "", @"^hf-[a-z0-9]+(?:-[a-z0-9]+)*$"), "Unsafe/invalid mapId.");
            Need(Regex.IsMatch(sourceSha256 ?? "", "^[a-fA-F0-9]{64}$"), "Final FBX SHA256 required.");
            Need(!string.IsNullOrWhiteSpace(sourceFbx), "sourceFbx required.");
            Need(Finite(importScale) && importScale > 0 && importScale <= 100, "Invalid import scale.");
            Need(!string.IsNullOrWhiteSpace(collisionLayer), "Collision layer required.");
            Need(firstSurfaceId > 0, "Surface IDs must be nonzero.");
            Need(requiredHumanSpawns >= 1 && requiredMosquitoSpawns >= 1, "Explicit spawn requirements required.");
            CheckNames(humanSpawns, requiredHumanSpawns, "human spawns");
            CheckNames(mosquitoSpawns, requiredMosquitoSpawns, "mosquito spawns");
            var allSpawns = (humanSpawns ?? Array.Empty<string>()).Concat(mosquitoSpawns ?? Array.Empty<string>())
                .Concat(lobbySpawns ?? Array.Empty<string>()).Concat(toolPickups ?? Array.Empty<string>()).ToArray();
            CheckNames(allSpawns, 1, "spawn/pickup markers");
            if (!string.IsNullOrEmpty(boundsMinEmpty) || !string.IsNullOrEmpty(boundsMaxEmpty))
                Need(!string.IsNullOrEmpty(boundsMinEmpty) && !string.IsNullOrEmpty(boundsMaxEmpty) && boundsMinEmpty != boundsMaxEmpty, "Bounds EMPTYs require a distinct pair.");
            else
            {
                Need(playBoundsMin?.Length == 3 && playBoundsMax?.Length == 3, "Playable bounds required; do not derive from ocean renderer.");
                for (int i = 0; i < 3; i++) Need(Finite(playBoundsMin[i]) && Finite(playBoundsMax[i]) && playBoundsMax[i] > playBoundsMin[i], "Invalid playable bounds.");
            }
            Need(nodes != null && nodes.Length > 0 && nodes.All(n => n != null), "Node rules required.");
            CheckNames(nodes.Select(n => n.path).ToArray(), 1, "node paths");
            foreach (var n in nodes)
            {
                Need(new[] { "solid", "water", "foam", "foliage", "decoration" }.Contains(n.kind), "Unknown node kind: " + n.kind);
                Need(n.path.Split('/').All(p => p.Length > 0 && p != "." && p != ".."), "Invalid relative node path.");
                Need(Finite(n.waveAmplitude) && n.waveAmplitude >= 0 && n.waveAmplitude <= .15f, "Wave amplitude must be 0..0.15 m.");
                Need(Finite(n.waveLength) && n.waveLength > 0 && Finite(n.waveSpeed), "Invalid wave settings.");
                Need(n.waterAnimation == "cpu" || (n.waterAnimation == "gpu" && (n.kind == "water" || n.kind == "foam")), "Invalid explicit water animation mode.");
            }
            Need(materials != null && materials.Length > 0 && materials.All(m => m != null), "Explicit flat-colour swatches required.");
            CheckNames(materials.Select(m => m.sourceName).ToArray(), 1, "material names");
            foreach (var m in materials)
            {
                Need(m.rgb?.Length == 3 && m.rgb.All(v => Finite(v) && v >= 0 && v <= 1), "Material RGB must be three sRGB values in 0..1.");
                Need(m.colorSpace == "linear" || m.colorSpace == "srgb", "Explicit material colour space must be linear or srgb.");
                m.ValidateEmission();
                m.ValidateSurface();
            }
        }

        public HiggsfieldNodeRule Resolve(string relativePath)
        {
            var matches = nodes.Where(n => relativePath == n.path || (n.descendants && relativePath.StartsWith(n.path + "/", StringComparison.Ordinal)))
                .OrderByDescending(n => n.path.Length).ToArray();
            Need(matches.Length > 0, "Unclassified mesh: " + relativePath);
            return matches[0]; // Most specific path overrides a parent rule; exact duplicate rules are rejected.
        }
        public static void ValidateEmissionOnlyChange(HiggsfieldImportContract before, HiggsfieldImportContract after)
        {
            Need(before != null && after != null, "Both recipes required.");
            before.Validate(); after.Validate();
            Need(EqualExceptEmission(before, after), "Emission repair cannot change map, FBX, paths, palette/base RGB, spawns or other recipe fields.");
        }
        static bool EqualExceptEmission(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.GetType() != b.GetType()) return false;
            var type = a.GetType();
            if (type.IsValueType || a is string) return a.Equals(b);
            if (a is Array aa && b is Array bb)
                return aa.Length == bb.Length && Enumerable.Range(0, aa.Length).All(i => EqualExceptEmission(aa.GetValue(i), bb.GetValue(i)));
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => type != typeof(HiggsfieldSwatch) || (f.Name != "emissionRgb" && f.Name != "emissionStrength"))
                .All(f => EqualExceptEmission(f.GetValue(a), f.GetValue(b)));
        }
        static void CheckNames(string[] names, int count, string label)
        {
            Need(names != null && names.Length >= count && names.All(n => !string.IsNullOrWhiteSpace(n)), "Insufficient/empty " + label);
            Need(names.Distinct(StringComparer.Ordinal).Count() == names.Length, "Duplicate " + label);
        }
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
    [Serializable] public sealed class HiggsfieldNodeRule
    {
        public string path, kind;
        public string waterAnimation = "cpu";
        public bool descendants, canPerch = true;
        public float waveAmplitude = .025f, waveLength = 4f, waveSpeed = .65f;
    }
    [Serializable] public sealed class HiggsfieldSwatch
    {
        public string sourceName;
        public float[] rgb;
        public string colorSpace = "srgb";
        public string alphaMode = "OPAQUE";
        public float opacity = 1;
        public bool doubleSided;
        public void ValidateSurface()
        {
            HiggsfieldImportContract.Need(alphaMode == "OPAQUE" || alphaMode == "BLEND", "Unsupported material alpha mode: " + sourceName);
            HiggsfieldImportContract.Need(HiggsfieldImportContract.Finite(opacity) && opacity > 0 && opacity <= 1, "Invalid material opacity: " + sourceName);
            HiggsfieldImportContract.Need(alphaMode != "OPAQUE" || (opacity == 1 && !doubleSided), "Opaque geometry requires full opacity and outward faces: " + sourceName);
        }
        // Always linear. Missing fields in old recipes mean black, independently of base colour.
        public float[] emissionRgb;
        public float emissionStrength;
        public void ValidateEmission()
        {
            HiggsfieldImportContract.Need(HiggsfieldImportContract.Finite(emissionStrength) && emissionStrength >= 0, "Invalid emission strength: " + sourceName);
            HiggsfieldImportContract.Need(emissionRgb == null || (emissionRgb.Length == 3 && emissionRgb.All(v => HiggsfieldImportContract.Finite(v) && v >= 0)), "Invalid linear emission RGB: " + sourceName);
            HiggsfieldImportContract.Need(emissionRgb != null || emissionStrength == 0, "Positive strength requires emission RGB: " + sourceName);
            if (emissionRgb != null)
                HiggsfieldImportContract.Need(emissionRgb.All(v => HiggsfieldImportContract.Finite(v * emissionStrength)), "Emission product overflows: " + sourceName);
        }
        public float[] EmissionLinear()
        {
            ValidateEmission();
            return emissionRgb == null ? new float[3] : emissionRgb.Select(v => v * emissionStrength).ToArray();
        }
    }
}
