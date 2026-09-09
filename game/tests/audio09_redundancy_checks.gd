extends SceneTree
## Production Foley and real physics. Observers count work without replacing it.
const FX = preload("res://scripts/audio_fx.gd")
const Doors = preload("res://scripts/door_catalog.gd")
var checks := 0
var failures: Array[String] = []
var report_path := ""
var baseline := false

class ObservedFX extends FX:
	var queries: Array[Dictionary] = []
	var requests: Array[Dictionary] = []
	var listener_queries := 0
	var no_listener := false
	func _listener_node() -> Node3D:
		listener_queries += 1
		return null if no_listener else super._listener_node()
	func _blocked(from: Vector3, to: Vector3, ignored_body: RID = RID()) -> bool:
		queries.append({"from":from,"to":to,"ignored":ignored_body})
		return super._blocked(from,to,ignored_body)
	func _emit(cue: String, position: Vector3, gain_db: float, pitch_value: float, ignored_body: RID = RID()) -> void:
		requests.append({"cue":cue,"p":position,"gain":gain_db,"pitch":pitch_value})
		super._emit(cue,position,gain_db,pitch_value,ignored_body)

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument == "--baseline": baseline = true
		if argument.begins_with("--report="): report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok: failures.append(label);printerr("AUDIO09_REDUNDANCY_FAIL "+label)

func wall_at(scene: Node3D, z: float) -> StaticBody3D:
	var wall := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size=Vector3(2,3,.05);shape.shape=box
	scene.add_child(wall);wall.add_child(shape);wall.position=Vector3(0,1.5,z)
	return wall

func _run() -> void:
	var scene := Node3D.new();root.add_child(scene)
	var listener := AudioListener3D.new();scene.add_child(listener)
	listener.position=Vector3(0,1.5,0);listener.make_current()
	var fx := ObservedFX.new();scene.add_child(fx);fx.setup();fx.set_process(false)
	fx.acoustic_listener=listener;fx.suspended=false
	var point := Vector3(0,1.5,4)
	fx._emit("land",point,-18,1.1)
	var clear_queries := fx.queries.size()
	check(clear_queries==(2 if baseline else 1),"clear cue uses expected number of real occlusion queries")
	check(int(fx.effects_started.get("land",0))==1,"clear line admits one cue")
	var voice: AudioStreamPlayer3D=fx.effect_pool[0]
	check(voice.position==point and is_equal_approx(voice.volume_db,-18) and is_equal_approx(voice.pitch_scale,1.1),"admitted cue retains position gain and pitch")
	var wall := wall_at(scene,2)
	await physics_frame;await physics_frame
	fx.queries.clear();fx._emit("land",point,-18,1.1)
	check(fx.queries.size()==1 and int(fx.effects_started.land)==1,"actual wall rejects cue with one query")
	fx.queries.clear();fx._emit("land",point,-18,1.1,wall.get_rid())
	var excluded_queries := fx.queries.size()
	check(excluded_queries==(2 if baseline else 1) and int(fx.effects_started.land)==2,"own leaf exclusion retains admitted cue")
	for query: Dictionary in fx.queries:
		check(query.ignored==wall.get_rid(),"own leaf RID reaches physics exclusion")
	var other := wall_at(scene,3)
	await physics_frame;await physics_frame
	fx.queries.clear();fx._emit("land",point,-18,1.1,wall.get_rid())
	check(fx.queries.size()==1 and int(fx.effects_started.land)==2,"other wall still blocks excluded own leaf")
	fx.queries.clear();fx._emit("land",Vector3(0,1.5,11),-18,1.1)
	check(fx.queries.is_empty() and int(fx.effects_started.land)==2,"distance rejection needs no ray")
	fx.suspended=true;fx._emit("land",point,-18,1.1)
	check(fx.queries.is_empty() and int(fx.effects_started.land)==2,"suspended emitter stays silent")
	fx.suspended=false;fx.no_listener=true;fx._emit("land",point,-18,1.1)
	check(fx.queries.is_empty() and int(fx.effects_started.land)==3,"standalone emitter without listener retains prior behavior")
	fx.no_listener=false
	wall.queue_free();other.queue_free();await process_frame;await physics_frame

	var states: Dictionary={}
	for id: String in Doors.get_doors("house"):
		states[id]={"angle":0.0,"moving":false,"blocked":false,"revision":0}
	fx.requests.clear();fx.sync_doors(states)
	check(fx.requests.is_empty(),"first door snapshot initializes without cue")
	fx.listener_queries=0
	for repeat: int in range(100): fx.sync_doors(states)
	var quiet_listener_queries := fx.listener_queries
	check(fx.requests.is_empty(),"100 repeated stationary door snapshots remain silent")
	check(quiet_listener_queries==(100*states.size() if baseline else 0),"stationary doors do not prepare unused listener positions")
	states.kitchen.moving=true;states.kitchen.revision=1
	fx.sync_doors(states);fx.sync_doors(states)
	check(fx.requests.size()==1 and fx.requests.back().cue=="door_move","movement transition emits once")
	states.kitchen.revision=2;fx.sync_doors(states)
	check(fx.requests.size()==2 and fx.requests.back().cue=="door_move","changed moving revision emits once")
	states.kitchen.blocked=true;states.kitchen.revision=3;fx.sync_doors(states)
	check(fx.requests.size()==3 and fx.requests.back().cue=="door_block","blocked transition takes precedence over movement")
	states.kitchen.moving=false;states.kitchen.blocked=false;fx.sync_doors(states)
	check(fx.requests.size()==4 and fx.requests.back().cue=="door_latch","closing transition emits latch")
	fx.sync_doors(states)
	check(fx.requests.size()==4,"repeated closed door is silent")
	var cue_records := fx.requests.duplicate(true)
	fx.clear();scene.queue_free();await process_frame;await create_timer(.25).timeout
	var result := {"checks":checks,"failures":failures,"baseline":baseline,"clear_queries":clear_queries,"excluded_queries":excluded_queries,"quiet_listener_queries":quiet_listener_queries,"door_cues":cue_records,"source_sha256":FileAccess.get_sha256("res://scripts/audio_fx.gd"),"scope":"Production emit/sync_doors with real physics and counting observers; no microphone, CPU cost or FPS claim"}
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null: file.store_string(JSON.stringify(result,"\t"));file.close()
	print("AUDIO09_REDUNDANCY checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
