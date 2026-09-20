using System;
using System.IO;
using LetMeSleep.Online;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LetMeSleep.Editor
{
    public static class WindowsAlfaBuild
    {
        public static string LastOutput { get; private set; }
        public static void Build()
            => BuildCandidate("0.9.4-alfa.3", AlfaBootstrapBuilder.ScenePath);

        public static void BuildV020()
            => BuildCandidate("0.2.0", "Assets/Scenes/LetMeSleepHiggsfield.unity");

        public static void PrepareV020()
        {
            const string scene = "Assets/Scenes/LetMeSleepHiggsfield.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                throw new InvalidOperationException("Five-map scene is missing.");
            // A fresh import deliberately invalidates facial certificates. Re-run the
            // existing geometry checks before requiring a clean, reproducible candidate.
            var transient = new GameObject("V020 facial preparation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var app = transient.AddComponent<LetMeSleep.Bootstrap.AlfaApplication>();
                app.hideFlags = HideFlags.HideAndDontSave;
                FacialContentBuilder.BuildAll(app);
            }
            finally { UnityEngine.Object.DestroyImmediate(transient); }
            PlayerSettings.productName = "Let me sleep";
            PlayerSettings.bundleVersion = "0.2.0";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
            Debug.Log("LMS_V020_PREPARED: review and commit settings before BuildV020.");
        }

        private static void BuildCandidate(string version, string scenePath)
        {
            string sourceCommit = Git("rev-parse HEAD");
            if (SourceDirty())
                throw new InvalidOperationException("Commit all candidate inputs before building Windows.");
            if (version == "0.2.0" && PlayerSettings.bundleVersion != version)
                throw new InvalidOperationException("Run PrepareV020 and commit settings before building.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new InvalidOperationException("Candidate scene is missing: " + scenePath);
            var source = EosConfiguration.Load("N:/LetMeSleep/Private/eos.local.json");
            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"online.local.json"),JsonUtility.ToJson(source));
            var config = PlayEveryWare.EpicOnlineServices.Config.Get<PlayEveryWare.EpicOnlineServices.WindowsConfig>();
            config.deployment = new PlayEveryWare.EpicOnlineServices.Deployment {
                SandboxId=PlayEveryWare.EpicOnlineServices.SandboxId.FromString(source.sandboxId), DeploymentId=Guid.Parse(source.deploymentId) };
            config.platformOptionsFlags |= PlayEveryWare.EpicOnlineServices.WrappedPlatformFlags.DisableOverlay; config.Write();
            NativePluginPolicy.Apply();
            PlayerSettings.productName="Let me sleep"; PlayerSettings.bundleVersion=version;
            LastOutput="N:/LetMeSleep/Artifacts/"+version+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(LastOutput);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{scenePath},
                locationPathName=LastOutput+"/Let-me-sleep.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            File.WriteAllText(LastOutput+"/build-receipt.json",JsonUtility.ToJson(new Receipt { result=report.summary.result.ToString(),errors=report.summary.totalErrors,
                unity=Application.unityVersion,outputBytes=report.summary.totalSize,utc=DateTime.UtcNow.ToString("O"),
                sourceCommit=sourceCommit,sourceDirty=SourceDirty(),version=PlayerSettings.bundleVersion },true));
            Debug.Log("LMS_ALFA_BUILD "+report.summary.result+" "+LastOutput);
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new InvalidOperationException("Windows candidate build failed: " + report.summary.result);
        }
        // Compare normalized contents: Unity can rewrite line endings on dynamic font
        // assets during a build, leaving only a stale Git stat-cache modification.
        private static bool SourceDirty() => Git("diff --name-only").Length != 0
            || Git("diff --cached --name-only").Length != 0
            || Git("ls-files --others --exclude-standard").Length != 0;
        private static string Git(string arguments)
        {
            using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git",arguments) {
                WorkingDirectory=Directory.GetParent(Application.dataPath).Parent.FullName,
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true });
            string output=process.StandardOutput.ReadToEnd(); string error=process.StandardError.ReadToEnd(); process.WaitForExit();
            if(process.ExitCode!=0) throw new InvalidOperationException("Cannot establish source provenance: "+error);
            return output.Trim();
        }
        [Serializable] private sealed class Receipt { public string result,unity,utc,sourceCommit,version; public bool sourceDirty; public int errors; public ulong outputBytes; }
    }
}

