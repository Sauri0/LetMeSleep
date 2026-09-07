extends Node
## Runs through the actual menu, client, local authority, bots, world and camera.
var client: Node
var options: Dictionary = {}
var checks := 0
var failures := 0
var observations: Array[Dictionary] = []

func _ready() -> void:
	_run.call_deferred()
func check(value: bool, label: String) -> void:
	checks += 1
	print("PRACTICE_UI %s %s" % ["PASS" if value else "FAIL",label])
	if not value:
		failures += 1
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
	await _settle()
	check(client.world.current_map == "lobby" and not client.practice_active,"initial menu has distinct lobby map")
	for role: String in ["human","mosquito"]:
		for mode: String in ["blood","survival","sleep"]:
			client.ui._open_practice()
			client.ui._practice_role_buttons[role].pressed.emit()
			client.ui._practice_mode_buttons[mode].pressed.emit()
			client.ui._practice_start_button.pressed.emit()
			await _settle(0.25)
			check(client.practice_active and client.playing and client.role == role,"menu starts chosen POV " + role + "/" + mode)
			check(client.practice.sim.config.mode == mode and client.world.current_map == "house","selected mode loads playable house")
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
					_action("jump",true)
					await _settle(0.22)
					check(float(me.p.y)>0.15 and not bool(me.grounded),"jump and camera rise")
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
					_action("ascend",true)
					await _settle(0.25)
					_action("ascend",false)
					check(float(me.p.y)>1.4,"mosquito flight controls remain available")
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
			_escape()
			await _settle()
			client.ui._pause_leave.pressed.emit()
			await _settle()
			check(not client.practice_active and not client.playing and client.ui._screen == "home" and client.world.current_map == "lobby","leave returns to menu/lobby and stops bots")
			observations.append({"role":role,"mode":mode,"finished":true})
	client.world.clear_actors()
	await _settle()
	var report := {"checks":checks,"failures":failures,"cases":observations,"native":DisplayServer.get_name()!="headless"}
	if options.has("check-report"):
		var file := FileAccess.open(str(options["check-report"]),FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
	print("PRACTICE_UI_RESULT " + JSON.stringify(report))
	get_tree().quit(failures)
