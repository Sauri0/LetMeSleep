extends SceneTree
## Authoring bridge for the exact runtime insect skeleton and state blends.
const Actor = preload("res://scripts/actor_view.gd")

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var clips := {}
	for label: String in ["hover","accelerate","brake","perch","focus","bite","detach","stunned","recover","rescue"]:
		var actor := Actor.new()
		root.add_child(actor)
		actor.build("mosquito","",0)
		var frames := []
		for frame: int in range(61):
			var t := float(frame)/60.0
			var state := "flying"
			var velocity := Vector3.ZERO
			if label=="accelerate": velocity = Vector3.FORWARD*3.8*smoothstep(0.0,.35,t)
			if label=="brake": velocity = Vector3.FORWARD*3.8*(1.0-smoothstep(0.0,.25,t))
			if label=="perch" and t>.15: state = "perched"
			if label=="bite": state = "biting"
			if label=="detach":
				state = "perched" if t<.20 else "flying"
				velocity = Vector3.UP*2.0*smoothstep(.20,.50,t)
			if label=="stunned" or (label=="recover" and t<.35): state = "stunned"
			actor.update_state({"p":Vector3.ZERO,"state":state,"velocity":velocity,"surface_normal":Vector3.UP if state in ["biting","perched"] else Vector3.ZERO},1.0/60.0)
			var visual := Transform3D(actor.model.basis.orthonormalized(),actor.model.position/Actor.MOSQUITO_VISUAL_SCALE)
			var bones := {}
			var skeleton: Skeleton3D = actor.imported_skin.skeleton
			for id: int in range(skeleton.get_bone_count()):
				var pose: Transform3D = visual*skeleton.get_bone_global_pose(id)
				bones[skeleton.get_bone_name(id)] = {"p":[pose.origin.x,pose.origin.y,pose.origin.z],"basis":[pose.basis.x.x,pose.basis.x.y,pose.basis.x.z,pose.basis.y.x,pose.basis.y.y,pose.basis.y.z,pose.basis.z.x,pose.basis.z.y,pose.basis.z.z]}
			frames.append({"frame":frame+1,"bones":bones})
		clips[label] = frames
		actor.free()
	var file := FileAccess.open("res://../art_source/characters/mosquito/authoritative_pose_clips.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"authority":"actor_view.gd + shared/character_skin.gd","fps":60,"clips":clips},"\t"))
	file.close()
	print("MOSQUITO_POSE_EXPORT clips=%d"%clips.size())
	quit()
