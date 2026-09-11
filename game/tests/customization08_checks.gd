extends SceneTree
## Real menu controls and disk persistence. Run only with the preferences/GPU
## reservation: every original byte is backed up before the UI can save it.
const UI = preload("res://scripts/ui.gd")
const Prefs = preload("res://scripts/preferences.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
var ui: CanvasLayer
var checks := 0
var failures: Array[String] = []
var emitted: Array[Dictionary] = []
var original := PackedByteArray()
var had_preferences := false
var backup_path := ""
var finishing := false

func _initialize() -> void:
	call_deferred("_run")

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		printerr("CUSTOMIZATION08_FAIL " + label)

func _backup() -> void:
	had_preferences = FileAccess.file_exists(Prefs.FILE_PATH)
	if had_preferences:
		original = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)
		backup_path = OS.get_user_data_dir().path_join("customization08-backup-%d.cfg" % Time.get_ticks_usec())
		var file := FileAccess.open(backup_path, FileAccess.WRITE)
		file.store_buffer(original)
		file.close()
	create_timer(45).timeout.connect(func() -> void:
		if not finishing:
			_check(false, "watchdog")
			_restore()
			quit(1)
	)

func _restore() -> void:
	if had_preferences:
		var file := FileAccess.open(Prefs.FILE_PATH, FileAccess.WRITE)
		file.store_buffer(original)
		file.close()
	elif FileAccess.file_exists(Prefs.FILE_PATH):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))

func _settle() -> void:
	await process_frame
	await process_frame

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

func _activate(button: Button) -> void:
	if ui._custom_category_scroll.is_ancestor_of(button):
		ui._custom_category_scroll.ensure_control_visible(button)
	button.grab_focus()
	await _key(KEY_ENTER)

func _choose(category: String, index: int) -> void:
	await _activate(ui._category_buttons[category])
	_check(ui._custom_category == category, "keyboard opens " + category)
	await _activate(ui._custom_option_buttons[index])
	_check(int(Prefs.cosmetics[ui._custom_role][category]) == index, "keyboard chooses %s:%d for %s" % [category,index,ui._custom_role])

func _mouse_button(position: Vector2, button: MouseButton, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.global_position = position
	event.button_index = button
	event.pressed = pressed
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed and button == MOUSE_BUTTON_LEFT else 0
	Input.parse_input_event(event)
	await process_frame

func _drag_preview(delta: Vector2) -> void:
	var center: Vector2 = ui._avatar_preview.get_global_rect().get_center()
	await _mouse_button(center, MOUSE_BUTTON_LEFT, true)
	var event := InputEventMouseMotion.new()
	event.position = center + delta
	event.global_position = event.position
	event.relative = delta
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(event)
	await process_frame
	await _mouse_button(center + delta, MOUSE_BUTTON_LEFT, false)
	await _settle()

func _zoom_preview() -> void:
	var center: Vector2 = ui._avatar_preview.get_global_rect().get_center()
	await _mouse_button(center, MOUSE_BUTTON_WHEEL_UP, true)
	await _mouse_button(center, MOUSE_BUTTON_WHEEL_UP, false)
	await _settle()

func _camera_controls(role: String) -> void:
	var preview: Control = ui._avatar_preview
	var yaw: float = preview.orbit_yaw
	var pitch: float = preview.orbit_pitch
	await _drag_preview(Vector2(32,11))
	_check(not is_equal_approx(yaw,preview.orbit_yaw) and not is_equal_approx(pitch,preview.orbit_pitch), role + " receives real mouse drag")
	_check(not preview.dragging, role + " releases drag")
	var zoom: float = preview.zoom
	await _zoom_preview()
	_check(preview.zoom < zoom, role + " receives real wheel zoom")
	for direction: String in ["front","side","back"]:
		await _activate(ui._custom_view_buttons[direction])
		var expected: float = 0 if direction=="front" else PI*0.5 if direction=="side" else PI
		_check(is_equal_approx(preview.orbit_yaw,expected), role + " " + direction + " view")
	await _activate(ui._custom_view_buttons.reset)
	_check(is_equal_approx(preview.zoom,1.0) and is_equal_approx(preview.orbit_yaw,-0.25), role + " reset restores full view")
	preview.focus_category("face")
	var target: Vector3 = preview.focus_target
	var distance: float = preview.focus_distance
	for part: String in ["eyes","brows","mouth"]:
		preview.focus_category(part)
		_check(preview.focus_target == target and is_equal_approx(preview.focus_distance,distance), role + " preserves legacy facial framing for " + part)
		await _settle()
		var neutral := true
		for value: float in preview.avatar.imported_skin.facial_values.values():
			if not is_zero_approx(value): neutral = false
		_check(neutral, role + " compares " + part + " without transient eyelid/gaze/expression offsets")

func _visible_categories(role: String) -> void:
	var actual: Array[String] = []
	for category: String in ui._category_buttons:
		if ui._category_buttons[category].is_visible_in_tree():
			actual.append(category)
	actual.sort()
	var expected: Array[String] = Cosmetics.category_keys(role)
	expected.sort()
	_check(actual == expected, role + " shows exactly its independent categories")
	_check(not ui._category_buttons.has("face"), role + " has no combined face selector")
	for category: String in expected:
		_check(is_instance_valid(ui._category_buttons[category].icon), role + " has a visual category icon for " + category)

func _tab_categories(role: String) -> void:
	ui._category_buttons.eyes.grab_focus()
	await _settle()
	for category: String in ["brows","mouth","mustache","beard","hair","hair_color","accessory","outfit","footwear","color","accent"]:
		if category not in Cosmetics.category_keys(role): continue
		await _key(KEY_TAB)
		_check(root.gui_get_focus_owner()==ui._category_buttons[category], role + " Tab reaches " + category + " without hidden categories")
	_check(ui._custom_category_scroll.get_global_rect().encloses(ui._category_buttons.accent.get_global_rect()), role + " Tab scrolls the final row into view")

func _capture(filename: String) -> void:
	if not OS.get_cmdline_user_args().has("--screens") or DisplayServer.get_name()=="headless": return
	await _settle()
	await RenderingServer.frame_post_draw
	var directory := ProjectSettings.globalize_path("res://../outputs/0.7-personalizacion")
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--screens-output="): directory = argument.trim_prefix("--screens-output=")
	DirAccess.make_dir_recursive_absolute(directory)
	_check(root.get_texture().get_image().save_png(directory.path_join(filename + ".png"))==OK,"saved native screenshot "+filename)

func _run() -> void:
	_backup()
	root.size = Vector2i(1280,720)
	root.content_scale_size = Vector2i(1280,720)
	ui = UI.new()
	root.add_child(ui)
	Prefs.cosmetics = Cosmetics.default_profile()
	ui.cosmetics_changed.connect(func(value: Dictionary) -> void: emitted.append(value.duplicate(true)))
	ui._open_customization()
	await _settle()
	_visible_categories("human")
	await _tab_categories("human")
	_check(ui.is_menu_open() and ui._avatar_preview.viewport.own_world_3d, "editor is an isolated menu world")
	var untouched: Dictionary = Prefs.cosmetics.duplicate(true)
	for selection: Array in [["eyes",2],["mouth",0],["brows",1],["mustache",2],["beard",1],["hair_color",4]]:
		await _choose(selection[0],selection[1])
	_check(Prefs.cosmetics.human.eyes==2 and Prefs.cosmetics.human.mouth==0 and Prefs.cosmetics.human.brows==1, "three human facial choices remain independent")
	_check(Prefs.cosmetics.human.mustache==2 and Prefs.cosmetics.human.beard==1 and Prefs.cosmetics.human.hair_color==4, "human facial hair and shared color coexist")
	for category: String in ["color","accent","hair","outfit","accessory","footwear"]:
		_check(Prefs.cosmetics.human[category]==untouched.human[category], "facial editing preserves human " + category)
	_check(Prefs.cosmetics.human.accessory==3 and Prefs.cosmetics.mosquito==untouched.mosquito, "nightcap and other species are preserved")
	await _capture("human-independent-parts")
	await _camera_controls("human")
	await _activate(ui._category_buttons.accent)
	await _settle()
	var visible_rect: Rect2 = ui._custom_category_scroll.get_global_rect()
	var final_row: Rect2 = ui._category_buttons.accent.get_global_rect()
	_check(visible_rect.encloses(final_row), "keyboard scroll reveals the final category at 720p")
	_check(ui._avatar_preview.size.x>=280 and ui._avatar_preview.size.y>=300, "scrolling categories preserve a large preview")
	for exclusive: String in ["mustache","beard","hair_color"]:
		await _activate(ui._category_buttons[exclusive])
		await _activate(ui._custom_role_buttons.mosquito)
		_check(ui._custom_category=="eyes" and ui._custom_option_buttons.size()==3, exclusive + " safely falls back to mosquito eyes")
		_visible_categories("mosquito")
		await _activate(ui._custom_role_buttons.human)
	await _activate(ui._custom_role_buttons.mosquito)
	await _tab_categories("mosquito")
	var human_saved: Dictionary = Prefs.cosmetics.human.duplicate(true)
	for selection: Array in [["eyes",2],["mouth",1],["brows",0]]:
		await _choose(selection[0],selection[1])
	_check(Prefs.cosmetics.mosquito.eyes==2 and Prefs.cosmetics.mosquito.mouth==1 and Prefs.cosmetics.mosquito.brows==0, "three mosquito facial choices remain independent")
	_check(Prefs.cosmetics.human==human_saved, "mosquito choices leave the human unchanged")
	await _capture("mosquito-independent-parts")
	await _camera_controls("mosquito")
	_check(not emitted.is_empty() and not emitted[-1].human.has("face") and not emitted[-1].mosquito.has("hair_color"), "UI emits the new role-specific payload")
	var expected: Dictionary = Prefs.cosmetics.duplicate(true)
	await _key(KEY_ESCAPE)
	_check(ui._screen=="home", "Escape returns one level to the main menu")
	var config := ConfigFile.new()
	_check(config.load(Prefs.FILE_PATH)==OK and config.get_value("appearance","cosmetics",{})==expected, "actual file contains all independent choices")
	ui.queue_free()
	await _settle()
	Prefs.cosmetics = {}
	Prefs._loaded = false
	ui = UI.new()
	root.add_child(ui)
	ui._open_customization()
	await _settle()
	_check(Prefs.cosmetics==expected, "fresh UI reloads the saved profile from disk")
	for role: String in ["human","mosquito"]:
		await _activate(ui._custom_role_buttons[role])
		_check(ui._avatar_preview.appearance==expected[role], role + " preview reopens with the saved independent parts")
	ui._select_custom_category("accent")
	await _key(KEY_ESCAPE)
	_check(ui._screen=="home", "immediate Escape safely closes before deferred category scrolling")
	await _finish()

func _finish() -> void:
	finishing = true
	if is_instance_valid(ui): ui.queue_free()
	await _settle()
	_restore()
	_check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH), "original preference bytes restored")
	_write_report()
	print("CUSTOMIZATION08_CHECKS %d/%d PASS" % [checks-failures.size(),checks])
	if not backup_path.is_empty(): print("PREFERENCES_BACKUP " + backup_path)
	quit(0 if failures.is_empty() else 1)

func _write_report() -> void:
	pass
