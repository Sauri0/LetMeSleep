# Auditoría de fuentes tras la prueba alfa.2

Estado: fuentes regeneradas, auditor de movimiento y reimportación FBX aprobados. Pendientes importación/reproducción y revisión visual en Unity; no aprobación artística ni producción beta.

## Evidencia de partida

El generador, FBX humano y FBX mosquito del worktree de M1 coinciden byte a byte con `N:/LetMeSleep/Repository/art_source/unity/characters`. Los catálogos tienen 15 acciones por especie. El builder pasó importación nativa e idempotencia, pero esos gates no prueban que los clips se reproduzcan con fase correcta en juego ni que la pose resulte convincente.

El sondeo anterior de 7 acciones confirmó curvas variables en los FBX. Comparaba extremos de huesos; FBX puede reconstruir longitudes de huesos, por lo que ese dato no sirve para medir fidelidad de deformación entre fuente y exportación. El nuevo auditor `audit_motion.py` usa cabezas de huesos en coordenadas de mundo, mide mallas evaluadas por acción y compara posiciones fuente/FBX con el mismo tiempo normalizado.

## Hallazgos de código antes de ejecutar Blender

- Walk y Run: sólo tres claves de pose, sin fase de apoyo explícita, sin compensación de pie ni rodilla resuelta hacia un objetivo. Tener loops coincidentes al principio/final no descarta penetración del suelo ni deslizamiento entre claves.
- Crouch, Fall, Faint y Recover: posiciones/ángulos prefijados, sin comprobación de altura de la malla en el piso durante el clip. Deben medirse antes de considerarlos acabados.
- Swat: mueve brazo y antebrazo, pero mantiene dedos a cero; no forma un agarre alrededor del mango. FingerCurl existe como prueba separada y no resuelve por sí solo la pose de herramienta.
- Mano: el gate fuente sólo prueba la dirección de cierre de los índices. Falta cuantificar los diez dedos y revisar el pulgar y los pesos de las uniones durante flexión.
- Blink: escala Z local de Eye. Hay que comprobar el eje real y la altura resultante del ojo; el nombre de canal no acredita un párpado correcto.

## Diferencia respecto de los bocetos

Se volvieron a inspeccionar las hojas originales de humanos y mosquitos. El humano de referencia tiene mandíbula más estrecha y angulosa, nariz pequeña integrada, ojos de facetas marcadas y extremidades con cambios de sección más claros. Nuestra cabeza tiene mayor volumen lateral y la silueta del pijama es más uniforme. Mantener pijama, pantuflas y gorro; corregir proporciones y planos del mismo personaje base, sin introducir ropa/clases de las referencias.

El mosquito de referencia distingue mejor patas delanteras/medias/traseras y sus alas tienen contorno de hoja alargada. La fuente actual repite casi en paralelo las tres patas de cada lado y extiende alas anchas lateralmente. Mantener tórax, abdomen largo, punta de picadura acordada y radio de juego; ajustar la articulación y silueta sólo tras identificar los defectos de animación.

## Reparto de correcciones

M1: fuentes, exportaciones, rig, deformaciones, poses, manos y comparación con referencia. W2: Animator/Presentation y medición de fase real en ejecución. Director: importación/capturas y turnos CPU/GPU. No se modifica runtime ajeno ni el script de captura del Director.

`motion_audit.json` contiene amplitudes de malla, curvas variables, pies, loops y comparación fuente/FBX de las 30 acciones. Se ejecutó en el turno CPU headless coordinado por Director. Las capturas de la primera muestra permanecen como historia, no como validación de esta revisión.

## Primer resultado y rectificación del auditor

La medición inicial confirmó movimiento de malla en las 30 acciones. En la fuente `.blend` se midieron penetraciones de piso: Walk 4,1 cm, Run 5,0 cm, Crouch/Jump/Land 26,9 cm, Faint 52,1 cm y Recover 37,1 cm. Estos sí son defectos confirmados de las fuentes y requieren corrección de apoyos.

La primera comparación también reportó diferencias de traslación de Hips de 30/60 cm entre fuente y FBX. **Esa conclusión sobre la exportación era incorrecta**: la inspección de las curvas FBX encontró los valores intactos; Blender infiere `use_connect=True` al reimportar, y su evaluador suprime la traslación de ese hueso conectado. El contrato fuente usa huesos no conectados. Se corrigió el auditor restaurando ese contrato antes de evaluar; no se modifica el exportador ni el archivo FBX para arreglar ese falso hallazgo. `motion_audit_before.json` conserva la medición inicial y debe leerse con esta rectificación.

Los diez dedos medidos cierran hacia la palma tanto en fuente como FBX. Eso no valida todavía contacto con el mango ni calidad de pliegues; son comprobaciones diferentes.

## Corrección fuente preparada

`author_motion.py` reemplaza las poses dispersas por trayectorias muestreadas a 30 Hz, claves lineales sin sobrepaso y piernas de dos huesos resueltas hacia los pies. Walk/Run distinguen apoyo y balanceo; Crouch/Land mantienen las plantas a nivel; Fall/Faint/Recover comprueban el punto inferior de la malla contra el plano de apoyo. Swat cierra la mano derecha durante toda la acción. Se conserva Root estacionario, nombres de acciones/huesos/IDs y tamaño de colisión.

La revisión geométrica estrecha mandíbula/cabeza, integra mejor ojos menos profundos, conserva nariz angular y cuello, afina el cuerpo del pijama, separa las tres patas por lado y alarga el contorno de las alas. No añade cosméticos. Cambia la geometría de la misma base aprobada como dirección; requiere nueva inspección visual en Unity.

## Resultado posterior medido

Generación, auditor completo y reimportación FBX terminaron con exit 0, cada proceso con 2 hilos y límite de 55 s, sin render/editor. Los `.blend`/FBX entregados corresponden al generador actual; los gates vinculan cada evidencia a su SHA-256.

| Comprobación | Resultado |
| --- | --- |
| Acciones con movimiento de malla | 30/30, evaluadas en fuente y FBX (60 filas) |
| Diferencia máxima de cabezas fuente/FBX | 0,000887 mm |
| Piso humano mínimo entre muestras | -0,569 mm, dentro del gate de 3 mm |
| Separación máxima inicio/fin de loops | 0,000553 mm |
| Traslación de Root | Dentro del gate de 0,1 mm |
| Cierre de diez dedos por formato | Desplazamiento hacia palma mínimo 27,19 mm |
| Altura de ojo cerrado/abierto | 8,07 % |
| Roundtrip geometría, huesos, sockets, acciones | 3/3 assets aprobados |

Se encontró y corrigió un defecto adicional de interpolación en Clap: las claves enteras coincidían en fuente/FBX, pero el frame 23,5 difería 192 mm en Hand.R y 247 mm en Little03.R. La fuente interpolaba ángulos Euler cerca de un cambio de representación mientras el FBX reimportado usaba quaternion. El simple ajuste de vueltas Euler no resolvió el defecto. Ahora `Character.clip` guarda rotaciones quaternion con signo continuo; Clap coincide dentro de 0,000439 mm. Se mantuvo el umbral original de 2 mm.

El auditor evalúa nueve tiempos normalizados por acción, más claves dispersas cuando existen; no constituye un barrido de todos los subframes. El contacto geométrico de palma se mide en frame 15, pero la deformación visual, el agarre real de herramienta, las transiciones y el contacto de picadura requieren revisión nativa. No se atribuye aprobación estética a estos números.

## Contrato de integración y catálogo

Se mantienen los 15 nombres/IDs por especie, rig, colisiones y boca del mosquito en reposo `(0,0,+0.095)` de Unity. Los rangos/duraciones cambiaron: leer `human/audit.json` y `mosquito/audit.json` al importar, sin conservar duraciones antiguas. Socket.Grip.L/R avanza 15 mm en la fuente, de Y=-0,025 a -0,040 m; validar el mango con W2. No se editaron runtime, Animator ni Presentation en esta revisión.

`plan_visual_evidence.py` produjo `visual_evidence_jobs.json`: tres assets reales, cinco vistas por asset, tres giros de 360°, treinta videos de animación, treinta hojas de cinco poses y dieciséis detalles (94 trabajos). No crea medios ni añade variantes cosméticas. Todos los trabajos comienzan `generated=false`, `reviewed=false`. Director posee el índice/capturas, W2 la reproducción; deben guardar fase real, cambios de hueso/malla, identidad de fuente y motor. Las vistas de agarre y picadura necesitan integración efectiva, no sólo una pose aislada.

Pendiente: importar y regenerar prefabs con el builder, comprobar reproducción/fase en Unity y generar/revisar las capturas del catálogo. El manifiesto se sella con `--source-only`, `rendered=false` y `unity_import_verified=false`.

## Corrección de pose inicial del importador Unity

La integración nativa de esta fuente encontró CameraEye Y=1,512 m, con Hips Y=0,732 m, incluso en el modelo importado sin controller. La lectura binaria del FBX con el parser oficial de Blender confirmó `Model/Hips Lcl Translation Y=0,75` y las cinco matrices BindPose de Hips también Y=0,75. El archivo conserva el reposo correcto. La primera AnimationStack es Human_Blink; su fase cero baja Hips 18 mm, exactamente la diferencia observada en Unity.

El builder `alpha-characters-5-bind-pose` reconstruye las transformaciones de reposo a partir de `renderer.localToWorldMatrix * sharedMesh.bindposes[i].inverse`, comprueba consistencia entre renderers y aplica padres antes que hijos. Lo hace antes de orientación/anclajes y de validar el prefab neutral. Los sockets conservan sus offsets locales; ninguna animación ni cámara recibe compensaciones. El gate de CameraEye sigue siendo 1,53 m ±5 mm. Las fases animadas conservan sus movimientos legítimos.

Compilación de ambas assemblies contra referencias Unity 6000.3.24f1: PASS. Director confirmó el 12/09/2026 (hora reportada: 18:52:07) `BuildAndVerifyIdempotence` nativo PASS con `alpha-characters-5-bind-pose`. Se leyó además el receipt integrado `unity/Assets/LetMeSleep/Content/Characters/BuildReceipt.json` con `success=true`, sourceDigest `cf2ebb2825…` y builderDigest `b7d8d88e…`. Queda cerrado el fallo de altura de reposo; la aprobación visual continúa pendiente de capturas nuevas y revisión efectiva de poses.
