class_name Preferences
extends RefCounted
## Local preferences only. No network or gameplay authority belongs here.

const FILE_PATH := "user://preferences.cfg"
const CosmeticsData = preload("res://scripts/cosmetics.gd")
const DEFAULT_KEYS: Dictionary = {
	"move_forward": KEY_W, "move_back": KEY_S, "move_left": KEY_A,
	"move_right": KEY_D, "ascend": KEY_SPACE, "descend": KEY_CTRL,
	"bite": KEY_E, "self_swat": KEY_Q, "perch": KEY_F,
	"interact": KEY_E, "pause": KEY_ESCAPE, "pickup": KEY_R, "drop": KEY_G,
	"sprint": KEY_SHIFT, "jump": KEY_SPACE, "crouch": KEY_CTRL,
	"toggle_help": KEY_F1,
}
const ACTION_NAMES: Dictionary = {
	"move_forward": "Avanzar", "move_back": "Retroceder", "move_left": "Izquierda",
	"move_right": "Derecha", "ascend": "Altura auxiliar + (opcional)", "descend": "Altura auxiliar − (opcional)",
	"bite": "Concentrar (mantener) / soltar picadura", "attack": "Palmada / golpe", "self_swat": "Palmada manual (alternativa)",
	"perch": "Posarse / volar", "interact": "Puerta / tarea (mantener)",
	"pickup": "Recoger / cambiar objeto", "drop": "Soltar objeto", "pause": "Menú",
	"sprint": "Correr (humano)", "jump": "Saltar (humano)", "crouch": "Agacharse (humano)",
	"toggle_help": "Abrir / cerrar guía",
}
static var human_sensitivity: float = 0.0025
static var mosquito_sensitivity: float = 0.0025
static var invert_y: bool = false
static var marker_pulse: bool = true
static var master_volume: float = 0.6
static var music_volume: float = 0.55
static var effects_volume: float = 0.8
static var ambience_volume: float = 0.45
static var ui_volume: float = 0.65
static var video_resolution: int = 1
static var video_fullscreen: bool = false
static var video_vsync: bool = false
static var video_fps: int = 0
static var video_shadows: int = 2
static var video_reflections: bool = true
static var player_name: String = ""
static var server_address: String = "127.0.0.1"
static var server_port: int = 27840
static var local_host_port: int = 27840
static var sharing_scope: String = "lan"
static var room_code: String = ""
static var invitation: String = ""
static var shared_address: String = ""
static var shared_port: int = 27840
static var cosmetics: Dictionary = CosmeticsData.default_profile()
static var _loaded: bool = false


static func setup_inputs() -> void:
	for action: String in DEFAULT_KEYS:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
		if InputMap.action_get_events(action).is_empty():
			var key := InputEventKey.new()
			key.physical_keycode = DEFAULT_KEYS[action]
			InputMap.action_add_event(action, key)
	if not InputMap.has_action("attack"):
		InputMap.add_action("attack")
	if InputMap.action_get_events("attack").is_empty():
		var mouse := InputEventMouseButton.new()
		mouse.button_index = MOUSE_BUTTON_LEFT
		InputMap.action_add_event("attack", mouse)


static func load_settings(settings_path: String = FILE_PATH) -> void:
	setup_inputs()
	if _loaded:
		return
	_loaded = true
	if settings_path == FILE_PATH and not FileAccess.file_exists(FILE_PATH):
		_migrate_legacy_settings()
	var config := ConfigFile.new()
	# A fresh profile is genuinely uncapped. Explicit saved choices win below.
	video_vsync = false
	video_fps = 0
	if config.load(settings_path) == OK:
		human_sensitivity = clampf(float(config.get_value("controls", "human_sensitivity", human_sensitivity)), 0.0005, 0.008)
		mosquito_sensitivity = clampf(float(config.get_value("controls", "mosquito_sensitivity", mosquito_sensitivity)), 0.0005, 0.008)
		invert_y = bool(config.get_value("controls", "invert_y", invert_y))
		marker_pulse = bool(config.get_value("accessibility", "marker_pulse", marker_pulse))
		master_volume = clampf(float(config.get_value("audio", "master_volume", master_volume)), 0.0, 1.0)
		music_volume = _audio_value(config, "music_volume", 0.55)
		effects_volume = _audio_value(config, "effects_volume", 0.8)
		ambience_volume = _audio_value(config, "ambience_volume", 0.45)
		ui_volume = _audio_value(config, "ui_volume", 0.65)
		video_resolution = clampi(int(config.get_value("video","resolution",1)),0,3)
		video_fullscreen = bool(config.get_value("video","fullscreen",false))
		video_vsync = bool(config.get_value("video","vsync",false))
		video_fps = int(config.get_value("video","fps",0))
		if video_fps not in [0,60,120,144,165,240]: video_fps=0
		video_shadows = clampi(int(config.get_value("video","shadows",2)),0,2)
		video_reflections = bool(config.get_value("video","reflections",true))
		player_name = str(config.get_value("connection", "player_name", "")).left(24)
		server_address = str(config.get_value("connection", "address", "127.0.0.1"))
		server_port = clampi(int(config.get_value("connection", "port", 27840)), 1, 65535)
		local_host_port = clampi(int(config.get_value("hosting", "port", 27840)), 1024, 65535)
		sharing_scope = str(config.get_value("sharing", "scope", "lan"))
		if sharing_scope not in ["lan", "internet", "virtual"]:
			sharing_scope = "lan"
		room_code = str(config.get_value("connection", "code", "")).left(12)
		invitation = str(config.get_value("connection", "invitation", "")).left(1024)
		shared_address = str(config.get_value("sharing", "address", "")).left(253)
		shared_port = clampi(int(config.get_value("sharing", "port", 27840)), 1024, 65535)
		cosmetics = CosmeticsData.sanitize(config.get_value("appearance", "cosmetics", CosmeticsData.default_profile()))
		for action: String in ACTION_NAMES:
			var binding: String = str(config.get_value("bindings", action, ""))
			var parts := binding.split(":")
			if parts.size() != 2 or not parts[1].is_valid_int():
				continue
			var value: int = int(parts[1])
			if parts[0] == "key" and value > 0:
				var key := InputEventKey.new()
				key.physical_keycode = value
				bind_action(action, key, false)
			elif parts[0] == "mouse" and value >= 1 and value <= 9:
				var mouse := InputEventMouseButton.new()
				mouse.button_index = value
				bind_action(action, mouse, false)
	apply_audio()


static func _migrate_legacy_settings(legacy_path: String = "", destination: String = FILE_PATH) -> Error:
	# Keep the previous product's directory name only as a migration key.
	# Never modify the old file or replace preferences already saved by this app.
	if FileAccess.file_exists(destination):
		return OK
	var source: String = legacy_path
	if source.is_empty():
		source = OS.get_user_data_dir().get_base_dir().path_join("Dejame dormir/preferences.cfg")
	if not FileAccess.file_exists(source):
		return ERR_FILE_NOT_FOUND
	var parsed := ConfigFile.new()
	var validation: Error = parsed.load(source)
	if validation != OK:
		push_warning("No se migraron los ajustes anteriores porque el archivo no se pudo validar: %s" % error_string(validation))
		return validation
	var error: Error = DirAccess.copy_absolute(ProjectSettings.globalize_path(source), ProjectSettings.globalize_path(destination))
	if error != OK:
		push_warning("No se pudieron copiar los ajustes anteriores: %s" % error_string(error))
	return error


static func save_settings(settings_path: String = FILE_PATH) -> void:
	var config := ConfigFile.new()
	config.set_value("controls", "human_sensitivity", human_sensitivity)
	config.set_value("controls", "mosquito_sensitivity", mosquito_sensitivity)
	config.set_value("controls", "invert_y", invert_y)
	config.set_value("accessibility", "marker_pulse", marker_pulse)
	config.set_value("audio", "master_volume", master_volume)
	config.set_value("audio", "music_volume", music_volume)
	config.set_value("audio", "effects_volume", effects_volume)
	config.set_value("audio", "ambience_volume", ambience_volume)
	config.set_value("audio", "ui_volume", ui_volume)
	config.set_value("video","resolution",video_resolution)
	config.set_value("video","fullscreen",video_fullscreen)
	config.set_value("video","vsync",video_vsync)
	config.set_value("video","fps",video_fps)
	config.set_value("video","shadows",video_shadows)
	config.set_value("video","reflections",video_reflections)
	config.set_value("connection", "player_name", player_name)
	config.set_value("connection", "address", server_address)
	config.set_value("connection", "port", server_port)
	config.set_value("hosting", "port", local_host_port)
	config.set_value("sharing", "scope", sharing_scope)
	config.set_value("connection", "code", room_code)
	config.set_value("connection", "invitation", invitation)
	config.set_value("sharing", "address", shared_address)
	config.set_value("sharing", "port", shared_port)
	cosmetics = CosmeticsData.sanitize(cosmetics)
	config.set_value("appearance", "cosmetics", cosmetics)
	for action: String in ACTION_NAMES:
		var events := InputMap.action_get_events(action)
		if events.is_empty():
			continue
		var event: InputEvent = events[0]
		if event is InputEventKey:
			config.set_value("bindings", action, "key:%s" % event.physical_keycode)
		elif event is InputEventMouseButton:
			config.set_value("bindings", action, "mouse:%s" % event.button_index)
	var error: Error = config.save(settings_path)
	if error != OK:
		push_warning("No se pudieron guardar las preferencias: %s" % error_string(error))
	apply_audio()


static func apply_audio() -> void:
	AudioServer.set_bus_volume_db(0, linear_to_db(maxf(master_volume, 0.0001)))
	AudioServer.set_bus_mute(0, master_volume <= 0.001)
	var levels := {"Music": music_volume, "Effects": effects_volume, "Ambience": ambience_volume, "UI": ui_volume}
	for bus: String in levels:
		var index: int = AudioServer.get_bus_index(bus)
		if index < 0:
			continue
		var level: float = clampf(float(levels[bus]), 0.0, 1.0)
		AudioServer.set_bus_volume_db(index, linear_to_db(maxf(level, 0.0001)))
		AudioServer.set_bus_mute(index, level <= 0.001)


static func _audio_value(config: ConfigFile, key: String, fallback: float) -> float:
	var value: Variant = config.get_value("audio", key, fallback)
	if not (value is float or value is int) or not is_finite(float(value)):
		return fallback
	return clampf(float(value), 0.0, 1.0)


static func bind_action(action: String, event: InputEvent, save: bool = true) -> void:
	if not ACTION_NAMES.has(action):
		return
	if not InputMap.has_action(action):
		InputMap.add_action(action)
	InputMap.action_erase_events(action)
	var clean: InputEvent
	if event is InputEventKey:
		var key := InputEventKey.new()
		key.physical_keycode = event.physical_keycode if event.physical_keycode != 0 else event.keycode
		clean = key
	elif event is InputEventMouseButton:
		var mouse := InputEventMouseButton.new()
		mouse.button_index = event.button_index
		clean = mouse
	else:
		return
	InputMap.action_add_event(action, clean)
	if save:
		save_settings()


static func reset_bindings() -> void:
	for action: String in ACTION_NAMES:
		if InputMap.has_action(action):
			InputMap.action_erase_events(action)
	setup_inputs()
	save_settings()


static func binding_text(action: String) -> String:
	if not InputMap.has_action(action):
		return "—"
	var events := InputMap.action_get_events(action)
	if events.is_empty():
		return "—"
	var event: InputEvent = events[0]
	if event is InputEventMouseButton:
		match event.button_index:
			MOUSE_BUTTON_LEFT: return "Clic izq."
			MOUSE_BUTTON_RIGHT: return "Clic der."
			MOUSE_BUTTON_MIDDLE: return "Clic central"
			_: return "Ratón %s" % event.button_index
	if event is InputEventKey:
		var code: int = event.physical_keycode if event.physical_keycode != 0 else event.keycode
		match code:
			KEY_SPACE: return "Espacio"
			KEY_ESCAPE: return "Esc"
			KEY_CTRL: return "Ctrl"
			_: return OS.get_keycode_string(code)
	return event.as_text()
