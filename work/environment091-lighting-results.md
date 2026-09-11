# Environment 0.9.1 — stair lighting handoff

Branch: `codex/091-environment`
Base: `59af8ad`

## Runtime change

`world.gd` consumes the v2 `stair_light_anchors` contract directly. Each
entry keeps its authored `p`, `target` and stable `id`; range is bounded to
3.0–7.5 m and the spotlight aims at the target. Vertical targets select a
non-collinear up vector, so `look_at` always produces a finite basis.

When anchors are absent, version 1 metadata falls back to one bounded light
per `stair_connections` entry, derived from `bottom` and `top`. It no longer
uses `half_x - 3`, `z = 0` or one shadow cone spanning the full house height.

The runtime records `generated_stair_lighting` on `map_root`, including
requested/created/dropped anchors, total positional lights, Compatibility cap
and `over_budget`. Anchors are never truncated silently. Explicit v2 metadata
over the cap emits a warning; historical v1 remains diagnostic-only.

## Contract checked with Modelador 1

- `stair_light_anchors: Array[Dictionary]` with `id`, `p`, `target`, `range`.
- One anchor per stair connection / flight, range 7.5 m.
- Production maximum: 22 rooms + 6 horizontal halls + 4 flights = 32 lights
  for a three-storey house.
- Final geometry uses `stair_offset = 3.65`, `room_inset = 7.1` and reserves
  `finish_allowance = 0.06` m per face for environment finishes.

## Godot evidence

Initial execution found the new test's three inferred return types and was
corrected. A second attempt before worktree import could not resolve global
classes (`ActorView` / EOS); that process was terminated by exact PID after
the script failed before `quit`. The required headless editor import then
completed with exit 0. No EOS configuration or credentials were copied.

After import and the final helper extraction, on Godot 4.5.2 Compatibility:

- `environment091_stair_lighting_test`: 15 checks, 0 failures, exit 0 and
  empty stderr. It constructed
  the full 32-light v2 budget, all four stair lights, and vertical aim bases.
- `environment091_v2_finish_test`: 108 checks, 0 failures, exit 0 and empty
  stderr over seven real v2 seeds.
- `frame07_joinery_test`: 992 checks, 0 failures; overlap 0, normal errors 0,
  exit 0 and empty stderr.
- `house07_joints_test`: 603 checks, 0 failures, exit 0 and empty stderr.

The first successful lighting test deliberately constructed a 33-light
negative runtime case and therefore wrote a warning to stderr. The final test
uses the shared pure budget calculation for that negative case instead. The
two v1 regressions also observed the historical 33-light warning before the
final conditional warning change. The final helper extraction and clean
stderr behavior are now verified.

The earlier surface and ceiling numbers in this report came from the original
v1 regression tranche. A later 864/0 and 3/0 attempt against v2 was discarded
because the old fixtures requested rejected v1 maps and silently observed the
authored fallback map. Commit `0572155` migrates those fixtures; the combined
build owns the authoritative rerun.

Native capture and full visual findings are recorded in
`work/environment091-v2-results.md`. No build, export or performance claim is
made. Raw local logs remain uncommitted under the two
`work/environment091-*-run*/` directories.
