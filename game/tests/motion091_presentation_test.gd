extends SceneTree
## Snapshot/render cadence, camera anchor and authoritative contact regression.
const Display = preload("res://scripts/human_presentation.gd")
const Actor = preload("res://scripts/actor_view.gd")
const Pose = preload("res://scripts/human_pose.gd")
const MosquitoPoseData = preload("res://scripts/mosquito_pose.gd")
const World = preload("res://scripts/world.gd")
const Sim = preload("res://scripts/simulation.gd")
var checks := 0
var failures: Array[String] = []
var metrics: Dictionary = {}
var report_path := ""

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):report_path=arg.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<24:printerr("MOTION091_FAIL "+label)

func sample(time: float) -> Dictionary:
	return {"role":"human","state":"human","alive":true,"grounded":true,"tool":"hands","p":Vector3(time*3.1,0,0),"pose_time":time,"yaw":wrapf(2.9+time*2,-PI,PI),"body_yaw":wrapf(2.7+time*2,-PI,PI),"pitch":sin(time*3)*.9,"crouch_amount":0.0,"motion_phase":fposmod(time*9,TAU),"motion_blend":1.0,"motion_speed":3.1,"motion_stride":1.15,"motion_direction":Vector3.FORWARD,"air_blend":0.0,"land_blend":0.0}

func _cadences() -> void:
	for snapshot_hz: int in [20,30]:
		for render_hz: int in [60,120]:
			var display := Display.new()
			var dt := 1.0/float(render_hz)
			var previous := display.advance_motion(sample(0),dt)
			var previous_raw := sample(0)
			var maximum_step := 0.0
			var maximum_raw_step := 0.0
			var maximum_pitch_step := 0.0
			var maximum_raw_pitch_step := 0.0
			var maximum_phase_step := 0.0
			var maximum_raw_phase_step := 0.0
			for frame: int in range(1,render_hz*2+1):
				var time := floorf((float(frame)/render_hz+.000001)*snapshot_hz)/snapshot_hz
				var data := sample(time)
				var copy := data.duplicate(true)
				var shown := display.advance_motion(data,dt)
				var step := wrapf(float(shown.body_yaw)-float(previous.body_yaw),-PI,PI)
				var raw_step := absf(wrapf(float(data.body_yaw)-float(previous_raw.body_yaw),-PI,PI))
				maximum_step=maxf(maximum_step,absf(step))
				maximum_raw_step=maxf(maximum_raw_step,raw_step)
				maximum_pitch_step=maxf(maximum_pitch_step,absf(float(shown.pitch)-float(previous.pitch)))
				maximum_raw_pitch_step=maxf(maximum_raw_pitch_step,absf(float(data.pitch)-float(previous_raw.pitch)))
				maximum_phase_step=maxf(maximum_phase_step,absf(wrapf(float(shown.motion_phase)-float(previous.motion_phase),-PI,PI)))
				maximum_raw_phase_step=maxf(maximum_raw_phase_step,absf(wrapf(float(data.motion_phase)-float(previous_raw.motion_phase),-PI,PI)))
				check(step>=-.000001,"turn never reverses at PI seam")
				check(float(shown.pose_time)<=time+.000001,"no prediction of public clock")
				check(absf(wrapf(float(shown.yaw)-float(shown.body_yaw),-PI,PI)-.2)<.00001,"head/body yaw use same interpolation time")
				check(data==copy,"source snapshot unchanged")
				previous=shown;previous_raw=data
			var label := "%d_to_%d"%[snapshot_hz,render_hz]
			metrics[label]={"yaw_step":maximum_step,"raw_yaw_step":maximum_raw_step,"pitch_step":maximum_pitch_step,"raw_pitch_step":maximum_raw_pitch_step,"phase_step":maximum_phase_step,"raw_phase_step":maximum_raw_phase_step}
			check(maximum_step<maximum_raw_step*.8,"smaller body turn steps "+label)
			check(maximum_pitch_step<maximum_raw_pitch_step*.8,"smaller head pitch steps "+label)
			check(maximum_phase_step<maximum_raw_phase_step*.8,"smaller gait phase steps "+label)
			var held := sample(2)
			for frame: int in range(render_hz):previous=display.advance_motion(held,dt)
			check(absf(wrapf(float(previous.body_yaw)-float(held.body_yaw),-PI,PI))<.00001,"turn settles at latest received value")
			check(absf(float(previous.pose_time)-2.0)<.00001,"clock stops at latest snapshot during packet gap")
	var display := Display.new()
	display.advance_motion(sample(1),.016)
	check(display.advance_motion(sample(0),.016)==sample(0),"round clock rollback resets")
	var static_pose := sample(2);static_pose.erase("pose_time")
	check(display.advance_motion(static_pose,.016)==static_pose,"static preview exact")
	for field: Dictionary in [{"bitten":true},{"threatened":true},{"swing":.2},{"strike":{"active":true}},{"throw_gesture":{"state":"charging"}},{"throw_gesture":{"state":"release"}},{"throw_gesture":{"state":"recovering"}},{"emote_id":"wave"},{"alive":false},{"tool":"broom"}]:
		display.clear();display.advance_motion(sample(0),.016)
		var data := sample(.05);data.merge(field,true)
		check(display.advance_motion(data,.016)==data,"exact critical state "+str(field))

func _colliders(view: Node3D, data: Dictionary) -> void:
	var basis := Basis(Vector3.UP,Pose.body_yaw(data))
	for piece: Dictionary in Pose.collision_segments(data):
		var key := str(piece.key).replace("upperarm_","upper_arm_")
		var collider: StaticBody3D=view.pose_colliders[key]
		var shape: Shape3D=collider.get_child(0).shape
		var from := view.global_position+basis*Vector3(piece.from)
		var to := view.global_position+basis*Vector3(piece.to)
		var world_authority_center := Vector3(data.p)+basis*(Vector3(piece.from)+Vector3(piece.to))*.5
		var world_error := collider.global_position.distance_to(world_authority_center)
		if bool(data.get("bitten",false)):
			check(world_error<.00001,"attached collider world position matches full authority "+key)
		else:
			var category := "gesture" if view.human_presentation.motion_exact else "noncritical"
			metrics[category+"_max_collider_world_error_m"]=maxf(float(metrics.get(category+"_max_collider_world_error_m",0.0)),world_error)
			check(is_equal_approx(world_error,view.global_position.distance_to(data.p)),"world collider error is exactly legacy root translation "+key)
		check(collider.global_position.is_equal_approx((from+to)*.5),"world collider centre preserves public yaw "+key)
		check(is_equal_approx(shape.radius,float(piece.radius)),"collider radius unchanged "+key)
		if shape is CapsuleShape3D:
			var half_axis: float = (shape.height-2*shape.radius)*.5
			check((collider.global_position-collider.global_basis.y*half_axis).is_equal_approx(from),"world collider axis start "+key)
			check((collider.global_position+collider.global_basis.y*half_axis).is_equal_approx(to),"world collider axis end "+key)

func _actors() -> void:
	var view := Actor.new();root.add_child(view);view.build("human","Motion",0);view.set_local(true)
	for speed: float in [2.0,6.0]:
		view.initialized=false;view.human_presentation.clear()
		var max_added_eye_offset := 0.0
		var max_total_eye_error := 0.0
		for frame: int in range(121):
			var time := floorf(float(frame)/3.0)*.05
			var data := sample(time)
			data.body_yaw=wrapf(time*speed,-PI,PI)
			data.yaw=data.body_yaw+.2
			view.update_state(data,1.0/60)
			var baseline_eye := view.global_position+Pose.view_origin(data)-Vector3(data.p)
			max_added_eye_offset=maxf(max_added_eye_offset,view.human_view_origin().distance_to(baseline_eye))
			max_total_eye_error=maxf(max_total_eye_error,view.human_view_origin().distance_to(Pose.view_origin(data)))
		metrics["local_camera_%.1frad"%speed]={"added_eye_offset_vs_baseline_m":max_added_eye_offset,"eye_vs_authority_m":max_total_eye_error}
		check(max_added_eye_offset<.00001,"local camera adds no yaw parallax at %.1f rad/s"%speed)
	view.initialized=false;view.human_presentation.clear()
	view.set_local(false)
	var first := sample(0)
	view.update_state(first,1.0/60)
	var next := sample(.05)
	var copy := next.duplicate(true)
	view.update_state(next,1.0/60)
	check(absf(wrapf(view.rotation.y-Pose.body_yaw(next),-PI,PI))>.01,"render root turn is interpolated")
	var previous := view.rotation.y
	for frame: int in range(5):
		view.update_state(next,1.0/120)
		check(wrapf(view.rotation.y-previous,-PI,PI)>0,"root continues turning between snapshots")
		previous=view.rotation.y
		_colliders(view,next)
		check(view.to_local(view.human_view_origin()).is_equal_approx(Vector3(view.body_pose.eye)),"camera remains attached to displayed body eye")
	check(next==copy,"Actor leaves raw authority intact")
	for flag: String in ["threatened","bitten","swing","strike","throw_gesture"]:
		view.human_presentation.clear()
		view.update_state(first,1.0/60)
		view.update_state(next,1.0/60)
		var before := view.global_transform
		var raw_pose := Pose.sample(next)
		var max_prior_limb_lag := 0.0
		for joint: String in ["hand_l","hand_r","ankle_l","ankle_r"]:
			var shown_joint := before*Vector3(view.body_pose[joint])
			var raw_joint := before.origin+Basis(Vector3.UP,Pose.body_yaw(next))*Vector3(raw_pose[joint])
			max_prior_limb_lag=maxf(max_prior_limb_lag,shown_joint.distance_to(raw_joint))
		var urgent := sample(.1)
		match flag:
			"strike":urgent.strike={"active":true,"progress":.4,"point":Vector3(.2,1,-.3),"normal":Vector3.FORWARD,"hand":"right","tool":"hands"}
			"throw_gesture":urgent.throw_gesture={"state":"charging","progress":.4,"power":.4,"direction":Vector3.FORWARD,"tool":"newspaper"}
			"swing":urgent.swing=.2
			_:urgent[flag]=true
		view.update_state(urgent,1.0/60)
		metrics[flag+"_entry"]={"root_distance":before.origin.distance_to(view.global_position),"root_angle":before.basis.get_rotation_quaternion().angle_to(view.global_basis.get_rotation_quaternion()),"ordinary_filtered_distance":before.origin.distance_to(before.origin.lerp(urgent.p,1.0-exp(-18.0/60))),"extra_root_shift_vs_baseline_m":view.global_position.distance_to(before.origin.lerp(urgent.p,1.0-exp(-18.0/60))),"prior_limb_lag_m":max_prior_limb_lag}
		var expected_root: Vector3=urgent.p if flag=="bitten" else before.origin.lerp(urgent.p,1.0-exp(-18.0/60))
		check(view.global_position.is_equal_approx(expected_root),"only attachment changes root position policy "+flag)
		check(absf(wrapf(view.rotation.y-Pose.body_yaw(urgent),-PI,PI))<.00001,"contact yaw is exact "+flag)
		check(view.body_pose==Pose.sample(urgent),"contact skeleton is exact "+flag)
		check(view.human_view_origin().is_equal_approx(view.global_position+Pose.view_origin(urgent)-Vector3(urgent.p)),"contact camera offset matches authority "+flag)
		_colliders(view,urgent)
		var resumed := sample(.15)
		view.update_state(resumed,1.0/60)
		check(view.global_position.x>expected_root.x and view.global_position.x<resumed.p.x,"exit resumes continuously from contact position "+flag)
	view.set_pose_critical(true)
	view.update_state(next,1.0/60)
	check(view.body_pose==Pose.sample(next),"private marker selects exact local pose before posing")
	view.set_pose_critical(false)
	var teleport := sample(1);teleport.p=Vector3(20,0,0)
	view.update_state(teleport,1.0/60)
	check(view.global_position==teleport.p and view.body_pose==Pose.sample(teleport),"teleport resets entire render sample")
	view.queue_free();await process_frame

func _attached() -> void:
	var sim := Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}}, {"map_id":"house","mode":"blood","round_seconds":180,"blood_goal":1000})
	var world := World.new();root.add_child(world)
	sim.actors[2].state="biting"
	sim.actors[2]._assignment={"human":1,"zone":4,"revision":1}
	sim.actors[1].bitten=true
	for frame: int in range(24):
		var human: Dictionary=sim.actors[1]
		human.body_yaw=wrapf(3.0+frame*.08,-PI,PI)
		human.yaw=human.body_yaw+.1
		human.pitch=-.7+frame*.02
		human.p=Vector3(frame*.1,0,0)
		human.motion_phase=fposmod(frame*.3,TAU)
		human.motion_blend=1.0
		sim._update_attached()
		var public: Dictionary=sim.public_snapshot().actors
		var original := public.duplicate(true)
		world.sync_actors(public,1,1.0/60)
		var human_view: Node3D=world.get_actor(1)
		var insect_view: Node3D=world.get_actor(2)
		check(human_view.global_position==human.p,"bitten human root is current authority frame")
		check(insect_view.global_position.is_equal_approx(public[2].p),"attached mosquito has no independent positional delay")
		var expected := MosquitoPoseData.orientation(public[2])
		check(insect_view.model.global_basis.orthonormalized().is_equal_approx(expected),"attached mosquito normal has no independent angular delay")
		check(human_view.body_pose==Pose.sample(public[1]),"attached human uses exact limb phase")
		check(public==original,"attachment presentation does not mutate simulation snapshot")
	world.queue_free();await process_frame

func _run() -> void:
	_cadences()
	await _actors()
	await _attached()
	var result := {"checks":checks,"failures":failures,"metrics":metrics,"visual_approval":false,"wan":false}
	if not report_path.is_empty():
		var file := FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(result,"\t"));file.close()
	print("MOTION091_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
