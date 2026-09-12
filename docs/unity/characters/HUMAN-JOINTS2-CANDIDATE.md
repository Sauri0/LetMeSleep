# Human joints / seated motion / facial candidate

2026-09-12. **Candidate only. The wrist correction still needs close views and
moving review.** Canonical human reference 9 and the previous menu export remain
unchanged. All candidate files are under
`art_source/unity/characters/candidates/human-menu-joints2/`.

## What changed

- Welded wrist skin now extends beneath the sleeve and shares LowerArm/Hand
  weights. The cuff is a narrower, open fold with a 2 mm wall, not a capped tube.
- The right palm follows the forearm with an authored wrist tilt. The elbow and
  wrist were repositioned together. The left palm points toward the back of the
  seat, avoiding a forced half-turn; its target height is now 0.599 m.
- Sleeve and trouser topology and weights have support loops around elbows and
  knees. Shoulders, breathing, small weight shifts and knee settling move during
  idle. The swat has distinct anticipation/follow-through and continues through
  its midpoint without a velocity stop. Return includes a tired settling motion.
- Fixed target gaze was removed from the four clips. Presentation owns live
  tracking after animation. Clip names and source durations remain
  MenuSeatedIdle 8 s, MenuLook 4 s, MenuSwat 1.4 s, MenuReturn 1.8 s.

## Facial integration contract

The rig retains 65 names and the same hierarchy. **Eye.L/R bind positions and
roll changed**: source centers `(±.081, -.108, 1.558)`, local forward +Y and up +Z.
Head remains forward +Z and up +Y. Reimport model, rig and clips together.

The white of the eye stays on Head, preserving its volume. Pupils follow Eye.L/R.
HumanHead has four eyelid samples per eye: Blink25.L, Blink50.L, Blink75.L,
Blink.L, and the corresponding R names. For closure 0/.25/.50/.75/1, interpolate
only the two neighboring samples; their Unity weights sum to 100. Basis requires
no explicit weight. Example closure .4: Blink25=40, Blink50=60, other samples=0.
The samples keep the moving lids outside the globe.

`menu/menu_audit.json` and `menu/facial_audit.json` contain the complete contract.
Presentation is the single facial writer after animation in menu and gameplay.
It reads the optional legacy Eye scale-Z blink signal before resetting eye scale
to 1, then drives the lids. Runtime target tracking and saved variants are not
verified by these native files.

FBX imports each animation take as separate armature and shape-key actions in
Blender. This is four menu takes with two channel families, not eight menu clips.
Auditors now choose armature actions by their pose-bone curves. Unity's imported
take names and shape indices still require verification.

## Evidence and remaining work

- Final menu FBX: `96aa49594a9bc26622509623e0fd8eb00ab2ba789f88e6a197b406faa458776d`.
- Final menu blend: `716eeba06f87597f6722d971782d83b29edb0f060483cdb1e6d8cac5962c3301`.
- The candidate includes an editable human base with 15 gameplay clips, plus the
  separate four-clip menu. Geometry: 9,652 triangles, 65 bones, eight facial morphs.
- Final facial checks pass in source and FBX: exact morph correspondence, eye
  pivots/axes, unchanged globe vertices across nine closure levels, and 25/25
  frontal rays covered by skin for each fully closed eye. Open eyes retain the
  same ray counts as the first candidate: 17 white, 4 pupil, 4 surrounding skin.
- Only final open/closed face images were rendered at 640×640, in
  `menu/face-diagnostic/`. Pupil protrusion is fixed; the closed-lid crease and
  expression still need artistic refinement. Intermediate closure, gaze extremes,
  skin tones and live tracking require further visual review.
- The first 49 mm lid version passed 920 menu source/FBX position samples and
  clip boundaries. The final 54 mm lid file has **not** repeated that complete
  audit; its hash must not inherit the earlier gate. Both versions use the same
  joint and motion source, but final full validation is pending.
- The earlier full menu diagnostic counted six body vertices inside the reduced
  seat interior, versus zero before these changes. Pelvis minimum remained about
  1.455 mm above the nominal seat. Locate those vertices and check actual cloth
  contact before approving support.
- `audit_human_joints.py` is prepared for 37 poses per format and candidate,
  including Swat frame 12.76, wrist sections, angles and mesh/rotation fidelity.
  It has not run. General and wrist/knee views have not been rendered. Full
  gameplay deformation, moving grip, body quality and Unity acceptance remain
  open; no art approval is claimed.

Seventeen native processes consumed 157.202 process-seconds; four failed checks
were preserved with their logs. Failures exposed float32 join mapping, FBX action
family counting and two incomplete eyelid clearances. All own processes closed,
and the slot was released to Director at 23:22:40.819 UTC. Exact file hashes,
source snapshot, prior versions' reports and cleanup evidence accompany the
candidate. The next native work should prioritize the original wrist/joint
request, final clip fidelity and support, then partial facial views.
