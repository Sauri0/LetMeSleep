extends SceneTree
## Real spatial emitters and physics; Dummy output, no input device access.
const FX=preload("res://scripts/audio_fx.gd")
var checks:=0
var failures: Array[String]=[]
func check(value: bool,label: String) -> void:
	checks+=1
	if not value: failures.append(label)
func _initialize() -> void: _run.call_deferred()
func _run() -> void:
	var scene:=Node3D.new();root.add_child(scene)
	var camera:=Camera3D.new();scene.add_child(camera);camera.position=Vector3(0,1.62,.738);camera.make_current()
	var listener:=AudioListener3D.new();scene.add_child(listener);listener.position=Vector3(0,1.5,0);listener.make_current()
	var fx:=FX.new();scene.add_child(fx);fx.setup();fx.set_process(false);fx.acoustic_listener=listener
	check(fx._listener_node()==listener,"Foley uses current positional voice listener")
	var actors: Dictionary={1:{"role":"mosquito","alive":true,"state":"flying","p":listener.position},2:{"role":"mosquito","alive":true,"state":"flying","p":Vector3(0,1.5,-7.5)}}
	fx.sync(actors,{},1);fx._process(.13)
	await physics_frame;await process_frame
	check(fx.buzzes[2].playing,"buzz inside listener 8m survives camera offset beyond 8m")
	check(camera.position.distance_to(actors[2].p)>8,"buzz witness distinguishes listener from camera")
	fx.clear();fx.suspended=false
	listener.position=Vector3(0,1.48,0);camera.position=Vector3(0,1.63,-.38)
	var effect_position:=Vector3(0,1.48,9.9)
	check(camera.position.distance_to(effect_position)>10,"effect witness distinguishes 10m distance origins")
	fx._emit("land",effect_position,-18,1)
	check(int(fx.effects_started.get("land",0))==1,"effect inside listener 10m is admitted")
	var wall:=StaticBody3D.new();scene.add_child(wall)
	var shape:=CollisionShape3D.new();var box:=BoxShape3D.new();box.size=Vector3(2,3,.05);shape.shape=box;wall.add_child(shape)
	wall.position=Vector3(0,1.5,-.2)
	await physics_frame;await physics_frame
	check(fx._blocked(camera.position,effect_position) and not fx._blocked(listener.position,effect_position),"wall occludes only offset camera in test witness")
	fx._emit("land",effect_position,-18,1)
	check(int(fx.effects_started.get("land",0))==2,"camera-only wall does not hide sound from actual listener")
	wall.position.z=.2
	await physics_frame;await physics_frame
	fx._emit("land",effect_position,-18,1)
	check(int(fx.effects_started.get("land",0))==2,"wall in actual acoustic path still blocks effect")
	listener.clear_current()
	check(fx._listener_node()==camera,"menus and standalone scenes fall back to camera listener")
	scene.queue_free();await process_frame;await create_timer(.25).timeout
	for failure: String in failures: print("FAIL "+failure)
	print("audio09_listener checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
