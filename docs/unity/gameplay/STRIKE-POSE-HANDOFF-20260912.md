# Crouched strike and arm chain fix

Runtime scope: only `Presentation/Gameplay/ActorVisualBinding.cs`, based on the
central `fd1b9c8` bite fix. Authority/input changes are separate commit `9058a5d`.
VisualAttentionRig, camera/presenter/menu, FBX, controllers, colliders and hit
queries were not edited.

While the human has CrouchFraction > .25, strike selection keeps Crouch id3
through windup, active stroke and recovery. StrikeStarted no longer overlays
standing full-body Swat id12 on that pose. Entering crouch cancels a pending
standing Swat temporal clip; standing strikes retain Swat. This preserves the
existing lower-body animation instead of adding a new asset or animation graph.
The strike remains visible because its arm follows the authoritative moving
forearm/wrist target through the two-segment solve below.

The former isolated `hand.position = target` is replaced by UpperArm/LowerArm
rotations about their real joints. Each solve measures actual world segment
lengths, clamps the target between the minimum/maximum reachable distances and
uses the authored elbow projected onto the shoulder-to-target plane for its
bend direction. A collinear pose uses actor-relative anatomical fallbacks.
Bone local offsets/scales and Hand.localRotation remain authored. No stretching,
copying of proxy shoulder positions, or direct wrist translation occurs.

Modelador Humanos confirmed the joints3 chain is direct
Shoulder→UpperArm→LowerArm→Hand (Socket.Grip and fingers below Hand), with source
lengths approximately .270185 + .230217 = .500403 m. His recommendation to preserve
wrist **local** rotation, rather than indiscriminate world rotation, is applied.
Runtime still measures actual imported lengths instead of hardcoding those values.

## Verification and remaining scope

Six native synchronous checks are prepared in
`docs/unity/gameplay/validation/attention/StrikePoseChecks.cs`:

1. Both arms at multiple actor yaws reach within range without changing offsets.
2. Far/near/at-shoulder targets clamp without stretching segments.
3. Authored wrist local rotation and bone scale are preserved.
4. Crouch remains the base motion through all strike phases and after recovery.
5. Crouch entry cancels a standing Swat temporal clip.
6. Standing retains Swat and no-strike state skips arm IK.

Patched adapter plus all six checks compiled **0 errors, 0 warnings** against
Unity 6000.3.24f1 and central imported DLLs. Evidence/source copies/hashes:
`N:/LetMeSleep/Validation/StrikePose-20260912/PatchedSources-20260913-005236-736/`.
**These six checks were not executed by this worker.** No Unity/native/GPU slot
was used. The 104 CPU PASS cited in the input handoff belong to the separate
authority/input change, not to this visual IK change.

After import, Director can run:

```powershell
& N:/LetMeSleep/Worktrees/gameplay/docs/unity/gameplay/validation/attention/Compile-StrikeChecks.ps1 -Mode Integrated
```

Execute the generated `run-in-director-slot.cs` in the assigned slot and save its
JSON. It creates/destroys its own synthetic proxy and bones, and invokes adapter
methods synchronously. It does not check full animation or player input. Do not
load the default PatchedSources DLL into Unity; it is only an offline compile.

Native review still needs the actual human mesh and clips: Ctrl held before a
strike and pressed during one, continuous recovery while crouched, both hands,
flyswatter grip, actor rotation, first-/third-person and remote interpolation.
Inspect entry blends as well as settled crouch. Verify pelvis stays low and the
arm does not flip or intersect the camera/body throughout the full stroke.

A target outside the actual arm's reach intentionally remains visually clamped;
authority hit queries remain unchanged. If that happens frequently, Director and
Humanos must calibrate visual shoulder/strike reach together. This patch does not
claim every physical hit target can be touched by an arm with different proportions.
It is not a ragdoll or facial/blink change. Existing bite normal orientation,
Head/Neck locking and final-contact sampling are preserved.
