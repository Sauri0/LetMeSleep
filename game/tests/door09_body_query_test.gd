extends SceneTree
## Source-only CPU attribution and differential movement replay. No renderer.
const Current=preload("res://scripts/door_catalog.gd")
const Reference=preload("res://tests/door09_catalog_reference.gd")
const Trace=preload("res://tests/door09_query_trace.gd")
const Arena=preload("res://scripts/arena.gd")
const Sim=preload("res://scripts/simulation.gd")
const State=preload("res://scripts/door_state.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Generator=preload("res://scripts/procedural_house.gd")
var checks:=0
var failures: Array[String]=[]
var destination:=""
var baseline_only:=false
var details: Dictionary={}

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):destination=arg.trim_prefix("--report=")
		if arg=="--baseline-only":baseline_only=true
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	checks+=1
	if not condition:
		failures.append(label)
		if failures.size()<10:printerr("FAIL: "+label)

func _source_arena() -> String:
	var source:=FileAccess.get_file_as_string("res://scripts/arena.gd")
	var original:='const Doors = preload("res://scripts/door_catalog.gd")'
	check(source.count(original)==1,"one explicit Arena dependency substitution")
	return source.replace("class_name Arena\n","").replace("class_name Arena\r\n","").replace(original,'const Doors = preload("res://tests/door09_query_trace.gd")')

func _arena_script() -> GDScript:
	var script:=GDScript.new();script.source_code=_source_arena()
	var code:=script.reload()
	check(code==OK,"source-derived Arena parses")
	if code!=OK:return null
	return script

func _sim_script() -> GDScript:
	var source:=FileAccess.get_file_as_string("res://scripts/simulation.gd")
	var original:='const ArenaData = preload("res://scripts/arena.gd")'
	check(source.count(original)==1,"one explicit Simulation Arena dependency substitution")
	source=source.replace("class_name GameSimulation\n","").replace("class_name GameSimulation\r\n","")
	# A file-backed preload avoids Godot 4.5.2's null static-method resolution
	# for a newly compiled nested class. This is still exact source substitution,
	# stored only under the fixture-owned work directory, never production.
	var traced_path:=ProjectSettings.globalize_path("res://../work/perf09-traced-arena.gd")
	var file:=FileAccess.open(traced_path,FileAccess.WRITE)
	if file==null:check(false,"cannot write test-only Arena dependency");return null
	file.store_string(_source_arena());file.close()
	source=source.replace(original,"const ArenaData = preload("+JSON.stringify(traced_path)+")")
	var script:=GDScript.new();script.source_code=source
	var code:=script.reload()
	check(code==OK,"source-derived Simulation parses")
	if code!=OK:return null
	return script

func _capture_movement(arena_script: GDScript) -> void:
	var generated_id:=Generator.map_id(1)
	var sim:=Sim.new();var roster: Dictionary={}
	for id: int in range(1,17):roster[id]={"role":"human" if id<=4 else "mosquito"}
	sim.start(roster,{"map_id":generated_id,"mode":"blood","human_count":4,"round_seconds":180,"blood_goal":1000})
	check(sim.phase=="playing","capture starts validated map")
	var actors:=sim.actors.duplicate(true)
	Trace.queries.clear();Trace.calls=0;Trace.total_us=0;Trace.use_reference=true;Trace.capture=false
	# Warm both immutable Arena indexes before the captured interval.
	Arena.obstacles(generated_id);arena_script.obstacles(generated_id)
	var move_total:=0
	for tick: int in range(360):
		if tick%90==0:
			for id: String in sim.doors:sim.door_state.toggle(id,float(tick)/60.0)
		sim.door_state.step(1.0/60.0,actors)
		Trace.tick=tick;Trace.snapshot=sim.doors.duplicate(true);Trace.capture=true
		for id: int in range(5,17):
			var actor: Dictionary=actors[id]
			actor.yaw=float(id)*.53+float(tick)*.007
			actor.pitch=sin(float(tick)/80.0)*.9
			var expected:=actor.duplicate(true)
			Arena.step_mosquito(expected,Vector3.FORWARD,1.0/60.0,generated_id,null,sim.doors)
			var start:=Time.get_ticks_usec()
			arena_script.step_mosquito(actor,Vector3.FORWARD,1.0/60.0,generated_id,null,sim.doors)
			move_total+=Time.get_ticks_usec()-start
			check(actor==expected,"source-derived movement matches production tick%d actor%d"%[tick,id])
	Trace.capture=false
	details.capture={"ticks":360,"mosquitoes":12,"query_calls":Trace.calls,"body_blocked_ms":float(Trace.total_us)/1000.0,"movement_inclusive_ms":float(move_total)/1000.0,"fraction_instrumented":float(Trace.total_us)/maxi(1,move_total),"fingerprint":sim.config.map_fingerprint,"limit":"Nested body spans exclude trace append; movement includes trace overhead and static geometry. No body-only fraction is asserted for the earlier native profile."}

func _replay(label: String) -> Dictionary:
	var total:=0;var mismatches:=0;var per_tick: Dictionary={}
	for query: Dictionary in Trace.queries:
		var start:=Time.get_ticks_usec()
		var result: bool=Reference.body_blocked(query.body,query.states,query.map_id) if label=="reference" else Current.body_blocked(query.body,query.states,query.map_id)
		var duration:=Time.get_ticks_usec()-start
		total+=duration
		per_tick[query.tick]=int(per_tick.get(query.tick,0))+duration
		if result!=bool(query.result):mismatches+=1
	var samples: Array=[]
	for duration: int in per_tick.values():samples.append(float(duration)/1000.0)
	samples.sort()
	check(mismatches==0,label+" captured body queries identical")
	return {"implementation":label,"queries":Trace.queries.size(),"ticks":samples.size(),"mismatches":mismatches,"total_ms":float(total)/1000.0,"mean_per_tick_ms":float(total)/1000.0/samples.size(),"p90_per_tick_ms":samples[int(samples.size()*.9)],"p99_per_tick_ms":samples[int(samples.size()*.99)],"max_per_tick_ms":samples[-1]}

func _boundaries() -> void:
	var blocked:=0;var clear:=0
	for map_id: String in ["house",Generator.map_id(1),Generator.map_id(2)]:
		var definitions:=Current.get_doors(map_id)
		for id: String in definitions:
			var definition: Dictionary=definitions[id]
			for ai: int in range(7):
				var angle:=float(ai)*PI/12.0
				var states: Dictionary={id:{"angle":angle},"unknown-door":{"angle":0.0}}
				var transform:=Current.leaf_transform(definition,angle)
				for human: bool in [false,true]:
					var radius:=.6 if human else .04
					for offset: float in [-radius-.035-.000002,-radius-.035+.000002,0,radius+.035-.000002,radius+.035+.000002,4.0]:
						for y: float in [.04,1.0,3.2]:
							var p:=transform*Vector3(float(definition.width)*.65,y,offset)
							var body:=AABB(p+Vector3(-radius,0 if human else -radius,-radius),Vector3(radius*2,1.4 if human else radius*2,radius*2))
							var expected:=Reference.body_blocked(body,states,map_id)
							check(Current.body_blocked(body,states,map_id)==expected,"boundary %s %s angle%d human%s offset%.8f y%.2f"%[map_id,id,ai,human,offset,y])
							if expected:blocked+=1
							else:clear+=1
	check(blocked>0 and clear>0,"boundary samples include obstruction and free space")
	details.boundaries={"blocked":blocked,"clear":clear,"maps":3}

func _full_simulation(script: GDScript) -> void:
	var roster: Dictionary={}
	for id: int in range(1,17):roster[id]={"role":"human" if id<=4 else "mosquito"}
	var settings: Dictionary={"map_id":Generator.map_id(1),"mode":"blood","human_count":4,"round_seconds":180,"blood_goal":1000}
	var before: RefCounted=script.new();before.start(roster,settings)
	var after:=Sim.new();after.start(roster,settings)
	Trace.capture=false;Trace.use_reference=true
	for tick: int in range(120):
		for simulation: RefCounted in [before,after]:
			if tick%60==0:
				for door_id: String in simulation.doors:simulation.door_state.toggle(door_id,float(tick)/60.0)
			for id: int in roster:
				simulation.submit_input(id,tick+1,Vector3(sin(float(tick+id)*.02)*.4,0,-1),float(id)*.7,-.2,false,id%2==0,tick>60 and id<=4,tick==50 and id<=4)
		before.step(1.0/60.0);after.step(1.0/60.0)
		check(before.public_snapshot()==after.public_snapshot(),"full simulation public tick%d"%tick)
		if tick%30==0:
			for id: int in roster:check(before.private_for(id)==after.private_for(id),"full simulation private tick%d actor%d"%[tick,id])
	details.full_simulation={"ticks":120,"actors":16,"source_substitution":"Same Simulation source with preloaded Arena source under work/perf09-traced-arena.gd using reference body_blocked only. All other algorithms unchanged; original file hashes recorded."}

func _run() -> void:
	if not FileAccess.file_exists("res://scripts/arena.gd") or not FileAccess.file_exists("res://scripts/simulation.gd"):
		printerr("DOOR09_BODY requires source checkout for auditable dependency substitution");quit(2);return
	var arena_script:=_arena_script()
	if arena_script==null:quit(1);return
	_capture_movement(arena_script)
	var replay: Array=[]
	_replay("reference");_replay("current") # Same query corpus, both paths warm.
	for label: String in ["reference","current","current","reference"]:replay.append(_replay(label))
	if not baseline_only:
		_boundaries()
		var sim_script:=_sim_script()
		if sim_script==null:quit(1);return
		_full_simulation(sim_script)
	var hashes: Dictionary={}
	for path: String in ["res://scripts/door_catalog.gd","res://scripts/door_state.gd","res://scripts/door_geometry.gd","res://scripts/arena.gd","res://scripts/simulation.gd","res://tests/door09_catalog_reference.gd","res://tests/door09_query_trace.gd","res://tests/door09_body_query_test.gd"]:hashes[path]=FileAccess.get_sha256(path)
	var report: Dictionary={"checks":checks,"failures":failures,"baseline_only":baseline_only,"details":details,"replay_abba":replay,"source_sha256":hashes,"query_trace_sha256":JSON.stringify(Trace.queries).sha256_text(),"scope":"CPU query attribution only; no renderer/FPS inference. Captured real Arena calls with dependency interception, validated against unmodified Arena every step."}
	if not destination.is_empty():
		var file:=FileAccess.open(destination,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
	print("DOOR09_BODY checks=%d failures=%d capture=%s"%[checks,failures.size(),JSON.stringify(details.capture)])
	for row: Dictionary in replay:print("DOOR09_BODY_CPU ",JSON.stringify(row))
	Trace.queries.clear();Trace.snapshot.clear()
	quit.call_deferred(0 if failures.is_empty() else 1)
