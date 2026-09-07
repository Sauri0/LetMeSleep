extends SceneTree
## Real Client mouse/keyboard handlers, practice authority and native camera.
const Pose = preload("res://scripts/human_pose.gd")
const Prefs = preload("res://scripts/preferences.gd")
var app: Node
var client: Node
var checks := 0
var failures := 0
var caption: Label

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	print("CAMERA06 %s %s" % ["PASS" if ok else "FAIL", message])

func mouse(yaw_delta: float, pitch_delta: float) -> void:
	var event := InputEventMouseMotion.new()
	event.relative = Vector2(-yaw_delta / Prefs.human_sensitivity, -pitch_delta / Prefs.human_sensitivity * (-1.0 if Prefs.invert_y else 1.0))
	client._unhandled_input(event)

func key(action: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	Input.parse_input_event(event)

func _run() -> void:
	root.size = Vector2i(1280,720)
	app = load("res://scripts/main.gd").new()
	root.add_child(app)
	client = app.get_node("Client")
	client.set_process_unhandled_input(false)
	client._start_practice("human","blood")
	client.practice.brains.clear()
	var human: Dictionary = client.practice.sim.actors[1]
	human.p = Vector3(0,0,6.8)
	client.practice._publish()
	caption = Label.new()
	caption.text = "PRUEBA DE CONTROLES · Mirar abajo y girar 360°"
	caption.position = Vector2(360,20)
	caption.add_theme_font_size_override("font_size",18)
	client.ui.add_child(caption)
	for frame: int in range(12): await process_frame
	mouse(wrapf(-client.yaw,-PI,PI),-1.35-client.pitch)
	var total_view := 0.0
	var total_body := 0.0
	var reversed_steps := 0
	var frozen_steps := 0
	var max_crouch := 0.0
	var sprint_seen := false
	var walked := 0.0
	var previous_body: float = human.body_yaw
	var previous_position: Vector3 = human.p
	for frame: int in range(360):
		if frame == 60: key("move_forward",true)
		if frame == 80: key("sprint",true)
		if frame == 130:
			key("move_forward",false)
			key("sprint",false)
		if frame == 150: key("crouch",true)
		if frame == 220: key("crouch",false)
		var old_yaw: float = client.yaw
		mouse(TAU/360.0,0.0)
		var delta: float = wrapf(client.yaw-old_yaw,-PI,PI)
		total_view += delta
		if delta < 0.001: frozen_steps += 1
		await process_frame
		var body_step: float = wrapf(float(human.body_yaw)-previous_body,-PI,PI)
		total_body += body_step
		if body_step < -0.0001: reversed_steps += 1
		previous_body = human.body_yaw
		walked += Vector3(human.p).distance_to(previous_position)
		previous_position = human.p
		max_crouch = maxf(max_crouch,float(human.crouch_amount))
		sprint_seen = sprint_seen or bool(human.sprinting)
	check(absf(total_view-TAU)<0.005 and frozen_steps==0,"all 360 mouse degrees consumed continuously while looking down")
	check(total_body>4.0 and reversed_steps==0,"torso follows continued turn without lifting view or reversing")
	check(walked>0.4 and sprint_seen and max_crouch>0.8,"WASD, run and crouch remain available during downward turn")
	caption.text = "Volver al frente · transición continua"
	mouse(0.0,-client.pitch)
	for frame: int in range(60): await process_frame
	check(absf(wrapf(float(human.yaw)-float(human.body_yaw),-PI,PI))<0.05,"looking forward recenters smoothly")
	# Set up an actual attached mosquito after the turn, then aim and strike with
	# the same mouse/button handlers; authority validates the hit and stun.
	var insect: Dictionary = client.practice.sim.actors[101]
	insect._assignment = {"human":1,"zone":4,"revision":1}
	insect.state = "biting"
	client.practice.sim._update_attached()
	client.practice._publish()
	caption.text = "Defensa manual después del giro · apuntar y palmada"
	for frame: int in range(45):
		var aim: Vector2 = Pose.aim_angles(human,insect.p)
		mouse(wrapf(aim.x-client.yaw,-PI,PI),aim.y-client.pitch)
		await process_frame
	check(insect.state=="biting","attachment follows shared body pose while aiming after turn")
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	client._unhandled_input(click)
	click.pressed = false
	client._unhandled_input(click)
	for frame: int in range(50): await process_frame
	check(insect.state=="stunned" and bool(insect.alive),"manual click still reaches visible mosquito after unrestricted turn")
	print("CAMERA06_METRICS ",JSON.stringify({"view_degrees":rad_to_deg(total_view),"body_degrees":rad_to_deg(total_body),"frozen_steps":frozen_steps,"reversed_steps":reversed_steps,"travel":walked,"max_crouch":max_crouch}))
	client._leave()
	for frame: int in range(6): await process_frame
	app.queue_free()
	await process_frame
	print("CAMERA06_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
