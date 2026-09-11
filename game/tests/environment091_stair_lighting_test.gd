extends SceneTree
const WorldScene=preload("res://scripts/world.gd")
var checks:=0
var failures:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("ENVIRONMENT091_LIGHT_FAIL "+label)
func run()->void:
	var world:Node3D=WorldScene.new();root.add_child(world)
	var explicit:Array[Dictionary]=world._stair_light_specs({"stair_light_anchors":[
		{"id":"turn-a","p":Vector3(6,5.8,-2),"target":Vector3(4,3.3,1),"range":6.25},
		{"id":"clamped","p":Vector3(-5,9,-3),"target":Vector3(-5,6,0),"range":40.0},
		{"id":"invalid-target","p":Vector3.ZERO,"target":Vector3.ZERO,"range":4.0},
		{"id":"invalid-range","p":Vector3.UP,"target":Vector3.ZERO,"range":0.0}]})
	check(explicit.size()==2,"explicit anchors reject degenerate entries")
	check(explicit[0].p==Vector3(6,5.8,-2) and explicit[0].target==Vector3(4,3.3,1),"explicit position and target are preserved")
	check(is_equal_approx(float(explicit[0].range),6.25) and is_equal_approx(float(explicit[1].range),7.5),"anchor reach is preserved inside bounded shadow range")
	var no_fallback:Array[Dictionary]=world._stair_light_specs({"stair_light_anchors":[],"stair_connections":[{"bottom":Vector3.ZERO,"top":Vector3.UP*3.2}]})
	check(no_fallback.is_empty(),"explicit empty anchors do not revive legacy coordinates")
	var legacy:Array[Dictionary]=world._stair_light_specs({"half_x":99.0,"ceiling":99.0,"stair_connections":[
		{"id":"legacy-left","bottom":Vector3(-7,0,-4.5),"top":Vector3(-7,3.2,4.5)},
		{"id":"legacy-turn","bottom":Vector3(3,3.2,7),"top":Vector3(8,6.4,7)}]})
	check(legacy.size()==2,"legacy connections each receive one bounded fallback")
	check(is_equal_approx(Vector3(legacy[0].p).x,-7.0) and is_equal_approx(Vector3(legacy[1].p).z,7.0),"fallback follows connection geometry instead of house constants")
	for spec:Dictionary in legacy:
		check(float(spec.range)>=3.0 and float(spec.range)<=7.5,"fallback shadow cone stays within one-flight bound")
	# Build the maximum approved three-storey v2 budget:22 room keys, two
	# horizontal corridor lamps per floor and four stair-flight anchors.
	world.map_root=Node3D.new();world.add_child(world.map_root)
	var rooms:Array[Dictionary]=[]
	for index:int in range(22):
		rooms.append({"bounds":AABB(Vector3(index%6*3.0,0,index/6*3.0),Vector3(2.8,3.2,2.8))})
	var corridors:Array[AABB]=[]
	for floor_index:int in range(3):
		corridors.append(AABB(Vector3(-2,floor_index*3.2,-8),Vector3(4,3.2,16)))
		corridors.append(AABB(Vector3(-8,floor_index*3.2,-2),Vector3(16,3.2,4)))
		corridors.append(AABB(Vector3(-8,floor_index*3.2,4),Vector3(16,3.2,4)))
	var anchors:Array[Dictionary]=[]
	for index:int in range(4):
		var floor_index:int=index/2
		var x:float=-8.0 if index%2==0 else 8.0
		anchors.append({"id":"v2-%d"%index,"p":Vector3(x,floor_index*3.2+3.6,0),
			"target":Vector3(x,floor_index*3.2+1.8,0),"range":7.5})
	world.map_data={"rooms":rooms,"corridors":corridors,"stair_light_anchors":anchors}
	world._build_generated_lighting()
	var lights:Array[Node]=world.map_root.find_children("*","Light3D",true,false)
	var stair_lights:Array[SpotLight3D]=[]
	for node:Node in lights:
		if node.has_meta("house_stair"):stair_lights.append(node as SpotLight3D)
	var report:Dictionary=world.map_root.get_meta("generated_stair_lighting")
	check(lights.size()==32 and stair_lights.size()==4,"approved three-storey contract builds every light at Compatibility cap")
	check(not bool(report.over_budget) and int(report.dropped)==0,"valid v2 budget never drops a stair anchor")
	for index:int in range(stair_lights.size()):
		var direction:Vector3=(Vector3(anchors[index].target)-stair_lights[index].position).normalized()
		check(stair_lights[index].basis.is_finite() and (-stair_lights[index].basis.z).dot(direction)>.999,
			"vertical stair target produces a finite correctly aimed basis")
	world.map_root.free()
	var excessive:Array[Dictionary]=anchors.duplicate(true)
	excessive.append({"id":"over-budget","p":Vector3(0,4,0),"target":Vector3(0,1,0),"range":4.0})
	report=world._generated_light_budget({"rooms":rooms,"corridors":corridors},excessive.size())
	check(bool(report.over_budget) and int(report.local_lights)==33,"over-budget metadata is explicit")
	world.queue_free();await process_frame;await process_frame
	print("ENVIRONMENT091_STAIR_LIGHT checks=",checks," failures=",failures)
	quit(0 if failures==0 else 1)
