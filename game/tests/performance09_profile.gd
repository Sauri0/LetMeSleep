extends SceneTree
## Diagnostic instrumentation only. Production implementations run through super.
## No renderer, population, physics, AI cadence, or correctness threshold changes.
const Maps = preload("res://scripts/map_catalog.gd")
const Arena = preload("res://scripts/arena.gd")
const ViewInstrumentation = preload("res://tests/performance09_view_instrumentation.gd")
const Generator = preload("res://scripts/procedural_house.gd")

class Collector:
	extends RefCounted
	var enabled := false
	var stack: Array = []
	var labels: Dictionary = {}
	var physics_rows: Dictionary = {}
	var render_rows: Dictionary = {}
	var edges: Dictionary = {}
	var errors: Array[String] = []
	var path_rebuilds := 0
	var path_rebuild_us := 0
	var trace_paths := false
	var path_queries: Array = []
	var max_depth := 0
	var bookkeeping_us := 0
	var bookkeeping_calls := 0
	func record_path(from: Vector3, to: Vector3, human: bool, map_id: String, rebuilds: int) -> void:
		if not enabled or not trace_paths or rebuilds<=0:return
		if rebuilds!=1:errors.append("unexpected multiple path calls in one _path_direction")
		# Numeric triples round-trip the actual Vector3 values. Godot's default
		# JSON representation of a Vector3 is a display string with less precision.
		path_queries.append({"from":[from.x,from.y,from.z],"to":[to.x,to.y,to.z],"human":human,"map_id":map_id,"physics_frame":Engine.get_physics_frames(),"process_frame":Engine.get_process_frames()})
	func enter(label: String) -> bool:
		if not enabled: return false
		var overhead_start := Time.get_ticks_usec()
		stack.append([label,overhead_start,0,Engine.get_physics_frames(),Engine.get_process_frames(),Engine.is_in_physics_frame()])
		max_depth=maxi(max_depth,stack.size())
		bookkeeping_us+=Time.get_ticks_usec()-overhead_start
		bookkeeping_calls+=1
		return true
	func leave(label: String, tracked: bool, rebuilds: int=0) -> void:
		if not tracked: return
		var now := Time.get_ticks_usec()
		if stack.is_empty(): errors.append("empty stack for "+label);return
		var span: Array=stack.pop_back()
		if str(span[0])!=label: errors.append("unbalanced stack for "+label)
		var inclusive: int=now-int(span[1])
		var residual: int=inclusive-int(span[2])
		var parent := "outside_wrapped_scope"
		if not stack.is_empty():
			parent=str(stack[-1][0]);stack[-1][2]+=inclusive
		if not labels.has(label):
			labels[label]={"calls":0,"inclusive_us":0,"residual_us":0,"inclusive_samples":[],"residual_samples":[],"worst":[]}
		var row: Dictionary=labels[label]
		row.calls+=1;row.inclusive_us+=inclusive;row.residual_us+=residual
		row.inclusive_samples.append(float(inclusive)/1000.0)
		row.residual_samples.append(float(residual)/1000.0)
		if row.worst.size()<6 or inclusive>int(row.worst[-1].inclusive_us):
			row.worst.append({"inclusive_us":inclusive,"residual_us":residual,"physics_frame":span[3],"process_frame":span[4],"phase":"physics" if span[5] else "idle","parent":parent,"route_rebuilds":rebuilds})
			row.worst.sort_custom(func(a:Dictionary,b:Dictionary)->bool:return int(a.inclusive_us)>int(b.inclusive_us))
			if row.worst.size()>6:row.worst.pop_back()
		# Idle callbacks share the last physics counter; they must not be charged
		# to that tick merely because it was the most recent one.
		if span[5]:_accumulate(physics_rows,int(span[3]),label,inclusive,residual)
		_accumulate(render_rows,int(span[4]),label,inclusive,residual)
		var edge: String=parent+" -> "+label
		if not edges.has(edge):edges[edge]={"calls":0,"inclusive_us":0}
		edges[edge].calls+=1;edges[edge].inclusive_us+=inclusive
		if rebuilds>0:path_rebuilds+=rebuilds;path_rebuild_us+=inclusive
		bookkeeping_us+=Time.get_ticks_usec()-now
		bookkeeping_calls+=1
	func _accumulate(table: Dictionary, frame: int, label: String, inclusive: int, residual: int) -> void:
		if not table.has(frame):table[frame]={}
		if not table[frame].has(label):table[frame][label]={"calls":0,"inclusive_us":0,"residual_us":0}
		table[frame][label].calls+=1
		table[frame][label].inclusive_us+=inclusive
		table[frame][label].residual_us+=residual
	static func distribution(values: Array) -> Dictionary:
		if values.is_empty():return {"samples":0}
		var sorted:=values.duplicate();sorted.sort()
		var total:=0.0
		for value: float in sorted:total+=value
		return {"samples":sorted.size(),"mean":total/sorted.size(),"p50":sorted[int(sorted.size()*.5)],"p90":sorted[int(sorted.size()*.9)],"p99":sorted[mini(sorted.size()-1,int(sorted.size()*.99))],"max":sorted[-1]}
	func summary() -> Dictionary:
		var result: Dictionary={"labels":{},"edges":edges,"physics_rows":physics_rows,"process_rows":render_rows,"max_nesting":max_depth,"errors":errors,"route_rebuild_count":path_rebuilds,"path_direction_us_with_rebuild":path_rebuild_us,"recorder_bookkeeping_ms":float(bookkeeping_us)/1000.0,"recorder_bookkeeping_calls":bookkeeping_calls}
		for label: String in labels:
			var row: Dictionary=labels[label]
			result.labels[label]={"calls":row.calls,"inclusive_total_ms":float(row.inclusive_us)/1000.0,"residual_total_ms":float(row.residual_us)/1000.0,"inclusive_ms":distribution(row.inclusive_samples),"residual_ms":distribution(row.residual_samples),"worst":row.worst}
		if trace_paths:result.path_queries=path_queries
		return result

class ProfilePractice:
	extends "res://scripts/practice_session.gd"
	var collector: RefCounted
	func advance(dt: float) -> void:
		var tracked: bool=collector.enter("Practice.advance")
		super.advance(dt)
		collector.leave("Practice.advance",tracked)
	func _publish() -> void:
		var tracked: bool=collector.enter("Practice._publish")
		super._publish()
		collector.leave("Practice._publish",tracked)

class ProfileBrain:
	extends "res://scripts/bot_brain.gd"
	var collector: RefCounted
	func decide(snapshot: Dictionary, own_private: Dictionary, dt: float) -> Dictionary:
		var tracked: bool=collector.enter("Brain.decide")
		var result: Dictionary=super.decide(snapshot,own_private,dt)
		collector.leave("Brain.decide",tracked)
		return result
	func _path_direction(from: Vector3, destination: Vector3, human: bool, map_id: String) -> Vector3:
		var before: int=int(stats.paths)
		var tracked: bool=collector.enter("Brain._path_direction")
		var result: Vector3=super._path_direction(from,destination,human,map_id)
		collector.leave("Brain._path_direction",tracked,int(stats.paths)-before)
		collector.record_path(from,destination,human,map_id,int(stats.paths)-before)
		return result

class ProfileDoors:
	extends "res://scripts/door_state.gd"
	var collector: RefCounted
	func step(dt: float, actors: Dictionary) -> void:
		var tracked: bool=collector.enter("Doors.step")
		super.step(dt,actors)
		collector.leave("Doors.step",tracked)

class ProfileSimulation:
	extends "res://scripts/simulation.gd"
	var collector: RefCounted
	func step(dt: float) -> void:
		var tracked: bool=collector.enter("Simulation.step")
		super.step(dt)
		collector.leave("Simulation.step",tracked)
	func _tick(dt: float) -> void:
		var tracked: bool=collector.enter("Simulation._tick")
		super._tick(dt)
		collector.leave("Simulation._tick",tracked)
	func _move_free_insect(actor: Dictionary, local_move: Vector3, dt: float, assist: Variant=null) -> void:
		var tracked: bool=collector.enter("Simulation._move_free_insect")
		super._move_free_insect(actor,local_move,dt,assist)
		collector.leave("Simulation._move_free_insect",tracked)
	func _avoid_humans(previous: Vector3, position: Vector3) -> Vector3:
		var tracked: bool=collector.enter("Simulation._avoid_humans")
		var result: Vector3=super._avoid_humans(previous,position)
		collector.leave("Simulation._avoid_humans",tracked)
		return result
	func _focus_info(id: int) -> Dictionary:
		var tracked: bool=collector.enter("Simulation._focus_info")
		var result: Dictionary=super._focus_info(id)
		collector.leave("Simulation._focus_info",tracked)
		return result
	func _update_help(dt: float) -> void:
		var tracked: bool=collector.enter("Simulation._update_help")
		super._update_help(dt)
		collector.leave("Simulation._update_help",tracked)
	func _help_info(id: int) -> Dictionary:
		var tracked: bool=collector.enter("Simulation._help_info")
		var result: Dictionary=super._help_info(id)
		collector.leave("Simulation._help_info",tracked)
		return result
	func _update_attached() -> void:
		var tracked: bool=collector.enter("Simulation._update_attached")
		super._update_attached()
		collector.leave("Simulation._update_attached",tracked)
	func _update_emotes(dt: float) -> void:
		var tracked: bool=collector.enter("Simulation._update_emotes")
		super._update_emotes(dt)
		collector.leave("Simulation._update_emotes",tracked)
	func _update_bite_feedback() -> void:
		var tracked: bool=collector.enter("Simulation._update_bite_feedback")
		super._update_bite_feedback()
		collector.leave("Simulation._update_bite_feedback",tracked)
	func _serve_assignments() -> void:
		var tracked: bool=collector.enter("Simulation._serve_assignments")
		super._serve_assignments()
		collector.leave("Simulation._serve_assignments",tracked)
	func _execute_action(id: int, verb: String, command: Dictionary={}) -> void:
		var tracked: bool=collector.enter("Simulation._execute_action")
		super._execute_action(id,verb,command)
		collector.leave("Simulation._execute_action",tracked)
	func _update_throw(id: int) -> void:
		var tracked: bool=collector.enter("Simulation._update_throw")
		super._update_throw(id)
		collector.leave("Simulation._update_throw",tracked)
	func _update_projectiles(dt: float, previous_humans: Dictionary, previous_insects: Dictionary) -> void:
		var tracked: bool=collector.enter("Simulation._update_projectiles")
		super._update_projectiles(dt,previous_humans,previous_insects)
		collector.leave("Simulation._update_projectiles",tracked)
	func _resolve_strike(id: int, previous: Dictionary={}, previous_insects: Dictionary={}, from_time: float=-1.0) -> void:
		var tracked: bool=collector.enter("Simulation._resolve_strike")
		super._resolve_strike(id,previous,previous_insects,from_time)
		collector.leave("Simulation._resolve_strike",tracked)
	func _update_tasks(dt: float) -> void:
		var tracked: bool=collector.enter("Simulation._update_tasks")
		super._update_tasks(dt)
		collector.leave("Simulation._update_tasks",tracked)
	func _evaluate_result() -> void:
		var tracked: bool=collector.enter("Simulation._evaluate_result")
		super._evaluate_result()
		collector.leave("Simulation._evaluate_result",tracked)
	func _attack_info(id: int) -> Dictionary:
		var tracked: bool=collector.enter("Simulation._attack_info")
		var result: Dictionary=super._attack_info(id)
		collector.leave("Simulation._attack_info",tracked)
		return result
	func _door_info(id: int, aim_yaw: float=NAN, aim_pitch: float=NAN) -> Dictionary:
		var tracked: bool=collector.enter("Simulation._door_info")
		var result: Dictionary=super._door_info(id,aim_yaw,aim_pitch)
		collector.leave("Simulation._door_info",tracked)
		return result
	func _pickup_info(id: int) -> Dictionary:
		var tracked: bool=collector.enter("Simulation._pickup_info")
		var result: Dictionary=super._pickup_info(id)
		collector.leave("Simulation._pickup_info",tracked)
		return result
	func public_snapshot() -> Dictionary:
		var tracked: bool=collector.enter("Simulation.public_snapshot")
		var result: Dictionary=super.public_snapshot()
		collector.leave("Simulation.public_snapshot",tracked)
		return result
	func private_for(id: int) -> Dictionary:
		var tracked: bool=collector.enter("Simulation.private_for")
		var result: Dictionary=super.private_for(id)
		collector.leave("Simulation.private_for",tracked)
		return result
	func submit_input(id: int, seq: int, move: Vector3, yaw: float, pitch: float, interact: bool, sprint: bool=false, crouch: bool=false, jump: bool=false) -> void:
		var tracked: bool=collector.enter("Simulation.submit_input")
		super.submit_input(id,seq,move,yaw,pitch,interact,sprint,crouch,jump)
		collector.leave("Simulation.submit_input",tracked)
	func action(id: int, seq: int, verb: String, aim_yaw: float=NAN, aim_pitch: float=NAN) -> void:
		var tracked: bool=collector.enter("Simulation.action")
		super.action(id,seq,verb,aim_yaw,aim_pitch)
		collector.leave("Simulation.action",tracked)

var collector := Collector.new()
var map_id := "house"
var destination := ""
var seconds := 12.0
var concurrent_work := "not declared; runner must verify exclusivity"
var app: Node
var client: Node
var failures: Array[String] = []
var preparation: Dictionary = {}
var source_hashes: Dictionary = {}
var completed := false
var view_scopes := false
var check_view_scopes := false
var scripted_door_stress := true
var view_instrumentation: Dictionary = {}
var overhead_calibration: Dictionary = {}

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--profile-map="):map_id=argument.trim_prefix("--profile-map=")
		if argument.begins_with("--report="):destination=argument.trim_prefix("--report=")
		if argument.begins_with("--seconds="):seconds=clampf(float(argument.trim_prefix("--seconds=")),5,30)
		if argument.begins_with("--concurrent-work="):concurrent_work=argument.trim_prefix("--concurrent-work=")
		if argument=="--trace-paths":collector.trace_paths=true
		if argument=="--view-scopes":view_scopes=true
		if argument=="--check-view-scopes":view_scopes=true;check_view_scopes=true
		if argument=="--natural-doors":scripted_door_stress=false
	_run.call_deferred()

func _require(condition: bool, message: String) -> bool:
	if not condition:failures.append(message);printerr("PROFILE09_INVALID "+message)
	return condition

func _install_practice() -> void:
	var previous: Node=client.practice
	var old_name: String=previous.name
	previous.name="UnprofiledPractice"
	var replacement:=ProfilePractice.new();replacement.collector=collector;replacement.name=old_name
	for signal_name: String in ["snapshot_updated","private_updated"]:
		for connection: Dictionary in previous.get_signal_connection_list(signal_name):
			replacement.connect(signal_name,connection.callable,int(connection.flags))
	client.practice=replacement;client.add_child(replacement)
	previous.stop();previous.get_parent().remove_child(previous);previous.queue_free()

func _run() -> void:
	if check_view_scopes:
		view_instrumentation=ViewInstrumentation.prepare(collector)
		_require(not view_instrumentation.has("error"),str(view_instrumentation.get("error","generated wrappers compile")))
		await _finish({"kind":"source-only instrumentation compilation check"});return
	if not _require(DisplayServer.get_name()!="headless","requires native Main/Client rendering context"):
		await _finish({});return
	if not _require(map_id in ["house",Generator.map_id(1)],"only authored house and generated seed 1 are defined scenarios"):
		await _finish({});return
	create_timer(48).timeout.connect(func()->void:
		if not completed:failures.append("48 second fixture watchdog");_finish.call_deferred({}))
	for file_path: String in ["res://tests/performance09_profile.gd","res://tests/performance09_view_instrumentation.gd","res://tests/performance07_live.gd","res://scripts/practice_session.gd","res://scripts/bot_brain.gd","res://scripts/simulation.gd","res://scripts/arena.gd","res://scripts/door_state.gd","res://scripts/door_catalog.gd","res://scripts/map_navigation.gd","res://scripts/map_catalog.gd","res://scripts/main.gd","res://scripts/client.gd","res://scripts/world.gd","res://scripts/actor_view.gd","res://scripts/human_presentation.gd","res://scripts/human_pose.gd","res://scripts/mosquito_pose.gd","res://scripts/surface_presentation.gd","res://assets/art/characters/shared/character_skin.gd","res://assets/art/characters/human/human_lms06.glb","res://assets/art/characters/mosquito/mosquito_lms06.glb"]:
		source_hashes[file_path]=FileAccess.get_sha256(file_path) if FileAccess.file_exists(file_path) else "unavailable_in_pack"
	# Isolated bookkeeping calibration is reported separately, never subtracted
	# from production spans or described as a clean benchmark.
	var calibration:=Collector.new();calibration.enabled=true
	var calibration_start:=Time.get_ticks_usec()
	for index: int in range(1000):
		var tracked:=calibration.enter("calibration.noop")
		calibration.leave("calibration.noop",tracked)
	var calibration_us:=Time.get_ticks_usec()-calibration_start
	overhead_calibration={"pairs":1000,"elapsed_ms":float(calibration_us)/1000.0,"mean_us_per_pair":float(calibration_us)/1000.0,"bookkeeping_ms":float(calibration.bookkeeping_us)/1000.0,"scope":"synthetic single-depth collector calls; excludes wrapper dispatch and does not predict nested real cost"}
	root.size=Vector2i(1920,1080)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED);Engine.max_fps=0
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	var clock_start:=Time.get_ticks_usec()
	view_instrumentation=ViewInstrumentation.prepare(collector) if view_scopes else ViewInstrumentation.prepare_close_only()
	if not _require(not view_instrumentation.has("error"),str(view_instrumentation.get("error","view wrappers prepared"))):
		await _finish({});return
	preparation.view_instrumentation_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	clock_start=Time.get_ticks_usec()
	app=view_instrumentation.main.new()
	root.add_child(app)
	preparation.main_client_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	root.content_scale_size=Vector2i(1920,1080)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED);Engine.max_fps=0
	client=app.get_node("Client")
	_install_practice()
	# Current Client prepares a generated practice map. Fix that prelude seed so
	# all attribution runs load the same assets before the documented scenario.
	client.options["map-seed"]=1
	clock_start=Time.get_ticks_usec();client._start_practice("human","blood")
	preparation.practice_prelude_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	preparation.prelude_map=str(client.practice.sim.config.map_id) if client.practice.sim!=null else ""
	if not _require(client.practice.active,"production practice prelude starts"):
		await _finish({});return
	client.practice.brains.clear();client.practice.input_sequences.clear();client.practice.action_sequences.clear();client.practice.bot_schedule.clear()
	var roster: Dictionary={}
	for id: int in range(1,17):
		roster[id]={"name":"Stress%d"%id,"role":"human" if id<=4 else "mosquito"}
		if id!=1:
			var brain:=ProfileBrain.new();brain.collector=collector;brain.setup(id)
			client.practice.brains[id]=brain;client.practice.input_sequences[id]=0;client.practice.action_sequences[id]=0
	clock_start=Time.get_ticks_usec()
	ProfileBrain.prepare_navigation(map_id)
	preparation.navigation_prepare_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	var simulation:=ProfileSimulation.new();simulation.collector=collector
	client.practice.sim=simulation
	clock_start=Time.get_ticks_usec()
	simulation.start(roster,{"map_id":map_id,"human_count":4,"mode":"blood","round_seconds":180,"blood_goal":1000})
	preparation.simulation_start_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	if not _require(simulation.phase=="playing","final map starts: "+simulation.reason):
		await _finish({});return
	# Preserve the exact per-round door dictionaries; only decorate step().
	var profiled_doors:=ProfileDoors.new();profiled_doors.collector=collector
	profiled_doors.definitions=simulation.door_state.definitions
	profiled_doors.states=simulation.door_state.states
	profiled_doors.last_toggle=simulation.door_state.last_toggle
	simulation.door_state=profiled_doors
	if map_id=="house":simulation.actors[1].p=Vector3(0,0,6.5)
	# Generated case retains all production spawn positions, including actor 1.
	var start_position: Vector3=simulation.actors[1].p
	if not _require(Arena.can_fit_human(start_position,Arena.HUMAN_HEIGHT,map_id,simulation.doors),"local initial spawn clears static geometry and doors"):
		await _finish({});return
	client.practice.publication_age=0.0
	# Client loads a map only on starting. Reset that flag before this fixture's
	# roster restart, otherwise the old live fixture renders a different map.
	client.playing=false;client.camera_initialized=false
	clock_start=Time.get_ticks_usec();client.practice._publish()
	preparation.final_publish_world_ms=float(Time.get_ticks_usec()-clock_start)/1000.0
	if not _require(client.world.current_map==map_id and str(client.state.config.map_id)==map_id,"world, client and authority map identities match"):
		await _finish({});return
	client.yaw=0.0;client.pitch=-.06
	var frame_times: Array=[];var render_cpu: Array=[];var render_gpu: Array=[];var physics: Array=[];var draws: Array=[];var primitives: Array=[]
	var tick_histogram: Dictionary={};var frame_rows: Array=[]
	var last_toggle:=-1;var transitions:=0;var moving_frames:=0
	var started:=Time.get_ticks_usec();var previous:=started
	var previous_physics:=Engine.get_physics_frames()
	Input.action_press("move_forward")
	while float(Time.get_ticks_usec()-started)/1e6<seconds+2.0:
		await process_frame
		var elapsed:=float(Time.get_ticks_usec()-started)/1e6
		if not collector.enabled and elapsed>=2.0:collector.enabled=true
		client.yaw=0.0 if posmod(int(elapsed/4.0),2)==0 else PI
		if scripted_door_stress and int(elapsed/2.5)!=last_toggle:
			last_toggle=int(elapsed/2.5)
			for door_id: String in simulation.doors:
				if not simulation.doors[door_id].moving:
					if simulation.door_state.toggle(door_id,float(simulation.elapsed)):transitions+=1
		await RenderingServer.frame_post_draw
		var now:=Time.get_ticks_usec()
		var ticks: int=Engine.get_physics_frames()-previous_physics
		if elapsed>=2.0:
			frame_times.append(float(now-previous)/1000.0)
			render_cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()))
			render_gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
			physics.append(Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS)*1000.0)
			draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
			primitives.append(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
			tick_histogram[ticks]=int(tick_histogram.get(ticks,0))+1
			frame_rows.append({"process_frame":Engine.get_process_frames(),"physics_frame":Engine.get_physics_frames(),"physics_ticks":ticks,"elapsed":elapsed,"frame_ms":frame_times[-1]})
			for door: Dictionary in simulation.doors.values():
				if door.moving:moving_frames+=1;break
		previous=now;previous_physics=Engine.get_physics_frames()
	collector.enabled=false;Input.action_release("move_forward")
	_require(collector.stack.is_empty(),"all measured spans closed")
	_require(simulation.actors.size()==16 and client.practice.brains.size()==15,"population and live bot count preserved")
	_require(client.world.current_map==map_id,"rendered map remains the authoritative map")
	if view_scopes:
		for label: String in ["Client._process","Client._physics_process","Client._flush_game_hud","Client._snapshot","Client._private","World._process","World.sync_actors","World.sync_doors","World.sync_pickups","Actor.human.update_state","Actor.mosquito.update_state","Actor.human.pose","Actor.human.colliders"]:
			_require(collector.labels.has(label),"expected live instrumentation reached: "+label)
		# Actor voice callbacks are deliberately disabled by production when no
		# decoded voice is active; zero calls must not enable them artificially.
	var bot_stats: Dictionary={}
	for id: int in client.practice.brains:bot_stats[id]=client.practice.brains[id].stats.duplicate(true)
	var prefs: Script=load("res://scripts/preferences.gd")
	var result: Dictionary={"kind":"instrumented live diagnostic; not a clean FPS benchmark","scenario":"performance07_live hallway input and periodic doors; real Main/Client/Practice; 4 humans + 12 mosquitoes + 15 live brains","map_id":map_id,"map_fingerprint":simulation.config.map_fingerprint,"map_seed":simulation.config.map_seed,"world_map":client.world.current_map,"world_fingerprint":client.world.map_data.get("fingerprint",""),"actors":16,"bots":15,"warmup_seconds":2,"measurement_seconds":seconds,"simulation_elapsed":simulation.elapsed,"concurrent_work":concurrent_work,"frame_ms":Collector.distribution(frame_times),"render_cpu_ms":Collector.distribution(render_cpu),"render_gpu_ms":Collector.distribution(render_gpu),"physics_monitor_ms":Collector.distribution(physics),"draws":Collector.distribution(draws),"primitives":Collector.distribution(primitives),"physics_ticks_per_rendered_frame":tick_histogram,"frame_rows":frame_rows,"cpu_attribution":collector.summary(),"bot_stats":bot_stats,"preparation_ms":preparation,"start_position":start_position,"end_position":simulation.actors[1].p,"door_commands":transitions,"frames_with_moving_doors":moving_frames,"resolution":root.content_scale_size,"window_size":root.size,"render_texture_size":root.get_texture().get_size(),"video_quality":{"shadows":prefs.video_shadows,"reflections":prefs.video_reflections,"msaa_3d":root.msaa_3d},"occlusion_culling":root.use_occlusion_culling,"adapter":RenderingServer.get_video_adapter_name(),"cpu":OS.get_processor_name(),"physics_ticks_per_second":Engine.physics_ticks_per_second,"vsync":"disabled","fps_limit":0,"source_sha256":source_hashes}
	result.renderer=RenderingServer.get_current_rendering_method()
	result.rendering_driver=RenderingServer.get_current_rendering_driver_name()
	result.door_policy="all_doors_every_2_5_seconds" if scripted_door_stress else "ordinary_bot_and_player_interactions"
	result.scripted_door_commands=transitions
	if not scripted_door_stress:result.scenario="real Main/Client/Practice, valid map spawn, forward/reverse input, 4 humans + 12 mosquitoes + 15 live brains; no scripted global door toggles"
	await _finish(result)

func _finish(result: Dictionary) -> void:
	if completed:return
	completed=true;collector.enabled=false
	if InputMap.has_action("move_forward"):Input.action_release("move_forward")
	var ending_hashes: Dictionary={}
	for path: String in source_hashes:
		ending_hashes[path]=FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable_in_pack"
		_require(ending_hashes[path]==source_hashes[path],"source stayed frozen: "+path)
	result.source_sha256_after=ending_hashes
	result.view_instrumentation=view_instrumentation.get("manifest",{})
	for path: String in result.view_instrumentation.get("files_sha256",{}):
		_require(FileAccess.get_sha256(path)==result.view_instrumentation.files_sha256[path],"generated instrumentation stayed frozen: "+path)
	result.overhead_calibration=overhead_calibration
	result.failures=failures
	result["measurement_contract"]={"inclusive":"elapsed wall time around super; contains nested measured calls and synchronous callbacks","residual":"inclusive minus direct measured child spans; contains unwrapped work and profiler bookkeeping, not pure function self-time","do_not_sum":"inclusive labels overlap; use call edges and nesting","path":"Brain._path_direction inclusive, not isolated static MapNavigation.path; rebuilds counted by production stats.paths","tick_residual":"includes static Arena.step_human, static stunned movement, bookkeeping loops, unwrapped calls and profiler overhead; not a directly timed human-movement label","detail_level":3 if view_scopes else 2,"view_scopes":view_scopes,"physics_rows":"only scopes entered during Engine.is_in_physics_frame; idle work never charged to last physics tick","process_rows":"all callbacks keyed by Engine process frame, including physics callbacks before that frame's idle pass","overhead":"recorder_bookkeeping_ms measures enter/leave bodies once each, excluding wrapper dispatch and timer/accounting operations after its last timestamp; it is not subtracted; parent residuals include child bookkeeping","not_measured":"native renderer/physics/audio thread work, unwrapped UI or audio callbacks outside measured parents, and engine dispatch; this is not total frame CPU","actor_scope":"human pose includes skin and its measured authoritative colliders; mosquito pose/collider/skin remain inside mosquito update_state residual; inactive voice callbacks legitimately have zero calls","no_gameplay_changes":true,"preferences_saved_by_fixture":false,"source_only":true}
	if not destination.is_empty():
		var file:=FileAccess.open(destination,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(result,"\t"));file.close()
		else:failures.append("report cannot be written")
	print("PERFORMANCE09_PROFILE map=%s failures=%d"%[map_id,failures.size()])
	var exit_code := 0 if failures.is_empty() and collector.errors.is_empty() else 1
	if is_instance_valid(client):await client._leave()
	if view_instrumentation.has("recorder"):view_instrumentation.recorder.current=null
	if is_instance_valid(app):
		# Production shutdown preserves the diagnostic exit code and drains music.
		app.request_exit(exit_code)
	else:quit.call_deferred(exit_code)
