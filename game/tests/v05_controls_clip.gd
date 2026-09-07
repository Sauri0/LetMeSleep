extends SceneTree
## Development-only, deterministic input demonstration. Real Simulation at 60Hz,
## Client snapshots at 20Hz and MovieWriter at 30fps; never a performance benchmark.
const Simulation = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Transport = preload("res://tests/v05_baseline_audit.gd").Transport
var simulation: RefCounted
var client: Node
var caption: Label
var sequence := 0

func _initialize() -> void:
	_run.call_deferred()

func snapshot() -> void:
	client._snapshot(simulation.public_snapshot())
	client.yaw = float(simulation.actors[1].yaw)
	client.pitch = float(simulation.actors[1].pitch)
	client.personal = simulation.private_for(1)
	client.ui.show_game(client.state,client.personal,1)

func attach(zone_id: int, position: Vector3) -> bool:
	simulation = Simulation.new()
	var config: Dictionary = Simulation.DEFAULT_CONFIG.duplicate()
	config.blood_goal = 100.0
	simulation.start({1:{"name":"Luna","role":"human"},2:{"name":"Zeta","role":"mosquito"},3:{"name":"Lejos","role":"mosquito"}},config)
	simulation.actors[1].p = position
	simulation.actors[1].appearance = {"color":1,"accent":4,"outfit":1,"face":0,"hair":1,"accessory":0}
	simulation.actors[2].appearance = {"color":3,"accent":0,"outfit":2,"face":1,"hair":2,"accessory":0}
	simulation.actors[3].p = Vector3(6,4.6,-8)
	simulation.actors[2]._assignment = {"human":1,"zone":zone_id,"revision":1}
	var assignment: Dictionary = simulation.private_for(2).assignment
	simulation.actors[2].p = assignment.p+assignment.normal*0.30
	for tick: int in range(130):
		sequence += 1
		simulation.submit_input(2,sequence,Vector3.ZERO,atan2(assignment.normal.x,assignment.normal.z),asin(-assignment.normal.y),true)
		simulation.step(1.0/60.0)
		if simulation.actors[2].state=="biting":
			return true
	return false

func add_captions() -> void:
	var layer := CanvasLayer.new()
	layer.layer = 110
	root.add_child(layer)
	var stamp := Label.new()
	stamp.text = "DESARROLLO 0.5 · PRUEBA DE CONTROLES"
	stamp.position = Vector2(280,48)
	stamp.size = Vector2(720,32)
	stamp.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	stamp.add_theme_font_size_override("font_size",18)
	stamp.add_theme_color_override("font_color",Color("fff1cc"))
	stamp.add_theme_color_override("font_outline_color",Color("172b36"))
	stamp.add_theme_constant_override("outline_size",7)
	layer.add_child(stamp)
	caption = Label.new()
	caption.position = Vector2(130,677)
	caption.size = Vector2(1020,30)
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	caption.add_theme_font_size_override("font_size",19)
	caption.add_theme_color_override("font_outline_color",Color("172b36"))
	caption.add_theme_constant_override("outline_size",7)
	layer.add_child(caption)

func _run() -> void:
	root.size = Vector2i(1280,720)
	var transport := Transport.new()
	root.add_child(transport)
	client = load("res://scripts/client.gd").new()
	client.network = transport
	client.options = {"visual-preview":"human"}
	root.add_child(client)
	client.set_process_unhandled_input(false)
	client.local_id = 1
	add_captions()
	if not attach(0,Vector3(0,0,7)):
		push_error("Clip setup could not attach mosquito with held E.")
		quit(1)
		return
	snapshot()
	await process_frame
	await process_frame
	print("CONTROLS_CLIP_START frame=%d" % Engine.get_frames_drawn())
	var human: Dictionary = simulation.actors[1]
	for movie_frame: int in range(250):
		for substep: int in range(2):
			var frame: int = movie_frame*2+substep
			sequence += 1
			var inspect: bool = (frame>=70 and frame<205) or (frame>=270 and frame<315)
			var crouch: bool = frame>=145 and frame<205
			var move: Vector3 = Vector3.FORWARD if frame<230 else Vector3.BACK
			var look: Vector2 = Pose.aim_angles(human,simulation.actors[2].p) if inspect else Vector2.ZERO
			simulation.submit_input(1,sequence,move,look.x,look.y,false,frame>=85 and frame<145,crouch,frame==271)
			if frame==335:
				simulation.action(2,1,"bite")
			if frame>=335:
				simulation.submit_input(2,sequence,Vector3.UP,0,0,false)
			simulation.step(1.0/60.0)
			if frame%3==0:
				snapshot()
		caption.text = "Caminar · mirar el cuerpo · volver al frente"
		if movie_frame>=42 and movie_frame<73:
			caption.text = "Correr · picadura visible sobre la ropa"
		elif movie_frame>=73 and movie_frame<103:
			caption.text = "Agacharse · inspeccionar el cuerpo"
		elif movie_frame>=135 and movie_frame<158:
			caption.text = "Saltar · mantener visible la picadura"
		elif movie_frame>=185:
			caption.text = "Retorno al frente · HUD normal"
		await process_frame
	if not attach(4,Vector3(-6.4,0,7.8)):
		push_error("Manual clip setup failed.")
		quit(1)
		return
	human = simulation.actors[1]
	var aim: Vector2 = Pose.aim_angles(human,simulation.actors[2].p)
	var last_aim := Vector2.ZERO
	for movie_frame: int in range(90):
		var blend: float = smoothstep(0.0,1.0,clampf(float(movie_frame-5)/25.0,0.0,1.0))
		if movie_frame<45:
			aim = Pose.aim_angles(human,simulation.actors[2].p)
			last_aim = Vector2.ZERO.lerp(aim,blend)
		if movie_frame>=77:
			last_aim = last_aim.lerp(Vector2.ZERO,0.22)
		for substep: int in range(2):
			sequence += 1
			simulation.submit_input(1,sequence,Vector3.ZERO,last_aim.x,last_aim.y,false)
			if movie_frame==45 and substep==0:
				simulation.action(1,1,"attack",last_aim.x,last_aim.y)
			simulation.step(1.0/60.0)
			if sequence%3==0:
				snapshot()
		caption.text = "Defensa corporal · apuntar al antebrazo · LMB"
		if movie_frame>=58:
			caption.text = "Impacto confirmado · mosquito aturdido"
		await process_frame
	var stunned: bool = bool(simulation.actors[2].alive) and simulation.actors[2].state=="stunned"
	print("CONTROLS_CLIP_END frame=%d manual_stun=%s seconds=11.333" % [Engine.get_frames_drawn(),stunned])
	client.playing = false
	client.world.clear_actors()
	await process_frame
	await process_frame
	client.queue_free()
	await process_frame
	await process_frame
	quit(0 if stunned else 1)
