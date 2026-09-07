# Contrato de transporte EOS propuesto — Let me sleep 0.5

Estado al 2026-09-07: investigación de EOSG 2.3.0 y codec local comprobados. **EOS no está activado ni integrado en Network; no hay conexión entre casas comprobada.** Los identificadores de producto/configuración siguen pendientes del alta del propietario. No se copiaron SDK/DLL al juego ni se publicaron credenciales. Las medidas corresponden a la fuente0.5 anterior al último cambio de aturdimiento/ayuda; deberán repetirse cuando se integre el transporte. El coordinador conserva la propiedad de Network y está ajustando su tabla RPC para rechazar clientes antiguos correctamente.

## Decisión de arquitectura

Usar un **listen server**: la instancia jugable del anfitrión ejecuta Simulation y un solo EOSGMultiplayerPeer servidor, con peer Godot **1**. El anfitrión juega como actor1; cada invitado usa otro PUID y peer Godot. No iniciar un hijo EOS ni conectar un cliente EOS a su propio PUID. Mantener ENet/directo como transporte independiente mientras se completa EOS. EOS hace descubrimiento y transporte; nunca simula ni decide resultados.

La implementación nativa confirma `create_server(socket_id)` → `unique_id=1`, `create_client(socket_id,remote_user_id)` → identificador propio y destino1. El PUID local es estático por proceso. [Fuente EOSG fijada a 2.3.0](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.cpp#L66).

## Inicialización y entrada sin cuentas de amigos

1. Un servicio `OnlineSession` inicializa una única plataforma EOS por ejecución, sólo si existe configuración local válida. Estado previo: `unconfigured`, sin intentar llamadas con placeholders. Activar el tick del SDK; un callback pendiente no avanza sin él. Para este listen server `HPlatform.is_server=false` (esa opción significa servidor dedicado). Desactivar overlay/RTC/presence: esta ruta sólo usa Connect, Lobby y P2P, no Epic Account Services.
2. Device ID persistente: `EOS.Connect.ConnectInterface.create_device_id(CreateDeviceIdOptions)`; aceptar `Success` o `DuplicateNotAllowed`, luego `LoginOptions.credentials.type=DeviceidAccessToken`, token nulo y `UserLoginInfo.display_name` limpio. `login_game_services_async()` maneja `InvalidUser`/continuance token/CreateUser. Serializar intentos; timeout/cancelación por generación evita callbacks anteriores sobre una sala nueva.
3. **No llamar `HAuth.login_anonymous_async()` sin corregirlo**: la copia descargada elimina el Device ID antes de crearlo (hauth.gd, alrededor de línea306). Eso regenera la identidad. Crear una función propia pequeña con el flujo anterior, sin borrar identidad en cada apertura. No mostrar/copiar PUID en la interfaz o logs normales. Guardar nombres visibles como estado del juego, no asumir que un Device ID tiene perfil público Epic. [Helper exacto](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/sample/addons/epic-online-services-godot/heos/hauth.gd).
4. Cada PC conserva su identidad. Dos instancias de prueba con la misma identidad local no sirven para demostrar comunicación entre personas; usar PCs/usuarios de sistema aislados o identidades de prueba distintas. No borrar DeviceID para fabricar dos identidades simultáneas en un host.

## Crear, encontrar, unirse y cerrar

- Crear `EOS.Lobby.CreateLobbyOptions` con `max_lobby_members=16`, `disable_host_migration=true`, `presence_enabled=false`, `enable_rtc_room=false`, `enable_join_by_id=false`, bucket de producto/protocolo. Esperar `HLobbies.create_lobby_async()` y los callbacks de actualización, obtener `lobby.lobby_id` y `owner_product_user_id`.
- Usar búsqueda por ID: `HLobbies.search_by_lobby_id_async(id)` y `HLobbies.join_async(result)`. No usar el helper `join_by_id_async`: sus comentarios lo reservan para invitaciones nativas de una plataforma integrada. `HLobbies.presence_enabled=false`; RTC desactivado. Las políticas exactas de búsqueda/visibilidad del deployment deben probarse con dos usuarios externos al equipo de desarrollo. La primera implementación puede usar un lobby anunciado sin explorador público en la UI, con una capacidad de invitación aleatoria validada por el servidor del juego antes de aceptar un jugador; no colocar esa capacidad en atributos públicos.
- Proponer invitación nueva, diferenciada de DD3/directo: envelope versionado `{transport:"eos",v:1,lobby_id,protocol,join_capability}`. No reutilizar un supuesto host IP ni almacenar secretos de cliente EOS en esa invitación. La búsqueda devuelve el dueño: no confiar en un PUID de servidor arbitrario escrito por el cliente. Validar protocolo del lobby y repetir handshake autoritativo existente antes de mostrar sala.
- Socket P2P corto derivado del ID de lobby con hash estable y prefijo del juego (longitud permitida por EOS; nunca el ID largo sin validar). `peer.create_server(socket)` en host; invitados `peer.create_client(socket,lobby.owner_product_user_id)`. Fijar `set_auto_accept_connection_requests(false)` y aceptar `incoming_connection_request` sólo para miembros actuales del lobby correcto; además validar miembro/PUID en el handshake del juego. Unir al lobby no otorga autoridad.
- `HP2P.set_relay_control(EOS.P2P.RelayControl.AllowRelays)` habilita ruta directa o relay, y `ForceRelays` se reserva para prueba deliberada. El callback `peer_connection_established` expone `network_type`: ésa será la evidencia de ruta, no un texto optimista. Mantener `SceneMultiplayer.server_relay=false`: esa propiedad es retransmisión entre clientes Godot y **no** desactiva el relay de EOS. [NAT/P2P oficial](https://dev.epicgames.com/docs/epic-online-services/multiplayer/nat-p2p-interface), [callback nativo](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.cpp#L1015).
- Cierre explícito del dueño: mensaje fiable `room_closed`, destruir lobby, cerrar P2P y limpiar sesión local. Caída del dueño: terminar la sala; no promover otro dueño aunque llegue un evento tardío del backend. Cierre desconocido de transporte se informa como pérdida de conexión, sin inventar causa. Al cancelar un create/join asincrónico, cerrar/dejar cualquier recurso que termine de crearse después de la cancelación.

## Adaptación precisa de Network (todavía no implementada)

Extraer cuerpos comunes que reciben un `sender` confiable; los RPC sólo obtienen `multiplayer.get_remote_sender_id()` y delegan. El host local invoca esos mismos cuerpos con1 desde funciones internas. No cambiar RPC remotos a `call_local` masivamente ni aceptar un `sender` suministrado en payload.

| Punto actual | Adaptación para listen server |
|---|---|
| `_request_join` | Extraer `_handle_join(sender,...)`. Registrar actor1 local tras crear transporte/lobby; mismo saneamiento, configuración y cupos. `room_owner=1`; emitir `accepted(1)` localmente. |
| `_client_connected` excluye servidor1 | Separar `has_local_player_session` de `transport_is_server`; no perder input del anfitrión. |
| `lobby_action`, `send_input`, `send_action` | Host local → handlers comunes con1; invitado → RPC a1. Mantener secuencia y yaw/pitch capturados al clic. |
| `_peer_connected`, `_peer_can_receive`, `_close_room` hacen cast ENet | Delegar estado/cierre/timeouts al adaptador de transporte. EOS usa `get_peer_user_id` y membership; jamás `ENetPacketPeer` sobre EOSG. |
| `_broadcast_lobby`, `_publish`, `_publish_waiting`, `_notify_all` | Helper `_deliver_to(id,kind,payload)`: id1 local emite copias de datos a Client/UI; otros usan codec y destino individual. Nunca RPC a sí mismo. |
| `_receive_lobby`, `_accept_snapshot`, privados | Reutilizar aceptación local, con epoch y barreras. Copias profundas en la entrega local para que la UI no mutile diccionarios del servidor. |
| `_peer_left`, `request_close_room` | Dueño local llama cierre común; invitados sólo pueden dejar su plaza. El PUID dueño original permanece fijo toda la sesión. |

Mantener private_for(id) por destinatario. En host local sólo entregar private_for(1) al cliente local; no enviar todos los privados en un paquete colectivo. El host ya ejecuta la autoridad y, por diseño, puede inspeccionar su memoria; esto no es protección contra un anfitrión modificado. Los invitados no deben recibir asignaciones/tareas ajenas ni cosméticos no seleccionados para el rol de ronda. Validar tipo de mensaje según lado antes del ensamblado: invitados no pueden mandar PUBLIC/PRIVATE ni hacerse pasar por1.

**Condición concreta antes de confiar en EOSG como autoridad de remitente:** en el tag2.3.0, `_poll` para `EVENT_STORE_PACKET` lee `peer_id` de la cabecera recibida y sólo comprueba `peers.has(peer_id)`; no compara ese ID con el PUID real de `packet_data.get_sender()`. El mediator sí conserva ese PUID, pero no establece allí la correspondencia de la cabecera. Antes de integrar, un fork revisado debe comprobar tamaño mínimo6, enum/eventos válidos y `peers[peer_id] == remote_user` antes de exponer el paquete a SceneMultiplayer. En el mediator debe comprobarse éxito/tamaño real devuelto por ReceivePacket antes de leer siquiera el byte0. Son hallazgos estáticos, no una explotación probada. El codec de aplicación no protege código nativo que procesa el paquete antes de llamarlo. Alternativa si no se mantiene ese fork: adaptador propio sobre EOS P2P que derive siempre la identidad del PUID del SDK y ejecute límites antes de decodificar; conserva el mismo contrato listenserver y codec. [Recepción EOSG](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.cpp#L688), [mediator](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_packet_peer_mediator.cpp#L88).

## Límite real de paquetes y mediciones

EOSG no fragmenta. `_get_max_packet_size()` devuelve `EOS_P2P_MAX_PACKET_SIZE` (1170), pero añade **6 bytes** propios y `_send_to` vuelve a comprobar el tamaño completo. El máximo real para el paquete Godot es por ello **1164 bytes**. No basta comparar el payload de juego con1170. [Cabecera](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.h#L60), [envío sin fragmentación](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.cpp#L486).

Medidas reproducibles: `measure_transport.gd`, ejecutado con Godot4.5.2 sobre el juego actual, sin cargar/inicializar EOS. Simulation real de4humanos+12mosquitos, nombres hasta20caracteres con acentos y estilos variados, 600ticks de movimiento/ataques. Un MultiplayerPeerExtension de captura observa los bytes **reales** producidos por el RPC Godot; no se envían a la red. El path corto `Network` no se confirma en esta sonda, por lo que captura el caso inicial sin caché. Los12 mensajes también pasan por encode/ingest del codec con igualdad exacta de datos y bytes reserializados.

| Mensaje | Variant sin comprimir | DEFLATE | RPC actual + cabecera EOSG |
|---|---:|---:|---:|
| Público inicial16 | 14.332 | 1.334 | 1.354 |
| Público en movimiento, muestras | 15.736–15.756 | 2.480–2.554 | 2.500–2.574 |
| Cambio de ronda fiable inicial (hoy Dictionary) | 14.332 | 1.334 | 14.353 |
| Lobby16 real, incluyendo avatares de espera | 15.124 | 1.198 | 15.145 |
| Privado humano, muestra | 540 | 286–294 | 561 |
| Privado mosquito, muestra | 480–484 | 273–279 | 501–505 |
| Público con entropía sintética adicional | 15.872 | 2.893 | 2.913 |

La muestra sintética varía todos los vectores/flotantes y cuatro golpes; es una prueba de tamaño, no un encuentro observado. Tiempo local orientativo para var_to_bytes+DEFLATE público: aproximadamente0,25–0,37ms en la última corrida, sin promesa de coste en otra PC. Archivo completo: `transport-measurements.json`.

Para un único PackedByteArray, el RPC actual aporta14bytes sin caché y EOSG6; los diccionarios RPC de esta sonda aportan15+6. Godot puede reducir su cabecera con caché o ampliarla con un path mayor. El codec limita cada frame a1032bytes, dejando132bytes para RPC/SceneMultiplayer dentro del máximo1164. Conservar un path corto y probar paquete real en integración; no tratar132como constante garantizada del motor. [Serialización RPC Godot4.5.2](https://github.com/godotengine/godot/blob/4.5.2-stable/modules/multiplayer/scene_rpc_interface.cpp#L334).

**Unreliable ordered no existe aquí:** EOSG lo cambia explícitamente a **reliable ordered** y avisa. Si se dejan los RPC actuales como están, las posiciones acumularán retransmisiones. Usar UNRELIABLE con secuencias/ticks propios para público, privado frecuente e input. Mantener controles/acciones/arranque fiable por canales separados. El codec no sustituye las garantías de orden de esos canales fiables. [Conversión exacta](https://github.com/3ddelano/epic-online-services-godot/blob/2.3.0/src/eosg_multiplayer_peer.cpp#L608).

## Codec implementado, aún sin integrar

`game/scripts/online_packet_codec.gd` es una clase pura RefCounted; no abre sockets ni carga servicios. API:

```gdscript
var encoded = OnlinePacketCodec.encode(payload_dictionary, kind, epoch, sequence, tick)
# {ok, frames:Array[PackedByteArray], raw_bytes, compressed_bytes} o {ok:false,error}
var receiver = OnlinePacketCodec.new()
receiver.reset(session_epoch)
var result = receiver.ingest(authenticated_sender, frame, now_ms, reliable_channel)
# Sólo complete=true permite usar result.payload. parcial/duplicado no contiene datos.
receiver.expire(now_ms)
receiver.forget_sender(departed_peer)
```

Un frame es32bytes de cabecera little-endian y hasta1000bytes DEFLATE. Campos: magic u16(0), versión u8(2), kind u8(3), epoch u32(4), sequence u32(8), tick u32(12), índice u16(16), cantidad u16(18), total comprimido u32(20), total bruto u32(24), checksum u32(28). Checksum: primeros32bits SHA256 del conjunto comprimido; detecta corrupción, **no autentica** al remitente. Identidad/destinatario provienen del transporte.

Límites: bruto65536, comprimido65792,66fragmentos,4mensajes parciales por emisor,64globales,16emisores. Memoria de bytes de ensamblado acotada aproximadamente4,21MB más contenedores, no asignación del tamaño declarado antes de recibir chunks. TTL300ms para datos no fiables y3000ms para fiables; duplicados no extienden vida. Limpiar por desconexión y por epoch; coordinar epoch de sesión en handshake, nunca aceptar el que declare un paquete arbitrario. Reiniciar epoch antes de agotar secuencias u32 (no se intenta wrap).

Valida cabeceras, cantidad/tamaño exacto de fragmentos, conflicto de duplicados, checksum y tamaño descomprimido; rechaza Object/Callable/RID, NaN/Inf, profundidad mayor12 y contenedores excesivos. Admite StringName usado realmente en snapshots Godot. Usa `bytes_to_var`, que en Godot4 no permite objetos, nunca `bytes_to_var_with_objects`. Descarta mensajes ya entregados y tick/sequence anteriores por emisor+kind. Ordena fragmentos fuera de orden; si falta alguno, expira sin entregar datos parciales.

Es responsabilidad del transporte enviar todos los frames de un mensaje fiable consecutivamente en el mismo canal ordenado. Si el mensaje fiable vence, solicitar resincronización/cerrar la sesión, no descartar silenciosamente un cambio de ronda. Para movimiento/público se espera el siguiente snapshot completo. Tres fragmentos por snapshot actual implican una probabilidad de llegada completa `(1-p)^3` bajo pérdida independiente; medir pérdida real antes de ajustar frecuencia/compresión. No retransmitir snapshots viejos ni transportar todos los privados dentro del público.

Pruebas: `online_packet_codec_test.gd` **114/114**, más **12/12 roundtrips** de mensajes actuales en la sonda. Incluyen pérdida/TTL, reordenado, duplicados, corrupción, límites, cuotas, epoch, tick, objeto serializado y valores no finitos. Ninguna prueba representa WAN o relay EOS funcional.

## Pista acotada del crash al salir

Existe un antecedente casi idéntico de crash11 en Windows al cerrar sin usar EOS ([issue64](https://github.com/3ddelano/epic-online-services-godot/issues/64)). La [PR65](https://github.com/3ddelano/epic-online-services-godot/pull/65) quitó `memdelete` de los singletons. Sin embargo, el `src/register_types.cpp` descargado del tag2.3.0 contiene otra vez `memdelete(_mediator)` y `memdelete(_ieos)` antes de poner punteros en null. Es una pista verificable de teardown; **no demuestra** qué instrucción causó el único crash observado ni que el binario incluya exactamente ese código. El coordinador informó reintentos editor/headless/export/EXE limpios. No parchear una DLL a ciegas ni dar por resuelto el incidente; antes de integrar, repetir cierre nativo y conservar traza si reaparece. La copia GDScript también activa overlay OpenGL por defecto al inicializar HPlatform; no aplica al smoke que no inicializa servicios, y para esta ruta se desactiva explícitamente.

## Criterios previos a afirmar que otra casa funciona

Configuración real y deployment apto para amigos sin cuentas; inicio DeviceID persistente en PCs distintas; crear/unirse/cancelar; evidencia de callback de relay forzado y ruta normal; 16jugadores o equivalentes con métricas de paquete/pérdida; controles y snapshots privados inspeccionados por destinatario; exclusión de0.4/0.5 incompatible según protocolo fijado; cierre host y crash sin migración; ventana de timeout/reintento específica; ninguna credencial en repositorio/log/ZIP. Hasta completar eso, la entrega0.5 no promete Internet integrado.
