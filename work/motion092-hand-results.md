# Hand orientation correction for 0.9.2

Status: runtime pose and carry-envelope validation passed in the coordinated
candidate. Imported bone geometry passed; Modelador 2's visible wrist/mesh
review remains open. No final anatomical visual acceptance is claimed.
This is separate from the completed 0.9.1 movement/attachment delivery at
`fc199c97ce2280180205bc012b9b25931289da88` (Director integration `70bc08c`).

## Demonstrated cause

The selected hand contract has the right thumb medial (wrist X .31, thumb base
X .272) and the left thumb medial (wrist X -.31, thumb base X -.272). With fingers
approximately down, that establishes the anatomical palm on approximately +Z.
Modelador 2 independently confirmed this against the selected source and mesh.
There is no palm-specific material or fingernail marking in the core mesh.

The legacy rig's finger points curve toward -Z, the dorsal side. For example,
right finger 1 runs from (.296,.698981,-.026706) through
(.296,.680097,-.037859) to (.296,.661434,-.051001). The runtime's
`Basis(width,+.10)` increases that dorsal bend. `character_skin.gd` also uses
`width.cross(longitudinal)` as its palm normal, which is the opposite anatomical
direction for this rig. This shared rig/runtime error affects both viewpoints;
camera visibility is not its cause.

That finger has 21.932 mm and 22.825 mm falanges, with its tip 20.000 mm on
the dorsal side of its knuckle before the additional runtime bend.

## Changes owned by Worker 1

- `human_pose.gd`: retains the right-handed rotation frame, exposes anatomical
  `palm_normal` separately, and seats each grasp on the palmar side of the same
  shaft position. Bare hand strikes flex the wrist and orient the palm toward
  the target surface. The palm centre reaches the same resolved target and arm
  reach remains unchanged. `palm_center` supports explicit finger direction and
  preserves the previous two-argument interface. Following the envelope failure
  below, the carry elbow is tucked inward (X offset -.06 instead of +.04),
  rotating the grasp around the unchanged shaft instead of widening the body.
- `emote_pose.gd`: reverses the width cross-product so wave, shrug, celebration
  and yawn expose the intended anatomical palm.
- `locomotion_test.gd`: updates its independent palm position assertion to use
  the articulated wrist direction instead of assuming a straight wrist.
- `emote09_pose_checks.gd`: checks expected palmar directions at full gesture.
- `motion092_hand_test.gd`: checks both sides, multiple surface normals, body
  headings, crouches, reachable/unreachable targets and full strike trajectories.
  Also checks held shaft clearance through throw states. Its imported-skeleton
  section infers palm from thumb handedness and checks actual hand transforms,
  connected falanges, authored lengths and neutral palmar curl in both POVs,
  including every held tool and two partial grasp transitions.
  A subsequent regression addition samples both forearm reservations through
  25 gait phases, walk/run, three crouch amounts and every held tool (750 poses).
- `motion091-run.ps1`: adds bounded modes for these checks.

Director explicitly authorized the additional emote and locomotion files.

## Coupled Modelador 2 change

Do not integrate HumanPose alone: Modelador 2 owns the coordinated correction
in `game/assets/art/characters/shared/character_skin.gd`. Agreed contract:
`frame_normal = width.cross(longitudinal)`, `palm_normal = -frame_normal`;
rotation matrices retain determinant +1. He is correcting the dorsal neutral
curve and replacing stretched grip targets with fixed-length articulated
falanges. No reflected mesh, changed bind rig, shifted shaft or hidden fingers
is part of this correction.

## Validation

Authorized engine block: 2026-09-11 23:48:20–23:49:08 UTC; released to Director
with zero Godot processes. Native Godot 4.5.2, Compatibility/OpenGL, RTX 3060 Ti.

| Check | Initial candidate result |
| --- | --- |
| Hand pose r2 | 62,696 checks, 0 failures, stderr empty |
| Emotes r1 | 31,230 checks, 0 failures, stderr empty |
| Manual defense r1hands092 | 6,897 checks, 0 failures, stderr empty |
| Locomotion r1 | 7,867 checks, 18 failures |

The 18 locomotion failures are repeated states of the right forearm reservation
with newspaper/slipper while standing: contact radial distances exceed the
unchanged .56 m centre limit by approximately 11–19 mm. The carry elbow change
described above was prepared in response. The subsequent coordinated rerun
below validates that correction; the initial results alone do not.

Hand pose r1 failed to parse because constant `Skin` shadowed Godot's native
class. Renamed to `CharacterSkin`; r2 completed. Both logs are retained. The r2
pose-only report serialized an unused imported metric as `inf`; the subsequent
coordinated run verifies the correction to null for unexecuted imported metrics.

## Coordinated validation after the elbow correction

Modelador 2 integrated `b138a99` and `e761261` into his character worktree and
ran the gates in his authorized block on 2026-09-11. Worker 1 inspected the
result artifacts and did not repeat the engine runs. Copies of the original
reports, stage metadata and logs are in `work/motion092-joint-validation/`.

| Native check | Checks | Failures | Result details |
| --- | ---: | ---: | --- |
| Updated hand pose + 750 carry poses | 64,196 | 0 | Maximum occupied reservation radius .582236507 m, below unchanged .60 m body radius |
| Locomotion | 7,867 | 0 | All 18 original carry-envelope failures resolved |
| Worker imported-bone test, before gait addition | 63,348 | 0 | Maximum digit length error 5.59e-9 m; palm contact error 3.73e-8 m |
| Modelador 2 ten-digit fixture | 36,018 | 0 | Both POVs, both palm strikes, five tools and animated transitions |

All four stages exited 0 with zero stderr. The final envelope and locomotion
stages ran 23:57:37–23:57:46 UTC. No radius, attachment offset, strike reach or
failing envelope assertion was relaxed. Unexecuted skin metrics in pose-only
output are now null, with valid JSON.

The bone results apply to Modelador 2's tested candidate skin, which is not yet
sealed as visually accepted. His captures still show shading/silhouette issues
near the wrists in rest/grasp, including without shadows and at full mesh
detail. That mesh/normal investigation remains with Modelador 2. The successful
bone constraints must not be presented as a final mesh or visual PASS.

Worker 1 has no pending engine run unless runtime code changes again.
