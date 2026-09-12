# Unity execution checkpoint

Active objective authorized by Branko: complete migration and cycle 0.9.4. On 2026-09-12 Branko further authorized autonomous decisions and asked for no more questions while asleep. Execute stages in order with technical acceptance; record artistic decisions for later user review. Do not claim user playtesting or WAN evidence that has not happened.

Latest steering: Tasks keeps three personal lives and human fainting, balance to refine through online play. At least five fixed maps; Director chooses house/patio, maritime island, night swamp camp, mountain cabin and farm/barn. Only house/patio and independent lobby in alfa.

Repository N:/LetMeSleep/Repository, codex/unity-094-alfa. Historical baseline 7dfa943. Godot remains untouched at its original path; git bundle and tracked working patch plus untracked inventory are in N:/LetMeSleep/Backups. The untracked originals remain in place; the inventory is not a content backup of those files.

Unity 6000.3.24f1, URP template com.unity.template.urp-blank. First CLI project-create run cancelled UPM; direct editor import subsequently exited 0 (N:/Unity/Setup/game-import.log). This establishes project loading, not a playable game.

Packages installed and imported: Input System 1.20.0, URP 17.3.0, navigation 2.0.14, test framework 1.6.0, uGUI 2.0.0, pinned EOS Plugin 6.1.2 (0b8f679193c5b6c74df5bddbe3248c9d7aadaee8), official com.unity.pipeline 0.7.0-exp.1. Unused template packages removed through PackageManager. No paid Unity AI enrollment.

The existing team tasks own separate worktrees. M1 source models/15 clips per species and builder are integrated, but its task is currently unavailable through app routing. M2 owns maps; W1 gameplay and physics; W2 presentation/audio; UI screens; QA independent tests/review. See AGENTS.md. Director holds the resident Unity editor and integrates their deltas.

Spatial contract: Unity metres, Y-up/Z-forward. Human capsule radius .25, height 1.72, eye 1.53, crouch height 1.0. Mosquito collision radius .055, initial model length .19/span .24 (wings not collision). Main circulation >=1.8 and stairs >=1.6, door sample clear opening1.10x2.20. Values remain tunable using actual visual/physical tests.

## Checkpoint 2026-09-12 10:06 UTC

- Branch codex/unity-094-alfa at N:/LetMeSleep/Repository. Exact user objective copied to N:/LetMeSleep/Planning/USER-GOAL-20260912.md. Active stage remains alfa; subsequent stages are not complete.
- Core RoomSession native Unity EditMode run passed 19/19; sanitized runner receipts in docs/unity/qa. Additional Gameplay and MessageFraming suites integrated and awaiting native combined run after current import.
- Native room import passed: 55 meshes, 45 colliders, 10 materials, 13 anchors; authored FBX axis discrepancy corrected by builder with measured door hinge/leaf checks. Geometry evidence is not a render-quality claim.
- EOS editor identity/create/leave observed. Original Windows probe exposed an unused overlay-helper native startup crash. NativePluginPolicy excludes that helper; a fresh Windows probe build at Artifacts/eos-probe-clean completed Succeeded:0 with helper absent and overlay/bootstrapper disabled through official configuration. Clean executable startup is being verified.
- Two players under this Windows account receive the same EOS DeviceID; guest correctly returned SameDeviceIdentity, zero transport packets. Host timed out. This does not validate relay, two-player networking, or WAN. Preserve device identity; external evidence is separately tracked.
- Actual playable boot/UI/map/characters/network composition remains in progress. No Unity release has been published or claimed ready for friends.
- Resident editor PID 30500, N:/Unity/Setup/unity-resident-2.log, nographics. Only Director opens/restarts it. A headless connectivity probe may run independently; check processes before changing GPU use.

## Checkpoint 2026-09-12 10:50 UTC

- Integrated 51 native EditMode tests passed previously. Further replica/tool QA and W1 pure tests passed (58); rerun native combined after current composition, do not conflate pure tests with PlayMode.
- Actual Bootstrap/AlfaApplication now wires menu, settings, preview, training, EOS lobby, lobby movement, round barrier, authoritative gameplay, presentation and results. Pending first complete PlayMode run and Windows candidate build.
- Per-actor flyswatter pickup/drop and replicated ownership integrated; seven authored pickups connected to map. World geometry now scoped to active map root. Begin protocol bumped alfa-2 with bounded tool definitions checked against map; final state reliable; no WAN evidence added.
- UI async action latches, contextual pause, remembered name and training cleanup integrated. Saved palette now applied in live lobby/round and transmitted to authenticated members. Pending runtime checks.
- M1 is unavailable through app routing (notLoaded / thread not found). Existing sources retained. W2 has temporary exclusive CLI slot on resident graphics editor PID18620 to diagnose identical pose renders and tool-axis presentation. Director is doing file work until slot released. No second editor.
- Full map source and seven tool markers delivered; rebuild required after latest delta. Presentation prefab regenerated once; new source requires another build after W2 diagnostic. Boot scene builder source exists, pending execution.
- Active D3D11 editor log N:/Unity/Setup/unity-render-resident.log; old nographics PID30500 exited. No playable Unity release published yet. WAN/friends, GTX1660Ti measurements and user art review remain external pending.

## Candidate checkpoint 2026-09-12 11:49 UTC

- Director integrated team changes through 2418efe. Native EditMode 61/61 and final PlayMode 2/2 passed. Final PlayMode receipt: ALFA-TRAINING-PLAYMODE-FINAL-20260912.json (1.45 seconds; two training roles and cleanup).
- Real rendered review corrected lobby inward normals, nighttime lighting, missing attic gables, UI selection glyph and narrow labels. House has 383 meshes /378 colliders, nine working door anchors and seven tool pickups. Final native captures remain at N:/LetMeSleep/Artifacts/review; these are game renders, not concept images.
- Human capsule no longer receives displacement from insect colliders. No-input native hold and authority-command defense validated; see QA receipts for exact partial scope. Body-down camera composition reviewed separately from input validation.
- EOS production portal read-only review confirmed redistributable Peer2Peer client policy. Native UI create/ready/leave succeeds. Two independent identities/relay/WAN remain unverified; never label online accepted from this result.
- Candidate source now includes bounded pre-Begin timeout, live menu models, PC quality default, isolated opt-in development-player probe, and clean-commit build/ZIP provenance. Preparing first complete Windows candidate; no Unity release is published at this checkpoint.
- Resident graphics editor PID18620 is Director-owned. Team source freeze for packaging; M1 routing remains unavailable.
- Alfa can be published as a candidate. The plan explicitly requires friends/WAN and Branko visual/play acceptance before beta; these are unresolved external gates, not automatic approvals from elapsed time. No later stage started.

## Public candidate 2026-09-12

- Build source: 35c2af4b168f5b95943f09fbb2c556924dd7b2cf. Unity Windows build Succeeded, zero errors, clean source before and after build; 230,513,038 output bytes.
- GitHub pre-release: https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.4-alfa . Main and migration branch contain this source. The release tag is tied to the build commit; subsequent documentation commits do not change the package.
- ZIP: 94,118,628 bytes; SHA256 e94038057ef08b23b91ed7246983c4a96f0dfaf8b651906f3fb7d970f8ab4e35. Launcher1.1.0: 38410f29b7436d14c986dcafdd49fae7629282eaf3d8e43134862f40d94ad90a, downloaded from GitHub and matched.
- Public updater installation passed checksum/extraction/manifest, repeated-start reuse and persisted activation in a new path containing spaces. Source/player and public-installed-player runtime probes passed both roles, stationary human, return to menu and EOS create/leave. No two-peer or WAN claim.
- Development package intentionally retains 123 managed/Burst PDB files (13,145,640 uncompressed bytes). These are diagnostic symbols; QA found no user credentials outside the reviewed redistributable EOS client config. Package hash remains immutable.
- Initial package probe launched hidden produced black captures and extremely short application-loop deltas. Those captures/timing numbers are invalid render/performance evidence. Visible execution is checked separately. None of these short probes certifies sustained rendering FPS or GTX1660Ti performance.
- Scope and external gates remain unchanged: candidate only; WAN/two identities, complete manual rounds, visual/fun approval and target-hardware profile pending. No beta/omega/delta/gamma execution or completion claim.
- Public installed executable also ran with a visible window after the hidden-render limitation was identified. All six runtime/online-host booleans passed; human render at1920x1080 inspected. Visible probe receipt ALFA-PUBLIC-VISIBLE-PLAYER-20260912.json. Timings remain short application-loop samples, not a sustained frame-rate benchmark. Player processes exited cleanly.
- After publishing and verifying the visible installed player, Director closed its resident Unity editor (PID18620). No game playtest process remains. Resume with one editor only after coordinating the slot; pending work is in the acceptance report and external gates.

## Public revision alfa.1 — 2026-09-12

- No stage transition. Source f0e6b80fd067ea7f25768c21d1e2cf898a8c65d1, tag/release v0.9.4-alfa.1; Windows build Succeeded, zero errors, normalized source content clean. Original alfa assets remain immutable.
- Preference recovery and future-schema protection validated with eight native storage tests, five native JSON-classifier cases, and Windows recovery/future/new-profile runs. All three Windows probes passed runtime/host checks. No WAN claim.
- Launcher1.1.1: 63 tests, published ZIP download/checksum/manifest, update of original alfa installation, second-start reuse, old slot retained. Public launcher hash verified. See qa/FINAL-REVIEW-0.9.4-ALFA1-CANDIDATE.md.
- Team: W1 full-round/retry evidence integrated; QA storage test suite integrated and final notes reviewed. Other owners remain frozen; no conflicting root edits requested.
- Beta still waits for independent players/networks and Branko approval. Art/balance/manual traversal/target GPU verification remain explicitly unaccepted. No questions sent while user sleeps.

## Alfa follow-up: traversal and camera — 2026-09-12

- Previous goal turn made concrete progress: alfa.1 published and public update/Windows profile recovery verified. Full cycle remains unchanged; alfa acceptance gates still apply.
- Current root b3cf37f integrates W1 perch acquisition correction (original W1a8015a9). Native old behavior reproduced twice: valid .25 acquisition canceled by .12 follow query before reaching surface. New approach preserves acquisition range only during approach and rejects a changed support. QA reviewed and ran Gameplay CPU 76/76; physical floor acquisition passed. This is not published yet.
- W1 passed a physical human route through stairs, upper hall, rear patio and return using normal inputs, without teleporting. Other routes/surfaces remain partial.
- Native floor perch exposed a camera defect: actor/anchor/camera at y.056, camera radius.08 intersects floor, ResolvedDistance0. Live frozen witness in editor PID5800 handed from W1 to W2; W2 owns camera correction and sole GPU/editor slot. Director must not restart it merely after a polling timeout.
- W2 also delivered9f75d96 (not yet integrated): authored Custom-tier light template with per-room resolutions, retaining Living/bedroom shadows within 2048 atlas. Director will regenerate the presentation library after camera verification and source integration. No FPS claim.
- Current candidate alfa.1 release notes now disclose perch limitation. Prepare next numbered alfa revision only after fixes/tests/build/public install are verified; preserve old package hashes.

## Public revision alfa.2 — 2026-09-12

- Source 0a82314da866f94ae71e3807cdaea875b3d111a9, tag/release v0.9.4-alfa.2. W1 perch correction, W2 camera collision recovery and supported URP Low/Medium shadow templates integrated. Earlier Custom enum attempt was rejected by native Unity and replaced before this candidate.
- Native house/lobby budget verified, independent QA reviewed: 56.25% /75% of PC 2048 atlas. Final Windows build clean, zero errors. First dirty-cache build retained only as rejected evidence; it was not packaged.
- Windows build probe six checks PASS; no atlas downscale warning in this observed PC run. Native floor perch/release and targeted camera tests passed. House front-entry route and full surface/door matrix remain partial.
- Public launcher1.1.1 downloaded alfa.2, checked checksum/manifest, activated and reused on second start (3/3). Previous alfa/alfa.1 installation slots remain. Original release artifacts immutable.
- Publication and exact package identity in qa/FINAL-REVIEW-0.9.4-ALFA2-CANDIDATE.md. Same source remains on main and codex/unity-094-alfa.
- Director owns final integration/editor slot; W1/W2/UI/environment frozen. No team overlaps and no later stage started. External gates and artistic approval remain as recorded in PENDING-EXTERNAL.md.
- Final public-installed Windows probe: six checks PASS, no atlas downscale warning in this run. Receipt ALFA2-PUBLIC-PLAYER-20260912.json; updater receipt committed. Director editor PID5800 and public test process23740 exited; GPU/editor slot released.
- Previous goal turn: progress (published and verified alfa.2). Remaining locally actionable alfa work includes front-entry/full surface traversal and visual defects identified by an owner review. External gates are unchanged; goal is not complete or genuinely blocked while those investigations can progress.

## Rechazo visual de Branko y lote de recuperación — 2026-09-12

Branko probó la candidata publicada y reportó una caída importante frente a Godot: faltan animaciones, mejora de UI y acabado. Las bases de los modelos no le disgustan, pero exige acercar diseños y gráficos a los bocetos aportados. La publicación alfa.2 permanece como candidata técnica; su distribución y probes no acreditan aceptación artística.

Trabajo reactivado por propietarios, sin comenzar beta:
- M1: revisar todos los clips/rig/manos y corregir fuentes/exportaciones de personajes alfa. Sin ampliar catálogo de cosméticos.
- W2: investigar reproducción/mezcla de animaciones en runtime, estados y velocidad; coordinar clips con M1. Luces motivadas por luminarias M2, sin manchas quemadas; conserva tiers/sombras.
- M2: estar/dormitorio/cocina como conjuntos habitados; carpintería, zócalos/vanos/transiciones limpios; luminarias visibles y materiales diferenciados. Conserva planta, rutas y pickups. Estar como muestra integrada antes de extender acabado. Floor_Oak: tablones .32m, largo alternado1.28m, juntas3mm, variación tonal≤8%; smoothness madera.16, revoque.08, textil.10 como base ajustable tras render. UV2 preservadas.
- UI: menú/lobby/HUD/personalizador/ajustes con paneles azul noche, jerarquía, títulos gruesos legibles, iconos propios, poco texto y modelos3D reales. Conserva contratos/callbacks. Sin funciones inventadas de los bocetos.
- W1: termina test físico entrada frontal y pared/techo; único editor/GPU PID33400. Sólo fixtures temporales; ningún cambio root de runtime ajeno.
- Director: contratos, integración y revisión conjunta contra referencias y en movimiento; QA independiente verifica errores reales. No publicar otro parche técnico como si fuera renovación visual completa.

Verificación del lote: mismas vistas comparables de estar/pasillo, dormitorio y lobby; movimiento continuo humano/mosquito con estados reales (no sólo poses forzadas); navegación UI y composición a720/1080. Comparar siluetas, proporciones, lectura doméstica, materiales, fuentes de luz y acciones con referencias, sin copiar textos/mecánicas. Documentar diferencias pendientes honestamente.

Audio fantasma reportado: procesos inspeccionados, no había Let-me-sleep.exe ni Godot activo. Era UnityPID33400 en Play de W1. Director y W1 confirmaron AudioListener.volume=0. Las pruebas físicas/visuales siguientes deben ser silenciosas; abrir editores batch con -noaudio cuando no se pruebe audio. No modificar ajustes reales ni silenciar otros programas. El usuario fue informado.

Branko también rechazó sonidos y música frente a Godot. La recuperación del audio existente se incluye en este mismo lote de calidad alfa, no se aplaza a omega. Comparación por archivos/código: qa/AUDIO-MIGRATION-GAP-ALFA2.md. W2 recibió stems y catálogo originales disponibles y créditos; no se presume una escucha realizada ni se amplían modos/herramientas por el inventario histórico.

W1 terminó recorrido adicional sin defecto runtime, commit e84de46 con recibos. EditorPID33400 cerrado y slot cedido a M1 para Blender CPU headless2hilos sin render. M1 verificó ausencia delPID antes de iniciar. M2/UI/W2 siguen archivos aislados; pruebas audibles requieren coordinación.

Precisión M1 tras aislar curvas: la supuesta pérdida de traslación Hips en FBX no se confirmó; las curvas existen y el reimportador de Blender añadía use_connect=True, invalidando la evaluación del auditor. No justificar cambios de exportación por ese falso positivo. Las penetraciones de suelo medidas en las poses fuente .blend siguen confirmadas. Director rectificó la explicación al usuario; validación Unity real posterior necesaria.
