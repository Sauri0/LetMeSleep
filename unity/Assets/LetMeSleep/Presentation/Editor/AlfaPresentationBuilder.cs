using System;
using System.Collections.Generic;
using System.IO;
using LetMeSleep.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LetMeSleep.Presentation.Editor
{
    public static class AlfaPresentationBuilder
    {
        private const string PresentationRoot = "Assets/LetMeSleep/Presentation/Generated";
        private const string AudioRoot = "Assets/LetMeSleep/Audio/Generated";
        private const string ClipRoot = "Assets/LetMeSleep/Audio/Clips";
        private const string MixerPath = AudioRoot + "/LMS_AlfaMixer.mixer";

        [MenuItem("Tools/Let me sleep/Build Alfa Presentation Library")]
        public static void Build()
        {
            EnsureFolder(PresentationRoot);
            EnsureFolder(PresentationRoot + "/Materials");
            EnsureFolder(PresentationRoot + "/Profiles");
            EnsureFolder(PresentationRoot + "/Prefabs");
            EnsureFolder(AudioRoot);
            EnsureFolder(AudioRoot + "/Cues");
            EnsureFolder(AudioRoot + "/Prefabs");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAudioImporters();

            AlfaPresentationPreset preset = CreateOrLoad<AlfaPresentationPreset>(
                PresentationRoot + "/AlfaPresentationPreset.asset");
            EditorUtility.SetDirty(preset);

            VolumeProfile volume = BuildVolumeProfile();
            BuildMaterials();
            BuildLightingPrefab(preset, volume);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                Debug.LogWarning($"LMS_AUDIO_MIXER_REQUIRED path={MixerPath}; clips remain audible through Master until the mixer is created and the builder is rerun.");
            Dictionary<string, AudioCue> cues = BuildAudioCues(mixer);
            BuildAudioRoot(cues, mixer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LMS_ALFA_PRESENTATION_LIBRARY_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            const string path = PresentationRoot + "/Profiles/AlfaGlobalVolume.asset";
            VolumeProfile profile = CreateOrLoad<VolumeProfile>(path);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(4f);
            color.saturation.Override(-3f);

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(-5f);
            whiteBalance.tint.Override(0f);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(0.08f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.55f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.30f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void BuildMaterials()
        {
            CreateMaterial("PaintedWall", "Universal Render Pipeline/Simple Lit",
                new Color(0.56f, 0.61f, 0.68f), 0f, 0.08f, false);
            CreateMaterial("VarnishedWood", "Universal Render Pipeline/Lit",
                new Color(0.28f, 0.14f, 0.075f), 0f, 0.30f, false);
            CreateMaterial("PajamaFabric", "Universal Render Pipeline/Simple Lit",
                new Color(0.23f, 0.42f, 0.56f), 0f, 0.10f, false);
            CreateMaterial("HardwareMetal", "Universal Render Pipeline/Lit",
                new Color(0.34f, 0.37f, 0.40f), 0.75f, 0.52f, false);
            CreateMaterial("WindowGlass", "Universal Render Pipeline/Lit",
                new Color(0.52f, 0.66f, 0.74f, 0.22f), 0f, 0.72f, true);
            Material wing = CreateMaterial("MosquitoWing", "Universal Render Pipeline/Lit",
                new Color(0.71f, 0.82f, 0.83f, 0.42f), 0f, 0.28f, true);
            // M1 exports physical front/back membrane faces. Back-face culling avoids
            // rendering both sides of both surfaces and doubling wing overdraw.
            SetFloat(wing, "_Cull", 2f);
            SetFloat(wing, "_ReceiveShadows", 0f);
            EditorUtility.SetDirty(wing);
        }

        private static Material CreateMaterial(
            string name, string shaderName, Color color, float metallic,
            float smoothness, bool transparent)
        {
            string path = $"{PresentationRoot}/Materials/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Required shader unavailable: {shaderName}");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetColor(material, "_BaseColor", color);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", smoothness);
            SetFloat(material, "_Surface", transparent ? 1f : 0f);
            SetFloat(material, "_Blend", transparent ? 1f : 0f);
            SetFloat(material, "_ZWrite", transparent ? 0f : 1f);
            CoreUtils.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            CoreUtils.SetKeyword(material, "_ALPHAPREMULTIPLY_ON", transparent);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildLightingPrefab(AlfaPresentationPreset preset, VolumeProfile profile)
        {
            var root = new GameObject("LMS_AlfaLightingRoot");
            try
            {
                var moonObject = new GameObject("Moon_MainDirectional");
                moonObject.transform.SetParent(root.transform, false);
                moonObject.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
                Light moon = moonObject.AddComponent<Light>();

                var volumeObject = new GameObject("Volume_Global");
                volumeObject.transform.SetParent(root.transform, false);
                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0f;
                volume.sharedProfile = profile;

                AlfaFramePolicy framePolicy = root.AddComponent<AlfaFramePolicy>();
                AlfaLightingRig rig = root.AddComponent<AlfaLightingRig>();
                Assign(framePolicy, "preset", preset);
                Assign(rig, "preset", preset);
                Assign(rig, "moon", moon);
                Assign(rig, "globalVolume", volume);
                rig.ApplyPreset();
                moon.lightmapBakeType = LightmapBakeType.Mixed;

                PrefabUtility.SaveAsPrefabAsset(
                    root, PresentationRoot + "/Prefabs/LMS_AlfaLightingRoot.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, AudioCue> BuildAudioCues(AudioMixer mixer)
        {
            var cues = new Dictionary<string, AudioCue>();
            cues["StrikeSwing"] = CreateCue("StrikeSwing", "SFX_StrikeSwing.wav", 40, 4, 0.98f, 1.03f, 1f, 0.8f, 18f, FindGroup(mixer, "Character"));
            cues["StrikeImpact"] = CreateCue("StrikeImpact", "SFX_StrikeImpact.wav", 24, 4, 0.97f, 1.03f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["BiteStarted"] = CreateCue("BiteStarted", "SFX_BiteStart.wav", 24, 8, 0.98f, 1.02f, 1f, 0.35f, 12f, FindGroup(mixer, "Critical"));
            cues["MosquitoWingLoop"] = CreateCue("MosquitoWingLoop", "SFX_MosquitoWingLoop.wav", 56, 12, 0.92f, 1.12f, 1f, 0.35f, 12f, FindGroup(mixer, "Mosquito"));
            cues["DoorOpen"] = CreateCue("DoorOpen", "SFX_DoorOpen.wav", 96, 10, 0.98f, 1.02f, 1f, 0.8f, 20f, FindGroup(mixer, "World"));
            cues["DoorClose"] = CreateCue("DoorClose", "SFX_DoorClose.wav", 96, 10, 0.98f, 1.02f, 1f, 0.8f, 20f, FindGroup(mixer, "World"));
            cues["HumanFainted"] = CreateCue("HumanFainted", "SFX_HumanFainted.wav", 24, 4, 0.98f, 1.02f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["Recovered"] = CreateCue("Recovered", "SFX_Recovered.wav", 24, 4, 0.98f, 1.02f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["RoundStart"] = CreateCue("RoundStart", "STG_RoundStart.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["HumansWin"] = CreateCue("HumansWin", "STG_HumansWin.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["MosquitoesWin"] = CreateCue("MosquitoesWin", "STG_MosquitoesWin.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["UiReady"] = CreateCue("UiReady", "UI_Ready.wav", 48, 4, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "UI"));
            return cues;
        }

        private static AudioCue CreateCue(
            string id, string clipName, int priority, int simultaneous,
            float minimumPitch, float maximumPitch, float spatialBlend,
            float minimumDistance, float maximumDistance, AudioMixerGroup output)
        {
            string path = $"{AudioRoot}/Cues/{id}.asset";
            AudioCue cue = CreateOrLoad<AudioCue>(path);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipRoot}/{clipName}");
            if (clip == null)
                throw new FileNotFoundException("Audio clip failed to import", clipName);

            var serialized = new SerializedObject(cue);
            serialized.FindProperty("cueId").stringValue = id;
            SerializedProperty clips = serialized.FindProperty("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            serialized.FindProperty("output").objectReferenceValue = output;
            serialized.FindProperty("priority").intValue = priority;
            serialized.FindProperty("maximumSimultaneous").intValue = simultaneous;
            serialized.FindProperty("minimumPitch").floatValue = minimumPitch;
            serialized.FindProperty("maximumPitch").floatValue = maximumPitch;
            serialized.FindProperty("spatialBlend").floatValue = spatialBlend;
            serialized.FindProperty("minimumDistance").floatValue = minimumDistance;
            serialized.FindProperty("maximumDistance").floatValue = maximumDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cue);
            return cue;
        }

        private static void BuildAudioRoot(Dictionary<string, AudioCue> cues, AudioMixer mixer)
        {
            var root = new GameObject("LMS_AlfaAudioRoot");
            try
            {
                root.AddComponent<AudioEmitterPool>();
                CreateBed(root.transform, "Music_Menu", "MUS_NightMischief_Menu.wav", 0.45f, true, false, FindGroup(mixer, "Music"));
                CreateBed(root.transform, "Music_Round", "MUS_NightMischief_Round.wav", 0.42f, false, false, FindGroup(mixer, "Music"));
                CreateBed(root.transform, "Ambience_NightHouse", "AMB_NightHouse.wav", 0.40f, true, false, FindGroup(mixer, "Ambience"));

                var catalog = root.AddComponent<AlfaAudioCatalog>();
                Assign(catalog, "strikeSwing", cues["StrikeSwing"]);
                Assign(catalog, "strikeImpact", cues["StrikeImpact"]);
                Assign(catalog, "biteStarted", cues["BiteStarted"]);
                Assign(catalog, "mosquitoWingLoop", cues["MosquitoWingLoop"]);
                Assign(catalog, "doorOpen", cues["DoorOpen"]);
                Assign(catalog, "doorClose", cues["DoorClose"]);
                Assign(catalog, "humanFainted", cues["HumanFainted"]);
                Assign(catalog, "recovered", cues["Recovered"]);
                Assign(catalog, "roundStart", cues["RoundStart"]);
                Assign(catalog, "humansWin", cues["HumansWin"]);
                Assign(catalog, "mosquitoesWin", cues["MosquitoesWin"]);
                Assign(catalog, "uiReady", cues["UiReady"]);

                PrefabUtility.SaveAsPrefabAsset(root, AudioRoot + "/Prefabs/LMS_AlfaAudioRoot.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateBed(
            Transform parent, string name, string clipName, float volume,
            bool playOnStart, bool spatial, AudioMixerGroup output)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            AudioBedPlayer player = child.AddComponent<AudioBedPlayer>();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipRoot}/{clipName}");
            if (clip == null)
                throw new FileNotFoundException("Audio clip failed to import", clipName);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("clip").objectReferenceValue = clip;
            serialized.FindProperty("output").objectReferenceValue = output;
            serialized.FindProperty("volume").floatValue = volume;
            serialized.FindProperty("playOnStart").boolValue = playOnStart;
            serialized.FindProperty("spatial").boolValue = spatial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string exactName)
        {
            if (mixer == null)
                return null;
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(exactName);
            for (int i = 0; i < groups.Length; i++)
                if (string.Equals(groups[i].name, exactName, StringComparison.Ordinal))
                    return groups[i];
            Debug.LogWarning($"LMS_AUDIO_GROUP_REQUIRED mixer={mixer.name} group={exactName}", mixer);
            return null;
        }

        private static void ConfigureAudioImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                    continue;

                string file = Path.GetFileName(path);
                bool music = file.StartsWith("MUS_", StringComparison.Ordinal);
                bool ambience = file.StartsWith("AMB_", StringComparison.Ordinal);
                bool loop = music || ambience || file.Contains("Loop");
                bool shortCritical = file.Contains("Impact") || file.Contains("Bite") ||
                    file.Contains("Fainted") || file.Contains("Recovered") ||
                    file.StartsWith("STG_", StringComparison.Ordinal) ||
                    file.StartsWith("UI_", StringComparison.Ordinal);

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming :
                    ambience ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = music || ambience
                    ? AudioCompressionFormat.Vorbis
                    : shortCritical ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
                settings.quality = music ? 0.75f : ambience ? 0.65f : 1f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !(music || ambience);
                importer.loadInBackground = music || ambience;
                importer.userData = loop ? "lms-loop=true" : "lms-loop=false";
                importer.SaveAndReimport();
            }
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;
            return profile.Add<T>(true);
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, pieces[i]);
                current = next;
            }
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new MissingFieldException(target.GetType().Name, field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }
    }
}
