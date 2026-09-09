extends Node
## Room lifecycle only. transport_ready means a peer exists, NOT that the game
## handshake has accepted the player. Network owns that separate acceptance gate.
signal state_changed(state: String)
signal failed(code: String)
signal transport_ready(info: Dictionary)
signal route_observed(network_type: int)
signal member_departed(user_id: String)
const Invitation = preload("res://scripts/online_invitation.gd")
const Backend = preload("res://scripts/eos_backend.gd")
const STEP_TIMEOUT_MS := 25000
const ADMISSION_TIMEOUT_MS := 30000
var state := "idle"
## Diagnostics only: forces an actual EOS relay; never synthesizes route evidence.
var force_relay := false
var backend: Node
var clock: Callable = Time.get_ticks_msec
var _generation := 0
var _deadline := 0
var _operation := ""
var _name := ""
var _protocol := 0
var _host := false
var _invitation: Dictionary = {}
var _lobby: Variant
var _peer: Variant
var _local_id := ""
var _owner_id := ""
var _socket := ""
var _created_user := false
var _renewing := false
var _renew_pending := false
var _cleanup: Array[Dictionary] = []
var _cleanup_failed := false
var _admission_deadlines: Dictionary = {}
var _admitted: Dictionary = {}
var _rejected: Dictionary = {}
var _kick_queue: Array[String] = []
var _kick_in_flight := ""

func _ready() -> void:
	if backend == null:
		backend = Backend.new()
		add_child(backend)
	backend.completed.connect(_completed)
	backend.auth_expiring.connect(_auth_expiring)
	backend.member_departed.connect(_member_departed)

## Only inject a fake/alternate backend before adding this node to the tree.
func set_backend(value: Node) -> void:
	assert(not is_inside_tree())
	backend = value

func start_host(config: Dictionary, display_name: String, protocol: int, capability: String) -> bool:
	return _start(config, display_name, protocol, true, {"capability": capability})

## Invitation must come from OnlineInvitation.decode.
func start_join(config: Dictionary, display_name: String, invitation: Dictionary) -> bool:
	if not invitation.get("protocol") is int or invitation.get("protocol") != Invitation.PROTOCOL:
		failed.emit("invalid_invitation")
		return false
	return _start(config, display_name, invitation.protocol, false, invitation)

func _start(config: Dictionary, display_name: String, protocol: int, is_host: bool, invitation: Dictionary) -> bool:
	if backend == null or backend.busy or not _cleanup.is_empty() or _cleanup_failed or not state in ["idle", "cancelled", "error"]:
		failed.emit("attempt_still_draining")
		return false
	if not valid_configuration(config):
		_set_state("unconfigured")
		failed.emit("configuration_required")
		# Keep retries possible after owner adds configuration.
		_set_state("error")
		return false
	if not backend.available():
		_set_state("error")
		failed.emit("sdk_unavailable")
		return false
	if protocol != Invitation.PROTOCOL or not _hex_capability(str(invitation.get("capability", ""))):
		failed.emit("invalid_invitation")
		return false
	if not is_host and (str(invitation.get("lobby_id", "")).is_empty() or not Invitation.valid_lobby_id(str(invitation.get("lobby_id", "")))):
		failed.emit("invalid_invitation")
		return false
	_generation += 1
	_name = display_name.strip_edges().left(20)
	if _name.is_empty():
		_name = "Player"
	_protocol = protocol
	_host = is_host
	_invitation = invitation.duplicate(true)
	_created_user = false
	_renewing = false
	_renew_pending = false
	_request("initialize", {"config": config.duplicate(true)})
	return true

static func valid_configuration(config: Dictionary) -> bool:
	for key: String in ["product_id", "sandbox_id", "deployment_id", "client_id", "client_secret"]:
		var value: Variant = config.get(key)
		if not value is String or value.strip_edges().is_empty() or value.length() > 512:
			return false
		var normalized: String = value.to_lower()
		for marker: String in ["replace", "your_", "placeholder", "pending", "pendiente", "<", ">"]:
			if normalized.contains(marker):
				return false
	return true

static func _hex_capability(value: String) -> bool:
	if value.length() != 32:
		return false
	for character: String in value:
		if not character in "0123456789abcdef":
			return false
	return true

func _request(operation: String, arguments: Dictionary = {}) -> void:
	var ticket := _generation
	_operation = operation
	_deadline = int(clock.call()) + STEP_TIMEOUT_MS
	_set_state("renewing" if _renewing else operation)
	# UI listeners may synchronously cancel when presenting the new state.
	if ticket != _generation:
		return
	if not backend.request(ticket, operation, arguments):
		_fail("backend_busy")

func _completed(ticket: int, operation: String, result: Dictionary) -> void:
	if operation == "cleanup":
		if not result.get("ok", false):
			_cleanup_failed = true
			failed.emit("room_cleanup_failed_restart_required")
		_drain_cleanup()
		return
	if ticket != _generation:
		# A lobby created/joined after cancel still belongs to this attempt.
		if operation in ["create", "join"] and result.get("lobby") != null:
			_queue_cleanup(result.lobby, operation == "create")
		elif operation == "search" and result.get("lobby") != null:
			backend.dispose_search(result.lobby)
		_drain_cleanup()
		return
	_deadline = 0
	if operation == "kick":
		_kick_in_flight = ""
		if not result.get("ok", false):
			_fail("member_kick_failed")
			return
		if _renew_pending:
			_auth_expiring()
		_drain_kicks()
		return
	if operation == "login" and result.get("error") == "invalid_user" and not _renewing and not _created_user and result.get("continuance_token") != null:
		_created_user = true
		_request("create_user", {"continuance_token": result.continuance_token})
		return
	if not result.get("ok", false):
		_fail(str(result.get("error", "online_failed")))
		return
	match operation:
		"initialize": _request("device")
		"device", "create_user":
			# CreateUser does not initialize the native peer's PUID. Login again.
			_request("login", {"display_name": _name})
		"login":
			var identity: String = str(result.get("local_user_id", ""))
			if identity.is_empty() or (_renewing and identity != _local_id):
				_fail("identity_changed")
				return
			_local_id = identity
			if _renewing:
				_renewing = false
				_set_state("transport_ready")
			elif _host:
				_request("create", {"protocol": _protocol})
			else:
				_request("search", {"lobby_id": _invitation.lobby_id})
		"search":
			var found: Variant = result.get("lobby")
			if not _valid_lobby(found, false):
				backend.dispose_search(found)
				_fail("room_protocol_or_owner_mismatch")
				return
			_owner_id = found.owner_product_user_id
			_request("join", {"lobby": found})
		"create", "join":
			_lobby = result.get("lobby")
			if not _valid_lobby(_lobby, _host):
				_fail("room_protocol_or_owner_mismatch")
				return
			if not _host and _lobby.owner_product_user_id != _owner_id:
				_fail("room_owner_changed")
				return
			_owner_id = _lobby.owner_product_user_id
			_socket = Invitation.socket_id(_lobby.lobby_id)
			if not _host and _socket != Invitation.socket_id(_invitation.lobby_id):
				_fail("room_socket_mismatch")
				return
			_open_peer()
	if _renew_pending and state == "transport_ready":
		_auth_expiring()

func _valid_lobby(lobby: Variant, is_host: bool) -> bool:
	if lobby == null or str(lobby.lobby_id).is_empty() or str(lobby.owner_product_user_id).is_empty():
		return false
	if lobby.bucket_id != "LMS-P" + str(_protocol):
		return false
	if is_host:
		return lobby.owner_product_user_id == _local_id
	return lobby.lobby_id == _invitation.lobby_id and lobby.owner_product_user_id != _local_id

func _open_peer() -> void:
	var opened: Dictionary = backend.make_peer(_host, _socket, _owner_id, force_relay)
	if not opened.get("ok", false):
		_fail(str(opened.get("error", "peer_creation_failed")))
		return
	_peer = opened.peer
	_peer.incoming_connection_request.connect(_incoming)
	_peer.peer_connection_established.connect(_route)
	_lobby.lobby_owner_changed.connect(_owner_changed)
	_lobby.kicked_from_lobby.connect(_kicked)
	_lobby.lobby_updated.connect(_refresh_members)
	_refresh_members()
	var ticket := _generation
	var info := {"peer": _peer, "is_host": _host, "lobby_id": _lobby.lobby_id, "owner_id": _owner_id, "socket_id": _socket, "capability": _invitation.capability, "protocol": _protocol}
	_set_state("transport_ready")
	if ticket == _generation:
		transport_ready.emit(info)

## Membership is necessary but NOT sufficient. Network must also verify capability,
## game protocol, sender's peer/PUID correspondence, and authoritative room limits.
func is_lobby_member(user_id: String) -> bool:
	return _lobby != null and not user_id.is_empty() and _lobby.get_member_by_product_user_id(user_id) != null

## Only Network calls this, after validating the invitation capability and the
## entire game handshake. A P2P connection by itself does not admit a member.
func mark_peer_admitted(user_id: String) -> bool:
	if not _host or _lobby == null or _lobby.owner_product_user_id != _local_id or not state in ["transport_ready", "renewing"]:
		return false
	if user_id == _local_id:
		return true
	_refresh_members()
	if not is_lobby_member(user_id) or _rejected.has(user_id) or _kick_in_flight == user_id:
		return false
	if _admitted.has(user_id):
		return true
	if not _admission_deadlines.has(user_id) or int(clock.call()) >= int(_admission_deadlines[user_id]):
		reject_peer(user_id)
		return false
	_admitted[user_id] = true
	_admission_deadlines.erase(user_id)
	return true

## Network can send its reliable rejection first, then call this after its
## delivery grace period. Kicks serialize with authentication and cleanup.
func reject_peer(user_id: String) -> bool:
	if not _host or _lobby == null or _lobby.owner_product_user_id != _local_id or user_id == _local_id or user_id.is_empty():
		return false
	if not is_lobby_member(user_id):
		return false
	_admitted.erase(user_id)
	_admission_deadlines.erase(user_id)
	if not _rejected.has(user_id):
		_rejected[user_id] = true
		_kick_queue.append(user_id)
	_drain_kicks()
	return true

func _refresh_members() -> void:
	if not _host or _lobby == null:
		return
	var present: Dictionary = {}
	for member: Variant in _lobby.members:
		var user_id: String = member.product_user_id
		if user_id.is_empty() or user_id == _local_id:
			continue
		present[user_id] = true
		if not _admission_deadlines.has(user_id) and not _admitted.has(user_id) and not _rejected.has(user_id):
			_admission_deadlines[user_id] = int(clock.call()) + ADMISSION_TIMEOUT_MS
	for tracked: Dictionary in [_admission_deadlines, _admitted, _rejected]:
		for user_id: String in tracked.keys():
			if not present.has(user_id):
				tracked.erase(user_id)
	for user_id: String in _kick_queue.duplicate():
		if not present.has(user_id):
			_kick_queue.erase(user_id)

func _member_departed(lobby_id: String, user_id: String) -> void:
	if _lobby == null or _lobby.lobby_id != lobby_id:
		return
	_admission_deadlines.erase(user_id)
	_admitted.erase(user_id)
	_rejected.erase(user_id)
	_kick_queue.erase(user_id)
	member_departed.emit(user_id)

func poll_admission() -> void:
	if not _host or not state in ["transport_ready", "renewing"] or _lobby == null:
		return
	_refresh_members()
	for user_id: String in _admission_deadlines.keys():
		if int(clock.call()) >= int(_admission_deadlines[user_id]):
			reject_peer(user_id)
	_drain_kicks()

func _drain_kicks() -> void:
	if not _host or _lobby == null or backend.busy or _kick_queue.is_empty() or not state in ["transport_ready", "renewing"]:
		return
	if _lobby.owner_product_user_id != _local_id:
		_fail("room_owner_changed")
		return
	var user_id: String = _kick_queue.pop_front()
	if not is_lobby_member(user_id):
		_drain_kicks()
		return
	_kick_in_flight = user_id
	_operation = "kick"
	_deadline = int(clock.call()) + STEP_TIMEOUT_MS
	if not backend.request(_generation, "kick", {"lobby": _lobby, "user_id": user_id}):
		_fail("backend_busy")

func _incoming(data: Dictionary) -> void:
	if _peer == null:
		return
	var user_id: String = str(data.get("remote_user_id", ""))
	if _host and state in ["transport_ready", "renewing"] and data.get("socket", "") == _socket and user_id != _local_id and is_lobby_member(user_id) and not _rejected.has(user_id):
		_peer.accept_connection_request(user_id)
	else:
		_peer.deny_connection_request(user_id)

func _route(data: Dictionary) -> void:
	if state in ["transport_ready", "renewing"] and _peer != null and data.get("socket", "") == _socket and is_lobby_member(str(data.get("remote_user_id", ""))):
		route_observed.emit(int(data.get("network_type", 0)))

func _owner_changed() -> void:
	if _lobby != null and _lobby.owner_product_user_id != _owner_id:
		_fail("room_owner_changed")

func _kicked() -> void:
	_fail("room_closed")

func _auth_expiring() -> void:
	_renew_pending = true
	if state != "transport_ready" or backend.busy:
		return
	_renew_pending = false
	_renewing = true
	_request("login", {"display_name": _name})

func _process(_delta: float) -> void:
	poll_timeout()
	poll_admission()

func poll_timeout() -> void:
	if _deadline > 0 and int(clock.call()) >= _deadline:
		_fail("online_timeout_" + _operation)

func cancel() -> void:
	_invalidate()
	_set_state("cancelled")

func close() -> void:
	_invalidate()
	_set_state("idle")

func _fail(code: String) -> void:
	_invalidate()
	_set_state("error")
	failed.emit(code)

func _invalidate() -> void:
	_generation += 1
	_deadline = 0
	_renew_pending = false
	_renewing = false
	_invitation.clear()
	_admission_deadlines.clear()
	_admitted.clear()
	_rejected.clear()
	_kick_queue.clear()
	_kick_in_flight = ""
	if _peer != null:
		var old_peer: Variant = _peer
		_peer = null
		old_peer.close()
	if _lobby != null:
		var old: Variant = _lobby
		_lobby = null
		if old.lobby_owner_changed.is_connected(_owner_changed):
			old.lobby_owner_changed.disconnect(_owner_changed)
		if old.kicked_from_lobby.is_connected(_kicked):
			old.kicked_from_lobby.disconnect(_kicked)
		if old.lobby_updated.is_connected(_refresh_members):
			old.lobby_updated.disconnect(_refresh_members)
		_queue_cleanup(old, _host)
	_drain_cleanup()

func _queue_cleanup(lobby: Variant, is_host: bool) -> void:
	_cleanup.append({"lobby": lobby, "is_host": is_host})

func _drain_cleanup() -> void:
	if not backend.busy and not _cleanup.is_empty():
		var item: Dictionary = _cleanup.pop_front()
		backend.request(_generation, "cleanup", item)

func _set_state(value: String) -> void:
	state = value
	state_changed.emit(state)


