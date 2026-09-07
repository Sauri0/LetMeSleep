extends Node

signal lobby_updated(data: Dictionary)
signal snapshot_updated(data: Dictionary)
signal private_updated(data: Dictionary)
signal notice(message: String)
signal accepted(peer_id: int)
signal disconnected
signal waiting_updated(data: Dictionary)

const Simulation = preload("res://scripts/simulation.gd")
const LobbyRules = preload("res://scripts/lobby_rules.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const Map = preload("res://scripts/arena.gd")
const VERSION := "0.4.0"
const PROTOCOL := 4
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
var local_cosmetics: Dictionary = {}
var waiting_actors: Dictionary = {}
var waiting_inputs: Dictionary = {}

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
		_reject.rpc_id(sender, "Versión incompatible. Todos necesitan Let me sleep %s (protocolo %d)." % [VERSION, PROTOCOL])
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
	connecting = false
	token = new_token
	accepted.emit(id)
	lobby_action("cosmetics", local_cosmetics)

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
	if sim != null and verb != "rematch":
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
					server_tick += 1
					sim = Simulation.new()
					var assigned: Dictionary = LobbyRules.draw(players, config)
					sim.start(assigned, config)
					private_revisions.clear()
					last_phase = ""
					print("ROUND_START mode=%s players=%d" % [config.mode, players.size()])
					_publish(true)
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
	return LobbyRules.validate(players, config, true)

func _broadcast_lobby() -> void:
	if players.is_empty():
		return
	var reason := _start_reason()
	var data := {"code": room_code, "owner": room_owner, "players": players.duplicate(true), "config": config.duplicate(true), "can_start": reason.is_empty(), "start_reason": reason, "barrier_tick": server_tick, "actors": waiting_actors.duplicate(true)}
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
	latest.clear()
	private_latest.clear()
	last_received_tick = barrier + 1
	lobby_updated.emit(data)

func send_input(seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
	if _client_connected():
		_request_movement.rpc_id(1, seq, move, yaw, pitch, interact, sprint, crouch, jump)

func send_action(seq: int, verb: String) -> void:
	if _client_connected():
		_action.rpc_id(1, seq, verb)

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
		if players.has(sender) and verb.length() < 24:
			sim.action(sender, seq, verb)

func _physics_process(dt: float) -> void:
	if connecting:
		connect_age += dt
		if connect_age > 10.0:
			close_client()
			notice.emit("El servidor no respondió en 10 segundos. Revisá que esté abierto y su dirección sea correcta.")
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
			_receive_snapshot.rpc_id(id, packet)

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
