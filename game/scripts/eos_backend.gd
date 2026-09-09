extends Node
## Optional EOSG adapter. No addon class is referenced at parse time.
## A single outstanding SDK request prevents uncorrelated global callbacks from
## completing a later attempt. The owner must keep this node alive while draining.
signal completed(ticket: int, operation: String, result: Dictionary)
signal auth_expiring
var busy := false
var initialized := false
var local_user_id := ""
var _eos: Variant
var _ieos: Object
var _platform: Node
var _lobbies: Node
var _auth: Node
var _runtime: Node
var _p2p: Node
const ADDON := "res://addons/epic-online-services-godot/"

func available() -> bool:
	if not Engine.has_singleton("IEOS") or not ClassDB.class_exists("EOSGMultiplayerPeer"):
		return false
	for singleton: String in ["HPlatform", "HLobbies", "HAuth", "EOSGRuntime", "HP2P"]:
		if get_node_or_null("/root/" + singleton) == null:
			return false
	return ResourceLoader.exists(ADDON + "eos.gd")

func request(ticket: int, operation: String, arguments: Dictionary) -> bool:
	if busy:
		return false
	busy = true
	_run.call_deferred(ticket, operation, arguments)
	return true

func _run(ticket: int, operation: String, arguments: Dictionary) -> void:
	var result: Dictionary = await _perform(operation, arguments)
	busy = false
	completed.emit(ticket, operation, result)

func _perform(operation: String, args: Dictionary) -> Dictionary:
	if operation == "initialize":
		if initialized:
			return {"ok": true}
		if not available():
			return {"ok": false, "error": "sdk_unavailable"}
		_eos = load(ADDON + "eos.gd")
		_ieos = Engine.get_singleton("IEOS")
		_platform = get_node("/root/HPlatform")
		_lobbies = get_node("/root/HLobbies")
		_auth = get_node("/root/HAuth")
		_runtime = get_node("/root/EOSGRuntime")
		_p2p = get_node("/root/HP2P")
		var logger: Variant = load(ADDON + "heos/hlog.gd")
		logger.log_level = logger.LogLevel.ERROR
		_platform.is_server = false
		_platform.flags = _eos.Platform.PlatformFlags.DisableOverlay | _eos.Platform.PlatformFlags.DisableSocialOverlay
		_platform.task_network_timeout_seconds = 20.0
		_lobbies.presence_enabled = false
		_lobbies.max_search_results = 1
		# HAuth's expiration handler otherwise starts a second, EAS-based login.
		var previous := Callable(_auth, "_on_connect_interface_auth_expiration")
		if _ieos.is_connected("connect_interface_auth_expiration", previous):
			_ieos.disconnect("connect_interface_auth_expiration", previous)
		if not _ieos.is_connected("connect_interface_auth_expiration", _expiration):
			_ieos.connect("connect_interface_auth_expiration", _expiration)
		var credentials: Variant = load(ADDON + "heos/hcredentials.gd").new()
		for field: String in ["product_id", "sandbox_id", "deployment_id", "client_id", "client_secret"]:
			credentials.set(field, args.config[field])
		credentials.product_name = "Let me sleep"
		credentials.product_version = str(args.get("version", "0.9.0"))
		initialized = await _platform.setup_eos_async(credentials)
		return {"ok": initialized, "error": "platform_failed"}
	if not initialized:
		return {"ok": false, "error": "platform_uninitialized"}
	match operation:
		"device":
			var options: Variant = _eos.Connect.CreateDeviceIdOptions.new()
			options.device_model = "Windows PC"
			_eos.Connect.ConnectInterface.create_device_id(options)
			var result: Dictionary = await Signal(_ieos, "connect_interface_create_device_id_callback")
			return {"ok": _eos.is_success(result) or result.result_code == _eos.Result.DuplicateNotAllowed, "error": "device_failed"}
		"login":
			var options: Variant = _eos.Connect.LoginOptions.new()
			options.credentials = _eos.Connect.Credentials.new()
			options.credentials.type = _eos.ExternalCredentialType.DeviceidAccessToken
			options.credentials.token = null
			options.user_login_info = _eos.Connect.UserLoginInfo.new()
			options.user_login_info.display_name = args.display_name
			_eos.Connect.ConnectInterface.login(options)
			var result: Dictionary = await Signal(_ieos, "connect_interface_login_callback")
			if result.result_code == _eos.Result.InvalidUser:
				return {"ok": false, "error": "invalid_user", "continuance_token": result.get("continuance_token")}
			if not _eos.is_success(result) or str(result.get("local_user_id", "")).is_empty():
				return {"ok": false, "error": "login_failed"}
			local_user_id = result.local_user_id
			_auth.product_user_id = local_user_id
			# The native mediator also requires this successful Login callback.
			_runtime.local_product_user_id = local_user_id
			return {"ok": true, "local_user_id": local_user_id}
		"create_user":
			var options: Variant = _eos.Connect.CreateUserOptions.new()
			options.continuance_token = args.continuance_token
			_eos.Connect.ConnectInterface.create_user(options)
			var result: Dictionary = await Signal(_ieos, "connect_interface_create_user_callback")
			return {"ok": _eos.is_success(result), "error": "create_user_failed"}
		"create":
			var options: Variant = _eos.Lobby.CreateLobbyOptions.new()
			options.max_lobby_members = 16
			options.disable_host_migration = true
			options.presence_enabled = false
			options.enable_rtc_room = false
			options.enable_join_by_id = false
			options.bucket_id = "LMS-P" + str(args.protocol)
			options.permission_level = _eos.Lobby.LobbyPermissionLevel.PublicAdvertised
			var lobby: Variant = await _lobbies.create_lobby_async(options)
			if lobby == null:
				return {"ok": false, "error": "create_failed"}
			return {"ok": true, "lobby": lobby}
		"search":
			var found: Variant = await _lobbies.search_by_lobby_id_async(args.lobby_id)
			if not found is Array or found.size() != 1:
				return {"ok": false, "error": "room_not_found"}
			return {"ok": true, "lobby": found[0]}
		"join":
			var lobby: Variant = await _lobbies.join_async(args.lobby)
			dispose_search(args.lobby)
			return {"ok": lobby != null, "error": "join_failed", "lobby": lobby}
		"cleanup":
			var ok: bool
			if args.is_host:
				ok = await args.lobby.destroy_async()
			else:
				ok = await args.lobby.leave_async()
			dispose_search(args.lobby)
			return {"ok": ok, "error": "cleanup_failed"}
	return {"ok": false, "error": "unsupported_operation"}

func make_peer(is_host: bool, socket_id: String, owner_id: String, force_relay := false) -> Dictionary:
	if not initialized or local_user_id.is_empty():
		return {"ok": false, "error": "not_authenticated"}
	var relay_mode: int = _eos.P2P.RelayControl.ForceRelays if force_relay else _eos.P2P.RelayControl.AllowRelays
	if not _eos.is_success(_p2p.set_relay_control(relay_mode)):
		return {"ok": false, "error": "relay_configuration_failed"}
	var peer: Variant = ClassDB.instantiate("EOSGMultiplayerPeer")
	peer.set_auto_accept_connection_requests(false)
	var result: int = peer.create_server(socket_id) if is_host else peer.create_client(socket_id, owner_id)
	if result != OK:
		peer.close()
		return {"ok": false, "error": "peer_creation_failed"}
	return {"ok": true, "peer": peer}

func _expiration(_data: Dictionary) -> void:
	auth_expiring.emit()

## Call only during application shutdown, after OnlineSession.close() has
## drained SDK callbacks and all game peers have closed their EOS sockets.
func shutdown() -> Dictionary:
	if busy:
		return {"ok": false, "error": "requests_pending"}
	if not initialized:
		return {"ok": true}
	if Engine.has_singleton("EOSGPacketPeerMediator"):
		var mediator := Engine.get_singleton("EOSGPacketPeerMediator")
		if not mediator.get_sockets().is_empty():
			return {"ok": false, "error": "peers_still_open"}
	if _ieos.is_connected("connect_interface_auth_expiration", _expiration):
		_ieos.disconnect("connect_interface_auth_expiration", _expiration)
	_runtime.set_process(false)
	_ieos.platform_interface_release()
	var code: int = _ieos.platform_interface_shutdown()
	initialized = false
	local_user_id = ""
	return {"ok": _eos.is_success(code), "error": "sdk_shutdown_failed" if not _eos.is_success(code) else "", "result_code": code}

## EOSG's HLobbyMember keeps a strong reference to its parent HLobby. Break
## that cycle once a search snapshot is unused or an acquired lobby was left.
## This releases local wrappers only; it never substitutes for server cleanup.
func dispose_search(lobby: Variant) -> void:
	if lobby == null:
		return
	# Upstream _disconnect_from_signals pairs one callback incorrectly. Remove
	# only this wrapper's own callbacks, without touching another room/session.
	if _ieos != null:
		for definition: Dictionary in _ieos.get_signal_list():
			for connection: Dictionary in _ieos.get_signal_connection_list(definition.name):
				if connection.callable.get_object() == lobby:
					_ieos.disconnect(definition.name, connection.callable)
	for member: Variant in lobby.members:
		member._lobby = null
	lobby.members.clear()
	lobby._lobby_details = null

