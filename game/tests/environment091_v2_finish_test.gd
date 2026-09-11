extends SceneTree
const WorldScene=preload("res://scripts/world.gd")
const Generator=preload("res://scripts/procedural_house.gd")
const Library=preload("res://assets/art/house/house_library.gd")
const Prefs=preload("res://scripts/preferences.gd")
var checks:=0
var failures:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("ENVIRONMENT091_V2_FAIL "+label)
func _finish_bounds(world:Node3D)->Array[Dictionary]:
	var result:Array[Dictionary]=[]
	var to_map:Transform3D=world.map_root.global_transform.affine_inverse()
	for node:Node in world.map_root.get_children():
		if str(node.name).begins_with("Baseboard"):
			result.append({"name":str(node.name),"box":Library.mesh_bounds(node as Node3D)})
	for node:Node in world.map_root.find_children("*","MeshInstance3D",true,false):
		var mesh:=node as MeshInstance3D
		if not mesh.get_meta("frame_join_wall",false):continue
		result.append({"name":str(mesh.name),"box":(to_map*mesh.global_transform)*mesh.get_aabb()})
	return result
func run()->void:
	var saved_shadows:int=Prefs.video_shadows
	Prefs.video_shadows=2
	var world:Node3D=WorldScene.new();root.add_child(world);world.build()
	for seed_value:int in [1,2,17,42,777,1988,2147483646]:
		var id:String=Generator.map_id(seed_value)
		world.load_map(id)
		var anchors:Array=world.map_data.stair_light_anchors
		var anchor_by_id:Dictionary={}
		for anchor:Dictionary in anchors:anchor_by_id[str(anchor.id)]=anchor
		var local_lights:Array[Light3D]=[];var stair_lights:Array[SpotLight3D]=[]
		for node:Node in world.map_root.find_children("*","Light3D",true,false):
			if node is DirectionalLight3D:continue
			local_lights.append(node as Light3D)
			if node.has_meta("house_stair"):stair_lights.append(node as SpotLight3D)
		var report:Dictionary=world.map_root.get_meta("generated_stair_lighting",{})
		check(local_lights.size()<=32,id+" stays inside Compatibility positional light cap")
		check(stair_lights.size()==anchors.size() and int(report.dropped)==0,id+" builds every stair anchor")
		check(not bool(report.over_budget),id+" lighting budget is valid")
		for light:SpotLight3D in stair_lights:
			var light_id:=str(light.get_meta("stair_light_id",""))
			check(anchor_by_id.has(light_id),id+" light has authored anchor "+light_id)
			if not anchor_by_id.has(light_id):continue
			var anchor:Dictionary=anchor_by_id[light_id]
			var direction:Vector3=(Vector3(anchor.target)-light.position).normalized()
			check(light.position.is_equal_approx(Vector3(anchor.p)) and is_equal_approx(light.spot_range,float(anchor.range)),id+" preserves anchor transform "+light_id)
			check(light.basis.is_finite() and (-light.basis.z).dot(direction)>.999,id+" aims at anchor target "+light_id)
			check(light.shadow_enabled and bool(light.get_meta("authored_shadows",false)),id+" stair shadow follows high quality setting "+light_id)
		var finishes:Array[Dictionary]=_finish_bounds(world);var offenders:Array[String]=[]
		for route:Dictionary in world.map_data.circulation_routes:
			if str(route.kind)!="stair_side":continue
			var clearance:AABB=route.clearance
			for finish:Dictionary in finishes:
				var overlap:AABB=clearance.intersection(AABB(finish.box))
				if overlap.has_volume() and overlap.get_volume()>1e-7:
					offenders.append(str(route.id)+":"+str(finish.name)+":"+str(overlap))
		check(offenders.is_empty(),id+" rendered finishes respect stair-side clearance "+str(offenders.slice(0,3)))
	Prefs.video_shadows=saved_shadows
	world.queue_free();await process_frame;await process_frame
	print("ENVIRONMENT091_V2_FINISH checks=",checks," failures=",failures)
	quit(0 if failures==0 else 1)
