extends "res://tests/render09_backend_probe.gd"
## Diagnostic only: never changes World, project settings or the gallery.

func _falloff(distance: float, reach: float) -> float:
	return pow(maxf(1.0-pow(distance/reach,4.0),0.0),2.0)

func _run() -> void:
	if output.is_empty() or DisplayServer.get_name()=="headless" or FileAccess.file_exists(output+"/report.json"):
		printerr("LIGHT_RANGE09_FAIL requires native rendering and fresh output")
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
	world.set_process(false)
	world.audio_fx.set_process(false)
	var data: Dictionary=world.map_data
	var room: Dictionary=data.rooms[0]
	var bounds: AABB=room.bounds
	var portal: Vector3=room.portal
	var center: Vector3=room.center
	var inside: Vector3=(center-portal).normalized()
	var corner:=Vector3(bounds.position.x+.3,bounds.position.y+.2,bounds.position.z+.3)
	var views: Array[Dictionary]=[
		{"id":"room","position":portal+inside*.8+Vector3.UP*1.6,"target":center+Vector3.UP*1.05},
		{"id":"hall","position":Vector3(0,1.6,0),"target":Vector3(0,1.6,-5)},
		{"id":"upper-hall","position":Vector3(0,float(data.floor_levels[1])+1.6,0),"target":Vector3(0,float(data.floor_levels[1])+1.6,5)},
		{"id":"room-corner","position":center+Vector3.UP*1.6,"target":corner},
		{"id":"door-spill","position":portal+inside*1.8+Vector3.UP*1.6,"target":portal-inside*1.0+Vector3.UP*.2},
	]
	# DoorView.setup starts all doors open; preserve these exact poses for A/B.
	await _capture("before",views)
	var changes: Array[Dictionary]=[]
	for child: Node in world.map_root.get_children():
		if not child is SpotLight3D or not child.has_meta("house_room"): continue
		var lamp: SpotLight3D=child
		var matched: Dictionary={}
		for candidate: Dictionary in data.rooms:
			var b: AABB=candidate.bounds
			var expected:=Vector3(b.get_center().x,b.position.y+2.49,b.get_center().z)
			if lamp.position.distance_to(expected)<.001:
				matched=candidate
				break
		if matched.is_empty():
			printerr("LIGHT_RANGE09_FAIL unmatched room lamp")
			quit(1)
			return
		var b: AABB=matched.bounds
		var floor_corner_distance:=Vector3(b.size.x*.5,2.49,b.size.z*.5).length()
		var old_range:=lamp.spot_range
		var new_range:=minf(old_range,maxf(floor_corner_distance+1.0,old_range*.85))
		var compensation:=_falloff(2.49,old_range)/_falloff(2.49,new_range)
		changes.append({"position":lamp.position,"old_range":old_range,"new_range":new_range,"old_energy":lamp.light_energy,"new_energy":lamp.light_energy*compensation,"floor_corner_distance":floor_corner_distance,"corner_radial_light_ratio":compensation*_falloff(floor_corner_distance,new_range)/_falloff(floor_corner_distance,old_range)})
		lamp.spot_range=new_range
		lamp.light_energy*=compensation
	await _capture("after",views)
	var report: Dictionary={"scope":"static room-light range pilot; unchanged shadows, light count, geometry, camera and tonemapping; no actors or gameplay FPS","map_id":world.current_map,"fingerprint":data.fingerprint,"texture_size":root.get_texture().get_size(),"renderer":RenderingServer.get_current_rendering_method(),"changes":changes,"records":records,"visual_review":"pending; center compensation does not preserve edges","world_sha256":FileAccess.get_sha256("res://scripts/world.gd"),"fixture_sha256":FileAccess.get_sha256("res://tests/render09_light_range_probe.gd")}
	var file:=FileAccess.open(output+"/report.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("LIGHT_RANGE09 "+JSON.stringify(report))
	world.queue_free()
	await process_frame
	await process_frame
	quit()
