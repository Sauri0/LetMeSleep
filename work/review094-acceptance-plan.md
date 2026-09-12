# Revisión funcional independiente — 0.9.4 alfa

Base de preparación: `2094817f07fc6eaa1ea14c855139b468a5bbca60`. Este documento separa los contratos de aceptación de la evidencia de ejecución. Ninguna fila marcada como pendiente o bloqueada acredita la alfa.

## Matriz A01–A12

| ID | Gate funcional independiente | Evidencia mínima | Estado sobre la base |
|---|---|---|---|
| A01 | El catálogo jugable contiene sólo `house-patio-v1` / `Casa con patio`; `default_map_id()` devuelve ese ID; la casa tiene `authored_version=1`; lobby queda fuera; IDs heredados, versiones generadas y seeds no resuelven ni son jugables. Las copias devueltas no comparten estado mutable. | `review094_fixed_catalog_contract.gd`, reporte con hashes de prueba y fuente. | Preparado. La base no cumple: conserva `house`, `house-v3-*` y generación procedural. |
| A02 | Dos plantas a 0 y 3,2 m; caja del edificio 24 × 6,4 × 20 m; dos escaleras de 2,8 m, 16 pasos de 0,4 × 0,2 m y descansos; puertas interiores de 2 m y exteriores de 2,4 m. Todas las rutas bidireccionales completan con la autoridad física real, sin salto, teletransporte ni cruce de hoja abierta. | Contrato estructural, `Nav.path`, `Arena.step_human`, anchos físicos muestreados, recibos por ruta y revisión visual coordinada. | Bloqueado hasta incorporar el commit de M1 y conocer la forma final de los metadatos de circulación. |
| A03 | Solar 40 × 8,8 × 40 m, edificio dentro del solar, patio transitable, dos accesos exteriores en `(0,0,±10)`, caminos, cercos, vegetación y muebles. Humanos y mosquitos recorren interior ↔ patio. | Validación geométrica de `exterior`, rutas físicas en ambos sentidos, inspección visual y prueba jugable. | Bloqueado por mapa alfa. La casa de la base no acredita exterior. |
| A04 | Muebles apoyados sin solapes inválidos; cada tarea, pickup y punto funcional tiene aproximación alcanzable; spawns humanos/mosquitos/respawn caben, están separados y no bloquean la circulación. | Validación de soportes y AABB, rutas desde todos los spawns, interacción real de tareas/pickups y negativos mutados. | Especificado; ejecución pendiente del mapa y contenido finales. |
| A05 | No hay parpadeo, z-fighting o clipping visible; materiales, luces, sombras, puertas y movimiento son coherentes dentro y fuera. | Sesión renderizada coordinada, capturas/video, recorrido fijo y registro de defectos por ubicación. | Requiere sesión GPU coordinada por Dirección. |
| A06 | Selector visible sólo para admin, una única opción alfa, estado sincronizado; invitados no pueden cambiar mapa. | Pruebas de estado/UI y par host/invitado con intentos autorizados y rechazados. | Pendiente de UI/red integradas. |
| A07 | Lobby conserva ready, ajustes, roles aleatorios, inicio, fin y retorno durante varias rondas. | Integración de lobby + Simulation con al menos dos rondas y cierres normales. | Pendiente de integración. |
| A08 | Dos identidades EOS reales e independientes crean/unifican lobby, juegan varias rondas y observan cierre/retorno. La prueba WAN usa dos personas/equipos y redes físicas distintas. | Protocolo `review094-online-evidence-protocol.md`; logs sanitizados y recibos de ambos participantes. | Local pendiente de build EOS nativo. WAN con amigo no disponible todavía. |
| A09 | Entrenamiento opera con bots reales en casa y patio; ambos roles pueden moverse y completar/afectar tareas accesibles. | `BotBrain` real, recorridos interior/patio, rondas completas y estados finales. | Pendiente del mapa y bots integrados. |
| A10 | Versión alfa y launcher respetan orden alfa → beta → omega → delta → gamma; instalar/actualizar funciona y se rechaza una versión incompatible. | Pruebas del manifiesto/launcher sobre artefactos candidatos y negativo de incompatibilidad. | Pendiente de artefactos del Director. |
| A11 | Rendimiento medido en el build alfa, con escena, jugadores/bots y recorrido declarados; sin extrapolar mediciones de 0.9.3. | Perfil repetible, FPS/frame time, memoria, hardware, configuración y hashes. | Pendiente de build integrado y sesión GPU. |
| A12 | Código, ZIP, checksum, descarga y launcher corresponden al mismo commit aprobado. | Hashes cruzados, contenido del ZIP, instalación limpia, arranque y descarga. | Propiedad de Dirección; QA verificará el candidato final. |

## Reutilización válida de pruebas anteriores

Se pueden reutilizar como mecanismos, después de sustituir el mapa y los objetivos:

- `review092_house_contract.gd`: medición física de anchos, apertura de puertas, `Nav.path` y seguimiento con `Arena.step_human`.
- `review092_functional_approach_test.gd`: recorrido centro → ancla funcional → aproximación y sus negativos de metadatos.
- `map_tasks09_test.gd`: selección, desplazamiento y trabajo de tareas dentro de Simulation.
- `pickup07_support_checks.gd`: apoyo geométrico e interacción de pickups.
- `practice_test.gd`: rondas completas con `BotBrain` real.

No son evidencia alfa los corpus de seeds, fingerprints de generación, identidades `house-v3-*`, conteos agregados de casas procedurales ni una ruta ejecutada sobre `house`. Esas aserciones deben eliminarse de cada corrida alfa, aunque el seguidor físico o el validador geométrico se reutilicen.

## Negativos obligatorios del mapa integrado

El gate geométrico incluirá mutaciones controladas sobre una copia del mapa, sin modificar producción:

1. Quitar cada conexión interior ↔ patio y comprobar que el validador detecta la pérdida de conectividad.
2. Estrechar una puerta o circulación por debajo de la medida contractual y exigir rechazo físico.
3. Introducir un waypoint dentro de un obstáculo y comprobar que el seguidor no acepta proximidad como llegada.
4. Mover una tarea/pickup fuera de su soporte o dentro de una colisión y exigir rechazo.
5. Superponer spawns o colocarlos sin cabida y exigir rechazo.
6. Romper una aproximación funcional, duplicarla o usar un vector no finito y exigir rechazo.

Los negativos prueban que el gate puede fallar; sus resultados no sustituyen el recorrido de la geometría de producción.

## Registro de baseline

El primer intento de `maps_test.gd` se hizo antes de que el worktree tuviera `.godot/extension_list.cfg` y produjo errores de parse/autoload EOS. Las DLL estaban presentes; después de un escaneo headless, el mismo Godot 4.5.2 cargó la extensión y `maps_test.gd` terminó limpio con `checks=431 failures=0`. Por lo tanto, el stderr inicial se atribuye al estado de importación del worktree y no demuestra una carencia del binario.

El primer escaneo completo de assets terminó con código 1 al reimportar `Bangers-Regular.ttf`; el primer error fue `Parameter "p_ptr" is null` en `free_static (core/os/memory.cpp:183)`. Los scripts headless posteriores sí arrancaron y terminaron sin ese error. La importación completa de assets deberá confirmarse antes de A05/A11.

El gate nuevo de catálogo se ejecutó limpio sobre la base: `checks=24 failures=12`, código 1 esperado. Las fallas corresponden a las dos APIs alfa ausentes, el mapa fijo ausente y la aceptación/resolución todavía activa de `house` y `house-v3-*`. Este resultado sólo demuestra que el gate detecta el baseline anterior; no acredita la implementación alfa, EOS, red, render ni el mapa nuevo.
