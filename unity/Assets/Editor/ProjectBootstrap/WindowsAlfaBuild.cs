using System;
using System.IO;
using System.Threading.Tasks;
using LetMeSleep.Online;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LetMeSleep.Editor
{
    public static class WindowsAlfaBuild
    {
        /// <summary>The production scene: the only entry of EditorBuildSettings since v0.2.0.</summary>
        public const string BuildScene = "Assets/Scenes/LetMeSleepHiggsfield.unity";
        public const string V030 = "0.3.0";
        private const string EosPrivateConfig = "N:/LetMeSleep/Private/eos.local.json";
        private const string ArtifactsRoot = "N:/LetMeSleep/Artifacts/";

        /// <summary>Release: what friends download. Development: internal smoke/diagnostics only, never published.</summary>
        private enum BuildProfile { Release, Development }

        public static string LastOutput { get; private set; }
        public static void Build()
            => BuildCandidate("0.9.4-alfa.3", AlfaBootstrapBuilder.ScenePath, BuildProfile.Development);

        public static void BuildV020()
            => BuildCandidate("0.2.0", BuildScene, BuildProfile.Development);

        public static void PrepareV020() => Prepare("0.2.0");

        /// <summary>
        /// Public v0.3.0 player: no Development Build, no profiler/PlayerConnection listener, no script debugging,
        /// no build-machine IPs in boot.config, no Development-only packages. The build probe
        /// (--lms-probe-output) and --lms-validation-data are compiled out of this profile.
        /// </summary>
        public static void BuildV030()
            => BuildCandidate(V030, BuildScene, BuildProfile.Release);

        /// <summary>
        /// Same commit as a Development player, only to run the smoke probe (--lms-probe-output) before
        /// publishing. Its receipt says profile=development and work/package-v030.ps1 refuses to package it.
        /// </summary>
        public static void BuildV030Diagnostics()
            => BuildCandidate(V030, BuildScene, BuildProfile.Development);

        /// <summary>Re-certifies facial contracts after a fresh import and writes version and build settings.</summary>
        public static void PrepareV030() => Prepare(V030);

        private static void Prepare(string version)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildScene) == null)
                throw new InvalidOperationException("Five-map scene is missing.");
            // A fresh import deliberately invalidates facial certificates. Re-run the
            // existing geometry checks before requiring a clean, reproducible candidate.
            var transient = new GameObject("Release facial preparation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var app = transient.AddComponent<LetMeSleep.Bootstrap.AlfaApplication>();
                app.hideFlags = HideFlags.HideAndDontSave;
                FacialContentBuilder.BuildAll(app);
            }
            finally { UnityEngine.Object.DestroyImmediate(transient); }
            PlayerSettings.productName = "Let me sleep";
            PlayerSettings.bundleVersion = version;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BuildScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LMS_PREPARED " + version + ": review and commit settings before building.");
        }

        private static string RepositoryRoot => Directory.GetParent(Application.dataPath).Parent.FullName;

        private static void BuildCandidate(string version, string scenePath, BuildProfile profile)
        {
            string sourceCommit = Git("rev-parse HEAD");
            if (SourceDirty())
                throw new InvalidOperationException("Commit all candidate inputs before building Windows.");
            bool numbered = version == "0.2.0" || version == V030;
            if (numbered && PlayerSettings.bundleVersion != version)
                throw new InvalidOperationException("Run Prepare for " + version + " and commit settings before building.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new InvalidOperationException("Candidate scene is missing: " + scenePath);
            string playerGuide = numbered ? Path.Combine(RepositoryRoot, "docs/player/PRUEBA-V" + version + ".md") : null;
            if (playerGuide != null && !File.Exists(playerGuide))
                throw new InvalidOperationException("The v" + version + " player testing guide is missing.");
            bool release = profile == BuildProfile.Release;

            var source = EosConfiguration.Load(EosPrivateConfig);
            string onlineConfig = Path.Combine(Application.streamingAssetsPath, "online.local.json");
            // The EOS client credentials exist in StreamingAssets only while this build runs (content-editor-build-11):
            // they travel inside the player, and no later ad-hoc build of the working copy picks up a stale copy.
            Directory.CreateDirectory(Application.streamingAssetsPath);
            BuildReport report;
            try
            {
                File.WriteAllText(onlineConfig, JsonUtility.ToJson(source));
                var config = PlayEveryWare.EpicOnlineServices.Config.Get<PlayEveryWare.EpicOnlineServices.WindowsConfig>();
                config.deployment = new PlayEveryWare.EpicOnlineServices.Deployment {
                    SandboxId = PlayEveryWare.EpicOnlineServices.SandboxId.FromString(source.sandboxId), DeploymentId = Guid.Parse(source.deploymentId) };
                config.platformOptionsFlags |= PlayEveryWare.EpicOnlineServices.WrappedPlatformFlags.DisableOverlay; config.Write();
                NativePluginPolicy.Apply();
                PlayerSettings.productName = "Let me sleep"; PlayerSettings.bundleVersion = version;
                LastOutput = ArtifactsRoot + version + (release ? "-" : "-diagnostics-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                Directory.CreateDirectory(LastOutput);
                // Release is BuildOptions.None on purpose: no Development, AllowDebugging, ConnectWithProfiler,
                // EnableDeepProfilingSupport or WaitForPlayerConnection, so no PlayerConnection listener either.
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath },
                    locationPathName = LastOutput + "/Let-me-sleep.exe", target = BuildTarget.StandaloneWindows64,
                    options = release ? BuildOptions.None : BuildOptions.Development });
            }
            finally
            {
                foreach (string file in new[] { onlineConfig, onlineConfig + ".meta" }) // both git-ignored
                    if (File.Exists(file)) File.Delete(file);
            }
            bool succeeded = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
            string[] releaseProblems = succeeded && release ? ReleaseProblems(LastOutput) : new string[0];
            if (succeeded && playerGuide != null)
                File.Copy(playerGuide, Path.Combine(LastOutput, "GUIA-DE-PRUEBA.md"));
            // Internal receipt: package-v030.ps1 checks it and leaves it out of the ZIP.
            File.WriteAllText(LastOutput + "/build-receipt.json", JsonUtility.ToJson(new Receipt { result = report.summary.result.ToString(),
                errors = report.summary.totalErrors, unity = Application.unityVersion, outputBytes = report.summary.totalSize,
                utc = DateTime.UtcNow.ToString("O"), sourceCommit = sourceCommit, sourceDirty = SourceDirty(), version = PlayerSettings.bundleVersion,
                profile = release ? "release" : "development", developmentBuild = !release, releaseProblems = releaseProblems }, true));
            // Same line format as earlier candidates (runbooks parse it); the profile goes on its own line.
            Debug.Log("LMS_BUILD_PROFILE " + (release ? "release" : "development"));
            Debug.Log("LMS_ALFA_BUILD " + report.summary.result + " " + LastOutput);
            if (!succeeded)
                throw new InvalidOperationException("Windows candidate build failed: " + report.summary.result);
            if (releaseProblems.Length != 0)
                throw new InvalidOperationException("The release player carries development content: " + string.Join("; ", releaseProblems));
        }

        /// <summary>What a public player must not contain (launcher-5, content-editor-build-1, architecture-1).</summary>
        private static string[] ReleaseProblems(string output)
        {
            var problems = new System.Collections.Generic.List<string>();
            string data = Path.Combine(output, "Let-me-sleep_Data");
            string bootConfig = Path.Combine(data, "boot.config");
            if (!File.Exists(bootConfig)) problems.Add("boot.config missing");
            else foreach (string line in File.ReadAllLines(bootConfig))
                // Development players listen for the Editor/Profiler and list the build machine's IPs here.
                if (line.StartsWith("player-connection", StringComparison.OrdinalIgnoreCase)
                    || (line.StartsWith("wait-for-native-debugger=", StringComparison.OrdinalIgnoreCase) && line.Trim() != "wait-for-native-debugger=0"))
                    problems.Add("boot.config: " + line.Split('=')[0]);
            string managed = Path.Combine(data, "Managed");
            if (Directory.Exists(managed))
                foreach (string file in Directory.GetFiles(managed, "*.dll"))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("Unity.Pipeline", StringComparison.OrdinalIgnoreCase) || name.StartsWith("UnityPipeline.", StringComparison.OrdinalIgnoreCase))
                        problems.Add("development-only assembly " + name);
                }
            return problems.ToArray();
        }

        // Compare normalized contents: Unity can rewrite line endings on dynamic font
        // assets during a build, leaving only a stale Git stat-cache modification.
        private static bool SourceDirty() => Git("diff --name-only").Length != 0
            || Git("diff --cached --name-only").Length != 0
            || Git("ls-files --others --exclude-standard").Length != 0;
        private static string Git(string arguments)
        {
            // stderr is drained concurrently: git can print one CRLF warning per file, and a full stderr pipe
            // while this thread waits on stdout would hang the build (content-editor-build-3).
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "-c core.safecrlf=false " + arguments) {
                WorkingDirectory = RepositoryRoot,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(120000)) { try { process.Kill(); } catch (InvalidOperationException) { } throw new TimeoutException("git " + arguments + " did not finish."); }
            if (process.ExitCode != 0) throw new InvalidOperationException("Cannot establish source provenance: " + error.Result);
            return output.Trim();
        }
        [Serializable] private sealed class Receipt
        {
            public string result, unity, utc, sourceCommit, version, profile;
            public bool sourceDirty, developmentBuild;
            public int errors;
            public ulong outputBytes;
            public string[] releaseProblems;
        }
    }
}
