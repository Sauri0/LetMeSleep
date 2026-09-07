extends Node

const NetworkScript = preload("res://scripts/network.gd")
var network: Node
var options: Dictionary = {}
var local_server_pid := -1

func _ready() -> void:
	tree_exiting.connect(_cleanup_server)
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--"):
			var parts := argument.substr(2).split("=", true, 1)
			options[parts[0]] = parts[1] if parts.size() > 1 else "true"
	network = NetworkScript.new()
	network.name = "Network"
	add_child(network)
	var port := int(options.get("port", str(NetworkScript.DEFAULT_PORT)))
	if options.has("server"):
		if options.has("config"):
			var server_config := ConfigFile.new()
			var config_error := server_config.load(str(options.config))
			if config_error != OK:
				printerr("SERVER_ERROR No se pudo leer servidor.cfg: " + error_string(config_error))
				get_tree().quit(2)
				return
			port = int(server_config.get_value("server", "port", NetworkScript.DEFAULT_PORT))
		if port < 1024 or port > 65535:
			printerr("SERVER_ERROR Puerto fuera del rango 1024..65535.")
			get_tree().quit(2)
			return
		var error: Error = network.host(port)
		if error != OK:
			printerr("SERVER_ERROR No se pudo abrir UDP %d: %s" % [port, error_string(error)])
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
	get_tree().auto_accept_quit = false

func _start_local_server() -> void:
	var client := get_node("Client")
	if local_server_pid > 0 and OS.is_process_running(local_server_pid):
		client.ui.show_status("El servidor local ya está abierto. Creá sala en 127.0.0.1.")
		return
	var executable := OS.get_executable_path()
	var args := PackedStringArray(["--headless"])
	if OS.has_feature("editor"):
		args.append_array(["--path", ProjectSettings.globalize_path("res://")])
	args.append_array(["--", "--server", "--port=27840"])
	# Godot create_process does not open a console when open_console=false.
	local_server_pid = OS.create_process(executable, args, false)
	if local_server_pid < 0:
		client.ui.show_status("No se pudo iniciar el servidor. Usá Iniciar-servidor.cmd del paquete.")
	else:
		client.ui.show_status("Servidor local iniciado (UDP 27840). Creá sala en 127.0.0.1. Se cierra al salir de este juego.")

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		_cleanup_server()
		get_tree().quit()

func _cleanup_server() -> void:
	if local_server_pid > 0 and OS.is_process_running(local_server_pid):
		OS.kill(local_server_pid)
	local_server_pid = -1
