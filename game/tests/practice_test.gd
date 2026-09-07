extends SceneTree

const Practice = preload("res://scripts/practice_session.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Sim = preload("res://scripts/simulation.gd")
const DT := 0.05
const APPEARANCES := {"human": {"color": 3, "accessory": 1, "face": 2, "hair": 1, "outfit": 2, "footwear": 0, "accent": 4}, "mosquito": {"color": 5, "accessory": 2, "face": 1, "hair": 2, "outfit": 1, "footwear": 0, "accent": 3}}
var checks := 0
var failures := 0

func check(value: bool, label: String) -> void:
	checks += 1
	print("PRACTICE %s %s" % ["PASS" if value else "FAIL", label])
	if not value:
		failures += 1

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	if not OS.get_cmdline_user_args().has("--contact-only"):
		for role: String in ["human", "mosquito"]:
			for mode: String in ["blood", "survival", "sleep"]:
				_test_complete_round(role, mode)
	_test_human_reaction()
	_test_contact_defense(1.0/60.0)
	_test_contact_defense(0.05)
	_test_bot_rescue()
	_test_obstructed_observation()
	print("PRACTICE_RESULT checks=%d failures=%d" % [checks, failures])
	quit(failures)

func _test_complete_round(role: String, mode: String) -> void:
	var session := Practice.new()
	# The fifth scheduled task reaches the upper storey. Let that actual calendar
	# run; no relocation, fabricated work or assignment injection in these rounds.
	var duration: float = 180.0 if role == "mosquito" and mode == "sleep" else 60.0
	session.start(role, mode, APPEARANCES, "Prueba", {"round_seconds": duration, "task_goal": 1})
	var label: String = role + "/" + mode
	check(session.active and session.sim.actors[1].role == role, "selected POV " + label)
	check(session.sim.config.mode == mode and session.sim.config.map_id == "house", "practice arena/mode " + label)
	check(session.sim.actors[1].appearance == APPEARANCES[role], "chosen appearance " + label)
	var observed := {"bites": 0, "detaches": 0, "first_bite": -1.0, "motion_valid": true, "motion_error": "", "task_floors": {}}
	for frame: int in range(int(duration / DT) + 4):
		var before: Dictionary = session.sim.public_snapshot().actors
		var task_before: Dictionary = session.sim.private_for(101).get("task", {})
		var tasks_before: int = session.sim.tasks_done
		session.send_input(frame, Vector3.ZERO, 0, 0, false)
		session.advance(DT)
		_observe_movement(session, before, observed)
		if int(session.sim.tasks_done) > tasks_before and not task_before.is_empty():
			observed.task_floors[1 if Vector3(task_before.p).y > 2.0 else 0] = true
		if session.sim.phase == "results":
			break
	var totals := {"moves": 0, "bites": 0, "detaches": 0, "attacks": 0, "self_swats": 0, "task_inputs": 0, "pickups": 0, "paths": 0}
	for brain: RefCounted in session.brains.values():
		for key: String in totals:
			totals[key] += brain.stats[key]
	check(session.sim.phase == "results" and session.sim.winner in ["human", "mosquito"], "round terminates with a rules result " + label)
	check(int(totals.moves) > 20, "bots move " + label)
	check(bool(observed.motion_valid), "bot trajectories respect walls/floors " + label + str(observed.motion_error))
	if role == "human" and mode == "blood":
		check(int(observed.bites) > 0 and float(session.sim.blood) > 0, "mosquito bots finish real concentration and extract shared blood")
		check(int(observed.detaches) > 0 and int(totals.detaches) > 0, "mosquito bot detach actions change attached state")
		check(float(observed.first_bite) > Sim.FOCUS_SECONDS, "separate spawns require navigation before first bite")
	if role == "mosquito" and mode == "sleep":
		check(int(totals.task_inputs) > 0 and int(session.sim.tasks_done) > 0, "human bot navigates and completes scheduled tasks")
		check(observed.task_floors.has(0) and observed.task_floors.has(1), "human bot completes tasks on both floors during one real round")
	print("PRACTICE_STATS " + JSON.stringify({"role": role, "mode": mode, "winner": session.sim.winner, "elapsed": session.sim.elapsed, "blood": session.sim.blood, "tasks": session.sim.tasks_done, "observed": observed, "bots": totals}))
	session.restart()
	check(session.sim.phase == "playing" and session.sim.elapsed == 0 and session.sim.actors[1].role == role, "restart keeps chosen POV and resets round " + label)
	check(session.sim.actors[1].appearance == APPEARANCES[role] and session.sim.blood == 0 and session.sim.tasks_done == 0, "restart preserves appearance and clears objective totals " + label)
	session.stop()
	check(not session.active and session.sim == null and session.brains.is_empty(), "leave clears local simulation " + label)
	session.free()

func _observe_movement(session: Node, before: Dictionary, observed: Dictionary) -> void:
	for id: int in session.brains:
		var old: Dictionary = before[id]
		var actor: Dictionary = session.sim.actors[id]
		if actor.role == "mosquito":
			if old.state != "biting" and actor.state == "biting":
				observed.bites += 1
				if float(observed.first_bite) < 0:
					observed.first_bite = session.sim.elapsed
			elif old.state == "biting" and actor.state == "flying":
				observed.detaches += 1
		# Respawns legitimately change location. All living continuous movement,
		# including following an attached body, must remain clear of map geometry.
		if bool(old.alive) and bool(actor.alive) and bool(observed.motion_valid):
			var offset: Vector3 = Vector3.UP * 0.9 if actor.role == "human" else Vector3.ZERO
			var from: Vector3 = Vector3(old.p) + offset
			var to: Vector3 = Vector3(actor.p) + offset
			if not to.is_finite() or not ArenaData.clear_segment(from, to, "house"):
				observed.motion_valid = false
				observed.motion_error = " id%d at%.2f %s -> %s" % [id, session.sim.elapsed, from, to]

func _test_human_reaction() -> void:
	# Observe a fixed, exposed target long enough to cover initial orientation
	# plus perception latency. Imperfect aim deliberately need not guarantee a hit.
	var brain := Brain.new()
	brain.setup(101)
	var actors := {101: {"role": "human", "alive": true, "p": Vector3(-7, 0, 8), "yaw": 0.0, "pitch": 0.0, "tool": "hands", "bitten": false}, 1: {"role": "mosquito", "alive": true, "p": Vector3(-7, 1.55, 7.5), "state": "flying"}}
	var first_attack := -1.0
	for frame: int in range(200):
		var intent: Dictionary = brain.decide({"phase": "playing", "actors": actors, "config": {"mode": "blood", "map_id": "house"}, "pickups": {}}, {}, DT)
		actors[101].yaw = intent.yaw
		actors[101].pitch = intent.pitch
		if intent.action == "attack" and first_attack < 0:
			first_attack = float(frame + 1) * DT
	check(first_attack >= 7.0 and first_attack <= 9.0, "human perceives exposed target then attacks after orientation/reaction latency")
	print("PRACTICE_REACTION first_attack=%.2f" % first_attack)

func _test_contact_defense(contact_dt: float) -> void:
	var session := Practice.new()
	session.start("mosquito", "sleep", APPEARANCES)
	var assignment: Dictionary = session.sim.private_for(1).assignment
	# Only setup location is adjusted; acquisition, motion, charge, contact,
	# defense, stun and recovery run through the production session.
	session.sim.actors[1].p = Vector3(assignment.p) + Vector3(assignment.normal) * 0.85
	session.send_action(1, "bite")
	session.advance(contact_dt)
	check(session.sim.actors[1].state != "biting", "practice bite action cannot bypass held concentration")
	var attached_at := -1.0
	var first_swat := -1.0
	var stunned_at := -1.0
	for frame: int in range(int(12.0/contact_dt)):
		var own: Dictionary = session.sim.private_for(1)
		if not Dictionary(own.assignment).is_empty():
			var delta: Vector3 = (Vector3(own.assignment.p) - Vector3(session.sim.actors[1].p)).normalized()
			session.send_input(frame, Vector3.ZERO, atan2(-delta.x, -delta.z), asin(delta.y), true)
		session.advance(contact_dt)
		if session.sim.actors[1].state == "biting" and attached_at < 0:
			attached_at = session.sim.elapsed
		if int(session.brains[101].stats.attacks) > 0 and first_swat < 0:
			first_swat = session.sim.elapsed
		if session.sim.actors[1].state == "stunned":
			stunned_at = session.sim.elapsed
			break
	check(attached_at >= Sim.FOCUS_SECONDS, "practice player attaches only after real held charge against moving bot")
	check(first_swat > attached_at + 3.3 and first_swat < attached_at + 4.3, "human bot responds to public bite feedback after tactile latency")
	check(stunned_at > first_swat and bool(session.sim.actors[1].alive), "actual defensive contact stuns a living mosquito in task mode")
	if stunned_at > 0:
		var before: Vector3 = session.sim.actors[1].p
		for frame: int in range(int(ceil(35.0 / contact_dt)) + 2):
			if session.sim.actors[1].state == "stunned":
				before = session.sim.actors[1].p
			session.advance(contact_dt)
		check(session.sim.actors[1].alive and session.sim.actors[1].state == "flying" and session.sim.phase == "playing", "unassisted mosquito recovers after35s without consuming lives or ending the round")
		check(Vector3(session.sim.actors[1].p).distance_to(before) < 0.08, "recovery stays at the last fallen location instead of respawn teleport")
	print("PRACTICE_CONTACT dt=%.4f attached=%.2f swat=%.2f stunned=%.2f" % [contact_dt, attached_at, first_swat, stunned_at])
	session.stop()
	session.free()

func _test_obstructed_observation() -> void:
	for obstruction: String in ["wall", "floor"]:
		var brain := Brain.new()
		brain.setup(101)
		var human_p: Vector3 = Vector3(-3, 0, 2.5) if obstruction == "wall" else Vector3(-7, 0, 8)
		var mosquito_p: Vector3 = Vector3(-1.3, 1.55, 2.5) if obstruction == "wall" else Vector3(-7, 3.35, 7.4)
		var actors := {101: {"role": "human", "alive": true, "p": human_p, "yaw": -PI / 2 if obstruction == "wall" else 0.0, "pitch": 0.0, "tool": "broom", "bitten": false}, 1: {"role": "mosquito", "alive": true, "p": mosquito_p, "state": "flying"}}
		var eye: Vector3 = Pose.view_origin(actors[101])
		check(eye.distance_to(mosquito_p) < float(Sim.TOOL_STATS.broom.reach) and not ArenaData.clear_segment(eye, mosquito_p), "occlusion fixture is in tool range behind " + obstruction)
		for frame: int in range(240):
			brain.decide({"phase": "playing", "actors": actors, "config": {"mode": "blood", "map_id": "house"}, "pickups": {}}, {}, DT)
		check(int(brain.stats.attacks) == 0, "human does not attempt aimed attacks through " + obstruction)

func _test_bot_rescue() -> void:
	var session := Practice.new()
	session.start("human", "blood", APPEARANCES)
	# Place one fallen ally and a nearby observer; the helper's decisions and
	# acceleration of recovery are entirely production BotBrain/Simulation.
	session.sim.actors[101].p = Vector3(-7,0.25,8)
	session.sim.actors[102].p = Vector3(-7.6,0.35,8)
	session.sim._kill(101)
	session.brains[101].age = 10.0
	session.brains[102].age = 10.0
	var recovered_at := -1.0
	for frame: int in range(12*60):
		session.send_input(frame,Vector3.ZERO,0,0,false)
		session.advance(1.0/60.0)
		if session.sim.actors[101].state == "flying":
			recovered_at = session.sim.elapsed
			break
	check(session.brains[102].stats.helps > 0,"mosquito bot chooses held help from a visible fallen teammate")
	check(recovered_at >= 8.75 and recovered_at < 12.0,"bot rescue accelerates real recovery without instantly reviving")
	check(session.sim.actors[101].alive and session.sim.phase == "playing","rescued practice rival remains in the same round")
	print("PRACTICE_RESCUE recovered=%.3f help_inputs=%d" % [recovered_at,session.brains[102].stats.helps])
	session.stop()
	session.free()
