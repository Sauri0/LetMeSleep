extends SceneTree
## Focused 0.9.1 regression for selection clarity and keyboard camera control.
## Run only with the shared Godot/preferences reservation.
const UI = preload("res://scripts/ui.gd")
const Prefs = preload("res://scripts/preferences.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")

var ui: CanvasLayer
var checks := 0
var failures: Array[String] = []
var original := PackedByteArray()
var had_preferences := false
var finishing := false
var restored := false
var target_size := Vector2i(1280,720)
var screens_output := ""

func _initialize() -> void:
	call_deferred("_run")

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		printerr("UI091_FAIL " + label)

func _settle() -> void:
	await process_frame
	await process_frame

func _backup() -> void:
	had_preferences = FileAccess.file_exists(Prefs.FILE_PATH)
	if had_preferences:
		original = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)
	create_timer(45).timeout.connect(func() -> void:
		if finishing:
			return
		_check(false,"watchdog")
		_restore()
		quit(1)
	)

func _restore() -> void:
	if restored:
		return
	if had_preferences:
		var file := FileAccess.open(Prefs.FILE_PATH,FileAccess.WRITE)
		file.store_buffer(original)
		file.close()
	elif FileAccess.file_exists(Prefs.FILE_PATH):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
	restored = true

func _finalize() -> void:
	_restore()

func _key(code: int) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	await _settle()

func _preview_key(code: int,echo := false) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = echo
	ui._avatar_preview._gui_input(event)
	await _settle()

func _pressed_views() -> Array[String]:
	var result: Array[String] = []
	for key: String in ui._custom_view_buttons:
		if ui._custom_view_buttons[key].button_pressed:
			result.append(key)
	result.sort()
	return result

func _read_capture_args() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--screens-output="):
			screens_output = argument.trim_prefix("--screens-output=")
		elif argument.begins_with("--size="):
			var parts := argument.trim_prefix("--size=").split("x")
			if parts.size()==2:
				target_size = Vector2i(maxi(1280,int(parts[0])),maxi(720,int(parts[1])))

func _capture(name: String) -> void:
	if screens_output.is_empty() or DisplayServer.get_name()=="headless":
		return
	DirAccess.make_dir_recursive_absolute(screens_output)
	await _settle()
	await RenderingServer.frame_post_draw
	var path := screens_output.path_join("%s-%dx%d.png"%[name,target_size.x,target_size.y])
	_check(root.get_texture().get_image().save_png(path)==OK,"saved native screenshot "+path)

func _run() -> void:
	_backup()
	_read_capture_args()
	root.size = target_size
	root.content_scale_size = Vector2i(1280,720)
	ui = UI.new()
	root.add_child(ui)
	Prefs.cosmetics = Cosmetics.default_profile()
	ui._open_customization()
	await _settle()

	_check(ui._custom_view_buttons.side.text=="Perfil","camera names the side inspection Perfil")
	_check(_pressed_views().is_empty(),"category framing does not claim a preset angle")
	_check(ui._custom_zoom_buttons.in.tooltip_text=="Acercar" and ui._custom_zoom_buttons.out.tooltip_text=="Alejar","zoom buttons expose clear actions")

	ui._select_custom_category("eyes")
	await _settle()
	var saved_eye := int(Prefs.cosmetics.human.eyes)
	_check(root.gui_get_focus_owner()==ui._custom_option_buttons[saved_eye],"opening a category moves keyboard focus to its selected option")
	ui._custom_option_buttons[2].pressed.emit()
	await _settle()
	_check(Prefs.cosmetics.human.eyes==2,"option button keeps the existing cosmetic ID contract")
	_check(ui._category_buttons.eyes.icon==ui._custom_thumbnail("human","eyes",2),"category thumbnail reflects the selected ID")
	_check(ui._category_buttons.eyes.tooltip_text.ends_with("Entornados"),"category tooltip names the selected option")
	_check(ui._custom_caption.text.contains("Ojos: Entornados"),"saved caption includes category and option")

	ui._custom_view_buttons.front.pressed.emit()
	_check(ui._avatar_preview.view_key=="front" and _pressed_views()==["front"],"front view has one visible active state")
	await _capture("human-eyes-front")
	ui._custom_view_buttons.side.pressed.emit()
	_check(ui._avatar_preview.view_key=="side" and is_equal_approx(ui._avatar_preview.orbit_yaw,PI*0.5) and _pressed_views()==["side"],"profile view and active state agree")
	ui._avatar_preview.grab_focus()
	var previous_yaw: float = ui._avatar_preview.orbit_yaw
	await _key(KEY_RIGHT)
	_check(ui._avatar_preview.orbit_yaw<previous_yaw and ui._avatar_preview.view_key=="free" and _pressed_views().is_empty(),"focused preview receives real arrow input and switches to free view")
	previous_yaw = ui._avatar_preview.orbit_yaw
	await _preview_key(KEY_RIGHT,true)
	_check(ui._avatar_preview.orbit_yaw<previous_yaw,"held arrow repeat continues orbiting")
	var previous_zoom: float = ui._avatar_preview.zoom
	ui._custom_zoom_buttons.in.pressed.emit()
	_check(ui._avatar_preview.zoom<previous_zoom,"visible zoom-in button moves the camera")
	previous_zoom = ui._avatar_preview.zoom
	await _preview_key(KEY_MINUS)
	_check(ui._avatar_preview.zoom>previous_zoom,"keyboard minus zooms out")
	ui._custom_view_buttons.reset.pressed.emit()
	_check(ui._avatar_preview.view_key=="general" and is_equal_approx(ui._avatar_preview.zoom,1.0) and _pressed_views().is_empty(),"reset restores full framing without claiming a preset angle")

	ui._select_custom_category("brows")
	await _settle()
	var neutral := true
	for value: float in ui._avatar_preview.avatar.imported_skin.facial_values.values():
		if not is_zero_approx(value): neutral=false
	_check(neutral,"facial comparison remains neutral")
	ui._select_custom_role("mosquito")
	await _settle()
	_check(ui._category_buttons.eyes.tooltip_text.ends_with("Redondos"),"role switch refreshes mosquito selection text")
	_check(ui._category_buttons.hair.text=="Antenas" and ui._category_buttons.outfit.text=="Cuerpo","role-specific category labels remain recognizable")
	ui._select_custom_category("eyes")
	ui._custom_view_buttons.front.pressed.emit()
	await _capture("mosquito-eyes-front")

	await _key(KEY_ESCAPE)
	_check(ui._screen=="home","Escape returns one level from customization")
	finishing = true
	ui.queue_free()
	await _settle()
	_restore()
	_check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH),"original preferences are restored byte-for-byte")
	print("UI091_CUSTOMIZATION_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
