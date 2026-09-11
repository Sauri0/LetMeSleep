extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, message: String) -> void:
	checks+=1
	if not ok: failures.append(message);print("FAIL ",message)

func _initialize() -> void:
	var seeds: Array[int]=[221853394,1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="): seeds=[int(arg.trim_prefix("--seed="))]
	var cases: Array=[]
	for seed_value: int in seeds:
		var data: Dictionary=Generator.new().generate(seed_value)
		var report:=Validation.validate(data)
		check(report.passed,"seed %d: %s"%[seed_value,report.errors])
		check(report.rejected_edges.is_empty(),"no rejected route edges %d"%seed_value)
		check(Validation.fingerprint(data)==Validation.fingerprint(Generator.new().generate(seed_value)),"determinism %d"%seed_value)
		var target_items: Array=[]
		for room: Dictionary in data.rooms:
			var actual: Array=[]
			for entry: Dictionary in data.structures:
				if entry.kind!="furniture" or entry.get("room","")!=room.id: continue
				actual.append(entry.asset_id)
				var box: AABB=entry.box
				check(AABB(room.bounds).encloses(box),"inside room %d/%s/%s"%[seed_value,room.id,entry.asset_id])
				var overlaps:=false
				for obstacle: AABB in data.obstacles:
					if not obstacle.is_equal_approx(box) and box.intersects(obstacle): overlaps=true;break
				check(not overlaps,"solid clearance %d/%s/%s"%[seed_value,room.id,entry.asset_id])
				check(not box.intersects(room.clearance[0]) and not box.intersects(room.clearance[1]),"door and lane clearance %d/%s/%s"%[seed_value,room.id,entry.asset_id])
			check(actual.size()>=3 and actual.size()==int(room.furniture_count),"real furniture count %d/%s"%[seed_value,room.id])
			if seed_value==221853394 and room.id=="room-13":
				target_items=actual
				check("desk" in actual and "bed" in actual,"regression bedroom has real desk and bed")
		cases.append({"seed":seed_value,"errors":report.errors,"rejected_edges":report.rejected_edges,
			"fingerprint":Validation.fingerprint(data),"regression_room_items":target_items})
		print("CASE seed=%d errors=%s target=%s"%[seed_value,report.errors,target_items])
	var file:=FileAccess.open("res://../work/modeler091-furnishing-regression-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"));file.close()
	print("MODELER091_FURNISH checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
