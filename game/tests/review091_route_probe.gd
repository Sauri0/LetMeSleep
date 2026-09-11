extends SceneTree
## Focused diagnostic for the first open-leaf route witness in seeds 1 and 2.
const Generator=preload("res://scripts/procedural_house.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Nav=preload("res://scripts/map_navigation.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const DT:=1.0/30.0

func _initialize()->void:_run.call_deferred()

func _states(map_id:String)->Dictionary:
	var result:Dictionary={}
	for id:String in Doors.get_doors(map_id):result[id]={"angle":Doors.OPEN_ANGLE,"target_angle":Doors.OPEN_ANGLE,"moving":false,"blocked":false,"revision":1}
	return result

func _targets(data:Dictionary)->Array[Dictionary]:
	var result:Array[Dictionary]=[]
	for station:Dictionary in data.stations:result.append({"kind":"station","id":str(station.get("id",station.get("name",""))),"p":station.p})
	for pickup:Dictionary in data.pickups:result.append({"kind":"pickup","id":str(pickup.get("id",pickup.get("tool",""))),"p":pickup.approach})
	return result

func _first_witness(seed_value:int)->Dictionary:
	var map_id:=Generator.map_id(seed_value)
	var data:=Maps.get_map(map_id)
	var states:=_states(map_id)
	for spawn_index:int in range(data.human_spawns.size()):
		var origin:Vector3=data.human_spawns[spawn_index]
		for target:Dictionary in _targets(data):
			var route:=Nav.path(origin,target.p,true,map_id)
			var previous:=origin
			for segment_index:int in range(route.size()):
				var point:Vector3=route[segment_index]
				var hit:=Doors.ray_doors(previous,point,states,map_id,ArenaData.HUMAN_RADIUS)
				if not hit.is_empty():
					return {"seed":seed_value,"map_id":map_id,"spawn_index":spawn_index,"origin":origin,"target":target,"route":route,"segment_index":segment_index,"segment_from":previous,"segment_to":point,"ray_hit":hit,"physical":_follow(origin,target.p,route,map_id,states)}
				previous=point
	return {"seed":seed_value,"error":"no open-leaf witness"}

func _follow(origin:Vector3,destination:Vector3,route:PackedVector3Array,map_id:String,states:Dictionary)->Dictionary:
	var actor:Dictionary={"p":origin,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true}
	var current:=0
	var stalled:=0
	var minimum:=origin.distance_to(destination)
	for tick:int in range(3600):
		var point:Vector3=route[current]
		var delta:=point-Vector3(actor.p)
		if Vector2(delta.x,delta.z).length()<.12 and absf(delta.y)<.23:
			current+=1
			if current==route.size():return {"reached":Vector3(actor.p).distance_to(destination)<.30,"ticks":tick,"final":actor.p,"minimum_distance":minimum,"stalled_ticks":stalled}
			point=route[current];delta=point-Vector3(actor.p)
		delta.y=0.0
		var before:Vector3=actor.p
		ArenaData.step_human(actor,{"move":delta.normalized(),"yaw":0.0,"jump":false},DT,map_id,states)
		var moved:=before.distance_to(actor.p)
		stalled=stalled+1 if moved<.001 else 0
		minimum=minf(minimum,Vector3(actor.p).distance_to(destination))
		if stalled>=120:return {"reached":false,"ticks":tick,"final":actor.p,"minimum_distance":minimum,"stalled_ticks":stalled}
	return {"reached":false,"ticks":3600,"final":actor.p,"minimum_distance":minimum,"stalled_ticks":stalled}

func _run()->void:
	var cases:Array=[]
	for seed_value:int in [1,2]:cases.append(_first_witness(seed_value))
	var file:=FileAccess.open("res://../work/review091-route-probe-results.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"cases":cases},"\t"));file.close()
	for row:Dictionary in cases:print("REVIEW091_ROUTE_PROBE "+JSON.stringify(row))
	quit(0)
