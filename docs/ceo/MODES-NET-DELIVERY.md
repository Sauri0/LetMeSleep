# MODES-NET-001 — entrega de serialización y validación

Fecha: 2026-09-19. Worktree: `N:/LetMeSleep/Worktrees/v020-modes`.

## Alcance implementado

- `RoomWireCodec` usa schema 2 y serializa `ModeId` y `ModeRuleProfileId`. Rechaza schema alfa, modo/perfil desconocido, cuota de Sangre efectiva en Supervivencia/Tareas, enums fuera de rango, trailing bytes y límites previos de sala.
- `GameplayWireCodec` usa schema 3. Snapshot público incluye modo, score de Tareas, oportunidades viables, vidas y eliminación; no incluye `TaskAssignment` ni `ObjectiveId` privado. Los enums se validan por membresía, no por máximo numérico.
- El canal privado serializa `TaskAssignment` con límites de ID/ticks/progreso/fallos. Los eventos públicos `TaskAssigned`, `TaskProgressed` y `TaskMissed` se rechazan. `TaskCompleted` sólo admite payload sin target, posición ni normal para no revelar el objetivo.
- La identidad compuesta `BalanceHash` se valida de forma canónica: balance, ID y valores del perfil, vidas derivadas y hash de catálogo hexadecimal. Snapshot se contrasta además con la configuración `Begin` aceptada.
- `OnlineGameplaySession` schema Begin 3 envía modo, perfil numérico, catálogo authored, goal configurado, hashes de catálogo/balance y roster. El Ack abarca epoch/ronda, mapa, contenido, modo, perfil, catálogo, balance y goal.
- Host y guest comparan modo/perfil/mapa/contenido, catálogo local, herramientas y roster autenticado. El guest sólo aplica private state cuyo `ActorId` pertenece a su PUID local y cuya assignment existe en su catálogo; epoch/ronda ajenos se descartan.
- El catálogo de red queda limitado a 24 objetivos y el Begin completo a 16 KiB, dentro de `MessageFraming`. El contrato de dominio admite más, pero una sala online rechaza configuraciones que no caben en el transporte acotado.
- Protocolo de sala final usado por dominio: `lms-unity-020-1`. Este identificador ya no se deriva del número del schema gameplay.

## Coordinación con dominio

Se trabajó contra las firmas finales de `RoomRules`, `GameplayRoundConfig`, `ModeRuleProfile`, `ObjectiveDefinition`, `ActorSnapshot`, `ActorPrivateState`, `TaskAssignment` y `GameSessionState`. Dominio añadió el constructor raw validado de `GameSessionState` solicitado por red, evitando retransmitir el catálogo completo en cada snapshot. El catálogo completo sólo cruza la barrera Begin; snapshots posteriores llevan su identidad hash.

La política acordada es que assignments y progreso/missed no producen evento público. `TaskCompleted` comunica únicamente el incremento público. Supervivencia usa una vida lógica; Tareas comienza con tres y `LifeState.Eliminated` exige cero.

## Pruebas

`ModesNetworkCodecTests.cs` añade 10 casos ejecutados en arnés C# externo:

- round-trip de sala para Sangre/Supervivencia/Tareas y rechazo de schema anterior;
- rechazo de modo y perfil de sala alterados;
- round-trip público de score/vidas sin filtrar ObjectiveId;
- round-trip de assignment privado y rechazo del gameplay schema anterior;
- rechazo de eventos privados en el canal público y de ubicación en TaskCompleted;
- rechazo de score cross-mode y vidas incoherentes;
- presencia de modo/perfil/catálogo en Begin e identidad completa en Ack;
- protocolo release y schemas explícitos.

Evidencia externa: `N:/LetMeSleep/Validation/ModesNet-20260919`. `dotnet build --no-incremental`: 0 errores, 0 advertencias. Ejecución: `MODES_NETWORK_EXTERNAL tests=10 failures=0 unity_runner=false eos_transport=false`.

Los tests históricos también se actualizaron sin relajar sus negativos: `WireChecks.InvalidSnapshotCountsAndNullPrivateIdentityAreRejected` ahora navega explícitamente el layout schema 3 antes de corromper el contador de actores, y `AlphaProtocolToolOwnershipTests` exige `lms-unity-020-1`, Room schema 2, gameplay schema 3 y rechazo del protocolo/schema alfa anteriores.

Regresión externa final: `docs/unity/gameplay/validation/Run-Validation.ps1` compiló Domain/Adapter con 0 errores y 0 advertencias y terminó `GAMEPLAY_CPU_CHECKS tests=170 failures=0`; recibo en `N:/LetMeSleep/Validation/Gameplay-9b3e7e84b98d43f68a20db4cd4a3be75`. `Run-ProtocolToolExternalHarness.ps1` terminó `tests=6 failures=0` después de incorporar `RoomWireCodec.cs` a su fuente explícita. Ninguno de estos arneses sustituye Unity Test Runner ni transporte EOS.

## Límites y siguiente integración

- No se abrió Unity ni se ejecutó Unity Test Runner.
- No se inició EOS, dos clientes, transporte local ni WAN. Esta entrega no acredita privacidad observada entre procesos, reconexión ni resultado de ronda remoto.
- Bootstrap debe pasar a `OnlineGameplaySession` el proveedor local de `ObjectiveDefinition`; el argumento es opcional para mantener compilación de llamadas existentes, pero Tareas rechaza Begin si el proveedor está ausente o su catálogo no coincide.
- La corrida nativa integrada debe ejecutar EditMode completo. La prueba EOS de dos identidades debe comprobar selector/ready/start, Begin/Ack, assignment exclusivo del dueño, score/evento público, resultado, retorno y segunda ronda.
