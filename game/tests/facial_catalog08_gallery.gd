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
			records.append({"id":id,"ordinal":ordinal,"appearance":data,"geometry_class_id":Cosmetics.geometry_class_id(role,data),"state":state,"channels":values,"corrective_weights":corrective_weights,"facial_application":"CharacterSkin.apply_facial_values","selected_facial_meshes":selected})
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
				captured_views.append({"id":id,"state":state,"view":VIEWS[view_index],"occupied_actor_samples":occupied,"sample_count":576})
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
	var report: Dictionary = {"schema":3,"role":role,"page":page,"page_size":PAGE_SIZE,"head_keys":_head_keys(),"head_count":head_count,"captured_head_count":end-start,"states":states,"views":VIEWS,"camera_yaws":YAWS,"camera_pitches":PITCHES,"tile_pixels":[TILE.x,TILE.y],"records":records,"captured_views":captured_views,"actor_content_checked":true,"sheets":sheets,"source_sha256":hashes,"checks":checks,"failures":failures,"capture_passed":failures==0,"visual_review_status":"pending","scope":"Real imported head combinations; fixed clothing and color. Every tile must contain actor pixels, but capture success does not certify fit or mark any sheet visually reviewed. Geometry/material equivalence is a separate gate."}
	var report_file := FileAccess.open(folder.path_join("%s-page-%03d.json"%[role,page]),FileAccess.WRITE)
	report_file.store_string(JSON.stringify(report,"\t")+"\n")
	preview.queue_free()
	caption.queue_free()
	background.queue_free()
	await process_frame
	await process_frame
	print("FACIAL_CATALOG08_RESULT checks=%d failures=%d role=%s page=%d heads=%d"%[checks,failures,role,page,end-start])
	quit(failures)
