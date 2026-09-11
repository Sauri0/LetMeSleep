extends SceneTree
## Independent 0.9.1 acceptance gate for generated-house scale and circulation.
## Uses production collision/navigation but never changes runtime state or prefs.
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Nav=preload("res://scripts/map_navigation.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const CORPUS: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
const SAMPLE_STEP:=.025
const MIN_CORRIDOR_WIDTH:=1.50
const MIN_LANDING_WIDTH:=1.50
const MIN_STAIR_WIDTH:=1.40
const MIN_DOOR_WIDTH:=1.30
const MIN_TREAD:=.28
const MAX_RISE:=.22
const DT:=1.0/30.0
var checks:=0
var failures: Array[String]=[]
var cases: Array[Dictionary]=[]
var report_path:="res://../work/review091-house-results.json"

func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=30:printerr("REVIEW091_HOUSE_FAIL "+label)
	return ok

func _layout_signature(data:Dictionary)->String:
	var shell:Array=[]
	for structure:Dictionary in data.structures:
		if str(structure.kind) in ["wall","floor","ceiling","step"]:
			shell.append([structure.kind,structure.box])
	return JSON.stringify(Validation._canonical({"bounds":data.bounds,"floors":data.floor_levels,"shell":shell})).sha256_text()

func _open_doors(map_id:String)->Dictionary:
	var result:Dictionary={}
	for id:String in Doors.get_doors(map_id):
		result[id]={"angle":Doors.OPEN_ANGLE,"target_angle":Doors.OPEN_ANGLE,"moving":false,"blocked":false,"revision":1}
	return result

func _fits_human(point:Vector3,map_id:String,states:Dictionary)->bool:
	return ArenaData.can_fit_human(point,ArenaData.HUMAN_HEIGHT,map_id,states)

## Reports the real free opening around a centerline. The center range already
## accounts for the 60 cm collision radius; add the diameter back to express
## wall-to-wall clearance rather than available center travel.
func _physical_width(point:Vector3,travel:Vector3,map_id:String,states:Dictionary)->float:
	var flat:=Vector3(travel.x,0,travel.z).normalized()
	if flat.length_squared()<.9 or not _fits_human(point,map_id,states):return 0.0
	var side:=Vector3(-flat.z,0,flat.x)
	var negative:=0.0
	var positive:=0.0
	for index:int in range(1,321):
		var distance:=float(index)*SAMPLE_STEP
		if not _fits_human(point-side*distance,map_id,states):break
		negative=distance
	for index:int in range(1,321):
		var distance:=float(index)*SAMPLE_STEP
		if not _fits_human(point+side*distance,map_id,states):break
		positive=distance
	return negative+positive+ArenaData.HUMAN_RADIUS*2.0

func _route_clear(origin:Vector3,route:PackedVector3Array,human:bool,map_id:String,states:Dictionary)->bool:
	if route.is_empty():return false
	var previous:=origin
	for point:Vector3 in route:
		if not Nav.can_travel(previous,point,human,map_id):return false
		var radius:=ArenaData.HUMAN_RADIUS if human else ArenaData.MOSQUITO_RADIUS
		if not Doors.ray_doors(previous,point,states,map_id,radius).is_empty():return false
		previous=point
	return true

func _follow_human(origin:Vector3,destination:Vector3,map_id:String,states:Dictionary)->Dictionary:
	var route:=Nav.path(origin,destination,true,map_id)
	if route.is_empty():return {"reached":false,"collision_free":false,"max_step":INF,"max_motion":INF,"ticks":0}
	var actor:Dictionary={"p":origin,"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true}
	var current:=0
	var collision_free:=true
	var max_step:=0.0
	var max_motion:=0.0
	for tick:int in range(3600):
		var point:Vector3=route[current]
		var delta:=point-Vector3(actor.p)
		if Vector2(delta.x,delta.z).length()<.12 and absf(delta.y)<.23:
			current+=1
			if current==route.size():
				return {"reached":Vector3(actor.p).distance_to(destination)<.30,"collision_free":collision_free,"max_step":max_step,"max_motion":max_motion,"ticks":tick}
			point=route[current]
			delta=point-Vector3(actor.p)
		delta.y=0.0
		var move:=delta.normalized()*minf(1.0,delta.length()/(ArenaData.HUMAN_SPEED*DT))
		var before:Vector3=actor.p
		ArenaData.step_human(actor,{"move":move,"yaw":0.0,"jump":false},DT,map_id,states)
		var motion:=Vector3(actor.p)-before
		max_step=maxf(max_step,absf(motion.y))
		max_motion=maxf(max_motion,motion.length())
		collision_free=collision_free and _fits_human(actor.p,map_id,states)
	return {"reached":false,"collision_free":collision_free,"max_step":max_step,"max_motion":max_motion,"ticks":3600}

func _closest_stair_point(stair:Dictionary,map_id:String)->Dictionary:
	var route:=Nav.path(stair.bottom,stair.top,true,map_id)
	if route.is_empty():return {"point":Vector3.INF,"direction":Vector3.ZERO}
	var target_y:float=(Vector3(stair.bottom).y+Vector3(stair.top).y)*.5
	var selected:Vector3=route[0]
	for point:Vector3 in route:
		if absf(point.y-target_y)<absf(selected.y-target_y):selected=point
	return {"point":selected,"direction":Vector3(stair.top)-Vector3(stair.bottom)}

func _target_points(data:Dictionary)->Array[Vector3]:
	var result:Array[Vector3]=[]
	for station:Dictionary in data.stations:result.append(station.p)
	for pickup:Dictionary in data.pickups:result.append(pickup.approach)
	return result

func _farthest_per_floor(origin:Vector3,data:Dictionary,map_id:String)->Array[Vector3]:
	var result:Array[Vector3]=[]
	for floor_y:float in data.floor_levels:
		var selected:=Vector3.INF
		var distance:=-1.0
		for target:Vector3 in _target_points(data):
			if absf(target.y-floor_y)>.01:continue
			var route:=Nav.path(origin,target,true,map_id)
			var length:=0.0
			var previous:=origin
			for point:Vector3 in route:length+=previous.distance_to(point);previous=point
			if not route.is_empty() and length>distance:selected=target;distance=length
		if selected.is_finite():result.append(selected)
	return result

func _case(seed_value:int,signatures:Dictionary)->void:
	var map_id:=Generator.map_id(seed_value)
	var generated:=Generator.new().generate(seed_value)
	var data:=Maps.get_map(map_id)
	var validation:=Validation.validate(generated)
	var states:=_open_doors(map_id)
	var row:Dictionary={"seed":seed_value,"map_id":map_id,"floors":data.get("floor_levels",[]).size(),"rooms":data.get("rooms",[]).size(),"layout_signature":_layout_signature(generated),"min_corridor_width":INF,"min_landing_width":INF,"min_stair_width":INF,"min_door_width":INF,"physical_routes":0}
	check(not data.is_empty() and validation.passed,"seed %d validates and loads: %s"%[seed_value,validation.errors])
	check(Validation.fingerprint(generated)==Validation.fingerprint(Generator.new().generate(seed_value)),"seed %d deterministic fingerprint"%seed_value)
	check(data.get("fingerprint","")==Validation.fingerprint(generated),"seed %d catalog matches generated geometry"%seed_value)
	check(not signatures.has(row.layout_signature),"seed %d has a distinct structural layout"%seed_value)
	signatures[row.layout_signature]=seed_value
	var floor_count:=data.floor_levels.size()
	check(floor_count in [2,3],"seed %d stays within the two/three-storey contract"%seed_value)
	check(data.rooms.size()<=22,"seed %d stays within the 22-room cap"%seed_value)
	check(data.stair_connections.size()==2*(floor_count-1) and data.stair_connections.size()<=4,"seed %d has opposite flights within the four-flight cap"%seed_value)
	check(data.stair_light_anchors.size()==data.stair_connections.size(),"seed %d has one light anchor per stair flight"%seed_value)
	check(data.rooms.size()+floor_count*2+data.stair_light_anchors.size()<=32,"seed %d stays within the 32-light Compatibility budget"%seed_value)
	check(float(data.stair_tread)>=MIN_TREAD,"seed %d stair tread %.3fm >= %.2fm"%[seed_value,float(data.stair_tread),MIN_TREAD])
	check(float(data.stair_rise)<=MAX_RISE,"seed %d stair rise %.3fm <= %.2fm"%[seed_value,float(data.stair_rise),MAX_RISE])
	for corridor:AABB in data.corridors:
		var along:=Vector3.FORWARD if corridor.size.z>=corridor.size.x else Vector3.RIGHT
		var center:=Vector3(corridor.get_center().x,corridor.position.y,corridor.get_center().z)
		var width:=_physical_width(center,along,map_id,states)
		row.min_corridor_width=minf(float(row.min_corridor_width),width)
		check(width+SAMPLE_STEP>=MIN_CORRIDOR_WIDTH,"seed %d corridor physical width %.3fm >= %.2fm"%[seed_value,width,MIN_CORRIDOR_WIDTH])
	for stair:Dictionary in data.stair_connections:
		var witness:=_closest_stair_point(stair,map_id)
		var stair_width:=_physical_width(witness.point,witness.direction,map_id,states) if Vector3(witness.point).is_finite() else 0.0
		row.min_stair_width=minf(float(row.min_stair_width),stair_width)
		check(stair_width+SAMPLE_STEP>=MIN_STAIR_WIDTH,"seed %d %s physical stair width %.3fm >= %.2fm"%[seed_value,str(stair.id),stair_width,MIN_STAIR_WIDTH])
		for landing:Vector3 in [stair.bottom,stair.top]:
			var landing_width:=_physical_width(landing,Vector3(stair.top)-Vector3(stair.bottom),map_id,states)
			row.min_landing_width=minf(float(row.min_landing_width),landing_width)
			check(landing_width+SAMPLE_STEP>=MIN_LANDING_WIDTH,"seed %d %s landing width %.3fm >= %.2fm"%[seed_value,str(stair.id),landing_width,MIN_LANDING_WIDTH])
		for ends:Array in [[stair.bottom,stair.top],[stair.top,stair.bottom]]:
			var followed:=_follow_human(ends[0],ends[1],map_id,states)
			row.physical_routes+=1
			check(bool(followed.reached) and bool(followed.collision_free),"seed %d %s stair direction physically completes"%[seed_value,str(stair.id)])
			var bounded_motion:=Vector2(ArenaData.HUMAN_SPEED*DT+.012,MAX_RISE+.011).length()
			check(float(followed.max_step)<=MAX_RISE+.011 and float(followed.max_motion)<=bounded_motion,"seed %d %s stair motion has no jump or teleport"%[seed_value,str(stair.id)])
	for portal:Dictionary in data.portals:
		var axis:=int(portal.axis)
		var direction:=Vector3.RIGHT if axis==0 else Vector3.FORWARD
		var width:=_physical_width(portal.p,direction,map_id,states)
		row.min_door_width=minf(float(row.min_door_width),width)
		check(width+SAMPLE_STEP>=MIN_DOOR_WIDTH,"seed %d door %s physical opening %.3fm >= %.2fm"%[seed_value,str(portal.id),width,MIN_DOOR_WIDTH])
	var targets:=_target_points(data)
	for origin:Vector3 in data.human_spawns:
		check(_fits_human(origin,map_id,states),"seed %d human spawn fits open-door collision"%seed_value)
		for target:Vector3 in targets:
			var route:=Nav.path(origin,target,true,map_id)
			check(_route_clear(origin,route,true,map_id,states),"seed %d human spawn reaches task/pickup without crossing an open leaf"%seed_value)
		for target:Vector3 in _farthest_per_floor(origin,data,map_id):
			var followed:=_follow_human(origin,target,map_id,states)
			row.physical_routes+=1
			check(bool(followed.reached) and bool(followed.collision_free),"seed %d human spawn physically reaches far target on floor %.1f"%[seed_value,target.y])
	for origin:Vector3 in data.mosquito_spawns:
		check(ArenaData.can_fit_mosquito(origin,map_id,states),"seed %d mosquito spawn fits open-door collision"%seed_value)
		for room:Dictionary in data.rooms:
			var target:=Vector3(room.center)+Vector3.UP*1.2
			check(_route_clear(origin,Nav.path(origin,target,false,map_id),false,map_id,states),"seed %d mosquito spawn reaches room %s without crossing an open leaf"%[seed_value,str(room.id)])
	cases.append(row)

func _run()->void:
	var signatures:Dictionary={}
	check(Generator.VERSION==2,"0.9.1 generated-house contract uses version 2 only")
	check(Generator.parse_seed("house-v1-1")==-1 and Generator.parse_seed("house-v3-1")==-1,"old and future generator IDs are rejected")
	for seed_value:int in CORPUS:_case(seed_value,signatures)
	check(signatures.size()==CORPUS.size(),"all %d corpus seeds have distinct structure independent of seed metadata"%CORPUS.size())
	var report:Dictionary={"checks":checks,"failures":failures,"cases":cases,"thresholds":{"corridor_m":MIN_CORRIDOR_WIDTH,"landing_m":MIN_LANDING_WIDTH,"stair_m":MIN_STAIR_WIDTH,"door_m":MIN_DOOR_WIDTH,"tread_m":MIN_TREAD,"rise_m":MAX_RISE},"scope":"production geometry/navigation; deterministic source corpus; open-door circulation; no renderer, visual approval, FPS, EOS, relay or WAN claim","source_sha256":{}}
	for path:String in ["res://tests/review091_house_contract.gd","res://scripts/procedural_house.gd","res://scripts/house_validation.gd","res://scripts/map_catalog.gd","res://scripts/map_navigation.gd","res://scripts/arena.gd","res://scripts/door_catalog.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:check(false,"review report path writable")
	for failure:String in failures:print("REVIEW091_HOUSE_DIAGNOSTIC "+failure)
	print("REVIEW091_HOUSE_RESULT checks=%d failures=%d layouts=%d"%[checks,failures.size(),signatures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)
