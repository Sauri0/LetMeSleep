# Correcciones de la prueba de Branko — alfa

La prueba del usuario corresponde a la candidata basada en `e9d15e7`.
La tanda siguiente sigue en desarrollo y todavía no está publicada. Todos los
tests visibles deben usar el monitor principal horizontal; el vertical queda
fuera de las pruebas. No cerrar alfa ni abrir beta por resultados unitarios.

| Reporte | Trabajo integrado / responsable | Evidencia y cierre pendiente |
|---|---|---|
| Zumbido molesto | Presentación: sólo Flying/ApproachingSurface sin adhesión; gain del cue 0.35, rango de pitch estrecho; limpieza de loops al cambiar ronda/proxy. | Builder central regeneró los cuatro cues. Falta escuchar mezcla y verificar todos los cambios de estado en player. |
| F fija la mirada; piso/pared/techo/objetos | `c4b37e0`: orientación por ViewForward 3D proyectado y transporte de tangente con la normal; Gameplay prepara adquisición en bordes contiguos. | 110/110 CPU incluyendo seis casos de orientación. Pendiente contacto real, bordes, objetos móviles y cancelación; no aprobado. |
| Golpear agachado levanta al humano | `08ee42e`: mantener Crouch durante preparación, golpe y recuperación; cancelar Swat de pie al agacharse. | Check nativo sintético PASS. Falta transición y pelvis del humano real. |
| Muñeca se estira | `08ee42e`: resolver UpperArm/LowerArm por rotación con longitudes reales y alcance limitado; conservar offsets/escala/rotación local de Hand. | Seis checks de brazo/stance PASS en Unity. Falta agarre, intersecciones y fluidez en vista real. |
| Parpadeo deforma mentón/cachetes | `1361949`: filtrar normales/tangentes de cada morph fuera de sus triángulos deformados; conservar posiciones y párpados. Recertificación facial ejecutada. | Renders GPU antes/después muestran eliminación de surcos de mandíbula. Cuatro cierres después dan 0° de cambio fuera de ojos. Otro defecto confirmado: caras inferiores del párpado orientadas hacia adentro; candidato de modelo en preparación. No cerrar parpadeo completo todavía. |
| Pasos/carrera/escaleras | `a3a58a6`: cadencia horizontal limitada, sin acumulación de eventos, pitch 0.98–1.02. | 129 assertions CPU del reloj de audio. Mitigación: no sincroniza aún el apoyo visual; clips y fase de locomoción requieren revisión conjunta. |
| No se entiende picar, defenderse o acertar | `221aec2`: texto contextual y feedback sólo con StrikeImpact confirmado. `39d7aa2`: panel más legible. `fd1b9c8`: orientación de picadura/contacto y bloqueo de Head/Neck conservando ojos. | Bite 6/6 checks PlayMode; falta comprensión del jugador y captura de HUD 720/1080. Sin marcadores de picadura. |
| Control durante muerte/desmayo y cuerpo rígido | `5372268`: neutralizar entrada durante Falling/Fainted/Stunned/Recovering, conservar validación/ack y exigir soltar botones para rearmar. Ragdoll articulado pendiente de integración. | 104/104 CPU; no prueba de mouse real ni caída articulada. La física debe poder rotar el cuerpo libremente aunque el input esté bloqueado. |
| Movimiento y zoom mosquito a primera persona | `d2068dc`: distancia solicitada cero, cámara después de pose final, oclusión temporal de cuerpo propio y restauración. | 5/5 PlayMode camera checks. Falta vista real en paredes/techo, movimiento y transición con rueda. |

Evidencia central de esta tanda:
`N:/LetMeSleep/Validation/alfa3-corrections-20260913/`.
Los casos automatizados de cámara, brazo y picadura usan escenarios sintéticos;
no certifican calidad artística, percepción auditiva, input real o WAN.

La comprobación adicional RealHumanStrike ejecutó 72 muestras con prefab,
controlador y clips humanos reales: PASS numérico, sin alargamiento de segmentos.
El primer recibo omitió detalles por serialización; el revisor está corrigiendo
su exportador antes de cuantificar alcance residual y agarre. Se conserva ese
recibo original y no se lo usa para certificar calibración ni transiciones.

Comparación facial: GPU RTX 3060 Ti / D3D11 / URP Unity6000.3.24f1, meshes
horneadas con BakeMesh y materiales reales, misma luz/cámara/seis poses.
Carpetas bajo `N:/LetMeSleep/Validation/TeamRecovery/visual/FaceBlinkAB/`:
baseline `capture-20260913-010943-664`, filtro de normales
`capture-20260913-011323-362`. No es video de animación en partida. Las capturas
anteriores del renderer skinned directo se conservan pero no mezclan con este A/B.

La simulación ragdoll deberá tener una sola autoridad física, colisionar con
el entorno y quedar en una pose producida por contactos. No alcanza con mover
un cuerpo rígido o elegir una animación aleatoria. El módulo aislado del mosquito
se está preparando; motor, snapshots, recuperación y representación remota
todavía requieren integración y verificación.

La prueba con el amigo desde otra conexión queda para cuando pueda realizarla.
Crear sala local no la sustituye. Mantener separadas correcciones compiladas,
candidata empaquetada, publicación y aceptación final.
