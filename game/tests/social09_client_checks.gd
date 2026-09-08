extends "res://tests/customization08_checks.gd"
## Native Main/Client/Practice integration; real inputs and real rig. No microphone.
const Catalog = preload("res://scripts/emote_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
var app: Node
var client: Node
var held: Dictionary = {}
var report: Dictionary = {}
var output := ""

func _settle_frames(count: int=5) -> void:
	for frame: int in range(count): await physics_frame
	await process_frame

func _action(action: String, pressed: bool) -> void:
	var event: InputEvent=InputMap.action_get_events(action)[0].duplicate()
	event.pressed=pressed
	if event is InputEventKey: event.echo=false
	if event is InputEventMouseButton:
		event.position=Vector2(12,12)
		event.global_position=event.position
	Input.parse_input_event(event)
	held[action]=pressed

func _tap(action: String) -> void:
	_action(action,true)
	await process_frame
	_action(action,false)
	await _settle_frames()

func _motion(delta: Vector2) -> void:
	var event:=InputEventMouseMotion.new()
	event.position=Vector2(640,360)
	event.global_position=event.position
	event.relative=delta
	Input.parse_input_event(event)

func _bind(action: String, key: Key) -> void:
	var event:=InputEventKey.new()
	event.physical_keycode=key
	Prefs.bind_action(action,event,false)

func _bone(actor: Node, name: String) -> Transform3D:
	var skin: Node=actor.imported_skin
	return skin.skeleton.get_bone_global_pose(int(skin.bone_ids[name]))

func _gesture(id: String) -> void:
	_action("emote_menu",true)
	await _settle_frames(2)
	_check(ui._emote_selector.visible,"real selector opens for "+id)
	ui._emote_selector.choose(id)
	_action("emote_menu",false)
	await _settle_frames(18)
	_check(str(client.practice.sim.actors[1].emote_id)==id,"authority accepted selector gesture "+id)
	_check(str(client.state.actors[1].emote_id)==id and float(client.state.actors[1].emote_time)>0,"published seconds reach Client "+id)

func _wait_recovery() -> void:
	var actor: Dictionary=client.practice.sim.actors[1]
	var until: float=float(actor._emote_cooldown)
	while float(client.practice.sim.elapsed)<until+.05:
		await physics_frame
	await _settle_frames(4)

func _capture(filename: String) -> void:
	if DisplayServer.get_name()=="headless": return
	await _settle()
	await RenderingServer.frame_post_draw
	_check(root.get_texture().get_image().save_png(output.path_join(filename+".png"))==OK,"saved "+filename)

func _run() -> void:
	_backup()
	output=ProjectSettings.globalize_path("res://../outputs/0.9-social-integration")
	DirAccess.make_dir_recursive_absolute(output)
	Prefs.load_settings()
	Prefs.video_resolution=0
	Prefs.video_fullscreen=false
	root.size=Vector2i(1280,720)
	root.content_scale_size=Vector2i(1280,720)
	_bind("emote_menu",KEY_B)
	_bind("move_forward",KEY_W)
	_bind("crouch",KEY_CTRL)
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	client=app.get_node("Client")
	client.options["map-seed"]="2"
	ui=client.ui
	await _settle_frames()
	_check(not bool(ui._voice_state.get("can_test",false)) and not ui._voice_test_held,"voice unavailable and microphone not requested")
	var appearances: Dictionary=Prefs.cosmetics.duplicate(true)
	ui._open_customization()
	ui._show_custom_emotes()
	await _settle_frames()
	var preview: Node=ui.get_customization_preview()
	var wrist_before: Transform3D=_bone(preview.avatar,"hand_l")
	ui.emote_preview_requested.emit("wave")
	await _settle_frames(30)
	_check(preview.emote_id=="wave" and preview.emote_time>.25,"Client starts preview driver with real time")
	_check(_bone(preview.avatar,"hand_l").origin.distance_to(wrist_before.origin)>.15,"preview moves imported hand bone")
	_check(Vector3(preview.avatar.global_position).length()<.001,"preview gesture has no root displacement")
	await _capture("preview-wave")
	ui._favorite_slot=0
	ui._favorite_buttons.yawn.pressed.emit()
	await _settle_frames(2)
	_check(Prefs.emote_favorites[0]=="yawn","real favorite UI updates root preference")
	var config:=ConfigFile.new()
	_check(config.load(Prefs.FILE_PATH)==OK and config.get_value("emotes","favorites",[])[0]=="yawn","favorite is written to actual settings file")
	_check(Prefs.cosmetics==appearances,"favorite preserves complete appearance payload")
	ui._select_custom_category("mouth")
	await _settle_frames()
	_check(preview.emote_id.is_empty(),"leaving gesture section stops real preview")
	ui._close_customization()
	Prefs.emote_favorites=[]
	Prefs._loaded=false
	Prefs.load_settings()
	ui._open_customization()
	ui._show_custom_emotes()
	await _settle_frames()
	_check(Prefs.emote_favorites[0]=="yawn" and ui._emote_state.favorites[0]=="yawn","favorite reloads through fresh file load and reopened menu")
	ui._close_customization()
	ui._open_practice()
	ui._practice_start_button.pressed.emit()
	client.practice.brains.clear()
	var actor: Dictionary=client.practice.sim.actors[1]
	actor.velocity=Vector3.ZERO
	client.practice._publish()
	await _settle_frames(18)
	_check(client.playing and client.practice_active and actor.grounded,"real practice starts eligible grounded human")
	_check(str(client.world.current_map)==str(client.practice.sim.config.map_id),"generated practice and rendered world share map identity")
	_action("emote_menu",true)
	await _settle_frames()
	_check(ui._emote_selector.visible and ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_VISIBLE,"B opens modal in actual Client")
	var yaw: float=client.yaw
	var sequence: int=client.action_sequence
	_action("move_forward",true)
	_motion(Vector2(190,-50))
	# Q reaches the same manual attack path without clicking the modal's
	# outside-click cancel area. Clicking outside intentionally dismisses it.
	await _tap("self_swat")
	await _settle_frames()
	_check(actor._move==Vector3.ZERO and client.action_sequence==sequence,"selector blocks movement and attack reaching authority")
	_check(is_equal_approx(client.yaw,yaw),"selector mouse does not rotate game camera")
	_action("move_forward",false)
	ui._emote_selector.choose("wave")
	_action("emote_menu",false)
	await _settle_frames(25)
	_check(actor.emote_id=="wave" and client.state.actors[1].emote_id=="wave","selection is published by real practice authority")
	var visual: Node3D=client.world.get_actor(1)
	var rest: Dictionary=client.state.actors[1].duplicate(true)
	rest.emote_id=""
	rest.emote_time=0.0
	_check(_bone(visual,"hand_l").origin.distance_to(Vector3(Pose.sample(rest).hand_l))>.15,"world rig visibly consumes replicated gesture")
	var start: Vector3=actor.p
	await _settle_frames(5)
	_check(Vector3(actor.p).distance_to(start)<.00001,"world gesture keeps authoritative root stationary")
	await _tap("toggle_help")
	_check(actor.emote_id.is_empty() and ui._help_open,"help menu cancels published gesture")
	await _tap("toggle_help")
	await _wait_recovery()
	await _gesture("shrug")
	_action("move_forward",true)
	await _settle_frames(7)
	_check(actor.emote_id.is_empty() and Vector3(actor._move).length()>.5,"physical W cancels gesture through authority")
	_action("move_forward",false)
	await _settle_frames(14)
	await _wait_recovery()
	await _gesture("yawn")
	root.focus_exited.emit()
	await _settle_frames()
	_check(actor.emote_id.is_empty(),"window focus loss cancels live gesture")
	await _wait_recovery()
	_action("crouch",true)
	await _settle_frames(20)
	_check(float(actor.crouch_amount)>.8 and not bool(ui._emote_state.available),"crouched human is unavailable in selector as in authority")
	_action("crouch",false)
	await _settle_frames(20)
	for tool: String in ["swatter","racket","newspaper","broom","slipper"]:
		actor.tool=tool
		client.practice._publish()
		await _settle_frames(5)
		visual=client.world.get_actor(1)
		var grip: Vector3=visual.to_local(visual.tool_socket.global_position)
		var wrist: Transform3D=_bone(visual,"hand_r")
		await _gesture("celebrate")
		_check(visual.current_tool==tool and is_instance_valid(visual.held_tool),"world retains equipped model "+tool)
		_check(visual.to_local(visual.tool_socket.global_position).distance_to(grip)<.001,"socket does not slip during gesture "+tool)
		_check(_bone(visual,"hand_r").origin.distance_to(wrist.origin)<.001,"equipped wrist remains fixed "+tool)
		await _tap("pause")
		_check(actor.emote_id.is_empty() and ui._paused,"Escape cancels equipped gesture "+tool)
		await _tap("pause")
		await _wait_recovery()
	report["public_emote_fields"]=["emote_id","emote_time"]
	report["real_microphone_used"]=false
	await _finish()

func _finish() -> void:
	finishing=true
	for action: String in held:
		if held[action]: _action(action,false)
	if is_instance_valid(app): app.queue_free()
	await _settle()
	_restore()
	var restored: bool=FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	_check(restored,"original preferences restored byte for byte")
	report.merge({"checks":checks,"failures":failures,"preferences_modified":not restored,"real_main_client_practice":true},true)
	var file:=FileAccess.open(output.path_join("social-checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("SOCIAL09_CLIENT_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
