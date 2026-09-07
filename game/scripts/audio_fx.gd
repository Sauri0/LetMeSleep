extends Node3D
## Original synthesized PCM. Only confirmed public actor transitions produce cues.
## No zone/assignment data enters this module. Master volume belongs to Preferences.

const SAMPLE_RATE: int = 22050
const EFFECT_VOICES: int = 12
const OCCLUSION_INTERVAL: float = 0.12
static var streams: Dictionary = {}

var buzzes: Dictionary = {}
var previous: Dictionary = {}
var effect_pool: Array[AudioStreamPlayer3D] = []
var effects_started: Dictionary = {}
var latest_actors: Dictionary = {}
var latest_views: Dictionary = {}
var local_id: int = 0
var since_sync: float = 10.0
var occlusion_age: float = 0.0
var next_effect: int = 0
var suspended: bool = true

func setup() -> void:
	if not effect_pool.is_empty():
		return
	_build_streams()
	for i: int in range(EFFECT_VOICES):
		var voice := AudioStreamPlayer3D.new()
		voice.name = "Cue%d" % i
		voice.bus = &"Master"
		voice.unit_size = 1.4
		voice.max_distance = 10.0
		voice.max_db = -9.0
		voice.panning_strength = 0.8
		voice.attenuation_filter_cutoff_hz = 6500.0
		add_child(voice)
		effect_pool.append(voice)

func sync(data: Dictionary, views: Dictionary, player_id: int) -> void:
	if effect_pool.is_empty():
		setup()
	since_sync = 0.0
	local_id = player_id
	latest_actors = data
	latest_views = views
	var listener_alive: bool = bool(Dictionary(data.get(local_id, {})).get("alive", false))
	suspended = not listener_alive
	for key: Variant in buzzes.keys():
		if not data.has(key) or str(Dictionary(data[key]).get("role", "")) != "mosquito":
			var stale: AudioStreamPlayer3D = buzzes[key]
			stale.stop()
			stale.queue_free()
			buzzes.erase(key)
	for key: Variant in previous.keys():
		if not data.has(key):
			previous.erase(key)
	for key: Variant in data:
		var actor: Dictionary = data[key]
		var role: String = str(actor.get("role", ""))
		var alive: bool = bool(actor.get("alive", true))
		var state: String = str(actor.get("state", "flying"))
		var tool: String = str(actor.get("tool", "hands"))
		var swing: float = float(actor.get("swing", 0.0))
		var position: Vector3 = actor.get("p", Vector3.ZERO)
		if role == "mosquito":
			if not buzzes.has(key):
				var buzz := AudioStreamPlayer3D.new()
				buzz.name = "Buzz%d" % int(key)
				buzz.stream = streams["buzz"]
				buzz.bus = &"Master"
				buzz.unit_size = 0.85
				buzz.max_distance = 8.0
				buzz.max_db = -20.0
				buzz.volume_db = -60.0
				buzz.panning_strength = 0.85
				buzz.attenuation_filter_cutoff_hz = 4600.0
				add_child(buzz)
				buzzes[key] = buzz
			var voice: AudioStreamPlayer3D = buzzes[key]
			voice.position = position
			if not alive or suspended:
				voice.stop()
			elif not voice.playing:
				voice.play(float(int(key) % 17) / 17.0)
		if previous.has(key) and not suspended:
			var before: Dictionary = previous[key]
			if role == "human" and alive:
				if swing > float(before.swing) + 0.04:
					_emit("clap" if tool == "hands" else "swish", position + Vector3(0, 1.18, 0), -12.0, 1.0)
				if tool != str(before.tool) and tool != "hands":
					_emit("pickup", position + Vector3(0, 1.0, 0), -18.0, 1.0)
			if role == "mosquito":
				if bool(before.alive) and not alive:
					_emit("impact", position, -11.0, 0.95 + float(int(key) % 4) * 0.025)
				elif alive and state == "biting" and str(before.state) != "biting":
					_emit("bite", position, -19.0, 1.0)
		previous[key] = {"alive":alive, "state":state, "tool":tool, "swing":swing}
	if suspended:
		_stop_all()

func _process(dt: float) -> void:
	since_sync += dt
	# Results stop syncing actors, and the local death screen hides the world.
	# Neither state may keep playing positional information about a live round.
	if since_sync > 0.30 or not is_visible_in_tree() or suspended:
		_stop_all()
		return
	occlusion_age += dt
	var update_occlusion: bool = occlusion_age >= OCCLUSION_INTERVAL
	if update_occlusion:
		occlusion_age = 0.0
	var camera: Camera3D = get_viewport().get_camera_3d()
	if not is_instance_valid(camera):
		return
	var listener: Vector3 = camera.global_position
	var flying_count: int = 0
	for actor_value: Variant in latest_actors.values():
		var actor: Dictionary = actor_value
		if str(actor.get("role", "")) == "mosquito" and bool(actor.get("alive", false)) and str(actor.get("state", "")) == "flying":
			flying_count += 1
	# Three sources retain full level; larger swarms keep comparable mix energy.
	var crowd_db: float = -10.0 * log(maxf(1.0, float(flying_count) / 3.0)) / log(10.0)
	for key: Variant in buzzes:
		var voice: AudioStreamPlayer3D = buzzes[key]
		var actor: Dictionary = latest_actors.get(key, {})
		if not bool(actor.get("alive", false)):
			voice.stop()
			continue
		if latest_views.has(key) and is_instance_valid(latest_views[key]):
			voice.global_position = (latest_views[key] as Node3D).global_position
		if not voice.playing:
			voice.play(float(int(key) % 17) / 17.0)
		if update_occlusion:
			voice.set_meta("blocked", _blocked(listener, voice.global_position))
		var blocked: bool = bool(voice.get_meta("blocked", false))
		var flying: bool = str(actor.get("state", "flying")) == "flying"
		var target_db: float = -23.0 + crowd_db
		if not flying:
			target_db -= 17.0
		if int(key) == local_id:
			target_db -= 9.0
		if blocked:
			target_db -= 15.0
		voice.volume_db = lerpf(voice.volume_db, target_db, minf(dt * 12.0, 1.0))
		voice.pitch_scale = (0.93 + float(int(key) % 7) * 0.025) * (1.0 if flying else 0.73)
		voice.attenuation_filter_cutoff_hz = 1100.0 if blocked else 4600.0

func clear() -> void:
	_stop_all()
	for value: Variant in buzzes.values():
		(value as AudioStreamPlayer3D).queue_free()
	buzzes.clear()
	previous.clear()
	latest_actors = {}
	latest_views = {}
	suspended = true
	since_sync = 10.0

func _stop_all() -> void:
	for value: Variant in buzzes.values():
		(value as AudioStreamPlayer3D).stop()
	for voice: AudioStreamPlayer3D in effect_pool:
		voice.stop()

func _emit(cue: String, position: Vector3, gain_db: float, pitch_value: float) -> void:
	if suspended or effect_pool.is_empty() or not streams.has(cue):
		return
	var voice: AudioStreamPlayer3D = effect_pool[next_effect]
	for candidate: AudioStreamPlayer3D in effect_pool:
		if not candidate.playing:
			voice = candidate
			break
	next_effect = (next_effect + 1) % EFFECT_VOICES
	voice.stop()
	voice.stream = streams[cue]
	voice.position = position
	voice.pitch_scale = pitch_value
	var camera: Camera3D = get_viewport().get_camera_3d()
	var blocked: bool = is_instance_valid(camera) and _blocked(camera.global_position, position)
	voice.volume_db = gain_db - (10.0 if blocked else 0.0)
	voice.attenuation_filter_cutoff_hz = 1400.0 if blocked else 6500.0
	voice.play()
	effects_started[cue] = int(effects_started.get(cue, 0)) + 1

func _blocked(from: Vector3, to: Vector3) -> bool:
	if from.distance_squared_to(to) < 0.0025:
		return false
	var ray := PhysicsRayQueryParameters3D.create(from, to, 1)
	return not get_world_3d().direct_space_state.intersect_ray(ray).is_empty()

static func _build_streams() -> void:
	if not streams.is_empty():
		return
	for cue: String in ["buzz", "clap", "swish", "impact", "bite", "pickup"]:
		streams[cue] = _synthesize(cue)

static func _synthesize(cue: String) -> AudioStreamWAV:
	var seconds: float = {"buzz":1.0, "clap":0.15, "swish":0.18, "impact":0.16, "bite":0.14, "pickup":0.23}.get(cue, 0.15)
	var count: int = roundi(seconds * SAMPLE_RATE)
	var pcm := PackedByteArray()
	pcm.resize(count * 2)
	var random := RandomNumberGenerator.new()
	random.seed = 604271
	var filtered_noise: float = 0.0
	for i: int in range(count):
		var t: float = float(i) / SAMPLE_RATE
		var x: float = float(i) / count
		var noise: float = random.randf_range(-1.0, 1.0)
		filtered_noise = lerpf(filtered_noise, noise, 0.32)
		var sample: float = 0.0
		match cue:
			"buzz":
				# Integral frequencies/modulation yield a seamless one-second loop.
				var phase: float = TAU * 218.0 * t + 0.22 * sin(TAU * 7.0 * t)
				var harmonic: float = 0.58 * sin(phase) + 0.18 * sin(phase * 2.0) + 0.08 * sin(phase * 3.0)
				sample = harmonic * (0.52 + 0.05 * sin(TAU * 4.0 * t) + 0.025 * sin(TAU * 11.0 * t))
			"clap":
				sample = (filtered_noise * 1.3 + sin(TAU * 155.0 * t) * 0.20) * exp(-t * 40.0) * minf(t / 0.0015, 1.0)
			"swish":
				sample = filtered_noise * sin(PI * x) * sin(PI * x) * 0.8
			"impact":
				sample = (sin(TAU * (160.0 * t - 200.0 * t * t)) * 0.48 + filtered_noise * 0.60) * exp(-t * 29.0) * minf(t / 0.001, 1.0)
			"bite":
				sample = (sin(TAU * (560.0 * t - 700.0 * t * t)) * 0.22 + sin(TAU * 1120.0 * t) * 0.05) * sin(PI * x) * exp(-t * 13.0)
			"pickup":
				var note: float = 660.0 if t < 0.085 else 880.0
				var note_time: float = t if t < 0.085 else t - 0.085
				sample = sin(TAU * note * note_time) * 0.26 * exp(-note_time * 22.0) * minf(note_time / 0.003, 1.0)
		if cue != "buzz":
			sample *= minf((seconds - t) / 0.008, 1.0)
		pcm.encode_s16(i * 2, clampi(roundi(sample * 32767.0), -32768, 32767))
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = SAMPLE_RATE
	stream.stereo = false
	stream.data = pcm
	if cue == "buzz":
		stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
		stream.loop_begin = 0
		stream.loop_end = count
	return stream
