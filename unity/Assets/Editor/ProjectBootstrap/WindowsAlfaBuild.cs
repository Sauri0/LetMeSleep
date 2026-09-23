using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LetMeSleep.Online;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
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
            // v0.3.0 smoke: the Direct3D12 runtime (D3D12Core.dll) crashed with 0xC0000005 while the player shut down in
            // 2 of 4 runs; Direct3D11 exited cleanly in every run with the same frames. Keep D3D12 only as a fallback.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
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
                utc = DateTime.UtcNow.ToString("O"), sourceCommit = sourceCommit,
                // BuildCandidate refuses a dirty tree before building, so the input is exactly sourceCommit.
                // Unity rewrites dynamic font atlases and URP prefilter flags while building; list them separately.
                sourceDirty = false, modifiedByBuild = Git("diff --name-only").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries),
                version = PlayerSettings.bundleVersion,
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
        internal static string[] ReleaseProblems(string output)
        {
            var problems = new List<string>();
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
                    if (IsDevelopmentOnlyAssembly(name)) problems.Add("development-only assembly " + name);
                }
            return problems.ToArray();
        }

        /// <summary>
        /// com.unity.pipeline (0.7.0-exp.1) runtime code: Unity.Pipeline.dll, Unity.Pipeline.IlInterpreter.dll and the
        /// UnityPipeline.* Roslyn plugins are constrained to UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_RUNTIME_PIPELINE.
        /// Unity.Pipeline.Attributes.dll is not: the package keeps its inert attributes in an unconstrained assembly so
        /// that release builds still compile, and a Mono player ships every compiled assembly whether referenced or not
        /// (the 0.2.0 player even carries Unity.Timeline.dll). work/package-v030.ps1 applies the same rule.
        /// </summary>
        internal static bool IsDevelopmentOnlyAssembly(string fileName)
        {
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "Unity.Pipeline.Attributes.dll", StringComparison.OrdinalIgnoreCase))
                return false;
            return fileName.StartsWith("Unity.Pipeline", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("UnityPipeline.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Release preflight without building a player: compiles the player scripts as Release and as Development into
        /// Temp/, predicts the managed plugins each profile copies (define constraints evaluated against the player
        /// defines) and runs the release assembly rule over the Release set. Throws when the release set would be
        /// rejected by BuildV030 or package-v030.ps1. Optional: --lms-release-assemblies-report &lt;file.json&gt;.
        /// </summary>
        public static void VerifyReleaseAssemblies()
        {
            string temp = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "LmsReleaseAssemblies");
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            var report = new ReleaseAssemblyReport { unity = Application.unityVersion, sourceCommit = Git("rev-parse HEAD"),
                sourceDirty = SourceDirty(), target = BuildTarget.StandaloneWindows64.ToString() };
            string[] playerDefines = PlayerDefines();
            report.playerDefines = playerDefines;
            report.releaseScripts = CompilePlayerScripts(Path.Combine(temp, "release"), ScriptCompilationOptions.None);
            report.developmentScripts = CompilePlayerScripts(Path.Combine(temp, "development"), ScriptCompilationOptions.DevelopmentBuild);
            report.releasePlugins = ManagedPlugins(new HashSet<string>(playerDefines, StringComparer.Ordinal));
            report.developmentPlugins = ManagedPlugins(new HashSet<string>(playerDefines.Append("DEVELOPMENT_BUILD"), StringComparer.Ordinal));
            report.releaseManaged = report.releaseScripts.Concat(report.releasePlugins).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            report.developmentManaged = report.developmentScripts.Concat(report.developmentPlugins).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            report.releaseProblems = report.releaseManaged.Where(IsDevelopmentOnlyAssembly).Select(name => "development-only assembly " + name).ToArray();
            // Sanity: the rule must still see something in a Development set, or the check proves nothing.
            report.developmentOnlyInDevelopment = report.developmentManaged.Where(IsDevelopmentOnlyAssembly).ToArray();
            string[] args = Environment.GetCommandLineArgs();
            int reportArg = Array.IndexOf(args, "--lms-release-assemblies-report");
            string reportPath = reportArg >= 0 && reportArg + 1 < args.Length ? Path.GetFullPath(args[reportArg + 1]) : Path.Combine(temp, "report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("LMS_RELEASE_ASSEMBLIES release=" + report.releaseManaged.Length + " problems=" + report.releaseProblems.Length
                + " attributes=" + report.releaseManaged.Contains("Unity.Pipeline.Attributes.dll", StringComparer.OrdinalIgnoreCase)
                + " developmentOnlyInDevelopment=" + string.Join(",", report.developmentOnlyInDevelopment) + " report=" + reportPath);
            if (report.releaseProblems.Length != 0)
                throw new InvalidOperationException("The release player would carry development content: " + string.Join("; ", report.releaseProblems));
        }

        private static string[] CompilePlayerScripts(string folder, ScriptCompilationOptions options)
        {
            Directory.CreateDirectory(folder);
            var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { group = BuildTargetGroup.Standalone,
                target = BuildTarget.StandaloneWindows64, options = options }, folder);
            if (result.assemblies == null || result.assemblies.Count == 0)
                throw new InvalidOperationException("Player script compilation failed (" + options + "): see the Editor log.");
            return result.assemblies.Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>Defines of a non-development Windows player compile (no UNITY_EDITOR*, no DEVELOPMENT_BUILD).</summary>
        private static string[] PlayerDefines()
        {
            bool development = EditorUserBuildSettings.development;
            try
            {
                EditorUserBuildSettings.development = false;
                // All game code lives in asmdefs (there is no Assembly-CSharp): keep the defines every player
                // assembly shares, which drops per-asmdef version defines.
                var player = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
                if (player.Length == 0) throw new InvalidOperationException("No player assemblies.");
                IEnumerable<string> shared = player[0].defines;
                foreach (var assembly in player.Skip(1)) shared = shared.Intersect(assembly.defines, StringComparer.Ordinal);
                return shared.Where(define => !define.StartsWith("UNITY_EDITOR", StringComparison.Ordinal) && define != "DEVELOPMENT_BUILD")
                    .OrderBy(define => define, StringComparer.Ordinal).ToArray();
            }
            finally { EditorUserBuildSettings.development = development; }
        }

        // The Test Framework strips com.unity.ext.nunit from players (no earlier player carries nunit.framework.dll).
        private static string[] ManagedPlugins(ISet<string> defines)
            => PluginImporter.GetImporters(BuildTarget.StandaloneWindows64)
                .Where(plugin => !plugin.isNativePlugin && plugin.assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !plugin.assetPath.StartsWith("Packages/com.unity.ext.nunit/", StringComparison.Ordinal)
                    && plugin.ShouldIncludeInBuild() && DefineConstraintsMet(plugin.DefineConstraints, defines))
                .Select(plugin => Path.GetFileName(plugin.assetPath)).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

        /// <summary>Each entry must hold; an entry is an OR of AND terms, each a define or !define.</summary>
        internal static bool DefineConstraintsMet(IEnumerable<string> constraints, ISet<string> defines)
        {
            foreach (string constraint in constraints ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(constraint)) continue;
                bool met = constraint.Split(new[] { "||" }, StringSplitOptions.None).Any(alternative =>
                    alternative.Split(new[] { "&&" }, StringSplitOptions.None).All(term =>
                    {
                        string define = term.Trim();
                        bool negated = define.StartsWith("!", StringComparison.Ordinal);
                        if (negated) define = define.Substring(1).Trim();
                        return defines.Contains(define) != negated;
                    }));
                if (!met) return false;
            }
            return true;
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
        [Serializable] private sealed class ReleaseAssemblyReport
        {
            public string unity, sourceCommit, target;
            public bool sourceDirty;
            public string[] playerDefines, releaseScripts, developmentScripts, releasePlugins, developmentPlugins, releaseManaged, developmentManaged,
                releaseProblems, developmentOnlyInDevelopment;
        }
        [Serializable] private sealed class Receipt
        {
            public string result, unity, utc, sourceCommit, version, profile;
            public bool sourceDirty, developmentBuild;
            public int errors;
            public ulong outputBytes;
            public string[] releaseProblems, modifiedByBuild;
        }
    }
}
