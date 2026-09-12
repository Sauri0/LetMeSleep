# Codec binario Gameplay alfa

Archivo autorizado excepcionalmente a W1: `unity/Assets/LetMeSleep/Online/GameplayWireCodec.cs` y su `.meta`. No se modificaron otros archivos Online ni su asmdef; Director conecta transporte/coordinador y referencia Gameplay desde Online.

## API pública

Namespace `LetMeSleep.Online`, clase estática `GameplayWireCodec`:

```csharp
byte[] Encode(PlayerInputCommand value);
byte[] Encode(PlayerActionCommand value);
byte[] Encode(GameSessionState value);
byte[] Encode(ActorPrivateState value);
byte[] Encode(GameplayEvent value);

bool TryDecodeInput(byte[] data, out PlayerInputCommand value);
bool TryDecodeAction(byte[] data, out PlayerActionCommand value);
bool TryDecodeSnapshot(byte[] data, out GameSessionState value);
bool TryDecodePrivate(byte[] data, out ActorPrivateState value);
bool TryDecodeEvent(byte[] data, out GameplayEvent value);
```

También hay cinco overloads `TryDecode(byte[], out Tipo)` si el caller ya conoce el tipo. Una trama inválida devuelve false y salida default/null; no expone objetos parcialmente decodificados. Encode rechaza DTOs locales inválidos mediante excepción, evitando enviar datos que el receptor no puede aceptar. El transporte decide canal/fragmentación/fiabilidad y autentica peer antes de llamar a Authority; el codec no concede permisos.

## Formato y límites

- Cabecera: magic uint32 `0x314D534C`, versión uint16 `2`, tipo uint8 (`1 Input`, `2 Action`, `3 Snapshot`, `4 Private`, `5 Event`). Números little endian de BinaryReader/Writer; sin JSON, UnityObject o reflection de serialización.
- Máximo **16384 bytes incluyendo cabecera**, **16 actores**, **128 puertas**, **32 pickups**. Counts se comprueban antes de reservar arrays. Duplicados de ActorId/DoorId/SurfaceId de puerta se rechazan. No hay PUID ni identidad de propietario en mensajes de cliente.
- Strings: UTF8 estricto con largo uint16 en bytes; MapId/ContentHash≤128, BalanceHash≤256, ToolId≤24. Se rechazan largos inválidos, truncamiento, secuencias UTF8 inválidas, controles y strings vacíos. No hay lectura sin límite ni BinaryReader.ReadString.
- Floats finitos/acotados, bool únicamente byte0/1, enums en dominio, direcciones/quaterniones válidos. Vistas conservan yaw/pitch y su consistencia con AimForward. Input diagonal se permite por componentes y lo normaliza Authority. Actor/época/ronda no pueden ser cero.
- Cliente tick dentro de ronda≤54000; privado incluye SessionEpoch/RoundId/HostTick. Snapshot conserva vida, locomoción, pose, revisiones, anclas, strike, puertas y resultado. Recuperación limitada al horizonte del perfil. Snapshot posee copia inmutable de actores/puertas.
- Config del snapshot se reconstruye de Tick+TimeRemaining (30 Hz, duración30–1800 s) y BalanceHash canónico del perfil. BalanceHash es una firma textual de versión/parámetros, no firma criptográfica. No reconstruye roster autorizado, propietarios ni geometría de puertas. ReplicaStateGate compara MapId/ContentHash/BalanceHash con config local antes de aplicar.
- Se exige consumo completo; bytes sobrantes o tipo/versión incorrectos se rechazan. Transporte no debe concatenar varias tramas antes de TryDecode ni omitir su propio control de remitente autorizado.

## Validación

`validation/Run-Validation.ps1` compila codec + dominio en C#9/netstandard2.1 y adaptador contra Unity6000.3.24f1/InputSystem. Suite externa total: **56 casos/0 fallos** (29 QA,18 dominio/réplica/herramientas,9 codec). No se ejecutó Unity Test Runner ni una conexión de red.

Los 9 casos de codec comprueban ida/vuelta de los cinco tipos y campos concretos, contenido UTF8, uint.MaxValue en secuencia, anclas/strike/puertas, privado correlacionado, máximo16/128/32 bajo16384 bytes, rechazo de excesos, todas las posiciones de truncamiento de los cinco tipos, bytes sobrantes, versión/tipo/enum/bool inválidos, NaN, UTF8 inválido, largo corrupto, counts inválidos, privado sin época, 1000 tramas aleatorias y 300 mutaciones de snapshot sin excepciones inesperadas. La comparación de bytes al recodificar complementa las aserciones de campos; no sustituye pruebas de transporte. La versión2 agrega EquippedToolId/ToolPickups, valida que cada equipo tenga un único dueño humano y rechaza herramientas huérfanas.

La entrega no acredita fragmentación EOS, entrega fiable, pérdida/latencia, WAN ni corrección de una escena física. Estas pruebas son responsabilidad de integración con Director/QA.
