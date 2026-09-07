extends "res://tests/v05_rescue_client_checks.gd"
## Read-only runtime diagnostic of the exact rescue setup and rendered vertices.

func report(label: String) -> void:
	var human: Dictionary = simulation.actors[1]
	var view: ActorView = client.world.get_actor(1)
	var pose: Dictionary = Pose.sample(human)
	print("CONTACT_STATE "+JSON.stringify({"case":label,"p":str(human.p),"grounded":human.grounded,"velocity":str(human.velocity),"crouch":human.crouch_amount,"motion_speed":human.motion_speed,"motion_phase":human.motion_phase,"model_transform":str(view.model.global_transform)}))
	for suffix: String in ["l","r"]:
		var foot: MeshInstance3D = view.limb_feet[suffix]
		var vertices: PackedVector3Array = foot.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		var low := Vector3(0,INF,0)
		for vertex: Vector3 in vertices:
			var point: Vector3 = foot.global_transform*vertex
			if point.y<low.y:
				low = point
		var query := PhysicsRayQueryParameters3D.create(low+Vector3.UP*0.25,low-Vector3.UP*0.30,1)
		var floor_hit: Dictionary = client.world.get_world_3d().direct_space_state.intersect_ray(query)
		var floor_y: float = floor_hit.position.y if not floor_hit.is_empty() else NAN
		var shared: Dictionary
		for segment: Dictionary in Pose.collision_segments(human):
			if segment.key=="foot_"+suffix:
				shared = segment
		print("CONTACT_FOOT "+JSON.stringify({"case":label,"side":suffix,"ankle":str(pose["ankle_"+suffix]),"lowest_vertex":str(low),"physical_floor":floor_y,"clearance_mm":(low.y-floor_y)*1000,"capsule_min_y":human.p.y+shared.from.y-shared.radius,"shoe_transform":str(foot.global_transform)}))

func _run() -> void:
	root.size = Vector2i(1280,720)
	local_id = 3
	prepare()
	for frame: int in range(20):
		await process_frame
	await physics_frame
	report("exact-rescue-rest")
	var base: Dictionary = simulation.actors[1].duplicate(true)
	for data: Dictionary in [{"case":"crouch","crouch_amount":1.0},{"case":"run-left","motion_speed":5.0,"sprinting":true,"motion_phase":PI*0.5},{"case":"run-right","motion_speed":5.0,"sprinting":true,"motion_phase":PI*1.5}]:
		simulation.actors[1] = base.duplicate(true)
		simulation.actors[1].merge(data,true)
		snapshot()
		for frame: int in range(4):
			await process_frame
		report(str(data.case))
	var helper: Dictionary = simulation.actors[3]
	var target: Dictionary = simulation.actors[2]
	helper.p = target.p+Vector3(0,0.25,-0.45)
	var direction: Vector3 = (Vector3(target.p)-Vector3(helper.p)).normalized()
	for tick: int in range(181):
		sequence += 1
		simulation.submit_input(3,sequence,Vector3.ZERO,atan2(-direction.x,-direction.z),asin(direction.y),true)
		simulation.step(1.0/60.0)
	snapshot()
	await process_frame
	var progress: ProgressBar = client.ui._focus_progress
	var fill: StyleBoxFlat = progress.get_theme_stylebox("fill")
	var background: StyleBoxFlat = progress.get_theme_stylebox("background")
	print("CONTACT_PROGRESS "+JSON.stringify({"value":progress.value,"visible":progress.visible,"size":str(progress.size),"help":simulation.private_for(3).help,"fill_color":str(fill.bg_color),"fill_border":str(fill.border_color),"border_top":fill.border_width_top,"border_bottom":fill.border_width_bottom,"background":str(background.bg_color)}))
	client.playing = false
	client.world.clear_actors()
	await create_timer(0.15).timeout
	await process_frame
	client.queue_free()
	await process_frame
	await process_frame
	quit(0)
