extends SceneTree
const Client = preload("res://scripts/client.gd")
const CameraRules = preload("res://scripts/mosquito_camera.gd")
const ArenaData = preload("res://scripts/arena.gd")
const Prefs = preload("res://scripts/preferences.gd")
var checks := 0
var failures: Array[String] = []
var report_path := ""

class Menu extends CanvasLayer:
	var blocked := false
	func is_menu_open() -> bool: return blocked

class Transport extends Node:
	var inputs: Array[Dictionary] = []
	var actions: Array[Dictionary] = []
	var acknowledgments: Array[Vector2] = []
	func send_input(_sequence: int, move: Vector3, yaw: float, pitch: float, interact: bool, _sprint: bool, _crouch: bool, _jump: bool) -> void:
		inputs.append({"move":move,"yaw":yaw,"pitch":pitch,"interact":interact})
	func send_action(_sequence: int, verb: String, yaw: float, pitch: float) -> void:
		actions.append({"verb":verb,"yaw":yaw,"pitch":pitch})
	func send_view_ack(_sequence: int, _revision: int, _first_input: int, yaw: float, pitch: float) -> void:
		acknowledgments.append(Vector2(yaw,pitch))

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--report="): report_path=arg.trim_prefix("--report=")
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures.append(label);printerr("MOSQUITO092_CAMERA_FAIL "+label)

func wheel(up: bool, factor: float=1.0) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index=MOUSE_BUTTON_WHEEL_UP if up else MOUSE_BUTTON_WHEEL_DOWN
	event.pressed=true
	event.factor=factor
	return event

func _client_controls() -> void:
	Prefs.setup_inputs()
	var client := Client.new()
	var menu := Menu.new()
	var transport := Transport.new()
	client.ui=menu;client.network=transport;client.playing=true;client.local_id=1
	client.state={"actors":{1:{"role":"mosquito","state":"flying","alive":true,"yaw":.2,"pitch":.1}}}
	client.role="human"
	var original_zoom: float=client.mosquito_zoom_target
	client._unhandled_input(wheel(false))
	check(client.mosquito_zoom_target==original_zoom,"human wheel cannot alter camera")
	client.role="mosquito"
	client._unhandled_input(wheel(false,.25))
	check(is_equal_approx(client.mosquito_zoom_target,original_zoom+.04),"fractional wheel yields continuous zoom")
	for step: int in range(40): client._unhandled_input(wheel(true))
	check(client.mosquito_zoom_target==0.0,"wheel reaches first person")
	for step: int in range(40): client._unhandled_input(wheel(false))
	check(client.mosquito_zoom_target==CameraRules.MAX_REACH,"far zoom bounded")
	menu.blocked=true;client._unhandled_input(wheel(true));menu.blocked=false
	check(client.mosquito_zoom_target==CameraRules.MAX_REACH,"menu does not change zoom")
	client.yaw=1.1;client.pitch=.35
	client._physics_process(.04)
	var sent: Dictionary=transport.inputs.back()
	check(Vector2(sent.yaw,sent.pitch).is_equal_approx(Vector2(.2,.1)),"idle free look retains transmitted body orientation")
	Input.action_press("move_forward")
	client._physics_process(.04)
	Input.action_release("move_forward")
	sent=transport.inputs.back()
	var flight := ArenaData.flight_direction(sent.move,sent.yaw,sent.pitch).normalized()
	var camera_forward: Vector3=-Basis(CameraRules.target(client.state.actors[1],client.yaw,client.pitch).basis).z
	check(flight.dot(camera_forward)>.99999,"W flies along camera direction through actual input callback")
	client.yaw=-.7;client.pitch=-.2;client._physics_process(.04)
	sent=transport.inputs.back()
	check(Vector2(sent.yaw,sent.pitch).is_equal_approx(Vector2(1.1,.35)),"stopping decouples further camera motion")
	Input.action_press("bite");client._physics_process(.04);Input.action_release("bite")
	sent=transport.inputs.back()
	check(sent.interact and Vector2(sent.yaw,sent.pitch).is_equal_approx(Vector2(-.7,-.2)),"focus still uses current camera aim")
	var action := InputEventAction.new();action.action="perch";action.pressed=true
	client._unhandled_input(action)
	check(transport.actions.back()=={"verb":"perch","yaw":client.yaw,"pitch":client.pitch},"perch action retains aim and binding")
	client.state.actors[1].state="perched"
	client.state.actors[1].surface_normal=Vector3.DOWN
	client.state.actors[1].surface_forward=Vector3.FORWARD
	client.yaw=.8;client.pitch=.25
	var before := client.state.duplicate(true)
	var still := client._movement_view(Vector3.ZERO,false)
	check(still.is_equal_approx(Vector2(-.7,-.2)) and client.state==before,"ceiling free look does not mutate surface state")
	check(client._movement_view(Vector3.FORWARD,false)==Vector2(.8,.25),"surface movement receives current view angles")
	client.personal={"surface":{"view_transition":{"revision":1,"source_yaw":.8,"source_pitch":.25,"yaw":-.4,"pitch":.1,"input_seq":10}}}
	client._apply_surface_view_transition()
	check(transport.acknowledgments.back()==Vector2(client.yaw,client.pitch) and client._movement_view(Vector3.ZERO,false)==transport.acknowledgments.back(),"departure ACK cannot be undone by retained idle angles")
	client.free();menu.free();transport.free()

func _collision() -> void:
	var scene := Node3D.new();root.add_child(scene)
	var wall := StaticBody3D.new();scene.add_child(wall)
	wall.position=Vector3(0,1,.6)
	var collider := CollisionShape3D.new();wall.add_child(collider)
	var box := BoxShape3D.new();box.size=Vector3(3,3,.1);collider.shape=box
	await physics_frame
	await physics_frame
	var guard := SphereShape3D.new();guard.radius=.045
	var actor := {"p":Vector3(0,1,0),"state":"flying"}
	for reach: float in [0.0,.08,.85,2.5]:
		var target := CameraRules.target(actor,0,0,reach)
		var result := CameraRules.resolve(scene.get_world_3d().direct_space_state,actor,Vector3(actor.p)+Vector3(target.offset),target.basis,guard,reach)
		var position: Vector3=result.origin+Basis(target.basis).z*float(result.distance)
		check(position.z<.51 and result.distance>=0.0 and result.distance<=reach,"camera sweep preserves wall clearance at zoom "+str(reach))
		var query := PhysicsShapeQueryParameters3D.new();query.shape=guard;query.transform=Transform3D(Basis.IDENTITY,position);query.collision_mask=1
		check(scene.get_world_3d().direct_space_state.intersect_shape(query).is_empty(),"camera guard outside wall at zoom "+str(reach))
	var zoom := CameraRules.MAX_REACH
	for frame: int in range(120):
		var next := CameraRules.smooth_reach(zoom,0.0,1.0/60.0)
		check(next>=0.0 and next<=zoom,"zoom converges without overshoot")
		zoom=next
	check(zoom==0.0,"zoom settles exactly at first-person endpoint")
	scene.free()

func _run() -> void:
	_client_controls()
	await _collision()
	var report := {"checks":checks,"failures":failures,"scope":"real client input callbacks, held body angles, surface ACK and native camera collision; no visual acceptance or network protocol changes"}
	if not report_path.is_empty():
		var file := FileAccess.open(report_path,FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("MOSQUITO092_CAMERA "+JSON.stringify(report))
	await process_frame
	quit(0 if failures.is_empty() else 1)
