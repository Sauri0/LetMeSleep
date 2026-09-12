class_name BotBrain
extends RefCounted
## Decisions receive the same public state and own private packet as a player.
## No access to opponents' marks, task deadlines or simulation internals.
const ArenaData = preload("res://scripts/arena.gd")
const Catalog = preload("res://scripts/map_catalog.gd")
const SimData = preload("res://scripts/simulation.gd")
const Routes = preload("res://scripts/map_navigation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Doors = preload("res://scripts/door_catalog.gd")
var visible_doors: Dictionary = {}
var door_passage: PackedVector3Array = []
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
var path_review_age := 0.0
var path_goal := Vector3.INF
var path_map_id := ""
var _catalog_cache: Dictionary={}
var _doors_cache: Dictionary={}
var path_human := false
var last_position := Vector3.INF
var stuck_age := 0.0
var decision_dt := 1.0/60.0
var retreat_left := 0.0
var retreat_burst_left := 0.0
var retreat_point := Vector3.INF
var patrol_index := 0
var stats := {"moves":0,"bites":0,"detaches":0,"attacks":0,"self_swats":0,"task_inputs":0,"pickups":0,"paths":0,"helps":0,"doors":0,"door_passages":0}

func setup(id: int) -> void:
	peer_id = id

static func prepare_navigation(map_id: String) -> void:
	# Build the two immutable movement graphs before play starts, so an insect's
	# first approach after its reaction pause does not trigger a graph-build hitch.
	Routes.graph_info(true,map_id)
	Routes.graph_info(false,map_id)

func decide(snapshot: Dictionary, own_private: Dictionary, dt: float) -> Dictionary:
	visible_doors = snapshot.get("doors",{})
	age += dt
	decision_dt = dt
	retreat_left = maxf(0,retreat_left-dt)
	retreat_burst_left = maxf(0,retreat_burst_left-dt)
	action_wait -= dt
	path_age += dt
	path_review_age += dt
	var result := {"move":Vector3.ZERO,"yaw":0.0,"pitch":0.0,"interact":false,"sprint":false,"crouch":false,"jump":false,"action":""}
	var me: Dictionary = snapshot.get("actors",{}).get(peer_id,{})
	if me.is_empty() or not bool(me.get("alive",false)) or snapshot.get("phase","") != "playing":
		return result
	result.yaw = float(me.get("yaw",0.0))
	result.pitch = float(me.get("pitch",0.0))
	var map_id := str(snapshot.get("config",{}).get("map_id",Catalog.default_map_id()))
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
		var key: String = {"bite":"bites","attack":"attacks","self_swat":"self_swats","pickup":"pickups","door":"doors"}.get(verb,"")
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
	var eye: Vector3 = Pose.view_origin(me)
	var mode := str(snapshot.config.mode)
	var visible: Dictionary = {}
	var closest := 6.5
	for id: int in snapshot.actors:
		var other: Dictionary = snapshot.actors[id]
		if other.role != "mosquito" or not bool(other.alive) or other.get("state", "") == "stunned":
			continue
		var delta: Vector3 = other.p - eye
		var forward := Vector3.FORWARD.rotated(Vector3.UP,float(me.yaw))
		var observed := delta.length() < 0.9 or delta.normalized().dot(forward) > 0.25
		if observed and delta.length() < closest and ArenaData.clear_segment(eye,other.p,map_id,visible_doors):
			closest = delta.length()
			visible = other.duplicate(true)
			visible.id = id
	# React to an actual attached insect, then aim the same manual ray as a player.
	# No access to reservations or automatic selection of a body band.
	if bool(me.get("bitten",false)):
		reaction_target = 0
		observation_wait = 1.6
		reaction_age += dt
		var contact := Vector3.INF
		var contact_distance := INF
		for other: Dictionary in snapshot.actors.values():
			if other.role == "mosquito" and other.get("state","") == "biting" and Vector2(other.p.x-me.p.x,other.p.z-me.p.z).length() < 0.65:
				var distance: float = eye.distance_to(other.p)
				if distance < contact_distance:
					contact_distance = distance
					contact = other.p
		if contact != Vector3.INF:
			_aim_body_contact(out, me, contact)
			var direction := Vector3.FORWARD.rotated(Vector3.RIGHT, float(out.pitch)).rotated(Vector3.UP, float(out.yaw))
			if reaction_age >= 3.6 + 0.2*sin(float(peer_id)) and direction.dot((contact-eye).normalized()) > 0.985:
				_action(out,"attack",1.8)
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
		_aim(out,eye,visible.p + Vector3(sin(age*1.7)*0.12,cos(age*1.3)*0.09,0),1.2)
		# The ray's maximum range is not the arm's physical reach. Approach far
		# enough for the real hand/tool gesture to reach, then attempt the strike.
		var contact_range := minf(1.3,0.60 + float(Pose.TOOL_LENGTHS[str(me.tool)]))
		if observation_wait <= 0 and closest < contact_range + 0.10:
			_action(out,"attack",1.65)
		if not working and closest > contact_range:
			var direction := _walk_direction(me.p,visible.p,map_id)
			if _human_door(me,out,direction,map_id):
				return
			out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * 0.45
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
		var stations: Array = _catalog(map_id).stations
		destination = stations[(patrol_index+peer_id)%stations.size()].p
		if Vector3(me.p).distance_to(destination) < 0.8:
			patrol_index += 1
	var direction := _walk_direction(me.p,destination,map_id)
	if _human_door(me,out,direction,map_id):
		return
	if direction.length() > 0.01:
		_aim(out,eye,eye+direction)
		out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * (0.9 if working else 0.50)
		out.sprint = working and Vector3(me.p).distance_to(destination) > 4.0
	if str(me.tool) == "hands":
		for pickup: Dictionary in snapshot.get("pickups",{}).values():
			if int(pickup.holder) == 0 and Vector3(me.p).distance_to(pickup.p) < 1.2:
				_action(out,"pickup",0.9)
				break

func _human_door(me: Dictionary, out: Dictionary, direction: Vector3, map_id: String) -> bool:
	if direction.length_squared()<0.01 or visible_doors.is_empty():
		return false
	var from: Vector3 = Vector3(me.p)+Vector3.UP
	var nearest: Dictionary = {}
	for id: String in visible_doors:
		if not _door_definitions(map_id).has(id) or float(visible_doors[id].angle)>Doors.OPEN_ANGLE-0.02:
			continue
		var hit: Dictionary = Doors.ray_leaf(_door_definitions(map_id)[id],0.0,from,from+direction*2.7)
		if not hit.is_empty() and (nearest.is_empty() or float(hit.distance)<float(nearest.distance)):
			nearest = hit
	if nearest.is_empty():
		return false
	var id: String = nearest.door_id
	var definition: Dictionary = _door_definitions(map_id)[id]
	var state: Dictionary = visible_doors[id]
	var point: Vector3 = Doors.handle_point(definition,float(state.angle))
	var eye: Vector3 = Pose.view_origin(me)
	_aim(out,eye,point,4.0)
	if bool(state.blocked) and float(state.target_angle)>0.1:
		# Step away from an opening leaf's room side, rather than continuously
		# walking into the sweep and keeping our own doorway blocked.
		var away: Vector3 = Vector3(me.p)-Vector3(definition.hinge)
		away.y = 0
		var room_direction: Vector3 = Doors.leaf_transform(definition,Doors.OPEN_ANGLE).basis.x
		if away.dot(room_direction)>0:
			away = room_direction
		out.move = away.normalized().rotated(Vector3.UP,-float(out.yaw))*0.75
		return true
	if bool(state.moving):
		return true
	if eye.distance_to(point)>SimData.DOOR_REACH-0.05:
		out.move = direction.rotated(Vector3.UP,-float(out.yaw))*0.65
		return true
	var wanted: Vector3 = (point-eye).normalized()
	var forward := Vector3.FORWARD.rotated(Vector3.RIGHT,float(out.pitch)).rotated(Vector3.UP,float(out.yaw))
	if forward.dot(wanted)>0.995 and float(state.target_angle)<0.1:
		_action(out,"door",0.6)
	return true

func _aim_body_contact(out: Dictionary, me: Dictionary, contact: Vector3) -> void:
	var body_yaw := float(me.get("body_yaw", me.yaw))
	var aim: Vector2 = Pose.aim_angles(me, contact)
	var target_yaw := aim.x
	var target_pitch := aim.y
	if absf(wrapf(target_yaw-body_yaw,-PI,PI)) > deg_to_rad(75.0):
		out.pitch = move_toward(float(out.pitch), -1.0, decision_dt*1.4)
		return
	out.yaw = float(out.yaw) + clampf(wrapf(target_yaw-float(out.yaw),-PI,PI),-decision_dt,decision_dt)
	out.pitch = move_toward(float(out.pitch),clampf(target_pitch,-1.92,1.30),decision_dt*1.4)

func _mosquito(snapshot: Dictionary, own: Dictionary, me: Dictionary, out: Dictionary, dt: float, map_id: String) -> void:
	var mode := str(snapshot.config.mode)
	if me.state == "stunned":
		return
	if me.state == "biting":
		attached_age += dt
		if attached_age > (4.2 if mode == "blood" else 2.8):
			_action(out,"bite",1.0)
			if out.action == "bite":
				stats.detaches += 1
				retreat_left = 4.0 + float(peer_id % 3)*0.4
				retreat_burst_left = 0.6
				var assignment: Dictionary = own.get("assignment",{})
				retreat_point = escape_point(me.p,Vector3(assignment.get("normal",Vector3.FORWARD)),map_id,visible_doors)
		return
	attached_age = 0.0
	# Give learners time to orientate; opponents begin independently.
	if age < 6.0 + float(peer_id % 3)*1.8:
		return
	if mode != "survival" and _help_ally(snapshot, me, out, map_id):
		return
	var definition: Dictionary = _catalog(map_id)
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
	if Vector3(me.p).distance_to(target) < 1.45 and exposed and ArenaData.clear_segment(me.p,target,map_id,visible_doors):
		_aim(out,me.p,target)
		out.interact = true
		out.move = Vector3.ZERO
		if focus.get("state","") == "charging":
			stats.bites += 1
	else:
		_fly_toward(me,out,outer,map_id,0.68)

func _help_ally(snapshot: Dictionary, me: Dictionary, out: Dictionary, map_id: String) -> bool:
	var target: Dictionary = {}
	var nearest := 6.5
	for id: int in snapshot.actors:
		var other: Dictionary = snapshot.actors[id]
		if id == peer_id or other.role != "mosquito" or other.get("state", "") != "stunned":
			continue
		var distance: float = Vector3(me.p).distance_to(other.p)
		if distance < nearest and ArenaData.clear_segment(me.p, other.p, map_id, visible_doors):
			nearest = distance
			target = other
	if target.is_empty():
		return false
	if nearest < 0.72:
		_aim(out, me.p, target.p)
		out.move = Vector3.ZERO
		out.interact = true
		stats.helps += 1
	else:
		_fly_toward(me, out, Vector3(target.p) + Vector3.UP*0.32, map_id, 0.60)
	return true

func _fly_toward(me: Dictionary, out: Dictionary, destination: Vector3, map_id: String, throttle: float) -> void:
	var direction := _path_direction(me.p,destination,false,map_id)
	if direction.length() < 0.01:
		return
	_aim(out,me.p,me.p+direction)
	var forward := ArenaData.flight_direction(Vector3.FORWARD,float(out.yaw),float(out.pitch))
	# Turn before accelerating toward a doorway. Never use world-up ceiling shortcuts.
	var alignment := forward.dot(direction.normalized())
	out.move = Vector3(0,0,-throttle*clampf((alignment-0.25)/0.65,0,1))

static func escape_point(from: Vector3, normal: Vector3, map_id: String, doors: Dictionary = {}) -> Vector3:
	var outward := Vector3(normal.x,clampf(normal.y,-0.15,0.3),normal.z).normalized()
	for distance: float in [3.2,2.4,1.6]:
		for angle: float in [0.0,0.65,-0.65,1.1,-1.1]:
			var candidate := from+outward.rotated(Vector3.UP,angle)*distance
			if Routes.can_travel(from,candidate,false,map_id) and ArenaData.clear_segment(from,candidate,map_id,doors):
				return candidate
	return from

func _walk_direction(from: Vector3, destination: Vector3, map_id: String) -> Vector3:
	var direction := _path_direction(from,destination,true,map_id)
	direction.y = 0.0
	return direction.normalized()

func _path_direction(from: Vector3, destination: Vector3, human: bool, map_id: String) -> Vector3:
	var rebuild: bool = path_map_id != map_id or path_human != human or path_goal == Vector3.INF or path_goal.distance_to(destination) > 0.9 or (stuck_age > 0.9 and path_age > 0.4)
	if path_review_age > 1.0:
		path_review_age = 0.0
		# A fixed task/patrol destination does not require another graph search
		# while its next segment remains traversable. Moving goals keep the same
		# one-second review cadence, and changed/stuck routes invalidate promptly.
		rebuild = rebuild or path_goal != destination or (not path.is_empty() and not Routes.can_travel(from,path[0],human,map_id))
	if rebuild:
		path_age = 0.0
		path_review_age = 0.0
		path_goal = destination
		path_map_id = map_id
		path_human = human
		path = Routes.path(from,destination,human,map_id)
		stats.paths += 1
	while not path.is_empty() and from.distance_to(path[0]) < (0.29 if human else 0.25):
		path.remove_at(0)
	if not human:
		while not door_passage.is_empty() and from.distance_to(door_passage[0])<0.085:
			door_passage.remove_at(0)
			if door_passage.is_empty():
				path_goal = Vector3.INF
		if not door_passage.is_empty():
			return (door_passage[0]-from).normalized()
		if not path.is_empty():
			var hit: Dictionary = Doors.ray_doors(from,path[0],visible_doors,map_id)
			if not hit.is_empty() and float(hit.distance)<3.0:
				var definition: Dictionary = _door_definitions(map_id)[str(hit.door_id)]
				var transform: Transform3D = Doors.leaf_transform(definition,0.0)
				var center: Vector3 = transform*Vector3(float(definition.width)*0.5,Doors.GAP*0.5,0)
				var normal: Vector3 = transform.basis.z
				var side: float = 1.0 if (from-center).dot(normal)>=0.0 else -1.0
				var entry: Vector3 = center+normal*side*0.25
				var exit_point: Vector3 = center-normal*side*0.25
				if Routes.can_travel(from,entry,false,map_id) and Routes.can_travel(entry,exit_point,false,map_id):
					door_passage = PackedVector3Array([entry,exit_point])
					stats.door_passages += 1
					return (entry-from).normalized()
	return Vector3.ZERO if path.is_empty() else (path[0]-from).normalized()

func _catalog(map_id: String) -> Dictionary:
	if not _catalog_cache.has(map_id): _catalog_cache[map_id]=Catalog.get_map(map_id)
	return _catalog_cache[map_id]

func _door_definitions(map_id: String) -> Dictionary:
	if not _doors_cache.has(map_id): _doors_cache[map_id]=Doors.get_doors(map_id)
	return _doors_cache[map_id]
