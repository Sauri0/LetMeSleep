# Auditoría de referencia Godot para Unity alfa

Fuente leída en la copia N: del repositorio, commit `0ea6cca`, 2026-09-12. No se ejecutó ni modificó Godot. Los resultados antiguos no acreditan Unity. Líneas sirven para localizar el código histórico, no para indicar propiedad de implementación nueva.

| Referencia histórica | Qué hace realmente | Decisión Unity |
|---|---|---|
| `game/scripts/arena.gd:174` `flight_direction`, `:183` `step_mosquito` | W combina yaw/pitch, ascenso mundial, aceleración y freno; convención -Z | Conservar intención; reescribir +Z Unity y validar polos/diagonal/frenado a 30 Hz |
| `arena.gd:21–36`, `:325` `step_human` | Velocidades, gravedad, salto, crouch y step; cápsula humana 1.95/.60, mosquito .04 | Conservar mecánicas, NO dimensiones: Director da humano 1.72/.25 y mosquito .055; medir geometría nueva |
| `arena.gd` `world_bounds`, `floor_below`, `move_human` | Solar separado de edificio, puerta actual en barrido vertical y apoyo | Conservar invariantes con colliders Unity y SurfaceId, no copiar cachés/índices AABB |
| `game/scripts/human_pose.gd:176–214` | Yaw libre, seguimiento de torso amortiguado, inspección, vista y alcance anatómicos compartidos | Conservar intención; contrato cinemático M1/W2 nuevo, no copia literal de huesos/offsets |
| `game/scripts/client.gd` `_movement_view`, `_physics_process` | Cámara local, input a 30/s, mirada libre del mosquito quieto | Conservar vista inmediata + comandos; no doble simulación por Update y FixedUpdate |
| `game/scripts/mosquito_camera.gd` `target`/`resolve` | Dos barridos y corrección de origen solapado, zoom, marco de superficie | Conservar principio; verificar overlap/normal en PhysX, sin copiar márgenes sin escala |
| `game/scripts/surface_locomotion.gd:55`, `:135`, `:252` | Adquisición host, tangentes, desprendimiento y continuidad entre caras; identidad depende de AABB/axis/side | Reescribir con SurfaceId estable y puntos locales. No tratar límites invisibles del solar como paredes/techo posables |
| `game/scripts/simulation.gd:288`, `:332`, `:432` | Secuencias, held y ACK de revisión de vista, acciones separadas | Conservar invariantes; usar PUID autenticado y ActorId de sesión. Revisiones jamás cruzan rondas |
| `simulation.gd:702–786` `_assign`/`_serve_assignments`/`_zone_pose` | Asigna zona a cada mosquito, prohíbe anterior y distribuye presión | RETIRAR. No port ni fallback: sin zonas asignadas, marcadores ni rotación |
| `simulation.gd:787–829`, `:1790` | Preparación asistida hacia marca a 1.6 m, ancla cerca de zona, detach reasigna otra | REESCRIBIR proximidad/contacto sin atracción remota; detach no cambia objetivo por sorteo |
| `simulation.gd:830–1040` | Rayo manual, hombro/alcance, elección de mano y barrido durante golpe; límite de obstáculos | Conservar defensa manual y barrido. Rehacer contra rig nuevo, sin búsqueda de marca y sin automatizar la mira |
| `simulation.gd:1144` `_kill` | `lives=0`; survival elimina, otros modos aturden 35 s desde impacto | Sangre usa caída recuperable; no trasladar nombre Kill, vidas cero ni 35 s. Tareas tres vidas queda para beta |
| `simulation.gd:1194–1242` | Ayuda visible a .8 m, acelera 4× reloj, recuperación limpia input | Conservar rescate/cancelación, usar perfil nuevo; distinguir caída aérea de Stunned y no apilar ayudantes |
| `simulation.gd:585–588`, `:1676` | Sangre compartida con tope global, cuota/tiempo; no desmayo humano por extracción completa | REESCRIBIR extracción continua y desmayo. Perfil por víctima explícito, regla de simultaneidad y protección al despertar |
| `game/scripts/bot_brain.gd:233–306` | Mosquito lee su assignment privado, persigue marca; ayuda, retirada y rutas | Mantener percepción/ruta/rescate; sustituir assignment por búsqueda local de contacto. No reusar información privada para obtener ventaja |
| `game/scripts/practice_session.gd:25`, `:61`, `:87` | Misma Simulation, scheduler bots, reinicio conserva mapa | Conservar misma autoridad; sólo Sangre visible en alfa, RoundId nuevo en restart |
| `game/scripts/projectile_collision.gd` | Barridos continuos de cápsulas/herramientas y hojas orientadas del mapa | Referencia para pruebas geométricas; lanzamientos/catálogo completo corresponden a omega, no inflar alfa |

## Defectos y dependencias que no deben reaparecer

1. El permiso de posarse no surge de una caja de límites invisibles; patio abierto no tiene techo físico ni superficie posable en el cielo.
2. La cámara no manda posición al host ni modifica alcance. Mirar abajo no puede bloquear yaw o voltear torso por ambigüedad de pitch.
3. Alas/cosméticos no deben aumentar radio de daño ni altura del actor. El collider nuevo no puede heredar 60 cm de radio humano por copiar constantes.
4. Las hojas de puerta se resuelven por mapa/surface activo, tanto movimiento como rayos, cámara, golpes y anclas. No `map_id == "house"`.
5. Una animación de palmada no acredita contacto; daño y trayectoria de palma deben compartir objetivo/tiempo cinemático, sin callbacks dependientes de visibilidad.
6. Ninguna ruta «libre» atraviesa un hueco de escalera sin apoyo humano. Vuelos y humanos tienen consultas y grafo adecuados a su forma; no usar navegación humana como permiso de vuelo.
7. Recuperar o abrir menú limpia input viejo. Al separarse de techo o cuerpo, validar trayectoria y espacio; no desplazar al mosquito a través de pared para disimular penetración.
8. No portar HUD de marks/assignment ni controles de modos todavía ausentes. El hint contextual de proximidad no señala un punto sobre la piel.

## Evidencia de esta auditoría

Lectura dirigida de los scripts anteriores y referencias oficiales Unity 6.3 citadas en ALFA-GAMEPLAY.md. No ventanas, editor, compilación Unity, playtest, rendimiento, WAN ni cambios a settings. El siguiente lote necesita contratos Core y rigs/colliders para convertir estos invariantes en pruebas ejecutables; no faltan permisos del usuario para continuar dentro de la coordinación del Director.
