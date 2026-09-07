extends SceneTree
## Same external fixture can run against the shipped 0.5 PCK and current sources.
## 1 means one visible actor (valid 1v1 authority); 16 means four humans/twelve insects.
## This is a local comparison, not minimum-spec certification or a WAN benchmark.
var report: Dictionary = {"cases": [], "renderer": "Compatibility", "resolution": [1280,720]}

func _initialize() -> void:
	_run.call_deferred()

func percentile(values: Array[float], fraction: float) -> float:
	var sorted := values.duplicate()
	sorted.sort()
	return sorted[mini(sorted.size()-1, int(floor(sorted.size()*fraction)))]

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		printerr("Native renderer required")
		quit(1)
		return
	root.size = Vector2i(1280,720)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	report.version = ProjectSettings.get_setting("application/config/version", "unknown")
	report.adapter = RenderingServer.get_video_adapter_name()
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	var camera := Camera3D.new()
	root.add_child(camera)
	camera.current = true
	camera.fov = 70.0
	for population: int in [1,16]:
		for host: bool in [false,true]:
			var sim: RefCounted = load("res://scripts/simulation.gd").new()
			var roster := {}
			var brains := {}
			for id: int in range(1,maxi(2,population)+1):
				roster[id] = {"name":"Fixture %d"%id,"role":"human" if id<=(4 if population==16 else 1) else "mosquito"}
				var brain: RefCounted = load("res://scripts/bot_brain.gd").new()
				brain.setup(id)
				brains[id] = brain
			sim.start(roster,{"mode":"blood","human_count":4 if population==16 else 1,"round_seconds":180,"blood_goal":1000})
			for id: int in sim.actors:
				sim.actors[id].p = Vector3(4.0+float(id%4)*0.52,3.2+(0.9 if id>4 else 0.0),7.0+floorf(float(id-1)/4.0)*0.4)
			world.clear_actors()
			world.set_local_role(0,"lobby")
			camera.position = Vector3(3,4.75,6.2)
			camera.look_at(Vector3(6.2,4.05,9.6))
			var times: Array[float] = []
			var authority: Array[float] = []
			var cpu: Array[float] = []
			var gpu: Array[float] = []
			var draws: Array[float] = []
			var before := Time.get_ticks_usec()
			for frame: int in range(180):
				var snapshot: Dictionary = sim.public_snapshot()
				var server_start := Time.get_ticks_usec()
				if host:
					for id: int in brains:
						var intent: Dictionary = brains[id].decide(snapshot,sim.private_for(id),1.0/60.0)
						sim.submit_input(id,frame+1,intent.move,intent.yaw,intent.pitch,intent.interact,intent.sprint,intent.crouch,intent.jump)
						if not str(intent.action).is_empty(): sim.action(id,frame+1,str(intent.action),intent.yaw,intent.pitch)
					sim.step(1.0/60.0)
				var authority_ms := float(Time.get_ticks_usec()-server_start)/1000.0
				var visible: Dictionary = snapshot.actors.duplicate()
				if population==1:
					for id: int in visible.keys():
						if id!=1: visible.erase(id)
				world.sync_actors(visible,0,1.0/60.0)
				await RenderingServer.frame_post_draw
				var now := Time.get_ticks_usec()
				if frame>=60:
					times.append(float(now-before)/1000.0)
					authority.append(authority_ms)
					cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()))
					gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
					draws.append(float(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)))
				before = now
			var entry := {"visible_actors":population,"authority_actors":maxi(2,population),"host_bots":host,"samples":times.size(),"frame_p50_ms":percentile(times,.5),"frame_p90_ms":percentile(times,.9),"frame_p99_ms":percentile(times,.99),"authority_p90_ms":percentile(authority,.9),"render_cpu_p90_ms":percentile(cpu,.9),"render_gpu_p90_ms":percentile(gpu,.9),"draws_p50":percentile(draws,.5),"static_memory_bytes":Performance.get_monitor(Performance.MEMORY_STATIC),"video_memory_bytes":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)}
			report.cases.append(entry)
			print("PERFORMANCE06 "+JSON.stringify(entry))
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):
			var file := FileAccess.open(arg.trim_prefix("--report="),FileAccess.WRITE)
			file.store_string(JSON.stringify(report,"\t"))
	world.queue_free()
	camera.queue_free()
	await process_frame
	await process_frame
	quit(0)
