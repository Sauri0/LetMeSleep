using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LetMeSleep.Editor
{
    public static class WindowsProbeBuild
    {
        public static string LastResult { get; private set; } = "NotStarted";
        public static void Build()
        {
            var source = LetMeSleep.Online.EosConfiguration.Load("N:/LetMeSleep/Private/eos.local.json");
            var config = PlayEveryWare.EpicOnlineServices.Config.Get<PlayEveryWare.EpicOnlineServices.WindowsConfig>();
            config.deployment = new PlayEveryWare.EpicOnlineServices.Deployment {
                SandboxId = PlayEveryWare.EpicOnlineServices.SandboxId.FromString(source.sandboxId),
                DeploymentId = System.Guid.Parse(source.deploymentId)
            };
            config.platformOptionsFlags |= PlayEveryWare.EpicOnlineServices.WrappedPlatformFlags.DisableOverlay;
            config.Write();
            NativePluginPolicy.Apply();
            // A new directory keeps this verification independent of the manually repaired probe.
            Directory.CreateDirectory("N:/LetMeSleep/Artifacts/eos-probe-clean");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = "N:/LetMeSleep/Artifacts/eos-probe-clean/LetMeSleepProbe.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            LastResult = report.summary.result + ":" + report.summary.totalErrors;
            File.WriteAllText("N:/LetMeSleep/Artifacts/eos-probe-clean/build-result.txt", LastResult);
        }
    }
}
