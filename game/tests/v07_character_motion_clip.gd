extends "res://tests/v06_character_controls_clip.gd"
## Identical deterministic input on the 0.6 executable and current source.
## The neutral stage isolates animation; it is not gameplay or a benchmark.
const View = preload("res://scripts/actor_view.gd")
var human_view: Node3D
var insect_view: Node3D
var camera: Camera3D
var output_dir: String
var version_label := "0.7"

func _run() -> void:
	root.size = Vector2i(1280,720)
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--label="): version_label = arg.trim_prefix("--label=")
		if arg.begins_with("--output="): output_dir = arg.trim_prefix("--output=")
	if output_dir.is_empty(): output_dir = ProjectSettings.globalize_path("res://../outputs/0.7-animacion/"+version_label)
	DirAccess.make_dir_recursive_absolute(output_dir)
	var environment_node := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("253e50")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("bdd6df")
	environment.ambient_light_energy = .35
	environment_node.environment = environment
	root.add_child(environment_node)
	for index: int in range(2):
		var light := DirectionalLight3D.new()
		light.rotation_degrees = Vector3(-45,-145 if index==0 else 35,0)
		light.light_color = Color("ffe2b8") if index==0 else Color("b9dce9")
		light.light_energy = 1.0 if index==0 else .4
		light.shadow_enabled = index==0
		root.add_child(light)
	var floor_mesh := PlaneMesh.new()
	floor_mesh.size = Vector2(80,80)
	var stage := Node3D.new()
	root.add_child(stage)
	View.mesh(stage,floor_mesh,Vector3(0,-.008,0),View.material(Color("7e8076")))
	for grid: int in range(-16,17):
		var strip := BoxMesh.new()
		strip.size = Vector3(32,.003,.008)
		View.mesh(stage,strip,Vector3(0,-.005,grid),View.material(Color("a3a296")))
		var cross := BoxMesh.new()
		cross.size = Vector3(.008,.003,32)
		View.mesh(stage,cross,Vector3(grid,-.005,0),View.material(Color("a3a296")))
	human_view = View.new()
	root.add_child(human_view)
	human_view.build("human","",0)
	human_view.name_label.hide()
	insect_view = View.new()
	root.add_child(insect_view)
	insect_view.build("mosquito","",0)
	insect_view.name_label.hide()
	camera = Camera3D.new()
	camera.near = .015
	root.add_child(camera)
	camera.make_current()
	add_captions()
	for label: Node in caption.get_parent().get_children():
		if label is Label and label != caption:
			label.text = "DESARROLLO %s · MISMA PRUEBA DE ANIMACIÓN" % version_label
	if not attach(0,Vector3(0,0,7)):
		push_error("Motion fixture: held E attachment failed")
		quit(1)
		return
	var human: Dictionary = simulation.actors[1]
	var mosquito: Dictionary = simulation.actors[2]
	for frame: int in range(420):
		for substep: int in range(2):
			sequence += 1
			var move := Vector3.ZERO
			var sprint := false
			var crouch := frame>=120 and frame<150
			var yaw := 0.0
			var pitch := 0.0
			if frame>=20 and frame<70: move = Vector3.FORWARD
			if frame>=70 and frame<120:
				move = Vector3.BACK
				sprint = true
			if frame>=120 and frame<180: move = Vector3.FORWARD
			if frame>=180 and frame<220:
				move = Vector3.FORWARD*.30
				yaw = (frame-180)*.09
			if frame>=220 and frame<280:
				var aim: Vector2 = Pose.aim_angles(human,mosquito.p)
				yaw = aim.x
				pitch = aim.y
				if frame<245: move = Vector3.FORWARD*.35
				if frame==250 and substep==0: simulation.action(1,1,"attack",yaw,pitch)
			simulation.submit_input(1,sequence,move,yaw,pitch,false,sprint,crouch,frame==150 and substep==0)
			if frame>=280:
				# Real flight integration and surface action after the human sequence.
				if frame==280 and substep==0:
					mosquito.state = "flying"
					mosquito.p = Vector3(0,1.2,0)
					mosquito.velocity = Vector3.ZERO
				mosquito._stun_remaining = 0.0
				var fly_move := Vector3.RIGHT if frame<310 else Vector3.LEFT if frame>=335 and frame<360 else Vector3.ZERO
				simulation.submit_input(2,sequence,fly_move,0,0,false)
			simulation.step(1.0/60.0)
		var public: Dictionary = simulation.public_snapshot().actors
		human_view.update_state(public[1],1.0/30.0)
		insect_view.update_state(public[2],1.0/30.0)
		human_view.set_local(frame>=220 and frame<280)
		if frame<220:
			camera.fov = 43
			camera.position = human_view.position+Vector3(1.8,1.1,-3.1)
			camera.look_at(human_view.position+Vector3(0,.90,0))
			caption.text = "Reposo · paso y contacto con el suelo"
			if frame>=70: caption.text = "Carrera · cambio de dirección"
			if frame>=120: caption.text = "Agacharse · saltar · aterrizar · girar"
		elif frame<280:
			camera.fov = 68
			camera.position = Pose.view_origin(human)
			camera.rotation = Vector3(human.pitch,human.yaw,0)
			caption.text = "Cuerpo real en primera persona · apuntar y LMB"
			if frame>=255:
				caption.text = "Impacto confirmado · mosquito aturdido" if mosquito.state=="stunned" else "Palmada · sin impacto confirmado"
		else:
			camera.fov = 42
			camera.position = insect_view.position+Vector3(.30,.15,-.48)
			camera.look_at(insect_view.position)
			caption.text = "Otra secuencia: mosquito · acelerar · frenar · cambiar de dirección"
		await process_frame
		if frame==279: print("MOTION_MANUAL_RESULT state="+str(mosquito.state))
		if frame in [35,90,133,153,167,198,238,253,295,325,350]:
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(output_dir.path_join("frame-%03d.png" % frame))
	print("MOTION_COMPARISON version=%s frames=420 manual_state=%s" % [version_label,mosquito.state])
	human_view.queue_free()
	insect_view.queue_free()
	await process_frame
	await process_frame
	quit()
