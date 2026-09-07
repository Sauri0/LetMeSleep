extends SceneTree
## Eight seconds of authoritative movement with 20Hz snapshots and real Client.
const Simulation = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Transport = preload("res://tests/v05_baseline_audit.gd").Transport
var client: Node
var simulation: RefCounted
var checks := 0
var failures := 0
var folder: String

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("MOTION05 %s %s" % ["PASS" if ok else "FAIL",message])

func show_snapshot() -> void:
	client._snapshot(simulation.public_snapshot())
	client.yaw = float(simulation.actors[1].yaw)
	client.pitch = float(simulation.actors[1].pitch)
	client.personal = simulation.private_for(1)
	client.ui.show_game(client.state,client.personal,1)

func capture(name: String) -> void:
	if DisplayServer.get_name()=="headless":
		return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(name+".png"))==OK,"capture "+name)

func _run() -> void:
	root.size = Vector2i(1280,720)
	folder = ProjectSettings.globalize_path("res://../outputs/0.5-preview")
	var transport := Transport.new()
	root.add_child(transport)
	client = load("res://scripts/client.gd").new()
	client.network = transport
	client.options = {"visual-preview":"human"}
	root.add_child(client)
	client.set_process_unhandled_input(false)
	client.local_id = 1
	simulation = Simulation.new()
	var config: Dictionary = Simulation.DEFAULT_CONFIG.duplicate()
	config.blood_goal = 100.0
	simulation.start({1:{"name":"Luna","role":"human"},2:{"name":"Zeta","role":"mosquito"}},config)
	var human: Dictionary = simulation.actors[1]
	human.p = Vector3(0,0,7)
	human.appearance = {"color":1,"accent":4,"outfit":1,"face":0,"hair":1,"accessory":0}
	simulation.actors[2].appearance = {"color":3,"accent":0,"outfit":2,"face":1,"hair":2,"accessory":0}
	simulation.actors[2]._assignment = {"human":1,"zone":0,"revision":1}
	var assignment: Dictionary = simulation.private_for(2).assignment
	simulation.actors[2].p = assignment.p+assignment.normal*0.30
	var insect_yaw: float = atan2(assignment.normal.x,assignment.normal.z)
	var insect_pitch: float = asin(-assignment.normal.y)
	var sequence := 0
	for tick: int in range(130):
		sequence += 1
		simulation.submit_input(2,sequence,Vector3.ZERO,insect_yaw,insect_pitch,true)
		simulation.step(1.0/60.0)
		if simulation.actors[2].state=="biting":
			break
	check(simulation.actors[2].state=="biting","held E attaches before movement")
	if simulation.actors[2].state!="biting":
		quit(1)
		return
	var start_position: Vector3 = human.p
	var peak_height := 0.0
	var maximum_view_delta := 0.0
	var maximum_phase := 0.0
	show_snapshot()
	var times: Dictionary = {40:"mov-01-frente-caminando",115:"mov-02-picadura-patron-corriendo",175:"mov-03-picadura-agachado",245:"mov-04-regreso-frente",295:"mov-05-picadura-saltando",370:"mov-06-frente-retorno",485:"mov-07-hud-normal-8s"}
	for frame: int in range(500):
		sequence += 1
		var inspect: bool = (frame>=70 and frame<205) or (frame>=270 and frame<315)
		var crouch: bool = frame>=145 and frame<205
		var move: Vector3 = Vector3.FORWARD if frame<230 else Vector3.BACK
		var look := Vector2(0,0)
		if inspect:
			look = Pose.aim_angles(human,simulation.actors[2].p)
		simulation.submit_input(1,sequence,move,look.x,look.y,false,frame>=85 and frame<145,crouch,frame==271)
		if frame==335:
			simulation.action(2,1,"bite")
		if frame>=335:
			simulation.submit_input(2,sequence,Vector3.UP,0,0,false)
		simulation.step(1.0/60.0)
		peak_height = maxf(peak_height,float(human.p.y))
		maximum_view_delta = maxf(maximum_view_delta,absf(wrapf(float(human.yaw)-float(human.body_yaw),-PI,PI)))
		maximum_phase = maxf(maximum_phase,float(human.motion_phase))
		if frame%3==0:
			show_snapshot()
		await physics_frame
		if times.has(frame):
			if inspect:
				check(simulation.actors[2].state=="biting","mosquito remains attached while moving frame%d" % frame)
			await capture(str(times[frame]))
	check(start_position.distance_to(human.p)>0.2 and maximum_phase>1.0,"actual server movement and gait advanced over8seconds")
	check(peak_height>0.15,"jump had real airborne height")
	check(maximum_view_delta>0.1,"inspection view rotates relative to body")
	check(absf(float(human.pitch))<0.01 and absf(client.pitch)<0.01,"camera returned to forward view after inspection")
	check(not bool(human.bitten),"final normal HUD has no active bite")
	print("MOTION05_METRICS peak_jump=%.3f relative_yaw=%.3f phase=%.3f start=%s end=%s" % [peak_height,maximum_view_delta,maximum_phase,start_position,human.p])
	client.playing = false
	client.world.clear_actors()
	await process_frame
	await process_frame
	client.queue_free()
	await process_frame
	await process_frame
	print("MOTION05_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
