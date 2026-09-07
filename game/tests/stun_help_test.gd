extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_nonfatal_modes()
	_test_fall_and_controls()
	_test_help_rate()
	_test_help_validation()
	_test_queue_and_rematch()
	print("STUN_HELP_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + message)

func make_sim(mode: String = "blood", mosquitoes: int = 2) -> RefCounted:
	var sim = Sim.new()
	var players: Dictionary = {1: {"role": "human"}}
	for id: int in range(2, mosquitoes + 2):
		players[id] = {"role": "mosquito"}
	sim.start(players, {"mode": mode, "round_seconds": 180.0, "blood_goal": 1000.0, "rotation_seconds": 4.0, "mosquito_lives": 1, "respawn_seconds": 1.0})
	return sim

func advance(sim: RefCounted, seconds: float) -> void:
	for tick: int in range(int(round(seconds / 0.025))):
		sim.step(0.025)

func place_ground(sim: RefCounted, id: int) -> void:
	sim.actors[id].p = Vector3(-7, ArenaData.MOSQUITO_RADIUS, 8)
	sim.actors[id].velocity = Vector3.ZERO

func help_input(sim: RefCounted, id: int, target: int, held: bool = true) -> void:
	var delta: Vector3 = (Vector3(sim.actors[target].p) - Vector3(sim.actors[id].p)).normalized()
	sim.submit_input(id, int(sim.actors[id]._input_seq) + 1, Vector3.ZERO, atan2(-delta.x, -delta.z), asin(clampf(delta.y, -1, 1)), held)

func _test_nonfatal_modes() -> void:
	for mode: String in ["blood", "sleep", "survival"]:
		var sim = make_sim(mode, 1)
		var mosquito: Dictionary = sim.actors[2]
		mosquito._assignment = {"human": 1, "zone": 0, "revision": 1}
		mosquito.state = "biting"
		sim._update_attached()
		var target: Vector3 = mosquito.p
		var angles: Vector2 = Pose.aim_angles(sim.actors[1], target)
		sim.action(1, 1, "attack", angles.x, angles.y)
		advance(sim, 0.3)
		check(sim.private_for(1).attack.hit, "real manual contact registers in " + mode)
		if mode == "survival":
			check(not mosquito.alive and mosquito.state == "dead" and sim.winner == "human", "Survival retains one lethal life and total-elimination victory")
			advance(sim, 36.0)
			check(not mosquito.alive, "Survival never recovers an eliminated mosquito")
		else:
			check(mosquito.alive and mosquito.state == "stunned" and sim.phase == "playing", mode + " hit causes35second stun without elimination winner")
			check(Dictionary(sim.private_for(2).assignment).is_empty() and sim.private_for(2).focus.state == "stunned", "stun immediately releases bite reservation and disables concentration")
			var left: float = sim.private_for(2).stun.remaining
			sim._kill(2)
			check(sim.private_for(2).stun.remaining == left, "repeat hit cannot refresh stun timer")
			sim.blood = 7.0
			advance(sim, left - 0.025)
			check(mosquito.state == "stunned" and sim.phase == "playing", "single stunned insect remains until complete duration")
			var floor_position: Vector3 = mosquito.p
			advance(sim, 0.05)
			check(mosquito.alive and mosquito.state == "flying" and Vector3(mosquito.p).distance_to(floor_position) < 0.001, "recovery happens in place without respawn teleport")
			check(not Dictionary(mosquito._assignment).is_empty() and int(mosquito._assignment.zone) != 0, "recovery joins fresh reservation rather than reviving old bite")
			check(is_equal_approx(sim.blood, 7.0), "earned blood persists through fall and recovery")
			check(int(mosquito.lives) == 0 and sim.private_for(2).respawn_left == 0.0, "legacy life and respawn configuration does not apply")
	var timeout = make_sim("blood", 1)
	timeout.elapsed = 179.0
	timeout._kill(2)
	advance(timeout, 1.0)
	check(timeout.winner == "human" and timeout.actors[2].state == "stunned", "blood timeout can end while mosquito remains stunned")
	var tasks = make_sim("sleep", 1)
	tasks.tasks_done = tasks.task_goal
	tasks.elapsed = 179.0
	tasks._kill(2)
	advance(tasks, 1.0)
	check(tasks.winner == "human", "Tasks winner follows shared goal even when everyone is stunned")

func _test_fall_and_controls() -> void:
	for initial: Vector3 in [Vector3(-7, 1.8, 8), Vector3(-7, 5.5, 8), Vector3(-11, 2.0, -1.0)]:
		var sim = make_sim()
		var actor: Dictionary = sim.actors[2]
		actor.p = initial
		sim._kill(2)
		var floor_y: float = ArenaData.floor_below(initial) + ArenaData.MOSQUITO_RADIUS
		var last_y: float = actor.p.y
		for tick: int in range(80):
			sim.submit_input(2, tick + 1, Vector3(1, 1, -1), 0.5, -0.5, true)
			sim.action(2, tick + 1, "bite")
			sim.action(2, tick + 1000, "perch")
			sim.step(0.025)
			check(actor.p.y <= last_y + 0.0001 and actor.p.y >= floor_y - 0.0001, "stunned body falls without climbing or tunnelling through supporting floor")
			last_y = actor.p.y
		check(absf(float(actor.p.y) - floor_y) < 0.015 and actor.grounded, "stun lands on correct lower/upper/stair support")
		check(Vector2(actor.p.x, actor.p.z).distance_to(Vector2(initial.x, initial.z)) < 0.001 and actor.state == "stunned", "stunned movement and bite/perch actions cannot control body")
		check(Dictionary(actor._assignment).is_empty(), "rotations never reserve a zone while stunned")

func _test_help_rate() -> void:
	for helper_count: int in [0, 1, 2]:
		var sim = make_sim("blood", helper_count + 1)
		place_ground(sim, 2)
		sim._kill(2)
		for id: int in range(3, helper_count + 3):
			sim.actors[id].p = Vector3(-7.4, 0.3, 8 + (id - 3) * 0.15)
		for tick: int in range(40):
			for id: int in range(3, helper_count + 3):
				help_input(sim, id, 2)
			sim.step(0.025)
		var expected: float = 34.0 if helper_count == 0 else 31.0
		check(absf(float(sim.private_for(2).stun.remaining) - expected) < 0.0001, "recovery is1x alone and4x total regardless of helper count%d" % helper_count)
		if helper_count > 0:
			check(sim.private_for(2).stun.helped and sim.private_for(3).help.state == "helping" and sim.actors[3].help_target == 2, "private helper feedback and public physical help target match")
			check(sim.actors[3]._focus_progress == 0.0 and sim.private_for(3).focus.state == "helping", "help has priority over concentration while E is held")
			for tick: int in range(320):
				for id: int in range(3, helper_count + 3):
					help_input(sim, id, 2)
				sim.step(0.025)
				if sim.actors[2].state != "stunned":
					break
			check(absf(sim.elapsed - 8.75) < 0.03 and sim.actors[2].state == "flying", "sustained valid help completes35second recovery in8.75seconds")
			check(sim.actors[3].help_target == 0 and sim.private_for(3).help.state == "idle", "recovery ends physical helping immediately without auto-starting a bite")

func _test_help_validation() -> void:
	var sim = make_sim()
	place_ground(sim, 2)
	sim._kill(2)
	sim.actors[3].p = Vector3(-7.4, 0.3, 8)
	help_input(sim, 3, 2, false)
	check(sim.private_for(3).help.state == "ready", "aiming near stunned teammate advertises hold-E help")
	sim.submit_input(3, 2, Vector3.ZERO, PI * 0.5, 0.0, true)
	check(sim.private_for(3).help.state == "idle", "looking away cannot help")
	sim.actors[3].p = Vector3(-8.2, 0.3, 8)
	help_input(sim, 3, 2)
	check(sim.private_for(3).help.state == "idle", "outside80cm cannot help")
	sim.actors[2].p = Vector3(-7, 3.24, 8)
	sim.actors[3].p = Vector3(-7, 2.8, 8)
	help_input(sim, 3, 2)
	check(sim.private_for(3).help.state == "idle", "help cannot pass through floor despite distance and aim")
	place_ground(sim, 2)
	sim.actors[3].p = Vector3(-7.4, 0.3, 8)
	help_input(sim, 3, 2)
	advance(sim, 0.5)
	check(sim.actors[3].help_target == 0 and not sim.private_for(2).stun.helped, "silent input expires active help instead of assisting forever")
	check(not sim.private_for(1).has("stun") and not sim.private_for(1).has("help"), "humans never receive mosquito recovery timers or help choices")
	for actor: Dictionary in sim.public_snapshot().actors.values():
		check(not actor.has("stun") and not actor.has("help") and not actor.has("_stun_remaining"), "public state contains no private stun countdown")
	var copy: Dictionary = sim.private_for(2)
	copy.stun.remaining = -100
	check(sim.private_for(2).stun.remaining > 0.0, "private recovery snapshot cannot mutate authority")

func _test_queue_and_rematch() -> void:
	var sim = make_sim("blood", 12)
	var old: String = Sim._zone_key(sim.actors[2]._assignment)
	var next: float = sim.actors[2]._next_rotation
	var first_waiter: int = sim._assignment_waiters[0]
	sim._kill(2)
	sim.step(0.025)
	check(Sim._zone_key(sim.actors[first_waiter]._assignment) == old and not sim._assignment_waiters.has(2), "stun releases surface to FIFO without taking a place while incapacitated")
	advance(sim, 12.0)
	check(Dictionary(sim.actors[2]._assignment).is_empty() and sim.actors[2]._next_rotation > next and fmod(float(sim.actors[2]._next_rotation) - next, 4.0) < 0.001, "individual rotation schedule keeps its original phase throughout stun")
	var roster := {1: {"role": "human"}, 2: {"role": "mosquito"}}
	sim.start(roster, {"mode": "sleep"})
	check(sim.actors[2].state == "flying" and not sim.private_for(2).stun.active and sim.actors[2].help_target == 0 and sim.blood == 0.0, "new round clears all stun, help and old score state")
	sim._kill(2)
	sim.abort("Test interruption")
	check(not sim.private_for(2).stun.active and sim.private_for(2).stun.remaining == 0.0 and sim._assignment_waiters.is_empty(), "aborting a round removes stale stun timer and queue state")
