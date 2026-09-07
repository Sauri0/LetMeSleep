extends SceneTree
## Integration fixture: real Client + UI + world, with only transport replaced.
## Run with the native renderer and -- --screens to capture the 3D lobby.

class NetworkDouble:
	extends Node
	signal lobby_updated(data: Dictionary)
	signal snapshot_updated(data: Dictionary)
	signal private_updated(data: Dictionary)
	signal notice(message: String)
	signal accepted(peer_id: int)
	signal disconnected
	signal waiting_updated(data: Dictionary)
	var local_cosmetics: Dictionary = {}
	var last_input: Dictionary = {}
	var input_count: int = 0
	var actions: Array[Dictionary] = []
	var connects: Array[Dictionary] = []
	var lobby_actions: Array[Dictionary] = []
	func connect_room(address: String, port: int, player_name: String, code: String, create: bool) -> void:
		connects.append({"address":address,"port":port,"name":player_name,"code":code,"create":create})
	func lobby_action(verb: String, value: Variant = null) -> void:
		lobby_actions.append({"verb":verb,"value":value})
	func send_input(sequence: int, movement: Vector3, yaw: float, pitch: float, interact: bool) -> void:
		input_count += 1
		last_input = {"sequence":sequence,"move":movement,"yaw":yaw,"pitch":pitch,"interact":interact}
	func send_action(sequence: int, verb: String) -> void:
		actions.append({"sequence":sequence,"verb":verb})
	func close_client() -> void:
		pass

var client: Node
var transport: NetworkDouble
var failures: int = 0
var checks: int = 0
var capture_screens: bool = false
var held_actions: Dictionary = {}

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, description: String) -> void:
	checks += 1
	print("CLIENT_UI %s %s" % ["PASS" if condition else "FAIL", description])
	if not condition:
		failures += 1

func _settle() -> void:
	await physics_frame
	await physics_frame
	await physics_frame
	await process_frame

func _mouse_matches(mode: int) -> bool:
	# Headless Godot keeps mouse_mode VISIBLE regardless of capture requests.
	# Native runs verify the actual mode; headless runs only cover UI/input state.
	return DisplayServer.get_name() == "headless" or Input.mouse_mode == mode

func _escape(echo: bool = false) -> void:
	var press := InputEventKey.new()
	press.keycode = KEY_ESCAPE
	press.physical_keycode = KEY_ESCAPE
	press.pressed = true
	press.echo = echo
	Input.parse_input_event(press)
	var release: InputEventKey = press.duplicate()
	release.pressed = false
	release.echo = false
	Input.parse_input_event(release)

func _action(action: String, pressed: bool) -> void:
	var bindings: Array[InputEvent] = InputMap.action_get_events(action)
	if bindings.is_empty():
		check(false, "fixture input binding exists for " + action)
		return
	var event: InputEvent = bindings[0].duplicate()
	if event is InputEventKey:
		event.pressed = pressed
		event.echo = false
	elif event is InputEventMouseButton:
		event.pressed = pressed
		event.position = Vector2(8,8)
		event.global_position = event.position
	Input.parse_input_event(event)
	held_actions[action] = pressed

func _mouse_motion() -> void:
	var event := InputEventMouseMotion.new()
	event.position = Vector2(640,360)
	event.global_position = event.position
	event.relative = Vector2(45,-12)
	Input.parse_input_event(event)

func _type_w() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_W
	event.physical_keycode = KEY_W
	event.unicode = 119
	event.pressed = true
	Input.parse_input_event(event)
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)

func _lobby_data() -> Dictionary:
	var config: Dictionary = load("res://scripts/simulation.gd").DEFAULT_CONFIG.duplicate(true)
	config.human_count = 1
	return {
		"code":"CASA02", "owner":1, "config":config,"can_start":true,"start_reason":"",
		"players":{1:{"name":"Luna","ready":true},2:{"name":"Mora","ready":true},3:{"name":"Nico","ready":true}},
		"actors":{
			1:{"name":"Luna","role":"human","state":"human","alive":true,"p":Vector3(-1.0,0,0.6),"yaw":0.0,"appearance":{"color":3,"accessory":1}},
			2:{"name":"Mora","role":"human","state":"human","alive":true,"p":Vector3(-0.1,0,-0.7),"yaw":2.8,"appearance":{"color":1,"accessory":2}},
			3:{"name":"Nico","role":"human","state":"human","alive":true,"p":Vector3(-1.9,0,-1.0),"yaw":2.9,"appearance":{"color":4,"accessory":0}},
		},
	}

func _round_data(role: String) -> Dictionary:
	var simulation: RefCounted = load("res://scripts/simulation.gd").new()
	var roster: Dictionary = {
		1:{"name":"Luna","role":role,"ready":true},
		2:{"name":"Mora","role":"mosquito" if role == "human" else "human","ready":true},
	}
	simulation.start(roster, load("res://scripts/simulation.gd").DEFAULT_CONFIG)
	return simulation.public_snapshot()

func _capture(name: String) -> void:
	if not capture_screens:
		return
	await RenderingServer.frame_post_draw
	var output_dir: String = ProjectSettings.globalize_path("res://../work")
	DirAccess.make_dir_recursive_absolute(output_dir)
	var path: String = output_dir.path_join("client-ui-%s.png" % name)
	var result: Error = root.get_texture().get_image().save_png(path)
	check(result == OK, "native screenshot " + name)

func _run() -> void:
	capture_screens = "--screens" in OS.get_cmdline_user_args()
	if DisplayServer.get_name() == "headless":
		print("CLIENT_UI NOTE headless backend: physical mouse capture assertions omitted; run native to verify them")
	transport = NetworkDouble.new()
	root.add_child(transport)
	client = load("res://scripts/client.gd").new()
	client.network = transport
	client.options = {}
	root.add_child(client)
	await _settle()
	check(client.ui._screen == "home" and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "home starts with pointer and menu")
	client.ui._home_default_focus.pressed.emit()
	await _settle()
	client.ui._name_edit.grab_focus()
	var text_before: String = client.ui._name_edit.text
	_type_w()
	await _settle()
	check(client.ui._name_edit.text != text_before and transport.input_count == 0, "typing in connection form edits text without gameplay input")
	_escape()
	await _settle()
	check(not client.ui._connection_open and client.ui._screen == "home", "Escape returns from connection form")
	transport.accepted.emit(1)
	var lobby: Dictionary = _lobby_data()
	transport.lobby_updated.emit(lobby)
	await _settle()
	check(client.waiting and not client.walking and client.ui.is_menu_open(), "enter lobby in menu state")
	check(_mouse_matches(Input.MOUSE_MODE_VISIBLE), "lobby menu releases mouse")
	check(client.world.actors.size() == 3 and client.world.get_actor(1).head.visible, "real 3D lobby contains all three full avatars")
	await _capture("lobby-menu")
	client.ui.walk_requested.emit()
	await _settle()
	check(client.walking and not client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_CAPTURED), "walk signal enters lobby controls and captures mouse")
	_action("move_forward",true)
	await _settle()
	check(Vector3(transport.last_input.move).length() > 0.5, "held movement reaches transport while walking")
	var look_before: float = client.yaw
	_mouse_motion()
	await _settle()
	check(not is_equal_approx(client.yaw,look_before), "mouse motion changes lobby look while walking")
	transport.lobby_updated.emit(lobby)
	await _settle()
	check(client.walking and not client.ui.is_menu_open(), "lobby refresh preserves walking state")
	_action("move_forward",false)
	await _settle()
	await _capture("lobby-walking")
	_escape()
	await _settle()
	check(not client.walking and client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "Escape from walking restores lobby menu and mouse")
	_action("move_forward",true)
	var actions_before: int = transport.actions.size()
	_action("attack",true)
	_action("attack",false)
	look_before = client.yaw
	_mouse_motion()
	await _settle()
	check(Vector3(transport.last_input.move) == Vector3.ZERO and transport.actions.size() == actions_before, "lobby menu blocks movement and combat actions")
	check(is_equal_approx(client.yaw,look_before), "lobby menu blocks camera motion")
	_action("move_forward",false)
	var field: LineEdit = client.ui._fields["round_seconds"].get_line_edit()
	var tab_ancestor: Node = field.get_parent()
	while tab_ancestor != null and not tab_ancestor is TabContainer:
		tab_ancestor = tab_ancestor.get_parent()
	if tab_ancestor is TabContainer:
		tab_ancestor.current_tab = 1
	await _settle()
	field.grab_focus()
	await _settle()
	_action("move_forward",true)
	await _settle()
	check(root.gui_get_focus_owner() == field and Vector3(transport.last_input.move) == Vector3.ZERO, "focused rule text field never moves avatar")
	_action("move_forward",false)
	_escape()
	await _settle()
	check(client.walking and not client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_CAPTURED), "Escape leaves focused rule field and restores walking")
	_escape()
	await _settle()
	client.ui._open_settings()
	await _settle()
	var original_binding: String = load("res://scripts/preferences.gd").binding_text("move_forward")
	client.ui._begin_binding("move_forward")
	_escape()
	await _settle()
	check(client.ui._settings_open and client.ui._binding_action.is_empty() and not client.walking, "Escape cancels key binding before closing settings or moving")
	check(load("res://scripts/preferences.gd").binding_text("move_forward") == original_binding, "cancelled rebinding preserves original control")
	_escape()
	await _settle()
	check(not client.ui._settings_open and not client.walking and client.ui.is_menu_open(), "Escape closes settings and stays in lobby menu")
	transport.snapshot_updated.emit(_round_data("human"))
	await _settle()
	check(client.playing and not client.waiting and client.role == "human" and _mouse_matches(Input.MOUSE_MODE_CAPTURED), "round entry takes control as human")
	check(not client.world.get_actor(1).head.visible, "human first-person head is masked after lobby")
	_escape()
	await _settle()
	check(client.ui._paused and client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "Escape opens human pause and releases pointer")
	_action("move_forward",true)
	_action("interact",true)
	actions_before = transport.actions.size()
	_action("attack",true)
	_action("attack",false)
	await _settle()
	check(Vector3(transport.last_input.move) == Vector3.ZERO and not bool(transport.last_input.interact) and transport.actions.size() == actions_before, "pause blocks movement task hold and attack")
	_action("move_forward",false)
	_action("interact",false)
	_escape(true)
	await _settle()
	check(client.ui._paused, "repeated Escape key event does not toggle pause")
	_escape()
	await _settle()
	check(not client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_CAPTURED), "Escape resumes human control")
	_action("interact",true)
	await _settle()
	check(bool(transport.last_input.interact), "human task input restored after pause")
	_action("interact",false)
	transport.snapshot_updated.emit({"phase":"results","winner":"humans","reason":"Fixture","config":lobby.config})
	await _settle()
	check(not client.playing and client.ui._screen == "results" and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "results restore menu pointer")
	transport.lobby_updated.emit(lobby)
	await _settle()
	transport.snapshot_updated.emit(_round_data("mosquito"))
	await _settle()
	check(client.role == "mosquito" and client.world.get_actor(1).actor_role == "mosquito", "next round can switch local role to mosquito")
	actions_before = transport.actions.size()
	_action("bite",true)
	_action("bite",false)
	await _settle()
	check(transport.actions.size() == actions_before + 1 and transport.actions.back().verb == "bite", "mosquito bite input reaches transport once")
	_escape()
	await _settle()
	actions_before = transport.actions.size()
	_action("bite",true)
	_action("bite",false)
	await _settle()
	check(transport.actions.size() == actions_before and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "mosquito pause blocks bite and releases pointer")
	_escape()
	await _settle()
	check(not client.ui.is_menu_open() and _mouse_matches(Input.MOUSE_MODE_CAPTURED), "mosquito pause restores control")
	for action: Variant in held_actions:
		if bool(held_actions[action]):
			_action(str(action),false)
	transport.disconnected.emit()
	await _settle()
	check(client.ui._screen == "home" and not client.playing and not client.waiting and _mouse_matches(Input.MOUSE_MODE_VISIBLE), "disconnect returns cleanly to home")
	client.world.end_customization()
	client.world.clear_actors()
	await _settle()
	print("CLIENT_UI_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
