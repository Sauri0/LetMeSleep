extends SceneTree
## Real Network/Voice RPC nodes and ENet peers. Synthetic PCM only; deliberately
## no VoiceSession, capture, decoder, AudioStreamPlayer, UI or device access.
const Net=preload("res://scripts/network.gd")
const Sim=preload("res://scripts/simulation.gd")
const Codec=preload("res://scripts/voice_codec.gd")
const Maps=preload("res://scripts/map_catalog.gd")
const Doors=preload("res://scripts/door_catalog.gd")
class IngressProbe:
	extends "res://scripts/voice_transport.gd"
	var ingress: Array[Dictionary]=[]
	@rpc("any_peer","call_remote","unreliable",3)
	func _request_frame(e: int,sequence: int,packet: PackedByteArray) -> void:
		var sender:=multiplayer.get_remote_sender_id()
		ingress.append({"sender":sender,"epoch":e,"sequence":sequence,"bytes":packet.size(),"authenticated":network.players.has(sender),"stream":streams.get(sender,{}).duplicate(true),"available":available(),"phase":network.sim.phase if network.sim!=null else "none"})
		super._request_frame(e,sequence,packet)
var surfaces: Array[Node]=[]
var peers: Array[Node]=[]
var clients: Array[Node]=[]
var authority: Node
var stranger: Node
var checks:=0
var failures: Array[String]=[]
var results: Array[Dictionary]=[]
var packets: Dictionary={}
var permissions: Dictionary={}
var endings: Dictionary={}
var epoch:=0
var sample:=PackedByteArray()
var finished:=false
var evidence: Dictionary={}
var probe_ingress:=false

func _initialize() -> void:
	multiplayer_poll=OS.get_cmdline_user_args().has("--poll-native")
	probe_ingress=OS.get_cmdline_user_args().has("--trace-ingress")
	_run.call_deferred()

func check(ok: bool,label: String) -> bool:
	checks+=1;results.append({"ok":ok,"label":label})
	if not ok: failures.append(label)
	print("NET_VOICE09 %s %s"%["PASS" if ok else "FAIL",label])
	return ok

func peer(label: String) -> Node:
	var surface:=Node.new();surface.name=label;root.add_child(surface)
	set_multiplayer(SceneMultiplayer.new(),surface.get_path())
	var node:=Net.new();node.name="Network";surface.add_child(node)
	if probe_ingress:
		node.voice.free();node.voice=IngressProbe.new();node.voice.name="Voice";node.add_child(node.voice)
	surfaces.append(surface);peers.append(node)
	packets[node]=[];permissions[node]=[];endings[node]=[]
	node.voice.packet_received.connect(func(speaker: int,e: int,permit: int,sequence: int,packet: PackedByteArray)->void:
		packets[node].append({"speaker":speaker,"epoch":e,"permit":permit,"sequence":sequence,"size":packet.size(),"bytes_match":packet==sample}))
	node.voice.permission_changed.connect(func(speaker: int,value: Dictionary)->void:
		permissions[node].append({"speaker":speaker,"value":value.duplicate(true)}))
	node.voice.stream_finished.connect(func(speaker: int,e: int,permit: int,sequence: int)->void:
		endings[node].append({"speaker":speaker,"epoch":e,"permit":permit,"sequence":sequence}))
	return node

func settle(frames: int=6) -> void:
	for frame: int in range(frames):
		if not multiplayer_poll:
			for node: Node in peers:
				if node.multiplayer.multiplayer_peer is ENetMultiplayerPeer: node.multiplayer.poll()
		await physics_frame
	await process_frame

func until(predicate: Callable,label: String,frames: int=120) -> bool:
	for frame: int in range(frames):
		if predicate.call(): return check(true,label)
		await settle(1)
	return check(false,label)

func id(node: Node) -> int:
	return node.multiplayer.get_unique_id()

func accepted(receiver: Node,speaker: int,sequence: int=-1) -> int:
	var count:=0
	for packet: Dictionary in packets[receiver]:
		if int(packet.speaker)==speaker and (sequence<0 or int(packet.sequence)==sequence): count+=1
	return count

func place_default() -> void:
	var points: Array[Vector3]=[Vector3(0,0,-.9),Vector3(0,0,.9),Vector3(.3,1.48,0),Vector3(-.3,1.48,0)]
	for index: int in range(clients.size()): authority.sim.actors[id(clients[index])].p=points[index]

func stage(map_id: String="house",roles: Array[String]=["human","human","mosquito","mosquito"]) -> void:
	var roster: Dictionary={}
	for index: int in range(clients.size()): roster[id(clients[index])]={"role":roles[index]}
	var simulation:=Sim.new()
	simulation.start(roster,{"mode":"blood","human_count":2,"map_id":map_id,"round_seconds":180,"blood_goal":1000})
	authority.sim=simulation
	check(simulation.phase=="playing","staged authoritative round validates real map "+map_id)
	if not evidence.has("rounds"): evidence.rounds=[]
	evidence.rounds.append({"map_id":map_id,"fingerprint":simulation.config.get("map_fingerprint",""),"roles":roles.duplicate()})
	if map_id=="house": place_default()
	else:
		var floor_point: Vector3=simulation._map_data.human_spawns[0]
		for index: int in range(clients.size()):
			var actor: Dictionary=simulation.actors[id(clients[index])]
			actor.p=floor_point+Vector3.RIGHT*.15*index+(Vector3.UP*1.48 if actor.role=="mosquito" else Vector3.ZERO)

func begin_stream(speaker: Node) -> bool:
	epoch+=1
	speaker.voice.begin(epoch)
	return await until(func()->bool:return authority.voice.streams.has(id(speaker)) and int(authority.voice.streams[id(speaker)].epoch)==epoch,"authenticated begin epoch "+str(epoch),40)

func send(speaker: Node,sequence: int,frames: int=3) -> void:
	if probe_ingress: speaker.voice._request_frame.rpc_id(1,epoch,sequence,sample)
	else: speaker.voice.send_frame(epoch,sequence,sample)
	await settle(frames)
	if not evidence.has("trace"): evidence.trace=[]
	var item: Dictionary={"speaker":id(speaker),"sequence":sequence,"epoch":epoch,"sender_connected":speaker._client_connected(),"packet_rate":authority.voice._packet_rates.get(id(speaker),{}).duplicate(true),"validator_error":authority.voice._validator.get_last_error(),"validator_message":authority.voice._validator.get_last_error_message(),"server":authority.voice.streams.get(id(speaker),{}).duplicate(true),"clients":[]}
	var wire: ENetPacketPeer=speaker.multiplayer.multiplayer_peer.get_peer(1)
	item.enet={"throttle":wire.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE),"limit":wire.get_statistic(ENetPacketPeer.PEER_PACKET_THROTTLE_LIMIT),"rtt":wire.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME),"last_rtt":wire.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME)}
	if probe_ingress:item.ingress_count=authority.voice.ingress.size()
	for node: Node in clients:
		item.clients.append({"id":id(node),"permission":node.voice.permissions.get(id(speaker),{}).duplicate(true),"received":accepted(node,id(speaker)),"at_sequence":accepted(node,id(speaker),sequence)})
	evidence.trace.append(item)

func stop(speaker: Node) -> void:
	speaker.voice.hard_stop(epoch)
	await settle(4)

func _run() -> void:
	evidence.source_sha256={}
	for path: String in ["res://scripts/voice_transport.gd","res://scripts/voice_acoustics.gd","res://scripts/voice_codec.gd","res://addons/lms_opus/bin/lms_opus.windows.x86_64.dll"]:
		evidence.source_sha256[path]=FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable_in_pack"
	create_timer(48).timeout.connect(func()->void:
		if not finished: check(false,"48 second watchdog");_finish.call_deferred())
	var encoder: RefCounted=Codec.create()
	if not check(encoder!=null and encoder.has_method("validate_frame"),"native v2 codec exposes structural frame validation"): await _finish();return
	var pcm:=PackedFloat32Array();pcm.resize(960)
	for index: int in range(960): pcm[index]=.12*sin(TAU*220.0*float(index)/48000.0)
	sample=encoder.encode_frame(pcm)
	if not check(sample.size()>0 and sample.size()<=160 and encoder.validate_frame(sample),"synthetic 20ms mono PCM produces valid bounded Opus frame"): await _finish();return
	evidence.packet_bytes=sample.size();evidence.opus=encoder.get_opus_version()
	authority=peer("VoiceAuthority")
	var port:=43000+int(Time.get_ticks_msec()%7000)
	if not check(authority.host(port)==OK,"real ENet server binds four RPC channels"): await _finish();return
	for index: int in range(4):
		var node:=peer("VoiceClient%d"%index);clients.append(node)
		node.connect_room("127.0.0.1",port,"Voice fixture %d"%index,"" if index==0 else authority.room_code,index==0)
		if not await until(func()->bool:return node._had_session and authority.players.has(id(node)),"client %d authenticates through room RPC"%index): await _finish();return
	for node: Node in peers: check(node.voice.available(),"codec ready in real voice transport "+str(node.get_parent().name))
	# Only actor/map/role setup and freezing gameplay are staged. Voice clocks,
	# authenticated identities, all control/frame/permission RPCs remain real.
	authority.set_physics_process(false)
	stage()
	stranger=peer("VoiceUnauthenticated")
	var raw:=ENetMultiplayerPeer.new()
	check(raw.create_client("127.0.0.1",port,4)==OK,"raw four-channel peer opens without room authentication")
	stranger.multiplayer.multiplayer_peer=raw
	if not await until(func()->bool:return raw.get_connection_status()==MultiplayerPeer.CONNECTION_CONNECTED and authority.multiplayer.get_peers().has(id(stranger)),"raw unauthenticated peer connects"): await _finish();return
	stranger.voice._request_control.rpc_id(1,"begin",99,-1)
	stranger.voice._request_frame.rpc_id(1,99,0,sample)
	await settle()
	check(not authority.players.has(id(stranger)) and not authority.voice.streams.has(id(stranger)),"unauthenticated ENet peer cannot create or emit a stream")
	await _roles_and_validation(encoder)
	if OS.get_cmdline_user_args().has("--probe-short"): await _finish();return
	await _permissions_and_mute()
	await _resume_permission()
	await _rate_and_finish()
	await _round_lifecycle()
	await _finish()

func _roles_and_validation(encoder: RefCounted) -> void:
	for speaker: Node in [clients[0],clients[2]]:
		if not await begin_stream(speaker): return
		for sequence: int in range(4): await send(speaker,sequence,2)
		for receiver: Node in clients:
			if receiver==speaker: continue
			var permit: Dictionary=receiver.voice.permissions.get(id(speaker),{})
			check(not permit.is_empty() and accepted(receiver,id(speaker))>0,"actual peer receives "+str(authority.sim.actors[id(speaker)].role)+"->"+str(authority.sim.actors[id(receiver)].role))
			if not permit.is_empty():
				check(is_equal_approx(float(permit.pitch),1.55 if speaker==clients[2] else 1.0) and permit.map_id=="house","permission stamps speaker pitch and authoritative map")
		check(not authority.voice.streams.has(id(clients[1])) and not authority.voice.streams.has(id(clients[3])),"sender identity cannot create another participant's stream")
		check(accepted(speaker,id(speaker))==0,"speaker does not receive relay of own voice")
		await stop(speaker)
	var speaker: Node=clients[0];var receiver: Node=clients[1]
	if not await begin_stream(speaker): return
	await send(speaker,0);await send(speaker,1)
	var highest: int=authority.voice.streams[id(speaker)].highest
	if not check(highest==1,"positive valid-frame baseline precedes malformed/replay checks"):
		await stop(speaker);return
	var before:=accepted(receiver,id(speaker))
	speaker.voice._request_control.rpc_id(1,"begin",epoch,-1)
	speaker.voice._request_control.rpc_id(1,"begin",epoch-1,-1)
	speaker.voice._request_control.rpc_id(1,"begin",0,-1)
	speaker.voice._request_frame.rpc_id(1,epoch-1,50,sample)
	speaker.voice._request_frame.rpc_id(1,epoch,1,sample)
	speaker.voice._request_frame.rpc_id(1,epoch,-1,sample)
	speaker.voice._request_frame.rpc_id(1,epoch,6001,sample)
	var stereo:=sample.duplicate();stereo[0]=stereo[0]|4
	var bad_duration:=sample.duplicate();bad_duration[0]=128
	check(not encoder.validate_frame(stereo) and not encoder.validate_frame(bad_duration),"adversarial TOCs are structurally invalid mono/duration frames")
	var oversized:=PackedByteArray();oversized.resize(161)
	var malformed:=PackedByteArray([3,0])
	check(not encoder.validate_frame(malformed),"malformed Opus framing is rejected by native validator")
	for packet: PackedByteArray in [PackedByteArray(),oversized,stereo,bad_duration,malformed]: speaker.voice._request_frame.rpc_id(1,epoch,50,packet)
	await settle(5)
	check(authority.voice.streams.has(id(speaker)) and authority.voice.streams[id(speaker)].epoch==epoch and authority.voice.streams[id(speaker)].highest==highest,"epoch replay, invalid sequence, length and TOC cannot advance server stream")
	check(accepted(receiver,id(speaker))==before,"replayed and malformed frames produce no client packet event")
	await send(speaker,20)
	await send(speaker,8)
	var out_of_order: bool=authority.voice.streams[id(speaker)].seen.has(8)
	var total:=accepted(receiver,id(speaker))
	speaker.voice.send_frame(epoch,7,sample);speaker.voice.send_frame(epoch,8,sample)
	await settle(3)
	evidence.reordering={"server_seen_8":out_of_order,"before":total,"after":accepted(receiver,id(speaker)),"server":authority.voice.streams.get(id(speaker),{}).duplicate(true)}
	check(out_of_order and accepted(receiver,id(speaker))==total,"bounded 12-frame reordering accepted once; older and duplicate packets denied")
	await stop(speaker)

func _permissions_and_mute() -> void:
	var speaker: Node=clients[0];var receiver: Node=clients[1];var other: Node=clients[2]
	if not await begin_stream(speaker): return
	for sequence: int in range(3): await send(speaker,sequence)
	var old: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	if not check(not old.is_empty(),"near listener has a permit before mute"): return
	receiver.voice.set_peer_muted(id(speaker),true)
	await settle(4)
	check(not receiver.voice.permissions.has(id(speaker)) and not authority.voice.streams[id(speaker)].recipients.has(id(receiver)),"per-peer mute revokes local and server permission")
	var muted_count:=accepted(receiver,id(speaker));var other_count:=accepted(other,id(speaker))
	await send(speaker,3)
	check(accepted(receiver,id(speaker))==muted_count and accepted(other,id(speaker))>other_count,"mute blocks only the selected receiver")
	receiver.voice.set_peer_muted(id(speaker),false)
	await settle(4)
	var renewed: Dictionary=receiver.voice.permissions.get(id(speaker),{})
	check(not renewed.is_empty() and int(renewed.permit)!=int(old.permit) and int(renewed.first_sequence)>3,"unmute in held PTT grants fresh permit and first sequence")
	# Delayed server-to-client delivery is genuinely serialized over ENet. No
	# receiver method is called locally to claim proof of network rejection.
	var before:=accepted(receiver,id(speaker))
	authority.voice._deliver.rpc_id(id(receiver),id(speaker),int(old.epoch),int(old.permit),99,sample)
	if not renewed.is_empty(): authority.voice._deliver.rpc_id(id(receiver),id(speaker),int(renewed.epoch),int(renewed.permit),int(renewed.first_sequence)-1,sample)
	await settle(3)
	check(accepted(receiver,id(speaker))==before,"late old-permit and pre-first-sequence frames are discarded by receiver")
	await send(speaker,4)
	check(accepted(receiver,id(speaker))>before,"fresh next frame resumes same PTT after unmute")
	# Permission refresh is intentionally 10 Hz; allow a full update before
	# asserting location/death decisions, without changing timer internals.
	authority.sim.actors[id(receiver)].p=Vector3(0,0,10)
	await settle(7);await send(speaker,5)
	check(not receiver.voice.permissions.has(id(speaker)),"host revokes receiver beyond acoustic range")
	place_default();await settle(7);await send(speaker,6)
	check(receiver.voice.permissions.has(id(speaker)),"returning near gains a new permission")
	authority.sim.actors[id(receiver)].alive=false
	await settle(7);await send(speaker,7)
	check(not receiver.voice.permissions.has(id(speaker)),"dead listener loses host permission")
	authority.sim.actors[id(receiver)].alive=true;authority.sim.actors[id(speaker)].alive=false
	await settle(7);await send(speaker,8)
	check(authority.voice.streams[id(speaker)].recipients.is_empty(),"dead speaker has no recipients")
	authority.sim.actors[id(speaker)].alive=true
	# A real house portal: sources stand on opposite sides, opening unobstructed.
	authority.sim.actors[id(speaker)].p=Vector3(-3,0,-8.2)
	authority.sim.actors[id(receiver)].p=Vector3(-1,0,-8.2)
	authority.sim.doors.kitchen.angle=Doors.OPEN_ANGLE
	await settle(7);await send(speaker,9)
	var opened: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	authority.sim.doors.kitchen.angle=0.0
	await settle(7);await send(speaker,10)
	var closed: Dictionary=receiver.voice.permissions.get(id(speaker),{})
	check(not opened.is_empty() and not closed.is_empty() and float(opened.cutoff)==12000.0 and float(closed.cutoff)==1200.0 and absf(float(closed.gain)/float(opened.gain)-.25)<.001,"real door angle changes server gain/filter over ENet")
	await stop(speaker);place_default()

func _resume_permission() -> void:
	var speaker: Node=clients[0];var receiver: Node=clients[1];var other: Node=clients[2]
	if not await begin_stream(speaker): return
	await send(speaker,0);await send(speaker,1)
	var old: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	var other_old: Dictionary=other.voice.permissions.get(id(speaker),{}).duplicate(true)
	if not check(not old.is_empty() and not other_old.is_empty(),"resume starts with two real current recipient permissions"): return
	other.voice.request_resume(id(speaker),epoch,int(old.permit))
	receiver.voice.request_resume(id(speaker),epoch-1,int(old.permit))
	await settle(3)
	check(receiver.voice.permissions.get(id(speaker),{}).get("permit")==old.permit and other.voice.permissions.get(id(speaker),{}).get("permit")==other_old.permit,"resume cannot renew another listener's permit or an old epoch")
	receiver.voice.request_resume(id(speaker),epoch,int(old.permit))
	await settle(3)
	var fresh: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	check(not fresh.is_empty() and fresh.permit!=old.permit and int(fresh.first_sequence)==int(authority.voice.streams[id(speaker)].highest)+1,"resume replaces own permission at server highest+1")
	check(other.voice.permissions.get(id(speaker),{}).get("permit")==other_old.permit,"resume leaves the other recipient's permit intact")
	receiver.voice.request_resume(id(speaker),epoch,int(old.permit))
	var before:=accepted(receiver,id(speaker))
	authority.voice._deliver.rpc_id(id(receiver),id(speaker),epoch,int(old.permit),99,sample)
	await settle(3)
	check(receiver.voice.permissions.get(id(speaker),{}).get("permit")==fresh.get("permit") and accepted(receiver,id(speaker))==before,"stale resume and old-permit delayed packet cannot restart delivery")
	await send(speaker,2)
	check(accepted(receiver,id(speaker))>before,"renewed recipient receives subsequent same-PTT audio")
	# Recompute current acoustics immediately on the authenticated resume request.
	authority.sim.actors[id(receiver)].p=Vector3(0,0,10)
	receiver.voice.request_resume(id(speaker),epoch,int(fresh.get("permit",-1)))
	await settle(3)
	check(not receiver.voice.permissions.has(id(speaker)),"resume cannot restore a recipient now beyond range")
	place_default();await settle(7);await send(speaker,3)
	fresh=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	receiver.voice.set_peer_muted(id(speaker),true)
	await settle(3)
	receiver.voice.request_resume(id(speaker),epoch,int(fresh.get("permit",-1)))
	await settle(3)
	check(not receiver.voice.permissions.has(id(speaker)) and not authority.voice.streams[id(speaker)].recipients.has(id(receiver)),"resume cannot bypass recipient mute")
	receiver.voice.set_peer_muted(id(speaker),false)
	await stop(speaker)

func _rate_and_finish() -> void:
	var speaker: Node=clients[3];var receiver: Node=clients[2]
	if not await begin_stream(speaker): return
	await send(speaker,0);await send(speaker,1)
	var began:=Time.get_ticks_msec()
	var before:=accepted(receiver,id(speaker))
	for sequence: int in range(2,102): speaker.voice.send_frame(epoch,sequence,sample)
	await settle(5)
	var stream: Dictionary=authority.voice.streams.get(id(speaker),{})
	var admitted: int=stream.get("seen",{}).size()
	var budget: float=12.0+float(Time.get_ticks_msec()-began)*.05
	var received:=accepted(receiver,id(speaker))-before
	check(received>0 and received<=ceili(budget) and received<100,"100-frame burst relay stays within token bucket and is not fully admitted")
	evidence.burst={"sent":100,"retained_seen":admitted,"elapsed_ms":Time.get_ticks_msec()-began,"maximum_tokens":budget,"received":accepted(receiver,id(speaker))-before}
	var rate_before: Dictionary=authority.voice._packet_rates.get(id(speaker),{}).duplicate(true)
	await stop(speaker)
	if not await begin_stream(speaker): return
	check(not rate_before.is_empty() and authority.voice._packet_rates.get(id(speaker),{})==rate_before,"stop/begin cannot refill or reset sender token bucket")
	var current_epoch:=epoch;speaker.voice.begin(epoch+1)
	await settle(2)
	check(authority.voice.streams[id(speaker)].epoch==current_epoch,"begin spam cannot replace a stream younger than 200ms")
	await settle(12)
	epoch+=1;speaker.voice.begin(epoch)
	await settle(3)
	check(authority.voice.streams[id(speaker)].epoch==epoch,"later fresh epoch replaces prior stream")
	await send(speaker,0);await send(speaker,1)
	var permit: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	speaker.voice.finish(epoch,2)
	await settle(2)
	await send(speaker,2);var count:=accepted(receiver,id(speaker))
	await send(speaker,3)
	check(accepted(receiver,id(speaker))==count and not endings[receiver].is_empty(),"finish relays final sequence and denies packets beyond it")
	await until(func()->bool:return not authority.voice.streams.has(id(speaker)) and not receiver.voice.permissions.has(id(speaker)),"finish tail expires and revokes permit",70)
	if not permit.is_empty():
		count=accepted(receiver,id(speaker))
		authority.voice._deliver.rpc_id(id(receiver),id(speaker),int(permit.epoch),int(permit.permit),2,sample)
		await settle(3)
		check(accepted(receiver,id(speaker))==count,"late frame after tail revocation cannot reach observer")

func _round_lifecycle() -> void:
	var speaker: Node=clients[0];var receiver: Node=clients[1]
	if not await begin_stream(speaker): return
	await send(speaker,0);await send(speaker,1)
	var old: Dictionary=receiver.voice.permissions.get(id(speaker),{}).duplicate(true)
	authority.sim.phase="results"
	await settle(7)
	check(authority.voice.streams.is_empty() and receiver.voice.permissions.is_empty(),"results phase revokes all active voice streams")
	# Same authenticated peers, new real generated geometry, inverted roles.
	stage("house-v1-2",["mosquito","mosquito","human","human"])
	if not await begin_stream(speaker): return
	for sequence: int in range(3): await send(speaker,sequence)
	var fresh: Dictionary=receiver.voice.permissions.get(id(speaker),{})
	check(not fresh.is_empty() and float(fresh.pitch)==1.55 and fresh.map_id=="house-v1-2","new round rebuilds pitch and map from reassigned server roles")
	var before:=accepted(receiver,id(speaker))
	if not old.is_empty(): authority.voice._deliver.rpc_id(id(receiver),id(speaker),int(old.epoch),int(old.permit),99,sample)
	await settle(3)
	check(accepted(receiver,id(speaker))==before,"previous-round permitted packet cannot cross new epoch/permit")
	await stop(speaker)
	# Real close_client invokes reset, covering state cleanup without a session.
	receiver.voice.set_peer_muted(id(speaker),true)
	await settle(3)
	var departed_id:=id(receiver)
	receiver.close_client()
	check(receiver.voice.permissions.is_empty() and receiver.voice.muted_peers.is_empty() and receiver.voice.streams.is_empty() and receiver.voice.last_epochs.is_empty(),"client reset clears voice permits, mute and epoch state")
	await settle(4)
	check(not authority.voice._receiver_mutes.has(departed_id),"peer departure clears host mute state")
	for node: Node in clients:
		for packet: Dictionary in packets[node]: check(bool(packet.bytes_match),"received payload remains the exact encoded synthetic frame")

func _finish() -> void:
	if finished:return
	finished=true
	if probe_ingress and is_instance_valid(authority): evidence.ingress=authority.voice.ingress.duplicate(true)
	for node: Node in clients:
		if is_instance_valid(node): node.close_client()
	if is_instance_valid(stranger): stranger.close_client()
	if is_instance_valid(authority) and authority.multiplayer.multiplayer_peer!=null: authority.multiplayer.multiplayer_peer.close()
	var output: String=ProjectSettings.globalize_path("res://../work/network09-voice-results.json")
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--voice-report="): output=argument.trim_prefix("--voice-report=")
	var report: Dictionary={"checks":checks,"failures":failures,"results":results,"evidence":evidence,"protocol":Net.PROTOCOL,"poll_native":multiplayer_poll,"probe_ingress":probe_ingress,"transport":"real ENet, one authority + four authenticated peers + one raw unauthenticated peer, loopback, separate SceneMultiplayer contexts","observer":"VoiceTransport.packet_received; no VoiceSession","staged":"Server simulation map/roles/positions and frozen gameplay tick only; real voice timers and RPCs","microphone":false,"playback":false,"preferences_modified":false}
	var file:=FileAccess.open(output,FileAccess.WRITE)
	if file!=null:file.store_string(JSON.stringify(report,"\t"));file.close()
	else:check(false,"report writable")
	for surface: Node in surfaces: surface.queue_free()
	await process_frame
	print("NET_VOICE09_RESULT checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
