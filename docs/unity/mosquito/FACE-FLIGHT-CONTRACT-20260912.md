# Mosquito R4 — fuente de rostro y vuelo

**SOURCE_ONLY, sin Blender ni Unity ejecutados.** Los `.blend`, `.fbx` y recibos nativos siguen siendo el candidato R3 de `2f6ef3e`, no acreditan estos cambios. Director debe conceder el siguiente turno de generación/validación. No se editaron humanos, helpers compartidos, runtime, prefabs ni manifiesto.

## Qué explica la fuente actual R3

Branko percibe el mosquito del menú como un modelo trasladado con alas inmóviles. La fuente R3 sí anima `Wing.L/R`, Thorax, abdomen y seis cadenas de patas. Fly/Hover tienen 13 muestras, frames1–13 a30FPS, duración.4s, tres batidas:7.5Hz a velocidad1. La excursión base R3 es.43rad, envolvente Fly hasta.53rad. El batido se aplica alrededor del origen de cada ala en espacio del rig mediante `Rz(sign*fold) @ Ry(-sign*flap)`, junto con la deformación del Thorax. Root permanece inmóvil.

La escena central `unity/Assets/Scenes/LetMeSleepBoot.unity` referencia para `MenuMosquitoFlight` GUID `483916dd41effed479826b9930069204`, fileID `-1491716763371280848`: coincide con `Models/LMS_Mosquito_alpha.fbx.meta`, nombre Mosquito_Fly, take `LMS_MosquitoRig|Mosquito_Fly`, frames0–12, loopTime1, compression0, optimizeGameObjects0. `LivingMenuContentBuilder` selecciona ese nombre. Las rutas de huesos fuente conservadas son `Root/Thorax/Wing.L` y `Root/Thorax/Wing.R` bajo el rig. Esto verifica referencias de archivos, **no** las rutas reales de curvas importadas respecto al Animator ni la evaluación visual de Unity.

Presentación confirma el graph manual: SetSpeed(0), SetTime(elapsed % clip.length) por Update, Evaluate(0). Ese patrón no prueba una congelación. ReducedMenuMotion sí fuerza explícitamente la fase0. El video no mide los FPS de Unity; las hojas extraídas cada.5s tampoco certifican legibilidad de una batida. No se atribuye la queja a alias temporal sin diagnóstico. La aceptación sigue requiriendo toma a velocidad normal y escala final, con mosquito suspendido y desplazándose, sin captura PNG concurrente.

## Candidato de vuelo R4

Se conserva frecuencia, duración, los15 IDs, Root y contratos físicos. La amplitud base sube de.43 a.58rad; envolvente Fly hasta.70rad, excursión muestreada de−.70 a+.67rad. Thorax y abdomen ganan movimiento secundario; el arrastre máximo fuente de patas pasa de.024 a.027m. El nuevo endpoint aéreo se comparte con Hover, PerchEnter/Land, Brake y Detach. SurfaceWalk, D=.100m/ciclo, escala.5, colisión.055m, Root apoyado.057m y los siete sockets permanecen intactos.

`flight_channels()` y `flight_contract()` exponen parámetros para auditoría. El aumento es una propuesta de legibilidad pendiente de comparación, no una corrección comprobada del menú. El auditor ahora comprueba por separado desplazamiento de marcadores reales de cada ala y pie en vuelo, además de los gates previos de cuerpo, Root, soporte, clips y endpoints. Estos umbrales numéricos no sustituyen percepción artística.

## Cara e interfaz de Presentación

`author_mosquito_face.py` añade seis huesos a los33 retenidos: total39. Todos son hijos directos de Head, con pivot fuente `(±.027,-.094,.113)` y tail orientada `+X` fuente. Pupil.L/R son pivots de globo; no necesitan traslación. El blanco permanece rígido en Head y conserva su geometría R3.

| Rol | Huesos | Delta en espacio fuente, respecto al bind |
|---|---|---|
| Mirada | Pupil.L/R | `Rz(yaw) @ Rx(pitch)`, yaw±12°, pitch±10°; forward−Y y up+Z |
| Cierre superior | LidUpper.L/R | Giro `+90° * closure` alrededor de+X |
| Cierre inferior | LidLower.L/R | Giro `−90° * closure` alrededor de+X |

Closure=0 abierto,1 cerrado; escala siempre1. Los párpados son dos cuartos de caparazón por ojo, con espesor.6mm fuente, que giran desde atrás hasta cubrir el frente. No se aplasta ni desplaza el blanco. Los límites son candidatos a inspección de cerca/frente/perfil, con especial atención a intersecciones entre caparazones, ceja y cabeza.

En el bind **Blender fuente**, local+Y de esos huesos es source+X y local−X es source+Z. Son datos de autoría, **no ejes certificados de Unity**. El exportador/importador puede reconstruir bases: el builder debe convertir cada eje al marco bind-local importado y serializarlo. `apply_facial_pose()` implementa el overlay QA en espacio del rig; la próxima auditoría registrará también bases reimportadas por Blender, sin llamarlas prueba Unity.

Presentación acordó `BlinkBone[]` con Transform, LocalAxis, ClosedAngleDegrees por ojo, aplicando `baseLocalRotation * AngleAxis(angle*closure,axis)`. Eye/PupilForward+Up y límites configurables admiten ambas especies. Humanos mantiene sus Eye.L/R y morphs; este módulo no requiere alterar esa solución. Un único VisualAttentionRig post-Animator aplica pupilas/parpadeo en menú y gameplay. El menú sólo entrega objetivo de atención. Los15 clips corporales conservan tracks faciales neutrales: no se añaden IDs y no debe haber un segundo writer que los restituya después del overlay. Base capturada en bind y restaurada/compuesta sin acumulación; rebind después de sustituir un rig.

Párpados usan `Mosquito_Shell` en `MosquitoSkin`, por lo que participan del ColorBinding existente categoría Mosquito. Ojos/pupilas conservan Mosquito_EyeWhite/Mosquito_Expression. No se añaden materiales que ignoren la personalización. Bootstrap ya llama ApplyAppearance al crear el decorado y actualiza apariencia de menú/lobby/juego; su persistencia real sigue a cargo de Director/Presentación. No hay cambios de red ni de autoridad de Gameplay.

## Comprobaciones y próximo lote

`FACE-FLIGHT-SOURCE-CHECK-20260912.json`: topología de cuatro caparazones en cinco aperturas; rayos frontales cubren670 muestras de blanco/pupila por lado cerrados y no ocultan la pupila abierta, incluyendo los extremos de mirada. Verifica además variación y continuidad de los canales de vuelo. No evalúa skin, rayos oblicuos, colisiones con la cabeza, ejes importados ni percepción temporal.

`SOURCE-CHECK-20260912.json`: sintaxis, topología previa, sockets contra el audit R3 existente y alcance/trípodes de SurfaceWalk. `check_mosquito_audit_math.py` conserva los cuatro casos de regresión de comparación de poses.

Preparado para el turno que conceda Director:

1. Generar exclusivamente mosquito con `build_mosquito_candidate.py`, guardar nuevos hashes y comprobar39huesos/15clips/siete sockets y materiales. Exportar cada especie por separado.
2. Ejecutar `audit_mosquito_candidate.py`:894frames corporales como antes,48 marcadores,20endpoints; añade12poses faciales por formato, comparación de pivots/deformación/skin y aislamiento de Root/Mouth/cuerpo. Auditoría requiere el R4 nuevo; no correrla contra R3 para reclamar compatibilidad.
3. Repetir soporte piso/pared/techo y revisar caída, pues los caparazones cambian la malla externa aunque no las patas. No asumir aprobación R3 heredada.
4. Vistas frente/35/perfil cara abiertas, medias y cerradas; extremos mirada y guiño por lado. `render_mosquito_witness.py` admite `--gaze-yaw`, `--gaze-pitch`, `--blink-left`, `--blink-right`. Captura temporal corta Fly/Hover normal; comparar ala/cuerpo/patas sin trasladar Root.
5. Director/Presentación importan, verifican curvas Wing por ruta real del Animator y bases de los seis controles, conectan VisualAttentionRig y registran ejes/quaterniones importados. Validar transición de estados y personalización guardada de ambas especies; cerrar la matriz de contextos de `LIVING-MENU-AND-GAME-EYES-ACCEPTANCE.md` con evidencia integrada.

P1 previo de patas cruzadas y otros pendientes visuales R3 siguen abiertos; esta entrega no los declara resueltos.

## Runner preparado, todavía sin ejecutar

`art_source/unity/characters/run_mosquito_candidate.py` muestra el plan por defecto y no inicia procesos. Sólo después de concesión explícita del Director se invoca con `--execute --slot-note "referencia de la concesión" --run-name r4-cpu2-01`. Usa una copia nueva de nueve módulos en `work/mosquito-candidate/<run>/art_source/unity/characters`; toda generación, auditoría y render queda allí. No reemplaza los activos canónicos del worktree ni los centrales y comprueba sus hashes al terminar. No promueve automáticamente.

Un Blender oculto por vez, dos hilos, timeout por paso y límite total12min, abortando ante el primer error. Lote: generación, auditoría temporal/facial, soporte,11 stills de frente/35/cara/perfil y posiciones de párpados/mirada, más24frames Fly y24Hover (dos ciclos cada uno,30FPS, velocidad1, Root fijo). Stills640px/8samples; secuencias384px/4samples. Guarda PID, tiempos, salida y hashes en runner-receipt.json. Estimación **4–7min** si no falla ningún gate, apoyada en R3 (generación6s, auditoría5s, soporte3s, tres vistas640px/8samples25s); no es un benchmark actual. Corregir un error o repetir exige informar al Director y respetar el tiempo de su turno.

Preparación verificada con el modo plan y análisis de sintaxis; aún no ejecutada contra Blender. Las secuencias son diagnóstico de autoría, no evidencia del menú a escala final ni del rendimiento del juego.
