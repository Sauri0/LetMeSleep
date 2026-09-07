class_name GameSimulation
extends RefCounted

## Authoritative, deterministic rules. This object never trusts client positions.
## Numbers below are reversible prototype hypotheses, not tested balance.
const ArenaData = preload("res://scripts/arena.gd")
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
const MAX_HUMANS := 5
const MAX_MOSQUITOES := 12
const MAX_PLAYERS := 16
const BITE_DISTANCE := 0.58
const BITE_PREPARATION := 0.65
const BLOOD_PER_SECOND := 1.0
const INPUT_TIMEOUT := 0.40
const DEFAULT_CONFIG := {
	"mode": "blood", "map_id": "house", "human_count": 1, "round_seconds": 120.0, "blood_goal": 30.0,
	"rotation_seconds": 14.0, "respawn_seconds": 4.0, "mosquito_lives": 3,
	"task_interval": 24.0, "task_deadline": 18.0, "task_work": 3.0,
	"task_penalty": 2.0, "task_floor": 8.0, "task_goal": 0,
}
const TOOL_STATS := {
	"hands": {"label": "Manos / palmadas", "reach": 1.35, "cooldown": 0.80, "radius": 0.24},
	"swatter": {"label": "Matamoscas", "reach": 1.65, "cooldown": 0.60, "radius": 0.28},
	"racket": {"label": "Raqueta eléctrica", "reach": 1.75, "cooldown": 1.05, "radius": 0.44},
	"newspaper": {"label": "Diario enrollado", "reach": 1.50, "cooldown": 0.43, "radius": 0.18},
	"broom": {"label": "Escoba", "reach": 2.20, "cooldown": 1.20, "radius": 0.30},
}
const PICKUP_SPAWNS = Maps.HOUSE.pickups
# Front marks are all defendable with starting hands + an aimed body-band swat.
# Rear marks exist only with multiple humans, and cannot be self-swatted.
const BODY_ZONES := [
	{"label": "Frente", "bone": "head", "p": Vector3(0, 1.72, -0.24), "rear": false, "band": 0},
	{"label": "Mejilla izquierda", "bone": "head", "p": Vector3(-0.16, 1.54, -0.24), "rear": false, "band": 0},
	{"label": "Mejilla derecha", "bone": "head", "p": Vector3(0.16, 1.54, -0.24), "rear": false, "band": 0},
	{"label": "Hombro izquierdo", "bone": "shoulder_l", "p": Vector3(-0.33, 1.34, -0.22), "rear": false, "band": 0},
	{"label": "Hombro derecho", "bone": "shoulder_r", "p": Vector3(0.33, 1.34, -0.22), "rear": false, "band": 0},
	{"label": "Pecho izquierdo", "bone": "torso", "p": Vector3(-0.15, 1.18, -0.34), "rear": false, "band": 1},
	{"label": "Pecho derecho", "bone": "torso", "p": Vector3(0.15, 1.18, -0.34), "rear": false, "band": 1},
	{"label": "Antebrazo izquierdo", "bone": "forearm_l", "p": Vector3(-0.39, 1.03, -0.14), "rear": false, "band": 1},
	{"label": "Antebrazo derecho", "bone": "forearm_r", "p": Vector3(0.39, 1.03, -0.14), "rear": false, "band": 1},
	{"label": "Abdomen izquierdo", "bone": "torso", "p": Vector3(-0.14, 0.84, -0.32), "rear": false, "band": 1},
	{"label": "Abdomen derecho", "bone": "torso", "p": Vector3(0.14, 0.84, -0.32), "rear": false, "band": 1},
	{"label": "Muslo izquierdo", "bone": "thigh_l", "p": Vector3(-0.16, 0.59, -0.23), "rear": false, "band": 2},
	{"label": "Muslo derecho", "bone": "thigh_r", "p": Vector3(0.16, 0.59, -0.23), "rear": false, "band": 2},
	{"label": "Rodilla izquierda", "bone": "knee_l", "p": Vector3(-0.16, 0.38, -0.23), "rear": false, "band": 2},
	{"label": "Rodilla derecha", "bone": "knee_r", "p": Vector3(0.16, 0.38, -0.23), "rear": false, "band": 2},
	{"label": "Tobillo", "bone": "ankle_r", "p": Vector3(0.16, 0.19, -0.24), "rear": false, "band": 2},
	{"label": "Espalda alta izquierda", "bone": "torso", "p": Vector3(-0.15, 1.27, 0.32), "rear": true, "band": 0},
	{"label": "Espalda alta derecha", "bone": "torso", "p": Vector3(0.15, 1.27, 0.32), "rear": true, "band": 0},
	{"label": "Espalda media izquierda", "bone": "torso", "p": Vector3(-0.15, 1.04, 0.32), "rear": true, "band": 1},
	{"label": "Espalda media derecha", "bone": "torso", "p": Vector3(0.15, 1.04, 0.32), "rear": true, "band": 1},
	{"label": "Espalda baja izquierda", "bone": "torso", "p": Vector3(-0.15, 0.82, 0.32), "rear": true, "band": 1},
	{"label": "Espalda baja derecha", "bone": "torso", "p": Vector3(0.15, 0.82, 0.32), "rear": true, "band": 1},
]

var actors: Dictionary = {}
var pickups: Dictionary = {}
var config: Dictionary = DEFAULT_CONFIG.duplicate(true)
var phase := "lobby"
var elapsed := 0.0
var blood := 0.0
var winner := ""
var reason := ""
var tasks_done := 0
var task_goal := 0
var _human_ids: Array[int] = []
var _mosquito_ids: Array[int] = []
var _pending_actions: Array[Dictionary] = []
var _frame := 0
var _assignment_serial := 0
var _map_data: Dictionary = Maps.get_map("house")

static func sanitize_config(requested: Dictionary) -> Dictionary:
	var result: Dictionary = DEFAULT_CONFIG.duplicate(true)
	var requested_map: String = str(requested.get("map_id", "house"))
	result.map_id = requested_map if Maps.is_playable(requested_map) else "house"
	if str(requested.get("mode", "blood")) in ["blood", "survival", "sleep"]:
		result.mode = str(requested.get("mode", "blood"))
	for key: String in ["human_count", "round_seconds", "blood_goal", "rotation_seconds", "respawn_seconds", "mosquito_lives", "task_interval", "task_deadline", "task_work", "task_penalty", "task_floor", "task_goal"]:
		var value: Variant = requested.get(key, result[key])
		if (value is float or value is int) and is_finite(float(value)):
			result[key] = float(value)
	result.human_count = clampi(int(result.human_count), 1, MAX_HUMANS)
	result.round_seconds = clampf(float(result.round_seconds), 30.0, 180.0)
	result.blood_goal = clampf(float(result.blood_goal), 1.0, 1000.0)
	result.rotation_seconds = clampf(float(result.rotation_seconds), 4.0, 40.0)
	result.respawn_seconds = clampf(float(result.respawn_seconds), 1.0, 15.0)
	result.mosquito_lives = clampi(int(result.mosquito_lives), 1, 9)
	result.task_interval = clampf(float(result.task_interval), 15.0, 60.0)
	result.task_work = clampf(float(result.task_work), 1.0, 8.0)
	result.task_deadline = clampf(float(result.task_deadline), float(result.task_work) + 2.0, float(result.task_interval) - 0.5)
	result.task_floor = clampf(float(result.task_floor), float(result.task_work) + 2.0, float(result.task_deadline))
	result.task_penalty = clampf(float(result.task_penalty), 0.5, 8.0)
	result.task_goal = clampi(int(result.task_goal), 0, 100)
	return result

static func validate_roster(players: Dictionary) -> String:
	var humans := 0
	var mosquitoes := 0
	for id: Variant in players:
		if not id is int or int(id) <= 0 or not players[id] is Dictionary:
			return "La lista de jugadores no es válida."
		var role: String = str(players[id].get("role", ""))
		if role == "human":
			humans += 1
		elif role == "mosquito":
			mosquitoes += 1
		else:
			return "El sorteo debe asignar humano o mosquito a cada jugador."
	if humans < 1:
		return "Hace falta al menos un humano."
	if humans > MAX_HUMANS or mosquitoes > MAX_MOSQUITOES:
		return "Este prototipo admite hasta 5 humanos y 12 mosquitos."
	if humans + mosquitoes > MAX_PLAYERS:
		return "La sala admite hasta 16 jugadores en total."
	if mosquitoes < 1:
		return "Hace falta al menos un mosquito."
	var capacity: int = humans * (16 if humans == 1 else BODY_ZONES.size())
	if mosquitoes + 2 > capacity:
		return "No quedan suficientes zonas distintas y alternativas libres."
	return ""

func start(players: Dictionary, requested_config: Dictionary) -> void:
	actors.clear()
	pickups.clear()
	_human_ids.clear()
	_mosquito_ids.clear()
	_pending_actions.clear()
	_frame = 0
	_assignment_serial = 0
	elapsed = 0.0
	blood = 0.0
	winner = ""
	reason = validate_roster(players)
	tasks_done = 0
	task_goal = 0
	config = sanitize_config(requested_config)
	_map_data = Maps.get_map(str(config.map_id))
	if not reason.is_empty():
		phase = "lobby"
		return
	phase = "playing"
	var ids: Array = players.keys()
	ids.sort()
	for player_id: int in ids:
		var player: Dictionary = players[player_id]
		var human: bool = player.role == "human"
		var role_index: int = _human_ids.size() if human else _mosquito_ids.size()
		if human:
			_human_ids.append(player_id)
		else:
			_mosquito_ids.append(player_id)
		actors[player_id] = {
			"name": str(player.get("name", "Jugador")).substr(0, 24), "role": player.role,
			"appearance": CosmeticsData.appearance_for(player.get("cosmetics", {}), str(player.role)).duplicate(true),
			"p": Maps.human_spawn(str(config.map_id), role_index) if human else Maps.mosquito_spawn(str(config.map_id), role_index),
			"yaw": 0.0, "pitch": 0.0, "state": "human" if human else "flying",
			"alive": true, "swing": 0.0, "bitten": false, "tool": "hands",
			"velocity": Vector3.ZERO, "grounded": human, "sprinting": false, "crouching": false,
			"crouch_amount": 0.0, "motion_phase": 0.0, "motion_speed": 0.0,
			"_sprint": false, "_crouch": false, "_jump": false, "_jump_held": false,
			"lives": 0 if human else (int(config.mosquito_lives) if config.mode == "sleep" else 1), "_respawn_at": 0.0,
			"_move": Vector3.ZERO, "_interact": false, "_last_input": -100.0,
			"_input_seq": -1, "_action_seq": -1, "_assignment": {}, "_revision": 0,
			"_next_rotation": 0.0, "_assignment_frame": -1, "_forbidden": "",
			"_bite_started": 0.0, "_task": {}, "_deadline": float(config.task_deadline),
			"_next_task": 3.0 + role_index * 1.5, "_failures": 0, "_tasks_given": 0,
		}
	for index: int in range(_mosquito_ids.size()):
		var actor: Dictionary = actors[_mosquito_ids[index]]
		# Each mosquito owns a fixed phase offset; no synchronized global rotation.
		actor._next_rotation = float(config.rotation_seconds) * (1.0 + float(index) / float(_mosquito_ids.size()))
		_assign(_mosquito_ids[index])
	for index: int in range(_map_data.pickups.size()):
		var pickup: Dictionary = _map_data.pickups[index].duplicate(true)
		pickup.holder = 0
		pickups[index + 1] = pickup
	var opportunities := 0
	for id: int in _human_ids:
		var first: float = float(actors[id]._next_task)
		# Count tasks with enough time to complete their work before round close.
		var last_start: float = float(config.round_seconds) - float(config.task_work)
		if first <= last_start:
			opportunities += 1 + int(floor((last_start - first) / float(config.task_interval)))
	task_goal = int(ceil(float(opportunities) * 2.0 / 3.0)) if int(config.task_goal) == 0 else mini(int(config.task_goal), opportunities)

func submit_input(id: int, seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool = false, crouch: bool = false, jump: bool = false) -> void:
	if phase != "playing" or not actors.has(id) or seq < 0:
		return
	var actor: Dictionary = actors[id]
	if seq <= int(actor._input_seq) or not bool(actor.alive):
		return
	if not move.is_finite() or not is_finite(yaw) or not is_finite(pitch):
		return
	actor._input_seq = seq
	actor._move = move.limit_length(1.0)
	actor.yaw = wrapf(yaw, -PI, PI)
	actor.pitch = clampf(pitch, -1.48, 1.48)
	actor._interact = interact and actor.role == "human"
	actor._sprint = sprint and actor.role == "human"
	actor._crouch = crouch and actor.role == "human"
	actor._jump = jump and actor.role == "human"
	actor._last_input = elapsed

func action(id: int, seq: int, verb: String) -> void:
	if phase != "playing" or not actors.has(id) or seq < 0:
		return
	var actor: Dictionary = actors[id]
	if seq <= int(actor._action_seq) or not bool(actor.alive):
		return
	if verb not in ["bite", "attack", "self_swat", "perch", "pickup", "drop"]:
		return
	actor._action_seq = seq
	# Reliable RPCs may arrive in a batch; bounded queue prevents action spam.
	var queued := 0
	for entry: Dictionary in _pending_actions:
		if int(entry.id) == id:
			queued += 1
	if queued < 4:
		_pending_actions.append({"id": id, "verb": verb})

func step(dt: float) -> void:
	if phase != "playing" or not is_finite(dt) or dt <= 0.0:
		return
	var remaining: float = minf(dt, 1.0)
	while remaining > 0.000001 and phase == "playing":
		var part: float = minf(remaining, 0.05)
		_tick(minf(part, maxf(0.0, float(config.round_seconds) - elapsed)))
		remaining -= part

func _tick(dt: float) -> void:
	_frame += 1
	elapsed += dt
	for id: int in actors:
		var actor: Dictionary = actors[id]
		actor.swing = maxf(0.0, float(actor.swing) - dt)
		if not bool(actor.alive) and actor.role == "mosquito" and config.mode == "sleep" and int(actor.lives) > 0 and elapsed + 0.000001 >= float(actor._respawn_at):
			_respawn(id)
		if not bool(actor.alive):
			continue
		if actor.role == "human":
			var fresh: bool = elapsed - float(actor._last_input) <= INPUT_TIMEOUT
			var local_move: Vector3 = actor._move if fresh else Vector3.ZERO
			ArenaData.step_human(actor, {"move": local_move, "yaw": actor.yaw, "pitch": actor.pitch, "sprint": bool(actor._sprint) and fresh, "crouch": bool(actor._crouch) and fresh, "jump": bool(actor._jump) and fresh}, dt, str(config.map_id))
	# Human roles are drawn across arbitrary peer IDs. Advance every human before
	# projecting insects so body contact never depends on roster insertion order.
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if not bool(actor.alive):
			continue
		var local_move: Vector3 = actor._move if elapsed - float(actor._last_input) <= INPUT_TIMEOUT else Vector3.ZERO
		if actor.state == "biting":
			continue
		if actor.state == "perched" and local_move.length_squared() > 0.01:
			actor.state = "flying"
		var displacement: Vector3 = local_move.rotated(Vector3.UP, float(actor.yaw)) * dt * ArenaData.MOSQUITO_SPEED
		var previous: Vector3 = actor.p
		actor.p = ArenaData.move_body(previous, displacement, false, str(config.map_id))
		actor.p = _avoid_humans(previous, actor.p)
		actor.velocity = (Vector3(actor.p) - previous) / maxf(dt, 0.000001)
	_update_attached()
	var commands: Array[Dictionary] = _pending_actions
	_pending_actions = []
	for command: Dictionary in commands:
		_execute_action(int(command.id), str(command.verb))
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		var due: bool = elapsed + 0.000001 >= float(actor._next_rotation)
		if due:
			while elapsed + 0.000001 >= float(actor._next_rotation):
				actor._next_rotation = float(actor._next_rotation) + float(config.rotation_seconds)
			if bool(actor.alive) and actor.state != "biting" and int(actor._assignment_frame) != _frame:
				_assign(id)
	_update_attached()
	for id: int in _human_ids:
		actors[id].bitten = false
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state == "biting" and bool(actor.alive):
			var assignment: Dictionary = actor._assignment
			actors[int(assignment.human)].bitten = true
			if config.mode == "blood":
				# Integrate only the portion after preparation; attach/detach never adds an instant award.
				var active_start: float = maxf(elapsed - dt, float(actor._bite_started) + BITE_PREPARATION)
				blood += maxf(0.0, elapsed - active_start) * BLOOD_PER_SECOND
	if config.mode == "sleep":
		_update_tasks(dt)
	_evaluate_result()

func _execute_action(id: int, verb: String) -> void:
	if not actors.has(id) or not bool(actors[id].alive):
		return
	var actor: Dictionary = actors[id]
	if actor.role == "human":
		match verb:
			"attack": _attack(id, false)
			"self_swat": _attack(id, true)
			"pickup": _pickup(id)
			"drop": _drop(id)
		return
	match verb:
		"bite":
			if actor.state == "biting":
				_detach(id)
			else:
				_attach(id)
		"perch":
			if actor.state == "flying":
				_try_perch(id)
			elif actor.state == "perched":
				actor.state = "flying"

func _avoid_humans(previous: Vector3, position: Vector3) -> Vector3:
	var result: Vector3 = position
	# Capsule endpoints model torso, head and legs; soft projection lets insects
	# slide around bodies without treating the entire human as a rectangular wall.
	for id: int in _human_ids:
		var human: Dictionary = actors[id]
		for capsule: Dictionary in Pose.collision_segments(human):
			var base: Vector3 = Vector3(human.p) + Vector3(capsule.from).rotated(Vector3.UP, float(human.yaw))
			var tip: Vector3 = Vector3(human.p) + Vector3(capsule.to).rotated(Vector3.UP, float(human.yaw))
			var segment: Vector3 = tip - base
			var nearest: Vector3 = base + segment * clampf((result - base).dot(segment) / maxf(segment.length_squared(), 0.000001), 0.0, 1.0)
			var delta: Vector3 = result - nearest
			var radius: float = float(capsule.radius) + ArenaData.MOSQUITO_RADIUS
			if delta.length_squared() < radius * radius:
				var outward: Vector3 = delta.normalized()
				if outward.length_squared() < 0.01:
					outward = (previous - nearest).normalized()
				if outward.length_squared() < 0.01:
					outward = Vector3.FORWARD.rotated(Vector3.UP, float(human.yaw))
				result = ArenaData.move_body(result, outward * (radius - delta.length() + 0.005), false, str(config.map_id))
	return result

func _try_perch(id: int) -> void:
	var actor: Dictionary = actors[id]
	var position: Vector3 = actor.p
	var radius: float = ArenaData.MOSQUITO_RADIUS
	var candidates: Array[Vector3] = [
		Vector3(position.x, radius, position.z), Vector3(position.x, float(_map_data.ceiling) - radius, position.z),
		Vector3(-float(_map_data.half_x) + radius, position.y, position.z), Vector3(float(_map_data.half_x) - radius, position.y, position.z),
		Vector3(position.x, position.y, -float(_map_data.half_z) + radius), Vector3(position.x, position.y, float(_map_data.half_z) - radius),
	]
	for obstacle: AABB in _map_data.obstacles:
		var expanded: AABB = obstacle.grow(radius + 0.005)
		for axis: int in range(3):
			for edge: float in [expanded.position[axis], expanded.end[axis]]:
				var candidate: Vector3 = position
				candidate[axis] = edge
				var on_face := true
				for other_axis: int in range(3):
					if other_axis != axis and (candidate[other_axis] < expanded.position[other_axis] or candidate[other_axis] > expanded.end[other_axis]):
						on_face = false
				if on_face:
					candidates.append(candidate)
	var best_distance := 0.32
	var best: Vector3 = position
	var found := false
	for candidate: Vector3 in candidates:
		var distance: float = position.distance_to(candidate)
		if distance <= best_distance and ArenaData.clear_segment(position, candidate, str(config.map_id)):
			best_distance = distance
			best = candidate
			found = true
	if found:
		actor.p = best
		actor.state = "perched"
		actor._move = Vector3.ZERO
		actor.velocity = Vector3.ZERO

func _assign(id: int) -> void:
	var actor: Dictionary = actors[id]
	if not bool(actor.alive):
		actor._assignment = {}
		return
	var occupied: Dictionary = {}
	var pressure: Dictionary = {}
	for human_id: int in _human_ids:
		pressure[human_id] = 0
	for other_id: int in _mosquito_ids:
		if other_id == id:
			continue
		var other: Dictionary = actors[other_id]
		var assignment: Dictionary = other._assignment
		if bool(other.alive) and not assignment.is_empty():
			occupied[_zone_key(assignment)] = true
			pressure[int(assignment.human)] = int(pressure[int(assignment.human)]) + 1
	var old_key: String = _zone_key(actor._assignment)
	var preferred_rear: bool = _human_ids.size() > 1 and _assignment_serial % 3 == 2
	var best: Dictionary = {}
	var best_score := 1000000
	var capacity: int = 16 if _human_ids.size() == 1 else BODY_ZONES.size()
	for human_id: int in _human_ids:
		# With the old minimum ratio removed, some humans can have no attackers.
		# Rotate equal-pressure ties so e.g. 5v1 does not target the first peer forever.
		var human_priority: int = posmod(_human_ids.find(human_id) - _assignment_serial, _human_ids.size())
		for zone_index: int in range(capacity):
			var candidate: Dictionary = {"human": human_id, "zone": zone_index}
			var key: String = _zone_key(candidate)
			if occupied.has(key) or key == old_key or key == str(actor._forbidden):
				continue
			var zone: Dictionary = BODY_ZONES[zone_index]
			var score: int = int(pressure[human_id]) * 1000 + human_priority * 100 + posmod(zone_index - _assignment_serial * 5, capacity)
			if bool(zone.rear) != preferred_rear:
				score += 30
			if score < best_score:
				best = candidate
				best_score = score
	# Roster validation reserves at least two spare marks, even with one exclusion.
	if best.is_empty():
		abort("No se pudo repartir una zona distinta. Volvé a preparar la sala.")
		return
	actor._revision = int(actor._revision) + 1
	best.revision = int(actor._revision)
	actor._assignment = best
	actor._assignment_frame = _frame
	_assignment_serial += 1

static func _zone_key(assignment: Dictionary) -> String:
	if assignment.is_empty():
		return ""
	return "%d:%d" % [int(assignment.human), int(assignment.zone)]

func _zone_pose(assignment: Dictionary) -> Dictionary:
	if assignment.is_empty() or not actors.has(int(assignment.human)):
		return {}
	var human: Dictionary = actors[int(assignment.human)]
	return Pose.zone_pose(human, BODY_ZONES[int(assignment.zone)])

func _attach(id: int) -> void:
	var actor: Dictionary = actors[id]
	var assignment: Dictionary = actor._assignment
	var pose: Dictionary = _zone_pose(assignment)
	if pose.is_empty():
		return
	var to_mosquito: Vector3 = actor.p - Vector3(pose.p)
	if to_mosquito.length() > BITE_DISTANCE or to_mosquito.dot(pose.normal) < -0.03:
		return
	if not ArenaData.clear_segment(actor.p, pose.p, str(config.map_id)) or _body_occludes(actor.p, pose.p, -1):
		return
	actor.state = "biting"
	actor._bite_started = elapsed
	actor._forbidden = "" # A successful new bite clears the previous-zone exclusion.
	actor._move = Vector3.ZERO
	actor.p = Vector3(pose.p) + Vector3(pose.normal) * 0.06

func _detach(id: int) -> void:
	var actor: Dictionary = actors[id]
	var pose: Dictionary = _zone_pose(actor._assignment)
	actor._forbidden = _zone_key(actor._assignment)
	actor.state = "flying"
	if not pose.is_empty():
		actor.p = ArenaData.move_body(actor.p, Vector3(pose.normal) * 0.24, false, str(config.map_id))
	_assign(id) # Deliberately never modifies _next_rotation.

func _update_attached() -> void:
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state == "biting" and bool(actor.alive):
			var pose: Dictionary = _zone_pose(actor._assignment)
			if not pose.is_empty():
				actor.p = Vector3(pose.p) + Vector3(pose.normal) * 0.06

func _attack(id: int, self_only: bool) -> void:
	var human: Dictionary = actors[id]
	if float(human.swing) > 0.0:
		return
	var stats: Dictionary = TOOL_STATS[str(human.tool)]
	human.swing = float(stats.cooldown)
	var eye: Vector3 = Vector3(human.p) + Vector3(Pose.sample(human).eye).rotated(Vector3.UP, float(human.yaw))
	var direction: Vector3 = Vector3.FORWARD.rotated(Vector3.RIGHT, float(human.pitch)).rotated(Vector3.UP, float(human.yaw))
	var band: int = 0 if float(human.pitch) >= -0.25 else (1 if float(human.pitch) >= -0.85 else 2)
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary = actors[mosquito_id]
		if not bool(mosquito.alive):
			continue
		var assignment: Dictionary = mosquito._assignment
		var attached: bool = mosquito.state == "biting"
		var on_self: bool = attached and int(assignment.human) == id
		if on_self and bool(BODY_ZONES[int(assignment.zone)].rear):
			continue # Neither aim nor a long tool lets the owner hit a cooperative rear mark.
		if self_only:
			if on_self and int(BODY_ZONES[int(assignment.zone)].band) == band:
				_kill(mosquito_id)
			continue
		var delta: Vector3 = Vector3(mosquito.p) - eye
		var along: float = delta.dot(direction)
		if along < 0.0 or along > float(stats.reach):
			continue
		var radial: float = (delta - direction * along).length()
		if radial > float(stats.radius) + ArenaData.MOSQUITO_RADIUS:
			continue
		if not ArenaData.clear_segment(eye, mosquito.p, str(config.map_id)) or _body_occludes(eye, mosquito.p, id):
			continue
		if attached and not on_self:
			var pose: Dictionary = _zone_pose(assignment)
			if (eye - Vector3(pose.p)).dot(pose.normal) <= 0.02:
				continue # A teammate must stand on the exposed side of the actual body.
		_kill(mosquito_id)

func _body_occludes(from: Vector3, to: Vector3, ignored_human: int) -> bool:
	for id: int in _human_ids:
		if id == ignored_human:
			continue
		var human: Dictionary = actors[id]
		var start_local: Vector3 = (from - Vector3(human.p)).rotated(Vector3.UP, -float(human.yaw))
		var end_local: Vector3 = (to - Vector3(human.p)).rotated(Vector3.UP, -float(human.yaw))
		for box: AABB in Pose.body_boxes(human):
			if box.intersects_segment(start_local, end_local) != null:
				return true
	return false

func _kill(id: int) -> void:
	var actor: Dictionary = actors[id]
	if not bool(actor.alive):
		return
	actor.alive = false
	actor.state = "dead"
	actor.lives = maxi(0, int(actor.lives) - 1)
	actor._respawn_at = elapsed + float(config.respawn_seconds) if config.mode == "sleep" and int(actor.lives) > 0 else 0.0
	actor._forbidden = _zone_key(actor._assignment)
	actor._assignment = {}
	actor._move = Vector3.ZERO
	actor.velocity = Vector3.ZERO
	actor._interact = false
	# Shared blood is never deducted. Only sleep offers personal limited lives.

func _respawn(id: int) -> void:
	var actor: Dictionary = actors[id]
	var candidates: Array = _map_data.respawn_points
	var best_position: Vector3 = candidates[0]
	var best_distance := -1.0
	for candidate: Vector3 in candidates:
		var clearance := 100.0
		for human_id: int in _human_ids:
			clearance = minf(clearance, candidate.distance_to(Vector3(actors[human_id].p) + Vector3.UP * 1.2))
		if clearance > best_distance:
			best_distance = clearance
			best_position = candidate
	actor.p = best_position
	actor.alive = true
	actor.state = "flying"
	actor._respawn_at = 0.0
	actor._move = Vector3.ZERO
	actor.velocity = Vector3.ZERO
	actor._interact = false
	actor._last_input = -100.0
	_assign(id)

func _pickup(id: int) -> void:
	var actor: Dictionary = actors[id]
	var nearest := -1
	var nearest_distance := 1.45
	for pickup_id: int in pickups:
		var pickup: Dictionary = pickups[pickup_id]
		if int(pickup.holder) != 0:
			continue
		var distance: float = Vector3(actor.p).distance_to(Vector3(pickup.p))
		if distance < nearest_distance and ArenaData.clear_segment(Vector3(actor.p) + Vector3.UP * 0.8, pickup.p, str(config.map_id)):
			nearest_distance = distance
			nearest = pickup_id
	if nearest < 0:
		return
	_drop(id)
	pickups[nearest].holder = id
	actor.tool = str(pickups[nearest].tool)

func _drop(id: int) -> void:
	var actor: Dictionary = actors[id]
	for pickup_id: int in pickups:
		var pickup: Dictionary = pickups[pickup_id]
		if int(pickup.holder) == id:
			pickup.holder = 0
			var forward: Vector3 = Vector3.FORWARD.rotated(Vector3.UP, float(actor.yaw))
			var height: float = lerpf(ArenaData.HUMAN_HEIGHT, ArenaData.HUMAN_CROUCH_HEIGHT, float(actor.crouch_amount))
			var landing: Vector3 = ArenaData.move_body(actor.p, forward * 0.55, true, str(config.map_id), height)
			landing.y = ArenaData.floor_below(landing + Vector3.UP * 0.15, str(config.map_id))
			pickup.p = landing + Vector3.UP * 0.15
			pickup.yaw = float(actor.yaw)
	actor.tool = "hands"

func _update_tasks(dt: float) -> void:
	for index: int in range(_human_ids.size()):
		var id: int = _human_ids[index]
		var human: Dictionary = actors[id]
		var task: Dictionary = human._task
		if not task.is_empty():
			var usable_dt: float = minf(dt, float(task.remaining))
			var near: bool = Vector3(human.p).distance_to(Vector3(task.p)) < 1.20
			var input_fresh: bool = elapsed - float(human._last_input) <= INPUT_TIMEOUT
			if near and bool(human._interact) and input_fresh and not bool(human.bitten) and float(human.swing) <= 0.0:
				task.progress = minf(float(task.work), float(task.progress) + usable_dt)
			task.remaining = maxf(0.0, float(task.remaining) - dt)
			if float(task.progress) + 0.000001 >= float(task.work):
				tasks_done += 1
				human._task = {}
			elif float(task.remaining) <= 0.000001:
				human._failures = int(human._failures) + 1
				human._deadline = maxf(float(config.task_floor), float(human._deadline) - float(config.task_penalty))
				human._task = {}
		if elapsed + 0.000001 >= float(human._next_task):
			var scheduled: float = float(human._next_task)
			while elapsed + 0.000001 >= float(human._next_task):
				human._next_task = float(human._next_task) + float(config.task_interval)
			if scheduled + float(config.task_work) <= float(config.round_seconds) and Dictionary(human._task).is_empty():
				var station_id: int = (int(human._tasks_given) + index) % _map_data.stations.size()
				var station: Dictionary = _map_data.stations[station_id]
				human._task = {
					"name": station.name, "station": station_id, "p": station.p,
					"remaining": float(human._deadline), "progress": 0.0, "work": float(config.task_work),
				}
				human._tasks_given = int(human._tasks_given) + 1

func _evaluate_result() -> void:
	if phase != "playing":
		return
	var alive := 0
	var total_lives := 0
	for id: int in _mosquito_ids:
		total_lives += int(actors[id].lives)
		if bool(actors[id].alive):
			alive += 1
	if config.mode == "blood" and blood + 0.000001 >= float(config.blood_goal):
		blood = float(config.blood_goal)
		_finish("mosquito", "Los mosquitos alcanzaron la cuota compartida.")
	elif config.mode in ["blood", "survival"] and alive == 0:
		_finish("human", "Los humanos eliminaron a todos los mosquitos.")
	elif config.mode == "sleep" and total_lives == 0:
		_finish("human", "Los mosquitos agotaron todas sus vidas.")
	elif elapsed + 0.000001 >= float(config.round_seconds):
		if config.mode == "blood":
			_finish("human", "Se terminó el tiempo antes de alcanzar la cuota de sangre.")
		elif config.mode == "survival":
			_finish("mosquito", "Al menos un mosquito sobrevivió hasta el final.")
		elif tasks_done >= task_goal:
			_finish("human", "Los humanos completaron la meta colectiva de tareas.")
		else:
			_finish("mosquito", "Los mosquitos impidieron completar la meta de tareas.")

func _finish(team: String, explanation: String) -> void:
	if phase != "playing":
		return
	winner = team
	reason = explanation
	phase = "results"
	_pending_actions.clear()

func abort(explanation: String) -> void:
	phase = "lobby"
	winner = ""
	reason = explanation
	_pending_actions.clear()
	for id: int in actors:
		actors[id]._assignment = {}
		actors[id]._move = Vector3.ZERO
		actors[id]._interact = false
		actors[id].bitten = false
		if actors[id].state == "biting":
			actors[id].state = "flying"

func public_snapshot() -> Dictionary:
	var public_actors: Dictionary = {}
	for id: int in actors:
		var actor: Dictionary = actors[id]
		# Explicit allowlist: never serialize hidden target/zone reservations or tasks.
		public_actors[id] = {
			"name": actor.name, "role": actor.role, "p": actor.p, "yaw": actor.yaw,
			"appearance": Dictionary(actor.appearance).duplicate(true),
			"pitch": actor.pitch, "state": actor.state, "alive": actor.alive,
			"swing": actor.swing, "bitten": actor.bitten, "tool": actor.tool, "lives": actor.lives,
			"velocity": actor.velocity, "grounded": actor.grounded, "sprinting": actor.sprinting,
			"crouching": actor.crouching, "crouch_amount": actor.crouch_amount,
			"motion_phase": actor.motion_phase, "motion_speed": actor.motion_speed,
		}
	return {
		"phase": phase, "map_id": config.map_id, "elapsed": elapsed, "time_left": maxf(0.0, float(config.round_seconds) - elapsed),
		"config": config.duplicate(true), "blood": blood, "winner": winner, "reason": reason,
		"tasks_done": tasks_done, "task_goal": task_goal, "actors": public_actors,
		"pickups": pickups.duplicate(true),
	}

func private_for(id: int) -> Dictionary:
	if not actors.has(id):
		return {}
	var actor: Dictionary = actors[id]
	if actor.role == "human":
		return {"task": Dictionary(actor._task).duplicate(true), "deadline": actor._deadline, "failures": actor._failures}
	var assignment: Dictionary = {}
	if bool(actor.alive) and not Dictionary(actor._assignment).is_empty():
		assignment = Dictionary(actor._assignment).duplicate(true)
		assignment.merge(_zone_pose(assignment))
	var respawn_left: float = maxf(0.0, float(actor._respawn_at) - elapsed) if config.mode == "sleep" and not bool(actor.alive) and int(actor.lives) > 0 else 0.0
	return {"assignment": assignment, "state": actor.state, "respawn_left": respawn_left, "lives": actor.lives}
