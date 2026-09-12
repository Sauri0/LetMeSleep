# Movimiento del lobby 3D

`Gameplay.Unity/LobbyMovementRuntime.cs` contiene locomoción de espera independiente de Sangre. Reutiliza únicamente las consultas/cápsulas de UnityGameplayWorld. No crea RoomSession, sortea roles, aplica daño, prepara picaduras ni depende de Online/Content. Todos los proxies son humanos y la capacidad es16.

## API para el coordinador

```csharp
void Bind(ulong epoch, string localPlayerId, bool isHost,
    IReadOnlyList<LobbySpawn> roster, uint rosterRevision = 1);
void SetRoster(IReadOnlyList<LobbySpawn> roster, uint revision);
CommandReject SubmitMove(string authenticatedId, Vector2 move, float yaw, uint sequence);
CommandReject SubmitMove(string authenticatedId, in LobbyMoveCommand command);
LobbySnapshot CaptureSnapshot();
bool ApplySnapshot(LobbySnapshot snapshot);
void SetInputBlocked(bool blocked);
void TickHost();
void Unbind();
```

`CreateRoster` es alias de SetRoster. `LobbySpawn(PlayerId,Position,Yaw=0)` recibe puntos M2 en metros; Director valida membresía y aporta la revisión de roster. Una revisión repetida con los mismos miembros es idempotente; cambiar miembros exige revisión mayor. IDs numéricos físicos son locales al lobby, el transporte usa PlayerId autenticado y no acepta IDs de propietario dentro del comando.

`LobbyMoveCommand`: SessionEpoch ulong, RosterRevision uint, Sequence uint, Move Vector2 y Yaw float radianes. `InputReady` sólo emite comando local cliente; Director lo manda por su canal3 y llama SubmitMove con identidad autenticada. Host local envía directamente al mismo validador. Verifica pertenencia, época/revisión, finitez, secuencia modular, 60 entradas/s y normaliza diagonal. Entrada ausente durante más7 ticks queda neutra.

`LobbySnapshot`: SessionEpoch, RosterRevision, HostTick y copia readonly de Poses. Cada `LobbyPose`: PlayerId string≤128, Position/Velocity Vector3, Yaw float, Grounded bool, LastInputSequence uint y MotionPhase float. `SnapshotReady` sale sólo del host; `SnapshotApplied` sirve UI/presentación local/remota. Cliente rechaza época/revisión/tick viejo, roster distinto/duplicado, posiciones/velocidades/números inválidos antes de cambiar proxies. Codec/cotas del canal3 y autenticación son responsabilidad del Director.

## Escena y presentación

Añadir LobbyMovementRuntime y UnityGameplayWorld en un objeto propio del lobby. Asignar `HumanPrefab` visual y `LocalCamera`; un host dedicado puede omitir visuales. El componente deshabilita colliders del prefab y root motion de Animator para conservar una sola autoridad física. `VisualCreated(string playerId,GameObject instance)` permite que W2 agregue animación sin dependencia inversa. SnapshotApplied expone velocidad/MotionPhase para animaciones opcionales.

Director proporciona LobbySpawnPoints de M2 y prefab humano. Activar este objeto sólo en fase Waiting; Unbind antes de comenzar Gameplay para quitar proxies y liberar cursor. La sala/protocolo determina cuándo entrar/salir; no se cambia de fase desde locomoción.

Motor host30Hz: cápsula1.72/.25 m, velocidad3.1m/s, gravedad12, step.22, colisión/deslizamiento del mismo motor humano. No hay salto en esta entrega alfa. Input WASD/mouse; cámara tercera persona con yaw/pitch inmediatos, inversiónY opcional, distancia.5–4m y esfera.12m para retraer frente a paredes. Se suaviza sólo expansión, nunca atravesar un obstáculo para esconderla.

Cliente predice localmente por ticks de30Hz, guarda hasta64 comandos y al recibir estado restaura posición/velocidad autoritativas y reproduce pendientes posteriores al ACK. Remotos mantienen hasta8 snapshots e interpolan visuales con100ms de demora; colliders usan estado recibido, sin mover colisión desde el renderer. Mantiene pose al faltar datos, no extrapola a través de geometría.

`SetInputBlocked(true)` neutraliza movimiento, limpia predicción y libera cursor antes de escribir en el tablero. Mientras bloqueado no lee WASD ni mouse de vuelo/cámara. Perder foco y deshabilitar también neutraliza. Bind desbloquea; Unbind libera cursor. Activar sólo un lector de input entre lobby y partida; el coordinador debe bloquear lobby antes de enfocar un campo de texto.

## Evidencia

Archivo compilado con C#9/netstandard2.1 contra Unity6000.3.24f1 e Input System:0 errores/0 advertencias. Los50 casos existentes de dominio/réplica/codec siguen pasando; **no son pruebas de locomoción del lobby en Unity**. No se abrió editor ni se alteraron escenas/paquetes/settings.

Pendientes de ejecución coordinada: dos clientes en sala, actualización de roster mientras caminan, teclado al escribir/volver a caminar, reconexión/revisión, colisión con mapa real, cámara junto a pared, predicción bajo latencia y representación de todos los humanos. Sin afirmar WAN, FPS ni puerta/step certificados por esta compilación.
