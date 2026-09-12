extends SceneTree
const Library = preload("res://assets/art/house/alfa_library.gd")
var checks := 0
var failures: Array[String] = []
var capture := false
var output := ""
var stage: Node3D
var camera: Camera3D
func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg == "--capture": capture=true
		if arg.begins_with("--output="): output=arg.trim_prefix("--output=")
	_run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("ASSETS094_FAIL "+label)
func measured(node: Node3D, parent: Transform3D=Transform3D.IDENTITY) -> AABB:
	var transform := parent*node.transform
	var result := AABB()
	if node is MeshInstance3D and node.mesh != null: result=transform*node.get_aabb()
	for child: Node in node.get_children():
		if child is Node3D:
			var box := measured(child,transform)
			if box.has_surface(): result=result.merge(box) if result.has_surface() else box
	return result
func faces(node: Node3D, parent: Transform3D=Transform3D.IDENTITY) -> PackedVector3Array:
	var result := PackedVector3Array()
	var transform := parent*node.transform
	if node is MeshInstance3D and node.mesh != null:
		for point: Vector3 in node.mesh.get_faces(): result.append(transform*point)
	for child: Node in node.get_children():
		if child is Node3D: result.append_array(faces(child,transform))
	return result
func supported(triangles: PackedVector3Array, point: Vector3) -> bool:
	for i: int in range(0,triangles.size(),3):
		if Geometry3D.segment_intersects_triangle(point+Vector3.UP*.002,point-Vector3.UP*.002,triangles[i],triangles[i+1],triangles[i+2]) != null: return true
	return false
func _run() -> void:
	check(Library.catalog().size()==38,"38 explicit assets")
	check(not Library.has_asset("../bed"),"unknown ID rejected")
	for spec: Dictionary in Library.catalog():
		var id: String=spec.id
		var model := Library.instantiate_asset(id)
		check(model!=null,id+" imports")
		if model==null: continue
		var actual := measured(model)
		var expected := Library.bounds(id)
		check(actual.position.distance_to(expected.position)<.0003 and actual.size.distance_to(expected.size)<.0003,id+" imported bounds match actual mesh")
		check(absf(actual.position.y)<.0003,id+" floor pivot")
		check(model.find_children("*","CollisionObject3D",true,false).is_empty(),id+" world owns physics")
		check(model.scale.is_equal_approx(Vector3.ONE),id+" no implicit scale")
		for box: AABB in Library.collision_boxes(id):
			check(Library.placement_bounds(id).grow(.0001).encloses(box),id+" placement reserves entire physical shape")
		var copy := Library.spec(id)
		copy["id"]="changed"
		check(Library.spec(id).id==id,id+" metadata isolated from caller mutation")
		var triangles := faces(model)
		for surface: Dictionary in Library.support_surfaces(id):
			var c := Vector3(surface.center[0],surface.center[1],surface.center[2])
			for offset: Vector2 in [Vector2.ZERO,Vector2(-.4,-.4),Vector2(.4,-.4),Vector2(-.4,.4),Vector2(.4,.4)]:
				check(supported(triangles,c+Vector3(offset.x*surface.size[0],0,offset.y*surface.size[1])),id+" actual triangle supports declared surface")
		model.free()
	if capture:
		check(DisplayServer.get_name()!="headless","capture uses native renderer")
		if DisplayServer.get_name()!="headless": await capture_sheets()
	if not output.is_empty():
		DirAccess.make_dir_recursive_absolute(output)
		var file := FileAccess.open(output.path_join("assets094-pack.json"),FileAccess.WRITE)
		file.store_string(JSON.stringify({"checks":checks,"failures":failures,"assets":Library.catalog().size(),"capture":capture,"scope":"Imported GLB bounds, floor pivots, ownership of physics, real triangle support surfaces; optional native visual sheets."},"\t"))
	print("ASSETS094_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
func capture_sheets() -> void:
	root.size=Vector2i(1600,1000)
	var world := Node3D.new();root.add_child(world)
	var environment_node := WorldEnvironment.new();var environment := Environment.new()
	environment.background_mode=Environment.BG_COLOR;environment.background_color=Color("26323d")
	environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;environment.ambient_light_color=Color("c4d0db");environment.ambient_light_energy=.55
	environment_node.environment=environment;world.add_child(environment_node)
	var light := DirectionalLight3D.new();light.rotation_degrees=Vector3(-52,-28,0);light.light_energy=1.15;light.shadow_enabled=true;world.add_child(light)
	camera=Camera3D.new();camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=15.6;camera.position=Vector3(11,14,-17);world.add_child(camera);camera.look_at(Vector3(0,.45,0));camera.make_current()
	var floor := MeshInstance3D.new();var floor_mesh := BoxMesh.new();floor_mesh.size=Vector3(19,.05,16);floor.mesh=floor_mesh;floor.position.y=-.027
	var mat := StandardMaterial3D.new();mat.albedo_color=Color("46525c");mat.roughness=1.;floor.material_override=mat;world.add_child(floor)
	stage=Node3D.new();world.add_child(stage)
	var groups := {
		"interior":["bed","nightstand","wardrobe","bookcase","desk","chair","stool","table"],
		"garden":["bench","patio_table","fence_panel","planter","pine","bush","rock_large","grass_patch","path_stone","mailbox","barrel","log_stack"],
		"facade":["porch_post","shutter","canopy","chimney","crate","fence_post"]}
	DirAccess.make_dir_recursive_absolute(output)
	for group: String in groups:
		for child: Node in stage.get_children(): stage.remove_child(child);child.free()
		var names: Array=groups[group]
		var rows := ceili(float(names.size())/4.)
		for i: int in range(names.size()):
			var model := Library.instantiate_asset("alfa_"+str(names[i]));model.position=Vector3((i%4-1.5)*3.5,0,(floori(i/4.)-(rows-1)*.5)*3.7);stage.add_child(model)
			var label := Label3D.new();label.text=str(names[i]);label.font_size=40;label.pixel_size=.008;label.billboard=BaseMaterial3D.BILLBOARD_ENABLED;label.modulate=Color("f4e4c5");label.position=model.position+Vector3(0,.10,-1.30);stage.add_child(label)
		for frame: int in range(5): await process_frame
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(output.path_join(group+".png"))==OK,group+" native sheet")
