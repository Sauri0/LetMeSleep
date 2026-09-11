extends SceneTree
## Independent 0.9.1 motion/defense gate. Exercises authority dictionaries,
## production collision/pose code and native ActorView presentation. Requires a
## renderer; does not inject OS input or exercise preferences/network/WAN.
const Generator=preload("res://scripts/procedural_house.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Sim=preload("res://scripts/simulation.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const Actor=preload("res://scripts/actor_view.gd")
const MAX_60HZ_ROOT_STEP:=ArenaData.HUMAN_RUN_SPEED/60.0+.005
var checks:=0
var failures: Array[String]=[]
var traces: Dictionary={}
var report_path:="res://../work/review091-motion-combat-results.json"

func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=30:printerr("REVIEW091_MOTION_FAIL "+label)
	return ok

func _open_doors(map_id:String)->Dictionary:
	var result:Dictionary={}
	for id:String in Doors.get_doors(map_id):
		result[id]={"angle":Doors.OPEN_ANGLE,"target_angle":Doors.OPEN_ANGLE,"moving":false,"blocked":false,"revision":1}
	return result

func _motion_trace(rate:int)->Dictionary:
	var map_id:=Generator.map_id(1)
	var states:=_open_doors(map_id)
	var actor:Dictionary={"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true,"crouch_amount":0.0}
	check(ArenaData.can_fit_human(actor.p,ArenaData.HUMAN_HEIGHT,map_id,states),"%dHz trace starts in clear generated hall"%rate)
	var dt:=1.0/float(rate)
	var total_view:=0.0
	var total_body:=0.0
	var reverse_body:=0
	var frozen_view:=0
	var max_local_camera_step:=0.0
	var previous_yaw:=float(actor.yaw)
	var previous_body:=float(actor.body_yaw)
	var previous_local_camera:=Pose.view_origin(actor)-Vector3(actor.p)
	var distance:=0.0
	var sprint_seen:=false
	var crouch_seen:=false
	var collision_free:=true
	# Four seconds perform the two turns; the fifth holds the final forward view
	# still, giving the torso the full one-second recenter window under test.
	for tick:int in range(rate*5):
		var elapsed:=float(tick+1)*dt
		var yaw:=wrapf(minf(elapsed,4.0)/4.0*TAU*2.0,-PI,PI)
		var pitch:=-1.30 if elapsed<3.0 else lerpf(-1.30,0.0,clampf(elapsed-3.0,0.0,1.0))
		var moving:=elapsed<3.0
		var sprint:=elapsed>=1.0 and elapsed<2.0
		var crouch:=elapsed>=2.0 and elapsed<3.0
		var move:=Vector3.FORWARD if moving else Vector3.ZERO
		var before:Vector3=actor.p
		ArenaData.step_human(actor,{"move":move,"yaw":yaw,"pitch":pitch,"sprint":sprint,"crouch":crouch,"jump":false},dt,map_id,states)
		distance+=before.distance_to(actor.p)
		var view_step:=wrapf(float(actor.yaw)-previous_yaw,-PI,PI)
		var body_step:=wrapf(float(actor.body_yaw)-previous_body,-PI,PI)
		total_view+=view_step;total_body+=body_step
		if elapsed<=4.0 and view_step<=0.000001:frozen_view+=1
		if body_step<-.000001:reverse_body+=1
		var local_camera:=Pose.view_origin(actor)-Vector3(actor.p)
		max_local_camera_step=maxf(max_local_camera_step,local_camera.distance_to(previous_local_camera))
		previous_yaw=actor.yaw;previous_body=actor.body_yaw;previous_local_camera=local_camera
		sprint_seen=sprint_seen or bool(actor.sprinting)
		crouch_seen=crouch_seen or float(actor.crouch_amount)>.95
		collision_free=collision_free and ArenaData.can_fit_human(actor.p,lerpf(ArenaData.HUMAN_HEIGHT,ArenaData.HUMAN_CROUCH_HEIGHT,float(actor.crouch_amount)),map_id,states)
	check(absf(total_view-TAU*2.0)<.002 and frozen_view==0,"%dHz consumes two full downward turns continuously"%rate)
	check(total_body>TAU and reverse_body==0,"%dHz torso follows moving inspection without reversal"%rate)
	check(distance>2.0 and sprint_seen and crouch_seen,"%dHz walk sprint and crouch all remain active"%rate)
	check(collision_free,"%dHz movement remains inside generated collision"%rate)
	check(max_local_camera_step<=.145,"%dHz local camera/head step remains bounded: %.5fm"%[rate,max_local_camera_step])
	check(absf(wrapf(float(actor.yaw)-float(actor.body_yaw),-PI,PI))<.05,"%dHz forward view recenters torso within one second"%rate)
	return {"rate":rate,"view_turn":total_view,"body_turn":total_body,"distance":distance,"max_local_camera_step":max_local_camera_step,"final_body_yaw":actor.body_yaw,"final_position":actor.p,"collision_free":collision_free}

func _attachment_and_defense()->Dictionary:
	var map_id:=Generator.map_id(1)
	var sim:=Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"map_id":map_id,"mode":"survival","rotation_seconds":4.0,"round_seconds":180.0})
	check(sim.phase=="playing","attachment fixture starts on generated map")
	var human:Dictionary=sim.actors[1]
	var insect:Dictionary=sim.actors[2]
	insect._assignment={"human":1,"zone":4,"revision":1}
	insect.state="biting"
	sim._update_attached()
	var first:Vector3=insect.p
	var max_error:=0.0
	var max_hitbox_error:=0.0
	for tick:int in range(120):
		var yaw:=wrapf(float(tick+1)/120.0*PI*1.5,-PI,PI)
		var crouch:=tick>=45 and tick<90
		sim.submit_input(1,tick+1,Vector3.FORWARD,yaw,-1.15,false,false,crouch,false)
		sim.step(1.0/60.0)
		var zone:Dictionary=sim._zone_pose(insect._assignment)
		var expected:=Vector3(zone.p)+Vector3(zone.normal)*Sim.ATTACH_OFFSET
		max_error=maxf(max_error,Vector3(insect.p).distance_to(expected))
		var public_human:Dictionary=sim.public_snapshot().actors[1]
		var public_pose:=Pose.sample(public_human)
		var authoritative_pose:=Pose.sample(human)
		max_hitbox_error=maxf(max_hitbox_error,Vector3(public_pose.head).distance_to(authoritative_pose.head))
		check(insect.state=="biting" and int(insect._assignment.human)==1,"attached mosquito stays on assigned moving human tick%d"%tick)
	check(first.distance_to(insect.p)>.20,"attached mosquito follows a materially changing body pose")
	check(max_error<.00001,"attachment uses exact authoritative zone pose")
	check(max_hitbox_error<.00001,"public body pose and authoritative hitbox pose remain aligned")
	# Stop locomotion before locking the exact manual ray. Duplicate sequence must
	# not create a second strike or result transition.
	for tick:int in range(15):
		sim.submit_input(1,121+tick,Vector3.ZERO,human.yaw,human.pitch,false,false,false,false)
		sim.step(1.0/60.0)
	var aim:=Pose.aim_angles(human,insect.p)
	sim.action(1,1,"attack",aim.x,aim.y)
	sim.action(1,1,"attack",aim.x,aim.y)
	var transitions:=0
	var was_alive:=bool(insect.alive)
	var windup_seen:=false
	for tick:int in range(18):
		sim.step(.025)
		windup_seen=windup_seen or str(sim.private_for(1).attack.get("state",""))=="windup"
		if was_alive and not bool(insect.alive):transitions+=1
		was_alive=bool(insect.alive)
	check(windup_seen,"manual defense retains visible physical anticipation")
	check(not bool(insect.alive) and transitions==1,"one exact manual ray produces exactly one hit transition")
	check(int(sim.private_for(1).attack.id)==1 and bool(sim.private_for(1).attack.hit),"duplicate action sequence cannot restart or duplicate defense")
	return {"attachment_motion_m":first.distance_to(insect.p),"max_attachment_error_m":max_error,"max_pose_error_m":max_hitbox_error,"alive_transitions":transitions,"attack":sim.private_for(1).attack}

func _presentation_transition()->Dictionary:
	var view:=Actor.new()
	root.add_child(view)
	view.build("human","Reviewer",0)
	view.set_local(false)
	var sample:=func(time:float)->Dictionary:return {"role":"human","state":"human","alive":true,"grounded":true,"tool":"hands","p":Vector3(time*ArenaData.HUMAN_SPEED,0,0),"pose_time":time,"yaw":.2,"body_yaw":0.0,"pitch":0.0,"crouch_amount":0.0,"motion_phase":time*8.0,"motion_blend":1.0,"motion_speed":ArenaData.HUMAN_SPEED,"motion_stride":1.15,"motion_direction":Vector3.RIGHT,"air_blend":0.0,"land_blend":0.0}
	view.update_state(sample.call(0.0),1.0/60.0)
	view.update_state(sample.call(.05),1.0/60.0)
	var before:Vector3=view.global_position
	var bitten:Dictionary=sample.call(.10);bitten.bitten=true
	view.update_state(bitten,1.0/60.0)
	var step:=before.distance_to(view.global_position)
	check(step<=MAX_60HZ_ROOT_STEP,"bitten entry root step %.5fm stays within one 60Hz sprint frame %.5fm"%[step,MAX_60HZ_ROOT_STEP])
	var result:={"root_step_m":step,"max_60hz_sprint_step_m":MAX_60HZ_ROOT_STEP}
	view.queue_free();await process_frame
	return result

func _run()->void:
	for rate:int in [30,60,120]:traces[str(rate)]=_motion_trace(rate)
	var body_values:Array=[]
	for rate:int in [30,60,120]:body_values.append(float(traces[str(rate)].body_turn))
	check(absf(body_values.max()-body_values.min())<.16,"body turn integration is stable across 30/60/120Hz")
	traces.defense=_attachment_and_defense()
	traces.presentation_transition=await _presentation_transition()
	var report:Dictionary={"checks":checks,"failures":failures,"traces":traces,"scope":"production authority/collision/pose; generated seed1; exact attachment and manual attack; no renderer, UI, network, FPS or WAN claim","source_sha256":{}}
	for path:String in ["res://tests/review091_motion_combat_contract.gd","res://scripts/arena.gd","res://scripts/human_pose.gd","res://scripts/simulation.gd","res://scripts/door_catalog.gd","res://scripts/actor_view.gd","res://scripts/human_presentation.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:check(false,"review report path writable")
	for failure:String in failures:print("REVIEW091_MOTION_DIAGNOSTIC "+failure)
	print("REVIEW091_MOTION_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)
