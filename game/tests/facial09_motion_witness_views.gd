extends SceneTree
## Replays native exported motion witnesses. Capture checks are not visual approval.
const Character = preload("res://assets/art/characters/shared/character_skin.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const Pose = preload("res://scripts/human_pose.gd")
var checks := 0
var failures: Array[String] = []
var folder := "res://../outputs/0.9-facial-motion-witnesses/before"
var source := "res://../work/facial09-motion-consumer-witnesses.json"
var after := false
var only_case := ""
var full_garment_indices := false

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		if failures.size()<12: printerr("FACIAL09_WITNESS_FAIL "+label)

func vector(value: Array) -> Vector3:
	return Vector3(float(value[0]),float(value[1]),float(value[2]))

func _json(value: Variant) -> Variant:
	if value is Vector3: return [value.x,value.y,value.z]
	if value is Array:
		var result: Array = []
		for item: Variant in value: result.append(_json(item))
		return result
	if value is Dictionary:
		var result: Dictionary = {}
		for key: Variant in value: result[str(key)]=_json(value[key])
		return result
	return value

func _compare_mesh(mesh: MeshInstance3D, skeleton: Skeleton3D, expected: Dictionary, garment_changed: bool) -> Dictionary:
	var actual_morphs: Dictionary = {}
	for index: int in range(mesh.get_blend_shape_count()):
		var name := str(mesh.mesh.get_blend_shape_name(index))
		actual_morphs[name]=mesh.get_blend_shape_value(index)
		check(absf(mesh.get_blend_shape_value(index)-float(expected.morph_values.get(name,0.0)))<.00001,"effective morph matches "+str(mesh.name)+"/"+name+" actual="+str(mesh.get_blend_shape_value(index))+" expected="+str(expected.morph_values.get(name,0.0)))
	var native: ArrayMesh = mesh.bake_mesh_from_current_skeleton_pose()
	var morphed: ArrayMesh = mesh.bake_mesh_from_current_blend_shape_mix() if mesh.get_blend_shape_count()>0 else null
	check(native!=null,"native mesh bake exists "+str(mesh.name))
	var palette: Array[Transform3D] = []
	var to_skeleton := mesh.global_transform.affine_inverse()*skeleton.global_transform
	for index: int in range(mesh.skin.get_bind_count()):
		var bone := skeleton.find_bone(mesh.skin.get_bind_name(index))
		if bone<0: bone=mesh.skin.get_bind_bone(index)
		palette.append(to_skeleton*skeleton.get_bone_global_pose(bone)*mesh.skin.get_bind_pose(index))
	var offset := 0
	var maximum := 0.0
	for surface: int in range(mesh.mesh.get_surface_count()):
		var original: Array = mesh.mesh.surface_get_arrays(surface)
		var points: PackedVector3Array = original[Mesh.ARRAY_VERTEX]
		var base: PackedVector3Array = native.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
		var morph: PackedVector3Array = morphed.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX] if morphed!=null else points
		var binds: PackedInt32Array = original[Mesh.ARRAY_BONES]
		var weights: PackedFloat32Array = original[Mesh.ARRAY_WEIGHTS]
		var influences: int = weights.size()/points.size()
		for index: int in range(points.size()):
			var delta := Vector3.ZERO
			for influence: int in range(influences):
				var bind_index: int = binds[index*influences+influence]
				var weight: float = weights[index*influences+influence]
				if weight>0.0: delta+=palette[bind_index].basis*(morph[index]-points[index])*weight
			var actual: Vector3 = mesh.global_transform*(base[index]+delta)
			maximum=maxf(maximum,actual.distance_to(vector(expected.vertices[offset+index])))
		offset+=points.size()
	check(offset==expected.vertices.size(),"same vertex count "+str(mesh.name))
	if not garment_changed:
		check(maximum<.00015,"unchanged face/head matches export "+str(mesh.name)+" max="+str(maximum))
	return {"mesh":str(mesh.name),"compared_vertices":offset,"max_difference_from_before_m":maximum,"geometry_change_expected":garment_changed,"actual_morph_values":actual_morphs}

func _run() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--input="): source=argument.trim_prefix("--input=")
		if argument.begins_with("--output="): folder=argument.trim_prefix("--output=")
		if argument=="--after": after=true
		if argument.begins_with("--case="): only_case=argument.trim_prefix("--case=")
		if argument=="--full-garment-indices": full_garment_indices=true
	check(DisplayServer.get_name()!="headless","native renderer required")
	if not failures.is_empty(): quit(2);return
	var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(source))
	var absolute := ProjectSettings.globalize_path(folder) if folder.begins_with("res://") else folder
	DirAccess.make_dir_recursive_absolute(absolute)
	root.size=Vector2i(1200,1000)
	root.msaa_3d=Viewport.MSAA_4X
	var environment := WorldEnvironment.new()
	environment.environment=Environment.new()
	environment.environment.background_mode=Environment.BG_COLOR
	environment.environment.background_color=Color("23313c")
	environment.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color=Color("d4deed")
	environment.environment.ambient_light_energy=.65
	root.add_child(environment)
	var light := DirectionalLight3D.new()
	light.rotation_degrees=Vector3(-40,-28,0)
	light.light_energy=.75
	light.shadow_enabled=true
	root.add_child(light)
	var camera := Camera3D.new()
	root.add_child(camera);camera.make_current();camera.fov=38;camera.near=.02
	var label := Label.new()
	label.position=Vector2(20,18)
	label.add_theme_font_size_override("font_size",22)
	label.add_theme_color_override("font_shadow_color",Color.BLACK)
	label.add_theme_constant_override("shadow_offset_x",2)
	label.add_theme_constant_override("shadow_offset_y",2)
	root.add_child(label)
	var records: Array[Dictionary] = []
	var current_hashes: Dictionary = {}
	for witness: Dictionary in report.witnesses:
		if not only_case.is_empty() and str(witness.case_id)!=only_case: continue
		for path: String in witness.source_sha256:
			current_hashes[path]=FileAccess.get_sha256(path)
			if not after or path not in ["res://assets/art/characters/human/human_lms06.glb","res://assets/art/characters/human/model.json"]:
				check(current_hashes[path]==str(witness.source_sha256[path]),"source hash unchanged "+path)
		var part: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(str(witness.native_part)))
		check(FileAccess.get_sha256(str(witness.native_part))==str(witness.native_part_sha256),"native input hash")
		var native_case: Dictionary = {}
		for candidate: Dictionary in part.cases:
			if candidate.id==witness.case_id: native_case=candidate;break
		var data: Dictionary = witness.actor.duplicate(true)
		for key: String in ["p","velocity"]:
			data[key]=vector(data[key])
		var skin := Character.new();root.add_child(skin);skin.setup("human")
		var appearance: Dictionary = {}
		# JSON numeric values decode as floats; Cosmetics intentionally accepts
		# integer IDs only. Restore the wire type before selecting the witness.
		for key: String in witness.appearance_to_reproduce:
			appearance[key]=int(witness.appearance_to_reproduce[key])
		skin.set_appearance(appearance)
		skin.set_first_person(false)
		if full_garment_indices:
			for mesh: MeshInstance3D in skin.meshes:
				if str(mesh.name).begins_with("human_outfit_"): mesh.lod_bias=1000.0
		skin.facial=Facial.new(.25);skin.facial_elapsed=1.0;skin.facial_applied.clear()
		var posed := Pose.sample(data)
		for frame: int in range(12): skin.apply_human(posed,data,.10)
		# Exported hidden alternatives receive the final controller values once.
		# Clear the selected-piece 0.0001 write cache to reproduce those exact
		# weights; keep the shared runtime corrective helper.
		skin.facial_applied.clear()
		skin.apply_facial_values(witness.facial_values)
		await process_frame;await RenderingServer.frame_post_draw
		await process_frame;await RenderingServer.frame_post_draw
		var meshes: Array[Dictionary] = []
		for mesh: MeshInstance3D in skin.meshes:
			if str(mesh.name) in [str(witness.pair_a),str(witness.pair_b)]:
				check(mesh.visible,"witness pieces coexist "+str(mesh.name))
				var expected: Dictionary=part.geometries[native_case.meshes[str(mesh.name)].geometry]
				meshes.append(_compare_mesh(mesh,skin.skeleton,expected,after and str(mesh.name).begins_with("human_outfit_")))
		check(meshes.size()==2,"both selected pair meshes measured")
		var head := skin.skeleton.global_transform*skin.skeleton.get_bone_global_pose(skin.skeleton.find_bone("head"))
		var target := head.origin+Vector3(0,-.11,0)
		var dirs: Array[Vector3] = [Vector3(0,.10,-1),Vector3(.85,.10,-.65),Vector3(-.85,.10,-.65),Vector3(0,-.45,-1)]
		var names := ["front","quarter-right","quarter-left","below"]
		var captures: Array[String] = []
		for index: int in range(dirs.size()):
			camera.position=target+dirs[index].normalized()*1.06
			camera.look_at(target)
			label.text="%s · %s × %s\nPose exportada real · %s · vista %s\nEstudio Compatibility · contacto pendiente de revisión"%[witness.case_id,witness.pair_a,witness.pair_b,witness.expression,names[index]]
			await process_frame;await RenderingServer.frame_post_draw
			await process_frame;await RenderingServer.frame_post_draw
			var file := "%s-%s.png"%[witness.case_id,names[index]]
			check(root.get_texture().get_image().save_png(absolute.path_join(file))==OK,"saved "+file)
			captures.append(file)
		records.append({"case_id":witness.case_id,"a":witness.pair_a,"b":witness.pair_b,"appearance":skin.appearance,"meshes":meshes,"captures":captures})
		skin.queue_free();await process_frame
	var output := {"format":"LMS_FACIAL09_WITNESS_RENDER","checks":checks,"failures":failures,"records":records,"after":after,"full_garment_indices_diagnostic":full_garment_indices,"source_sha256":current_hashes,"visual_approved":false,"input_sha256":FileAccess.get_sha256(source),"fixture_sha256":FileAccess.get_sha256("res://tests/facial09_motion_witness_views.gd")}
	var file := FileAccess.open(absolute.path_join("witness-render.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(_json(output),"\t"));file.close()
	print("FACIAL09_WITNESS_RESULT checks=%d failures=%d captures=%d output=%s"%[checks,failures.size(),records.size()*4,absolute])
	quit(0 if failures.is_empty() else 1)
