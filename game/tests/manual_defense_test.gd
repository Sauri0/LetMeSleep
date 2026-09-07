extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const ArenaData = preload("res://scripts/arena.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_view()
	_test_visibility()
	_test_manual_contact()
	_test_queue()
	_test_feedback_and_actions()
	_test_occlusion()
	_test_moving_focus()
	_test_wall_gesture()
	print("MANUAL_DEFENSE_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		if failures < 100:
			printerr("FAIL: " + description)

func make_sim(humans: int = 1, mosquitoes: int = 1) -> RefCounted:
	var sim = Sim.new()
	var roster: Dictionary = {}
	for id: int in range(1, humans + mosquitoes + 1):
		roster[id] = {"role": "human" if id <= humans else "mosquito"}
	sim.start(roster, {"mode": "survival", "rotation_seconds": 4.0, "round_seconds": 180.0})
	return sim

func contact_fixture(sim: RefCounted, zone: int, crouch: float = 0.0, tool: String = "hands") -> void:
	var human: Dictionary = sim.actors[1]
	human.crouch_amount = crouch
	human._crouch = crouch > 0.0
	human._last_input = sim.elapsed
	human.tool = tool
	sim.actors[2]._assignment = {"human": 1, "zone": zone, "revision": 1}
	sim.actors[2].state = "biting"
	sim._update_attached()

func _test_view() -> void:
	var actor: Dictionary = {"p": Vector3(-7, 0, 8), "yaw": 0.0, "pitch": 0.0, "body_yaw": 0.0}
	Pose.apply_view(actor, 0.7, -0.8, 0.05)
	check(actor.inspecting and actor.body_yaw == 0.0 and is_equal_approx(actor.yaw, 0.7), "looking down separates view yaw from torso")
	Pose.apply_view(actor, 2.5, -0.6, 0.05)
	check(actor.inspecting and is_equal_approx(actor.yaw, 2.5) and actor.body_yaw > 0.0, "inspection consumes all mouse yaw and torso follows beyond comfortable range")
	Pose.apply_view(actor, 0.7, -0.4, 0.05)
	check(not actor.inspecting and actor.body_yaw > 0.0 and actor.body_yaw < actor.yaw, "looking forward recovers torso gradually")
	var before: Dictionary = actor.duplicate(true)
	Pose.apply_view(actor, INF, NAN, 0.1)
	check(actor == before, "invalid view cannot alter body or camera")
	Pose.apply_view(actor, 0.7, -10.0)
	check(actor.pitch == Pose.HUMAN_PITCH_MIN, "human can inspect beyond vertical with bounded pitch")
	var origin: Vector3 = Pose.view_origin(actor)
	actor.yaw = -0.5
	check(Pose.view_origin(actor) == origin, "head yaw does not orbit or teleport camera around body")
	var movement: Dictionary = {"p": Vector3(-7, 0, 8), "yaw": 0.7, "body_yaw": 0.0, "pitch": -1.0, "inspecting": true}
	var start: Vector3 = movement.p
	ArenaData.step_human(movement, {"yaw": 0.7, "pitch": -1.0, "move": Vector3.FORWARD}, 0.1)
	var delta: Vector3 = Vector3(movement.p) - start
	check(delta.normalized().dot(Vector3.FORWARD.rotated(Vector3.UP, 0.7)) > 0.999 and movement.body_yaw == 0.0, "WASD follows view while inspecting frozen torso")
	for hz: int in [20, 60]:
		for direction: float in [-1.0, 1.0]:
			var turning: Dictionary = {"yaw":0.0,"body_yaw":0.0,"pitch":-1.65}
			var view_sum := 0.0
			var body_sum := 0.0
			for frame: int in range(hz * 8):
				var old_view: float = turning.yaw
				var old_body: float = turning.body_yaw
				var requested: float = direction * TAU * float(frame + 1) / float(hz * 4)
				Pose.apply_view(turning, requested, -1.65, 1.0 / hz)
				var view_step: float = wrapf(float(turning.yaw)-old_view,-PI,PI)
				var body_step: float = wrapf(float(turning.body_yaw)-old_body,-PI,PI)
				view_sum += view_step
				body_sum += body_step
				check(view_step * direction > 0.0 and body_step * direction >= -0.000001, "continuous downward turn never freezes or counter-rotates")
			check(absf(view_sum-direction*TAU*2) < 0.001 and absf(body_sum) > TAU, "two full downward turns remain available without looking up at %dHz" % hz)

func _test_visibility() -> void:
	for crouch: float in [0.0, 0.5, 1.0]:
		for grounded: bool in [true, false]:
			for phase: float in [0.0, PI * 0.5, PI, PI * 1.5]:
				for yaw: float in [0.0, 1.2]:
					var human: Dictionary = {"p": Vector3(-7, 0.25 if not grounded else 0.0, 8), "yaw": yaw, "body_yaw": yaw, "grounded": grounded, "crouch_amount": crouch, "motion_phase": phase, "motion_speed": 5.0, "sprinting": true}
					for zone: int in range(Sim.FRONT_ZONE_COUNT):
						var posed: Dictionary = Pose.zone_pose(human, Sim.BODY_ZONES[zone])
						var point: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * Sim.ATTACH_OFFSET
						var angles: Vector2 = Pose.aim_angles(human, point)
						var aimed: Dictionary = human.duplicate(true)
						Pose.apply_view(aimed, angles.x, angles.y)
						var origin: Vector3 = Pose.view_origin(aimed)
						var direction: Vector3 = (point - origin).normalized()
						var hit: Dictionary = Pose.ray_body(aimed, origin, point, true)
						check(Pose.view_direction(aimed).dot(direction) > 0.9999, "front mark fits actual yaw/pitch limits z%d c%.1f ground%s phase%.2f" % [zone, crouch, str(grounded), phase])
						check(hit.is_empty() or float(hit.distance) >= origin.distance_to(point) - 0.02, "own body does not hide front mark z%d c%.1f ground%s phase%.2f" % [zone, crouch, str(grounded), phase])
						check(ArenaData.clear_segment(origin, point), "view/mark remains outside authored floor and walls")

func _test_manual_contact() -> void:
	for tool: String in Sim.TOOL_STATS:
		for crouch: float in [0.0, 1.0]:
			for zone: int in range(Sim.FRONT_ZONE_COUNT):
				var sim = make_sim()
				contact_fixture(sim, zone, crouch, tool)
				var target: Vector3 = sim.actors[2].p
				var angles: Vector2 = Pose.aim_angles(sim.actors[1], target)
				sim.action(1, 1, "attack", angles.x, angles.y)
				sim.step(0.025)
				check(sim.actors[2].alive and sim.private_for(1).attack.state == "windup", "manual click has physical anticipation")
				var strike: Dictionary = sim.actors[1].strike
				check((Vector3(strike.point) - Vector3(strike.origin)).normalized().dot(strike.direction) > 0.9999, "locked contact point remains on clicked ray")
				var witnessed := false
				for tick: int in range(12):
					var was_alive: bool = sim.actors[2].alive
					sim.step(0.025)
					if was_alive and not bool(sim.actors[2].alive):
						var actor: Dictionary = sim.actors[1]
						var contact: Vector3 = Vector3(actor.p) + Vector3(Pose.sample(actor).strike_contact).rotated(Vector3.UP, Pose.body_yaw(actor))
						witnessed = contact.distance_to(sim.actors[2].p) <= float(Sim.TOOL_STATS[str(actor.strike.tool)].radius) + ArenaData.MOSQUITO_RADIUS + 0.0001
				check(not sim.actors[2].alive and witnessed, "visible palm/tool physically hits zone%d crouch%.1f tool%s" % [zone, crouch, tool])
				if zone == 5 and tool != "hands":
					check(sim.actors[1].strike.hand == "left" and sim.actors[1].strike.tool == "hands", "own right forearm uses free left palm without moving bitten arm")
	# The same legacy key now misses unless its own manual ray actually points at contact.
	var miss = make_sim()
	contact_fixture(miss, 0)
	miss.action(1, 1, "self_swat", 0.0, 0.0)
	miss.step(0.35)
	check(miss.actors[2].alive and miss.private_for(1).attack.state == "miss", "Q cannot auto-select body band or attached insect")
	var cooldown: float = miss.actors[1].swing
	var angles: Vector2 = Pose.aim_angles(miss.actors[1], miss.actors[2].p)
	miss.action(1, 2, "attack", angles.x, angles.y)
	miss.step(0.025)
	check(miss.private_for(1).attack.id == 1 and miss.actors[1].swing < cooldown, "Q and LMB share recovery without restarting gesture")

func _test_queue() -> void:
	var sim = make_sim(1, 12)
	check(sim.phase == "playing" and sim._assignment_waiters.size() == 4, "1v12 admits eight readable marks and four waiting insects")
	var first_waiter: int = sim._assignment_waiters[0]
	var donor := 2
	var old_key: String = Sim._zone_key(sim.actors[donor]._assignment)
	var calendar: float = sim.actors[donor]._next_rotation
	sim._assign(donor)
	check(Sim._zone_key(sim.actors[first_waiter]._assignment) == old_key and Dictionary(sim.actors[donor]._assignment).is_empty(), "released mark goes to oldest waiter before donor")
	check(sim.actors[donor]._next_rotation == calendar and sim.private_for(donor).focus.state == "waiting", "waiting preserves individual calendar and is private")
	var visited: Dictionary = {}
	for tick: int in range(480):
		sim.step(0.025)
		var occupied: Dictionary = {}
		for id: int in sim._mosquito_ids:
			var assignment: Dictionary = sim.actors[id]._assignment
			if assignment.is_empty():
				continue
			visited[id] = true
			var key: String = Sim._zone_key(assignment)
			check(not occupied.has(key) and int(assignment.zone) < Sim.FRONT_ZONE_COUNT, "FIFO rotation preserves unique frontal reservations")
			occupied[key] = true
	check(visited.size() == 12, "every living mosquito receives a turn through fixed independent rotations")
	var attached = make_sim(1, 12)
	for id: int in range(2, 10):
		attached.actors[id].state = "biting"
	attached._update_attached()
	var retained: String = Sim._zone_key(attached.actors[2]._assignment)
	for tick: int in range(160):
		attached.step(0.05)
	check(Sim._zone_key(attached.actors[2]._assignment) == retained and attached._assignment_waiters.size() == 4, "active bite keeps reservation even when others wait")
	var first: int = attached._assignment_waiters[0]
	var schedule: float = attached.actors[2]._next_rotation
	attached._detach(2)
	check(Sim._zone_key(attached.actors[first]._assignment) == retained and Dictionary(attached.actors[2]._assignment).is_empty() and attached.actors[2]._next_rotation == schedule, "explicit detach gives occupied surface to queue without resetting calendar")

func _test_feedback_and_actions() -> void:
	var sim = make_sim()
	check(not sim.private_for(1).bite_feedback.active, "free assignment does not generate human bite feedback")
	contact_fixture(sim, 0)
	sim.step(0.025)
	var feedback: Dictionary = sim.private_for(1).bite_feedback
	check(feedback.active and feedback.count == 1 and feedback.side == "left" and feedback.id == 1, "actual attached contact yields one private directional event")
	sim.step(0.025)
	check(sim.private_for(1).bite_feedback.id == feedback.id, "steady bite cannot retrigger HUD event each snapshot")
	var angles: Vector2 = Pose.aim_angles(sim.actors[1], sim.actors[2].p)
	sim.action(1, 1, "attack", angles.x, NAN)
	sim.action(1, 1, "attack", INF, angles.y)
	check(sim.actors[1]._action_seq == -1, "invalid/partial aim pairs do not consume sequence or attack")
	sim.action(1, 1, "attack", angles.x, angles.y)
	sim.action(1, 1, "attack", 0.0, 0.0)
	sim.action(1, 0, "self_swat", 0.0, 0.0)
	sim.step(0.30)
	check(not sim.actors[2].alive and sim.private_for(1).attack.id == 1 and sim.private_for(1).attack.hit, "atomic click aim beats stale movement orientation and rejects duplicates/reordering")
	check(not sim.private_for(1).bite_feedback.active and sim.private_for(1).bite_feedback.id == 2, "contact end increments private event once")
	for actor: Dictionary in sim.public_snapshot().actors.values():
		check(not actor.has("assignment") and not actor.has("focus") and not actor.has("attack") and not actor.has("bite_feedback"), "public actor exposes physical gesture only")
		var strike: Dictionary = actor.strike
		check(not strike.has("zone") and not strike.has("target_id") and not strike.has("rotation_at"), "gesture cannot expose reservation or invisible target")
	var private_copy: Dictionary = sim.private_for(1)
	private_copy.attack.id = 999
	check(sim.private_for(1).attack.id == 1, "private feedback copy cannot mutate authority")

func _test_occlusion() -> void:
	var sim = make_sim(2, 1)
	sim.actors[1].p = Vector3(-7, 0, 8)
	sim.actors[2].p = Vector3(-7, 0, 8.9)
	sim.actors[3]._assignment = {"human": 1, "zone": Sim.FRONT_ZONE_COUNT, "revision": 1}
	sim.actors[3].state = "biting"
	sim._update_attached()
	var angles: Vector2 = Pose.aim_angles(sim.actors[1], sim.actors[3].p)
	sim.action(1, 1, "attack", angles.x, angles.y)
	sim.step(0.35)
	check(sim.actors[3].alive, "owner cannot hit cooperative rear through own body")
	angles = Pose.aim_angles(sim.actors[2], sim.actors[3].p)
	sim.action(2, 1, "attack", angles.x, angles.y)
	sim.step(0.35)
	check(not sim.actors[3].alive, "teammate rescues rear contact by actual exposed manual ray")
	var floor_sim = make_sim()
	floor_sim.actors[1].p = Vector3(-7, 3.2, 8)
	floor_sim.actors[1].tool = "broom"
	floor_sim.actors[2].p = Vector3(-7, 2.8, 7.6)
	angles = Pose.aim_angles(floor_sim.actors[1], floor_sim.actors[2].p)
	floor_sim.action(1, 1, "attack", angles.x, angles.y)
	floor_sim.step(0.35)
	check(floor_sim.actors[2].alive, "long manual tool cannot hit through floor slab")

func _test_moving_focus() -> void:
	for dt: float in [0.05, 1.0 / 60.0]:
		var sim = make_sim()
		var human: Dictionary = sim.actors[1]
		human.p = Vector3(-7, 0, 8)
		contact_fixture(sim, 0, 1.0)
		var mosquito: Dictionary = sim.actors[2]
		mosquito.state = "flying"
		var posed: Dictionary = sim._zone_pose(mosquito._assignment)
		mosquito.p = Vector3(posed.p) + Vector3(posed.normal) * 0.45
		for tick: int in range(int(4.0 / dt)):
			sim.submit_input(1, tick + 1, Vector3.RIGHT, 0.0, 0.0, false, false, true, false)
			posed = sim._zone_pose(mosquito._assignment)
			var direction: Vector3 = (Vector3(posed.p) - Vector3(mosquito.p)).normalized()
			sim.submit_input(2, tick + 1, Vector3.ZERO, atan2(-direction.x, -direction.z), asin(direction.y), true)
			sim.step(dt)
			if mosquito.state == "biting":
				break
		check(mosquito.state == "biting" and sim.elapsed < 2.5, "charged focus attaches to crouch-walking human at%.1fHz without feed-forward overshoot" % (1.0 / dt))

func _test_wall_gesture() -> void:
	var map: Dictionary = Sim.Maps.get_map("house")
	for tool: String in Sim.TOOL_STATS:
		for point: Vector3 in [Vector3(-float(map.half_x) + ArenaData.HUMAN_RADIUS, 0, 8), Vector3(-7, 0, float(map.half_z) - ArenaData.HUMAN_RADIUS)]:
			for yaw: float in [0.0, PI * 0.5, PI, -PI * 0.5]:
				var sim = make_sim()
				var human: Dictionary = sim.actors[1]
				human.merge({"p": point, "yaw": yaw, "body_yaw": yaw, "tool": tool}, true)
				sim.action(1, 1, "attack", yaw, -0.1)
				sim.step(0.025)
				for progress: float in [0.25, 0.5, 0.75]:
					human.strike.progress = progress
					for zone: int in [4, 5]:
						var posed: Dictionary = Pose.zone_pose(human, Sim.BODY_ZONES[zone])
						var contact: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * Sim.ATTACH_OFFSET
						check(absf(contact.x) + ArenaData.MOSQUITO_RADIUS <= float(map.half_x) + 0.001 and absf(contact.z) + ArenaData.MOSQUITO_RADIUS <= float(map.half_z) + 0.001, "gesture keeps attached forearm surface inside physical map walls")
