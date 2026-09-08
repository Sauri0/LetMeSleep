extends SceneTree
const Catalog = preload("res://scripts/audio_catalog.gd")
const FX = preload("res://scripts/audio_fx.gd")
var checks := 0
var failures := 0
func _initialize() -> void: _run.call_deferred()
func check(ok: bool,label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("TOOL_AUDIO07 %s %s"%["PASS" if ok else "FAIL",label])
func _run() -> void:
	var hashes := {}
	for cue: String in Catalog.TOOL_CUES:
		var stream := Catalog.cue(cue)
		check(stream!=null and stream.get_length()>.05 and stream.get_length()<.6,"bounded readable Foley "+cue)
		var bytes := FileAccess.get_file_as_bytes("res://assets/audio/sfx/"+cue+".ogg")
		var digest := bytes.hex_encode().sha256_text()
		check(not hashes.has(digest),"distinct recorded event "+cue);hashes[digest]=true
	var fx := FX.new();root.add_child(fx);fx.setup()
	var actors := {1:{"role":"human","alive":true,"state":"human","tool":"racket","swing":.5,"p":Vector3.ZERO,"strike":{"active":true,"point":Vector3.UP}},2:{"role":"mosquito","alive":true,"state":"flying","p":Vector3.UP,"tool":"hands"}}
	fx.sync(actors,{},1)
	actors[2].state="stunned";actors[2].impact={"id":1,"tool":"newspaper","kind":"projectile","material":"cloth"}
	fx.sync(actors,{},1);fx.sync(actors,{},1)
	check(int(fx.effects_started.get("hit_newspaper",0))==1 and int(fx.effects_started.get("hit_racket",0))==0,"confirmed projectile keeps paper sound despite nearby equipped racket; duplicate snapshot is silent")
	var pickups := {1:{"tool":"slipper","holder":1,"state":"held","p":Vector3.UP,"impact_id":0}}
	fx.sync_pickups(pickups)
	pickups[1].holder=0;pickups[1].state="flying";fx.sync_pickups(pickups);fx.sync_pickups(pickups)
	check(int(fx.effects_started.get("throw_slipper",0))==1 and int(fx.effects_started.get("drop",0))==0,"one confirmed throw cue without duplicate generic drop")
	pickups[1].state="ground";pickups[1].impact_id=1;pickups[1].impact_material="tile";fx.sync_pickups(pickups);fx.sync_pickups(pickups)
	check(int(fx.effects_started.get("land_tile",0))==1,"surface landing is emitted once from authoritative item event")
	pickups[2]={"tool":"newspaper","holder":1,"state":"held","p":Vector3.UP,"owner":0,"impact_id":0}
	fx.sync_pickups(pickups)
	pickups[2].holder=0;pickups[2].owner=1;pickups[2].state="ground";pickups[2].impact_id=1;pickups[2].impact_material="wood"
	fx.sync_pickups(pickups);fx.sync_pickups(pickups)
	check(int(fx.effects_started.get("throw_newspaper",0))==1 and int(fx.effects_started.get("drop",0))==0,"short flight between snapshots retains confirmed throw sound")
	fx.clear();fx.queue_free();await process_frame
	print("TOOL_AUDIO07_RESULT checks=%d failures=%d"%[checks,failures]);quit(0 if failures==0 else 1)
