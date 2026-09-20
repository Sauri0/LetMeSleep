# MODES-UI-001 — selección, HUD privado y espectador

Fecha: 2026-09-20 UTC. Trabajo realizado directamente en `N:/LetMeSleep/Repository`, rama `codex/v0.2.0`. Propiedad: Bootstrap, UI y `GameplayVisualPresenter` (extensión autorizada por CEO). Sin cambios propios Core/Gameplay/Online/Gameplay.Unity ni assets de mapas.

## Cambios entregados

- Sala: selector host de Sangre/Supervivencia/Tareas, valor de sólo lectura para invitados, mismo bloqueo de controles mientras se guardan reglas. Cambiar mapa/cantidad humana conserva modo/perfil; cambiar modo aplica perfil estándar (Tareas 120 s, otros 180 s) e invalida ready por Core. Ready/inicio comprueban mapa y catálogo del modo.
- Entrenamiento: selector de tres modos y catálogo de mapas instalado, instrucciones por rol/modo, configuración real de autoridad, repeat conserva modo/mapa/rol. Fallar al preparar devuelve una pantalla operable con explicación y libera latch; no deja «Preparando…» indefinido.
- Bootstrap crea config desde RoomRules y pasa proveedor local de objetivos a OnlineGameplaySession. El proveedor retorna vacío fuera de Tareas; Tareas llama `game.World.GetObjectiveDefinitions()`. `GameplayObjectiveCatalog.ValidateAuthoring(mapId)` permite saber si el prefab tiene contenido; Begin y mundo conservan validación geométrica real. La ausencia de catálogo bloquea de forma visible esa combinación, no elimina Tareas del selector. Completar catálogos reales de los cinco mapas sigue perteneciendo al ticket de técnica/contenido.
- HUD: Sangre conserva su contador; Supervivencia muestra mosquitos vivos; Tareas muestra progreso colectivo. Mosquitos ven vidas. La tarjeta privada humana resuelve objeto/acción locales, R mantenida, tiempo personal, porcentaje y fallos; completada/vencida/ruta bloqueada tienen textos distintos. Sin flechas, balizas ni coordenadas de tareas.
- Privacidad: `ModeHudText.LocalPrivate` comprueba actor, epoch y ronda; `PrivateTask` exige humano local no eliminado. No se consulta el private state del actor observado. La UI también oculta cualquier texto de tarea recibido para rol mosquito.
- Espectador: elimina el cuerpo visual; oculta retícula; Tab cambia sólo entre compañeros activos. `GameplaySpectatorCamera` mueve únicamente la cámara local, evita obstáculos del mundo y restaura componentes de cámara al desactivarse/salir/reiniciar. No modifica LocalActorId, dueño ni private state. Técnica controla collider/input de eliminados.
- Resultados muestran score y razón del modo, sin interpretar winner Unassigned como victoria mosquito. Logs de playtest añaden sólo modeId y counters públicos, jamás objectiveId/asignación.
- Probe de build admite `--lms-probe-mode blood|survival|tasks` y `--lms-probe-map <id>`; registra ambos en `player-probe.json` y falla explícitamente ante combinación ausente. Es un opt-in de development player, no ejecuta nada en lanzamiento ordinario.

La preparación guest propaga errores de `BeginGame`; Online gate `fe1f25c` (CEO) los convierte en `LocalRoundPreparationFailed` y no confirma Ack. Bootstrap recibe el evento de error y muestra resultado interrumpido. Host captura errores de preparación sin declarar una ronda iniciada.

## Contratos de contenido utilizados

Componente en `LetMeSleep.Content.Environment`: `GameplayObjectiveCatalog`, propiedades MapId/Entries y `void ValidateAuthoring(string expectedMapId)`. Getter mundo: `IReadOnlyList<ObjectiveDefinition> GetObjectiveDefinitions()` después de asignar MapRoot.

Claves locales de esta entrega: `task.isla.cabin_access`, `task.casa.bathroom_tile`, `task.camp.washroom`, `task.yacht.main_deck`, `task.port.lighthouse_floor`; acciones `task.action.hold_clean`, `hold_repair`, `hold_switch`. Se traducen a objeto y acción concretos en español. Un ID desconocido nunca se imprime como instrucción al jugador.

## Evidencia

Carpeta: `N:/LetMeSleep/Validation/V020/ModesUI`.

- `Player/compile.log`: compilación offline 8 módulos fuente actuales, netstandard2.1 + DEVELOPMENT_BUILD, **0 warnings / 0 errors**.
- `Editor/compile.log`: mismo conjunto, ruta UNITY_EDITOR, **0 warnings / 0 errors**.
- `checks.log`: **33 PASS / 0 FAIL**, contra fuentes reales de ModeHudText/AlfaModeText y dominio actual. Cubre assignment propio/ajeno/stale, ausencia de catálogo, estados privados, elección/ciclo de espectador sólo del equipo, score/copy de tres modos y claves de cinco mapas.
- Fuentes reproducibles versionadas: `docs/ceo/validation/Compile-ModesUi.ps1` y `ModesUiChecks.cs`. Compilación con DLL reales Unity/paquetes existentes; no se sustituyen APIs de dominio por stubs. Resto de referencias de arte/audio permanecen en Library; esto es compilación, no ejecución nativa.

Comandos del harness externo:

```powershell
& N:/LetMeSleep/Validation/V020/ModesUI/Compile.ps1 -Variant Player
& N:/LetMeSleep/Validation/V020/ModesUI/Compile.ps1 -Variant Editor
dotnet run --project N:/LetMeSleep/Validation/V020/ModesUI/Checks.csproj --no-launch-profile
```

## Aceptación nativa pendiente

CEO debe comprobar en 720p/1080p: room host cambia modo/mapa y guest sólo lee; ready se limpia; training entra/repite/sale en cada modo/mapa; texto privado y progreso se ven sin cortar ni revelar tareas rivales; elimina mosquito y Tab observa sólo aliado, pausa/salir/restauración de cámara funcionan; resultados/rematch mantienen reglas. También validar ambas identidades online y objetivos/rutas reales del contenido técnico. Esta entrega no certifica imágenes, WAN, build distribuible, rendimiento ni mapas listos.
