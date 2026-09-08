# Perfil de atribución CPU 0.9

Fixture: `game/tests/performance09_profile.gd`. Usa Main, Client, World, UI y práctica reales. Las subclases envuelven métodos de producción mediante `super`, sin copiar sus algoritmos ni cambiar umbrales, cadencia, población o física.

Escenarios separados:

- `--profile-map=house`: 16 actores, cuatro humanos, 15 bots; jugador en `(0,0,6.5)`, W mantenida, giro de 180° cada cuatro segundos y órdenes periódicas a puertas, como `performance07_live`.
- `--profile-map=house-v1-1`: mismos actores, control y tiempos, pero conserva el spawn generado válido del jugador. No se presenta como el mismo recorrido visible de la casa autoral.

Ambos usan dos segundos de calentamiento y doce de medición, 1920×1080 internos, VSync desactivado y sin límite de FPS. El preámbulo de práctica también fija semilla 1; se registra su tiempo por separado. Se reinicia `client.playing` antes de publicar el roster final y se comprueba que World, Client y Simulation compartan mapa. Esto corrige el desajuste detectado en el benchmark anterior, que podía dejar renderizada la casa aleatoria del preámbulo mientras la autoridad ya usaba `house`.

La versión actual registra `renderer` y `rendering_driver` obtenidos del motor, así como hashes de DoorState/DoorCatalog. La última corrida usa explícitamente `--rendering-method gl_compatibility`; no cambia el renderer dentro de la fixture.

## Qué significan las mediciones

| Etiqueta | Alcance inclusivo |
|---|---|
| Practice.advance | Scheduler, observación, decisiones, inputs, autoridad y publicación cuando corresponde |
| Practice._publish | Snapshot, privado y callbacks síncronos de Client/UI/World/audio |
| Brain.decide | Decisión real completa, incluida navegación |
| Brain._path_direction | Revisión y seguimiento de ruta, y búsqueda estática cuando se reconstruye |
| Simulation.step | Tick autoritativo completo |
| Simulation.public_snapshot | Construcción real del estado público |
| Simulation.private_for | Construcción real del privado solicitado |
| Simulation.submit_input / action | Validación y aplicación/encolado de entradas reales |

`inclusive` es tiempo de pared alrededor de la llamada a `super`. Las etiquetas se solapan y **no deben sumarse como costes independientes**. `residual` resta sólo los hijos medidos directos: incluye trabajo no instrumentado y parte del registro del perfilador, no es tiempo puro de la función. La jerarquía y las llamadas quedan en `edges`; hay acumulación por frame de física y de proceso, seis testigos costosos por etiqueta y un histograma de ticks por frame renderizado.

`MapNavigation.path` es estático detrás de una constante de BotBrain, por lo que no puede interceptarse mediante estas subclases. Se cuenta cada reconstrucción usando `stats.paths` de producción. El tiempo de `_path_direction` cuando hay reconstrucción conserva el resto de su trabajo y no se atribuye íntegramente al método estático.

Sin `--view-scopes`, los callbacks separados de Client/World no quedan dentro de estos spans. La mezcla de audio y el trabajo nativo de render tampoco están cubiertos por wrappers GDScript. Los monitores de motor y los tiempos de frame acompañan el informe para ubicar esa diferencia. La instrumentación añade coste; las corridas con consumer facial concurrente son diagnósticos de atribución, **no benchmarks definitivos de FPS ni comparaciones antes/después**.

El segundo nivel agrega `_tick`, movimiento libre de mosquito, separación de cuerpos, concentración, ayuda, contactos adheridos, DoorState.step, asignaciones, emotes, consultas privadas humanas, lanzamientos, proyectiles, golpes, tareas y resultado. Son overrides que llaman a `super`. El movimiento humano y de aturdidos usa funciones estáticas Arena dentro del bucle original: queda en el residuo `_tick` junto con sus bucles y trabajo no instrumentado, no se etiqueta falsamente como una medida exacta de locomoción humana. La ausencia de llamadas a tareas en el escenario Sangre significa que esa rama no fue ejercitada.

## Reproducción y límites

La extensión actual requiere fuentes `.gd` originales. Es un diagnóstico **source-only**, no un gate del EXE empaquetado. Ejecutar desde la raíz del proyecto, en una ventana coordinada, con el Godot local:

```powershell
Godot_v4.5.2-stable_win64_console.exe --path game --audio-driver Dummy --rendering-method gl_compatibility --script res://tests/performance09_profile.gd -- --profile-map=house-v1-1 --view-scopes --natural-doors --report=C:/ruta/perf-view.json --concurrent-work=none
```

Cambiar sólo `--profile-map=house-v1-1` para la variante generada. El wrapper externo conserva stdout, stderr, exit code y hash del ejecutable; límite de proceso 50 segundos, más watchdog interno de 48 segundos. No escribe preferencias. Lee los ajustes de vídeo del cliente real y los registra; Dummy conserva el procesamiento de audio sin salida a hardware.

No hay objetivos nuevos de FPS ni relajación de validaciones. La reserva de ejecución se comunica por separado a Root y Visual.

## Tercer nivel: presentación y callbacks fuera de publicación

`--view-scopes` usa `performance09_view_instrumentation.gd` para generar copias aisladas en `work/perf09-view-instrumentation/<pid>/`. No escribe ningún módulo de producción. Las únicas sustituciones del cuerpo de Main/Client/World son sus factories Client/World/Actor; se exige una coincidencia exacta y reversibilidad de cada sustitución. La copia de Main cambia además únicamente el código final de salida para conservar exit 1 si falla el diagnóstico; ejecuta el mismo cierre real, drenaje de música y espera de red de 0,25 s.

Las subclases llaman a `super` con los mismos argumentos, señales, nodos y orden de callbacks. Actor sigue heredando de ActorView, conservando los casts tipados de World. Se registran huellas de todos los archivos generados y de las fuentes antes/después; cualquier cambio invalida la corrida.

| Etiqueta nueva | Alcance |
|---|---|
| Client._process | Sincronización visual, cámara y controles de presentación |
| Client._physics_process | Muestreo/envío de input en su callback real |
| Client._flush_game_hud | Actualización diferida/coalescida de HUD; fuera del callback de publicación |
| Client._snapshot / _private | Consumidores síncronos de ambos estados |
| World._process | Visibilidad, etiquetas y consultas de presentación |
| World.sync_actors / sync_doors / sync_pickups | Sincronización real por frame |
| Actor.human / mosquito.update_state | Actualización completa, separada por rol |
| Actor.human.pose / colliders | Pose/skin y actualización física visual del humano; el segundo es hijo del primero |
| Actor.<rol>.voice_process | Sólo cuando producción lo activa por voz reproducida; cero llamadas es válido |

La pose/collider/skin del mosquito queda dentro de su update_state; no se inventa una separación de métodos que producción implementa en línea. No se activa voz ni se omiten actores para provocar muestras.

`physics_rows` incluye sólo llamadas que entran con `Engine.is_in_physics_frame()`. Un callback idle no se carga al último contador de física. `process_rows` agrupa todos los spans por el contador de proceso y cada testigo caro declara su fase. El coste de las funciones de registro se suma una vez por enter/leave y se informa aparte; aún excluye dispatch del wrapper y pequeñas operaciones posteriores al último timestamp. Además se mide una calibración sintética de 1.000 pares de registro. Ninguna de esas estimaciones se resta para fabricar tiempo puro ni FPS sin instrumentación.

`--natural-doors` conserva la interacción normal de bots/jugador, eliminando sólo las órdenes globales de estrés del fixture. Se registran `door_policy`, órdenes y frames con movimiento. Dos segundos de calentamiento y doce de medición se mantienen. `--check-view-scopes` permite compilar las copias en headless sin instanciar Main ni guardar preferencias, dentro de una ventana de motor autorizada.
