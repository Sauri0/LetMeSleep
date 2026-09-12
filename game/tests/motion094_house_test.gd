extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Nav = preload("res://scripts/map_navigation.gd")
const Doors = preload("res://scripts/door_catalog.gd")
var checks := 0
var failures := 0
var map_id: String
var data: Dictionary
var open_doors: Dictionary = {}

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("FAIL: " + label)

func _initialize() -> void:
	map_id = Maps.default_map_id()
	data = Maps.get_map(map_id)
	check(not data.is_empty() and int(data.get("authored_version",0)) > 0, "load authored canonical map")
	if data.is_empty():
		quit(1)
		return
	for id: String in data.doors:
		open_doors[id] = {"angle":PI*.5}
	check(ArenaData.world_bounds() == data.bounds, "default movement bounds use canonical lot")
	check(ArenaData.obstacles() == ArenaData.obstacles(map_id), "default geometry uses canonical map")
	check(ArenaData.human_spawn(0) == data.human_spawns[0], "default human spawn uses canonical map")
	check(ArenaData.mosquito_spawn(0) == data.mosquito_spawns[0], "default insect spawn uses canonical map")
	for p: Vector3 in data.human_spawns:
		check(ArenaData.can_fit_human(p,ArenaData.HUMAN_HEIGHT,map_id,open_doors), "human spawn fits " + str(p))
	for p: Vector3 in data.mosquito_spawns:
		check(ArenaData.can_fit_mosquito(p,map_id,open_doors), "mosquito spawn fits " + str(p))
	_test_lot_and_roof()
	_test_doors()
	_test_stairs()
	_test_routes()
	print("MOTION094_HOUSE checks=%d failures=%d" % [checks,failures])
	quit(0 if failures == 0 else 1)

func _test_lot_and_roof() -> void:
	var building: AABB = data.building_bounds
	var patio := Vector3(0,building.end.y+.6,15)
	check(ArenaData.can_fit_mosquito(patio,map_id), "outdoor flight above house height")
	var lifted: Vector3 = ArenaData.move_body(patio,Vector3.UP,false,map_id)
	check(lifted.y > patio.y+.99, "mosquito ascends above patio without invisible ceiling")
	var above := Vector3(0,building.end.y+.6,0)
	var stopped: Vector3 = ArenaData.move_body(above,Vector3.DOWN,false,map_id)
	check(stopped.y > building.end.y and stopped.y < above.y, "descent stops on physical house roof")
	check(not ArenaData.can_fit_mosquito(Vector3(0,building.end.y+.1,0),map_id), "mosquito does not fit within roof slab")
	var edge := Vector3(18,7,18)
	var limit: Vector3 = ArenaData.move_body(edge,Vector3(4,0,4),false,map_id)
	check(limit.x <= data.bounds.end.x-ArenaData.MOSQUITO_RADIUS+.00001 and limit.z <= data.bounds.end.z-ArenaData.MOSQUITO_RADIUS+.00001, "flight stays within lot")

func _walk(from: Vector3, to: Vector3, doors: Dictionary) -> Dictionary:
	var actor := {"p":from,"velocity":Vector3.ZERO,"yaw":0.0,"pitch":0.0,"grounded":true}
	var valid := ArenaData.can_fit_human(from,ArenaData.HUMAN_HEIGHT,map_id,doors)
	for tick: int in range(500):
		var delta: Vector3 = to-Vector3(actor.p)
		delta.y = 0
		if delta.length() < .06:
			break
		var direction := delta.normalized()
		ArenaData.step_human(actor,{"move":direction,"yaw":0.0},.02,map_id,doors)
		valid = valid and ArenaData.can_fit_human(actor.p,ArenaData.HUMAN_HEIGHT,map_id,doors)
	for tick: int in range(20):
		ArenaData.step_human(actor,{},.02,map_id,doors)
	return {"p":actor.p,"valid":valid,"arrived":Vector3(actor.p).distance_to(to)<.12}

func _test_stairs() -> void:
	for stair: Dictionary in data.stair_connections:
		for up: bool in [true,false]:
			var start: Vector3 = stair.bottom if up else stair.top
			var end: Vector3 = stair.top if up else stair.bottom
			var walked := _walk(start,end,open_doors)
			check(walked.valid and walked.arrived, "physical stair %s up=%s result=%s" % [stair.id,up,walked])

func _test_doors() -> void:
	var exterior_count := 0
	for portal: Dictionary in data.portals:
		var id: String = portal.id
		var definition: Dictionary = data.doors[id]
		var normal := Vector3.ZERO
		normal[int(portal.axis)] = 1
		# Begin clear of the 0.60 m body radius and the adjacent stair railing.
		var start: Vector3 = Vector3(portal.p)-normal*.85
		var end: Vector3 = Vector3(portal.p)+normal*.85
		var closed: Dictionary = open_doors.duplicate(true)
		closed[id].angle = 0.0
		var blocked := _walk(start,end,closed)
		check(blocked.valid and not blocked.arrived, "closed door blocks human " + id)
		var crossing := _walk(start,end,open_doors)
		check(crossing.valid and crossing.arrived, "open door admits human " + id + str(crossing))
		var low: Vector3 = start+Vector3.UP*.075
		var insect: Vector3 = ArenaData.move_body(low,end-start,false,map_id,ArenaData.HUMAN_HEIGHT,closed)
		check(insect.distance_to(end+Vector3.UP*.075)<.001, "mosquito passes closed-door gap " + id)
		var eye: Vector3 = start+Vector3.UP*1.2
		insect = ArenaData.move_body(eye,end-start,false,map_id,ArenaData.HUMAN_HEIGHT,closed)
		check(insect.distance_to(end+Vector3.UP*1.2)>.3, "closed leaf blocks mosquito at eye level " + id)
		var top: Vector3 = Doors.leaf_transform(definition,PI*.5)*Vector3(float(definition.width)*.5,float(definition.height)+.02,0)
		check(is_equal_approx(ArenaData.floor_below(top,map_id,open_doors),float(definition.hinge.y)+float(definition.height)), "authored door supports stunned insect " + id)
		if bool(portal.get("exterior",false)):
			exterior_count += 1
			var back := _walk(end,start,open_doors)
			check(back.valid and back.arrived, "outdoor doorway works in reverse " + id)
	check(exterior_count>=2, "front and patio accesses both exercised")

func _test_routes() -> void:
	var human_start: Vector3 = data.human_spawns[0]
	var insect_start: Vector3 = data.mosquito_spawns[0]
	var goals: Array[Vector3] = [Vector3(0,0,15),Vector3(-16,0,0),Vector3(16,0,0),Vector3(0,0,-15)]
	for station: Dictionary in data.stations:
		goals.append(station.p)
	for goal: Vector3 in goals:
		var human_path := Nav.path(human_start,goal,true)
		check(not human_path.is_empty(), "human route from spawn to task/patio " + str(goal))
		var insect_path := Nav.path(insect_start,goal+Vector3.UP*1.2,false)
		check(not insect_path.is_empty(), "mosquito route from upper floor to task/patio " + str(goal))
	var graph := Nav.graph_info(true)
	check(graph.invalid_edges.is_empty(), "authored human graph has no invalid edges")
	check(not Nav.can_travel(Vector3(NAN,0,0),human_start,true), "nonfinite route is rejected")
