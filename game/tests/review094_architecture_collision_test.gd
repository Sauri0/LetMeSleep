extends SceneTree
const Maps = preload("res://scripts/map_catalog.gd")
const Arena = preload("res://scripts/arena.gd")
const Assets = preload("res://assets/art/house/alfa_library.gd")
var checks := 0
var failures := 0
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		printerr("ARCHITECTURE094_FAIL "+label)
func _initialize() -> void:
	var data := Maps.get_map(Maps.default_map_id())
	var plan: Dictionary = data.exterior.architecture
	for prop: Dictionary in plan.props:
		var bounds := Assets.bounds(prop.asset_id)
		bounds.position += Vector3(prop.p)
		check(bounds.end.y <= data.bounds.end.y,"asset stays below flight ceiling: "+prop.asset_id)
		for box: AABB in Assets.collision_boxes(prop.asset_id):
			var point := Vector3(prop.p)+box.get_center()
			check(not Arena.can_fit_mosquito(point,data.id,{}),"flight cannot enter "+prop.asset_id)
	for x: float in [-12.4,-10,-6,0,6,10,12.4]:
		var top: float = plan.eave+plan.rise*(1.0-absf(x)/float(plan.run))
		check(not Arena.can_fit_mosquito(Vector3(x,top,0),data.id,{}),"sloped roof blocks flight at "+str(x))
		if top+.25 < data.bounds.end.y-Arena.MOSQUITO_RADIUS:
			check(Arena.can_fit_mosquito(Vector3(x,top+.25,0),data.id,{}),"air above roof remains reachable at "+str(x))
	check(Arena.can_fit_human(Vector3(0,0,-11.1),Arena.HUMAN_HEIGHT,data.id,{}),"porch keeps centered entrance walkable")
	check(not Arena.can_fit_human(Vector3(2.28,0,-11.14),Arena.HUMAN_HEIGHT,data.id,{}),"human cannot walk through porch post")
	print("ARCHITECTURE094 checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
