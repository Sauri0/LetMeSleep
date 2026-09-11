# Equipo activo — pulido 0.9.1

Branko confirmó los SIETE puestos el 11 de septiembre. Este reparto sustituye los anteriores.

| Tarea | Modelo | Razonamiento | Propiedad |
|---|---|---|---|
| Director | GPT-6 Astra | Extra alto | Integración, contratos, red/EOS, build/versionado/publicación |
| Modelador 1 | GPT-6 Astra | Alto | procedural_house.gd, map_catalog.gd, map_navigation.gd, house_validation.gd, art_source/environments/**, game/assets/art/house/** |
| Modelador 2 | GPT-6 Astra | Extra alto | cosmetics.gd, art_source/characters/**, game/assets/art/characters/**, exportadores exclusivamente de personajes |
| Worker 1 | GPT-6 Astra | Alto | client.gd, arena.gd, actor_view.gd, human_pose.gd, human_presentation.gd, mosquito_pose.gd, surface_presentation.gd |
| Worker 2 | GPT-5.6 Sol | Alto | world.gd, frame_joinery.gd, house_details.gd, shaders/materiales exclusivos del entorno |
| Revisión funcional | GPT-5.6 Sol | Extra alto | game/tests/review091_*.gd, work/review091*; sin editar runtime |
| Revisar interfaz visual | GPT-5.6 Sol | Alto | ui.gd, avatar_preview.gd, iconos/recursos exclusivos UI |

## Objetivo y límites

- Casa lógica y variada: circulación útil, escaleras/descansos cómodos, proporciones y mobiliario apropiados.
- Cabeza/cuerpo/cámara coherentes durante movimiento y mirada; conservar defensa manual, hitboxes y mosquitos adheridos.
- Rostros/modelos mejores, opciones distinguibles, sin intersecciones; comprobar frente/perfil/espalda y animación.
- Superficies sin parpadeos ni cortes, materiales/luces creíbles dentro de caricatura lisa; no estética Disney o infantil.
- Humano compacto redondeado con pijama/pantuflas/gorro por defecto, mosquito alargado de alas finas. Personalización en menú con zoom/rotación.
- Windows, objetivo 1080p60 en GTX1660Ti; FPS sin límite por defecto, no prometer rendimiento no medido.
- Mantener online integrado. WAN real con amigo aún pendiente; Branko lo probará después del pulido.
- Revisión completa e integración antes de exportar/presentar una nueva candidata. No sobrescribir v0.9.0-rc.1.

## Coordinación obligatoria

Repositorio del Director: C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir, rama codex/0.9.1-video-polish.
El cwd de OneDrive no es el repo. Director entrega un worktree y commit base por tarea antes de editar.
Nadie está solo: no revertir cambios ajenos, no git add ., no reset/clean ni publicar/exportar independientemente.
No editar archivos ajenos; proponer contratos y cambios al propietario mediante Director. Worker 1 posee actor_view; Modelador 2 propone allí cambios de montaje de modelos.
Los IDs cosméticos se conservan; nuevos valores se acuerdan con UI y se revisa compatibilidad de red. Cambios de geometría/versionado procedural se acuerdan con Director.
Una sola sesión Godot/Blender/capturas/benchmark a la vez, con turno explícito del Director. Antes revisar que Branko no esté jugando. Lectura/edición independiente sí puede avanzar en paralelo.
No crear más agentes. Entregas con commits selectivos, archivos, pruebas, evidencia visual y límites pendientes.
Pruebas nuevas por implementación: modeler091_*, character091_*, motion091_*, environment091_*, ui091_* según propietario; reviewer usa review091_*.

## Base comprobada

Cambios locales previos del Director quedan registrados como base antes de abrir worktrees: techo único, giro al mirar abajo, preview facial neutral, unión de paredes, eliminación de caras interiores/duplicadas, dinteles cubiertos por losas, esquinas revestidas, suelos por regiones y molduras por planta.
Baseline final: house09_surfaces_test 707/0; house09_ceiling_test 3 mapas/0; frame07_joinery_test 992/0; house07_joints_test 603/0. Eso no sustituye QA visual ni el resto de regresiones.
Capturas locales verificadas: work/user-video-sep11/storey-trim. Video personal NO publicar.
Contexto: work/VIDEO-FOLLOWUP-0.9.1.md y final de work/WORKER1-HANDOFF-2026-09-09.md.
Cambio antiguo work/voice09-acoustics-results.json se preserva, fuera de esta entrega.

## Secuencia

1. Director sella la base y entrega worktrees. Cada dueño implementa en su área y pide dependencias precisas.
2. Modelador 1 define planta/metadata para que Worker 2 adapte luces y detalles. Modelador 2 acuerda opciones con UI y articulación con Worker 1.
3. Revisión funcional construye pruebas independientes. Director concede turnos de motor, integra y resuelve contratos.
4. Validar recorridos, puertas, ambos POV, bots, defensa, personalización en todos los ángulos, superficies en movimiento y rendimiento comparable.
5. Director exporta/prueba EXE, publica código y ZIP nuevo, informa exactamente lo comprobado y qué necesita prueba WAN.
## Worktrees entregados y contratos cerrados

Base común 59af8ad. Cada worktree parte de esa revisión, no de rc1.
Raíz de las seis copias: C:/Users/brank/Documents/Codex/2026-09-06/
- Modelador 1: lms091-house / codex/091-house.
- Modelador 2: lms091-characters / codex/091-characters.
- Worker 1: lms091-motion / codex/091-motion.
- Worker 2: lms091-environment / codex/091-environment.
- Revisión funcional: lms091-qa / codex/091-qa.
- Revisar interfaz visual: lms091-ui / codex/091-ui.

Director commit 7e2ce85: aplicación 0.9.1 y protocolo10; invitaciones antiguas se rechazan antes de conectar. Pruebas: invitación online217/0, sesión72/0, invitación LAN96/0 y conexión23/0. Solo pruebas locales/fixtures: no WAN.
Generador nuevo SOLO v2; house-v1-seed rechazado, sin reinterpretar ni mantener doble generador. QA autorizado a migrar fixtures de mapas en tests existentes, conservando exigencias y negativos de versiones inválidas.
Metadata de escaleras acordada: stair_light_anchors (p:Vector3,target:Vector3,range:float), layout_dimensions, stair_connections con extremos reales, circulation_routes. Modelador1 produce y Worker2 consume; propietario del contrato detallado Modelador1.
Catálogo facial primer tramo conserva todos los IDs/nombres/conteos (acuerdo Modelador2/UI).
QA mínimos de circulación: pasillo/descanso>=1.50m, puerta>=1.30m (plan conserva2m), escalera>=1.40m (plan2.8m), huella>=.28m, contrahuella<=.22m; verificarlos geométricamente, no confiar en metadata.
