extends SceneTree
## Real client, physical remappable key, Practice authority and authored door mesh.
const Prefs = preload("res://scripts/preferences.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Doors = preload("res://scripts/door_catalog.gd")

var app: Node
var client: Node
var checks := 0
var failures := 0
var folder := ""
var door_id := ""
var pref_bytes := PackedByteArray()
var pref_exists := false

func _select_door(sim_doors: Dictionary, map_id: String) -> String:
	if sim_doors.is_empty():
		return ""
	var actor: Vector3 = client.practice.sim.actors[1].p if client.practice.sim.actors.has(1) else Vector3.ZERO
	var best_distance := INF
	var picked := str(sim_doors.keys()[0])
	for id: String in sim_doors:
		var candidate := _door_handle_position(id)
		var distance := actor.distance_to(candidate)
		if distance < best_distance:
			best_distance = distance
			picked = str(id)
	if Doors.get_doors(map_id).has(picked):
		return picked
	for id: String in Doors.get_doors(map_id):
		if sim_doors.has(id):
			return id
	return picked

func _door_view(id: String) -> Dictionary:
	if not is_instance_valid(client) or not is_instance_valid(client.world) or not is_instance_valid(client.world.door_views):
		return {}
	var views: Dictionary = client.world.door_views.views
	if not views.has(id):
		return {}
	return Dictionary(views.get(id, {}))

func _door_handle_position(id: String) -> Vector3:
	var map_id: String = str(client.practice.sim.config.get("map_id", "house"))
	var definition: Dictionary = Doors.get_doors(map_id).get(id, {})
	if not definition.is_empty():
		var angle: float = float(client.practice.sim.doors.get(id, {}).get("angle", Doors.OPEN_ANGLE))
		return Doors.handle_point(definition, angle)
	var view := _door_view(id)
	if view.has("pivot") and is_instance_valid(view.pivot):
		return view.pivot.global_position
	if view.has("body") and is_instance_valid(view.body):
		return view.body.global_position
	return Vector3.ZERO

func _door_pivot_position(id: String) -> Vector3:
	var view := _door_view(id)
	if view.has("pivot") and is_instance_valid(view.pivot):
		return view.pivot.global_position
	return _door_handle_position(id)

func _door_view_angle(id: String) -> float:
	var view := _door_view(id)
	if view.is_empty():
		var state: Dictionary = client.practice.sim.doors.get(id, {})
		return float(state.get("angle", Doors.OPEN_ANGLE))
	return float(view.get("angle", Doors.OPEN_ANGLE))

func _approach_point(id: String, radius: float) -> Vector3:
	var map_id: String = str(client.practice.sim.config.get("map_id", "house"))
	var definition: Dictionary = Doors.get_doors(map_id).get(id, {})
	var target := _door_handle_position(id)
	var from: Vector3 = definition.get("hinge", target)
	var plan: Vector3 = target - from
	plan.y = 0.0
	if plan.length_squared() < 0.0001:
		plan = Vector3.BACK
	plan = plan.normalized()
	var result: Vector3 = target + plan * radius
	result.y = 0.0
	return result

func _select_interactive_door(sim_doors: Dictionary) -> String:
	for id: String in sim_doors:
		var view: Dictionary = _door_view(id)
		var pivot: Node3D = view.get("pivot", null)
		if not is_instance_valid(pivot):
			continue
		var target := Vector3(pivot.global_position.x, client.practice.sim.actors[1].p.y, pivot.global_position.z)
		client.practice.sim.actors[1].p = target
		var angles: Vector2 = Pose.aim_angles(client.practice.sim.actors[1], pivot.global_position)
		client.yaw = angles.x
		client.pitch = angles.y
		client.practice._publish()
		for _frame: int in range(70):
			await frames(1)
			var interaction: Dictionary = client.personal.get("interaction", {})
			if str(interaction.get("kind","")) == "door" and str(interaction.get("door_id","")) != "":
				return str(interaction.get("door_id",""))
	return ""

func _audio_count(name: String) -> int:
	return int(Dictionary(client.world.audio_fx.effects_started).get(name, 0))

func _wait_for_angle(id: String, target: float, tolerance: float, timeout: int = 180) -> bool:
	for _frame: int in range(timeout):
		if absf(float(client.practice.sim.doors.get(id, {}).get("angle", 0.0) - target)) <= tolerance:
			return true
		await frames(1)
	return false

func _wait_for_audio(name: String, start_count: int, expected_delta: int = 1, timeout: int = 220) -> bool:
	for _frame: int in range(timeout):
		if _audio_count(name) - start_count >= expected_delta:
			return true
		await frames(1)
	return false

func _wait_for_prompt(door_expected: String, require_use: bool = true, timeout: int = 120) -> bool:
	for _frame: int in range(timeout):
		var interaction: Dictionary = client.personal.get("interaction", {})
		if interaction.get("kind", "") == "door" and str(interaction.get("door_id", "")) == door_expected:
			if not require_use or bool(interaction.get("can_use", false)):
				return true
		await frames(1)
	return false

func _initialize() -> void:
	pref_exists = FileAccess.file_exists(Prefs.FILE_PATH)
	pref_bytes = FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if pref_exists else PackedByteArray()
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--door-shots="):
			folder=argument.trim_prefix("--door-shots=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("DOOR_CLIENT07 %s %s" % ["PASS" if ok else "FAIL",label])

func frames(count: int) -> void:
	for frame: int in range(count): await physics_frame
	await process_frame

func tap(action: String) -> void:
	var event: InputEvent = InputMap.action_get_events(action)[0].duplicate()
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	await frames(6)

func aim_at(point: Vector3) -> void:
	for correction: int in range(4):
		var angles := Pose.aim_angles(client.practice.sim.actors[1],point)
		client.yaw = angles.x
		client.pitch = angles.y
		await frames(12)

func shot(name: String) -> void:
	if folder.is_empty(): return
	DirAccess.make_dir_recursive_absolute(folder)
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(folder.path_join(name+".png"))

func _run() -> void:
	if DisplayServer.get_name()=="headless":
		check(false,"headless renderer is unsupported for this fixture")
		quit(1); return
	root.size=Vector2i(1280,720)
	var existing := FileAccess.file_exists(Prefs.FILE_PATH)
	var bytes := FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existing else PackedByteArray()
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	client=app.get_node("Client")
	client._start_practice("human","blood")
	client.practice.brains.clear()
	var sim: RefCounted=client.practice.sim
	sim.actors[1].p=Vector3(-.7,0,-8.2)
	for id: int in [101,102]:
		if sim.actors.has(id):
			sim.actors[id].p=Vector3(5+id%2,1.5,2)
	var interactive := _select_interactive_door(sim.doors)
	if not interactive.is_empty():
		door_id = interactive
	else:
		door_id = _select_door(sim.doors, str(sim.config.get("map_id", "house")))
	if door_id == "":
		check(false, "procedural house generates at least one door definition")
		quit(1)
		return
	var approach := _approach_point(door_id, 0.5)
	var pivot_position := _door_pivot_position(door_id)
	if not pivot_position.is_equal_approx(Vector3.ZERO):
		sim.actors[1].p = Vector3(pivot_position.x, sim.actors[1].p.y, pivot_position.z)
	elif approach != Vector3.ZERO:
		sim.actors[1].p = approach
	if not client.world.door_views.views.has(door_id):
		check(false, "test door exists in rendered door views")
		quit(1); return
	var door_state: Dictionary = sim.doors[door_id]
	door_state.angle=0.0
	door_state.target_angle=0.0
	door_state.moving=false
	client.practice._publish()
	await aim_at(_door_pivot_position(door_id))
	check(await _wait_for_prompt(door_id, false),"aimed nearby door reaches actual private HUD")
	check(client.ui._door_hint.visible and client.ui._door_hint.text.contains("puerta") and not client.ui._help_open,"compact contextual prompt by reticle, guide collapsed")
	check(client.world.door_views.views.size()==sim.doors.size(),"all authoritative doors are visible")
	var view: Dictionary = _door_view(door_id)
	check(not view.is_empty(),"selected door has a rendered view")
	var pivot: Node3D = view.get("pivot", null)
	if not is_instance_valid(pivot):
		check(false, "selected door pivot exists")
		quit(1); return
	check(pivot.get_child(0).has_meta("authored_asset"),"authored GLB replaces integration placeholder")
	await shot("01-cerrada")
	await frames(45)
	var remap := InputEventKey.new()
	remap.physical_keycode=KEY_K
	Prefs.bind_action("interact",remap,false)
	await frames(6)
	var remapped := Prefs.binding_text("interact")
	check(client.ui._door_hint.text.find(remapped)>=0,"door hint follows remapped interaction")
	var open_move_start := _audio_count("door_move")
	await tap("interact")
	check(await _wait_for_angle(door_id, Doors.OPEN_ANGLE, .015),"visible leaf and authoritative leaf converge open")
	check(await _wait_for_prompt(door_id, true),"aimed open leaf is interactable while opening")
	check(await _wait_for_audio("door_move", open_move_start, 1),"confirmed door movement emits a spatial cue")
	check(absf(_door_view_angle(door_id)-float(sim.doors[door_id].angle))<.015,"visible leaf converges to authoritative angle")
	await shot("02-abierta")
	# Prepared a second approach for closing.
	approach = _approach_point(door_id, 0.45)
	pivot_position = _door_pivot_position(door_id)
	if approach != Vector3.ZERO:
		sim.actors[1].p = pivot_position if not pivot_position.is_equal_approx(Vector3.ZERO) else approach
	await aim_at(_door_pivot_position(door_id))
	check(await _wait_for_prompt(door_id, true),"approaching open leaf provides close action")
	print("CLOSE_CONTEXT ",client.personal.get("interaction",{})," actor=",sim.actors[1].p," yaw=",client.yaw," pitch=",client.pitch)
	var latch_start := _audio_count("door_latch")
	await tap("interact")
	check(await _wait_for_angle(door_id, 0.0, .015),"same binding closes door")
	print("CLOSE_STATE ",sim.doors[door_id]," actor=",sim.actors[1].p)
	check(await _wait_for_audio("door_latch", latch_start, 1),"closed completion produces latch")
	var before: Dictionary=client.world.audio_fx.effects_started.duplicate()
	for repeat: int in range(20): client.practice._publish()
	check(client.world.audio_fx.effects_started==before,"duplicate snapshots do not replay door sounds")
	await tap("toggle_help")
	var revision: int=sim.doors[door_id].revision
	await tap("interact")
	await frames(20)
	check(sim.doors[door_id].revision==revision,"modal controls guide blocks door actions")
	await tap("toggle_help")
	client.yaw=0
	client.pitch=0
	await frames(20)
	check(client.personal.get("interaction",{}).is_empty(),"looking away removes contextual door action")
	check(not client.ui._door_hint.visible,"door prompt does not remain over gameplay")
	await shot("03-gameplay-libre")
	if pref_exists:
		var restore := FileAccess.open(Prefs.FILE_PATH, FileAccess.WRITE)
		restore.store_buffer(pref_bytes)
		restore.close()
	else:
		if FileAccess.file_exists(Prefs.FILE_PATH):
			DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	check((FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==bytes) if existing else not FileAccess.file_exists(Prefs.FILE_PATH),"checks preserve saved user preferences")
	await client._leave()
	app.queue_free()
	await process_frame
	print("DOOR_CLIENT07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
