# Puerto del Faro — fuente ajustada y receta GPU

Fuente Blender recomendada: `05-pueblo/UnityAdjustedSource/HF_MAP_05_pueblo_UNITY_ADJUSTED.blend`, SHA256 `18466cf28d52587f5b246e95b95093557e5e310b6bfd6ee69df960760390fce7`. Contiene sólo el bisel final de 30 mm del segundo peldaño exterior. Readback PASS y originales preservados. Los FBX/GLB siguientes conservan el peldaño original; no hubo reexport/import. Detalle y recibo: `PuertoAdjustedSource/README.md`, `PuertoAdjustedSource/delivery.json`.

# Puerto del Faro — explicit GPU water recipe delivered

Final source descriptor: `puerto.final-input.json`, map ID `hf-puerto-del-faro-v1`. The clean FBX is `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo/HF_MAP_05_pueblo_UNITY.fbx`, SHA256 `aa4bb192353bfbebe5f3d48e4dd6950245b854f397b95a7fb7d7ac85a2a834a4`. Source exports remain unchanged.

Final recipe: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo/UnityRecipeGpu/hf-puerto-del-faro-v1.recipe.json`, SHA256 `fd3ae29ef31b71bfdf66d5cb4f86b9664a1b5a06553a6ee9268b950cbe01a2b5`. Validation JSON is adjacent; compact metadata and exact central validation-code hashes are in `puerto.delivery.json`.

The earlier CPU candidate remains intact in `05-pueblo/UnityRecipe` as evidence and must not be substituted for this GPU recipe. The only recipe changes are Water_Ocean_Pueblo waterAnimation=GPU and amplitude .025 -> .09. No palette, geometry, collision, spawn or bounds changes occurred. Both final recipe and validation were reproduced identically by the current central generator; actual C# Validate/Resolve passed all 730 paths.

## Source evidence

Clean FBX, GLB and audit agree on names, hierarchy, effective material slots, base RGBA and emission. Counts are 811 export objects, 730 mesh objects, 242 GLB mesh definitions, 175746 triangles and 36 materials; no exclusions and no GLB animation clips. The top-level report count aliases were additionally checked against audit and GLB instance triangle totals. Classification is 458 solids, 270 decorations, one ocean and one foam.

All 21 spawns have physical names `Spawn_Human_01_Pueblo` through `05_Pueblo` and `Spawn_Mosquito_01_Pueblo` through `16_Pueblo`. Their unique semantic_name values match canonical role/number names across report, audit and GLB. They are EMPTYs, belong to HF_MAP_05_pueblo, and retain physical paths in the recipe. No name rewriting or role guessing is needed.

Bounds_Min_Pueblo and Bounds_Max_Pueblo match the reported Blender world limits `[-55,-42.5,-2]..[55,42.5,20]`. The descriptor uses Unity XYZ `[-55,-2,-42.5]..[55,20,42.5]`, containing all 5/16 audited spawn origins and excluding the ocean/backdrop from playable extents.

All 36 materials are OPAQUE, opacity 1. PDF05_Amber is the only emissive swatch: linear RGB approximately `[1,.46,.085]`, strength 2.1, matched against GLB emissive data. Exterior and interior previews were reported acceptable by the coordinator; this task did not perform native visual approval.

## Explicit water mode and native follow-up

Water_Ocean_Pueblo has 3721 authored vertices, Wave_A/Wave_B and 21597 GLB POSITION entries across its material primitives. The latter does not establish Unity's final MeshFilter vertex count. Foam_Shoreline_SparsePatches has 160 authored vertices/GLB entries and no morphs.

The descriptor explicitly selects `gpuWaterPaths=["Water_Ocean_Pueblo"]`; foam remains CPU. The updated generator recognizes source wave_amplitude_m=.09 and wave_period_seconds=8. The recipe uses .09 m amplitude, an 8-second cycle and the existing generic 4 m wavelength. The source report separately records Wave_B=.07 and signed morph weights: this .09 m runtime envelope is conservative and is not exact parity with the two authored Blender wave functions. The <=.15 amplitude and 20,000-vertex CPU guards were not raised.

Native renderer/GPU water operation, ground normals/culling, human capsules, lighthouse stairs, routes, foam continuity, lighting and runtime performance remain coordinator-owned checks. No Blender/headless, bridge, credits, art edits or Unity process were used here. Generator, contract, importer and auditor source files remain untouched by this task.
