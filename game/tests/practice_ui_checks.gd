extends Node
## Runs through the actual menu, client, local authority, bots, world and camera.
## Launch with --no-microphone; optional --check-role/--check-mode split native runs.
const Maps = preload("res://scripts/map_catalog.gd")
const Prefs = preload("res://scripts/preferences.gd")
var client: Node
var options: Dictionary = {}
var checks := 0
var failures := 0
var observations: Array[Dictionary] = []
var failed_checks: Array[String] = []
var preferences_before := PackedByteArray()
var preferences_existed := false

func _ready() -> void:
	_run.call_deferred()
func check(value: bool, label: String) -> void:
	checks += 1
	print("PRACTICE_UI %s %s" % ["PASS" if value else "FAIL",label])
	if not value:
		failures += 1
		failed_checks.append(label)

func _map_identity(label: String) -> Dictionary:
	var map_id: String = str(client.practice.sim.config.map_id)
	var data: Dictionary = Maps.get_map(map_id)
	check(map_id != "house" and data.has("fingerprint"),label+" uses a generated house")
	check(client.world.current_map == map_id and str(client.state.get("config",{}).get("map_id","")) == map_id,label+" authority, snapshot and rendered map agree")
	check(not str(data.get("fingerprint","")).is_empty() and str(client.world.map_data.get("fingerprint","")) == str(data.get("fingerprint","")),label+" rendered fingerprint agrees with generated authority")
	return {"map_id":map_id,"fingerprint":data.get("fingerprint","")}
func _settle(seconds: float = 0.1) -> void:
	await get_tree().create_timer(seconds).timeout
func _action(action: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	Input.parse_input_event(event)
func _escape() -> void:
	var event := InputEventKey.new()
	event.physical_keycode = KEY_ESCAPE
	event.keycode = KEY_ESCAPE
	event.pressed = true
	Input.parse_input_event(event)
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
func _capture(name: String) -> void:
	if not options.has("check-output") or DisplayServer.get_name() == "headless":
		return
	await RenderingServer.frame_post_draw
	var folder := str(options["check-output"])
	DirAccess.make_dir_recursive_absolute(folder)
	var error := get_viewport().get_texture().get_image().save_png(folder.path_join(name+".png"))
	check(error == OK,"native capture " + name)
func _run() -> void:
	preferences_existed = FileAccess.file_exists(Prefs.FILE_PATH)
	preferences_before = FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if preferences_existed else PackedByteArray()
	check(not client.voice.capture.hardware_allowed,"automated run explicitly disables microphone and device enumeration")
	if client.voice.capture.hardware_allowed:
		await _finish()
		return
	var roles: Array[String] = ["human","mosquito"]
	var modes: Array[String] = ["blood","survival","sleep"]
	if options.has("check-role"):
		check(str(options["check-role"]) in roles,"requested role belongs to practice matrix")
		roles.assign([str(options["check-role"])])
	if options.has("check-mode"):
		check(str(options["check-mode"]) in modes,"requested mode belongs to practice matrix")
		modes.assign([str(options["check-mode"])])
	if failures > 0:
		await _finish()
		return
	await _settle()
	check(client.world.current_map == "lobby" and not client.practice_active,"initial menu has distinct lobby map")
	for role: String in roles:
		for mode: String in modes:
			client.ui._open_practice()
			client.ui._practice_role_buttons[role].pressed.emit()
			client.ui._practice_mode_buttons[mode].pressed.emit()
			client.ui._practice_start_button.pressed.emit()
			await _settle(0.25)
			check(client.practice_active and client.playing and client.role == role,"menu starts chosen POV " + role + "/" + mode)
			if client.practice.sim == null or not client.playing:
				await _finish()
				return
			check(client.practice.sim.config.mode == mode,"selected mode reaches authority")
			var first_map := _map_identity(role+"/"+mode)
			check(client.practice.brains.size() >= 1 and client.ui._practice,"bots and practice identity active")
			var me: Dictionary = client.practice.sim.actors[1]
			if mode == "blood":
				var start: Vector3 = me.p
				_action("move_forward",true)
				_action("sprint",true)
				await _settle(0.28)
				_action("move_forward",false)
				_action("sprint",false)
				check(Vector3(me.p).distance_to(start)>0.35,"actual movement reaches authority " + role)
				if role == "human":
					await _capture("humano-correr")
					var floor_y: float = me.p.y
					_action("jump",true)
					await _settle(0.22)
					check(float(me.p.y)>floor_y+0.15 and not bool(me.grounded),"jump rises above actual starting floor")
					await _capture("humano-saltar")
					_action("jump",false)
					await _settle(0.9)
					var eye_height: float = client.camera.global_position.y - float(me.p.y)
					_action("crouch",true)
					await _settle(0.3)
					check(float(me.crouch_amount)>0.7 and client.camera.global_position.y-float(me.p.y)<eye_height-0.3,"crouch updates body and first-person eye")
					await _capture("humano-agacharse")
					_action("crouch",false)
					_action("attack",true)
					_action("attack",false)
					await _settle(0.08)
					check(float(me.swing)>0,"first-person attack runs authoritative action")
					await _capture("humano-palmada")
				else:
					var flight_start: Vector3 = me.p
					var mouse := InputEventMouseMotion.new()
					# Exercise real input without assuming saved sensitivity/invert-Y.
					mouse.relative = Vector2(0,-.45/(Prefs.mosquito_sensitivity*(-1.0 if Prefs.invert_y else 1.0)))
					Input.parse_input_event(mouse)
					_action("move_forward",true)
					await _settle(0.4)
					_action("move_forward",false)
					check(float(me.p.y)>flight_start.y+0.2,"W follows camera pitch without height keys")
					await _settle(0.25)
					check(Vector3(me.velocity).length()<0.1,"release forward brakes pointed flight")
					await _capture("mosquito-practica")
			_escape()
			await _settle()
			check(client.ui.is_menu_open(),"Escape opens practice menu")
			var start: Vector3 = me.p
			var clock_before: float = client.practice.sim.elapsed
			_action("move_right",true)
			_action("attack",true)
			_action("attack",false)
			await _settle(0.2)
			_action("move_right",false)
			check(Vector2(me.p.x-start.x,me.p.z-start.z).length()<0.05,"menu blocks movement " + role + "/" + mode)
			check(client.practice.sim.elapsed>clock_before,"menu leaves local authority running")
			_escape()
			await _settle()
			check(not client.ui.is_menu_open() or client.ui._screen == "results","Escape restores gameplay or completed round shows results")
			# Inject only the end-of-round boundary; rule outcomes are exercised by
			# practice_test. This test verifies UI reset and navigation wiring.
			client.practice.sim._finish("human","Resultado de prueba de navegación")
			client.practice._publish()
			await _settle()
			check(client.ui._screen == "results","practice result screen")
			client.ui._rematch_button.pressed.emit()
			await _settle()
			check(client.playing and client.practice.sim.elapsed<0.4 and client.role == role,"repeat resets chosen practice")
			var rematch_map := _map_identity("rematch "+role+"/"+mode)
			check(client.practice.sim.config.mode == mode,"rematch keeps selected mode")
			_escape()
			await _settle()
			client.ui._pause_leave.pressed.emit()
			await _settle()
			check(not client.practice_active and not client.playing and client.ui._screen == "home" and client.world.current_map == "lobby","leave returns to menu/lobby and stops bots")
			check(not client.practice.active and client.practice.sim == null and client.practice.brains.is_empty(),"leave releases authority and bot session")
			observations.append({"role":role,"mode":mode,"first_map":first_map,"rematch_map":rematch_map,"finished":true})
	check(observations.size() == roles.size()*modes.size(),"all requested role/mode cases completed")
	await _finish()

func _finish() -> void:
	for action: String in ["move_forward","move_right","sprint","jump","crouch","attack"]:
		_action(action,false)
	if client.practice_active:
		await client._leave()
	client.world.clear_actors()
	await _settle()
	var preferences_unchanged := FileAccess.file_exists(Prefs.FILE_PATH) == preferences_existed
	if preferences_existed:
		preferences_unchanged = preferences_unchanged and FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == preferences_before
	check(preferences_unchanged,"practice preserves preferences byte for byte")
	if not preferences_unchanged:
		if preferences_existed:
			var restore := FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE)
			if restore != null:
				restore.store_buffer(preferences_before)
				restore.close()
		else:
			DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	var report_file: FileAccess
	if options.has("check-report"):
		report_file = FileAccess.open(str(options["check-report"]),FileAccess.WRITE)
		check(report_file != null,"practice report is writable")
	var report := {"checks":checks,"failures":failures,"failed_checks":failed_checks,"cases":observations,"native":DisplayServer.get_name()!="headless","preferences_unchanged":preferences_unchanged,"microphone_allowed":client.voice.capture.hardware_allowed,"scope":"Real menu/client/practice/bots/camera; end-of-round result is injected solely for navigation. No WAN or packaged-build claim unless run against that executable."}
	if report_file != null:
		report_file.store_string(JSON.stringify(report,"\t"))
		report_file.close()
	print("PRACTICE_UI_RESULT " + JSON.stringify(report))
	get_tree().quit.call_deferred(failures)
