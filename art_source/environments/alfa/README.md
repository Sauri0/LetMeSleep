# Alfa environment asset library

Original environment pack for the fixed house and patio, following
`work/references094/environment.png`: restrained wood/terracotta/stone palette,
opaque matte materials, visible planar facets and simple joinery. No paid
providers, downloaded models, character redesign, or runtime map generation.

## Engine contract

Preload `res://assets/art/house/alfa_library.gd`. Public static API:
`catalog()`, `has_asset(id)`, `spec(id)`, `bounds(id)`, `placement_bounds(id)`,
`collision_boxes(id)`, `support_surfaces(id)`, `instantiate_asset(id)`.

An instance has identity scale and is placed by its root position/rotation.
Coordinates are metres, Y up. Ground pivots are at base centre. Shutter,
canopy and reused wall shelf have back-plane/base pivots (back is Z=0,
projection toward -Z). The bed retains its existing head at -X. No auto-fit,
random placement, material mutation, or physics bodies are added by the library.

```gdscript
const Alfa = preload("res://assets/art/house/alfa_library.gd")
var model = Alfa.instantiate_asset("alfa_bench")
parent.add_child(model)
model.position = Vector3(2, 0, 8)
model.rotation.y = PI / 2
var reservation = model.transform * Alfa.placement_bounds("alfa_bench")
for local_box in Alfa.collision_boxes("alfa_bench"):
    var physical_box = model.transform * local_box
    # Map/World consume this box using their authoritative physics system.
```

`visual_bounds` is the measured imported mesh AABB. `placement_bounds` encloses
both visible bounds and every physical box: reserve it when placing assets,
then add actor/path clearance separately. `collision_boxes` are conservative
local volumes, often compound (legs, boards, trunk/canopy); the map owns their
admission and rotation. They are not exact mesh collision or navigation data.
`support_surfaces` provide centre XYZ, clear width/depth and edge margin; native
CPU triangle tests verify five points on each declared support surface.

Grass, shrubs and path stones have no collision boxes. Their placement bounds
still reserve visible foliage/stone. Path stones are 35 mm thick decoration;
terrain/World remains walking-surface authority. Do not place them above a
second coplanar decorative ground mesh. Trees reserve the full canopy, not
only the trunk. Legacy kitchen/bath models use conservative full bounds and
have no declared clear pickup surfaces; use the existing task contract there.

Reused assets reference their existing GLB and BLEND directly. The wrapper
applies only the documented import offset to normalize the pivot; no bytes of
those resources were changed. Scene assets remain shared, but spec dictionaries
are copied before returning them so a caller cannot corrupt the catalog.

## Measured catalog

Sizes below are placement reservations (X width, Y height, Z depth), including
physical overhang. `manifest.json` carries exact minima/sizes, shape boxes,
supports, source paths and SHA256 for GLB and editable sources.

| ID | Reservation metres | Triangles | Origin |
|---|---|---:|---|
| alfa_bench | 1.650 × 0.920 × 0.600 | 484 | original_alfa |
| alfa_patio_table | 1.498 × 0.760 × 0.850 | 484 | original_alfa |
| alfa_chair | 0.500 × 0.940 × 0.520 | 396 | original_alfa |
| alfa_stool | 0.380 × 0.460 × 0.380 | 220 | original_alfa |
| alfa_fence_panel | 2.000 × 1.000 × 0.140 | 528 | original_alfa |
| alfa_fence_post | 0.160 × 1.080 × 0.160 | 50 | original_alfa |
| alfa_path_stone | 0.523 × 0.035 × 0.409 | 24 | original_alfa |
| alfa_planter | 0.460 × 0.900 × 0.460 | 150 | original_alfa |
| alfa_pine | 1.800 × 3.400 × 1.800 | 108 | original_alfa |
| alfa_bush | 1.050 × 0.750 × 0.850 | 20 | original_alfa |
| alfa_rock_small | 0.650 × 0.450 × 0.500 | 20 | original_alfa |
| alfa_rock_large | 1.050 × 0.850 × 0.850 | 20 | original_alfa |
| alfa_grass_patch | 0.634 × 0.300 × 0.585 | 36 | original_alfa |
| alfa_mailbox | 0.418 × 1.200 × 0.508 | 176 | original_alfa |
| alfa_crate | 0.550 × 0.550 × 0.550 | 440 | original_alfa |
| alfa_barrel | 0.580 × 0.820 × 0.580 | 188 | original_alfa |
| alfa_log_stack | 0.900 × 0.480 × 0.640 | 96 | original_alfa |
| alfa_porch_post | 0.180 × 2.600 × 0.180 | 44 | original_alfa |
| alfa_shutter | 0.440 × 1.200 × 0.079 | 440 | original_alfa |
| alfa_canopy | 1.600 × 0.280 × 0.600 | 12 | original_alfa |
| alfa_chimney | 0.650 × 1.100 × 0.600 | 232 | original_alfa |
| alfa_bed | 1.985 × 0.750 × 1.180 | 8968 | reused_project |
| alfa_table | 1.450 × 0.720 × 0.850 | 840 | reused_project |
| alfa_desk | 1.450 × 0.720 × 0.850 | 1148 | reused_project |
| alfa_nightstand | 0.600 × 0.590 × 0.520 | 1028 | reused_project |
| alfa_bookcase | 1.120 × 1.350 × 0.520 | 4788 | reused_project |
| alfa_wardrobe | 1.120 × 1.350 × 0.600 | 3524 | reused_project |
| alfa_dresser | 1.120 × 1.350 × 0.578 | 2976 | reused_project |
| alfa_sofa | 2.474 × 1.015 × 1.000 | 14416 | reused_project |
| alfa_armchair | 1.324 × 1.015 × 1.000 | 8352 | reused_project |
| alfa_sink | 1.720 × 1.120 × 0.760 | 4532 | reused_project |
| alfa_stove | 1.720 × 1.154 × 0.773 | 4416 | reused_project |
| alfa_fridge | 0.720 × 1.460 × 0.787 | 1160 | reused_project |
| alfa_washer | 1.500 × 1.013 × 0.728 | 7088 | reused_project |
| alfa_toilet | 0.614 × 0.980 × 0.752 | 2488 | reused_project |
| alfa_bath_vanity | 1.600 × 1.088 × 0.724 | 3756 | reused_project |
| alfa_wall_shelf | 0.950 × 0.626 × 0.249 | 4232 | reused_project |
| alfa_lamp | 0.521 × 0.545 × 0.521 | 1844 | reused_project |

## Reproduction and verification

Run local Blender in background with `--python-exit-code 1 --python
art_source/environments/alfa/build_pack.py`. Optional asset names follow `--`.
Then run `python art_source/environments/alfa/inspect_pack.py`: this measures
real GLB bytes/transforms, adds reuse aliases, regenerates matching manifests
and checks geometry/material/physical policy. No remote service is involved.
Each new asset has a BLEND with named editable components and a self-contained
GLB. GLB join/export does not flatten the saved source components.

Import `game` with Godot 4.5.2 headless, then run
`res://tests/assets094_pack_test.gd -- --output=<absolute report folder>`.
Native renderer option `--capture` adds interior/garden/facade image sheets;
its GPU turn must be coordinated with Director. `run_tool.ps1` bounds process
execution and records exit code, timestamps, timeout and stderr length.

Current validation: static 491/0; Godot import exit 0; headless imported geometry
379/0. Native visual review is pending the coordinated render turn. These
results do not claim integrated placement, gameplay paths, WAN or frame rate.
Integration and performance belong to the corresponding map/runtime owners.

The new assets use project-original geometry/materials under repository terms;
reference art directs the style and is not embedded as a texture. Existing
assets preserve their original source provenance. No third-party licensing or
provider dependency has been added. The game-dev CLI is unavailable locally;
these are local Blender/Godot receipts, not a game-dev canonical-package receipt.
