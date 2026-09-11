extends SceneTree
const Joinery=preload("res://scripts/frame_joinery.gd")
const Doors=preload("res://scripts/door_catalog.gd")
var checks:=0
var failures:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("FRAME_JOIN_FAIL "+label)
func polygon_area(points:PackedVector2Array)->float:
	var area:=0.0
	for i:int in range(points.size()):area+=points[i].cross(points[(i+1)%points.size()])
	return absf(area)*.5
func run()->void:
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map("house")
	var masks:Dictionary={};var frames:=0;var cuts:=0;var overlap:=0.0;var normal_errors:=0;var area_after:=0.0;var area_before:=0.0
	for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
		if not Joinery.is_frame(mesh,world.map_root):continue
		frames+=1
		check(mesh.global_basis.determinant()>0,"frame winding not mirrored")
		check(mesh.mesh.get_surface_count()>0,"authored mesh retained")
		for face:Dictionary in Joinery.rectangles_from_frame(mesh):
			if not masks.has(face.plane):masks[face.plane]=[]
			masks[face.plane].append(face.rect)
	for mesh:MeshInstance3D in world.map_root.find_children("*","MeshInstance3D",true,false):
		if not mesh.has_meta("frame_original_box"):continue
		cuts+=1
		var box:AABB=mesh.get_meta("frame_original_box")
		area_before+=2.0*(box.size.x*box.size.y+box.size.x*box.size.z+box.size.y*box.size.z)
		check(mesh.global_basis.is_equal_approx(Basis.IDENTITY),"wall stays on its catalog axes")
		if mesh.mesh.get_surface_count()==0:continue
		var arrays:Array=mesh.mesh.surface_get_arrays(0)
		var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var normals:PackedVector3Array=arrays[Mesh.ARRAY_NORMAL]
		for i:int in range(0,vertices.size(),3):
			var a:Vector3=mesh.to_global(vertices[i]);var b:Vector3=mesh.to_global(vertices[i+1]);var c:Vector3=mesh.to_global(vertices[i+2])
			var cross:Vector3=(b-a).cross(c-a)
			if cross.length_squared()<1e-14:continue
			area_after+=cross.length()*.5
			if cross.normalized().dot(normals[i])>-.999:normal_errors+=1
			var axis:int=cross.abs().max_axis_index();var u:int=(axis+1)%3;var v:int=(axis+2)%3
			var triangle:=PackedVector2Array([Vector2(a[u],a[v]),Vector2(b[u],b[v]),Vector2(c[u],c[v])])
			for rect:Rect2 in masks.get(Joinery.plane_key(axis,a[axis]),[]):
				var rectangle:=PackedVector2Array([rect.position,Vector2(rect.end.x,rect.position.y),rect.end,Vector2(rect.position.x,rect.end.y)])
				for polygon:PackedVector2Array in Geometry2D.intersect_polygons(triangle,rectangle):overlap+=polygon_area(polygon)
	var report:Dictionary=world.map_root.get_meta("frame_joinery")
	check(frames==52 and int(report.frames)==frames,"all52 dynamic and fixed frame instances inspected")
	check(cuts>0 and cuts==int(report.wall_meshes),"corrected geometry is present in the actual World")
	check(overlap<.0001,"remaining triangle overlap with frame faces is zero within geometry tolerance")
	check(normal_errors==0,"all replacement faces preserve outward normal and front winding")
	check(absf(area_before-area_after-float(report.removed_area_m2))<.01,"removed area matches frame coverage plus hidden/duplicate wall faces")
	for angle:float in [0.0,PI/4,PI/2]:
		var states:Dictionary={}
		for id:String in Doors.get_doors():states[id]={"angle":angle,"target_angle":angle,"moving":angle==PI/4,"blocked":false,"revision":1}
		world.sync_doors(states,1.0)
		for id:String in Doors.get_doors():
			var view:Dictionary=world.door_views.views[id]
			check(view.pivot.transform.is_equal_approx(Doors.leaf_transform(Doors.DEFINITIONS[id],angle)),"leaf pivot/angle preserved "+id)
			var shape:CollisionShape3D=view.body.get_child(0)
			check(shape.shape.size.is_equal_approx(Doors.leaf_box(Doors.DEFINITIONS[id]).size),"door collider dimensions retained "+id)
			check(is_equal_approx(shape.position.y-shape.shape.size.y*.5,.14),"mosquito passage gap retained "+id)
	var instance:int=world.map_root.get_instance_id();world.load_map("house");await process_frame;await process_frame
	check(world.map_root.get_instance_id()==instance and world.map_root.get_meta("frame_joinery")==report,"no rebuild during frames or repeated same-map load")
	print("FRAME_JOINERY checks=",checks," failures=",failures," overlap_m2=",overlap," normal_errors=",normal_errors," ",JSON.stringify(report))
	world.queue_free();await process_frame;await process_frame;quit(0 if failures==0 else 1)
