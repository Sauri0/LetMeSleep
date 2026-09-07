extends SceneTree
## Real Main + Client + Practice authority; physical InputEvents, no transport double.
const Prefs = preload("res://scripts/preferences.gd")
var app: Node
var client: Node
var checks := 0
var failures := 0
var held: Dictionary = {}

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, description: String) -> void:
	checks += 1
	if not value: failures += 1
	print("HUD_INPUT06 %s %s" % ["PASS" if value else "FAIL",description])

func settle(frames: int = 5) -> void:
	for frame: int in range(frames): await physics_frame
	await process_frame

func action(name: String, pressed: bool) -> void:
	var event: InputEvent = InputMap.action_get_events(name)[0].duplicate()
	if event is InputEventKey:
		event.pressed = pressed
		event.echo = false
	elif event is InputEventMouseButton:
		event.pressed = pressed
		event.position = Vector2(12,12)
		event.global_position = event.position
	Input.parse_input_event(event)
	held[name] = pressed

func tap(name: String) -> void:
	action(name,true)
	await process_frame
	action(name,false)
	await settle()

func motion(delta: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = Vector2(640,360)
	event.global_position = event.position
	event.relative = delta
	Input.parse_input_event(event)

func _run() -> void:
	if DisplayServer.get_name()=="headless":
		printerr("HUD_INPUT06 requires a native renderer to check mouse capture")
		quit(1)
		return
	var existed := FileAccess.file_exists(Prefs.FILE_PATH)
	var bytes := FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existed else PackedByteArray()
	root.size = Vector2i(1280,720)
	app = load("res://scripts/main.gd").new()
	root.add_child(app)
	client = app.get_node("Client")
	# Test F1 physically without changing the saved user mapping.
	var help_key := InputEventKey.new()
	help_key.physical_keycode = KEY_F1
	Prefs.bind_action("toggle_help",help_key,false)
	for role: String in ["human","mosquito"]:
		client._start_practice(role,"blood")
		client.practice.brains.clear()
		var actor: Dictionary = client.practice.sim.actors[1]
		actor.p = Vector3(0,0 if role=="human" else 1.5,6.8)
		client.practice._publish()
		await settle(10)
		check(client.playing and client.role==role and not client.ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_CAPTURED,"Practice starts with live capture: "+role)
		action("move_forward",true)
		action("interact" if role=="human" else "bite",true)
		await settle()
		check(Vector3(actor._move).length()>0.5 and bool(actor._interact),"Held move and interaction reach authority before help: "+role)
		motion(Vector2(24,-9))
		await settle()
		var yaw_before: float = client.yaw
		var pitch_before: float = client.pitch
		await tap("toggle_help")
		check(client.ui._help_open and client.ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_VISIBLE,"Physical F1 opens modal help and releases capture: "+role)
		check(Vector3(actor._move)==Vector3.ZERO and not bool(actor._interact),"Opening help cancels previously held movement and interaction in authority: "+role)
		var sequence_before: int = client.action_sequence
		for verb: String in ["attack","self_swat","perch","pickup","drop"]:
			action(verb,true)
			action(verb,false)
		action("sprint",true)
		action("jump",true)
		action("crouch",true)
		motion(Vector2(360,-180))
		await settle()
		check(client.action_sequence==sequence_before and Vector3(actor._move)==Vector3.ZERO and not bool(actor._interact),"Help blocks combat, tools, movement and sustained actions: "+role)
		if role=="human": check(not bool(actor._sprint) and not bool(actor._crouch) and not bool(actor._jump),"Help also blocks run, crouch and jump")
		check(is_equal_approx(client.yaw,yaw_before) and is_equal_approx(client.pitch,pitch_before),"Mouse motion over help cannot move the camera: "+role)
		var focus: Control = root.gui_get_focus_owner()
		client.practice._publish()
		await settle()
		check(client.ui._help_open and root.gui_get_focus_owner()==focus and client.ui._help.is_ancestor_of(focus),"Real published snapshots preserve help and keyboard focus: "+role)
		for name: String in held.keys():
			if held[name]: action(name,false)
		await tap("toggle_help")
		check(not client.ui._help_open and not client.ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_CAPTURED,"Physical F1 returns capture to the game: "+role)
		check(absf(wrapf(client.yaw-yaw_before,-PI,PI))<0.00001 and absf(client.pitch-pitch_before)<0.00001,"Returning from help does not jump the camera angles: "+role)
		action("move_forward",true)
		await settle()
		check(Vector3(actor._move).length()>0.5,"Movement resumes after closing help: "+role)
		action("move_forward",false)
		motion(Vector2(12,4))
		await settle()
		check(absf(wrapf(client.yaw-yaw_before,-PI,PI))>0.001,"A fresh mouse motion resumes camera control: "+role)
		await tap("pause")
		client.ui._open_help()
		await tap("pause")
		check(client.ui._paused and not client.ui._help_open and Input.mouse_mode==Input.MOUSE_MODE_VISIBLE,"Escape from pause's guide returns only to pause: "+role)
		await tap("pause")
		check(not client.ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_CAPTURED,"Next Escape resumes the real client: "+role)
	await client._leave()
	await settle()
	app.queue_free()
	await process_frame
	check((FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==bytes) if existed else not FileAccess.file_exists(Prefs.FILE_PATH),"Native help checks do not write user preferences")
	if existed and FileAccess.get_file_as_bytes(Prefs.FILE_PATH)!=bytes:
		var restore := FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE)
		restore.store_buffer(bytes)
		restore.close()
	elif not existed and FileAccess.file_exists(Prefs.FILE_PATH): DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	print("HUD_INPUT06_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
