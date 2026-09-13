# Campamento v2 — confirmed winding repair

Root's v1 native review showed missing interior ground/water and four of five human spawns without a front-facing ground hit. Headless Blender 5.2.1 LTS confirmed the source cause: all 1363 terrain triangles and the six original water surfaces faced downward in world space. LakeCenter had 17 downward triangles and one correct upward triangle; LakeSeam's eight triangles also faced down. This was actual polygon winding, not a material-color hypothesis.

The original editable source, both existing FBX exports, GLB, audit and reports were copied and SHA-verified into `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/SourceBeforeNormals` before any source edit. V1 recipes, exports and Unity evidence remain intact. The editable main `HF_MAP_03_campamento.blend` now contains the correction. New exports and audit are under `NormalsV2`; the new recipe is `UnityRecipeV2/hf-campamento-pinar-v2.recipe.json`.

## Exact changes

1743 polygons across 12 objects were reversed, without adding/removing faces or changing their vertex membership:

| Objects | Flipped polygons | Evidence |
|---|---:|---|
| CAMP_Terrain_Grass | 1363 | All 1363 triangles downward, now upward |
| CAMP_Lake_Bed | 1 | All 16 triangles downward, now upward |
| Six Water_* objects | 328 | All 656 triangles downward, now upward |
| CAMP_Prod_LakeCenter | 17 | Flipped only downward triangles; kept the existing upward triangle |
| CAMP_Prod_LakeSeam | 4 | Eight triangles downward, now upward |
| CAMP_Shelter_Roof_R | 4 | Right roof slope downward; matching left roof already upward |
| CAMP_Prod_ChoppingBoard | 26 | Closed manifold with consistent inward winding; signed volume -0.00293859 m³, now positive |

Open surfaces were selected by explicit object name and tested world triangle direction. The board additionally required exactly paired, consistently directed manifold edges. No global normal recalculation or blanket downward-face flip was used. Existing foam, paths, plaza, rim, other modules and ambiguous roof-ridge geometry were left untouched.

The script used Blender's polygon winding inversion with no custom split normals present; the API defines this operation as flipping the polygon orientation. [MeshPolygon API](https://docs.blender.org/api/3.2/bpy.types.MeshPolygon.html). Standard FBX export used temporary unique mesh copies with effective object material slots, then restored them. GLB exported the selected active scene, retained morphs and contained no animation clips. [Export operators](https://docs.blender.org/api/main/bpy.ops.export_scene.html).

## Verification

Before/after in-memory fingerprints verified unchanged vertices, shape-key coordinates/weights, polygon membership/count/material indices/smoothing, object transforms/parents/material slots, all other meshes and all saved scenes. The old and renewed audits have exactly equal object records, materials and exclusion lists. Counts remain 1493 export objects, 1436 mesh objects, 1056 GLB mesh definitions, 76947 triangles, 40 materials, 501 solids, 8 water, 7 foam, 920 decorations and 5/16 spawns. PlayBounds and emission remain unchanged.

The saved .blend, exported FBX and exported GLB were independently reopened/imported in isolated headless Blender. All three confirm upward terrain/water/roof surfaces and positive board volume. Strict recipe crosschecks and the actual C# Validate/Resolve over 1436 paths pass. Full face indices and before/after directions are in `NormalsV2/normals-repair.json`; readback is in `normals-readback.json`. Compact evidence and hashes are in `campamento-v2.delivery.json` beside this document.

Key hashes:

- Editable .blend: `47b8e7d6e674244d9d46aaa32bf6f1e8fe0f71d99750612f84eaa44811a887df`
- V2 FBX: `3fd036eb8ede448aa48eb72764ae9f143dbf6ecff375e1ae10d144dfb44badc3`
- V2 GLB: `d93aeb853c0193b27126541d50342d6517d0f76ae906ce665f7c306f4a9b76d7`
- V2 recipe: `a476f692f8bea9429cd437a9f16c0f72735e54e6d305414f5f6b3a2a9171daae`

All Blender processes used `--background --factory-startup --disable-autoexec`; no GUI, rendering, Higgsfield/bridge connection or Unity execution occurred here. Root still owns Unity v2 culling, ground/capsule tests, water Play Mode and performance review. No double-sided global culling workaround or invisible ground was added.
