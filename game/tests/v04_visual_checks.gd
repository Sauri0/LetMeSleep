extends SceneTree
const Pose = preload("res://scripts/human_pose.gd")
const Simulation = preload("res://scripts/simulation.gd")
const Maps = preload("res://scripts/map_catalog.gd")

var world: Node3D
var camera: Camera3D
var failures: int = 0
var checks: int = 0
var capture_screens: bool = false

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	print("V04_VISUAL %s %s" % ["PASS" if ok else "FAIL", message])
	if not ok:
		failures += 1

func settle() -> void:
	await physics_frame
	await physics_frame
	await process_frame

func human(extra: Dictionary = {}) -> Dictionary:
	var result: Dictionary = {"role":"human","name":"Human","p":Vector3.ZERO,"yaw":0.0,"alive":true,"tool":"hands","grounded":true}
	result.merge(extra, true)
	return result

func capture(name: String) -> void:
	if not capture_screens:
		return
	await settle()
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://../work/v04-" + name + ".png")) == OK, "capture " + name)

func _run() -> void:
	capture_screens = "--screens" in OS.get_cmdline_user_args() and DisplayServer.get_name() != "headless"
	world = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	camera = Camera3D.new()
	camera.near = 0.025
	camera.fov = 70.0
	root.add_child(camera)
	camera.current = true
	await settle()
	check(world.current_map == "house" and world.map_data.bounds.size.is_equal_approx(Vector3(28,6.4,22)), "large two-floor map loaded")
	var authored: int = 0
	for child: Node in world.map_root.get_children():
		if child.has_meta("catalog_box"):
			authored += 1
	check(authored == Maps.HOUSE.structures.size(), "each catalog solid has one rendered furniture or structure root")
	check(world.map_root.find_children("*","StaticBody3D",true,false).size() == Maps.HOUSE.obstacles.size() + 6, "physical structures use every catalog obstacle plus perimeter")
	var variants: Dictionary = {
		"idle":{}, "walk":{"motion_phase":PI/2,"motion_speed":3.1},
		"run":{"motion_phase":PI*1.5,"motion_speed":5.0,"sprinting":true},
		"crouch":{"crouch_amount":1.0}, "jump":{"grounded":false,"p":Vector3(0,0.4,0)},
		"clap":{"swing":0.62}, "racket":{"tool":"racket","swing":0.87}
	}
	world.set_local_role(0,"lobby")
	for label: String in variants:
		var state: Dictionary = human(variants[label])
		world.sync_actors({1:state},0,1.0)
		await settle()
		var aligned: bool = true
		var exposed: bool = true
		for i: int in range(Simulation.BODY_ZONES.size()):
			var assignment: Dictionary = Pose.zone_pose(state,Simulation.BODY_ZONES[i])
			assignment.human = 1
			assignment.zone = i
			var point: Vector3 = assignment.p
			var normal: Vector3 = assignment.normal
			var query := PhysicsRayQueryParameters3D.create(point+normal*0.2,point-normal*0.025,2)
			var hit: Dictionary = world.get_world_3d().direct_space_state.intersect_ray(query)
			if hit.is_empty() or Vector3(hit.position).distance_to(point) > 0.015:
				aligned = false
				print("V04_SURFACE_MISMATCH %s %s %s" % [label,assignment.label,str(hit)])
			camera.position = point + normal * 1.1
			camera.look_at(point)
			world.show_assignment(assignment,camera,point+normal*0.5)
			if not world.marker.visible:
				exposed = false
				print("V04_HIDDEN_ZONE %s %s" % [label,assignment.label])
		check(aligned,label+" all22 zones touch shared physics surface within15mm")
		check(exposed,label+" all22 private zones visible from exposed face")
	world.sync_actors({1:human(),2:{"role":"mosquito","name":"Mosquito","p":Vector3(0,1.2,-0.8),"alive":true,"state":"flying","appearance":{"color":4,"accessory":2}}},2,1.0)
	var mosquito: ActorView = world.get_actor(2)
	var shape: SphereShape3D = (mosquito.body_shapes[0].get_child(0) as CollisionShape3D).shape
	check(mosquito.model.scale.is_equal_approx(Vector3.ONE*0.35) and is_equal_approx(shape.radius,0.04),"mosquito complete model reduced65percent and collider radius4cm")
	var insect_bounds: AABB
	var first_mesh: bool = true
	for mesh: MeshInstance3D in mosquito.model.find_children("*","MeshInstance3D",true,false):
		var bounds: AABB = (mosquito.global_transform.affine_inverse()*mesh.global_transform)*mesh.get_aabb()
		insect_bounds = bounds if first_mesh else insect_bounds.merge(bounds)
		first_mesh = false
	print("V04_MOSQUITO_BOUNDS new=%s previous_scale_equivalent=%s" % [insect_bounds.size,insect_bounds.size/0.35])
	var origin: Vector3 = mosquito.model.position
	for i: int in range(20):
		mosquito.update_state({"p":mosquito.global_position,"alive":true,"state":"flying"},0.05)
	check(origin.is_equal_approx(mosquito.model.position),"flight wing animation adds no body drift or bob")
	await _sequence()
	world.clear_actors()
	await create_timer(0.12).timeout
	world.queue_free()
	camera.queue_free()
	await process_frame
	await create_timer(0.12).timeout
	print("V04_VISUAL_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func _sequence() -> void:
	world.clear_actors()
	var simulation := Simulation.new()
	simulation.start({1:{"name":"Alex","role":"human"},2:{"name":"Mora","role":"mosquito"}},Simulation.DEFAULT_CONFIG)
	simulation.actors[1].p = Vector3(-6.5,0,8.1)
	simulation.actors[1].yaw = 0.0
	simulation.actors[2]._assignment = {"human":1,"zone":5,"revision":1}
	var assignment: Dictionary = simulation.private_for(2).assignment
	var point: Vector3 = assignment.p
	var normal: Vector3 = assignment.normal
	simulation.actors[2].p = point + normal * 0.85
	simulation.actors[2].yaw = atan2(normal.x,normal.z)
	simulation.actors[2].pitch = asin(-normal.y)
	world.set_local_role(2,"mosquito")
	world.sync_actors(simulation.public_snapshot().actors,2,1.0)
	camera.position = Vector3(-4.7,1.55,5.98)
	camera.look_at(Vector3(-6.5,0.95,8.1))
	await capture("size-comparison")
	var sequence: int = 0
	for phase: String in ["approach","charging","biting"]:
		if phase != "approach":
			for tick: int in range(36 if phase == "charging" else 55):
				sequence += 1
				simulation.submit_input(2,sequence,Vector3.ZERO,simulation.actors[2].yaw,simulation.actors[2].pitch,true)
				simulation.step(1.0/60.0)
		world.sync_actors(simulation.public_snapshot().actors,2,1.0)
		var position: Vector3 = simulation.actors[2].p
		camera.position = position + normal * 0.75 + Vector3(0.18,0.12,0)
		camera.look_at(position - normal * 0.25)
		await settle()
		var focus: Dictionary = simulation.private_for(2).focus
		if phase == "charging":
			check(float(focus.progress) > 0.1 and float(focus.progress) < 0.9,"held focus genuinely charges in authoritative simulation")
		if phase == "biting":
			check(simulation.actors[2].state == "biting", "held focus completes assisted attachment in authoritative simulation")
		world.show_assignment(assignment,camera,position,focus)
		check(world.marker.visible,"private focus mark remains visible at "+phase)
		await capture(phase)
	world.clear_actors()
	var player: Dictionary = human({"p":Vector3(-5.4,0,8.5),"pitch":-0.2,"tool":"swatter"})
	world.set_local_role(1,"human")
	world.sync_actors({1:player},1,1.0)
	camera.position = Vector3(player.p) + Vector3(Pose.sample(player).eye)
	camera.rotation = Vector3(-0.2,0,0)
	await capture("human-fps")
	world.clear_actors()
	camera.position = Vector3(-11,1.55,-5.15)
	camera.look_at(Vector3(-11,2.7,2.7))
	await capture("stairs-lower")
	camera.position = Vector3(-11,4.75,4.9)
	camera.look_at(Vector3(-11,2.5,-2))
	await capture("stairs-upper")
	camera.position = Vector3(3.0,4.75,6.2)
	camera.look_at(Vector3(6.2,4.05,9.6))
	await capture("bedroom-upper")
