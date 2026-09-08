extends SceneTree
## Authoring bridge: bake the gameplay pose contract into editable Blender clips.
const Pose = preload("res://scripts/human_pose.gd")

func _initialize() -> void:
	var clips: Dictionary = {}
	for label: String in ["idle","walk","run","lobby_walk","crouch","jump","land","turn","inspect","clap","tool_hold","task"]:
		var frames: Array = []
		var seconds := 1.0
		if label=="idle": seconds = 6.0
		if label in ["walk","lobby_walk"]: seconds = 1.15/3.1
		if label=="run": seconds = 1.55/5.0
		if label=="clap" or label=="task": seconds = .36
		if label=="jump": seconds = .77
		if label=="land": seconds = .30
		var count: int = maxi(6,roundi(seconds*60.0))
		for frame: int in range(count+1):
			var t: float = float(frame)/count
			var pulse: float = sin(t*PI)
			var actor: Dictionary = {"p":Vector3.ZERO,"body_yaw":0.0,"yaw":0.0,"pitch":0.0,"grounded":true,"tool":"hands"}
			actor.pose_time = t*seconds
			actor.relaxed_pose = label in ["idle","lobby_walk"]
			if label in ["walk","run","lobby_walk"]:
				actor.motion_speed = 5.0 if label=="run" else 3.1
				actor.motion_blend = 1.0
				actor.motion_phase = t*TAU
				actor.sprinting = label=="run"
			elif label=="crouch": actor.crouch_amount = smoothstep(0.0,1.0,pulse)
			elif label=="jump":
				actor.grounded = frame==0 or frame==count
				actor.velocity = Vector3(0,cos(t*PI)*2.0,0)
				actor.air_blend = smoothstep(0,.10,t)*(1.0-smoothstep(.90,1.0,t))
			elif label=="land":
				actor.air_blend = 1.0-smoothstep(0.0,.35,t)
				actor.land_blend = 1.0-t
			elif label=="turn":
				actor.yaw = t*TAU
				actor.body_yaw = t*TAU-.25*sin(t*PI)
				actor.motion_blend = .35
				actor.motion_phase = t*TAU
			elif label=="inspect":
				actor.pitch = -1.3*pulse
				actor.yaw = 0.65*sin(t*TAU)*pulse
			elif label=="tool_hold": actor.tool = "swatter"
			elif label in ["clap","task"]:
				actor.tool = "hands" if label=="clap" else "swatter"
				actor.strike = {"id":1,"active":t<0.999,"hand":"left" if label=="clap" else "right","tool":actor.tool,"point":Vector3(.10,1.03,-.23) if label=="clap" else Vector3(0,1.15,-.62),"origin":Pose.view_origin(actor),"progress":t,"duration":.36,"kind":"body" if label=="clap" else "map"}
			var pose: Dictionary = Pose.sample(actor)
			var sample: Dictionary = {"frame":frame+1,"root_y":maxf(0.0,4.6*t*seconds-6.0*pow(t*seconds,2.0)) if label=="jump" else 0.0,"tool":actor.tool}
			for key: String in pose:
				var value: Variant = pose[key]
				if value is Vector3: sample[key] = [value.x,value.y,value.z]
				elif value is Basis:
					var q: Quaternion = value.get_rotation_quaternion()
					sample[key] = [q.x,q.y,q.z,q.w]
				elif value is float or value is int: sample[key] = value
			frames.append(sample)
		clips[label] = frames
	var path: String = ProjectSettings.globalize_path("res://../art_source/characters/human/authoritative_pose_clips.json")
	var file := FileAccess.open(path,FileAccess.WRITE)
	file.store_string(JSON.stringify({"authority":"res://scripts/human_pose.gd","fps":60,"clips":clips},"\t"))
	file.close()
	print("CHARACTER_POSE_EXPORT clips=%d" % clips.size())
	quit(0)
