extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const Nav = preload("res://scripts/map_navigation.gd")
const ArenaData = preload("res://scripts/arena.gd")
var checks: int = 0
var failures: int = 0

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("ROUTE_FAIL " + description)

func _initialize() -> void:
	var start_ms: int = Time.get_ticks_msec()
	for human: bool in [true, false]:
		var graph: Dictionary = Nav.graph_info(human)
		check(graph.invalid_edges.is_empty(), "every authored edge fits %s collision" % ("human" if human else "mosquito"))
		check(_reached(graph, -99.0).size() == graph.nodes.size(), "whole graph connected for role")
		# Removing either stair flight must leave the other route between floors.
		for omitted_x: float in [-11.0, 11.0]:
			var reachable: Array[int] = _reached(graph, omitted_x)
			var complete: bool = true
			for index: int in range(graph.nodes.size()):
				var feet: Vector3 = Vector3(graph.nodes[index]) - (Vector3.ZERO if human else Vector3.UP * 1.2)
				if is_equal_approx(feet.y, 0.0) or is_equal_approx(feet.y, 3.2):
					complete = complete and reachable.has(index)
			check(complete, "all floor rooms stay connected without stair %s" % omitted_x)
		var origins: Array = Maps.HOUSE.human_spawns if human else Maps.HOUSE.mosquito_spawns
		for origin: Vector3 in origins:
			for station: Dictionary in Maps.HOUSE.stations:
				var target: Vector3 = station.p + (Vector3.ZERO if human else Vector3.UP * 1.2)
				var route: PackedVector3Array = Nav.path(origin, target, human)
				check(_route_clear(origin, route, human), "spawn route to %s role=%s" % [station.name, human])
		for source: Dictionary in Maps.HOUSE.stations:
			for target_station: Dictionary in Maps.HOUSE.stations:
				var offset: Vector3 = Vector3.ZERO if human else Vector3.UP * 1.2
				var route: PackedVector3Array = Nav.path(source.p + offset, target_station.p + offset, human)
				check(_route_clear(source.p + offset, route, human), "all station pairs: %s to %s role=%s" % [source.name,target_station.name,human])
		for pickup: Dictionary in Maps.HOUSE.pickups:
			var point: Vector3 = pickup.p + Vector3.UP * (1.05 if not human else -0.15)
			check(_route_clear(origins[0], Nav.path(origins[0], point, human), human), "pickup accessible on its floor for role")
	check(not Nav.can_travel(Vector3(0, 1.2, 0), Vector3(0, 4.4, 0), false), "mosquito cannot fly through upper slab")
	check(not Nav.can_travel(Vector3(-11, 3.2, -4.8), Vector3(-11, 3.2, 4.8), true), "human cannot walk across an unsupported stairwell")
	check(Nav.path(Vector3(INF,0,0),Vector3.ZERO,true).is_empty() and Nav.path(Vector3.ZERO,Vector3.ZERO,true,"unknown").is_empty(), "invalid input cannot invent a route")
	for side: float in [-1.0, 1.0]:
		var lower := Vector3(side*11.0,0.0,side*4.8)
		var upper := Vector3(side*11.0,3.2,-side*4.8)
		check(_follow_human(lower, upper), "authoritative human climbs stair %s without jump" % side)
		check(_follow_human(upper, lower), "authoritative human descends stair %s without teleport" % side)
	for station: Dictionary in Maps.HOUSE.stations:
		check(_follow_human(Maps.HOUSE.human_spawns[0],station.p), "human physically reaches task %s" % station.name)
	var elapsed: int = Time.get_ticks_msec() - start_ms
	print("ROUTE_TEST_RESULT checks=%d failures=%d elapsed_ms=%d" % [checks,failures,elapsed])
	quit(0 if failures == 0 else 1)

func _reached(graph: Dictionary, omitted_x: float) -> Array[int]:
	var reached: Array[int] = [0]
	var queue: Array[int] = [0]
	while not queue.is_empty():
		var current: int = queue.pop_front()
		for neighbor: int in graph.edges[current]:
			var a: Vector3 = graph.nodes[current]
			var b: Vector3 = graph.nodes[neighbor]
			if is_equal_approx(a.x,omitted_x) and is_equal_approx(b.x,omitted_x) and absf(a.y-b.y)>0.01:
				continue
			if not reached.has(neighbor):
				reached.append(neighbor)
				queue.append(neighbor)
	return reached

func _route_clear(origin: Vector3, route: PackedVector3Array, human: bool) -> bool:
	if route.is_empty(): return false
	var previous: Vector3 = origin
	for point: Vector3 in route:
		if not Nav.can_travel(previous,point,human): return false
		previous = point
	return true

func _follow_human(origin: Vector3, destination: Vector3) -> bool:
	var route: PackedVector3Array = Nav.path(origin,destination,true)
	if route.is_empty(): return false
	var actor: Dictionary = {"p":origin,"yaw":0.0,"pitch":0.0}
	var current: int = 0
	for tick: int in range(2400):
		var point: Vector3 = route[current]
		var delta: Vector3 = point-Vector3(actor.p)
		if Vector2(delta.x,delta.z).length()<0.12 and absf(delta.y)<0.23:
			current += 1
			if current == route.size():
				return Vector3(actor.p).distance_to(destination)<0.26
			point = route[current]
			delta = point-Vector3(actor.p)
		delta.y = 0.0
		var movement: Vector3 = delta.normalized() * minf(1.0, delta.length() / (ArenaData.HUMAN_SPEED / 30.0))
		ArenaData.step_human(actor,{"move":movement,"yaw":0.0},1.0/30.0)
		if not ArenaData.can_fit_human(actor.p):
			printerr("PHYSICAL_ROUTE body intersects geometry at ",actor.p)
			return false
	printerr("PHYSICAL_ROUTE timeout origin=",origin," destination=",destination," stuck=",actor.p," waypoint=",route[current])
	return false
