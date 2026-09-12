using System.IO;
using UnityEditor;

namespace LetMeSleep.Editor
{
    [InitializeOnLoad]
    public static class NativePluginPolicy
    {
        static NativePluginPolicy() { EditorApplication.delayCall += Apply; }
        public static void Apply()
        {
            // Our EOS integration disables the overlay and owns no native rendering callbacks.
            // The bundled overlay helper crashes in UnityPluginLoad before game code runs.
            foreach (var importer in PluginImporter.GetAllImporters())
                if (Path.GetFileName(importer.assetPath).StartsWith("GfxPluginNativeRender"))
                    importer.SetIncludeInBuildDelegate(_ => false);
        }
    }
}
