# Yate v2 — explicit GPU water recipe validated

Recipe: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/UnityRecipeV2/hf-yate-a-la-deriva-v2.recipe.json`.
SHA256: `6b47362e7b862bb99c6532f8f3cbf1412c6b15e1344e3d7b3af6c7f55682da81`.
Descriptor: `yate.gpu-input.json`. Adjacent validation JSON records strict export crosschecks. Compact current delivery evidence, source hashes and exact central validation-code hashes are in `yate-v2.delivery.json`. The prior v1 recipe and `yate.delivery.json` remain historical evidence.

V1's native import hit the existing CPU water vertex guard. The coordinator implemented explicit GPU water support and requested v2 with `gpuWaterPaths=["Water_Ocean"]`. Running its current central generator with the new descriptor generated v2, and a second in-memory preparation reproduced recipe and validation identically. The central `Verify-RecipeOffline.ps1` compiled the current actual C# contract and passed Validate/Resolve for all 160 paths. Only mapId v1 -> v2 and the ocean's waterAnimation CPU -> GPU changed relative to v1; source exports, materials, geometry, spawns, bounds and wave parameters are identical. No reserved source files were edited.

Counts: 213 objects, 160 mesh objects, 23 materials; 96 solids, 62 decorations, one water, one foam; 5 human and 16 mosquito spawns with physical `.003` paths. Explicit Unity PlayBounds remain `[-4.6,.7,-15.2]..[4.6,10.4,15.1]`.

`YATE_Glass` now preserves authored `alphaMode=BLEND`, opacity `0.30000001192092896` and `doubleSided=true`. The report.map identity and nested count aliases are recognized while retaining strict source checks. Actual native transparent material state, sorting and culling still need Unity review.

`Water_Ocean` now reads authored wave_params: amplitude envelope `.085+.055=.14 m`, cycle `10 s`, wavelength `2π/hypot(.42,.22)=13.25200877883 m`, angular speed `.62831853072`. These values approximate the authored envelope/period/spatial scale; the current runtime directions and time functions are not exact parity with independent Blender morph drivers. Foam keeps its existing default CPU settings. No new source wave shapes or animation clips were created.

V2 native import remains pending coordinator execution. The ocean explicitly uses GPU displacement; foam keeps its existing CPU path. The 20,000-vertex CPU guard remains unchanged. Shader/material assignment, displacement and culling bounds, water/foam visual continuity, glass, ground/capsule/routes, lighting and runtime performance remain native review items. No native success or performance gain is inferred from recipe validation.

No Blender/headless, bridge, rendering or Unity process was started here; only .NET contract validation and read-only Python file crosschecks ran.
