extends SceneTree
const Maps=preload("res://scripts/map_catalog.gd")
const Arena=preload("res://scripts/arena.gd")
const Navigation=preload("res://scripts/map_navigation.gd")
const Doors=preload("res://scripts/door_catalog.gd")
var checks:=0
var failures: Array[String]=[]
func check(value: bool, message: String) -> void:
	checks+=1
	if not value: failures.append(message)
func _initialize() -> void:
	var first: Dictionary=Maps.new_house(1)
	var retained: Array[AABB]=Arena.obstacles(first.id)
	var retained_count:=retained.size()
	var visited: Dictionary={}
	for seed_value: int in range(1,41):
		var map: Dictionary=Maps.new_house(seed_value*997)
		check(not map.is_empty(),"valid map "+str(seed_value))
		if map.is_empty(): continue
		visited[map.id]=true
		var point: Vector3=map.human_spawns[0]
		check(Arena.can_fit_human(point,Arena.HUMAN_HEIGHT,map.id),"arena geometry usable "+str(map.id))
		Navigation.graph_info(true,map.id);Navigation.graph_info(false,map.id)
		Doors.get_doors(map.id)
		check(Arena._map_cache.size()<=24 and Arena._obstacle_cache.size()<=24 and Arena._spatial_cache.size()<=24,"Arena cache bounded")
		check(Navigation._cache.size()<=48 and Doors._generated.size()<=24 and Maps._generated.size()<=24,"Navigation/doors/catalog bounded")
	check(visited.size()>=30,"at least thirty distinct rounds")
	check(retained.size()==retained_count,"active geometry reference survives eviction")
	check(not Arena._map_cache.has(first.id),"old unreferenced map evicted")
	check(Maps.get_map(first.id).fingerprint==first.fingerprint,"regeneration preserves exact fingerprint")
	for failure: String in failures: print("FAIL "+failure)
	print("geometry09_cache checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
