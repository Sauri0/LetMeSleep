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
	if int(data.get("generator_version",0))>=2:
		errors.append_array(validate_circulation(data,human,extra))
	return {"passed":errors.is_empty(),"errors":errors,"valid_edges":valid_edges,"rejected_edges":rejected,
		"reachable_nodes":seen.size(),"node_count":nodes.size(),"rooms":data.rooms.size(),"floors":data.floor_levels.size()}

static func validate_circulation(data: Dictionary, human: Dictionary, extra: Array[AABB]) -> Array[String]:
	var errors: Array[String]=[]
	var obstacles: Array=data.obstacles.duplicate()
	obstacles.append_array(extra)
	var sides_per_floor: Dictionary={}
	for route: Dictionary in data.get("circulation_routes",[]):
		var label: String=route.id
		var a: Vector3=route.from
		var b: Vector3=route.to
		var width: float=route.clear_width
		var clearance: AABB=route.clearance
		if not a.is_finite() or not b.is_finite() or not is_finite(width) or width<1.50:
			errors.append("Invalid circulation width/position: "+label);continue
		var longitudinal_z:=absf(b.z-a.z)>absf(b.x-a.x)
		var across:=Vector3(width*.5,0,0) if longitudinal_z else Vector3(0,0,width*.5)
		var expected:=AABB(a.min(b)-across+Vector3.UP*.01,(b-a).abs()+across*2+Vector3.UP*2.05)
		if not clearance.position.is_equal_approx(expected.position) or not clearance.size.is_equal_approx(expected.size):
			errors.append("Circulation clearance differs from declared width: "+label)
		if not Geometry._segment(a,b,human) or not Geometry._segment(b,a,human):
			errors.append("Blocked circulation route: "+label)
		_validate_clearance(expected,a.y,obstacles,data,errors,label)
		if str(route.kind)=="stair_side": sides_per_floor[route.floor]=int(sides_per_floor.get(route.floor,0))+1
	for floor_index: int in range(data.floor_levels.size()):
		if int(sides_per_floor.get(floor_index,0))!=4: errors.append("Missing four stair-side routes on floor %d"%floor_index)
	if data.stair_connections.size()!=2*(data.floor_levels.size()-1): errors.append("Missing opposite staircase connections")
	for stair: Dictionary in data.stair_connections:
		if float(stair.width)<1.40 or float(stair.get("rise",INF))>.22 or float(stair.get("tread",0))<.28:
			errors.append("Invalid stair dimensions: "+str(stair.id))
		for end_name: String in ["bottom","top"]:
			if not stair.has(end_name+"_landing"):
				errors.append("Missing stair landing: "+str(stair.id));continue
			var landing: AABB=stair[end_name+"_landing"]
			if minf(landing.size.x,landing.size.z)<1.50 or landing.size.y<Geometry.HUMAN_HEIGHT:
				errors.append("Undersized stair landing: "+str(stair.id))
			var clear:=landing
			clear.position.y+=.01;clear.size.y-=.01
			_validate_clearance(clear,landing.position.y,obstacles,data,errors,str(stair.id)+"/"+end_name)
	var anchors: Array=data.get("stair_light_anchors",[])
	if anchors.size()!=data.stair_connections.size(): errors.append("Missing light anchor per stair flight")
	if data.rooms.size()+data.floor_levels.size()*2+anchors.size()>32: errors.append("House exceeds 32-light environment budget")
	for anchor: Dictionary in anchors:
		var p: Vector3=anchor.p;var target: Vector3=anchor.target
		if not p.is_finite() or not target.is_finite() or p.distance_to(target)<.25 or not is_finite(float(anchor.range)) or float(anchor.range)>7.5:
			errors.append("Invalid stair light anchor")
	return errors

static func _validate_clearance(clearance: AABB, floor_y: float, obstacles: Array, data: Dictionary, errors: Array[String], label: String) -> void:
	for obstacle: AABB in obstacles:
		if clearance.intersects(obstacle):
			errors.append("Obstructed circulation volume: %s (overlap %s)"%[label,clearance.intersection(obstacle).size]);break
	for door: Dictionary in data.doors.values():
		if DoorGeometry.intersects_body(door,PI*.5,clearance):
			errors.append("Open door intrudes into circulation volume: "+label);break
	if not _floor_covers(clearance,floor_y,data.floors): errors.append("Unsupported circulation area: "+label)

static func _floor_covers(area: AABB, floor_y: float, floors: Array) -> bool:
	# Subtract the union of actual supporting slab footprints. A centre-line
	# support test alone can accept a lane whose edge hangs over a stair hole.
	var remaining: Array[Rect2]=[Rect2(Vector2(area.position.x,area.position.z),Vector2(area.size.x,area.size.z))]
	for floor_box: AABB in floors:
		if absf(floor_box.end.y-floor_y)>.002: continue
		var footprint:=Rect2(Vector2(floor_box.position.x,floor_box.position.z),Vector2(floor_box.size.x,floor_box.size.z))
		var next: Array[Rect2]=[]
		for part: Rect2 in remaining:
			var overlap:=part.intersection(footprint)
			if not overlap.has_area(): next.append(part);continue
			for rect: Rect2 in [
				Rect2(part.position,Vector2(overlap.position.x-part.position.x,part.size.y)),
				Rect2(Vector2(overlap.end.x,part.position.y),Vector2(part.end.x-overlap.end.x,part.size.y)),
				Rect2(Vector2(overlap.position.x,part.position.y),Vector2(overlap.size.x,overlap.position.y-part.position.y)),
				Rect2(Vector2(overlap.position.x,overlap.end.y),Vector2(overlap.size.x,part.end.y-overlap.end.y))]:
				if rect.size.x>.001 and rect.size.y>.001: next.append(rect)
		remaining=next
		if remaining.is_empty(): return true
	return remaining.is_empty()

static func fingerprint(data: Dictionary) -> String:
	var source: Dictionary={}
	for key: String in ["generator_version","seed","bounds","floor_levels","structures","obstacles","barrier_boxes","pickup_supports",
		"rooms","doors","stations","pickups","human_spawns","mosquito_spawns","respawn_points","nav_nodes","nav_edges"]:
		source[key]=data.get(key)
	if int(data.get("generator_version",0))>=2:
		for key: String in ["layout_dimensions","stair_connections","circulation_routes","stair_light_anchors"]: source[key]=data.get(key)
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
