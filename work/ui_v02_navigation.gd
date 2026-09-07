extends SceneTree

const UI = preload("res://scripts/ui.gd")
const Prefs = preload("res://scripts/preferences.gd")
const Simulation = preload("res://scripts/simulation.gd")
var ui: CanvasLayer
var failures: Array[String] = []
var escape_count: int = 0
var preview_count: int = 0
var preview_close_count: int = 0
var original_settings: PackedByteArray
var settings_existed: bool = false

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, detail: String) -> void:
	if not value:
		failures.append(detail)
		printerr("UI_NAV_FAIL " + detail)
	else:
		print("UI_NAV_OK " + detail)

func key(code: int, shift: bool = false) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.shift_pressed = shift
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func run() -> void:
	settings_existed = FileAccess.file_exists(Prefs.FILE_PATH)
	if settings_existed:
		original_settings = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)
	root.size = Vector2i(1280, 720)
	ui = UI.new()
	root.add_child(ui)
	ui.escape_requested.connect(func() -> void:
		escape_count += 1
		if ui._screen == "lobby":
			ui.set_lobby_walking(not ui._lobby_walking)
		elif ui._screen == "game":
			ui.set_pause(not ui.is_menu_open())
	)
	ui.preview_requested.connect(func(_role: String, _appearance: Dictionary) -> void: preview_count += 1)
	ui.preview_closed.connect(func() -> void: preview_close_count += 1)
	await process_frame
	await process_frame
	check(root.gui_get_focus_owner() == ui._home_default_focus, "Home starts on the primary action")
	await key(KEY_TAB)
	check(root.gui_get_focus_owner() != ui._home_default_focus and ui._home.is_ancestor_of(root.gui_get_focus_owner()), "Tab moves within home")
	await key(KEY_TAB, true)
	check(root.gui_get_focus_owner() == ui._home_default_focus, "Shift Tab returns to primary action")
	await key(KEY_ENTER)
	check(ui._connection_open, "Enter opens create connection")
	check(root.gui_get_focus_owner() == ui._name_edit, "Connection focuses player name")
	ui._toggle_connection_options()
	ui._port_edit.get_line_edit().grab_focus()
	await key(KEY_ESCAPE)
	check(ui._connection_open and not ui._advanced_open, "Escape in SpinBox closes advanced options one level")
	ui._name_edit.grab_focus()
	await key(KEY_ESCAPE)
	check(not ui._connection_open and ui._home_menu.visible, "Escape in LineEdit returns to home")
	ui._open_connection(true)
	ui._name_edit.text = "UI navigation check"
	ui._address_edit.text = ""
	ui._request_connection(true)
	check(ui._advanced_open and ui._address_box.visible, "Missing hidden address reveals connection options")
	check(root.gui_get_focus_owner() == ui._address_edit, "Missing address is focused only when visible")
	ui._address_edit.text = "127.0.0.1"
	ui._close_connection()
	ui._open_customization()
	await process_frame
	check(preview_count > 0 and ui._screen == "customization", "Customization asks for 3D preview")
	ui._select_custom_role("mosquito")
	check(ui._custom_role == "mosquito", "Separate mosquito editor")
	await key(KEY_ESCAPE)
	check(ui._screen == "home" and preview_close_count == 1, "Escape closes customization and preview exactly once")
	ui._open_settings()
	await process_frame
	check(root.gui_get_focus_owner() == ui._settings_default_focus, "Settings focuses its visible return button")
	var modal_focus_ok: bool = true
	for index: int in range(24):
		await key(KEY_TAB)
		var owner: Control = root.gui_get_focus_owner()
		modal_focus_ok = modal_focus_ok and owner != null and ui._settings.is_ancestor_of(owner)
	check(modal_focus_ok, "24 Tabs stay within settings modal")
	ui._begin_binding("attack")
	var previous_binding: String = Prefs.binding_text("attack")
	await key(KEY_ESCAPE)
	check(ui._binding_action.is_empty() and ui._settings_open and Prefs.binding_text("attack") == previous_binding, "Escape cancels rebinding without closing settings or changing action")
	await key(KEY_ESCAPE)
	check(not ui._settings_open and ui._screen == "home", "Next Escape closes settings one level")
	var players: Dictionary = {1: {"name": "Ana", "role": "waiting", "ready": false}, 2: {"name": "Beto", "role": "waiting", "ready": true}}
	var config: Dictionary = Simulation.DEFAULT_CONFIG.duplicate(true)
	config.human_count = 5
	var lobby: Dictionary = {"owner": 1, "code": "SIESTA", "players": players, "config": config, "can_start": false, "start_reason": "Se configuraron 5 humanos y hacen falta mosquitos."}
	ui.show_lobby(lobby, 1)
	await process_frame
	check(int(ui._fields.human_count.value) == 5 and ui._start_button.disabled, "Owner target of five is retained with only two present")
	check(not ui.has_signal("role_requested"), "There is no player role-selection signal")
	ui.set_lobby_walking(true)
	check(not ui.is_menu_open() and root.gui_get_focus_owner() == null, "Walking releases UI focus and allows movement")
	ui.show_lobby(lobby, 1)
	check(ui._lobby_walking and not ui.is_menu_open(), "Repeated lobby snapshots keep walking state")
	await key(KEY_ESCAPE)
	check(not ui._lobby_walking and ui.is_menu_open(), "Escape from walking restores lobby menu")
	var previous_escapes: int = escape_count
	ui._open_settings()
	await key(KEY_ESCAPE)
	check(not ui._settings_open and not ui._lobby_walking and escape_count == previous_escapes, "Closing lobby settings does not also enter walking")
	config.mode = "sleep"
	ui.show_lobby(lobby, 2)
	check(ui._mode.disabled and ui._config_apply.disabled and not ui._fields.human_count.editable, "Guest cannot edit mode or human count")
	check(ui._field_rows.mosquito_lives.visible, "Sleep life options visible")
	config.mode = "blood"
	ui.show_lobby(lobby, 2)
	check(not ui._field_rows.mosquito_lives.visible, "Blood life options hidden")
	# Exercise the actual ConfigFile write/load path, then restore exact user bytes.
	Prefs.cosmetics = {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 2, "accessory": 1}}
	Prefs.save_settings()
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics == {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 2, "accessory": 1}}, "Real save plus fresh load preserves two independent appearances")
	if settings_existed:
		var restored := FileAccess.open(Prefs.FILE_PATH, FileAccess.WRITE)
		restored.store_buffer(original_settings)
		restored.close()
		check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original_settings, "Original user preferences restored byte for byte")
	else:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
		check(not FileAccess.file_exists(Prefs.FILE_PATH), "Originally absent user preferences remain absent")
	if failures.is_empty():
		print("UI_NAVIGATION_PASS: real InputEvents, focus scopes, Escape levels, random-role lobby, personal cosmetics persistence; original preferences restored.")
	else:
		printerr("UI_NAVIGATION_FAILED count=" + str(failures.size()))
	quit(0 if failures.is_empty() else 1)
