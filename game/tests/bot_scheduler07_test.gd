extends SceneTree
## Verify the practice scheduler, then use real brains for door/navigation cases.
const Practice = preload("res://scripts/practice_session.gd")
const Sim = preload("res://scripts/simulation.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const DT := 1.0/60.0
var checks := 0
var failures := 0

class RecordingBrain extends RefCounted:
	var calls := 0
	var times: Array[float] = []
	var ages: Array[float] = []
	func decide(snapshot: Dictionary, _own: Dictionary, dt: float) -> Dictionary:
		calls += 1
		times.append(float(snapshot.elapsed))
		ages.append(dt)
		return {"move":Vector3.ZERO,"yaw":0.0,"pitch":0.0,"interact":false,"sprint":false,"crouch":false,"jump":true,"action":"attack" if calls==1 else ""}

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("FAIL: "+label)

func _initialize() -> void:
	_schedule()
	_late_tick()
	_path_invalidation()
	_real_routes()
	_mosquito_gap()
	print("BOT_SCHEDULER07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func _schedule() -> void:
	var session := Practice.new()
	session.start("human","blood",{})
	session.brains.clear()
	session.input_sequences.clear()
	session.action_sequences.clear()
	var roster := {1:{"role":"human"}}
	for id: int in range(2,17):
		roster[id]={"role":"human" if id<=4 else "mosquito"}
		session.brains[id]=RecordingBrain.new()
		session.input_sequences[id]=0
		session.action_sequences[id]=0
	session.sim.start(roster,{"mode":"blood","human_count":4,"round_seconds":180})
	var calls_each_tick: Array[int] = []
	for tick: int in range(120):
		var before := 0
		for brain: RefCounted in session.brains.values():before += brain.calls
		session.advance(DT)
		var after := 0
		for brain: RefCounted in session.brains.values():after += brain.calls
		calls_each_tick.append(after-before)
		if tick>2:
			check(after-before==5,"15 brains distribute five decisions per physics tick")
		if tick==2:
			check(session.sim.actors[2]._jump,"jump button retains its held-input semantics")
	for id: int in session.brains:
		var brain: RefCounted = session.brains[id]
		check(brain.calls in [40,41],"each brain keeps20Hz decision frequency")
		check(brain.times[0]<Practice.BOT_DECISION_SECONDS,"first decision latency below50ms")
		var maximum := 0.0
		for index: int in range(1,brain.times.size()):maximum=maxf(maximum,brain.times[index]-brain.times[index-1])
		check(maximum<=Practice.BOT_DECISION_SECONDS+.000001,"new public events observed within50ms")
		check(absf(brain.ages.reduce(func(a: float,b: float):return a+b,0.0)+session.bot_schedule[id].age-2.0)<.00001,"brain clocks retain actual elapsed time")
		check(session.action_sequences[id]==1,"single action never replays between decisions")
		check(session.input_sequences[id]>=119,"held input refreshed at60Hz")
	check(absf(session.sim.elapsed-2.0)<.00001,"authority advances every60Hz tick")
	check(session.sim._frame==120,"scheduler does not reduce collision/contact integration")
	check(session.sim.actors[2].grounded and is_zero_approx(float(session.sim.actors[2].p.y)),"held jump lands once without fabricating new presses between decisions")
	var replacement := RecordingBrain.new()
	session.brains[2]=replacement
	for frame: int in range(3):session.advance(DT)
	check(replacement.calls>0 and session.bot_schedule[2].brain==replacement,"replacement brain never inherits a stale intent")
	session.stop()
	check(session.bot_schedule.is_empty(),"stop clears retained decisions")
	session.free()

func _late_tick() -> void:
	var session := Practice.new()
	session.start("mosquito","blood",{})
	var brain := RecordingBrain.new()
	session.brains[101]=brain
	session.advance(.18)
	check(brain.calls==1,"late tick does not replay a burst of stale actions")
	check(session.bot_schedule[101].wait>0.0 and session.bot_schedule[101].wait<=Practice.BOT_DECISION_SECONDS,"late tick resumes bounded decision phase")
	check(is_equal_approx(brain.ages[0],.18),"late tick retains reaction time passed to brain")
	session.stop()
	session.free()

func _real_routes() -> void:
	var session := Practice.new()
	session.start("mosquito","sleep",{},"Tester",{"round_seconds":180})
	var sim: RefCounted = session.sim
	sim.actors[101]._next_task=1000.0
	for door: Dictionary in sim.doors.values():
		door.angle=0.0
		door.target_angle=0.0
	var all_clear := true
	for index: int in range(Maps.HOUSE.stations.size()):
		var station: Dictionary=Maps.HOUSE.stations[index]
		sim.actors[101]._task={"p":station.p,"name":station.name,"station":index,"remaining":30.0,"work":3.0,"progress":0.0}
		var before: int=sim.tasks_done
		var start: float=sim.elapsed
		for frame: int in range(30*60):
			var previous: Vector3=sim.actors[101].p
			session.advance(DT)
			all_clear=all_clear and ArenaData.can_fit_human(sim.actors[101].p,1.8,"house",sim.doors)
			all_clear=all_clear and ArenaData.clear_segment(previous+Vector3.UP,Vector3(sim.actors[101].p)+Vector3.UP,"house",sim.doors)
			if sim.tasks_done>before:break
		check(sim.tasks_done>before,"scheduled real human opens doors and completes station%d"%index)
		print("BOT_SCHEDULE_ROUTE station=%d seconds=%.3f"%[index,sim.elapsed-start])
	check(all_clear,"held human controls preserve wall/floor/door collision on every tick")
	check(session.brains[101].stats.doors>0,"route actually issues authoritative door actions")
	session.stop()
	session.free()

func _path_invalidation() -> void:
	var brain:=Brain.new()
	brain.setup(2)
	var from:=Vector3(-7,0,8)
	var goal:=Vector3(11,3.2,7)
	brain._path_direction(from,goal,true,"house")
	var baseline: int=brain.stats.paths
	for review: int in range(3):
		brain.path_age=2.0
		brain.path_review_age=2.0
		brain._path_direction(from,goal,true,"house")
	check(brain.stats.paths==baseline,"unchanged valid route avoids repeated graph search")
	goal.x+=.05
	brain.path_review_age=1.1
	brain._path_direction(from,goal,true,"house")
	check(brain.stats.paths==baseline+1 and brain.path_goal==goal,"small moving goal invalidates on regular review")
	goal.z-=2.0
	brain._path_direction(from,goal,true,"house")
	check(brain.stats.paths==baseline+2 and brain.path_goal==goal,"large goal change replans immediately")
	brain.path_age=.5
	brain.stuck_age=1.0
	brain._path_direction(from,goal,true,"house")
	check(brain.stats.paths==baseline+3,"stuck actor can replace an otherwise cached route")
	brain.stuck_age=0.0
	brain.path=PackedVector3Array([Vector3(-13.9,0,8)])
	brain.path_review_age=1.1
	brain._path_direction(from,goal,true,"house")
	check(brain.stats.paths==baseline+4,"invalid next segment forces another graph search")
	brain._path_direction(from+Vector3.UP,goal,false,"house")
	check(brain.stats.paths==baseline+5,"movement profile change invalidates the cached route")
	brain._path_direction(Vector3(0,1,0),Vector3(1,1,0),false,"lobby")
	check(brain.stats.paths==baseline+6,"map change invalidates the cached route")
	var insect:=Brain.new()
	insect.setup(5)
	var air_from:=Vector3(-7,1,8)
	var air_goal:=Vector3(-4,1,8)
	insect._path_direction(air_from,air_goal,false,"house")
	insect.door_passage=PackedVector3Array([air_from])
	insect._path_direction(air_from,air_goal,false,"house")
	check(insect.path_goal==Vector3.INF,"finishing a door passage explicitly invalidates the old route")
	var old_paths: int=insect.stats.paths
	insect._path_direction(air_from,air_goal,false,"house")
	check(insect.stats.paths==old_paths+1,"insect rebuilds its route after a completed passage")
	# The authority must keep reacting to a leaf closed after the route was
	# selected; caching the static graph must never cache dynamic door clearance.
	var session:=Practice.new()
	session.start("mosquito","sleep",{},"Tester",{"round_seconds":120})
	var sim: RefCounted=session.sim
	sim.actors[101].p=Vector3(-.7,0,-8.2)
	sim.actors[101]._next_task=1000.0
	sim.actors[101]._task={"p":Vector3(-4.8,0,-8.2),"name":"Fixture","station":0,"remaining":30.0,"work":3.0,"progress":0.0}
	for frame: int in range(3):session.advance(DT)
	check(not session.brains[101].path.is_empty(),"dynamic-close fixture has an active path")
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	var clear := true
	for frame: int in range(15*60):
		session.advance(DT)
		clear=clear and ArenaData.can_fit_human(sim.actors[101].p,1.8,"house",sim.doors)
		if sim.tasks_done>0:break
	check(clear,"door closed during cached route continues blocking every physical tick")
	check(sim.tasks_done==1 and session.brains[101].stats.doors>0,"cached-route human opens newly closed leaf and reaches its task")
	session.stop()
	session.free()

func _mosquito_gap() -> void:
	var session := Practice.new()
	session.start("human","blood",{},"Tester",{"round_seconds":120,"rotation_seconds":40})
	session.brains.erase(102)
	session.sim.start({1:{"role":"human"},101:{"role":"mosquito"}},{"mode":"blood","round_seconds":120,"rotation_seconds":40})
	var sim: RefCounted=session.sim
	sim.doors.kitchen.angle=0.0
	sim.doors.kitchen.target_angle=0.0
	sim.actors[1].p=Vector3(-4.8,0,-8.2)
	sim.actors[101].p=Vector3(-.8,1.2,-8.2)
	session.brains[101].age=10.0
	var low_seen := false
	var crossed := false
	var all_clear := true
	for frame: int in range(35*60):
		var previous: Vector3=sim.actors[101].p
		session.advance(DT)
		var p: Vector3=sim.actors[101].p
		low_seen=low_seen or (absf(p.x+2.14)<.30 and p.y<.105)
		crossed=crossed or p.x<-2.6
		all_clear=all_clear and ArenaData.clear_segment(previous,p,"house",sim.doors)
		if sim.actors[101].state=="biting":break
	check(low_seen and crossed,"scheduled mosquito crosses the visible door gap")
	check(all_clear,"retained flight input cannot pass through a door/wall")
	check(sim.actors[101].state=="biting","scheduled mosquito resumes held focus after passage")
	check(session.brains[101].stats.doors==0,"mosquito still cannot open doors")
	print("BOT_SCHEDULE_GAP seconds=%.3f"%sim.elapsed)
	session.stop()
	session.free()
