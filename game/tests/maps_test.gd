extends SceneTree

const Maps = preload("res://scripts/map_catalog.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Sim = preload("res://scripts/simulation.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	var house: Dictionary = Maps.get_map("house")
	var lobby: Dictionary = Maps.get_map("lobby")
	check(house.bounds.size == Vector3(28, 6.4, 22) and lobby.bounds.size == Vector3(8, 4, 6), "two-storey house is larger while lobby retains independent dimensions")
	check(Maps.is_playable("house") and not Maps.is_playable("lobby") and not Maps.is_playable("unknown"), "lobby is not a selectable round map")
	check(Maps.get_map("unknown").is_empty(), "unknown map produces no invented catalog entry")
	check(lobby.stations.is_empty() and lobby.pickups.is_empty() and lobby.obstacles.size() == 2, "lobby has its own benches and no gameplay stations or pickups")
	check(house.stations.size() == 8 and house.pickups.size() == 10 and house.rooms.size() == 16, "house has sixteen furnished rooms and ten supported tool instances")
	var physical_count: int = house.obstacles.size()
	check(house.structures.size() == physical_count, "every rendered structure has one authoritative obstacle")
	for index: int in range(physical_count):
		check(house.structures[index].box == house.obstacles[index], "visual/physics box %d identical" % index)
	for hole: AABB in house.stair_holes:
		var clear: bool = true
		for slab: AABB in house.floors:
			clear = clear and not slab.intersects(hole.grow(-0.001))
		check(clear, "stairwell is a real hole in upper slabs")
	check(house.steps.size() == 32 and house.stair_rise <= 0.22 and house.stair_tread >= 0.5, "both stairs have sixteen reachable short steps")
	for floor_y: float in [0.0, 3.2]:
		var stations: int = 0
		var pickups: int = 0
		for station: Dictionary in house.stations:
			if is_equal_approx(station.p.y, floor_y): stations += 1
		for pickup: Dictionary in house.pickups:
			if is_equal_approx(Vector3(pickup.approach).y, floor_y): pickups += 1
		check(stations >= 3 and pickups == 5, "each floor contains several tasks and five supported pickup instances")
	house.stations[0].name = "mutated"
	house.obstacles.clear()
	check(Maps.get_map("house").obstacles.size() == physical_count and Maps.get_map("house").stations[0].name != "mutated", "map callers cannot mutate catalog through returned dictionaries")
	check(Sim.sanitize_config({"map_id": "lobby"}).map_id == "house" and Sim.sanitize_config({"map_id": "invalid"}).map_id == "house", "round config validates playable map server-side")
	var positions: Array[Vector3] = []
	for index: int in range(16):
		var spawn: Vector3 = Maps.human_spawn("lobby", index)
		check(ArenaData.can_fit_human(spawn, ArenaData.HUMAN_HEIGHT, "lobby") and not positions.has(spawn), "lobby spawn %d unique and clear for human collision" % index)
		positions.append(spawn)
		var actor: Dictionary = {"p": spawn}
		ArenaData.step_human(actor, {"jump": true}, 0.15, "lobby")
		check(actor.p.y > 0.0 and not actor.grounded, "lobby spawn %d can use same authoritative jump" % index)
	for index: int in range(5):
		check(ArenaData.can_fit_human(Maps.human_spawn("house", index)), "house human spawn %d clear" % index)
		for other: int in range(index):
			check(Maps.human_spawn("house", index).distance_to(Maps.human_spawn("house", other)) > ArenaData.HUMAN_RADIUS * 2.0, "human spawns do not overlap")
	for index: int in range(12):
		var spawn: Vector3 = Maps.mosquito_spawn("house", index)
		check(ArenaData.move_body(spawn, Vector3.ZERO, false) == spawn, "house mosquito spawn %d clear" % index)
		for other: int in range(index):
			check(spawn.distance_to(Maps.mosquito_spawn("house", other)) > ArenaData.MOSQUITO_RADIUS * 2.0, "mosquito spawns do not overlap")
		for human: int in range(5):
			var eyes: Vector3 = Maps.human_spawn("house", human) + Vector3.UP * 1.65
			check(not ArenaData.clear_segment(eyes, spawn), "opposing spawns have no initial line of sight")
	var waiting_actor: Dictionary = {"p": Vector3.ZERO}
	for tick: int in range(4):
		ArenaData.step_human(waiting_actor, {"move": Vector3.BACK, "sprint": true}, 1.0, "lobby")
	check(waiting_actor.p.z <= 2.40001, "waiting movement uses lobby perimeter")
	check(not ArenaData.clear_segment(Vector3(3.7, 0.2, -1.2), Vector3(3.7, 0.2, 1.2), "lobby"), "lobby LOS uses its bench")
	check(ArenaData.clear_segment(Vector3(3.7, 0.2, -1.2), Vector3(3.7, 0.2, 1.2), "house"), "same line is clear in distinct house data")
	check(ArenaData.clear_segment(Vector3(0, 2.7, 0), Vector3(0, 3.5, 0), "lobby") and not ArenaData.clear_segment(Vector3(0, 2.7, 0), Vector3(0, 3.5, 0)), "house slab blocks vertical LOS without affecting the lobby")
	var sim = Sim.new()
	sim.start({1: {"role": "human"}, 2: {"role": "mosquito"}}, {"map_id": "house"})
	check(sim.public_snapshot().map_id == "house" and sim.pickups.size() == 10, "round snapshot publishes selected map with its ten pickups")
	print("MAPS_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + description)
