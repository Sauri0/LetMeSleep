extends SceneTree
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
var checks:=0
var failures: Array[String]=[]

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("FAIL "+label)

func _initialize() -> void:
	_composite_specs()
	_bed_groups()
	_headboards()
	_task_contract()
	var file:=FileAccess.open("res://../work/modeler092-contract-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures},"\t"));file.close()
	print("MODELER092_CONTRACT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)

func _composite_specs() -> void:
	var generator:=Generator.new()
	var data:=generator.generate_structure(1)
	var found:=false
	for room: Dictionary in data.rooms:
		if int(room.bed_count)>0:
			var stands:=0
			for spec: Dictionary in generator._furnishing_specs(room):
				if str(spec.asset_id)=="nightstand" and bool(spec.essential): stands+=1
			check(stands>=1,"each bedroom plan requests at least one essential nightstand")
		if not ("kitchen" in room.uses and "dining" in room.uses): continue
		found=true
		var specs:=generator._furnishing_specs(room)
		var tables:=0;var surfaces:=0
		for spec: Dictionary in specs:
			if str(spec.asset_id)=="table": tables+=1
			if bool(spec.pickup_surface): surfaces+=1
		check(specs.size()==7 and tables==1 and surfaces==1,"existing kitchen/dining deduplication keeps seven pieces and one auxiliary table")
	check(found,"fixture contains kitchen/dining")

func _bed_groups() -> void:
	var objects: Array[Dictionary]=[
		{"asset_id":"bed","box":AABB(Vector3.ZERO,Vector3(2,.75,1.2)),"rotation_y":0.0},
		{"asset_id":"bed","box":AABB(Vector3(0,0,2.5),Vector3(2,.75,1.2)),"rotation_y":0.0},
		{"asset_id":"nightstand","box":AABB(Vector3(.1,0,-.7),Vector3(.5,.6,.5)),"rotation_y":0.0}]
	check(Validation.bed_group_valid(objects),"parallel beds with 1.30m aisle and adjacent nightstand")
	var changed:=objects.duplicate(true)
	changed.pop_back()
	check(not Validation.bed_group_valid(changed),"completed bedroom group requires at least one nightstand")
	check(Validation.bed_group_valid(changed,false),"partial search state may await its nightstand")
	changed=objects.duplicate(true)
	changed[1].box=AABB(Vector3(0,0,1.4),Vector3(2,.75,1.2))
	check(not Validation.bed_group_valid(changed),"reject beds separated by only 20cm")
	changed=objects.duplicate(true)
	changed[1].box=AABB(Vector3(3.3,0,3),Vector3(2,.75,1.2))
	check(not Validation.bed_group_valid(changed),"diagonal corner distance is not a shared aisle")
	changed=objects.duplicate(true)
	changed[2].box=AABB(Vector3(.1,0,-2),Vector3(.5,.6,.5))
	check(not Validation.bed_group_valid(changed),"reject nightstand on distant wall")
	changed=objects.duplicate(true)
	changed.append({"asset_id":"dresser","box":AABB(Vector3(.1,0,1.3),Vector3(1,.8,.8)),"rotation_y":0.0})
	check(not Validation.bed_group_valid(changed),"reject furniture occupying the bed aisle")
	for yaw: float in [PI*.5,PI,-PI*.5]:
		var transform:=Transform3D(Basis(Vector3.UP,yaw),Vector3(10,3.2,-5))
		changed=objects.duplicate(true)
		for item: Dictionary in changed:
			item.box=transform*AABB(item.box);item.rotation_y=yaw
		check(Validation.bed_group_valid(changed),"bed/nightstand relations survive cardinal rotation and translation")

func _headboards() -> void:
	var inside:=AABB(Vector3.ZERO,Vector3(8,3,8))
	var bed:=AABB(Vector3(.2,0,3),Vector3(2,.75,1.2))
	for yaw: float in [0.0,PI*.5,PI,-PI*.5]:
		var transform:=Transform3D(Basis(Vector3.UP,yaw),Vector3(12,3.2,-4))
		var rotated: AABB=transform*bed
		var room: AABB=transform*inside
		check(Validation.bed_headboard_on_wall(rotated,yaw,room),"headboard matches each cardinal wall")
		check(not Validation.bed_headboard_on_wall(rotated,yaw+PI*.5,room),"same box with side facing wall is rejected")
		check(not Validation.bed_headboard_on_wall(rotated,yaw+PI,room),"same box with foot facing wall is rejected")
	check(not Validation.bed_headboard_on_wall(AABB(Vector3(3,0,3),bed.size),0.0,inside),"reject freestanding headboard")

func _task_contract() -> void:
	var data: Dictionary={"half_z":11.0,"rooms":[],"stations":[],"structures":[]}
	var labels: Array[String]=["VENTANA","VENTILADOR","REPELENTE","EQUIPO","MANTAS","MOSQUITERO","SÁBANAS","VAJILLA"]
	for index: int in range(labels.size()):
		var floor_index:=0 if index<4 else 1
		var bounds:=AABB(Vector3(0,floor_index*3.2,6.55),Vector3(5,3.2,4.2))
		var point:=Vector3(2.5,floor_index*3.2,8.65)
		var id: String="fixture-%d"%index
		var box:=AABB(Vector3(1.5,floor_index*3.2,9.37),Vector3(1.45,.72,.85))
		var display:=Vector3(box.get_center().x,box.end.y,box.get_center().z)
		var surface: Dictionary={"approach":point,"box":box,"structure_id":"support-%d"%index,"task_display_p":display,"task_display_yaw":0.0}
		data.rooms.append({"id":id,"floor":floor_index,"bounds":bounds,"pickup_surface":surface})
		data.stations.append({"room":id,"label":labels[index],"p":point,"display_p":display,"display_yaw":0.0})
		data.structures.append({"id":surface.structure_id,"room":id,"kind":"furniture","asset_id":"table","box":box,"pickup_surface":true})
		if labels[index] in ["MANTAS","SÁBANAS"]: data.structures.append({"room":id,"asset_id":"bed"})
	check(Validation.validate_tasks(data).is_empty(),"eight distinct task rooms on two floors with matching objects")
	var changed:=data.duplicate(true)
	changed.stations[1].room=changed.stations[0].room
	check(_contains(Validation.validate_tasks(changed),"Tasks share room"),"reject duplicate task room")
	changed=data.duplicate(true)
	for room: Dictionary in changed.rooms: room.floor=0
	check(_contains(Validation.validate_tasks(changed),"at least two floors"),"reject tasks on one floor")
	changed=data.duplicate(true)
	changed.rooms[0].bounds=AABB(Vector3.ZERO,Vector3(5,3.2,4.2))
	changed.stations[0].p=Vector3(2.5,0,2.1)
	check(_contains(Validation.validate_tasks(changed),"no exterior window"),"reject window task in interior room")
	changed=data.duplicate(true);changed.structures.clear()
	check(_contains(Validation.validate_tasks(changed),"no real bed"),"preserve real-bed requirement")
	changed=data.duplicate(true);changed.rooms[1].pickup_surface.clear()
	check(_contains(Validation.validate_tasks(changed),"support approach"),"reject tabletop task without a support")
	changed=data.duplicate(true);changed.stations[1].erase("display_p")
	check(_contains(Validation.validate_tasks(changed),"display pose"),"reject missing task display position")
	changed=data.duplicate(true);changed.stations[1].display_p=Vector3.ZERO
	check(_contains(Validation.validate_tasks(changed),"display pose"),"reject corrupt task display position")
	changed=data.duplicate(true);changed.stations[1].display_yaw=PI
	check(_contains(Validation.validate_tasks(changed),"display pose"),"reject corrupt task display yaw")
	changed=data.duplicate(true);changed.stations[1].display_yaw=NAN
	check(_contains(Validation.validate_tasks(changed),"display pose"),"reject nonfinite task display yaw")
	changed=data.duplicate(true);changed.stations[1].display_p=Vector3(INF,0,0)
	check(_contains(Validation.validate_tasks(changed),"display pose"),"reject nonfinite task display point")
	changed=data.duplicate(true)
	changed.rooms[1].pickup_surface.task_display_p=Vector3(100,0,100)
	changed.stations[1].display_p=Vector3(100,0,100)
	check(_contains(Validation.validate_tasks(changed),"display pose"),"matching metadata outside support is rejected")
	changed=data.duplicate(true)
	changed.rooms[1].pickup_surface.structure_id="missing-support"
	check(_contains(Validation.validate_tasks(changed),"real support"),"metadata must refer to a real furniture support")

func _contains(errors: Array[String], fragment: String) -> bool:
	for error: String in errors:
		if fragment in error: return true
	return false
