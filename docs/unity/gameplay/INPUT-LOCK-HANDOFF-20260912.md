# Incapacitated control gate

Base `fd1b9c8`, branch `codex/gameplay-input-strike`. Runtime edits limited to
GameplayAuthority.cs and GameplayRuntime.cs; the strike/arm work is separate.

Falling, Fainted, Stunned and Recovering continue accepting valid input sequence
acknowledgements and enforcing ownership, view revision, numeric/direction and
rate validation. They no longer apply incoming Yaw/Pitch/Aim or retain movement/
held actions. State transitions into incapacity and back to control clear held
input. This gate does not write physical transforms or stop falling integration.

Local snapshot handling synchronizes the frozen view, clears pending actions and
held input. Mouse polling and sending gameplay commands remain neutral throughout
incapacity. Recovery requires release of gameplay buttons before a fresh input;
mouse deltas during the blocked/rearm period are discarded. Pause/UI remains
independent: the new gate does not call SetInputBlocked or change cursor policy.
No wire schema, life-state names, hit queries or ragdoll implementation changed.

Validation: existing CPU suite plus 10 new cases in IncapacitatedInputChecks.cs
passed **104/104**, with adapter compilation **0 errors / 0 warnings**.
The eight parameterized cases each cover both species across all four states,
using authenticated remote-style and host-bot submission, then fresh input after
resume. Additional cases preserve rejection/rate/ack behavior and ensure entry
clears an already-held command. Falling still reaches motor integration with
gravity. State setup invokes the real transition helper through reflection;
FakeWorld is not a physics or mouse test. Existing combat/recovery tests also ran.

Evidence and reproduction project/receipt:
`N:/LetMeSleep/Validation/IncapacitatedInput-20260912-v1/`.
No Unity/native execution was performed by this worker.

Director's remaining local check: in each incapacity state move mouse, hold W/E/R/
Primary, open/close pause, lose/regain focus, then recover. Orientation must remain
frozen while incapacitated, held controls must stay neutral until released, and a
fresh press/mouse movement must work after release. Confirm a new round starts
normally and inspect remote view. This does not certify the future articulated
ragdoll, which must continue receiving physical rotation independently of input.

Separate harness correction `ef6a28a` distinguishes automatic OnDisable in
Play Mode from manual cleanup in Edit Mode. Director later ran the original
six bite checks successfully in Play Mode; evidence reported at
`N:/LetMeSleep/Validation/alfa3-corrections-20260913/bite-playmode-result.json`.
The earlier Edit Mode failure remains historical evidence, not a runtime defect.
