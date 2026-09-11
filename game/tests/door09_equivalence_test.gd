extends SceneTree
## Differential authority gate and bounded CPU replay; no rendering or prefs.
const Current = preload("res://scripts/door_state.gd")
const Reference = preload("res://tests/door09_reference.gd")
const Catalog = preload("res://scripts/door_catalog.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Sim = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Generator = preload("res://scripts/procedural_house.gd")
var checks := 0
var failures: Array[String] = []
var destination := ""
var replay_report: Array = []
var evidence: Dictionary = {}

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="): destination=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	checks+=1
	if not condition:
		failures.append(label)
		if failures.size()<12: printerr("FAIL: "+label)

func actor_at(index: int, position: Vector3, phase: float=0.0) -> Dictionary:
	var human := index<4
	var actor := {"role":"human" if human else "mosquito","alive":true,"p":position,"state":"human" if human else "flying","yaw":phase*.23,"body_yaw":phase*.23,"pitch":-.8,"crouch_amount":(sin(phase)+1.0)*.5 if human else 0.0,"motion_phase":phase,"motion_speed":3.0,"motion_blend":1.0,"motion_direction":Vector3.FORWARD,"grounded":true,"sprinting":false,"tool":"hands","strike":{}}
	if human and index%2==0:
		actor.tool="broom"
		actor.strike={"point":position+Vector3(.6,1.1,-1.0).rotated(Vector3.UP,float(actor.yaw)),"normal":Vector3.BACK,"hand":"right","tool":"broom","progress":.45,"active":true}
	if not human:
		actor.state=["flying","perched","biting","stunned"][index%4]
	return actor

func _predicate_cases() -> void:
	var before:=Reference.new()
	var after:=Current.new()
	var positive := 0
	var negative := 0
	var limb_only := 0
	# Normal offsets straddle the exact physical skin at touching boundaries.
	for facing: int in range(4):
		var definition: Dictionary=Catalog.DEFINITIONS.kitchen.duplicate(true)
		definition.closed_yaw=float(facing)*PI*.5+.17
		definition.open_sign=-1.0 if facing%2 else 1.0
		for angle_index: int in range(7):
			var angle := float(angle_index)*PI/12.0
			var transform := Catalog.leaf_transform(definition,angle)
			for role: int in range(2):
				var radius := .60 if role==0 else .04
				for normal_offset: float in [-2.0,-radius-.035-.000002,-radius-.035+.000002,0.0,radius+.035-.000002,radius+.035+.000002,2.0]:
					for height: float in [.04,1.0,3.5]:
						var actor := actor_at(1 if role==0 else 5,transform*Vector3(1.1,height,normal_offset),float(angle_index))
						actor.crouch_amount=0.0
						var occupants := {1:actor}
						var expected: bool=before._occupied(definition,angle,occupants)
						check(after._occupied(definition,angle,occupants)==expected,"predicate facing%d angle%d role%d normal%.8f height%.2f"%[facing,angle_index,role,normal_offset,height])
						if expected: positive+=1
						else: negative+=1
	# Human limbs beyond the travel box must survive the broad phase too.
	var definition: Dictionary=Catalog.DEFINITIONS.kitchen
	for phase: int in range(24):
		var angle := float(phase%7)*PI/12.0
		var transform := Catalog.leaf_transform(definition,angle)
		for offset: float in [.62,.72,.82,.92,1.02]:
			var actor := actor_at(0,transform*Vector3(1.1,0,offset),float(phase)*.3)
			actor.yaw=float(definition.closed_yaw)+angle
			actor.body_yaw=actor.yaw
			actor.strike.point=transform*Vector3(1.1,1.15,0)
			var expected: bool=before._occupied(definition,angle,{1:actor})
			var travel:=AABB(Vector3(actor.p)+Vector3(-.6,0,-.6),Vector3(1.2,lerpf(1.95,1.4,float(actor.crouch_amount)),1.2))
			if expected and not Catalog.intersects_body(definition,angle,travel):limb_only+=1
			check(after._occupied(definition,angle,{1:actor})==expected,"extended limb phase%d offset%.2f"%[phase,offset])
	check(positive>0 and negative>0,"predicate corpus contains both blocked and clear cases")
	check(limb_only>0,"predicate corpus exercises limbs outside travel box")
	evidence.predicates={"blocked":positive,"clear":negative,"limb_only":limb_only}

func _frames(map_id: String, count: int) -> Array:
	var definitions := Catalog.get_doors(map_id)
	var ids := definitions.keys()
	var frames: Array=[]
	for frame: int in range(count):
		var actors: Dictionary={}
		for index: int in range(16):
			var definition: Dictionary=definitions[ids[index%ids.size()]]
			var phase := float(frame)/60.0+float(index)*.71
			var local := Vector3(float(definition.width)*.5,.04 if index>=4 else 0.0,sin(phase*2.3)*2.8)
			if index>=4:local.y=.04 if index%4==3 else .8+sin(phase)*.4
			var position := Catalog.leaf_transform(definition,PI*.25)*local
			var actor := actor_at(index,position,phase)
			actor.alive=not (index==15 and frame%80<20)
			actors[index+1]=actor
		frames.append(actors)
	return frames

func _commands(state: RefCounted, frame: int) -> void:
	if frame%60!=0:return
	for id: String in state.states:
		state.toggle(id,float(frame)/60.0)
		state.toggle(id,float(frame)/60.0) # Same-frame duplicate still rejected.

func _differential(map_id: String, frames: Array) -> void:
	var before:=Reference.new();before.reset(map_id)
	var after:=Current.new();after.reset(map_id)
	var blocked := 0
	var reversing := 0
	for frame: int in range(frames.size()):
		_commands(before,frame);_commands(after,frame)
		var actors: Dictionary=frames[frame]
		var saved := actors.duplicate(true)
		var dt: float=[1.0/60.0,1.0/20.0,.1][frame%3]
		before.step(dt,actors)
		after.step(dt,actors)
		check(before.states==after.states and before.last_toggle==after.last_toggle,map_id+" exact door state at step%d"%frame)
		check(actors==saved,map_id+" no actor mutation at step%d"%frame)
		for state: Dictionary in after.states.values():
			if state.blocked:
				blocked+=1
				if float(state.target_angle)>float(state.angle):reversing+=1
	check(blocked>0 and reversing>0,map_id+" occupied sweeps include reversal or stopped opening")
	var saved:=after.states.duplicate(true)
	for invalid: float in [0.0,-1.0,NAN,INF]: after.step(invalid,frames[0])
	check(after.states==saved,map_id+" invalid dt leaves state intact")
	evidence[map_id]={"steps":frames.size(),"doors":after.states.size(),"blocked_samples":blocked,"open_target_when_blocked":reversing,"scenario_sha256":JSON.stringify(frames).sha256_text()}

func _safety_and_invalidation() -> void:
	var state:=Current.new();state.reset()
	var definition: Dictionary=Catalog.DEFINITIONS.kitchen
	var actor:=actor_at(1,Catalog.leaf_transform(definition,0)*Vector3(1.1,0,0))
	var position: Vector3=actor.p
	state.toggle("kitchen",0)
	var reversed:=false
	for frame: int in range(90):
		state.step(1.0/60.0,{1:actor})
		reversed=reversed or (bool(state.states.kitchen.blocked) and float(state.states.kitchen.target_angle)==Catalog.OPEN_ANGLE)
	check(reversed and actor.p==position,"closing occupant reopens without displacement")
	# Removal at the following call invalidates all per-step candidate data.
	state.states.kitchen.target_angle=0.0
	for frame: int in range(90):state.step(1.0/60.0,{})
	check(float(state.states.kitchen.angle)==0.0,"removed occupant is not retained across steps")
	state.reset();state.toggle("kitchen",0)
	var under:=actor_at(7,Catalog.leaf_transform(definition,0)*Vector3(1.1,.04,0))
	for frame: int in range(90):state.step(1.0/60.0,{1:under})
	check(float(state.states.kitchen.angle)==0.0 and is_equal_approx(float(under.p.y),.04),"stunned mosquito retains the physical14cm gap")
	var other:=Current.new();other.reset()
	var untouched:=other.snapshot()
	state.step(.05,{1:actor})
	check(other.snapshot()==untouched,"independent rooms share no occupant state")
	state.reset()
	check(state.snapshot()==untouched,"round reset clears door state")

func _same_simulation() -> void:
	var roster: Dictionary={}
	for id: int in range(1,17):roster[id]={"role":"human" if id<=4 else "mosquito"}
	var generated_id:=Generator.map_id(1)
	var settings: Dictionary={"map_id":generated_id,"mode":"blood","human_count":4,"round_seconds":180,"blood_goal":1000}
	var before:=Sim.new();before.start(roster,settings)
	var after:=Sim.new();after.start(roster,settings)
	var legacy:=Reference.new();legacy.reset(str(before.config.map_id))
	before.door_state=legacy;before.doors=legacy.states
	check(before.phase=="playing" and after.phase=="playing","full simulations start on validated generated map")
	for frame: int in range(120):
		_commands(before.door_state,frame);_commands(after.door_state,frame)
		for id: int in roster:
			var direction:=Vector3(sin(float(frame+id)*.02)*.4,0,-1)
			for simulation: RefCounted in [before,after]:
				simulation.submit_input(id,frame+1,direction,float(id)*.7,-.2,false,id%2==0,frame>60 and id<=4,frame==50 and id<=4)
		before.step(1.0/60.0);after.step(1.0/60.0)
		check(before.public_snapshot()==after.public_snapshot(),"same full simulation public tick%d"%frame)
		if frame%30==0:
			for id: int in roster:check(before.private_for(id)==after.private_for(id),"same full simulation private tick%d actor%d"%[frame,id])
	evidence.full_simulation={"map_id":generated_id,"ticks":120,"actors":16,"dt":1.0/60.0,"map_fingerprint":after.config.map_fingerprint}

func _timed_replay(script: Script, map_id: String, frames: Array, label: String) -> Dictionary:
	var state: RefCounted=script.new();state.reset(map_id)
	var samples: Array=[]
	var total := 0
	for frame: int in range(frames.size()):
		_commands(state,frame)
		var start := Time.get_ticks_usec()
		state.step(1.0/60.0,frames[frame])
		var duration := Time.get_ticks_usec()-start
		total+=duration;samples.append(float(duration)/1000.0)
	samples.sort()
	return {"implementation":label,"steps":samples.size(),"total_ms":float(total)/1000.0,"mean_ms":float(total)/1000.0/frames.size(),"p50_ms":samples[int(samples.size()*.5)],"p90_ms":samples[int(samples.size()*.9)],"p99_ms":samples[int(samples.size()*.99)],"max_ms":samples[-1],"final_state":state.snapshot()}

func _run() -> void:
	_predicate_cases()
	for map_id: String in ["house",Generator.map_id(1),Generator.map_id(2)]:
		var frames:=_frames(map_id,180)
		_differential(map_id,frames)
	_safety_and_invalidation()
	_same_simulation()
	var generated_id:=Generator.map_id(1)
	var frames:=_frames(generated_id,360)
	# Both paths and geometry are warm before symmetrical A/B/B/A wall timing.
	_timed_replay(Reference,generated_id,frames.slice(0,60),"warmup_reference")
	_timed_replay(Current,generated_id,frames.slice(0,60),"warmup_current")
	for label: String in ["reference","current","current","reference"]:
		replay_report.append(_timed_replay(Reference if label=="reference" else Current,generated_id,frames,label))
	var reference_final: Dictionary=replay_report[0].final_state
	for row: Dictionary in replay_report:
		check(row.final_state==reference_final,"ABBA identical final door state "+str(row.implementation))
		row.erase("final_state")
	var report: Dictionary={"checks":checks,"failures":failures,"evidence":evidence,"cpu_replay":replay_report,"cpu_contract":"Warm deterministic actor-pose replay; door step only, commands/setup outside spans; ABBA order; no FPS claim. External runner declares concurrency.","scenario_sha256":JSON.stringify(frames).sha256_text(),"source_sha256":{}}
	for path: String in ["res://scripts/door_state.gd","res://tests/door09_reference.gd","res://tests/door09_equivalence_test.gd","res://scripts/human_pose.gd","res://scripts/door_geometry.gd","res://scripts/door_catalog.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	if not destination.is_empty():
		var file:=FileAccess.open(destination,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:check(false,"cannot write report")
	print("DOOR09_EQUIVALENCE checks=%d failures=%d"%[checks,failures.size()])
	for row: Dictionary in replay_report:print("DOOR09_CPU ",JSON.stringify(row))
	quit.call_deferred(0 if failures.is_empty() else 1)
