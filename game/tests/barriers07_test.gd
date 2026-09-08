extends SceneTree
const ArenaData = preload("res://scripts/arena.gd")
const Barriers = preload("res://scripts/house_barriers.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Nav = preload("res://scripts/map_navigation.gd")
var checks := 0
var failures := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("BARRIER_FAIL "+label)

func _initialize() -> void:
	check(ArenaData.obstacles().size()==Maps.HOUSE.obstacles.size()+Barriers.get_boxes().size(),"authority contains every rendered railing member")
	check(ArenaData.obstacles().is_read_only(),"cached static geometry cannot become mutable room state")
	check(ArenaData.obstacles("lobby").size()==Maps.LOBBY.obstacles.size(),"house railings never enter lobby")
	for side: float in [-1.0,1.0]:
		var origin := Vector3(side*9.1,3.6,0)
		var end := Vector3(side*9.7,3.6,0)
		check(not ArenaData.clear_segment(origin,end),"ray stops at individual landing post")
		check(not Nav.can_travel(origin,end,false),"mosquito route respects post radius")
		var moved: Vector3 = ArenaData.move_body(origin,end-origin,false)
		check(absf(moved.x)<9.33,"small physical mosquito stops before post")
		origin.z=.5
		end.z=.5
		check(ArenaData.clear_segment(origin,end) and Nav.can_travel(origin,end,false),"real air between balusters remains ray and flight passage")
		check(ArenaData.move_body(origin,end-origin,false).distance_to(end)<.001,"mosquito physically passes between members")
		var feet := Vector3(side*8.7325,3.2,0)
		check(ArenaData.can_fit_human(feet),"authored side corridor point has full standing clearance")
		var human: Dictionary = ArenaData.move_human(feet,Vector3(side,0,0))
		check(absf(human.p.x)<8.77 and ArenaData.can_fit_human(human.p),"human cannot walk through landing guard")
	print("BARRIERS07_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
