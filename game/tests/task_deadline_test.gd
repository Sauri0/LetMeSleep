extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Nav = preload("res://scripts/map_navigation.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const DT := 0.025
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_config()
	_test_walking_task()
	_test_penalty_floor()
	_test_round_cutoff()
	_test_first_opportunity()
	_test_default_objective()
	print("TASK_DEADLINE_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(failures)

func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("TASK_DEADLINE_FAIL " + label)

func _test_config() -> void:
	var defaults: Dictionary = Sim.sanitize_config({})
	check(defaults.task_deadline == 30.0 and defaults.task_interval == 36.0, "initial deadline and frequency remain unchanged")
	check(defaults.task_work == 3.0 and defaults.task_penalty == 2.0 and defaults.task_goal == 0, "work, personal penalty and collective goal policy remain unchanged")
	check(defaults.task_floor == 24.0, "default visible floor includes walking allowance")
	for work: int in range(1, 9):
		var config: Dictionary = Sim.sanitize_config({"task_work": work, "task_interval": 15, "task_deadline": 8, "task_floor": 4})
		var minimum: float = float(work) + Sim.TASK_TRAVEL_RESERVE
		check(config.task_deadline == minimum and config.task_floor == minimum, "unsafe requested deadline/floor corrected for work%d" % work)
		check(config.task_interval >= minimum + 0.5 and config.task_deadline <= config.task_interval - 0.5, "frequency leaves complete deadline for work%d" % work)
		check(Sim.sanitize_config(config) == config, "sanitized room config is stable for work%d" % work)
	check(Sim.minimum_task_deadline(NAN) == 24.0 and Sim.minimum_task_deadline(INF) == 24.0, "UI helper handles malformed work safely")
	check(Sim.minimum_round_seconds({"mode": "sleep", "human_count": 1, "task_work": 8}) == 33.0 and Sim.minimum_round_seconds({"mode": "sleep", "human_count": 5, "task_work": 8}) == 39.0, "short task rounds include every first slot and dispatch margin")
	check(Sim.minimum_round_seconds({"mode": "blood", "human_count": 5, "task_work": 8}) == 30.0 and Sim.minimum_round_seconds({"mode": "survival", "human_count": 5, "task_work": 8}) == 30.0, "other modes retain their round-duration minimum")

func _test_walking_task() -> void:
	# Offline scan covered all 94 authored nodes x 8 stations. Its longest path
	# is node 5 -> Mosquitero, 52.43 horizontal metres. Keep that physical worst
	# fixture in the build suite; do not rerun all 752 routes on every export.
	var origin := Vector3(11.0, 0.0, -8.2)
	var station: Dictionary = Maps.HOUSE.stations[5]
	var route: PackedVector3Array = Nav.path(origin, station.p, true)
	check(not route.is_empty(), "worst walking route remains authored and connected")
	if route.is_empty():
		return
	var sim = Sim.new()
	sim.start({1: {"name": "Walker", "role": "human"}, 2: {"name": "Idle", "role": "mosquito"}}, {"mode": "sleep", "task_deadline": 8, "task_floor": 8, "task_goal": 1})
	# Fixture setup selects a legitimate starting place and the scheduled task;
	# its time, work, movement and completion are production authority throughout.
	sim.actors[1].p = origin
	sim.actors[1]._tasks_given = 5
	sim.actors[1]._next_task = 0.0
	sim.step(DT)
	check(sim.private_for(1).task.station == 5 and sim.private_for(1).task.remaining == 24.0, "server issues visible minimum deadline without a hidden per-task bonus")
	var current := 0
	var first_work := -1.0
	var collision_free := true
	var sprinted := false
	for tick: int in range(960):
		var task: Dictionary = sim.private_for(1).task
		if task.is_empty():
			break
		var position: Vector3 = sim.actors[1].p
		var near: bool = position.distance_to(task.p) < 1.2 and ArenaData.clear_segment(position + Vector3.UP * 0.65, Vector3(task.p) + Vector3.UP * 0.65)
		var movement := Vector3.ZERO
		if not near:
			var delta: Vector3 = Vector3(route[current]) - position
			if Vector2(delta.x, delta.z).length() < 0.11 and absf(delta.y) < 0.23 and current < route.size() - 1:
				current += 1
				delta = Vector3(route[current]) - position
			delta.y = 0.0
			movement = delta.normalized() * minf(1.0, delta.length() / (ArenaData.HUMAN_SPEED * DT))
		elif first_work < 0:
			first_work = sim.elapsed
		sim.submit_input(1, tick, movement, 0.0, 0.0, near, false, false, false)
		sim.step(DT)
		collision_free = collision_free and ArenaData.can_fit_human(sim.actors[1].p)
		sprinted = sprinted or bool(sim.actors[1].sprinting)
	check(collision_free and not sprinted, "worst route completes with ordinary walking and no collision")
	check(sim.tasks_done == 1 and sim.private_for(1).failures == 0, "real work completes before the minimum deadline")
	check(first_work > 15.0 and sim.elapsed < 21.0, "walking plus work retains at least three seconds of minimum-deadline margin")
	check(sim.config.task_interval == 36.0 and sim.task_goal == 1, "travel does not change frequency or shared objective")
	print("TASK_WALKING_TIMING arrival=%.3f completion=%.3f deadline=24.000" % [first_work, sim.elapsed])

func _test_penalty_floor() -> void:
	var sim = Sim.new()
	sim.start({1: {"name": "Idle", "role": "human"}, 2: {"name": "Idle insect", "role": "mosquito"}}, {"mode": "sleep", "round_seconds": 180.0})
	var goal: int = sim.task_goal
	var deadlines: Array[float] = []
	var previous_failures := 0
	for tick: int in range(3600):
		sim.step(0.05)
		var own: Dictionary = sim.private_for(1)
		if int(own.failures) > previous_failures:
			deadlines.append(float(own.deadline))
			previous_failures = int(own.failures)
	check(deadlines == [28.0, 26.0, 24.0, 24.0, 24.0], "natural failures reduce only the personal deadline and stop at the visible floor")
	check(sim.config.task_interval == 36.0 and sim.actors[1]._next_task == 183.0, "failure never accelerates the fixed assignment calendar")
	check(sim.task_goal == goal and sim.tasks_done == 0 and sim.winner == "mosquito", "personal penalties preserve collective goal and result semantics")

func _test_round_cutoff() -> void:
	var sim = Sim.new()
	sim.start({1: {"name": "Idle", "role": "human"}, 2: {"name": "Idle insect", "role": "mosquito"}}, {"mode": "sleep"})
	check(sim.config.round_seconds == 120.0 and sim.task_goal == 2, "default120 counts three eligible opportunities and applies collective two-thirds goal")
	var issued_at: Array[float] = []
	var last_count := 0
	for tick: int in range(2400):
		sim.step(0.05)
		var count: int = sim.actors[1]._tasks_given
		if count > last_count:
			issued_at.append(snappedf(sim.elapsed, 0.05))
			last_count = count
	check(issued_at == [3.0, 39.0, 75.0], "late111-second task is omitted while cadence stays36 seconds")
	check(sim.private_for(1).task.is_empty() and sim.actors[1]._next_task == 147.0, "omitted late task adds no impossible obligation or calendar reset")
	var bounded = Sim.new()
	bounded.start({1: {"name": "Walker", "role": "human"}, 2: {"name": "Idle insect", "role": "mosquito"}}, {"mode": "sleep", "round_seconds": 30.0, "task_goal": 99})
	for tick: int in range(60):
		bounded.step(0.05)
	check(bounded.task_goal == 1 and is_equal_approx(float(bounded.private_for(1).task.remaining), 27.0), "visible task budget is capped by round remainder and configured goal by viable opportunities")
	check(float(bounded.private_for(1).deadline) == 30.0, "round-remainder cap does not penalize the personal future deadline")

func _test_first_opportunity() -> void:
	for humans: int in [1, 5]:
		for work: float in [1.0, 8.0]:
			var roster: Dictionary = {}
			for id: int in range(1, humans + 1):
				roster[id] = {"name": "Human", "role": "human"}
			roster[100] = {"name": "Insect", "role": "mosquito"}
			var config: Dictionary = {"mode": "sleep", "human_count": humans, "round_seconds": 30.0, "task_work": work}
			var sim = Sim.new()
			sim.start(roster, config)
			check(sim.config.round_seconds >= Sim.minimum_round_seconds(config), "extreme short round is sanitized for%d humans/work%.0f" % [humans, work])
			var received: Dictionary = {}
			var budgets_valid := true
			# Irregular ticks exercise tasks dispatched just after a fixed phase.
			for tick: int in range(270):
				sim.step(0.037)
				for id: int in range(1, humans + 1):
					var task: Dictionary = sim.private_for(id).task
					if not task.is_empty() and not received.has(id):
						received[id] = true
						budgets_valid = budgets_valid and float(task.remaining) >= Sim.minimum_task_deadline(work) and float(task.remaining) <= float(sim.config.round_seconds) - sim.elapsed + 0.00001
			check(received.size() == humans and budgets_valid, "every human receives at least one fully budgeted task even after dispatch latency")
			check(sim.task_goal == int(ceil(float(humans) * 2.0 / 3.0)), "extreme-room shared goal counts only first viable opportunities")

func _test_default_objective() -> void:
	var sim = Sim.new()
	sim.start({1: {"name": "Walker", "role": "human"}, 2: {"name": "Idle insect", "role": "mosquito"}}, {"mode": "sleep"})
	var route := PackedVector3Array()
	var current := 0
	var station := -1
	var routes_valid := true
	var collision_free := true
	for tick: int in range(4800):
		var task: Dictionary = sim.private_for(1).task
		var movement := Vector3.ZERO
		var near := false
		if not task.is_empty():
			var position: Vector3 = sim.actors[1].p
			if int(task.station) != station:
				station = int(task.station)
				route = Nav.path(position, task.p, true)
				current = 0
				routes_valid = routes_valid and not route.is_empty()
			near = position.distance_to(task.p) < 1.2 and ArenaData.clear_segment(position + Vector3.UP * 0.65, Vector3(task.p) + Vector3.UP * 0.65)
			if not near and not route.is_empty():
				var delta: Vector3 = Vector3(route[current]) - position
				if Vector2(delta.x, delta.z).length() < 0.11 and absf(delta.y) < 0.23 and current < route.size() - 1:
					current += 1
					delta = Vector3(route[current]) - position
				delta.y = 0.0
				movement = delta.normalized() * minf(1.0, delta.length() / (ArenaData.HUMAN_SPEED * DT))
		sim.submit_input(1, tick, movement, 0.0, 0.0, near, false, false, false)
		sim.step(DT)
		collision_free = collision_free and ArenaData.can_fit_human(sim.actors[1].p)
	check(routes_valid and collision_free, "default round task routes are physically walkable from real spawn without sprint")
	check(sim.tasks_done == 3 and sim.task_goal == 2 and sim.private_for(1).failures == 0, "default shared target is attainable with real travel and work before cutoff")
	check(sim.phase == "results" and sim.winner == "human", "default120 closes with correct collective winner after viable tasks")
	print("TASK_DEFAULT_ROUND completed=%d goal=%d winner=%s" % [sim.tasks_done, sim.task_goal, sim.winner])
