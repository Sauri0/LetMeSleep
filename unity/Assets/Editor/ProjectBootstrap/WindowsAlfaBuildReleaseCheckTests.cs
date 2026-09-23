using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// The release check of WindowsAlfaBuild.BuildV030 over player trees shaped like real ones: a non-Development
    /// Mono player still ships Unity.Pipeline.Attributes.dll (com.unity.pipeline keeps it unconstrained) and
    /// unreferenced assemblies such as Unity.Timeline.dll, and neither may block the release.
    /// </summary>
    public sealed class WindowsAlfaBuildReleaseCheckTests
    {
        // Managed names from the 0.2.0 Development player, minus what the define constraints drop in Release.
        private static readonly string[] ReleaseManaged =
        {
            "Assembly-CSharp.dll", "LetMeSleep.Bootstrap.dll", "LetMeSleep.Online.dll", "Newtonsoft.Json.dll",
            "Unity.InputSystem.dll", "Unity.Pipeline.Attributes.dll", "Unity.RenderPipeline.Universal.ShaderLibrary.dll",
            "Unity.RenderPipelines.Core.Runtime.dll", "Unity.RenderPipelines.Universal.Runtime.dll", "Unity.TextMeshPro.dll",
            "Unity.Timeline.dll", "UnityEngine.CoreModule.dll", "UnityEngine.UI.dll", "mscorlib.dll"
        };
        private static readonly string[] ReleaseBootConfig =
        {
            "gfx-enable-gfx-jobs=1", "gfx-threading-mode=6", "wait-for-native-debugger=0", "hdr-display-enabled=0", "build-guid=0123"
        };
        private string root;

        [SetUp] public void CreateRoot() => root = Path.Combine(Path.GetTempPath(), "lms-release-check-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void DeleteRoot() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        private string Player(IEnumerable<string> managed, IEnumerable<string> bootConfig)
        {
            string data = Path.Combine(root, "Let-me-sleep_Data");
            Directory.CreateDirectory(Path.Combine(data, "Managed"));
            foreach (string name in managed) File.WriteAllText(Path.Combine(data, "Managed", name), "x");
            File.WriteAllLines(Path.Combine(data, "boot.config"), bootConfig);
            return root;
        }

        [Test]
        public void ReleaseShapedPlayerWithPipelineAttributesPasses()
            => Assert.That(WindowsAlfaBuild.ReleaseProblems(Player(ReleaseManaged, ReleaseBootConfig)), Is.Empty);

        [TestCase("Unity.Pipeline.dll")]
        [TestCase("Unity.Pipeline.IlInterpreter.dll")]
        [TestCase("UnityPipeline.Microsoft.CodeAnalysis.dll")]
        [TestCase("UnityPipeline.Microsoft.CodeAnalysis.CSharp.dll")]
        [TestCase("UnityPipeline.System.Collections.Immutable.dll")]
        public void DevelopmentOnlyPipelineAssembliesAreRejected(string assembly)
            => Assert.That(WindowsAlfaBuild.ReleaseProblems(Player(ReleaseManaged.Append(assembly), ReleaseBootConfig)),
                Is.EqualTo(new[] { "development-only assembly " + assembly }));

        [Test]
        public void DevelopmentBootConfigIsRejected()
        {
            string[] problems = WindowsAlfaBuild.ReleaseProblems(Player(ReleaseManaged,
                ReleaseBootConfig.Concat(new[] { "player-connection-mode=Listen", "player-connection-ip=10.0.0.2" }).ToArray()));
            Assert.That(problems, Is.EqualTo(new[] { "boot.config: player-connection-mode", "boot.config: player-connection-ip" }));
            Assert.That(WindowsAlfaBuild.ReleaseProblems(Player(ReleaseManaged, new[] { "wait-for-native-debugger=1" })),
                Is.EqualTo(new[] { "boot.config: wait-for-native-debugger" }));
        }

        [Test]
        public void AssemblyRuleKeepsAttributesAndRenderPipelines()
        {
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("Unity.Pipeline.Attributes.dll"), Is.False);
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("unity.pipeline.attributes.dll"), Is.False);
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("Unity.RenderPipelines.Core.Runtime.dll"), Is.False);
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("Unity.Pipeline.pdb"), Is.False);
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("Unity.Pipeline.dll"), Is.True);
            Assert.That(WindowsAlfaBuild.IsDevelopmentOnlyAssembly("UnityPipeline.System.Reflection.Metadata.dll"), Is.True);
        }

        [Test]
        public void DefineConstraintsFollowThePlayerProfile()
        {
            string[] pipeline = { "UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_RUNTIME_PIPELINE" };
            var release = new HashSet<string> { "UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "ENABLE_MONO" };
            var development = new HashSet<string>(release) { "DEVELOPMENT_BUILD" };
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(pipeline, release), Is.False);
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(pipeline, development), Is.True);
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(new string[0], release), Is.True);
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(new[] { "!DEVELOPMENT_BUILD", "UNITY_STANDALONE" }, release), Is.True);
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(new[] { "!DEVELOPMENT_BUILD" }, development), Is.False);
            Assert.That(WindowsAlfaBuild.DefineConstraintsMet(new[] { "UNITY_STANDALONE && ENABLE_IL2CPP" }, release), Is.False);
        }
    }
}
