extends SceneTree
const World=preload("res://scripts/world.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Navigation=preload("res://scripts/map_navigation.gd")
const Arena=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
var checks:=0
var failures: Array[String]=[]

func check(value: bool, message: String) -> void:
	checks+=1
	if not value: failures.append(message)

func _initialize() -> void: _run.call_deferred()

func _run() -> void:
	create_timer(48).timeout.connect(func()->void:printerr("procedural world watchdog");quit(1))
	var seed_value:=2
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="): seed_value=int(arg.trim_prefix("--seed="))
	var data: Dictionary=Maps.get_map(Maps.Generator.map_id(seed_value))
	check(not data.is_empty(),"generated map validates")
	if data.is_empty(): quit(1);return
	var world:=World.new();root.add_child(world);world.build();world.load_map(data.id)
	check(world.current_map==data.id,"world uses requested seed")
	check(world.map_data.fingerprint==data.fingerprint,"world geometry fingerprint matches authority data")
	check(world.door_views.definitions.size()==data.doors.size(),"all generated doors rendered")
	check(world.map_root.find_children("*","SpotLight3D",true,false).size()<=32,"light count respects Compatibility budget")
	check(world.map_root.find_children("PickupSupport_*","Node3D",true,false).size()==data.pickup_supports.size(),"every generated support rendered")
	for human: bool in [true,false]:
		var graph: Dictionary=Navigation.graph_info(human,data.id)
		check(graph.invalid_edges.is_empty(),"runtime route geometry accepts all edges human="+str(human))
		for room: Dictionary in data.rooms:
			var origin: Vector3=data.human_spawns[0] if human else data.mosquito_spawns[0]
			var goal: Vector3=room.center if human else Vector3(room.center)+Vector3.UP*1.2
			check(not Navigation.path(origin,goal,human,data.id).is_empty(),"runtime path to "+str(room.id)+" human="+str(human))
	var opened: Dictionary={}
	for id: String in data.doors: opened[id]={"angle":Doors.OPEN_ANGLE}
	for point: Vector3 in data.human_spawns: check(Arena.can_fit_human(point,Arena.HUMAN_HEIGHT,data.id,opened),"human spawn fits authoritative geometry")
	var output:=ProjectSettings.globalize_path("res://../outputs/0.9-generated-house/seed-%d"%seed_value)
	DirAccess.make_dir_recursive_absolute(output)
	var captures: Array[String]=[]
	for floor_index: int in range(data.floor_levels.size()):
		var room: Dictionary={}
		for candidate: Dictionary in data.rooms:
			if int(candidate.floor)==floor_index: room=candidate;break
		var p: Vector3=room.portal
		var inside: Vector3=(Vector3(room.center)-p).normalized()
		world.menu_camera.position=p+inside*.8+Vector3.UP*1.6
		world.menu_camera.look_at(Vector3(room.center)+Vector3.UP*1.05)
		world.menu_camera.make_current()
		for frame: int in range(3): await process_frame
		await RenderingServer.frame_post_draw
		var name: String="floor-%d.png"%floor_index
		check(root.get_texture().get_image().save_png(output+"/"+name)==OK,"save actual room capture "+name)
		captures.append(name)
	var file:=FileAccess.open(output+"/checks.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"seed":seed_value,"map_id":data.id,"fingerprint":data.fingerprint,"checks":checks,"failures":failures,"captures":captures,"source":true},"\t"));file.close()
	for failure:String in failures: print("FAIL "+failure)
	world.free()
	print("procedural09_world checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
