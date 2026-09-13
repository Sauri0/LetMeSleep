using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    /// <summary>Explicit NEW material copy; never edits the builtin or changes RenderSettings.</summary>
    public static class HiggsfieldSkyboxMaterialBuilder
    {
#pragma warning disable CS0649
        [Serializable] public sealed class Request
        {
            public int schema_version;
            public string outputAssetPath, receiptPath;
            public string builtinResourcePath, expectedBuiltinGuid;
            public long expectedBuiltinFileId;
            public Color skyTint, groundColor;
        }
        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string unityVersion, configSha256, outputAssetPath, assetGuid, assetSha256;
            public string sourceGuid, shader;
            public long sourceFileId;
            public Color skyTint, groundColor;
            public string scope = "Provisional procedural sky from approved ambient colors. Review camera used SolidColor; visible sky has not been visually validated.";
        }
#pragma warning restore CS0649
        public static void BuildFromCommandLine()
        {
            string path = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-higgsfieldSkyboxConfig")
                {
                    Require(path == null && i + 1 < args.Length, "Supply one skybox config.");
                    path = args[++i];
                }
            Build(path);
        }
        public static void Build(string configPath)
        {
            Require(!string.IsNullOrWhiteSpace(configPath) && Path.IsPathRooted(configPath) && File.Exists(configPath) &&
                new FileInfo(configPath).Length <= 1024 * 1024, "Absolute config up to 1 MiB required.");
            byte[] config = File.ReadAllBytes(configPath);
            var request = JsonUtility.FromJson<Request>(Encoding.UTF8.GetString(config));
            Require(request != null && request.schema_version == 1, "Schema 1 required.");
            string output = request.outputAssetPath;
            Require(output != null && output.StartsWith("Assets/", StringComparison.Ordinal) &&
                output.EndsWith(".mat", StringComparison.Ordinal) && !output.Contains("\\"), "Canonical Assets material path required.");
            foreach (var segment in output.Split('/'))
                Require(segment.Length > 0 && segment != "." && segment != ".." && segment == segment.Trim() &&
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, "Invalid asset path.");
            Require(AssetDatabase.IsValidFolder(Path.GetDirectoryName(output).Replace('\\', '/')), "Output folder must exist.");
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
            RequireNew(file); RequireNew(file + ".meta");
            Require(!string.IsNullOrWhiteSpace(request.receiptPath) && Path.IsPathRooted(request.receiptPath), "Absolute receipt required.");
            string receiptPath = Path.GetFullPath(request.receiptPath);
            Require(Directory.Exists(Path.GetDirectoryName(receiptPath)) &&
                !receiptPath.StartsWith(Path.GetFullPath(Application.dataPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase), "Receipt requires an existing external folder.");
            RequireNew(receiptPath);
            Require(request.builtinResourcePath == "Default-Skybox.mat" &&
                request.expectedBuiltinGuid == "0000000000000000f000000000000000" && request.expectedBuiltinFileId == 10304,
                "Explicit verified DefaultSkybox identity required.");
            var builtin = AssetDatabase.GetBuiltinExtraResource<Material>(request.builtinResourcePath);
            Require(builtin && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(builtin, out string guid, out long fileId) &&
                guid == request.expectedBuiltinGuid && fileId == request.expectedBuiltinFileId,
                "Builtin identity differs; do not fall back to another material.");
            Require(builtin.shader && builtin.shader.name == "Skybox/Procedural" && builtin.HasProperty("_SkyTint") &&
                builtin.HasProperty("_GroundColor"), "Expected procedural shader properties missing.");
            ValidateColor(request.skyTint); ValidateColor(request.groundColor);
            var copy = new Material(builtin) { name = Path.GetFileNameWithoutExtension(output) };
            bool created = false;
            try
            {
                copy.SetColor("_SkyTint", request.skyTint); copy.SetColor("_GroundColor", request.groundColor);
                var receipt = new Receipt { unityVersion = Application.unityVersion, configSha256 = Hash(config),
                    outputAssetPath = output, sourceGuid = request.expectedBuiltinGuid, sourceFileId = request.expectedBuiltinFileId,
                    shader = copy.shader.name, skyTint = request.skyTint, groundColor = request.groundColor };
                using (var stream = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Write(stream, receipt);
                    RequireNew(file); RequireNew(file + ".meta");
                    AssetDatabase.CreateAsset(copy, output); created = AssetDatabase.Contains(copy);
                    Require(created, "Material creation failed.");
                    AssetDatabase.SaveAssetIfDirty(copy);
                    receipt.assetGuid = AssetDatabase.AssetPathToGUID(output);
                    Require(!string.IsNullOrEmpty(receipt.assetGuid) && receipt.assetGuid != request.expectedBuiltinGuid,
                        "New material GUID missing.");
                    receipt.assetSha256 = Hash(File.ReadAllBytes(file)); receipt.success = true; Write(stream, receipt);
                }
            }
            finally { if (!created) UnityEngine.Object.DestroyImmediate(copy); }
        }
        private static void ValidateColor(Color c)
        {
            foreach (float value in new[] { c.r, c.g, c.b, c.a })
                Require(!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1, "Explicit color in [0,1] required.");
        }
        private static void RequireNew(string path) => Require(!File.Exists(path) && !Directory.Exists(path), "Refusing existing output: " + path);
        private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void Write(FileStream stream, Receipt receipt)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt, true));
            stream.Position = 0; stream.SetLength(0); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
    }
}
