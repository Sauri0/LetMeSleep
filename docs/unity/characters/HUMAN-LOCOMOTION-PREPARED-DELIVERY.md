# Locomoción humana funcional: fuente preparada, sin exportación

Estado: **prepared-not-exported**. Seis pruebas matemáticas pasan y cinco scripts compilan con Python3.14. No se ejecutó Blender, el auditor nativo ni Unity para estos clips. No existen FBX nuevos de esta entrega. La orden de Director limita el trabajo a clips funcionales y datos de alcance; rediseño estético queda fuera.

Fallo que aborda: el ciclo fuente actual tiene una distancia de apoyo incompatible con la velocidad y la cadencia visual/audio usadas en juego. La propuesta mantiene longitud de piernas y reemplaza trayectoria de apoyo/vuelo/pelvis. Conserva transformaciones locales de manos, dedos y sockets de agarre; el arreglo de la muñeca durante el golpe pertenece a Gameplay, integrado por Director en9b214e8, y requiere su verificación de runtime.

## Artefactos preparados

- `human_locomotion_contract.py`: cuatro perfiles, curvas de tobillo/cadera, distancia por ciclo y marcadores explícitos.
- `author_human_locomotion.py`: dos reemplazos y dos acciones nuevas sobre el rig existente; no llama al constructor completo. Captura Hand, quince dedos y Socket.Grip de cada lado antes de reemplazar Walk/Run.
- `export_human_locomotion.py`: salida aislada Human solamente; verifica65huesos,5mallas,15acciones iniciales y17finales. Preserva trece acciones ajenas mediante hash de sus curvas, geometría, morphs, pesos, rig y winding exactos. No cambia HumanMenu, Mosquito, prefabs, Animator, Binding ni IDs de runtime.
- `audit_human_locomotion.py`: preparado para comparar fuente/FBX, suelas reales, subframes, huesos de brazos/piernas, uniones, matrices locales de agarre, costura del loop y trece acciones ajenas. Compara también payloads FBX de geometría, shapes, skin, transformaciones, asignación de materiales y winding. Los estiramientos de aristas de muñeca/codo/rodilla son diagnósticos para revisar, sin umbral que pretenda aprobar la apariencia.
- `check_human_locomotion_contract.py`: seis regresiones sin bpy; alcance de todo el swing con20mm de reserva, apoyo fijo, continuidad de posición/velocidad, cadera continua y separación entre duración de archivo y cadencia.
- `diagnostics/human-locomotion-prepared1/locomotion_profiles.prepared.json` y `source-check.json`: contrato sin hashes de assets nuevos y hashes de los scripts comprobados. Su estado prohíbe interpretarlos como clips exportados.

## Contrato con Audio/Presentation

| Clip explícito | Velocidad m/s | D m/ciclo | Contactos/s | Duty por pie | Duración fuente |
|---|---:|---:|---:|---:|---:|
| Human_WalkSlow |1|.833333333333|2.4|.60|2s|
| Human_Walk |1.55|.96875|3.2|.56|2s|
| Human_Trot |3.1|1.55|4|.32|2s|
| Human_Run |5|2.173913043478|4.6|.24|2s|

Cada archivo tiene61frames a30fps,60intervalos. Los tiempos nominales reproducidos son.833333/.625/.5/.434783s, distintos de los2s de archivo. L contacta en fase0 y R en.5. El clock integra distancia horizontal presentada/D y evalúa fase por **Clip.length real importado**. Audio confirmó compatibilidad en052f4f0: muestreo manual sin clamp2.5. A velocidad nominal, las velocidades equivalentes de reproducción son2.4/3.2/4/4.6.

Los cuatro perfiles son erguido/plano. Human_Walk a1.55 no es locomoción agachada. Las transiciones entre duties diferentes, arranque/freno, giro y terreno siguen sin certificar; no habilitar sólo porque la matemática de los cuatro puntos pasa. La cadencia queda sujeta a escucha y percepción. Se conserva Crouch intacto y no se inventan flags ni IDs.

## Exportación selectiva preparada

Baseline exclusivamente `candidates/human-blink-winding1/human`, blend SHA256 `918ddcf802ef761cf89bf20bedcec4019de8d8adc4bb765c2e5c7361e3f862a1` y FBX `2473a8e6420dcb7c15d58c6dcb0e9fc64769bc8af6636844c71851a42bd84b66`. El exporter rechaza otro blend, hashes inconsistentes o carpeta de salida existente.

**Ejecutar sólo tras asignación expresa del slot nativo de Director.** El runner limita Blender a2threads, BelowNormal, sin ventana y registra PID/salida. No hace reintentos ni promoción automática. Este comando está preparado y no se ejecutó:

```powershell
$slotNote = 'REEMPLAZAR por la asignación explícita de Director'
if ($slotNote.StartsWith('REEMPLAZAR')) { throw 'Falta la asignación de slot' }
$runRoot = 'N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/.validation/human-locomotion1'
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
& 'C:/Users/brank/AppData/Local/Programs/Python/Python314/python.exe' `
  'N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/run_menu_native_stage.py' `
  --run-root $runRoot --stage human-locomotion1 --script export_human_locomotion.py `
  --slot-note $slotNote -- `
  --baseline-human 'N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/candidates/human-blink-winding1/human' `
  --output-human 'N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/candidates/human-locomotion1/human' `
  --audit
```

Salidas esperadas, todavía ausentes: Human blend/FBX, `audit.json`, `preservation.json`, `source-snapshot`, `locomotion_profiles.json` con hashes nuevos, `locomotion_fbx_preservation.json`, `locomotion_native_audit.json` y `human_arm_reach_samples.json`. Un fallo numérico escribe métricas cuando alcanza ese punto y termina con exit distinto de0; no se debe promover el candidato ni asumir un gate aprobado por la existencia del archivo.

El auditor toma fases uniformes cada1/240 de ciclo más límites exactos de apoyo. Límites iniciales: error de tobillo≤3mm, cadera≤1mm, suela apoyada≤3mm del suelo, drift de vértices de suela≤6mm por apoyo, separación/longitud de extremidades≤.1mm, escala≤1e-4, costura de matriz≤1e-4. Mide el Root virtual avanzando D por ciclo; el Root del clip permanece fijo. Importación FBX se desconecta sólo en memoria para evitar el `use_connect` inferido por Blender; no se guarda ese ajuste.

La tabla de alcance incluye origen de UpperArm, LowerArm, Hand y Socket.Grip en coordenadas fuente y actor, longitudes de brazo/antebrazo, hombro→mano y hombro→agarre. Idle fases0/.25/.5/.75/1; Crouch0/.25/.5/.6/.75/1. Son poses evaluadas, separadas del bind y del alcance efectivo de collider/herramienta. Gameplay debe compararlas con su barrido y pole de IK sin inferir alcance autoritativo del mesh solamente.

## Criterio de candidata

Para pasar a integración faltan exportación/auditor nativos con recibo de PID, comprobación de duración/loop/selección real de clips en Unity, y reproducción de contacto/alcance, muñecas, articulaciones y audio con el clock compartido. No se requiere rediseñar silueta, materiales o rostro para cerrar estos defectos funcionales. La aceptación visual estética queda para la renovación posterior.
