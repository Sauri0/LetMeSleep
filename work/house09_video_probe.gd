extends SceneTree
const Joinery = preload("res://scripts/frame_joinery.gd")
var output := ""
func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): output = arg.trim_prefix("--output=")
	run.call_deferred()
func run() -> void:
	root.size = Vector2i(1280,720)
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world); world.build(); world.load_map("house-v1-1")
	var masks: Dictionary = {}
	for mesh: MeshInstance3D in world.map_root.find_children("*", "MeshInstance3D", true, false):
		if not Joinery.is_frame(mesh,world.map_root): continue
		for face: Dictionary in Joinery.rectangles_from_frame(mesh):
			if int(face.axis) != 1: continue
			if not masks.has(face.plane): masks[face.plane] = []
			masks[face.plane].append(face.rect)
	var floor_overlap := 0.0
	var count := 0
	for mesh: MeshInstance3D in world.map_root.find_children("*", "MeshInstance3D", true, false):
		if str(mesh.get_meta("catalog_kind","")) != "floor" or not mesh.mesh is BoxMesh: continue
		var box: AABB = mesh.global_transform * mesh.mesh.get_aabb()
		var rect := Rect2(Vector2(box.position.z,box.position.x),Vector2(box.size.z,box.size.x))
		for mask: Rect2 in masks.get(Joinery.plane_key(1,box.end.y),[]):
			var overlap := rect.intersection(mask).get_area()
			if overlap > .000001: floor_overlap += overlap; count += 1
	print("FRAME_FLOOR_DIAG overlap_m2=",floor_overlap," intersections=",count)
	if not output.is_empty() and DisplayServer.get_name() != "headless":
		DirAccess.make_dir_recursive_absolute(output)
		var camera := Camera3D.new(); root.add_child(camera)
		camera.near=.025; camera.far=55; camera.fov=74; camera.make_current()
		var y: float = float(world.map_data.ceiling)-1.5
		camera.position=Vector3(0,y,6)
		for index: int in range(3):
			camera.look_at(Vector3((index-1)*3,y+.8,-6))
			for frame: int in range(8): await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(output.path_join("ceiling-%d.png"%index))
		camera.queue_free()
	world.queue_free(); await process_frame; await process_frame; quit()
