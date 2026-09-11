extends SceneTree
## Native comparison of existing IDs, using the actual customization studio.
## --output=absolute_folder. Saves fixed neutral facial pages and garment poses.
const Preview = preload("res://scripts/avatar_preview.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const TILE := Vector2i(480,520)
var output := ""
var only := "all"
var cloth_source := ""
var cloth_candidate: Shader
var checks := 0
var failures: Array[String] = []
var records: Array[Dictionary] = []
var preview: Preview
var caption: Label

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output=argument.trim_prefix("--output=")
		if argument.begins_with("--only="): only=argument.trim_prefix("--only=")
		if argument.begins_with("--cloth-source="): cloth_source=argument.trim_prefix("--cloth-source=")
	_run.call_deferred()

func check(value: bool, label: String) -> void:
	checks+=1
	if not value:
		failures.append(label)
		if failures.size()<15: printerr("CHARACTER091_VIEW_FAIL "+label)

func settle() -> void:
	for frame: int in range(3):
		await process_frame
		await RenderingServer.frame_post_draw

func configure(role: String, appearance: Dictionary, category: String, pose: Dictionary={}) -> void:
	preview.set_avatar(role,appearance)
	preview.focus_category(category)
	preview.set_expression("neutral")
	preview.set_process(false)
	var data := {"p":Vector3(0,.88 if role=="mosquito" else 0.0,0),"yaw":PI,"body_yaw":PI,
		"state":"flying" if role=="mosquito" else "human","relaxed_pose":true,"pose_time":0.0,
		"appearance":appearance,"preview_only":true,"facial_preview":"neutral","facial_no_blink":true}
	data.merge(pose,true)
	preview.avatar.update_state(data,1.0)
	if role=="human" and cloth_candidate!=null:
		for material: Material in preview.avatar.imported_skin.material_cache.values():
			if material is ShaderMaterial: material.shader=cloth_candidate
	var neutral: Dictionary={}
	for channel: String in Facial.CHANNELS: neutral[channel]=0.0
	preview.avatar.imported_skin.apply_facial_values(neutral)
	var selected: Array[String]=[]
	for mesh: MeshInstance3D in preview.avatar.imported_skin.face_channels:
		if not mesh.visible: continue
		selected.append(str(mesh.name))
		for channel: String in Facial.CHANNELS:
			check(is_zero_approx(mesh.get_blend_shape_value(preview.avatar.imported_skin.face_channels[mesh][channel])),role+" stable neutral "+str(mesh.name)+"/"+channel)
	check(selected.size()==3,role+" exactly three independent facial selections")

func capture(sheet: Image, column: int, row: int, title: String) -> void:
	caption.text=title
	await settle()
	var content: Image=preview.viewport.get_texture().get_image()
	var background := content.get_pixel(0,0)
	var occupied := 0
	for y: int in range(8,content.get_height()-8,12):
		for x: int in range(8,content.get_width()-8,12):
			var pixel:=content.get_pixel(x,y)
			if Vector3(pixel.r-background.r,pixel.g-background.g,pixel.b-background.b).length()>.08: occupied+=1
	check(occupied>35,"nonempty actor render "+title)
	var rendered:=root.get_texture().get_image()
	rendered.convert(Image.FORMAT_RGBA8)
	check(rendered.get_size()==TILE,"fixed capture dimensions: "+str(rendered.get_size()))
	sheet.blit_rect(rendered,Rect2i(Vector2i.ZERO,TILE),Vector2i(column*TILE.x,row*TILE.y))
	records.append({"label":title,"occupied_samples":occupied,"camera_distance":preview.focus_distance,
		"camera_yaw":preview.orbit_yaw,"appearance":preview.appearance.duplicate(true)})

func page(role: String, category: String, views: Array[String]) -> void:
	var count:=Cosmetics.option_count(role,category)
	var sheet:=Image.create(TILE.x*count,TILE.y*views.size(),false,Image.FORMAT_RGBA8)
	for variant: int in range(count):
		var appearance:=Cosmetics.appearance_for({},role)
		appearance.color=1;appearance.accent=4;appearance[category]=variant
		configure(role,appearance,category)
		for row: int in range(views.size()):
			preview.set_view(views[row])
			await capture(sheet,variant,row,"%s · %s %d · %s\n%s"%[role,category,variant,views[row],Cosmetics.option_names(role,category)[variant]])
	check(sheet.save_png(output.path_join(role+"-"+category+".png"))==OK,"save "+role+category)

func garment_motion() -> void:
	var poses: Array[Dictionary]=[
		{"label":"rest","crouch_amount":0.0},
		{"label":"crouch / look left","crouch_amount":1.0,"yaw":PI-1.2,"pitch":-1.3},
		{"label":"stride / look right","crouch_amount":0.0,"motion_phase":PI*.5,"motion_speed":3.1,"motion_blend":1.0,"yaw":PI+1.2,"pitch":.8},
	]
	var sheet:=Image.create(TILE.x*3,TILE.y*6,false,Image.FORMAT_RGBA8)
	for outfit: int in range(3):
		var appearance: Dictionary=Cosmetics.default_profile().human
		appearance.outfit=outfit;appearance.footwear=outfit;appearance.color=1;appearance.accent=4
		for pose_index: int in range(poses.size()):
			configure("human",appearance,"",poses[pose_index])
			for view_index: int in range(2):
				preview.set_view("side" if view_index==0 else "back")
				await capture(sheet,outfit,pose_index*2+view_index,"human · outfit %d · %s\n%s"%[outfit,poses[pose_index].label,"profile" if view_index==0 else "back"])
	check(sheet.save_png(output.path_join("human-garment-motion.png"))==OK,"save garment motion")

func neckline() -> void:
	var sheet:=Image.create(TILE.x*3,TILE.y*3,false,Image.FORMAT_RGBA8)
	for outfit: int in range(3):
		var appearance: Dictionary=Cosmetics.default_profile().human
		appearance.outfit=outfit
		configure("human",appearance,"outfit")
		preview.focus_target=Vector3(0,1.32,0)
		preview.focus_distance=1.65
		for row: int in range(3):
			var view: String=["front","side","back"][row]
			preview.set_view(view)
			await capture(sheet,outfit,row,"Pijama %d · cuello y hombros\n%s"%[outfit,view])
	check(sheet.save_png(output.path_join("human-neckline.png"))==OK,"save neckline comparison")

func _run() -> void:
	if output.is_empty() or DisplayServer.get_name()=="headless":
		printerr("Provide --output=absolute_folder and a native renderer")
		quit(2);return
	DirAccess.make_dir_recursive_absolute(output)
	if not cloth_source.is_empty():
		cloth_candidate=Shader.new()
		cloth_candidate.code=FileAccess.get_file_as_string(cloth_source)
		check(not cloth_candidate.code.is_empty(),"candidate cloth shader source exists")
	root.size=TILE
	root.content_scale_size=TILE
	var background:=ColorRect.new();background.color=Color("182d38");background.size=TILE;root.add_child(background)
	preview=Preview.new();preview.position=Vector2.ZERO;preview.size=Vector2(TILE.x,TILE.y-58);root.add_child(preview)
	preview.set_process(false)
	caption=Label.new();caption.position=Vector2(12,TILE.y-54);caption.size=Vector2(TILE.x-24,52)
	caption.add_theme_font_size_override("font_size",17);root.add_child(caption)
	if only in ["all","faces"]:
		for role: String in ["human","mosquito"]:
			for category: String in ["eyes","brows","mouth"]: await page(role,category,["front","side"])
			for category: String in ["hair","accessory"]: await page(role,category,["front","side","back"])
	if only in ["all","garments"]:
		await neckline()
		await garment_motion()
	var hashes: Dictionary={}
	for role: String in ["human","mosquito"]:
		hashes[role]=FileAccess.get_sha256("res://assets/art/characters/%s/%s_lms06.glb"%[role,role])
	var file:=FileAccess.open(output.path_join("character091-views.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"records":records,"glb_sha256":hashes,"cloth_source":cloth_source,
		"visual_review":"pending","scope":"Neutral existing face IDs in front/profile, all hair/accessory IDs from three views, all outfits at three shared poses. Render success alone does not certify intersection-free geometry."},"\t"));file.close()
	print("CHARACTER091_VIEWS checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
