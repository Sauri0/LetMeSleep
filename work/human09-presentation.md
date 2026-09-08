# Remote crouch presentation

Status: runtime frozen and integrated by Root in commit `55b4e5c`; parse and
focused/regression tests passed. Full native before/after garment recording is
owned by Visual and remains separate from this technical gate.

## Observed defect

The continuous studio recording in
`outputs/0.9-garment-continuous-final1/report.json` advances the real authority
at 60 Hz, publishes actor data at 20 Hz and records at 30 Hz. Frames 228 to 229
show authoritative `crouch_amount` 0.9 to 0.6: a 165 mm head displacement,
135 mm shoulder displacement and 151.74 mm maximum upper-garment vertex step.
The old renderer applied each full public posture directly; only its root
position was interpolated.

## Deliberately limited policy

`HumanPresentation` stores one displayed crouch value per ActorView. Between
public snapshots it moves toward the latest received amount at at most 6 units
per second, the existing authority transition rate. It does not infer intent
from `crouching`, predict future posture, change simulation state or interpolate
aim, gesture time, strike progress, tool contacts or root position.

The filter applies only to remote, noncritical human presentation. These cases
use the unmodified latest public pose immediately:

- Local first-person human.
- The human referenced by this mosquito's already-private assignment.
- Actual bite, active focus threat, melee swing/strike, throwing or emote.
- Dead/non-human state, new actor, role replacement, teleport, time reset,
  equipment change or static pose without a public `pose_time`.

Client passes the private target ID into World before that frame's
`ActorView.update_state`. It is not published or inferred from hidden state.
Camera, marker and attack ray code are unchanged. Separate collider caching
always samples the raw authoritative dictionary, including in noncritical
views where the cosmetic crouch is still catching up. Body mesh and colliders
remain exactly aligned in the critical cases above.

This is **not a solution for every visible transition**. In a 1-human/1-mosquito
game an assignment is often continuously active, so that target remains exact
at the received snapshot cadence. Smoothing this critical case coherently would
require a broader shared camera/marker/contact presentation contract.

## Validation prepared

`game/tests/human09_presentation_test.gd` covers rate bounds at 30/60/120 Hz,
20 Hz received posture, no extrapolation, exact critical entry and bounded
exit, unchanged other fields, static/time/teleport resets, actual ActorView
mesh and collider samples, FPS eye alignment and same-frame World target hook.
A paired authority simulation also compares every public snapshot and both
private channels while one side feeds the real ActorView.

No source authority, HumanPose, body dimensions, map geometry, input cadence or
network payload is modified. Native garment displacement remains a measured
diagnostic; source-only checks cannot establish aesthetic approval.

## Executed gates

- Parse: clean.
- `human09_presentation_test.gd`: **1,457/1,457 PASS**, 2.180 s, exit 0,
  empty stderr. Real 60 Hz authority with 20 Hz published pose produced a
  maximum displayed head step of **55.036 mm per 60 Hz tick** during standing
  up. This differs from the native baseline's 165 mm per-snapshot head jump;
  it is not a claim about the final 30 Hz video or full mesh yet.
- Unchanged `v07_character_cache_checks.gd`: **8/8 PASS**, 1.348 s, exit 0,
  empty stderr; equal-value caching, hash collision and deep-copy behavior.
- Unchanged `selected07_actor_checks.gd`: **537/537 PASS**, 0.917 s, exit 0,
  empty stderr; exact collider position/radius/height across postures, selected
  facial/material integration and existing mosquito orientations.

Evidence: `work/human09-presentation.json/.log/.err/.run.json`,
`work/human09-v07_character_cache_checks.log/.err/.run.json`,
`work/human09-selected07_actor_checks.log/.err/.run.json`.

Runtime handoff: `game/scripts/human_presentation.gd`,
`game/scripts/actor_view.gd`, the optional argument and pre-update hook in
`game/scripts/world.gd`, and its private-target argument in
`game/scripts/client.gd`. New fixture: `game/tests/human09_presentation_test.gd`.
No existing test expectations were changed.
