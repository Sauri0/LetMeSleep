extends SceneTree
const Library=preload("res://assets/art/house/house_library.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const Details=preload("res://scripts/house_details.gd")
var checks:=0
var failures:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("HOUSE_JOINT_FAIL "+label)
func run()->void:
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build();world.load_map("house")
	var crowns:Array[AABB]=[]
	for child:Node in world.map_root.get_children():
		if not str(child.name).begins_with("Cornice"):continue
		var model:Node3D=child.get_child(0)
		var box:=Library.mesh_bounds(model,child.transform)
		var ceiling:=2.989 if box.position.y<3.0 else 6.4
		check(box.end.y<=ceiling+.001 and absf(box.end.y-ceiling)<.002,"cornice meets ceiling without penetrating its visible underside")
		check(child.basis.determinant()>0.99,"trim uses a rotation, never mirrored winding")
		crowns.append(box)
	check(not crowns.is_empty(),"authored cornices exist")
	for door:Dictionary in Doors.get_doors().values():
		var center:Vector3=Doors.leaf_transform(door,0.0)*Vector3(float(door.width)*.5,0,0)
		var covered:=false
		for crown:AABB in crowns:
			if absf(crown.get_center().y-(2.95 if center.y<1.0 else 6.36))>.1:continue
			if Vector2(crown.get_center().x,crown.get_center().z).distance_to(Vector2(center.x,center.z))<float(door.width)*.5+.3:
				if crown.size.x>float(door.width)*.9 or crown.size.z>float(door.width)*.9:covered=true;break
		check(covered,"cornice remains continuous above actual portal "+str(door.id))
	for room:Dictionary in world.map_data.rooms:
		var nominal:AABB=room.bounds;var fitted:AABB=Details.interior_bounds(world,nominal)
		check(fitted.size.x>=nominal.size.x-.001 and fitted.size.z>=nominal.size.z-.001,"finish meets wall faces without shrinking the room")
		check(fitted.position.distance_to(nominal.position)<.22 and fitted.end.distance_to(nominal.end)<.22,"only metadata margin corrected; architecture unchanged")
	print("HOUSE_JOINT_RESULT checks=%d failures=%d"%[checks,failures])
	world.queue_free();await process_frame;await process_frame;quit(0 if failures==0 else 1)
