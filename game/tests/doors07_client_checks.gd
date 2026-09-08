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

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--door-shots="): folder=argument.trim_prefix("--door-shots=")
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
	if DisplayServer.get_name()=="headless": quit(1); return
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
	for id: int in [101,102]: sim.actors[id].p=Vector3(5+id%2,1.5,2)
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	sim.doors.kitchen.moving=false
	client.practice._publish()
	await aim_at(Doors.handle_point(Doors.DEFINITIONS.kitchen,0))
	check(client.personal.get("interaction",{}).get("door_id","")=="kitchen","aimed nearby door reaches actual private HUD")
	check(client.ui._door_hint.visible and client.ui._door_hint.text.contains("puerta") and not client.ui._help_open,"compact contextual prompt by reticle, guide collapsed")
	check(client.world.door_views.views.size()==10,"all ten visible door instances")
	var pivot: Node3D=client.world.door_views.views.kitchen.pivot
	check(pivot.get_child(0).has_meta("authored_asset"),"authored GLB replaces integration placeholder")
	await shot("01-cerrada")
	await frames(45)
	var remap := InputEventKey.new()
	remap.physical_keycode=KEY_K
	Prefs.bind_action("interact",remap,false)
	await frames(6)
	check(client.ui._door_hint.text.contains("K"),"door hint follows remapped interaction")
	await tap("interact")
	await frames(85)
	check(float(sim.doors.kitchen.angle)>1.5 and int(sim.doors.kitchen.revision)==1,"one physical remapped key opens authoritative leaf once")
	check(absf(float(client.world.door_views.views.kitchen.angle)-float(sim.doors.kitchen.angle))<.015,"visible leaf converges to authoritative angle")
	check(int(client.world.audio_fx.effects_started.get("door_move",0))==1,"confirmed door movement emits one spatial cue")
	await shot("02-abierta")
	# Prepared second position approaches the handle now inside the room.
	sim.actors[1].p=Vector3(-5.1,0,-7.4)
	await aim_at(Doors.handle_point(Doors.DEFINITIONS.kitchen,Doors.OPEN_ANGLE))
	check(bool(client.personal.get("interaction",{}).get("can_use",false)),"approaching open leaf provides close action")
	print("CLOSE_CONTEXT ",client.personal.get("interaction",{})," actor=",sim.actors[1].p," yaw=",client.yaw," pitch=",client.pitch)
	await tap("interact")
	await frames(85)
	check(float(sim.doors.kitchen.angle)<.01,"same binding closes door")
	print("CLOSE_STATE ",sim.doors.kitchen," actor=",sim.actors[1].p)
	check(int(client.world.audio_fx.effects_started.get("door_latch",0))==1,"closed completion produces one latch")
	var before: Dictionary=client.world.audio_fx.effects_started.duplicate()
	for repeat: int in range(20): client.practice._publish()
	check(client.world.audio_fx.effects_started==before,"duplicate snapshots do not replay door sounds")
	await tap("toggle_help")
	var revision: int=sim.doors.kitchen.revision
	await tap("interact")
	await frames(20)
	check(sim.doors.kitchen.revision==revision,"modal controls guide blocks door actions")
	await tap("toggle_help")
	client.yaw=0
	client.pitch=0
	await frames(20)
	check(client.personal.get("interaction",{}).is_empty(),"looking away removes contextual door action")
	check(not client.ui._door_hint.visible,"door prompt does not remain over gameplay")
	await shot("03-gameplay-libre")
	check((FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==bytes) if existing else not FileAccess.file_exists(Prefs.FILE_PATH),"checks preserve saved user preferences")
	await client._leave()
	app.queue_free()
	await process_frame
	print("DOOR_CLIENT07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
