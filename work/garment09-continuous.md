# Continuous garment witness — fit4 findings

No artifact or runtime source is changed by this test. The new fixture drives
three isolated real Simulation instances, then their public snapshots and
ActorView/HumanPose implementations. The initial positions and equipment are
prepared. The visible stage is a studio, not a rendered gameplay map.

The 21-second sequence has 630 recorded frames at30Hz. Simulation and ActorView
advance sequentially at60Hz; public snapshots are published at20Hz, as in the
observer path. There is one setup per actor, with no per-pose reset.

- Columns: outfit0/hands, outfit1/racket, outfit2/broom.
- Walk, stop, crouch, look down to−1.92rad, turn through−75°/+75°, breathe,
  stand and return to front.
- Four authority gestures: wave, celebrate, shrug and yawn. Their accepted
  start times, natural completion times and root drift are recorded.
- Actual upper-garment vertices are baked after the rendered frame. Per-frame
  maximum displacement is measured in actor-local coordinates. This is a
  diagnostic for locating visible jumps, not an arbitrary aesthetic threshold.
- Current source hashes must exist and remain unchanged. This runner is
  source-only; it does not silently certify stripped packed scripts.
- Dummy audio driver; no microphone or voice provider. The resulting video
  intentionally has no audio stream.

Commands, only in a coordinated motor/CPU window:

    python work/garment09-continuous-run.py --smoke --output outputs/0.9-garment-continuous-smoke1
    python work/garment09-continuous-run.py --output outputs/0.9-garment-continuous-final1 --native-timeout 180

The default native guard is55 seconds; the parent authorized180 seconds for
this uninterrupted full recording. The runner preserves existing output
directories, checks the native report and sequential PNG set, encodes those
exact frames at30Hz using FFmpeg, then verifies frame count, duration and no
audio using ffprobe. It does not interpolate frames or change playback speed.

Native full run completed:630 PNG /21 seconds,4,483 functional checks and zero
failures, with empty stderr. Parse1.25s, native76.54s, encode3.65s. All twelve
authority gestures completed for their exact catalog durations; root drift
was zero. FFprobe confirms630 frames,21seconds and no audio. Source hashes
stayed identical, including human GLB88db54d28af6a4225179c51c3b5179e6ae493a51357d51a872d8e13f0841ebac.

Evidence: `outputs/0.9-garment-continuous-final1/human-fit4-continuous.mp4`,
the630 original PNG in `frames/`, native `report.json` and runner manifest.
This is evidence with findings, not visual approval. Inspected still witnesses
include59,228,229,278,390,480 and550 plus the preserved source-before57.

1. Crouch presentation has discrete jumps. Frames228→229 (7.60→7.633s)
   receive public crouch_amount0.9→0.6. The actual head rises165mm and shoulder
   rises135mm; upper garment's maximum vertex displacement is151.74mm.
   ActorView samples the new HumanPose directly and holds it between20Hz
   publications. This is a pose transition shared by the garment, not an
   isolated vertex spike. The similar crouch entrance maximum is149.29mm.
2. The central light-grey fragment is the authored secondary tube in each
   human_outfit_*_trim. Read-only component/ray audit in
   `garment09-continuous-seam-audit.py/json` finds144 vertices per component,
   88 behind and56 in front of actual cloth triangles in both source-before
   and fit4 bind geometry. Signed front depth spans−21.79..+18.34mm in fit4
   (before−21.79..+18.11mm). It is a partially buried/raised trim path, not an
   accidental inner cloth face. Dynamic depth has not been inferred from this
   bind-space measurement.
3. High/angular shoulder contours precede fit4 (before-exact human57-front).
   Lowering the neckline makes its broad horizontal extent more apparent.
   At frame390 celebrate, the torso/sleeve transition still forms pronounced
   corners. No art change or subjective approval follows from the numeric
   face-contact closure. The earlier285-pose proof did not cover these emotes.

The three-column front camera is for neck/shoulder continuity; a hand can reach
the side boundary during wide gestures and the top of the broom is cropped.
It is not a full-body/tool-framing gate or exhaustive cosmetic review.
No finite-pose contact result is extended to all continuous poses, all tools
and clothing combinations, or overall gameplay/FPS.
