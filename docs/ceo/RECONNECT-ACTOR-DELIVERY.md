# RECONNECT-ACTOR — control separado de simulación

Entrega del 20/09/2026. Archivos propios: GameplayAuthority.cs, GameplayModeAuthorityTests.cs y este informe. No cambios Core/Online/Bootstrap, interfaces de red, DTOs ni archivos Unity nuevos.

## Contrato para integración

API pública en la autoridad concreta: `void SetActorConnected(uint actorId, bool connected)`. Es capacidad del host; no está expuesta por IGameplayCommandSink ni existe un comando remoto que la invoque. El coordinador llama `false` ante pérdida de conexión y puede llamar `true` después de autenticar al dueño, antes de completar la preparación del cliente. La red debe mantener Input/Action bloqueados hasta completar preparación y Ack; la API no realiza ese handshake ni sustituye esa barrera.

Integración comunicada por CEO: Hello autenticado renueva RoomConnected y la revisión de autoridad; el mismo callback principal establece el estado de peer en reanudación antes de retornar. Esa barrera de canal impide procesar Input/Action hasta Ack, sin intercalado de mensajes durante el callback. Connected y CapturePrivate.CanAct describen disponibilidad del actor; no certifican que el canal de reanudación ya esté habilitado.

Actor comienza con Connected=true. Actor desconocido o notificación repetida del mismo estado son no-op. Cada cambio efectivo incrementa ViewRevision y Revision; un detach de contacto puede renovar adicionalmente ViewRevision. El consumidor debe usar la revisión publicada, no asumir incremento exactamente uno.

Al desconectar:

- Se borran inputs sostenidos/salto; se cancelan picadura propia, golpe/carga de strike y ayuda propia. HelpEnded se emite si correspondía. Se cancela ApproachingSurface para evitar continuación automática de la aproximación.
- Un mosquito ya posado conserva el soporte pasivo: puede seguir siendo movido por ese soporte o perderlo por física normal. Desconectar no teletransporta ni aplica velocidad cero indiscriminadamente.
- Acciones encoladas del actor mantienen su revisión antigua y son rechazadas al procesarse, incluso si desconecta/reconecta antes del siguiente tick. Las colas de otros actores se conservan.
- Input y Action posteriores se rechazan con InvalidState; los métodos de bot no pueden asumir un actor humano remoto. CapturePrivate.CanAct=false.
- No se borran posición, vidas, tarea, reloj, progreso ni recuperación. Los plazos y el decaimiento de tarea siguen avanzando. La física, daño recibido, desmayo, recuperación y respawn propios del modo continúan. No se modifica CanAct global: el actor desconectado sigue siendo víctima válida.

Al reconectar (false→true):

- Se limpian HasInput/HasAction, secuencias de input/acción, contadores de frecuencia e historial de acciones; se renueva ViewRevision y se conserva la simulación existente.
- Las secuencias bajas del cliente nuevo se aceptan únicamente con la revisión vigente. Los paquetes del cliente anterior son OldViewRevision; durante desconexión son InvalidState. Nuevos inputs no dan control durante incapacidad ni reviven un eliminado.
- El reset de secuencias ocurre **al reconectar**, no al desconectar. Disconnect conserva acknowledgements anteriores hasta ese momento. Repetir true no reinicia el stream activo.

## Verificación y límites

46/46 pruebas CPU PASS contra fuentes centrales, de las cuales siete nuevas de reconexión. Cobertura: comandos rechazados, ausencia de sustitución por bot, repetición idempotente, cola invalidada, secuencias/revisión e historial renovados, mosquito desconectado eliminado por golpe, humano desconectado que recibe picadura y se desmaya, cancelación de strike/picadura/aproximación, conservación de tarea/posición/vidas, recuperación/respawn sin reconexión implícita y ayuda interrumpida sin rescate gratuito.

Harness usado: `N:/LetMeSleep/Validation/V020/ModeUserRules/Checks.csproj`; recibo específico con hashes en `N:/LetMeSleep/Validation/V020/ReconnectActor/receipt.json`. Los fixtures de dominio verifican la autoridad real con un mundo determinista; no certifican colisión PhysX ni conectividad EOS/WAN. La prueba nativa y la integración del handshake pertenecen al siguiente gate CEO. Ambos scripts editados ya tienen meta existente.
