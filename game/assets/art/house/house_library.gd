extends RefCounted
## Authored GLB visuals only. World/map_catalog remain collision authority.
static var scenes: Dictionary = {}
static var bounds_cache: Dictionary = {}
static var tinted_materials: Dictionary = {}

static func asset_for(data: Dictionary) -> String:
	if data.has("asset_id"): return str(data.asset_id)
	var label := str(data.get("label",""))
	var style := str(data.get("style","cabinet"))
	var room := str(data.get("room_name",""))
	if room=="Lavadero":
		if label=="Lavado": return "washer"
		if label=="Canasto": return "hamper"
		return "wardrobe"
	if room=="Baño":
		if label=="Lavado": return "bath_vanity"
		if label=="Canasto": return "toilet"
		return "bath_shower"
	if room=="Dormitorio azul":
		if label=="Mesada": return "bed"
		return "wardrobe" if label=="Despensa" else "dresser"
	if room=="Despensa":
		if label=="Cama": return "pantry_crates"
		return "pantry_shelf" if label=="Ropero" else "table"
	if room=="Taller de costura":
		if label=="Mesa grande": return "sewing_table"
		if label=="Vajillero": return "bookcase"
	if room=="Sala de TV" and label=="Equipo": return "television"
	if room=="Sala de música" and label=="Equipo": return "piano"
	if room=="Sala de juegos" and label=="Consola": return "game_table"
	if room=="Comedor" and label=="Mesa grande": return "dining_set"
	if label=="Mesada": return "stove"
	if label=="Alacena": return "sink"
	if label=="Despensa" and AABB(data.box).get_center().z < -5.9: return "fridge"
	if label in ["Biblioteca","Estantes"]: return "bookcase"
	if label=="Ropero": return "wardrobe"
	if style in ["bed","table","desk"]: return style
	if style=="sofa": return "armchair" if maxf(data.box.size.x,data.box.size.z)<1.6 else "sofa"
	if label in ["Mesa de luz","Mesita"] or data.box.size.y<0.7: return "nightstand"
	return "dresser"

static func mesh_bounds(node: Node3D, parent_transform: Transform3D = Transform3D.IDENTITY) -> AABB:
	var transform := parent_transform * node.transform
	var result := AABB()
	if node is MeshInstance3D and node.mesh != null:
		result = transform * node.get_aabb()
	for child: Node in node.get_children():
		if child is Node3D:
			var next := mesh_bounds(child,transform)
			if next.has_surface(): result = result.merge(next) if result.has_surface() else next
	return result

static func instantiate_asset(asset: String) -> Node3D:
	if not scenes.has(asset):
		scenes[asset] = load("res://assets/art/house/%s.glb"%asset)
	var model: Node3D = scenes[asset].instantiate()
	if not bounds_cache.has(asset): bounds_cache[asset] = mesh_bounds(model)
	model.set_meta("authored_asset",asset)
	return model

static func build(root: Node3D, data: Dictionary, tint: Color) -> void:
	if data.has("asset_id"):
		_build_explicit(root,data,tint)
		return
	var size: Vector3 = data.box.size
	var at: Vector3 = data.box.get_center()
	var room_center: Vector3 = data.get("room_center",Vector3.ZERO)
	root.position = Vector3(at.x,data.box.position.y,at.z)
	# Front is -Z after explicit Blender Z-up to glTF Y-up export.
	if size.z>size.x*1.25:
		root.rotation.y = -PI/2.0 if room_center.x>at.x else PI/2.0
		size = Vector3(size.z,size.y,size.x)
	elif at.z<room_center.z:
		root.rotation.y = PI
	var asset := asset_for(data)
	var model := instantiate_asset(asset)
	var bounds: AABB = bounds_cache[asset]
	# Faucet/pot sit above the counter, as in the previous visual fixtures.
	var physical_height := 0.86 if asset in ["sink","stove"] else 0.78 if asset=="bath_vanity" else bounds.size.y
	model.scale = Vector3(size.x/bounds.size.x,size.y/physical_height,size.z/bounds.size.z)
	model.position = -Vector3(bounds.get_center().x,bounds.position.y,bounds.get_center().z)*model.scale
	root.add_child(model)
	if asset in ["sofa","armchair","bed","dresser","wardrobe","bookcase","sink","stove"]:
		_tint_cloth(model,tint)
	var room := str(data.get("room_name",""))
	if asset in ["nightstand","desk","table"]:
		var decor_asset := "tea_service" if room in ["Sala de estar","Sala de TV","Comedor"] else "bedside_set" if "Dormitorio" in room or "visitas" in room else "mug"
		var decoration := instantiate_asset(decor_asset)
		var decoration_bounds: AABB = bounds_cache[decor_asset]
		var factor := minf(1.0,minf(size.x,size.z)*.48/maxf(decoration_bounds.size.x,decoration_bounds.size.z))
		decoration.scale = Vector3.ONE*factor
		decoration.position = Vector3(0,size.y,0)
		root.add_child(decoration)

static func _build_explicit(root: Node3D, data: Dictionary, tint: Color) -> void:
	var asset := str(data.asset_id)
	if not asset.is_valid_identifier() or not ResourceLoader.exists("res://assets/art/house/%s.glb"%asset):
		push_error("Unknown explicit furniture asset_id: "+asset)
		return
	var box: AABB = data.box
	var at := box.get_center()
	root.position=Vector3(at.x,box.position.y,at.z)
	root.rotation.y=float(data.get("rotation_y",0.0))
	var model := instantiate_asset(asset)
	var bounds: AABB = bounds_cache[asset]
	var scale := float(data.get("visual_scale",1.0))
	if not is_finite(scale) or scale<=0.0:
		push_error("Furniture visual_scale must be positive and finite")
		model.free()
		return
	model.scale=Vector3.ONE*scale
	model.position=-Vector3(bounds.get_center().x,bounds.position.y,bounds.get_center().z)*scale
	root.add_child(model)
	root.set_meta("explicit_asset",asset)
	root.set_meta("surface_height",float(data.get("surface_height",box.size.y)))
	if asset in ["sofa","armchair","bed","dresser","wardrobe","bookcase","sink","stove"]: _tint_cloth(model,tint)
	# Generated pickup surfaces stay clear. Decorative props already authored
	# inside a GLB remain there; the legacy house keeps its old additions above.

static func _tint_cloth(node: Node, tint: Color) -> void:
	if node is MeshInstance3D:
		for surface: int in range(node.mesh.get_surface_count()):
			var material: Material = node.mesh.surface_get_material(surface)
			if material is StandardMaterial3D and ("blue" in material.resource_name or "sage" in material.resource_name):
				var key := material.resource_name+tint.to_html()
				if not tinted_materials.has(key):
					var variant: StandardMaterial3D = material.duplicate()
					variant.albedo_color = tint
					tinted_materials[key] = variant
				node.set_surface_override_material(surface,tinted_materials[key])
	for child: Node in node.get_children(): _tint_cloth(child,tint)
