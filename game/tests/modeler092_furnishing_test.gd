extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
const Geometry=preload("res://scripts/navigation_geometry.gd")
const DoorGeometry=preload("res://scripts/door_geometry.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("FAIL "+label)

func _initialize() -> void:
	var seeds: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="): seeds=[int(arg.trim_prefix("--seed="))]
	var cases: Array=[]
	for seed_value: int in seeds:
		var start:=Time.get_ticks_msec()
		var data:=Generator.new().generate(seed_value)
		var report:=Validation.validate(data)
		check(report.passed,"seed %d: %s"%[seed_value,report.errors])
		check(Validation.fingerprint(data)==Validation.fingerprint(Generator.new().generate(seed_value)),"determinism %d"%seed_value)
		var rooms: Array=[]
		for room: Dictionary in data.rooms:
			rooms.append({"id":room.id,"uses":room.uses,"area":room.area_m2,"report":room.furnishing_report})
		cases.append({"seed":seed_value,"elapsed_ms":Time.get_ticks_msec()-start,"errors":report.errors,"rooms":rooms,"furniture":data.furniture_count})
		if seed_value==seeds[0]:
			_door_sweeps(data)
			var missing:=data.duplicate(true)
			var changed_bed:=false
			for item: Dictionary in missing.structures:
				if str(item.get("asset_id",""))=="bed": item.asset_id="table";changed_bed=true;break
			check(changed_bed,"missing-bed regression actually removes a generated bed")
			var extra: Array[AABB]=[];extra.append_array(missing.barrier_boxes);extra.append_array(missing.pickup_support_boxes)
			check(not Validation.validate_furnishing(missing,Geometry.create(missing,true,extra)).is_empty(),"reject bedroom missing real bed")
	var file:=FileAccess.open("res://../work/modeler092-furnishing-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"cases":cases},"\t"));file.close()
	print("MODELER092_FURNISHING checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)

func _door_sweeps(data: Dictionary) -> void:
	for door: Dictionary in data.doors.values():
		for angle_index: int in range(19):
			var transform:=DoorGeometry.leaf_transform(door,float(angle_index)*PI/36)
			for radius: float in [.02,1.0,1.99]:
				var point:=transform*Vector3(radius,1,0)
				var box:=AABB(point-Vector3.ONE*.04,Vector3.ONE*.08)
				check(Validation.door_sweep_intersects(door,box),"full sweep contains hinge/middle/tip at sampled angle")
		var middle:=DoorGeometry.leaf_transform(door,PI*.25)*Vector3(1.8,1,0)
		var obstruction:=AABB(middle-Vector3.ONE*.05,Vector3.ONE*.1)
		check(not DoorGeometry.intersects_body(door,0,obstruction) and not DoorGeometry.intersects_body(door,PI*.5,obstruction),"intermediate obstruction leaves endpoints clear")
		check(DoorGeometry.intersects_body(door,PI*.25,obstruction) and Validation.door_sweep_intersects(door,obstruction),"detect obstruction during actual opening")
		var far:=AABB(Vector3(door.hinge)+Vector3(6,0,6),Vector3.ONE)
		check(not Validation.door_sweep_intersects(door,far),"full sweep excludes distant furniture")
