extends SceneTree

const UI = preload("res://scripts/ui.gd")
const Prefs = preload("res://scripts/preferences.gd")
const Simulation = preload("res://scripts/simulation.gd")
const Invitation = preload("res://scripts/invitation.gd")
const Cosmetics = preload("res://scripts/cosmetics.gd")
var ui: CanvasLayer
var failures: Array[String] = []
var escape_count: int = 0
var preview_count: int = 0
var preview_close_count: int = 0
var original_settings: PackedByteArray
var settings_existed: bool = false
var settings_backup: String = ""
var practice_args: Array = []
var practice_restarts: int = 0
var connection_args: Array = []
var applied_config: Dictionary = {}
var host_args: Array = []
var sound_events: Array[String] = []
var screen_events: Array[String] = []
var checks: int = 0

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, detail: String) -> void:
	checks += 1
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
		settings_backup = OS.get_user_data_dir().path_join("ui-navigation-backup-%d.cfg" % Time.get_ticks_usec())
		var backup := FileAccess.open(settings_backup,FileAccess.WRITE)
		backup.store_buffer(original_settings)
		backup.close()
	root.size = Vector2i(1280, 720)
	ui = UI.new()
	root.add_child(ui)
	ui.ui_sound_requested.connect(func(kind: String) -> void: sound_events.append(kind))
	ui.screen_changed.connect(func(screen: String) -> void: screen_events.append(screen))
	ui.escape_requested.connect(func() -> void:
		escape_count += 1
		if ui._screen == "lobby":
			ui.set_lobby_walking(not ui._lobby_walking)
		elif ui._screen == "game":
			ui.set_pause(not ui.is_menu_open())
	)
	ui.preview_requested.connect(func(_role: String, _appearance: Dictionary) -> void: preview_count += 1)
	ui.preview_closed.connect(func() -> void: preview_close_count += 1)
	ui.practice_requested.connect(func(role: String, mode: String) -> void: practice_args = [role, mode])
	ui.practice_restart_requested.connect(func() -> void: practice_restarts += 1)
	ui.config_requested.connect(func(value: Dictionary) -> void: applied_config = value)
	ui.host_requested.connect(func(player: String, port: int) -> void: host_args = [player,port])
	ui.connect_requested.connect(func(address: String, port: int, player: String, code: String, create: bool) -> void: connection_args = [address, port, player, code, create])
	await process_frame
	await process_frame
	check(root.gui_get_focus_owner() == ui._home_default_focus, "Home starts on the primary action")
	ui._home_default_focus.mouse_entered.emit()
	check(sound_events.is_empty(), "Initial focus and hover do not play interface sounds")
	check(not ui._practice_screen.visible and not ui._invite_settings.visible, "Home has no accidental practice screen or invitation modal")
	await key(KEY_TAB)
	check(root.gui_get_focus_owner() != ui._home_default_focus and ui._home.is_ancestor_of(root.gui_get_focus_owner()), "Tab moves within home")
	await key(KEY_TAB, true)
	check(root.gui_get_focus_owner() == ui._home_default_focus, "Shift Tab returns to primary action")
	await key(KEY_DOWN)
	check(root.gui_get_focus_owner() != ui._home_default_focus and ui._home.is_ancestor_of(root.gui_get_focus_owner()), "Arrow down navigates home actions")
	await key(KEY_UP)
	check(root.gui_get_focus_owner() == ui._home_default_focus, "Arrow up returns to primary action")
	await key(KEY_ENTER)
	check(ui._screen == "practice" and ui._practice_screen.visible, "Enter opens local practice setup")
	check(sound_events == ["confirm"] and screen_events[-1] == "practice", "Primary keyboard action emits one confirmation and the new screen")
	await key(KEY_ESCAPE)
	check(ui._screen == "home" and not ui._practice_screen.visible, "Escape returns from practice setup")
	ui._open_connection(true)
	await process_frame
	check(root.gui_get_focus_owner() == ui._name_edit, "Connection focuses player name")
	ui._toggle_connection_options()
	await _capture("ui06-host-advanced")
	check(ui._connect_submit.get_global_rect().end.y < root.size.y and ui._host_port_edit.is_visible_in_tree(), "Advanced host action remains visible inside a 720p window")
	ui._host_port_edit.get_line_edit().grab_focus()
	await key(KEY_ESCAPE)
	check(ui._connection_open and not ui._advanced_open, "Escape in SpinBox closes advanced options one level")
	ui._name_edit.grab_focus()
	await key(KEY_ESCAPE)
	check(not ui._connection_open and ui._home_menu.visible, "Escape in LineEdit returns to home")
	ui._open_connection(true)
	ui._name_edit.text = "UI navigation check"
	ui._address_edit.text = ""
	ui._request_connection(true)
	check(host_args == ["UI navigation check",Prefs.local_host_port], "One Create action starts your own server without reading a saved remote address")
	check(ui._connection_busy and ui._connect_submit.disabled and not ui._local_server_button.visible, "Host startup prevents duplicate clicks and has no separate server button")
	ui.show_connection_state({"phase":"failed","message":"No se pudo abrir UDP","can_retry":true})
	check(not ui._connection_busy and ui._connection_retry.visible and ui._connection_feedback.text == "No se pudo abrir UDP", "Failed host keeps the form and exposes an explicit retry")
	ui._address_edit.text = "127.0.0.1"
	ui._close_connection()
	ui._open_connection(false)
	check(ui._invitation_box.visible and not ui._address_box.visible and not ui._code_box.visible, "Join offers one invitation field and hides manual address/code")
	ui._invitation_edit.text = Invitation.PREFIX + "not-valid"
	var sounds_before_error: int = sound_events.size()
	ui._connect_submit.pressed.emit()
	check(connection_args.is_empty(), "Malformed invitation cannot request a connection")
	check(sound_events.size() == sounds_before_error+1 and sound_events[-1] == "error", "An invalid button action produces one error cue without a second selection sound")
	ui._invitation_edit.text = Invitation.encode("192.168.1.25", 27840, "SIESTA")
	Prefs.shared_address = "192.168.1.99"
	ui._request_connection(false)
	check(connection_args == ["192.168.1.25", 27840, "UI navigation check", "SIESTA", false], "Valid invitation emits decoded address, port and room together")
	check(Prefs.shared_address == "192.168.1.99" and ui._room_join_address == "192.168.1.25", "Joining preserves your own hosting address and keeps the room endpoint separately")
	ui._close_connection()
	ui._open_customization()
	await process_frame
	check(preview_count == 0 and ui._screen == "customization" and ui._avatar_preview.viewport.own_world_3d, "Customization embeds its own 3D world without requesting lobby preview")
	var human_before: Dictionary = Prefs.cosmetics.human.duplicate(true)
	ui._select_custom_role("mosquito")
	check(ui._custom_role == "mosquito", "Separate mosquito editor")
	ui._select_custom_category("hair")
	ui._select_custom_option(2)
	check(int(Prefs.cosmetics.mosquito.hair) == 2 and ui._custom_option_buttons.size() == 3, "Mosquito category editor changes its own antenna style")
	ui._select_custom_role("human")
	check(Prefs.cosmetics.human == human_before and int(Prefs.cosmetics.mosquito.hair) == 2, "Editing mosquito antennas leaves the entire human appearance unchanged")
	for category: String in Cosmetics.category_keys("human"):
		ui._select_custom_category(category)
		check(ui._custom_option_buttons.size() == Cosmetics.option_count("human",category), "Editor presents all options for human category " + category)
		var all_visual := true
		for option: Button in ui._custom_option_buttons:
			all_visual = all_visual and option.icon != null and option.icon.get_width() > 0 and not option.tooltip_text.is_empty()
		check(all_visual, "Human category uses real SVG thumbnails and named controls: " + category)
	ui._select_custom_category("footwear")
	await process_frame
	await process_frame
	ui._custom_option_buttons[2].grab_focus()
	await key(KEY_ENTER)
	await process_frame
	check(Prefs.cosmetics.human.footwear == 2 and ui._custom_option_buttons[2].button_pressed and ui._custom_option_buttons[2].text.begins_with("✓"), "Enter selects independent slippers with a non-color selection mark")
	check(root.gui_get_focus_owner() == ui._custom_option_buttons[2], "Rebuilding visual options restores keyboard focus to the selected card")
	ui._select_custom_category("accessory")
	ui._select_custom_option(3)
	check(Prefs.cosmetics.human.accessory == 3 and Prefs.cosmetics.human.footwear == 2, "Nightcap and slippers are independent options")
	await _capture("ui06-human-nightcap")
	ui._select_custom_category("color")
	await process_frame
	await process_frame
	check(ui._custom_option_buttons[-1].get_global_rect().end.y < root.size.y and ui._custom_option_buttons[-1].get_global_rect().end.x < root.size.x, "Six visual swatches fit at 720p without overflowing the editor")
	ui._select_custom_category("outfit")
	ui._select_custom_option(0)
	await _capture("ui06-human-outfit-0")
	ui._select_custom_option(1)
	await _capture("ui06-human-outfit-1")
	ui._select_custom_category("eyes")
	await _capture("ui06-customization")
	check(ui._avatar_preview.size.x >= 280 and ui._avatar_preview.size.y >= 300 and ui._avatar_preview.get_global_rect().end.y < root.size.y, "Isolated avatar studio fits the menu at 720p")
	ui._select_custom_role("mosquito")
	ui._select_custom_category("footwear")
	ui._select_custom_option(1)
	check(Prefs.cosmetics.mosquito.footwear == 1 and Prefs.cosmetics.human.footwear == 2, "Mosquito leg customization preserves independent human slippers")
	ui._select_custom_category("outfit")
	ui._select_custom_option(0)
	await _capture("ui06-mosquito-outfit-0")
	ui._select_custom_option(1)
	await _capture("ui06-mosquito-outfit-1")
	for view: String in ["front","side","back","reset"]:
		check(ui._custom_view_buttons.has(view) and ui._custom_view_buttons[view].is_visible_in_tree(), "Editor exposes named camera control " + view)
		ui._custom_view_buttons[view].pressed.emit()
	await _capture("ui06-customization-mosquito")
	await key(KEY_ESCAPE)
	check(ui._screen == "home" and preview_close_count == 1, "Escape closes customization and preview exactly once")
	ui._open_settings()
	await process_frame
	check(root.gui_get_focus_owner() == ui._settings_default_focus, "Settings focuses its visible return button")
	check(screen_events[-1] == "settings", "Settings exposes a separate music context without replacing the underlying screen")
	for volume_key: String in ["volume","music_volume","effects_volume","ambience_volume","ui_volume"]:
		check(ui._settings_controls.has(volume_key), "Settings exposes independent volume slider " + volume_key)
	var master_before := Prefs.master_volume
	ui._settings_controls.music_volume.value = 17.0
	ui._settings_controls.effects_volume.value = 36.0
	ui._settings_controls.ambience_volume.value = 52.0
	ui._settings_controls.ui_volume.value = 0.0
	check(is_equal_approx(Prefs.music_volume,0.17) and is_equal_approx(Prefs.effects_volume,0.36) and is_equal_approx(Prefs.ambience_volume,0.52) and is_zero_approx(Prefs.ui_volume), "Four audio sliders update their own preference")
	check(is_equal_approx(Prefs.master_volume,master_before), "Changing audio categories does not reset the master volume")
	await _capture("ui06-audio-settings")
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
	check(screen_events[-1] == "home", "Closing settings restores its underlying music context")
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
	Prefs.shared_address = ""
	ui._copy_invitation()
	await process_frame
	check(ui._invite_settings_open and root.gui_get_focus_owner() == ui._invite_address_edit, "Copy without shared endpoint opens and focuses explicit invitation setup")
	var invite_focus_ok := true
	for index: int in range(12):
		await key(KEY_TAB)
		var owner: Control = root.gui_get_focus_owner()
		invite_focus_ok = invite_focus_ok and owner != null and ui._invite_settings.is_ancestor_of(owner)
	check(invite_focus_ok, "Tab cannot escape invitation modal")
	ui._invite_address_edit.text = "127.0.0.1"
	ui._save_invite_settings()
	check(ui._invite_settings_open and Prefs.shared_address.is_empty(), "Loopback cannot be saved as a friend endpoint")
	ui._invite_scope.select(1)
	ui._update_invite_scope()
	ui._invite_address_edit.text = "192.168.1.25"
	ui._save_invite_settings()
	check(ui._invite_settings_open and Prefs.shared_address.is_empty(), "Other-house sharing refuses a private LAN address instead of issuing a misleading invitation")
	await key(KEY_ESCAPE)
	check(not ui._invite_settings_open and not ui._lobby_walking and escape_count == previous_escapes, "Escape closes invitation modal without entering lobby walking")
	config.mode = "sleep"
	ui.show_lobby(lobby, 2)
	check(ui._mode.disabled and ui._config_apply.disabled and not ui._fields.human_count.editable, "Guest cannot edit mode or human count")
	check(not ui._field_rows.has("mosquito_lives") and not ui._field_rows.has("respawn_seconds"), "Tasks has no obsolete lives or respawn configuration")
	config.mode = "blood"
	ui.show_lobby(lobby, 2)
	check(not "elimínenlos" in ui._mode_description.text and "aturden" in ui._mode_description.text, "Blood rules explain stunning instead of elimination")
	_task_config_limits(lobby)
	ui.show_home()
	ui._open_practice()
	ui._select_practice_role("mosquito")
	ui._select_practice_mode("sleep")
	ui._practice_start_button.pressed.emit()
	check(practice_args == ["mosquito", "sleep"] and ui._practice, "Practice starts immediately with selected role and mode")
	var snapshot := {"config": {"mode": "sleep"}, "actors": {1: {"role": "mosquito", "alive": true, "state":"stunned"}}, "winner": "human"}
	ui.show_game(snapshot, {"stun":{"active":true,"remaining":35.0,"total":35.0,"helped":false}}, 1)
	check(ui._practice and ui._practice_banner.visible and "Aturdido" in ui._hud_state.text and "35" in ui._hud_state.text, "Practice state and authoritative stun time survive game transition")
	ui.show_results(snapshot)
	check(not ui._rematch_button.disabled and ui._rematch_button.text == "REPETIR PRÁCTICA" and ui._result_leave.text == "VOLVER AL MENÚ", "Practice results offer restart and return home without social ready state")
	ui._rematch_button.pressed.emit()
	check(practice_restarts == 1, "Practice repeat emits separate restart signal")
	ui.show_home()
	check(not ui._practice, "Return home clears practice state")
	check(Prefs.DEFAULT_KEYS.sprint == KEY_SHIFT and Prefs.DEFAULT_KEYS.jump == KEY_SPACE and Prefs.DEFAULT_KEYS.crouch == KEY_CTRL and Prefs.DEFAULT_KEYS.ascend == KEY_SPACE and Prefs.DEFAULT_KEYS.descend == KEY_CTRL, "Human locomotion defaults share Space/Ctrl only across roles")
	await _hud_semantics()
	await _capture_editor_samples()
	# Exercise the actual ConfigFile write/load path, then restore exact user bytes.
	Prefs.cosmetics = {"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 2, "accessory": 1}}
	Prefs.shared_address = "192.168.1.25"
	Prefs.shared_port = 29840
	Prefs.save_settings()
	Prefs.cosmetics = {}
	Prefs.shared_address = ""
	Prefs.shared_port = 1024
	Prefs._loaded = false
	Prefs.load_settings()
	check(Prefs.cosmetics == Cosmetics.sanitize({"human": {"color": 5, "accessory": 2}, "mosquito": {"color": 2, "accessory": 1}}), "Real save plus fresh load preserves appearances with expanded defaults")
	check(Prefs.shared_address == "192.168.1.25" and Prefs.shared_port == 29840, "Real save plus fresh load preserves explicit friend endpoint independently")
	check(Prefs.binding_text("bite") == "H" and Prefs.binding_text("self_swat") == "T", "Reload keeps reassigned concentration and defense controls")
	check(Prefs.binding_text("toggle_help") == "F2", "Reassigned help shortcut survives real save and reload")
	if settings_existed:
		var restored := FileAccess.open(Prefs.FILE_PATH, FileAccess.WRITE)
		restored.store_buffer(original_settings)
		restored.close()
		check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original_settings, "Original user preferences restored byte for byte")
		if FileAccess.get_file_as_bytes(Prefs.FILE_PATH) == original_settings:
			DirAccess.remove_absolute(settings_backup)
	else:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Prefs.FILE_PATH))
		check(not FileAccess.file_exists(Prefs.FILE_PATH), "Originally absent user preferences remain absent")
	if failures.is_empty():
		print("UI_NAVIGATION_PASS checks=%d: real InputEvents, focus scopes, Escape levels, HUD states, bindings and persistence; original preferences restored." % checks)
	else:
		printerr("UI_NAVIGATION_FAILED count=" + str(failures.size()))
	quit(0 if failures.is_empty() else 1)


func _task_config_limits(lobby: Dictionary) -> void:
	lobby.config = Simulation.DEFAULT_CONFIG.duplicate(true)
	lobby.config.mode = "sleep"
	ui.show_lobby(lobby, 1)
	check(is_equal_approx(ui._fields.task_floor.value, 24.0) and is_equal_approx(ui._fields.task_floor.min_value, 24.0) and is_equal_approx(ui._fields.task_interval.min_value, 24.5), "Task defaults visibly reserve 21 seconds for travel beyond three seconds of work")
	ui._fields.task_work.value = 8.0
	ui._fields.task_interval.value = 15.0
	check(is_equal_approx(ui._fields.task_interval.value, 29.5) and is_equal_approx(ui._fields.task_deadline.value, 29.0) and is_equal_approx(ui._fields.task_floor.value, 29.0), "Editing longer work raises interval and both deadlines together to authoritative minima")
	lobby.config = Simulation.sanitize_config({"mode": "sleep", "task_work": 1.0, "task_interval": 22.5, "task_deadline": 22.0, "task_floor": 22.0})
	ui.show_lobby(lobby, 1)
	check(is_equal_approx(ui._fields.task_work.value, 1.0) and is_equal_approx(ui._fields.task_interval.value, 22.5) and is_equal_approx(ui._fields.task_deadline.value, 22.0) and is_equal_approx(ui._fields.task_floor.value, 22.0) and is_equal_approx(ui._fields.task_deadline.max_value, 22.0), "Receiving lower valid rules removes previous clamps without changing server values")
	ui._apply_config()
	var accepted: Dictionary = Simulation.sanitize_config(applied_config)
	var matches_authority: bool = not applied_config.is_empty()
	for field: String in ["task_interval", "task_work", "task_deadline", "task_floor"]:
		matches_authority = matches_authority and is_equal_approx(float(applied_config.get(field, -1)), float(accepted[field]))
	check(matches_authority, "Applied visible task rules survive server sanitization without a second hidden clamp")
	ui._fields.human_count.value = 5.0
	ui._fields.task_work.value = 8.0
	ui._fields.round_seconds.value = 30.0
	check(is_equal_approx(ui._fields.round_seconds.min_value, 39.0) and is_equal_approx(ui._fields.round_seconds.value, 39.0), "Task round minimum includes the last human's first reachable assignment and a simulation tick")
	ui._mode.select(0)
	ui._mode_selected(0)
	ui._fields.round_seconds.value = 30.0
	check(is_equal_approx(ui._fields.round_seconds.min_value, 30.0) and is_equal_approx(ui._fields.round_seconds.value, 30.0), "Switching to Blood restores its independent 30-second round minimum")
	ui._mode.select(2)
	ui._mode_selected(2)
	check(is_equal_approx(ui._fields.round_seconds.value, 39.0), "Switching back to Tasks immediately displays the valid longer round")
	lobby.config = Simulation.sanitize_config({"mode": "sleep", "human_count": 1, "task_work": 1.0, "round_seconds": 30.0})
	ui.show_lobby(lobby, 1)
	check(is_equal_approx(ui._fields.round_seconds.value, 30.0) and is_equal_approx(ui._fields.round_seconds.min_value, 30.0), "Receiving a shorter valid task round clears the previous roster's minimum")


func _hud_semantics() -> void:
	var actor: Dictionary = {"role": "human", "alive": true, "pitch": 0.0, "tool": "hands", "p": Vector3.ZERO, "bitten": false, "threatened": false}
	var snapshot: Dictionary = {"config": {"mode": "blood", "blood_goal": 30}, "actors": {1: actor}, "elapsed": 12.0, "time_left": 90.0}
	ui.show_game(snapshot, {}, 1)
	var help_binding := InputEventKey.new()
	help_binding.physical_keycode = KEY_F1
	Prefs.bind_action("toggle_help",help_binding,false)
	ui.show_game(snapshot,{},1)
	check(not ui._help_open and not ui._help.visible and "F1" in ui._hud_help.text, "Game starts with folded help and a discreet shortcut")
	await key(KEY_F1)
	check(ui._help_open and ui.is_menu_open() and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE, "F1 opens modal help and releases gameplay capture")
	var help_focus: Control = root.gui_get_focus_owner()
	ui.show_game(snapshot,{},1)
	await process_frame
	check(ui._help_open and root.gui_get_focus_owner() == help_focus, "Incoming snapshots keep help open and retain its focus")
	var help_focus_ok := true
	for index: int in range(8):
		await key(KEY_TAB)
		help_focus_ok = help_focus_ok and ui._help.is_ancestor_of(root.gui_get_focus_owner())
	check(help_focus_ok, "Tab stays within the folded-help modal")
	await _capture("ui06-help-human")
	await key(KEY_F1)
	check(not ui._help_open and not ui.is_menu_open() and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED, "The same shortcut closes help and resumes capture")
	await key(KEY_ESCAPE)
	ui._open_help()
	await key(KEY_ESCAPE)
	check(not ui._help_open and ui._paused and ui.is_menu_open(), "Escape from help opened in pause returns one level to pause")
	await key(KEY_ESCAPE)
	check(not ui.is_menu_open(), "A second Escape resumes the game after leaving help")
	ui._open_settings()
	ui._begin_binding("toggle_help")
	await key(KEY_F2)
	await key(KEY_ESCAPE)
	ui.show_game(snapshot,{},1)
	check(Prefs.binding_text("toggle_help") == "F2" and "F2" in ui._hud_help.text, "Help can be reassigned and its HUD shortcut updates")
	await key(KEY_F2)
	check(ui._help_open, "Reassigned shortcut opens help through a real input event")
	await key(KEY_ESCAPE)
	for sample: Array in [[0.0,"cabeza"],[-0.25,"cabeza"],[-0.251,"torso"],[-0.85,"torso"],[-0.851,"piernas"]]:
		actor.pitch = sample[0]
		ui.show_game(snapshot, {}, 1)
		check(not ui._hud_context.visible and not "cubrir" in ui._hud_hint.text, "Idle view at pitch %s shows no automatic defense band or permanent controls" % sample[0])
	actor.threatened = true
	actor.pitch = -0.5
	ui.show_game(snapshot, {"assignment": {"label": "zona privada enemiga"}}, 1)
	check(ui._hud_state.text.is_empty() and not "zona privada" in ui._hud_state.text, "Approach alone does not reveal enemy assignments or claim a real bite")
	await _capture("ui06-hud-idle")
	ui.show_game(snapshot, {"attack":{"id":3,"state":"windup","recovery":0.3}}, 1)
	check(ui._attack_recovery.visible and ui._attack_recovery.size.x <= 40 and ui._attack_feedback_key == "3:windup", "Windup uses a tiny recovery indicator without announcing a hit")
	ui.show_game(snapshot, {"attack":{"id":3,"state":"miss","recovery":0.2}}, 1)
	check(ui._attack_feedback_key == "3:miss" and ui._comic_feedback.text == "Falló", "A confirmed miss receives distinct compact feedback")
	ui.show_game(snapshot, {"attack":{"id":4,"state":"hit","recovery":0.1}}, 1)
	check(ui._attack_feedback_key == "4:hit" and ui._comic_feedback.text == "¡Tocó!", "Only a confirmed hit displays contact feedback")
	# Bind through real input events while the settings layer is modal.
	await key(KEY_ESCAPE)
	ui._open_settings()
	ui._begin_binding("bite")
	await key(KEY_H)
	ui._begin_binding("self_swat")
	await key(KEY_T)
	check(Prefs.binding_text("bite") == "H" and Prefs.binding_text("self_swat") == "T", "Real rebinding updates concentration and defense actions")
	Prefs.setup_inputs()
	check(Prefs.binding_text("bite") == "H" and Prefs.binding_text("self_swat") == "T", "Input setup does not replace existing bindings with defaults")
	await key(KEY_ESCAPE)
	check(ui._paused and ui.is_menu_open() and not ui._settings_open, "Closing game settings returns to pause without resuming")
	await key(KEY_ESCAPE)
	check(not ui.is_menu_open(), "Next Escape resumes gameplay")
	ui.show_game(snapshot, {}, 1)
	actor.bitten = true
	ui.show_game(snapshot, {"bite_feedback":{"active":true,"count":1,"side":"front"}}, 1)
	check(Prefs.binding_text("attack") in ui._hud_hint.text and "Mirá tu cuerpo" in ui._hud_hint.text and not "cubrir" in ui._hud_hint.text, "Real bite directs manual aim and uses the configured attack control")
	ui.show_game(snapshot, {"bite_feedback":{"active":true,"count":1,"side":"rear"}}, 1)
	check("pedí ayuda" in ui._hud_hint.text.to_lower() and not "zona" in ui._hud_hint.text, "Rear contact asks for teammate help without revealing free zones")
	actor.role = "mosquito"
	actor.state = "flying"
	actor.lives = 1
	var personal: Dictionary = {"assignment": {"label": "Antebrazo"}, "focus": {"state": "ready", "progress": 0.0, "reason": "Mantené E para estabilizarte y picar"}}
	ui.show_game(snapshot, personal, 1)
	await key(KEY_F2)
	check(ui._help_open and "MOSQUITO" in ui._help_body.text and "H" in ui._help_body.text, "Mosquito guide is role-specific and uses reassigned concentration")
	await _capture("ui06-help-mosquito")
	await key(KEY_ESCAPE)
	check("Mantené H" in ui._hud_hint.text and not ui._focus_progress.visible and ui._reticle.visible, "Ready state uses rebound concentration key with visible mosquito reticle")
	ui._hud_context_until = 0.0
	ui.show_game(snapshot, personal, 1)
	check(not ui._hud_context.visible, "Repeated ready snapshots do not keep contextual help permanently visible")
	personal.focus = {"state": "charging", "progress": 0.45}
	ui.show_game(snapshot, personal, 1)
	check(ui._focus_progress.visible and is_equal_approx(ui._focus_progress.value,45.0) and "45%" in ui._hud_state.text and "Soltá H para cancelar" in ui._hud_hint.text, "Charging shows authoritative progress and rebound cancel instruction")
	await _capture("ui06-hud-charging")
	await key(KEY_ESCAPE)
	var pause_focus: Control = root.gui_get_focus_owner()
	personal.focus = {"state": "idle", "progress": 0.0}
	ui.show_game(snapshot, personal, 1)
	await process_frame
	check(ui._paused and ui.is_menu_open() and root.gui_get_focus_owner() == pause_focus, "Incoming canceled-focus snapshot preserves pause and its keyboard focus")
	check(not ui._focus_progress.visible and is_zero_approx(ui._focus_progress.value), "Canceled concentration clears progress instead of showing stale charge")
	await key(KEY_ESCAPE)
	check(not ui.is_menu_open(), "Resume remains available after server cancels concentration")
	personal.focus = {"state": "blocked", "reason": "Soltá E antes de concentrarte otra vez"}
	ui.show_game(snapshot, personal, 1)
	check("Soltá H" in ui._hud_state.text and not " E " in ui._hud_state.text and not ui._focus_progress.visible, "Blocked reason substitutes the current binding for default E")
	personal.focus = {"state": "blocked", "reason": "  "}
	ui.show_game(snapshot, personal, 1)
	check(not ui._hud_state.text.strip_edges().is_empty(), "Blocked state with an empty reason still explains next step")
	personal.focus = {"state": "idle", "reason": "Soltá E antes de concentrarte otra vez"}
	ui.show_game(snapshot, personal, 1)
	check("Soltá H" in ui._hud_state.text, "Suppressed idle concentration explains how to restart with rebound key")
	personal.focus = {"state": "attached", "progress": 1.0}
	ui.show_game(snapshot, personal, 1)
	check("Picando" in ui._hud_state.text and "H para desprenderte" in ui._hud_hint.text and not ui._focus_progress.visible, "Attached private state shows rebound detach action")
	for mode: String in ["blood","sleep"]:
		snapshot.config.mode=mode
		actor.state="stunned"
		ui.show_game(snapshot,{"stun":{"active":true,"remaining":17.2,"helped":false},"focus":{"state":"charging","progress":0.9}},1)
		check("Aturdido" in ui._hud_state.text and "18" in ui._hud_state.text and not ui._reticle.visible and not ui._focus_progress.visible and ui._hud_equipment.text.is_empty(), "Stun in %s hides aim, charge and obsolete lives while showing remaining time" % mode)
		ui.show_game(snapshot,{"stun":{"active":true,"remaining":12.0,"helped":true}},1)
		check("×4" in ui._hud_hint.text and "Te están ayudando" in ui._hud_hint.text, "A real helper changes the stunned player's recovery hint in " + mode)
		if mode == "blood": await _capture("ui06-hud-stunned")
		actor.state="flying"
		ui.show_game(snapshot,{"help":{"state":"ready","target":7,"remaining":20.0,"progress":0.4},"focus":{"state":"ready"}},1)
		check("Compañero aturdido" in ui._hud_state.text and "Mantené H" in ui._hud_hint.text and not "picar" in ui._hud_hint.text, "Nearby rescue takes precedence over biting and respects rebound key in " + mode)
		ui.show_game(snapshot,{"help":{"state":"helping","target":7,"remaining":17.5,"progress":0.5}},1)
		check(ui._focus_progress.visible and is_equal_approx(ui._focus_progress.value,50.0) and "Ayudando" in ui._hud_state.text, "Helping uses authoritative recovery progress in " + mode)
		if mode == "blood": await _capture("ui06-hud-helping")
		ui.show_game(snapshot,{"help":{"state":"idle"},"focus":{"state":"idle"}},1)
		check(not ui._focus_progress.visible and not ui._hud_context.visible and ui._reticle.visible, "Releasing help clears rescue progress and returns the normal view in " + mode)
	snapshot.config.mode="survival"
	actor.state="dead"
	actor.alive = false
	ui.show_game(snapshot, {"focus": {"state":"charging","progress":0.8}}, 1)
	check("Eliminado" in ui._hud_state.text and not ui._focus_progress.visible and not ui._reticle.visible, "Death clears live focus UI and reticle")
	check(not "ayuda" in ui._hud_hint.text and not "vidas" in ui._hud_equipment.text, "Survival elimination has no rescue or personal lives UI")
	actor.alive = true
	actor.state="human"
	actor.role = "human"
	actor.bitten = true
	actor.tool = "racket"
	snapshot.elapsed = 0.0
	ui.show_game(snapshot, {}, 1)
	await process_frame
	await process_frame
	var bottom: Control = ui._hud_hint.get_parent().get_parent()
	check(bottom.get_global_rect().end.y <= root.size.y and ui._hud_hint.get_global_rect().end.y <= root.size.y, "Contextual human help with rebound controls stays inside the viewport")
	check(ui._hud_context.get_global_rect().end.x < 400 and ui._hud_context.size.x < root.size.x / 3.0, "Contextual help occupies only the left corner, leaving the lower center clear")
	ui.show_home()


func _capture(filename: String) -> void:
	if not OS.get_cmdline_user_args().has("--screens") or DisplayServer.get_name() == "headless":
		return
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var directory := ProjectSettings.globalize_path("res://../outputs/0.6-editor/")
	DirAccess.make_dir_recursive_absolute(directory)
	root.get_texture().get_image().save_png(directory.path_join(filename + ".png"))


func _capture_editor_samples() -> void:
	if not OS.get_cmdline_user_args().has("--screens") or DisplayServer.get_name()=="headless": return
	Prefs.cosmetics = Cosmetics.default_profile()
	ui._open_customization()
	for role: String in ["human","mosquito"]:
		ui._select_custom_role(role)
		ui._select_custom_category("outfit")
		ui._avatar_preview.reset_view()
		await _capture("ui06-sample-"+role+"-a")
		var variant: Dictionary = {"color":1,"accent":4,"eyes":0,"mouth":1,"brows":2,"hair":2,"outfit":1,"accessory":0,"footwear":1} if role=="human" else {"color":3,"accent":4,"eyes":1,"mouth":2,"brows":0,"hair":2,"outfit":1,"accessory":1,"footwear":2}
		for key: String in variant:
			ui._select_custom_category(key)
			ui._select_custom_option(int(variant[key]))
		ui._select_custom_category("outfit")
		ui._avatar_preview.reset_view()
		await _capture("ui06-sample-"+role+"-b")
	ui._close_customization()
