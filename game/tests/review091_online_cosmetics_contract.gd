extends SceneTree
## Reviewer-owned integration of production Network + OnlineTransport + generated
## map/cosmetics. ENet and a session fixture replace EOS SDK/P2P only; this is
## explicitly not a relay, NAT, two-machine or WAN result.
const Network=preload("res://scripts/network.gd")
const Invitation=preload("res://scripts/online_invitation.gd")
const Generator=preload("res://scripts/procedural_house.gd")
const Cosmetics=preload("res://scripts/cosmetics.gd")
const LOBBY:="review091-loopback"
const HOST_USER:="review091-host"
const GUEST_USER:="review091-guest"

class LoopbackPeer extends ENetMultiplayerPeer:
	var users:Dictionary={}
	func get_peer_user_id(id:int)->String:return str(users.get(id,""))

class FixtureBackend extends RefCounted:
	var busy:=false
	func shutdown()->Dictionary:return {"ok":true}

class FixtureSession extends Node:
	signal state_changed(state:String)
	signal failed(reason:String)
	signal transport_ready(info:Dictionary)
	signal member_departed(user_id:String)
	var backend:=FixtureBackend.new()
	var _cleanup:Array=[]
	var peer:LoopbackPeer
	var members:Array[String]=[HOST_USER,GUEST_USER]
	var admitted:Array[String]=[]
	var closed:=false
	func start_host(_config:Dictionary,_name:String,protocol:int,capability:String)->void:
		if protocol!=Invitation.PROTOCOL or not Invitation.valid_capability(capability):failed.emit("invalid_fixture_request");return
		transport_ready.emit({"peer":peer,"is_host":true,"lobby_id":LOBBY})
	func start_join(_config:Dictionary,_name:String,invitation:Dictionary)->void:
		if invitation.get("lobby_id")!=LOBBY:failed.emit("invalid_fixture_lobby");return
		transport_ready.emit({"peer":peer,"is_host":false,"lobby_id":LOBBY})
	func is_lobby_member(user:String)->bool:return user in members
	func mark_peer_admitted(user:String)->bool:
		if user not in members:return false
		admitted.append(user);return true
	func reject_peer(user:String)->void:
		members.erase(user);member_departed.emit(user)
	func close()->void:closed=true

var checks:=0
var failures:Array[String]=[]
var host_side:Node
var guest_side:Node
var host:Node
var guest:Node
var host_session:FixtureSession
var guest_session:FixtureSession
var host_peer:LoopbackPeer
var guest_peer:LoopbackPeer
var guest_id:=0
var lobby_packets:Dictionary={}
var report_path:="res://../work/review091-online-cosmetics-results.json"

func _initialize()->void:
	for argument:String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):report_path=argument.trim_prefix("--report=")
	_run.call_deferred()

func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:
		failures.append(label)
		if failures.size()<=30:printerr("REVIEW091_ONLINE_FAIL "+label)
	return ok

func until(predicate:Callable,seconds:float=5.0)->bool:
	var deadline:=Time.get_ticks_msec()+int(seconds*1000.0)
	while not predicate.call() and Time.get_ticks_msec()<deadline:await create_timer(.01).timeout
	return bool(predicate.call())

func settle(frames:int=10)->void:
	for frame:int in range(frames):await physics_frame
	await process_frame

func _attach(network:Node,peer:LoopbackPeer)->FixtureSession:
	var session:=FixtureSession.new();session.name="OnlineSession";session.peer=peer
	network.add_child(session);network.online_session=session
	session.state_changed.connect(network._online_state)
	session.failed.connect(network._online_failed)
	session.transport_ready.connect(network._online_ready)
	session.member_departed.connect(network._online_member_departed)
	return session

func _cleanup()->void:
	if is_instance_valid(guest):guest.close_client()
	if is_instance_valid(host):host.close_client()
	if guest_peer!=null:guest_peer.close()
	if host_peer!=null:host_peer.close()
	if is_instance_valid(guest_side):guest_side.queue_free()
	if is_instance_valid(host_side):host_side.queue_free()
	await process_frame

func _finish(extra:Dictionary={})->void:
	var report:Dictionary={"checks":checks,"failures":failures,"transport":"real SceneMultiplayer and OnlineTransport over ENet loopback","eos_sdk":false,"p2p":false,"relay":false,"nat":false,"two_machine":false,"wan":false,"wan_status":"pending user test after polish","protocol":Invitation.PROTOCOL,"extra":extra,"source_sha256":{}}
	for path:String in ["res://tests/review091_online_cosmetics_contract.gd","res://scripts/network.gd","res://scripts/online_transport.gd","res://scripts/online_invitation.gd","res://scripts/simulation.gd","res://scripts/cosmetics.gd","res://scripts/procedural_house.gd"]:
		report.source_sha256[path]=FileAccess.get_sha256(path)
	if not report_path.is_empty():
		var file:=FileAccess.open(report_path,FileAccess.WRITE)
		if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
		else:check(false,"review online report path writable")
	for failure:String in failures:print("REVIEW091_ONLINE_DIAGNOSTIC "+failure)
	await _cleanup()
	print("REVIEW091_ONLINE_RESULT checks=%d failures=%d eos_sdk=false relay=false wan=false"%[checks,failures.size()])
	quit.call_deferred(0 if failures.is_empty() else 1)

func _run()->void:
	create_timer(30).timeout.connect(func()->void:printerr("REVIEW091_ONLINE watchdog");quit(1))
	host_peer=LoopbackPeer.new()
	if not check(host_peer.create_server(0,4,4)==OK,"open loopback authority peer"):await _finish();return
	host_peer.get_host().bandwidth_limit(0,0)
	var port:=host_peer.get_host().get_local_port()
	guest_peer=LoopbackPeer.new()
	if not check(guest_peer.create_client("127.0.0.1",port,4)==OK,"open loopback guest peer"):await _finish();return
	guest_id=guest_peer.get_unique_id()
	check(guest_id>1,"guest receives distinct transport peer ID")
	host_peer.users={1:HOST_USER,guest_id:GUEST_USER};guest_peer.users=host_peer.users.duplicate()
	host_side=Node.new();host_side.name="ReviewHost";root.add_child(host_side)
	guest_side=Node.new();guest_side.name="ReviewGuest";root.add_child(guest_side)
	set_multiplayer(SceneMultiplayer.new(),host_side.get_path())
	set_multiplayer(SceneMultiplayer.new(),guest_side.get_path())
	host=Network.new();host.name="Network";host_side.add_child(host)
	guest=Network.new();guest.name="Network";guest_side.add_child(guest)
	host_session=_attach(host,host_peer);guest_session=_attach(guest,guest_peer)
	host.lobby_updated.connect(func(packet:Dictionary)->void:lobby_packets.host=packet.duplicate(true))
	guest.lobby_updated.connect(func(packet:Dictionary)->void:lobby_packets.guest=packet.duplicate(true))
	var host_raw:Dictionary={"human":{"color":4,"eyes":2,"mouth":0,"brows":1,"hair":2,"hair_color":5,"mustache":2,"beard":1,"outfit":1,"accessory":3,"footwear":2,"accent":3},"mosquito":{"color":1,"eyes":2,"mouth":1,"brows":0,"hair":2,"outfit":1,"accessory":1,"footwear":2,"accent":5}}
	var guest_raw:Dictionary={"human":{"color":2,"eyes":0,"mouth":2,"brows":2,"hair":1,"hair_color":3,"mustache":1,"beard":2,"outfit":2,"accessory":2,"footwear":1,"accent":4},"mosquito":{"color":5,"eyes":1,"mouth":0,"brows":2,"hair":1,"outfit":2,"accessory":2,"footwear":1,"accent":2}}
	host.local_cosmetics=host_raw;guest.local_cosmetics=guest_raw
	host.host_online({},"QA Host")
	if not check(await until(func()->bool:return host._had_session),"online entry accepts local host through fixture session"):await _finish();return
	check(Invitation.decode(host.invitation_text).get("ok",false),"host creates current valid invitation")
	guest.join_online({},"QA Guest",host.invitation_text)
	if not check(await until(func()->bool:return guest._had_session and host.players.size()==2),"guest handshake crosses production online transport"):await _finish();return
	check(host_session.admitted==[GUEST_USER],"session identity admitted only after game handshake")
	var host_clean:=Cosmetics.sanitize(host_raw);var guest_clean:=Cosmetics.sanitize(guest_raw)
	if not check(await until(func()->bool:return host.players[1].cosmetics==host_clean and host.players[guest_id].cosmetics==guest_clean),"distinct sanitized cosmetics reach authority"):await _finish();return
	await settle()
	for side:String in ["host","guest"]:
		var roster:Dictionary=Dictionary(lobby_packets.get(side,{})).get("players",{})
		check(roster.get(1,{}).get("cosmetics",{})==host_clean and roster.get(guest_id,{}).get("cosmetics",{})==guest_clean,side+" lobby sees both independent profiles")
	host.lobby_action("config",{"mode":"blood","human_count":1,"round_seconds":180,"blood_goal":1000})
	await settle()
	check(str(host.config.map_id)=="house","host accepts the sanitized pre-round map config")
	host.lobby_action("ready",true);guest.lobby_action("ready",true)
	if not check(await until(func()->bool:return bool(host.players[1].ready) and bool(host.players[guest_id].ready)),"both ready actions cross transport"):await _finish();return
	host.lobby_action("start",null)
	if not check(await until(func()->bool:return host.sim!=null and guest.latest.get("phase","")=="playing",10.0),"round starts after generated-map ACK barrier"):await _finish();return
	var map_id:=str(host.config.map_id)
	var map_seed:=Generator.parse_seed(map_id)
	check(map_seed>0,"authority selects a current v2 generated map for the round")
	check(int(host.config.get("map_generator_version",-1))==Generator.VERSION and int(host.config.get("map_seed",-1))==map_seed,"authority stamps the generated version and canonical seed")
	check(host._pending_map.is_empty() and host._prepared_map==guest._prepared_map,"host and guest acknowledge identical generated blueprint")
	check(str(host._prepared_map.id)==map_id and str(guest.latest.get("config",{}).get("map_fingerprint",""))==str(host._prepared_map.fingerprint),"published map identity/fingerprint match acknowledged geometry")
	var acknowledged_fingerprint:=str(host._prepared_map.fingerprint)
	var chosen:Dictionary={}
	for id:int in [1,guest_id]:
		var profile:Dictionary=host_clean if id==1 else guest_clean
		var role:String=host.sim.actors[id].role
		chosen[id]=Cosmetics.appearance_for(profile,role)
		check(host.sim.actors[id].appearance==chosen[id] and guest.latest.actors[id].appearance==chosen[id],"actor %d receives exact selected-role appearance"%id)
	host.send_input(1,Vector3.FORWARD,0.2,-.3,false)
	guest.send_input(1,Vector3.LEFT,-.4,.2,false)
	check(await until(func()->bool:return host.sim.actors[1]._input_seq==1 and host.sim.actors[guest_id]._input_seq==1),"both local and remote movement reach authority")
	var tick_before:=guest.last_received_tick
	check(await until(func()->bool:return guest.last_received_tick>tick_before),"guest continues receiving live public snapshots")
	host.sim._finish("mosquito","Reviewer fixture completes results")
	host.set_physics_process(false);host._publish(true);await settle()
	host.lobby_action("rematch",null)
	check(await until(func()->bool:return host.sim==null),"owner rematch returns both peers to lobby")
	await settle()
	check(host.waiting_actors[1].appearance==host_clean.human and host.waiting_actors[guest_id].appearance==guest_clean.human,"rematch restores each saved human lobby appearance")
	guest.close_client()
	check(await until(func()->bool:return host.players.size()==1 and host.players.has(1)),"guest disconnect removes only remote player")
	await _finish({"map_id":map_id,"fingerprint":acknowledged_fingerprint,"host_appearance":chosen.get(1,{}),"guest_appearance":chosen.get(guest_id,{})})
