extends SceneTree
const Library = preload("res://assets/art/house/house_library.gd")
const Barriers = preload("res://scripts/house_barriers.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Supports = preload("res://scripts/pickup_supports.gd")
var checks := 0
var failures := 0
func _initialize() -> void:
	_run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("HOUSE06_FAIL "+label)
func _run() -> void:
	root.size = Vector2i(1280,720)
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	check(world.map_root.find_children("*","StaticBody3D",true,false).size()==world.map_data.obstacles.size()+6+Barriers.get_boxes().size()+Doors.get_doors().size()+Supports.get_boxes().size(),"collision authority includes shared railings, door leaves and domestic pickup supports")
	var count := 0
	for child: Node in world.map_root.get_children():
		if child.get_meta("catalog_kind","")!="furniture": continue
		count += 1
		var catalog: AABB = child.get_meta("catalog_box")
		var model: Node3D = child.get_child(0)
		var visual: AABB = Library.mesh_bounds(model,child.transform)
		if absf(visual.position.x-catalog.position.x)>.002 or absf(visual.position.z-catalog.position.z)>.002:
			print("HOUSE06_BOUNDS asset=%s catalog=%s visual=%s root=%s model=%s"%[model.get_meta("authored_asset"),catalog,visual,child.transform,model.transform])
		check(absf(visual.position.y-catalog.position.y)<.002,"furniture rests on floor")
		check(absf(visual.position.x-catalog.position.x)<.002 and absf(visual.end.x-catalog.end.x)<.002,"furniture X collision envelope")
		check(absf(visual.position.z-catalog.position.z)<.002 and absf(visual.end.z-catalog.end.z)<.002,"furniture Z collision envelope")
		check(model.has_meta("authored_asset"),"authored GLB installed")
	check(count>30,"whole house library coverage")
	var camera := Camera3D.new()
	camera.fov=70
	root.add_child(camera)
	camera.make_current()
	var views := {
		"dormitorio":[Vector3(3,4.75,6.2),Vector3(6.2,4.05,9.6)],
		"sala":[Vector3(-3,1.65,-2.8),Vector3(-6,0.8,1.8)],
		"sala-conjunto":[Vector3(-2.75,1.65,1.5),Vector3(-6.0,0.85,-2.55)],
		"cocina":[Vector3(-2.8,1.65,-6.1),Vector3(-6,0.9,-10)],
		"pasillo":[Vector3(0,1.55,8),Vector3(0,1.55,-8)]}
	if DisplayServer.get_name()!="headless":
		var folder := ProjectSettings.globalize_path("res://../outputs/0.7-house/legacy-camera-regression")
		DirAccess.make_dir_recursive_absolute(folder)
		for label: String in views:
			camera.position=views[label][0]
			camera.look_at(views[label][1])
			if label == "pasillo":
				await physics_frame
				for pixel: Vector2 in [Vector2(640,360),Vector2(222,180),Vector2(1058,180)]:
					var origin := camera.project_ray_origin(pixel)
					var query := PhysicsRayQueryParameters3D.create(origin,origin+camera.project_ray_normal(pixel)*40.0,1)
					var hit := world.get_world_3d().direct_space_state.intersect_ray(query)
					check(not hit.is_empty(),"corridor wall closes at pixel "+str(pixel))
					print("HOUSE06_CORRIDOR_RAY pixel=%s point=%s normal=%s"%[pixel,hit.get("position"),hit.get("normal")])
			for frame: int in range(8): await process_frame
			await RenderingServer.frame_post_draw
			check(root.get_texture().get_image().save_png(folder.path_join(label+".png"))==OK,"capture "+label)
	print("HOUSE06_RESULT checks=%d failures=%d furnished=%d"%[checks,failures,count])
	world.queue_free()
	camera.queue_free()
	await process_frame
	await process_frame
	quit(0 if failures==0 else 1)
