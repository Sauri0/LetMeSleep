using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Content.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Editor
{
    /// <summary>Builds the decorative actor independently of the approved gameplay character and IDs.</summary>
    public static class LivingMenuContentBuilder
    {
        private const string Root = "Assets/LetMeSleep/Content/Characters";
        private const string Model = Root + "/Models/LMS_HumanMenu.fbx";
        private const string Prefab = Root + "/Prefabs/LMS_HumanMenu.prefab";
        private static readonly string[] States = { "MenuSeatedIdle", "MenuLook", "MenuSwat", "MenuReturn" };

        public static void BuildAndBind(AlfaApplication app)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../art_source/unity/characters/menu/LMS_HumanMenu.fbx"));
            if (!File.Exists(source)) throw new FileNotFoundException("Authored living-menu character is required before rebuilding the alpha boot scene.", source);
            var bytes = File.ReadAllBytes(source);
            if (!File.Exists(Model) || !File.ReadAllBytes(Model).SequenceEqual(bytes)) File.WriteAllBytes(Model, bytes);
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true; importer.optimizeGameObjects = false; importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = true; // Preserve authored eyelids in menu and gameplay.
            importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false; importer.motionNodeName = "Root";
            var takes = importer.defaultClipAnimations;
            importer.clipAnimations = States.Select(name =>
            {
                var take = takes.SingleOrDefault(t => t.name == name || t.name.EndsWith("|" + name, StringComparison.Ordinal));
                if (take == null) throw new InvalidDataException("Missing menu take " + name);
                return new ModelImporterClipAnimation
                {
                    name = name, takeName = take.takeName, firstFrame = take.firstFrame, lastFrame = take.lastFrame,
                    loopTime = name == States[0], loopPose = false, lockRootRotation = true, lockRootHeightY = true,
                    lockRootPositionXZ = true, keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true
                };
            }).ToArray();
            // Reuse the production URP palette; importing the candidate must not rewrite shared materials.
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
            {
                var shared = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + material.name + ".mat");
                if (!shared) throw new InvalidDataException("Missing menu material " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), shared);
            }
            importer.SaveAndReimport();
            BuildDecorativePrefab(app.HumanPrefab);
            app.MenuHumanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            app.MenuFlyswatterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LMS_Flyswatter.prefab");
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().ToArray();
            app.MenuSeatedIdle = clips.Single(c => c.name == States[0]);
            app.MenuLook = clips.Single(c => c.name == States[1]);
            app.MenuSwat = clips.Single(c => c.name == States[2]);
            app.MenuReturn = clips.Single(c => c.name == States[3]);
            app.MenuMosquitoFlight = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/LMS_Mosquito_alpha.fbx")
                .OfType<AnimationClip>().Single(c => c.name == "Mosquito_Fly");
            if (!app.MenuFlyswatterPrefab) throw new InvalidDataException("Missing menu flyswatter prefab");
            Debug.Log("LMS_LIVING_MENU_CONTENT_BUILT: dedicated actor, four seated clips and mosquito flight; native visual review pending.");
        }

        private static void BuildDecorativePrefab(GameObject production)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Object.Instantiate(production);
                SceneManager.MoveGameObjectToScene(root, scene);
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "LMS_HumanMenu";
                var view = root.GetComponent<CharacterView>();
                var oldModel = view.Animator.transform;
                var paths = view.Anchors.Select(a => AnimationUtility.CalculateTransformPath(a.SourceBone, oldModel)).ToArray();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), scene);
                // Keep the production SourceOrientation wrapper, not only VisualRoot's scale.
                instance.transform.SetParent(oldModel.parent, false);
                instance.transform.SetLocalPositionAndRotation(oldModel.localPosition, oldModel.localRotation);
                instance.transform.localScale = oldModel.localScale;
                view.Animator = instance.GetComponent<Animator>();
                if (!view.Animator || !view.Animator.avatar || !view.Animator.avatar.isValid)
                    throw new InvalidDataException("Menu Generic avatar invalid");
                view.Animator.runtimeAnimatorController = null;
                view.Animator.applyRootMotion = false;
                view.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                RestoreBindPose(instance.transform);
                for (int i = 0; i < paths.Length; i++)
                {
                    var bone = instance.transform.Find(paths[i]);
                    if (!bone) throw new InvalidDataException("Menu rig anchor missing " + paths[i]);
                    view.Anchors[i].SourceBone = bone;
                    view.Anchors[i].RotationOffset = Quaternion.Inverse(bone.rotation) * root.transform.rotation;
                }
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                        AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + m.name + ".mat")
                        ?? throw new InvalidDataException("Missing shared menu palette " + m.name)).ToArray();
                    if (renderer is SkinnedMeshRenderer skin) skin.quality = SkinQuality.Bone4;
                }
                view.Colors = renderers.SelectMany(r => r.sharedMaterials.Select((m, i) => new CharacterView.ColorBinding
                {
                    Renderer = r, MaterialIndex = i,
                    Category = m.name == "Human_Skin" ? "Skin" : m.name == "Human_Pajamas" ? "Pajamas" : null
                })).Where(binding => binding.Category != null).ToArray();
                view.HeadRenderers = renderers.Where(r => r.name == "HumanHead" || r.name == "HumanNightcap").ToArray();
                view.Motions = Array.Empty<CharacterView.MotionBinding>();
                Object.DestroyImmediate(oldModel.gameObject);
                view.RefreshAnchors();
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                if (!PrefabUtility.SaveAsPrefabAsset(root, Prefab)) throw new InvalidOperationException("Could not save decorative human prefab");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void RestoreBindPose(Transform model)
        {
            var matrices = new Dictionary<Transform, Matrix4x4>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var poses = skin.sharedMesh.bindposes;
                if (poses.Length != skin.bones.Length) throw new InvalidDataException("Menu bind-pose count mismatch");
                for (int i = 0; i < poses.Length; i++)
                {
                    var bone = skin.bones[i];
                    if (!bone) throw new InvalidDataException("Menu bind bone missing");
                    var matrix = skin.localToWorldMatrix * poses[i].inverse;
                    if (matrices.TryGetValue(bone, out var previous))
                        for (int j = 0; j < 16; j++)
                            if (Mathf.Abs(previous[j] - matrix[j]) > .0001f) throw new InvalidDataException("Inconsistent menu bind pose " + bone.name);
                    matrices[bone] = matrix;
                }
            }
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (!matrices.TryGetValue(bone, out var world)) continue;
                var local = bone.parent ? bone.parent.worldToLocalMatrix * world : world;
                bone.localPosition = local.GetColumn(3); bone.localRotation = local.rotation; bone.localScale = local.lossyScale;
            }
        }
    }
}
