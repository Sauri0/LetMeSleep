extends SceneTree
const Joinery=preload("res://scripts/frame_joinery.gd")
const Details=preload("res://scripts/house_details.gd")
const Library=preload("res://assets/art/house/house_library.gd")
const Generator=preload("res://scripts/procedural_house.gd")
var failures:=0
var checks:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("SURFACES_FAIL "+label)
func box(parent:Node3D,bounds:AABB,kind:String="wall")->MeshInstance3D:
	var mesh:=MeshInstance3D.new();var shape:=BoxMesh.new();shape.size=bounds.size
	mesh.mesh=shape;mesh.position=bounds.get_center();mesh.set_meta("catalog_kind",kind);parent.add_child(mesh)
	return mesh
func area(mesh:MeshInstance3D)->float:
	var faces:PackedVector3Array=mesh.mesh.get_faces();var result:=0.0
	for i:int in range(0,faces.size(),3):result+=(faces[i+1]-faces[i]).cross(faces[i+2]-faces[i]).length()*.5
	return result
func union_case(label:String,boxes:Array[AABB],expected:float)->void:
	var group:=Node3D.new();root.add_child(group)
	for bounds:AABB in boxes:box(group,bounds)
	Joinery.resolve(group)
	var actual:=0.0
	for mesh:MeshInstance3D in group.get_children():actual+=area(mesh)
	check(absf(actual-expected)<.0001,label+" preserves exposed union area")
	group.free()
func run()->void:
	var unit:=AABB(Vector3.ZERO,Vector3.ONE)
	union_case("single",[unit],6.0)
	union_case("duplicate",[unit,unit],6.0)
	union_case("rounded duplicate",[unit,AABB(Vector3(.000001,0,0),Vector3.ONE)],6.0)
	union_case("touching",[unit,AABB(Vector3(1,0,0),Vector3.ONE)],10.0)
	union_case("overlapping",[unit,AABB(Vector3(.5,0,0),Vector3.ONE)],8.0)
	union_case("separated",[unit,AABB(Vector3(1.1,0,0),Vector3.ONE)],12.0)
	union_case("T junction",[AABB(Vector3.ZERO,Vector3(2,1,1)),AABB(Vector3(.5,0,1),Vector3.ONE)],14.0)
	var group:=Node3D.new();root.add_child(group)
	var wall:=box(group,AABB(Vector3.ZERO,Vector3(1,1.2,1)))
	var slab:=box(group,AABB(Vector3(0,1,0),Vector3(2,.2,2)),"floor")
	var slab_faces:=slab.mesh.get_faces()
	Joinery.resolve(group)
	check(absf(area(wall)-5.0)<.0001,"wall and lintel faces cannot pierce the upper slab")
	check(slab.mesh.get_faces()==slab_faces,"floor mesh itself is unchanged by wall cleanup")
	group.free()
	var pieces:=Details.floor_finishes(AABB(Vector3(0,-.2,0),Vector3(4,.2,4)),[
		{"bounds":AABB(Vector3.ZERO,Vector3(2,3.2,2)),"theme_id":"kitchen"},
		{"bounds":AABB(Vector3(1,0,1),Vector3(2,3.2,2)),"theme_id":"bathroom"},
		{"bounds":AABB(Vector3(0,3.2,0),Vector3(4,3.2,4)),"theme_id":"laundry"}])
	var total:=0.0;var tile:=0.0;var overlap:=0.0
	for i:int in range(pieces.size()):
		var bounds:AABB=pieces[i].box;var footprint:=Rect2(Vector2(bounds.position.x,bounds.position.z),Vector2(bounds.size.x,bounds.size.z))
		total+=footprint.get_area()
		if pieces[i].tile:tile+=footprint.get_area()
		check(absf(bounds.end.y)<.000001,"material regions keep exact feet level")
		for j:int in range(i):
			var other:AABB=pieces[j].box
			overlap+=footprint.intersection(Rect2(Vector2(other.position.x,other.position.z),Vector2(other.size.x,other.size.z))).get_area()
	check(absf(total-16.0)<.0001 and absf(tile-7.0)<.0001 and overlap<.0001,"floor materials cover slab once and ignore other storeys")
	var world:Node3D=load("res://scripts/world.gd").new();root.add_child(world);world.build()
	for map_id:String in ["house",Generator.map_id(1),Generator.map_id(2)]:
		world.load_map(map_id)
		var levels:Array=world.map_data.floor_levels;var crowns_per_floor:Array[int]=[];crowns_per_floor.resize(levels.size());crowns_per_floor.fill(0)
		for node:Node in world.map_root.get_children():
			if not str(node.name).begins_with("Cornice"):continue
			var bounds:=Library.mesh_bounds(node.get_child(0),node.transform)
			var floor_index:int=clampi(int(bounds.position.y/3.2),0,levels.size()-1)
			var ceiling:float=float(levels[floor_index+1])-.211 if floor_index+1<levels.size() else float(world.map_data.ceiling)
			check(absf(bounds.end.y-ceiling)<.002,map_id+" crown meets its real ceiling without piercing the next floor")
			crowns_per_floor[floor_index]+=1
		for count:int in crowns_per_floor:check(count>0,map_id+" all storeys receive cornices")
	world.queue_free();await process_frame;await process_frame
	print("HOUSE09_SURFACES checks=",checks," failures=",failures)
	quit(0 if failures==0 else 1)
