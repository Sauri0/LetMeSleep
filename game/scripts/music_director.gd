extends Node
## Musical transitions depend only on clocks and the listener's own perception.
const Catalog = preload("res://scripts/audio_catalog.gd")
const BAR_SECONDS := 4.0 * 60.0 / Catalog.MUSIC_BPM
const SILENCE_DB := -72.0
var menu_player: AudioStreamPlayer
var game_player: AudioStreamPlayer
var quiet_player: AudioStreamPlayer
var menu_stream: AudioStreamSynchronized
var game_stream: AudioStreamSynchronized
var accents: Array[AudioStreamPlayer] = []
var ui_players: Array[AudioStreamPlayer] = []
var screen := ""
var desired_layers := Vector3(0.0, SILENCE_DB, SILENCE_DB)
var applied_layers := Vector3(0.0, SILENCE_DB, SILENCE_DB)
var music_clock := 0.0
var previous_bar := -1
var own_previous_state := ""
var last_attack_id := -1
var previous_task_active := false
var previous_failures := 0
var duck_left := 0.0
var ui_cooldown := 0.0
var accents_started: Dictionary = {}
var ui_started: Dictionary = {}

func _ready() -> void:
	setup()

func setup() -> void:
	if is_instance_valid(menu_player):
		return
	Catalog.ensure_buses()
	menu_stream = _synchronized("menu")
	game_stream = _synchronized("gameplay")
	menu_player = _player(&"Music")
	menu_player.stream = menu_stream
	game_player = _player(&"Music")
	game_player.stream = game_stream
	quiet_player = _player(&"Music")
	quiet_player.stream = Catalog.quiet_theme()
	for i: int in range(2): accents.append(_player(&"Music"))
	for i: int in range(3): ui_players.append(_player(&"UI"))

func _player(bus_name: StringName) -> AudioStreamPlayer:
	var player := AudioStreamPlayer.new()
	player.bus = bus_name
	player.volume_db = SILENCE_DB
	add_child(player)
	return player

func _synchronized(theme: String) -> AudioStreamSynchronized:
	var result := AudioStreamSynchronized.new()
	result.stream_count = 3
	var layers: Array[String] = ["base", "rhythm", "melody"]
	for i: int in range(3):
		result.set_sync_stream(i, Catalog.music_layer(theme, layers[i]))
		result.set_sync_stream_volume(i, 0.0)
	return result

static func local_layers(snapshot: Dictionary, personal: Dictionary, player_id: int) -> Vector3:
	var own: Dictionary = snapshot.get("actors", {}).get(player_id, {})
	var activity := false
	var strike: Dictionary = own.get("strike", {})
	activity = bool(strike.get("active", false)) or bool(own.get("bitten", false))
	var focus: Dictionary = personal.get("focus", {})
	activity = activity or str(focus.get("state", "")) in ["charging", "attached", "helping"]
	var remaining: float = maxf(0.0, float(snapshot.get("remaining", 999.0)))
	if snapshot.has("time_left"):
		remaining = float(snapshot.time_left)
	elif snapshot.has("elapsed"):
		remaining = float(snapshot.get("config", {}).get("round_seconds", 999.0)) - float(snapshot.elapsed)
	var urgent: bool = remaining <= 20.0
	var task: Dictionary = personal.get("task", {})
	urgent = urgent or (not task.is_empty() and float(task.get("remaining", 999.0)) <= 8.0)
	return Vector3(-5.0, -3.0 if urgent else (-9.0 if activity else -22.0), -11.0 if urgent else (-19.0 if activity else -32.0))

func set_context(next_screen: String, snapshot: Dictionary = {}, personal: Dictionary = {}, player_id: int = 0) -> void:
	setup()
	var normalized := "home" if next_screen == "menu" else next_screen
	var changed := normalized != screen
	screen = normalized
	if screen == "playing":
		if not game_player.playing and game_stream.get_sync_stream(0) != null:
			game_player.play()
			music_clock = 0.0
			previous_bar = -1
		if changed: accent("start")
		desired_layers = local_layers(snapshot, personal, player_id)
		var own: Dictionary = snapshot.get("actors", {}).get(player_id, {})
		var own_state := str(own.get("state", ""))
		if own_state == "stunned" and own_previous_state != "stunned": accent("stun")
		elif own_previous_state == "stunned" and own_state == "flying": accent("recover")
		own_previous_state = own_state
		var attack: Dictionary = personal.get("attack", {})
		if int(attack.get("id", -1)) != last_attack_id:
			last_attack_id = int(attack.get("id", -1))
			if last_attack_id >= 0: duck_left = maxf(duck_left, .75)
		if bool(own.get("bitten", false)): duck_left = maxf(duck_left, .4)
		if personal.has("task"):
			var task_active: bool = not Dictionary(personal.task).is_empty()
			var task_failures := int(personal.get("failures", 0))
			if previous_task_active and not task_active and task_failures == previous_failures: accent("task")
			previous_task_active = task_active
			previous_failures = task_failures
	else:
		own_previous_state = ""
		last_attack_id = -1
		previous_task_active = false
		previous_failures = 0
		if screen in ["customize", "preferences"] and not quiet_player.playing and quiet_player.stream != null:
			quiet_player.play()
		if screen in ["home", "lobby", "results"] and not menu_player.playing and menu_stream.get_sync_stream(0) != null:
			menu_player.play()
		if changed and screen == "results":
			var own: Dictionary = snapshot.get("actors", {}).get(player_id, {})
			var winner := str(snapshot.get("winner", snapshot.get("result", {}).get("winner", "")))
			if not winner.is_empty(): accent("win" if winner == str(own.get("role", "")) else "lose")

func _process(dt: float) -> void:
	if not is_instance_valid(menu_player): return
	ui_cooldown = maxf(0.0, ui_cooldown - dt)
	duck_left = maxf(0.0, duck_left - dt)
	music_clock += dt
	var bar := int(floor(music_clock / BAR_SECONDS))
	if bar != previous_bar:
		applied_layers = desired_layers
		previous_bar = bar
	for i: int in range(3):
		game_stream.set_sync_stream_volume(i, lerpf(game_stream.get_sync_stream_volume(i), applied_layers[i], minf(1.0, dt * 2.0)))
		var lobby_level: float = [-3.0, -13.0, -12.0][i] if screen == "lobby" else 0.0
		menu_stream.set_sync_stream_volume(i, lerpf(menu_stream.get_sync_stream_volume(i), lobby_level, minf(1.0, dt * 2.0)))
	var duck: float = -4.0 if duck_left > 0.0 else 0.0
	var menu_target: float = (-6.0 if screen == "results" else -2.0) + duck if screen in ["home", "lobby", "results"] else SILENCE_DB
	var game_target: float = duck if screen == "playing" else SILENCE_DB
	var quiet_target: float = -2.0 + duck if screen in ["customize", "preferences"] else SILENCE_DB
	menu_player.volume_db = lerpf(menu_player.volume_db, menu_target, minf(dt * 3.0, 1.0))
	game_player.volume_db = lerpf(game_player.volume_db, game_target, minf(dt * 3.0, 1.0))
	quiet_player.volume_db = lerpf(quiet_player.volume_db, quiet_target, minf(dt * 3.0, 1.0))
	if screen != "playing" and game_player.volume_db < -65.0: game_player.stop()

func accent(name: String) -> void:
	var stream := Catalog.accent(name)
	if stream == null or accents.is_empty(): return
	var voice: AudioStreamPlayer = accents[0]
	for candidate: AudioStreamPlayer in accents:
		if not candidate.playing:
			voice = candidate
			break
	voice.stop()
	voice.stream = stream
	voice.volume_db = -5.0
	voice.play()
	duck_left = maxf(duck_left, 1.0)
	accents_started[name] = int(accents_started.get(name, 0)) + 1

func ui_cue(name: String) -> void:
	setup()
	if name not in ["select", "confirm", "error"] or ui_cooldown > 0.0: return
	var stream := Catalog.cue("ui_" + name)
	if stream == null: return
	ui_cooldown = .065
	var voice: AudioStreamPlayer = ui_players[0]
	for candidate: AudioStreamPlayer in ui_players:
		if not candidate.playing:
			voice = candidate
			break
	voice.stop()
	voice.stream = stream
	voice.volume_db = -8.0
	voice.play()
	ui_started[name] = int(ui_started.get(name, 0)) + 1

func clear() -> void:
	screen = ""
	for value: Node in get_children():
		if value is AudioStreamPlayer: value.stop()
	own_previous_state = ""
	last_attack_id = -1
	previous_task_active = false
	previous_failures = 0
	duck_left = 0.0
