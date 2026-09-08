extends SceneTree
## Actual production ENet RPCs and packet codec. Only scenario positions are
## prepared on the authority; pickup, charge, release and retrieval use clients.
const Net=preload("res://scripts/network.gd")
const Sim=preload("res://scripts/simulation.gd")
const Pose=preload("res://scripts/human_pose.gd")
const Collision=preload("res://scripts/projectile_collision.gd")
const Audit=preload("res://tests/network_privacy_audit.gd")
var server: Node
var human: Node
var insect: Node
var hid:=0
var mid:=0
var sequence:=0
var action_sequence:=0
var checks:=0
var failures:=0
var surfaces: Array[Node]=[]
var audits: Array=[]

func _initialize() -> void:_run.call_deferred()
func check(value: bool,label: String) -> void:
	checks+=1
	if not value:failures+=1
	print("NET_THROW07 %s %s" % ["PASS" if value else "FAIL",label])

func peer(label: String) -> Node:
	var surface:=Node.new()
	surface.name=label
	root.add_child(surface)
	set_multiplayer(SceneMultiplayer.new(),surface.get_path())
	var net:=Net.new()
	net.name="Network"
	surface.add_child(net)
	surfaces.append(surface)
	return net

func settle(frames: int=8,fresh: bool=true) -> void:
	for frame: int in range(frames):
		if fresh and hid!=0:
			sequence+=1
			human.send_input(sequence,Vector3.ZERO,0.0,0.0,false)
		await physics_frame
	await process_frame

func action(verb: String) -> void:
	action_sequence+=1
	human.send_action(action_sequence,verb,0.0,0.0)

func prepare(tool: String) -> int:
	var roster: Dictionary=server.players.duplicate(true)
	roster[hid].role="human"
	roster[mid].role="mosquito"
	server.sim=Sim.new()
	server.sim.start(roster,{"mode":"blood","human_count":1,"round_seconds":180,"blood_goal":1000})
	server.sim.actors[hid].p=Vector3(-7,0,8)
	server.sim.actors[mid].p=Vector3(6,1.5,-8)
	var selected:=-1
	for id: int in server.sim.pickups:
		server.sim.pickups[id].p=Vector3(5,4,-8)
		if selected<0 and server.sim.pickups[id].tool==tool:selected=id
	var rotation:=Vector3(PI*.5,0,0)
	server.sim.pickups[selected].rotation=rotation
	server.sim.pickups[selected].p=Vector3(-6.6,Collision.resting_offset(tool,rotation),8)
	server._publish(true)
	await settle()
	check(human.private_latest.get("pickup",{}).get("can_take",false),"real visible item offers pickup "+tool)
	action("pickup")
	await settle()
	check(server.sim.actors[hid].tool==tool and int(server.sim.pickups[selected].holder)==hid,"RPC atomically acquires existing object "+tool)
	return selected

func transport_threshold() -> void:
	server.set_physics_process(false)
	var rng:=RandomNumberGenerator.new()
	rng.seed=7007
	var noise:=PackedByteArray()
	for index: int in range(2048):noise.append(rng.randi_range(0,255))
	for large: bool in [false,true]:
		server.server_tick+=1
		var payload: String=noise.hex_encode() if large else "small"
		var state: Dictionary={"phase":"playing","tick":server.server_tick,"actors":{hid:{"role":"human"},mid:{"role":"mosquito"}},"fixture_payload":payload}
		var packet:=var_to_bytes(state).compress(FileAccess.COMPRESSION_DEFLATE)
		check((packet.size()>Net.MAX_UNRELIABLE_SNAPSHOT_BYTES)==large,"fixture exercises actual transport threshold bytes="+str(packet.size()))
		for id: int in [hid,mid]:server._send_snapshot(id,packet)
		for frame: int in range(60):
			if human.last_received_tick>=server.server_tick and insect.last_received_tick>=server.server_tick:break
			await physics_frame
		for client: Node in [human,insect]:check(client.latest.get("fixture_payload","")==payload,"exact decoded payload on "+("reliable fragmented" if large else "unreliable small")+" path")
	server.set_physics_process(true)

func _run() -> void:
	create_timer(35).timeout.connect(func() -> void:printerr("NET_THROW07 watchdog");quit(1))
	server=peer("Authority")
	var port:=42000+int(Time.get_ticks_msec()%8000)
	check(server.host(port)==OK,"authority starts")
	human=peer("HumanClient")
	insect=peer("MosquitoClient")
	for client: Node in [human,insect]:
		var audit=Audit.new()
		audits.append(audit)
		client.private_updated.connect(audit.record_private)
		client.snapshot_updated.connect(func(packet: Dictionary) -> void:audit.record_public(packet,client.multiplayer.get_unique_id()))
	human.connect_room("127.0.0.1",port,"Throw human","",true)
	for attempt: int in range(200):
		if server.players.size()==1:break
		await physics_frame
	insect.connect_room("127.0.0.1",port,"Throw mosquito",server.room_code,false)
	for attempt: int in range(200):
		if server.players.size()==2:break
		await physics_frame
	check(server.players.size()==2,"two clients authenticate normally")
	if server.players.size()!=2:quit(1);return
	hid=human.multiplayer.get_unique_id()
	mid=insect.multiplayer.get_unique_id()
	for tool: String in ["newspaper","slipper"]:
		var selected:=await prepare(tool)
		if tool=="newspaper":await transport_threshold()
		action("throw_start")
		await settle(12)
		check(human.private_latest.get("throw",{}).get("state","")=="charging" and float(human.private_latest.get("throw",{}).get("power",0))>0,"charge comes back from server "+tool)
		check(not insect.private_latest.has("throw"),"mosquito never receives throw authorization "+tool)
		action("throw_cancel")
		await settle()
		action("throw_release")
		await settle(12)
		check(server.sim._projectiles.is_empty() and server.sim.actors[hid].tool==tool,"cancelled charge cannot release later "+tool)
		action("throw_start")
		await settle(12)
		var eye:=Pose.view_origin(server.sim.actors[hid])
		server.sim.actors[mid].p=eye+Vector3.FORWARD*.55
		server._publish(true)
		action("throw_release")
		var saw_flight:=false
		for frame: int in range(80):
			await settle(1)
			for client: Node in [human,insect]:
				var item: Dictionary=client.latest.get("pickups",{}).get(selected,{})
				if item.get("state","")=="flying" and item.get("rotation") is Vector3:saw_flight=true
			if server.sim.actors[mid].state=="stunned" and server.sim.pickups[selected].state=="ground":break
		check(saw_flight,"production codec transmits flying object and Euler rotation "+tool)
		check(server.sim.actors[hid].tool=="hands" and server.sim.pickups[selected].holder==0,"release transfers original pickup once "+tool)
		check(server.sim.actors[mid].state=="stunned" and server.sim.actors[mid].alive,"RPC projectile confirms nonlethal35s impact "+tool)
		await settle()
		check(human.latest.get("actors",{}).get(mid,{}).get("impact",{}).get("tool","")==tool and insect.latest.get("actors",{}).get(mid,{}).get("impact",{}).get("kind","")=="projectile","both clients receive original projectile impact event "+tool)
		var impact_id: int=server.sim.actors[mid].impact.id
		action("throw_release")
		await settle()
		check(server.sim.actors[mid].impact.id==impact_id and server.sim.pickups.size()==10,"duplicate release produces no clone or duplicate impact "+tool)
		for frame: int in range(180):
			if server.sim.pickups[selected].state=="ground":break
			await settle(1)
		check(server.sim.pickups[selected].state=="ground","thrown object settles persistently "+tool)
		var item: Dictionary=server.sim.pickups[selected]
		server.sim.actors[hid].p=Vector3(item.p.x+.65,0,item.p.z)
		server._publish(true)
		await settle()
		action("pickup")
		await settle()
		check(server.sim.actors[hid].tool==tool and item.holder==hid,"client retrieves same landed object through RPC "+tool)
	# Stop simulation, then use the production reliable final publication so
	# private packets cannot remain unaudited merely because channels reorder.
	server.set_physics_process(false)
	var final_tick: int=server.server_tick
	server._publish(true)
	for frame: int in range(60):
		if human.last_received_tick>=final_tick and insect.last_received_tick>=final_tick and int(human.private_latest.get("tick",-1))>=final_tick and int(insect.private_latest.get("tick",-1))>=final_tick:break
		await physics_frame
	for client: Node in [human,insect]:check(client.last_received_tick>=final_tick and int(client.private_latest.get("tick",-1))>=final_tick,"final reliable public/private barrier")
	for audit in audits:check(audit.ok(),"privacy pending=%d matched=%d failures=%s" % [audit.pending_count(),audit.matched_count,str(audit.failures)])
	human.close_client()
	insect.close_client()
	server.multiplayer.multiplayer_peer.close()
	for surface: Node in surfaces:surface.queue_free()
	await process_frame
	print("NET_THROW07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(0 if failures==0 else 1)
