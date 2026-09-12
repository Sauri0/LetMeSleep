extends SceneTree
## Gate independiente 0.9.4 alfa para el catalogo fijo. Esta prueba no
## acredita geometria, render, red, rendimiento ni una partida completa.

const Maps = preload("res://scripts/map_catalog.gd")
const MAP_ID := "house-patio-v1"
const MAP_LABEL := "Casa con patio"
const EXPECTED_AUTHORED_VERSION := 1
const EXPECTED_BOUNDS := AABB(Vector3(-20, 0, -20), Vector3(40, 8.8, 40))
const EXPECTED_BUILDING_BOUNDS := AABB(Vector3(-12, 0, -10), Vector3(24, 6.4, 20))
const REJECTED_IDS: Array[String] = [
	"", "house", "house-v2-1", "house-v3-1", "house-v3-7",
	"house-v4-1", "house-patio-v1-1", "../house-patio-v1",
]

var checks := 0
var failures: Array[String] = []
var report_path := ""


func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):
			report_path = argument.trim_prefix("--report=")
	_run.call_deferred()


func check(ok: bool, label: String) -> bool:
	checks += 1
	if not ok:
		failures.append(label)
		printerr("REVIEW094_FIXED_CATALOG_FAIL " + label)
	return ok


func _required_static(method: StringName) -> Callable:
	var callable := Callable(Maps, method)
	check(callable.is_valid(), "MapCatalog exposes static %s" % method)
	return callable


func _check_catalog() -> void:
	var playable_callable := _required_static(&"playable_maps")
	var default_callable := _required_static(&"default_map_id")

	var default_value: Variant = null
	if default_callable.is_valid():
		default_value = default_callable.call()
	check(default_value is String and String(default_value) == MAP_ID,
		"default_map_id is exactly %s" % MAP_ID)

	var catalog_value: Variant = null
	if playable_callable.is_valid():
		catalog_value = playable_callable.call()
	if not check(catalog_value is Array, "playable_maps returns an Array"):
		return
	var catalog: Array = catalog_value
	check(catalog.size() == 1, "playable catalog contains exactly one map")
	if catalog.size() != 1:
		return
	if not check(catalog[0] is Dictionary, "playable catalog entry is a Dictionary"):
		return
	var entry: Dictionary = catalog[0]
	check(str(entry.get("id", "")) == MAP_ID, "playable catalog exposes the canonical id")
	check(str(entry.get("label", "")) == MAP_LABEL, "playable catalog exposes the approved label")
	check(entry.get("id") is String and entry.get("label") is String,
		"playable catalog id and label retain String types")


func _check_authored_map() -> Dictionary:
	var data := Maps.get_map(MAP_ID)
	if not check(not data.is_empty(), "canonical authored map resolves"):
		return data
	check(str(data.get("id", "")) == MAP_ID, "authored map preserves canonical id")
	check(str(data.get("label", "")) == MAP_LABEL, "authored map preserves approved label")
	check(bool(data.get("playable", false)), "authored map is playable")
	check(int(data.get("authored_version", -1)) == EXPECTED_AUTHORED_VERSION,
		"authored_version is exactly 1")
	check(not data.has("generator_version"), "authored map has no generator_version")
	check(not data.has("generation_stage"), "authored map has no generation_stage")
	check(data.get("bounds") is AABB and data.bounds == EXPECTED_BOUNDS,
		"lot bounds match the approved 40 x 8.8 x 40 contract")
	check(data.get("building_bounds") is AABB and data.building_bounds == EXPECTED_BUILDING_BOUNDS,
		"building bounds match the approved 24 x 6.4 x 20 contract")
	check(data.get("floor_levels", []) == [0.0, 3.2], "authored levels are 0.0 m and 3.2 m")
	check(data.get("exterior") is Dictionary, "authored map exposes exterior metadata")
	if data.get("exterior") is Dictionary:
		var exterior: Dictionary = data.exterior
		check(exterior.get("bounds") is AABB, "exterior exposes physical bounds")
		for key: String in ["areas", "paths", "props"]:
			check(exterior.get(key) is Array and not Array(exterior.get(key)).is_empty(),
				"exterior exposes nonempty %s" % key)
	check(data.get("windows") is Array and not Array(data.get("windows", [])).is_empty(),
		"authored map exposes windows")
	for key: String in ["rooms", "structures", "portals", "nav_nodes", "nav_edges",
			"human_spawns", "mosquito_spawns", "respawn_points", "stations", "pickups"]:
		check(data.get(key) is Array, "authored map preserves Array field %s" % key)
	return data


func _check_rejections() -> void:
	check(Maps.is_playable(MAP_ID), "canonical authored map is accepted as playable")
	check(not Maps.is_playable("lobby"), "lobby remains outside the playable catalog")
	var lobby := Maps.get_map("lobby")
	check(not lobby.is_empty() and str(lobby.get("id", "")) == "lobby",
		"lobby remains available as a nonplayable support map")
	for id: String in REJECTED_IDS:
		check(not Maps.is_playable(id), "noncanonical map id is not playable: %s" % id)
		check(Maps.get_map(id).is_empty(), "noncanonical map id does not resolve: %s" % id)


func _check_default_and_copy_isolation(pristine: Dictionary) -> void:
	if pristine.is_empty():
		return
	check(Maps.get_map() == pristine, "get_map default resolves the canonical authored map")
	check(Maps.get_map(MAP_ID) == pristine, "repeated authored loads are deterministic")

	var mutated := Maps.get_map(MAP_ID)
	mutated["__review094_mutation__"] = true
	if mutated.get("exterior") is Dictionary:
		mutated.exterior["__review094_mutation__"] = true
	if mutated.get("human_spawns") is Array and not mutated.human_spawns.is_empty():
		mutated.human_spawns[0] = Vector3.INF
	var fresh := Maps.get_map(MAP_ID)
	check(fresh == pristine, "top-level and nested caller mutations cannot alter the catalog")
	check(not fresh.has("__review094_mutation__"), "catalog rejects injected top-level state")
	if fresh.get("exterior") is Dictionary:
		check(not fresh.exterior.has("__review094_mutation__"),
			"catalog rejects injected nested exterior state")


func _write_report(data: Dictionary) -> void:
	if report_path.is_empty():
		return
	var report := {
		"checks": checks,
		"failures": failures,
		"map_id": MAP_ID,
		"authored_version": data.get("authored_version", null),
		"source_sha256": {
			"res://tests/review094_fixed_catalog_contract.gd": FileAccess.get_sha256("res://tests/review094_fixed_catalog_contract.gd"),
			"res://scripts/map_catalog.gd": FileAccess.get_sha256("res://scripts/map_catalog.gd"),
		},
		"scope": "fixed playable catalog, authored identity and metadata envelope, procedural-id rejection, deterministic deep-copy isolation; no geometry, render, simulation, network, performance or release claim",
	}
	var file := FileAccess.open(report_path, FileAccess.WRITE)
	if file == null:
		check(false, "report path can be opened: %s" % report_path)
		return
	file.store_string(JSON.stringify(report, "\t"))
	file.close()


func _run() -> void:
	_check_catalog()
	var data := _check_authored_map()
	_check_rejections()
	_check_default_and_copy_isolation(data)
	_write_report(data)
	print("REVIEW094_FIXED_CATALOG_RESULT checks=%d failures=%d" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
