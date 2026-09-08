extends "res://tests/social09_client_checks.gd"
## Native end-to-end support orientation in the authored physical house.
## Uses public snapshots/real Client camera; never injects a camera basis.
const ArenaData = preload("res://scripts/arena.gd")
const Surface = preload("res://scripts/surface_locomotion.gd")
var samples: Array[Dictionary]=[]
var sphere := SphereShape3D.new()
var query := PhysicsShapeQueryParameters3D.new()
var max_camera_step := 0.0
var solid_camera_frames := 0
var collisions: Array[Dictionary]=[]

func _camera_clear() -> bool:
	query.transform=Transform3D(Basis.IDENTITY,client.camera.global_position)
	return client.camera.get_world_3d().direct_space_state.intersect_shape(query,1).is_empty()

func _reset_at(position: Vector3, facing: float=0.0) -> void:
	var actor: Dictionary=client.practice.sim.actors[1]
	client.practice.sim.surface_motion.clear(actor)
	actor.state="flying"
	actor.p=position
	actor.velocity=Vector3.ZERO
	actor.yaw=facing
	actor.pitch=0.0
	client.yaw=facing
	client.pitch=0.0
	client.practice._publish()
	await _settle_frames(10)
	_check(ArenaData.can_fit_mosquito(actor.p,"house",client.practice.sim.doors),"fixture begins in free physical space")
	await _tap("perch")
	await _settle_frames(20)
	_check(actor.state=="perched","real F acquires physical support")

func _run() -> void:
	_backup()
	output=ProjectSettings.globalize_path("res://../outputs/0.9-social-integration")
	DirAccess.make_dir_recursive_absolute(output)
	Prefs.load_settings()
	Prefs.video_resolution=0
	Prefs.video_fullscreen=false
	root.size=Vector2i(1280,720)
	root.content_scale_size=Vector2i(1280,720)
	_bind("perch",KEY_F)
	_bind("move_forward",KEY_W)
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	client=app.get_node("Client")
	ui=client.ui
	# Explicit authored-map fixture: both authority and World receive the same
	# ID through Practice.start -> snapshot. This test does not mix generated
	# world coordinates with authored collision coordinates.
	client.practice_active=true
	client.local_id=1
	ui.set_practice(true)
	client.practice.start("mosquito","blood",Prefs.cosmetics,"Surface check",{"map_id":"house","round_seconds":180,"blood_goal":1000})
	client.practice.brains.clear()
	await _settle_frames(10)
	_check(client.playing and client.role=="mosquito" and client.world.current_map=="house" and client.practice.sim.config.map_id=="house","real Client and practice use identical authored house")
	_check(client.arm.shape is SphereShape3D and is_equal_approx(client.arm.shape.radius,.045) and is_equal_approx(client.camera.near,.025),"camera keeps original collision radius and near plane")
	sphere.radius=.025
	query.shape=sphere
	query.collision_mask=1
	query.collide_with_areas=false
	var faces := [
		{"label":"floor","p":Vector3(0,.16,6.5),"normal":Vector3.UP},
		{"label":"ceiling","p":Vector3(0,2.84,6.5),"normal":Vector3.DOWN},
		{"label":"west","p":Vector3(-1.74,1.6,2.4),"normal":Vector3.RIGHT},
		{"label":"east","p":Vector3(1.74,1.6,2.4),"normal":Vector3.LEFT},
		{"label":"north","p":Vector3(-7,1.8,5.54),"normal":Vector3.FORWARD},
		{"label":"south","p":Vector3(-7,1.8,6.06),"normal":Vector3.BACK},
	]
	var actor: Dictionary=client.practice.sim.actors[1]
	if OS.get_cmdline_user_args().has("--corner-only"): faces.clear()
	for face: Dictionary in faces:
		await _reset_at(face.p)
		var published: Dictionary=client.state.actors[1]
		_check(Vector3(published.surface_normal).is_equal_approx(face.normal),"authority normal reaches snapshot "+face.label)
		await _settle_frames(15)
		var visual: Node3D=client.world.get_actor(1)
		var displayed: Basis=visual.model.global_basis.orthonormalized()
		_check(displayed.y.dot(face.normal)>.995,"rendered mosquito adheres to actual normal "+face.label)
		_check(client.rig.global_basis.y.dot(face.normal)>.995,"camera up follows support "+face.label)
		_check((-client.camera.global_basis.z).dot(Vector3(published.surface_forward))>.99,"camera forward agrees with transported W "+face.label)
		_check(_camera_clear(),"native camera outside solid "+face.label)
		var start: Vector3=actor.p
		var forward: Vector3=published.surface_forward
		_action("move_forward",true)
		await _settle_frames(20)
		_action("move_forward",false)
		await _settle_frames(7)
		var delta: Vector3=Vector3(actor.p)-start
		_check(delta.dot(forward)>.10 and absf(delta.dot(face.normal))<.002,"physical W travels in camera-forward plane "+face.label)
		_check(actor.state=="perched" and _camera_clear(),"walking preserves support and camera clearance "+face.label)
		samples.append({"face":face.label,"normal":str(face.normal),"camera":str(client.camera.global_position),"movement":str(delta)})
		await _capture("surface-"+face.label)
	# The same real W input rounds two actual architectural corners continuously.
	await _reset_at(Vector3(1.5,.16,2.4),-PI*.5)
	var seen: Dictionary={}
	var initial: float=client.practice.sim.elapsed
	var previous_camera: Quaternion=client.rig.global_basis.get_rotation_quaternion()
	var previous_time: float=initial
	var wrong_direction := 0
	_action("move_forward",true)
	while float(client.practice.sim.elapsed)-initial<8.0:
		await process_frame
		var current_time: float=client.practice.sim.elapsed
		var dt: float=current_time-previous_time
		var normal: Vector3=actor._surface_normal
		seen[str(normal)]=true
		var current_camera: Quaternion=client.rig.global_basis.get_rotation_quaternion()
		var turn: float=previous_camera.angle_to(current_camera)
		max_camera_step=maxf(max_camera_step,turn)
		_check(turn<=minf(PI*.51,PI*.5*(1.0-exp(-12.0*maxf(dt,.001)))+.10),"camera rotates continuously at corner")
		if not _camera_clear():
			solid_camera_frames+=1
			var hits: Array=client.camera.get_world_3d().direct_space_state.intersect_shape(query,1)
			var collision: Dictionary={"camera":str(client.camera.global_position),"rig":str(client.rig.global_position),"body":str(actor.p),"normal":str(normal),"forward":str(actor._surface_forward),"time":current_time-initial}
			collision["arm_hit_length"]=client.arm.get_hit_length()
			collision["camera_local"]=str(client.camera.position)
			collision["rendered_body"]=str(client.world.get_actor(1).global_position)
			collision["rig_basis"]=str(client.rig.global_basis)
			if not hits.is_empty():
				var body: Node3D=hits[0].collider
				collision["collider"]=str(body.get_path())
				collision["collider_position"]=str(body.global_position)
				if body.get_child_count()>0 and body.get_child(0) is CollisionShape3D:
					var shape: Shape3D=body.get_child(0).shape
					if shape is BoxShape3D: collision["box_size"]=str(shape.size)
			collisions.append(collision)
		if actor.state!="perched" or Vector3(actor.velocity).dot(Vector3(actor._surface_forward))<-.001: wrong_direction+=1
		previous_camera=current_camera
		previous_time=current_time
	_action("move_forward",false)
	await _settle_frames(12)
	_check(seen.has(str(Vector3.UP)) and seen.has(str(Vector3.LEFT)) and seen.has(str(Vector3.DOWN)),"continuous W crosses real floor wall ceiling")
	_check(solid_camera_frames==0,"camera never enters solid during either corner")
	_check(wrong_direction==0,"surface input never detaches or reverses W at corner")
	_check(Vector3(actor._surface_normal)==Vector3.DOWN and Vector3(actor._surface_forward).x<-.99,"ceiling heading is transported rather than reconstructed from world yaw")
	_check((-client.camera.global_basis.z).dot(Vector3(actor._surface_forward))>.98,"camera settles aligned with ceiling travel")
	await _capture("surface-corner-ceiling")
	report={"samples":samples,"collisions":collisions,"solid_camera_frames":solid_camera_frames,"max_camera_step_degrees":rad_to_deg(max_camera_step),"map_id":"house","native_camera_radius_checked":.025}
	await _finish()

func _finish() -> void:
	finishing=true
	for action: String in held:
		if held[action]: _action(action,false)
	if is_instance_valid(app): app.queue_free()
	await _settle()
	_restore()
	var restored: bool=FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	_check(restored,"original preferences restored byte for byte")
	report.merge({"checks":checks,"failures":failures,"preferences_modified":not restored,"real_main_client_practice":true},true)
	var file:=FileAccess.open(output.path_join("surface-checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("SURFACE09_CLIENT_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
