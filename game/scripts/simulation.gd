class_name GameSimulation
extends RefCounted

## Authoritative, deterministic rules. This object never trusts client positions.
## Numbers below are reversible prototype hypotheses, not tested balance.
const ArenaData = preload("res://scripts/arena.gd")
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
const InsectPose = preload("res://scripts/mosquito_pose.gd")
const ToolData = preload("res://scripts/tool_catalog.gd")
const Projectiles = preload("res://scripts/projectile_collision.gd")
const DoorCatalogData = preload("res://scripts/door_catalog.gd")
const DoorStateScript = preload("res://scripts/door_state.gd")
const DOOR_REACH := 2.2
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
const TOOL_STATS = ToolData.MELEE_STATS
const PICKUP_SPAWNS = Maps.HOUSE.pickups
# Eight readable front surfaces; a solo human can aim at every one. Additional
# insects wait privately instead of receiving marks hidden from the owner.
const FRONT_ZONE_COUNT := 8
const BODY_ZONES := [
	{"label": "Pecho izquierdo", "bone": "torso", "p": Vector3(-0.10, 1.16, -0.30), "rear": false},
	{"label": "Pecho derecho", "bone": "torso", "p": Vector3(0.10, 1.16, -0.30), "rear": false},
	{"label": "Abdomen izquierdo", "bone": "torso", "p": Vector3(-0.065, 0.94, -0.30), "rear": false},
	{"label": "Abdomen derecho", "bone": "torso", "p": Vector3(0.065, 0.94, -0.30), "rear": false},
	{"label": "Antebrazo izquierdo", "bone": "forearm_l", "p": Vector3(-0.24, 0.98, -0.48), "rear": false},
	{"label": "Antebrazo derecho", "bone": "forearm_r", "p": Vector3(0.24, 0.98, -0.48), "rear": false},
	{"label": "Muslo izquierdo", "bone": "thigh_l", "p": Vector3(-0.135, 0.59, -0.20), "rear": false},
	{"label": "Muslo derecho", "bone": "thigh_r", "p": Vector3(0.135, 0.59, -0.20), "rear": false},
	{"label": "Espalda izquierda", "bone": "torso", "p": Vector3(-0.12, 1.09, 0.30), "rear": true},
	{"label": "Espalda derecha", "bone": "torso", "p": Vector3(0.12, 1.09, 0.30), "rear": true},
]

var actors: Dictionary = {}
var door_state = DoorStateScript.new()
var doors: Dictionary = {}
var pickups: Dictionary = {}
var _projectiles: Dictionary = {}
const THROW_HOLD_TIMEOUT := 3.0
const PROJECTILE_STEP := 1.0/60.0
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
var _human_pose_cache: Dictionary = {}
var _neutral_pose: Dictionary = {}

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
	_human_pose_cache.clear()
	_neutral_pose=Pose.sample({})
	actors.clear()
	pickups.clear()
	_projectiles.clear()
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
	door_state.reset(str(config.map_id))
	doors = door_state.states
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
			"impact":{"id":0,"tool":"hands","kind":"melee","material":"skin"},
			"throw_gesture":{"id":-1,"state":"idle","progress":0.0,"power":0.0,"direction":Vector3.FORWARD,"tool":"hands"},
			"_throw":{},"_throw_recovery":0.0,"_throw_reason":"",
			"_bite_feedback": {"id": 0, "active": false, "count": 0, "side": "front"}, "_bite_signature": "",
			"velocity": Vector3.ZERO, "grounded": human, "sprinting": false, "crouching": false,
			"crouch_amount": 0.0, "motion_phase": 0.0, "motion_speed": 0.0,
			"_sprint": false, "_crouch": false, "_jump": false, "_jump_held": false,
			"lives": 1 if not human and config.mode == "survival" else 0, "_respawn_at": 0.0,
			"_move": Vector3.ZERO, "_interact": false, "_last_input": -100.0,
			"_input_seq": -1, "_action_seq": -1, "_door_at": -100.0, "_assignment": {}, "_revision": 0,
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
		pickup.state = "ground"
		pickup.velocity = Vector3.ZERO
		pickup.owner = 0
		pickup.ttl = 0.0
		pickup.rotation = pickup.get("rotation",Vector3(PI*.5,float(pickup.get("yaw",0.0)),0.0))
		pickup.impact_id = 0
		pickup.impact_kind = "ground"
		pickup.impact_material = "wood"
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
	if verb not in ["bite", "attack", "self_swat", "perch", "pickup", "drop", "door", "throw_start", "throw_release", "throw_cancel"]:
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
	if verb=="throw_cancel":
		for index: int in range(_pending_actions.size()-1,-1,-1):
			if int(_pending_actions[index].id)==id and str(_pending_actions[index].verb) in ["throw_start","throw_release"]:_pending_actions.remove_at(index)
		_cancel_throw(id,"cancelled")
		return
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
	var previous_humans: Dictionary = {}
	var previous_insects: Dictionary = {}
	for id: int in _human_ids:
		if not _projectiles.is_empty() or (not bool(actors[id]._strike_resolved) and elapsed<=float(actors[id]._strike_until)):
			previous_humans[id]=actors[id].duplicate(true)
	for id: int in _mosquito_ids:
		var insect: Dictionary=actors[id]
		if not previous_humans.is_empty():
			previous_insects[id]=_impact_actor(insect)
		insect._focus_previous_point=Vector3(INF,INF,INF)
		if float(insect._focus_progress)>0.0 and insect.state=="flying" and not Dictionary(insect._assignment).is_empty():
			insect._focus_previous_point=_zone_pose(insect._assignment).p
	_frame += 1
	elapsed += dt
	door_state.step(dt, actors)
	for id: int in actors:
		var actor: Dictionary = actors[id]
		actor.swing = maxf(0.0, float(actor.swing) - dt)
		actor.threatened = false
		if not Dictionary(actor.strike).is_empty():
			actor.strike.progress = clampf((elapsed - float(actor._strike_started)) / maxf(.01,float(actor.strike.get("duration",Pose.SWING_GESTURE_SECONDS))), 0.0, 1.0)
			actor.strike.active = float(actor.strike.progress) < 1.0
		if not bool(actor.alive):
			continue
		if actor.role == "human":
			var fresh: bool = elapsed - float(actor._last_input) <= INPUT_TIMEOUT
			var local_move: Vector3 = actor._move if fresh else Vector3.ZERO
			ArenaData.step_human(actor, {"move": local_move, "yaw": actor.yaw, "pitch": actor.pitch, "sprint": bool(actor._sprint) and fresh, "crouch": bool(actor._crouch) and fresh, "jump": bool(actor._jump) and fresh}, dt, str(config.map_id), doors)
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		actor.help_target = 0
		if actor.state == "stunned":
			var previous: Vector3 = actor.p
			ArenaData.step_stunned(actor, dt, str(config.map_id), doors)
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
			ArenaData.step_mosquito(actor, local_move, dt, str(config.map_id), null, doors)
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
		ArenaData.step_mosquito(actor, local_move, dt, str(config.map_id), assist, doors)
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
		_update_throw(id)
	_update_projectiles(dt,previous_humans,previous_insects)
	for id: int in _human_ids:
		_resolve_strike(id,previous_humans.get(id,{}),previous_insects,elapsed-dt)
		if elapsed>float(actors[id]._strike_until) and str(actors[id]._attack.state)=="windup":
			actors[id]._attack.state="miss"
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
			"attack", "self_swat":
				_cancel_throw(id,"melee")
				_attack(id, command)
			"pickup": _pickup(id)
			"drop": _drop(id)
			"door": _door_action(id, command)
			"throw_start": _start_throw(id,command)
			"throw_release": _release_throw(id,command)
			"throw_cancel": _cancel_throw(id,"cancelled")
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

func _door_info(id: int, aim_yaw: float = NAN, aim_pitch: float = NAN) -> Dictionary:
	if phase != "playing" or not actors.has(id):
		return {}
	var actor: Dictionary = actors[id]
	if actor.role != "human" or not bool(actor.alive):
		return {}
	var aimed: Dictionary = actor.duplicate(false)
	if is_finite(aim_yaw) and is_finite(aim_pitch):
		aimed.yaw = aim_yaw
		aimed.pitch = aim_pitch
	var eye: Vector3 = Pose.view_origin(aimed)
	var point: Vector3 = eye+Pose.view_direction(aimed)*DOOR_REACH
	var hit: Dictionary = ArenaData.ray_map(eye,point,str(config.map_id),0.0,doors)
	if hit.is_empty() or str(hit.kind) != "door" or _body_occludes(eye,hit.p,id):
		return {}
	var door_id: String = hit.door_id
	var state: Dictionary = doors[door_id]
	var cooling: bool = elapsed-float(actor._door_at)<DoorStateScript.COOLDOWN or elapsed-float(door_state.last_toggle.get(door_id,-100.0))<DoorStateScript.COOLDOWN
	var available: bool = (not bool(state.moving) or bool(state.blocked)) and not cooling
	return {"kind":"door","door_id":door_id,"label":DoorCatalogData.DEFINITIONS[door_id].label,"verb":"Cerrar" if float(state.target_angle)>0.1 else "Abrir","p":hit.p,"can_use":available,"reason":"" if available else "Esperá que termine de moverse"}

func _door_action(id: int, command: Dictionary) -> void:
	var actor: Dictionary = actors[id]
	var info: Dictionary = _door_info(id,float(command.get("yaw",actor.yaw)),float(command.get("pitch",actor.pitch)))
	if info.is_empty() or not bool(info.can_use):
		return
	if door_state.toggle(str(info.door_id),elapsed):
		actor._door_at = elapsed

func _avoid_humans(previous: Vector3, position: Vector3) -> Vector3:
	var result: Vector3 = position
	# Capsule endpoints model torso, head and legs; soft projection lets insects
	# slide around bodies without treating the entire human as a rectangular wall.
	for id: int in _human_ids:
		var human: Dictionary = actors[id]
		if not ArenaData.human_envelope(human).grow(ArenaData.MOSQUITO_RADIUS+.005).has_point(result):
			continue
		for capsule: Dictionary in _pose_bundle(id).capsules:
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
				result = ArenaData.move_body(result, outward * (radius - delta.length() + 0.005), false, str(config.map_id), ArenaData.HUMAN_HEIGHT, doors)
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
	var normals: Array[Vector3] = [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.BACK,Vector3.FORWARD]
	for obstacle: AABB in ArenaData.obstacles(str(config.map_id)):
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
					var normal := Vector3.ZERO
					normal[axis] = -1.0 if edge == expanded.position[axis] else 1.0
					normals.append(normal)
	var best_distance := 0.32
	var best: Vector3 = position
	var found := false
	var best_normal := Vector3.UP
	for index: int in range(candidates.size()):
		var candidate: Vector3 = candidates[index]
		var distance: float = position.distance_to(candidate)
		# Leaves are not perch supports. Even a fixed wall/floor candidate must
		# leave the insect's whole radius clear of every current dynamic leaf.
		if DoorCatalogData.body_blocked(AABB(candidate-Vector3.ONE*radius,Vector3.ONE*radius*2.0),doors,str(config.map_id)) or not DoorCatalogData.ray_doors(position,candidate,doors,str(config.map_id),radius).is_empty():
			continue
		if distance <= best_distance and ArenaData.clear_segment(position, candidate, str(config.map_id), doors):
			best_distance = distance
			best = candidate
			best_normal = normals[index]
			found = true
	if found:
		actor.p = best
		actor.state = "perched"
		actor._surface_normal = best_normal
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

# Each simulation owns its cache. Exact dependency arrays invalidate on any
# within-tick pose change; no frame-only key or shared mutable actor state.
func _pose_bundle(id: int) -> Dictionary:
	var human: Dictionary=actors[id]
	var key: Array=Pose.cache_key(human)
	var cached: Dictionary=_human_pose_cache.get(id,{})
	if not cached.is_empty() and cached.key==key:
		return cached
	var posed: Dictionary=Pose.sample(human)
	cached={"key":key,"pose":posed,"capsules":Pose.collision_segments(human,posed),"zones":{}}
	_human_pose_cache[id]=cached
	return cached

func _zone_pose(assignment: Dictionary) -> Dictionary:
	if assignment.is_empty() or not actors.has(int(assignment.human)):
		return {}
	var human_id: int=int(assignment.human)
	var zone_id: int=int(assignment.zone)
	var cached: Dictionary=_pose_bundle(human_id)
	if not cached.zones.has(zone_id):
		cached.zones[zone_id]=Pose.zone_pose(actors[human_id],BODY_ZONES[zone_id],cached.pose,cached.capsules,_neutral_pose)
	return Dictionary(cached.zones[zone_id]).duplicate(false)

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
	if not ArenaData.clear_segment(actor.p, pose.p, str(config.map_id), doors) or _body_occludes(actor.p, pose.p, -1):
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
		actor.p = ArenaData.move_body(actor.p, Vector3(pose.normal) * 0.24, false, str(config.map_id), ArenaData.HUMAN_HEIGHT, doors)
	_assign(id) # Deliberately never modifies _next_rotation.

func _update_attached() -> void:
	for id: int in _mosquito_ids:
		var actor: Dictionary = actors[id]
		if actor.state == "biting" and bool(actor.alive):
			var pose: Dictionary = _zone_pose(actor._assignment)
			if not pose.is_empty():
				actor.p = Vector3(pose.p) + Vector3(pose.normal) * ATTACH_OFFSET

func _strike_plan(id: int, command: Dictionary = {}) -> Dictionary:
	var human: Dictionary = actors[id]
	var aimed: Dictionary = human.duplicate(false)
	aimed.yaw = float(command.get("yaw", human.yaw))
	aimed.pitch = float(command.get("pitch", human.pitch))
	var eye: Vector3 = Pose.view_origin(aimed)
	var direction: Vector3 = Pose.view_direction(aimed)
	var stats: Dictionary = TOOL_STATS[str(human.tool)]
	var skeleton: Dictionary = _pose_bundle(id).pose
	var left_shoulder: Vector3=Vector3(human.p)+Vector3(skeleton.shoulder_l).rotated(Vector3.UP,Pose.body_yaw(human))
	var right_shoulder: Vector3=Vector3(human.p)+Vector3(skeleton.shoulder_r).rotated(Vector3.UP,Pose.body_yaw(human))
	# Catalog reach is shoulder -> face, including the shaft exactly once.
	# This initial eye-ray bound only gathers contacts; the chosen shoulder's
	# sphere intersection below determines the actual attainable depth.
	var query_reach: float=float(stats.reach)+maxf(eye.distance_to(left_shoulder),eye.distance_to(right_shoulder))
	var end: Vector3 = eye + direction * query_reach
	var first: Dictionary = ArenaData.ray_map(eye, end, str(config.map_id), float(stats.radius), doors)
	for other_id: int in _human_ids:
		if ArenaData.human_envelope(actors[other_id]).intersects_segment(eye,end)==null:
			continue
		var hit: Dictionary = Pose.ray_body(actors[other_id], eye, end, other_id == id)
		if not hit.is_empty() and (first.is_empty() or float(hit.distance) < float(first.distance)):
			first = hit
			first.kind = "body"
	# The face sweeps along the captured manual ray. A visible insect inside
	# that physical footprint may set depth, but never rotates or snaps the ray.
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary = actors[mosquito_id]
		if not bool(mosquito.alive) or mosquito.state=="stunned":
			continue
		if not _impact_near_ray(mosquito,eye,direction,query_reach,float(stats.radius)):
			continue
		for candidate: Dictionary in InsectPose.ray_candidates(InsectPose.collision_segments(_impact_actor(mosquito)),eye,direction,query_reach,float(stats.radius)):
			if not _strike_visible(id,mosquito,eye,candidate.visible): continue
			var along: float=candidate.along
			if first.is_empty() or along<float(first.distance):
				first={"p":eye+direction*along,"normal":-direction,"distance":along,"kind":"body"}
	var point: Vector3 = end if first.is_empty() else Vector3(first.p)
	var normal: Vector3 = -direction if first.is_empty() else Vector3(first.normal)
	var local_point: Vector3 = (point - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
	var strike_tool: String = str(human.tool)
	var own_right_contact := false
	for capsule: Dictionary in _pose_bundle(id).capsules:
		if Pose.limb_key(str(capsule.key)) not in ["upperarm_r", "forearm_r", "hand_r"]:
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
	var shoulder: Vector3 = Vector3(human.p) + Vector3(skeleton.shoulder_r if hand == "right" else skeleton.shoulder_l).rotated(Vector3.UP, Pose.body_yaw(human))
	# The cartoon arm has a finite shared reach. The renderer and hit detector use
	# the same resulting palm or tool-face point throughout the gesture.
	var maximum: float = float(stats.reach)
	var offset: Vector3 = eye - shoulder
	var projected: float = offset.dot(direction)
	var discriminant: float = projected * projected - (offset.length_squared() - maximum * maximum)
	var ray_reach: float = maxf(0.0, -projected + sqrt(discriminant)) if discriminant>=0.0 else 0.0
	point = eye + direction * minf(eye.distance_to(point),ray_reach)
	return {"origin":eye,"point":point,"direction":direction,"normal":normal,"hand":hand,"tool":strike_tool,"radius":float(stats.radius),"reach":ray_reach,"reach_origin":"eye","kind":"air" if first.is_empty() else str(first.kind)}

func _attack(id: int, command: Dictionary) -> void:
	var human: Dictionary = actors[id]
	if float(human.swing)>0.0:
		return
	var plan: Dictionary = _strike_plan(id,command)
	var stats: Dictionary = TOOL_STATS[str(plan.tool)]
	human.swing = float(stats.cooldown)
	human._strike_started = elapsed
	human._strike_at = elapsed + float(stats.get("damage_start",STRIKE_START))
	human._strike_until = elapsed + float(stats.get("damage_end",STRIKE_END))
	human._strike_resolved = false
	human.strike = plan.duplicate(true)
	human.strike.merge({"id":int(command.get("seq",human._action_seq)),"progress":0.0,"duration":float(stats.get("gesture",Pose.SWING_GESTURE_SECONDS)),"active":true},true)
	human._attack={"id":human.strike.id,"state":"windup","hit":false,"point":plan.point}

func _strike_visible(id: int, mosquito: Dictionary, eye: Vector3, position: Vector3) -> bool:
	if not bool(mosquito.alive) or mosquito.state=="stunned":
		return false
	var assignment: Dictionary = mosquito._assignment
	var attached: bool = mosquito.state=="biting" and not assignment.is_empty()
	var on_self: bool = attached and int(assignment.human)==id
	if on_self and bool(BODY_ZONES[int(assignment.zone)].rear):
		return false
	# The decorative-length impact anatomy cannot reach back through a leaf
	# while its locomotion core remains behind that obstacle.
	if not ArenaData.clear_segment(eye,mosquito.p,str(config.map_id),doors) or not ArenaData.clear_segment(eye,position,str(config.map_id),doors):
		return false
	for other_id: int in _human_ids:
		if ArenaData.human_envelope(actors[other_id]).intersects_segment(eye,position)==null:
			continue
		var hit: Dictionary = Pose.ray_body(actors[other_id],eye,position,other_id==id,other_id==id)
		if not hit.is_empty() and float(hit.distance)<eye.distance_to(position)-ArenaData.MOSQUITO_RADIUS*.5:
			return false
	if attached and not on_self:
		var zone: Dictionary = _zone_pose(assignment)
		if (eye-Vector3(zone.p)).dot(zone.normal)<=.02:
			return false
	return true

func _surface_normal(actor: Dictionary) -> Vector3:
	if bool(actor.alive) and actor.state=="biting":
		return _zone_pose(actor._assignment).get("normal",Vector3.ZERO)
	if bool(actor.alive) and actor.state=="perched":
		return actor.get("_surface_normal",Vector3.UP)
	return Vector3.ZERO

func _impact_actor(actor: Dictionary) -> Dictionary:
	# Only public orientation inputs. A free assignment never changes anatomy.
	return {"p":actor.p,"yaw":actor.yaw,"pitch":actor.pitch,"velocity":actor.velocity,"state":actor.state,"surface_normal":_surface_normal(actor)}

func _impact_near_ray(actor: Dictionary, eye: Vector3, direction: Vector3, reach: float, radius: float) -> bool:
	var nearest: Vector3=InsectPose.closest_axis(actor.p,eye,eye+direction*reach)
	return nearest.distance_squared_to(actor.p)<=pow(radius+InsectPose.BOUND_RADIUS,2.0)

# Subsample the actual shared gesture, then sweep relative face/insect motion
# continuously inside each interval. A fast insect cannot tunnel between ticks.
func _strike_actor_at(human: Dictionary, previous: Dictionary, from_time: float, at_time: float) -> Dictionary:
	var actor: Dictionary=human.duplicate(false)
	var weight: float=clampf((at_time-from_time)/maxf(elapsed-from_time,.000001),0.0,1.0)
	actor.p=Vector3(previous.get("p",human.p)).lerp(human.p,weight)
	actor.body_yaw=lerp_angle(Pose.body_yaw(previous),Pose.body_yaw(human),weight)
	for key: String in ["crouch_amount","motion_blend","air_blend","land_blend","turn_blend","pose_time"]:
		actor[key]=lerpf(float(previous.get(key,human.get(key,0.0))),float(human.get(key,0.0)),weight)
	actor.motion_phase=lerp_angle(float(previous.get("motion_phase",0.0)),float(human.motion_phase),weight)
	actor.strike=Dictionary(human.strike).duplicate(false)
	actor.strike.progress=clampf((at_time-float(human._strike_started))/maxf(.01,float(human.strike.get("duration",Pose.SWING_GESTURE_SECONDS))),0.0,1.0)
	return actor

func _strike_contact(actor: Dictionary) -> Vector3:
	return Vector3(actor.p)+Vector3(Pose.sample(actor).strike_contact).rotated(Vector3.UP,Pose.body_yaw(actor))

func _resolve_strike(id: int, previous: Dictionary = {}, previous_insects: Dictionary = {}, from_time: float = -1.0) -> void:
	var human: Dictionary=actors[id]
	if bool(human._strike_resolved) or Dictionary(human.strike).is_empty():
		return
	if from_time<0.0:
		from_time=elapsed
	var start: float=maxf(from_time,float(human._strike_at))
	var end: float=minf(elapsed,float(human._strike_until))
	if start>end+.000001:
		return
	var samples: int=maxi(1,int(ceilf((end-start)/.008)))
	var a: Vector3=_strike_contact(_strike_actor_at(human,previous,from_time,start))
	for index: int in range(samples):
		var t0: float=lerpf(start,end,float(index)/samples)
		var t1: float=lerpf(start,end,float(index+1)/samples)
		var b: Vector3=_strike_contact(_strike_actor_at(human,previous,from_time,t1))
		var selected: int=_swept_strike_hit(id,human.strike,a,b,previous_insects,from_time,t0,t1)
		if selected>=0:
			_kill(selected,str(human.strike.tool),"melee")
			human._strike_resolved=true
			human._attack.state="hit"
			human._attack.hit=true
			return
		a=b

func _swept_strike_hit(id: int, plan: Dictionary, a: Vector3, b: Vector3, previous_insects: Dictionary, from_time: float, t0: float, t1: float) -> int:
	var stats: Dictionary=TOOL_STATS[str(plan.tool)]
	var eye: Vector3=plan.origin
	var direction: Vector3=plan.direction
	var nearest := -1
	var nearest_time := INF
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary=actors[mosquito_id]
		if not bool(mosquito.alive) or mosquito.state=="stunned":
			continue
		# An insect carried on the striking arm is not struck merely because
		# that arm enters the aiming corridor. Self-defence on that forearm
		# requires the opposite hand, selected by the same manual ray plan.
		var attachment: Dictionary=mosquito._assignment
		if mosquito.state=="biting" and not attachment.is_empty() and int(attachment.human)==id:
			var suffix: String="_r" if str(plan.hand)=="right" else "_l"
			var bone: String=str(BODY_ZONES[int(attachment.zone)].bone)
			if bone in ["forearm"+suffix,"shoulder"+suffix,"upperarm"+suffix,"hand"+suffix]:
				continue
		var current_pose: Dictionary=_impact_actor(mosquito)
		var before_value: Variant=previous_insects.get(mosquito_id,current_pose)
		var before_pose: Dictionary=current_pose.duplicate(false)
		if before_value is Dictionary: before_pose=before_value
		elif before_value is Vector3: before_pose.p=before_value # Legacy fixture/API compatibility.
		var before: Vector3=before_pose.p
		var u0: float=clampf((t0-from_time)/maxf(elapsed-from_time,.000001),0.0,1.0)
		var u1: float=clampf((t1-from_time)/maxf(elapsed-from_time,.000001),0.0,1.0)
		var m0: Vector3=before.lerp(mosquito.p,u0)
		var m1: Vector3=before.lerp(mosquito.p,u1)
		var relative: Vector3=m0-a
		var change: Vector3=(m1-b)-relative
		var broad_fraction: float=clampf(-relative.dot(change)/maxf(change.length_squared(),.000000001),0.0,1.0)
		if (relative+change*broad_fraction).length()>float(stats.radius)+InsectPose.BOUND_RADIUS:
			continue
		var q0 := InsectPose.orientation(before_pose).get_rotation_quaternion()
		var q1 := InsectPose.orientation(current_pose).get_rotation_quaternion()
		var capsules0 := InsectPose.segments_at(m0,Basis(q0.slerp(q1,u0)))
		var capsules1 := InsectPose.segments_at(m1,Basis(q0.slerp(q1,u1)))
		for index: int in range(capsules1.size()):
			var hit := InsectPose.swept_contact(a,b,capsules0[index],capsules1[index],float(stats.radius))
			if hit.is_empty(): continue
			var fraction: float=hit.fraction
			var position: Vector3=hit.axis
			var contact: Vector3=a.lerp(b,fraction)
			var radius: float=float(stats.radius)+float(hit.radius)
			var delta: Vector3=position-eye
			var along: float=delta.dot(direction)
			if along<0.0 or along>float(plan.get("reach",stats.reach))+radius or (delta-direction*along).length()>radius:
				continue
			var visible: Vector3=position+(eye-position).normalized()*float(hit.radius)
			if not _strike_visible(id,mosquito,eye,visible): continue
			if not ArenaData.clear_segment(contact,hit.surface,str(config.map_id),doors): continue
			if not ArenaData.ray_map(a,contact,str(config.map_id),float(stats.radius),doors).is_empty(): continue
			# The face cannot travel through a torso to reach a visible insect.
			var blocked := false
			for human_id: int in _human_ids:
				var body: Dictionary=Pose.ray_body(actors[human_id],a,contact,human_id==id,human_id==id)
				if not body.is_empty() and float(body.distance)<a.distance_to(contact)-.008:
					blocked=true
					break
			if not blocked and fraction<nearest_time:
				nearest=mosquito_id
				nearest_time=fraction
	return nearest

func _attack_info(id: int) -> Dictionary:
	var human: Dictionary=actors[id]
	var result: Dictionary=Dictionary(human._attack).duplicate(true)
	result.recovery=maxf(0.0,float(human.swing))
	result.can_swing=result.recovery<=0.0 and phase=="playing"
	result.candidate=false
	result.status="recovery" if not result.can_swing else "ready"
	result.tool=str(human.tool)
	result.radius=float(TOOL_STATS[result.tool].radius)
	result.reach=0.0
	result.reach_origin="eye"
	if not result.can_swing:
		return result
	var plan: Dictionary=_strike_plan(id)
	result.tool=plan.tool
	result.radius=plan.radius
	result.reach=plan.reach
	var potential := false
	for mosquito_id: int in _mosquito_ids:
		var mosquito: Dictionary=actors[mosquito_id]
		if bool(mosquito.alive) and mosquito.state!="stunned" and _impact_near_ray(mosquito,plan.origin,plan.direction,float(plan.reach),float(plan.radius)) and not InsectPose.ray_candidates(InsectPose.collision_segments(_impact_actor(mosquito)),plan.origin,plan.direction,float(plan.reach),float(plan.radius)).is_empty():
			potential=true
			break
	if not potential:
		if str(plan.kind) in ["map","door"]:result.status="blocked"
		return result
	var posed: Dictionary=human.duplicate(false)
	posed.strike=plan.duplicate(false)
	posed.strike.active=true
	var stats: Dictionary=TOOL_STATS[str(plan.tool)]
	var start: float=stats.get("damage_start",STRIKE_START)
	var end: float=stats.get("damage_end",STRIKE_END)
	var duration: float=stats.get("gesture",Pose.SWING_GESTURE_SECONDS)
	posed.strike.progress=start/duration
	var a: Vector3=_strike_contact(posed)
	for index: int in range(1,23):
		posed.strike.progress=lerpf(start,end,float(index)/22.0)/duration
		var b: Vector3=_strike_contact(posed)
		if _swept_strike_hit(id,plan,a,b,{},elapsed,elapsed,elapsed)>=0:
			result.candidate=true
			result.status="opportunity"
			break
		a=b
	if not result.candidate and str(plan.kind) in ["map","door"]:
		result.status="blocked"
	return result

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
		if ArenaData.human_envelope(human).intersects_segment(from,to)==null:
			continue
		var start_local: Vector3 = (from - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
		var end_local: Vector3 = (to - Vector3(human.p)).rotated(Vector3.UP, -Pose.body_yaw(human))
		for capsule: Dictionary in _pose_bundle(id).capsules:
			var closest: PackedVector3Array = Geometry3D.get_closest_points_between_segments(start_local, end_local, capsule.from, capsule.to)
			if closest[0].distance_to(closest[1]) < float(capsule.radius) - 0.008:
				return true
	return false

func _kill(id: int, tool: String="hands", kind: String="melee") -> void:
	var actor: Dictionary = actors[id]
	if not bool(actor.alive) or actor.state == "stunned":
		return
	var safe_tool: String=tool if ToolData.has_tool(tool) else "hands"
	actor.impact={"id":int(actor.impact.id)+1,"tool":safe_tool,"kind":"projectile" if kind=="projectile" else "melee","material":str(ToolData.DATA[safe_tool].material)}
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
		if not ArenaData.clear_segment(helper.p, target.p, str(config.map_id), doors) or _body_occludes(helper.p, target.p, -1):
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

func _owned_pickup(id: int) -> int:
	for pickup_id: int in pickups:
		if int(pickups[pickup_id].holder)==id and str(pickups[pickup_id].tool)==str(actors[id].tool):return pickup_id
	return -1

func _throw_info(id: int) -> Dictionary:
	var actor: Dictionary=actors[id]
	var tool: String=str(actor.tool)
	var stats: Dictionary=ToolData.throw_stats(tool)
	var pending: Dictionary=actor._throw
	var charge: float=clampf((elapsed-float(pending.get("started",elapsed)))/maxf(.01,float(stats.get("charge_seconds",1.0))),0.0,1.0) if not pending.is_empty() else 0.0
	if str(pending.get("state",""))=="release":charge=float(pending.power)
	var recovery: float=maxf(0.0,float(actor._throw_recovery)-elapsed)
	var can: bool=phase=="playing" and bool(actor.alive) and ToolData.throwable(tool) and _owned_pickup(id)>=0 and float(actor.swing)<=0.0 and recovery<=0.0
	var reason: String=str(actor._throw_reason)
	if not ToolData.throwable(tool):reason="not_throwable"
	elif reason.is_empty() and (recovery>0.0 or float(actor.swing)>0.0):reason="recovery"
	return {"id":int(actor.throw_gesture.id),"state":str(actor.throw_gesture.state),"tool":tool,"charge":charge,"power":charge,"charge_seconds":float(stats.get("charge_seconds",0.0)),"speed":lerpf(float(stats.get("min_speed",0.0)),float(stats.get("max_speed",0.0)),charge),"can_throw":can,"recovery":recovery,"reason":reason}

func _cancel_throw(id: int, reason: String) -> void:
	if not actors.has(id) or actors[id].role!="human":return
	var actor: Dictionary=actors[id]
	if Dictionary(actor._throw).is_empty():return
	actor._throw={}
	actor._throw_reason=reason
	actor.throw_gesture.state="idle"
	actor.throw_gesture.progress=0.0
	actor.throw_gesture.power=0.0

func _start_throw(id: int, command: Dictionary) -> void:
	var actor: Dictionary=actors[id]
	if not Dictionary(actor._throw).is_empty():return
	if not bool(_throw_info(id).can_throw):return
	if elapsed-float(actor._last_input)>INPUT_TIMEOUT:
		actor._throw_reason="stale"
		return
	var aimed: Dictionary=actor.duplicate(false)
	aimed.yaw=command.yaw
	aimed.pitch=command.pitch
	actor._throw={"state":"charging","started":elapsed,"pickup":_owned_pickup(id),"tool":str(actor.tool)}
	actor._throw_reason=""
	actor.throw_gesture={"id":int(command.seq),"state":"charging","progress":0.0,"power":0.0,"direction":Pose.view_direction(aimed),"tool":str(actor.tool)}

func _throw_aim_point(id: int, command: Dictionary) -> Vector3:
	var aimed: Dictionary=actors[id].duplicate(false)
	aimed.yaw=command.yaw
	aimed.pitch=command.pitch
	var eye: Vector3=Pose.view_origin(aimed)
	var end: Vector3=eye+Pose.view_direction(aimed)*8.0
	# Exact, zero-width camera ray: no melee footprint or target snapping.
	var first: Dictionary=ArenaData.ray_map(eye,end,str(config.map_id),0.0,doors)
	for human_id: int in _human_ids:
		var hit: Dictionary=Pose.ray_body(actors[human_id],eye,end,human_id==id)
		if not hit.is_empty() and (first.is_empty() or float(hit.distance)<float(first.distance)):first=hit
	for mosquito_id: int in _mosquito_ids:
		if not bool(actors[mosquito_id].alive) or actors[mosquito_id].state=="stunned":continue
		for capsule: Dictionary in InsectPose.collision_segments(_impact_actor(actors[mosquito_id])):
			var hit: Dictionary=Pose.ray_capsule(eye,end,capsule)
			if not hit.is_empty() and (first.is_empty() or float(hit.distance)<float(first.distance)):first=hit
	return end if first.is_empty() else Vector3(first.p)

func _throw_direction(actor: Dictionary, pending: Dictionary) -> Vector3:
	# Opposite shared grip endpoints recover the shoulder without duplicating
	# pose dimensions. Both endpoints use the actual server charge.
	var shoulder: Vector3=Pose.throw_origin(actor,Vector3.FORWARD,float(pending.power)).lerp(Pose.throw_origin(actor,Vector3.BACK,float(pending.power)),.5)
	return (Vector3(pending.aim_point)-shoulder).normalized()

func _release_throw(id: int, command: Dictionary) -> void:
	var actor: Dictionary=actors[id]
	var pending: Dictionary=actor._throw
	if pending.is_empty() or str(pending.state)!="charging":return
	if elapsed-float(actor._last_input)>INPUT_TIMEOUT or elapsed-float(pending.started)>THROW_HOLD_TIMEOUT:
		_cancel_throw(id,"stale")
		return
	if _owned_pickup(id)!=int(pending.pickup) or str(actor.tool)!=str(pending.tool):
		_cancel_throw(id,"equipment")
		return
	var stats:=ToolData.throw_stats(str(pending.tool))
	pending.state="release"
	pending.power=clampf((elapsed-float(pending.started))/float(stats.charge_seconds),0.0,1.0)
	pending.release_at=elapsed+float(stats.launch_seconds)
	pending.aim_point=_throw_aim_point(id,command)
	pending.direction=_throw_direction(actor,pending)
	actor.throw_gesture.state="release"
	actor.throw_gesture.progress=0.0
	actor.throw_gesture.power=pending.power
	actor.throw_gesture.direction=pending.direction

func _update_throw(id: int) -> void:
	var actor: Dictionary=actors[id]
	var pending: Dictionary=actor._throw
	if pending.is_empty():
		if str(actor.throw_gesture.state)=="recovering":
			var seconds: float=ToolData.throw_stats(str(actor.throw_gesture.tool)).get("recovery_seconds",.45)
			actor.throw_gesture.progress=clampf(1.0-(float(actor._throw_recovery)-elapsed)/seconds,0.0,1.0)
			if elapsed>=float(actor._throw_recovery):actor.throw_gesture.state="idle"
		return
	if not bool(actor.alive) or _owned_pickup(id)!=int(pending.pickup) or str(actor.tool)!=str(pending.tool):
		_cancel_throw(id,"equipment")
		return
	if elapsed-float(actor._last_input)>INPUT_TIMEOUT:
		_cancel_throw(id,"stale")
		return
	var stats:=ToolData.throw_stats(str(pending.tool))
	if str(pending.state)=="charging":
		if elapsed-float(pending.started)>THROW_HOLD_TIMEOUT:
			_cancel_throw(id,"timeout")
			return
		actor.throw_gesture.progress=clampf((elapsed-float(pending.started))/float(stats.charge_seconds),0.0,1.0)
		actor.throw_gesture.power=actor.throw_gesture.progress
		actor.throw_gesture.direction=Pose.view_direction(actor)
	else:
		# The captured world point stays fixed even if a mosquito moves away.
		pending.direction=_throw_direction(actor,pending)
		actor.throw_gesture.direction=pending.direction
		actor.throw_gesture.progress=clampf(1.0-(float(pending.release_at)-elapsed)/float(stats.launch_seconds),0.0,1.0)
		if elapsed+.000001>=float(pending.release_at):_launch_throw(id)

func _human_capsules(actor: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary]=[]
	var basis:=Basis(Vector3.UP,Pose.body_yaw(actor))
	for piece: Dictionary in Pose.collision_segments(actor):
		result.append({"key":piece.key,"from":Vector3(actor.p)+basis*Vector3(piece.from),"to":Vector3(actor.p)+basis*Vector3(piece.to),"radius":piece.radius})
	return result

func _launch_throw(id: int) -> void:
	var actor: Dictionary=actors[id]
	var pending: Dictionary=Dictionary(actor._throw).duplicate(true)
	var tool: String=pending.tool
	var stats:=ToolData.throw_stats(tool)
	var direction: Vector3=pending.direction
	var origin: Vector3=Pose.throw_origin(actor,direction,float(pending.power))
	var rotation:=Projectiles.launch_basis(direction).get_euler()
	var charged: Dictionary=actor.duplicate(true)
	charged.throw_gesture.state="charging"
	charged.throw_gesture.progress=1.0
	var source: Vector3=Vector3(actor.p)+Vector3(Pose.sample(charged).tool_grip).rotated(Vector3.UP,Pose.body_yaw(actor))
	var source_shape:=Projectiles.capsule(tool,source,rotation)
	var final_shape:=Projectiles.capsule(tool,origin,rotation)
	var blocked: bool=(Vector3(pending.aim_point)-origin).dot(direction)<=.005 or not Projectiles.map_hit(source_shape,origin-source,str(config.map_id),doors).is_empty()
	for human_id: int in _human_ids:
		for body: Dictionary in _human_capsules(actors[human_id]):
			if human_id==id and Pose.limb_key(str(body.key)) in ["upperarm_r","forearm_r","hand_r"]:continue
			if not Projectiles.sweep_capsules(source_shape,final_shape,body,body).is_empty():blocked=true
	if blocked:
		_cancel_throw(id,"blocked")
		actor._throw_recovery=elapsed+.15
		return
	var pickup_id: int=pending.pickup
	if not pickups.has(pickup_id) or int(pickups[pickup_id].holder)!=id:
		_cancel_throw(id,"equipment")
		return
	var pickup: Dictionary=pickups[pickup_id]
	pickup.holder=0
	pickup.state="flying"
	pickup.p=origin
	pickup.rotation=rotation
	pickup.yaw=float(actor.yaw)
	pickup.velocity=direction*lerpf(float(stats.min_speed),float(stats.max_speed),float(pending.power))
	pickup.owner=id
	pickup.ttl=float(stats.lifetime)
	var excluded: Dictionary={}
	for mosquito_id: int in _mosquito_ids:
		for body: Dictionary in InsectPose.collision_segments(_impact_actor(actors[mosquito_id])):
			if not Projectiles.sweep_capsules(final_shape,final_shape,body,body).is_empty():excluded[mosquito_id]=true
	_projectiles[pickup_id]={"launched":elapsed,"distance":0.0,"damaging":true,"last_impact":-1.0,"tool":tool,"excluded":excluded}
	actor.tool="hands"
	actor._throw={}
	actor._throw_reason=""
	actor._throw_recovery=elapsed+float(stats.recovery_seconds)
	actor.throw_gesture.state="recovering"
	actor.throw_gesture.progress=0.0

func _projectile_event(pickup: Dictionary, flight: Dictionary, kind: String, material: String) -> void:
	if elapsed-float(flight.last_impact)<.06:return
	flight.last_impact=elapsed
	pickup.impact_id=int(pickup.get("impact_id",0))+1
	pickup.impact_kind=kind if kind in ["map","door","human","mosquito","ground"] else "map"
	pickup.impact_material=material if material in ["wood","tile","cloth"] else "wood"

func _settle_projectile(pickup_id: int) -> void:
	var pickup: Dictionary=pickups[pickup_id]
	var rotation:=Vector3(PI*.5,float(pickup.get("yaw",0.0)),0.0)
	var floor_y: float=ArenaData.floor_below(Vector3(pickup.p)+Vector3.UP*.08,str(config.map_id),doors)
	var position: Vector3=pickup.p
	position.y=floor_y+Projectiles.resting_offset(str(pickup.tool),rotation)
	# Only flatten if the final visual-sized object fits here. Never move it
	# through a nearby leg, wall or table edge to manufacture a resting pose.
	if Projectiles.visual_fits(str(pickup.tool),position,rotation,str(config.map_id),doors):
		pickup.p=position
		pickup.rotation=rotation
	else:
		position.y=floor_y+Projectiles.resting_offset(str(pickup.tool),pickup.rotation)
		if Projectiles.visual_fits(str(pickup.tool),position,pickup.rotation,str(config.map_id),doors):pickup.p=position
	pickup.state="ground"
	pickup.velocity=Vector3.ZERO
	pickup.ttl=0.0
	_projectiles.erase(pickup_id)

func _update_projectiles(dt: float, previous_humans: Dictionary, previous_insects: Dictionary) -> void:
	for pickup_id: int in _projectiles.keys():
		if not pickups.has(pickup_id):
			_projectiles.erase(pickup_id)
			continue
		var pickup: Dictionary=pickups[pickup_id]
		var flight: Dictionary=_projectiles[pickup_id]
		if str(pickup.get("state",""))!="flying" or int(pickup.holder)!=0:
			_projectiles.erase(pickup_id)
			continue
		var stats:=ToolData.throw_stats(str(flight.tool))
		var remaining: float=minf(dt,maxf(0.0,elapsed-float(flight.launched)))
		var consumed:=dt-remaining
		while remaining>.000001 and _projectiles.has(pickup_id):
			var part: float=minf(remaining,PROJECTILE_STEP)
			var old: Vector3=pickup.p
			var velocity: Vector3=pickup.velocity
			var displacement:=velocity*part+Vector3.DOWN*float(stats.gravity)*part*part*.5
			velocity.y-=float(stats.gravity)*part
			var a:=Projectiles.capsule(str(flight.tool),old,pickup.rotation)
			var b:=Projectiles.translated(a,displacement)
			var first: Dictionary=Projectiles.map_hit(a,displacement,str(config.map_id),doors)
			var u0: float=consumed/maxf(dt,.000001)
			var u1: float=(consumed+part)/maxf(dt,.000001)
			for human_id: int in _human_ids:
				var current: Array[Dictionary]=_human_capsules(actors[human_id])
				var before: Array[Dictionary]=_human_capsules(previous_humans.get(human_id,actors[human_id]))
				for index: int in range(current.size()):
					if human_id==int(pickup.owner) and float(flight.distance)<.30 and elapsed-float(flight.launched)<.15 and Pose.limb_key(str(current[index].key)) in ["upperarm_r","forearm_r","hand_r"]:continue
					var hit:=Projectiles.sweep_capsules(a,b,Projectiles.lerp_capsule(before[index],current[index],u0),Projectiles.lerp_capsule(before[index],current[index],u1))
					if hit.is_empty() or (not first.is_empty() and float(first.fraction)<=float(hit.fraction)):continue
					hit.kind="human"
					hit.material="cloth"
					first=hit
			var armed: bool=bool(flight.damaging) and float(pickup.ttl)>0.0
			if armed:
				for mosquito_id: int in _mosquito_ids:
					var insect: Dictionary=actors[mosquito_id]
					if not bool(insect.alive) or insect.state=="stunned":continue
					var current: Dictionary=_impact_actor(insect)
					var previous: Variant=previous_insects.get(mosquito_id,current)
					if not previous is Dictionary:previous=current
					var current_shapes:=InsectPose.collision_segments(current)
					var before_shapes:=InsectPose.collision_segments(previous)
					if Dictionary(flight.excluded).has(mosquito_id):
						var overlaps:=false
						for body: Dictionary in current_shapes:
							if not Projectiles.sweep_capsules(b,b,body,body).is_empty():overlaps=true
						if not overlaps:flight.excluded.erase(mosquito_id)
						continue
					for index: int in range(current_shapes.size()):
						var hit:=Projectiles.sweep_capsules(a,b,Projectiles.lerp_capsule(before_shapes[index],current_shapes[index],u0),Projectiles.lerp_capsule(before_shapes[index],current_shapes[index],u1))
						if hit.is_empty() or (not first.is_empty() and float(first.fraction)<=float(hit.fraction)):continue
						if not ArenaData.clear_segment(old,hit.p,str(config.map_id),doors):continue
						hit.kind="mosquito"
						hit.material="cloth"
						hit.id=mosquito_id
						first=hit
			if first.is_empty():
				pickup.p=old+displacement
			else:
				pickup.p=old+displacement*float(first.fraction)+Vector3(first.normal)*.002
				_projectile_event(pickup,flight,str(first.kind),str(first.material))
				if str(first.kind)=="mosquito":_kill(int(first.id),str(flight.tool),"projectile")
				flight.damaging=false
				var normal: Vector3=first.normal
				velocity=velocity.bounce(normal)*float(stats.bounce)
				if normal.y>.55 and (velocity.length()<1.5 or elapsed-float(flight.launched)>float(stats.lifetime)):
					_settle_projectile(pickup_id)
			flight.distance=float(flight.distance)+old.distance_to(pickup.p)
			if str(pickup.state)=="flying":pickup.velocity=velocity
			pickup.ttl=maxf(0.0,float(pickup.ttl)-part)
			remaining-=part
			consumed+=part

func _public_pickups() -> Dictionary:
	var result: Dictionary={}
	for id: int in pickups:
		var item: Dictionary=pickups[id]
		result[id]={"tool":str(item.tool),"p":item.p,"holder":int(item.holder),"yaw":float(item.get("yaw",0.0)),"rotation":item.get("rotation",Vector3.ZERO),"state":str(item.get("state","held" if int(item.holder)!=0 else "ground")),"velocity":item.get("velocity",Vector3.ZERO),"owner":int(item.get("owner",0)),"ttl":float(item.get("ttl",0.0)),"impact_id":int(item.get("impact_id",0)),"impact_kind":str(item.get("impact_kind","ground")),"impact_material":str(item.get("impact_material","wood"))}
	return result

func _pickup_info(id: int) -> Dictionary:
	var result := {"tool":"","can_take":false,"distance":0.0}
	if not actors.has(id) or actors[id].role!="human" or not bool(actors[id].alive) or phase!="playing":
		return result
	var actor: Dictionary=actors[id]
	var nearest_distance := 1.45
	for pickup_id: int in pickups:
		var pickup: Dictionary=pickups[pickup_id]
		if int(pickup.holder)!=0 or str(pickup.get("state","ground"))!="ground":
			continue
		var distance: float=Vector3(actor.p).distance_to(pickup.p)
		if distance<nearest_distance and ArenaData.clear_segment(Pose.view_origin(actor),pickup.p,str(config.map_id),doors):
			nearest_distance=distance
			result={"_id":pickup_id,"tool":str(pickup.tool),"can_take":true,"distance":distance}
	return result

func _pickup(id: int) -> void:
	_cancel_throw(id,"equipment")
	var info: Dictionary=_pickup_info(id)
	if not bool(info.can_take):
		return
	_drop(id)
	pickups[int(info._id)].holder=id
	pickups[int(info._id)].state="held"
	pickups[int(info._id)].velocity=Vector3.ZERO
	pickups[int(info._id)].owner=0
	pickups[int(info._id)].ttl=0.0
	actors[id].tool=str(info.tool)

func _drop(id: int) -> void:
	var actor: Dictionary = actors[id]
	_cancel_throw(id,"equipment")
	if not bool(actor._strike_resolved):
		actor._strike_resolved=true
		if not Dictionary(actor.strike).is_empty():actor.strike.active=false
		if str(actor._attack.state)=="windup":actor._attack.state="miss"
	for pickup_id: int in pickups:
		var pickup: Dictionary = pickups[pickup_id]
		if int(pickup.holder) == id:
			pickup.holder = 0
			pickup.state = "ground"
			pickup.velocity=Vector3.ZERO
			pickup.owner=0
			pickup.ttl=0.0
			var forward: Vector3 = Vector3.FORWARD.rotated(Vector3.UP, float(actor.yaw))
			var height: float = lerpf(ArenaData.HUMAN_HEIGHT, ArenaData.HUMAN_CROUCH_HEIGHT, float(actor.crouch_amount))
			var landing: Vector3 = ArenaData.move_body(actor.p, forward * 0.55, true, str(config.map_id), height, doors)
			landing.y = ArenaData.floor_below(landing + Vector3.UP * 0.15, str(config.map_id), doors)
			pickup.rotation=Vector3(PI*.5,float(actor.yaw),0.0)
			pickup.p = landing + Vector3.UP * Projectiles.resting_offset(str(pickup.tool),pickup.rotation)
			pickup.yaw = float(actor.yaw)
	actor.tool = "hands"

func _update_tasks(dt: float) -> void:
	for index: int in range(_human_ids.size()):
		var id: int = _human_ids[index]
		var human: Dictionary = actors[id]
		var task: Dictionary = human._task
		if not task.is_empty():
			var usable_dt: float = minf(dt, float(task.remaining))
			var near: bool = Vector3(human.p).distance_to(Vector3(task.p)) < 1.20 and ArenaData.clear_segment(Vector3(human.p) + Vector3.UP * 0.65, Vector3(task.p) + Vector3.UP * 0.65, str(config.map_id), doors)
			var input_fresh: bool = elapsed - float(human._last_input) <= INPUT_TIMEOUT
			if near and bool(human._interact) and input_fresh and _door_info(id).is_empty() and not bool(human.bitten) and float(human.swing) <= 0.0:
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
	for id: int in _human_ids:_cancel_throw(id,"round_end")

func abort(explanation: String) -> void:
	phase = "lobby"
	winner = ""
	reason = explanation
	_pending_actions.clear()
	for id: int in _human_ids:_cancel_throw(id,"round_end")
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
		# Visible orientation only after physical contact. A free reservation
		# never contributes a normal, target ID, body zone or rotation schedule.
		var surface_normal: Vector3 = _surface_normal(actor)
		# Explicit allowlist: never serialize hidden target/zone reservations or tasks.
		public_actors[id] = {
			"name": actor.name, "role": actor.role, "p": actor.p, "yaw": actor.yaw,
			"appearance": Dictionary(actor.appearance).duplicate(true),
			"body_yaw": actor.body_yaw, "inspecting": actor.inspecting, "strike": Dictionary(actor.strike).duplicate(true),
			"throw_gesture":Dictionary(actor.throw_gesture).duplicate(true),"impact":Dictionary(actor.impact).duplicate(true),
			"pitch": actor.pitch, "state": actor.state, "alive": actor.alive,
			"surface_normal": surface_normal,
			"help_target": actor.help_target,
			"swing": actor.swing, "bitten": actor.bitten, "threatened": actor.threatened, "tool": actor.tool, "lives": actor.lives,
			"velocity": actor.velocity, "grounded": actor.grounded, "sprinting": actor.sprinting,
			"crouching": actor.crouching, "crouch_amount": actor.crouch_amount,
			"motion_phase": actor.motion_phase, "motion_speed": actor.motion_speed,
			"pose_time":actor.get("pose_time",0.0),"motion_blend":actor.get("motion_blend",0.0),"motion_stride":actor.get("motion_stride",1.15),
			"air_blend":actor.get("air_blend",0.0),"land_blend":actor.get("land_blend",0.0),"turn_blend":actor.get("turn_blend",0.0),
			"motion_direction":actor.get("motion_direction",Vector3.FORWARD),
		}
	return {
		"phase": phase, "map_id": config.map_id, "elapsed": elapsed, "time_left": maxf(0.0, float(config.round_seconds) - elapsed),
		"config": config.duplicate(true), "blood": blood, "winner": winner, "reason": reason,
		"tasks_done": tasks_done, "task_goal": task_goal, "actors": public_actors,
		"pickups": _public_pickups(), "doors": door_state.snapshot(),
	}

func private_for(id: int) -> Dictionary:
	if not actors.has(id):
		return {}
	var actor: Dictionary = actors[id]
	if actor.role == "human":
		var attack: Dictionary = _attack_info(id)
		var pickup: Dictionary = _pickup_info(id)
		pickup.erase("_id")
		return {"throw":_throw_info(id),"pickup":pickup,"task": Dictionary(actor._task).duplicate(true), "deadline": actor._deadline, "failures": actor._failures, "attack": attack, "bite_feedback": Dictionary(actor._bite_feedback).duplicate(true), "interaction": _door_info(id)}
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
	elif not _focus_facing(actor,pose,forward,delta):
		result.reason = "Apuntá hacia tu marca"
	elif not ArenaData.clear_segment(actor.p, pose.p, str(config.map_id), doors) or _body_occludes(actor.p, pose.p, -1):
		result.reason = "La marca está detrás de un obstáculo"
	else:
		result.can_focus = true
		result.state = "charging" if _focus_held(actor) else "ready"
		result.reason = "Mantené E para estabilizarte y picar"
	if not bool(result.can_focus) and _focus_held(actor):
		result.state = "blocked"
	return result

# Close stabilization may retain aim at the preceding authoritative pose: input
# observes that pose, never the human movement which follows in this tick. This
# allowance changes only facing; current range, side and LOS remain mandatory.
func _focus_facing(actor: Dictionary, pose: Dictionary, forward: Vector3, delta: Vector3) -> bool:
	var charging: bool=float(actor._focus_progress)>0.0
	if forward.dot(delta.normalized()) >= (.25 if charging else .40):
		return true
	var previous: Vector3=actor.get("_focus_previous_point",Vector3(INF,INF,INF))
	if not charging or delta.length()>.35 or not previous.is_finite() or previous.distance_to(pose.p)>.31:
		return false
	var observed: Vector3=previous-Vector3(actor.p)
	return observed.length()>.005 and forward.dot(observed.normalized())>=.25
