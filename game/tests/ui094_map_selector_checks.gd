extends SceneTree
## UI-only contract for the authored alfa map selector.
const UI = preload("res://scripts/ui.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const Simulation = preload("res://scripts/simulation.gd")

var ui: CanvasLayer
var checks := 0
var failures: Array[String] = []
var practice_args: Array = []
var applied_config: Dictionary = {}


func _initialize() -> void:
	_run.call_deferred()


func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		printerr("UI094_FAIL " + label)


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


func _visible_copy(root_control: Control) -> String:
	var result := PackedStringArray()
	var pending: Array[Node] = [root_control]
	while not pending.is_empty():
		var node: Node = pending.pop_back()
		if node is Control and not (node as Control).is_visible_in_tree():
			continue
		if node is Label:
			result.append((node as Label).text)
		elif node is Button:
			result.append((node as Button).text)
		elif node is CheckButton:
			result.append((node as CheckButton).text)
		if node is LineEdit:
			result.append(str(node.placeholder_text))
		if node is OptionButton:
			for index: int in range(node.item_count):
				result.append(node.get_item_text(index))
		if node is Control:
			result.append(str(node.tooltip_text))
		for child: Node in node.get_children():
			pending.append(child)
	return "\n".join(result).to_lower()


func _run() -> void:
	root.size = Vector2i(1280, 720)
	var catalog: Array[Dictionary] = Maps.playable_maps()
	_check(catalog == [{"id":"house-patio-v1", "label":"Casa con patio"}], "alfa exposes only the available authored map")
	_check(Maps.default_map_id() == "house-patio-v1", "authored patio house is the default playable map")

	ui = UI.new()
	root.add_child(ui)
	ui.practice_requested.connect(func(role: String, mode: String, map_id: String) -> void:
		practice_args = [role, mode, map_id]
	)
	ui.config_requested.connect(func(config: Dictionary) -> void: applied_config = config)
	await _settle()

	ui._open_practice()
	await _settle()
	_check(ui._practice_map_select.is_visible_in_tree() and ui._practice_map_select.item_count == 1, "training keeps the one-map selector visible")
	_check(ui._practice_map_select.get_item_text(0) == "Casa con patio" and ui._practice_map_select.get_item_metadata(0) == "house-patio-v1", "training selector is fed by playable map id and label")
	_check(root.gui_get_focus_owner() == ui._practice_start_button, "training retains an actionable initial keyboard focus")
	var practice_copy := _visible_copy(ui._practice_screen)
	ui._practice_start_button.pressed.emit()
	_check(practice_args == ["human", "blood", "house-patio-v1"], "training emits the selected authored map with role and mode")
	ui.show_home()
	ui._open_practice()
	await _key(KEY_ESCAPE)
	_check(ui._screen == "home", "Escape returns one level from the training map selector")

	var config := Simulation.DEFAULT_CONFIG.duplicate(true)
	config.map_id = "house-patio-v1"
	var players := {1:{"name":"Ana", "ready":false}, 2:{"name":"Beto", "ready":false}}
	var lobby := {"owner":1, "players":players, "config":config, "can_start":false}
	ui.show_lobby(lobby, 1)
	await _settle()
	_check(ui._lobby_map_select.visible and not ui._lobby_map_select.disabled, "host sees an editable map selector")
	_check(ui._lobby_map_select.item_count == 1 and ui._lobby_map_select.get_item_text(0) == "Casa con patio", "lobby shows no unavailable future map")
	ui._apply_config()
	_check(applied_config.get("map_id") == "house-patio-v1", "host rule update includes selected map_id")

	ui.show_lobby(lobby, 2)
	await _settle()
	_check(ui._lobby_map_select.visible and ui._lobby_map_select.disabled, "guest sees the selected map read-only")
	_check("anfitrión" in ui._lobby_map_note.text and "Casa con patio" in ui._lobby_map_note.text, "guest receives a clear read-only map explanation")
	var visible_copy := _visible_copy(ui._lobby_panel) + "\n" + practice_copy
	var procedural_words_absent := true
	for forbidden: String in ["semilla", "seed", "aleatori", "procedural", "generad"]:
		procedural_words_absent = procedural_words_absent and forbidden not in visible_copy
	_check(procedural_words_absent, "playable UI makes no procedural or random-map promise")

	print("UI094_MAP_SELECTOR %d/%d PASS" % [checks-failures.size(), checks])
	quit(0 if failures.is_empty() else 1)
