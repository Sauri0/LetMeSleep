extends SceneTree
const Fx = preload("res://scripts/audio_fx.gd")
const Catalog = preload("res://scripts/audio_catalog.gd")
const Music = preload("res://scripts/music_director.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition: failures += 1
	print("AUDIO06 %s %s" % ["PASS" if condition else "FAIL",label])

func _run() -> void:
	Catalog.ensure_buses()
	var initial_buses := AudioServer.bus_count
	Catalog.ensure_buses()
	check(initial_buses == AudioServer.bus_count,"audio buses idempotent")
	for bus: StringName in Catalog.BUSES:
		check(AudioServer.get_bus_index(bus) >= 1,"separate %s bus" % bus)
	var total_seconds := 0.0
	for name: String in Catalog.CUES:
		var sound: AudioStream = Catalog.cue(name)
		check(sound != null,"asset %s loaded" % name)
		if sound == null: continue
		check(sound.get_length() > .04 and sound.get_length() < 10.0,"bounded %s duration" % name)
		check(sound is AudioStreamOggVorbis and sound.loop == (name in Catalog.LOOPS),"correct %s loop policy" % name)
		total_seconds += sound.get_length()
	var fx := Fx.new()
	root.add_child(fx)
	fx.setup()
	fx.set_process(false)
	fx.set_context("lobby")
	check(fx.ambience_pool.size()==1 and fx.map_id=="lobby","real lobby audio context creates one ambience source")
	fx.set_context("house")
	check(fx.ambience_pool.size()==4 and fx.map_id=="house","real house audio context restores room sources")
	await process_frame
	var camera := Camera3D.new()
	root.add_child(camera)
	camera.position = Vector3(0,1.2,0)
	camera.make_current()
	var slab := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(8,.2,8)
	shape.shape = box
	slab.add_child(shape)
	root.add_child(slab)
	slab.position = Vector3(0,3.1,0)
	await physics_frame
	await physics_frame
	var actors := {1:{"role":"human","alive":true,"tool":"hands","swing":0.0,"p":Vector3.ZERO,"state":"standing"}}
	for id: int in range(2,14):
		actors[id] = {"role":"mosquito","alive":true,"state":"flying","p":Vector3(float(id%4)*.25,1.2,-1.0-float(id%3)*.1),"grounded":false}
	actors[2].p = Vector3(0,4.4,0)
	fx.sync(actors,{},1)
	fx._process(.2)
	check(fx.effect_pool.size()==12,"bounded 12 positional one-shot voices")
	check(fx.buzzes.size()==12,"12 actor sources tracked")
	var playing := 0
	for voice: AudioStreamPlayer3D in fx.buzzes.values():
		if voice.playing: playing += 1
	check(playing == 6,"only six nearest audible buzzing voices play")
	check(not fx.buzzes[2].playing and fx.buzzes[2].get_meta("blocked",false),"floor hides buzzing source completely")
	check(fx.effects_started.is_empty(),"first snapshot does not invent Foley")
	actors[1].swing = .8
	actors[1].strike = {"tool":"hands"}
	for i: int in range(25): fx.sync(actors,{},1)
	check(fx.effects_started.get("swish",0)==1 and fx.effects_started.get("clap",0)==0,"manual miss gesture makes one swish and no fake clap hit")
	actors[1].tool = "racket"
	fx.sync(actors,{},1)
	fx.sync(actors,{},1)
	check(fx.effects_started.get("pickup",0)==1,"confirmed equip emits once")
	actors[3].state = "biting"
	fx.sync(actors,{},1)
	fx.sync(actors,{},1)
	check(fx.effects_started.get("bite",0)==1,"actual bite emits once")
	actors[3].state = "stunned"
	for i: int in range(30): fx.sync(actors,{},1)
	check(fx.effects_started.get("impact",0)==1 and fx.effects_started.get("stun",0)==1 and not fx.buzzes[3].playing,"stun emits once and stops wings")
	actors[3].grounded = true
	fx.sync(actors,{},1)
	fx.sync(actors,{},1)
	check(fx.effects_started.get("fall",0)==1,"ground impact follows grounded transition")
	actors[3].state = "flying"
	fx.sync(actors,{},1)
	fx.sync(actors,{},1)
	check(fx.effects_started.get("recover",0)==1,"recovery emits once")
	fx.sync_private({"assignment":{"human":99,"zone":5},"focus":{"state":"ready","can_focus":true}})
	check(not fx.focus_player.playing,"available private mark is silent")
	fx.sync_private({"focus":{"state":"charging"}})
	check(fx.focus_player.playing,"own active focus has a local cue")
	fx.sync_private({"focus":{"state":"helping"},"help":{"state":"helping"}})
	check(not fx.focus_player.playing and fx.help_player.playing,"help replaces focus cue")
	fx.sync_private({"focus":{"state":"ready"},"help":{"state":"ready"}})
	check(not fx.help_player.playing,"help releases immediately when stopped")
	fx.sync_private({"task":{},"failures":0})
	fx.sync_private({"task":{"station":2,"remaining":30.0,"progress":0.0},"failures":0})
	fx.sync_private({"task":{"station":2,"remaining":29.9,"progress":.1},"failures":0})
	check(fx.effects_started.get("task_start",0)==1,"task assignment emits once without countdown spam")
	fx.sync_private({"task":{},"failures":1})
	check(fx.effects_started.get("task_fail",0)==1 and fx.effects_started.get("task_done",0)==0,"failure is never played as completion")
	var safe := {"time_left":90.0,"actors":{1:{"role":"human","bitten":false},2:{"role":"mosquito","state":"flying","p":Vector3(0,4,0)}}}
	var baseline: Vector3 = Music.local_layers(safe,{},1)
	safe.actors[2].state = "biting"
	safe.actors[2].p = Vector3.ZERO
	check(Music.local_layers(safe,{},1)==baseline,"enemy state/location cannot change musical tension")
	check(Music.local_layers(safe,{"assignment":{"zone":7,"human":8}},1)==baseline,"unavailable/assigned marks cannot change music")
	safe.actors[1].bitten = true
	check(Music.local_layers(safe,{},1).y > baseline.y,"own perceived bite may raise activity layer")
	safe.time_left = 15.0
	check(Music.local_layers(safe,{},1).z > baseline.z,"public final clock may raise tension")
	fx._process(.31)
	playing = 0
	for voice: AudioStreamPlayer3D in fx.buzzes.values():
		if voice.playing: playing += 1
	check(playing==0 and not fx.focus_player.playing and not fx.help_player.playing,"stale stream stops all positional/local loops")
	fx.sync(actors,{},3)
	actors[3].alive = false
	fx.sync(actors,{},3)
	check(fx.suspended,"dead survival listener has no positional spectator audio")
	fx.clear()
	await process_frame
	check(fx.buzzes.is_empty() and fx.private_previous.is_empty(),"leaving clears public/private audio state")
	fx.queue_free()
	slab.queue_free()
	camera.queue_free()
	await process_frame
	Catalog.cache.clear()
	await create_timer(.2).timeout
	print("AUDIO06_RESULT checks=%d failures=%d catalog_seconds=%.3f" % [checks,failures,total_seconds])
	quit(failures)
