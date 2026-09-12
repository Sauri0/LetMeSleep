class_name ProjectileCollision
extends RefCounted
## Pure continuous capsule sweeps. Dynamic doors/actors are passed by each Sim.
const ArenaData=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const ToolsData=preload("res://scripts/tool_catalog.gd")
const EPSILON:=.000002

static func launch_basis(direction: Vector3) -> Basis:
	return ToolsData.launch_basis(direction)

static func resting_offset(tool: String, rotation: Vector3) -> float:
	var bounds: AABB=ToolsData.visual(tool).get("bounds",AABB())
	var basis:=Basis.from_euler(rotation)
	var low:=0.0
	for index: int in range(8):
		var point:=bounds.position+Vector3(bounds.size.x if index&1 else 0.0,bounds.size.y if index&2 else 0.0,bounds.size.z if index&4 else 0.0)
		low=minf(low,(basis*point).y)
	return .003-low

static func visual_bounds(tool: String, position: Vector3, rotation: Vector3) -> AABB:
	var source: AABB=ToolsData.visual(tool).get("bounds",AABB())
	var basis:=Basis.from_euler(rotation)
	var low:=Vector3(INF,INF,INF)
	var high:=-low
	for index: int in range(8):
		var corner:=source.position+Vector3(source.size.x if index&1 else 0.0,source.size.y if index&2 else 0.0,source.size.z if index&4 else 0.0)
		var point:=position+basis*corner
		low=low.min(point)
		high=high.max(point)
	return AABB(low,high-low)

static func visual_fits(tool: String, position: Vector3, rotation: Vector3, map_id: String, doors: Dictionary) -> bool:
	var body:=visual_bounds(tool,position,rotation).grow(-.0001)
	for obstacle: AABB in ArenaData._nearby(body,map_id):
		if body.intersects(obstacle):return false
	return not Doors.body_blocked(body,doors,map_id)

static func capsule(tool: String, position: Vector3, rotation: Vector3) -> Dictionary:
	var stats:=ToolsData.throw_stats(tool)
	var basis:=Basis.from_euler(rotation)
	return {"from":position+basis*Vector3(stats.get("capsule_from",Vector3.ZERO)),"to":position+basis*Vector3(stats.get("capsule_to",Vector3.ZERO)),"radius":float(stats.get("radius",.04))}

static func bounds(shape: Dictionary, displacement: Vector3=Vector3.ZERO) -> AABB:
	var low: Vector3=Vector3(shape.from).min(shape.to).min(Vector3(shape.from)+displacement).min(Vector3(shape.to)+displacement)
	var high: Vector3=Vector3(shape.from).max(shape.to).max(Vector3(shape.from)+displacement).max(Vector3(shape.to)+displacement)
	return AABB(low,high-low).grow(float(shape.radius)+EPSILON)

static func translated(shape: Dictionary, displacement: Vector3) -> Dictionary:
	return {"from":Vector3(shape.from)+displacement,"to":Vector3(shape.to)+displacement,"radius":shape.radius}

static func lerp_capsule(before: Dictionary, after: Dictionary, weight: float) -> Dictionary:
	return {"from":Vector3(before.from).lerp(after.from,weight),"to":Vector3(before.to).lerp(after.to,weight),"radius":after.radius}

static func sweep_capsules(a0: Dictionary, a1: Dictionary, b0: Dictionary, b1: Dictionary) -> Dictionary:
	var da: Vector3=Vector3(a1.from)-Vector3(a0.from)
	var ea: Vector3=Vector3(a1.to)-Vector3(a0.to)
	var db: Vector3=Vector3(b1.from)-Vector3(b0.from)
	var eb: Vector3=Vector3(b1.to)-Vector3(b0.to)
	var relative_speed: float=maxf((da-db).length(),maxf((da-eb).length(),maxf((ea-db).length(),(ea-eb).length())))
	var radius: float=float(a0.radius)+float(b0.radius)
	var fraction:=0.0
	for iteration: int in range(80):
		var pair:=Geometry3D.get_closest_points_between_segments(Vector3(a0.from)+da*fraction,Vector3(a0.to)+ea*fraction,Vector3(b0.from)+db*fraction,Vector3(b0.to)+eb*fraction)
		var delta: Vector3=Vector3(pair[0])-Vector3(pair[1])
		var distance:=delta.length()
		if distance<=radius+EPSILON:
			var normal:=delta.normalized() if distance>EPSILON else -(da-db).normalized()
			if normal.length_squared()<.5:normal=Vector3.UP
			return {"fraction":fraction,"normal":normal,"p":Vector3(pair[0])-normal*float(a0.radius)}
		if relative_speed<EPSILON:return {}
		fraction+=(distance-radius)/relative_speed
		if fraction>1.0:return {}
	return {}

## Exact closest points between a segment and an AABB: each interval between
## axis/face crossings has a quadratic squared distance with a closed minimum.
static func segment_box(from: Vector3, to: Vector3, box: AABB) -> Dictionary:
	var direction:=to-from
	var stops: Array[float]=[0.0,1.0]
	for axis: int in range(3):
		if absf(direction[axis])<.00000001:continue
		for edge: float in [box.position[axis],box.end[axis]]:
			var t: float=(edge-from[axis])/direction[axis]
			if t>0.0 and t<1.0:stops.append(t)
	stops.sort()
	var best:=INF
	var best_axis:=from
	var best_box:=from.clamp(box.position,box.end)
	for index: int in range(stops.size()-1):
		var midpoint: float=(stops[index]+stops[index+1])*.5
		var numerator:=0.0
		var denominator:=0.0
		for axis: int in range(3):
			var value: float=from[axis]+direction[axis]*midpoint
			if value>=box.position[axis] and value<=box.end[axis]:continue
			var edge: float=box.position[axis] if value<box.position[axis] else box.end[axis]
			numerator+=direction[axis]*(from[axis]-edge)
			denominator+=direction[axis]*direction[axis]
		var weight: float=clampf(-numerator/maxf(denominator,.000000001),stops[index],stops[index+1])
		var point:=from+direction*weight
		var on_box:=point.clamp(box.position,box.end)
		var distance:=point.distance_squared_to(on_box)
		if distance<best:
			best=distance
			best_axis=point
			best_box=on_box
	return {"axis":best_axis,"box":best_box,"distance":sqrt(best)}

static func sweep_box(shape: Dictionary, displacement: Vector3, box: AABB) -> Dictionary:
	var speed:=displacement.length()
	var fraction:=0.0
	for iteration: int in range(80):
		var nearest:=segment_box(Vector3(shape.from)+displacement*fraction,Vector3(shape.to)+displacement*fraction,box)
		var distance: float=nearest.distance
		if distance<=float(shape.radius)+EPSILON:
			var normal: Vector3=(Vector3(nearest.axis)-Vector3(nearest.box)).normalized()
			if normal.length_squared()<.5:normal=-displacement.normalized()
			if normal.length_squared()<.5:normal=Vector3.UP
			return {"fraction":fraction,"normal":normal,"p":nearest.box}
		if speed<EPSILON:return {}
		fraction+=(distance-float(shape.radius))/speed
		if fraction>1.0:return {}
	return {}

static func map_hit(shape: Dictionary, displacement: Vector3, map_id: String, doors: Dictionary) -> Dictionary:
	var first: Dictionary={}
	var candidates: Array[AABB]=ArenaData._nearby(bounds(shape,displacement),map_id)
	var lot: AABB=ArenaData.world_bounds(map_id)
	candidates.append(AABB(Vector3(lot.position.x,lot.end.y,lot.position.z),Vector3(lot.size.x,1,lot.size.z)))
	for box: AABB in candidates:
		var hit:=sweep_box(shape,displacement,box)
		if hit.is_empty() or (not first.is_empty() and float(first.fraction)<=float(hit.fraction)):continue
		hit.kind="map"
		hit.material="tile" if box.size.y<.3 or box.size.y>2.5 else "wood"
		first=hit
	var definitions: Dictionary=ArenaData._door_definitions(map_id) if not doors.is_empty() else {}
	for id: String in doors:
		if not definitions.has(id):continue
		var definition: Dictionary=definitions[id]
		var transform:=Doors.leaf_transform(definition,float(doors[id].angle))
		var inverse:=transform.affine_inverse()
		var local: Dictionary={"from":inverse*Vector3(shape.from),"to":inverse*Vector3(shape.to),"radius":shape.radius}
		var hit:=sweep_box(local,inverse.basis*displacement,Doors.leaf_box(definition))
		if hit.is_empty() or (not first.is_empty() and float(first.fraction)<=float(hit.fraction)):continue
		hit.p=transform*Vector3(hit.p)
		hit.normal=transform.basis*Vector3(hit.normal)
		hit.kind="door"
		hit.material="wood"
		first=hit
	return first
