extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_roster_config()
	_test_one_versus_one()
	_test_role_appearance()
	_test_privacy_and_reservations()
	_test_inputs()
	_test_surface_perch()
	_test_attached_wall_clearance()
	_test_station_routes()
	_test_bite_calendar_and_blood()
	_test_all_solo_zones()
	_test_cooperative_rear()
	_test_tools()
	_test_lives_and_results()
	_test_tasks()
	_test_abort_rematch()
	print("RULES_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + description)

func roster(humans := 1, mosquitoes := 2) -> Dictionary:
	var players: Dictionary = {}
	for index: int in range(humans + mosquitoes):
		players[index + 1] = {"name": "Prueba %d" % (index + 1), "role": "human" if index < humans else "mosquito", "ready": true}
	return players

func make_sim(mode := "blood", humans := 1, mosquitoes := 2, options: Dictionary = {}) -> RefCounted:
	var result = Sim.new()
	var settings: Dictionary = {"mode": mode, "human_count": humans, "blood_goal": 1000.0, "round_seconds": 120.0, "rotation_seconds": 4.0}
	settings.merge(options, true)
	result.start(roster(humans, mosquitoes), settings)
	return result

func advance(sim: RefCounted, seconds: float) -> void:
	var remaining := seconds
	while remaining > 0.000001 and sim.phase == "playing":
		var part: float = minf(remaining, 0.05)
		sim.step(part)
		remaining -= part

func place_assignment(sim: RefCounted, mosquito: int, human: int, zone: int) -> void:
	sim.actors[mosquito]._assignment = {"human": human, "zone": zone, "revision": int(sim.actors[mosquito]._revision)}
	var pose: Dictionary = sim._zone_pose(sim.actors[mosquito]._assignment)
	sim.actors[mosquito].p = Vector3(pose.p) + Vector3(pose.normal) * 0.20

func attach(sim: RefCounted, mosquito: int, human: int, zone: int, sequence := 1) -> void:
	place_assignment(sim, mosquito, human, zone)
	sim.action(mosquito, sequence, "bite")
	for tick: int in range(64):
		var pose: Dictionary = sim._zone_pose(sim.actors[mosquito]._assignment)
		var delta: Vector3 = (Vector3(pose.p) - Vector3(sim.actors[mosquito].p)).normalized()
		sim.submit_input(mosquito, int(sim.actors[mosquito]._input_seq) + 1, Vector3.ZERO, atan2(-delta.x, -delta.z), asin(delta.y), true)
		sim.step(0.025)

func aim_at(sim: RefCounted, human: int, target: Vector3, sequence := 1) -> void:
	var angles: Vector2 = Pose.aim_angles(sim.actors[human], target)
	sim.submit_input(human, sequence, Vector3.ZERO, angles.x, angles.y, false)

func _test_roster_config() -> void:
	check(Sim.validate_roster(roster()).is_empty(), "1v2 roster accepted")
	check(Sim.validate_roster(roster(2, 4)).is_empty(), "2v4 roster accepted")
	check(Sim.validate_roster(roster(1, 12)).is_empty(), "maximum solo mosquitoes have zone alternatives")
	check(Sim.validate_roster(roster(1, 1)).is_empty(), "minimum 1v1 roster accepted")
	check(Sim.validate_roster(roster(2, 3)).is_empty(), "old two-to-one ratio removed")
	check(Sim.validate_roster(roster(5, 1)).is_empty(), "five humans versus one mosquito accepted")
	check(not Sim.validate_roster(roster(1, 0)).is_empty(), "empty mosquito team rejected")
	check(not Sim.validate_roster(roster(0, 1)).is_empty(), "empty human team rejected")
	check(Sim.validate_roster(roster(5, 10)).is_empty(), "confirmed maximum 5v10 roster accepted")
	check(not Sim.validate_roster(roster(6, 12)).is_empty(), "confirmed human capacity enforced")
	check(not Sim.validate_roster(roster(5, 12)).is_empty(), "16-player transport room limit enforced")
	check(not Sim.validate_roster(roster(1, 13)).is_empty(), "experimental mosquito capacity enforced")
	var bad: Dictionary = roster()
	bad[1].role = "admin"
	check(not Sim.validate_roster(bad).is_empty(), "unknown role rejected")
	var settings: Dictionary = Sim.sanitize_config({"mode": "unknown", "round_seconds": NAN, "mosquito_lives": 99, "task_interval": 15, "task_work": 8, "task_deadline": 99, "task_floor": 0})
	check(settings.mode == "blood" and is_finite(float(settings.round_seconds)), "malformed config cannot introduce NaN or unknown mode")
	check(int(settings.mosquito_lives) == 9, "legacy lives option remains sanitized for old room presets")
	check(float(settings.task_deadline) < float(settings.task_interval) and float(settings.task_floor) >= Sim.minimum_task_deadline(float(settings.task_work)), "tasks maintain interval and walking travel allowance")
	check(int(Sim.DEFAULT_CONFIG.human_count) == 1, "human-count setting defaults to one")
	check(int(Sim.sanitize_config({"human_count": 5}).human_count) == 5, "human count sanitized independently of connected players")
	check(int(Sim.sanitize_config({"human_count": 99}).human_count) == 5 and int(Sim.sanitize_config({"human_count": -5}).human_count) == 1, "human count retains fixed supported bounds")

func _test_one_versus_one() -> void:
	for mode: String in ["blood", "survival", "sleep"]:
		var sim = make_sim(mode, 1, 1)
		check(sim.phase == "playing" and sim.actors.size() == 2, "1v1 starts in %s" % mode)
		check(sim.actors[1].role == "human" and sim.actors[2].role == "mosquito", "1v1 has both assigned roles in %s" % mode)
		var assignment: Dictionary = sim.private_for(2).assignment
		check(int(assignment.human) == 1 and int(assignment.zone) < Sim.FRONT_ZONE_COUNT, "1v1 uses only self-defendable front zones in %s" % mode)
		attach(sim, 2, 1, 5)
		check(sim.actors[2].state == "biting", "1v1 allows a valid bite in %s" % mode)
		var previous: String = Sim._zone_key(sim.actors[2]._assignment)
		sim.action(2, 2, "bite")
		sim.step(0.05)
		check(Sim._zone_key(sim.actors[2]._assignment) != previous, "1v1 keeps a valid detach alternative in %s" % mode)
	var sparse = make_sim("blood", 5, 1)
	var visited_humans: Dictionary = {int(sparse.private_for(6).assignment.human): true}
	for interval: int in range(4):
		advance(sparse, 4.0)
		visited_humans[int(sparse.private_for(6).assignment.human)] = true
	check(visited_humans.size() == 5, "5v1 rotates equal-pressure targets instead of favoring the first human forever")

func _test_role_appearance() -> void:
	var players: Dictionary = roster(1, 1)
	players[1].cosmetics = {"human": {"color": 4, "accessory": 2}, "mosquito": {"color": 1, "accessory": 1}, "secret": "omit"}
	players[2].cosmetics = {"human": {"color": 0, "accessory": 1}, "mosquito": {"color": 5, "accessory": 2}}
	var sim = Sim.new()
	sim.start(players, {})
	var snapshot: Dictionary = sim.public_snapshot()
	check(snapshot.actors[1].appearance.color == 4 and snapshot.actors[1].appearance.accessory == 2 and not snapshot.actors[1].appearance.has("secret"), "human actor copies only its human appearance")
	check(snapshot.actors[2].appearance.color == 5 and snapshot.actors[2].appearance.accessory == 2, "mosquito actor copies only its mosquito appearance")
	check(not snapshot.actors[1].has("cosmetics") and not snapshot.actors[2].has("cosmetics"), "actors never publish entire cosmetics profile")
	snapshot.actors[1].appearance.color = 99
	players[2].cosmetics.mosquito.color = 99
	check(int(sim.public_snapshot().actors[1].appearance.color) == 4, "public appearance snapshot cannot mutate authority")
	check(int(sim.public_snapshot().actors[2].appearance.color) == 5, "source profile changes do not mutate an active-round appearance")
	players[1].cosmetics = "invalid"
	players[2].cosmetics = {"mosquito": {"color": -1, "accessory": 999, "extra": "omit"}}
	sim.start(players, {})
	check(sim.public_snapshot().actors[1].appearance == {"color":0,"accessory":0,"eyes":0,"mouth":0,"brows":0,"hair":0,"hair_color":0,"mustache":0,"beard":0,"outfit":0,"footwear":0,"accent":0}, "invalid appearance profile gets safe defaults")
	check(sim.public_snapshot().actors[2].appearance == {"color":0,"accessory":0,"eyes":0,"mouth":0,"brows":0,"hair":0,"outfit":0,"footwear":0,"accent":0}, "out-of-range appearance fields cannot leak into actors")

func _test_privacy_and_reservations() -> void:
	var sim = make_sim("blood", 1, 12)
	var seen: Dictionary = {}
	for id: int in sim._mosquito_ids:
		var assignment: Dictionary = sim.private_for(id).assignment
		if assignment.is_empty():
			check(sim.private_for(id).focus.state == "waiting", "overflow receives private waiting feedback")
			continue
		var key: String = Sim._zone_key(assignment)
		check(not seen.has(key), "unique reservation for mosquito %d" % id)
		seen[key] = true
		check(int(assignment.zone) < Sim.FRONT_ZONE_COUNT, "solo never receives rear zone")
	var snapshot: Dictionary = sim.public_snapshot()
	for actor: Dictionary in snapshot.actors.values():
		check(not actor.has("assignment") and not actor.has("zone") and not actor.has("_assignment") and not actor.has("_next_rotation"), "public actor contains no hidden assignments or rotation schedule")
	check(not sim.private_for(1).has("assignment"), "human receives no assignment")
	var private_data: Dictionary = sim.private_for(2)
	private_data.assignment.zone = 99
	check(int(sim.private_for(2).assignment.zone) != 99, "private snapshot cannot mutate simulation")
	snapshot.pickups[1].holder = 999
	check(int(sim.pickups[1].holder) == 0, "public snapshot pickups cloned")
	for index: int in range(15):
		advance(sim, 1.0)
		seen.clear()
		for id: int in sim._mosquito_ids:
			if Dictionary(sim.actors[id]._assignment).is_empty():
				continue
			var key: String = Sim._zone_key(sim.actors[id]._assignment)
			check(not seen.has(key), "reservations remain unique through fixed rotations")
			seen[key] = true
	var pair = make_sim()
	var first_revision: int = int(pair.actors[2]._revision)
	var second_revision: int = int(pair.actors[3]._revision)
	advance(pair, 4.0)
	check(int(pair.actors[2]._revision) > first_revision and int(pair.actors[3]._revision) == second_revision, "mosquito schedules have independent fixed phase offsets")
	var maximum = make_sim("blood", 5, 10)
	seen.clear()
	for id: int in maximum._mosquito_ids:
		var key: String = Sim._zone_key(maximum.actors[id]._assignment)
		check(not seen.has(key), "5v10 assignments remain distinct")
		seen[key] = true

func _test_inputs() -> void:
	var sim = make_sim()
	var before: Vector3 = sim.actors[1].p
	sim.submit_input(1, 1, Vector3(1000, 0, 0), 0.0, 0.0, false)
	sim.step(0.1)
	check(Vector3(sim.actors[1].p).distance_to(before) <= 0.311, "movement intent bounded to human speed")
	var valid_position: Vector3 = sim.actors[1].p
	sim.submit_input(1, 0, Vector3(-1, 0, 0), 1.0, 1.0, true)
	check(float(sim.actors[1].yaw) == 0.0 and not bool(sim.actors[1]._interact), "reordered movement input discarded")
	sim.submit_input(1, 2, Vector3(NAN, 0, 0), NAN, INF, true)
	check(int(sim.actors[1]._input_seq) == 1, "nonfinite input rejected without poisoning sequence")
	sim.submit_input(1, 2, Vector3(0, 1000, 0), 0.0, 0.0, false)
	sim.step(0.1)
	check(is_equal_approx(float(sim.actors[1].p.y), 0.0), "human cannot fly")
	sim.submit_input(1, 3, Vector3(0, 0, -1), PI / 2, 0.0, false)
	sim.step(0.1)
	check(float(sim.actors[1].p.x) < valid_position.x, "movement is local intent rotated by yaw")
	advance(sim, 0.5)
	var stopped: Vector3 = sim.actors[1].p
	advance(sim, 0.3)
	check(Vector3(sim.actors[1].p).is_equal_approx(stopped), "stale movement stops before disconnect timeout")
	sim.actors[1].p = Vector3(float(Sim.ArenaData.HALF_X) - 0.7, 0, 0)
	sim.submit_input(1, 4, Vector3.RIGHT, 0.0, 0.0, false)
	sim.step(0.3)
	check(float(sim.actors[1].p.x) <= float(Sim.ArenaData.HALF_X) - Sim.ArenaData.HUMAN_RADIUS + 0.001, "server enforces arena wall")
	sim.actors[1].p = Vector3(-5.2, 0, -1.15)
	sim.submit_input(1, 5, Vector3.LEFT, 0.0, 0.0, false)
	sim.step(0.3)
	check(float(sim.actors[1].p.x) >= -5.8 + Sim.ArenaData.HUMAN_RADIUS - 0.001, "server blocks movement through furniture")
	sim.actors[1].p = Vector3.ZERO
	sim.submit_input(1, 6, Vector3.ZERO, 0.0, 0.0, false)
	sim.actors[2].p = Vector3(0, 1.1, -0.6)
	sim.submit_input(2, 1, Vector3.BACK, 0.0, 0.0, false)
	sim.step(0.1)
	check(float(sim.actors[2].p.z) < -0.279, "mosquito body softly collides with human torso")
	sim.step(0.2)
	check(float(sim.actors[2].p.z) < -0.279, "continued input cannot tunnel through human torso")

func _test_surface_perch() -> void:
	var sim = make_sim()
	sim.actors[2].p = Vector3(-7, 1.5, 8)
	sim.action(2, 1, "perch")
	sim.step(0.05)
	check(sim.actors[2].state == "flying", "perch cannot freeze insect in open air")
	sim.actors[2].p = Vector3(-7, 0.2, 8)
	sim.action(2, 2, "perch")
	sim.step(0.05)
	check(sim.actors[2].state == "perched" and absf(float(sim.actors[2].p.y) - Sim.ArenaData.MOSQUITO_RADIUS) <= 0.0051, "perch snaps mosquito onto floor")
	sim.submit_input(2, 1, Vector3.UP, 0.0, 0.0, false)
	sim.step(0.05)
	check(sim.actors[2].state == "flying" and float(sim.actors[2].p.y) > Sim.ArenaData.MOSQUITO_RADIUS, "movement lifts mosquito from surface")
	sim.actors[2].p = Vector3(float(Sim.ArenaData.HALF_X) - 0.4, 1.2, 0)
	sim.actors[2].velocity = Vector3.ZERO
	sim.submit_input(2, 2, Vector3.ZERO, 0.0, 0.0, false)
	sim.action(2, 3, "perch")
	sim.step(0.05)
	check(sim.actors[2].state == "perched" and float(sim.actors[2].p.x) > float(Sim.ArenaData.HALF_X) - 0.4 and Sim.ArenaData.move_body(sim.actors[2].p, Vector3.ZERO, false).is_equal_approx(sim.actors[2].p), "perch snaps onto inner wall surface")
	sim.actors[2].state = "flying"
	sim.actors[2].p = Vector3(-6.5, 0.8, -1.15)
	sim.action(2, 4, "perch")
	sim.step(0.05)
	check(sim.actors[2].state == "perched" and is_equal_approx(float(sim.actors[2].p.y), 0.62 + Sim.ArenaData.MOSQUITO_RADIUS + 0.005), "perch snaps onto furnished table surface")

func _test_attached_wall_clearance() -> void:
	var sim = make_sim("blood", 2, 4)
	var edges: Array[Vector3] = [
		Vector3(100, 0, 0), Vector3(-100, 0, 0), Vector3(0, 0, 100), Vector3(0, 0, -100),
		Vector3(100, 0, 100), Vector3(100, 0, -100), Vector3(-100, 0, 100), Vector3(-100, 0, -100),
	]
	for edge: Vector3 in edges:
		var legal_human: Vector3 = Sim.ArenaData.move_body(Vector3.ZERO, edge, true)
		for zone: int in range(Sim.BODY_ZONES.size()):
			for angle: int in range(16):
				sim.actors[1].p = legal_human
				sim.actors[1].yaw = TAU * float(angle) / 16.0
				sim.actors[1].body_yaw = sim.actors[1].yaw
				sim.actors[3]._assignment = {"human": 1, "zone": zone, "revision": 1}
				sim.actors[3].state = "biting"
				sim.step(0.001)
				var point: Vector3 = sim.actors[3].p
				var radius: float = Sim.ArenaData.MOSQUITO_RADIUS
				var inside: bool = absf(point.x) + radius <= Sim.ArenaData.HALF_X + 0.00001 and absf(point.z) + radius <= Sim.ArenaData.HALF_Z + 0.00001 and point.y >= radius and point.y + radius <= Sim.ArenaData.CEILING
				check(inside and sim.actors[3].state == "biting", "attached zone %d at yaw %d stays wholly inside map edge %s" % [zone, angle, str(edge)])

func _test_station_routes() -> void:
	# Whole-house physical routes and both staircases live in map_routes_test.
	# Here retain the rules-level interaction check at every authored endpoint.
	var sim = make_sim("sleep", 1, 2, {"task_work": 1.0})
	sim.actors[1]._next_task = 100.0
	var sequence := 0
	for station_id: int in range(Sim.Maps.HOUSE.stations.size()):
		var station: Dictionary = Sim.Maps.HOUSE.stations[station_id]
		check(Sim.ArenaData.can_fit_human(station.p), "station %d has a valid standing endpoint" % station_id)
		sim.actors[1].p = station.p
		sim.actors[1].velocity = Vector3.ZERO
		sim.actors[1]._task = {"name": station.name, "station": station_id, "p": station.p, "remaining": 6.0, "progress": 0.0, "work": 1.0}
		var completed_before: int = int(sim.tasks_done)
		for tick: int in range(6):
			sequence += 1
			sim.submit_input(1, sequence, Vector3.ZERO, 0.0, 0.0, true)
			sim.step(0.2)
		check(int(sim.tasks_done) == completed_before + 1, "station %d task completes from reachable clear position" % station_id)

func _test_bite_calendar_and_blood() -> void:
	var sim = make_sim()
	attach(sim, 2, 1, 5)
	check(sim.actors[2].state == "biting", "valid nearby exposed mark permits bite")
	sim.action(2, 1, "bite")
	sim.step(0.05)
	check(sim.actors[2].state == "biting", "duplicate action does not detach")
	sim.submit_input(2, 1, Vector3.RIGHT, 0.0, 0.0, false)
	sim.step(0.05)
	check(sim.actors[2].state == "biting", "movement and released interact never detach")
	var reserved: String = Sim._zone_key(sim.actors[2]._assignment)
	var human_origin: Vector3 = sim.actors[1].p
	var mosquito_origin: Vector3 = sim.actors[2].p
	sim.submit_input(1, 1, Vector3.RIGHT, 0.0, 0.0, false)
	sim.step(0.1)
	var moved_pose: Dictionary = sim._zone_pose(sim.actors[2]._assignment)
	check(Vector3(sim.actors[2].p).is_equal_approx(Vector3(moved_pose.p) + Vector3(moved_pose.normal) * Sim.ATTACH_OFFSET) and Vector3(sim.actors[1].p) != human_origin and Vector3(sim.actors[2].p) != mosquito_origin, "attached mosquito follows translated and animated human surface")
	advance(sim, 4.1)
	check(Sim._zone_key(sim.actors[2]._assignment) == reserved, "attached reservation survives scheduled rotations")
	check(float(sim.blood) > 2.5, "blood extracted progressively after preparation")
	var earned: float = float(sim.blood)
	var upcoming: float = float(sim.actors[2]._next_rotation)
	var revision: int = int(sim.actors[2]._revision)
	sim.actors[2]._next_rotation = float(sim.elapsed) + 0.05
	var coincident: float = float(sim.actors[2]._next_rotation)
	sim.action(2, 2, "bite")
	sim.step(0.05)
	check(sim.actors[2].state == "flying" and Sim._zone_key(sim.actors[2]._assignment) != reserved, "explicit detach immediately chooses another zone")
	check(int(sim.actors[2]._revision) == revision + 1, "coincident detach and rotation perform only one assignment")
	check(is_equal_approx(float(sim.actors[2]._next_rotation), coincident + 4.0), "detach preserves calendar at coincident boundary")
	check(float(sim.blood) == earned, "detach does not grant or remove blood")
	advance(sim, 4.0)
	check(Sim._zone_key(sim.actors[2]._assignment) != reserved, "last detached zone excluded until successful new bite")
	sim._kill(2)
	sim.step(0.05)
	check(float(sim.blood) == earned, "death preserves shared blood")
	check(upcoming > 4.0, "calendar progressed while attached")
	var invalid = make_sim()
	place_assignment(invalid, 2, 1, 5)
	var pose: Dictionary = invalid._zone_pose(invalid.actors[2]._assignment)
	invalid.actors[2].p = Vector3(pose.p) - Vector3(pose.normal) * 0.2
	invalid.action(2, 1, "bite")
	invalid.step(0.05)
	check(invalid.actors[2].state != "biting", "bite cannot approach through human torso")
	invalid.actors[2].p = Vector3(5, 2, 4)
	invalid.action(2, 2, "bite")
	invalid.step(0.05)
	check(invalid.actors[2].state != "biting", "distant bite rejected")

func _test_all_solo_zones() -> void:
	for zone: int in range(Sim.FRONT_ZONE_COUNT):
		var sim = make_sim()
		attach(sim, 2, 1, zone)
		check(sim.actors[2].state == "biting", "solo zone %d reachable for attachment" % zone)
		sim.action(1, 1, "self_swat", 0.0, 0.0)
		sim.step(0.30)
		check(sim.actors[2].state == "biting", "wrong manual aim cannot automatically swat zone %d" % zone)
		var angles: Vector2 = Pose.aim_angles(sim.actors[1], sim.actors[2].p)
		sim.action(1, 2, "attack", angles.x, angles.y)
		sim.step(0.05)
		check(sim.actors[2].state == "biting", "Q and LMB share cooldown")
		advance(sim, 0.85)
		angles = Pose.aim_angles(sim.actors[1], sim.actors[2].p)
		sim.action(1, 3, "self_swat", angles.x, angles.y)
		sim.step(0.3)
		check(sim.actors[2].state == "stunned", "starting hands defend solo zone %d with manual ray" % zone)

func _test_cooperative_rear() -> void:
	var sim = make_sim("blood", 2, 4)
	var rear_assigned := false
	for id: int in sim._mosquito_ids:
		if int(sim.actors[id]._assignment.zone) >= Sim.FRONT_ZONE_COUNT:
			rear_assigned = true
	check(rear_assigned, "cooperative roster includes rear assignments")
	sim.actors[1].p = Vector3.ZERO
	sim.actors[2].p = Vector3(0, 0, 1.5)
	attach(sim, 3, 1, Sim.FRONT_ZONE_COUNT)
	check(sim.actors[3].state == "biting", "rear mark can be bitten from behind")
	sim.action(1, 1, "self_swat")
	sim.step(0.05)
	check(sim.actors[3].state == "biting", "owner cannot self-swat rear zone")
	sim.actors[1].tool = "broom"
	advance(sim, 0.85)
	aim_at(sim, 1, sim.actors[3].p, 1)
	sim.action(1, 2, "attack")
	sim.step(0.05)
	check(sim.actors[3].state == "biting", "long tool cannot bypass own rear-zone restriction")
	# Looking at one's own rear rotates the whole body; restore body facing before
	# testing which side the teammate is on.
	sim.submit_input(1, 2, Vector3.ZERO, 0.0, 0.0, false)
	sim.step(0.05)
	sim.actors[2].p = Vector3(0, 0, -0.65)
	aim_at(sim, 2, sim.actors[3].p, 1)
	sim.action(2, 1, "attack")
	sim.step(0.05)
	check(sim.actors[3].state == "biting", "teammate cannot hit rear mosquito through torso from front")
	sim.actors[2].p = Vector3(0, 0, 1.0)
	sim.actors[2].body_yaw = 0.0
	sim.actors[2].yaw = 0.0
	advance(sim, 0.85)
	aim_at(sim, 2, sim.actors[3].p, 2)
	sim.action(2, 2, "attack")
	sim.step(0.3)
	check(sim.actors[3].state == "stunned", "teammate aimed hands rescue exposed rear zone")

func _test_tools() -> void:
	var sim = make_sim("blood", 2, 4)
	check(sim.actors[1].tool == "hands" and sim.actors[2].tool == "hands", "humans start with hands")
	var pick_position: Vector3 = Sim.Maps.HOUSE.pickups[0].approach
	sim.actors[1].p = pick_position
	sim.actors[2].p = pick_position
	sim.action(1, 1, "pickup")
	sim.action(2, 1, "pickup")
	sim.step(0.05)
	check(int(sim.pickups[1].holder) == 1 and sim.actors[1].tool == "swatter" and sim.actors[2].tool == "hands", "simultaneous pickup has one atomic owner")
	sim.action(1, 1, "drop")
	sim.step(0.05)
	check(int(sim.pickups[1].holder) == 1, "duplicated action sequence cannot drop tool")
	sim.actors[1].p = Sim.Maps.HOUSE.pickups[1].approach
	sim.action(1, 2, "pickup")
	sim.step(0.05)
	check(int(sim.pickups[1].holder) == 0 and int(sim.pickups[2].holder) == 1 and sim.actors[1].tool == "racket", "swap drops old tool and grants new instance atomically")
	check(sim.pickups.size() == Sim.Maps.HOUSE.pickups.size(), "tool swap does not clone or destroy pickup instances")
	sim.action(1, 3, "drop")
	sim.step(0.05)
	check(sim.actors[1].tool == "hands" and int(sim.pickups[2].holder) == 0, "drop returns starting hands")
	sim.actors[3].p = sim.pickups[2].p
	sim.action(3, 1, "pickup")
	sim.step(0.05)
	check(int(sim.pickups[2].holder) == 0, "mosquito cannot steal tool")
	sim.actors[1].p = Vector3(5, 0, 4)
	sim.action(1, 4, "pickup")
	sim.step(0.05)
	check(sim.actors[1].tool == "hands", "out-of-range pickup rejected")
	check(float(Sim.TOOL_STATS.broom.reach) > float(Sim.TOOL_STATS.hands.reach) and float(Sim.TOOL_STATS.newspaper.cooldown) < float(Sim.TOOL_STATS.hands.cooldown) and float(Sim.TOOL_STATS.racket.radius) > float(Sim.TOOL_STATS.hands.radius), "tool catalog has distinct reach speed and area")

func _test_lives_and_results() -> void:
	var blood_sim = make_sim("blood", 1, 2, {"mosquito_lives": 9, "respawn_seconds": 1.0})
	blood_sim._kill(2)
	advance(blood_sim, 1.2)
	check(bool(blood_sim.actors[2].alive) and blood_sim.actors[2].state == "stunned" and int(blood_sim.actors[2].lives) == 0, "blood hit stuns rather than using legacy lives or respawn")
	blood_sim._kill(3)
	blood_sim.step(0.05)
	check(blood_sim.phase == "playing" and blood_sim.winner.is_empty(), "all mosquitoes stunned does not end blood early")
	var quota = make_sim("blood", 1, 2, {"blood_goal": 1.0})
	attach(quota, 2, 1, 5)
	advance(quota, 2.0)
	check(quota.winner == "mosquito" and is_equal_approx(float(quota.blood), 1.0), "shared quota ends blood exactly once")
	var settled: float = float(quota.elapsed)
	quota.step(0.5)
	check(float(quota.elapsed) == settled, "finished round immutable under further simulation steps")
	var timeout = make_sim("blood", 1, 2, {"round_seconds": 30.0})
	advance(timeout, 30.0)
	check(timeout.winner == "human", "blood time limit with insufficient quota awards humans")
	var survival = make_sim("survival", 1, 2, {"round_seconds": 30.0})
	survival._kill(2)
	advance(survival, 30.0)
	check(survival.winner == "mosquito", "one mosquito alive at survival deadline wins without needing to bite")
	var survival_dead = make_sim("survival")
	survival_dead._kill(2)
	survival_dead._kill(3)
	survival_dead.step(0.05)
	check(survival_dead.winner == "human", "survival total elimination awards humans")
	var sleep_sim = make_sim("sleep", 1, 2, {"mosquito_lives": 1, "respawn_seconds": 1.0})
	for cycle: int in range(3):
		sleep_sim._kill(2)
		sleep_sim._kill(3)
		sleep_sim.step(0.05)
		check(sleep_sim.actors[2].alive and sleep_sim.actors[2].state == "stunned" and sleep_sim.phase == "playing", "Tasks repeated hits remain nonfatal without exhausting personal lives")
		check(float(sleep_sim.private_for(2).stun.remaining) > 34.0 and Dictionary(sleep_sim.private_for(2).assignment).is_empty(), "stunned player gets own recovery timer and no reserved mark")
		advance(sleep_sim, 35.0)
		check(bool(sleep_sim.actors[2].alive) and sleep_sim.actors[2].state == "flying" and not Dictionary(sleep_sim.private_for(2).assignment).is_empty(), "automatic recovery keeps actor alive and grants fresh private mark")
	check(sleep_sim.phase == "playing" and sleep_sim.winner.is_empty(), "repeated stuns do not manufacture a Tasks elimination winner")

func _test_tasks() -> void:
	var sim = make_sim("sleep", 2, 4, {"task_interval": 36.0, "task_deadline": 26.0, "task_work": 1.0, "task_penalty": 2.0, "task_floor": 22.0})
	advance(sim, 4.55)
	check(not Dictionary(sim.private_for(1).task).is_empty() and not Dictionary(sim.private_for(2).task).is_empty(), "each human receives personal task on staggered fixed calendar")
	var next_one: float = float(sim.actors[1]._next_task)
	var next_two: float = float(sim.actors[2]._next_task)
	var round_length: float = float(sim.config.round_seconds)
	var other_remaining: float = float(sim.actors[2]._task.remaining)
	sim.actors[1]._task.remaining = 0.05
	sim.step(0.05)
	check(int(sim.private_for(1).failures) == 1 and float(sim.private_for(1).deadline) == 24.0, "failure penalizes only failing human's future deadline")
	check(int(sim.private_for(2).failures) == 0 and float(sim.private_for(2).deadline) == 26.0 and is_equal_approx(float(sim.actors[2]._task.remaining), other_remaining - 0.05), "teammate deadline and active-task budget unaffected by other failure")
	check(float(sim.actors[1]._next_task) == next_one and float(sim.actors[2]._next_task) == next_two and float(sim.config.round_seconds) == round_length, "failure never changes schedules or round duration")
	var progress_sim = make_sim("sleep", 1, 2, {"task_work": 1.0, "rotation_seconds": 40.0})
	advance(progress_sim, 3.05)
	progress_sim.actors[1].p = progress_sim.actors[1]._task.p
	progress_sim.submit_input(1, 1, Vector3.ZERO, 0.0, 0.0, true)
	progress_sim.step(0.25)
	var progress: float = float(progress_sim.actors[1]._task.progress)
	check(progress > 0.0, "nearby held interaction advances personal task")
	attach(progress_sim, 2, 1, 5)
	check(progress_sim.actors[2].state == "biting", "task interruption fixture completes authoritative concentration")
	progress = float(progress_sim.actors[1]._task.progress)
	progress_sim.submit_input(1, 2, Vector3.ZERO, 0.0, 0.0, true)
	progress_sim.step(0.25)
	check(is_equal_approx(float(progress_sim.actors[1]._task.progress), progress), "attached mosquito pauses and preserves task progress")
	progress_sim.action(2, 2, "bite")
	progress_sim.submit_input(1, 3, Vector3.ZERO, 0.0, 0.0, true)
	progress_sim.step(0.25)
	check(float(progress_sim.actors[1]._task.progress) > progress, "task can resume after explicit detach")
	for index: int in range(4):
		progress_sim.submit_input(1, 4 + index, Vector3.ZERO, 0.0, 0.0, true)
		progress_sim.step(0.25)
	check(int(progress_sim.tasks_done) == 1, "completed task increments collective total once")
	var success = make_sim("sleep", 1, 2, {"round_seconds": 30.0})
	success.tasks_done = success.task_goal
	success.step(0.05)
	check(success.phase == "playing", "task goal evaluated at round end as prototype rule")
	advance(success, 30.0)
	check(success.winner == "human", "sleep collective task goal awards humans at close")
	var blocked = make_sim("sleep", 1, 2, {"round_seconds": 30.0})
	advance(blocked, 30.0)
	check(blocked.winner == "mosquito", "sleep incomplete collective goal awards mosquitoes at close")

func _test_abort_rematch() -> void:
	var sim = make_sim("sleep")
	attach(sim, 2, 1, 5)
	sim.abort("Participante desconectado")
	check(sim.phase == "lobby" and sim.winner == "" and not bool(sim.actors[1].bitten), "disconnect abort returns lobby with no winner or ghost bite")
	check(Dictionary(sim.private_for(2).assignment).is_empty(), "abort clears private assignments")
	sim.start(roster(), {"mode": "sleep"})
	check(sim.phase == "playing" and float(sim.blood) == 0.0 and int(sim.tasks_done) == 0 and int(sim.actors[2].lives) == 0 and not bool(sim.private_for(2).stun.active) and sim.actors[1].tool == "hands", "rematch resets modes, stun state, tools and score")
	for pickup: Dictionary in sim.pickups.values():
		check(int(pickup.holder) == 0, "rematch resets all pickup ownership")
