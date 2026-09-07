extends SceneTree
## Shared-pose integration, physics map isolation and native rendered evidence.
const Pose = preload("res://scripts/human_pose.gd")
const Simulation = preload("res://scripts/simulation.gd")

var world: Node3D
var camera: Camera3D
var failures: int = 0
var checks: int = 0
var capture_screens: bool = false

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	checks += 1
	print("POSE_VISUAL %s %s" % ["PASS" if condition else "FAIL", label])
	if not condition:
		failures += 1

func settle() -> void:
	await physics_frame
	await physics_frame
	await process_frame

func _human(extra: Dictionary = {}) -> Dictionary:
	var data := {"role":"human", "name":"Pose", "p":Vector3.ZERO, "yaw":0.0, "pitch":0.0, "alive":true, "tool":"hands", "grounded":true}
	data.merge(extra, true)
	return data

func _wall_hit() -> bool:
	var query := PhysicsRayQueryParameters3D.create(Vector3(0, 1.6, 0), Vector3(4.5, 1.6, 0), 1)
	return not world.get_world_3d().direct_space_state.intersect_ray(query).is_empty()

func _collider_count() -> int:
	return world.map_root.find_children("*", "StaticBody3D", true, false).size()

func _run() -> void:
	capture_screens = "--screens" in OS.get_cmdline_user_args() and DisplayServer.get_name() != "headless"
	world = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	camera = Camera3D.new()
	camera.near = 0.035
	camera.fov = 74.0
	root.add_child(camera)
	await settle()
	check(world.current_map == "lobby" and _collider_count() == 8, "initial courtyard contains catalog boundary and two benches")
	check(world.map_data.pickups.is_empty() and world.map_data.stations.is_empty(), "lobby contains no playable stations or pickups")
	check(_wall_hit(), "lobby edge blocks at four metres")
	var old_map: Node3D = world.map_root
	world.load_map("house")
	check(not old_map.is_inside_tree(), "old map colliders leave physics tree immediately")
	await settle()
	check(world.current_map == "house" and _collider_count() == world.map_data.obstacles.size() + 6 and not _wall_hit(), "house replaces smaller lobby bounds and authored furniture")
	world.load_map("unknown")
	check(world.current_map == "house", "unknown map preserves current map")
	for i: int in range(4):
		world.load_map("lobby" if i % 2 == 0 else "house")
		await settle()
	check(world.find_children("Map_*", "Node3D", false, false).size() == 1 and _collider_count() == world.map_data.obstacles.size() + 6, "repeated map changes retain one geometry root")
	world.set_local_role(0, "lobby")
	var poses: Dictionary = {
		"idle": _human(),
		"walk": _human({"motion_phase":PI / 2, "motion_speed":3.1}),
		"run": _human({"motion_phase":PI / 2, "motion_speed":5.0, "sprinting":true}),
		"crouch": _human({"crouch_amount":1.0, "crouching":true, "motion_phase":PI / 2, "motion_speed":1.55}),
		"jump": _human({"p":Vector3(0,0.48,0), "grounded":false, "velocity":Vector3(0,2.0,0)}),
		"swat": _human({"swing":0.62, "tool":"hands", "pitch":0.4}),
		"racket": _human({"swing":0.87, "tool":"racket"})
	}
	var view: ActorView
	for pose_name: String in poses:
		var data: Dictionary = poses[pose_name]
		world.sync_actors({1:data}, 0, 1.0)
		view = world.get_actor(1)
		var expected: Dictionary = Pose.sample(data)
		var valid: bool = view.head.position.is_equal_approx(expected.head) and view.head.basis.is_equal_approx(expected.head_basis) and view.torso_node.position.is_equal_approx(expected.torso)
		for suffix: String in ["l", "r"]:
			valid = valid and view.limb_hands[suffix].position.is_equal_approx(expected["hand_" + suffix])
			valid = valid and view.limb_meshes["shin_" + suffix].position.is_equal_approx((Vector3(expected["knee_" + suffix]) + Vector3(expected["ankle_" + suffix])) * 0.5)
			valid = valid and view.pose_colliders["forearm_" + suffix].position.is_equal_approx(view.limb_meshes["forearm_" + suffix].position)
		check(valid, pose_name + " mesh joints, face and occlusion colliders match authoritative pose")
		await settle()
		var marks_visible: bool = true
		for zone_id: int in range(Simulation.BODY_ZONES.size()):
			var assignment: Dictionary = Pose.zone_pose(data, Simulation.BODY_ZONES[zone_id])
			assignment["human"] = 1
			assignment["zone"] = zone_id
			var point: Vector3 = assignment.p
			var normal: Vector3 = assignment.normal
			camera.position = point + normal * 1.0
			camera.look_at(point)
			world.show_assignment(assignment, camera, point + normal * 0.9)
			if not world.marker.visible:
				print("POSE_MARK_OCCLUDED %s %s" % [pose_name, assignment.label])
				marks_visible = false
		check(marks_visible, pose_name + " all assigned body zones visible from their unobstructed face")
	var idle: Dictionary = Pose.sample(poses.idle)
	var walk: Dictionary = Pose.sample(poses.walk)
	var run: Dictionary = Pose.sample(poses.run)
	var crouch: Dictionary = Pose.sample(poses.crouch)
	var jump: Dictionary = Pose.sample(poses.jump)
	check(absf(walk.ankle_l.z - walk.ankle_r.z) > 0.15 and absf(run.ankle_l.z) > absf(walk.ankle_l.z), "walk alternates steps and run increases stride")
	check(crouch.eye.y < idle.eye.y - 0.45 and crouch.pelvis.y < idle.pelvis.y - 0.2, "crouch lowers eye, torso and pelvis")
	check(jump.knee_l.z < idle.knee_l.z - 0.05 and jump.ankle_l.y > idle.ankle_l.y + 0.04, "airborne pose tucks knees and lifts feet")
	for running: bool in [false, true]:
		var first_step: Dictionary = Pose.sample(_human({"motion_phase":PI/2,"motion_speed":5.0 if running else 3.1,"sprinting":running}))
		var second_step: Dictionary = Pose.sample(_human({"motion_phase":PI*1.5,"motion_speed":5.0 if running else 3.1,"sprinting":running}))
		check(first_step.ankle_l.z * second_step.ankle_l.z < 0.0 and first_step.ankle_r.z * second_step.ankle_r.z < 0.0, ("run" if running else "walk") + " opposite gait phases exchange supporting foot")
	world.set_local_role(1, "human")
	check(not view.head.visible and not view.left_arm.visible and view.fps_root.visible and view.model.visible, "first person retains torso and legs with separate visible arms")
	world.sync_actors({1:_human({"swing":0.8})}, 1, 1.0)
	var resting_arm_gap: float = view.fps_right_arm.position.x - view.fps_left_arm.position.x
	world.sync_actors({1:_human({"swing":0.62})}, 1, 1.0)
	check(view.fps_right_arm.position.x - view.fps_left_arm.position.x < resting_arm_gap * 0.4 and Vector3(view.body_pose.hand_l).distance_to(view.body_pose.hand_r) < 0.2, "palmada midpoint closes palms in shared body and first person arms")
	world.sync_actors({1:_human({"swing":0.0})}, 1, 1.0)
	check(is_equal_approx(view.fps_right_arm.position.x - view.fps_left_arm.position.x, resting_arm_gap), "completed palmada restores first person resting arms")
	world.sync_actors({1:_human({"tool":"racket"})}, 1, 1.0)
	check(is_instance_valid(view.held_tool) and is_instance_valid(view.fps_held_tool) and view.current_tool == "racket", "equipped tool has both world and first person representation")
	world.set_local_role(1, "lobby")
	check(view.head.visible and view.left_arm.visible and not view.fps_root.visible, "lobby restores complete third person body")
	world.sync_actors({1:_human(),2:{"role":"mosquito","p":Vector3(0,1.55,-0.3),"name":"BOT Mora","alive":true}}, 1, 1.0)
	var insect: ActorView = world.get_actor(2)
	camera.position = Vector3(0,1.55,0)
	insect.update_name_visibility(camera)
	check(not insect.name_label.visible and insect.model.visible and insect.body_shapes[0].collision_layer == 2, "close mosquito label hides without changing body or hitbox")
	camera.position = Vector3(0,1.55,2)
	insect.update_name_visibility(camera)
	check(insect.name_label.visible, "mosquito label restores at readable distance")
	if capture_screens:
		await _screens(poses)
	world.clear_actors()
	# The native audio mixer releases stopped one-shot playbacks on its own tick.
	await create_timer(0.12).timeout
	world.queue_free()
	camera.queue_free()
	await process_frame
	await process_frame
	await create_timer(0.12).timeout
	print("POSE_VISUAL_RESULT checks=%d failures=%d" % [checks, failures])
	quit(failures)

func _capture(filename: String) -> void:
	await settle()
	await RenderingServer.frame_post_draw
	var path: String = ProjectSettings.globalize_path("res://../work/" + filename)
	check(root.get_texture().get_image().save_png(path) == OK, "native capture " + filename)

func _screens(poses: Dictionary) -> void:
	world.clear_actors()
	world.load_map("lobby")
	world.set_local_role(1, "lobby")
	var lineup: Dictionary = {}
	for i: int in range(4):
		lineup[i + 1] = _human({"name":["Pausa", "Corriendo", "Agachado", "Salto"][i], "p":Vector3(-2.25 + i * 1.5, 0.35 if i == 3 else 0.0, -0.5), "yaw":PI, "appearance":{"color":i,"accessory":i % 3}})
	lineup[2].merge({"motion_phase":PI/2, "motion_speed":5.0, "sprinting":true}, true)
	lineup[3].merge({"crouch_amount":1.0, "crouching":true}, true)
	lineup[4].grounded = false
	world.sync_actors(lineup, 1, 1.0)
	camera.position = Vector3(0, 2.45, 2.75)
	camera.look_at(Vector3(0, 1.2, -0.9))
	camera.current = true
	await _capture("v03-lobby-rig.png")
	lineup[2].motion_phase = PI * 1.5
	lineup[1].swing = 0.8
	world.sync_actors(lineup, 1, 1.0)
	lineup[1].swing = 0.62
	world.sync_actors(lineup, 1, 1.0)
	await _capture("v03-lobby-rig-next-step.png")
	world.clear_actors()
	world.load_map("house")
	var human: Dictionary = _human({"p":Vector3(0,0,1.9), "yaw":0.0, "pitch":-0.35, "tool":"swatter"})
	world.set_local_role(1, "human")
	world.sync_actors({1:human}, 1, 1.0)
	camera.position = Vector3(human.p) + Vector3(Pose.sample(human).eye)
	camera.rotation = Vector3(human.pitch, human.yaw, 0)
	await _capture("v03-human-fps.png")
	human.tool = "hands"
	human.swing = 0.8
	world.sync_actors({1:human}, 1, 1.0)
	human.swing = 0.62
	world.sync_actors({1:human}, 1, 1.0)
	await _capture("v03-human-clap-mid.png")
	human.swing = 0.0
	human.crouch_amount = 1.0
	world.sync_actors({1:human}, 1, 1.0)
	camera.position = Vector3(human.p) + Vector3(Pose.sample(human).eye)
	await _capture("v03-human-crouch-eye.png")
	human = _human({"name":"Compartimos la pose", "crouch_amount":1.0, "motion_phase":PI/2, "motion_speed":1.55, "tool":"racket"})
	var assignment: Dictionary = Pose.zone_pose(human, Simulation.BODY_ZONES[7])
	assignment["human"] = 1
	assignment["zone"] = 7
	var mosquito: Dictionary = {"role":"mosquito", "name":"", "p":Vector3(assignment.p) + Vector3(-0.45,0.10,-0.62), "yaw":PI, "alive":true,"state":"flying"}
	world.set_local_role(2, "mosquito")
	world.sync_actors({1:human,2:mosquito}, 2, 1.0)
	camera.position = Vector3(-0.25, 1.2, -2.5)
	camera.look_at(Vector3(0,0.65,0))
	await settle()
	world.show_assignment(assignment, camera, mosquito.p)
	check(world.marker.visible, "mosquito screenshot retains private forearm mark on crouched human")
	await _capture("v03-mosquito-crouch.png")
