# Human locomotion clock — implementation handoff

## Implemented seam update (supersedes manual installation steps below)

The following integration is now implemented as a delta based on Gameplay's released final wrist Binding `6de665324c016abc0a864527e3ab08b16b9bd6e8`. Hand/tool solver code is preserved. The original handoff below remains implementation background, not additional installation work.

- CharacterContentBuilder retains original human IDs0..14. With a complete source audit containing all four named loops, it appends WalkSlow/Trot at15/16 and connects actual imported clips by exact name. Old audit with neither new clip retains the old controller; partial new export fails explicitly. No character geometry/material edits.
- HumanLocomotionSetup only creates a presenter when Human_WalkSlow, Human_Walk, Human_Trot and Human_Run all resolve to unique, nonlegacy, positive-duration looping references, plus owned foot source bones. Missing/partial clips create no graph and do not silence legacy footsteps. Runtime names are a completeness gate; Director still verifies final source JSON/hashes, not a claim of automatic source certification.
- GameplayVisualPresenter binds that presenter to ActorVisualBinding and registers its audio source; destruction/replacement unregisters. GameplayPresentationRoot binds audio before creating/registering visuals. RegisterLocomotion now selects the shared stream **per actor**, not globally. The explicit global EnableSharedHumanLocomotionAudio API remains available but is not called by this seam. Thus a mixed old/new first-person or remote prefab set cannot silence the old instances.
- Binding bypasses old gait Play/Speed/Sync only for eligible configured humans, releases ownership before action/temporary/recovery/crouch/idle, and reapplies controller state. It evaluates gait after the base visual position, before hands, then publishes once after anchors. Landing frame suppresses foot contacts.
- Configured human local visuals now use the existing bounded interpolation/extrapolation path as remotes do; current local snapshot placement at30Hz would otherwise look like a teleport-speed displacement to a144Hz render clock. This adds the existing visual interpolation delay, at most the bounded snapshot interval, to those local visuals. Authority, input and proxy movement are unchanged; native camera/hand feel must be checked. Old humans and mosquito placement stay on their original paths.
- Pause uses Time.deltaTime; cut is actual ShouldCut/first-state, not the old always-true local flag. No graph is acquired without valid clips. Scene/prefab generation and native validation remain Director-owned.

Verification: all Presentation/Gameplay source including the released final wrist Binding and seam compiles offline with zero warnings/errors. Central Gameplay source was separately compiled for this check because its existing Unity DLL had not yet exposed the final ToolOffset method; no gameplay source was changed. CharacterContentBuilder is compiled separately. The pure clock frequency/reset suite remains the evidence for algorithm behavior; no graph/controller takeover, actual first-person interpolation, sound, meshes or G4 PASS is claimed.

Candidate scope is functional cadence/contact/audio correction only. No new silhouette, materials, aesthetic redesign or Higgsfield artifacts were produced. Current blocker for activating the route is the complete imported functional clip set, followed by native action/locomotion/camera/audio checks. Future visual redesign is separate.

2026-09-12. Source implementation, not installed in ActorVisualBinding. Director requested that file remain owned by Gameplay during hit calibration. No Unity execution; G4 remains OPEN for final mesh/pose/contact/audio evidence.

## Delivered

`HumanLocomotionClock` is a pure C# per-instance phase integrator. `HumanLocomotionPresenter` samples explicitly configured clips with one manual AnimationMixerPlayable. GameplayAudioPresenter can register that presenter's single contact stream and disable its old snapshot cadence. No Authority, protocol, clips, generated assets or ActorVisualBinding changed.

Clock profiles accept any nonempty speed-ordered set. They store actual imported clip duration and world metres per complete cycle. L contact = integer phase, R = integer + 0.5; the initial L pose is a silent baseline. Contact identity is generation/index. Consumption is destructive; a repeated frame cannot create another contact. Speed selects adjacent profiles with linear blend, preserving accumulated phase. This is phase-aligned blending by speed, not a claimed polished temporal transition into idle or actions.

## Explicit profile configuration

Humanos' preliminary four-profile proposal (confirm against final exported JSON before enabling):

| Explicit clip | Nominal speed m/s | D m/full cycle | Contacts/s |
|---|---:|---:|---:|
| Human_WalkSlow | 1 | 0.8333333333 | 2.4 |
| Human_Walk | 1.55 | 0.96875 | 3.2 |
| Human_Trot | 3.1 | 1.55 | 4 |
| Human_Run | 5 | 2.1739130435 | 4.6 |

Use `HumanLocomotionPresenter.GaitClip { Clip, Speed, DistancePerCycle }` for each verified reference. Do not bind the old Human_Walk to Trot's D. `Configure` reads `Clip.length`; nominal .5/.434783 periods are not assumed to be file duration. Each sample time is `frac(phase) * Clip.length`; manual clip speed is zero so it cannot advance a second time. Equivalent playback is `Clip.length * cyclesPerSecond`.

The revision string records the caller's verified source/profile identity; it does not hash FBX data or certify clips automatically. Director must check the source JSON/imported references first. Partial-input speeds use the same profiles with slower phase; crouch is currently excluded until its authored stance is approved.

## Director seam — after Gameplay releases Binding

Create and configure one presenter on each human visual, including first-person and remote visuals:

```csharp
locomotion.Configure(actorId, view.Animator, verifiedGaits,
    leftFootAnchor, rightFootAnchor, verifiedProfileRevision);
audio.EnableSharedHumanLocomotionAudio(); // before first snapshot subscription
audio.RegisterLocomotion(locomotion);
```

Animator and distinct foot anchors must belong to that visual. Registration is idempotent; replacing an actor's source unsubscribes its previous instance. EnableSharedHumanLocomotionAudio is sticky: missing/unregistered instances remain silent rather than reverting to the old snapshot accumulator. This opt-in keeps central behavior unchanged until the seam/new clips are installed; after opt-in there is only one footstep producer. Existing jump/landing and other cues remain.

In ActorVisualBinding, resolve eligibility from the actual current action, not solely motion ID: human, grounded, Active, moving, not crouching, StrikeState.None and no temporary/base-pose action. Stop gait before setting up an action. Example ordering:

```csharp
// Before old ApplyMotion/Animator writes when leaving gait:
if (!eligible) locomotion.Suspend();
// Reapply the normal action after ownership returns, even if the old cached ID matches.
// While eligible, bypass old human ApplyAnimatorSpeed/PlayMotion/SynchronizeLoopPhase.

// In LateUpdate, after the base visual position is interpolated/extrapolated:
locomotion.EvaluateRenderedPose(finalBasePosition, eligible,
    actualDiscontinuity, Time.frameCount, Time.deltaTime);
// Existing hands/solver and foot-anchor refresh follow.
locomotion.PublishContacts(Time.frameCount, suppress: landedThisFrame);
```

Use scaled simulation delta so timeScale=0 suspends. If the game pauses independently of timeScale, explicitly Suspend and suppress eligible evaluation until resumed. Use the final interpolated base position, before cosmetic corrections; do not integrate the snapshot position or authoritative MotionPhase. Remote eligibility should match the presented interpolation boundary. Audio additionally suppresses contacts if the latest authority state is inactive/airborne/striking; this conservative guard can drop a delayed remote contact and needs native timeline validation.

Do not forward the old `cut` flag verbatim: it is true every snapshot for localActor. Flag actual teleports/identity changes. Configure/recreate on new visual instances/round identities. For a discontinuity while reusing the instance, pass discontinuity=true or Suspend, then let the next eligible frame establish a silent baseline.

The presenter removes runtimeAnimatorController while it owns its manual graph, captures/restores controller, speed, applyRootMotion and culling, and releases on Suspend/disable/destroy. It evaluates once after automatic animation, before hand/facial final writers. Caller must exclusively honor ownership; no foreign controller writer may reassign those fields concurrently. Graph creation failures restore captured state. On release, old controller state/time is not preserved as an action queue: caller must explicitly reapply the desired action. This takeover/handoff must be verified natively before production activation.

Unregister before destroying visuals. Audio Bind/disable clears registrations; re-register live presenters after rebinding/re-enabling. Presenter disable destroys its graph and clears pending contacts. No global object searches or new replication are used.

## Clock resets and contact behavior

- Nonfinite position/time, delta<=0, delta>0.25 s, explicit discontinuity, ineligible state or speed above 1.5 times the largest configured speed suspend/reset. Next valid frame only baselines. The upper bound is a correction gate, not gameplay speed authorization.
- Stop/extrapolation with less than0.05 m/s advances no phase or contact. Resume after explicit Suspend does not integrate movement while paused. Crouch, recovery, jump and strikes should suspend through caller eligibility.
- One frame crossing multiple contacts drops them; there is no backlog. Queue expires at the next new frame. Publish is once per evaluated frame, after refreshed foot anchors, and reads the final corresponding foot position.
- Audio consumes the event immediately at that position with existing cue pitch .98–1.02; no .28 s guard applies to the shared stream, so .25/.217391 intervals are retained. Crossing fraction is exposed for later DSP alignment; the current audio call does not schedule retrospectively. Clip attack latency remains unmeasured.

## Verification and open limits

Pure clock executable uses actual source helper, four profiles and intentionally non-nominal clip lengths. At 15/30/60/144 Hz, each ten-second run emits exactly24/32/40/46 contacts for1/1.55/3.1/5 m/s. Checks cover alternating feet, unique/destructive delivery, repeated-frame idempotency, speed-change phase continuity, real clip-duration sampling, teleport, pause, recovery, long gaps, invalid inputs, same-frame cuts, stop and fresh instances. These are algorithm checks, not performance or sound results.

Offline compile of presenter/clock/actual GameplayAudioPresenter against Unity6000.3.24f1 and central gameplay/audio assemblies: zero errors/warnings. No Unity Test Runner, graph playback, native listening or final FBX was used.

Old clips cannot safely use these profiles: previous Walk/Run support travel is much too short. Humanos' new source proposal is still awaiting imported clips and mesh/kinematic validation. Mixing profiles with different stance duties can move a planted foot during transitions; this implementation adds no foot locking/ground IK. Stairs, abrupt gait changes, controller handoff and audio attack timing remain native gates. Director should capture final pose phase/contact IDs, stance drift/penetration at relevant speeds, action transitions, jitter/teleports and acoustic attacks on the same frames before declaring G4 closed.
