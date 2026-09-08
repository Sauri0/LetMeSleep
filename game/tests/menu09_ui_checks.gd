extends "res://tests/customization08_checks.gd"
## Isolated real UI inputs. Voice/backend states are injected, never microphone
## audio. Run with the native/preferences reservation; original bytes restored.
const Catalog = preload("res://scripts/emote_catalog.gd")
var requests: Array[String] = []
var favorites: Array = []
var preview_requests: Array[String] = []
var voice_requests: Array[bool] = []
var mute_requests: Array[bool] = []
var peer_requests: Array = []
var closed_previews := 0
var snapshot := {"actors":{2:{"role":"human","alive":true,"state":"human","tool":"hands"}},"time_left":99,"config":{"mode":"blood","blood_goal":12}}
var output := ""

func _event(code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)
	await _settle()

func _temporary_binding(action: String, code: int) -> void:
	if not InputMap.has_action(action): InputMap.add_action(action)
	InputMap.action_erase_events(action)
	var event := InputEventKey.new()
	event.physical_keycode = code
	InputMap.action_add_event(action,event)

func _run() -> void:
	_backup()
	root.size = Vector2i(1280,720)
	output = ProjectSettings.globalize_path("res://../outputs/0.9-menu")
	DirAccess.make_dir_recursive_absolute(output)
	ui = UI.new()
	root.add_child(ui)
	_temporary_binding("emote_menu",KEY_B)
	_temporary_binding("push_to_talk",KEY_V)
	ui.emote_requested.connect(func(id: String) -> void: requests.append(id))
	ui.emote_favorite_requested.connect(func(slot: int, id: String) -> void: favorites.append([slot,id]))
	ui.emote_preview_requested.connect(func(id: String) -> void: preview_requests.append(id))
	ui.emote_preview_closed.connect(func() -> void: closed_previews+=1)
	ui.voice_test_requested.connect(func(value: bool) -> void: voice_requests.append(value))
	ui.voice_mute_requested.connect(func(value: bool) -> void: mute_requests.append(value))
	ui.voice_peer_mute_requested.connect(func(id: int, value: bool) -> void: peer_requests.append([id,value]))
	await _settle()
	_check(root.gui_get_focus_owner()==ui._home_default_focus,"home focuses primary action")
	_check(ui._menu_mascot.preview.role=="mosquito" and ui._menu_mascot.preview.avatar.actor_role=="mosquito","home renders production mosquito")
	_check(ui._menu_mascot.preview.viewport.own_world_3d,"home mascot has isolated world")
	_check(ui._menu_mascot.preview.viewport.transparent_bg,"home mascot integrates into comic background")
	_check(ui._menu_mascot.preview.focus_mode==Control.FOCUS_NONE and ui._menu_mascot.preview.mouse_filter==Control.MOUSE_FILTER_IGNORE,"mascot cannot steal Tab or mouse")
	_check(ui._menu_mascot.size.y>=230 and ui._menu_mascot.get_global_rect().end.y<root.size.y,"home silhouette area fits at 720p")
	_check(ui._home_menu.get_global_rect().end.y<root.size.y,"home actions fit at 720p")
	await _capture("home")
	await _key(KEY_TAB)
	_check(ui._home.is_ancestor_of(root.gui_get_focus_owner()),"Tab stays in home")
	ui._home_default_focus.grab_focus()
	await _key(KEY_ENTER)
	_check(ui._screen=="practice" and not ui._menu_mascot.active,"Enter opens practice and suspends mascot")
	await _key(KEY_ESCAPE)
	_check(ui._screen=="home" and ui._menu_mascot.active,"Escape restores home mascot")
	var profile: Dictionary = Prefs.cosmetics.duplicate(true)
	ui._open_customization()
	ui.set_emote_state({"available":true,"preview_available":true,"favorites":Catalog.IDS})
	ui._show_custom_emotes()
	await _settle()
	_check(ui._custom_emotes and ui._avatar_preview.focus_key.is_empty(),"emote section shows full body")
	_check(ui._custom_emote_button.get_global_rect().position.y<root.size.y,"emote category is easy to reach")
	var buttons := _buttons(ui._custom_options)
	for button: Button in buttons:
		if button.text=="Probar": button.pressed.emit(); break
	_check(preview_requests==["wave"],"preview sends real gesture request")
	for button: Button in buttons:
		if button.text=="Usar en atajo": button.pressed.emit(); break
	_check(favorites==[[0,"wave"]],"favorite sends exact slot and shared ID")
	ui._favorite_buttons.wave.grab_focus()
	ui.set_emote_state({"available":true,"preview_available":true,"favorites":["celebrate","wave","shrug","yawn"]})
	await _settle()
	_check(root.gui_get_focus_owner()==ui._favorite_buttons.wave,"favorite refresh preserves keyboard focus")
	ui.set_emote_state({"available":true,"preview_available":true,"favorites":Catalog.IDS})
	_check(Prefs.cosmetics==profile,"emotes do not alter cosmetics")
	ui._select_custom_category("eyes")
	_check(not ui._custom_emotes and closed_previews==1,"appearance selection stops gesture preview")
	ui._show_custom_emotes()
	ui._select_custom_role("mosquito")
	_check(not ui._custom_emotes and not ui._custom_emote_button.visible and closed_previews==2,"mosquito retains appearance editor without human emotes")
	ui._close_customization()
	ui.show_game(snapshot,{},2)
	await _event(KEY_B,true)
	_check(ui._emote_selector.visible and ui.is_menu_open(),"hold B opens modal and blocks gameplay")
	_check(Input.mouse_mode==Input.MOUSE_MODE_VISIBLE,"selector releases cursor")
	await _event(KEY_B,false)
	_check(requests.is_empty() and not ui._emote_selector.visible,"release without selection cancels")
	await _event(KEY_B,true)
	await _key(KEY_RIGHT)
	ui.show_game(snapshot,{},2)
	_check(ui._emote_selector.visible,"network snapshots preserve modal")
	await _capture("emote-selector")
	await _event(KEY_B,false)
	_check(requests==["wave"],"hold direction release sends one shared emote")
	_check(not ui.is_menu_open() and Input.mouse_mode==Input.MOUSE_MODE_CAPTURED,"selector returns capture to game")
	await _event(KEY_B,true)
	await _key(KEY_ESCAPE)
	await _event(KEY_B,false)
	_check(requests.size()==1 and not ui._emote_selector.visible,"Escape never performs gesture")
	ui.set_pause(true)
	ui._emote_button.pressed.emit()
	await _key(KEY_DOWN)
	await _key(KEY_ENTER)
	_check(requests.size()==2 and ui._paused and ui.is_menu_open(),"click selector returns to existing pause")
	ui.set_pause(false)
	ui.set_emote_state({"available":false,"reason":"Solo humanos libres","favorites":Catalog.IDS})
	await _key(KEY_B)
	_check(not ui._emote_selector.visible and requests.size()==2,"unavailable gesture cannot open or submit")
	ui.set_emote_state({"available":true,"favorites":Catalog.IDS})
	_temporary_binding("emote_menu",KEY_N)
	await _key(KEY_B)
	_check(not ui._emote_selector.visible,"old shortcut inactive after remap")
	await _event(KEY_N,true)
	_check(ui._emote_selector.visible and ui._emote_selector.hint.text.begins_with("N"),"remapped shortcut and hint agree")
	await _event(KEY_N,false)
	var voice := {"available":true,"can_test":true,"visible":true,"status":"idle","muted":false,"level":0.0,"peers":[{"id":4,"name":"Vecino","muted":false}]}
	ui.set_voice_state(voice)
	_check(ui._voice_indicator.visible and voice_requests.is_empty(),"idle voice visible without requesting capture")
	ui._open_settings()
	await _settle()
	_check(voice_requests.is_empty() and not ui._voice_indicator.visible,"opening settings never captures")
	_check(not ui._voice_test.disabled,"explicit manual test enabled by backend")
	var scroll: ScrollContainer = ui._voice_test.get_parent().get_parent().get_parent()
	if scroll is ScrollContainer: scroll.ensure_control_visible(ui._voice_test)
	ui._voice_test.grab_focus()
	await _event(KEY_ENTER,true)
	_check(voice_requests==[true],"holding focused test explicitly requests microphone")
	await _event(KEY_ENTER,false)
	_check(voice_requests==[true,false],"release closes manual microphone request")
	ui._voice_test.grab_focus()
	await _event(KEY_ENTER,true)
	await _key(KEY_ESCAPE)
	await _event(KEY_ENTER,false)
	_check(voice_requests==[true,false,true,false] and not ui._settings_open,"Escape stops held microphone before closing settings")
	voice.status="capturing"
	voice.level=.56
	ui.set_voice_state(voice)
	_check(ui._voice_indicator.text_label.text=="Transmitiendo" and is_equal_approx(ui._voice_indicator.level_bar.value,56),"indicator reflects confirmed audio level")
	voice.level=NAN
	ui.set_voice_state(voice)
	_check(ui._voice_indicator.level_bar.value==0,"non-finite audio level cannot corrupt meter")
	voice.status="muted"
	voice.muted=true
	ui.set_voice_state(voice)
	_check(ui._voice_test.disabled and not ui._voice_indicator.level_bar.visible,"muted voice cannot start manual test")
	ui._open_settings()
	ui._voice_mute.toggled.emit(false)
	_check(mute_requests==[false],"mute control emits explicit request")
	var peer_button: CheckButton = ui._voice_peers.get_child(1)
	peer_button.toggled.emit(true)
	_check(peer_requests==[[4,true]],"per-player mute targets exact peer")
	voice.error="No se pudo abrir el micrófono. Revisá el dispositivo en Windows."
	ui.set_voice_state(voice)
	_check(ui._voice_status.text==voice.error,"specific backend errors remain visible")
	ui._close_settings()
	ui.show_home()
	_check(not ui._voice_indicator.visible and not ui._emote_selector.visible,"home closes session overlays")
	await _finish()

func _buttons(parent: Node) -> Array[Button]:
	var result: Array[Button]=[]
	for child: Node in parent.get_children():
		if child is Button: result.append(child)
		result.append_array(_buttons(child))
	return result

func _capture(filename: String) -> void:
	if DisplayServer.get_name()=="headless": return
	await _settle()
	await RenderingServer.frame_post_draw
	_check(root.get_texture().get_image().save_png(output.path_join(filename+".png"))==OK,"saved native "+filename)

func _finish() -> void:
	finishing=true
	ui.queue_free()
	await _settle()
	_restore()
	_check(FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH),"original preference bytes restored")
	var file:=FileAccess.open(output.path_join("checks.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"microphone_capture":false,"synthetic_voice_state":true},"\t"))
	file.close()
	print("MENU09_UI_CHECKS %d/%d PASS"%[checks-failures.size(),checks])
	quit(0 if failures.is_empty() else 1)
