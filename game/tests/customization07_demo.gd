extends "res://tests/customization08_checks.gd"
## Prepared, reproducible menu demonstration. Intended for native --write-movie
## at --fixed-fps 30; the real Client supplies Godot UI cues/music. No game round.
var app: Node
var caption: Label
var report_path := "user://customization07-demo.json"
var recorded_profile: Dictionary = {}
var recorded_cues: Dictionary = {}

func _title(text: String) -> void:
	caption.text = "Tu pinta · escena preparada\n" + text

func _pause(seconds: float) -> void:
	await create_timer(seconds).timeout

func _orbit_preview(delta: Vector2) -> void:
	var rectangle: Rect2 = ui._avatar_preview.get_global_rect()
	var start := Vector2(rectangle.position.x+18 if delta.x>0 else rectangle.end.x-18,rectangle.get_center().y)
	await _mouse_button(start, MOUSE_BUTTON_LEFT, true)
	for step: int in range(60):
		var event := InputEventMouseMotion.new()
		event.position = start + delta * float(step+1)/60.0
		event.global_position = event.position
		event.relative = delta/60.0
		event.button_mask = MOUSE_BUTTON_MASK_LEFT
		Input.parse_input_event(event)
		await process_frame
	await _mouse_button(start+delta, MOUSE_BUTTON_LEFT, false)
	await _settle()

func _run() -> void:
	_backup()
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--demo-report="): report_path = argument.trim_prefix("--demo-report=")
	root.size = Vector2i(1280,720)
	root.content_scale_size = Vector2i(1280,720)
	# This explicitly prepared recording has a fixed viewport and audible native
	# mix. The inherited fixture restores the user's complete file afterward.
	Prefs.load_settings()
	Prefs.video_resolution = 0
	Prefs.video_fullscreen = false
	Prefs.master_volume = 0.6
	Prefs.music_volume = 0.55
	Prefs.ui_volume = 0.65
	app = load("res://scripts/main.gd").new()
	root.add_child(app)
	ui = app.get_node("Client").ui
	var overlay := CanvasLayer.new()
	overlay.layer = 100
	root.add_child(overlay)
	caption = Label.new()
	caption.position = Vector2(310,8)
	caption.size = Vector2(620,48)
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	caption.add_theme_font_size_override("font_size",16)
	caption.add_theme_color_override("font_color",Color("263a43"))
	caption.add_theme_color_override("font_outline_color",Color("fff8e4"))
	caption.add_theme_constant_override("outline_size",4)
	overlay.add_child(caption)
	Prefs.cosmetics = Cosmetics.default_profile()
	ui._open_customization()
	await _settle()
	_title("Ojos, cejas y boca: elegí cada parte")
	await _choose("eyes",2)
	await _pause(.65)
	await _choose("brows",2)
	await _pause(.55)
	await _choose("mouth",0)
	await _pause(.6)
	_title("Bigote y barba · un mismo color para todo el pelo")
	await _choose("mustache",2)
	await _pause(.6)
	await _choose("beard",1)
	await _pause(.6)
	await _choose("hair_color",2)
	await _pause(.7)
	var yaw: float = ui._avatar_preview.orbit_yaw
	await _zoom_preview()
	var zoomed: float = ui._avatar_preview.zoom
	_title("Humano · acercar y girar 360° con el ratón")
	await _orbit_preview(Vector2(TAU/0.012,0))
	_check(not is_equal_approx(yaw,ui._avatar_preview.orbit_yaw) and zoomed<1 and is_equal_approx(ui._avatar_preview.zoom,zoomed), "recorded human rotates by mouse while preserving wheel zoom")
	_check(absf(absf(ui._avatar_preview.orbit_yaw-yaw)-TAU)<0.005, "recorded human completes a continuous 360 degree orbit")
	await _pause(.65)
	var saved: Dictionary = Prefs.cosmetics.duplicate(true)
	_title("Guardado en esta PC · cerrar y volver a abrir")
	await _key(KEY_ESCAPE)
	Prefs.cosmetics = {}
	Prefs._loaded = false
	Prefs.load_settings()
	await _pause(.35)
	ui._open_customization()
	await _settle()
	_check(Prefs.cosmetics==saved and ui._avatar_preview.appearance==saved.human, "recorded menu reloads actual saved human")
	await _pause(.7)
	_title("Mosquito: también combina sus ojos, cejas y boca")
	await _activate(ui._custom_role_buttons.mosquito)
	await _choose("eyes",2)
	await _pause(.6)
	await _choose("brows",1)
	await _pause(.6)
	await _choose("mouth",2)
	await _pause(.7)
	yaw = ui._avatar_preview.orbit_yaw
	await _zoom_preview()
	zoomed = ui._avatar_preview.zoom
	_title("Mosquito · giro continuo de 360° con el ratón")
	await _orbit_preview(Vector2(-TAU/0.012,0))
	_check(not is_equal_approx(yaw,ui._avatar_preview.orbit_yaw) and zoomed<1 and is_equal_approx(ui._avatar_preview.zoom,zoomed), "recorded mosquito rotates by mouse while preserving wheel zoom")
	_check(absf(absf(ui._avatar_preview.orbit_yaw-yaw)-TAU)<0.005, "recorded mosquito completes a continuous 360 degree orbit")
	_check(Prefs.cosmetics.human==saved.human and Prefs.cosmetics.mosquito.eyes==2 and Prefs.cosmetics.mosquito.brows==1 and Prefs.cosmetics.mosquito.mouth==2, "recorded species keep independent parts")
	_title("Dos apariencias propias · sin cambiar las reglas del juego")
	await _pause(1.2)
	var music: Node = app.get_node("Client").music
	_check(music.screen=="customize" and music.quiet_player.playing, "real Godot customization music is playing")
	_check(int(music.ui_started.get("select",0))>0, "real Godot UI selection cues were played")
	recorded_profile = Prefs.cosmetics.duplicate(true)
	recorded_cues = music.ui_started.duplicate(true)
	app.queue_free()
	await _settle()
	await _finish()

func _write_report() -> void:
	var file := FileAccess.open(report_path, FileAccess.WRITE)
	var restored: bool = FileAccess.get_file_as_bytes(Prefs.FILE_PATH)==original if had_preferences else not FileAccess.file_exists(Prefs.FILE_PATH)
	file.store_string(JSON.stringify({"prepared_scene":true,"staged_control_demo":true,"preferences_modified":not restored,"native_audio":"Godot Client MusicDirector","ui_cues":recorded_cues,"checks":checks,"failures":failures,"human":recorded_profile.get("human",{}),"mosquito":recorded_profile.get("mosquito",{})},"  "))
	file.close()
