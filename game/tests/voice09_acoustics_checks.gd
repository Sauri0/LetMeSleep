extends SceneTree
## Finite geometry/authority audit; no microphone, codec, network or listening claim.
const Acoustics=preload("res://scripts/voice_acoustics.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Doors=preload("res://scripts/door_catalog.gd")
const ArenaData=preload("res://scripts/arena.gd")
const Generator=preload("res://scripts/procedural_house.gd")
const ROLE_PAIRS: Array[Vector2i]=[Vector2i(0,0),Vector2i(0,1),Vector2i(1,1),Vector2i(1,0)]
const ROLES: Array[String]=["human","mosquito"]
var checks:=0
var failures: Array[String]=[]
var evidence: Array[Dictionary]=[]

func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=30: printerr("VOICE09_ACOUSTICS_FAIL "+label)

func actor(role: String,point: Vector3) -> Dictionary:
	return {"role":role,"alive":true,"p":point-Vector3.UP*1.48 if role=="human" else point,"crouch_amount":0.0}

func packed(point: Vector3) -> Array:
	return [point.x,point.y,point.z]

func path_points(path: PackedVector3Array) -> Array:
	var result: Array=[]
	for point: Vector3 in path: result.append(packed(point))
	return result

func permission(acoustics: RefCounted,pair: Vector2i,from: Vector3,to: Vector3,states: Dictionary={}) -> Dictionary:
	return acoustics.permission(actor(ROLES[pair.x],from),actor(ROLES[pair.y],to),states)

func _initialize() -> void:
	var began:=Time.get_ticks_usec()
	var source_hash:=FileAccess.get_sha256("res://scripts/voice_acoustics.gd") if FileAccess.file_exists("res://scripts/voice_acoustics.gd") else "unavailable_in_pack"
	_ranges_and_dead()
	for id: String in ["house",Generator.map_id(1),Generator.map_id(2)]:
		_map_geometry(id)
	var report: Dictionary={"checks":checks,"failures":failures,"elapsed_ms":(Time.get_ticks_usec()-began)/1000.0,"source_sha256":source_hash,"maps":evidence,"scope":"Finite authored/generated geometry and directed role permissions; no codec, ENet, audible mix or live frame-time claim."}
	var target: String="res://../work/voice09-acoustics-results.json"
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--voice-report="): target=argument.trim_prefix("--voice-report=")
	var output:=FileAccess.open(target,FileAccess.WRITE)
	if output==null: check(false,"report writable: "+target)
	else: output.store_string(JSON.stringify(report,"\t"));output.close()
	print("VOICE09_ACOUSTICS_RESULT checks=%d failures=%d elapsed_ms=%.3f"%[checks,failures.size(),(Time.get_ticks_usec()-began)/1000.0])
	quit(0 if failures.is_empty() else 1)

func _ranges_and_dead() -> void:
	var acoustics=Acoustics.new();acoustics.configure("house")
	var start:=Vector3(0,1.48,-5.1)
	for pair: Vector2i in ROLE_PAIRS:
		var label: String=ROLES[pair.x]+"->"+ROLES[pair.y]
		var maximum:=3.0 if pair==Vector2i(1,0) else 8.0 if pair.x==1 else 10.0
		var role_gain:=.25 if pair==Vector2i(1,0) else .85 if pair.x==1 else 1.0
		var previous:=INF
		for fraction: float in [.1,.5,.9,1.0,1.01]:
			var finish:=start+Vector3.BACK*maximum*fraction
			check(ArenaData.can_fit_mosquito(finish,"house") if pair.y==1 else ArenaData.can_fit_human(finish-Vector3.UP*1.48,ArenaData.HUMAN_HEIGHT,"house"),"range listener is physically valid "+label+" "+str(fraction))
			check(acoustics.clear_line(start,finish),"real central hallway clear "+label+" "+str(fraction))
			var result: Dictionary=permission(acoustics,pair,start,finish)
			if fraction>=1.0:
				check(result.is_empty(),"range %sm excludes boundary/outside %s %s"%[maximum,label,fraction]);continue
			check(not result.is_empty(),"within range audible "+label+" "+str(fraction))
			if result.is_empty(): continue
			check(absf(float(result.distance)-maximum*fraction)<.0001,"distance uses world meters "+label)
			check(absf(float(result.gain)-role_gain*pow(1.0-fraction*fraction,2))<.0001,"direct role attenuation "+label)
			check(float(result.gain)<previous and float(result.cutoff)==12000.0,"gain decreases with distance "+label)
			check(is_equal_approx(float(result.pitch),1.55 if pair.x==1 else 1.0),"pitch belongs to speaker role "+label)
			previous=float(result.gain)
		var speaker:=actor(ROLES[pair.x],start)
		var listener:=actor(ROLES[pair.y],start+Vector3.BACK)
		speaker.alive=false
		check(acoustics.permission(speaker,listener,{}).is_empty(),"dead speaker denied "+label)
		speaker.alive=true;listener.alive=false
		check(acoustics.permission(speaker,listener,{}).is_empty(),"dead listener denied "+label)
	var low_human:=actor("human",Vector3(0,1.48,0));low_human.crouch_amount=1.0
	check(Acoustics.mouth(low_human).is_equal_approx(Vector3(0,1.05,0)),"crouch changes actual source height")
	check(Acoustics.mouth(actor("mosquito",Vector3(0,2.1,0))).is_equal_approx(Vector3(0,2.1,0)),"mosquito source is its body position")

func _map_geometry(id: String) -> void:
	var acoustics=Acoustics.new();acoustics.configure(id)
	var data: Dictionary=Maps.get_map(id)
	check(not data.is_empty() and not acoustics.walls.is_empty(),"real map config has occluding structures "+id)
	if data.is_empty(): return
	var item: Dictionary={"map_id":id,"fingerprint":data.get("fingerprint",Maps.Validation.fingerprint(data)),"nodes":acoustics.nodes.size(),"raw_edges":data.get("nav_edges",[]).size(),"bad_edges":[],"edge_permission_witnesses":[],"portals":[],"sealed_floors":[],"around_opening":{}}
	# Each graph link is tested in exactly the acoustic space actually traversed.
	# Capsule-valid endpoints distinguish a usable leak from an unusable nav seed.
	for link: Vector2i in data.get("nav_edges",[]):
		var from: Vector3=acoustics.nodes[link.x]
		var to: Vector3=acoustics.nodes[link.y]
		var clear: bool=acoustics.clear_line(from,to)
		check(clear,"raised nav edge respects structure %s %s"%[id,link])
		if clear: continue
		item.bad_edges.append({"a":link.x,"b":link.y,"from":packed(from),"to":packed(to),"hits":_structural_hits(data,from,to)})
		if not ArenaData.can_fit_mosquito(from,id) or not ArenaData.can_fit_mosquito(to,id): continue
		var path: PackedVector3Array=acoustics.route(from,to,8.0)
		var result: Dictionary=permission(acoustics,Vector2i(1,1),from,to)
		if not result.is_empty() and not _path_clear(acoustics,path):
			item.edge_permission_witnesses.append({"from":packed(from),"to":packed(to),"path":path_points(path),"permission":result,"valid_mosquito_endpoints":true})
			check(false,"audible permission uses blocked graph segment "+id+" "+str(link))
	# Explicit doors: closed transmits muffled speech, it is not a silence switch.
	for door_id: String in acoustics.definitions:
		var definition: Dictionary=acoustics.definitions[door_id]
		var transform: Transform3D=Doors.leaf_transform(definition,0.0)
		var center: Vector3=transform*Vector3(float(definition.width)*.5,1.48,0)
		var direction: Vector3=transform.basis.z
		var from:=center-direction*.85
		var to:=center+direction*.85
		if not acoustics.clear_line(from,to) or not ArenaData.can_fit_mosquito(from,id) or not ArenaData.can_fit_mosquito(to,id): continue
		for pair: Vector2i in ROLE_PAIRS:
			var open_states: Dictionary={door_id:{"angle":Doors.OPEN_ANGLE}}
			var closed_states: Dictionary={door_id:{"angle":0.0}}
			var opened: Dictionary=permission(acoustics,pair,from,to,open_states)
			var closed: Dictionary=permission(acoustics,pair,from,to,closed_states)
			var reverse: Dictionary=permission(acoustics,pair,to,from,closed_states)
			check(not opened.is_empty() and not closed.is_empty(),"portal audibility open/closed "+id+" "+door_id+" "+str(pair))
			if opened.is_empty() or closed.is_empty(): continue
			check(absf(float(closed.gain)/float(opened.gain)-.25)<.0001 and float(closed.cutoff)==1200.0 and float(opened.cutoff)==12000.0,"closed door attenuation/filter "+id+" "+door_id)
			check(not reverse.is_empty() and absf(float(reverse.gain)-float(closed.gain))<.0001,"portal propagation reciprocal for fixed role pair "+id+" "+door_id)
		item.portals.append({"id":door_id,"from":packed(from),"to":packed(to)})
	check(not item.portals.is_empty(),"at least one physically clear real portal exercised "+id)
	_sealed_floor(acoustics,data,id,item)
	_around_opening(acoustics,id,item)
	evidence.append(item)

func _structural_hits(data: Dictionary,from: Vector3,to: Vector3) -> Array:
	var result: Array=[]
	for structure: Dictionary in data.get("structures",[]):
		if str(structure.get("kind","")) not in ["wall","floor","ceiling"]: continue
		var box: AABB=structure.box
		if box.intersects_segment(from,to)!=null: result.append({"kind":structure.kind,"box_p":packed(box.position),"box_size":packed(box.size)})
	return result

func _path_clear(acoustics: RefCounted,path: PackedVector3Array) -> bool:
	for index: int in range(1,path.size()):
		if not acoustics.clear_line(path[index-1],path[index]): return false
	return true

func _sealed_floor(acoustics: RefCounted,data: Dictionary,id: String,item: Dictionary) -> void:
	# Sample two legitimate insect positions just below/above an actual forjado.
	# An independent lower bound is twice the horizontal distance to the nearest
	# slab edge. If it exceeds 10 m, no legal detour fits any role's range.
	for structure: Dictionary in data.get("structures",[]):
		if str(structure.get("kind","")) not in ["floor","ceiling"]: continue
		var box: AABB=structure.box
		if box.position.y<1.0 or minf(box.size.x,box.size.z)<=10.0: continue
		var center:=box.get_center()
		var lower:=Vector3(center.x,box.position.y-.2,center.z)
		var upper:=Vector3(center.x,box.end.y+.2,center.z)
		if not ArenaData.can_fit_mosquito(lower,id) or not ArenaData.can_fit_mosquito(upper,id): continue
		check(not acoustics.clear_line(lower,upper),"physical ceiling/floor blocks direct sound "+id)
		check(acoustics.route(lower,upper,10.0).is_empty(),"slab detour lower bound exceeds maximum voice budget "+id)
		for ends: Array in [[lower,upper],[upper,lower]]:
			check(permission(acoustics,Vector2i(1,1),ends[0],ends[1]).is_empty(),"sealed floor denied both directions "+id)
		item.sealed_floors.append({"lower":packed(lower),"upper":packed(upper),"box_p":packed(box.position),"box_size":packed(box.size)})
		break
	check(not item.sealed_floors.is_empty(),"real sealed-floor witness exists "+id)

func _around_opening(acoustics: RefCounted,id: String,item: Dictionary) -> void:
	# Select a real unobstructed insect pair with a bent, short acoustic route.
	# Checking every returned segment catches graph shortcuts through a floor.
	var examined:=0
	for i: int in range(acoustics.nodes.size()):
		var from: Vector3=acoustics.nodes[i]
		if not ArenaData.can_fit_mosquito(from,id): continue
		for j: int in range(i+1,acoustics.nodes.size()):
			var to: Vector3=acoustics.nodes[j]
			if from.distance_to(to)>6.5 or acoustics.clear_line(from,to) or not ArenaData.can_fit_mosquito(to,id): continue
			examined+=1
			var path: PackedVector3Array=acoustics.route(from,to,8.0)
			if path.size()<3: continue
			var result: Dictionary=permission(acoustics,Vector2i(1,1),from,to)
			if result.is_empty(): continue
			check(_path_clear(acoustics,path),"bent route uses only visible segments "+id)
			var reverse: Dictionary=permission(acoustics,Vector2i(1,1),to,from)
			check(not reverse.is_empty() and absf(float(reverse.distance)-float(result.distance))<.001,"bent route reciprocal "+id)
			check(float(result.distance)>from.distance_to(to)+.01,"path budget includes corner detour "+id)
			var insufficient: float=(from.distance_to(to)+float(result.distance))*.5
			check(acoustics.route(from,to,insufficient).is_empty(),"Euclidean-near source remains inaudible when detour exceeds budget "+id)
			item.around_opening={"from":packed(from),"to":packed(to),"path":path_points(path),"permission":result,"examined_pairs":examined}
			return
	check(false,"real short bent opening route found "+id)
