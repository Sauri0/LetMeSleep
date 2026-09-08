# Central piping and continuous witness

Current model: `90cd6a695b32c3b98395df8f8da496ee56ffebd6026db757c03f9006eb777a06`.
No runtime/pose/radius change was made by this repair. Sim owns the separate
remote-crouch presentation fix. Source and final GLB are synchronized.

The original central secondary tube crossed the shirt: bind-space signed
depth ranged from21.79mm buried to18.34mm exposed. Projection corrected its
vertices but left52 of840 frontal interior/edge samples buried by up to.919mm.
Two local subdivision cuts and reconforming resolved that chord error.
Each final piping has1,104 authoring vertices /1,154 export vertices. The
visible outfit adds1,952 triangles; current default model is54,644 triangles.

Final checks:

- GLB invariance2,691 checks:30 complete meshes, all other trim vertices and
  triangles, materials, nodes, hierarchy, rig/binds, animation and morph data.
- Per outfit7,560 frontal triangle/edge samples:0 buried. Signed depth ranges
  +.189 to+2.215mm at those samples. Rear roots embed at most.2mm by design.
- Current native control:1 pose,84,611 exporter checks,0 failures. CPU skinning
  resolves Godot's import permutation, proves the complete triangle multiset,
  and uses actual packed imported weights. Maximum control discrepancy13.24µm.
- Three changed piping components recalculated in243 stored human poses:
  5,832 semantic pairs,0 contacts. The other geometry and42 mosquito poses
  remain covered by exact data/driver identity and the prior native proof.
  This preserves the declared finite-pose coverage; it is not285 new exports.
- Full continuous sequence:630 PNG,21seconds,4,484 checks,0 failures. Twelve
  emotes complete naturally with zero root drift. No microphone/audio stream.

Current clip:
`outputs/0.9-garment-trim/continuous-after2/human-fit4-continuous.mp4`.
Before clip:
`outputs/0.9-garment-continuous-final1/human-fit4-continuous.mp4`.
Both contain every original PNG plus report/runner hashes.

At30Hz the maximum upper-garment presentation step while standing up falls
151.74→101.23mm, and crouching149.29→101.01mm. The authoritative crouch/root
sequence is identical. Main garment geometry is identical, so this is the
separate presentation change, not a new cloth deformation repair. Wave and
celebrate retain their prior60.76/122.11mm maxima; their time was not smoothed.
Frame229 specifically falls151.74→50.73mm (display crouch.8, authoritative.6).

Ribete continuity is visible in current frames59/229/390/550 with regular
button interruptions. The prior broad/angular shoulder contour remains in
the celebrate pose; this report does not grant overall aesthetic approval.
The front torso camera may crop wide hands or broom top. This is not an
all-tool/all-cosmetic or complete gameplay/FPS test.

Final machine references: `work/garment09-trim-final.json`,
`work/garment09-trim-motion-final/index.json`,
`work/garment09-trim-invariance.json`,
`work/garment09-trim2-depth-interiors.json`,
`work/garment09-trim-reskin-control-final.json`.

Authoring: `garment_trim.py` is used by the full selected pipeline.
`characters_trim_repair.py` is an incremental reproducible repair and needs
the preserved fit4 blend/model in `outputs/0.9-garment-trim/source-fit4`.
The final editable blend already contains the repair. Frozen A/B untouched.

The earlier native selected972 log is historical trim1. Root subsequently
ran the same production/tools gate on frozen trim2: 972 checks, 540 records,
zero failures, exit 0 and empty stderr. Current evidence is
`work/release07-selected09-trim2-final.run.json` and
`outputs/0.9-garment-trim/selected-final/`. This remains a source gate, not EXE.
