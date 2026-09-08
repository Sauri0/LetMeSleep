extends SceneTree
## Real Main / Client / Practice, physical mapped events and server snapshots.
const Prefs = preload("res://scripts/preferences.gd")
const Collision = preload("res://scripts/projectile_collision.gd")
var app: Node
var client: Node
var checks := 0
var failures := 0

func _initialize() -> void: _run.call_deferred()
func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok: failures+=1
	print("THROW_CLIENT07 %s %s"%["PASS" if ok else "FAIL",label])
func settle(frames: int=5) -> void:
	for frame: int in range(frames): await physics_frame
	await process_frame
func action(name: String,pressed: bool) -> void:
	var event: InputEvent = InputMap.action_get_events(name)[0].duplicate()
	event.pressed=pressed
	if event is InputEventKey: event.echo=false
	elif event is InputEventMouseButton:
		event.position=Vector2(640,360);event.global_position=event.position
	Input.parse_input_event(event)
func tap(name: String) -> void:
	action(name,true);await process_frame;action(name,false);await settle()
func equip() -> int:
	var sim: RefCounted=client.practice.sim
	for id: int in sim.pickups:
		if str(sim.pickups[id].tool)!="newspaper": continue
		var item: Dictionary=sim.pickups[id]
		item.holder=0;item.state="ground";item.rotation=Vector3(PI/2,0,0)
		item.p=sim.actors[1].p+Vector3(.4,Collision.resting_offset("newspaper",item.rotation),0)
		client.practice._publish();await settle()
		await tap("pickup")
		check(str(sim.actors[1].tool)=="newspaper","physical pickup equips one real object")
		return id
	return -1
func _run() -> void:
	if DisplayServer.get_name()=="headless": quit(1);return
	var existed := FileAccess.file_exists(Prefs.FILE_PATH)
	var bytes := FileAccess.get_file_as_bytes(Prefs.FILE_PATH) if existed else PackedByteArray()
	root.size=Vector2i(1280,720)
	app=load("res://scripts/main.gd").new();root.add_child(app);client=app.get_node("Client")
	var help_key:=InputEventKey.new();help_key.physical_keycode=KEY_F1;Prefs.bind_action("toggle_help",help_key,false)
	client._start_practice("human","blood");client.practice.brains.clear()
	var sim: RefCounted=client.practice.sim
	sim.actors[1].p=Vector3(-7,0,8);client.yaw=0;client.pitch=0
	client.practice._publish();await settle(10)
	check(sim.actors[1].tool=="hands","human starts with hands")
	var id: int=await equip()
	action("throw",true);await settle(18)
	check(client._throw_pressed and str(client.personal.get("throw",{}).get("state",""))=="charging","mapped right button charges through real authority")
	check(client.ui._hud_tool.text.contains("%") and client.ui._attack_recovery.visible,"charge uses existing compact HUD")
	await tap("toggle_help");await settle()
	check(client.ui._help_open and not client._throw_pressed and str(sim.actors[1].throw_gesture.state)!="charging","F1 cancels server charge while opening guide")
	action("throw",false);await settle()
	check(sim.pickups[id].holder==1 and sim._projectiles.is_empty(),"release over guide cannot launch")
	await tap("toggle_help")
	action("throw",true);await settle(12);root.focus_exited.emit();await settle()
	action("throw",false);await settle()
	check(not client._throw_pressed and sim.pickups[id].holder==1 and sim._projectiles.is_empty(),"window focus signal cancels; late release preserves held object")
	action("throw",true);await settle(12);await tap("pause");action("throw",false);await settle()
	check(client.ui.is_menu_open() and sim.pickups[id].holder==1 and sim._projectiles.is_empty(),"Escape cancels before opening pause")
	await tap("pause")
	action("throw",true);await settle(24);action("throw",false);await settle(12)
	check(sim.actors[1].tool=="hands" and sim.pickups[id].holder==0,"fresh physical release transfers the same object")
	check(str(sim.pickups[id].state) in ["flying","ground"],"released object exists in synchronized world")
	check(client.world.pickup_views.has(id) and client.world.pickup_views[id].visible,"real client renders released object")
	await client._leave();app.queue_free();await process_frame
	check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==bytes if existed else not FileAccess.file_exists(Prefs.FILE_PATH),"fixture preserves user preferences")
	if existed and FileAccess.get_file_as_bytes(Prefs.FILE_PATH)!=bytes:
		var restore:=FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE);restore.store_buffer(bytes);restore.close()
	elif not existed and FileAccess.file_exists(Prefs.FILE_PATH): DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	print("THROW_CLIENT07_RESULT checks=%d failures=%d"%[checks,failures]);quit(0 if failures==0 else 1)
