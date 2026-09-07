extends Node

signal lobby_updated(data: Dictionary)
signal snapshot_updated(data: Dictionary)
signal private_updated(data: Dictionary)
signal notice(message: String)
signal accepted(peer_id: int)
signal disconnected

const Simulation = preload("res://scripts/simulation.gd")
const VERSION := "0.1.0"
const PROTOCOL := 1
const DEFAULT_PORT := 27840
const MAX_PLAYERS := 16
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

func _ready() -> void:
	config = Simulation.DEFAULT_CONFIG.duplicate(true)
	multiplayer.connected_to_server.connect(_connected)
	multiplayer.connection_failed.connect(_failed)
	multiplayer.server_disconnected.connect(_server_lost)
	multiplayer.peer_disconnected.connect(_peer_left)
	multiplayer.peer_connected.connect(_peer_connected)

func host(port: int) -> Error:
	var peer := ENetMultiplayerPeer.new()
	var error := peer.create_server(port, 32, 3)
	if error != OK:
		return error
	multiplayer.multiplayer_peer = peer
	# All gameplay routes through server 1; no peer-to-peer RPC relay is needed.
	(multiplayer as SceneMultiplayer).server_relay = false
	is_server = true
	print("SERVER_READY version=%s protocol=%d UDP=%d" % [VERSION, PROTOCOL, port])
	return OK

func connect_room(address: String, port: int, player_name: String, code: String, create: bool) -> void:
	close_client()
	pending_join = {"name": player_name.strip_edges().left(20), "code": code.strip_edges().to_upper(), "create": create}
	if pending_join.name.is_empty():
		pending_join.name = "Amigo"
	var peer := ENetMultiplayerPeer.new()
	var error := peer.create_client(address.strip_edges(), port, 3)
	if error != OK:
		notice.emit("No se pudo conectar. Revisá dirección y puerto UDP.")
		return
	multiplayer.multiplayer_peer = peer
	connecting = true
	connect_age = 0.0
	notice.emit("Conectando con %s:%d…" % [address, port])

func close_client() -> void:
	if not is_server and multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
		multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	connecting = false
	latest.clear()
	private_latest.clear()
	last_received_tick = -1

func _connected() -> void:
	_request_join.rpc_id(1, VERSION, PROTOCOL, str(pending_join.get("name", "Amigo")), str(pending_join.get("code", "")), bool(pending_join.get("create", false)), token)

func _failed() -> void:
	connecting = false
	notice.emit("No se pudo llegar al servidor. Abrí Iniciar servidor y revisá dirección / UDP 27840.")
	disconnected.emit()

func _server_lost() -> void:
	connecting = false
	notice.emit("Se perdió el servidor. La ronda terminó sin ganador. Podés volver a conectarte.")
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
	if not is_server:
		return
	join_attempts.erase(id)
	menu_rates.erase(id)
	private_revisions.erase(id)
	if not players.has(id):
		return
	var departed_name: String = str(players[id].name)
	if peer_tokens.has(id):
		tokens[peer_tokens[id]] = {"name": players[id].name, "role": players[id].role}
		peer_tokens.erase(id)
	players.erase(id)
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
	if room_owner == id:
		room_owner = int(players.keys()[0]) if not players.is_empty() else 0
	if players.is_empty():
		room_code = ""
		config = Simulation.DEFAULT_CONFIG.duplicate(true)
		tokens.clear()
	_broadcast_lobby()

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
		_reject.rpc_id(sender, "Versión incompatible. Todos necesitan Dejame dormir %s (protocolo %d)." % [VERSION, PROTOCOL])
		return
	if sim != null:
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
	var chosen_role := "human" if players.is_empty() else "mosquito"
	if previous_token.length() == 32 and tokens.has(previous_token):
		chosen_role = str(tokens[previous_token].role)
		tokens.erase(previous_token)
	var new_token := Crypto.new().generate_random_bytes(16).hex_encode()
	peer_tokens[sender] = new_token
	players[sender] = {"name": clean_name, "role": chosen_role, "ready": false}
	_welcome.rpc_id(sender, sender, new_token)
	print("JOIN id=%d role=%s count=%d" % [sender, chosen_role, players.size()])
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
	connecting = false
	token = new_token
	accepted.emit(id)

@rpc("authority", "call_remote", "reliable", 0)
func _reject(message: String) -> void:
	connecting = false
	notice.emit(message)
	# Keep notice visible; next attempt replaces this transport.

@rpc("authority", "call_remote", "reliable", 0)
func _server_notice(message: String) -> void:
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
	if sim != null and str(sim.public_snapshot().phase) == "playing":
		return
	match verb:
		"role":
			if value is String and value in ["human", "mosquito"]:
				if str(players[sender].role) == value:
					return
				players[sender].role = value
				_set_unready()
		"ready":
			if value is bool:
				if bool(players[sender].ready) == value:
					return
				players[sender].ready = value
		"config":
			if sender == room_owner and value is Dictionary:
				config = sanitize_config(value)
				_set_unready()
		"start":
			if sender == room_owner:
				var reason := _start_reason()
				if reason.is_empty():
					server_tick += 1
					sim = Simulation.new()
					sim.start(players, config)
					private_revisions.clear()
					last_phase = ""
					print("ROUND_START mode=%s players=%d" % [config.mode, players.size()])
					_publish(true)
					return
				_server_notice.rpc_id(sender, reason)
		"rematch":
			if sender == room_owner:
				sim = null
				private_revisions.clear()
				_set_unready()
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
	var validator := Simulation.new()
	var reason: String = validator.validate_roster(players)
	if not reason.is_empty():
		return reason
	for id: int in players:
		if not bool(players[id].ready):
			return "Falta que todos pulsen Estoy listo."
	return ""

func _broadcast_lobby() -> void:
	if players.is_empty():
		return
	var reason := _start_reason()
	var data := {"code": room_code, "owner": room_owner, "players": players.duplicate(true), "config": config.duplicate(true), "can_start": reason.is_empty(), "start_reason": reason, "barrier_tick": server_tick}
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
	latest.clear()
	private_latest.clear()
	last_received_tick = int(data.get("barrier_tick", -1)) + 1
	lobby_updated.emit(data)

func send_input(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool) -> void:
	if _client_connected():
		_request_movement.rpc_id(1, seq, move, yaw, pitch, interact)

func send_action(seq: int, verb: String) -> void:
	if _client_connected():
		_action.rpc_id(1, seq, verb)

@rpc("any_peer", "call_remote", "unreliable_ordered", 1)
func _request_movement(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool) -> void:
	if is_server and sim != null:
		var sender := multiplayer.get_remote_sender_id()
		if players.has(sender):
			sim.submit_input(sender, seq, move, yaw, pitch, interact)

@rpc("any_peer", "call_remote", "reliable", 0)
func _action(seq: int, verb: String) -> void:
	if is_server and sim != null:
		var sender := multiplayer.get_remote_sender_id()
		if players.has(sender) and verb.length() < 24:
			sim.action(sender, seq, verb)

func _physics_process(dt: float) -> void:
	if connecting:
		connect_age += dt
		if connect_age > 10.0:
			close_client()
			notice.emit("El servidor no respondió en 10 segundos. Revisá que esté abierto y su dirección sea correcta.")
	if not is_server or sim == null:
		return
	sim.step(dt)
	server_tick += 1
	publish_accumulator += dt
	if publish_accumulator >= 0.05:
		publish_accumulator = 0.0
		_publish(false)

func _publish(force_reliable: bool) -> void:
	if sim == null:
		return
	var state: Dictionary = sim.public_snapshot()
	state["tick"] = server_tick
	# Repeated field names make compact dictionary snapshots highly compressible.
	# Compression avoids unreliable ENet fragmentation even in the 5v10 test.
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
			_receive_snapshot.rpc_id(id, packet)
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

func _accept_snapshot(data: Dictionary) -> void:
	var tick := int(data.get("tick", 0))
	if tick < last_received_tick:
		return
	last_received_tick = tick
	latest = data
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
