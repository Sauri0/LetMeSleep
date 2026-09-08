extends SceneTree
## Staged meshes in the actual house, with its unchanged lighting and scale.
const SampleSkin=preload("res://assets/art/characters/shared/character_skin.gd")
const Pose=preload("res://scripts/human_pose.gd")
func _initialize()->void:_run.call_deferred()
func _run()->void:
	root.size=Vector2i(1600,1000)
	root.msaa_3d=Viewport.MSAA_2X
	var world:Node3D=load("res://scripts/world.gd").new()
	root.add_child(world);world.build();world.load_map("house")
	var camera:=Camera3D.new();root.add_child(camera)
	camera.fov=48;camera.near=.025;camera.make_current()
	var label:=Label.new();label.position=Vector2(20,18)
	label.add_theme_font_size_override("font_size",24)
	label.add_theme_color_override("font_shadow_color",Color.BLACK)
	label.add_theme_constant_override("shadow_offset_x",2)
	label.add_theme_constant_override("shadow_offset_y",2)
	root.add_child(label)
	for variant:String in ["A","B"]:
		var group:=Node3D.new();root.add_child(group)
		for species:String in ["human","mosquito"]:
			var skin:=SampleSkin.new();group.add_child(skin)
			var directory:="res://assets/art/samples07/characters/%s/%s/"%[variant,species]
			skin.setup(species,directory+species+"_lms06.glb",directory+"rig_contract.json")
			skin.set_appearance({"color":1,"accent":4,"face":1 if species=="human" else 0,"hair":0,"outfit":0,"accessory":3 if species=="human" else 0,"footwear":0})
			skin.position=Vector3(5.4,3.2,8.6) if species=="human" else Vector3(4.9,4.38,8.4)
			skin.rotation.y=.9
			skin.scale=Vector3.ONE*(1.0 if species=="human" else .35)
			var data:Dictionary={"p":Vector3.ZERO,"yaw":.9,"body_yaw":.9,"pitch":0.0,"state":"human" if species=="human" else "flying","velocity":Vector3.ZERO,"motion_speed":0.0,"grounded":true,"relaxed_pose":true,"pose_time":0.0,"preview_only":true,"facial_preview":"neutral","facial_no_blink":true,"tool":"hands"}
			for frame:int in range(30):
				if species=="human":skin.apply_human(Pose.sample(data),data,1.0/30.0)
				else:skin.apply_mosquito(data,0.0,0.0)
		var folder:=ProjectSettings.globalize_path("res://../outputs/0.7-muestras/personajes/"+variant)
		DirAccess.make_dir_recursive_absolute(folder)
		for detail:bool in [false,true]:
			camera.position=Vector3(4.1,4.9,7.2) if detail else Vector3(3.15,4.85,6.25)
			camera.look_at(Vector3(5.4,4.72,8.6) if detail else Vector3(5.25,4.13,8.6))
			label.text="Propuesta %s · casa real · Compatibility · escala del juego · luz actual sin retoques"%variant
			for frame:int in range(12):await process_frame
			await RenderingServer.frame_post_draw
			assert(root.get_texture().get_image().save_png(folder.path_join("world-detail.png" if detail else "world.png"))==OK)
		group.queue_free();await process_frame
	print("SAMPLES07_WORLD_VIEWS variants=2 captures=4 unchanged_house_lighting=true")
	world.queue_free();camera.queue_free();label.queue_free()
	await process_frame;await process_frame;quit()
