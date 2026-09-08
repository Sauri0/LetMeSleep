extends SceneTree
## Native geometry witness, not an intersection verdict. No preferences or game
## runtime are changed. All variants are exported with explicit coexistence rules.
## Godot 4.5 skeleton baking ignores blend shapes, so morph deltas are skinned
## separately with imported bind weights. Every base vertex is checked against
## the native skeleton bake before that palette is used for its morph delta.
const Character = preload("res://assets/art/characters/shared/character_skin.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Mosquito = preload("res://scripts/mosquito_pose.gd")
const BAKE_TOLERANCE_M := .00015
var output_path := "res://../work/facial-parts08-motion.json"
var roles: Array[String] = ["human","mosquito"]
var case_limit := 0
var case_start := 0
var checks := 0
var failures: Array[String] = []
var max_base_error_m := 0.0
var compared_vertices := 0
var geometry_vertices := 0
var topologies: Dictionary = {}
var geometries: Dictionary = {}
var cases: Array[Dictionary] = []
var topology_arrays: Dictionary = {}
var geometry_cache: Dictionary = {}

func _source_hashes() -> Dictionary:
	var result: Dictionary = {}
	for path: String in ["res://assets/art/characters/shared/character_skin.gd","res://assets/art/characters/shared/facial_expression.gd","res://scripts/human_pose.gd","res://scripts/mosquito_pose.gd","res://assets/art/characters/human/human_lms06.glb","res://assets/art/characters/mosquito/mosquito_lms06.glb","res://assets/art/characters/human/model.json","res://assets/art/characters/mosquito/model.json"]:
		result[path]=FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "source not present in exported pack"
	return result

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, message: String) -> bool:
	checks += 1
	if not value:
		failures.append(message)
		if failures.size()<=12: print("FACIAL_MOTION08_FAIL ",message)
	return value

func _v(point: Vector3) -> Array:
	return [snappedf(point.x,.0000001),snappedf(point.y,.0000001),snappedf(point.z,.0000001)]

func _json(value: Variant) -> Variant:
	if value is Vector3: return _v(value)
	if value is Basis: return [_v(value.x),_v(value.y),_v(value.z)]
	if value is Transform3D: return {"origin":_v(value.origin),"basis":_json(value.basis)}
	if value is Dictionary:
		var result: Dictionary = {}
		for key: Variant in value: result[str(key)] = _json(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item: Variant in value: result.append(_json(item))
		return result
	return value

func _category(name: String) -> String:
	return str(name.split("_")[1])

func _variant(name: String) -> int:
	var pieces := name.split("_")
	return int(pieces[2]) if pieces.size()>2 and pieces[2].is_valid_int() else -1

func _relevant(role: String, name: String) -> bool:
	if role=="human": return _category(name) in ["head","outfit","mouth","mustache","beard"]
	return _category(name) in ["core","hair","eyes","brows","mouth","accessory"]

func _binds(mesh: MeshInstance3D, skeleton: Skeleton3D) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if not check(mesh.skin!=null,"imported skin exists: "+str(mesh.name)): return result
	for index: int in range(mesh.skin.get_bind_count()):
		var name := str(mesh.skin.get_bind_name(index))
		var bone := skeleton.find_bone(name) if not name.is_empty() else mesh.skin.get_bind_bone(index)
		if not check(bone>=0 and bone<skeleton.get_bone_count(),"bind resolves: "+str(mesh.name)+"/"+str(index)): continue
		result.append({"bone":bone,"name":skeleton.get_bone_name(bone),"pose":mesh.skin.get_bind_pose(index)})
	return result

func _register(mesh: MeshInstance3D, skeleton: Skeleton3D) -> void:
	var name := str(mesh.name)
	var binds := _binds(mesh,skeleton)
	var surfaces: Array[Dictionary] = []
	var topology := {"category":_category(name),"variant":_variant(name),"vertex_count":0,"indices":[],"surfaces":[],"binds":_json(binds),"vertex_bind_indices":[],"vertex_weights":[]}
	var start := 0
	for surface: int in range(mesh.mesh.get_surface_count()):
		var arrays: Array = mesh.mesh.surface_get_arrays(surface)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var bone_indices: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
		var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
		var indices := PackedInt32Array()
		if arrays[Mesh.ARRAY_INDEX]!=null: indices=arrays[Mesh.ARRAY_INDEX]
		if indices.is_empty():
			for index: int in range(vertices.size()): indices.append(index)
		check(mesh.mesh.surface_get_primitive_type(surface)==Mesh.PRIMITIVE_TRIANGLES,"triangular surface "+name)
		check(indices.size()%3==0,"complete triangles "+name)
		var influences: int = weights.size()/maxi(vertices.size(),1)
		check(influences in [4,8] and bone_indices.size()==weights.size(),"imported vertex weights "+name)
		var material := mesh.mesh.surface_get_material(surface)
		topology.surfaces.append({"index":surface,"material":str(material.resource_name) if material!=null else "","vertex_start":start,"vertex_count":vertices.size(),"triangle_start":topology.indices.size()/3,"triangle_count":indices.size()/3})
		for index: int in indices: topology.indices.append(index+start)
		for index: int in range(vertices.size()):
			var vertex_binds: Array[int] = []
			var vertex_weights: Array[float] = []
			var total := 0.0
			for influence: int in range(influences):
				var weight := float(weights[index*influences+influence])
				var bind_index := int(bone_indices[index*influences+influence])
				if weight<=0.0: continue
				check(bind_index>=0 and bind_index<binds.size(),"vertex bind in range "+name)
				vertex_binds.append(bind_index);vertex_weights.append(weight);total+=weight
			check(absf(total-1.0)<.001,"normalized imported vertex "+name)
			topology.vertex_bind_indices.append(vertex_binds)
			topology.vertex_weights.append(vertex_weights)
		surfaces.append({"arrays":arrays,"vertices":vertices,"bones":bone_indices,"weights":weights,"influences":influences})
		start += vertices.size()
	topology.vertex_count=start
	topologies[name]=topology
	topology_arrays[name]={"binds":binds,"surfaces":surfaces}

func _palette(mesh: MeshInstance3D, skeleton: Skeleton3D) -> Array[Transform3D]:
	var result: Array[Transform3D] = []
	var mesh_to_skeleton := mesh.global_transform.affine_inverse()*skeleton.global_transform
	for bind: Dictionary in topology_arrays[str(mesh.name)].binds:
		result.append(mesh_to_skeleton*skeleton.get_bone_global_pose(int(bind.bone))*Transform3D(bind.pose))
	return result

func _skin_point(point: Vector3, vertex: int, surface: Dictionary, palette: Array[Transform3D], delta_only: bool = false) -> Vector3:
	var result := Vector3.ZERO
	var influences: int = int(surface.influences)
	for influence: int in range(influences):
		var weight := float(surface.weights[vertex*influences+influence])
		if weight<=0.0: continue
		var bind_index: int = int(surface.bones[vertex*influences+influence])
		if bind_index<0 or bind_index>=palette.size(): continue
		result += (palette[bind_index].basis*point if delta_only else palette[bind_index]*point)*weight
	return result

func _morph_values(mesh: MeshInstance3D) -> Dictionary:
	var result: Dictionary = {}
	for index: int in range(mesh.get_blend_shape_count()): result[str(mesh.mesh.get_blend_shape_name(index))]=mesh.get_blend_shape_value(index)
	return result

func _geometry(mesh: MeshInstance3D, skeleton: Skeleton3D) -> String:
	var name := str(mesh.name)
	var palette := _palette(mesh,skeleton)
	var morphs := _morph_values(mesh)
	var signature: Array = [mesh.global_transform,palette,morphs]
	var cache_id := name+"-"+str(hash(signature))
	for previous: Dictionary in geometry_cache.get(cache_id,[]):
		if previous.signature==signature: return previous.id
	var native: ArrayMesh = mesh.bake_mesh_from_current_skeleton_pose()
	if not check(native!=null,"native skeleton bake "+name): return ""
	var morphed: ArrayMesh
	if mesh.get_blend_shape_count()>0:
		morphed=mesh.bake_mesh_from_current_blend_shape_mix()
		if not check(morphed!=null,"native morph bake "+name): return ""
	var points: Array = []
	var local_max := 0.0
	var source: Array = topology_arrays[name].surfaces
	for surface_index: int in range(source.size()):
		var surface: Dictionary = source[surface_index]
		var original: PackedVector3Array = surface.vertices
		var baked: PackedVector3Array = native.surface_get_arrays(surface_index)[Mesh.ARRAY_VERTEX]
		var morph_points: PackedVector3Array = morphed.surface_get_arrays(surface_index)[Mesh.ARRAY_VERTEX] if morphed!=null else original
		if not check(original.size()==baked.size() and original.size()==morph_points.size(),"vertex ordering/count retained "+name): continue
		for index: int in range(original.size()):
			var cpu := mesh.global_transform*_skin_point(original[index],index,surface,palette)
			var world := mesh.global_transform*baked[index]
			local_max=maxf(local_max,cpu.distance_to(world))
			world+=mesh.global_transform.basis*_skin_point(morph_points[index]-original[index],index,surface,palette,true)
			points.append(_v(world))
			compared_vertices+=1
	max_base_error_m=maxf(max_base_error_m,local_max)
	check(local_max<=BAKE_TOLERANCE_M,"CPU bind palette agrees with native bake "+name+" gap="+str(local_max))
	var id := "geometry-"+str(geometries.size())
	geometries[id]={"mesh":name,"vertices":points,"morph_values":morphs,"native_base_max_error_m":local_max}
	geometry_vertices+=points.size()
	var entries: Array = geometry_cache.get(cache_id,[])
	entries.append({"signature":signature.duplicate(true),"id":id});geometry_cache[cache_id]=entries
	return id

func _pairs(role: String, names: Array[String]) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for a: String in names:
		if _category(a)!=("outfit" if role=="human" else "hair"): continue
		for b: String in names:
			if _category(b) not in (["head","mouth","mustache","beard"] if role=="human" else ["eyes","brows","mouth","accessory","core"]): continue
			result.append({"a":a,"b":b,"relation":"head_vs_garment" if role=="human" else "antenna_vs_head_piece"})
	return result

func _human_cases() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var postures: Array[Dictionary] = [
		{"label":"stand_breath_high","crouch_amount":0.0,"pose_time":PI/(2.0*1.7)},
		{"label":"stand_breath_low","crouch_amount":0.0,"pose_time":3.0*PI/(2.0*1.7)},
		{"label":"crouch_breath_high","crouch_amount":1.0,"pose_time":PI/(2.0*1.7)},
		{"label":"crouch_breath_low","crouch_amount":1.0,"pose_time":3.0*PI/(2.0*1.7)},
	]
	for phase: float in [0.0,PI*.5,PI,PI*1.5]: postures.append({"label":"run_"+str(roundi(rad_to_deg(phase))),"motion_phase":phase,"motion_speed":5.0,"motion_blend":1.0,"sprinting":true})
	postures.append({"label":"crouch_landing","crouch_amount":1.0,"land_blend":1.0,"motion_blend":1.0,"motion_phase":0.0})
	for posture: Dictionary in postures:
		for yaw: float in [-Pose.VIEW_YAW_LIMIT,0.0,Pose.VIEW_YAW_LIMIT]:
			for pitch: float in [Pose.HUMAN_PITCH_MIN,0.0,Pose.HUMAN_PITCH_MAX]:
				for expression: String in ["neutral","effort","impact"]:
					var data: Dictionary={"p":Vector3.ZERO,"state":"human","alive":true,"body_yaw":0.0,"yaw":yaw,"pitch":pitch,"grounded":true,"velocity":Vector3.ZERO,"motion_speed":0.0,"motion_blend":0.0,"motion_phase":0.0,"air_blend":0.0,"land_blend":0.0,"pose_time":0.0,"tool":"hands","preview_only":true,"facial_preview":expression,"facial_no_blink":true}
					data.merge(posture,true);data.erase("label")
					result.append({"id":"human-"+str(result.size()),"label":str(posture.label),"expression":expression,"actor":data})
	return result

func _mosquito_cases() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for state: String in ["flying","biting","stunned"]:
		for phase_sign: float in [-1.0,1.0]:
			for speed: float in [0.0,3.8]:
				for expression: String in ["neutral","alert","impact"]:
					var target_time := (PI*.5 if phase_sign>0 else PI*1.5)/2.2
					var data: Dictionary={"p":Vector3.ZERO,"state":state,"alive":true,"yaw":0.0,"pitch":0.0,"velocity":Vector3(speed,0,0),"surface_normal":Vector3.FORWARD if state=="biting" else Vector3.ZERO,"preview_only":true,"facial_preview":expression,"facial_no_blink":true}
					result.append({"id":"mosquito-"+str(result.size()),"label":state,"expression":expression,"actor":data,"clock_time":target_time,"stun":1.0 if state=="stunned" else 0.0,"transition":false})
	# Mid-transition states are explicitly fixture-preconditioned; the exact bone
	# state is exported, so these samples are never mistaken for a time integral.
	for state: String in ["biting","stunned","flying"]:
		for phase_sign: float in [-1.0,1.0]:
			var data: Dictionary={"p":Vector3.ZERO,"state":state,"alive":true,"yaw":0.0,"pitch":0.0,"velocity":Vector3(3.8,0,0),"surface_normal":Vector3.FORWARD if state=="biting" else Vector3.ZERO,"preview_only":true,"facial_preview":"impact","facial_no_blink":true}
			result.append({"id":"mosquito-"+str(result.size()),"label":state+"_transition","expression":"impact","actor":data,"clock_time":(PI*.5 if phase_sign>0 else PI*1.5)/2.2,"stun":.5,"transition":true})
	return result

func _capture(skin: Node3D, spec: Dictionary, names: Array[String]) -> void:
	var role: String = skin.species
	var data: Dictionary = spec.actor
	skin.facial=Facial.new(.25)
	skin.facial_elapsed=1.0;skin.facial_applied.clear()
	if role=="human":
		skin.transform=Transform3D.IDENTITY
		var posed := Pose.sample(data)
		for frame: int in range(12): skin.apply_human(posed,data,.10)
	else:
		skin.basis=Mosquito.orientation(data).scaled(Vector3.ONE*Mosquito.VISUAL_SCALE)
		skin.position=Vector3.ZERO
		for frame: int in range(12): skin._animate_face(data,.10)
		skin.flight_blend=.5 if bool(spec.transition) else (1.0 if str(data.state)=="flying" else 0.0)
		skin.bite_blend=.5 if bool(spec.transition) else (1.0 if str(data.state)=="biting" else 0.0)
		skin.insect_previous_time=float(spec.clock_time)
		skin.apply_mosquito(data,float(spec.clock_time),float(spec.stun))
	# Selection visibility is recorded unchanged; apply the same current channels
	# to alternative pieces solely to bake their geometry for compatible pairs.
	var original_visibility: Dictionary = {}
	for mesh: MeshInstance3D in skin.face_channels:
		original_visibility[mesh]=mesh.visible
		if str(mesh.name) in names: mesh.visible=true
	skin.apply_facial_values(skin.facial_values)
	for mesh: MeshInstance3D in original_visibility: mesh.visible=original_visibility[mesh]
	await process_frame
	await RenderingServer.frame_post_draw
	var mesh_states: Dictionary = {}
	for mesh: MeshInstance3D in skin.meshes:
		if str(mesh.name) in names: mesh_states[str(mesh.name)]={"geometry":_geometry(mesh,skin.skeleton),"visible":mesh.visible}
	var bones: Dictionary = {}
	for index: int in range(skin.skeleton.get_bone_count()): bones[skin.skeleton.get_bone_name(index)]=_json(skin.skeleton.global_transform*skin.skeleton.get_bone_global_pose(index))
	var record: Dictionary=spec.duplicate(true)
	record.actor=_json(data);record.role=role;record.meshes=mesh_states;record.bones=bones
	record.appearance=skin.appearance.duplicate(true);record.facial_values=skin.facial_values.duplicate(true)
	record.skin_transform=_json(skin.global_transform)
	if role=="mosquito":
		record.flight_blend=skin.flight_blend;record.bite_blend=skin.bite_blend
		record.antenna_angle_before_side_sign=(sin(float(spec.clock_time)*2.2)*.07+clampf(Vector3(data.velocity).length()/3.8,0,1)*.08)*(1.0-float(spec.stun))
	cases.append(record)
	if cases.size()%20==0: print("FACIAL_MOTION08_PROGRESS cases=",cases.size()," geometries=",geometries.size())

func _run() -> void:
	var started_usec := Time.get_ticks_usec()
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_path=argument.trim_prefix("--output=")
		if argument.begins_with("--roles="):
			roles.clear()
			for role: String in argument.trim_prefix("--roles=").split(","):
				if role in ["human","mosquito"]: roles.append(role)
		if argument.begins_with("--case-limit="): case_limit=maxi(0,int(argument.trim_prefix("--case-limit=")))
		if argument.begins_with("--case-count="): case_limit=maxi(0,int(argument.trim_prefix("--case-count=")))
		if argument.begins_with("--case-start="): case_start=maxi(0,int(argument.trim_prefix("--case-start=")))
	if DisplayServer.get_name()=="headless":
		print("FACIAL_MOTION08_FAIL Native renderer required; headless would not register the skin bake.")
		quit(2);return
	root.size=Vector2i(320,240)
	var sources_before := _source_hashes()
	var camera:=Camera3D.new();root.add_child(camera);camera.position=Vector3(0,1,-3);camera.make_current()
	var compatible_pairs: Dictionary = {}
	var expected_case_ids: Array[String] = []
	var expected_totals := {"human":_human_cases().size(),"mosquito":_mosquito_cases().size()}
	var requested_ranges: Dictionary = {}
	check(not roles.is_empty(),"at least one valid role requested")
	for role: String in roles:
		var skin:=Character.new();root.add_child(skin);skin.setup(role)
		skin.set_appearance({"eyes":0,"brows":0,"mouth":0,"outfit":0,"hair":0,"accessory":0,"footwear":0,"mustache":1,"beard":1})
		skin.set_first_person(false)
		var names: Array[String] = []
		for mesh: MeshInstance3D in skin.meshes:
			if _relevant(role,str(mesh.name)): names.append(str(mesh.name));_register(mesh,skin.skeleton)
		compatible_pairs[role]=_pairs(role,names)
		await process_frame;await process_frame
		var specifications: Array[Dictionary] = _human_cases() if role=="human" else _mosquito_cases()
		var last := mini(specifications.size(),case_start+case_limit) if case_limit>0 else specifications.size()
		requested_ranges[role]={"start":case_start,"end_exclusive":last,"total":specifications.size()}
		check(case_start<last,"requested role span is nonempty "+role)
		for index: int in range(case_start,last):
			var spec: Dictionary = specifications[index]
			expected_case_ids.append(str(spec.id))
			await _capture(skin,spec,names)
		skin.queue_free();await process_frame
	var sources := _source_hashes()
	check(sources==sources_before,"source and GLB hashes unchanged throughout export")
	var actual_case_ids: Array[String] = []
	for record: Dictionary in cases: actual_case_ids.append(str(record.id))
	check(actual_case_ids==expected_case_ids,"every requested case exported exactly once in order")
	var report: Dictionary={"format":"LMS_FACIAL_MOTION_08","version":1,"units":"metres","source_sha256":sources,"topologies":topologies,"geometries":geometries,"cases":cases,"compatible_pairs":compatible_pairs,"selection_rules":{"one_variant_per_category":true,"outfit_trim_uses_same_outfit_index":true,"mustache_beard_independent":true,"none_has_no_mesh":true,"colors_geometry_invariant":true},"summary":{"cases":cases.size(),"geometries":geometries.size(),"vertices":geometry_vertices,"native_compared_vertices":compared_vertices,"max_base_error_m":max_base_error_m,"base_tolerance_m":BAKE_TOLERANCE_M,"checks":checks,"failures":failures,"export_complete":failures.is_empty() and case_limit==0,"case_limit":case_limit},"limits":["Exports actual selected GLB triangles and imported skin weights at finite conservative poses; does not assert nonintersection.","Alternatives exported together are not simultaneously visible. Only compatible_pairs are requested collision relations.","Morphs come from a seeded controller after 1.2 seconds of explicit preview preconditioning; separate morph-domain audit remains necessary.","Transition blends are explicitly preconditioned witnesses, not recorded spontaneous gameplay.","Finite motion samples do not prove continuous clearance between samples."]}
	report.coverage={"expected_totals":expected_totals,"requested_ranges":requested_ranges,"case_ids":actual_case_ids,"span_complete":failures.is_empty()}
	report.summary.export_complete=failures.is_empty() and roles.size()==2 and cases.size()==int(expected_totals.human)+int(expected_totals.mosquito)
	report.summary.case_start=case_start
	report.summary.elapsed_before_serialization_seconds=float(Time.get_ticks_usec()-started_usec)/1000000.0
	var absolute:=ProjectSettings.globalize_path(output_path) if output_path.begins_with("res://") else output_path
	DirAccess.make_dir_recursive_absolute(absolute.get_base_dir())
	var file:=FileAccess.open(absolute,FileAccess.WRITE)
	if file==null:
		print("FACIAL_MOTION08_FAIL cannot write ",absolute);quit(2);return
	file.store_string(JSON.stringify(report));file.close()
	var byte_count: int = FileAccess.open(absolute,FileAccess.READ).get_length()
	var index_report: Dictionary={"format":"LMS_FACIAL_MOTION_08_PART","version":1,"file":absolute,"sha256":FileAccess.get_sha256(absolute),"bytes":byte_count,"source_sha256":sources,"coverage":report.coverage,"summary":report.summary,"elapsed_seconds":float(Time.get_ticks_usec()-started_usec)/1000000.0}
	var index_file:=FileAccess.open(absolute+".index.json",FileAccess.WRITE)
	if index_file==null:
		print("FACIAL_MOTION08_FAIL cannot write part index");quit(2);return
	index_file.store_string(JSON.stringify(index_report,"\t"));index_file.close()
	print("FACIAL_MOTION08_RESULT cases=%d geometries=%d vertices=%d compared=%d max_error_m=%.8f checks=%d failures=%d output=%s"%[cases.size(),geometries.size(),geometry_vertices,compared_vertices,max_base_error_m,checks,failures.size(),absolute])
	camera.queue_free();await process_frame
	quit(0 if failures.is_empty() else 1)
