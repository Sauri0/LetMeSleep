# Environment 0.9.1 — v2 validation results

Branch: `codex/091-environment`
Map: `house-v2-1`
Renderer: Godot 4.5.2 Compatibility / OpenGL 3, 1920×1080
Adapter: NVIDIA GeForce RTX 3060 Ti

## Automated evidence

The authorized source run completed with exit 0 and empty stderr for every
test below:

- `environment091_stair_lighting_test.gd`: 15 checks, 0 failures.
- `environment091_v2_finish_test.gd`: 108 checks, 0 failures over seeds 1, 2,
  17, 42, 777, 1988 and 2147483646.
- `frame07_joinery_test.gd`: 992 checks, 0 failures, 0 overlaps and 0 normal
  errors; approximately 268.103 ms.
- `house07_joints_test.gd`: 603 checks, 0 failures.

The seven-seed v2 test verifies the 32 positional-light Compatibility limit,
all four authored stair lights, finite spotlight bases, high-quality shadow
semantics and zero intersections between rendered baseboards/wall finishes
and declared `stair_side` clearance volumes.

An earlier `house09_surfaces_test` result of 864/0 and
`house09_ceiling_test` result of 3/0 was discarded: those pre-migration
fixtures requested rejected v1 maps and observed the authored fallback map.
The Director integrated the migrated fixtures in `0572155`; the combined
build owns their authoritative rerun.

## Native capture and visual review

The capture probe completed in 9.7 seconds with exit 0 and empty stderr. Its
manifest contains 23 distinct 1920×1080 PNG hashes: eight bidirectional stair
views and fifteen central/lateral route views. The loaded map reported 45
circulation routes, four stair connections and 32 local lights; all four
authored stair lights were created, none dropped, and the budget remained
inside the Compatibility cap.

Worker 2 reviewed all 23 images. Treads, landings, rails, floors, ceilings,
wainscot and baseboards remain continuous. The lateral routes are open on
both sides of every stair opening. No broad duplicate planes, finish
penetrations, blocked passages, detached shadows or visible z-fighting were
found in the stills. A static capture cannot prove absence of temporal
flicker.

Two visual limits remain. Upper-flight undersides become nearly black in some
views, and rail/window shadows are hard and repetitive. The walking surfaces
and stair edges remain readable, so this is recorded as lighting polish for
0.9.2 rather than an unplayable 0.9.1 defect. One lateral view exposed mirrored
fixed signage from the rear; `world.gd` now disables double-sided rendering
for fixed labels while retaining it for billboards. That post-capture change
requires the combined build's visual recapture.

The Director independently reviewed `stair-0--1-up.png`,
`stair-0-1-down.png` and `route-003-stair_side.png` and found continuous,
readable stairs, corridors and landings without the broad duplicate planes
seen in the source video. This is partial review, not global visual approval.

Representative committed views:

- `captures-house-v2-1/stair-0--1-up.png`
- `captures-house-v2-1/route-003-stair_side.png`
- `captures-house-v2-1/route-030-central.png`

Comparable performance measurement remains deferred to the final combined
source set. No GTX 1660 Ti performance claim is made.
