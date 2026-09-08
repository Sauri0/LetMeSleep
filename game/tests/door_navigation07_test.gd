extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Maps = preload("res://scripts/map_catalog.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_human_tasks()
	_open_leaf_tip()
	_mosquito_gap()
	print("DOOR_NAVIGATION07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("FAIL: "+label)

func feed(sim: RefCounted, brain: RefCounted, id: int, seq: int) -> void:
	var intent: Dictionary = brain.decide(sim.public_snapshot(),sim.private_for(id),.05)
	sim.submit_input(id,seq,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
	if not str(intent.action).is_empty():
		sim.action(id,seq,intent.action,intent.yaw,intent.pitch)
	sim.step(.05)

func _human_tasks() -> void:
	for close_doors: bool in [false,true]:
		var sim = Sim.new()
		sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {"mode":"sleep","round_seconds":180})
		sim.actors[1]._next_task = 1000.0
		if close_doors:
			for door: Dictionary in sim.doors.values():
				door.angle = 0.0
				door.target_angle = 0.0
		var brain = Brain.new()
		brain.setup(1)
		var seq := 0
		for index: int in range(Maps.HOUSE.stations.size()):
			var station: Dictionary = Maps.HOUSE.stations[index]
			sim.actors[1]._task = {"p":station.p,"name":station.name,"station":index,"remaining":30.0,"work":3.0,"progress":0.0}
			var previous: int = sim.tasks_done
			var started: float = sim.elapsed
			for frame: int in range(600):
				seq += 1
				feed(sim,brain,1,seq)
				if sim.tasks_done>previous:
					break
			print("DOOR_TASK_ROUTE closed=",close_doors," station=",index," seconds=",sim.elapsed-started," completed=",sim.tasks_done>previous," p=",sim.actors[1].p)
			check(sim.tasks_done>previous,"real bot reaches station%d with doors initially%s" % [index,"closed" if close_doors else "open"])
			if sim.tasks_done==previous:
				break
		check(sim.tasks_done==8,"all eight tasks across both storeys complete")

func _open_leaf_tip() -> void:
	var sim = Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {"mode":"sleep","round_seconds":120})
	sim.actors[1].p = Vector3(-.7,0,8.2)
	sim.actors[1]._next_task = 1000.0
	sim.actors[1]._task = {"p":Vector3(-5,0,7),"name":"Repelente","station":2,"remaining":24.0,"work":3.0,"progress":0.0}
	var brain = Brain.new()
	brain.setup(1)
	for frame: int in range(480):
		feed(sim,brain,1,frame)
		if sim.tasks_done>0:
			break
	print("DOOR_OPEN_TIP seconds=",sim.elapsed," p=",sim.actors[1].p)
	check(sim.tasks_done==1,"human route avoids the physical tip of an already-open leaf")

func _mosquito_gap() -> void:
	var sim = Sim.new()
	sim.start({1:{"role":"mosquito"},2:{"role":"human"}}, {"mode":"blood","round_seconds":120,"rotation_seconds":40})
	sim.doors.kitchen.angle = 0.0
	sim.doors.kitchen.target_angle = 0.0
	sim.actors[2].p = Vector3(-4.8,0,-8.2)
	sim.actors[1].p = Vector3(-.8,1.2,-8.2)
	var brain = Brain.new()
	brain.setup(1)
	brain.age = 10.0
	var low_seen := false
	var crossed := false
	for frame: int in range(700):
		feed(sim,brain,1,frame)
		var p: Vector3 = sim.actors[1].p
		low_seen = low_seen or (absf(p.x+2.14)<.30 and p.y<.105)
		crossed = crossed or p.x < -2.6
		if sim.actors[1].state=="biting":
			break
	print("DOOR_MOSQUITO_ROUTE seconds=",sim.elapsed," passages=",brain.stats.door_passages," p=",sim.actors[1].p," state=",sim.actors[1].state)
	check(brain.stats.doors==0 and sim.doors.kitchen.revision==0,"mosquito navigation never opens a door")
	check(low_seen and crossed,"real mosquito bot descends and crosses visible gap")
	check(sim.actors[1].state=="biting","mosquito resumes normal approach and held focus after door passage")
