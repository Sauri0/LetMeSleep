# Human locomotion1: exportación funcional y auditor nativo

Estado: **numeric-pass-visual-audio-pending**. Corrige en fuente la relación entre distancia recorrida, apoyo de pies y cadencia de los cuatro ciclos humanos. El candidato sigue aislado; no fue promovido ni activado en Unity por Modelador Humanos. No modifica diseño, geometría ni materiales.

Fuente preparada1bca463, seguida por ampliación del auditor para alcance funcional. Director asignó slot tras terminar UnityPID380. Una ejecución Blender5.2.1: **PID27188**,2threads, BelowNormal, oculto y sin renders; inicio2026-09-13T02:01:14.729881Z, fin02:02:27.126956Z,72.396s, **exit0**. Se verificó ausencia del PID y se devolvió el slot. Los recibos están en `candidates/human-locomotion1/evidence`. El log contiene avisos de limpieza de la dependencia cryptography de extensiones de Blender; exportación y auditor terminaron correctamente. No se alteraron esas dependencias ni se lanzó un reintento.

## Candidato exacto

Carpeta `N:/LetMeSleep/Worktrees/characters/art_source/unity/characters/candidates/human-locomotion1/human`.

| Archivo | SHA256 |
|---|---|
| LMS_Human_alpha.blend |78db69ddd6e2923804ee2b2528c50f51536aefe0b349477a622aa554a4ae1e15|
| LMS_Human_alpha.fbx |ccb0f1f92918ea52e738719190be934d7ec14843b4b6206284bd4cb89895297b|

Baseline humano de winding final: blend918ddcf802ef761cf89bf20bedcec4019de8d8adc4bb765c2e5c7361e3f862a1, FBX2473a8e6420dcb7c15d58c6dcb0e9fc64769bc8af6636844c71851a42bd84b66. Rig65huesos,5mallas, morphs, posiciones, pesos, winding y trece acciones ajenas permanecen iguales en las comprobaciones de fuente. FBX conserva exactamente payloads de posiciones, shapes, skin, transformaciones de modelos, asignación de materiales y winding. Las trece acciones ajenas coinciden además en muestras de matrices antes/después del roundtrip.

Se reemplazan **Human_Walk/Human_Run** y se añaden **Human_WalkSlow/Human_Trot**:17acciones totales. Cada ciclo nuevo dura2s reales en fuente y roundtrip,61frames/30fps. `locomotion_profiles.json` entrega nombres explícitos, D=.833333333333/.96875/1.55/2.173913043478 y contactosL0/R.5. Audio debe seguir leyendo Clip.length real de Unity. IDs, importación de loops y referencias pertenecen a la integración de Director; no se presupone que los dos nombres nuevos entren automáticamente al Animator.

## Evidencia numérica

Seis checks matemáticos previos pasaron. Auditor Blender:248fases por clip y formato,1984poses con malla evaluada, incluidos subframes y fronteras de apoyo. Sin errores de gate. Máximos FBX:

| Clip | Error tobillo | Drift de suela por apoyo | Penetración máxima suelo | Flexión rodilla |
|---|---:|---:|---:|---:|
| Human_WalkSlow |2.384mm|.153mm|.151mm|79.50°|
| Human_Walk |2.368mm|.154mm|.152mm|81.92°|
| Human_Trot |2.047mm|.368mm|.368mm|107.52°|
| Human_Run |2.784mm|.667mm|.667mm|121.05°|

Mayor separación de unión brazo/pierna≤.0013mm; variación de longitud≤.00023mm; delta local de matriz Hand/dedos/grip respecto de Walk/Run anterior≤4.19e-7. Root posicional0. Error máximo de coeficiente de matriz fuente↔FBX2.51e-6, costura de loop6.56e-7. Las métricas de matriz combinan coeficientes de rotación/escala y traslación; no son distancias en metros.

La malla necesita revisión en Unity: el mayor cociente de longitud de arista frente al bind es1.045 en región de muñeca,1.425 en codo y1.887 en rodilla. Son diagnósticos sin umbral de aceptación estética; el auditor no convierte estas cifras ni el gate de suelas en aprobación visual de articulaciones. El arreglo del brazo durante golpe y del alcance autoritativo sigue en Gameplay/Director, separado de estos clips.

## Datos para calibración de alcance

`human_arm_reach_samples.json` contiene Idle/Crouch/Swat+cuatro gaits en fuente y FBX; `human_arm_reach_summary.json` conserva sus envolventes sin filas. Mínimo120Hz de tiempo del archivo; Swat incluye todos los ticks0..18/30s, y gaits también todas las fronteras de apoyo. Se entregan AABB actor-local de UpperArm/LowerArm/Hand/Socket.Grip de ambos lados, máximos desplazamientos entre muestras, mínimos/máximos de longitud Upper/Lower y spans hombro→Hand/grip, y matrices fuente de ambos sockets.

Los AABB son **envolventes muestreadas**, no límites continuos demostrados ni validación de crossfade. Las distancias entre muestras tampoco acotan una excursión interior no muestreada. No incluyen giro global del actor ni el IK de golpe. Se debe aplicar el modelo de interpolación y margen que adopte Gameplay para su perfil autoritativo.

Ejemplos fuente de altura de hombro: Idle1.151988..1.152m; Crouch llega a.681802m; Swat1.151401..1.152012m. Los gaits nuevos bajan hombros: WalkSlow1.084962..1.109962m, Walk1.074904..1.109904m, Trot1.049696..1.089494m, Run1.034449..1.085060m. Esto exige usar los datos del gait al resolver takeover, cámara y alcance, no la altura de Idle para todo.

Hand→Impact de herramienta montada queda dependencia explícita de calibración de ToolView importado por Director/Gameplay, según instrucción de Director de no frenar esta exportación. Se verificó sólo que el FBX de herramienta existente coincide conE2C01038AE45FD5A22CB52A14C4B34EDD4FFB3FC54DD3494DCC8743BAC17CC16; no se regeneró ni cambió herramienta. Los recibos AuthorityStrike ya contienen mediciones reales de ese montaje.

## Pendiente funcional para cerrar candidata

Importar y asignar referencias de los cuatro clips; comprobar loops y duración real; sincronizar pose/contactos/audio con el clock compartido. Reproducir las muñecas durante golpe con objeto, transición Crouch/golpe y alcance en las nuevas alturas de hombro. Revisar arranque/freno, crossfade entre duties, giro, caída y estabilidad en el entorno de juego. Estos resultados planos por ciclo no certifican terreno desigual ni locomoción agachada.

La percepción de cadencia y articulaciones queda pendiente de reproducción real. La futura renovación estética con Higgsfield permanece fuera de esta candidata y no bloquea los arreglos funcionales.
