# Campamento water and optional runtime batching

Scope: preserved `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/SourceFreeDraft` only. This is Free draft evidence, not acceptance of the active Astra revision. No Blender, bridge, Unity process, scene/prefab edit or performance measurement was used.

## Water evidence

The preserved FBX v7400 has BlendShape/BlendShapeChannel deformers and no Skin/Cluster. Its six water objects have Wave1/Wave2; Foam_Lake_01 and Foam_Lake_02 each have only Wave1. The GLB agrees (841 nodes, 534 meshes). GLB POSITION counts are 384/192/192 for Creek/Deep/Shallow and 272/136/136 for Lake/Deep/Shallow; each foam has 7. These are export entries, not measured Unity vertex counts.

The folder contains no clean `_UNITY.fbx`. Boneless blendshapes do not prove Unity will return MeshFilter. Actual renderer class/readability/submeshes still require the final stock clean export and native import. If it yields a readable MeshFilter, the existing private CPU mesh path remains usable within the 20,000 vertex limit. If it yields SkinnedMeshRenderer, the importer now serializes one explicit complete pair: Wave_A/Wave_B or Wave1/Wave2, with optional FBX name prefix. Missing, mixed, ambiguous or additional morphs fail; bones and collider restrictions remain.

Single-Wave1 foam still fails the skinned path. Do not duplicate a morph or fabricate a second name to pass. Review final export before selecting a foam implementation. The runtime's complementary sinusoidal weights do not reproduce the draft's two independent Blender drivers (2/3 cycles over 250 frames, second phase 1.2). Appearance and foam seams need native review.

## Runtime batching integration proposal

`HiggsfieldStaticBatching` is available but never automatically attached or invoked. Import defaults, recipes, content hashes and AlfaApplication are unchanged. BatchingStatic is intentionally not assigned: a build-time bake could occur before the runtime moving-part exclusions are supplied. The coordinator can opt in on a new instance after placement and an immobility audit:

```csharp
// approvedImmutableRenderers comes from audited map paths; never all child renderers blindly.
// excludedRoots includes every moving part and any additional water/foam not tagged by the importer.
var batching = map.AddComponent<HiggsfieldStaticBatching>();
int batched = batching.CombineOnce(approvedImmutableRenderers, excludedRoots);
```

Both arrays are mandatory. An empty exclusion array means the caller audited that there are no additional exclusions. Null/outside-map entries fail. The hook filters water, boneless/skinned morphs, animation/rigidbody ancestry, disabled/inactive objects, unreadable meshes, existing batches and shader batching exclusions. Arbitrary future scripts that move geometry cannot be inferred: the positive list is the caller's lifetime immobility declaration.

The hook uses Unity's explicit GameObject-array overload. Unity retains individual GameObjects and culling, copies mesh data into batch buffers, and requires combined child transforms to remain fixed relative to the root. Therefore eligible MeshFilter mesh references may change and batch memory needs measurement. [Unity 6.3 Combine API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/StaticBatchingUtility.Combine.html).

It checks all transform parents/local poses (including anchors), renderer/collider identities, material slots, MeshCollider mesh references and excluded mesh references after the call. A failed check requires discarding the instance; no unsafe retry/rollback is attempted. Fewer than two eligible candidates causes no Combine. EligibleCount, BatchedCount and StructurePreserved provide diagnostics, not FPS evidence.

## Validation and remaining native gate

Offline compilation against installed Unity 6000.3.24f1 passed; 49 pure System contract/name checks passed (12 new naming checks). No Combine execution is claimed. With a granted Unity slot, test a runtime prefab instance: two compatible static solids, water MeshFilter, boneless skinned water, single-morph foam, animated/moving subtree and EMPTY spawn anchors. Confirm exclusions, counts/invariants, unchanged collider raycasts and anchors, working water, repeated-call rejection, and clean destruction/recreation. Capture before/after draw calls and memory on the target pipeline before choosing any default. Final Campamento import and shader/platform batching compatibility remain pending.
