extends SceneTree
## Native rig evidence, staged actors under a common light. No network or FPS
## benchmark claim. The six support directions use actual collision planes.
const Actor = preload("res://scripts/actor_view.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Mosquito = preload("res://scripts/mosquito_pose.gd")
const Catalog = preload("res://scripts/emote_catalog.gd")
const Preview = preload("res://scripts/avatar_preview.gd")
var checks := 0
var failures := 0
var folder := ""
var examples: Array = []
var clock_time := 0.0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		print("APPENDAGE09 FAIL "+label)

func capture(name: String) -> void:
	if DisplayServer.get_name()=="headless": return
	for frame: int in range(2):
		await process_frame
		await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(name+".png"))==OK,"native capture "+name)

func _run() -> void:
	root.size=Vector2i(1120,800)
	folder=ProjectSettings.globalize_path("res://../outputs/0.9-visual")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): folder=arg.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(folder)
	var world := Node3D.new()
	root.add_child(world)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode=Environment.BG_COLOR
	environment.background_color=Color("293f50")
	environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color=Color("c2d2df")
	environment.ambient_light_energy=.5
	environment_node.environment=environment
	world.add_child(environment_node)
	var light := DirectionalLight3D.new()
	light.rotation_degrees=Vector3(-45,-25,0)
	light.rotation_degrees.y=155
	light.light_energy=.85
	light.shadow_enabled=true
	world.add_child(light)
	var camera := Camera3D.new()
	camera.near=.004
	camera.fov=38
	world.add_child(camera)
	camera.make_current()
	var label := Label.new()
	label.position=Vector2(24,20)
	label.add_theme_font_size_override("font_size",23)
	root.add_child(label)
	var floor_body := StaticBody3D.new()
	floor_body.collision_layer=1
	world.add_child(floor_body)
	var collision := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size=Vector3(1,.1,1)
	collision.shape=box
	floor_body.add_child(collision)
	var mesh := BoxMesh.new()
	mesh.size=box.size
	Actor.mesh(floor_body,mesh,Vector3.ZERO,Actor.material(Color("598986")))
	var insect := Actor.new()
	world.add_child(insect)
	insect.build("mosquito","",0)
	insect.preview_only=true
	insect.name_label.visible=false
	for normal: Vector3 in [Vector3.UP,Vector3.DOWN,Vector3.RIGHT,Vector3.LEFT,Vector3.FORWARD,Vector3.BACK]:
		var forward := Vector3.FORWARD.slide(normal).normalized()
		if forward.length_squared()<.5: forward=Vector3.UP
		var basis := Mosquito.surface_basis(normal,forward)
		floor_body.transform=Transform3D(basis,-normal*.05)
		await physics_frame
		var data := {"p":normal*.045,"state":"perched","surface_normal":normal,"surface_forward":forward,"velocity":Vector3.ZERO,"motion_phase":0.0,"motion_speed":0.0,"appearance":{},"facial_no_blink":true,"preview_only":true}
		for frame: int in range(45): insect.update_state(data,1.0/60.0)
		await process_frame
		for phase: float in [0.0,.3,.6,.9,1.2,1.5]:
			data.motion_phase=phase
			data.motion_speed=.65
			data.velocity=forward*.65
			for frame: int in range(8): insect.update_state(data,1.0/60.0)
			var contacts: Dictionary = insect.imported_skin.surface_contacts
			check(contacts.size()==6,"all six tarsi evaluated")
			var planted := 0
			for name: String in contacts:
				var contact: Dictionary = contacts[name]
				check(bool(contact.supported),"actual slab supports "+name+str(normal))
				var height := float(contact.sole_height)
				check(height>=.0001 and height<.015,"deformed tarsus does not penetrate support")
				if bool(contact.stance):
					planted+=1
					check(absf(height-.0003)<.0002,"actual stance sole reaches support on each normal")
			check(planted>=3,"alternating tripods keep support")
			check(insect.global_position.distance_to(data.p)<.0001,"cosmetic limbs do not displace solver centre")
		camera.position=basis*Vector3(.23,.24,-.36)
		camera.look_at(normal*.045,basis.y)
		label.text="PRUEBA PREPARADA · patas sobre superficie · normal "+str(normal)
		await capture("surface-%s"%str(normal).replace(" ",""))
		for style: int in range(3):
			data.appearance={"footwear":style}
			for frame: int in range(3): insect.update_state(data,1.0/60.0)
			for frame: int in range(2):
				await process_frame
				await RenderingServer.frame_post_draw
			var minimum := INF
			for piece: MeshInstance3D in insect.imported_skin.meshes:
				if not piece.visible or not str(piece.name).begins_with("mosquito_footwear_"): continue
				var baked := piece.bake_mesh_from_current_skeleton_pose()
				for surface: int in range(baked.get_surface_count()):
					for vertex: Vector3 in baked.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]:
						minimum=minf(minimum,(piece.global_transform*vertex).dot(normal))
			check(minimum>=.00005 and minimum<.0006,"native deformed footwear sole reaches slab; style "+str(style))
		var previous_feet: Dictionary = {}
		for frame: int in range(40):
			data.p+=forward*.65/60.0
			data.motion_phase+=.65/60.0/.15*TAU
			insect.update_state(data,1.0/60.0)
			await process_frame
			var feet: Dictionary = insect.imported_skin.surface_contacts
			for name: String in feet:
				var foot: Dictionary = feet[name]
				check(bool(foot.supported),"moving visible actor has finite surface support")
				if previous_feet.has(name):
					var previous: Dictionary = previous_feet[name]
					if bool(foot.stance) and bool(previous.stance) and float(foot.cycle)>=float(previous.cycle):
						check((Vector3(foot.tip)-Vector3(previous.tip)).slide(normal).length()<.0004,"stance planted tangentially while actual body moves")
			previous_feet=feet.duplicate(true)
		# A tip outside the finite slab must not be reported as planted on air.
		data.p=normal*.045+basis.x*.60
		for frame: int in range(50): insect.update_state(data,1.0/60.0)
		var supported := 0
		for contact: Dictionary in insect.imported_skin.surface_contacts.values():
			if bool(contact.supported): supported+=1
		check(supported==0,"absent ledge retracts feet without infinite-plane support")
	insect.queue_free()
	floor_body.transform=Transform3D(Basis.IDENTITY,Vector3(0,-.05,0))
	var human := Actor.new()
	world.add_child(human)
	human.build("human","",0)
	human.preview_only=true
	human.name_label.visible=false
	camera.near=.02
	camera.position=Vector3(2.1,1.6,-3.6)
	camera.look_at(Vector3(0,1,0))
	for id: String in Catalog.IDS:
		var data := {"p":Vector3.ZERO,"state":"human","emote_id":id,"emote_time":.85,"tool":"hands","relaxed_pose":true,"preview_only":true,"appearance":{}}
		human.update_state(data,.1)
		await process_frame
		await RenderingServer.frame_post_draw
		for side: String in ["l","r"]:
			var skeleton: Skeleton3D = human.imported_skin.skeleton
			var wrist := skeleton.get_bone_global_pose(int(human.imported_skin.bone_ids["hand_"+side])).origin
			check(wrist.distance_to(human.body_pose["hand_"+side])<.0001,"render wrist matches shared emote contact pose")
		label.text="PRUEBA PREPARADA · gesto corporal: "+str(Catalog.get_emote(id).label)
		await capture("emote-"+id)
		examples.append({"id":id,"time":.85})
	var preview := Preview.new()
	root.add_child(preview)
	preview.set_process(false)
	preview.visible=false
	check(preview.play_emote("wave"),"preview accepts catalog gesture")
	preview._process(.8)
	check(preview.avatar.body_pose==Pose.sample(preview.avatar.human_snapshot_values),"preview and gameplay consume identical HumanPose")
	preview.stop_emote()
	preview._process(.01)
	check(not preview.avatar.human_snapshot_values.has("emote_id"),"empty preview leaves facial fixture actor fields unchanged")
	var report := {"checks":checks,"failures":failures,"scene":"prepared native rig; no online integration claim","normals":6,"examples":examples}
	var file := FileAccess.open(folder.path_join("appendage09.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	print("APPENDAGE09 %d/%d PASS"%[checks-failures,checks])
	quit(1 if failures else 0)
