extends SceneTree
const CharacterSkin = preload("res://assets/art/characters/shared/character_skin.gd")
const Pose = preload("res://scripts/human_pose.gd")
var camera: Camera3D
var character: Node3D
var folder: String
func _initialize() -> void:
	_run.call_deferred()
func capture(name: String) -> void:
	for frame: int in range(10):await process_frame
	if DisplayServer.get_name()!="headless":
		await RenderingServer.frame_post_draw
		print("CHAR06_CAPTURE %s code=%d" % [name,root.get_texture().get_image().save_png(folder.path_join(name+".png"))])
func _run() -> void:
	root.size = Vector2i(1280,720)
	folder = ProjectSettings.globalize_path("res://../outputs/0.6-arte-personajes")
	DirAccess.make_dir_recursive_absolute(folder)
	var environment := WorldEnvironment.new()
	var settings := Environment.new()
	settings.background_mode = Environment.BG_COLOR
	settings.background_color = Color("293f50")
	settings.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	settings.ambient_light_color = Color("b9c9d5")
	settings.ambient_light_energy = 0.35
	environment.environment = settings
	root.add_child(environment)
	for side: float in [-1.0,1.0]:
		var light := DirectionalLight3D.new()
		light.rotation_degrees = Vector3(-35,side*35,0)
		light.light_color = Color("ffddb4") if side<0 else Color("9ccce6")
		light.light_energy = 1.1 if side<0 else 0.40
		root.add_child(light)
	var plane := MeshInstance3D.new()
	var mesh := PlaneMesh.new()
	mesh.size = Vector2(8,8)
	plane.mesh = mesh
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("af9e85")
	plane.material_override = material
	root.add_child(plane)
	camera = Camera3D.new()
	camera.fov = 34.0
	camera.near = 0.02
	root.add_child(camera)
	camera.make_current()
	for species: String in ["human","mosquito"]:
		character = CharacterSkin.new()
		root.add_child(character)
		character.setup(species)
		character.rotation.y = PI
		character.scale = Vector3.ONE*(3.1 if species=="mosquito" else 1.0)
		character.position.y = 0.78 if species=="mosquito" else 0.0
		print("CHAR06_RIG species=%s bones=%d meshes=%d skeleton=%s first_rest=%s" % [species,character.skeleton.get_bone_count(),character.meshes.size(),character.skeleton.name,character.skeleton.get_bone_global_rest(0)])
		camera.position = Vector3(0.6,1.34,3.5)
		camera.look_at(Vector3(0,0.95,0))
		for style: int in range(3):
			var appearance: Dictionary = {"color":5 if style==0 else 2 if style==1 else 3,"accent":4,"face":1 if style==0 else style%3,"hair":style,"outfit":style,"footwear":style,"accessory":3 if species=="human" and style==0 else style}
			character.set_appearance(appearance)
			if species=="human":character.apply_human(Pose.sample({}),{},1.0/60.0)
			else:character.apply_mosquito({"state":"flying"},0.1,0.0)
			await capture(species+"-variante-%d"%style)
		if species=="human":
			character.set_appearance({"color":5,"accent":4,"face":1,"hair":0,"outfit":0,"footwear":0,"accessory":3})
			camera.position = Vector3(0.28,1.70,1.20)
			camera.look_at(Vector3(0,1.60,0))
			await capture("humano-rostro")
			camera.position = Vector3(0.5,.24,1.15)
			camera.look_at(Vector3(0,.14,0))
			await capture("humano-pantuflas")
		character.queue_free()
		await process_frame
		await process_frame
	quit(0)
