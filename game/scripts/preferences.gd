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
}
const ACTION_NAMES: Dictionary = {
	"move_forward": "Avanzar", "move_back": "Retroceder", "move_left": "Izquierda",
	"move_right": "Derecha", "ascend": "Volar hacia arriba", "descend": "Volar hacia abajo",
	"bite": "Picar / desprenderse", "attack": "Palmada / golpe", "self_swat": "Defensa propia",
	"perch": "Posarse / volar", "interact": "Hacer tarea (mantener)",
	"pickup": "Recoger / cambiar objeto", "drop": "Soltar objeto", "pause": "Menú",
}
static var human_sensitivity: float = 0.0025
static var mosquito_sensitivity: float = 0.0025
static var invert_y: bool = false
static var marker_pulse: bool = true
static var master_volume: float = 0.6
static var player_name: String = ""
static var server_address: String = "127.0.0.1"
static var server_port: int = 27840
static var room_code: String = ""
static var cosmetics: Dictionary = {"human": {"color": 0, "accessory": 0}, "mosquito": {"color": 0, "accessory": 0}}
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


static func load_settings() -> void:
	setup_inputs()
	if _loaded:
		return
	_loaded = true
	var config := ConfigFile.new()
	if config.load(FILE_PATH) == OK:
		human_sensitivity = clampf(float(config.get_value("controls", "human_sensitivity", human_sensitivity)), 0.0005, 0.008)
		mosquito_sensitivity = clampf(float(config.get_value("controls", "mosquito_sensitivity", mosquito_sensitivity)), 0.0005, 0.008)
		invert_y = bool(config.get_value("controls", "invert_y", invert_y))
		marker_pulse = bool(config.get_value("accessibility", "marker_pulse", marker_pulse))
		master_volume = clampf(float(config.get_value("audio", "master_volume", master_volume)), 0.0, 1.0)
		player_name = str(config.get_value("connection", "player_name", "")).left(24)
		server_address = str(config.get_value("connection", "address", "127.0.0.1"))
		server_port = clampi(int(config.get_value("connection", "port", 27840)), 1, 65535)
		room_code = str(config.get_value("connection", "code", "")).left(12)
		cosmetics = CosmeticsData.sanitize(config.get_value("appearance", "cosmetics", {}))
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


static func save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("controls", "human_sensitivity", human_sensitivity)
	config.set_value("controls", "mosquito_sensitivity", mosquito_sensitivity)
	config.set_value("controls", "invert_y", invert_y)
	config.set_value("accessibility", "marker_pulse", marker_pulse)
	config.set_value("audio", "master_volume", master_volume)
	config.set_value("connection", "player_name", player_name)
	config.set_value("connection", "address", server_address)
	config.set_value("connection", "port", server_port)
	config.set_value("connection", "code", room_code)
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
	var error: Error = config.save(FILE_PATH)
	if error != OK:
		push_warning("No se pudieron guardar las preferencias: %s" % error_string(error))
	apply_audio()


static func apply_audio() -> void:
	AudioServer.set_bus_volume_db(0, linear_to_db(maxf(master_volume, 0.0001)))
	AudioServer.set_bus_mute(0, master_volume <= 0.001)


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
