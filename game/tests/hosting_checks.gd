extends Node
## Real UI request -> hidden child bind acknowledgement -> ENet room.
const Prefs = preload("res://scripts/preferences.gd")
var app: Node
var checks := 0
var failures := 0
var events: Array = []
var original := PackedByteArray()
var existed := false

func _ready() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	events.append({"ok":ok,"message":message})
	print("HOSTING05 %s %s" % ["PASS" if ok else "FAIL",message])

func wait_until(predicate: Callable, seconds := 6.0) -> bool:
	var started := Time.get_ticks_msec()
	while Time.get_ticks_msec()-started < seconds*1000:
		if predicate.call(): return true
		await get_tree().create_timer(0.05).timeout
	return predicate.call()

func _run() -> void:
	var client: Node = app.get_node("Client")
	var ui: CanvasLayer = client.ui
	existed = FileAccess.file_exists(Prefs.FILE_PATH)
	original = FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existed else PackedByteArray()
	var port := 28571
	var reservation := ENetMultiplayerPeer.new()
	while reservation.create_server(port,2) != OK and port < 28600:
		port += 1
	reservation.close()
	Prefs.server_address = "203.0.113.77"
	Prefs.server_port = port+7
	ui._open_connection(true)
	ui._name_edit.text = "Anfitrión de prueba"
	ui._host_port_edit.value = port
	ui._connect_submit.pressed.emit()
	var first_pid: int = app.local_server_pid
	ui._connect_submit.pressed.emit()
	check(first_pid > 0 and app.local_server_pid == first_pid,"Repeated create uses one owned child")
	check(await wait_until(func() -> bool: return client.waiting),"Create button enters room automatically after real server bind")
	check(app.local_server_port == port and app.network._last_request.address == "127.0.0.1" and int(app.network._last_request.port) == port,"Custom listening port and loopback ignore stale friend endpoint")
	check(app.network.room_code.length() == 6 and client.local_id == app.network.room_owner,"Accepted local player owns room with actual code")
	check(app.launch_reply.is_empty(),"Launch acknowledgement removed after nonce/PID/port validation")
	client._leave()
	check(await wait_until(func() -> bool: return app.local_server_pid == -1 and not OS.is_process_running(first_pid)),"Leaving closes owned server without orphan")
	# Reserve the same UDP port in this test process; the child must fail bind.
	check(reservation.create_server(port,2) == OK,"Test owns occupied-port fixture")
	ui._open_connection(true)
	ui._name_edit.text = "Anfitrión de prueba"
	ui._host_port_edit.value = port
	ui._connect_submit.pressed.emit()
	check(await wait_until(func() -> bool: return app.last_host_state.get("code","") == "host_bind_failed"),"Bind failure is shown instead of false server-ready")
	check(not client.waiting and app.local_server_pid == -1 and not app.network.connecting,"Failed child cannot join the unrelated listener")
	reservation.close()
	ui._connection_retry.pressed.emit()
	check(await wait_until(func() -> bool: return client.waiting),"Retry after host failure starts own server instead of old remote request")
	client._leave()
	await wait_until(func() -> bool: return app.local_server_pid == -1)
	ui._open_connection(true)
	ui._name_edit.text = "Anfitrión de prueba"
	ui._connect_submit.pressed.emit()
	var cancelled_pid: int = app.local_server_pid
	ui.cancel_connection_requested.emit()
	check(await wait_until(func() -> bool: return app.local_server_pid == -1 and not OS.is_process_running(cancelled_pid)),"Cancel during startup terminates only the owned child")
	check(not app.launch_pending and not app.network.connecting,"Cancellation leaves no pending connection")
	await get_tree().create_timer(0.3).timeout
	if existed:
		var restored := FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE)
		restored.store_buffer(original)
		restored.close()
		check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original,"User preferences restored byte for byte")
	else:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
		check(not FileAccess.file_exists(Prefs.FILE_PATH),"Originally absent preferences remain absent")
	var report := {"checks":checks,"failures":failures,"events":events,"port":port}
	if app.options.has("report"):
		var file := FileAccess.open(str(app.options.report),FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
		file.close()
	print("HOSTING05_RESULT checks=%d failures=%d" % [checks,failures])
	if failures > 0:
		get_tree().quit.call_deferred(1)
	else:
		# Exercise the same graceful close requested by the game's quit button.
		get_tree().root.propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST)
