class_name BotBrain
extends RefCounted
## Decisions receive the same public state and own private packet as a player.
## No access to opponents' marks, task deadlines or simulation internals.
const ArenaData = preload("res://scripts/arena.gd")
const Catalog = preload("res://scripts/map_catalog.gd")
const SimData = preload("res://scripts/simulation.gd")
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
var navigation: AStarGrid2D
var grid_map := ""
var path: PackedVector2Array = []
var path_age := 0.0
var path_goal := Vector3.INF
var last_position := Vector3.INF
var stuck_age := 0.0
var stats := {"moves":0,"bites":0,"detaches":0,"attacks":0,"self_swats":0,"task_inputs":0,"pickups":0,"paths":0}

func setup(id: int) -> void:
	peer_id = id

func decide(snapshot: Dictionary, own_private: Dictionary, dt: float) -> Dictionary:
	age += dt
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

func _aim(result: Dictionary, from: Vector3, to: Vector3) -> void:
	var delta := to - from
	if delta.length() < 0.01:
		return
	result.yaw = atan2(-delta.x, -delta.z)
	result.pitch = clampf(asin(delta.normalized().y),-1.35,1.25)

func _human(snapshot: Dictionary, own: Dictionary, me: Dictionary, out: Dictionary, dt: float, map_id: String) -> void:
	var eye: Vector3 = me.p + Vector3.UP * lerpf(1.55,0.98,float(me.get("crouch_amount",0)))
	var mode := str(snapshot.config.mode)
	var visible: Dictionary = {}
	var closest := 6.5
	for id: int in snapshot.actors:
		var other: Dictionary = snapshot.actors[id]
		if other.role != "mosquito" or not bool(other.alive):
			continue
		var delta: Vector3 = other.p - eye
		var forward := Vector3.FORWARD.rotated(Vector3.UP,float(me.yaw))
		var observed := delta.length() < 1.1 or delta.normalized().dot(forward) > -0.15
		if observed and delta.length() < closest and ArenaData.clear_segment(eye,other.p,map_id):
			closest = delta.length()
			visible = other.duplicate(true)
			visible.id = id
	# A bite is tactile feedback. Pick a self-defense band by contact height,
	# never by reading the hidden zone assignment. Rear marks remain protected
	# by the authoritative simulation even if the AI attempts a self swat.
	if bool(me.get("bitten",false)):
		reaction_age += dt
		var contact_height := 1.5
		for other: Dictionary in snapshot.actors.values():
			if other.role == "mosquito" and other.get("state","") == "biting" and Vector2(other.p.x-me.p.x,other.p.z-me.p.z).length() < 0.65:
				contact_height = (other.p.y-me.p.y) / maxf(0.6,1.0-float(me.get("crouch_amount",0))*0.35)
		out.pitch = 0.0 if contact_height >= 1.28 else (-0.55 if contact_height >= 0.76 else -1.1)
		if reaction_age >= 1.35:
			_action(out,"self_swat",1.1)
		return
	reaction_age = 0.0
	var task: Dictionary = own.get("task",{})
	var working := mode == "sleep" and not task.is_empty()
	# Human bots have a deliberately imperfect aim/reaction for practice.
	if not visible.is_empty() and closest < 2.8:
		if int(visible.id) != reaction_target:
			reaction_target = int(visible.id)
			observation_wait = 0.85
		observation_wait -= dt
		_aim(out,eye,visible.p + Vector3(sin(age*1.7)*0.12,cos(age*1.3)*0.09,0))
		var reach := float(SimData.TOOL_STATS[str(me.tool)].reach)
		if observation_wait <= 0 and closest < reach + 0.05:
			_action(out,"attack",1.25)
		if not working and closest > 1.05:
			var direction := _walk_direction(me.p,visible.p,map_id)
			out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * 0.65
		if not working:
			return
	var destination: Vector3
	if working:
		destination = task.p
		var distance := Vector2(me.p.x-task.p.x,me.p.z-task.p.z).length()
		out.interact = distance < 1.05
		if out.interact:
			stats.task_inputs += 1
			out.move = Vector3.ZERO
			return
	else:
		var corners := [Vector3(-1.5,0,1.2),Vector3(1.6,0,1.4),Vector3(1.5,0,-1.6),Vector3(-1.7,0,-1.3)]
		destination = corners[(int(age/5.0)+peer_id)%corners.size()]
	var direction := _walk_direction(me.p,destination,map_id)
	if direction.length() > 0.01:
		_aim(out,eye,eye+direction)
		out.move = direction.rotated(Vector3.UP,-float(out.yaw)) * (0.82 if working else 0.45)
		out.sprint = working and Vector3(me.p).distance_to(destination) > 4.0
	if str(me.tool) == "hands":
		for pickup: Dictionary in snapshot.get("pickups",{}).values():
			if int(pickup.holder) == 0 and Vector3(me.p).distance_to(pickup.p) < 1.2:
				_action(out,"pickup",0.9)
				break

func _mosquito(snapshot: Dictionary, own: Dictionary, me: Dictionary, out: Dictionary, dt: float, map_id: String) -> void:
	var mode := str(snapshot.config.mode)
	var definition: Dictionary = Catalog.get_map(map_id)
	var ceiling := float(definition.get("ceiling",ArenaData.CEILING))
	if me.state == "biting":
		attached_age += dt
		if attached_age > (2.8 if mode == "blood" else 1.9):
			_action(out,"bite",0.75)
			if out.action == "bite":
				stats.detaches += 1
		return
	attached_age = 0.0
	if mode == "survival":
		# Survival does not require biting. Fly a changing orbit with clearance.
		var waypoint := Vector3(sin(age*0.45+peer_id)*2.6,minf(ceiling-0.3,2.2)+sin(age)*0.12,cos(age*0.45+peer_id)*2.2)
		var delta: Vector3 = waypoint-me.p
		_aim(out,me.p,waypoint)
		out.move = delta.normalized().rotated(Vector3.UP,-float(out.yaw))*0.6
		return
	var assignment: Dictionary = own.get("assignment",{})
	if assignment.is_empty():
		return
	if int(assignment.revision) != last_revision:
		last_revision = int(assignment.revision)
		approaching = true
	var outer: Vector3 = assignment.p + assignment.normal*0.9
	outer.y = minf(ceiling-0.25,maxf(2.12,float(assignment.p.y)+0.45))
	if approaching and Vector3(me.p).distance_to(outer) < 0.35:
		approaching = false
	var destination: Vector3 = outer if approaching else Vector3(assignment.p)+Vector3(assignment.normal)*0.22
	if not ArenaData.clear_segment(me.p,destination,map_id):
		destination.y = ceiling-0.25
		if float(me.p.y) < ceiling-0.45:
			destination.x = me.p.x
			destination.z = me.p.z
	if stuck_age > 1.0:
		approaching = true
		destination = outer + Vector3(sin(age)*0.35,0,cos(age)*0.35)
	var delta: Vector3 = destination-me.p
	_aim(out,me.p,assignment.p)
	out.move = delta.normalized().rotated(Vector3.UP,-float(out.yaw)) * minf(0.85,delta.length()*1.8)
	if not approaching and Vector3(me.p).distance_to(assignment.p) < 0.49:
		_action(out,"bite",0.65)

func _walk_direction(from: Vector3, destination: Vector3, map_id: String) -> Vector3:
	if navigation == null or grid_map != map_id:
		_build_grid(map_id)
	if path_age > 0.7 or path_goal == Vector3.INF or path_goal.distance_to(destination) > 0.7 or (stuck_age > 0.9 and path_age > 0.25):
		path_age = 0.0
		path_goal = destination
		var start := _nearest_cell(Vector2(from.x,from.z))
		var end := _nearest_cell(Vector2(destination.x,destination.z))
		path = navigation.get_point_path(start,end)
		stats.paths += 1
	var flat := Vector2(from.x,from.z)
	while not path.is_empty() and flat.distance_to(path[0]) < 0.24:
		path.remove_at(0)
	if path.is_empty():
		return Vector3.ZERO
	var direction := path[0]-flat
	return Vector3(direction.x,0,direction.y).normalized()

func _build_grid(map_id: String) -> void:
	grid_map = map_id
	var definition: Dictionary = Catalog.get_map(map_id)
	var hx := float(definition.get("half_x",ArenaData.HALF_X))
	var hz := float(definition.get("half_z",ArenaData.HALF_Z))
	var cell := 0.4
	navigation = AStarGrid2D.new()
	navigation.region = Rect2i(0,0,int(hx*2/cell),int(hz*2/cell))
	navigation.cell_size = Vector2.ONE*cell
	navigation.offset = Vector2(-hx+cell/2,-hz+cell/2)
	navigation.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	navigation.update()
	for x: int in range(navigation.region.size.x):
		for z: int in range(navigation.region.size.y):
			var id := Vector2i(x,z)
			var p := navigation.get_point_position(id)
			var blocked := absf(p.x)>hx-ArenaData.HUMAN_RADIUS or absf(p.y)>hz-ArenaData.HUMAN_RADIUS
			for obstacle: AABB in definition.get("obstacles",ArenaData.OBSTACLES):
				var expanded := Rect2(Vector2(obstacle.position.x,obstacle.position.z),Vector2(obstacle.size.x,obstacle.size.z)).grow(ArenaData.HUMAN_RADIUS+0.03)
				if expanded.has_point(p):
					blocked = true
			navigation.set_point_solid(id,blocked)

func _nearest_cell(point: Vector2) -> Vector2i:
	var best := Vector2i.ZERO
	var distance := INF
	for x: int in range(navigation.region.size.x):
		for z: int in range(navigation.region.size.y):
			var id := Vector2i(x,z)
			if navigation.is_point_solid(id):
				continue
			var candidate := point.distance_squared_to(navigation.get_point_position(id))
			if candidate < distance:
				distance = candidate
				best = id
	return best
