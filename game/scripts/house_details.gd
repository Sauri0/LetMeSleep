extends RefCounted
## Authored dressing aligned to the physical map and shared open balustrades.
const Library = preload("res://assets/art/house/house_library.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Barriers = preload("res://scripts/house_barriers.gd")

static func window_specs(data: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for room: Dictionary in data.rooms:
		var b: AABB = room.bounds
		if b.position.z < -8.0 or b.end.z>10.5:
			var rear := b.get_center().z>0.0
			result.append({"p":Vector3(b.get_center().x+minf(b.size.x*.33,2.6),b.position.y+1.55,10.875 if rear else -10.875),"axis":2,"rear":rear,"room":str(room.name),"tint":room.color})
	for floor_y: float in [0.0,3.2]:
		result.append({"p":Vector3(0,floor_y+1.60,-10.875),"axis":2,"rear":false,"room":"Pasillo","tint":Color("91a4ab")})
	# Stair windows occupy the real exterior wall, clear of the slab/landings.
	for x: float in [-13.875,13.875]:
		result.append({"p":Vector3(x,4.85,0),"axis":0,"rear":x>0,"room":"Escalera","tint":Color("7c9d9d")})
	return result

static func wall_pieces(bounds: AABB, windows: Array[Dictionary]) -> Array[AABB]:
	var result: Array[AABB] = [bounds]
	var axis := 0 if bounds.size.x < bounds.size.z else 2
	var along := 2 if axis==0 else 0
	for window: Dictionary in windows:
		if int(window.axis)!=axis or absf(Vector3(window.p)[axis]-bounds.get_center()[axis])>.3: continue
		var p: Vector3 = window.p
		var next: Array[AABB] = []
		for piece: AABB in result:
			var low := maxf(piece.position[along],p[along]-.735)
			var high := minf(piece.end[along],p[along]+.735)
			var bottom := maxf(piece.position.y,p.y-.72)
			var top := minf(piece.end.y,p.y+.72)
			if high<=low or top<=bottom: next.append(piece);continue
			for span: Vector2 in [Vector2(piece.position[along],low),Vector2(high,piece.end[along])]:
				if span.y-span.x>.001:
					var cut := piece;cut.position[along]=span.x;cut.size[along]=span.y-span.x;next.append(cut)
			for span: Vector2 in [Vector2(piece.position.y,bottom),Vector2(top,piece.end.y)]:
				if span.y-span.x>.001:
					var cut := piece;cut.position[along]=low;cut.size[along]=high-low;cut.position.y=span.x;cut.size.y=span.y-span.x;next.append(cut)
		result=next
	return result

static func asset(parent: Node3D, name: String, at: Vector3, scale: Vector3 = Vector3.ONE, yaw: float = 0.0) -> Node3D:
	var view := Library.instantiate_asset(name)
	parent.add_child(view)
	view.position=at;view.scale=scale;view.rotation.y=yaw
	return view

static func floor_pieces(bounds:AABB) -> Array[AABB]:
	var result:Array[AABB]=[]
	var x:=bounds.position.x
	while x<bounds.end.x-.001:
		var z:=bounds.position.z
		var width:=minf(4.0,bounds.end.x-x)
		while z<bounds.end.z-.001:
			var depth:=minf(4.0,bounds.end.z-z)
			result.append(AABB(Vector3(x,bounds.position.y,z),Vector3(width,bounds.size.y,depth)))
			z+=depth
		x+=width
	return result

static func _warm_diffuser(node:Node3D) -> void:
	var meshes:Array[Node]=node.find_children("*","MeshInstance3D",true,false)
	if node is MeshInstance3D:meshes.append(node)
	for mesh:MeshInstance3D in meshes:
		for index:int in range(mesh.mesh.get_surface_count()):
			var material:StandardMaterial3D=mesh.mesh.surface_get_material(index)
			if "linen" not in material.resource_name:continue
			var warm:StandardMaterial3D=material.duplicate()
			warm.albedo_color=Color("ead4a6")
			warm.emission_enabled=true;warm.emission=Color("e7bd78");warm.emission_energy_multiplier=.45
			mesh.set_surface_override_material(index,warm)

static func trim(world: Node3D, bounds: AABB) -> void:
	var along := 0 if bounds.size.x>bounds.size.z else 2
	var axis := 2 if along==0 else 0
	for floor_y: float in [0.0,3.2]:
		var ceiling:=2.989 if floor_y==0.0 else 6.4
		var baseboard:=bounds.position.y<=floor_y+.001 and bounds.end.y>=floor_y+.13
		var cornice:=bounds.position.y<ceiling and bounds.end.y>=ceiling-.001
		if not baseboard and not cornice:continue
		for side: float in [-1.0,1.0]:
			var normal:=Vector3.ZERO;normal[axis]=side
			# The authored profile protrudes towards local-Z negative. A proper
			# rotation preserves winding; mirrored scales inverted two wall runs.
			var tangent:=Vector3.UP.cross(-normal)
			var p:=bounds.position
			p[axis]=bounds.get_center()[axis]+side*(bounds.size[axis]*.5+.002)
			p[along]=bounds.position[along] if tangent[along]>0.0 else bounds.end[along]
			for is_cornice:bool in [false,true]:
				if (is_cornice and not cornice) or (not is_cornice and not baseboard):continue
				var root:=Node3D.new();world.map_root.add_child(root)
				root.name="Cornice" if is_cornice else "Baseboard"
				root.position=p;root.position.y=ceiling-.078 if is_cornice else floor_y
				root.basis=Basis(tangent,Vector3.UP,-normal)
				asset(root,"molding",Vector3.ZERO,Vector3(bounds.size[along],.6 if is_cornice else 1.0,1))

static func interior_bounds(world:Node3D,bounds:AABB) -> AABB:
	var start:=bounds.position;var end:=bounds.end
	# Room labels intentionally leave a10cm margin. Finish surfaces meet the
	# actual wall faces, instead of reproducing that metadata margin as a stripe.
	for axis:int in [0,2]:
		var other:=2 if axis==0 else 0
		for entry:Dictionary in world.map_data.structures:
			if entry.kind!="wall":continue
			var wall:AABB=entry.box
			if wall.size[axis]>.35 or wall.position.y>bounds.position.y+.01 or wall.end.y<bounds.position.y+2.0:continue
			if minf(wall.end[other],bounds.end[other])-maxf(wall.position[other],bounds.position[other])<.3:continue
			if absf(wall.end[axis]-bounds.position[axis])<.151:start[axis]=wall.end[axis]
			if absf(wall.position[axis]-bounds.end[axis])<.151:end[axis]=wall.position[axis]
	return AABB(start,end-start)

static func build_room(world: Node3D, room: Dictionary) -> void:
	var bounds: AABB=room.bounds
	var center:=bounds.get_center();var y:=bounds.position.y
	var name:=str(room.name);var color:Color=world._room_color(room)
	var wet:=name in ["Cocina","Lavadero","Baño"]
	world._room_wall_finish(bounds,color,wet)
	var room_root:=Node3D.new();room_root.name="RoomDetails_"+name.validate_node_name()
	room_root.set_meta("room_name",name);room_root.set_meta("floor",int(room.floor));world.map_root.add_child(room_root)
	_warm_diffuser(asset(room_root,"pendant",Vector3(center.x,y+(2.99 if int(room.floor)==0 else 3.19),center.z)))
	if wet:
		var floor_material:Material=world._surface_material(Color("a5b4ad") if name=="Baño" else Color("b5ac95"),"tile")
		world._box(room_root,Vector3(center.x,y+.003,center.z),Vector3(bounds.size.x,.005,bounds.size.z),floor_material).cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	else:
		var rug_size:=Vector3(minf(bounds.size.x*.62,3.7),.012,minf(bounds.size.z*.44,2.6))
		var rug_at:=Vector3(center.x,y+.011,center.z)
		var rug:MeshInstance3D=world._box(room_root,rug_at,rug_size,world._surface_material(color.darkened(.30),"cloth"))
		rug.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		for side: float in [-1.0,1.0]:
			world._box(room_root,rug_at+Vector3(0,.009,side*(rug_size.z*.5-.10)),Vector3(rug_size.x-.18,.003,.045),world.cream).cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	# Place detail on a solid wall face. Nothing crosses a physical doorway.
	var faces:=solid_faces(world,bounds)
	var decorated:=0
	for wall:Dictionary in faces:
		if decorated>=2:break
		var mounted:=Node3D.new();room_root.add_child(mounted)
		mounted.position=Vector3(wall.p)+Vector3(wall.normal)*.025
		mounted.look_at(mounted.global_position-Vector3(wall.normal))
		var model_name:=room_wall_asset(name) if decorated==0 else "wall_shelf" if room_wall_asset(name)=="wall_art" else "wall_art"
		if model_name=="bath_mirror":
			for furnishing:Node in world.map_root.get_children():
				if furnishing.get_meta("catalog_kind","")!="furniture" or str(furnishing.get_child(0).get_meta("authored_asset",""))!="bath_vanity":continue
				var box:AABB=furnishing.get_meta("catalog_box")
				mounted.position=Vector3(box.get_center().x-.50*box.size.x/1.6,y+1.70,-10.735)
				mounted.rotation=Vector3.ZERO
		var visual:=asset(mounted,model_name,Vector3(0,-.04,.13),Vector3.ONE,PI)
		if model_name=="bath_mirror":
			visual.scale=Vector3.ONE*.69
			_mirror_material(visual)
		# Wall names are discreet navigation plaques, not floating room banners.
		if decorated==0:
			world._box(mounted,Vector3(0,.69,0),Vector3(.80,.17,.025),world.wood)
			world._label(mounted,str(room.label),Vector3(0,.69,.02),.00130,Color("efe0ba"))
		decorated+=1
	room_root.set_meta("identity",true)
	room_root.set_meta("wall_details",decorated)
	if name=="Baño":
		var probe:=ReflectionProbe.new();room_root.add_child(probe)
		probe.name="BathroomReflectionOnce";probe.position=Vector3(center.x,y+1.65,center.z)
		probe.size=Vector3(bounds.size.x,2.9,bounds.size.z);probe.interior=true
		probe.box_projection=true;probe.update_mode=ReflectionProbe.UPDATE_ONCE
		probe.intensity=.45;probe.enable_shadows=false;probe.max_distance=5.0
		probe.ambient_mode=ReflectionProbe.AMBIENT_DISABLED

static func room_wall_asset(name:String) -> String:
	if name=="Baño":return "bath_mirror"
	if name=="Cocina":return "kitchen_rack"
	if name=="Biblioteca":return "wall_books"
	if name=="Sala de música":return "wall_guitar"
	if name=="Taller de costura":return "thread_rack"
	if name in ["Recibidor","Lavadero"]:return "entry_rack"
	if name in ["Despensa","Sala de juegos"]:return "wall_shelf"
	return "wall_art"

static func solid_faces(world:Node3D,bounds:AABB) -> Array[Dictionary]:
	var result:Array[Dictionary]=[]
	# Prefer a Z wall and then an X wall, giving useful views from both entries.
	for axis:int in [2,0]:
		var best:Dictionary={};var best_width:=0.0
		for structure:Dictionary in world.map_data.structures:
			if str(structure.kind)!="wall":continue
			var wall:AABB=structure.box
			if wall.position.y>bounds.position.y+.1 or wall.end.y<bounds.position.y+2.7:continue
			var other:int=0 if axis==2 else 2
			var low:float=maxf(wall.position[other],bounds.position[other]);var high:float=minf(wall.end[other],bounds.end[other])
			if high-low<1.4:continue
			var normal:=Vector3.ZERO;var face:=0.0
			if absf(wall.end[axis]-bounds.position[axis])<.2:face=wall.end[axis]+.006;normal[axis]=1
			elif absf(wall.position[axis]-bounds.end[axis])<.2:face=wall.position[axis]-.006;normal[axis]=-1
			else:continue
			var p:=Vector3.ZERO;p[axis]=face;p[other]=(low+high)*.5;p.y=bounds.position.y+1.91
			var covers_window:=false
			for window:Dictionary in world._house_windows:
				if int(window.axis)==axis and absf(Vector3(window.p)[axis]-face)<.3 and absf(Vector3(window.p)[other]-p[other])<1.40 and absf(Vector3(window.p).y-p.y)<1.2:
					covers_window=true;break
			if not covers_window and high-low>best_width:
				best={"p":p,"normal":normal};best_width=high-low
		if not best.is_empty():result.append(best)
	return result

static func _mirror_material(node:Node3D) -> void:
	var meshes:Array[Node]=node.find_children("*","MeshInstance3D",true,false)
	if node is MeshInstance3D:meshes.append(node)
	for mesh:MeshInstance3D in meshes:
		for index:int in range(mesh.mesh.get_surface_count()):
			var material:StandardMaterial3D=mesh.mesh.surface_get_material(index)
			if "glass" in material.resource_name:
				var mirror:StandardMaterial3D=material.duplicate()
				mirror.metallic=.85;mirror.roughness=.16;mirror.albedo_color=Color("abb8bf")
				mesh.set_surface_override_material(index,mirror)

static func build_windows(world: Node3D, specs: Array[Dictionary]) -> void:
	var glass:=StandardMaterial3D.new()
	glass.albedo_color=Color(.30,.48,.64,.12)
	glass.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
	glass.roughness=.22;glass.cull_mode=BaseMaterial3D.CULL_DISABLED
	for spec: Dictionary in specs:
		var p:Vector3=spec.p
		var root:=Node3D.new();root.name="Window_"+str(spec.room).validate_node_name();world.map_root.add_child(root)
		root.position=p
		if int(spec.axis)==2:root.rotation.y=PI if bool(spec.rear) else 0.0
		else:root.rotation.y=-PI/2 if bool(spec.rear) else PI/2
		# Window +Z points indoors; Blender furnishings face -Z.
		asset(root,"window_frame",Vector3(0,0,.03),Vector3.ONE,PI)
		world._box(root,Vector3(0,0,.003),Vector3(1.45,1.42,.008),glass).cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		for side:float in [-1.0,1.0]:
			var curtain:=asset(root,"curtain",Vector3(side*.91,-.74,.18),Vector3(1.05,.95,1))
			Library._tint_cloth(curtain,Color(spec.tint).lightened(.15))
		world._segment(root,Vector3(-1.10,.88,.18),Vector3(1.10,.88,.18),.024,world.gold)
		# The cool environment already supplies the night fill. A second,
		# unshadowed source at every window leaked across nearby room walls and
		# raised the scene from23 to39 positional lights (Compatibility cap32).

static func build_portals(world: Node3D) -> void:
	var doors:Dictionary=Doors.get_doors("house")
	for structure:Dictionary in world.map_data.structures:
		if str(structure.get("label",""))!="Dintel":continue
		var box:AABB=structure.box
		var center:=box.get_center();var floor_y:=3.2 if box.position.y>3 else 0.0
		var dynamic_portal:=false
		for definition:Dictionary in doors.values():
			var door_center:Vector3=Doors.leaf_transform(definition,0.0)*Vector3(float(definition.width)*.5,1.2,0)
			if Vector2(door_center.x,door_center.z).distance_to(Vector2(center.x,center.z))<.3 and absf(Vector3(definition.hinge).y-floor_y)<.1:
				dynamic_portal=true;break
		if dynamic_portal:continue
		var width:float=maxf(box.size.x,box.size.z)
		var p:=Vector3(box.position.x,floor_y,box.get_center().z)
		var yaw:=0.0
		if box.size.z>box.size.x:
			p=Vector3(box.get_center().x,floor_y,box.position.z);yaw=-PI/2
		var frame:=asset(world.map_root,"door_frame",p,Vector3(width,1,1),yaw)
		frame.set_meta("portal_width",width);frame.set_meta("fixed_open_portal",true)

static func build_stairs(world: Node3D) -> void:
	var root:=Node3D.new();root.name="Balustrades";world.map_root.add_child(root)
	for part:Dictionary in Barriers.get_parts():
		if str(part.kind)=="post":
			var box:AABB=part.box
			asset(root,"rail_post",Vector3(box.get_center().x,box.position.y,box.get_center().z))
		elif str(part.kind)=="bar":
			var box:AABB=part.box
			asset(root,"rail_bar",Vector3(box.get_center().x,box.get_center().y,box.position.z),Vector3(box.size.z,box.size.y/.085,box.size.x/.095),-PI/2)
		else:
			var start:Vector3=part.from;var end:Vector3=part.to
			var direction:Vector3=(end-start).normalized()
			var depth:=direction.cross(Vector3.UP).normalized()
			var up:=depth.cross(direction).normalized()
			var rail:=asset(root,"rail_bar",start,Vector3(start.distance_to(end),1,1))
			rail.basis=Basis(direction,up,depth).scaled_local(Vector3(start.distance_to(end),1,1))
	for box:AABB in Barriers.get_boxes():
		world._collider(box.get_center(),box.size)

static func build_exterior(world: Node3D) -> void:
	var night:=Node3D.new();night.name="NightExterior";world.map_root.add_child(night)
	for definition:Array in [[Vector3(0,-.08,-17),0.0],[Vector3(0,-.08,17),PI],[Vector3(-20,-.08,0),PI/2],[Vector3(20,-.08,0),-PI/2]]:
		var landscape:=asset(night,"night_landscape",definition[0],Vector3.ONE,float(definition[1]))
		for mesh:MeshInstance3D in landscape.find_children("*","MeshInstance3D",true,false):
			for surface:int in range(mesh.mesh.get_surface_count()):
				var old:StandardMaterial3D=mesh.mesh.surface_get_material(surface)
				var mat:StandardMaterial3D=old.duplicate()
				mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
				mat.albedo_color=Color("8e7853") if "brass" in old.resource_name else Color("24384a") if "green" in old.resource_name else Color("1b2c40")
				mesh.set_surface_override_material(surface,mat)
	var ground:=StandardMaterial3D.new();ground.albedo_color=Color("162b31");ground.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
	world._box(night,Vector3(0,-.16,0),Vector3(64,.08,64),ground)

static func finish(world: Node3D) -> void:
	build_windows(world,window_specs(world.map_data))
	build_portals(world)
	build_stairs(world)
	build_exterior(world)
	for floor_y:float in [0.0,3.2]:
		world._room_wall_finish(AABB(Vector3(-1.9,floor_y,-10.75),Vector3(3.8,3.0 if floor_y==0.0 else 3.2,21.5)),Color("a9a191"),false)
		for z:float in [-7.8,0.0,7.8]:asset(world.map_root,"pendant",Vector3(0,floor_y+(2.99 if floor_y==0 else 3.19),z),Vector3.ONE*.85)

static func place_station_radio(world:Node3D,point:Vector3) -> void:
	var nearest:Node3D;var distance:=INF
	for furnishing:Node in world.map_root.get_children():
		if furnishing.get_meta("catalog_kind","")!="furniture":continue
		var kind:=str(furnishing.get_child(0).get_meta("authored_asset",""))
		if kind not in ["desk","table","nightstand"]:continue
		var box:AABB=furnishing.get_meta("catalog_box")
		if absf(box.position.y-point.y)>.1:continue
		var next:=Vector2(point.x,point.z).distance_to(Vector2(box.get_center().x,box.get_center().z))
		if next<distance:nearest=furnishing;distance=next
	if is_instance_valid(nearest):
		var box:AABB=nearest.get_meta("catalog_box")
		var toward:Vector3=nearest.to_local(point)
		var radio:=asset(nearest,"radio",Vector3(.30*signf(toward.x),box.size.y,0),Vector3.ONE*.65)
		radio.set_meta("station_label","EQUIPO")
