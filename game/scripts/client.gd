extends Node

signal local_server_requested
const PreferencesScript = preload("res://scripts/preferences.gd")
const WorldScript = preload("res://scripts/world.gd")
const UIScript = preload("res://scripts/ui.gd")
const PracticeScript = preload("res://scripts/practice_session.gd")
const MapCatalog = preload("res://scripts/map_catalog.gd")
const HumanPose = preload("res://scripts/human_pose.gd")
var network: Node
var options: Dictionary
var world: Node3D
var ui: CanvasLayer
var local_id := 0
var state: Dictionary = {}
var personal: Dictionary = {}
var playing := false
var yaw := 0.0
var pitch := 0.0
var sequence := 0
var action_sequence := 0
var input_accumulator := 0.0
var role := "human"
var rig: Node3D
var arm: SpringArm3D
var camera: Camera3D
var camera_initialized := false
var was_alive := true
var screenshot_done := false
var screenshot_age := 0.0
var waiting := false
var walking := false
var waiting_state: Dictionary = {}
var practice: Node
var practice_active := false

func _ready() -> void:
	PreferencesScript.load_settings()
	PreferencesScript.setup_inputs()
	network.local_cosmetics = PreferencesScript.cosmetics
	world = WorldScript.new()
	world.name = "World"
	add_child(world)
	world.build()
	rig = Node3D.new()
	add_child(rig)
	arm = SpringArm3D.new()
	arm.collision_mask = 1
	arm.margin = 0.045
	var shape := SphereShape3D.new()
	shape.radius = 0.045
	arm.shape = shape
	rig.add_child(arm)
	camera = Camera3D.new()
	camera.near = 0.025
	camera.far = 55.0
	camera.fov = 78.0
	arm.add_child(camera)
	ui = UIScript.new()
	add_child(ui)
	practice = PracticeScript.new()
	add_child(practice)
	practice.snapshot_updated.connect(_snapshot)
	practice.private_updated.connect(func(data: Dictionary) -> void:
		personal = data
		if playing:
			ui.show_game(state, personal, local_id))
	ui.practice_requested.connect(_start_practice)
	ui.practice_restart_requested.connect(func() -> void:
		if practice_active:
			playing = false
			practice.restart())
	ui.connect_requested.connect(network.connect_room)
	ui.local_server_requested.connect(func() -> void: local_server_requested.emit())
	ui.cosmetics_changed.connect(func(value: Dictionary) -> void:
		network.local_cosmetics = value
		network.lobby_action("cosmetics", value))
	ui.preview_requested.connect(world.show_customization)
	ui.preview_closed.connect(world.end_customization)
	ui.walk_requested.connect(func() -> void: _set_walking(true))
	ui.escape_requested.connect(_on_escape)
	ui.ready_requested.connect(func(value: bool) -> void: network.lobby_action("ready", value))
	ui.config_requested.connect(func(value: Dictionary) -> void: network.lobby_action("config", value))
	ui.start_requested.connect(func() -> void: network.lobby_action("start"))
	ui.rematch_requested.connect(func() -> void: network.lobby_action("rematch"))
	ui.leave_requested.connect(_leave)
	network.accepted.connect(func(id: int) -> void: local_id = id)
	network.lobby_updated.connect(_lobby)
	network.waiting_updated.connect(func(data: Dictionary) -> void:
		if waiting:
			waiting_state = data)
	network.snapshot_updated.connect(_snapshot)
	network.private_updated.connect(func(data: Dictionary) -> void:
		personal = data
		if playing:
			ui.show_game(state, personal, local_id))
	network.notice.connect(ui.show_status)
	network.disconnected.connect(_disconnected)
	ui.show_home()
	if options.has("visual-preview"):
		_preview()
	elif options.has("practice-role"):
		_start_practice(str(options["practice-role"]), str(options.get("practice-mode", "blood")))

func _start_practice(selected_role: String, mode: String) -> void:
	network.close_client()
	practice_active = true
	playing = false
	waiting = false
	walking = false
	local_id = 1
	ui.set_practice(true)
	world.clear_actors()
	practice.start(selected_role, mode, PreferencesScript.cosmetics, PreferencesScript.player_name)

func _lobby(data: Dictionary) -> void:
	var entering := not waiting
	waiting = true
	playing = false
	state.clear()
	personal.clear()
	waiting_state = {"actors": data.get("actors", {})}
	world.visible = true
	if entering:
		world.end_customization()
		world.clear_actors()
		world.load_map("lobby")
		sequence = 0
		yaw = 0.0
		pitch = -0.12
		walking = false
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		camera.make_current()
	world.set_local_role(local_id, "lobby")
	ui.show_lobby(data, local_id)
	camera_initialized = false

func _set_walking(value: bool) -> void:
	walking = value and waiting
	ui.set_lobby_walking(walking)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if walking else Input.MOUSE_MODE_VISIBLE

func _snapshot(data: Dictionary) -> void:
	state = data
	if str(data.get("phase", "")) == "results":
		playing = false
		personal.clear()
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		world.show_assignment({}, camera, Vector3.ZERO)
		ui.show_results(data)
		return
	if str(data.get("phase", "")) != "playing":
		return
	var starting := not playing
	playing = true
	waiting = false
	walking = false
	var actor: Dictionary = data.get("actors", {}).get(local_id, {})
	role = str(actor.get("role", "human"))
	if starting:
		world.clear_actors()
		world.load_map(str(data.get("config", {}).get("map_id", "house")))
		yaw = float(actor.get("yaw", 0.0))
		pitch = 0.0
		sequence = 0
		action_sequence = 0
		personal.clear()
		camera_initialized = false
		ui.set_pause(false)
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		camera.make_current()
	world.set_local_role(local_id, role)
	ui.show_game(state, personal, local_id)

func _process(dt: float) -> void:
	if waiting:
		world.sync_actors(waiting_state.get("actors", {}), local_id, dt)
		world.set_local_role(local_id, "lobby")
		var avatar: Node3D = world.get_actor(local_id)
		if avatar != null:
			rig.global_position = avatar.global_position + Vector3(0, 1.35, 0)
			rig.rotation = Vector3(pitch, yaw, 0)
			arm.spring_length = 2.2
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if walking and not ui.is_menu_open() else Input.MOUSE_MODE_VISIBLE
		_screenshot_tick(dt)
		return
	if not playing:
		_screenshot_tick(dt)
		return
	var actors: Dictionary = state.get("actors", {})
	world.sync_actors(actors, local_id, dt)
	world.sync_pickups(state.get("pickups", {}))
	var actor: Dictionary = actors.get(local_id, {})
	if actor.is_empty():
		return
	var alive := bool(actor.get("alive", true))
	# A dead player sees the wait HUD on a neutral background, never a free spectator camera.
	world.visible = alive
	if alive:
		var visual: Node3D = world.get_actor(local_id)
		var position: Vector3 = visual.global_position if visual != null else actor.p
		var offset: Vector3 = HumanPose.sample(actor).eye if role == "human" else Vector3(0, 0.12, 0) + Vector3.FORWARD.rotated(Vector3.RIGHT,pitch).rotated(Vector3.UP,yaw)*0.10
		var camera_origin := position + offset
		if role == "mosquito":
			var map: Dictionary = MapCatalog.get_map(str(state.get("config", {}).get("map_id", "house")))
			camera_origin.x = clampf(camera_origin.x, -float(map.half_x)+0.16, float(map.half_x)-0.16)
			camera_origin.z = clampf(camera_origin.z, -float(map.half_z)+0.16, float(map.half_z)-0.16)
			camera_origin.y = clampf(camera_origin.y, 0.16, float(map.ceiling)-0.16)
		rig.global_position = camera_origin
		rig.rotation = Vector3(pitch, yaw, 0)
		arm.spring_length = 0.0 if role == "human" else 0.85
		camera.fov = 78.0 if role == "human" else 70.0
		if role == "mosquito":
			world.show_assignment(personal.get("assignment", {}), camera, actor.p, personal.get("focus",{}))
		else:
			world.show_assignment({}, camera, Vector3.ZERO)
		camera_initialized = true
	elif was_alive:
		world.show_assignment({}, camera, Vector3.ZERO)
	was_alive = alive
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if ui.is_menu_open() else Input.MOUSE_MODE_CAPTURED
	_screenshot_tick(dt)

func _physics_process(dt: float) -> void:
	if (not playing and not waiting) or options.has("visual-preview"):
		return
	input_accumulator += dt
	if input_accumulator < 1.0 / 30.0:
		return
	input_accumulator = 0.0
	var move := Vector3.ZERO
	var interact := false
	var sprint := false
	var crouch := false
	var jump := false
	if not ui.is_menu_open() and (not waiting or walking):
		move.x = Input.get_axis("move_left", "move_right")
		move.z = Input.get_axis("move_forward", "move_back")
		move.y = Input.get_axis("descend", "ascend") if role == "mosquito" and not waiting else 0.0
		interact = Input.is_action_pressed("interact" if role == "human" else "bite") and not waiting
		if waiting or role == "human":
			sprint = Input.is_action_pressed("sprint")
			crouch = Input.is_action_pressed("crouch")
			jump = Input.is_action_pressed("jump")
	sequence += 1
	var transport: Node = practice if practice_active else network
	transport.send_input(sequence, move, yaw, pitch, interact, sprint, crouch, jump)

func _on_escape() -> void:
	if waiting:
		_set_walking(not walking)
	elif playing:
		ui.set_pause(not ui.is_menu_open())
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if ui.is_menu_open() else Input.MOUSE_MODE_CAPTURED

func _unhandled_input(event: InputEvent) -> void:
	if not playing and not waiting:
		return
	if ui.is_menu_open():
		return
	if event is InputEventMouseMotion:
		var sensitivity: float = PreferencesScript.human_sensitivity if waiting or role == "human" else PreferencesScript.mosquito_sensitivity
		yaw = wrapf(yaw - event.relative.x * sensitivity, -PI, PI)
		pitch = clampf(pitch - event.relative.y * sensitivity * (-1.0 if PreferencesScript.invert_y else 1.0), -1.40, 1.30)
	if event.is_echo():
		return
	if waiting:
		return
	for verb: String in ["bite", "attack", "self_swat", "perch", "pickup", "drop"]:
		if event.is_action_pressed(verb):
			if verb == "bite" and (role != "mosquito" or state.get("actors",{}).get(local_id,{}).get("state","") != "biting"):
				continue
			action_sequence += 1
			var transport: Node = practice if practice_active else network
			transport.send_action(action_sequence, verb)
			break

func _leave() -> void:
	practice.stop()
	practice_active = false
	ui.set_practice(false)
	network.close_client()
	_disconnected()
	ui.show_home()

func _disconnected() -> void:
	if practice_active:
		return
	playing = false
	waiting = false
	walking = false
	world.visible = true
	world.clear_actors()
	world.load_map("lobby")
	world.menu_camera.make_current()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	ui.show_home()

func _preview() -> void:
	# Local visual fixture, never reachable through server RPC or used as game authority.
	var kind := str(options.get("visual-preview", ""))
	if kind.begins_with("custom-"):
		PreferencesScript.cosmetics = {"human":{"color": 3, "accessory": 1}, "mosquito":{"color": 4, "accessory": 2}}
		ui._open_customization()
		ui._select_custom_role(kind.trim_prefix("custom-"))
		return
	if kind == "lobby":
		local_id = 1
		var players := {1:{"name":"Luna", "role":"waiting", "ready":false}, 2:{"name":"Mora", "role":"waiting", "ready":true}, 3:{"name":"Zeta", "role":"waiting", "ready":false}}
		var actors := {}
		for id: int in players:
			actors[id] = {"name":players[id].name, "role":"human", "p":Vector3(-2.0 + (id-1) * 1.7, 0, -1.5), "yaw":0.0, "pitch":0.0, "state":"human", "alive":true, "swing":0.0, "bitten":false, "tool":"hands", "appearance":{"color":id, "accessory":id % 3}}
		_lobby({"code":"CASA42", "owner":1, "players":players, "actors":actors, "config":load("res://scripts/simulation.gd").DEFAULT_CONFIG, "can_start":false, "start_reason":"Falta que todos pulsen Estoy listo."})
		return
	var simulation: RefCounted = load("res://scripts/simulation.gd").new()
	var roster := {1:{"name":"Luna", "role":"human", "ready":true}, 2:{"name":"Zeta", "role":"mosquito", "ready":true}, 3:{"name":"Mora", "role":"mosquito", "ready":true}}
	simulation.start(roster, load("res://scripts/simulation.gd").DEFAULT_CONFIG)
	local_id = 2 if str(options.get("visual-preview", "")) == "mosquito" else 1
	if local_id == 2:
		var preview_target: Dictionary = simulation.private_for(2).assignment
		simulation.actors[2].p = preview_target.p + preview_target.normal * 1.1 + Vector3(0.32, -0.20, 0)
		simulation.actors[2].yaw = PI
		simulation.actors[3].p = Vector3(0.7, 1.4, -0.8)
	_snapshot(simulation.public_snapshot())
	personal = simulation.private_for(local_id)
	ui.show_game(state, personal, local_id)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func _screenshot_tick(dt: float) -> void:
	if not options.has("screenshot") or screenshot_done:
		return
	screenshot_age += dt
	if screenshot_age > 2.5:
		screenshot_done = true
		_capture.call_deferred()

func _capture() -> void:
	await RenderingServer.frame_post_draw
	var path := str(options.screenshot)
	var result := get_viewport().get_texture().get_image().save_png(path)
	print("SCREENSHOT %s code=%d" % [path, result])
	playing = false
	waiting = false
	practice.stop()
	world.clear_actors()
	await get_tree().process_frame
	await get_tree().process_frame
	get_tree().quit()
