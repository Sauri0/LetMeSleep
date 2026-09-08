extends SceneTree
const Sim = preload("res://scripts/simulation.gd")
const Catalog = preload("res://scripts/emote_catalog.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Audit = preload("res://tests/network_privacy_audit.gd")
var checks := 0
var failures := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures<=20: printerr("EMOTE09_AUTHORITY_FAIL "+label)

func _initialize() -> void:
	_catalog_and_timing()
	_eligibility()
	_cancellation()
	_shared_sequence()
	_focus_and_tasks()
	_round_and_privacy()
	print("EMOTE09_AUTHORITY_RESULT checks=%d failures=%d"%[checks,failures])
	quit(0 if failures==0 else 1)

func make_sim(mode: String="blood") -> RefCounted:
	var sim = Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {"mode":mode,"round_seconds":180,"blood_goal":1000})
	sim.actors[1].p=Vector3(-7,0,8)
	sim.actors[1]._next_task=1000.0
	sim.submit_input(1,1,Vector3.ZERO,0,0,false)
	return sim

func fresh_step(sim: RefCounted, dt: float) -> void:
	sim.submit_input(1,int(sim.actors[1]._input_seq)+1,Vector3.ZERO,sim.actors[1].yaw,sim.actors[1].pitch,false)
	sim.step(dt)

func _catalog_and_timing() -> void:
	check(Catalog.IDS.size()==4,"four shared gestures")
	for id: String in Catalog.IDS:
		for dt: float in [1.0/60.0,.05]:
			var sim = make_sim()
			var start: Vector3=sim.actors[1].p
			var duration: float=Catalog.get_emote(id).duration
			check(sim.request_emote(1,id),"idle human can request "+id)
			check(sim.public_snapshot().actors[1].emote_id==id and sim.actors[1].emote_time==0.0,"only authority starts gesture time "+id)
			var frames: int=int(round(duration/dt))
			for frame: int in range(frames):
				fresh_step(sim,dt)
				check(sim.actors[1].p==start,"gesture never moves root "+id)
				if frame<frames-1:
					check(sim.actors[1].emote_id==id and absf(float(sim.actors[1].emote_time)-float(frame+1)*dt)<.00001,"gesture advances by authority seconds "+id)
			check(sim.actors[1].emote_id=="" and sim.actors[1].emote_time==0.0,"catalog duration expires at both tick rates "+id)
			check(not sim.request_emote(1,id),"recovery is enforced after duration "+id)
			for recovery_tick: int in range(11): fresh_step(sim,.05)
			check(sim.request_emote(1,id),"new request accepted after bounded recovery "+id)

func _eligibility() -> void:
	var sim = make_sim()
	check(not sim.request_emote(999,"wave") and not sim.request_emote(2,"wave"),"unknown peer and mosquito cannot emote")
	check(not sim.request_emote(1,"invalid") and not sim.request_emote(1,"WAVE"),"strict catalog IDs")
	for reason: String in ["airborne","movement","jump","sprint","crouch","interact","task","attack","throw","stale","dead"]:
		var busy = make_sim()
		var actor: Dictionary=busy.actors[1]
		match reason:
			"airborne": actor.grounded=false
			"movement": actor.velocity=Vector3(.01,0,.02)
			"jump": actor._jump=true
			"sprint": actor._sprint=true
			"crouch": actor.crouch_amount=.5
			"interact": actor._interact=true
			"task": actor._task={"remaining":30}
			"attack": actor.swing=.1
			"throw": actor._throw={"state":"charging"}
			"stale": actor._last_input=-100
			"dead": actor.alive=false
		check(not busy.request_emote(1,"wave") and actor.emote_id=="","busy state rejects gesture: "+reason)
	sim.action(1,1,"attack",0,0)
	check(not sim.request_emote(1,"wave"),"queued manual action wins over a later emote request")
	for tool: String in ["swatter","racket","newspaper","broom","slipper"]:
		for emote: String in Catalog.IDS:
			var equipped=make_sim()
			equipped.actors[1].tool=tool
			var resting: Dictionary=Pose.sample(equipped.actors[1])
			check(equipped.request_emote(1,emote),"idle equipped human can gesture: "+tool+"/"+emote)
			for tick: int in range(12): fresh_step(equipped,.05)
			var gesturing: Dictionary=Pose.sample(equipped.actors[1])
			check(equipped.actors[1].emote_id==emote and gesturing.tool_grip==resting.tool_grip and gesturing.hand_r==resting.hand_r,"gesture preserves equipped grip and wrist: "+tool+"/"+emote)

func _shared_sequence() -> void:
	var sim=make_sim()
	check(not sim.submit_emote(1,-1,"wave") and not sim.submit_emote(2,1,"wave") and not sim.submit_emote(999,1,"wave"),"wrapper rejects negative sequence, mosquito and unknown sender")
	check(not sim.submit_emote(1,1,"invalid") and not sim.submit_emote(1,1,"x".repeat(24)) and sim.actors[1]._action_seq==-1,"invalid ID and oversized payload do not consume sequence")
	check(sim.submit_emote(1,2,"wave") and sim.actors[1]._action_seq==2,"valid gesture consumes shared action sequence")
	sim.action(1,2,"attack",0,0)
	check(sim.actors[1].emote_id=="wave" and sim._pending_actions.is_empty(),"duplicate manual action cannot interrupt or queue after gesture")
	check(not sim.submit_emote(1,1,"") and not sim.submit_emote(1,2,""),"reordered or duplicate cancel cannot interrupt gesture")
	check(not sim.submit_emote(1,3,"shrug") and sim.actors[1]._action_seq==3,"busy gesture consumes a fresh valid sequence without resetting time")
	check(sim.submit_emote(1,4,"") and sim.actors[1].emote_id=="","fresh cancel uses the same sequence")
	sim.action(1,5,"attack",0,0)
	check(not sim.submit_emote(1,5,"wave") and sim.actors[1]._action_seq==5,"gesture cannot reuse a manual action sequence")
	check(not sim.submit_emote(1,6,"wave") and sim.actors[1]._action_seq==6,"queued action and cooldown cannot be bypassed with a fresh gesture sequence")

func _cancellation() -> void:
	for input: String in ["move","jump","sprint","crouch","interact"]:
		var sim = make_sim()
		check(sim.request_emote(1,"wave"),"begin input cancellation "+input)
		sim.submit_input(1,2,Vector3.FORWARD if input=="move" else Vector3.ZERO,0,0,input=="interact",input=="sprint",input=="crouch",input=="jump")
		check(sim.actors[1].emote_id=="","input immediately cancels before simulation: "+input)
	for verb: String in ["attack","self_swat","pickup","drop","door","throw_start","throw_release"]:
		var sim = make_sim()
		check(sim.request_emote(1,"wave"),"begin action cancellation "+verb)
		sim.action(1,1,verb,0,0)
		check(sim.actors[1].emote_id=="","manual action immediately cancels: "+verb)
	var sim = make_sim()
	check(sim.request_emote(1,"celebrate"),"begin duplicate and cancel policy")
	sim.submit_input(1,1,Vector3.FORWARD,0,0,false)
	sim.submit_input(1,2,Vector3(NAN,0,0),0,0,false)
	check(sim.actors[1].emote_id=="celebrate","stale/nonfinite inputs cannot interrupt gesture state")
	check(not sim.request_emote(1,"celebrate") and not sim.request_emote(1,"yawn"),"repeat or different gesture cannot reset the active timer")
	check(sim.request_emote(1,""),"empty ID cancels for menu or focus loss")
	check(sim.actors[1].emote_id=="" and not sim.request_emote(1,"wave"),"cancellation does not erase the spam cooldown")
	var stale = make_sim()
	stale.request_emote(1,"wave")
	stale.step(.5)
	check(stale.actors[1].emote_id=="","lost fresh control cancels without a client cancellation packet")
	var equipment = make_sim()
	equipment.request_emote(1,"wave")
	equipment.actors[1].tool="newspaper"
	fresh_step(equipment,.025)
	check(equipment.actors[1].emote_id=="","equipment transition is revalidated on authority tick")

func _focus_and_tasks() -> void:
	var sim = make_sim()
	sim.actors[2]._assignment={"human":1,"zone":6,"revision":1}
	var point: Dictionary=sim._zone_pose(sim.actors[2]._assignment)
	sim.actors[2].p=Vector3(point.p)+Vector3(point.normal)*.7
	check(sim.request_emote(1,"wave"),"free private reservation alone does not block or disclose an emote")
	for tick: int in range(16):
		var direction: Vector3=(Vector3(sim.private_for(2).assignment.p)-Vector3(sim.actors[2].p)).normalized()
		sim.submit_input(2,tick+1,Vector3.ZERO,atan2(-direction.x,-direction.z),asin(clampf(direction.y,-1,1)),true)
		fresh_step(sim,1.0/60.0)
	check(float(sim.actors[2]._focus_progress)>.1 and sim.actors[1].emote_id=="","actual visible concentration cancels gesture to restore defense")
	var tasks = make_sim("sleep")
	tasks.actors[1]._next_task=.1
	check(tasks.request_emote(1,"shrug"),"idle human may gesture before task assignment")
	fresh_step(tasks,.15)
	check(not Dictionary(tasks.actors[1]._task).is_empty() and tasks.actors[1].emote_id=="","new hand task cancels gesture in the same dispatch tick")
	check(tasks.tasks_done==0 and tasks.actors[1]._failures==0 and tasks.actors[1]._deadline==30.0,"gesture does not award work or change individual penalty")

func _round_and_privacy() -> void:
	var sim = make_sim()
	sim.request_emote(1,"yawn")
	fresh_step(sim,.2)
	var snapshot: Dictionary=sim.public_snapshot(); snapshot.tick=sim._frame
	var personal: Dictionary=sim.private_for(1); personal.tick=sim._frame
	var audit = Audit.new(); audit.record_public(snapshot,1); audit.record_private(personal)
	check(audit.ok() and snapshot.actors[1].emote_id=="yawn" and snapshot.actors[1].emote_time>.1,"only visible ID and elapsed time are replicated")
	check(not snapshot.actors[1].has("_emote_until") and not snapshot.actors[1].has("_emote_cooldown"),"authority deadlines remain internal")
	var key: Array=Pose.cache_key(sim.actors[1])
	fresh_step(sim,.025)
	check(Pose.cache_key(sim.actors[1])!=key,"shared pose cache invalidates as gesture advances")
	sim._finish("human","fixture")
	check(sim.actors[1].emote_id=="" and not sim.request_emote(1,"wave"),"results end gesture and reject restart")
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {})
	check(sim.actors[1].emote_id=="" and sim.actors[1].emote_time==0.0 and sim.actors[1]._emote_cooldown==0.0,"round start clears all previous gesture state")
	sim.submit_input(1,1,Vector3.ZERO,0,0,false)
	check(sim.request_emote(1,"wave"),"new round can start a fresh valid gesture")
	sim.abort("fixture")
	check(sim.actors[1].emote_id=="" and not sim.request_emote(1,"wave"),"abort clears gesture and closes authority entry point")
