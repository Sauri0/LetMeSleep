extends SceneTree
## Renderer contract for confirmed public states. Gameplay stun/revive rules are
## checked separately by authoritative simulation tests.
var world: Node3D
var camera: Camera3D
var states: Dictionary
var failures := 0
var checks := 0
var folder: String

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
	print("STUN_VISUAL05 %s %s" % ["PASS" if value else "FAIL",label])

func settle(seconds: float=0.35) -> void:
	for step: int in range(int(seconds*60)):
		world.sync_actors(states,2,1.0/60.0)
		await process_frame
	await physics_frame

func capture(name: String) -> void:
	if DisplayServer.get_name()=="headless":
		return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(name+".png"))==OK,"capture "+name)

func _run() -> void:
	root.size = Vector2i(1280,720)
	folder = ProjectSettings.globalize_path("res://../outputs/0.5-preview")
	world = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	world.set_local_role(2,"mosquito")
	states = {
		1:{"role":"mosquito","name":"Luna","alive":true,"state":"flying","p":Vector3(-6.4,0.22,7.8),"yaw":0.2,"pitch":0.0,"appearance":{"color":3,"accent":4,"hair":2,"face":1,"outfit":1,"accessory":2}},
		2:{"role":"mosquito","name":"Zeta","alive":true,"state":"flying","p":Vector3(-6.15,0.35,8.3),"yaw":0.0},
		3:{"role":"human","name":"Teo","alive":true,"state":"human","p":Vector3(-4.5,0,8),"yaw":0.0}
	}
	camera = Camera3D.new()
	root.add_child(camera)
	camera.fov = 70.0
	camera.near = 0.025
	camera.position = Vector3(-6.4,0.32,8.43)
	camera.look_at(Vector3(-6.4,0.11,7.8))
	camera.make_current()
	await settle()
	var fallen: ActorView = world.get_actor(1)
	check(fallen.visible and not fallen.stun_sparkles.visible,"flying body visible without stun effects")
	await capture("aturdido-01-vuelo")
	states[1].state = "stunned"
	states[1].p = Vector3(-6.4,0.04,7.8)
	await settle()
	check(fallen.visible and fallen.body_shapes[0].collision_layer==2,"stunned remains visible with original physical body")
	check(is_equal_approx((fallen.body_shapes[0].get_child(0).shape as SphereShape3D).radius,0.04),"visual state keeps physical radius .04")
	check(fallen.stun_blend>0.99 and absf(fallen.model.rotation.z)>1.2,"body inclines gradually to fallen pose")
	check(fallen.left_wing.rotation.y>1.2 and fallen.mosquito_legs.scale.y<0.3,"wings fold and legs relax")
	check(fallen.stun_sparkles.visible and not fallen.name_label.visible,"compact stun effects replace near name")
	check(fallen.help_icon.visible and not fallen.help_icon.no_depth_test,"nearby teammate sees depth-tested help icon")
	check(not world.audio_fx.buzzes[1].playing,"fallen mosquito is silent")
	await capture("aturdido-02-companero-cerca")
	var collider_before: RID = fallen.body_shapes[0].get_rid()
	var materials_before: String = fallen.appearance_signature
	await settle(0.65)
	check(collider_before==fallen.body_shapes[0].get_rid() and materials_before==fallen.appearance_signature,"repeated snapshots preserve collider and materials")
	states[2].role = "human"
	states[2].p = Vector3(-6.1,0,8.3)
	await settle()
	check(not fallen.help_icon.visible,"human observer cannot see teammate help indicator")
	states[2].role = "mosquito"
	states[2].p = Vector3(-6.15,0.35,8.3)
	await settle()
	var occluder := StaticBody3D.new()
	occluder.collision_layer = 1
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(0.6,0.8,0.08)
	shape.shape = box
	occluder.add_child(shape)
	occluder.position = Vector3(-6.35,0.25,8.10)
	root.add_child(occluder)
	await settle()
	check(not fallen.help_icon.visible,"wall occludes icon from camera and teammate body")
	occluder.queue_free()
	await settle()
	states[2].state = "stunned"
	await settle()
	check(not fallen.help_icon.visible,"stunned observer receives no help action icon")
	states[2].state = "flying"
	states[1].state = "flying"
	states[1].p = Vector3(-6.4,0.24,7.8)
	await settle()
	check(fallen.stun_blend<0.01 and absf(fallen.model.rotation.z)<0.01,"recovery returns to upright pose smoothly")
	check(not fallen.stun_sparkles.visible and not fallen.help_icon.visible,"recovery clears all stun indicators")
	check(world.audio_fx.buzzes[1].playing,"recovery restores flight buzz")
	await capture("aturdido-03-recuperado")
	states[1].alive = false
	states[1].state = "dead"
	await settle()
	check(not fallen.visible and fallen.body_shapes[0].collision_layer==0,"survival death remains hidden and noncolliding")
	world.clear_actors()
	await process_frame
	world.queue_free()
	camera.queue_free()
	await process_frame
	await process_frame
	print("STUN_VISUAL05_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
