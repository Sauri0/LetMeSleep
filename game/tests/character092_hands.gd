extends SceneTree
## Real imported skeleton and native hand views. Requires the paired HumanPose092.
const Actor = preload("res://scripts/actor_view.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Tools = preload("res://scripts/tool_catalog.gd")
var output := ""
var skin_source := ""
var capture := false
var full_mesh := false
var shadows := true
var only := ""
var audit_mesh := false
var mesh_records: Array[Dictionary]=[]
var checks := 0
var failures: Array[String] = []
var records: Array[Dictionary] = []
var max_length_error := 0.0
var max_joint_gap := 0.0
var minimum_neutral_curl := INF
var max_flexion := 0.0
var camera: Camera3D
var actor: Node3D

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output=argument.trim_prefix("--output=")
		if argument.begins_with("--skin-source="): skin_source=argument.trim_prefix("--skin-source=")
		if argument=="--capture": capture=true
		if argument=="--full-mesh": full_mesh=true
		if argument=="--no-shadows": shadows=false
		if argument.begins_with("--only="): only=argument.trim_prefix("--only=")
		if argument=="--audit-mesh": audit_mesh=true
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks+=1
	if not value:
		failures.append(label)
		if failures.size()<20: printerr("CHARACTER092_HAND_FAIL "+label)

func point(value: Array) -> Vector3:
	return Vector3(value[0],value[1],value[2])

func bone_change(skin: Node3D, name: String) -> Transform3D:
	var id: int=skin.bone_ids[name]
	return skin.skeleton.get_bone_global_pose(id)*skin.skeleton.get_bone_global_rest(id).affine_inverse()

func inspect_hand(skin: Node3D, side: String, neutral: bool, opened: bool, label: String) -> Array:
	var wrist := point(skin.contract["hand_"+side].from)
	var longitudinal := (point(skin.contract["hand_"+side].to)-wrist).normalized()
	var thumb := point(skin.contract["thumb_a_"+side].from)-wrist
	var sign := -1.0 if side=="l" else 1.0
	# Derive palm from the thumb, independently of CharacterSkin's cross helper.
	var rest_palm := -sign*longitudinal.cross(thumb).normalized()
	var palm_change := bone_change(skin,"hand_"+side)
	var palm := (palm_change.basis*rest_palm).normalized()
	var chain_points: Array=[]
	for digit: String in ["finger0","finger1","finger2","finger3","thumb"]:
		var a := digit+"_a_"+side
		var b := digit+"_b_"+side
		var ca := bone_change(skin,a)
		var cb := bone_change(skin,b)
		var base := ca*point(skin.contract[a].from)
		var joint := ca*point(skin.contract[a].to)
		var distal_root := cb*point(skin.contract[b].from)
		var tip := cb*point(skin.contract[b].to)
		var gap := joint.distance_to(distal_root)
		max_joint_gap=maxf(max_joint_gap,gap)
		check(gap<.00002,label+" connected "+a)
		for name: String in [a,b]:
			var change := ca if name==a else cb
			var rest := point(skin.contract[name].to)-point(skin.contract[name].from)
			var error := absf((change.basis*rest).length()-rest.length())
			max_length_error=maxf(max_length_error,error)
			check(error<.00002,label+" authored length "+name)
			check(change.is_finite() and absf(change.basis.determinant()-1.0)<.0001,label+" rigid proper transform "+name)
		var flexion := (joint-base).angle_to(tip-joint)
		max_flexion=maxf(max_flexion,flexion)
		check(flexion<=1.851,label+" joint flexion limit "+digit)
		if neutral:
			var curl := (tip-base).dot(palm)
			minimum_neutral_curl=minf(minimum_neutral_curl,curl)
			check(curl>=-.00002,label+" neutral palmar curl "+digit)
		if opened: check(absf((tip-base).dot(palm))<.00002,label+" palmada opens "+digit)
		chain_points.append([base,joint,tip])
	return chain_points

func settle() -> void:
	for frame: int in range(3):
		await process_frame
		if capture or audit_mesh: await RenderingServer.frame_post_draw

func save_view(name: String) -> void:
	await settle()
	check(root.get_texture().get_image().save_png(output.path_join(name+".png"))==OK,"native capture "+name)

func detail_views(name: String, side: String) -> void:
	var skin: Node3D=actor.imported_skin
	var change := bone_change(skin,"hand_"+side)
	var wrist := point(skin.contract["hand_"+side].from)
	var axis := (point(skin.contract["hand_"+side].to)-wrist).normalized()
	var long_axis := (change.basis*axis).normalized()
	var thumb := point(skin.contract["thumb_a_"+side].from)-wrist
	var sign := -1.0 if side=="l" else 1.0
	var palm := (change.basis*(-sign*axis.cross(thumb).normalized())).normalized()
	var across := long_axis.cross(palm).normalized()
	var centre: Vector3=skin.skeleton.to_global(change*(wrist+axis*.048))
	camera.fov=42.0
	for view: String in ["palm","dorsum","side"]:
		var direction: Vector3={"palm":palm,"dorsum":-palm,"side":across}[view]
		camera.position=centre+direction*.32-long_axis*.015
		camera.look_at(centre,-long_axis)
		await save_view(name+"-detail-"+side+"-"+view)

func _run() -> void:
	if output.is_empty() or ((capture or audit_mesh) and DisplayServer.get_name()=="headless"):
		printerr("Provide --output=absolute_folder; --capture requires native renderer")
		quit(2);return
	DirAccess.make_dir_recursive_absolute(output)
	root.size=Vector2i(960,720)
	root.content_scale_size=root.size
	root.msaa_3d=Viewport.MSAA_4X
	var world := Node3D.new();root.add_child(world)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode=Environment.BG_COLOR
	environment.background_color=Color("233e4a")
	environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color=Color("dce7ee")
	environment.ambient_light_energy=.55
	environment_node.environment=environment;world.add_child(environment_node)
	var light := DirectionalLight3D.new();light.rotation_degrees=Vector3(-48,-35,0)
	light.light_energy=1.0;light.shadow_enabled=shadows;world.add_child(light)
	camera=Camera3D.new();world.add_child(camera);camera.make_current();camera.near=.015
	actor=Actor.new();world.add_child(actor);actor.build("human","",0)
	if not skin_source.is_empty():
		var old_skin: Node3D=actor.imported_skin
		var parent := old_skin.get_parent();parent.remove_child(old_skin);old_skin.free()
		var script: Script=load(skin_source)
		actor.imported_skin=script.new();parent.add_child(actor.imported_skin)
		actor.imported_skin.setup("human")
	if full_mesh:
		for mesh: MeshInstance3D in actor.imported_skin.meshes: mesh.lod_bias=1000.0
	var cases: Array[Dictionary]=[
		{"name":"rest","tool":"hands"},
		{"name":"slap-left","tool":"hands","strike":{"active":true,"tool":"hands","hand":"left","point":Vector3(-.18,1.05,-.52),"normal":Vector3.BACK,"progress":.45}},
		{"name":"slap-right","tool":"hands","strike":{"active":true,"tool":"hands","hand":"right","point":Vector3(.18,1.05,-.52),"normal":Vector3.BACK,"progress":.45}}]
	for tool: String in Tools.GRASPS: cases.append({"name":"grasp-"+tool,"tool":tool})
	var reference: Dictionary={}
	for fps: bool in [false,true]:
		actor.set_local(fps)
		for entry: Dictionary in cases:
			if not only.is_empty() and str(entry.name) not in only.split(","): continue
			var data := {"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"pitch":-1.1,"state":"human","alive":true,"grounded":true,
				"pose_time":0.0,"motion_speed":0.0,"motion_phase":0.0,"motion_blend":0.0,"tool":entry.tool,"appearance":{"color":2}}
			if entry.has("strike"): data.strike=entry.strike
			var skin: Node3D=actor.imported_skin
			skin.hand_grip={"l":0.0,"r":0.0}
			skin.human_pose_hash=0;skin.human_pose_values={}
			var name: String=str(entry.name)+("-fps" if fps else "-third")
			var final_points: Array=[]
			for frame: int in range(33):
				actor.update_state(data,1.0/60.0)
				final_points=[]
				for side: String in ["l","r"]:
					var opened: bool=entry.has("strike") and str(entry.strike.hand)==("left" if side=="l" else "right")
					final_points.append(inspect_hand(skin,side,side=="l" or entry.tool=="hands",opened,name+" "+side))
				if audit_mesh and not fps and frame in [0,2,5,11,32]:
					await audit_core(name+"-frame"+str(frame))
			if fps: check(final_points==reference[entry.name],name+" identical skeleton in both POV")
			else: reference[entry.name]=final_points.duplicate(true)
			records.append({"name":name,"grip":skin.hand_grip.duplicate(),"pose":str(actor.body_pose),"chains":str(final_points)})
			if capture:
				if not fps: await dump_core(name)
				if fps:
					camera.fov=78.0;camera.position=Pose.view_origin(data);camera.rotation=Vector3(data.pitch,0,0)
				else:
					camera.fov=48.0;camera.position=Vector3(1.5,1.25,-2.0);camera.look_at(Vector3(0,1.0,-.1))
				await save_view(name)
				if str(entry.name) in ["rest","slap-left","slap-right","grasp-newspaper"]:
					await detail_views(name,"l" if entry.name=="slap-left" else "r")
	var report := {"checks":checks,"failures":failures,"records":records,"maximum_length_error_m":max_length_error,"maximum_joint_gap_m":max_joint_gap,
		"minimum_neutral_palmar_curl_m":minimum_neutral_curl,"maximum_joint_flexion_rad":max_flexion,
		"skin_sha256":FileAccess.get_sha256(skin_source if not skin_source.is_empty() else "res://assets/art/characters/shared/character_skin.gd"),
		"pose_sha256":FileAccess.get_sha256("res://scripts/human_pose.gd"),
		"full_mesh":full_mesh,"shadows":shadows,"mesh_records":mesh_records,
		"scope":"Actual imported bone transforms and fixed lengths through 33 acquisition frames for all five tools, neutral and each palmada, both POV; native production-eye and detail views. No complete mesh collision certification or performance claim."}
	var file := FileAccess.open(output.path_join("character092-hands.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"));file.close()
	print("CHARACTER092_HANDS checks=%d failures=%d max_length_error=%f max_joint_gap=%f"%[checks,failures.size(),max_length_error,max_joint_gap])
	quit(0 if failures.is_empty() else 1)

func dump_core(name: String) -> void:
	await settle()
	for mesh: MeshInstance3D in actor.imported_skin.meshes:
		if str(mesh.name)!="human_core": continue
		var baked := mesh.bake_mesh_from_current_skeleton_pose()
		var surfaces: Array=[]
		for surface: int in range(baked.get_surface_count()):
			var current := baked.surface_get_arrays(surface)
			var authored := mesh.mesh.surface_get_arrays(surface)
			var rows: Array=[]
			for i: int in range(current[Mesh.ARRAY_VERTEX].size()):
				var position: Vector3=current[Mesh.ARRAY_VERTEX][i]
				var normal: Vector3=current[Mesh.ARRAY_NORMAL][i]
				var source: Vector3=authored[Mesh.ARRAY_VERTEX][i]
				rows.append([[position.x,position.y,position.z],[normal.x,normal.y,normal.z],[source.x,source.y,source.z]])
			surfaces.append({"rows":rows,"indices":Array(current[Mesh.ARRAY_INDEX])})
		var file := FileAccess.open(output.path_join(name+"-core.json"),FileAccess.WRITE)
		file.store_string(JSON.stringify(surfaces));file.close()
	if is_instance_valid(actor.held_tool):
		var triangles: Array=[]
		for mesh: MeshInstance3D in actor.held_tool.find_children("*","MeshInstance3D",true,false):
			for vertex: Vector3 in mesh.mesh.get_faces():
				var p := mesh.global_transform*vertex
				triangles.append([p.x,p.y,p.z])
		var file := FileAccess.open(output.path_join(name+"-tool.json"),FileAccess.WRITE)
		file.store_string(JSON.stringify(triangles));file.close()

func audit_core(label: String) -> void:
	await settle()
	for mesh: MeshInstance3D in actor.imported_skin.meshes:
		if str(mesh.name)!="human_core": continue
		var baked := mesh.bake_mesh_from_current_skeleton_pose()
		var opposed := 0
		var collapsed := 0
		var minimum_dot := 1.0
		var reversed_faces := 0
		var minimum_orientation := 1.0
		var witnesses: Array=[]
		var reversed_witnesses: Array=[]
		var faces_count := 0
		var skeleton: Skeleton3D=actor.imported_skin.skeleton
		var palette: Array[Basis]=[]
		var skin_transforms: Dictionary={}
		var to_skeleton := mesh.global_transform.affine_inverse()*skeleton.global_transform
		for index: int in range(mesh.skin.get_bind_count()):
			var bone := skeleton.find_bone(mesh.skin.get_bind_name(index))
			if bone<0: bone=mesh.skin.get_bind_bone(index)
			var transform: Transform3D=to_skeleton*skeleton.get_bone_global_pose(bone)*mesh.skin.get_bind_pose(index)
			palette.append(transform.basis)
			skin_transforms[skeleton.get_bone_name(bone)]=[transform.basis.x.x,transform.basis.x.y,transform.basis.x.z,
				transform.basis.y.x,transform.basis.y.y,transform.basis.y.z,transform.basis.z.x,transform.basis.z.y,transform.basis.z.z,
				transform.origin.x,transform.origin.y,transform.origin.z]
		for surface: int in range(baked.get_surface_count()):
			var arrays := baked.surface_get_arrays(surface)
			var points: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
			var normals: PackedVector3Array=arrays[Mesh.ARRAY_NORMAL]
			var indices: PackedInt32Array=arrays[Mesh.ARRAY_INDEX]
			var original := mesh.mesh.surface_get_arrays(surface)
			var source: PackedVector3Array=original[Mesh.ARRAY_VERTEX]
			var binds: PackedInt32Array=original[Mesh.ARRAY_BONES]
			var weights: PackedFloat32Array=original[Mesh.ARRAY_WEIGHTS]
			var influences: int=weights.size()/source.size()
			var vertex_basis: Array[Basis]=[]
			for vertex: int in range(source.size()):
				var average := Basis(Vector3.ZERO,Vector3.ZERO,Vector3.ZERO)
				for influence: int in range(influences):
					var contribution: Basis=palette[binds[vertex*influences+influence]]*weights[vertex*influences+influence]
					average=Basis(average.x+contribution.x,average.y+contribution.y,average.z+contribution.z)
				vertex_basis.append(average)
			for offset: int in range(0,indices.size(),3):
				var a := indices[offset];var b := indices[offset+1];var c := indices[offset+2]
				var cross := (points[c]-points[a]).cross(points[b]-points[a])
				var authored_area := (source[c]-source[a]).cross(source[b]-source[a]).length()
				var expected_basis := Basis(vertex_basis[a].x+vertex_basis[b].x+vertex_basis[c].x,
					vertex_basis[a].y+vertex_basis[b].y+vertex_basis[c].y,
					vertex_basis[a].z+vertex_basis[b].z+vertex_basis[c].z)*(1.0/3.0)
				var source_normal := (source[c]-source[a]).cross(source[b]-source[a]).normalized()
				var expected_normal := (expected_basis.inverse().transposed()*source_normal).normalized()
				var orientation := cross.normalized().dot(expected_normal)
				minimum_orientation=minf(minimum_orientation,orientation)
				if orientation<0.0:
					reversed_faces+=1
					if reversed_witnesses.size()<12: reversed_witnesses.append({"surface":surface,"triangle":offset/3,"orientation_dot":orientation,"source":str(source[a]),"a":str(points[a]),"b":str(points[b]),"c":str(points[c])})
				var dot := cross.normalized().dot((normals[a]+normals[b]+normals[c]).normalized())
				faces_count+=1
				minimum_dot=minf(minimum_dot,dot)
				if dot<-.001:
					opposed+=1
					if witnesses.size()<12: witnesses.append({"surface":surface,"triangle":offset/3,"dot":dot,"a":str(points[a]),"b":str(points[b]),"c":str(points[c]),"source":str(source[a])})
				if authored_area>1e-10 and cross.length()<authored_area*.001: collapsed+=1
		mesh_records.append({"label":label,"faces":faces_count,"opposed_to_skin_normal":opposed,"collapsed":collapsed,"minimum_dot":minimum_dot,"witnesses":witnesses,
			"reversed_against_source_face":reversed_faces,"minimum_orientation_dot":minimum_orientation,"reversed_witnesses":reversed_witnesses,"skin_transforms":skin_transforms})
		# Smooth shading normals can face away after nonuniform bone scales even
		# under an affine map with positive determinant. Keep that diagnostic,
		# but test geometric folding against the source face and its skin palette.
		check(reversed_faces==0,label+" no reversed source faces: "+str(reversed_faces))
		check(collapsed==0,label+" no collapsed triangles")
