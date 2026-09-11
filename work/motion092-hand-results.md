# Hand orientation correction for 0.9.2

Status: coordinated candidate; initial pose checks passed, carry-envelope
correction and imported-skin validation await the next shared engine slot.
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
described above is prepared in response. It still needs a rerun; the three
initial PASS results are not evidence for that later posture adjustment.

Hand pose r1 failed to parse because constant `Skin` shadowed Godot's native
class. Renamed to `CharacterSkin`; r2 completed. Both logs are retained. The r2
pose-only report serialized an unused imported metric as `inf`; the test now
reports unexecuted imported metrics as null, pending its next run.

Imported finger checks require the coordinated skin correction; no imported
skin, anatomical mesh or visual PASS is claimed here. The existing body radius,
attachment offset, strike reach and failing envelope assertion were preserved.
No engine was launched while Modelador 2, QA or Director held the shared slot.
