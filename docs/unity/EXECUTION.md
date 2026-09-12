# Unity execution checkpoint

Active objective authorized by Branko: complete migration and cycle 0.9.4. On 2026-09-12 Branko further authorized autonomous decisions and asked for no more questions while asleep. Execute stages in order with technical acceptance; record artistic decisions for later user review. Do not claim user playtesting or WAN evidence that has not happened.

Latest steering: Tasks keeps three personal lives and human fainting, balance to refine through online play. At least five fixed maps; Director chooses house/patio, maritime island, night swamp camp, mountain cabin and farm/barn. Only house/patio and independent lobby in alfa.

Repository N:/LetMeSleep/Repository, codex/unity-094-alfa. Historical baseline 7dfa943. Godot remains untouched at its original path; git bundle and tracked working patch plus untracked inventory are in N:/LetMeSleep/Backups. The untracked originals remain in place; the inventory is not a content backup of those files.

Unity 6000.3.24f1, URP template com.unity.template.urp-blank. First CLI project-create run cancelled UPM; direct editor import subsequently exited 0 (N:/Unity/Setup/game-import.log). This establishes project loading, not a playable game.

Packages installed and imported: Input System 1.20.0, URP 17.3.0, navigation 2.0.14, test framework 1.6.0, uGUI 2.0.0, pinned EOS Plugin 6.1.2 (0b8f679193c5b6c74df5bddbe3248c9d7aadaee8), official com.unity.pipeline 0.7.0-exp.1. Unused template packages removed through PackageManager. No paid Unity AI enrollment.

All six existing team tasks are contributing. M1 source models/15 clips per species and import builder integrated; M2 sample-room native geometry import passed and full house/patio plus lobby is in production; W1 authoritative Sangre/physics/input/bots integrated, QA follow-ups underway; W2 cameras/audio/presentation bridge underway; UI actual screens underway; QA independent Core/gameplay/packet tests integrated. See AGENTS.md for ownership. Director holds the resident Unity editor; M2 has the CPU Blender generation slot.

Spatial contract: Unity metres, Y-up/Z-forward. Human capsule radius .25, height 1.72, eye 1.53, crouch height 1.0. Mosquito collision radius .055, initial model length .19/span .24 (wings not collision). Main circulation >=1.8 and stairs >=1.6, door sample clear opening1.10x2.20. Values remain tunable using actual visual/physical tests.

## Checkpoint 2026-09-12 10:06 UTC

- Branch codex/unity-094-alfa at N:/LetMeSleep/Repository. Exact user objective copied to N:/LetMeSleep/Planning/USER-GOAL-20260912.md. Active stage remains alfa; subsequent stages are not complete.
- Core RoomSession native Unity EditMode run passed 19/19; sanitized runner receipts in docs/unity/qa. Additional Gameplay and MessageFraming suites integrated and awaiting native combined run after current import.
- Native room import passed: 55 meshes, 45 colliders, 10 materials, 13 anchors; authored FBX axis discrepancy corrected by builder with measured door hinge/leaf checks. Geometry evidence is not a render-quality claim.
- EOS editor identity/create/leave observed. Original Windows probe exposed an unused overlay-helper native startup crash. NativePluginPolicy excludes that helper; a fresh Windows probe build at Artifacts/eos-probe-clean completed Succeeded:0 with helper absent and overlay/bootstrapper disabled through official configuration. Clean executable startup is being verified.
- Two players under this Windows account receive the same EOS DeviceID; guest correctly returned SameDeviceIdentity, zero transport packets. Host timed out. This does not validate relay, two-player networking, or WAN. Preserve device identity; external evidence is separately tracked.
- Actual playable boot/UI/map/characters/network composition remains in progress. No Unity release has been published or claimed ready for friends.
- Resident editor PID 30500, N:/Unity/Setup/unity-resident-2.log, nographics. Only Director opens/restarts it. A headless connectivity probe may run independently; check processes before changing GPU use.
