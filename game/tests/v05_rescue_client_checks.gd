extends SceneTree
## Real Client and HUD, fresh instance per POV. Simulated E/LMB commands use the
## authoritative rules; no artificial HUD role switch or private timer override.
const Simulation = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Transport = preload("res://tests/v05_baseline_audit.gd").Transport
var simulation: RefCounted
var client: Node
var sequence := 0
var local_id := 2
var checks := 0
var failures := 0
var folder: String

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("RESCUE_CLIENT05 %s %s" % ["PASS" if ok else "FAIL",label])

func snapshot() -> void:
	client._snapshot(simulation.public_snapshot())
	client.personal = simulation.private_for(local_id)
	client.yaw = float(simulation.actors[local_id].yaw)
	client.pitch = float(simulation.actors[local_id].pitch)
	client.ui.show_game(client.state,client.personal,local_id)

func capture(name: String) -> void:
	for frame: int in range(18):
		await process_frame
	await physics_frame
	if DisplayServer.get_name()=="headless":
		return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(folder.path_join(name+".png"))==OK,"capture "+name)

func prepare() -> void:
	simulation = Simulation.new()
	var config: Dictionary = Simulation.DEFAULT_CONFIG.duplicate()
	config.blood_goal = 100.0
	simulation.start({1:{"name":"Luna","role":"human"},2:{"name":"Zeta","role":"mosquito"},3:{"name":"Mora","role":"mosquito"}},config)
	var human: Dictionary = simulation.actors[1]
	human.p = Vector3(-6.4,0,7.8)
	human.appearance = {"color":1,"outfit":1,"accent":4}
	simulation.actors[3].p = Vector3(-9,1.0,8.5)
	simulation.actors[2]._assignment = {"human":1,"zone":4,"revision":1}
	var mark: Dictionary = simulation.private_for(2).assignment
	simulation.actors[2].p = mark.p+mark.normal*0.3
	for frame: int in range(130):
		sequence += 1
		simulation.submit_input(2,sequence,Vector3.ZERO,atan2(mark.normal.x,mark.normal.z),asin(-mark.normal.y),true)
		simulation.step(1.0/60.0)
		if simulation.actors[2].state=="biting":
			break
	check(simulation.actors[2].state=="biting","E attached mosquito before strike")
	for frame: int in range(3):
		sequence += 1
		var aim: Vector2 = Pose.aim_angles(human,simulation.actors[2].p)
		simulation.submit_input(1,sequence,Vector3.ZERO,aim.x,aim.y,false)
		simulation.step(1.0/60.0)
	simulation.action(1,1,"attack",human.yaw,human.pitch)
	for frame: int in range(75):
		simulation.step(1.0/60.0)
	check(simulation.actors[2].state=="stunned" and simulation.actors[2].alive,"real manual LMB caused stun rather than death")
	check(simulation.actors[2].grounded and is_equal_approx(float(simulation.actors[2].p.y),0.04),"gravity settled at physical floor+.04")
	simulation.actors[2].yaw = PI
	simulation.actors[2].pitch = -0.12
	var transport := Transport.new()
	root.add_child(transport)
	client = load("res://scripts/client.gd").new()
	client.network = transport
	client.options = {}
	client.local_id = local_id
	root.add_child(client)
	client.set_process_unhandled_input(false)
	client.set_physics_process(false)
	snapshot()

func _run() -> void:
	root.size = Vector2i(1280,720)
	folder = ProjectSettings.globalize_path("res://../outputs/0.5-preview")
	for observer: int in [2,3]:
		local_id = observer
		prepare()
		if local_id==2:
			await capture("rescate-01-caido-client")
			check(client.world.visible and client.camera.global_position.y>=0.025,"fallen real TPS remains visible above floor")
			check(str(client.ui._hud_state.text).contains(" s"),"fresh fallen HUD includes authoritative remaining seconds")
		var target: Dictionary = simulation.actors[2]
		var helper: Dictionary = simulation.actors[3]
		helper.p = target.p+Vector3(0,0.25,-0.45)
		var direction: Vector3 = (Vector3(target.p)-Vector3(helper.p)).normalized()
		var help_yaw: float = atan2(-direction.x,-direction.z)
		var help_pitch: float = asin(direction.y)
		sequence += 1
		simulation.submit_input(3,sequence,Vector3.ZERO,help_yaw,help_pitch,false)
		simulation.step(1.0/60.0)
		snapshot()
		if local_id==3:
			check(simulation.private_for(3).help.state=="ready","helper gets authoritative nearby prompt")
			await capture("rescate-02-ayuda-disponible-client")
		var help_seconds := 0.0
		for tick: int in range(650):
			sequence += 1
			simulation.submit_input(3,sequence,Vector3.ZERO,help_yaw,help_pitch,true)
			simulation.step(1.0/60.0)
			help_seconds += 1.0/60.0
			if tick%3==0:
				snapshot()
			if tick%6==0:
				await process_frame
			if tick==180:
				check(simulation.private_for(2).stun.helped and int(helper.help_target)==2,"held E confirms active rescue publicly and privately")
				await capture("rescate-03-recibiendo-ayuda-client" if local_id==2 else "rescate-04-ayudando-client")
			if target.state=="flying":
				break
		check(target.state=="flying" and help_seconds<10.0,"teammate accelerates recovery without timer override")
		var ground: Vector3 = target.p
		for frame: int in range(36):
			sequence += 1
			simulation.submit_input(2,sequence,Vector3.UP,PI,0.0,false)
			simulation.step(1.0/60.0)
			if frame%3==0:
				snapshot()
			await process_frame
		check(float(target.p.y)>ground.y+0.3,"recovered mosquito can actively fly again")
		if local_id==2:
			await capture("rescate-05-control-restaurado-client")
		client.playing = false
		client.world.clear_actors()
		await process_frame
		client.queue_free()
		await process_frame
		await process_frame
	print("RESCUE_CLIENT05_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
