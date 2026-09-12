extends Node

const NetworkScript = preload("res://scripts/network.gd")
var network: Node
var options: Dictionary = {}
var local_server_pid := -1
var local_server_port := 0
var launch_generation := 0
var launch_pending := false
var launch_reply := ""
var parent_check_age := 0.0
var local_join_pending := false
var closing := false
var last_host_state: Dictionary = {}

func _ready() -> void:
	tree_exiting.connect(_cleanup_server)
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--"):
			var parts := argument.substr(2).split("=", true, 1)
			options[parts[0]] = parts[1] if parts.size() > 1 else "true"
	network = NetworkScript.new()
	network.name = "Network"
	add_child(network)
	if options.has("server"):
		printerr("Las salas se crean dentro del juego: Crear sala online.")
		get_tree().quit(2)
		return
	if options.has("bot"):
		var bot: Node = load("res://tests/network_bot.gd").new()
		bot.name = "Bot"
		bot.network = network
		bot.options = options
		add_child(bot)
		return
	var client: Node = load("res://scripts/client.gd").new()
	client.name = "Client"
	client.network = network
	client.options = options
	add_child(client)
	client.local_server_requested.connect(_start_local_server)
	client.local_server_cancel_requested.connect(_cancel_local_launch)
	client.local_server_close_requested.connect(_cleanup_server)
	network.accepted.connect(func(_id: int) -> void: local_join_pending = false)
	network.disconnected.connect(_on_client_disconnected)
	get_tree().auto_accept_quit = false
	if options.has("hosting-checks"):
		var checks: Node = load("res://tests/hosting_checks.gd").new()
		checks.app = self
		add_child(checks)
	if options.has("practice-checks"):
		var checks: Node = load("res://tests/practice_ui_checks.gd").new()
		checks.client = client
		checks.options = options
		add_child(checks)
	if options.has("gameplay-demo"):
		var demo: Node = load("res://tests/gameplay_demo.gd").new()
		demo.client = client
		demo.options = options
		add_child(demo)

func _write_server_reply(port: int, error: Error) -> void:
	if not options.has("ready-file") or not options.has("launch-token"):
		return
	var reply := FileAccess.open(str(options["ready-file"]), FileAccess.WRITE)
	if reply == null:
		printerr("SERVER_ERROR No se pudo escribir la confirmación de arranque.")
		get_tree().quit(2)
		return
	reply.store_string(JSON.stringify({"token": str(options["launch-token"]), "pid": OS.get_process_id(), "port": port, "error": int(error), "message": error_string(error)}))
	reply.close()

func _host_state(phase: String, code: String, message: String, port: int) -> void:
	last_host_state = {"attempt": launch_generation, "phase": phase, "code": code, "message": message, "address": "127.0.0.1", "port": port, "elapsed": 0.0, "can_retry": phase == "failed", "can_cancel": phase == "starting_server"}
	get_node("Client").ui.show_connection_state(last_host_state)

func _start_local_server(player_name: String, port: int) -> void:
	if launch_pending:
		return
	if port < 1024 or port > 65535:
		_host_state("failed", "invalid_endpoint", "Elegí un puerto entre 1024 y 65535.", port)
		return
	# Reuse only our own child whose bind was confirmed earlier.
	if local_server_pid > 0 and OS.is_process_running(local_server_pid) and local_server_port == port:
		local_join_pending = true
		network.connect_room("127.0.0.1", port, player_name, "", true)
		return
	_cleanup_server()
	launch_generation += 1
	var generation := launch_generation
	launch_pending = true
	var token := "%d-%d-%d" % [OS.get_process_id(), Time.get_ticks_usec(), randi()]
	launch_reply = OS.get_user_data_dir().path_join("server-ready-" + token + ".json")
	_host_state("starting_server", "", "Abriendo tu servidor…", port)
	var executable := OS.get_executable_path()
	var args := PackedStringArray(["--headless"])
	if OS.has_feature("editor"):
		args.append_array(["--path", ProjectSettings.globalize_path("res://")])
	args.append_array(["--", "--server", "--port=%d" % port, "--ready-file=" + launch_reply, "--launch-token=" + token, "--parent-pid=%d" % OS.get_process_id()])
	# Godot create_process does not open a console when open_console=false.
	local_server_pid = OS.create_process(executable, args, false)
	if local_server_pid < 0:
		launch_pending = false
		_host_state("failed", "host_process_failed", "No se pudo iniciar el proceso del servidor.", port)
		return
	var started := Time.get_ticks_msec()
	while generation == launch_generation and Time.get_ticks_msec() - started < 8000:
		if FileAccess.file_exists(launch_reply):
			var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(launch_reply))
			if data is Dictionary and str(data.get("token", "")) == token and int(data.get("pid", -1)) == local_server_pid and int(data.get("port", 0)) == port:
				_remove_launch_reply()
				launch_pending = false
				if int(data.get("error", FAILED)) != OK:
					_cleanup_server()
					_host_state("failed", "host_bind_failed", "No se pudo abrir UDP %d: %s" % [port, str(data.get("message", "Error desconocido"))], port)
					return
				local_server_port = port
				local_join_pending = true
				network.connect_room("127.0.0.1", port, player_name, "", true)
				return
		if not OS.is_process_running(local_server_pid):
			break
		await get_tree().create_timer(0.05).timeout
	if generation != launch_generation:
		return
	_cleanup_server()
	_host_state("failed", "host_process_failed", "El servidor no confirmó su arranque. Reintentá o revisá la guía.", port)

func _remove_launch_reply() -> void:
	if not launch_reply.is_empty() and FileAccess.file_exists(launch_reply):
		DirAccess.remove_absolute(launch_reply)
	launch_reply = ""

func _cancel_local_launch() -> void:
	if launch_pending or local_join_pending:
		_cleanup_server()

func _on_client_disconnected() -> void:
	if local_server_pid <= 0:
		return
	var generation := launch_generation
	# Let the room-closed notice reach the other clients before retiring our child.
	await get_tree().create_timer(0.3).timeout
	if generation == launch_generation:
		_cleanup_server()

func _process(dt: float) -> void:
	if not options.has("server") or not options.has("parent-pid"):
		return
	parent_check_age += dt
	if parent_check_age >= 1.0:
		parent_check_age = 0.0
		var parent_pid := int(options["parent-pid"])
		if parent_pid > 0 and not OS.is_process_running(parent_pid):
			get_tree().quit()

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		request_exit()

func request_exit(exit_code: int = 0) -> void:
	if closing:
		return
	closing = true
	var client := get_node_or_null("Client")
	if client != null:
		client.music.shutdown()
	network.request_close_room()
	await get_tree().create_timer(0.25).timeout
	# Guests and cancelled handshakes also own a lobby/SDK request to retire.
	network.close_client()
	# Keep the SDK tick and lobby node alive while leave/destroy callbacks drain.
	# Quit remains bounded when Internet disappears during application shutdown.
	if network.has_method("online_cleanup_pending"):
		var cleanup_deadline := Time.get_ticks_msec() + 25000
		while network.online_cleanup_pending() and Time.get_ticks_msec() < cleanup_deadline:
			await get_tree().create_timer(0.05).timeout
	if network.has_method("shutdown_online_backend"):
		if not network.shutdown_online_backend():
			push_warning("El servicio online no completó su cierre dentro del plazo.")
	_cleanup_server()
	get_tree().quit(exit_code)

func _cleanup_server() -> void:
	launch_generation += 1
	launch_pending = false
	local_join_pending = false
	if local_server_pid > 0 and OS.is_process_running(local_server_pid):
		OS.kill(local_server_pid)
	local_server_pid = -1
	local_server_port = 0
	_remove_launch_reply()
