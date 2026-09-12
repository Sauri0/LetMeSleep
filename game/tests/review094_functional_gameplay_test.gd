extends SceneTree

const Practice = preload("res://scripts/practice_session.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const ArenaData = preload("res://scripts/arena.gd")
const DT := 0.05
const REPORT_PATH := "res://../work/review094-functional-gameplay-results.json"

var checks := 0
var failures := 0
var cases: Array[Dictionary] = []

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("REVIEW094_FUNCTIONAL_FAIL " + label)

func _initialize() -> void:
	var map_id := Maps.default_map_id()
	var data := Maps.get_map(map_id)
	check(map_id == "house-patio-v1", "functional gate runs the canonical authored map")
	check(not data.is_empty() and bool(data.get("playable", false)), "canonical map is playable")
	if not data.is_empty():
		_test_human_bot_tasks(map_id, data)
		_test_mosquito_bots_reach_patio(map_id, data)
	_write_report(map_id)
	print("REVIEW094_FUNCTIONAL checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func _test_human_bot_tasks(map_id: String, data: Dictionary) -> void:
	var session := Practice.new()
	var started_usec := Time.get_ticks_usec()
	session.start("mosquito", "sleep", {}, "QA alfa", {
		"map_id": map_id,
		"round_seconds": 180.0,
		"task_goal": 99,
	})
	check(session.active and session.sim.phase == "playing", "sleep practice starts with a real human bot")
	if not session.active:
		cases.append({"case":"human_tasks", "start_reason":str(session.sim.reason)})
		session.free()
		return
	check(session.sim.actors.has(101) and session.sim.actors[101].role == "human", "practice roster assigns the human bot")
	# A fresh match normally presents open doors. Close every authored leaf so
	# the long task round also exercises the production bot's door interaction.
	for door_id: String in session.sim.doors:
		session.sim.doors[door_id].angle = 0.0
		session.sim.doors[door_id].target_angle = 0.0
		session.sim.doors[door_id].moving = false
		session.sim.doors[door_id].blocked = false
	var completed: Array[Dictionary] = []
	var lower_completed := false
	var upper_completed := false
	var collision_free := true
	var maximum_horizontal_step := 0.0
	var maximum_vertical_step := 0.0
	var previous_position: Vector3 = session.sim.actors[101].p
	var player_sequence := 0
	for _tick: int in range(int(180.0 / DT) + 20):
		if session.sim.phase != "playing":
			break
		var task_before: Dictionary = session.sim.private_for(101).get("task", {}).duplicate(true)
		var done_before: int = session.sim.tasks_done
		player_sequence += 1
		session.send_input(player_sequence, Vector3.ZERO, 0.0, 0.0, false)
		session.advance(DT)
		var actor: Dictionary = session.sim.actors[101]
		var position: Vector3 = actor.p
		maximum_horizontal_step = maxf(maximum_horizontal_step, Vector2(previous_position.x - position.x, previous_position.z - position.z).length())
		maximum_vertical_step = maxf(maximum_vertical_step, absf(previous_position.y - position.y))
		previous_position = position
		var height := lerpf(ArenaData.HUMAN_HEIGHT, ArenaData.HUMAN_CROUCH_HEIGHT, float(actor.crouch_amount))
		collision_free = collision_free and ArenaData.can_fit_human(position, height, map_id, session.sim.doors)
		if session.sim.tasks_done > done_before and not task_before.is_empty():
			var station_index := int(task_before.station)
			var station: Dictionary = data.stations[station_index]
			var floor_index := 1 if Vector3(station.p).y > 1.0 else 0
			lower_completed = lower_completed or floor_index == 0
			upper_completed = upper_completed or floor_index == 1
			completed.append({
				"station":str(station.id),
				"floor":floor_index,
				"elapsed":session.sim.elapsed,
				"route_meters":float(task_before.route_meters),
				"budget":float(task_before.budget),
			})
	var brain_stats: Dictionary = session.brains[101].stats.duplicate(true)
	check(session.sim.phase == "results", "long sleep practice reaches an authoritative result")
	check(session.sim.tasks_done == session.sim.task_goal and session.sim.winner == "human", "human bot completes the collective task goal")
	check(completed.size() == session.sim.tasks_done and completed.size() >= 5, "every completed task is observed with its real assignment")
	check(lower_completed and upper_completed, "human bot completes tasks on both floors")
	check(int(brain_stats.task_inputs) > 0 and int(brain_stats.paths) > 0, "human bot uses real path and interaction inputs")
	check(int(brain_stats.doors) > 0, "human bot operates authored doors while completing tasks")
	check(collision_free, "human bot remains collision-valid through the long round")
	check(maximum_horizontal_step <= ArenaData.HUMAN_RUN_SPEED * DT + 0.04, "human bot never teleports horizontally between authority ticks")
	cases.append({
		"case":"human_tasks",
		"simulated_seconds":session.sim.elapsed,
		"wall_milliseconds":(Time.get_ticks_usec() - started_usec) / 1000.0,
		"tasks_done":session.sim.tasks_done,
		"task_goal":session.sim.task_goal,
		"completed":completed,
		"brain_stats":brain_stats,
		"maximum_horizontal_step_meters":maximum_horizontal_step,
		"maximum_vertical_step_meters":maximum_vertical_step,
		"collision_free":collision_free,
		"winner":session.sim.winner,
	})
	session.free()

func _test_mosquito_bots_reach_patio(map_id: String, data: Dictionary) -> void:
	var session := Practice.new()
	var started_usec := Time.get_ticks_usec()
	session.start("human", "survival", {}, "QA alfa", {
		"map_id":map_id,
		"round_seconds":60.0,
	})
	check(session.active and session.sim.phase == "playing", "survival practice starts with real mosquito bots")
	if not session.active:
		cases.append({"case":"mosquito_patio", "start_reason":str(session.sim.reason)})
		session.free()
		return
	var patio: AABB
	for area: Dictionary in data.exterior.areas:
		if str(area.id) == "patio":
			patio = area.bounds
	check(patio.has_volume(), "authored patio area is available to the functional gate")
	var refuges: Array = data.respawn_points
	var patio_target := Vector3(0, 1.35, 15)
	var patio_index := -1
	for index: int in range(refuges.size()):
		if Vector3(refuges[index]).distance_to(patio_target) < 0.01:
			patio_index = index
	check(patio_index >= 0, "production mosquito patrol includes the patio waypoint")
	var started_inside: Dictionary = {}
	var reached_patio: Dictionary = {}
	var collision_free := true
	var maximum_step := 0.0
	var previous_positions: Dictionary = {}
	for id: int in session.brains:
		# Select the production patrol's patio point as the first waypoint. The
		# route, movement, doors and collisions remain the live practice code.
		session.brains[id].patrol_index = posmod(patio_index - posmod(id, refuges.size()), refuges.size())
		var position: Vector3 = session.sim.actors[id].p
		started_inside[id] = _contains_horizontal(data.building_bounds, position)
		reached_patio[id] = false
		previous_positions[id] = position
	var player_sequence := 0
	for _tick: int in range(int(60.0 / DT) + 20):
		if session.sim.phase != "playing":
			break
		player_sequence += 1
		session.send_input(player_sequence, Vector3.ZERO, 0.0, 0.0, false)
		session.advance(DT)
		for id: int in session.brains:
			var actor: Dictionary = session.sim.actors[id]
			var position: Vector3 = actor.p
			maximum_step = maxf(maximum_step, Vector3(previous_positions[id]).distance_to(position))
			previous_positions[id] = position
			collision_free = collision_free and ArenaData.can_fit_mosquito(position, map_id, session.sim.doors)
			reached_patio[id] = bool(reached_patio[id]) or _contains_horizontal(patio, position)
	var stats: Dictionary = {}
	for id: int in session.brains:
		stats[str(id)] = session.brains[id].stats.duplicate(true)
		check(bool(started_inside[id]), "mosquito bot %d starts inside the house" % id)
		check(bool(reached_patio[id]), "mosquito bot %d reaches the authored patio" % id)
		check(int(session.brains[id].stats.paths) > 0 and int(session.brains[id].stats.moves) > 20, "mosquito bot %d follows a real production path" % id)
	check(session.sim.phase == "results" and session.sim.winner == "mosquito", "patio patrol survives to the authoritative round result")
	check(collision_free, "mosquito bots remain collision-valid from house to patio")
	check(maximum_step <= ArenaData.MOSQUITO_SPEED * DT + 0.04, "mosquito bots never teleport between authority ticks")
	cases.append({
		"case":"mosquito_patio",
		"simulated_seconds":session.sim.elapsed,
		"wall_milliseconds":(Time.get_ticks_usec() - started_usec) / 1000.0,
		"started_inside":started_inside,
		"reached_patio":reached_patio,
		"brain_stats":stats,
		"maximum_step_meters":maximum_step,
		"collision_free":collision_free,
		"winner":session.sim.winner,
	})
	session.free()

func _contains_horizontal(bounds: AABB, point: Vector3) -> bool:
	return point.x >= bounds.position.x and point.x <= bounds.end.x and point.z >= bounds.position.z and point.z <= bounds.end.z

func _write_report(map_id: String) -> void:
	var source_files := [
		"res://scripts/fixed_house.gd",
		"res://scripts/map_catalog.gd",
		"res://scripts/map_navigation.gd",
		"res://scripts/arena.gd",
		"res://scripts/simulation.gd",
		"res://scripts/bot_brain.gd",
		"res://scripts/practice_session.gd",
	]
	var hashes: Dictionary = {}
	for path: String in source_files:
		hashes[path] = FileAccess.get_sha256(path)
	var file := FileAccess.open(REPORT_PATH, FileAccess.WRITE)
	file.store_string(JSON.stringify({
		"gate":"review094-functional-gameplay",
		"map_id":map_id,
		"checks":checks,
		"failures":failures,
		"dt":DT,
		"cases":cases,
		"source_sha256":hashes,
	}, "\t"))
	file.close()
