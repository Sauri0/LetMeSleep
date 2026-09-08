extends SceneTree
const Arena = preload("res://scripts/arena.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Barriers = preload("res://scripts/house_barriers.gd")
const Library = preload("res://assets/art/house/house_library.gd")
const Details = preload("res://scripts/house_details.gd")
var checks:=0
var failures:=0

func _initialize() -> void:_run.call_deferred()
func check(ok:bool,detail:String) -> void:
	checks+=1
	if not ok:failures+=1;printerr("HOUSE07_FAIL "+detail)

func _run() -> void:
	root.size=Vector2i(1280,720)
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world)
	world.build();world.load_map("house")
	var expected_assets={"Lavadero":"washer","Baño":"toilet","Dormitorio azul":"bed","Despensa":"pantry_crates","Sala de TV":"television","Sala de música":"piano","Taller de costura":"sewing_table","Comedor":"dining_set","Sala de juegos":"game_table"}
	var found:Dictionary={}
	for room:Dictionary in world.map_data.rooms:found[str(room.name)]=[]
	var furniture_count:=0
	for child:Node in world.map_root.get_children():
		if child.get_meta("catalog_kind","")!="furniture":continue
		furniture_count+=1
		var box:AABB=child.get_meta("catalog_box");var model:Node3D=child.get_child(0)
		var visual:AABB=Library.mesh_bounds(model,child.transform)
		check(absf(visual.position.y-box.position.y)<.002,"furniture sits on its physical floor")
		check(absf(visual.position.x-box.position.x)<.002 and absf(visual.end.x-box.end.x)<.002,"furniture visual X matches solid envelope")
		check(absf(visual.position.z-box.position.z)<.002 and absf(visual.end.z-box.end.z)<.002,"furniture visual Z matches solid envelope")
		for room:Dictionary in world.map_data.rooms:
			if AABB(room.bounds).has_point(box.get_center()):found[str(room.name)].append(str(model.get_meta("authored_asset","")));break
	for name:String in expected_assets:check(str(expected_assets[name]) in found[name],"recognizable room function "+name)
	check("bath_shower" in found["Baño"],"bathroom has closed shower screen and bath")
	for child:Node in world.map_root.get_children():
		if child.get_meta("catalog_kind","")!="furniture":continue
		var model:Node3D=child.get_child(0)
		if model.get_meta("authored_asset","")!="bath_shower":continue
		var box:AABB=child.get_meta("catalog_box")
		var visual:AABB=Library.mesh_bounds(model,child.transform)
		check(absf(visual.size.y-2.15)<.002 and visual.end.y<6.2,"shower matches2.15m solid envelope with ceiling clearance")
		check(is_equal_approx(box.size.x,1.6) and is_equal_approx(box.size.z,.65),"shower preserves original furniture footprint")
	check(furniture_count==52,"both floors preserve all52 furniture placements")
	for room:Dictionary in world.map_data.rooms:
		var details:Node=world.map_root.get_node_or_null("RoomDetails_"+str(room.name).validate_node_name())
		check(is_instance_valid(details) and int(details.get_meta("wall_details",0))>=1,"room has a solid-wall mounted authored detail: "+str(room.name))
	check(world.door_views.views.size()==10,"ten authored hinged doors installed")
	check(world.map_root.find_children("BathroomReflectionOnce","ReflectionProbe",true,false).size()==1,"one bounded reflection probe")
	var emitting_diffusers:=0
	for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
		for index:int in range(mesh.mesh.get_surface_count()):
			var material:Material=mesh.get_surface_override_material(index)
			if material is StandardMaterial3D and material.emission_enabled and "linen" in material.resource_name:emitting_diffusers+=1
	check(emitting_diffusers==16,"all16 room pendant diffusers have restrained warm emission")
	var shadows:=0
	for lamp:Light3D in world.map_root.find_children("*","Light3D",true,false):
		if lamp.shadow_enabled and lamp.light_energy>0:shadows+=1
	check(shadows<=4,"shadow light budget remains at most4")
	check(not world.scene_environment.ssr_enabled and not world.scene_environment.ssao_enabled,"unsupported screen-space effects stay disabled")
	for structure:Dictionary in world.map_data.structures:
		if structure.kind!="floor":continue
		var volume:=0.0;var local_bounds:=true
		for part:AABB in Details.floor_pieces(structure.box):
			volume+=part.get_volume()
			local_bounds=local_bounds and part.size.x<=4.001 and part.size.z<=4.001
		check(local_bounds,"floor light selection has local visual bounds")
		check(absf(volume-AABB(structure.box).get_volume())<.002,"floor visual partition preserves exact solid volume")
	await physics_frame
	for passage:Dictionary in [{"z":0.0,"open":false},{"z":.5,"open":true}]:
		var start:=Vector3(9.15,3.75,float(passage.z));var end:=Vector3(9.68,3.75,float(passage.z))
		var query:=PhysicsRayQueryParameters3D.create(start,end,1)
		var native_clear:bool=world.get_world_3d().direct_space_state.intersect_ray(query).is_empty()
		check(native_clear==bool(passage.open),"native balustrade has real post/gap")
		check(Arena.clear_segment(start,end)==native_clear,"authority and native post/gap agree")
	for spec:Dictionary in Details.window_specs(world.map_data):
		var p:Vector3=spec.p;var axis:int=spec.axis;var normal:=Vector3.ZERO;normal[axis]=1
		check(not Arena.clear_segment(p-normal*.4,p+normal*.4),"glazed exterior aperture remains physically solid")
	_stairs()
	if DisplayServer.get_name()!="headless" and OS.get_cmdline_user_args().has("--screens"):
		await _screens(world)
		await _contact_screen(world)
	if DisplayServer.get_name()!="headless" and OS.get_cmdline_user_args().has("--motion"):
		await _bathroom_motion(world)
	print("HOUSE07_RESULT checks=%d failures=%d furniture=%d barriers=%d"%[checks,failures,furniture_count,Barriers.get_boxes().size()])
	world.queue_free();await process_frame;await process_frame
	quit(0 if failures==0 else 1)

func _stairs() -> void:
	for side:float in [-1.0,1.0]:
		for up:bool in [true,false]:
			var z:float=-4.4 if (side<0)==up else 4.4
			var human:Dictionary={"p":Vector3(side*11.0,0.0 if up else 3.2,z),"yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true}
			var direction:=Vector3(0,0,1 if z<0 else -1)
			var valid:=true
			for tick:int in range(190):
				Arena.step_human(human,{"move":direction},1.0/60)
				valid=valid and Arena.can_fit_human(human.p)
			check(valid,"stair body remains collision free side%s up%s"%[side,up])
			check(absf(float(human.p.y)-(3.2 if up else 0.0))<.01 and absf(float(human.p.z))>4.0,"stair reaches opposite landing side%s up%s"%[side,up])

func _screens(world:Node3D) -> void:
	var states:Dictionary={}
	for id:String in Doors.get_doors():states[id]={"angle":0.0,"target_angle":0.0,"moving":false,"blocked":false,"revision":1}
	world.sync_doors(states,1.0)
	var camera:=Camera3D.new();root.add_child(camera);camera.fov=70;camera.make_current()
	var points:Array[Vector3]=[Vector3(-8.7,1.65,-6.4),Vector3(-2.8,1.65,-6.1),Vector3(2.8,1.65,-6.4),Vector3(-2.75,1.65,1.5),Vector3(2.75,1.65,-1.5),Vector3(-2.8,1.65,6.4),Vector3(2.8,1.65,6.4),Vector3(8.7,1.65,6.4)]
	var targets:Array[Vector3]=[Vector3(-12,.9,-9.6),Vector3(-6,.9,-10),Vector3(9,.9,-9.7),Vector3(-6,.85,-2.55),Vector3(6,.85,2.55),Vector3(-10,.9,9.3),Vector3(6,.9,9.6),Vector3(12,.9,9.6)]
	var names:Array[String]=["lavadero","cocina","comedor","sala","tv","recibidor","visitas","despensa","bano","dormitorio-azul","costura","biblioteca","musica","juegos","dormitorio-rosa","dormitorio-verde"]
	var folder:=ProjectSettings.globalize_path("res://../outputs/0.7-house")
	DirAccess.make_dir_recursive_absolute(folder)
	var report:Dictionary={"version":ProjectSettings.get_setting("application/config/version"),"fixture":"static rendered room survey","doors":"0.7 closed only for identical interior framing;0.6 baseline has no doors","fixed_views":[]}
	for i:int in range(16):
		var offset:=Vector3.UP*(3.2 if i>=8 else 0.0)
		camera.position=points[i%8]+offset;camera.look_at(targets[i%8]+offset)
		for frame:int in range(10):await process_frame
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(folder.path_join("%02d-%s.png"%[i+1,names[i]]))==OK,"rendered room "+names[i])
		report.fixed_views.append({"name":names[i],"position":camera.position,"target":targets[i%8]+offset})
	var file:=FileAccess.open(folder.path_join("survey.json"),FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	camera.queue_free()

func _contact_screen(world:Node3D) -> void:
	world.sync_actors({91:{"role":"human","name":"","p":Vector3(5.4,3.2,8.6),"yaw":.9,"view_yaw":.9,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true,"alive":true,"tool":"hands","state":"free","appearance":{"color":0,"accent":0,"accessory":3,"face":0,"hair":0,"outfit":0,"footwear":0}}},0,1.0)
	var camera:=Camera3D.new();root.add_child(camera);camera.fov=70;camera.make_current()
	camera.position=Vector3(3.15,4.85,6.25);camera.look_at(Vector3(6,4.2,8.6))
	for frame:int in range(10):await process_frame
	await RenderingServer.frame_post_draw
	var file:=ProjectSettings.globalize_path("res://../outputs/0.7-house/17-rosa-human-contact.png")
	check(root.get_texture().get_image().save_png(file)==OK,"human and furniture contact under authored room shadow light")
	print("HOUSE07_LIGHTING ambient=",world.scene_environment.ambient_light_energy," color=",world.scene_environment.ambient_light_color," spot_limit=",ProjectSettings.get_setting("rendering/limits/opengl/max_lights_per_object",8))
	world.clear_actors();camera.queue_free()

func _bathroom_motion(_world:Node3D) -> void:
	var camera:=Camera3D.new();root.add_child(camera);camera.fov=70;camera.make_current()
	var folder:=ProjectSettings.globalize_path("res://../outputs/0.7-house/bathroom-motion")
	DirAccess.make_dir_recursive_absolute(folder)
	for i:int in range(90):
		var t:=float(i)/89.0
		camera.position=Vector3(-9.0-.7*sin(t*PI),4.85,-6.8-t*.5)
		camera.look_at(Vector3(-12.4,4.35,-9.6))
		await process_frame;await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(folder.path_join("frame-%03d.png"%i))
	check(FileAccess.file_exists(folder.path_join("frame-089.png")),"moving camera bathroom capture completed")
	camera.queue_free()
