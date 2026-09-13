# Campamento Pinar — final recipe delivery

Recipe: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/UnityRecipe/hf-campamento-pinar-v1.recipe.json`.
Export crosschecks: adjacent `hf-campamento-pinar-v1.validation.json`.
Reproducible descriptor: `campamento.final-input.json`; compact hashes, water and emission evidence: `campamento.delivery.json` in this directory.

Coordinator confirmed the Astra refinement final. The descriptor pins clean FBX SHA256 `7928ebce668c4fdd4a2a46807aba1c74996b9b0813444da5854678308d2b7808`; source GLB is the final original `.glb`, not the preserved Free draft. Generator rereads source hashes before output. Recipe SHA256 `de9ef0efc4d1ef93445de747f17156f9c37f6dace21a5a06f4f35ceea2350f43`.

Passed: 1493 exported objects, 1436 mesh objects, 1056 GLB mesh definitions, 76947 instanced triangles, 40 materials. Classification: 501 solid, 920 decoration, 8 water, 7 foam. Audit, GLB and clean FBX names/hierarchies/material slots agree. The 15 explicitly hidden/export-excluded source remnants agree with the clean exporter receipt and are absent from both exports; 1508 source objects therefore remains consistent. The generator now validates present nested report export/source/triangle/material/spawn/collision counts instead of ignoring the new report layout. Ten Python tests pass, including rejection of each inconsistent nested count and overlapping exclusion lists.

Five human and sixteen mosquito EMPTY spawns retain their physical `.002` names and full paths. Layer Default. Explicit playable bounds convert coordinator Blender XYZ `[-46,-36,-.5]..[46,36,10]` into Unity XYZ `[-46,-.5,-36]..[46,10,36]`. Every spawn's audited world origin is inside. Visual extents are deliberately not substituted for playable bounds.

Five emissive materials match audit linear color times strength against GLB emissiveFactor/KHR strength: Ember, LanternGlass, FlameAmber, FlameGold, FlameHeart. Values are recorded in the delivery JSON and recipe; no new lights or shader defaults were added.

LakeCenter and LakeSeam require exact-path water overrides because their names lack the Water_ prefix. All eight water meshes have Wave1/Wave2 in audit, GLB and clean FBX. Foam_Lake_01/02 each retain only Wave1; the other five foams have no morphs. All 15 receive the existing water/foam component. CPU settings use the contract defaults (.025 m amplitude, 4 m wavelength, 8 s cycle); they do not reproduce independent Blender driver timing. GLB vertex entries are evidence only, not native Unity counts. Current two-morph skinned support remains unchanged: if native import produces SkinnedMeshRenderer for single-morph foam, root will decide an explicit one-morph foam implementation after observing that result.

The actual C# `HiggsfieldImportContract.Validate` and `Resolve` passed all 1436 recipe paths without Unity. Reproduce with:

```powershell
./Higgsfield/Integration/Verify-RecipeOffline.ps1 -Recipe N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/UnityRecipe/hf-campamento-pinar-v1.recipe.json
```

No native applications, bridge or paid generation were started by this task. Root owns the native import now underway. Renderer types, water seams/motion, capsules and routes, lighting, navigation/registry and runtime performance remain outside this offline acceptance. Do not change source art or relax import guards to hide a native failure.
