extends "res://tests/gameplay06_demo.gd"
## Prepared shots with actual client input and authoritative moving opponent.
## Flight speed, cooldown, collision, hit reactions and door rules are unchanged.
const Doors = preload("res://scripts/door_catalog.gd")
var crossing := false

func _title(text: String) -> void:
	caption.text="Práctica · situaciones preparadas\n"+text

func _sample() -> void:
	super._sample()
	if crossing and client.practice_active:
		var foe: Dictionary=client.practice.sim.actors[101]
		client.practice.sim.submit_input(101,int(foe._input_seq)+1,Vector3.RIGHT,0,0,false)

func _aim_door(angle: float) -> void:
	_aim(Doors.handle_point(Doors.DEFINITIONS.kitchen,angle))

func _run() -> void:
	root.size=Vector2i(1280,720)
	app=load("res://scripts/main.gd").new()
	root.add_child(app)
	root.content_scale_size=Vector2i(1280,720)
	client=app.get_node("Client")
	client.set_process_unhandled_input(false)
	overlay=CanvasLayer.new()
	overlay.layer=100
	root.add_child(overlay)
	caption=Label.new()
	caption.position=Vector2(280,16)
	caption.size=Vector2(720,54)
	caption.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	caption.mouse_filter=Control.MOUSE_FILTER_IGNORE
	caption.add_theme_font_size_override("font_size",18)
	caption.add_theme_color_override("font_color",Color("fff1ca"))
	caption.add_theme_color_override("font_outline_color",Color("17121d"))
	caption.add_theme_constant_override("outline_size",4)
	overlay.add_child(caption)
	create_timer(45).timeout.connect(func() -> void:
		if not finishing: _check(false,"watchdog"); _write_report(); quit(1))
	_start("human")
	var sim: RefCounted=client.practice.sim
	var me: Dictionary=sim.actors[1]
	var foe: Dictionary=sim.actors[101]
	me.p=Vector3(-7,0,8)
	foe.p=Vector3(6,1.5,-8)
	sim.actors[102].p=Vector3(7,1.5,-8)
	sim.pickups={0:{"tool":"swatter","p":Vector3(-6.7,.15,7.3),"holder":0,"yaw":0.0}}
	client.practice._publish()
	_title("1/4 · Recoger una herramienta · "+Prefs.binding_text("pickup"))
	await _wait(1.4,func() -> void: _aim(sim.pickups[0].p))
	_key("pickup",true)
	_key("pickup",false)
	await _wait(.5)
	_check(me.tool=="swatter","actual pickup input equips swatter")
	_look(0,0)
	await _wait(.45)
	_title("2/4 · El golpe es manual: fuera del recorrido, falla")
	foe.p=Pose.view_origin(me)+Vector3(.7,0,-.35)
	client.practice._publish()
	await _wait(.7)
	_key("attack",true)
	_key("attack",false)
	await _wait(.9)
	_check(foe.state=="flying" and sim.private_for(1).attack.state=="miss","off-corridor target stays flying after real miss")
	foe.p=Vector3(6,1.5,-8)
	var plan: Dictionary=sim._strike_plan(1)
	foe.p=Vector3(plan.point)-Vector3.RIGHT*ArenaData.MOSQUITO_SPEED*(1.0/60.0+.12)
	foe.velocity=Vector3.RIGHT*ArenaData.MOSQUITO_SPEED
	_title("Anticipar el cruce · mosquito en vuelo a velocidad real")
	crossing=true
	_sample()
	_key("attack",true)
	_key("attack",false)
	await _wait(.6)
	crossing=false
	_check(foe.state=="stunned" and foe.alive,"continuous real gesture catches moving opponent and stuns")
	_check(sim.private_for(1).attack.state=="hit","authoritative hit feedback follows moving contact")
	_title("Acierto · cae aturdido durante 35 s")
	await _wait(1.4,func() -> void: _aim(foe.p))
	_start("human")
	sim=client.practice.sim
	me=sim.actors[1]
	foe=sim.actors[101]
	me.p=Vector3(-7,0,8)
	sim.actors[102].p=Vector3(7,1.5,-8)
	foe._assignment={"human":1,"zone":6,"revision":1}
	foe.state="biting"
	sim._update_attached()
	client.practice._publish()
	_title("3/4 · Mirar el cuerpo y apuntar la palmada")
	await _wait(1.1,func() -> void: _aim(foe.p))
	_check(bool(sim.private_for(1).attack.candidate),"visible own-body contact offers a manual opportunity")
	_key("attack",true)
	_key("attack",false)
	await _wait(.65)
	_check(foe.state=="stunned","manual own-body palm reacts at authoritative contact")
	await _wait(.7,func() -> void: _aim(foe.p))
	_start("human")
	sim=client.practice.sim
	sim.actors[1].p=Vector3(-.7,0,-8.2)
	sim.actors[101].p=Vector3(6,1.5,2)
	sim.actors[102].p=Vector3(7,1.5,2)
	sim.doors.kitchen.angle=0
	sim.doors.kitchen.target_angle=0
	client.practice._publish()
	_title("4/4 · Puerta cercana · "+Prefs.binding_text("interact"))
	await _wait(1.0,func() -> void: _aim_door(0))
	_key("interact",true)
	_key("interact",false)
	await _wait(1.35)
	_check(float(sim.doors.kitchen.angle)>1.5,"real interaction opens visible door")
	_key("move_forward",true)
	await _wait(.8)
	_key("move_forward",false)
	_title("Más espacio para jugar · guía sólo con "+Prefs.binding_text("toggle_help"))
	await _wait(1.1)
	metrics["moving_target_speed"]=ArenaData.MOSQUITO_SPEED
	metrics["fixture_setups"]=["prepared pickups and positions","controlled full-speed crossing driven by movement inputs","one cut starts attached; the hit is manual","door starts closed; opening uses client input"]
	await _finish()

func _write_report() -> void:
	var report: Dictionary={"fixture":"gameplay07_demo","checks":checks,"failures":failures,"screen_seconds":elapsed,"observations":observations,"metrics":metrics,"staged_control_demo":true,"preferences_modified":false}
	if not report_path.is_empty():
		DirAccess.make_dir_recursive_absolute(report_path.get_base_dir())
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
	print("GAMEPLAY07_DEMO_RESULT checks=%d failures=%d seconds=%.3f" % [checks,failures,elapsed])
