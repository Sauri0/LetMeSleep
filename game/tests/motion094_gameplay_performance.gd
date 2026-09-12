extends SceneTree
## Clean wall-frame probe: production Main/Client/Practice/World/HUD callbacks.
## No manual simulation ticks, callback wrappers, renderer substitutes or saves.
const SIZE := Vector2i(1920,1080)
const ACTIONS := ["move_forward","move_back","move_left","move_right","ascend","descend"]
const SOURCES := ["project.godot","scripts/main.gd","scripts/client.gd","scripts/world.gd","scripts/ui.gd","scripts/practice_session.gd","scripts/bot_brain.gd","scripts/simulation.gd","scripts/arena.gd","scripts/map_navigation.gd","scripts/navigation_geometry.gd","scripts/fixed_house.gd","scripts/map_catalog.gd","scripts/actor_view.gd","scripts/human_pose.gd","scripts/mosquito_pose.gd","scripts/video_settings.gd","scripts/house_details.gd","assets/art/house/alfa/manifest.json"]
var options: Dictionary = {}
var app: Node
var client: Node
var prefs: Script
var saved_preferences: Dictionary = {}
var source_hashes: Dictionary = {}
var failures: Array[String] = []
var report: Dictionary = {"kind":"real-gameplay-wall-frame","results":[],"failures":[]}
var destination := ""
var settings_hash := ""
var seconds := 20.0
var warmup := 12.0
var route := PackedVector3Array()
var route_index := 0
var input_clock := 0.0
var old_fps := 0
var old_mouse := Input.MOUSE_MODE_VISIBLE
var old_vsync := DisplayServer.VSYNC_DISABLED
var finished := false

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--"):
			var pair := argument.substr(2).split("=",true,1)
			options[pair[0]] = pair[1] if pair.size()>1 else "true"
	destination = str(options.get("report",""))
	seconds = clampf(float(options.get("seconds",20)),5,60)
	warmup = clampf(float(options.get("warmup",12)),10,30)
	_run.call_deferred()

static func distribution(values: Array[float]) -> Dictionary:
	if values.is_empty(): return {"samples":0,"available":false}
	var sorted := values.duplicate()
	sorted.sort()
	var total := 0.0
	var over := 0
	for value: float in sorted:
		total += value
		if value>1000.0/60.0: over += 1
	var middle := sorted.size()/2
	var median: float = sorted[middle] if sorted.size()%2 else (sorted[middle-1]+sorted[middle])*.5
	return {"samples":sorted.size(),"available":true,"median":median,"p95":sorted[clampi(ceili(sorted.size()*.95)-1,0,sorted.size()-1)],"mean":total/sorted.size(),"max":sorted[-1],"over_16_67_percent":100.0*over/sorted.size()}

func _require(ok: bool, label: String) -> bool:
	if not ok:
		failures.append(label)
		printerr("MOTION094_PERF_FAIL " + label)
	return ok

func _fingerprint(path: String) -> String:
	return FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "absent"

func _run() -> void:
	if options.has("validate-only"):
		_require(distribution([]).samples==0,"empty sample set does not invent FPS")
		var stats := distribution([4.0,1.0,3.0,2.0])
		_require(stats.median==2.5 and stats.p95==4.0 and stats.samples==4,"median and nearest-rank p95")
		_require(distribution([20.0]).over_16_67_percent==100.0,"slow frame classification")
		report.kind = "probe-validation-only-no-performance-measurement"
		_write_report()
		quit(0 if failures.is_empty() else 1)
		return
	if not _require(DisplayServer.get_name()!="headless","GPU run requires real renderer; headless cannot certify A11"):
		_write_report();quit(1);return
	if not _require(options.has("profile") and FileAccess.file_exists(str(options.profile)),"runner supplied temporary preferences profile"):
		_write_report();quit(1);return
	prefs = load("res://scripts/preferences.gd")
	# Preserve process-local preferences before any load; a custom profile skips
	# the production legacy migration and makes Client.load_settings a no-op.
	for property: Dictionary in prefs.get_script_property_list():
		var name: String = property.name
		if name in ["script","resource_local_to_scene","resource_path","resource_name"]: continue
		var value: Variant = prefs.get(name)
		if value is Dictionary or value is Array: value = value.duplicate(true)
		saved_preferences[name] = value
	settings_hash = _fingerprint(prefs.FILE_PATH)
	prefs.load_settings(str(options.profile))
	old_fps = Engine.max_fps
	old_mouse = Input.mouse_mode
	old_vsync = DisplayServer.window_get_vsync_mode()
	for path: String in SOURCES: source_hashes[path] = _fingerprint("res://"+path)
	report.source_sha256 = source_hashes.duplicate()
	report.source_git = str(options.get("source-git","unknown"))
	report.fixture_sha256 = _fingerprint(get_script().resource_path)
	report.hardware = {"gpu":RenderingServer.get_video_adapter_name(),"vendor":RenderingServer.get_video_adapter_vendor(),"cpu":OS.get_processor_name(),"logical_processors":OS.get_processor_count(),"os":OS.get_name(),"os_version":OS.get_version(),"memory":OS.get_memory_info()}
	report.renderer = {"method":RenderingServer.get_current_rendering_method(),"driver":RenderingServer.get_current_rendering_driver_name(),"godot":Engine.get_version_info().string}
	report.measurement = {"wall_interval":"consecutive frame_post_draw timestamps; no simulated dt or FPS monitor","percentile":"nearest-rank p95; midpoint median","warmup_seconds":warmup,"requested_sample_seconds":seconds,"physics_monitor":"last engine physics-process time; may repeat between ticks, not total frame CPU","render_gpu":"positive samples only; unavailable if driver reports zero","limits":"local GPU only; training population; excludes networking/WAN; no extrapolation to other hardware"}
	app = load("res://scripts/main.gd").new()
	root.add_child(app)
	client = app.get_node("Client")
	root.mode = Window.MODE_WINDOWED
	root.size = SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_VIEWPORT
	root.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	root.content_scale_size = SIZE
	Engine.max_fps = 0
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	# One POV per bounded process keeps cleanup and evidence independent.
	var selected_role := str(options.get("role","human"))
	if not _require(selected_role in ["human","mosquito"],"known requested POV"):
		await _finish()
		return
	for role: String in [selected_role]:
		await _scenario(role)
		if not failures.is_empty(): break
	await _finish()

func _scenario(role: String) -> void:
	var maps: Script = load("res://scripts/map_catalog.gd")
	var nav: Script = load("res://scripts/map_navigation.gd")
	var map_id: String = maps.default_map_id()
	var data: Dictionary = maps.get_map(map_id)
	client._start_practice(role,"sleep" if role=="human" else "blood",map_id)
	# Preserve normal roster, brains and scheduling. Extend only the round/goal
	# so a benchmark never silently turns into a cheap results-screen sample.
	client.playing = false
	client.practice.start(role,"sleep" if role=="human" else "blood",prefs.cosmetics,"Performance",{"map_id":map_id,"round_seconds":180,"blood_goal":1000})
	var sim: RefCounted = client.practice.sim
	if not _require(client.playing and sim.phase=="playing" and client.world.current_map==map_id,"real client/authority/world started " + role): return
	client.world.apply_video_settings()
	var origin: Vector3 = sim.actors[1].p
	var target := Vector3(0,0,15) if role=="human" else Vector3(0,1.2,15)
	route = nav.path(origin,target,role=="human",map_id)
	var back: PackedVector3Array = nav.path(target,origin,role=="human",map_id)
	route.append_array(back)
	if not _require(not route.is_empty() and not back.is_empty(),"scripted normal-input route reaches patio and returns " + role): return
	route_index = 0
	var frame_ms: Array[float] = []
	var physics_ms: Array[float] = []
	var render_cpu: Array[float] = []
	var render_gpu: Array[float] = []
	var ticks_histogram: Dictionary = {}
	var rows: Array = []
	var actor_min: int = sim.actors.size()
	var actor_max := actor_min
	var start_us := Time.get_ticks_usec()
	var previous_us := start_us
	var previous_tick := Engine.get_physics_frames()
	var previous_position := origin
	var travelled := 0.0
	var measured_travel := 0.0
	var maximum_camera_delta := 0.0
	var camera_start: Basis = client.camera.global_basis
	var measured_start := 0
	var invalid_frames := 0
	var exterior_frames := 0
	var last_input_us := start_us
	while float(Time.get_ticks_usec()-start_us)/1e6<warmup+seconds:
		await RenderingServer.frame_post_draw
		var now := Time.get_ticks_usec()
		var elapsed := float(now-start_us)/1e6
		var frame_ticks := Engine.get_physics_frames()-previous_tick
		var position: Vector3 = sim.actors[1].p
		var distance := position.distance_to(previous_position)
		travelled += distance
		if elapsed>=warmup:
			if measured_start==0: measured_start=previous_us
			var ms := float(now-previous_us)/1000.0
			frame_ms.append(ms)
			var physics := Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS)*1000.0
			physics_ms.append(physics)
			var cpu := RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid())
			var gpu := RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid())
			render_cpu.append(cpu)
			if gpu>0: render_gpu.append(gpu)
			ticks_histogram[frame_ticks] = int(ticks_histogram.get(frame_ticks,0))+1
			actor_min = mini(actor_min,sim.actors.size())
			actor_max = maxi(actor_max,sim.actors.size())
			measured_travel += distance
			if not data.building_bounds.has_point(position): exterior_frames += 1
			if not client.playing or sim.phase!="playing" or client.ui.is_menu_open(): invalid_frames += 1
			maximum_camera_delta = maxf(maximum_camera_delta,camera_start.get_rotation_quaternion().angle_to(client.camera.global_basis.get_rotation_quaternion()))
			rows.append({"frame":Engine.get_process_frames(),"wall_us":now,"frame_ms":ms,"physics_ticks":frame_ticks,"physics_monitor_ms":physics,"render_cpu_ms":cpu,"render_gpu_ms":gpu,"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)})
		previous_us=now;previous_tick=Engine.get_physics_frames();previous_position=position
		if now-last_input_us>=33333:
			_drive(role,position,elapsed,minf(float(now-last_input_us)/1e6,.1))
			last_input_us=now
	_release_inputs()
	var bots: Dictionary = {}
	var bot_moves := 0
	for id: int in client.practice.brains:
		bots[id] = client.practice.brains[id].stats.duplicate(true)
		bot_moves += int(bots[id].moves)
	_require(frame_ms.size()>=30 and invalid_frames==0,"only live gameplay frames measured " + role)
	_require(measured_travel>1.0 and maximum_camera_delta>.1,"body and camera moved during sample " + role)
	_require(bot_moves>0 and not bots.is_empty(),"production bots active " + role)
	_require(Vector2i(root.get_texture().get_size())==SIZE and Engine.max_fps==0 and DisplayServer.window_get_vsync_mode()==DisplayServer.VSYNC_DISABLED,"1080p uncapped render contract " + role)
	var item := {"role":role,"mode":sim.config.mode,"map_id":map_id,"map_fingerprint":sim.config.map_fingerprint,"world_fingerprint":client.world.map_data.get("fingerprint",""),"actors_min":actor_min,"actors_max":actor_max,"bots":bots,"wall_frame_ms":distribution(frame_ms),"physics_monitor_ms":distribution(physics_ms),"render_cpu_ms":distribution(render_cpu),"render_gpu_ms":distribution(render_gpu),"measured_wall_seconds":float(previous_us-measured_start)/1e6,"actual_warmup_seconds":float(measured_start-start_us)/1e6,"simulation_elapsed":sim.elapsed,"physics_ticks_per_rendered_frame":ticks_histogram,"body_travel_m":travelled,"measured_body_travel_m":measured_travel,"maximum_camera_angle_rad":maximum_camera_delta,"patio_frames":exterior_frames,"invalid_gameplay_frames":invalid_frames,"resolution":[root.get_texture().get_width(),root.get_texture().get_height()],"window_size":[root.size.x,root.size.y],"quality":{"shadows":prefs.video_shadows,"reflections":prefs.video_reflections,"msaa_3d":root.msaa_3d,"shadow_atlas":root.positional_shadow_atlas_size,"vsync":false,"fps_cap":Engine.max_fps},"physics_hz":Engine.physics_ticks_per_second,"raw_frames":rows}
	report.results.append(item)
	print("MOTION094_GAMEPLAY role=%s median=%.3fms p95=%.3fms actors=%d bots=%d" % [role,item.wall_frame_ms.get("median",0),item.wall_frame_ms.get("p95",0),actor_max,bots.size()])
	_write_report()

func _drive(role: String, position: Vector3, elapsed: float, dt: float) -> void:
	var next: Vector3 = route[route_index]
	if position.distance_to(next)<.24:
		route_index = mini(route_index+1,route.size()-1)
		next = route[route_index]
	var delta := next-position
	if role=="human": delta.y=0
	var direction := delta.normalized()
	var target_yaw := atan2(-direction.x,-direction.z)
	var target_pitch := .12*sin(elapsed*.8)
	var sensitivity: float = prefs.human_sensitivity if role=="human" else prefs.mosquito_sensitivity
	var mouse := InputEventMouseMotion.new()
	mouse.relative = Vector2(-clampf(wrapf(target_yaw-float(client.yaw),-PI,PI),-dt*2,dt*2)/sensitivity,-(target_pitch-float(client.pitch))/sensitivity)
	client._unhandled_input(mouse)
	var local := direction.rotated(Vector3.UP,-float(client.yaw))*.8
	_set_axis("move_left","move_right",local.x)
	_set_axis("move_forward","move_back",local.z)
	_set_axis("descend","ascend",local.y if role=="mosquito" else 0.0)

func _set_axis(negative: String, positive: String, value: float) -> void:
	Input.action_release(negative);Input.action_release(positive)
	if value<-.01: Input.action_press(negative,-value)
	elif value>.01: Input.action_press(positive,value)

func _release_inputs() -> void:
	for action: String in ACTIONS:
		if InputMap.has_action(action): Input.action_release(action)

func _write_report() -> void:
	report.failures = failures.duplicate()
	if not destination.is_empty():
		var file := FileAccess.open(destination,FileAccess.WRITE)
		if file!=null: file.store_string(JSON.stringify(report,"\t"));file.close()
		else: _require(false,"write performance report")

func _finish() -> void:
	if finished: return
	finished=true
	_release_inputs()
	if is_instance_valid(client): await client._leave()
	for path: String in source_hashes:
		_require(_fingerprint("res://"+path)==source_hashes[path],"source remained unchanged " + path)
	_require(_fingerprint(prefs.FILE_PATH)==settings_hash,"user preferences file unchanged")
	report.preferences = {"temporary_profile":true,"saved":false,"user_file_unchanged":_fingerprint(prefs.FILE_PATH)==settings_hash,"restored_in_memory":true}
	for name: String in saved_preferences: prefs.set(name,saved_preferences[name])
	Engine.max_fps=old_fps
	DisplayServer.window_set_vsync_mode(old_vsync)
	Input.mouse_mode=old_mouse
	_write_report()
	app.request_exit(0 if failures.is_empty() else 1)
