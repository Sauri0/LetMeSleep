# MODES-DOMAIN-001 — entrega de dominio v0.2.0

Fecha: 2026-09-20 UTC. Rama `codex/v020-modes`, base `7dee7b3`. Propiedad: RoomSession, Gameplay contracts/authority/rules/bots y tests propios. Entrega CPU; no declara mapas authored, Unity, build, WAN ni publicación listos.

## Resultado

Se conservan Sangre y el motor compartido. Supervivencia añade eliminación explícita, rechazo de input/acciones posteriores, victoria humana por cero mosquitos y victoria mosquito al timeout. Tareas añade asignación privada, progreso por Use sostenido, pausas, ventanas personales, score/goal compartidos, tres vidas personales, rescate completo, espera de spawn seguro, protección y eliminación final. No hay generación de estaciones ni marcadores.

Protocolo Core conservado: `lms-unity-020-1`. Codecs/UI/Bootstrap/mundo Unity están fuera de esta entrega; MODES-NET-001 modifica codecs separadamente sobre el mismo worktree.

## Contratos de integración

- Core `GameModes.Blood/Survival/Tasks` = `blood/survival/tasks`; `ProfileId(mode)` = `v020-{mode}-1`. `RoomRules` añade opcionales `modeId`, `modeRuleProfileId`; otros modos tienen BloodQuota efectiva cero. `ChangeRules` conserva incremento de revisión e invalidación de ready.
- `GameplayRoundConfig` añade opcionales `modeId`, `ModeRuleProfile modeRules`, `IReadOnlyList<ObjectiveDefinition> objectives`, `int tasksGoal`. Expone ModeId, ModeRules, ModeRuleProfileId, Objectives, ObjectiveCatalogHash y ConfiguredTasksGoal. `BalanceHash` ahora incluye balance base + ModeRules.Hash + ObjectiveCatalogHash, también en Sangre.
- `ModeRuleProfile(modeId, taskCadenceTicks=1200, taskDeadlineTicks=900, taskMinimumDeadlineTicks=450, taskFailurePenaltyTicks=90)`; lives fijas: Sangre 0 (no finitas), Supervivencia 1, Tareas 3. Cadencia y plazos están en ticks de 30 Hz.
- `ObjectiveDefinition(string objectiveId, ObjectiveKind kind, string displayKey, string actionKey, Float3 position, Float3 approachPoint, float useRadius, uint workTicks, string routeRegionId, uint routeBudgetTicks=150)`. Kinds cerrados Repair/Clean/Switch. Máximo 128; IDs únicos de hasta 96 caracteres, números finitos, approach dentro del radio y trabajo+ruta dentro del mínimo plazo. Catálogo ordenado por ID y hash SHA256 de todos sus campos. El contenido local debe incorporar/contrastar este catálogo en su identidad, además del hash de mapa.
- `ActorSnapshot` añade `int livesRemaining=0`; propiedad `Eliminated` deriva de `LifeState.Eliminated`. No se reutiliza Stunned para eliminación. El mundo y presentación deben retirar colisiones/interacciones/cuerpo jugable y ofrecer cámara de espectador; dominio no llama motores, contacto ni recovery para eliminados.
- `ActorPrivateState` añade `TaskAssignment taskAssignment=null`. Assignment contiene ObjectiveId, IssuedTick, DeadlineTick, WorkTicks, ProgressTicks, Status y PersonalFailures. Sólo el propietario humano recibe esta estructura. Status: Active/Completed/Missed/WaitingForRoute.
- `GameSessionState` añade ModeId, TasksCompleted, TasksGoal, ViableTaskOpportunities. Constructor de config y constructor raw para decoder; este último guarda identidad/hash sin repetir catálogo por snapshot y valida escalares/counters. Red debe validar roster/payload y contrastar Begin aceptado.
- Nuevos reasons: AllOpponentsEliminated, TasksMet, TasksMissed. Eventos públicos implementados: TaskCompleted (Position/Normal cero, TargetActorId=0, sin objetivo), LifeConsumed, ActorEliminated, ActorRespawned. TaskAssigned/TaskProgressed/TaskMissed quedan como enum reservado: se derivan del estado privado y **no** se emiten en el broadcast público.

`IGameplayModeWorld` vive en `Gameplay/ModeRules.cs` como capacidad adicional sin romper implementadores de IGameplayWorld:

```csharp
bool ValidateObjective(in SpawnActor human, ObjectiveDefinition objective);
bool IsObjectiveAvailable(uint actorId, ObjectiveDefinition objective);
bool CanWorkObjective(uint actorId, ObjectiveDefinition objective, Float3 position, Float3 aim);
bool TryMosquitoRespawn(uint actorId, out Float3 position);
```

ValidateObjective exige región conocida, soporte/colisión authored y rutas desde todos los spawns e interobjetivos dentro del presupuesto. IsObjectiveAvailable refleja bloqueo temporal, no progreso. CanWorkObjective resuelve línea/interacción real; la autoridad además comprueba distancia. TryMosquitoRespawn sólo devuelve spawn mosquito validado libre. Tareas rechaza Begin sin interfaz, catálogo o validación positiva de cada humano/objetivo. Estos contratos NO sustituyen evidencia nativa del mapa.

Bots: `BotObservation` añade al final `modeId`, `ActorPrivateState ownPrivate`, `ObjectiveDefinition taskObjective`, `Func<ObjectiveDefinition,Float3> taskDirection`. Rechaza private state ajeno. Mosquito nunca almacena tarea humana. El humano persigue su tarea salvo defensa local inmediata; sin ruta suministrada espera. `BotPatrol.DirectionTo(position,targetRegion,approachPoint,passageOpen)` encuentra pasajes abiertos authored; devuelve cero sin ruta. Unity debe conectar ese resultado al callback y conservar steering/motor físico. Supervivencia hace evadir al mosquito visible; eliminados producen input quieto y ninguna acción.

## Semántica de tiempo

A 120 s, oportunidades en tick 0/1200/2400: tres por humano; goal automático `ceil(2*N/3)`, objetivo configurado limitado a oportunidades. Se permite sólo slot cuyo plazo base cabe antes del corte; reducir plazos no crea más slots ni baja el goal. Asignaciones demoradas por ruta esperan si su ventana completa aún cabe en el slot. Un bloqueo temporal de una asignación pausa el vencimiento hasta el fin del slot sin penalizar ni descontar el objetivo. La cadencia está anclada a la ronda y nunca al momento de completar/fallar de otra persona.

El deadline es exclusivo: se puede progresar antes de DeadlineTick; al llegar se registra Missed. El borde deadline==cadencia cuenta un único fallo antes de emitir siguiente assignment. Use sostenido dentro del radio y línea acumula un tick de trabajo por tick; salir/ocluir, golpe/uso de puerta, contacto de picadura, caída/desmayo o recuperación pausa sin borrar. Completar no termina anticipadamente: resultado TasksMet/TasksMissed al timeout. Eliminar todos los mosquitos sí termina antes.

Mosquito de Tareas: golpe -> Falling -> Stunned al apoyar. Ventana = Balance.RecoveryBaseSeconds (12 s por defecto); ayuda acumula trabajo a HelpMultiplier (3 por defecto), rescate completo conserva vida aunque el destino siga ocupado. Agotar ventana consume exactamente una; si no hay spawn seguro espera sin consumir de nuevo. Reaparece Recovering durante 0.4 s y después recibe ProtectionSeconds (1.5 s por defecto). Sangre conserva recuperación original.

## Evidencia reproducible

`N:/LetMeSleep/Validation/V020/ModesDomain/Checks.csproj`, `Domain.csproj`, `Program.cs`, `checks.log`. El proyecto compila **fuentes reales del worktree**, no copias ni DLL central. NUnit de Unity se usa como librería de asserts; runner CPU invoca Test/TestCase/SetUp de las clases seleccionadas. Mundo de prueba implementa queries de colisión como fixture controlado; no certifica colisión Unity.

Comando: `dotnet run --project N:/LetMeSleep/Validation/V020/ModesDomain/Checks.csproj --no-launch-profile`.

Resultado final: **51 PASS, 0 FAIL**: 22 casos nuevos de modos + 10 de mordidas/puertas y 19 Core sala/roster/seguridad/snapshots. Incluye 30 Hz, vida única con spawn bloqueado, tres muertes, rescate conservado con destino ocupado, protección, pausas por radio/puerta/golpe/picadura, privacidad, plazo individual/piso, borde cadencia, goal y timeout, reinicio, abandono, navegación authored y rechazo de contenido inválido. `git diff --check` sin errores.

Durante preparación, un caso de picadura omitía el input de release necesario para rearmar la mordida; se corrigió el fixture y pasó. Un intento de ampliar wildcard Core incluyó un test que requiere Online; se excluyó esa clase del harness de dominio (la cobertura de codecs pertenece a MODES-NET-001). No se ocultó una falla de dominio.

## Pendientes del integrador

1. Codecs Begin/Ack/private/public y gate de identidad, con el cambio de BalanceHash; agente red asignado.
2. Catálogos/rutas reales para cinco mapas, validación/importación y capacidad IGameplayModeWorld; no habilitar Tareas en mapa incompleto.
3. Consumir Eliminated en mundo/colliders/presentación/espectador y no enviar acciones de observadores.
4. Conectar UI selector/configuración/entrenamiento, asignación privada contextual sin marcador y bots con ruta.
5. Pruebas nativas por mapa, resultado/rematch, dos identidades/EOS y balance real. Sin apertura de Unity/editor ni publicación en este ticket.
