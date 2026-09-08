class_name SurfaceLocomotion
extends RefCounted
## Per-simulation support identity; all geometry queries use the same immutable
## map ID and that room's current doors. The client never supplies a support.
const ArenaData = preload("res://scripts/arena.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const SPEED := .65
const ACCELERATION := 5.0
const BRAKING := 10.0
const OFFSET := .045
const ACQUIRE_DISTANCE := .32
const APPROACH_SPEED := 1.2
const RELEASE_DISTANCE := .035
const STEP_DISTANCE := .015
const STRIDE := .15
const EPS := .000001

var map_id := "house"
var _boxes: Array[AABB] = []
var _indices: Dictionary = {}
var _bounds := AABB()

func configure(id: String) -> void:
	map_id = id
	_boxes = ArenaData.obstacles(id)
	_indices.clear()
	for index: int in range(_boxes.size()):
		if not _indices.has(_boxes[index]): _indices[_boxes[index]] = index
	var data: Dictionary = ArenaData._map(id)
	_bounds = AABB(Vector3(-float(data.half_x),0,-float(data.half_z)),Vector3(float(data.half_x)*2,float(data.ceiling),float(data.half_z)*2))

func _face(index: int, axis: int, side: int) -> Dictionary:
	var box: AABB = _bounds if index == -1 else _boxes[index]
	var normal := Vector3.ZERO
	normal[axis] = float(-side if index == -1 else side)
	var low: Vector3 = box.position + Vector3.ONE*OFFSET if index == -1 else box.position-Vector3.ONE*OFFSET
	var high: Vector3 = box.end-Vector3.ONE*OFFSET if index == -1 else box.end+Vector3.ONE*OFFSET
	return {"id":"%s:%d:%d:%d"%[map_id,index,axis,side],"map_id":map_id,"index":index,"axis":axis,"side":side,"normal":normal,"plane":box.position[axis] if side<0 else box.end[axis],"low":low,"high":high}

func _valid_face(face: Dictionary) -> bool:
	return str(face.get("map_id","")) == map_id and int(face.get("index",-2)) >= -1 and int(face.get("index",-2)) < _boxes.size() and int(face.get("axis",-1)) in [0,1,2] and int(face.get("side",0)) in [-1,1]

func _project(position: Vector3, face: Dictionary) -> Vector3:
	var point: Vector3 = position.clamp(face.low,face.high)
	point[int(face.axis)] = float(face.plane)+Vector3(face.normal)[int(face.axis)]*OFFSET
	return point

func sweep_clear(from: Vector3, to: Vector3, doors: Dictionary) -> bool:
	if not from.is_finite() or not to.is_finite(): return false
	var steps: int = maxi(1,int(ceil(from.distance_to(to)/.01)))
	for index: int in range(steps+1):
		if not ArenaData.can_fit_mosquito(from.lerp(to,float(index)/float(steps)),map_id,doors): return false
	return true

func find_support(position: Vector3, doors: Dictionary) -> Dictionary:
	var faces: Array[Dictionary] = []
	for axis: int in range(3):
		for side: int in [-1,1]: faces.append(_face(-1,axis,side))
	for box: AABB in ArenaData._nearby(AABB(position,Vector3.ZERO).grow(ACQUIRE_DISTANCE+OFFSET),map_id):
		for axis: int in range(3):
			for side: int in [-1,1]: faces.append(_face(int(_indices[box]),axis,side))
	var best: Dictionary = {}
	var distance := ACQUIRE_DISTANCE
	for face: Dictionary in faces:
		var point := _project(position,face)
		var gap := position.distance_to(point)
		if gap > distance or (position-point).dot(face.normal)<-EPS: continue
		if not sweep_clear(position,point,doors): continue
		if not Doors.ray_doors(position,point,doors,map_id,ArenaData.MOSQUITO_RADIUS).is_empty(): continue
		if not best.is_empty() and is_equal_approx(gap,distance) and str(face.id)>str(best.id): continue
		best = face
		best.point = point
		distance = gap
	return best

static func initial_forward(normal: Vector3, yaw: float, pitch: float) -> Vector3:
	var forward := ArenaData.flight_direction(Vector3.FORWARD,yaw,pitch).slide(normal)
	if forward.length_squared()<.02:
		forward = Vector3.UP.rotated(Vector3.RIGHT,pitch).rotated(Vector3.UP,yaw).slide(normal)
	if forward.length_squared()<.001: forward = Vector3.RIGHT.slide(normal)
	return forward.normalized()

func begin(actor: Dictionary, doors: Dictionary) -> bool:
	var support := find_support(actor.p,doors)
	if support.is_empty():
		actor._surface_reason = "Acercate a una superficie"
		return false
	actor._surface_pending = support
	actor.velocity = Vector3.ZERO
	actor._surface_reason = "Acercándose a la superficie"
	return true

func clear(actor: Dictionary) -> void:
	actor._surface = {}
	actor._surface_pending = {}
	actor._surface_normal = Vector3.ZERO
	actor._surface_forward = Vector3.ZERO
	actor._surface_jump = false
	actor._surface_reason = ""
	actor.motion_speed = 0.0
	actor.grounded = false

func approach(actor: Dictionary, dt: float, doors: Dictionary) -> void:
	var support: Dictionary = actor.get("_surface_pending",{})
	if not _valid_face(support): clear(actor); return
	var previous: Vector3 = actor.p
	var target := _project(previous,support)
	if previous.distance_to(target)>ACQUIRE_DISTANCE+.01 or not sweep_clear(previous,target,doors):
		clear(actor)
		actor._surface_reason = "El apoyo quedó bloqueado"
		return
	actor.p = previous.move_toward(target,APPROACH_SPEED*dt)
	actor.velocity = (Vector3(actor.p)-previous)/maxf(dt,EPS)
	if Vector3(actor.p).distance_to(target)<=EPS:
		actor._surface = support
		actor._surface_pending = {}
		actor._surface_normal = support.normal
		actor._surface_forward = initial_forward(support.normal,float(actor.yaw),float(actor.pitch))
		actor._surface_view_yaw = actor.yaw
		actor._surface_reason = ""
		actor.state = "perched"
		actor.grounded = true
		actor.velocity = Vector3.ZERO

func supported(actor: Dictionary, doors: Dictionary) -> bool:
	var face: Dictionary = actor.get("_surface",{})
	if not _valid_face(face): return false
	if Vector3(actor.p).distance_to(_project(actor.p,face))>.0002: return false
	return ArenaData.can_fit_mosquito(actor.p,map_id,doors)

func can_release(actor: Dictionary, doors: Dictionary) -> bool:
	var normal: Vector3 = actor.get("_surface_normal",Vector3.ZERO)
	return normal.length_squared()>.9 and sweep_clear(actor.p,Vector3(actor.p)+normal*RELEASE_DISTANCE,doors)

func release(actor: Dictionary, doors: Dictionary) -> bool:
	if not can_release(actor,doors):
		actor._surface_reason = "No hay espacio para despegar"
		return false
	var normal: Vector3 = actor._surface_normal
	clear(actor)
	actor.state = "flying"
	actor.velocity = normal*.8
	actor._surface_reason = ""
	return true

# Segment versus the offset box, retaining the entering face. Tangential
# contact with a coplanar neighbour is not an entering collision.
func _entry(from: Vector3, displacement: Vector3, box: AABB) -> Dictionary:
	var first := 0.0
	var last := 1.0
	var axis_hit := -1
	var side_hit := 0
	for axis: int in range(3):
		if absf(displacement[axis])<EPS:
			if from[axis]<=box.position[axis]+EPS or from[axis]>=box.end[axis]-EPS: return {}
			continue
		var a: float = (box.position[axis]-from[axis])/displacement[axis]
		var b: float = (box.end[axis]-from[axis])/displacement[axis]
		var entering: float = minf(a,b)
		if entering>=first-EPS:
			first = maxf(0.0,entering)
			axis_hit = axis
			side_hit = -1 if displacement[axis]>0.0 else 1
		last = minf(last,maxf(a,b))
		if first>last+EPS: return {}
	if axis_hit<0 or first>1.0+EPS or last<0.0: return {}
	return {"fraction":clampf(first,0.0,1.0),"axis":axis_hit,"side":side_hit}

func _event(position: Vector3, displacement: Vector3, face: Dictionary, doors: Dictionary) -> Dictionary:
	var event: Dictionary = {}
	var nearest := 1.0+EPS
	# Reaching the end of the expanded face follows its adjacent face, with a
	# continuous centre path outside the existing conservative travel box.
	for axis: int in range(3):
		if axis==int(face.axis) or absf(displacement[axis])<EPS: continue
		var side: int = 1 if displacement[axis]>0 else -1
		var edge: float = Vector3(face.high)[axis] if side>0 else Vector3(face.low)[axis]
		var fraction: float = (edge-position[axis])/displacement[axis]
		if fraction>=-EPS and fraction<=nearest:
			nearest = maxf(0.0,fraction)
			event = {"fraction":nearest,"face":_face(int(face.index),axis,side),"kind":"face"}
	for box: AABB in ArenaData._nearby(ArenaData._segment_bounds(position,position+displacement,OFFSET),map_id):
		var index: int = _indices[box]
		if index==int(face.index): continue
		var hit := _entry(position,displacement,box.grow(OFFSET))
		if not hit.is_empty() and float(hit.fraction)<=nearest:
			nearest = hit.fraction
			event = {"fraction":nearest,"face":_face(index,int(hit.axis),int(hit.side)),"kind":"face"}
	# A furniture face can reach an outer room boundary before its own edge.
	for axis: int in range(3):
		if absf(displacement[axis])<EPS: continue
		var side: int = 1 if displacement[axis]>0 else -1
		var boundary := _face(-1,axis,side)
		var edge: float = float(boundary.plane)+Vector3(boundary.normal)[axis]*OFFSET
		var fraction: float = (edge-position[axis])/displacement[axis]
		if fraction>=-EPS and fraction<=nearest:
			nearest = maxf(0.0,fraction)
			event = {"fraction":nearest,"face":boundary,"kind":"face"}
	var door_hit := Doors.ray_doors(position,position+displacement,doors,map_id,OFFSET)
	if not door_hit.is_empty():
		var fraction: float = float(door_hit.distance)/maxf(displacement.length(),EPS)
		if fraction<=nearest: event = {"fraction":maxf(0.0,fraction-.0001),"kind":"blocked"}
	return event

func _part(actor: Dictionary, displacement: Vector3, doors: Dictionary) -> float:
	var remaining := displacement
	var travelled := 0.0
	for turn: int in range(4):
		if remaining.length_squared()<EPS*EPS: return travelled
		var face: Dictionary = actor._surface
		var previous: Vector3 = actor.p
		var event := _event(previous,remaining,face,doors)
		var fraction: float = float(event.fraction) if not event.is_empty() else 1.0
		var next := previous+remaining*fraction
		if not sweep_clear(previous,next,doors):
			actor.velocity = Vector3.ZERO
			actor._surface_reason = "Paso bloqueado"
			return travelled
		actor.p = next
		travelled += previous.distance_to(next)
		if event.is_empty(): return travelled
		if event.kind=="blocked":
			actor.velocity = Vector3.ZERO
			actor._surface_reason = "Paso bloqueado"
			return travelled
		var neighbour: Dictionary = event.face
		var old_normal: Vector3 = face.normal
		# Adjacent floor/wall modules share a plane. Cross the seam before
		# considering a turn around this particular box's exterior edge.
		if int(neighbour.index)==int(face.index) and int(neighbour.axis)!=int(face.axis):
			var probe := next+remaining.normalized()*.003
			for box: AABB in ArenaData._nearby(AABB(probe,Vector3.ZERO).grow(OFFSET+.01),map_id):
				var index: int = _indices[box]
				if index==int(face.index): continue
				var coplanar := _face(index,int(face.axis),int(face.side))
				if absf(float(coplanar.plane)-float(face.plane))<EPS and probe.distance_to(_project(probe,coplanar))<.0002:
					neighbour = coplanar
					break
		var new_normal: Vector3 = neighbour.normal
		if old_normal.dot(new_normal)<-.5 or next.distance_to(_project(next,neighbour))>.0002:
			actor.velocity = Vector3.ZERO
			actor._surface_reason = "Borde: F o Espacio para volar"
			return travelled
		var rotation := Basis(Quaternion(old_normal,new_normal))
		actor._surface = neighbour
		actor._surface_normal = new_normal
		actor._surface_forward = (rotation*Vector3(actor._surface_forward)).slide(new_normal).normalized()
		actor.velocity = rotation*Vector3(actor.velocity)
		remaining = rotation*remaining*(1.0-fraction)
	return travelled

func step_surface(actor: Dictionary, local_move: Vector3, dt: float, doors: Dictionary) -> void:
	if not is_finite(dt) or dt<=0.0 or not local_move.is_finite(): return
	if not supported(actor,doors):
		clear(actor)
		actor.state = "flying"
		actor.velocity = Vector3.ZERO
		actor._surface_reason = "El apoyo dejó de ser válido"
		return
	var normal: Vector3 = actor._surface_normal
	var yaw_delta: float = wrapf(float(actor.yaw)-float(actor.get("_surface_view_yaw",actor.yaw)),-PI,PI)
	actor._surface_view_yaw = actor.yaw
	var forward: Vector3 = Vector3(actor._surface_forward).rotated(normal,yaw_delta).slide(normal).normalized()
	actor._surface_forward = forward
	var intent := Vector2(local_move.x,local_move.z).limit_length(1.0)
	var target: Vector3 = (forward*-intent.y+forward.cross(normal)*intent.x)*SPEED
	actor.velocity = Vector3(actor.velocity).slide(normal).move_toward(target,(BRAKING if intent.length_squared()<.0001 else ACCELERATION)*dt)
	actor._surface_reason = ""
	var steps: int = maxi(1,int(ceil(Vector3(actor.velocity).length()*dt/STEP_DISTANCE)))
	var distance := 0.0
	for part: int in range(steps):
		distance += _part(actor,Vector3(actor.velocity)*dt/float(steps),doors)
	actor.motion_speed = distance/dt
	actor.motion_phase = fposmod(float(actor.get("motion_phase",0.0))+distance*TAU/STRIDE,TAU)
	# Velocity follows the final tangent after a corner; distance records the
	# travelled polyline rather than treating a corner's chord as the gait path.
	if distance<EPS: actor.velocity = Vector3.ZERO
	actor.grounded = true

func info(actor: Dictionary, doors: Dictionary) -> Dictionary:
	var attached: bool = actor.get("state","")=="perched"
	return {"attached":attached,"moving":attached and float(actor.get("motion_speed",0.0))>.01,"can_detach":attached and can_release(actor,doors),"approaching":not Dictionary(actor.get("_surface_pending",{})).is_empty(),"reason":str(actor.get("_surface_reason",""))}
