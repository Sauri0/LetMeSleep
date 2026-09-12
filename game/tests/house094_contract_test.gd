extends SceneTree
const Fixed=preload("res://scripts/fixed_house.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Geometry=preload("res://scripts/navigation_geometry.gd")
const DoorGeometry=preload("res://scripts/door_geometry.gd")
const Checks=preload("res://scripts/house_validation.gd")
var checks:=0
var failures: Array[String]=[]
func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:failures.append(label);printerr("FAIL "+label)
func _initialize() -> void:
	var data:=Fixed.new().build()
	check(data.id=="house-patio-v1" and data.authored_version==1,"authored identity")
	check(not data.has("seed") and not data.has("generator_version"),"no procedural identity")
	check(data.fingerprint==Fixed.fingerprint(Fixed.new().build()),"repeatable full fingerprint")
	check(Maps.default_map_id()==data.id and Maps.playable_maps()==[{"id":data.id,"label":"Casa con patio"}],"single-map API")
	for id: String in ["house","house-v3-1","house-v2-1","house-patio-v2","unknown","lobby"]:check(not Maps.is_playable(id),"reject playable "+id)
	check(not Maps.get_map("lobby").is_empty() and Maps.get_map("house-v3-1").is_empty(),"lobby separate and seed unavailable")
	var extra: Array[AABB]=[];extra.assign(data.barrier_boxes);extra.append_array(data.pickup_support_boxes)
	for door: Dictionary in data.doors.values():extra.append(DoorGeometry.leaf_transform(door,PI*.5)*DoorGeometry.leaf_box(door))
	var human:=Geometry.create(data,true,extra)
	var mosquito:=Geometry.create(data,false,extra)
	check(data.floor_levels==[0.0,3.2] and data.stair_connections.size()==2,"two floors two stairs")
	check(data.stations.size()==8,"eight authored tasks")
	for p: Vector3 in data.human_spawns:check(Geometry._fits(p,human),"human spawn "+str(p))
	for p: Vector3 in data.mosquito_spawns:
		check(Geometry._fits(p,mosquito),"mosquito spawn "+str(p))
		if not Geometry._fits(p,mosquito):
			for i: int in range(mosquito.expanded.size()):
				if AABB(mosquito.expanded[i]).has_point(p):printerr("HIT "+str(mosquito.expanded[i]))
		for h: Vector3 in data.human_spawns:check(p.distance_to(h)>8,"spawn role separation")
	var targets: Array[Vector3]=[]
	for room: Dictionary in data.rooms:
		check(room.furniture_count>=3,"furniture count "+room.id)
		for a: Dictionary in room.functional_approaches:targets.append(a.p);check(Geometry._fits(a.p,human),"furniture approach "+a.structure_id+" "+str(a.p))
	for item: Dictionary in data.structures:
		if item.kind!="furniture":continue
		for other: Dictionary in data.structures:
			if other.id==item.id or other.box.end.y<=item.box.position.y+.003:continue
			check(not AABB(item.box).intersects(other.box),"furniture collision "+item.id+"/"+other.id)
		for door: Dictionary in data.doors.values():check(not Checks.door_sweep_intersects(door,item.box),"full door sweep "+item.id+"/"+door.id)
	for station: Dictionary in data.stations:targets.append(station.p);check(Geometry._fits(station.p,human),"task "+station.id)
	for pickup: Dictionary in data.pickups:targets.append(pickup.approach);check(Geometry._fits(pickup.approach,human),"pickup "+pickup.tool+" "+pickup.room)
	var edges: Array=[]
	for p: Vector3 in data.nav_nodes:edges.append([]);check(Geometry._fits(p,human),"node fits "+str(p))
	for e: Vector2i in data.nav_edges:
		edges[e.x].append(e.y);edges[e.y].append(e.x)
		check(Geometry._segment(data.nav_nodes[e.x],data.nav_nodes[e.y],human),"graph forward")
		check(Geometry._segment(data.nav_nodes[e.y],data.nav_nodes[e.x],human),"graph reverse")
	var seen: Dictionary={0:true};var queue: Array[int]=[0]
	while not queue.is_empty():
		var i: int=queue.pop_front()
		for j: int in edges[i]:
			if not seen.has(j):seen[j]=true;queue.append(j)
	for i: int in range(data.nav_nodes.size()):check(seen.has(i),"connected node "+str(data.nav_nodes[i]))
	var changed:=data.duplicate(true);changed.exterior.paths[0].width+=.1
	check(Fixed.fingerprint(changed)!=data.fingerprint,"fingerprint covers exterior")
	var report:={"checks":checks,"failures":failures,"fingerprint":data.fingerprint,"rooms":data.rooms.size(),"furniture":data.furniture_count,"nodes":data.nav_nodes.size(),"edges":data.nav_edges.size(),"targets":targets.size()}
	var file:=FileAccess.open("res://../work/house094-contract-results.json",FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("HOUSE094_CONTRACT checks=%d failures=%d"%[checks,failures.size()]);quit(0 if failures.is_empty() else 1)
