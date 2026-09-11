extends Control
## Selection only. The server/pose renderer owns every actual body gesture.
signal selected(id: String)
signal cancelled
const Catalog = preload("res://scripts/emote_catalog.gd")
var buttons: Dictionary = {}
var order: Array[String] = []
var candidate := ""
var held := false
var panel: PanelContainer
var hint: Label
var opening := false
var pointer_armed := false

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var shade := ColorRect.new()
	shade.color = Color(0.06,0.08,0.11,.58)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(shade)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	panel = PanelContainer.new()
	panel.custom_minimum_size.x = 520
	var style := StyleBoxFlat.new()
	style.bg_color = Color("fff5da")
	style.border_color = Color("232532")
	style.set_border_width_all(4)
	style.set_corner_radius_all(8)
	style.content_margin_left = 20
	style.content_margin_right = 20
	style.content_margin_top = 16
	style.content_margin_bottom = 16
	panel.add_theme_stylebox_override("panel",style)
	center.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation",12)
	panel.add_child(box)
	var title := Label.new()
	title.text = "UN GESTO VALE MÁS…"
	title.add_theme_font_size_override("font_size",24)
	box.add_child(title)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation",10)
	grid.add_theme_constant_override("v_separation",10)
	box.add_child(grid)
	for data: Dictionary in Catalog.entries():
		var id: String = data.id
		var button := Button.new()
		button.text = data.label
		button.custom_minimum_size = Vector2(228,74)
		button.add_theme_font_size_override("font_size",23)
		button.toggle_mode = true
		button.pressed.connect(func() -> void: selected.emit(id))
		button.focus_entered.connect(func() -> void:
			if not opening and visible and not held:
				candidate = id
				for key: String in buttons: buttons[key].button_pressed = key==id
		)
		grid.add_child(button)
		buttons[id] = button
		order.append(id)
	hint = Label.new()
	hint.add_theme_font_size_override("font_size",14)
	box.add_child(hint)
	var close := Button.new()
	close.text = "Cancelar · Esc"
	close.add_theme_font_size_override("font_size",18)
	close.pressed.connect(func() -> void: cancelled.emit())
	box.add_child(close)
	hide()

func open_selector(favorites: Array, hold_to_choose: bool, binding: String) -> void:
	order = Catalog.normalize_favorites(favorites)
	for index: int in range(order.size()):
		var button: Button = buttons[order[index]]
		button.get_parent().move_child(button,index)
		button.button_pressed = false
	candidate = ""
	held = hold_to_choose
	pointer_armed = false
	hint.text = "%s: soltá para elegir · flechas o ratón · Esc cancela" % binding if held else "Elegí un gesto · flechas y Enter · Esc cancela"
	show()
	opening = true
	buttons[order[0]].grab_focus()
	opening = false

func choose(id: String) -> void:
	if not Catalog.is_valid(id): return
	candidate = id
	for key: String in buttons: buttons[key].button_pressed = key==candidate
	buttons[id].grab_focus()

func handle_event(event: InputEvent) -> bool:
	if not visible: return false
	if event is InputEventMouseMotion:
		# Releasing captured mouse can synthesize a warp over a button. It is
		# not an intentional selection, just as initial keyboard focus is not.
		if not pointer_armed:
			pointer_armed = true
			return false
		if event.relative.length_squared() < 1.0: return false
		for id: String in order:
			if buttons[id].get_global_rect().has_point(event.position): choose(id)
		return false
	if event is InputEventMouseButton and event.pressed and not panel.get_global_rect().has_point(event.position):
		cancelled.emit()
		return true
	if event is InputEventKey and event.pressed and not event.echo:
		var key: int = event.physical_keycode if event.physical_keycode else event.keycode
		if key in [KEY_LEFT,KEY_RIGHT,KEY_UP,KEY_DOWN]:
			var current: int = order.find(candidate)
			var offset: int = -1 if key==KEY_LEFT else 1 if key==KEY_RIGHT else -2 if key==KEY_UP else 2
			choose(order[posmod(current+offset,order.size())] if current>=0 else order[0])
			return true
		if key==KEY_ENTER and not candidate.is_empty():
			selected.emit(candidate)
			return true
	return false

func finish_hold() -> void:
	if candidate.is_empty(): cancelled.emit()
	else: selected.emit(candidate)
