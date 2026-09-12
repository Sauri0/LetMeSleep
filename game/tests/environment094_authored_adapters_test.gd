extends SceneTree
## Synthetic authored-map contract. It proves World distinguishes the building
## from the playable lot before the canonical house-patio-v1 data is integrated.

const WorldScript = preload("res://scripts/world.gd")
const Details = preload("res://scripts/house_details.gd")

var checks := 0
var failures: Array[String] = []

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		printerr("ENVIRONMENT094_AUTHORED_FAIL " + label)

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var building := AABB(Vector3(-12, 0, -10), Vector3(24, 6.4, 20))
	var lot := AABB(Vector3(-20, 0, -20), Vector3(40, 8.8, 40))
	var obstacles: Array[AABB] = [
		AABB(Vector3(-12, -0.2, -10), Vector3(24, 0.2, 20)),
		AABB(Vector3(-12, 6.4, -10), Vector3(24, 0.2, 20)),
	]
	var data := {
		"id": "house-patio-v1", "authored_version": 1,
		"bounds": lot, "building_bounds": building,
		"half_x": 20.0, "half_z": 20.0, "ceiling": 8.8,
		"floor_levels": [0.0, 3.2], "obstacles": obstacles,
		"rooms": [{"name": "Estar", "floor": 0, "bounds": AABB(Vector3(-6, 0, -5), Vector3(5, 3, 5))}],
		"corridors": [AABB(Vector3(-1.2, 0, -8), Vector3(2.4, 3, 16))],
		"stair_connections": [{"bottom": Vector3(-8, 0, -2), "top": Vector3(-8, 3.2, 2)}],
		"stair_light_anchors": [{"id": "main-stair-light", "p": Vector3(-8, 4.8, 0),
			"target": Vector3(-8, 1.8, 0), "range": 6.5}],
		"windows": [{"id": "patio-window", "p": Vector3(4, 1.55, 10), "axis": 2,
			"rear": true, "room": "Estar", "tint": Color("8aa6a0")}],
		"exterior": {"bounds": lot,
			"areas": [{"id": "patio-grass", "kind": "garden",
				"bounds": AABB(Vector3(-12, -.08, 10), Vector3(24, .08, 10))}],
			"paths": [{"id": "patio-path", "kind": "stone", "surface": "stone",
				"points": [Vector3(0, 0, 9.9), Vector3(0, 0, 18)], "from": Vector3(0, 0, 9.9),
				"to": Vector3(0, 0, 18), "width": 2.6}], "props": []},
		"structures": [], "pickup_supports": [],
	}
	var world: Node3D = WorldScript.new()
	root.add_child(world)
	world.map_data = data
	world.current_map = "house-patio-v1"
	world.map_root = Node3D.new()
	world.add_child(world.map_root)
	_check(world._house_building_bounds() == building, "building_bounds wins over 40 m lot")
	var roof: AABB = world._fallback_roof_box()
	_check(roof.position == Vector3(-12, 6.4, -10) and roof.size == Vector3(24, .15, 20),
		"fallback roof covers only building at physical roof height")
	world._build_map_colliders()
	var bodies: Array[Node] = world.map_root.find_children("*", "StaticBody3D", true, false)
	_check(bodies.size() == obstacles.size(), "authored map creates no implicit lot floor roof or walls")
	for index: int in range(bodies.size()):
		var shape := bodies[index].get_child(0) as CollisionShape3D
		_check(bodies[index].position == obstacles[index].get_center() and (shape.shape as BoxShape3D).size == obstacles[index].size,
			"authored obstacle %d remains exact" % index)
	var windows := Details.window_specs(data)
	_check(windows.size() == 1 and windows[0].id == "patio-window" and windows[0].p == Vector3(4, 1.55, 10),
		"explicit authored windows pass through adapter")
	var without_windows := data.duplicate(true)
	without_windows.erase("windows")
	_check(Details.window_specs(without_windows).is_empty(), "authored map never receives legacy magic windows")
	var exterior_report := Details.build_authored_exterior(world)
	_check(exterior_report.areas == 1 and exterior_report.path_segments == 1 and exterior_report.props == 0,
		"authored exterior builds exact metadata surfaces without optional props")
	_check(bool(exterior_report.visual_only) and exterior_report.collision_source == "map obstacles",
		"exterior adapter declares map obstacles as sole collision source")
	var exterior_meshes: Array[Node] = world.map_root.get_node("AuthoredExterior").find_children("*", "MeshInstance3D", true, false)
	_check(exterior_meshes.size() == 2, "one garden area and one straight path segment are visible")
	_check(world.map_root.find_children("*", "StaticBody3D", true, false).size() == obstacles.size(),
		"visual exterior adds no duplicate collision")
	world._build_generated_lighting()
	var room_lights := 0
	var hall_lights := 0
	var stair_lights := 0
	for node: Node in world.map_root.find_children("*", "SpotLight3D", true, false):
		room_lights += 1 if node.has_meta("house_room") else 0
		hall_lights += 1 if node.has_meta("house_hall") else 0
		stair_lights += 1 if node.has_meta("house_stair") else 0
	_check(room_lights == 1 and hall_lights == 0 and stair_lights == 1,
		"authored rooms and stair anchors drive bounded lights without fixed coordinates")
	var stair: SpotLight3D
	for node: Node in world.map_root.find_children("*", "SpotLight3D", true, false):
		if node.has_meta("house_stair"):
			stair = node as SpotLight3D
	_check(is_instance_valid(stair) and stair.position == Vector3(-8, 4.8, 0) and stair.spot_range == 6.5,
		"authored stair light preserves anchor position and range")
	world.queue_free()
	await process_frame
	print("ENVIRONMENT094_AUTHORED checks=%d failures=%d" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
