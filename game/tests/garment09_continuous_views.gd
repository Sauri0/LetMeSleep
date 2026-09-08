extends SceneTree
## Prepared continuous input, actual Simulation/ActorView/HumanPose.
## Studio review only: no microphone, audio provider, gameplay or FPS claim.
const Sim = preload("res://scripts/simulation.gd")
const Actor = preload("res://scripts/actor_view.gd")
const Catalog = preload("res://scripts/emote_catalog.gd")
const DT := 1.0/60.0
const FPS := 30
const TOTAL_FRAMES := 630
const TOOLS: Array[String] = ["hands","racket","broom"]
const EMOTE_TICKS := {540:"wave",720:"celebrate",900:"shrug",1044:"yawn"}
const SOURCE_FILES: Array[String] = [
	"res://assets/art/characters/human/human_lms06.glb",
	"res://assets/art/characters/human/rig_contract.json",
	"res://assets/art/characters/shared/character_skin.gd",
	"res://assets/art/characters/shared/facial_expression.gd",
	"res://assets/art/characters/shared/cloth.gdshader",
	"res://scripts/actor_view.gd","res://scripts/human_pose.gd",
	"res://scripts/human_presentation.gd",
	"res://scripts/emote_pose.gd","res://scripts/emote_catalog.gd",
	"res://scripts/simulation.gd","res://scripts/tool_catalog.gd"]
var output := "res://../outputs/0.9-garment-continuous"
var limit := TOTAL_FRAMES
var frames_enabled := true
var trim_full_indices := false
var checks := 0
var failures: Array[String] = []
var rows: Array[Dictionary] = []
var actors: Array[Node3D] = []
var simulations: Array[RefCounted] = []
var cameras: Array[Camera3D] = []
var snapshots: Array[Dictionary] = []
var garment_meshes: Array[MeshInstance3D] = []
var indices: Array[PackedInt32Array] = []
var previous: Array[PackedVector3Array] = []
var maxima: Array[Dictionary] = []
var starts: Array[Vector3] = []
var completed_emotes: Array[Dictionary] = []
var emote_timing: Array[Dictionary] = []
var sources: Dictionary = {}
var caption: Label

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks+=1
	if not value:
		failures.append(label)
		if failures.size()<15: printerr("GARMENT09_CONTINUOUS_FAIL "+label)

func _vec(point: Vector3) -> Array:
	return [point.x,point.y,point.z]

func _hashes() -> Dictionary:
	var result := {}
	for path: String in SOURCE_FILES: result[path]=FileAccess.get_sha256(path)
	return result

func _caption(text: String, position: Vector2, size: Vector2, font: int=24) -> Label:
	var label := Label.new()
	label.text=text
	label.position=position
	label.size=size
	label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size",font)
	label.add_theme_color_override("font_color",Color("f1eee3"))
	root.add_child(label)
	return label

func _make_column(index: int) -> void:
	var viewport := SubViewport.new()
	viewport.size=Vector2i(640,900)
	viewport.own_world_3d=true
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d=Viewport.MSAA_4X
	root.add_child(viewport)
	var texture := TextureRect.new()
	texture.position=Vector2(index*640,70)
	texture.size=Vector2(640,900)
	texture.texture=viewport.get_texture()
	texture.mouse_filter=Control.MOUSE_FILTER_IGNORE
	root.add_child(texture)
	var environment := Environment.new()
	environment.background_mode=Environment.BG_COLOR
	environment.background_color=Color("23313c")
	environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color=Color("d4deed")
	environment.ambient_light_energy=.65
	var world := WorldEnvironment.new()
	world.environment=environment
	viewport.add_child(world)
	var light := DirectionalLight3D.new()
	light.rotation_degrees=Vector3(-40,-28,0)
	light.light_energy=.75
	light.shadow_enabled=true
	viewport.add_child(light)
	var actor := Actor.new()
	viewport.add_child(actor)
	actor.build("human","",0)
	actor.preview_only=true
	actor.name_label.hide()
	actor.set_local(false)
	actors.append(actor)
	var camera := Camera3D.new()
	camera.fov=47.0
	camera.near=.02
	viewport.add_child(camera)
	camera.make_current()
	cameras.append(camera)
	var sim := Sim.new()
	sim.start({1:{"role":"human"},2:{"role":"mosquito"}},{"mode":"blood","round_seconds":180,"blood_goal":1000,"map_id":"house"})
	check(sim.phase=="playing","authority starts column "+str(index))
	sim.actors[1].p=Vector3(0,0,7.5)
	sim.actors[1]._next_task=1000.0
	sim.actors[1].tool=TOOLS[index]
	sim.actors[1].appearance={"color":0,"accent":0,"outfit":index,"eyes":0,"mouth":0,"brows":0,"hair":0,"hair_color":0,"mustache":0,"beard":0,"accessory":0,"footwear":0}
	sim.actors[2].p=Vector3(6,4.6,-8)
	sim.actors[2]._assignment={}
	simulations.append(sim)
	snapshots.append(sim.public_snapshot().actors[1])
	actor.update_state(snapshots[index],DT)
	previous.append(PackedVector3Array())
	maxima.append({"vertex_step_m":0.0,"vertex_step_frame":0,"walk_speed_mps":0.0,"crouch":0.0,"yaw_min":0.0,"yaw_max":0.0,"root_emote_drift_m":0.0})
	starts.append(sim.actors[1].p)
	completed_emotes.append({})
	emote_timing.append({})
	var selected: MeshInstance3D
	for mesh: MeshInstance3D in actor.imported_skin.meshes:
		if trim_full_indices and str(mesh.name).begins_with("human_outfit_") and str(mesh.name).ends_with("_trim"):
			mesh.lod_bias=128.0
		if str(mesh.name)=="human_outfit_"+str(index):
			selected=mesh
	check(selected!=null,"selected garment exists "+str(index))
	garment_meshes.append(selected)
	var chosen := PackedInt32Array()
	if selected!=null:
		var points: PackedVector3Array=selected.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		for vertex: int in range(points.size()):
			if points[vertex].y>1.10 and absf(points[vertex].x)<.40: chosen.append(vertex)
	indices.append(chosen)
	check(chosen.size()>100,"actual upper garment vertices selected "+str(index))
	_caption("Prenda %d · %s"%[index,TOOLS[index]],Vector2(index*640,72),Vector2(640,34),23)

func _input_at(time: float) -> Dictionary:
	var move := Vector3.FORWARD if time>=.5 and time<2.0 else Vector3.ZERO
	var crouch := time>=2.0 and time<7.6
	var pitch := -1.92*smoothstep(2.0,3.0,time)*(1.0-smoothstep(7.6,8.4,time))
	var yaw := 0.0
	if time>=3.0 and time<4.0: yaw=-deg_to_rad(75.0)*smoothstep(3.0,4.0,time)
	elif time>=4.0 and time<6.0: yaw=lerpf(-deg_to_rad(75.0),deg_to_rad(75.0),smoothstep(4.0,6.0,time))
	elif time>=6.0 and time<7.0: yaw=deg_to_rad(75.0)*(1.0-smoothstep(6.0,7.0,time))
	return {"move":move,"yaw":yaw,"pitch":pitch,"crouch":crouch}

func _stage(time: float) -> String:
	if time<.5: return "Reposo"
	if time<2.0: return "Caminar"
	if time<3.0: return "Frenar · agacharse · mirar abajo"
	if time<7.6: return "Mirada −75° ↔ +75° · respiración real"
	if time<9.0: return "Levantarse · volver al frente"
	if time<12.0: return "Saludar · entrada y retorno"
	if time<15.0: return "Festejar · entrada y retorno"
	if time<17.4: return "Encoger hombros · entrada y retorno"
	return "Bostezar · entrada y retorno"

func _measure(index: int, frame: int) -> Dictionary:
	var actor: Node3D=actors[index]
	var mesh: MeshInstance3D=garment_meshes[index]
	var baked := mesh.bake_mesh_from_current_skeleton_pose()
	check(baked!=null,"post-draw native garment bake "+str(frame)+"/"+str(index))
	var points := PackedVector3Array()
	var max_step := 0.0
	if baked!=null:
		var all_points: PackedVector3Array=baked.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		var transform := actor.global_transform.affine_inverse()*mesh.global_transform
		for vertex: int in indices[index]: points.append(transform*all_points[vertex])
		var old: PackedVector3Array=previous[index]
		if old.size()==points.size():
			var finite := true
			for vertex: int in range(points.size()):
				finite=finite and points[vertex].is_finite()
				max_step=maxf(max_step,points[vertex].distance_to(old[vertex]))
			check(finite,"all upper garment vertices finite "+str(frame)+"/"+str(index))
		previous[index]=points
	if max_step>float(maxima[index].vertex_step_m):
		maxima[index].vertex_step_m=max_step
		maxima[index].vertex_step_frame=frame
	var public: Dictionary=snapshots[index]
	var speed: float=public.get("motion_speed",0.0)
	maxima[index].walk_speed_mps=maxf(maxima[index].walk_speed_mps,speed)
	maxima[index].crouch=maxf(maxima[index].crouch,float(public.get("crouch_amount",0.0)))
	maxima[index].yaw_min=minf(maxima[index].yaw_min,float(public.yaw))
	maxima[index].yaw_max=maxf(maxima[index].yaw_max,float(public.yaw))
	var emote: String=str(public.get("emote_id",""))
	if not emote.is_empty():
		if not completed_emotes[index].has(emote): completed_emotes[index][emote]=_vec(public.p)
		var p: Array=completed_emotes[index][emote]
		maxima[index].root_emote_drift_m=maxf(maxima[index].root_emote_drift_m,Vector3(public.p).distance_to(Vector3(p[0],p[1],p[2])))
	return {"tool":TOOLS[index],"outfit":index,"root":_vec(public.p),"display_root":_vec(actor.global_position),
		"head":_vec(actor.body_pose.head),"left_shoulder":_vec(actor.body_pose.shoulder_l),"right_shoulder":_vec(actor.body_pose.shoulder_r),
		"pitch":public.pitch,"yaw":public.yaw,"body_yaw":public.body_yaw,"crouch":public.get("crouch_amount",0.0),
		"display_crouch":actor.human_snapshot_values.get("crouch_amount",0.0),
		"motion_speed":speed,"motion_phase":public.get("motion_phase",0.0),"pose_time":public.get("pose_time",0.0),
		"emote_id":emote,"emote_time":public.get("emote_time",0.0),"selected_vertices":points.size(),"max_vertex_step_m":max_step}

func _run() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output=argument.trim_prefix("--output=")
		if argument.begins_with("--limit="): limit=clampi(int(argument.trim_prefix("--limit=")),1,TOTAL_FRAMES)
		if argument=="--no-frames": frames_enabled=false
		if argument=="--trim-full-indices": trim_full_indices=true
	output=ProjectSettings.globalize_path(output) if output.begins_with("res://") else output
	DirAccess.make_dir_recursive_absolute(output.path_join("frames"))
	check(DisplayServer.get_name()!="headless","native renderer required")
	if not failures.is_empty(): quit(2);return
	root.size=Vector2i(1920,1080)
	root.content_scale_size=Vector2i(1920,1080)
	var background := ColorRect.new()
	background.color=Color("17242c")
	background.size=Vector2(1920,1080)
	root.add_child(background)
	sources=_hashes()
	for path: String in SOURCE_FILES: check(not str(sources[path]).is_empty(),"source SHA available "+path)
	for index: int in range(3): _make_column(index)
	_caption("0.9.0 · SECUENCIA PREPARADA CONTINUA · ActorView / HumanPose reales · sin audio",Vector2.ZERO,Vector2(1920,36),25)
	caption=_caption("",Vector2(0,982),Vector2(1920,40),27)
	_caption("Sim 60 Hz · snapshots públicos 20 Hz · ActorView 60 Hz · registro 30 Hz / dt 33,333 ms · cámara de estudio",Vector2(0,1030),Vector2(1920,36),22)
	if not failures.is_empty(): quit(2);return
	for warmup: int in range(2):
		await process_frame
		await RenderingServer.frame_post_draw
	for frame: int in range(limit):
		for substep: int in range(2):
			var tick := frame*2+substep
			var time := tick*DT
			var control := _input_at(time)
			for index: int in range(3):
				var sim: RefCounted=simulations[index]
				sim.submit_input(1,tick+1,control.move,control.yaw,control.pitch,false,false,control.crouch)
				if EMOTE_TICKS.has(tick):
					var emote: String=EMOTE_TICKS[tick]
					var accepted: bool=sim.request_emote(1,emote)
					check(accepted,"authority accepts "+emote+"/"+TOOLS[index])
					if accepted:
						emote_timing[index][emote]={"started":sim.elapsed,"ended":-1.0,"expected_duration":float(Catalog.get_emote(emote).duration)}
				sim.step(DT)
				for emote: String in emote_timing[index]:
					var timing: Dictionary=emote_timing[index][emote]
					if float(timing.ended)<0 and str(sim.actors[1].emote_id)!=emote:
						timing.ended=sim.elapsed
						check(absf(float(timing.ended)-float(timing.started)-float(timing.expected_duration))<DT*.51,"natural full gesture duration "+emote+"/"+TOOLS[index])
				if tick%3==0: snapshots[index]=sim.public_snapshot().actors[1]
				actors[index].update_state(snapshots[index],DT)
		for index: int in range(3):
			var actor: Node3D=actors[index]
			var target: Vector3=actor.to_global(Vector3(actor.body_pose.head)+Vector3(0,-.25,0))
			cameras[index].position=target+Vector3(0,.10,-2.15)
			cameras[index].look_at(target)
		caption.text="%05.2f s · %s · cuadro %03d"%[frame/float(FPS),_stage(frame/float(FPS)),frame]
		await process_frame
		await RenderingServer.frame_post_draw
		var measured: Array[Dictionary]=[]
		for index: int in range(3): measured.append(_measure(index,frame))
		rows.append({"frame":frame,"time_seconds":frame/float(FPS),"dt_seconds":1.0/FPS,"stage":_stage(frame/float(FPS)),"actors":measured})
		if frames_enabled:
			check(root.get_texture().get_image().save_png(output.path_join("frames/%04d.png"%frame))==OK,"frame saved "+str(frame))
		if frame%90==0: print("GARMENT09_CONTINUOUS_PROGRESS frame=%d/%d"%[frame,limit])
	if limit==TOTAL_FRAMES:
		for index: int in range(3):
			check(float(maxima[index].walk_speed_mps)>.5,"actual walking observed")
			check(float(maxima[index].crouch)>.99,"actual crouch observed")
			check(completed_emotes[index].size()==4,"four authority gestures observed publicly")
			check(emote_timing[index].size()==4,"four authority starts tracked")
			for timing: Dictionary in emote_timing[index].values():
				check(float(timing.ended)>float(timing.started),"each authority gesture completed naturally")
			check(str(simulations[index].actors[1].emote_id).is_empty(),"last gesture returns to rest")
			check(float(maxima[index].root_emote_drift_m)<.00001,"gestures retain stationary root")
	check(sources==_hashes(),"source hashes unchanged during continuous recording")
	var report := {"fixture":"garment09_continuous_views","source_only":true,"source_sha256":sources,"frame_count":rows.size(),"expected_frames":TOTAL_FRAMES,
		"diagnostic_trim_lod_bias":128.0 if trim_full_indices else 1.0,
		"sequence_complete":limit==TOTAL_FRAMES,"checks":checks,"failures":failures,"dt_authority":DT,"dt_view":DT,"dt_recording":1.0/FPS,
		"snapshot_hz":20,"metrics":maxima,"emote_timing":emote_timing,"rows":rows,"visual_approved":false,
		"limits":["Prepared equipment and initial positions on three isolated copies of house authority; the shown studio is not a gameplay map.",
		"All ticks and transitions run sequentially without per-pose reset. Voice level remains zero and no microphone/provider is created.",
		"Maximum vertex displacement is a measured diagnostic, not an aesthetic PASS threshold.",
		"Three representative combinations: outfit0/hands, outfit1/racket, outfit2/broom. This is not exhaustive tool/outfit coverage.",
		"Remote noncritical presentation: no FPS/focus/strike/throw. Critical bypass remains authoritative20Hz. Mesh displacement includes actual rig motion, not just cloth deformation.",
		"Native recording at fixed simulation dt is not a performance benchmark. No physics, pose, radius or runtime smoothing is changed."]}
	var file := FileAccess.open(output.path_join("report.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	print("GARMENT09_CONTINUOUS_RESULT frames=%d complete=%s checks=%d failures=%d output=%s"%[rows.size(),str(limit==TOTAL_FRAMES),checks,failures.size(),output])
	quit(0 if failures.is_empty() else 1)
