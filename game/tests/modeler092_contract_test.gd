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
		{"asset_id":"bed","box":AABB(Vector3(3.3,0,0),Vector3(2,.75,1.2)),"rotation_y":0.0},
		{"asset_id":"nightstand","box":AABB(Vector3(-.7,0,.1),Vector3(.5,.6,.5)),"rotation_y":0.0}]
	check(Validation.bed_group_valid(objects),"parallel beds with 1.30m aisle and adjacent nightstand")
	var changed:=objects.duplicate(true)
	changed[1].box=AABB(Vector3(2.2,0,0),Vector3(2,.75,1.2))
	check(not Validation.bed_group_valid(changed),"reject beds separated by only 20cm")
	changed=objects.duplicate(true)
	changed[1].box=AABB(Vector3(3.3,0,3),Vector3(2,.75,1.2))
	check(not Validation.bed_group_valid(changed),"diagonal corner distance is not a shared aisle")
	changed=objects.duplicate(true)
	changed[2].box=AABB(Vector3(-2,0,.1),Vector3(.5,.6,.5))
	check(not Validation.bed_group_valid(changed),"reject nightstand on distant wall")
	changed=objects.duplicate(true)
	changed.append({"asset_id":"dresser","box":AABB(Vector3(2.1,0,.1),Vector3(1,.8,.8)),"rotation_y":0.0})
	check(not Validation.bed_group_valid(changed),"reject furniture occupying the bed aisle")
	for yaw: float in [PI*.5,PI,-PI*.5]:
		var transform:=Transform3D(Basis(Vector3.UP,yaw),Vector3(10,3.2,-5))
		changed=objects.duplicate(true)
		for item: Dictionary in changed:
			item.box=transform*AABB(item.box);item.rotation_y=yaw
		check(Validation.bed_group_valid(changed),"bed/nightstand relations survive cardinal rotation and translation")

func _task_contract() -> void:
	var data: Dictionary={"half_z":11.0,"rooms":[],"stations":[],"structures":[]}
	var labels: Array[String]=["VENTANA","VENTILADOR","REPELENTE","EQUIPO","MANTAS","MOSQUITERO","SÁBANAS","VAJILLA"]
	for index: int in range(labels.size()):
		var floor_index:=0 if index<4 else 1
		var bounds:=AABB(Vector3(0,floor_index*3.2,6.55),Vector3(5,3.2,4.2))
		var point:=Vector3(2.5,floor_index*3.2,8.65)
		var id: String="fixture-%d"%index
		data.rooms.append({"id":id,"floor":floor_index,"bounds":bounds,"pickup_surface":{"approach":point}})
		data.stations.append({"room":id,"label":labels[index],"p":point})
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

func _contains(errors: Array[String], fragment: String) -> bool:
	for error: String in errors:
		if fragment in error: return true
	return false
