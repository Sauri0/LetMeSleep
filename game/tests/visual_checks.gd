extends SceneTree

var failures: int = 0
var world: Node3D

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	print("VISUAL_CHECK %s %s" % ["PASS" if condition else "FAIL", label])
	if not condition:
		failures += 1

func _run() -> void:
	world = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	var actors: Dictionary = {
		1: {"role":"human", "name":"Human", "p":Vector3.ZERO, "yaw":0.0, "alive":true},
		2: {"role":"mosquito", "name":"Local", "p":Vector3(0, 1.15, -2), "yaw":0.0, "alive":true}
	}
	world.sync_actors(actors, 2, 1.0)
	var camera := Camera3D.new()
	root.add_child(camera)
	camera.position = Vector3(0, 1.2, -3)
	camera.look_at(Vector3(0, 1.1, 0))
	await physics_frame
	await physics_frame
	var assignment := {"human":1,"zone":0,"p":Vector3(0,1.15,-0.37),"normal":Vector3.FORWARD,"label":"Pecho"}
	world.show_assignment(assignment, camera, Vector3(0,1.15,-2))
	check(world.marker.visible, "private marker visible from assigned face")
	world.show_assignment({}, camera, Vector3(0,1.15,-2))
	check(not world.marker.visible, "empty private assignment hides marker")
	world.show_assignment(assignment, camera, Vector3(0,1.15,2))
	check(not world.marker.visible, "mosquito behind torso cannot see frontal mark")
	camera.position = Vector3(0,1.2,3)
	camera.look_at(Vector3(0,1.1,0))
	world.show_assignment(assignment, camera, Vector3(0,1.15,-2))
	check(not world.marker.visible, "camera behind torso cannot see frontal mark")
	camera.position = Vector3(0,1.2,-7)
	camera.look_at(Vector3(0,1.1,0))
	world.show_assignment(assignment, camera, Vector3(0,1.15,-2))
	check(not world.marker.visible, "camera across wall cannot see mark")
	camera.position = Vector3(0,1.2,-3)
	camera.look_at(Vector3(0,1.1,0))
	actors[3] = {"role":"human", "name":"Occluder", "p":Vector3(0,0,-1), "yaw":0.0, "alive":true}
	world.sync_actors(actors, 2, 1.0)
	await physics_frame
	await physics_frame
	world.show_assignment(assignment, camera, Vector3(0,1.15,-2))
	check(not world.marker.visible, "other human torso occludes mark")
	var exclusions: Array[RID] = []
	check(world._occluded(Vector3(-4,0.35,1), Vector3(-4,0.35,-1), exclusions), "coffee table blocks physical line")
	world.sync_pickups({1:{"tool":"racket","p":Vector3(1,0,1),"holder":0}})
	check(world.pickup_views[1].visible, "unheld tool visible")
	world.sync_pickups({1:{"tool":"racket","p":Vector3(1,0,1),"holder":1}})
	check(not world.pickup_views[1].visible, "held tool removed from ground")
	world.set_local_role(1, "human")
	check(not world.actors[1].head.visible, "local human head hidden for first person")
	check(world.actors[1].model.visible, "local human body retained")
	actors[2]["alive"] = false
	world.sync_actors(actors, 1, 1.0)
	check(not world.actors[2].visible, "dead mosquito hidden")
	actors[2]["alive"] = true
	world.sync_actors(actors, 1, 1.0)
	check(world.actors[2].visible, "respawned mosquito restored")
	print("VISUAL_CHECK_RESULT failures=%d" % failures)
	quit(failures)
