extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Maps = preload("res://scripts/map_catalog.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_flight()
	_test_focus()
	_test_exposed_poses()
	_test_defense()
	_test_occlusion()
	_test_extraction()
	print("FOCUS_COMBAT_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		if failures < 100:
			printerr("FAIL: " + description)

func make_sim(humans: int = 1, mosquitoes: int = 1, goal: float = 12.0) -> RefCounted:
	var sim = Sim.new()
	var roster: Dictionary = {}
	for id: int in range(1, humans + mosquitoes + 1):
		roster[id] = {"name": "Test", "role": "human" if id <= humans else "mosquito"}
	sim.start(roster, {"human_count": humans, "blood_goal": goal, "rotation_seconds": 40.0})
	return sim

func aim(sim: RefCounted, id: int, target: Vector3, held: bool, move: Vector3 = Vector3.ZERO) -> void:
	if sim.actors[id].role == "human":
		var angles: Vector2 = Pose.aim_angles(sim.actors[id], target)
		sim.submit_input(id, int(sim.actors[id]._input_seq) + 1, move, angles.x, angles.y, held)
	else:
		var delta: Vector3 = (target - Vector3(sim.actors[id].p)).normalized()
		sim.submit_input(id, int(sim.actors[id]._input_seq) + 1, move, atan2(-delta.x, -delta.z), asin(delta.y), held)

func place(sim: RefCounted, id: int, human: int, zone: int, distance: float = 0.8) -> void:
	sim.actors[id]._assignment = {"human": human, "zone": zone, "revision": 1}
	var posed: Dictionary = sim._zone_pose(sim.actors[id]._assignment)
	sim.actors[id].p = Vector3(posed.p) + Vector3(posed.normal) * distance
	sim.actors[id].velocity = Vector3.ZERO
	aim(sim, id, posed.p, false)

func concentrate(sim: RefCounted, id: int, seconds: float) -> void:
	for tick: int in range(int(ceil(seconds / 0.025))):
		var posed: Dictionary = sim._zone_pose(sim.actors[id]._assignment)
		aim(sim, id, posed.p, true)
		sim.step(0.025)

func _test_flight() -> void:
	check(ArenaData.flight_direction(Vector3.FORWARD, 0, 1.0).y > 0.84, "W climbs toward upward aim")
	check(ArenaData.flight_direction(Vector3.FORWARD, 0, -1.0).y < -0.84, "W descends toward downward aim")
	check(ArenaData.flight_direction(Vector3.UP, 1, -1) == Vector3.UP, "vertical auxiliary input remains world vertical")
	var origin: Vector3 = Maps.human_spawn("house", 0) + Vector3.UP * 1.4
	var actor: Dictionary = {"p": origin, "yaw": 0.0, "pitch": 0.7, "velocity": Vector3.ZERO}
	ArenaData.step_mosquito(actor, Vector3.FORWARD, 0.025)
	check(actor.velocity.length() < 0.4 and actor.velocity.length() > 0.0, "flight starts with acceleration rather than full speed")
	for tick: int in range(20):
		ArenaData.step_mosquito(actor, Vector3.FORWARD, 0.025)
	var release: Vector3 = actor.p
	for tick: int in range(7):
		ArenaData.step_mosquito(actor, Vector3.ZERO, 0.025)
	check(actor.velocity.length() < 0.001 and Vector3(actor.p).distance_to(release) < 0.30, "release brakes firmly within175ms and30cm")
	var ceiling: float = float(Maps.get_map("house").ceiling)
	actor.p = Vector3(origin.x, ceiling - 0.07, origin.z)
	actor.velocity = Vector3.ZERO
	for tick: int in range(10):
		ArenaData.step_mosquito(actor, Vector3.UP, 0.025)
	check(actor.p.y <= ceiling - ArenaData.MOSQUITO_RADIUS + 0.00001, "small insect remains inside ceiling")

func _test_focus() -> void:
	var sim = make_sim()
	place(sim, 2, 1, 5, 1.2)
	var original: Vector3 = sim.actors[2].p
	check(sim.private_for(2).focus.can_focus, "own exposed mark can acquire at1.2m")
	sim.action(2, 1, "bite")
	sim.step(0.025)
	check(sim.actors[2].state != "biting", "bite action cannot bypass concentration")
	concentrate(sim, 2, 0.50)
	check(sim.private_for(2).focus.progress > 0.3 and sim.actors[2].state == "flying", "held focus charges visibly without premature latch")
	check(Vector3(sim.actors[2].p).distance_to(original) < ArenaData.MOSQUITO_SPEED * 0.55, "assisted approach remains speed bounded")
	check(sim.actors[1].threatened and not sim.actors[1].bitten and sim.blood == 0.0, "human gets anticipation before attach or blood")
	aim(sim, 2, sim.private_for(2).assignment.p, false)
	sim.step(0.025)
	check(sim.private_for(2).focus.progress == 0.0, "releasing focus before attachment cancels charge")
	concentrate(sim, 2, 1.6)
	check(sim.actors[2].state == "biting", "continuous valid charge closes distance and attaches")
	check(sim.blood == 0.0, "preparation gives reaction time after attachment")
	var calendar: float = sim.actors[2]._next_rotation
	var old_zone: int = sim.actors[2]._assignment.zone
	sim.action(2, 2, "bite")
	sim.step(0.025)
	check(sim.actors[2].state == "flying" and sim.actors[2]._assignment.zone != old_zone and sim.actors[2]._next_rotation == calendar, "explicit detach changes mark without resetting schedule")
	concentrate(sim, 2, 1.5)
	check(sim.actors[2].state == "flying" and sim.private_for(2).focus.progress == 0.0, "held key cannot reattach after explicit detach until released")
	var public: Dictionary = sim.public_snapshot()
	check(public.actors[1].has("threatened") and not public.actors[2].has("focus") and not public.actors[2].has("_assignment"), "only generic warning is public; focus and assignment remain private")
	place(sim, 2, 1, Sim.FRONT_ZONE_COUNT, 0.8)
	sim.actors[2]._focus_suppressed = false
	var posed: Dictionary = sim._zone_pose(sim.actors[2]._assignment)
	sim.actors[2].p = Vector3(posed.p) - Vector3(posed.normal) * 0.8
	concentrate(sim, 2, 1.4)
	check(sim.actors[2].state != "biting" and sim.private_for(2).focus.progress == 0.0, "focus cannot pull through torso onto a rear mark")
	place(sim, 2, 1, 5, 0.8)
	concentrate(sim, 2, 0.5)
	sim.step(0.6)
	check(sim.actors[2].state != "biting" and sim.private_for(2).focus.progress == 0.0, "silent input loses focus before automated attachment")

func _test_exposed_poses() -> void:
	var sim = make_sim()
	var human: Dictionary = sim.actors[1]
	for crouch: float in [0.0, 0.5, 1.0]:
		for pitch: float in [-1.2, 0.0, 1.2]:
			for swing: float in [0.0]:
				human.merge({"crouch_amount": crouch, "pitch": pitch, "swing": swing}, true)
				for zone: Dictionary in Sim.BODY_ZONES:
					var posed: Dictionary = Pose.zone_pose(human, zone)
					var outward: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * 0.3
					check(not sim._body_occludes(outward, posed.p, -1), "surface exposed %s crouch%.1f pitch%.1f swing%.2f" % [zone.label, crouch, pitch, swing])

func _test_defense() -> void:
	for zone: int in range(Sim.FRONT_ZONE_COUNT):
		var sim = make_sim()
		place(sim, 2, 1, zone, 0.55)
		concentrate(sim, 2, 1.6)
		check(sim.actors[2].state == "biting", "all solo zones can concentrate and attach: %d" % zone)
		aim(sim, 1, sim.actors[2].p, false)
		sim.action(1, 1, "self_swat")
		sim.step(0.05)
		check(sim.actors[2].state == "biting", "swat has anticipation before physical gesture contact")
		sim.step(0.2)
		check(sim.actors[2].state == "stunned", "hands defend manually aimed solo zone during strike window: %d" % zone)
	var coop = make_sim(2)
	place(coop, 3, 1, Sim.FRONT_ZONE_COUNT, 0.5)
	concentrate(coop, 3, 1.6)
	coop.submit_input(1, 1, Vector3.ZERO, 0.0, -0.55, false)
	coop.action(1, 1, "self_swat")
	coop.step(0.3)
	check(coop.actors[3].state == "biting", "rear zone still requires teammate after combat adjustment")
	var mark: Dictionary = coop.private_for(3).assignment
	coop.actors[2].p = Vector3(coop.actors[1].p) + Vector3(mark.normal) * 1.0
	coop.actors[2].p.y = coop.actors[1].p.y
	aim(coop, 2, coop.actors[3].p, false)
	coop.action(2, 1, "attack")
	coop.step(0.3)
	check(coop.actors[3].state == "stunned", "teammate aimed slap removes rear insect from exposed side")

func _test_occlusion() -> void:
	var blocked = make_sim(2)
	blocked.actors[1].p = Vector3(-7.0, 0.0, 8.0)
	blocked.actors[2].p = Vector3(-7.0, 0.0, 7.65)
	blocked.actors[3].p = Vector3(-7.0, 1.2, 7.35)
	check(blocked._body_occludes(Vector3(-7.0, 1.55, 8.0), blocked.actors[3].p, 1), "defensive palm fixture has a human body between eye and free insect")
	blocked.submit_input(1, 1, Vector3.ZERO, 0.0, -0.55, false)
	blocked.action(1, 1, "self_swat")
	blocked.step(0.3)
	check(blocked.actors[3].state != "stunned", "defensive palm does not kill a free insect through another human")
	blocked.actors[2].p = Vector3(-5.0, 0.0, 8.0)
	blocked.actors[3].p = Vector3(-7.0, 1.2, 7.25)
	blocked.actors[3].velocity = Vector3.ZERO
	blocked.step(0.6)
	aim(blocked, 1, blocked.actors[3].p, false)
	blocked.action(1, 2, "self_swat")
	blocked.step(0.3)
	check(blocked.actors[3].state == "stunned", "same defensive palm reaches an exposed free insect under the clicked ray")
	var floor_sim = make_sim()
	floor_sim.actors[1].p = Vector3(-7.0, 3.2, 8.0)
	floor_sim.actors[2]._assignment = {"human": 1, "zone": 6, "revision": 1}
	var mark: Dictionary = floor_sim.private_for(2).assignment
	floor_sim.actors[2].p = Vector3(mark.p.x, 2.7, mark.p.z)
	aim(floor_sim, 2, mark.p, true)
	check(Vector3(floor_sim.actors[2].p).distance_to(mark.p) < Sim.FOCUS_DISTANCE, "upper-floor mark is within acquisition distance through slab")
	check(not floor_sim.private_for(2).focus.can_focus, "floor blocks focus despite sufficient range and aim")
	concentrate(floor_sim, 2, 1.5)
	check(floor_sim.actors[2].state != "biting" and floor_sim.private_for(2).focus.progress == 0.0, "holding focus never crosses a floor slab")
	var lower: Vector3 = Vector3(-7.0, 2.7, 8.0)
	var upper: Vector3 = ArenaData.move_body(lower, Vector3.UP * 1.0, false)
	check(upper.y <= 3.0 - ArenaData.MOSQUITO_RADIUS, "small flying insect cannot tunnel through the floor slab")

func _test_extraction() -> void:
	for count: int in [1, 2, 12]:
		var sim = make_sim(1, count)
		# This isolates feeding balance from navigation. Real focus is separately
		# exercised above: permanent attachment must not exceed shared throughput.
		for id: int in range(2, count + 2):
			if Dictionary(sim.actors[id]._assignment).is_empty():
				continue
			sim.actors[id].state = "biting"
			sim.actors[id]._bite_started = 0.0
		for tick: int in range(240):
			if sim.phase != "playing":
				break
			sim.step(0.1)
		var expected: float = Sim.BITE_PREPARATION + 12.0 / minf(count * Sim.BLOOD_PER_SECOND, Sim.SHARED_BLOOD_RATE_CAP)
		check(absf(sim.elapsed - expected) < 0.11, "unopposed quota timing count%d observed%.2f expected%.2f" % [count, sim.elapsed, expected])
		print("FEEDING_TIMING mosquitoes=%d seconds=%.2f" % [count, sim.elapsed])
