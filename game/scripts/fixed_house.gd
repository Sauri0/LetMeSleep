class_name FixedHouse
extends RefCounted
## Authored plan: coordinates below are design decisions, never a random seed.
const ID := "house-patio-v1"
const LABEL := "Casa con patio"
const VERSION := 1
const Furniture=preload("res://scripts/furniture_blueprint.gd")
const Placement=preload("res://scripts/pickup_placement.gd")
const Support=preload("res://scripts/pickup_support_geometry.gd")
const Geometry=preload("res://scripts/navigation_geometry.gd")
const DoorGeometry=preload("res://scripts/door_geometry.gd")
const Canonical=preload("res://scripts/house_validation.gd")
var d: Dictionary
var solid_keys: Dictionary={}
var room_index: Dictionary={}

func build() -> Dictionary:
	d={"id":ID,"label":LABEL,"playable":true,"authored_version":VERSION,
		"half_x":20.0,"half_z":20.0,"ceiling":8.8,
		"bounds":AABB(Vector3(-20,0,-20),Vector3(40,8.8,40)),
		"building_bounds":AABB(Vector3(-12,0,-10),Vector3(24,6.4,20)),
		"floor_levels":[0.0,3.2],"door_height":2.45,"stair_rise":.2,"stair_tread":.4,
		"obstacles":[],"floors":[],"steps":[],"structures":[],"rooms":[],"corridors":[],
		"doors":{},"portals":[],"windows":[],"stair_holes":[],"stair_connections":[],
		"barrier_parts":[],"barrier_boxes":[],"pickup_supports":[],"pickup_support_boxes":[],
		"pickups":[],"stations":[],"human_spawns":[],"mosquito_spawns":[],"respawn_points":[],
		"lobby_spawns":[],"nav_nodes":[],"nav_edges":[],"circulation_routes":[],"stair_light_anchors":[],
		"exterior":{"bounds":AABB(Vector3(-20,0,-20),Vector3(40,8.8,40)),"areas":[],"paths":[],"props":[]}}
	solid_keys.clear();room_index.clear()
	_shell()
	_rooms()
	_stairs()
	_furniture()
	_outside()
	_tasks_and_pickups()
	_spawns()
	_navigation()
	d.furniture_count=0
	for item: Dictionary in d.structures:
		if item.kind=="furniture": d.furniture_count+=1
	d.authored_stage="complete"
	d.fingerprint=fingerprint(d)
	return d.duplicate(true)

static func fingerprint(data: Dictionary) -> String:
	var source:=data.duplicate(true)
	source.erase("fingerprint")
	return JSON.stringify(Canonical._canonical(source)).sha256_text()

func _solid(kind: String, box: AABB, floor_index: int, label: String="", tint: Color=Color("cbbba4")) -> Dictionary:
	var key:=str(box)
	if solid_keys.has(key): return solid_keys[key]
	var item: Dictionary={"id":"fixed-solid-%03d"%d.structures.size(),"kind":kind,"box":box,"floor":floor_index,"label":label,"style":"","color":tint}
	d.obstacles.append(box);d.structures.append(item);solid_keys[key]=item
	if kind=="floor":d.floors.append(box)
	if kind=="step":d.steps.append(box)
	return item

func _slabs_without(slabs: Array[AABB], hole: AABB) -> Array[AABB]:
	var result: Array[AABB]=[]
	for slab: AABB in slabs:
		var x0:=maxf(slab.position.x,hole.position.x);var x1:=minf(slab.end.x,hole.end.x)
		var z0:=maxf(slab.position.z,hole.position.z);var z1:=minf(slab.end.z,hole.end.z)
		if x1<=x0 or z1<=z0:result.append(slab);continue
		for span: Vector2 in [Vector2(slab.position.x,x0),Vector2(x1,slab.end.x)]:
			if span.y>span.x:result.append(AABB(Vector3(span.x,slab.position.y,slab.position.z),Vector3(span.y-span.x,slab.size.y,slab.size.z)))
		for span: Vector2 in [Vector2(slab.position.z,z0),Vector2(z1,slab.end.z)]:
			if span.y>span.x:result.append(AABB(Vector3(x0,slab.position.y,span.x),Vector3(x1-x0,slab.size.y,span.y-span.x)))
	return result

func _wall(axis: int, coordinate: float, low: float, high: float, y: float, height: float=3.2, thickness: float=.2) -> void:
	if high-low<.001:return
	var along:=2 if axis==0 else 0
	var p:=Vector3.ZERO;p[axis]=coordinate-thickness*.5;p[along]=low;p.y=y
	var size:=Vector3(thickness,height,thickness);size[along]=high-low
	_solid("wall",AABB(p,size),int(y/3.2))

func _door_wall(id: String, axis: int, coordinate: float, low: float, high: float, y: float, along: float, inward: float, width: float=2.0, exterior: bool=false) -> void:
	_wall(axis,coordinate,low,along-width*.5,y)
	_wall(axis,coordinate,along+width*.5,high,y)
	_wall(axis,coordinate,along-width*.5,along+width*.5,y+2.45,.75)
	var p:=Vector3.ZERO;p[axis]=coordinate;p[2 if axis==0 else 0]=along;p.y=y
	var hinge:=p;hinge[2 if axis==0 else 0]-=width*.5
	d.doors[id]={"id":id,"label":id,"hinge":hinge,"width":width,"height":2.45,"thickness":.07,
		"closed_yaw":-PI*.5 if axis==0 else 0.0,"open_sign":inward if axis==0 else -inward,"exterior":exterior}
	d.portals.append({"id":id,"room":id,"p":p,"axis":axis,"width":width,"height":2.45,"exterior":exterior})

func _shell() -> void:
	_solid("floor",AABB(Vector3(-12,-.2,-10),Vector3(24,.2,20)),0,"Planta baja")
	for box: AABB in [AABB(Vector3(-20,-.2,-20),Vector3(40,.2,10)),AABB(Vector3(-20,-.2,10),Vector3(40,.2,10)),AABB(Vector3(-20,-.2,-10),Vector3(8,.2,20)),AABB(Vector3(12,-.2,-10),Vector3(8,.2,20))]:
		var item:=_solid("floor",box,0,"Jardín",Color("667945"));item.exterior=true
	var slabs: Array[AABB]=[AABB(Vector3(-12,3,-10),Vector3(24,.2,20))]
	for x: float in [-10.0,10.0]:
		var hole:=AABB(Vector3(x-1.6,3,-3.2),Vector3(3.2,.2,6.4));d.stair_holes.append(hole)
		slabs=_slabs_without(slabs,hole)
	for box: AABB in slabs:_solid("floor",box,1,"Planta alta")
	_solid("ceiling",AABB(Vector3(-12,6.4,-10),Vector3(24,.2,20)),1,"Techo del edificio")
	for y: float in [0.0,3.2]:
		for x: float in [-11.875,11.875]:_wall(0,x,-10,10,y,3.2,.25)
		if y==0:
			_door_wall("front-door",2,-9.875,-11.75,11.75,0,0,1,2.4,true)
			_door_wall("patio-door",2,9.875,-11.75,11.75,0,0,-1,2.4,true)
		else:
			for z: float in [-9.875,9.875]:_wall(2,z,-11.75,11.75,y,3.2,.25)
		for box: AABB in [AABB(Vector3(-2,y,-9.75),Vector3(4,3.0,19.5)),AABB(Vector3(-11.75,y,-4.8),Vector3(23.5,3.0,2.5)),AABB(Vector3(-11.75,y,2.3),Vector3(23.5,3.0,2.5))]:d.corridors.append(box)
		for z: float in [-3.9,3.9]:_route("cross_hall",Vector3(-10,y,z),Vector3(10,y,z),1.5)
		_route("spine",Vector3(0,y,-8),Vector3(0,y,8),3.6)
		for x: float in [-7.45,7.45]:_route("stair_side",Vector3(x,y,-3.9),Vector3(x,y,3.9),1.5)

func _room(id: String,name: String,theme: String,floor_index: int,x0: float,x1: float,z0: float,z1: float,axis: int,portal: Vector2) -> void:
	var y:=floor_index*3.2
	var bounds:=AABB(Vector3(x0,y,z0),Vector3(x1-x0,3.0,z1-z0))
	var door:=Vector3(portal.x,y,portal.y)
	var center:=Vector3(bounds.get_center().x,y,bounds.get_center().z)
	var inward:=signf(center[axis]-door[axis])
	for a: int in [0,2]:
		var along:=2 if a==0 else 0
		for c: float in [bounds.position[a],bounds.end[a]]:
			if absf(c)==11.75 or absf(c)==9.75:continue
			if a==axis and absf(c-door[a])<.01:_door_wall(id,a,c,bounds.position[along],bounds.end[along],y,door[along],inward)
			else:_wall(a,c,bounds.position[along],bounds.end[along],y)
	var inside:=bounds.grow(-.1);inside.position.y=y;inside.size.y=3.0
	var room: Dictionary={"id":id,"name":name,"label":name.to_upper(),"theme":theme,"theme_id":theme,"floor":floor_index,"bounds":bounds,"interior_bounds":inside,"color":Color("a5b5a0"),"portal":door,"door_axis":axis,"center":center,"uses":[theme],"area_m2":inside.size.x*inside.size.z,"functional_approaches":[],"clearance":[],"furniture_count":0,"exterior":false}
	room.functional_zones=[{"id":id+"/"+theme,"use":theme,"bounds":inside,"anchor":center}]
	d.rooms.append(room);room_index[id]=room;d.doors[id].label=name
	if z0< -9 or z1>9:
		var rear:=z1>9
		d.windows.append({"id":id+"-window","p":Vector3(bounds.get_center().x,y+1.55,9.875 if rear else -9.875),"axis":2,"rear":rear,"room":name,"room_id":id,"tint":room.color})

func _rooms() -> void:
	_room("living","Sala de estar","living_room",0,-11.75,-2,-9.75,-4.8,2,Vector2(-7,-4.8))
	_room("kitchen","Cocina","kitchen",0,2,11.75,-9.75,-4.8,2,Vector2(4,-4.8))
	_room("bath-ground","Baño de visitas","bathroom",0,-11.75,-7,4.8,9.75,2,Vector2(-9.3,4.8))
	_room("laundry","Lavadero","laundry",0,-7,-2,4.8,9.75,2,Vector2(-3.4,4.8))
	_room("dining","Comedor al patio","dining",0,2,11.75,4.8,9.75,2,Vector2(4,4.8))
	_room("entry","Recibidor","entry",0,-6.5,-2,-2.3,2.3,0,Vector2(-2,0))
	_room("study","Estudio","study",0,2,6.5,-2.3,2.3,0,Vector2(2,0))
	_room("blue","Dormitorio azul","bedroom_blue",1,-11.75,-2,-9.75,-4.8,2,Vector2(-7,-4.8))
	_room("rose","Dormitorio rosa","bedroom_rose",1,2,11.75,-9.75,-4.8,2,Vector2(7,-4.8))
	_room("guest","Cuarto de visitas","guest_room",1,-11.75,-7,4.8,9.75,2,Vector2(-8.3,4.8))
	_room("bath-upper","Baño familiar","bathroom",1,-7,-2,4.8,9.75,2,Vector2(-3.4,4.8))
	_room("library","Biblioteca","library",1,2,11.75,4.8,9.75,2,Vector2(7,4.8))
	_room("music","Sala de música","music_room",1,-6.5,-2,-2.3,2.3,0,Vector2(-2,0))
	_room("sewing","Taller de costura","sewing_room",1,2,6.5,-2.3,2.3,0,Vector2(2,0))

func _route(kind: String,a: Vector3,b: Vector3,width: float) -> void:
	d.circulation_routes.append({"id":"fixed-route-%02d"%d.circulation_routes.size(),"kind":kind,"floor":roundi(a.y/3.2),"from":a,"to":b,"clear_width":width})

func _barrier(kind: String,box: AABB) -> void:
	d.barrier_boxes.append(box);d.barrier_parts.append({"kind":kind,"box":box})

func _stairs() -> void:
	for x: float in [-10.0,10.0]:
		var direction:=1.0 if x<0 else -1.0
		var bottom:=Vector3(x,0,-3.9*direction);var top:=Vector3(x,3.2,3.9*direction)
		var stair: Dictionary={"id":"west-stair" if x<0 else "east-stair","floor":0,"bottom":bottom,"top":top,"direction":direction,"width":2.8,"center_x":x,"run_start_z":-3.2,"run_end_z":3.2,"hole_width":3.2,"rise":.2,"tread":.4,"steps":16}
		for end_name: String in ["bottom","top"]:
			var p: Vector3=stair[end_name]
			stair[end_name+"_landing"]=AABB(Vector3(x-1.4,p.y,-4.7 if p.z<0 else 3.2),Vector3(2.8,2.05,1.5))
		d.stair_connections.append(stair)
		d.stair_light_anchors.append({"id":stair.id+"-light","p":Vector3(x,3.8,0),"target":Vector3(x,1.7,0),"range":7.0})
		for step: int in range(16):
			var z:=-3.2+step*.4 if direction>0 else 2.8-step*.4
			_solid("step",AABB(Vector3(x-1.4,step*.2,z),Vector3(2.8,.2,.4)),0,"Escalera")
		for side: float in [-1.0,1.0]:
			for i: int in range(9):
				var z:=-3.2+i*.8;var y:=(z+3.2)*.5 if direction>0 else (3.2-z)*.5
				_barrier("post",AABB(Vector3(x+side*1.48-.035,y,z-.035),Vector3(.07,.96,.07)))
				_barrier("post",AABB(Vector3(x+side*1.6-.035,3.2,z-.035),Vector3(.07,.96,.07)))
			_barrier("bar",AABB(Vector3(x+side*1.6-.0475,4.1475,-3.2),Vector3(.095,.085,6.4)))
			var a:=Vector3(x+side*1.48,.99 if direction>0 else 4.19,-3.2)
			var b:=Vector3(x+side*1.48,4.19 if direction>0 else .99,3.2)
			d.barrier_parts.append({"kind":"slope","from":a,"to":b})
			for i: int in range(64):
				var p:=a.lerp(b,i/64.0);var q:=a.lerp(b,(i+1)/64.0)
				d.barrier_boxes.append(AABB(Vector3(p.x-.0475,minf(p.y,q.y)-.0425,p.z),Vector3(.095,absf(p.y-q.y)+.085,q.z-p.z)))

func _piece(room_id: String,asset: String,x: float,z: float,yaw: float,ax: float,az: float) -> void:
	var room: Dictionary=room_index[room_id];var spec: Dictionary={}
	for row: Dictionary in Furniture.for_theme(room.theme_id):
		if row.asset_id==asset:spec=row.duplicate(true);break
	if spec.is_empty():
		var measured: Dictionary=Furniture.ASSETS[asset]
		spec={"asset_id":asset,"label":asset,"style":"table" if asset=="nightstand" else "cabinet","size":Vector3(measured.size.x,measured.height,measured.size.z),"visual_size":measured.size,"visual_scale":1.0,"surface_height":measured.height,"pickup_surface":false,"theme_id":room.theme_id}
	var size: Vector3=spec.size
	if absf(sin(yaw))>.5:size=Vector3(size.z,size.y,size.x)
	var box:=AABB(Vector3(x,int(room.floor)*3.2,z),size)
	var item:=_solid("furniture",box,int(room.floor),spec.label,room.color)
	item.merge(spec,true);item.room=room_id;item.room_id=room_id;item.rotation_y=yaw
	item.functional_zone_id=room_id+"/"+room.theme_id;item.essential=true
	item.approach=Vector3(ax,box.position.y,az)
	room.functional_approaches.append({"structure_id":item.id,"zone_id":item.functional_zone_id,"origin":room.center,"p":item.approach})
	room.furniture_count+=1
	if bool(spec.pickup_surface):
		var support_point:=Vector3(box.get_center().x,box.end.y,box.get_center().z)
		room.pickup_table=box;room.pickup_approach=item.approach
		room.pickup_surface={"structure_id":item.id,"box":box,"approach":item.approach,"support_point":support_point,"rotation":Vector3(PI/2,PI/2,0),"task_display_p":support_point,"task_display_yaw":yaw}

func _furniture() -> void:
	# asset, fixed lower-left X/Z, yaw, fixed standing X/Z. No placement solver.
	var plans: Dictionary={
		"living":[["sofa",-11.4,-9.3,0,-10.2,-7.6],["table",-7.3,-9.2,0,-6.6,-7.55],["armchair",-4.0,-9.3,0,-3.3,-7.5]],
		"kitchen":[["table",5.0,-9.4,0,5.9,-7.6],["sink",7.0,-9.4,0,7.8,-7.85],["stove",9.0,-9.4,0,9.8,-7.85],["fridge",10.6,-6.4,PI/2,9.85,-6.0]],
		"bath-ground":[["bath_vanity",-11.4,8.7,PI,-10.6,7.9],["toilet",-8.0,8.5,PI,-7.8,7.7],["bath_shower",-11.4,5.2,-PI/2,-9.5,6.0]],
		"laundry":[["table",-6.7,8.6,PI,-6.0,7.85],["washer",-3.9,8.7,PI,-3.1,7.9],["wardrobe",-6.7,5.2,PI,-6.15,6.7]],
		"dining":[["table",9.8,8.6,PI,10.4,7.85],["dining_set",6.2,8.7,PI,7.45,7.95],["dresser",10.6,5.2,PI/2,9.8,5.6]],
		"entry":[["table",-6.3,1.2,0,-5.6,.4],["wardrobe",-6.3,-2.1,PI,-5.6,-.6],["armchair",-3.4,1.15,0,-2.8,.35]],
		"study":[["desk",3.8,1.2,0,4.5,.4],["bookcase",4.9,-2.1,PI,5.6,-.7],["armchair",2.2,1.2,0,2.8,.4]],
		"blue":[["desk",-6,-9.3,0,-5.3,-7.7],["bed",-11.5,-9.3,0,-8.75,-8.7],["wardrobe",-3.8,-9.3,0,-3.1,-7.75],["nightstand",-11.3,-7.97,PI,-11.0,-6.7]],
		"rose":[["table",7,-9.3,0,7.7,-7.7],["bed",2.2,-9.3,0,4.95,-8.7],["dresser",10.4,-9.3,0,10.8,-7.9],["nightstand",2.4,-7.97,PI,2.8,-6.7]],
		"guest":[["table",-8.8,8.6,PI,-8.1,7.8],["bed",-11.5,5.15,0,-8.6,5.75],["wardrobe",-11.4,8.8,PI,-10.75,8.0],["nightstand",-11.3,6.48,PI,-11.0,7.75]],
		"bath-upper":[["bath_vanity",-6.7,8.7,PI,-5.9,7.9],["toilet",-3.0,8.5,PI,-2.7,7.7],["bath_shower",-6.7,5.2,PI,-6.15,6.8]],
		"library":[["desk",6.4,8.6,PI,7.1,7.85],["bookcase",2.3,8.8,PI,3.0,8.0],["armchair",10.0,8.5,PI,10.6,7.75]],
		"music":[["table",-6.3,1.2,0,-5.6,.4],["piano",-6.3,-2.1,PI,-5.6,-.6],["bookcase",-3.4,1.6,0,-2.8,.8]],
		"sewing":[["table",3.8,1.2,0,4.5,.4],["sewing_table",4.9,-2.1,PI,5.6,-.5],["wardrobe",2.2,1.2,0,2.8,.4]]}
	for id: String in plans:
		for p: Array in plans[id]:_piece(id,p[0],p[1],p[2],p[3],p[4],p[5])

func _outside() -> void:
	for spec: Array in [["front","Frente",Rect2(-19.5,-19.5,39,9.5)],["patio","Patio",Rect2(-19.5,10,39,9.5)],["west-garden","Jardín oeste",Rect2(-19.5,-10,7.5,20)],["east-garden","Jardín este",Rect2(12,-10,7.5,20)]]:
		var rect: Rect2=spec[2]
		d.exterior.areas.append({"id":spec[0],"label":spec[1],"kind":"garden","bounds":AABB(Vector3(rect.position.x,0,rect.position.y),Vector3(rect.size.x,8.8,rect.size.y))})
	for points: Array in [[Vector3(0,0,-17),Vector3(0,0,-9.875)],[Vector3(0,0,9.875),Vector3(0,0,16)],[Vector3(-16,0,-16),Vector3(16,0,-16)],[Vector3(-16,0,16),Vector3(16,0,16)],[Vector3(-16,0,-16),Vector3(-16,0,16)],[Vector3(16,0,-16),Vector3(16,0,16)]]:
		d.exterior.paths.append({"id":"path-%02d"%d.exterior.paths.size(),"kind":"stone","points":[points[0],points[1]],"from":points[0],"to":points[1],"width":2.6,"surface":"stone"})
		_route("exterior_path",points[0],points[1],2.6)
	for axis: int in [0,2]:
		for side: float in [-19.7,19.7]:
			var p:=Vector3(-19.7,0,-19.7);p[axis]=side
			var size:=Vector3(39.4,1.3,39.4);size[axis]=.15
			_solid("fence",AABB(p,size),0,"Cerca del jardín",Color("866548"))
	for x: float in [-17.8,17.8]:
		for z: float in [-12.5,12.5]:
			var box:=AABB(Vector3(x-.3,0,z-.3),Vector3(.6,3.2,.6))
			var item:=_solid("tree_trunk",box,0,"Pino",Color("795a3e"));item.exterior=true
			d.exterior.props.append({"id":"pine-%d"%d.exterior.props.size(),"asset_id":"alfa_pine","p":Vector3(x,0,z),"yaw":0.0,"scale":Vector3.ONE,"collision_boxes":[box]})
	for x: float in [-7.0,7.0]:
		var box:=AABB(Vector3(x-.9,0,13.7),Vector3(1.8,.9,.65))
		var item:=_solid("outdoor_furniture",box,0,"Banco del patio",Color("9b7553"));item.exterior=true
		d.exterior.props.append({"id":"patio-bench-"+str(x),"asset_id":"alfa_bench","p":Vector3(x,0,14.025),"yaw":0.0,"scale":Vector3.ONE,"collision_boxes":[box]})
	_solid("outdoor_furniture",AABB(Vector3(10,0,12),Vector3(1.6,.75,.9)),0,"Mesa del patio",Color("9b7553"))
	d.exterior.props.append({"id":"patio-table","asset_id":"alfa_patio_table","p":Vector3(10.8,0,12.45),"yaw":0.0,"scale":Vector3.ONE,"collision_boxes":[AABB(Vector3(10,0,12),Vector3(1.6,.75,.9))]})

func _support(room_id: String,tool: String,origin: Vector3) -> void:
	var spec: Dictionary={"id":room_id+"-"+tool,"kind":"broom_rack" if tool=="broom" else "shoe_bench","origin":origin}
	d.pickup_supports.append(spec)
	for part: Dictionary in Support.parts(spec):
		var box: AABB=part.box;box.position+=origin;d.pickup_support_boxes.append(box)
	var contact:=origin+(Vector3(.24,.035,.11) if tool=="broom" else Vector3(.45,.34,.2))
	d.pickups.append(Placement.resolve({"tool":tool,"support_point":contact,"support_origin":origin,"support":room_id,"room":room_id,"approach":origin+Vector3(.24 if tool=="broom" else .45,0,-.8),"rotation":Vector3(0,0,PI) if tool=="broom" else Vector3(-PI/2,-PI/2,0)}))

func _tasks_and_pickups() -> void:
	var table_tasks: Array=[["living","Prender el ventilador","VENTILADOR"],["kitchen","Guardar la vajilla","VAJILLA"],["study","Apagar el equipo","EQUIPO"],["dining","Preparar repelente","REPELENTE"]]
	var assigned: Dictionary={}
	for row: Array in table_tasks:
		var room: Dictionary=room_index[row[0]];var surface: Dictionary=room.pickup_surface
		d.stations.append({"id":"task-"+row[0],"name":row[1],"label":row[2],"room":room.id,"p":surface.approach,"display_p":surface.support_point,"display_yaw":surface.task_display_yaw,"display_scale":.4,"support_structure_id":surface.structure_id})
		assigned[room.id]=true
	for row: Array in [["blue","Acomodar las mantas","MANTAS"],["rose","Buscar las sábanas","SÁBANAS"]]:
		for item: Dictionary in d.structures:
			if item.get("room","")==row[0] and item.get("asset_id","")=="bed":
				d.stations.append({"id":"task-"+row[0],"name":row[1],"label":row[2],"room":row[0],"p":item.approach,"display_p":Vector3(item.box.get_center().x,item.box.end.y,item.box.get_center().z),"display_scale":.4,"support_structure_id":item.id});break
	for row: Array in [["library","Revisar el mosquitero","MOSQUITERO",Vector3(8.8,3.2,8.65)],["living","Cerrar la ventana","VENTANA",Vector3(-8.05,0,-8.6)]]:
		d.stations.append({"id":"task-window-"+row[0],"name":row[1],"label":row[2],"room":row[0],"p":row[3],"window_id":row[0]+"-window"})
	var index:=0
	for id: String in ["entry","laundry","blue","rose","guest","library","music","sewing"]:
		var surface: Dictionary=room_index[id].pickup_surface
		d.pickups.append(Placement.resolve({"tool":["swatter","racket","newspaper"][index%3],"support_point":surface.support_point,"support_origin":surface.box.position,"support":"Mesa de "+room_index[id].name,"approach":surface.approach,"room":id,"rotation":Vector3(PI/2,PI/2,0)}));index+=1
	_support("patio","broom",Vector3(13.0,0,12.0))
	_support("patio","slipper",Vector3(-10.0,0,13.0))
	_support("library","broom",Vector3(4.8,3.2,8.8))
	_support("library","slipper",Vector3(9,3.2,6.4))

func _spawns() -> void:
	d.human_spawns=[Vector3(-.8,0,-6.3),Vector3(.8,0,-6.3),Vector3(-.8,0,-4.5),Vector3(.8,0,-4.5),Vector3(0,0,-2)]
	for x: float in [4.0,6.4,8.0,10.0]:
		for z: float in [6.5,7.0,7.5]:d.mosquito_spawns.append(Vector3(x,4.55,z))
	for p: Vector3 in [Vector3(0,0,-7),Vector3(0,0,7),Vector3(0,0,15),Vector3(-16,0,0),Vector3(16,0,0),Vector3(0,3.2,-7),Vector3(0,3.2,7)]:d.respawn_points.append(p+Vector3.UP*1.35)

func _node(p: Vector3) -> int:
	for i: int in range(d.nav_nodes.size()):
		if Vector3(d.nav_nodes[i]).distance_squared_to(p)<.000001:return i
	d.nav_nodes.append(p);return d.nav_nodes.size()-1

func _navigation() -> void:
	var extra: Array[AABB]=[]
	extra.assign(d.barrier_boxes);extra.append_array(d.pickup_support_boxes)
	for door: Dictionary in d.doors.values():extra.append(DoorGeometry.leaf_transform(door,PI*.5)*DoorGeometry.leaf_box(door))
	var geometry:=Geometry.create(d,true,extra)
	# Deliberate room grids are walkability samples only; they never place geometry.
	for room: Dictionary in d.rooms:
		var b: AABB=room.interior_bounds
		for fx: float in [.05,.275,.5,.725,.95]:
			for fz: float in [.05,.275,.5,.725,.95]:
				var p:=Vector3(lerpf(b.position.x+.65,b.end.x-.65,fx),b.position.y,lerpf(b.position.z+.65,b.end.z-.65,fz))
				if Geometry._fits(p,geometry):_node(p)
		for approach: Dictionary in room.functional_approaches:_node(approach.p)
	for portal: Dictionary in d.portals:
		_node(portal.p)
		if not room_index.has(portal.room):continue
		var room: Dictionary=room_index[portal.room]
		var inward:=Vector3.ZERO;inward[int(portal.axis)]=signf(Vector3(room.center)[int(portal.axis)]-Vector3(portal.p)[int(portal.axis)])
		for distance: float in [-.9,.9,1.8,2.8,3.1]:
			var p: Vector3=portal.p+inward*distance
			if Geometry._fits(p,geometry):_node(p)
	for route: Dictionary in d.circulation_routes:
		var a: Vector3=route.from;var b: Vector3=route.to
		var steps:=maxi(1,ceili(a.distance_to(b)/3.0))
		for i: int in range(steps+1):_node(a.lerp(b,float(i)/steps))
	for p: Dictionary in d.stations:_node(p.p)
	for group: Array in [d.pickups,d.stations]:
		for item: Dictionary in group:
			var p: Vector3=item.get("approach",item.get("p",Vector3.ZERO))
			_node(p)
			for offset: Vector3 in [Vector3.LEFT,Vector3.RIGHT,Vector3.FORWARD,Vector3.BACK]:
				var q:=p+offset*1.5
				if Geometry._fits(q,geometry) and Geometry._segment(p,q,geometry):_node(q)
	for p: Vector3 in d.human_spawns:_node(p)
	for p: Vector3 in d.respawn_points:_node(p-Vector3.UP*1.35)
	for stair: Dictionary in d.stair_connections:
		_node(stair.bottom);_node(stair.top)
		for i: int in range(16):_node(Vector3(stair.center_x,(i+1)*.2,(-3.43+i*.4)*float(stair.direction)))
	for i: int in range(d.nav_nodes.size()):
		var a: Vector3=d.nav_nodes[i]
		for j: int in range(i+1,d.nav_nodes.size()):
			var b: Vector3=d.nav_nodes[j]
			if a.distance_to(b)>4.2:continue
			if Geometry._segment(a,b,geometry) and Geometry._segment(b,a,geometry):d.nav_edges.append(Vector2i(i,j))
