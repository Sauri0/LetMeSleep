extends SceneTree
## Optional live integration check. Requires the local Peer2Peer configuration.
## Runs the real menu, rendered lobby and Main shutdown; no second player/WAN.
const Main = preload("res://scripts/main.gd")
const Prefs = preload("res://scripts/preferences.gd")
const Configuration = preload("res://scripts/online_config.gd")
var failures: Array[String] = []
var checks := 0
var app: Node
var original_preferences := PackedByteArray()
var preferences_existed := false

func _initialize() -> void:
	run.call_deferred()

func check(value: bool, label: String) -> void:
	checks += 1
	print("ONLINE_MAIN %s %s" % ["PASS" if value else "FAIL", label])
	if not value:
		failures.append(label)

func run() -> void:
	check(not Configuration.load_values().is_empty(), "packaged configuration loads without a command-line override")
	if not failures.is_empty():
		quit(1)
		return
	preferences_existed = FileAccess.file_exists(Prefs.FILE_PATH)
	if preferences_existed:
		original_preferences = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)
	app = Main.new()
	app.name = "Main"
	root.add_child(app)
	await process_frame
	var client: Node = app.get_node("Client")
	client.ui._open_connection(true)
	client.ui._name_edit.text = "Menu integration check"
	client.ui._connect_submit.pressed.emit()
	var deadline := Time.get_ticks_msec() + 45000
	while not client.waiting and app.network._connection_phase != "failed" and Time.get_ticks_msec() < deadline:
		await process_frame
	check(client.waiting, "Create room button enters the rendered waiting room through EOS")
	check(app.local_server_pid == -1 and app.network._local_host, "hosting stays inside the game process")
	check(app.network.players.has(1) and app.network.room_owner == 1, "host is the waiting room administrator")
	check(app.network.invitation_text.begins_with("LMS1-"), "room produces an online invitation")
	await create_timer(1.0).timeout
	# Restore the user's name/settings before Main exits the process.
	if preferences_existed:
		var restored := FileAccess.open(Prefs.FILE_PATH, FileAccess.WRITE)
		restored.store_buffer(original_preferences)
		restored.close()
		check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original_preferences, "user preferences restored byte for byte")
	else:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	await app.request_exit(0 if failures.is_empty() else 1)
	check(not app.network.online_session.backend.initialized, "Main released EOS after draining the room")
	print("ONLINE_MAIN_RESULT checks=%d failures=%d" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
