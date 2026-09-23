using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LetMeSleep.Core.Customization;
using UnityEngine;

namespace LetMeSleep.Content.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterCustomizationHost))]
    // After the cameras (MosquitoFollowCamera 1250, HumanViewCamera 1300): parts mirror the render state those
    // cameras give the authored body in the same frame (see LateUpdate).
    [DefaultExecutionOrder(1400)]
    public sealed class CharacterModularVisualAssembler : MonoBehaviour
    {
        private sealed class PartPlan
        {
            public GameObject Prefab;
            public CharacterCustomizationPart Part;
            public bool IsBase;
        }

        private sealed class ApplicationPlan
        {
            public CharacterView View;
            public CharacterCustomizationHost Host;
            public CustomizationCatalogSnapshot Snapshot;
            public AppearanceSelection Selection;
            public CustomizationRole Role;
            public string Key;
            public readonly List<PartPlan> Parts = new List<PartPlan>();
            public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        }

        private sealed class PreparedApplication
        {
            public readonly List<GameObject> Objects = new List<GameObject>();
            public readonly List<Renderer> Heads = new List<Renderer>();
            public readonly List<CharacterView.ColorBinding> Colors = new List<CharacterView.ColorBinding>();
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        private struct ColorTarget : IEquatable<ColorTarget>
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public bool Equals(ColorTarget other) => Renderer == other.Renderer && MaterialIndex == other.MaterialIndex;
            public override bool Equals(object obj) => obj is ColorTarget other && Equals(other);
            public override int GetHashCode() => ((Renderer != null ? Renderer.GetInstanceID() : 0) * 397) ^ MaterialIndex;
        }

        private readonly List<GameObject> appliedObjects = new List<GameObject>();
        private readonly Dictionary<Renderer, bool> authoredBaseEnabled = new Dictionary<Renderer, bool>();
        private readonly Dictionary<ColorTarget, MaterialPropertyBlock> authoredColorBlocks =
            new Dictionary<ColorTarget, MaterialPropertyBlock>();
        private Renderer[] appliedHeads = Array.Empty<Renderer>();
        private CharacterView.ColorBinding[] appliedColors = Array.Empty<CharacterView.ColorBinding>();
        private Renderer[] appliedRenderers = Array.Empty<Renderer>();
        private CharacterCustomizationHost appliedHost;
        private bool mirroredForceOff;
        private CharacterView appliedView;
        private string appliedKey;
        private bool baseStatesCaptured;
        private bool baseSuppressed;

        /// <summary>True once a modular appearance is on the view (the authored base is hidden).</summary>
        public bool HasAppliedParts => appliedView != null && appliedObjects.Count > 0;

        public bool CanApply(CharacterView view, CharacterCustomizationCatalog catalog,
            CustomizationRole role, out string reason)
        {
            reason = string.Empty;
            CustomizationCatalogSnapshot snapshot = null;
            string[] errors = null;
            if (catalog == null || !catalog.TryCreateSnapshot(out snapshot, out errors))
            {
                reason = errors == null ? "Customization catalog is required." : string.Join("; ", errors);
                return false;
            }
            return CanApply(view, catalog, snapshot.DefaultSelection(), role, out reason);
        }

        public bool CanApply(CharacterView view, CharacterCustomizationCatalog catalog,
            AppearanceSelection selection, CustomizationRole role, out string reason) =>
            TryBuildPlan(view, catalog, selection, role, out _, out reason);

        public bool TryApply(CharacterView view, CharacterCustomizationCatalog catalog,
            AppearanceSelection selection, CustomizationRole role, out string error)
        {
            if (!TryBuildPlan(view, catalog, selection, role, out var plan, out error)) return false;
            if (appliedView == view && appliedKey == plan.Key && appliedObjects.All(item => item != null))
                return true;

            PreparedApplication staged = null;
            try
            {
                staged = Prepare(plan);
                Commit(plan, staged);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (staged != null) DestroyObjects(staged.Objects);
                error = "Modular appearance was not applied: " + exception.Message;
                return false;
            }
        }

        public void ClearAppliedParts()
        {
            if (appliedView != null) appliedView.ClearCustomizationBindings(this);
            DestroyObjects(appliedObjects);
            appliedObjects.Clear();
            RestoreAuthoredBaseRenderers();
            RestoreAuthoredColorBlocks();
            appliedHeads = Array.Empty<Renderer>();
            appliedColors = Array.Empty<CharacterView.ColorBinding>();
            appliedRenderers = Array.Empty<Renderer>();
            appliedHost = null;
            mirroredForceOff = false;
            appliedView = null;
            appliedKey = null;
        }

        private void OnDestroy() => ClearAppliedParts();

        /// <summary>
        /// The parts follow the render state other presentation code gives the authored body they replace: a camera
        /// that hides its own actor while it is inside it (MosquitoFollowCamera sets forceRenderingOff on the
        /// authored renderers it bound before the parts existed) hides the parts too, in the same frame.
        /// </summary>
        private void LateUpdate()
        {
            if (appliedRenderers.Length == 0 || appliedHost == null) return;
            bool hidden = false;
            foreach (var renderer in appliedHost.OwnedBaseRenderers ?? Array.Empty<Renderer>())
                if (renderer != null && renderer.forceRenderingOff) { hidden = true; break; }
            if (hidden == mirroredForceOff) return;
            mirroredForceOff = hidden;
            foreach (var renderer in appliedRenderers) if (renderer != null) renderer.forceRenderingOff = hidden;
        }

        private bool TryBuildPlan(CharacterView view, CharacterCustomizationCatalog catalog,
            AppearanceSelection selection, CustomizationRole role, out ApplicationPlan plan, out string error)
        {
            plan = null;
            if (role != CustomizationRole.Human && role != CustomizationRole.Mosquito)
            { error = "Customization role is invalid."; return false; }
            CustomizationCatalogSnapshot snapshot = null;
            string[] catalogErrors = null;
            if (catalog == null || !catalog.TryCreateSnapshot(out snapshot, out catalogErrors))
            { error = catalogErrors == null ? "Customization catalog is required." : string.Join("; ", catalogErrors); return false; }
            if (!snapshot.RuntimeReady)
            { error = "Customization catalog has no validated base assets for both roles."; return false; }
            if (!snapshot.TryNormalize(selection, out var normalized, out error)) return false;
            if (!TryValidateHost(view, role, out var host, out error)) return false;

            var result = new ApplicationPlan
            {
                View = view,
                Host = host,
                Snapshot = snapshot,
                Selection = normalized,
                Role = role
            };
            int selectedBaseCount = 0;
            foreach (var selected in normalized.For(role).Selections)
            {
                if (!snapshot.TrySlot(selected.SlotId, out var slot) || !slot.TryOption(selected.OptionId, out var option))
                { error = "Normalized appearance references an unavailable option."; return false; }
                if (option.Kind == CustomizationOptionKind.None) continue;
                if (option.Kind == CustomizationOptionKind.Color)
                {
                    result.Colors.Add(slot.SlotId, Unpack(option.SwatchRgba));
                    continue;
                }
                if (!catalog.TryGetAssets(slot.SlotId, option.OptionId, out var runtimeAsset, out _) ||
                    !(runtimeAsset is GameObject prefab))
                { error = "Visual option has no GameObject asset: " + slot.SlotId + "/" + option.OptionId; return false; }
                var part = prefab.GetComponent<CharacterCustomizationPart>();
                if (!TryValidatePart(result, slot, option, prefab, part, out error)) return false;
                result.Parts.Add(new PartPlan { Prefab = prefab, Part = part, IsBase = slot.IsBaseSlot });
                if (slot.IsBaseSlot) selectedBaseCount++;
            }
            if (selectedBaseCount != 1)
            { error = "The selected role must provide exactly one modular base."; return false; }
            if (!TryValidateColorBindings(result, out error)) return false;
            result.Key = BuildKey(snapshot, normalized, role);
            plan = result;
            error = string.Empty;
            return true;
        }

        private bool TryValidateHost(CharacterView view, CustomizationRole role,
            out CharacterCustomizationHost host, out string error)
        {
            host = GetComponent<CharacterCustomizationHost>();
            if (view == null || view.gameObject != gameObject || host == null || host.gameObject != gameObject || host.View != view)
            { error = "CharacterView, assembler and host metadata must belong to the same root."; return false; }
            if (host.Role != role)
            { error = "Customization host role does not match the requested role."; return false; }
            if (string.IsNullOrEmpty(host.RigId) || view.Animator == null || view.VisualRoot == null || host.PartsRoot == null)
            { error = "Customization host rig, animator, VisualRoot and PartsRoot are required."; return false; }
            if (!host.PartsRoot.IsChildOf(view.VisualRoot) || host.PartsRoot == view.VisualRoot ||
                host.PartsRoot.IsChildOf(view.Animator.transform))
            { error = "PartsRoot must be a dedicated child of VisualRoot outside the animated rig."; return false; }
            if (!Approximately(view.VisualRoot.localScale, host.ExpectedVisualScale))
            { error = "Host VisualRoot scale does not match its declared contract."; return false; }
            var owned = host.OwnedBaseRenderers ?? Array.Empty<Renderer>();
            var partsRoot = host.PartsRoot;
            if (owned.Length == 0 || owned.Any(item => item == null || !item.transform.IsChildOf(view.VisualRoot) ||
                    item.transform.IsChildOf(partsRoot)) || owned.Distinct().Count() != owned.Length)
            { error = "Host must explicitly own distinct authored base renderers outside PartsRoot."; return false; }
            foreach (var binding in host.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                if (!TryValidateColorBinding(view.VisualRoot, binding, out error)) return false;
            error = string.Empty;
            return true;
        }

        private static bool TryValidatePart(ApplicationPlan plan, CustomizationSlotSnapshot slot,
            CustomizationOptionSnapshot option, GameObject prefab, CharacterCustomizationPart part, out string error)
        {
            if (part == null || part.gameObject != prefab)
            { error = "Visual prefab requires CharacterCustomizationPart on its root: " + option.OptionId; return false; }
            if (part.Role != plan.Role || part.SlotId != slot.SlotId || part.Kind != option.Kind)
            { error = "Part metadata does not match catalog role, slot and kind: " + option.OptionId; return false; }
            if (part.TargetRigId != plan.Host.RigId || !Approximately(part.TargetVisualScale, plan.Host.ExpectedVisualScale))
            { error = "Part targets a different rig or visual scale: " + option.OptionId; return false; }
            if (!Approximately(prefab.transform.localPosition, Vector3.zero) ||
                Quaternion.Angle(prefab.transform.localRotation, Quaternion.identity) > .01f ||
                !Approximately(prefab.transform.localScale, Vector3.one))
            { error = "Part prefab root must use identity local transform in its target rig space."; return false; }
            if (part.FacialImpact == CharacterFacialImpact.ReplacesTrackedFace)
            { error = "Face replacement needs a future, specific facial-rig certification: " + option.OptionId; return false; }

            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (!(component is Transform) && !(component is Renderer) && !(component is MeshFilter) &&
                    !(component is CharacterCustomizationPart))
                { error = "Part prefab contains a forbidden component: " + component.GetType().Name; return false; }
            if (prefab.GetComponentsInChildren<CharacterCustomizationPart>(true).Length != 1)
            { error = "Part prefab must contain exactly one metadata component."; return false; }

            var skinBindings = part.SkinnedRenderers ?? Array.Empty<CharacterCustomizationPart.SkinnedRendererBinding>();
            var socketBindings = part.SocketParts ?? Array.Empty<CharacterCustomizationPart.SocketBinding>();
            if (option.Kind == CustomizationOptionKind.SkinnedPart && (skinBindings.Length == 0 || socketBindings.Length != 0))
            { error = "SkinnedPart needs skinned bindings and cannot declare socket roots."; return false; }
            if (option.Kind == CustomizationOptionKind.SocketPart && (socketBindings.Length == 0 || skinBindings.Length != 0))
            { error = "SocketPart needs socket bindings and cannot declare skinned bindings."; return false; }
            if (option.Kind == CustomizationOptionKind.Composite && (socketBindings.Length == 0 || skinBindings.Length == 0))
            { error = "Composite needs both skinned and socket bindings."; return false; }
            if (slot.IsBaseSlot && skinBindings.Length == 0)
            { error = "A modular base must provide a skinned renderer."; return false; }

            var declaredRenderers = new HashSet<Renderer>();
            foreach (var binding in skinBindings)
            {
                var renderer = binding?.Renderer;
                if (renderer == null || !renderer.transform.IsChildOf(prefab.transform) ||
                    renderer.gameObject.GetComponents<Renderer>().Length != 1 || !declaredRenderers.Add(renderer))
                { error = "Skinned renderer binding is null, ambiguous or outside its prefab."; return false; }
                if (!HasUniqueSourcePath(prefab.transform, renderer.transform))
                { error = "Skinned renderer source path is ambiguous."; return false; }
                var mesh = renderer.sharedMesh;
                var paths = binding.BonePaths ?? Array.Empty<string>();
                if (mesh == null || mesh.bindposes == null || mesh.bindposes.Length == 0 || mesh.bindposes.Length != paths.Length)
                { error = "Skinned renderer bind poses and explicit bone paths do not match."; return false; }
                if (!TryFindUnique(plan.View.Animator.transform, binding.RootBonePath, out _) ||
                    paths.Any(path => !TryFindUnique(plan.View.Animator.transform, path, out _)))
                { error = "Skinned renderer references a missing or ambiguous target bone."; return false; }
            }

            var socketRoots = new HashSet<Transform>();
            foreach (var binding in socketBindings)
            {
                if (binding == null || binding.PartRoot == null ||
                    (binding.PartRoot != prefab.transform && binding.PartRoot.parent != prefab.transform) ||
                    !socketRoots.Add(binding.PartRoot) || string.IsNullOrEmpty(binding.AnchorName) ||
                    plan.View.GetAnchor(binding.AnchorName) == null)
                { error = "Socket binding needs a unique prefab root/direct child and an existing host anchor."; return false; }
                if (!HasUniqueSourcePath(prefab.transform, binding.PartRoot) ||
                    binding.PartRoot.GetComponentsInChildren<Renderer>(true)
                        .Any(renderer => !HasUniqueSourcePath(prefab.transform, renderer.transform)))
                { error = "Socket source path is ambiguous."; return false; }
                if (skinBindings.Any(item => item.Renderer.transform.IsChildOf(binding.PartRoot)))
                { error = "A skinned binding cannot be nested below a socket root."; return false; }
                foreach (var renderer in binding.PartRoot.GetComponentsInChildren<Renderer>(true)) declaredRenderers.Add(renderer);
            }
            var allRenderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (allRenderers.Any(item => !declaredRenderers.Contains(item)))
            { error = "Every renderer in a modular prefab must be owned by skinned or socket metadata."; return false; }

            var heads = part.FirstPersonHeadRenderers ?? Array.Empty<Renderer>();
            if (heads.Any(item => item == null || !declaredRenderers.Contains(item)) || heads.Distinct().Count() != heads.Length)
            { error = "First-person head renderers must be distinct renderers owned by this part."; return false; }
            if (part.FacialImpact == CharacterFacialImpact.OccludesExistingFace && heads.Length == 0)
            { error = "A face-occluding part must declare first-person head renderers."; return false; }
            foreach (var binding in part.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                if (!TryValidateColorBinding(prefab.transform, binding, out error) || !declaredRenderers.Contains(binding.Renderer))
                { if (string.IsNullOrEmpty(error)) error = "Part color binding renderer is not owned by the part."; return false; }
            var conditional = new HashSet<Renderer>();
            foreach (var binding in part.ConditionalRenderers ?? Array.Empty<CharacterCustomizationPart.ConditionalRendererBinding>())
                if (binding == null || binding.Renderer == null || !declaredRenderers.Contains(binding.Renderer) ||
                    !conditional.Add(binding.Renderer) || !plan.Snapshot.TrySlot(binding.HiddenWhenSlotSelected, out var other) ||
                    other.Role != plan.Role || other.SlotId == slot.SlotId)
                { error = "Conditional renderer needs an owned renderer and another slot of the same role: " + option.OptionId; return false; }
            error = string.Empty;
            return true;
        }

        private static bool TryValidateColorBindings(ApplicationPlan plan, out string error)
        {
            var boundSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in plan.Host.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                if (!TryValidateSelectedColor(plan, binding, boundSlots, out error)) return false;
            foreach (var part in plan.Parts)
                foreach (var binding in part.Part.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                    if (!TryValidateSelectedColor(plan, binding, boundSlots, out error)) return false;
            // A selected colour whose consumer is not worn (hair colour under no visible hair, pajama colour with
            // jeans, marking colour without markings) stays in the selection and simply has nothing to tint.
            error = string.Empty;
            return true;
        }

        private static bool TryValidateSelectedColor(ApplicationPlan plan,
            CharacterCustomizationPart.ColorChannelBinding binding, HashSet<string> boundSlots, out string error)
        {
            if (!plan.Snapshot.TrySlot(binding.ColorSlotId, out var slot) || slot.Role != plan.Role ||
                !plan.Colors.ContainsKey(slot.SlotId))
            { error = "Color binding must reference a selected Color slot for this role."; return false; }
            boundSlots.Add(slot.SlotId);
            error = string.Empty;
            return true;
        }

        private static bool TryValidateColorBinding(Transform root,
            CharacterCustomizationPart.ColorChannelBinding binding, out string error)
        {
            if (binding == null || binding.Renderer == null || !binding.Renderer.transform.IsChildOf(root) ||
                string.IsNullOrEmpty(binding.ColorSlotId))
            { error = "Color channel needs a renderer under its declared root and a Color slot ID."; return false; }
            int materialCount = binding.Renderer.sharedMaterials == null ? 0 : binding.Renderer.sharedMaterials.Length;
            if (binding.MaterialIndex < 0 || binding.MaterialIndex >= materialCount)
            { error = "Color channel material index is outside the renderer material array."; return false; }
            error = string.Empty;
            return true;
        }

        private PreparedApplication Prepare(ApplicationPlan plan)
        {
            var prepared = new PreparedApplication();
            try
            {
                var stagingRoot = new GameObject("CustomizationStaging");
                stagingRoot.SetActive(false);
                stagingRoot.transform.SetParent(plan.Host.PartsRoot, false);
                stagingRoot.layer = plan.View.gameObject.layer;
                prepared.Objects.Add(stagingRoot);
                foreach (var source in plan.Host.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                    prepared.Colors.Add(new CharacterView.ColorBinding
                    { Renderer = source.Renderer, MaterialIndex = source.MaterialIndex, Category = source.ColorSlotId,
                      Shade = source.Shade, Alpha = source.Alpha });

                foreach (var item in plan.Parts)
                {
                    var clone = Instantiate(item.Prefab, stagingRoot.transform, false);
                    clone.name = "Customization_" + item.Part.SlotId;
                    SetLayerRecursively(clone.transform, plan.View.gameObject.layer);
                    // StagingRoot keeps it hidden; activeSelf must be true so the atomic parent activation reveals it.
                    clone.SetActive(true);

                    var rendererMap = new Dictionary<Renderer, Renderer>();
                    foreach (var skin in item.Part.SkinnedRenderers ?? Array.Empty<CharacterCustomizationPart.SkinnedRendererBinding>())
                    {
                        var cloneRenderer = FindCloneComponent<SkinnedMeshRenderer>(item.Prefab.transform, clone.transform,
                            skin.Renderer.transform);
                        cloneRenderer.rootBone = FindUnique(plan.View.Animator.transform, skin.RootBonePath);
                        cloneRenderer.bones = skin.BonePaths.Select(path => FindUnique(plan.View.Animator.transform, path)).ToArray();
                        rendererMap.Add(skin.Renderer, cloneRenderer);
                    }
                    foreach (var socket in item.Part.SocketParts ?? Array.Empty<CharacterCustomizationPart.SocketBinding>())
                    {
                        FindCloneTransform(item.Prefab.transform, clone.transform, socket.PartRoot);
                        foreach (var sourceRenderer in socket.PartRoot.GetComponentsInChildren<Renderer>(true))
                            rendererMap[sourceRenderer] = FindCloneComponent<Renderer>(item.Prefab.transform, clone.transform,
                                sourceRenderer.transform);
                    }
                    foreach (var head in item.Part.FirstPersonHeadRenderers ?? Array.Empty<Renderer>())
                        prepared.Heads.Add(rendererMap[head]);
                    foreach (var color in item.Part.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
                        prepared.Colors.Add(new CharacterView.ColorBinding
                        { Renderer = rendererMap[color.Renderer], MaterialIndex = color.MaterialIndex, Category = color.ColorSlotId,
                          Shade = color.Shade, Alpha = color.Alpha });
                    prepared.Renderers.AddRange(rendererMap.Values);
                    foreach (var conditional in item.Part.ConditionalRenderers ?? Array.Empty<CharacterCustomizationPart.ConditionalRendererBinding>())
                        if (IsWorn(plan, conditional.HiddenWhenSlotSelected)) prepared.Hidden.Add(rendererMap[conditional.Renderer]);

                    foreach (var socket in item.Part.SocketParts ?? Array.Empty<CharacterCustomizationPart.SocketBinding>())
                    {
                        var cloneRoot = FindCloneTransform(item.Prefab.transform, clone.transform, socket.PartRoot);
                        cloneRoot.gameObject.SetActive(false);
                        cloneRoot.SetParent(plan.View.GetAnchor(socket.AnchorName), false);
                        if (!prepared.Objects.Contains(cloneRoot.gameObject))
                            prepared.Objects.Add(cloneRoot.gameObject);
                    }
                }
                return prepared;
            }
            catch
            {
                DestroyObjects(prepared.Objects);
                throw;
            }
        }

        private void Commit(ApplicationPlan plan, PreparedApplication staged)
        {
            var previousObjects = appliedObjects.ToArray();
            var previousHeads = appliedHeads;
            var previousColors = appliedColors;
            var previousView = appliedView;
            bool previousSuppressed = baseSuppressed;
            bool capturedThisAttempt = !baseStatesCaptured;
            var attemptBlocks = CaptureColorBlocks(staged.Colors);
            var authoredBlockKeysAdded = CaptureAuthoredColorBlocks(plan.Host);
            if (capturedThisAttempt) CaptureAuthoredBaseStates(plan.Host);
            try
            {
                if (previousView != null && previousView != plan.View)
                    previousView.ClearCustomizationBindings(this);
                plan.View.SetCustomizationBindings(this, staged.Heads.ToArray(), staged.Colors.ToArray());
                foreach (var color in plan.Colors) plan.View.ApplyColor(color.Key, color.Value);
                foreach (var renderer in plan.Host.OwnedBaseRenderers) if (renderer != null) renderer.enabled = false;
                baseSuppressed = true;
                var reference = plan.Host.OwnedBaseRenderers.FirstOrDefault(item => item != null);
                bool keepHidden = mirroredForceOff && previousView == plan.View;
                foreach (var renderer in staged.Renderers)
                {
                    if (renderer == null) continue;
                    // Rendering layers given to the actor before the parts existed (a map's rim light) carry over.
                    if (reference != null) renderer.renderingLayerMask |= reference.renderingLayerMask;
                    renderer.forceRenderingOff = keepHidden;
                }
                foreach (var renderer in staged.Hidden) if (renderer != null) renderer.enabled = false;
                foreach (var item in staged.Objects) if (item != null) item.SetActive(true);
            }
            catch
            {
                RestoreColorBlocks(attemptBlocks);
                if (previousView != null)
                    previousView.SetCustomizationBindings(this, previousHeads, previousColors);
                else plan.View.ClearCustomizationBindings(this);
                if (previousSuppressed)
                    foreach (var renderer in plan.Host.OwnedBaseRenderers) if (renderer != null) renderer.enabled = false;
                else RestoreAuthoredBaseRenderers();
                baseSuppressed = previousSuppressed;
                if (capturedThisAttempt && !previousSuppressed)
                { authoredBaseEnabled.Clear(); baseStatesCaptured = false; }
                if (previousView == null)
                    foreach (var key in authoredBlockKeysAdded) authoredColorBlocks.Remove(key);
                throw;
            }

            foreach (var old in previousObjects) if (old != null) old.SetActive(false);
            DestroyObjects(previousObjects);
            appliedObjects.Clear();
            appliedObjects.AddRange(staged.Objects);
            appliedHeads = staged.Heads.ToArray();
            appliedColors = staged.Colors.ToArray();
            appliedRenderers = staged.Renderers.Where(item => item != null).Distinct().ToArray();
            appliedHost = plan.Host;
            if (previousView != plan.View) mirroredForceOff = false;
            appliedView = plan.View;
            appliedKey = plan.Key;
        }

        private static bool IsWorn(ApplicationPlan plan, string slotId)
        {
            string optionId = plan.Selection.For(plan.Role).OptionFor(slotId);
            return plan.Snapshot.TrySlot(slotId, out var slot) && slot.TryOption(optionId ?? string.Empty, out var option) &&
                   option.Kind != CustomizationOptionKind.None;
        }

        private void CaptureAuthoredBaseStates(CharacterCustomizationHost host)
        {
            authoredBaseEnabled.Clear();
            foreach (var renderer in host.OwnedBaseRenderers)
                if (renderer != null) authoredBaseEnabled[renderer] = renderer.enabled;
            baseStatesCaptured = true;
        }

        private void RestoreAuthoredBaseRenderers()
        {
            if (baseStatesCaptured)
                foreach (var pair in authoredBaseEnabled) if (pair.Key != null) pair.Key.enabled = pair.Value;
            authoredBaseEnabled.Clear();
            baseStatesCaptured = false;
            baseSuppressed = false;
        }

        private List<ColorTarget> CaptureAuthoredColorBlocks(CharacterCustomizationHost host)
        {
            var added = new List<ColorTarget>();
            foreach (var binding in host.ColorChannels ?? Array.Empty<CharacterCustomizationPart.ColorChannelBinding>())
            {
                var key = new ColorTarget { Renderer = binding.Renderer, MaterialIndex = binding.MaterialIndex };
                if (authoredColorBlocks.ContainsKey(key)) continue;
                authoredColorBlocks.Add(key, ReadColorBlock(key));
                added.Add(key);
            }
            return added;
        }

        private static Dictionary<ColorTarget, MaterialPropertyBlock> CaptureColorBlocks(
            IEnumerable<CharacterView.ColorBinding> bindings)
        {
            var result = new Dictionary<ColorTarget, MaterialPropertyBlock>();
            foreach (var binding in bindings)
            {
                var key = new ColorTarget { Renderer = binding.Renderer, MaterialIndex = binding.MaterialIndex };
                if (!result.ContainsKey(key)) result.Add(key, ReadColorBlock(key));
            }
            return result;
        }

        private static MaterialPropertyBlock ReadColorBlock(ColorTarget key)
        {
            var block = new MaterialPropertyBlock();
            if (key.Renderer != null) key.Renderer.GetPropertyBlock(block, key.MaterialIndex);
            return block;
        }

        private static void RestoreColorBlocks(IEnumerable<KeyValuePair<ColorTarget, MaterialPropertyBlock>> source)
        {
            foreach (var pair in source)
                if (pair.Key.Renderer != null)
                    pair.Key.Renderer.SetPropertyBlock(pair.Value, pair.Key.MaterialIndex);
        }

        private void RestoreAuthoredColorBlocks()
        {
            RestoreColorBlocks(authoredColorBlocks);
            authoredColorBlocks.Clear();
        }

        private static string BuildKey(CustomizationCatalogSnapshot snapshot,
            AppearanceSelection selection, CustomizationRole role)
        {
            var text = new StringBuilder(snapshot.Fingerprint).Append('|').Append((byte)role);
            foreach (var item in selection.For(role).Selections.OrderBy(value => value.SlotId, StringComparer.Ordinal))
                text.Append('|').Append(item.SlotId).Append('=').Append(item.OptionId);
            return text.ToString();
        }

        private static Color Unpack(uint value) => new Color32((byte)(value >> 24), (byte)(value >> 16),
            (byte)(value >> 8), (byte)value);

        private static bool Approximately(Vector3 left, Vector3 right) =>
            Mathf.Abs(left.x - right.x) <= .0001f && Mathf.Abs(left.y - right.y) <= .0001f &&
            Mathf.Abs(left.z - right.z) <= .0001f;

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        private static bool TryFindUnique(Transform root, string path, out Transform result)
        {
            result = root;
            if (root == null) return false;
            if (string.IsNullOrEmpty(path)) return true;
            foreach (var segment in path.Split('/'))
            {
                if (string.IsNullOrEmpty(segment)) return false;
                Transform match = null;
                int count = 0;
                for (int i = 0; i < result.childCount; i++)
                    if (result.GetChild(i).name == segment) { match = result.GetChild(i); count++; }
                if (count != 1) { result = null; return false; }
                result = match;
            }
            return true;
        }

        private static Transform FindUnique(Transform root, string path)
        {
            if (!TryFindUnique(root, path, out var result))
                throw new InvalidOperationException("Target transform path became unavailable: " + path);
            return result;
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (root == target) return string.Empty;
            var segments = new Stack<string>();
            for (var current = target; current != null && current != root; current = current.parent)
                segments.Push(current.name);
            if (target == null || !target.IsChildOf(root)) throw new InvalidOperationException("Prefab binding left its root.");
            return string.Join("/", segments.ToArray());
        }

        private static bool HasUniqueSourcePath(Transform root, Transform target)
        {
            string path = RelativePath(root, target);
            return TryFindUnique(root, path, out var resolved) && resolved == target;
        }

        private static Transform FindCloneTransform(Transform sourceRoot, Transform cloneRoot, Transform source)
        {
            string path = RelativePath(sourceRoot, source);
            if (!TryFindUnique(cloneRoot, path, out var result))
                throw new InvalidOperationException("Cloned part path is missing or ambiguous: " + path);
            return result;
        }

        private static T FindCloneComponent<T>(Transform sourceRoot, Transform cloneRoot, Transform source)
            where T : Component
        {
            var target = FindCloneTransform(sourceRoot, cloneRoot, source);
            var components = target.GetComponents<T>();
            if (components.Length != 1) throw new InvalidOperationException("Cloned component path is ambiguous: " + target.name);
            return components[0];
        }

        private static void DestroyObjects(IEnumerable<GameObject> objects)
        {
            foreach (var item in objects.Where(value => value != null).Distinct())
            {
                item.SetActive(false);
                if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
            }
        }
    }
}
