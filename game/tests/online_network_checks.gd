extends SceneTree
const Network=preload("res://scripts/network.gd")
const Transport=preload("res://scripts/online_transport.gd")
const Codec=preload("res://scripts/online_packet_codec.gd")
var checks:=0
var failures: Array[String]=[]

class LocalNetwork extends Network:
	var sent: Array[Dictionary]=[]
	var members: Array[int]=[1,2,3]
	var identities: Dictionary={}
	func online_member(id: int) -> bool: return id in members
	func online_user_id(id: int) -> String: return str(identities.get(id,"fixture-user-"+str(id)))
	func _peer_can_receive(id: int) -> bool: return id in [1,2,3]

class AdmissionSession extends Node:
	var admitted: Array[String]=[]
	var rejected: Array[String]=[]
	func mark_peer_admitted(user: String) -> bool:
		admitted.append(user);return true
	func reject_peer(user: String) -> void: rejected.append(user)
	func close() -> void: pass

class RecordingTransport extends "res://scripts/online_transport.gd":
	func send_message(id: int, method: String, args: Array) -> void:
		get_parent().sent.append({"target":id,"method":method,"args":args.duplicate(true)})

func _initialize() -> void: call_deferred("run")
func check(value: bool, label: String) -> void:
	checks+=1
	if not value: failures.append(label);push_error(label)

func run() -> void:
	var n:=LocalNetwork.new();n.name="Network";root.add_child(n)
	n.online_transport.free();n.online_transport=RecordingTransport.new();n.online_transport.name="OnlineTransport";n.add_child(n.online_transport)
	n.online_session=AdmissionSession.new();n.add_child(n.online_session)
	n.set_physics_process(false);n.voice.set_process(false)
	n._online=true;n._local_host=true;n.is_server=true;n.connecting=true
	n._connection_phase="joining_room";n._online_capability="0123456789abcdef0123456789abcdef"
	n._attempt_id=5
	n._handle_request_join(1,n.VERSION,n.PROTOCOL,"Host",n._online_capability,true,"")
	await process_frame;await process_frame
	check(n.players.has(1) and n.room_owner==1 and n._had_session,"host actor 1 accepted in same process")
	check(n._client_connected(),"local host can use client actions")
	n.send_input(1,Vector3.RIGHT,0.1,0.2,false,true,false,false)
	await process_frame
	check(n.waiting_inputs.get(1,{}).get("seq")==1,"local host movement reaches authoritative handler")
	n._handle_request_movement(1,0,Vector3.LEFT,0.0,0.0,false)
	check(n.waiting_inputs[1].seq==1,"stale host movement rejected")
	n._handle_request_join(3,n.VERSION,n.PROTOCOL,"Friend","wrong",false,"")
	check(not n.players.has(3),"wrong online capability rejected")
	n._handle_request_join(2,n.VERSION,n.PROTOCOL,"Friend",n._online_capability,false,"")
	check(n.players.has(2),"lobby member with capability accepted")
	check(n.online_session.admitted==["fixture-user-2"],"only successful remote handshake marks EOS admission")
	n._handle_request_join(3,n.VERSION,n.PROTOCOL,"Racing retry",n._online_capability,false,"")
	check(not n.players.has(3),"rejected peer cannot race a valid handshake before lobby kick")
	# A new P2P peer ID for the same EOS member must not bypass the grace guard.
	n._peer_left(3)
	n.members.append(4);n.identities[4]="fixture-user-3"
	n._handle_request_join(4,n.VERSION,n.PROTOCOL,"Reconnected socket",n._online_capability,false,"")
	check(not n.players.has(4) and not n.online_session.admitted.has("fixture-user-3"),"same PUID cannot gain admission by reconnecting P2P during rejection grace")
	await create_timer(0.22).timeout
	check(n.online_session.rejected==["fixture-user-3"],"bad capability releases EOS slot after explicit reject grace")
	# A genuine EOS departure/rejoin creates a new membership. An older grace
	# callback must not kick that newly admitted membership of the same PUID.
	n._online_member_departed("fixture-user-3")
	n._handle_request_join(3,n.VERSION,n.PROTOCOL,"Old membership","wrong",false,"")
	n._peer_left(3)
	n._online_member_departed("fixture-user-3")
	n._handle_request_join(4,n.VERSION,n.PROTOCOL,"New membership",n._online_capability,false,"")
	check(n.players.has(4),"confirmed EOS departure permits fresh membership admission")
	await create_timer(0.22).timeout
	check(n.players.has(4) and n.online_session.rejected==["fixture-user-3"],"stale rejection callback cannot kick newly admitted EOS membership")
	n._online_member_departed("fixture-user-3")
	n._handle_request_join(3,n.VERSION,n.PROTOCOL,"Imposter",n._online_capability,true,"")
	check(not n.players.has(3),"remote cannot create room")
	n._online_member_departed("fixture-user-3")
	n._handle_request_join(3,n.VERSION,n.PROTOCOL+1,"Wrong version",n._online_capability,false,"")
	check(not n.players.has(3),"online protocol mismatch rejected")
	n._dispatch_message(99,"_request_lobby",["ready",true])
	check(not n.players[1].ready and not n.players[2].ready,"nonmember cannot invoke lobby actions")
	n._dispatch_message(2,"_request_lobby",["config",{"human_count":5}])
	check(n.config.human_count==1,"remote cannot change host rules")
	n._local_dispatch(4,"_request_lobby",["ready",true])
	check(not n.players[1].ready,"previous attempt local delivery ignored")
	n.lobby_action("ready",true)
	await process_frame
	check(n.players[1].ready,"host ready uses shared handler")
	n._pending_map={"id":"house","fingerprint":"test","ready":{}}
	n._receive_map_ack(1,"map_ready",{"id":"other","fingerprint":"test"})
	check(n._pending_map.ready.is_empty(),"wrong map ACK ignored")
	n._receive_map_ack(1,"map_ready",{"id":"house","fingerprint":"test"})
	check(n._pending_map.ready.has(1) and n.sim==null,"host ACK waits for friend ACK")
	n._receive_map_ack(2,"map_ready",{"id":"house","fingerprint":"wrong"})
	check(n._pending_map.is_empty() and n.sim==null,"map mismatch cancels round for everyone")
	var secret: Dictionary={"tick":50,"assignment":{"revision":1,"zone":"host_only"}}
	n._send_to(1,"_receive_private_reliable",[secret])
	secret.assignment.zone="mutated_on_server"
	await process_frame
	check(n.private_latest.assignment.zone=="host_only","local private state cloned before delivery")
	var leaked:=false
	for sent: Dictionary in n.sent:
		if sent.method.begins_with("_receive_private") and sent.args[0].get("assignment",{}).get("zone")=="host_only": leaked=true
	check(not leaked,"host private data never broadcast to friends")
	# Voice permission delivery to the host is supported without self-RPC.
	n._send_to(1,"voice:_permission",[2,{"epoch":1,"permit":7,"first_sequence":0}])
	await process_frame
	check(n.voice.permissions.has(2),"host receives remote speaker permission locally")
	var received: Array=[]
	n.voice.packet_received.connect(func(speaker: int,epoch: int,permit: int,seq: int,packet: PackedByteArray): received.append([speaker,epoch,permit,seq,packet]))
	n._send_to(1,"voice:_deliver",[2,1,6,0,PackedByteArray([1])])
	await process_frame
	check(received.is_empty(),"host rejects voice frame with wrong permit")
	n._send_to(1,"voice:_deliver",[2,1,7,0,PackedByteArray([1])])
	await process_frame
	check(received.size()==1 and received[0][0]==2,"authorized friend voice reaches host playback")
	n.voice.set_peer_muted(2,true)
	await process_frame
	check(n.voice._receiver_mutes.get(1,{}).has(2),"host mute reaches authoritative per-receiver state")
	# Real Opus encoding and authoritative simulation, with staged nearby actors.
	n.sim=Network.Simulation.new()
	n.sim.start({1:{"name":"Host","role":"human"},2:{"name":"Friend","role":"mosquito"}},n.config)
	n.sim.actors[1].p=Vector3.ZERO;n.sim.actors[2].p=Vector3(0.3,1,0)
	n.voice._ensure_map()
	var encoder: RefCounted=n.voice.Codec.create()
	check(encoder!=null,"native Opus codec available for host microphone path")
	if encoder!=null:
		var pcm:=PackedFloat32Array();pcm.resize(960)
		for i: int in 960: pcm[i]=0.1*sin(TAU*220*i/48000.0)
		var frame: PackedByteArray=encoder.encode_frame(pcm)
		n.voice.begin(10)
		await process_frame
		check(n.voice.streams.has(1),"host voice begin creates own authenticated stream")
		n.voice.send_frame(10,0,frame)
		await process_frame
		var forwarded:=false
		for sent: Dictionary in n.sent:
			if sent.target==2 and sent.method=="voice:_deliver" and sent.args[0]==1 and sent.args[4]==frame: forwarded=true
		check(forwarded,"real encoded host voice is routed to nearby friend")
		# P2P and game player remain connected, but EOS has revoked membership.
		n.members.erase(2);n.sent.clear()
		n.voice.send_frame(10,1,frame)
		await process_frame
		check(n.sent.is_empty(),"revoked EOS member receives no voice despite live game/P2P slot")
		n.voice._update_permissions(1,2,true)
		check(not n.voice.streams[1].recipients.has(2),"revoked member loses authoritative voice permit")
		n._send_to(2,"voice:_permission",[1,{"epoch":10,"permit":99,"first_sequence":2}])
		n._send_to(2,"voice:_deliver",[1,10,99,2,frame])
		n._send_to(2,"_receive_lobby",[{}])
		check(n.sent.is_empty(),"common outbound dispatcher blocks permission audio and gameplay for revoked member")
		n.members.append(2)
		n.voice.hard_stop(10)
		await process_frame
		check(not n.voice.streams.has(1),"host release revokes stream without self RPC")
	check(not Transport.valid_message("queue_free",[]),"arbitrary methods rejected")
	check(not Transport.valid_message("_request_movement",[1,"wrong",0.0,0.0,false,false,false,false]),"typed envelope validated")
	check(not Transport.reliable_method("_request_movement") and not Transport.reliable_method("voice:_deliver"),"EOS movement and voice use genuine unreliable mode")
	var payload: Dictionary={"method":"_receive_lobby","args":[{"objects":Crypto.new().generate_random_bytes(12000)}]}
	var encoded: Dictionary=Codec.encode(payload,Codec.Kind.CONTROL,17,1,0)
	check(encoded.ok and encoded.frames.size()>1,"large lobby fragmented")
	var reassembler:=Codec.new();reassembler.reset(17)
	var result: Dictionary={}
	var bounded:=true
	for frame: PackedByteArray in encoded.frames:
		bounded=bounded and frame.size()<=1032
		result=reassembler.ingest(1,frame,100,true)
	check(bounded,"every gameplay frame fits conservative EOS budget")
	check(result.get("complete",false) and result.payload==payload,"fragmented lobby lossless roundtrip")
	n.members.erase(2)
	n._online_member_departed("fixture-user-2")
	check(not n.players.has(2) and not n.waiting_actors.has(2),"EOS departure removes gameplay actor before P2P disconnect")
	n._online_member_departed("fixture-user-2")
	check(n.players.size()==1 and n.players.has(1),"repeated EOS/P2P departure cannot remove another player")
	n.close_client()
	check(not n._online and not n.is_server and n.players.is_empty() and n.voice.permissions.is_empty(),"close clears local host and voice state")
	n.queue_free();await process_frame
	print("ONLINE_NETWORK_CHECKS checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
