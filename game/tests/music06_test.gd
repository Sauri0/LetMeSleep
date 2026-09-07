extends SceneTree
const Director = preload("res://scripts/music_director.gd")
const Catalog = preload("res://scripts/audio_catalog.gd")
const Prefs = preload("res://scripts/preferences.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition: failures += 1
	print("MUSIC06 %s %s" % ["PASS" if condition else "FAIL",label])

func _run() -> void:
	var music := Director.new()
	root.add_child(music)
	for theme: String in ["menu", "gameplay"]:
		var length := 0.0
		for layer: String in ["base", "rhythm", "melody"]:
			var stream := Catalog.music_layer(theme,layer)
			check(stream != null,"%s %s asset" % [theme,layer])
			if stream == null: continue
			if length == 0.0: length = stream.get_length()
			check(absf(length-stream.get_length()) < 1.0/44100.0,"%s %s identical sample duration" % [theme,layer])
			check(stream.loop and stream.bpm == 104.0 and stream.beat_count == 128,"%s %s loop and bar metadata" % [theme,layer])
		check(absf(length-Catalog.MUSIC_SECONDS)<1.0/44100.0,"%s 32-bar duration" % theme)
	check(Catalog.quiet_theme()!=null and absf(Catalog.quiet_theme().get_length()-72.0)<.001,"calm B theme is 72 seconds")
	for name: String in Catalog.ACCENTS:
		var stream := Catalog.accent(name)
		check(stream!=null and not stream.loop and stream.get_length()<=3.0,"bounded one-shot accent %s" % name)
	check(music.game_stream is AudioStreamSynchronized and music.game_stream.stream_count==3,"gameplay uses sample-synchronized stream")
	var recorder := AudioEffectRecord.new()
	recorder.format = AudioStreamWAV.FORMAT_16_BITS
	AudioServer.add_bus_effect(0,recorder)
	recorder.set_recording_active(true)
	music.set_context("home")
	await create_timer(1.0).timeout
	check(music.menu_player.playing and music.menu_player.volume_db > -12.0,"menu fades in audibly")
	var before := music.menu_player.get_playback_position()
	for i: int in range(25): music.set_context("home")
	await create_timer(.25).timeout
	check(music.menu_player.get_playback_position()>before,"repeated menu updates do not restart theme")
	music.set_context("lobby")
	await create_timer(.7).timeout
	check(music.menu_player.get_playback_position()>before and music.menu_stream.get_sync_stream_volume(2)<-7.0,"lobby preserves cursor and lightens arrangement")
	music.set_context("customize")
	await create_timer(.7).timeout
	check(music.quiet_player.playing and music.quiet_player.volume_db > music.menu_player.volume_db,"customization crossfades to calm theme")
	var snapshot := {"time_left":90.0,"actors":{1:{"role":"mosquito","state":"flying","alive":true}}}
	music.set_context("playing",snapshot,{},1)
	for i: int in range(40): music.set_context("playing",snapshot,{},1)
	await create_timer(.7).timeout
	check(music.game_player.playing and music.accents_started.get("start",0)==1,"one round-start accent despite duplicate snapshots")
	var own := {"focus":{"state":"charging"}}
	music.set_context("playing",snapshot,own,1)
	music._process(Director.BAR_SECONDS)
	check(music.applied_layers.y == -9.0,"own action layer applied on musical bar")
	snapshot.time_left = 10.0
	music.set_context("playing",snapshot,own,1)
	music._process(Director.BAR_SECONDS)
	check(music.applied_layers.z == -11.0,"final clock activates restrained tension layer")
	snapshot.actors[1].state = "stunned"
	for i: int in range(25): music.set_context("playing",snapshot,{},1)
	check(music.accents_started.get("stun",0)==1,"stun motif never repeats during 35-second wait")
	snapshot.actors[1].state = "flying"
	music.set_context("playing",snapshot,{},1)
	check(music.accents_started.get("recover",0)==1,"actual own recovery emits motif once")
	music.set_context("playing",snapshot,{"task":{"station":1},"failures":0},1)
	music.set_context("playing",snapshot,{"task":{},"failures":1},1)
	check(music.accents_started.get("task",0)==0,"failed task does not play success accent")
	music.set_context("playing",snapshot,{"task":{"station":2},"failures":1},1)
	music.set_context("playing",snapshot,{"task":{},"failures":1},1)
	music.set_context("playing",snapshot,{"task":{},"failures":1},1)
	check(music.accents_started.get("task",0)==1,"own completed task plays one musical accent")
	music.ui_cue("confirm")
	for i: int in range(40): music.ui_cue("confirm")
	check(music.ui_started.get("confirm",0)==1,"UI polyphony/rate bound")
	snapshot.winner = "mosquito"
	music.set_context("results",snapshot,{},1)
	for i: int in range(20): music.set_context("results",snapshot,{},1)
	check(music.accents_started.get("win",0)==1,"validated winner creates one result cadence")
	await create_timer(1.0).timeout
	var saved_music := Prefs.music_volume
	Prefs.music_volume = 0.0
	Prefs.apply_audio()
	check(AudioServer.is_bus_mute(AudioServer.get_bus_index("Music")),"music zero mutes only its bus")
	check(not AudioServer.is_bus_mute(AudioServer.get_bus_index("Effects")),"effects remain independently enabled")
	Prefs.music_volume = saved_music
	Prefs.apply_audio()
	music.clear()
	check(not music.menu_player.playing and not music.game_player.playing and not music.quiet_player.playing,"clear stops every music context")
	await create_timer(.2).timeout
	recorder.set_recording_active(false)
	var recording: AudioStreamWAV = recorder.get_recording()
	var peak := 0
	if recording != null:
		var recorded_bytes: PackedByteArray = recording.data
		for i: int in range(0,recorded_bytes.size(),2): peak=maxi(peak,absi(recorded_bytes.decode_s16(i)))
	check(recording!=null and recording.data.size()>44100 and peak>100,"actual Godot master mix contains audible nonzero PCM")
	check(peak<32000,"actual layered master mix retains clipping headroom")
	if recording!=null:
		recording.save_to_wav(ProjectSettings.globalize_path("res://../work/audio06-godot-mix.wav"))
	AudioServer.remove_bus_effect(0,AudioServer.get_bus_effect_count(0)-1)
	recording = null
	recorder = null
	music.queue_free()
	await process_frame
	Catalog.cache.clear()
	await create_timer(.2).timeout
	print("MUSIC06_RESULT checks=%d failures=%d recorded_peak=%d" % [checks,failures,peak])
	quit(failures)
