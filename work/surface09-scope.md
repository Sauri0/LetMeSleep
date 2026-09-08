# Caminar por superficies — alcance propuesto 0.9

Revisión de lectura, 2026-09-08. Pedido autorizado: el mosquito debe poder posarse y caminar por piso, techo y paredes. **Este documento no implementa ni valida la mecánica.** El checkpoint facial continúa por separado; no se ejecutó Godot, Blender o import para esta revisión.

## Qué falta hoy

- `Simulation._tick` cambia `perched` a `flying` con cualquier movimiento de longitud cuadrada mayor que .01 y luego llama al integrador de vuelo. Por eso WASD no puede caminar. El estado posado guarda una normal, pero no una referencia de soporte ni una dirección tangente persistente.
- `_try_perch` busca planos de los límites y caras de AABB estáticos a menos de .32 m, elige el más cercano y mueve directamente el centro. Comprueba un segmento y puertas, pero no conserva qué cara sostiene al insecto ni vuelve a validarla en cada tick. La adquisición y el tránsito deben compartir la misma prueba de cuerpo completo, sin atravesar una cara distinta.
- `MosquitoPose.orientation` proyecta el yaw mundial sobre la superficie; mirando perpendicular a una pared cae en un fallback. La dirección de marcha necesita un eje tangente público estable para que cuerpo, colisión de impacto y clientes no elijan orientaciones distintas.
- El cliente ya envía intención local, yaw, pitch e interacción a 30 Hz; práctica y online alimentan la misma simulación a 60 Hz. No es necesario transmitir posiciones, normales elegidas por el cliente ni una potencia/velocidad propia.
- La cámara del mosquito conserva vertical mundial, un brazo de .85 m y origen elevado .12 m. Sólo limita ese origen a los límites exteriores. En techo o pared, ese desplazamiento puede comenzar detrás del soporte antes de que actúe el brazo de cámara. Debe probarse su origen y recorrido contra el mapa real.
- `contact_orientation06_test` verifica seis caras y una mesa, pero explícitamente espera que mover despegue. Esa expectativa deberá cambiar por el comportamiento nuevo, conservando las pruebas de privacidad, normal exterior y golpe real.

Referencias: `game/scripts/simulation.gd` funciones `_tick`, `_try_perch`, `_surface_normal`, `_impact_actor`; `game/scripts/arena.gd` `move_body`, `_move_insect_part`, `flight_direction`, `step_mosquito`, `ray_map`; `game/scripts/mosquito_pose.gd:21`; `game/scripts/client.gd` cámara en `_process` y envío en `_physics_process`; `game/tests/contact_orientation06_test.gd:122`.

## Controles propuestos

| Situación | Control | Resultado autoritativo |
| --- | --- | --- |
| Volando cerca de una superficie válida | F, acción existente `perch` | Inicia una aproximación corta y comprobada al soporte más cercano; si el cuerpo no puede llegar, permanece volando. |
| Posado | WASD mantenido | Avanza por el plano tangente; soltar frena y conserva apoyo. No convierte automáticamente a vuelo. |
| Posado | Mirar con mouse | Cambia la mira libre; la dirección útil se proyecta sobre el soporte con fallback continuo cuando se mira perpendicular. |
| Posado | F o Space | Despega con salida corta por la normal exterior, sólo por espacio libre. Space continúa siendo altura auxiliar en vuelo. Requiere nueva pulsación para no despegar inmediatamente al llegar con Space mantenido. |
| Posado | Ctrl | No despega ni empuja dentro de la pared. La ayuda vertical auxiliar se usa sólo en vuelo. |
| Posado/volando | E mantenida | Ayuda válida al compañero tiene prioridad. Si hay carga válida de la marca propia, despega hacia ella mediante el mismo solver de vuelo/asistencia. No existe picadura por caminar sobre un humano. |
| Aturdido/eliminado o menú abierto | Movimiento | Aturdido/eliminado no camina. Menú envía intención cero; el apoyo se mantiene si aún existe. |

Conservar acciones configurables y etiquetas en preferencias. El servidor recibe la intención y valida transiciones; un cliente modificado no puede elegir soporte, normal, velocidad ni ignorar el estado. La nueva pulsación de Space se deduce de `_move.y` recibido, sin reutilizar la bandera humana `jump`.

Propuesta inicial medible para caminar: velocidad .65 m/s, aceleración 5 m/s², frenado 10 m/s², radio físico existente .04 m y separación normal de .005 m en superficies estáticas. Son valores de implementación a confirmar con el primer testigo real; no alteran velocidad de vuelo 3.8 m/s, hitboxes anatómicas, tiempos de golpe ni reglas de sangre/aturdimiento.

## Autoridad y geometría

Nuevo helper puro sugerido: `game/scripts/surface_locomotion.gd`. Recibe geometría inmutable de la sala y puertas de **esa instancia**, nunca un singleton dinámico. API propuesta:

```gdscript
find_support(position, aim_forward, geometry, doors, max_distance := .32) -> Dictionary
surface_frame(normal, previous_forward, yaw, pitch) -> Dictionary # forward/right
step_surface(actor, local_move, dt, geometry, doors) -> Dictionary
release_surface(actor, geometry, doors) -> Dictionary
```

Los resultados contienen `p`, `velocity`, `normal`, `forward`, `attached`, `reason` y una referencia interna de soporte. El actor conserva `_surface` con identificador estable de cara, contacto, normal, tangente y revisión/fingerprint del mapa. Los identificadores y puntos candidatos no se replican. La normal pública sólo procede del contacto realmente aceptado.

El integrador recorre pasos acotados a ≤.02 m, con tiempo efectivo de la simulación y prueba continua o subpasos suficientes del cuerpo contra AABB, barreras, soportes y OBB de puertas. El broad phase sólo descarta: la aceptación sigue comprobando las caras reales. El radio .04 y el hueco de puerta .14 permanecen iguales. No se transforma una puerta oblicua en una caja enorme ni se reduce el radio para pasar esquinas.

En cada paso:

1. Verificar que el apoyo anterior aún pertenece a la misma geometría y que existe espacio para el cuerpo.
2. Proyectar la intención sobre el plano, con dirección anterior transportada al nuevo plano cuando la proyección de la mira sea casi nula. No permitir NaN, inversión aleatoria del eje o movimiento normal hacia el sólido.
3. Avanzar hasta la primera restricción real y examinar caras contiguas. En una junta coplanar, continuar sin salto. En una esquina interior piso↔pared o pared↔techo, transferir el apoyo con trayectoria del centro continua y validada. En una esquina exterior, rodear el borde conservando la holgura del cuerpo; no interpolar el centro a través del sólido ni prometer un giro continuo usando sólo normales interpoladas.
4. Si no hay cara contigua válida, detenerse en el borde manteniendo el apoyo. F/Space permiten despegar. Una separación o hueco no se cruza mediante adhesión a distancia.
5. Revalidar tras la resolución de colisión con humanos. Si un humano aparta al mosquito del soporte, detener/desprender de manera física; no restaurar por fuerza el anclaje penetrando el cuerpo.

Las hojas móviles siguen excluidas como soporte: se bloquea acercamiento y marcha contra su OBB actual, y la política de ocupación de puerta ya considera el cuerpo del mosquito. No se posa sobre humanos ni proyectiles. Caminar en techo o pared no vuelve inmune al mosquito: el barrido manual y arrojables continúan usando la orientación/anatomía compartidas.

`Arena.ray_map` devuelve actualmente sólo `kind`, punto, normal y distancia para arquitectura. El helper necesitará identidad estable de cara o un índice de geometría de instancia, incluyendo los seis límites exteriores. **Coordinar con la generación modular de Root antes de fijar ese identificador.** Las cachés estáticas actuales de Arena se indexan sólo por `map_id`; reutilizar el mismo ID con dos seeds distintos sería incorrecto. No introducir una dependencia de estado global de sala para resolverlo.

## Contrato público, privado y visual

- Mantener `state="perched"` tanto quieto como caminando. `velocity` y `motion_speed` distinguen movimiento; `motion_phase` avanza por distancia recorrida real.
- Añadir `surface_forward: Vector3` normalizado y tangente, junto al `surface_normal` existente, sólo cuando hay apoyo real. Ambos son cero en vuelo, stun o eliminación. La orientación de picadura sigue derivada de la marca ya ocupada; no publica asignación libre.
- `MosquitoPose.surface_basis(normal, forward)` será una función pura consumida por `orientation` y, a través de ella, por ActorView y las cápsulas de impacto. La animación de patas usa velocidad/fase reales; no cambia la posición autoritativa ni oculta saltos de contacto con interpolación cosmética.
- El privado propio puede añadir `surface={attached, moving, can_detach, reason}` para indicación de controles. No necesita listar superficies cercanas, IDs de caras, rivales o marcas libres.
- Usar sólo valores compatibles con el codec actual, especialmente Vector3; no enviar Basis/Quaternion. Root decide el bump del protocolo conjunto con el nuevo mapa modular. La mecánica cambia semántica de controles aunque conserve el formato de entrada.
- Cámara TPS: Root aplica origen exterior respecto a la normal y prueba su recorrido contra arquitectura y puertas; conserva el horizonte mundial para no invertir controles al caminar por techo. Mostrar un mosquito realmente posado y sus patas apoyadas debe verificarse desde las seis orientaciones, con accesorios y en los bordes.

## Propiedad propuesta de archivos

| Responsable | Archivos / alcance |
| --- | --- |
| `/root/simulation` | `simulation.gd`, `arena.gd`, nuevo `surface_locomotion.gd`, `mosquito_pose.gd` sólo contrato de orientación acordado, nuevos tests `surface09_test.gd`, ajustes justificados de `contact_orientation06_test.gd` y pruebas de privacidad/autoridad relacionadas. |
| Root | `client.gd`, `network.gd`, `practice_session.gd`, protocolo/codec, fixtures ENet y cámara; API de geometría modular/seed común y su identidad. BotBrain sólo si hace falta enseñar al bot a despegar al quedar posado, sin otra optimización. |
| Visual | `actor_view.gd`, CharacterSkin y animación de patas/alas al caminar, sobre la misma `MosquitoPose` pública. No cambiar cápsulas ni escala por un ajuste visual. |
| UI | Texto contextual y bindings por contrato, sin solver o elección local de soporte. Entorno mantiene equivalencia malla/colisión del mapa modular. |

Confirmar asignación puntual de `mosquito_pose.gd` con Visual antes de editarlo; actualmente se comparte en orientación de impacto/render. No editar HumanPose o faciales para implementar caminar.

## Gates antes de aceptar la mecánica

- Caminar y frenar en las seis caras, mesas y segmentos de ambos pisos; tiempo/distancia a 60 y 20 Hz sin deriva normal ni aceleración dependiente de FPS.
- Piso→pared→techo, juntas coplanares y borde exterior con registros de centro/normal/forward por subpaso; cuerpo nunca dentro del sólido, tangente finita y sin teleport. Borde sin continuación detiene; no cruza huecos.
- Puerta cerrada y girando, muebles estrechos, barandas y soportes: movimiento y despegue bloqueados de forma consistente, espacio libre bajo puerta conservado; no adhesión a una hoja.
- Entradas duplicadas/reordenadas, stale y menú; apoyo sólo del servidor, F/Space por pulsación real, quietud sin flotación. Reset de ronda y dos salas con seeds distintos sin reutilizar apoyos/cachés.
- E ayuda sin despegue involuntario; foco propio despega y carga sin saltar paredes ni omitir tiempo; calendario/FIFO y sangre acumulada intactos. Impacto desde pared/techo da stun 35 s con ayuda total 4× en Sangre/Tareas; Supervivencia elimina como antes.
- Privacidad: `surface_forward/normal` cero con reserva libre y sin contacto; los campos internos de soporte no salen en snapshots. Candidato privado y golpe real usan la misma orientación y LOS.
- ENet con dos clientes: movimiento tangente sostenido, esquina y despegue observados por ambos, comparación con autoridad, duplicación/stale, reconexión de snapshot y reset de ronda. Práctica recorre el mismo camino de Sim; no un solver local alternativo.
- Testigo nativo de cámara y patas en piso, pared y techo, transición y golpe real. El gate geométrico no sustituye la inspección de apoyo visible. No declarar rendimiento 16 actores sin medir el candidato integrado.

## Estado al entregar este alcance

Sólo lectura y este documento. No cambios de runtime, tests, preferencias, mallas o protocolo; cero procesos Godot/Blender/import ejecutados por esta revisión. Implementación pendiente de luz de Root tras checkpoint facial.
