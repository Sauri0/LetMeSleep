extends Node

signal lobby_updated(data: Dictionary)
signal snapshot_updated(data: Dictionary)
signal private_updated(data: Dictionary)
signal notice(message: String)
signal accepted(peer_id: int)
signal disconnected
signal waiting_updated(data: Dictionary)
signal connection_state_changed(data: Dictionary)
signal invitation_updated(text: String)
const OnlineSession = preload("res://scripts/online_session.gd")
const OnlineInvitation = preload("res://scripts/online_invitation.gd")
const OnlineTransport = preload("res://scripts/online_transport.gd")
var online_session: Node
var online_transport: Node
var invitation_text := ""
var _online := false
var _local_host := false
var _online_capability := ""
var _online_request: Dictionary = {}
var _online_rejected: Dictionary = {}
var _online_rejected_users: Dictionary = {}
var _online_reject_serial := 0
var _online_users: Dictionary = {}

const VoiceTransport=preload("res://scripts/voice_transport.gd")
var voice: Node
const Simulation = preload("res://scripts/simulation.gd")
const LobbyRules = preload("res://scripts/lobby_rules.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Map = preload("res://scripts/arena.gd")
const InvitationCodec = preload("res://scripts/invitation.gd")
const VERSION := "0.9.0"
const PROTOCOL := InvitationCodec.PROTOCOL
const DEFAULT_PORT := 27840
const MAX_PLAYERS := 16
# Leave room for Godot RPC and ENet headers below the transport's MTU.
const MAX_UNRELIABLE_SNAPSHOT_BYTES := 1200
var is_server := false
var players: Dictionary = {}
var config: Dictionary = {}
var room_code := ""
var room_owner := 0
var sim: RefCounted
var pending_join: Dictionary = {}
var latest: Dictionary = {}
var private_latest: Dictionary = {}
var token := ""
var server_tick := 0
var last_received_tick := -1
var publish_accumulator := 0.0
var last_phase := ""
var private_revisions: Dictionary = {}
var join_attempts: Dictionary = {}
var tokens: Dictionary = {}
var peer_tokens: Dictionary = {}
var menu_rates: Dictionary = {}
var connect_age := 0.0
var connecting := false
var local_cosmetics: Dictionary = {}
var waiting_actors: Dictionary = {}
var waiting_inputs: Dictionary = {}
var connection_state: Dictionary = {}
var _attempt_id := 0
var _connection_phase := "idle"
var _last_request: Dictionary = {}
var _resolver_id := -1
var _had_session := false
var _terminal_failure := false
var _pending_map: Dictionary={}
var _prepared_map: Dictionary={}
const MAP_PREPARE_TIMEOUT:=20.0
const TRANSPORT_TIMEOUT := 15.0
const HANDSHAKE_TIMEOUT := 8.0

func _ready() -> void:
	config = Simulation.DEFAULT_CONFIG.duplicate(true)
	voice=VoiceTransport.new();voice.name="Voice";add_child(voice)
	online_transport=OnlineTransport.new();online_transport.name="OnlineTransport";add_child(online_transport)
	online_transport.send_failed.connect(_online_packet_failed)
	multiplayer.connected_to_server.connect(_connected)
	multiplayer.connection_failed.connect(_failed)
	multiplayer.server_disconnected.connect(_server_lost)
	multiplayer.peer_disconnected.connect(_peer_left)
	multiplayer.peer_connected.connect(_peer_connected)

func host(port: int) -> Error:
	if port < 1024 or port > 65535:
		return ERR_INVALID_PARAMETER
	var peer := ENetMultiplayerPeer.new()
	var error := peer.create_server(port, 32, 4)
	if error != OK:
		return error
	# Godot 4.5.2 passes max_channels + SYSCH_MAX as incoming bandwidth
	# inside create_server. Restore unlimited bandwidth before peers connect;
	# otherwise ENet throttles voice and movement packets to nearly zero.
	# modules/enet/enet_multiplayer_peer.cpp, 4.5.2-stable, line 60.
	peer.get_host().bandwidth_limit(0,0)
	multiplayer.multiplayer_peer = peer
	# All gameplay routes through server 1; no peer-to-peer RPC relay is needed.
	(multiplayer as SceneMultiplayer).server_relay = false
	is_server = true
	print("SERVER_READY version=%s protocol=%d UDP=%d" % [VERSION, PROTOCOL, port])
	return OK

func host_online(eos_config: Dictionary, player_name: String) -> void:
	_start_online(eos_config, player_name, "", true)

func join_online(eos_config: Dictionary, player_name: String, invite_text: String) -> void:
	_start_online(eos_config, player_name, invite_text.strip_edges(), false)

func _start_online(eos_config: Dictionary, player_name: String, invite_text: String, create: bool) -> void:
	close_client()
	_attempt_id += 1
	_terminal_failure=false
	_online_request={"config":eos_config.duplicate(true),"name":player_name,"invite":invite_text,"host":create}
	_last_request={"address":"EOS","port":0,"name":player_name}
	var invitation: Dictionary={} if create else OnlineInvitation.decode(invite_text)
	if not create and not invitation.get("ok",false):
		_fail_connection("invalid_invitation",str(invitation.get("error","Invitacion invalida.")))
		return
	_online=true
	connecting=true
	_online_capability=OnlineInvitation.generate_capability() if create else str(invitation.capability)
	pending_join={"name":player_name.strip_edges().left(20),"create":create,"code":""}
	if not is_instance_valid(online_session):
		online_session=OnlineSession.new();online_session.name="OnlineSession";add_child(online_session)
		online_session.state_changed.connect(_online_state)
		online_session.failed.connect(_online_failed)
		online_session.transport_ready.connect(_online_ready)
		online_session.member_departed.connect(_online_member_departed)
	if online_cleanup_pending():
		_set_connection_state("online_cleanup","","Cerrando la conexión anterior…")
		var attempt:=_attempt_id
		var deadline:=Time.get_ticks_msec()+25000
		while online_cleanup_pending() and Time.get_ticks_msec()<deadline:
			await get_tree().process_frame
			if attempt!=_attempt_id or not _online: return
		if online_cleanup_pending():
			_fail_connection("cleanup_timeout","La conexión anterior no terminó de cerrar. Esperá un momento y reintentá.")
			return
	if create: online_session.start_host(eos_config,player_name,PROTOCOL,_online_capability)
	else: online_session.start_join(eos_config,player_name,invitation)

func online_cleanup_pending() -> bool:
	return is_instance_valid(online_session) and (online_session.backend.busy or not online_session._cleanup.is_empty())

func shutdown_online_backend() -> bool:
	if not is_instance_valid(online_session): return true
	if online_cleanup_pending() or _online: return false
	var outcome: Dictionary=online_session.backend.shutdown()
	return bool(outcome.get("ok",false))

func _online_state(state: String) -> void:
	if not _online or state in ["idle","cancelled","error","transport_ready","renewing"]: return
	var message: String="Preparando conexión online…"
	if state in ["device","login","create_user"]: message="Conectando con el servicio online…"
	elif state=="create": message="Creando tu sala privada…"
	elif state in ["find","search"]: message="Buscando la sala de tu amigo…"
	elif state=="join": message="Entrando a la sala…"
	_set_connection_state("online_"+state,"",message)

func _online_failed(code: String) -> void:
	if not _online: return
	var joined:=_had_session
	var message: String="No se pudo completar la conexión online. Revisá tu conexión a Internet y reintentá."
	if code in ["room_not_found","lobby_not_found"]: message="Esta sala ya no está disponible. Pedile una invitación nueva al anfitrión."
	elif code in ["room_closed","room_owner_changed"]: message="El anfitrión cerró o dejó la sala."
	elif code=="room_protocol_or_owner_mismatch": message="La sala no corresponde a esta versión. Todos deben descargar la misma actualización."
	elif code in ["configuration_required","sdk_unavailable"]: message="A esta instalación le falta la configuración online. Descargá nuevamente la versión completa."
	elif code=="room_cleanup_failed_restart_required": message="No se pudo cerrar la conexión anterior. Reiniciá el juego antes de volver a conectar."
	_fail_connection(code,message)
	if joined: disconnected.emit()

func _online_packet_failed(_reason: String) -> void:
	# Defer teardown so a failed publish does not mutate players during iteration.
	var attempt:=_attempt_id
	_fail_packet_attempt.call_deferred(attempt)

func _fail_packet_attempt(attempt: int) -> void:
	if attempt==_attempt_id and _online:
		_fail_connection("online_packet_limit","La partida superó el tamaño permitido de mensajes. Volvé a crear la sala.")
		disconnected.emit()

func _online_ready(info: Dictionary) -> void:
	if not _online or not connecting: return
	_local_host=bool(info.is_host)
	is_server=_local_host
	var epoch: int=("LMS/epoch/"+str(info.lobby_id)).sha256_buffer().decode_u32(0)
	online_transport.reset(epoch)
	multiplayer.multiplayer_peer=info.peer
	(multiplayer as SceneMultiplayer).server_relay=false
	if _local_host:
		invitation_text=OnlineInvitation.encode(str(info.lobby_id),_online_capability)
		invitation_updated.emit(invitation_text)
		_set_connection_state("joining_room","","Abriendo tu sala...")
		_handle_request_join(1,VERSION,PROTOCOL,str(pending_join.name),_online_capability,true,"")
	else:
		invitation_text=OnlineInvitation.encode(str(info.lobby_id),_online_capability)
		invitation_updated.emit(invitation_text)
		_set_connection_state("connecting_transport","","Conectando con el anfitrion...")
		if multiplayer.multiplayer_peer.get_connection_status()==MultiplayerPeer.CONNECTION_CONNECTED:
			_connected()

func online_member(id: int) -> bool:
	if _local_host and id==1: return true
	if not _online or not is_instance_valid(online_session): return false
	return online_session.is_lobby_member(online_user_id(id))

func online_user_id(id: int) -> String:
	var peer: MultiplayerPeer=multiplayer.multiplayer_peer
	return str(peer.call("get_peer_user_id",id)) if peer!=null and peer.has_method("get_peer_user_id") else ""

func _online_member_departed(user_id: String) -> void:
	if not _online or not is_server: return
	# A confirmed EOS departure ends this membership's rejection generation.
	# A P2P-only disconnect does not: reconnecting a socket cannot bypass it.
	_online_rejected_users.erase(user_id)
	for id: int in _online_rejected.keys():
		if _online_rejected[id]==user_id: _online_rejected.erase(id)
	for id: int in _online_users.keys():
		if str(_online_users[id])!=user_id: continue
		if multiplayer.get_peers().has(id): _disconnect_peer(id)
		_peer_left(id)

func _disconnect_peer(id: int) -> void:
	var peer: MultiplayerPeer=multiplayer.multiplayer_peer
	if peer!=null: peer.disconnect_peer(id)

## Deferred local delivery mirrors network delivery, avoids map ACK reentrancy,
## and copies containers so client presentation cannot mutate server state.
func _send_to(id: int, method: String, args: Array) -> void:
	if _local_host and id==1:
		var attempt:=_attempt_id
		_local_dispatch.call_deferred(attempt,method,args.duplicate(true))
	elif _online:
		# EOS lobby membership can be revoked before the P2P disconnect arrives.
		# Apply this to every outbound route, including voice and permissions.
		if online_member(id): online_transport.send_message(id,method,args)
	else:
		var parameters: Array=[id,method];parameters.append_array(args)
		callv("rpc_id",parameters)

func _local_dispatch(attempt: int, method: String, args: Array) -> void:
	if attempt!=_attempt_id or not _local_host: return
	_dispatch_message(1,method,args,true)

func _dispatch_message(sender: int, method: String, args: Array, local: bool=false) -> void:
	if not _online or not OnlineTransport.valid_message(method,args): return
	if method.begins_with("voice:"):
		voice.dispatch_online(sender,method.trim_prefix("voice:"),args,local)
		return
	if method in ["_request_join","_request_lobby","_request_movement","_action"]:
		if not is_server or (not local and not online_member(sender)): return
		if method!="_request_join" and not players.has(sender): return
		var values: Array=[sender];values.append_array(args)
		callv("_handle"+method,values)
	elif sender==1 and (not is_server or local):
		callv(method,args)

func connect_room(address: String, port: int, player_name: String, code: String, create: bool) -> void:
	_online_request.clear()
	close_client()
	_attempt_id += 1
	_terminal_failure = false
	address = address.strip_edges()
	if address != str(_last_request.get("address", "")) or port != int(_last_request.get("port", 0)):
		token = ""
	_last_request = {"address":address,"port":port,"name":player_name,"code":code,"create":create}
	pending_join = {"name": player_name.strip_edges().left(20), "code": code.strip_edges().to_upper(), "create": create}
	if pending_join.name.is_empty():
		pending_join.name = "Amigo"
	if port < 1024 or port > 65535 or address.is_empty() or (address != "localhost" and not address.is_valid_ip_address() and not InvitationCodec.validate_host(address).is_empty()):
		_fail_connection("invalid_endpoint", "Revisá la dirección y el puerto UDP (1024–65535).")
		return
	if not create and not InvitationCodec._room_ok(str(pending_join.code)):
		_fail_connection("wrong_room", "El código de sala debe tener seis letras o números.")
		return
	connecting = true
	if address.is_valid_ip_address():
		_open_transport(address)
	else:
		_resolver_id = IP.resolve_hostname_queue_item(address, IP.TYPE_ANY)
		if _resolver_id == IP.RESOLVER_INVALID_ID:
			_fail_connection("dns_failed", "No se pudo resolver el nombre del servidor.")
			return
		_set_connection_state("resolving", "", "Buscando el servidor…")

func _open_transport(resolved: String) -> void:
	var peer := ENetMultiplayerPeer.new()
	var error := peer.create_client(resolved, int(_last_request.port), 4)
	if error != OK:
		_fail_connection("client_open_failed", "No se pudo abrir la conexión UDP: " + error_string(error))
		return
	multiplayer.multiplayer_peer = peer
	connecting = true
	_set_connection_state("connecting_transport", "", "Esperando respuesta UDP de %s:%d…" % [_last_request.address, _last_request.port])

func _set_connection_state(phase: String, code: String = "", message: String = "") -> void:
	_connection_phase = phase
	connect_age = 0.0
	connection_state = {"attempt":_attempt_id,"phase":phase,"code":code,"message":message,"address":str(_last_request.get("address","")),"port":int(_last_request.get("port",DEFAULT_PORT)),"elapsed":0.0,"can_retry":phase == "failed" and not _last_request.is_empty(),"can_cancel":phase.begins_with("online_") or phase in ["resolving","connecting_transport","joining_room"],"version":VERSION,"protocol":PROTOCOL}
	connection_state_changed.emit(connection_state.duplicate(true))
	if not message.is_empty():
		notice.emit(message)

func _fail_connection(code: String, message: String) -> void:
	_terminal_failure = true
	close_client()
	_set_connection_state("failed", code, message)

func cancel_connect() -> void:
	_attempt_id += 1
	_terminal_failure = true
	close_client()
	_set_connection_state("cancelled", "cancelled", "Conexión cancelada.")

func retry_connect() -> void:
	if connecting or _last_request.is_empty() or _had_session:
		return
	if not _online_request.is_empty():
		var retry: Dictionary=_online_request.duplicate(true)
		if retry.host: host_online(retry.config,retry.name)
		else: join_online(retry.config,retry.name,retry.invite)
		return
	var request := _last_request.duplicate(true)
	connect_room(str(request.address), int(request.port), str(request.name), str(request.code), bool(request.create))

func close_client() -> void:
	# Closing native peers can synchronously emit connection/loss callbacks.
	connecting=false
	_terminal_failure=true
	if _online:
		_online=false;_local_host=false;is_server=false
		players.clear();waiting_actors.clear();waiting_inputs.clear();join_attempts.clear();menu_rates.clear();private_revisions.clear();tokens.clear();peer_tokens.clear();_pending_map.clear()
		_online_rejected.clear()
		_online_rejected_users.clear()
		_online_users.clear()
		sim=null;server_tick=0;publish_accumulator=0.0;last_phase=""
		invitation_text="";_online_capability=""
		invitation_updated.emit("")
		if is_instance_valid(online_transport): online_transport.reset(0)
		if is_instance_valid(online_session): online_session.close()
	if is_instance_valid(voice): voice.reset()
	_prepared_map.clear()
	connecting = false
	_had_session = false
	_terminal_failure = true
	if _resolver_id != -1:
		IP.erase_resolve_item(_resolver_id)
		_resolver_id = -1
	if not is_server and multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
		multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
		room_owner = 0
		room_code = ""
	connecting = false
	latest.clear()
	private_latest.clear()
	last_received_tick = -1

func _connected() -> void:
	if not connecting or _connection_phase != "connecting_transport":
		return
	_set_connection_state("joining_room", "", "El servidor respondió. Verificando versión y sala…")
	_send_to(1, "_request_join", [VERSION, PROTOCOL, str(pending_join.get("name", "Amigo")), _online_capability if _online else str(pending_join.get("code", "")), bool(pending_join.get("create", false)), token])

func _failed() -> void:
	if connecting:
		if _online:
			_fail_connection("transport_failed","No se pudo conectar con el anfitrión. Confirmá que la sala siga abierta y reintentá.")
			return
		_fail_connection("transport_failed", "No llegó respuesta UDP de %s:%d. Revisá el servidor y la ruta de conexión." % [_last_request.get("address",""),_last_request.get("port",DEFAULT_PORT)])

func _server_lost() -> void:
	if _terminal_failure:
		return
	var accepted_session := _had_session
	_fail_connection("server_lost" if accepted_session else "handshake_lost", "Se perdió la conexión con el servidor." if accepted_session else "Se perdió la conexión antes de aceptar la sala.")
	if accepted_session:
		disconnected.emit()

func _peer_connected(id: int) -> void:
	if is_server:
		join_attempts[id] = {"count": 0, "since": Time.get_ticks_msec()}
		var peer: ENetMultiplayerPeer = multiplayer.multiplayer_peer as ENetMultiplayerPeer
		if peer != null: peer.get_peer(id).set_timeout(8, 3000, 6000)
		var attempt:=_attempt_id
		var transport:=multiplayer.multiplayer_peer
		# Peers that never introduce themselves cannot occupy server slots indefinitely.
		get_tree().create_timer(12.0).timeout.connect(func() -> void:
			if attempt==_attempt_id and transport==multiplayer.multiplayer_peer and is_server and not players.has(id) and multiplayer.get_peers().has(id):
				_disconnect_peer(id))

func _peer_left(id: int) -> void:
	_online_rejected.erase(id)
	_online_users.erase(id)
	if is_instance_valid(online_transport): online_transport.forget_sender(id)
	if is_instance_valid(voice): voice.peer_left(id)
	if not is_server:
		return
	join_attempts.erase(id)
	menu_rates.erase(id)
	private_revisions.erase(id)
	if not players.has(id):
		return
	_pending_map.clear()
	if id == room_owner:
		_close_room("El anfitrión cerró la sala.")
		return
	var departed_name: String = str(players[id].name)
	if peer_tokens.has(id):
		tokens[peer_tokens[id]] = {"name": players[id].name, "role": players[id].role}
		peer_tokens.erase(id)
	players.erase(id)
	waiting_actors.erase(id)
	waiting_inputs.erase(id)
	if sim != null:
		var ended: Dictionary = sim.public_snapshot()
		if str(ended.phase) == "playing":
			sim.abort("%s se desconectó. Ronda interrumpida sin ganador." % departed_name)
			_notify_all("%s se desconectó. Ronda interrumpida sin ganador." % departed_name)
		else:
			_notify_all("%s %s salió; volvimos a la sala." % [str(ended.get("reason", "Ronda terminada.")), departed_name])
		sim = null
		private_revisions.clear()
		_set_unready()
		_reset_waiting()
	if players.is_empty():
		room_code = ""
		config = Simulation.DEFAULT_CONFIG.duplicate(true)
		tokens.clear()
	_broadcast_lobby()

func request_close_room() -> void:
	if _client_connected() and multiplayer.get_unique_id() == room_owner:
		_send_to(1, "_request_lobby", ["close_room",null])

func _close_room(message: String) -> void:
	_pending_map.clear()
	var recipients := players.keys()
	for id: int in recipients:
		if _local_host and id==1: continue
		if _peer_can_receive(id):
			_send_to(id, "_server_notice", ["@room_closed:" + message])
	players.clear()
	waiting_actors.clear()
	waiting_inputs.clear()
	private_revisions.clear()
	peer_tokens.clear()
	tokens.clear()
	room_owner = 0
	room_code = ""
	sim = null
	config = Simulation.DEFAULT_CONFIG.duplicate(true)
	var closing_online := _online
	var peer := multiplayer.multiplayer_peer
	var attempt:=_attempt_id
	get_tree().create_timer(0.2).timeout.connect(func() -> void:
		if attempt!=_attempt_id or peer!=multiplayer.multiplayer_peer: return
		if closing_online:
			if _online: _room_closed(message)
		elif peer != null:
			for id: int in recipients:
				if multiplayer.get_peers().has(id): peer.disconnect_peer(id))

func _room_closed(message: String) -> void:
	if not _had_session:
		return
	close_client()
	_set_connection_state("closed","host_closed",message)
	disconnected.emit()

@rpc("any_peer", "call_remote", "reliable", 0)
func _request_join(version: String, protocol: int, player_name: String, code: String, create: bool, previous_token: String) -> void:
	if not _online: _handle_request_join(multiplayer.get_remote_sender_id(), version, protocol, player_name, code, create, previous_token)

func _handle_request_join(sender: int, version: String, protocol: int, player_name: String, code: String, create: bool, previous_token: String) -> void:
	if not is_server:
		return
	if _online and (_online_rejected.has(sender) or _online_rejected_users.has(online_user_id(sender))): return
	if players.has(sender):
		return
	var attempt: Dictionary = join_attempts.get(sender, {"count": 0, "since": Time.get_ticks_msec()})
	attempt.count += 1
	join_attempts[sender] = attempt
	if int(attempt.count) > 5:
		_reject_join(sender, "Demasiados intentos. Reconectá y revisá el código.")
		return
	if version != VERSION or protocol != PROTOCOL:
		_reject_join(sender, "Versión incompatible. Todos necesitan Let me sleep %s (protocolo %d)." % [VERSION, PROTOCOL])
		return
	if sim != null or not _pending_map.is_empty():
		_reject_join(sender, "El grupo está jugando o viendo resultados. Esperá a que el anfitrión pulse Volver a sala.")
		return
	if players.size() >= MAX_PLAYERS:
		_reject_join(sender, "La sala está llena (16 lugares de prueba).")
		return
	if _online and ((sender!=1 and not online_member(sender)) or (create and sender!=1) or code!=_online_capability):
		_reject_join(sender,"Invitación online inválida. Pedile una nueva al anfitrión.")
		return
	if create:
		if not room_code.is_empty():
			_reject_join(sender, "Ya hay una sala en este servidor. Pedile su código al anfitrión.")
			return
		room_code = _new_code()
		room_owner = sender
	elif (_online and code != _online_capability) or (not _online and (room_code.is_empty() or code.to_upper() != room_code)):
		_reject_join(sender, "Código de sala incorrecto. Revisá el código y la dirección del servidor.")
		return
	if _online and sender!=1:
		var user_id: String=online_user_id(sender)
		if not online_session.mark_peer_admitted(user_id):
			_reject_join(sender,"La sala dejó de estar disponible. Pedile una invitación nueva al anfitrión.")
			return
		_online_users[sender]=user_id
	var clean_name := player_name.strip_edges().left(20).replace("\n", " ").replace("\r", " ")
	if clean_name.is_empty():
		clean_name = "Amigo"
	if previous_token.length() == 32 and tokens.has(previous_token):
		tokens.erase(previous_token)
	var new_token := Crypto.new().generate_random_bytes(16).hex_encode()
	peer_tokens[sender] = new_token
	players[sender] = {"name": clean_name, "role": "waiting", "ready": false, "cosmetics": Cosmetics.sanitize({})}
	_spawn_waiting(sender)
	_send_to(sender, "_welcome", [sender, new_token])
	print("JOIN id=%d role=waiting count=%d" % [sender, players.size()])
	_broadcast_lobby()

func _reject_join(sender: int, message: String) -> void:
	_send_to(sender,"_reject",[message])
	if not _online or sender==1: return
	var user_id: String=online_user_id(sender)
	_online_rejected[sender]=user_id
	if user_id.is_empty(): return
	if _online_rejected_users.has(user_id): return
	_online_reject_serial+=1
	var serial:=_online_reject_serial
	_online_rejected_users[user_id]=serial
	var attempt:=_attempt_id
	var peer:=multiplayer.multiplayer_peer
	# Allow the explicit rejection to leave before removing the EOS lobby slot.
	get_tree().create_timer(0.2).timeout.connect(func() -> void:
		if attempt!=_attempt_id or not _online or peer!=multiplayer.multiplayer_peer: return
		if int(_online_rejected_users.get(user_id,-1))!=serial: return
		online_session.reject_peer(user_id)
		# The same EOS member may already have replaced its P2P socket.
		for id: int in multiplayer.get_peers():
			if online_user_id(id)==user_id: _disconnect_peer(id))

func _new_code() -> String:
	const LETTERS := "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
	var bytes := Crypto.new().generate_random_bytes(6)
	var code := ""
	for value: int in bytes:
		code += LETTERS[value % LETTERS.length()]
	return code

@rpc("authority", "call_remote", "reliable", 0)
func _welcome(id: int, new_token: String) -> void:
	if not connecting or _connection_phase != "joining_room":
		return
	connecting = false
	_had_session = true
	token = new_token
	_set_connection_state("joined", "", "¡Ya estás en la sala!")
	accepted.emit(id)
	lobby_action("cosmetics", local_cosmetics)

@rpc("authority", "call_remote", "reliable", 0)
func _reject(message: String) -> void:
	if not connecting:
		return
	var code := "join_rejected"
	if message.begins_with("Versión"):
		code = "version_mismatch"
	elif message.begins_with("Código"):
		code = "wrong_room"
	elif message.contains("llena"):
		code = "room_full"
	elif message.contains("está jugando"):
		code = "room_playing"
	elif message.begins_with("Ya hay"):
		code = "room_exists"
	_fail_connection(code, message)

@rpc("authority", "call_remote", "reliable", 0)
func _server_notice(message: String) -> void:
	if message.begins_with("@room_closed:"):
		_room_closed(message.trim_prefix("@room_closed:"))
		return
	notice.emit(message)

func lobby_action(verb: String, value: Variant = null) -> void:
	if _client_connected():
		_send_to(1, "_request_lobby", [verb, value])

func _client_connected() -> bool:
	return (_local_host and _had_session) or (not is_server and multiplayer.multiplayer_peer != null and multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED and multiplayer.get_unique_id() != 1)

@rpc("any_peer", "call_remote", "reliable", 0)
func _request_lobby(verb: String, value: Variant) -> void:
	if not _online: _handle_request_lobby(multiplayer.get_remote_sender_id(), verb, value)

func _handle_request_lobby(sender: int, verb: String, value: Variant) -> void:
	if not is_server:
		return
	if not players.has(sender) or not _menu_rate_ok(sender):
		return
	if verb == "close_room":
		if sender == room_owner:
			_close_room("El anfitrión cerró la sala.")
		return
	if sim != null and verb != "rematch":
		return
	if verb in ["map_ready","map_failed"]:
		_receive_map_ack(sender,verb,value)
		return
	if not _pending_map.is_empty():
		_send_to(sender, "_server_notice", ["Estamos preparando la casa para todos los jugadores."])
		return
	match verb:
		"role":
			_send_to(sender, "_server_notice", ["Los roles se sortean al empezar. Nadie elige equipo."])
			return
		"cosmetics":
			players[sender].cosmetics = Cosmetics.sanitize(value)
			waiting_actors[sender].appearance = Cosmetics.appearance_for(players[sender].cosmetics, "human")
		"ready":
			if value is bool:
				if bool(players[sender].ready) == value:
					return
				players[sender].ready = value
		"config":
			if sender == room_owner and value is Dictionary:
				var chosen: Variant = value.get("human_count", config.get("human_count", 1))
				if not (chosen is int or chosen is float) or not is_finite(float(chosen)) or float(chosen) != floorf(float(chosen)) or int(chosen) < 1 or int(chosen) > 5:
					_send_to(sender, "_server_notice", ["La cantidad de humanos debe ser un entero entre 1 y 5."])
					return
				config = sanitize_config(value)
				_set_unready()
		"start":
			if sender == room_owner:
				var reason := _start_reason()
				if reason.is_empty():
					_prepare_round()
					return
				_send_to(sender, "_server_notice", [reason])
		"rematch":
			if sender == room_owner and sim != null and str(sim.public_snapshot().phase) == "results":
				sim = null
				private_revisions.clear()
				_set_unready()
				_reset_waiting()
			else:
				return
	_broadcast_lobby()

func _menu_rate_ok(sender: int) -> bool:
	var now := Time.get_ticks_msec()
	var rate: Dictionary = menu_rates.get(sender, {"time": now, "count": 0})
	if now - int(rate.time) > 1000:
		rate = {"time": now, "count": 0}
	rate.count += 1
	menu_rates[sender] = rate
	return int(rate.count) <= 20

static func sanitize_config(value: Dictionary) -> Dictionary:
	return Simulation.sanitize_config(value)

func _set_unready() -> void:
	for id: int in players:
		players[id].ready = false

func _start_reason() -> String:
	if not _pending_map.is_empty(): return "Preparando la misma casa para todos…"
	return LobbyRules.validate(players, config, true)

func _prepare_round() -> void:
	var generated: Dictionary=Maps.new_house()
	if generated.is_empty():
		_notify_all("No se pudo generar una casa transitable. Intentá empezar otra vez.")
		return
	if str(generated.id)==str(config.get("map_id","")):
		generated=Maps.new_house(1+int(generated.seed)%2147483646)
		if generated.is_empty(): return
	_pending_map={"id":generated.id,"fingerprint":generated.fingerprint,"version":generated.generator_version,"seed":generated.seed,"age":0.0,"ready":{}}
	_broadcast_lobby()

func _receive_map_ack(sender: int, verb: String, value: Variant) -> void:
	if _pending_map.is_empty() or not value is Dictionary: return
	if value.get("id")!=_pending_map.id: return
	if verb=="map_failed" or value.get("fingerprint")!=_pending_map.fingerprint:
		_pending_map.clear()
		_notify_all("La casa no coincide en todas las computadoras. Revisen que tengan la misma versión.")
		_broadcast_lobby()
		return
	_pending_map.ready[sender]=true
	for id: int in players:
		if not _pending_map.ready.has(id): return
	var prepared:=_pending_map.duplicate(true)
	_pending_map.clear()
	config.map_id=prepared.id
	server_tick+=1
	sim=Simulation.new()
	sim.start(LobbyRules.draw(players,config),config)
	if sim.phase!="playing":
		var reason: String=sim.reason
		sim=null
		_notify_all(reason)
		_broadcast_lobby()
		return
	config=sim.config.duplicate(true)
	private_revisions.clear();last_phase=""
	print("ROUND_START mode=%s players=%d map=%s fingerprint=%s"%[config.mode,players.size(),prepared.id,prepared.fingerprint])
	_publish(true)

func _broadcast_lobby() -> void:
	if players.is_empty():
		return
	var reason := _start_reason()
	var data := {"code": room_code, "owner": room_owner, "players": players.duplicate(true), "config": config.duplicate(true), "can_start": reason.is_empty(), "start_reason": reason, "barrier_tick": server_tick, "actors": waiting_actors.duplicate(true)}
	if not _pending_map.is_empty():
		data.map_prepare={"id":_pending_map.id,"fingerprint":_pending_map.fingerprint,"version":_pending_map.version,"seed":_pending_map.seed}
	for id: int in players:
		if _peer_can_receive(id):
			_send_to(id, "_receive_lobby", [data])

func _peer_can_receive(id: int) -> bool:
	if _local_host and id==1: return _had_session or connecting
	if _online: return multiplayer.get_peers().has(id) and online_member(id)
	if not multiplayer.get_peers().has(id):
		return false
	var peer := multiplayer.multiplayer_peer as ENetMultiplayerPeer
	return peer != null and peer.get_peer(id).get_state() == ENetPacketPeer.STATE_CONNECTED

func _notify_all(message: String) -> void:
	for id: int in players:
		if _peer_can_receive(id):
			_send_to(id, "_server_notice", [message])

@rpc("authority", "call_remote", "reliable", 0)
func _receive_lobby(data: Dictionary) -> void:
	# Reliable lobby updates may trail a newer round snapshot on another channel.
	# A real return to the lobby carries the current round tick as its barrier.
	var barrier := int(data.get("barrier_tick", -1))
	if str(latest.get("phase", "")) in ["playing", "results"] and barrier < last_received_tick:
		return
	room_owner = int(data.get("owner",0))
	room_code = str(data.get("code",""))
	latest.clear()
	private_latest.clear()
	last_received_tick = barrier + 1
	lobby_updated.emit(data)
	if data.get("map_prepare") is Dictionary:
		var prepare: Dictionary=data.map_prepare
		var id: String=str(prepare.get("id",""))
		var generated: Dictionary=Maps.get_map(id)
		var matched: bool=not generated.is_empty() and generated.get("fingerprint")==prepare.get("fingerprint") and generated.get("generator_version")==prepare.get("version") and generated.get("seed")==prepare.get("seed")
		if matched:
			_prepared_map={"id":id,"fingerprint":generated.fingerprint}
			_send_to(1, "_request_lobby", ["map_ready",_prepared_map])
		else:
			_prepared_map.clear()
			_send_to(1, "_request_lobby", ["map_failed",{"id":id}])
			notice.emit("No se pudo verificar la casa de esta partida.")

func send_input(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if _client_connected():
		_send_to(1, "_request_movement", [seq, move, yaw, pitch, interact, sprint, crouch, jump])

func send_action(seq: int, verb: String, aim_yaw: float = NAN, aim_pitch: float = NAN) -> void:
	if _client_connected():
		# Keep the bootstrap RPC table/arity stable so older clients receive the
		# explicit version rejection instead of calling the wrong RPC index.
		var command := verb
		if is_finite(aim_yaw) and is_finite(aim_pitch):
			command = JSON.stringify({"verb":verb,"yaw":aim_yaw,"pitch":aim_pitch})
		elif not (is_nan(aim_yaw) and is_nan(aim_pitch)):
			return
		_send_to(1, "_action", [seq, command])

func send_emote(seq: int, emote_id: String) -> void:
	if _client_connected() and emote_id.length()<24:
		_send_to(1, "_action", [seq,JSON.stringify({"verb":"emote","id":emote_id})])

func send_view_ack(seq: int, revision: int, first_input_seq: int, yaw: float, pitch: float) -> void:
	if _client_connected():
		_send_to(1, "_action", [seq,JSON.stringify({"verb":"view_ack","revision":revision,"first_input_seq":first_input_seq,"yaw":yaw,"pitch":pitch})])

@rpc("any_peer", "call_remote", "unreliable_ordered", 1)
func _request_movement(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if not _online: _handle_request_movement(multiplayer.get_remote_sender_id(), seq, move, yaw, pitch, interact, sprint, crouch, jump)

func _handle_request_movement(sender: int, seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if is_server and sim == null:
		if not players.has(sender) or not move.is_finite() or not is_finite(yaw) or not is_finite(pitch):
			return
		if seq <= int(waiting_inputs.get(sender, {}).get("seq", -1)):
			return
		waiting_inputs[sender] = {"seq": seq, "move": Vector3(move.x, 0, move.z).limit_length(1.0), "yaw": wrapf(yaw, -PI, PI), "pitch":clampf(pitch,-1.4,1.3), "sprint":sprint, "crouch":crouch, "jump":jump, "time": Time.get_ticks_msec()}
		return
	if is_server and sim != null:
		if players.has(sender):
			sim.submit_input(sender, seq, move, yaw, pitch, interact, sprint, crouch, jump)

@rpc("any_peer", "call_remote", "reliable", 0)
func _action(seq: int, verb: String) -> void:
	if not _online: _handle_action(multiplayer.get_remote_sender_id(), seq, verb)

func _handle_action(sender: int, seq: int, verb: String) -> void:
	if is_server and sim != null:
		if not players.has(sender) or verb.length() > 256:
			return
		var aim_yaw := NAN
		var aim_pitch := NAN
		if verb.begins_with("{"):
			var command: Variant = JSON.parse_string(verb)
			if command is Dictionary and command.get("verb")=="view_ack":
				if command.size()!=5: return
				for key: String in ["revision","first_input_seq","yaw","pitch"]:
					if not (command.get(key) is float or command.get(key) is int) or not is_finite(float(command[key])): return
				for key: String in ["revision","first_input_seq"]:
					if float(command[key])!=floorf(float(command[key])) or float(command[key])<0 or float(command[key])>2147483647: return
				sim.submit_view_ack(sender,seq,int(command.revision),int(command.first_input_seq),float(command.yaw),float(command.pitch))
				return
			if command is Dictionary and command.get("verb")=="emote":
				if command.size()==2 and command.get("id") is String and str(command.id).length()<24:
					sim.submit_emote(sender,seq,command.id)
				return
			if not command is Dictionary or not command.get("verb") is String or not command.get("yaw") is float or not command.get("pitch") is float:
				return
			verb = command.verb
			aim_yaw = command.yaw
			aim_pitch = command.pitch
		if verb.length() < 24:
			sim.action(sender, seq, verb, aim_yaw, aim_pitch)

func _physics_process(dt: float) -> void:
	if is_server and not _pending_map.is_empty():
		_pending_map.age=float(_pending_map.age)+dt
		if float(_pending_map.age)>MAP_PREPARE_TIMEOUT:
			_pending_map.clear()
			_notify_all("Un jugador no terminó de preparar la casa. Intentá empezar de nuevo.")
			_broadcast_lobby()
	if connecting and _connection_phase in ["resolving","connecting_transport","joining_room"]:
		connect_age += dt
		connection_state["elapsed"] = connect_age
		if _connection_phase == "resolving" and _resolver_id != -1:
			var status := IP.get_resolve_item_status(_resolver_id)
			if status == IP.RESOLVER_STATUS_DONE:
				var resolved := IP.get_resolve_item_address(_resolver_id)
				IP.erase_resolve_item(_resolver_id)
				_resolver_id = -1
				_open_transport(resolved)
			elif status == IP.RESOLVER_STATUS_ERROR:
				_fail_connection("dns_failed", "No se pudo resolver el nombre del servidor.")
		if connecting and connect_age > (HANDSHAKE_TIMEOUT if _connection_phase == "joining_room" else (30.0 if _online else TRANSPORT_TIMEOUT)):
			if _connection_phase == "joining_room":
				_fail_connection("handshake_timeout", "El servidor respondió, pero no completó la entrada. Revisá que ambos usen Let me sleep %s." % VERSION)
			elif _connection_phase == "resolving":
				_fail_connection("dns_timeout", "El nombre del servidor no se resolvió a tiempo.")
			else:
				if _online:
					_fail_connection("transport_timeout","El anfitrión no respondió a tiempo. Pedile que confirme que su sala sigue abierta y volvé a intentar.")
				else:
					_fail_connection("transport_timeout", "Sin respuesta UDP de %s:%d. No se pudo comprobar la ruta hasta el anfitrión." % [_last_request.address,_last_request.port])
	if not is_server:
		return
	if sim != null:
		sim.step(dt)
	else:
		_step_waiting(dt)
	server_tick += 1
	publish_accumulator += dt
	if publish_accumulator >= 0.05:
		publish_accumulator = 0.0
		if sim == null:
			_publish_waiting()
		else:
			_publish(false)

func _spawn_waiting(id: int) -> void:
	var index := players.keys().find(id)
	var position: Vector3 = Map.human_spawn(index,"lobby")
	waiting_actors[id] = {"name": players[id].name, "role": "human", "p": position, "yaw": 0.0, "pitch": 0.0, "state": "human", "alive": true, "swing": 0.0, "bitten": false, "tool": "hands", "appearance": Cosmetics.appearance_for(players[id].cosmetics, "human")}

func _reset_waiting() -> void:
	waiting_actors.clear()
	waiting_inputs.clear()
	for id: int in players:
		_spawn_waiting(id)

func _step_waiting(dt: float) -> void:
	for id: int in waiting_actors:
		var intent: Dictionary = waiting_inputs.get(id, {})
		var actor: Dictionary = waiting_actors[id]
		if intent.is_empty() or Time.get_ticks_msec() - int(intent.time) > 400:
			intent = {"move":Vector3.ZERO,"yaw":actor.yaw,"pitch":actor.pitch,"sprint":false,"crouch":false,"jump":false}
		Map.step_human(actor,intent,dt,"lobby")

func _publish_waiting() -> void:
	if players.is_empty():
		return
	var state := {"phase": "waiting", "tick": server_tick, "actors": waiting_actors.duplicate(true)}
	var packet := var_to_bytes(state).compress(FileAccess.COMPRESSION_DEFLATE)
	for id: int in players:
		if _peer_can_receive(id):
			_send_snapshot(id,packet)

func _send_snapshot(id: int,packet: PackedByteArray) -> void:
	if packet.size()>MAX_UNRELIABLE_SNAPSHOT_BYTES:
		_send_to(id, "_receive_snapshot_reliable", [packet])
	else:
		_send_to(id, "_receive_snapshot", [packet])

func _publish(force_reliable: bool) -> void:
	if sim == null:
		return
	var state: Dictionary = sim.public_snapshot()
	state["tick"] = server_tick
	# Repeated field names make compact dictionary snapshots highly compressible.
	# Larger object-rich states use reliable ENet fragmentation on the snapshot
	# channel; control and private messages retain their separate channels.
	var packet := var_to_bytes(state).compress(FileAccess.COMPRESSION_DEFLATE)
	var changed := str(state.phase) != last_phase
	if changed:
		last_phase = str(state.phase)
		if last_phase == "results":
			print("ROUND_RESULT winner=%s reason=%s" % [state.get("winner", ""), state.get("reason", "")])
	for id: int in players:
		if not _peer_can_receive(id):
			continue
		if changed or force_reliable:
			_send_to(id, "_receive_round", [state])
		else:
			_send_snapshot(id,packet)
		var private_data: Dictionary = sim.private_for(id)
		private_data["tick"] = server_tick
		var revision := int(private_data.get("assignment", {}).get("revision", -1))
		if changed or force_reliable or int(private_revisions.get(id, -2)) != revision:
			_send_to(id, "_receive_private_reliable", [private_data])
			private_revisions[id] = revision
		else:
			_send_to(id, "_receive_private", [private_data])

@rpc("authority", "call_remote", "reliable", 0)
func _receive_round(data: Dictionary) -> void:
	_accept_snapshot(data)

@rpc("authority", "call_remote", "unreliable_ordered", 1)
func _receive_snapshot(packet: PackedByteArray) -> void:
	var raw := packet.decompress_dynamic(65536, FileAccess.COMPRESSION_DEFLATE)
	var data: Variant = bytes_to_var(raw)
	if data is Dictionary:
		_accept_snapshot(data)

@rpc("authority", "call_remote", "reliable", 1)
func _receive_snapshot_reliable(packet: PackedByteArray) -> void:
	_receive_snapshot(packet)

func _accept_snapshot(data: Dictionary) -> void:
	var map_id: String=str(data.get("config",{}).get("map_id","house"))
	if Maps.Generator.parse_seed(map_id)>0:
		if _prepared_map.get("id")!=map_id or _prepared_map.get("fingerprint")!=data.get("config",{}).get("map_fingerprint"):
			notice.emit("Se rechazó una partida cuya casa no estaba verificada.")
			return
	var tick := int(data.get("tick", 0))
	if tick < last_received_tick:
		return
	last_received_tick = tick
	latest = data
	if str(data.get("phase", "")) == "waiting":
		waiting_updated.emit(data)
	else:
		snapshot_updated.emit(data)

@rpc("authority", "call_remote", "unreliable_ordered", 2)
func _receive_private(data: Dictionary) -> void:
	if int(data.get("tick", 0)) < maxi(last_received_tick, int(private_latest.get("tick", -1))):
		return
	private_latest = data
	private_updated.emit(data)

@rpc("authority", "call_remote", "reliable", 0)
func _receive_private_reliable(data: Dictionary) -> void:
	_receive_private(data)
