class_name Arena
extends RefCounted
const Pose = preload("res://scripts/human_pose.gd")

const Maps = preload("res://scripts/map_catalog.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const HouseBarriers = preload("res://scripts/house_barriers.gd")
const PickupSupports = preload("res://scripts/pickup_supports.gd")
# House aliases preserve existing consumers. New code selects data by map_id.
const HALF_X = Maps.HOUSE.half_x
const HALF_Z = Maps.HOUSE.half_z
const CEILING = Maps.HOUSE.ceiling
const OBSTACLES = Maps.HOUSE.obstacles
const STATIONS = Maps.HOUSE.stations
const HUMAN_SPEED := 3.1
const HUMAN_RUN_SPEED := 5.0
const HUMAN_CROUCH_SPEED := 1.55
const HUMAN_HEIGHT := 1.95
const HUMAN_CROUCH_HEIGHT := 1.40
const HUMAN_RADIUS := 0.60
const MOSQUITO_SPEED := 3.8
const MOSQUITO_RADIUS := 0.04
const FLIGHT_ACCELERATION := 13.0
const FLIGHT_BRAKING := 28.0
const STEP_HEIGHT := 0.22
const GRAVITY := 12.0
const JUMP_SPEED := 4.6
const MAX_FALL_SPEED := 16.0
const STUN_GRAVITY := 6.0
const STUN_FALL_SPEED := 6.0

static func _map(map_id: String) -> Dictionary:
	return Maps.LOBBY if map_id == "lobby" else Maps.HOUSE

# Only immutable map geometry is cached. Dynamic doors are supplied per call.
static var _obstacle_cache: Dictionary = {}
static var _spatial_cache: Dictionary = {}
const STATIC_CELL := 2.0

static func obstacles(map_id: String = "house") -> Array[AABB]:
	if not _obstacle_cache.has(map_id):
		var boxes: Array[AABB] = []
		for box: AABB in _map(map_id).obstacles:
			boxes.append(box)
		boxes.append_array(HouseBarriers.get_boxes(map_id))
		boxes.append_array(PickupSupports.get_boxes(map_id))
		boxes.make_read_only()
		_obstacle_cache[map_id] = boxes
	return _obstacle_cache[map_id]

# Broad phase over immutable architecture. Every returned candidate still uses
# the exact original AABB narrow phase; each simulation supplies its own doors.
static func _nearby(bounds: AABB, map_id: String) -> Array[AABB]:
	var boxes: Array[AABB]=obstacles(map_id)
	if not _spatial_cache.has(map_id):
		var cells: Dictionary={}
		for index: int in range(boxes.size()):
			var box: AABB=boxes[index]
			var low := Vector3i((box.position/STATIC_CELL).floor())
			var high := Vector3i((box.end/STATIC_CELL).floor())
			for x: int in range(low.x,high.x+1):
				for y: int in range(low.y,high.y+1):
					for z: int in range(low.z,high.z+1):
						var cell := Vector3i(x,y,z)
						if not cells.has(cell):cells[cell]=[]
						cells[cell].append(index)
		_spatial_cache[map_id]=cells
	if not bounds.position.is_finite() or not bounds.end.is_finite():
		return boxes
	var low := Vector3i((bounds.position/STATIC_CELL).floor())
	var high := Vector3i((bounds.end/STATIC_CELL).floor())
	if (high.x-low.x+1)*(high.y-low.y+1)*(high.z-low.z+1)>96:
		return boxes
	var seen: Dictionary={}
	var result: Array[AABB]=[]
	var grid: Dictionary=_spatial_cache[map_id]
	for x: int in range(low.x,high.x+1):
		for y: int in range(low.y,high.y+1):
			for z: int in range(low.z,high.z+1):
				for index: int in grid.get(Vector3i(x,y,z),[]):
					if not seen.has(index):
						seen[index]=true
						if boxes[index].intersects(bounds):result.append(boxes[index])
	return result

static func _segment_bounds(from: Vector3, to: Vector3, padding: float = 0.0) -> AABB:
	return AABB(from.min(to),(to-from).abs()).grow(maxf(padding,0.0)+.00001)

static func human_spawn(index: int, map_id: String = "house") -> Vector3:
	var data: Dictionary = _map(map_id)
	var points: Array = data.lobby_spawns if map_id == "lobby" else data.human_spawns
	return points[posmod(index, points.size())]

static func mosquito_spawn(index: int, map_id: String = "house") -> Vector3:
	var points: Array = _map(map_id).mosquito_spawns
	return points[posmod(index, points.size())] if not points.is_empty() else human_spawn(index, map_id) + Vector3.UP * 1.2

static func move_body(pos: Vector3, displacement: Vector3, human: bool, map_id: String = "house", height: float = HUMAN_HEIGHT, doors: Dictionary = {}) -> Vector3:
	if not pos.is_finite() or not displacement.is_finite():
		return pos if pos.is_finite() else Vector3.ZERO
	if human:
		return move_human(pos, displacement, map_id, height, doors).p
	var pieces: int = maxi(1, int(ceil(displacement.length() / 0.06)))
	var result: Vector3 = pos
	for part: int in range(pieces):
		result = _move_insect_part(result, displacement / pieces, map_id, doors)
	return result

static func _move_insect_part(pos: Vector3, displacement: Vector3, map_id: String, doors: Dictionary = {}) -> Vector3:
	var data: Dictionary = _map(map_id)
	var next: Vector3 = pos
	for axis: int in [0, 2, 1]:
		var trial: Vector3 = next
		trial[axis] += displacement[axis]
		trial.x = clampf(trial.x, -float(data.half_x) + MOSQUITO_RADIUS, float(data.half_x) - MOSQUITO_RADIUS)
		trial.z = clampf(trial.z, -float(data.half_z) + MOSQUITO_RADIUS, float(data.half_z) - MOSQUITO_RADIUS)
		trial.y = clampf(trial.y, MOSQUITO_RADIUS, float(data.ceiling) - MOSQUITO_RADIUS)
		var body := AABB(trial - Vector3.ONE * MOSQUITO_RADIUS, Vector3.ONE * MOSQUITO_RADIUS * 2)
		var blocked: bool = Doors.body_blocked(body, doors, map_id)
		for obstacle: AABB in _nearby(body,map_id):
			if body.intersects(obstacle):
				blocked = true
				break
		if not blocked:
			next = trial
	return next

static func flight_direction(local_move: Vector3, yaw: float, pitch: float) -> Vector3:
	if not local_move.is_finite() or not is_finite(yaw) or not is_finite(pitch):
		return Vector3.ZERO
	# W follows the full aiming direction; explicit ascend/descend stays vertical
	# in world space, so the player can still lift away while looking downward.
	var forward: Vector3 = Vector3.FORWARD.rotated(Vector3.RIGHT, pitch).rotated(Vector3.UP, yaw)
	var right: Vector3 = Vector3.RIGHT.rotated(Vector3.UP, yaw)
	return (right * local_move.x - forward * local_move.z + Vector3.UP * local_move.y).limit_length(1.0)

static func step_mosquito(actor: Dictionary, local_move: Vector3, dt: float, map_id: String = "house", assisted_velocity: Variant = null, doors: Dictionary = {}) -> void:
	if not is_finite(dt) or dt <= 0.0:
		return
	var target: Vector3 = flight_direction(local_move, float(actor.yaw), float(actor.pitch)) * MOSQUITO_SPEED
	if assisted_velocity is Vector3 and Vector3(assisted_velocity).is_finite():
		target = Vector3(assisted_velocity).limit_length(MOSQUITO_SPEED)
	var velocity: Vector3 = actor.get("velocity", Vector3.ZERO)
	var rate: float = FLIGHT_BRAKING if target.length_squared() < 0.0001 else FLIGHT_ACCELERATION
	velocity = velocity.move_toward(target, rate * minf(dt, 0.1))
	var previous: Vector3 = actor.p
	actor.p = move_body(previous, velocity * minf(dt, 0.1), false, map_id, HUMAN_HEIGHT, doors)
	actor.velocity = (Vector3(actor.p) - previous) / minf(dt, 0.1)

static func step_stunned(actor: Dictionary, dt: float, map_id: String = "house", doors: Dictionary = {}) -> void:
	if not is_finite(dt) or dt <= 0.0:
		return
	var part: float = minf(dt, 0.05)
	var position: Vector3 = actor.p
	var velocity: Vector3 = actor.get("velocity", Vector3.ZERO)
	velocity.x = 0.0
	velocity.z = 0.0
	velocity.y = maxf(-STUN_FALL_SPEED, minf(0.0, velocity.y) - STUN_GRAVITY * part)
	var support: float = floor_below(position, map_id, doors) + MOSQUITO_RADIUS
	var requested_y: float = maxf(support, position.y + velocity.y * part)
	actor.p = move_body(position, Vector3(0, requested_y - position.y, 0), false, map_id, HUMAN_HEIGHT, doors)
	var blocked: bool = float(actor.p.y) > requested_y + 0.00001
	actor.grounded = float(actor.p.y) <= support + 0.0001 or blocked
	if bool(actor.grounded):
		velocity.y = 0.0
	actor.velocity = velocity

# Conservative bound of every shared anatomical capsule, including the full
# arm extension during strikes. It is only a broad phase, never a hit volume.
static func human_envelope(actor: Dictionary) -> AABB:
	return AABB(Vector3(actor.get("p",Vector3.ZERO))+Vector3(-1.45,-.30,-1.45),Vector3(2.9,2.8,2.9))

static func _body_box(pos: Vector3, height: float) -> AABB:
	return AABB(pos + Vector3(-HUMAN_RADIUS, 0.003, -HUMAN_RADIUS), Vector3(HUMAN_RADIUS * 2, maxf(0.01, height - 0.006), HUMAN_RADIUS * 2))

static func can_fit_human(pos: Vector3, height: float = HUMAN_HEIGHT, map_id: String = "house", doors: Dictionary = {}) -> bool:
	var data: Dictionary = _map(map_id)
	if not pos.is_finite() or height < HUMAN_CROUCH_HEIGHT or height > HUMAN_HEIGHT:
		return false
	if absf(pos.x) + HUMAN_RADIUS > float(data.half_x) + 0.00001 or absf(pos.z) + HUMAN_RADIUS > float(data.half_z) + 0.00001 or pos.y < -0.00001 or pos.y + height > float(data.ceiling) + 0.00001:
		return false
	var body: AABB = _body_box(pos, height)
	for obstacle: AABB in _nearby(body,map_id):
		if body.intersects(obstacle):
			return false
	return not Doors.body_blocked(body, doors, map_id)

static func is_grounded(pos: Vector3, map_id: String = "house", doors: Dictionary = {}) -> bool:
	if pos.y <= 0.004:
		return true
	for obstacle: AABB in _nearby(AABB(pos+Vector3(-HUMAN_RADIUS,-.006,-HUMAN_RADIUS),Vector3(HUMAN_RADIUS*2,.012,HUMAN_RADIUS*2)),map_id):
		if absf(pos.y - obstacle.end.y) <= 0.005 and pos.x + HUMAN_RADIUS > obstacle.position.x and pos.x - HUMAN_RADIUS < obstacle.end.x and pos.z + HUMAN_RADIUS > obstacle.position.z and pos.z - HUMAN_RADIUS < obstacle.end.z:
			return true
	return Doors.body_blocked(AABB(pos+Vector3(-HUMAN_RADIUS,-0.005,-HUMAN_RADIUS),Vector3(HUMAN_RADIUS*2,0.006,HUMAN_RADIUS*2)),doors,map_id)

static func move_human(pos: Vector3, displacement: Vector3, map_id: String = "house", height: float = HUMAN_HEIGHT, doors: Dictionary = {}) -> Dictionary:
	if not pos.is_finite() or not displacement.is_finite():
		return {"p":pos,"grounded":false,"hit_ceiling":false}
	if displacement.length()>0.10:
		var count: int = int(ceil(displacement.length()/0.10))
		var result := {"p":pos,"grounded":false,"hit_ceiling":false}
		for index: int in range(count):
			result = move_human(result.p,displacement/float(count),map_id,height,doors)
		return result
	var data: Dictionary = _map(map_id)
	var next: Vector3 = pos
	var grounded := false
	var hit_ceiling := false
	# Sweep the vertical feet/top planes, allowing an exact landing on furniture.
	var desired_y: float = pos.y + displacement.y
	var ceiling_y: float = float(data.ceiling) - height
	if desired_y <= 0.0:
		desired_y = 0.0
		grounded = true
	if desired_y >= ceiling_y:
		desired_y = ceiling_y
		hit_ceiling = displacement.y > 0.0
	var swept_body: AABB=_body_box(pos,height).merge(_body_box(Vector3(pos.x,desired_y,pos.z),height)).grow(.006)
	for obstacle: AABB in _nearby(swept_body,map_id):
		var overlaps: bool = pos.x + HUMAN_RADIUS > obstacle.position.x and pos.x - HUMAN_RADIUS < obstacle.end.x and pos.z + HUMAN_RADIUS > obstacle.position.z and pos.z - HUMAN_RADIUS < obstacle.end.z
		if not overlaps:
			continue
		if displacement.y < 0.0 and pos.y >= obstacle.end.y - 0.004 and desired_y <= obstacle.end.y:
			desired_y = maxf(desired_y, obstacle.end.y)
			grounded = true
		elif displacement.y > 0.0 and pos.y + height <= obstacle.position.y + 0.004 and desired_y + height >= obstacle.position.y:
			desired_y = minf(desired_y, obstacle.position.y - height)
			hit_ceiling = true
	for id: String in doors:
		if not Doors.DEFINITIONS.has(id) or map_id != "house":
			continue
		var definition: Dictionary = Doors.DEFINITIONS[id]
		var footprint := AABB(Vector3(pos.x-HUMAN_RADIUS,-100,pos.z-HUMAN_RADIUS),Vector3(HUMAN_RADIUS*2,200,HUMAN_RADIUS*2))
		if not Doors.intersects_body(definition,float(doors[id].angle),footprint):
			continue
		var top: float = float(definition.hinge.y)+float(definition.height)
		var bottom: float = float(definition.hinge.y)+Doors.GAP
		if displacement.y<0 and pos.y>=top-0.004 and desired_y<=top:
			desired_y = maxf(desired_y,top)
			grounded = true
		elif displacement.y>0 and pos.y+height<=bottom+0.004 and desired_y+height>=bottom:
			desired_y = minf(desired_y,bottom-height)
			hit_ceiling = true
	next.y = desired_y
	for axis: int in [0, 2]:
		var trial: Vector3 = next
		trial[axis] += displacement[axis]
		trial.x = clampf(trial.x, -float(data.half_x) + HUMAN_RADIUS, float(data.half_x) - HUMAN_RADIUS)
		trial.z = clampf(trial.z, -float(data.half_z) + HUMAN_RADIUS, float(data.half_z) - HUMAN_RADIUS)
		var body: AABB = _body_box(trial, height)
		var blocked: bool = Doors.body_blocked(body, doors, map_id)
		for obstacle: AABB in _nearby(body,map_id):
			if body.intersects(obstacle):
				blocked = true
				break
		if not blocked:
			next = trial
		elif is_grounded(next, map_id, doors) and displacement.y <= 0.0:
			# Sweep the leading footprint onto one short step while keeping headroom.
			var step_y: float = next.y
			for obstacle: AABB in _nearby(body,map_id):
				if body.intersects(obstacle) and obstacle.end.y > step_y:
					step_y = obstacle.end.y
			var raised: Vector3 = Vector3(trial.x, step_y, trial.z)
			if step_y - next.y <= STEP_HEIGHT + 0.00001 and can_fit_human(raised, height, map_id, doors):
				next = raised
				grounded = true
	grounded = grounded and is_grounded(next, map_id, doors)
	if displacement.y == 0.0:
		grounded = is_grounded(next, map_id, doors)
	return {"p": next, "grounded": grounded, "hit_ceiling": hit_ceiling}

static func step_human(actor: Dictionary, intent: Dictionary, dt: float, map_id: String = "house", doors: Dictionary = {}) -> void:
	if not is_finite(dt) or dt <= 0.0:
		return
	var remaining: float = minf(dt, 1.0)
	while remaining > 0.000001:
		var part: float = minf(remaining, 0.025)
		_human_tick(actor, intent, part, map_id, doors)
		remaining -= part

static func _human_tick(actor: Dictionary, intent: Dictionary, dt: float, map_id: String, doors: Dictionary = {}) -> void:
	var position: Vector3 = actor.get("p", Vector3.ZERO)
	if not position.is_finite():
		position = human_spawn(0, map_id)
	var local_move: Vector3 = intent.get("move", Vector3.ZERO) if intent.get("move", Vector3.ZERO) is Vector3 else Vector3.ZERO
	if not local_move.is_finite():
		local_move = Vector3.ZERO
	local_move.y = 0.0
	local_move = local_move.limit_length(1.0)
	var yaw: Variant = intent.get("yaw", actor.get("yaw", 0.0))
	var pitch: Variant = intent.get("pitch", actor.get("pitch", 0.0))
	if (yaw is float or yaw is int) and (pitch is float or pitch is int):
		Pose.apply_view(actor, float(yaw), float(pitch), dt)
	var wants_crouch: bool = intent.get("crouch", false) is bool and bool(intent.get("crouch", false))
	var wants_sprint: bool = intent.get("sprint", false) is bool and bool(intent.get("sprint", false))
	var jump: bool = intent.get("jump", false) is bool and bool(intent.get("jump", false))
	var crouch: float = clampf(float(actor.get("crouch_amount", 0.0)), 0.0, 1.0)
	var next_crouch: float = move_toward(crouch, 1.0 if wants_crouch else 0.0, dt * 6.0)
	var next_height: float = lerpf(HUMAN_HEIGHT, HUMAN_CROUCH_HEIGHT, next_crouch)
	if next_crouch >= crouch or can_fit_human(position, next_height, map_id, doors):
		crouch = next_crouch
	var height: float = lerpf(HUMAN_HEIGHT, HUMAN_CROUCH_HEIGHT, crouch)
	var velocity: Vector3 = actor.get("velocity", Vector3.ZERO)
	if not velocity.is_finite():
		velocity = Vector3.ZERO
	var supported: bool = is_grounded(position, map_id, doors) and velocity.y <= 0.01
	if supported:
		velocity.y = 0.0
	if jump and not bool(actor.get("_jump_held", false)) and supported:
		velocity.y = JUMP_SPEED
		supported = false
	actor._jump_held = jump
	var crouching: bool = crouch > 0.05
	var sprinting: bool = wants_sprint and not crouching and local_move.length_squared() > 0.01
	var speed: float = HUMAN_CROUCH_SPEED if crouching else (HUMAN_RUN_SPEED if sprinting else HUMAN_SPEED)
	var travel: Vector3 = local_move.rotated(Vector3.UP, float(actor.get("yaw", 0.0))) * speed
	velocity.y = maxf(-MAX_FALL_SPEED, velocity.y - GRAVITY * dt)
	var was_grounded: bool = bool(actor.get("grounded",supported))
	var moved: Dictionary = move_human(position, Vector3(travel.x, velocity.y, travel.z) * dt, map_id, height, doors)
	actor.p = moved.p
	if bool(moved.grounded) or bool(moved.hit_ceiling):
		velocity.y = 0.0
	var delta: Vector3 = Vector3(moved.p) - position
	velocity.x = delta.x / dt
	velocity.z = delta.z / dt
	actor.velocity = velocity
	actor.grounded = bool(moved.grounded)
	actor.crouch_amount = crouch
	actor.crouching = crouching
	actor.sprinting = sprinting
	Pose.advance_motion(actor,delta,dt,was_grounded)

static func clear_segment(from: Vector3, to: Vector3, map_id: String = "house", doors: Dictionary = {}) -> bool:
	for obstacle: AABB in _nearby(_segment_bounds(from,to),map_id):
		if obstacle.intersects_segment(from, to) != null:
			return false
	return Doors.ray_doors(from,to,doors,map_id).is_empty()

static func ray_map(from: Vector3, to: Vector3, map_id: String = "house", padding: float = 0.0, doors: Dictionary = {}) -> Dictionary:
	var first: Dictionary = {}
	for obstacle: AABB in _nearby(_segment_bounds(from,to,padding),map_id):
		var intersection: Variant = obstacle.grow(maxf(0.0, padding)).intersects_segment(from, to)
		if intersection == null:
			continue
		var point: Vector3 = intersection
		var distance: float = from.distance_to(point)
		if not first.is_empty() and distance >= float(first.distance):
			continue
		var normal := Vector3.ZERO
		var closest := INF
		for axis: int in range(3):
			for upper: bool in [false, true]:
				var edge: float = obstacle.end[axis] if upper else obstacle.position[axis]
				if absf(point[axis] - edge) < closest:
					closest = absf(point[axis] - edge)
					normal = Vector3.ZERO
					normal[axis] = 1.0 if upper else -1.0
		first = {"p": point, "normal": normal, "distance": distance, "kind": "map"}
	var door_hit := Doors.ray_doors(from,to,doors,map_id,padding)
	if not door_hit.is_empty() and (first.is_empty() or float(door_hit.distance)<float(first.distance)):
		return door_hit
	return first

static func floor_below(position: Vector3, map_id: String = "house", doors: Dictionary = {}) -> float:
	var floor_y := 0.0
	for obstacle: AABB in _nearby(_segment_bounds(Vector3(position.x,0,position.z),position),map_id):
		if position.x >= obstacle.position.x and position.x <= obstacle.end.x and position.z >= obstacle.position.z and position.z <= obstacle.end.z and obstacle.end.y <= position.y:
			floor_y = maxf(floor_y, obstacle.end.y)
	for id: String in doors:
		if not Doors.DEFINITIONS.has(id) or map_id != "house":
			continue
		var definition: Dictionary = Doors.DEFINITIONS[id]
		var top: float = float(definition.hinge.y)+float(definition.height)
		if top<=position.y and Doors.intersects_body(definition,float(doors[id].angle),AABB(Vector3(position.x-.001,top-.005,position.z-.001),Vector3(.002,.006,.002))):
			floor_y = maxf(floor_y,top)
	return floor_y
