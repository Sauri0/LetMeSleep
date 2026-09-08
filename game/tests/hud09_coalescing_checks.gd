extends SceneTree
## Real Client callbacks with inert world/audio: no devices or preferences.
const Client = preload("res://scripts/client.gd")
var checks := 0
var failures: Array[String] = []
var report_path := ""

class QuietClient extends Client:
	func _ready() -> void: pass
	func _process(_dt: float) -> void: pass
	func _physics_process(_dt: float) -> void: pass

class HudWitness extends CanvasLayer:
	var updates: Array[Dictionary] = []
	var screen := "home"
	var paused := false
	func show_game(snapshot: Dictionary, private_data: Dictionary, id: int) -> void:
		screen="game"
		updates.append({"public":snapshot.duplicate(true),"private":private_data.duplicate(true),"id":id})
	func show_results(_data: Dictionary) -> void: screen="results"
	func show_lobby(_data: Dictionary, _id: int) -> void: screen="lobby"
	func set_pause(value: bool) -> void: paused=value

class AudioWitness extends Node:
	var private_updates := 0
	func sync_private(_data: Dictionary) -> void: private_updates+=1
	func sync_doors(_data: Dictionary, _views: Dictionary) -> void: pass
	func sync_pickups(_data: Dictionary) -> void: pass
	func clear() -> void: pass

class WorldWitness extends Node3D:
	var audio_fx: Node
	var door_views: Node3D
	func clear_actors() -> void: pass
	func load_map(_id: String) -> void: pass
	func set_local_role(_id: int, _role: String) -> void: pass
	func sync_doors(_data: Dictionary, _dt: float) -> void: pass
	func show_assignment(_data: Dictionary, _camera: Camera3D, _point: Vector3) -> void: pass
	func end_customization() -> void: pass

class MusicWitness extends Node:
	var contexts := 0
	func set_context(_context: String, _state: Dictionary, _personal: Dictionary, _id: int) -> void:
		contexts+=1

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="): report_path=arg.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("HUD09_COALESCING_FAIL "+label)

func snapshot(time: float) -> Dictionary:
	return {"phase":"playing","time_left":time,"actors":{1:{"role":"human","alive":true,"yaw":0.0}},"config":{"map_id":"house"}}

func drain() -> void:
	# Observe after the engine's deferred queue, not by calling the flush helper.
	await process_frame
	await process_frame

func _run() -> void:
	var old_mouse := Input.mouse_mode
	var client := QuietClient.new()
	var hud := HudWitness.new()
	var world := WorldWitness.new()
	var sound := AudioWitness.new()
	var music := MusicWitness.new()
	var camera := Camera3D.new()
	client.ui=hud;client.world=world;client.music=music;client.camera=camera;client.local_id=1
	world.audio_fx=sound
	client.add_child(hud);client.add_child(world);world.add_child(sound)
	client.add_child(music);client.add_child(camera)
	root.add_child(client)

	client._snapshot(snapshot(100))
	check(hud.screen=="game" and hud.updates.size()==1,"round entry is immediate")
	client._private({"attack":{"id":1,"status":"hit"}})
	await drain()
	check(hud.updates.back().private.attack.id==1,"entry receives fresh private state")
	hud.updates.clear()
	var public := snapshot(99)
	var private_data := {"attack":{"id":2,"status":"hit"},"defense":{"zone":"arm"}}
	var public_copy := public.duplicate(true)
	var private_copy := private_data.duplicate(true)
	var audio_before := sound.private_updates
	var music_before := music.contexts
	client._snapshot(public)
	client._private(private_data)
	check(hud.updates.is_empty(),"steady pair waits only for deferred queue")
	check(client.state==public and client.personal==private_data,"received gameplay state remains immediate")
	check(sound.private_updates==audio_before+1 and music.contexts==music_before+2,"audio callbacks remain immediate and complete")
	await drain()
	check(hud.updates.size()==1,"public/private pair produces one HUD application")
	check(hud.updates.back()=={"public":public,"private":private_data,"id":1},"HUD consumes latest complete pair")
	check(public==public_copy and private_data==private_copy,"input dictionaries unchanged")

	hud.updates.clear()
	client._private({"attack":{"id":3,"status":"blocked"}})
	await drain()
	check(hud.updates.size()==1 and hud.updates.back().private.attack.id==3,"private-only arrival refreshes without another public packet")
	hud.updates.clear()
	client._snapshot(snapshot(98))
	await drain()
	check(hud.updates.size()==1 and hud.updates.back().public.time_left==98,"public-only arrival refreshes without another private packet")
	check(hud.updates.back().private.attack.id==3,"public-only arrival retains latest private state")

	hud.updates.clear()
	for index: int in range(5):
		client._snapshot(snapshot(97-index))
		client._private({"attack":{"id":4+index,"status":"hit"}})
	await drain()
	check(hud.updates.size()==1,"same-frame packet burst coalesces")
	check(hud.updates.back().public.time_left==93 and hud.updates.back().private.attack.id==8,"burst presents newest data, not first queued data")

	hud.updates.clear()
	client._snapshot(snapshot(92))
	client._snapshot({"phase":"results"})
	check(hud.screen=="results" and not client.playing,"results transition stays immediate")
	client._private({"attack":{"id":9}})
	await drain()
	check(hud.screen=="results" and hud.updates.is_empty(),"queued refresh and late private cannot reopen results")

	client._snapshot(snapshot(90))
	hud.updates.clear()
	client._private({"attack":{"id":10}})
	client._lobby({"actors":{}})
	check(hud.screen=="lobby" and client.waiting,"lobby transition stays immediate")
	await drain()
	check(hud.screen=="lobby" and hud.updates.is_empty(),"old round callback cannot reopen lobby")

	client._snapshot(snapshot(80))
	client._private({"attack":{"id":11}})
	client._snapshot({"phase":"results"})
	client._snapshot(snapshot(70))
	client._private({"attack":{"id":12}})
	hud.updates.clear()
	await drain()
	check(hud.updates.size()==1 and hud.updates.back().private.attack.id==12,"queued callback across rematch presents new round once")
	check(hud.updates.back().public.time_left==70,"no previous-round public state survives rematch")
	hud.paused=true
	client._snapshot(snapshot(69));client._private({"attack":{"id":13}})
	await drain()
	check(hud.paused,"steady refresh does not dismiss pause")

	client.queue_free()
	await drain()
	Input.mouse_mode=old_mouse
	var result := {"checks":checks,"failures":failures,"scope":"real Client callbacks and deferred queue; inert HUD/world/audio, no rendered layout, network or FPS claim"}
	if not report_path.is_empty():
		var file := FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null: file.store_string(JSON.stringify(result,"\t"));file.close()
	print("HUD09_COALESCING checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
