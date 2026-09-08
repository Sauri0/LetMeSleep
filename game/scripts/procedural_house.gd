class_name ProceduralHouse
extends RefCounted
## Deterministic spatial assembly. Runtime integration follows validation and
## furnishing; never substitute the authored house when generation fails.
const VERSION := 1
const FLOOR_HEIGHT := 3.2
const WALL := .20
const DOOR_WIDTH := 2.0
const DOOR_HEIGHT := 2.45
const DoorGeometry=preload("res://scripts/door_geometry.gd")
const Furniture=preload("res://scripts/furniture_blueprint.gd")
const Placement = preload("res://scripts/pickup_placement.gd")
const SupportGeometry = preload("res://scripts/pickup_support_geometry.gd")
const ROOM_THEMES := [
	["Cocina","e8c57b","cabinet","kitchen"],["Comedor","d8b59c","table","dining"],
	["Lavadero","8ebcaf","cabinet","laundry"],["Baño","a5c9c0","dresser","bathroom"],
	["Dormitorio azul","9ab8c2","bed","bedroom_blue"],["Dormitorio rosa","cc9d8f","bed","bedroom_rose"],
	["Cuarto de visitas","b1bf97","bed","guest_room"],["Biblioteca","c5b594","cabinet","library"],
	["Sala de estar","dcaf93","sofa","living_room"],["Sala de música","b9adc9","desk","music_room"],
	["Taller de costura","c9b4a8","desk","sewing_room"],["Sala de juegos","d7c18f","table","game_room"],
	["Despensa","bfafd0","cabinet","pantry"],["Estudio","a9bec0","desk","study"],
	["Recibidor","e3c5a0","dresser","entry"],["Dormitorio verde","9caf90","bed","bedroom_green"]]
const TASKS := [["Cerrar la ventana","VENTANA"],["Prender el ventilador","VENTILADOR"],
	["Preparar repelente","REPELENTE"],["Apagar el equipo","EQUIPO"],
	["Acomodar las mantas","MANTAS"],["Revisar el mosquitero","MOSQUITERO"],
	["Buscar las sábanas","SÁBANAS"],["Guardar la vajilla","VAJILLA"]]
var _random_state := 1
var _solid_keys: Dictionary = {}
var _data: Dictionary = {}

func _next() -> int:
	_random_state = (_random_state*48271)%2147483647
	return _random_state

func _integer(low: int, high: int) -> int:
	return low+_next()%(high-low+1)

func _grid(low: float, high: float) -> float:
	return float(_integer(int(round(low*20.0)),int(round(high*20.0))))/20.0

static func map_id(seed_value: int) -> String:
	return "house-v%d-%d"%[VERSION,clampi(seed_value,1,2147483646)]

static func parse_seed(id: String) -> int:
	var prefix := "house-v%d-"%VERSION
	if not id.begins_with(prefix): return -1
	var digits := id.trim_prefix(prefix)
	if not digits.is_valid_int(): return -1
	var value := int(digits)
	return value if value>=1 and value<=2147483646 and str(value)==digits else -1

func generate_structure(seed_value: int) -> Dictionary:
	_random_state = clampi(seed_value,1,2147483646)
	_solid_keys.clear()
	var floor_count := _integer(2,3)
	var half_x := _grid(14.0,16.0)
	var half_z := _grid(11.0,12.5)
	var hall_half := _grid(1.7,2.1)
	_data={"id":map_id(seed_value),"label":"Casa de esta noche","playable":true,
		"generator_version":VERSION,"seed":clampi(seed_value,1,2147483646),
		"half_x":half_x,"half_z":half_z,"ceiling":floor_count*FLOOR_HEIGHT,
		"bounds":AABB(Vector3(-half_x,0,-half_z),Vector3(half_x*2,floor_count*FLOOR_HEIGHT,half_z*2)),
		"floor_levels":[],"door_height":DOOR_HEIGHT,"stair_rise":.2,"stair_tread":.5,
		"obstacles":[],"floors":[],"steps":[],"structures":[],"stair_holes":[],
		"rooms":[],"corridors":[],"doors":{},"portals":[],"stair_connections":[],
		"barrier_parts":[],"barrier_boxes":[],"pickup_supports":[],"pickup_support_boxes":[],"pickups":[],"stations":[],
		"human_spawns":[],"mosquito_spawns":[],"respawn_points":[],"lobby_spawns":[],
		"nav_nodes":[],"nav_edges":[],"generation_stage":"structure_unvalidated"}
	for floor_index: int in range(floor_count):
		var y := floor_index*FLOOR_HEIGHT
		_data.floor_levels.append(y)
		var slabs: Array[AABB]=[AABB(Vector3(-half_x,y-.2,-half_z),Vector3(half_x*2,.2,half_z*2))]
		if floor_index>0:
			for side: float in [-1.0,1.0]:
				var hole := AABB(Vector3(side*(half_x-3.0)-1.6,y-.2,-4.0),Vector3(3.2,.2,8.0))
				_data.stair_holes.append(hole)
				slabs=_subtract_slabs(slabs,hole)
		for slab: AABB in slabs: _solid("floor",slab,floor_index,"Piso")
		_solid("wall",AABB(Vector3(-half_x,y,-half_z),Vector3(.25,FLOOR_HEIGHT,half_z*2)),floor_index)
		_solid("wall",AABB(Vector3(half_x-.25,y,-half_z),Vector3(.25,FLOOR_HEIGHT,half_z*2)),floor_index)
		_solid("wall",AABB(Vector3(-half_x+.25,y,-half_z),Vector3(half_x*2-.5,FLOOR_HEIGHT,.25)),floor_index)
		_solid("wall",AABB(Vector3(-half_x+.25,y,half_z-.25),Vector3(half_x*2-.5,FLOOR_HEIGHT,.25)),floor_index)
		_floor_rooms(floor_index,floor_count,half_x,half_z,hall_half)
		if floor_index<floor_count-1:
			for side: float in [-1.0,1.0]: _stairs(floor_index,side,half_x)
	_solid("ceiling",AABB(Vector3(-half_x,floor_count*FLOOR_HEIGHT,-half_z),Vector3(half_x*2,.2,half_z*2)),floor_count-1,"Techo")
	return _data.duplicate(true)

func generate(seed_value: int) -> Dictionary:
	generate_structure(seed_value)
	# Substreams keep physical distribution independent of cosmetic choices.
	_random_state = 1+(clampi(seed_value,1,2147483646)*69621)%2147483646
	_furnish()
	_spawns()
	_assign_tasks()
	_barriers()
	_build_routes()
	_data.generation_stage = "furnished_unvalidated"
	return _data.duplicate(true)

func _solid(kind: String, box: AABB, floor_index: int, label: String="") -> void:
	if box.size.x<.001 or box.size.y<.001 or box.size.z<.001: return
	var key := str(box)
	if _solid_keys.has(key): return
	_solid_keys[key]=true
	_data.obstacles.append(box)
	_data.structures.append({"id":"solid-%04d"%(_data.structures.size()),"kind":kind,"box":box,
		"label":label,"style":"","color":Color("d8c2a2") if kind=="wall" else Color("b28d68"),"floor":floor_index})
	if kind=="floor": _data.floors.append(box)
	if kind=="step": _data.steps.append(box)

func _subtract_slabs(slabs: Array[AABB], hole: AABB) -> Array[AABB]:
	var result: Array[AABB]=[]
	for slab: AABB in slabs:
		var low_x := maxf(slab.position.x,hole.position.x)
		var high_x := minf(slab.end.x,hole.end.x)
		var low_z := maxf(slab.position.z,hole.position.z)
		var high_z := minf(slab.end.z,hole.end.z)
		if high_x<=low_x or high_z<=low_z: result.append(slab);continue
		for x_span: Vector2 in [Vector2(slab.position.x,low_x),Vector2(high_x,slab.end.x)]:
			if x_span.y>x_span.x: result.append(AABB(Vector3(x_span.x,slab.position.y,slab.position.z),Vector3(x_span.y-x_span.x,slab.size.y,slab.size.z)))
		for z_span: Vector2 in [Vector2(slab.position.z,low_z),Vector2(high_z,slab.end.z)]:
			if z_span.y>z_span.x: result.append(AABB(Vector3(low_x,slab.position.y,z_span.x),Vector3(high_x-low_x,slab.size.y,z_span.y-z_span.x)))
	return result

func _floor_rooms(floor_index: int, total_floors: int, half_x: float, half_z: float, hall_half: float) -> void:
	var y := floor_index*FLOOR_HEIGHT
	var north_end := -_grid(5.65,6.45)
	var south_start := _grid(5.65,6.45)
	var split_quadrants: Array[int]=[0,1,2,3]
	for index: int in range(3,0,-1):
		var swap := _integer(0,index)
		var previous := split_quadrants[index]
		split_quadrants[index]=split_quadrants[swap];split_quadrants[swap]=previous
	var split_count := _integer(2,4) if total_floors==2 else _integer(0,2)
	_data.corridors.append(AABB(Vector3(-hall_half,y,-half_z+.25),Vector3(hall_half*2,FLOOR_HEIGHT,half_z*2-.5)))
	_data.corridors.append(AABB(Vector3(-half_x+.25,y,north_end),Vector3(half_x*2-.5,FLOOR_HEIGHT,-3.8-north_end)))
	_data.corridors.append(AABB(Vector3(-half_x+.25,y,3.8),Vector3(half_x*2-.5,FLOOR_HEIGHT,south_start-3.8)))
	for quadrant: int in range(4):
		var right := quadrant%2==1
		var rear := quadrant>=2
		var low_x := hall_half if right else -half_x+.25
		var high_x := half_x-.25 if right else -hall_half
		var low_z := south_start if rear else -half_z+.25
		var high_z := half_z-.25 if rear else north_end
		var cuts: Array[float]=[low_x,high_x]
		if split_quadrants.find(quadrant)<split_count:
			cuts.insert(1,snappedf(lerpf(low_x,high_x,_grid(.40,.60)),.05))
		for column: int in range(cuts.size()-1):
			_room(AABB(Vector3(cuts[column],y,low_z),Vector3(cuts[column+1]-cuts[column],FLOOR_HEIGHT,high_z-low_z)),floor_index,2,low_z if rear else high_z,not rear)
	for right: bool in [false,true]:
		var low_x := hall_half if right else -half_x+4.8
		var high_x := half_x-4.8 if right else -hall_half
		_room(AABB(Vector3(low_x,y,-3.8),Vector3(high_x-low_x,FLOOR_HEIGHT,7.6)),floor_index,0,low_x if right else high_x,not right)

func _room(bounds: AABB, floor_index: int, door_axis: int, door_coordinate: float, _positive_side: bool) -> void:
	var id := "room-%02d"%_data.rooms.size()
	var along := 2 if door_axis==0 else 0
	var door_center: Vector3=bounds.get_center()
	var half_span := bounds.size[along]*.5
	var variation := minf(half_span-DOOR_WIDTH*.5-.65,.75)
	door_center[along]+=_grid(-maxf(0,variation),maxf(0,variation))
	door_center[door_axis]=door_coordinate;door_center.y=bounds.position.y
	_data.rooms.append({"id":id,"name":"Habitación %d"%(_data.rooms.size()+1),"label":"HABITACIÓN",
		"bounds":bounds,"floor":floor_index,"color":Color("b8c5b4"),"portal":door_center,"door_axis":door_axis})
	for axis: int in [0,2]:
		var a := 2 if axis==0 else 0
		for coordinate: float in [bounds.position[axis],bounds.end[axis]]:
			var exterior := absf(absf(coordinate)-(float(_data.half_x if axis==0 else _data.half_z)-.25))<.001
			if exterior: continue
			var is_door := axis==door_axis and absf(coordinate-door_coordinate)<.001
			var spans: Array[Vector2]=[Vector2(bounds.position[a],bounds.end[a])]
			if is_door: spans=[Vector2(bounds.position[a],door_center[a]-DOOR_WIDTH*.5),Vector2(door_center[a]+DOOR_WIDTH*.5,bounds.end[a])]
			for span: Vector2 in spans:
				var p := bounds.position; p[axis]=coordinate-WALL*.5;p[a]=span.x
				var size := Vector3(WALL,FLOOR_HEIGHT,WALL);size[a]=span.y-span.x
				_solid("wall",AABB(p,size),floor_index)
			if is_door:
				var p := door_center;p[axis]-=WALL*.5;p[a]-=DOOR_WIDTH*.5;p.y+=DOOR_HEIGHT
				var size := Vector3(WALL,FLOOR_HEIGHT-DOOR_HEIGHT,WALL);size[a]=DOOR_WIDTH
				_solid("wall",AABB(p,size),floor_index,"Dintel")
	var hinge := door_center
	hinge[along]-=DOOR_WIDTH*.5
	var yaw := 0.0 if door_axis==2 else -PI*.5
	var inside_sign := signf(bounds.get_center()[door_axis]-door_coordinate)
	_data.doors[id]={"id":id,"label":id,"hinge":hinge,"width":DOOR_WIDTH,"height":DOOR_HEIGHT,
		"thickness":.07,"closed_yaw":yaw,"open_sign":inside_sign*(1.0 if door_axis==0 else -1.0)}
	_data.portals.append({"id":id,"room":id,"p":door_center,"axis":door_axis,"width":DOOR_WIDTH,"height":DOOR_HEIGHT})

func _stairs(floor_index: int, side: float, half_x: float) -> void:
	var x := side*(half_x-3.0)
	var y := floor_index*FLOOR_HEIGHT
	var forward := 1.0 if side<0 else -1.0
	for step: int in range(16):
		var z := -4.0+float(step)*.5 if forward>0 else 3.5-float(step)*.5
		# Thin treads keep a full floor-height of headroom between stacked flights.
		_solid("step",AABB(Vector3(x-1.4,y+step*.2,z),Vector3(2.8,.2,.5)),floor_index,"Escalera")
	_data.stair_connections.append({"id":"stair-%d-%d"%[floor_index,int(side)],"floor":floor_index,
		"bottom":Vector3(x,y,-4.65*forward),"top":Vector3(x,y+FLOOR_HEIGHT,4.65*forward),"direction":forward,"width":2.8})

func _furnish() -> void:
	var themes: Array = ROOM_THEMES.duplicate(true)
	for index: int in range(themes.size()-1,0,-1):
		var other := _integer(0,index)
		var previous: Array = themes[index]
		themes[index]=themes[other];themes[other]=previous
	var floor_rooms: Dictionary = {}
	for index: int in range(_data.rooms.size()):
		var room: Dictionary = _data.rooms[index]
		var theme: Array = themes[index%themes.size()]
		room.name = str(theme[0])+(" %d"%(index/themes.size()+1) if index>=themes.size() else "")
		room.label = str(room.name).to_upper(); room.color=Color(str(theme[1]));room.theme=str(theme[2]);room.theme_id=str(theme[3])
		_data.doors[room.id].label=room.name
		var b: AABB = room.bounds
		var center := Vector3(b.get_center().x,b.position.y,b.get_center().z)
		# Align the clear room destination with the doorway, away from its open leaf.
		center[2 if int(room.door_axis)==0 else 0]=Vector3(room.portal)[2 if int(room.door_axis)==0 else 0]
		room.center=center
		var furniture: Array[Dictionary] = []
		# Door sweep and a full-width lane to the room centre are reserved first.
		var portal: Vector3=room.portal
		var lane := AABB(center.min(portal)-Vector3(.78,0,.78),(center-portal).abs()+Vector3(1.56,2.4,1.56))
		var axis: int=room.door_axis
		var along:=2 if axis==0 else 0
		var swing_origin:=portal-Vector3(1.15,0,1.15)
		var swing_size:=Vector3(2.3,2.45,2.3)
		swing_origin[axis]=portal[axis]-.15 if center[axis]>portal[axis] else portal[axis]-2.15
		var swing := AABB(swing_origin,swing_size)
		room.clearance=[lane,swing]
		var window_clearance:=AABB()
		if absf(b.position.z+float(_data.half_z)-.25)<.01 or absf(b.end.z-float(_data.half_z)+.25)<.01:
			var rear:=b.get_center().z>0
			window_clearance=AABB(Vector3(b.get_center().x-.82,b.position.y+1.12,b.end.z-1.05 if rear else b.position.z),Vector3(1.64,1.3,1.05))
		var access_lanes: Array[AABB]=[]
		var furnishings: Array[Dictionary]=Furniture.for_theme(room.theme_id)
		for item: int in range(furnishings.size()):
			var spec: Dictionary=furnishings[item]
			var candidates: Array[Dictionary]=[]
			for quarter: int in range(1 if bool(spec.pickup_surface) else 2):
				var size: Vector3=spec.size if quarter==0 else Vector3(spec.size.z,spec.size.y,spec.size.x)
				for fraction: float in [0.0,.125,.25,.375,.5,.625,.75,.875,1.0]:
					for z: float in [b.position.z+.3,b.end.z-.3-size.z]: candidates.append({"box":AABB(Vector3(lerpf(b.position.x+.3,b.end.x-.3-size.x,fraction),b.position.y,z),size),"quarter":quarter})
					for x: float in [b.position.x+.3,b.end.x-.3-size.x]: candidates.append({"box":AABB(Vector3(x,b.position.y,lerpf(b.position.z+.3,b.end.z-.3-size.z,fraction)),size),"quarter":quarter})
			# Prefer corners far from the entrance; dimensions and theme vary by seed.
			candidates.sort_custom(func(a:Dictionary,c:Dictionary)->bool:return AABB(a.box).position.distance_squared_to(portal)>AABB(c.box).position.distance_squared_to(portal))
			for candidate: Dictionary in candidates:
				var box: AABB=candidate.box
				var visual_box:=box
				visual_box.size.y=float(spec.visual_size.y)
				if box.intersects(lane) or box.intersects(swing) or (window_clearance.has_volume() and visual_box.intersects(window_clearance)): continue
				var free := true
				for access: AABB in access_lanes:
					if box.intersects(access): free=false;break
				for placed: Dictionary in furniture:
					if box.grow(.18).intersects(placed.box): free=false;break
				if not free: continue
				if bool(spec.pickup_surface):
					var approach:=Vector3(box.get_center().x,b.position.y,box.end.z+.72 if box.get_center().z<center.z else box.position.z-.72)
					var body_margin:=Vector3(.63,0,.63)
					if not b.has_point(approach-body_margin+Vector3.UP*.1) or not b.has_point(approach+body_margin+Vector3.UP*.1): continue
					# The approach must not cut across the very table it serves.
					var expanded:=AABB(box.position-body_margin,box.size+body_margin*2)
					if expanded.intersects_segment(center+Vector3.UP*.1,approach+Vector3.UP*.1)!=null: continue
					var access:=AABB(center.min(approach)-Vector3(.65,0,.65),(center-approach).abs()+Vector3(1.3,2,1.3))
					if DoorGeometry.intersects_body(_data.doors[room.id],PI*.5,access): continue
				_solid("furniture",box,int(room.floor),str(spec.label))
				var entry: Dictionary=_data.structures.back()
				entry.merge(spec,true);entry.color=room.color;entry.room=room.id
				entry.rotation_y=(PI if box.get_center().z<center.z else 0.0) if int(candidate.quarter)==0 else (-PI*.5 if box.get_center().x<center.x else PI*.5)
				furniture.append(entry)
				if bool(spec.pickup_surface):
					var approach:=Vector3(box.get_center().x,b.position.y,box.end.z+.72 if box.get_center().z<center.z else box.position.z-.72)
					access_lanes.append(AABB(center.min(approach)-Vector3(.65,0,.65),(center-approach).abs()+Vector3(1.3,2,1.3)))
				break
		room.furniture_count=furniture.size()
		room.clearance.append_array(access_lanes)
		if not floor_rooms.has(room.floor): floor_rooms[room.floor]=[]
		floor_rooms[room.floor].append(room)
		_data.human_spawns.append(center)
		_data.mosquito_spawns.append(center+Vector3.UP*1.35)
		_data.respawn_points.append(center+Vector3.UP*1.6)
		# Keep support geometry and its interaction approach in the same blueprint.
		for entry: Dictionary in furniture:
			if bool(entry.get("pickup_surface",false)): room.pickup_table=entry.box;break
	for floor_index: int in floor_rooms:
		var rooms: Array=floor_rooms[floor_index]
		for tool: String in ["broom","slipper"]:
			for room: Dictionary in rooms:
				if _place_support(room,tool): break
		for index: int in range(rooms.size()):
			var room: Dictionary=rooms[index]
			if not room.has("pickup_table"): continue
			var table: AABB=room.pickup_table
			var tool: String=["swatter","racket","newspaper"][index%3]
			var support:=Vector3(table.get_center().x,table.end.y,table.get_center().z)
			var approach:=Vector3(table.get_center().x,table.position.y,table.end.z+.72 if table.get_center().z<Vector3(room.center).z else table.position.z-.72)
			_data.pickups.append(Placement.resolve({"tool":tool,"support_point":support,"support":"Mesa de "+str(room.name),
				"support_origin":table.position,"approach":approach,"rotation":Vector3(PI/2,PI/2,0),"room":room.id}))
			room.pickup_approach=approach
	# Shuffle spawn order independently: spread teams and avoid sharing one position.
	for key: String in ["human_spawns","mosquito_spawns","respawn_points"]:
		for index: int in range(_data[key].size()-1,0,-1):
			var other:=_integer(0,index)
			var previous: Vector3=_data[key][index]
			_data[key][index]=_data[key][other];_data[key][other]=previous

func _spawns() -> void:
	var ground: Array[Dictionary]=[]
	var upper: Array[Dictionary]=[]
	for room: Dictionary in _data.rooms:
		if int(room.floor)==0: ground.append(room)
		if int(room.floor)==_data.floor_levels.size()-1: upper.append(room)
	for rooms: Array[Dictionary] in [ground,upper]:
		for index: int in range(rooms.size()-1,0,-1):
			var other:=_integer(0,index)
			var previous: Dictionary=rooms[index];rooms[index]=rooms[other];rooms[other]=previous
	_data.human_spawns.clear();_data.mosquito_spawns.clear()
	for index: int in range(5): _data.human_spawns.append(ground[index].center)
	for offset: Vector3 in [Vector3(-.28,1.35,-.28),Vector3(.28,1.35,.28),Vector3(-.28,1.35,.28),Vector3(.28,1.35,-.28)]:
		for room: Dictionary in upper:
			var candidate:=Vector3(room.center)+offset
			var separated:=true
			for human: Vector3 in _data.human_spawns:
				var blocked:=false
				for obstacle: AABB in _data.obstacles:
					if obstacle.intersects_segment(human+Vector3.UP*1.4,candidate)!=null: blocked=true;break
				if not blocked: separated=false;break
			if separated: _data.mosquito_spawns.append(candidate)
			if _data.mosquito_spawns.size()==12: return

func _assign_tasks() -> void:
	var used: Dictionary={}
	var floor_counts: Dictionary={}
	var assigned: Dictionary={}
	# Reserve rooms with real beds/windows before generic table tasks can use
	# them. Publish in the original task order after constrained assignment.
	for index: int in [4,6,0,5,1,2,3,7]:
		var label: String=TASKS[index][1]
		var candidates: Array[Dictionary]=[]
		for room: Dictionary in _data.rooms:
			if used.has(room.id): continue
			var b: AABB=room.bounds
			if label in ["VENTANA","MOSQUITERO"] and absf(b.position.z+float(_data.half_z)-.25)>.01 and absf(b.end.z-float(_data.half_z)+.25)>.01: continue
			if label in ["MANTAS","SÁBANAS"]:
				var has_bed:=false
				for item: Dictionary in _data.structures:
					if str(item.get("room",""))==str(room.id) and str(item.get("asset_id",""))=="bed": has_bed=true;break
				if not has_bed: continue
			candidates.append(room)
		candidates.sort_custom(func(a:Dictionary,b:Dictionary)->bool:
			var a_count: int=floor_counts.get(a.floor,0);var b_count: int=floor_counts.get(b.floor,0)
			return a_count<b_count if a_count!=b_count else str(a.id)<str(b.id))
		if candidates.is_empty(): continue
		var room: Dictionary=candidates[0]
		used[room.id]=true;floor_counts[room.floor]=int(floor_counts.get(room.floor,0))+1
		var station: Dictionary={"name":TASKS[index][0],"label":label,"p":room.center,"room":room.id}
		if label in ["VENTILADOR","REPELENTE","EQUIPO","VAJILLA"] and room.has("pickup_table") and room.has("pickup_approach"):
			var table: AABB=room.pickup_table
			station.p=room.pickup_approach
			station.display_p=Vector3(table.get_center().x-table.size.x*.34,table.end.y,table.get_center().z)
			station.display_scale=.4
			station.display_yaw=PI if table.get_center().z<Vector3(room.center).z else 0.0
			var size:=Vector3(.30,.62,.30) if label=="VENTILADOR" else Vector3(.32,.22,.24)
			var prop_box:=AABB(-Vector3(size.x*.5,0,size.z*.5),size)
			if label=="EQUIPO":
				# Radio GLB bounds include its antenna; visual scale is .4 * 1.4.
				prop_box=AABB(Vector3(-.245,.01,-.1365)*.56,Vector3(.49,.5709462,.2515)*.56)
			prop_box=Transform3D(Basis(Vector3.UP,float(station.display_yaw)),station.display_p)*prop_box
			_solid("task_prop",prop_box,int(room.floor),label)
		assigned[index]=station
	for index: int in range(TASKS.size()):
		if assigned.has(index): _data.stations.append(assigned[index])

func _place_support(room: Dictionary, tool: String) -> bool:
	var b: AABB=room.bounds
	var center: Vector3=room.center
	var kind: String="broom_rack" if tool=="broom" else "shoe_bench"
	var size:=Vector3(.48,1.09,.24) if tool=="broom" else Vector3(.9,.34,.4)
	var obstacles: Array=_data.obstacles.duplicate()
	obstacles.append_array(_data.pickup_support_boxes)
	for z_fraction: float in [.9,.7,.5,.3]:
		for x_fraction: float in [.1,.9,.3,.7,.5]:
			var origin:=Vector3(lerpf(b.position.x+.3,b.end.x-.3-size.x,x_fraction),b.position.y,lerpf(b.position.z+1.4,b.end.z-.3-size.z,z_fraction))
			var box:=AABB(origin,size)
			var approach:=Vector3(origin.x+size.x*.5,b.position.y,origin.z-.76)
			var access:=AABB(center.min(approach)-Vector3(.63,0,.63),(center-approach).abs()+Vector3(1.26,1.95,1.26))
			if box.intersects(access) or DoorGeometry.intersects_body(_data.doors[room.id],PI*.5,access): continue
			var free:=true
			for clearance: AABB in room.clearance:
				if box.grow(.08).intersects(clearance): free=false;break
			for obstacle: AABB in obstacles:
				if obstacle.end.y<=b.position.y+.002 or obstacle.position.y>=b.position.y+1.95: continue
				if box.grow(.06).intersects(obstacle) or access.intersects(obstacle): free=false;break
			if not free: continue
			var id:=str(room.id)+"-"+tool
			var spec: Dictionary={"id":id,"kind":kind,"origin":origin}
			_data.pickup_supports.append(spec)
			for part: Dictionary in SupportGeometry.parts(spec):
				var part_box: AABB=part.box;part_box.position+=origin
				_data.pickup_support_boxes.append(part_box)
			var support:=origin+(Vector3(.24,.035,.11) if tool=="broom" else Vector3(.45,.34,.2))
			_data.pickups.append(Placement.resolve({"tool":tool,"support_point":support,"support_origin":origin,
				"support":("Portaescobas de " if tool=="broom" else "Zapatero de ")+str(room.name),
				"approach":approach,"rotation":Vector3(0,0,PI) if tool=="broom" else Vector3(-PI/2,-PI/2,0),"room":room.id}))
			if not room.has("support_approaches"): room.support_approaches=[]
			room.support_approaches.append(approach)
			room.clearance.append(access)
			return true
	return false

func _barrier_box(kind: String, box: AABB) -> void:
	_data.barrier_boxes.append(box)
	_data.barrier_parts.append({"kind":kind,"box":box})

func _barriers() -> void:
	for stair: Dictionary in _data.stair_connections:
		var x: float=Vector3(stair.bottom).x
		var floor_y: float=Vector3(stair.bottom).y
		for side: float in [-1.0,1.0]:
			var edge_x:=x+side*1.48
			for step: int in range(9):
				var z: float=-4.0+step
				var y: float=floor_y+((z+4.0) if float(stair.direction)>0 else (4.0-z))*.4
				_barrier_box("post",AABB(Vector3(edge_x-.035,y,z-.035),Vector3(.07,.96,.07)))
			var a:=Vector3(edge_x,floor_y+(.99 if float(stair.direction)>0 else 4.19),-4)
			var b:=Vector3(edge_x,floor_y+(4.19 if float(stair.direction)>0 else .99),4)
			_data.barrier_parts.append({"kind":"slope","from":a,"to":b})
			for step: int in range(64):
				var start:=a.lerp(b,float(step)/64);var end:=a.lerp(b,float(step+1)/64)
				_data.barrier_boxes.append(AABB(Vector3(edge_x-.0475,minf(start.y,end.y)-.0425,start.z),Vector3(.095,absf(end.y-start.y)+.085,end.z-start.z)))
			var top_y:=floor_y+FLOOR_HEIGHT
			edge_x=x+side*1.6
			for step: int in range(9): _barrier_box("post",AABB(Vector3(edge_x-.035,top_y,-4.035+step),Vector3(.07,.96,.07)))
			_barrier_box("bar",AABB(Vector3(edge_x-.0475,top_y+.9475,-4),Vector3(.095,.085,8)))
			_barrier_box("bar",AABB(Vector3(edge_x-.0225,top_y+.07,-4),Vector3(.045,.055,8)))

func _node(point: Vector3) -> int:
	var nodes: Array=_data.nav_nodes
	for index: int in range(nodes.size()):
		if Vector3(nodes[index]).distance_squared_to(point)<.000001: return index
	nodes.append(point)
	return nodes.size()-1

func _link(a: Vector3, b: Vector3) -> void:
	var first:=_node(a);var second:=_node(b)
	if first==second: return
	var edge:=Vector2i(mini(first,second),maxi(first,second))
	if not _data.nav_edges.has(edge): _data.nav_edges.append(edge)

func _build_routes() -> void:
	for floor_y: float in _data.floor_levels:
		var floor_corridors: Array[AABB]=[]
		for corridor: AABB in _data.corridors:
			if absf(corridor.position.y-floor_y)<.001: floor_corridors.append(corridor)
		var north: float=floor_corridors[1].get_center().z
		var south: float=floor_corridors[2].get_center().z
		_link(Vector3(0,floor_y,north),Vector3(0,floor_y,south))
		for room: Dictionary in _data.rooms:
			if absf(Vector3(room.center).y-floor_y)>.001: continue
			var p: Vector3=room.portal
			var center: Vector3=room.center
			var turn:=center
			var along:=2 if int(room.door_axis)==0 else 0
			turn[along]=p[along]
			_link(center,turn);_link(turn,p)
			var hall:=p
			if int(room.door_axis)==0: hall.x=0
			else: hall.z=north if p.z<0 else south
			_link(p,hall)
			_link(hall,Vector3(0,floor_y,hall.z))
			_link(Vector3(0,floor_y,hall.z),Vector3(0,floor_y,north if hall.z<0 else south))
			if room.has("pickup_approach"): _link(center,room.pickup_approach)
			for approach: Vector3 in room.get("support_approaches",[]): _link(center,approach)
		for side: float in [-1.0,1.0]:
			var x: float=side*(float(_data.half_x)-3.0)
			for z: float in [north,south]: _link(Vector3(0,floor_y,z),Vector3(x,floor_y,z))
	for stair: Dictionary in _data.stair_connections:
		var previous: Vector3=stair.bottom
		for step: int in range(16):
			var point:=Vector3(previous.x,int(stair.floor)*FLOOR_HEIGHT+(step+1)*.2,(-4.25+step*.5)*float(stair.direction))
			_link(previous,point);previous=point
		_link(previous,stair.top)
		for landing: Vector3 in [Vector3(stair.bottom),Vector3(stair.top)]:
			for corridor: AABB in _data.corridors:
				if absf(corridor.position.y-landing.y)<.001 and corridor.size.x>corridor.size.z and corridor.has_point(landing+Vector3.UP*.1):
					_link(landing,Vector3(landing.x,landing.y,corridor.get_center().z))
