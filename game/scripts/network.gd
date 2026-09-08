extends Node

signal lobby_updated(data: Dictionary)
signal snapshot_updated(data: Dictionary)
signal private_updated(data: Dictionary)
signal notice(message: String)
signal accepted(peer_id: int)
signal disconnected
signal waiting_updated(data: Dictionary)
signal connection_state_changed(data: Dictionary)

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

func connect_room(address: String, port: int, player_name: String, code: String, create: bool) -> void:
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
	connection_state = {"attempt":_attempt_id,"phase":phase,"code":code,"message":message,"address":str(_last_request.get("address","")),"port":int(_last_request.get("port",DEFAULT_PORT)),"elapsed":0.0,"can_retry":phase == "failed" and not _last_request.is_empty(),"can_cancel":phase in ["resolving","connecting_transport","joining_room"],"version":VERSION,"protocol":PROTOCOL}
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
	var request := _last_request.duplicate(true)
	connect_room(str(request.address), int(request.port), str(request.name), str(request.code), bool(request.create))

func close_client() -> void:
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
	_request_join.rpc_id(1, VERSION, PROTOCOL, str(pending_join.get("name", "Amigo")), str(pending_join.get("code", "")), bool(pending_join.get("create", false)), token)

func _failed() -> void:
	if connecting:
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
		peer.get_peer(id).set_timeout(8, 3000, 6000)
		# Peers that never introduce themselves cannot occupy server slots indefinitely.
		get_tree().create_timer(12.0).timeout.connect(func() -> void:
			if is_server and not players.has(id) and multiplayer.get_peers().has(id):
				peer.disconnect_peer(id))

func _peer_left(id: int) -> void:
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
		_request_lobby.rpc_id(1,"close_room",null)

func _close_room(message: String) -> void:
	_pending_map.clear()
	var recipients := players.keys()
	for id: int in recipients:
		if _peer_can_receive(id):
			_server_notice.rpc_id(id,"@room_closed:" + message)
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
	var peer := multiplayer.multiplayer_peer as ENetMultiplayerPeer
	get_tree().create_timer(0.2).timeout.connect(func() -> void:
		if peer != null:
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
	if not is_server:
		return
	var sender := multiplayer.get_remote_sender_id()
	if players.has(sender):
		return
	var attempt: Dictionary = join_attempts.get(sender, {"count": 0, "since": Time.get_ticks_msec()})
	attempt.count += 1
	join_attempts[sender] = attempt
	if int(attempt.count) > 5:
		_reject.rpc_id(sender, "Demasiados intentos. Reconectá y revisá el código.")
		return
	if version != VERSION or protocol != PROTOCOL:
		_reject.rpc_id(sender, "Versión incompatible. Todos necesitan Let me sleep %s (protocolo %d)." % [VERSION, PROTOCOL])
		return
	if sim != null or not _pending_map.is_empty():
		_reject.rpc_id(sender, "El grupo está jugando o viendo resultados. Esperá a que el anfitrión pulse Volver a sala.")
		return
	if players.size() >= MAX_PLAYERS:
		_reject.rpc_id(sender, "La sala está llena (16 lugares de prueba).")
		return
	if create:
		if not room_code.is_empty():
			_reject.rpc_id(sender, "Ya hay una sala en este servidor. Pedile su código al anfitrión.")
			return
		room_code = _new_code()
		room_owner = sender
	elif room_code.is_empty() or code.to_upper() != room_code:
		_reject.rpc_id(sender, "Código de sala incorrecto. Revisá el código y la dirección del servidor.")
		return
	var clean_name := player_name.strip_edges().left(20).replace("\n", " ").replace("\r", " ")
	if clean_name.is_empty():
		clean_name = "Amigo"
	if previous_token.length() == 32 and tokens.has(previous_token):
		tokens.erase(previous_token)
	var new_token := Crypto.new().generate_random_bytes(16).hex_encode()
	peer_tokens[sender] = new_token
	players[sender] = {"name": clean_name, "role": "waiting", "ready": false, "cosmetics": Cosmetics.sanitize({})}
	_spawn_waiting(sender)
	_welcome.rpc_id(sender, sender, new_token)
	print("JOIN id=%d role=waiting count=%d" % [sender, players.size()])
	_broadcast_lobby()

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
		_request_lobby.rpc_id(1, verb, value)

func _client_connected() -> bool:
	return not is_server and multiplayer.multiplayer_peer != null and multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED and multiplayer.get_unique_id() != 1

@rpc("any_peer", "call_remote", "reliable", 0)
func _request_lobby(verb: String, value: Variant) -> void:
	if not is_server:
		return
	var sender := multiplayer.get_remote_sender_id()
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
		_server_notice.rpc_id(sender,"Estamos preparando la casa para todos los jugadores.")
		return
	match verb:
		"role":
			_server_notice.rpc_id(sender, "Los roles se sortean al empezar. Nadie elige equipo.")
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
					_server_notice.rpc_id(sender, "La cantidad de humanos debe ser un entero entre 1 y 5.")
					return
				config = sanitize_config(value)
				_set_unready()
		"start":
			if sender == room_owner:
				var reason := _start_reason()
				if reason.is_empty():
					_prepare_round()
					return
				_server_notice.rpc_id(sender, reason)
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
			_receive_lobby.rpc_id(id, data)

func _peer_can_receive(id: int) -> bool:
	if not multiplayer.get_peers().has(id):
		return false
	var peer := multiplayer.multiplayer_peer as ENetMultiplayerPeer
	return peer != null and peer.get_peer(id).get_state() == ENetPacketPeer.STATE_CONNECTED

func _notify_all(message: String) -> void:
	for id: int in players:
		if _peer_can_receive(id):
			_server_notice.rpc_id(id, message)

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
			_request_lobby.rpc_id(1,"map_ready",_prepared_map)
		else:
			_prepared_map.clear()
			_request_lobby.rpc_id(1,"map_failed",{"id":id})
			notice.emit("No se pudo verificar la casa de esta partida.")

func send_input(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if _client_connected():
		_request_movement.rpc_id(1, seq, move, yaw, pitch, interact, sprint, crouch, jump)

func send_action(seq: int, verb: String, aim_yaw: float = NAN, aim_pitch: float = NAN) -> void:
	if _client_connected():
		# Keep the bootstrap RPC table/arity stable so older clients receive the
		# explicit version rejection instead of calling the wrong RPC index.
		var command := verb
		if is_finite(aim_yaw) and is_finite(aim_pitch):
			command = JSON.stringify({"verb":verb,"yaw":aim_yaw,"pitch":aim_pitch})
		elif not (is_nan(aim_yaw) and is_nan(aim_pitch)):
			return
		_action.rpc_id(1, seq, command)

func send_emote(seq: int, emote_id: String) -> void:
	if _client_connected() and emote_id.length()<24:
		_action.rpc_id(1,seq,JSON.stringify({"verb":"emote","id":emote_id}))

func send_view_ack(seq: int, revision: int, first_input_seq: int, yaw: float, pitch: float) -> void:
	if _client_connected():
		_action.rpc_id(1,seq,JSON.stringify({"verb":"view_ack","revision":revision,"first_input_seq":first_input_seq,"yaw":yaw,"pitch":pitch}))

@rpc("any_peer", "call_remote", "unreliable_ordered", 1)
func _request_movement(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if is_server and sim == null:
		var sender := multiplayer.get_remote_sender_id()
		if not players.has(sender) or not move.is_finite() or not is_finite(yaw) or not is_finite(pitch):
			return
		if seq <= int(waiting_inputs.get(sender, {}).get("seq", -1)):
			return
		waiting_inputs[sender] = {"seq": seq, "move": Vector3(move.x, 0, move.z).limit_length(1.0), "yaw": wrapf(yaw, -PI, PI), "pitch":clampf(pitch,-1.4,1.3), "sprint":sprint, "crouch":crouch, "jump":jump, "time": Time.get_ticks_msec()}
		return
	if is_server and sim != null:
		var sender := multiplayer.get_remote_sender_id()
		if players.has(sender):
			sim.submit_input(sender, seq, move, yaw, pitch, interact, sprint, crouch, jump)

@rpc("any_peer", "call_remote", "reliable", 0)
func _action(seq: int, verb: String) -> void:
	if is_server and sim != null:
		var sender := multiplayer.get_remote_sender_id()
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
	if connecting:
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
		if connecting and connect_age > (HANDSHAKE_TIMEOUT if _connection_phase == "joining_room" else TRANSPORT_TIMEOUT):
			if _connection_phase == "joining_room":
				_fail_connection("handshake_timeout", "El servidor respondió, pero no completó la entrada. Revisá que ambos usen Let me sleep %s." % VERSION)
			elif _connection_phase == "resolving":
				_fail_connection("dns_timeout", "El nombre del servidor no se resolvió a tiempo.")
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
		_receive_snapshot_reliable.rpc_id(id,packet)
	else:
		_receive_snapshot.rpc_id(id,packet)

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
			_receive_round.rpc_id(id, state)
		else:
			_send_snapshot(id,packet)
		var private_data: Dictionary = sim.private_for(id)
		private_data["tick"] = server_tick
		var revision := int(private_data.get("assignment", {}).get("revision", -1))
		if changed or force_reliable or int(private_revisions.get(id, -2)) != revision:
			_receive_private_reliable.rpc_id(id, private_data)
			private_revisions[id] = revision
		else:
			_receive_private.rpc_id(id, private_data)

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
