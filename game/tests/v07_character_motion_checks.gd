extends SceneTree
const Pose = preload("res://scripts/human_pose.gd")
const Sim = preload("res://scripts/simulation.gd")
var checks := 0
var failures := 0

func check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures += 1
		print("MOTION07 FAIL "+message)

func _initialize() -> void:
	var maximum_slide := 0.0
	var supported_samples := 0
	for mode: String in ["walk","run","crouch"]:
		var speed := 5.0 if mode=="run" else 1.55 if mode=="crouch" else 3.1
		var data := {"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"grounded":true,"velocity":Vector3.FORWARD*speed,"sprinting":mode=="run","crouch_amount":1.0 if mode=="crouch" else 0.0}
		var previous := {}
		for frame: int in range(360):
			var displacement := Vector3.FORWARD*speed/120.0
			data.p += displacement
			Pose.advance_motion(data,displacement,1.0/120.0,true)
			var pose: Dictionary = Pose.sample(data)
			for side: String in ["_l","_r"]:
				var point: Vector3 = Vector3(pose["ankle"+side])+Vector3(data.p)
				if frame>60 and previous.has(side) and absf(point.y-.1)<.00001 and absf(Vector3(previous[side]).y-.1)<.00001:
					maximum_slide = maxf(maximum_slide,point.distance_to(previous[side]))
					supported_samples += 1
				previous[side] = point
			if frame%5!=0: continue
			for zone_id: int in range(8):
				var zone: Dictionary = Pose.zone_pose(data,Sim.BODY_ZONES[zone_id])
				var insect: Vector3 = zone.p+zone.normal*Sim.ATTACH_OFFSET
				var ray: Dictionary = Pose.ray_body(data,Pose.view_origin(data),insect,true)
				check(ray.is_empty(),"own target remains exposed %s frame%d zone%d" % [mode,frame,zone_id])
			for capsule: Dictionary in Pose.collision_segments(data):
				for endpoint: Vector3 in [capsule.from,capsule.to]:
					check(Vector2(endpoint.x,endpoint.z).length()+float(capsule.radius)<=.605,"body radius "+mode+str(capsule.key))
	check(supported_samples>150,"enough real support samples")
	check(maximum_slide<.001,"planted foot cancels forward displacement, max %.6fm/tick"%maximum_slide)
	var actor := {"p":Vector3.ZERO,"grounded":true,"velocity":Vector3.ZERO,"motion_blend":0.0}
	var last: Dictionary = Pose.sample(actor)
	var maximum_jump_delta := 0.0
	for frame: int in range(100):
		var was_grounded: bool = actor.grounded
		actor.grounded = frame<10 or frame>=65
		actor.velocity = Vector3(0,4.6-(frame-10)*12.0/120.0,0) if not actor.grounded else Vector3.ZERO
		Pose.advance_motion(actor,Vector3.ZERO,1.0/120.0,was_grounded)
		var pose := Pose.sample(actor)
		maximum_jump_delta = maxf(maximum_jump_delta,Vector3(pose.ankle_l).distance_to(last.ankle_l))
		last = pose
	check(maximum_jump_delta<.015,"jump/touchdown do not snap joints: %.6f"%maximum_jump_delta)
	check(Pose.strike_weight(.1)==0.0 and Pose.strike_weight(.45)==1.0 and Pose.strike_weight(1.0)==0.0,"anticipation/contact/recovery timing")
	for crouch: float in [0.0,1.0]:
		for direction: Vector3 in [Vector3.FORWARD,Vector3.BACK,Vector3.LEFT,Vector3.RIGHT]:
			for step: int in range(64):
				for zone_id: int in [4,5]:
					var data := {"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"crouch_amount":crouch,"motion_phase":TAU*step/64.0,"motion_speed":5.2,"motion_blend":1.0,"sprinting":true,"grounded":true,"motion_direction":direction}
					var zone: Dictionary = Pose.zone_pose(data,Sim.BODY_ZONES[zone_id])
					var aim: Vector2 = Pose.aim_angles(data,zone.p+zone.normal*Sim.ATTACH_OFFSET)
					check(absf(aim.x)<Pose.VIEW_YAW_LIMIT,"forearm inspection does not turn its own target away crouch%s direction%s phase%d zone%d" % [crouch,direction,step,zone_id])
	print("MOTION07_RESULT checks=%d failures=%d max_support_slide=%.7fm samples=%d max_jump_delta=%.6f" % [checks,failures,maximum_slide,supported_samples,maximum_jump_delta])
	quit(failures)
