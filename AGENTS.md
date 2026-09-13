# Let me sleep — Unity migration

## Nueva etapa autorizada: Higgsfield / Humanos

Observación y visibilidad: Branko pidió Blender abierto y visible para seguir
el avance, y capturas por app/visor en vez del escritorio que puede tapar con
otras ventanas. Para evidencia usar captura interna del visor o render de Blender
en la sesión asignada; si minimizada no actualiza framebuffer, usar render interno.
No traer ventanas al frente repetidamente ni prometer captura de ventana minimizada
infalible. Captura de escritorio sólo para verificar disposición de ventanas.

Corrección posterior de Branko: crear lo nuevo DESDE CERO directamente desde
los bocetos; no reciclar ni usar modelos/estética del alfa como base artística
o referencia para Higgsfield. Requisitos técnicos de integración separados de
la autoría; fuentes nuevas, antecedentes intactos. Ver sección prioritaria de
Higgsfield/HUMANOS-INICIO.md. Esta corrección prevalece sobre propuestas previas
de adaptar/reutilizar la base humana antigua.

Branko indicó «Comenza» después de ceder temporalmente la coordinación a
Encargado de Higgfield (01a09869-54a6-7f71-8e90-05de22618557). Se inicia la nueva
actualización por departamentos secuenciales, con Humanos como único departamento
en producción. Rigen Higgsfield/PLAN-EQUIPO-BOCETOS.md y
Higgsfield/HUMANOS-INICIO.md. Esta orden sustituye el STOP inferior únicamente
para los tickets nuevos emitidos por el Encargado; el alfa y sus asignaciones
históricas permanecen congelados. Director cedió turnos, integración y coordinación
y retomará después del cierre verificado. No debe pedírsele autorización operativa.

El Encargado asigna los turnos Unity/Blender, contratos compartidos y tareas de
apoyo; sólo Humanos produce contenido propio. Usar las tareas existentes,
conservar WIP y trabajar en N:. No activar otros departamentos por leer su plan.

**STOP — última orden del usuario:** alfa congelada tal como está. No continuar
implementación, pruebas, builds, importación ni iniciar otra etapa hasta nueva
orden. Ver `docs/unity/ALFA-FROZEN-20260913.md`. Este estado sustituye las
autorizaciones de continuación que figuran debajo. Higgsfield lo coordina el usuario.

Team reassigned by Branko on 2026-09-12 after his explicit «listo todos». The current ownership and task IDs in `N:/LetMeSleep/Repository/docs/unity/TEAM-RECOVERY-20260912.md` supersede the historical role mapping below and in older worktree AGENTS files. Read that central file before editing. Preserve existing task model settings. The general pause for creating chats is lifted; editor/GPU/Blender slots still require Director assignment.

Active stage: 0.9.4/alfa. Master plan: docs/unity/PLAN-UNITY-0.9.4.md. Latest user steering authorizes autonomous completion of the plan while asleep, without questions; see docs/unity/EXECUTION.md. Execute stages in order under Director coordination. Keep external evidence and user review pending explicitly; no worker starts a later stage independently.

- All new projects, tools, build output and worktrees on N:. Godot directories are historical reference, not runtime to continue editing.
- Integrator: Director at N:/LetMeSleep/Repository, branch codex/unity-094-alfa. Work in your assigned worktree and paths only.
- You are not alone. Never revert other changes, reset/clean, git add all, or publish independently. Selective commits; do not stage credentials or generated caches.
- Never edit the same Unity scene, prefab or Blender source concurrently. Director owns package manifests, project settings and assembly contracts. Ask Director for edits outside your ownership.
- Do not open an editor, playtest, benchmark or start Blender rendering without a GPU/time slot from Director. File work and bounded non-rendering generation are allowed in own worktree.
- No subagents from workers. Reuse existing team tasks. Keep checkpoint reports concise: commit, changed files, evidence, remaining issues.
- Reference images guide shapes/materials/composition only; no Bite & Build name, crafting, guns, classes, daily rewards or invented modes. Name Let me sleep. Default human wears pajamas, slippers and nightcap.
- Latest rules: no bite markers; W flies along aim; manual human defense; private 3D lobby and online room codes; roles random every round; no 2:1 ratio; fixed maps. Three personal lives in Tasks maintained, exact balance to refine in real online play. At least five maps this cycle; alfa only house/patio and separate lobby.
- Do not assert WAN, graphical quality or FPS from unit tests or old Godot evidence. Cite actual version, environment and test scope.
- Every committed Unity asset/script needs its .meta. Use Unity APIs for scenes/prefabs/settings, PackageManager API for dependencies. No paid service or trial enrollment required for this task.

Ownership: Modelador 1 art_source/unity/characters and assigned character exports; Modelador 2 art_source/unity/environments and assigned map exports; Worker 1 Gameplay; Worker 2 Presentation/Audio; Revisar interfaz visual UI; Revisión funcional Tests/docs QA; Director Core/Online/Editor bootstrap/project settings/build/launcher/integration.
