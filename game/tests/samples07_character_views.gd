extends SceneTree
const Preview=preload("res://assets/art/samples07/sample_preview.gd")
var previews:Array[SubViewportContainer]=[]
var variant:="A"
var output_dir:String
var title:Label
var role_labels:Array[Label]=[]
func _initialize()->void:_run.call_deferred()
func _capture(name:String)->void:
	for frame:int in range(8):
		for preview:SubViewportContainer in previews:preview.step(1.0/30.0)
		await process_frame
	if name.begins_with("facial-") and name!="facial-impact":
		for attempt:int in range(24):
			var blinking:=false
			for preview:SubViewportContainer in previews:blinking=blinking or float(preview.skin.facial_values.BlinkL)>.4
			if not blinking:break
			for preview:SubViewportContainer in previews:preview.step(1.0/30.0)
			await process_frame
	await RenderingServer.frame_post_draw
	var error:=root.get_texture().get_image().save_png(output_dir.path_join(name+".png"))
	assert(error==OK)
func _run()->void:
	root.size=Vector2i(1600,1000)
	for arg:String in OS.get_cmdline_user_args():
		if arg.begins_with("--variant="):variant=arg.trim_prefix("--variant=")
		if arg.begins_with("--output="):output_dir=arg.trim_prefix("--output=")
	if output_dir.is_empty():output_dir=ProjectSettings.globalize_path("res://../outputs/0.7-muestras/personajes/"+variant)
	DirAccess.make_dir_recursive_absolute(output_dir)
	var background:=ColorRect.new();background.color=Color("172b37");background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);root.add_child(background)
	var stack:=VBoxContainer.new();stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);stack.add_theme_constant_override("separation",10);root.add_child(stack)
	title=Label.new();title.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;title.add_theme_font_size_override("font_size",27);stack.add_child(title)
	var line:=Label.new();line.text="Estudio técnico · Godot Compatibility · propuesta sin seleccionar · misma paleta, luz y escala";line.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;line.add_theme_font_size_override("font_size",20);stack.add_child(line)
	var row:=HBoxContainer.new();row.size_flags_vertical=Control.SIZE_EXPAND_FILL;row.add_theme_constant_override("separation",10);stack.add_child(row)
	for role:String in ["human","mosquito"]:
		var cell:=VBoxContainer.new();cell.size_flags_horizontal=Control.SIZE_EXPAND_FILL;row.add_child(cell)
		var label:=Label.new();label.text="Humano · escala 1:1" if role=="human" else "Mosquito · ampliación ×4 · escala real en vista conjunta";label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;label.add_theme_font_size_override("font_size",21);cell.add_child(label)
		role_labels.append(label)
		var preview:=Preview.new();preview.size_flags_horizontal=Control.SIZE_EXPAND_FILL;preview.size_flags_vertical=Control.SIZE_EXPAND_FILL;cell.add_child(preview);preview.set_sample(variant,role,1 if role=="human" else 0);previews.append(preview)
	for view:String in ["front","side","back","three-quarter","exploded"]:
		title.text="Propuesta "+variant+" · "+("curva compacta" if variant=="A" else "alargada desgarbada")+" · "+view
		for preview:SubViewportContainer in previews:preview.set_exploded(view=="exploded");preview.set_view(view)
		await _capture(view)
	for preview:SubViewportContainer in previews:preview.set_exploded(false);preview.set_view("three-quarter")
	previews[0].set_real_scale_pair()
	title.text="Propuesta "+variant+" · escala relativa real a la izquierda · ampliación del mosquito a la derecha"
	await _capture("escala")
	for state:String in ["sleepy","alert","effort","impact"]:
		title.text="Prueba facial funcional · "+state+" · modelos staged, dirección visual sin elegir"
		role_labels[0].text="Humano · detalle por cámara · blink automático inhibido"
		role_labels[1].text="Mosquito · detalle por cámara + modelo ×4 · blink inhibido"
		for preview:SubViewportContainer in previews:preview.set_face_focus();preview.set_expression(state)
		for frame:int in range(40):
			for preview:SubViewportContainer in previews:preview.step(1.0/30.0)
			await process_frame
		await _capture("facial-"+state)
	stack.queue_free();background.queue_free()
	await process_frame
	await process_frame
	print("SAMPLES07_CHARACTER_VIEWS variant=",variant," output=",output_dir)
	quit()
