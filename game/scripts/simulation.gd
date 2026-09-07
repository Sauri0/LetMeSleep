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
const BITE_DISTANCE := 0.07
const ATTACH_OFFSET := 0.045
const FOCUS_DISTANCE := 1.60
const FOCUS_SECONDS := 1.20
const BITE_PREPARATION := 1.0
const BLOOD_PER_SECOND := 0.80
const SHARED_BLOOD_RATE_CAP := 1.0
const STRIKE_START := 0.08
const STRIKE_END := 0.25
const INPUT_TIMEOUT := 0.40
const STUN_SECONDS := 35.0
const HELP_DISTANCE := 0.80
const HELP_RATE := 4.0
const HELP_FACING := 0.35
# Longest authored walking route takes 16.375 s without sprint. Keep an explicit
# 21 s travel allowance in room settings, including turns and off-route starts.
const TASK_TRAVEL_RESERVE := 21.0
const TASK_DISPATCH_MARGIN := 0.05
const DEFAULT_CONFIG := {
	"mode": "blood", "map_id": "house", "human_count": 1, "round_seconds": 120.0, "blood_goal": 12.0,
	"rotation_seconds": 14.0, "respawn_seconds": 4.0, "mosquito_lives": 3,
	"task_interval": 36.0, "task_deadline": 30.0, "task_work": 3.0,
	"task_penalty": 2.0, "task_floor": 24.0, "task_goal": 0,
}
const TOOL_STATS := {
	"hands": {"label": "Manos / palmadas", "reach": 1.70, "cooldown": 0.80, "radius": 0.095},
	"swatter": {"label": "Matamoscas", "reach": 1.65, "cooldown": 0.60, "radius": 0.115},
	"racket": {"label": "Raqueta eléctrica", "reach": 1.75, "cooldown": 1.05, "radius": 0.14},
	"newspaper": {"label": "Diario enrollado", "reach": 1.50, "cooldown": 0.43, "radius": 0.06},
	"broom": {"label": "Escoba", "reach": 2.20, "cooldown": 1.20, "radius": 0.17},
}
const PICKUP_SPAWNS = Maps.HOUSE.pickups
# Eight readable front surfaces; a solo human can aim at every one. Additional
# insects wait privately instead of receiving marks hidden from the owner.
const FRONT_ZONE_COUNT := 8
const BODY_ZONES := [
	{"label": "Pecho izquierdo", "bone": "torso", "p": Vector3(-0.10, 1.16, -0.30), "rear": false},
	{"label": "Pecho derecho", "bone": "torso", "p": Vector3(0.10, 1.16, -0.30), "rear": false},
	{"label": "Abdomen izquierdo", "bone": "torso", "p": Vector3(-0.10, 0.94, -0.30), "rear": false},
	{"label": "Abdomen derecho", "bone": "torso", "p": Vector3(0.10, 0.94, -0.30), "rear": false},
	{"label": "Antebrazo izquierdo", "bone": "forearm_l", "p": Vector3(-0.24, 0.98, -0.48), "rear": false},
	{"label": "Antebrazo derecho", "bone": "forearm_r", "p": Vector3(0.24, 0.98, -0.48), "rear": false},
	{"label": "Muslo izquierdo", "bone": "thigh_l", "p": Vector3(-0.135, 0.59, -0.20), "rear": false},
	{"label": "Muslo derecho", "bone": "thigh_r", "p": Vector3(0.135, 0.59, -0.20), "rear": false},
	{"label": "Espalda izquierda", "bone": "torso", "p": Vector3(-0.12, 1.09, 0.30), "rear": true},
	{"label": "Espalda derecha", "bone": "torso", "p": Vector3(0.12, 1.09, 0.30), "rear": true},
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
var _assignment_waiters: Array[int] = []
var _map_data: Dictionary = Maps.get_map("house")

static func minimum_task_deadline(work: float) -> float:
	return clampf(work if is_finite(work) else float(DEFAULT_CONFIG.task_work), 1.0, 8.0) + TASK_TRAVEL_RESERVE

static func minimum_round_seconds(settings: Dictionary) -> float:
	if str(settings.get("mode", "blood")) != "sleep":
		return 30.0
	var requested_count: Variant = settings.get("human_count", 1)
	var count: int = clampi(int(requested_count), 1, MAX_HUMANS) if (requested_count is float or requested_count is int) and is_finite(float(requested_count)) else 1
	var requested_work: Variant = settings.get("task_work", DEFAULT_CONFIG.task_work)
	var work: float = float(requested_work) if requested_work is float or requested_work is int else float(DEFAULT_CONFIG.task_work)
	var last_first_task: float = 3.0 + float(count - 1) * 1.5
	return maxf(30.0, ceilf(last_first_task + minimum_task_deadline(work) + TASK_DISPATCH_MARGIN))

static func last_task_start(settings: Dictionary) -> float:
	# Leave one authority tick for dispatch after the scheduled instant. The
	# exact same cutoff counts opportunities and prevents late task creation.
	return float(settings.round_seconds) - minimum_task_deadline(float(settings.task_work)) - TASK_DISPATCH_MARGIN

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
	result.task_work = clampf(float(result.task_work), 1.0, 8.0)
	var minimum_deadline: float = minimum_task_deadline(float(result.task_work))
	result.task_interval = clampf(float(result.task_interval), maxf(15.0, minimum_deadline + 0.5), 60.0)
	result.task_deadline = clampf(float(result.task_deadline), minimum_deadline, float(result.task_interval) - 0.5)
	result.task_floor = clampf(float(result.task_floor), minimum_deadline, float(result.task_deadline))
	result.task_penalty = clampf(float(result.task_penalty), 0.5, 8.0)
	result.task_goal = clampi(int(result.task_goal), 0, 100)
	result.round_seconds = clampf(float(result.round_seconds), minimum_round_seconds(result), 180.0)
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
	return ""

func start(players: Dictionary, requested_config: Dictionary) -> void:
	actors.clear()
	pickups.clear()
	_human_ids.clear()
	_mosquito_ids.clear()
	_pending_actions.clear()
	_frame = 0
	_assignment_serial = 0
	_assignment_waiters.clear()
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
			"yaw": 0.0, "pitch": 0.0, "body_yaw": 0.0, "inspecting": false, "strike": {}, "state": "human" if human else "flying",
			"alive": true, "swing": 0.0, "bitten": false, "threatened": false, "tool": "hands",
			"help_target": 0, "_stun_remaining": 0.0, "_stun_helped": false,
			"_focus_progress": 0.0, "_focus_pulse_until": 0.0, "_focus_suppressed": false,
			"_strike_at": -1.0, "_strike_until": -1.0, "_strike_started": -1.0, "_strike_resolved": false,
			"_attack": {"id": -1, "state": "idle", "hit": false, "point": Vector3.ZERO},
			"_bite_feedback": {"id": 0, "active": false, "count": 0, "side": "front"}, "_bite_signature": "",
			"velocity": Vector3.ZERO, "grounded": human, "sprinting": false, "crouching": false,
			"crouch_amount": 0.0, "motion_phase": 0.0, "motion_speed": 0.0,
			"_sprint": false, "_crouch": false, "_jump": false, "_jump_held": false,
			"lives": 1 if not human and config.mode == "survival" else 0, "_respawn_at": 0.0,
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
	# Assigned rosters are authoritative, including direct offline callers.
	config.human_count = _human_ids.size()
	config.round_seconds = maxf(float(config.round_seconds), minimum_round_seconds(config))
	for id: int in _human_ids:
		var first: float = float(actors[id]._next_task)
		var last_start: float = last_task_start(config)
		if first <= last_start + 0.000001:
			opportunities += 1 + int(floor(maxf(0.0, last_start - first) / float(config.task_interval)))
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
	if actor.role == "human":
		Pose.apply_view(actor, yaw, pitch)
	else:
		actor.yaw = wrapf(yaw, -PI, PI)
		actor.pitch = clampf(pitch, -1.48, 1.48)
	if actor.state == "stunned":
		actor._move = Vector3.ZERO
		actor._interact = false
		return
	actor._interact = interact
	if not interact:
		actor._focus_suppressed = false
		actor._focus_pulse_until = 0.0
	actor._sprint = sprint and actor.role == "human"
	actor._crouch = crouch and actor.role == "human"
	actor._jump = jump and actor.role == "human"
	actor._last_input = elapsed

func action(id: int, seq: int, verb: String, aim_yaw: float = NAN, aim_pitch: float = NAN) -> void:
	if phase != "playing" or not actors.has(id) or seq < 0:
		return
	var actor: Dictionary = actors[id]
	if seq <= int(actor._action_seq) or not bool(actor.alive) or actor.state == "stunned":
		return
	if verb not in ["bite", "attack", "self_swat", "perch", "pickup", "drop"]:
		return
	if is_nan(aim_yaw) and is_nan(aim_pitch):
		aim_yaw = float(actor.yaw)
		aim_pitch = float(actor.pitch)
	elif not is_finite(aim_yaw) or not is_finite(aim_pitch):
		return
	if actor.role == "human":
		aim_pitch = clampf(aim_pitch, Pose.HUMAN_PITCH_MIN, Pose.HUMAN_PITCH_MAX)
		aim_yaw = Pose.clamp_view_yaw(actor, aim_yaw, aim_pitch)
	actor._action_seq = seq
	# Reliable RPCs may arrive in a batch; bounded queue prevents action spam.
	var queued := 0
	for entry: Dictionary in _pending_actions:
		if int(entry.id) == id:
			queued += 1
	if queued < 4:
		_pending_actions.append({"id": id, "seq": seq, "verb": verb, "yaw": aim_yaw, "pitch": aim_pitch})

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
		actor.threatened = false
		if not Dictionary(actor.strike).is_empty():
			actor.strike.progress = clampf((elapsed - float(actor._strike_started)) / Pose.SWING_GESTURE_SECONDS, 0.0, 1.0)
			actor.strike.active = float(actor.strike.progress) < 1.0
		if not bool(actor.alive):
			continue
		if actor.role == "human":
			var fresh: bool = elapsed - float(actor._last_input) <= INPUT_TIMEOUT
			var local_move: Vector3 = actor._move if fresh else Vector3.ZERO
			ArenaData.step_human(actor, {"move": local_move, "yaw": actor.yaw, "pitch": actor.pitch, "sprint": bool(actor._sprint) and fresh, "crouch": bool(actor._crouch) and fresh, "jump": bool(actor._jump) and fresh}, dt, str(config.map_id))
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		actor.help_target = 0
		if actor.state == "stunned":
			var previous: Vector3 = actor.p
			ArenaData.step_stunned(actor, dt, str(config.map_id))
			actor.p = _avoid_humans(previous, actor.p)
	_update_help(dt)
	# Human roles are drawn across arbitrary peer IDs. Advance every human before
	# projecting insects so body contact never depends on roster insertion order.
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if not bool(actor.alive) or actor.state == "stunned":
			continue
		var local_move: Vector3 = actor._move if elapsed - float(actor._last_input) <= INPUT_TIMEOUT else Vector3.ZERO
		if actor.state == "biting":
			continue
		if int(actor.help_target) != 0:
			actor._focus_progress = 0.0
			var previous: Vector3 = actor.p
			ArenaData.step_mosquito(actor, local_move, dt, str(config.map_id))
			actor.p = _avoid_humans(previous, actor.p)
			continue
		var focus: Dictionary = _focus_info(id)
		var holding: bool = _focus_held(actor)
		var assist: Variant = null
		if holding and bool(focus.can_focus):
			actor._focus_progress = minf(1.0, float(actor._focus_progress) + dt / FOCUS_SECONDS)
			if float(actor._focus_progress) >= 1.0:
				_attach(id)
				if actor.state == "biting":
					continue
			var target: Dictionary = actors[int(actor._assignment.human)]
			if float(actor._focus_progress) >= 0.1:
				target.threatened = true
			var pose: Dictionary = _zone_pose(actor._assignment)
			var destination: Vector3 = Vector3(pose.p) + Vector3(pose.normal) * ATTACH_OFFSET
			assist = ((destination - Vector3(actor.p)) * 5.0 + Vector3(target.velocity)).limit_length(ArenaData.MOSQUITO_SPEED)
			actor.state = "flying"
		else:
			actor._focus_progress = 0.0
		if actor.state == "perched" and local_move.length_squared() > 0.01:
			actor.state = "flying"
		var previous: Vector3 = actor.p
		ArenaData.step_mosquito(actor, local_move, dt, str(config.map_id), assist)
		actor.p = _avoid_humans(previous, actor.p)
		actor.velocity = (Vector3(actor.p) - previous) / maxf(dt, 0.000001)
		if float(actor._focus_progress) >= 1.0:
			_attach(id)
	_update_attached()
	var commands: Array[Dictionary] = _pending_actions
	_pending_actions = []
	for command: Dictionary in commands:
		_execute_action(int(command.id), str(command.verb), command)
	for id: int in _human_ids:
		if elapsed + 0.000001 >= float(actors[id]._strike_at) and elapsed <= float(actors[id]._strike_until) + 0.000001:
			_resolve_strike(id)
		elif elapsed > float(actors[id]._strike_until) and str(actors[id]._attack.state) == "windup":
			actors[id]._attack.state = "miss"
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		var due: bool = elapsed + 0.000001 >= float(actor._next_rotation)
		if due:
			while elapsed + 0.000001 >= float(actor._next_rotation):
				actor._next_rotation = float(actor._next_rotation) + float(config.rotation_seconds)
			if bool(actor.alive) and actor.state not in ["biting", "stunned"] and int(actor._assignment_frame) != _frame:
				_assign(id)
	_update_attached()
	for id: int in _human_ids:
		actors[id].bitten = false
	var blood_this_tick := 0.0
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state == "biting" and bool(actor.alive):
			var assignment: Dictionary = actor._assignment
			actors[int(assignment.human)].bitten = true
			if config.mode == "blood":
				# Integrate only the portion after preparation; attach/detach never adds an instant award.
				var active_start: float = maxf(elapsed - dt, float(actor._bite_started) + BITE_PREPARATION)
				blood_this_tick += maxf(0.0, elapsed - active_start) * BLOOD_PER_SECOND
	_update_bite_feedback()
	_serve_assignments()
	blood += minf(blood_this_tick, dt * SHARED_BLOOD_RATE_CAP)
	if config.mode == "sleep":
		_update_tasks(dt)
	_evaluate_result()

func _execute_action(id: int, verb: String, command: Dictionary = {}) -> void:
	if not actors.has(id) or not bool(actors[id].alive) or actors[id].state == "stunned":
		return
	var actor: Dictionary = actors[id]
	if actor.role == "human":
		match verb:
			"attack", "self_swat": _attack(id, command)
			"pickup": _pickup(id)
			"drop": _drop(id)
		return
	match verb:
		"bite":
			if int(actor.help_target) != 0:
				return
			if actor.state == "biting":
				_detach(id)
			else:
				# A press can begin the attempt, but it cannot bypass sustained charge.
				actor._focus_pulse_until = elapsed + 0.20
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
			var base: Vector3 = Vector3(human.p) + Vector3(capsule.from).rotated(Vector3.UP, Pose.body_yaw(human))
			var tip: Vector3 = Vector3(human.p) + Vector3(capsule.to).rotated(Vector3.UP, Pose.body_yaw(human))
			var segment: Vector3 = tip - base
			var nearest: Vector3 = base + segment * clampf((result - base).dot(segment) / maxf(segment.length_squared(), 0.000001), 0.0, 1.0)
			var delta: Vector3 = result - nearest
			var radius: float = float(capsule.radius) + ArenaData.MOSQUITO_RADIUS
			if delta.length_squared() < radius * radius:
				var outward: Vector3 = delta.normalized()
				if outward.length_squared() < 0.01:
					outward = (previous - nearest).normalized()
				if outward.length_squared() < 0.01:
					outward = Vector3.FORWARD.rotated(Vector3.UP, Pose.body_yaw(human))
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
	actor._focus_progress = 0.0
	if not Dictionary(actor._assignment).is_empty():
		actor._forbidden = _zone_key(actor._assignment)
	actor._assignment = {}
	actor._assignment_frame = _frame
	if bool(actor.alive) and actor.state != "stunned" and not _assignment_waiters.has(id):
		_assignment_waiters.append(id)
	_serve_assignments()

func _serve_assignments() -> void:
	var occupied: Dictionary = {}
	var pressure: Dictionary = {}
	for human_id: int in _human_ids:
		pressure[human_id] = 0
	for other_id: int in _mosquito_ids:
		var other: Dictionary = actors[other_id]
		var assignment: Dictionary = other._assignment
		if bool(other.alive) and not assignment.is_empty():
			occupied[_zone_key(assignment)] = true
			pressure[int(assignment.human)] = int(pressure[int(assignment.human)]) + 1
	var waiting: Array[int] = []
	for id: int in _assignment_waiters:
		var actor: Dictionary = actors[id]
		if not bool(actor.alive) or actor.state == "stunned" or not Dictionary(actor._assignment).is_empty():
			continue
		var preferred_rear: bool = _human_ids.size() > 1 and _assignment_serial % 3 == 2
		var best: Dictionary = {}
		var best_score := 1000000
		var capacity: int = FRONT_ZONE_COUNT if _human_ids.size() == 1 else BODY_ZONES.size()
		for human_id: int in _human_ids:
			var human_priority: int = posmod(_human_ids.find(human_id) - _assignment_serial, _human_ids.size())
			for zone_index: int in range(capacity):
				var candidate: Dictionary = {"human": human_id, "zone": zone_index}
				var key: String = _zone_key(candidate)
				if occupied.has(key) or key == str(actor._forbidden):
					continue
				var zone: Dictionary = BODY_ZONES[zone_index]
				var score: int = int(pressure[human_id]) * 1000 + human_priority * 100 + posmod(zone_index - _assignment_serial * 5, capacity)
				if bool(zone.rear) != preferred_rear:
					score += 30
				if score < best_score:
					best = candidate
					best_score = score
		if best.is_empty():
			waiting.append(id)
			continue
		actor._revision = int(actor._revision) + 1
		best.revision = int(actor._revision)
		actor._assignment = best
		actor._assignment_frame = _frame
		occupied[_zone_key(best)] = true
		pressure[int(best.human)] = int(pressure[int(best.human)]) + 1
		_assignment_serial += 1
	_assignment_waiters = waiting

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
	if float(actor._focus_progress) < 1.0 or not _focus_held(actor):
		return
	var assignment: Dictionary = actor._assignment
	var pose: Dictionary = _zone_pose(assignment)
	if pose.is_empty() or not bool(_focus_info(id).can_focus):
		return
	var destination: Vector3 = Vector3(pose.p) + Vector3(pose.normal) * ATTACH_OFFSET
	if Vector3(actor.p).distance_to(destination) > BITE_DISTANCE:
		return
	if not ArenaData.clear_segment(actor.p, pose.p, str(config.map_id)) or _body_occludes(actor.p, pose.p, -1):
		return
	actor.state = "biting"
	actor._bite_started = elapsed
	actor._forbidden = "" # A successful new bite clears the previous-zone exclusion.
	actor._move = Vector3.ZERO
	actor.velocity = Vector3.ZERO
	actor.p = destination

func _detach(id: int) -> void:
	var actor: Dictionary = actors[id]
	var pose: Dictionary = _zone_pose(actor._assignment)
	actor._forbidden = _zone_key(actor._assignment)
	actor.state = "flying"
	actor._focus_suppressed = true
	actor._focus_progress = 0.0
	actor._focus_pulse_until = 0.0
	actor.velocity = Vector3.ZERO
	if not pose.is_empty():
		actor.p = ArenaData.move_body(actor.p, Vector3(pose.normal) * 0.24, false, str(config.map_id))
	_assign(id) # Deliberately never modifies _next_rotation.

func _update_attached() -> void:
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state == "biting" and bool(actor.alive):
			var pose: Dictionary = _zone_pose(actor._assignment)
			if not pose.is_empty():
				actor.p = Vector3(pose.p) + Vector3(pose.normal) * ATTACH_OFFSET

func _attack(id: int, command: Dictionary) -> void:
	var human: Dictionary = actors[id]
	if float(human.swing) > 0.0:
		return
	var aimed: Dictionary = human.duplicate(false)
	aimed.yaw = float(command.get("yaw", human.yaw))
	aimed.pitch = float(command.get("pitch", human.pitch))
	var eye: Vector3 = Pose.view_origin(aimed)
	var direction: Vector3 = Pose.view_direction(aimed)
	var stats: Dictionary = TOOL_STATS[str(human.tool)]
	var end: Vector3 = eye + direction * float(stats.reach)
	var first: Dictionary = ArenaData.ray_map(eye, end, str(config.map_id), float(stats.radius) + 0.05)
	for other_id: int in _human_ids:
		var hit: Dictionary = Pose.ray_body(actors[other_id], eye, end, other_id == id)
		if not hit.is_empty() and (first.is_empty() or float(hit.distance) < float(first.distance)):
			first = hit
			first.kind = "body"
	# Actual ray/sphere intersection is allowed; no nearest-insect selection or
	# aim correction. A near miss lands on the body/air under the clicked reticle.
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary = actors[mosquito_id]
		if not bool(mosquito.alive) or mosquito.state == "stunned":
			continue
		var hit: Dictionary = Pose.ray_capsule(eye, end, {"from": mosquito.p, "to": mosquito.p, "radius": ArenaData.MOSQUITO_RADIUS})
		if not hit.is_empty() and (first.is_empty() or float(hit.distance) < float(first.distance)):
			first = hit
			first.kind = "body"
	var point: Vector3 = end if first.is_empty() else Vector3(first.p)
	var normal: Vector3 = -direction if first.is_empty() else Vector3(first.normal)
	var local_point: Vector3 = (point - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
	var strike_tool: String = str(human.tool)
	var own_right_contact := false
	for capsule: Dictionary in Pose.collision_segments(human):
		if str(capsule.key) not in ["upperarm_r", "forearm_r", "hand_r"]:
			continue
		var segment: Vector3 = Vector3(capsule.to) - Vector3(capsule.from)
		var nearest: Vector3 = Vector3(capsule.from) + segment * clampf((local_point - Vector3(capsule.from)).dot(segment) / maxf(segment.length_squared(), 0.000001), 0.0, 1.0)
		if local_point.distance_to(nearest) <= float(capsule.radius) + float(TOOL_STATS.hands.radius):
			own_right_contact = true
	# The ray can meet the insect a few centimetres above the skin and miss the
	# narrow forearm itself. Test the clicked physical contact against the palm's
	# footprint, without consulting any insect assignment or selecting a target.
	if strike_tool != "hands" and own_right_contact and local_point.x > 0.0:
		strike_tool = "hands"
		stats = TOOL_STATS.hands
	var hand: String = "right" if strike_tool != "hands" or local_point.x <= 0.0 else "left"
	var skeleton: Dictionary = Pose.sample(human)
	var shoulder: Vector3 = Vector3(human.p) + Vector3(skeleton.shoulder_r if hand == "right" else skeleton.shoulder_l).rotated(Vector3.UP, Pose.body_yaw(human))
	# The cartoon arm has a finite shared reach. The renderer and hit detector use
	# the same resulting palm or tool-face point throughout the gesture.
	var maximum: float = Pose.ARM_REACH + float(Pose.TOOL_LENGTHS.get(strike_tool, 0.0))
	var offset: Vector3 = eye - shoulder
	var projected: float = offset.dot(direction)
	var discriminant: float = projected * projected - (offset.length_squared() - maximum * maximum)
	var ray_reach: float = maxf(0.0, -projected + sqrt(maxf(0.0, discriminant)))
	point = eye + direction * minf(eye.distance_to(point), minf(float(stats.reach), ray_reach))
	human.swing = float(stats.cooldown)
	human._strike_started = elapsed
	human._strike_at = elapsed + STRIKE_START
	human._strike_until = elapsed + STRIKE_END
	human._strike_resolved = false
	human.strike = {"id": int(command.get("seq", human._action_seq)), "origin": eye, "point": point, "direction": direction, "normal": normal, "progress": 0.0, "duration": Pose.SWING_GESTURE_SECONDS, "hand": hand, "tool": strike_tool, "active": true, "kind": "air" if first.is_empty() else str(first.kind)}
	human._attack = {"id": human.strike.id, "state": "windup", "hit": false, "point": point}

func _resolve_strike(id: int) -> void:
	var human: Dictionary = actors[id]
	if bool(human._strike_resolved) or Dictionary(human.strike).is_empty():
		return
	var stats: Dictionary = TOOL_STATS[str(human.strike.tool)]
	var eye: Vector3 = human.strike.origin
	var direction: Vector3 = human.strike.direction
	var pose: Dictionary = Pose.sample(human)
	var contact: Vector3 = Vector3(human.p) + Vector3(pose.strike_contact).rotated(Vector3.UP, Pose.body_yaw(human))
	var nearest := -1
	var nearest_distance := INF
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary = actors[mosquito_id]
		if not bool(mosquito.alive) or mosquito.state == "stunned":
			continue
		var assignment: Dictionary = mosquito._assignment
		var attached: bool = mosquito.state == "biting" and not assignment.is_empty()
		var on_self: bool = attached and int(assignment.human) == id
		if on_self and bool(BODY_ZONES[int(assignment.zone)].rear):
			continue
		var delta: Vector3 = Vector3(mosquito.p) - eye
		var along: float = delta.dot(direction)
		if along < 0.0 or along > float(stats.reach):
			continue
		var radius: float = float(stats.radius) + ArenaData.MOSQUITO_RADIUS
		if (delta - direction * along).length() > radius or contact.distance_to(mosquito.p) > radius:
			continue
		if not ArenaData.clear_segment(eye, mosquito.p, str(config.map_id)) or not ArenaData.clear_segment(contact, mosquito.p, str(config.map_id)):
			continue
		var blocked := false
		for other_id: int in _human_ids:
			var hit: Dictionary = Pose.ray_body(actors[other_id], eye, mosquito.p, other_id == id, other_id == id)
			if not hit.is_empty() and float(hit.distance) < eye.distance_to(mosquito.p) - ArenaData.MOSQUITO_RADIUS * 0.5:
				blocked = true
				break
		if blocked:
			continue
		if attached and not on_self:
			var zone: Dictionary = _zone_pose(assignment)
			if (eye - Vector3(zone.p)).dot(zone.normal) <= 0.02:
				continue
		var distance: float = contact.distance_to(mosquito.p)
		if distance < nearest_distance:
			nearest = mosquito_id
			nearest_distance = distance
	if nearest >= 0:
		_kill(nearest)
		human._strike_resolved = true
		human._attack.state = "hit"
		human._attack.hit = true

func _update_bite_feedback() -> void:
	for id: int in _human_ids:
		var human: Dictionary = actors[id]
		var ids: Array[String] = []
		var rear := false
		var lateral := 0.0
		for mosquito_id: int in _mosquito_ids:
			var mosquito: Dictionary = actors[mosquito_id]
			if not bool(mosquito.alive) or mosquito.state != "biting" or Dictionary(mosquito._assignment).is_empty() or int(mosquito._assignment.human) != id:
				continue
			ids.append(str(mosquito_id))
			rear = rear or bool(BODY_ZONES[int(mosquito._assignment.zone)].rear)
			lateral += (Vector3(mosquito.p) - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human)).x
		var signature: String = ",".join(ids)
		if signature != str(human._bite_signature):
			human._bite_signature = signature
			human._bite_feedback.id = int(human._bite_feedback.id) + 1
		human._bite_feedback.active = not ids.is_empty()
		human._bite_feedback.count = ids.size()
		human._bite_feedback.side = "rear" if rear else ("front" if absf(lateral) < 0.03 else ("left" if lateral < 0.0 else "right"))

func _body_occludes(from: Vector3, to: Vector3, ignored_human: int) -> bool:
	for id: int in _human_ids:
		if id == ignored_human:
			continue
		var human: Dictionary = actors[id]
		var start_local: Vector3 = (from - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
		var end_local: Vector3 = (to - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
		for capsule: Dictionary in Pose.collision_segments(human):
			var closest: PackedVector3Array = Geometry3D.get_closest_points_between_segments(start_local, end_local, capsule.from, capsule.to)
			if closest[0].distance_to(closest[1]) < float(capsule.radius) - 0.008:
				return true
	return false

func _kill(id: int) -> void:
	var actor: Dictionary = actors[id]
	if not bool(actor.alive) or actor.state == "stunned":
		return
	actor._forbidden = _zone_key(actor._assignment)
	actor._assignment = {}
	_assignment_waiters.erase(id)
	actor._move = Vector3.ZERO
	actor.velocity = Vector3.ZERO
	actor._interact = false
	actor.help_target = 0
	actor._focus_progress = 0.0
	actor._focus_pulse_until = 0.0
	actor._focus_suppressed = true
	actor._respawn_at = 0.0
	actor.lives = 0
	if config.mode == "survival":
		actor.alive = false
		actor.state = "dead"
	else:
		actor.alive = true
		actor.state = "stunned"
		actor.grounded = false
		actor._stun_remaining = STUN_SECONDS
		actor._stun_helped = false
	# Shared blood persists through a fall, help, recovery and elimination.

func _recover_stun(id: int) -> void:
	var actor: Dictionary = actors[id]
	for helper_id: int in _mosquito_ids:
		if int(actors[helper_id].help_target) == id:
			actors[helper_id].help_target = 0
			actors[helper_id]._focus_suppressed = true
	actor.state = "flying"
	actor._stun_remaining = 0.0
	actor._stun_helped = false
	actor.grounded = false
	actor.velocity = Vector3.ZERO
	actor._move = Vector3.ZERO
	actor._interact = false
	actor._last_input = -100.0
	actor._focus_progress = 0.0
	actor._focus_pulse_until = 0.0
	actor._focus_suppressed = true
	_assign(id) # Current floor position and fixed rotation calendar are preserved.

func _help_info(id: int) -> Dictionary:
	var helper: Dictionary = actors[id]
	var result := {"state": "idle", "target": 0, "distance": 0.0, "remaining": 0.0, "progress": 0.0}
	if not bool(helper.alive) or helper.state in ["stunned", "dead", "biting"] or config.mode == "survival":
		return result
	var best_distance := HELP_DISTANCE
	for target_id: int in _mosquito_ids:
		var target: Dictionary = actors[target_id]
		if target_id == id or target.state != "stunned":
			continue
		var delta: Vector3 = Vector3(target.p) - Vector3(helper.p)
		var distance: float = delta.length()
		if distance > best_distance:
			continue
		if distance > 0.12 and ArenaData.flight_direction(Vector3.FORWARD, float(helper.yaw), float(helper.pitch)).dot(delta.normalized()) < HELP_FACING:
			continue
		if not ArenaData.clear_segment(helper.p, target.p, str(config.map_id)) or _body_occludes(helper.p, target.p, -1):
			continue
		best_distance = distance
		result.target = target_id
		result.distance = distance
		result.remaining = maxf(0.0, float(target._stun_remaining))
		result.progress = 1.0 - float(result.remaining) / STUN_SECONDS
		result.state = "helping" if bool(helper._interact) and elapsed - float(helper._last_input) <= INPUT_TIMEOUT else "ready"
	return result

func _update_help(dt: float) -> void:
	var helped: Dictionary = {}
	for id: int in _mosquito_ids:
		var info: Dictionary = _help_info(id)
		if str(info.state) == "helping":
			actors[id].help_target = int(info.target)
			actors[id]._focus_progress = 0.0
			helped[int(info.target)] = true
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state != "stunned":
			continue
		actor._stun_helped = helped.has(id)
		actor._stun_remaining = maxf(0.0, float(actor._stun_remaining) - dt * (HELP_RATE if bool(actor._stun_helped) else 1.0))
		if float(actor._stun_remaining) <= 0.000001:
			_recover_stun(id)

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
			var near: bool = Vector3(human.p).distance_to(Vector3(task.p)) < 1.20 and ArenaData.clear_segment(Vector3(human.p) + Vector3.UP * 0.65, Vector3(task.p) + Vector3.UP * 0.65, str(config.map_id))
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
			var round_remaining: float = maxf(0.0, float(config.round_seconds) - elapsed)
			var task_budget: float = minf(float(human._deadline), round_remaining)
			if scheduled <= last_task_start(config) + 0.000001 and task_budget + 0.000001 >= minimum_task_deadline(float(config.task_work)) and Dictionary(human._task).is_empty():
				var station_id: int = (int(human._tasks_given) + index) % _map_data.stations.size()
				var station: Dictionary = _map_data.stations[station_id]
				human._task = {
					"name": station.name, "station": station_id, "p": station.p,
					"remaining": task_budget, "progress": 0.0, "work": float(config.task_work),
				}
				human._tasks_given = int(human._tasks_given) + 1

func _evaluate_result() -> void:
	if phase != "playing":
		return
	var alive := 0
	for id: int in _mosquito_ids:
		if bool(actors[id].alive):
			alive += 1
	if config.mode == "blood" and blood + 0.000001 >= float(config.blood_goal):
		blood = float(config.blood_goal)
		_finish("mosquito", "Los mosquitos alcanzaron la cuota compartida.")
	elif config.mode == "survival" and alive == 0:
		_finish("human", "Los humanos eliminaron a todos los mosquitos.")
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
	_assignment_waiters.clear()
	for id: int in actors:
		actors[id]._assignment = {}
		actors[id]._move = Vector3.ZERO
		actors[id]._interact = false
		actors[id].bitten = false
		actors[id].threatened = false
		actors[id]._focus_progress = 0.0
		actors[id]._focus_pulse_until = 0.0
		actors[id].help_target = 0
		actors[id]._stun_remaining = 0.0
		actors[id]._stun_helped = false
		if actors[id].state in ["biting", "stunned"]:
			actors[id].state = "flying"

func public_snapshot() -> Dictionary:
	var public_actors: Dictionary = {}
	for id: int in actors:
		var actor: Dictionary = actors[id]
		# Explicit allowlist: never serialize hidden target/zone reservations or tasks.
		public_actors[id] = {
			"name": actor.name, "role": actor.role, "p": actor.p, "yaw": actor.yaw,
			"appearance": Dictionary(actor.appearance).duplicate(true),
			"body_yaw": actor.body_yaw, "inspecting": actor.inspecting, "strike": Dictionary(actor.strike).duplicate(true),
			"pitch": actor.pitch, "state": actor.state, "alive": actor.alive,
			"help_target": actor.help_target,
			"swing": actor.swing, "bitten": actor.bitten, "threatened": actor.threatened, "tool": actor.tool, "lives": actor.lives,
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
		var attack: Dictionary = Dictionary(actor._attack).duplicate(true)
		attack.recovery = maxf(0.0, float(actor.swing))
		return {"task": Dictionary(actor._task).duplicate(true), "deadline": actor._deadline, "failures": actor._failures, "attack": attack, "bite_feedback": Dictionary(actor._bite_feedback).duplicate(true)}
	var assignment: Dictionary = {}
	if bool(actor.alive) and not Dictionary(actor._assignment).is_empty():
		assignment = Dictionary(actor._assignment).duplicate(true)
		assignment.merge(_zone_pose(assignment))
	var stun := {"active": actor.state == "stunned", "remaining": maxf(0.0, float(actor._stun_remaining)), "total": STUN_SECONDS, "helped": bool(actor._stun_helped)}
	return {"assignment": assignment, "state": actor.state, "focus": _focus_info(id), "stun": stun, "help": _help_info(id), "respawn_left": 0.0, "lives": actor.lives}

func _focus_held(actor: Dictionary) -> bool:
	return not bool(actor._focus_suppressed) and ((bool(actor._interact) and elapsed - float(actor._last_input) <= INPUT_TIMEOUT) or elapsed < float(actor._focus_pulse_until))

func _focus_info(id: int) -> Dictionary:
	var actor: Dictionary = actors[id]
	var result: Dictionary = {"state": "idle", "progress": float(actor._focus_progress), "distance": 0.0, "can_focus": false, "reason": "Acercate a tu marca"}
	if actor.state == "stunned":
		result.state = "stunned"
		result.progress = 0.0
		result.reason = "Aturdido: un compañero puede ayudarte"
		return result
	if int(actor.help_target) != 0:
		result.state = "helping"
		result.progress = 0.0
		result.reason = "Ayudando a tu compañero"
		return result
	if not bool(actor.alive) or Dictionary(actor._assignment).is_empty():
		result.progress = 0.0
		if bool(actor.alive):
			result.state = "waiting"
			result.reason = "Esperando una zona disponible"
		return result
	if actor.state == "biting":
		result.state = "attached"
		result.progress = 1.0
		result.reason = "Pulsá E otra vez para desprenderte"
		return result
	var pose: Dictionary = _zone_pose(actor._assignment)
	var delta: Vector3 = Vector3(pose.p) - Vector3(actor.p)
	result.distance = delta.length()
	var forward: Vector3 = ArenaData.flight_direction(Vector3.FORWARD, float(actor.yaw), float(actor.pitch))
	if bool(actor._focus_suppressed):
		result.reason = "Soltá E antes de concentrarte otra vez"
	elif delta.length() > FOCUS_DISTANCE:
		result.reason = "Acercate a tu marca"
	elif (Vector3(actor.p) - Vector3(pose.p)).dot(pose.normal) < -0.015:
		result.reason = "Rodeá el cuerpo hasta el lado de la marca"
	elif forward.dot(delta.normalized()) < (0.25 if float(actor._focus_progress) > 0.0 else 0.40):
		result.reason = "Apuntá hacia tu marca"
	elif not ArenaData.clear_segment(actor.p, pose.p, str(config.map_id)) or _body_occludes(actor.p, pose.p, -1):
		result.reason = "La marca está detrás de un obstáculo"
	else:
		result.can_focus = true
		result.state = "charging" if _focus_held(actor) else "ready"
		result.reason = "Mantené E para estabilizarte y picar"
	if not bool(result.can_focus) and _focus_held(actor):
		result.state = "blocked"
	return result
