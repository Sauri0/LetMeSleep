extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const State = preload("res://scripts/door_state.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Brain = preload("res://scripts/bot_brain.gd")
const Audit = preload("res://tests/network_privacy_audit.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_geometry()
	_authority()
	_safe_sweep()
	_barriers()
	_interaction_and_perch()
	_bots()
	print("DOORS07_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("FAIL: "+label)

func make_sim(humans: int = 1, insects: int = 1, mode: String = "blood") -> RefCounted:
	var sim = Sim.new()
	var roster: Dictionary = {}
	for id: int in range(1,humans+insects+1):
		roster[id] = {"role":"human" if id<=humans else "mosquito"}
	sim.start(roster,{"mode":mode,"human_count":humans,"round_seconds":180,"blood_goal":1000,"rotation_seconds":40})
	return sim

func closed(sim: RefCounted, id: String = "kitchen") -> void:
	sim.doors[id].angle = 0.0
	sim.doors[id].target_angle = 0.0
	sim.doors[id].moving = false

func aim(sim: RefCounted, id: int, point: Vector3, held: bool = false) -> Vector2:
	var angles: Vector2
	if sim.actors[id].role=="human":
		angles = Pose.aim_angles(sim.actors[id],point)
	else:
		var direction: Vector3 = (point-Vector3(sim.actors[id].p)).normalized()
		angles = Vector2(atan2(-direction.x,-direction.z),asin(clampf(direction.y,-1,1)))
	sim.submit_input(id,int(sim.actors[id]._input_seq)+1,Vector3.ZERO,angles.x,angles.y,held)
	return angles

func advance(sim: RefCounted, seconds: float) -> void:
	for frame: int in range(int(ceil(seconds/.025))):
		sim.step(.025)

func _geometry() -> void:
	check(Doors.get_doors().size()==10 and Doors.get_doors("lobby").is_empty(),"ten house doors, none in separate lobby")
	var doors: Dictionary = Doors.get_doors()
	for id: String in doors:
		var definition: Dictionary = doors[id]
		check(is_equal_approx(Doors.leaf_box(definition).position.y,.14),id+" visible14cm ground gap")
		for step: int in range(19):
			var angle: float = float(step)*PI/36.0
			var blocked := false
			for obstacle: AABB in ArenaData.obstacles("house"):
				if Doors.intersects_body(definition,angle,obstacle):
					blocked = true
					printerr("GEOMETRY ",id," angle=",angle," obstacle=",obstacle)
					break
			check(not blocked,id+" leaf sweep clears static architecture at%ddegrees" % (step*5))
	var definition: Dictionary = doors.kitchen
	var transform: Transform3D = Doors.leaf_transform(definition,PI*.25)
	var center: Vector3 = transform*Doors.leaf_box(definition).get_center()
	var empty_corner: Vector3 = center+Vector3(.5,0,-.5)
	check(not Doors.intersects_body(definition,PI*.25,AABB(empty_corner-Vector3.ONE*.04,Vector3.ONE*.08)),"45degree leaf does not collide with empty enclosing-AABB corner")
	check(Doors.ray_leaf(definition,PI*.25,center+transform.basis.z,center-transform.basis.z).get("kind","")=="door","ray uses real rotated leaf")
	check(Doors.ray_leaf(definition,PI*.25,empty_corner+Vector3.UP*.3,empty_corner-Vector3.UP*.3).is_empty(),"ray remains clear through empty rotated-box corner")
	var original: float = float(Doors.DEFINITIONS.kitchen.width)
	doors.kitchen.width = 100
	check(float(Doors.DEFINITIONS.kitchen.width)==original,"caller cannot mutate immutable catalog through returned dictionary")

func _authority() -> void:
	var sim = make_sim(2)
	closed(sim)
	sim.actors[1].p = Vector3(-.7,0,-8.2)
	sim.actors[2].p = Vector3(-.7,0,-9.6)
	var point: Vector3 = Doors.handle_point(Doors.DEFINITIONS.kitchen,0)
	var angles := aim(sim,1,point)
	check(sim.private_for(1).interaction.get("door_id","")=="kitchen","private prompt selects aimed physical door")
	sim.action(1,1,"door",angles.x,angles.y)
	sim.action(1,1,"door",angles.x,angles.y)
	var second := aim(sim,2,point)
	sim.action(2,1,"door",second.x,second.y)
	sim.step(.025)
	check(int(sim.doors.kitchen.revision)==1 and float(sim.doors.kitchen.target_angle)>1,"simultaneous/duplicated presses cause one consistent toggle")
	sim.action(1,0,"door",angles.x,angles.y)
	sim.action(1,2,"door",angles.x,angles.y)
	sim.step(.025)
	check(int(sim.doors.kitchen.revision)==1,"old sequence and cooldown cannot reverse door")
	advance(sim,1.2)
	check(is_equal_approx(float(sim.doors.kitchen.angle),Doors.OPEN_ANGLE),"clear opening reaches exact open angle")
	var stranger = make_sim()
	closed(stranger)
	stranger.actors[2].p = Vector3(-.7,1.3,-8.2)
	var insect_aim := aim(stranger,2,point)
	stranger.action(2,1,"door",insect_aim.x,insect_aim.y)
	stranger.step(.025)
	check(stranger.doors.kitchen.revision==0,"mosquito cannot operate door")
	stranger.actors[1].p = Vector3(0,0,-8.2)
	stranger.action(1,1,"door",INF,0)
	stranger.step(.025)
	check(stranger.doors.kitchen.revision==0,"nonfinite action aim rejected")
	stranger.actors[1].p = Vector3(1,0,-8.2)
	var far := aim(stranger,1,point)
	stranger.action(1,2,"door",far.x,far.y)
	stranger.step(.025)
	check(stranger.doors.kitchen.revision==0,"out of range cannot operate door")
	stranger.actors[1].p = Vector3(-.7,0,-8.2)
	stranger.action(1,3,"door",-PI*.5,0)
	stranger.step(.025)
	check(stranger.doors.kitchen.revision==0,"looking away cannot operate nearby door")
	stranger.actors[1].p = Vector3(-.7,3.2,-8.2)
	var floor_aim := aim(stranger,1,point)
	stranger.action(1,4,"door",floor_aim.x,floor_aim.y)
	stranger.step(.025)
	check(stranger.doors.kitchen.revision==0,"other floor never reaches target through slab")
	var other = make_sim()
	check(float(other.doors.kitchen.angle)==Doors.OPEN_ANGLE and float(stranger.doors.kitchen.angle)==0,"two simulations retain independent door states")
	var snapshot: Dictionary = sim.public_snapshot()
	snapshot.tick = 7
	var private_packet: Dictionary = sim.private_for(1)
	private_packet.tick = 7
	var audit = Audit.new()
	audit.record_private(private_packet)
	audit.record_public(snapshot,1)
	check(audit.ok(),"full snapshot doors do not leak private reservations: "+str(audit.failures))
	check(not sim.private_for(3).has("interaction"),"door interaction feedback belongs only to human recipient")
	snapshot.doors.kitchen.angle = -42
	check(float(sim.doors.kitchen.angle)>=0,"snapshot cannot mutate server state")
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {})
	check(sim.doors.size()==10 and sim.doors.kitchen.revision==0 and sim.doors.kitchen.angle==Doors.OPEN_ANGLE,"rematch resets complete door state")

func _safe_sweep() -> void:
	var state = State.new()
	state.reset()
	var actor := {"role":"human","alive":true,"p":Vector3(-2.14,0,-8.2),"crouch_amount":0.0}
	var before: Vector3 = actor.p
	state.toggle("kitchen",0)
	var blocked_seen := false
	for frame: int in range(100):
		state.step(.025,{1:actor})
		blocked_seen = blocked_seen or bool(state.states.kitchen.blocked)
		check(actor.p==before,"closing sweep never moves occupant")
	check(blocked_seen and float(state.states.kitchen.target_angle)==Doors.OPEN_ANGLE,"occupied closing sweep reverses toward open")
	state.reset()
	state.states.kitchen.angle = 0.0
	state.states.kitchen.target_angle = 0.0
	var def: Dictionary = Doors.DEFINITIONS.kitchen
	actor.p = Doors.leaf_transform(def,.6)*Vector3(1.2,0,0)
	state.toggle("kitchen",0)
	for frame: int in range(70):
		state.step(.025,{1:actor})
	check(state.states.kitchen.blocked and float(state.states.kitchen.angle)<Doors.OPEN_ANGLE,"opening sweep stops before overlapping occupant")
	check(state.toggle("kitchen",2.0) and float(state.states.kitchen.target_angle)==0.0,"blocked opening allows explicit reversal instead of permanent lock")
	for frame: int in range(70):
		state.step(.025,{})
	check(is_equal_approx(float(state.states.kitchen.angle),0.0),"reversed clear leaf reaches closed position")
	state.reset()
	state.toggle("kitchen",0)
	var mosquito := {"role":"mosquito","alive":true,"state":"stunned","p":Vector3(-2.14,.04,-8.2)}
	for frame: int in range(60):
		state.step(.025,{1:mosquito})
	check(float(state.states.kitchen.angle)==0,"stunned insect safely below visible gap does not block or get crushed")
	check(mosquito.p==Vector3(-2.14,.04,-8.2),"under-gap stunned body is not displaced")

func _barriers() -> void:
	var sim = make_sim(1,2)
	closed(sim)
	var outside := Vector3(-.8,0,-8.2)
	var inside := Vector3(-3.5,0,-8.2)
	var stop: Vector3 = ArenaData.move_body(outside,inside-outside,true,"house",ArenaData.HUMAN_HEIGHT,sim.doors)
	check(stop.x>-1.51,"closed leaf blocks human movement including large requested displacement")
	var low_out: Vector3 = outside+Vector3.UP*.07
	var low_in: Vector3 = inside+Vector3.UP*.07
	check(ArenaData.move_body(low_out,low_in-low_out,false,"house",ArenaData.HUMAN_HEIGHT,sim.doors).distance_to(low_in)<.001,"mosquito physically crosses14cm gap with4cm radius")
	var high_out: Vector3 = outside+Vector3.UP*1.2
	var high_in: Vector3 = inside+Vector3.UP*1.2
	check(ArenaData.move_body(high_out,high_in-high_out,false,"house",ArenaData.HUMAN_HEIGHT,sim.doors).x>-2.07,"mosquito cannot tunnel through solid door leaf")
	check(not ArenaData.clear_segment(high_out,high_in,"house",sim.doors) and ArenaData.clear_segment(low_out,low_in,"house",sim.doors),"LOS is blocked by leaf but clear through visible gap")
	sim.actors[1].p = Vector3(-2.95,0,-8.2)
	sim.actors[2]._assignment = {"human":1,"zone":0,"revision":1}
	var posed: Dictionary = sim.private_for(2).assignment
	sim.actors[2].p = Vector3(posed.p)+Vector3(1.25,0,-.3)
	aim(sim,2,posed.p,true)
	check(not sim.private_for(2).focus.can_focus,"concentration cannot acquire through closed door")
	sim.actors[3].p = Vector3(-2.35,.55,-8.2)
	sim._kill(3)
	sim.actors[2].p = Vector3(-1.93,.55,-8.2)
	aim(sim,2,sim.actors[3].p,true)
	check(sim.private_for(2).help.state=="idle","help cannot cross door despite valid distance and aim")
	sim.actors[1].p = Vector3(-1.45,0,-8.2)
	sim.actors[2].p = Vector3(-2.40,1.2,-8.45)
	var attack_aim := aim(sim,1,sim.actors[2].p)
	sim.action(1,1,"attack",attack_aim.x,attack_aim.y)
	advance(sim,.3)
	check(sim.actors[1].strike.kind=="door" and not sim.private_for(1).attack.hit,"manual strike stops on visible door and cannot hit through it")
	sim.doors.kitchen.angle = Doors.OPEN_ANGLE
	sim.doors.kitchen.target_angle = Doors.OPEN_ANGLE
	check(ArenaData.move_body(outside,inside-outside,true,"house",ArenaData.HUMAN_HEIGHT,sim.doors).distance_to(inside)<.001,"fully opened door admits human across same portal")

func _interaction_and_perch() -> void:
	var sim = make_sim(1,1,"sleep")
	closed(sim)
	sim.actors[1].p = Vector3(-.7,0,-8.2)
	sim.actors[1]._next_task = 1000.0
	sim.actors[1]._task = {"p":Vector3(-.7,0,-7.9),"name":"Nearby work","station":0,"work":3.0,"progress":0.0,"remaining":30.0}
	aim(sim,1,Doors.handle_point(Doors.DEFINITIONS.kitchen,0.0),true)
	sim.step(.1)
	check(sim.private_for(1).interaction.get("kind","")=="door" and float(sim.private_for(1).task.progress)==0.0,"server prioritizes a valid aimed door over simultaneously held task E")
	sim.submit_input(1,2,Vector3.ZERO,-PI*.5,0,true)
	sim.step(.1)
	check(sim.private_for(1).interaction.is_empty() and float(sim.private_for(1).task.progress)>0.0,"looking away restores held work without a door priority at a distance")
	sim.actors[2].p = Vector3(-1.94,1.3,-8.2)
	sim.action(2,1,"perch")
	sim.step(.025)
	check(sim.actors[2].state=="flying","dynamic leaf is excluded as perch support")
	sim.actors[2].p = Vector3(-1.94,.26,-8.2)
	sim.action(2,2,"perch")
	sim.step(.4) # Continuous physical acquisition, not the previous one-tick snap.
	var floor_point: Vector3 = sim.actors[2].p
	check(sim.actors[2].state=="perched" and sim.public_snapshot().actors[2].surface_normal==Vector3.UP,"nearby fixed floor remains a valid perch")
	sim.door_state.toggle("kitchen",sim.elapsed)
	advance(sim,1.1)
	check(sim.actors[2].p==floor_point and float(sim.actors[2].p.y)<.05,"opening a nearby leaf cannot leave a perched insect floating")

func _bots() -> void:
	for reverse: bool in [false,true]:
		var sim = make_sim(1,1,"sleep")
		closed(sim)
		sim.actors[1].p = Vector3(-4.8,0,-8.2) if reverse else Vector3(-.7,0,-8.2)
		var destination := Vector3(-.7,0,-8.2) if reverse else Vector3(-5,0,-8.8)
		sim.actors[1]._task = {"p":destination,"work":3.0,"progress":0.0,"remaining":30.0,"station":0,"name":"Fixture"}
		var brain = Brain.new()
		brain.setup(1)
		for frame: int in range(1200):
			var intent: Dictionary = brain.decide(sim.public_snapshot(),sim.private_for(1),.025)
			sim.submit_input(1,frame, intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
			if not str(intent.action).is_empty():
				sim.action(1,frame,intent.action,intent.yaw,intent.pitch)
			sim.step(.025)
			if sim.tasks_done>0:
				break
		print("DOOR_BOT_METRIC reverse=",reverse," elapsed=",sim.elapsed," tasks=",sim.tasks_done," doors=",brain.stats.doors," p=",sim.actors[1].p," state=",sim.doors.kitchen)
		check(brain.stats.doors>0 and sim.tasks_done>0,"real human bot opens route door and completes task from "+("room" if reverse else "hall"))
		check(float(sim.elapsed)<24.0,"door traversal plus3second work fits existing minimum deadline")
