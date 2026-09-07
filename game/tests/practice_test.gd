extends SceneTree
const Practice = preload("res://scripts/practice_session.gd")
const Brain = preload("res://scripts/bot_brain.gd")
var checks := 0
var failures := 0
func check(value: bool, label: String) -> void:
	checks += 1
	print("PRACTICE %s %s" % ["PASS" if value else "FAIL",label])
	if not value:
		failures += 1
func _initialize() -> void:
	_run.call_deferred()
func _run() -> void:
	var appearances := {"human":{"color":3,"accessory":1},"mosquito":{"color":5,"accessory":2}}
	for role: String in ["human","mosquito"]:
		for mode: String in ["blood","survival","sleep"]:
			var session := Practice.new()
			session.start(role,mode,appearances,"Prueba",{"round_seconds":60,"blood_goal":8,"task_goal":1})
			check(session.active and session.sim.actors[1].role == role,"selected POV " + role + "/" + mode)
			check(session.sim.config.mode == mode and session.sim.config.map_id == "house","practice arena/mode")
			check(session.sim.actors[1].appearance == appearances[role],"chosen appearance")
			for frame: int in range(1240):
				session.send_input(frame,Vector3.ZERO,0,0,false)
				session.advance(0.05)
				if session.sim.phase == "results":
					break
			var totals := {"moves":0,"bites":0,"detaches":0,"attacks":0,"self_swats":0,"task_inputs":0,"pickups":0,"paths":0}
			for brain: RefCounted in session.brains.values():
				for key: String in totals:
					totals[key] += brain.stats[key]
			check(session.sim.phase == "results","round terminates " + role + "/" + mode)
			check(int(totals.moves)>20,"bots move " + role + "/" + mode)
			if role == "human" and mode == "blood":
				check(int(totals.bites)>0 and float(session.sim.blood)>0,"mosquito bots reach own marks and extract blood")
				check(int(totals.detaches)>0,"mosquito bots detach explicitly")
			if role == "mosquito" and mode == "sleep":
				check(int(totals.task_inputs)>0 and int(session.sim.tasks_done)>0,"human bot navigates and completes tasks")
			print("PRACTICE_STATS " + JSON.stringify({"role":role,"mode":mode,"winner":session.sim.winner,"blood":session.sim.blood,"tasks":session.sim.tasks_done,"bots":totals}))
			session.restart()
			check(session.sim.phase == "playing" and session.sim.elapsed == 0 and session.sim.actors[1].role == role,"restart keeps chosen POV and resets round")
			session.stop()
			check(not session.active and session.sim == null and session.brains.is_empty(),"leave clears local simulation")
			session.free()
	# Attack is tested with a visible practice target. The human receives no
	# private insect data; all hits still go through Simulation.action.
	var session := Practice.new()
	session.start("mosquito","blood",appearances)
	session.sim.actors[101].p = Vector3(0,0,0)
	session.sim.actors[1].p = Vector3(0,1.5,-0.85)
	for frame: int in range(100):
		session.advance(0.05)
		if not bool(session.sim.actors[1].alive):
			break
	check(int(session.brains[101].stats.attacks)>0 and not bool(session.sim.actors[1].alive),"human bot reacts and hits visible target through rules")
	session.stop()
	session.free()
	# Tactile defense: the practice player is attached by the real bite action;
	# the bot gets only public position/contact feedback, never the assignment.
	session = Practice.new()
	session.start("mosquito","sleep",appearances)
	var assignment: Dictionary = session.sim.private_for(1).assignment
	session.sim.actors[1].p = Vector3(assignment.p)+Vector3(assignment.normal)*0.20
	session.send_action(1,"bite")
	session.sim.step(0.05)
	check(session.sim.actors[1].state == "biting","practice bite attaches under authoritative rules")
	for frame: int in range(60):
		session.advance(0.05)
		if not bool(session.sim.actors[1].alive):
			break
	check(int(session.brains[101].stats.self_swats)>0 and not bool(session.sim.actors[1].alive),"human bot reacts to a bite and defends the correct body band")
	session.stop()
	session.free()
	var brain := Brain.new()
	brain.setup(101)
	var observer := {101:{"role":"human","alive":true,"p":Vector3(4.5,0,3.9),"yaw":0.0,"pitch":0.0,"tool":"hands","bitten":false},1:{"role":"mosquito","alive":true,"p":Vector3(4.5,0.5,2.2),"state":"flying"}}
	for frame: int in range(80):
		brain.decide({"phase":"playing","actors":observer,"config":{"mode":"blood","map_id":"house"},"pickups":{}},{},0.05)
	check(int(brain.stats.attacks)==0,"human does not attack through cabinet")
	print("PRACTICE_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
