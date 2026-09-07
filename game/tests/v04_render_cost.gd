extends SceneTree
## Native relative-cost sample; this is not a minimum-hardware FPS claim.
func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		quit(0)
		return
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	var shadows: int = 0
	for light: Light3D in world.map_root.find_children("*","Light3D",true,false):
		if light is OmniLight3D or light is SpotLight3D:
			if "--no-room-shadows" in OS.get_cmdline_user_args():
				light.shadow_enabled = false
			if light.shadow_enabled:
				shadows += 1
	var camera := Camera3D.new()
	root.add_child(camera)
	camera.current = true
	camera.fov = 70.0
	var views: Dictionary = {
		"bedroom":[Vector3(3,4.75,6.2),Vector3(6.2,4.05,9.6)],
		"stairs":[Vector3(-11,1.55,-5.15),Vector3(-11,2.7,2.7)],
		"hallway":[Vector3(0,1.55,8),Vector3(0,1.55,-8)]
	}
	for label: String in views:
		camera.position = views[label][0]
		camera.look_at(views[label][1])
		for frame: int in range(35):
			await RenderingServer.frame_post_draw
		var samples: Array[float] = []
		var before: int = Time.get_ticks_usec()
		for frame: int in range(160):
			await RenderingServer.frame_post_draw
			var now: int = Time.get_ticks_usec()
			samples.append(float(now-before)/1000.0)
			before = now
		samples.sort()
		print("RENDER_COST %s median_ms=%.3f p90_ms=%.3f draw_calls=%d room_shadow_lights=%d" % [label,samples[80],samples[144],int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)),shadows])
	world.queue_free()
	camera.queue_free()
	await process_frame
	await process_frame
	quit(0)
