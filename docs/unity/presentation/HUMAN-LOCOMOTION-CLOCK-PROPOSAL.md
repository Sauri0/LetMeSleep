# Common human locomotion/contact clock

Proposal for the next coordinated batch, 2026-09-12. No runtime or clip edits in this batch; no Unity execution. Gameplay owns ActorVisualBinding, Humanos owns authored gait data, Presentation owns clock/audio integration after ownership is released.

## API contract

One non-networked clock per visual instance, not per reusable actor ID. Proposed C# surface (types below are design, not implemented):

```csharp
void Configure(GaitProfile walk, GaitProfile run);
void Reset(ResetReason reason, RenderPose baseline);
void Suspend();
LocomotionSample Advance(in LocomotionInput input);
bool TryConsumeContact(out FootContact contact);
```

- `GaitProfile`: revision/hash, clip ID, source duration, supported speed range, distance per full cycle in world metres at scale 1, left/right contact phases and stance windows. Humanos must supply/validate them. Two contacts per cycle are a current source property, not a universal assumption.
- `RenderPose`: position, orientation, render time and visual-instance generation. It is the interpolated base actor pose, before procedural body corrections.
- `LocomotionInput`: RenderPose, frame ID, render delta, effective grounded/active flags and selected/blended gait. Use one timeline for pose and flags; remote flags change at the interpolation boundary corresponding to that pose. No new protocol fields.
- `LocomotionSample`: generation, monotonically increasing cycle phase, wrapped phase, cycles/second, walk/run blend and clip sample times. The animation consumer receives the exact sample used to identify contacts.
- `FootContact`: generation + monotonic contact index, foot, crossing time within this render step and world position resolved from that foot after animation evaluation. Identity permits deduplication; draining is destructive. No event is created merely by Configure/Reset/Suspend.

`Advance` is idempotent for a repeated frame ID. Integrate valid horizontal rendered displacement as `phase += ds / D(effectiveSpeed)`; effective speed uses that same displacement/time, with bounded filtering if needed. Preserve accumulated phase when blending gaits. Do not multiply old authoritative MotionPhase by a speed-dependent ratio: changing that ratio causes phase jumps. Keep one pending contact maximum; crossings skipped by gaps/cuts are discarded, never replayed.

## Update ownership and animation order

ActorVisualBinding is the proposed caller. In its single LateUpdate, after selecting/interpolating/extrapolating the visual base position:

1. Resolve valid base pose and matching locomotion flags, then call clock.Advance exactly once. Local and remote paths use the same rule. Freeze the clock when bounded extrapolation stops moving the visual.
2. Evaluate the locomotion pose from the returned phase. Walk/run crossfade uses one phase with authored foot alignment and separate sample time `phase * clipDuration` for each clip. Effective playback is `clipDuration * cyclesPerSecond` if advancing a playable between samples; explicit sampling and free advance must not both run.
3. Complete existing hand/anchor work and publish the finalized foot contacts. Audio drains after visual evaluation; facial final evaluation remains in its established later order.

This requires an explicit single owner of the human base locomotion pose. Simply setting Animator.speed in LateUpdate does not update the already evaluated current-frame skeleton. Proposed implementation is a manually evaluated locomotion playable/mixer while gait owns the base pose, with a defined handoff to existing non-locomotion actions. It must prevent the regular controller and manual graph from both writing the same base pose. Do not add Animator.Update calls to the existing automatic controller as a shortcut. The exact graph/controller handoff is part of the next implementation review; it is not claimed solved here.

Remove human resynchronization to authoritative MotionPhase for these gait states, and replace the current Run threshold >2.45 m/s with gait-profile selection/hysteresis agreed with Humanos. Neither authority movement nor network state changes. Do not interfere with Gameplay's mosquito surface/bite work.

## Audio consumer

GameplayAudioPresenter retains gameplay one-shots and material/cue selection. Its human footstep branch must stop using the separate snapshot FootstepCadence accumulator when the common clock is installed. Register/unregister the actual visual-instance clock on visual creation/destruction; missing clock is silent with a diagnostic, not a simultaneous fallback.

After pose evaluation, consume each eligible left/right contact once, locate the corresponding final foot anchor, choose ground material and play its cue at pitch 0.98–1.02. Suppress gait contacts during a landing one-shot or non-locomotion action. Do not subscribe to both clips' animation events during a crossfade. For sub-frame timing, contact crossing time can inform DSP scheduling, but late contacts are not queued for catch-up. The source clip's audible attack offset must be measured before claiming acoustic synchronization.

The prior 0.28 s audio guard was a mitigation, not an immutable contract: any guard in this integration must derive from the accepted gait profile, or it would drop legitimate faster contacts proposed by Humanos.

## Reset and pause rules

| Condition | Required behavior |
|---|---|
| Spawn, proxy replacement, new round or identity change | New generation; baseline current pose; empty queue; no initial contact. |
| Teleport, explicit discontinuity, invalid time/pose or long sample gap | Reset at destination; discard pending crossings. Use explicit discontinuity or displacement plausibility, not ActorVisualBinding's existing `cut` flag alone: localActor currently makes that true every snapshot. |
| Pause/disable/no simulation advancement | Suspend; clear pending audio. No phase advancement from wall-clock time. On resume baseline current pose and reset derivative; preserve phase only if identity/pose continuity is confirmed. Otherwise reset generation. |
| Falling, fainted, recovery or action taking the base pose | Suspend gait and discard contacts; do not advance it behind the action. On returning grounded, baseline pose, choose a supported contact-aligned entry phase without emitting an entry sound. |
| Ordinary stop/start or walk/run blend | Preserve phase when continuous, clear stale pending events on stop; interpolate profiles without resetting phase. Only future actual crossings emit. |
| Remote jitter/duplicate frame | Evaluate once per frame; use presented movement. Discard backlog/cut crossings; never burst missed contacts. |

## Current source limits and acceptance gate

Current source Walk: T=1.333 s, foot stance travel 0.20 m, duty 0.62, equivalent no-slip D=0.323 m/cycle. Run: T=0.8 s, stance travel 0.28 m, duty 0.48, D=0.583 m/cycle. Human scale is 1. These are ideal source trajectory calculations, not a native mesh/contact measurement.

At 3.1 m/s and exploratory 2.5 contacts/s, required D=2.48 m/cycle and Walk playback=1.667. At 5 m/s and 3.3 contacts/s, D=3.030 m/cycle and Run playback=1.32. Keeping current duties would demand stance travel 1.538/1.455 m; Humanos reports 0.34+0.32 m leg reach, so these cannot be adopted by multiplying foot offsets. Slowing current clips to those cadences leaves theoretical support drift approximately 2.70/4.04 m/s. Bounded foot IK cannot repair this mismatch.

Therefore cadence targets remain exploratory. Humanos must propose feasible cadence, duty/flight phase, pelvis motion and authored support travel for unchanged gameplay maxima 3.1/5 m/s (partial input/collision can reduce actual speed). New clips or a different accepted cadence are necessary. A shared clock alone earns no no-slip/realism PASS.

Current runtime also selects Run at normal 3.1 m/s. Its playback at 5 m/s is capped at 2.5, yielding 6.25 visual contacts/s locally, while remote resync follows the old authority phase equivalent to 8.33 contacts/s. This divergence must be removed, not used as a target.

Next gate: pure clock checks for frame-rate independence, continuous gait transitions, unique contacts and lifecycle resets; then Director-controlled native capture/listening with final clip hashes, rendered phase/contact IDs, per-foot stance drift at 3.1/5 m/s, stairs and network jitter. Validate contact position, penetration, cadence and audible attack against the same frames. No visual/audio PASS before that evidence.
