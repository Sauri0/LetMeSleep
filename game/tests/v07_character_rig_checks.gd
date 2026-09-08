extends SceneTree
## Checks the imported, currently deformed mesh, not only the gameplay capsules.
const Actor = preload("res://scripts/actor_view.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Simulation = preload("res://scripts/simulation.gd")
const Preview = preload("res://scripts/avatar_preview.gd")
var checks := 0
var failures := 0
var folder: String
var camera: Camera3D
var human: ActorView
var insect: ActorView

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("RIG07 %s %s" % ["PASS" if ok else "FAIL",message])

func settle() -> void:
	for frame: int in range(6): await process_frame
	await physics_frame

func capture(label: String) -> void:
	if DisplayServer.get_name()=="headless": return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(label+".png"))==OK,"capture "+label)

func triangles() -> PackedVector3Array:
	var result := PackedVector3Array()
	for mesh: MeshInstance3D in human.imported_skin.meshes:
		if not mesh.visible: continue
		var baked: ArrayMesh = mesh.bake_mesh_from_current_skeleton_pose()
		for point: Vector3 in baked.get_faces():
			result.append(mesh.global_transform*point)
	return result

func mesh_ray(from: Vector3, to: Vector3, faces: PackedVector3Array) -> float:
	var direction: Vector3 = (to-from).normalized()
	var nearest := INF
	for i: int in range(0,faces.size(),3):
		var result: Variant = Geometry3D.ray_intersects_triangle(from,direction,faces[i],faces[i+1],faces[i+2])
		if result!=null:
			nearest = minf(nearest,from.distance_to(result))
	return nearest

func _run() -> void:
	root.size = Vector2i(1280,720)
	folder = ProjectSettings.globalize_path("res://../outputs/0.7-animacion/rig")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): folder=arg.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(folder)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("293f50")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("b9c9d5")
	environment.ambient_light_energy = 0.35
	environment_node.environment = environment
	root.add_child(environment_node)
	for side: float in [-1.0,1.0]:
		var light := DirectionalLight3D.new()
		light.rotation_degrees = Vector3(-45,-145 if side<0 else 35,0)
		light.light_color = Color("ffddb4") if side<0 else Color("9ccce6")
		light.light_energy = 1.0 if side<0 else 0.4
		light.shadow_enabled = side<0
		root.add_child(light)
	var plane := PlaneMesh.new()
	plane.size = Vector2(12,12)
	var stage := Node3D.new()
	root.add_child(stage)
	Actor.mesh(stage,plane,Vector3.ZERO,Actor.material(Color("988873")))
	camera = Camera3D.new()
	camera.near = 0.02
	camera.fov = 68.0
	root.add_child(camera)
	camera.make_current()
	human = Actor.new()
	root.add_child(human)
	human.build("human","",0)
	human.name_label.hide()
	insect = Actor.new()
	root.add_child(insect)
	insect.build("mosquito","",0)
	insect.name_label.hide()
	insect.hide()
	check(human.imported_skin.skeleton.get_bone_count()==32,"human deformation rig has 32 bones")
	check(insect.imported_skin.skeleton.get_bone_count()==21,"mosquito rig has six articulated legs, wings and proboscis")
	var data: Dictionary = {"p":Vector3.ZERO,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"grounded":true,"state":"human","appearance":{"color":5,"accent":4,"face":1,"hair":0,"outfit":0,"accessory":3,"footwear":0}}
	for posture: String in ["stand","walk","run","crouch","crouch_run","jump"]:
		data.crouch_amount = 1.0 if posture in ["crouch","crouch_run"] else 0.0
		data.motion_speed = 5.2 if posture in ["run","crouch_run"] else 3.1 if posture=="walk" else 0.0
		data.motion_phase = 1.2 if posture=="walk" else 2.4
		data.sprinting = posture in ["run","crouch_run"]
		data.grounded = posture!="jump"
		data.velocity = Vector3(0,2.0 if posture=="jump" else 0,0)
		data.p = Vector3(0,0.35 if posture=="jump" else 0,0)
		human.update_state(data,1.0)
		human.set_local(false)
		camera.fov = 40.0
		camera.position = Vector3(2.2,1.5,-3.6)
		camera.look_at(Vector3(0,.98,0))
		await settle()
		await capture("rig-"+posture)
		human.set_local(true)
		await settle()
		var faces: PackedVector3Array = triangles()
		check(faces.size()>30000,"rendered mesh baked for "+posture)
		for zone_id: int in range(Simulation.BODY_ZONES.size()):
			var zone: Dictionary = Pose.zone_pose(data,Simulation.BODY_ZONES[zone_id])
			var from: Vector3 = zone.p+zone.normal*0.25
			var hit: float = mesh_ray(from,zone.p-zone.normal*.10,faces)
			check(is_finite(hit) and absf(hit-0.25)<0.045,"mesh skin matches %s zone%d offset=%.4f" % [posture,zone_id,hit-0.25])
			if zone_id>=8: continue
			var target: Vector3 = zone.p+zone.normal*Simulation.ATTACH_OFFSET
			var origin: Vector3 = Pose.view_origin(data)
			var nearest: float = mesh_ray(origin,target,faces)
			check(nearest>origin.distance_to(target)-0.015,"actual mesh leaves own target visible "+posture+str(zone_id))
			if posture in ["stand","crouch"]:
				insect.update_state({"p":target,"yaw":atan2(zone.normal.x,zone.normal.z),"surface_normal":zone.normal,"state":"biting","appearance":{"color":3,"accent":0,"face":1,"hair":0,"outfit":0}},1.0)
				camera.fov = 68.0
				camera.position = origin
				camera.look_at(target)
				await settle()
				await capture("fps-"+posture+"-zona%d"%zone_id)
		insect.hide()
	data.crouch_amount = 0.0
	data.grounded = true
	data.p = Vector3.ZERO
	data.relaxed_pose = true
	human.set_local(false)
	camera.fov = 40.0
	camera.position = Vector3(1.5,1.3,-3.4)
	camera.look_at(Vector3(0,.96,0))
	for label: String in ["reposo","caminar"]:
		data.motion_speed = 3.1 if label=="caminar" else 0.0
		data.motion_phase = 1.45
		human.update_state(data,1.0)
		await settle()
		await capture("humano-relajado-"+label)
	camera.fov = 34.0
	camera.position = Vector3(.57,.84,-.84)
	camera.look_at(Vector3(.31,.71,-.025))
	data.motion_speed = 0.0
	human.update_state(data,1.0)
	await settle()
	await capture("humano-palma")
	data.relaxed_pose = false
	data.motion_phase = 0.0
	data.sprinting = false
	camera.fov = 40
	camera.position = Vector3(1.7,1.25,-2.8)
	camera.look_at(Vector3(0,.95,0))
	for tool: String in ["swatter","racket","newspaper","broom"]:
		data.tool = tool
		data.strike = {}
		human.update_state(data,1.0)
		await settle()
		await capture("herramienta-"+tool+"-agarre")
		data.strike = {"active":true,"progress":.45,"point":Vector3(0,1.12,-.58),"normal":Vector3.BACK,"hand":"right","tool":tool}
		human.update_state(data,1.0)
		var pose: Dictionary = Pose.sample(data)
		var tool_face: Vector3 = human.tool_socket.to_global(Vector3.DOWN*float(Pose.TOOL_LENGTHS[tool]))
		check(tool_face.distance_to(pose.strike_contact)<.001,"visible tool face equals authoritative contact "+tool)
		check(absf(human.tool_socket.global_basis.z.dot(pose.tool_normal))>.999,"tool broad face follows shared contact normal "+tool)
		await settle()
		await capture("herramienta-"+tool+"-contacto")
	data.tool = "hands"
	data.strike = {}
	human.update_state(data,1.0)
	human.set_local(true)
	for mesh: MeshInstance3D in human.imported_skin.meshes:
		if "_head" in str(mesh.name) or "_face_" in str(mesh.name) or "_hair_" in str(mesh.name) or "_accessory_" in str(mesh.name):
			check(not mesh.visible,"local head detail hidden "+str(mesh.name))
	human.hide()
	var preview := Preview.new()
	preview.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(preview)
	for role: String in ["human","mosquito"]:
		preview.set_avatar(role,{"color":5,"accent":4,"face":1,"hair":0,"outfit":0,"accessory":3 if role=="human" else 0,"footwear":0})
		for view: String in ["front","side","back"]:
			preview.set_view(view)
			await settle()
			await capture("editor-"+role+"-"+view)
		preview.focus_category("face")
		check(preview.focus_key=="face" and preview.focus_distance<3.0,"preview face focus "+role)
		preview.focus_category("footwear")
		check(preview.focus_key=="footwear","preview footwear focus "+role)
		preview.reset_view()
		check(preview.focus_key.is_empty() and preview.zoom==1.0,"preview reset "+role)
	preview.queue_free()
	human.queue_free()
	insect.queue_free()
	await process_frame
	await process_frame
	print("RIG07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
