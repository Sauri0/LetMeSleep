class_name HouseValidation
extends RefCounted
const DoorGeometry=preload("res://scripts/door_geometry.gd")
const Geometry = preload("res://scripts/navigation_geometry.gd")
const Furniture=preload("res://scripts/furniture_blueprint.gd")

static func door_sweep_intersects(definition: Dictionary, body: AABB) -> bool:
	var hinge: Vector3=definition.hinge
	if body.end.y<=hinge.y+DoorGeometry.GAP or body.position.y>=hinge.y+float(definition.height): return false
	# Authored doors rotate by 90 degrees from a cardinal axis. Their complete
	# horizontal sweep is a quarter disk; padding covers thickness and rounding.
	var local: AABB=DoorGeometry.leaf_transform(definition,0).affine_inverse()*body
	var positive_z: bool=float(definition.open_sign)<0
	var near_z:=local.position.z if positive_z else -local.end.z
	var far_z:=local.end.z if positive_z else -local.position.z
	var padding:=float(definition.thickness)*.5+.01
	if local.end.x< -padding or far_z< -padding: return false
	var x:=maxf(0,local.position.x-padding)
	var z:=maxf(0,near_z-padding)
	return x*x+z*z<pow(float(definition.width)+padding,2)

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
	if int(data.get("generator_version",0))>=3:
		errors.append_array(validate_zoning(data))
		errors.append_array(validate_furnishing(data,human))
		errors.append_array(validate_tasks(data))
	return {"passed":errors.is_empty(),"errors":errors,"valid_edges":valid_edges,"rejected_edges":rejected,
		"reachable_nodes":seen.size(),"node_count":nodes.size(),"rooms":data.rooms.size(),"floors":data.floor_levels.size()}

static func room_interior(data: Dictionary, bounds: AABB) -> AABB:
	var inside:=bounds
	for axis: int in [0,2]:
		var limit: float=float(data.half_x if axis==0 else data.half_z)-.25
		var first:=0.0 if absf(absf(bounds.position[axis])-limit)<.001 else .1
		var last:=0.0 if absf(absf(bounds.end[axis])-limit)<.001 else .1
		inside.position[axis]+=first;inside.size[axis]-=first+last
	inside.size.y=3.0
	return inside

static func validate_furnishing(data: Dictionary, human: Dictionary) -> Array[String]:
	var errors: Array[String]=[]
	var orders: Dictionary={}
	var total:=0
	for room: Dictionary in data.rooms:
		var objects: Array[Dictionary]=[]
		var counts: Dictionary={}
		var zone_ids: Dictionary={}
		for zone: Dictionary in room.functional_zones: zone_ids[zone.id]=true
		var inside:=room_interior(data,room.bounds)
		var occupied:=0.0
		for item: Dictionary in data.structures:
			if str(item.kind)!="furniture" or str(item.get("room",""))!=str(room.id): continue
			objects.append(item);total+=1
			var box: AABB=item.box
			occupied+=box.size.x*box.size.z
			var asset: String=item.get("asset_id","")
			counts[asset]=int(counts.get(asset,0))+1
			if not inside.grow(.001).encloses(box): errors.append("Furniture outside useful room: "+str(item.id))
			if str(item.get("room_id",""))!=str(room.id) or not zone_ids.has(item.get("functional_zone_id","")) or not item.has("essential"):
				errors.append("Missing furnishing presentation metadata: "+str(item.id))
			var order: int=item.get("placement_order",-1)
			if order<0 or orders.has(order): errors.append("Invalid furnishing placement order")
			orders[order]=true
			if door_sweep_intersects(data.doors[room.id],box): errors.append("Furniture intrudes into opening sweep: "+str(item.id))
			for lane: AABB in room.get("movement_clearance",[]):
				if box.intersects(lane): errors.append("Furniture blocks functional route: "+str(item.id));break
		for first: int in range(objects.size()):
			for second: int in range(first+1,objects.size()):
				if AABB(objects[first].box).intersects(objects[second].box): errors.append("Furniture overlap in "+str(room.id))
		var expected: Dictionary={}
		for index: int in range(room.uses.size()):
			var use: String=room.uses[index]
			for spec: Dictionary in Furniture.for_theme(use):
				if bool(spec.pickup_surface) and (use=="bathroom" or index>0): continue
				expected[spec.asset_id]=int(expected.get(spec.asset_id,0))+1
		if int(room.bed_count)>1:
			expected.bed=int(room.bed_count)
			var storage: String="wardrobe" if room.theme_id=="bedroom_blue" else "dresser"
			expected[storage]=int(expected.get(storage,0))+1
		for asset: String in expected:
			if int(counts.get(asset,0))<int(expected[asset]): errors.append("Missing essential %s in %s"%[asset,room.id])
		if int(room.bed_count)>0 and not bed_group_valid(objects): errors.append("Invalid bed group spacing in "+str(room.id))
		if objects.size()!=int(room.get("furniture_count",-1)) or objects.size()>8: errors.append("Invalid furnishing count in "+str(room.id))
		if occupied/(inside.size.x*inside.size.z)>.40: errors.append("Overcrowded room: "+str(room.id))
		for approach: Dictionary in room.get("functional_approaches",[]):
			if not Geometry._segment(approach.origin,approach.p,human) or _open_leaf_blocks(approach.p,true,data):
				errors.append("Inaccessible furnishing: "+str(approach.structure_id))
	if total>144: errors.append("House exceeds furnishing budget")
	if total!=int(data.get("furniture_count",-1)): errors.append("Incorrect map furnishing count")
	for index: int in range(total):
		if not orders.has(index): errors.append("Non-contiguous furnishing placement order");break
	return errors

static func bed_aisle(first: AABB, second: AABB) -> AABB:
	# Require a straight, overlapping passage between bed faces, not just a
	# diagonal distance between corners. The passage remains clear of furniture.
	for axis: int in [0,2]:
		var along:=2 if axis==0 else 0
		var left:=first if first.get_center()[axis]<second.get_center()[axis] else second
		var right:=second if first.get_center()[axis]<second.get_center()[axis] else first
		var gap: float=right.position[axis]-left.end[axis]
		var low: float=maxf(first.position[along],second.position[along])
		var high: float=minf(first.end[along],second.end[along])
		if gap<1.30-.001 or high-low<minf(first.size[along],second.size[along])*.5: continue
		var origin:=Vector3(0,first.position.y,0);origin[axis]=left.end[axis];origin[along]=low
		var size:=Vector3(0,2.05,0);size[axis]=gap;size[along]=high-low
		return AABB(origin,size)
	return AABB()

static func nightstand_beside_bed(stand: AABB, bed: AABB, yaw: float) -> bool:
	var frame:=Transform3D(Basis(Vector3.UP,yaw),bed.get_center()).affine_inverse()
	var local_bed: AABB=frame*bed
	var local_stand: AABB=frame*stand
	var side_gap:=maxf(local_stand.position.x-local_bed.end.x,local_bed.position.x-local_stand.end.x)
	var overlap:=minf(local_stand.end.z,local_bed.end.z)-maxf(local_stand.position.z,local_bed.position.z)
	return side_gap>=.10-.001 and side_gap<=.35+.001 and overlap>=minf(.25,local_stand.size.z*.5)

static func bed_group_valid(objects: Array[Dictionary]) -> bool:
	var beds: Array[Dictionary]=[]
	for item: Dictionary in objects:
		if str(item.asset_id)=="bed": beds.append(item)
	for first: int in range(beds.size()):
		for second: int in range(first+1,beds.size()):
			var aisle:=bed_aisle(beds[first].box,beds[second].box)
			if not aisle.has_volume(): return false
			for item: Dictionary in objects:
				if str(item.asset_id)!="bed" and aisle.intersects(item.box): return false
	for item: Dictionary in objects:
		if str(item.asset_id)!="nightstand": continue
		var beside:=false
		for bed: Dictionary in beds:
			if nightstand_beside_bed(item.box,bed.box,float(bed.rotation_y)): beside=true;break
		if not beside: return false
	return true

static func validate_tasks(data: Dictionary) -> Array[String]:
	var errors: Array[String]=[]
	var rooms: Dictionary={};var used: Dictionary={};var floors: Dictionary={};var labels: Dictionary={}
	for room: Dictionary in data.rooms: rooms[room.id]=room
	for task: Dictionary in data.stations:
		var id: String=task.get("room","")
		if not rooms.has(id): errors.append("Task references unknown room: "+id);continue
		if used.has(id): errors.append("Tasks share room: "+id)
		used[id]=true
		var room: Dictionary=rooms[id]
		floors[room.floor]=true
		var label: String=task.get("label","")
		if labels.has(label): errors.append("Duplicate bedtime task: "+label)
		labels[label]=true
		var bounds: AABB=room.bounds
		if not bounds.has_point(Vector3(task.p)+Vector3.UP*.1): errors.append("Task outside assigned room: "+label)
		if label in ["VENTANA","MOSQUITERO"] and absf(bounds.position.z+float(data.half_z)-.25)>.01 and absf(bounds.end.z-float(data.half_z)+.25)>.01:
			errors.append("Window task has no exterior window: "+label)
		if label in ["MANTAS","SÁBANAS"]:
			var bed:=false
			for item: Dictionary in data.structures:
				if str(item.get("room",""))==id and str(item.get("asset_id",""))=="bed": bed=true;break
			if not bed: errors.append("Bedtime task has no real bed: "+label)
		if label in ["VENTILADOR","REPELENTE","EQUIPO","VAJILLA"]:
			var surface: Dictionary=room.get("pickup_surface",{})
			if surface.is_empty() or not Vector3(task.p).is_equal_approx(surface.approach): errors.append("Task lacks assigned support approach: "+label)
	if data.stations.size()!=8 or used.size()!=8: errors.append("Expected eight distinct task rooms")
	if floors.size()<2: errors.append("Bedtime tasks need at least two floors")
	for label: String in ["VENTANA","VENTILADOR","REPELENTE","EQUIPO","MANTAS","MOSQUITERO","SÁBANAS","VAJILLA"]:
		if not labels.has(label): errors.append("Missing bedtime task: "+label)
	return errors

static func _portal_hall(data: Dictionary, room: Dictionary) -> Vector3:
	var point: Vector3=room.portal
	if int(room.door_axis)==0: point.x=0
	else:
		var corridor: AABB=data.corridors[int(room.floor)*3+(1 if point.z<0 else 2)]
		point.z=corridor.get_center().z
	return point

static func portal_distance(data: Dictionary, a: Dictionary, b: Dictionary) -> float:
	if a.floor!=b.floor: return INF
	var first:=_portal_hall(data,a);var last:=_portal_hall(data,b)
	return Vector3(a.portal).distance_to(first)+absf(first.x-last.x)+absf(first.z-last.z)+last.distance_to(b.portal)

static func validate_zoning(data: Dictionary) -> Array[String]:
	var errors: Array[String]=[]
	var floor_count: int=data.floor_levels.size()
	var ground: Dictionary={}
	for floor_index: int in range(floor_count):
		var count:=0;var bathrooms:=0;var bedrooms:=0
		var bath: Dictionary={}
		var sleep_rooms: Array[Dictionary]=[]
		for room: Dictionary in data.rooms:
			if int(room.floor)!=floor_index: continue
			count+=1
			var uses: Array=room.get("uses",[])
			if uses.is_empty() or uses.size()>2 or str(uses[0])!=str(room.get("theme_id","")):
				errors.append("Invalid room use contract: "+str(room.id));continue
			var inside:=room_interior(data,room.bounds)
			var area:=inside.size.x*inside.size.z
			if absf(area-float(room.get("area_m2",0)))>.005: errors.append("Incorrect useful room area: "+str(room.id))
			if str(room.get("zone",""))=="service":
				if inside.size.x<3.40-.005 or inside.size.z<4.0-.005 or area>20.0+.005:
					errors.append("Disproportionate service room: "+str(room.id))
			if "bathroom" in uses: bathrooms+=1;bath=room
			if str(room.get("zone",""))=="sleep":
				bedrooms+=1;sleep_rooms.append(room)
				if floor_index==0: errors.append("Primary bedroom on ground floor: "+str(room.id))
			if floor_index==0:
				for use: String in uses: ground[use]=room
			var regions: Array=room.get("functional_zones",[])
			if regions.size()!=uses.size(): errors.append("Missing functional zone: "+str(room.id))
			for region: Dictionary in regions:
				var bounds: AABB=region.bounds
				if not inside.grow(.001).encloses(bounds) or str(region.use) not in uses:
					errors.append("Invalid functional zone: "+str(room.id))
		if bathrooms!=1: errors.append("Expected one bathroom on floor %d"%floor_index)
		if floor_index==0 and count!=8: errors.append("Ground floor must contain eight rooms")
		if floor_index>0:
			if count<(8 if floor_count==2 else 7) or count>(10 if floor_count==2 else 7): errors.append("Invalid upper-floor room count")
			if bedrooms<2: errors.append("Missing upper-floor bedrooms")
			for bedroom: Dictionary in sleep_rooms:
				if bath.is_empty() or portal_distance(data,bath,bedroom)>30.0: errors.append("Bedroom has no nearby same-floor bathroom")
	for use: String in ["bathroom","laundry","pantry","kitchen","dining","entry","living_room"]:
		if not ground.has(use): errors.append("Missing ground-floor use: "+use)
	for relation: Array in [["pantry","kitchen",11.0],["laundry","pantry",12.5],["kitchen","living_room",12.0]]:
		if ground.has(relation[0]) and ground.has(relation[1]):
			if portal_distance(data,ground[relation[0]],ground[relation[1]])>float(relation[2]): errors.append("Domestic adjacency too far: "+str(relation[0])+"/"+str(relation[1]))
	if ground.has("kitchen") and ground.has("dining") and ground.kitchen.id!=ground.dining.id:
		errors.append("Kitchen and dining must share their connected room")
	return errors

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
