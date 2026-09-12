# Unity execution checkpoint

Active objective authorized by Branko: complete migration and cycle 0.9.4. On 2026-09-12 Branko further authorized autonomous decisions and asked for no more questions while asleep. Execute stages in order with technical acceptance; record artistic decisions for later user review. Do not claim user playtesting or WAN evidence that has not happened.

Latest steering: Tasks keeps three personal lives and human fainting, balance to refine through online play. At least five fixed maps; Director chooses house/patio, maritime island, night swamp camp, mountain cabin and farm/barn. Only house/patio and independent lobby in alfa.

Repository N:/LetMeSleep/Repository, codex/unity-094-alfa. Historical baseline 7dfa943. Godot remains untouched at its original path; git bundle and tracked working patch plus untracked inventory are in N:/LetMeSleep/Backups. The untracked originals remain in place; the inventory is not a content backup of those files.

Unity 6000.3.24f1, URP template com.unity.template.urp-blank. First CLI project-create run cancelled UPM; direct editor import subsequently exited 0 (N:/Unity/Setup/game-import.log). This establishes project loading, not a playable game.

Initial packages: template Input System, URP, navigation, tests and uGUI; add pinned EOS Plugin 6.1.2 and official com.unity.pipeline 0.7.0-exp.1 via PackageManager. No paid Unity AI enrollment. Remove unused template version-control, multiplayer-center, visual-scripting and Rider packages. Package imports and compilation must be verified before first project checkpoint.

All six existing team tasks dispatched and observed active. Initial deliverables: M1 real character sources; M2 room/door sources; W1 gameplay contracts; W2 URP/audio recipe; UI interaction contract; QA independent acceptance matrix. See AGENTS.md for ownership. Director holds Unity editor; M2 has CPU Blender generation slot only.

Spatial contract: Unity metres, Y-up/Z-forward. Human capsule radius .25, height 1.72, eye 1.53, crouch height 1.0. Mosquito collision radius .055, initial model length .19/span .24 (wings not collision). Main circulation >=1.8 and stairs >=1.6, door sample clear opening1.10x2.20. Values remain tunable using actual visual/physical tests.
