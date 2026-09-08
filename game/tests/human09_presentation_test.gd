extends SceneTree
const Display = preload("res://scripts/human_presentation.gd")
const Actor = preload("res://scripts/actor_view.gd")
const World = preload("res://scripts/world.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Sim = preload("res://scripts/simulation.gd")
const DT := 1.0/60.0
var checks := 0
var failures: Array[String] = []
var report_path := ""
var metrics: Dictionary = {}

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<20:printerr("HUMAN09_PRESENTATION_FAIL "+label)

func _data(crouch: float, time: float) -> Dictionary:
	return {"role":"human","p":Vector3.ZERO,"state":"human","alive":true,"grounded":true,"tool":"hands","crouch_amount":crouch,"pose_time":time,"yaw":0.0,"body_yaw":0.0,"pitch":0.0}

func _helpers() -> void:
	for rate: int in [30,60,120]:
		var display := Display.new()
		var dt := 1.0/float(rate)
		var sample := _data(1.0,0.0)
		var previous := display.advance(sample,dt)
		var max_step := 0.0
		for tick: int in range(1,rate+1):
			var elapsed := float(tick)/float(rate)
			var sample_time := floorf(elapsed*20.0)/20.0
			sample=_data(maxf(0.0,1.0-sample_time*6.0),sample_time)
			sample.yaw=elapsed*.25
			var original := sample.duplicate(true)
			var presented := display.advance(sample,dt)
			var delta := absf(float(presented.crouch_amount)-float(previous.crouch_amount))
			max_step=maxf(max_step,delta)
			check(delta<=6.0*dt+.000001,"bounded crouch transition "+str(rate))
			check(float(presented.crouch_amount)>=float(sample.crouch_amount)-.000001 and float(presented.crouch_amount)<=float(previous.crouch_amount)+.000001,"no posture overshoot or extrapolation")
			check(presented.yaw==sample.yaw and presented.pose_time==sample.pose_time,"aim and public clock unfiltered")
			check(sample==original,"input dictionary unchanged")
			previous=presented
		check(is_zero_approx(float(previous.crouch_amount)),"settles at latest authority posture")
		metrics[str(rate)+"hz_max_crouch_delta"]=max_step
	var display := Display.new()
	display.advance(_data(1,1),DT)
	var target := _data(.6,1.05)
	check(float(display.advance(target,DT).crouch_amount)>.6,"ordinary remote posture is smoothed")
	check(display.advance(target,DT,true)==target,"critical entry is exact immediately")
	var next := _data(.3,1.10)
	check(is_equal_approx(float(display.advance(next,DT).crouch_amount),.5),"critical exit resumes from last exact posture")
	check(display.advance(_data(0,0),DT)==_data(0,0),"new round clock resets")
	display.advance(_data(1,2),DT,true)
	check(display.advance(_data(0,2.05),DT,false,true)==_data(0,2.05),"teleport reset is exact")
	var static_pose := _data(1,3);static_pose.erase("pose_time")
	check(display.advance(static_pose,DT)==static_pose,"static pose without public clock remains exact")
	for trigger: Dictionary in [{"bitten":true},{"threatened":true},{"swing":.3},{"strike":{"active":true,"progress":.3}},{"throw_gesture":{"state":"charging"}},{"throw_gesture":{"state":"release"}},{"throw_gesture":{"state":"recovering"}},{"emote_id":"wave"},{"alive":false},{"tool":"broom"}]:
		display.clear();display.advance(_data(1,1),DT)
		var urgent := _data(0,1.05);urgent.merge(trigger,true)
		check(display.advance(urgent,DT)==urgent,"critical fields bypass filter "+str(trigger))

func _colliders(view: Node3D, data: Dictionary, label: String) -> void:
	for piece: Dictionary in Pose.collision_segments(data):
		var key := str(piece.key).replace("upperarm_","upper_arm_")
		var collider: StaticBody3D=view.pose_colliders[key]
		var shape: Shape3D=collider.get_child(0).shape
		check(collider.position.is_equal_approx((Vector3(piece.from)+Vector3(piece.to))*.5),label+" collider position "+key)
		check(is_equal_approx(float(shape.radius),float(piece.radius)),label+" radius "+key)
		if shape is CapsuleShape3D:check(is_equal_approx(shape.height,Vector3(piece.from).distance_to(piece.to)+float(piece.radius)*2),label+" height "+key)

func _integration() -> void:
	var world := World.new();root.add_child(world)
	# Production World.sync_actors works independently of build/map/audio setup.
	world.local_role="mosquito"
	var initial := _data(1,1)
	world.sync_actors({1:initial},2,DT)
	var view: Node3D=world.get_actor(1)
	var changed := _data(.6,1.05)
	world.sync_actors({1:changed},2,DT)
	check(float(view.human_snapshot_values.crouch_amount)>.6,"World ordinary remote mesh is smoothed")
	_colliders(view,changed,"ordinary remote")
	world.sync_actors({1:changed},2,DT,1)
	check(view.body_pose==Pose.sample(changed),"private target hook applies BEFORE same-frame posing")
	_colliders(view,changed,"marked target")
	var next := _data(.3,1.10)
	world.sync_actors({1:next},2,DT)
	check(float(view.human_snapshot_values.crouch_amount)>.3 and not view.pose_critical,"cleared private target resumes smoothing")
	view.set_local(true)
	world.sync_actors({1:next},2,DT)
	check(view.body_pose==Pose.sample(next),"FPS body pose is exact")
	check(Vector3(view.body_pose.eye).is_equal_approx(Pose.view_origin(next)-Vector3(next.p)),"FPS eye stays at unchanged authoritative camera origin")
	view.set_local(false)
	for trigger: Dictionary in [{"bitten":true},{"threatened":true},{"strike":{"active":true,"progress":.45,"point":Vector3(-.2,.85,-.4),"normal":Vector3.FORWARD,"hand":"right","tool":"hands"}},{"throw_gesture":{"state":"charging","progress":.5,"power":.5,"direction":Vector3.FORWARD,"tool":"newspaper"}}]:
		world.sync_actors({1:_data(1,2)},2,DT,1)
		var urgent := _data(.2,2.05);urgent.merge(trigger,true)
		var copy := urgent.duplicate(true)
		world.sync_actors({1:urgent},2,DT)
		check(view.body_pose==Pose.sample(urgent),"actual mesh exact for "+str(trigger))
		check(urgent==copy,"World/Actor never mutate authority input")
		_colliders(view,urgent,"critical action")
	var teleport := _data(.1,3);teleport.p=Vector3(10,0,0)
	world.sync_actors({1:teleport},2,DT)
	check(view.body_pose==Pose.sample(teleport),"actual Actor teleport resets displayed posture")
	world.sync_actors({1:{"role":"mosquito","state":"flying","alive":true,"p":Vector3(1,2,0)}},2,DT)
	world.sync_actors({1:_data(.8,0)},2,DT)
	check(world.get_actor(1).body_pose==Pose.sample(_data(.8,0)),"role replacement initializes exact posture")
	world.queue_free()
	await process_frame

func _authority() -> void:
	var sim := Sim.new();var reference := Sim.new()
	var roster := {1:{"role":"human"},2:{"role":"mosquito"}}
	var config := {"map_id":"house","mode":"blood","round_seconds":180,"blood_goal":1000}
	sim.start(roster,config);reference.start(roster,config)
	var view := Actor.new();root.add_child(view);view.build("human","",0)
	var public: Dictionary={}
	var maximum_smoothed_head_step := 0.0
	var previous_head := Vector3.ZERO
	for tick: int in range(120):
		var crouch: bool=tick<40
		for authority: RefCounted in [sim,reference]:
			authority.submit_input(1,tick+1,Vector3.ZERO,0,-1.2,false,false,crouch)
			authority.step(DT)
		if tick%3==0:public=sim.public_snapshot().actors[1]
		view.update_state(public,DT)
		check(sim.public_snapshot()==reference.public_snapshot(),"presentation leaves full authority snapshot identical")
		check(sim.private_for(1)==reference.private_for(1) and sim.private_for(2)==reference.private_for(2),"presentation leaves both private channels identical")
		if tick>40 and tick<65:maximum_smoothed_head_step=maxf(maximum_smoothed_head_step,Vector3(view.body_pose.head).distance_to(previous_head))
		previous_head=view.body_pose.head
	check(maximum_smoothed_head_step<=.056,"real 20 Hz rise has at most one 60 Hz crouch step in head")
	metrics.authority_60hz_smoothed_head_step_m=maximum_smoothed_head_step
	view.queue_free()
	await process_frame

func _run() -> void:
	_helpers()
	await _integration()
	await _authority()
	await process_frame
	var result := {"checks":checks,"failures":failures,"metrics":metrics,"scope":"remote noncritical crouch only; local FPS/private mark/action/contact preserve exact pose; full authority snapshots and both private channels unchanged","visual_approval":false}
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(result,"\t"));file.close()
	print("HUMAN09_PRESENTATION checks=%d failures=%d"%[checks,failures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)
