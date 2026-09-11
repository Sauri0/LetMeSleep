extends SceneTree
const Actor = preload("res://scripts/actor_view.gd")
const World = preload("res://scripts/world.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Mosquito = preload("res://scripts/mosquito_pose.gd")
const Sim = preload("res://scripts/simulation.gd")
const DT := 1.0/60.0
var checks := 0
var failures: Array[String] = []
var metrics := {"max_bite_entry_camera_extra_m":0.0,"max_relative_contact_error_m":0.0,"max_shared_collider_error_m":0.0,"max_marker_error_m":0.0}
var report_path := ""

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):report_path=arg.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<24:printerr("MOTION091_ATTACHMENT_FAIL "+label)

func _run() -> void:
	var sim := Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"},3:{"role":"human"},4:{"role":"mosquito"}}, {"map_id":"house","mode":"blood","round_seconds":180,"blood_goal":1000})
	var world := World.new();root.add_child(world);world._build_marker()
	world.local_role="human"
	var camera := Camera3D.new();world.add_child(camera)
	for frame: int in range(72):
		for human_id: int in [1,3]:
			var human: Dictionary=sim.actors[human_id]
			human.p=Vector3(-3+frame*.06 if human_id==1 else 3-frame*.03,0,0)
			human.body_yaw=wrapf(3+frame*.03,-PI,PI)
			human.yaw=human.body_yaw+.12
			human.pitch=-1.1
			human.pose_time=frame*DT
			human.motion_phase=fposmod(frame*.24,TAU)
			human.motion_blend=1.0
			human.crouch_amount=.25+.2*sin(frame*.05)
			human.bitten=frame>=15 and not (human_id==1 and frame>=45 and frame<55)
		if frame>=30 and frame<45:
			# Identical authority positions, distinct render histories: proximity
			# inference is ambiguous, explicit active parent ID must win.
			sim.actors[3].p=sim.actors[1].p
		for insect_id: int in [2,4]:
			var insect: Dictionary=sim.actors[insect_id]
			var biting := frame>=15 and not (insect_id==2 and frame>=45 and frame<55)
			insect.state="biting" if biting else "flying"
			var host := (1 if insect_id==2 else 3) if frame<55 else (3 if insect_id==2 else 1)
			insect._assignment={"human":host,"zone":4 if insect_id==2 else 8,"revision":1}
			if not biting:insect.p=Vector3(sim.actors[host].p)+Vector3(.5,1,.5)
		sim._update_attached()
		var public: Dictionary=sim.public_snapshot().actors
		if frame>=65:public.erase(3)
		if frame==70:public[2].attached_to=0
		if frame==71:public[2].attached_to="1"
		var ordered: Dictionary={}
		for id: int in [4,2,3,1]:
			if public.has(id):ordered[id]=public[id]
		var original := ordered.duplicate(true)
		var old_roots: Dictionary={}
		for human_id: int in [1,3]:
			if world.actors.has(human_id):old_roots[human_id]=world.get_actor(human_id).global_position
		world.sync_actors(ordered,1,DT)
		Actor.align_attachments(ordered,world.actors)
		for human_id: int in [1,3]:
			if not ordered.has(human_id):continue
			var data: Dictionary=ordered[human_id]
			var view: Node3D=world.get_actor(human_id)
			var expected: Vector3=data.p
			if old_roots.has(human_id) and Vector3(old_roots[human_id]).distance_to(data.p)<=2.2:
				expected=Vector3(old_roots[human_id]).lerp(data.p,1-exp(-18*DT))
			check(view.global_position.is_equal_approx(expected),"human root follows unchanged baseline filter at frame %d"%frame)
			if human_id==1:
				var expected_eye := expected+Pose.view_origin(data)-Vector3(data.p)
				var camera_error: float = view.human_view_origin().distance_to(expected_eye)
				metrics.max_bite_entry_camera_extra_m=maxf(metrics.max_bite_entry_camera_extra_m,camera_error)
				check(camera_error<.00001,"local eye adds no bite entry/sustain/exit correction")
		for insect_id: int in [2,4]:
			var data: Dictionary=ordered[insect_id]
			var view: Node3D=world.get_actor(insect_id)
			var host_id: Variant=data.attached_to
			var attached: bool=data.state=="biting" and host_id is int and host_id!=0 and ordered.has(host_id)
			if not attached:
				if data.state=="biting":check(view.global_position.is_equal_approx(data.p),"missing/invalid host cannot reuse old offset")
				continue
			var host_data: Dictionary=ordered[host_id]
			var host: Node3D=world.get_actor(host_id)
			var shift := host.global_position-Vector3(host_data.p)
			var contact_error := view.global_position.distance_to(Vector3(data.p)+shift)
			metrics.max_relative_contact_error_m=maxf(metrics.max_relative_contact_error_m,contact_error)
			check(contact_error<.00001,"explicit parent controls contact despite reverse order or overlap")
			check((view.global_position-host.human_view_origin()).is_equal_approx(Vector3(data.p)-Pose.view_origin(host_data)),"self-defense ray relative to insect equals authority ray")
			var pieces := Mosquito.collision_segments(data)
			for index: int in range(pieces.size()):
				var piece: Dictionary=pieces[index]
				var collider: Node3D=view.body_shapes[index]
				var expected := (Vector3(piece.from)+Vector3(piece.to))*.5+shift
				var error := collider.global_position.distance_to(expected)
				metrics.max_shared_collider_error_m=maxf(metrics.max_shared_collider_error_m,error)
				check(error<.00001,"mosquito ray collider shares host render translation")
		check(ordered==original,"attachment pass never modifies public snapshot")
		await physics_frame
		if frame in [15,25,55,60]:
			var assignment: Dictionary=sim.private_for(2).assignment
			var host: Node3D=world.get_actor(int(assignment.human))
			var host_data: Dictionary=ordered[int(assignment.human)]
			var shift := host.global_position-Vector3(host_data.p)
			var target := Vector3(assignment.p)+shift
			var normal: Vector3=assignment.normal
			camera.global_position=target+normal*.5
			camera.look_at(target,Vector3.UP)
			world.local_actor_id=2
			world.show_assignment(assignment,camera,world.get_actor(2).global_position,{"state":"biting","progress":1.0})
			check(world.marker.visible,"actual private mark stays visible at common-frame contact")
			var error := world.marker.global_position.distance_to(target+normal*.009)
			metrics.max_marker_error_m=maxf(metrics.max_marker_error_m,error)
			check(error<.00001,"actual marker shares translation with insect and host")
	world.queue_free();await process_frame
	var result := {"checks":checks,"failures":failures,"metrics":metrics,"scope":"shared active-bite render translation; no new input latency or root policy; public parent contract from Director"}
	if not report_path.is_empty():
		var file := FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(result,"\t"));file.close()
	print("MOTION091_ATTACHMENT_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
