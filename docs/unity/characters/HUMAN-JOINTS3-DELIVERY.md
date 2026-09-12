# Human joint correction — coherent delivery

Candidate: `art_source/unity/characters/candidates/human-menu-joints3/`.
Replaces candidate joints2; canonical reference 9 and old menu remain unchanged.

- `human/`: editable base, FBX, 15 gameplay clips and import audit.
- `menu/`: editable source, FBX, four clips, full menu/joint/facial audits and
  `joint-review/wrist_idle.png` plus `seated_three_quarter.png`.
- `manifest.json`: exact hashes, scope and remaining visual issues.

The wrist bridge, open cuffs, forearm-following grip and revised elbow/knee
topology are exported together. In 37 sampled poses per format/candidate,
maximum wrist bend changed R 146.28° -> 28.00°, L 99.92° -> 67.51°. The idle
close-up visibly restores hand/forearm continuity. These samples are not a
complete moving grip review or a volume certification.

Confirmed seat intrusions were HumanBody vertices 344–346 and 904–906 on the
z=.70 trouser ring: only 18.26% Hips weight let thigh flexion pull cloth into the
cushion. The upper ring now follows the pelvis more strongly. Final source/FBX
checks report zero interior vertices across 920 samples, pelvis minimum
0.576455 m and maximum foot error below 0.001 mm. Full mesh collision/contact
approval remains separate from the conservative seat diagnostic.

The same final export passes joint mesh/rotation fidelity and facial checks:
65 bones, eight lid shapes, corrected Eye bind centers/axes, unchanged eyeball
vertices and 25/25 rays covered per closed eye in source and FBX. Facial contract
and markers from joints2 remain valid; **Unity axes are not yet verified**.

Two inspected images show improved wrist/cuff continuity and left palm placement.
Full moving thumb/grip contact, knee/lapel rigidity, eyelid crease, saved variants
and the integrated Unity sequence remain open. The base's 15 gameplay clips were
exported/imported, but a complete gameplay deformation audit has not run on this
final candidate. No artistic approval is claimed.

The second slot used nine native processes, all exit 0. Last PID 380 closed at
23:36:00.072 UTC, within the three-minute operating window; Director received
the slot. No additional native processes are queued.

Base FBX SHA256: `9e85748b5ec38f57791bb7377dbe43bc6328792986837c7fdb260371f6a8d20b`.
Menu FBX SHA256: `3412a9caaec030c3fe89d646fd063c8431ac3db38a9b76a8c49d8bc0d98470ff`.
