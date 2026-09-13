# Yate v3 — corrected ocean winding and explicit GPU water

## Editable source synchronized with the final native repairs

`N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/UnityAdjustedSource/HF_MAP_04_yate_UNITY_ADJUSTED.blend` is the adjusted editable copy, SHA256 `de6304396d5c95aab52003be5118058b15a7819e734694fb14f5d5fecaf8ae50`. It includes the rigid storage/lid translation (Blender −1.4,+2,0 m) and the bulkhead notch within X [1.625,3.075], above Z 2.70. The final Unity apply/readback ContentHash is `dee1fea0f5f21429ed434668ee408a6ec3efd67984ad24e0640faa1177ca20d6`.

The existing visible MCP wrote only the Yate scene and dependencies, then independently read back the three changed objects. Source-copy validation passed; all live geometry/transforms were restored, Puerto remained active and idle, and original NormalsV3 files retained their hashes. Details and receipts are in `YateAdjustedSource/README.md` and `YateAdjustedSource/delivery.json`. No new FBX/GLB or Unity import was performed: the NormalsV3 exports below retain the original storage position and uncut bulkhead. Full portable engine/source equivalence is not claimed.

## Original NormalsV3 export package

Recipe: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/UnityRecipeV3/hf-yate-a-la-deriva-v3.recipe.json`.
SHA256: `ebae0978d8fcaf9ff371601e7a3175503f82b61e8e87c5faba5e36337d5d1a08`.
Descriptor: `yate-v3.gpu-input.json`. Adjacent validation JSON records strict export crosschecks. Current evidence is `yate-v3.delivery.json`; v1/v2 recipes and metadata remain historical evidence.

V3 corrects a confirmed source defect: all 32000 Water_Ocean triangles faced down. The visible Blender MCP slot reversed only that object's polygon winding; vertices, morph coordinates/weights, materials, all other meshes and scene transforms were unchanged. The serialized GLB was independently decoded and its transformed triangle cross-products changed from 32000 down to 32000 up. A standalone editable source containing only HF_MAP_04_yate plus dependencies and clean exports are in `04-yate/NormalsV3`. Original Yate files remain intact in place and are SHA-preserved in SourceBeforeNormals. Puerto's active scene was restored and its file hash stayed unchanged.

V1's native import hit the existing CPU water vertex guard. V2 introduced explicit `gpuWaterPaths=["Water_Ocean"]`; this remains in v3. Native GPU motion passed according to root, but the source still had reversed winding, prompting v3. The current central generator reproduced the v3 recipe and validation identically. The actual central C# contract passed Validate/Resolve for all 160 paths. Only mapId/sourceFbx/sourceSha256 differ from the v2 recipe; GPU mode, materials, spawns, bounds and wave parameters are identical. No reserved generator/contract/importer files were edited.

Counts: 213 objects, 160 mesh objects, 23 materials; 96 solids, 62 decorations, one water, one foam; 5 human and 16 mosquito spawns with physical `.003` paths. Explicit Unity PlayBounds remain `[-4.6,.7,-15.2]..[4.6,10.4,15.1]`.

`YATE_Glass` now preserves authored `alphaMode=BLEND`, opacity `0.30000001192092896` and `doubleSided=true`. The report.map identity and nested count aliases are recognized while retaining strict source checks. Actual native transparent material state, sorting and culling still need Unity review.

`Water_Ocean` now reads authored wave_params: amplitude envelope `.085+.055=.14 m`, cycle `10 s`, wavelength `2π/hypot(.42,.22)=13.25200877883 m`, angular speed `.62831853072`. These values approximate the authored envelope/period/spatial scale; the current runtime directions and time functions are not exact parity with independent Blender morph drivers. Foam keeps its existing default CPU settings. No new source wave shapes or animation clips were created.

V3 native import remains pending coordinator execution. The ocean explicitly uses GPU displacement; foam keeps its existing CPU path. The 20,000-vertex CPU guard remains unchanged. Source culling, shader/material assignment, displacement bounds, water/foam continuity, glass, ground/capsule/routes, lighting and performance remain native review items. No v3 native success is inferred from recipe validation.

No new Blender/headless, rendering, paid generation or Unity process was started. V3 used the explicitly authorized existing visible Blender MCP connection, then .NET and Python file checks. The one-shot guarded repair is in `yate_normals_repair.py`; it aborts on scope/source mismatch or an existing NormalsV3 folder.
