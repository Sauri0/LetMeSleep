# Environment 0.9.2 — render and shadow cost plan

Status: static review only. No Godot process ran and no 0.9.1 runtime changed.

## What the existing evidence rules out

- The molding/railing probe reduced 392 mesh instances to 128 MultiMeshes but
  raised Compatibility hallway draws from 1,685 to 2,680 and upper-hall draws
  from 1,437 to 3,503. Do not batch lit geometry across spatial/light bounds.
- Shortening room-light ranges raised draws in four of five views and reduced
  computed corner contribution to roughly 50–57% of the previous value. Keep
  v3 room-light reach unchanged until a new controlled test proves otherwise.
- Forward+ reduced static draws and live frame time on the RTX 3060 Ti, but
  changed the image and still did not sustain 60 FPS in the 16-player run.
  Compatibility remains the 0.9.2 baseline; a renderer migration is a separate
  hardware and visual decision.

## Static cost bound

`environment092-static-render-cost.json` reads the JSON chunk of the 23 GLBs
used by the 64 FurnitureBlueprint entries. It is an inventory, not a Godot
draw-call measurement. The Blueprint-weighted furniture instance contains
4.140625 material primitives, 4,236 triangles and 3,460 vertices on average.
All 23 imports already request generated LODs and shadow meshes.

World also creates one transparent, unshaded, non-shadow-casting contact quad
per furniture item. If every item were visible and affected by one shadowed
light, the submission proxy is:

| Limit | Visible model surfaces + contact quads | Shadow surface candidates |
|---|---:|---:|
| v2: 88 furniture | 452 | 364 |
| v3: 144 furniture | 740 | 596 |
| Increase | +288 | +232 |

The 63.64% object-count increase is the risk; actual frame draws depend on
frustum visibility, light overlap, material passes and renderer behavior.
`pantry_shelf` has 10 material primitives; `bath_vanity`, `bookcase`,
`game_table`, `sewing_table`, `sink` and `stove` have 7 each. Preserve their
visible detail and target redundant submissions around them.

## Agreed v3 contract

Modelador 1 will publish these fields on every `kind=furniture` structure:

- `room_id: String`
- `functional_zone_id: String`
- `essential: bool`
- `placement_order: int`, stable for the map

`functional_zones[].bounds` remains composition/interaction metadata. It is
not a collision volume, room or light volume. Worker 2 keeps one light derived
from each `room.bounds`; internal zones never add lights. The existing cap
remains `rooms + horizontal halls + stair_light_anchors <= 32`.

## Candidates, one factor at a time

1. **Batch contact quads by functional zone.** Use one unit quad and shared
   unshaded material per `functional_zone_id`, with per-instance transform and
   a tight union AABB. At the 144-item cap, changing 144 quads to at most 44
   zone batches has an all-visible upper saving of about 100 submissions. This
   avoids the old batch probe's light-selection failure because these quads
   are unshaded and never cast shadows. Reject on alpha ordering differences,
   wider visibility than the source quads, or a measured regression.
2. **Disable model shadow casting only for non-essential complements.** Keep
   essential items and pickup surfaces as normal casters; retain every visible
   model and its contact quad. If the 56 objects above the v2 cap are all
   complements, the upper proxy removes 232 shadow-surface submissions per
   affecting shadow light. Test this after contact batching, and reject rooms
   whose furniture loses grounding or whose moving actor shadows become
   visually ambiguous.
3. **Trial distance culling only for non-essential complements.** Compare
   `visibility_range_end` off, 24 m and 18 m. Essential items, pickups and task
   supports remain uncapped. A continuous route capture must cover open stair
   sightlines and thresholds in both directions. Reject any visible pop or an
   empty-looking secondary zone. Do not cull room lights or their shadows by
   camera distance: a source can still illuminate the adjacent room through an
   open door.

Do not combine candidates until each passes independently. Keep the existing
`shadows=0` mode only as a diagnostic upper bound; it is not the proposed
high-quality result.

## Required A/B

Choose one two-floor and one three-floor v3 seed, including the highest valid
furniture count, and freeze map fingerprints, furniture manifest, doors,
cameras, resolution and renderer. Record the exact source hashes and actual
furniture/essential/complement counts.

For each candidate, run baseline and candidate in interleaved fresh processes.
Use the existing room, hall and upper-hall cameras plus a compound room, both
stair sides and one long diagonal. After equal warm-up, collect at least 60
samples per view of:

- draw calls, primitives and visible objects;
- render CPU/GPU p50, p90 and p99;
- shadow-casting furniture and material-surface candidates in the view;
- 1920×1080 paired captures and a route sequence across each culling boundary.

Contact batching should preserve paired pixels except for bounded alpha
rounding. Shadow and culling candidates require manual review for furniture
grounding, stair readability, light leaks and temporal popping. Accept a
candidate only when draw-call p50/p90 falls in the affected views, render
CPU/GPU does not regress by more than 5% in any representative view, and the
visual review passes. Report unchanged or noisy measurements as inconclusive.

Finally, run `performance07_live.gd` on the same final v3 seed at 1080p,
Compatibility, unlimited FPS and natural doors: 2 actors/high shadows,
16 actors/high shadows, and 16 actors/shadows off as diagnostic. Use three
interleaved repetitions per baseline/candidate and compare frame/render
CPU/render GPU/draw/primitives p50, p90, p99 and `% >16.67 ms`. RTX 3060 Ti
results do not certify GTX 1660 Ti; repeat the accepted candidate on target
hardware before making that claim.
