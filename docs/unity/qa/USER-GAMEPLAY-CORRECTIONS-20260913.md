# Correcciones de la prueba de Branko — alfa

La prueba del usuario corresponde a la candidata basada en `e9d15e7`.
La tanda siguiente sigue en desarrollo y todavía no está publicada. Todos los
tests visibles deben usar el monitor principal horizontal; el vertical queda
fuera de las pruebas. No cerrar alfa ni abrir beta por resultados unitarios.

| Reporte | Trabajo integrado / responsable | Evidencia y cierre pendiente |
|---|---|---|
| Zumbido molesto | Presentación: sólo Flying/ApproachingSurface sin adhesión; gain del cue 0.35, rango de pitch estrecho; limpieza de loops al cambiar ronda/proxy. | Builder central regeneró los cuatro cues. Falta escuchar mezcla y verificar todos los cambios de estado en player. |
| F fija la mirada; piso/pared/techo/objetos | `c4b37e0` orientación 3D; `e87ffcd` adquisición local de superficies contiguas sin teletransporte. | 8/8 casos nativos PhysX: piso–pared–techo y regreso, borde convexo, soporte móvil, gaps y F. Falta recorrido del player y percepción en mapas reales. |
| Golpear agachado levanta al humano | `08ee42e`: mantener Crouch durante preparación, golpe y recuperación; cancelar Swat de pie al agacharse. | Check nativo sintético PASS. Falta transición y pelvis del humano real. |
| Muñeca se estira | `08ee42e` longitudes reales; `3582d72` misma trayectoria temporal que colisión y orientación de mano/herramienta con offset real. | 72 muestras con rig real sin estirar. Otros 54 escenarios con Authority real confirmaron desajuste de matamoscas de hasta 65.54 cm antes. Medición posterior realizada, análisis y vistas en movimiento pendientes. |
| Parpadeo deforma mentón/cachetes | `1361949` filtro de normales y `a758252` modelo canónico con 120 caras inferiores de párpados orientadas correctamente. | A/B GPU: sin triángulos de mentón/cachete y cierre completo de ambos ojos, cabeza quieta/girada. Revisión independiente acotada; forma abultada y parpadeo continuo en juego pendientes. |
| Pasos/carrera/escaleras | `a3a58a6` mitigación de cadencia/pitch; `545171d` reloj único y `3e3c57f` conexión por actor sólo con cuatro clips válidos. | 16 escenarios de frecuencia del reloj y compilación del seam; aún sin clips nuevos importados. Humanos tiene slot para export funcional. Pendiente audio/apoyos y cambios de Animator en player. |
| No se entiende picar, defenderse o acertar | `221aec2`: texto contextual y feedback sólo con StrikeImpact confirmado. `39d7aa2`: panel más legible. `fd1b9c8`: orientación de picadura/contacto y bloqueo de Head/Neck conservando ojos. | Bite 6/6 checks PlayMode; falta comprensión del jugador y captura de HUD 720/1080. Sin marcadores de picadura. |
| Control durante muerte/desmayo y cuerpo rígido | `5372268`: neutralizar entrada durante Falling/Fainted/Stunned/Recovering, conservar validación/ack y exigir soltar botones para rearmar. Ragdoll articulado pendiente de integración. | 104/104 CPU; no prueba de mouse real ni caída articulada. La física debe poder rotar el cuerpo libremente aunque el input esté bloqueado. |
| Movimiento y zoom mosquito a primera persona | `d2068dc`: distancia solicitada cero, cámara después de pose final, oclusión temporal de cuerpo propio y restauración. | 5/5 PlayMode camera checks. Falta vista real en paredes/techo, movimiento y transición con rueda. |

Evidencia central de esta tanda:
`N:/LetMeSleep/Validation/alfa3-corrections-20260913/`.
Los casos automatizados de cámara, brazo y picadura usan escenarios sintéticos;
no certifican calidad artística, percepción auditiva, input real o WAN.

La comprobación adicional RealHumanStrike ejecutó 72 muestras con prefab,
controlador y clips humanos reales: PASS numérico, sin alargamiento de segmentos.
El primer recibo omitió detalles por serialización; la repetición 012237 conserva
todos los datos. Reveló clamp en 12/54 muestras IK y desajuste de herramienta,
por lo que no se usó para aprobar calibración. Los siguientes escenarios
AuthorityStrike emplean comandos y barridos reales: baseline 013849 y post
014832 bajo `N:/LetMeSleep/Validation/AuthorityStrike-20260913/native/`.

Comparación facial: GPU RTX 3060 Ti / D3D11 / URP Unity6000.3.24f1, meshes
horneadas con BakeMesh y materiales reales, misma luz/cámara/seis poses.
Carpetas bajo `N:/LetMeSleep/Validation/TeamRecovery/visual/FaceBlinkAB/`:
baseline `capture-20260913-010943-664`, filtro de normales
`capture-20260913-011323-362`, párpado inferior corregido
`capture-20260913-012911-708`. No es video de animación en partida. Las capturas
anteriores del renderer skinned directo se conservan pero no mezclan con este A/B.

La simulación ragdoll deberá tener una sola autoridad física, colisionar con
el entorno y quedar en una pose producida por contactos. No alcanza con mover
un cuerpo rígido o elegir una animación aleatoria. El módulo aislado del mosquito
se probó de forma aislada: primera caída con contacto pero separación de
articulaciones de 20.39 cm y sin reposo, por lo que falló el límite de 12 mm.
`4078e84` corrige inicialización y añade diagnóstico por articulación.
La segunda prueba también falló: gap máximo 13.11 cm en Leg202.R, paso 18;
anclajes iniciales coinciden y no hay desfase Transform/Rigidbody. La
inestabilidad se produce durante el contacto y no entra en reposo a 10 s.
El tercer candidato `1b17f0d`, con inercia/solver de contacto ajustados, falló
antes de simular: tensor de inercia identidad en comprobación inicial.
Recibo `ragdoll-tests-v3.xml`; autor corrigiendo. Módulo sigue sin activación.
Motor, snapshots, recuperación y
representación remota todavía requieren integración y verificación.

`57081c6` añade un protocolo opt-in separado de poses de 518/546 bytes y gate
por actor/vida/revisión/tick. Nueve casos ejecutados directamente en Editor
pasan; revisión independiente corrigió dos fallas de replay/revisión cero.
La repetición CPU con regresión aislada del watermark también pasa 9/9.
Todavía no está conectado a OnlineGameplaySession ni negocia perfiles.

La prueba con el amigo desde otra conexión queda para cuando pueda realizarla.
Crear sala local no la sustituye. Mantener separadas correcciones compiladas,
candidata empaquetada, publicación y aceptación final.
