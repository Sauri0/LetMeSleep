extends SceneTree
## Authoring bridge: bake the gameplay pose contract into editable Blender clips.
const Pose = preload("res://scripts/human_pose.gd")

func _initialize() -> void:
	var clips: Dictionary = {}
	for label: String in ["idle","walk","run","crouch","jump","inspect","clap","tool_hold","task"]:
		var frames: Array = []
		var count: int = 36 if label not in ["clap","jump"] else 18
		for frame: int in range(count+1):
			var t: float = float(frame)/count
			var pulse: float = sin(t*PI)
			var actor: Dictionary = {"p":Vector3.ZERO,"body_yaw":0.0,"yaw":0.0,"pitch":0.0,"grounded":true,"tool":"hands"}
			if label in ["walk","run"]:
				actor.motion_speed = 5.2 if label=="run" else 3.1
				actor.motion_phase = t*TAU
				actor.sprinting = label=="run"
			elif label=="crouch": actor.crouch_amount = smoothstep(0.0,1.0,pulse)
			elif label=="jump":
				actor.grounded = frame==0 or frame==count
				actor.velocity = Vector3(0,cos(t*PI)*2.0,0)
			elif label=="inspect":
				actor.pitch = -1.3*pulse
				actor.yaw = 0.65*sin(t*TAU)*pulse
			elif label=="tool_hold": actor.tool = "swatter"
			elif label in ["clap","task"]:
				actor.tool = "hands" if label=="clap" else "swatter"
				actor.strike = {"id":1,"active":t<0.999,"hand":"left" if label=="clap" else "right","tool":actor.tool,"point":Vector3(.10,1.03,-.23) if label=="clap" else Vector3(0,1.15,-.62),"origin":Pose.view_origin(actor),"progress":t,"duration":.36,"kind":"body" if label=="clap" else "map"}
			var pose: Dictionary = Pose.sample(actor)
			var sample: Dictionary = {"frame":frame+1,"root_y":pulse*.23 if label=="jump" else 0.0,"tool":actor.tool}
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
	file.store_string(JSON.stringify({"authority":"res://scripts/human_pose.gd","fps":24,"clips":clips},"\t"))
	file.close()
	print("CHARACTER_POSE_EXPORT clips=%d" % clips.size())
	quit(0)
