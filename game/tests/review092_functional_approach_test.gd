extends SceneTree
## Independent 0.9.2 gate for physical access to generated furniture.
## It follows the authored center -> zone anchor -> approach polyline through
## production Arena authority. It never promotes a conservative ray hit to a
## failure unless the human body actually cannot complete the route.
const Generator=preload("res://scripts/procedural_house.gd")
const Validation=preload("res://scripts/house_validation.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const CORPUS: Array[int]=[1,2,7,31,97,257,997,2026,65537,1234567,2147483646]
const EXPECTED_VERSION:=3
const DT:=1.0/30.0
const ARRIVAL_RADIUS:=.12
const FLOOR_TOLERANCE:=.011
const MAX_STALLED_TICKS:=90
var checks:=0
var failures: Array[String]=[]
var report_path:="res://../work/review092-functional-approach-results.json"
var only_seed:=0

func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
		if argument.begins_with("--seed="):only_seed=int(argument.trim_prefix("--seed="))
	_run.call_deferred()

func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=40:printerr("REVIEW092_FUNCTIONAL_APPROACH_FAIL "+label)
	return ok

func _open_doors(map_id:String)->Dictionary:
	var result:Dictionary={}
	for id:String in Doors.get_doors(map_id):
		result[id]={"angle":Doors.OPEN_ANGLE,"target_angle":Doors.OPEN_ANGLE,"moving":false,"blocked":false,"revision":1}
	return result

func _structures_by_id(data:Dictionary)->Dictionary:
	var result:Dictionary={}
	for structure:Dictionary in data.structures:
		var id:=str(structure.get("id",""))
		if not id.is_empty():result[id]=structure
	return result

func _approach_errors(room:Dictionary,approach:Dictionary,structures:Dictionary)->Array[String]:
	var errors:Array[String]=[]
	for key:String in ["zone_id","structure_id","origin","p"]:
		if not approach.has(key):errors.append("missing "+key)
	if not errors.is_empty():return errors
	if not (approach.origin is Vector3) or not Vector3(approach.origin).is_finite():errors.append("origin is not a finite Vector3")
	if not (approach.p is Vector3) or not Vector3(approach.p).is_finite():errors.append("p is not a finite Vector3")
	if not errors.is_empty():return errors
	var zone:Dictionary={}
	for candidate:Dictionary in room.get("functional_zones",[]):
		if str(candidate.get("id",""))==str(approach.zone_id):zone=candidate;break
	if zone.is_empty():
		errors.append("zone_id does not resolve")
	elif not zone.has("anchor") or not (zone.anchor is Vector3) or not Vector3(zone.anchor).is_finite():
		errors.append("zone anchor is not a finite Vector3")
	elif Vector3(zone.anchor).distance_to(approach.origin)>.001:
		errors.append("origin does not match zone anchor")
	var structure_id:=str(approach.structure_id)
	if not structures.has(structure_id):
		errors.append("structure_id does not resolve")
	else:
		var structure:Dictionary=structures[structure_id]
		if str(structure.get("kind",""))!="furniture":errors.append("structure is not furniture")
		if str(structure.get("room_id",structure.get("room","")))!=str(room.id):errors.append("structure belongs to another room")
		if str(structure.get("functional_zone_id",""))!=str(approach.zone_id):errors.append("structure belongs to another functional zone")
		if not structure.has("approach") or not (structure.approach is Vector3) or Vector3(structure.approach).distance_to(approach.p)>.001:
			errors.append("p does not match structure approach")
	return errors

func _approach_coverage_errors(room:Dictionary,structures:Dictionary)->Array[String]:
	var errors:Array[String]=[]
	var expected:Dictionary={}
	for id:String in structures:
		var structure:Dictionary=structures[id]
		if str(structure.get("kind",""))=="furniture" and str(structure.get("room_id",structure.get("room","")))==str(room.id):expected[id]=true
	var seen:Dictionary={}
	for approach:Dictionary in room.get("functional_approaches",[]):
		var id:=str(approach.get("structure_id",""))
		if id.is_empty():continue
		if seen.has(id):errors.append("duplicate approach for furniture "+id)
		seen[id]=true
	for id:String in expected:
		if not seen.has(id):errors.append("missing approach for furniture "+id)
	for id:String in seen:
		if not expected.has(id):errors.append("approach references non-room furniture "+id)
	return errors

func _route_length(points:Array[Vector3])->float:
	var result:=0.0
	for index:int in range(1,points.size()):result+=points[index-1].distance_to(points[index])
	return result

func _walk(points:Array[Vector3],map_id:String,states:Dictionary,tick_budget_override:int=-1)->Dictionary:
	var result:Dictionary={"reached":false,"reason":"invalid_route","ticks":0,"waypoint_index":0,
		"final":Vector3.INF,"end_distance":INF,"max_vertical_step":0.0,"max_motion":0.0,"stalled_ticks":0}
	if points.size()<2:return result
	for index:int in range(points.size()):
		if not points[index].is_finite():
			result.reason="nonfinite_waypoint";result.waypoint_index=index;return result
		if not ArenaData.can_fit_human(points[index],ArenaData.HUMAN_HEIGHT,map_id,states):
			result.reason="start_blocked" if index==0 else ("end_blocked" if index==points.size()-1 else "waypoint_blocked")
			result.waypoint_index=index;result.final=points[0];result.end_distance=points[0].distance_to(points.back());return result
	var actor:Dictionary={"p":points[0],"yaw":0.0,"body_yaw":0.0,"pitch":0.0,"velocity":Vector3.ZERO,"grounded":true}
	var waypoint:=1
	var route_length:=_route_length(points)
	var natural_ticks:=int(ceil(route_length/(ArenaData.HUMAN_SPEED*DT)))
	var budget:=maxi(1,natural_ticks*3+120)
	if tick_budget_override>=0:budget=tick_budget_override
	var stalled:=0
	var floor_y:=points[0].y
	for tick:int in range(budget):
		while waypoint<points.size():
			var arrival_delta:Vector3=points[waypoint]-Vector3(actor.p)
			if Vector2(arrival_delta.x,arrival_delta.z).length()>ARRIVAL_RADIUS or absf(arrival_delta.y)>.03:break
			waypoint+=1
		if waypoint>=points.size():
			result.reached=true;result.reason="reached";result.ticks=tick;result.waypoint_index=waypoint
			result.final=actor.p;result.end_distance=Vector3(actor.p).distance_to(points.back());result.stalled_ticks=stalled
			return result
		var target:=points[waypoint]
		var delta:=target-Vector3(actor.p)
		var horizontal:=Vector3(delta.x,0,delta.z)
		if horizontal.length_squared()<.000001:
			result.reason="vertical_waypoint";result.ticks=tick;result.waypoint_index=waypoint;result.final=actor.p
			result.end_distance=Vector3(actor.p).distance_to(points.back());return result
		var before:Vector3=actor.p
		var before_distance:=Vector2(delta.x,delta.z).length()
		ArenaData.step_human(actor,{"move":horizontal.normalized(),"yaw":0.0,"pitch":0.0,"jump":false,"crouch":false,"sprint":false},DT,map_id,states)
		var motion:=Vector3(actor.p)-before
		result.max_vertical_step=maxf(float(result.max_vertical_step),absf(motion.y))
		result.max_motion=maxf(float(result.max_motion),motion.length())
		var after_delta:=target-Vector3(actor.p)
		var after_distance:=Vector2(after_delta.x,after_delta.z).length()
		stalled=stalled+1 if after_distance>=before_distance-.0005 else 0
		if not ArenaData.can_fit_human(actor.p,ArenaData.HUMAN_HEIGHT,map_id,states):
			result.reason="collision";result.ticks=tick+1;result.waypoint_index=waypoint;result.final=actor.p
			result.end_distance=Vector3(actor.p).distance_to(points.back());result.stalled_ticks=stalled;return result
		if absf(float(actor.p.y)-floor_y)>FLOOR_TOLERANCE:
			result.reason="left_floor";result.ticks=tick+1;result.waypoint_index=waypoint;result.final=actor.p
			result.end_distance=Vector3(actor.p).distance_to(points.back());result.stalled_ticks=stalled;return result
		if stalled>=MAX_STALLED_TICKS:
			result.reason="stalled";result.ticks=tick+1;result.waypoint_index=waypoint;result.final=actor.p
			result.end_distance=Vector3(actor.p).distance_to(points.back());result.stalled_ticks=stalled;return result
	result.reason="incomplete";result.ticks=budget;result.waypoint_index=waypoint;result.final=actor.p
	result.end_distance=Vector3(actor.p).distance_to(points.back());result.stalled_ticks=stalled
	return result

func _walk_and_check(points:Array[Vector3],map_id:String,states:Dictionary,label:String)->Dictionary:
	var result:=_walk(points,map_id,states)
	check(bool(result.reached) and str(result.reason)=="reached" and int(result.waypoint_index)==points.size(),label+" consumes every waypoint: "+str(result))
	check(float(result.end_distance)<=ARRIVAL_RADIUS+.0001,label+" ends inside the arrival radius")
	check(float(result.max_vertical_step)<=FLOOR_TOLERANCE,label+" stays on the authored floor")
	check(float(result.max_motion)<=ArenaData.HUMAN_SPEED*DT+.012,label+" has no jump or teleport")
	return result

func _negative_cases(data:Dictionary,map_id:String,states:Dictionary,structures:Dictionary)->Array[Dictionary]:
	var results:Array[Dictionary]=[]
	var selected_room:Dictionary={}
	var selected_approach:Dictionary={}
	for room:Dictionary in data.rooms:
		if not Array(room.get("functional_approaches",[])).is_empty():
			selected_room=room;selected_approach=room.functional_approaches[0];break
	check(not selected_room.is_empty(),"negative fixtures have a generated functional approach")
	if selected_room.is_empty():return results
	var structure_id:=str(selected_approach.get("structure_id",""))
	check(structures.has(structure_id),"negative fixtures have a linked furniture structure")
	if not structures.has(structure_id):return results
	var furniture:Dictionary=structures[structure_id]
	var box:AABB=furniture.box
	var start:Vector3=selected_room.center
	var blocked:=Vector3(box.get_center().x,start.y,box.get_center().z)
	var end_blocked:=_walk([start,blocked],map_id,states)
	check(not bool(end_blocked.reached) and str(end_blocked.reason)=="end_blocked","blocked destination is rejected before proximity can pass")
	results.append({"case":"blocked_destination","result":end_blocked})
	var real_end:Vector3=selected_approach.p
	var waypoint_blocked:=_walk([start,blocked,real_end],map_id,states)
	check(not bool(waypoint_blocked.reached) and str(waypoint_blocked.reason)=="waypoint_blocked","blocked intermediate waypoint is not skipped")
	results.append({"case":"blocked_waypoint","result":waypoint_blocked})
	var missing_zone:=selected_approach.duplicate(true);missing_zone.zone_id="__review_missing_zone__"
	var missing_errors:=_approach_errors(selected_room,missing_zone,structures)
	check(not missing_errors.is_empty(),"missing functional zone metadata is rejected")
	results.append({"case":"missing_zone","errors":missing_errors})
	var shifted_origin:=selected_approach.duplicate(true);shifted_origin.origin=Vector3(shifted_origin.origin)+Vector3.RIGHT
	var origin_errors:=_approach_errors(selected_room,shifted_origin,structures)
	check(not origin_errors.is_empty(),"approach origin detached from zone anchor is rejected")
	results.append({"case":"detached_origin","errors":origin_errors})
	var missing_approach_room:=selected_room.duplicate(true)
	var reduced:Array=Array(missing_approach_room.functional_approaches).duplicate(true);reduced.pop_back()
	missing_approach_room.functional_approaches=reduced
	var coverage_errors:=_approach_coverage_errors(missing_approach_room,structures)
	check(not coverage_errors.is_empty(),"a furniture structure without its approach is rejected")
	results.append({"case":"missing_furniture_approach","errors":coverage_errors})
	var duplicate_approach_room:=selected_room.duplicate(true)
	var duplicated:Array=Array(duplicate_approach_room.functional_approaches).duplicate(true);duplicated.append(selected_approach.duplicate(true))
	duplicate_approach_room.functional_approaches=duplicated
	var duplicate_errors:=_approach_coverage_errors(duplicate_approach_room,structures)
	check(not duplicate_errors.is_empty(),"duplicate furniture approach metadata is rejected")
	results.append({"case":"duplicate_furniture_approach","errors":duplicate_errors})
	var nonfinite_room:=selected_room.duplicate(true)
	for zone:Dictionary in nonfinite_room.functional_zones:
		if str(zone.get("id",""))==str(selected_approach.zone_id):zone.anchor=Vector3(NAN,zone.anchor.y,zone.anchor.z);break
	var nonfinite_errors:=_approach_errors(nonfinite_room,selected_approach,structures)
	check(not nonfinite_errors.is_empty(),"nonfinite functional zone anchor is rejected")
	results.append({"case":"nonfinite_zone_anchor","errors":nonfinite_errors})
	var forward:Array[Vector3]=[start,Vector3(selected_approach.origin),real_end]
	var incomplete:=_walk(forward,map_id,states,0)
	check(not bool(incomplete.reached) and str(incomplete.reason)=="incomplete" and int(incomplete.waypoint_index)<forward.size(),"exhausted budget never accepts proximity alone")
	results.append({"case":"zero_budget","result":incomplete})
	return results

func _case(seed_value:int,run_negatives:bool)->Dictionary:
	var map_id:=Generator.map_id(seed_value)
	var row:Dictionary={"seed":seed_value,"map_id":map_id,"rooms":0,"approaches":0,"physical_routes":0,"routes":[]}
	check(map_id=="house-v3-%d"%seed_value,"seed %d uses the exact v3 map identity"%seed_value)
	var generated:=Generator.new().generate(seed_value)
	var validation:=Validation.validate(generated)
	check(bool(validation.passed),"seed %d validates before physical access: %s"%[seed_value,validation.errors])
	var data:=Maps.get_map(map_id)
	if not check(not data.is_empty(),"seed %d loads through MapCatalog without fallback"%seed_value):return row
	check(int(data.get("generator_version",-1))==EXPECTED_VERSION and str(data.get("id",""))==map_id,"seed %d catalog preserves exact v3 identity"%seed_value)
	var states:=_open_doors(map_id)
	var structures:=_structures_by_id(data)
	row.rooms=data.rooms.size()
	for room:Dictionary in data.rooms:
		var approaches:Array=room.get("functional_approaches",[])
		check(not approaches.is_empty(),"seed %d room %s exposes functional approaches"%[seed_value,str(room.id)])
		var coverage_errors:=_approach_coverage_errors(room,structures)
		check(coverage_errors.is_empty(),"seed %d room %s has exactly one approach per furniture structure: %s"%[seed_value,str(room.id),coverage_errors])
		for approach:Dictionary in approaches:
			row.approaches=int(row.approaches)+1
			var metadata_errors:=_approach_errors(room,approach,structures)
			var label:="seed %d room %s zone %s structure %s"%[seed_value,str(room.id),str(approach.get("zone_id","")),str(approach.get("structure_id",""))]
			if not check(metadata_errors.is_empty(),label+" metadata resolves: "+str(metadata_errors)):continue
			var forward:Array[Vector3]=[Vector3(room.center),Vector3(approach.origin),Vector3(approach.p)]
			var reverse:Array[Vector3]=[Vector3(approach.p),Vector3(approach.origin),Vector3(room.center)]
			var forward_result:=_walk_and_check(forward,map_id,states,label+" forward")
			var reverse_result:=_walk_and_check(reverse,map_id,states,label+" reverse")
			row.physical_routes=int(row.physical_routes)+2
			row.routes.append({"room":room.id,"zone_id":approach.zone_id,"structure_id":approach.structure_id,
				"forward":forward_result,"reverse":reverse_result})
	if run_negatives:row.negative_cases=_negative_cases(data,map_id,states,structures)
	return row

func _finish(cases:Array[Dictionary],selected_corpus:Array[int])->void:
	var report:Dictionary={"checks":checks,"failures":failures,"cases":cases,"corpus":selected_corpus,
		"expected_generator_version":EXPECTED_VERSION,
		"scope":"production Arena.step_human authority over room center -> functional zone anchor -> furniture approach in both directions, with every generated door fully open; no renderer, visual approval, door transition, network, prediction, controller or WAN claim",
		"source_sha256":{}}
	for path:String in ["res://tests/review092_functional_approach_test.gd","res://scripts/procedural_house.gd","res://scripts/house_validation.gd","res://scripts/map_catalog.gd","res://scripts/arena.gd","res://scripts/door_catalog.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:check(false,"review092 report path writable")
	for failure:String in failures:print("REVIEW092_FUNCTIONAL_APPROACH_DIAGNOSTIC "+failure)
	var routes:=0
	for row:Dictionary in cases:routes+=int(row.get("physical_routes",0))
	print("REVIEW092_FUNCTIONAL_APPROACH_RESULT checks=%d failures=%d routes=%d"%[checks,failures.size(),routes])
	quit.call_deferred(0 if failures.is_empty() else 1)

func _run()->void:
	var corpus:Array[int]=CORPUS
	if only_seed!=0:
		check(only_seed in CORPUS,"selected seed belongs to the review092 corpus")
		corpus=[only_seed]
	if not check(Generator.VERSION==EXPECTED_VERSION,"functional approach gate requires Generator.VERSION == 3"):
		_finish([],corpus);return
	var cases:Array[Dictionary]=[]
	for index:int in range(corpus.size()):cases.append(_case(corpus[index],index==0))
	_finish(cases,corpus)
