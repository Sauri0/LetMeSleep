extends SceneTree
## Approved smooth base only: same scene, rooms, cameras and lighting per capture.
const Arena=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
var phase:="after"
var checks:=0
var failures:=0
func _initialize()->void:
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--phase="):phase=arg.trim_prefix("--phase=")
	run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("LISO_FAIL "+label)
func run()->void:
	root.size=Vector2i(1920,1080);root.msaa_3d=Viewport.MSAA_2X
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map("house")
	# Closed leaves are behind these indoor camera positions; default open leaves
	# masked the wall/floor in the first survey. Same state in both comparisons.
	var states:Dictionary={}
	for id:String in Doors.get_doors():states[id]={"angle":0.0,"target_angle":0.0,"moving":false,"blocked":false,"revision":1}
	world.sync_doors(states,1.0)
	if phase=="before":
		# Reproduce the previous shader exactly through its explicit controls.
		# Only this fixture changes them; production always selects liso.
		for material:ShaderMaterial in world.surface_materials.values():
			material.set_shader_parameter("detail_amount",1.0)
			material.set_shader_parameter("finish_roughness",.88)
	var camera:=Camera3D.new();root.add_child(camera);camera.near=.025;camera.fov=70;camera.make_current()
	var folder:=ProjectSettings.globalize_path("res://../outputs/0.7-liso/"+phase);DirAccess.make_dir_recursive_absolute(folder)
	var views:Array[Dictionary]=[
		{"name":"09-bano","p":Vector3(-8.7,4.85,-6.4),"to":Vector3(-12,4.1,-9.6)},
		{"name":"10-dormitorio-azul","p":Vector3(-2.8,4.85,-6.1),"to":Vector3(-6,4.1,-10)},
		{"name":"17-dormitorio-rosa","p":Vector3(3.15,4.85,6.25),"to":Vector3(6,4.2,8.6)},
		{"name":"02-cocina","p":Vector3(-2.8,1.65,-6.1),"to":Vector3(-6,.9,-10)}]
	for view:Dictionary in views:
		camera.position=view.p;camera.look_at(view.to)
		var free:=true
		for box:AABB in Arena.obstacles():
			if box.grow(.025).has_point(camera.position):free=false
		check(free,"camera free "+view.name)
		for frame:int in range(14):await process_frame
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(folder.path_join(view.name+".png"))==OK,"saved "+view.name)
	if phase=="after":
		for room:Dictionary in world.map_data.rooms:
			var tint:Color=world._room_color(room)
			var material:ShaderMaterial=world._surface_material(tint,"wall")
			check(material.get_shader_parameter("tint")==tint,"room palette preserved "+str(room.name))
			check(is_equal_approx(float(material.get_shader_parameter("finish_roughness")),.82),"smooth wall roughness "+str(room.name))
		for kind:String in ["wood","wall","step","tile","panel"]:
			var material:ShaderMaterial=world._surface_material(Color("bc835b"),kind)
			check(is_equal_approx(float(material.get_shader_parameter("detail_amount")),0.0),"architectural microvariation disabled "+kind)
		var cloth:ShaderMaterial=world._surface_material(Color("bc835b"),"cloth")
		check(is_equal_approx(float(cloth.get_shader_parameter("detail_amount")),1.0),"cloth weave preserved")
		check(is_equal_approx(float(cloth.get_shader_parameter("finish_roughness")),.88),"cloth roughness preserved")
		check(world.wood is StandardMaterial3D,"object/trim material remains independent")
		check(world.map_data.rooms.size()==16,"all room functions preserved")
	var file:=FileAccess.open(folder.path_join("views.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"phase":phase,"views":views,"resolution":[1920,1080],"msaa":2,"room_count":world.map_data.rooms.size(),"checks":checks,"failures":failures},"\t"));file.close()
	print("HOUSE_LISO checks=",checks," failures=",failures," phase=",phase)
	world.queue_free();camera.queue_free();await process_frame;await process_frame;quit(0 if failures==0 else 1)
