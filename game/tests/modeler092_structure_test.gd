extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("FAIL "+label)

func _initialize() -> void:
	var seeds: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="): seeds=[int(arg.trim_prefix("--seed="))]
	check(Generator.map_id(1)=="house-v3-1","v3 has a new identity")
	for id: String in ["house-v1-1","house-v2-1","house-v3-0","house-v3-01","house-v3-2147483647","house-v4-1"]:
		check(Generator.parse_seed(id)==-1,"reject "+id)
	var cases: Array=[]
	for seed_value: int in seeds:
		var data:=Generator.new().generate_structure(seed_value)
		var errors:=Validation.validate_zoning(data)
		check(errors.is_empty(),"zoning %d: %s"%[seed_value,errors])
		check(Validation.fingerprint(data)==Validation.fingerprint(Generator.new().generate_structure(seed_value)),"structure determinism %d"%seed_value)
		check(data.rooms.size()+2*data.floor_levels.size()+data.stair_light_anchors.size()<=32,"lighting capacity %d"%seed_value)
		for room: Dictionary in data.rooms:
			if room.zone=="service": check(float(room.area_m2)<=20.0,"compact service %d/%s"%[seed_value,room.id])
		cases.append({"seed":seed_value,"rooms":data.rooms.size(),"floors":data.floor_levels.size(),"service_quadrant":data.service_quadrant,"errors":errors})
		if seed_value==seeds[0]:
			var mutated:=data.duplicate(true)
			for room: Dictionary in mutated.rooms:
				if room.zone=="service":
					var bounds: AABB=room.bounds;bounds.size.x+=12;room.bounds=bounds;break
			check(not Validation.validate_zoning(mutated).is_empty(),"reject oversized service even with unchanged metadata")
	for floors: int in [2,3]:
		for quadrant: int in range(4):
			for minimum: bool in [true,false]:
				var fixture: Dictionary={"floor_count":floors,"service_quadrant":quadrant,"half_x":14.5 if minimum else 14.8,
					"half_z":11.0 if minimum else 11.4,"hall_half":1.8 if minimum else 1.7,"hall_end":6.6 if minimum else 6.4}
				var data:=Generator.new().generate_structure(1,fixture)
				check(not data.is_empty(),"extreme fixture generates")
				check(Validation.validate_zoning(data).is_empty(),"extreme dimensions %s"%fixture)
				for room: Dictionary in data.rooms:
					if room.theme_id=="entry":
						var point:=Vector3(room.portal);point.x=AABB(room.interior_bounds).get_center().x;point.y+=.1
						check(AABB(room.functional_zones[0].bounds).has_point(point),"entry group is on entrance side of compound room")
	var file:=FileAccess.open("res://../work/modeler092-structure-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"));file.close()
	print("MODELER092_STRUCTURE checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
