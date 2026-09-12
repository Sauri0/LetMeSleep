extends SceneTree
const Geometry = preload("res://scripts/navigation_geometry.gd")
var checks := 0
var failures := 0

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("FAIL: " + label)

func _initialize() -> void:
	# An offset lot guards against accidentally reverting to symmetric house
	# half-extents or an implicit world-ground at zero.
	var lot := AABB(Vector3(10, 2, -8), Vector3(20, 10, 16))
	var roof := AABB(Vector3(12, 7, -4), Vector3(8, .2, 8))
	var data := {"bounds":lot,"building_bounds":AABB(Vector3(12,2,-4),Vector3(8,5,8)),"obstacles":[roof],"floors":[],"steps":[]}
	var human := Geometry.create(data, true)
	var insect := Geometry.create(data, false)
	check(Geometry.world_bounds(data) == lot, "explicit lot bounds need no legacy half-extents")
	check(Geometry._fits(Vector3(25,2,0),human), "human fits outdoor ground beyond building")
	check(not Geometry._fits(Vector3(9.9,2,0),human), "human cannot leave offset lot")
	check(not Geometry._fits(Vector3(25,0,0),human), "human cannot enter space below lot ground")
	check(Geometry._fits(Vector3(25,9,0),insect), "outdoor flight extends above building roof")
	check(not Geometry._fits(Vector3(15,7.1,0),insect), "physical roof blocks mosquito")
	check(not Geometry._segment(Vector3(15,6,0),Vector3(15,8,0),insect), "flight cannot tunnel vertically through roof")
	check(Geometry._segment(Vector3(25,6,0),Vector3(25,10,0),insect), "patio has no invisible roof plane")
	check(Geometry._support_height(Vector3(25,2,0),human,.025) == 2, "support uses lot ground height")
	check(not is_finite(Geometry._support_height(Vector3(25,0,0),human,.025)), "no ground support outside height tolerance")
	check(Geometry._project_human(Vector3(25,3,0),human) == Vector3(25,2,0), "route projection uses offset ground")
	check(Geometry._segment(Vector3(23,2,0),Vector3(27,2,0),human), "human route crosses patio ground")
	check(not Geometry._segment(Vector3(23,5,0),Vector3(27,5,0),human), "human cannot walk unsupported above patio")
	check(not Geometry._fits(Vector3(25,12,0),insect), "mosquito radius remains inside sky boundary")
	print("MOTION094_BOUNDS checks=%d failures=%d" % [checks,failures])
	quit(0 if failures == 0 else 1)
