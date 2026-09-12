extends SceneTree
## Loads the canonical authored map through World and verifies the visual
## environment consumes the same metadata and collision ownership as gameplay.

const WorldScript = preload("res://scripts/world.gd")
const Maps = preload("res://scripts/map_catalog.gd")

var checks := 0
var failures: Array[String] = []

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		printerr("ENVIRONMENT094_WORLD_FAIL " + label)

func _initialize() -> void:
	_run.call_deferred()

func _count_meta(nodes: Array[Node], key: String) -> int:
	var count := 0
	for node: Node in nodes:
		count += 1 if node.has_meta(key) else 0
	return count

func _run() -> void:
	var expected: Dictionary = Maps.get_map("house-patio-v1")
	var world: Node3D = WorldScript.new()
	root.add_child(world)
	world.build()
	world.load_map("house-patio-v1")
	await process_frame

	_check(world.current_map == "house-patio-v1", "World loads the canonical authored map")
	_check(world.map_data.authored_version == 1 and not world.map_data.has("generator_version"),
		"World preserves authored identity without a generator contract")
	_check(world.map_data.bounds == AABB(Vector3(-20, 0, -20), Vector3(40, 8.8, 40)),
		"World keeps the 40 m playable lot")
	_check(world._house_building_bounds() == AABB(Vector3(-12, 0, -10), Vector3(24, 6.4, 20)),
		"World keeps the smaller building footprint")

	var exterior: Node = world.map_root.get_node_or_null("AuthoredExterior")
	_check(is_instance_valid(exterior), "authored exterior root exists")
	var report: Dictionary = exterior.get_meta("build_report", {}) if is_instance_valid(exterior) else {}
	_check(report.get("areas", -1) == expected.exterior.areas.size()
		and report.get("requested_areas", -1) == expected.exterior.areas.size(),
		"all authored exterior areas render")
	_check(report.get("path_segments", -1) == expected.exterior.paths.size()
		and report.get("requested_paths", -1) == expected.exterior.paths.size(),
		"all straight authored paths render once")
	_check(report.get("props", -1) == expected.exterior.props.size()
		and report.get("requested_props", -1) == expected.exterior.props.size(),
		"all alfa props instantiate")
	_check(Array(report.get("errors", [])).is_empty(), "authored exterior reports no invalid assets")
	_check(bool(report.get("visual_only", false)) and report.get("collision_source", "") == "map obstacles",
		"authored exterior leaves collision authority with map obstacles")

	var meshes: Array[Node] = exterior.find_children("*", "MeshInstance3D", true, false) if is_instance_valid(exterior) else []
	var area_meshes := meshes.filter(func(node: Node) -> bool: return node.get_meta("catalog_kind", "") == "exterior_area")
	var path_meshes := meshes.filter(func(node: Node) -> bool: return node.get_meta("catalog_kind", "") == "exterior_path")
	_check(area_meshes.size() == expected.exterior.areas.size(), "each exterior area owns one ground mesh")
	_check(path_meshes.size() == expected.exterior.paths.size(), "each path owns one surface mesh")
	for mesh: MeshInstance3D in area_meshes:
		var source: AABB = mesh.get_meta("source_bounds")
		_check(is_equal_approx((mesh.mesh as BoxMesh).size.y, .036) and is_equal_approx(source.size.y, 8.8),
			"semantic outdoor volume renders as a thin ground face")

	var placements: Array[Node] = exterior.find_children("ExteriorProp_*", "Node3D", true, false) if is_instance_valid(exterior) else []
	_check(placements.size() == expected.exterior.props.size(), "prop placement count matches authored metadata")
	var placements_by_id: Dictionary = {}
	for placement: Node3D in placements:
		placements_by_id[str(placement.get_meta("exterior_id", ""))] = placement
	for prop: Dictionary in expected.exterior.props:
		var placement: Node3D = placements_by_id.get(str(prop.id)) as Node3D
		_check(is_instance_valid(placement) and placement.position == Vector3(prop.p)
			and is_equal_approx(placement.rotation.y, float(prop.yaw)) and placement.scale == Vector3(prop.scale),
			"prop transform follows metadata: " + str(prop.id))
		_check(is_instance_valid(placement) and placement.get_child_count() == 1
			and placement.get_child(0).get_meta("alfa_asset_id", "") == prop.asset_id,
			"prop resolves through alfa library: " + str(prop.id))

	var all_meshes: Array[Node] = world.map_root.find_children("*", "MeshInstance3D", true, false)
	var proxy_visuals := all_meshes.filter(func(node: Node) -> bool:
		return node.get_meta("catalog_kind", "") in ["tree_trunk", "outdoor_furniture"])
	_check(proxy_visuals.is_empty(), "map collision proxies are hidden behind alfa prop visuals")

	var ceilings := all_meshes.filter(func(node: Node) -> bool: return node.get_meta("catalog_kind", "") == "ceiling")
	var roof_box: AABB = ceilings[0].get_meta("catalog_box") if ceilings.size() == 1 else AABB()
	var building: AABB = expected.building_bounds
	_check(ceilings.size() == 1 and roof_box.position.x == building.position.x
		and roof_box.position.z == building.position.z and roof_box.size.x == building.size.x
		and roof_box.size.z == building.size.z and is_equal_approx(roof_box.position.y, building.end.y),
		"the only roof visual covers the building footprint")
	_check(world.map_root.get_node_or_null("NightExterior") == null,
		"authored map does not receive the legacy exterior shell")

	var lights: Array[Node] = world.map_root.find_children("*", "SpotLight3D", true, false)
	var horizontal_corridors := 0
	for corridor: AABB in expected.corridors:
		horizontal_corridors += 1 if corridor.size.x >= corridor.size.z else 0
	_check(_count_meta(lights, "house_room") == expected.rooms.size(), "one bounded light per authored room")
	_check(_count_meta(lights, "house_hall") == horizontal_corridors, "horizontal corridors drive hall lights")
	_check(_count_meta(lights, "house_stair") == expected.stair_light_anchors.size(), "authored anchors drive stair lights")
	var light_report: Dictionary = world.map_root.get_meta("generated_stair_lighting", {})
	_check(int(light_report.get("local_lights", 99)) <= int(light_report.get("compatibility_cap", 0)),
		"authored lighting stays inside the Compatibility light budget")

	_check(world.map_root.has_meta("frame_joinery"), "frame joinery resolves after final geometry")
	_check(world.map_root.get_node_or_null("HouseOcclusion") != null, "house occlusion builds after final geometry")
	var occlusion_report: Dictionary = world.map_root.get_node("HouseOcclusion").get_meta("geometry", {})
	_check(int(occlusion_report.get("meshes", 0)) > 0 and int(occlusion_report.get("triangles", 0)) > 0,
		"house occlusion contains rendered wall and floor triangles")

	world.queue_free()
	await process_frame
	print("ENVIRONMENT094_WORLD checks=%d failures=%d" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
