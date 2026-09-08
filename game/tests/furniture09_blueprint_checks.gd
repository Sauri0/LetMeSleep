extends SceneTree
## Measures generated furniture against the real imported GLBs. Display names
## are deliberately changed: only explicit asset IDs may select these models.
const Blueprint = preload("res://scripts/furniture_blueprint.gd")
const Library = preload("res://assets/art/house/house_library.gd")
var checks := 0
var failures := 0
var folder := ""
var cases: Array = []

func _initialize() -> void: _run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		print("FURNITURE09 FAIL "+label)

func world_faces(node: Node3D) -> PackedVector3Array:
	var result := PackedVector3Array()
	if node is MeshInstance3D and node.mesh!=null:
		for vertex: Vector3 in node.mesh.get_faces(): result.append(node.global_transform*vertex)
	for child: Node in node.get_children():
		if child is Node3D: result.append_array(world_faces(child))
	return result

func surface_ray(point: Vector3, faces: PackedVector3Array) -> float:
	var distance := INF
	for triangle: int in range(0,faces.size(),3):
		var hit: Variant = Geometry3D.ray_intersects_triangle(point,Vector3.DOWN,faces[triangle],faces[triangle+1],faces[triangle+2])
		if hit!=null: distance=minf(distance,point.distance_to(hit))
	return distance

func _run() -> void:
	root.size=Vector2i(1320,900)
	folder=ProjectSettings.globalize_path("res://../outputs/0.9-furniture-blueprint")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): folder=arg.trim_prefix("--output=")
	DirAccess.make_dir_recursive_absolute(folder)
	var world := Node3D.new()
	root.add_child(world)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode=Environment.BG_COLOR
	environment.background_color=Color("304b59")
	environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color=Color("c7d3df")
	environment.ambient_light_energy=.6
	environment_node.environment=environment
	world.add_child(environment_node)
	var light := DirectionalLight3D.new()
	light.rotation_degrees=Vector3(-55,155,0)
	light.light_energy=.7
	light.shadow_enabled=true
	world.add_child(light)
	var camera := Camera3D.new()
	camera.projection=Camera3D.PROJECTION_ORTHOGONAL
	camera.size=8.8
	camera.position=Vector3(7,8,-10)
	world.add_child(camera)
	camera.look_at(Vector3(0,.4,0))
	camera.make_current()
	var title := Label.new()
	title.position=Vector2(24,18)
	title.add_theme_font_size_override("font_size",24)
	root.add_child(title)
	var caption := Label.new()
	caption.position=Vector2(24,58)
	caption.add_theme_font_size_override("font_size",17)
	root.add_child(caption)
	var platform := BoxMesh.new()
	platform.size=Vector3(8,.1,7)
	var floor_mesh := MeshInstance3D.new()
	floor_mesh.mesh=platform
	floor_mesh.position.y=-.05
	var material := StandardMaterial3D.new()
	material.albedo_color=Color("78908d")
	floor_mesh.material_override=material
	world.add_child(floor_mesh)
	var beds := ["bedroom_blue","bedroom_rose","guest_room","bedroom_green"]
	for theme: String in Blueprint.THEME_IDS:
		var specs := Blueprint.for_theme(theme)
		check(specs.size()==4,"four functional objects "+theme)
		var asset_ids: Array[String] = []
		for item: Dictionary in specs: asset_ids.append(str(item.asset_id))
		if theme in beds: check("bed" in asset_ids,"real bed in "+theme)
		if theme=="bathroom": check("bath_vanity" in asset_ids and "toilet" in asset_ids and "bath_shower" in asset_ids,"bathroom has three distinct fixtures")
		if theme=="kitchen": check("sink" in asset_ids and "stove" in asset_ids and "fridge" in asset_ids,"kitchen functions are explicit")
		check(bool(specs[0].pickup_surface) and specs[0].asset_id in ["table","desk"],"first pickup surface is a clear real tabletop")
		var group := Node3D.new()
		world.add_child(group)
		var descriptions: Array[String] = []
		for index: int in range(specs.size()):
			var spec: Dictionary = specs[index]
			descriptions.append("%s (%s)"%[spec.label,spec.asset_id])
			check(Library.asset_for({"asset_id":spec.asset_id,"label":"Etiqueta cambiada","room_name":"Baño 999"})==spec.asset_id,"display names cannot change explicit model")
			for turn: int in range(4):
				var data := spec.duplicate(true)
				data.rotation_y=float(turn)*PI*.5
				var size: Vector3 = spec.size
				if turn%2==1: size=Vector3(size.z,size.y,size.x)
				data.box=AABB(Vector3(-size.x*.5,0,-size.z*.5),size)
				data.label="Canasto";data.room_name="Baño"
				var piece := Node3D.new()
				group.add_child(piece)
				Library.build(piece,data,Color("91b1b8"))
				check(piece.get_child_count()==1,"explicit route adds one model and no pickup-blocking decor")
				var model: Node3D = piece.get_child(0)
				check(str(model.get_meta("authored_asset"))==str(spec.asset_id),"GLB exact ID")
				check(model.scale.is_equal_approx(Vector3.ONE*float(spec.visual_scale)),"uniform scale only")
				var actual := Library.mesh_bounds(model,piece.transform)
				check(absf(actual.position.y)<.0002,"furniture bottom meets floor")
				check(absf(actual.size.x-size.x)<.0002 and absf(actual.size.z-size.z)<.0002,"footprint matches rotated physical box")
				check(absf(actual.size.y-Vector3(spec.visual_size).y)<.0002,"full visual height includes authored upper details")
				check(is_equal_approx(piece.rotation.y,float(data.rotation_y)),"orientation explicit, never inferred from aspect")
				if index==0:
					var faces := world_faces(piece)
					for x: float in [-.32,0,.32]:
						for z: float in [-.22,0,.22]:
							var probe := piece.to_global(Vector3(x,float(spec.surface_height)+.20,z))
							check(absf(surface_ray(probe,faces)-.20)<.0003,"pickup patch rests on actual clear tabletop")
				group.remove_child(piece)
				piece.free()
			var data := spec.duplicate(true)
			var at := Vector3(-1.85 if index%2==0 else 1.85,0,-1.65 if index<2 else 1.65)
			data.box=AABB(at-Vector3(spec.size)*Vector3(.5,0,.5),spec.size)
			data.rotation_y=0.0
			var shown := Node3D.new()
			group.add_child(shown)
			Library.build(shown,data,Color("91b1b8"))
		title.text="CATÁLOGO REAL · "+theme+" · escala uniforme"
		caption.text="\n".join(descriptions)
		if DisplayServer.get_name()!="headless":
			for frame: int in range(2):
				await process_frame
				await RenderingServer.frame_post_draw
			check(root.get_texture().get_image().save_png(folder.path_join(theme+".png"))==OK,"native comparison "+theme)
		cases.append({"theme":theme,"assets":asset_ids,"count":specs.size()})
		world.remove_child(group)
		group.free()
	check(Blueprint.for_theme("unknown").is_empty(),"unknown semantic theme is not silently reinterpreted")
	# Original authored fallback intentionally retains its historic mapping.
	check(Library.asset_for({"room_name":"Baño","label":"Canasto"})=="toilet","authored bathroom fallback unchanged")
	check(Library.asset_for({"room_name":"Dormitorio azul","label":"Mesada"})=="bed","authored blue bedroom fallback unchanged")
	check(Library.asset_for({"room_name":"Lavadero","label":"Lavado"})=="washer","authored laundry fallback unchanged")
	var file := FileAccess.open(folder.path_join("furniture09-blueprint.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"themes":cases,"scope":"catalog and actual GLB transforms; generator integration separate"},"\t"))
	print("FURNITURE09 %d/%d PASS"%[checks-failures,checks])
	quit(1 if failures else 0)
