using System;
using System.Collections.Generic;
using System.IO;
using LetMeSleep.Audio;
using LetMeSleep.Presentation.Gameplay;
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
            Material nightSkybox = BuildNightSkybox();
            BuildMaterials();
            BuildLightingPrefab(preset, volume, nightSkybox);
            ParticleSystem impactVfx = BuildImpactVfx();
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                Debug.LogWarning($"LMS_AUDIO_MIXER_REQUIRED path={MixerPath}; clips remain audible through Master until the mixer is created and the builder is rerun.");
            Dictionary<string, AudioCue> cues = BuildAudioCues(mixer);
            GameObject audioRoot = BuildAudioRoot(cues, mixer);
            BuildGameplayPresentationPrefab(preset, audioRoot, impactVfx);

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
            bloom.intensity.Override(0.025f);
            bloom.threshold.Override(1.35f);
            bloom.scatter.Override(0.35f);

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
            CreateMaterial("ImpactParticle", "Universal Render Pipeline/Particles/Unlit",
                new Color(0.90f, 0.76f, 0.26f, 0.86f), 0f, 0f, true);
        }

        private static Material BuildNightSkybox()
        {
            const string path = PresentationRoot + "/Materials/NightSkybox.mat";
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                throw new InvalidOperationException("Required shader unavailable: Skybox/Procedural");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "NightSkybox" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetFloat(material, "_SunDisk", 0f);
            SetFloat(material, "_SunSize", 0.01f);
            SetFloat(material, "_AtmosphereThickness", 0.28f);
            SetColor(material, "_SkyTint", new Color(0.065f, 0.095f, 0.17f));
            SetColor(material, "_GroundColor", new Color(0.018f, 0.026f, 0.052f));
            SetFloat(material, "_Exposure", 0.26f);
            EditorUtility.SetDirty(material);
            return material;
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

        private static void BuildLightingPrefab(
            AlfaPresentationPreset preset, VolumeProfile profile, Material nightSkybox)
        {
            var root = new GameObject("LMS_AlfaLightingRoot");
            try
            {
                var moonObject = new GameObject("Moon_MainDirectional");
                moonObject.transform.SetParent(root.transform, false);
                moonObject.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
                Light moon = moonObject.AddComponent<Light>();

                var lobbyFillObject = new GameObject("Lobby_CharacterFill");
                lobbyFillObject.transform.SetParent(root.transform, false);
                lobbyFillObject.transform.localRotation = Quaternion.LookRotation(
                    new Vector3(-0.55f, -0.50f, 0.67f).normalized, Vector3.up);
                Light lobbyFill = lobbyFillObject.AddComponent<Light>();
                lobbyFill.type = LightType.Directional;
                lobbyFill.shadows = LightShadows.None;
                lobbyFill.enabled = false;

                Light mapLightLowTemplate = CreateMapLightTemplate(
                    root.transform,
                    "MapPointLight_LowTemplate",
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
                Light mapLightMediumTemplate = CreateMapLightTemplate(
                    root.transform,
                    "MapPointLight_MediumTemplate",
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium);

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
                Assign(rig, "lobbyFill", lobbyFill);
                Assign(rig, "globalVolume", volume);
                Assign(rig, "nightSkybox", nightSkybox);
                Assign(rig, "mapLightLowTemplate", mapLightLowTemplate);
                Assign(rig, "mapLightMediumTemplate", mapLightMediumTemplate);
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

        private static Light CreateMapLightTemplate(Transform parent, string name, int resolutionTier)
        {
            var templateObject = new GameObject(name);
            templateObject.transform.SetParent(parent, false);
            Light template = templateObject.AddComponent<Light>();
            template.type = LightType.Point;
            template.shadows = LightShadows.Soft;

            UniversalAdditionalLightData additionalLightData =
                templateObject.AddComponent<UniversalAdditionalLightData>();
            var serializedLightData = new SerializedObject(additionalLightData);
            SerializedProperty tierProperty = serializedLightData.FindProperty(
                "m_AdditionalLightsShadowResolutionTier");
            if (tierProperty == null)
                throw new InvalidOperationException(
                    "URP additional-light shadow resolution tier is unavailable.");
            tierProperty.intValue = resolutionTier;
            serializedLightData.ApplyModifiedPropertiesWithoutUndo();

            template.enabled = false;
            templateObject.SetActive(false);
            return template;
        }

        private static ParticleSystem BuildImpactVfx()
        {
            var root = new GameObject("LMS_ImpactBurst");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = 0.24f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.98f, 0.83f, 0.30f, 0.90f),
                    new Color(0.72f, 0.88f, 0.96f, 0.68f));
                main.maxParticles = 16;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(
                        0f, new ParticleSystem.MinMaxCurve(6f, 12f), 1, 0f)
                });
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.03f;

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    PresentationRoot + "/Materials/ImpactParticle.mat");
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root, PresentationRoot + "/Prefabs/LMS_ImpactBurst.prefab");
                return prefab.GetComponent<ParticleSystem>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, AudioCue> BuildAudioCues(AudioMixer mixer)
        {
            var cues = new Dictionary<string, AudioCue>();
            cues["StrikeSwing"] = CreateCue("StrikeSwing", new[] { "SFX_StrikeSwing.wav", "SFX_Legacy_Swish.ogg" }, 40, 4, 0.98f, 1.03f, 1f, 0.8f, 18f, FindGroup(mixer, "Character"));
            cues["StrikeImpact"] = CreateCue("StrikeImpact", new[] { "SFX_StrikeImpact.wav", "SFX_Legacy_Clap.ogg", "SFX_Legacy_Impact.ogg" }, 24, 4, 0.97f, 1.03f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["BiteStarted"] = CreateCue("BiteStarted", new[] { "SFX_BiteStart.wav", "SFX_Legacy_Bite.ogg" }, 24, 8, 0.98f, 1.02f, 1f, 0.35f, 12f, FindGroup(mixer, "Critical"));
            cues["MosquitoWingLoop"] = CreateCue("MosquitoWingLoop", "SFX_Legacy_Buzz_Flight.ogg", 56, 12, 0.92f, 1.12f, 1f, 0.35f, 12f, FindGroup(mixer, "Mosquito"), true);
            cues["MosquitoWingPerchLoop"] = CreateCue("MosquitoWingPerchLoop", "SFX_Legacy_Buzz_Perch.ogg", 56, 12, 0.72f, 0.84f, 1f, 0.35f, 10f, FindGroup(mixer, "Mosquito"), true);
            cues["MosquitoWingBiteLoop"] = CreateCue("MosquitoWingBiteLoop", "SFX_Legacy_Buzz_Bite.ogg", 48, 12, 0.90f, 1.04f, 1f, 0.35f, 12f, FindGroup(mixer, "Mosquito"), true);
            cues["MosquitoPerch"] = CreateCue("MosquitoPerch", "SFX_Legacy_Perch.ogg", 56, 6, 0.96f, 1.04f, 1f, 0.35f, 10f, FindGroup(mixer, "Mosquito"));
            cues["MosquitoDetach"] = CreateCue("MosquitoDetach", "SFX_Legacy_Detach.ogg", 56, 6, 0.96f, 1.04f, 1f, 0.35f, 10f, FindGroup(mixer, "Mosquito"));
            AudioMixerGroup characterGroup = FindGroup(mixer, "Character");
            AudioMixerGroup worldGroup = FindGroup(mixer, "World");
            cues["HumanFootstep"] = CreateCue("HumanFootstep", "SFX_Legacy_Step_Wood.ogg", 80, 12, 0.94f, 1.06f, 1f, 0.7f, 15f, characterGroup);
            cues["HumanFootstepTile"] = CreateCue("HumanFootstepTile", "SFX_Legacy_Step_Tile.ogg", 80, 12, 0.94f, 1.06f, 1f, 0.7f, 15f, characterGroup);
            cues["HumanFootstepCloth"] = CreateCue("HumanFootstepCloth", "SFX_Legacy_Step_Cloth.ogg", 80, 12, 0.94f, 1.06f, 1f, 0.7f, 15f, characterGroup);
            cues["HumanJump"] = CreateCue("HumanJump", "SFX_Legacy_Cloth.ogg", 72, 8, 0.96f, 1.04f, 1f, 0.7f, 14f, characterGroup);
            cues["HumanLand"] = CreateCue("HumanLand", "SFX_Legacy_Land_Wood.ogg", 64, 8, 0.96f, 1.03f, 1f, 0.7f, 16f, characterGroup);
            cues["HumanLandTile"] = CreateCue("HumanLandTile", "SFX_Legacy_Land_Tile.ogg", 64, 8, 0.96f, 1.03f, 1f, 0.7f, 16f, characterGroup);
            cues["HumanLandCloth"] = CreateCue("HumanLandCloth", "SFX_Legacy_Land_Cloth.ogg", 64, 8, 0.96f, 1.03f, 1f, 0.7f, 16f, characterGroup);
            cues["ToolPickup"] = CreateCue("ToolPickup", "SFX_Legacy_Pickup.ogg", 72, 6, 0.97f, 1.03f, 1f, 0.7f, 14f, worldGroup);
            cues["ToolDrop"] = CreateCue("ToolDrop", "SFX_Legacy_Drop.ogg", 72, 6, 0.96f, 1.04f, 1f, 0.7f, 14f, worldGroup);
            cues["MosquitoKnockedDown"] = CreateCue("MosquitoKnockedDown", new[] { "SFX_Legacy_Stun.ogg", "SFX_Legacy_Fall.ogg" }, 24, 4, 0.97f, 1.03f, 1f, 0.35f, 18f, FindGroup(mixer, "Critical"));
            cues["DoorOpen"] = CreateCue("DoorOpen", new[] { "SFX_DoorOpen.wav", "SFX_Legacy_Door_Move.ogg" }, 96, 10, 0.98f, 1.02f, 1f, 0.8f, 20f, FindGroup(mixer, "World"));
            cues["DoorClose"] = CreateCue("DoorClose", new[] { "SFX_DoorClose.wav", "SFX_Legacy_Door_Latch.ogg" }, 96, 10, 0.98f, 1.02f, 1f, 0.8f, 20f, FindGroup(mixer, "World"));
            cues["HumanFainted"] = CreateCue("HumanFainted", "SFX_HumanFainted.wav", 24, 4, 0.98f, 1.02f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["Recovered"] = CreateCue("Recovered", new[] { "SFX_Recovered.wav", "SFX_Legacy_Recover.ogg" }, 24, 4, 0.98f, 1.02f, 1f, 0.7f, 22f, FindGroup(mixer, "Critical"));
            cues["RoundStart"] = CreateCue("RoundStart", "STG_RoundStart.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["HumansWin"] = CreateCue("HumansWin", "STG_HumansWin.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["MosquitoesWin"] = CreateCue("MosquitoesWin", "STG_MosquitoesWin.wav", 24, 1, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "Critical"));
            cues["UiReady"] = CreateCue("UiReady", "UI_Ready.wav", 48, 4, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "UI"));
            cues["UiSelect"] = CreateCue("UiSelect", "UI_Legacy_Select.ogg", 48, 4, 0.99f, 1.01f, 0f, 1f, 1f, FindGroup(mixer, "UI"));
            cues["UiConfirm"] = CreateCue("UiConfirm", "UI_Legacy_Confirm.ogg", 40, 4, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "UI"));
            cues["UiError"] = CreateCue("UiError", "UI_Legacy_Error.ogg", 32, 2, 1f, 1f, 0f, 1f, 1f, FindGroup(mixer, "UI"));
            return cues;
        }

        private static AudioCue CreateCue(
            string id, string clipName, int priority, int simultaneous,
            float minimumPitch, float maximumPitch, float spatialBlend,
            float minimumDistance, float maximumDistance, AudioMixerGroup output,
            bool loop = false)
        {
            return CreateCue(id, new[] { clipName }, priority, simultaneous,
                minimumPitch, maximumPitch, spatialBlend, minimumDistance,
                maximumDistance, output, loop);
        }

        private static AudioCue CreateCue(
            string id, string[] clipNames, int priority, int simultaneous,
            float minimumPitch, float maximumPitch, float spatialBlend,
            float minimumDistance, float maximumDistance, AudioMixerGroup output,
            bool loop = false)
        {
            string path = $"{AudioRoot}/Cues/{id}.asset";
            AudioCue cue = CreateOrLoad<AudioCue>(path);

            var serialized = new SerializedObject(cue);
            serialized.FindProperty("cueId").stringValue = id;
            SerializedProperty clips = serialized.FindProperty("clips");
            clips.arraySize = clipNames.Length;
            for (int i = 0; i < clipNames.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipRoot}/{clipNames[i]}");
                if (clip == null)
                    throw new FileNotFoundException("Audio clip failed to import", clipNames[i]);
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
            serialized.FindProperty("output").objectReferenceValue = output;
            serialized.FindProperty("priority").intValue = priority;
            serialized.FindProperty("maximumSimultaneous").intValue = simultaneous;
            serialized.FindProperty("minimumPitch").floatValue = minimumPitch;
            serialized.FindProperty("maximumPitch").floatValue = maximumPitch;
            serialized.FindProperty("spatialBlend").floatValue = spatialBlend;
            serialized.FindProperty("minimumDistance").floatValue = minimumDistance;
            serialized.FindProperty("maximumDistance").floatValue = maximumDistance;
            serialized.FindProperty("loop").boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cue);
            return cue;
        }

        private static GameObject BuildAudioRoot(Dictionary<string, AudioCue> cues, AudioMixer mixer)
        {
            var root = new GameObject("LMS_AlfaAudioRoot");
            try
            {
                AudioEmitterPool emitters = root.AddComponent<AudioEmitterPool>();
                AudioMixerGroup musicGroup = FindGroup(mixer, "Music");
                AudioMixerGroup ambienceGroup = FindGroup(mixer, "Ambience");
                AudioBedPlayer menu = CreateBed(root.transform, "Music_Menu_Base", "MUS_Legacy_Menu_Base.ogg", 0.70f, false, false, musicGroup);
                AudioBedPlayer menuRhythm = CreateBed(root.transform, "Music_Menu_Rhythm", "MUS_Legacy_Menu_Rhythm.ogg", 0.50f, false, false, musicGroup);
                AudioBedPlayer menuMelody = CreateBed(root.transform, "Music_Menu_Melody", "MUS_Legacy_Menu_Melody.ogg", 0.45f, false, false, musicGroup);
                AudioBedPlayer round = CreateBed(root.transform, "Music_Round_Base", "MUS_Legacy_Gameplay_Base.ogg", 0.62f, false, false, musicGroup);
                AudioBedPlayer roundRhythm = CreateBed(root.transform, "Music_Round_Rhythm", "MUS_Legacy_Gameplay_Rhythm.ogg", 0.42f, false, false, musicGroup);
                AudioBedPlayer roundMelody = CreateBed(root.transform, "Music_Round_Melody", "MUS_Legacy_Gameplay_Melody.ogg", 0.35f, false, false, musicGroup);
                AudioBedPlayer quiet = CreateBed(root.transform, "Music_Quiet", "MUS_Legacy_Quiet.ogg", 0.68f, false, false, musicGroup);
                AudioBedPlayer ambience = CreateBed(root.transform, "Ambience_NightHouse", "AMB_NightHouse.wav", 0.30f, false, false, ambienceGroup);
                AudioBedPlayer nightAir = CreateBed(root.transform, "Ambience_NightAir", "AMB_Legacy_NightAir.ogg", 0.22f, false, false, ambienceGroup);

                var catalog = root.AddComponent<AlfaAudioCatalog>();
                Assign(catalog, "strikeSwing", cues["StrikeSwing"]);
                Assign(catalog, "strikeImpact", cues["StrikeImpact"]);
                Assign(catalog, "biteStarted", cues["BiteStarted"]);
                Assign(catalog, "mosquitoWingLoop", cues["MosquitoWingLoop"]);
                Assign(catalog, "mosquitoWingPerchLoop", cues["MosquitoWingPerchLoop"]);
                Assign(catalog, "mosquitoWingBiteLoop", cues["MosquitoWingBiteLoop"]);
                Assign(catalog, "mosquitoPerch", cues["MosquitoPerch"]);
                Assign(catalog, "mosquitoDetach", cues["MosquitoDetach"]);
                Assign(catalog, "humanFootstep", cues["HumanFootstep"]);
                Assign(catalog, "humanFootstepTile", cues["HumanFootstepTile"]);
                Assign(catalog, "humanFootstepCloth", cues["HumanFootstepCloth"]);
                Assign(catalog, "humanJump", cues["HumanJump"]);
                Assign(catalog, "humanLand", cues["HumanLand"]);
                Assign(catalog, "humanLandTile", cues["HumanLandTile"]);
                Assign(catalog, "humanLandCloth", cues["HumanLandCloth"]);
                Assign(catalog, "toolPickup", cues["ToolPickup"]);
                Assign(catalog, "toolDrop", cues["ToolDrop"]);
                Assign(catalog, "mosquitoKnockedDown", cues["MosquitoKnockedDown"]);
                Assign(catalog, "doorOpen", cues["DoorOpen"]);
                Assign(catalog, "doorClose", cues["DoorClose"]);
                Assign(catalog, "humanFainted", cues["HumanFainted"]);
                Assign(catalog, "recovered", cues["Recovered"]);
                Assign(catalog, "roundStart", cues["RoundStart"]);
                Assign(catalog, "humansWin", cues["HumansWin"]);
                Assign(catalog, "mosquitoesWin", cues["MosquitoesWin"]);
                Assign(catalog, "uiReady", cues["UiReady"]);
                Assign(catalog, "uiSelect", cues["UiSelect"]);
                Assign(catalog, "uiConfirm", cues["UiConfirm"]);
                Assign(catalog, "uiError", cues["UiError"]);

                AlfaAudioDirector director = root.AddComponent<AlfaAudioDirector>();
                Assign(director, "emitters", emitters);
                Assign(director, "catalog", catalog);
                Assign(director, "menuMusic", menu);
                Assign(director, "menuRhythm", menuRhythm);
                Assign(director, "menuMelody", menuMelody);
                Assign(director, "roundMusic", round);
                Assign(director, "roundRhythm", roundRhythm);
                Assign(director, "roundMelody", roundMelody);
                Assign(director, "quietMusic", quiet);
                Assign(director, "ambience", ambience);
                Assign(director, "nightAir", nightAir);

                return PrefabUtility.SaveAsPrefabAsset(root, AudioRoot + "/Prefabs/LMS_AlfaAudioRoot.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildGameplayPresentationPrefab(
            AlfaPresentationPreset preset, GameObject audioRootPrefab,
            ParticleSystem impactVfx)
        {
            const string characterRoot = "Assets/LetMeSleep/Content/Characters/Prefabs";
            GameObject human = AssetDatabase.LoadAssetAtPath<GameObject>(characterRoot + "/LMS_Human.prefab");
            GameObject humanFirstPerson = AssetDatabase.LoadAssetAtPath<GameObject>(characterRoot + "/LMS_Human_FirstPerson.prefab");
            GameObject mosquito = AssetDatabase.LoadAssetAtPath<GameObject>(characterRoot + "/LMS_Mosquito.prefab");
            GameObject flyswatter = AssetDatabase.LoadAssetAtPath<GameObject>(characterRoot + "/LMS_Flyswatter.prefab");
            if (human == null || humanFirstPerson == null || mosquito == null || flyswatter == null || audioRootPrefab == null)
            {
                Debug.LogWarning("LMS_GAMEPLAY_PRESENTATION_DEFERRED; run the Character builder first, then rerun this builder.");
                return;
            }

            var root = new GameObject("LMS_GameplayPresentation");
            try
            {
                GameplayVisualPresenter visuals = root.AddComponent<GameplayVisualPresenter>();
                LobbyVisualPresenter lobbyVisuals = root.AddComponent<LobbyVisualPresenter>();
                GameplayAudioPresenter audioEvents = root.AddComponent<GameplayAudioPresenter>();
                GameplayVfxPresenter vfxEvents = root.AddComponent<GameplayVfxPresenter>();
                GameplayPresentationRoot facade = root.AddComponent<GameplayPresentationRoot>();
                visuals.SetPrefabs(human, humanFirstPerson, mosquito, flyswatter);

                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                AudioListener listener = cameraObject.AddComponent<AudioListener>();
                listener.enabled = false;
                HumanViewCamera humanCamera = cameraObject.AddComponent<HumanViewCamera>();
                MosquitoFollowCamera mosquitoCamera = cameraObject.AddComponent<MosquitoFollowCamera>();
                Assign(humanCamera, "preset", preset);
                Assign(humanCamera, "controlledCamera", camera);
                Assign(humanCamera, "pitchPivot", cameraObject.transform);
                Assign(mosquitoCamera, "preset", preset);
                Assign(mosquitoCamera, "controlledCamera", camera);
                Assign(mosquitoCamera, "cameraTransform", cameraObject.transform);
                humanCamera.enabled = false;
                mosquitoCamera.enabled = false;
                visuals.SetCameras(humanCamera, mosquitoCamera);

                GameObject audioInstance = (GameObject)PrefabUtility.InstantiatePrefab(audioRootPrefab);
                audioInstance.transform.SetParent(root.transform, false);
                AlfaAudioDirector audioDirector = audioInstance.GetComponent<AlfaAudioDirector>();
                Assign(facade, "visuals", visuals);
                Assign(facade, "lobbyVisuals", lobbyVisuals);
                Assign(facade, "audioEvents", audioEvents);
                Assign(facade, "vfxEvents", vfxEvents);
                Assign(facade, "audioDirector", audioDirector);
                Assign(vfxEvents, "impactPrefab", impactVfx);

                PrefabUtility.SaveAsPrefabAsset(
                    root, PresentationRoot + "/Prefabs/LMS_GameplayPresentation.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AudioBedPlayer CreateBed(
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
            return player;
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
