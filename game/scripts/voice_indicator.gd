extends HBoxContainer
## Presentation of confirmed voice state; never opens a microphone.
signal mute_requested(muted: bool)
var data: Dictionary = {}
var text_label: Label
var level_bar: ProgressBar
var mute_button: Button

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_theme_constant_override("separation",5)
	text_label = Label.new()
	text_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	text_label.add_theme_font_size_override("font_size",12)
	text_label.add_theme_color_override("font_color",Color("fff5da"))
	text_label.add_theme_color_override("font_outline_color",Color("232532"))
	text_label.add_theme_constant_override("outline_size",3)
	add_child(text_label)
	level_bar = ProgressBar.new()
	level_bar.custom_minimum_size = Vector2(26,5)
	level_bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	level_bar.show_percentage = false
	for part: String in ["background","fill"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("50bea9") if part=="fill" else Color("34434c")
		level_bar.add_theme_stylebox_override(part,style)
	add_child(level_bar)
	mute_button = Button.new()
	mute_button.text = "Silenciar"
	mute_button.add_theme_font_size_override("font_size",12)
	mute_button.pressed.connect(func() -> void: mute_requested.emit(not bool(data.get("muted",false))))
	add_child(mute_button)
	mute_button.hide()
	set_state(data)

func set_state(value: Dictionary) -> void:
	data = value.duplicate(true)
	if not is_instance_valid(text_label): return
	var status: String = str(data.get("status","unavailable"))
	var binding: String = str(data.get("binding","—"))
	var muted: bool = bool(data.get("muted",status=="muted"))
	var reason: String = str(data.get("reason",""))
	match status:
		"capturing": text_label.text = "Transmitiendo" if not bool(data.get("testing",false)) else "Probando micrófono"
		"idle": text_label.text = binding + " · hablar"
		"muted": text_label.text = "Mic silenciado"
		_: text_label.text = {"Disponible durante la ronda":"Voz durante la ronda","No disponible mientras estás eliminado":"Voz · eliminado","Voz sólo online":"Voz sólo online"}.get(reason,"Voz no disponible")
	level_bar.visible = status=="capturing"
	var level: float = float(data.get("level",0.0))
	level_bar.value = clampf(level,0,1)*100.0 if level_bar.visible and is_finite(level) else 0.0
	var error: String = str(data.get("error",""))
	tooltip_text = error if not error.is_empty() else reason
	mute_button.text = "Activar mic" if muted else "Silenciar mic"
	visible = bool(data.get("visible",false))
