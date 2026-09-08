extends SceneTree
## Exhaustive, indexed head-composition sheets from the real imported avatar.
## Rendering is evidence for review, never an automatic visual PASS.
const Preview = preload("res://scripts/avatar_preview.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const TILE := Vector2i(480,560)
const PAGE_SIZE := 24
const VIEWS := ["front","quarter-left","profile-left","profile-right","quarter-right","back","above-quarter","below-quarter"]
const YAWS := [0.0,-PI/4.0,-PI/2.0,PI/2.0,PI/4.0,PI,-PI/4.0,PI/4.0]
const PITCHES := [-.035,-.035,-.035,-.035,-.035,-.035,-.60,.50]
const FRAME_MARGIN := 20.0
const BAKE_TOLERANCE := .00015
var role := "human"
var page := 0
var head_override := -1
var states: PackedStringArray = ["neutral"]
var folder := ""
var preview: SubViewportContainer
var caption: Label
var checks := 0
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("FACIAL_CATALOG08_FAIL "+label)

func _head_keys() -> Array[String]:
	var result: Array[String] = ["eyes","mouth","brows","hair","accessory"]
	if role=="human": result.append_array(["mustache","beard"])
	return result

func _appearance(ordinal: int) -> Dictionary:
	var data: Dictionary = Cosmetics.appearance_for({},role)
	# Clothing has its own geometry coverage. Fix it explicitly in these head views.
	data.outfit = 0
	data.footwear = 0
	data.color = 1
	data.accent = 4
	if role=="human": data.hair_color = 0
	for key: String in _head_keys():
		var count: int = Cosmetics.option_count(role,key)
		data[key] = ordinal%count
		ordinal = int(ordinal/count)
	return data

func _expression(state: String, data: Dictionary) -> Dictionary:
	var values: Dictionary = {}
	if state in ["neutral","blink-half","blink","brow-up","brow-down","brow-up-blink","brow-down-blink","mouth-open","mouth-press","mouth-open-smile"]:
		for channel: String in Facial.CHANNELS: values[channel] = 0.0
		var extreme_channel: String = {"brow-up":"BrowUp","brow-down":"BrowDown","mouth-open":"MouthOpen","mouth-press":"MouthPress"}.get(state,"")
		if not extreme_channel.is_empty(): values[extreme_channel] = 1.0
		if state=="mouth-open-smile": values.MouthOpen = 1.0; values.MouthSmile = 1.0
		if state in ["blink-half","blink"]:
			values.BlinkL = .5 if state=="blink-half" else 1.0
			values.BlinkR = values.BlinkL
		if state in ["brow-up-blink","brow-down-blink"]:
			values["BrowUp" if state=="brow-up-blink" else "BrowDown"] = 1.0
			values.BlinkL = 1.0
			values.BlinkR = 1.0
	else:
		var controller := Facial.new(0.0)
		for frame: int in range(90):
			values = controller.advance({"preview_only":true,"facial_no_blink":true,"facial_preview":state,"state":"human" if role=="human" else "flying"},role,1.0/30.0,int(data.eyes))
		if state=="smile": values.MouthSmile = 1.0; values.CheekLift = .20
	return values

func _actor_pixel_samples() -> int:
	var rendered: Image = preview.viewport.get_texture().get_image()
	if rendered.is_empty(): return 0
	var background: Color = rendered.get_pixel(0,0)
	var occupied := 0
	# Excludes captions and the studio pedestal. A blank first SubViewport frame
	# must fail even though the root screenshot itself contains labels/background.
	for row: int in range(24):
		for column: int in range(24):
			var x := int(rendered.get_width()*(.15+.70*float(column)/23.0))
			var y := int(rendered.get_height()*(.10+.70*float(row)/23.0))
			var pixel: Color = rendered.get_pixel(x,y)
			if Vector3(pixel.r-background.r,pixel.g-background.g,pixel.b-background.b).length()>.07:
				occupied += 1
	return occupied

func _head_points(skin: Node3D, id: String) -> Array[Dictionary]:
	# Godot's skeleton bake omits morphs. Match its base with the imported bind
	# palette, then add the separately skinned effective blend-shape delta.
	var result: Array[Dictionary] = []
	var skeleton: Skeleton3D = skin.skeleton
	for mesh: MeshInstance3D in skin.meshes:
		if not mesh.visible: continue
		var category: String = str(mesh.name).split("_")[1]
		if category not in ["head","core","eyes","mouth","brows","hair","accessory","mustache","beard","proboscis"]: continue
		if category=="core" and role!="mosquito": continue
		check(mesh.skin!=null,id+" imported head skin "+str(mesh.name))
		if mesh.skin==null: continue
		var palette: Array[Transform3D] = []
		var head_binds: Dictionary = {}
		var proboscis_binds: Dictionary = {}
		var relative: Transform3D = mesh.global_transform.affine_inverse()*skeleton.global_transform
		for bind: int in range(mesh.skin.get_bind_count()):
			var bone: int = skeleton.find_bone(mesh.skin.get_bind_name(bind))
			if bone<0: bone=mesh.skin.get_bind_bone(bind)
			check(bone>=0 and bone<skeleton.get_bone_count(),id+" valid head bind")
			if bone<0 or bone>=skeleton.get_bone_count(): return []
			palette.append(relative*skeleton.get_bone_global_pose(bone)*mesh.skin.get_bind_pose(bind))
			var bone_name := str(skeleton.get_bone_name(bone))
			if bone_name in ["head","proboscis"]: head_binds[bind]=true
			if bone_name=="proboscis": proboscis_binds[bind]=true
		var native: ArrayMesh = mesh.bake_mesh_from_current_skeleton_pose()
		var morphed: ArrayMesh = mesh.bake_mesh_from_current_blend_shape_mix() if mesh.get_blend_shape_count()>0 else null
		check(native!=null and (mesh.get_blend_shape_count()==0 or morphed!=null),id+" native head geometry bake")
		if native==null: continue
		var points := PackedVector3Array()
		var proboscis_vertices := 0
		var maximum := 0.0
		for surface: int in range(mesh.mesh.get_surface_count()):
			var arrays: Array = mesh.mesh.surface_get_arrays(surface)
			var original: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var baked: PackedVector3Array = native.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
			var morph: PackedVector3Array = morphed.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX] if morphed!=null else original
			var binds: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
			var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
			var influences: int = weights.size()/maxi(original.size(),1)
			check(influences in [4,8] and binds.size()==weights.size() and original.size()==baked.size() and original.size()==morph.size(),id+" head vertex order and weights")
			for vertex: int in range(original.size()):
				var base := Vector3.ZERO
				var delta := Vector3.ZERO
				var head_weight := 0.0
				var proboscis_weight := 0.0
				for influence: int in range(influences):
					var weight: float = weights[vertex*influences+influence]
					if weight<=0.0: continue
					var bind: int = binds[vertex*influences+influence]
					base+=palette[bind]*original[vertex]*weight
					delta+=palette[bind].basis*(morph[vertex]-original[vertex])*weight
					if head_binds.has(bind): head_weight+=weight
					if proboscis_binds.has(bind): proboscis_weight+=weight
				maximum=maxf(maximum,(mesh.global_transform*base).distance_to(mesh.global_transform*baked[vertex]))
				# Core also contains the body: include the head and the separately
				# articulated proboscis, while excluding thorax, wings and abdomen.
				if category!="core" or head_weight>=.5:
					points.append(mesh.global_transform*(baked[vertex]+delta))
					if category=="proboscis" or proboscis_weight>=.5: proboscis_vertices+=1
		check(maximum<=BAKE_TOLERANCE,id+" imported palette agrees with native head bake")
		check(not points.is_empty(),id+" visible head part has vertices "+str(mesh.name))
		if not points.is_empty(): result.append({"mesh":str(mesh.name),"category":category,"points":points,"proboscis_vertices":proboscis_vertices,"native_base_max_error_m":maximum})
	if role=="mosquito":
		var proboscis_total := 0
		for part: Dictionary in result: proboscis_total+=int(part.proboscis_vertices)
		check(proboscis_total>0,id+" separately bound or separate-mesh proboscis included")
	return result

func _fit_head(parts: Array[Dictionary]) -> void:
	if parts.is_empty(): return
	if role=="mosquito":
		var bounds := AABB(parts[0].points[0],Vector3.ZERO)
		for part: Dictionary in parts:
			for point: Vector3 in part.points: bounds=bounds.expand(point)
		preview.focus_target=bounds.get_center()
	var dimensions := Vector2(preview.viewport.size)
	var aspect: float = dimensions.x/dimensions.y
	var half_y: float = tan(deg_to_rad(preview.camera.fov)*.5)
	if preview.camera.keep_aspect==Camera3D.KEEP_WIDTH: half_y/=aspect
	var half_x: float = half_y*aspect
	half_x*=1.0-2.0*FRAME_MARGIN/dimensions.x
	half_y*=1.0-2.0*FRAME_MARGIN/dimensions.y
	var distance: float = 1.08 if role=="human" else .1
	for view_index: int in range(VIEWS.size()):
		preview.orbit_yaw=YAWS[view_index]
		preview.orbit_pitch=PITCHES[view_index]
		preview._update_camera()
		var inverse: Basis = preview.camera.global_basis.inverse()
		for part: Dictionary in parts:
			for point: Vector3 in part.points:
				var local: Vector3 = inverse*(point-preview.focus_target)
				distance=maxf(distance,local.z+maxf(absf(local.x)/half_x,absf(local.y)/half_y))
				distance=maxf(distance,local.z+preview.camera.near+.01)
	# One distance/target is retained across all eight angles of this appearance.
	preview.focus_distance=distance+.003
	preview._update_camera()

func _project_head(parts: Array[Dictionary], id: String, view: String) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var dimensions := Vector2(preview.viewport.size)
	for part: Dictionary in parts:
		var margin := INF
		var rect := Rect2(preview.camera.unproject_position(part.points[0]),Vector2.ZERO)
		var behind := false
		for point: Vector3 in part.points:
			behind = behind or preview.camera.is_position_behind(point)
			var pixel: Vector2 = preview.camera.unproject_position(point)
			rect=rect.expand(pixel)
			margin=minf(margin,minf(minf(pixel.x,pixel.y),minf(dimensions.x-pixel.x,dimensions.y-pixel.y)))
		check(not behind and margin>=FRAME_MARGIN-.1,id+" complete projected "+str(part.mesh)+" "+view+" margin="+str(margin))
		result.append({"mesh":part.mesh,"category":part.category,"vertices":part.points.size(),"proboscis_vertices":part.proboscis_vertices,"rect_pixels":[rect.position.x,rect.position.y,rect.size.x,rect.size.y],"min_margin_px":margin,"behind_camera":behind,"native_base_max_error_m":part.native_base_max_error_m})
	return result

func _run() -> void:
	folder = ProjectSettings.globalize_path("res://../outputs/0.7-combinaciones")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--role="): role = arg.trim_prefix("--role=")
		if arg.begins_with("--page="): page = int(arg.trim_prefix("--page="))
		if arg.begins_with("--head="): head_override = int(arg.trim_prefix("--head="))
		if arg.begins_with("--states="): states = arg.trim_prefix("--states=").split(",")
		if arg.begins_with("--output="): folder = arg.trim_prefix("--output=")
	check(role in ["human","mosquito"],"known species")
	check(DisplayServer.get_name()!="headless","real rendering backend")
	var head_count := 1
	for key: String in _head_keys(): head_count *= Cosmetics.option_count(role,key)
	check(head_count==(2916 if role=="human" else 243),"complete head domain")
	if head_override>=0:
		check(head_override<head_count,"specific head in domain")
		page = int(head_override/PAGE_SIZE)
	check(page>=0 and page*PAGE_SIZE<head_count,"page in range")
	if failures>0: quit(failures); return
	DirAccess.make_dir_recursive_absolute(folder)
	root.size = TILE
	root.content_scale_size = TILE
	var background := ColorRect.new()
	background.color = Color("182b36")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(background)
	preview = Preview.new()
	root.add_child(preview)
	preview.position = Vector2.ZERO
	preview.size = Vector2(TILE.x,TILE.y-64)
	preview.set_process(false)
	caption = Label.new()
	caption.position = Vector2(6,TILE.y-62)
	caption.size = Vector2(TILE.x-12,62)
	caption.add_theme_font_size_override("font_size",19)
	root.add_child(caption)
	var records: Array = []
	var captured_views: Array = []
	var sheets: Array = []
	var start := head_override if head_override>=0 else page*PAGE_SIZE
	var end := start+1 if head_override>=0 else mini(start+PAGE_SIZE,head_count)
	for state: String in states:
		check(state in ["neutral","blink-half","blink","sleepy","smile","alert","effort","impact","brow-up","brow-down","brow-up-blink","brow-down-blink","mouth-open","mouth-press","mouth-open-smile"],"explicit expression state")
		var images: Array[Image] = []
		for pair: int in range(int(VIEWS.size()/2)):
			var sheet := Image.create(TILE.x*(2 if head_override>=0 else 8),TILE.y*(1 if head_override>=0 else 6),false,Image.FORMAT_RGBA8)
			sheet.fill(Color("182b36"))
			images.append(sheet)
		for ordinal: int in range(start,end):
			var data := _appearance(ordinal)
			var id := "%s-H%04d"%["H" if role=="human" else "M",ordinal]
			preview.set_avatar(role,data)
			preview.set_process(false)
			preview.avatar.set_process(false)
			preview.avatar.clock_time = 0.0
			preview.avatar.update_state({"p":Vector3(0,.88 if role=="mosquito" else 0.0,0),"yaw":PI,"body_yaw":PI,"state":"flying" if role=="mosquito" else "human","relaxed_pose":true,"pose_time":0.0,"appearance":data,"preview_only":true,"facial_no_blink":true},0.0)
			preview.focus_category("eyes")
			if role=="human":
				preview.focus_target = Vector3(0,1.62,0)
				preview.focus_distance = 1.08
			preview.orbit_pitch = -.035
			var values := _expression(state,data)
			var skin: Node3D = preview.avatar.imported_skin
			var selected: Array[String] = []
			for mesh: MeshInstance3D in skin.face_channels:
				if not mesh.visible: continue
				selected.append(str(mesh.name))
				for channel: String in Facial.CHANNELS:
					check(skin.face_channels[mesh].has(channel),id+" channel "+channel)
			check(selected.size()==3,id+" three independent facial pieces")
			skin.apply_facial_values(values)
			for settle: int in range(2):
				await process_frame
				await RenderingServer.frame_post_draw
			var head_parts := _head_points(skin,id)
			check(head_parts.size()>=5,id+" complete head parts for framing")
			_fit_head(head_parts)
			var corrective_weights: Dictionary = {}
			for mesh: MeshInstance3D in skin.face_channels:
				if not mesh.visible: continue
				var weights: Dictionary = {}
				for channel: String in skin.face_channels[mesh]:
					if channel not in Facial.CHANNELS:
						weights[channel] = mesh.get_blend_shape_value(int(skin.face_channels[mesh][channel]))
						for control: String in ["BrowUp","BrowDown"]:
							for blink: String in ["BlinkL","BlinkR"]:
								if channel==control+blink:
									check(is_equal_approx(float(weights[channel]),float(values[control])*float(values[blink])),id+" applied corrective "+channel)
				corrective_weights[str(mesh.name)] = weights
			records.append({"id":id,"ordinal":ordinal,"appearance":data,"geometry_class_id":Cosmetics.geometry_class_id(role,data),"state":state,"channels":values,"corrective_weights":corrective_weights,"facial_application":"CharacterSkin.apply_facial_values","selected_facial_meshes":selected,"head_framing":{"target":[preview.focus_target.x,preview.focus_target.y,preview.focus_target.z],"distance":preview.focus_distance,"method":"native_skeleton_plus_skinned_morphs"}})
			for view_index: int in range(VIEWS.size()):
				preview.orbit_yaw = YAWS[view_index]
				preview.orbit_pitch = PITCHES[view_index]
				preview._update_camera()
				caption.text = "%s · %s · %s\nE%d M%d B%d H%d A%d%s"%[id,state,VIEWS[view_index],data.eyes,data.mouth,data.brows,data.hair,data.accessory," T%d D%d"%[data.mustache,data.beard] if role=="human" else ""]
				# The child viewport can be scheduled a frame after its parent when
				# first shown; complete both render cycles before reading its texture.
				for settle: int in range(2):
					await process_frame
					await RenderingServer.frame_post_draw
				var occupied := _actor_pixel_samples()
				check(occupied>=24,id+" visible actor pixels "+VIEWS[view_index])
				var projection := _project_head(head_parts,id,VIEWS[view_index])
				captured_views.append({"id":id,"state":state,"view":VIEWS[view_index],"occupied_actor_samples":occupied,"sample_count":576,"projected_head_parts":projection})
				var capture := root.get_texture().get_image()
				capture.convert(Image.FORMAT_RGBA8)
				check(capture.get_size()==TILE,id+" native tile dimensions")
				var slot := ordinal-start
				var dest := Vector2i((slot%4)*TILE.x*2+(view_index%2)*TILE.x,int(slot/4)*TILE.y)
				images[int(view_index/2)].blit_rect(capture,Rect2i(Vector2i.ZERO,TILE),dest)
		for pair: int in range(int(VIEWS.size()/2)):
			var file := "%s-page-%03d-%s-pair-%d.png"%[role,page,state,pair]
			check(images[pair].save_png(folder.path_join(file))==OK,"save sheet "+file)
			sheets.append({"file":file,"views":[VIEWS[pair*2],VIEWS[pair*2+1]],"state":state,"rows":1 if head_override>=0 else 6,"head_pairs_per_row":1 if head_override>=0 else 4,"first_head":start,"last_head":end-1})
	var hashes: Dictionary = {}
	for path: String in ["res://assets/art/characters/%s/%s_lms06.glb"%[role,role],"res://assets/art/characters/shared/character_skin.gd","res://scripts/cosmetics.gd","res://scripts/avatar_preview.gd"]:
		hashes[path] = FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "source_file_not_present_in_packed_export"
	var report: Dictionary = {"schema":4,"role":role,"page":page,"page_size":PAGE_SIZE,"head_keys":_head_keys(),"head_count":head_count,"captured_head_count":end-start,"states":states,"views":VIEWS,"camera_yaws":YAWS,"camera_pitches":PITCHES,"tile_pixels":[TILE.x,TILE.y],"records":records,"captured_views":captured_views,"actor_content_checked":true,"head_framing_checked":true,"framing_margin_px":FRAME_MARGIN,"useful_viewport_pixels":[TILE.x,TILE.y-64],"sheets":sheets,"source_sha256":hashes,"checks":checks,"failures":failures,"capture_passed":failures==0,"visual_review_status":"pending","scope":"Real imported head combinations; fixed clothing and color. Projected deformed head parts include antennae and proboscis with margin; wings and abdomen do not drive framing. Capture success does not certify fit or mark any sheet visually reviewed. Geometry/material equivalence is a separate gate."}
	var report_file := FileAccess.open(folder.path_join("%s-page-%03d.json"%[role,page]),FileAccess.WRITE)
	report_file.store_string(JSON.stringify(report,"\t")+"\n")
	preview.queue_free()
	caption.queue_free()
	background.queue_free()
	await process_frame
	await process_frame
	print("FACIAL_CATALOG08_RESULT checks=%d failures=%d role=%s page=%d heads=%d"%[checks,failures,role,page,end-start])
	quit(failures)
