extends SceneTree
## 0.9.2 hand correction: imported bone geometry, palm contact and shared poses.
const Pose = preload("res://scripts/human_pose.gd")
const Tools = preload("res://scripts/tool_catalog.gd")
const Simulation = preload("res://scripts/simulation.gd")
const ArenaData = preload("res://scripts/arena.gd")
const CharacterSkin = preload("res://assets/art/characters/shared/character_skin.gd")
var checks := 0
var failures: Array[String] = []
var metrics := {"maximum_palm_contact_error":null,"maximum_digit_length_error":null,"minimum_neutral_curl":null,"maximum_carry_reservation_radius":0.0}
var report_path := ""
var pose_only := false

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="): report_path=arg.trim_prefix("--report=")
		if arg=="--pose-only": pose_only=true
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<30: printerr("HAND092 FAIL "+label)

func _point(value: Array) -> Vector3:
	return Vector3(value[0],value[1],value[2])

func _poses() -> void:
	for side: String in ["left","right"]:
		var suffix := "_l" if side=="left" else "_r"
		var other := "_r" if side=="left" else "_l"
		for yaw: float in [0.0,1.4,-2.9]:
			for crouch: float in [0.0,1.0]:
				var actor := {"p":Vector3(2,0,-3),"body_yaw":yaw,"yaw":yaw,"tool":"hands","crouch_amount":crouch}
				var rest := Pose.sample(actor)
				var shoulder: Vector3 = rest["shoulder"+suffix]
				for normal: Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.BACK,Vector3.FORWARD,Vector3(.3,.5,.8).normalized()]:
					for distance: float in [.35,.75,1.3]:
						var target := shoulder+Vector3(.1,-.2,-1).normalized()*distance
						var expected := shoulder+(target-shoulder).limit_length(Pose.ARM_REACH)
						actor.strike={"active":true,"hand":side,"tool":"hands","point":Vector3(actor.p)+target.rotated(Vector3.UP,yaw),"normal":normal.rotated(Vector3.UP,yaw),"progress":.45}
						var posed := Pose.sample(actor)
						var width: Vector3 = posed["hand_width"+suffix]
						var fingers: Vector3 = posed["hand_direction"+suffix]
						var frame := Basis(width,fingers,width.cross(fingers))
						check(frame.is_finite() and absf(frame.determinant()-1.0)<.00001,"proper rotation "+side)
						check((-frame.z).dot(-normal)>.99999,"palmar side faces surface "+side)
						check(absf(fingers.dot(normal))<.00001,"fingers tangent to contact surface "+side)
						var centre: Vector3 = posed["hand"+suffix]+fingers*.05
						check(centre.distance_to(expected)<.00001,"wrist and palm reach geometric target "+side)
						check(Vector3(posed.strike_contact).distance_to(centre)<.00001,"authority follows actual palm "+side)
						check(centre.distance_to(shoulder)<=Pose.ARM_REACH+.00001,"no added attack reach "+side)
						check(posed["hand"+other]==rest["hand"+other] and posed["elbow"+other]==rest["elbow"+other],"other arm unchanged "+side)
						var previous: Dictionary = {}
						for frame_index: int in range(121):
							actor.strike.progress=float(frame_index)/120.0
							var moving := Pose.sample(actor)
							check(Vector3(moving.strike_contact).is_finite(),"finite complete swing "+side)
							if not previous.is_empty():
								check(Vector3(moving["hand"+suffix]).distance_to(previous["hand"+suffix])<.08,"continuous wrist trajectory "+side)
							previous=moving
	for tool: String in Tools.GRASPS:
		for state: String in ["idle","charging","release","recovering"]:
			for progress: float in [0.0,.2,.5,.8,1.0]:
				var actor := {"tool":tool,"throw_gesture":{"state":state,"progress":progress,"power":.7,"direction":Vector3(.2,.3,-.8).normalized(),"tool":tool}}
				var posed := Pose.sample(actor)
				var centre: Vector3 = posed.hand_r+Vector3(posed.hand_direction_r)*.05
				var toward_object: Vector3 = Vector3(posed.tool_grip)-centre
				var palm := -Vector3(posed.hand_width_r).cross(posed.hand_direction_r)
				check(toward_object.dot(palm)>0.0,"shaft on anatomical palm side "+tool+" "+state)
				check(absf(toward_object.length()-(float(Tools.GRASPS[tool].radius)+.018))<.00001,"shaft clearance preserved "+tool)

func _carry_envelope() -> void:
	for tool: String in Tools.GRASPS:
		for crouch: float in [0.0,.5,1.0]:
			for running: bool in [false,true]:
				for frame: int in range(25):
					var actor := {"tool":tool,"crouch_amount":crouch,"motion_phase":TAU*float(frame)/24.0,"motion_speed":5.0 if running else 3.1,"sprinting":running}
					var pose := Pose.sample(actor)
					var capsules := Pose.collision_segments(actor,pose)
					for zone_id: int in [4,5]:
						var zone := Pose.zone_pose(actor,Simulation.BODY_ZONES[zone_id],pose,capsules)
						var contact: Vector3 = zone.p+Vector3(zone.normal)*Simulation.ATTACH_OFFSET
						var radius := Vector2(contact.x,contact.z).length()+ArenaData.MOSQUITO_RADIUS
						metrics.maximum_carry_reservation_radius=maxf(metrics.maximum_carry_reservation_radius,radius)
						check(radius<=ArenaData.HUMAN_RADIUS+.00001,"carried forearm reservation fits body "+tool+" crouch="+str(crouch)+" run="+str(running)+" frame="+str(frame)+" zone="+str(zone_id))

func _bone_change(skin: Node3D, name: String) -> Transform3D:
	var id: int = skin.bone_ids[name]
	return skin.skeleton.get_bone_global_pose(id)*skin.skeleton.get_bone_global_rest(id).affine_inverse()

func _skin_geometry() -> void:
	metrics.maximum_palm_contact_error=0.0
	metrics.maximum_digit_length_error=0.0
	metrics.minimum_neutral_curl=INF
	var skin := CharacterSkin.new()
	root.add_child(skin)
	skin.setup("human")
	for first_person: bool in [false,true]:
		skin.set_first_person(first_person)
		for side: String in ["l","r"]:
			var hand := "hand_"+side
			var wrist := _point(skin.contract[hand].from)
			var long_axis := (_point(skin.contract[hand].to)-wrist).normalized()
			# Infer palm from the imported thumb's handedness, independently of
			# the animation's width/normal helper and camera visibility.
			var thumb := _point(skin.contract["thumb_a_"+side].from)-wrist
			var side_sign := -1.0 if side=="l" else 1.0
			var rest_palm := -side_sign*long_axis.cross(thumb).normalized()
			for state: String in ["rest","slap","grasp_swatter","grasp_racket","grasp_newspaper","grasp_broom","grasp_slipper","grasp_early","grasp_mid"]:
				var grasping := state.begins_with("grasp_")
				var tool := state.trim_prefix("grasp_") if Tools.GRASPS.has(state.trim_prefix("grasp_")) else "newspaper"
				var data := {"tool":tool if grasping else "hands"}
				var target := Vector3(-.1 if side=="l" else .1,1.05,-.6)
				if state=="slap": data.strike={"active":true,"tool":"hands","hand":"left" if side=="l" else "right","point":target,"normal":Vector3.BACK,"progress":.45}
				var pose := Pose.sample(data)
				if grasping: skin.apply_human(Pose.sample({"tool":"hands"}),{"tool":"hands"},1.0)
				var dt := .018 if state=="grasp_early" else .043 if state=="grasp_mid" else 1.0
				skin.apply_human(pose,data,dt)
				var change := _bone_change(skin,hand)
				var palm := (change.basis*rest_palm).normalized()
				var fingers := (change.basis*long_axis).normalized()
				check(change.basis.determinant()>0.0,"imported hand preserves handedness "+side)
				if state=="slap":
					var actual_centre := change*(wrist+long_axis*.05)
					var error := actual_centre.distance_to(target)
					metrics.maximum_palm_contact_error=maxf(metrics.maximum_palm_contact_error,error)
					check(error<.00002,"imported palm reaches target in POV "+str(first_person)+" "+side)
					check(palm.dot(Vector3.FORWARD)>.9999,"imported palmar side strikes target "+side)
				for finger: int in range(4):
					var a := "finger%d_a_%s"%[finger,side]
					var b := "finger%d_b_%s"%[finger,side]
					var ca := _bone_change(skin,a)
					var cb := _bone_change(skin,b)
					var base := ca*_point(skin.contract[a].from)
					var joint := ca*_point(skin.contract[a].to)
					var tip := cb*_point(skin.contract[b].to)
					check(joint.distance_to(cb*_point(skin.contract[b].from))<.00002,"connected imported falanges "+a)
					for name: String in [a,b]:
						var authored := _point(skin.contract[name].to)-_point(skin.contract[name].from)
						var actual := _bone_change(skin,name).basis*authored
						var error := absf(actual.length()-authored.length())
						metrics.maximum_digit_length_error=maxf(metrics.maximum_digit_length_error,error)
						check(error<.00002,"falange retains authored length "+name+" "+state)
					if not grasping or side=="l":
						var curl := (tip-base).dot(palm)
						metrics.minimum_neutral_curl=minf(metrics.minimum_neutral_curl,curl)
						check(curl>=-.00002,"neutral fingers flex to palm in POV "+str(first_person)+" "+a)
						check((tip-base).dot(fingers)>0.0,"fingers point beyond knuckle "+a)
	root.remove_child(skin)
	skin.free()

func _run() -> void:
	_poses()
	_carry_envelope()
	if not pose_only: _skin_geometry()
	var report := {"checks":checks,"failures":failures,"metrics":metrics,"pose_only":pose_only}
	if not report_path.is_empty():
		var output := FileAccess.open(report_path,FileAccess.WRITE)
		output.store_string(JSON.stringify(report,"\t"))
		output.close()
	print("HAND092_RESULT "+JSON.stringify(report))
	await process_frame
	quit(0 if failures.is_empty() else 1)
