extends SceneTree
const ArenaData = preload("res://scripts/arena.gd")
const Maps = preload("res://scripts/map_catalog.gd")
var checks := 0
var failures := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		if failures<15:printerr("SPATIAL_FAIL "+label)

func _initialize() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed=700173
	for map_id: String in ["house","lobby"]:
		var map: Dictionary=Maps.get_map(map_id)
		var boxes: Array[AABB]=ArenaData.obstacles(map_id)
		for index: int in range(900):
			var origin := Vector3(rng.randf_range(-map.half_x,map.half_x),rng.randf_range(0,map.ceiling),rng.randf_range(-map.half_z,map.half_z))
			var end := origin+Vector3(rng.randf_range(-8,8),rng.randf_range(-4,4),rng.randf_range(-8,8))
			if index%3==0:end.y=origin.y
			var radius: float=[0.0,.04,.17,.3][index%4]
			var distance := INF
			var clear := true
			for box: AABB in boxes:
				var hit: Variant=box.grow(radius).intersects_segment(origin,end)
				if hit!=null:distance=minf(distance,origin.distance_to(hit))
				clear=clear and box.intersects_segment(origin,end)==null
			var actual: Dictionary=ArenaData.ray_map(origin,end,map_id,radius)
			check((actual.is_empty() and not is_finite(distance)) or (not actual.is_empty() and absf(float(actual.distance)-distance)<.00001),"spatial ray equals exhaustive geometry, including thick/flat/long rays")
			check(ArenaData.clear_segment(origin,end,map_id)==clear,"LOS broad phase preserves exhaustive occlusion")
			var height: float=ArenaData.HUMAN_HEIGHT if index%2==0 else ArenaData.HUMAN_CROUCH_HEIGHT
			var fits: bool=absf(origin.x)+.6<=float(map.half_x)+.00001 and absf(origin.z)+.6<=float(map.half_z)+.00001 and origin.y+height<=float(map.ceiling)+.00001
			var body:=AABB(origin+Vector3(-.6,.003,-.6),Vector3(1.2,height-.006,1.2))
			for box: AABB in boxes:fits=fits and not body.intersects(box)
			check(ArenaData.can_fit_human(origin,height,map_id)==fits,"standing/crouched fit equals exhaustive body collision")
			var floor_y:=0.0
			for box: AABB in boxes:
				if origin.x>=box.position.x and origin.x<=box.end.x and origin.z>=box.position.z and origin.z<=box.end.z and box.end.y<=origin.y:floor_y=maxf(floor_y,box.end.y)
			check(is_equal_approx(ArenaData.floor_below(origin,map_id),floor_y),"floor support equals exhaustive geometry")
	print("ARENA_SPATIAL07_TEST_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
