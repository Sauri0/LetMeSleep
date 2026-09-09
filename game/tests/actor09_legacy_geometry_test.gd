extends SceneTree
## Exact production reference differs only by removing its global class_name.
## This fixture times CPU submission and the following native draw separately.
const Current = preload("res://scripts/actor_view.gd")
const Reference = preload("res://tests/actor09_legacy_reference.gd")
const Tools = preload("res://scripts/tool_catalog.gd")
const Emotes = preload("res://scripts/emote_catalog.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const BASELINE_SHA := "90bf7163a767f6ccfc0223aae625e7541dfe61f49ede67e8fb0ca473f37f6a0c"
const DT := 1.0/60.0
var checks := 0
var failures: Array[String]=[]
var report_path := ""
var reference_view: Node3D
var current_view: Node3D
var change_counts := {"reference":0,"current":0}
var report: Dictionary={}

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<12:printerr("ACTOR09_LEGACY_FAIL "+label)

func _pose(index: int, tool: String) -> Dictionary:
	var phase:=float(index)*.137
	var data: Dictionary={"id":1,"role":"human","state":"human","alive":true,"p":Vector3.ZERO,"tool":tool,"yaw":sin(phase)*1.3,"body_yaw":.2,"pitch":sin(phase*.7)*1.5,"pose_time":float(index)/20.0,"crouch_amount":(.5+.5*sin(phase*.9)),"motion_phase":phase,"motion_speed":3.0,"motion_blend":.8,"motion_stride":1.15,"motion_direction":Vector3.FORWARD,"sprinting":true,"grounded":true}
	if posmod(index,5)==1:
		data.strike={"active":true,"progress":fposmod(phase,1.0),"point":Vector3(.4,1.1,-.7),"normal":Vector3.FORWARD,"hand":"right","tool":tool}
	elif posmod(index,5)==2 and Tools.throwable(tool):
		data.throw_gesture={"state":["charging","release","recovering"][posmod(index,3)],"progress":fposmod(phase,1.0),"power":.6,"direction":Vector3(.15,.1,-1).normalized(),"tool":tool}
	elif posmod(index,5)==3:
		data.emote_id=Emotes.IDS[posmod(index,4)]
		data.emote_time=fposmod(phase,1.8)
	return data

func _legacy_meshes(view: Node3D) -> Array[MeshInstance3D]:
	var result: Array[MeshInstance3D]=[view.torso_node.get_child(0)]
	for key: String in view.limb_meshes:result.append(view.limb_meshes[key])
	return result

func _set_legacy_visible(view: Node3D, visible_value: bool) -> void:
	for mesh: MeshInstance3D in _legacy_meshes(view):mesh.visible=visible_value

func _same_tree(a: Node, b: Node, label: String) -> void:
	check(a.get_child_count()==b.get_child_count(),label+" node topology")
	if a is Node3D and b is Node3D:
		check(a.transform.is_equal_approx(b.transform),label+" transform")
		check(a.visible==b.visible,label+" visibility")
	if a is MeshInstance3D and b is MeshInstance3D and a.visible:
		check(a.mesh.get_aabb().is_equal_approx(b.mesh.get_aabb()),label+" visible geometry bounds")
		if a.mesh is ArrayMesh and b.mesh is ArrayMesh:
			check(a.mesh.get_blend_shape_count()==b.mesh.get_blend_shape_count(),label+" visible morph topology")
			for shape: int in range(a.mesh.get_blend_shape_count()):
				check(is_equal_approx(a.get_blend_shape_value(shape),b.get_blend_shape_value(shape)),label+" visible morph "+str(shape))
	if a is CollisionShape3D and b is CollisionShape3D:
		check(a.shape.get_class()==b.shape.get_class(),label+" collider class")
		if a.shape is CapsuleShape3D:
			check(a.shape.radius==b.shape.radius and a.shape.height==b.shape.height,label+" authoritative capsule")
		elif a.shape is SphereShape3D:check(a.shape.radius==b.shape.radius,label+" authoritative sphere")
	for i: int in range(mini(a.get_child_count(),b.get_child_count())):_same_tree(a.get_child(i),b.get_child(i),label+"/"+str(i))

func _compare(label: String) -> void:
	check(reference_view.body_pose==current_view.body_pose,label+" shared pose exact")
	check(reference_view.imported_skin.facial_values==current_view.imported_skin.facial_values,label+" facial driver output exact")
	check(reference_view.tool_socket.global_transform.is_equal_approx(current_view.tool_socket.global_transform),label+" grip/socket")
	var before: Skeleton3D=reference_view.imported_skin.skeleton
	var after: Skeleton3D=current_view.imported_skin.skeleton
	check(before.get_bone_count()==after.get_bone_count(),label+" rig topology")
	for bone: int in range(before.get_bone_count()):
		check(before.get_bone_global_pose(bone).is_equal_approx(after.get_bone_global_pose(bone)),label+" bone "+str(bone))
	_same_tree(reference_view,current_view,label)

func _measure(view: Node3D, mode: String, block: int) -> Dictionary:
	# Four changing snapshots per batch, with native draw between batches. It
	# measures writer CPU and deferred draw cost without calling the profiler.
	var calls:=0;var cpu_us:=0;var draw_us:=0
	for batch: int in range(12):
		var started:=Time.get_ticks_usec()
		for tick: int in range(4):
			var data:=_pose(1000+batch*4+tick,"hands")
			view._apply_human_pose(data,DT,data)
			calls+=1
		cpu_us+=Time.get_ticks_usec()-started
		started=Time.get_ticks_usec()
		await RenderingServer.frame_post_draw
		draw_us+=Time.get_ticks_usec()-started
		await process_frame
	return {"mode":mode,"block":block,"calls":calls,"submission_cpu_ms":float(cpu_us)/1000.0,"mean_submission_ms":float(cpu_us)/1000.0/calls,"post_draw_wait_ms":float(draw_us)/1000.0,"limit":"post_draw includes renderer and scheduling, not isolated GPU cost"}

func _run() -> void:
	var sources: Dictionary={}
	for path: String in ["res://scripts/actor_view.gd","res://assets/art/characters/shared/character_skin.gd","res://scripts/human_pose.gd","res://scripts/human_presentation.gd","res://tests/actor09_legacy_reference.gd"]:
		sources[path]=FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable_in_pack"
	var reference_source_available:=FileAccess.file_exists("res://tests/actor09_legacy_reference.gd")
	if reference_source_available:
		var text:=FileAccess.get_file_as_string("res://tests/actor09_legacy_reference.gd")
		var newline:="\r\n" if text.contains("\r\n") else "\n"
		check(("class_name ActorView"+newline+text).sha256_text()==BASELINE_SHA,"reference is exact frozen ActorView except class_name")
	reference_view=Reference.new();current_view=Current.new()
	root.add_child(reference_view);root.add_child(current_view)
	reference_view.build("human","Reference",0);current_view.build("human","Candidate",0)
	# Production derives a cosmetic phase from instance_id. Give both real
	# controllers the same initial seed, then advance normally without resets.
	reference_view.imported_skin.facial=Facial.new(.375)
	current_view.imported_skin.facial=Facial.new(.375)
	# Match display names as part of the visible tree comparison.
	reference_view.name_label.text="Witness";current_view.name_label.text="Witness"
	for mode: String in ["reference","current"]:
		var view: Node3D=reference_view if mode=="reference" else current_view
		for mesh: MeshInstance3D in _legacy_meshes(view):
			mesh.mesh.changed.connect(func()->void:change_counts[mode]+=1)
	var cases:=0
	for tool: String in Tools.IDS:
		for step: int in range(12):
			var data:=_pose(cases,tool);var original:=data.duplicate(true)
			var local_value:=step%4==0
			reference_view.set_local(local_value);current_view.set_local(local_value)
			reference_view.set_pose_critical(step%3==0);current_view.set_pose_critical(step%3==0)
			reference_view.update_state(data,DT);current_view.update_state(data,DT)
			_compare(tool+"/"+str(step))
			check(data==original,"public input unchanged")
			cases+=1
	# Reveal fallback with a byte-identical cached pose, then exercise moving
	# fallback under an invisible ancestor. Both must match the old geometry.
	var cached:=_pose(cases,"hands")
	reference_view.set_local(true);current_view.set_local(true)
	reference_view.update_state(cached,DT);current_view.update_state(cached,DT)
	_set_legacy_visible(reference_view,true);_set_legacy_visible(current_view,true)
	reference_view._apply_human_pose(cached,DT,cached);current_view._apply_human_pose(cached,DT,cached)
	_compare("fallback revealed on cache hit")
	reference_view.visible=false;current_view.visible=false
	for step: int in range(8):
		var data:=_pose(cases+step+1,"hands")
		reference_view.update_state(data,DT);current_view.update_state(data,DT)
		_compare("fallback hidden ancestor "+str(step))
	reference_view.visible=true;current_view.visible=true
	_set_legacy_visible(reference_view,false);_set_legacy_visible(current_view,false)
	await process_frame
	if DisplayServer.get_name()!="headless":
		await RenderingServer.frame_post_draw
		await process_frame
	var before:=change_counts.duplicate()
	var runs: Array=[]
	if DisplayServer.get_name()!="headless":
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED);Engine.max_fps=0
		for block: int in range(2):
			for mode: String in ["reference","current","current","reference"]:
				runs.append(await _measure(reference_view if mode=="reference" else current_view,mode,block))
		check(change_counts.current==before.current,"hidden candidate procedural mesh has no changed events during ABBA")
		check(change_counts.reference>before.reference,"reference actually regenerates hidden procedural geometry")
	for path: String in sources:
		var final_hash:=FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable_in_pack"
		if sources[path]!="unavailable_in_pack":check(sources[path]==final_hash,"frozen source "+path)
	report={"checks":checks,"failures":failures,"public_pose_cases":cases,"facial_seed":.375,"baseline_sha256":BASELINE_SHA,"reference_source_hash_verified":reference_source_available,"source_sha256":sources,"runs":runs,"procedural_mesh_changed_events":{"reference":change_counts.reference-before.reference,"current":change_counts.current-before.current},"scope":"same public pose, transforms, GLB bones, visible geometry bounds and all ray shapes; only hidden legacy mesh dimensions differ; CPU diagnostic, not global FPS; pack can run compiled reference but cannot verify omitted original source hashes"}
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
	print("ACTOR09_LEGACY checks=%d failures=%d"%[checks,failures.size()])
	reference_view.queue_free();current_view.queue_free()
	await process_frame
	quit.call_deferred(0 if failures.is_empty() else 1)
