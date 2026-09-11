extends SceneTree
const Joinery = preload("res://scripts/frame_joinery.gd")
var output := ""
var near_plane := .025
var diagnose_rays := false
var no_shadows := false
var no_lod := false
func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): output = arg.trim_prefix("--output=")
		if arg.begins_with("--near="): near_plane = float(arg.trim_prefix("--near="))
		if arg=="--rays": diagnose_rays=true
		if arg=="--no-shadows": no_shadows=true
		if arg=="--no-lod": no_lod=true
	run.call_deferred()
func run() -> void:
	root.size = Vector2i(1280,720)
	if no_lod: root.mesh_lod_threshold=0.0
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world); world.build(); world.load_map("house-v1-1")
	if no_shadows:
		for light:Light3D in world.find_children("*","Light3D",true,false): light.shadow_enabled=false
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
		camera.near=near_plane; camera.far=55; camera.fov=74; camera.make_current()
		var y: float = float(world.map_data.ceiling)-1.5
		camera.position=Vector3(0,y,6)
		for index: int in range(3):
			camera.look_at(Vector3((index-1)*3,y+.8,-6))
			for frame: int in range(8): await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(output.path_join("ceiling-%d.png"%index))
			if index==1 and diagnose_rays:
				for pixel:Vector2 in [Vector2(450,545),Vector2(454,545),Vector2(460,550)]:
					var origin:=camera.project_ray_origin(pixel);var direction:=camera.project_ray_normal(pixel)
					var hits:Array[Dictionary]=[]
					for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
						if not mesh.is_visible_in_tree() or mesh.mesh.get_surface_count()==0:continue
						if (mesh.global_transform*mesh.mesh.get_aabb()).intersects_ray(origin,direction)==null:continue
						var faces:PackedVector3Array=mesh.mesh.get_faces()
						for vertex:int in range(0,faces.size(),3):
							var a:=mesh.to_global(faces[vertex]);var b:=mesh.to_global(faces[vertex+1]);var c:=mesh.to_global(faces[vertex+2])
							if (b-a).cross(c-a).dot(direction)<=0:continue
							var hit:Variant=Geometry3D.ray_intersects_triangle(origin,direction,a,b,c)
							if hit!=null:hits.append({"distance":origin.distance_to(hit),"at":str(hit),"path":str(mesh.get_path()),"kind":mesh.get_meta("catalog_kind",""),"skin":mesh.get_meta("frame_join_wall",false),"bounds":str(mesh.global_transform*mesh.mesh.get_aabb())})
					hits.sort_custom(func(a:Dictionary,b:Dictionary)->bool:return a.distance<b.distance)
					print("RAY_DIAG ",pixel," ",JSON.stringify(hits.slice(0,2)))
		camera.queue_free()
	world.queue_free(); await process_frame; await process_frame; quit()
