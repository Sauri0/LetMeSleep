# Yate — final recipe validated

Recipe: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/UnityRecipe/hf-yate-a-la-deriva-v1.recipe.json`.
SHA256: `28c2d86e4d798f7f426be20c5a2a934c658b0c542a6ac54cebbbde24bcf4a05c`.
Adjacent validation JSON records strict export crosschecks. Compact delivery evidence, source hashes and exact central validation-code hashes are in `yate.delivery.json`.

The coordinator generated this recipe with its updated alpha/report/wave support. Running the current central generator in memory with `yate.final-input.json` reproduced both saved recipe and validation structures identically, without overwriting them. The central `Verify-RecipeOffline.ps1` compiled the current actual C# contract and passed Validate/Resolve for all 160 paths. No reserved source files were edited in this validation task.

Counts: 213 objects, 160 mesh objects, 23 materials; 96 solids, 62 decorations, one water, one foam; 5 human and 16 mosquito spawns with physical `.003` paths. Explicit Unity PlayBounds remain `[-4.6,.7,-15.2]..[4.6,10.4,15.1]`.

`YATE_Glass` now preserves authored `alphaMode=BLEND`, opacity `0.30000001192092896` and `doubleSided=true`. The report.map identity and nested count aliases are recognized while retaining strict source checks. Actual native transparent material state, sorting and culling still need Unity review.

`Water_Ocean` now reads authored wave_params: amplitude envelope `.085+.055=.14 m`, cycle `10 s`, wavelength `2π/hypot(.42,.22)=13.25200877883 m`, angular speed `.62831853072`. These values approximate the authored envelope/period/spatial scale; the current runtime directions and time functions are not exact parity with independent Blender morph drivers. Foam keeps its existing default CPU settings. No new source wave shapes or animation clips were created.

Native import remains pending coordinator execution. The ocean's 16,160 source vertices and 96,000 GLB POSITION entries do not determine Unity's final MeshFilter vertex count. Check the actual renderer/mesh against the existing 20,000-vertex CPU guard before choosing its runtime path. Water/foam visual continuity, glass, ground/capsule/routes, lighting and runtime performance remain native review items.

No Blender/headless, bridge, rendering or Unity process was started here; only .NET contract validation and read-only Python file crosschecks ran.
