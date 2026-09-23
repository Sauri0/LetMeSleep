using System.IO;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters.Editor;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    /// <summary>
    /// Character re-import in dependency order, for batch mode (-executeMethod) and the editor menu:
    /// gameplay characters and their idempotence check, the living-menu actor derived from LMS_Human,
    /// the facial certificates of the four prefabs (a fresh FBX import invalidates them), then the
    /// modular customization (parts, catalog, hosts and the build scene's provider).
    /// Unlike AlfaBootstrapBuilder.Build it does not rebuild the boot scene, and unlike
    /// WindowsAlfaBuild.PrepareV020 it does not touch player or build settings.
    /// </summary>
    public static class CharacterContentPipeline
    {
        private const string Prefabs = "Assets/LetMeSleep/Content/Characters/Prefabs/";

        [MenuItem("Let Me Sleep/Content/Rebuild Characters, Living Menu and Facial Certificates")]
        public static void RebuildAll()
        {
            CharacterContentBuilder.BuildAndVerifyIdempotence();
            var transient = new GameObject("Character content pipeline") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var app = transient.AddComponent<AlfaApplication>();
                app.hideFlags = HideFlags.HideAndDontSave;
                app.HumanPrefab = Required(Prefabs + "LMS_Human.prefab");
                app.MosquitoPrefab = Required(Prefabs + "LMS_Mosquito.prefab");
                LivingMenuContentBuilder.BuildAndBind(app);
                FacialContentBuilder.BuildAll(app);
            }
            finally { Object.DestroyImmediate(transient); }
            AssetDatabase.SaveAssets();
            // v0.3.0 modular customization: part prefabs, catalog, hosts on the four rebuilt prefabs and the scene
            // provider (existing thumbnails are reassigned; bake new ones with InstallAllWithThumbnails).
            CharacterCustomizationContentBuilder.InstallAll();
            Debug.Log("LMS_CHARACTER_PIPELINE_PASSED characters, living menu, facial certificates and modular customization rebuilt");
        }

        private static GameObject Required(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
    }
}
