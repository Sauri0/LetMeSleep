extends SceneTree
## Two actual ENet clients and one authority in isolated multiplayer subtrees.
## Server-only prepared positions; all player inputs/actions and received states
## travel through the production RPCs. No alternate gameplay RPC is introduced.
const Net = preload("res://scripts/network.gd")
const Sim = preload("res://scripts/simulation.gd")
const Pose = preload("res://scripts/human_pose.gd")
const Audit = preload("res://tests/network_privacy_audit.gd")
var server: Node
var human: Node
var insect: Node
var hid := 0
var mid := 0
var checks := 0
var failures := 0
var sequence := 0
var action_sequence := 0
var audits: Array = []
var surfaces: Array[Node] = []

func _initialize() -> void: _run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks+=1
	if not ok: failures+=1
	print("NET_COMBAT07 %s %s" % ["PASS" if ok else "FAIL",label])

func peer(label: String) -> Node:
	var surface := Node.new()
	surface.name=label
	root.add_child(surface)
	set_multiplayer(SceneMultiplayer.new(),surface.get_path())
	var net:=Net.new()
	net.name="Network"
	surface.add_child(net)
	surfaces.append(surface)
	return net

func settle(count: int = 8) -> void:
	for frame: int in range(count): await physics_frame
	await process_frame

func reset(tool: String="hands") -> void:
	var roster: Dictionary=server.players.duplicate(true)
	roster[hid].role="human"
	roster[mid].role="mosquito"
	server.sim=Sim.new()
	server.sim.start(roster,{"mode":"blood","human_count":1,"round_seconds":180,"blood_goal":1000})
	server.sim.actors[hid].p=Vector3(-7,0,8)
	server.sim.actors[hid].tool=tool
	server.sim.actors[mid].p=Vector3(6,1.5,-8)
	sequence+=1
	human.send_input(sequence,Vector3.ZERO,0,0,false)
	insect.send_input(sequence,Vector3.ZERO,0,0,false)
	server._publish(true)
	await settle()

func _run() -> void:
	create_timer(25).timeout.connect(func() -> void: printerr("NET_COMBAT07 watchdog"); quit(1))
	server=peer("Authority")
	var port := 42000+int(Time.get_ticks_msec()%8000)
	check(server.host(port)==OK,"local authority starts")
	human=peer("HumanClient")
	insect=peer("MosquitoClient")
	for client: Node in [human,insect]:
		var audit=Audit.new()
		audits.append(audit)
		client.private_updated.connect(audit.record_private)
		client.snapshot_updated.connect(func(packet: Dictionary) -> void: audit.record_public(packet,client.multiplayer.get_unique_id()))
	human.connect_room("127.0.0.1",port,"Human test","",true)
	for attempt: int in range(200):
		if server.players.size()==1: break
		await physics_frame
	insect.connect_room("127.0.0.1",port,"Mosquito test",server.room_code,false)
	for attempt: int in range(200):
		if server.players.size()==2: break
		await physics_frame
	check(server.players.size()==2,"two authenticated clients complete normal join")
	if server.players.size()!=2: quit(1); return
	hid=human.multiplayer.get_unique_id()
	mid=insect.multiplayer.get_unique_id()
	for tool: String in Sim.TOOL_STATS:
		await reset(tool)
		var eye:=Pose.view_origin(server.sim.actors[hid])
		server.sim.actors[mid].p=eye+Vector3(.075,0,-.22)
		server._publish(true)
		await settle()
		check(bool(human.private_latest.get("attack",{}).get("candidate",false)),"human receives physical opportunity "+tool)
		check(not insect.private_latest.has("attack") and not insect.private_latest.has("pickup"),"other role cannot receive human cues "+tool)
		action_sequence+=1
		human.send_action(action_sequence,"attack",0,0)
		await settle(30)
		check(server.sim.actors[mid].state=="stunned" and server.sim.actors[mid].alive,"RPC hit stuns for blood mode "+tool)
		check(human.latest.get("actors",{}).get(mid,{}).get("state","")=="stunned" and insect.latest.get("actors",{}).get(mid,{}).get("state","")=="stunned","both clients receive authoritative reaction "+tool)
		check(human.private_latest.get("attack",{}).get("state","")=="hit","only attacker receives confirmed hit "+tool)
	await reset("broom")
	var actor: Dictionary=server.sim.actors[hid]
	actor.p=Vector3(-.7,0,-8.2)
	actor.yaw=PI*.5
	actor.body_yaw=PI*.5
	server.sim.doors.kitchen.angle=0
	server.sim.doors.kitchen.target_angle=0
	server.sim.actors[mid].p=Vector3(-2.6,Pose.view_origin(actor).y,-8.2)
	sequence+=1
	human.send_input(sequence,Vector3.ZERO,PI*.5,0,false)
	await settle()
	check(not human.private_latest.get("attack",{}).get("candidate",true),"closed leaf hides opportunity across network")
	action_sequence+=1
	human.send_action(action_sequence,"attack",PI*.5,0)
	await settle(30)
	check(server.sim.actors[mid].state=="flying","network attack cannot bypass closed leaf")
	await reset()
	var plan: Dictionary=server.sim._strike_plan(hid)
	server.sim.actors[mid].p=Vector3(plan.point)-Vector3.RIGHT*3.8*(1.0/60.0+.12)
	server.sim.actors[mid].velocity=Vector3.RIGHT*3.8
	sequence+=1
	insect.send_input(sequence,Vector3.RIGHT,0,0,false)
	action_sequence+=1
	human.send_action(action_sequence,"attack",0,0)
	for frame: int in range(25):
		sequence+=1
		insect.send_input(sequence,Vector3.RIGHT,0,0,false)
		await physics_frame
	await settle()
	check(server.sim.actors[mid].state=="stunned","manual lead catches a flying peer through production RPCs")
	# Public and private unreliable channels may arrive on different frames.
	# Freeze this completed fixture and send one final production reliable pair,
	# so the audit cannot finish while its last private tick lacks public evidence.
	server.set_physics_process(false)
	var final_tick: int = server.server_tick
	server._publish(true)
	for attempt: int in range(60):
		if human.last_received_tick >= final_tick and insect.last_received_tick >= final_tick and int(human.private_latest.get("tick",-1)) >= final_tick and int(insect.private_latest.get("tick",-1)) >= final_tick:
			break
		await physics_frame
	for client: Node in [human,insect]:
		check(client.last_received_tick >= final_tick and int(client.private_latest.get("tick",-1)) >= final_tick,"final public/private production barrier reaches client")
	for audit in audits:
		check(audit.ok(),"production public/private packets preserve privacy pending=%d matched=%d "%[audit.pending_count(),audit.matched_count]+str(audit.failures))
	human.close_client()
	insect.close_client()
	server.multiplayer.multiplayer_peer.close()
	for surface: Node in surfaces: surface.queue_free()
	await process_frame
	print("NET_COMBAT07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
