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
        {
            string sourceCommit = Git("rev-parse HEAD");
            if (Git("status --porcelain").Length != 0)
                throw new InvalidOperationException("Commit all candidate inputs before building Windows.");
            var source = EosConfiguration.Load("N:/LetMeSleep/Private/eos.local.json");
            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"online.local.json"),JsonUtility.ToJson(source));
            var config = PlayEveryWare.EpicOnlineServices.Config.Get<PlayEveryWare.EpicOnlineServices.WindowsConfig>();
            config.deployment = new PlayEveryWare.EpicOnlineServices.Deployment {
                SandboxId=PlayEveryWare.EpicOnlineServices.SandboxId.FromString(source.sandboxId), DeploymentId=Guid.Parse(source.deploymentId) };
            config.platformOptionsFlags |= PlayEveryWare.EpicOnlineServices.WrappedPlatformFlags.DisableOverlay; config.Write();
            NativePluginPolicy.Apply();
            PlayerSettings.productName="Let me sleep"; PlayerSettings.bundleVersion="0.9.4-alfa";
            LastOutput="N:/LetMeSleep/Artifacts/alfa-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(LastOutput);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{AlfaBootstrapBuilder.ScenePath},
                locationPathName=LastOutput+"/Let-me-sleep.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            File.WriteAllText(LastOutput+"/build-receipt.json",JsonUtility.ToJson(new Receipt { result=report.summary.result.ToString(),errors=report.summary.totalErrors,
                unity=Application.unityVersion,outputBytes=report.summary.totalSize,utc=DateTime.UtcNow.ToString("O"),
                sourceCommit=sourceCommit,sourceDirty=Git("status --porcelain").Length!=0,version=PlayerSettings.bundleVersion },true));
            Debug.Log("LMS_ALFA_BUILD "+report.summary.result+" "+LastOutput);
        }
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

