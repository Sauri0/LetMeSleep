# Flight buzz and footstep cadence — 2026-09-12

Source correction; native listening and animation-contact comparison remain pending.

## Findings and behavior

- GameplayAudioPresenter previously selected wing loops on Surface, PreparingBite and Biting, in addition to airborne states. It now permits only Flying/ApproachingSurface with neither surface nor bite attachment. ApproachingSurface is the airborne approach, not a perched buzz. Falling, stunned, recovery and all attached poses are silent. Stop retains the emitter's existing 40 ms fade.
- Loop cleanup now happens before rebinding director/runtime, when a round changes or ends, and when the followed proxy changes; existing disable/despawn cleanup remains. Ended snapshots cannot restart buzz.
- No menu/preview wing-loop producer exists in the current source. MainMenuLivingScene and preview attention have no audio emitter calls, including ReducedMenuMotion. No changes to these paths were needed.
- Flight cue volume is 0.35 (approximately -9.1 dB relative to 1); random pitch narrows from 0.92–1.12 to 0.98–1.02. This is a proposed mix adjustment, not a listening PASS. Existing perch/bite assets are retained for compatibility but are not selected by gameplay.
- Prior footsteps consumed every half MotionPhase. Authority advances phase by 3D distance / 1.2 m: nominal walk 3.1 m/s implies 5.17 events/s, run 5 m/s implies 8.33. Vertical stairs also advanced this phase. AudioSource pitch was only 0.94–1.06, with Doppler disabled; it never inherited Animator.speed.
- Footsteps now accumulate horizontal displacement against speed-dependent contact spacing: interval clamp(0.48 - (speed - 1.55) * 0.058, 0.28, 0.55) seconds. They require consecutive grounded samples and Active life state. No animation crossfade events or replicated state are added. Playback pitch is 0.98–1.02; sample data/duration stays unchanged.
- Snapshot gaps over 0.25 s, nonpositive time deltas, nonfinite values and displacement exceeding max(0.15 m, speed * elapsed * 1.5) clear pending distance. Recovery/airborne/first sample also reset it. At most one contact is retained; wall-clock minimum 0.28 s prevents backlog bursts. Landing suppresses a footstep in that snapshot. Small corrections below the displacement gate are indistinguishable from movement.

## Evidence and limitations

Pure C# FootstepCadenceChecks: 129 assertions pass. In ten seconds, walk/run counts at 15 Hz = 25/30, at 30 Hz = 25/33, at 60 Hz = 25/35. Includes speed transitions, recovery, teleport, vertical-only movement, duplicates/out-of-order snapshots, gaps and same-frame backlog.

GameplayAudioPresenter + actual FootstepCadence compile against Unity 6000.3.24f1 and current central gameplay/audio assemblies. AlfaPresentationBuilder separately compiles against current central runtime assemblies. These are offline compilations, not Unity Test Runner or playback.

Audio now intentionally differs from the existing visual half-phase rate. ActorVisualBinding/authority were not changed: matching final slipper contacts and authored animation tempo still needs native capture/listening, especially stairs and sprint. Material remains nearest AudioZone; stair-specific timbre was not invented. Existing source clips may themselves sound unsuitable and were not heard or regenerated in this batch.

## Director integration recipe

1. Integrate only this batch's source/meta/doc files. Let central Unity recompile. No generated asset YAML hand edits.
2. In Director's authorized native slot invoke `LetMeSleep.Presentation.Editor.AlfaPresentationBuilder.Build()` or menu `Tools/Let me sleep/Build Alfa Presentation Library`. This refreshes importers, cues and audio/presentation prefabs and saves assets. It also rebuilds the builder's other generated presentation assets, so run before the final scene generation/integration pass.
3. For a dedicated disposable batch editor only, the exact entry point is `LetMeSleep.Presentation.Editor.AlfaPresentationBuilder.BuildFromCommandLine`; it calls Build then exits the editor. Never invoke that entry point in the user's interactive editor.
4. Confirm `LMS_ALFA_PRESENTATION_LIBRARY_BUILT`; inspect `Assets/LetMeSleep/Audio/Generated/Cues/MosquitoWingLoop.asset`: volume 0.35 and pitch 0.98/1.02. Inspect HumanFootstep, HumanFootstepTile, HumanFootstepCloth pitch 0.98/1.02. Rebuild the central scenes/player through the existing Director pipeline so generated references are included.
5. Native checks: fly→surface→bite→fall→recovery, actor replacement/despawn, new round/results, preview and reduced-motion menu; no lingering buzz. Listen/capture ten seconds each walking/running/stairs and compare audible attacks against final rendered slipper contacts. Test network snapshot jitter, teleport and recovery for bursts. Native sound quality and contact synchronization remain open until these checks.
