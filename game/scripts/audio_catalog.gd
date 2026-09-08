class_name AudioCatalog
extends RefCounted
## Offline rendered assets. Sources/CC0 sample provenance live in art_source/audio.

const Prefs = preload("res://scripts/preferences.gd")
const ROOT := "res://assets/audio/"
const MUSIC_BPM := 104.0
const MUSIC_BARS := 32
const MUSIC_SECONDS := MUSIC_BARS * 4.0 * 60.0 / MUSIC_BPM
const BUSES: Array[StringName] = [&"Music", &"Effects", &"Ambience", &"UI"]
const LOOPS: Array[String] = ["buzz", "buzz_perch", "buzz_bite", "help_loop", "focus_loop", "room_fan", "room_fridge", "night_air", "clock"]
const CUES: Array[String] = ["buzz", "buzz_perch", "buzz_bite", "clap", "swish", "impact", "bite", "detach", "perch", "pickup", "drop", "stun", "fall", "help_loop", "recover", "focus_loop", "task_start", "task_done", "task_fail", "step_wood", "step_tile", "step_cloth", "land", "cloth", "tool_swatter", "tool_racket", "tool_newspaper", "tool_broom", "ui_select", "ui_confirm", "ui_error", "room_fan", "room_fridge", "night_air", "clock", "door_move", "door_latch", "door_block"]
const ACCENTS: Array[String] = ["start", "stun", "recover", "task", "win", "lose"]
static var cache: Dictionary = {}

static func ensure_buses() -> void:
	for bus_name: StringName in BUSES:
		if AudioServer.get_bus_index(bus_name) < 0:
			AudioServer.add_bus()
			var index := AudioServer.bus_count - 1
			AudioServer.set_bus_name(index, bus_name)
			AudioServer.set_bus_send(index, &"Master")
	Prefs.apply_audio()

static func refresh_mix() -> void:
	Prefs.apply_audio()

static func cue(name: String) -> AudioStream:
	if name not in CUES:
		return null
	return _stream("sfx/" + name, name in LOOPS)

static func accent(name: String) -> AudioStream:
	if name not in ACCENTS:
		return null
	return _stream("music/accent_" + name, false)

static func music_layer(theme: String, layer: String) -> AudioStream:
	if theme not in ["menu", "gameplay"] or layer not in ["base", "rhythm", "melody"]:
		return null
	return _stream("music/" + theme + "_" + layer, true)

static func quiet_theme() -> AudioStream:
	return _stream("music/quiet", true)

static func _stream(relative: String, looping: bool) -> AudioStream:
	if cache.has(relative):
		return cache[relative]
	var path := ROOT + relative + ".ogg"
	if not ResourceLoader.exists(path):
		return null
	var stream: AudioStream = load(path)
	if stream is AudioStreamOggVorbis:
		stream.loop = looping
		stream.loop_offset = 0.0
		if relative.begins_with("music/") and looping:
			stream.bpm = 80.0 if relative == "music/quiet" else MUSIC_BPM
			stream.bar_beats = 4
			stream.beat_count = 96 if relative == "music/quiet" else MUSIC_BARS * 4
	cache[relative] = stream
	return stream
