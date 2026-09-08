extends SceneTree
## Wall-clock measurement of real Main/Client/Practice at 60 Hz physics.
## Prepared stress scene: hallway traversal, authoritative bots, periodic doors.
## External dynamic loading also permits the identical fixture on shipped 0.6.
var resolution := Vector2i(1920,1080)
var population := 16
var seconds := 12.0
var path := ""
var app: Node
var client: Node
var report: Dictionary = {}

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg=="--resolution=1440": resolution=Vector2i(2560,1440)
		if arg=="--resolution=2160": resolution=Vector2i(3840,2160)
		if arg.begins_with("--population="): population=clampi(int(arg.trim_prefix("--population=")),1,16)
		if arg.begins_with("--seconds="): seconds=clampf(float(arg.trim_prefix("--seconds=")),5,30)
		if arg.begins_with("--report="): path=arg.trim_prefix("--report=")
	_run.call_deferred()

func distribution(samples: Array[float]) -> Dictionary:
	var sorted := samples.duplicate()
	sorted.sort()
	var over := 0
	for value: float in sorted:
		if value>1000.0/60.0: over+=1
	return {"p50":sorted[int(sorted.size()*.5)],"p90":sorted[int(sorted.size()*.9)],"p99":sorted[mini(sorted.size()-1,int(sorted.size()*.99))],"max":sorted[-1],"over_16_67ms_percent":100.0*over/sorted.size()}

func _run() -> void:
	if DisplayServer.get_name()=="headless": quit(1); return
	root.size=Vector2i(1920,1080)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps=0
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	# Apply the measurement's documented output after the client's saved settings.
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	root.content_scale_size=resolution
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps=0
	client=app.get_node("Client")
	client._start_practice("human","blood")
	client.practice.brains.clear()
	client.practice.input_sequences.clear()
	client.practice.action_sequences.clear()
	var roster := {}
	var humans := 4 if population==16 else 1
	for id: int in range(1,maxi(population,2)+1):
		roster[id]={"name":"Stress%d"%id,"role":"human" if id<=humans else "mosquito"}
		if id!=1:
			var brain: RefCounted=load("res://scripts/bot_brain.gd").new()
			brain.setup(id)
			client.practice.brains[id]=brain
			client.practice.input_sequences[id]=0
			client.practice.action_sequences[id]=0
	client.practice.sim.start(roster,{"human_count":humans,"mode":"blood","round_seconds":180,"blood_goal":1000})
	var sim: RefCounted=client.practice.sim
	sim.actors[1].p=Vector3(0,0,6.5)
	client.yaw=0.0
	client.pitch=-.06
	var doors_available := false
	for property: Dictionary in sim.get_property_list():
		if property.name=="doors": doors_available=true
	client.practice._publish()
	var frame_times: Array[float]=[]
	var render_cpu: Array[float]=[]
	var render_gpu: Array[float]=[]
	var physics: Array[float]=[]
	var draws: Array[float]=[]
	var vertices: Array[float]=[]
	var moving_frames := 0
	var transitions := 0
	var last_toggle := -1
	var started := Time.get_ticks_usec()
	var previous := started
	var start_position: Vector3=sim.actors[1].p
	Input.action_press("move_forward")
	while float(Time.get_ticks_usec()-started)/1e6 < seconds+2.0:
		await process_frame
		var elapsed := float(Time.get_ticks_usec()-started)/1e6
		# A bounded hall walk uses the real input/locomotion/camera loop.
		client.yaw=0.0 if posmod(int(elapsed/4),2)==0 else PI
		if doors_available and int(elapsed/2.5)!=last_toggle:
			last_toggle=int(elapsed/2.5)
			for id: String in sim.doors:
				if not sim.doors[id].moving:
					if sim.door_state.toggle(id,float(sim.elapsed)): transitions+=1
		await RenderingServer.frame_post_draw
		var now := Time.get_ticks_usec()
		if elapsed>=2.0:
			frame_times.append(float(now-previous)/1000.0)
			render_cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()))
			render_gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
			physics.append(Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS)*1000)
			draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
			vertices.append(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
			if doors_available:
				for door: Dictionary in sim.doors.values():
					if door.moving: moving_frames+=1; break
		previous=now
	Input.action_release("move_forward")
	report={"version":ProjectSettings.get_setting("application/config/version"),"adapter":RenderingServer.get_video_adapter_name(),"cpu":OS.get_processor_name(),"renderer":"Compatibility","resolution":root.content_scale_size,"window_size":root.size,"render_texture_size":root.get_texture().get_size(),"scenario":"real Main/Client/Practice, hallway input, live authoritative bots; periodic authoritative door commands when supported","actors":sim.actors.size(),"requested_population":population,"host_and_bots":true,"warmup_seconds":2,"measurement_seconds":seconds,"samples":frame_times.size(),"frame_ms":distribution(frame_times),"render_cpu_ms":distribution(render_cpu),"render_gpu_ms":distribution(render_gpu),"physics_ms":distribution(physics),"draws":distribution(draws),"primitives":distribution(vertices),"door_commands":transitions,"frames_with_moving_doors":moving_frames,"start_position":start_position,"end_position":sim.actors[1].p,"memory_bytes":Performance.get_monitor(Performance.MEMORY_STATIC),"video_memory_bytes":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED),"physics_ticks_per_second":Engine.physics_ticks_per_second,"vsync":"disabled","fps_limit":0}
	print("PERFORMANCE07_LIVE "+JSON.stringify(report))
	if not path.is_empty():
		var file:=FileAccess.open(path,FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
	await client._leave()
	app.queue_free()
	await process_frame
	quit()
