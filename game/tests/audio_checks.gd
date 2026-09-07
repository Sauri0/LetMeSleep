extends SceneTree

var failures: int = 0

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	print("AUDIO_CHECK %s %s" % ["PASS" if condition else "FAIL", label])
	if not condition:
		failures += 1

func _run() -> void:
	var world: Node3D = load("res://scripts/world.gd").new()
	root.add_child(world)
	world.build()
	world.load_map("house")
	var fx: Node3D = world.audio_fx
	fx.set_process(false)
	var streams: Dictionary = load("res://scripts/audio_fx.gd").streams
	check(streams.size() == 6, "six original streams generated")
	var total_bytes: int = 0
	for key: Variant in streams:
		var stream: AudioStreamWAV = streams[key]
		check(stream.format == AudioStreamWAV.FORMAT_16_BITS and not stream.stereo and stream.mix_rate == 22050, "%s mono PCM16 22050Hz" % key)
		var peak: int = 0
		var bytes: PackedByteArray = stream.data
		total_bytes += bytes.size()
		for i: int in range(0, bytes.size(), 2):
			peak = maxi(peak, absi(bytes.decode_s16(i)))
		check(peak > 500 and peak < 30000, "%s nonempty waveform with headroom" % key)
	check(total_bytes < 100000, "all cached audio under 100KB PCM")
	check(streams.buzz.loop_mode == AudioStreamWAV.LOOP_FORWARD and streams.buzz.loop_end == 22050, "buzz loops by sample count")
	var actors: Dictionary = {1:{"role":"human","alive":true,"tool":"hands","swing":0.0,"p":Vector3.ZERO}}
	for id: int in range(2,14):
		actors[id] = {"role":"mosquito","alive":true,"state":"flying","p":Vector3(float(id % 4)*0.3,1.2,-1.0)}
	actors[2].p = Vector3(0,4.4,0)
	var camera := Camera3D.new()
	root.add_child(camera)
	camera.position = Vector3(0,1.2,0)
	camera.look_at(Vector3(0,1.2,-1))
	camera.make_current()
	world.sync_actors(actors, 1, 0.05)
	await physics_frame
	await physics_frame
	fx._process(0.20)
	check(fx.buzzes.size() == 12 and fx.effect_pool.size() == 12, "swarm 12 plus bounded effect pool")
	check(fx.effects_started.is_empty(), "initial snapshot emits no false confirmation")
	check(bool(fx.buzzes[2].get_meta("blocked", false)), "upper floor occludes buzzing source")
	check(fx.buzzes[2].volume_db <= fx.buzzes[3].volume_db - 14.0, "occluded buzzing reduced 15dB")
	var flying_db: float = fx.buzzes[3].volume_db
	actors[3].state = "perched"
	fx.sync(actors, world.actors, 1)
	fx._process(0.20)
	check(fx.buzzes[3].volume_db < flying_db - 15.0, "perched buzzing quieter")
	actors[1].swing = 0.45
	fx.sync(actors, world.actors, 1)
	for i: int in range(40):
		fx.sync(actors, world.actors, 1)
	check(int(fx.effects_started.get("clap", 0)) == 1, "confirmed swing emits exactly one clap across repeated frames")
	actors[1].tool = "racket"
	fx.sync(actors, world.actors, 1)
	fx.sync(actors, world.actors, 1)
	check(int(fx.effects_started.get("pickup", 0)) == 1, "confirmed equip emits once")
	actors[4].state = "biting"
	fx.sync(actors, world.actors, 1)
	fx.sync(actors, world.actors, 1)
	check(int(fx.effects_started.get("bite", 0)) == 1, "confirmed bite emits once")
	actors[4].alive = false
	actors[4].state = "dead"
	fx.sync(actors, world.actors, 1)
	fx.sync(actors, world.actors, 1)
	check(int(fx.effects_started.get("impact", 0)) == 1 and not fx.buzzes[4].playing, "confirmed death emits one impact and stops its buzz")
	actors[4].alive = true
	actors[4].state = "flying"
	fx.sync(actors, world.actors, 1)
	check(fx.buzzes[4].playing, "respawn restores buzzing")
	fx._process(0.31)
	var all_stopped: bool = true
	for value: Variant in fx.buzzes.values():
		all_stopped = all_stopped and not value.playing
	check(all_stopped, "end of actor sync stops round audio")
	fx.sync(actors, world.actors, 4)
	actors[4].alive = false
	fx.sync(actors, world.actors, 4)
	all_stopped = true
	for value: Variant in fx.buzzes.values():
		all_stopped = all_stopped and not value.playing
	check(all_stopped and fx.suspended, "dead listener hears no continuing positional cues")
	world.clear_actors()
	await process_frame
	await process_frame
	check(fx.buzzes.is_empty() and fx.get_child_count() == 12, "clear removes swarm nodes and retains only reusable pool")
	print("AUDIO_CHECK_RESULT failures=%d bytes=%d" % [failures,total_bytes])
	await create_timer(0.12).timeout
	quit(failures)
