extends "res://tests/gameplay06_demo.gd"
## Two prepared cuts. Actual Client input, Practice authority, held/thrown mesh
## and Godot audio. Use the frozen EXE with MovieWriter; this is not a benchmark.
const Projectiles=preload("res://scripts/projectile_collision.gd")
var item_id:=-1

func _title(text:String)->void:
	caption.text="PRÁCTICA · ESCENAS PREPARADAS\n"+text

func _key(action:String,pressed:bool)->void:
	if not is_instance_valid(client) or not InputMap.has_action(action):return
	var bindings:=InputMap.action_get_events(action)
	if bindings.is_empty():return
	var event:InputEvent=bindings[0].duplicate()
	event.pressed=pressed
	if event is InputEventKey:event.echo=false
	if event is InputEventMouseButton:event.position=Vector2(640,360);event.global_position=event.position
	Input.parse_input_event(event)
	client._unhandled_input(event)

func _prepare(tool:String)->void:
	_start("human")
	var sim:RefCounted=client.practice.sim
	sim.actors[1].p=Vector3(0,0,7.5)
	for id:int in sim.actors:
		if id!=1:sim.actors[id].p=Vector3(7,1.5,-8)
	item_id=-1
	for id:int in sim.pickups:
		if str(sim.pickups[id].tool)==tool:
			item_id=id;break
	_check(item_id>=0,"prepared cut uses an existing "+tool+" pickup")
	if item_id<0:return
	var item:Dictionary=sim.pickups[item_id]
	item.holder=0;item.state="ground";item.rotation=Vector3(-PI/2,0,0)
	item.p=sim.actors[1].p+Vector3(.25,Projectiles.resting_offset(tool,item.rotation),-.65)
	item.velocity=Vector3.ZERO
	client.practice._publish()
	_aim(item.p)

func _pick_up()->void:
	_key("pickup",true);await _wait(.06);_key("pickup",false)
	await _wait(.30)
	_check(int(client.practice.sim.pickups[item_id].holder)==1,"mapped pickup transfers the same physical item")

func _throw_for(seconds:float)->void:
	_look(0,-.18)
	_key("throw",true)
	await _wait(seconds)
	var gesture:Dictionary=client.practice.sim.actors[1].throw_gesture
	var charge:float=float(gesture.power)
	metrics[str(gesture.tool)+"_charge_power"]=charge
	_check(str(gesture.state)=="charging","held right button charges through authority")
	_check(charge<.65 if seconds<.5 else charge>.98,"short/full charge is distinguished by server")
	_key("throw",false)
	await _wait(.20)
	_check(client.practice.sim.actors[1].tool=="hands" and int(client.practice.sim.pickups[item_id].holder)==0,"release opens hand and launches the equipped instance")
	await _wait(1.40,func()->void:_aim(client.practice.sim.pickups[item_id].p))

func _recover()->void:
	var sim:RefCounted=client.practice.sim
	var spent:=0.0
	while Vector3(sim.actors[1].p).distance_to(sim.pickups[item_id].p)>1.0 and spent<3.2:
		_aim(Vector3(sim.pickups[item_id].p)+Vector3.UP*.04)
		_key("move_forward",true)
		await _wait(.10)
		spent+=.10
	_key("move_forward",false)
	await _wait(.25,func()->void:_aim(sim.pickups[item_id].p))
	await _pick_up()
	_check(sim.actors[1].tool=="slipper","walk and pickup recover the thrown slipper")
	_look(client.yaw,-.85)
	await _wait(.75)

func _run()->void:
	root.size=Vector2i(1280,720)
	app=load("res://scripts/main.gd").new();root.add_child(app)
	root.content_scale_size=Vector2i(1280,720)
	client=app.get_node("Client");client.set_process_unhandled_input(false)
	overlay=CanvasLayer.new();overlay.layer=100;root.add_child(overlay)
	caption=Label.new();caption.position=Vector2(270,16);caption.size=Vector2(740,55)
	caption.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;caption.mouse_filter=Control.MOUSE_FILTER_IGNORE
	caption.add_theme_font_size_override("font_size",18)
	caption.add_theme_color_override("font_color",Color("fff1ca"));caption.add_theme_color_override("font_outline_color",Color("17121d"));caption.add_theme_constant_override("outline_size",4)
	overlay.add_child(caption)
	create_timer(40).timeout.connect(func()->void:
		if not finishing:_check(false,"watchdog");_write_report();quit(1))
	_prepare("newspaper")
	_title("Diario · recoger y mirar el agarre real")
	await _wait(.80);await _pick_up();_look(0,-.85);await _wait(1.00)
	var sim:RefCounted=client.practice.sim
	var foe:Dictionary=sim.actors[101]
	_look(0,0);await _wait(.15)
	foe.p=Pose.view_origin(sim.actors[1])+Vector3.FORWARD*.60
	foe.velocity=Vector3.ZERO;client.practice._publish()
	_title("Clic izquierdo · golpe manual")
	await _wait(.80)
	_key("attack",true);await _wait(.06);_key("attack",false)
	await _wait(.70)
	_check(str(foe.state)=="stunned","visible manual newspaper stroke stuns the prepared opponent")
	_title("Clic derecho · carga corta y soltar")
	await _wait(.35);await _throw_for(.28)
	await _wait(.55)
	_prepare("slipper")
	_title("Pantufla de mano · objeto independiente")
	await _wait(.75);await _pick_up();_look(0,-.85);await _wait(.90)
	_title("Clic derecho · carga completa y soltar")
	await _wait(.30);await _throw_for(1.30)
	_title("Caminar y recoger la misma pantufla")
	await _recover()
	metrics.fixture_setups=["two cuts with existing pickups placed nearby","one stationary opponent placed on the unchanged camera ray","all pickup, melee, charge, release and walking actions use mapped Client input","native game audio; no added sounds or camera viewmodel"]
	metrics.audio_volumes={"master":Prefs.master_volume,"effects":Prefs.effects_volume,"music":Prefs.music_volume}
	_key("throw",false);_key("attack",false)
	await _finish()

func _write_report()->void:
	var report:Dictionary={"fixture":"tools07_demo","checks":checks,"failures":failures,"screen_seconds":elapsed,"observations":observations,"metrics":metrics,"staged_control_demo":true,"preferences_modified":false}
	if not report_path.is_empty():
		DirAccess.make_dir_recursive_absolute(report_path.get_base_dir())
		var file:=FileAccess.open(report_path,FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"))
	print("TOOLS07_DEMO_RESULT checks=%d failures=%d seconds=%.3f"%[checks,failures,elapsed])
