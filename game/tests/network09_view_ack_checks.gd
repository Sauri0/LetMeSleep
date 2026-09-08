extends "res://tests/network09_map_social_checks.gd"
## Reuses only the real ENet harness; no map/social assertions are repeated.
var fresh_inputs := true

func settle(frames: int = 6, excluded: Array[Node] = []) -> void:
	for frame: int in range(frames):
		input_sequence+=1
		if fresh_inputs:
			for node: Node in clients:
				if node._had_session: node.send_input(input_sequence,Vector3.ZERO,0.0,0.0,false)
		for node: Node in peers:
			if not excluded.has(node) and node.multiplayer.multiplayer_peer is ENetMultiplayerPeer: node.multiplayer.poll()
		await physics_frame
	await process_frame

func _deliver_input(actor: Dictionary, seq: int, move: Vector3, yaw: float, pitch: float, label: String) -> bool:
	# This RPC is explicitly unreliable. A test precondition cannot demand that
	# one isolated datagram survive; repeat the same intention at the normal
	# 30 Hz cadence until observed. Keep its sequence/angles unchanged, and keep
	# the later ACK/orientation assertions exact. No production retry is added.
	var attempts:=0
	while int(actor._input_seq)<seq and attempts<60:
		guest.send_input(seq,move,yaw,pitch,false)
		attempts+=1
		await settle(2)
	diagnostics.append({"stage":label,"requested_seq":seq,"received_seq":actor._input_seq,"datagrams_sent":attempts})
	return check(int(actor._input_seq)==seq,label)

func _run() -> void:
	output=ProjectSettings.globalize_path("res://../outputs/0.9-surface-view-network")
	DirAccess.make_dir_recursive_absolute(output)
	create_timer(40).timeout.connect(func() -> void:
		if not finished: check(false,"view ACK watchdog"); _finish.call_deferred())
	authority=peer("ViewAuthority")
	var port:=44000+int(Time.get_ticks_msec()%5000)
	if not check(authority.host(port)==OK,"view authority starts ENet"):
		await _finish(); return
	owner=peer("Human")
	guest=peer("Mosquito")
	clients.assign([owner,guest])
	owner.connect_room("127.0.0.1",port,"Human","",true)
	if not await until(func() -> bool: return authority.players.size()==1 and owner._had_session,"human authenticates"):
		await _finish(); return
	guest.connect_room("127.0.0.1",port,"Mosquito",authority.room_code,false)
	if not await until(func() -> bool: return authority.players.size()==2 and guest._had_session,"mosquito authenticates"):
		await _finish(); return
	stranger=peer("UnauthenticatedView")
	var raw:=ENetMultiplayerPeer.new()
	check(raw.create_client("127.0.0.1",port,3)==OK,"raw transport opens")
	stranger.multiplayer.multiplayer_peer=raw
	await until(func() -> bool: return raw.get_connection_status()==MultiplayerPeer.CONNECTION_CONNECTED and authority.multiplayer.get_peers().has(stranger.multiplayer.get_unique_id()),"raw transport connects without joining")
	var hid: int=owner.multiplayer.get_unique_id()
	var mid: int=guest.multiplayer.get_unique_id()
	var roster: Dictionary=authority.players.duplicate(true)
	roster[hid].role="human"; roster[mid].role="mosquito"
	authority.sim=Net.Simulation.new()
	authority.sim.start(roster,{"map_id":"house","mode":"blood","human_count":1,"round_seconds":180,"blood_goal":1000})
	authority._publish(true)
	await settle()
	authority.set_physics_process(false)
	fresh_inputs=false
	var actor: Dictionary=authority.sim.actors[mid]
	for input_first: bool in [false,true]:
		var tr: Dictionary=await _depart(actor)
		if tr.is_empty(): await _finish(); return
		var baseline_action: int=actor._action_seq
		var first: int=int(tr.input_seq)+5
		var valid: Dictionary={"verb":"view_ack","revision":tr.revision,"first_input_seq":first,"yaw":float(tr.yaw)+.17,"pitch":float(tr.pitch)-.11}
		var malformed: Array[String]=[
			"<view_ack/>",
			JSON.stringify({"verb":"view_ack","revision":tr.revision}),
			JSON.stringify({"verb":"view_ack","revision":true,"first_input_seq":first,"yaw":0,"pitch":0}),
			JSON.stringify({"verb":"view_ack","revision":float(tr.revision)+.5,"first_input_seq":first,"yaw":0,"pitch":0}),
			JSON.stringify({"verb":"view_ack","revision":tr.revision,"first_input_seq":first,"yaw":null,"pitch":0}),
			JSON.stringify({"verb":"view_ack","revision":tr.revision,"first_input_seq":first,"yaw":0,"pitch":0,"player":mid}),
			JSON.stringify({"verb":"view_ack","revision":int(tr.revision)+1,"first_input_seq":first,"yaw":0,"pitch":0}),
			JSON.stringify({"verb":"view_ack","revision":tr.revision,"first_input_seq":tr.input_seq,"yaw":0,"pitch":0}),
		]
		var seq: int=baseline_action+10
		for command: String in malformed:
			guest._action.rpc_id(1,seq,command); seq+=1
			await settle(2)
			check(not actor._view_transition.acknowledged and int(actor._action_seq)==baseline_action,"malformed/stale view ACK rejected: "+command)
		owner._action.rpc_id(1,seq,JSON.stringify(valid))
		stranger._action.rpc_id(1,seq,JSON.stringify(valid))
		await settle()
		check(not actor._view_transition.acknowledged and int(actor._action_seq)==baseline_action,"human and unauthenticated peer cannot acknowledge mosquito view")
		await _deliver_input(actor,first-1,Vector3.RIGHT,-2.4,.6,"old-frame input actually reaches authority before ACK")
		check(absf(wrapf(actor.yaw-float(tr.yaw),-PI,PI))<.00001 and absf(actor.pitch-float(tr.pitch))<.00001,"pre-ACK old-frame input does not overwrite authoritative departure")
		if input_first:
			await _deliver_input(actor,first+1,Vector3.FORWARD,float(valid.yaw)+.05,float(valid.pitch)-.03,"rebased input reaches authority before its ACK")
		guest.send_view_ack(seq,int(tr.revision),first,float(valid.yaw),float(valid.pitch))
		await until(func() -> bool: return actor._view_transition.acknowledged,"production send_view_ack JSON authenticates current revision")
		check(int(actor._view_input_floor)==first,"ACK installs advertised input barrier")
		if not input_first:
			await _deliver_input(actor,first,Vector3.FORWARD,float(valid.yaw),float(valid.pitch),"first advertised input reaches authority after ACK")
			await _deliver_input(actor,first+1,Vector3.FORWARD,float(valid.yaw)+.05,float(valid.pitch)-.03,"newer rebased input reaches authority after its ACK")
		diagnostics.append({"input_first":input_first,"phase":authority.sim.phase,"first":first,"received_seq":actor._input_seq,"floor":actor._view_input_floor,"expected_yaw":float(valid.yaw)+.05,"expected_pitch":float(valid.pitch)-.03,"actual_yaw":actor.yaw,"actual_pitch":actor.pitch,"buffer":actor._view_buffer.duplicate(true)})
		check(absf(wrapf(actor.yaw-(float(valid.yaw)+.05),-PI,PI))<.00001 and absf(actor.pitch-(float(valid.pitch)-.03))<.00001,"latest corrected input survives "+("input before ACK" if input_first else "ACK before input"))
		var accepted_yaw: float=actor.yaw
		var accepted_pitch: float=actor.pitch
		guest.send_view_ack(seq,int(tr.revision),first,0,0)
		guest.send_view_ack(seq+1,int(tr.revision),first,0,0)
		guest.send_input(first-2,Vector3.ZERO,-2.0,0,false)
		await settle()
		check(actor.yaw==accepted_yaw and actor.pitch==accepted_pitch and int(actor._action_seq)==seq,"duplicate ACKs and late old input cannot recenter or consume action sequence")
		authority.server_tick+=1
		authority._publish(true)
		await settle()
		check(guest.private_latest.get("surface",{}).get("view_transition",{}).get("acknowledged",false),"own reliable private snapshot carries acknowledged revision")
		check(not owner.private_latest.has("surface") and not guest.latest.get("actors",{}).get(mid,{}).has("view_transition"),"transition and sequence barrier remain private to mosquito")
	check(Net.PROTOCOL==Inv.PROTOCOL,"view ACK uses current negotiated protocol")
	await _finish()

func _depart(actor: Dictionary) -> Dictionary:
	var mid: int=guest.multiplayer.get_unique_id()
	authority.sim.surface_motion.clear(actor)
	actor.state="flying"; actor.p=Vector3(-1.74,1.6,2.4); actor.velocity=Vector3.ZERO
	var seq: int=int(actor._input_seq)+1
	await _deliver_input(actor,seq,Vector3.ZERO,0,0,"real fresh surface input arrives")
	diagnostics.append({"stage":"surface_setup_input","requested_seq":seq,"actual_seq":actor._input_seq,"floor":actor._view_input_floor,"guest_connected":guest._client_connected()})
	guest.send_action(int(actor._action_seq)+1,"perch",0,0)
	await until(func() -> bool: return not authority.sim._pending_actions.is_empty(),"real perch RPC queues on authority")
	for frame: int in range(30):
		authority.sim.step(1.0/60.0)
		if actor.state=="perched": break
	check(actor.state=="perched" and actor._surface_normal==Vector3.RIGHT,"real approach acquires authored wall")
	actor._surface_forward=Vector3.UP
	actor._surface_view_yaw=actor.yaw
	guest.send_action(int(actor._action_seq)+1,"perch",0,0)
	await until(func() -> bool: return not authority.sim._pending_actions.is_empty(),"real departure RPC queues on authority")
	authority.sim.step(1.0/60.0)
	var tr: Dictionary=authority.sim.private_for(mid).surface.view_transition.duplicate(true)
	check(actor.state=="flying" and not tr.is_empty() and not tr.acknowledged,"departure creates pending revision through production simulation")
	return tr
