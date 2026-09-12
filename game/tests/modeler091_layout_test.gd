extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
const Geometry=preload("res://scripts/navigation_geometry.gd")
const Maps=preload("res://scripts/map_catalog.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, message: String) -> void:
	checks+=1
	if not ok: failures.append(message);print("FAIL "+message)

func _initialize() -> void:
	var seeds: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="): seeds=[int(arg.trim_prefix("--seed="))]
	check(Generator.map_id(1)=="house-v3-1","new map uses v3 identity")
	for id: String in ["house-v1-1","house-v2-1","house-v2-2147483646","house-v3-0","house-v3-01","house-v3--5","house-v3-2147483647","house-v4-1"]:
		check(Generator.parse_seed(id)==-1,"reject unsupported or noncanonical "+id)
	check(Maps.get_map("house-v2-1").is_empty(),"old v2 ID has no geometry or fallback")
	var cases: Array=[]
	for seed_value: int in seeds:
		var data: Dictionary=Generator.new().generate(seed_value)
		var report:=Validation.validate(data)
		check(report.passed,"seed %d: %s"%[seed_value,report.errors])
		check(Validation.fingerprint(data)==Validation.fingerprint(Generator.new().generate(seed_value)),"determinism %d"%seed_value)
		check(data.rooms.size()+2*data.floor_levels.size()+data.stair_light_anchors.size()<=32,"light budget %d"%seed_value)
		var side_count:=0
		var minimum_width:=INF
		for route: Dictionary in data.circulation_routes:
			if route.kind=="stair_side":
				side_count+=1
				minimum_width=minf(minimum_width,route.clear_width)
				check(float(route.clear_width)>=1.50,"side width %d/%s"%[seed_value,route.id])
		check(side_count==4*data.floor_levels.size(),"both sides of both stairs on every floor %d"%seed_value)
		for anchor: Dictionary in data.stair_light_anchors:
			var inside_solid:=false
			for box: AABB in data.obstacles:
				if box.has_point(anchor.p): inside_solid=true;break
			check(not inside_solid,"light emitter outside solids %d/%s"%[seed_value,anchor.id])
		cases.append({"seed":seed_value,"floors":data.floor_levels.size(),"rooms":data.rooms.size(),"minimum_width":minimum_width,
			"rejected_edges":report.rejected_edges,"fingerprint":Validation.fingerprint(data),"errors":report.errors})
		if seed_value==seeds[0]: _negative_cases(data)
	var file:=FileAccess.open("res://../work/modeler091-layout-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"));file.close()
	print("MODELER091_LAYOUT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)

func _negative_cases(data: Dictionary) -> void:
	var route: Dictionary={}
	for item: Dictionary in data.circulation_routes:
		if item.kind=="stair_side": route=item;break
	var center: Vector3=(Vector3(route.from)+Vector3(route.to))*.5
	# Keep the centre line traversable, but consume the extra promised width.
	var intrusion:=AABB(center+Vector3(.70,.1,-.15),Vector3(.04,1,.3))
	var changed:=data.duplicate(true)
	changed.obstacles.append(intrusion)
	var extra: Array[AABB]=[]
	extra.append_array(changed.barrier_boxes);extra.append_array(changed.pickup_support_boxes)
	var human:=Geometry.create(changed,true,extra)
	check(Geometry._segment(route.from,route.to,human),"width regression remains traversable by centre line")
	var errors:=Validation.validate_circulation(changed,human,extra)
	check(_contains(errors,"Obstructed circulation volume"),"detect obstruction outside capsule centre route")
	changed=data.duplicate(true)
	changed.circulation_routes.erase(changed.circulation_routes.filter(func(r:Dictionary)->bool:return r.kind=="stair_side")[0])
	check(_contains(Validation.validate_circulation(changed,Geometry.create(changed,true,extra),extra),"Missing four"),"detect missing side route")
	var area:=AABB(Vector3(0,.01,0),Vector3(2,2,4))
	var slabs: Array=[AABB(Vector3(0,-.2,0),Vector3(1,.2,4)),AABB(Vector3(1,-.2,0),Vector3(1,.2,4))]
	check(Validation._floor_covers(area,0,slabs),"adjacent slabs jointly support full width")
	slabs[1]=AABB(Vector3(1.1,-.2,0),Vector3(.9,.2,4))
	check(not Validation._floor_covers(area,0,slabs),"detect narrow unsupported gap inside lane")

func _contains(errors: Array[String], fragment: String) -> bool:
	for error: String in errors:
		if fragment in error: return true
	return false
