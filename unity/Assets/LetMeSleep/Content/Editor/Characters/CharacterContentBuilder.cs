using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Characters.Editor
{
    /// <summary>Run in the Director's resident editor. Never edits project settings or open scenes.</summary>
    public static class CharacterContentBuilder
    {
        public const string OutputRoot = "Assets/LetMeSleep/Content/Characters";
        public const string ReceiptPath = OutputRoot + "/BuildReceipt.json";
        private const string BuilderVersion = "alpha-characters-6-wing-transmission";
        private static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../art_source/unity/characters"));
        private static readonly string[] HumanStates = {
            "Idle", "Walk", "Run", "Crouch", "Jump", "Land", "Turn", "Clap", "Hit", "Fall",
            "Faint", "Recover", "Swat", "Blink", "FingerCurl"
        };
        private static readonly string[] MosquitoStates = {
            "Idle", "Hover", "Fly", "Brake", "PerchEnter", "PerchIdle", "SurfaceWalk", "BiteStart",
            "BiteLoop", "Detach", "Hit", "Fall", "Recover", "Land", "Bite"
        };
        private static readonly string[,] HumanAnchors = {
            { "CameraEye", "Socket.Eye" }, { "AimChest", "Socket.AimChest" },
            { "HandGrip_L", "Socket.Grip.L" }, { "HandGrip_R", "Socket.Grip.R" },
            { "ToolSocket_R", "Socket.Grip.R" }, { "Foot_L", "Socket.Foot.L" }, { "Foot_R", "Socket.Foot.R" }
        };
        private static readonly string[,] MosquitoAnchors = {
            { "CameraTarget", "Socket.CameraTarget" }, { "AimForward", "Socket.AimForward" },
            { "ProboscisTip", "Socket.Mouth" }, { "WingRoot_L", "Socket.WingRoot.L" },
            { "WingRoot_R", "Socket.WingRoot.R" }, { "GroundContact", "Socket.GroundContact" }
        };

        // Populated by JsonUtility, never assigned directly by C#.
#pragma warning disable CS0649
        [Serializable] private sealed class SourceClip
        {
            public string name;
            public float duration_seconds;
            public bool loop;
        }
        [Serializable] private sealed class SourceMaterial
        {
            public string name;
            public Color color;
            public float roughness;
        }
        [Serializable] private sealed class SourceAudit
        {
            public string species;
            public int triangles;
            public int bones;
            public int mesh_count;
            public bool passed;
            public string[] bone_names;
            public SourceClip[] clips;
            public SourceMaterial[] material_palette;
        }
#pragma warning restore CS0649
        [Serializable] public sealed class AssetRecord { public string path; public string guid; }
        [Serializable] public sealed class AnchorRecord { public string name; public string bonePath; public string anchorPath; }
        [Serializable] public sealed class OrientationRecord
        {
            public Vector3 frontPointActorLocal;
            public Vector3 leftPointActorLocal;
            public Vector3 rightPointActorLocal;
            public Vector3 sourceCorrectionEuler;
            public float nearestTipVertexDistance;
            public bool bakeMeshScaleCompensated;
        }
        [Serializable] public sealed class ValidationRecord
        {
            public string prefab;
            public int triangles;
            public int renderers;
            public int bones;
            public int clips;
            public bool genericAvatarValid;
            public bool animationBindingsValid;
            public bool sampledMeshesFinite;
            public bool rootStationary;
            public float maximumLoopFootDelta;
            public CharacterView.MotionBinding[] motions;
            public AnchorRecord[] anchors;
            public string[] bonePaths;
            public OrientationRecord orientation;
        }
        [Serializable] public sealed class BuildReceipt
        {
            public string builder = BuilderVersion;
            public string unityVersion;
            public string sourceDigest;
            public string builderDigest;
            public bool success;
            public string scope = "Imported assets and sampled animation structure; no playtest, visual approval or performance claim";
            public string[] errors;
            public AssetRecord[] assets;
            public ValidationRecord[] validations;
        }

        [MenuItem("Let Me Sleep/Content/Build Characters")]
        public static void BuildAll()
        {
            EnsureFolder(OutputRoot);
            var receipt = new BuildReceipt { unityVersion = Application.unityVersion };
            try
            {
                receipt.sourceDigest = SourceDigest();
                receipt.builderDigest = Digest(new[] { Absolute(OutputRoot + "/Runtime/CharacterView.cs"),
                    Absolute(OutputRoot + "/Runtime/ToolView.cs"),
                    Absolute("Assets/LetMeSleep/Content/Editor/Characters/CharacterContentBuilder.cs") });
                var human = ReadAudit("Human");
                var mosquito = ReadAudit("Mosquito");
                var tool = ReadAudit("Flyswatter");
                ImportModel(human); ImportModel(mosquito); ImportModel(tool);
                var humanController = BuildController(human, HumanStates);
                var mosquitoController = BuildController(mosquito, MosquitoStates);
                BuildCharacter(human, humanController, HumanStates, false);
                BuildCharacter(human, humanController, HumanStates, true);
                BuildCharacter(mosquito, mosquitoController, MosquitoStates, false);
                BuildTool(tool);
                AssetDatabase.SaveAssets();
                receipt.validations = new[] {
                    ValidateCharacter("LMS_Human", human, false),
                    ValidateCharacter("LMS_Human_FirstPerson", human, true),
                    ValidateCharacter("LMS_Mosquito", mosquito, false),
                    ValidateTool(tool)
                };
                receipt.assets = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith(OutputRoot + "/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)
                        && p != ReceiptPath && !p.EndsWith(".cs", StringComparison.Ordinal)
                        && !p.EndsWith(".asmdef", StringComparison.Ordinal))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => new AssetRecord { path = p, guid = AssetDatabase.AssetPathToGUID(p) }).ToArray();
                Require(receipt.assets.All(a => !string.IsNullOrEmpty(a.guid)), "Generated asset without GUID");
                receipt.success = true;
                receipt.errors = Array.Empty<string>();
                WriteReceipt(receipt);
                Debug.Log("LMS_CHARACTER_BUILD_PASSED " + ReceiptPath);
            }
            catch (Exception exception)
            {
                receipt.success = false;
                receipt.errors = new[] { exception.ToString() };
                WriteReceipt(receipt);
                Debug.LogError("LMS_CHARACTER_BUILD_FAILED " + exception.Message);
                throw;
            }
        }

        [MenuItem("Let Me Sleep/Content/Build Characters and Check Idempotence")]
        public static void BuildAndVerifyIdempotence()
        {
            BuildAll();
            var before = GeneratedDigest();
            BuildAll();
            var after = GeneratedDigest();
            Require(before == after, "Character build changed generated asset bytes on its second identical run");
            Debug.Log("LMS_CHARACTER_IDEMPOTENCE_PASSED " + after);
        }

        private static SourceAudit ReadAudit(string species)
        {
            var path = Path.Combine(SourceRoot, species.ToLowerInvariant(), "audit.json");
            Require(File.Exists(path), "Missing source audit: " + path);
            var audit = JsonUtility.FromJson<SourceAudit>(File.ReadAllText(path));
            Require(audit != null && audit.passed && audit.species == species, "Invalid source audit: " + path);
            Require(audit.material_palette != null && audit.material_palette.Length > 0, "Source palette missing: " + species);
            return audit;
        }

        private static string ModelPath(string species) => OutputRoot + "/Models/LMS_" + species + "_alpha.fbx";
        private static string PrefabPath(string name) => OutputRoot + "/Prefabs/" + name + ".prefab";

        private static void ImportModel(SourceAudit audit)
        {
            EnsureFolder(OutputRoot + "/Models"); EnsureFolder(OutputRoot + "/Materials");
            var path = ModelPath(audit.species);
            var source = Path.Combine(SourceRoot, audit.species.ToLowerInvariant(), "LMS_" + audit.species + "_alpha.fbx");
            Require(File.Exists(source), "Missing FBX: " + source);
            WriteIfChanged(path, File.ReadAllBytes(source));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer != null, "FBX did not produce a ModelImporter: " + path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = true; // Preserve authored eyelids; facial animation is shared by menu and gameplay.
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.animationType = audit.species == "Flyswatter" ? ModelImporterAnimationType.None : ModelImporterAnimationType.Generic;
            importer.avatarSetup = audit.clips.Length > 0 ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = audit.clips.Length > 0;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            if (audit.clips.Length > 0)
            {
                importer.motionNodeName = "Root";
                var defaults = importer.defaultClipAnimations;
                importer.clipAnimations = audit.clips.Select(clip =>
                {
                    var take = defaults.SingleOrDefault(d => d.name.EndsWith(clip.name, StringComparison.Ordinal));
                    Require(take != null, "Missing FBX take " + clip.name);
                    return new ModelImporterClipAnimation {
                        name = clip.name, takeName = take.takeName, firstFrame = take.firstFrame, lastFrame = take.lastFrame,
                        loopTime = clip.loop, loopPose = false, lockRootRotation = true, lockRootHeightY = true,
                        lockRootPositionXZ = true, keepOriginalOrientation = true,
                        keepOriginalPositionY = true, keepOriginalPositionXZ = true
                    };
                }).ToArray();
            }
            foreach (var sourceMaterial in audit.material_palette)
            {
                var material = UpsertMaterial(sourceMaterial);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterial.name), material);
            }
            importer.SaveAndReimport();
        }

        private static Material UpsertMaterial(SourceMaterial source)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader unavailable; resolve the Director's packages before building characters");
            var path = OutputRoot + "/Materials/" + source.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = source.name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            var color = source.color;
            bool wing = source.name == "Mosquito_Wing";
            if (wing) color.a = .42f; // Two physical faces; cull back to avoid doubling opacity.
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", wing ? .15f : 1 - source.roughness);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Surface", wing ? 1 : 0);
            // URP's preserve-specular path leaves highlights unattenuated by alpha.
            // Thin membranes use ordinary alpha transmission with no mirror reflection.
            material.SetFloat("_Blend", 0);
            material.SetFloat("_BlendModePreserveSpecular", 0);
            material.SetFloat("_SpecularHighlights", wing ? 0 : 1);
            material.SetFloat("_EnvironmentReflections", wing ? 0 : 1);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", wing ? 0 : 1);
            material.SetFloat("_SrcBlend", (float)(wing ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(wing ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(wing ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ReceiveShadows", wing ? 0 : 1);
            material.SetOverrideTag("RenderType", wing ? "Transparent" : "Opaque");
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", wing);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", wing);
            SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", wing);
            SetKeyword(material, "_RECEIVE_SHADOWS_OFF", wing);
            material.SetShaderPassEnabled("ShadowCaster", !wing);
            material.renderQueue = wing ? (int)RenderQueue.Transparent : -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        private static AnimatorController BuildController(SourceAudit audit, string[] states)
        {
            EnsureFolder(OutputRoot + "/Controllers");
            var path = OutputRoot + "/Controllers/LMS_" + audit.species + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "Motion", type = AnimatorControllerParameterType.Int } };
            var machine = controller.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath(audit.species)).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            for (var id = 0; id < states.Length; id++)
            {
                var name = states[id];
                var clip = clips.SingleOrDefault(c => c.name == audit.species + "_" + name);
                Require(clip != null, "Imported clip missing: " + audit.species + "_" + name);
                var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == name)
                    ?? machine.AddState(name, new Vector3(250, 60 * id));
                state.motion = clip; state.writeDefaultValues = false;
                if (id == 0) machine.defaultState = state;
            }
            // Presentation drives CrossFade explicitly. An always-true Motion=0 transition would
            // incorrectly interrupt externally selected clips and return actors to Idle.
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var child in machine.states.Where(s => !states.Contains(s.state.name)).ToArray()) machine.RemoveState(child.state);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildCharacter(SourceAudit audit, AnimatorController controller, string[] states, bool firstPerson)
        {
            bool human = audit.species == "Human";
            string name = "LMS_" + audit.species + (firstPerson ? "_FirstPerson" : "");
            EnsureFolder(OutputRoot + "/Prefabs");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                var visual = Child(root.transform, "VisualRoot");
                visual.localScale = Vector3.one * (human ? 1 : .5f);
                var instance = InstantiateOrientedModel(visual, audit.species, scene);
                var animator = instance.GetComponent<Animator>();
                Require(animator != null && animator.avatar != null && animator.avatar.isValid, "Invalid Generic avatar: " + audit.species);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                RestoreBindPose(instance.transform);
                var view = root.AddComponent<CharacterView>();
                view.Animator = animator; view.VisualRoot = visual;
                if (human)
                {
                    var collider = root.AddComponent<CapsuleCollider>();
                    collider.radius = .25f; collider.height = 1.72f; collider.center = Vector3.up * .86f;
                    collider.isTrigger = true; collider.enabled = false; view.HitVolume = collider;
                }
                else
                {
                    var collider = root.AddComponent<SphereCollider>();
                    collider.radius = .055f; collider.isTrigger = true; collider.enabled = false; view.HitVolume = collider;
                }
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                view.HeadRenderers = renderers.Where(r => r.name == "HumanHead" || r.name == "HumanNightcap").ToArray();
                var colors = new List<CharacterView.ColorBinding>();
                foreach (var renderer in renderers)
                {
                    bool wing = renderer.name == "MosquitoMembranes" || renderer.name == "MosquitoVeins";
                    renderer.shadowCastingMode = wing ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = !wing;
                    if (renderer is SkinnedMeshRenderer skinned) skinned.quality = SkinQuality.Bone4;
                    for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                    {
                        var material = renderer.sharedMaterials[slot];
                        Require(material != null, "Null material on " + renderer.name);
                        string category = material.name == "Human_Skin" ? "Skin" : material.name == "Human_Pajamas" ? "Pajamas"
                            : (material.name == "Mosquito_Shell" || material.name == "Mosquito_Abdomen") ? "Mosquito" : null;
                        if (category != null) colors.Add(new CharacterView.ColorBinding { Renderer = renderer, MaterialIndex = slot, Category = category });
                    }
                }
                view.Colors = colors.ToArray();
                view.Anchors = MakeAnchors(root.transform, instance.transform, human ? HumanAnchors : MosquitoAnchors);
                view.Motions = states.Select((state, id) => new CharacterView.MotionBinding {
                    Id = id, StateName = "Base Layer." + state, ClipName = audit.species + "_" + state,
                    Loop = audit.clips.Single(c => c.name == audit.species + "_" + state).loop
                }).ToArray();
                view.SetFirstPersonVisibility(firstPerson); view.RefreshAnchors();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
                Require(saved != null, "Could not save prefab " + name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static CharacterView.AnchorBinding[] MakeAnchors(Transform root, Transform model, string[,] names)
        {
            var container = Child(root, "PresentationAnchors");
            var result = new List<CharacterView.AnchorBinding>();
            for (int i = 0; i < names.GetLength(0); i++)
            {
                var bone = Unique(model, names[i, 1]);
                result.Add(new CharacterView.AnchorBinding {
                    Name = names[i, 0], Anchor = Child(container, names[i, 0]), SourceBone = bone,
                    RotationOffset = Quaternion.Inverse(bone.rotation) * root.rotation
                });
            }
            return result.ToArray();
        }

        private static void BuildTool(SourceAudit audit)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("LMS_Flyswatter"); SceneManager.MoveGameObjectToScene(root, scene);
                var model = InstantiateOrientedModel(root.transform, audit.species, scene);
                var tool = root.AddComponent<ToolView>();
                tool.Grip = Child(root.transform, "Grip"); tool.Grip.position = Unique(model.transform, "Socket.Grip").position;
                tool.Impact = Child(root.transform, "Impact"); tool.Impact.position = Unique(model.transform, "Socket.Impact").position;
                tool.GripToImpact = Vector3.Distance(tool.Grip.position, tool.Impact.position);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("LMS_Flyswatter"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static ValidationRecord ValidateCharacter(string name, SourceAudit audit, bool firstPerson)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            Require(prefab != null, "Missing generated prefab " + name);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var view = instance.GetComponent<CharacterView>();
                Require(view != null && view.Animator != null && view.Animator.avatar != null && view.Animator.avatar.isValid, "Invalid rig on " + name);
                RestoreBindPose(view.Animator.transform);
                view.RefreshAnchors();
                Require(!view.Animator.avatar.isHuman && !view.Animator.applyRootMotion, "Expected in-place Generic rig on " + name);
                Require(instance.transform.localScale == Vector3.one, "Actor root has non-unit scale");
                Require(view.VisualRoot.localScale == Vector3.one * (audit.species == "Human" ? 1 : .5f), "Visual scale changed");
                var orientation = ValidateOrientation(instance.transform, view.Animator.transform, audit.species);
                Require(view.IsFirstPerson == firstPerson, "FP visibility flag mismatch");
                Require(view.HitVolume != null && view.HitVolume.isTrigger && !view.HitVolume.enabled, "Missing disabled reference hit volume; Gameplay owns live colliders");
                if (audit.species == "Human")
                {
                    var capsule = view.HitVolume as CapsuleCollider;
                    Require(capsule != null && Mathf.Abs(capsule.radius - .25f) < .0001f && Mathf.Abs(capsule.height - 1.72f) < .0001f, "Human capsule changed");
                    Require(view.HeadRenderers.Length == 2, "Head and cap must be independently hideable");
                    Require(Mathf.Abs(view.GetAnchor("CameraEye").position.y - 1.53f) < .005f, "Camera eye height is not 1.53 m; check FBX axes/units");
                }
                else
                {
                    Require(Mathf.Abs(((SphereCollider)view.HitVolume).radius - .055f) < .0001f, "Mosquito collision radius changed");
                    var mouthLocal = instance.transform.InverseTransformPoint(view.GetAnchor("ProboscisTip").position);
                    Require(Vector3.Distance(mouthLocal, new Vector3(0, 0, .095f)) < .001f, "Mosquito tip does not match Gameplay +Z 0.095 m at rest");
                }
                foreach (var binding in view.Anchors) Require(binding.Anchor != null && binding.SourceBone != null, "Missing anchor on " + name);
                foreach (var bone in audit.bone_names) Unique(view.Animator.transform, bone);
                int triangles = CountTriangles(instance);
                Require(triangles == audit.triangles, "Triangle count changed on " + name + ": " + triangles);
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(renderers.Length == audit.mesh_count, "Renderer count changed on " + name);
                foreach (var renderer in renderers)
                {
                    Require(renderer.sharedMesh != null && renderer.sharedMesh.bindposes.Length == renderer.bones.Length,
                        "Skin bindpose count mismatch on " + renderer.name);
                    Require(renderer.bones.All(b => b != null), "Null skin bone on " + renderer.name);
                    Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit"), "Non-URP material on " + renderer.name);
                }
                var clips = view.Animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                var rootBone = Unique(view.Animator.transform, "Root");
                var rootAtRest = rootBone.localPosition;
                float maxLoopDelta = 0;
                foreach (var sourceClip in audit.clips)
                {
                    var clip = clips.SingleOrDefault(c => c.name == sourceClip.name);
                    Require(clip != null, "Missing controller clip " + sourceClip.name);
                    Require(Mathf.Abs(clip.length - sourceClip.duration_seconds) <= 1.1f / 30, "Clip duration changed: " + clip.name);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        Require(string.IsNullOrEmpty(binding.path) || view.Animator.transform.Find(binding.path) != null, "Unbound animation path " + clip.name + ": " + binding.path);
                    clip.SampleAnimation(view.Animator.gameObject, 0); view.RefreshAnchors();
                    var feet = audit.species == "Human" && sourceClip.loop ? new[] { view.GetAnchor("Foot_L").position, view.GetAnchor("Foot_R").position } : null;
                    foreach (float time in new[] { clip.length * .5f, Mathf.Max(0, clip.length - .0001f) })
                    {
                        clip.SampleAnimation(view.Animator.gameObject, time); view.RefreshAnchors();
                        Require(Vector3.Distance(rootBone.localPosition, rootAtRest) < .001f, "Root translation found in " + clip.name);
                        foreach (var renderer in renderers)
                        {
                            var baked = new Mesh();
                            try { renderer.BakeMesh(baked, true); Require(baked.vertices.All(Finite), "Non-finite sampled skin in " + clip.name); }
                            finally { Object.DestroyImmediate(baked); }
                        }
                    }
                    if (feet != null)
                        maxLoopDelta = Mathf.Max(maxLoopDelta, Vector3.Distance(feet[0], view.GetAnchor("Foot_L").position), Vector3.Distance(feet[1], view.GetAnchor("Foot_R").position));
                }
                Require(maxLoopDelta <= .02f, "Loop foot seam exceeds 2 cm on " + name);
                return new ValidationRecord { prefab = PrefabPath(name), triangles = triangles, renderers = renderers.Length,
                    bones = audit.bones, clips = clips.Length, genericAvatarValid = true, animationBindingsValid = true,
                    sampledMeshesFinite = true, rootStationary = true, maximumLoopFootDelta = maxLoopDelta, motions = view.Motions,
                    orientation = orientation,
                    anchors = view.Anchors.Select(a => new AnchorRecord { name = a.Name,
                        bonePath = AnimationUtility.CalculateTransformPath(a.SourceBone, view.Animator.transform),
                        anchorPath = AnimationUtility.CalculateTransformPath(a.Anchor, instance.transform) }).ToArray(),
                    bonePaths = audit.bone_names.Select(n => AnimationUtility.CalculateTransformPath(Unique(view.Animator.transform, n), view.Animator.transform)).ToArray() };
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static ValidationRecord ValidateTool(SourceAudit audit)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("LMS_Flyswatter"));
            Require(prefab != null, "Missing flyswatter prefab");
            var tool = prefab.GetComponent<ToolView>();
            Require(tool != null && tool.Grip != null && tool.Impact != null && tool.GripToImpact > .35f && tool.GripToImpact < .38f, "Invalid tool grip/impact anchors");
            var triangles = CountTriangles(prefab); Require(triangles == audit.triangles, "Tool triangle count changed");
            Require(prefab.transform.InverseTransformPoint(tool.Impact.position).z > .003f, "Flyswatter striking face is not +Z");
            return new ValidationRecord { prefab = PrefabPath("LMS_Flyswatter"), triangles = triangles,
                renderers = prefab.GetComponentsInChildren<Renderer>(true).Length, bones = audit.bones, clips = 0 };
        }

        private static int CountTriangles(GameObject root)
        {
            var meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)
                .Concat(root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh));
            int total = 0;
            foreach (var mesh in meshes)
            {
                Require(mesh != null, "Null mesh");
                for (int i = 0; i < mesh.subMeshCount; i++) total += (int)mesh.GetIndexCount(i) / 3;
            }
            return total;
        }

        private static GameObject InstantiateOrientedModel(Transform parent, string species, Scene scene)
        {
            // Correct the whole imported asset outside its Animator. Bone local transforms,
            // curves, bindposes and mesh handedness remain untouched; no socket-only offset.
            var orientation = Child(parent, "SourceOrientation");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(species)), scene);
            model.name = "Model"; model.transform.SetParent(orientation, false);
            RestoreBindPose(model.transform);
            string originName = species == "Human" ? "Head" : species == "Mosquito" ? "Thorax" : "Root";
            string frontName = species == "Human" ? "Socket.Eye" : species == "Mosquito" ? "Socket.Mouth" : "Socket.Impact";
            Vector3 forward = parent.InverseTransformDirection(Unique(model.transform, frontName).position - Unique(model.transform, originName).position);
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            Require(forward.sqrMagnitude > 1e-8f, "Cannot infer imported forward from source landmarks: " + species);
            // FromToRotation is ambiguous for opposite vectors and can choose a pitch/roll
            // half-turn. Use an explicit yaw so +Y stays up for the measured -Z import.
            float sourceYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            orientation.localRotation = Quaternion.AngleAxis(-sourceYaw, Vector3.up);
            Require(Vector3.Dot(orientation.up, parent.up) > .9999f, "Source orientation changed the up axis");
            return model;
        }

        private static void RestoreBindPose(Transform model)
        {
            // Unity may initialize an imported Generic hierarchy from the first FBX take.
            // That animated pose is not the skin's bind pose (Blink lowers Hips by 18 mm).
            // Reconstruct the actual bind transforms, never compensate an individual socket.
            var matrices = new Dictionary<Transform, Matrix4x4>();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Require(renderer.sharedMesh != null, "Missing mesh while restoring bind pose: " + renderer.name);
                var poses = renderer.sharedMesh.bindposes;
                Require(poses.Length == renderer.bones.Length, "Bind pose count mismatch: " + renderer.name);
                for (int i = 0; i < poses.Length; i++)
                {
                    var bone = renderer.bones[i];
                    Require(bone != null, "Missing bind bone: " + renderer.name);
                    var matrix = renderer.localToWorldMatrix * poses[i].inverse;
                    if (matrices.TryGetValue(bone, out var previous))
                    {
                        for (int element = 0; element < 16; element++)
                            Require(Mathf.Abs(previous[element] - matrix[element]) < .0001f,
                                "Inconsistent skin bind matrices for " + bone.name);
                    }
                    else matrices.Add(bone, matrix);
                }
            }
            // Set parents before children; socket-only bones retain their local rest offset.
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (!matrices.TryGetValue(bone, out var world)) continue;
                var local = bone.parent == null ? world : bone.parent.worldToLocalMatrix * world;
                bone.localPosition = local.GetColumn(3);
                bone.localRotation = local.rotation;
                bone.localScale = local.lossyScale;
            }
        }

        private static OrientationRecord ValidateOrientation(Transform actor, Transform model, string species)
        {
            bool human = species == "Human";
            var front = actor.InverseTransformPoint(Unique(model, human ? "Socket.Eye" : "Socket.Mouth").position);
            var left = actor.InverseTransformPoint(Unique(model, human ? "UpperArm.L" : "Wing.L").position);
            var right = actor.InverseTransformPoint(Unique(model, human ? "UpperArm.R" : "Wing.R").position);
            Require(front.z > (human ? .10f : .09f), species + " geometry faces -Z; front landmark=" + front);
            Require(left.x < -.001f && right.x > .001f, species + " left/right anatomy is reversed: L=" + left + " R=" + right);
            if (human)
            {
                Require(actor.InverseTransformPoint(Unique(model, "Foot.L").position).x < -.08f
                    && actor.InverseTransformPoint(Unique(model, "Foot.R").position).x > .08f, "Human left/right feet are reversed");
            }
            float nearest = 0;
            if (!human)
            {
                nearest = float.PositiveInfinity;
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var baked = new Mesh();
                    try
                    {
                        // Unity 6000.3: useScale=true compensates the renderer's Transform
                        // scale. TransformPoint then applies VisualRoot's .5 exactly once.
                        // The default false already includes it in the baked vertex data.
                        renderer.BakeMesh(baked, true);
                        foreach (var vertex in baked.vertices)
                            nearest = Mathf.Min(nearest, Vector3.Distance(front, actor.InverseTransformPoint(renderer.transform.TransformPoint(vertex))));
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
                Require(nearest < .002f, "Mosquito forward anchor is detached from the proboscis mesh: " + nearest);
            }
            Require(model.parent.name == "SourceOrientation" && model.parent.localScale == Vector3.one,
                "Source orientation must be a unit-scale parent outside the Animator");
            return new OrientationRecord { frontPointActorLocal = front, leftPointActorLocal = left,
                rightPointActorLocal = right, sourceCorrectionEuler = model.parent.localEulerAngles,
                nearestTipVertexDistance = nearest, bakeMeshScaleCompensated = true };
        }

        private static Transform Unique(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            Require(found.Length == 1, "Expected one bone " + name + ", got " + found.Length);
            return found[0];
        }
        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        private static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.z));
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static void WriteIfChanged(string path, byte[] bytes)
        {
            path = Absolute(path);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
        }
        private static string Absolute(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        private static void WriteReceipt(BuildReceipt receipt)
        {
            WriteIfChanged(ReceiptPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt, true) + "\n"));
            AssetDatabase.ImportAsset(ReceiptPath, ImportAssetOptions.ForceSynchronousImport);
        }
        private static string SourceDigest()
        {
            var paths = new[] { "human", "mosquito", "flyswatter" }.SelectMany(s => new[] {
                Path.Combine(SourceRoot, s, "audit.json"), Path.Combine(SourceRoot, s, "LMS_" + char.ToUpperInvariant(s[0]) + s.Substring(1) + "_alpha.fbx") });
            return Digest(paths);
        }
        private static string GeneratedDigest()
        {
            AssetDatabase.SaveAssets();
            return Digest(Directory.GetFiles(Absolute(OutputRoot), "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal));
        }
        private static string Digest(IEnumerable<string> paths)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = new List<byte>();
                foreach (var path in paths)
                {
                    Require(File.Exists(path), "Hash input missing: " + path);
                    bytes.AddRange(Encoding.UTF8.GetBytes(Path.GetFileName(path)));
                    bytes.AddRange(hash.ComputeHash(File.ReadAllBytes(path)));
                }
                return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
