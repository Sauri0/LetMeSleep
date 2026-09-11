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

After import, on Godot 4.5.2 Compatibility:

- `environment091_stair_lighting_test`: 16 checks, 0 failures. It constructed
  the full 32-light v2 budget, all four stair lights, and vertical aim bases.
- `house09_surfaces_test`: 707 checks, 0 failures.
- `house09_ceiling_test`: 3 maps, 0 failures.
- `frame07_joinery_test`: 992 checks, 0 failures; overlap 0, normal errors 0.
- `house07_joints_test`: 603 checks, 0 failures.

The first successful lighting test deliberately constructed a 33-light
negative runtime case and therefore wrote a warning to stderr. The final test
uses the shared pure budget calculation for that negative case instead. The
two v1 regressions also observed the historical 33-light warning before the
final conditional warning change. The final helper extraction and clean
stderr behavior still require a short rerun after integration or a new motor
turn.

No captures, build, export or performance claim were made in this tranche.
Raw local logs are under `work/environment091-lighting-run/`.
