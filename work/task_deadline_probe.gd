extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const Nav = preload("res://scripts/map_navigation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const DT := 0.025
var records: Array[Dictionary] = []

func _initialize() -> void:
	var origins: Array[Dictionary] = []
	for index: int in range(Maps.HOUSE.human_spawns.size()):
		origins.append({"label": "spawn%d" % index, "p": Maps.HOUSE.human_spawns[index], "index": index, "spawn": true})
	for index: int in range(Maps.HOUSE.stations.size()):
		origins.append({"label": Maps.HOUSE.stations[index].label, "p": Maps.HOUSE.stations[index].p, "index": index, "spawn": false})
	for origin: Dictionary in origins:
		for index: int in range(Maps.HOUSE.stations.size()):
			var station: Dictionary = Maps.HOUSE.stations[index]
			var route: PackedVector3Array = Nav.path(origin.p, station.p, true)
			var travel: Dictionary = follow(origin.p, station.p, route)
			var flat: float = Vector2(origin.p.x - station.p.x, origin.p.z - station.p.z).length()
			var route_length := 0.0
			var previous: Vector3 = origin.p
			for point: Vector3 in route:
				route_length += Vector2(point.x - previous.x, point.z - previous.z).length()
				previous = point
			var record: Dictionary = {"from": origin.label, "from_index": origin.index, "spawn": origin.spawn, "to": station.label, "to_index": index, "flat_distance": flat, "route_horizontal_distance": route_length, "direct_lower_bound_total": maxf(0, flat - 1.2) / ArenaData.HUMAN_RUN_SPEED + 3.0, "travel_seconds": travel.seconds, "total_seconds": float(travel.seconds) + 3.0, "valid": travel.valid, "reason": travel.reason}
			records.append(record)
	var worst: Dictionary = {}
	var over8 := 0
	var over30 := 0
	var impossible8 := 0
	var invalid := 0
	var consecutive: Array[Dictionary] = []
	var initial: Array[Dictionary] = []
	for record: Dictionary in records:
		if not record.valid:
			invalid += 1
		if worst.is_empty() or float(record.total_seconds) > float(worst.total_seconds):
			worst = record
		over8 += 1 if float(record.total_seconds) > 8.0 else 0
		over30 += 1 if float(record.total_seconds) > 30.0 else 0
		impossible8 += 1 if float(record.direct_lower_bound_total) > 8.0 else 0
		if not record.spawn and posmod(int(record.from_index) + 1, 8) == int(record.to_index):
			consecutive.append(record)
		if record.spawn and int(record.from_index) == int(record.to_index):
			initial.append(record)
	var result := {"count": records.size(), "invalid": invalid, "speed": ArenaData.HUMAN_RUN_SPEED, "work": 3.0, "task_radius": 1.2, "routed_over8": over8, "routed_over30": over30, "direct_lower_bound_over8": impossible8, "worst": worst, "initial_tasks": initial, "consecutive_tasks": consecutive, "records": records}
	var output := FileAccess.open("res://../work/task-deadlines04.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(result, "\t"))
	output.close()
	result.erase("records")
	print("TASK_DEADLINE_PROBE " + JSON.stringify(result))
	quit(invalid)

func follow(origin: Vector3, destination: Vector3, route: PackedVector3Array) -> Dictionary:
	if route.is_empty():
		return {"seconds": 60.0, "valid": false, "reason": "no route"}
	var actor: Dictionary = {"p": origin, "yaw": 0.0, "pitch": 0.0}
	var current := 0
	for tick: int in range(2400):
		if Vector3(actor.p).distance_to(destination) < 1.2 and ArenaData.clear_segment(Vector3(actor.p) + Vector3.UP * 0.65, destination + Vector3.UP * 0.65):
			return {"seconds": float(tick) * DT, "valid": true, "reason": ""}
		var point: Vector3 = route[current]
		var delta: Vector3 = point - Vector3(actor.p)
		if Vector2(delta.x, delta.z).length() < 0.11 and absf(delta.y) < 0.23 and current < route.size() - 1:
			current += 1
			point = route[current]
			delta = point - Vector3(actor.p)
		delta.y = 0.0
		var movement: Vector3 = delta.normalized() * minf(1.0, delta.length() / (ArenaData.HUMAN_RUN_SPEED * DT))
		ArenaData.step_human(actor, {"move": movement, "yaw": 0.0, "sprint": true}, DT)
		if not ArenaData.can_fit_human(actor.p):
			return {"seconds": float(tick + 1) * DT, "valid": false, "reason": "body intersects geometry"}
	return {"seconds": 60.0, "valid": false, "reason": "timeout " + str(actor.p)}
