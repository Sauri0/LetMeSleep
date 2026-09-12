extends RefCounted
## Authored dressing aligned to the physical map and shared open balustrades.
const Library = preload("res://assets/art/house/house_library.gd")
const Doors = preload("res://scripts/door_catalog.gd")
const Barriers = preload("res://scripts/house_barriers.gd")
const Joinery = preload("res://scripts/frame_joinery.gd")
const ALFA_LIBRARY_PATH := "res://assets/art/house/alfa_library.gd"

static func window_specs(data: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if data.has("windows"):
		for index: int in range(Array(data.windows).size()):
			var source: Variant = Array(data.windows)[index]
			if not source is Dictionary or not source.get("p") is Vector3:
				continue
			var spec := (source as Dictionary).duplicate(true)
			spec["id"] = str(spec.get("id", "window-%02d" % index))
			spec["axis"] = int(spec.get("axis", 2))
			spec["rear"] = bool(spec.get("rear", false))
			spec["room"] = str(spec.get("room", "Exterior"))
			spec["tint"] = Color(spec.get("tint", Color("91a4ab")))
			result.append(spec)
		return result
	# Authored maps never fall through to the coordinate-specific legacy facade.
	if data.has("authored_version"):
		return result
	if data.has("generator_version"):
		for room: Dictionary in data.rooms:
			var b: AABB=room.bounds
			if absf(b.position.z+float(data.half_z)-.25)<.01 or absf(b.end.z-float(data.half_z)+.25)<.01:
				var rear:=b.get_center().z>0
				result.append({"p":Vector3(b.get_center().x,b.position.y+1.55,(float(data.half_z)-.125)*(1 if rear else -1)),"axis":2,"rear":rear,"room":str(room.name),"tint":room.color})
		for floor_y: float in data.floor_levels:
			result.append({"p":Vector3(0,floor_y+1.6,-float(data.half_z)+.125),"axis":2,"rear":false,"room":"Pasillo","tint":Color("91a4ab")})
			for side: float in [-1.0,1.0]:
				result.append({"p":Vector3(side*(float(data.half_x)-.125),floor_y+1.65,0),"axis":0,"rear":side>0,"room":"Escalera","tint":Color("7c9d9d")})
		return result
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

static func alfa_asset(parent: Node3D, name: String, at: Vector3, yaw: float = 0.0) -> Node3D:
	var library: Script = load(ALFA_LIBRARY_PATH) if ResourceLoader.exists(ALFA_LIBRARY_PATH) else null
	if library == null or not library.has_asset(name):
		return null
	var view: Node3D = library.instantiate_asset(name)
	if not is_instance_valid(view):
		return null
	parent.add_child(view)
	view.position = at
	view.rotation.y = yaw
	view.set_meta("catalog_kind", "authored_facade")
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

static func wet_room(room:Dictionary)->bool:
	return str(room.get("theme_id","")) in ["kitchen","laundry","bathroom"] or str(room.get("name","")) in ["Cocina","Lavadero","Baño"]

## Partition the actual slab instead of laying another almost-coplanar box on
## top. All regions keep exactly the catalog's top height and total footprint.
static func floor_finishes(bounds:AABB,rooms:Array)->Array[Dictionary]:
	var result:Array[Dictionary]=[]
	for cell:AABB in floor_pieces(bounds):result.append({"box":cell,"tile":false})
	for room:Dictionary in rooms:
		var room_box:AABB=room.bounds
		if not wet_room(room) or absf(room_box.position.y-bounds.end.y)>.001:continue
		var cut:=Rect2(Vector2(room_box.position.x,room_box.position.z),Vector2(room_box.size.x,room_box.size.z))
		var next:Array[Dictionary]=[]
		for piece:Dictionary in result:
			var box:AABB=piece.box
			var rect:=Rect2(Vector2(box.position.x,box.position.z),Vector2(box.size.x,box.size.z))
			var hit:=rect.intersection(cut)
			if not hit.has_area() or bool(piece.tile):next.append(piece);continue
			for remainder:Rect2 in Joinery.subtract_rect(rect,cut):
				next.append({"box":AABB(Vector3(remainder.position.x,box.position.y,remainder.position.y),Vector3(remainder.size.x,box.size.y,remainder.size.y)),"tile":false})
			var bath:bool=str(room.get("theme_id",""))=="bathroom" or str(room.get("name",""))=="Baño"
			next.append({"box":AABB(Vector3(hit.position.x,box.position.y,hit.position.y),Vector3(hit.size.x,box.size.y,hit.size.y)),"tile":true,"tint":Color("a5b4ad") if bath else Color("b5ac95")})
		result=next
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
	var levels:Array=world.map_data.floor_levels
	for floor_index:int in range(levels.size()):
		var floor_y:float=levels[floor_index]
		# The next slab is 20 cm thick with an 11 mm ceiling finish below it.
		# A fixed 6.4 m crown on the middle storey pierced the upper floor.
		var building:AABB=world._house_building_bounds()
		var ceiling:float=float(levels[floor_index+1])-.211 if floor_index+1<levels.size() else building.end.y
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
	var wet:=wet_room(room)
	world._room_wall_finish(bounds,color,wet)
	var room_root:=Node3D.new();room_root.name="RoomDetails_"+name.validate_node_name()
	room_root.set_meta("room_name",name);room_root.set_meta("floor",int(room.floor));world.map_root.add_child(room_root)
	var levels:Array=world.map_data.floor_levels
	var building:AABB=world._house_building_bounds()
	var ceiling_y:float=float(levels[int(room.floor)+1])-.21 if int(room.floor)+1<levels.size() else building.end.y-.01
	var ceiling_height:=ceiling_y-y
	_warm_diffuser(asset(room_root,"pendant",Vector3(center.x,y+ceiling_height,center.z)))
	# Wet-room tiles are material regions of the slab built by World.
	if not wet:
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
		if model_name=="bath_mirror" and not world.map_data.has("generator_version"):
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
		if world.map_data.has("authored_version"):
			# Alfa shutters use a back/base pivot and face -Z, matching the
			# exterior side of this window-local frame without mesh scaling.
			for side: float in [-1.0, 1.0]:
				var shutter := alfa_asset(root, "alfa_shutter", Vector3(side * .99, -.60, -.035))
				if is_instance_valid(shutter):
					shutter.set_meta("window_id", str(spec.get("id", "")))
		for side:float in [-1.0,1.0]:
			var curtain:=asset(root,"curtain",Vector3(side*.91,-.74,.18),Vector3(1.05,.95,1))
			Library._tint_cloth(curtain,Color(spec.tint).lightened(.15))
		world._segment(root,Vector3(-1.10,.88,.18),Vector3(1.10,.88,.18),.024,world.gold)
		# The cool environment already supplies the night fill. A second,
		# unshadowed source at every window leaked across nearby room walls and
		# raised the scene from23 to39 positional lights (Compatibility cap32).

static func build_portals(world: Node3D) -> void:
	var doors:Dictionary=Doors.get_doors(world.current_map)
	for structure:Dictionary in world.map_data.structures:
		if str(structure.get("label",""))!="Dintel":continue
		var box:AABB=structure.box
		var center:=box.get_center();var floor_y:=floorf(box.position.y/3.2)*3.2
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
	for part:Dictionary in Barriers.get_parts(world.current_map):
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
	for box:AABB in Barriers.get_boxes(world.current_map):
		world._collider(box.get_center(),box.size)

static func build_exterior(world: Node3D) -> void:
	# Authored outdoor geometry is driven by exterior metadata and the alfa
	# library adapter. Never cover that 40 m lot with the legacy backdrop here.
	if world.map_data.has("authored_version"):
		build_authored_exterior(world)
		return
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

static func _path_points(path: Dictionary) -> Array[Vector3]:
	var result: Array[Vector3] = []
	for point: Variant in path.get("points", []):
		if point is Vector3:
			result.append(point)
	if result.size() < 2 and path.get("from") is Vector3 and path.get("to") is Vector3:
		result = [Vector3(path.from), Vector3(path.to)]
	return result

static func _build_authored_area(world: Node3D, root: Node3D, area: Dictionary) -> bool:
	if not area.get("bounds") is AABB:
		return false
	var bounds: AABB = area.bounds
	if not bounds.has_volume():
		return false
	var kind := str(area.get("kind", "garden"))
	var tint := Color(area.get("color", Color("42634f") if kind == "garden" else Color("786f61")))
	# Area bounds describe the outdoor volume used by layout/navigation. Render
	# only its ground face; otherwise an 8.8 m semantic height becomes a solid.
	var surface_box := AABB(Vector3(bounds.position.x, bounds.position.y + .002, bounds.position.z),
		Vector3(bounds.size.x, .036, bounds.size.z))
	var mesh: MeshInstance3D = world._box(root, surface_box.get_center(), surface_box.size, world._surface_material(tint, kind))
	mesh.name = "ExteriorArea_" + str(area.get("id", kind)).validate_node_name()
	mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mesh.set_meta("catalog_kind", "exterior_area")
	mesh.set_meta("exterior_id", str(area.get("id", "")))
	mesh.set_meta("exterior_surface", kind)
	mesh.set_meta("source_bounds", bounds)
	return true

static func _build_authored_path(world: Node3D, root: Node3D, path: Dictionary) -> int:
	var points := _path_points(path)
	var width := float(path.get("width", 0.0))
	if points.size() < 2 or width <= 0.0:
		return 0
	var tint := Color(path.get("color", Color("918579")))
	var material: Material = world._surface_material(tint, str(path.get("surface", "stone")))
	var built := 0
	for index: int in range(points.size() - 1):
		var from := points[index]
		var to := points[index + 1]
		var delta := to - from
		var horizontal := Vector2(delta.x, delta.z)
		if horizontal.length() < 0.01:
			continue
		var segment := Node3D.new()
		segment.name = "ExteriorPath_%s_%02d" % [str(path.get("id", "path")).validate_node_name(), index]
		segment.position = (from + to) * 0.5 + Vector3.UP * 0.014
		segment.rotation.y = atan2(delta.x, delta.z)
		root.add_child(segment)
		var mesh: MeshInstance3D = world._box(segment, Vector3.ZERO, Vector3(width, .028, horizontal.length()), material)
		mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mesh.set_meta("catalog_kind", "exterior_path")
		mesh.set_meta("exterior_id", str(path.get("id", "")))
		mesh.set_meta("exterior_surface", str(path.get("surface", "stone")))
		built += 1
	return built

static func _build_authored_prop(root: Node3D, library: Script, prop: Dictionary) -> bool:
	if not prop.get("p") is Vector3 or not prop.get("scale", Vector3.ONE) is Vector3:
		return false
	var asset_id := str(prop.get("asset_id", ""))
	if asset_id.is_empty() or not library.has_asset(asset_id):
		return false
	var visual: Node3D = library.instantiate_asset(asset_id)
	if not is_instance_valid(visual):
		return false
	var placement := Node3D.new()
	placement.name = "ExteriorProp_" + str(prop.get("id", asset_id)).validate_node_name()
	placement.position = Vector3(prop.p)
	placement.rotation.y = float(prop.get("yaw", 0.0))
	placement.scale = Vector3(prop.get("scale", Vector3.ONE))
	placement.set_meta("catalog_kind", "exterior_prop")
	placement.set_meta("exterior_id", str(prop.get("id", "")))
	placement.set_meta("authored_asset", asset_id)
	root.add_child(placement)
	placement.add_child(visual)
	return true

static func _gable(world: Node3D, root: Node3D, points: PackedVector3Array, material: Material, id: String) -> void:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for point: Vector3 in points:
		tool.add_vertex(point)
	tool.generate_normals()
	var mesh := MeshInstance3D.new()
	mesh.name = "AuthoredGable_" + id
	mesh.mesh = tool.commit()
	mesh.material_override = material
	mesh.set_meta("catalog_kind", "authored_gable")
	root.add_child(mesh)

static func build_authored_architecture(world: Node3D, exterior_root: Node3D) -> Dictionary:
	var existing := exterior_root.get_node_or_null("AuthoredArchitecture")
	if existing != null:
		return Dictionary(existing.get_meta("build_report", {}))
	var root := Node3D.new()
	root.name = "AuthoredArchitecture"
	exterior_root.add_child(root)
	var building: AABB = world._house_building_bounds()
	var lot: AABB = world.map_data.get("bounds", building)
	var eave_y := building.end.y + .04
	var rise := minf(2.25, maxf(.8, lot.end.y - eave_y - .15))
	var ridge_y := eave_y + rise
	var half_run := building.size.x * .5 + .55
	var roof_depth := building.size.z + 1.1
	var slope_length := sqrt(half_run * half_run + rise * rise)
	var roof_angle := atan2(rise, half_run)
	var roof_material: Material = world._surface_material(Color("b9553e"), "tile")
	for side: float in [-1.0, 1.0]:
		var panel: MeshInstance3D = world._box(root,
			Vector3(building.get_center().x + side * half_run * .5, eave_y + rise * .5, building.get_center().z),
			Vector3(slope_length, .16, roof_depth), roof_material)
		panel.name = "AuthoredRoof_%s" % ("East" if side > 0.0 else "West")
		panel.rotation.z = -side * roof_angle
		panel.set_meta("catalog_kind", "authored_roof")
	var wall_material: Material = world._surface_material(Color("d7cbbd"), "wall")
	var x0 := building.position.x
	var x1 := building.end.x
	var ridge_x := building.get_center().x
	var front_z := building.position.z - .006
	var rear_z := building.end.z + .006
	_gable(world, root, PackedVector3Array([
		Vector3(x0, eave_y, front_z), Vector3(ridge_x, ridge_y, front_z), Vector3(x1, eave_y, front_z)]),
		wall_material, "Front")
	_gable(world, root, PackedVector3Array([
		Vector3(x0, eave_y, rear_z), Vector3(x1, eave_y, rear_z), Vector3(ridge_x, ridge_y, rear_z)]),
		wall_material, "Patio")

	# A tiled porch spans the centered front entrance. Six unscaled alfa canopy
	# modules make a 4.8 x 1.2 m cover; two measured posts hold its outer edge.
	var facade_x := building.get_center().x
	for z_offset: float in [.02, .62]:
		for x_offset: float in [-1.6, 0.0, 1.6]:
			alfa_asset(root, "alfa_canopy", Vector3(facade_x + x_offset, 2.62, building.position.z - z_offset))
	for x_offset: float in [-2.28, 2.28]:
		alfa_asset(root, "alfa_porch_post", Vector3(facade_x + x_offset, 0.0, building.position.z - 1.14))

	var chimney_x := building.position.x + building.size.x * .72
	var normalized_x := absf(chimney_x - ridge_x) / (building.size.x * .5)
	var chimney_y := eave_y + rise * (1.0 - normalized_x) - .10
	alfa_asset(root, "alfa_chimney", Vector3(chimney_x, chimney_y, building.get_center().z + 1.2))
	var report := {"roof_panels": 2, "gables": 2, "canopies": 6, "porch_posts": 2,
		"chimneys": 1, "visual_only": true, "ridge_y": ridge_y}
	root.set_meta("build_report", report)
	return report

static func build_authored_exterior(world: Node3D) -> Dictionary:
	var existing: Node = world.map_root.get_node_or_null("AuthoredExterior")
	if existing != null:
		return Dictionary(existing.get_meta("build_report", {}))
	var root := Node3D.new()
	root.name = "AuthoredExterior"
	world.map_root.add_child(root)
	var exterior: Dictionary = world.map_data.get("exterior", {})
	var errors: Array[String] = []
	var areas := 0
	for area: Variant in exterior.get("areas", []):
		if area is Dictionary and _build_authored_area(world, root, area):
			areas += 1
		else:
			errors.append("invalid exterior area")
	var path_segments := 0
	for path: Variant in exterior.get("paths", []):
		if not path is Dictionary:
			errors.append("invalid exterior path")
			continue
		var built := _build_authored_path(world, root, path)
		if built == 0:
			errors.append("invalid exterior path " + str(path.get("id", "")))
		path_segments += built
	var props := 0
	var library: Script = load(ALFA_LIBRARY_PATH) if ResourceLoader.exists(ALFA_LIBRARY_PATH) else null
	for prop: Variant in exterior.get("props", []):
		if library != null and prop is Dictionary and _build_authored_prop(root, library, prop):
			props += 1
		else:
			errors.append("invalid or unavailable exterior prop " + str(prop.get("id", "")) if prop is Dictionary else "invalid exterior prop")
	var report := {"areas": areas, "path_segments": path_segments, "props": props,
		"requested_areas": Array(exterior.get("areas", [])).size(),
		"requested_paths": Array(exterior.get("paths", [])).size(),
		"requested_props": Array(exterior.get("props", [])).size(), "errors": errors,
		"visual_only": true, "collision_source": "map obstacles"}
	root.set_meta("build_report", report)
	build_authored_architecture(world, root)
	return report

static func finish(world: Node3D) -> void:
	build_windows(world,window_specs(world.map_data))
	build_portals(world)
	build_stairs(world)
	build_exterior(world)
	if world.map_data.has("generator_version") or world.map_data.has("authored_version"):
		var levels:Array=world.map_data.floor_levels
		var building:AABB=world._house_building_bounds()
		for corridor: AABB in world.map_data.corridors:
			world._room_wall_finish(corridor,Color("a9a191"),false)
			if corridor.size.x>corridor.size.z:
				var floor_index:=levels.find(corridor.position.y)
				var ceiling_y:=float(levels[floor_index+1])-.21 if floor_index>=0 and floor_index+1<levels.size() else building.end.y-.01
				asset(world.map_root,"pendant",Vector3(corridor.get_center().x,ceiling_y,corridor.get_center().z),Vector3.ONE*.85)
		return
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
