extends SceneTree
## Actual authoritative focus/attachment and click gesture, real Client camera.
const Simulation = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Transport = preload("res://tests/v05_baseline_audit.gd").Transport
var client: Node
var simulation: RefCounted
var checks := 0
var failures := 0
var output_dir: String

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("CHARACTER_CLIENT07 %s %s" % ["PASS" if ok else "FAIL",message])

func settle() -> void:
	for i: int in range(12):
		await process_frame
	await physics_frame

func show_snapshot() -> void:
	client._snapshot(simulation.public_snapshot())
	client.yaw = float(simulation.actors[1].yaw)
	client.pitch = float(simulation.actors[1].pitch)
	client.personal = simulation.private_for(1)
	client.ui.show_game(client.state,client.personal,1)
	await settle()

func capture(file_name: String) -> void:
	if DisplayServer.get_name()=="headless":
		return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(output_dir.path_join(file_name+".png"))==OK,"capture "+file_name)

func _run() -> void:
	root.size = Vector2i(1280,720)
	output_dir = ProjectSettings.globalize_path("res://../outputs/0.7-animacion/cliente")
	DirAccess.make_dir_recursive_absolute(output_dir)
	var transport := Transport.new()
	root.add_child(transport)
	client = load("res://scripts/client.gd").new()
	client.network = transport
	client.options = {"visual-preview":"human"}
	root.add_child(client)
	client.set_process_unhandled_input(false)
	client.local_id = 1
	for posture: String in ["stand","crouch"]:
		for zone_id: int in range(Simulation.FRONT_ZONE_COUNT):
			# Each case starts a new authoritative round. Do not interpolate its
			# actors from the prior case's unrelated target or death position.
			client.world.clear_actors()
			simulation = Simulation.new()
			simulation.start({1:{"name":"Luna","role":"human"},2:{"name":"Zeta","role":"mosquito"},3:{"name":"Lejos","role":"mosquito"}},Simulation.DEFAULT_CONFIG)
			var human: Dictionary = simulation.actors[1]
			human.appearance = {"color":5,"accent":4,"face":1,"hair":0,"outfit":0,"accessory":3,"footwear":0}
			human.p = Vector3(-6.4,0,7.8)
			human.yaw = 0.0
			human.body_yaw = 0.0
			human.crouch_amount = 1.0 if posture=="crouch" else 0.0
			simulation.actors[3].p = Vector3(5,4.5,-8)
			simulation.actors[2]._assignment = {"human":1,"zone":zone_id,"revision":1}
			var assignment: Dictionary = simulation.private_for(2).assignment
			simulation.actors[2].p = assignment.p+assignment.normal*0.30
			simulation.actors[2].yaw = atan2(assignment.normal.x,assignment.normal.z)
			simulation.actors[2].pitch = asin(-assignment.normal.y)
			var sequence := 0
			for tick: int in range(130):
				sequence += 1
				simulation.submit_input(1,sequence,Vector3.ZERO,0.0,0.0,false,false,posture=="crouch")
				simulation.submit_input(2,sequence,Vector3.ZERO,simulation.actors[2].yaw,simulation.actors[2].pitch,true)
				simulation.step(1.0/60.0)
				if simulation.actors[2].state=="biting":
					break
			check(simulation.actors[2].state=="biting","authoritative held-E attachment "+posture+str(zone_id))
			for adjustment: int in range(3):
				sequence += 1
				var aim: Vector2 = Pose.aim_angles(human,simulation.actors[2].p)
				simulation.submit_input(1,sequence,Vector3.ZERO,aim.x,aim.y,false,false,posture=="crouch")
				simulation.step(1.0/60.0)
			await show_snapshot()
			var position: Vector3 = simulation.actors[2].p
			var screen: Vector2 = client.camera.unproject_position(position)
			var center: Vector2 = root.get_visible_rect().size*0.5
			check(screen.distance_to(center)<3.0 and not client.camera.is_position_behind(position),"mosquito centered in real camera "+posture+str(zone_id))
			check(client.camera.global_position.distance_to(Pose.view_origin(human))<0.002,"camera uses server view origin "+posture+str(zone_id))
			var query := PhysicsRayQueryParameters3D.create(client.camera.global_position,position,2)
			var hit: Dictionary = client.world.get_world_3d().direct_space_state.intersect_ray(query)
			var insect: ActorView = client.world.get_actor(2)
			check(not hit.is_empty() and hit.collider==insect.body_shapes[0],"rendered body cannot hide target ray "+posture+str(zone_id))
			await capture("propio-%s-zona-%d" % [posture,zone_id])
			if posture=="stand" and zone_id==4:
				simulation.action(1,1,"attack",human.yaw,human.pitch)
				for frame: int in range(24):
					simulation.step(1.0/60.0)
					if frame in [0,5,10,17,23]:
						await show_snapshot()
						await capture("gesto-manual-%02d" % frame)
				check(bool(simulation.actors[2].alive) and simulation.actors[2].state=="stunned","manual LMB stuns the mosquito actually under reticle in blood mode")
	client.playing = false
	client.world.clear_actors()
	await settle()
	client.queue_free()
	await process_frame
	await process_frame
	print("CHARACTER_CLIENT07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
