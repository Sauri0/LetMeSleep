extends CanvasLayer
## Spanish menus and private local HUD. The server remains the source of truth.

signal connect_requested(address: String, port: int, player_name: String, code: String, create: bool)
signal local_server_requested
signal role_requested(role: String)
signal ready_requested(value: bool)
signal config_requested(config: Dictionary)
signal start_requested
signal rematch_requested
signal leave_requested

const Prefs = preload("res://scripts/preferences.gd")
const Simulation = preload("res://scripts/simulation.gd")
const INK := Color("172935")
const PANEL := Color("213743")
const CREAM := Color("fff0d3")
const MUTED := Color("b5c5c4")
const CORAL := Color("ff966f")
const MINT := Color("9addbd")
const MODES := ["blood", "survival", "sleep"]
const MODE_NAMES := {"blood": "Recolección de sangre", "survival": "Supervivencia", "sleep": "Dejanos dormir"}
const TOOL_NAMES := {"hands": "Manos · palmadas", "swatter": "Matamoscas", "racket": "Raqueta eléctrica", "newspaper": "Diario enrollado", "broom": "Escoba"}
const FALLBACK_CONFIG: Dictionary = Simulation.DEFAULT_CONFIG
const CONFIG_FIELDS := [
	["round_seconds", "Duración de ronda · s", 30, 180, 1, "all"],
	["rotation_seconds", "Cambio de zona · s", 4, 40, 1, "all"],
	["blood_goal", "Cuota compartida", 1, 1000, 1, "blood"],
	["mosquito_lives", "Vidas por mosquito", 1, 9, 1, "sleep"],
	["respawn_seconds", "Demora para reaparecer · s", 1, 15, 1, "sleep"],
	["task_interval", "Frecuencia de tareas · s", 15, 60, 1, "sleep"],
	["task_deadline", "Plazo inicial por tarea · s", 3, 59.5, 0.5, "sleep"],
	["task_work", "Trabajo por tarea · s", 1, 8, 1, "sleep"],
	["task_penalty", "Menos plazo por fallo · s", 0.5, 8, 0.5, "sleep"],
	["task_floor", "Plazo mínimo · s", 3, 59.5, 0.5, "sleep"],
	["task_goal", "Meta de tareas · 0 = auto", 0, 100, 1, "sleep"],
]

var _root: Control
var _home: Control
var _lobby: Control
var _hud: Control
var _results: Control
var _pause: Control
var _settings: Control
var _screen: String = "home"
var _built: bool = false
var _paused: bool = false
var _settings_open: bool = false
var _local_id: int = 0
var _owner: bool = false
var _ready_value: bool = false
var _config_signature: String = ""
var _roster_signature: String = ""
var _config: Dictionary = FALLBACK_CONFIG.duplicate(true)
var _updating_config: bool = false
var _binding_action: String = ""
var _binding_buttons: Dictionary = {}
var _field_rows: Dictionary = {}
var _fields: Dictionary = {}
var _status_labels: Array[Label] = []
var _name_edit: LineEdit
var _address_edit: LineEdit
var _port_edit: SpinBox
var _code_edit: LineEdit
var _code_label: Label
var _players_label: Label
var _roster: VBoxContainer
var _mode: OptionButton
var _mode_description: Label
var _config_apply: Button
var _ready_button: Button
var _start_button: Button
var _start_reason: Label
var _human_button: Button
var _mosquito_button: Button
var _hud_role: Label
var _hud_objective: Label
var _hud_time: Label
var _hud_progress: ProgressBar
var _hud_progress_text: Label
var _hud_state: Label
var _hud_hint: Label
var _hud_tool: Label
var _task_panel: PanelContainer
var _task_title: Label
var _task_time: Label
var _task_detail: Label
var _task_progress: ProgressBar
var _reticle: Label
var _result_title: Label
var _result_subtitle: Label
var _result_stats: Label
var _rematch_button: Button
var _binding_notice: Label
var _settings_controls: Dictionary = {}


func _ready() -> void:
	Prefs.load_settings()
	layer = 10
	_build()
	show_home()


func _build() -> void:
	if _built:
		return
	_built = true
	_root = Control.new()
	_root.name = "Interface"
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.theme = _make_theme()
	add_child(_root)
	_build_home()
	_build_lobby()
	_build_hud()
	_build_results()
	_build_pause()
	_build_settings()


func _make_theme() -> Theme:
	var theme := Theme.new()
	theme.default_font_size = 18
	theme.set_color("font_color", "Label", CREAM)
	theme.set_color("font_color", "Button", CREAM)
	theme.set_color("font_hover_color", "Button", Color.WHITE)
	theme.set_color("font_disabled_color", "Button", Color("788c92"))
	theme.set_color("font_color", "LineEdit", CREAM)
	theme.set_color("font_placeholder_color", "LineEdit", MUTED)
	theme.set_color("caret_color", "LineEdit", CORAL)
	theme.set_color("font_color", "CheckButton", CREAM)
	for kind: String in ["Button", "OptionButton"]:
		theme.set_stylebox("normal", kind, _style(Color("344b55"), 10, 16, 11))
		theme.set_stylebox("hover", kind, _style(Color("49626a"), 10, 16, 11))
		theme.set_stylebox("pressed", kind, _style(Color("586d70"), 10, 16, 11))
		theme.set_stylebox("disabled", kind, _style(Color("273d47"), 10, 16, 11))
		theme.set_stylebox("focus", kind, _outline(MINT))
	for state: String in ["normal", "focus"]:
		theme.set_stylebox(state, "LineEdit", _style(Color("11232e"), 9, 12, 10))
	theme.set_stylebox("panel", "PopupMenu", _style(PANEL, 10, 8, 8))
	theme.set_stylebox("background", "ProgressBar", _style(Color("122630"), 5, 0, 0))
	theme.set_stylebox("fill", "ProgressBar", _style(CORAL, 5, 0, 0))
	theme.set_constant("separation", "VBoxContainer", 10)
	theme.set_constant("separation", "HBoxContainer", 12)
	return theme


func _style(color: Color, radius: int = 14, horizontal: int = 20, vertical: int = 20) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.content_margin_left = horizontal
	style.content_margin_right = horizontal
	style.content_margin_top = vertical
	style.content_margin_bottom = vertical
	return style


func _outline(color: Color) -> StyleBoxFlat:
	var style := _style(Color(0, 0, 0, 0), 10, 0, 0)
	style.border_color = color
	style.set_border_width_all(2)
	return style


func _label(text_value: String, size: int = 18, color: Color = CREAM, wrap: bool = false) -> Label:
	var label := Label.new()
	label.text = text_value
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	if wrap:
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return label


func _button(text_value: String, callback: Callable, accent: bool = false) -> Button:
	var button := Button.new()
	button.text = text_value
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.pressed.connect(callback)
	if accent:
		button.add_theme_stylebox_override("normal", _style(CORAL, 10, 16, 11))
		button.add_theme_stylebox_override("hover", _style(Color("ffb38f"), 10, 16, 11))
		button.add_theme_stylebox_override("pressed", _style(Color("e68262"), 10, 16, 11))
		button.add_theme_color_override("font_color", INK)
		button.add_theme_color_override("font_hover_color", INK)
		button.add_theme_color_override("font_pressed_color", INK)
	return button


func _panel(parent: Node, color: Color = PANEL) -> PanelContainer:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", _style(color))
	parent.add_child(panel)
	return panel


func _vbox(parent: Node, separation: int = 12) -> VBoxContainer:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", separation)
	parent.add_child(box)
	return box


func _full_control(parent: Node) -> Control:
	var control := Control.new()
	control.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	parent.add_child(control)
	return control


func _menu_surface() -> Control:
	var surface := _full_control(_root)
	var backdrop := ColorRect.new()
	backdrop.color = Color(0.055, 0.11, 0.16, 0.94)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	surface.add_child(backdrop)
	return surface


func _margin(parent: Node, amount: int = 28) -> MarginContainer:
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, amount)
	parent.add_child(margin)
	return margin


func _scroll(parent: Node) -> ScrollContainer:
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	parent.add_child(scroll)
	return scroll


func _status(parent: Node) -> Label:
	var status := _label("", 15, MINT, true)
	status.custom_minimum_size.y = 22
	parent.add_child(status)
	_status_labels.append(status)
	return status


func _build_home() -> void:
	_home = _menu_surface()
	var columns := HBoxContainer.new()
	columns.add_theme_constant_override("separation", 38)
	_margin(_home, 38).add_child(columns)
	var intro := _vbox(columns, 18)
	intro.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	intro.size_flags_stretch_ratio = 0.9
	intro.add_child(_label("UNA NOCHE. CERO PAZ.", 15, CORAL))
	intro.add_child(_label("Dejame\ndormir", 70))
	intro.add_child(_label("Humanos contra mosquitos", 24, MINT))
	intro.add_child(_label("Una casa calurosa, tus amigos y ese zumbido que no te deja en paz.", 22, CREAM, true))
	var space := Control.new()
	space.size_flags_vertical = Control.SIZE_EXPAND_FILL
	intro.add_child(space)
	intro.add_child(_label("01  Elegí tu bando\n02  Reuní al grupo\n03  Que empiece el zumbido", 19, MUTED))
	var intro_actions := HBoxContainer.new()
	intro.add_child(intro_actions)
	var settings_button := _button("Ajustes y controles", _open_settings)
	settings_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	intro_actions.add_child(settings_button)
	intro_actions.add_child(_button("Salir", func() -> void:
		get_tree().root.propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST)
		get_tree().quit()
	))
	intro.add_child(_label("PROTOTIPO 0.1.0  ·  WINDOWS  ·  ONLINE", 13, MUTED))
	var card := _panel(columns)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.size_flags_stretch_ratio = 1.05
	var card_box := _vbox(card, 8)
	var scroll := _scroll(card_box)
	var content := _vbox(scroll, 10)
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_child(_label("Entrá a la casa", 30))
	content.add_child(_label("El anfitrión enciende el servidor; todos usan su dirección y el mismo código de sala.", 16, MUTED, true))
	content.add_child(_label("TU NOMBRE", 13, CORAL))
	_name_edit = LineEdit.new()
	_name_edit.max_length = 24
	_name_edit.placeholder_text = "Cómo te dicen tus amigos"
	_name_edit.text = Prefs.player_name
	content.add_child(_name_edit)
	content.add_child(_label("DIRECCIÓN DEL SERVIDOR", 13, CORAL))
	var address_row := HBoxContainer.new()
	content.add_child(address_row)
	_address_edit = LineEdit.new()
	_address_edit.text = Prefs.server_address
	_address_edit.placeholder_text = "IP o nombre del anfitrión"
	_address_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	address_row.add_child(_address_edit)
	_port_edit = SpinBox.new()
	_port_edit.min_value = 1
	_port_edit.max_value = 65535
	_port_edit.value = Prefs.server_port
	_port_edit.custom_minimum_size.x = 116
	_port_edit.tooltip_text = "Puerto UDP del servidor"
	address_row.add_child(_port_edit)
	content.add_child(_label("En esta PC: 127.0.0.1. En otra casa: dirección del anfitrión. El código identifica la sala; la dirección permite llegar al servidor.", 14, MUTED, true))
	content.add_child(_label("CÓDIGO DE SALA · SOLO PARA UNIRSE", 13, CORAL))
	_code_edit = LineEdit.new()
	_code_edit.max_length = 12
	_code_edit.placeholder_text = "Ej.: ABC123"
	_code_edit.text = Prefs.room_code
	content.add_child(_code_edit)
	var actions := HBoxContainer.new()
	content.add_child(actions)
	var create_button := _button("Crear sala", _request_connection.bind(true), true)
	create_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(create_button)
	var join_button := _button("Unirme", _request_connection.bind(false))
	join_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(join_button)
	content.add_child(HSeparator.new())
	content.add_child(_button("Encender servidor en esta PC", func() -> void:
		_address_edit.text = "127.0.0.1"
		_port_edit.value = 27840
		local_server_requested.emit()
	))
	content.add_child(_label("De 1 a 5 humanos, con al menos 2 mosquitos por humano. Sangre y supervivencia: una vida. Dejanos dormir: vidas configurables.", 14, MUTED, true))
	_status(card_box)


func _request_connection(create: bool) -> void:
	var username := _name_edit.text.strip_edges()
	if username.is_empty():
		show_status("Escribí tu nombre para entrar.")
		_name_edit.grab_focus()
		return
	var address := _address_edit.text.strip_edges()
	if address.is_empty():
		show_status("Falta la dirección del servidor.")
		_address_edit.grab_focus()
		return
	var code := _code_edit.text.strip_edges().to_upper()
	if not create and code.is_empty():
		show_status("Pedile el código de sala al anfitrión.")
		_code_edit.grab_focus()
		return
	Prefs.player_name = username
	Prefs.server_address = address
	Prefs.server_port = int(_port_edit.value)
	Prefs.room_code = code
	Prefs.save_settings()
	show_status("Conectando con %s:%d…" % [address, int(_port_edit.value)])
	connect_requested.emit(address, int(_port_edit.value), username, code, create)


func _build_lobby() -> void:
	_lobby = _menu_surface()
	var frame := _vbox(_margin(_lobby), 14)
	var header := HBoxContainer.new()
	frame.add_child(header)
	var title := _vbox(header, 2)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_child(_label("La previa", 34))
	title.add_child(_label("Dejame dormir · Prepará la próxima ronda", 15, MUTED))
	_code_label = _label("SALA  —", 26, CORAL)
	header.add_child(_code_label)
	header.add_child(_button("Copiar código", func() -> void:
		DisplayServer.clipboard_set(_code_label.get_meta("code", ""))
		show_status("Código copiado. Compartí también la dirección del servidor.")
	))
	header.add_child(_button("Ajustes", _open_settings))
	var columns := HBoxContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	columns.add_theme_constant_override("separation", 20)
	frame.add_child(columns)
	var team_card := _panel(columns)
	team_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	team_card.size_flags_stretch_ratio = 1.05
	var team := _vbox(team_card, 12)
	team.add_child(_label("Elegí tu bando", 25))
	var role_row := HBoxContainer.new()
	team.add_child(role_row)
	_human_button = _button("Humano", func() -> void: role_requested.emit("human"))
	_human_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	role_row.add_child(_human_button)
	_mosquito_button = _button("Mosquito", func() -> void: role_requested.emit("mosquito"))
	_mosquito_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	role_row.add_child(_mosquito_button)
	_players_label = _label("Esperando jugadores", 16, MINT, true)
	team.add_child(_players_label)
	var roster_scroll := _scroll(team)
	_roster = _vbox(roster_scroll, 6)
	_roster.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	team.add_child(_label("Humanos: manos y objetos del mapa.\nMosquitos: volá, picá y desprendete con una pulsación.", 15, MUTED, true))
	_ready_button = _button("Estoy listo", func() -> void: ready_requested.emit(not _ready_value), true)
	team.add_child(_ready_button)
	var config_card := _panel(columns)
	config_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var config_box := _vbox(config_card, 8)
	config_box.add_child(_label("Reglas de la casa", 25))
	_mode = OptionButton.new()
	for mode: String in MODES:
		_mode.add_item(MODE_NAMES[mode])
	_mode.item_selected.connect(_mode_selected)
	config_box.add_child(_mode)
	_mode_description = _label("", 15, MUTED, true)
	config_box.add_child(_mode_description)
	var config_scroll := _scroll(config_box)
	var fields_box := _vbox(config_scroll, 7)
	fields_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for field: Array in CONFIG_FIELDS:
		var key: String = field[0]
		var row := HBoxContainer.new()
		fields_box.add_child(row)
		var label := _label(field[1], 15, CREAM, true)
		label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(label)
		var value := SpinBox.new()
		value.min_value = field[2]
		value.max_value = field[3]
		value.step = field[4]
		value.value = _config.get(key, field[2])
		value.custom_minimum_size.x = 105
		value.value_changed.connect(func(_number: float) -> void:
			if not _updating_config:
				_update_dependent_ranges()
				_config_apply.text = "Aplicar cambios"
		)
		row.add_child(value)
		_fields[key] = value
		_field_rows[key] = row
	config_box.add_child(_label("Valores iniciales de prueba. Cambiar las reglas requiere que todos vuelvan a marcarse listos.", 13, MUTED, true))
	_config_apply = _button("Aplicar reglas", _apply_config)
	config_box.add_child(_config_apply)
	_start_reason = _label("", 16, MUTED, true)
	frame.add_child(_start_reason)
	var footer := HBoxContainer.new()
	frame.add_child(footer)
	footer.add_child(_button("Salir de la sala", func() -> void: leave_requested.emit()))
	var status := _status(footer)
	status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_start_button = _button("Empezar ronda", func() -> void: start_requested.emit(), true)
	footer.add_child(_start_button)


func _mode_selected(_index: int) -> void:
	_update_mode_fields()
	if not _updating_config:
		_config_apply.text = "Aplicar cambios"


func _update_mode_fields() -> void:
	var mode: String = MODES[clampi(_mode.selected, 0, 2)]
	for field: Array in CONFIG_FIELDS:
		_field_rows[field[0]].visible = field[5] == "all" or field[5] == mode
	match mode:
		"blood": _mode_description.text = "Mosquitos: junten la cuota compartida. Humanos: evítenlo hasta el final o elimínenlos a todos. La sangre extraída se conserva."
		"survival": _mode_description.text = "Mosquitos: basta con que uno siga vivo al final. Humanos: elimínenlos a todos. Picar es opcional."
		"sleep": _mode_description.text = "Humanos: alcancen la meta colectiva o agoten las vidas de todos los mosquitos. Cada fallo acorta solo el plazo futuro de quien falló. Las picaduras pausan el trabajo."


func _update_dependent_ranges() -> void:
	# Mirror the authoritative dependencies while editing, so every displayed
	# value can actually be accepted by the server.
	if not _fields.has("task_floor"):
		return
	var was_updating: bool = _updating_config
	_updating_config = true
	_fields["task_deadline"].min_value = float(_fields["task_work"].value) + 2.0
	_fields["task_deadline"].max_value = float(_fields["task_interval"].value) - 0.5
	_fields["task_floor"].min_value = float(_fields["task_work"].value) + 2.0
	_fields["task_floor"].max_value = float(_fields["task_deadline"].value)
	_updating_config = was_updating


func _apply_config() -> void:
	if not _owner:
		return
	var config := _config.duplicate(true)
	config["mode"] = MODES[clampi(_mode.selected, 0, 2)]
	for key: String in _fields:
		config[key] = _fields[key].value
	config_requested.emit(config)
	_config_apply.text = "Reglas enviadas"


func _build_hud() -> void:
	_hud = _full_control(_root)
	_hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var left := _panel(_hud, Color(0.06, 0.12, 0.16, 0.90))
	left.position = Vector2(22, 22)
	left.custom_minimum_size = Vector2(360, 0)
	var objectives := _vbox(left, 5)
	_hud_role = _label("", 15, CORAL)
	objectives.add_child(_hud_role)
	_hud_objective = _label("", 20, CREAM, true)
	_hud_objective.custom_minimum_size.x = 320
	objectives.add_child(_hud_objective)
	_hud_progress_text = _label("", 16, MINT)
	objectives.add_child(_hud_progress_text)
	_hud_progress = ProgressBar.new()
	_hud_progress.show_percentage = false
	_hud_progress.custom_minimum_size.y = 9
	objectives.add_child(_hud_progress)
	var clock_panel := _panel(_hud, Color(0.06, 0.12, 0.16, 0.9))
	clock_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	clock_panel.offset_left = -166
	clock_panel.offset_top = 22
	clock_panel.offset_right = -22
	clock_panel.offset_bottom = 118
	var clock_box := _vbox(clock_panel, 2)
	var clock_label := _label("RONDA", 13, MUTED)
	clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	clock_box.add_child(clock_label)
	_hud_time = _label("00:00", 32)
	_hud_time.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	clock_box.add_child(_hud_time)
	_task_panel = _panel(_hud, Color(0.06, 0.12, 0.16, 0.94))
	_task_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT)
	_task_panel.offset_left = -328
	_task_panel.offset_right = -22
	_task_panel.offset_top = -100
	_task_panel.offset_bottom = 100
	var task_box := _vbox(_task_panel, 6)
	task_box.add_child(_label("TU TAREA PERSONAL", 13, CORAL))
	_task_title = _label("", 20, CREAM, true)
	_task_title.custom_minimum_size.x = 265
	task_box.add_child(_task_title)
	_task_time = _label("", 22, MINT)
	task_box.add_child(_task_time)
	_task_progress = ProgressBar.new()
	_task_progress.show_percentage = false
	_task_progress.custom_minimum_size.y = 10
	task_box.add_child(_task_progress)
	_task_detail = _label("", 14, MUTED, true)
	task_box.add_child(_task_detail)
	var bottom := _panel(_hud, Color(0.06, 0.12, 0.16, 0.9))
	bottom.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	bottom.offset_left = 22
	bottom.offset_right = -22
	bottom.offset_top = -131
	bottom.offset_bottom = -22
	var hints := _vbox(bottom, 4)
	_hud_state = _label("", 20, MINT, true)
	hints.add_child(_hud_state)
	_hud_tool = _label("", 16, CORAL, true)
	hints.add_child(_hud_tool)
	_hud_hint = _label("", 14, CREAM, true)
	hints.add_child(_hud_hint)
	_reticle = _label("·", 38, CREAM)
	_reticle.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_reticle.position -= Vector2(9, 25)
	_hud.add_child(_reticle)
	_set_mouse_ignore_recursive(_hud)


func _set_mouse_ignore_recursive(node: Node) -> void:
	if node is Control:
		node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for child: Node in node.get_children():
		_set_mouse_ignore_recursive(child)


func _build_results() -> void:
	_results = _menu_surface()
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_results.add_child(center)
	var panel := _panel(center)
	panel.custom_minimum_size.x = 620
	var box := _vbox(panel, 18)
	box.add_child(_label("FIN DE LA RONDA", 14, CORAL))
	_result_title = _label("", 40, CREAM, true)
	box.add_child(_result_title)
	_result_subtitle = _label("", 20, MINT, true)
	box.add_child(_result_subtitle)
	_result_stats = _label("", 18, MUTED, true)
	box.add_child(_result_stats)
	box.add_child(HSeparator.new())
	_rematch_button = _button("Volver a la sala · revancha", func() -> void: rematch_requested.emit(), true)
	box.add_child(_rematch_button)
	box.add_child(_button("Salir de la sala", func() -> void: leave_requested.emit()))
	_status(box)


func _build_pause() -> void:
	_pause = _menu_surface()
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_pause.add_child(center)
	var panel := _panel(center)
	panel.custom_minimum_size.x = 440
	var box := _vbox(panel, 14)
	box.add_child(_label("Un segundo…", 34))
	box.add_child(_label("La partida online sigue en marcha.", 17, MUTED))
	box.add_child(_button("Seguir jugando", func() -> void: set_pause(false), true))
	box.add_child(_button("Ajustes y controles", _open_settings))
	box.add_child(_button("Salir de la sala", func() -> void: leave_requested.emit()))
	box.add_child(_label("Si alguien se desconecta, la ronda se interrumpe sin ganador.", 15, MUTED, true))


func _build_settings() -> void:
	_settings = _menu_surface()
	var frame := _vbox(_margin(_settings, 30), 14)
	var heading := HBoxContainer.new()
	frame.add_child(heading)
	var title := _label("A tu manera", 32)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	heading.add_child(_button("Listo · guardar y volver", _close_settings, true))
	var columns := HBoxContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	columns.add_theme_constant_override("separation", 22)
	frame.add_child(columns)
	var preferences_panel := _panel(columns)
	preferences_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var preference_scroll := _scroll(preferences_panel)
	var box := _vbox(preference_scroll, 18)
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_child(_label("Cámara y sonido", 24))
	_add_slider(box, "human", "Sensibilidad · humano", Prefs.human_sensitivity * 1000.0, 0.5, 8.0, 0.1)
	_add_slider(box, "mosquito", "Sensibilidad · mosquito", Prefs.mosquito_sensitivity * 1000.0, 0.5, 8.0, 0.1)
	_add_slider(box, "volume", "Volumen general", Prefs.master_volume * 100.0, 0.0, 100.0, 1.0)
	var invert := CheckButton.new()
	invert.text = "Invertir eje vertical"
	invert.button_pressed = Prefs.invert_y
	invert.toggled.connect(func(value: bool) -> void: Prefs.invert_y = value)
	box.add_child(invert)
	var pulse := CheckButton.new()
	pulse.text = "Pulso suave de mi marca"
	pulse.button_pressed = Prefs.marker_pulse
	pulse.toggled.connect(func(value: bool) -> void: Prefs.marker_pulse = value)
	box.add_child(pulse)
	box.add_child(_label("Tu marca usa forma y color. Solo vos podés verla; no se muestra un contador de cambio de zona.", 16, MUTED, true))
	box.add_child(_label("DEFENSA PROPIA", 14, CORAL))
	box.add_child(_label("Usá la tecla de defensa propia y orientá la mirada: al frente o arriba para cabeza y hombros; algo hacia abajo para torso; bien abajo para piernas. Las zonas de espalda necesitan ayuda de otro humano.", 16, CREAM, true))
	var controls_panel := _panel(columns)
	controls_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var controls := _vbox(controls_panel, 10)
	controls.add_child(_label("Teclas y botones", 24))
	_binding_notice = _label("Elegí un control y pulsá la nueva tecla o botón del ratón. Esc cancela. Las acciones de roles distintos pueden compartir tecla.", 15, MUTED, true)
	controls.add_child(_binding_notice)
	var scroll := _scroll(controls)
	var bindings := _vbox(scroll, 5)
	bindings.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for action: String in Prefs.ACTION_NAMES:
		var row := HBoxContainer.new()
		bindings.add_child(row)
		var label := _label(Prefs.ACTION_NAMES[action], 16, CREAM, true)
		label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(label)
		var button := _button(Prefs.binding_text(action), _begin_binding.bind(action))
		button.custom_minimum_size.x = 130
		row.add_child(button)
		_binding_buttons[action] = button
	controls.add_child(_button("Restablecer teclas", func() -> void:
		Prefs.reset_bindings()
		_refresh_binding_buttons()
	))
	frame.add_child(_label("Los ajustes se guardan en esta computadora. El menú no detiene una ronda online.", 14, MUTED))


func _add_slider(parent: Node, key: String, label_text: String, value: float, minimum: float, maximum: float, increment: float) -> void:
	var box := _vbox(parent, 5)
	var label := _label("", 17)
	box.add_child(label)
	var slider := HSlider.new()
	slider.min_value = minimum
	slider.max_value = maximum
	slider.step = increment
	slider.value = value
	slider.custom_minimum_size.y = 28
	box.add_child(slider)
	label.text = "%s · %.1f" % [label_text, value]
	slider.value_changed.connect(func(number: float) -> void:
		label.text = "%s · %.1f" % [label_text, number]
		match key:
			"human": Prefs.human_sensitivity = number / 1000.0
			"mosquito": Prefs.mosquito_sensitivity = number / 1000.0
			"volume":
				Prefs.master_volume = number / 100.0
				Prefs.apply_audio()
	)
	_settings_controls[key] = slider


func _begin_binding(action: String) -> void:
	_binding_action = action
	_refresh_binding_buttons()
	_binding_notice.text = "Nuevo control para «%s»: pulsá una tecla o botón. Esc cancela." % Prefs.ACTION_NAMES[action]
	_binding_buttons[action].text = "Pulsá una tecla…"


func _input(event: InputEvent) -> void:
	if _binding_action.is_empty() or not _settings_open:
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE:
			_binding_action = ""
		else:
			Prefs.bind_action(_binding_action, event)
			_binding_action = ""
		_finish_binding()
	elif event is InputEventMouseButton and event.pressed:
		Prefs.bind_action(_binding_action, event)
		_binding_action = ""
		_finish_binding()


func _finish_binding() -> void:
	get_viewport().set_input_as_handled()
	_refresh_binding_buttons()
	_binding_notice.text = "Controles guardados. Elegí otro para cambiarlo. Las acciones de roles distintos pueden compartir tecla."


func _refresh_binding_buttons() -> void:
	for action: String in _binding_buttons:
		_binding_buttons[action].text = Prefs.binding_text(action)


func _open_settings() -> void:
	_settings_open = true
	_settings.show()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _close_settings() -> void:
	_binding_action = ""
	Prefs.save_settings()
	_settings_open = false
	_settings.hide()
	if _screen == "game" and not _paused:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func show_home() -> void:
	_build()
	_set_screen("home")
	_config_signature = ""
	_roster_signature = ""
	_owner = false
	_local_id = 0
	_input_release()


func show_status(message: String) -> void:
	_build()
	for label: Label in _status_labels:
		label.text = message


func show_lobby(data: Dictionary, local_id: int) -> void:
	_build()
	var changed_screen: bool = _screen != "lobby"
	_set_screen("lobby")
	_local_id = local_id
	_owner = int(data.get("owner", 0)) == local_id
	var code: String = str(data.get("code", "—"))
	_code_label.text = "SALA  " + code
	_code_label.set_meta("code", code)
	var players: Dictionary = data.get("players", {})
	var local_player: Dictionary = players.get(local_id, players.get(str(local_id), {}))
	_ready_value = bool(local_player.get("ready", false))
	_ready_button.text = "Listo ✓ · cancelar" if _ready_value else "Estoy listo"
	_human_button.text = "Humano ✓" if local_player.get("role", "") == "human" else "Humano"
	_mosquito_button.text = "Mosquito ✓" if local_player.get("role", "") == "mosquito" else "Mosquito"
	var signature: String = var_to_str(players)
	if signature != _roster_signature:
		_roster_signature = signature
		_update_roster(players, local_id, int(data.get("owner", 0)))
	var config: Dictionary = data.get("config", FALLBACK_CONFIG)
	var config_signature: String = var_to_str(config)
	if config_signature != _config_signature or changed_screen:
		_config_signature = config_signature
		_config = FALLBACK_CONFIG.duplicate(true)
		_config.merge(config, true)
		_updating_config = true
		_mode.select(maxi(0, MODES.find(str(_config.get("mode", "blood")))))
		for key: String in _fields:
			_fields[key].value = _config.get(key, FALLBACK_CONFIG.get(key, 0))
		_updating_config = false
		_update_dependent_ranges()
		_update_mode_fields()
		_config_apply.text = "Aplicar reglas" if _owner else "Solo el anfitrión cambia las reglas"
	_mode.disabled = not _owner
	for key: String in _fields:
		_fields[key].editable = _owner
	_config_apply.disabled = not _owner
	_start_button.disabled = not _owner or not bool(data.get("can_start", false))
	_start_button.text = "Empezar ronda" if _owner else "Esperando al anfitrión"
	_start_reason.text = str(data.get("start_reason", ""))
	if _start_reason.text.is_empty():
		_start_reason.text = "Todos listos. ¡Que empiece la noche!" if data.get("can_start", false) else "Al menos 2 mosquitos por humano y todos listos para empezar."
	if changed_screen:
		_input_release()


func _update_roster(players: Dictionary, local_id: int, owner_id: int) -> void:
	for child: Node in _roster.get_children():
		_roster.remove_child(child)
		child.queue_free()
	var humans: int = 0
	var mosquitoes: int = 0
	for id: Variant in players:
		var player: Dictionary = players[id]
		var human: bool = player.get("role", "") == "human"
		humans += int(human)
		mosquitoes += int(not human)
		var line: String = "%s  %s" % ["H" if human else "M", str(player.get("name", "Jugador")).left(24)]
		if int(id) == local_id:
			line += " · vos"
		if int(id) == owner_id:
			line += " · anfitrión"
		line += "   ✓" if player.get("ready", false) else "   …"
		var label := _label(line, 17, MINT if player.get("ready", false) else CREAM, true)
		_roster.add_child(label)
	_players_label.text = "%d / 5 humanos  ·  %d mosquitos\nMínimo 2 mosquitos por humano" % [humans, mosquitoes]


func show_game(snapshot: Dictionary, private_data: Dictionary, local_id: int) -> void:
	_build()
	var changed_screen: bool = _screen != "game"
	_set_screen("game")
	_local_id = local_id
	var actors: Dictionary = snapshot.get("actors", {})
	var actor: Dictionary = actors.get(local_id, actors.get(str(local_id), {}))
	var role: String = str(actor.get("role", "mosquito"))
	var human: bool = role == "human"
	var alive: bool = bool(actor.get("alive", true))
	var state: String = str(actor.get("state", "flying"))
	var config: Dictionary = snapshot.get("config", FALLBACK_CONFIG)
	var mode: String = str(config.get("mode", "blood"))
	_hud_role.text = ("HUMANO" if human else "MOSQUITO") + "  /  " + str(MODE_NAMES.get(mode, mode)).to_upper()
	_hud_time.text = _clock(float(snapshot.get("time_left", 0.0)))
	_hud_time.add_theme_color_override("font_color", CORAL if float(snapshot.get("time_left", 0.0)) < 20.0 else CREAM)
	var living_mosquitoes: int = 0
	for other: Variant in actors.values():
		if other is Dictionary and other.get("role", "") == "mosquito" and other.get("alive", false):
			living_mosquitoes += 1
	_hud_progress.visible = mode != "survival"
	match mode:
		"blood":
			var blood: float = float(snapshot.get("blood", 0.0))
			var goal: float = maxf(1.0, float(config.get("blood_goal", 80.0)))
			_hud_objective.text = "Frená la recolección de sangre" if human else "Llenen la cuota entre todos"
			_hud_progress_text.text = "Sangre: %.1f / %.0f · %d mosquitos vivos" % [blood, goal, living_mosquitoes]
			_hud_progress.value = blood / goal * 100.0
		"survival":
			_hud_objective.text = "Eliminá a todos los mosquitos" if human else "Que al menos uno llegue al final"
			_hud_progress_text.text = "%d mosquitos vivos · sin reapariciones" % living_mosquitoes
		"sleep":
			var done: int = int(snapshot.get("tasks_done", 0))
			var goal: int = maxi(1, int(snapshot.get("task_goal", config.get("task_goal", 6))))
			_hud_objective.text = "Completen las tareas de la casa" if human else "Picá para interrumpir las tareas"
			_hud_progress_text.text = "Tareas del equipo: %d / %d" % [done, goal]
			_hud_progress.value = float(done) / float(goal) * 100.0
	_task_panel.visible = human and mode == "sleep"
	_reticle.visible = human and alive
	if human:
		_hud_state.text = "¡Te están picando! Defendete o pedí ayuda." if actor.get("bitten", false) else "Cuidá tu espacio. Ayudá a tus compañeros."
		_hud_state.add_theme_color_override("font_color", CORAL if actor.get("bitten", false) else MINT)
		var tool: String = str(actor.get("tool", "hands"))
		_hud_tool.text = "EQUIPADO  " + str(TOOL_NAMES.get(tool, tool))
		var stats: Dictionary = Simulation.TOOL_STATS.get(tool, {})
		if not stats.is_empty():
			_hud_tool.text += " · alcance %.2f m · recuperación %.2f s · radio %.2f m" % [float(stats.get("reach", 0)), float(stats.get("cooldown", 0)), float(stats.get("radius", 0))]
		_hud_hint.text = "%s golpear · %s defensa propia según mirada · %s recoger/cambiar · %s soltar · %s tarea (mantener) · %s menú" % [Prefs.binding_text("attack"), Prefs.binding_text("self_swat"), Prefs.binding_text("pickup"), Prefs.binding_text("drop"), Prefs.binding_text("interact"), Prefs.binding_text("pause")]
		if mode == "sleep":
			_update_task(private_data, bool(actor.get("bitten", false)))
	else:
		var assignment: Dictionary = private_data.get("assignment", {})
		var lives: int = int(private_data.get("lives", actor.get("lives", 0)))
		_hud_tool.text = "TU ZONA  " + str(assignment.get("label", "Buscando zona…")) if alive else "Seguís con tu equipo hasta el resultado."
		if mode == "sleep":
			_hud_tool.text += " · TUS VIDAS: %d" % lives
		_hud_state.add_theme_color_override("font_color", MINT)
		if not alive or state == "dead":
			if mode == "sleep" and lives > 0:
				_hud_state.text = "Reaparecés en %.1f s · te quedan %d vidas" % [float(private_data.get("respawn_left", 0.0)), lives]
				_hud_hint.text = "Esperá para volver a volar. · %s menú" % Prefs.binding_text("pause")
			else:
				_hud_state.text = "Eliminado · volvés en la próxima ronda"
				_hud_hint.text = "%s menú · Esperá el resultado de tu equipo." % Prefs.binding_text("pause")
		elif state == "biting":
			_hud_state.text = "Picando · estás prendido al cuerpo"
			_hud_hint.text = "Pulsá %s para desprenderte y recibir otra zona. Soltar la tecla no te desprende. · %s menú" % [Prefs.binding_text("bite"), Prefs.binding_text("pause")]
		elif state == "perched":
			_hud_state.text = "Posado · seguís siendo visible"
			_hud_hint.text = "%s volver a volar · %s picar en tu marca · %s menú" % [Prefs.binding_text("perch"), Prefs.binding_text("bite"), Prefs.binding_text("pause")]
		else:
			_hud_state.text = "Buscá tu marca y acercate para picar"
			_hud_hint.text = "%s/%s/%s/%s moverte · %s subir · %s bajar · %s picar/desprenderte · %s posarte · %s menú" % [Prefs.binding_text("move_forward"), Prefs.binding_text("move_left"), Prefs.binding_text("move_back"), Prefs.binding_text("move_right"), Prefs.binding_text("ascend"), Prefs.binding_text("descend"), Prefs.binding_text("bite"), Prefs.binding_text("perch"), Prefs.binding_text("pause")]
	if changed_screen and not _paused and not _settings_open:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _update_task(private_data: Dictionary, bitten: bool) -> void:
	var task: Dictionary = private_data.get("task", {})
	var failures: int = int(private_data.get("failures", 0))
	if task.is_empty():
		_task_title.text = "Un momento de calma"
		_task_time.text = "Sin tarea activa"
		_task_progress.value = 0
		_task_detail.text = "Ayudá a cuidar la casa. Tus fallos: %d. Próximo plazo: %.0f s." % [failures, float(private_data.get("deadline", 0.0))]
		return
	_task_title.text = str(task.get("name", "Tarea de la casa"))
	_task_time.text = "TU PLAZO  " + _clock(float(task.get("remaining", 0.0)))
	_task_time.add_theme_color_override("font_color", CORAL if float(task.get("remaining", 0.0)) < 8.0 else MINT)
	_task_progress.value = float(task.get("progress", 0.0)) / maxf(0.01, float(task.get("work", 5.0))) * 100.0
	_task_detail.text = "Trabajo pausado por picadura. El avance se conserva." if bitten else "Acercate al objeto indicado y mantené %s. Tus fallos: %d." % [Prefs.binding_text("interact"), failures]


func show_results(snapshot: Dictionary) -> void:
	_build()
	_set_screen("results")
	var winner: String = str(snapshot.get("winner", ""))
	match winner:
		"human", "humans": _result_title.text = "La casa está en paz."
		"mosquito", "mosquitoes": _result_title.text = "El zumbido ganó."
		_: _result_title.text = "Ronda interrumpida"
	_result_subtitle.text = "Ganaron los humanos" if winner in ["human", "humans"] else ("Ganaron los mosquitos" if winner in ["mosquito", "mosquitoes"] else "Sin ganador")
	var reason: String = str(snapshot.get("reason", ""))
	var config: Dictionary = snapshot.get("config", FALLBACK_CONFIG)
	var mode: String = str(config.get("mode", "blood"))
	var stats: String = str(MODE_NAMES.get(mode, mode)) + "\n"
	if not reason.is_empty():
		stats += reason + "\n\n"
	match mode:
		"blood": stats += "Sangre compartida: %.1f / %.0f" % [float(snapshot.get("blood", 0)), float(config.get("blood_goal", 80))]
		"sleep": stats += "Tareas del equipo: %d / %d" % [int(snapshot.get("tasks_done", 0)), int(snapshot.get("task_goal", config.get("task_goal", 6)))]
		_: stats += "Tiempo jugado: " + _clock(float(snapshot.get("elapsed", 0.0)))
	_result_stats.text = stats
	_rematch_button.disabled = not _owner
	_rematch_button.text = "Volver a la sala · revancha" if _owner else "Esperando la revancha del anfitrión"
	_input_release()


func set_pause(open: bool) -> void:
	if _screen != "game":
		if _settings_open and not open:
			_close_settings()
		return
	if _settings_open:
		_close_settings()
	_paused = open
	_pause.visible = open
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if open else Input.MOUSE_MODE_CAPTURED


func is_menu_open() -> bool:
	return _screen != "game" or _paused or _settings_open


func _input_release() -> void:
	_paused = false
	_pause.hide()
	_settings_open = false
	_settings.hide()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _set_screen(screen: String) -> void:
	if _screen != screen:
		_paused = false
		_pause.hide()
		_settings_open = false
		_settings.hide()
	_screen = screen
	_home.visible = screen == "home"
	_lobby.visible = screen == "lobby"
	_hud.visible = screen == "game"
	_results.visible = screen == "results"


func _clock(seconds: float) -> String:
	var whole: int = maxi(0, int(ceilf(seconds)))
	return "%02d:%02d" % [whole / 60, whole % 60]
