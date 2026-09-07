extends Node3D
## Offline acoustic Foley. Only confirmed public actor transitions produce cues.
## No zone/assignment data enters this module. Master volume belongs to Preferences.

const Catalog = preload("res://scripts/audio_catalog.gd")
const Maps = preload("res://scripts/map_catalog.gd")
const MAX_BUZZ_VOICES := 6
const SAMPLE_RATE: int = 44100
const EFFECT_VOICES: int = 12
const OCCLUSION_INTERVAL: float = 0.12
static var streams: Dictionary = {}

func _exit_tree() -> void:
	_stop_all()
	for value: Variant in buzzes.values():
		(value as AudioStreamPlayer3D).stream = null
	for voice: AudioStreamPlayer3D in effect_pool:
		voice.stream = null
	streams.clear()

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
var map_id := "house"
var private_age := 10.0
var private_previous: Dictionary = {}
var focus_player: AudioStreamPlayer
var help_player: AudioStreamPlayer
var ambience_pool: Array[AudioStreamPlayer3D] = []

func setup() -> void:
	if not effect_pool.is_empty():
		return
	Catalog.ensure_buses()
	_build_streams()
	for i: int in range(EFFECT_VOICES):
		var voice := AudioStreamPlayer3D.new()
		voice.name = "Cue%d" % i
		voice.bus = &"Effects"
		voice.unit_size = 1.4
		voice.max_distance = 10.0
		voice.max_db = -9.0
		voice.panning_strength = 0.8
		voice.attenuation_filter_cutoff_hz = 6500.0
		add_child(voice)
		effect_pool.append(voice)
	for cue: String in ["focus_loop", "help_loop"]:
		var player := AudioStreamPlayer.new()
		player.bus = &"Effects"
		player.stream = streams.get(cue)
		player.volume_db = -15.0
		add_child(player)
		if cue == "focus_loop": focus_player = player
		else: help_player = player
	set_context(map_id)

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
				buzz.stream = streams.get("buzz")
				buzz.bus = &"Effects"
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
			if not alive or state=="stunned" or suspended:
				voice.stop()

		if previous.has(key) and not suspended:
			var before: Dictionary = previous[key]
			if role == "human" and alive:
				if swing > float(before.swing) + 0.04:
					var strike_tool := str(Dictionary(actor.get("strike", {})).get("tool", tool))
					_emit("swish" if strike_tool == "hands" else "tool_" + strike_tool, position + Vector3(0, 1.18, 0), -12.0, 1.0)
				if tool != str(before.tool):
					_emit("drop" if tool == "hands" else "pickup", position + Vector3(0, 1.0, 0), -13.0, 1.0)
				var grounded := bool(actor.get("grounded", true))
				if grounded and not bool(before.get("grounded", true)):
					_emit("land", position, -12.0, 1.0)
				elif grounded and float(actor.get("motion_speed", 0.0)) > .2 and int(floor(float(actor.get("motion_phase", 0.0))/PI)) != int(floor(float(before.get("motion", 0.0))/PI)):
					_emit(_step_cue(position), position+Vector3(0,.1,0), -12.0 if bool(actor.get("sprinting", false)) else -18.0, .95+float(int(key)%5)*.025)
			if role == "mosquito":
				if (bool(before.alive) and not alive) or (alive and state=="stunned" and str(before.state)!="stunned"):
					_emit("impact", position, -9.0, 0.95 + float(int(key) % 4) * 0.025)
					_emit(_impact_material(position), position, -12.0, 1.0)
					if state == "stunned": _emit("stun", position, -14.0, 1.0)
				elif str(before.state) == "stunned" and state == "flying":
					_emit("recover", position, -14.0, 1.0)
				elif str(before.state) == "biting" and state == "flying":
					_emit("detach", position, -15.0, 1.0)
				elif state == "perched" and str(before.state) != "perched":
					_emit("perch", position, -16.0, 1.0)
				elif alive and state == "biting" and str(before.state) != "biting":
					_emit("bite", position, -19.0, 1.0)
				if state == "stunned" and bool(actor.get("grounded", false)) and not bool(before.get("grounded", false)):
					_emit("fall", position, -12.0, 1.0)
		previous[key] = {"alive":alive, "state":state, "tool":tool, "swing":swing, "grounded":actor.get("grounded", true), "motion":actor.get("motion_phase", 0.0)}
	if suspended:
		_stop_all()

func _process(dt: float) -> void:
	since_sync += dt
	private_age += dt
	if private_age > .3:
		if is_instance_valid(focus_player): focus_player.stop()
		if is_instance_valid(help_player): help_player.stop()
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
	# Fixed headroom for the six-voice cap; hidden swarm size cannot alter the mix.
	var crowd_db := -3.0
	var candidate_ids: Array = buzzes.keys()
	candidate_ids.sort_custom(func(a: Variant,b: Variant) -> bool: return listener.distance_squared_to(buzzes[a].global_position) < listener.distance_squared_to(buzzes[b].global_position))
	var playing_count := 0
	for key: Variant in candidate_ids:
		var voice: AudioStreamPlayer3D = buzzes[key]
		var actor: Dictionary = latest_actors.get(key, {})
		if not bool(actor.get("alive", false)) or str(actor.get("state",""))=="stunned":
			voice.stop()
			continue
		if latest_views.has(key) and is_instance_valid(latest_views[key]):
			voice.global_position = (latest_views[key] as Node3D).global_position
		if update_occlusion or not voice.has_meta("blocked"):
			voice.set_meta("blocked", _blocked(listener, voice.global_position))
		var blocked: bool = bool(voice.get_meta("blocked", false))
		if listener.distance_to(voice.global_position) >= 8.0 or (blocked and int(key) != local_id) or playing_count >= MAX_BUZZ_VOICES:
			voice.stop()
			continue
		playing_count += 1
		var state := str(actor.get("state", "flying"))
		var cue := "buzz_bite" if state == "biting" else ("buzz_perch" if state == "perched" else "buzz")
		if voice.get_meta("cue", "") != cue:
			voice.stop()
			voice.stream = streams.get(cue)
			voice.set_meta("cue", cue)
		if not voice.playing and voice.stream != null:
			voice.play(float(int(key) % 17) / 17.0)
		var flying: bool = str(actor.get("state", "flying")) == "flying"
		var target_db: float = -15.0 + crowd_db
		if not flying:
			target_db -= 6.0
		if int(key) == local_id:
			target_db -= 9.0
		if blocked:
			target_db -= 15.0
		voice.volume_db = lerpf(voice.volume_db, target_db, minf(dt * 12.0, 1.0))
		voice.pitch_scale = (0.93 + float(int(key) % 7) * 0.025) * (1.0 if flying else 0.73)
		voice.attenuation_filter_cutoff_hz = 1100.0 if blocked else 4600.0

	for ambient: AudioStreamPlayer3D in ambience_pool:
		if listener.distance_to(ambient.global_position) > 7.0:
			ambient.stop()
			continue
		if not ambient.playing: ambient.play()
		if update_occlusion: ambient.set_meta("blocked", _blocked(listener, ambient.global_position))
		ambient.volume_db = -32.0 if bool(ambient.get_meta("blocked", false)) else -18.0
		ambient.attenuation_filter_cutoff_hz = 900.0 if bool(ambient.get_meta("blocked", false)) else 4200.0

func clear() -> void:
	_stop_all()
	for value: Variant in buzzes.values():
		(value as AudioStreamPlayer3D).queue_free()
	buzzes.clear()
	previous.clear()
	private_previous.clear()
	private_age = 10.0
	latest_actors = {}
	latest_views = {}
	suspended = true
	since_sync = 10.0

func _stop_all() -> void:
	if is_instance_valid(focus_player): focus_player.stop()
	if is_instance_valid(help_player): help_player.stop()
	for ambient: AudioStreamPlayer3D in ambience_pool: ambient.stop()
	for value: Variant in buzzes.values():
		(value as AudioStreamPlayer3D).stop()
	for voice: AudioStreamPlayer3D in effect_pool:
		voice.stop()

func _emit(cue: String, position: Vector3, gain_db: float, pitch_value: float) -> void:
	if suspended or effect_pool.is_empty() or not streams.has(cue):
		return
	var listener_camera := get_viewport().get_camera_3d()
	if is_instance_valid(listener_camera) and (listener_camera.global_position.distance_to(position) > 10.0 or _blocked(listener_camera.global_position, position)):
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

func _impact_material(position: Vector3) -> String:
	for actor_value: Variant in latest_actors.values():
		var actor: Dictionary = actor_value
		var strike: Dictionary = actor.get("strike", {})
		if str(actor.get("role", "")) != "human" or not bool(strike.get("active", false)) or not strike.has("point"):
			continue
		if Vector3(strike.point).distance_to(position) <= .3:
			var tool := str(strike.get("tool", "hands"))
			return "clap" if tool == "hands" else "tool_" + tool
	return "clap"

static func _build_streams() -> void:
	if not streams.is_empty(): return
	for cue: String in Catalog.CUES:
		var stream := Catalog.cue(cue)
		if stream != null: streams[cue] = stream

func set_context(next_map: String) -> void:
	map_id = next_map if next_map in ["house", "lobby"] else "house"
	for voice: AudioStreamPlayer3D in ambience_pool: voice.queue_free()
	ambience_pool.clear()
	if effect_pool.is_empty(): return
	var sources: Array[Dictionary] = []
	if map_id == "lobby":
		sources.append({"cue":"night_air", "p":Vector3(0,2.1,-2.5)})
	else:
		sources.append({"cue":"room_fan", "p":Vector3(5,1.6,-1.6)})
		sources.append({"cue":"room_fridge", "p":Vector3(-6.95,.7,-6.94)})
		sources.append({"cue":"clock", "p":Vector3(0,1.9,10.65)})
		sources.append({"cue":"night_air", "p":Vector3(-5,4.5,-10.4)})
	for source: Dictionary in sources:
		if not streams.has(source.cue): continue
		var voice := AudioStreamPlayer3D.new()
		voice.bus = &"Ambience"
		voice.stream = streams[source.cue]
		voice.position = source.p
		voice.volume_db = -18.0
		voice.max_db = -18.0
		voice.unit_size = 1.5
		voice.max_distance = 7.0
		add_child(voice)
		ambience_pool.append(voice)

func _step_cue(position: Vector3) -> String:
	for room: Dictionary in Maps.get_map(map_id).get("rooms", []):
		if AABB(room.bounds).has_point(position+Vector3(0,.2,0)):
			var label := str(room.name).to_lower()
			if "cocina" in label or "baño" in label or "lavadero" in label: return "step_tile"
			if "dormitorio" in label or "cuarto" in label: return "step_cloth"
	return "step_wood"

func sync_private(personal: Dictionary) -> void:
	setup()
	private_age = 0.0
	if suspended:
		focus_player.stop()
		help_player.stop()
		return
	_set_local_loop(focus_player, str(Dictionary(personal.get("focus", {})).get("state", "")) == "charging")
	_set_local_loop(help_player, str(Dictionary(personal.get("help", {})).get("state", "")) == "helping")
	var task: Dictionary = personal.get("task", {})
	var old_task: Dictionary = private_previous.get("task", {})
	var position: Vector3 = latest_actors.get(local_id, {}).get("p", Vector3.ZERO)
	if not private_previous.is_empty():
		if not task.is_empty() and task != old_task and (old_task.is_empty() or task.get("station", -1) != old_task.get("station", -1)):
			_emit("task_start", position+Vector3(0,1,0), -13.0, 1.0)
		if int(personal.get("failures", 0)) > int(private_previous.get("failures", 0)):
			_emit("task_fail", position+Vector3(0,1,0), -11.0, 1.0)
		elif not old_task.is_empty() and task.is_empty():
			_emit("task_done", position+Vector3(0,1,0), -11.0, 1.0)
	private_previous = {"task":task.duplicate(), "failures":personal.get("failures", 0)}

func _set_local_loop(player: AudioStreamPlayer, active: bool) -> void:
	if active and player.stream != null:
		if not player.playing: player.play()
	else: player.stop()
