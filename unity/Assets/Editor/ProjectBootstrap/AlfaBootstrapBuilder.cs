using System;
using System.IO;
using LetMeSleep.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace LetMeSleep.Editor
{
    public static class AlfaBootstrapBuilder
    {
        public const string ScenePath = "Assets/Scenes/LetMeSleepBoot.unity";
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("Let me sleep").AddComponent<AlfaApplication>();
            app.HousePrefab = Required<GameObject>("Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/HousePatio.prefab");
            app.LobbyPrefab = Required<GameObject>("Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab");
            app.HumanPrefab = Required<GameObject>("Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Human.prefab");
            app.MosquitoPrefab = Required<GameObject>("Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Mosquito.prefab");
            LivingMenuContentBuilder.BuildAndBind(app);
            app.GameplayPresentationPrefab = Required<GameObject>("Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_GameplayPresentation.prefab");
            app.MenuAudioPrefab = Required<GameObject>("Assets/LetMeSleep/Audio/Generated/Prefabs/LMS_AlfaAudioRoot.prefab");
            app.Mixer = Required<AudioMixer>("Assets/LetMeSleep/Audio/Generated/LMS_AlfaMixer.mixer");
            app.HeadingFont = Font("Bangers-Regular"); app.BodyFont = Font("AtkinsonHyperlegible-Regular");
            app.MenuCamera = new GameObject("MenuCamera", typeof(AudioListener)).AddComponent<Camera>();
            app.MenuCamera.tag = "MainCamera"; app.MenuCamera.nearClipPlane = .025f;
            app.MenuCamera.farClipPlane = 160; app.MenuCamera.cullingMask = ~(1 << 30);
            app.MenuCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            app.PreviewStage = new GameObject("CustomizationStage").transform; app.PreviewStage.position = new Vector3(500, 0, 0);
            app.PreviewCamera = new GameObject("CustomizationCamera").AddComponent<Camera>();
            app.PreviewCamera.cullingMask = 1 << 30; app.PreviewCamera.nearClipPlane = .005f; app.PreviewCamera.farClipPlane = 15;
            app.PreviewCamera.clearFlags = CameraClearFlags.SolidColor; app.PreviewCamera.backgroundColor = new Color(.045f,.08f,.13f);
            app.PreviewCamera.fieldOfView = 35; app.PreviewCamera.enabled = false;
            const string texturePath = "Assets/LetMeSleep/Bootstrap/Customization.renderTexture";
            app.PreviewTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (!app.PreviewTexture) { app.PreviewTexture = new RenderTexture(1024,1024,24) { name = "Customization" }; AssetDatabase.CreateAsset(app.PreviewTexture,texturePath); }
            app.PreviewCamera.targetTexture = app.PreviewTexture;
            var lighting = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>("Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_AlfaLightingRoot.prefab"),scene);
            app.LightingRig = lighting.GetComponent<LetMeSleep.Presentation.AlfaLightingRig>();
            var previewLight = new GameObject("CustomizationKey").AddComponent<Light>(); previewLight.type = LightType.Directional;
            previewLight.intensity = 1.5f; previewLight.cullingMask = 1 << 30; previewLight.transform.rotation = Quaternion.Euler(30,150,0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) };
            AssetDatabase.SaveAssets(); Debug.Log("LMS_ALFA_BOOTSTRAP_BUILT");
        }
        private static T Required<T>(string path) where T: UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : throw new FileNotFoundException(path);
        private static TMP_FontAsset Font(string name)
        {
            string path = "Assets/LetMeSleep/UI/Fonts/" + name;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path + " SDF.asset");
            if (font) return font;
            font = TMP_FontAsset.CreateFontAsset(Required<Font>(path + ".ttf"),64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name = name + " SDF"; AssetDatabase.CreateAsset(font,path + " SDF.asset");
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture,font);
            AssetDatabase.AddObjectToAsset(font.material,font);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 áéíóúüñÁÉÍÓÚÜÑ¿¡·…:/.-_()+%",out string missing);
            EditorUtility.SetDirty(font); return font;
        }
    }
}

