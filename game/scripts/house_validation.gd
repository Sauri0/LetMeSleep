class_name HouseValidation
extends RefCounted
const DoorGeometry=preload("res://scripts/door_geometry.gd")
const Geometry = preload("res://scripts/navigation_geometry.gd")

static func validate(data: Dictionary) -> Dictionary:
	var errors: Array[String]=[]
	var extra: Array[AABB]=[]
	for key: String in ["barrier_boxes","pickup_support_boxes"]:
		for box: AABB in data.get(key,[]): extra.append(box)
	var human := Geometry.create(data,true,extra)
	var mosquito := Geometry.create(data,false,extra)
	var nodes: Array=data.nav_nodes
	var adjacency: Array=[]
	for index: int in range(nodes.size()): adjacency.append([])
	var valid_edges: Array[Vector2i]=[]
	var rejected: Array[Dictionary]=[]
	for edge: Vector2i in data.nav_edges:
		var a: Vector3=nodes[edge.x];var b: Vector3=nodes[edge.y]
		if Geometry._segment(a,b,human) and Geometry._segment(a+Vector3.UP*1.2,b+Vector3.UP*1.2,mosquito):
			valid_edges.append(edge);adjacency[edge.x].append(edge.y);adjacency[edge.y].append(edge.x)
		else: rejected.append({"edge":edge,"from":a,"to":b})
	var seen: Dictionary={}
	var queue: Array[int]=[]
	if not nodes.is_empty(): queue.append(0)
	while not queue.is_empty():
		var at: int=queue.pop_back()
		if seen.has(at): continue
		seen[at]=true
		for neighbor: int in adjacency[at]:
			if not seen.has(neighbor): queue.append(neighbor)
	if seen.size()!=nodes.size(): errors.append("Disconnected route graph: %d/%d reachable"%[seen.size(),nodes.size()])
	for index: int in range(nodes.size()):
		if not Geometry._fits(nodes[index],human): errors.append("Blocked human route node %d at %s"%[index,nodes[index]])
		if _open_leaf_blocks(nodes[index],true,data): errors.append("Open door blocks route node %d"%index)
	for room: Dictionary in data.rooms:
		if int(room.get("furniture_count",0))<3: errors.append("Room %s has fewer than three furnishings"%room.id)
	for key: String in ["human_spawns","mosquito_spawns","respawn_points"]:
		for point: Vector3 in data[key]:
			if _open_leaf_blocks(point,key=="human_spawns",data): errors.append("Open door blocks "+key+" at "+str(point))
			if not Geometry._fits(point,human if key=="human_spawns" else mosquito): errors.append("Blocked "+key+" at "+str(point))
	for pickup: Dictionary in data.pickups:
		if not Geometry._fits(pickup.approach,human): errors.append("Blocked pickup approach in "+str(pickup.room))
	if data.human_spawns.size()<5 or data.mosquito_spawns.size()<12: errors.append("Insufficient roster spawn capacity")
	for h: Vector3 in data.human_spawns:
		for m: Vector3 in data.mosquito_spawns:
			if (h+Vector3.UP*1.4).distance_to(m)<3.0: errors.append("Opposing spawn is within immediate reach")
			var blocked:=false
			for obstacle: AABB in data.obstacles:
				if obstacle.intersects_segment(h+Vector3.UP*1.4,m)!=null: blocked=true;break
			if not blocked: errors.append("Opposing spawns have immediate line of sight")
	for key: String in ["human_spawns","mosquito_spawns"]:
		for first: int in range(data[key].size()):
			for second: int in range(first+1,data[key].size()):
				if Vector3(data[key][first]).distance_to(data[key][second])<(1.2 if key=="human_spawns" else .25): errors.append("Overlapping spawn in "+key)
	for task: Dictionary in data.stations:
		if not Geometry._fits(task.p,human): errors.append("Blocked task "+str(task.name))
		if str(task.label) in ["MANTAS","SÁBANAS"]:
			var has_bed:=false
			for item: Dictionary in data.structures:
				if str(item.get("room",""))==str(task.room) and str(item.get("asset_id",""))=="bed": has_bed=true;break
			if not has_bed: errors.append("Bedtime task has no real bed: "+str(task.name))
	for floor_y: float in data.floor_levels:
		for tool: String in ["swatter","racket","newspaper","broom","slipper"]:
			var found:=false
			for pickup: Dictionary in data.pickups:
				if str(pickup.tool)==tool and absf(Vector3(pickup.approach).y-floor_y)<.001: found=true;break
			if not found: errors.append("Missing %s on floor %.1f"%[tool,floor_y])
	if data.rooms.size()<16: errors.append("House is smaller than sixteen rooms")
	if data.stations.size()<8: errors.append("Missing bedtime stations")
	return {"passed":errors.is_empty(),"errors":errors,"valid_edges":valid_edges,"rejected_edges":rejected,
		"reachable_nodes":seen.size(),"node_count":nodes.size(),"rooms":data.rooms.size(),"floors":data.floor_levels.size()}

static func fingerprint(data: Dictionary) -> String:
	var source: Dictionary={}
	for key: String in ["generator_version","seed","bounds","floor_levels","structures","obstacles","barrier_boxes","pickup_supports",
		"rooms","doors","stations","pickups","human_spawns","mosquito_spawns","respawn_points","nav_nodes","nav_edges"]:
		source[key]=data.get(key)
	return JSON.stringify(_canonical(source)).sha256_text()

static func _canonical(value: Variant) -> Variant:
	if value is Dictionary:
		var result: Dictionary={}
		var keys: Array=value.keys();keys.sort()
		for key: Variant in keys: result[str(key)]=_canonical(value[key])
		return result
	if value is Array:
		var result: Array=[]
		for item: Variant in value: result.append(_canonical(item))
		return result
	if value is Vector3: return ["v3",roundi(value.x*100000),roundi(value.y*100000),roundi(value.z*100000)]
	if value is Vector2i: return ["v2i",value.x,value.y]
	if value is AABB: return ["box",_canonical(value.position),_canonical(value.size)]
	if value is Color: return ["color",value.to_html()]
	if value is float: return ["f",roundi(value*100000)]
	return value

static func _open_leaf_blocks(point: Vector3, human: bool, data: Dictionary) -> bool:
	var radius: float=Geometry.HUMAN_RADIUS if human else Geometry.MOSQUITO_RADIUS
	var body:=AABB(point+Vector3(-radius,.003 if human else -radius,-radius),Vector3(radius*2,Geometry.HUMAN_HEIGHT-.006 if human else radius*2,radius*2))
	for definition: Dictionary in data.doors.values():
		if DoorGeometry.intersects_body(definition,PI*.5,body): return true
	return false
