extends Node
## Reproducible control demonstration. Opponents stand still in this fixture;
## all movement, focus, attachment and stairs go through the actual Client input.
const Prefs = preload("res://scripts/preferences.gd")
var client: Node
var options: Dictionary = {}
var failures := 0
var checks := 0
func _ready() -> void:
	_run.call_deferred()
func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout
func _key(action: String, held: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = held
	Input.parse_input_event(event)
func _look(yaw: float, pitch: float) -> void:
	var sensitivity: float = Prefs.human_sensitivity if client.role == "human" else Prefs.mosquito_sensitivity
	var motion := InputEventMouseMotion.new()
	motion.relative = Vector2(-wrapf(yaw-client.yaw,-PI,PI)/sensitivity,-(pitch-client.pitch)/sensitivity*(-1.0 if Prefs.invert_y else 1.0))
	# Deliver viewport-normalized relative motion to the real handler. Headless
	# windows otherwise rescale synthetic mouse coordinates by their window size.
	client._unhandled_input(motion)
func _aim_at(point: Vector3) -> void:
	var delta: Vector3 = point-client.practice.sim.actors[1].p
	_look(atan2(-delta.x,-delta.z),asin(delta.normalized().y))
func _check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("DEMO %s %s" % ["PASS" if ok else "FAIL",message])
func _capture(name: String) -> void:
	if not options.has("demo-output") or DisplayServer.get_name() == "headless": return
	await RenderingServer.frame_post_draw
	var folder := str(options["demo-output"])
	DirAccess.make_dir_recursive_absolute(folder)
	get_viewport().get_texture().get_image().save_png(folder.path_join(name+".png"))
func _start(role: String) -> void:
	client.ui._open_practice()
	client.ui._practice_role_buttons[role].pressed.emit()
	client.ui._practice_mode_buttons["blood" if role == "mosquito" else "survival"].pressed.emit()
	client.ui._practice_start_button.pressed.emit()
	client.practice.brains.clear()
	client.ui._practice_banner.text = "DEMO DE CONTROLES / RIVAL QUIETO"
func _run() -> void:
	await _wait(0.2)
	_start("mosquito")
	var me: Dictionary = client.practice.sim.actors[1]
	var human: Dictionary = client.practice.sim.actors[101]
	me.p = human.p+Vector3(0,1.3,-2.5)
	me.velocity = Vector3.ZERO
	client.practice._publish()
	await _wait(0.2)
	_look(PI,0.40)
	await _wait(0.8)
	var start: Vector3 = me.p
	_key("move_forward",true)
	await _wait(0.4)
	_key("move_forward",false)
	await _wait(0.4)
	_check(me.p.y > start.y+0.2,"camera-pointed W rises without height keys")
	_check(Vector3(me.velocity).length()<0.1,"release brakes")
	await _capture("01-vuelo-y-freno")
	_aim_at(client.practice.sim.private_for(1).assignment.p)
	_key("bite",true)
	# Follow the actual animated mark while assistance changes the approach angle.
	for sample: int in range(11):
		_aim_at(client.practice.sim.private_for(1).assignment.p)
		await _wait(0.05)
	var focus: Dictionary = client.practice.sim.private_for(1).focus
	print("DEMO_TRACE focus=",focus," position=",me.p," assignment=",client.practice.sim.private_for(1).assignment," yaw=",client.yaw," pitch=",client.pitch)
	_check(focus.progress>0.15 and focus.progress<1.0,"held input charges private focus")
	await _capture("02-carga")
	_key("bite",false)
	await _wait(0.25)
	_check(client.practice.sim.private_for(1).focus.progress==0,"release cancels charge")
	_aim_at(client.practice.sim.private_for(1).assignment.p)
	_key("bite",true)
	await _wait(1.6)
	_check(me.state=="biting","new held input attaches after charge")
	_key("bite",false)
	await _wait(1.4)
	_check(me.state=="biting" and client.practice.sim.blood>0,"release keeps attachment and blood extracts after preparation")
	await _capture("03-picadura")
	_key("bite",true)
	await _wait(0.05)
	_key("bite",false)
	_check(me.state=="flying","explicit second press detaches")
	_key("move_back",true)
	await _wait(0.8)
	_key("move_back",false)
	await _wait(0.35)
	await _capture("04-retirada")
	client._leave()
	await _wait(0.2)
	_start("human")
	me = client.practice.sim.actors[1]
	me.p = Vector3(-11,0,-4.8)
	me.velocity = Vector3.ZERO
	client.practice._publish()
	await _wait(0.2)
	_look(PI,-0.03)
	await _wait(0.6)
	await _capture("05-escalera-abajo")
	_key("move_forward",true)
	await _wait(3.5)
	_key("move_forward",false)
	await _wait(0.4)
	_check(me.p.y>3.15,"human walks up complete staircase without jumping")
	print("DEMO_TRACE stairs=",me.p," yaw=",client.yaw," pitch=",client.pitch)
	await _capture("06-escalera-arriba")
	await _wait(0.8)
	client._leave()
	await _wait(0.2)
	print("DEMO_RESULT checks=%d failures=%d" % [checks,failures])
	get_tree().quit(failures)
