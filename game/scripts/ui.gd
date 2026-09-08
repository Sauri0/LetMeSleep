extends CanvasLayer
## Spanish menus and private local HUD. The server remains the source of truth.

signal connect_requested(address: String, port: int, player_name: String, code: String, create: bool)
signal local_server_requested
signal host_requested(player_name: String, local_port: int)
signal cancel_connection_requested
signal retry_connection_requested
signal cosmetics_changed(data: Dictionary)
signal ui_sound_requested(kind: String)
signal screen_changed(screen: String)
signal preview_requested(role: String, appearance: Dictionary)
signal preview_closed
signal walk_requested
signal escape_requested
signal ready_requested(value: bool)
signal config_requested(config: Dictionary)
signal start_requested
signal rematch_requested
signal leave_requested
signal practice_requested(role: String, mode: String)
signal practice_restart_requested

const Prefs = preload("res://scripts/preferences.gd")
const Simulation = preload("res://scripts/simulation.gd")
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const InvitationCodec = preload("res://scripts/invitation.gd")
const AvatarPreview = preload("res://scripts/avatar_preview.gd")
const TITLE_FONT = preload("res://assets/fonts/Bangers/Bangers-Regular.ttf")
const BODY_FONT = preload("res://assets/fonts/AtkinsonHyperlegible/AtkinsonHyperlegible-Regular.ttf")
const BOLD_FONT = preload("res://assets/fonts/AtkinsonHyperlegible/AtkinsonHyperlegible-Bold.ttf")
const INK := Color("232532")
const PAPER := Color("fff5da")
const PANEL := PAPER
const CREAM := INK
const MUTED := Color("5c6068")
const CORAL := Color("ef6652")
const MINT := Color("50bea9")
const SUN := Color("ffcf47")
const MODES := ["blood", "survival", "sleep"]
const MODE_NAMES := {"blood": "Recolección de sangre", "survival": "Supervivencia", "sleep": "Tareas"}
const TOOL_NAMES := {"hands": "Manos · palmadas", "swatter": "Matamoscas", "racket": "Raqueta eléctrica", "newspaper": "Diario enrollado", "broom": "Escoba", "slipper": "Pantufla"}
const FALLBACK_CONFIG: Dictionary = Simulation.DEFAULT_CONFIG
const CONFIG_FIELDS := [
	["human_count", "Humanos por ronda", 1, 5, 1, "all"],
	["round_seconds", "Duración de ronda · s", 30, 180, 1, "all"],
	["rotation_seconds", "Cambio de zona · s", 4, 40, 1, "all"],
	["blood_goal", "Cuota compartida", 1, 1000, 1, "blood"],
	["task_interval", "Frecuencia de tareas · s", Simulation.TASK_TRAVEL_RESERVE + 1.5, 60, 0.5, "sleep"],
	["task_deadline", "Plazo inicial por tarea · s", Simulation.TASK_TRAVEL_RESERVE + 1.0, 59.5, 0.5, "sleep"],
	["task_work", "Trabajo por tarea · s", 1, 8, 1, "sleep"],
	["task_penalty", "Menos plazo por fallo · s", 0.5, 8, 0.5, "sleep"],
	["task_floor", "Plazo mínimo · s", Simulation.TASK_TRAVEL_RESERVE + 1.0, 59.5, 0.5, "sleep"],
	["task_goal", "Meta de tareas · 0 = auto", 0, 100, 1, "sleep"],
]

class AttackCue extends Control:
	var candidate := false
	var blocked := false
	var hit_until := 0.0
	func update_state(opportunity: bool, obstruction: bool) -> void:
		if candidate==opportunity and blocked==obstruction: return
		candidate=opportunity
		blocked=obstruction
		queue_redraw()
	func confirm_hit() -> void:
		hit_until=float(Time.get_ticks_msec())/1000.0+.20
		queue_redraw()
		get_tree().create_timer(.21).timeout.connect(queue_redraw)
	func _draw() -> void:
		var center := size*.5
		if float(Time.get_ticks_msec())/1000.0<hit_until:
			for angle: float in [PI*.25,PI*.75,PI*1.25,PI*1.75]:
				draw_line(center+Vector2.from_angle(angle)*6,center+Vector2.from_angle(angle)*11,Color("fff5da"),2,true)
		elif candidate:
			for angle: float in [0.0,PI*.5,PI,PI*1.5]:
				draw_arc(center,10,angle-.22,angle+.22,5,Color("ffcf47"),1.5,true)
		elif blocked:
			draw_line(center+Vector2(-4,9),center+Vector2(4,9),Color("c8b5a0"),1.2,true)

class ComicBackdrop extends Control:
	var kind: String = "dots"
	var title_font: Font
	const TINT := Color("232532")
	const PAGE := Color("fff5da")
	func _ready() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		resized.connect(queue_redraw)
	func _ellipse(center: Vector2, radii: Vector2, angle: float, fill: Color) -> void:
		var points := PackedVector2Array()
		for index: int in range(48):
			var t: float = TAU * float(index) / 48.0
			points.append(center + Vector2(cos(t) * radii.x, sin(t) * radii.y).rotated(angle))
		draw_colored_polygon(points, fill)
		points.append(points[0])
		draw_polyline(points, TINT, 4.0, true)
	func _draw() -> void:
		if size.x <= 0 or size.y <= 0:
			return
		draw_set_transform(Vector2.ZERO, 0.0, Vector2(size.x / 1280.0, size.y / 720.0))
		draw_rect(Rect2(0, 0, 1280, 720), PAGE)
		if kind == "home":
			var division := PackedVector2Array([Vector2(702, 0), Vector2(1280, 0), Vector2(1280, 720), Vector2(548, 720)])
			draw_colored_polygon(division, Color("50bea9"))
			draw_line(Vector2(702, 0), Vector2(548, 720), TINT, 6.0)
		else:
			for ray: int in range(16):
				var start: float = TAU * float(ray) / 16.0
				var center := Vector2(640, 350)
				draw_colored_polygon(PackedVector2Array([center, center + Vector2.from_angle(start) * 1000, center + Vector2.from_angle(start + 0.16) * 1000]), Color("ffde76"))
		for y: int in range(15, 720, 22):
			for x: int in range(15, 1280, 22):
				if kind != "home" or float(x) > 708.0 - float(y) * 0.21:
					draw_circle(Vector2(x, y), 1.7, Color(0.14, 0.15, 0.20, 0.16))
		if kind == "home":
			# Original drawn window + mosquito; no raster asset or third-party art.
			draw_rect(Rect2(59, 490, 119, 147), TINT)
			draw_rect(Rect2(65, 496, 107, 135), Color("608aaf"))
			draw_circle(Vector2(101, 527), 19, PAGE)
			draw_circle(Vector2(110, 519), 18, Color("608aaf"))
			draw_line(Vector2(119, 495), Vector2(119, 632), TINT, 5)
			draw_line(Vector2(64, 566), Vector2(174, 566), TINT, 5)
			draw_line(Vector2(49, 641), Vector2(188, 641), TINT, 7)
			_ellipse(Vector2(304, 528), Vector2(53, 21), -0.72, PAGE)
			_ellipse(Vector2(354, 514), Vector2(56, 22), -0.20, PAGE)
			_ellipse(Vector2(327, 568), Vector2(59, 33), 0.16, Color("ef6652"))
			draw_line(Vector2(310, 539), Vector2(300, 595), TINT, 5)
			draw_line(Vector2(334, 537), Vector2(324, 600), TINT, 5)
			draw_circle(Vector2(380, 554), 25, TINT)
			draw_circle(Vector2(380, 552), 21, Color("ffcf47"))
			draw_circle(Vector2(389, 547), 8, PAGE)
			draw_circle(Vector2(392, 546), 4, TINT)
			draw_line(Vector2(400, 554), Vector2(454, 535), TINT, 5)
			for index: int in range(3):
				var base := Vector2(309 + index * 23, 588)
				draw_polyline(PackedVector2Array([base, base + Vector2(-18, 27), base + Vector2(-42, 30)]), TINT, 4.0, true)
			draw_arc(Vector2(235, 551), 31, 1.5, 4.5, 24, TINT, 3.0, true)
			if title_font != null:
				draw_string(title_font, Vector2(425, 506), "¡BZZ!", HORIZONTAL_ALIGNMENT_LEFT, -1, 42, TINT)

var _root: Control
var _home: Control
var _lobby: Control
var _hud: Control
var _results: Control
var _pause: Control
var _settings: Control
var _customization: Control
var _lobby_panel: PanelContainer
var _lobby_walking_panel: PanelContainer
var _lobby_walking_label: Label
var _lobby_walking: bool = false
var _lobby_player_count: int = 0
var _custom_role: String = "human"
var _custom_role_buttons: Dictionary = {}
var _color_buttons: Array[Button] = []
var _accessory_select: OptionButton
var _custom_title: Label
var _custom_caption: Label
var _home_menu: VBoxContainer
var _connection_form: VBoxContainer
var _connection_open: bool = false
var _connection_mode_create: bool = true
var _advanced_open: bool = false
var _address_box: VBoxContainer
var _port_row: HBoxContainer
var _code_box: VBoxContainer
var _advanced_help: Label
var _local_server_button: Button
var _host_port_edit: SpinBox
var _host_port_row: HBoxContainer
var _existing_server: CheckButton
var _connection_busy := false
var _connection_cancel: Button
var _connection_retry: Button
var _connection_feedback: Label
var _connect_submit: Button
var _connection_title: Label
var _advanced_button: Button
var _home_default_focus: Button
var _settings_default_focus: Button
var _last_settings_focus: Control
var _practice: bool = false
var _practice_screen: Control
var _practice_role: String = "human"
var _practice_mode: String = "blood"
var _practice_role_buttons: Dictionary = {}
var _practice_mode_buttons: Dictionary = {}
var _practice_detail: Label
var _practice_start_button: Button
var _practice_banner: Label
var _result_leave: Button
var _pause_leave: Button
var _invitation_box: VBoxContainer
var _invitation_edit: LineEdit
var _invite_settings: Control
var _invite_settings_open: bool = false
var _invite_address_edit: LineEdit
var _invite_port_edit: SpinBox
var _invite_lan: OptionButton
var _invite_status: Label
var _invite_scope: OptionButton
var _invite_help: Label
var _room_join_scope := ""
var _room_join_address: String = ""
var _room_join_port: int = 27840
var _comic_feedback: Label
var _comic_feedback_tween: Tween
var _previous_bitten: bool = false
var _previous_swing: float = 0.0
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
var _hud_role: Label
var _hud_objective: Label
var _hud_time: Label
var _hud_progress: ProgressBar
var _hud_progress_text: Label
var _hud_state: Label
var _hud_hint: Label
var _focus_progress: ProgressBar
var _focus_key_pattern: RegEx
var _hud_tool: Label
var _task_panel: PanelContainer
var _task_title: Label
var _task_time: Label
var _task_detail: Label
var _task_progress: ProgressBar
var _reticle: Label
var _attack_cue: AttackCue
var _door_hint: Label
var _result_title: Label
var _result_subtitle: Label
var _result_stats: Label
var _rematch_button: Button
var _binding_notice: Label
var _settings_controls: Dictionary = {}
var _avatar_preview: SubViewportContainer
var _custom_category := "outfit"
var _category_buttons: Dictionary = {}
var _custom_options: VBoxContainer
var _custom_option_buttons: Array[Button] = []
var _custom_view_buttons: Dictionary = {}
var _thumbnail_cache: Dictionary = {}
var _ui_error_serial: int = 0
var _connection_phase: String = "idle"
var _help: Control
var _help_open: bool = false
var _help_body: Label
var _help_close: Button
var _help_return_focus: Control
var _help_role: String = "human"
var _help_mode: String = "blood"
var _hud_help: Label
var _hud_context: PanelContainer
var _hud_equipment: Label
var _attack_recovery: ProgressBar
var _hud_context_key := ""
var _hud_context_until := 0.0
var _attack_feedback_key := ""


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
	_build_help()
	_build_customization()
	_build_practice()
	_build_settings()
	_build_invite_settings()


func _make_theme() -> Theme:
	var theme := Theme.new()
	theme.default_font = BODY_FONT
	theme.default_font_size = 18
	for type: String in ["Label", "Button", "OptionButton", "CheckButton", "LineEdit", "PopupMenu"]:
		theme.set_color("font_color", type, INK)
		theme.set_color("font_hover_color", type, INK)
		theme.set_color("font_pressed_color", type, INK)
		theme.set_color("font_focus_color", type, INK)
		theme.set_color("font_disabled_color", type, MUTED)
	theme.set_font("font", "Button", TITLE_FONT)
	theme.set_font_size("font_size", "Button", 27)
	theme.set_font("font", "OptionButton", BOLD_FONT)
	theme.set_font("font", "CheckButton", BOLD_FONT)
	theme.set_font("font", "PopupMenu", BOLD_FONT)
	theme.set_color("font_placeholder_color", "LineEdit", MUTED)
	theme.set_color("caret_color", "LineEdit", INK)
	for kind: String in ["Button", "OptionButton", "CheckButton"]:
		theme.set_stylebox("normal", kind, _style(PAPER, 4, 16, 10))
		theme.set_stylebox("hover", kind, _style(SUN, 4, 16, 10))
		theme.set_stylebox("pressed", kind, _style(MINT.lightened(0.22), 4, 16, 10))
		theme.set_stylebox("disabled", kind, _style(Color("e3dbc7"), 4, 16, 10))
		theme.set_stylebox("focus", kind, _outline(CORAL))
	for state: String in ["normal", "focus"]:
		var entry_style := _style(Color("fffdf3"), 3, 12, 10)
		if state == "focus":
			entry_style.border_color = CORAL
			entry_style.set_border_width_all(4)
		theme.set_stylebox(state, "LineEdit", entry_style)
	theme.set_stylebox("panel", "PopupMenu", _style(PAPER, 3, 10, 10))
	theme.set_stylebox("hover", "PopupMenu", _style(SUN, 2, 8, 5))
	theme.set_stylebox("background", "ProgressBar", _style(Color("e5dac0"), 2, 0, 0))
	theme.set_stylebox("fill", "ProgressBar", _style(CORAL, 2, 0, 0))
	theme.set_stylebox("slider", "HSlider", _style(Color("e5dac0"), 2, 0, 4))
	theme.set_stylebox("grabber_area", "HSlider", _style(MINT, 2, 0, 4))
	theme.set_stylebox("grabber_area_highlight", "HSlider", _style(SUN, 2, 0, 4))
	var slider_handle := _ink_icon('<rect x="2" y="1" width="14" height="20" rx="2" fill="#232532"/><path d="M7 5v12m4-12v12" stroke="#fff5da" stroke-width="2"/>', 18, 22)
	theme.set_icon("grabber", "HSlider", slider_handle)
	theme.set_icon("grabber_highlight", "HSlider", slider_handle)
	theme.set_icon("updown", "SpinBox", _ink_icon('<path d="M1 8L7 2l6 6M1 15l6 6 6-6" fill="none" stroke="#232532" stroke-width="3"/>', 14, 24))
	theme.set_icon("arrow", "OptionButton", _ink_icon('<path d="M2 3l5 5 5-5" fill="none" stroke="#232532" stroke-width="3"/>', 14, 12))
	theme.set_constant("separation", "VBoxContainer", 10)
	theme.set_constant("separation", "HBoxContainer", 12)
	return theme


func _ink_icon(body: String, width: int, height: int) -> ImageTexture:
	var image := Image.new()
	image.load_svg_from_string('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">%s</svg>' % [width, height, body])
	return ImageTexture.create_from_image(image)


func _style(color: Color, radius: int = 14, horizontal: int = 20, vertical: int = 20) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = PAPER if color.a > 0.0 and color.v < 0.5 else color
	style.set_corner_radius_all(mini(radius, 5))
	if color.a > 0.0:
		style.border_color = INK
		style.set_border_width_all(3)
	style.content_margin_left = horizontal
	style.content_margin_right = horizontal
	style.content_margin_top = vertical
	style.content_margin_bottom = vertical
	return style


func _outline(color: Color) -> StyleBoxFlat:
	var style := _style(Color(0, 0, 0, 0), 10, 0, 0)
	style.border_color = color
	style.set_border_width_all(4)
	style.expand_margin_left = 3
	style.expand_margin_right = 3
	style.expand_margin_top = 3
	style.expand_margin_bottom = 3
	return style


func _label(text_value: String, size: int = 18, color: Color = CREAM, wrap: bool = false) -> Label:
	var label := Label.new()
	label.text = text_value
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", _text_ink(color))
	if size >= 24:
		label.add_theme_font_override("font", TITLE_FONT)
	elif size <= 16:
		label.add_theme_font_override("font", BOLD_FONT)
	if wrap:
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return label


func _text_ink(color: Color) -> Color:
	if color == MINT:
		return Color("166452")
	if color == CORAL:
		return Color("a9362c")
	return color


func _button(text_value: String, callback: Callable, accent: bool = false) -> Button:
	var button := Button.new()
	button.text = text_value
	button.focus_mode = Control.FOCUS_ALL
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.pressed.connect(func() -> void:
		var previous_error := _ui_error_serial
		callback.call()
		# Validation errors have their own cue, never two sounds for one click.
		if _ui_error_serial == previous_error:
			ui_sound_requested.emit("confirm" if accent else "select")
	)
	var base: Color = SUN if accent else PAPER
	var normal := _style(base, 4, 16, 9)
	normal.shadow_color = INK
	normal.shadow_size = 0
	normal.shadow_offset = Vector2(4, 5)
	normal.shadow_size = 4
	button.add_theme_stylebox_override("normal", normal)
	button.add_theme_stylebox_override("hover", _style(MINT.lightened(0.28) if accent else SUN, 4, 16, 9))
	button.add_theme_stylebox_override("pressed", _style(CORAL.lightened(0.28), 4, 16, 9))
	for name: String in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(name, INK)
	return button


func _panel(parent: Node, color: Color = PANEL) -> PanelContainer:
	var panel := PanelContainer.new()
	var style := _style(color, 5, 20, 18)
	style.shadow_color = INK
	style.shadow_size = 5
	style.shadow_offset = Vector2(5, 6)
	panel.add_theme_stylebox_override("panel", style)
	parent.add_child(panel)
	return panel


func _small_button(text_value: String, callback: Callable, accent: bool = false) -> Button:
	var button := _button(text_value, callback, accent)
	button.add_theme_font_override("font", BOLD_FONT)
	button.add_theme_font_size_override("font_size", 15)
	button.add_theme_stylebox_override("normal", _style(SUN if accent else PAPER, 3, 10, 7))
	button.add_theme_stylebox_override("hover", _style(MINT.lightened(0.25), 3, 10, 7))
	button.add_theme_stylebox_override("pressed", _style(CORAL.lightened(0.3), 3, 10, 7))
	button.add_theme_stylebox_override("disabled", _style(Color("e3dbc7"), 3, 10, 7))
	return button


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
	_add_comic_backdrop(surface, "rays")
	return surface


func _add_comic_backdrop(parent: Control, kind: String) -> void:
	var backdrop := ComicBackdrop.new()
	backdrop.kind = kind
	backdrop.title_font = TITLE_FONT
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	parent.add_child(backdrop)


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
	_home = _full_control(_root)
	_add_comic_backdrop(_home, "home")
	var columns := HBoxContainer.new()
	columns.add_theme_constant_override("separation", 62)
	_margin(_home, 46).add_child(columns)
	var intro := _vbox(columns, 16)
	intro.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	intro.size_flags_stretch_ratio = 1.05
	intro.add_child(_label("EDICIÓN NOCTURNA / N.º 05", 15, INK))
	var logo := _label("LET ME\nSLEEP", 112, SUN)
	logo.add_theme_color_override("font_outline_color", INK)
	logo.add_theme_constant_override("outline_size", 14)
	logo.add_theme_color_override("font_shadow_color", INK)
	logo.add_theme_constant_override("shadow_offset_x", 5)
	logo.add_theme_constant_override("shadow_offset_y", 6)
	intro.add_child(logo)
	intro.add_child(_label("HUMANOS CONTRA MOSQUITOS", 27, INK))
	var details := HBoxContainer.new()
	details.add_theme_constant_override("separation", 20)
	intro.add_child(details)
	details.add_child(_label("3 MODOS", 13, MUTED))
	details.add_child(_label("ROLES POR SORTEO", 13, MUTED))
	details.add_child(_label("DESDE 1v1", 13, MUTED))
	var space := Control.new()
	space.size_flags_vertical = Control.SIZE_EXPAND_FILL
	intro.add_child(space)
	intro.add_child(_label("PROTOTIPO %s  ·  WINDOWS" % str(ProjectSettings.get_setting("application/config/version","0.6.0")), 13, MUTED))
	var card := _panel(columns, Color(0.10, 0.21, 0.25, 0.96))
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var card_box := _vbox(card, 10)
	var scroll := _scroll(card_box)
	var content := _vbox(scroll, 8)
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_home_menu = _vbox(content, 14)
	_home_menu.add_child(_label("¿QUÉ SE ARMA HOY?", 38))
	_home_default_focus = _button("PRÁCTICA", _open_practice, true)
	_home_menu.add_child(_home_default_focus)
	_home_menu.add_child(_label("Entrá a probar con rivales automáticos.", 14, MUTED, true))
	_home_menu.add_child(HSeparator.new())
	_home_menu.add_child(_button("CREAR SALA", _open_connection.bind(true)))
	_home_menu.add_child(_button("UNIRME CON INVITACIÓN", _open_connection.bind(false)))
	_home_menu.add_child(_button("TU PINTA", _open_customization))
	_home_menu.add_child(_small_button("Ajustes y controles", _open_settings))
	_home_menu.add_child(_small_button("Salir del juego", func() -> void:
		get_tree().root.propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST)
	))
	_home_menu.add_child(_label("Tab y flechas para navegar · Enter para elegir", 13, MUTED, true))
	_connection_form = _vbox(content, 10)
	_connection_form.add_child(_small_button("← Volver", _close_connection))
	_connection_title = _label("", 27)
	_connection_form.add_child(_connection_title)
	_connection_form.add_child(_label("TU NOMBRE", 13, CORAL))
	_name_edit = LineEdit.new()
	_name_edit.max_length = 24
	_name_edit.placeholder_text = "Cómo te dicen tus amigos"
	_name_edit.text = Prefs.player_name
	_connection_form.add_child(_name_edit)
	_invitation_box = _vbox(_connection_form, 5)
	_invitation_box.add_child(_label("INVITACIÓN", 13, INK))
	_invitation_edit = LineEdit.new()
	_invitation_edit.max_length = 1024
	_invitation_edit.placeholder_text = "Pegá acá la invitación DD3-…"
	_invitation_edit.text = Prefs.invitation
	_invitation_box.add_child(_invitation_edit)
	_address_box = _vbox(_connection_form, 5)
	_address_box.add_child(_label("DIRECCIÓN DEL ANFITRIÓN", 13, CORAL))
	_address_edit = LineEdit.new()
	_address_edit.text = Prefs.server_address
	_address_edit.placeholder_text = "IP o nombre del servidor"
	_address_edit.tooltip_text = "En esta PC: 127.0.0.1. En otra casa: dirección del anfitrión."
	_address_box.add_child(_address_edit)
	_code_box = _vbox(_connection_form, 5)
	_code_box.add_child(_label("CÓDIGO DE SALA", 13, CORAL))
	_code_edit = LineEdit.new()
	_code_edit.max_length = 12
	_code_edit.placeholder_text = "Ej.: ABC123"
	_code_edit.text = Prefs.room_code
	_code_box.add_child(_code_edit)
	_local_server_button = _button("1 · Encender servidor en esta PC", func() -> void:
		_address_edit.text = "127.0.0.1"
		_port_edit.value = 27840
		local_server_requested.emit()
	)
	_local_server_button.tooltip_text = "El servidor aloja la sala. Queda en esta PC mientras jugás."
	_connection_form.add_child(_local_server_button)
	_local_server_button.hide()
	_connect_submit = _button("Crear sala", func() -> void: _request_connection(_connection_mode_create), true)
	_connection_form.add_child(_connect_submit)
	_advanced_button = _small_button("Opciones de conexión  ▾", _toggle_connection_options)
	_connection_form.add_child(_advanced_button)
	_existing_server = CheckButton.new()
	_existing_server.text = "Usar un servidor ya abierto"
	_existing_server.toggled.connect(func(_value: bool) -> void: _update_connection_options())
	_connection_form.add_child(_existing_server)
	_host_port_row = HBoxContainer.new()
	_connection_form.add_child(_host_port_row)
	var host_port_label := _label("Puerto de esta PC", 15, MUTED)
	host_port_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_host_port_row.add_child(host_port_label)
	_host_port_edit = SpinBox.new()
	_host_port_edit.min_value = 1024
	_host_port_edit.max_value = 65535
	_host_port_edit.value = Prefs.local_host_port
	_host_port_row.add_child(_host_port_edit)
	_port_row = HBoxContainer.new()
	_connection_form.add_child(_port_row)
	var port_label := _label("Puerto UDP", 15, MUTED)
	port_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	port_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_port_row.add_child(port_label)
	_port_edit = SpinBox.new()
	_port_edit.min_value = 1024
	_port_edit.max_value = 65535
	_port_edit.value = Prefs.server_port
	_port_edit.custom_minimum_size.x = 124
	_port_row.add_child(_port_edit)
	_advanced_help = _label("Sin invitación: dejá ese campo vacío y usá dirección, puerto y código. Compartir una dirección no configura tu router.", 14, MUTED, true)
	_connection_form.add_child(_advanced_help)
	_connection_feedback = _label("", 15, INK, true)
	_connection_feedback.custom_minimum_size.x = 330
	_connection_form.add_child(_connection_feedback)
	var connection_actions := HBoxContainer.new()
	_connection_form.add_child(connection_actions)
	_connection_cancel = _small_button("Cancelar", func() -> void: cancel_connection_requested.emit())
	connection_actions.add_child(_connection_cancel)
	_connection_retry = _small_button("Reintentar", func() -> void: retry_connection_requested.emit())
	connection_actions.add_child(_connection_retry)
	_connection_cancel.hide()
	_connection_retry.hide()
	_connection_form.hide()
	_status(card_box)


func _open_connection(create: bool) -> void:
	_connection_open = true
	_connection_mode_create = create
	_advanced_open = false
	_home_menu.hide()
	_connection_form.show()
	_connection_title.text = "Prepará la noche" if create else "Encontrá a tu grupo"
	_connect_submit.text = "CREAR SALA" if create else "UNIRME"
	_update_connection_options()
	_queue_focus(_name_edit)


func _close_connection() -> void:
	if _connection_busy:
		cancel_connection_requested.emit()
		show_connection_state({"phase":"cancelled"})
	_connection_open = false
	_advanced_open = false
	_connection_form.hide()
	_home_menu.show()
	_queue_focus(_home_default_focus)


func _toggle_connection_options() -> void:
	_advanced_open = not _advanced_open
	_update_connection_options()


func _update_connection_options() -> void:
	var existing: bool = _connection_mode_create and _existing_server.button_pressed
	_address_box.visible = _advanced_open and (not _connection_mode_create or existing)
	_code_box.visible = not _connection_mode_create and _advanced_open
	_invitation_box.visible = not _connection_mode_create
	_port_row.visible = _advanced_open and (not _connection_mode_create or existing)
	_host_port_row.visible = _advanced_open and _connection_mode_create and not existing
	_existing_server.visible = _advanced_open and _connection_mode_create
	_advanced_help.visible = _advanced_open
	_advanced_help.text = ("Se abrirá un servidor en esta PC. Después podés configurar la invitación para tus amigos." if not existing else "Conectate al servidor que ya está encendido en esa dirección.") if _connection_mode_create else "Sin invitación: dejá ese campo vacío y usá dirección, puerto y código."
	_local_server_button.hide()
	_advanced_button.text = "Ocultar opciones  ▴" if _advanced_open else "Opciones de conexión  ▾"


func _request_connection(create: bool) -> void:
	if _connection_busy:
		return
	var username := _name_edit.text.strip_edges()
	if username.is_empty():
		show_error("Escribí tu nombre para entrar.")
		_name_edit.grab_focus()
		return
	if create and not _existing_server.button_pressed:
		Prefs.player_name = username
		Prefs.local_host_port = int(_host_port_edit.value)
		Prefs.save_settings()
		show_connection_state({"phase":"starting_server","message":"Abriendo el servidor de esta PC…","can_cancel":true})
		host_requested.emit(username, Prefs.local_host_port)
		return
	var address := _address_edit.text.strip_edges()
	var port: int = int(_port_edit.value)
	var code := _code_edit.text.strip_edges().to_upper()
	if not create and not _invitation_edit.text.strip_edges().is_empty():
		var decoded: Dictionary = InvitationCodec.decode(_invitation_edit.text)
		if not bool(decoded.get("ok", false)):
			show_error(str(decoded.get("error", "Revisá la invitación.")))
			_invitation_edit.grab_focus()
			return
		address = str(decoded.host)
		port = int(decoded.port)
		code = str(decoded.room)
		_room_join_scope = str(decoded.get("scope", ""))
	elif not create and not _advanced_open:
		show_error("Pegá la invitación que te pasó el anfitrión.")
		_invitation_edit.grab_focus()
		return
	if address.is_empty():
		show_error("Falta la dirección del servidor.")
		if not _address_box.visible:
			_advanced_open = true
			_update_connection_options()
		_address_edit.grab_focus()
		return
	if not create and code.is_empty():
		show_error("Falta el código de sala en la conexión avanzada.")
		_code_edit.grab_focus()
		return
	Prefs.player_name = username
	Prefs.server_address = address
	Prefs.server_port = port
	Prefs.room_code = code
	_room_join_address = "" if create else address
	_room_join_port = port
	if not create:
		Prefs.invitation = _invitation_edit.text.strip_edges()
	Prefs.save_settings()
	show_status("Conectando…")
	show_connection_state({"phase":"connecting_transport","message":"Conectando con el anfitrión…","can_cancel":true})
	connect_requested.emit(address, port, username, code, create)

func show_connection_state(data: Dictionary) -> void:
	_build()
	var phase := str(data.get("phase", "idle"))
	if phase == "failed" and _connection_phase != phase:
		_ui_error_serial += 1
		ui_sound_requested.emit("error")
	_connection_phase = phase
	_connection_busy = phase in ["starting_server","resolving","connecting_transport","joining_room"]
	_connect_submit.disabled = _connection_busy
	_connection_cancel.visible = bool(data.get("can_cancel", _connection_busy))
	_connection_retry.visible = bool(data.get("can_retry", phase == "failed"))
	_connection_feedback.text = str(data.get("message", ""))
	_connection_feedback.add_theme_color_override("font_color", _text_ink(CORAL) if phase == "failed" else INK)
	if phase == "failed":
		_queue_focus(_connection_retry if _connection_retry.visible else _connect_submit)


func _build_lobby() -> void:
	# The shared 3D room remains visible. Only this sidebar intercepts clicks.
	_lobby = _full_control(_root)
	_lobby.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_lobby_panel = _panel(_lobby, Color(0.07, 0.14, 0.18, 0.96))
	_lobby_panel.set_anchors_and_offsets_preset(Control.PRESET_LEFT_WIDE)
	_lobby_panel.offset_left = 22
	_lobby_panel.offset_right = 440
	_lobby_panel.offset_top = 22
	_lobby_panel.offset_bottom = -22
	var frame := _vbox(_lobby_panel, 8)
	var header := HBoxContainer.new()
	frame.add_child(header)
	var title := _vbox(header, 1)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_child(_label("LA PREVIA", 34))
	_code_label = _label("SALA  —", 16, CORAL)
	title.add_child(_code_label)
	header.add_child(_small_button("Invitar", _copy_invitation, true))
	_players_label = _label("Esperando amigos…", 15, MINT, true)
	frame.add_child(_players_label)
	var navigation := HBoxContainer.new()
	frame.add_child(navigation)
	var walking := _small_button("Caminar por la sala", func() -> void: walk_requested.emit(), true)
	walking.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	navigation.add_child(walking)
	navigation.add_child(_small_button("Ajustes", _open_settings))
	var tabs := TabContainer.new()
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	tabs.add_theme_stylebox_override("panel", _style(Color(0, 0, 0, 0), 0, 0, 8))
	tabs.add_theme_stylebox_override("tab_selected", _style(Color("36525a"), 7, 13, 7))
	tabs.add_theme_stylebox_override("tab_unselected", _style(Color("1b313d"), 7, 13, 7))
	tabs.add_theme_font_size_override("font_size", 16)
	tabs.add_theme_color_override("font_selected_color", CREAM)
	tabs.add_theme_color_override("font_unselected_color", MUTED)
	frame.add_child(tabs)
	var roster_scroll := _scroll(tabs)
	roster_scroll.name = "Jugadores"
	_roster = _vbox(roster_scroll, 7)
	_roster.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var rules_scroll := _scroll(tabs)
	rules_scroll.name = "Reglas"
	var config_box := _vbox(rules_scroll, 8)
	config_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	config_box.add_child(_label("El anfitrión fija la cantidad de humanos. Los roles se sortean de nuevo en cada ronda.", 14, MUTED, true))
	config_box.add_child(_small_button("Dirección para amigos", _open_invite_settings))
	_mode = OptionButton.new()
	_mode.add_theme_font_size_override("font_size", 16)
	for mode: String in MODES:
		_mode.add_item(MODE_NAMES[mode])
	_mode.item_selected.connect(_mode_selected)
	config_box.add_child(_mode)
	_mode_description = _label("", 14, MUTED, true)
	config_box.add_child(_mode_description)
	for field: Array in CONFIG_FIELDS:
		var key: String = field[0]
		var row := HBoxContainer.new()
		config_box.add_child(row)
		var label := _label(field[1], 14, CREAM, true)
		label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(label)
		var value := SpinBox.new()
		value.min_value = field[2]
		value.max_value = field[3]
		value.step = field[4]
		value.value = _config.get(key, field[2])
		value.custom_minimum_size.x = 98
		value.value_changed.connect(func(_number: float) -> void:
			if not _updating_config:
				_update_dependent_ranges()
				_config_apply.text = "Aplicar cambios"
		)
		row.add_child(value)
		_fields[key] = value
		_field_rows[key] = row
	config_box.add_child(_label("Cambiar reglas requiere que todos vuelvan a marcarse listos. Los valores de balance son de prueba.", 13, MUTED, true))
	_config_apply = _small_button("Aplicar reglas", _apply_config)
	config_box.add_child(_config_apply)
	_start_reason = _label("", 14, MUTED, true)
	frame.add_child(_start_reason)
	var actions := HBoxContainer.new()
	frame.add_child(actions)
	_ready_button = _small_button("Estoy listo", func() -> void: ready_requested.emit(not _ready_value), true)
	_ready_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(_ready_button)
	_start_button = _small_button("Empezar", func() -> void: start_requested.emit())
	_start_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(_start_button)
	frame.add_child(_small_button("Salir de la sala", func() -> void: leave_requested.emit()))
	_status(frame)
	_lobby_walking_panel = _panel(_lobby, Color(0.05, 0.12, 0.16, 0.92))
	_lobby_walking_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	_lobby_walking_panel.offset_left = 22
	_lobby_walking_panel.offset_top = -100
	_lobby_walking_panel.offset_right = 590
	_lobby_walking_panel.offset_bottom = -22
	_lobby_walking_label = _label("", 16, CREAM, true)
	_lobby_walking_label.custom_minimum_size.x = 515
	_lobby_walking_panel.add_child(_lobby_walking_label)
	_set_mouse_ignore_recursive(_lobby_walking_panel)
	_lobby_walking_panel.hide()


func _mode_selected(_index: int) -> void:
	_update_mode_fields()
	if not _updating_config:
		_update_dependent_ranges()
		_config_apply.text = "Aplicar cambios"


func _update_mode_fields() -> void:
	var mode: String = MODES[clampi(_mode.selected, 0, 2)]
	for field: Array in CONFIG_FIELDS:
		_field_rows[field[0]].visible = field[5] == "all" or field[5] == mode
	match mode:
		"blood": _mode_description.text = "Mosquitos: junten la cuota compartida. Humanos: evítenlo hasta el final. Los golpes aturden 35 s; otro mosquito puede ayudar."
		"survival": _mode_description.text = "Mosquitos: basta con que uno siga vivo al final. Humanos: elimínenlos a todos. Picar es opcional."
		"sleep": _mode_description.text = "Humanos: alcancen la meta colectiva. Las picaduras pausan el trabajo; cada fallo acorta solo tu plazo futuro. Los golpes aturden 35 s y un compañero puede ayudar."


func _update_dependent_ranges(received: Dictionary = {}) -> void:
	# Read before changing ranges: their automatic clamps must not overwrite
	# server values when a previous configuration had stricter minima.
	if not _fields.has("task_floor"):
		return
	var was_updating: bool = _updating_config
	_updating_config = true
	var requested: Dictionary = {}
	for key: String in _fields:
		requested[key] = received.get(key, _fields[key].value)
	requested["mode"] = received.get("mode", MODES[clampi(_mode.selected, 0, 2)])
	var valid: Dictionary = Simulation.sanitize_config(requested)
	_fields["round_seconds"].min_value = Simulation.minimum_round_seconds(valid)
	_fields["round_seconds"].value = valid.round_seconds
	_fields["round_seconds"].tooltip_text = "En Tareas, la ronda reserva traslado y trabajo para el primer encargo de cada humano." if valid.mode == "sleep" else ""
	var minimum: float = Simulation.minimum_task_deadline(float(valid.task_work))
	_fields["task_interval"].min_value = maxf(15.0, minimum + 0.5)
	_fields["task_interval"].value = valid.task_interval
	_fields["task_deadline"].max_value = 59.5
	_fields["task_deadline"].min_value = minimum
	_fields["task_deadline"].max_value = float(valid.task_interval) - 0.5
	_fields["task_deadline"].value = valid.task_deadline
	_fields["task_floor"].max_value = 59.5
	_fields["task_floor"].min_value = minimum
	_fields["task_floor"].max_value = float(valid.task_deadline)
	_fields["task_floor"].value = valid.task_floor
	_fields["task_floor"].tooltip_text = "Reserva %.0f s para llegar al puesto, además del tiempo de trabajo. Los fallos nunca bajan de este piso." % Simulation.TASK_TRAVEL_RESERVE
	_updating_config = was_updating


func _apply_config() -> void:
	if not _owner:
		return
	var config := _config.duplicate(true)
	config.erase("mosquito_lives")
	config.erase("respawn_seconds")
	config["mode"] = MODES[clampi(_mode.selected, 0, 2)]
	for key: String in _fields:
		config[key] = _fields[key].value
	config_requested.emit(config)
	_config_apply.text = "Reglas enviadas"


func _build_hud() -> void:
	_hud = _full_control(_root)
	var left := _hud_card(_hud)
	left.position = Vector2(12,12)
	left.custom_minimum_size.x = 180
	var objectives := _vbox(left,3)
	_hud_role = _hud_label("",12)
	objectives.add_child(_hud_role)
	_hud_role.hide()
	_hud_objective = _hud_label("",14)
	objectives.add_child(_hud_objective)
	_hud_objective.hide()
	_hud_progress_text = _hud_label("",15)
	objectives.add_child(_hud_progress_text)
	_hud_progress = ProgressBar.new()
	_hud_progress.show_percentage = false
	_hud_progress.custom_minimum_size.y = 4
	objectives.add_child(_hud_progress)
	var clock_panel := _hud_card(_hud)
	clock_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	clock_panel.offset_left = -88
	clock_panel.offset_right = -12
	clock_panel.offset_top = 12
	clock_panel.offset_bottom = 50
	_hud_time = _hud_label("00:00",24)
	_hud_time.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	clock_panel.add_child(_hud_time)
	_hud_equipment = _hud_label("",11)
	_hud_equipment.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hud_equipment.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_hud_equipment.offset_left = -255
	_hud_equipment.offset_right = -12
	_hud_equipment.offset_top = 56
	_hud.add_child(_hud_equipment)
	_task_panel = _hud_card(_hud)
	_task_panel.position = Vector2(12,65)
	_task_panel.custom_minimum_size.x = 218
	var task_box := _vbox(_task_panel,3)
	var task_row := HBoxContainer.new()
	task_box.add_child(task_row)
	_task_title = _hud_label("",13)
	_task_title.custom_minimum_size.x = 145
	_task_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_task_title.clip_text = true
	_task_title.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	task_row.add_child(_task_title)
	_task_time = _hud_label("",13)
	task_row.add_child(_task_time)
	_task_progress = ProgressBar.new()
	_task_progress.show_percentage = false
	_task_progress.custom_minimum_size.y = 4
	task_box.add_child(_task_progress)
	_task_detail = _hud_label("",12,true)
	task_box.add_child(_task_detail)
	_hud_context = _hud_card(_hud)
	_hud_context.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	_hud_context.offset_left = 12
	_hud_context.offset_right = 258
	_hud_context.offset_top = -64
	_hud_context.offset_bottom = -12
	_hud_context.grow_vertical = Control.GROW_DIRECTION_BEGIN
	var hints := _vbox(_hud_context,3)
	_hud_state = _hud_label("",14,true)
	_hud_state.custom_minimum_size.x = 230
	hints.add_child(_hud_state)
	_hud_tool = _hud_label("",12,true)
	hints.add_child(_hud_tool)
	_hud_hint = _hud_label("",12,true)
	hints.add_child(_hud_hint)
	_focus_progress = ProgressBar.new()
	_focus_progress.show_percentage = false
	_focus_progress.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_focus_progress.offset_left = -52
	_focus_progress.offset_right = 52
	_focus_progress.offset_top = 25
	_focus_progress.offset_bottom = 29
	_hud.add_child(_focus_progress)
	_focus_progress.hide()
	_attack_recovery = ProgressBar.new()
	_attack_recovery.show_percentage = false
	_attack_recovery.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_attack_recovery.offset_left = -18
	_attack_recovery.offset_right = 18
	_attack_recovery.offset_top = 21
	_attack_recovery.offset_bottom = 24
	_hud.add_child(_attack_recovery)
	_attack_recovery.hide()
	_reticle = _hud_label("·",30)
	_reticle.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_reticle.position -= Vector2(6,21)
	_hud.add_child(_reticle)
	_attack_cue=AttackCue.new()
	_attack_cue.mouse_filter=Control.MOUSE_FILTER_IGNORE
	_attack_cue.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_attack_cue.offset_left=-16
	_attack_cue.offset_right=16
	_attack_cue.offset_top=-16
	_attack_cue.offset_bottom=16
	_hud.add_child(_attack_cue)
	_door_hint=_hud_label("",14)
	_door_hint.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	_door_hint.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_door_hint.offset_left=-110
	_door_hint.offset_right=110
	_door_hint.offset_top=32
	_door_hint.offset_bottom=56
	_hud.add_child(_door_hint)
	_practice_banner = _hud_label("PRÁCTICA",11)
	_practice_banner.position = Vector2(12,46)
	_hud.add_child(_practice_banner)
	_practice_banner.hide()
	_comic_feedback = _hud_label("",22)
	_comic_feedback.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_comic_feedback.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_comic_feedback.offset_left = -90
	_comic_feedback.offset_right = 90
	_comic_feedback.offset_top = -55
	_comic_feedback.offset_bottom = -26
	_hud.add_child(_comic_feedback)
	_comic_feedback.hide()
	_hud_help = _hud_label("",11)
	_hud_help.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hud_help.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	_hud_help.offset_left = -160
	_hud_help.offset_right = -12
	_hud_help.offset_top = -28
	_hud_help.offset_bottom = -10
	_hud.add_child(_hud_help)
	# Compact bars need their own thin styles: the menu's 3 px border hides
	# the fill completely on a 3–4 px gameplay progress bar.
	for bar: ProgressBar in [_hud_progress, _task_progress, _focus_progress, _attack_recovery]:
		var track := StyleBoxFlat.new()
		track.bg_color = Color(0.05, 0.07, 0.09, 0.85)
		track.border_color = Color(1, 1, 1, 0.65)
		track.set_border_width_all(1)
		var fill := StyleBoxFlat.new()
		fill.bg_color = SUN
		bar.add_theme_stylebox_override("background", track)
		bar.add_theme_stylebox_override("fill", fill)
	_set_mouse_ignore_recursive(_hud)

func _hud_card(parent: Node) -> PanelContainer:
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.07,0.09,0.12,0.64)
	style.set_corner_radius_all(4)
	style.content_margin_left = 7
	style.content_margin_right = 7
	style.content_margin_top = 4
	style.content_margin_bottom = 4
	panel.add_theme_stylebox_override("panel",style)
	parent.add_child(panel)
	return panel

func _hud_label(value: String, font_size: int, wrap: bool = false) -> Label:
	var label := _label(value,font_size,PAPER,wrap)
	label.add_theme_color_override("font_outline_color",INK)
	label.add_theme_constant_override("outline_size",3)
	return label


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
	_rematch_button = _button("Volver a la sala · revancha", func() -> void:
		if _practice:
			practice_restart_requested.emit()
		else:
			rematch_requested.emit()
	, true)
	box.add_child(_rematch_button)
	_result_leave = _button("Salir de la sala", func() -> void: leave_requested.emit())
	box.add_child(_result_leave)
	_status(box)


func _build_pause() -> void:
	_pause = _menu_surface()
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_pause.add_child(center)
	var panel := _panel(center)
	panel.custom_minimum_size.x = 440
	var box := _vbox(panel, 14)
	box.add_child(_label("¡UN SEGUNDO!", 48))
	box.add_child(_label("La ronda sigue en marcha.", 17, MUTED))
	box.add_child(_button("Seguir jugando", func() -> void: set_pause(false), true))
	box.add_child(_button("Cómo jugar", _open_help))
	box.add_child(_button("Ajustes y controles", _open_settings))
	_pause_leave = _button("Salir de la sala", func() -> void: leave_requested.emit())
	box.add_child(_pause_leave)
	box.add_child(_label("Si alguien se desconecta, la ronda se interrumpe sin ganador.", 15, MUTED, true))


func _build_help() -> void:
	_help = _menu_surface()
	_help.hide()
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_help.add_child(center)
	var panel := _panel(center)
	panel.custom_minimum_size = Vector2(620,490)
	var box := _vbox(panel,16)
	var heading := HBoxContainer.new()
	box.add_child(heading)
	var title := _label("GUÍA RÁPIDA",36)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	_help_close = _small_button("Listo · volver",_close_help,true)
	heading.add_child(_help_close)
	_help_body = _label("",17,INK,true)
	box.add_child(_help_body)
	box.add_child(_label("El menú no detiene la ronda. Podés cambiar las teclas desde Ajustes.",13,MUTED,true))


func _open_help() -> void:
	if _help_open or _screen != "game": return
	_help_return_focus = get_viewport().gui_get_focus_owner()
	_help_open = true
	var keys := [Prefs.binding_text("move_forward"),Prefs.binding_text("move_left"),Prefs.binding_text("move_back"),Prefs.binding_text("move_right")]
	var text_value := ""
	if _help_role == "human":
		text_value = "HUMANO\n%s %s %s %s · moverte\n%s correr · %s saltar · %s agacharte\n\nAPUNTÁ Y GOLPEÁ\nMirá al mosquito o mirá tu cuerpo si te pica. %s da una palmada; %s es la alternativa. Detrás, pedí ayuda.\n%s recoger / cambiar herramienta · %s soltar\n" % [keys[0],keys[1],keys[2],keys[3],Prefs.binding_text("sprint"),Prefs.binding_text("jump"),Prefs.binding_text("crouch"),Prefs.binding_text("attack"),Prefs.binding_text("self_swat"),Prefs.binding_text("pickup"),Prefs.binding_text("drop")]
		text_value += "Apuntá a una puerta cercana y pulsá %s para abrirla o cerrarla.\n" % Prefs.binding_text("interact")
		text_value += "El aro junto a la mira señala una oportunidad de golpe: el mosquito puede moverse antes del contacto. La barra corta indica recuperación.\n"
		text_value += "Diario o pantufla: mantené %s para cargar y soltá para lanzar. La potencia depende del objeto; podés recogerlo cuando se detenga. Abrir el menú o cambiar de herramienta cancela la carga.\n" % Prefs.binding_text("throw")
		if _help_mode == "sleep": text_value += "Mantené %s en tu puesto para hacer la tarea. Una picadura pausa el trabajo.\n" % Prefs.binding_text("interact")
	else:
		text_value = "MOSQUITO\n%s avanza hacia la mira · soltar frena\n%s retrocede · %s / %s mueve a los lados\n%s posarse / volver a volar\n\nTU MARCA\nMantené %s cerca de la marca: concentrás, te acercás y picás. Soltar cancela la carga. Mientras picás, soltá y pulsá %s otra vez para desprenderte; retrocedé para retirarte.\n" % [keys[0],keys[2],keys[1],keys[3],Prefs.binding_text("perch"),Prefs.binding_text("bite"),Prefs.binding_text("bite")]
		if _help_mode != "survival": text_value += "Un golpe aturde 35 s. Cerca de un compañero caído, apuntale y mantené %s para ayudarlo: recuperación ×4, sin acumular ayudantes.\n" % Prefs.binding_text("bite")
	var objectives := {"blood":"SANGRE · Los mosquitos completan la cuota compartida; los humanos intentan impedirlo hasta el final.","sleep":"TAREAS · Los humanos completan la meta común antes del final; los mosquitos intentan interrumpirlos.","survival":"SUPERVIVENCIA · Los mosquitos deben llegar vivos al final. Un golpe elimina hasta la próxima ronda; picar es opcional."}
	_help_body.text = text_value + "\n" + str(objectives.get(_help_mode,""))
	_help.show()
	_set_focus_scope(_help)
	_queue_focus(_help_close)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	screen_changed.emit("help")


func _close_help() -> void:
	if not _help_open: return
	_help_open = false
	_help.hide()
	_set_focus_scope(_base_focus_scope())
	if _paused:
		if is_instance_valid(_help_return_focus): _queue_focus(_help_return_focus)
		else: _focus_first(_pause)
	else:
		get_viewport().gui_release_focus()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if is_menu_open() else Input.MOUSE_MODE_CAPTURED
	screen_changed.emit(_screen)


func _build_customization() -> void:
	_customization = _menu_surface()
	var frame := _vbox(_margin(_customization,24),14)
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation",14)
	frame.add_child(header)
	_custom_title = _label("TU PINTA",44)
	_custom_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_custom_title)
	for role: String in ["human","mosquito"]:
		var button := _small_button("Humano" if role == "human" else "Mosquito",_select_custom_role.bind(role))
		button.toggle_mode = true
		header.add_child(button)
		_custom_role_buttons[role] = button
	header.add_child(_small_button("Listo · volver",_close_customization,true))
	var columns := HBoxContainer.new()
	columns.add_theme_constant_override("separation",16)
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	frame.add_child(columns)
	var categories := _vbox(columns,8)
	categories.custom_minimum_size.x = 160
	var titles := {"color":"Color","face":"Cara","hair":"Pelo / antenas","outfit":"Ropa / cuerpo","accessory":"Accesorios","footwear":"Pies / patas","accent":"Detalles"}
	for key: String in CosmeticsData.CATEGORY_KEYS:
		var button := _small_button(titles[key],_select_custom_category.bind(key))
		button.toggle_mode = true
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.icon = _custom_thumbnail("human",key,0)
		button.add_theme_constant_override("icon_max_width",30)
		button.custom_minimum_size.y = 51
		button.add_theme_font_size_override("font_size",14)
		categories.add_child(button)
		_category_buttons[key] = button
	var studio := _vbox(columns,8)
	studio.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_avatar_preview = AvatarPreview.new()
	_avatar_preview.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_avatar_preview.size_flags_vertical = Control.SIZE_EXPAND_FILL
	studio.add_child(_avatar_preview)
	var camera_row := HBoxContainer.new()
	camera_row.alignment = BoxContainer.ALIGNMENT_CENTER
	studio.add_child(camera_row)
	for view: String in ["front","side","back"]:
		var captions := {"front":"Frente","side":"Lado","back":"Espalda"}
		var button := _small_button(captions[view],func() -> void:
			if _avatar_preview.has_method("set_view"): _avatar_preview.set_view(view)
		)
		button.add_theme_font_size_override("font_size",14)
		camera_row.add_child(button)
		_custom_view_buttons[view] = button
	var reset := _small_button("↺",func() -> void: _avatar_preview.reset_view())
	reset.tooltip_text = "Restablecer la vista completa"
	camera_row.add_child(reset)
	_custom_view_buttons["reset"] = reset
	var camera_help := _label("Arrastrá para girar · rueda para acercar",13,MUTED,true)
	camera_help.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	studio.add_child(camera_help)
	var panel := _panel(columns)
	panel.custom_minimum_size.x = 336
	var scroll := _scroll(panel)
	_custom_options = _vbox(scroll,10)
	_custom_options.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_custom_caption = _label("Guardado en esta PC · solo apariencia",14,INK,true)
	frame.add_child(_custom_caption)
	# Kept as compatibility handles; category controls own the actual editor.
	_accessory_select = OptionButton.new()
	_accessory_select.hide()
	_customization.add_child(_accessory_select)


func _open_customization() -> void:
	Prefs.cosmetics = CosmeticsData.sanitize(Prefs.cosmetics)
	_set_screen("customization")
	_custom_role = "human"
	_refresh_customization()
	_focus_custom_category()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	_queue_focus(_custom_role_buttons["human"])


func _close_customization() -> void:
	Prefs.save_settings()
	_set_screen("home")
	_queue_focus(_home_default_focus)


func _select_custom_role(role: String) -> void:
	if role not in ["human", "mosquito"]:
		return
	_custom_role = role
	_refresh_customization()
	_focus_custom_category()


func _select_color(index: int) -> void:
	Prefs.cosmetics[_custom_role]["color"] = index
	_commit_cosmetics()


func _select_accessory(index: int) -> void:
	Prefs.cosmetics[_custom_role]["accessory"] = index
	_commit_cosmetics()


func _commit_cosmetics() -> void:
	Prefs.cosmetics = CosmeticsData.sanitize(Prefs.cosmetics)
	Prefs.save_settings()
	cosmetics_changed.emit(Prefs.cosmetics.duplicate(true))
	_refresh_customization()


func _refresh_customization() -> void:
	var appearance: Dictionary = CosmeticsData.appearance_for(Prefs.cosmetics,_custom_role)
	for role: String in _custom_role_buttons:
		_custom_role_buttons[role].button_pressed = role == _custom_role
	for key: String in _category_buttons:
		_category_buttons[key].button_pressed = key == _custom_category
		_category_buttons[key].icon = _custom_thumbnail(_custom_role,key,0)
	_category_buttons.hair.text = "Pelo" if _custom_role == "human" else "Antenas"
	_category_buttons.outfit.text = "Ropa" if _custom_role == "human" else "Cuerpo"
	_category_buttons.footwear.text = "Pantuflas" if _custom_role == "human" else "Patas"
	for child: Node in _custom_options.get_children():
		_custom_options.remove_child(child)
		child.queue_free()
	_custom_option_buttons.clear()
	var names: Array = CosmeticsData.option_names(_custom_role,_custom_category)
	_custom_options.add_child(_label("ELEGÍ TU ESTILO",19,INK))
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation",8)
	grid.add_theme_constant_override("v_separation",8)
	_custom_options.add_child(grid)
	for index: int in range(names.size()):
		var button := _small_button(str(names[index]),_select_custom_option.bind(index))
		button.toggle_mode = true
		button.button_pressed = int(appearance.get(_custom_category,0)) == index
		button.custom_minimum_size = Vector2(136,140)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.icon = _custom_thumbnail(_custom_role,_custom_category,index)
		button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
		button.vertical_icon_alignment = VERTICAL_ALIGNMENT_TOP
		button.expand_icon = false
		button.clip_text = true
		button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		button.add_theme_constant_override("icon_max_width",116)
		button.add_theme_constant_override("h_separation",3)
		button.add_theme_font_size_override("font_size",13)
		button.add_theme_stylebox_override("normal",_style(Color("fff6df"),8,8,8))
		button.add_theme_stylebox_override("hover",_style(Color("fff0b0"),8,8,8))
		button.add_theme_stylebox_override("pressed",_style(MINT,8,8,8))
		button.add_theme_stylebox_override("hover_pressed",_style(MINT.lightened(0.1),8,8,8))
		button.text = ("✓ " if button.button_pressed else "") + str(names[index])
		button.tooltip_text = str(names[index]) + (" · En uso" if button.button_pressed else " · Elegir")
		button.set_meta("appearance_index",index)
		grid.add_child(button)
		_custom_option_buttons.append(button)
	_custom_caption.text = "✓ Guardado · " + ("Humano" if _custom_role == "human" else "Mosquito") + " · " + str(names[int(appearance.get(_custom_category,0))]) + "    /    Solo apariencia"
	_avatar_preview.set_avatar(_custom_role,appearance)
	if _screen == "customization":
		_set_focus_scope(_customization)

func _select_custom_category(key: String) -> void:
	if key not in CosmeticsData.CATEGORY_KEYS: return
	_custom_category = key
	_refresh_customization()
	_focus_custom_category()
	_queue_focus(_category_buttons[key])

func _select_custom_option(index: int) -> void:
	if index < 0 or index >= CosmeticsData.option_count(_custom_role,_custom_category): return
	Prefs.cosmetics[_custom_role][_custom_category] = index
	_commit_cosmetics()
	if index < _custom_option_buttons.size():
		_queue_focus(_custom_option_buttons[index])


func _custom_thumbnail(role: String, category: String, index: int) -> Texture2D:
	var file: String = "swatch_%d" % index if category in ["color","accent"] else "%s_%s_%d" % [role,category,index]
	if not _thumbnail_cache.has(file):
		_thumbnail_cache[file] = load("res://assets/icons/customization/%s.svg" % file)
	return _thumbnail_cache[file] as Texture2D


func _focus_custom_category() -> void:
	if _avatar_preview.has_method("focus_category"):
		_avatar_preview.focus_category(_custom_category)


func _build_practice() -> void:
	_practice_screen = _menu_surface()
	_practice_screen.hide()
	var frame := _vbox(_margin(_practice_screen, 38), 18)
	var header := HBoxContainer.new()
	frame.add_child(header)
	var heading := _label("ENTRÁ EN CALOR", 56)
	heading.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(heading)
	header.add_child(_small_button("← Volver", _close_practice_setup))
	frame.add_child(_label("PRÁCTICA LOCAL · Vos elegís tu rol. Los rivales son automáticos.", 16, INK, true))
	var columns := HBoxContainer.new()
	columns.add_theme_constant_override("separation", 28)
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	frame.add_child(columns)
	var role_panel := _panel(columns)
	role_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var roles := _vbox(role_panel, 16)
	roles.add_child(_label("1. ¿DE QUÉ LADO?", 34))
	for role: String in ["human", "mosquito"]:
		var button := _button("HUMANO" if role == "human" else "MOSQUITO", _select_practice_role.bind(role))
		button.toggle_mode = true
		button.custom_minimum_size.y = 76
		roles.add_child(button)
		_practice_role_buttons[role] = button
		roles.add_child(_label("Palmadas, objetos y defensa propia." if role == "human" else "Volá, buscá tu marca y picá.", 16, MUTED, true))
	var modes_panel := _panel(columns)
	modes_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var modes := _vbox(modes_panel, 12)
	modes.add_child(_label("2. ¿QUÉ PROBAMOS?", 34))
	for mode: String in MODES:
		var button := _button(str(MODE_NAMES[mode]).to_upper(), _select_practice_mode.bind(mode))
		button.toggle_mode = true
		modes.add_child(button)
		_practice_mode_buttons[mode] = button
	_practice_detail = _label("", 17, INK, true)
	modes.add_child(_practice_detail)
	_practice_start_button = _button("¡A PRACTICAR!", func() -> void:
		set_practice(true)
		practice_requested.emit(_practice_role, _practice_mode)
	, true)
	_practice_start_button.custom_minimum_size.y = 58
	frame.add_child(_practice_start_button)


func _open_practice() -> void:
	_set_screen("practice")
	_refresh_practice()
	_queue_focus(_practice_start_button)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _close_practice_setup() -> void:
	_set_screen("home")
	_queue_focus(_home_default_focus)


func _select_practice_role(role: String) -> void:
	_practice_role = role
	_refresh_practice()


func _select_practice_mode(mode: String) -> void:
	_practice_mode = mode
	_refresh_practice()


func _refresh_practice() -> void:
	for role: String in _practice_role_buttons:
		_practice_role_buttons[role].button_pressed = role == _practice_role
	for mode: String in _practice_mode_buttons:
		_practice_mode_buttons[mode].button_pressed = mode == _practice_mode
	match _practice_mode:
		"blood": _practice_detail.text = "Cuota compartida de sangre. Los golpes aturden; los compañeros ayudan."
		"survival": _practice_detail.text = "Al menos un mosquito debe llegar vivo al final."
		"sleep": _practice_detail.text = "Tareas, interrupciones y rescates entre mosquitos."


func set_practice(value: bool) -> void:
	_practice = value
	if is_instance_valid(_practice_banner):
		_practice_banner.visible = value
	if is_instance_valid(_pause_leave):
		_pause_leave.text = "Volver al menú" if value else "Salir de la sala"


func _build_invite_settings() -> void:
	_invite_settings = _menu_surface()
	_invite_settings.hide()
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_invite_settings.add_child(center)
	var panel := _panel(center)
	panel.custom_minimum_size.x = 600
	var body := _vbox(panel, 14)
	body.add_child(_label("INVITÁ A LA CASA", 46))
	_invite_scope = OptionButton.new()
	for choice: String in ["Misma red", "Otra casa · Internet", "Red virtual compartida"]:
		_invite_scope.add_item(choice)
	_invite_scope.item_selected.connect(func(_index: int) -> void: _update_invite_scope())
	body.add_child(_invite_scope)
	body.add_child(_label("DIRECCIÓN PARA TUS AMIGOS", 14, INK))
	_invite_address_edit = LineEdit.new()
	_invite_address_edit.placeholder_text = "IP de red, IP pública o nombre de servidor"
	_invite_address_edit.max_length = 253
	body.add_child(_invite_address_edit)
	_invite_lan = OptionButton.new()
	_invite_lan.item_selected.connect(func(index: int) -> void:
		if index > 0:
			_invite_address_edit.text = str(_invite_lan.get_item_metadata(index))
	)
	body.add_child(_invite_lan)
	var port_row := HBoxContainer.new()
	body.add_child(port_row)
	var label := _label("Puerto para amigos", 16, INK)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	port_row.add_child(label)
	_invite_port_edit = SpinBox.new()
	_invite_port_edit.min_value = 1024
	_invite_port_edit.max_value = 65535
	_invite_port_edit.custom_minimum_size.x = 126
	port_row.add_child(_invite_port_edit)
	_invite_help = _label("", 15, MUTED, true)
	_invite_help.custom_minimum_size.x = 550
	body.add_child(_invite_help)
	_invite_status = _status(body)
	body.add_child(_button("GUARDAR Y COPIAR INVITACIÓN", _save_invite_settings, true))
	body.add_child(_small_button("← Volver", _close_invite_settings))


func _open_invite_settings() -> void:
	_invite_settings_open = true
	_invite_address_edit.text = Prefs.shared_address if _owner else _room_join_address
	_invite_port_edit.value = Prefs.shared_port if _owner else _room_join_port
	var scope: String = Prefs.sharing_scope if _owner else _room_join_scope
	_invite_scope.select(maxi(0, ["lan","internet","virtual"].find(scope)))
	_invite_lan.clear()
	_invite_lan.add_item("Elegir dirección de esta red…")
	for address: String in InvitationCodec.local_addresses():
		_invite_lan.add_item(address + " · misma red")
		_invite_lan.set_item_metadata(_invite_lan.item_count - 1, address)
	_update_invite_scope()
	_invite_settings.show()
	_set_focus_scope(_invite_settings)
	_queue_focus(_invite_address_edit)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _close_invite_settings() -> void:
	_invite_settings_open = false
	_invite_settings.hide()
	_set_focus_scope(_base_focus_scope())
	_queue_focus(_ready_button)


func _save_invite_settings() -> void:
	var address := _invite_address_edit.text.strip_edges()
	var scope: String = ["lan","internet","virtual"][_invite_scope.selected]
	var invalid: String = InvitationCodec.validate_sharing_address(address, scope)
	if not invalid.is_empty():
		_invite_status.text = invalid
		_ui_error_serial += 1
		ui_sound_requested.emit("error")
		_invite_address_edit.grab_focus()
		return
	if _owner:
		Prefs.shared_address = address
		Prefs.shared_port = int(_invite_port_edit.value)
		Prefs.sharing_scope = scope
		Prefs.save_settings()
	else:
		_room_join_address = address
		_room_join_port = int(_invite_port_edit.value)
		_room_join_scope = scope
	_close_invite_settings()
	_copy_invitation()


func _copy_invitation() -> void:
	var code: String = str(_code_label.get_meta("code", ""))
	var address: String = Prefs.shared_address if _owner else _room_join_address
	var port: int = Prefs.shared_port if _owner else _room_join_port
	var scope: String = Prefs.sharing_scope if _owner else _room_join_scope
	var encoded: String = InvitationCodec.encode(address, port, code, scope)
	if encoded.is_empty():
		_open_invite_settings()
		_invite_status.text = "Revisá la dirección y el alcance para tus amigos. Copiar una invitación no comprueba Internet."
		return
	DisplayServer.clipboard_set(encoded)
	show_status("Invitación copiada · " + ("solo esta red" if scope == "lan" else ("red virtual compartida" if scope == "virtual" else "alcance externo aún sin comprobar")))

func _update_invite_scope() -> void:
	var scope: String = ["lan","internet","virtual"][_invite_scope.selected]
	_invite_lan.visible = scope != "internet"
	_invite_address_edit.placeholder_text = "IP pública o nombre de servidor" if scope == "internet" else ("Dirección de la red virtual compartida" if scope == "virtual" else "Dirección de esta red")
	match scope:
		"internet": _invite_help.text = "Tu servidor escucha en UDP %d. Indicá la dirección y el puerto públicos que lleguen a esa PC. La invitación no abre puertos ni resuelve CGNAT; una conexión desde fuera es la comprobación real." % Prefs.local_host_port
		"virtual": _invite_help.text = "Todos deben estar conectados a la misma red virtual. Usá su dirección; el juego no instala ni configura esa red."
		_: _invite_help.text = "Para equipos de la misma red. Esta dirección local no permite entrar desde otra casa."


func _build_settings() -> void:
	_settings = _menu_surface()
	var frame := _vbox(_margin(_settings, 30), 14)
	var heading := HBoxContainer.new()
	frame.add_child(heading)
	var title := _label("A tu manera", 32)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	_settings_default_focus = _button("Listo · volver", _close_settings, true)
	heading.add_child(_settings_default_focus)
	var columns := HBoxContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	columns.add_theme_constant_override("separation", 22)
	frame.add_child(columns)
	var preferences_panel := _panel(columns)
	preferences_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var preference_scroll := _scroll(preferences_panel)
	var box := _vbox(preference_scroll, 18)
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_build_video_settings(box)
	box.add_child(_label("Sonido", 24))
	_add_slider(box, "volume", "Volumen general", Prefs.master_volume * 100.0, 0.0, 100.0, 1.0)
	_add_slider(box, "music_volume", "Música", Prefs.music_volume * 100.0, 0.0, 100.0, 1.0)
	_add_slider(box, "effects_volume", "Efectos", Prefs.effects_volume * 100.0, 0.0, 100.0, 1.0)
	_add_slider(box, "ambience_volume", "Ambiente", Prefs.ambience_volume * 100.0, 0.0, 100.0, 1.0)
	_add_slider(box, "ui_volume", "Interfaz", Prefs.ui_volume * 100.0, 0.0, 100.0, 1.0)
	box.add_child(_label("Cámara", 24))
	_add_slider(box, "human", "Sensibilidad · humano", Prefs.human_sensitivity * 1000.0, 0.5, 8.0, 0.1)
	_add_slider(box, "mosquito", "Sensibilidad · mosquito", Prefs.mosquito_sensitivity * 1000.0, 0.5, 8.0, 0.1)
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
	box.add_child(_label("CÓMO JUGAR", 14, CORAL))
	box.add_child(_label("Humano: tercera persona en la sala, primera al jugar. Podés correr, saltar y agacharte.", 15, INK, true))
	box.add_child(_label("Mosquito: avanzás hacia donde apunta la cámara, también hacia arriba o abajo. Soltar avance frena. Mantené concentración cerca de tu zona para estabilizarte, cargar y acercarte; soltá para cancelar. Una nueva pulsación mientras picás te desprende.", 15, INK, true))
	box.add_child(_label("Sangre y Tareas: un golpe te deja aturdido 35 s. Un mosquito cercano puede apuntarte y mantener concentración para ayudarte: la recuperación avanza cuatro veces más rápido. Varios ayudantes no suman velocidad. En Supervivencia, el golpe te elimina hasta la próxima ronda.", 15, INK, true))
	box.add_child(_label("DEFENSA PROPIA", 14, CORAL))
	box.add_child(_label("La defensa es manual: mirá tu cuerpo, apuntá al mosquito y golpeá. La tecla de defensa anterior repite ese mismo golpe; no elige una zona automáticamente. Si te pican detrás, pedí ayuda a un compañero. El aviso de picadura solo indica contactos reales.", 16, CREAM, true))
	box.add_child(_label("HERRAMIENTAS", 14, CORAL))
	for tool_id: String in Simulation.TOOL_STATS:
		var stats: Dictionary = Simulation.TOOL_STATS[tool_id]
		box.add_child(_label("%s\nAlcance %.2f m · recuperación %.2f s · radio %.2f m" % [TOOL_NAMES.get(tool_id, tool_id), float(stats.reach), float(stats.cooldown), float(stats.radius)], 14, MUTED, true))
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


func _build_video_settings(box: VBoxContainer) -> void:
	var video = preload("res://scripts/video_settings.gd")
	box.add_child(_label("Imagen y fluidez",24))
	box.add_child(_label("Resolución de imagen",16))
	var resolution := OptionButton.new()
	for size: Vector2i in video.SIZES: resolution.add_item("%d × %d" % [size.x,size.y])
	resolution.select(Prefs.video_resolution)
	resolution.item_selected.connect(func(index: int) -> void: Prefs.video_resolution=index; video.apply_display(get_window()))
	box.add_child(resolution)
	for setting: String in ["fullscreen","vsync","reflections"]:
		var button := CheckButton.new()
		button.text={"fullscreen":"Pantalla completa","vsync":"Sincronizar con la pantalla","reflections":"Reflejos del entorno"}[setting]
		button.button_pressed={"fullscreen":Prefs.video_fullscreen,"vsync":Prefs.video_vsync,"reflections":Prefs.video_reflections}[setting]
		button.toggled.connect(func(enabled: bool) -> void:
			match setting:
				"fullscreen": Prefs.video_fullscreen=enabled
				"vsync": Prefs.video_vsync=enabled
				"reflections": Prefs.video_reflections=enabled
			video.apply_display(get_window())
		)
		box.add_child(button)
	box.add_child(_label("Límite de cuadros por segundo",16))
	var cap := OptionButton.new()
	for value: int in video.CAPS: cap.add_item("Sin límite" if value==0 else "%d FPS" % value)
	cap.select(maxi(0,video.CAPS.find(Prefs.video_fps)))
	cap.item_selected.connect(func(index: int) -> void: Prefs.video_fps=video.CAPS[index]; video.apply_display(get_window()))
	box.add_child(cap)
	box.add_child(_label("Sombras",16))
	var shadows := OptionButton.new()
	for label: String in ["Desactivadas","Ligeras","Detalladas"]: shadows.add_item(label)
	shadows.select(Prefs.video_shadows)
	shadows.item_selected.connect(func(index: int) -> void: Prefs.video_shadows=index; video.apply_display(get_window()))
	box.add_child(shadows)
	box.add_child(_label("En ventana, la imagen se ajusta al espacio disponible. Más resolución y detalle requieren más potencia.",13,MUTED,true))

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
	var audio_control: bool = key == "volume" or key.ends_with("_volume")
	label.text = ("%s · %d%%" % [label_text,int(value)]) if audio_control else ("%s · %.1f" % [label_text,value])
	slider.value_changed.connect(func(number: float) -> void:
		label.text = ("%s · %d%%" % [label_text,int(number)]) if audio_control else ("%s · %.1f" % [label_text,number])
		match key:
			"human": Prefs.human_sensitivity = number / 1000.0
			"mosquito": Prefs.mosquito_sensitivity = number / 1000.0
			"volume":
				Prefs.master_volume = number / 100.0
				Prefs.apply_audio()
			"music_volume": Prefs.music_volume = number / 100.0
			"effects_volume": Prefs.effects_volume = number / 100.0
			"ambience_volume": Prefs.ambience_volume = number / 100.0
			"ui_volume": Prefs.ui_volume = number / 100.0
		if key.ends_with("_volume"):
			Prefs.apply_audio()
	)
	_settings_controls[key] = slider


func _begin_binding(action: String) -> void:
	_binding_action = action
	_refresh_binding_buttons()
	_binding_notice.text = "Nuevo control para «%s»: pulsá una tecla o botón. Esc cancela." % Prefs.ACTION_NAMES[action]
	_binding_buttons[action].text = "Pulsá una tecla…"


func _input(event: InputEvent) -> void:
	# Handle Escape before focused LineEdits/SpinBoxes can consume it. Physical
	# Escape remains a way back even when the gameplay pause action is remapped.
	var escape: bool = event is InputEventKey and event.pressed and (event.keycode == KEY_ESCAPE or event.physical_keycode == KEY_ESCAPE)
	if not _binding_action.is_empty() and _settings_open:
		if event is InputEventKey and event.pressed and not event.echo:
			if not escape:
				Prefs.bind_action(_binding_action, event)
			_binding_action = ""
			_finish_binding()
		elif event is InputEventMouseButton and event.pressed:
			Prefs.bind_action(_binding_action, event)
			_binding_action = ""
			_finish_binding()
		return
	if _screen == "game" and not _settings_open and not _invite_settings_open and event.is_action_pressed("toggle_help"):
		get_viewport().set_input_as_handled()
		if not event.is_echo():
			if _help_open: _close_help()
			else: _open_help()
		return
	if escape or event.is_action_pressed("pause"):
		get_viewport().set_input_as_handled()
		if event.is_echo():
			return
		if _help_open:
			_close_help()
		elif _invite_settings_open:
			_close_invite_settings()
		elif _settings_open:
			_close_settings()
		elif _screen == "practice":
			_close_practice_setup()
		elif _screen == "customization":
			_close_customization()
		elif _screen == "home" and _connection_open:
			if _advanced_open:
				_advanced_open = false
				_update_connection_options()
				_queue_focus(_advanced_button)
			else:
				_close_connection()
		else:
			escape_requested.emit()


func _finish_binding() -> void:
	get_viewport().set_input_as_handled()
	_refresh_binding_buttons()
	_binding_notice.text = "Controles guardados. Elegí otro para cambiarlo. Las acciones de roles distintos pueden compartir tecla."


func _refresh_binding_buttons() -> void:
	for action: String in _binding_buttons:
		_binding_buttons[action].text = Prefs.binding_text(action)


func _open_settings() -> void:
	_last_settings_focus = get_viewport().gui_get_focus_owner()
	_settings_open = true
	_settings.show()
	_set_focus_scope(_settings)
	_queue_focus(_settings_default_focus)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	screen_changed.emit("settings")


func _close_settings() -> void:
	_binding_action = ""
	Prefs.save_settings()
	_settings_open = false
	_settings.hide()
	_set_focus_scope(_base_focus_scope())
	if is_instance_valid(_last_settings_focus) and _last_settings_focus.is_visible_in_tree() and _last_settings_focus.focus_mode != Control.FOCUS_NONE:
		_queue_focus(_last_settings_focus)
	if _screen == "game" and not _paused:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	screen_changed.emit(_screen)


func show_home() -> void:
	_build()
	set_practice(false)
	_set_screen("home")
	_config_signature = ""
	_roster_signature = ""
	_owner = false
	_local_id = 0
	_room_join_address = ""
	_room_join_port = 27840
	_input_release()


func show_status(message: String) -> void:
	_build()
	for label: Label in _status_labels:
		label.text = message


func show_error(message: String) -> void:
	show_status(message)
	_ui_error_serial += 1
	ui_sound_requested.emit("error")


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
	_lobby_player_count = players.size()
	var local_player: Dictionary = players.get(local_id, players.get(str(local_id), {}))
	_ready_value = bool(local_player.get("ready", false))
	_ready_button.text = "Listo ✓ · cancelar" if _ready_value else "Estoy listo"
	# Movement snapshots must not rebuild roster widgets or steal keyboard focus.
	var roster_display: Dictionary = {}
	for id: Variant in players:
		var player: Dictionary = players[id]
		roster_display[id] = {"name": player.get("name", ""), "ready": player.get("ready", false)}
	var signature: String = var_to_str(roster_display) + str(data.get("owner", 0))
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
			_fields[key].value = _config.get(key, FALLBACK_CONFIG.get(key, 1 if key == "human_count" else 0))
		_updating_config = false
		_update_dependent_ranges(_config)
		_update_mode_fields()
		_config_apply.text = "Aplicar reglas" if _owner else "Las reglas las cambia el anfitrión"
	_mode.disabled = not _owner
	for key: String in _fields:
		_fields[key].editable = _owner
	_config_apply.disabled = not _owner
	_start_button.disabled = not _owner or not bool(data.get("can_start", false))
	_start_button.text = "Empezar" if _owner else "Esperando inicio"
	_start_reason.text = str(data.get("start_reason", ""))
	if _start_reason.text.is_empty():
		_start_reason.text = "Todos listos. ¡A jugar!" if data.get("can_start", false) else "Hace falta al menos un jugador por bando y todos listos."
	var humans: int = int(_config.get("human_count", 1))
	_players_label.text = "%d en la casa · %d %s por sorteo\nEl resto juega como mosquito" % [_lobby_player_count, humans, "humano" if humans == 1 else "humanos"]
	_lobby_walking_label.text = "SALA " + code + "  ·  " + str(_lobby_player_count) + " amigos\n" + Prefs.binding_text("pause") + " menú de sala · " + Prefs.binding_text("move_forward") + "/" + Prefs.binding_text("move_left") + "/" + Prefs.binding_text("move_back") + "/" + Prefs.binding_text("move_right") + " caminar"
	if changed_screen:
		set_lobby_walking(false)


func _update_roster(players: Dictionary, local_id: int, owner_id: int) -> void:
	for child: Node in _roster.get_children():
		_roster.remove_child(child)
		child.queue_free()
	for id: Variant in players:
		var player: Dictionary = players[id]
		var line: String = ("✓  " if player.get("ready", false) else "·  ") + str(player.get("name", "Amigo")).left(24)
		if int(id) == local_id:
			line += " · vos"
		if int(id) == owner_id:
			line += " · anfitrión"
		var label := _label(line, 16, MINT if player.get("ready", false) else CREAM, true)
		_roster.add_child(label)


func set_lobby_walking(value: bool) -> void:
	_lobby_walking = value and _screen == "lobby"
	_lobby_panel.visible = not _lobby_walking
	_lobby_walking_panel.visible = _lobby_walking
	_set_focus_scope(_lobby_walking_panel if _lobby_walking else _lobby_panel)
	if _lobby_walking:
		get_viewport().gui_release_focus()
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	else:
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		if _screen == "lobby":
			_queue_focus(_ready_button)


func show_game(snapshot: Dictionary, private_data: Dictionary, local_id: int) -> void:
	_build()
	var changed_screen: bool = _screen != "game"
	_set_screen("game")
	if changed_screen:
		_hud_context_key = ""
		_attack_feedback_key = ""
	_local_id = local_id
	var actors: Dictionary = snapshot.get("actors",{})
	var actor: Dictionary = actors.get(local_id,actors.get(str(local_id),{}))
	var human: bool = str(actor.get("role","mosquito")) == "human"
	var alive: bool = bool(actor.get("alive",true))
	var state: String = str(actor.get("state","flying"))
	var config: Dictionary = snapshot.get("config",FALLBACK_CONFIG)
	var mode: String = str(config.get("mode","blood"))
	_help_role = "human" if human else "mosquito"
	_help_mode = mode
	_hud_help.text = "%s · ayuda" % Prefs.binding_text("toggle_help")
	var now := float(Time.get_ticks_msec()) / 1000.0
	_hud_role.text = ("HUMANO" if human else "MOSQUITO") + " · " + {"blood":"SANGRE","survival":"SUPERVIVENCIA","sleep":"TAREAS"}.get(mode,mode)
	_hud_time.text = _clock(float(snapshot.get("time_left",0)))
	_hud_time.add_theme_color_override("font_color",CORAL.lightened(0.3) if float(snapshot.get("time_left",0)) < 20 else PAPER)
	var living := 0
	for other: Variant in actors.values():
		if other is Dictionary and other.get("role","") == "mosquito" and other.get("alive",false): living += 1
	_hud_progress.visible = mode != "survival"
	match mode:
		"blood":
			var goal := maxf(1.0,float(config.get("blood_goal",12)))
			_hud_progress_text.text = "Sangre %.1f / %.0f" % [float(snapshot.get("blood",0)),goal]
			_hud_progress.value = float(snapshot.get("blood",0)) / goal * 100.0
		"survival": _hud_progress_text.text = "%d mosquitos vivos" % living
		"sleep":
			var goal := maxi(1,int(snapshot.get("task_goal",1)))
			_hud_progress_text.text = "Tareas %d / %d" % [int(snapshot.get("tasks_done",0)),goal]
			_hud_progress.value = float(snapshot.get("tasks_done",0)) / goal * 100.0
	_hud_objective.text = ""
	_hud_state.text = ""
	_hud_hint.text = ""
	_hud_tool.text = ""
	_hud_state.add_theme_color_override("font_color",PAPER)
	_focus_progress.hide()
	_focus_progress.value = 0
	_attack_recovery.hide()
	_reticle.visible = alive
	_attack_cue.visible=human and alive
	_door_hint.hide()
	_task_panel.visible = human and mode == "sleep" and not Dictionary(private_data.get("task",{})).is_empty()
	var context_key := "idle"
	var persistent := false
	if human:
		var tool := str(actor.get("tool","hands"))
		_hud_equipment.text = str(TOOL_NAMES.get(tool,tool)).replace(" · palmadas","")
		var attack: Dictionary = private_data.get("attack",{})
		_attack_cue.update_state(bool(attack.get("candidate",false)) and bool(attack.get("can_swing",false)),str(attack.get("status",""))=="blocked")
		var attack_key := "%s:%s" % [attack.get("id",-1),attack.get("state","")]
		if int(attack.get("id",-1)) >= 0 and attack_key != _attack_feedback_key:
			_attack_feedback_key = attack_key
			if attack.get("state","") in ["hit","miss"]:
				_flash_comic("¡Tocó!" if attack.state == "hit" else "Falló")
				if attack.state=="hit": _attack_cue.confirm_hit()
		var recovery := maxf(0.0,float(attack.get("recovery",0)))
		_attack_recovery.visible = alive and recovery > 0
		_attack_recovery.value = clampf(recovery / maxf(0.01,float(Simulation.TOOL_STATS.get(tool,Simulation.TOOL_STATS.hands).cooldown)),0,1) * 100
		var contact: Dictionary = private_data.get("bite_feedback",{})
		var interaction: Dictionary = private_data.get("interaction",{})
		var pickup: Dictionary=private_data.get("pickup",{})
		var bitten: bool = bool(contact.get("active",actor.get("bitten",false)))
		if bitten:
			var side := str(contact.get("side","front"))
			_hud_state.text = "¡Picadura!" + {"left":" · izquierda","right":" · derecha","rear":" · detrás"}.get(side,"")
			_hud_state.add_theme_color_override("font_color",CORAL.lightened(0.35))
			_hud_hint.text = "Pedí ayuda a un compañero" if side == "rear" else "Mirá tu cuerpo · %s" % Prefs.binding_text("attack")
			context_key = "bitten:" + side
			persistent = true
		elif alive and interaction.get("kind","") == "door":
			_door_hint.text = "[%s] %s puerta" % [Prefs.binding_text("interact"),interaction.get("verb","Abrir")] if bool(interaction.get("can_use",false)) else "Puerta en movimiento"
			_door_hint.show()
		elif bool(pickup.get("can_take",false)):
			var nearby := str(pickup.get("tool",""))
			_hud_state.text = str(TOOL_NAMES.get(nearby,nearby))
			_hud_hint.text = "%s recoger" % Prefs.binding_text("pickup")
			context_key = "pickup:" + nearby
		if mode == "sleep": _update_task(private_data,bitten)
		var throwing: Dictionary = private_data.get("throw",{})
		if alive and str(throwing.get("state","")) == "charging":
			var power := clampf(float(throwing.get("power",throwing.get("charge",0))),0,1)
			_attack_recovery.show()
			_attack_recovery.value = power * 100.0
			_hud_tool.text = "%s · %.0f%% · soltá %s para lanzar" % [TOOL_NAMES.get(tool,tool),power*100.0,Prefs.binding_text("throw")]
		elif alive and str(throwing.get("reason","")) == "blocked":
			_hud_tool.text = "Sin espacio para lanzar"
		elif alive and bool(throwing.get("can_throw",false)):
			_hud_tool.text = "%s · %s mantener para cargar" % [TOOL_NAMES.get(tool,tool),Prefs.binding_text("throw")]
	else:
		var assignment: Dictionary = private_data.get("assignment",{})
		var focus: Dictionary = private_data.get("focus",{})
		var focus_state := str(focus.get("state","idle"))
		var stun: Dictionary = private_data.get("stun",{})
		var help: Dictionary = private_data.get("help",{})
		var help_state := str(help.get("state","idle"))
		_hud_equipment.text = ""
		_hud_tool.text = ""
		if mode != "survival" and (state == "stunned" or bool(stun.get("active",false))):
			_reticle.hide()
			_hud_tool.text = ""
			_hud_state.text = "Aturdido · %.0f s" % ceilf(maxf(0.0,float(stun.remaining))) if stun.has("remaining") else "Aturdido"
			_hud_hint.text = "Te están ayudando · ×4" if bool(stun.get("helped",false)) else "Un compañero puede ayudarte"
			context_key = "stunned"
			persistent = true
		elif not alive or state == "dead":
			_hud_state.text = "Eliminado · volvés en la próxima ronda"
			context_key = "dead"
			persistent = true
		elif mode != "survival" and help_state in ["ready","helping"]:
			_hud_tool.text = ""
			_hud_state.text = "Ayudando · %.0f%%" % (clampf(float(help.get("progress",0)),0,1)*100.0) if help_state == "helping" else "Compañero aturdido"
			_hud_hint.text = "Mantené %s · ayuda ×4" % Prefs.binding_text("bite")
			context_key = "help:" + help_state + ":" + str(help.get("target",0))
			if help_state == "helping":
				_focus_progress.show()
				_focus_progress.value = clampf(float(help.get("progress",0)),0,1)*100.0
				persistent = true
		elif state == "biting" or focus_state == "attached":
			_hud_state.text = "Picando"
			_hud_hint.text = "%s para desprenderte" % Prefs.binding_text("bite")
			context_key = "attached"
			persistent = true
		elif focus_state == "charging":
			_focus_progress.show()
			_focus_progress.value = clampf(float(focus.get("progress",0)),0,1) * 100
			_hud_state.text = "Concentrando… %.0f%%" % _focus_progress.value
			_hud_hint.text = "Soltá %s para cancelar" % Prefs.binding_text("bite")
			context_key = "charging"
			persistent = true
		elif focus_state == "ready":
			_hud_state.text = "Zona a tu alcance"
			_hud_hint.text = "Mantené %s para picar" % Prefs.binding_text("bite")
			context_key = "ready:" + str(assignment.get("revision",0))
		elif focus_state == "blocked" or str(focus.get("reason","")).begins_with("Soltá "):
			_hud_state.text = _focus_reason(str(focus.get("reason","")),"Buscá una entrada libre")
			context_key = "blocked:" + _hud_state.text
		elif focus_state == "waiting":
			_hud_state.text = "Esperando una zona libre"
			context_key = "waiting_assignment"
		elif state == "perched":
			_hud_state.text = "Posado"
			_hud_hint.text = "%s volver a volar" % Prefs.binding_text("perch")
			context_key = "perched"
	if context_key != _hud_context_key:
		_hud_context_key = context_key
		_hud_context_until = now + 2.5
	_hud_context.visible = not _hud_state.text.is_empty() and (persistent or now < _hud_context_until)
	_hud_hint.visible = not _hud_hint.text.is_empty()
	_hud_tool.visible = not _hud_tool.text.is_empty()
	if changed_screen and not _paused and not _settings_open:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _defense_band(_pitch: float) -> String:
	return "golpe manual"


func _focus_reason(reason: String, fallback: String) -> String:
	var value: String = reason.strip_edges()
	if value.is_empty():
		return fallback
	# Simulation reasons describe a keyboard-independent action; old protocol
	# wording can still contain the default E. Replace the token, never a letter
	# inside another word, and avoid interpreting the new binding as regex syntax.
	if _focus_key_pattern == null:
		_focus_key_pattern = RegEx.new()
		_focus_key_pattern.compile("\\bE\\b")
	var matches: Array[RegExMatch] = _focus_key_pattern.search_all(value)
	matches.reverse()
	for token: RegExMatch in matches:
		value = value.left(token.get_start()) + Prefs.binding_text("bite") + value.substr(token.get_end())
	return value


func _update_task(private_data: Dictionary, bitten: bool) -> void:
	var task: Dictionary = private_data.get("task",{})
	if task.is_empty():
		_task_panel.hide()
		return
	_task_title.text = str(task.get("name","Tarea"))
	_task_time.text = _clock(float(task.get("remaining",0)))
	_task_time.add_theme_color_override("font_color",CORAL.lightened(0.3) if float(task.get("remaining",0)) < 8 else PAPER)
	_task_progress.value = float(task.get("progress",0)) / maxf(0.01,float(task.get("work",3))) * 100
	_task_detail.text = "Picadura · trabajo pausado" if bitten else ""
	_task_detail.visible = bitten


func _flash_comic(text_value: String) -> void:
	if is_instance_valid(_comic_feedback_tween):
		_comic_feedback_tween.kill()
	_comic_feedback.text = text_value
	_comic_feedback.modulate = Color.WHITE
	_comic_feedback.show()
	_comic_feedback_tween = create_tween()
	_comic_feedback_tween.tween_interval(0.24)
	_comic_feedback_tween.tween_property(_comic_feedback, "modulate:a", 0.0, 0.16)
	_comic_feedback_tween.tween_callback(_comic_feedback.hide)


func show_results(snapshot: Dictionary) -> void:
	_build()
	_set_screen("results")
	var winner: String = str(snapshot.get("winner", ""))
	match winner:
		"human", "humans": _result_title.text = "¡SE ACABÓ EL ZUMBIDO!"
		"mosquito", "mosquitoes": _result_title.text = "¡A DORMIR... MAÑANA!"
		_: _result_title.text = "Ronda interrumpida"
	_result_subtitle.text = "Ganaron los humanos" if winner in ["human", "humans"] else ("Ganaron los mosquitos" if winner in ["mosquito", "mosquitoes"] else "Sin ganador")
	if _practice:
		_result_subtitle.text = "PRÁCTICA / " + _result_subtitle.text
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
	_rematch_button.disabled = not _practice and not _owner
	_rematch_button.text = "REPETIR PRÁCTICA" if _practice else ("VOLVER A LA SALA" if _owner else "Esperando la revancha del anfitrión")
	_result_leave.text = "VOLVER AL MENÚ" if _practice else "Salir de la sala"
	_input_release()


func set_pause(open: bool) -> void:
	if _screen == "lobby":
		if _settings_open:
			_close_settings()
		set_lobby_walking(not open)
		return
	if _screen != "game":
		if _settings_open and not open:
			_close_settings()
		return
	if _settings_open:
		_close_settings()
	_paused = open
	_pause.visible = open
	_set_focus_scope(_pause if open else _hud)
	if open:
		_focus_first(_pause)
	else:
		get_viewport().gui_release_focus()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if open else Input.MOUSE_MODE_CAPTURED


func is_menu_open() -> bool:
	if _settings_open or _invite_settings_open or _help_open:
		return true
	if _screen == "lobby":
		return not _lobby_walking
	return _screen != "game" or _paused


func _input_release() -> void:
	_help_open = false
	_help.hide()
	_paused = false
	_pause.hide()
	_settings_open = false
	_settings.hide()
	_invite_settings_open = false
	_invite_settings.hide()
	_binding_action = ""
	_lobby_walking = false
	_lobby_panel.show()
	_lobby_walking_panel.hide()
	_set_focus_scope(_base_focus_scope())
	if _screen == "home":
		_queue_focus(_home_default_focus)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _set_screen(screen: String) -> void:
	var changed: bool = _screen != screen
	if changed:
		_help_open = false
		_help.hide()
		if _screen == "customization":
			preview_closed.emit()
		_paused = false
		_pause.hide()
		_settings_open = false
		_settings.hide()
		_invite_settings_open = false
		_invite_settings.hide()
		_binding_action = ""
		_lobby_walking = false
	_screen = screen
	_home.visible = screen == "home"
	_lobby.visible = screen == "lobby"
	_hud.visible = screen == "game"
	_results.visible = screen == "results"
	_customization.visible = screen == "customization"
	_practice_screen.visible = screen == "practice"
	if changed:
		screen_changed.emit(screen)
	if changed:
		_set_focus_scope(_base_focus_scope())
		_focus_first(_base_focus_scope())


func _base_focus_scope() -> Control:
	if _help_open: return _help
	match _screen:
		"home": return _home
		"lobby": return _lobby_walking_panel if _lobby_walking else _lobby_panel
		"customization": return _customization
		"practice": return _practice_screen
		"results": return _results
		_: return _pause if _paused else _hud


func _set_focus_scope(scope: Control) -> void:
	# A full-screen settings layer is modal for keyboard focus as well as mouse.
	# Remember each widget's intended focus mode, so closing it restores Tab flow.
	var pending: Array[Node] = [_root]
	while not pending.is_empty():
		var node: Node = pending.pop_back()
		if node is Control:
			var control: Control = node
			if not control.has_meta("original_focus_mode"):
				control.set_meta("original_focus_mode", control.focus_mode)
			var in_scope: bool = control == scope or scope.is_ancestor_of(control)
			control.focus_mode = int(control.get_meta("original_focus_mode")) if in_scope else Control.FOCUS_NONE
		# TabContainer's internal TabBar is focusable too; otherwise a modal's
		# Tab cycle could slip behind it into the lobby.
		for child: Node in node.get_children(true):
			pending.append(child)


func _focus_first(scope: Control) -> bool:
	if not scope.is_visible_in_tree():
		return false
	if scope.focus_mode == Control.FOCUS_ALL and not (scope is BaseButton and scope.disabled):
		_queue_focus(scope)
		return true
	for child: Node in scope.get_children():
		if child is Control and _focus_first(child):
			return true
	return false


func _queue_focus(control: Control) -> void:
	_grab_focus_if_current.call_deferred(control)


func _grab_focus_if_current(control: Control) -> void:
	# Several network events can change screens before the deferred frame.
	# An older screen must never reclaim focus or try to focus hidden controls.
	if not is_instance_valid(control) or not control.is_visible_in_tree() or control.focus_mode == Control.FOCUS_NONE:
		return
	if control is BaseButton and control.disabled:
		return
	var scope: Control = _help if _help_open else (_invite_settings if _invite_settings_open else (_settings if _settings_open else _base_focus_scope()))
	if control != scope and not scope.is_ancestor_of(control):
		return
	control.grab_focus()


func _clock(seconds: float) -> String:
	var whole: int = maxi(0, int(ceilf(seconds)))
	return "%02d:%02d" % [whole / 60, whole % 60]
