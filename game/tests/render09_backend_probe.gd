extends SceneTree
## Static renderer diagnostic with identical map, quality and three cameras.
## Select the backend through Godot CLI; never changes project settings.
const World = preload("res://scripts/world.gd")
const Maps = preload("res://scripts/map_catalog.gd")
var output := ""
var world: Node3D
var records: Array[Dictionary] = []
var linear_tonemap := false

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output=argument.trim_prefix("--output=")
		if argument=="--linear-tonemap": linear_tonemap=true
	_run.call_deferred()

func _median(values: Array[float]) -> float:
	values.sort()
	return values[int(values.size()/2)]

func _capture(stage: String, views: Array[Dictionary]) -> void:
	for view: Dictionary in views:
		world.menu_camera.position=view.position
		world.menu_camera.look_at(view.target)
		world.menu_camera.make_current()
		for frame: int in range(16):
			await process_frame
			await RenderingServer.frame_post_draw
		var draws: Array[float]=[]
		var cpu: Array[float]=[]
		var gpu: Array[float]=[]
		for frame: int in range(30):
			await process_frame
			await RenderingServer.frame_post_draw
			draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
			cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()))
			gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
		var filename: String=stage+"-"+str(view.id)+".png"
		if root.get_texture().get_image().save_png(output+"/"+filename)!=OK:
			printerr("RENDER09_BACKEND_FAIL saving "+filename)
			quit(1)
			return
		records.append({"stage":stage,"view":view.id,"position":view.position,"target":view.target,"samples":30,"median_draws":_median(draws),"median_render_cpu_ms":_median(cpu),"median_render_gpu_ms":_median(gpu),"capture":filename})

func _run() -> void:
	if output.is_empty() or DisplayServer.get_name()=="headless":
		printerr("RENDER09_BACKEND_FAIL requires native rendering and explicit output")
		quit(1)
		return
	if FileAccess.file_exists(output+"/report.json"):
		printerr("RENDER09_BACKEND_FAIL evidence already exists")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output)
	root.size=Vector2i(1920,1080)
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	root.content_scale_size=Vector2i(1920,1080)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps=0
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	world=World.new()
	root.add_child(world)
	world.build()
	world.load_map("house-v1-1")
	if linear_tonemap: world.scene_environment.tonemap_mode=Environment.TONE_MAPPER_LINEAR
	world.set_process(false)
	world.audio_fx.set_process(false)
	var data: Dictionary=world.map_data
	if str(world.current_map)!="house-v1-1" or not data.has("fingerprint"):
		printerr("RENDER09_BACKEND_FAIL generated map mismatch")
		quit(1)
		return
	var room: Dictionary=data.rooms[0]
	var portal: Vector3=room.portal
	var inside: Vector3=(Vector3(room.center)-portal).normalized()
	var views: Array[Dictionary]=[
		{"id":"room","position":portal+inside*.8+Vector3.UP*1.6,"target":Vector3(room.center)+Vector3.UP*1.05},
		{"id":"hall","position":Vector3(0,1.6,0),"target":Vector3(0,1.6,-5)},
		{"id":"upper-hall","position":Vector3(0,float(data.floor_levels[1])+1.6,0),"target":Vector3(0,float(data.floor_levels[1])+1.6,5)},
	]
	await _capture("native",views)
	var report: Dictionary={"scope":"static environment renderer diagnostic; no actors, UI, simulation, gameplay FPS or visual approval","map_id":world.current_map,"fingerprint":data.fingerprint,"texture_size":root.get_texture().get_size(),"window_size":root.size,"adapter":RenderingServer.get_video_adapter_name(),"renderer":RenderingServer.get_current_rendering_method(),"rendering_driver":RenderingServer.get_current_rendering_driver_name(),"records":records,"visual_review":"pending","fixture_sha256":FileAccess.get_sha256("res://tests/render09_backend_probe.gd")}
	report["tonemap_mode"]=world.scene_environment.tonemap_mode
	report["linear_tonemap_override"]=linear_tonemap
	FileAccess.open(output+"/report.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("RENDER09_BACKEND "+JSON.stringify(report))
	world.queue_free()
	await process_frame
	await process_frame
	quit()
