# Bite contact fix — 2026-09-12

Base central `5ceb85a7649d673938b759c0de8a4c0af0245ee5`, branch
`codex/gameplay-bite-contact`. Director assigned temporary ownership of
ActorVisualBinding.cs and VisualAttentionRig.cs. No other runtime, assets,
collision, input, camera or online contracts were changed.

## Behavior

For a valid PreparingBite/Biting attachment, the mosquito's visual root faces
the negative world surface normal. Its up vector is projected from its previous
visual orientation, with world-up/world-forward fallbacks for vertical contacts.
The body frame is independent of view yaw while attached. The existing translation
then closes the actual animated mouth to the authoritative contact point. Flight
after detach uses the existing exponential visual rotation release.

`VisualAttentionRig.SetHeadTrackingEnabled(false)` restores only its own Head/Neck
offsets and clears their smoothing state. During contact the facial writer skips
those joints, while pupil tracking, microsaccades and blink continue. Reenabling
tracking starts at zero correction and uses the existing exponential response.
Invalid attachment, absent tip, detach or disabled binding release the restriction.
The public target and reduced-motion contracts are unchanged.

`AfterEvaluation` fires after the facial joints/lids have been written. The contact
adapter uses it to refresh the copied mouth anchor and measure final residual in
the same frame; without an active/configured facial rig it measures immediately.
It unsubscribes when disabled. This event observes contact; it does not apply a
second corrective transform.

The existing `LMS_BITE_VISUAL_OFFSET` threshold (0.15 m) and one-warning-per-instance
behavior remain intact. Its value is still pre-translation, now after the corrected
body frame. Separate `LMS_BITE_CONTACT` records the first completed sample per
attachment episode, with preCorrection/finalResidual/frame. A new
`LMS_BITE_VISUAL_RESIDUAL` error identifies final separation above 1 mm, once per
episode. Read `LastBitePreCorrectionMeters`, `LastBiteFinalResidualMeters` and
`LastBiteSampleFrame` for every completed sample; frame -1 means no current sample.

## Checks prepared and compiled

`BiteContactChecks.cs` contains six native checks against a synthetic rig and
real adapter/facial components, with synchronous callbacks:

1. Yaw changes after attachment preserve mouth contact and authoritative pose/view.
2. Surface normals, translated/rotated victim poses and matching revisions follow.
3. Head/Neck stay fixed after alignment while pupils and blink continue; reenabling
   tracking supplies a negative control that must detect contact separation.
4. Detach restores head tracking and body tilt progressively and clears the sample.
5. Stale victim PoseRevision releases contact tracking and diagnostics.
6. Disable releases tracking and unsubscribes from facial completion.

The two patched runtime files, the unchanged MenuReactionPolicy dependency, and
all six checks compiled offline against Unity 6000.3.24f1 and central gameplay/
character DLLs: **0 errors, 0 warnings**. Evidence:
`N:/LetMeSleep/Validation/BiteContact-20260912/PatchedSources-20260913-003633-491/`.
That directory contains immutable source copies, project, log and SHA receipt.

**None of the six checks has been executed in Unity by this worker.** No native,
editor/player launch, GPU, render or audio slot was used. Do not label compile as
test PASS. No new Unity assets were introduced, so no new .meta files are needed.

After Director integrates/imports, generate a test-only assembly against the real
central runtime with:

```powershell
& N:/LetMeSleep/Worktrees/gameplay/docs/unity/gameplay/validation/attention/Compile-BiteChecks.ps1 -Mode Integrated
```

Only that Integrated output is suitable for Director's existing eval_file/native
slot. Execute its `run-in-director-slot.cs` once and save the returned JSON; the
six synchronous cases create/destroy their own synthetic world, not a game session.
The entry now distinguishes `RunAll` (requires Play Mode; automatic OnDisable)
from `RunAllManualEditMode` (explicitly invokes OnDisable, reports manual cleanup
scope). The generated snippet selects the matching entry and the JSON labels it.
Director's original central fd1b9c8 Edit Mode run reported five PASS and failure
of `disable_releases_tracking_and_unsubscribes`: that harness wrongly assumed
automatic MonoBehaviour disable callbacks in Edit Mode. Preserve that original
result. The manual cleanup check cannot certify automatic lifecycle; Play Mode
validation remains required. No runtime cleanup was changed for this harness fix.
The default PatchedSources artifact is an offline compilation check, not a runtime
plugin; do not load it into the central editor.

Synthetic checks do not establish imported R4 clip/blend behavior, visible leg
placement, actual frame scheduling, WAN or player feel. Follow with Director's
live R4 test: side/top bite, held yaw rotation, moving/crouching victim, full
entry/loop/detach, local and remote. Observe final mouth residual and the visual
body/collider relationship. The frame rotates the whole visual, including legs;
it does not move physical anchors or change collision. If a distinct leg contact
pose is required, coordinate that art contract instead of changing gameplay.
