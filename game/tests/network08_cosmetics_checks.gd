extends SceneTree
## Real ENet authentication, lobby updates, lottery start and rematch. No UI,
## preference file, custom gameplay RPC or injected role selection is used.
const Net=preload("res://scripts/network.gd")
const CosmeticsData=preload("res://scripts/cosmetics.gd")
const Audit=preload("res://tests/network_privacy_audit.gd")
var server: Node
var owner: Node
var guest: Node
var old: Node
var surfaces: Array[Node]=[]
var audits: Array=[]
var lobby_packets: Dictionary={}
var checks:=0
var failures:=0
func _initialize() -> void:_run.call_deferred()
func check(value: bool,label: String) -> void:
	checks+=1
	if not value:failures+=1
	print("NET_COSMETICS08 %s %s" % ["PASS" if value else "FAIL",label])
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
func until(predicate: Callable,seconds: float=3.0) -> bool:
	var end:=Time.get_ticks_msec()+int(seconds*1000)
	while not predicate.call() and Time.get_ticks_msec()<end:await physics_frame
	return bool(predicate.call())
func settle(frames: int=8) -> void:
	for frame: int in range(frames):await physics_frame
	await process_frame
func _run() -> void:
	create_timer(25).timeout.connect(func() -> void:printerr("NET_COSMETICS08 watchdog");quit(1))
	server=peer("Authority")
	var port:=43000+int(Time.get_ticks_msec()%6000)
	check(server.host(port)==OK,"authority starts protocol8")
	owner=peer("Owner")
	guest=peer("Guest")
	old=peer("OldProtocol")
	for client: Node in [owner,guest]:
		var audit=Audit.new()
		audits.append(audit)
		client.private_updated.connect(audit.record_private)
		client.lobby_updated.connect(func(packet: Dictionary) -> void:lobby_packets[client.get_instance_id()]=packet)
		client.snapshot_updated.connect(func(packet: Dictionary) -> void:audit.record_public(packet,client.multiplayer.get_unique_id()))
	var owner_raw: Dictionary={"human":{"color":4,"eyes":2,"mouth":0,"brows":1,"mustache":2,"beard":1,"hair_color":5,"accessory":3,"hair":2,"footwear":1},"mosquito":{"color":2,"eyes":0,"mouth":2,"brows":1,"mustache":2,"beard":1,"hair_color":5,"hair":1},"hitbox":99}
	var guest_raw: Dictionary={"human":{"face":2,"eyes":1,"mouth":0,"brows":2,"mustache":1,"beard":2,"hair_color":3,"hair":1},"mosquito":{"eyes":2,"mouth":1,"brows":0,"outfit":2,"footwear":2,"accessory":2}}
	owner.local_cosmetics=owner_raw
	guest.local_cosmetics=guest_raw
	owner.connect_room("127.0.0.1",port,"Piezas1","",true)
	check(await until(func() -> bool:return owner._had_session and server.players.size()==1),"owner authenticates normally")
	guest.connect_room("127.0.0.1",port,"Piezas2",server.room_code,false)
	check(await until(func() -> bool:return guest._had_session and server.players.size()==2),"guest authenticates normally")
	if server.players.size()!=2:quit(1);return
	var oid: int=owner.multiplayer.get_unique_id()
	var gid: int=guest.multiplayer.get_unique_id()
	var owner_clean:=CosmeticsData.sanitize(owner_raw)
	var guest_clean:=CosmeticsData.sanitize(guest_raw)
	check(await until(func() -> bool:return server.players[oid].cosmetics==owner_clean and server.players[gid].cosmetics==guest_clean),"welcome appearance update crosses real RPC and is sanitized")
	await settle()
	for client: Node in [owner,guest]:
		var roster: Dictionary=lobby_packets.get(client.get_instance_id(),{}).get("players",{})
		check(roster.get(oid,{}).get("cosmetics",{})==owner_clean and roster.get(gid,{}).get("cosmetics",{})==guest_clean,"both clients receive independent validated profiles in lobby")
		check(client.latest.get("actors",{}).get(oid,{}).get("appearance",{})==owner_clean.human,"lobby human avatar receives facial pieces and hair color")
	old.multiplayer.connected_to_server.disconnect(old._connected)
	old.multiplayer.connected_to_server.connect(func() -> void:
		old._set_connection_state("joining_room","","Checking old checkpoint")
		old._request_join.rpc_id(1,Net.VERSION,7,"Old07",server.room_code,false,""))
	old.connect_room("127.0.0.1",port,"Old07",server.room_code,false)
	check(await until(func() -> bool:return old.connection_state.get("phase","")=="failed"),"same0.7 version with old protocol7 is rejected")
	check(old.connection_state.get("code","")=="version_mismatch" and "protocolo 8" in str(old.connection_state.get("message","")),"old checkpoint receives explicit mismatch reason")
	check(server.players.size()==2 and not old._had_session and old.private_latest.is_empty(),"rejected old peer obtains no role or private state")
	# Malformed new IDs are tested through the server, not only the pure helper.
	var malformed: Dictionary=guest_raw.duplicate(true)
	malformed.human.eyes=1.0
	malformed.human.mustache="2"
	malformed.human.hair_color=999
	malformed.mosquito.beard=2
	guest.lobby_action("cosmetics",malformed)
	guest_clean=CosmeticsData.sanitize(malformed)
	check(await until(func() -> bool:return server.players[gid].cosmetics==guest_clean),"network rejects float/string/out-of-range IDs without coercion")
	check(server.players[oid].cosmetics==owner_clean,"guest cannot change the owner's profile")
	owner.lobby_action("config",{"mode":"blood","human_count":1,"round_seconds":180})
	await settle()
	owner.lobby_action("ready",true)
	guest.lobby_action("ready",true)
	check(await until(func() -> bool:return server.players[oid].ready and server.players[gid].ready),"both clients ready through lobby RPC")
	owner.lobby_action("start",null)
	check(await until(func() -> bool:return server.sim!=null and owner.latest.get("phase","")=="playing" and guest.latest.get("phase","")=="playing"),"server lottery starts the real match")
	if server.sim==null:quit(1);return
	await settle()
	var selected: Dictionary={}
	var human_count:=0
	for id: int in [oid,gid]:
		var actor: Dictionary=server.sim.actors[id]
		if actor.role=="human":human_count+=1
		selected[id]=CosmeticsData.appearance_for(server.players[id].cosmetics,actor.role)
		check(actor.appearance==selected[id],"simulation copies only the assigned role's canonical pieces")
		if actor.role=="mosquito":check(not actor.appearance.has("hair_color") and not actor.appearance.has("beard") and not actor.appearance.has("mustache"),"match insect omits human-only fields")
		for client: Node in [owner,guest]:check(client.latest.actors[id].appearance==selected[id],"every peer receives exact chosen-role appearance")
	check(human_count==1,"appearance update never affects exact team draw")
	guest.lobby_action("cosmetics",{})
	await settle()
	check(server.sim.actors[gid].appearance==selected[gid] and server.players[gid].cosmetics==guest_clean,"midround appearance update cannot mutate active body")
	server.sim._finish("mosquito","Fixture completes round for rematch")
	server.set_physics_process(false)
	server._publish(true)
	await settle()
	owner.lobby_action("rematch",null)
	check(await until(func() -> bool:return server.sim==null),"owner returns to lobby through production rematch RPC")
	await settle()
	check(server.waiting_actors[oid].appearance==owner_clean.human and server.waiting_actors[gid].appearance==guest_clean.human,"rematch restores saved human lobby appearance for both users")
	# Last playing/results packets already have matching public evidence.
	server.set_physics_process(false)
	await settle()
	for audit in audits:check(audit.ok(),"privacy pending=%d matched=%d failures=%s" % [audit.pending_count(),audit.matched_count,str(audit.failures)])
	for client: Node in [owner,guest,old]:client.close_client()
	server.multiplayer.multiplayer_peer.close()
	for surface: Node in surfaces:surface.queue_free()
	await process_frame
	print("NETWORK08_COSMETICS_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
