# Superficies: API implementada y evidencia

Checkpoint de fuente, 2026-09-08. No es una validación del EXE, la cámara o la animación nueva. Root integra esos consumidores y la generación de mapas; Visual integra las patas.

## Contrato integrable

`Simulation.submit_input` conserva su firma. `move.x/z` son intención lateral/adelante-atrás. `move.y > .5` detecta Space; sólo una nueva pulsación recibida mientras el mosquito ya está `perched` solicita despegue. Una pulsación retenida antes de posarse no lo hace despegar. Secuencias viejas/duplicadas y datos no finitos se rechazan antes de modificar ese estado. El timeout existente de .40 s sustituye intención por cero.

`action(...,"perch")`, F configurable, tiene tres resultados:

- Volando: busca un apoyo estático válido dentro de .32 m e inicia aproximación física a un máximo de 1.2 m/s. Permanece `flying` hasta llegar; no cambia de posición durante el despacho de F.
- Aproximándose: otra F cancela la aproximación.
- Posado: F o una nueva Space solicitan vuelo. Se comprueban .035 m libres por la normal exterior; un paso bloqueado conserva apoyo y devuelve motivo. La salida inicial es velocidad .8 m/s por esa normal, sometida después al integrador de vuelo normal.

Posado, WASD camina a .65 m/s, acelera a 5 m/s² y frena a 10 m/s². Ctrl no presiona el cuerpo dentro del soporte. El rumbo inicial procede de la mira proyectada, con fallback del eje superior de cámara; después el delta de yaw gira el rumbo alrededor de la normal del soporte. Pitch es mirada libre. Al cruzar una esquina se transporta ese rumbo y velocidad al plano siguiente: W no se invierte al llegar al techo.

E válida de ayuda mantiene su prioridad y permite ayudar quieto sobre el soporte. E de concentración válida libera el apoyo antes de la asistencia de vuelo y conserva la carga de 1.2 s, LOS y calendario. La autoridad sigue siendo la única que decide acople, ayuda e impactos.

Campos públicos del mosquito:

| Campo | Significado |
| --- | --- |
| `state="perched"` | Quieto o caminando con apoyo real. No se añade un estado de animación distinto. |
| `surface_normal: Vector3` | Normal unitaria exterior del apoyo; en picadura sigue la normal de la zona ya ocupada. Cero en vuelo, stun o eliminación. |
| `surface_forward: Vector3` | Rumbo tangente unitario, perpendicular a la normal, sólo cuando está posado. Cero en otros estados. |
| `velocity` | Velocidad sobre la tangente final; tras girar sigue el nuevo plano. |
| `motion_speed` | Metros recorridos por segundo. |
| `motion_phase` | Radianes: avanza `distancia / .15 * TAU`, envuelto a un ciclo. Usa los tramos efectivamente recorridos en una esquina. |
| `grounded` | Hay apoyo, también sobre pared o techo; leer junto con `state`. |

`MosquitoPose.surface_basis(normal, forward)` devuelve Basis pura para uso local. `orientation(actor)` la consume en `perched`; `collision_segments` y ActorView obtienen así la misma orientación pública. No se transmite Basis/Quaternion.

Privado propio nuevo: `surface={attached:bool,moving:bool,can_detach:bool,approaching:bool,reason:String}`. No contiene ID, cara, ubicación de apoyo candidato ni lista de superficies. `_surface`, `_surface_pending`, sus IDs derivados de mapa/índice/cara y el historial de rumbo permanecen dentro de cada instancia de Sim. El auditor de red prohíbe `surface` y `support_id` públicos y valida que ese privado sólo llegue al mosquito.

## Geometría y mapas

Radio de locomoción .04 m, separación del centro al plano .045 m. Sigue la colisión conservadora de desplazamiento existente y el hueco de puerta .14 m. Las caras de AABB contiguas se resuelven con subpasos de recorrido ≤.015 m y pruebas de espacio completo ≤.01 m. Las puertas conservan su OBB real y no se aceptan como apoyo.

La trayectoria del centro al rodear un borde exterior sigue la caja expandida. En el vértice, puede quedar a `sqrt(2)*.045 = .06364 m` del borde físico; no se interpola su centro a través del sólido. La normal pasa a la cara contigua. Este límite se comunicó a Visual para que el apoyo de patas no presuponga un plano infinito detrás de cada normal. Si no cabe la continuación, se detiene y permite despegar; no salta huecos por adhesión.

`Arena._map` usa `Maps.get_map(map_id)` y conserva sólo geometría inmutable en su caché por ID, igual que sus obstáculos/índices. Root define IDs únicos por revisión+seed. El helper `SurfaceLocomotion` mantiene soporte e índice de caras por instancia; las puertas se pasan por llamada. Un apoyo de otra identidad de mapa falla y se libera en el lugar, sin corregir hacia otra sala.

Archivos runtime de esta implementación: `game/scripts/surface_locomotion.gd`, `simulation.gd`, `arena.gd`, `mosquito_pose.gd`. No se editaron MapCatalog, DoorCatalog, MapNavigation, Client, Network, PracticeSession, ActorView, Skin ni GLB.

## Pruebas realizadas

Todas las siguientes ejecuciones de fuente fueron headless, con exit 0 y sin errores, en la ventana coordinada. Logs originales bajo `work/surface09-<suite>.log`:

| Suite | Comprobaciones | Cobertura pertinente |
| --- | ---: | --- |
| `surface09_test` | 6722 | Seis caras; viaje/frenado a20/60 Hz; suelo→pared→techo sin invertir W; borde exterior de mesa; juntas coplanares; cuerpo libre en cada tick; seed/apoyo aislado; Space edge y stale/duplicados/no finitos; privacidad; hueco bajo puerta; E/foco real y ayuda posada4×. |
| `contact_orientation06_test` | 306 | Orientación de contacto, reservas privadas y resultados del impacto. |
| `rules_test` | 1624 | Reglas previas, adquisición de superficies y ciclos de ronda. |
| `doors07_test` | 342 | OBB, exclusión de la hoja como apoyo, ocupación y bots con puertas. |
| `focus_combat_test` | 145 | Carga, acople, LOS y tiempos de extracción. |
| `stun_help_test` | 297 | Stun35s, ayuda total4×, recuperación y Supervivencia. |
| `mosquito_impact07_test` | 1492 | Anatomía compartida y orientación de impacto. |

Las pruebas previas de posarse se actualizaron sólo donde esperaban acople instantáneo: ahora esperan .4 s para observar la aproximación y mantienen sus verificaciones de destino/normal. La prueba de despegue ahora identifica una nueva Space; WASD queda cubierto como marcha en la suite nueva. No se ampliaron radios, rangos de golpe o tolerancias para hacer pasar esos casos.

Tras la tabla se corrigió el acumulador de fase para sumar ambos tramos de esquina, en vez de su cuerda, y se limpia el motivo al cancelar. La repetición final de `surface09_test` pasó **6722/6722**, exit0 y sin errores; salida original `work/surface09-final.log`. Se ejecutó en la siguiente ventana coordinada después de la liberación de UI.

## Pendiente de integración

Cámara TPS exterior en las seis orientaciones, patas/alas, snapshot/input con dos clientes ENet, mapa generado por seed, emotes y testigo nativo. Esos puntos no quedan certificados por los gates de fuente anteriores. Sin cambios de preferencias ni publicación/commit desde este subtask.
