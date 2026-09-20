using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class CharacterModularVisualAssemblerPlayModeTests
    {
        private readonly List<Object> owned = new List<Object>();
        private GameObject actor;
        private CharacterView view;
        private CharacterCustomizationHost host;
        private CharacterModularVisualAssembler assembler;
        private Transform rigRoot;
        private Transform rigBone;
        private Transform socket;
        private Transform partsRoot;
        private MeshRenderer authoredBody;
        private MeshRenderer authoredHead;
        private Material material;
        private CharacterCustomizationCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            material = Own(new Material(FindShader()));
            material.name = "Synthetic material";
            actor = Own(new GameObject("Synthetic customization host"));
            view = actor.AddComponent<CharacterView>();
            host = actor.AddComponent<CharacterCustomizationHost>();
            assembler = actor.AddComponent<CharacterModularVisualAssembler>();
            view.HitVolume = actor.AddComponent<BoxCollider>();

            var visual = new GameObject("VisualRoot").transform;
            visual.SetParent(actor.transform, false);
            view.VisualRoot = visual;
            var rig = new GameObject("Rig");
            rig.transform.SetParent(visual, false);
            view.Animator = rig.AddComponent<Animator>();
            rigRoot = new GameObject("Root").transform;
            rigRoot.SetParent(rig.transform, false);
            rigBone = new GameObject("Bone").transform;
            rigBone.SetParent(rigRoot, false);
            partsRoot = new GameObject("CustomizationParts").transform;
            partsRoot.SetParent(visual, false);
            socket = new GameObject("HatSocket").transform;
            socket.SetParent(actor.transform, false);
            socket.localPosition = new Vector3(.25f, 1.5f, .1f);
            view.Anchors = new[] { new CharacterView.AnchorBinding { Name = "HatSocket", Anchor = socket } };

            authoredBody = MeshRenderer("Authored body", visual, true);
            authoredHead = MeshRenderer("Authored disabled head", visual, false);
            view.HeadRenderers = new Renderer[] { authoredHead };
            host.Role = CustomizationRole.Human;
            host.View = view;
            host.RigId = "synthetic.human.rig";
            host.ExpectedVisualScale = Vector3.one;
            host.PartsRoot = partsRoot;
            host.OwnedBaseRenderers = new Renderer[] { authoredBody, authoredHead };
            host.ColorChannels = new[] { new CharacterCustomizationPart.ColorChannelBinding
                { Renderer = authoredBody, MaterialIndex = 0, ColorSlotId = "human.tint" } };
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            actor = null;
            catalog = null;
        }

        [UnityTest]
        public IEnumerator SkinnedBaseSwapRemapsBonesUsesMpbAndRestoresOnlyOwnedState()
        {
            var humanBase = SkinnedPart("human.base", CustomizationOptionKind.SkinnedPart, true, true);
            catalog = Catalog(humanBase);
            var animator = view.Animator;
            var anchor = socket;
            var hitVolume = view.HitVolume;
            var visualScale = view.VisualRoot.localScale;
            var sharedMaterial = material;

            view.SetFirstPersonVisibility(true);
            Assert.That(authoredHead.enabled, Is.False, "first-person mode must not reactivate authored disabled renderers");
            Assert.That(assembler.CanApply(view, catalog, CustomizationRole.Human, out var reason), Is.True, reason);
            Assert.That(assembler.TryApply(view, catalog, DefaultSelection(), CustomizationRole.Human, out var error),
                Is.True, error);

            var installed = partsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Assert.That(installed.rootBone, Is.SameAs(rigRoot));
            Assert.That(installed.bones, Is.EqualTo(new[] { rigBone }));
            Assert.That(installed.enabled, Is.True);
            Assert.That(installed.gameObject.activeInHierarchy, Is.True);
            Assert.That(installed.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
            Assert.That(authoredBody.enabled, Is.False);
            Assert.That(authoredHead.enabled, Is.False);
            Assert.That(installed.sharedMaterial, Is.SameAs(sharedMaterial));
            var block = new MaterialPropertyBlock();
            installed.GetPropertyBlock(block, 0);
            Assert.That(block.GetColor(Shader.PropertyToID("_BaseColor")),
                Is.EqualTo((Color)new Color32(220, 40, 30, 255)).Using(ColorComparer));
            Assert.That(view.Animator, Is.SameAs(animator));
            Assert.That(view.HitVolume, Is.SameAs(hitVolume));
            Assert.That(view.GetAnchor("HatSocket"), Is.SameAs(anchor));
            Assert.That(view.VisualRoot.localScale, Is.EqualTo(visualScale));

            int objectCount = ActiveOwnedObjects();
            Assert.That(assembler.TryApply(view, catalog, DefaultSelection(), CustomizationRole.Human, out error), Is.True, error);
            Assert.That(ActiveOwnedObjects(), Is.EqualTo(objectCount), "same selection must not duplicate parts");

            assembler.ClearAppliedParts();
            Assert.That(authoredBody.enabled, Is.True);
            Assert.That(authoredHead.enabled, Is.False);
            Assert.That(view.HeadRenderers, Is.EqualTo(new Renderer[] { authoredHead }));
            var restoredBlock = new MaterialPropertyBlock();
            authoredBody.GetPropertyBlock(restoredBlock, 0);
            Assert.That(restoredBlock.isEmpty, Is.True, "Clear must restore the authored host property block");
            yield return null;
            Assert.That(ActiveOwnedObjects(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SocketPartAttachesToDeclaredAnchorAndJoinsFirstPersonSet()
        {
            var humanBase = SkinnedPart("human.base", CustomizationOptionKind.SkinnedPart, false, true);
            var hat = SocketPart("human.hat", CustomizationOptionKind.SocketPart);
            catalog = Catalog(humanBase, hat);
            var selection = DefaultSelection();
            selection.Human.SetOption("human.hat", "hat-a");
            view.SetFirstPersonVisibility(true);

            Assert.That(assembler.TryApply(view, catalog, selection, CustomizationRole.Human, out var error), Is.True, error);
            var installed = socket.GetComponentsInChildren<MeshRenderer>(true).Single();
            Assert.That(installed.transform.parent, Is.SameAs(socket));
            Assert.That(installed.transform.localPosition, Is.EqualTo(new Vector3(.1f, .2f, .3f)));
            Assert.That(view.HeadRenderers, Does.Contain(installed));
            Assert.That(installed.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));

            assembler.ClearAppliedParts();
            yield return null;
            Assert.That(socket.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
        }

        [Test]
        public void CompositeBaseSupportsSkinAndSocketWithoutReplacingHostRig()
        {
            var composite = CompositePart("human.base");
            var sourceRenderers = composite.GetComponentsInChildren<Renderer>(true);
            Assert.That(sourceRenderers.All(item => item.gameObject.layer == 0), Is.True);
            actor.layer = 30;
            catalog = Catalog(composite, baseKind: CustomizationOptionKind.Composite);
            Assert.That(assembler.TryApply(view, catalog, DefaultSelection(), CustomizationRole.Human, out var error),
                Is.True, error);
            var installed = partsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Assert.That(installed.bones.Single(), Is.SameAs(rigBone));
            Assert.That(installed.gameObject.activeInHierarchy, Is.True);
            Assert.That(socket.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(1));
            Assert.That(actor.GetComponentsInChildren<Renderer>(true)
                .Where(item => item != authoredBody && item != authoredHead)
                .All(item => item.gameObject.layer == 30), Is.True);
            Assert.That(sourceRenderers.All(item => item.gameObject.layer == 0), Is.True,
                "applying a preview layer must not mutate the source prefab");
            Assert.That(view.Animator.transform, Is.SameAs(rigRoot.parent));
        }

        [UnityTest]
        public IEnumerator InvalidReplacementRollsBackWithoutLeaksOrBodyOverlay()
        {
            var valid = SkinnedPart("human.base", CustomizationOptionKind.SkinnedPart, false, true);
            var invalid = AmbiguousCompositePart("human.base");
            catalog = Catalog(valid, alternateBase: invalid);
            Assert.That(assembler.TryApply(view, catalog, DefaultSelection(), CustomizationRole.Human, out var firstError),
                Is.True, firstError);
            var installed = partsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            int before = ActiveOwnedObjects();
            int childrenBefore = partsRoot.childCount;
            var invalidSelection = DefaultSelection();
            invalidSelection.Human.SetOption("human.base", "base-bad");

            Assert.That(assembler.CanApply(view, catalog, invalidSelection, CustomizationRole.Human,
                out var canApplyError), Is.False);
            Assert.That(canApplyError, Does.Contain("ambiguous"));
            Assert.That(assembler.TryApply(view, catalog, invalidSelection, CustomizationRole.Human, out var error), Is.False);
            Assert.That(error, Does.Contain("ambiguous"));
            Assert.That(installed, Is.Not.Null);
            Assert.That(installed.gameObject.activeInHierarchy, Is.True);
            Assert.That(authoredBody.enabled, Is.False);
            Assert.That(ActiveOwnedObjects(), Is.EqualTo(before));
            yield return null;
            Assert.That(partsRoot.childCount, Is.EqualTo(childrenBefore), "failed Prepare must destroy its hidden clone");
        }

        [Test]
        public void ForbiddenPrefabComponentsAndFaceReplacementFailBeforeHostMutation()
        {
            var invalid = SkinnedPart("human.base", CustomizationOptionKind.SkinnedPart, false, true);
            invalid.AddComponent<BoxCollider>();
            catalog = Catalog(invalid);
            Assert.That(assembler.CanApply(view, catalog, CustomizationRole.Human, out var componentError), Is.False);
            Assert.That(componentError, Does.Contain("forbidden component"));
            Assert.That(authoredBody.enabled, Is.True);

            Object.DestroyImmediate(invalid.GetComponent<BoxCollider>());
            invalid.GetComponent<CharacterCustomizationPart>().FacialImpact = CharacterFacialImpact.ReplacesTrackedFace;
            Assert.That(assembler.CanApply(view, catalog, CustomizationRole.Human, out var facialError), Is.False);
            Assert.That(facialError, Does.Contain("facial-rig certification"));
            Assert.That(authoredBody.enabled, Is.True);
            Assert.That(ActiveOwnedObjects(), Is.Zero);
        }

        [Test]
        public void MissingHostMetadataKeepsProductionGateUnavailable()
        {
            var humanBase = SkinnedPart("human.base", CustomizationOptionKind.SkinnedPart, false, true);
            catalog = Catalog(humanBase);
            host.RigId = string.Empty;
            Assert.That(assembler.CanApply(view, catalog, DefaultSelection(), CustomizationRole.Human, out var reason), Is.False);
            Assert.That(reason, Does.Contain("rig"));
            Assert.That(authoredBody.enabled, Is.True);
        }

        private CharacterCustomizationCatalog Catalog(GameObject humanBase, GameObject optional = null,
            CustomizationOptionKind baseKind = CustomizationOptionKind.SkinnedPart, GameObject alternateBase = null)
        {
            var mosquitoBase = Own(new GameObject("Synthetic mosquito data asset"));
            var result = Own(ScriptableObject.CreateInstance<CharacterCustomizationCatalog>());
            result.CatalogId = "lms.v020.synthetic.visual";
            result.Revision = 1;
            result.Slots = new[]
            {
                Slot(CustomizationRole.Human, "human.base", 1, true, false, true, "base-a"),
                Slot(CustomizationRole.Human, "human.hat", 2, false, true, false, "none"),
                Slot(CustomizationRole.Human, "human.tint", 3, false, false, false, "red"),
                Slot(CustomizationRole.Mosquito, "mosquito.base", 4, true, false, true, "mosquito-a")
            };
            var options = new List<CharacterCustomizationCatalog.OptionDefinition>
            {
                Visual(CustomizationRole.Human, "human.base", "base-a", 1, baseKind, humanBase),
                new CharacterCustomizationCatalog.OptionDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.hat", OptionId = "none", WireOptionId = 0, Kind = CustomizationOptionKind.None },
                new CharacterCustomizationCatalog.OptionDefinition { Role = CustomizationRole.Human,
                    SlotId = "human.tint", OptionId = "red", WireOptionId = 1, Kind = CustomizationOptionKind.Color,
                    HasSwatch = true, Swatch = new Color32(220, 40, 30, 255) },
                Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", 1,
                    CustomizationOptionKind.SkinnedPart, mosquitoBase)
            };
            if (optional != null)
                options.Add(Visual(CustomizationRole.Human, "human.hat", "hat-a", 1,
                    optional.GetComponent<CharacterCustomizationPart>().Kind, optional));
            if (alternateBase != null)
                options.Add(Visual(CustomizationRole.Human, "human.base", "base-bad", 2,
                    alternateBase.GetComponent<CharacterCustomizationPart>().Kind, alternateBase));
            result.Options = options.ToArray();
            return result;
        }

        private GameObject SkinnedPart(string slotId, CustomizationOptionKind kind, bool firstPersonHead, bool color)
        {
            var root = Own(new GameObject("Synthetic skinned " + slotId));
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = Mesh();
            renderer.sharedMaterials = new[] { material };
            var sourceRoot = new GameObject("SyntheticSourceRoot").transform;
            sourceRoot.SetParent(root.transform, false);
            var sourceBone = new GameObject("SyntheticSourceBone").transform;
            sourceBone.SetParent(sourceRoot, false);
            renderer.rootBone = sourceRoot;
            renderer.bones = new[] { sourceBone };
            var metadata = root.AddComponent<CharacterCustomizationPart>();
            metadata.Role = CustomizationRole.Human;
            metadata.SlotId = slotId;
            metadata.Kind = kind;
            metadata.TargetRigId = host.RigId;
            metadata.TargetVisualScale = Vector3.one;
            metadata.SkinnedRenderers = new[] { new CharacterCustomizationPart.SkinnedRendererBinding
                { Renderer = renderer, RootBonePath = "Root", BonePaths = new[] { "Root/Bone" } } };
            metadata.FirstPersonHeadRenderers = firstPersonHead ? new Renderer[] { renderer } : Array.Empty<Renderer>();
            metadata.ColorChannels = color ? new[] { new CharacterCustomizationPart.ColorChannelBinding
                { Renderer = renderer, MaterialIndex = 0, ColorSlotId = "human.tint" } } : Array.Empty<CharacterCustomizationPart.ColorChannelBinding>();
            root.SetActive(false);
            return root;
        }

        private GameObject SocketPart(string slotId, CustomizationOptionKind kind)
        {
            var root = Own(new GameObject("Synthetic socket " + slotId));
            var visual = new GameObject("HatVisual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(.1f, .2f, .3f);
            visual.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            var metadata = root.AddComponent<CharacterCustomizationPart>();
            metadata.Role = CustomizationRole.Human;
            metadata.SlotId = slotId;
            metadata.Kind = kind;
            metadata.TargetRigId = host.RigId;
            metadata.TargetVisualScale = Vector3.one;
            metadata.SocketParts = new[] { new CharacterCustomizationPart.SocketBinding
                { PartRoot = visual.transform, AnchorName = "HatSocket" } };
            metadata.FirstPersonHeadRenderers = new Renderer[] { renderer };
            metadata.FacialImpact = CharacterFacialImpact.OccludesExistingFace;
            root.SetActive(false);
            return root;
        }

        private GameObject CompositePart(string slotId)
        {
            var root = SkinnedPart(slotId, CustomizationOptionKind.Composite, false, true);
            var visual = new GameObject("CompositeSocket");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            var metadata = root.GetComponent<CharacterCustomizationPart>();
            metadata.SocketParts = new[] { new CharacterCustomizationPart.SocketBinding
                { PartRoot = visual.transform, AnchorName = "HatSocket" } };
            return root;
        }

        private GameObject AmbiguousCompositePart(string slotId)
        {
            var root = SkinnedPart(slotId, CustomizationOptionKind.Composite, false, true);
            var unused = new GameObject("Duplicate");
            unused.transform.SetParent(root.transform, false);
            var socketVisual = new GameObject("Duplicate");
            socketVisual.transform.SetParent(root.transform, false);
            socketVisual.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var renderer = socketVisual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            root.GetComponent<CharacterCustomizationPart>().SocketParts = new[]
            {
                new CharacterCustomizationPart.SocketBinding { PartRoot = socketVisual.transform, AnchorName = "HatSocket" }
            };
            return root;
        }

        private MeshRenderer MeshRenderer(string name, Transform parent, bool enabled)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            owner.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            renderer.enabled = enabled;
            return renderer;
        }

        private Mesh Mesh()
        {
            var mesh = Own(new Mesh { name = "Synthetic modular mesh" });
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bindposes = new[] { Matrix4x4.identity };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 }
            };
            return mesh;
        }

        private AppearanceSelection DefaultSelection()
        {
            Assert.That(catalog.TryCreateSnapshot(out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            return snapshot.DefaultSelection();
        }

        private int ActiveOwnedObjects() => actor.GetComponentsInChildren<CharacterCustomizationPart>(true)
            .Count(item => item.gameObject != actor && item.gameObject.activeInHierarchy);

        private static CharacterCustomizationCatalog.SlotDefinition Slot(CustomizationRole role, string id,
            int wire, bool required, bool none, bool isBase, string defaultOption) =>
            new CharacterCustomizationCatalog.SlotDefinition { Role = role, SlotId = id, Label = id,
                WireSlotId = wire, Required = required, AllowsNone = none, IsBaseSlot = isBase,
                DefaultOptionId = defaultOption };

        private static CharacterCustomizationCatalog.OptionDefinition Visual(CustomizationRole role, string slot,
            string id, int wire, CustomizationOptionKind kind, GameObject asset) =>
            new CharacterCustomizationCatalog.OptionDefinition { Role = role, SlotId = slot, OptionId = id,
                Label = id, WireOptionId = wire, Kind = kind, AssetId = "synthetic-" + id, RuntimeAsset = asset };

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        private static Shader FindShader() => Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader");

        private static readonly IEqualityComparer<Color> ColorComparer = new ApproximateColorComparer();

        private sealed class ApproximateColorComparer : IEqualityComparer<Color>
        {
            public bool Equals(Color x, Color y) => Mathf.Abs(x.r - y.r) < .01f && Mathf.Abs(x.g - y.g) < .01f &&
                Mathf.Abs(x.b - y.b) < .01f && Mathf.Abs(x.a - y.a) < .01f;
            public int GetHashCode(Color obj) => 0;
        }
    }
}
