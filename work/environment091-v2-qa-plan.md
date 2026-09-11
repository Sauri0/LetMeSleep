# Environment 0.9.1 — prepared v2 QA

Status: executed for the Worker 2 gates and native `house-v2-1` capture.
Surface/ceiling fixtures were subsequently migrated in `0572155`; the
combined build owns their authoritative rerun. Performance remains deferred
until all 0.9.1 branches are integrated.

## Headless gates

Run each in its own process with a 55-second external timeout and separate
stdout/stderr logs:

1. `res://tests/modeler091_layout_test.gd`
2. `res://tests/environment091_stair_lighting_test.gd`
3. `res://tests/environment091_v2_finish_test.gd`
4. `res://tests/house09_surfaces_test.gd`
5. `res://tests/house09_ceiling_test.gd`
6. `res://tests/frame07_joinery_test.gd`
7. `res://tests/house07_joints_test.gd`

The v2 finish test loads seeds 1, 2, 17, 42, 777, 1988 and 2147483646. It
checks exact anchor/light correspondence, finite spotlight orientation, the
32-light Compatibility ceiling and intersections between rendered wall
finishes/baseboards and declared `stair_side` clearance volumes.

## Native capture

Run `res://../work/environment091_v2_capture.gd` at 1920×1080 with
`--rendering-method gl_compatibility`, passing an absolute `--output` and a
canonical `--map-id=house-v2-<seed>` after `--`.

The probe derives views from `stair_connections` and `circulation_routes`.
For each flight it captures bottom-up and top-down views; for each floor it
captures the central route and four lateral stair routes. The manifest records
camera/target coordinates, image hashes, renderer and lighting budget. Capture
success leaves `visual_review_status` pending.

Review the images for:

- continuous tread, nosing, floor and ceiling edges while looking up/down;
- baseboards and cornices staying outside the declared lateral clearance;
- no light leaking through slabs or walls, acne, detached shadows or abrupt
  dark zones at both landings;
- readable materials without flicker at grazing angles;
- useful lateral circulation around both sides of both stair openings.

## Comparable performance cases

Use the existing `res://tests/performance07_live.gd` fixture, source mode,
1080p internal resolution, unlimited FPS, VSync off, Dummy audio,
`--rendering-method gl_compatibility`, `--map-id=house-v2-1`, twelve measured
seconds after two seconds warm-up and `--natural-doors`:

- population 2, shadows 2;
- population 16, shadows 2;
- population 16, shadows 0 as a diagnostic only.

The earlier rc.1 measurements used `house-v1-1` and different source. Compare
the same fields (frame/render CPU/render GPU/physics percentiles, draw calls,
primitives and frames over 16.67 ms), while reporting the changed map identity,
source hashes and hardware. Do not present the delta as a controlled causal
saving or as GTX 1660 Ti certification.
