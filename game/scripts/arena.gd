class_name Arena
extends RefCounted

const Maps = preload("res://scripts/map_catalog.gd")
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

static func _map(map_id: String) -> Dictionary:
	return Maps.LOBBY if map_id == "lobby" else Maps.HOUSE

static func human_spawn(index: int, map_id: String = "house") -> Vector3:
	var data: Dictionary = _map(map_id)
	var points: Array = data.lobby_spawns if map_id == "lobby" else data.human_spawns
	return points[posmod(index, points.size())]

static func mosquito_spawn(index: int, map_id: String = "house") -> Vector3:
	var points: Array = _map(map_id).mosquito_spawns
	return points[posmod(index, points.size())] if not points.is_empty() else human_spawn(index, map_id) + Vector3.UP * 1.2

static func move_body(pos: Vector3, displacement: Vector3, human: bool, map_id: String = "house", height: float = HUMAN_HEIGHT) -> Vector3:
	if not pos.is_finite() or not displacement.is_finite():
		return pos if pos.is_finite() else Vector3.ZERO
	if human:
		return move_human(pos, displacement, map_id, height).p
	var pieces: int = maxi(1, int(ceil(displacement.length() / 0.06)))
	var result: Vector3 = pos
	for part: int in range(pieces):
		result = _move_insect_part(result, displacement / pieces, map_id)
	return result

static func _move_insect_part(pos: Vector3, displacement: Vector3, map_id: String) -> Vector3:
	var data: Dictionary = _map(map_id)
	var next: Vector3 = pos
	for axis: int in [0, 2, 1]:
		var trial: Vector3 = next
		trial[axis] += displacement[axis]
		trial.x = clampf(trial.x, -float(data.half_x) + MOSQUITO_RADIUS, float(data.half_x) - MOSQUITO_RADIUS)
		trial.z = clampf(trial.z, -float(data.half_z) + MOSQUITO_RADIUS, float(data.half_z) - MOSQUITO_RADIUS)
		trial.y = clampf(trial.y, MOSQUITO_RADIUS, float(data.ceiling) - MOSQUITO_RADIUS)
		var body := AABB(trial - Vector3.ONE * MOSQUITO_RADIUS, Vector3.ONE * MOSQUITO_RADIUS * 2)
		var blocked := false
		for obstacle: AABB in data.obstacles:
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

static func step_mosquito(actor: Dictionary, local_move: Vector3, dt: float, map_id: String = "house", assisted_velocity: Variant = null) -> void:
	if not is_finite(dt) or dt <= 0.0:
		return
	var target: Vector3 = flight_direction(local_move, float(actor.yaw), float(actor.pitch)) * MOSQUITO_SPEED
	if assisted_velocity is Vector3 and Vector3(assisted_velocity).is_finite():
		target = Vector3(assisted_velocity).limit_length(MOSQUITO_SPEED)
	var velocity: Vector3 = actor.get("velocity", Vector3.ZERO)
	var rate: float = FLIGHT_BRAKING if target.length_squared() < 0.0001 else FLIGHT_ACCELERATION
	velocity = velocity.move_toward(target, rate * minf(dt, 0.1))
	var previous: Vector3 = actor.p
	actor.p = move_body(previous, velocity * minf(dt, 0.1), false, map_id)
	actor.velocity = (Vector3(actor.p) - previous) / minf(dt, 0.1)

static func _body_box(pos: Vector3, height: float) -> AABB:
	return AABB(pos + Vector3(-HUMAN_RADIUS, 0.003, -HUMAN_RADIUS), Vector3(HUMAN_RADIUS * 2, maxf(0.01, height - 0.006), HUMAN_RADIUS * 2))

static func can_fit_human(pos: Vector3, height: float = HUMAN_HEIGHT, map_id: String = "house") -> bool:
	var data: Dictionary = _map(map_id)
	if not pos.is_finite() or height < HUMAN_CROUCH_HEIGHT or height > HUMAN_HEIGHT:
		return false
	if absf(pos.x) + HUMAN_RADIUS > float(data.half_x) + 0.00001 or absf(pos.z) + HUMAN_RADIUS > float(data.half_z) + 0.00001 or pos.y < -0.00001 or pos.y + height > float(data.ceiling) + 0.00001:
		return false
	var body: AABB = _body_box(pos, height)
	for obstacle: AABB in data.obstacles:
		if body.intersects(obstacle):
			return false
	return true

static func is_grounded(pos: Vector3, map_id: String = "house") -> bool:
	if pos.y <= 0.004:
		return true
	for obstacle: AABB in _map(map_id).obstacles:
		if absf(pos.y - obstacle.end.y) <= 0.005 and pos.x + HUMAN_RADIUS > obstacle.position.x and pos.x - HUMAN_RADIUS < obstacle.end.x and pos.z + HUMAN_RADIUS > obstacle.position.z and pos.z - HUMAN_RADIUS < obstacle.end.z:
			return true
	return false

static func move_human(pos: Vector3, displacement: Vector3, map_id: String = "house", height: float = HUMAN_HEIGHT) -> Dictionary:
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
	for obstacle: AABB in data.obstacles:
		var overlaps: bool = pos.x + HUMAN_RADIUS > obstacle.position.x and pos.x - HUMAN_RADIUS < obstacle.end.x and pos.z + HUMAN_RADIUS > obstacle.position.z and pos.z - HUMAN_RADIUS < obstacle.end.z
		if not overlaps:
			continue
		if displacement.y < 0.0 and pos.y >= obstacle.end.y - 0.004 and desired_y <= obstacle.end.y:
			desired_y = maxf(desired_y, obstacle.end.y)
			grounded = true
		elif displacement.y > 0.0 and pos.y + height <= obstacle.position.y + 0.004 and desired_y + height >= obstacle.position.y:
			desired_y = minf(desired_y, obstacle.position.y - height)
			hit_ceiling = true
	next.y = desired_y
	for axis: int in [0, 2]:
		var trial: Vector3 = next
		trial[axis] += displacement[axis]
		trial.x = clampf(trial.x, -float(data.half_x) + HUMAN_RADIUS, float(data.half_x) - HUMAN_RADIUS)
		trial.z = clampf(trial.z, -float(data.half_z) + HUMAN_RADIUS, float(data.half_z) - HUMAN_RADIUS)
		var body: AABB = _body_box(trial, height)
		var blocked := false
		for obstacle: AABB in data.obstacles:
			if body.intersects(obstacle):
				blocked = true
				break
		if not blocked:
			next = trial
		elif is_grounded(next, map_id) and displacement.y <= 0.0:
			# Sweep the leading footprint onto one short step while keeping headroom.
			var step_y: float = next.y
			for obstacle: AABB in data.obstacles:
				if body.intersects(obstacle) and obstacle.end.y > step_y:
					step_y = obstacle.end.y
			var raised: Vector3 = Vector3(trial.x, step_y, trial.z)
			if step_y - next.y <= STEP_HEIGHT + 0.00001 and can_fit_human(raised, height, map_id):
				next = raised
				grounded = true
	grounded = grounded and is_grounded(next, map_id)
	if displacement.y == 0.0:
		grounded = is_grounded(next, map_id)
	return {"p": next, "grounded": grounded, "hit_ceiling": hit_ceiling}

static func step_human(actor: Dictionary, intent: Dictionary, dt: float, map_id: String = "house") -> void:
	if not is_finite(dt) or dt <= 0.0:
		return
	var remaining: float = minf(dt, 1.0)
	while remaining > 0.000001:
		var part: float = minf(remaining, 0.025)
		_human_tick(actor, intent, part, map_id)
		remaining -= part

static func _human_tick(actor: Dictionary, intent: Dictionary, dt: float, map_id: String) -> void:
	var position: Vector3 = actor.get("p", Vector3.ZERO)
	if not position.is_finite():
		position = human_spawn(0, map_id)
	var local_move: Vector3 = intent.get("move", Vector3.ZERO) if intent.get("move", Vector3.ZERO) is Vector3 else Vector3.ZERO
	if not local_move.is_finite():
		local_move = Vector3.ZERO
	local_move.y = 0.0
	local_move = local_move.limit_length(1.0)
	for key: String in ["yaw", "pitch"]:
		var value: Variant = intent.get(key, actor.get(key, 0.0))
		if (value is float or value is int) and is_finite(float(value)):
			actor[key] = wrapf(float(value), -PI, PI) if key == "yaw" else clampf(float(value), -1.48, 1.48)
	var wants_crouch: bool = intent.get("crouch", false) is bool and bool(intent.get("crouch", false))
	var wants_sprint: bool = intent.get("sprint", false) is bool and bool(intent.get("sprint", false))
	var jump: bool = intent.get("jump", false) is bool and bool(intent.get("jump", false))
	var crouch: float = clampf(float(actor.get("crouch_amount", 0.0)), 0.0, 1.0)
	var next_crouch: float = move_toward(crouch, 1.0 if wants_crouch else 0.0, dt * 6.0)
	var next_height: float = lerpf(HUMAN_HEIGHT, HUMAN_CROUCH_HEIGHT, next_crouch)
	if next_crouch >= crouch or can_fit_human(position, next_height, map_id):
		crouch = next_crouch
	var height: float = lerpf(HUMAN_HEIGHT, HUMAN_CROUCH_HEIGHT, crouch)
	var velocity: Vector3 = actor.get("velocity", Vector3.ZERO)
	if not velocity.is_finite():
		velocity = Vector3.ZERO
	var supported: bool = is_grounded(position, map_id) and velocity.y <= 0.01
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
	var moved: Dictionary = move_human(position, Vector3(travel.x, velocity.y, travel.z) * dt, map_id, height)
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
	actor.motion_speed = Vector2(velocity.x, velocity.z).length()
	actor.motion_phase = float(actor.get("motion_phase", 0.0))
	if bool(actor.grounded):
		actor.motion_phase = fposmod(float(actor.motion_phase) + Vector2(delta.x, delta.z).length() * TAU / (2.8 if sprinting else 2.3), TAU)

static func clear_segment(from: Vector3, to: Vector3, map_id: String = "house") -> bool:
	for obstacle: AABB in _map(map_id).obstacles:
		if obstacle.intersects_segment(from, to) != null:
			return false
	return true

static func floor_below(position: Vector3, map_id: String = "house") -> float:
	var floor_y := 0.0
	for obstacle: AABB in _map(map_id).obstacles:
		if position.x >= obstacle.position.x and position.x <= obstacle.end.x and position.z >= obstacle.position.z and position.z <= obstacle.end.z and obstacle.end.y <= position.y:
			floor_y = maxf(floor_y, obstacle.end.y)
	return floor_y
