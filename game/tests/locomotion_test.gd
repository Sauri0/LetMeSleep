extends SceneTree

const Sim = preload("res://scripts/simulation.gd")
const Map = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_movement()
	_test_jump_and_furniture()
	_test_authority()
	_test_pose_geometry()
	_test_swings_and_exposed_marks()
	_test_biting_pose()
	print("LOCOMOTION_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + description)

func actor_at(point: Vector3 = Vector3.ZERO) -> Dictionary:
	return {"p": point, "yaw": 0.0, "pitch": 0.0, "velocity": Vector3.ZERO, "grounded": true}

func new_sim(humans: int = 1) -> RefCounted:
	var result = Sim.new()
	var players: Dictionary = {}
	for index: int in range(humans + 1):
		players[index + 1] = {"name": "Test", "role": "human" if index < humans else "mosquito"}
	result.start(players, {"human_count": humans, "blood_goal": 1000.0})
	return result

func _test_movement() -> void:
	var walk: Dictionary = actor_at()
	var run: Dictionary = actor_at()
	var crouch: Dictionary = actor_at()
	Map.step_human(walk, {"move": Vector3.FORWARD}, 0.5)
	Map.step_human(run, {"move": Vector3.FORWARD, "sprint": true}, 0.5)
	Map.step_human(crouch, {"move": Vector3.FORWARD, "sprint": true, "crouch": true}, 0.5)
	check(is_equal_approx(-float(walk.p.z), Map.HUMAN_SPEED * 0.5), "walking uses bounded server speed")
	check(is_equal_approx(-float(run.p.z), Map.HUMAN_RUN_SPEED * 0.5) and run.sprinting, "sprint changes speed and public stance")
	check(is_equal_approx(-float(crouch.p.z), Map.HUMAN_CROUCH_SPEED * 0.5) and crouch.crouching and not crouch.sprinting, "crouch overrides sprint")
	check(crouch.crouch_amount == 1.0 and Pose.sample(crouch).eye.y < Pose.sample(walk).eye.y - 0.5, "crouch settles and shared eye follows")
	var diagonal: Dictionary = actor_at()
	Map.step_human(diagonal, {"move": Vector3(500, 900, -500), "sprint": true}, 0.3)
	check(Vector3(diagonal.p).length() <= 1.50001 and diagonal.p.y == 0.0, "diagonal input normalized and human vertical intent ignored")
	var turned: Dictionary = actor_at()
	Map.step_human(turned, {"move": Vector3.FORWARD, "yaw": PI * 0.5}, 0.2)
	check(turned.p.x < -0.61 and absf(float(turned.p.z)) < 0.001, "local movement rotates with server yaw")
	Map.step_human(run, {}, 0.1)
	check(run.motion_speed == 0.0 and run.velocity == Vector3.ZERO and not run.sprinting, "empty intent stops walking immediately")
	# The stair-side post occupies the old z=0 start. Begin on the clear
	# landing so this measures wall collision from a valid body position.
	var wall: Dictionary = actor_at(Vector3(float(Map.HALF_X) - 1.0, 0, 4.8))
	check(Map.can_fit_human(wall.p), "wall sprint fixture begins clear of stairs and railing")
	Map.step_human(wall, {"move": Vector3.RIGHT, "sprint": true}, 1.0)
	check(wall.p.x <= float(Map.HALF_X) - Map.HUMAN_RADIUS + 0.00001 and Map.can_fit_human(wall.p), "sprint remains within wall collision envelope")
	var obstacle: Dictionary = actor_at(Vector3(-11.4, 0, 8.7))
	Map.step_human(obstacle, {"move": Vector3.BACK, "sprint": true}, 1.0)
	check(obstacle.p.z <= 8.8501 and Map.can_fit_human(obstacle.p), "sprint substeps cannot tunnel into sofa")
	var invalid: Dictionary = actor_at()
	Map.step_human(invalid, {"move": Vector3(NAN, 0, 0), "yaw": INF, "pitch": NAN, "jump": "yes", "sprint": 1, "crouch": 1}, 0.2)
	check(invalid.p == Vector3.ZERO and invalid.yaw == 0.0 and not invalid.crouching and not invalid.sprinting, "direct integrator rejects malformed motion and flags")
	Map.step_human(invalid, {"jump": true}, NAN)
	check(invalid.p == Vector3.ZERO, "nonfinite integration time ignored")

func _test_jump_and_furniture() -> void:
	var jumper: Dictionary = actor_at()
	Map.step_human(jumper, {"jump": true}, 0.1)
	check(jumper.p.y > 0.3 and not jumper.grounded and jumper.velocity.y > 0.0, "jump impulse lifts feet and clears grounded")
	var first_vertical: float = jumper.velocity.y
	Map.step_human(jumper, {"jump": false}, 0.025)
	Map.step_human(jumper, {"jump": true}, 0.025)
	check(jumper.velocity.y < first_vertical, "fresh jump press while airborne gives no extra impulse")
	var apex: float = jumper.p.y
	for tick: int in range(60):
		Map.step_human(jumper, {"jump": true}, 0.025)
		apex = maxf(apex, float(jumper.p.y))
	check(apex > 0.7 and apex < 0.86, "jump apex respects room ceiling and prototype impulse")
	check(jumper.grounded and jumper.p.y == 0.0 and jumper.velocity.y == 0.0, "gravity lands at floor while jump held without automatic repeat")
	Map.step_human(jumper, {"jump": false}, 0.025)
	Map.step_human(jumper, {"jump": true}, 0.025)
	check(jumper.p.y > 0.0, "release and second press permits another grounded jump")
	var platform: Dictionary = actor_at(Vector3(-6.5, 0.82, -1.15))
	platform.grounded = false
	Map.step_human(platform, {}, 0.5)
	check(is_equal_approx(float(platform.p.y), 0.62) and platform.grounded, "fall lands exactly on table surface")
	Map.step_human(platform, {"move": Vector3.RIGHT}, 0.7)
	Map.step_human(platform, {}, 0.6)
	check(platform.p.y == 0.0 and platform.grounded, "walking off furniture restores gravity and lands")
	var cupboard: Dictionary = actor_at(Vector3(-6.7, 1.45, 2.7))
	cupboard.crouch_amount = 1.0
	Map.step_human(cupboard, {}, 0.3)
	var height: float = lerpf(Map.HUMAN_HEIGHT, Map.HUMAN_CROUCH_HEIGHT, float(cupboard.crouch_amount))
	check(cupboard.crouching and Map.can_fit_human(cupboard.p, height) and is_equal_approx(float(cupboard.p.y), 1.45), "standing is prevented when furniture and ceiling leave only crouch space: p=%s height=%.4f crouch=%.4f" % [str(cupboard.p), height, cupboard.crouch_amount])
	Map.step_human(cupboard, {"jump": true, "crouch": true}, 0.2)
	height = lerpf(Map.HUMAN_HEIGHT, Map.HUMAN_CROUCH_HEIGHT, float(cupboard.crouch_amount))
	check(cupboard.p.y + height <= 3.00001 and cupboard.velocity.y <= 0.0, "crouched jump hits ceiling without penetration")

func _test_authority() -> void:
	var sim = new_sim()
	sim.actors[1].p = Vector3.ZERO
	sim.submit_input(1, 10, Vector3.FORWARD, 0.0, 0.0, false, true, false, true)
	sim.step(0.1)
	check(sim.actors[1].sprinting and sim.actors[1].p.y > 0.0, "simulation accepts optional locomotion flags")
	sim.submit_input(1, 9, Vector3.ZERO, 1.0, 0.0, false, false, true, false)
	sim.submit_input(1, 10, Vector3.ZERO, 1.0, 0.0, false, false, true, false)
	check(sim.actors[1]._sprint and not sim.actors[1]._crouch and sim.actors[1].yaw == 0.0, "reordered and repeated packets cannot replace valid locomotion flags")
	sim.submit_input(1, 11, Vector3(NAN, 0, 0), 0.0, 0.0, false, false, true, true)
	check(sim.actors[1]._input_seq == 10 and not sim.actors[1]._crouch, "nonfinite packet rejected atomically with locomotion flags")
	sim.step(1.0)
	var stopped: Vector3 = sim.actors[1].p
	sim.step(0.1)
	check(sim.actors[1].grounded and stopped.y == 0.0 and sim.actors[1].p == stopped, "stale input stops horizontal motion while gravity keeps integrating")
	sim.submit_input(2, 1, Vector3.ZERO, 0.0, 0.0, false, true, true, true)
	sim.step(0.05)
	check(not sim.actors[2]._sprint and not sim.actors[2]._crouch and not sim.actors[2]._jump, "human-only locomotion flags ignored for mosquito")
	var snapshot: Dictionary = sim.public_snapshot()
	check(snapshot.actors[1].has("velocity") and snapshot.actors[1].has("motion_phase") and snapshot.actors[1].has("crouch_amount"), "public snapshot includes deterministic rendering pose inputs")
	check(not snapshot.actors[1].has("_jump_held") and not snapshot.actors[1].has("_jump"), "private input bookkeeping remains off public snapshot")
	sim.start({1: {"role": "human"}, 2: {"role": "mosquito"}}, {})
	check(sim.actors[1].grounded and sim.actors[1].velocity == Vector3.ZERO and sim.actors[1].crouch_amount == 0.0, "new round clears previous movement state")
	var positions: Array[Vector3] = []
	for human_id: int in [1, 2]:
		var mosquito_id: int = 3 - human_id
		sim.start({human_id: {"role": "human"}, mosquito_id: {"role": "mosquito"}}, {})
		sim.actors[human_id].p = Vector3.ZERO
		sim.actors[mosquito_id].p = Vector3(0, 1.1, -0.4)
		sim.submit_input(human_id, 1, Vector3.FORWARD, 0.0, 0.0, false, true)
		sim.step(0.05)
		positions.append(sim.actors[mosquito_id].p)
	check(positions[0].is_equal_approx(positions[1]), "randomly assigned human peer ID cannot change moving body contact order")

func _test_pose_geometry() -> void:
	var neutral: Dictionary = actor_at()
	var idle: Dictionary = Pose.sample(neutral)
	var active: Dictionary = neutral.duplicate(true)
	active.motion_speed = 5.0
	active.motion_phase = PI * 0.5
	active.sprinting = true
	check(Pose.sample(active).knee_l != idle.knee_l and Pose.sample(active).hand_r != idle.hand_r, "movement phase animates actual leg and arm points")
	active.grounded = false
	check(Pose.sample(active).knee_l.z < idle.knee_l.z and Pose.sample(active).ankle_r.y > idle.ankle_r.y, "jump has a shared airborne leg pose")
	check(Pose.sample(active) == Pose.sample(active), "same snapshot always returns identical pose")
	# All contact points plus insect radius must remain within the human's
	# reserved horizontal envelope and vertical capsule across animation poses.
	for crouch: float in [0.0, 0.5, 1.0]:
		for grounded: bool in [true, false]:
			for swing: float in [0.0, 0.12, 0.8]:
				for phase: int in range(8):
					for pitch: float in [-1.48, 0.0, 1.48]:
						active.merge({"crouch_amount": crouch, "grounded": grounded, "swing": swing, "motion_phase": TAU * phase / 8.0, "pitch": pitch}, true)
						for zone: Dictionary in Sim.BODY_ZONES:
							var posed: Dictionary = Pose.zone_pose(active, zone)
							var point: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * Sim.ATTACH_OFFSET
							var height: float = lerpf(Map.HUMAN_HEIGHT, Map.HUMAN_CROUCH_HEIGHT, crouch)
							var inside: bool = Vector2(point.x, point.z).length() + Map.MOSQUITO_RADIUS <= Map.HUMAN_RADIUS + 0.00001 and point.y >= Map.MOSQUITO_RADIUS - 0.00001 and point.y + Map.MOSQUITO_RADIUS <= height + 0.00001
							check(inside and point.is_finite() and is_equal_approx(Vector3(posed.normal).length(), 1.0), "zone %s within collision envelope c=%.1f ground=%s swing=%.2f phase=%d pitch=%.2f point=%s" % [zone.label, crouch, str(grounded), swing, phase, pitch, str(point)])

func _test_biting_pose() -> void:
	for zone: int in range(Sim.FRONT_ZONE_COUNT):
		var sim = new_sim()
		sim.actors[1].p = Vector3.ZERO
		sim.actors[2]._assignment = {"human": 1, "zone": zone, "revision": 1}
		var posed: Dictionary = sim._zone_pose(sim.actors[2]._assignment)
		sim.actors[2].p = Vector3(posed.p) + Vector3(posed.normal) * 0.2
		sim.action(2, 1, "bite")
		for focus_tick: int in range(96):
			posed = sim._zone_pose(sim.actors[2]._assignment)
			var direction: Vector3 = (Vector3(posed.p) - Vector3(sim.actors[2].p)).normalized()
			sim.submit_input(2, focus_tick + 1, Vector3.ZERO, atan2(-direction.x, -direction.z), asin(direction.y), true)
			sim.step(0.025)
		check(sim.actors[2].state == "biting", "front zone %d attach before locomotion" % zone)
		var schedule: float = sim.actors[2]._next_rotation
		for tick: int in range(30):
			sim.submit_input(1, tick + 1, Vector3(0.2, 0, 0), 0.2, 0.0, false, true, tick >= 10, tick == 0)
			sim.step(0.025)
			var expected: Dictionary = sim.private_for(2).assignment
			check(Vector3(sim.actors[2].p).is_equal_approx(Vector3(expected.p) + Vector3(expected.normal) * Sim.ATTACH_OFFSET), "attached mosquito follows authoritative pose every frame zone %d tick %d" % [zone, tick])
		check(sim.actors[2].state == "biting" and sim.actors[2]._next_rotation == schedule and sim.blood > 0.0, "jump/crouch keep bite and schedule, blood persists zone %d" % zone)
		var angles: Vector2 = Pose.aim_angles(sim.actors[1], sim.actors[2].p)
		sim.submit_input(1, 100, Vector3.ZERO, angles.x, angles.y, false, false, true, false)
		sim.action(1, 1, "self_swat")
		sim.step(0.3)
		check(sim.actors[2].state == "stunned", "all solo zones remain defendable when crouched zone %d" % zone)
	var cooperative = new_sim(2)
	cooperative.actors[1].p = Vector3.ZERO
	cooperative.actors[1].crouch_amount = 1.0
	cooperative.actors[2].p = Vector3(0, 0, 0.85)
	cooperative.actors[3]._assignment = {"human": 1, "zone": Sim.FRONT_ZONE_COUNT, "revision": 1}
	cooperative.actors[3].state = "biting"
	cooperative.submit_input(1, 1, Vector3.ZERO, 0.0, -0.5, false, false, true, false)
	cooperative.step(0.025)
	cooperative.action(1, 1, "self_swat")
	cooperative.step(0.025)
	check(cooperative.actors[3].state == "biting", "crouching cannot self-swat cooperative rear zone")
	var angles: Vector2 = Pose.aim_angles(cooperative.actors[2], cooperative.actors[3].p)
	cooperative.submit_input(2, 1, Vector3.ZERO, angles.x, angles.y, false)
	cooperative.action(2, 1, "attack")
	cooperative.step(0.3)
	check(cooperative.actors[3].state == "stunned", "teammate can aim at crouched rear zone using shared eye/body pose")

func _test_swings_and_exposed_marks() -> void:
	var sim = new_sim()
	var human: Dictionary = sim.actors[1]
	human.p = Vector3.ZERO
	for tool: String in Sim.TOOL_STATS:
		check(is_equal_approx(float(Sim.TOOL_STATS[tool].cooldown), float(Pose.SWING_SECONDS[tool])), "pose cooldown matches authoritative %s swing" % tool)
		for crouch: float in [0.0, 0.5, 1.0]:
			for frame: int in range(9):
				human.merge({"tool": tool, "crouch_amount": crouch, "swing": float(Sim.TOOL_STATS[tool].cooldown) * frame / 8.0, "motion_phase": PI * 0.5, "motion_speed": 5.0, "sprinting": true}, true)
				for zone: Dictionary in Sim.BODY_ZONES:
					var posed: Dictionary = Pose.zone_pose(human, zone)
					var contact: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * Sim.ATTACH_OFFSET
					var outward: Vector3 = Vector3(posed.p) + Vector3(posed.normal) * 0.2
					check(not sim._body_occludes(outward, posed.p, -1), "zone %s exposes its contact surface during %s swing frame=%d crouch=%.1f" % [zone.label, tool, frame, crouch])
					check(Vector2(contact.x, contact.z).length() + Map.MOSQUITO_RADIUS <= Map.HUMAN_RADIUS + 0.00001, "animated %s %s contact stays inside reserved body envelope c=%.1f frame=%d p=%s" % [tool, zone.label, crouch, frame, str(contact)])
	var hands_idle: Dictionary = Pose.sample({"tool": "hands"})
	var point := Vector3(-0.12, 1.10, -0.30)
	var strike := {"tool": "hands", "hand": "right", "point": point, "progress": 0.5, "active": true}
	var hands_contact: Dictionary = Pose.sample({"tool": "hands", "strike": strike})
	# hand_r is the wrist joint. Verify the actual palm beyond it, independently
	# of the reported strike centre, while both joints of the other arm stay put.
	var palm_center: Vector3 = Vector3(hands_contact.hand_r) + (Vector3(hands_contact.hand_r) - Vector3(hands_contact.elbow_r)).normalized() * Pose.PALM_OFFSET
	check(palm_center.distance_to(point) < 0.0001 and Vector3(hands_contact.strike_contact).distance_to(palm_center) < 0.0001 and hands_contact.hand_l == hands_idle.hand_l and hands_contact.elbow_l == hands_idle.elbow_l, "manual palm reaches clicked point while other arm stays still")
	strike = {"tool": "swatter", "hand": "right", "point": Vector3(0, 1.2, -0.8), "progress": 0.5, "active": true}
	var tool_swing: Dictionary = Pose.sample({"tool": "swatter", "strike": strike})
	check(tool_swing.hand_l == hands_idle.hand_l and Vector3(tool_swing.strike_contact).distance_to(strike.point) < 0.0001, "equipped tool face reaches clicked point with left arm at rest")
