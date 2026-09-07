class_name BotBrain
extends RefCounted
## Decisions receive the same public state and own private packet as a player.
## No access to opponents' marks, task deadlines or simulation internals.
const ArenaData = preload("res://scripts/arena.gd")
const Catalog = preload("res://scripts/map_catalog.gd")
const SimData = preload("res://scripts/simulation.gd")
const Routes = preload("res://scripts/map_navigation.gd")
const Pose = preload("res://scripts/human_pose.gd")
var peer_id := 0
var age := 0.0
var action_wait := 0.0
var observation_wait := 0.0
var last_seen: Dictionary = {}
var reaction_target := 0
var reaction_age := 0.0
var last_revision := -1
var approaching := true
var attached_age := 0.0
var path: PackedVector3Array = []
var path_age := 0.0
var path_goal := Vector3.INF
var last_position := Vector3.INF
var stuck_age := 0.0
var decision_dt := 1.0/60.0
var retreat_left := 0.0
var retreat_burst_left := 0.0
var retreat_point := Vector3.INF
var patrol_index := 0
var stats := {"moves":0,"bites":0,"detaches":0,"attacks":0,"self_swats":0,"task_inputs":0,"pickups":0,"paths":0}

func setup(id: int) -> void:
	peer_id = id

func decide(snapshot: Dictionary, own_private: Dictionary, dt: float) -> Dictionary:
	age += dt
	decision_dt = dt
	retreat_left = maxf(0,retreat_left-dt)
	retreat_burst_left = maxf(0,retreat_burst_left-dt)
	action_wait -= dt
	path_age += dt
	var result := {"move":Vector3.ZERO,"yaw":0.0,"pitch":0.0,"interact":false,"sprint":false,"crouch":false,"jump":false,"action":""}
	var me: Dictionary = snapshot.get("actors",{}).get(peer_id,{})
	if me.is_empty() or not bool(me.get("alive",false)) or snapshot.get("phase","") != "playing":
		return result
	result.yaw = float(me.get("yaw",0.0))
	result.pitch = float(me.get("pitch",0.0))
	var map_id := str(snapshot.get("config",{}).get("map_id","house"))
	if last_position != Vector3.INF and Vector3(me.p).distance_to(last_position) < 0.012:
		stuck_age += dt
	else:
		stuck_age = 0.0
	last_position = me.p
	if me.role == "human":
		_human(snapshot, own_private, me, result, dt, map_id)
	else:
		_mosquito(snapshot, own_private, me, result, dt, map_id)
	if Vector3(result.move).length() > 0.01:
		stats.moves += 1
	return result

func _action(result: Dictionary, verb: String, delay: float) -> void:
	if action_wait <= 0.0:
		result.action = verb
		action_wait = delay
		var key: String = {"bite":"bites","attack":"attacks","self_swat":"self_swats","pickup":"pickups"}.get(verb,"")
		if not str(key).is_empty():
			stats[key] += 1

func _aim(result: Dictionary, from: Vector3, to: Vector3, turn_rate: float = 2.4) -> void:
	var delta := to - from
	if delta.length() < 0.01:
		return
	var wanted_yaw := atan2(-delta.x, -delta.z)
	result.yaw = float(result.yaw) + clampf(wrapf(wanted_yaw-float(result.yaw),-PI,PI),-turn_rate*decision_dt,turn_rate*decision_dt)
	result.pitch = move_toward(float(result.pitch),clampf(asin(delta.normalized().y),-1.35,1.25),2.0*decision_dt)

func _human(snapshot: Dictionary, own: Dictionary, me: Dictionary, out: Dictionary, dt: float, map_id: String) -> void:
	var eye: Vector3 = me.p + Vector3(Pose.sample(me).eye).rotated(Vector3.UP,float(me.yaw))
	var mode := str(snapshot.config.mode)
	var visible: Dictionary = {}
	var closest := 6.5
	for id: int in snapshot.actors:
		var other: Dictionary = snapshot.actors[id]
		if other.role != "mosquito" or not bool(other.alive):
			continue
		var delta: Vector3 = other.p - eye
		var forward := Vector3.FORWARD.rotated(Vector3.UP,float(me.yaw))
		var observed := delta.length() < 0.9 or delta.normalized().dot(forward) > 0.25
		if observed and delta.length() < closest and ArenaData.clear_segment(eye,other.p,map_id):
			closest = delta.length()
			visible = other.duplicate(true)
			visible.id = id
	# A bite is tactile feedback. Pick a self-defense band by contact height,
	# never by reading the hidden zone assignment. Rear marks remain protected
	# by the authoritative simulation even if the AI attempts a self swat.
	if bool(me.get("bitten",false)):
		reaction_target = 0
		observation_wait = 1.6
		reaction_age += dt
		var contact_height := 1.5
		for other: Dictionary in snapshot.actors.values():
			if other.role == "mosquito" and other.get("state","") == "biting" and Vector2(other.p.x-me.p.x,other.p.z-me.p.z).length() < 0.65:
				contact_height = (other.p.y-me.p.y) / maxf(0.6,1.0-float(me.get("crouch_amount",0))*0.35)
		out.pitch = 0.0 if contact_height >= 1.28 else (-0.55 if contact_height >= 0.76 else -1.1)
		if reaction_age >= 3.6 + 0.2*sin(float(peer_id)):
			# A novice opponent occasionally chooses the adjacent body band.
			if posmod(int(stats.self_swats),3) == 1:
				out.pitch = -0.6 if out.pitch > -0.3 else 0.0
			_action(out,"self_swat",1.8)
		return
	reaction_age = 0.0
	var task: Dictionary = own.get("task",{})
	var working := mode == "sleep" and not task.is_empty()
	if visible.is_empty() or closest >= 2.2:
		reaction_target = 0
		observation_wait = 2.5
	# Human bots have a deliberately imperfect aim/reaction for practice.
	if age > 5.0 and not visible.is_empty() and closest < 2.2:
		if int(visible.id) != reaction_target:
			reaction_target = int(visible.id)
			observation_wait = 2.5
		observation_wait -= dt
		_aim(out,eye,visible.p + Vector3(sin(age*1.7)*0.55,cos(age*1.3)*0.40,0),0.8)
		var reach := float(SimData.TOOL_STATS[str(me.tool)].reach)
		if observation_wait <= 0 and closest < reach + 0.05:
			_action(out,"attack",1.65)
		if not working and closest > 1.65:
			var direction := _walk_direction(me.p,visible.p,map_id)
			out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * 0.25
		if not working:
			return
	var destination: Vector3
	if working:
		destination = task.p
		var distance := Vector3(me.p).distance_to(task.p)
		out.interact = distance < 1.05
		if out.interact:
			stats.task_inputs += 1
			out.move = Vector3.ZERO
			return
	else:
		var stations: Array = Catalog.get_map(map_id).stations
		destination = stations[(patrol_index+peer_id)%stations.size()].p
		if Vector3(me.p).distance_to(destination) < 0.8:
			patrol_index += 1
	var direction := _walk_direction(me.p,destination,map_id)
	if direction.length() > 0.01:
		_aim(out,eye,eye+direction)
		out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * (0.9 if working else 0.50)
		out.sprint = working and Vector3(me.p).distance_to(destination) > 4.0
	if str(me.tool) == "hands":
		for pickup: Dictionary in snapshot.get("pickups",{}).values():
			if int(pickup.holder) == 0 and Vector3(me.p).distance_to(pickup.p) < 1.2:
				_action(out,"pickup",0.9)
				break

func _mosquito(snapshot: Dictionary, own: Dictionary, me: Dictionary, out: Dictionary, dt: float, map_id: String) -> void:
	var mode := str(snapshot.config.mode)
	if me.state == "biting":
		attached_age += dt
		if attached_age > (4.2 if mode == "blood" else 2.8):
			_action(out,"bite",1.0)
			if out.action == "bite":
				stats.detaches += 1
				retreat_left = 4.0 + float(peer_id % 3)*0.4
				retreat_burst_left = 0.6
				var assignment: Dictionary = own.get("assignment",{})
				retreat_point = escape_point(me.p,Vector3(assignment.get("normal",Vector3.FORWARD)),map_id)
		return
	attached_age = 0.0
	# Give learners time to orientate; opponents begin independently.
	if age < 6.0 + float(peer_id % 3)*1.8:
		return
	var definition: Dictionary = Catalog.get_map(map_id)
	if retreat_left > 0.0 and retreat_point != Vector3.INF:
		if retreat_burst_left > 0.0:
			out.move = Vector3.BACK*0.85
			return
		_fly_toward(me,out,retreat_point,map_id,0.7)
		return
	if mode == "survival":
		var refuges: Array = definition.respawn_points
		var waypoint: Vector3 = refuges[(patrol_index+peer_id)%refuges.size()]
		if Vector3(me.p).distance_to(waypoint) < 0.45:
			patrol_index += 1
		_fly_toward(me,out,waypoint,map_id,0.5)
		return
	var assignment: Dictionary = own.get("assignment",{})
	if assignment.is_empty():
		return
	var target: Vector3 = assignment.p
	var normal: Vector3 = assignment.normal
	var outer: Vector3 = target+normal*1.05
	var focus: Dictionary = own.get("focus",{})
	var exposed := (Vector3(me.p)-target).dot(normal) > 0.012
	if Vector3(me.p).distance_to(target) < 1.45 and exposed and ArenaData.clear_segment(me.p,target,map_id):
		_aim(out,me.p,target)
		out.interact = true
		out.move = Vector3.ZERO
		if focus.get("state","") == "charging":
			stats.bites += 1
	else:
		_fly_toward(me,out,outer,map_id,0.68)

func _fly_toward(me: Dictionary, out: Dictionary, destination: Vector3, map_id: String, throttle: float) -> void:
	var direction := _path_direction(me.p,destination,false,map_id)
	if direction.length() < 0.01:
		return
	_aim(out,me.p,me.p+direction)
	var forward := ArenaData.flight_direction(Vector3.FORWARD,float(out.yaw),float(out.pitch))
	# Turn before accelerating toward a doorway. Never use world-up ceiling shortcuts.
	var alignment := forward.dot(direction.normalized())
	out.move = Vector3(0,0,-throttle*clampf((alignment-0.25)/0.65,0,1))

static func escape_point(from: Vector3, normal: Vector3, map_id: String) -> Vector3:
	var outward := Vector3(normal.x,clampf(normal.y,-0.15,0.3),normal.z).normalized()
	for distance: float in [3.2,2.4,1.6]:
		for angle: float in [0.0,0.65,-0.65,1.1,-1.1]:
			var candidate := from+outward.rotated(Vector3.UP,angle)*distance
			if Routes.can_travel(from,candidate,false,map_id):
				return candidate
	return from

func _walk_direction(from: Vector3, destination: Vector3, map_id: String) -> Vector3:
	var direction := _path_direction(from,destination,true,map_id)
	direction.y = 0.0
	return direction.normalized()

func _path_direction(from: Vector3, destination: Vector3, human: bool, map_id: String) -> Vector3:
	if path_age > 1.0 or path_goal == Vector3.INF or path_goal.distance_to(destination) > 0.9 or (stuck_age > 0.9 and path_age > 0.4):
		path_age = 0.0
		path_goal = destination
		path = Routes.path(from,destination,human,map_id)
		stats.paths += 1
	while not path.is_empty() and from.distance_to(path[0]) < (0.29 if human else 0.25):
		path.remove_at(0)
	return Vector3.ZERO if path.is_empty() else (path[0]-from).normalized()
