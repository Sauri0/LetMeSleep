extends SceneTree
## Real imported A2 binding and native skeleton bake; authoring clearance has
## its own strict triangle audit. A finite pose test is not a visual approval.
const Character = preload("res://assets/art/characters/shared/character_skin.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures := 0
var maximum_error := 0.0
var records: Array[Dictionary] = []

func _initialize() -> void: _run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures <= 12: printerr("GLASSES09_NATIVE_FAIL "+label)

func _run() -> void:
	var skin := Character.new()
	root.add_child(skin)
	skin.setup("human")
	skin.set_appearance({"accessory":2})
	check(skin.skeleton.get_bone_count()==36,"36 bones preserved")
	var head_id: int = skin.skeleton.find_bone("head")
	var affected: Array[MeshInstance3D] = []
	for mesh: MeshInstance3D in skin.meshes:
		var category: String = str(mesh.name).split("_")[1]
		if category not in ["head","eyes","brows","mouth","hair","mustache","beard","accessory"]: continue
		if category=="accessory" and str(mesh.name)!="human_accessory_2": continue
		if str(mesh.name).ends_with("_capped"): continue
		check(mesh.skin!=null,"imported skin "+str(mesh.name))
		if mesh.skin==null: continue
		var rigid := true
		for surface: int in range(mesh.mesh.get_surface_count()):
			var arrays: Array = mesh.mesh.surface_get_arrays(surface)
			var bones: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
			var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
			for vertex: int in range(int(weights.size()/4)):
				var total := 0.0
				for influence: int in range(4):
					var index: int = vertex*4+influence
					if weights[index]<=.000001: continue
					var bound_bone: int = skin.skeleton.find_bone(mesh.skin.get_bind_name(bones[index]))
					if bound_bone<0: bound_bone=mesh.skin.get_bind_bone(bones[index])
					rigid = rigid and bound_bone==head_id
					total += weights[index]
				rigid = rigid and absf(total-1.0)<.00001
		check(rigid,"all imported vertices share rigid head "+str(mesh.name))
		if str(mesh.name) in ["human_accessory_2","human_head"]: affected.append(mesh)
	check(affected.size()==2,"A2 and head located")
	var reference: Dictionary = {}
	for crouch: float in [0.0,1.0]:
		for yaw: float in [-deg_to_rad(75),0.0,deg_to_rad(75)]:
			for pitch: float in [-1.92,0.0,1.30]:
				var data: Dictionary = {"state":"human","p":Vector3.ZERO,"yaw":yaw,"body_yaw":0.0,"pitch":pitch,"crouch_amount":crouch,"facial_no_blink":true}
				skin.apply_human(Pose.sample(data),data,1.0/60.0)
				for frame: int in range(2):
					await process_frame
					await RenderingServer.frame_post_draw
				var head_world: Transform3D = skin.skeleton.global_transform*skin.skeleton.get_bone_global_pose(head_id)
				for mesh: MeshInstance3D in affected:
					var baked: ArrayMesh = mesh.bake_mesh_from_current_skeleton_pose()
					var points: Array[Vector3] = []
					var to_head: Transform3D = head_world.affine_inverse()*mesh.global_transform
					for surface: int in range(baked.get_surface_count()):
						var vertices: PackedVector3Array = baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
						for vertex: Vector3 in vertices: points.append(to_head*vertex)
					var name: String = str(mesh.name)
					if not reference.has(name): reference[name] = points
					var original: Array = reference[name]
					check(points.size()==original.size(),"bake vertex count "+name)
					var error := 0.0
					for i: int in range(points.size()): error=maxf(error,points[i].distance_to(original[i]))
					maximum_error=maxf(maximum_error,error)
					check(error<.00005,"relative head/A2 geometry invariant "+name)
					records.append({"mesh":name,"crouch":crouch,"yaw":yaw,"pitch":pitch,"maximum_error_m":error,"lod_bias":mesh.lod_bias})
	var report: Dictionary = {"checks":checks,"failures":failures,"poses":18,"maximum_relative_error_m":maximum_error,"records":records,
		"scope":"Native imported head/A2 skeleton bake invariant across18 poses;17 neighbouring pieces have rigid head weights. Morph contact and visual LOD evidence are separate.",
		"source_sha256":{}}
	for path: String in ["res://assets/art/characters/human/human_lms06.glb","res://assets/art/characters/human/model.json","res://assets/art/characters/shared/character_skin.gd","res://scripts/human_pose.gd"]:
		if FileAccess.file_exists(path): report.source_sha256[path]=FileAccess.get_sha256(path)
	var output: String = ProjectSettings.globalize_path("res://../work/glasses09-native.json")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): output=arg.trim_prefix("--output=")
	var file := FileAccess.open(output,FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"));file.close()
	skin.queue_free()
	await process_frame
	print("GLASSES09_NATIVE checks=%d failures=%d poses=18 max_error=%.8f"%[checks,failures,maximum_error])
	quit(1 if failures>0 else 0)
