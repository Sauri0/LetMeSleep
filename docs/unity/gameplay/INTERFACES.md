# Interfaces y mensajes iniciales para Core/Gameplay

Especificación de integración, no archivos C# compilados. Director implementa Core/Online, `RoomState`, `RoomRules`, membresía, sorteo y versión; Gameplay no duplica esas reglas. Identidad autenticada: string opaca EOS PUID. `ActorId` uint es índice de sesión, nunca identidad ni prueba de autorización. Online entrega el principal autenticado fuera del payload y Core lo vincula con actor/ronda.

Unity 6000.3.24f1, coordenadas del [contrato](ALFA-GAMEPLAY.md). Core leído exige C# puro sin UnityEngine: usar valores inmutables `Float2(X,Y)`, `Float3(X,Y,Z)` y `Rotation(X,Y,Z,W)` normalizada, con campos float y constructores explícitos, en los DTO de gameplay. Conversión a UnityEngine.Vector3/Quaternion exclusivamente en adaptadores físicos/presentación. Ningún DTO lleva GameObject, Transform, Animator, Unity instance ID, collider reference ni ScriptableObject. Los bocetos siguientes fijan campos y semántica; serializador y ensamblados los fija Director.

## Alineación con Core alfa 1

Leídos sin modificar `N:/LetMeSleep/Repository/docs/unity/CORE-CONTRACT.md` y `unity/Assets/LetMeSleep/Core/RoomSession.cs` antes de su primer commit. Referencias exactas disponibles: `RoomSession.Protocol = "lms-unity-094-alfa-1"`, `RoomView.Revision`, `Round`, `OwnerId`, `Phase`, `Rules`, `Members`; `MemberView.Id/Role`; `RoomRules.MapId`, `RoundSeconds`, `BloodQuota`; `PlayerRole.Human/Mosquito`.

- Director crea un adaptador de sesión que observa RoomView; cuando entra a Playing con Round nuevo, traduce los roles ya sorteados y llama BeginRound exactamente una vez. MemberView.Id es PUID opaco; mapas ActorId↔PUID pertenecen a esa sesión. No volver a sortear ni crear un segundo RoomSession en Gameplay.
- `RoundId` corresponde a RoomView.Round dentro de SessionEpoch nuevo por sala. View.Revision ordena cambios de roster/reglas; no usarla como reloj de física ni nueva ronda. Se comprueba tipo/rango al convertir int a ulong.
- Tiempo/cuota provienen de RoomRules: valores iniciales Core 180 s / 20 unidades. BalanceProfile no sobrescribe esos ajustes de sala; sólo aporta física/extracción/recuperación. Capacidad 16, humanos 1–5, ambos equipos y modo/mapa disponibles los valida Core.
- Gameplay emite resultado único; el adaptador host llama `RoomSession.FinishRound(OwnerId)` y expone RoomPhase.Results. `EndRound` de Gameplay termina su simulación, no modifica RoomSession por sí mismo. No publicar Waiting/Playing/Results/Closed como segunda máquina de sala.
- En ReturnToWaiting el adaptador descarta simulación/inputs y usa RoomView nuevo; el inicio siguiente genera snapshot limpio. Closed por salida del anfitrión cancela la simulación y anclas sin adjudicar victoria ficticia.
- Leave de invitado elimina actor; si queda sin miembros uno de los equipos, finalizar con victoria del equipo restante y motivo OpponentLeft; si no queda nadie, Abort. Si aún hay ambos equipos, continúa Sangre y se revalida acceso corporal cuando queda un solo humano. Este resultado lo decide Gameplay, tal como indica Core.
- Entrenamiento usa un adaptador local y mismo Gameplay, no requiere simular EOS PUID. Principal de jugador local y bots tienen tipo explícito Local/Bot interno; ningún string reservado puede entrar por transporte online. Roster local permite rol elegido sin alterar el sorteo online.

El snapshot de sala `RoomView` y el de partida `GameSessionState` son distintos y ambos inmutables. Online debe codificar DTO acotados explícitos; no asumir que JsonUtility serializa propiedades de sólo lectura. Estas decisiones se alinean con el Core real leído, sin editarlo.

## Comandos y entrada

```csharp
public readonly struct CommandHeader {
    public readonly ulong SessionEpoch, RoundId;
    public readonly uint ActorId, Sequence, ClientTick, ViewRevision;
}
public readonly struct PlayerInputCommand {
    public readonly CommandHeader Header;
    public readonly Float2 MovePlanar; // X derecha, Y adelante; módulo <= 1
    public readonly float Vertical;     // -1..1, sólo mosquito
    public readonly float ViewYawRadians, ViewPitchRadians;
    public readonly Float3 AimForward; // mundial normalizado; sin origen enviado
    public readonly bool SprintHeld, CrouchHeld, BiteHeld, UseHeld;
}
public enum ActionKind : byte {
    Jump, Primary, PerchToggle, Detach, Use
}
public readonly struct PlayerActionCommand {
    public readonly CommandHeader Header;
    public readonly ActionKind Kind;
    public readonly Float3 AimForward; // muestra de mira al pulsar
}
public enum CommandReject : byte {
    None, UnknownActor, WrongOwner, WrongRound, StaleSequence,
    InvalidNumber, InvalidDirection, WrongRole, InvalidState,
    Cooldown, OutOfReach, Obstructed, OldViewRevision, RateLimited
}
public interface IGameplayCommandSink {
    CommandReject SubmitInput(string authenticatedPuid,
        in PlayerInputCommand command);
    CommandReject SubmitAction(string authenticatedPuid,
        in PlayerActionCommand command);
}
```

Core conserva streams de secuencia separados Input/Action y reinicia por RoundId. Orden uint con comparación modular; diferencia >=2³¹ ambigua se rechaza. Duplicado Action con mismo contenido devuelve la misma aceptación sin reejecutar; contenido distinto con igual secuencia se rechaza. Input newest-wins, viejo no recupera teclas. Opcional Online repite última acción hasta ACK dentro de cota. Presupuesto inicial 60 Input/s y 20 Action/s por actor, cola máxima 32; exceso no ralentiza tick. No se aceptan target/posición/daño del remitente.

Yaw/pitch se conservan además del vector porque mirar bajo -90° admite dos descomposiciones equivalentes: reconstruir siempre con asin/atan2 puede girar el cuerpo 180° al inspeccionar piernas. Host exige vector consistente con esos ángulos/marco dentro de 1° y usa la misma rama de pitch; cerca del polo conserva yaw/derecha anterior. Action toma el marco y pose del último Input aceptado junto con su muestra de mira. No limitar yaw por cuello ni confiar en un quaternion no normalizado.

`ViewRevision` cambia al cambiar marco de superficie o restaurar control. Host acepta intención mundial sólo para revisión actual; ACK/snapshot contiene nueva revisión y `lastAcceptedInputSequence`. Cliente preserva la orientación mundial visible, elimina predicción antigua y reemite held actual tras liberar acciones peligrosas. No aplicar una conversión de yaw de pared dos veces; no confiar en mensajes viejos de otra ronda.

## Autoridad y consultas

```csharp
public interface IGameplayAuthority : IGameplayCommandSink {
    void BeginRound(in GameplayRoundConfig config, IReadOnlyList<SpawnActor> roster);
    void Advance(in HostTick tick); // única llamada 30 Hz; HostTick.Index + DeltaSeconds
    void RemoveActor(uint actorId, ActorRemovalReason reason);
    GameSessionState CaptureSnapshot();
    ActorPrivateState CapturePrivate(uint actorId);
    IReadOnlyList<GameplayEvent> DrainEvents();
    void EndRound(RoundEndReason reason); // idempotente
}
public interface IGameplayWorldQuery {
    MotorResult MoveHuman(in HumanMotorQuery query);
    MotorResult MoveMosquito(in MosquitoMotorQuery query);
    bool TrySurface(in SurfaceQuery query, out SurfaceContact contact);
    bool TryBiteContact(in BiteQuery query, out BiteContact contact);
    StrikeHit SweepStrike(in StrikeSweep query);
    bool TryFreeRecoveryPoint(in RecoveryQuery query, out Float3 point);
}
public interface IBotController {
    BotCommands Decide(in BotObservation observation, in BotTick tick);
}
public interface IGameplayPresentationSink {
    void ApplySnapshot(GameSessionState snapshot, double renderHostTime);
    void ApplyPrivate(ActorPrivateState state);
    void ApplyEvent(in GameplayEvent item); // deduplicado por EventId + RoundId
}
```

`GameplayRoundConfig`: SessionEpoch, RoundId, MapId, ContentHash, BalanceId/hash, Mode=Blood, HostTickRate=30, RoundDurationTicks, BloodGoal. `SpawnActor`: ActorId, owner PUID nullable sólo para bot, rol ya sorteado, SpawnId validado, CosmeticProfileId. UI nunca construye roster autorizado. `HostTick`: uint index monotónico dentro de RoundId, delta fijo; acumulación de tiempo y carga máxima por frame son Core. MotorQuery: actor, pose inicial/velocidad, input validado, dt, dimensiones y revisión de mundo; no permite mutar mundo desde Presentation. MotorResult: posición, velocidad, grounded, normal/apoyo, contactos ordenados y bloqueo de techo.

`SurfaceQuery`: posición, dirección, adquisición máxima, radio, máscaras, actor excluido. `SurfaceContact`: SurfaceId estable, revisión, punto y normal locales, transform host actual, flags CanPerch/Moving. `BiteQuery`: actor mosquito, punta actual, dirección, distancia, población humana elegible. `BiteContact`: VictimId, AnatomicalSurfaceId, LocalPoint/Normal, PoseRevision, distancia y razón de rechazo. `StrikeSweep`: StrikeId, dueño, herramienta, mano, tiempo normalizado anterior/actual, geometría continua y filtro de actores ya golpeados; `StrikeHit`: ActorId objetivo opcional, fracción, punto/normal mundiales, material y bloqueante. Orden por menor fracción, desempate estable por IDs; no hits detrás de primer obstáculo. `RecoveryQuery`: actor, soporte actual, radio/cápsula y búsqueda local limitada.

Bots de host usan un principal reservado interno con permiso sólo sobre su ActorId; nunca aceptarlo desde red. `BotObservation`: propio snapshot/privado, aliados visibles/permitidos, enemigos percibidos con última observación y antigüedad, geometría de mapa/puertas percibidas, modo/tiempo; sin anclas ajenas privadas ni diccionario mutable de autoridad. `BotCommands`: Input y cero o una Action, mismos validadores. Decisión inicial 10 Hz escalonada, held se aplica a 30 Hz; no saltar cooldowns ni generar conocimiento oculto para recuperar una ruta.

## GameSessionState y eventos

Snapshot inmutable: construcción copia los valores y congela colecciones; `IReadOnlyList<T>` sobre una lista que host sigue mutando NO cumple. No incluir buffers pooled que se sobrescriben mientras los leen UI/Online. Propuesta inicial objeto inmutable por publicación; optimizar sólo con medición y contrato de lifetime explícito.

| Dato | Campos mínimos |
|---|---|
| GameSessionState | SessionEpoch, RoundId, HostTick, HostTime, MapId, ContentHash, BalanceHash, simulationPhase Running/Ended, timeRemainingTicks, BloodCollected, BloodGoal, resultado opcional, Actors, Doors |
| ActorSnapshot | ActorId, Role, LifeState, StateRevision, Position, Velocity, BodyRotation, ViewForward, ViewRevision, PoseRevision, Grounded, CrouchFraction, MotionPhase, SurfaceAttachment opcional, BiteAttachment opcional, StrikeState, RecoveryEndTick |
| SurfaceAttachment | SurfaceId/revisión, localPoint, localNormal, tangentForward |
| BiteAttachment público | VictimId, SurfaceId anatómico, localPoint/normal, poseRevision; suficiente para representar mosquito anclado |
| ActorPrivateState | ActorId, última secuencia aceptada de ambos streams, motivo de rechazo, InteractionHint, propia preparación/extracción, ayuda propia, countdown, acciones habilitadas |
| StrikeState | StrikeId, toolId, hand, phase, startTick, duración por fase, origin/target/normal validados; ningún autoseleccionado del renderer |
| GameplayEvent | SessionEpoch, RoundId, EventId monotónico, HostTick, Kind, SourceActorId, TargetActorId opcional, StateRevision, payload acotado |

Eventos: `StrikeStarted`, `StrikeImpact`, `BiteStarted`, `BiteEnded(reason)`, `MosquitoKnockedDown`, `RecoveryStarted`, `HelpStarted/Ended`, `Recovered`, `HumanFainted`, `DoorChanged`, `RoundEnded`. Los deltas de sangre/progreso continuo van en snapshot, no un evento por tick. Core implementa transporte fiable/ACK de acciones y eventos discretos; estado nuevo siempre permite reconstruir presentación aunque se pierda un evento cosmético. Impacto repetido no duplica audio ni daño. No transportar `AssignedZone`, `MarkerPosition`, `RotationSeconds` ni `NextTarget`.

Predicción: vista local inmediata y motor del jugador local opcional con comandos pendientes; impactos, sangre y transiciones de vida siempre confirmados por host. Reconciliar desde snapshot con input ACK; no reproducir sonidos/daño al rehacer movimientos. Remotos interpolan dos snapshots del mismo RoundId/StateRevision; salto de ancla/respawn corta interpolation y usa transición visual explícita. Extrapolación inicial máxima 100 ms, luego mantener pose; nunca atravesar colisión para esconder retraso. No se exige PhysX determinista entre clientes.

## Perfil inicial de balance y tolerancias

Estos valores son supuestos revisables elegidos para arrancar alfa sin preguntas a Branko, no resultados de partidas ni promesas de balance. Perfil versionado `alfa-blood-initial-1`; host envía hash. No reutilizar archivo de settings personal como fuente de física.

| Parámetro | Inicio | Condición de validación |
|---|---:|---|
| Humano altura/radio/ojos/agachado | 1.72/.25/1.53/1.0 m | Contrato Director; ojos agachado se deducen del rig, siempre dentro de cápsula válida |
| Mosquito radio | .055 m | Contrato Director; separar carcasa locomotora de silueta decorativa |
| Walk/Run/Crouch | 3.1/5.0/1.55 m/s | Referencia inicial; diagonal ≤ velocidad configurada |
| Flight/Accel/Brake | 3.8 m/s, 13/28 m/s² | 30/60/144 FPS mismo avance físico; freno sin deriva constante |
| Surface speed/acquire | .65 m/s / .25 m | Sólo apoyo alcanzable por barrido y geometría registrada |
| Gravity/Jump/Step | 12 m/s², 4.6 m/s, .22 m | Verificar risers definitivos M2 y techo; no forzar step sobre un obstáculo alto |
| ContactAcquire/Break | .025/.045 m desde punta | Ajustar probóscide con M1; break no concede permiso para adquirir fuera de .025 |
| BitePreparation | .6 s | Continuidad, sin teleport ni objetivo asignado |
| FullExtraction | 8 s | Progreso continuo; tasa por víctima capada a 1 unidad/s; solo 8 unidades produce desmayo |
| RecoveryBase | 12 s desde apoyo | Compartida humano/mosquito; golpes repetidos no la renuevan |
| Help multiplier/range | 3× / .6 m | Máximo un multiplicador por víctima, requiere línea libre y held válido |
| Recovery protection | 1.5 s | No inicia nueva picadura; no concede daño/velocidad extra |
| Ronda/cuota | 180 s / 20 unidades | Ajustable por host dentro de límites Core; requiere partidas reales |
| Mano windup/active/recovery | .08/.17/.35 s | Duraciones convertidas a ticks; ventana se barre aunque caiga entre ticks |
| Herramienta muestra | `swatter` propuesta | M1/Core fijan dimensiones/alcance del asset; no lanzar ni inventario en alfa |
| Error geométrico motor | ≤.002 m penetración residual | No porcentaje del tamaño humano para mosquito; 2 mm también es límite inicial del insecto |
| Error ancla host → piel | ≤.003 m aparte del offset anatómico | Probar carrera/crouch/giro/impacto y puerta; no compensar con inflación de collider |
| Diferencia vector W/aim | ≤1° sin colisión | Cámara libre en reposo no modifica vuelo hasta input activo |
| Corrección visual suave | ≤.10 s, colisión siempre respetada | Error >.15 m o cambio de vida/ancla exige corrección inmediata segura |
| Orden/contador | ±1 host tick para estados temporales | 30 Hz: 33.34 ms; temporizadores de red no dependen del FPS |

Metros y balance están separados. Si el nuevo rig hace inválido alcance manual, se corrige geometría/pose antes de declarar defendible una superficie. La tabla no autoriza modificar Core, paquetes, escenas ni settings desde este lote documental.
