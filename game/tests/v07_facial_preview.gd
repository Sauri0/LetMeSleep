extends SceneTree
## Exportable, isolated facial exercise. No profile writes, network or gameplay.
const Preview=preload("res://scripts/avatar_preview.gd")
const Cosmetics=preload("res://scripts/cosmetics.gd")
const Facial=preload("res://assets/art/characters/shared/facial_expression.gd")
const SamplePreview=preload("res://assets/art/samples07/sample_preview.gd")
var previews: Array[SubViewportContainer]=[]
var output_dir: String
var checks:=0
var failures:=0
var state_label: Label
var sample_variant := ""
var animate_blinks := false
var state_frames := 90
func _initialize()->void:
	_run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:
		failures+=1
		print("FACIAL07 FAIL ",label)
func _run()->void:
	root.size=Vector2i(1920,1080)
	output_dir=ProjectSettings.globalize_path("res://../outputs/0.7-polish/faciales")
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):output_dir=arg.trim_prefix("--output=")
		if arg.begins_with("--sample="):sample_variant=arg.trim_prefix("--sample=")
		if arg=="--animate-blinks":animate_blinks=true
		if arg.begins_with("--state-frames="):state_frames=clampi(int(arg.trim_prefix("--state-frames=")),30,180)
	DirAccess.make_dir_recursive_absolute(output_dir)
	var background:=ColorRect.new()
	background.color=Color("182b36")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(background)
	var margin:=MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side:String in ["left","right","top","bottom"]:margin.add_theme_constant_override("margin_"+side,18)
	root.add_child(margin)
	var stack:=VBoxContainer.new()
	stack.add_theme_constant_override("separation",10)
	margin.add_child(stack)
	var title:=Label.new()
	title.text="Prueba funcional sobre modelos actuales · dirección visual pendiente de elección"
	if not sample_variant.is_empty():title.text="Propuesta "+sample_variant+" · "+title.text
	else:title.text="Humano A + mosquito B · faciales integrados · Godot Compatibility"
	title.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size",27)
	stack.add_child(title)
	state_label=Label.new()
	state_label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	state_label.add_theme_font_size_override("font_size",23)
	stack.add_child(state_label)
	var grid:=GridContainer.new()
	grid.columns=3
	grid.size_flags_vertical=Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation",12)
	grid.add_theme_constant_override("v_separation",12)
	stack.add_child(grid)
	for species:String in ["human","mosquito"]:
		for face:int in range(3):
			var cell:=VBoxContainer.new()
			cell.size_flags_horizontal=Control.SIZE_EXPAND_FILL
			cell.size_flags_vertical=Control.SIZE_EXPAND_FILL
			grid.add_child(cell)
			var label:=Label.new()
			label.text=("Humano" if species=="human" else "Mosquito")+" · "+str(Cosmetics.option_names(species,"eyes")[face])
			label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
			label.add_theme_font_size_override("font_size",23)
			cell.add_child(label)
			var preview:SubViewportContainer=SamplePreview.new() if not sample_variant.is_empty() else Preview.new()
			preview.size_flags_horizontal=Control.SIZE_EXPAND_FILL
			preview.size_flags_vertical=Control.SIZE_EXPAND_FILL
			cell.add_child(preview)
			# The project uses a 720p logical canvas even when the window is 1080p.
			# Two editor-sized minimums would push the lower faces outside the frame.
			preview.custom_minimum_size=Vector2(220,230)
			if sample_variant.is_empty():
				preview.set_avatar(species,{"color":1,"accent":4,"face":face,"hair":0,"outfit":0,"accessory":0,"footwear":0})
				preview.focus_category("face")
				preview.focus_distance=1.05 if species=="human" else 1.75
				preview.set_view("front")
			else:
				preview.set_sample(sample_variant,species,face)
				preview.freeze_blinks=not animate_blinks
				preview.set_face_focus()
			preview.set_process(false)
			previews.append(preview)
			var visible_faces:=0
			var skin:Node3D=_skin(preview)
			for mesh:MeshInstance3D in skin.face_channels:
				if not mesh.visible:continue
				visible_faces+=1
				for channel:String in Facial.CHANNELS:
					check(skin.face_channels[mesh].has(channel),species+str(face)+" channel "+channel)
			check(visible_faces==(3 if sample_variant.is_empty() else 1),species+str(face)+" exactly one selection per facial category")
	for state:String in ["sleepy","alert","effort","impact"]:
		state_label.text={"sleepy":"Reposo somnoliento · mirada y parpadeo independientes","alert":"Alerta · ojos, cejas y boca","effort":"Esfuerzo · presión de labios y cejas","impact":"Reacción de impacto · transición breve en el juego"}[state]
		for preview:SubViewportContainer in previews:preview.set_expression(state)
		for frame:int in range(state_frames):
			for preview:SubViewportContainer in previews:
				if sample_variant.is_empty():preview._process(1.0/30.0)
				else:preview.step(1.0/30.0)
			await process_frame
			if frame==mini(65,state_frames-1) and DisplayServer.get_name()!="headless":
				await RenderingServer.frame_post_draw
				check(root.get_texture().get_image().save_png(output_dir.path_join("rostros-"+state+".png"))==OK,"capture "+state)
	for preview:SubViewportContainer in previews:
		check(float(_skin(preview).facial_values.MouthOpen)>.4,"impact opens selected mouth")
		check(float(_skin(preview).facial_values.BrowUp)>.35,"impact raises selected brows")
	margin.queue_free()
	background.queue_free()
	await process_frame
	await process_frame
	print("FACIAL07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)

func _skin(preview:SubViewportContainer)->Node3D:
	return preview.skin if not sample_variant.is_empty() else preview.avatar.imported_skin
