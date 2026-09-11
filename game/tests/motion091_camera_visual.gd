extends SceneTree
## Native first-person scenario: real mouse handler, input cadence, practice
## authority and Client camera. Fixed simulation dt; screenshots at fixed frames.
const Pose = preload("res://scripts/human_pose.gd")
const Prefs = preload("res://scripts/preferences.gd")
const DT := 1.0/60.0
const MAP_SEED := 1306743498
const CAPTURES := {45:"look-down",90:"walk-turn-down",150:"walk-look-up",205:"walk-turn-return",239:"full-turn-down",295:"settled"}
var app: Node
var client: Node
var report_path := ""
var checks := 0
var failures: Array[String] = []
var captures: Array[String] = []
var metrics: Dictionary = {}

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):report_path=arg.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<20:printerr("MOTION091_CAMERA_FAIL "+label)

func aim(yaw: float, pitch: float) -> void:
	var event := InputEventMouseMotion.new()
	event.relative=Vector2(-wrapf(yaw-float(client.yaw),-PI,PI)/Prefs.human_sensitivity,-(pitch-float(client.pitch))/Prefs.human_sensitivity*(-1.0 if Prefs.invert_y else 1.0))
	client._unhandled_input(event)

func _run() -> void:
	root.size=Vector2i(1280,720)
	app=load("res://scripts/main.gd").new();root.add_child(app)
	root.size=Vector2i(1280,720)
	client=app.get_node("Client")
	client.set_process(false)
	client.set_physics_process(false)
	client.set_process_unhandled_input(false)
	client.options["map-seed"]=MAP_SEED
	client._start_practice("human","blood")
	client.practice.set_physics_process(false)
	client.practice.brains.clear()
	var human: Dictionary=client.practice.sim.actors[1]
	human.yaw=0.0;human.body_yaw=0.0;human.pitch=0.0
	var spawn: Vector3=human.p
	client.practice._publish()
	aim(0,0)
	var travelled := 0.0
	var previous_position: Vector3=human.p
	var max_direction_error := 0.0
	var max_eye_error := 0.0
	var max_extra_eye_offset := 0.0
	var max_body_step := 0.0
	var previous_body := float(human.body_yaw)
	var walked_down := false
	for frame: int in range(300):
		var moving := frame>=60 and frame<240
		if moving:Input.action_press("move_forward")
		else:Input.action_release("move_forward")
		var turn := TAU*clampf(float(frame-60)/180.0,0.0,1.0)
		var look := -1.35*clampf(float(frame)/45.0,0.0,1.0)
		if frame>=120 and frame<180:look=lerpf(-1.35,1.0,float(frame-120)/59.0)
		elif frame>=180 and frame<240:look=lerpf(1.0,-1.35,float(frame-180)/59.0)
		elif frame>=240:look=lerpf(-1.35,0.0,float(frame-240)/59.0)
		aim(turn,look)
		client._physics_process(DT)
		client.practice.advance(DT)
		client._process(DT)
		var shown: Node3D=client.world.get_actor(1)
		var data: Dictionary=client.state.actors[1]
		var desired_direction := Pose.view_direction({"yaw":client.yaw,"pitch":client.pitch})
		max_direction_error=maxf(max_direction_error,(-client.camera.global_basis.z).distance_to(desired_direction))
		max_eye_error=maxf(max_eye_error,client.rig.global_position.distance_to(shown.human_view_origin()))
		var baseline_eye := shown.global_position+Pose.view_origin(data)-Vector3(data.p)
		max_extra_eye_offset=maxf(max_extra_eye_offset,client.rig.global_position.distance_to(baseline_eye))
		travelled+=Vector3(human.p).distance_to(previous_position)
		previous_position=human.p
		max_body_step=maxf(max_body_step,absf(wrapf(float(human.body_yaw)-previous_body,-PI,PI)))
		if moving and look<-.8 and absf(wrapf(float(human.body_yaw)-previous_body,-PI,PI))>.001:walked_down=true
		previous_body=human.body_yaw
		await process_frame
		if CAPTURES.has(frame) and not report_path.is_empty():
			await RenderingServer.frame_post_draw
			var path := report_path.trim_suffix(".json")+"-"+str(CAPTURES[frame])+".png"
			check(root.get_texture().get_image().save_png(path)==OK,"capture "+str(CAPTURES[frame]))
			captures.append(path)
	Input.action_release("move_forward")
	check(max_direction_error<.00001,"mouse direction remains immediate through full turn and pitch extremes")
	check(max_eye_error<.00001,"real Client camera follows the displayed body eye")
	check(max_extra_eye_offset<.00001,"local camera adds no positional or orbital delay to baseline")
	check(travelled>.4 and walked_down,"C02 turns torso while walking and looking down")
	check(absf(wrapf(float(human.yaw)-float(human.body_yaw),-PI,PI))<.02,"view recentres without permanent head/body offset")
	metrics={"map_seed":MAP_SEED,"spawn":str(spawn),"travel_m":travelled,"max_body_step_rad":max_body_step,"camera_direction_error":max_direction_error,"camera_eye_error_m":max_eye_error,"added_eye_offset_m":max_extra_eye_offset,"sim_hz":60,"snapshot_hz":30,"frames":300,"captures":captures}
	client.playing=false
	client.practice.stop()
	client.world.clear_actors()
	app.queue_free();await process_frame
	var result := {"checks":checks,"failures":failures,"metrics":metrics,"visual_approval":false,"performance_claim":false}
	if not report_path.is_empty():
		var file := FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(result,"\t"));file.close()
	print("MOTION091_CAMERA_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
