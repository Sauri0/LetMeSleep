extends SceneTree
## Comparable, deterministic human-only facial inspection; no profile writes.
const Preview = preload("res://scripts/avatar_preview.gd")
const Facial = preload("res://assets/art/characters/shared/facial_expression.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
var previews: Array[SubViewportContainer] = []
var checks := 0
var failures := 0
var folder: String
var phase := "before"
var profile_only := false
var baseline_head := ""
var facial_hair := false

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("CHEEK07 FAIL ",label)

func _run() -> void:
	root.size = Vector2i(1920,1080)
	folder = ProjectSettings.globalize_path("res://../outputs/0.7-cachetes/before")
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): folder = arg.trim_prefix("--output=")
		if arg.begins_with("--phase="): phase = arg.trim_prefix("--phase=")
		if arg=="--profile-only": profile_only = true
		if arg.begins_with("--baseline-head="): baseline_head = arg.trim_prefix("--baseline-head=")
		if arg=="--facial-hair": facial_hair = true
	if profile_only: root.size = Vector2i(1920,800)
	DirAccess.make_dir_recursive_absolute(folder)
	var backdrop := ColorRect.new()
	backdrop.color = Color("182b36")
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(backdrop)
	var stack := VBoxContainer.new()
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stack.add_theme_constant_override("separation",8)
	root.add_child(stack)
	var title := Label.new()
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size",27)
	stack.add_child(title)
	var subtitle := Label.new()
	subtitle.text = "Humano A · 3 caras humanas · misma cámara/luz de estudio · sin parpadeo aleatorio"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	subtitle.add_theme_font_size_override("font_size",21)
	stack.add_child(subtitle)
	var grid := GridContainer.new()
	grid.columns = 3
	grid.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack.add_child(grid)
	var views: Array = ["profile"] if profile_only else ["front","three-quarter"]
	var old_head: Mesh
	if not baseline_head.is_empty():
		var document := GLTFDocument.new()
		var gltf := GLTFState.new()
		check(document.append_from_file(baseline_head,gltf)==OK,"read preserved pre-fix head")
		var imported := document.generate_scene(gltf)
		for node: Node in imported.find_children("human_head","MeshInstance3D",true,false): old_head = node.mesh
		check(old_head!=null,"find preserved head mesh")
		imported.free()
	for view: String in views:
		for face: int in range(3):
			var cell := VBoxContainer.new()
			cell.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			cell.size_flags_vertical = Control.SIZE_EXPAND_FILL
			grid.add_child(cell)
			var label := Label.new()
			label.text = ["Despierto","Soñoliento","Cejas firmes"][face]+" · "+("frente" if view=="front" else "perfil" if view=="profile" else "3/4")
			if facial_hair: label.text = ["Sin vello + anteojos","Bigote corto + perilla","Bigote caído + barba"][face]+" · "+("frente" if view=="front" else "perfil" if view=="profile" else "3/4")
			label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			label.add_theme_font_size_override("font_size",23)
			cell.add_child(label)
			var preview := Preview.new()
			preview.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			preview.size_flags_vertical = Control.SIZE_EXPAND_FILL
			cell.add_child(preview)
			preview.custom_minimum_size = Vector2(220,230)
			preview.set_avatar("human",{"color":1,"accent":4,"face":face,"hair":0,"outfit":0,"accessory":0,"footwear":0})
			if facial_hair:
				preview.set_avatar("human",{"color":1,"accent":4,"eyes":face,"brows":face,"mouth":face,"hair":face,"outfit":0,"accessory":2 if face==0 else 3 if face==2 else 0,"mustache":face,"beard":face,"hair_color":[3,1,4][face],"footwear":0})
			if old_head!=null:
				for mesh: MeshInstance3D in preview.avatar.imported_skin.meshes:
					if str(mesh.name)=="human_head": mesh.mesh = old_head
			preview.focus_category("face")
			preview.focus_target = Vector3(0,1.565,0)
			preview.focus_distance = 1.08
			preview.orbit_yaw = 0.0 if view=="front" else -PI/2 if view=="profile" else -.60
			preview.orbit_pitch = -.035
			preview._update_camera()
			preview.set_process(false)
			preview.avatar.set_process(false)
			for node: Node in preview.studio.get_children():
				if node is Light3D: node.queue_free()
				if node is WorldEnvironment:
					node.environment.ambient_light_energy = .40
			var light := DirectionalLight3D.new()
			light.rotation_degrees = Vector3(-32,-35,0)
			light.light_color = Color("ffe4c9")
			light.light_energy = .85
			light.shadow_enabled = true
			light.shadow_bias = .10
			light.shadow_normal_bias = .8
			light.directional_shadow_max_distance = 6.0
			preview.studio.add_child(light)
			previews.append(preview)
			check(preview.avatar.imported_skin.skeleton.get_bone_count()==36,"preserved human grip rig")
	var report: Dictionary = {"phase":phase,"faces":3,"views":views,"states":{}}
	for state: String in ["neutral","sleepy","smile","alert","effort","impact"]:
		title.text = ("ANTES" if phase=="before" else "DESPUÉS")+" · "+{"neutral":"Reposo neutro","sleepy":"Somnoliento","smile":"Sonrisa (control completo)","alert":"Alerta","effort":"Esfuerzo","impact":"Impacto"}[state]
		var state_values: Array = []
		for index: int in range(previews.size()):
			var preview: SubViewportContainer = previews[index]
			var skin: Node3D = preview.avatar.imported_skin
			var controller := Facial.new(0.0)
			var data: Dictionary = {"preview_only":true,"facial_no_blink":true,"facial_preview":"neutral" if state=="smile" else state,"state":"human"}
			var values: Dictionary
			for frame: int in range(90): values = controller.advance(data,"human",1.0/30.0,index%3)
			if state=="smile": values.MouthSmile = 1.0; values.CheekLift = .20
			var selected_count := 0
			for mesh: MeshInstance3D in skin.face_channels:
				if not mesh.visible: continue
				selected_count += 1
				for channel: String in Facial.CHANNELS:
					check(skin.face_channels[mesh].has(channel),"channel preserved "+channel)
					mesh.set_blend_shape_value(int(skin.face_channels[mesh][channel]),float(values[channel]))
			check(selected_count==3,"one selected eyes, brows and mouth set")
			state_values.append(values)
		report.states[state] = state_values
		for frame: int in range(5): await process_frame
		if DisplayServer.get_name()!="headless":
			await RenderingServer.frame_post_draw
			check(root.get_texture().get_image().save_png(folder.path_join(("profile-" if profile_only else "")+state+".png"))==OK,"capture "+state)
	var file := FileAccess.open(folder.path_join(("profile-" if profile_only else "")+"facial-controls.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	stack.queue_free()
	backdrop.queue_free()
	await process_frame
	await process_frame
	print("CHEEK07_RESULT checks=%d failures=%d"%[checks,failures])
	quit(failures)
