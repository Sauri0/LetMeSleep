extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const Nav = preload("res://scripts/map_navigation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const DT := 0.025

func _initialize() -> void:
	var graph: Dictionary = Nav.graph_info(true)
	var records: Array[Dictionary] = []
	for index: int in range(graph.nodes.size()):
		for station: Dictionary in Maps.HOUSE.stations:
			var route: PackedVector3Array = Nav.path(graph.nodes[index], station.p, true)
			var previous: Vector3 = graph.nodes[index]
			var distance := 0.0
			for point: Vector3 in route:
				distance += Vector2(point.x - previous.x, point.z - previous.z).length()
				previous = point
			records.append({"node": index, "origin": graph.nodes[index], "station": station.label, "destination": station.p, "distance": distance, "route": route})
	records.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return float(a.distance) > float(b.distance))
	var worst: Dictionary = records[0]
	var physical: Dictionary = follow(worst.origin, worst.destination, worst.route)
	var other: Dictionary = follow(Maps.HOUSE.stations[5].p, Maps.HOUSE.stations[7].p, Nav.path(Maps.HOUSE.stations[5].p, Maps.HOUSE.stations[7].p, true))
	var top: Array[Dictionary] = []
	for index: int in range(5):
		var record: Dictionary = records[index].duplicate()
		record.erase("route")
		top.append(record)
	var result := {"nodes": graph.nodes.size(), "routes": records.size(), "walk_speed": ArenaData.HUMAN_SPEED, "top": top, "worst_physical": physical, "station_pair_physical": other}
	var file := FileAccess.open("res://../work/task-nav-deadlines04.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "\t"))
	file.close()
	print("TASK_NAV_DEADLINE_PROBE " + JSON.stringify(result))
	quit(0 if physical.valid and other.valid else 1)

func follow(origin: Vector3, destination: Vector3, route: PackedVector3Array) -> Dictionary:
	if route.is_empty():
		return {"seconds": 60.0, "valid": false, "reason": "no route"}
	var actor: Dictionary = {"p": origin, "yaw": 0.0, "pitch": 0.0}
	var current := 0
	for tick: int in range(2400):
		if Vector3(actor.p).distance_to(destination) < 1.2 and ArenaData.clear_segment(Vector3(actor.p) + Vector3.UP * 0.65, destination + Vector3.UP * 0.65):
			return {"seconds": float(tick) * DT, "total_work3": float(tick) * DT + 3.0, "valid": true, "reason": ""}
		var point: Vector3 = route[current]
		var delta: Vector3 = point - Vector3(actor.p)
		if Vector2(delta.x, delta.z).length() < 0.11 and absf(delta.y) < 0.23 and current < route.size() - 1:
			current += 1
			point = route[current]
			delta = point - Vector3(actor.p)
		delta.y = 0.0
		var movement: Vector3 = delta.normalized() * minf(1.0, delta.length() / (ArenaData.HUMAN_SPEED * DT))
		ArenaData.step_human(actor, {"move": movement, "yaw": 0.0, "sprint": false}, DT)
		if not ArenaData.can_fit_human(actor.p):
			return {"seconds": float(tick + 1) * DT, "valid": false, "reason": "body intersects geometry"}
	return {"seconds": 60.0, "valid": false, "reason": "timeout " + str(actor.p)}
