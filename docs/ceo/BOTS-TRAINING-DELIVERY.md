# Bots de entrenamiento — contrato y evidencia

Fecha: 2026-09-20. Alcance: BotController, BotPatrol, GameplayBotNavigation, enlace Runtime/World y pruebas propias. Fuente de decisiones: docs/ceo/definicion-v020/respuestas-20260920-065440/DECISIONES.md, J25–J32. Ninguna habilitación de bots online.

Integración posterior CEO: Runtime ya proporciona contexto por bot: inventario
privado propio, pickup activo público concordante, herramientas con primer
contacto visible y desvío estimado por rutas authored abiertas. ContactPoint
procede del collider observado; el bot no apunta a una raíz fuera del collider.
Velocidad para ETA: observada entre1 y3,8m/s en vuelo,0,65 sobre superficie.
No certifica una ruta física completa ni acceso a inventarios de otros actores.
`horizon-bots-authority-native-01.xml`:71/71PASS, incluye32bots y9horizonte.
`vfx-perception-native-02.xml`:18/18PASS, incluidos4negativos/positivos de
percepción física de herramientas. Backend J32 incorporado posteriormente, según la sección siguiente.

## J32 — backend de replanteo integrado (2026-09-20)

El entrenamiento conecta la intención del Controller con la patrulla propia del actor. Tras 150 ticks sin mejorar al menos 0.15 m el mejor registro de distancia al waypoint, invalida el pasaje utilizado durante 180 ticks. DirectionTo humano y Explore mosquito consultan la misma exclusión temporal: el pasaje vuelve a ser elegible exactamente al alcanzar BlockedUntilTick. La decisión siguiente puede elegir otra ruta topológica, aunque tenga un rumbo parecido. El bloqueo es local a ese bot/ronda y no cambia la puerta física.

El progreso exige acercarse al waypoint real o avanzar de waypoint; desplazarse lateralmente o repetir una oscilación ya recorrida no reinicia el reloj. Un waypoint interior queda estable hasta llegada o invalidación. El conteo ocurre antes de Steer, por lo que Steer=0 también identifica atasco, pero sólo durante intención sostenida de movimiento. Estar quieto, trabajar, rescatar, picar, mantener un ataque, emitir una acción de interacción/equipo o estar incapacitado reinicia el seguimiento. Se eliminó el reloj autónomo de Explore que podía acumular durante una interrupción.

BotNavigationContext ofrece ReadProgress e InvalidatePassage; BotNavigationProgress publica clave estable de waypoint, ID opcional del pasaje y distancia restante. Se conservan los constructores anteriores de BotObservation. GameplayRuntime proporciona ContextFor(actorId) y state.HostTick+1 a las rutas, el mismo tick que recibe Decide. El overload antiguo de DirectionTo conserva compatibilidad; entrenamiento usa el overload temporal. ConfigureModeMap crea una navegación nueva por ronda, y Controller reinicia sus observables por identidad/epoch/ronda.

Cuando la intención final cambia para recoger una herramienta, perseguir, evadir o rescatar, no se bloquea un pasaje de tarea/exploración abandonado. Esas decisiones usan objetivos observados o proyección del movimiento libre; donde no hay un pasaje conocido, queda el replanteo direccional local existente. No se inventa una ruta física para perseguir actores ni para el interior de una región.

Evidencia J32: `N:/LetMeSleep/Validation/V020/BotReplanIntegration/cpu-results.txt`, 54/54 CPU (32 bots existentes, 2 rutas existentes y 20 casos nuevos). Compilación offline Player y Editor de dominio/adaptador: 0 advertencias y 0 errores. Gate EditMode nativo definitivo: `N:/LetMeSleep/Validation/V020/bot-replan-native-02.xml`, 54/54 PASS, 0 fallos, 0 omitidos, en Unity 6000.3.24f1/Windows. La ejecución01 seleccionó sólo dos casos por namespace incorrecto y no se usa como evidencia de este conjunto.

`N:/LetMeSleep/Validation/V020/bot-navigation-physics-native-01.xml`: los 4/4 GameplayBotNavigationContextPlayModeTests pasan en Unity 6000.3.24f1/Windows: contexto creado antes de la patrulla, aislamiento por actor, reapertura329/330 y nueva instancia ConfigureModeMap sin exclusiones heredadas. El archivo combinado contiene además pruebas ajenas de steering y no se presenta como PASS total. Los tres scripts nuevos ya tienen .meta generados por Unity.

Esta evidencia no acredita reparación de las rutas físicas de Casa, comportamiento completo en los cinco mapas, dificultad final, gráficos ni WAN. La física/steering sigue en revisión independiente. No se habilitaron bots online.

## Contrato

Se conserva exactamente el constructor previo de BotObservation (incluido TaskDirection). Overload nuevo añade como último parámetro `BotTrainingContext training`; el contexto es opcional y exclusivamente de datos:

- `BotTrainingContext(IReadOnlyList<BotToolOpportunity> visibleTools, float rescueTravelSpeed)` conserva el overload de dos argumentos. Nuevo overload de tres añade `ToolPickupSnapshot? equippedPickup`; propiedad pública `EquippedPickup`.
- `BotToolOpportunity(ToolPickupSnapshot pickup, float detourMeters, Float3 contactPoint)`: sólo pickups actualmente observables con LOS; DetourMeters es desvío estimado por topología abierta. El constructor de dos argumentos conserva compatibilidad y usa pickup.Position como punto de contacto.
- `VisibleTools` copia inmutable del listado. `RescueTravelSpeed` debe ser positiva y finita; sin contexto se usa 1 m/s como estimación conservadora, no información oculta.
- OwnPrivate se valida contra Self.ActorId y sólo informa inventario/tarea propios.
- BotController expone `SelectedActorId`, `ReplanCount`, `BlockedUntilTick` y `BlockedDirection`. El overload con BotNavigationContext conecta la observación de waypoint y la invalidación real de pasajes descritas en J32; los overloads anteriores siguen disponibles.

## Comportamiento implementado

Reacción inicial a un actor observado: 8–12 ticks deterministas (267–400 ms a 30 Hz). Runtime llama Decide cada 3 ticks, por lo que observa 9 o 12 ticks efectivos (300/400 ms). Memoriza únicamente último snapshot/contacto observado, incluido rumbo/velocidad públicos; a 90 ticks sin observación elimina el recuerdo. No extrapola posiciones ocultas ni ataca/rescata objetivos recordados sin visibilidad actual. Identidad, epoch o ronda nuevos limpian memoria y secuencias.

Humanos interrumpen tarea ante un mosquito visible a menos de 2 m, acercándose, O picando al propio humano (OR literal J28). Amenazas actuales tienen prioridad sobre recuerdos/otros enemigos. Al terminar amenaza, conserva el flujo TaskDirection/UseHeld existente y su tarea privada. Esto no concede conocimiento de tareas enemigas.

Mosquitos puntúan distancia, visibilidad, inmovilidad observable y golpe público en curso; penalizan 8 puntos repetir víctima durante 240 ticks desde su último attachment. Los pesos son concreciones técnicas reversibles, no cifras nuevas elegidas por Branko. No leen progreso/asignaciones privadas de víctimas. Supervivencia evade humanos tras reaccionar; conserva la posibilidad de rescatar aliados válidos.

Rescate exige aliado actualmente visible, expiración RecoveryEndTick conocida, llegada estimada más 45 ticks de margen y ningún humano visible a menos de 2.5 m del aliado. Los 1.5 s corresponden a margen al LLEGAR, según J31; no se afirma completar rescate dentro de ese margen. Sin expiry válido no supone tiempo infinito.

Herramientas: sólo con slot libre propio, manos equipadas, sin amenaza actual ni tarea ya mantenida en rango; desvío estrictamente menor de 4 m; ignora raqueta/aerosol agotados. No confirma intercambios de inventario lleno. Usa interacción ordinaria de autoridad. Contexto Runtime integrado posteriormente, según el checkpoint superior.

Atasco: el backend J32 superior reemplaza la detección inicial por desplazamiento local. Conserva el umbral técnico reversible de 0.15 m, pero lo aplica a progreso hacia waypoint/objetivo, con invalidación real y exclusión temporal de pasajes conocidos.

## Evidencia histórica del primer corte

`N:/Validation/V020/InventoryDraft/BotTests.csproj` compila contra fuentes reales centrales mediante Domain.csproj. CPU: 29/29 PASS, dominio 0 warnings/0 errors. No Unity ejecutado por este agente. Casos: límites reacción + cadencia real cada3ticks; recuerdo89/90ticks sin acción oculta; reset ronda; penalización239/240ticks; rescate44/45ticks y peligro visible; aliado oculto; atasco149/150 y bloqueo329/330; Steer cero; progreso real; desvío3.99/4m; inventario lleno; evasión Survival.

Pendiente actualizar prueba histórica TaskBotOnlyUsesOwnAssignmentAndSurvivalEvades que asumía reacción instantánea, añadir límites de tarea/amenazas/herramientas agotadas, importar meta del nuevo test y gate Unity. No inferir dificultad final, comportamiento por todos los mapas, navegación cerrada ni autorización online de estos tests.


## Uso de equipo por los bots (segunda parte)

El adaptador deberá resolver EquippedPickup desde el ledger público actual, exigiendo la triple coincidencia de actor propietario, pickup activo del inventario privado propio y fase Held. El dominio repite esa comprobación más ToolId de Self y revisión de inventario no nula. Sin ese dato no fabrica revisiones ni inicia lanzamiento. No se amplió el acceso a otros inventarios privados.

- Aerosol: PrimaryHeld mientras el enemigo siga visible dentro de 2 m y queden unidades; neutral al perderlo. No envía Primary redundantes para simular emisión.
- Raqueta: Primary dentro de 1.05 m, recurso positivo y cooldown público cumplido. La autoridad sigue validando todos los comandos.
- Pantufla: alcance de decisión del bot 4 m, concreción CEO reversible; carga hasta 27 ticks y ReleaseThrow explícito. Begin/Release llevan SelectedSlot, ActivePickup, revisión actual del pickup e InventoryRevision. No renueva carga activa. No inicia sin estamina para potencia completa (15 puntos). Al perder visibilidad/alcance/coherencia cancela con CancelThrow y PrimaryHeld=false, sin lanzar hacia un recuerdo.
- Estos cambios no modifican velocidades/alcance físico de proyectiles ni recursos de herramientas. Los tests iniciales verificaban comandos de dominio; el checkpoint posterior añade integración Runtime y percepción PhysX. Sigue pendiente la sesión gráfica completa.

Los 13 casos agregados al primer corte cubren OR literal de amenazas 1.99/2 m y aproximación/alejamiento a4 m, tarea mantenida que no se interrumpe para recoger, herramienta agotada descartada, PrimaryHeld/neutral de aerosol, propietario/pickup inválidos, Begin/Release con revisiones exactas, cancelación por ocultación, carga sin renovación, revisión obsoleta y cooldown/recurso de raqueta. En conjunto son29 tests.
## Herramienta agotada: vuelta a manos

Seguimiento autorizado tras gate nativo `bots-directed-native-01`: 40/40 PASS (29 bots +9 modos +2 rutas) para commit10736cd.

El bot ahora emite SelectInventorySlot(-1) con InventoryRevision privada propia, TargetPickupId=0 y ExpectedPickupRevision=0 cuando su aerosol/raqueta equipados llegan a recurso0. Neutraliza PrimaryHeld/UseHeld; la transacción habitual de autoridad cancela acciones/carga sin consumir estamina. El objeto agotado permanece en su slot con su mismo ID y recurso0, sin caída ni recarga.

Guarda la combinación pickup/revisión de inventario/ViewRevision de la petición efectivamente emitida: no repite la selección al recibir la misma observación pendiente. Una revisión de inventario o control nueva permite otra petición válida. No marca peticiones cuando el estado incapacitado impide emitirlas. No presupone acknowledgements ni cambia slots localmente.

CPU de esta revisión: BotTests31/31 PASS; RegressionTests94/94 PASS. Dos escenarios de autoridad real ejecutan recogida→emisión/pulsos→agotamiento120/5→selección de manos a cadencia de bot cada3ticks, conservan objeto/recursos y estamina100, y verifican LastAcceptedActionSequence. El mundo simulado sólo responde geometría/motor y efectos; la autoridad, comandos, consumos y snapshot son fuentes centrales reales. El fixture incorpora suelo horizontal para impedir que su stub previo integrara gravedad hacia abajo mientras declaraba Grounded=true. No es evidencia PhysX ni gráfica. Gate nativo de esta ampliación pendiente.
